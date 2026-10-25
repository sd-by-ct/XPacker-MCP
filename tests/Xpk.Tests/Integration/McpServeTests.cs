using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class McpServeTests
    {
        private static string XpkExe =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\..\out\xpk.exe"));

        private static Task<McpClient> StartServer() =>
            McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "xpk",
                Command = XpkExe,
                Arguments = new[] { "serve" },
            }));

        [Fact]
        public async Task Serve_ToolsList_ContainsHello()
        {
            Assert.True(File.Exists(XpkExe), XpkExe);
            await using (var client = await StartServer())
            {
                var tools = await client.ListToolsAsync();

                Assert.Contains(tools, t => t.Name == "hello");
            }
        }

        [Fact]
        public async Task Serve_CallHello_ReturnsGreeting()
        {
            await using (var client = await StartServer())
            {
                var result = await client.CallToolAsync("hello", new Dictionary<string, object> { ["name"] = "Jeroen" });

                Assert.NotEqual(true, result.IsError);
                string text = result.Content.OfType<TextContentBlock>().Single().Text;
                Assert.Contains("Jeroen", text);
            }
        }
    }
}
