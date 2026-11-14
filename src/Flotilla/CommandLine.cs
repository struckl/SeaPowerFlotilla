using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace Flotilla
{
    static class CommandLine
    {
        const string Usage = @"Usage: SeaPowerFlotilla.exe --cli <command>

  list [--json]               List installs, game builds and paths.
  create NAME [--setup]       Create a clean copy, or clone the Steam setup.
  update NAME                 Update a copy's game files from Steam.
  play NAME                   Start an install and wait for a copy to exit.
  delete NAME --yes           Permanently delete a copy.
  help                        Show this help.";

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(int processId);

        internal static int Run(string[] args)
        {
            const int attachParentProcess = -1;
            AttachConsole(attachParentProcess);
            return Run(args, Settings.LoadFleet, Console.Out, Console.Error);
        }

        internal static int Run(string[] args, Func<Fleet> loadFleet, TextWriter output, TextWriter error)
        {
            if (args.Length == 0 || (args.Length == 1 && args[0] is "help" or "--help" or "-h" or "/?"))
            {
                output.WriteLine(Usage);
                return 0;
            }

            try
            {
                var argument = args.Length > 1 ? args[1] : "";
                var option = args.Length == 3 ? args[2] : "";
                switch (args[0], args.Length, option)
                {
                    case ("list", 1, _):
                    case ("list", 2, _) when argument == "--json":
                        List(loadFleet(), args.Length == 2, output);
                        return 0;
                    case ("create", 2, _) or ("create", 3, "--setup") when HasName(argument):
                        var createFleet = LoadSteamFleet(loadFleet);
                        output.WriteLine(createFleet.Create(argument, args.Length == 2, null, CancellationToken.None).Dir);
                        return 0;
                    case ("update", 2, _) when HasName(argument):
                        var updateFleet = LoadSteamFleet(loadFleet);
                        var result = updateFleet.Update(updateFleet.GetInstall(argument), null, CancellationToken.None);
                        output.WriteLine($"{result.Copied} files copied, {result.Deleted} removed.");
                        return 0;
                    case ("play", 2, _) when HasName(argument):
                        var playFleet = LoadSteamFleet(loadFleet);
                        playFleet.Play(playFleet.GetInstall(argument));
                        return 0;
                    case ("delete", 3, "--yes") when HasName(argument):
                        var deleteFleet = loadFleet();
                        var install = deleteFleet.GetInstall(argument);
                        deleteFleet.Delete(install);
                        output.WriteLine($"Deleted \"{install.Name}\".");
                        return 0;
                    case ("delete", 2, _) when HasName(argument):
                        error.WriteLine("Pass --yes to permanently delete this copy.");
                        return 1;
                    default:
                        error.WriteLine(Usage);
                        return 1;
                }
            }
            catch (FlotillaException e)
            {
                error.WriteLine(e.Message);
                return 2;
            }
            catch (Exception e)
            {
                error.WriteLine(e.Message);
                Settings.Log("command failed: " + e);
                return 3;
            }
        }

        static bool HasName(string name) => !string.IsNullOrWhiteSpace(name) && !name.StartsWith("-", StringComparison.Ordinal);

        static Fleet LoadSteamFleet(Func<Fleet> loadFleet)
        {
            var fleet = loadFleet();
            fleet.RequireSteamGame();
            return fleet;
        }

        static void List(Fleet fleet, bool json, TextWriter output)
        {
            var installs = fleet.List();
            if (json)
            {
                var rows = installs.Select(i => new { name = i.Name, kind = i.Kind, build = i.Build, path = i.Dir });
                output.WriteLine(new JavaScriptSerializer().Serialize(rows));
                return;
            }

            foreach (var install in installs)
                output.WriteLine($"{install.Name}\t{install.Kind}\t{install.Build}\t{install.Dir}");
        }
    }
}
