using System.Collections.Generic;
using System.IO;
using Xpk.Cli.Output;
using Xpk.Core.Env;
using Xpk.Core.Model;
using Xpk.Core.Session;

namespace Xpk.Cli.Commands
{
    /// <summary>What every command needs: parsed arguments, output writers and the RTB environment.</summary>
    internal sealed class CliContext
    {
        private XpkConfig _config;
        private RtbEnvironment _env;

        public CliContext(Args args, TextWriter output, TextWriter error)
        {
            Args = args;
            Out = output;
            Err = error;
        }

        public Args Args { get; }
        public TextWriter Out { get; }
        public TextWriter Err { get; }
        public bool Json => Args.Has("json");

        public XpkConfig Config => _config ?? (_config = XpkConfig.Load());

        public RtbEnvironment Env => _env ?? (_env = RtbEnvironment.Initialize(Config));

        public XPackSession OpenXPack() =>
            XPackSession.Open(Env, Args.Require("xpack"), VersionGuard.Check(Env, Config));

        public void Ok(object data, IEnumerable<string> warnings = null) => JsonOut.Ok(Out, data, warnings);

        public void WriteWarnings(IEnumerable<string> warnings)
        {
            foreach (string w in warnings) Err.WriteLine("Waarschuwing: " + w);
        }

        public static ItemKind ParseKind(string word)
        {
            switch ((word ?? "").ToLowerInvariant())
            {
                case "object":
                case "objects":
                    return ItemKind.Object;
                case "sobject":
                case "sobjects":
                case "stringobject":
                case "stringobjects":
                    return ItemKind.StringObject;
                case "material":
                case "materials":
                    return ItemKind.Material;
                case "texture":
                case "textures":
                    return ItemKind.Texture;
                default:
                    throw new UsageException("Onbekende soort '" + word + "'. Kies objects, sobjects, materials of textures.");
            }
        }
    }
}
