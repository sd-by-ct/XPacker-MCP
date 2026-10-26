using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Xpk.Core;
using Xpk.Core.Env;
using Xpk.Core.Session;
using Xunit;

namespace Xpk.Tests.Unit
{
    public class RaceBitsCallTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [Fact]
        public void RaceBitsCall_ReturnsResult()
        {
            Assert.Equal(42, RaceBitsCall.Run(() => 42, TimeSpan.FromSeconds(5), "test"));
        }

        [Fact]
        public void RaceBitsCall_Exception_Propagates()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                RaceBitsCall.Run<int>(() => throw new InvalidOperationException("boem"), TimeSpan.FromSeconds(5), "test"));

            Assert.Equal("boem", ex.Message);
        }

        [Fact]
        public void RaceBitsCall_MessageBox_ClosedAndReported()
        {
            var ex = Assert.Throws<XpkException>(() =>
                RaceBitsCall.Run(() => MessageBox(IntPtr.Zero, "Failed Ongeldige tekens in pad.", "RaceBits", 0), TimeSpan.FromSeconds(20), "test"));

            Assert.Equal(ErrorCodes.RaceBitsDialog, ex.Code);
            Assert.Contains("Ongeldige tekens in pad", ex.Message);
        }

        [Fact]
        public void RaceBitsCall_Timeout()
        {
            var ex = Assert.Throws<XpkException>(() =>
                RaceBitsCall.Run(() => { Thread.Sleep(5000); return 0; }, TimeSpan.FromMilliseconds(300), "test"));

            Assert.Equal(ErrorCodes.RaceBitsTimeout, ex.Code);
        }

        [Fact]
        public void FolderCheck_PathTooLong_Refused()
        {
            string ok = @"C:\" + new string('a', 255) + ".x";      // 260 chars: one too many for MAX_PATH
            string fine = @"C:\" + new string('b', 250) + ".x";

            Assert.Equal(ok, FolderCheck.FindTooLong(new[] { fine, ok }));
            Assert.Null(FolderCheck.FindTooLong(new[] { fine }));
        }

        [Fact]
        public void Catalog_Resolve_ByName_CaseInsensitive()
        {
            string dir = Path.Combine(Path.GetTempPath(), "xpk-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "Mijn Pack", "Objects"));
            Directory.CreateDirectory(Path.Combine(dir, "Geen pack"));
            try
            {
                Assert.Equal(Path.Combine(dir, "Mijn Pack"), XPackCatalog.Resolve(new[] { dir }, "mijn pack"));
                var ex = Assert.Throws<XpkException>(() => XPackCatalog.Resolve(new[] { dir }, "Ander"));
                Assert.Equal(ErrorCodes.XPackNotFound, ex.Code);
                Assert.Contains("Mijn Pack", ex.Message);
                Assert.DoesNotContain("Geen pack", ex.Message);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void FolderCheck_OfflineAttribute_NotLocal()
        {
            const FileAttributes recallOnDataAccess = (FileAttributes)0x400000;

            Assert.True(FolderCheck.IsNotLocal(FileAttributes.Archive | FileAttributes.Offline));
            Assert.True(FolderCheck.IsNotLocal(FileAttributes.Archive | recallOnDataAccess));
            Assert.False(FolderCheck.IsNotLocal(FileAttributes.Archive | FileAttributes.ReadOnly));
        }
    }
}
