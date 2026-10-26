using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xpk.Core.Env;

namespace Xpk.Tests.Integration
{
    /// <summary>
    /// The test XPack. The source (tests/Fixtures/source/Default-testpack) is local only, see tests/Fixtures/README.md.
    /// Tests never touch the source: each one works on its own copy under tests/Fixtures/work/&lt;test&gt;.
    /// </summary>
    internal static class Fixture
    {
        public const string Name = "Default-testpack";

        public static string FixturesDir =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\Fixtures"));

        public static string SourceDir => Path.Combine(FixturesDir, "source", Name);

        public static RtbEnvironment Env() => RtbEnvironment.Initialize(XpkConfig.Default());

        /// <summary>Copies the fixture to tests/Fixtures/work/&lt;testName&gt;[\&lt;parent&gt;]\&lt;packName&gt; and returns that folder.</summary>
        public static string FreshCopy(string testName, string parent = null, string packName = Name)
        {
            string source = SourceDir;
            if (!Directory.Exists(source))
                throw new InvalidOperationException("Fixture ontbreekt: " + source + ". Zie tests/Fixtures/README.md.");

            string root = Path.Combine(FixturesDir, "work", testName);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string target = Path.Combine(parent == null ? root : Path.Combine(root, parent), packName);
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(target + dir.Substring(source.Length));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, target + file.Substring(source.Length));
            return target;
        }

        public static string[] Hashes(string dir) =>
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f =>
                {
                    using (var md5 = MD5.Create())
                    using (var s = File.OpenRead(f))
                        return f.Substring(dir.Length) + "|" + BitConverter.ToString(md5.ComputeHash(s));
                })
                .ToArray();
    }
}
