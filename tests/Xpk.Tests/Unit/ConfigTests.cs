using System;
using System.IO;
using Xpk.Core.Env;
using Xunit;

namespace Xpk.Tests.Unit
{
    public class ConfigTests : IDisposable
    {
        private const string InstalledHash = "611efe142ff387bc17a6f0eb9c236d7d426a21b8f7c3fce2ce01c275a43694ff";
        private const string OtherHash = "0000000000000000000000000000000000000000000000000000000000000000";

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "xpk-tests", Guid.NewGuid().ToString("N"));

        public ConfigTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        [Fact]
        public void UnknownHash_IsReadOnly()
        {
            var guard = VersionGuard.Check(OtherHash, XpkConfig.Default());

            Assert.True(guard.IsReadOnly);
            Assert.Contains(OtherHash.Substring(0, 8), guard.Reason);
        }

        [Fact]
        public void KnownHash_IsWritable()
        {
            var guard = VersionGuard.Check(InstalledHash.ToUpperInvariant(), XpkConfig.Default());

            Assert.False(guard.IsReadOnly);
            Assert.Null(guard.Reason);
            Assert.Null(guard.Warning);
        }

        [Fact]
        public void AllowUnknown_IsWritable_WithWarning()
        {
            var cfg = XpkConfig.Default();
            cfg.AllowUnknownRaceBits = true;

            var guard = VersionGuard.Check(OtherHash, cfg);

            Assert.False(guard.IsReadOnly);
            Assert.NotNull(guard.Warning);
        }

        [Fact]
        public void Load_MissingFile_Defaults()
        {
            var cfg = XpkConfig.Load(Path.Combine(_dir, "config.json"));

            Assert.Equal(XpkConfig.DefaultRtbDir, cfg.RtbDir);
            Assert.Contains(InstalledHash, cfg.KnownRaceBitsSha256);
            Assert.False(cfg.AllowUnknownRaceBits);
            Assert.EndsWith(@"xpk\backups", cfg.BackupDir, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_PartialJson_KeepsDefaults()
        {
            string path = Path.Combine(_dir, "config.json");
            File.WriteAllText(path, "{ \"AllowUnknownRaceBits\": true, \"backupDir\": \"D:\\\\xpk-backups\" }");

            var cfg = XpkConfig.Load(path);

            Assert.True(cfg.AllowUnknownRaceBits);
            Assert.Equal(@"D:\xpk-backups", cfg.BackupDir);
            Assert.Equal(XpkConfig.DefaultRtbDir, cfg.RtbDir);
            Assert.Contains(InstalledHash, cfg.KnownRaceBitsSha256);
        }
    }
}
