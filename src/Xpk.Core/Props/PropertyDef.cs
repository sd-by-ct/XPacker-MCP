using System;
using System.Reflection;

namespace Xpk.Core.Props
{
    /// <summary>One whitelisted, scalar property of an item kind (bool, int, float, string or enum).</summary>
    public sealed class PropertyDef
    {
        private readonly Func<object, object> _get;
        private readonly Action<object, object> _set;

        public PropertyDef(string name, Type valueType, bool acRelevant, Func<object, object> get, Action<object, object> set)
        {
            Name = name;
            ValueType = valueType;
            AcRelevant = acRelevant;
            _get = get;
            _set = set;
        }

        public string Name { get; }

        public Type ValueType { get; }

        /// <summary>Whether Assetto Corsa uses the property; see the rule in PropertyMap.</summary>
        public bool AcRelevant { get; }

        public object Get(object item) => _get(item);

        /// <summary>Changes the item in memory only; writing to disk is the Writer's job (phase 2).</summary>
        public void Set(object item, object value) => _set(item, value);

        /// <summary>Builds a def for a public instance property or field of <paramref name="owner"/>.</summary>
        internal static PropertyDef ForMember(Type owner, string name, bool acRelevant)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var prop = owner.GetProperty(name, flags);
            if (prop != null)
            {
                EnsureSupported(owner, name, prop.PropertyType);
                if (prop.GetGetMethod() == null || prop.GetSetMethod() == null)
                    throw new InvalidOperationException(owner.Name + "." + name + " is niet lees- en schrijfbaar.");
                return new PropertyDef(name, prop.PropertyType, acRelevant, item => prop.GetValue(item, null), (item, v) => prop.SetValue(item, v, null));
            }

            var field = owner.GetField(name, flags);
            if (field != null)
            {
                EnsureSupported(owner, name, field.FieldType);
                return new PropertyDef(name, field.FieldType, acRelevant, field.GetValue, field.SetValue);
            }

            throw new InvalidOperationException(owner.Name + "." + name + " bestaat niet in deze RaceBits.dll.");
        }

        private static void EnsureSupported(Type owner, string name, Type type)
        {
            if (!(type.IsEnum || type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(string)))
                throw new InvalidOperationException(owner.Name + "." + name + " heeft type " + type.Name + "; alleen scalaire typen horen in de whitelist.");
        }
    }
}
