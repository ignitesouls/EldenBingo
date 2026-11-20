using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace EldenBingo.Export;

// One hidden instance of this executable owns the local listener for all app instances.
// Publishers use same-user named pipes; HTTP exposes read-only snapshots on loopback only.
internal sealed class LocalJsonBroker
{
    private sealed class Feed { public string Json; public Feed(string json) => Json = json; }
    private readonly Dictionary<int, Feed> _feeds = new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _httpSlots = new(32);
    private long _lastActivity = Environment.TickCount64;
    private readonly int _port;

    private LocalJsonBroker(int port) => _port = port;

    public static void Run(int port)
    {
        if (port is < 1024 or > 65535) return;
        using var mutex = new Mutex(true, "Local\\" + LocalJsonWire.PipeName(port), out var owns);
        if (!owns) return;
        try { new LocalJsonBroker(port).runAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is SocketException or IOException or UnauthorizedAccessException) { }
        finally { mutex.ReleaseMutex(); }
    }

    private async Task runAsync()
    {
        using var stop = new CancellationTokenSource();
        var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start();
        TcpListener? ipv6 = null;
        try
        {
            if (Socket.OSSupportsIPv6)
            {
                try { ipv6 = new TcpListener(IPAddress.IPv6Loopback, _port); ipv6.Server.ExclusiveAddressUse = true; ipv6.Start(); }
                catch (SocketException) { ipv6?.Stop(); ipv6 = null; }
            }
            var tasks = new List<Task> { acceptHttp(listener, stop.Token), acceptPipes(stop.Token) };
            if (ipv6 != null) tasks.Add(acceptHttp(ipv6, stop.Token));
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync())
            {
                lock (_gate)
                    if (_feeds.Count == 0 && Environment.TickCount64 - _lastActivity > 30000) break;
                if (tasks.Any(t => t.IsCompleted)) break;
            }
            stop.Cancel();
            listener.Stop();
            ipv6?.Stop();
            try { await Task.WhenAll(tasks); }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
        }
        finally { stop.Cancel(); listener.Stop(); ipv6?.Stop(); }
    }

    private async Task acceptPipes(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(LocalJsonWire.PipeName(_port), PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try { await pipe.WaitForConnectionAsync(token); }
            catch { pipe.Dispose(); throw; }
            lock (_gate) _lastActivity = Environment.TickCount64;
            _ = handlePublisher(pipe, token);
        }
    }

    private async Task handlePublisher(NamedPipeServerStream pipe, CancellationToken stop)
    {
        int slot = 0;
        Feed? owned = null;
        try
        {
            async Task<string> read()
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                return await LocalJsonWire.ReadAsync(pipe, timeout.Token);
            }
            var initial = await read();
            var line = initial.IndexOf('\n');
            if (line < 1 || !int.TryParse(initial[..line], out slot) || slot is < 1 or > 99) throw new InvalidDataException("Choose a lobby slot from 1 to 99.");
            var data = redact(initial[(line + 1)..]);
            lock (_gate)
            {
                if (_feeds.ContainsKey(slot)) throw new InvalidDataException($"Lobby {slot} is already in use by another app instance. Select a different lobby number.");
                owned = new Feed(data);
                _feeds[slot] = owned;
                _lastActivity = Environment.TickCount64;
            }
            await LocalJsonWire.WriteAsync(pipe, "", stop);
            while (!stop.IsCancellationRequested)
            {
                var next = await read();
                if (next.Length == 0)
                {
                    lock (_gate) { _feeds.Remove(slot); _lastActivity = Environment.TickCount64; owned = null; }
                    await LocalJsonWire.WriteAsync(pipe, "", stop);
                    break;
                }
                var json = redact(next);
                Volatile.Write(ref owned!.Json, json);
                await LocalJsonWire.WriteAsync(pipe, "", stop);
            }
        }
        catch (InvalidDataException ex)
        {
            try { await LocalJsonWire.WriteAsync(pipe, ex.Message, stop); }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        finally
        {
            lock (_gate)
            {
                if (owned != null && _feeds.TryGetValue(slot, out var current) && ReferenceEquals(current, owned)) _feeds.Remove(slot);
                _lastActivity = Environment.TickCount64;
            }
            pipe.Dispose();
        }
    }

    private static string redact(string snapshot)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(snapshot) > LocalJsonWire.MaximumSnapshotBytes) throw new InvalidDataException("The live JSON snapshot is too large.");
            var root = JsonNode.Parse(snapshot, documentOptions: new() { MaxDepth = 32 }) as JsonObject;
            if (root?["matchInfo"] is not JsonObject info || root["markedSquares"] is not JsonArray || root["remainingSquares"] is not JsonArray)
                throw new InvalidDataException("Expected a match export snapshot.");
            var phase = info["matchStatus"] is JsonValue value && value.TryGetValue<string>(out var status) ? status : "";
            if (phase is not ("Preparation" or "Running" or "Finished"))
            {
                root["markedSquares"] = new JsonArray();
                root["remainingSquares"] = new JsonArray();
            }
            return root.ToJsonString();
        }
        catch (System.Text.Json.JsonException) { throw new InvalidDataException("Invalid match JSON."); }
    }

    private async Task acceptHttp(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(token);
            if (!_httpSlots.Wait(0)) { client.Dispose(); continue; }
            _ = handleHttp(client, token);
        }
    }

    private async Task handleHttp(TcpClient client, CancellationToken stop)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            var stream = client.GetStream();
            var header = new StringBuilder();
            var buffer = new byte[1024];
            while (!header.ToString().Contains("\r\n\r\n"))
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0 || header.Length + read > 8192) return;
                header.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }
            var lines = header.ToString().Split("\r\n");
            var request = lines[0].Split(' ');
            var host = lines.Where(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)).ToArray();
            var validHost = host.Length == 1 && Uri.TryCreate("http://" + host[0][5..].Trim(), UriKind.Absolute, out var hostUri) &&
                hostUri.IsLoopback && hostUri.Port == _port && hostUri.UserInfo.Length == 0;
            var code = 404;
            var body = "{\"error\":\"No active lobby at this URL.\"}";
            if (!validHost) code = 403;
            else if (request.Length != 3) code = 400;
            else if (request[0] == "OPTIONS") { code = 204; body = ""; }
            else if (request[0] is not ("GET" or "HEAD")) code = 405;
            else
            {
                var path = request[1].Split('?')[0];
                if (path == "/health") { code = 200; body = "{\"service\":\"EldenBingo.LocalJson\",\"status\":\"ok\"}"; }
                else if (path.StartsWith("/live/lobby", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal) &&
                    int.TryParse(path[11..^5], out var slot) && slot is >= 1 and <= 99 && path == $"/live/lobby{slot}.json")
                {
                    lock (_gate)
                        if (_feeds.TryGetValue(slot, out var feed)) { code = 200; body = Volatile.Read(ref feed.Json); }
                }
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            var reason = code switch { 200 => "OK", 204 => "No Content", 400 => "Bad Request", 403 => "Forbidden", 405 => "Method Not Allowed", _ => "Not Found" };
            var headers = $"HTTP/1.1 {code} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, HEAD, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), timeout.Token);
            if (request.Length > 0 && request[0] != "HEAD") await stream.WriteAsync(bytes, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
        finally { client.Dispose(); _httpSlots.Release(); }
    }
}
