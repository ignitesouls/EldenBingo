# JSON output reference

This describes the current **Export JSON** file and **Local JSON URL** response. Both use the same snapshot schema. The local listener applies an additional board-visibility check before publishing the snapshot.

## File versus local URL

| Behavior | Export JSON | Local JSON URL |
| --- | --- | --- |
| Output | A JSON file at the location chosen in the save dialog | An HTTP response on the publishing user's computer |
| Updates | One snapshot when saved; the file is not continuously rewritten | Latest snapshot published approximately once per second, including changes received from the game |
| Clock | Time at serialization, after the save dialog is accepted | Whole-second clock updates while the endpoint is enabled |
| Reading | Read the saved file | Fetch the URL again to receive newer data |
| Requirements | Joined lobby and a board received by the client; otherwise the app reports no board to export | Joined lobby and explicitly enabled local publishing; an unrevealed board may be empty |
| Lifetime | The file remains until changed or deleted | Stops on stop-sharing, leaving the lobby, disconnecting, or closing the app |

The default address is `http://localhost:5168/live/lobby1.json`. Slots 1–99 are supported. Another application instance can use `/live/lobby2.json` at the same port. The slot number is independent of the actual lobby code. Restarting the same slot and port reuses the address with fresh data. An occupied slot cannot be overwritten.

`localhost` means the reader's own computer. Giving this URL to another person does not give them access to your feed. The listener binds to IPv4 loopback and, when available, IPv6 loopback. It is HTTP, not public HTTPS, and does not require any bingo-server deployment changes.

## Match states and board visibility

The exact, case-sensitive `matchInfo.matchStatus` values are:

| State | Meaning | Square arrays | Clock |
| --- | --- | --- | --- |
| `NotRunning` | Waiting, reset, or a newly generated board before starting | Both empty, even if an administrator spectator can see the board privately | Does not advance; normally zero after reset |
| `Starting` | Initial start countdown | Both empty | Negative seconds advance toward zero |
| `Preparation` | Players can inspect the upcoming board before play starts | Revealed once the board packet reaches this client; normally all objectives are in `remainingSquares` | Negative preparation seconds advance toward zero |
| `Running` | Active match | Revealed; claims move objectives between the arrays | Zero at the start, then increases |
| `Finished` | Match stopped/finished | Revealed board and surviving claims retained | Frozen at the final time |

With zero preparation time, the usual flow is `NotRunning → Starting → Running → Finished`. Otherwise it includes `Preparation` between `Starting` and `Running`. Reset returns to `NotRunning` and immediately hides the arrays again.

**Paused is a separate boolean, not another matchStatus string.** For example, a paused match still reports `matchStatus: "Running"` with `paused: true`. Pausing freezes the clock and timed-objective progression; it does not hide an already revealed board. Preparation may also be paused.

The export tracker and local listener both allow squares only in `Preparation`, `Running`, and `Finished`. Unknown or incorrectly cased phases are hidden by the listener. Administrator spectator privileges do not bypass export hiding in `NotRunning` or `Starting`.

Status and board packets arrive separately. A new `Preparation` or `Running` snapshot can briefly contain empty arrays before the board arrives. There can be further publication/reader-polling delay. Empty arrays alone are therefore not a reliable state indicator; inspect `matchStatus` too. A missing board stays empty even in a phase that permits revelation.

## Root object

| Field | Type | Description |
| --- | --- | --- |
| `matchInfo` | object | Lobby roster, source-file name, timestamps, current phase, and clock |
| `markedSquares` | array of objects | Current surviving claims with available claim attribution; may include multiple entries at the same position in non-lockout games |
| `remainingSquares` | array of objects | Objectives currently claimed by no team |

All three fields are present. Hidden board arrays are `[]`, not `null` or omitted. A claim is a team claim; this is a current-state snapshot, not an event log or objective-completion history.

## matchInfo fields

| Field | Type | Meaning |
| --- | --- | --- |
| `lobbyName` | string | Actual lobby name/code, not the local output slot |
| `admins` | string[] | Nicknames of current administrators, including administrator spectators |
| `players` | object[] | Current lobby occupants, including spectators and administrators; see below |
| `squareSetJsonFileName` | string | Source squareset filename supplied in match metadata; may be empty before one is available. Not its contents or full filesystem path |
| `matchStartedAt` | string or null | UTC round-trip ISO 8601 timestamp when **Running** began. Still null during Starting/Preparation. Pausing does not change it |
| `matchEndedAt` | string or null | UTC round-trip ISO 8601 timestamp when the match was stopped/finished; null before then |
| `matchEndedAtSeconds` | integer or null | Nonnegative elapsed match seconds recorded at finish; null before finish |
| `matchTimeSeconds` | integer | Current whole-second match clock. Negative in countdown/preparation; nonnegative in normal Running. Obtained by flooring milliseconds / 1000 |
| `matchTime` | string | The same clock formatted as `HH:MM:SS`, prefixed with `-` when negative; hours may exceed two digits |
| `matchStatus` | string | `NotRunning`, `Starting`, `Preparation`, `Running`, or `Finished` |
| `paused` | boolean | Whether the match clock is paused |

Example clock pairs: `-60` / `"-00:01:00"`, `90` / `"00:01:30"`, and `3661` / `"01:01:01"`. These are durations, not Unix timestamps. The desktop interpolates the clock from the latest received server timer, then publishes whole-second changes. Resynchronizing from a server update can correct it.

Calculate duration using `matchTimeSeconds` or `matchEndedAtSeconds`. Subtracting start/end UTC timestamps includes real-world paused time. If the match is stopped before Running begins, `matchStartedAt` can remain null and the finish duration is clamped to zero, while the current phase clock can still be negative.

Timestamps and source-file metadata come from received match metadata, not the computer's chosen output slot. Starting a new match clears start/end metadata; changing status to NotRunning alone does not necessarily erase metadata retained by the server. Use the phase as the authority for visibility.

## players entries

| Field | Type | Meaning |
| --- | --- | --- |
| `name` | string | Current nickname |
| `team` | integer | Team index 0–8; `-1` for spectators |
| `teamName` | string | Team's current display name, including custom names |
| `isAdmin` | boolean | Administrator privileges |
| `isSpectator` | boolean | Spectator status |

Default index order: **0 Red, 1 Blue, 2 Green, 3 Orange, 4 Purple, 5 Cyan, 6 Pink, 7 Brown, 8 Yellow**. Default team names include `Team`, such as `Red Team`. A spectator's default team name is `Spectator`.

The roster updates on joins, departures, nickname changes, team changes, and administrator changes received by the client. Players are ordered by team then nickname; administrator names are sorted separately. Team-name updates are reflected in roster entries. Historical claim names need not change when a player later renames or leaves.

## markedSquares entries

| Field | Type | Meaning |
| --- | --- | --- |
| `squareName` | string | Current resolved objective text, including chosen random values and current/frozen timed values |
| `markedBy` | string | Nickname recorded for the person who performed the claim action |
| `markedAt` | integer | Elapsed match seconds at the claim, clamped to at least zero; not a wall-clock timestamp |
| `position` | string | One-based coordinate: `C1R1` is column 1, row 1 (top-left) |
| `pointValue` | integer | Objective's points; defaults to 1, or its configured nonnegative `point-value` |

Entries are ordered by board index, then team internally. In non-lockout games, different teams can claim the same objective, producing multiple entries with the same position. Teammates do not each create a separate team claim. Unclaiming removes that team's current entry; removing the last claim puts the objective back into `remainingSquares`. Claim history is not retained here.

The schema does **not** include a claim's team index or user GUID. In particular, `markedBy` can be the administrator who acted on another player's behalf. Duplicate nicknames, renamed/departed players, or administration actions make matching `markedBy` to the current roster unreliable. Do not use that inference as an authoritative team scoreboard.

## remainingSquares entries

| Field | Type | Meaning |
| --- | --- | --- |
| `squareName` | string | Current resolved objective text |
| `position` | string | One-based `C<column>R<row>` coordinate |
| `pointValue` | integer | Current configured objective points, default 1 |

Entries are in row-major board order: `C1R1`, `C2R1`, and so on, then the next row. An objective is remaining only when **no team** claims it. In non-lockout games it does not mean "still available to my team." A zero-point objective is still removed from this array when claimed.

## Random and timed text

The output contains generated display text, such as `Kill 3 Bell Bearing Hunters`, rather than the original `%num%` template or all possible random alternatives.

Timed objectives show their initial values in Preparation and progress only during Running. Pausing freezes progression. A first claim freezes that square's timed text for every team. When the original claimant removes the last claim, Resume catches up to current match time; Keep frozen leaves its value unchanged; Cancel changes nothing. The export reflects whichever result is actually applied. It does not include the timed schedule or a separate frozen-state flag.

## Complete example: a 3×3 board during Running

```json
{
  "matchInfo": {
    "lobbyName": "OMXL",
    "admins": ["Referee"],
    "players": [
      { "name": "Referee", "team": -1, "teamName": "Spectator", "isAdmin": true, "isSpectator": true },
      { "name": "Ash", "team": 0, "teamName": "Red Team", "isAdmin": false, "isSpectator": false },
      { "name": "Rune", "team": 1, "teamName": "Blue Team", "isAdmin": false, "isSpectator": false }
    ],
    "squareSetJsonFileName": "objectives.json",
    "matchStartedAt": "2026-11-18T18:00:00.0000000Z",
    "matchEndedAt": null,
    "matchEndedAtSeconds": null,
    "matchTimeSeconds": 90,
    "matchTime": "00:01:30",
    "matchStatus": "Running",
    "paused": false
  },
  "markedSquares": [
    { "squareName": "Defeat Margit", "markedBy": "Ash", "markedAt": 40, "position": "C1R1", "pointValue": 2 }
  ],
  "remainingSquares": [
    { "squareName": "Collect a Golden Seed", "position": "C2R1", "pointValue": 1 },
    { "squareName": "Kill 2 Bell Bearing Hunters", "position": "C3R1", "pointValue": 1 },
    { "squareName": "Defeat a dragon", "position": "C1R2", "pointValue": 1 },
    { "squareName": "Find 5 Sites of Grace", "position": "C2R2", "pointValue": 1 },
    { "squareName": "Collect 3 map fragments", "position": "C3R2", "pointValue": 1 },
    { "squareName": "Defeat Godrick", "position": "C1R3", "pointValue": 1 },
    { "squareName": "Obtain a talisman", "position": "C2R3", "pointValue": 1 },
    { "squareName": "Defeat Rennala", "position": "C3R3", "pointValue": 1 }
  ]
}
```

In Preparation, expect `matchStatus: "Preparation"`, a negative preparation clock, `matchStartedAt: null`, and normally all nine objectives in `remainingSquares`. In NotRunning/Starting, both square arrays are empty while metadata and roster remain available.

## Fields that are not exported

There is no separate board-size field, complete squares matrix, team ID on claims, persistent player ID, scoreboard total, bingo-line list/bonus, winner, random seed, complete lobby settings, categories, weights, raw source JSON, tooltips, classes, counters, stars, private dimming, timed schedules/frozen flags, chat, local colors/profiles, server address, or administrator credentials.

Do not treat `markedSquares.length` as the number of distinct marked positions in a non-lockout game. Deduplicate by `position` if you want that count. Likewise, adding `pointValue` values does not reproduce an authoritative team score: claim team attribution and bingo bonuses are absent.

## Local HTTP behavior and reader handling

| Request or condition | Result |
| --- | --- |
| `GET /live/lobby1.json` with that slot active | `200`, latest JSON snapshot |
| `HEAD` to an active feed | Same headers as GET, no response body |
| Missing/inactive slot while the listener is running | `404`, `{"error":"No active lobby at this URL."}` |
| `GET /health` | `200`, `{"service":"EldenBingo.LocalJson","status":"ok"}`; proves the listener runs, not that a feed exists |
| `OPTIONS` | `204`, cross-origin/preflight headers, no body |
| Unsupported write method such as `PUT` | `405`; HTTP clients cannot mutate the match |
| Invalid/non-loopback Host header | `403` |
| No listener, or port blocked/in use | Connection error rather than a match JSON object |

Responses use UTF-8 `application/json`, `Cache-Control: no-store`, and cross-origin GET headers. Hosted pages can read from a browser on the same computer subject to that browser's permissions and security restrictions. The local read endpoint does not require a login or secret token; any permitted local reader can read the published roster and revealed board.

Publishers share a hidden local listener through same-user named pipes. Current snapshots are held in memory, with a 512 KiB limit; there is no automatic live JSON file on disk. Updates coalesce to the latest state, so a claim followed by an unclaim between publications can be missed by a polling reader. This is not a guaranteed record of every event.

Poll about once per second, avoid overlapping requests, verify HTTP success, and validate the three root fields. Replace the displayed snapshot rather than appending claims forever. Clear previously displayed squares when the phase hides them or a slot disappears; a hidden array is not proof the match is complete. Treat unavailable/stale data as unavailable rather than inferring a winner.

Stopping/closing a publisher normally removes its slot promptly. An abruptly terminated publisher is removed when its pipe closes or times out (up to about ten seconds for an unresponsive publisher). Other active slots continue. With no feeds, the listener exits after about thirty seconds. During a service error, the last accepted snapshot can remain readable until the publisher disconnects; there is no `updatedAt`, revision, or heartbeat field in the JSON to prove freshness. Check the app's Local JSON status and HTTP availability too.
