using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Channels;

namespace EldenBingo.Export;

internal sealed class LocalJsonPublisher : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<string> _updates = Channel.CreateBounded<string>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Task _worker;
    private Task? _disposeTask;
    private readonly object _disposeGate = new();
    public int Slot { get; }
    public int Port { get; }
    public Uri ReadUrl { get; }
    public event Action<bool, string>? StatusChanged;

    public static Uri UrlForSlot(int slot, int port)
    {
        if (slot is < 1 or > 99 || port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(slot), "Choose lobby 1–99 and a port from 1024–65535.");
        return new Uri($"http://localhost:{port}/live/lobby{slot}.json");
    }

    private LocalJsonPublisher(NamedPipeClientStream pipe, int slot, int port, string initial)
    {
        _pipe = pipe; Slot = slot; Port = port; ReadUrl = UrlForSlot(slot, port);
        _worker = Task.Run(() => run(initial));
    }

    public static async Task<LocalJsonPublisher> StartAsync(int slot, int port, string snapshot, CancellationToken token = default)
    {
        UrlForSlot(slot, port);
        var pipe = new NamedPipeClientStream(".", LocalJsonWire.PipeName(port), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            try { await pipe.ConnectAsync(300, token).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                startBroker(port);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(8));
                try { await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                { throw new IOException($"Could not start the local JSON service on port {port}. Another program may be using it. Choose a different port."); }
            }
            using var setup = CancellationTokenSource.CreateLinkedTokenSource(token);
            setup.CancelAfter(TimeSpan.FromSeconds(4));
            await LocalJsonWire.WriteAsync(pipe, slot + "\n" + snapshot, setup.Token).ConfigureAwait(false);
            var error = await LocalJsonWire.ReadAsync(pipe, setup.Token).ConfigureAwait(false);
            if (error.Length != 0) throw new IOException(error);
            return new(pipe, slot, port, snapshot);
        }
        catch { pipe.Dispose(); throw; }
    }

    private static void startBroker(int port)
    {
        var assembly = typeof(LocalJsonPublisher).Assembly;
        var current = Environment.ProcessPath ?? throw new IOException("Cannot locate the application executable.");
        var file = Assembly.GetEntryAssembly() == assembly ? current : Path.ChangeExtension(assembly.Location, ".exe");
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(file).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(assembly.Location);
        info.ArgumentList.Add("--local-json-host");
        info.ArgumentList.Add(port.ToString());
        using var process = Process.Start(info) ?? throw new IOException("Could not start the local JSON service.");
    }

    public void Publish(string json) => _updates.Writer.TryWrite(json);

    private async Task run(string latest)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                while (_updates.Reader.TryRead(out var next)) latest = next;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(4));
                await LocalJsonWire.WriteAsync(_pipe, latest, timeout.Token);
                var error = await LocalJsonWire.ReadAsync(_pipe, timeout.Token);
                if (error.Length != 0) throw new IOException(error);
                StatusChanged?.Invoke(true, $"Updated localhost {DateTime.Now:HH:mm:ss}");
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            if (!_stop.IsCancellationRequested) StatusChanged?.Invoke(false, "Local service unavailable. Stop and start sharing again. " + ex.Message);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate) return new(_disposeTask ??= dispose());
    }

    private async Task dispose()
    {
        _stop.Cancel();
        await _worker.ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await LocalJsonWire.WriteAsync(_pipe, "", timeout.Token).ConfigureAwait(false);
            await LocalJsonWire.ReadAsync(_pipe, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        finally { _pipe.Dispose(); _stop.Dispose(); }
    }
}
