using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/文本/对话KEY")]
    public sealed class PlotDialogKeyAsset : PlotTextKeyAsset
    {
        [Serializable] public sealed class DialogKeyData : PlotTextEntry { }
        public List<DialogKeyData> dialogKeyDataList = new List<DialogKeyData>();
        protected override IEnumerable<PlotTextEntry> Entries => dialogKeyDataList;
        public void AddDialogKey(string key, int languageCount = 5)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("KEY不能为空", nameof(key));
            if (HasKey(key)) return;
            var languages = EmptyLanguages(languageCount);
            if (dialogKeyDataList == null) dialogKeyDataList = new List<DialogKeyData>();
            dialogKeyDataList.Add(new DialogKeyData { key = key, value = languages });
        }
        public string GetDialogContent(string key, int languageIndex = 0) => ReadContent(key, languageIndex);
        public string GetDialogContent(string key, PlotDialogLanguageType language) => ReadContent(key, (int)language);
        public void SetDialogContent(string key, int languageIndex, string content) => WriteContent(key, languageIndex, content);
        public void SetDialogContent(string key, PlotDialogLanguageType language, string content) => WriteContent(key, (int)language, content);
        public void RemoveDialogKey(string key) => dialogKeyDataList?.RemoveAll(e => e != null && e.key == key);
        public void ClearAllKeys() => dialogKeyDataList?.Clear();
        public List<DialogKeyData> GetAllDialogData() => dialogKeyDataList == null ? new List<DialogKeyData>() : new List<DialogKeyData>(dialogKeyDataList);
    }
}
