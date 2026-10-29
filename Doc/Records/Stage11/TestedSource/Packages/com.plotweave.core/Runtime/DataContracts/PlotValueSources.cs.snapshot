using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PlotWeave.Data
{
    /// <summary>统一读口；宿主可实现此接口，以直接访问配表且不使用反射。</summary>
    public interface IPlotValueSource
    {
        bool IsValid { get; }
        bool TryGetValue(string member, out object value);
    }
    public sealed class DictionaryPlotValueSource : IPlotValueSource
    {
        private readonly Dictionary<string, object> _data;
        public DictionaryPlotValueSource(IDictionary<string, object> data)
        { _data = data == null ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(data, StringComparer.Ordinal); }
        public bool IsValid => _data.Count > 0;
        public bool TryGetValue(string member, out object value) => _data.TryGetValue(member, out value);
    }
    /// <summary>兼容Luban等已编译对象的字段和属性，不依赖任何cfg命名空间。</summary>
    public sealed class ObjectPlotValueSource : IPlotValueSource
    {
        private readonly object _data;
        public ObjectPlotValueSource(object data) { _data = data; }
        public bool IsValid => _data != null;
        public bool TryGetValue(string member, out object value)
        {
            value = null;
            if (_data == null) return false;
            var type = _data.GetType();
            var property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0) { value = property.GetValue(_data); return true; }
            var field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return false;
            value = field.GetValue(_data); return true;
        }
    }
    public abstract class PlotDataAdapter : IPlotDataBase
    {
        protected readonly IPlotValueSource Source;
        public string DataSource { get; }
        public bool IsValid => Source.IsValid;
        protected PlotDataAdapter(IPlotValueSource source, string dataSource)
        { Source = source ?? throw new ArgumentNullException(nameof(source)); DataSource = dataSource; }
    }
    public static class PlotValueReader
    {
        public static IPlotValueSource Wrap(object value)
        {
            if (value is IPlotValueSource source) return source;
            if (value is IDictionary<string, object> data) return new DictionaryPlotValueSource(data);
            return new ObjectPlotValueSource(value);
        }
        public static T Value<T>(IPlotValueSource source, string key) => source.TryGetValue(key, out var raw) ? ConvertValue<T>(raw) : Default<T>();
        public static T Required<T>(IPlotValueSource source, string key)
        {
            if (!source.TryGetValue(key, out var raw) || raw == null) throw new KeyNotFoundException("缺少必需字段: " + key);
            return ConvertValue<T>(raw);
        }
        private static T Default<T>() => typeof(T) == typeof(string) ? (T)(object)"" : default;
        public static T ConvertValue<T>(object raw)
        {
            if (raw == null) return Default<T>();
            if (raw is T typed)
            {
                if (typed is float finite && (float.IsNaN(finite) || float.IsInfinity(finite))) throw new FormatException("数值必须有限");
                if (typeof(T).IsEnum && !Enum.IsDefined(typeof(T), typed)) throw new FormatException("枚举值未定义: " + typed);
                if (typed is Vector3 vector && (!Finite(vector.x) || !Finite(vector.y) || !Finite(vector.z))) throw new FormatException("Vector3分量必须有限");
                return typed;
            }
            var type = typeof(T);
            var text = raw is IConvertible ? Convert.ToString(raw, CultureInfo.InvariantCulture) : raw.ToString();
            object result;
            if (type.IsEnum)
            {
                // 不按整数序号猜测枚举；跨项目顺序可不同，名称才是绑定依据。
                if (!Enum.GetNames(type).Contains(text, StringComparer.Ordinal)) throw new FormatException("枚举键不存在: " + type.Name + "." + text);
                result = Enum.Parse(type, text, false);
            }
            else if (type == typeof(string)) result = text;
            else if (type == typeof(int)) result = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else if (type == typeof(float)) result = global::PlotWeave.PlotArgumentReader.Float(text);
            else if (type == typeof(bool)) result = text == "0" ? false : text == "1" ? true : bool.Parse(text);
            else if (type == typeof(Vector3)) result = global::PlotWeave.PlotArgumentReader.Vector3(text.Trim().Trim('(', ')'));
            else throw new InvalidCastException("没有数据转换器: " + raw.GetType().Name + " → " + type.Name);
            return (T)result;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static T Bean<T>(IPlotValueSource source, string key, Func<IPlotValueSource, T> create)
            => source.TryGetValue(key, out var raw) && raw != null ? create(Wrap(raw)) : default;
        public static List<T> List<T>(IPlotValueSource source, string key, Func<object, T> convert)
        {
            var result = new List<T>();
            if (!source.TryGetValue(key, out var raw) || raw == null) return result;
            if (raw is string || !(raw is IEnumerable sequence)) throw new FormatException(key + "应为列表");
            foreach (var value in sequence) result.Add(convert(value));
            return result;
        }
    }
}
