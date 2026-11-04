using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Flotilla
{
    /// <summary>Finds Sea Power in the Steam libraries; nothing here changes anything.</summary>
    public static class Steam
    {
        public static bool IsRunning()
        {
            var found = Process.GetProcessesByName("steam");
            foreach (var p in found) p.Dispose();
            return found.Length > 0;
        }

        public static string InstallPath()
        {
            var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                       ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path.Replace('/', '\\'));
        }

        public static List<string> Libraries(string steam)
        {
            var libraries = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            return libraries.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The Sea Power folder of the Steam library that has its app manifest, or null.</summary>
        public static string FindGame()
        {
            var steam = InstallPath();
            if (steam == null) return null;
            foreach (var library in Libraries(steam))
            {
                var manifest = Path.Combine(library, "steamapps", $"appmanifest_{Game.AppId}.acf");
                if (!File.Exists(manifest)) continue;
                var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                if (!m.Success) continue;
                var dir = Path.Combine(library, "steamapps", "common", m.Groups[1].Value);
                if (File.Exists(Path.Combine(dir, Game.Exe))) return dir;
            }
            return null;
        }
    }
}
