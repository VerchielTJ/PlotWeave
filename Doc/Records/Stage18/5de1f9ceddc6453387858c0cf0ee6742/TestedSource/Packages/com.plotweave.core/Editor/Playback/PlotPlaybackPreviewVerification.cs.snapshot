using System;
using System.IO;
using System.Linq;
using PlotWeave.Authoring.UI;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    [InitializeOnLoad]
    public static class PlotPlaybackPreviewVerification
    {
        private const string Key = "PlotWeave.Stage05.Preview";
        [Serializable] private sealed class State
        {
            public string id, mode, scene, phase, image, error = "", previousScene, playerState, text, background;
            public bool previousBackground, previousMaximized, hadView, ready;
            public int node, actors;
            public double deadline, captureAfter;
        }
        static PlotPlaybackPreviewVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode) return; var state = Read(); if (state == null) return;
                EditorSceneManager.playModeStartScene = state.previousScene == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene);
                Application.runInBackground = state.previousBackground; var view = GameView(); if (view != null && state.hadView) view.maximized = state.previousMaximized;
                state.phase = File.Exists(state.image) && state.error == "" && state.node == 1001 && state.actors == 2 && state.ready && state.playerState == "Playing" ? "captured" : "failed";
                if (state.phase == "failed" && state.error == "") state.error = "运行检查未达到预期播放状态";
                Record(state); SessionState.EraseString(Key);
            };
        }
        private static State Read() { var json = SessionState.GetString(Key, ""); return json == "" ? null : JsonUtility.FromJson<State>(json); }
        private static void Save(State state) => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Record(State state)
        { Directory.CreateDirectory("Doc/Records/Stage05"); File.WriteAllText("Doc/Records/Stage05/preview-" + state.id + ".json", JsonUtility.ToJson(state, true)); }
        private static EditorWindow GameView() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        public static void Start(string mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("已有运行中的场景或检查，未中断它");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前场景未保存，请先保存再检查");
            var scene = mode == "editor" ? PlotPlaybackExample.EditorScene : mode == "asset" ? PlotPlaybackExample.GameScene : mode == "json" ? PlotPlaybackExample.JsonScene : mode == "luban" ? PlotWeave.Authoring.Luban.PlotLubanExample.ScenePath : throw new ArgumentException("无效的固定检查模式");
            if (!File.Exists(scene)) throw new FileNotFoundException("共享播放样例尚未生成", scene);
            var id = Guid.NewGuid().ToString("N"); var state = new State { id = id, mode = mode, scene = scene, phase = "starting", previousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), previousBackground = Application.runInBackground, deadline = EditorApplication.timeSinceStartup + 90, image = "Doc/Records/Stage05/Images/" + mode + "-" + id + ".png" };
            Directory.CreateDirectory(Path.GetDirectoryName(state.image)); Save(state); Record(state); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene); EditorApplication.isPlaying = true;
        }
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("共享播放画面检查超时");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
                var game = GameView(); if (game != null) { if (!state.hadView) { state.hadView = true; state.previousMaximized = game.maximized; game.maximized = true; Save(state); } game.Repaint(); }
                if (state.phase == "capturing")
                { if (File.Exists(state.image) && new FileInfo(state.image).Length > 0) { Record(state); EditorApplication.isPlaying = false; } return; }
                var host = UnityEngine.Object.FindObjectsOfType<PlotPlayerHost>().FirstOrDefault(h => h.Core != null && h.gameObject.scene.path == state.scene);
                if (host == null)
                {
                    if (state.mode == "editor")
                    {
                        var panel = UnityEngine.Object.FindObjectsOfType<PlotAuthoringPanel>().FirstOrDefault(p => p.gameObject.scene.path == state.scene);
                        if (panel == null || PlotEditorPlaybackBinding.GetPlayer(panel) == null) return;
                        var button = panel.GetComponentsInChildren<Button>(true).First(b => b.name == "PreviewNode"); if (!button.interactable) return; button.onClick.Invoke();
                    }
                    return;
                }
                if (host.Core.State == PlotPlayerState.Faulted) throw new InvalidOperationException("播放核心进入Faulted状态");
                if (host.Core.IsTyping) host.Core.OnPlayerInput();
                if (!host.Core.IsReady) return;
                if (state.phase != "frame") { state.phase = "frame"; state.captureAfter = EditorApplication.timeSinceStartup + .5; Save(state); return; }
                if (EditorApplication.timeSinceStartup < state.captureAfter) return;
                state.node = host.Core.GetCurrentNodeID(); state.actors = host.View.ActorCount; state.text = host.View.VisibleText; state.background = host.View.BackgroundKey; state.playerState = host.Core.State.ToString(); state.ready = host.Core.IsReady;
                if (state.node != 1001 || state.actors != 2 || state.background != "demo.stage" || !state.text.Contains("类型与数据迁移样例")) throw new InvalidOperationException("共享播放样例输出与基线不一致");
                state.phase = "capturing"; Save(state); Record(state); ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image));
            }
            catch (Exception error)
            {
                state.error = error.ToString(); Record(state); Save(state);
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                else { Application.runInBackground = state.previousBackground; EditorSceneManager.playModeStartScene = state.previousScene == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene); SessionState.EraseString(Key); }
                Debug.LogException(error);
            }
        }
    }
}
