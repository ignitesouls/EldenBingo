using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using EldenBingoCommon;
using Neto.Client;
using Neto.Shared;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EldenBingoServer.Tests;

public class MatchStartTests
{
    [Fact]
    public async Task UnpausingAtTheCountdownBoundaryStillFiresTheTimer()
    {
        var settings = new BingoGameSettings(3, true, false, new HashSet<EldenRingClasses>(), 1, 0, 177, 0, 0);
        var room = new ServerRoom("Timer test", "", null!, settings);
        var elapsed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        room.TimerElapsed += (_, _) => elapsed.TrySetResult();
        room.Match.UpdateMatchStatus(MatchStatus.Starting, -60000);
        room.PauseMatch();
        room.Match.ForceTimer(0);

        try
        {
            room.UnpauseMatch();
            await elapsed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            room.Match.UpdateMatchStatus(MatchStatus.Finished, 0);
        }
    }

    [Fact]
    public async Task UploadFailureKeepsPreviousSquaresetAndStartRequiresAReplacementBoard()
    {
        var listen = new TcpListener(IPAddress.Loopback, 0);
        listen.Start();
        int port = ((IPEndPoint)listen.LocalEndpoint).Port;
        listen.Stop();

        var server = new Server(port) { IPAddresses = new[] { IPAddress.Loopback } };
        var client = new TestClient();
        server.Host();
        try
        {
            await client.ConnectTo(server.Port);
            var settings = new BingoGameSettings(3, true, false, new HashSet<EldenRingClasses>(), 1, 0, 177, 0, 0);
            await client.SendPacketToServer(new Packet(new ClientRequestCreateRoom("Test", "", "Referee", -1, settings)));
            await client.Joined.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var room = Assert.Single(server.Rooms);

            var squares = new JArray(Enumerable.Range(1, 9).Select(index => new JObject { ["name"] = $"Objective {index}" }));
            await client.SendPacketToServer(new Packet(new ClientBingoJson(squares.ToString(), "valid.json")));
            var uploaded = await client.NextStatus();
            Assert.Contains("successfully uploaded", uploaded.Message);
            var validBoard = Assert.IsType<ServerBingoBoard>(room.Match.Board);
            var validGenerator = room.BoardGenerator;
            var validCategories = room.CategoryConfig;

            await client.SendPacketToServer(new Packet(new ClientBingoJson("[]", "empty.json")));
            Assert.Contains("Could not generate", (await client.NextStatus()).Message);
            Assert.Same(validBoard, room.Match.Board);
            Assert.Same(validGenerator, room.BoardGenerator);
            Assert.Same(validCategories, room.CategoryConfig);
            Assert.Equal("valid.json", room.SquareSetJsonFileName);

            await client.SendPacketToServer(new Packet(new ClientSetGameSettings(settings with { BoardSize = 4 })));
            Assert.Contains("Could not generate", (await client.NextStatus()).Message);
            await client.SendPacketToServer(new Packet(new ClientChangeMatchStatus(MatchStatus.Starting)));
            Assert.Contains("Could not generate", (await client.NextStatus()).Message);
            Assert.Equal(MatchStatus.NotRunning, room.Match.MatchStatus);
            Assert.Same(validBoard, room.Match.Board);

            await client.SendPacketToServer(new Packet(new ClientSetGameSettings(settings with { PreparationTime = 1 })));
            Assert.Contains("New lobby settings", (await client.NextStatus()).Message);
            var player = new TestClient();
            await player.ConnectTo(server.Port);
            await player.SendPacketToServer(new Packet(new ClientRequestJoinRoom("Test", "", "Player", 0)));
            await player.Joined.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await client.SendPacketToServer(new Packet(new ClientChangeMatchStatus(MatchStatus.Starting)));
            Assert.Empty((await player.NextBoard(TimeSpan.FromSeconds(5))).Squares);
            var revealed = await player.NextBoard(TimeSpan.FromSeconds(15));
            Assert.Equal(9, revealed.Squares.Length);
        }
        finally
        {
            await server.Stop();
        }
    }

    private sealed class TestClient : NetoClient
    {
        private readonly Channel<ServerAdminStatusMessage> _status = Channel.CreateUnbounded<ServerAdminStatusMessage>();
        private readonly Channel<ServerEntireBingoBoardUpdate> _boards = Channel.CreateUnbounded<ServerEntireBingoBoardUpdate>();
        public TaskCompletionSource Joined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override string Version => EldenBingoCommon.Version.CurrentVersion;

        public TestClient()
        {
            RegisterAssembly(typeof(BingoBoard).Assembly);
            AddListener<ServerJoinRoomAccepted>((_, _) => Joined.TrySetResult());
            AddListener<ServerAdminStatusMessage>((_, message) => _status.Writer.TryWrite(message));
            AddListener<ServerEntireBingoBoardUpdate>((_, board) => _boards.Writer.TryWrite(board));
        }

        public async Task ConnectTo(int port)
        {
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Connected += (_, _) => connected.TrySetResult();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (await Connect(new IPEndPoint(IPAddress.Loopback, port)) == ConnectionResult.Connected)
                {
                    await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    return;
                }
                await Task.Delay(50);
            }
            throw new InvalidOperationException("The loopback server did not start.");
        }

        public Task<ServerAdminStatusMessage> NextStatus() =>
            _status.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public Task<ServerEntireBingoBoardUpdate> NextBoard(TimeSpan timeout) =>
            _boards.Reader.ReadAsync().AsTask().WaitAsync(timeout);
    }
}
