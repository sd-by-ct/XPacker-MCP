using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Xpk.Core.Select
{
    /// <summary>
    /// Selects items by path, relative to the item kind (e.g. "Bomen\Eik_01"). '\' and '/' are equivalent and case
    /// does not matter.
    /// - Without wildcards the pattern is an item or a folder: it matches that item and everything below it, on
    ///   segment boundaries ("Bomen" does not match "Bomen2\X", unlike XPacker's StartsWith).
    /// - '*' matches within one segment, '**' matches zero or more segments ("Kerbs\**", "**\Tree0*").
    /// - "re:" starts a .NET regex, matched (unanchored) against the path with '\' separators.
    /// </summary>
    public sealed class Selector
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private readonly Regex _regex;

        private Selector(string pattern, Regex regex)
        {
            Pattern = pattern;
            _regex = regex;
        }

        public static Selector All { get; } = new Selector("", null);

        public string Pattern { get; }

        public static Selector Parse(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return All;
            pattern = pattern.Trim();

            if (pattern.StartsWith("re:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return new Selector(pattern, new Regex(pattern.Substring(3), Options));
                }
                catch (ArgumentException ex)
                {
                    throw new XpkException(ErrorCodes.InvalidValue, "Ongeldige regex in selector '" + pattern + "': " + ex.Message, ex);
                }
            }

            string path = Normalize(pattern);
            if (path.Length == 0) return All;
            return new Selector(pattern, new Regex(path.IndexOf('*') < 0 ? FolderRegex(path) : GlobRegex(path), Options));
        }

        public bool Matches(string itemPath) => _regex == null || _regex.IsMatch(Normalize(itemPath ?? ""));

        public static string Normalize(string path) => path.Replace('/', '\\').Trim().Trim('\\');

        private static string FolderRegex(string path) => "^" + Regex.Escape(path) + @"(\\.*)?$";

        private static string GlobRegex(string path)
        {
            var sb = new StringBuilder("^");
            string[] segments = path.Split('\\');
            bool afterSegment = false;   // the previous piece was a normal segment, so the next one needs a '\'
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                bool last = i == segments.Length - 1;
                if (seg == "**")
                {
                    if (last) sb.Append(afterSegment ? @"(\\.*)?" : ".*");
                    else sb.Append(afterSegment ? @"\\(.*\\)?" : @"(.*\\)?");
                    afterSegment = false;
                }
                else
                {
                    if (afterSegment) sb.Append(@"\\");
                    foreach (string part in seg.Split('*'))
                        sb.Append(Regex.Escape(part)).Append(@"[^\\]*");
                    sb.Length -= @"[^\\]*".Length;   // one wildcard fewer than parts
                    afterSegment = true;
                }
            }
            return sb.Append('$').ToString();
        }

        public override string ToString() => Pattern;
    }
}
