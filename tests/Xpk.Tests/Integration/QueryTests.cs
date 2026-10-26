using System;
using System.Linq;
using Xpk.Core;
using Xpk.Core.Model;
using Xpk.Core.Queries;
using Xpk.Core.Select;
using Xpk.Core.Session;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class QueryTests
    {
        private static XPackSession Open(string testName) => XPackSession.Open(Fixture.Env(), Fixture.FreshCopy(testName));

        [Fact]
        public void ListObjects_WithSelector_ReturnsOnlyMatches()
        {
            var s = Open(nameof(ListObjects_WithSelector_ReturnsOnlyMatches));

            var page = Query.List(s, ItemKind.Object, Selector.Parse(@"Ripple\Saw"), new[] { "LODOut", "shadow" });

            Assert.Equal(4, page.Total);
            Assert.Equal(new[] { @"Ripple\Saw\Left02_Red", @"Ripple\Saw\Left02_White", @"Ripple\Saw\Right02_Red", @"Ripple\Saw\Right02_White" },
                page.Items.Select(i => i.Path));
            Assert.Equal(new[] { "LODOut", "Shadow" }, page.Items[0].Fields.Keys);
            Assert.Equal(300f, page.Items[0].Fields["LODOut"]);
            Assert.Null(page.NextOffset);
        }

        [Fact]
        public void ListObjects_Paging_TotalAndNextOffset()
        {
            var s = Open(nameof(ListObjects_Paging_TotalAndNextOffset));

            var first = Query.List(s, ItemKind.Object, null, null, offset: 0, limit: 10);
            var last = Query.List(s, ItemKind.Object, null, null, offset: 20, limit: 10);

            Assert.Equal(28, first.Total);
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(10, first.NextOffset);
            Assert.Equal(8, last.Items.Count);
            Assert.Null(last.NextOffset);
            Assert.Empty(first.Items.Select(i => i.Path).Intersect(last.Items.Select(i => i.Path)));
        }

        [Fact]
        public void List_UnknownField_UNKNOWN_PROPERTY()
        {
            var s = Open(nameof(List_UnknownField_UNKNOWN_PROPERTY));

            var ex = Assert.Throws<XpkException>(() => Query.List(s, ItemKind.Object, null, new[] { "Schaduw" }));

            Assert.Equal(ErrorCodes.UnknownProperty, ex.Code);
        }

        [Fact]
        public void GetObject_ComplexTree_HasChildren()
        {
            var s = Open(nameof(GetObject_ComplexTree_HasChildren));

            var info = Query.Get(s, ItemKind.Object, "vegetation/large trees/tree04_complex");

            Assert.Equal(@"Vegetation\Large Trees\Tree04_complex", info.Path);
            Assert.Equal(new[] { @"Vegetation\Large Trees\Tree04_trunk", @"Vegetation\Large Trees\Tree04_LOD" }, info.Children.Select(c => c.Object));
            Assert.Equal(3, info.Children[0].Scale.Length);
            Assert.IsType<string>(info.Fields["Shadow"]);
        }

        [Fact]
        public void GetObject_Unknown_NOT_FOUND()
        {
            var s = Open(nameof(GetObject_Unknown_NOT_FOUND));

            var ex = Assert.Throws<XpkException>(() => Query.Get(s, ItemKind.Object, @"Vegetation\Trees"));

            Assert.Equal(ErrorCodes.NotFound, ex.Code);
        }

        [Fact]
        public void GetObject_HasMaterials()
        {
            var s = Open(nameof(GetObject_HasMaterials));

            var info = Query.Get(s, ItemKind.Object, @"Vegetation\Trees\Tree01");

            Assert.Equal(new[] { @"Objects\Vegetation\Trees\Tree01" }, info.Materials);
        }

        [Fact]
        public void GetMaterial_HasTexMapsAndUsageCount()
        {
            var s = Open(nameof(GetMaterial_HasTexMapsAndUsageCount));

            var info = Query.Get(s, ItemKind.Material, @"Objects\Vegetation\Trees\Tree01");

            var map = Assert.Single(info.TexMaps);
            Assert.Equal("TexMap1", map.Slot);
            Assert.Equal(@"Objects\Vegetation\Trees\Tree01.dds", map.Texture);
            Assert.True(map.Exists);
            Assert.Equal(1, info.UsageCount);
            Assert.Equal(new[] { @"Vegetation\Trees\Tree01" }, info.UsedBy);
            Assert.Empty(info.MissingRequiredTextures);
            Assert.True(info.Fields.ContainsKey("ShaderType"));
        }

        [Fact]
        public void GetSObject_HasObjects()
        {
            var s = Open(nameof(GetSObject_HasObjects));

            var info = Query.Get(s, ItemKind.StringObject, @"Ripple\Pyramid1");

            Assert.Equal(new[] { @"Ripple\Pyramid\01_Blue", @"Ripple\Pyramid\01_Red", @"Ripple\Pyramid\01_White1" }, info.Objects);
        }

        [Fact]
        public void Textures_KerbVariants_DifferentMd5()
        {
            var s = Open(nameof(Textures_KerbVariants_DifferentMd5));

            var page = Query.List(s, ItemKind.Texture, Selector.Parse("re:(^|\\\\)ripple001\\.dds$"), null);

            Assert.Equal(2, page.Total);
            var a = page.Items[0];
            var b = page.Items[1];
            Assert.NotEqual(a.Md5, b.Md5);
            Assert.Equal(32, a.Md5.Length);
            Assert.All(page.Items, t =>
            {
                Assert.True(t.Width > 0 && t.Height > 0, t.Path);
                Assert.StartsWith("DDS", t.Format);
                Assert.True(t.Bytes > 0);
            });
        }

        [Fact]
        public void Texture_UsedBy_Materials()
        {
            var s = Open(nameof(Texture_UsedBy_Materials));

            var info = Query.Get(s, ItemKind.Texture, @"Objects\Vegetation\Trees\Tree01.dds");

            Assert.Equal(new[] { @"Objects\Vegetation\Trees\Tree01" }, info.UsedBy);
        }

        [Fact]
        public void Query_DoesNotWrite()
        {
            string folder = Fixture.FreshCopy(nameof(Query_DoesNotWrite));
            var before = Fixture.Hashes(folder);
            var s = XPackSession.Open(Fixture.Env(), folder);

            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
                Query.List(s, kind, null, null, 0, 500);

            Assert.Equal(before, Fixture.Hashes(folder));
            Assert.False(s.ChangedOnDisk());
        }
    }
}
