using System;
using System.IO;

namespace Xpk.Core.Env
{
    /// <summary>
    /// Settings for xpk. Phase 0 only holds the folders; loading from %APPDATA%\xpk\config.json follows in phase 1.
    /// </summary>
    public sealed class XpkConfig
    {
        public const string DefaultRtbDir = @"C:\Program Files (x86)\Steam\steamapps\common\Race Track Builder";

        /// <summary>RTB install folder. Read-only: xpk never writes here.</summary>
        public string RtbDir { get; set; }

        /// <summary>RTB projects folder, passed to Globals.Initialize. Default: Documents\Race Track Builder (follows OneDrive redirection).</summary>
        public string RtbProjectsDir { get; set; }

        public static XpkConfig Default() => new XpkConfig
        {
            RtbDir = DefaultRtbDir,
            RtbProjectsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Race Track Builder"),
        };
    }
}
