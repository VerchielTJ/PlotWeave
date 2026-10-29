using System;
using System.IO;
using System.Linq;
using PlotWeave.Authoring.UI;
using PlotWeave.Legacy.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Authoring
{
    public enum PlotAuthoringStartupDecision { Cancel, FocusGame, RejectUnrelatedPlay, WaitForTransition, OpenSceneAndPlay }

    /// <summary>进入制作状态的唯一入口；静态打开场景不初始化事务或修改资产。</summary>
    [InitializeOnLoad]
    public static class PlotAuthoringStartup
    {
        private const string PendingKey = "PlotWeave.AuthoringStartup.Pending";
        private const string PreviousStartKey = "PlotWeave.AuthoringStartup.PreviousStartScene";

        static PlotAuthoringStartup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static PlotAuthoringStartupDecision Decide(bool isPlaying, bool transitioning, bool hasAuthoringSession, bool sceneChangeAccepted)
        {
            if (isPlaying) return hasAuthoringSession ? PlotAuthoringStartupDecision.FocusGame : PlotAuthoringStartupDecision.RejectUnrelatedPlay;
            if (transitioning) return PlotAuthoringStartupDecision.WaitForTransition;
            return sceneChangeAccepted ? PlotAuthoringStartupDecision.OpenSceneAndPlay : PlotAuthoringStartupDecision.Cancel;
        }

        private static bool HasAuthoringSession() => Resources.FindObjectsOfTypeAll<PlotLegacyAuthoringHost>()
            .Any(h => h != null && h.gameObject.scene.IsValid() && h.InitializationReady)
            || Resources.FindObjectsOfTypeAll<PlotAuthoringPanel>()
                .Any(h => h != null && h.gameObject.scene.IsValid() && h.Session != null);

        private static bool HasAuthoringScene() => Resources.FindObjectsOfTypeAll<PlotLegacyAuthoringHost>()
            .Any(h => h != null && h.gameObject.scene == SceneManager.GetActiveScene() && h.project != null)
            || Resources.FindObjectsOfTypeAll<PlotAuthoringPanel>()
                .Any(h => h != null && h.gameObject.scene == SceneManager.GetActiveScene() && h.project != null);

        private static bool CanStart()
        {
            var decision = Decide(EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode, HasAuthoringSession(), true);
            if (decision == PlotAuthoringStartupDecision.FocusGame) { FocusGame(); return false; }
            if (decision == PlotAuthoringStartupDecision.RejectUnrelatedPlay)
            {
                var error = Resources.FindObjectsOfTypeAll<PlotLegacyAuthoringHost>()
                    .Where(h => h != null && h.gameObject.scene.IsValid() && h.InitializationFailed)
                    .Select(h => h.InitializationError).FirstOrDefault();
                var message = !string.IsNullOrEmpty(error) ? "剧情制作初始化失败：\n" + error + "\n请查看Console并退出Play Mode后重试。"
                    : HasAuthoringScene() ? "剧情制作尚未完成初始化。请查看Console中的初始化结果；当前不会重复打开场景或丢弃输入。"
                    : "当前正在运行其他场景。请先退出Play Mode，再打开剧情制作。";
                EditorUtility.DisplayDialog("PlotWeave 制作入口", message, "知道了");
                return false;
            }
            if (decision == PlotAuthoringStartupDecision.WaitForTransition)
            {
                Debug.LogWarning("PlotWeave正在等待Unity完成Play Mode切换，请切换完成后再进入制作。");
                return false;
            }
            return true;
        }

        public static void Start()
        {
            if (!CanStart()) return;
            if (Decide(false, false, false, EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) != PlotAuthoringStartupDecision.OpenSceneAndPlay) return;
            var scene = PlotLegacyWorkspaceMenus.TryGetAuthoringScene(out var local) ? local : PlotAuthoringBootstrap.ExampleScene;
            if (scene == PlotAuthoringBootstrap.ExampleScene) PlotAuthoringBootstrap.EnsureExampleScene();
            if (SceneManager.GetActiveScene().path != scene) EditorSceneManager.OpenScene(scene);
            EnterScene(scene);
        }

        public static void EnterCurrentScene()
        {
            if (!CanStart()) return;
            if (!HasAuthoringScene()) throw new InvalidOperationException("当前场景没有剧情制作宿主，请使用Tools/PlotWeave/打开剧情编辑器");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnterScene(SceneManager.GetActiveScene().path);
        }

        public static void OpenSceneOnly(string scene, Action prepareIfMissing = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("PlotWeave 静态场景", "请先退出Play Mode，再只打开制作场景。要查看正在运行的制作界面，请使用主入口。", "知道了");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(scene)) prepareIfMissing?.Invoke();
            if (!File.Exists(scene)) throw new FileNotFoundException("制作场景不存在", scene);
            EditorSceneManager.OpenScene(scene);
            ShowStartupHint();
        }

        private static void EnterScene(string scene)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if (asset == null) throw new InvalidOperationException("制作场景必须先保存为有效场景资产: " + scene);
            SessionState.SetString(PreviousStartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(PendingKey, true);
            // A pinned Play Mode start scene must not redirect this explicit authoring action.
            EditorSceneManager.playModeStartScene = asset;
            foreach (var hint in Resources.FindObjectsOfTypeAll<PlotAuthoringStartupWindow>()) hint.Close();
            try
            {
                EditorApplication.EnterPlaymode();
                EditorApplication.delayCall += CheckStartRequest;
            }
            catch { RestoreStartScene(); throw; }
        }

        private static void CheckStartRequest()
        {
            if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            RestoreStartScene();
            Debug.LogError("Unity未进入剧情制作Play Mode。请先处理Console中的编译或启动错误，再使用制作入口。");
        }

        private static void RestoreStartScene()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            var previous = SessionState.GetString(PreviousStartKey, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(PendingKey, false);
            SessionState.EraseString(PreviousStartKey);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                RestoreStartScene();
                EditorApplication.delayCall += FocusGame;
            }
            else if (state == PlayModeStateChange.EnteredEditMode) RestoreStartScene();
        }

        public static void FocusGame()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) throw new InvalidOperationException("此Unity版本未提供Game视图");
            var view = EditorWindow.GetWindow(type);
            view.Show(); view.Focus();
        }

        private static void ShowStartupHint()
        {
            if (Application.isBatchMode || BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode
                || SessionState.GetBool(PendingKey, false) || !HasAuthoringScene()) return;
            var window = EditorWindow.GetWindow<PlotAuthoringStartupWindow>(false, "PlotWeave 制作启动", false);
            window.minSize = new Vector2(360, 145);
            window.Show();
        }
    }

    public sealed class PlotAuthoringStartupWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.LabelField("当前为静态场景预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit Mode尚未初始化节点输入、工具栏与制作布局。进入Play Mode后，请在Game视图中录入和试跑剧情。", MessageType.Info);
            if (GUILayout.Button("进入剧情制作（Play）")) PlotAuthoringStartup.EnterCurrentScene();
        }
    }
}
