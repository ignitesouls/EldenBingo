namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private TextBox? _guideSearch;
    internal void SelectHelpGuides()
    {
        _settingsSearch.Clear();
        tabControl1.SelectedTab = _categories.Single(c => c.Key == "guides").Page;
    }
    private static readonly (string Title, string Text)[] GuideTopics =
    {
        ("Getting started", @"Create and use a squareset

1. Create a UTF-8 text file ending in .json. Use a JSON editor or a plain-text editor, not a Word document.
2. Put objectives inside a squares array. Older files that contain just the array are also supported.
3. Supply enough usable objectives: a 5×5 board needs 25. Extra objectives allow selection variety. Category/region restrictions and unchosen forced-center candidates can reduce the usable pool.
4. Open Settings → Squareset testing to test the file with your board size and bingo game options. Review failures before using it in a match.
5. As lobby administrator, use Browse beside Upload Bingo JSON, then Upload. Start Match when everyone is ready. Randomize New Board generates another board from the uploaded file.

Basic wrapped format:
{
  ""squares"": [
    { ""name"": ""Defeat Margit"" },
    { ""name"": ""Collect a Golden Seed"" }
  ]
}
This short example illustrates the format; add enough objectives for your chosen board size.

JSON uses double quotes. Separate entries with commas and avoid a trailing comma after the final entry. Colors are customized in Settings; they are not part of the squareset JSON.

Which guide should I read?
JSON options explains fields, categories, regions, scoring, and center rules. JSON options also covers changing requirements, timed variables, and unclaim choices. Testing explains local simulations and diagnostics. Colors and Color JSON cover appearance and sharing. JSON options includes worked examples. Board controls covers shortcuts, focus, search, and pop-outs. Local JSON URL explains live data for overlays. Troubleshooting links common symptoms to checks.

Bingo game options
Create Lobby and Edit Lobby Settings control board size, lockout, preparation time, random seed, classes, category caps, and bingo bonus points. Match presets save these together. Timed-square unclaims use a per-click confirmation instead of a lobby-wide resume switch.

A seed of Random (0) chooses fresh randomness. A fixed seed reproduces a sequence of boards for the same JSON/settings; regenerating advances that sequence."),
        ("JSON options", @"Squareset fields

name — Required objective text. Blank names are skipped.
tooltip — Optional hint shown when hovering over the objective.
weight — Positive number, default 1. Larger values generally make an objective more likely to be selected. Constraints and center rules also affect its frequency.
point-value — Nonnegative whole number, default 1. A claim awards this many points; unclaiming removes them. Custom values are displayed on the square. Bingo bonuses remain separate.
category / categories — Give an objective one category or several. The category cap in bingo game settings and any JSON-specific limits apply to these tags.
region / regions — Optional region tags used by setRegionLimits.

Example objective:
{
  ""name"": ""Defeat Margit"",
  ""tooltip"": ""Stormveil approach"",
  ""weight"": 2,
  ""point-value"": 2,
  ""categories"": [""Bosses"", ""Limgrave""],
  ""region"": ""Limgrave""
}

Center rules on odd-sized boards
center: 1 — One forced-center candidate is selected using weighted ordering. Other forced-center candidates are excluded from that board.
center: 0 — The objective can appear anywhere except the middle.
No center field — Normal selection, including the middle when no forced candidate exists.
Even-sized boards have no single center, so center rules do not apply. Forced centers are chosen before ordinary category/region filtering and count toward subsequent limits.

Root-level category rules (beside squares):
""categoryLimits"": { ""Bosses"": 4 },
""categoryMinimums"": { ""Bosses"": 2 }
The older keys ""category limits"" and ""category minimums"" are also accepted. Objectives with multiple categories count toward each. Avoid minimums that exceed caps or available board slots.

Root-level distinct-region rules:
""setRegionLimits"": [
  { ""name"": ""Route"", ""regionLimit"": 2,
    ""regionNames"": [""Limgrave"", ""Liurnia"", ""Caelid""] }
]
This permits at most two distinct regions from that group. It does not limit the number of objectives within a chosen region.

Random tokens and timed tokens

Random token arrays keep their original behavior:
{
  ""name"": ""Kill %num% Bell Bearing Hunters"",
  ""num"": [""2"", ""3""]
}
One value is chosen during board generation. It does not change during the match.

Use a timed object to change an unclaimed objective:
{
  ""name"": ""Kill %num% Bell Bearing Hunters"",
  ""num"": {
    ""mode"": ""timed"",
    ""stages"": [
      { ""at-seconds"": 0, ""value"": ""2"" },
      { ""at-seconds"": 300, ""value"": ""3"" }
    ]
  }
}
This starts at 2 and changes to 3 at five minutes if unclaimed. Stage times are absolute elapsed match seconds, not delays after the previous stage. Start at 0 and use strictly increasing whole-second times. Values can be strings or numbers. Multiple timed tokens and ordinary random tokens can share an objective.

Timing and claims
Countdown/preparation do not advance stages. Pausing stops progression. Finished matches do not advance. The first claim freezes all timed tokens on that square for every team, including non-lockout games. Stars and counters do not freeze an objective.

Undoing an accidental claim
When the player who made the last claim clicks it to unclaim, a dialog offers three choices:
Resume timed square — remove the claim and catch up to the current match time. The requirement may increase immediately; the clock does not restart from zero.
Keep frozen — remove the claim but keep the current objective unchanged.
Cancel — leave the claim and its frozen value untouched.
Keep frozen is the default Enter action. There is no lobby-wide resume switch. If another team still claims the square, it stays frozen. Clearing somebody else's claim does not offer resumption. A new claim freezes the value again. The server validates the last claim's owner even if a client asks to resume it.

Hover over a timed square to read its timeline and frozen state. Changes are sent to both board windows and Local JSON URL. Update the desktop app and standalone server to use these rules.

Worked examples and a quick timed test

Timed values with a normal random variable
{
  ""name"": ""Kill %num% %boss%"",
  ""boss"": [""Bell Bearing Hunters"", ""Night's Cavalry""],
  ""num"": {
    ""mode"": ""timed"",
    ""stages"": [
      { ""at-seconds"": 0, ""value"": 2 },
      { ""at-seconds"": 10, ""value"": 3 },
      { ""at-seconds"": 20, ""value"": 4 }
    ]
  },
  ""point-value"": 2,
  ""center"": 1
}
The boss type is selected once. Only num changes. The objective is worth two points at every stage; point-value is not a token. center: 1 guarantees it appears in the middle of an odd-sized board when it is the only forced-center candidate.

To test it quickly
1. In an external text editor, create a squareset with at least 25 usable objectives for a 5x5 board.
2. Replace one objective with the timed example above. Keep it as the only center: 1 candidate so it is guaranteed to appear. Save as timed-test.json.
3. In Squareset testing, select the saved file and choose a 5x5 board with zero preparation time. Run test and check the failure diagnostics. The tester samples timed values at 00:00; use a match to check later transitions.
4. In your lobby, Browse to the file and Upload. Start Match and wait for Running; the initial countdown does not count toward the token deadlines.
5. Leave the center unclaimed: it should change at 10 and 20 elapsed match seconds.
6. Try another match and claim it before 10 seconds. It should remain at its claimed value. Click to unclaim and choose Resume to catch up, Keep frozen to leave the value, or Cancel to keep the claim.
7. Pause before a deadline, wait in real time, then unpause. The match clock, not wall-clock waiting, controls the transition.

Combining categories
An objective tagged categories: [""Bosses"", ""Limgrave""] counts toward both. A root categoryLimits Bosses: 4 and categoryMinimums Bosses: 2 asks for between two and four Bosses objectives. The lobby-wide category cap can impose a tighter maximum.
Do not add minimums together to estimate slots blindly: a multi-category objective may satisfy several minimums. Use the tester's failure diagnostics to see which constraints actually block selection.

Weight experiments
Give two otherwise similar objectives weights 1 and 4, then run many boards with a fixed seed. The heavier one is generally more frequent, but category limits, center rules, region availability, and board size can change the relationship. Compare using the same attempts and settings."),
        ("Testing", @"Check your squareset before a bingo game

Open Settings → Squareset testing → Test squareset randomization, or use Quick settings in the toolbar.
Run a test
1. Choose JSON selects a saved local squareset file.
2. Set the same board size, category cap, seed, and class options you plan to use in your bingo game.
3. Choose Boards to attempt (1 to 100,000). Start with 1,000 for a quick overview; increase it to investigate rare selections.
4. Choose Run test. Cancel test stops the run; run again for a complete report.
5. Check Generation failures first, then compare objective and category frequencies.

Local processing and privacy
This runs entirely on your computer and works while disconnected. It reads the saved file and runs the same generator used for matches on a background task, using separate in-memory models and random state. It does not upload JSON, contact a server, send a randomize request, change the lobby board, or advance the live lobby's random sequence. An existing connection may continue its normal traffic independently of the test.

Reading the result tabs
Resolved objectives — Shows observed text after random tokens are resolved, such as Kill 3 night’s cavs and Kill 4 night’s cavs, with separate frequencies. Unobserved combinations are not listed here.
Source objectives — Original templates and combined totals, including objectives selected zero times. Duplicate names from different JSON entries keep separate source indexes.
Categories / Regions — Occurrences, average count per successful board, and percentage of successful boards containing each tag.
Generation failures — Explains insufficient objective pools, conflicting category minimums/caps, restrictive region groups, and missing center candidates.
Selection diagnostics — Per-objective counts for category/region rejection, unchosen forced centers, center-placement rejection, never reached, and considered but passed over. Picks on failed boards are listed separately.

Understanding the numbers
Frequencies use successful boards only. Diagnostic counts cover all attempts, once per objective per reason; reasons may overlap. Timed-token frequencies measure the values at 00:00, and the Timing column shows later stages.

Inspect one category
Choose a category above the result tables to show its objectives in Resolved objectives, Source objectives, and Selection diagnostics. Membership matches complete category tags, including objectives tagged with more than one category. Source objectives includes entries selected zero times; Resolved objectives only includes variants actually observed.
In Categories, select a row and choose Show selected category's objectives, or double-click it, to open the matching Source objectives. This clears the text search. The dropdown can also be combined with a text search. Show all removes only the category filter.
Filtering never reruns generation or changes counts, percentages, or sort order. Category/region totals and generation failures continue to describe the full run. CSV exports include the selected category and the filtered objective rows.

Search, sort, preview, and export
Search filters all result tables. Click a column heading to sort ascending/descending; numeric columns sort by number. View sample board previews a successful board, including tooltip hints. Export shown results saves the current filtered/sorted tables as CSV.

Comparing changes fairly
More attempts make the estimates more useful, but frequency is not a guarantee. To compare edits, keep the same seed, board size, and attempt count. A fixed seed reproduces a sequence of attempts, not one identical board repeated each time. Random (0) starts a fresh random sequence. Save your file changes before rerunning; each run reads the saved file once. A small sample can miss rare objectives even when they are eligible."),
        ("Colors", @"Customize your board colors

All appearance and keyword colors are client-side. Other players can use different palettes for the same teams. Changing a color does not rename teams or change scoring.

Team colors and square styling
1. Open Settings → Team colors & squares.
2. In Team colors, click one of the nine team buttons to choose its color. Default, Vivid, and Pastel provide starting palettes. Protanopia, Deuteranopia, and Tritanopia offer editable color-blind starting palettes; selecting one enables automatic text contrast. Choose what looks clearest for you and adjust individual colors as needed.
3. Team colors contains Empty squares, Square text, and Borders / gaps colors. Square styling contains border thickness, spacing, and hover brightness.
4. Enable Automatically contrast text on single-team squares to choose light/dark text for readability. This also keeps the team selector labels readable.
5. Click Save settings to apply your edits. Cancel discards staged changes.

Save and share palettes
Open Saved profiles, enter a profile name, and choose Save as new. Select an existing profile and Update selected to replace it. Save settings commits the profile library. Switch saved profiles immediately from Quick settings in the toolbar.
Import profiles loads a shared/backed-up profile file; Export profiles can export a selected palette or back up profiles and the current layout.

Keyword colors
1. Open Settings → Text & effects.
2. Open Keyword colors and choose Edit current rules. Rules, intensity, and saved keyword profiles are together.
3. Add a keyword such as Bell Bearing, then click its color cell to choose a color. Rule order controls which matching color wins; put more specific rules earlier.
4. Confirm the editor, adjust keyword color intensity, and Save settings.
Keyword rules tint unclaimed objective text. Claimed squares use the team text/contrast settings instead.

Keyword profiles store rules, order, colors, and intensity. Save as new / Update selected works like team palettes; Quick settings switches them independently. An empty keyword profile turns keyword highlights off. Import/export can share keyword profiles too.

Fonts, board sizes, and layouts
Text & effects controls fonts and highlights. Board & pop-out size controls dimensions. Layouts saves presentation settings and window arrangements. Focus layouts temporarily hide player/admin controls and can keep score, timer, or chat visible.

Pop-out boards stay always on top. While a modal dialog is open, they hide temporarily so the dialog remains accessible."),
        ("Color JSON", @"Understand color-profile JSON

Squaresets and color profiles are different documents. Do not put color fields into an objective and expect them to change the board. Appearance is local to each player.

Creating and editing color-profile files
Use Settings > Team colors & squares or Text & effects to edit colors visually. Export profiles creates a valid JSON archive for sharing, backup, or editing in an external text editor.
Its format must be EldenBingo.Profiles and version must be 1. colorProfiles stores team palettes, keywordProfiles stores ordered text-color rules, and layouts stores optional presentation profiles.
Start from an exported profile rather than guessing every palette field. teamColors contains exactly nine numeric colors in the application's team order. emptyColor, textColor, and borderColor are numeric colors too; autoContrast is true or false.
Readable keyword colors
Keyword rules use the same readable colors as keyword-color JSON files: a named color such as Yellow, or an RGB string such as 255, 128, 192. RGB channels must be whole numbers from 0 to 255. Unknown names and transparent colors are rejected. Keyword and Color property names are used in new exports; existing lowercase names still work on import.
Exports use a standard color name when it matches the color, otherwise R, G, B. Old signed ARGB integers remain supported, so existing keyword profiles still load. Keyword rules inside layout backups use the same format. Profiles must be opaque; intensity controls highlighting strength separately.
Team palette fields (teamColors, emptyColor, textColor, borderColor) retain their numeric ARGB format. Use the visual pickers to edit these, then export a profile.

Minimal keyword profile archive
{
  ""format"": ""EldenBingo.Profiles"",
  ""version"": 1,
  ""colorProfiles"": [],
  ""layouts"": [],
  ""keywordProfiles"": [
    {
      ""name"": ""Boss highlights"",
      ""intensity"": 80,
      ""rules"": [
        { ""Keyword"": ""Acquire an Exultation Talisman"", ""Color"": ""Yellow"" },
        { ""Keyword"": ""Kill Radahn"", ""Color"": ""255, 128, 192"" }
      ]
    }
  ]
}
The example uses yellow and a custom pink. Keyword matching is case-insensitive and uses the first matching rule. It tints the whole unclaimed objective text, not just the matching word. intensity ranges from 0 to 100. Claimed squares use team text/contrast rules.

Apply safely
Choose Import profiles in Settings, select the saved archive, select the imported profile, and Save settings. The importer validates the archive before staging it. Import stages a copy and renames name collisions; it does not overwrite the existing saved palette. If you cancel Settings, the imported copy is discarded.
Export selected profiles to share just one palette or keyword set. Backups can include layouts, window positions, fonts, and the current presentation settings. No server/admin credentials are included in profile archives."),
        ("Troubleshooting", @"Troubleshooting squaresets and colors

The JSON will not parse
Read the parsing error in the tester and use an external JSON editor to locate the problem. Check double quotes, commas between entries, matching brackets, and the final entry's trailing comma. JSON does not support comments. A token such as %num% needs an exact matching num property on that objective; names and token keys are case-sensitive.

Not enough objectives / no valid board
A 5×5 board needs 25 usable entries. Blank names are skipped. On an odd board with multiple center: 1 candidates, only the chosen candidate remains available. Category and region rules can reduce the pool further. Increase the pool, relax a conflicting cap, or inspect the failure detail before changing weights.

Category minimum cannot be met
Check that enough entries carry the exact category, that its maximum is at least its minimum, and that all required selections fit the board. Region rules can prevent otherwise suitable category entries from being selected. The failure tab reports actual blockers; several reasons can describe one failed attempt.

An objective is never selected
Check weight is positive. Read Selection diagnostics: Never reached means the board filled before the selector examined it; Category/Region rejected means it was blocked. Unchosen forced center is expected for losing center: 1 candidates. Timed values in the tester are sampled only at 00:00; later timed stages are not separate starting variants.

A timed objective did not change
Confirm the JSON uses mode: timed rather than an array, the first stage is at zero, and later times increase. Confirm Running has started, the match is not paused/finished, and the square has not frozen after a claim. Hover to read its timeline. Ordinary arrays never change after generation.

The resume dialog did not appear
The match must still be Running (it may be paused). The choice is offered to the player who made the last remaining claim on a timed square. Another team still claiming the square, a teammate clearing somebody else's claim, or a normal objective will not offer resumption. Keep frozen removes the claim without progressing; Cancel does not unclaim. Update both client and standalone server when using new timed behavior.

Colors do not change as expected
Save settings after editing. Team colors and keyword profiles are independent. Put specific keyword rules before broad ones, and check intensity is not zero. Keyword tints apply to unclaimed squares; claimed single-team text follows automatic contrast. Use Quick settings to inspect which saved profiles match the current appearance.

My file saved but the lobby did not change
Saving edits only the local file. Use the lobby's Browse and Upload controls to load it. Run test again to reread the latest saved file; edits made during a run do not affect that run. Importing color JSON also requires reviewing/selecting the profile in Settings and saving it.

I cannot click a dialog while the pop-out is open
The pop-out should hide while a modal dialog is open and restore afterward. Keep the application updated; the pop-out is always on top but should not block confirmations."),
        ("Board controls", @"Board controls and viewing layouts

Private square dimming
Middle-click a revealed square to dim it, then middle-click again to restore it. Use this to visually set aside objectives without claiming them. Dimming appears in both your main board and pop-out and stays private to this application instance.
F4 is the default Dim / restore square shortcut. It toggles the square selected by navigation keys or mouse hover. Change or clear it in Settings > Keyboard. Settings > Keyboard > Mouse controls independently enables or disables middle-click dimming. Scroll-wheel counter behavior is grouped there too.
Dimming does not change scores, claims, stars, counters, timed objectives, or JSON exports. It clears when the board is replaced or you leave the lobby; it is not saved across sessions. Available from Preparation onward, including for spectators who can see the board.

Default shortcuts and disabling bindings
New default bindings cover every action: arrows and numpad for movement, Space to claim/unclaim, numpad Multiply to star, numpad Add/Subtract for counters, F4 to dim/restore, F3 to find, and F10 for focus. Existing custom bindings are preserved; F4 is not taken from another action.
Use the clear button beside an action to disable that shortcut. Clear all bindings removes every keyboard shortcut and disables middle-click dimming; Save settings keeps that choice after restarting. Restore defaults re-enables the complete preset and middle-click dimming. Arrow defaults and Numpad defaults offer alternate starting points. Cancel discards staged changes.

Keyboard navigation and actions
Open Settings > Keyboard > Key bindings to assign movement, diagonal movement, mark/unmark, star/unstar, counter adjustments, dim/restore, focus mode, and board search. Select an action's binding and press the desired key. Use its clear button to remove the binding. If a key is already assigned, clear its previous binding before assigning it to another action. Escape cancels key capture.
Left Ctrl and Right Ctrl can be assigned independently as action keys. Click a binding, press the desired Ctrl key, then Save settings. The Control modifier checkbox is separate: it requires Ctrl to be held while using a binding.
Required Shift, Control, or Left Alt modifiers must be held for board hotkeys to run. Save settings applies changes; Cancel discards staged edits. Existing settings can affect the bindings, so inspect the displayed keys instead of assuming every installation uses the defaults.
Keyboard navigation highlights a square for board actions. By default, mouse-wheel counter changes require the pointer to actually hover over a square. Enable Allow scrolling the keyboard-selected square in Settings > Keyboard > Mouse controls to also target the square selected with navigation keys when the pointer is elsewhere. Actual mouse hover takes priority. This option is off by default and applies after Save settings. Typing in the app or opening a dialog pauses these controls.

Find a square
Use Find square in the toolbar or the Search board binding (F3 by default when available). Type part of an objective to highlight matches in place. The board keeps its original arrangement. Search is available after the board is revealed to you and does not expose a hidden board. Clear the text to remove highlights; close the search row when finished.

Focus layouts
Use Focus in the toolbar or the Toggle focus mode binding (F10 by default when available). The dropdown offers Board only, Board + score & timer, and Board + score, timer & chat layouts. Choose a layout to enter focus mode. Toggle Focus again to return to the normal lobby view.
Focus gives the board more room by hiding the player list and administrator controls. It changes your display only: the match and connection continue normally.

Pop-out board
Open the pop-out to place the board in another window, for example on a second monitor. It stays always on top and follows the same match and local appearance settings. Its toolbar can wrap when the window becomes narrow.
While a confirmation or file dialog is open, the pop-out hides temporarily so the dialog can be reached. Close or cancel the dialog to restore it. Closing the pop-out does not leave the lobby.

Presets and presentation settings
Match presets save bingo game rules in Create Lobby or Edit Lobby Settings. Layouts saves presentation settings such as fonts, dimensions, and window positions. Team palettes and keyword profiles switch independently through Quick settings. A display layout does not change the match rules or your administrator access.

Sound controls
Settings > Audio > Event sounds provides separate volume and mute controls for claims/unclaims, bingo, start, pause/resume, and finish. Each event volume is multiplied by the master volume. Browse selects your own WAV or MP3; Test previews the staged file. Start, pause/resume, and finish have no bundled sound and remain silent until configured. Clear removes their custom file. Claims/unclaims and bingo retain their original sounds; Original restores them after a custom choice. A custom claim file replaces every claim/unclaim variant. Keep custom files at their selected paths. Save settings applies file choices; Cancel discards them. Pause and resume share one file. Playback contains the output device, master volume, and master sound toggle. The master sound toggle still controls gameplay sounds."),
        ("Local JSON URL", @"Live match data on your computer

Start a local feed
1. Join the lobby you want to publish, then choose Local JSON URL.
2. Select Lobby number 1 and port 5168 unless you need another slot or port.
3. Keep Enable local JSON URL for this lobby checked and choose Start local URL.
4. Open the dialog again and choose Copy JSON URL. Paste the address into your overlay or page's data source.

Stable addresses and multiple lobbies
Lobby 1 at the default port uses:
http://localhost:5168/live/lobby1.json
Lobby 2 uses:
http://localhost:5168/live/lobby2.json
The number is a local output slot, not your bingo game's lobby code. The same slot and port give the same address after restarting. For a second open application instance, select another unused slot. An occupied slot cannot be overwritten. Stop the feed before changing its slot or port.

Who can read the URL?
The endpoint runs on your computer and requires no changes to the bingo server. localhost refers to the computer reading the URL, so sending this address to another person does not give them your data. Keep the app and lobby open while using the feed.
A hosted website can request this local feed from a browser on your computer, subject to that browser's local-network and security permissions. Cross-origin GET support does not override browser restrictions. This is a local HTTP address, not a public HTTPS endpoint.

Updates and board privacy
The feed includes lobby players and their teams, match status, claims, objective point values, and the per-second match clock. It does not include scoreboard totals, bingo bonuses, or a team ID on each claim. See Export reference for the exact fields and limitations. A consuming page must fetch again to receive changes; a single request is only a snapshot. Avoid caching responses.
Squares remain hidden in NotRunning and Starting (countdown). They become available in Preparation, Running, and Finished; resetting the match hides them again. Pausing stops elapsed match time. Timed-objective text follows the match's current state.

Stop publishing
Open Local JSON URL, clear Enable local JSON URL for this lobby, and choose Stop local URL. Leaving the lobby, disconnecting, or closing the app also removes that feed. Publishing is enabled explicitly for each joined lobby.

Manual export versus live data
Export JSON saves one snapshot to a chosen file. It does not continuously rewrite that file. Use Local JSON URL for an updating data source. Your page still needs its own refresh or polling logic.

If a page shows no data
Confirm the feed is running, the slot and port match the pasted URL, and the browser is on the same computer. Try opening the URL directly to inspect the JSON. Check browser permissions if direct access works but a hosted page cannot fetch it. Empty board arrays in NotRunning and Starting are expected. With preparation disabled, the board first appears in Running. A phase update may briefly arrive before the board packet; wait for the next snapshot."),
        ("Export reference", @"JSON fields, states, and timing

File and URL use the same schema
Export JSON saves one snapshot after the save dialog is accepted. It requires a joined lobby and a received board; it reports no board to export otherwise. The saved file is not continuously updated.
Local JSON URL serves the latest snapshot on this computer, approximately once per second. Enable it for the joined lobby and fetch again to receive updates. Rapid changes can coalesce; this is not an event log. A local output slot is independent of the actual lobby code.

Match states and square visibility
NotRunning — Waiting or reset. Both square arrays are empty, even for administrator spectators who privately see a board.
Starting — Initial countdown. Both square arrays stay empty; the clock is negative and approaches zero.
Preparation — The upcoming board is revealed. Normally every objective is in remainingSquares. The preparation clock is negative and approaches zero.
Running — Revealed board. The clock starts at zero and increases; claims update the square arrays.
Finished — Revealed board and surviving claims remain; the clock freezes.
With no preparation time, the flow skips Preparation and reveals at Running. Resetting to NotRunning hides old squares again. Missing board data still produces empty arrays in a visible phase; the board packet may arrive shortly after the status packet.

Pausing and clocks
Paused is not a separate status. A paused running match reports matchStatus: Running and paused: true. Pausing freezes the phase clock and timed-objective progression without hiding revealed squares.
matchTimeSeconds is a signed whole-second duration, not a Unix timestamp. matchTime formats it as HH:MM:SS, such as -00:01:00 during preparation or 00:01:30 after 90 seconds. Milliseconds are floored to whole seconds. The client interpolates from the latest server timer and can correct its clock after another status update.
Use matchTimeSeconds or matchEndedAtSeconds for bingo game duration. Subtracting the UTC timestamps includes real-world paused time.

Root fields
matchInfo — Object containing the lobby roster, source filename, phase, timestamps, and clock.
markedSquares — Array of current surviving claims with available attribution. A non-lockout position can appear once for each claiming team.
remainingSquares — Array of objectives claimed by no team. These are not objectives still available specifically to your team.
All three root fields are present. Hidden square arrays are [], not null or omitted.

matchInfo fields
lobbyName — Actual lobby name/code.
admins — Array of administrator nicknames, including administrator spectators.
players — Current occupants, including spectators and administrators. Each entry contains name (nickname), team (integer 0-8, or -1 for spectator), teamName (current display name), isAdmin (boolean), and isSpectator (boolean).
squareSetJsonFileName — Squareset filename from match metadata; may be empty. It is not the source JSON contents or full path.
matchStartedAt — UTC ISO 8601 timestamp for entering Running, or null beforehand. Preparation does not set it.
matchEndedAt — UTC ISO 8601 finish/stop timestamp, or null before finishing.
matchEndedAtSeconds — Nonnegative elapsed seconds recorded at finish, or null beforehand.
matchTimeSeconds — Current signed whole-second clock.
matchTime — Current formatted duration.
matchStatus — Exact string NotRunning, Starting, Preparation, Running, or Finished.
paused — Boolean pause flag.
Stopping before Running can leave matchStartedAt null and clamp the finish duration to zero while the phase clock is still negative. A new start clears start/end metadata. Resetting the status alone does not necessarily clear metadata retained by the server; inspect the phase.

Team indexes and roster updates
0 Red, 1 Blue, 2 Green, 3 Orange, 4 Purple, 5 Cyan, 6 Pink, 7 Brown, 8 Yellow. Spectators use -1. teamName reflects custom names; it does not describe the local palette.
Roster updates follow received joins, departures, nickname changes, team changes, and administrator changes. Players are ordered by team then nickname; admins are sorted by name. Historical claim names need not change after a nickname change or departure.

markedSquares entry fields
squareName — Current resolved objective text.
markedBy — Nickname recorded for the person performing the claim action; it can be an administrator acting for someone else.
markedAt — Elapsed match seconds at that claim, clamped to at least zero.
position — One-based C<column>R<row> coordinate. C1R1 is top-left; C3R2 is column 3, row 2.
pointValue — Nonnegative objective points, default 1.
There is no team index or user GUID on a claim entry. Matching markedBy to the current roster is unreliable with duplicate/changed names, departed players, or administrator actions. It cannot provide authoritative team scoring.
Claims are ordered by board index then team internally. Unclaiming removes that team's entry; removing the last claim returns the objective to remainingSquares. Teammates do not each get separate team claims.

remainingSquares entry fields
squareName — Current resolved objective text.
position — One-based C<column>R<row> coordinate.
pointValue — Nonnegative objective points, default 1.
Entries follow row-major board order: C1R1, C2R1, and so on, then the next row. A zero-point square still leaves this array when claimed.

Random and timed objectives
Text is resolved, for example Kill 3 Bell Bearing Hunters rather than a %num% template. Timed objectives show initial values in Preparation, progress only in Running, stop during pauses, and freeze on their first claim. Resume after an eligible unclaim catches up to current match time; Keep frozen preserves the value. The export follows the applied text but does not include timelines or a frozen-state flag.

Data that is not included
There is no board-size field or full squares matrix; claim team ID; player GUID; scoreboard total; bingo lines/bonuses; winner; seed; full bingo game settings; source JSON; categories/weights; tooltips; classes; counters; stars; timed schedules; chat; local palettes; server address; or administrator credentials.
markedSquares.length counts claim entries, not necessarily distinct positions. Deduplicate by position to count marked objectives. Adding pointValue values cannot reproduce an authoritative team score because claim team IDs and bingo bonuses are absent.

Local URL response behavior
An active /live/lobby1.json returns 200 with match JSON. HEAD returns headers without a body. An inactive slot returns 404 with an error object while the listener is running. With no listener, expect a connection error instead.
/health returns service EldenBingo.LocalJson and status ok; it proves the listener is running, not that a lobby slot exists. OPTIONS supports browser preflight. PUT and other unsupported write methods return 405. Non-local Host headers return 403.
Responses use UTF-8 application/json, no-store caching, and cross-origin read headers. Browser permissions still apply. The local read endpoint has no secret token/login; any permitted local reader can read the published data. localhost only works on the publishing computer.

Reader refresh and lifecycle
Poll about once per second, avoid overlapping requests, check HTTP success, and replace your displayed snapshot. Clear old squares on NotRunning/Starting or when the slot disappears. Do not infer a winner from empty arrays.
Stop sharing, leaving, disconnecting, or closing normally removes the slot promptly. Crashed/unresponsive publishers disappear when their pipe closes or times out, up to about ten seconds. Other slots continue. The empty listener exits after about thirty seconds.
Snapshots are in memory with a 512 KiB size limit. There is no automatic live JSON file. No updatedAt, revision, or heartbeat field proves freshness; during service problems the last accepted snapshot may remain readable until disconnect. Check the app's local-sharing status as well.")
    };

    private TabPage createGuidePage()
    {
        var page = new TabPage { Padding = new Padding(8) };
        var topics = new TabControl { Dock = DockStyle.Fill, Multiline = true, AccessibleName = "Guide topics" };
        var articles = new List<RichTextBox>();
        foreach (var (title, text) in GuideTopics)
        {
            var topic = new TabPage(title) { Padding = new Padding(8) };
            var article = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText, Font = new Font("Segoe UI", 10),
                WordWrap = true, DetectUrls = false, Text = text, AccessibleName = title + " guide",
                ScrollBars = RichTextBoxScrollBars.Vertical, HideSelection = false };
            formatGuide(article);
            article.Select(0, 0);
            articles.Add(article);
            topic.Controls.Add(article); topics.TabPages.Add(topic);
        }
        var findRow = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4, 3, 4, 3) };
        var findLabel = new Label { Text = "Find in topic:", Dock = DockStyle.Left, Width = 90, TextAlign = ContentAlignment.MiddleLeft };
        _guideSearch = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Find within the selected guide topic…", AccessibleName = "Find in guide" };
        var next = new Button { Text = "Find next", Dock = DockStyle.Right, Width = 95 };
        var outcome = new Label { Dock = DockStyle.Right, Width = 105, TextAlign = ContentAlignment.MiddleCenter, AccessibleName = "Guide search result" };
        void selectTopic()
        {
            outcome.Text = "";
        }
        topics.SelectedIndexChanged += (_, _) => selectTopic();
        void find()
        {
            if (string.IsNullOrEmpty(_guideSearch.Text)) return;
            var article = articles[Math.Max(0, topics.SelectedIndex)];
            var start = article.SelectionStart + article.SelectionLength;
            var found = article.Find(_guideSearch.Text, Math.Min(start, article.TextLength), RichTextBoxFinds.None);
            if (found < 0) found = article.Find(_guideSearch.Text, 0, RichTextBoxFinds.None);
            if (found >= 0)
            {
                var matches = new List<int>();
                for (var offset = 0; offset <= article.TextLength - _guideSearch.TextLength;)
                {
                    var position = article.Text.IndexOf(_guideSearch.Text, offset, StringComparison.OrdinalIgnoreCase);
                    if (position < 0) break;
                    matches.Add(position);
                    offset = position + _guideSearch.TextLength;
                }
                outcome.Text = $"{matches.IndexOf(found) + 1} of {matches.Count}";
                article.Focus(); article.ScrollToCaret();
            }
            else outcome.Text = "No matches";
        }
        next.Click += (_, _) => find();
        _guideSearch.TextChanged += (_, _) => outcome.Text = "";
        _guideSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { find(); e.SuppressKeyPress = true; } };
        findRow.Controls.Add(_guideSearch); findRow.Controls.Add(outcome); findRow.Controls.Add(next); findRow.Controls.Add(findLabel);
        page.Controls.Add(topics);
        page.Controls.Add(findRow);
        page.Controls.Add(new Label { Text = "Help & guides", Dock = DockStyle.Top, Height = 40,
            UseMnemonic = false, Padding = new Padding(6, 8, 0, 0), Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold) });
        topics.BringToFront();
        selectTopic();
        return page;
    }

    private static List<(string Title, int Start)> guideSections(RichTextBox article)
    {
        // RichEdit selection indexes count each newline once, including when
        // a verbatim guide string was compiled from a CRLF source file.
        var lines = article.Text.Replace("\r\n", "\n").Split('\n');
        var result = new List<(string, int)>();
        var offset = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length > 0 && (i == 0 ||
                (line.Length < 100 && char.IsLetter(line[0]) &&
                 string.IsNullOrWhiteSpace(lines[i - 1]) &&
                 !line.Any(c => ".;{}\"".Contains(c)))))
                result.Add((line, offset));
            offset += lines[i].Length + 1;
        }
        return result;
    }

    private static void formatGuide(RichTextBox article)
    {
        foreach (var section in guideSections(article))
        {
            article.Select(section.Start, section.Title.Length);
            article.SelectionFont = new Font("Segoe UI", section.Start == 0 ? 13 : 11, FontStyle.Bold);
        }
        var lines = article.Text.Replace("\r\n", "\n").Split('\n');
        var offset = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var definition = line.IndexOf(" — ", StringComparison.Ordinal);
            if (definition > 0)
            {
                article.Select(offset, definition);
                article.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
            }
            if (trimmed.StartsWith('{') || trimmed.StartsWith('}') || trimmed.StartsWith('[') ||
                trimmed.StartsWith(']') || trimmed.StartsWith('"'))
            {
                article.Select(offset, line.Length);
                article.SelectionFont = new Font("Consolas", 10);
                article.SelectionBackColor = SystemColors.Control;
            }
            offset += line.Length + 1;
        }
    }
}
