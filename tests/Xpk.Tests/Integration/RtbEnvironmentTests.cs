using System;
using System.IO;
using System.Linq;
using Xpk.Core.Env;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class RtbEnvironmentTests
    {
        // Installed RaceBits.dll: Steam branch v1950, RTB v1.9.5.6 (see docs/PLAN.md, "Afwijkingen in fase 0").
        private const string InstalledRaceBitsSha256Prefix = "944c3fe8";

        [Fact]
        public void Initialize_LoadsRaceBits_ReportsInstalledHash()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());

            Assert.StartsWith(InstalledRaceBitsSha256Prefix, env.RaceBitsSha256);
            Assert.Equal(64, env.RaceBitsSha256.Length);
            Assert.False(string.IsNullOrEmpty(env.RaceBitsVersion));
            Assert.Equal(
                Path.Combine(XpkConfig.Default().RtbDir, "RaceBits.dll"),
                env.RaceBitsPath,
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Initialize_ReportsXPacksDirs()
        {
            var env = RtbEnvironment.Initialize(XpkConfig.Default());

            Assert.NotEmpty(env.XPacksDirs);
            Assert.All(env.XPacksDirs, d => Assert.True(Directory.Exists(d), d));
            Assert.Contains(env.XPacksDirs, d => d.EndsWith(@"Race Track Builder\XPacks", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Initialize_Twice_ReturnsSameInstance()
        {
            var a = RtbEnvironment.Initialize(XpkConfig.Default());
            var b = RtbEnvironment.Initialize(XpkConfig.Default());

            Assert.Same(a, b);
        }

        // Taken before any test in this class initializes RaceBits (the environment is a per-process singleton).
        private static readonly string[] RtbDirBeforeInit = Snapshot(XpkConfig.Default().RtbDir);

        [Fact]
        public void Initialize_DoesNotWriteToRtbDir()
        {
            RtbEnvironment.Initialize(XpkConfig.Default());

            Assert.Equal(RtbDirBeforeInit, Snapshot(XpkConfig.Default().RtbDir));
        }

        private static string[] Snapshot(string dir) =>
            new DirectoryInfo(dir).GetFiles("*", SearchOption.TopDirectoryOnly)
                .Select(f => f.Name + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
    }
}
