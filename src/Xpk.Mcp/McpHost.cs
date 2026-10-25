using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xpk.Mcp.Tools;

namespace Xpk.Mcp
{
    /// <summary>
    /// Runs the MCP server over stdio. Stdout is reserved for protocol messages: the caller must redirect
    /// Console.Out to stderr before anything else runs; the transport writes to the raw stdout stream.
    /// </summary>
    public static class McpHost
    {
        public static async Task RunAsync(CancellationToken cancellationToken)
        {
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation
                {
                    Name = "xpk",
                    Version = typeof(McpHost).Assembly.GetName().Version.ToString(),
                },
                ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(),
            };
            foreach (var method in typeof(HelloTools).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() != null)
                    options.ToolCollection.Add(McpServerTool.Create(method));
            }

            var transport = new StdioServerTransport("xpk");
            await using (var server = McpServer.Create(transport, options))
            {
                await server.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
