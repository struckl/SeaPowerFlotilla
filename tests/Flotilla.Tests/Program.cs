using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Flotilla;

/// <summary>
/// Runs every Flotilla step against a fake Steam install and a fake LocalLow folder in a temp folder, with a stand-in
/// for the game. Never touches the real game, its data or Steam. Usage: Flotilla.Tests.exe [work folder]
/// </summary>
static class Tests
{
    static int failures;

    static int Main(string[] args)
    {
        var box = Path.GetFullPath(Path.Combine(args.Length > 0 ? args[0] : Path.GetTempPath(), "flotilla-test"));
        if (Directory.Exists(box)) Copier.DeleteTree(box);
        var game = Path.Combine(box, "steamapps", "common", "Sea Power");
        var sa = Path.Combine(game, "Sea Power_Data", "StreamingAssets");
        var locallow = Path.Combine(box, "LocalLow", "Triassic Games");

        Put(game, "Sea Power.exe"); Put(game, "UnityPlayer.dll"); Put(game, "winhttp.dll"); Put(game, "doorstop_config.ini");
        Put(game, @"BepInEx\core\BepInEx.dll"); Put(game, @"BepInEx\LogOutput.log"); Put(game, @"BepInEx\cache\c.dat");
        Put(game, @"BepInEx\config\BepInEx.cfg");
        Put(game, @"Sea Power_Data\level0", "v1"); Put(game, @"Sea Power_Data\Logs\l.txt");
        Put(sa, "changelog.txt", "01-Nov-2026: 0.8.7 Build 261021\n"); Put(sa, @"original\a.ini", "v1"); Put(sa, @"EntityScenes\s.bin");
        Put(sa, @"SeaPowerTopo\_info.ini"); Put(sa, @"user\controls.ini"); Put(sa, @"ACConfigs\ac.cfg");
        Put(locallow, @"Sea Power\usersettings.ini", "steam-settings"); Put(locallow, @"Sea Power\Player.log");
        Put(locallow, @"Sea Power\saves\s1"); Put(locallow, @"Sea Power\CrashDiagnostics\d.txt");

        bool running = false;
        var log = new List<string>();
        var fleet = new Fleet
        {
            SteamGame = game, FleetDir = Fleet.DefaultFleetDir(game), Swap = new DataSwap(locallow),
            GameRunning = () => running, SteamRunning = () => true, Log = log.Add,
        };
        var swap = fleet.Swap;
        Check(fleet.FleetDir == Path.Combine(box, "Sea Power Flotilla"), "default fleet folder next to the library");
        Check(fleet.CheckFleetDir(Path.Combine(game, "copies")) != null, "fleet folder inside the game is refused");
        Check(fleet.CheckName("Steam") != null && fleet.CheckName("a/b") != null && fleet.CheckName("My setup 2") == null, "name rules");

        // Create both kinds
        var clean = fleet.Create("Clean", true, null, CancellationToken.None);
        var setup = fleet.Create("My setup", false, null, CancellationToken.None);
        var c = Tree(clean.Dir); var s = Tree(setup.Dir);
        Console.WriteLine("clean: " + string.Join(", ", c));
        Console.WriteLine("setup: " + string.Join(", ", s));
        Check(!c.Any(x => x.StartsWith("BepInEx") || x == "winhttp.dll" || x == "doorstop_config.ini"), "clean: no mod loader");
        Check(!c.Any(x => x.Contains(@"\SeaPowerTopo\") || x.Contains(@"\user\") || x.Contains("ACConfigs") || x.Contains(@"\Logs\")), "clean: no mods, user, logs");
        Check(c.Contains(@"Sea Power_Data\StreamingAssets\original\a.ini") && c.Contains(@"Sea Power_Data\StreamingAssets\changelog.txt"), "clean: game data");
        Check(c.Contains("steam_appid.txt") && c.Contains("flotilla.ini") && !c.Any(x => x.StartsWith(@"UserData\")), "clean: own files, empty UserData");
        Check(s.Contains(@"BepInEx\core\BepInEx.dll") && s.Contains(@"BepInEx\config\BepInEx.cfg") && s.Contains("winhttp.dll"), "setup: mod loader");
        Check(!s.Any(x => x.Contains("LogOutput") || x.Contains(@"BepInEx\cache")), "setup: no BepInEx logs/cache");
        Check(s.Contains(@"Sea Power_Data\StreamingAssets\SeaPowerTopo\_info.ini") && s.Contains(@"Sea Power_Data\StreamingAssets\user\controls.ini"), "setup: mods and user");
        Check(s.Contains(@"UserData\usersettings.ini") && s.Contains(@"UserData\saves\s1"), "setup: settings and saves");
        Check(!s.Any(x => x.EndsWith("Player.log") || x.Contains("CrashDiagnostics")), "setup: no logs or crash reports in UserData");
        Check(fleet.List().Select(i => i.Name).SequenceEqual(new[] { "Steam", "Clean", "My setup" }), "list");
        Check(clean.Clean && !setup.Clean, "kinds read back");

        // Cancelled create leaves nothing behind
        var cts = new CancellationTokenSource(); cts.Cancel();
        try { fleet.Create("Half", false, null, cts.Token); Check(false, "cancel throws"); }
        catch (OperationCanceledException) { Check(!Directory.Exists(Path.Combine(fleet.FleetDir, "Half")), "cancelled copy removed"); }

        // Killed while copying: found and removed by its marker; a live copy, a finished copy and a foreign folder are left alone
        Check(fleet.Unfinished().Count == 0 && !File.Exists(Path.Combine(setup.Dir, Fleet.UnfinishedMarker)), "finished copies carry no unfinished marker");
        var killed = Path.Combine(fleet.FleetDir, "Killed");
        Put(killed, Fleet.UnfinishedMarker, "999999"); Put(killed, @"Sea Power_Data\level0");
        var live = Path.Combine(fleet.FleetDir, "Live");
        Put(live, Fleet.UnfinishedMarker, System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
        var foreign = Path.Combine(fleet.FleetDir, "My own folder");
        Put(foreign, "notes.txt");
        Check(fleet.Unfinished().SequenceEqual(new[] { killed }), "only the killed copy counts as unfinished");
        Check(!fleet.List().Any(i => i.Name == "Killed"), "unfinished copy not listed");
        fleet.RemoveUnfinished(killed);
        Check(!Directory.Exists(killed) && Directory.Exists(live) && File.Exists(Path.Combine(foreign, "notes.txt")), "unfinished copy removed, others kept");
        Check(fleet.CheckName("Killed") == null, "its name is free again");
        Copier.DeleteTree(live); Copier.DeleteTree(foreign);

        // Play a copy: junction during the run, Steam data untouched, copy data written
        fleet.RunGame = install =>
        {
            Check(DataSwap.IsLink(swap.Live), "junction during the run");
            Check(fleet.IsActive(install), "the copy is the active one");
            File.WriteAllText(Path.Combine(swap.Live, "usersettings.ini"), "written by " + install.Name);
            Check(File.ReadAllText(Path.Combine(install.Dir, "steam_appid.txt")) == "1286220", "steam_appid.txt");
        };
        fleet.Play(clean);
        Check(!DataSwap.IsLink(swap.Live) && !Directory.Exists(swap.Parked) && !File.Exists(swap.ActiveFile), "back to normal after the run");
        Check(File.ReadAllText(Path.Combine(swap.Live, "usersettings.ini")) == "steam-settings", "Steam data untouched");
        Check(File.ReadAllText(Path.Combine(clean.DataDir, "usersettings.ini")) == "written by Clean", "copy data written");

        // A game that crashes still gets the data folder back
        fleet.RunGame = _ => throw new InvalidOperationException("boom");
        try { fleet.Play(setup); } catch (InvalidOperationException) { }
        Check(!swap.NeedsRepair && File.ReadAllText(Path.Combine(swap.Live, "usersettings.ini")) == "steam-settings", "repaired after a failed start");

        // Flotilla killed mid-run: the junction stays until EnsureNormal; it refuses while the game runs
        swap.SwitchTo(setup.DataDir, setup.Dir);
        Check(fleet.IsActive(setup) && !fleet.IsActive(clean), "active copy known");
        running = true;
        Throws(() => fleet.EnsureNormal(), "no repair while the game runs");
        Throws(() => fleet.Delete(setup), "no delete of the running copy");
        Throws(() => fleet.Play(clean), "one game at a time");
        running = false;
        Check(fleet.EnsureNormal() && File.ReadAllText(Path.Combine(swap.Live, "usersettings.ini")) == "steam-settings", "repair after a kill");
        Check(File.Exists(Path.Combine(setup.DataDir, "usersettings.ini")), "removing the junction keeps the copy's data");

        // Conflict: both real folders -> refuse, touch nothing
        Directory.CreateDirectory(swap.Parked);
        Throws(() => swap.Repair(), "two real data folders: refuse");
        Check(Directory.Exists(swap.Live) && Directory.Exists(swap.Parked), "conflict leaves both");
        Directory.Delete(swap.Parked);

        // No LocalLow folder yet (fresh PC): run still works and leaves an empty Steam folder
        Copier.DeleteTree(Path.Combine(box, "LocalLow2"));
        var freshSwap = new DataSwap(Path.Combine(box, "LocalLow2", "Triassic Games"));
        var freshFleet = new Fleet { SteamGame = game, FleetDir = fleet.FleetDir, Swap = freshSwap, GameRunning = () => false, SteamRunning = () => true, RunGame = _ => { } };
        freshFleet.Play(clean);
        Check(Directory.Exists(freshSwap.Live) && !DataSwap.IsLink(freshSwap.Live) && !freshSwap.NeedsRepair, "fresh PC: normal afterwards");

        // Steam update: changed, new and removed game files; mods, loader, missions and data kept; Steam's mods not copied
        Put(game, @"Sea Power_Data\level0", "v2"); Put(sa, @"original\b.ini", "new"); File.Delete(Path.Combine(sa, @"original\a.ini"));
        Directory.CreateDirectory(Path.Combine(sa, @"original\gone")); Put(setup.Dir, @"Sea Power_Data\StreamingAssets\original\gone2\x.ini");
        Put(sa, "changelog.txt", "02-Dec-2026: 0.8.8 Build 270000\n"); Put(sa, @"NewModInSteam\_info.ini");
        Put(setup.Dir, @"Sea Power_Data\StreamingAssets\OnlyHere\_info.ini"); Put(setup.Dir, @"Sea Power_Data\StreamingAssets\user\missions\m.ini");
        Check(setup.Build == "0.8.7 (build 261021)" && Game.BuildOf(game) == "0.8.8 (build 270000)", "builds read");
        var r1 = fleet.Update(setup, null, CancellationToken.None);
        var r2 = fleet.Update(clean, null, CancellationToken.None);
        Console.WriteLine($"update setup: {r1.Copied} copied, {r1.Deleted} deleted; clean: {r2.Copied} copied, {r2.Deleted} deleted");
        s = Tree(setup.Dir); c = Tree(clean.Dir);
        foreach (var t in new[] { s, c })
        {
            Check(t.Contains(@"Sea Power_Data\StreamingAssets\original\b.ini") && !t.Contains(@"Sea Power_Data\StreamingAssets\original\a.ini"), "update: new and removed game files");
            Check(!t.Any(x => x.Contains("NewModInSteam")), "update: Steam's mod folders not copied");
            Check(t.Contains(@"UserData\usersettings.ini") && t.Contains("steam_appid.txt") && t.Contains("flotilla.ini"), "update: own files kept");
        }
        Check(!Directory.Exists(Path.Combine(setup.Dir, @"Sea Power_Data\StreamingAssets\original\gone2")), "update: stale game folder removed");
        Check(File.ReadAllText(Path.Combine(setup.Dir, @"Sea Power_Data\level0")) == "v2" && setup.Build == "0.8.8 (build 270000)", "update: changed file");
        Check(s.Contains(@"Sea Power_Data\StreamingAssets\OnlyHere\_info.ini") && s.Contains(@"Sea Power_Data\StreamingAssets\user\missions\m.ini"), "update: copy's mods and missions kept");
        Check(s.Contains(@"BepInEx\core\BepInEx.dll") && s.Contains("winhttp.dll") && s.Contains(@"UserData\saves\s1"), "update: loader and saves kept");
        Check(!c.Any(x => x.StartsWith("BepInEx") || x == "winhttp.dll"), "update: clean stays clean");
        Check(fleet.Update(setup, null, CancellationToken.None).Copied == 0, "update twice: nothing to do");

        // Mods per copy: order and switches kept, other sections untouched, unknown entries preserved, only this copy changes
        var workshop = ModList.WorkshopDir(fleet);
        Check(workshop == Path.Combine(box, "steamapps", "workshop", "content", "1286220"), "Workshop folder found");
        Put(workshop, @"111\_info.ini", "[Language_en]\r\nName=Ship Pack\r\nDescription=d\r\n[Compatibility]\r\nApproximateVersion=0.8.5\r\n"); Put(workshop, @"222\_info.ini");
        var ini = Path.Combine(setup.DataDir, "usersettings.ini");
        var cleanIni = Path.Combine(clean.DataDir, "usersettings.ini");
        File.WriteAllText(ini, "[A]\r\nx=1\r\n[LoadOrder]\r\nMod2Directory=111,False,111\r\nMod1Directory=SeaPowerTopo,True,0\r\nMod3Directory=Gone,True,0\r\nNumberOfModFiles=3\r\nother=5\r\n[B]\r\ny=2\r\n");
        File.WriteAllText(cleanIni, "[A]\r\nx=9\r\n");
        var list = ModList.Read(fleet, setup);
        Console.WriteLine("mods: " + string.Join(", ", list.Select(m => $"{m.Folder}={m.Enabled}{(m.Missing ? "(missing)" : "")}")));
        Check(list[0].Folder == "SeaPowerTopo" && list[0].Enabled && list[0].IsLocal, "saved order first");
        Check(list[1].Name == "Ship Pack" && !list[1].Enabled && list[1].SteamId == 111 && list[1].MadeFor == "0.8.5", "Workshop item: name, off, version");
        Check(list[2].Missing, "unknown entry kept");
        Check(list.Single(m => m.Folder == "222").Enabled == false, "new Workshop item starts off");
        Check(!list.Any(m => m.Folder == "user" || m.Folder == "original" || m.Folder == "EntityScenes"), "game folders are not mods");
        list[1].Enabled = true; list[0].Enabled = false;
        var moved = list[0]; list[0] = list[1]; list[1] = moved;
        ModList.Save(fleet, setup, list);
        var lines = File.ReadAllLines(ini);
        Check(lines.Contains("Mod1Directory=111,True,111") && lines.Contains("Mod2Directory=SeaPowerTopo,False,0") && lines.Contains("Mod3Directory=Gone,True,0"), "saved in the game's format");
        Check(lines.Contains($"NumberOfModFiles={list.Count}") && lines.Count(l => l.StartsWith("NumberOfModFiles")) == 1, "count written once");
        Check(lines.Contains("[A]") && lines.Contains("x=1") && lines.Contains("other=5") && lines.Contains("[B]") && lines.Contains("y=2"), "the rest of the file untouched");
        Check(File.Exists(ini + ".before-flotilla") && File.ReadAllText(ini + ".before-flotilla").Contains("Mod2Directory=111,False,111"), "backup of the first state");
        Check(ModList.Read(fleet, setup)[0].Folder == "111" && File.ReadAllText(cleanIni) == "[A]\r\nx=9\r\n", "reads back; the other copy unchanged");

        var modSource = Path.Combine(box, "Downloads", "MyMod"); Put(modSource, "_info.ini", "[Language_en]\r\nName=My Mod\r\n"); Put(modSource, @"data\a.ini");
        Check(ModList.Add(fleet, setup, modSource) == "MyMod" && File.Exists(Path.Combine(Game.Streaming(setup.Dir), @"MyMod\data\a.ini")), "add a folder to one copy");
        Check(!Directory.Exists(Path.Combine(Game.Streaming(clean.Dir), "MyMod")) && !Directory.Exists(Path.Combine(sa, "MyMod")), "other copy and Steam install do not get it");
        Throws(() => ModList.Add(fleet, setup, modSource), "same mod twice refused");
        var zip = Path.Combine(box, "Downloads", "Zipped-1.0.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(modSource, zip);
        Check(ModList.Add(fleet, clean, zip) == "Zipped-1.0" && File.Exists(Path.Combine(Game.Streaming(clean.Dir), @"Zipped-1.0\_info.ini")), "add a zip with the mod at its top");
        var nested = Path.Combine(box, "Downloads", "nested.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(modSource, nested, System.IO.Compression.CompressionLevel.Fastest, true);
        Check(ModList.Add(fleet, clean, nested) == "MyMod" && File.Exists(Path.Combine(Game.Streaming(clean.Dir), @"MyMod\data\a.ini")), "add a zip with the mod in a folder");
        var bad = Path.Combine(box, "Downloads", "Plain"); Put(bad, "x.txt");
        Throws(() => ModList.Add(fleet, setup, bad), "folder without _info.ini refused");
        var reserved = Path.Combine(box, "Downloads", "original"); Put(reserved, "_info.ini");
        Throws(() => ModList.Add(fleet, setup, reserved), "game folder name refused");
        var mine = ModList.Read(fleet, setup).Single(m => m.Folder == "MyMod");
        Throws(() => ModList.Remove(fleet, setup, ModList.Read(fleet, setup).Single(m => m.Folder == "222")), "Workshop folder never deleted");
        Check(Directory.Exists(Path.Combine(workshop, "222")), "Workshop folder still there");
        running = true;
        Throws(() => ModList.Save(fleet, setup, list), "no save while the game runs");
        Throws(() => ModList.Add(fleet, setup, bad), "no add while the game runs");
        running = false;
        ModList.Remove(fleet, setup, mine);
        Check(!Directory.Exists(Path.Combine(Game.Streaming(setup.Dir), "MyMod")), "local mod removed");

        // Keep in this copy: the Workshop item's files become this copy's own, marked with where they came from
        Put(workshop, @"111\data\ship.ini", "v1");
        var oldTime = DateTime.UtcNow.AddDays(-3);
        foreach (var f in Directory.GetFiles(Path.Combine(workshop, "111"), "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(f, oldTime);
        var shipPack = ModList.Read(fleet, setup).Single(m => m.SteamId == 111 && !m.IsLocal);
        Check(ModList.Keep(fleet, setup, shipPack) == "Ship Pack (kept)", "keep: folder named after the mod");
        var keptDir = Path.Combine(Game.Streaming(setup.Dir), "Ship Pack (kept)");
        Check(File.ReadAllText(Path.Combine(keptDir, @"data\ship.ini")) == "v1" && File.Exists(Path.Combine(keptDir, ModList.KeptMarker)), "keep: files and marker copied");
        Check(!Directory.Exists(Path.Combine(Game.Streaming(clean.Dir), "Ship Pack (kept)")) && Directory.Exists(Path.Combine(workshop, "111")), "keep: other copy and Workshop folder untouched");
        var keptEntry = ModList.Read(fleet, setup).Single(m => m.Folder == "Ship Pack (kept)");
        Check(keptEntry.IsLocal && keptEntry.IsKept && keptEntry.KeptFrom == 111 && keptEntry.SteamId == 0 && keptEntry.Name == "Ship Pack", "keep: listed as a kept local mod");
        Check(Math.Abs((keptEntry.KeptAt - oldTime).TotalSeconds) < 2 && !keptEntry.WorkshopNewer && keptEntry.Where.StartsWith("Kept, "), "keep: dated by the Workshop files, not newer yet");
        Throws(() => ModList.Keep(fleet, setup, shipPack), "keep twice refused");
        Throws(() => ModList.Keep(fleet, setup, keptEntry), "a local mod cannot be kept");
        File.WriteAllText(Path.Combine(workshop, @"111\data\ship.ini"), "v2");  // Steam updates the item
        keptEntry = ModList.Read(fleet, setup).Single(m => m.Folder == "Ship Pack (kept)");
        Check(keptEntry.WorkshopNewer && keptEntry.Where.EndsWith("(Workshop newer)"), "keep: a newer Workshop download is shown");
        Check(File.ReadAllText(Path.Combine(keptDir, @"data\ship.ini")) == "v1", "keep: the Steam update does not reach the kept copy");
        var keptList = ModList.Read(fleet, setup);
        keptList.Single(m => m.Folder == "Ship Pack (kept)").Enabled = true;
        ModList.Save(fleet, setup, keptList);
        Check(File.ReadAllLines(ini).Contains("Mod" + (keptList.FindIndex(m => m.Folder == "Ship Pack (kept)") + 1) + "Directory=Ship Pack (kept),True,0"), "keep: saved as a local mod in the game's format");
        running = true;
        Throws(() => ModList.Keep(fleet, setup, shipPack), "no keep while the game runs");
        running = false;
        // The game's version rule, the Refresh of a kept mod and the check after a game update
        var approx = Compat.Section("[Compatibility]\r\n;ApproximateVersion=1.0.0\r\nApproximateVersion=0.8.5\r\n");
        var range = Compat.Section("[Language_en]\r\nName=x\r\n[Compatibility]\r\nGreaterThanEqualToVersion=0.8.7\r\nLessThanVersion=0.10.0\r\n");
        Check(Compat.Matches(approx, "0.8.7") && !Compat.Matches(approx, "0.9.0") && !Compat.Matches(approx, "0.8.4"), "rule: ApproximateVersion = same minor, patch up");
        Check(Compat.Matches(range, "0.9.0") && Compat.Matches(range, "0.8.7") && !Compat.Matches(range, "0.10.0") && !Compat.Matches(range, "0.8.6"), "rule: range");
        Check(Compat.Matches(Compat.Section("[Compatibility]\r\nExactVersion=0.9.0\r\n"), "0.9.0") && !Compat.Matches(Compat.Section("[Compatibility]\r\nExactVersion=0.9.0\r\n"), "0.9.1"), "rule: exact");
        Check(!Compat.Matches(Compat.Section("[Compatibility]\r\nGreaterThanEqualToVersion=0.8.0\r\nExcludeVersions=0.8.5, 0.9.0\r\n"), "0.9.0"), "rule: excluded version");
        Check(Compat.Matches(new Dictionary<string, string>(), "0.9.0") && Compat.Describe(range) == "0.8.7 up to 0.10.0" && Compat.Describe(approx) == "0.8.5", "rule: no declaration fits; short words");
        Check(Game.VersionOf(setup.Dir) == "0.8.8", "game version of a copy from its changelog");

        File.WriteAllText(Path.Combine(workshop, @"111\_info.ini"), "[Language_en]\r\nName=Ship Pack\r\n[Compatibility]\r\nGreaterThanEqualToVersion=0.9.0\r\n");  // the author's 0.9 version
        Put(Game.Streaming(setup.Dir), @"OldLocal\_info.ini", "[Language_en]\r\nName=Old Local\r\n[Compatibility]\r\nApproximateVersion=0.8.5\r\n");
        var checkList = ModList.Read(fleet, setup);
        foreach (var m in checkList) m.Enabled = m.Folder == "Ship Pack (kept)" || m.Folder == "222" || m.Folder == "OldLocal";
        var rows = ModList.Check(fleet, checkList, "0.9.0");
        Check(rows.Count == 3, "check: only the mods that are on");
        var keptRow = rows.Single(r => r.Mod.Folder == "Ship Pack (kept)");
        Check(!keptRow.Fits && keptRow.Step == ModList.Step.Refresh && keptRow.WorkshopFor == "0.9.0", "check: kept mod that does not fit, Workshop fits: refresh");
        Check(rows.Single(r => r.Mod.Folder == "222").Step == ModList.Step.None, "check: a fitting mod is left alone");
        Check(rows.Single(r => r.Mod.Folder == "OldLocal").Step == ModList.Step.SwitchOff, "check: nothing fits: switch off");
        Check(ModList.Check(fleet, checkList, "0.8.7").Single(r => r.Mod.Folder == "Ship Pack (kept)").Step == ModList.Step.None, "check: the kept mod fits the old game, stays frozen");
        ModList.Apply(fleet, setup, rows.Where(r => r.Step != ModList.Step.None));
        Check(File.ReadAllText(Path.Combine(keptDir, @"data\ship.ini")) == "v2" && keptRow.Mod.IsKept && keptRow.Mod.Fits("0.9.0") && !keptRow.Mod.WorkshopNewer, "refresh: kept copy now has the Workshop files, still kept");
        Check(!Directory.Exists(keptDir + ".flotilla-new") && !Directory.Exists(keptDir + ".flotilla-old"), "refresh: no leftovers");
        Check(!checkList.Single(m => m.Folder == "OldLocal").Enabled && checkList.Single(m => m.Folder == "222").Enabled, "apply: only the ticked mod switched off");
        File.WriteAllText(Path.Combine(workshop, @"111\data\ship.ini"), "v3");
        Check(File.ReadAllText(Path.Combine(keptDir, @"data\ship.ini")) == "v2", "refresh: later Steam updates still do not reach it");
        running = true;
        Throws(() => ModList.Refresh(fleet, setup, keptRow.Mod), "no refresh while the game runs");
        running = false;
        Throws(() => ModList.Refresh(fleet, setup, checkList.Single(m => m.Folder == "222")), "a Workshop mod cannot be refreshed");
        Copier.DeleteTree(Path.Combine(Game.Streaming(setup.Dir), "OldLocal"));

        ModList.Remove(fleet, setup, keptEntry);
        Check(!Directory.Exists(keptDir) && Directory.Exists(Path.Combine(workshop, "111")), "use Workshop again: kept copy gone, Workshop folder stays");
        Throws(() => ModList.Read(fleet, fleet.List()[0]), "Steam install not edited");
        File.Delete(cleanIni);
        Throws(() => ModList.Read(fleet, clean), "copy never started: asks to start it once");

        // Workshop start: recognised, placed outside the Workshop, own item hidden from the mod lists, versions compared as numbers
        Check(AppHome.InWorkshop(@"E:\Games\steamapps\workshop\content\1286220\999\SeaPowerFlotilla.exe") && !AppHome.InWorkshop(@"C:\Tools\SeaPowerFlotilla.exe"), "Workshop path recognised");
        Check(AppHome.IsNewer("0.10.0.0", "0.9.0.0") && !AppHome.IsNewer("0.1.0.0", "0.1.0.0") && !AppHome.IsNewer("0.1.0", "0.2.0") && !AppHome.IsNewer("?", "0.1.0"), "versions compared as numbers");
        var ownItem = Path.Combine(workshop, "999"); Put(ownItem, AppHome.ExeName, "new"); Put(ownItem, AppHome.ExeName + ".config", "cfg"); Put(ownItem, "_info.ini");
        var home = Path.Combine(box, "AppHome"); Put(home, AppHome.ExeName, "old");
        AppHome.Place(Path.Combine(ownItem, AppHome.ExeName), home);
        Check(File.ReadAllText(Path.Combine(home, AppHome.ExeName)) == "new" && File.Exists(Path.Combine(home, AppHome.ExeName + ".config")), "placed, replacing an older copy");
        Check(!ModList.Read(fleet, setup).Any(m => m.Folder == "999"), "Flotilla's own Workshop item is not listed as a mod");

        // Rename: the folder follows, data stays, other names and states refused
        var renamed = fleet.Rename(setup, "Pacific (DCM + 80 mods)");
        Check(renamed.Name == "Pacific (DCM + 80 mods)" && Directory.Exists(renamed.Dir) && !Directory.Exists(setup.Dir), "renamed with + and brackets");
        Check(File.Exists(Path.Combine(renamed.DataDir, "usersettings.ini")) && renamed.Kind == setup.Kind, "data and kind kept");
        Check(fleet.CheckName("Übung") == null && fleet.CheckName("Тест") == null, "non-English letters allowed");
        Throws(() => fleet.Rename(renamed, "Clean"), "name in use refused");
        Throws(() => fleet.Rename(renamed, "a\\b"), "bad name refused");
        Throws(() => fleet.Rename(fleet.List()[0], "New"), "Steam install keeps its name");
        running = true;
        Throws(() => fleet.Rename(renamed, "Later"), "no rename while the game runs");
        running = false;
        fleet.Rename(renamed, setup.Name);

        // Delete: a junction inside a copy is removed as a link, its target survives; the Steam install is never deleted
        var outside = Path.Combine(box, "outside"); Put(outside, "keep.txt");
        Junction(Path.Combine(clean.Dir, "link"), outside);
        fleet.Delete(clean);
        Check(!Directory.Exists(clean.Dir) && File.Exists(Path.Combine(outside, "keep.txt")), "delete: copy gone, link target kept");
        Throws(() => fleet.Delete(fleet.List()[0]), "Steam install never deleted");
        Check(File.Exists(Path.Combine(game, "Sea Power.exe")), "Steam install still there");

        CheckCommandLine(fleet);

        Console.WriteLine(failures == 0 ? "ALL OK" : $"{failures} FAILED");
        if (failures == 0) Copier.DeleteTree(box);
        return failures == 0 ? 0 : 1;
    }

    static void CheckCommandLine(Fleet fleet)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int Run(params string[] args)
        {
            output.GetStringBuilder().Clear();
            error.GetStringBuilder().Clear();
            return CommandLine.Run(args, () => fleet, output, error);
        }

        Check(CommandLine.Run(["--help"], () => throw new InvalidOperationException(), output, error) == 0
            && output.ToString().Contains("--cli"), "CLI help works without loading the fleet");
        Check(Run("list", "--unknown") == 1 && output.ToString() == "" && error.ToString().Contains("Usage"),
            "CLI rejects invalid arguments on stderr");
        Check(Run("create", "CLI copy") == 0 && fleet.Find("CLI copy")?.Clean == true, "CLI creates a clean copy");
        Check(Run("create", "CLI setup", "--setup") == 0 && fleet.Find("CLI setup")?.Kind == Fleet.KindSetup, "CLI creates a setup copy");
        Check(Run("list", "--json") == 0 && output.ToString().Contains("\"name\":\"CLI copy\"")
            && output.ToString().Contains("\"kind\":\"clean\"") && error.ToString() == "", "CLI lists installs as JSON");
        Check(Run("update", "CLI copy") == 0 && output.ToString().Contains("files copied"), "CLI updates a copy");
        Check(Run("play", "unknown") == 2 && error.ToString().Contains("unknown"), "CLI reports an unknown copy on stderr");

        bool played = false;
        fleet.RunGame = install => played = install.Name == "CLI copy" && fleet.IsActive(install);
        Check(Run("play", "cli copy") == 0 && played && !fleet.Swap.NeedsRepair, "CLI plays a copy and restores Steam data");
        Check(Run("delete", "CLI copy") == 1 && fleet.Find("CLI copy") != null, "CLI delete requires --yes");
        Check(Run("delete", "Steam", "--yes") == 2 && fleet.Find("Steam") != null, "CLI refuses to delete the Steam install");
        Check(Run("delete", "CLI copy", "--yes") == 0 && fleet.Find("CLI copy") == null, "CLI deletes a confirmed copy");
    }

    static void Put(string root, string rel, string text = "x")
    {
        var path = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }

    static List<string> Tree(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(f => f.Substring(root.Length + 1)).OrderBy(f => f).ToList();

    static void Junction(string link, string target)
    {
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
        p.WaitForExit();
    }

    static void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Console.WriteLine($"  [{(ok ? " ok " : "FAIL")}] {what}");
    }

    static void Throws(Action action, string what)
    {
        try { action(); Check(false, what); }
        catch (FlotillaException e) { Check(true, $"{what} ({e.Message.Split('\n')[0]})"); }
    }
}
