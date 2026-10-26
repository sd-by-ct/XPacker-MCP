using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xpk.Cli.Output
{
    internal static class TableOut
    {
        /// <summary>Left-aligned columns, two spaces apart, with a dashed line under the header.</summary>
        public static void Write(TextWriter w, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            var all = rows.ToList();
            var widths = headers.Select((h, i) => Math.Max(h.Length, all.Select(r => (r[i] ?? "").Length).DefaultIfEmpty(0).Max())).ToArray();
            w.WriteLine(Line(headers, widths));
            w.WriteLine(Line(widths.Select(n => new string('-', n)).ToList(), widths));
            foreach (var row in all) w.WriteLine(Line(row, widths));
        }

        /// <summary>"Name  value" lines with the names aligned.</summary>
        public static void WritePairs(TextWriter w, IEnumerable<KeyValuePair<string, string>> pairs, string indent = "")
        {
            var list = pairs.ToList();
            int width = list.Select(p => p.Key.Length).DefaultIfEmpty(0).Max();
            foreach (var p in list) w.WriteLine(indent + p.Key.PadRight(width) + "  " + p.Value);
        }

        private static string Line(IReadOnlyList<string> cells, int[] widths) =>
            string.Join("  ", cells.Select((c, i) => (c ?? "").PadRight(widths[i]))).TrimEnd();
    }
}
