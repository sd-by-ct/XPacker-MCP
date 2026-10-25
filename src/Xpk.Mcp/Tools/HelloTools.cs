using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Xpk.Mcp.Tools
{
    /// <summary>Phase 0 smoke test for the MCP SDK on net48. Removed when the real tools arrive in phase 3.</summary>
    public static class HelloTools
    {
        [McpServerTool(Name = "hello", ReadOnly = true)]
        [Description("Returns a greeting. Used to check that the xpk MCP server is reachable.")]
        public static string Hello([Description("Name to greet.")] string name) =>
            "Hello " + name + ", xpk MCP server is running.";
    }
}
