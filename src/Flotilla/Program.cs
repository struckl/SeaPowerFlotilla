using System;
using System.Linq;
using System.Windows.Forms;

namespace Flotilla
{
    static class Program
    {
        public const string Title = "R.A.T.S. Flotilla";

        /// <summary>
        /// No arguments: the window. "--play NAME": start an install from a shortcut. "--cli": run a terminal command.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] != "--play" || args.Length < 2))
                return CommandLine.Run(args[0] == "--cli" ? args.Skip(1).ToArray() : args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var fleet = Settings.LoadFleet();
                if (args.Length >= 2 && args[0] == "--play")
                {
                    var name = string.Join(" ", args.Skip(1));
                    fleet.RequireSteamGame();
                    fleet.Play(fleet.GetInstall(name));
                    return 0;
                }
                if (AppHome.HandOver(fleet)) return 0;
                Application.Run(new MainForm(fleet));
                return 0;
            }
            catch (FlotillaException e)
            {
                Settings.Log("stopped: " + e.Message);
                MessageBox.Show(e.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            catch (Exception e)
            {
                Settings.Log("error: " + e);
                MessageBox.Show($"Something went wrong:\n\n{e.Message}\n\nDetails are in {Settings.Dir}\\flotilla.log.", Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 3;
            }
        }
    }
}
