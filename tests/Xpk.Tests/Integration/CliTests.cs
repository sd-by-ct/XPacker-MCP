using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class CliTests
    {
        private static string XpkExe =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\..\out\xpk.exe"));

        private sealed class Result
        {
            public int ExitCode;
            public string Out;
            public string Err;
        }

        private static Result Run(params string[] args)
        {
            var psi = new ProcessStartInfo(XpkExe, string.Join(" ", args.Select(Quote)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string stdout = p.StandardOutput.ReadToEnd();
                Assert.True(p.WaitForExit(120000), "xpk.exe reageert niet");
                return new Result { ExitCode = p.ExitCode, Out = stdout, Err = err.Result };
            }
        }

        private static string Quote(string a) => a.IndexOf(' ') >= 0 || a.Length == 0 ? "\"" + a + "\"" : a;

        [Fact]
        public void Cli_ListJson_IsValidJson()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_ListJson_IsValidJson));

            var r = Run("list", "objects", "--xpack", folder, "--json");

            Assert.True(r.ExitCode == 0, r.Err);
            using (var doc = JsonDocument.Parse(r.Out))
            {
                var root = doc.RootElement;
                Assert.True(root.GetProperty("ok").GetBoolean());
                var data = root.GetProperty("data");
                Assert.Equal(28, data.GetProperty("total").GetInt32());
                Assert.Equal(28, data.GetProperty("items").GetArrayLength());
                var first = data.GetProperty("items")[0];
                Assert.Equal(JsonValueKind.Number, first.GetProperty("fields").GetProperty("LODOut").ValueKind);
                Assert.Equal(JsonValueKind.String, first.GetProperty("fields").GetProperty("Shadow").ValueKind);
                Assert.Equal(JsonValueKind.Array, root.GetProperty("warnings").ValueKind);
            }
        }

        [Fact]
        public void Cli_ListSelectFields_Text()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_ListSelectFields_Text));

            var r = Run("list", "objects", "--xpack", folder, "--select", @"Ripple\Saw\**", "--fields", "LODOut,Shadow");

            Assert.True(r.ExitCode == 0, r.Err);
            Assert.Contains(@"Ripple\Saw\Left02_Red", r.Out);
            Assert.DoesNotContain("Pyramid", r.Out);
            Assert.Contains("LODOut", r.Out);
            Assert.Contains("4 van 4", r.Out);
        }

        [Fact]
        public void Cli_UnknownXPack_JsonErrorCode()
        {
            var r = Run("info", "--xpack", "Bestaat-niet-" + Guid.NewGuid().ToString("N"), "--json");

            Assert.Equal(1, r.ExitCode);
            using (var doc = JsonDocument.Parse(r.Out))
            {
                Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal("XPACK_NOT_FOUND", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
            }
        }

        [Fact]
        public void Cli_UnknownField_ErrorText()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_UnknownField_ErrorText));

            var r = Run("list", "objects", "--xpack", folder, "--fields", "Schaduw");

            Assert.Equal(1, r.ExitCode);
            Assert.Contains("UNKNOWN_PROPERTY", r.Err);
            Assert.Contains("LODOut", r.Err);
        }

        [Fact]
        public void Cli_BadUsage_ExitCode2()
        {
            var r = Run("list", "dingen");

            Assert.Equal(2, r.ExitCode);
            Assert.Contains("Gebruik", r.Err);
        }

        [Fact]
        public void Cli_GetText_UsesDecimalPoint()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_GetText_UsesDecimalPoint));
            // The fixture material with AC_Ambient 0.2; Windows here runs with a Dutch culture (decimal comma).
            string material = Directory.GetFiles(Path.Combine(folder, "Materials"), "*.xml", SearchOption.AllDirectories)
                .First(f => File.ReadAllText(f).Contains("<AC_Ambient>0.2</AC_Ambient>"));
            string path = material.Substring(Path.Combine(folder, "Materials").Length + 1).Replace(".xml", "");

            var r = Run("get", "material", path, "--xpack", folder);

            Assert.True(r.ExitCode == 0, r.Err);
            Assert.Matches(new Regex(@"AC_Ambient\s+0\.2\b"), r.Out);
            Assert.DoesNotMatch(new Regex(@"\d,\d"), r.Out);
        }

        [Fact]
        public void Cli_GetObjectJson_HasChildren()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_GetObjectJson_HasChildren));

            var r = Run("get", "object", "Vegetation/Large Trees/Tree04_complex", "--xpack", folder, "--json");

            Assert.True(r.ExitCode == 0, r.Err);
            using (var doc = JsonDocument.Parse(r.Out))
                Assert.Equal(2, doc.RootElement.GetProperty("data").GetProperty("children").GetArrayLength());
        }

        [Fact]
        public void Cli_InfoJson_HasCountsAndHash()
        {
            string folder = Fixture.FreshCopy(nameof(Cli_InfoJson_HasCountsAndHash));

            var r = Run("info", "--xpack", folder, "--json");

            Assert.True(r.ExitCode == 0, r.Err);
            using (var doc = JsonDocument.Parse(r.Out))
            {
                var data = doc.RootElement.GetProperty("data");
                Assert.Equal(28, data.GetProperty("materials").GetInt32());
                Assert.StartsWith("611efe14", data.GetProperty("race_bits_sha256").GetString());
            }
        }

        [Fact]
        public void Cli_ListXPacks_Json()
        {
            var r = Run("list", "xpacks", "--json");

            Assert.True(r.ExitCode == 0, r.Err);
            using (var doc = JsonDocument.Parse(r.Out))
                Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        }
    }
}
