using System;
using System.IO;
using System.Text.Json;

namespace Xpk.Core.Env
{
    /// <summary>
    /// Settings for xpk, read from %APPDATA%\xpk\config.json. Every field is optional; a missing file or field keeps the default.
    /// </summary>
    public sealed class XpkConfig
    {
        public const string DefaultRtbDir = @"C:\Program Files (x86)\Steam\steamapps\common\Race Track Builder";

        /// <summary>RaceBits.dll of RTB v2.0 (Steam branch v2000, build 26041913); see docs/racebits-api.md.</summary>
        public const string RaceBitsV2000Sha256 = "611efe142ff387bc17a6f0eb9c236d7d426a21b8f7c3fce2ce01c275a43694ff";

        public XpkConfig()
        {
            RtbDir = DefaultRtbDir;
            RtbProjectsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Race Track Builder");
            BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xpk", "backups");
            KnownRaceBitsSha256 = new[] { RaceBitsV2000Sha256 };
        }

        /// <summary>RTB install folder. Read-only: xpk never writes here.</summary>
        public string RtbDir { get; set; }

        /// <summary>RTB projects folder, passed to Globals.Initialize. Default: Documents\Race Track Builder (follows OneDrive redirection).</summary>
        public string RtbProjectsDir { get; set; }

        /// <summary>Backups before each write: &lt;BackupDir&gt;\&lt;xpack&gt;\&lt;yyyyMMdd-HHmmss&gt;\.</summary>
        public string BackupDir { get; set; }

        /// <summary>SHA-256 hashes of RaceBits.dll builds that passed the integration tests. Others make xpk read-only.</summary>
        public string[] KnownRaceBitsSha256 { get; set; }

        /// <summary>Allow writing with an unknown RaceBits.dll (after a successful test run).</summary>
        public bool AllowUnknownRaceBits { get; set; }

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "xpk", "config.json");

        public static XpkConfig Default() => new XpkConfig();

        public static XpkConfig Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) return Default();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            try
            {
                return JsonSerializer.Deserialize<XpkConfig>(File.ReadAllText(path), options) ?? Default();
            }
            catch (JsonException ex)
            {
                throw new XpkException(ErrorCodes.ConfigInvalid, "Configbestand " + path + " is geen geldige JSON: " + ex.Message, ex);
            }
        }
    }
}
