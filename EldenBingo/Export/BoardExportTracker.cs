using EldenBingoCommon;
using Newtonsoft.Json;

namespace EldenBingo.Export
{
    // Export state is copied from received packets, never read concurrently from UI/game controls.
    internal sealed class BoardExportTracker
    {
        private readonly object _gate = new();
        private int _size;
        private Square[] _squares = Array.Empty<Square>();
        private readonly Dictionary<(int, int), SquareMarkInfo> _marks = new();
        private MatchExportInfo _match = EmptyMatch("");
        private UserInRoom[]? _roster;
        private readonly Dictionary<int, string> _teamNames = new();
        private readonly HashSet<int> _renamedTeams = new();
        private readonly Func<long> _clock;
        private long _clockAnchor;
        private int _baseMilliseconds;
        private MatchStatus _status = MatchStatus.NotRunning;
        private bool _paused;
        private long? _publishedSeconds;

        public BoardExportTracker(Func<long>? clock = null) => _clock = clock ?? (() => Environment.TickCount64);
        public event Action<string>? Changed;
        public bool HasBoard { get { lock (_gate) return _squares.Length > 0; } }

        public void Reset(string lobby)
        {
            update(() => { _size = 0; _squares = Array.Empty<Square>(); _marks.Clear(); _match = EmptyMatch(lobby); _roster = null; _teamNames.Clear(); _renamedTeams.Clear(); setClock(MatchStatus.NotRunning, false, 0); });
        }

        public void SetClock(MatchStatus status, bool paused, int milliseconds) => update(() => setClock(status, paused, milliseconds));

        private void setClock(MatchStatus status, bool paused, int milliseconds)
        {
            _status = status;
            _paused = paused;
            _baseMilliseconds = milliseconds;
            _clockAnchor = _clock();
        }

        private long clockSeconds()
        {
            var advancing = _status > MatchStatus.NotRunning && _status < MatchStatus.Finished && !_paused;
            var milliseconds = _baseMilliseconds + (advancing ? _clock() - _clockAnchor : 0);
            return (long)Math.Floor(milliseconds / 1000d);
        }

        public void Tick()
        {
            lock (_gate)
            {
                var seconds = clockSeconds();
                if (seconds != _publishedSeconds) publish(seconds);
            }
        }

        private void publish(long seconds)
        {
            _publishedSeconds = seconds;
            Changed?.Invoke(serialize(seconds));
        }

        public void SetRoster(IEnumerable<UserInRoom> users) => update(() => _roster = users.Select(u => new UserInRoom(u)).ToArray());

        public void SetTeamName(int team, string name) => update(() =>
        {
            _teamNames[team] = name;
            _renamedTeams.Add(team);
        });

        private void setMetadata(MatchExportInfo info)
        {
            _match = info;
            foreach (var player in info.Players)
                if (!_renamedTeams.Contains(player.Team)) _teamNames[player.Team] = player.TeamName;
        }

        public void SetBoard(ServerEntireBingoBoardUpdate packet)
        {
            update(() =>
            {
                setMetadata(packet.MatchInfo);
                _size = packet.Size;
                _squares = packet.Size > 0 && packet.Squares.Length == packet.Size * packet.Size
                    ? packet.Squares.Select(s => new Square(s.Text, s.Team.ToArray(), s.PointValue)).ToArray() : Array.Empty<Square>();
                _marks.Clear();
                foreach (var mark in packet.MarkedSquares) _marks[(mark.Index, mark.Team)] = mark;
            });
        }

        public void SetMatch(ServerMatchStatusUpdate packet)
        {
            update(() =>
            {
                setMetadata(packet.MatchInfo);
                setClock(packet.MatchStatus, packet.Paused, packet.Timer);
                if (packet.MatchStatus == MatchStatus.Starting) _marks.Clear();
            });
        }

        public void SetSquare(ServerSquareUpdate packet)
        {
            update(() =>
            {
                if (packet.Index >= 0 && packet.Index < _squares.Length)
                    _squares[packet.Index] = new Square(packet.Square.Text, packet.Square.Team.ToArray(), packet.Square.PointValue);
            });
        }

        public void SetChecked(ServerUserChecked packet, string name)
        {
            update(() =>
            {
                if (packet.Index < 0 || packet.Index >= _squares.Length) return;
                _squares[packet.Index] = _squares[packet.Index] with { Teams = packet.TeamsChecked.ToArray() };
                if (packet.TeamsChecked.Contains(packet.Team))
                    _marks[(packet.Index, packet.Team)] = new SquareMarkInfo(packet.Index, packet.Team, packet.UserGuid, name, packet.MarkedAt);
                else _marks.Remove((packet.Index, packet.Team));
            });
        }

        private void update(Action change)
        {
            // Notifications stay ordered even when delayed packet callbacks overlap.
            lock (_gate)
            {
                change();
                publish(clockSeconds());
            }
        }

        public string Serialize() { lock (_gate) return serialize(clockSeconds()); }

        private string serialize(long seconds)
        {
            var players = _roster == null ? _match.Players : _roster.OrderBy(u => u.Team).ThenBy(u => u.Nick)
                .Select(u => new PlayerExportInfo(u.Nick, u.Team, _teamNames.GetValueOrDefault(u.Team, BingoConstants.GetTeamName(u.Team)), u.IsAdmin, u.IsSpectator)).ToArray();
            var admins = _roster == null ? _match.Admins : _roster.Where(u => u.IsAdmin).Select(u => u.Nick).OrderBy(n => n).ToArray();
            string position(int index) => $"C{index % _size + 1}R{index / _size + 1}";
            var marked = _marks.Values.Where(m => m.Index >= 0 && m.Index < _squares.Length && _squares[m.Index].Teams.Contains(m.Team))
                .OrderBy(m => m.Index).ThenBy(m => m.Team)
                .Select(m => new { squareName = _squares[m.Index].Text, markedBy = m.UserName, markedAt = m.MarkedAt, position = position(m.Index), pointValue = _squares[m.Index].PointValue }).ToArray();
            var remaining = _squares.Select((square, index) => (square, index)).Where(s => s.square.Teams.Length == 0)
                .Select(s => new { squareName = s.square.Text, position = position(s.index), pointValue = s.square.PointValue }).ToArray();
            var data = new
            {
                matchInfo = new
                {
                    lobbyName = _match.LobbyName,
                    admins,
                    players = players.Select(p => new { name = p.Name, team = p.Team, teamName = p.TeamName, isAdmin = p.IsAdmin, isSpectator = p.IsSpectator }).ToArray(),
                    squareSetJsonFileName = _match.SquareSetJsonFileName,
                    matchStartedAt = _match.MatchStartedAt?.ToString("O"),
                    matchEndedAt = _match.MatchEndedAt?.ToString("O"),
                    matchEndedAtSeconds = _match.MatchEndedAtSeconds,
                    matchTimeSeconds = seconds,
                    matchTime = formatTime(seconds),
                    matchStatus = _status.ToString(),
                    paused = _paused
                },
                markedSquares = _status is MatchStatus.Preparation or MatchStatus.Running or MatchStatus.Finished ? marked : marked.Take(0).ToArray(),
                remainingSquares = _status is MatchStatus.Preparation or MatchStatus.Running or MatchStatus.Finished ? remaining : remaining.Take(0).ToArray()
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        private static MatchExportInfo EmptyMatch(string lobby) => new(lobby, Array.Empty<string>(), Array.Empty<PlayerExportInfo>(), "", null, null, null);
        private static string formatTime(long seconds)
        {
            var value = Math.Abs(seconds);
            return $"{(seconds < 0 ? "-" : "")}{value / 3600:00}:{value / 60 % 60:00}:{value % 60:00}";
        }
        private sealed record Square(string Text, int[] Teams, int PointValue);
    }
}
