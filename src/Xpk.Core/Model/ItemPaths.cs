namespace Xpk.Core.Model
{
    public static class ItemPaths
    {
        /// <summary>"&lt;XPack&gt;\&lt;Kind&gt;\a\b" → "a\b". RaceBits names items and texture maps with both prefixes.</summary>
        public static string KindRelative(string raceBitsPath)
        {
            if (string.IsNullOrEmpty(raceBitsPath)) return raceBitsPath ?? "";
            int first = raceBitsPath.IndexOf('\\');
            int second = first < 0 ? -1 : raceBitsPath.IndexOf('\\', first + 1);
            return second < 0 ? raceBitsPath : raceBitsPath.Substring(second + 1);
        }

        /// <summary>The XPack name, the first segment of a RaceBits path.</summary>
        public static string PackName(string raceBitsPath)
        {
            int first = (raceBitsPath ?? "").IndexOf('\\');
            return first < 0 ? raceBitsPath : raceBitsPath.Substring(0, first);
        }
    }
}
