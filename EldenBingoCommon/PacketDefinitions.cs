using EldenBingoCommon;

#region Server to client

public record ServerRoomNameSuggestion(string RoomName);
public record ServerCreateRoomDenied(string Reason);
public record ServerJoinRoomAccepted(string RoomName, UserInRoom[] Users, MatchStatus MatchStatus, bool Paused, int Timer);
public record ServerJoinRoomDenied(string Reason);
public record ServerUserJoinedRoom(UserInRoom User);
public record ServerUserLeftRoom(UserInRoom User);
public record ServerUserCoordinates(Guid UserGuid, float X, float Y, float Angle, bool IsUnderground, MapInstance MapInstance);
public record ServerAdminStatusMessage(string Message, int Color);
public record ServerUserChat(Guid UserGuid, string Message);
public record ServerMatchStatusUpdate(MatchStatus MatchStatus, bool Paused, int Timer, MatchExportInfo MatchInfo);
public record ServerEntireBingoBoardUpdate(int Size, bool Lockout, BingoBoardSquare[] Squares, EldenRingClasses[] AvailableClasses, SquareMarkInfo[] MarkedSquares, MatchExportInfo MatchInfo);
public record ServerScoreboardUpdate(TeamScore[] Scoreboard);
public record ServerBingoAchievedUpdate(BingoLine Bingo);
public record ServerSquareUpdate(BingoBoardSquare Square, int Index);
public record ServerUserChecked(Guid UserGuid, int Index, int Team, int[] TeamsChecked, int MarkedAt);
public record ServerCurrentGameSettings(BingoGameSettings GameSettings);
public record ServerTeamNameChanged(Guid UserGuid, int Team, string TeamColorName, string Name);
public record ServerBroadcastMessage(string Message);
public record ServerUserChangedTeam(Guid UserGuid, int Team, string TeamColorName, UserInRoom[] Users);
public record SquareMarkInfo(int Index, int Team, Guid UserGuid, string UserName, int MarkedAt);
public record MatchExportInfo(string LobbyName, string[] Admins, PlayerExportInfo[] Players, string SquareSetJsonFileName, DateTime? MatchStartedAt, DateTime? MatchEndedAt, int? MatchEndedAtSeconds);
public record PlayerExportInfo(string Name, int Team, string TeamName, bool IsAdmin, bool IsSpectator);

#endregion Server to client

#region Client to server

public record ClientRequestRoomName();
public record ClientRequestCreateRoom(string RoomName, string AdminPass, string Nick, int Team, BingoGameSettings Settings);
public record ClientRequestJoinRoom(string RoomName, string AdminPass, string Nick, int Team);
public record ClientRequestLeaveRoom();
public record ClientCoordinates(float X, float Y, float Angle, bool IsUnderground, MapInstance MapInstance);
public record ClientChat(string Message);
public record ClientBingoJson(string Json, string FileName);
public record ClientRandomizeBoard();
public record ClientChangeMatchStatus(MatchStatus MatchStatus);
public record ClientTogglePause();
public record ClientTryCheck(int Index, Guid ForUser);
public record ClientTryMark(int Index);
public record ClientTrySetCounter(int Index, int Change, Guid ForUser);
public record ClientSetGameSettings(BingoGameSettings GameSettings);
public record ClientRequestCurrentGameSettings();
public record ClientSetTeamName(int Team, string Name);
public record ClientRequestTeamChange(int Team);

#endregion Client to server
