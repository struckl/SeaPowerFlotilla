# Changelog

## Unreleased

- Fixed: the mod check now reads `_info.ini` exactly as the game does, so its "fits / does not fit" matches the game's own warning:
  - a comment after a version (`GreaterThanEqualToVersion=0.8.7 ; note`) no longer makes a mod "not fit" (Made for shows `0.8.7`)
  - `[compatibility]` in lower case is ignored, as in the game (Made for: any)
  - when a key is there twice, or `[Compatibility]` twice, the first one counts, as in the game
  - a version the game cannot read (`0.9.0-01`) is a "no", as in the game
  - a version number too big no longer crashes the check
- Fixed: Made for shows the lower bound the game itself uses when both lower keys are set (`GreaterThanVersion` first, shown as `above 0.9.3`)
- Fixed: the main window's "Local mods" count includes Anchor Chain's `ACConfigs` folder, as the game and the Mods window list it
- Changed (internal): one copy of each name, path rule and message; the version rule in one self-contained file; tests split into steps

## 0.3.1 - 2026-11-11

The mod check no longer ticks "Switch off" by itself: the game only warns about a mod that does not fit its version and still runs
it on Ignore, and many players run such mods on purpose. Refresh steps stay ticked. Run for real on a copy of a full setup
(20 Workshop and local mods): copy made, a Workshop mod kept, saved, refreshed, the copy updated to the public beta branch and
back-checked (a second Update copies nothing), Steam's own install untouched. The windows themselves not clicked through.

## 0.3.0 - 2026-11-11

Mods follow the game: after **Update** moves a copy to a new game version, Flotilla checks its mods the way the game does and offers
the steps: refresh a kept mod whose Workshop version fits, switch off a mod with no fitting version (in that copy only). Every step can
be unticked. **Refresh from Workshop** updates a kept mod on purpose; **Check game version...** runs the check any time. The Mods list
shows each mod's version range. Tested offline (tests/Flotilla.Tests; the version rule also compared with the workspace's on every
Workshop mod on the test PC); the windows rendered off-screen, not clicked through in the game.

## 0.2.0 - 2026-11-11

Each copy can have its own version of a Workshop mod: **Keep in this copy** in the Mods window copies the mod into that copy and
switches the Workshop entry off there, so Steam updates and unsubscribing no longer change it. The list shows when it was kept and
when the Workshop has something newer; **Use Workshop again** goes back. Workshop item declares game 0.8.7 up to 0.10.0, so games on
0.9.0 download Flotilla updates again. Tested offline (tests/Flotilla.Tests); the window rendered off-screen, not clicked through in
the game.

## 0.1.0 - 2026-11-09

First version: clean copies and copies of the setup, Play with a per-copy data folder, Update after game updates,
desktop shortcuts, Delete. Works from the Workshop (moves itself out of the Workshop folder, takes newer versions from it). Rename a copy. Mods window per copy (on/off, order, add a folder or zip to one copy). A copy interrupted by a crash or power cut is removed the next time Flotilla opens. Tested offline (tests/Flotilla.Tests). Run with the game on build 261107: a clean copy made and started, the Steam install's data folder back unchanged afterwards. A mod list saved in the Mods window was read by the game: the right mods on, in the right order.
