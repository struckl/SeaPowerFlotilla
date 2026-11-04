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

        // Delete: a junction inside a copy is removed as a link, its target survives; the Steam install is never deleted
        var outside = Path.Combine(box, "outside"); Put(outside, "keep.txt");
        Junction(Path.Combine(clean.Dir, "link"), outside);
        fleet.Delete(clean);
        Check(!Directory.Exists(clean.Dir) && File.Exists(Path.Combine(outside, "keep.txt")), "delete: copy gone, link target kept");
        Throws(() => fleet.Delete(fleet.List()[0]), "Steam install never deleted");
        Check(File.Exists(Path.Combine(game, "Sea Power.exe")), "Steam install still there");

        Console.WriteLine(failures == 0 ? "ALL OK" : $"{failures} FAILED");
        if (failures == 0) Copier.DeleteTree(box);
        return failures == 0 ? 0 : 1;
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
