# Bingo App v0.25 — Update Notes

Version **0.25** adds more board customization, configurable controls, local live-data output, and tools for creating and testing squaresets. Settings and guides have also been reorganized to make the growing feature set easier to navigate.

The desktop app and standalone server now report **0.25.0** internally. Update both when using the new server-controlled squareset behavior, including timed variables and custom point values.

## Board controls and quality of life

### Configurable keyboard shortcuts

The Keyboard settings page now provides bindings for:

- All eight movement directions, including diagonals.
- Claiming and unclaiming a square.
- Adding and removing a private star.
- Increasing and decreasing counters.
- Dimming and restoring squares.
- Searching the revealed board.
- Toggling focus mode.

Left Ctrl and Right Ctrl can be assigned independently. Optional Shift, Control, and Left Alt requirements let you restrict shortcuts to deliberate key combinations. All selected modifiers must be held; Right Alt does not satisfy a Left Alt requirement.

Default controls are provided for every action:

| Action | Default binding |
| --- | --- |
| Up, down, left, right | Arrow keys and Numpad 8, 2, 4, 6 |
| Diagonal movement | Numpad 7, 9, 1, 3 |
| Claim / unclaim | Space |
| Star / unstar | Numpad Multiply |
| Increase / decrease counter | Numpad Add / Subtract |
| Dim / restore square | F4 |
| Find square | F3 |
| Toggle focus mode | F10 |

Existing custom bindings are preserved. New defaults do not take a key already assigned to another action, so an existing setup may differ from this table.

Select a binding and press a key to change it. Escape cancels capture, and the clear button disables an individual action. **Clear all bindings** removes keyboard shortcuts and disables middle-click dimming. Disabled bindings stay disabled after saving and restarting. **Restore defaults**, **Arrow defaults**, and **Numpad defaults** provide starting points for rebuilding a setup.

Shortcuts work with the main and pop-out boards, including while playing out of focus. They pause while typing in the app or using a dialog. Held keys trigger once per press, and board navigation stops at the board edges.

### Private square dimming

Middle-click a revealed square, or use the Dim / restore binding, to darken it. Repeat the action to restore its normal appearance.

- Dimming is visible only to that client.
- The main board and pop-out share the same dimmed squares.
- It does not claim the square, change scores, remove stars or counters, freeze timed objectives, or appear in JSON exports.
- It is available from Preparation onward, including to spectators who can see the board.
- Dimmed state clears when the board is replaced or the user leaves the lobby; it is not saved across sessions.

Middle-click dimming can be disabled independently under **Settings → Keyboard → Mouse controls**. Its keyboard binding can be changed or cleared separately.

### Scroll-wheel targeting

By default, scrolling changes a counter only when the physical pointer is over a visible square. A keyboard-selected highlight alone does not enable wheel changes.

The optional **Allow scrolling the keyboard-selected square** setting restores that behavior for users who prefer it. It is **off by default**. When enabled, a square under the mouse takes priority; otherwise, the square selected with navigation keys can receive the adjustment. Mouse actions do not require the optional keyboard modifiers.

### Find a square

Use **Find square**, or F3 by default, to search objective text. Matching objectives are highlighted in their existing positions, with a result count. Search does not rearrange the board and is available only after the board has been revealed to the user.

Search is supported in the main and pop-out boards. Closing or clearing search removes its highlights; hiding or replacing the board clears stale results. Search-bar alignment has also been improved.

## Focus mode and pop-out behavior

Focus mode gives the board more room by hiding the player list and administrator controls. Three viewing layouts are available:

- **Board only**.
- **Board + score & timer**.
- **Board + score, timer & chat**.

Choose a layout from the Focus dropdown. F10 toggles the selected layout by default, and exiting focus restores the normal lobby view. Leaving the lobby also exits focus mode.

The pop-out remains always on top. While a confirmation, Settings window, or file/color dialog is open, it temporarily hides so that the dialog cannot become trapped underneath it. It restores afterward, preserving its arrangement. The toolbar also wraps at smaller window sizes so its controls remain accessible.

## Board appearance and saved profiles

### More board customization

**Team Colors & Squares** now includes:

- Custom colors for all nine existing teams.
- Empty-square, square-text, and border/gap colors.
- Border thickness and square spacing.
- Hover brightness.
- Automatic light/dark text contrast on single-team claimed squares and team-selection buttons.

These are **client-side display preferences**. They do not add teams, rename them, change scoring, or affect another player's palette.

### Color-blind starting palettes

Alongside Default, Vivid, and Pastel, the app includes **Protanopia**, **Deuteranopia**, and **Tritanopia** starting palettes. Selecting one also enables automatic text contrast.

Every team color remains editable. These are starting points for different visual needs, not simulations or a guarantee that all nine teams will be distinguishable to every viewer. Choose the palette that looks clearest and adjust it as needed. The Deuteranopia starting palette uses [Paul Tol's nine-color muted scheme](https://sronpersonalpages.nl/~pault/), reordered for team slots.

### Saved team and keyword profiles

Save multiple team palettes and switch between them through **Quick settings**. Keyword color profiles can be saved and switched independently, including their ordered rules, colors, and intensity. An empty keyword profile can be used to turn keyword highlights off.

Profiles support naming, updating, deletion, and import/export. Shared profiles and backups can include team palettes, keyword profiles, and presentation layouts. Name collisions on import retain both copies rather than silently overwriting an existing profile. Imported and edited profiles remain staged until **Save settings**.

Keyword profile JSON now uses readable colors, matching the familiar keyword-color format:

```json
[
  { "Keyword": "Acquire an Exultation Talisman", "Color": "Yellow" },
  { "Keyword": "Kill Radahn", "Color": "255, 128, 192" }
]
```

These entries belong inside a named profile's `rules` array when editing a profile archive. The archive still includes its format/version wrapper, profile name, and intensity.

New keyword-rule exports use a standard color name where possible, otherwise an RGB string. Existing signed ARGB values and older lowercase rule property names remain supported on import. Team-palette numeric fields retain their existing format.

## User-created layouts and bingo game presets

### Presentation layouts

Layouts now starts at **None (current settings)** and includes **no built-in role presets**. Users can create and name their own layouts from their current appearance and window arrangement. Existing saved custom layouts remain available.

Saved layouts can restore fonts, background and board appearance, main-window and pop-out geometry, sidebar width, board-size limits, highlights, and pop-out presentation options. A live preview is available for appearance settings when the Settings window has enough space.

Layouts affect presentation only; they do not change a user's team, administrator access, or bingo game rules.

### Bingo game settings presets

The **Match presets** controls in Create Lobby and Lobby Settings save groups of options together, including:

- Board size and lockout rules.
- Random seed.
- Preparation time.
- Class restrictions and random-class options.
- Category limit.
- Bingo bonus points.

Use these presets to reuse a preferred setup without re-entering every option. Preset-library changes remain staged until saved, and applying a preset fills the settings form for review before the lobby settings are confirmed.

## Reorganized Settings and Help & Guides

Settings uses a searchable sidebar and remembers the last selected category. The window is resizable, and appearance previews give way to editing space in narrower windows.

Several pages now separate their tasks into focused tabs:

| Settings page | Organization |
| --- | --- |
| Team Colors & Squares | Team colors, Square styling, Saved profiles |
| Text & Effects | Font & highlights, Keyword colors |
| Keyboard | Key bindings, Mouse controls |
| Audio | Playback, Event sounds |

Bindings are grouped into Movement, Square actions, and Board/window controls. Required modifiers use automatically sized rows to prevent their description or checkboxes from being cut off with larger text. Smaller settings pages use vertically stacked groups.

**Help & Guides** now includes detailed offline instructions, examples, troubleshooting, and an export reference. Timed-variable explanations and the useful squareset recipes are combined into **JSON Options**; separate Timed tokens and Recipes tabs are no longer needed. The color-recipe paragraph and section-jump dropdown have been removed. Topic tabs, formatted examples, and in-topic search remain.

User-facing terminology now consistently describes **bingo games**.

## Custom event sounds

Under **Settings → Audio → Event sounds**, users can choose their own **WAV or MP3** files for:

- Claims and unclaims.
- Bingo.
- Bingo game start.
- Pause and resume.
- Finish.

Each event has independent enable/mute, volume, file selection, and Test controls. Event volume is multiplied by the master volume, and Playback contains the output-device and master-sound settings.

The original claim/unclaim and bingo sounds remain available. **Original** restores them after a custom selection. A custom claim sound replaces all claim/unclaim variants.

The three newly added start/pause/finish sound files have been removed. Those events are **silent until a user chooses a file**. **Clear** removes their custom selection. Pause and resume share one sound choice.

Custom files are referenced at their selected paths, so keep them in place. File choices are staged until **Save settings**; Test can preview a staged choice, while Cancel discards changes. Joining an existing bingo game does not replay its start or finish cue.

## New squareset behavior

### Custom point values

Add `point-value` to an objective to change its scoring value:

```json
{
  "name": "Defeat Margit",
  "point-value": 2
}
```

Objectives without this field are worth **1 point**. Values must be nonnegative whole numbers, so zero-point objectives are also supported. Claiming awards the configured points and unclaiming removes them; bingo bonuses remain separate.

The board displays a point label when the value differs from 1, such as **2 pts** or **0 pts**. Labels appear on revealed boards in both windows and in sample-board previews. JSON exports also include point values.

### Center placement rules

On odd-sized boards:

- `"center": 0` prevents an objective from occupying the center.
- `"center": 1` makes it a forced-center candidate. One candidate is selected through weighted selection and placed in the center; the other forced-center candidates are excluded from that board.
- Omitting `center` keeps normal selection, including possible center placement when no forced-center candidate exists.

Even-sized boards have no single center, so these rules do not restrict their placement.

### Timed variables

Variables can now change requirements at specified elapsed bingo game times. Existing random arrays retain their original behavior; timed progression is explicitly enabled with an object:

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

This starts at two hunters and progresses to three at five minutes, then four at ten minutes, while unclaimed. Stage times are absolute elapsed seconds, start at zero, and must strictly increase. Timed and ordinary random variables can be combined in the same objective.

- Preparation and the initial countdown do not advance timed stages.
- Pausing stops progression; Finished stops further advancement.
- The first claim freezes the square's timed requirements for all teams.
- Stars, counters, and private dimming do not freeze timed requirements.
- Tooltips show the timeline and frozen state.

When the original claimant removes the last remaining claim, a confirmation offers **Resume timed square**, **Keep frozen**, or **Cancel**. Resume catches up to the current bingo game time rather than restarting the variable's clock. Keep frozen removes the claim without advancing its text; it is the default Enter action. Cancel leaves the claim intact.

Other teams' remaining claims prevent resumption, and clearing another player's claim does not provide the same resume choice. The server validates eligibility. Timed text updates reach the main board, pop-out, search, and local JSON feed.

Existing squaresets remain readable, including bare arrays, wrapped files, and ordinary random-token arrays.

## Expanded squareset testing

Open **Settings → Squareset testing → Test squareset randomization**, or use Quick settings, to test a saved local JSON file.

Testing runs the shared generation code **entirely on the user's computer**. It makes no server randomization requests, uploads no JSON, and changes neither the live lobby board nor its random sequence. It also works disconnected.

The selected board size, seed, class options, categories, regions, weights, center rules, and variables apply during generation.

### Individual statistics and diagnostics

- **Resolved objectives:** observed objective text after random substitutions, so variants such as “Kill 3 night's cavs” and “Kill 4 night's cavs” have separate statistics.
- **Source objectives:** original templates and combined counts, including objectives selected zero times.
- **Categories and Regions:** occurrences, average counts, and board coverage.
- **Generation failures:** explanations for insufficient objective pools, conflicting or unmet category requirements, region blockers, and center-placement problems.
- **Selection diagnostics:** per-objective category/region rejections, unchosen forced-center exclusions, center-placement rejections, objectives never reached, and considered-but-unselected objectives. Picks discarded with failed boards are shown separately.

Boards that fill without meeting required category minimums are rejected rather than accepted as valid.

### Category drill-down

Choose a category to compare its individual objectives in the resolved, source, and selection-diagnostics tables. Matching uses complete category tags and supports objectives belonging to multiple categories.

Select a category row and choose **Show selected category's objectives**, or double-click it, to open the matching source objectives. **Show all** removes the category selection. Category filtering combines with text search and sorting; source rows include objectives selected zero times.

Filtering does not rerun generation or recalculate probabilities. The original successful-board totals remain the denominator, and category/region totals and generation failures continue to describe the complete run.

### Working with results

- Search result tables and sort columns, including numeric statistics.
- Preview a successfully generated sample board.
- Export the shown results as CSV, including the category selection and filtered objective rows.
- Cancel a test and start another run.
- Use a fixed seed to repeat the same sequence for the same file, settings, and attempt count.

Frequencies are estimates from the attempted boards, not guaranteed probabilities. Timed-variable results measure their initial values at **00:00**; use a running bingo game to test later transitions.

## Local live JSON for overlays and other tools

**Local JSON URL** exposes the current lobby snapshot on the user's computer without requiring a separate API deployment or bingo-server hosting changes.

Default addresses are deterministic:

```text
http://localhost:5168/live/lobby1.json
http://localhost:5168/live/lobby2.json
```

Choose a local slot from 1–99. Another application instance can use another slot at the same port. The slot number is independent of the actual lobby code, and the same slot/port combination keeps the same address after restarting. Occupied slots cannot be overwritten.

The feed includes the current roster, teams, administrator/spectator flags, bingo game state, pause flag, timestamps, whole-second clock, current claims, unclaimed objectives, positions, and point values.

### When squares appear

| State | Exported square data |
| --- | --- |
| NotRunning | Hidden |
| Starting | Hidden during the initial countdown |
| Preparation | Revealed once the client receives the board |
| Running | Revealed and updated with current claims/text |
| Finished | Revealed board and surviving claims retained |

With preparation disabled, squares first appear in Running. Resetting to NotRunning hides them again. An administrator's private early board preview does not bypass these export rules.

The clock advances in whole seconds, stops during pauses, and freezes at finish. Countdown/preparation durations can be negative. A status packet can arrive shortly before its board packet, so an allowed phase may briefly have empty square arrays.

### Reading and stopping the feed

The latest snapshot is published approximately once per second. A consuming page must fetch again to receive newer data. Multiple claims that remain marked appear together in the next snapshot; a brief claim followed by an unclaim between publications may never appear to a reader. This is a current-state feed, not an event history.

The address works on the **publishing computer**. It is not a public HTTPS link that another person can open remotely. A hosted overlay can request the feed through a browser on that computer, subject to browser permissions. Responses support cross-origin reads and disable caching.

Publishing is enabled for the joined lobby. Stopping sharing, leaving, disconnecting, or closing the app removes its feed. Other active slots continue independently.

**Export JSON** remains available for a one-time file snapshot. It does not continuously rewrite the selected file; use Local JSON URL for live data.

The output does not currently include an authoritative scoreboard, bingo bonuses, a winner, counters, stars, private dimming, or a team ID on each claim. See the [complete JSON output reference](json-export-reference.md) for every field, state, limitation, and HTTP response.

## Lobby improvements and fixes

- Added a copy icon beside the bottom lobby code, with copy confirmation feedback.
- Replaced generic confirmation labels with clearer Join Lobby/Create Lobby actions.
- Clarified that an optional administrator password grants administrator access.
- Displays **Random** for a random seed rather than requiring users to know that zero means random.
- Separates Quick settings from the main Settings button.
- Provides labelled toolbar icons for Quick settings, Focus, and Find square.
- Fixes the lobby footer inheriting a black custom background.
- Improves small pop-out toolbar layouts and access to modal dialogs.
- Improves search alignment and keyboard-settings readability.

## Availability and compatibility notes

- **The map is disabled for this version.** Its visible settings, map-specific controls, and outdated instructions have been removed cause we hate fun.
- No built-in JSON editor is included. Edit source files externally, then use the local tester and lobby upload controls.
- Existing user-created profiles and supported squareset formats remain readable. Older numeric keyword colors are accepted; new readable keyword-profile exports are intended for updated clients.