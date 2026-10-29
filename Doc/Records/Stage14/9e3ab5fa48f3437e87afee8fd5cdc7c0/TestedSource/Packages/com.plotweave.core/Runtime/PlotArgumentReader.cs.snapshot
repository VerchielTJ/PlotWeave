using System;
using System.Globalization;

namespace PlotWeave
{
    public static class PlotArgumentReader
    {
        public static int Int(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        public static float Float(string text)
        {
            var value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new FormatException("数值必须有限。");
            return value;
        }
        public static bool Bool(string text) => bool.Parse(text);
        public static UnityEngine.Vector2 Vector2(string text)
        {
            var parts = text.Split(','); if (parts.Length != 2) throw new FormatException("Vector2 需要 x,y。");
            return new UnityEngine.Vector2(Float(parts[0]), Float(parts[1]));
        }
        public static UnityEngine.Vector3 Vector3(string text)
        {
            var parts = text.Split(','); if (parts.Length != 3) throw new FormatException("Vector3 需要 x,y,z。");
            return new UnityEngine.Vector3(Float(parts[0]), Float(parts[1]), Float(parts[2]));
        }
        public static UnityEngine.Color Color(string text) => UnityEngine.ColorUtility.TryParseHtmlString(text, out var value) ? value : throw new FormatException("Color 需要 HTML 色值。");
    }
}
