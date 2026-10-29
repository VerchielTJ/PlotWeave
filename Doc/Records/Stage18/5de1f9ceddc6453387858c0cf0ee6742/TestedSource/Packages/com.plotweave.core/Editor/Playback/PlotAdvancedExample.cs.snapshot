using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotAdvancedExample
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage08_Advanced";
        public const string ScenePath = Root + "/BranchAndEvents.unity";
        private static PlotEventJsonData Event(string key, int callback = -1, string next = "", params string[] args) => new PlotEventJsonData(key, callback) { callbackList = next, parameters = new List<string>(args) };
        public static PlotPlaybackDocument CreateDocument()
        {
            var data = new PlotPlaybackDocument();
            data.Actors.Add(new PlotPlaybackActor { ID = 101, Name = "向导", Thespian = "demo.guide" });
            data.Nodes.Add(new PlotPlaybackNode {
                ScenesID = 2001, PlotRoleID = 101, Dialog = "<b>PlotWeave · 分支与事件</b>\n欢迎来到岔路口。你想去哪里？", RoleAction = new List<PlotRoleAction> { new PlotRoleAction { RoleID = 101, RolePos = 0, ActionType = "Pop", Emote = "Happy" } },
                PlotSetBack = new List<PlotSetBackground> { new PlotSetBackground { BackName = "demo.stage", SetType = "CrossFade" } },
                Branch = new List<PlotBranch> { new PlotBranch { Key = "沿森林小径前进", Target = 2002 }, new PlotBranch { Key = "返回温暖的村庄", Target = 2003 } },
                Events = new List<PlotEventJsonData> { Event("wait", -1, "1", ".2"), Event("signal", 1, "", "junction.ready") }
            });
            for (var i = 0; i < 2; i++)
            {
                var route = i == 0 ? "森林" : "村庄";
                data.Nodes.Add(new PlotPlaybackNode {
                    ScenesID = 2002 + i, PlotRoleID = 101, Dialog = "你选择了" + route + "。\n角色移动、表情、背景、音效、特效与回调链共享同一个播放核心。", RoleAction = new List<PlotRoleAction> { new PlotRoleAction { RoleID = 101, RolePos = i == 0 ? 2 : 0, ActionType = "Move", Emote = i == 0 ? "Surprised" : "Happy" } },
                    PlotSetBack = new List<PlotSetBackground> { new PlotSetBackground { BackName = "demo.stage", SetType = "CrossFade" } },
                    Events = new List<PlotEventJsonData> { Event("set", -1, "1", "route", route), Event("sound", 1, "2", "demo.bell"), Event("effect", 2, "3", "demo.flash", ".25"), Event("signal", 3, "", "route.ready", "${route}") }
                });
            }
            data.Validate(); return data;
        }
        [MenuItem("Tools/PlotWeave/迁移样例/创建分支与事件演出样例")]
        public static void PrepareVerification()
        {
            PlotPlaybackExample.PrepareVerification(); Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var path = Root + "/Advanced.asset"; var asset = AssetDatabase.LoadAssetAtPath<PlotPlaybackAsset>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<PlotPlaybackAsset>(); asset.data = CreateDocument(); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset); }
            var wav = Root + "/Bell.wav";
            if (!File.Exists(wav))
            {
                using (var stream = new BinaryWriter(File.Create(wav)))
                { const int rate = 22050, count = 4410; stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + count * 2); stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1); stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16); stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(count * 2); for (var i = 0; i < count; i++) stream.Write((short)(Math.Sin(i * Math.PI * 2 * 660 / rate) * (1 - i / (float)count) * 7000)); }
                AssetDatabase.ImportAsset(wav, ImportAssetOptions.ForceSynchronousImport);
            }
            var prefab = Root + "/Flash.prefab";
            if (!File.Exists(prefab))
            {
                var obj = new GameObject("EventFlash", typeof(RectTransform), typeof(Image));
                try { var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(180, 180); var image = obj.GetComponent<Image>(); image.color = new Color(1, .8f, .25f, .45f); image.raycastTarget = false; PrefabUtility.SaveAsPrefabAsset(obj, prefab); }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
            }
            if (!File.Exists(ScenePath))
            {
                var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera"; SceneManager.MoveGameObjectToScene(camera, scene);
                    var host = new GameObject("Advanced Plot Player").AddComponent<PlotPlayerHost>(); SceneManager.MoveGameObjectToScene(host.gameObject, scene); host.source = asset; host.entryNode = 2001; host.config.TextSpeed = .025f;
                    var catalog = AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(PlotPlaybackExample.CatalogPath); host.sprites = catalog.sprites.Select(s => new PlotPlaybackSpriteBinding { key = s.key, sprite = s.sprite }).ToList();
                    host.audioBindings.Add(new PlotPlaybackAudioBinding { key = "demo.bell", clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wav) }); host.effects.Add(new PlotPlaybackEffectBinding { key = "demo.flash", prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefab) }); EditorSceneManager.SaveScene(scene, ScenePath);
                }
                finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
            Debug.Log("PLOTWEAVE_STAGE08_READY " + ScenePath);
        }
    }
}
