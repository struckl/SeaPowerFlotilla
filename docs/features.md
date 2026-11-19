# R.A.T.S. Flotilla: feature detail

Everything the app does, in full. The short list is in the [README](../README.md).

## Copies

- **New copy...** makes a copy of the game, about 19 GB either way:
  - Clean: only the game's own files as Steam installs them. No mod loader, no local mods, no missions, fresh settings, no mods enabled.
  - Copy of my setup: everything of your Steam install: BepInEx, local mods, missions, settings, enabled mods with load order, saves.
- **Rename...** renames a copy. A desktop shortcut it has follows the new name.
- **Delete...** removes a copy for good. The Steam install is never deleted or changed.
- Copies go to `Sea Power Flotilla` next to the Steam library that holds the game (for example `D:\SteamLibrary\Sea Power Flotilla`). **Change...** picks another folder.

## Playing

- **Play** starts the selected install. Steam must be running. Flotilla can be closed while you play.
- **Desktop shortcut** makes `Sea Power (<name>)` on the desktop, which starts that copy directly.
- Only one Sea Power at a time: do not start the game from the Steam library while a copy runs, it would use that copy's settings.

## Mods

**Mods...** works on one copy. A copy has to be started once first.

- Switch mods on or off and set their order.
- Add a mod (a folder or a .zip) to this copy only, or remove it again.
- Shows which game version each mod was made for.
- **Keep in this copy** gives the copy its own copy of a Workshop mod:
  - Files go into the copy as `<name> (kept)`; the Workshop entry is switched off there.
  - Steam updates and unsubscribing no longer change it in that copy. Other copies still get the updates.
  - The list shows when it was kept and when the Workshop has a newer download.
- **Refresh from Workshop** replaces a kept mod with the Workshop download as it is now. It stays kept.
- **Use Workshop again** deletes the kept files and switches the Workshop entry back on at the same place in the load order.
- A mod added here exists in that copy alone. Keep one copy per game version (and do not press Update on it) to give each version its own mods.
- Workshop subscriptions belong to your Steam account, so every copy sees every subscribed item. Which are enabled, and in what order, is kept per copy. A clean copy starts with none enabled.

## Updating

- **Update** brings a copy's game files up to date after Steam updated the game. Its mod loader, mods, missions, settings and saves stay.
- Steam updates only its own install. The list marks copies that are behind. You can keep a copy on the older version on purpose.
- After Update moves a copy to a new game version, Flotilla checks every mod that is on in it, the way the game does (`[Compatibility]` in `_info.ini`):
  - A kept mod that does not fit, whose Workshop version does, is refreshed (ticked).
  - A mod with no fitting version can be switched off in that copy (not ticked by default: the game only warns about such a mod and runs it if you press Ignore).
  - **Leave as is** changes nothing.
- **Check game version...** in **Mods...** runs the same check at any time.
- For a specific game version in a copy: pick that branch in Steam (Sea Power, Properties, Betas), press Update on the copy, switch Steam back. Do not press Update on that copy again unless Steam is on the branch you want in it.
- The Workshop has only each mod's newest version. An older one exists only where you kept it.
- Code mods: Keel loads only the mods switched on, so the kept copy runs. Anchor Chain loads every mod DLL it finds and takes the newest version, so the Workshop version can still win there; unsubscribe from it, or use Keel in that copy.

## Other

- Steam Cloud syncs the Steam install only. Window size and monitor are shared by all installs.
- If the PC goes off while a copy runs, your Steam install's settings come back the next time you open Flotilla or start a copy.
- Details (where Sea Power keeps what, the data folder switch, what Update touches): [docs/how-it-works.md](how-it-works.md).


## What was run

- Workshop item declares game 0.8.7 up to (not including) 0.10.0.
- Run with the game on build 261107: copy made, started through Flotilla, Steam data folder back unchanged.
- 0.3.1 on a copy of a full setup: copy, Keep, Save, Refresh, Update to the public beta and back, Steam install untouched. The windows themselves were not clicked through.
