using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotAuthoringPanelTests
    {
        private string _folder;
        private PlotAuthoringProject _project;
        private GameObject _host;
        private PlotAuthoringPanel _panel;
        private readonly List<PlotAtomRowData> _temporary = new List<PlotAtomRowData>();
        [SetUp] public void SetUp()
        {
            _folder = "Assets/PlotWeavePanelTest_" + Guid.NewGuid().ToString("N");
            _project = PlotAuthoringBootstrap.CreateWorkspace(_folder);
            _host = new GameObject("AuthoringPanelTest"); _panel = _host.AddComponent<PlotAuthoringPanel>();
            _panel.Initialize(new PlotEditorManager(_project), () => UnsavedChoice.Cancel, ignored => false);
            Canvas.ForceUpdateCanvases();
        }
        [TearDown] public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            foreach (var row in _temporary) if (row != null) UnityEngine.Object.DestroyImmediate(row);
            _temporary.Clear();
            Undo.FlushUndoRecordObjects();
            if (_folder != null && AssetDatabase.IsValidFolder(_folder)) foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { _folder }))
            { var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)); if (asset != null) Undo.ClearUndo(asset); }
            if (_folder != null && _folder.StartsWith("Assets/PlotWeavePanelTest_", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(_folder)) AssetDatabase.DeleteAsset(_folder);
        }
        private Button Button(string name) => _panel.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        private InputField Input(string name) => _panel.GetComponentsInChildren<InputField>(true).First(f => f.name == name);
        [Test] public void MainPanelBuildsTheOriginalWorkflowAreasFromTheWorkspace()
        {
            Assert.That(_panel.Session.WorkingRow.GetColumnSingleValue("ScenesID"), Is.EqualTo("1001"));
            Assert.That(_panel.transform.Find("PlotWeaveAuthoringCanvas/AuthoringRoot/AuthoringBody/NodePanel"), Is.Not.Null);
            Assert.That(Input("Value_ScenesID_0").readOnly, Is.True);
            Assert.That(Button("OpenList_*RoleAction"), Is.Not.Null);
            Assert.That(Input("LocalizedContent").text, Does.Contain("类型与数据迁移样例"));
            Assert.That(Button("PreviewNode").interactable, Is.False, "播放器尚未绑定时不能放一个假预览按钮");
        }
        [Test] public void UiFieldChangesStayInDraftUntilSaveIsInvoked()
        {
            var input = Input("Value_PlotRoleID_0"); input.text = "102"; input.onEndEdit.Invoke("102");
            Assert.That(_panel.Session.CurrentAsset.GetColumnSingleValue("PlotRoleID"), Is.EqualTo("101"));
            Assert.That(_panel.Session.HasUnsavedChanges, Is.True);
            Button("SaveWorkspace").onClick.Invoke();
            Assert.That(_panel.Session.CurrentAsset.GetColumnSingleValue("PlotRoleID"), Is.EqualTo("102"));
            Assert.That(_panel.Session.HasUnsavedChanges, Is.False);
        }
        [Test] public void UnsavedTextStopsNodeSwitchWhenUserChoosesCancel()
        {
            Input("LocalizedContent").text = "未提交的对话";
            var node = _panel.NodeView.GetComponentsInChildren<PlotNodeButton>().First(n => n.NodeId == 1002);
            node.GetComponent<Button>().onClick.Invoke();
            Assert.That(_panel.Session.WorkingRow.GetColumnSingleValue("ScenesID"), Is.EqualTo("1001"));
            Assert.That(_panel.Session.HasUnsavedChanges, Is.True);
            Assert.That(_project.dialogs.GetDialogContent("demo.start"), Is.Not.EqualTo("未提交的对话"));
        }
        [Test] public void BeanListPanelUsesTypedEnumKeysAndStagesNewRows()
        {
            Button("OpenList_*RoleAction").onClick.Invoke(); Canvas.ForceUpdateCanvases(); _panel.ListEditor.RefreshVisible();
            Assert.That(_panel.ListEditor.IsOpen, Is.True);
            var dropdown = _panel.GetComponentsInChildren<Dropdown>(true).First(d => d.name == "Enum_*RoleAction_1");
            dropdown.value = 1; dropdown.onValueChanged.Invoke(1);
            Assert.That(_panel.Session.WorkingRow.GetColumnRows("*RoleAction")[0].values[1], Is.EqualTo("Shake"));
            var count = _panel.Session.WorkingRow.GetColumnRowCount("*RoleAction"); Button("AddListItem").onClick.Invoke();
            Assert.That(_panel.Session.WorkingRow.GetColumnRowCount("*RoleAction"), Is.EqualTo(count + 1));
            Assert.That(_panel.Session.CurrentAsset.GetColumnRowCount("*RoleAction"), Is.EqualTo(2));
        }
        [Test] public void NodeListReusesOnlyVisibleButtonsForLargeDataSets()
        {
            for (var i = 0; i < 500; i++)
            {
                var row = ScriptableObject.CreateInstance<PlotAtomRowData>(); row.headerDefinition = _project.header;
                row.SetColumnSingleValue("ScenesID", (10000 + i).ToString()); _temporary.Add(row);
            }
            _panel.NodeView.SetRows(_temporary, 0); Canvas.ForceUpdateCanvases(); _panel.NodeView.RefreshVisible();
            Assert.That(_panel.NodeView.PoolSize, Is.LessThan(100));
            var scroll = _panel.NodeView.GetComponent<ScrollRect>(); scroll.content.anchoredPosition = new Vector2(0, 499 * 40);
            _panel.NodeView.RefreshVisible();
            Assert.That(_panel.NodeView.GetComponentsInChildren<PlotNodeButton>().Any(n => n.NodeId == 10499), Is.True);
        }
        [Test] public void ParameterListReusesVisibleEditorsForLargeBeanLists()
        {
            for (var i = 0; i < 300; i++) _panel.Session.WorkingRow.AddRowToColumn("*RoleAction", new List<string> { "101", "Default", "Neutral", "0" });
            _panel.ListEditor.Open(_project.header.GetColumnByName("*RoleAction")); Canvas.ForceUpdateCanvases(); _panel.ListEditor.RefreshVisible();
            Assert.That(_panel.ListEditor.PoolSize, Is.LessThan(30));
        }
        [Test] public void CancelledDeleteDoesNotModifyTheNodeOrKeyAssets()
        {
            Button("DeleteNode").onClick.Invoke(); Assert.That(new PlotEditorManager(_project).FindRow(1001), Is.Not.Null);
            Button("DeleteTextKey").onClick.Invoke(); Assert.That(_project.dialogs.HasKey("demo.start"), Is.True);
        }
        [Test] public void SavedAuthoringSceneHasAValidRuntimeScriptAndWorkspaceReference()
        {
            var scene = EditorSceneManager.OpenScene(PlotAuthoringBootstrap.ExampleScene, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlotAuthoringPanel>(true)).Single();
                Assert.That(MonoScript.FromMonoBehaviour(host), Is.Not.Null);
                Assert.That(host.project.header, Is.Not.Null);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
