using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Flotilla
{
    public sealed class ModEntry
    {
        public string Name, Folder, Dir, MadeFor;
        public ulong SteamId;
        public bool Enabled, IsLocal;
        /// <summary>In the saved list but not on this PC right now (say, an unsubscribed item). Kept, not shown.</summary>
        public bool Missing;
        /// <summary>A local folder that is this copy's own copy of a Workshop item ("Keep in this copy"): the item's id, else 0.</summary>
        public ulong KeptFrom;
        /// <summary>When the kept files were downloaded by Steam (their newest file).</summary>
        public DateTime KeptAt;
        /// <summary>Steam has a newer download of the item than the kept copy.</summary>
        public bool WorkshopNewer;
        /// <summary>The [Compatibility] section of its _info.ini: which game versions it says it fits.</summary>
        public Dictionary<string, string> Decl = new Dictionary<string, string>();

        public bool Fits(string gameVersion) => Compat.Matches(Decl, gameVersion);

        public bool IsKept => KeptFrom != 0;

        public string Where =>
            !IsLocal ? "Workshop"
            : !IsKept ? "This copy only"
            : $"Kept, {KeptAt:yyyy-MM-dd}" + (WorkshopNewer ? " (Workshop newer)" : "");
    }

    /// <summary>
    /// The mods of one copy: its own StreamingAssets folders plus the Workshop downloads, and the copy's on/off list and order,
    /// which the game keeps in usersettings.ini, section [LoadOrder], as ModNDirectory=folder,enabled,steamId.
    /// Copies only: Flotilla does not change the Steam install.
    /// </summary>
    public static class ModList
    {
        const string Section = "[LoadOrder]";
        static readonly Regex Entry = new Regex(@"^Mod(\d+)Directory=(.*)$", RegexOptions.IgnoreCase);
        static readonly HashSet<string> NotMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EntityScenes", "original", "user" };

        static string SettingsFile(Install install) => Path.Combine(install.DataDir, "usersettings.ini");

        /// <summary>The game writes its settings on the first start; Flotilla only edits that file, it never invents one.</summary>
        public static bool HasSettings(Install install) => File.Exists(SettingsFile(install));

        public static string WorkshopDir(Fleet fleet) =>
            Path.GetFullPath(Path.Combine(fleet.SteamGame, "..", "..", "workshop", "content", Game.AppId.ToString()));

        public static List<ModEntry> Read(Fleet fleet, Install install)
        {
            NeedCopy(install);
            if (!HasSettings(install)) throw new FlotillaException($"Start \"{install.Name}\" once and close it again: the game has to write its settings first.");
            var available = Available(fleet, install);
            var result = new List<ModEntry>();
            foreach (var (folder, enabled, id) in SavedOrder(File.ReadAllLines(SettingsFile(install))))
            {
                var mod = (id != 0 ? available.FirstOrDefault(m => m.SteamId == id) : null)
                          ?? available.FirstOrDefault(m => Layout.Same(m.Folder, folder));
                if (mod == null) { result.Add(new ModEntry { Name = folder, Folder = folder, SteamId = id, Enabled = enabled, Missing = true }); continue; }
                mod.Enabled = enabled;
                available.Remove(mod);
                result.Add(mod);
            }
            result.AddRange(available);  // mods the list does not know yet start switched off, as in the game
            return result;
        }

        static List<ModEntry> Available(Fleet fleet, Install install)
        {
            var mods = new List<ModEntry>();
            foreach (var dir in Directory.GetDirectories(Game.Streaming(install.Dir)).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                if (!NotMods.Contains(Path.GetFileName(dir)))
                    mods.Add(Describe(dir, 0, true));
            var workshop = WorkshopDir(fleet);
            if (Directory.Exists(workshop))
                foreach (var dir in Directory.GetDirectories(workshop).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                    if (ulong.TryParse(Path.GetFileName(dir), out var id) && !File.Exists(Path.Combine(dir, AppHome.ExeName)))  // Flotilla's own item is no mod
                        mods.Add(Describe(dir, id, false));
            foreach (var kept in mods.Where(m => m.IsKept))
            {
                var item = mods.FirstOrDefault(m => !m.IsLocal && m.SteamId == kept.KeptFrom);
                kept.WorkshopNewer = item != null && Newest(item.Dir) > kept.KeptAt.AddSeconds(2);
            }
            return mods;
        }

        /// <summary>Written into a kept folder: which Workshop item it came from and how new its files were.</summary>
        public const string KeptMarker = "flotilla-kept.txt";

        /// <summary>The newest file time in a folder: when Steam last downloaded the item.</summary>
        static DateTime Newest(string dir)
        {
            var times = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f => !Layout.Same(Path.GetFileName(f), KeptMarker)).Select(File.GetLastWriteTimeUtc);
            return times.DefaultIfEmpty(Directory.GetLastWriteTimeUtc(dir)).Max();
        }

        static void ReadMarker(ModEntry mod)
        {
            var marker = Path.Combine(mod.Dir, KeptMarker);
            if (!File.Exists(marker)) return;
            foreach (var line in File.ReadAllLines(marker))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (Layout.Same(key, "WorkshopId")) ulong.TryParse(value, out mod.KeptFrom);
                else if (Layout.Same(key, "FilesFrom") && DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at)) mod.KeptAt = at;
            }
        }

        static ModEntry Describe(string dir, ulong id, bool local)
        {
            var mod = new ModEntry { Folder = Path.GetFileName(dir), Dir = dir, SteamId = id, IsLocal = local, Name = Path.GetFileName(dir), MadeFor = "" };
            if (local) ReadMarker(mod);
            var info = Path.Combine(dir, "_info.ini");
            if (!File.Exists(info)) return mod;
            var text = File.ReadAllText(info);
            mod.Decl = Compat.Section(text);
            mod.MadeFor = Compat.Describe(mod.Decl);
            var name = Regex.Match(text, @"\[Language_en\][^\[]*?^Name=(.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (name.Success) mod.Name = name.Groups[1].Value;
            return mod;
        }

        static IEnumerable<(string folder, bool enabled, ulong id)> SavedOrder(string[] lines)
        {
            var found = new List<(int n, string folder, bool enabled, ulong id)>();
            foreach (var line in SectionLines(lines))
            {
                var m = Entry.Match(line.Trim());
                if (!m.Success) continue;
                var parts = m.Groups[2].Value.Split(',');
                bool enabled = true;
                ulong id = 0;
                if (parts.Length > 1) bool.TryParse(parts[1].Trim(), out enabled);
                if (parts.Length > 2) ulong.TryParse(parts[2].Trim(), out id);
                found.Add((int.Parse(m.Groups[1].Value), parts[0].Trim(), enabled, id));
            }
            return found.OrderBy(f => f.n).Select(f => (f.folder, f.enabled, f.id));
        }

        static IEnumerable<string> SectionLines(string[] lines)
        {
            int start = Array.FindIndex(lines, l => Layout.Same(l.Trim(), Section));
            if (start < 0) yield break;
            for (int i = start + 1; i < lines.Length && !lines[i].TrimStart().StartsWith("["); i++) yield return lines[i];
        }

        /// <summary>Replaces the mod lines of [LoadOrder] and nothing else; the file before the first change stays next to it as a backup.</summary>
        public static void Save(Fleet fleet, Install install, IList<ModEntry> mods)
        {
            NeedClosed(fleet, install);
            var file = SettingsFile(install);
            var lines = File.ReadAllLines(file).ToList();
            var backup = file + ".before-flotilla";
            if (!File.Exists(backup)) File.Copy(file, backup);
            var written = mods.Select((m, i) => $"Mod{i + 1}Directory={m.Folder},{(m.Enabled ? "True" : "False")},{m.SteamId}").ToList();
            written.Add($"NumberOfModFiles={mods.Count}");
            int start = lines.FindIndex(l => Layout.Same(l.Trim(), Section));
            if (start < 0)
            {
                lines.Add(Section);
                lines.AddRange(written);
            }
            else
            {
                int end = start + 1;
                while (end < lines.Count && !lines[end].TrimStart().StartsWith("[")) end++;
                var others = lines.GetRange(start + 1, end - start - 1)
                    .Where(l => !Entry.IsMatch(l.Trim()) && !l.Trim().StartsWith("NumberOfModFiles=", StringComparison.OrdinalIgnoreCase)).ToList();
                lines.RemoveRange(start + 1, end - start - 1);
                lines.InsertRange(start + 1, written.Concat(others));
            }
            File.WriteAllLines(file, lines, new UTF8Encoding(false));
            fleet.Log($"mods of '{install.Name}': {mods.Count(m => m.Enabled)} of {mods.Count} on");
        }

        /// <summary>Installs a mod folder, or the mod inside a .zip, into this copy only. Returns its folder name.</summary>
        public static string Add(Fleet fleet, Install install, string source)
        {
            NeedClosed(fleet, install);
            if (File.Exists(source) && Layout.Same(Path.GetExtension(source), ".zip"))
            {
                var temp = Path.Combine(Path.GetTempPath(), "flotilla-" + Guid.NewGuid().ToString("N"));
                try
                {
                    ZipFile.ExtractToDirectory(source, temp);
                    var info = Directory.GetFiles(temp, "_info.ini", SearchOption.AllDirectories).OrderBy(f => f.Count(c => c == '\\')).FirstOrDefault();
                    if (info == null) throw new FlotillaException("There is no mod in that zip: it has no _info.ini.");
                    var root = Path.GetDirectoryName(info);
                    var name = Layout.Same(root.TrimEnd('\\'), temp.TrimEnd('\\')) ? Path.GetFileNameWithoutExtension(source) : Path.GetFileName(root);
                    return CopyIn(fleet, install, root, name);
                }
                finally { Copier.DeleteTree(temp); }
            }
            if (!Directory.Exists(source)) throw new FlotillaException($"Not found: {source}");
            if (!File.Exists(Path.Combine(source, "_info.ini"))) throw new FlotillaException("That folder is not a Sea Power mod: it has no _info.ini.");
            return CopyIn(fleet, install, source, Path.GetFileName(source.TrimEnd('\\')));
        }

        static string CopyIn(Fleet fleet, Install install, string source, string name)
        {
            if (NotMods.Contains(name) || name.Contains(",") || name.Length == 0) throw new FlotillaException($"A mod cannot be installed under the name \"{name}\".");
            var target = Path.Combine(Game.Streaming(install.Dir), name);
            if (Directory.Exists(target)) throw new FlotillaException($"\"{install.Name}\" already has a mod folder \"{name}\". Remove it first to replace it.");
            Copier.Copy(target, Copier.Scan(source, (rel, isDir) => true), null, default);
            fleet.Log($"added mod '{name}' to '{install.Name}'");
            return name;
        }

        /// <summary>
        /// "Keep in this copy": copies a Workshop item's files into this copy as a local mod, so Steam updates and unsubscribing no
        /// longer change it here. Returns the new folder name; the caller switches the Workshop entry off in this copy.
        /// </summary>
        public static string Keep(Fleet fleet, Install install, ModEntry item)
        {
            NeedClosed(fleet, install);
            if (item.IsLocal || item.SteamId == 0) throw new FlotillaException("Only a Workshop mod can be kept in a copy.");
            if (!Directory.Exists(item.Dir)) throw new FlotillaException($"Steam has no files of \"{item.Name}\" on this PC right now.");
            var name = new string(item.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != ',').ToArray()).Trim().TrimEnd('.');
            if (name.Length == 0 || NotMods.Contains(name)) name = item.SteamId.ToString();
            name += " (kept)";
            var filesFrom = Newest(item.Dir);
            var folder = CopyIn(fleet, install, item.Dir, name);
            WriteMarker(Path.Combine(Game.Streaming(install.Dir), folder), item, filesFrom);
            fleet.Log($"kept Workshop item {item.SteamId} in '{install.Name}' as '{folder}'");
            return folder;
        }

        static void WriteMarker(string dir, ModEntry item, DateTime filesFrom) =>
            File.WriteAllLines(Path.Combine(dir, KeptMarker), new[]
            {
                "; Written by R.A.T.S. Flotilla: this copy's own copy of a Workshop item. Steam updates do not change it.",
                $"WorkshopId={item.SteamId}",
                $"Name={item.Name}",
                $"FilesFrom={filesFrom.ToString("o", System.Globalization.CultureInfo.InvariantCulture)}",
                $"KeptOn={DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)}",
            }, new UTF8Encoding(false));

        /// <summary>The Workshop download a kept mod came from, or null when the item is not on this PC (unsubscribed).</summary>
        public static ModEntry WorkshopOf(Fleet fleet, ModEntry kept)
        {
            if (!kept.IsKept) return null;
            var dir = Path.Combine(WorkshopDir(fleet), kept.KeptFrom.ToString());
            return Directory.Exists(dir) ? Describe(dir, kept.KeptFrom, false) : null;
        }

        /// <summary>
        /// "Refresh from Workshop": replaces a kept mod's files with the current Workshop download; it stays kept (frozen) at that
        /// state. The new files go next to the old ones first, so a failed copy leaves the kept mod as it was. Updates the entry.
        /// </summary>
        public static void Refresh(Fleet fleet, Install install, ModEntry kept)
        {
            NeedClosed(fleet, install);
            if (!kept.IsKept) throw new FlotillaException("Only a kept mod can be refreshed from the Workshop.");
            var item = WorkshopOf(fleet, kept);
            if (item == null) throw new FlotillaException($"\"{kept.Name}\" is not subscribed on the Workshop right now, so there is nothing to refresh it from. Subscribe to it in Steam first.");
            var filesFrom = Newest(item.Dir);
            var fresh = kept.Dir + ".flotilla-new";
            var old = kept.Dir + ".flotilla-old";
            Copier.DeleteTree(fresh);
            Copier.DeleteTree(old);
            try
            {
                Copier.Copy(fresh, Copier.Scan(item.Dir, (rel, isDir) => true), null, default);
                WriteMarker(fresh, item, filesFrom);
                Directory.Move(kept.Dir, old);
                Directory.Move(fresh, kept.Dir);
            }
            catch
            {
                if (!Directory.Exists(kept.Dir) && Directory.Exists(old)) Directory.Move(old, kept.Dir);
                Copier.DeleteTree(fresh);
                throw;
            }
            Copier.DeleteTree(old);
            var now = Describe(kept.Dir, 0, true);
            kept.Name = now.Name;
            kept.Decl = now.Decl;
            kept.MadeFor = now.MadeFor;
            kept.KeptAt = now.KeptAt;
            kept.WorkshopNewer = false;
            fleet.Log($"refreshed kept '{kept.Folder}' in '{install.Name}' from Workshop item {kept.KeptFrom}");
        }

        public enum Step { None, Refresh, SwitchOff }

        public sealed class CheckRow
        {
            public ModEntry Mod;
            public bool Fits;
            public Step Step;
            /// <summary>For a Refresh: what the Workshop version says it fits.</summary>
            public string WorkshopFor;
        }

        /// <summary>
        /// The mod check against a game version: every mod that is on in the list, whether it fits, and what to do when it does not:
        /// a kept mod whose Workshop version fits is refreshed (ticked), anything else that does not fit can be switched off in this
        /// copy (unticked: the game only warns about such a mod and still runs it on Ignore).
        /// </summary>
        public static List<CheckRow> Check(Fleet fleet, IEnumerable<ModEntry> mods, string gameVersion)
        {
            var rows = new List<CheckRow>();
            foreach (var mod in mods.Where(m => m.Enabled && !m.Missing))
            {
                var row = new CheckRow { Mod = mod, Fits = mod.Fits(gameVersion) };
                if (!row.Fits)
                {
                    var item = WorkshopOf(fleet, mod);
                    if (item != null && item.Fits(gameVersion)) { row.Step = Step.Refresh; row.WorkshopFor = item.MadeFor; }
                    else row.Step = Step.SwitchOff;
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Runs the ticked steps: refreshes kept mods (files change at once) and switches mods off in the list (saved by the caller).</summary>
        public static void Apply(Fleet fleet, Install install, IEnumerable<CheckRow> ticked)
        {
            foreach (var row in ticked)
                if (row.Step == Step.Refresh) Refresh(fleet, install, row.Mod);
                else if (row.Step == Step.SwitchOff) row.Mod.Enabled = false;
        }

        /// <summary>Deletes a mod folder of this copy. Workshop items are never deleted: they belong to every install.</summary>
        public static void Remove(Fleet fleet, Install install, ModEntry mod)
        {
            NeedClosed(fleet, install);
            if (!mod.IsLocal) throw new FlotillaException("Workshop items are shared by all installs. Switch it off here, or unsubscribe in Steam.");
            fleet.Log($"removed mod '{mod.Folder}' from '{install.Name}'");
            Copier.DeleteTree(mod.Dir);
        }

        static void NeedCopy(Install install)
        {
            if (install.IsSteam) throw new FlotillaException("Flotilla does not change the Steam install. Use the mod menu in the game for that one.");
        }

        static void NeedClosed(Fleet fleet, Install install)
        {
            NeedCopy(install);
            if (fleet.GameRunning()) throw new FlotillaException("Sea Power is running. Close it first.");
        }
    }
}
