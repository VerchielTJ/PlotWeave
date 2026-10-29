using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Editor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotWeaveTests
    {
        private string _folder;
        [SetUp] public void Before() { _folder = Path.Combine(Path.GetTempPath(), "PlotWeaveTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_folder); }
        [TearDown] public void After() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
        private static IPlotDataSource Source(PlotDocument data) => new JsonPlotDataSource(JsonUtility.ToJson(data));

        [Test]
        public void AssetAndWorkbookCompileProduceEquivalentDocuments()
        {
            var data = PlotSample.Create();
            data.nodes[0].text = "特殊文本\n\"引号\", 中文 & <xml> =SUM(A1)";
            data.nodes[0].position = new Vector2(-123.75f, 0.125f);
            data.enums.Add(new PlotEnumDefinition { key = "Mood", values = { "平静", "开心" } });
            data.commands.Add(new PlotCommandDefinition { key = "custom.mood", parameters = { new PlotParameterDefinition { key = "mood", type = PlotValueType.Enum, enumKey = "Mood", defaultValue = "开心" } } });
            var file = Path.Combine(_folder, "story.xlsx"); PlotWorkbook.Export(data, file);
            var restored = PlotWorkbook.Compile(file);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(data)));
        }
        [Test]
        public void WorkbookNumbersUseInvariantCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                var data = PlotSample.Create(); data.nodes[0].position = new Vector2(1.25f, 6.75f);
                var file = Path.Combine(_folder, "culture.xlsx"); PlotWorkbook.Export(data, file);
                Assert.That(PlotWorkbook.Compile(file).nodes[0].position, Is.EqualTo(data.nodes[0].position));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Test]
        public void BranchesAndVariablesAreTheSameForAssetAndCompiledSource()
        {
            var data = PlotSample.Create(); var path = Path.Combine(_folder, "story.xlsx"); PlotWorkbook.Export(data, path);
            foreach (var source in new[] { Source(data), Source(PlotWorkbook.Compile(path)) })
            using (var session = new PlotSession())
            {
                session.Start(source); session.Tick(0);
                Assert.That(session.Current.id, Is.EqualTo("welcome"));
                Assert.That(session.Line, Does.Contain("旅人"));
                session.Advance(); session.Tick(0); Assert.That(session.Choices.Count, Is.EqualTo(2));
                session.Choose(0); session.Tick(0); Assert.That(session.State, Is.EqualTo(PlotPlaybackState.Running));
                session.Tick(0.4f); Assert.That(session.Line, Does.Contain("探索者"));
                Assert.That(session.State, Is.EqualTo(PlotPlaybackState.WaitingForInput));
                session.Advance(); session.Tick(0); session.Advance();
                Assert.That(session.State, Is.EqualTo(PlotPlaybackState.Completed));
            }
        }
        [Test]
        public void PlaybackUsesSnapshotWithoutChangingSourceVariables()
        {
            var data = PlotSample.Create();
            using (var session = new PlotSession())
            {
                session.Start(Source(data), "events"); session.Tick(0);
                Assert.That(session.Context.Get("player"), Is.EqualTo("探索者"));
                Assert.That(data.variables[0].value, Is.EqualTo("旅人"));
            }
        }
        [Test]
        public void StopDisposesPendingExternalCommand()
        {
            var data = PlotSample.Create(); data.nodes[0].commands.Clear(); data.nodes[0].commands.Add(new PlotCommand { key = "external" });
            var handler = new PendingHandler();
            using (var session = new PlotSession(handler))
            {
                session.Start(Source(data)); session.Tick(0); session.Stop();
                Assert.That(handler.Operation.Disposed, Is.True);
                session.Tick(100); Assert.That(session.State, Is.EqualTo(PlotPlaybackState.Stopped));
            }
        }
        [Test]
        public void InvalidTargetsAndDuplicateIdsAreRejected()
        {
            var data = PlotSample.Create(); data.nodes[0].nextNodeId = "missing"; data.nodes[1].id = data.nodes[0].id;
            var result = PlotValidation.Check(data);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("重复")), Is.True);
            Assert.That(result.Errors.Any(e => e.Contains("missing")), Is.True);
        }
        [Test]
        public void TypedCommandArgumentsAreValidated()
        {
            var data = PlotSample.Create(); data.nodes[2].commands.First(c => c.key == "wait").With("seconds", "NaN");
            Assert.That(PlotValidation.Check(data).IsValid, Is.False);
        }
        [Test]
        public void ConditionalChoicesUseOwnerContext()
        {
            var data = PlotSample.Create(); data.nodes[1].choices[0].conditionVariable = "locked"; data.nodes[1].choices[0].conditionValue = "false";
            using (var session = new PlotSession())
            {
                session.Start(Source(data), "route"); session.Tick(0);
                Assert.That(session.Choices.Count, Is.EqualTo(1));
                session.Choose(0); Assert.That(session.Current.id, Is.EqualTo("end"));
            }
        }
        [Test]
        public void UnsupportedCommandsFailExplicitly()
        {
            var data = PlotSample.Create(); data.nodes[0].commands.Add(new PlotCommand { key = "not.registered" });
            using (var session = new PlotSession())
            {
                session.Start(Source(data)); session.Tick(0);
                Assert.That(session.State, Is.EqualTo(PlotPlaybackState.Faulted));
                Assert.That(session.Error, Does.Contain("not.registered"));
            }
        }
        [Test]
        public void CodeGenerationRejectsNormalizedNameCollisions()
        {
            var data = PlotSample.Create(); data.commands.Add(new PlotCommandDefinition { key = "a.b" }); data.commands.Add(new PlotCommandDefinition { key = "a-b" });
            Assert.Throws<FormatException>(() => PlotCodeGenerator.Build(data));
        }
        [Test]
        public void GeneratedAdaptersUseInvariantReaders()
        {
            var code = PlotCodeGenerator.Build(PlotSample.Create());
            Assert.That(code, Does.Contain("PlotArgumentReader.Float"));
            Assert.That(code, Does.Not.Contain("GameCtrl"));
            Assert.That(code, Does.Not.Contain("cfg."));
        }
        [Test]
        public void JsonSourceCanBeReadAgainWithoutRetainingRuntimeState()
        {
            var source = Source(PlotSample.Create());
            var first = source.Read(); first.nodes.Clear();
            Assert.That(source.Read().nodes.Count, Is.EqualTo(4));
        }
        [Test]
        public void CyclicGraphDoesNotSpinWithoutPlayerInput()
        {
            var data = PlotSample.Create(); data.nodes[0].nextNodeId = "welcome";
            using (var session = new PlotSession())
            {
                session.Start(Source(data)); session.Tick(100);
                Assert.That(session.State, Is.EqualTo(PlotPlaybackState.WaitingForInput));
                session.Advance(); session.Tick(0);
                Assert.That(session.Current.id, Is.EqualTo("welcome"));
            }
        }
        [Test]
        public void OversizedWorkbookDoesNotReplaceExistingFile()
        {
            var file = Path.Combine(_folder, "preserve.xlsx");
            var data = PlotSample.Create(); PlotWorkbook.Export(data, file);
            var original = File.ReadAllBytes(file);
            data.nodes[0].text = new string('x', 33000);
            Assert.Throws<FormatException>(() => PlotWorkbook.Export(data, file));
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(original));
        }
        [Test]
        public void GraphEditorCanConstructAndReloadSample()
        {
            var asset = ScriptableObject.CreateInstance<PlotAsset>(); asset.document = PlotSample.Create();
            PlotWeaveWindow window = null;
            try
            {
                window = UnityEditor.EditorWindow.GetWindow<PlotWeaveWindow>();
                window.CreateGUI(); PlotWeaveWindow.Open(asset); window.CreateGUI();
                Assert.That(window.rootVisualElement.childCount, Is.GreaterThan(0));
            }
            finally { if (window != null) window.Close(); UnityEngine.Object.DestroyImmediate(asset); }
        }
        private sealed class PendingHandler : IPlotCommandHandler
        {
            public readonly PendingOperation Operation = new PendingOperation();
            public bool CanHandle(string key) => key == "external";
            public IPlotOperation Execute(PlotCommand command, PlotContext context) => Operation;
        }
        private sealed class PendingOperation : IPlotOperation
        {
            public bool Disposed;
            public bool IsCompleted => false;
            public void Tick(float dt) { }
            public void Dispose() { Disposed = true; }
        }
    }
}
