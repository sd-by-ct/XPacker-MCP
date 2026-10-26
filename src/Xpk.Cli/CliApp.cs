using System;
using System.IO;
using System.Linq;
using Xpk.Cli.Commands;
using Xpk.Cli.Output;
using Xpk.Core;

namespace Xpk.Cli
{
    /// <summary>The read commands: list, get, info. Exit codes: 0 ok, 1 error (with code), 2 wrong usage.</summary>
    internal static class CliApp
    {
        public const string Usage =
            "Gebruik:\n" +
            "  xpk list <objects|sobjects|materials|textures> --xpack <naam|map> [--select <patroon>] [--fields a,b]\n" +
            "           [--offset n] [--limit n] [--json]\n" +
            "  xpk list xpacks [--json]\n" +
            "  xpk get  <object|sobject|material|texture> <pad> --xpack <naam|map> [--json]\n" +
            "  xpk info --xpack <naam|map> [--json]\n" +
            "  xpk serve                    MCP-server (stdio)\n" +
            "\n" +
            "Selector: Bomen (map met alles eronder), Bomen\\* (één niveau), Kerbs\\** (alle niveaus), re:<regex>.\n" +
            "Getallen altijd met een punt: 0.6.";

        public static int Run(string command, string[] rest, TextWriter output, TextWriter error)
        {
            bool json = rest.Contains("--json");
            try
            {
                var c = new CliContext(Args.Parse(rest), output, error);
                switch (command)
                {
                    case "list": return ListCommand.Run(c);
                    case "get": return GetCommand.Run(c);
                    case "info": return InfoCommand.Run(c);
                    case null:
                    case "help":
                    case "--help":
                    case "-h":
                        output.WriteLine(Usage);
                        return command == null ? 2 : 0;
                    default:
                        throw new UsageException("Onbekend commando: " + command);
                }
            }
            catch (UsageException ex)
            {
                if (json) JsonOut.Error(output, ErrorCodes.InvalidArguments, ex.Message);
                error.WriteLine("Fout: " + ex.Message);
                error.WriteLine();
                error.WriteLine(Usage);
                return 2;
            }
            catch (XpkException ex)
            {
                if (json) JsonOut.Error(output, ex.Code, ex.Message);
                else error.WriteLine("Fout (" + ex.Code + "): " + ex.Message);
                return 1;
            }
        }
    }
}
