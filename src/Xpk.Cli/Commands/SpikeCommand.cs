using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Xpk.Core.Env;
using Xpk.Core.Spike;

namespace Xpk.Cli.Commands
{
    /// <summary>
    /// Temporary phase 0 command: xpk spike &lt;xpack-folder&gt; [--object &lt;name&gt;] [--lodout-plus-one] [--apply] [--force]
    /// Without --apply nothing is written.
    /// </summary>
    internal static class SpikeCommand
    {
        public static int Run(string[] args)
        {
            string folder = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            if (folder == null)
            {
                Console.Error.WriteLine("Gebruik: xpk spike <xpack-map> [--object <naam>] [--lodout-plus-one] [--apply] [--force]");
                return 2;
            }
            string objectName = ValueOf(args, "--object");
            bool bump = args.Contains("--lodout-plus-one");
            bool apply = args.Contains("--apply");
            bool force = args.Contains("--force");

            var env = RtbEnvironment.Initialize(XpkConfig.Default());
            Console.WriteLine("RaceBits: " + env.RaceBitsPath);
            Console.WriteLine("SHA-256:  " + env.RaceBitsSha256);
            Console.WriteLine("XPack-mappen: " + string.Join("; ", env.XPacksDirs));

            var spike = SpikeRunner.Open(env, folder);
            Console.WriteLine(spike.Summary);
            foreach (string path in spike.ObjectPaths())
                Console.WriteLine("  " + path + "  LODOut=" + spike.GetLodOut(path).ToString(CultureInfo.InvariantCulture));

            if (objectName == null) return 0;

            float delta = bump ? 1f : 0f;
            float old = spike.GetLodOut(objectName);
            string xml = spike.ObjectXmlPath(objectName);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Plan: {0}  LODOut {1} -> {2}  (bestand {3})", objectName, old, old + delta, xml));
            if (!apply)
            {
                Console.WriteLine("Proefdraai: er is niets geschreven. Gebruik --apply om op te slaan.");
                return 0;
            }

            if (!force && Process.GetProcessesByName("XPacker").Length > 0)
            {
                Console.Error.WriteLine("XPACKER_RUNNING: sluit XPacker eerst, of gebruik --force.");
                return 3;
            }

            string backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xpk", "backups",
                spike.Summary.Name, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            string relative = xml.Substring(spike.Summary.Folder.Length).TrimStart('\\');
            string backupFile = Path.Combine(backupDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
            File.Copy(xml, backupFile);
            Console.WriteLine("Backup: " + backupFile);

            var result = spike.SaveObject(objectName, delta);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Opgeslagen: {0}  LODOut {1} -> {2}", result.ObjectPath, result.OldLodOut, result.NewLodOut));
            return 0;
        }

        private static string ValueOf(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
