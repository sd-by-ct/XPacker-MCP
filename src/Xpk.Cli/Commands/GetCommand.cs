using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xpk.Cli.Output;
using Xpk.Core.Model;
using Xpk.Core.Props;
using Xpk.Core.Queries;

namespace Xpk.Cli.Commands
{
    /// <summary>xpk get &lt;object|sobject|material|texture&gt; &lt;pad&gt; --xpack &lt;naam|map&gt; [--json]</summary>
    internal static class GetCommand
    {
        public static int Run(CliContext c)
        {
            if (c.Args.Positional.Count != 2)
                throw new UsageException("Geef een soort en een pad op, bijvoorbeeld: xpk get material Kerbs\\Kerb_RW --xpack <naam>");
            var kind = CliContext.ParseKind(c.Args.Positional[0]);
            var session = c.OpenXPack();
            var info = Query.Get(session, kind, c.Args.Positional[1]);
            var warnings = session.Summary().Warnings;

            if (c.Json)
            {
                c.Ok(info, warnings);
                return 0;
            }

            c.WriteWarnings(warnings);
            c.Out.WriteLine(Query.KindName(kind) + ": " + info.Path);
            c.Out.WriteLine();
            if (info.Fields != null && info.Fields.Count > 0)
            {
                var acIrrelevant = new HashSet<string>(PropertyMap.For(kind).Where(d => !d.AcRelevant).Select(d => d.Name));
                TableOut.WritePairs(c.Out, info.Fields.Select(f => new KeyValuePair<string, string>(
                    f.Key, PropertyMap.FormatValue(f.Value) + (acIrrelevant.Contains(f.Key) ? "   (niet voor AC)" : ""))));
                c.Out.WriteLine();
            }
            WriteExtras(c, info);
            return 0;
        }

        private static void WriteExtras(CliContext c, ItemInfo i)
        {
            switch (i.Kind)
            {
                case ItemKind.Object:
                    List(c, "Materialen", i.Materials);
                    if (i.Children.Count > 0)
                    {
                        c.Out.WriteLine("Child objects (" + i.Children.Count.ToString(CultureInfo.InvariantCulture) + "):");
                        foreach (var ch in i.Children)
                            c.Out.WriteLine("  " + ch.Object + "  schaal " + Vec(ch.Scale) + "  rotatie " + Vec(ch.RotationDegrees) +
                                            "  positie " + Vec(ch.Translation));
                    }
                    c.Out.WriteLine("Is child object: " + (i.IsChildObject == true ? "ja" : "nee"));
                    break;
                case ItemKind.StringObject:
                    List(c, "Objecten", i.Objects);
                    break;
                case ItemKind.Material:
                    c.Out.WriteLine("Textures (" + i.TexMaps.Count.ToString(CultureInfo.InvariantCulture) + "):");
                    foreach (var t in i.TexMaps)
                        c.Out.WriteLine("  " + t.Slot + "  " + t.Texture + (t.Exists ? "" : "   (ONTBREEKT)"));
                    List(c, "Purpose", i.Purposes);
                    List(c, "Ontbrekende verplichte textures", i.MissingRequiredTextures);
                    List(c, "Gebruikt door", i.UsedBy);
                    break;
                case ItemKind.Texture:
                    TableOut.WritePairs(c.Out, new[]
                    {
                        new KeyValuePair<string, string>("Formaat", i.Format ?? "(bestand ontbreekt)"),
                        new KeyValuePair<string, string>("Breedte", ListCommand.Num(i.Width)),
                        new KeyValuePair<string, string>("Hoogte", ListCommand.Num(i.Height)),
                        new KeyValuePair<string, string>("Bytes", ListCommand.Num(i.Bytes)),
                        new KeyValuePair<string, string>("MD5", i.Md5 ?? ""),
                    });
                    List(c, "Gebruikt door", i.UsedBy);
                    break;
            }
        }

        private static void List(CliContext c, string title, IReadOnlyList<string> items)
        {
            c.Out.WriteLine(title + " (" + items.Count.ToString(CultureInfo.InvariantCulture) + "):");
            foreach (string s in items) c.Out.WriteLine("  " + s);
        }

        private static string Vec(float[] v) => string.Join(" ", v.Select(x => PropertyMap.FormatValue(x)));
    }
}
