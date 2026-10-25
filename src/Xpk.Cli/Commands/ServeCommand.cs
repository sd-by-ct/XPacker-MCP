using System;
using System.Threading;
using Xpk.Mcp;

namespace Xpk.Cli.Commands
{
    /// <summary>xpk serve: MCP server over stdio. Stops when stdin closes or on Ctrl+C.</summary>
    internal static class ServeCommand
    {
        public static int Run()
        {
            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
                try
                {
                    McpHost.RunAsync(cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
            }
            return 0;
        }
    }
}
