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
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            string command = args.FirstOrDefault();
            string[] rest = args.Skip(1).ToArray();
            try
            {
                switch (command)
                {
                    case "spike":
                        return SpikeCommand.Run(rest);
                    default:
                        Console.Error.WriteLine("Gebruik: xpk spike <xpack-map> [--object <naam>] [--lodout-plus-one] [--apply]");
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
