using System;
using System.Linq;
using System.Windows.Forms;

namespace Flotilla
{
    static class Program
    {
        public const string Title = "R.A.T.S. Flotilla";

        /// <summary>
        /// No arguments: the window. "--play NAME": start that install without a window (desktop shortcuts, the Play button);
        /// the process stays until the game has closed and then puts the data folder back.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var fleet = Settings.LoadFleet();
                if (args.Length >= 2 && args[0] == "--play")
                {
                    var name = string.Join(" ", args.Skip(1));
                    if (fleet.SteamGame == null) throw new FlotillaException("Sea Power was not found in your Steam libraries. Open Flotilla once to locate it.");
                    var install = fleet.Find(name) ?? throw new FlotillaException($"There is no Sea Power copy called \"{name}\" in {fleet.FleetDir}.");
                    fleet.Play(install);
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
