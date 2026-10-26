using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xpk.Core.Session
{
    public sealed class XPackEntry
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public DateTime LastModifiedUtc { get; set; }
    }

    /// <summary>Finds XPack source folders: by path, or by name in the XPack folders (RtbEnvironment.XPacksDirs).</summary>
    public static class XPackCatalog
    {
        /// <summary>A folder is an XPack when it has Description.xml or an Objects/Materials folder.</summary>
        public static bool IsXPackFolder(string folder) =>
            File.Exists(Path.Combine(folder, "Description.xml"))
            || Directory.Exists(Path.Combine(folder, "Objects"))
            || Directory.Exists(Path.Combine(folder, "Materials"));

        public static IReadOnlyList<XPackEntry> List(IEnumerable<string> xpacksDirs) =>
            xpacksDirs
                .Where(Directory.Exists)
                .SelectMany(Directory.EnumerateDirectories)
                .Where(IsXPackFolder)
                .Select(d => new XPackEntry
                {
                    Name = Path.GetFileName(d),
                    Folder = d,
                    LastModifiedUtc = LastModifiedUtc(d),
                })
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Returns the full folder path, or throws XPACK_NOT_FOUND with the names that do exist.</summary>
        public static string Resolve(IEnumerable<string> xpacksDirs, string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                throw new XpkException(ErrorCodes.XPackNotFound, "Geef een XPack op (naam of map).");

            string trimmed = nameOrPath.Trim().TrimEnd('\\', '/');
            if (Directory.Exists(trimmed))
            {
                string full = Path.GetFullPath(trimmed);
                if (!IsXPackFolder(full))
                    throw new XpkException(ErrorCodes.XPackNotFound,
                        "Map " + full + " is geen XPack: Description.xml, Objects en Materials ontbreken.");
                return full;
            }

            var dirs = xpacksDirs.ToList();
            bool looksLikePath = Path.IsPathRooted(trimmed) || trimmed.IndexOfAny(new[] { '\\', '/' }) >= 0;
            if (!looksLikePath)
            {
                var match = List(dirs).FirstOrDefault(e => string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match.Folder;
            }

            var available = List(dirs).Select(e => e.Name).Take(20).ToList();
            throw new XpkException(ErrorCodes.XPackNotFound,
                (looksLikePath ? "Map bestaat niet: " + trimmed : "XPack '" + trimmed + "' niet gevonden in " + string.Join("; ", dirs)) +
                (available.Count > 0 ? ". Beschikbaar: " + string.Join(", ", available) : "."));
        }

        private static DateTime LastModifiedUtc(string folder)
        {
            try
            {
                return new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)
                    .Select(f => f.LastWriteTimeUtc)
                    .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder))
                    .Max();
            }
            catch (IOException)
            {
                return Directory.GetLastWriteTimeUtc(folder);
            }
        }
    }
}
