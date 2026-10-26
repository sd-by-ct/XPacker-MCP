using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using Xpk.Core;
using Xpk.Core.Props;
using Xunit;

namespace Xpk.Tests.Unit
{
    public class PropertyMapTests
    {
        private enum TestShadows { None, Caster, Object }

        private static readonly PropertyDef FloatDef = new PropertyDef("LODOut", typeof(float), true, null, null);
        private static readonly PropertyDef IntDef = new PropertyDef("Count", typeof(int), true, null, null);
        private static readonly PropertyDef BoolDef = new PropertyDef("Render", typeof(bool), true, null, null);
        private static readonly PropertyDef EnumDef = new PropertyDef("Shadow", typeof(TestShadows), true, null, null);
        private static readonly PropertyDef StringDef = new PropertyDef("Description", typeof(string), true, null, null);

        private static T WithCulture<T>(string culture, Func<T> f)
        {
            var old = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            try { return f(); }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }

        private static string Code(Action a) => Assert.Throws<XpkException>(a).Code;

        [Fact]
        public void ParseValue_DecimalPoint_UnderDutchCulture()
        {
            Assert.Equal(0.6f, WithCulture("nl-NL", () => PropertyMap.ParseValue(FloatDef, "0.6")));
            Assert.Equal(-1.629207E-07f, WithCulture("nl-NL", () => PropertyMap.ParseValue(FloatDef, "-1.629207E-07")));
        }

        [Fact]
        public void ParseValue_Comma_Accepted()
        {
            Assert.Equal(0.6f, WithCulture("nl-NL", () => PropertyMap.ParseValue(FloatDef, "0,6")));
            Assert.Equal(0.6f, WithCulture("en-US", () => PropertyMap.ParseValue(FloatDef, "0,6")));
            Assert.Equal(300f, PropertyMap.ParseValue(FloatDef, "300"));
        }

        [Theory]
        [InlineData("1.000,5")]
        [InlineData("1,000.5")]
        [InlineData("1.000.000")]
        [InlineData("1,2,3")]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void ParseValue_Ambiguous_Rejected(string raw)
        {
            Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(FloatDef, raw)));
        }

        [Fact]
        public void ParseValue_Int_RejectsFraction()
        {
            Assert.Equal(12, PropertyMap.ParseValue(IntDef, "12"));
            Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(IntDef, "1,5")));
        }

        [Fact]
        public void ParseValue_EnumByName_CaseInsensitive()
        {
            Assert.Equal(TestShadows.Caster, PropertyMap.ParseValue(EnumDef, "caster"));
            Assert.Equal(TestShadows.None, PropertyMap.ParseValue(EnumDef, " NONE "));
            var ex = Assert.Throws<XpkException>(() => PropertyMap.ParseValue(EnumDef, "Schaduw"));
            Assert.Equal(ErrorCodes.InvalidValue, ex.Code);
            Assert.Contains("None, Caster, Object", ex.Message);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("Caster,Object")]
        public void ParseValue_EnumNumber_Rejected(string raw)
        {
            Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(EnumDef, raw)));
        }

        [Fact]
        public void ParseValue_Bool_And_String()
        {
            Assert.Equal(true, PropertyMap.ParseValue(BoolDef, "True"));
            Assert.Equal(false, PropertyMap.ParseValue(BoolDef, "false"));
            Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(BoolDef, "ja")));
            Assert.Equal("Bomen 0,5", PropertyMap.ParseValue(StringDef, "Bomen 0,5"));
        }

        [Fact]
        public void ParseValue_Json_NumberAndString()
        {
            using (var doc = JsonDocument.Parse("{\"a\":300.5,\"b\":\"0,6\",\"c\":true,\"d\":\"Caster\",\"e\":\"x\",\"f\":7}"))
            {
                var root = doc.RootElement;
                Assert.Equal(300.5f, PropertyMap.ParseValue(FloatDef, root.GetProperty("a")));
                Assert.Equal(0.6f, PropertyMap.ParseValue(FloatDef, root.GetProperty("b")));
                Assert.Equal(true, PropertyMap.ParseValue(BoolDef, root.GetProperty("c")));
                Assert.Equal(TestShadows.Caster, PropertyMap.ParseValue(EnumDef, root.GetProperty("d")));
                Assert.Equal(7, PropertyMap.ParseValue(IntDef, root.GetProperty("f")));
                Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(BoolDef, root.GetProperty("e"))));
                Assert.Equal(ErrorCodes.InvalidValue, Code(() => PropertyMap.ParseValue(StringDef, root.GetProperty("a"))));
            }
        }

        [Fact]
        public void FormatValue_Invariant()
        {
            Assert.Equal("0.6", WithCulture("nl-NL", () => PropertyMap.FormatValue(0.6f)));
            Assert.Equal("Caster", PropertyMap.FormatValue(TestShadows.Caster));
            Assert.Equal("True", PropertyMap.FormatValue(true));
        }
    }
}
