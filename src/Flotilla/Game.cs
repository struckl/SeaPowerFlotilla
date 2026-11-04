using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flotilla
{
    /// <summary>A problem the player can act on; shown as a message, never as a crash.</summary>
    public sealed class FlotillaException : Exception
    {
        public FlotillaException(string message) : base(message) { }
    }

    public static class Game
    {
        public const int AppId = 1286220;
        public const string Exe = "Sea Power.exe";
        public const string ProcessName = "Sea Power";
        public const string Data = "Sea Power_Data";

        public static bool IsRunning()
        {
            var found = Process.GetProcessesByName(ProcessName);
            foreach (var p in found) p.Dispose();
            return found.Length > 0;
        }

        public static string Streaming(string dir) => Path.Combine(dir, Data, "StreamingAssets");

        /// <summary>"0.8.7 (build 261021)" from the first dated line of the game's changelog.</summary>
        public static string BuildOf(string dir)
        {
            var changelog = Path.Combine(Streaming(dir), "changelog.txt");
            if (!File.Exists(changelog)) return "?";
            foreach (var line in File.ReadLines(changelog))
            {
                var m = Regex.Match(line, @"^\S+:\s+(\S+)\s+Build\s+(\d+)");
                if (m.Success) return $"{m.Groups[1].Value} (build {m.Groups[2].Value})";
            }
            return "?";
        }

        public static bool HasLoader(string dir) =>
            File.Exists(Path.Combine(dir, "winhttp.dll")) || Directory.Exists(Path.Combine(dir, "BepInEx"));

        /// <summary>Mod folders in StreamingAssets: everything that is not the game's own data, user missions or Anchor Chain's configs.</summary>
        public static List<string> LocalMods(string dir)
        {
            var streaming = Streaming(dir);
            if (!Directory.Exists(streaming)) return new List<string>();
            return Directory.GetDirectories(streaming).Select(Path.GetFileName)
                .Where(n => !Layout.GameStreaming.Contains(n) && !Layout.Same(n, "user") && !Layout.Same(n, "ACConfigs"))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>
    /// Which files of an install go where. Paths are relative to the install folder, with '\' separators.
    /// Steam installs the game files; the mod loader (BepInEx + doorstop) and the StreamingAssets mod folders
    /// are added by players, the user folder and the logs by the game.
    /// </summary>
    public static class Layout
    {
        static readonly StringComparer C = StringComparer.OrdinalIgnoreCase;
        public static bool Same(string a, string b) => C.Equals(a, b);

        public static readonly HashSet<string> LoaderDirs = new HashSet<string>(C) { "BepInEx" };
        public static readonly HashSet<string> LoaderFiles = new HashSet<string>(C) { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };
        public static readonly HashSet<string> GameStreaming = new HashSet<string>(C) { "EntityScenes", "original" };
        public static readonly HashSet<string> OwnDirs = new HashSet<string>(C) { Fleet.DataFolder };
        public static readonly HashSet<string> OwnFiles = new HashSet<string>(C) { "steam_appid.txt", Fleet.Marker };
        static readonly HashSet<string> SkipDataDirs = new HashSet<string>(C) { "CrashDiagnostics", "Sentry", ".sentry-native", "temp", "cache" };

        /// <summary>Files Steam installs: what a clean copy gets and what Update refreshes.</summary>
        public static bool IsGame(string rel, bool isDir)
        {
            var s = rel.Split('\\');
            if (s.Length == 1)
                return isDir ? !LoaderDirs.Contains(s[0]) && !OwnDirs.Contains(s[0])
                             : !LoaderFiles.Contains(s[0]) && !OwnFiles.Contains(s[0]);
            if (LoaderDirs.Contains(s[0]) || OwnDirs.Contains(s[0])) return false;
            if (Same(s[0], Game.Data))
            {
                if (Same(s[1], "Logs")) return false;
                if (Same(s[1], "StreamingAssets") && s.Length >= 3)
                {
                    bool inFolder = s.Length > 3 || isDir;
                    if (inFolder && !GameStreaming.Contains(s[2])) return false;
                }
            }
            return true;
        }

        /// <summary>A copy of the whole setup: everything except logs, caches and Flotilla's own files.</summary>
        public static bool IsSetup(string rel, bool isDir)
        {
            var s = rel.Split('\\');
            if (s.Length == 1) return isDir ? !OwnDirs.Contains(s[0]) : !OwnFiles.Contains(s[0]);
            if (Same(s[0], Game.Data) && Same(s[1], "Logs")) return false;
            if (Same(s[0], "BepInEx"))
            {
                if (Same(s[1], "cache")) return false;
                if (s.Length == 2 && !isDir && s[1].StartsWith("LogOutput", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        /// <summary>Of the LocalLow data: settings, enabled mods, saves; not logs, crash reports and caches.</summary>
        public static bool IsKeptData(string rel, bool isDir)
        {
            var s = rel.Split('\\');
            if (isDir || s.Length > 1) return !SkipDataDirs.Contains(s[0]);
            var name = s[0];
            return !(name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || Same(name, "logs.zip")
                     || name.StartsWith("sentry", StringComparison.OrdinalIgnoreCase));
        }
    }
}
