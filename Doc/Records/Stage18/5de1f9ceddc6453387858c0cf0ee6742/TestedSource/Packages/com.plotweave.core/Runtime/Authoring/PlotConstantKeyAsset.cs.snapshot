using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/文本/常量KEY")]
    public sealed class PlotConstantKeyAsset : PlotTextKeyAsset
    {
        [Serializable] public sealed class ConstantKeyData : PlotTextEntry { }
        public List<ConstantKeyData> constantKeyDataList = new List<ConstantKeyData>();
        protected override IEnumerable<PlotTextEntry> Entries => constantKeyDataList;
        public void AddConstantKey(string key, int languageCount = 5)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("KEY不能为空", nameof(key));
            if (HasKey(key)) return;
            var languages = EmptyLanguages(languageCount);
            if (constantKeyDataList == null) constantKeyDataList = new List<ConstantKeyData>();
            constantKeyDataList.Add(new ConstantKeyData { key = key, value = languages });
        }
        public string GetConstantContent(string key, int languageIndex = 0) => ReadContent(key, languageIndex);
        public string GetConstantContent(string key, PlotDialogLanguageType language) => ReadContent(key, (int)language);
        public void SetConstantContent(string key, int languageIndex, string content) => WriteContent(key, languageIndex, content);
        public void SetConstantContent(string key, PlotDialogLanguageType language, string content) => WriteContent(key, (int)language, content);
        public void RemoveConstantKey(string key) => constantKeyDataList?.RemoveAll(e => e != null && e.key == key);
        public void ClearAllKeys() => constantKeyDataList?.Clear();
        public List<ConstantKeyData> GetAllConstantData() => constantKeyDataList == null ? new List<ConstantKeyData>() : new List<ConstantKeyData>(constantKeyDataList);
    }
}
