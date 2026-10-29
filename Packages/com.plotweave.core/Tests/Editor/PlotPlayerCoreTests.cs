using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Data;
using PlotWeave.Generation;
using PlotWeave.Playback;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotPlayerCoreTests
    {
        private sealed class Pending : IDisposable
        { public bool Cancelled; public Action<bool, string> Done; public void Dispose() { Cancelled = true; } }
        private sealed class View : IPlotPlayerView
        {
            public bool Immediate = true;
            public string Visible = "", Speaker = "";
            public int Ticks;
            public readonly List<Pending> Loads = new List<Pending>();
            public Action CurtainDone;
            public void Initialize(IPlotPlayerDataProvider data, IPlotResourceLoader resources, IPlotObjectPool pool, PlotPlayerConfig config) { }
            public void Open() { } public void Close() { } public void Clear() { }
            public void RenderDialog(string speaker, string text, int visible) { Speaker = speaker; Visible = text.Substring(0, visible); }
            public IDisposable Present(PlotPlaybackNode node, Action<bool, string> completed)
            { var pending = new Pending { Done = completed }; Loads.Add(pending); if (Immediate) completed(true, ""); return pending; }
            public IDisposable Curtain(string top, string bottom, Action completed) { CurtainDone = completed; return new Pending(); }
            public void Tick(float delta) { Ticks++; }
        }
        private sealed class Environment : IPlotRuntimeEnvironment { public PlotPlayerRuntimeMode Mode => PlotPlayerRuntimeMode.Editor; public float DeltaTime => .1f; }
        private PlotPlaybackDocument _document;
        private PlotPlayerConfig _config;
        private PlotPlayerCore _core;
        private PlotPlayerObjectPool _pool;
        private View _view;
        [SetUp] public void SetUp()
        {
            _document = new PlotPlaybackDocument(); _document.Nodes.Add(new PlotPlaybackNode { ScenesID = 1, Dialog = "hello", Next = 2 }); _document.Nodes.Add(new PlotPlaybackNode { ScenesID = 2 });
            _config = new PlotPlayerConfig { TextSpeed = .1f, AutoDelay = .1f }; _view = new View(); _pool = new PlotPlayerObjectPool();
        }
        [TearDown] public void TearDown() { _core?.Dispose(); _pool.Dispose(); }
        private void Start()
        {
            var provider = new PlotPlayerDataProvider(_document);
            _core = new PlotPlayerCore(provider, new PlotPlayerResourceLoader(), _pool, new PlotPlayerTextProcessor(provider), new Environment(), _config); _core.Init(_view); _core.Open(); _core.LoadPlot(1);
        }
        [Test] public void FirstClickRevealsThenAdvancesAndCompletesExactlyOnce()
        {
            Start(); var ended = 0; _core.OnPlotCompleted += () => ended++;
            Assert.That(_view.Visible, Is.Empty); _core.OnPlayerInput(); Assert.That(_view.Visible, Is.EqualTo("hello")); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(1));
            _core.OnPlayerInput(); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(2)); _core.OnPlayerInput(); _core.OnPlayerInput(); _core.Tick(100);
            Assert.That(ended, Is.EqualTo(1)); Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Completed));
        }
        [Test] public void AutomaticAdvanceWaitsForAllPresentationAndItsSingleDelay()
        {
            _view.Immediate = false; _config.TextSpeed = 0; Start(); _core.SetAutoPlay(true); _core.Tick(10); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(1));
            _view.Loads[0].Done(true, ""); _core.Tick(.05f); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(1)); _core.Tick(.06f);
            Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(2)); Assert.That(_view.Loads.Count, Is.EqualTo(2));
        }
        [Test] public void PauseFreezesTypingPresentationTicksAndInput()
        {
            Start(); _core.Tick(.11f); var visible = _view.Visible; var ticks = _view.Ticks; _core.Pause(); _core.Tick(10); _core.OnPlayerInput();
            Assert.That(_view.Visible, Is.EqualTo(visible)); Assert.That(_view.Ticks, Is.EqualTo(ticks)); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(1));
            _core.Resume(); _core.OnPlayerInput(); Assert.That(_view.Visible, Is.EqualTo("hello"));
        }
        [Test] public void JumpCancelsOldWorkAndIgnoresItsLateError()
        {
            _view.Immediate = false; Start(); var previous = _view.Loads[0]; var errors = 0; _core.OnError += ignored => errors++;
            _core.JumpToNode(2); Assert.That(previous.Cancelled, Is.True); previous.Done(false, "late failure");
            Assert.That(errors, Is.Zero); Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Playing)); Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(2));
        }
        [Test] public void CloseCancelsWorkAndCannotBeResurrectedByCompletion()
        {
            _view.Immediate = false; Start(); var pending = _view.Loads[0]; _core.Close(); pending.Done(true, ""); _core.Tick(20);
            Assert.That(pending.Cancelled, Is.True); Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Closed)); Assert.That(_core.GetCurrentNodeID(), Is.Zero);
        }
        [Test] public void MissingNodeReportsFailureInsteadOfFalselyLoadingIt()
        {
            Start(); var errors = new List<string>(); var loads = new List<int>(); _core.OnError += errors.Add; _core.OnPlotLoaded += loads.Add; _core.LoadPlot(999);
            Assert.That(errors.Count, Is.EqualTo(1)); Assert.That(loads, Is.Empty); Assert.That(_core.GetCurrentNodeID(), Is.Not.EqualTo(999)); Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Faulted));
        }
        [Test] public void UnicodeTypingDoesNotSplitSurrogatesOrCombiningMarks()
        {
            _document.Nodes[0].Dialog = "🙂e\u0301中"; Start(); _core.Tick(.11f); Assert.That(_view.Visible, Is.EqualTo("🙂")); _core.Tick(.11f); Assert.That(_view.Visible, Is.EqualTo("🙂e\u0301"));
        }
        [Test] public void CurtainBlocksAdvanceUntilItsOwnCompletionAndCloseCancelsIt()
        {
            Start(); var calls = 0; _core.ShowCurtainAndLoadNode("Top", "Bottom", 2, () => calls++); var late = _view.CurtainDone; _core.OnPlayerInput();
            Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Playing)); _core.Close(); late(); Assert.That(calls, Is.Zero);
            _core.Open(); _core.ShowCurtainAndLoadNode("Top", "Bottom", 2, () => calls++); _view.CurtainDone(); _view.CurtainDone(); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void TextTablesLanguageAndEscapesAreIsolated()
        {
            _document.Dialogs.Add(new PlotPlaybackText { Key = "shared", Languages = new List<string> { "对白\\n第二行", "", "Dialog", "", "" } });
            _document.Constants.Add(new PlotPlaybackText { Key = "shared", Languages = new List<string> { "角色", "", "Actor", "", "" } });
            var processor = new PlotPlayerTextProcessor(new PlotPlayerDataProvider(_document)); Assert.That(processor.Process("shared"), Is.EqualTo("对白\n第二行")); Assert.That(processor.Process("shared", true), Is.EqualTo("角色"));
            processor.Language = 2; Assert.That(processor.Process("shared"), Is.EqualTo("Dialog")); Assert.That(processor.Process("shared", true), Is.EqualTo("Actor")); Assert.That(processor.Process("\\\\n"), Is.EqualTo("\\n"));
        }
        [Test] public void ProviderCopiesInputAndDoesNotLeakMutableNodeLists()
        {
            var provider = new PlotPlayerDataProvider(_document); _document.Nodes[0].Next = 999; provider.TryGetNode(1, out var first); Assert.That(first.Next, Is.EqualTo(2)); first.Next = 888;
            provider.TryGetNode(1, out var second); Assert.That(second.Next, Is.EqualTo(2));
        }
        [Test] public void ExplicitLegacyNextModeCanBypassBranchSelection()
        {
            _config.EnableBranches = false;
            _document.Nodes[0].Branch.Add(new PlotBranch { Key = "optional", Target = 3 }); _document.Nodes.Add(new PlotPlaybackNode { ScenesID = 3 }); Start(); _core.OnPlayerInput(); _core.OnPlayerInput();
            Assert.That(_core.GetCurrentNodeID(), Is.EqualTo(2)); Assert.That(((PlotPlayerDataProvider)_core.GetDataProvider()).TryGetNode(1, out var node), Is.True); Assert.That(node.Branch[0].Target, Is.EqualTo(3));
        }
        [Test] public void GeneratedEditorAndGameAdaptersProduceTheSamePlaybackSnapshot()
        {
            var data = PlotGenerationExample.CreateDefault();
            try
            {
                object Adapter(string name, object source)
                { var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlotWeave.Generated.Stage02.Default." + name)).First(t => t != null); return Activator.CreateInstance(type, new[] { source }); }
                var editor = PlotPlaybackNode.Read(Adapter("EditorPlotAtomDataAdapter", data.Row));
                var game = PlotPlaybackNode.Read(Adapter("GamePlotAtomDataAdapter", AuthoringPlotValueSource.FromRow(data.Row)));
                Assert.That(JsonUtility.ToJson(game), Is.EqualTo(JsonUtility.ToJson(editor))); Assert.That(game.RoleAction[1].ActionType, Is.EqualTo("Shake")); Assert.That(game.Branch.Count, Is.EqualTo(2));
            }
            finally { foreach (var asset in data.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void InvalidTimingAndPositionConfigFailsBeforePlayback()
        {
            _config.TextSpeed = float.NaN; Assert.Throws<ArgumentException>(() => Start());
        }
        [Test] public void ExtremelyLargeTicksFinishTypingWithoutIntegerOverflow()
        { Start(); _core.Tick(float.MaxValue); Assert.That(_view.Visible, Is.EqualTo("hello")); Assert.That(_core.State, Is.EqualTo(PlotPlayerState.Playing)); }
    }
}
