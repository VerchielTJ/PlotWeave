#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PlotWeave.Authoring.UI
{
    /// <summary>只复用可见节点按钮，不为全部节点生成GameObject。</summary>
    public sealed class PlotNodeView : MonoBehaviour
    {
        private const float RowHeight = 40;
        private ScrollRect _scroll;
        private List<PlotAtomRowData> _rows = new List<PlotAtomRowData>();
        private readonly List<PlotNodeButton> _pool = new List<PlotNodeButton>();
        private Action<int> _select;
        private string _idColumn;
        private int _selected;
        private float _height;
        private float _rowHeight = RowHeight, _minimumFont;
        private readonly Dictionary<Component, float> _originalFonts = new Dictionary<Component, float>();
        private Func<Transform,Button> _factory;
        public int PoolSize => _pool.Count;
        public int ActiveCount { get; private set; }
        public void Initialize(ScrollRect scroll, string idColumn, Action<int> select,Func<Transform,Button> factory=null)
        {
            _scroll = scroll; _idColumn = idColumn; _select = select;_factory=factory;
            _scroll.onValueChanged.AddListener(OnScroll);
        }
        public void SetRows(List<PlotAtomRowData> rows, int selected)
        {
            _rows = rows ?? new List<PlotAtomRowData>(); _selected = selected;
            _scroll.content.sizeDelta = new Vector2(0, _rows.Count * _rowHeight); RefreshVisible();
        }
        public void ConfigureDisplay(float rowHeight, float minimumFont)
        {
            rowHeight = Mathf.Max(RowHeight, rowHeight);
            if (Mathf.Abs(_rowHeight - rowHeight) < .01f && Mathf.Abs(_minimumFont - minimumFont) < .01f) return;
            var first = _scroll.content.anchoredPosition.y / _rowHeight;
            _rowHeight = rowHeight; _minimumFont = minimumFont;
            _scroll.content.sizeDelta = new Vector2(0, _rows.Count * _rowHeight);
            var position = _scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(first * _rowHeight, 0, Mathf.Max(0, _scroll.content.rect.height - _scroll.viewport.rect.height));
            _scroll.content.anchoredPosition = position;
            RefreshVisible();
        }
        private void OnScroll(Vector2 ignored) => RefreshVisible();
        private void Update()
        {
            if (_scroll != null && Mathf.Abs(_scroll.viewport.rect.height - _height) > 1) RefreshVisible();
        }
        public void RefreshVisible()
        {
            if (_scroll == null) return;
            _height = _scroll.viewport.rect.height;
            var count = Mathf.Min(_rows.Count, Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(100, _height) / _rowHeight) + 2));
            while (_pool.Count < count)
            {
                var button = _factory!=null?_factory(_scroll.content):PlotUiFactory.Button(_scroll.content, "PooledNode", "", null);
                var layout = button.GetComponent<LayoutElement>(); if (layout != null) layout.ignoreLayout = true;
                var rect = (RectTransform)button.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                var node = button.GetComponent<PlotNodeButton>(); if (node == null) node = button.gameObject.AddComponent<PlotNodeButton>();
                _pool.Add(node);
            }
            var first = Mathf.Clamp(Mathf.FloorToInt(_scroll.content.anchoredPosition.y / _rowHeight), 0, Mathf.Max(0, _rows.Count - count));
            ActiveCount = count;
            for (var i = 0; i < _pool.Count; i++)
            {
                var view = _pool[i]; view.gameObject.SetActive(i < count); if (i >= count) continue;
                var index = first + i; var row = _rows[index]; var id = int.Parse(row.GetColumnSingleValue(_idColumn), CultureInfo.InvariantCulture);
                var rect = (RectTransform)view.transform;
                rect.sizeDelta = new Vector2(0, _rowHeight - 4);
                rect.anchoredPosition = new Vector2(0, -index * _rowHeight);
                foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
                { if (!_originalFonts.ContainsKey(text)) _originalFonts[text] = text.fontSize; text.fontSize = Mathf.Max(_originalFonts[text], _minimumFont); }
                foreach (var text in view.GetComponentsInChildren<Text>(true))
                { if (!_originalFonts.ContainsKey(text)) _originalFonts[text] = text.fontSize; text.fontSize = Mathf.CeilToInt(Mathf.Max(_originalFonts[text], _minimumFont)); }
                view.Bind(id, id.ToString(CultureInfo.InvariantCulture), id == _selected, _select);
            }
        }
        private void OnDestroy() { if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScroll); }
    }
}
#endif
