using Xpk.Core;
using Xpk.Core.Select;
using Xunit;

namespace Xpk.Tests.Unit
{
    public class SelectorTests
    {
        [Fact]
        public void Folder_DoesNotMatchSiblingWithSamePrefix()
        {
            var sel = Selector.Parse("Bomen");

            Assert.False(sel.Matches(@"Bomen2\X"));
            Assert.False(sel.Matches("Bomen2"));
        }

        [Fact]
        public void Folder_MatchesItselfAndDescendants()
        {
            var sel = Selector.Parse(@"Bomen\");

            Assert.True(sel.Matches("Bomen"));
            Assert.True(sel.Matches(@"Bomen\Eik_01"));
            Assert.True(sel.Matches(@"Bomen\Loof\Eik_01"));
            Assert.False(sel.Matches(@"Struiken\Bomen"));
        }

        [Fact]
        public void Star_MatchesOneLevel()
        {
            var sel = Selector.Parse(@"Bomen\*");

            Assert.True(sel.Matches(@"Bomen\Eik_01"));
            Assert.False(sel.Matches(@"Bomen\Loof\Eik_01"));
            Assert.False(sel.Matches("Bomen"));
            Assert.True(Selector.Parse(@"Bomen\Eik_*").Matches(@"Bomen\Eik_01"));
            Assert.False(Selector.Parse(@"Bomen\Eik_*").Matches(@"Bomen\Den_01"));
        }

        [Fact]
        public void DoubleStar_MatchesDeep()
        {
            var sel = Selector.Parse(@"Kerbs\**");

            Assert.True(sel.Matches(@"Kerbs\Rood"));
            Assert.True(sel.Matches(@"Kerbs\A\B\Rood"));
            Assert.False(sel.Matches(@"Kerbs2\Rood"));
            Assert.True(Selector.Parse(@"**\Tree0*").Matches(@"Vegetation\Large Trees\Tree04_trunk"));
            Assert.True(Selector.Parse(@"**\Tree0*").Matches("Tree01"));
        }

        [Fact]
        public void Slash_And_Backslash_Equivalent()
        {
            Assert.True(Selector.Parse("Kerbs/**").Matches(@"Kerbs\A\Rood"));
            Assert.True(Selector.Parse(@"Kerbs\**").Matches("Kerbs/A/Rood"));
            Assert.True(Selector.Parse("/Kerbs/A/").Matches(@"Kerbs\A\Rood"));
        }

        [Fact]
        public void Regex_Prefix()
        {
            var sel = Selector.Parse(@"re:^Ripple\\(Pyramid|Saw)\\.*_Red$");

            Assert.True(sel.Matches(@"Ripple\Saw\Left02_Red"));
            Assert.True(sel.Matches("Ripple/Pyramid/01_Red"));
            Assert.False(sel.Matches(@"Ripple\Saw\Left02_White"));
            Assert.True(Selector.Parse("re:trunk").Matches(@"Vegetation\Large Trees\Tree04_trunk_shadow"));
        }

        [Fact]
        public void Regex_Invalid_INVALID_VALUE()
        {
            var ex = Assert.Throws<XpkException>(() => Selector.Parse("re:(open"));

            Assert.Equal(ErrorCodes.InvalidValue, ex.Code);
        }

        [Fact]
        public void CaseInsensitive()
        {
            Assert.True(Selector.Parse(@"bomen\EIK_*").Matches(@"Bomen\eik_01"));
            Assert.True(Selector.Parse("RE:^bomen").Matches(@"Bomen\Eik"));
        }

        [Fact]
        public void Empty_MatchesAll()
        {
            Assert.True(Selector.Parse(null).Matches(@"A\B"));
            Assert.True(Selector.Parse("  ").Matches("A"));
            Assert.True(Selector.All.Matches("A"));
        }

        [Fact]
        public void RegexCharsInPlainPattern_AreLiteral()
        {
            Assert.True(Selector.Parse(@"Bench_FBX(1)").Matches("Bench_FBX(1)"));
            Assert.False(Selector.Parse("Tree0.").Matches("Tree01"));
        }
    }
}
