using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml;

namespace Xpk.Core.Env
{
    /// <summary>
    /// Loads RaceBits.dll from the RTB folder and initializes it headless. One instance per process,
    /// because RaceBits.Globals is static.
    /// </summary>
    public sealed class RtbEnvironment
    {
        // Only these assemblies are resolved from the RTB folder; everything else (System.*) comes from the GAC.
        private static readonly string[] RtbAssemblies =
        {
            "RaceBits", "RaceFbx", "FbxWrapper", "RTBPlugins", "Serilog",
            "SharpDX", "SharpDX.Direct3D11", "SharpDX.DXGI", "SharpDX.D3DCompiler", "SharpDX.Direct2D1", "SharpDX.Direct3D11.Effects",
        };

        private static readonly object Gate = new object();
        private static RtbEnvironment _instance;
        private static string _resolveDir;

        private RtbEnvironment() { }

        public string RtbDir { get; private set; }
        public string ProjectsDir { get; private set; }
        public string RaceBitsPath { get; private set; }

        /// <summary>FileVersion of RaceBits.dll. RTB always reports 1.0.0.0, so the hash is what identifies a build.</summary>
        public string RaceBitsVersion { get; private set; }

        /// <summary>SHA-256 of RaceBits.dll, lower-case hex.</summary>
        public string RaceBitsSha256 { get; private set; }

        /// <summary>Folders that hold XPacks: the XPacker source folder (from XPacker's user.config) and RTB's zip folder.</summary>
        public IReadOnlyList<string> XPacksDirs { get; private set; }

        /// <summary>The headless Direct3D11 device handed to Globals.Initialize (SharpDX.Direct3D11.Device).</summary>
        public object Device { get; private set; }

        public static RtbEnvironment Initialize(XpkConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            lock (Gate)
            {
                if (_instance != null)
                {
                    if (!string.Equals(Path.GetFullPath(_instance.RtbDir), Path.GetFullPath(cfg.RtbDir), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("RtbEnvironment is already initialized with RTB folder " + _instance.RtbDir);
                    return _instance;
                }

                string rtbDir = Path.GetFullPath(cfg.RtbDir);
                string raceBits = Path.Combine(rtbDir, "RaceBits.dll");
                if (!File.Exists(raceBits))
                    throw new FileNotFoundException("RaceBits.dll niet gevonden in de RTB-map.", raceBits);

                _resolveDir = rtbDir;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveRtbAssembly;
                SetDllDirectory(rtbDir);

                var env = new RtbEnvironment
                {
                    RtbDir = rtbDir,
                    ProjectsDir = cfg.RtbProjectsDir,
                    RaceBitsPath = raceBits,
                    RaceBitsVersion = FileVersionInfo.GetVersionInfo(raceBits).FileVersion,
                    RaceBitsSha256 = Sha256(raceBits),
                };
                env.InitializeRaceBits();
                _instance = env;
                return env;
            }
        }

        // Kept separate and non-inlined so no RaceBits/SharpDX type is touched before AssemblyResolve is registered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeRaceBits()
        {
            SharpDX.Direct3D11.Device device;
            try
            {
                device = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware);
            }
            catch (SharpDX.SharpDXException)
            {
                device = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Warp);
            }
            Device = device;

            RaceBits.Globals.Initialize(device, RtbDir, ProjectsDir);

            var dirs = new List<string>();
            AddIfExists(dirs, ReadXPackerSourceFolder());
            AddIfExists(dirs, ReadStaticString(typeof(RaceBits.Globals), "UserXPacksFolder"));
            AddIfExists(dirs, Path.Combine(ProjectsDir ?? "", "XPacks"));
            XPacksDirs = dirs;
        }

        private static Assembly ResolveRtbAssembly(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (!RtbAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
            if (loaded != null) return loaded;

            string path = Path.Combine(_resolveDir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        // UserXPacksFolder exists in the v1950 build, not in v2000; read it by reflection so both builds work.
        private static string ReadStaticString(Type type, string property)
        {
            var p = type.GetProperty(property, BindingFlags.Public | BindingFlags.Static);
            return p?.GetValue(null, null) as string;
        }

        /// <summary>XPacker keeps its source folder in its user.config (setting XPacksFolder).</summary>
        private static string ReadXPackerSourceFolder()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaceTrackBuilder");
            if (!Directory.Exists(root)) return null;
            var config = Directory.GetDirectories(root, "XPacker.exe_*")
                .SelectMany(d => Directory.GetFiles(d, "user.config", SearchOption.AllDirectories))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (config == null) return null;
            try
            {
                var doc = new XmlDocument();
                doc.Load(config.FullName);
                return doc.SelectSingleNode("//setting[@name='XPacksFolder']/value")?.InnerText;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        private static void AddIfExists(List<string> dirs, string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
            string full = Path.GetFullPath(dir).TrimEnd('\\');
            if (!dirs.Contains(full, StringComparer.OrdinalIgnoreCase)) dirs.Add(full);
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var s = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(s).Select(b => b.ToString("x2")));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
