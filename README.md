# Sea Power: R.A.T.S. Flotilla

A Windows app that keeps several Sea Power installs on one PC, each with its own mods, settings and saves; your Steam install stays as it is.

| | |
| --- | --- |
| Version | `0.3.1` (2026-11-11) |
| Game | Sea Power 0.8.7 up to (not including) 0.10.0; run with build 261107 (2026-11-09) and the public beta in 0.3.1 (2026-11-11) |
| Needs | Windows 10 or 11 (.NET Framework 4.8) and a Steam install of Sea Power; not a game mod, no mod loader |
| Get it | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836624723) or [GitHub Releases](https://github.com/struckl/SeaPowerFlotilla/releases) |
| Settings | none; the copy folder is chosen with Change... in the main window |
| Guide | [Flotilla guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3836683383) |
| Feedback | [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/flotilla) |
| Source | R.A.T.S. Source License, see `LICENSE` |

![Flotilla](docs/flotilla.png)

## Features

- Copies of the game, about 19 GB each: Clean (only the game's own files) or Copy of my setup (BepInEx, mods, missions, settings, saves).
- Rename, delete and move copies; the Steam install is never deleted or changed.
- Play starts the selected install; a desktop shortcut starts a copy directly. The game starts only when you press Play.
- Mods window per copy: switch on or off, set the order, add a folder or .zip to that copy only.
- Keep in this copy: a Workshop mod stays at the version you have, other copies still get updates.
- Update brings a copy's game files up to date and checks its mods the way the game does.
- Each copy can stay on its own game version.

Every button and edge case: [docs/features.md](docs/features.md); where Sea Power keeps what: [docs/how-it-works.md](docs/how-it-works.md).

## Install

1. Download `SeaPowerFlotilla-<version>.zip` from [Releases](https://github.com/struckl/SeaPowerFlotilla/releases) and unzip it to a folder that stays (desktop shortcuts point there).
2. Start `SeaPowerFlotilla.exe`.
3. Or subscribe on the Workshop and start `SeaPowerFlotilla.exe` once from the item's folder; it moves to `%LOCALAPPDATA%\R.A.T.S. Flotilla\App` and picks up newer Workshop versions by itself.

- No admin rights, no Steam setup. The exe is not signed, so Windows may say "unknown publisher": More info > Run anyway.
- Its entry in the game's Mods menu does nothing and can stay off.

## Settings

- None to edit. The copy folder is chosen with **Change...** in the main window.

## Building

- Needs the .NET SDK 8 or newer (it builds the .NET Framework 4.8 exe through the reference-assemblies package); no code from the Keel repo.
- The offline tests run against a fake install and a fake data folder, never the game.

```bash
powershell tools/package.ps1
```

It builds, runs the tests and writes `dist/SeaPowerFlotilla-<version>.zip`. `tools/make-icon.py` draws the icon (Pillow).

| Path | What |
|---|---|
| `src/Flotilla` | The app |
| `tests/Flotilla.Tests` | Offline tests |
| `docs/` | `features.md`, `how-it-works.md`, the picture |
| `workshop/` | Workshop page text and pictures |

## Credits and license

- [R.A.T.S. Source License](LICENSE): read it, fork it, send fixes; no re-uploads, no reuse in other projects.
- Not affiliated with Triassic Games or MicroProse.

*R.A.T.S. - Ratte's Assorted Tactical Systems*
