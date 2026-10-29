using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using PlotWeave.Legacy;
using PlotWeave.Legacy.Editor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    /// <summary>Malformed in-memory templates must fail closed without leaking a working session.</summary>
    public sealed class PlotAuthoringInitializationTests
    {
        private GameObject _root;
        private PlotLegacyAuthoringHost _host;
        private PlotAuthoringProject _project;
        private PlotDialogKeyAsset _dialogs;
        private PlotConstantKeyAsset _constants;
        private MemoryStore _store;
        private string _prefix;

        [SetUp] public void SetUp()
        {
            _prefix = "PlotWeaveInitMemory_" + Guid.NewGuid().ToString("N");
            _project = ScriptableObject.CreateInstance<PlotAuthoringProject>(); _project.name = _prefix;
            _dialogs = ScriptableObject.CreateInstance<PlotDialogKeyAsset>(); _dialogs.name = _prefix + "_Dialogs";
            _dialogs.AddDialogKey("source.dialog"); _dialogs.SetDialogContent("source.dialog", 0, "unchanged source");
            _constants = ScriptableObject.CreateInstance<PlotConstantKeyAsset>(); _constants.name = _prefix + "_Constants";
            _project.dialogs = _dialogs; _project.constants = _constants; _store = new MemoryStore(_project);
            // Deliberately omit CurrentNodeEditor: Initialize creates its working texts first,
            // then encounters a real template-contract failure before any editing can begin.
            _root = new GameObject("PlotEditorCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(PlotLegacyTemplateBindings));
            _host = _root.AddComponent<PlotLegacyAuthoringHost>(); _host.project = _project;
            _host.template = _root.GetComponent<PlotLegacyTemplateBindings>();
        }
        [TearDown] public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_project); UnityEngine.Object.DestroyImmediate(_dialogs); UnityEngine.Object.DestroyImmediate(_constants);
        }
        private int[] TextObjectIds() => Resources.FindObjectsOfTypeAll<PlotTextKeyAsset>()
            .Where(t => t.name.StartsWith(_prefix, StringComparison.Ordinal)).Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();

        [Test] public void TemplateFailureReleasesWorkingTextsAndShowsReadonlyFailureWithoutChangingSource()
        {
            var before = TextObjectIds(); var source = JsonUtility.ToJson(_dialogs);
            Assert.Throws<InvalidOperationException>(() => _host.Initialize(_store));
            Assert.That(_host.Session, Is.Null); Assert.That(_host.InitializationReady, Is.False);
            Assert.That(_host.InitializationFailed, Is.True); Assert.That(_host.InitializationError, Is.Not.Empty);
            CollectionAssert.AreEqual(before, TextObjectIds()); Assert.That(JsonUtility.ToJson(_dialogs), Is.EqualTo(source));
            Assert.That(_store.SaveCalls, Is.Zero);
            var group = _root.GetComponent<CanvasGroup>(); Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            Assert.That(_root.GetComponent<GraphicRaycaster>().enabled, Is.False);
            Assert.That(_root.GetComponentsInChildren<Text>(true).Single(t => t.name == "InitializationFailure").text, Does.Contain("初始化失败"));
            var invoked = false; _host.Safe(() => invoked = true); Assert.That(invoked, Is.False);
        }
        [Test] public void FailedHostCannotReinitializeOrDeclareAnUnwiredSessionReady()
        {
            Assert.Throws<InvalidOperationException>(() => _host.Initialize(_store));
            var reads = _store.ProjectReads; var failure = _host.InitializationError;
            Assert.Throws<InvalidOperationException>(() => _host.Initialize(_store));
            Assert.Throws<InvalidOperationException>(() => _host.CompleteInitialization());
            Assert.That(_store.ProjectReads, Is.EqualTo(reads)); Assert.That(_host.InitializationError, Is.EqualTo(failure));
            Assert.That(_host.Session, Is.Null); Assert.That(_host.InitializationReady, Is.False);
        }
        [Test] public void AbortClosesOwnedPreviewOnceAndRemovesAllSessionEntrypoints()
        {
            var closed = 0; _host.Preview = _ => { }; _host.ValidatePreview = () => { }; _host.RefreshPreview = () => { };
            _host.ClosePreview = () => closed++; _host.OpenDataExchange = () => { };
            var error = new InvalidOperationException("in-memory binding failure");
            _host.AbortInitialization(error); _host.AbortInitialization(error);
            Assert.That(closed, Is.EqualTo(1)); Assert.That(_host.Preview, Is.Null); Assert.That(_host.ValidatePreview, Is.Null);
            Assert.That(_host.RefreshPreview, Is.Null); Assert.That(_host.ClosePreview, Is.Null); Assert.That(_host.OpenDataExchange, Is.Null);
            Assert.That(_host.Session, Is.Null); Assert.That(_host.InitializationReady, Is.False); Assert.That(_store.SaveCalls, Is.Zero);
            Assert.That(_root.GetComponentsInChildren<Text>(true).Count(t => t.name == "InitializationFailure"), Is.EqualTo(1));
        }
        [Test] public void BootstrapFailureBeforeSessionCreationIsLoggedOnceAndNotRetried()
        {
            // The real asset-store constructor rejects this memory project's missing header
            // before it can read folders. Exercise the actual bootstrap catch and retry guard.
            var bind = typeof(PlotLegacyAuthoringBootstrap).GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bind, Is.Not.Null);
            LogAssert.Expect(LogType.Error, new Regex("PLOTWEAVE_AUTHORING_INIT_FAILED", RegexOptions.Singleline));
            bind.Invoke(null, new object[] { _host }); var failure = _host.InitializationError;
            bind.Invoke(null, new object[] { _host });
            Assert.That(_host.InitializationFailed, Is.True); Assert.That(_host.InitializationReady, Is.False);
            Assert.That(_host.InitializationError, Is.EqualTo(failure)); Assert.That(_host.Session, Is.Null);
        }
        private sealed class MemoryStore : IPlotAuthoringStore
        {
            private readonly PlotAuthoringProject _project;
            public int ProjectReads { get; private set; }
            public int SaveCalls { get; private set; }
            public PlotAuthoringProject Project { get { ProjectReads++; return _project; } }
            public MemoryStore(PlotAuthoringProject project) { _project = project; }
            public List<PlotAtomRowData> ListRows(int? from = null, int? to = null) => new List<PlotAtomRowData>();
            public PlotAtomRowData FindRow(int id) => null;
            public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
            { SaveCalls++; throw new InvalidOperationException("Memory initialization tests cannot save"); }
            public PlotAtomRowData CreateRow(int id) => throw new NotSupportedException();
            public PlotAtomRowData RenameRow(int oldId, int newId) => throw new NotSupportedException();
            public bool DeleteRow(int id) => throw new NotSupportedException();
        }
    }
}
