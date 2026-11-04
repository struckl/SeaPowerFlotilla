using System;
using System.Collections.Generic;
using System.IO;

namespace Flotilla
{
    /// <summary>settings.ini and flotilla.log in %LOCALAPPDATA%\R.A.T.S. Flotilla.</summary>
    public static class Settings
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "R.A.T.S. Flotilla");
        static string File_ => Path.Combine(Dir, "settings.ini");
        static string LogFile => Path.Combine(Dir, "flotilla.log");

        public static Dictionary<string, string> Read()
        {
            var values = new Dictionary<string, string>();
            if (File.Exists(File_))
                foreach (var line in File.ReadAllLines(File_))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2) values[parts[0].Trim()] = parts[1].Trim();
                }
            return values;
        }

        public static void Write(string key, string value)
        {
            var values = Read();
            values[key] = value;
            Directory.CreateDirectory(Dir);
            var lines = new List<string>();
            foreach (var pair in values) lines.Add($"{pair.Key}={pair.Value}");
            File.WriteAllLines(File_, lines);
        }

        public static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 1_000_000) File.Delete(LogFile);
                File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}\r\n");
            }
            catch (IOException) { }
        }

        /// <summary>The fleet with the saved folders, or the found Steam install and the default fleet folder next to its library.</summary>
        public static Fleet LoadFleet()
        {
            var values = Read();
            values.TryGetValue("SteamGame", out var game);
            if (string.IsNullOrEmpty(game) || !File.Exists(Path.Combine(game, Game.Exe))) game = Steam.FindGame();
            values.TryGetValue("FleetDir", out var fleetDir);
            return new Fleet
            {
                SteamGame = game,
                FleetDir = string.IsNullOrEmpty(fleetDir) ? Fleet.DefaultFleetDir(game) : fleetDir,
                Log = Log,
            };
        }
    }
}
