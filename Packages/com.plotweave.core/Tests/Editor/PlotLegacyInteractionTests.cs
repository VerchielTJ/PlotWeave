using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using PlotWeave.Legacy;
using PlotWeave.Legacy.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyInteractionTests
    {
        private string _folder;private GameObject _instance;private PlotLegacyAuthoringHost _host;private PlotAuthoringProject _project;
        [SetUp] public void SetUp()
        {
            if(!File.Exists(PlotLegacyUiMigration.LatestPath)){Assert.Ignore("本地原布局未导入");return;}
            var report=JsonUtility.FromJson<PlotLegacyUiReport>(File.ReadAllText(PlotLegacyUiMigration.LatestPath));var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(report.layouts.Single(l=>l.source.EndsWith("/PlotEditorScene.unity",StringComparison.Ordinal)).asset);
            _folder="Assets/PlotWeaveLegacyUiTest_"+Guid.NewGuid().ToString("N");_project=PlotAuthoringBootstrap.CreateWorkspace(_folder);_instance=UnityEngine.Object.Instantiate(prefab);_host=_instance.AddComponent<PlotLegacyAuthoringHost>();_host.project=_project;_host.template=_instance.GetComponent<PlotLegacyTemplateBindings>();_host.Initialize(new PlotEditorManager(_project),()=>UnsavedChoice.Discard,_=>true);
        }
        [TearDown] public void TearDown(){if(_instance!=null)UnityEngine.Object.DestroyImmediate(_instance);if(_folder!=null&&AssetDatabase.IsValidFolder(_folder)){Undo.FlushUndoRecordObjects();foreach(var guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{_folder})){var value=AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));if(value!=null)Undo.ClearUndo(value);}AssetDatabase.DeleteAsset(_folder);}}
        private PlotLegacyBehaviourProxy Script(string name)=>_host.template.Scripts(name).First();
        private static T Find<T>(Component root,string name)where T:Component=>PlotLegacyWidgets.Find<T>(root,name);
        private void NoError()=>Assert.That(_host.LastError,Is.Empty);
        [Test] public void OriginalNodeCreateRenameDeleteButtonsUseTheSharedStore()
        {
            var creator=Script("NodeCreater");creator.Field<TMP_InputField>("NewNodeInputField").text="9001";creator.Field<Button>("CreatNewNodeButton").onClick.Invoke();NoError();Assert.That(_host.Session.CurrentAsset.GetColumnSingleValue("ScenesID"),Is.EqualTo("9001"));var current=Script("CurrentNodeEditor");current.Field<TMP_InputField>("RenameInputField").text="9010";current.Field<Button>("RenameNodeButton").onClick.Invoke();NoError();Assert.That(new PlotEditorManager(_project).FindRow(9001),Is.Null);Assert.That(new PlotEditorManager(_project).FindRow(9010),Is.Not.Null);current.Field<Button>("DeletNodeButton").onClick.Invoke();NoError();Assert.That(new PlotEditorManager(_project).FindRow(9010),Is.Null);
        }
        [Test] public void OriginalDialogLanguageKeyAndAddedSaveButtonActuallyPersist()
        {
            var editor=Script("DiaLogKeyEditor");Find<Button>(editor,"GenerateKey").onClick.Invoke();NoError();var key=_host.Session.CurrentDialogKey;Find<TMP_InputField>(editor,"DialogInputField").text="新的简体对白";Find<TMP_Dropdown>(editor,"LanguageDropdown").value=2;Find<TMP_InputField>(editor,"DialogInputField").text="English dialog";Assert.That(_project.dialogs.HasKey(key),Is.False);Find<Button>(editor,"SaveDialogKey").onClick.Invoke();NoError();Assert.That(_project.dialogs.GetDialogContent(key,0),Is.EqualTo("新的简体对白"));Assert.That(_project.dialogs.GetDialogContent(key,2),Is.EqualTo("English dialog"));var box=Find<Transform>(editor,"DialogBoxBG");var before=box.gameObject.activeSelf;Find<Button>(editor,"DialogBoxSwitch").onClick.Invoke();Assert.That(box.gameObject.activeSelf,Is.Not.EqualTo(before));
        }
        [Test] public void OriginalNestedListControlsKeepDraftsSeparateAndPersistOnlyOnSave()
        {
            var units=_host.template.Scripts("RowUnit").ToList();var unit=units.First(u=>u.Field<Component>("Text").GetComponent<TMP_Text>().text.Contains("*RoleAction"));var panel=unit.Field<PlotLegacyBehaviourProxy>("ParameterEditor");panel.Field<Button>("OpenListButton").onClick.Invoke();NoError();var list=Script("ParameterList");var before=_host.Session.WorkingRow.GetColumnRowCount("*RoleAction");list.Field<Button>("AddButton").onClick.Invoke();NoError();Assert.That(_host.Session.WorkingRow.GetColumnRowCount("*RoleAction"),Is.EqualTo(before+1));Assert.That(_host.Session.CurrentAsset.GetColumnRowCount("*RoleAction"),Is.EqualTo(before));Find<Button>(_host,"SaveAll").onClick.Invoke();NoError();Assert.That(_host.Session.CurrentAsset.GetColumnRowCount("*RoleAction"),Is.EqualTo(before+1));
        }
    }
}
