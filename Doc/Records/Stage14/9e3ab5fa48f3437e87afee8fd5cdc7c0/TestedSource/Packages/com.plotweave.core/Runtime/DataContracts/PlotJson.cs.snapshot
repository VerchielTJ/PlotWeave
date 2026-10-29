using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlotWeave.Data
{
    /// <summary>用于动态UI属性/事件载荷的数据JSON；不创建类型或执行代码。</summary>
    public static class PlotJson
    {
        public static object Parse(string json) { var reader = new Reader(json ?? throw new ArgumentNullException(nameof(json))); var value = reader.Value(0); reader.Space(); if (!reader.End) throw new FormatException("JSON尾部存在额外内容"); return value; }
        public static Dictionary<string, object> Object(string json) => Parse(json) as Dictionary<string, object> ?? throw new FormatException("需要JSON对象");
        public static string Write(object value) { var output = new StringBuilder(); WriteValue(output, value, 0); return output.ToString(); }
        private static void Quoted(StringBuilder output, string value)
        {
            output.Append('"'); foreach (var c in value) switch (c) { case '"': output.Append("\\\""); break; case '\\': output.Append("\\\\"); break; case '\n': output.Append("\\n"); break; case '\r': output.Append("\\r"); break; case '\t': output.Append("\\t"); break; default: if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); else output.Append(c); break; } output.Append('"');
        }
        private static void WriteValue(StringBuilder output, object value, int depth)
        {
            if (depth > 96) throw new FormatException("JSON嵌套超过96层");
            if (value == null) { output.Append("null"); return; } if (value is string text) { Quoted(output, text); return; } if (value is bool flag) { output.Append(flag ? "true" : "false"); return; }
            if (value is IDictionary<string, object> map) { output.Append('{'); var first = true; foreach (var pair in map) { if (!first) output.Append(','); first = false; Quoted(output, pair.Key); output.Append(':'); WriteValue(output, pair.Value, depth + 1); } output.Append('}'); return; }
            if (value is IEnumerable list) { output.Append('['); var first = true; foreach (var item in list) { if (!first) output.Append(','); first = false; WriteValue(output, item, depth + 1); } output.Append(']'); return; }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is decimal) { output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            if (value is float || value is double) { var number = Convert.ToDouble(value, CultureInfo.InvariantCulture); if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("JSON数值必须有限"); output.Append(number.ToString("R", CultureInfo.InvariantCulture)); return; }
            throw new ArgumentException("不支持的JSON值类型: " + value.GetType().Name);
        }
        private sealed class Reader
        {
            private readonly string _text; private int _index;
            public Reader(string text) { if (text.Length > 16777216) throw new FormatException("单个JSON载荷超过16Mi字符"); _text = text; }
            public bool End => _index == _text.Length;
            public void Space() { while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++; }
            private bool Take(char c) { Space(); if (_index == _text.Length || _text[_index] != c) return false; _index++; return true; }
            private void Expect(char c) { if (!Take(c)) throw new FormatException("JSON缺少字符: " + c + " at " + _index); }
            public object Value(int depth)
            {
                if (depth > 96) throw new FormatException("JSON嵌套超过96层"); Space(); if (End) throw new FormatException("JSON意外结束"); var c = _text[_index];
                if (c == '"') return String();
                if (c == '{') { _index++; var map = new Dictionary<string, object>(StringComparer.Ordinal); if (Take('}')) return map; do { Space(); var key = String(); Expect(':'); if (map.ContainsKey(key)) throw new FormatException("JSON键重复: " + key); map.Add(key, Value(depth + 1)); } while (Take(',')); Expect('}'); return map; }
                if (c == '[') { _index++; var list = new List<object>(); if (Take(']')) return list; do { list.Add(Value(depth + 1)); } while (Take(',')); Expect(']'); return list; }
                if (_index + 4 <= _text.Length && _text.Substring(_index, 4) == "true") { _index += 4; return true; } if (_index + 5 <= _text.Length && _text.Substring(_index, 5) == "false") { _index += 5; return false; } if (_index + 4 <= _text.Length && _text.Substring(_index, 4) == "null") { _index += 4; return null; }
                var start = _index; if (c == '-') _index++;
                if (_index >= _text.Length || !char.IsDigit(_text[_index])) throw new FormatException("JSON数值无效");
                if (_text[_index] == '0') _index++; else while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                if (_index < _text.Length && _text[_index] == '.') { _index++; var decimalStart = _index; while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++; if (_index == decimalStart) throw new FormatException("JSON小数缺少数字"); }
                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E')) { _index++; if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++; var exponentStart = _index; while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++; if (_index == exponentStart) throw new FormatException("JSON指数缺少数字"); }
                var token = _text.Substring(start, _index - start); if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer; var number = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture); if (double.IsInfinity(number) || double.IsNaN(number)) throw new FormatException("JSON数值必须有限"); return number;
            }
            private string String()
            {
                if (_index >= _text.Length || _text[_index++] != '"') throw new FormatException("JSON键或字符串缺少引号"); var value = new StringBuilder();
                while (_index < _text.Length)
                {
                    var c = _text[_index++]; if (c == '"') return value.ToString(); if (c < 32) throw new FormatException("JSON字符串包含未转义控制字符"); if (c != '\\') { value.Append(c); continue; }
                    if (_index >= _text.Length) throw new FormatException("JSON转义意外结束"); c = _text[_index++];
                    switch (c) { case '"': case '\\': case '/': value.Append(c); break; case 'b': value.Append('\b'); break; case 'f': value.Append('\f'); break; case 'n': value.Append('\n'); break; case 'r': value.Append('\r'); break; case 't': value.Append('\t'); break; case 'u': if (_index + 4 > _text.Length) throw new FormatException("Unicode转义不完整"); value.Append((char)ushort.Parse(_text.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); _index += 4; break; default: throw new FormatException("JSON转义无效"); }
                }
                throw new FormatException("JSON字符串未结束");
            }
        }
    }
}
