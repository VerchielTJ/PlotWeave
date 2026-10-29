#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Authoring.UI
{
    public sealed class DiaLogKeyEditor
    {
        private readonly Transform _parent;
        private readonly PlotEditingSession _session;
        private readonly Action<Action> _safe;
        private readonly Func<string, bool> _confirm;
        private InputField _keyInput;
        private InputField _content;
        private string _key = "";
        private string _loaded = "";
        private int _language;
        private bool _constant;
        private bool _binding;
        public DiaLogKeyEditor(Transform parent, PlotEditingSession session, Action<Action> safe, Func<string, bool> confirm)
        { _parent = parent; _session = session; _safe = safe; _confirm = confirm; }
        private void Guard(Action action) { if (!_binding) _safe(action); }
        public void Draw(bool useNodeKey)
        {
            _binding = true;
            try
            {
                if (useNodeKey && !_constant) _key = _session.CurrentDialogKey;
                PlotUiFactory.Clear(_parent);
                PlotUiFactory.Label(_parent, "TextTitle", "KEY 与多语言", 32, 20);
                PlotUiFactory.Dropdown(_parent, "TextTableKind", new List<string> { "对话表", "常量表" }, _constant ? 1 : 0, index => Guard(() => { Commit(); _constant = index == 1; _key = _constant ? "" : _session.CurrentDialogKey; Draw(false); }));
                _keyInput = PlotUiFactory.Input(_parent, "TextKey", _key, text => Guard(() => { Commit(); _key = text.Trim(); Draw(false); }), placeholderText: "输入KEY或按节点生成");
                PlotUiFactory.Dropdown(_parent, "TextLanguage", new List<string> { "简体中文", "繁體中文", "English", "日本語", "한국어" }, _language, index => Guard(() => { Commit(); _language = index; Draw(false); }));
                _loaded = _constant ? _session.Constants?.GetConstantContent(_key, _language) ?? "" : _session.Dialogs?.GetDialogContent(_key, _language) ?? "";
                _content = PlotUiFactory.Input(_parent, "LocalizedContent", _loaded, ignored => Guard(Commit), true, "输入当前语言内容");
                PlotUiFactory.Button(_parent, "SaveLocalizedDraft", "保存到工作副本", () => Guard(Commit));
                if (!_constant)
                {
                    PlotUiFactory.Button(_parent, "GenerateDialogKey", "按节点生成并绑定 KEY", () => Guard(() => { Commit(); _key = _session.GenerateDialogKey(); Draw(false); }));
                    PlotUiFactory.Button(_parent, "BindDialogKey", "将此 KEY 绑定到节点", () => Guard(() => { Commit(); _session.SetDialogKey(_key); }));
                }
                PlotUiFactory.Button(_parent, "DeleteTextKey", "移除此 KEY", () => Guard(() =>
                {
                    if (string.IsNullOrWhiteSpace(_key)) return;
                    if (_confirm != null && !_confirm("从工作副本移除KEY：" + _key + "？其他节点可能仍引用它，保存前可以放弃修改。")) return;
                    _session.RemoveText(_constant, _key); _key = ""; Draw(false);
                }));
                PlotUiFactory.Label(_parent, "TextSaveHint", "修改暂存于副本，点击顶部保存后写入资产。", 54, 14);
            }
            finally { _binding = false; }
        }
        public void Commit()
        {
            if (_binding || _content == null || _content.text == _loaded) return;
            if (string.IsNullOrWhiteSpace(_key)) throw new InvalidOperationException("请先填写KEY，或按节点生成KEY");
            _session.SetText(_constant, _key, _language, _content.text); _loaded = _content.text;
        }
    }
}
#endif
