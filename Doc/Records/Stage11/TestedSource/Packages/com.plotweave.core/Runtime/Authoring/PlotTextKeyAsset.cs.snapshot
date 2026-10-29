using System;
using System.Collections.Generic;
using System.Linq;

namespace PlotWeave.Authoring
{
    [Serializable] public class PlotTextEntry
    {
        public string key = "";
        public List<string> value = new List<string>();
    }

    public abstract class PlotTextKeyAsset : PlotAuthoringAsset
    {
        protected abstract IEnumerable<PlotTextEntry> Entries { get; }
        protected PlotTextEntry FindEntry(string key) => string.IsNullOrEmpty(key) ? null : Entries?.FirstOrDefault(e => e != null && e.key == key);
        protected static List<string> EmptyLanguages(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            return Enumerable.Repeat("", count).ToList();
        }
        protected string ReadContent(string key, int languageIndex) => FindEntry(key)?.value is List<string> values && languageIndex >= 0 && languageIndex < values.Count ? values[languageIndex] ?? "" : "";
        protected void WriteContent(string key, int languageIndex, string content)
        {
            var entry = FindEntry(key);
            if (entry == null) throw new KeyNotFoundException("KEY不存在: " + key);
            if (entry.value == null || languageIndex < 0 || languageIndex >= entry.value.Count) throw new ArgumentOutOfRangeException(nameof(languageIndex));
            entry.value[languageIndex] = content ?? "";
        }
        public bool HasKey(string key) => FindEntry(key) != null;
        public List<string> GetAllKeys() => Entries?.Where(e => e != null).Select(e => e.key).ToList() ?? new List<string>();
        public int GetLanguageCount(string key) => FindEntry(key)?.value?.Count ?? 0;
        public int GetDefaultLanguageCount() => Entries?.FirstOrDefault()?.value?.Count ?? 0;
        public bool HasContent(string key, int languageIndex) => !string.IsNullOrEmpty(ReadContent(key, languageIndex));
        public bool HasContent(string key, PlotDialogLanguageType language) => HasContent(key, (int)language);
        public bool ValidateData() => Validate().IsValid;
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult();
            if (Entries == null) { result.Errors.Add("文本列表为空引用"); return result; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int? count = null;
            foreach (var entry in Entries)
            {
                if (entry == null) { result.Errors.Add("文本记录为空引用"); continue; }
                result.Unique(seen, entry.key, "文本KEY");
                if (entry.value == null) { result.Errors.Add(entry.key + "语言列表为空引用"); continue; }
                if (count == null) count = entry.value.Count;
                result.Require(entry.value.Count > 0, entry.key + "语言列表不能为空");
                result.Require(entry.value.Count == count, entry.key + "语言数量不一致");
            }
            return result;
        }
    }
}
