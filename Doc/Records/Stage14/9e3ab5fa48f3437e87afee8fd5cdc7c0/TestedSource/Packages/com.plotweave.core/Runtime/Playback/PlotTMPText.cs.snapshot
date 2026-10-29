using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace PlotWeave.Playback
{
    public interface IPlotPlayerTextLayout
    { int[] GetTypingOffsets(string fullText); }

    /// <summary>TMP负责解析富文本；Core只按解析后的Unicode文字单元推进。</summary>
    public static class PlotTMPText
    {
        private static TMP_FontAsset _font;
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                var bundled = Resources.Load<Font>("PlotWeaveFonts/NotoSansCJKsc-Regular");
                if (bundled != null)
                { _font = TMP_FontAsset.CreateFontAsset(bundled, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024); if (_font != null) return _font; }
                foreach (var name in new[] { "Microsoft YaHei", "Microsoft YaHei UI", "微软雅黑", "SimSun", "Noto Sans CJK SC", "Arial" })
                {
                    var font = Font.CreateDynamicFontFromOSFont(name, 24); if (font == null) continue;
                    var candidate = TMP_FontAsset.CreateFontAsset(font, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                    if (candidate == null) continue;
                    if (candidate.TryAddCharacters("中文", out string missing) && missing.Length == 0) { _font = candidate; break; }
                    if (_font == null) _font = candidate;
                }
                if (_font == null) _font = TMP_Settings.defaultFontAsset;
                if (_font == null) throw new InvalidOperationException("请先导入TMP Essential Resources，或在播放资源配置中指定TMP字体");
                return _font;
            }
        }
        public static void Prepare(TMP_Text label, string text)
        { label.richText = true; label.text = text ?? ""; label.maxVisibleCharacters = int.MaxValue; label.ForceMeshUpdate(true, true); }
        private static string Character(TMP_CharacterInfo info, string source)
        {
            if (info.elementType == TMP_TextElementType.Sprite) return "\uFFFC";
            if (info.index >= 0 && info.index < source.Length && source[info.index] != '<')
            { if (info.index + 1 < source.Length && char.IsHighSurrogate(source[info.index]) && char.IsLowSurrogate(source[info.index + 1])) return source.Substring(info.index, 2); return source[info.index].ToString(); }
            return info.character.ToString();
        }
        public static int[] TypingOffsets(TMP_Text label, string text)
        {
            text = text ?? ""; Prepare(label, text); var plain = new StringBuilder(); var indices = new List<int>();
            for (var i = 0; i < label.textInfo.characterCount; i++)
            {
                var info = label.textInfo.characterInfo[i]; var value = Character(info, text); plain.Append(value);
                for (var j = 0; j < value.Length; j++) indices.Add(info.index);
            }
            var groups = StringInfo.ParseCombiningCharacters(plain.ToString()); var result = new int[groups.Length];
            for (var i = 1; i < groups.Length; i++) result[i] = indices[groups[i]];
            label.maxVisibleCharacters = 0; return result;
        }
        public static string Reveal(TMP_Text label, string text, int codeUnits)
        {
            text = text ?? ""; if (label.text != text) Prepare(label, text);
            var plain = new StringBuilder(); var count = 0;
            for (var i = 0; i < label.textInfo.characterCount; i++)
            {
                var info = label.textInfo.characterInfo[i]; if (codeUnits < text.Length && info.index >= codeUnits) break;
                plain.Append(Character(info, text)); count++;
            }
            label.maxVisibleCharacters = count; return plain.ToString();
        }
    }
}
