using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlotWeave.Chapters;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    [InitializeOnLoad] public static class PlotChapterPreviewVerification
    {
        private const string Key = "PlotWeave.Stage10.Preview";
        [Serializable] private sealed class State
        { public string id, phase, previousScene, error = "", image; public bool background, maximized, hadView; public int route, returns; public double deadline; public List<string> trace = new List<string>(); }
        static PlotChapterPreviewVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode) return; var state = Read(); if (state == null) return;
                Application.runInBackground = state.background; EditorSceneManager.playModeStartScene = state.previousScene == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene); var game = GameView(); if (game != null && state.hadView) game.maximized = state.maximized;
                state.phase = state.error == "" && state.route == 2 && state.returns == 2 ? "passed" : "failed"; if (state.phase == "failed" && state.error == "") state.error = "章节回程与结算结果未达预期"; Record(state); SessionState.EraseString(Key);
            };
        }
        private static EditorWindow GameView() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        private static State Read() { var json = SessionState.GetString(Key, ""); return json == "" ? null : JsonUtility.FromJson<State>(json); }
        private static void Save(State state) => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Record(State state) { Directory.CreateDirectory("Doc/Records/Stage10"); File.WriteAllText("Doc/Records/Stage10/preview-" + state.id + ".json", JsonUtility.ToJson(state, true)); }
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("已有运行中的场景或检查");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前场景尚未保存");
            if (!File.Exists(PlotChapterExample.ScenePath)) throw new FileNotFoundException(PlotChapterExample.ScenePath);
            var state = new State { id = Guid.NewGuid().ToString("N"), phase = "start", previousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), background = Application.runInBackground, deadline = EditorApplication.timeSinceStartup + 120 };
            Directory.CreateDirectory("Doc/Records/Stage10/Images"); Save(state); Record(state); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotChapterExample.ScenePath); EditorApplication.isPlaying = true;
        }
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("章节宿主运行验收超时"); if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); var game = GameView(); if (game != null) { if (!state.hadView) { state.hadView = true; state.maximized = game.maximized; game.maximized = true; Save(state); } game.Repaint(); }
                var demo = UnityEngine.Object.FindObjectsOfType<PlotChapterDemo>().FirstOrDefault(d => d.Session != null && d.gameObject.scene.path == PlotChapterExample.ScenePath); if (demo == null) return; var host = demo.GetComponent<PlotPlayerHost>();
                if (demo.Session.State == PlotChapterState.Failed || demo.Session.State == PlotChapterState.ReportFailed) throw new Exception(demo.Session.Error);
                if (state.phase == "start")
                { var name = state.route == 0 ? "MainChapter" : "DailyChapter"; state.trace.Add("enter:" + name); state.phase = "play"; Save(state); demo.GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke(); return; }
                if (state.phase == "capture")
                { if (!File.Exists(state.image) || new FileInfo(state.image).Length == 0) return; state.route++; if (state.route == 2) { Record(state); Save(state); EditorApplication.isPlaying = false; } else { state.phase = "start"; Save(state); } return; }
                if (demo.Session.State == PlotChapterState.Completed)
                {
                    var kind = state.route == 0 ? PlotChapterKind.Main : PlotChapterKind.Daily;
                    if (!demo.Backend.IsCompleted(new PlotChapterRequest(kind, "demo-01")) || demo.ReturnCount != state.route + 1 || host.Core.State != PlotPlayerState.Closed) throw new Exception("进度确认、回程或播放器关闭错误");
                    state.returns = demo.ReturnCount; state.trace.Add("ack-and-return:" + kind); state.phase = "capture"; state.image = "Doc/Records/Stage10/Images/return" + state.route + "-" + state.id + ".png"; Save(state); ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image)); return;
                }
                if (demo.Session.State != PlotChapterState.Playing || host.Core == null) return;
                if (host.Core.State == PlotPlayerState.Faulted) throw new Exception("章节播放失败");
                if (host.Core.State == PlotPlayerState.WaitingForChoice) { host.View.GetComponentsInChildren<Button>().Single(b => b.name == "Choice_" + state.route).onClick.Invoke(); return; }
                if (host.Core.IsTyping) host.Core.OnPlayerInput(); else if (host.Core.IsReady) host.Core.OnPlayerInput();
            }
            catch (Exception error) { state.error = error.ToString(); Save(state); Record(state); if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; else { state.phase = "failed"; Record(state); SessionState.EraseString(Key); } Debug.LogException(error); }
        }
    }
}
