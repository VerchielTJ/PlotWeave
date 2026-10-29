using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Generation;
using PlotWeave.Legacy.Editor;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyAssetImportTests
    {
        [Test] public void OnlyEmptyExtraMembersCanBeNormalizedAndRowIdentityIsPreserved()
        {
            var sample = PlotGenerationExample.CreateDefault(); PlotAtomRowData row = null;
            try
            {
                sample.Row.GetColumnRows("*PlotSetBack")[0].values.Add(""); var payload = new PlotLegacyNodePayload { source = "fixture", json = JsonUtility.ToJson(sample.Row) }; var changes = new List<PlotLegacyNormalization>(); row = PlotLegacyAssetImport.Normalize(payload, sample.Header, changes); Assert.That(row.rowId, Is.EqualTo(sample.Row.rowId)); Assert.That(row.GetColumnRows("*PlotSetBack")[0].values.Count, Is.EqualTo(2)); Assert.That(changes.Single().rule, Is.EqualTo("trim-empty-trailing-members"));
                sample.Row.GetColumnRows("*PlotSetBack")[0].values[2] = "must-preserve"; payload.json = JsonUtility.ToJson(sample.Row); Assert.Throws<FormatException>(() => PlotLegacyAssetImport.Normalize(payload, sample.Header, new List<PlotLegacyNormalization>()));
            }
            finally { if (row != null) UnityEngine.Object.DestroyImmediate(row); foreach (var asset in sample.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void MissingScalarsAndEmptyEnumFieldsUseExplicitSchemaDefaults()
        {
            var sample = PlotGenerationExample.CreateDefault(); PlotAtomRowData row = null;
            try
            { sample.Row.columns.RemoveAll(c => c.columnName == "Dialog"); sample.Row.GetColumnRows("*RoleAction")[0].values[1] = ""; var changes = new List<PlotLegacyNormalization>(); row = PlotLegacyAssetImport.Normalize(new PlotLegacyNodePayload { source = "fixture", json = JsonUtility.ToJson(sample.Row) }, sample.Header, changes); Assert.That(row.GetColumnSingleValue("Dialog"), Is.Empty); Assert.That(row.GetColumnRows("*RoleAction")[0].values[1], Is.EqualTo("Default")); Assert.That(changes.Count, Is.EqualTo(2)); }
            finally { if (row != null) UnityEngine.Object.DestroyImmediate(row); foreach (var asset in sample.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void RowSemanticHashDoesNotDependOnUnityObjectInstanceIds()
        {
            var sample = PlotGenerationExample.CreateDefault(); var copy = UnityEngine.Object.Instantiate(sample.Row); var secondHeader = UnityEngine.Object.Instantiate(sample.Header);
            try { copy.headerDefinition = secondHeader; Assert.That(PlotLegacyAssetImport.RowHash(copy), Is.EqualTo(PlotLegacyAssetImport.RowHash(sample.Row))); }
            finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(secondHeader); foreach (var asset in sample.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void RealOriginalAssetsAllReadBackWithTheirNormalizedSemanticsAndCompile()
        {
            if (!File.Exists(PlotLegacyAssetImport.InputPath) || !File.Exists(PlotLegacyAssetImport.LatestPath)) { Assert.Ignore("本地原工程内容未导入；公开演示不依赖原游戏文本"); return; }
            var bundle = JsonUtility.FromJson<PlotLegacyBundle>(File.ReadAllText(PlotLegacyAssetImport.InputPath)); var report = JsonUtility.FromJson<PlotLegacyImportReport>(File.ReadAllText(PlotLegacyAssetImport.LatestPath)); Assert.That(report.status, Is.EqualTo("passed")); Assert.That(report.nodes, Is.EqualTo(bundle.nodes.Count));
            var project = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(report.projectPath); var bySource = bundle.nodes.ToDictionary(n => n.source);
            foreach (var item in report.imported)
            {
                var row = AssetDatabase.LoadAssetAtPath<PlotAtomRowData>(item.asset); var expected = PlotLegacyAssetImport.Normalize(bySource[item.source], project.header, new List<PlotLegacyNormalization>());
                try { Assert.That(PlotLegacyAssetImport.RowHash(row), Is.EqualTo(PlotLegacyAssetImport.RowHash(expected)), item.source); Assert.That(row.rowId, Is.EqualTo(item.rowId)); }
                finally { UnityEngine.Object.DestroyImmediate(expected); }
            }
            var compiled = PlotPlaybackCompiler.Compile(project); Assert.That(compiled.Nodes.Count, Is.EqualTo(bundle.nodes.Count)); Assert.That(compiled.Actors.Count, Is.EqualTo(report.actors)); Assert.That(compiled.Dialogs.Count, Is.EqualTo(report.dialogs)); Assert.That(compiled.Nodes.Single(n => n.ScenesID == 10001).RoleAction, Is.Empty, "原RoleID=0空槽保留于作者数据，在播放投影中过滤"); Assert.That(compiled.Nodes.Single(n => n.ScenesID == 20043).RoleAction.Count, Is.EqualTo(2), "旧剧院复用重复演员，作者行中的第三项继续保留");
        }
        [Test] public void ChinesePresentationKeysExecuteWithoutChangingTheAuthoredKeys()
        {
            var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero); var go = new GameObject("ChineseLegacyPlayback");
            try
            {
                var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false; host.config.TextSpeed = 0; host.config.Positions = Enumerable.Range(0, 19).Select(i => new Vector2((i - 9) * 100, -10)).ToArray(); host.sprites.Add(new PlotPlaybackSpriteBinding { key = "actor", sprite = sprite }); host.sprites.Add(new PlotPlaybackSpriteBinding { key = "back", sprite = sprite });
                var node = new PlotPlaybackNode { ScenesID = 1, Dialog = "原数据", RoleAction = new List<PlotRoleAction> { new PlotRoleAction { RoleID = 1, RolePos = 12, ActionType = "默认", Emote = "默认" } }, PlotSetBack = new List<PlotSetBackground> { new PlotSetBackground { BackName = "back", SetType = "淡入" } } };
                host.Initialize(new PlotPlaybackDocument { Nodes = new List<PlotPlaybackNode> { node }, Actors = new List<PlotPlaybackActor> { new PlotPlaybackActor { ID = 1, Name = "actor", Thespian = "actor" } } }, PlotPlayerRuntimeMode.Editor); host.Play(1); host.Core.Tick(1); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.Playing)); Assert.That(host.View.ActorCount, Is.EqualTo(1)); Assert.That(((RectTransform)host.View.ActorObjects.Single().transform).anchoredPosition.x, Is.EqualTo(300)); Assert.That(node.RoleAction[0].ActionType, Is.EqualTo("默认")); Assert.That(host.View.ActorObjects.Single().transform.Find("PlotEmote"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
