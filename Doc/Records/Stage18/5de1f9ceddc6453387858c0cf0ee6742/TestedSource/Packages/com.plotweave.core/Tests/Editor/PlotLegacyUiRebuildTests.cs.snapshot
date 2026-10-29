using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Legacy;
using PlotWeave.Legacy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyUiRebuildTests
    {
        private static PlotLegacyUiLayout Fixture()
        {
            return new PlotLegacyUiLayout { source="fixture",name="Fixture",nodes=new List<PlotLegacyUiNode>{
                new PlotLegacyUiNode{id="root",transformId="tr",parentId="0",name="Root",rect=true,active=true},
                new PlotLegacyUiNode{id="second",transformId="ts",parentId="tr",name="Second",rect=true,active=true,order=1},
                new PlotLegacyUiNode{id="first",transformId="tf",parentId="tr",name="First",rect=true,active=true,order=0}},
                components=new List<PlotLegacyUiComponent>{new PlotLegacyUiComponent{id="button",ownerId="first",kind="Button",sourceScript="",json="{\"m_Interactable\":1}"},
                new PlotLegacyUiComponent{id="proxy",ownerId="root",kind="Behaviour",sourceScript="Fixture/Controller.cs",json="{\"Button\":{\"fileID\":\"button\"},\"Scalar\":2}"}}};
        }
        [Test] public void HierarchyAndBehaviourReferencesAreRebuiltWithoutOldScriptAssemblies()
        {
            var issues=new List<PlotLegacyUiIssue>();var root=PlotLegacyUiRebuilder.Build(Fixture(),key=>null,issues);
            try{var bindings=root.GetComponent<PlotLegacyTemplateBindings>();var proxy=bindings.Scripts("Controller").Single();Assert.That(proxy.Field<Button>("Button").name,Is.EqualTo("First"));var parent=root.transform.Find("Root");Assert.That(parent.GetChild(0).name,Is.EqualTo("First"));Assert.That(parent.GetChild(1).name,Is.EqualTo("Second"));Assert.That(root.activeSelf,Is.False);Assert.That(issues,Is.Empty);}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void SavedPrefabCanReloadItsNewProxyScriptsAndObjectReferences()
        {
            var folder="Assets/PlotWeaveUiRebuildTest_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();var root=PlotLegacyUiRebuilder.Build(Fixture(),key=>null,new List<PlotLegacyUiIssue>());
            try{var path=folder+"/Fixture.prefab";PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab.GetComponent<PlotLegacyTemplateBindings>().Scripts("Controller").Single().Field<Button>("Button"),Is.Not.Null);foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),Is.Zero);}
            finally{UnityEngine.Object.DestroyImmediate(root);AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void MissingParentIsAnExplicitImportError()
        {var layout=Fixture();layout.nodes[1].parentId="missing";Assert.Throws<FormatException>(()=>PlotLegacyUiRebuilder.Build(layout,key=>null,new List<PlotLegacyUiIssue>()));}
        [TestCase("Behaviour")][TestCase("AspectRatioFitter")]
        public void UnityAspectRatioFitterIsNativeAndKeepsItsSizingContract(string kind)
        {
            var layout=Fixture();layout.components.Add(new PlotLegacyUiComponent{id="aspect",ownerId="second",kind=kind,sourceScript="UnityPackage/AspectRatioFitter.cs",json="{\"m_AspectMode\":3,\"m_AspectRatio\":0.82905984}"});var root=PlotLegacyUiRebuilder.Build(layout,_=>null,new List<PlotLegacyUiIssue>());
            try{var fitter=root.GetComponentInChildren<AspectRatioFitter>(true);Assert.That(fitter,Is.Not.Null);Assert.That((int)fitter.aspectMode,Is.EqualTo(3));Assert.That(fitter.aspectRatio,Is.EqualTo(.82905984f).Within(.000001f));Assert.That(root.GetComponentsInChildren<PlotLegacyBehaviourProxy>(true).Any(p=>p.sourceScript=="UnityPackage/AspectRatioFitter.cs"),Is.False);}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void TmpInputFieldGetsItsActualTextComponentBeforeFontsAreApplied()
        {
            var layout=Fixture();layout.components.Add(new PlotLegacyUiComponent{id="text",ownerId="second",kind="TMP_Text",sourceScript="",json="{\"m_text\":\"文本\"}"});layout.components.Add(new PlotLegacyUiComponent{id="input",ownerId="root",kind="TMP_InputField",sourceScript="",json="{\"m_TextComponent\":{\"fileID\":\"text\"},\"m_TextViewport\":{\"fileID\":\"ts\"}}"});var root=PlotLegacyUiRebuilder.Build(layout,key=>null,new List<PlotLegacyUiIssue>());
            try{var input=root.GetComponentInChildren<TMP_InputField>(true);Assert.That(input.textComponent,Is.Not.Null);Assert.That(input.textComponent.name,Is.EqualTo("Second"));}finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void NativeRectTransformAndCameraAreRestoredWithoutUnsupportedJsonCalls()
        {
            var layout=Fixture();layout.nodes[0].transformJson="{\"m_LocalScale\":{\"x\":2,\"y\":3,\"z\":1},\"m_AnchorMin\":{\"x\":0.1,\"y\":0.2},\"m_AnchorMax\":{\"x\":0.8,\"y\":0.9},\"m_SizeDelta\":{\"x\":720,\"y\":480},\"m_AnchoredPosition\":{\"x\":42,\"y\":-24}}";
            layout.components.Add(new PlotLegacyUiComponent{id="camera",ownerId="root",kind="Camera",json="{\"m_ClearFlags\":2,\"orthographic\":1,\"orthographic size\":8}"});
            var root=PlotLegacyUiRebuilder.Build(layout,key=>null,new List<PlotLegacyUiIssue>());
            try{var rect=(RectTransform)root.transform.Find("Root");Assert.That(rect.sizeDelta,Is.EqualTo(new Vector2(720,480)));Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(42,-24)));Assert.That(rect.localScale,Is.EqualTo(new Vector3(2,3,1)));Assert.That(root.GetComponentInChildren<Camera>(true).clearFlags,Is.EqualTo(CameraClearFlags.Color));}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
