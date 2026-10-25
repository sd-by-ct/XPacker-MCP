using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Xpk.Core.Env;

namespace Xpk.Core.Spike
{
    /// <summary>
    /// Phase 0 spike (temporary): open an XPack folder the way XPacker does, bump one LODOut and save it.
    /// Replaced by XPackSession and Writer in phases 1 and 2.
    /// </summary>
    public sealed class SpikeRunner
    {
        private readonly RaceBits.XPack _pack;

        private SpikeRunner(RaceBits.XPack pack, SpikeSummary summary)
        {
            _pack = pack;
            Summary = summary;
        }

        public SpikeSummary Summary { get; }

        /// <param name="xpackFolder">The XPack source folder, e.g. C:\Users\Moor\xpk\NewXPackTest.</param>
        public static SpikeRunner Open(RtbEnvironment env, string xpackFolder)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));
            string folder = Path.GetFullPath(xpackFolder).TrimEnd('\\');
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("XPack-map bestaat niet: " + folder);
            return OpenCore(folder);
        }

        private static SpikeRunner OpenCore(string folder)
        {
            string parent = Path.GetDirectoryName(folder);
            string name = Path.GetFileName(folder);
            var sw = Stopwatch.StartNew();

            // XPack(zipPath) sets XPackFullZipFilePath; XPackFolder is derived from it as <dir>\<name>.
            // Without it the XPackFolder getter throws. The zip itself does not need to exist.
            var pack = new RaceBits.XPack(Path.Combine(parent, name + ".zip"));
            if (!pack.LoadFromFolder(parent, name))
                throw new InvalidOperationException("RaceBits LoadFromFolder gaf false voor " + folder);
            var packs = new RaceBits.XPacks(parent);
            packs.Add(pack);
            bool verified = pack.VerifyAndSetReferences(packs);

            // Present in the v2000 build only; XPacker v2000 calls it after loading.
            var fill = typeof(RaceBits.XPack).GetMethod("FillEmptyDefaultListsFromAllItems", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            fill?.Invoke(pack, null);

            var summary = new SpikeSummary
            {
                Name = pack.Name,
                Folder = pack.XPackFolder,
                Objects = pack.Objects.Count,
                SObjects = pack.SObjects.Count,
                Materials = pack.Materials.Count,
                Textures = pack.Textures.Count,
                CrossSections = pack.CrossSections.Count,
                ReferencesVerified = verified,
                LoadMilliseconds = sw.ElapsedMilliseconds,
            };
            return new SpikeRunner(pack, summary);
        }

        public string[] ObjectPaths() => _pack.Objects.Select(o => o.Path).ToArray();

        public float GetLodOut(string objectName) => FindObject(objectName).LODOut;

        /// <summary>The object's scalar properties and mesh size as invariant strings, for semantic round-trip checks.</summary>
        public string[] ObjectValues(string objectName)
        {
            var obj = FindObject(objectName);
            var values = typeof(RaceBits.BObject).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                            && (p.PropertyType.IsPrimitive || p.PropertyType.IsEnum || p.PropertyType == typeof(string)))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.Name + "=" + Convert.ToString(p.GetValue(obj, null), CultureInfo.InvariantCulture))
                .ToList();
            values.Add("PolyLists=" + obj.PolyLists.Count);
            values.Add("HierarcyTemplate=" + obj.HierarcyTemplate.Count);
            return values.ToArray();
        }

        /// <summary>Full path of the object's XML file, for backups.</summary>
        public string ObjectXmlPath(string objectName)
        {
            var obj = FindObject(objectName);
            string relative = RaceBits.XPack.GetPathWithPackNameRemoved(obj.Path);
            return Path.Combine(Summary.Folder, relative + ".xml");
        }

        /// <summary>Adds <paramref name="lodOutDelta"/> to LODOut (0 = plain round-trip) and calls CreateObject(obj, false).</summary>
        public SpikeSaveResult SaveObject(string objectName, float lodOutDelta)
        {
            var obj = FindObject(objectName);
            float old = obj.LODOut;
            obj.LODOut = old + lodOutDelta;
            _pack.CreateObject(obj, false);
            return new SpikeSaveResult { ObjectPath = obj.Path, OldLodOut = old, NewLodOut = obj.LODOut };
        }

        private RaceBits.BObject FindObject(string objectName)
        {
            // Accepts "Billboards_L_1", "Objects\Billboards_L_1" or the full RaceBits path "<pack>\Objects\Billboards_L_1".
            string wanted = objectName.Replace('/', '\\').Trim('\\');
            var match = _pack.Objects.Where(o =>
                    string.Equals(o.Path, wanted, StringComparison.OrdinalIgnoreCase)
                    || o.Path.EndsWith("\\Objects\\" + wanted, StringComparison.OrdinalIgnoreCase)
                    || o.Path.EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (match.Count == 1) return match[0];
            if (match.Count == 0) throw new ArgumentException("Object niet gevonden: " + objectName);
            throw new ArgumentException("Meerdere objecten passen bij " + objectName + ": " + string.Join(", ", match.Select(o => o.Path)));
        }
    }

    public sealed class SpikeSummary
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public int Objects { get; set; }
        public int SObjects { get; set; }
        public int Materials { get; set; }
        public int Textures { get; set; }
        public int CrossSections { get; set; }
        public bool ReferencesVerified { get; set; }
        public long LoadMilliseconds { get; set; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} objecten, {2} String Objects, {3} materialen, {4} textures, {5} Cross Sections (referenties {6}, {7} ms)",
            Name, Objects, SObjects, Materials, Textures, CrossSections, ReferencesVerified ? "ok" : "NIET ok", LoadMilliseconds);
    }

    public sealed class SpikeSaveResult
    {
        public string ObjectPath { get; set; }
        public float OldLodOut { get; set; }
        public float NewLodOut { get; set; }
    }
}
