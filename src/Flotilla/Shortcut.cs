using System;
using System.IO;
using System.Windows.Forms;

namespace Flotilla
{
    public static class Shortcut
    {
        /// <summary>"Sea Power (NAME).lnk" on the desktop: starts the copy through Flotilla, with the game's icon.</summary>
        public static string MakeOnDesktop(Install install)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"Sea Power ({install.Name}).lnk");
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
