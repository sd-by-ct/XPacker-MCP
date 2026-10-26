using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xpk.Core.Model;
using Xpk.Core.Props;
using Xpk.Core.Select;
using Xpk.Core.Session;

namespace Xpk.Core.Queries
{
    /// <summary>
    /// Read-only queries on an open XPack. Builds plain ItemInfo snapshots; never writes and never changes the pack.
    /// Only the requested page is built, so MD5s and texture headers are computed for at most `limit` textures.
    /// </summary>
    public static class Query
    {
        public const int DefaultLimit = 200;

        public static ItemPage List(XPackSession s, ItemKind kind, Selector sel = null, string[] fields = null, int offset = 0, int limit = DefaultLimit)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (offset < 0) throw new XpkException(ErrorCodes.InvalidArguments, "offset moet 0 of groter zijn.");
            if (limit < 1) throw new XpkException(ErrorCodes.InvalidArguments, "limit moet minstens 1 zijn.");
            var defs = SelectDefs(kind, fields);
            sel = sel ?? Selector.All;

            var matches = Items(s, kind).Where(e => sel.Matches(e.Path)).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
            var usage = new Usage(s);
            var page = matches.Skip(offset).Take(limit).Select(e => Build(s, kind, e, defs, usage)).ToList();
            int next = offset + page.Count;
            return new ItemPage
            {
                Items = page,
                Total = matches.Count,
                Offset = offset,
                NextOffset = next < matches.Count ? next : (int?)null,
            };
        }

        /// <summary>One item by its path relative to the kind (case-insensitive, '/' or '\'). Throws NOT_FOUND.</summary>
        public static ItemInfo Get(XPackSession s, ItemKind kind, string path)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            string wanted = Selector.Normalize(path ?? "");
            var all = Items(s, kind).ToList();
            var hit = all.FirstOrDefault(e => string.Equals(e.Path, wanted, StringComparison.OrdinalIgnoreCase));
            if (hit == null && kind == ItemKind.Texture && Path.GetExtension(wanted).Length == 0)
            {
                var byStem = all.Where(e => string.Equals(StripExtension(e.Path), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byStem.Count == 1) hit = byStem[0];
            }
            if (hit == null)
                throw new XpkException(ErrorCodes.NotFound, KindName(kind) + " '" + wanted + "' niet gevonden in " + s.Name + ".");
            return Build(s, kind, hit, PropertyMap.For(kind), new Usage(s));
        }

        public static string KindName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Object: return "Object";
                case ItemKind.StringObject: return "String Object";
                case ItemKind.Material: return "Materiaal";
                default: return "Texture";
            }
        }

        private static IReadOnlyList<PropertyDef> SelectDefs(ItemKind kind, string[] fields)
        {
            if (fields == null || fields.Length == 0) return PropertyMap.For(kind);
            return fields.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => PropertyMap.Find(kind, f)).Distinct().ToList();
        }

        private sealed class Entry
        {
            public string Path;
            public object Item;
        }

        private static IEnumerable<Entry> Items(XPackSession s, ItemKind kind)
        {
            var p = s.Pack;
            switch (kind)
            {
                case ItemKind.Object: return p.Objects.Select(o => new Entry { Path = ItemPaths.KindRelative(o.Path), Item = o });
                case ItemKind.StringObject: return p.SObjects.Select(o => new Entry { Path = ItemPaths.KindRelative(o.Path), Item = o });
                case ItemKind.Material: return p.Materials.Select(m => new Entry { Path = ItemPaths.KindRelative(m.Path), Item = m });
                default: return p.Textures.Select(t => new Entry { Path = ItemPaths.KindRelative(t.XPackPath), Item = t });
            }
        }

        private static ItemInfo Build(XPackSession s, ItemKind kind, Entry e, IReadOnlyList<PropertyDef> defs, Usage usage)
        {
            var info = new ItemInfo { Kind = kind, Path = e.Path };
            if (defs.Count > 0 || kind != ItemKind.Texture)
            {
                info.Fields = new Dictionary<string, object>();
                foreach (var def in defs)
                {
                    object v = def.Get(e.Item);
                    info.Fields[def.Name] = v is Enum ? v.ToString() : v;
                }
            }

            switch (kind)
            {
                case ItemKind.Object: FillObject(info, (RaceBits.BObject)e.Item); break;
                case ItemKind.StringObject: FillSObject(info, (RaceBits.SObject)e.Item); break;
                case ItemKind.Material: FillMaterial(s, info, (RaceBits.Mat)e.Item, usage); break;
                default: FillTexture(s, info, (RaceBits.Texture)e.Item, usage); break;
            }
            return info;
        }

        private static void FillObject(ItemInfo info, RaceBits.BObject o)
        {
            info.IsChildObject = o.IsChildObject;
            info.Materials = o.PolyLists
                .Select(pl => pl.MaterialPath)
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            info.Children = o.HierarcyTemplate.Select(h => new ChildInfo
            {
                Object = ItemPaths.KindRelative(h.BObjectPath),
                Scale = new[] { h.Scale.X, h.Scale.Y, h.Scale.Z },
                RotationDegrees = new[] { h.Rotation_Degrees.X, h.Rotation_Degrees.Y, h.Rotation_Degrees.Z },
                Translation = new[] { h.Translation.X, h.Translation.Y, h.Translation.Z },
                ChanceBorn = h.ChanceBorn,
            }).ToList();
        }

        private static void FillSObject(ItemInfo info, RaceBits.SObject so)
        {
            info.Objects = new[] { so.Start, so.Pattern, so.End }
                .Where(pat => pat?.Pieces != null)
                .SelectMany(pat => pat.Pieces)
                .Where(piece => piece.BObject != null)
                .Select(piece => ItemPaths.KindRelative(piece.BObject.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void FillMaterial(XPackSession s, ItemInfo info, RaceBits.Mat m, Usage usage)
        {
            var maps = new[] { m.TexMap1, m.TexMap2, m.TexMap3, m.TexMap4, m.TexMap5, m.TexMap6, m.TexMap7, m.TexMap8, m.TexMap9 };
            var texMaps = new List<TexMapInfo>();
            for (int i = 0; i < maps.Length; i++)
            {
                string path = maps[i]?.Path;
                if (string.IsNullOrEmpty(path)) continue;
                texMaps.Add(new TexMapInfo
                {
                    Slot = "TexMap" + (i + 1),
                    Texture = OwnRelative(s, path),
                    Exists = File.Exists(Path.Combine(ParentDir(s), path)),
                });
            }
            info.TexMaps = texMaps;
            info.Purposes = Enum.GetValues(typeof(RaceBits.Purposes)).Cast<RaceBits.Purposes>()
                .Where(p => p != RaceBits.Purposes.None && m.PurposeContains(p))
                .Select(p => p.ToString())
                .ToList();
            info.MissingRequiredTextures = (m.GetMissingRequiredTextures() ?? new List<string>()).ToList();
            info.UsedBy = usage.MaterialUsers(m.Path);
            info.UsageCount = info.UsedBy.Count;
        }

        private static void FillTexture(XPackSession s, ItemInfo info, RaceBits.Texture t, Usage usage)
        {
            string file = Path.Combine(ParentDir(s), t.XPackPath);
            info.UsedBy = usage.TextureUsers(t.XPackPath);
            if (!File.Exists(file)) return;
            info.Bytes = new FileInfo(file).Length;
            var header = TextureHeader.Read(file);
            info.Width = header?.Width;
            info.Height = header?.Height;
            info.Format = header?.Format ?? Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                info.Md5 = string.Concat(md5.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        // RaceBits paths start with the XPack name, which is the name of the XPack folder.
        private static string ParentDir(XPackSession s) => Path.GetDirectoryName(s.Folder);

        /// <summary>Relative to the kind when the path is in this XPack; the full RaceBits path otherwise.</summary>
        private static string OwnRelative(XPackSession s, string raceBitsPath) =>
            string.Equals(ItemPaths.PackName(raceBitsPath), s.Pack.Name, StringComparison.OrdinalIgnoreCase)
                ? ItemPaths.KindRelative(raceBitsPath)
                : raceBitsPath;

        private static string StripExtension(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Length == 0 ? path : path.Substring(0, path.Length - ext.Length);
        }

        /// <summary>Who uses which material and texture, built once per query.</summary>
        private sealed class Usage
        {
            private readonly XPackSession _s;
            private Dictionary<string, List<string>> _materialUsers;
            private Dictionary<string, List<string>> _textureUsers;

            public Usage(XPackSession s) => _s = s;

            public IReadOnlyList<string> MaterialUsers(string materialPath)
            {
                if (_materialUsers == null) _materialUsers = BuildMaterialUsers();
                return Lookup(_materialUsers, materialPath);
            }

            public IReadOnlyList<string> TextureUsers(string texturePath)
            {
                if (_textureUsers == null) _textureUsers = BuildTextureUsers();
                return Lookup(_textureUsers, texturePath);
            }

            private static IReadOnlyList<string> Lookup(Dictionary<string, List<string>> map, string key) =>
                map.TryGetValue(key ?? "", out var users)
                    ? users.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();

            private Dictionary<string, List<string>> BuildMaterialUsers()
            {
                var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var p = _s.Pack;
                foreach (var o in p.Objects)
                foreach (var pl in o.PolyLists)
                {
                    string mat = pl.Material?.Path ?? (string.IsNullOrEmpty(pl.MaterialPath) ? null : p.Name + @"\Materials\" + pl.MaterialPath);
                    if (mat != null) Add(map, mat, ItemPaths.KindRelative(o.Path));
                }
                foreach (var so in p.SObjects)
                {
                    if (so.Materials == null) continue;
                    foreach (var m in so.Materials) Add(map, m.Path, ItemPaths.KindRelative(so.Path));
                }
                return map;
            }

            private Dictionary<string, List<string>> BuildTextureUsers()
            {
                var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in _s.Pack.Materials)
                foreach (var tm in new[] { m.TexMap1, m.TexMap2, m.TexMap3, m.TexMap4, m.TexMap5, m.TexMap6, m.TexMap7, m.TexMap8, m.TexMap9 })
                {
                    if (!string.IsNullOrEmpty(tm?.Path)) Add(map, tm.Path, ItemPaths.KindRelative(m.Path));
                }
                return map;
            }

            private static void Add(Dictionary<string, List<string>> map, string key, string user)
            {
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
                list.Add(user);
            }
        }
    }
}
