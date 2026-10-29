using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Authoring.UI;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    /// <summary>编辑宿主只负责重建制作快照与装配同一PlotPlayerHost/Core。</summary>
    public sealed class EditorPlotPlayerUI : IDisposable
    {
        private readonly PlotAuthoringProject _project;
        private readonly Func<PlotPlaybackDocument> _snapshot;
        private readonly RectTransform _viewport;
        private PlotPlayerHost _host;
        private PlotPlayerViewBindings _bindings;
        private PlotPlaybackDocument _prepared;
        private RectTransform _designRoot;
        private Vector2 _designSize;
        private bool _layoutBound;
        private int _lastEntry;
        public PlotPlayerHost Host => _host;
        public bool IsPreviewActive => _host != null && _host.Core != null
            && _host.Core.State != PlotPlayerState.Closed
            && _host.Core.State != PlotPlayerState.Uninitialized
            && _bindings != null && _bindings.uiRoot != null && _bindings.uiRoot.activeInHierarchy;

        public EditorPlotPlayerUI(PlotAuthoringProject project) : this(project, null, null) { }

        /// <summary>The optional source projects the live editing session without saving its assets.</summary>
        public EditorPlotPlayerUI(PlotAuthoringProject project, Func<PlotPlaybackDocument> snapshot, RectTransform viewport = null)
        {
            _project = project != null ? project : throw new ArgumentNullException(nameof(project));
            if (viewport != null && viewport.GetComponentInParent<Canvas>() == null)
                throw new ArgumentException("内嵌预览区域必须位于制作Canvas内", nameof(viewport));
            _snapshot = snapshot;
            _viewport = viewport;
        }

        private PlotPlaybackDocument ReadSnapshot()
        {
            var data = _snapshot != null ? _snapshot() : PlotPlaybackCompiler.Compile(_project);
            if (data == null) throw new InvalidOperationException("制作快照为空");
            data.Validate();
            return data;
        }

        public void Play(int id) => PlaySnapshot(ReadSnapshot(), id, null, false);

        /// <summary>Refresh is an editing operation, not a save. A closed preview stays closed.</summary>
        public void Refresh(int? entry = null)
        {
            var data = ReadSnapshot();
            if (!IsPreviewActive) { _prepared = data; return; }
            var current = _host.Core.GetCurrentNodeID();
            var target = entry ?? (current > 0 ? current : _lastEntry);
            PlaySnapshot(data, target, _host.Core.GetAutoPlay(), _host.Core.IsPaused);
        }

        private void PlaySnapshot(PlotPlaybackDocument data, int id, bool? auto, bool paused)
        {
            // Validate before replacing the previous working player so input errors leave it usable.
            if (id <= 0 || !data.Nodes.Any(node => node.ScenesID == id))
                throw new InvalidOperationException("制作快照中不存在预览节点: " + id);
            if (_host == null)
            {
                var root = _viewport != null
                    ? new GameObject("PlotWeave Editor Playback", typeof(RectTransform), typeof(PlotPlayerHost))
                    : new GameObject("PlotWeave Editor Playback", typeof(PlotPlayerHost));
                _host = root.GetComponent<PlotPlayerHost>();
                if (_viewport != null)
                {
                    root.transform.SetParent(_viewport, false);
                    Fill((RectTransform)root.transform);
                }
            }
            _host.playOnStart = false;
            var catalog = _project.visuals != null ? _project.visuals : AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(PlotPlaybackExample.CatalogPath);
            _host.sprites = catalog == null ? new List<PlotPlaybackSpriteBinding>() : catalog.sprites.Select(s => new PlotPlaybackSpriteBinding { key = s.key, sprite = s.sprite }).ToList();
            _host.fontAsset = catalog == null ? null : catalog.fontAsset; _host.inlineSprites = catalog == null ? null : catalog.inlineSprites;
            _host.viewPrefab = catalog == null ? null : catalog.viewPrefab; _host.actorPrefabs = catalog == null ? new List<PlotActorPrefabBinding>() : catalog.actorPrefabs;
            _host.audioBindings = catalog == null ? new List<PlotPlaybackAudioBinding>() : catalog.audioBindings; _host.effects = catalog == null ? new List<PlotPlaybackEffectBinding>() : catalog.effects; _host.emotions = catalog == null ? new List<PlotPlaybackEmotionBinding>() : catalog.emotions;
            _host.config = catalog != null && catalog.playerConfig != null ? JsonUtility.FromJson<PlotPlayerConfig>(JsonUtility.ToJson(catalog.playerConfig)) : new PlotPlayerConfig();
            _host.Initialize(data, PlotPlayerRuntimeMode.Editor);
            _bindings = _host.View.CaptureBindings();
            if (_viewport != null) ConfigureEmbeddedView();
            if (auto.HasValue) _host.Core.SetAutoPlay(auto.Value);
            _host.Play(id);
            if (paused) _host.Core.Pause();
            PlotLegacyWidgets.AutoState(_bindings.auto, _host.Core.GetAutoPlay());
            PlotLegacyWidgets.Caption(_bindings.pause, _host.Core.IsPaused ? "继续" : "暂停");
            _prepared = data;
            _lastEntry = id;
        }

        private void ConfigureEmbeddedView()
        {
            // Keep a stable design coordinate system: Fill alone cannot scale fixed dialog/actor
            // positions when the authoring viewport becomes smaller than the original player.
            Fill((RectTransform)_host.transform);
            if (_designRoot == null)
            {
                _designSize = ReadDesignSize();
                var root = new GameObject("PlotWeave Preview Design", typeof(RectTransform));
                _designRoot = (RectTransform)root.transform; _designRoot.SetParent(_host.transform, false);
                _designRoot.anchorMin = _designRoot.anchorMax = _designRoot.pivot = new Vector2(.5f, .5f);
                _designRoot.sizeDelta = _designSize; _designRoot.anchoredPosition = Vector2.zero;
                _designRoot.localRotation = Quaternion.identity;
            }
            var content = _host.View.transform != _host.transform ? _host.View.transform : _bindings.uiRoot.transform;
            if (content.parent != _designRoot) content.SetParent(_designRoot, false);
            // Rebuilt prefabs can add neutral wrappers around their actual UI root. Only those
            // wrappers fill the design surface; local canvases/popups retain their own layout.
            for (var at = _bindings.uiRoot.transform; at != null && at != _designRoot; at = at.parent)
                if (at is RectTransform rect) Fill(rect);
            foreach (var scaler in _host.GetComponentsInChildren<CanvasScaler>(true)) scaler.enabled = false;
            foreach (var canvas in _host.GetComponentsInChildren<Canvas>(true))
                canvas.overrideSorting = false;
            UpdateEmbeddedLayout();
            if (!_layoutBound) { Canvas.willRenderCanvases += UpdateEmbeddedLayout; _layoutBound = true; }
        }

        private Vector2 ReadDesignSize()
        {
            // A prefab's explicit root size wins. Rebuilt legacy roots may be zero-sized;
            // their original CanvasScaler reference resolution then defines design units.
            var source = _host.viewPrefab != null ? _host.viewPrefab : _bindings.uiRoot;
            if (_host.viewPrefab != null && source.transform is RectTransform rect && ValidSize(rect.rect.size)) return rect.rect.size;
            var scaler = source.GetComponentsInChildren<CanvasScaler>(true)
                .FirstOrDefault(s => s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && ValidSize(s.referenceResolution));
            if (scaler != null) return scaler.referenceResolution;
            var sourceBindings = source.GetComponent<PlotPlayerViewBindings>();
            if (sourceBindings != null && sourceBindings.uiRoot != null && sourceBindings.uiRoot.transform is RectTransform surface && ValidSize(surface.rect.size)) return surface.rect.size;
            return ValidSize(_viewport.rect.size) ? _viewport.rect.size : new Vector2(1600, 900);
        }

        private static bool ValidSize(Vector2 size) => size.x > 0 && size.y > 0
            && !float.IsNaN(size.x) && !float.IsNaN(size.y) && !float.IsInfinity(size.x) && !float.IsInfinity(size.y);

        private void UpdateEmbeddedLayout()
        {
            if (_designRoot == null || _viewport == null || !ValidSize(_viewport.rect.size)) return;
            var fit = Mathf.Min(_viewport.rect.width / _designSize.x, _viewport.rect.height / _designSize.y);
            var scale = new Vector3(fit, fit, 1);
            if (_designRoot.localScale != scale) _designRoot.localScale = scale;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            var position = rect.localPosition;
            rect.localPosition = new Vector3(position.x, position.y, 0);
        }
        public void Dispose()
        {
            if (_layoutBound) { Canvas.willRenderCanvases -= UpdateEmbeddedLayout; _layoutBound = false; }
            if (_host != null)
            {
                _host.Core?.Close();
                if (Application.isPlaying) UnityEngine.Object.Destroy(_host.gameObject);
                else UnityEngine.Object.DestroyImmediate(_host.gameObject);
            }
            _host = null; _bindings = null; _prepared = null; _lastEntry = 0; _designRoot = null; _designSize = Vector2.zero;
        }
    }

    [InitializeOnLoad]
    public static class PlotEditorPlaybackBinding
    {
        private static readonly Dictionary<PlotAuthoringPanel, EditorPlotPlayerUI> Players = new Dictionary<PlotAuthoringPanel, EditorPlotPlayerUI>();
        static PlotEditorPlaybackBinding()
        {
            EditorApplication.update += BindPanels;
            EditorApplication.playModeStateChanged += change => { if (change == PlayModeStateChange.ExitingPlayMode) { foreach (var player in Players.Values) player.Dispose(); Players.Clear(); } };
        }
        private static void BindPanels()
        {
            if (!Application.isPlaying || EditorApplication.isCompiling) return;
            foreach (var stale in Players.Keys.Where(p => p == null).ToArray()) { Players[stale].Dispose(); Players.Remove(stale); }
            foreach (var panel in UnityEngine.Object.FindObjectsOfType<PlotAuthoringPanel>())
            {
                if (panel.Session == null || Players.ContainsKey(panel)) continue;
                var player = new EditorPlotPlayerUI(panel.project, () => PlotPlaybackCompiler.Compile(panel.Session)); Players.Add(panel, player);
                panel.BindPreview(player.Play);
            }
        }
        public static EditorPlotPlayerUI GetPlayer(PlotAuthoringPanel panel) => Players.TryGetValue(panel, out var player) ? player : null;
    }
}
