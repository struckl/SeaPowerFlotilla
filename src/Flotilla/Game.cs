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
            var line = ChangelogLine(dir);
            return line == null ? "?" : $"{line.Groups[1].Value} (build {line.Groups[2].Value})";
        }

        /// <summary>"0.9.0" from the same changelog line: the version the game's mod rule compares against. Null when unknown.</summary>
        public static string VersionOf(string dir) => ChangelogLine(dir)?.Groups[1].Value;

        static Match ChangelogLine(string dir)
        {
            var changelog = Path.Combine(Streaming(dir), "changelog.txt");
            if (!File.Exists(changelog)) return null;
            return File.ReadLines(changelog).Select(l => Regex.Match(l, @"^\S+:\s+(\S+)\s+Build\s+(\d+)")).FirstOrDefault(m => m.Success);
        }

        /// <summary>Where Steam keeps the Workshop downloads of Sea Power, next to the library the game is in.</summary>
        public static string WorkshopDir(string steamGame) =>
            Path.GetFullPath(Path.Combine(steamGame, "..", "..", "workshop", "content", AppId.ToString()));

        /// <summary>Lets Steam accept a start of the exe from outside the library instead of restarting the Steam copy.</summary>
        public static void WriteAppIdFile(string dir) => File.WriteAllText(Path.Combine(dir, Layout.AppIdFile), AppId.ToString());

        public static bool HasLoader(string dir) =>
            File.Exists(Path.Combine(dir, "winhttp.dll")) || Directory.Exists(Path.Combine(dir, "BepInEx"));

        /// <summary>
        /// The count in the main list: mod folders in StreamingAssets, Anchor Chain's ACConfigs folder included, as the game and the
        /// Mods window list it.
        /// </summary>
        public static List<string> LocalMods(string dir)
        {
            var streaming = Streaming(dir);
            if (!Directory.Exists(streaming)) return new List<string>();
            return Directory.GetDirectories(streaming).Select(Path.GetFileName)
                .Where(n => !Layout.NotMods.Contains(n))
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

        /// <summary>Two paths name the same folder (full paths, no trailing '\').</summary>
        public static bool SameFolder(string a, string b) => Same(Full(a), Full(b));

        /// <summary>Folder <paramref name="inner"/> is <paramref name="outer"/> or lies somewhere inside it.</summary>
        public static bool IsInside(string inner, string outer) => (Full(inner) + "\\").StartsWith(Full(outer) + "\\", StringComparison.OrdinalIgnoreCase);

        static string Full(string path) => Path.GetFullPath(path).TrimEnd('\\');

        public static readonly HashSet<string> LoaderDirs = new HashSet<string>(C) { "BepInEx" };
        public static readonly HashSet<string> LoaderFiles = new HashSet<string>(C) { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };
        public static readonly HashSet<string> GameStreaming = new HashSet<string>(C) { "EntityScenes", "original" };
        /// <summary>The StreamingAssets folders the game's own mod list leaves out (FileManager.ignoreDirectories).</summary>
        public static readonly HashSet<string> NotMods = new HashSet<string>(GameStreaming.Concat(new[] { "user" }), C);

        public const string Marker = "flotilla.ini";
        /// <summary>Lies in a copy while it is being made and holds the id of the Flotilla process making it.</summary>
        public const string UnfinishedMarker = "flotilla.unfinished";
        public const string DataFolder = "UserData";
        public const string AppIdFile = "steam_appid.txt";
        public static readonly HashSet<string> OwnDirs = new HashSet<string>(C) { DataFolder };
        public static readonly HashSet<string> OwnFiles = new HashSet<string>(C) { AppIdFile, Marker, UnfinishedMarker };
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
