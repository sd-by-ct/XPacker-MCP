using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Xpk.Cli.Commands;

namespace Xpk.Cli
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string command = args.FirstOrDefault();
            if (command == "serve")
            {
                // First statement in serve mode: stdout belongs to the MCP protocol only.
                Console.SetOut(Console.Error);
            }

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            string[] rest = args.Skip(1).ToArray();
            try
            {
                switch (command)
                {
                    case "serve":
                        return ServeCommand.Run();
                    case "spike":
                        return SpikeCommand.Run(rest);
                    default:
                        Console.Error.WriteLine("Gebruik: xpk serve | xpk spike <xpack-map> [--object <naam>] [--lodout-plus-one] [--apply]");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fout: " + ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
