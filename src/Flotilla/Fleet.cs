using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Flotilla
{
    public sealed class Install
    {
        public string Name, Dir, Kind;
        public bool IsSteam;

        public string DataDir => Path.Combine(Dir, Layout.DataFolder);
        /// <summary>Reads the game's changelog each time: after an Update the same install has a new build.</summary>
        public string ReadBuild() => Game.BuildOf(Dir);
        public bool Clean => Kind == Fleet.KindClean;
        public string KindText => IsSteam ? "Steam install" : Clean ? "Clean" : "Copy of setup";
    }

    /// <summary>The Steam install plus the copies in the fleet folder, and everything that can be done with them.</summary>
    public sealed class Fleet
    {
        public const string KindClean = "clean", KindSetup = "setup", KindSteam = "steam";
        /// <summary>Free space a new copy leaves on its drive, so the PC and the game still have room to write.</summary>
        public const long SpareBytes = 1L << 30;
        public const string ClosedFirst = "Sea Power is running. Close it first.";

        public string SteamGame;
        public string FleetDir;
        public DataSwap Swap = new DataSwap();
        public Func<bool> GameRunning = Game.IsRunning;
        public Func<bool> SteamRunning = Steam.IsRunning;
        /// <summary>Starts the copy's exe and returns when it has exited.</summary>
        public Action<Install> RunGame = DefaultRun;
        public Action<string> Log = _ => { };

        public static string DefaultFleetDir(string steamGame) =>
            steamGame == null ? null : Path.GetFullPath(Path.Combine(steamGame, "..", "..", "..", "Sea Power Flotilla"));

        public List<Install> List()
        {
            var list = new List<Install>();
            if (SteamGame != null && File.Exists(Path.Combine(SteamGame, Game.Exe)))
                list.Add(new Install { Name = "Steam", Dir = SteamGame, IsSteam = true, Kind = KindSteam });
            if (FleetDir != null && Directory.Exists(FleetDir))
                foreach (var dir in Directory.GetDirectories(FleetDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                    if (File.Exists(Path.Combine(dir, Game.Exe)) && File.Exists(Path.Combine(dir, Layout.Marker)))
                        list.Add(ReadMarker(dir));
            return list;
        }

        public Install Find(string name) =>
            List().FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

        static Install ReadMarker(string dir)
        {
            var install = new Install { Name = Path.GetFileName(dir), Dir = dir, Kind = KindSetup };
            foreach (var line in File.ReadAllLines(Path.Combine(dir, Layout.Marker)))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0] == "kind") install.Kind = parts[1].Trim();
            }
            return install;
        }

        /// <summary>Null if the name can be used for a new copy, else why not.</summary>
        public string CheckName(string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length == 0) return "Give the copy a name.";
            if (name.Length > 40) return "Keep the name under 40 characters.";
            if (!Regex.IsMatch(name, @"^[\p{L}\p{N}][\p{L}\p{N} _\-+()&]*$")) return "Use letters, digits, spaces and - _ + ( ) & only.";
            if (string.Equals(name, "Steam", StringComparison.OrdinalIgnoreCase)) return "\"Steam\" is the Steam install itself.";
            if (NameInUse(name)) return $"There is already a folder \"{name}\" in {FleetDir}.";
            return CheckFleetDir(FleetDir);
        }

        public bool NameInUse(string name) => FleetDir != null && Directory.Exists(Path.Combine(FleetDir, name));

        /// <summary>Null if copies can live in this folder: not inside the game (a copy would copy itself) and not inside another copy.</summary>
        public string CheckFleetDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return "Choose a folder for the copies.";
            if (Layout.IsInside(dir, SteamGame))
                return "The copies cannot live inside the Sea Power folder itself. Choose a folder next to it.";
            return null;
        }

        public CopyPlan PlanCopy(bool clean) => Copier.Scan(SteamGame, clean ? (Func<string, bool, bool>)Layout.IsGame : Layout.IsSetup);

        public CopyPlan PlanData() =>
            Swap.Switched || !Directory.Exists(Swap.Live) ? new CopyPlan() : Copier.Scan(Swap.Live, Layout.IsKeptData);

        public static long FreeBytes(string folder)
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            return new DriveInfo(root).AvailableFreeSpace;
        }

        /// <summary>Back to normal if a run of a copy did not end cleanly (PC turned off, Flotilla killed).</summary>
        public bool EnsureNormal()
        {
            if (!Swap.NeedsRepair) return false;
            NeedClosed();
            Swap.Repair();
            Log("put the Steam install's data folder back");
            return true;
        }

        public Install Create(string name, bool clean, IProgress<double> progress, CancellationToken cancel)
        {
            var problem = CheckName(name);
            if (problem != null) throw new FlotillaException(problem);
            if (GameRunning()) throw new FlotillaException("Sea Power is running. Close it first: its files and settings are in use.");
            EnsureNormal();
            name = name.Trim();
            var dir = Path.Combine(FleetDir, name);
            var plan = PlanCopy(clean);
            var data = clean ? new CopyPlan() : PlanData();
            Directory.CreateDirectory(FleetDir);
            var need = plan.Bytes + data.Bytes;
            if (FreeBytes(FleetDir) < need + SpareBytes)
                throw new FlotillaException($"Not enough space: the copy needs {Gb(need)} and {Path.GetPathRoot(FleetDir)} has {Gb(FreeBytes(FleetDir))} free.");
            Log($"create {(clean ? "clean" : "setup")} copy '{name}' in {dir}: {plan.Files.Count} files, {Gb(plan.Bytes)}");
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, Layout.UnfinishedMarker), Process.GetCurrentProcess().Id.ToString());
                Copier.Copy(dir, plan, progress, cancel);
                Copier.Copy(Path.Combine(dir, Layout.DataFolder), data, null, cancel);
                Game.WriteAppIdFile(dir);
                // created= and from= are read by no code: they tell a person looking into the folder what it is.
                File.WriteAllLines(Path.Combine(dir, Layout.Marker), new[]
                {
                    $"; {Program.Title}: this folder is a copy of Sea Power. Delete it in Flotilla.",
                    $"kind={(clean ? KindClean : KindSetup)}",
                    $"created={DateTime.Now:yyyy-MM-dd HH:mm}",
                    $"from={Game.BuildOf(SteamGame)}",
                });
                File.Delete(Path.Combine(dir, Layout.UnfinishedMarker));
            }
            catch (Exception)
            {
                Copier.DeleteTree(dir);
                throw;
            }
            return Find(name);
        }

        /// <summary>
        /// Folders Flotilla started copying and never finished (PC turned off, Flotilla killed). Only folders with Flotilla's
        /// own marker count, and none that a running Flotilla is still filling.
        /// </summary>
        public List<string> Unfinished()
        {
            if (FleetDir == null || !Directory.Exists(FleetDir)) return new List<string>();
            return Directory.GetDirectories(FleetDir)
                .Where(d => File.Exists(Path.Combine(d, Layout.UnfinishedMarker)) && !File.Exists(Path.Combine(d, Layout.Marker)) && !BeingMade(d))
                .ToList();
        }

        static bool BeingMade(string dir)
        {
            if (!int.TryParse(File.ReadAllText(Path.Combine(dir, Layout.UnfinishedMarker)).Trim(), out var id)) return false;
            try
            {
                using (var maker = Process.GetProcessById(id))
                using (var self = Process.GetCurrentProcess())
                    return maker.ProcessName == self.ProcessName;
            }
            catch (ArgumentException) { return false; }  // no process with that id any more
        }

        public void RemoveUnfinished(string dir)
        {
            Log($"remove unfinished copy {dir}");
            Copier.DeleteTree(dir);
        }

        /// <summary>After a Steam update: the copy's game files become the Steam install's; its mods, missions and data stay.</summary>
        public SyncResult Update(Install install, IProgress<double> progress, CancellationToken cancel)
        {
            if (install.IsSteam) throw new FlotillaException("Steam updates the Steam install itself.");
            NeedClosed();
            Log($"update '{install.Name}' from {install.ReadBuild()} to {Game.BuildOf(SteamGame)}");
            var result = Copier.Sync(SteamGame, install.Dir, Layout.IsGame, progress, cancel);
            Log($"update '{install.Name}': {result.Copied} files copied, {result.Deleted} removed");
            return result;
        }

        /// <summary>Gives a copy a new name, which is also the name of its folder.</summary>
        public Install Rename(Install install, string name)
        {
            if (install.IsSteam) throw new FlotillaException("The Steam install keeps its name.");
            var problem = CheckName(name);
            if (problem != null) throw new FlotillaException(problem);
            NeedClosed();
            EnsureNormal();
            name = name.Trim();
            Log($"rename '{install.Name}' to '{name}'");
            Directory.Move(install.Dir, Path.Combine(FleetDir, name));
            return Find(name);
        }

        public void Delete(Install install)
        {
            if (install.IsSteam) throw new FlotillaException("Flotilla never deletes the Steam install. Uninstall it in Steam.");
            if (IsActive(install))
            {
                if (GameRunning()) throw new FlotillaException($"\"{install.Name}\" is running. Close Sea Power first.");
                Swap.Repair();
            }
            Log($"delete '{install.Name}' ({install.Dir})");
            Copier.DeleteTree(install.Dir);
        }

        public bool IsActive(Install install) =>
            !install.IsSteam && Swap.Switched && Swap.ActiveCopy != null && Layout.SameFolder(Swap.ActiveCopy, install.Dir);

        /// <summary>Throws while Sea Power runs: its files and the data folder are in use.</summary>
        public void NeedClosed()
        {
            if (GameRunning()) throw new FlotillaException(ClosedFirst);
        }

        /// <summary>The checks before a start; throws with the reason if it cannot start now.</summary>
        public void CheckCanPlay()
        {
            if (GameRunning()) throw new FlotillaException("Sea Power is already running. One at a time.");
            if (!SteamRunning()) throw new FlotillaException("Start Steam first. Sea Power needs it for your licence and the Workshop.");
        }

        /// <summary>Starts an install and, for a copy, waits until the game has exited and puts the data folder back.</summary>
        public void Play(Install install)
        {
            CheckCanPlay();
            EnsureNormal();
            if (install.IsSteam)
            {
                Process.Start($"steam://rungameid/{Game.AppId}")?.Dispose();
                return;
            }
            Game.WriteAppIdFile(install.Dir);
            Swap.SwitchTo(install.DataDir, install.Dir);
            Log($"play '{install.Name}'");
            try
            {
                RunGame(install);
                // The crash handler can outlive the exe for a moment, and the game must be gone before its folder moves.
                while (GameRunning()) Thread.Sleep(2000);
            }
            finally
            {
                Swap.Repair();
                Log($"'{install.Name}' closed, Steam install's data folder back");
            }
        }

        static void DefaultRun(Install install)
        {
            var info = new ProcessStartInfo(Path.Combine(install.Dir, Game.Exe)) { WorkingDirectory = install.Dir, UseShellExecute = false };
            using (var game = Process.Start(info)) game.WaitForExit();
        }

        public static string Gb(long bytes) => $"{bytes / 1e9:0.0} GB";
    }
}
