using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    /// <summary>Memory-only authoring previews: no workspace generation or AssetDatabase save.</summary>
    public sealed class PlotAuthoringEmbeddedPreviewTests
    {
        private PlotAuthoringProject _project;
        private PlotPlaybackVisualCatalog _catalog;
        private PlotPlaybackDocument _draft;
        private GameObject _editor, _template;
        private RectTransform _viewport, _input;
        private EditorPlotPlayerUI _player;
        private HashSet<EventSystem> _existingEvents;
        private int _snapshots;

        [SetUp] public void SetUp()
        {
            _existingEvents = new HashSet<EventSystem>(Resources.FindObjectsOfTypeAll<EventSystem>());
            _project = ScriptableObject.CreateInstance<PlotAuthoringProject>();
            _catalog = ScriptableObject.CreateInstance<PlotPlaybackVisualCatalog>();
            _catalog.playerConfig = new PlotPlayerConfig { TextSpeed = 0 };
            _project.visuals = _catalog; _draft = Document("working draft"); _snapshots = 0;
            _editor = new GameObject("AuthoringCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _editor.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _viewport = Rect("WindowMask", _editor.transform); _viewport.gameObject.AddComponent<RectMask2D>();
            _viewport.anchorMin = Vector2.zero; _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(0, 420); _viewport.offsetMax = new Vector2(-1040, 0);
            _input = Rect("AuthoringInput", _editor.transform); _input.sizeDelta = new Vector2(1040, 420);
        }
        [TearDown] public void TearDown()
        {
            _player?.Dispose();
            if (_template != null) UnityEngine.Object.DestroyImmediate(_template);
            if (_editor != null) UnityEngine.Object.DestroyImmediate(_editor);
            UnityEngine.Object.DestroyImmediate(_project); UnityEngine.Object.DestroyImmediate(_catalog);
            foreach (var events in Resources.FindObjectsOfTypeAll<EventSystem>())
                if (!_existingEvents.Contains(events) && events.gameObject.scene.IsValid()) UnityEngine.Object.DestroyImmediate(events.gameObject);
        }

        [Test] public void RefreshBeforePlayingPreparesLiveDataWithoutOpeningAPlayer()
        {
            Player(); var childCount = _viewport.childCount; _player.Refresh(1);
            Assert.That(_snapshots, Is.EqualTo(1)); Assert.That(_player.Host, Is.Null);
            Assert.That(_player.IsPreviewActive, Is.False); Assert.That(_viewport.childCount, Is.EqualTo(childCount));
        }
        [Test] public void EmbeddedWrappersFillExistingViewportAndKeepEditorCanvasScaling()
        {
            _template = Template(); _catalog.viewPrefab = _template; Player(); _player.Play(1);
            Assert.That(_player.Host.transform.parent, Is.SameAs(_viewport)); AssertFill((RectTransform)_player.Host.transform);
            AssertFill((RectTransform)_player.Host.View.transform);
            var bindings = _player.Host.View.CaptureBindings(); AssertFill((RectTransform)bindings.uiRoot.transform);
            var canvases = _player.Host.GetComponentsInChildren<Canvas>(true); Assert.That(canvases, Is.Not.Empty);
            foreach (var canvas in canvases)
            {
                Assert.That(canvas.isRootCanvas, Is.False); Assert.That(canvas.rootCanvas, Is.SameAs(_editor.GetComponent<Canvas>()));
                Assert.That(canvas.overrideSorting, Is.False); AssertFill((RectTransform)canvas.transform);
            }
            Assert.That(_player.Host.GetComponentsInChildren<CanvasScaler>(true).All(s => !s.enabled), Is.True);
            Assert.That(_editor.GetComponent<CanvasScaler>().enabled, Is.True);
            Assert.That(_viewport.GetComponents<RectMask2D>().Length, Is.EqualTo(1));
            Assert.That(_input.gameObject.activeInHierarchy, Is.True); Assert.That(_input.sizeDelta, Is.EqualTo(new Vector2(1040, 420)));
        }
        [Test] public void CloseButtonClosesOnlyPlaybackAndRefreshDoesNotReopenIt()
        {
            Player(); _player.Play(1); _player.Host.View.CaptureBindings().close.onClick.Invoke(); var core = _player.Host.Core;
            Assert.That(_player.IsPreviewActive, Is.False); _draft = Document("edited while closed"); _player.Refresh();
            Assert.That(_player.Host.Core, Is.SameAs(core)); Assert.That(core.State, Is.EqualTo(PlotPlayerState.Closed));
            Assert.That(_editor.activeInHierarchy && _viewport.gameObject.activeInHierarchy && _input.gameObject.activeInHierarchy, Is.True);
            _player.Play(1); Assert.That(_player.Host.View.FullText, Is.EqualTo("edited while closed"));
        }
        [Test] public void SmallViewportUniformlyFitsFixedDialogAndStageInDesignCoordinates()
        {
            _template = Template(); _catalog.viewPrefab = _template; SetViewportSize(new Vector2(960, 540)); Player(); _player.Play(1);
            Canvas.ForceUpdateCanvases(); var bindings = _player.Host.View.CaptureBindings();
            var design = (RectTransform)_player.Host.View.transform.parent;
            Assert.That(design.rect.size, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(design.localScale.x, Is.EqualTo(.5f).Within(.0001f)); Assert.That(design.localScale.y, Is.EqualTo(design.localScale.x));
            Assert.That(bindings.dialog.rectTransform.rect.width, Is.EqualTo(1565));
            AssertInsideViewport((RectTransform)bindings.dialogBox.transform); AssertInsideViewport(bindings.dialog.rectTransform); AssertInsideViewport(bindings.stage);
        }
        [Test] public void ViewportResizeRefitsTheSamePreviewWithoutStretchingItsInternalLayout()
        {
            _template = Template(); _catalog.viewPrefab = _template; SetViewportSize(new Vector2(960, 540)); Player(); _player.Play(1);
            var core = _player.Host.Core; var bindings = _player.Host.View.CaptureBindings();
            SetViewportSize(new Vector2(800, 600)); Canvas.ForceUpdateCanvases();
            var design = (RectTransform)_player.Host.View.transform.parent;
            Assert.That(design.localScale.x, Is.EqualTo(800f / 1920f).Within(.0001f)); Assert.That(design.localScale.y, Is.EqualTo(design.localScale.x));
            Assert.That(_player.Host.Core, Is.SameAs(core)); Assert.That(bindings.dialog.rectTransform.rect.width, Is.EqualTo(1565));
            AssertInsideViewport((RectTransform)bindings.dialogBox.transform); AssertInsideViewport(bindings.dialog.rectTransform); AssertInsideViewport(bindings.stage);
        }
        [Test] public void ZeroSizedLegacyWrapperUsesCanvasReferenceAsItsDesignResolution()
        {
            _template = Template(); var root = (RectTransform)_template.transform; root.sizeDelta = Vector2.zero; root.localScale = Vector3.zero;
            var scaler = _template.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            _catalog.viewPrefab = _template; SetViewportSize(new Vector2(960, 540)); Player(); _player.Play(1); Canvas.ForceUpdateCanvases();
            var design = (RectTransform)_player.Host.View.transform.parent;
            Assert.That(design.rect.size, Is.EqualTo(new Vector2(1920, 1080))); Assert.That(design.localScale.x, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(_player.Host.View.transform.localScale, Is.EqualTo(Vector3.one));
            AssertInsideViewport((RectTransform)_player.Host.View.CaptureBindings().dialogBox.transform);
        }
        [Test] public void NestedLocalCanvasKeepsItsOwnSizeAndPositionWithinTheDesignSurface()
        {
            _template = Template(); var surface = _template.GetComponent<PlotPlayerViewBindings>().uiRoot.transform;
            var local = Rect("LocalControlsCanvas", surface); local.sizeDelta = new Vector2(300, 100); local.anchoredPosition = new Vector2(10, 200);
            local.gameObject.AddComponent<Canvas>().overrideSorting = true; local.gameObject.AddComponent<CanvasScaler>();
            _catalog.viewPrefab = _template; SetViewportSize(new Vector2(960, 540)); Player(); _player.Play(1);
            var clone = _player.Host.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "LocalControlsCanvas");
            Assert.That(clone.sizeDelta, Is.EqualTo(new Vector2(300, 100))); Assert.That(clone.anchoredPosition, Is.EqualTo(new Vector2(10, 200)));
            Assert.That(clone.GetComponent<Canvas>().overrideSorting, Is.False); Assert.That(clone.GetComponent<CanvasScaler>().enabled, Is.False);
        }
        [Test] public void RefreshReplaysUnsavedCurrentNodeAndRetainsAutoPauseSettings()
        {
            Player(); _player.Play(1); _player.Host.Core.SetAutoPlay(true); _player.Host.Core.Pause();
            var previous = _player.Host.Core; _draft = Document("new unsaved text"); _player.Refresh();
            Assert.That(_player.Host.Core, Is.Not.SameAs(previous)); Assert.That(_player.Host.Core.GetCurrentNodeID(), Is.EqualTo(1));
            Assert.That(_player.Host.View.FullText, Is.EqualTo("new unsaved text"));
            Assert.That(_player.Host.Core.GetAutoPlay(), Is.True); Assert.That(_player.Host.Core.IsPaused, Is.True);
            Assert.That(_player.Host.View.CaptureBindings().pause.GetComponentInChildren<Text>().text, Is.EqualTo("继续"));
            Assert.That(_snapshots, Is.EqualTo(2));
        }
        [Test] public void RefreshCanReplayAnExplicitEntryFromLatestDraft()
        {
            Player(); _player.Play(1); _draft.Nodes.Add(new PlotPlaybackNode { ScenesID = 2, Dialog = "second draft node" }); _player.Refresh(2);
            Assert.That(_player.Host.Core.GetCurrentNodeID(), Is.EqualTo(2)); Assert.That(_player.Host.View.FullText, Is.EqualTo("second draft node"));
        }
        [Test] public void CompletedPreviewCanRefreshButExplicitlyHiddenViewStaysClosed()
        {
            Player(); _player.Play(1); _player.Host.Core.OnPlayerInput(); Assert.That(_player.Host.Core.State, Is.EqualTo(PlotPlayerState.Completed));
            Assert.That(_player.IsPreviewActive, Is.True); _draft = Document("revised ending"); _player.Refresh();
            Assert.That(_player.Host.Core.State, Is.EqualTo(PlotPlayerState.Playing)); Assert.That(_player.Host.View.FullText, Is.EqualTo("revised ending"));
            _player.Host.Core.OnPlayerInput(); _player.Host.View.Close(); var completed = _player.Host.Core; _player.Refresh(1);
            Assert.That(_player.IsPreviewActive, Is.False); Assert.That(_player.Host.Core, Is.SameAs(completed));
        }
        [Test] public void InvalidDraftOrEntryPreservesWorkingPreview()
        {
            Player(); _player.Play(1); var working = _player.Host.Core; _draft = new PlotPlaybackDocument { FormatVersion = 99 };
            Assert.Throws<FormatException>(() => _player.Refresh()); Assert.That(_player.Host.Core, Is.SameAs(working));
            _draft = Document("valid replacement"); Assert.Throws<InvalidOperationException>(() => _player.Refresh(999));
            Assert.That(_player.Host.Core, Is.SameAs(working)); Assert.That(_player.Host.View.FullText, Is.EqualTo("working draft"));
        }
        [Test] public void NoViewportKeepsStandaloneCanvasBehaviorAndLegacyConstructorRemainsAvailable()
        {
            using (var legacy = new EditorPlotPlayerUI(_project)) Assert.That(legacy.Host, Is.Null);
            _player = new EditorPlotPlayerUI(_project, () => _draft); _player.Play(1);
            Assert.That(_player.Host.transform.parent, Is.Null);
            Assert.That(_player.Host.GetComponentInChildren<Canvas>().isRootCanvas, Is.True);
            Assert.That(_player.Host.GetComponentInChildren<CanvasScaler>().enabled, Is.True);
        }
        private void Player() => _player = new EditorPlotPlayerUI(_project, () => { _snapshots++; return _draft; }, _viewport);
        private void SetViewportSize(Vector2 size)
        { _viewport.anchorMin = _viewport.anchorMax = _viewport.pivot = new Vector2(.5f, .5f); _viewport.sizeDelta = size; _viewport.anchoredPosition = Vector2.zero; }
        private void AssertInsideViewport(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = _viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(_viewport.rect.xMin - .01f, _viewport.rect.xMax + .01f));
                Assert.That(local.y, Is.InRange(_viewport.rect.yMin - .01f, _viewport.rect.yMax + .01f));
            }
        }
        private static PlotPlaybackDocument Document(string text) => new PlotPlaybackDocument
        {
            Nodes = new List<PlotPlaybackNode> { new PlotPlaybackNode { ScenesID = 1, Dialog = "draft.dialog" } },
            Dialogs = new List<PlotPlaybackText> { new PlotPlaybackText { Key = "draft.dialog", Languages = new List<string> { text, text, text, text, text } } }
        };
        private static RectTransform Rect(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static T Widget<T>(string name, Transform parent) where T : Component => Rect(name, parent).gameObject.AddComponent<T>();
        private static Button Button(string name, Transform parent)
        { var button = Widget<Button>(name, parent); Widget<Text>("Caption", button.transform).text = name; return button; }
        private static GameObject Template()
        {
            // Rebuilt legacy views have a neutral root plus an offset inner UI surface.
            var root = new GameObject("NeutralPlayerWrapper", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080); root.GetComponent<Canvas>().overrideSorting = true;
            var surface = Rect("OriginalPlayerSurface", root.transform); surface.sizeDelta = new Vector2(2160, 1080); surface.anchoredPosition = new Vector2(100, 50);
            var bindings = root.AddComponent<PlotPlayerViewBindings>(); bindings.uiRoot = surface.gameObject;
            bindings.stage = Rect("Stage", surface); bindings.stage.anchorMin = Vector2.zero; bindings.stage.anchorMax = Vector2.one; bindings.stage.offsetMin = bindings.stage.offsetMax = Vector2.zero;
            bindings.background = Widget<Image>("Background", surface);
            var dialogBox = Rect("DialogBox", surface); bindings.dialogBox = dialogBox.gameObject;
            dialogBox.anchorMin = Vector2.zero; dialogBox.anchorMax = new Vector2(1, 0); dialogBox.pivot = new Vector2(.5f, 0); dialogBox.sizeDelta = new Vector2(0, 440); dialogBox.anchoredPosition = Vector2.zero;
            bindings.dialog = Widget<TextMeshProUGUI>("Dialog", bindings.dialogBox.transform);
            var textRect = bindings.dialog.rectTransform; textRect.anchorMin = new Vector2(.5f, 0); textRect.anchorMax = new Vector2(.5f, 1); textRect.sizeDelta = new Vector2(1565, -266); textRect.anchoredPosition = new Vector2(75, -80);
            bindings.speaker = Widget<TextMeshProUGUI>("Speaker", surface); bindings.curtain = Widget<CanvasGroup>("Curtain", surface);
            bindings.curtainTop = Widget<TextMeshProUGUI>("TopTitle", bindings.curtain.transform); bindings.curtainBottom = Widget<TextMeshProUGUI>("BottomTitle", bindings.curtain.transform);
            bindings.nodeLabel = Widget<Text>("Node", surface); bindings.next = Button("Next", surface); bindings.auto = Button("Auto", surface);
            bindings.pause = Button("Pause", surface); bindings.close = Button("Close", surface); bindings.Validate(); root.SetActive(false); return root;
        }
        private static void AssertFill(RectTransform rect)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero)); Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin.sqrMagnitude, Is.LessThan(.001f)); Assert.That(rect.offsetMax.sqrMagnitude, Is.LessThan(.001f));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
