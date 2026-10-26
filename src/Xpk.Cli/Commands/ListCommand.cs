using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xpk.Cli.Output;
using Xpk.Core.Model;
using Xpk.Core.Props;
using Xpk.Core.Queries;
using Xpk.Core.Select;
using Xpk.Core.Session;

namespace Xpk.Cli.Commands
{
    /// <summary>
    /// xpk list &lt;objects|sobjects|materials|textures|xpacks&gt; --xpack &lt;naam|map&gt; [--select ..] [--fields a,b]
    /// [--offset n] [--limit n] [--json]
    /// </summary>
    internal static class ListCommand
    {
        // Text output without --fields shows these columns; --json always has all fields.
        private static readonly Dictionary<ItemKind, string[]> DefaultColumns = new Dictionary<ItemKind, string[]>
        {
            [ItemKind.Object] = new[] { "Render", "Shadow", "ShadowCast", "LODIn", "LODOut", "Purpose" },
            [ItemKind.StringObject] = new[] { "SObjectType", "Render", "Shadow", "LODIn", "LODOut" },
            [ItemKind.Material] = new[] { "ShaderType", "AC_TransparencyType", "GameSurface" },
            [ItemKind.Texture] = new string[0],
        };

        public static int Run(CliContext c)
        {
            if (c.Args.Positional.Count != 1) throw new UsageException("Geef precies één soort op, bijvoorbeeld: xpk list objects --xpack <naam>");
            string word = c.Args.Positional[0];
            if (string.Equals(word, "xpacks", StringComparison.OrdinalIgnoreCase)) return ListXPacks(c);

            var kind = CliContext.ParseKind(word);
            var session = c.OpenXPack();
            string[] fields = c.Args.GetList("fields");
            var page = Query.List(session, kind, Selector.Parse(c.Args.Get("select")), fields,
                c.Args.GetInt("offset", 0), c.Args.GetInt("limit", Query.DefaultLimit));
            var warnings = session.Summary().Warnings;

            if (c.Json)
            {
                c.Ok(page, warnings);
                return 0;
            }

            c.WriteWarnings(warnings);
            string[] columns = fields != null && fields.Length > 0
                ? page.Items.FirstOrDefault()?.Fields.Keys.ToArray() ?? fields.Select(f => PropertyMap.Find(kind, f).Name).ToArray()
                : DefaultColumns[kind];
            var headers = new List<string> { "Pad" };
            headers.AddRange(columns);
            headers.AddRange(ExtraHeaders(kind));
            TableOut.Write(c.Out, headers, page.Items.Select(i =>
            {
                var row = new List<string> { i.Path };
                row.AddRange(columns.Select(col => i.Fields != null && i.Fields.TryGetValue(col, out var v) ? PropertyMap.FormatValue(v) : ""));
                row.AddRange(ExtraCells(i));
                return (IReadOnlyList<string>)row;
            }));
            c.Out.WriteLine();
            c.Out.WriteLine(Footer(page));
            return 0;
        }

        private static IEnumerable<string> ExtraHeaders(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Material: return new[] { "Gebruikt", "Textures" };
                case ItemKind.Texture: return new[] { "Formaat", "Breedte", "Hoogte", "Bytes", "MD5", "Gebruikt" };
                default: return new string[0];
            }
        }

        private static IEnumerable<string> ExtraCells(ItemInfo i)
        {
            switch (i.Kind)
            {
                case ItemKind.Material:
                    return new[] { Num(i.UsageCount), Num(i.TexMaps?.Count) };
                case ItemKind.Texture:
                    return new[] { i.Format, Num(i.Width), Num(i.Height), Num(i.Bytes), i.Md5, Num(i.UsedBy?.Count) };
                default:
                    return new string[0];
            }
        }

        private static string Footer(ItemPage page)
        {
            string text = page.Items.Count.ToString(CultureInfo.InvariantCulture) + " van " + page.Total.ToString(CultureInfo.InvariantCulture);
            if (page.NextOffset.HasValue)
                text += "; volgende pagina: --offset " + page.NextOffset.Value.ToString(CultureInfo.InvariantCulture);
            return text;
        }

        private static int ListXPacks(CliContext c)
        {
            var entries = XPackCatalog.List(c.Env.XPacksDirs);
            if (c.Json)
            {
                c.Ok(entries.Select(e => new
                {
                    name = e.Name,
                    folder = e.Folder,
                    last_modified = e.LastModifiedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                }).ToList());
                return 0;
            }
            TableOut.Write(c.Out, new[] { "Naam", "Laatst gewijzigd", "Map" }, entries.Select(e => (IReadOnlyList<string>)new[]
            {
                e.Name,
                e.LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                e.Folder,
            }));
            c.Out.WriteLine();
            c.Out.WriteLine(entries.Count.ToString(CultureInfo.InvariantCulture) + " XPacks in " + string.Join("; ", c.Env.XPacksDirs));
            return 0;
        }

        internal static string Num(long? n) => n.HasValue ? n.Value.ToString(CultureInfo.InvariantCulture) : "";
    }
}
