__**Bingo App v0.25 — Official Release!**__

This update brings more board customization, better controls, expanded squareset features, and new tools for testing and streaming your bingo games!

__**More flexible squaresets**__
• **Custom points:** Add `"point-value": 2` to make an objective worth two points. Missing values default to 1, and zero-point squares are supported. Non-default values appear on the revealed board.
• **Center rules:** On odd-sized boards, `"center": 0` prevents center placement. `"center": 1` makes an objective a forced-center candidate; one is selected through weighted selection, and the other forced candidates are excluded. Omitting the field keeps normal selection. Even-sized boards have no single center.
• **Timed variables:** Unclaimed objectives can change requirements at set times—for example, “Kill 2 Bell Bearing Hunters” becoming “Kill 3 Bell Bearing Hunters” after five minutes.
• Timed stages advance during Running, stop during pauses, and freeze when first claimed. The original claimant removing the last claim can choose **Resume**, **Keep frozen**, or **Cancel**. Resume catches up to the current game time.
• Existing squaresets and ordinary random-variable arrays remain supported. Timed and random variables can be combined.

__**Expanded squareset testing**__
• See resolved variants separately, alongside original templates and objectives selected zero times.
• Compare selection counts, board percentages, center frequency, weights, points, categories, and regions.
• Filter by an exact category to inspect its individual objectives and diagnostics, including multi-category squares.
• Understand failures and rejections caused by objective availability, category requirements, regions, and center rules.
• Search, sort, preview sample boards, cancel tests, and export results as CSV.
• Tests run **entirely locally**, without uploading JSON or changing your live lobby. Fixed seeds allow repeatable comparisons. Timed variables are tested at their starting values.

__**Customization and profiles**__
• Customize all nine team colors, empty squares, text, borders, spacing, and hover brightness.
• Automatic text contrast and editable **Protanopia, Deuteranopia, and Tritanopia** starting palettes.
• Save and independently switch team palettes and keyword-color profiles through Quick settings.
• Import/export profiles for sharing and backups. Keyword rules support readable names and RGB colors.
• Save your own appearance and window layouts—no built-in layout presets. Colors and appearance remain **client-side**.

__**Controls and viewing**__
• Configurable shortcuts, including separate Left/Right Ctrl bindings, optional modifiers, and defaults that can be fully disabled.
• **Middle-click / F4:** privately dim or restore a square.
• **F3:** find objectives without rearranging the board.
• **F10:** focus mode, with board-only, score/timer, or score/timer/chat layouts.
• Optional scroll-wheel adjustments using keyboard selection; actual mouse hover remains the default.
• Private dimming syncs between your main and pop-out boards without changing claims or scores.

__**Live JSON**__
Use a stable local address such as `http://localhost:5168/live/lobby1.json` for overlays and tools. Includes roster, game state, claims, points, and a per-second clock. Multiple local slots are supported. Squares reveal in **Preparation**, or **Running** when preparation is skipped. This is a localhost feed, not a public sharing link.

__**Custom sounds**__
Choose your own WAV/MP3 files with per-event mute, volume, and Test controls. Start/pause/finish are silent until configured; original claim and bingo sounds remain available.

__**More improvements**__
Saved bingo game settings presets, reorganized Settings and Help & Guides, one-click lobby-code copying, clearer labels, and fixes for trapped pop-out dialogs, small toolbars, footer colors, search alignment, and text clipping.

**The map is disabled in this version.**
