using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xpk.Core.Env;
using Xpk.Core.Spike;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class SpikeTests
    {
        // Phase 0 has no fixture yet (phase 1); the spike tests copy the phase-0 work copy and never touch it.
        private const string SourceXPack = @"C:\Users\Moor\xpk\NewXPackTest";
        private const string ObjectName = "Billboards_L_1";

        private static string FreshCopy(string testName)
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\Fixtures\work", testName));
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string target = Path.Combine(root, "NewXPackTest");
            foreach (string dir in Directory.GetDirectories(SourceXPack, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(target + dir.Substring(SourceXPack.Length));
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(SourceXPack, "*", SearchOption.AllDirectories))
                File.Copy(file, target + file.Substring(SourceXPack.Length));
            return target;
        }

        private static string[] Hashes(string dir) =>
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f =>
                {
                    using (var md5 = MD5.Create())
                    using (var s = File.OpenRead(f))
                        return f.Substring(dir.Length) + "|" + BitConverter.ToString(md5.ComputeHash(s));
                })
                .ToArray();

        [Fact]
        public void Open_WorkCopy_CountsMatch()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());
            string folder = FreshCopy(nameof(Open_WorkCopy_CountsMatch));

            var spike = SpikeRunner.Open(env, folder);

            Assert.Equal("NewXPackTest", spike.Summary.Name);
            Assert.Equal(4, spike.Summary.Objects);
            Assert.Equal(1, spike.Summary.SObjects);
            Assert.Equal(11, spike.Summary.Materials);
            Assert.Equal(12, spike.Summary.Textures);
            Assert.Equal(1, spike.Summary.CrossSections);
        }

        // RaceBits v2.0 normalizes on the first save (it omits defaults such as SeparationMinDistance=0),
        // so a no-change save is checked semantically and for idempotence, not byte for byte against the source.

        [Fact]
        public void SaveObject_NoChange_OnlyObjectFileTouched_ValuesUnchanged()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());
            string folder = FreshCopy(nameof(SaveObject_NoChange_OnlyObjectFileTouched_ValuesUnchanged));
            var before = Hashes(folder);
            var valuesBefore = SpikeRunner.Open(env, folder).ObjectValues(ObjectName);

            SpikeRunner.Open(env, folder).SaveObject(ObjectName, lodOutDelta: 0);

            var changedFiles = before.Except(Hashes(folder)).Select(h => h.Split('|')[0]).ToArray();
            Assert.All(changedFiles, f => Assert.Equal(@"\Objects\" + ObjectName + ".xml", f));
            Assert.Equal(valuesBefore, SpikeRunner.Open(env, folder).ObjectValues(ObjectName));
        }

        [Fact]
        public void SaveObject_NoChange_SecondSaveByteIdentical()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());
            string folder = FreshCopy(nameof(SaveObject_NoChange_SecondSaveByteIdentical));
            SpikeRunner.Open(env, folder).SaveObject(ObjectName, lodOutDelta: 0);
            var afterFirst = Hashes(folder);

            SpikeRunner.Open(env, folder).SaveObject(ObjectName, lodOutDelta: 0);

            Assert.Equal(afterFirst, Hashes(folder));
        }

        [Fact]
        public void SaveObject_LodOutPlusOne_OnlyThatLineChanges()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());
            string folder = FreshCopy(nameof(SaveObject_LodOutPlusOne_OnlyThatLineChanges));
            string xml = Path.Combine(folder, "Objects", ObjectName + ".xml");
            SpikeRunner.Open(env, folder).SaveObject(ObjectName, lodOutDelta: 0);   // normalize first
            var before = Hashes(folder);
            var linesBefore = File.ReadAllLines(xml);

            var spike = SpikeRunner.Open(env, folder);
            var result = spike.SaveObject(ObjectName, lodOutDelta: 1);

            Assert.Equal(result.OldLodOut + 1, result.NewLodOut);
            var changedFiles = before.Except(Hashes(folder)).Select(h => h.Split('|')[0]).ToArray();
            Assert.Equal(new[] { @"\Objects\" + ObjectName + ".xml" }, changedFiles);
            var linesAfter = File.ReadAllLines(xml);
            Assert.Equal(linesBefore.Length, linesAfter.Length);
            var diff = linesBefore.Zip(linesAfter, (a, b) => new { a, b }).Where(p => p.a != p.b).ToArray();
            Assert.Single(diff);
            Assert.Equal("  <LODOut>" + result.NewLodOut.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</LODOut>", diff[0].b);

            var reopened = SpikeRunner.Open(env, folder);
            Assert.Equal(result.NewLodOut, reopened.GetLodOut(ObjectName));
        }
    }
}
