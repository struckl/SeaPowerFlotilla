# Sea Power: R.A.T.S. Flotilla

*R.A.T.S. – Reconnaissance, Acoustics, Terrain & Signals*

**Bugs, ideas, questions:** [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/flotilla)

Several Sea Power installs on one PC, side by side, each with its own mods, settings and saves. Keep one bone-clean
for bug reports, one loaded with a big mod set, one for trying things out, and keep your Steam install as it is.

![Flotilla](docs/flotilla.png)

## Get it

Download `SeaPowerFlotilla-<version>.zip` from [Releases](https://github.com/struckl/SeaPowerFlotilla/releases), unzip it to a
folder that stays (desktop shortcuts point there) and start `SeaPowerFlotilla.exe`. Nothing to install: it needs .NET Framework 4.8,
which every Windows 10 and 11 has. The exe is not signed, so Windows may say "unknown publisher": *More info* → *Run anyway*.

Or subscribe on the Steam Workshop and start `SeaPowerFlotilla.exe` once from the item's folder; it moves to `%LOCALAPPDATA%\R.A.T.S. Flotilla\App` and picks up newer Workshop versions by itself.

It is not a game mod: its entry in the game's Mods menu does nothing and can stay off.

Step by step on Steam: the [Flotilla guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3836683383). On the Workshop: [R.A.T.S. Flotilla](https://steamcommunity.com/sharedfiles/filedetails/?id=3836624723).

## Use

| Button | What it does |
|---|---|
| **New copy...** | **Clean**: only the game's own files as Steam installs them: no mod loader, no local mods, no missions, fresh settings with no mods enabled. **Copy of my setup**: everything of your Steam install: BepInEx, local mods, missions, settings, the enabled mods with their load order, saves. About 19 GB either way. |
| **Play** | Starts the selected install (Steam must be running). Flotilla can be closed while you play. |
| **Mods...** | For one copy: switch mods on or off, set their order, add a mod (a folder or a .zip) to this copy only, remove it again. **Keep in this copy** gives the copy its own copy of a Workshop mod, so Steam updates no longer change it there; **Refresh from Workshop** brings a kept mod up to the Workshop version on purpose; **Use Workshop again** goes back. **Check game version...** checks the copy's mods against its game version. Shows which game version each mod was made for. A copy has to be started once first. |
| **Rename...** | Gives a copy a name that says what it is for. A desktop shortcut it already has follows the new name. |
| **Update** | After Steam updated the game: brings the copy's game files up to date. Its mod loader, mods, missions, settings and saves stay. When the game version changed, the mod check follows (below). |
| **Desktop shortcut** | `Sea Power (<name>)` on the desktop, starts that copy directly. |
| **Delete...** | Removes a copy for good. The Steam install is never deleted or changed. |

Copies go to `Sea Power Flotilla` next to the Steam library that holds the game (for example `D:\SteamLibrary\Sea Power Flotilla`);
**Change...** picks another folder. Steam needs no setup, and Flotilla needs no admin rights.

## Good to know

- **Workshop subscriptions** belong to your Steam account, so every copy sees every subscribed item. Which ones are enabled,
  and in what order, is kept per copy (the game keeps it with the settings). A clean copy starts with none enabled.
- **Each copy its own mod versions**: select a Workshop mod in **Mods...** and press **Keep in this copy**. Its files are copied into
  that copy as `<name> (kept)` and the Workshop entry is switched off there. Steam updates and unsubscribing no longer change it in
  that copy; the other copies still get the updates. The list shows when it was kept and when the Workshop has a newer download.
  **Use Workshop again** deletes the kept files and switches the Workshop entry back on in the same place of the load order.
  **Refresh from Workshop** replaces a kept mod with the Workshop download as it is now; it stays kept.
- **Mods follow the game**: after **Update** moves a copy to a new game version, Flotilla checks every mod that is on in it, the way
  the game does (`[Compatibility]` in the mod's `_info.ini`). A kept mod that does not fit, whose Workshop version does, is refreshed
  (ticked). A mod with no fitting version can be switched off in that copy (unticked: the game only warns about such a mod when it
  starts and still runs it if you press Ignore). **Leave as is** changes nothing.
  The same check is in **Mods...** under **Check game version...**. A specific game version for a copy: pick that branch in Steam
  (Sea Power, Properties, Betas: the public beta, or one of the frozen "Release 0.x.y" branches), then **Update** the copy, then switch
  Steam back. Do not press **Update** on that copy again unless Steam is on the branch you want in it. The Workshop has only each mod's newest version; an older one exists only
  where you kept it.
  Code mods: Keel loads only the mods switched on, so the kept copy is what runs. Anchor Chain loads every mod DLL it finds and takes
  the newest version, so there the Workshop version can still win; unsubscribe from it, or use Keel in that copy.
- **A mod for one game version only**: a mod added with **Mods...** exists in that copy alone. Keep one copy per game version (and do not press **Update** on it), and each version has its own mods.
- **One Sea Power at a time.** Do not start Sea Power from the Steam library while a copy runs: it would use that copy's settings.
- **Game updates**: Steam updates only its own install; the list marks copies that are behind. Press **Update**, or keep a copy on
  the older version on purpose (handy before switching Steam to a beta branch).
- **Steam Cloud** syncs the Steam install only. Window size and monitor are shared by all installs.
- If the PC goes off while a copy runs, your Steam install's settings come back the next time you open Flotilla or start a copy.

How it works in detail (where Sea Power keeps what, the data folder switch, what Update touches): [docs/how-it-works.md](docs/how-it-works.md).

## Build

```
powershell tools/package.ps1      # builds, runs the offline tests, writes dist/SeaPowerFlotilla-<version>.zip
```

.NET SDK 8 or newer (it builds the .NET Framework 4.8 exe through the reference-assemblies package). The tests
(`tests/Flotilla.Tests`) run every step against a fake install and a fake data folder in the temp folder and never touch the game.
`tools/make-icon.py` draws the icon (Pillow).

## License

MIT, see [LICENSE](LICENSE). Not affiliated with Triassic Games or MicroProse.

*R.A.T.S. – Ratte's Assorted Tactical Systems*
