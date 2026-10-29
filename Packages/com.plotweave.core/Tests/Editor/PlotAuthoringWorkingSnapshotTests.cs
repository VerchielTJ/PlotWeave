using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Authoring.UI;
using PlotWeave.Generation;
using PlotWeave.Legacy;
using UnityEngine;

namespace PlotWeave.Tests
{
    /// <summary>预览输入事务的内存测试：不创建资产、不保存工作区。</summary>
    public sealed class PlotAuthoringWorkingSnapshotTests
    {
        private SessionFixture _fixture;
        [SetUp] public void SetUp() { _fixture = new SessionFixture(); }
        [TearDown] public void TearDown() { _fixture?.Dispose(); }

        [Test] public void UnsavedTextScalarsAndListsEnterPreviewWithoutChangingSource()
        {
            var sourceBefore = _fixture.SourceSnapshot();
            var session = _fixture.Session;
            session.SetValue("PlotRoleID", 0, 0, "102");
            session.SetValue("Next", 0, 0, "0");
            session.SetDialogKey("preview.unsaved");
            for (var language = 0; language < 5; language++)
            {
                session.SetText(false, "preview.unsaved", language, "dialog-" + language);
                session.SetText(true, "preview.constant", language, "constant-" + language);
            }
            session.SetValue("*RoleAction", 0, 1, "Shake");
            session.SetValue("*RoleAction", 0, 2, "Happy");
            session.SetValue("*RoleAction", 0, 3, "3");
            session.RemoveListRow("*RoleAction", 1);
            session.AddListRow("*Branch");
            session.SetValue("*Branch", 2, 0, "preview.choice");
            session.SetValue("*Branch", 2, 1, "1002");

            var preview = PlotPlaybackCompiler.Compile(session);
            var node = preview.Nodes.Single(n => n.ScenesID == 1001);
            Assert.That(node.PlotRoleID, Is.EqualTo(102));
            Assert.That(node.Next, Is.Zero);
            Assert.That(node.Dialog, Is.EqualTo("preview.unsaved"));
            Assert.That(node.RoleAction.Count, Is.EqualTo(1));
            Assert.That(node.RoleAction[0].ActionType, Is.EqualTo("Shake"));
            Assert.That(node.RoleAction[0].Emote, Is.EqualTo("Happy"));
            Assert.That(node.RoleAction[0].RolePos, Is.EqualTo(3));
            Assert.That(node.Branch.Last().Key, Is.EqualTo("preview.choice"));
            Assert.That(node.Branch.Last().Target, Is.EqualTo(1002));
            Assert.That(preview.Dialogs.Single(t => t.Key == "preview.unsaved").Languages,
                Is.EqualTo(Enumerable.Range(0, 5).Select(i => "dialog-" + i)));
            Assert.That(preview.Constants.Single(t => t.Key == "preview.constant").Languages,
                Is.EqualTo(Enumerable.Range(0, 5).Select(i => "constant-" + i)));
            Assert.That(session.HasUnsavedChanges, Is.True);
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
            Assert.That(_fixture.SourceSnapshot(), Is.EqualTo(sourceBefore));
        }

        [Test] public void OtherNodesComeFromStoreAndTheReturnedSnapshotOwnsItsValues()
        {
            _fixture.Other.SetColumnSingleValue("Dialog", "store.latest");
            _fixture.Other.SetColumnSingleValue("PlotRoleID", "102");
            _fixture.Session.SetDialogKey("working.current");
            _fixture.Session.SetText(false, "working.current", 2, "before");
            var sourceBefore = _fixture.SourceSnapshot();

            var preview = PlotPlaybackCompiler.Compile(_fixture.Session);
            Assert.That(preview.Nodes.Count, Is.EqualTo(2));
            Assert.That(preview.Nodes.Single(n => n.ScenesID == 1002).Dialog, Is.EqualTo("store.latest"));
            Assert.That(preview.Nodes.Single(n => n.ScenesID == 1002).PlotRoleID, Is.EqualTo(102));
            var previewBefore = JsonUtility.ToJson(preview);
            _fixture.Session.SetValue("*RoleAction", 0, 1, "Shake");
            _fixture.Session.SetText(false, "working.current", 2, "after");
            Assert.That(JsonUtility.ToJson(preview), Is.EqualTo(previewBefore));
            Assert.That(_fixture.SourceSnapshot(), Is.EqualTo(sourceBefore));
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
        }

        [TestCase("rowId")]
        [TestCase("ScenesID")]
        [TestCase("invalidScenesID")]
        [TestCase("header")]
        public void ChangedWorkingIdentityIsRejected(string identity)
        {
            var sourceBefore = _fixture.SourceSnapshot();
            var row = _fixture.Session.WorkingRow;
            switch (identity)
            {
                case "rowId": row.rowId = Guid.NewGuid().ToString(); break;
                case "ScenesID": row.SetColumnSingleValue("ScenesID", "1002"); break;
                case "invalidScenesID": row.SetColumnSingleValue("ScenesID", "0"); break;
                case "header": row.headerDefinition = null; break;
            }
            Assert.Throws<InvalidOperationException>(() => PlotPlaybackCompiler.Compile(_fixture.Session));
            Assert.That(_fixture.SourceSnapshot(), Is.EqualTo(sourceBefore));
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
        }

        [Test] public void RemovedCurrentNodeCannotBeResurrectedByPreview()
        {
            _fixture.Store.Rows.Remove(_fixture.Data.Row);
            Assert.Throws<InvalidOperationException>(() => PlotPlaybackCompiler.Compile(_fixture.Session));
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReplacedOrDuplicatedCurrentIdentityIsRejected(bool duplicate)
        {
            if (duplicate) _fixture.Store.Rows.Add(_fixture.Data.Row);
            else
            {
                _fixture.Other.SetColumnSingleValue("ScenesID", "1001");
                _fixture.Store.Rows.Remove(_fixture.Data.Row);
            }
            Assert.Throws<InvalidOperationException>(() => PlotPlaybackCompiler.Compile(_fixture.Session));
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
        }

        [Test] public void PreviewKeepsLegacyProjectionActorsFiveLanguagesAndEventClones()
        {
            _fixture.Project.legacyEmptyActorSlots = true;
            var row = _fixture.Session.WorkingRow;
            row.SetColumnRows("*RoleAction", new List<PlotAtomRowData.StringList>());
            row.AddRowToColumn("*RoleAction", new List<string> { "101", "Default", "Neutral", "0" });
            row.AddRowToColumn("*RoleAction", new List<string> { "0", "Default", "Neutral", "0" });
            row.AddRowToColumn("*RoleAction", new List<string> { "101", "Shake", "Happy", "2" });
            var sourceBefore = _fixture.SourceSnapshot();

            var preview = PlotPlaybackCompiler.Compile(_fixture.Session);
            var node = preview.Nodes.Single(n => n.ScenesID == 1001);
            Assert.That(node.RoleAction.Count, Is.EqualTo(1));
            Assert.That(node.RoleAction[0].RoleID, Is.EqualTo(101));
            Assert.That(node.RoleAction[0].ActionType, Is.EqualTo("Shake"));
            Assert.That(node.RoleAction[0].RolePos, Is.EqualTo(2));
            Assert.That(preview.Actors.Single(a => a.ID == 101).Name, Is.EqualTo("引导员"));
            Assert.That(preview.Actors.Single(a => a.ID == 101).Thespian, Is.EqualTo("demo.guide"));
            Assert.That(preview.Dialogs.All(t => t.Languages.Count == 5), Is.True);
            Assert.That(preview.Constants.All(t => t.Languages.Count == 5), Is.True);
            Assert.That(node.Events.Single().parameters, Is.EqualTo(new[] { "preserved" }));
            node.Events[0].parameters[0] = "preview-only";
            Assert.That(_fixture.SourceSnapshot(), Is.EqualTo(sourceBefore));
            Assert.That(_fixture.Store.SaveCount, Is.Zero);
        }

        [Test] public void UnselectedOrDisposedSessionIsRejected()
        {
            using (var empty = new PlotEditingSession(_fixture.Store))
                Assert.Throws<InvalidOperationException>(() => PlotPlaybackCompiler.Compile(empty));
            _fixture.Session.Dispose();
            Assert.Throws<InvalidOperationException>(() => PlotPlaybackCompiler.Compile(_fixture.Session));
            Assert.Throws<ArgumentNullException>(() => PlotPlaybackCompiler.Compile((PlotEditingSession)null));
        }

        private sealed class SessionFixture : IDisposable
        {
            public readonly PlotAuthoringExample Data = PlotGenerationExample.CreateDefault();
            public readonly PlotAuthoringProject Project = ScriptableObject.CreateInstance<PlotAuthoringProject>();
            public readonly PlotAtomRowData Other;
            public readonly MemoryStore Store;
            public readonly PlotEditingSession Session;
            public SessionFixture()
            {
                Project.header = Data.Header; Project.dialogs = Data.Dialog; Project.constants = Data.Constant; Project.actors = Data.Actors;
                Project.eventTracks = ScriptableObject.CreateInstance<PlotNodeEventTrackAsset>();
                Project.eventTracks.tracks.Add(new PlotNodeEventTrack
                {
                    nodeID = 1001,
                    events = new List<PlotEventJsonData> { new PlotEventJsonData("preview.event") { parameters = new List<string> { "preserved" } } }
                });
                Other = UnityEngine.Object.Instantiate(Data.Row);
                Other.rowId = Guid.NewGuid().ToString(); Other.SetColumnSingleValue("ScenesID", "1002"); Other.SetColumnSingleValue("Next", "0");
                Store = new MemoryStore(Project, Data.Row, Other);
                Session = new PlotEditingSession(Store); Session.Select(1001, null);
            }
            public string[] SourceSnapshot() => Data.Assets.Concat(new UnityEngine.Object[] { Other, Project, Project.eventTracks }).Select(JsonUtility.ToJson).ToArray();
            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(Other);
                UnityEngine.Object.DestroyImmediate(Project.eventTracks);
                UnityEngine.Object.DestroyImmediate(Project);
                foreach (var asset in Data.Assets) UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private sealed class MemoryStore : IPlotAuthoringStore
        {
            public PlotAuthoringProject Project { get; }
            public readonly List<PlotAtomRowData> Rows;
            public int SaveCount;
            public MemoryStore(PlotAuthoringProject project, params PlotAtomRowData[] rows) { Project = project; Rows = new List<PlotAtomRowData>(rows); }
            private int ID(PlotAtomRowData row) => int.Parse(row.GetColumnSingleValue(Project.idColumn), CultureInfo.InvariantCulture);
            public List<PlotAtomRowData> ListRows(int? from = null, int? to = null) => Rows.Where(r => (!from.HasValue || ID(r) >= from) && (!to.HasValue || ID(r) <= to)).ToList();
            public PlotAtomRowData FindRow(int id) => Rows.Find(r => ID(r) == id);
            public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
            { SaveCount++; throw new InvalidOperationException("预览不应保存数据"); }
            public PlotAtomRowData CreateRow(int id) => throw new NotSupportedException();
            public PlotAtomRowData RenameRow(int oldId, int newId) => throw new NotSupportedException();
            public bool DeleteRow(int id) => throw new NotSupportedException();
        }
    }
}
