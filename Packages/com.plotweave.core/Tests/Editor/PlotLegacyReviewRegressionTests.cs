using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using PlotWeave.Legacy.Editor;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyReviewRegressionTests
    {
        [Test] public void PackedUnityColor32KeepsItsOriginalChannels()
        {
            var layout=new PlotLegacyUiLayout{name="PackedColorFixture",source="fixture",nodes=new List<PlotLegacyUiNode>{new PlotLegacyUiNode{id="root",transformId="transform",parentId="0",name="Label",rect=true,active=true}},components=new List<PlotLegacyUiComponent>{new PlotLegacyUiComponent{id="label",ownerId="root",kind="TMP_Text",sourceScript="",json="{\"m_fontColor32\":{\"serializedVersion\":2,\"rgba\":4283519054},\"m_fontColor\":{\"r\":0.30588236,\"g\":0.3137255,\"b\":0.31764707,\"a\":1}}"}}};
            var root=PlotLegacyUiRebuilder.Build(layout,_=>null,new List<PlotLegacyUiIssue>());
            try{var label=root.GetComponentInChildren<TMP_Text>(true);var serialized=new SerializedObject(label);var field=serialized.FindProperty("m_fontColor32");Assert.That(field,Is.Not.Null);var value=(Color32)field.colorValue;Assert.That(value.r,Is.EqualTo(78));Assert.That(value.g,Is.EqualTo(80));Assert.That(value.b,Is.EqualTo(81));Assert.That(value.a,Is.EqualTo(255));}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [TestCase("null")]
        [TestCase("{\"x\":0,\"y\":0,\"width\":0,\"height\":0}")]
        public void FullImageMetadataDoesNotCreateAZeroSizedSprite(string slice)
        {
            var id=Guid.NewGuid().ToString("N");var sourceRoot="Library/PlotWeaveSpriteReview_"+id;var output="Assets/PlotWeaveSpriteReview_"+id;
            Directory.CreateDirectory(sourceRoot);var texture=new Texture2D(4,3,TextureFormat.RGBA32,false);var pixels=new Color[12];for(var i=0;i<pixels.Length;i++)pixels[i]=new Color((i%4)/3f,(i/4)/2f,.4f,1);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(sourceRoot+"/image.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            var json="{\"formatVersion\":1,\"layouts\":[],\"resources\":[{\"key\":\"fixture:21300000\",\"source\":\"image.png\",\"fileID\":\"21300000\",\"kind\":\".png\",\"rect\":"+slice+",\"pivot\":{\"x\":0.5,\"y\":0.5},\"border\":{\"x\":0,\"y\":0,\"z\":0,\"w\":0},\"pixelsPerUnit\":100}],\"sources\":[]}";
            try
            {
                var bundle=JsonUtility.FromJson<PlotLegacyUiBundle>(json);var resources=new PlotLegacyUiResources(bundle,sourceRoot,output,new PlotLegacyUiReport());var sprite=resources.Resolve("fixture:21300000") as Sprite;
                Assert.That(sprite,Is.Not.Null);Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(4,3)));Assert.That(sprite.textureRect.size,Is.EqualTo(new Vector2(4,3)));Assert.That(sprite.vertices.Length,Is.GreaterThanOrEqualTo(4));Assert.That(sprite.bounds.size.x,Is.GreaterThan(0));Assert.That(sprite.bounds.size.y,Is.GreaterThan(0));
                var assetPath=AssetDatabase.GetAssetPath(sprite);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport);var reloaded=AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);Assert.That(reloaded,Is.Not.Null);Assert.That(reloaded.rect.size,Is.EqualTo(new Vector2(4,3)),"保存重导入后也必须具有可渲染的非零范围");
            }
            finally{if(AssetDatabase.IsValidFolder(output))AssetDatabase.DeleteAsset(output);if(Directory.Exists(sourceRoot))Directory.Delete(sourceRoot,true);}
        }
    }
}
