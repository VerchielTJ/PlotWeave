using System;
using System.IO;
using System.Linq;
using PlotWeave.Authoring.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Authoring
{
    /// <summary>只运行自己的制作样例，跨域重载保存检查状态；捕获结果不等于人工视觉验收。</summary>
    [InitializeOnLoad]
    public static class PlotAuthoringPreviewVerification
    {
        private const string StateKey = "PlotWeave.AuthoringPreview.State";
        [Serializable] private sealed class State
        {
            public string id;
            public string phase;
            public string previousStartScene;
            public string image;
            public string error;
            public double deadline;
            public int nodeCount;
            public int nodePool;
            public bool previousRunInBackground;
            public bool hasGameView;
            public bool previousGameViewMaximized;
            public string nodeSummary;
            public string editorNodeSummary;
            public string assetNodeSummary;
            public string endEditorNodeSummary;
        }
        static PlotAuthoringPreviewVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += mode =>
            {
                if (mode != PlayModeStateChange.EnteredEditMode) return;
                var state = Read(); if (state == null) return;
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousStartScene);
                Application.runInBackground = state.previousRunInBackground;
                var view = GameView(); if (state.hasGameView && view != null) view.maximized = state.previousGameViewMaximized;
                state.phase = File.Exists(state.image) ? "captured" : "failed";
                state.endEditorNodeSummary = EditorNodeSummary();
                if (state.phase == "failed" && string.IsNullOrEmpty(state.error)) state.error = "在截图完成前退出了检查运行";
                Record(state); SessionState.EraseString(StateKey);
            };
        }
        private static State Read()
        {
            var json = SessionState.GetString(StateKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<State>(json);
        }
        private static void Save(State state) => SessionState.SetString(StateKey, JsonUtility.ToJson(state));
        private static EditorWindow GameView() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        private static string RowSummary(PlotAtomRowData row) => row == null ? "missing" : "RoleID=" + row.GetColumnSingleValue("PlotRoleID") + "; Dialog=" + row.GetColumnSingleValue("Dialog") + "; Next=" + row.GetColumnSingleValue("Next");
        private static string EditorNodeSummary()
        {
            var project = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(PlotAuthoringBootstrap.ExampleRoot + "/Workspace.asset");
            return project == null ? "missing-project" : RowSummary(new PlotEditorManager(project).FindRow(1001));
        }
        private static void Record(State state)
        {
            Directory.CreateDirectory("Doc/Records/Stage03");
            File.WriteAllText("Doc/Records/Stage03/preview-" + state.id + ".json", JsonUtility.ToJson(state, true));
        }
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("已有运行中的场景，未中断它");
            if (Read() != null) throw new InvalidOperationException("已有制作场景截图检查在执行");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前有未保存场景；请先自行保存，再执行截图检查");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotAuthoringBootstrap.ExampleScene);
            if (scene == null) throw new FileNotFoundException("制作样例场景未生成");
            var id = Guid.NewGuid().ToString("N");
            var state = new State
            {
                id = id, phase = "starting", previousStartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                image = "Doc/Records/Stage03/Images/authoring-" + id + ".png", deadline = EditorApplication.timeSinceStartup + 45,
                previousRunInBackground = Application.runInBackground, editorNodeSummary = EditorNodeSummary()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(state.image)); Save(state); Record(state);
            EditorSceneManager.playModeStartScene = scene; EditorApplication.isPlaying = true;
        }
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("制作场景截图检查超时");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                EditorApplication.QueuePlayerLoopUpdate();
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>()) if (window.GetType().FullName == "UnityEditor.GameView") window.Repaint();
                if (state.phase == "starting")
                {
                    var panel = Resources.FindObjectsOfTypeAll<PlotAuthoringPanel>().FirstOrDefault(p => p.gameObject.scene.IsValid() && p.Session != null);
                    if (panel == null) return;
                    state.nodeCount = panel.Session.Store.ListRows().Count; state.nodePool = panel.NodeView.PoolSize;
                    state.nodeSummary = RowSummary(panel.Session.WorkingRow); state.assetNodeSummary = RowSummary(panel.Session.CurrentAsset);
                    state.phase = "waiting-frame"; state.deadline = EditorApplication.timeSinceStartup + 40; Save(state);
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    var view = GameView();
                    if (view != null) { state.hasGameView = true; state.previousGameViewMaximized = view.maximized; Save(state); view.maximized = true; view.Focus(); }
                    EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                    {
                        var current = Read(); if (current == null || !EditorApplication.isPlaying) return;
                        current.phase = "capturing"; Save(current); ScreenCapture.CaptureScreenshot(Path.GetFullPath(current.image));
                    };
                }
                else if (state.phase == "capturing" && File.Exists(state.image))
                {
                    state.phase = "captured"; Save(state); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                state.phase = "failed"; state.error = ex.ToString(); Save(state); Record(state);
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                else
                {
                    EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousStartScene);
                    Application.runInBackground = state.previousRunInBackground;
                    var view = GameView(); if (state.hasGameView && view != null) view.maximized = state.previousGameViewMaximized;
                    SessionState.EraseString(StateKey);
                }
            }
        }
    }
}
