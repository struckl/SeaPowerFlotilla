# How Flotilla works

## Where Sea Power keeps its things

| What | Where | Per copy? |
|---|---|---|
| Game files, BepInEx, doorstop (`winhttp.dll`, `doorstop_config.ini`) | install folder | yes, each copy is a full copy |
| Local mod folders, Anchor Chain's `ACConfigs`, user missions (`StreamingAssets\user`) | `Sea Power_Data\StreamingAssets` | yes |
| Settings, **enabled mods + load order** (`usersettings.ini`, section `[LoadOrder]`), saves, campaign state, logs | `%USERPROFILE%\AppData\LocalLow\Triassic Games\Sea Power` (Unity's `persistentDataPath`) | yes: `<copy>\UserData`, see below |
| Workshop subscriptions and their downloads | Steam account, `steamapps\workshop\content\1286220` | no, shared. A newly seen item starts disabled; the enabled list is per copy |
| Window size, monitor, analytics consent | registry `HKCU\Software\Triassic Games\Sea Power` | no, shared |
| Steam Cloud (`StreamingAssets\user`) | the Steam install | copies are not synced |

## Starting a copy

Unity derives `persistentDataPath` from the company and product name, so a plain copy of the game would still read and write
the Steam install's data folder. When a copy starts, Flotilla:

1. checks that no Sea Power runs and that Steam does,
2. renames `LocalLow\Triassic Games\Sea Power` to `Sea Power.steam` and puts a directory junction `Sea Power` → `<copy>\UserData`
   in its place (junctions need no admin rights), and notes the copy in `Sea Power.flotilla`,
3. writes `steam_appid.txt` (1286220) next to the copy's exe, so `SteamAPI.RestartAppIfNecessary` accepts the start instead of
   handing it to the Steam library copy, and starts `<copy>\Sea Power.exe`,
4. waits in a small background process (`SeaPowerFlotilla.exe --play "<name>"`, the same thing a desktop shortcut runs) until the
   game and its crash handler are gone, then removes the junction and renames `Sea Power.steam` back.

Between runs the data folder is exactly what it always was, so starting Sea Power from the Steam library works as before. If
step 4 never happens (PC off, process killed), the next start of Flotilla or of a copy does it, once no Sea Power runs. If both
`Sea Power` and `Sea Power.steam` exist as real folders, Flotilla stops and asks you to keep one: it never merges or deletes data folders.

## What a copy gets

- **Clean**: every file of the install except the mod loader (`BepInEx`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`),
  `Sea Power_Data\Logs` and every StreamingAssets folder except the game's own `EntityScenes` and `original`. `UserData` is empty,
  so the game starts like a first install.
- **Copy of my setup**: every file except logs and caches (`Sea Power_Data\Logs`, `BepInEx\cache`, `BepInEx\LogOutput*`), plus the
  data folder without logs, crash reports, `temp` and `cache`.

Links inside the Steam install are not followed. A cancelled or failed copy is removed again. While a copy is being made it holds
`flotilla.unfinished`; if the PC goes off or Flotilla is killed, the next start of Flotilla removes that folder. Only folders with
that file count, so nothing else in the copies folder is ever touched.

## Mods of a copy

The **Mods...** window lists the copy's own StreamingAssets folders and the Workshop downloads. Saving rewrites only the mod lines of
`[LoadOrder]` in `<copy>\UserData\usersettings.ini` (`ModNDirectory=folder,enabled,steamId`, the game's own format) and keeps the first
version as `usersettings.ini.before-flotilla`. Entries for mods not on the PC right now are kept. A copy must have been started once,
because the game writes that file. Adding copies a mod into this copy's StreamingAssets only.

**Keep in this copy** copies a Workshop item's folder into the copy's StreamingAssets as `<name> (kept)`, writes `flotilla-kept.txt`
into it (`WorkshopId`, `Name`, `FilesFrom` = the newest file time of the Workshop folder, `KeptOn`) and switches the Workshop entry
off in this copy; the kept folder takes its place in the load order and is saved like any local mod (`<name> (kept),True,0`). The
Mods window reads the marker back and compares `FilesFrom` with the Workshop folder to show "Workshop newer". **Use Workshop again**
deletes the kept folder and puts the Workshop entry back at its place, with the kept folder's on/off; it needs the item to be
subscribed. The game's own subscription list cannot be split per copy: it asks Steam for the account's list at every start. Workshop folders are never deleted and the
Steam install is never edited: use the game's own mod menu there.

## Update

Update compares the *game files* (the clean set above) of the Steam install and the copy by size and modified time, copies what
differs, and removes game files that the Steam install no longer has. It never touches the copy's mod loader, mod folders,
`user`, `ACConfigs` or `UserData`, and never copies mod folders from the Steam install. If a game update ever adds a new folder to
StreamingAssets, Flotilla takes it for a mod folder and leaves it out; the list `GameStreaming` in `src/Flotilla/Game.cs` then needs it.

## From the Workshop

Steam can replace or remove a Workshop folder at any time, so Flotilla does not run from one. Started there, it copies itself to
`%LOCALAPPDATA%\R.A.T.S. Flotilla\App`, offers a desktop shortcut the first time and continues from that copy. When that copy starts and the
Workshop folder holds a newer version, it hands over to it and the newer version replaces it. Flotilla's own item is left out of its Mods window.

## Files of Flotilla itself

- `%LOCALAPPDATA%\R.A.T.S. Flotilla\settings.ini`: the copies folder and, if it was located by hand, the Sea Power folder.
- `%LOCALAPPDATA%\R.A.T.S. Flotilla\flotilla.log`: what was copied, started and put back.
- `<copy>\flotilla.ini`: marks a folder as a Flotilla copy (kind, date, game version it was made from).
