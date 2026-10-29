using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    [InitializeOnLoad] public static class PlotAdvancedPreviewVerification
    {
        private const string Key = "PlotWeave.Stage08.Preview";
        [Serializable] private sealed class State
        { public string id, phase, previousScene, error = "", image; public bool background, maximized, hadView, pauseChecked; public int route, completed, signals; public double deadline, next; public List<string> trace = new List<string>(); }
        private static PlotPlayerHost _bound;
        static PlotAdvancedPreviewVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode) return; var state = Read(); if (state == null) return;
                Application.runInBackground = state.background; EditorSceneManager.playModeStartScene = state.previousScene == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene); var game = GameView(); if (game != null && state.hadView) game.maximized = state.maximized;
                state.phase = state.error == "" && state.route == 2 && state.completed == 2 && state.signals == 4 && state.pauseChecked ? "passed" : "failed"; if (state.phase == "failed" && state.error == "") state.error = "运行状态或回调数量未达预期"; Record(state); SessionState.EraseString(Key); _bound = null;
            };
        }
        private static EditorWindow GameView() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        private static State Read() { var json = SessionState.GetString(Key, ""); return json == "" ? null : JsonUtility.FromJson<State>(json); }
        private static void Save(State state) => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Record(State state) { Directory.CreateDirectory("Doc/Records/Stage08"); File.WriteAllText("Doc/Records/Stage08/preview-" + state.id + ".json", JsonUtility.ToJson(state, true)); }
        public static void RecoverLastFailedCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("运行中的检查不能恢复现场");
            var state = Directory.GetFiles("Doc/Records/Stage08", "preview-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Select(path => JsonUtility.FromJson<State>(File.ReadAllText(path))).FirstOrDefault(s => s.phase == "failed");
            if (state == null) throw new InvalidOperationException("没有失败检查的现场快照");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene); Application.runInBackground = state.background;
            var game = GameView(); if (game != null && state.hadView) game.maximized = state.maximized;
            File.WriteAllText("Doc/Records/Stage08/recovery-" + state.id + ".json", JsonUtility.ToJson(state, true));
        }
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("已有运行中的场景或检查");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前场景尚未保存");
            if (!File.Exists(PlotAdvancedExample.ScenePath)) throw new FileNotFoundException(PlotAdvancedExample.ScenePath);
            var state = new State { id = Guid.NewGuid().ToString("N"), phase = "start", previousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), background = Application.runInBackground, deadline = EditorApplication.timeSinceStartup + 120 };
            Directory.CreateDirectory("Doc/Records/Stage08/Images"); Save(state); Record(state); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotAdvancedExample.ScenePath); EditorApplication.isPlaying = true;
        }
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("分支事件运行验收超时"); if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); var game = GameView(); if (game != null) { if (!state.hadView) { state.hadView = true; state.maximized = game.maximized; game.maximized = true; Save(state); } game.Repaint(); }
                var host = UnityEngine.Object.FindObjectsOfType<PlotPlayerHost>().FirstOrDefault(h => h.Core != null && h.gameObject.scene.path == PlotAdvancedExample.ScenePath); if (host == null) return;
                if (_bound != host)
                { _bound = host; host.Completed += () => { var s = Read(); if (s == null) return; s.completed++; s.trace.Add("completed:" + host.Core.GetCurrentNodeID()); Save(s); }; host.Core.SignalReceived += (name, payload) => { var s = Read(); if (s == null) return; s.signals++; s.trace.Add(name + ":" + payload); Save(s); }; }
                if (host.Core.State == PlotPlayerState.Faulted) throw new InvalidOperationException("演出进入Faulted");
                // 事件可能刚在上一帧写回；重新读取计数，避免覆盖。
                state = Read();
                if (state.phase == "capture")
                { if (!File.Exists(state.image) || new FileInfo(state.image).Length == 0) return; state.phase = "choose"; Save(state); return; }
                if (state.phase == "pause")
                { if (EditorApplication.timeSinceStartup < state.next) return; if (host.Core.State != PlotPlayerState.WaitingForChoice || host.Core.GetCurrentNodeID() != 2001) throw new Exception("暂停期间选择改变了节点"); host.Core.Resume(); state.pauseChecked = true; state.phase = "capture"; state.image = "Doc/Records/Stage08/Images/choices-" + state.id + ".png"; Save(state); ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image)); return; }
                if (state.phase == "choose")
                { var choice = host.View.GetComponentsInChildren<Button>().Single(b => b.name == "Choice_" + state.route); state.phase = "route"; Save(state); choice.onClick.Invoke(); return; }
                if (state.phase == "finalcapture")
                { if (!File.Exists(state.image) || new FileInfo(state.image).Length == 0) return; host.Core.OnPlayerInput(); state = Read(); if (host.Core.State != PlotPlayerState.Completed) throw new Exception("终点未完成"); state.route++; state.phase = "start"; Save(state); if (state.route == 2) { Record(state); EditorApplication.isPlaying = false; } else host.Play(2001); return; }
                if (host.Core.IsTyping) host.Core.OnPlayerInput(); if (!host.Core.IsReady) return;
                if (host.Core.GetCurrentNodeID() == 2001)
                {
                    host.Core.OnPlayerInput(); state = Read(); if (host.Core.State != PlotPlayerState.WaitingForChoice || host.Core.Choices.Count != 2) throw new Exception("未进入二选一状态"); state.trace.Add("choices:" + state.route);
                    if (!state.pauseChecked) { host.Core.Pause(); host.Core.Choose(0); state.phase = "pause"; state.next = EditorApplication.timeSinceStartup + .4; }
                    else state.phase = "choose"; Save(state); return;
                }
                if (state.phase == "route")
                { if (host.Core.GetCurrentNodeID() != 2002 + state.route || host.Core.GetVariable("route") != (state.route == 0 ? "森林" : "村庄") || host.View.ActorCount != 1) throw new Exception("分支产出错误"); state.trace.Add("route:" + host.Core.GetCurrentNodeID() + ":" + host.Core.GetVariable("route")); state.phase = "finalcapture"; state.image = "Doc/Records/Stage08/Images/route" + state.route + "-" + state.id + ".png"; Save(state); ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image)); }
            }
            catch (Exception error) { state.error = error.ToString(); Save(state); Record(state); if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; else { state.phase = "failed"; Record(state); SessionState.EraseString(Key); } Debug.LogException(error); }
        }
    }
}
