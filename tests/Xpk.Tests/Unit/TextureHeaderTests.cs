using System;
using System.IO;
using System.Text;
using Xpk.Core.Model;
using Xunit;

namespace Xpk.Tests.Unit
{
    public class TextureHeaderTests
    {
        private static void PutInt(byte[] b, int offset, int value) => BitConverter.GetBytes(value).CopyTo(b, offset);

        private static TextureHeader Read(byte[] bytes) => TextureHeader.Read(new MemoryStream(bytes));

        [Fact]
        public void TextureHeader_Dds()
        {
            var b = new byte[128];
            Encoding.ASCII.GetBytes("DDS ").CopyTo(b, 0);
            PutInt(b, 4, 124);
            PutInt(b, 12, 256);    // height
            PutInt(b, 16, 512);    // width
            PutInt(b, 80, 0x4);    // DDPF_FOURCC
            Encoding.ASCII.GetBytes("DXT5").CopyTo(b, 84);

            var h = Read(b);

            Assert.Equal(512, h.Width);
            Assert.Equal(256, h.Height);
            Assert.Equal("DDS DXT5", h.Format);
        }

        [Fact]
        public void TextureHeader_DdsUncompressed()
        {
            var b = new byte[128];
            Encoding.ASCII.GetBytes("DDS ").CopyTo(b, 0);
            PutInt(b, 12, 64);
            PutInt(b, 16, 32);
            PutInt(b, 80, 0x41);   // DDPF_RGB | DDPF_ALPHAPIXELS
            PutInt(b, 88, 32);

            Assert.Equal("DDS RGBA32", Read(b).Format);
        }

        [Fact]
        public void TextureHeader_Png()
        {
            var b = new byte[33];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }.CopyTo(b, 0);
            Encoding.ASCII.GetBytes("IHDR").CopyTo(b, 12);
            new byte[] { 0, 0, 4, 0, 0, 0, 2, 0 }.CopyTo(b, 16);   // 1024 x 512, big-endian

            var h = Read(b);

            Assert.Equal(1024, h.Width);
            Assert.Equal(512, h.Height);
            Assert.Equal("PNG", h.Format);
        }

        [Fact]
        public void TextureHeader_Jpg()
        {
            var b = new byte[]
            {
                0xFF, 0xD8,                                     // SOI
                0xFF, 0xE0, 0x00, 0x06, 1, 2, 3, 4,              // APP0, length 6
                0xFF, 0xC4, 0x00, 0x04, 0, 0,                    // DHT (not a frame header)
                0xFF, 0xC0, 0x00, 0x0B, 8, 0x01, 0x2C, 0x02, 0x58, 3, 0, 0, 0,   // SOF0: 600 x 300
            };

            var h = Read(b);

            Assert.Equal(600, h.Width);
            Assert.Equal(300, h.Height);
            Assert.Equal("JPEG", h.Format);
        }

        [Fact]
        public void TextureHeader_Unknown_Null()
        {
            Assert.Null(Read(Encoding.ASCII.GetBytes("RBR texture data")));
            Assert.Null(Read(new byte[0]));
        }

        [Fact]
        public void ItemPath_StripsPackAndKind()
        {
            Assert.Equal(@"Vegetation\Trees\Tree01", ItemPaths.KindRelative(@"Default-testpack\Objects\Vegetation\Trees\Tree01"));
            Assert.Equal("Ripple001.dds", ItemPaths.KindRelative(@"Default-testpack\Textures\Ripple001.dds"));
            Assert.Equal("", ItemPaths.KindRelative(""));
        }
    }
}
