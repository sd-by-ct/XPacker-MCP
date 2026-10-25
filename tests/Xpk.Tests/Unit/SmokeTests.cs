using Xunit;

namespace Xpk.Tests.Unit
{
    public class SmokeTests
    {
        [Fact]
        public void TestProject_Runs()
        {
            Assert.True(System.Environment.Is64BitProcess);
        }
    }
}
