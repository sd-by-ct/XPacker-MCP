using System;
using System.Linq;
using Xpk.Core;
using Xpk.Core.Model;
using Xpk.Core.Props;
using Xpk.Core.Session;
using Xunit;

namespace Xpk.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class PropertyMapIntegrationTests
    {
        public PropertyMapIntegrationTests() => Fixture.Env();   // RaceBits types need the AssemblyResolve handler

        [Fact]
        public void Object_HasCoreFields()
        {
            var names = PropertyMap.For(ItemKind.Object).Select(d => d.Name).ToList();

            foreach (string n in new[] { "Render", "Shadow", "ShadowCast", "LODIn", "LODOut", "Purpose", "Collisions", "Driveable" })
                Assert.Contains(n, names);
            Assert.True(PropertyMap.Find(ItemKind.Object, "shadow").ValueType.IsEnum);
            Assert.Equal(typeof(float), PropertyMap.Find(ItemKind.Object, "LODOut").ValueType);
        }

        [Fact]
        public void Find_Unknown_UNKNOWN_PROPERTY_ListsValidNames()
        {
            var ex = Assert.Throws<XpkException>(() => PropertyMap.Find(ItemKind.Object, "Schaduw"));

            Assert.Equal(ErrorCodes.UnknownProperty, ex.Code);
            Assert.Contains("LODOut", ex.Message);
        }

        [Fact]
        public void AcRelevant_ByNameRule()
        {
            Assert.False(PropertyMap.Find(ItemKind.Material, "rF_SpecularPower").AcRelevant);
            Assert.False(PropertyMap.Find(ItemKind.Material, "rf_Roughness").AcRelevant);
            Assert.True(PropertyMap.Find(ItemKind.Material, "AC_Ambient").AcRelevant);
            Assert.True(PropertyMap.Find(ItemKind.Material, "GrassFX_Size").AcRelevant);
            Assert.True(PropertyMap.Find(ItemKind.Material, "ShaderType").AcRelevant);
            Assert.True(PropertyMap.Find(ItemKind.Material, "PerlinBias").AcRelevant);
            Assert.False(PropertyMap.Find(ItemKind.Object, "UseCollisionColumn").AcRelevant);
            Assert.True(PropertyMap.Find(ItemKind.Object, "Shadow").AcRelevant);
            Assert.True(PropertyMap.For(ItemKind.StringObject).All(d => d.AcRelevant));
        }

        [Fact]
        public void Texture_HasNoWritableFields()
        {
            Assert.Empty(PropertyMap.For(ItemKind.Texture));
        }

        [Fact]
        public void AllDefs_GetOnFixture_ReturnsValues()
        {
            string folder = Fixture.FreshCopy(nameof(AllDefs_GetOnFixture_ReturnsValues));
            var pack = XPackSession.Open(Fixture.Env(), folder).Pack;

            var obj = pack.Objects.First(o => o.Path.EndsWith(@"Trees\Tree01", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(500f, PropertyMap.Find(ItemKind.Object, "LODOut").Get(obj));
            foreach (var def in PropertyMap.For(ItemKind.Object)) def.Get(obj);
            foreach (var def in PropertyMap.For(ItemKind.StringObject)) def.Get(pack.SObjects[0]);
            foreach (var def in PropertyMap.For(ItemKind.Material)) def.Get(pack.Materials[0]);
        }

        [Fact]
        public void Set_ChangesInMemoryOnly()
        {
            string folder = Fixture.FreshCopy(nameof(Set_ChangesInMemoryOnly));
            var before = Fixture.Hashes(folder);
            var pack = XPackSession.Open(Fixture.Env(), folder).Pack;
            var obj = pack.Objects[0];
            var lodOut = PropertyMap.Find(ItemKind.Object, "LODOut");

            lodOut.Set(obj, PropertyMap.ParseValue(lodOut, "123,5"));

            Assert.Equal(123.5f, obj.LODOut);
            Assert.Equal(before, Fixture.Hashes(folder));
        }
    }
}
