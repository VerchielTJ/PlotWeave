#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Authoring.UI
{
    /// <summary>列表Bean独立面板，仅绑定可见的参数行，支持任意成员数量。</summary>
    public sealed class ParameterList : IDisposable
    {
        private readonly PlotEditingSession _session;
        private readonly Action<Action> _safe;
        private readonly RectTransform _root;
        private readonly ScrollRect _scroll;
        private readonly Text _title;
        private readonly List<RectTransform> _pool = new List<RectTransform>();
        private PlotAtomDataHeaderAsset.ColumnDefinition _column;
        private bool _binding;
        private int _first = -1;
        private int _count = -1;
        private float _height;
        private float _rowHeight;
        public bool IsOpen => _root.gameObject.activeSelf;
        public int PoolSize => _pool.Count;
        public ParameterList(Transform parent, PlotEditingSession session, Action<Action> safe)
        {
            _session = session; _safe = safe;
            _root = PlotUiFactory.Stack(parent, "ListParameterPanel", background: true); PlotUiFactory.Stretch(_root);
            var header = PlotUiFactory.Stack(_root, "ListToolbar", true);
            _title = PlotUiFactory.Label(header, "ListTitle", "参数列表", 34, 20);
            PlotUiFactory.Button(header, "AddListItem", "添加一项", () => Guard(() => { _session.AddListRow(_column.columnName); Refresh(); }), 120);
            PlotUiFactory.Button(header, "CloseList", "返回节点", Close, 120);
            _scroll = PlotUiFactory.Scroll(_root, "ParameterRows"); _scroll.onValueChanged.AddListener(OnScroll); Close();
        }
        private void Guard(Action action) { if (!_binding) _safe(action); }
        public void Open(PlotAtomDataHeaderAsset.ColumnDefinition column)
        {
            _column = column; _root.gameObject.SetActive(true); _root.SetAsLastSibling();
            _scroll.content.anchoredPosition = Vector2.zero; _first = -1; Refresh();
        }
        public void Close() { _root.gameObject.SetActive(false); _column = null; }
        private void OnScroll(Vector2 value) => RefreshVisible();
        public void Refresh()
        {
            if (!IsOpen || _column == null || _session.WorkingRow == null) return;
            var members = _column.valueType == PlotEventTypeAsset.ValueType.CustomBean ? _session.WorkingRow.headerDefinition.GetColumnEventParameterCount(_column.columnName) : 1;
            _rowHeight = members * 72 + 82;
            var count = _session.WorkingRow.GetColumnRowCount(_column.columnName);
            _scroll.content.sizeDelta = new Vector2(0, count * _rowHeight);
            _title.text = _column.description + "  [" + _column.columnName + "]  · " + count + " 项";
            _first = -1; RefreshVisible();
        }
        public void Update()
        {
            if (IsOpen && Mathf.Abs(_scroll.viewport.rect.height - _height) > 1) { _first = -1; RefreshVisible(); }
        }
        public void RefreshVisible()
        {
            if (!IsOpen || _column == null || _session.WorkingRow == null) return;
            _height = _scroll.viewport.rect.height;
            var total = _session.WorkingRow.GetColumnRowCount(_column.columnName);
            var count = Mathf.Min(total, Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(100, _height) / _rowHeight) + 1));
            var first = Mathf.Clamp(Mathf.FloorToInt(_scroll.content.anchoredPosition.y / _rowHeight), 0, Mathf.Max(0, total - count));
            if (first == _first && count == _count) return;
            _first = first; _count = count; _binding = true;
            try
            {
                while (_pool.Count < count)
                {
                    var item = PlotUiFactory.Stack(_scroll.content, "PooledParameterRow", background: true);
                    item.anchorMin = new Vector2(0, 1); item.anchorMax = Vector2.one; item.pivot = new Vector2(.5f, 1);
                    _pool.Add(item);
                }
                for (var i = 0; i < _pool.Count; i++)
                {
                    var view = _pool[i]; view.gameObject.SetActive(i < count); if (i >= count) continue;
                    view.sizeDelta = new Vector2(0, _rowHeight - 8); view.anchoredPosition = new Vector2(0, -(first + i) * _rowHeight);
                    PlotUiFactory.Clear(view); var index = first + i;
                    var toolbar = PlotUiFactory.Stack(view, "ParameterToolbar", true);
                    PlotUiFactory.Label(toolbar, "RowIndex", "第 " + (index + 1) + " 项");
                    PlotUiFactory.Button(toolbar, "RemoveListItem", "移除此项", () => Guard(() => { _session.RemoveListRow(_column.columnName, index); Refresh(); }), 110);
                    new ParameterEditor(_session, Guard, Refresh).DrawRow(view, _column, index);
                }
            }
            finally { _binding = false; }
        }
        public void Dispose() { if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScroll); }
    }
}
#endif
