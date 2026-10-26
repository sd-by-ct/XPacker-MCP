using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Xpk.Cli.Commands
{
    /// <summary>Wrong command line: exit code 2 with the usage text.</summary>
    internal sealed class UsageException : Exception
    {
        public UsageException(string message) : base(message) { }
    }

    /// <summary>Positional arguments plus --name value options and --flags.</summary>
    internal sealed class Args
    {
        private static readonly string[] ValueOptions = { "xpack", "select", "fields", "offset", "limit" };
        private static readonly string[] FlagOptions = { "json" };

        private readonly Dictionary<string, string> _options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public List<string> Positional { get; } = new List<string>();

        public static Args Parse(IEnumerable<string> args)
        {
            var result = new Args();
            var queue = new Queue<string>(args);
            while (queue.Count > 0)
            {
                string a = queue.Dequeue();
                if (!a.StartsWith("--", StringComparison.Ordinal))
                {
                    result.Positional.Add(a);
                    continue;
                }
                string name = a.Substring(2);
                if (FlagOptions.Contains(name, StringComparer.OrdinalIgnoreCase))
                    result._flags.Add(name);
                else if (ValueOptions.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    if (queue.Count == 0) throw new UsageException("Optie --" + name + " heeft een waarde nodig.");
                    result._options[name] = queue.Dequeue();
                }
                else
                    throw new UsageException("Onbekende optie: " + a);
            }
            return result;
        }

        public bool Has(string flag) => _flags.Contains(flag);

        public string Get(string option) => _options.TryGetValue(option, out var v) ? v : null;

        public string Require(string option) =>
            Get(option) ?? throw new UsageException("Optie --" + option + " ontbreekt.");

        public int GetInt(string option, int fallback)
        {
            string v = Get(option);
            if (v == null) return fallback;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
            throw new UsageException("Optie --" + option + " verwacht een geheel getal, niet '" + v + "'.");
        }

        public string[] GetList(string option) =>
            Get(option)?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
    }
}
