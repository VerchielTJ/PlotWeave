#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlotWeave.Authoring.UI;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>Keep editor navigation readable; desktop minimum is explicit rather than silently shrinking every control.</summary>
    public sealed class PlotAuthoringViewportLayout : MonoBehaviour
    {
        public const int MinimumWidth = 1024, MinimumHeight = 600;
        public bool IsViewportTooSmall { get; private set; }
        private sealed class ToolbarItem
        {
            public LayoutElement size;
            public Text label;
            public float originalWidth;
            public int originalFont;
        }
        private Canvas _canvas;
        private RectTransform _toolbar, _nodes, _notice, _panel, _viewport;
        private Vector2 _nodeOffsetMax, _viewportOffsetMax;
        private Vector2 _lastPanelSize = new Vector2(-1, -1);
        private PlotLegacyAuthoringHost _host;
        private Text _noticeText;
        private Text _previewHint;
        private readonly List<ToolbarItem> _items = new List<ToolbarItem>();
        private readonly Dictionary<RectTransform, Vector2> _controlSizes = new Dictionary<RectTransform, Vector2>();
        private RectTransform _testRow;
        private TMP_InputField _entry;
        private Button _play, _refresh;
        private bool _configured;

        public static void Install(PlotLegacyAuthoringHost host)
        {
            var layout = host.GetComponent<PlotAuthoringViewportLayout>();
            if (layout == null) layout = host.gameObject.AddComponent<PlotAuthoringViewportLayout>();
            layout.Configure(host);
        }
        private void Configure(PlotLegacyAuthoringHost host)
        {
            if (_configured) return;
            _host = host;
            _canvas = host.PreviewViewport.GetComponentInParent<Canvas>().rootCanvas;
            _viewport = host.PreviewViewport; _viewportOffsetMax = _viewport.offsetMax;
            _panel = _viewport.parent as RectTransform;
            AnchorFromReference("RowDataEditor", Vector2.zero);
            AnchorFromReference("TestPlayerButton", Vector2.zero);
            AnchorFromReference("DiaLogKeyEditor", Vector2.zero);
            AnchorFromReference("NodeListLoader", Vector2.one);
            AnchorFromReference("CurrentNodeEditor", Vector2.one);
            AnchorFromReference("NodeCreater", Vector2.one);
            StretchRightColumn("PlotNodeView"); StretchRightColumn("TargetBack");
            _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.one;
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            _toolbar = _canvas.transform.Find("PlotWeaveMigrationToolbar") as RectTransform;
            _nodes = host.NodeView.transform as RectTransform;
            _nodeOffsetMax = _nodes.offsetMax;
            var previewBackground = _viewport.GetComponent<Image>();
            if (previewBackground != null) previewBackground.color = new Color(.07f, .10f, .15f, 1);
            _previewHint = PlotUiFactory.Label(_viewport, "AuthoringPreviewHint", "选择节点后点击播放；修改输入后可刷新试跑。", 44, 24);
            _previewHint.rectTransform.anchorMin = Vector2.zero; _previewHint.rectTransform.anchorMax = Vector2.one;
            _previewHint.rectTransform.offsetMin = new Vector2(24, 24); _previewHint.rectTransform.offsetMax = new Vector2(-24, -24);
            _previewHint.alignment = TextAnchor.MiddleCenter; _previewHint.color = new Color(.62f, .7f, .78f); _previewHint.raycastTarget = false;
            _testRow = _panel.Find("TestPlayerButton") as RectTransform;
            _entry = _testRow.GetComponentsInChildren<TMP_InputField>(true).First(i => i.name == "PlayerStartInputField");
            _play = _testRow.GetComponentsInChildren<Button>(true).First(b => b.name == "PlayButton");
            _refresh = _testRow.GetComponentsInChildren<Button>(true).First(b => b.name == "RefreshPlayDataButton");
            foreach (var rect in _testRow.GetComponentsInChildren<RectTransform>(true))
                if (rect.GetComponent<Button>() != null || rect.GetComponent<TMP_InputField>() != null) _controlSizes[rect] = rect.rect.size;
            var keys = _panel.Find("DiaLogKeyEditor");
            foreach (var rect in keys.GetComponentsInChildren<RectTransform>(true))
                if (rect.name != "DialogInputField" && (rect.GetComponent<Button>() != null || rect.GetComponent<TMP_InputField>() != null || rect.GetComponent<TMP_Dropdown>() != null)) _controlSizes[rect] = rect.rect.size;
            var redundantSave = keys.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "SaveDialogKey");
            if (redundantSave != null) redundantSave.gameObject.SetActive(false); // Same Session.Save is already exposed by the readable global toolbar.
            foreach (var button in _toolbar.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<Text>(true);
                var size = button.GetComponent<LayoutElement>();
                if (label == null || size == null) continue;
                _items.Add(new ToolbarItem { size = size, label = label, originalWidth = size.preferredWidth, originalFont = label.fontSize });
            }
            var notice = new GameObject("ViewportSizeNotice", typeof(RectTransform), typeof(Image));
            _notice = (RectTransform)notice.transform; _notice.SetParent(_canvas.transform, false);
            _notice.anchorMin = new Vector2(0, 1); _notice.anchorMax = Vector2.one; _notice.pivot = new Vector2(.5f, 1);
            var background = notice.GetComponent<Image>(); background.color = new Color(.17f, .13f, .025f, .96f); background.raycastTarget = false;
            _noticeText = PlotUiFactory.Label(_notice, "Message", "", 36, 14);
            var textRect = _noticeText.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(12, 0); textRect.offsetMax = new Vector2(-12, 0);
            _noticeText.color = new Color(1, .86f, .48f); _noticeText.alignment = TextAnchor.MiddleLeft; _noticeText.raycastTarget = false;
            _configured = true;
            UpdateLayout();
        }
        private void OnEnable() => Canvas.willRenderCanvases += UpdateLayout;
        private void OnDisable() => Canvas.willRenderCanvases -= UpdateLayout;
        public void UpdateLayout()
        {
            if (!_configured || _canvas == null || _toolbar == null || _nodes == null) return;
            var scale = Mathf.Max(.01f, _canvas.scaleFactor);
            _previewHint.fontSize = Mathf.CeilToInt(14 / scale);
            LayoutInputControls(scale);
            _host.NodeView.ConfigureDisplay(28 / scale, 12 / scale);
            var nodesOffset = _nodeOffsetMax - new Vector2(8, 0);
            if (_nodes.offsetMax != nodesOffset) _nodes.offsetMax = nodesOffset;
            var height = Mathf.Max(38, 28 / scale);
            if (Mathf.Abs(_toolbar.rect.height - height) > .01f) _toolbar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var group = _toolbar.GetComponent<HorizontalLayoutGroup>();
            if (group != null) { group.childControlHeight = true; group.childControlWidth = true; group.childForceExpandWidth = false; group.childAlignment = TextAnchor.MiddleLeft; group.spacing = 4 / scale; }
            foreach (var item in _items)
            {
                if (item.size == null || item.label == null) continue;
                item.label.fontSize = Mathf.Max(item.originalFont, Mathf.CeilToInt(12 / scale));
                var textWidth = item.label.preferredWidth + 16 / scale;
                item.size.minWidth = 44 / scale;
                item.size.preferredWidth = Mathf.Max(item.originalWidth, Mathf.Max(44 / scale, textWidth));
                item.size.minHeight = height; item.size.preferredHeight = height;
            }
            IsViewportTooSmall = Screen.width < MinimumWidth || Screen.height < MinimumHeight;
            var top = height + (IsViewportTooSmall ? 48 / scale : 6 / scale);
            var viewportOffset = new Vector2(_viewportOffsetMax.x, _viewportOffsetMax.y - top);
            if (_viewport.offsetMax != viewportOffset) _viewport.offsetMax = viewportOffset;
            if (_lastPanelSize != _panel.rect.size)
            {
                _host.RefreshInputDock();
                _lastPanelSize = _panel.rect.size;
            }
            _notice.gameObject.SetActive(IsViewportTooSmall);
            if (IsViewportTooSmall)
            {
                _notice.anchoredPosition = new Vector2(0, -height - 4 / scale); _notice.sizeDelta = new Vector2(0, 40 / scale);
                _noticeText.fontSize = Mathf.CeilToInt(12 / scale);
                _noticeText.text = "当前窗口 " + Screen.width + "×" + Screen.height + "；制作工作区建议至少1024×600，请扩大或最大化Game视图。";
            }
        }
        private void AnchorFromReference(string name, Vector2 anchor)
        {
            var rect = _panel.Find(name) as RectTransform; if (rect == null) return;
            var previous = (rect.anchorMin + rect.anchorMax) * .5f;
            var position = rect.anchoredPosition + Vector2.Scale(previous - anchor, new Vector2(3200, 1500));
            rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position;
        }
        private void StretchRightColumn(string name)
        {
            var rect = _panel.Find(name) as RectTransform; if (rect == null) return;
            AnchorFromReference(name, new Vector2(1, .5f));
            var width = rect.rect.width;
            rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(width, 0); rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0);
        }
        private void LayoutInputControls(float scale)
        {
            foreach (var item in _controlSizes)
            {
                var rect = item.Key;
                var height = Mathf.Max(item.Value.y, 28 / scale);
                if (Mathf.Abs(rect.rect.height - height) > .01f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            var refresh = (RectTransform)_refresh.transform; var entry = (RectTransform)_entry.transform; var play = (RectTransform)_play.transform;
            var padding = 8f; var gap = 8f;
            var rw = Mathf.Max(_controlSizes[refresh].x, 44 / scale);
            var pw = Mathf.Max(_controlSizes[play].x, 44 / scale);
            var available = _testRow.rect.width - padding * 2 - gap * 2;
            var iw = Mathf.Max(40 / scale, available - rw - pw);
            PlaceControl(refresh, padding, rw); PlaceControl(entry, padding + rw + gap, iw); PlaceControl(play, padding + rw + gap + iw + gap, pw);
        }
        private static void PlaceControl(RectTransform rect, float left, float width)
        {
            var anchor = new Vector2(0, .5f); var position = new Vector2(left, 0);
            if (rect.anchorMin != anchor) rect.anchorMin = anchor;
            if (rect.anchorMax != anchor) rect.anchorMax = anchor;
            if (rect.pivot != anchor) rect.pivot = anchor;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (Mathf.Abs(rect.rect.width - width) > .01f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }
}
#endif
