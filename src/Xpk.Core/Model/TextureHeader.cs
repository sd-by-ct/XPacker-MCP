using System;
using System.IO;
using System.Text;

namespace Xpk.Core.Model
{
    /// <summary>Width, height and format from the file header of a DDS, PNG or JPEG texture, without Direct3D.</summary>
    public sealed class TextureHeader
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>"DDS DXT5", "DDS RGBA32", "PNG" or "JPEG".</summary>
        public string Format { get; private set; }

        public static TextureHeader Read(string path)
        {
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return Read(s);
        }

        /// <summary>Null for an unknown or truncated file.</summary>
        public static TextureHeader Read(Stream s)
        {
            var head = new byte[128];
            int n = ReadUpTo(s, head, 0, head.Length);
            if (n >= 8 && head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G')
                return n >= 24 ? new TextureHeader { Width = BigEndian32(head, 16), Height = BigEndian32(head, 20), Format = "PNG" } : null;
            if (n >= 128 && Encoding.ASCII.GetString(head, 0, 4) == "DDS ")
                return Dds(head);
            if (n >= 2 && head[0] == 0xFF && head[1] == 0xD8)
            {
                s.Position = 2;
                return Jpeg(s);
            }
            return null;
        }

        // DDS: "DDS " + DDS_HEADER; height at 12, width at 16, pixel format flags at 80, FourCC at 84, RGB bit count at 88.
        private static TextureHeader Dds(byte[] h)
        {
            int flags = BitConverter.ToInt32(h, 80);
            string format;
            if ((flags & 0x4) != 0)
                format = "DDS " + Encoding.ASCII.GetString(h, 84, 4).TrimEnd('\0', ' ');
            else
                format = "DDS " + ((flags & 0x1) != 0 ? "RGBA" : "RGB") + BitConverter.ToInt32(h, 88);
            return new TextureHeader { Height = BitConverter.ToInt32(h, 12), Width = BitConverter.ToInt32(h, 16), Format = format };
        }

        // JPEG: walk the markers to the first frame header (SOF0..SOF15, except DHT C4, JPG C8 and DAC CC).
        private static TextureHeader Jpeg(Stream s)
        {
            var seg = new byte[7];
            while (true)
            {
                int ff = s.ReadByte();
                if (ff != 0xFF) return null;
                int marker = s.ReadByte();
                while (marker == 0xFF) marker = s.ReadByte();
                if (marker < 0 || marker == 0xD9 || marker == 0xDA) return null;
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;   // markers without length

                var lenBytes = new byte[2];
                if (ReadUpTo(s, lenBytes, 0, 2) < 2) return null;
                int length = (lenBytes[0] << 8) | lenBytes[1];
                if (length < 2) return null;

                bool isFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isFrame)
                {
                    if (ReadUpTo(s, seg, 0, 5) < 5) return null;
                    return new TextureHeader
                    {
                        Height = (seg[1] << 8) | seg[2],
                        Width = (seg[3] << 8) | seg[4],
                        Format = "JPEG",
                    };
                }
                if (s.CanSeek) s.Seek(length - 2, SeekOrigin.Current);
                else if (ReadUpTo(s, new byte[length - 2], 0, length - 2) < length - 2) return null;
                if (s.CanSeek && s.Position >= s.Length) return null;
            }
        }

        private static int BigEndian32(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

        private static int ReadUpTo(Stream s, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buffer, offset + total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }
    }
}
