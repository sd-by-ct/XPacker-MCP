using System.Collections.Generic;

namespace Xpk.Core.Model
{
    /// <summary>
    /// Plain snapshot of one item, detached from RaceBits. Paths are relative to the item kind ("Bomen\Eik_01";
    /// textures with extension). Only the members for the item's kind are set; the rest stay null.
    /// </summary>
    public sealed class ItemInfo
    {
        public ItemKind Kind { get; set; }

        public string Path { get; set; }

        /// <summary>Whitelisted properties (PropertyMap), in whitelist order. Enums by name, numbers as float/int.</summary>
        public IDictionary<string, object> Fields { get; set; }

        // Object
        public bool? IsChildObject { get; set; }

        /// <summary>Materials used by an object's PolyLists, relative to Materials\.</summary>
        public IReadOnlyList<string> Materials { get; set; }

        public IReadOnlyList<ChildInfo> Children { get; set; }

        // String Object
        /// <summary>Objects used in the Start, Pattern and End pieces of a String Object.</summary>
        public IReadOnlyList<string> Objects { get; set; }

        // Material
        public IReadOnlyList<TexMapInfo> TexMaps { get; set; }

        /// <summary>Material purposes (Terrain, Track, Wall, Object); read-only, see PropertyMap.</summary>
        public IReadOnlyList<string> Purposes { get; set; }

        public IReadOnlyList<string> MissingRequiredTextures { get; set; }

        /// <summary>Number of objects and String Objects that use the material.</summary>
        public int? UsageCount { get; set; }

        /// <summary>Material: objects/String Objects using it. Texture: materials using it.</summary>
        public IReadOnlyList<string> UsedBy { get; set; }

        // Texture
        public long? Bytes { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string Format { get; set; }
        public string Md5 { get; set; }
    }

    public sealed class ChildInfo
    {
        /// <summary>Child object, relative to Objects\.</summary>
        public string Object { get; set; }
        public float[] Scale { get; set; }
        public float[] RotationDegrees { get; set; }
        public float[] Translation { get; set; }
        public float ChanceBorn { get; set; }
    }

    public sealed class TexMapInfo
    {
        /// <summary>RaceBits slot (TexMap1..TexMap9).</summary>
        public string Slot { get; set; }

        /// <summary>Texture relative to Textures\, or the full RaceBits path when it lives in another XPack.</summary>
        public string Texture { get; set; }

        /// <summary>The texture file exists in this XPack's folder.</summary>
        public bool Exists { get; set; }
    }

    public sealed class ItemPage
    {
        public IReadOnlyList<ItemInfo> Items { get; set; }
        public int Total { get; set; }
        public int Offset { get; set; }

        /// <summary>Offset of the next page, or null when this is the last page.</summary>
        public int? NextOffset { get; set; }
    }
}
