using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotPlaybackIntegrationTests
    {
        private string _folder;
        private PlotAuthoringProject _project;
        private readonly List<GameObject> _hosts = new List<GameObject>();
        private Sprite _sprite;
        private Texture2D _texture;
        [SetUp] public void SetUp()
        {
            _folder = "Assets/PlotWeavePlaybackTest_" + Guid.NewGuid().ToString("N"); _project = PlotAuthoringBootstrap.CreateWorkspace(_folder);
            _texture = new Texture2D(8, 8); _sprite = Sprite.Create(_texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f));
        }
        [TearDown] public void TearDown()
        {
            foreach (var host in _hosts) if (host != null) UnityEngine.Object.DestroyImmediate(host); _hosts.Clear();
            UnityEngine.Object.DestroyImmediate(_sprite); UnityEngine.Object.DestroyImmediate(_texture); Undo.FlushUndoRecordObjects();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { _folder })) { var value = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)); if (value != null) Undo.ClearUndo(value); }
            if (_folder.StartsWith("Assets/PlotWeavePlaybackTest_", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(_folder)) AssetDatabase.DeleteAsset(_folder);
        }
        private PlotPlayerHost Host(PlotPlaybackDocument data)
        {
            var go = new GameObject("SharedPlayerTest"); _hosts.Add(go); var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false;
            host.config.EnableBranches = false;
            foreach (var key in new[] { "demo.stage", "demo.guide", "demo.traveler" }) host.sprites.Add(new PlotPlaybackSpriteBinding { key = key, sprite = _sprite });
            host.Initialize(data, PlotPlayerRuntimeMode.GameRuntime); return host;
        }
        [Test] public void WorkspaceAssetAndJsonHostsRunTheSameCoreAndReachSameResult()
        {
            var data = PlotPlaybackCompiler.Compile(_project); var json = JsonUtility.FromJson<PlotPlaybackDocument>(JsonUtility.ToJson(data));
            var editor = Host(data); var game = Host(json);
            var first = new List<int>(); var second = new List<int>(); editor.Core.OnPlotLoaded += first.Add; game.Core.OnPlotLoaded += second.Add;
            foreach (var host in new[] { editor, game })
            { host.Play(1001); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Does.Contain("类型与数据迁移样例")); host.Core.OnPlayerInput(); host.Core.OnPlayerInput(); }
            CollectionAssert.AreEqual(first, second); Assert.That(first, Is.EqualTo(new[] { 1001, 1002 })); Assert.That(editor.Core.State, Is.EqualTo(PlotPlayerState.Completed)); Assert.That(game.Core.State, Is.EqualTo(editor.Core.State));
        }
        [Test] public void RoleTheatreRetainsCurrentActorsAndReturnsThoseAbsentFromNextNode()
        {
            var host = Host(PlotPlaybackCompiler.Compile(_project)); host.Play(1001); Assert.That(host.View.ActorCount, Is.EqualTo(2)); var objects = host.View.ActorObjects.ToArray();
            host.Core.LoadPlot(1002); Assert.That(host.View.ActorCount, Is.Zero); Assert.That(objects.All(o => !o.activeSelf), Is.True);
            host.Core.LoadPlot(1001); Assert.That(host.View.ActorObjects.All(o => objects.Contains(o)), Is.True); Assert.That(host.View.BackgroundKey, Is.EqualTo("demo.stage"));
        }
        [Test] public void ActorActionsReachHostHooksWithoutDiscardingEmoteData()
        {
            var host = Host(PlotPlaybackCompiler.Compile(_project)); var actions = new List<PlotRoleAction>(); host.View.ActorActionRequested += (action, ignored) => actions.Add(action); host.Play(1001);
            Assert.That(actions.Count, Is.EqualTo(2)); Assert.That(actions[1].ActionType, Is.EqualTo("Shake")); Assert.That(actions[1].Emote, Is.EqualTo("Happy"));
        }
        [Test] public void ObjectPoolRejectsDoubleReturnAndReusesOwnedObjects()
        {
            var go = new GameObject("PoolTest"); _hosts.Add(go);
            using (var pool = new PlotPlayerObjectPool())
            {
                var first = pool.Rent("actor", go.transform, () => new GameObject("Actor")); pool.Return(first);
                Assert.Throws<InvalidOperationException>(() => pool.Return(first)); var second = pool.Rent("actor", go.transform, () => throw new Exception("应复用")); Assert.That(second, Is.SameAs(first));
            }
        }
        [Test] public void CurtainTicksCompleteOnceAndCanBeReplaced()
        {
            var host = Host(PlotPlaybackCompiler.Compile(_project)); host.Play(1001); var count = 0; host.Core.ShowCurtain("A", "B", () => count++);
            host.Core.ShowCurtain("C", "D", () => count++); host.Core.Tick(4); host.Core.Tick(4); Assert.That(count, Is.EqualTo(1));
        }
        [Test] public void InvalidRefreshPreservesThePreviouslyWorkingHost()
        {
            var host = Host(PlotPlaybackCompiler.Compile(_project)); host.Play(1001); var previous = host.Core;
            Assert.Throws<FormatException>(() => host.Initialize(new PlotPlaybackDocument { FormatVersion = 99 }, PlotPlayerRuntimeMode.Editor));
            Assert.That(host.Core, Is.SameAs(previous)); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Is.Not.Empty);
        }
        private sealed class LateLoader : IPlotResourceLoader
        {
            private sealed class Ticket : IDisposable { public void Dispose() { } }
            public readonly List<Action<Sprite, string>> Pending = new List<Action<Sprite, string>>();
            public IDisposable LoadSprite(string key, Action<Sprite, string> completed) { Pending.Add(completed); return new Ticket(); }
        }
        [Test] public void LateSpriteCallbacksCannotRepopulateTheatreAfterClose()
        {
            var data = PlotPlaybackCompiler.Compile(_project); var provider = new PlotPlayerDataProvider(data); var loader = new LateLoader(); var go = new GameObject("LateResourceTest"); _hosts.Add(go); var view = go.AddComponent<PlotPlayerView>();
            using (var pool = new PlotPlayerObjectPool()) using (var core = new PlotPlayerCore(provider, loader, pool, new PlotPlayerTextProcessor(provider), new PlotPlayerRuntimeEnvironment(PlotPlayerRuntimeMode.Editor), new PlotPlayerConfig()))
            {
                core.Init(view); core.Open(); core.LoadPlot(1001); core.Close(); foreach (var complete in loader.Pending) complete(_sprite, "");
                Assert.That(view.ActorCount, Is.Zero); Assert.That(view.BackgroundKey, Is.Empty); Assert.That(core.State, Is.EqualTo(PlotPlayerState.Closed));
            }
        }
    }
}
