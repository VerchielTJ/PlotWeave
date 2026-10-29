using System;
using System.Collections.Generic;
using System.Linq;

namespace PlotWeave.Legacy
{
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class ChineseEnumAttribute : Attribute
    { public Type ChineseType { get; } public ChineseEnumAttribute(Type type) { ChineseType = type; } }
    /// <summary>显式类型注册支持旧静态枚举，不扫描程序集，不携带原游戏资源枚举。</summary>
    public static class EnumMappingUtility
    {
        private static readonly Dictionary<Type, Type> Chinese = new Dictionary<Type, Type>();
        private static readonly Dictionary<Type, Type> English = new Dictionary<Type, Type>();
        public static void Register(Type english, Type chinese)
        {
            if (english == null || chinese == null || !english.IsEnum || !chinese.IsEnum) throw new ArgumentException("映射两端必须为枚举");
            var from = Enum.GetValues(english).Cast<Enum>().Select(Convert.ToInt64).ToArray(); var to = Enum.GetValues(chinese).Cast<Enum>().Select(Convert.ToInt64).ToArray();
            if (from.Distinct().Count() != from.Length || to.Distinct().Count() != to.Length || !from.OrderBy(n => n).SequenceEqual(to.OrderBy(n => n))) throw new ArgumentException("枚举映射需要相同且唯一的底层数值，不能按排列顺序猜测");
            if ((Chinese.TryGetValue(english, out var old) && old != chinese) || (English.TryGetValue(chinese, out var reverse) && reverse != english)) throw new InvalidOperationException("枚举类型已有不同映射");
            Chinese[english] = chinese; English[chinese] = english;
        }
        public static Type GetChineseType(Type english)
        {
            if (!Chinese.TryGetValue(english, out var type))
            { var attribute = (ChineseEnumAttribute)Attribute.GetCustomAttribute(english, typeof(ChineseEnumAttribute)); if (attribute == null) throw new KeyNotFoundException("缺少中文枚举映射"); Register(english, attribute.ChineseType); type = attribute.ChineseType; }
            return type;
        }
        public static Type GetEnglishType(Type chinese) => English.TryGetValue(chinese, out var type) ? type : throw new KeyNotFoundException("缺少英文枚举映射，请先Register");
        public static Enum GetChineseValue(Enum value)
        { if (value == null || !Enum.IsDefined(value.GetType(), value)) throw new ArgumentException("枚举值未定义"); return (Enum)Enum.ToObject(GetChineseType(value.GetType()), Convert.ToInt64(value)); }
        public static bool GetEnglishValue(Enum value, out Enum result)
        { result = null; if (value == null || !Enum.IsDefined(value.GetType(), value) || !English.TryGetValue(value.GetType(), out var type)) return false; result = (Enum)Enum.ToObject(type, Convert.ToInt64(value)); return true; }
        public static TChinese GetChineseValue<TEnum, TChinese>(TEnum value) where TEnum : Enum where TChinese : Enum
        { var result = GetChineseValue(value); if (result.GetType() != typeof(TChinese)) throw new ArgumentException("中文枚举类型不匹配"); return (TChinese)result; }
        public static TEnum GetEnglishValue<TEnum, TChinese>(TChinese value) where TEnum : Enum where TChinese : Enum
        { if (GetChineseType(typeof(TEnum)) != typeof(TChinese) || !GetEnglishValue(value, out var result)) throw new ArgumentException("英文枚举类型或值不匹配"); return (TEnum)result; }
        private static bool FindName(IEnumerable<Type> types, string name, out Enum result)
        {
            result = null; if (string.IsNullOrEmpty(name)) return false;
            var candidates = types.Distinct().Where(t => Enum.IsDefined(t, name)).ToArray();
            // 不用字典遍历顺序解决同名歧义；调用者可使用带类型的泛型API。
            if (candidates.Length != 1) return false; result = (Enum)Enum.Parse(candidates[0], name); return true;
        }
        public static bool GetChineseEnumValue(string name, out Enum value) => FindName(Chinese.Values, name, out value);
        public static bool GetEnglishEnumValue(string name, out Enum value) => FindName(Chinese.Keys, name, out value);
    }
}
