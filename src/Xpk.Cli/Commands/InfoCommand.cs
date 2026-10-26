using System.Collections.Generic;
using System.Globalization;
using Xpk.Cli.Output;

namespace Xpk.Cli.Commands
{
    /// <summary>xpk info --xpack &lt;naam|map&gt; [--json]: summary, RaceBits hash and read-only status.</summary>
    internal static class InfoCommand
    {
        public static int Run(CliContext c)
        {
            if (c.Args.Positional.Count != 0) throw new UsageException("xpk info verwacht geen extra argumenten, alleen --xpack.");
            var s = c.OpenXPack().Summary();
            if (c.Json)
            {
                c.Ok(s, s.Warnings);
                return 0;
            }

            string N(int n) => n.ToString(CultureInfo.InvariantCulture);
            TableOut.WritePairs(c.Out, new[]
            {
                new KeyValuePair<string, string>("XPack", s.Name),
                new KeyValuePair<string, string>("Map", s.Folder),
                new KeyValuePair<string, string>("Auteur", s.Author),
                new KeyValuePair<string, string>("Datum", s.Date),
                new KeyValuePair<string, string>("Beschrijving", s.Description),
                new KeyValuePair<string, string>("Objecten", N(s.Objects)),
                new KeyValuePair<string, string>("String Objects", N(s.SObjects)),
                new KeyValuePair<string, string>("Materialen", N(s.Materials)),
                new KeyValuePair<string, string>("Textures", N(s.Textures)),
                new KeyValuePair<string, string>("Cross Sections", N(s.CrossSections)),
                new KeyValuePair<string, string>("RaceBits SHA-256", s.RaceBitsSha256),
                new KeyValuePair<string, string>("Schrijven", s.ReadOnly ? "nee, alleen-lezen: " + s.ReadOnlyReason : "ja"),
            });
            if (s.Warnings.Count > 0)
            {
                c.Out.WriteLine();
                c.Out.WriteLine("Waarschuwingen (" + N(s.Warnings.Count) + "):");
                foreach (string w in s.Warnings) c.Out.WriteLine("  " + w);
            }
            return 0;
        }
    }
}
