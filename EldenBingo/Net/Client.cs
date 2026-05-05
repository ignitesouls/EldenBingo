using EldenBingo.Net;
using EldenBingoCommon;
using Neto.Client;
using Neto.Shared;
using System.Reflection;

namespace EldenBingo
{
    public class Client : NetoClient
    {
        private Room? _room;

        private ISet<string> _delayTypes;

        public Client() : base(Properties.Settings.Default.IdentityToken)
        {
            //Always register the EldenBingoCommon assembly
            RegisterAssembly(Assembly.GetAssembly(typeof(BingoBoard)));
            registerHandlers();
            _delayTypes = new HashSet<string>()
            {
                nameof(ServerUserCoordinates),
                nameof(ServerMatchStatusUpdate),
                nameof(ServerEntireBingoBoardUpdate),
                nameof(ServerScoreboardUpdate),
                nameof(ServerScoreboardUpdate),
                nameof(ServerBingoAchievedUpdate),
                nameof(ServerSquareUpdate),
                nameof(ServerUserChecked),
                nameof(ServerAttackResult),
                nameof(ServerBattleshipTeamView),
                nameof(ServerBattleshipGameOver)
            };
            Disconnected += client_Disconnected;
        }

        internal event EventHandler? OnUsersChanged;

        internal event EventHandler<RoomChangedEventArgs>? OnRoomChanged;

        // Battleship events
        internal event EventHandler<BattleshipConfigEventArgs>? OnBattleshipConfig;
        internal event EventHandler<BattleshipTeamViewEventArgs>? OnBattleshipTeamView;
        internal event EventHandler<AttackResultEventArgs>? OnAttackResult;
        internal event EventHandler? OnAllShipsPlaced;
        internal event EventHandler<BattleshipGameOverEventArgs>? OnBattleshipGameOver;

        /// <summary>
        /// Artificial delay for all match related packets, in milliseconds
        /// </summary>
        public int PacketDelayMs { get; set; } = 0;

        public UserInRoom? LocalUser { get; private set; }
        public BingoBoard? BingoBoard => Room?.Match.Board;
        public bool IsBattleshipMode { get; private set; }

        public override string Version => EldenBingoCommon.Version.CurrentVersion;

        internal Room? Room
        {
            get
            {
                return _room;
            }
            set
            {
                if (_room != value)
                {
                    var oldRoom = _room;
                    _room = value;
                    if (_room == null)
                    {
                        LocalUser = null;
                        IsBattleshipMode = false;
                    }
                    fireOnRoomChanged(oldRoom);
                }
            }
        }

        public override string GetConnectionStatusString()
        {
            if (!IsConnected)
                return "Not connected";
            if (CancellationToken.IsCancellationRequested)
                return "Stopping...";
            if (Room == null)
                return "Connected - Not in a lobby";
            else
                return "Connected - Lobby: " + Room.Name;
        }

        public async Task RequestRoomName()
        {
            var req = new Packet(new ClientRequestRoomName());
            await SendPacketToServer(req);
        }

        public async Task CreateRoom(string roomName, string adminPass, string nickname, int team, BingoGameSettings settings)
        {
            var request = new ClientRequestCreateRoom(roomName, adminPass, nickname, team, settings);
            await SendPacketToServer(new Packet(request));
        }

        public async Task JoinRoom(string roomName, string adminPass, string nickname, int team)
        {
            var request = new ClientRequestJoinRoom(roomName, adminPass, nickname, team);
            await SendPacketToServer(new Packet(request));
        }

        public async Task LeaveRoom()
        {
            Room = null;
            FireOnStatus("Left lobby");
            await SendPacketToServer(new Packet(new ClientRequestLeaveRoom()));
        }

        public async Task PlaceShips(ShipPlacement[] placements)
        {
            await SendPacketToServer(new Packet(new ClientPlaceShips(placements)));
        }

        public async Task ConfirmShipPlacement()
        {
            await SendPacketToServer(new Packet(new ClientConfirmShipPlacement()));
        }

        protected override async void DispatchObjects(ClientModel? sender, IEnumerable<object> objects)
        {
            if (PacketDelayMs > 0 && LocalUser != null && LocalUser.IsSpectator)
            {
                var ordinaryPackets = new Queue<object>();
                var delayPackets = new Queue<object>();
                foreach (var o in objects)
                {
                    var t = o.GetType();
                    if (t?.FullName != null && _delayTypes.Contains(t.Name))
                    {
                        delayPackets.Enqueue(o);
                    }
                    else
                    {
                        ordinaryPackets.Enqueue(o);
                    }
                }
                base.DispatchObjects(sender, ordinaryPackets);
                if (delayPackets.Count > 0)
                {
                    await Task.Delay(PacketDelayMs);
                    base.DispatchObjects(sender, delayPackets);
                }
            }
            else
            {
                base.DispatchObjects(sender, objects);
            }
        }

        private void client_Disconnected(object? sender, StringEventArgs e)
        {
            Room = null;
        }

        private void registerHandlers()
        {
            AddListener<ServerUserJoinedRoom>(userJoinedRoom);
            AddListener<ServerUserLeftRoom>(userLeftRoom);
            AddListener<ServerCreateRoomDenied>(createRoomDenied);
            AddListener<ServerJoinRoomDenied>(joinRoomDenied);
            AddListener<ServerJoinRoomAccepted>(joinRoomAccepted);
            AddListener<ServerEntireBingoBoardUpdate>(entireBingoBoardUpdate);
            AddListener<ServerMatchStatusUpdate>(matchStatusUpdate);
            AddListener<ServerBattleshipConfig>(battleshipConfig);
            AddListener<ServerBattleshipTeamView>(battleshipTeamView);
            AddListener<ServerAttackResult>(attackResult);
            AddListener<ServerAllShipsPlaced>(allShipsPlaced);
            AddListener<ServerBattleshipGameOver>(battleshipGameOver);
        }

        private void userJoinedRoom(ClientModel? _, ServerUserJoinedRoom userJoined)
        {
            if (Room != null)
            {
                Room.AddUser(userJoined.User);
                fireOnUsersChanged();
            }
        }

        private void userLeftRoom(ClientModel? _, ServerUserLeftRoom userLeft)
        {
            if (Room != null)
            {
                Room.RemoveUser(userLeft.User);
                fireOnUsersChanged();
            }
        }

        private void createRoomDenied(ClientModel? _, ServerCreateRoomDenied createRoomDenied)
        {
            Room = null;
            FireOnStatus($"Create lobby failed: {createRoomDenied.Reason}");
        }

        private void joinRoomDenied(ClientModel? _, ServerJoinRoomDenied joinDenied)
        {
            Room = null;
            FireOnStatus($"Join lobby failed: {joinDenied.Reason}");
        }

        private void joinRoomAccepted(ClientModel? _, ServerJoinRoomAccepted joinAccepted)
        {
            var sameRoomAsBefore = Room != null && Room.Name == joinAccepted.RoomName;

            if (!sameRoomAsBefore)
                FireOnStatus($"Joined lobby");
            var room = new Room(joinAccepted.RoomName);
            room.Match.UpdateMatchStatus(joinAccepted.MatchStatus, joinAccepted.Paused, joinAccepted.Timer);
            foreach (var user in joinAccepted.Users)
                room.AddUser(user);

            //Store a reference to my own User
            LocalUser = room.GetUser(ClientGuid);

            //Set the new current room (which fires the RoomChanged event)
            Room = room;
        }

        private void entireBingoBoardUpdate(ClientModel? _, ServerEntireBingoBoardUpdate boardUpdate)
        {
            if (Room != null)
            {
                Room.Match.Board = boardUpdate.Size > 0 && boardUpdate.Squares.Length == boardUpdate.Size * boardUpdate.Size ?
                    new BingoBoard(boardUpdate.Size, boardUpdate.Lockout, boardUpdate.Squares, boardUpdate.AvailableClasses) :
                    null;
            }
        }

        private void matchStatusUpdate(ClientModel? _, ServerMatchStatusUpdate matchStatus)
        {
            if (Room != null)
            {
                Room.Match.UpdateMatchStatus(matchStatus.MatchStatus, matchStatus.Paused, matchStatus.Timer);
            }
            // Ensure battleship mode flag is cleared when the match leaves battleship state
            if (matchStatus.MatchStatus == MatchStatus.NotRunning || matchStatus.MatchStatus == MatchStatus.Finished)
            {
                IsBattleshipMode = false;
            }
        }

        private void battleshipConfig(ClientModel? _, ServerBattleshipConfig config)
        {
            IsBattleshipMode = true;
            OnBattleshipConfig?.Invoke(this, new BattleshipConfigEventArgs(config.Ships, config.BoardSize));
        }

        private void battleshipTeamView(ClientModel? _, ServerBattleshipTeamView view)
        {
            OnBattleshipTeamView?.Invoke(this, new BattleshipTeamViewEventArgs(view.TeamView));
        }

        private void attackResult(ClientModel? _, ServerAttackResult result)
        {
            OnAttackResult?.Invoke(this, new AttackResultEventArgs(result.Index, result.Result, result.AttackingTeam, result.DefendingTeam, result.SunkShip));
        }

        private void allShipsPlaced(ClientModel? _, ServerAllShipsPlaced msg)
        {
            OnAllShipsPlaced?.Invoke(this, EventArgs.Empty);
        }

        private void battleshipGameOver(ClientModel? _, ServerBattleshipGameOver gameOver)
        {
            OnBattleshipGameOver?.Invoke(this, new BattleshipGameOverEventArgs(gameOver.WinningTeam, gameOver.WinningTeamName));
        }

        private void fireOnRoomChanged(Room? oldRoom)
        {
            OnRoomChanged?.Invoke(this, new RoomChangedEventArgs(oldRoom, Room));
        }

        private void fireOnUsersChanged()
        {
            OnUsersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}