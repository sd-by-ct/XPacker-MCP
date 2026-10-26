using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xpk.Cli.Output
{
    /// <summary>
    /// The JSON envelope shared with the MCP tools: { ok, data, warnings[], error: { code, message } }.
    /// snake_case names; property names inside "fields" stay as in RaceBits (e.g. LODOut).
    /// </summary>
    internal static class JsonOut
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() },
        };

        public static void Ok(TextWriter w, object data, IEnumerable<string> warnings = null) =>
            Write(w, new Envelope { Ok = true, Data = data, Warnings = new List<string>(warnings ?? new string[0]) });

        public static void Error(TextWriter w, string code, string message) =>
            Write(w, new Envelope { Ok = false, Warnings = new List<string>(), Error = new ErrorInfo { Code = code, Message = message } });

        private static void Write(TextWriter w, Envelope e) => w.WriteLine(JsonSerializer.Serialize(e, Options));

        private sealed class Envelope
        {
            public bool Ok { get; set; }
            public object Data { get; set; }
            public List<string> Warnings { get; set; }
            public ErrorInfo Error { get; set; }
        }

        private sealed class ErrorInfo
        {
            public string Code { get; set; }
            public string Message { get; set; }
        }
    }
}
