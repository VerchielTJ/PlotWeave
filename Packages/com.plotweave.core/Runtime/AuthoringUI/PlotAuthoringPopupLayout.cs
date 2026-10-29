#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PlotWeave.Authoring.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>Elevates one original popup without moving it out of its RowUnit or scroll hierarchy.</summary>
    public sealed class PlotAuthoringPopupLayout : MonoBehaviour
    {
        private sealed class Original
        {
            public Vector3 scale;
            public Vector2 position;
            public Canvas canvas;
        }
        private readonly Dictionary<RectTransform, Original> _originals = new Dictionary<RectTransform, Original>();
        private Canvas _canvas;
        private RectTransform _canvasRect, _panel;
        private bool _layingOut;
        public RectTransform ActivePopup { get; private set; }

        public void Initialize(Canvas canvas, RectTransform panel)
        {
            _canvas = canvas != null ? canvas.rootCanvas : throw new ArgumentNullException(nameof(canvas));
            _canvasRect = (RectTransform)_canvas.transform; _panel = panel != null ? panel : _canvasRect;
        }
        private void OnEnable() => Canvas.willRenderCanvases += Refresh;
        private void OnDisable() { Canvas.willRenderCanvases -= Refresh; Close(); }

        public void Toggle(RectTransform popup)
        { if (ActivePopup == popup && popup != null && popup.gameObject.activeSelf) Close(); else Open(popup); }
        public void Open(RectTransform popup)
        {
            if (_canvas == null) throw new InvalidOperationException("先初始化制作弹层布局");
            if (popup == null) throw new ArgumentNullException(nameof(popup));
            if (ActivePopup != popup) Close();
            if (!_originals.TryGetValue(popup, out var original))
            {
                var canvas = popup.GetComponent<Canvas>(); if (canvas == null) canvas = popup.gameObject.AddComponent<Canvas>();
                original = new Original { scale = popup.localScale, position = popup.anchoredPosition, canvas = canvas };
                _originals.Add(popup, original);
                var raycaster = popup.GetComponent<GraphicRaycaster>(); if (raycaster == null) popup.gameObject.AddComponent<GraphicRaycaster>();
            }
            ActivePopup = popup; popup.gameObject.SetActive(true);
            EnsureClose(popup); Refresh();
        }
        public void Close()
        {
            var popup = ActivePopup; ActivePopup = null;
            if (popup == null) return;
            popup.gameObject.SetActive(false);
            if (_originals.TryGetValue(popup, out var original))
            { popup.localScale = original.scale; popup.anchoredPosition = original.position; }
        }
        private void EnsureClose(RectTransform popup)
        {
            var existing = popup.Find("PlotWeaveClosePopup");
            var button = existing == null ? PlotUiFactory.Button(popup, "PlotWeaveClosePopup", "关闭", null, 160) : existing.GetComponent<Button>();
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { if (ActivePopup == popup) Close(); });
            var rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(160, 50);
            var size = button.GetComponent<LayoutElement>();
            if (size != null) { size.minHeight = size.preferredHeight = 50; size.preferredWidth = 160; }
            if (popup.GetComponent<LayoutGroup>() != null) return;
            // The original list header protrudes above its ScrollRect. Put close above that
            // header, and include it in the same visible-bounds calculation used for clamping.
            if (size != null) size.ignoreLayout = true;
            var top = Measure(popup, true).yMax;
            var localTop = popup.InverseTransformPoint(_canvasRect.TransformPoint(new Vector3(0, top, 0))).y;
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-8, Mathf.Max(0, localTop - popup.rect.yMax) + 8);
        }
        public void Refresh()
        {
            if (_layingOut || ActivePopup == null || _canvas == null || !ActivePopup.gameObject.activeInHierarchy) return;
            _layingOut = true;
            try
            {
                var popup = ActivePopup; var original = _originals[popup];
                original.canvas.overrideSorting = true; original.canvas.sortingLayerID = _canvas.sortingLayerID;
                original.canvas.sortingOrder = _canvas.sortingOrder + 20;
                popup.localScale = original.scale;
                var safe = SafeRect(popup); if (safe.width <= 0 || safe.height <= 0) return;
                // A close control has a physical minimum even when the surrounding legacy
                // popup has to fit a smaller viewport. Re-measure after its layout changes.
                for(var pass=0;pass<24;pass++)
                {
                    SizeClose(popup);LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
                    var measured=Measure(popup,false);
                    var fit=Mathf.Min(1,safe.width/Mathf.Max(1,measured.width),safe.height/Mathf.Max(1,measured.height));
                    if(fit>=.99999f)break;
                    var scale=popup.localScale;popup.localScale=new Vector3(scale.x*fit,scale.y*fit,scale.z);
                }
                SizeClose(popup);LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
                var bounds = Measure(popup, false);
                var dx = bounds.xMin < safe.xMin ? safe.xMin - bounds.xMin : bounds.xMax > safe.xMax ? safe.xMax - bounds.xMax : 0;
                var dy = bounds.yMin < safe.yMin ? safe.yMin - bounds.yMin : bounds.yMax > safe.yMax ? safe.yMax - bounds.yMax : 0;
                if (dx != 0 || dy != 0) popup.position += _canvasRect.TransformVector(new Vector3(dx, dy, 0));
            }
            finally { _layingOut = false; }
        }
        private void SizeClose(RectTransform popup)
        {
            var close=popup.Find("PlotWeaveClosePopup") as RectTransform;if(close==null)return;
            var camera=_canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:_canvas.worldCamera;
            var origin=RectTransformUtility.WorldToScreenPoint(camera,close.TransformPoint(Vector3.zero));
            var x=RectTransformUtility.WorldToScreenPoint(camera,close.TransformPoint(Vector3.right));
            var y=RectTransformUtility.WorldToScreenPoint(camera,close.TransformPoint(Vector3.up));
            var pixelX=Mathf.Max(.0001f,Vector2.Distance(origin,x));var pixelY=Mathf.Max(.0001f,Vector2.Distance(origin,y));
            var width=Mathf.Max(160,44/pixelX);var height=Mathf.Max(50,28/pixelY);close.sizeDelta=new Vector2(width,height);
            var size=close.GetComponent<LayoutElement>();if(size!=null){size.minHeight=size.preferredHeight=height;size.minWidth=size.preferredWidth=width;}
            var label=close.GetComponentInChildren<Text>(true);if(label!=null)label.fontSize=Mathf.Max(16,Mathf.CeilToInt(12/pixelY));
        }
        private Rect SafeRect(RectTransform popup)
        {
            var safe = Intersect(_canvasRect.rect, Bounds(_panel));
            for (var at = popup.parent; at != null && at != _canvasRect; at = at.parent)
                if (at is RectTransform rect && HasMask(at)) safe = Intersect(safe, Bounds(rect));
            var toolbar = _canvasRect.Find("PlotWeaveMigrationToolbar") as RectTransform;
            if (toolbar != null && toolbar.gameObject.activeInHierarchy)
            {
                var bar = Bounds(toolbar);
                if (bar.xMax > safe.xMin && bar.xMin < safe.xMax && bar.yMin < safe.yMax && bar.yMax > safe.yMin)
                    safe.yMax = Mathf.Min(safe.yMax, bar.yMin);
            }
            var margin = 8 / Mathf.Max(.01f, _canvas.scaleFactor);
            return Rect.MinMaxRect(safe.xMin + margin, safe.yMin + margin, safe.xMax - margin, safe.yMax - margin);
        }
        private Rect Measure(RectTransform popup, bool omitClose)
        {
            var result = Bounds(popup);
            foreach (var graphic in popup.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || (omitClose && IsClose(graphic.transform, popup))) continue;
                var nested = graphic.canvas;
                if (nested != null && nested != _originals[popup].canvas && nested.overrideSorting) continue;
                var visible = Bounds((RectTransform)graphic.transform);
                for (var at = graphic.transform.parent; at != null && at != popup; at = at.parent)
                    if (at is RectTransform rect && HasMask(at)) visible = Intersect(visible, Bounds(rect));
                if (visible.width <= 0 || visible.height <= 0) continue;
                result = Rect.MinMaxRect(Mathf.Min(result.xMin, visible.xMin), Mathf.Min(result.yMin, visible.yMin), Mathf.Max(result.xMax, visible.xMax), Mathf.Max(result.yMax, visible.yMax));
            }
            return result;
        }
        private static bool IsClose(Transform value, Transform popup)
        { for (var at = value; at != null && at != popup; at = at.parent) if (at.name == "PlotWeaveClosePopup") return true; return false; }
        private static bool HasMask(Transform value)
        {
            var rectMask = value.GetComponent<RectMask2D>(); if (rectMask != null && rectMask.isActiveAndEnabled) return true;
            var mask = value.GetComponent<Mask>(); return mask != null && mask.isActiveAndEnabled;
        }
        private Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); var first = _canvasRect.InverseTransformPoint(corners[0]);
            var min = new Vector2(first.x, first.y); var max = min;
            for (var i = 1; i < corners.Length; i++) { var point = (Vector2)_canvasRect.InverseTransformPoint(corners[i]); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Rect Intersect(Rect a, Rect b)
        { var min = Vector2.Max(a.min, b.min); var max = Vector2.Max(min, Vector2.Min(a.max, b.max)); return Rect.MinMaxRect(min.x, min.y, max.x, max.y); }
    }
}
#endif
