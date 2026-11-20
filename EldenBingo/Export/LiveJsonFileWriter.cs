using System.Text;
using System.Threading.Channels;

namespace EldenBingo.Export
{
    internal sealed class LiveJsonFileWriter : IAsyncDisposable
    {
        private readonly string _path;
        private readonly Channel<string> _updates = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _worker;
        private int _disposed;
        public event Action<bool, string>? StatusChanged;

        public LiveJsonFileWriter(string path)
        {
            _path = Path.GetFullPath(path);
            _worker = Task.Run(run);
        }

        public void Publish(string json) => _updates.Writer.TryWrite(json);

        private async Task run()
        {
            var token = _stop.Token;
            string? written = null;
            try
            {
                while (await _updates.Reader.WaitToReadAsync(token))
                {
                    string? latest = null;
                    while (_updates.Reader.TryRead(out var next)) latest = next;
                    await Task.Delay(200, token);
                    while (_updates.Reader.TryRead(out var next)) latest = next;
                    if (latest == null || latest == written) continue;
                    while (true)
                    {
                        try
                        {
                            await WriteAtomicAsync(_path, latest, token);
                            written = latest;
                            StatusChanged?.Invoke(true, $"Updated {DateTime.Now:HH:mm:ss}");
                            break;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                        {
                            StatusChanged?.Invoke(false, "Cannot write file; retrying. " + ex.Message);
                            await Task.Delay(1000, token);
                            while (_updates.Reader.TryRead(out var next)) latest = next;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }

        internal static async Task WriteAtomicAsync(string path, string json, CancellationToken token = default)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, json, new UTF8Encoding(false), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path, true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) { await _worker.ConfigureAwait(false); return; }
            _stop.Cancel();
            _updates.Writer.TryComplete();
            await _worker.ConfigureAwait(false);
            _stop.Dispose();
        }
    }
}
