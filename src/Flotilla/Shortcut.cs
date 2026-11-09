using System;
using System.IO;
using System.Windows.Forms;

namespace Flotilla
{
    public static class Shortcut
    {
        static string DesktopPath(string name) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"Sea Power ({name}).lnk");

        /// <summary>"R.A.T.S. Flotilla.lnk" on the desktop: starts the app itself.</summary>
        public static void MakeAppShortcut(string exe)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            try
            {
                dynamic link = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Program.Title + ".lnk"));
                link.TargetPath = exe;
                link.WorkingDirectory = Path.GetDirectoryName(exe);
                link.IconLocation = exe + ",0";
                link.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        /// <summary>After a rename: a shortcut the copy already had follows the new name.</summary>
        public static void Renamed(string oldName, Install install)
        {
            var old = DesktopPath(oldName);
            if (!File.Exists(old)) return;
            File.Delete(old);
            MakeOnDesktop(install);
        }

        /// <summary>"Sea Power (NAME).lnk" on the desktop: starts the copy through Flotilla, with the game's icon.</summary>
        public static string MakeOnDesktop(Install install)
        {
            var path = DesktopPath(install.Name);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            try
            {
                dynamic link = shell.CreateShortcut(path);
                link.TargetPath = Application.ExecutablePath;
                link.Arguments = $"--play \"{install.Name}\"";
                link.WorkingDirectory = install.Dir;
                link.IconLocation = Path.Combine(install.Dir, Game.Exe) + ",0";
                link.Description = $"Sea Power, the copy \"{install.Name}\" (R.A.T.S. Flotilla)";
                link.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            return path;
        }
    }
}
