using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xpk.Core.Model;

namespace Xpk.Core.Props
{
    /// <summary>
    /// Whitelist of the properties xpk reads and (from phase 2) writes, per item kind, plus culture-independent
    /// parsing of user input. Names are the RaceBits member names, e.g. {"Shadow":"None","LODOut":300.5}.
    /// </summary>
    public static class PropertyMap
    {
        private static readonly Dictionary<ItemKind, IReadOnlyList<PropertyDef>> Cache = new Dictionary<ItemKind, IReadOnlyList<PropertyDef>>();

        public static IReadOnlyList<PropertyDef> For(ItemKind kind)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(kind, out var defs))
                {
                    defs = Build(kind);
                    Cache[kind] = defs;
                }
                return defs;
            }
        }

        /// <summary>Case-insensitive lookup; throws UNKNOWN_PROPERTY with the valid names.</summary>
        public static PropertyDef Find(ItemKind kind, string name)
        {
            var defs = For(kind);
            var def = defs.FirstOrDefault(d => string.Equals(d.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (def != null) return def;
            throw new XpkException(ErrorCodes.UnknownProperty,
                "Onbekende eigenschap '" + name + "' voor " + kind + ". Geldig: " + string.Join(", ", defs.Select(d => d.Name)));
        }

        // RaceBits types are only touched here, after RtbEnvironment registered its AssemblyResolve handler.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IReadOnlyList<PropertyDef> Build(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Object: return Defs(typeof(RaceBits.BObject), kind, ObjectNames);
                case ItemKind.StringObject: return Defs(typeof(RaceBits.SObject), kind, SObjectNames);
                case ItemKind.Material: return Defs(typeof(RaceBits.Mat), kind, MaterialNames);
                default: return new PropertyDef[0];   // textures have no editable properties
            }
        }

        private static IReadOnlyList<PropertyDef> Defs(Type owner, ItemKind kind, string[] names) =>
            names.Select(n => PropertyDef.ForMember(owner, n, IsAcRelevant(kind, n))).ToList();

        // AcRelevant is decided by name; there is no simulator table available. When in doubt: true.
        // (Decision Jeroen, 26 Oct 2026, docs/PLAN.md phase 1.)
        // - Material: rF_* and rf_* = false (rFactor 2). AC_*, GrassFX_* (CSP Grass FX), ShaderType,
        //   DefaultApplicationType, PerMeterCoverage, GameSurface, Purpose, TexMap1-9, Perlin* = true.
        // - Object: UseCollisionColumn/CollisionColumn (XML: UseRBRCollisionColumn/RBRCollisionColumn,
        //   Richard Burns Rally) = false, the rest true.
        // - String Object: rFactorVisGroup = false, the rest true.
        internal static bool IsAcRelevant(ItemKind kind, string name)
        {
            switch (kind)
            {
                case ItemKind.Material: return !name.StartsWith("rf_", StringComparison.OrdinalIgnoreCase);
                case ItemKind.Object: return name != "UseCollisionColumn" && name != "CollisionColumn";
                case ItemKind.StringObject: return name != "rFactorVisGroup";
                default: return true;
            }
        }

        // Scalars only. Not here (not scalar): Object.CollisionColumn (Vector3), SObject.rFactorVisGroup (class),
        // Mat colours, Mat.TexMap1-9 and the material purpose (Mat.PurposeContains/SetPurpose); Query shows those read-only.
        private static readonly string[] ObjectNames =
        {
            "Render", "Driveable", "Collisions", "Moveable", "ShadowReceiver", "ReceiveLighting", "AlwaysRender",
            "Shadow", "ShadowCast", "Purpose", "LODIn", "LODOut", "SeparationMinDistance", "Description", "Tags",
            "IsComplexObject", "UseCollisionColumn",
        };

        private static readonly string[] SObjectNames =
        {
            "SObjectType", "Description", "Render", "Driveable", "Collisions", "Moveable", "ShadowReceiver",
            "ReceiveLighting", "AlwaysRender", "Shadow", "ShadowQuality", "ShadowCast", "ShadowOnMoveables",
            "LODIn", "LODOut", "IdealLength", "SquishableLength", "UnsquishableLength",
        };

        private static readonly string[] MaterialNames =
        {
            "ShaderType", "DefaultApplicationType", "PerMeterCoverage", "GameSurface",
            "AC_GameSurface", "AC_Ambient", "AC_Diffuse", "AC_Specular", "AC_SpecularPower", "AC_AlphaRef",
            "AC_DetailUVMultiplier", "AC_DetailNormalBlend", "AC_MultR", "AC_MultG", "AC_MultB", "AC_MultA",
            "AC_MagicMult", "AC_TransparencyType", "AC_ksTree", "AC_Scale", "AC_Gain",
            "GrassFX_Size", "GrassFX_Width", "GrassFX_Cut", "GrassFX_MaskThreshold", "GrassFX_MaskRedThreshold",
            "GrassFX_MaskMinLuminance", "GrassFX_MaskMaxLuminance",
            "rF_TransparencyType", "rF_SpecularPower", "rF_rFactorMaterialName", "rF_KeeprFactorMaterialName",
            "rF_UpNormals", "rF_AnimationType", "rF_TerrainMixMapBias", "rF_TerrainPatternStrength",
            "rf_Specular", "rf_Roughness", "rf_NormalIntensity",
            "PerlinBias", "PerlinFrequency", "PerlinAmplitude", "PerlinExcludeEdge",
        };

        /// <summary>
        /// Parses user input, independent of the current culture. Numbers take a decimal point or a decimal comma
        /// ("0.6" and "0,6" are both 0.6); more than one separator ("1.000,5", "1,000.5") is ambiguous and refused.
        /// Enums by name only (case-insensitive). Throws INVALID_VALUE.
        /// </summary>
        public static object ParseValue(PropertyDef def, string raw)
        {
            Type t = def.ValueType;
            if (t == typeof(string)) return raw ?? "";

            string s = (raw ?? "").Trim();
            if (t == typeof(bool))
            {
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
                throw Invalid(def, raw, "true of false");
            }

            if (t.IsEnum)
            {
                string name = Enum.GetNames(t).FirstOrDefault(n => string.Equals(n, s, StringComparison.OrdinalIgnoreCase));
                if (name == null) throw Invalid(def, raw, "een van: " + string.Join(", ", Enum.GetNames(t)));
                return Enum.Parse(t, name);
            }

            int separators = s.Count(c => c == '.' || c == ',');
            if (separators > 1)
                throw Invalid(def, raw, "een getal met hoogstens één decimaalteken (punt of komma), zonder duizendtallen");
            s = s.Replace(',', '.');

            if (t == typeof(int))
            {
                if (int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i)) return i;
                throw Invalid(def, raw, "een geheel getal");
            }

            if (double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out double d) && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                if (t == typeof(float))
                {
                    if (Math.Abs(d) > float.MaxValue) throw Invalid(def, raw, "een getal binnen het bereik van float");
                    return (float)d;
                }
                if (t == typeof(double)) return d;
            }
            throw Invalid(def, raw, "een getal, bijvoorbeeld 0.6");
        }

        /// <summary>Parses a JSON value (MCP): numbers and booleans directly, strings via ParseValue(def, string).</summary>
        public static object ParseValue(PropertyDef def, JsonElement raw)
        {
            Type t = def.ValueType;
            switch (raw.ValueKind)
            {
                case JsonValueKind.String:
                    return ParseValue(def, raw.GetString());
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (t == typeof(bool)) return raw.GetBoolean();
                    break;
                case JsonValueKind.Number:
                    if (t == typeof(int) && raw.TryGetInt32(out int i)) return i;
                    if (t == typeof(float) || t == typeof(double)) return ParseValue(def, raw.GetRawText());
                    break;
            }
            throw Invalid(def, raw.GetRawText(), "een waarde van type " + TypeName(t));
        }

        /// <summary>Formats a value for output: numbers invariant ("0.6"), enums by name.</summary>
        public static string FormatValue(object value)
        {
            switch (value)
            {
                case null: return null;
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable fmt: return fmt.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        public static string TypeName(Type t) =>
            t.IsEnum ? "enum (" + string.Join(", ", Enum.GetNames(t)) + ")"
            : t == typeof(float) || t == typeof(double) ? "number"
            : t == typeof(int) ? "integer"
            : t == typeof(bool) ? "boolean"
            : "string";

        private static XpkException Invalid(PropertyDef def, string raw, string expected) =>
            new XpkException(ErrorCodes.InvalidValue, "Ongeldige waarde '" + raw + "' voor " + def.Name + ": verwacht " + expected + ".");
    }
}
