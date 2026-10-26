using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xpk.Core.Env;

namespace Xpk.Core.Session
{
    /// <summary>
    /// One XPack loaded through RaceBits, plus a snapshot (mtime + size) of its files to detect edits made elsewhere
    /// (e.g. in XPacker). Opening follows XPacker's order: new XPack(zip), LoadFromFolder, VerifyAndSetReferences,
    /// FillEmptyDefaultListsFromAllItems. Opening never writes.
    /// </summary>
    public sealed class XPackSession
    {
        // Explorer rewrites these at will; they are not part of the XPack.
        private static readonly string[] IgnoredFiles = { "Thumbs.db", "desktop.ini" };

        private readonly RtbEnvironment _env;
        private readonly VersionGuard _guard;
        private RaceBits.XPack _pack;
        private List<string> _warnings;
        private Dictionary<string, FileStamp> _snapshot;

        private XPackSession(RtbEnvironment env, string folder, VersionGuard guard)
        {
            _env = env;
            Folder = folder;
            _guard = guard;
        }

        public string Folder { get; }

        public string Name => Path.GetFileName(Folder);

        public VersionGuard Guard => _guard;

        internal RaceBits.XPack Pack => _pack;

        /// <param name="nameOrPath">An XPack folder, or the name of an XPack in one of env.XPacksDirs.</param>
        /// <param name="guard">Write permission for this RaceBits build; default: VersionGuard.Check(env, XpkConfig.Load()).</param>
        public static XPackSession Open(RtbEnvironment env, string nameOrPath, VersionGuard guard = null)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));
            string folder = XPackCatalog.Resolve(env.XPacksDirs, nameOrPath);
            var session = new XPackSession(env, folder, guard ?? VersionGuard.Check(env, XpkConfig.Load()));
            session.Load();
            return session;
        }

        public void Reload() => Load();

        /// <summary>True when a file was added, removed or changed (mtime or size) since the last Open/Reload.</summary>
        public bool ChangedOnDisk()
        {
            var now = TakeSnapshot(Folder);
            return now.Count != _snapshot.Count
                   || now.Any(kv => !_snapshot.TryGetValue(kv.Key, out var old) || !old.Equals(kv.Value));
        }

        public XPackSummary Summary()
        {
            var warnings = new List<string>(_warnings);
            if (_guard.Warning != null) warnings.Add(_guard.Warning);
            return new XPackSummary
            {
                Name = _pack.Name,
                Folder = Folder,
                Author = _pack.Author,
                Date = _pack.Date,
                Description = _pack.Description,
                Objects = _pack.Objects.Count,
                SObjects = _pack.SObjects.Count,
                Materials = _pack.Materials.Count,
                Textures = _pack.Textures.Count,
                CrossSections = _pack.CrossSections.Count,
                RaceBitsSha256 = _env.RaceBitsSha256,
                ReadOnly = _guard.IsReadOnly,
                ReadOnlyReason = _guard.Reason,
                Warnings = warnings,
            };
        }

        private void Load()
        {
            FolderCheck.Ensure(Folder);
            var snapshot = TakeSnapshot(Folder);
            var warnings = new List<string>();
            var pack = RaceBitsCall.Run(() => LoadPack(Folder, warnings), RaceBitsCall.DefaultTimeout, "openen van " + Name);
            _pack = pack;
            _warnings = warnings;
            _snapshot = snapshot;
        }

        // Non-inlined: RaceBits types are only touched after RtbEnvironment registered its AssemblyResolve handler.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static RaceBits.XPack LoadPack(string folder, List<string> warnings)
        {
            string parent = Path.GetDirectoryName(folder);
            string name = Path.GetFileName(folder);

            // XPack(zipPath) sets XPackFullZipFilePath; XPackFolder is derived from it. The zip need not exist.
            var pack = new RaceBits.XPack(Path.Combine(parent, name + ".zip"));
            try
            {
                if (!pack.LoadFromFolder(parent, name))
                    throw new XpkException(ErrorCodes.LoadFailed, "RaceBits kon " + folder + " niet laden (LoadFromFolder gaf false).");
            }
            catch (Exception ex) when (!(ex is XpkException))
            {
                string detail = ex.InnerException != null && !ex.Message.Contains(ex.InnerException.Message)
                    ? ex.Message + ": " + ex.InnerException.Message
                    : ex.Message;
                throw new XpkException(ErrorCodes.LoadFailed, "XPack laden mislukt. " + detail.Replace("\r\n", " ").Replace('\n', ' '), ex);
            }

            var packs = new RaceBits.XPacks(parent);
            packs.Add(pack);
            if (!pack.VerifyAndSetReferences(packs))
            {
                // VerifyAndSetReferences only returns a bool; the separate calls give the messages.
                pack.VerifyMaterials(packs, warnings);
                pack.VerifyObjectsAndSetReferences(warnings);
                pack.VerifySObjectsAndSetObjects(warnings);
                pack.VerifyCrossSections(packs, warnings);
                if (warnings.Count == 0) warnings.Add("RaceBits meldt ontbrekende verwijzingen in " + name + ".");
            }
            pack.FillEmptyDefaultListsFromAllItems();
            return pack;
        }

        private static Dictionary<string, FileStamp> TakeSnapshot(string folder) =>
            new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => !IgnoredFiles.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    f => f.FullName.Substring(folder.Length),
                    f => new FileStamp(f.LastWriteTimeUtc.Ticks, f.Length),
                    StringComparer.OrdinalIgnoreCase);

        private struct FileStamp : IEquatable<FileStamp>
        {
            public FileStamp(long ticks, long length)
            {
                Ticks = ticks;
                Length = length;
            }

            public long Ticks { get; }
            public long Length { get; }

            public bool Equals(FileStamp other) => Ticks == other.Ticks && Length == other.Length;
        }
    }
}
