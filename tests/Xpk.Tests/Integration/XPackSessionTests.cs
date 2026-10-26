using System;
using System.Diagnostics;
using System.IO;
using Xpk.Core;
using Xpk.Core.Session;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class XPackSessionTests
    {
        [Fact]
        public void Open_Fixture_CountsMatch()
        {
            string folder = Fixture.FreshCopy(nameof(Open_Fixture_CountsMatch));

            var summary = XPackSession.Open(Fixture.Env(), folder).Summary();

            Assert.Equal(Fixture.Name, summary.Name);
            Assert.Equal(folder, summary.Folder, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("Brendon Pywell", summary.Author);
            Assert.Equal(28, summary.Objects);
            Assert.Equal(4, summary.SObjects);
            Assert.Equal(28, summary.Materials);
            Assert.Equal(67, summary.Textures);
            Assert.Equal(13, summary.CrossSections);
            Assert.StartsWith("611efe14", summary.RaceBitsSha256);
            Assert.Empty(summary.Warnings);
        }

        [Fact]
        public void Open_DoesNotWriteToFolder()
        {
            string folder = Fixture.FreshCopy(nameof(Open_DoesNotWriteToFolder));
            var before = Fixture.Hashes(folder);

            XPackSession.Open(Fixture.Env(), folder);

            Assert.Equal(before, Fixture.Hashes(folder));
        }

        [Fact]
        public void ChangedOnDisk_AfterTouch_IsTrue()
        {
            string folder = Fixture.FreshCopy(nameof(ChangedOnDisk_AfterTouch_IsTrue));
            var session = XPackSession.Open(Fixture.Env(), folder);
            Assert.False(session.ChangedOnDisk());

            File.SetLastWriteTimeUtc(Path.Combine(folder, @"Objects\Vegetation\Trees\Tree01.xml"), DateTime.UtcNow.AddMinutes(1));

            Assert.True(session.ChangedOnDisk());
        }

        [Fact]
        public void ChangedOnDisk_NewFile_IsTrue()
        {
            string folder = Fixture.FreshCopy(nameof(ChangedOnDisk_NewFile_IsTrue));
            var session = XPackSession.Open(Fixture.Env(), folder);

            File.WriteAllText(Path.Combine(folder, @"Objects\Extra.xml"), "<Object />");

            Assert.True(session.ChangedOnDisk());
        }

        [Fact]
        public void ChangedOnDisk_ThumbsDb_Ignored()
        {
            string folder = Fixture.FreshCopy(nameof(ChangedOnDisk_ThumbsDb_Ignored));
            var session = XPackSession.Open(Fixture.Env(), folder);

            File.WriteAllText(Path.Combine(folder, @"Objects\Thumbs.db"), "explorer");
            File.WriteAllText(Path.Combine(folder, @"Objects\desktop.ini"), "explorer");

            Assert.False(session.ChangedOnDisk());
        }

        [Fact]
        public void Reload_ResetsSnapshot()
        {
            string folder = Fixture.FreshCopy(nameof(Reload_ResetsSnapshot));
            var session = XPackSession.Open(Fixture.Env(), folder);
            File.SetLastWriteTimeUtc(Path.Combine(folder, @"Objects\Vegetation\Trees\Tree01.xml"), DateTime.UtcNow.AddMinutes(1));
            Assert.True(session.ChangedOnDisk());

            session.Reload();

            Assert.False(session.ChangedOnDisk());
            Assert.Equal(28, session.Summary().Objects);
        }

        [Fact]
        public void Open_PathWithSpacesAndAccents()
        {
            string folder = Fixture.FreshCopy(nameof(Open_PathWithSpacesAndAccents), "Mäp met spaties", "Default testpack é");

            var summary = XPackSession.Open(Fixture.Env(), folder).Summary();

            Assert.Equal("Default testpack é", summary.Name);
            Assert.Equal(28, summary.Objects);
            Assert.Equal(28, summary.Materials);
        }

        [Fact]
        public void Open_UnknownName_XPACK_NOT_FOUND()
        {
            var ex = Assert.Throws<XpkException>(() => XPackSession.Open(Fixture.Env(), "Bestaat-niet-" + Guid.NewGuid().ToString("N")));

            Assert.Equal(ErrorCodes.XPackNotFound, ex.Code);
        }

        [Fact]
        public void Open_FolderWithoutXPack_XPACK_NOT_FOUND()
        {
            string folder = Path.Combine(Fixture.FixturesDir, "work", nameof(Open_FolderWithoutXPack_XPACK_NOT_FOUND));
            Directory.CreateDirectory(folder);

            var ex = Assert.Throws<XpkException>(() => XPackSession.Open(Fixture.Env(), folder));

            Assert.Equal(ErrorCodes.XPackNotFound, ex.Code);
        }

        [Fact]
        public void Open_CorruptObject_ReportsErrorWithoutHanging()
        {
            string folder = Fixture.FreshCopy(nameof(Open_CorruptObject_ReportsErrorWithoutHanging));
            File.WriteAllText(Path.Combine(folder, @"Objects\Vegetation\Trees\Tree01.xml"), "<Object><LODOut>abc");
            var sw = Stopwatch.StartNew();

            var ex = Assert.Throws<XpkException>(() => XPackSession.Open(Fixture.Env(), folder));

            Assert.Equal(ErrorCodes.LoadFailed, ex.Code);
            Assert.Contains(@"Trees\Tree01", ex.Message);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void Open_MissingMaterial_Warning()
        {
            string folder = Fixture.FreshCopy(nameof(Open_MissingMaterial_Warning));
            File.Delete(Path.Combine(folder, @"Materials\Objects\Vegetation\Trees\Tree02.xml"));

            var summary = XPackSession.Open(Fixture.Env(), folder).Summary();

            Assert.Contains(summary.Warnings, w => w.IndexOf(@"Trees\Tree02", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
