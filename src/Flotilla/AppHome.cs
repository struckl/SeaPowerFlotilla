using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Flotilla
{
    /// <summary>
    /// Steam can replace or remove files in a Workshop folder at any time, so the app does not run from there: started from
    /// the Workshop it places itself in %LOCALAPPDATA%\R.A.T.S. Flotilla\App and continues from that copy, which desktop shortcuts
    /// point to. That copy in turn hands over to a newer Workshop version when one has arrived.
    /// </summary>
    public static class AppHome
    {
        public const string ExeName = "SeaPowerFlotilla.exe";
        public static string Dir => Path.Combine(Settings.Dir, "App");
        public static string Exe => Path.Combine(Dir, ExeName);

        public static bool InWorkshop(string path) =>
            path.IndexOf($@"\workshop\content\{Game.AppId}\", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsNewer(string candidate, string current)
        {
            try { return Version.Parse(candidate) > Version.Parse(current); }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException) { return false; }
        }

        static string VersionOf(string exe) => FileVersionInfo.GetVersionInfo(exe).FileVersion;

        /// <summary>Copies the exe and its config into home, waiting a moment for an older copy that is just closing.</summary>
        public static void Place(string sourceExe, string home)
        {
            Directory.CreateDirectory(home);
            foreach (var source in new[] { sourceExe, sourceExe + ".config" })
            {
                if (!File.Exists(source)) continue;
                for (int attempt = 1; ; attempt++)
                {
                    try { File.Copy(source, Path.Combine(home, Path.GetFileName(source)), true); break; }
                    catch (IOException) when (attempt < 20) { Thread.Sleep(250); }
                }
            }
        }

        /// <summary>True if another process has taken over and this one should end.</summary>
        public static bool HandOver(Fleet fleet)
        {
            var self = Application.ExecutablePath;
            try
            {
                if (InWorkshop(self)) return FromWorkshop(self);
                if (Layout.Same(Path.GetFullPath(self), Path.GetFullPath(Exe)) && fleet.SteamGame != null) return ToNewerWorkshop(self, fleet);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception)
            {
                Settings.Log("hand-over failed, running from here: " + e.Message);  // still a working app
            }
            return false;
        }

        static bool FromWorkshop(string self)
        {
            bool first = !File.Exists(Exe);
            if (first || IsNewer(VersionOf(self), VersionOf(Exe))) Place(self, Dir);
            if (first && MessageBox.Show("Put a \"R.A.T.S. Flotilla\" shortcut on your desktop?", Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Shortcut.MakeAppShortcut(Exe);
            Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = false })?.Dispose();
            return true;
        }

        static bool ToNewerWorkshop(string self, Fleet fleet)
        {
            var workshop = ModList.WorkshopDir(fleet);
            if (!Directory.Exists(workshop)) return false;
            var own = VersionOf(self);
            var newer = Directory.GetDirectories(workshop).Select(d => Path.Combine(d, ExeName))
                .FirstOrDefault(f => File.Exists(f) && IsNewer(VersionOf(f), own));
            if (newer == null) return false;
            Process.Start(new ProcessStartInfo(newer) { UseShellExecute = false })?.Dispose();
            return true;
        }
    }
}
