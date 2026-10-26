using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xpk.Core.Session
{
    /// <summary>
    /// Checks an XPack folder before RaceBits opens it: RaceBits runs on .NET Framework and fails on paths of 260+
    /// characters, and OneDrive files that are only online would be downloaded (or fail) halfway through loading.
    /// </summary>
    public static class FolderCheck
    {
        /// <summary>Longest full file path RaceBits can handle (MAX_PATH minus the terminating null).</summary>
        public const int MaxPathLength = 259;

        // FILE_ATTRIBUTE_RECALL_ON_OPEN and FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS: cloud placeholders (OneDrive "online only").
        private const FileAttributes RecallOnOpen = (FileAttributes)0x40000;
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x400000;

        /// <summary>The longest path above the limit, or null.</summary>
        public static string FindTooLong(IEnumerable<string> paths) =>
            paths.Where(p => p.Length > MaxPathLength).OrderByDescending(p => p.Length).FirstOrDefault();

        public static bool IsNotLocal(FileAttributes attributes) =>
            (attributes & (FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0;

        /// <summary>Throws PATH_TOO_LONG or FILE_NOT_LOCAL.</summary>
        public static void Ensure(string folder)
        {
            List<FileInfo> files;
            try
            {
                files = new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
            }
            catch (PathTooLongException ex)
            {
                throw new XpkException(ErrorCodes.PathTooLong,
                    "Een pad in " + folder + " is langer dan " + MaxPathLength + " tekens. Verplaats de XPack naar een kortere map.", ex);
            }

            string tooLong = FindTooLong(files.Select(f => f.FullName));
            if (tooLong != null)
                throw new XpkException(ErrorCodes.PathTooLong,
                    "Pad van " + tooLong.Length + " tekens; RaceBits kan maximaal " + MaxPathLength + " aan: " + tooLong +
                    ". Verplaats de XPack naar een kortere map.");

            var notLocal = files.Where(f => IsNotLocal(f.Attributes)).Select(f => f.FullName).ToList();
            if (notLocal.Count > 0)
                throw new XpkException(ErrorCodes.FileNotLocal,
                    notLocal.Count + " bestand(en) staan alleen online (OneDrive), bijvoorbeeld " + notLocal[0] +
                    ". Kies in Verkenner 'Altijd behouden op dit apparaat' voor de XPack-map.");
        }
    }
}
