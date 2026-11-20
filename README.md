# Elden Bingo
This application makes running, administrating, spectating and streaming Elden Ring Bingo games easier. It was made with [Bingo Brawlers](https://bingobrawlers.com) in mind. It's built on .NET 6.0, so requires that the [runtimes](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-6.0.19-windows-x64-installer) are installed.

![eldenbingo-window](https://user-images.githubusercontent.com/604653/236489862-7a69d672-9243-49fb-88fc-236ae502f655.png)  

# Overview of Features
* Host a server capable of running multiple bingo games at once
* Create bingo lobbies with optional administrator password and upload your own bingo .json file
* Join lobbies as an individual player or part of a team, or even as spectator
* Chat in lobbies
* Configure your lobby with custom rules, like randomized starting classes
* Support for lockout and non-lockout matches
* Matches can be started, paused and stopped by the referees
* Squares can be checked/unchecked by players or by referees on behalf of players
* Right click to mark squares with stars, visible only to the player marking the square
* Scroll Wheel up/down over a square allows players or referees to keep track of their progress of a square. This progress can only be seen by the owning player and spectators, but not by opposing players/teams
* Customize bingo board color/size/font

# Hosting your own server
Download the regular application and open Settings, and then enable "Host bingo server on launch".  
![host](https://user-images.githubusercontent.com/604653/235767838-ae5752a7-e9e7-4abb-a1d1-c8e6a59292aa.png)

Remember to set-up the appropriate port forwarding.

You can also host a dedicated lobby server. I don't release server binaries with the regular releases anymore but if you need them you can easily download the code and compile it yourself.

# Connecting to a server and joining a lobby
Connect to a server by pressing the 'Connect' button in the top left corner. You can choose to auto-connect to the same server every time you launch the application.

After you've successfully connected to the server, you can create your own lobby or join an existing one. A 'Lobby' in this application is a private room where you can run your bingo game. Any player that wishes to connect to a lobby needs that lobby's room name. There is no lobby browser.

## Joining a lobby
When joining (or creating) a lobby, you will be asked to input a nickname and select a team. In the 'Team' dropdown you can also select 'Spectator'.  
![join_spectator](https://user-images.githubusercontent.com/604653/235904929-2adf97ee-e6c4-4fc3-a8c7-586c383453d1.png)

## Creating a lobby
When creating a lobby you can enter any Room name you want, or use the one that was generated. If you enter an admin password, any player that connects to the lobby with that same admin password also becomes an administrator. If you leave it empty, only you will be able to administrate.

You can also configure the rules of the lobby:  
![image](https://github.com/user-attachments/assets/44d037ec-13d9-4220-bcf1-7a4d53c3fc60)

* *Board size* specifies how large the bingo board will be. This takes effect the next time a board is generated.
* *Lockout* specifies that only one player/team can mark the same square. If this is unchecked, multiple players/teams will be able to mark the same square.
* *Random seed* will ensure that the same sequence of boards and random classes are generated/picked. This sequence will reset when a new json is uploaded. Choose **Random** to let the app pick a seed (stored as `0` in the settings).
* *Preparation Time* creates an extra preparation phase at the beginning of the match, after the initial countdown, in which players can see the board and the available classes and plan ahead before the match starts.  **0 means no preparation phase**
* *Bonus points for bingo* can be used if you want bingo lines to be worth a set number of points instead of immediately ending the match.  
* *Limit starting classes* can be used if you want to limit the choice of starting classes in order to introduce some variation. Set the pool of possible classes below.
* Setting *Max square in same category* will ensure that at most that many squares in the same category will be included in one board. **0 means this feature is disabled**. For more info on categories and the json format, see [Json Format](#json-format). 

# Administrating a lobby
You get no unfair gameplay advantage as an administrator, so you can join the game just fine. Only Admin-Spectators (ie. a player that is both administrator and spectator) have special privileges (see [AdminSpectators](#adminspectators)).

When you've joined a lobby as an administrator, the administrator tools will show under the bingo board. Use these tools to upload a Bingo .json file, following the same format as Bingo Brawlers and BingoSync but with some extensions. [Here is an example file](https://bingobrawlers.com/files/bingo-brawlers.json). For more info on the json format, see [Json Format](#json-format).

Once you've uploaded the file, a board is generated but will not be made visible to the players until the match is started. AdminSpectators can see the board and generate new boards if necessary.

Use the match control buttons at the bottom to start, pause or stop the match.  
![admin-controls](https://user-images.githubusercontent.com/604653/235774234-1d690243-9827-4510-9e51-a0befd3f0b78.png)  

# Bingo board controls
* Middle click - Dim or restore a revealed square privately, synchronized between your main and pop-out board. The default keyboard shortcut is **F4**. Dimming changes no claims, scores, timed objectives, or exports and clears on board replacement or leaving the lobby.
* Left click - Check or uncheck a square for you/your team. Visible for everyone.
* Right click - Mark a square with a star. The star can be used for anything, like a reminder for yourself. The star is only visible to you.
* Mouse wheel up/down - Increase/decrease the count of this square. The counter is useful for squares that have a set number of tasks that need to be completed, where it's easy to lose track of your progress. The counter is only visible to your own team and spectators.

The pop-out board belongs to the main window. While a confirmation, settings dialog, or file/color picker is open, the pop-out temporarily hides so its always-on-top window cannot cover a disabled application's dialog. It restores after the last dialog closes, preserving its position, size, and on-top behavior. Nested dialogs keep it hidden until the outer dialog closes.

In **Settings → Keyboard**, bind all eight movement directions, **Mark / unmark square**, **Star / unstar square**, and **Increment / decrement count**. Click a binding and press a key; **Escape** cancels capture, and **×** clears it. Duplicate keys are explained without replacing another action. Optionally require **Shift**, **Control**, and/or **Left Alt**; all checked modifiers must be held. Right Alt does not satisfy Left Alt. Mouse wheel counters remain available without these keyboard modifiers, but only while the physical mouse pointer is over a visible square. Arrow-key selection alone does not enable wheel changes; scrolling over chat, board gaps, or a covering window leaves counters unchanged.

Existing arrow/numpad navigation and the out-of-focus mark key migrate automatically, including alternative arrow and numpad bindings. **Arrow defaults**, **Numpad defaults**, and **Clear all bindings** are available as starting points. Changes apply with **Save settings**, and Cancel discards them. Hotkeys work on the main and pop-out boards and remain available while playing out of focus; they pause while typing in the application's text fields or using a dialog. A held key fires once per press, and navigation clamps at the board's edges.

**Focus mode** is available from the main toolbar or **F10** by default. Use the arrow next to **Focus** to choose **Board only**, **Board + score & timer**, or **Board + score, timer & chat**. The choice is remembered; F10 toggles that layout. Each layout centers a larger board and hides the player list and admin controls, while the latter two keep live scores and the timer in a side panel. The chat layout also keeps the lobby log and message input. **Exit focus** or the same shortcut restores the previous sidebar width and the admin controls allowed by your current role. Leaving the lobby exits focus mode. Board size limits remain respected.

Use **Find square** (or **F3**) to search the revealed board. All matching objectives get a contrasting dashed outline and a result count; their positions stay unchanged. Escape or × closes and clears search. Search is unavailable while the board is hidden, and hiding/resetting/leaving clears old results. The pop-out also has a **Find** button.

The pop-out stays always on top; its Pin/Unpin button and shortcut have been removed. It temporarily hides for modal dialogs and restores safely afterward. Focus (F10) and board search (F3) remain configurable in Settings > Keyboard. Older pin bindings are retired without changing other shortcuts.

In **Settings → Audio**, enable or mute **claims/unclaims**, **bingo**, **start**, **pause/resume**, and **finish** independently. Each event has a 0–100% volume multiplied by the master volume, plus a Test button. The master sound toggle still controls gameplay sounds. Start, pause/resume, and finish are silent until you select your own sound file. In Event sounds, Browse chooses a WAV or MP3 per event, Test previews staged choices, and Clear removes the custom cue. Claims/unclaims and bingo retain their original sounds; Original restores those after a custom choice. A custom claim sound applies to all claim/unclaim variants. Custom files are referenced at their chosen paths, so keep them in place. Save settings applies choices; Cancel discards them. Joining or synchronizing an existing match does not replay start/finish sounds; cues follow actual status transitions.

Use **Match presets…** in either Create Lobby's options or Lobby Settings to save board size, lockout, seed (including Random), preparation time, allowed/random class options, category limit, and bingo bonus together. **Save as new**, **Update selected**, and **Delete selected** edit a local preset library; **Save presets** commits the library and **Use preset** also fills the settings form. Confirm that form to apply settings to the lobby. Cancel discards library edits, and preset class selections are copied independently.

# AdminSpectators
As an AdminSpectator, you are basically the referee of the match. You can view the generated bingo board before the game has started, and generate new boards. If you mark a player in the client list, you can perform board actions on behalf of that player, like checking/unchecking squares and incrementing/decrementing the count of a square.  
![counters](https://user-images.githubusercontent.com/604653/235781324-d6e7f488-9c25-4920-b6be-682e061e8987.png)  

# Settings
Settings uses a searchable sidebar with separate categories for layouts, team colors and squares, text and effects, board/pop-out sizing, keyboard controls, audio, spectating, server hosting, and application preferences. Search for a setting such as **volume**, **font**, **spacing**, or **delay**; press **Ctrl+F** to focus search and **Escape** to clear it. Resize the window as needed. The last category is remembered, and the live preview is shown for appearance settings when there is enough space.

Keyboard settings separates **Key bindings** from **Mouse controls**. Key bindings groups Movement, Square actions, and Board and window shortcuts, with presets and required modifiers above them. Mouse controls groups scroll-wheel counters and private dimming with short explanations beside each option. Save/cancel feedback remains visible below both tabs; detailed instructions are linked in Help & guides.

Appearance settings use focused sections: **Team colors & squares** separates Team colors, Square styling, and Saved profiles; **Text & effects** separates Font & highlights from Keyword colors; **Audio** separates Playback from Event sounds. Main/pop-out sizing and application preferences use vertically stacked groups. Required keyboard modifiers size themselves to the description and checkbox text rather than a fixed row height.

## Layout presets and preview
**Settings → Layouts** starts with **None (current settings)** and includes no built-in layouts. Create your own named layouts from the current presentation and window arrangement, then load them when needed. Existing saved layouts remain available. Layouts change presentation only, not team membership, spectator status, or administrator access.

The preview is shown while editing appearance categories, including font, background color, board size, and pop-out size. It hides in narrower windows to leave room for the settings. It shows a sample 5×5 board and a text sample at the selected font size. Your live match is unchanged until you click **Save settings**. Cancel discards changes, including additions or deletions of named layouts.

To save a streaming setup, arrange the main window and pop-out board, then open Settings. Customize the appearance and sizes, enter a name in Layouts, and click **Save named layout**, followed by **Save settings**. Saved layouts include main-window and pop-out positions, sidebar width, font, background, board limits, highlights, and pop-out transparency/toolbar state. Select a saved layout to preview and restore it. A matching name can be replaced, and custom layouts can be deleted. Windows are fitted to available monitors when restored.

## Board style and team colors
Open **Settings → Team colors & squares** to choose a Default, Vivid, Pastel, Protanopia, Deuteranopia, or Tritanopia team palette, or click any of the nine team buttons to pick a custom color. You can also change empty-square and text colors, border/gap color, border thickness (including no border), square spacing, and hover brightness. Automatic contrast chooses black or white text on single-team claimed squares to suit the team color.

These are local display preferences: they apply to your board, pop-out board, scoreboard, team pickers, and player list. They do not add team slots, rename teams, or change anyone else's display. The live preview shows all nine team colors. **Save settings** applies the style; **Cancel** discards edits. Named layouts include the board style, so each streaming setup can have its own palette and spacing. **Reset board style** restores the default style in the dialog before saving.

Save multiple **color profiles** in **Settings → Team colors & squares**. Name the current colors and choose **Save as new**, or select an existing profile and choose **Update selected** to replace or rename it. Profiles store team, empty-square, text, and border colors plus automatic contrast. They preserve fonts, spacing, effects, and window sizes when selected. Use **Save settings** to keep profile edits; Cancel discards them. For instant switching during a lobby, open **Quick settings** in the main toolbar and pick a saved profile; a checkmark indicates matching colors. These profiles are stored locally and never sent to the server or other players.

Use **Export profiles…** on that page to share the selected saved palette, or **Back up all profiles + current layout…** to export every saved color/layout profile and the current presentation settings. **Import profiles…** validates the versioned JSON file before making changes. Duplicate names get an imported suffix, leaving existing profiles intact. Imports stay staged until **Save settings**, and imported layouts appear in **Layouts**. Backups cover presentation settings (including fonts, geometry, keyword colors, and board styling); they do not contain identity tokens or administrator credentials. Files are limited to 1 MB and 200 profiles.

The bottom connection/lobby status has a clickable copy icon immediately beside the lobby code. It copies only the code, briefly shows a checkmark, and provides a tooltip and keyboard-accessible name. The icon is hidden outside a lobby. Local JSON URL status remains beside its controls in the lobby. Settings opens the full preferences window. The separate **Quick settings** dropdown switches profiles and opens the squareset tester. Quick settings, Focus, and Find square have icons with labels.

In **Settings → Text & effects**, save multiple **keyword color profiles**, each including its ordered rules, colors, and intensity. Choose **Save as new**, **Update selected** (also renames), or **Delete selected**, then **Save settings** to keep changes. **Cancel** discards edits. Use **Quick settings** to switch keyword profiles instantly, independently of team palettes. An empty profile turns keyword highlights off. Keyword presets are included in full profile backups; selected keyword profiles can also be exported and imported on their own.

## In-app help and guides
Open **Settings > Help & guides**, or **Quick settings > Help and guides**, for detailed instructions on loading JSON squaresets, weights/categories/regions, center and point-value rules, random and timed tokens, testing diagnostics, client-side colors, profile JSON, worked recipes, board controls, local JSON feeds, and troubleshooting. Topics have bold section headings and monospace JSON examples. The guide search box (Ctrl+F while reading a guide) finds text in the selected topic, shows the match position or No matches, and wraps when using Find next. The guide is included with the app and works offline.

Test Randomization reads a saved JSON file on your computer and runs the shared match generator locally on a background task. It uses separate in-memory models and random state: no uploads, server requests, or changes to the live lobby or its random sequence. It works disconnected. Selected bingo game settings apply, including weights, category/region rules, centers, and classes. Results summarize successful boards separately from generation failures; timed tokens are sampled at 00:00. A fixed seed reproduces the test sequence for the same file and settings. Rerun after saving edits to read the updated file.

# Manual JSON export
**Export JSON** saves a single snapshot. Continuous file writing has been removed; use **Local JSON URL…** for live updates. Manual export and the local endpoint include the same lobby, roster, point-value, claim, and match-clock fields. The board remains hidden in NotRunning and Starting, then becomes visible in Preparation, Running, and Finished. If preparation is disabled, it is first revealed at Running. See [the full JSON output reference](docs/json-export-reference.md) for every field, state, clock rule, and endpoint response.

# Live JSON URLs
The desktop app serves JSON on **the user's own computer**, independently of the bingo server. No server deployment, certificate, administrator setup, or separate API installation is needed.

1. In your lobby, choose **Local JSON URL…**.
2. Select a **Lobby number** (1–99); leave the port at **5168** unless another program uses it.
3. Choose **Start local URL**, then **Copy JSON URL**.

The URLs are deterministic:

- Lobby 1: `http://localhost:5168/live/lobby1.json`
- Lobby 2: `http://localhost:5168/live/lobby2.json`

The same slot and port always produce the same URL, including after stopping and restarting. Slot numbers are local output slots, independent of the actual bingo game's lobby code. Open another app instance and select Lobby 2 to run a second feed on the same port. Occupied slots cannot be overwritten; select another number. Slot and port choices are remembered, but publishing is enabled explicitly for each joined lobby. Stop the endpoint before changing its slot or port.

The app automatically starts a hidden local listener shared by its instances. Each instance owns only its selected slot; closing one does not interrupt the others. Leaving, disconnecting, or stopping removes that slot. Abruptly terminating a publishing app removes its feed when its local pipe closes (or within ten seconds if it stops responding). With no remaining feeds, the hidden listener exits after about thirty seconds. All snapshots are in memory, limited to 512 KiB, and every fresh session starts with that lobby's current data.

Updates include lobby players and the per-second match clock, independently of manual export. `markedSquares` and `remainingSquares` remain empty during NotRunning and Starting (countdown); they become visible in Preparation, Running, and Finished, and reset hides them again. Both the export tracker and the local listener enforce the phase check. The HTTP interface is read-only, binds to loopback, disables caching, and supports cross-origin GET requests.

A hosted page can read the endpoint from the **same user's browser** with `fetch(url, {cache: 'no-store'})`. Modern browsers may require permission for local-network access; see [Chrome's local network access guidance](https://developer.chrome.com/blog/local-network-access). A website's backend, another computer, or another viewer cannot reach the user's localhost. Browser-specific network permissions still apply.


# Json Format
Objectives can specify a **point-value**, a nonnegative whole number. Omitted values default to **1**. For example, `{ "name": "Defeat Margit", "point-value": 2 }` awards two points to each team that claims it. Unclaiming removes those points; bingo bonuses remain separate. Custom values appear on the main and pop-out boards as a small points label, and live/file JSON exports include `pointValue`. Update both the desktop clients and standalone server for this feature.

Open **Settings → Squareset testing → Test squareset randomization…**, or use Quick settings, to open the local tester. Choose a JSON file, set the bingo game options (including board size, category limit, and seed), and choose how many boards to attempt. The tester uses the same generator and configuration loader as live lobbies, including weights, category minimums/maximums, region limits, center rules, tokens, and class randomization. Both bare arrays and wrapped squaresets are accepted; `category limits` / `category minimums` and `categoryLimits` / `categoryMinimums` are supported.

Results show separate rows for observed resolved objectives (for example, `Kill 3 night's cavs` and `Kill 4 night's cavs`), center frequency, and category/region occurrences and board coverage. The Source objectives tab keeps the original templates and aggregate totals, including objectives selected zero times. Frequencies use successful boards only; failed generations are listed separately. Resolved variants retain their original source index and template, so duplicate names from separate JSON entries stay separate. Unobserved token combinations are not listed in the resolved tab. Search filters every result tab, including objective text, source templates, categories, regions, and other column values. Click column headings to sort ascending or descending; numeric columns sort by number. A fixed seed reproduces the same sequence for the same JSON, settings, and number of attempts. **View sample board** previews a successful board without joining a lobby. **Export shown results…** saves all six result tables as CSV with the current filter and sort order. Tests run in the background and can be canceled; no lobby state is changed. Frequencies are sampled estimates, not guaranteed probabilities. The Generation failures tab explains insufficient objective pools, unmet or conflicting category minimums, category/region blockers, and missing center candidates. One failed attempt may have several reasons, so reason percentages can overlap. The Selection diagnostics tab counts category rejections, region rejections, unchosen forced-center exclusions, center-placement rejections, objectives never reached by selection, and evaluated objectives passed over. Each rejection reason counts once per objective per attempt, including failed attempts; picked objectives on failed boards are listed separately from successful selections. Diagnostic tracing does not consume random values or alter the selection order. Boards that fill before meeting all category minimums now fail generation.

The format is the same as is used by Bingo Brawlers and BingoSync but with extensions for tooltips, categories, center square and token replacements.  
![json2](https://github.com/user-attachments/assets/847c42df-868c-4c3c-bc87-e73931643b82)

Use the **tooltip** key to define a tooltip when hovering that square:  
 ![image](https://github.com/awsker/EldenBingo/assets/604653/a5f97ed4-9454-462a-bd31-8b2de1e186f7)

Use the **category** key to define a single category, or the **categories** key to define an array of categories. These categories can be used in conjunction with the lobby setting *Max square in same category* to ensure that at most that number of categories will be present in one bingo board, in order to generate more balanced bingo boards.

On odd-sized boards, **center: 1** makes an objective a forced-center candidate. One candidate is chosen through weighted random selection and placed in the middle; the other forced-center candidates are excluded. **center: 0** allows an objective anywhere except the middle. Omitting **center** keeps normal random placement, including the middle when no forced-center candidate exists. An odd-sized board needs at least one eligible objective for its center; generation fails if no valid center can be selected. Even-sized boards have no single middle square, so these placement rules do not apply.

Tokens can be used to create more dynamic squares. Create a token by surrounding a word with percentage signs (for example %x%) and declare a list of possible substitutions in an array with the same name as the token. One of those will be picked at random when the square is generated. You can even have multiple tokens in the same square. See example in the image above.

## Timed tokens

Existing token arrays remain random choices and require no changes. To opt a particular variable into timed progression, use an object with `mode: "timed"` instead of an array:

```json
{
  "name": "Kill %num% Bell Bearing Hunters",
  "num": {
    "mode": "timed",
    "stages": [
      { "at-seconds": 0, "value": "2" },
      { "at-seconds": 300, "value": "3" },
      { "at-seconds": 600, "value": "4" }
    ]
  }
}
```

This starts at 2 and changes to 3 at five minutes, then 4 at ten minutes, while unclaimed. Times are absolute elapsed match seconds, beginning when the phase enters Running. Countdown and preparation do not advance stages. Pauses stop progression; finished matches do not advance. The server checks for due changes every 250 ms and broadcasts ordinary square updates to all players and spectators. The main board, pop-out, board search, and Local JSON URL receive the updated text.

The first claim freezes that square's current value for every team, including non-lockout games. When the original player clicks the last remaining claim to unclaim it, a dialog offers **Resume timed square**, **Keep frozen**, or **Cancel**. Resume removes the claim and catches up to the current match time; Keep frozen removes the claim without changing its text; Cancel leaves the claim intact. Keep frozen is the default Enter action. There is no lobby-wide resume checkbox. Other teams' remaining claims and another player clearing somebody else's claim cannot resume it. The server verifies ownership and the per-click choice. Claims at a stage boundary use the value due at the server's match time. Stars, counters, points, and private per-user data remain intact. Saved server boards preserve schedules, resolved random values, and frozen state.

Stages must be a nonempty array, start at `at-seconds: 0`, and use strictly increasing nonnegative whole-second times (up to 2147483). Values can be strings or numbers. Multiple timed tokens can share an objective, and timed tokens can be mixed with ordinary random arrays; random tokens are rolled once during board generation. The square tooltip shows its full timeline and whether it has frozen. The squareset tester measures timed values at 00:00, with a Timing column describing subsequent stages. Bare-array and wrapped squareset formats both remain supported.

Rebuild both the desktop app and standalone server to use timed variables.

# Credits
* Nordgaren for process assembly injection
* Tremwil on The Grand Archives discord
* Process reading code from [EldenRingFPSUnlockerAndMore](https://github.com/uberhalit/EldenRingFpsUnlockAndMore) by [uberhalit](https://github.com/uberhalit)
* Button images shamelessly borrowed from [EldenRingMap](https://eldenringmap.com), drawn by [Caio Razera](https://dcaier.artstation.com/)

The optional **Settings > Keyboard > Scroll wheel on keyboard-selected square** preference is off by default. Enable it to adjust a counter using navigation-key selection while the mouse is elsewhere. A square under the pointer takes priority, and mouse-only highlights do not become keyboard targets. Save settings applies the preference; Cancel discards it.

**Keyboard defaults:** All actions have default bindings: arrows/numpad movement, Space for claim/unclaim, numpad Multiply for stars, numpad Add/Subtract for counters, F4 for dimming, F3 for search, and F10 for focus. Customize or clear individual keys in **Settings > Keyboard**. **Clear all bindings** clears every keyboard shortcut and turns off middle-click dimming; Save settings preserves disabled bindings after restarting. **Restore defaults** restores the full preset and middle-click dimming. Middle-click can also be disabled independently with **Middle mouse dims / restores squares**. Existing custom bindings are preserved.

Keyword profile rules use readable color values, matching keyword-color files: `{"Keyword":"Acquire an Exultation Talisman","Color":"Yellow"}` or `{"Keyword":"Kill Radahn","Color":"255, 128, 192"}`. Exports use a standard color name when possible, otherwise an RGB string. Existing signed ARGB values and lowercase rule property names remain valid on profile import. This also applies to keyword rules inside layout backups; team-palette numeric fields retain their existing format. Profile archives still include their profile names, intensity, and format/version wrapper.

The color-blind palettes are editable starting points, not simulations or a guarantee that every viewer can distinguish all nine teams. Selecting one enables automatic text contrast. The Deuteranopia starting palette uses [Paul Tol's nine-color muted scheme](https://sronpersonalpages.nl/~pault/), reordered for the team slots. Choose and adjust the palette that is clearest for you. Timed-variable explanations and worked squareset examples are combined under **Help & guides > JSON options**, rather than separate Timed tokens or Recipes tabs.

The squareset tester has an exact **Category** filter for Resolved objectives, Source objectives, and Selection diagnostics. Multi-category objectives appear under each matching tag. **Source objectives** includes squares selected zero times. Select a category row and choose **Show selected category's objectives**, or double-click it, to open its source objectives. Category filtering combines with search and sorting, preserves the original whole-run statistics, and is included in CSV exports. **Show all** clears the category selection.
