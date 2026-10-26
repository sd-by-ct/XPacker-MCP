using System;
using System.Linq;

namespace Xpk.Core.Env
{
    /// <summary>
    /// Decides whether xpk may write with the loaded RaceBits.dll. FileVersion is always 1.0.0.0, so only the SHA-256
    /// identifies a build. An unknown build (e.g. after an RTB update) is read-only unless AllowUnknownRaceBits is set.
    /// </summary>
    public sealed class VersionGuard
    {
        private VersionGuard() { }

        public bool IsReadOnly { get; private set; }

        /// <summary>Why xpk is read-only (Dutch, for the user); null when writable.</summary>
        public string Reason { get; private set; }

        /// <summary>Set when writing is allowed with an unknown build.</summary>
        public string Warning { get; private set; }

        public static VersionGuard Check(RtbEnvironment env, XpkConfig cfg) => Check(env.RaceBitsSha256, cfg);

        public static VersionGuard Check(string raceBitsSha256, XpkConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            bool known = (cfg.KnownRaceBitsSha256 ?? new string[0])
                .Any(h => string.Equals(h?.Trim(), raceBitsSha256, StringComparison.OrdinalIgnoreCase));
            if (known) return new VersionGuard();

            string shortHash = (raceBitsSha256 ?? "").Length >= 8 ? raceBitsSha256.Substring(0, 8) : raceBitsSha256;
            if (cfg.AllowUnknownRaceBits)
                return new VersionGuard
                {
                    Warning = "Onbekende RaceBits.dll (" + shortHash + "…); schrijven is toegestaan door allowUnknownRaceBits in de config.",
                };

            return new VersionGuard
            {
                IsReadOnly = true,
                Reason = "Onbekende RaceBits.dll (" + shortHash + "…), waarschijnlijk na een RTB-update. xpk is alleen-lezen " +
                         "tot de integratietests groen zijn en de hash in KnownRaceBitsSha256 staat.",
            };
        }
    }
}
