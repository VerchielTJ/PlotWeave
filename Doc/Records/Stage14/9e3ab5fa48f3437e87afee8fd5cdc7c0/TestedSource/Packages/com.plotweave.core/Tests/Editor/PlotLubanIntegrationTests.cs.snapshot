using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Excel;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using PlotWeave.Data;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotLubanIntegrationTests
    {
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("请先执行真实Luban样例生成并等待编译: " + name);
        public sealed class Loader<T> { public T Load(string name) => (T)Activator.CreateInstance(typeof(T), File.ReadAllBytes(PlotLubanExample.Generated + "/Data/" + name + ".bytes")); }
        private static object Tables()
        {
            var buf = Find("Luban.ByteBuf"); var loader = Activator.CreateInstance(typeof(Loader<>).MakeGenericType(buf)); var callback = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), buf), loader, "Load"); return Activator.CreateInstance(Find("PlotWeaveLuban.Tables"), callback);
        }
        private static object Member(object value, string member) => PlotValueReader.Required<object>(new ObjectPlotValueSource(value), member);
        private static IEnumerable Rows(object tables, string member) => (IEnumerable)Member(Member(tables, member), "DataList");
        [Test] public void RealBinaryTablesPreserveRepeatedBeansEnumsAndBranchesThroughGeneratedGameAdapter()
        {
            var tables = Tables(); var raw = Rows(tables, "TbPlotAtom").Cast<object>().Single(n => (int)Member(n, "ScenesID") == 1001); var adapter = Activator.CreateInstance(Find("PlotWeave.Generated.Stage02.Default.GamePlotAtomDataAdapter"), raw); var node = PlotPlaybackNode.Read(adapter);
            Assert.That(node.RoleAction.Count, Is.EqualTo(2)); Assert.That(node.RoleAction[1].RoleID, Is.EqualTo(102)); Assert.That(node.RoleAction[1].ActionType, Is.EqualTo("Shake")); Assert.That(node.Branch.Count, Is.EqualTo(2)); Assert.That(node.Branch[1].Target, Is.EqualTo(1003));
        }
        [Test] public void RealBinaryTextAndActorTablesReachTheSameSharedCore()
        {
            var tables = Tables(); var adapterType = Find("PlotWeave.Generated.Stage02.Default.GamePlotAtomDataAdapter"); var document = PlotLubanTableReader.Read(Rows(tables, "TbPlotAtom"), Rows(tables, "TbActor"), Rows(tables, "TbDialog"), Rows(tables, "TbConstant"), raw => Activator.CreateInstance(adapterType, raw));
            var expected = PlotPlaybackCompiler.Compile(AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(PlotPlaybackExample.Root + "/Workspace/Workspace.asset")); Assert.That(JsonUtility.ToJson(document), Is.EqualTo(JsonUtility.ToJson(expected)));
            var go = new GameObject("LubanBinaryPlayer");
            try { var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false; host.config.TextSpeed = 0; var visuals = AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(PlotPlaybackExample.CatalogPath); host.sprites = visuals.sprites; host.Initialize(document, PlotPlayerRuntimeMode.GameRuntime); host.Play(1001); host.Core.Tick(1); Assert.That(host.View.ActorCount, Is.EqualTo(2)); Assert.That(host.View.VisibleText, Does.Contain("类型与数据迁移样例")); host.Core.OnPlayerInput(); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.WaitingForChoice)); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void TextShapeNormalizationAlsoWorksWithTheGeneratedTextTableAdapter()
        {
            var table = Rows(Tables(), "TbDialog").Cast<object>().Select(PlotLubanTableReader.TextValueSource).ToArray(); var adapter = Activator.CreateInstance(Find("PlotWeave.Generated.Stage02.Default.GamePlotDialogKeyTableAdapter"), new object[] { table }); var text = (IPlotTextData)adapter; Assert.That(text.GetText("demo.start", 0), Does.Contain("类型与数据迁移样例"));
        }
        [Test] public void SchemaAdapterKeepsContinuationFieldsUnderTheirRootAndAvoidsDuplicateAliases()
        {
            var old = new PlotExcelSheet(); old.Set(3, 1, "Mood"); old.Set(3, 6, "Happy"); old.Set(3, 7, "Happy"); old.Set(4, 6, "Sad"); var sheet = PlotLubanSchema.Enums(old); Assert.That(sheet.Get(3, 7), Is.EqualTo("Happy")); Assert.That(sheet.Get(3, 8), Is.Empty); Assert.That(sheet.Get(4, 1), Is.Empty); Assert.That(sheet.Get(4, 7), Is.EqualTo("Sad")); Assert.That(sheet.MergedRanges, Does.Contain("H1:L1"));
        }
        [TestCase("Assets/../Outside")][TestCase("Packages/Generated")][TestCase("Assets")]
        public void GenerationCannotEscapeItsAssetsSubdirectory(string path) => Assert.Throws<ArgumentException>(() => PlotLubanPipeline.RequireOutput(path));
        [Test] public void EventSidecarRejectsUnknownNodesBeforeMutatingTheDocument()
        {
            var document = new PlotPlaybackDocument { Nodes = new List<PlotPlaybackNode> { new PlotPlaybackNode { ScenesID = 1 } } }; var tracks = ScriptableObject.CreateInstance<PlotNodeEventTrackAsset>();
            try { tracks.tracks.Add(new PlotNodeEventTrack { nodeID = 1, events = new List<PlotWeave.Legacy.PlotEventJsonData> { new PlotWeave.Legacy.PlotEventJsonData("wait") { parameters = new List<string> { ".1" } } } }); var valid = tracks.ExportJson(); tracks.tracks.Add(new PlotNodeEventTrack { nodeID = 99 }); Assert.Throws<FormatException>(() => PlotLubanTableReader.ApplyEventSidecar(document, tracks.ExportJson())); Assert.That(document.Nodes[0].Events, Is.Empty); PlotLubanTableReader.ApplyEventSidecar(document, valid); Assert.That(document.Nodes[0].Events.Single().eventName, Is.EqualTo("wait")); }
            finally { UnityEngine.Object.DestroyImmediate(tracks); }
        }
    }
}
