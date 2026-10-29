using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using PlotWeave.Generation;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotEditingSessionTests
    {
        private string _root;
        private PlotAuthoringExample _data;
        private PlotAuthoringProject _project;
        private PlotEditorManager _store;
        private PlotEditingSession _session;
        [SetUp] public void SetUp()
        {
            _root = "Assets/PlotWeaveSessionTest_" + Guid.NewGuid().ToString("N");
            _data = PlotGenerationExample.CreateDefault(); _data.Save(_root + "/Data");
            _project = ScriptableObject.CreateInstance<PlotAuthoringProject>();
            _project.header = _data.Header; _project.dialogs = _data.Dialog; _project.constants = _data.Constant; _project.actors = _data.Actors; _project.nodeFolder = _root + "/Nodes";
            AssetDatabase.CreateAsset(_project, _root + "/Project.asset"); AssetDatabase.SaveAssets();
            _store = new PlotEditorManager(_project); _store.CreateRow(1001); _store.CreateRow(1002);
            _session = new PlotEditingSession(_store); _session.Select(1001, null);
        }
        [TearDown] public void TearDown()
        {
            _session?.Dispose();
            Undo.FlushUndoRecordObjects();
            if (_root != null && AssetDatabase.IsValidFolder(_root)) foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { _root }))
            { var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)); if (asset != null) Undo.ClearUndo(asset); }
            if (_root != null && _root.StartsWith("Assets/PlotWeaveSessionTest_", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(_root)) AssetDatabase.DeleteAsset(_root);
        }
        [Test] public void EditingDoesNotChangePersistentDataUntilSave()
        {
            _session.SetValue("Dialog", 0, 0, "new.key"); _session.SetText(false, "new.key", 2, "English");
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.Empty);
            Assert.That(_project.dialogs.HasKey("new.key"), Is.False); Assert.That(_session.HasUnsavedChanges, Is.True);
            var guid = _session.CurrentAsset.rowId;
            _session.Save();
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.EqualTo("new.key"));
            Assert.That(_project.dialogs.GetDialogContent("new.key", 2), Is.EqualTo("English"));
            Assert.That(_session.CurrentAsset.rowId, Is.EqualTo(guid)); Assert.That(_session.CurrentAsset.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(_session.HasUnsavedChanges, Is.False);
        }
        [Test] public void CancelSwitchKeepsWorkingNodeAndPendingText()
        {
            _session.SetValue("Dialog", 0, 0, "pending");
            Assert.That(_session.Select(1002, () => UnsavedChoice.Cancel), Is.False);
            Assert.That(_session.WorkingRow.GetColumnSingleValue("ScenesID"), Is.EqualTo("1001"));
            Assert.That(_session.WorkingRow.GetColumnSingleValue("Dialog"), Is.EqualTo("pending"));
        }
        [Test] public void DiscardSwitchDropsChangesAndSaveSwitchPersistsThem()
        {
            _session.SetValue("Dialog", 0, 0, "discarded");
            _session.Select(1002, () => UnsavedChoice.Discard);
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.Empty);
            _session.SetValue("Dialog", 0, 0, "saved");
            _session.Select(1001, () => UnsavedChoice.Save);
            Assert.That(_store.FindRow(1002).GetColumnSingleValue("Dialog"), Is.EqualTo("saved"));
        }
        [Test] public void InvalidRowPreventsRowAndTextTablesFromBeingWritten()
        {
            _session.SetValue("PlotRoleID", 0, 0, "bad-int"); _session.SetText(false, "new.key", 0, "未保存");
            Assert.Throws<FormatException>(() => _session.Save());
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("PlotRoleID"), Is.EqualTo("0"));
            Assert.That(_project.dialogs.HasKey("new.key"), Is.False);
            Assert.That(_session.HasUnsavedChanges, Is.True);
        }
        [Test] public void BeanListEditingUsesDefinitionDefaultsAndMemberPositions()
        {
            _session.AddListRow("*RoleAction");
            Assert.That(_session.WorkingRow.GetColumnRows("*RoleAction")[0].values, Is.EqualTo(new[] { "0", "Default", "Neutral", "0" }));
            _session.SetValue("*RoleAction", 0, 0, "101"); _session.SetValue("*RoleAction", 0, 1, "Shake");
            _session.Save(); Assert.That(_store.FindRow(1001).GetColumnRows("*RoleAction")[0].values[1], Is.EqualTo("Shake"));
            _session.RemoveListRow("*RoleAction", 0); _session.Save();
            Assert.That(_store.FindRow(1001).GetColumnRowCount("*RoleAction"), Is.EqualTo(0));
        }
        [Test] public void GeneratedKeysAndLanguageEditsRemainStagedTogether()
        {
            var key = _session.GenerateDialogKey(); Assert.That(key, Is.EqualTo("PlotDialog_1001"));
            _session.SetText(false, key, 0, "中文"); _session.SetText(false, key, 2, "English");
            Assert.That(_project.dialogs.HasKey(key), Is.False);
            _session.Save();
            Assert.That(_project.dialogs.GetDialogContent(key, 0), Is.EqualTo("中文"));
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.EqualTo(key));
            _session.RemoveText(false, key); _session.Discard();
            Assert.That(_session.Dialogs.HasKey(key), Is.True);
        }
        [Test] public void InvalidLanguageWriteDoesNotCreateAnUntrackedKey()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _session.SetText(false, "new.key", 9, "bad"));
            Assert.That(_session.Dialogs.HasKey("new.key"), Is.False); Assert.That(_session.HasUnsavedChanges, Is.False);
        }
        [Test] public void ExternalEditsAreDetectedBeforeOverwritingTheAsset()
        {
            _session.SetValue("Dialog", 0, 0, "local");
            _store.FindRow(1001).SetColumnSingleValue("Dialog", "external");
            Assert.Throws<InvalidOperationException>(() => _session.Save());
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.EqualTo("external"));
            Assert.That(_session.WorkingRow.GetColumnSingleValue("Dialog"), Is.EqualTo("local"));
        }
        [Test] public void NodeRangesAndDuplicatesAreHandledWithinTheWorkspace()
        {
            Assert.That(_store.ListRows(1002, 1002).Count, Is.EqualTo(1));
            Assert.That(_store.ListRows(1002, 1002)[0].GetColumnSingleValue("ScenesID"), Is.EqualTo("1002"));
            Assert.Throws<InvalidOperationException>(() => _store.CreateRow(1001));
            Assert.Throws<ArgumentOutOfRangeException>(() => _store.CreateRow(0));
            Assert.Throws<ArgumentException>(() => _store.ListRows(1002, 1001));
        }
        [Test] public void RenameAndDeleteKeepOtherNodeFilesIntact()
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_session.CurrentAsset));
            _session.Rename(2001, null);
            Assert.That(_store.FindRow(1001), Is.Null); Assert.That(_store.FindRow(2001), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_session.CurrentAsset)), Is.EqualTo(guid));
            Assert.That(_session.Delete(null), Is.True);
            Assert.That(_store.FindRow(2001), Is.Null); Assert.That(_store.FindRow(1002), Is.Not.Null);
        }
        [Test] public void StoreRefusesSavingAnAssetOutsideItsNodeDirectory()
        {
            Assert.Throws<InvalidOperationException>(() => _store.SaveChanges(_data.Row, _session.WorkingRow, _session.Dialogs, _session.Constants));
        }
        [Test] public void WorkspacesWithoutOptionalTextTablesCanStillEditNodes()
        {
            _session.Dispose(); _project.dialogs = null; _project.constants = null;
            _session = new PlotEditingSession(_store); _session.Select(1001, null);
            _session.SetValue("PlotRoleID", 0, 0, "101"); _session.Save();
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("PlotRoleID"), Is.EqualTo("101"));
        }
        [Test] public void DraftCanRestoreUnsavedRowAndLanguagesWithoutAssetReferences()
        {
            _session.SetValue("Dialog", 0, 0, "draft.key"); _session.SetText(false, "draft.key", 2, "recovered");
            var draft = _session.ExportDraft(); _session.Dispose();
            _session = new PlotEditingSession(_store); _session.RestoreDraft(draft);
            Assert.That(_session.WorkingRow.GetColumnSingleValue("Dialog"), Is.EqualTo("draft.key"));
            Assert.That(_session.Dialogs.GetDialogContent("draft.key", 2), Is.EqualTo("recovered"));
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("Dialog"), Is.Empty);
            Assert.That(_session.HasUnsavedChanges, Is.True); _session.Save();
            Assert.That(_project.dialogs.GetDialogContent("draft.key", 2), Is.EqualTo("recovered"));
        }
        [Test] public void RestoreDoesNotOverwriteAnotherUnsavedDraftEvenOnTheSameNode()
        {
            var saved = _session.ExportDraft(); _session.SetValue("Dialog", 0, 0, "keep.local");
            Assert.Throws<InvalidOperationException>(() => _session.RestoreDraft(saved));
            Assert.That(_session.WorkingRow.GetColumnSingleValue("Dialog"), Is.EqualTo("keep.local"));
        }
        [Test] public void UnknownDraftVersionsAreRejectedBeforeChangingTheSelection()
        {
            Assert.Throws<FormatException>(() => _session.RestoreDraft("{\"version\":9,\"nodeId\":1002,\"columns\":[]}"));
            Assert.That(_session.WorkingRow.GetColumnSingleValue("ScenesID"), Is.EqualTo("1001"));
        }
        [Test] public void SavedChangesRemainConsistentAfterSynchronousAssetReimport()
        {
            _session.SetValue("PlotRoleID", 0, 0, "102"); _session.Save();
            var path = AssetDatabase.GetAssetPath(_session.CurrentAsset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Assert.That(_store.FindRow(1001).GetColumnSingleValue("PlotRoleID"), Is.EqualTo("102"));
        }
        [Test] public void CreatingMoreNodesDoesNotMakeTheFirstCreatedAssetStale()
        {
            var first = _store.CreateRow(2001); _store.CreateRow(2002); _store.CreateRow(2003);
            Assert.That(first, Is.SameAs(_store.FindRow(2001)));
            var working = UnityEngine.Object.Instantiate(first);
            try { working.SetColumnSingleValue("PlotRoleID", "123"); _store.SaveChanges(first, working, _session.Dialogs, _session.Constants); }
            finally { UnityEngine.Object.DestroyImmediate(working); }
            Assert.That(_store.FindRow(2001).GetColumnSingleValue("PlotRoleID"), Is.EqualTo("123"));
        }
    }
}
