using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Authoring.UI;
using PlotWeave.Playback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>动态尺寸验收只操作内存制作副本。已存在的现场先采样，逐尺寸失败不阻断后续诊断。</summary>
    [InitializeOnLoad] public static class PlotAuthoringLayoutVerification
    {
        public const string Key = "PlotWeave.AuthoringLayout.Preview";
        public const string LatestPath = "Tools/PlotWeaveMigration/authoring-layout.json";
        private const string Records = "Doc/Records/Stage20";
        private const int MinimumWidth = 1024, MinimumHeight = 600;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1092, 905), new Vector2Int(1024, 768), new Vector2Int(900, 600), new Vector2Int(720, 900) };
        [Serializable] private sealed class SceneObservation { public string name, path; public bool dirty, loaded; }
        [Serializable] private sealed class HostObservation
        {
            public string name, scene, project, dialogText;
            public bool active, sessionExists, workingRowExists, toolbarExists;
            public List<string> languages = new List<string>();
        }
        [Serializable] private sealed class Observed
        {
            public bool isPlaying, isPlayingOrWillChange, isCompiling, gameViewExists, gameViewMaximized;
            public string activeSceneName, activeScenePath, gameViewType, gameViewReadError;
            public int screenWidth, screenHeight, selectedSizeIndex = -1;
            public Vector2 targetSize, targetRenderSize;
            public Rect gameViewPosition;
            public List<SceneObservation> scenes = new List<SceneObservation>();
            public List<HostObservation> hosts = new List<HostObservation>();
        }
        [Serializable] private sealed class SourceHash { public string path, before, after, memoryBefore, memoryAfter; }
        [Serializable] private sealed class Check
        {
            public string name, error = "", hit = "", category = "geometry-or-interaction";
            public bool passed;
            public Rect bounds;
            public float fontPixels;
        }
        [Serializable] private sealed class SizeResult
        {
            public int width, height, actualWidth, actualHeight;
            public bool resolutionMatched, completed, supported, rawPassed, viewportTooSmall, unsupportedNoticeVisible;
            public string error = "", outcome = "pending", noticeText = "";
            public int rawFailureCount, densityFailureCount, geometryOrInteractionFailureCount;
            public Rect noticeBounds;
            public Vector2 renderSize, nodeScrollBefore, nodeScrollAfter;
            public List<string> images = new List<string>();
            public List<Check> checks = new List<Check>();
        }
        [Serializable] private sealed class State
        {
            public string id, phase, nextPhase, image, scene, previousScene, workspace, prefab, catalog, memoryFolder, draftPath, temporarySizeLabel, error = "";
            public int sizeIndex, stableFrames, saveCalls, rows, groupType, originalIndex, originalCount, gameInstance, focusWindow, listAddFrame;
            public int[] originalSelectedIndices;
            public bool hadGame, maximized, background, zoomCaptured, gameRestored, sourceUnchanged, sourceMemoryUnchanged, completed;
            public bool recoveryAttempted, recovered;
            public int minimumViewportWidth = MinimumWidth, minimumViewportHeight = MinimumHeight;
            public int expectedSizeCount = Sizes.Length, testedSizeCount, supportedSizeCount, unsupportedSizeCount, unsupportedDensityFailures, unsupportedGeometryOrInteractionFailures;
            public bool supportedSizesPassed, unsupportedNoticeChecksPassed, allRawChecksPassed, allSizesPassed;
            public string supportPolicy = "Desktop authoring workspace requires at least 1024x600. Smaller viewports are diagnostic only and are not supported.";
            public string recoveryError = "", removedTemporaryLabel = "";
            public int removedTemporaryIndex = -1;
            public Observed recoveryObservedAfter;
            public Rect gamePosition;
            public Vector2 originalTarget, zoomScale, zoomTranslation;
            public int restoredIndex, restoredCount;
            public Vector2 restoredTarget;
            public Rect restoredGamePosition;
            public double deadline, sizeDeadline, next;
            public Observed observedBefore;
            public List<SourceHash> sources = new List<SourceHash>();
            public List<SizeResult> results = new List<SizeResult>();
            public List<string> restorationErrors = new List<string>();
        }
        private static GameObject _root, _events;
        private static PlotLegacyAuthoringHost _host;
        private static EditorPlotPlayerUI _player;
        private static MemoryStore _store;
        private static PlotLegacyBehaviourProxy _scalarUnit, _branchUnit, _list;
        private static ScrollRect _nodes;
        private static string _memoryPrefix;
        private static readonly List<UnityEngine.Object> Memory = new List<UnityEngine.Object>();
        static PlotAuthoringLayoutVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                var state = Read(); if (state == null) return;
                if (change == PlayModeStateChange.ExitingPlayMode) RestoreGame(state);
                if (change == PlayModeStateChange.EnteredEditMode) Finish(state);
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                var state = Read(); if (state == null) return;
                // Entering/leaving PlayMode normally reloads the domain. No transient
                // size or UI exists before Build, and normal teardown restores it first.
                if (state.phase == "starting" || state.phase == "stopping") return;
                state.error = "验收期间发生程序集重载，已停止并恢复临时GameView尺寸";
                RestoreGame(state); Save(state); Record(state);
            };
        }
        private static State Read() { var json = SessionState.GetString(Key, ""); return json == "" ? null : JsonUtility.FromJson<State>(json); }
        private static void Save(State state) => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Record(State state)
        {
            Directory.CreateDirectory(Records); Directory.CreateDirectory(Path.GetDirectoryName(LatestPath));
            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(Records + "/authoring-layout-" + state.id + ".json", json); File.WriteAllText(LatestPath, json);
        }
        private static EditorWindow Game() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        private static PropertyInfo Property(Type type, string name)
        {
            for (var at = type; at != null; at = at.BaseType) { var property = at.GetProperty(name, Flags | BindingFlags.DeclaredOnly); if (property != null) return property; }
            throw new MissingMemberException(type.FullName, name);
        }
        private static object Get(object value, string property)
        {
            var p = Property(value as Type ?? value.GetType(), property); return p.GetValue(p.GetGetMethod(true).IsStatic ? null : value);
        }
        private static void Set(object value, string property, object data)
        {
            var p = Property(value.GetType(), property); p.SetValue(p.GetSetMethod(true).IsStatic ? null : value, data);
        }
        private static object Call(object value, string method, params object[] arguments)
        {
            var found = value.GetType().GetMethods(Flags).Single(m => m.Name == method && m.GetParameters().Length == arguments.Length);
            return found.Invoke(value, arguments);
        }
        private static object SizeSingleton()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes", true);
            return Get(type.BaseType, "instance");
        }
        private static object Group(int group)
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            return Call(SizeSingleton(), "GetGroup", Enum.ToObject(type, group));
        }
        private static Observed Observe()
        {
            var result = new Observed
            {
                isPlaying = EditorApplication.isPlaying, isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode,
                isCompiling = EditorApplication.isCompiling, activeSceneName = SceneManager.GetActiveScene().name,
                activeScenePath = SceneManager.GetActiveScene().path, screenWidth = Screen.width, screenHeight = Screen.height
            };
            for (var i = 0; i < SceneManager.sceneCount; i++)
            { var scene = SceneManager.GetSceneAt(i); result.scenes.Add(new SceneObservation { name = scene.name, path = scene.path, dirty = scene.isDirty, loaded = scene.isLoaded }); }
            var game = Game(); result.gameViewExists = game != null;
            if (game != null)
            {
                result.gameViewType = game.GetType().FullName; result.gameViewPosition = game.position; result.gameViewMaximized = game.maximized;
                try { result.selectedSizeIndex = (int)Get(game, "selectedSizeIndex"); result.targetSize = (Vector2)Get(game, "targetSize"); result.targetRenderSize = (Vector2)Get(game, "targetRenderSize"); }
                catch (Exception error) { result.gameViewReadError = error.ToString(); }
            }
            foreach (var host in Resources.FindObjectsOfTypeAll<PlotLegacyAuthoringHost>().Where(h => !EditorUtility.IsPersistent(h) && h.gameObject.scene.IsValid()))
            {
                var input = host.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(c => c.name == "DialogInputField");
                var language = host.GetComponentsInChildren<TMP_Dropdown>(true).FirstOrDefault(c => c.name == "LanguageDropdown");
                result.hosts.Add(new HostObservation
                {
                    name = host.name, scene = host.gameObject.scene.path, active = host.gameObject.activeInHierarchy,
                    project = AssetDatabase.GetAssetPath(host.project), sessionExists = host.Session != null,
                    workingRowExists = host.Session != null && host.Session.WorkingRow != null,
                    toolbarExists = host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "PlotWeaveMigrationToolbar"),
                    dialogText = input == null ? "" : input.text, languages = language == null ? new List<string>() : language.options.Select(o => o.text).ToList()
                });
            }
            return result;
        }
        [MenuItem("Tools/PlotWeave/验收/多尺寸原制作布局（内存副本）")]
        public static void Start()
        {
            // Sampling precedes every precondition and never creates a window or changes PlayMode.
            var state = new State { id = Guid.NewGuid().ToString("N"), phase = "observed", observedBefore = Observe() };
            Record(state);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("已有PlayMode或验收；只记录现场，不改变用户当前运行状态");
                if (state.observedBefore.scenes.Any(s => s.dirty)) throw new InvalidOperationException("存在dirty场景；只记录现场，不保存或切换用户场景");
                if (EditorApplication.isCompiling) throw new InvalidOperationException("正在编译；已记录现场，请编译结束后再验收");
                var interaction = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
                var ui = JsonUtility.FromJson<PlotLegacyUiReport>(File.ReadAllText(PlotLegacyUiMigration.LatestPath));
                state.workspace = interaction.workspace; state.catalog = interaction.catalog;
                state.prefab = ui.layouts.Single(l => l.source.EndsWith("/PlotEditorScene.unity", StringComparison.Ordinal)).asset;
                state.scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotPlaybackExample.GameScene) != null ? PlotPlaybackExample.GameScene : interaction.playbackScene;
                var source = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(state.workspace);
                if (source == null || AssetDatabase.LoadAssetAtPath<GameObject>(state.prefab) == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(state.scene) == null) throw new InvalidOperationException("验收源工作区、原模板或公开容器场景缺失");
                var sourceRows = new PlotEditorManager(source).ListRows();
                if (sourceRows.Count == 0) throw new InvalidOperationException("验收源工作区没有节点");
                state.memoryFolder = "Assets/PlotWeaveLayoutMemory_" + state.id; state.draftPath = Draft(state.memoryFolder);
                // GameViewSize.baseText truncates strings to 40 characters in this Unity version.
                state.temporarySizeLabel = "PWLay_" + state.id;
                if (Directory.Exists(state.memoryFolder) || File.Exists(state.memoryFolder + ".meta") || File.Exists(state.draftPath)) throw new IOException("验收唯一内存路径或草稿已存在");
                state.previousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene); state.background = Application.runInBackground;
                state.focusWindow = EditorWindow.focusedWindow == null ? 0 : EditorWindow.focusedWindow.GetInstanceID();
                var game = Game(); state.hadGame = game != null;
                if (game != null) CaptureGame(state, game);
                var dependencies = AssetDatabase.GetDependencies(new[] { state.workspace, state.prefab, state.catalog, state.scene, interaction.authoringScene }, true)
                    .Concat(sourceRows.Select(AssetDatabase.GetAssetPath)).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToList();
                var paths = dependencies.Concat(dependencies.Select(p => p + ".meta")).Concat(new[] { source.nodeFolder + ".meta" });
                if (Directory.Exists(source.nodeFolder)) paths = paths.Concat(Directory.GetFiles(source.nodeFolder, "*", SearchOption.AllDirectories));
                foreach (var path in paths.Where(File.Exists).Select(p => p.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
                    state.sources.Add(new SourceHash { path = path, before = FileHash(path), memoryBefore = dependencies.Contains(path) ? DataMemoryHash(path) : "" });
                state.phase = "starting"; state.deadline = EditorApplication.timeSinceStartup + 180;
                foreach (var size in Sizes) state.results.Add(new SizeResult { width = size.x, height = size.y, supported = IsSupported(size.x, size.y) });
                Save(state); Record(state);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(state.scene); EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                state.error = error.ToString(); state.phase = "rejected"; Record(state);
                // No editor state has changed before the SessionState record is installed.
                var installed = Read(); if (installed != null && installed.id == state.id) { RestoreGame(state); Finish(state); }
                throw;
            }
        }
        private static void CaptureGame(State state, EditorWindow game)
        {
            state.gameInstance = game.GetInstanceID(); state.maximized = game.maximized; state.gamePosition = game.position;
            state.groupType = Convert.ToInt32(Get(game, "currentSizeGroupType")); state.originalIndex = (int)Get(game, "selectedSizeIndex");
            state.originalTarget = (Vector2)Get(game, "targetSize"); state.originalCount = (int)Call(Group(state.groupType), "GetTotalCount");
            state.originalSelectedIndices = (int[])((int[])game.GetType().GetField("m_SelectedSizes", Flags).GetValue(game)).Clone();
            var zoom = game.GetType().GetField("m_ZoomArea", Flags).GetValue(game);
            if (zoom != null) { state.zoomScale = (Vector2)Get(zoom, "scale"); state.zoomTranslation = (Vector2)Get(zoom, "translation"); state.zoomCaptured = true; }
        }
        private static void SetSize(State state, SizeResult size)
        {
            var game = Game();
            if (game == null) game = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView", true));
            if (!state.hadGame && state.gameInstance == 0) CaptureGame(state, game);
            game.maximized = true;
            var group = Group(state.groupType); var index = FindTemporarySize(group, state.temporarySizeLabel);
            if (index < 0)
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var type = assembly.GetType("UnityEditor.GameViewSize", true); var kind = assembly.GetType("UnityEditor.GameViewSizeType", true);
                var entry = Activator.CreateInstance(type, Flags, null, new object[] { Enum.Parse(kind, "FixedResolution"), size.width, size.height, state.temporarySizeLabel }, CultureInfo.InvariantCulture);
                Call(group, "AddCustomSize", entry); index = FindTemporarySize(group, state.temporarySizeLabel);
            }
            if (index < 0) throw new InvalidOperationException("临时GameView尺寸未加入内存列表");
            var current = Call(group, "GetGameViewSize", index); Set(current, "width", size.width); Set(current, "height", size.height);
            Set(game, "selectedSizeIndex", index);
            // Verified against local 2022.3.62f3c1 IL. SetCustomResolution is deliberately
            // avoided because it calls SaveToHDD; these operations only refresh the view.
            Call(game, "UpdateZoomAreaAndParent"); Set(game, "targetSize", (Vector2)Get(game, "targetRenderSize")); game.Repaint();
            state.stableFrames = 0; state.sizeDeadline = EditorApplication.timeSinceStartup + 8; state.next = EditorApplication.timeSinceStartup + .25; state.phase = "resize-wait"; Save(state);
        }
        private static int FindTemporarySize(object group, string label)
        {
            var storedLabel = StoredLabel(label); var found = -1;
            for (var i = 0; i < (int)Call(group, "GetTotalCount"); i++)
                if (string.Equals((string)Get(Call(group, "GetGameViewSize", i), "baseText"), storedLabel, StringComparison.Ordinal))
                {
                    if (found >= 0) throw new InvalidOperationException("临时GameView标签存在多个精确匹配，拒绝自动删除");
                    found = i;
                }
            return found;
        }
        private static string StoredLabel(string label) => label != null && label.Length > 40 ? label.Substring(0, 40) : label;
        [MenuItem("Tools/PlotWeave/验收/恢复最近失败的布局验收现场")]
        public static void RecoverLastFailed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null)
                throw new InvalidOperationException("当前仍在PlayMode或验收中，拒绝改动用户运行现场");
            var state = JsonUtility.FromJson<State>(File.ReadAllText(LatestPath));
            if (state == null || state.phase != "failed" || !Guid.TryParseExact(state.id, "N", out _)
                || (state.temporarySizeLabel != "PlotWeaveLayout_" + state.id && state.temporarySizeLabel != "PWLay_" + state.id)
                || state.originalSelectedIndices == null || state.originalCount <= 0)
                throw new InvalidOperationException("最近报告不包含可核验的失败验收身份和GameView基线，拒绝自动恢复");
            var expectedLabel = StoredLabel(state.temporarySizeLabel);
            var group = Group(state.groupType); var index = FindTemporarySize(group, state.temporarySizeLabel);
            if (index >= 0)
            {
                var entry = Call(group, "GetGameViewSize", index);
                if (index < (int)Call(group, "GetBuiltinCount") || index < state.originalCount
                    || (string)Get(entry, "baseText") != expectedLabel || Get(entry, "sizeType").ToString() != "FixedResolution")
                    throw new InvalidOperationException("匹配项不是本轮追加的固定尺寸custom对象，拒绝自动删除");
            }
            state.recoveryAttempted = true; var errorCount = state.restorationErrors.Count;
            RestoreGame(state, false);
            RestoreStep(state, "runInBackground", () => Application.runInBackground = state.background);
            RestoreStep(state, "playModeStartScene", () => EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene));
            RestoreStep(state, "owned memory", () => CleanupRecovered(state));
            RestoreStep(state, "focused window", () => { var focus = EditorUtility.InstanceIDToObject(state.focusWindow) as EditorWindow; if (focus != null) focus.Focus(); });
            state.recoveryObservedAfter = Observe();
            state.recovered = state.gameRestored && state.restorationErrors.Count == errorCount;
            state.recoveryError = string.Join("\n", state.restorationErrors.Skip(errorCount));
            Record(state);
            File.WriteAllText(Records + "/authoring-layout-recovery-" + state.id + ".json", JsonUtility.ToJson(state, true));
            File.WriteAllText("Tools/PlotWeaveMigration/authoring-layout-recovery.json", JsonUtility.ToJson(state, true));
            if (!state.recovered) throw new InvalidOperationException("恢复未完整完成，未删除任何其他尺寸项: " + state.recoveryError);
        }
        private static void RestoreStep(State state, string label, Action action)
        {
            try { action(); } catch (Exception error) { state.restorationErrors.Add(label + ": " + error); }
        }
        private static void RestoreGame(State state, bool persistSession = true)
        {
            var errorCount = state.restorationErrors.Count; state.gameRestored = false;
            var game = EditorUtility.InstanceIDToObject(state.gameInstance) as EditorWindow;
            if (game == null || game.GetType().FullName != "UnityEditor.GameView") game = Game();
            var restoreWindow = game;
            RestoreStep(state, "GameView owned custom size", () =>
            {
                if (state.originalSelectedIndices == null) return;
                var group = Group(state.groupType); var index = FindTemporarySize(group, state.temporarySizeLabel);
                if (index >= 0)
                {
                    var entry = Call(group, "GetGameViewSize", index);
                    if (index < (int)Call(group, "GetBuiltinCount") || index < state.originalCount || Get(entry, "sizeType").ToString() != "FixedResolution")
                        throw new InvalidOperationException("匹配标签不是本轮追加的custom尺寸，拒绝删除");
                    state.removedTemporaryLabel = (string)Get(entry, "baseText"); state.removedTemporaryIndex = index;
                    Call(group, "RemoveCustomSize", index);
                }
                state.restoredCount = (int)Call(group, "GetTotalCount");
                if (state.restoredCount != state.originalCount) throw new InvalidOperationException("GameView尺寸列表数量未恢复；不会删除任何未匹配项");
            });
            RestoreStep(state, "GameView selected indices", () =>
            {
                if (restoreWindow == null || state.originalSelectedIndices == null) return;
                restoreWindow.GetType().GetField("m_SelectedSizes", Flags).SetValue(restoreWindow, (int[])state.originalSelectedIndices.Clone());
                Set(restoreWindow, "selectedSizeIndex", state.originalIndex);
                state.restoredIndex = (int)Get(restoreWindow, "selectedSizeIndex");
                if (state.restoredIndex != state.originalIndex) throw new InvalidOperationException("GameView选择索引未恢复");
            });
            RestoreStep(state, "GameView window/maximized", () =>
            {
                if (restoreWindow == null) return;
                if (state.hadGame)
                {
                    restoreWindow.maximized = state.maximized;
                    if (!state.maximized && state.gamePosition.width > 0 && state.gamePosition.height > 0
                        && ((restoreWindow.position.position - state.gamePosition.position).sqrMagnitude > 1 || (restoreWindow.position.size - state.gamePosition.size).sqrMagnitude > 1))
                        restoreWindow.position = state.gamePosition;
                    state.restoredGamePosition = restoreWindow.position;
                    if (restoreWindow.maximized != state.maximized) throw new InvalidOperationException("GameView最大化状态未恢复");
                }
            });
            RestoreStep(state, "GameView target/zoom", () =>
            {
                if (restoreWindow == null) return;
                if (!state.hadGame) { restoreWindow.Close(); return; }
                Call(restoreWindow, "UpdateZoomAreaAndParent"); Set(restoreWindow, "targetSize", state.originalTarget);
                var zoom = restoreWindow.GetType().GetField("m_ZoomArea", Flags).GetValue(restoreWindow);
                if (state.zoomCaptured && zoom != null) Call(zoom, "SetTransform", state.zoomTranslation, state.zoomScale);
                state.restoredTarget = (Vector2)Get(restoreWindow, "targetSize");
                if ((state.restoredTarget - state.originalTarget).sqrMagnitude > 1) throw new InvalidOperationException("GameView目标尺寸未恢复");
                restoreWindow.Repaint();
            });
            state.gameRestored = state.restorationErrors.Count == errorCount;
            if (persistSession) Save(state); Record(state);
        }
        private static T Clone<T>(T source) where T : ScriptableObject
        { if (source == null) return null; var copy = UnityEngine.Object.Instantiate(source); copy.name = _memoryPrefix + source.name; copy.hideFlags = HideFlags.HideAndDontSave; Memory.Add(copy); return copy; }
        private static T Required<T>(Component root, string name) where T : Component => root.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException("缺少控件 " + name);
        private static List<PlotLegacyBehaviourProxy> Scripts(Component root, string name) => root.GetComponentsInChildren<PlotLegacyBehaviourProxy>(true).Where(p => p.sourceScript.EndsWith("/" + name + ".cs", StringComparison.Ordinal)).ToList();
        private static int ID(PlotAtomRowData row, PlotAuthoringProject project) => int.Parse(row.GetColumnSingleValue(project.idColumn), CultureInfo.InvariantCulture);
        private static void Build(State state)
        {
            _memoryPrefix = "PlotWeaveLayoutMemory_" + state.id + "_";
            for (var i = 0; i < SceneManager.sceneCount; i++) foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects()) root.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(state.workspace); var project = Clone(source); project.nodeFolder = state.memoryFolder;
            project.dialogs = Clone(source.dialogs); project.constants = Clone(source.constants); project.actors = Clone(source.actors); project.eventTracks = Clone(source.eventTracks);
            project.visuals = Clone(source.visuals != null ? source.visuals : AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(state.catalog));
            if (project.visuals != null)
            {
                project.visuals.playerConfig = JsonUtility.FromJson<PlotPlayerConfig>(JsonUtility.ToJson(project.visuals.playerConfig ?? new PlotPlayerConfig()));
                project.visuals.playerConfig.TextSpeed = 0; project.visuals.playerConfig.AutoPlay = false;
            }
            var rows = new PlotEditorManager(source).ListRows().Select(Clone).ToList(); _store = new MemoryStore(project, rows); state.rows = rows.Count;
            _root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(state.prefab)); _root.name = "PlotWeave Layout Memory " + state.id;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas)) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000; }
            _root.SetActive(true);
            if (EventSystem.current == null) _events = new GameObject("Layout Memory EventSystem " + state.id, typeof(EventSystem), typeof(StandaloneInputModule));
            _host = _root.AddComponent<PlotLegacyAuthoringHost>(); _host.template = _root.GetComponent<PlotLegacyTemplateBindings>();
            _host.Initialize(_store, () => throw new InvalidOperationException("布局验收禁止保存/切换未保存节点"), _ => false);
            _player = new EditorPlotPlayerUI(project, () => PlotPlaybackCompiler.Compile(_host.Session), _host.PreviewViewport);
            _host.Preview = _player.Play; _host.RefreshPreview = () => _player.Refresh(); _host.ClosePreview = _player.Dispose;
            _host.ValidatePreview = () => PlotPlaybackCompiler.Compile(_host.Session); _host.OpenDataExchange = () => { };
            _host.CompleteInitialization();
            var row = _host.Session.WorkingRow; row.SetColumnSingleValue("PlotRoleID", "0"); row.SetColumnSingleValue("Next", "0");
            foreach (var column in row.headerDefinition.sheets.Where(c => c.IsList)) row.SetColumnRows(column.columnName, new List<PlotAtomRowData.StringList>());
            if (project.eventTracks != null) project.eventTracks.tracks.RemoveAll(t => t.nodeID == ID(row, project));
            _host.Session.SetDialogKey("__layout_memory_" + state.id);
            Required<TMP_InputField>(_host, "PlayerStartInputField").SetTextWithoutNotify(ID(row, project).ToString(CultureInfo.InvariantCulture));
            var units = Scripts(_host, "RowUnit").Where(p => p.gameObject.activeInHierarchy).ToList();
            _scalarUnit = units.Single(p => Caption(p).Contains("[PlotRoleID]"));
            _branchUnit = units.Single(p => Caption(p).Contains("[*Branch]"));
            _list = Scripts(_host, "ParameterList").Single(); _nodes = _host.NodeView.GetComponent<ScrollRect>();
            state.phase = "resize"; Save(state); Record(state);
        }
        private static string Hierarchy(Transform value)
        { var names = new Stack<string>(); for (var at = value; at != null; at = at.parent) names.Push(at.name); return string.Join("/", names); }
        private static Rect Bounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>(); var camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera, c)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private static void InScreen(Rect rect)
        {
            if (rect.width < .5f || rect.height < .5f || rect.xMin < -1 || rect.yMin < -1 || rect.xMax > Screen.width + 1 || rect.yMax > Screen.height + 1)
                throw new InvalidOperationException("控件边界超出屏幕或没有面积: " + rect + "; Screen=" + Screen.width + "x" + Screen.height);
        }
        private static Check Attempt(State state, SizeResult result, string name, Action<Check> action, string category = "geometry-or-interaction")
        {
            var check = new Check { name = name, category = category }; result.checks.Add(check);
            try { Canvas.ForceUpdateCanvases(); action(check); check.passed = true; }
            catch (Exception error) { check.error = error.ToString(); }
            state.saveCalls = _store == null ? state.saveCalls : _store.SaveCalls; Save(state); Record(state); return check;
        }
        private static RaycastResult Hit(Component control, Check check, bool scroll = false)
        {
            if (control == null || !control.gameObject.activeInHierarchy) throw new InvalidOperationException("控件未显示");
            check.bounds = Bounds((RectTransform)control.transform); InScreen(check.bounds);
            var text = control.GetComponentsInChildren<TMP_Text>().FirstOrDefault(); if (text != null) check.fontPixels = text.fontSize * text.rectTransform.lossyScale.y;
            var system = EventSystem.current; if (system == null || !system.isActiveAndEnabled) throw new InvalidOperationException("没有活动EventSystem");
            var hits = new List<RaycastResult>(); system.RaycastAll(new PointerEventData(system) { position = check.bounds.center }, hits);
            var top = hits.FirstOrDefault(h => h.gameObject != null); check.hit = top.gameObject == null ? "<none>" : Hierarchy(top.gameObject.transform);
            var handler = top.gameObject == null ? null : scroll ? ExecuteEvents.GetEventHandler<IScrollHandler>(top.gameObject) : ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);
            if (handler != control.gameObject) throw new InvalidOperationException("控件中心被遮挡，top=" + check.hit + "; handler=" + (handler == null ? "<none>" : Hierarchy(handler.transform)));
            return top;
        }
        private static void Click(Button button, Check check)
        {
            if (button == null || !button.IsInteractable()) throw new InvalidOperationException("按钮不可交互");
            var hit = Hit(button, check); var pointer = new PointerEventData(EventSystem.current) { position = check.bounds.center, button = PointerEventData.InputButton.Left, clickCount = 1, eligibleForClick = true, pointerCurrentRaycast = hit, pointerPressRaycast = hit };
            if (!ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler)) throw new InvalidOperationException("命中按钮未响应点击");
            if (_host.LastError != "") throw new InvalidOperationException(_host.LastError);
        }
        private static string TextFor(SizeResult result, string suffix) => "Layout " + result.width + "x" + result.height + " input " + suffix;
        private static void Input(SizeResult result, string suffix, Check check)
        {
            var input = Required<TMP_InputField>(_host, "DialogInputField"); Hit(input, check);
            input.SetTextWithoutNotify(TextFor(result, suffix)); input.onValueChanged.Invoke(input.text);
            if (_host.LastError != "" || _host.Session.Dialogs.GetDialogContent(_host.Session.CurrentDialogKey, 0) != input.text) throw new InvalidOperationException("输入没有进入工作副本: " + _host.LastError);
        }
        private static void CheckPreview(SizeResult result, string suffix)
        {
            if (_player.Host == null || _player.Host.Core == null || _player.Host.Core.State == PlotPlayerState.Faulted) throw new InvalidOperationException("预览Core没有正常运行");
            var bindings = _player.Host.View.CaptureBindings();
            if (!bindings.dialog.gameObject.activeInHierarchy || bindings.dialog.text != TextFor(result, suffix)) throw new InvalidOperationException("预览文字不匹配: " + bindings.dialog.text);
            if (!_player.Host.Core.GetDataProvider().TryGetText(_host.Session.CurrentDialogKey, false, 0, out var value) || value != TextFor(result, suffix)) throw new InvalidOperationException("Core没有使用当前输入");
            if (!_host.Session.HasUnsavedChanges || _store.SaveCalls != 0) throw new InvalidOperationException("试跑保存了工作区或丢失未保存标记");
        }
        private static void CheckMain(State state, SizeResult result)
        {
            Attempt(state, result, "minimum-viewport-policy", c => CheckViewportPolicy(result, c), "minimum-viewport-policy");
            Attempt(state, result, "input-preview-separation", c =>
            {
                c.bounds = Bounds(Required<RectTransform>(_host, "DialogBoxBG")); InScreen(c.bounds);
                if (c.bounds.Overlaps(Bounds(_host.PreviewViewport))) throw new InvalidOperationException("编辑输入区覆盖预览画面");
                if (_player.Host != null && c.bounds.Overlaps(Bounds((RectTransform)_player.Host.View.CaptureBindings().dialogBox.transform))) throw new InvalidOperationException("编辑输入区覆盖播放对白框");
            });
            foreach (var name in new[] { "PlayButton", "RefreshPlayDataButton", "DataExchange", "DialogBoxSwitch", "GenerateKey" })
            {
                Attempt(state, result, "main-hit-" + name, c => Hit(Required<Button>(_host, name), c));
                Attempt(state, result, "main-density-" + name, c =>
                {
                    c.bounds = Bounds((RectTransform)Required<Button>(_host, name).transform);
                    if (c.bounds.width < 44 || c.bounds.height < 24) throw new InvalidOperationException("按钮有效尺寸小于44x24像素");
                }, "density");
            }
            Attempt(state, result, "dialog-input-hit", c => Hit(Required<TMP_InputField>(_host, "DialogInputField"), c));
            Attempt(state, result, "dialog-input-density", c =>
            {
                var input = Required<TMP_InputField>(_host, "DialogInputField"); c.bounds = Bounds((RectTransform)input.transform);
                var text = input.GetComponentsInChildren<TMP_Text>().FirstOrDefault(); if (text != null) c.fontPixels = text.fontSize * text.rectTransform.lossyScale.y;
                if (c.bounds.height < 48 || c.fontPixels < 10) throw new InvalidOperationException("输入框过矮或有效字号低于10像素");
            }, "density");
            foreach (var unit in Scripts(_host, "RowUnit").Where(p => p.gameObject.activeInHierarchy))
                Attempt(state, result, "field-hit-" + Caption(unit), c => Hit(unit.Field<Button>("OpenButton"), c));
            Attempt(state, result, "node-scroll-input", c =>
            {
                result.nodeScrollBefore = _nodes.content.anchoredPosition; var hit = Hit(_nodes, c, true);
                var pointer = new PointerEventData(EventSystem.current) { position = c.bounds.center, scrollDelta = new Vector2(0, -40), pointerCurrentRaycast = hit };
                for (var i = 0; i < 6; i++) ExecuteEvents.Execute(_nodes.gameObject, pointer, ExecuteEvents.scrollHandler);
            });
        }
        private static bool IsSupported(int width, int height) => width >= MinimumWidth && height >= MinimumHeight;
        private static string GraphicText(Graphic graphic) => (graphic is TMP_Text tmp ? tmp.text : graphic is Text label ? label.text : "") ?? "";
        private static void CheckViewportPolicy(SizeResult result, Check check)
        {
            var layout = _root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c => c != null && c.GetType().Name == "PlotAuthoringViewportLayout");
            if (layout == null) throw new InvalidOperationException("缺少实际PlotAuthoringViewportLayout，不能将小窗口标为已提示");
            var property = layout.GetType().GetProperty("IsViewportTooSmall", Flags);
            var field = layout.GetType().GetField("IsViewportTooSmall", Flags);
            if (property == null && field == null) throw new InvalidOperationException("布局组件没有公开IsViewportTooSmall状态");
            result.viewportTooSmall = (bool)(property != null ? property.GetValue(layout) : field.GetValue(layout));
            result.supported = IsSupported(result.width, result.height);
            var notices = _root.GetComponentsInChildren<Graphic>(true).Where(g =>
            {
                var value = GraphicText(g); return value.Contains("窗口") && value.Contains("Game") && (value.Contains("过窄") || value.Contains("扩大"));
            }).ToList();
            result.unsupportedNoticeVisible = false;
            foreach (var notice in notices)
            {
                if (!notice.enabled || !notice.gameObject.activeInHierarchy || notice.color.a <= .01f
                    || notice.GetComponentsInParent<CanvasGroup>(true).Any(group => group.alpha <= .01f)) continue;
                var bounds = Bounds(notice.rectTransform);
                try { InScreen(bounds); } catch (InvalidOperationException) { continue; }
                result.unsupportedNoticeVisible = true; result.noticeText = GraphicText(notice); result.noticeBounds = bounds; check.bounds = bounds;
                if (notice is TMP_Text tmp) check.fontPixels = tmp.fontSize * tmp.rectTransform.lossyScale.y;
                else if (notice is Text label) check.fontPixels = label.fontSize * label.rectTransform.lossyScale.y;
                break;
            }
            if (result.viewportTooSmall != !result.supported) throw new InvalidOperationException("实际最低工作区状态与1024x600支持边界不一致");
            if (!result.supported && !result.unsupportedNoticeVisible) throw new InvalidOperationException("不支持的小窗口没有实际可见的扩大Game提示");
            if (result.supported && result.unsupportedNoticeVisible) throw new InvalidOperationException("支持尺寸仍显示小窗口提示，动态resize状态未更新");
        }
        private static void Summarize(SizeResult result)
        {
            result.supported = IsSupported(result.width, result.height);
            result.rawFailureCount = result.checks.Count(c => !c.passed);
            result.densityFailureCount = result.checks.Count(c => !c.passed && c.category == "density");
            result.geometryOrInteractionFailureCount = result.checks.Count(c => !c.passed && c.category == "geometry-or-interaction");
            result.rawPassed = result.completed && result.resolutionMatched && result.error == "" && result.checks.Count > 0 && result.rawFailureCount == 0;
            result.outcome = result.supported ? (result.rawPassed ? "passed" : "failed")
                : !result.completed || !result.resolutionMatched || result.error != "" ? "unsupported-incomplete-or-resolution-failed"
                : result.geometryOrInteractionFailureCount > 0 ? "unsupported-with-geometry-or-interaction-failures"
                : result.densityFailureCount > 0 ? "unsupported-low-density" : "unsupported-below-minimum-viewport";
        }
        private static string Caption(PlotLegacyBehaviourProxy unit)
        {
            var component = unit.Field<Component>("Text"); if (component == null) return unit.name;
            var tmp = component.GetComponent<TMP_Text>(); if (tmp != null) return tmp.text;
            var label = component.GetComponent<Text>(); return label == null ? unit.name : label.text;
        }
        private static bool Critical(SizeResult result) => result.width == 1920 || result.width == 1092 || result.width == 1024 || result.width == 720;
        private static void Capture(State state, SizeResult result, string label, string next)
        {
            if (!Critical(result)) { state.phase = next; state.next = EditorApplication.timeSinceStartup + .2; Save(state); return; }
            state.image = Records + "/authoring-layout-" + state.id + "-" + result.width + "x" + result.height + "-" + label + ".png";
            result.images.Add(state.image); state.nextPhase = next; state.phase = "capture-wait"; state.next = EditorApplication.timeSinceStartup + .25;
            Save(state); Record(state); ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image));
        }
        private static PlotLegacyBehaviourProxy ParameterPanel(PlotLegacyBehaviourProxy unit) => unit.Field<PlotLegacyBehaviourProxy>("ParameterEditor");
        private static bool VisibleInList(Component control)
        {
            if (control == null || !control.gameObject.activeInHierarchy || !(control.transform is RectTransform rect)) return false;
            var scroll = _list.Field<ScrollRect>("LoopVerticalScrollRect");
            if (scroll == null || scroll.viewport == null || !scroll.viewport.gameObject.activeInHierarchy) return false;
            var bounds = Bounds(rect); var viewport = Bounds(scroll.viewport);
            if (bounds.width < .5f || bounds.height < .5f || bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > Screen.width + 1 || bounds.yMax > Screen.height + 1) return false;
            return bounds.xMin >= viewport.xMin - 1 && bounds.yMin >= viewport.yMin - 1 && bounds.xMax <= viewport.xMax + 1 && bounds.yMax <= viewport.yMax + 1;
        }
        private static void CleanupPopups()
        {
            // Cleanup is allowed even when a diagnostic click failed; it never counts as an interaction pass.
            if (_scalarUnit != null && ParameterPanel(_scalarUnit) != null) ParameterPanel(_scalarUnit).gameObject.SetActive(false);
            if (_branchUnit != null && ParameterPanel(_branchUnit) != null) ParameterPanel(_branchUnit).gameObject.SetActive(false);
            if (_list != null) _list.gameObject.SetActive(false);
        }
        private static void Advance(State state, SizeResult result)
        {
            CleanupPopups(); result.completed = true; Summarize(result); state.sizeIndex++;
            state.phase = state.sizeIndex < state.results.Count ? "resize" : "stopping";
            if (state.phase == "stopping") state.completed = true;
            Save(state); Record(state);
        }
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("多尺寸验收超过180秒");
                if (!EditorApplication.isPlaying)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && state.phase != "starting") Finish(state);
                    return;
                }
                Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); Game()?.Repaint();
                if (state.error != "") throw new InvalidOperationException(state.error);
                if (state.phase == "starting") { Build(state); return; }
                if (state.phase == "stopping") { state.saveCalls = _store == null ? state.saveCalls : _store.SaveCalls; Save(state); Cleanup(); RestoreGame(state); EditorApplication.isPlaying = false; return; }
                if (_host == null || _store == null || _player == null) throw new InvalidOperationException("验收内存事务丢失，禁止回退到真实资产");
                var result = state.results[state.sizeIndex];
                if (EditorApplication.timeSinceStartup < state.next) return;
                switch (state.phase)
                {
                    case "resize":
                        CleanupPopups(); _nodes.verticalNormalizedPosition = 1;
                        SetSize(state, result); return;
                    case "resize-wait":
                        result.actualWidth = Screen.width; result.actualHeight = Screen.height; result.renderSize = (Vector2)Get(Game(), "targetRenderSize");
                        state.stableFrames = Screen.width == result.width && Screen.height == result.height ? state.stableFrames + 1 : 0;
                        if (state.stableFrames < 3 && EditorApplication.timeSinceStartup < state.sizeDeadline) { Save(state); return; }
                        result.resolutionMatched = state.stableFrames >= 3;
                        if (!result.resolutionMatched) result.error = "GameView尺寸未稳定匹配，仍保留当前尺寸诊断";
                        state.phase = "input-play"; state.next = EditorApplication.timeSinceStartup + .3; Save(state); return;
                    case "input-play":
                        Attempt(state, result, "input-A", c => Input(result, "A", c));
                        Attempt(state, result, "play-click", c => Click(Required<Button>(_host, "PlayButton"), c));
                        state.phase = "refresh"; state.next = EditorApplication.timeSinceStartup + .4; Save(state); return;
                    case "refresh":
                        Attempt(state, result, "preview-A", c => CheckPreview(result, "A"));
                        Attempt(state, result, "input-B", c => Input(result, "B", c));
                        Attempt(state, result, "refresh-click", c => Click(Required<Button>(_host, "RefreshPlayDataButton"), c));
                        state.phase = "main"; state.next = EditorApplication.timeSinceStartup + .4; Save(state); return;
                    case "main":
                        Attempt(state, result, "preview-B", c => CheckPreview(result, "B")); CheckMain(state, result);
                        state.phase = "scroll-result"; state.next = EditorApplication.timeSinceStartup + .3; Save(state); return;
                    case "scroll-result":
                        Attempt(state, result, "node-scroll-moved", c => { result.nodeScrollAfter = _nodes.content.anchoredPosition; if ((result.nodeScrollAfter - result.nodeScrollBefore).sqrMagnitude < 1) throw new InvalidOperationException("右侧节点列表没有响应滚轮"); });
                        Capture(state, result, "overview", "scalar-open"); return;
                    case "capture-wait":
                        if (!File.Exists(state.image) || new FileInfo(state.image).Length == 0) return;
                        state.phase = state.nextPhase; state.next = EditorApplication.timeSinceStartup + .15; Save(state); return;
                    case "scalar-open":
                        Attempt(state, result, "scalar-field-open", c => Click(_scalarUnit.Field<Button>("OpenButton"), c));
                        state.phase = "scalar-check"; state.next = EditorApplication.timeSinceStartup + .25; Save(state); return;
                    case "scalar-check":
                        Attempt(state, result, "scalar-popup-bounds", c => { var panel = ParameterPanel(_scalarUnit); if (!panel.gameObject.activeInHierarchy) throw new InvalidOperationException("标量编辑弹层没有打开"); c.bounds = Bounds((RectTransform)panel.transform); InScreen(c.bounds); });
                        Attempt(state, result, "scalar-popup-input-hit", c => { var input = ParameterPanel(_scalarUnit).GetComponentsInChildren<TMP_InputField>().First(i => i.gameObject.activeInHierarchy); Hit(input, c); });
                        Capture(state, result, "scalar", "branch-open"); return;
                    case "branch-open":
                        CleanupPopups();
                        Attempt(state, result, "branch-field-open", c => Click(_branchUnit.Field<Button>("OpenButton"), c));
                        state.phase = "list-open"; state.next = EditorApplication.timeSinceStartup + .2; Save(state); return;
                    case "list-open":
                        Attempt(state, result, "list-editor-open", c => Click(ParameterPanel(_branchUnit).Field<Button>("OpenListButton"), c));
                        state.phase = "list-check"; state.next = EditorApplication.timeSinceStartup + .25; Save(state); return;
                    case "list-check":
                        Attempt(state, result, "list-popup-bounds", c => { if (!_list.gameObject.activeInHierarchy) throw new InvalidOperationException("列表编辑弹层没有打开"); c.bounds = Bounds((RectTransform)_list.transform); InScreen(c.bounds); });
                        Attempt(state, result, "list-add-real-click", c =>
                        {
                            var before = _host.Session.WorkingRow.GetColumnRowCount("*Branch"); Click(_list.Field<Button>("AddButton"), c);
                            if (_host.Session.WorkingRow.GetColumnRowCount("*Branch") != before + 1) throw new InvalidOperationException("列表添加没有修改工作副本");
                        });
                        // New/enabled Graphics do not acquire native raycast depth until a
                        // later rendered frame. Do not test those controls in the Add update.
                        state.listAddFrame = Time.frameCount; state.phase = "list-after-add"; state.next = EditorApplication.timeSinceStartup + .25; Save(state); return;
                    case "list-after-add":
                        if (Time.frameCount <= state.listAddFrame) return;
                        Attempt(state, result, "list-row-input-hit", c =>
                        {
                            var input = _list.GetComponentsInChildren<TMP_InputField>().FirstOrDefault(VisibleInList);
                            if (input == null) throw new InvalidOperationException("列表viewport中没有完整可见的输入框；active池化行不等于可见行");
                            Hit(input, c);
                        });
                        Attempt(state, result, "list-remove-real-click", c =>
                        {
                            var before = _host.Session.WorkingRow.GetColumnRowCount("*Branch");
                            var button = Scripts(_list, "EvtListParameterEditor").Where(r => r.gameObject.activeInHierarchy)
                                .Select(r => r.Field<Button>("DeletButton")).FirstOrDefault(b => b != null && b.IsInteractable() && VisibleInList(b));
                            if (button == null) throw new InvalidOperationException("列表viewport中没有完整可见的删除按钮；不会点击屏外overscan池化行");
                            Click(button, c); if (_host.Session.WorkingRow.GetColumnRowCount("*Branch") != before - 1) throw new InvalidOperationException("列表删除没有修改工作副本");
                        });
                        Capture(state, result, "list", "size-complete"); return;
                    case "size-complete": Advance(state, result); return;
                }
            }
            catch (Exception error)
            {
                state.error = error.ToString(); state.saveCalls = _store == null ? state.saveCalls : _store.SaveCalls;
                state.phase = "stopping"; Save(state); Record(state); Cleanup(); RestoreGame(state);
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false; else Finish(state);
                Debug.LogException(error);
            }
        }
        private static string FileHash(string path)
        { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static string DataMemoryHash(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(asset is PlotAuthoringAsset) && !(asset is PlotAuthoringProject) && !(asset is PlotPlaybackVisualCatalog)) return "";
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset)))).Replace("-", "").ToLowerInvariant();
        }
        private static string Draft(string folder)
        { using (var sha = SHA256.Create()) return "Library/PlotWeaveRecovery/" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "").ToLowerInvariant() + "-legacy.json"; }
        private static void Cleanup()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root); else _player?.Dispose();
            if (_events != null) UnityEngine.Object.DestroyImmediate(_events);
            foreach (var item in Memory) if (item != null && !EditorUtility.IsPersistent(item)) UnityEngine.Object.DestroyImmediate(item);
            Memory.Clear(); _root = null; _events = null; _host = null; _player = null; _store = null; _scalarUnit = null; _branchUnit = null; _list = null; _nodes = null; _memoryPrefix = null;
        }
        private static void CleanupRecovered(State state)
        {
            // A domain reload can discard managed references; only exact per-run names
            // and nonpersistent objects qualify for this recovery cleanup.
            foreach (var root in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => !EditorUtility.IsPersistent(g)
                && (g.name == "PlotWeave Layout Memory " + state.id || g.name == "Layout Memory EventSystem " + state.id)).ToArray())
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            var prefix = "PlotWeaveLayoutMemory_" + state.id + "_";
            foreach (var value in Resources.FindObjectsOfTypeAll<ScriptableObject>().Where(v => !EditorUtility.IsPersistent(v)
                && v.name.StartsWith(prefix, StringComparison.Ordinal) && (v.hideFlags & HideFlags.DontSave) != 0).ToArray())
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
        private static void Finish(State state)
        {
            try
            {
                Cleanup(); CleanupRecovered(state); RestoreGame(state); Application.runInBackground = state.background;
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene);
                var focus = EditorUtility.InstanceIDToObject(state.focusWindow) as EditorWindow; if (focus != null) focus.Focus();
                var expectedFolder = "Assets/PlotWeaveLayoutMemory_" + state.id;
                if (state.memoryFolder != expectedFolder || state.draftPath != Draft(expectedFolder)) throw new InvalidOperationException("拒绝清理身份不匹配的恢复草稿");
                var expected = Path.GetFullPath(state.draftPath); var recovery = Path.GetFullPath("Library/PlotWeaveRecovery") + Path.DirectorySeparatorChar;
                if (!expected.StartsWith(recovery, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("恢复草稿超出Library范围");
                if (File.Exists(expected)) File.Delete(expected);
                if (Directory.Exists(expectedFolder) || File.Exists(expectedFolder + ".meta")) throw new InvalidOperationException("内存验收意外创建了Assets路径");
                foreach (var source in state.sources)
                { source.after = File.Exists(source.path) ? FileHash(source.path) : "<missing>"; source.memoryAfter = string.IsNullOrEmpty(source.memoryBefore) ? "" : DataMemoryHash(source.path); }
                state.sourceUnchanged = state.sources.Count > 0 && state.sources.All(s => s.before == s.after);
                state.sourceMemoryUnchanged = state.sources.Where(s => !string.IsNullOrEmpty(s.memoryBefore)).All(s => s.memoryBefore == s.memoryAfter);
                if (!state.sourceUnchanged || !state.sourceMemoryUnchanged || state.saveCalls != 0) throw new InvalidOperationException("源数据SHA不一致或发生保存调用");
                foreach (var result in state.results) Summarize(result);
                state.testedSizeCount = state.results.Count(r => r.completed); state.supportedSizeCount = state.results.Count(r => r.supported); state.unsupportedSizeCount = state.results.Count(r => !r.supported);
                state.supportedSizesPassed = state.supportedSizeCount > 0 && state.results.Where(r => r.supported).All(r => r.rawPassed);
                state.unsupportedNoticeChecksPassed = state.results.Where(r => !r.supported).All(r => r.completed && r.resolutionMatched && r.error == ""
                    && r.viewportTooSmall && r.unsupportedNoticeVisible && r.checks.Any(c => c.name == "minimum-viewport-policy" && c.passed));
                state.unsupportedDensityFailures = state.results.Where(r => !r.supported).Sum(r => r.densityFailureCount);
                state.unsupportedGeometryOrInteractionFailures = state.results.Where(r => !r.supported).Sum(r => r.geometryOrInteractionFailureCount);
                state.allRawChecksPassed = state.results.Count == Sizes.Length && state.results.All(r => r.rawPassed);
                // Passing the desktop minimum policy must never be presented as full adaptation.
                state.allSizesPassed = state.allRawChecksPassed && state.unsupportedSizeCount == 0;
                var policyPassed = state.completed && state.results.Count == Sizes.Length && state.results.All(r => r.completed)
                    && state.supportedSizesPassed && state.unsupportedNoticeChecksPassed && state.sourceUnchanged && state.sourceMemoryUnchanged
                    && state.error == "" && state.restorationErrors.Count == 0 && state.gameRestored;
                state.phase = !policyPassed ? "failed" : state.unsupportedSizeCount > 0 ? "passed-with-minimum-viewport" : "passed";
            }
            catch (Exception error) { if (state.error == "") state.error = error.ToString(); state.phase = "failed"; }
            finally { Record(state); SessionState.EraseString(Key); }
        }
        private sealed class MemoryStore : IPlotAuthoringStore
        {
            public PlotAuthoringProject Project { get; }
            private readonly List<PlotAtomRowData> _rows;
            public int SaveCalls { get; private set; }
            public MemoryStore(PlotAuthoringProject project, List<PlotAtomRowData> rows) { Project = project; _rows = rows; }
            public List<PlotAtomRowData> ListRows(int? from = null, int? to = null) => _rows.Where(r => (!from.HasValue || ID(r, Project) >= from.Value) && (!to.HasValue || ID(r, Project) <= to.Value)).ToList();
            public PlotAtomRowData FindRow(int id) => _rows.Find(r => ID(r, Project) == id);
            public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants) { SaveCalls++; throw new InvalidOperationException("多尺寸验收禁止保存任何资产"); }
            public PlotAtomRowData CreateRow(int id) => throw new NotSupportedException();
            public PlotAtomRowData RenameRow(int oldId, int newId) => throw new NotSupportedException();
            public bool DeleteRow(int id) => throw new NotSupportedException();
        }
    }
}
