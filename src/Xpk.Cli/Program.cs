using System;
using System.Globalization;
using System.Linq;
using System.Text;
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

            try
            {
                if (command == "serve") return ServeCommand.Run();

                Console.OutputEncoding = new UTF8Encoding(false);
                return CliApp.Run(command, args.Skip(1).ToArray(), Console.Out, Console.Error);
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
