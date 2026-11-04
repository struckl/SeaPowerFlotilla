using System;
using System.Diagnostics;
using System.IO;

namespace Flotilla
{
    /// <summary>
    /// Sea Power keeps settings, the enabled mods and their load order, saves and logs in
    /// %USERPROFILE%\AppData\LocalLow\Triassic Games\Sea Power (Unity's persistentDataPath), the same folder for every copy.
    /// While a copy runs, that folder is parked as "Sea Power.steam" and a junction to the copy's UserData takes its place;
    /// afterwards the junction goes and the folder comes back. Between runs nothing is different from a plain Steam install.
    /// </summary>
    public sealed class DataSwap
    {
        public readonly string Root;
        public string Live => Path.Combine(Root, "Sea Power");
        public string Parked => Path.Combine(Root, "Sea Power.steam");
        /// <summary>Holds the folder of the copy the junction points to (reading a junction's target needs native calls).</summary>
        public string ActiveFile => Path.Combine(Root, "Sea Power.flotilla");

        public DataSwap(string root = null)
        {
            Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Triassic Games");
        }

        public static bool IsLink(string path) =>
            Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        public bool Switched => IsLink(Live);
        public bool NeedsRepair => Switched || Directory.Exists(Parked) || File.Exists(ActiveFile);
        public bool Conflict => !Switched && Directory.Exists(Live) && Directory.Exists(Parked);

        public string ActiveCopy => File.Exists(ActiveFile) ? File.ReadAllText(ActiveFile).Trim() : null;

        /// <summary>Back to normal: the Steam install's own folder under its own name. Call only while no Sea Power runs.</summary>
        public void Repair()
        {
            if (Conflict)
                throw new FlotillaException(
                    $"There are two Sea Power data folders:\n{Live}\n{Parked}\n\n" +
                    "The second one holds your Steam install's settings and saves from before a copy was started. " +
                    "Look into both, keep the one you want and name it \"Sea Power\".");
            if (Switched) Directory.Delete(Live, false);  // removes the junction, never what it points to
            if (Directory.Exists(Parked)) Directory.Move(Parked, Live);
            if (File.Exists(ActiveFile)) File.Delete(ActiveFile);
        }

        public void SwitchTo(string data, string copyDir)
        {
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(Root);
            if (Directory.Exists(Live)) Directory.Move(Live, Parked);
            else Directory.CreateDirectory(Parked);
            try
            {
                MakeJunction(Live, data);
                File.WriteAllText(ActiveFile, copyDir);
            }
            catch
            {
                if (Switched) Directory.Delete(Live, false);
                if (!Directory.Exists(Live)) Directory.Move(Parked, Live);
                throw;
            }
        }

        static void MakeJunction(string link, string target)
        {
            var info = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using (var p = Process.Start(info))
            {
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 || !IsLink(link))
                    throw new FlotillaException($"Could not link the game's data folder to the copy:\n{output.Trim()}");
            }
        }
    }
}
