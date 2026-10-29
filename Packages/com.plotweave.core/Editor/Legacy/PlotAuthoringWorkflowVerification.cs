using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// <summary>原制作UI的无落盘端到端验收。只有报告、截图和唯一的Library恢复草稿会写入磁盘。</summary>
    [InitializeOnLoad] public static class PlotAuthoringWorkflowVerification
    {
        public const string Key = "PlotWeave.AuthoringWorkflow.Preview";
        public const string LatestPath = "Tools/PlotWeaveMigration/authoring-workflow.json";
        private const string RecordFolder = "Doc/Records/Stage19";
        private const string TextA = "PlotWeave authoring input A";
        private const string TextB = "PlotWeave authoring input B";
        [Serializable] private sealed class SourceHash { public string path, before, after, memoryBefore, memoryAfter; }
        [Serializable] private sealed class State
        {
            public string id, phase, scene, authoringScene, previousScene, workspace, prefab, catalog, memoryFolder, draftPath, image, error = "";
            public bool background, hadView, maximized, completed, unsaved, sourceUnchanged, sourceMemoryUnchanged, exchangeMatched, actualSceneBindings;
            public int focusWindow, rows, saveCalls, inputChecks, previewChecks, refreshChecks, dirtyDependencyCount, layoutChecks, inputToggleChecks, guideToggleChecks;
            public Rect inputBounds, viewportBounds, previewDialogBounds;
            public double deadline, next;
            public List<string> images = new List<string>(), uiHits = new List<string>(), dirtyDependencies = new List<string>();
            public List<SourceHash> sources = new List<SourceHash>();
        }
        private static GameObject _root, _events;
        private static PlotLegacyAuthoringHost _host;
        private static EditorPlotPlayerUI _player;
        private static MemoryStore _store;
        private static bool _exchangeMatched;
        private static readonly List<UnityEngine.Object> Memory = new List<UnityEngine.Object>();
        static PlotAuthoringWorkflowVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode) return;
                var state = Read(); if (state != null) Finish(state);
            };
        }
        private static State Read()
        {
            var json = SessionState.GetString(Key, "");
            return json == "" ? null : JsonUtility.FromJson<State>(json);
        }
        private static void Save(State state) => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Record(State state)
        {
            Directory.CreateDirectory(RecordFolder); Directory.CreateDirectory(Path.GetDirectoryName(LatestPath));
            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(RecordFolder + "/authoring-workflow-" + state.id + ".json", json);
            File.WriteAllText(LatestPath, json);
        }
        private static EditorWindow Game() => Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string DataMemoryHash(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            // Compare authoring data, including dirty objects, without saving or clearing their state.
            // Fonts/textures may maintain normal runtime caches and are protected by disk hashes.
            if (!(asset is PlotAuthoringAsset) && !(asset is PlotAuthoringProject) && !(asset is PlotPlaybackVisualCatalog)) return "";
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset)))).Replace("-", "").ToLowerInvariant();
        }
        private static string Draft(string folder)
        {
            using (var sha = SHA256.Create())
                return "Library/PlotWeaveRecovery/" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(folder))).Replace("-", "").ToLowerInvariant() + "-legacy.json";
        }
        [MenuItem("Tools/PlotWeave/验收/原制作输入与实时预览闭环（不保存资产）")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Read() != null) throw new InvalidOperationException("请先结束已有PlayMode或验收");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存的场景，安全验收不会替你保存或切换它");
            var interaction = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            var ui = JsonUtility.FromJson<PlotLegacyUiReport>(File.ReadAllText(PlotLegacyUiMigration.LatestPath));
            var source = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(interaction.workspace);
            if (source == null) throw new InvalidOperationException("原制作工作区不存在");
            var prefab = ui.layouts.Single(l => l.source.EndsWith("/PlotEditorScene.unity", StringComparison.Ordinal)).asset;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null) throw new InvalidOperationException("原制作UI模板不存在");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotPlaybackExample.GameScene) != null ? PlotPlaybackExample.GameScene : interaction.playbackScene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new InvalidOperationException("没有可用的已保存公开播放场景");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(interaction.authoringScene) == null || !File.Exists(interaction.authoringScene)) throw new InvalidOperationException("真实制作入口场景不存在");
            var authoringYaml = File.ReadAllText(interaction.authoringScene);
            var hostGuid = AssetDatabase.AssetPathToGUID("Packages/com.plotweave.core/Runtime/AuthoringUI/PlotLegacyAuthoringHost.cs");
            var actualSceneBindings = !string.IsNullOrEmpty(hostGuid) && authoringYaml.Contains("guid: " + hostGuid)
                && authoringYaml.Contains("project: {fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(interaction.workspace))
                && authoringYaml.Contains("template: {fileID: ") && authoringYaml.Contains("m_Name: WindowMask");
            if (!actualSceneBindings) throw new InvalidOperationException("真实制作场景没有完整的Host/project/template/WindowMask绑定");
            var game = Game();
            var state = new State
            {
                id = Guid.NewGuid().ToString("N"), phase = "starting", scene = scene, workspace = interaction.workspace,
                prefab = prefab, catalog = interaction.catalog, authoringScene = interaction.authoringScene, actualSceneBindings = actualSceneBindings,
                previousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                background = Application.runInBackground, hadView = game != null, maximized = game != null && game.maximized,
                focusWindow = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetInstanceID() : 0,
                deadline = EditorApplication.timeSinceStartup + 180
            };
            state.memoryFolder = "Assets/PlotWeaveWorkflowMemory_" + state.id;
            state.draftPath = Draft(state.memoryFolder);
            if (Directory.Exists(state.memoryFolder) || File.Exists(state.memoryFolder + ".meta") || File.Exists(state.draftPath))
                throw new IOException("验收的唯一内存路径或恢复草稿已存在");
            var rows = new PlotEditorManager(source).ListRows();
            var assets = AssetDatabase.GetDependencies(new[] { state.workspace, state.prefab, state.catalog, state.scene, state.authoringScene }, true)
                .Concat(rows.Select(AssetDatabase.GetAssetPath)).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToList();
            foreach (var path in assets)
            {
                var loaded = AssetDatabase.LoadMainAssetAtPath(path);
                if (loaded != null && EditorUtility.IsDirty(loaded)) state.dirtyDependencies.Add(path);
            }
            state.dirtyDependencyCount = state.dirtyDependencies.Count;
            var paths = assets.Concat(assets.Select(p => p + ".meta")).Concat(new[] { source.nodeFolder + ".meta" });
            if (Directory.Exists(source.nodeFolder)) paths = paths.Concat(Directory.GetFiles(source.nodeFolder, "*", SearchOption.AllDirectories));
            foreach (var path in paths.Where(File.Exists).Select(p => p.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
                state.sources.Add(new SourceHash { path = path, before = Hash(path), memoryBefore = assets.Contains(path) ? DataMemoryHash(path) : "" });
            Save(state); Record(state);
            try { EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene); EditorApplication.isPlaying = true; }
            catch (Exception error) { state.error = error.ToString(); Finish(state); throw; }
        }
        private static T Clone<T>(T value) where T : ScriptableObject
        {
            if (value == null) return null;
            var copy = UnityEngine.Object.Instantiate(value); copy.hideFlags = HideFlags.HideAndDontSave; Memory.Add(copy); return copy;
        }
        private static T Required<T>(Component root, string name) where T : Component => root.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException("原UI缺少 " + name);
        private static void Build(State state)
        {
            // The saved start scene is only a PlayMode container; every change below is transient.
            for (var i = 0; i < SceneManager.sceneCount; i++) foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects()) root.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(state.workspace);
            var project = Clone(source); project.name = "WorkflowMemory_" + state.id; project.nodeFolder = state.memoryFolder;
            project.dialogs = Clone(source.dialogs); project.constants = Clone(source.constants); project.actors = Clone(source.actors);
            project.eventTracks = Clone(source.eventTracks);
            project.visuals = Clone(source.visuals != null ? source.visuals : AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(state.catalog));
            if (project.visuals != null)
            {
                project.visuals.playerConfig = JsonUtility.FromJson<PlotPlayerConfig>(JsonUtility.ToJson(project.visuals.playerConfig ?? new PlotPlayerConfig()));
                project.visuals.playerConfig.TextSpeed = 0;
            }
            var rows = new PlotEditorManager(source).ListRows().Select(Clone).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("原工作区没有节点");
            _store = new MemoryStore(project, rows); state.rows = rows.Count; _exchangeMatched = false;
            _root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(state.prefab));
            _root.name = "PlotWeave Workflow Verification " + state.id;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas))
            { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000; }
            _root.SetActive(true);
            if (EventSystem.current == null) _events = new GameObject("Workflow EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            _host = _root.AddComponent<PlotLegacyAuthoringHost>();
            _host.template = _root.GetComponent<PlotLegacyTemplateBindings>();
            _host.Initialize(_store, () => throw new InvalidOperationException("验收禁止保存或丢弃当前输入"), _ => false);
            _player = new EditorPlotPlayerUI(project, () => PlotPlaybackCompiler.Compile(_host.Session), _host.PreviewViewport);
            _host.Preview = _player.Play; _host.ClosePreview = _player.Dispose;
            _host.RefreshPreview = () => _player.Refresh(); _host.ValidatePreview = () => PlotPlaybackCompiler.Compile(_host.Session);
            _host.OpenDataExchange = () => _exchangeMatched = ReferenceEquals(_host.project, project) && ReferenceEquals(_host.Session.Store.Project, project);
            var row = _host.Session.WorkingRow;
            // Use one controlled synthetic line, retaining all other imported rows unchanged in memory.
            row.SetColumnSingleValue("PlotRoleID", "0"); row.SetColumnSingleValue("Next", "0");
            foreach (var column in row.headerDefinition.sheets.Where(c => c.IsList)) row.SetColumnRows(column.columnName, new List<PlotAtomRowData.StringList>());
            if (project.eventTracks != null) project.eventTracks.tracks.RemoveAll(t => t.nodeID == ID(row, project));
            _host.Session.SetDialogKey("__plotweave_workflow_" + state.id);
            Required<TMP_InputField>(_host, "PlayerStartInputField").SetTextWithoutNotify(ID(row, project).ToString(CultureInfo.InvariantCulture));
            state.phase = "input"; state.next = EditorApplication.timeSinceStartup + 1; Save(state); Record(state);
        }
        private static int ID(PlotAtomRowData row, PlotAuthoringProject project) => int.Parse(row.GetColumnSingleValue(project.idColumn), CultureInfo.InvariantCulture);
        private static string Hierarchy(Transform value)
        {
            var names = new Stack<string>(); for (var at = value; at != null; at = at.parent) names.Push(at.name); return string.Join("/", names);
        }
        private static RaycastResult CheckHit(State state, Component control, string label)
        {
            if (!control.gameObject.activeInHierarchy) throw new InvalidOperationException("控件未显示: " + label);
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)control.transform;
            var canvas = control.GetComponentInParent<Canvas>(); var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                if (point.x < -1 || point.x > Screen.width + 1 || point.y < -1 || point.y > Screen.height + 1) throw new InvalidOperationException("控件边界超出屏幕: " + label + " @" + point);
            }
            var center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var system = EventSystem.current; if (system == null || !system.isActiveAndEnabled) throw new InvalidOperationException("缺少活动EventSystem");
            var hits = new List<RaycastResult>(); system.RaycastAll(new PointerEventData(system) { position = center }, hits);
            var top = hits.FirstOrDefault(h => h.gameObject != null);
            if (top.gameObject == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject) != control.gameObject)
                throw new InvalidOperationException("控件射线被遮挡: " + label + "; top=" + (top.gameObject == null ? "<none>" : Hierarchy(top.gameObject.transform)));
            state.uiHits.Add(label + " -> " + Hierarchy(top.gameObject.transform) + " @" + center); Save(state); return top;
        }
        private static void Click(State state, Button button, string label)
        {
            if (!button.IsInteractable()) throw new InvalidOperationException("按钮不可交互: " + label);
            var hit = CheckHit(state, button, label);
            var pointer = new PointerEventData(EventSystem.current) { position = hit.screenPosition, button = PointerEventData.InputButton.Left, clickCount = 1, eligibleForClick = true, pointerCurrentRaycast = hit, pointerPressRaycast = hit };
            if (!ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler)) throw new InvalidOperationException("按钮没有响应: " + label);
            if (_host.LastError != "") throw new InvalidOperationException(_host.LastError);
        }
        private static void Input(State state, string value)
        {
            var input = Required<TMP_InputField>(_host, "DialogInputField"); CheckHit(state, input, "对话输入");
            input.SetTextWithoutNotify(value); input.onValueChanged.Invoke(value);
            if (_host.LastError != "" || _host.Session.Dialogs.GetDialogContent(_host.Session.CurrentDialogKey, 0) != value)
                throw new InvalidOperationException("原输入控件未将文字写入会话: " + _host.LastError);
            state.inputChecks++; Save(state);
        }
        private static Rect ScreenBounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera, c)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private static void CheckDocking(State state, PlotPlayerViewBindings bindings, string expected)
        {
            var input = Required<TMP_InputField>(_host, "DialogInputField");
            var box = Required<RectTransform>(_host, "DialogBoxBG");
            state.inputBounds = ScreenBounds(box); state.viewportBounds = ScreenBounds(_host.PreviewViewport);
            state.previewDialogBounds = ScreenBounds((RectTransform)bindings.dialogBox.transform);
            if (state.inputBounds.Overlaps(state.viewportBounds) || state.inputBounds.Overlaps(state.previewDialogBounds))
                throw new InvalidOperationException("编辑输入区与预览画面或预览对白框重叠");
            if (input.text != expected || bindings.dialog.text != expected || bindings.dialog.color.a <= .01f || !bindings.dialogBox.activeInHierarchy)
                throw new InvalidOperationException("输入区与播放结果没有同时显示相同文字");
            CheckHit(state, Required<Button>(_host, "RefreshPlayDataButton"), "停靠后刷新入口");
            foreach (var unit in _host.template.Scripts("RowUnit").Where(p => p.gameObject.activeInHierarchy))
            {
                var button = unit.Field<Button>("OpenButton");
                if (button != null) CheckHit(state, button, "停靠后字段入口 " + button.transform.parent.name);
            }
            state.layoutChecks++; Save(state);
        }
        private static void CheckToggles(State state, bool inputToggle)
        {
            var target = inputToggle ? Required<Transform>(_host, "DialogBoxBG") : _host.PreviewViewport.Find("PosCtrl");
            if (target == null) throw new InvalidOperationException("原输入框或站位参考未保留");
            var button = Required<Button>(_host, inputToggle ? "DialogBoxSwitch" : "PositionGuides");
            var before = target.gameObject.activeSelf;
            if (!inputToggle && before) throw new InvalidOperationException("站位参考应默认隐藏，避免占用输入区");
            Click(state, button, inputToggle ? "隐藏输入区" : "显示站位参考");
            if (target.gameObject.activeSelf == before) throw new InvalidOperationException("原UI开关没有改变显示状态");
            Click(state, button, inputToggle ? "恢复输入区" : "隐藏站位参考");
            if (target.gameObject.activeSelf != before) throw new InvalidOperationException("原UI开关没有恢复显示状态");
            if (inputToggle) state.inputToggleChecks++; else state.guideToggleChecks++;
            Save(state);
        }
        private static void CheckPreview(State state, string expected)
        {
            var player = _player.Host;
            if (player == null || player.Core == null || player.Core.State == PlotPlayerState.Faulted) throw new InvalidOperationException("预览Core未正常运行");
            var bindings = player.View.CaptureBindings();
            if (bindings.dialog.text != expected || !bindings.dialog.gameObject.activeInHierarchy) throw new InvalidOperationException("预览View文字不匹配: " + bindings.dialog.text);
            if (!player.Core.GetDataProvider().TryGetText(_host.Session.CurrentDialogKey, false, 0, out var text) || text != expected) throw new InvalidOperationException("Core数据未使用当前输入");
            if (!_host.Session.HasUnsavedChanges || _store.SaveCalls != 0) throw new InvalidOperationException("试跑改变了未保存状态或调用了保存");
            var viewport = _host.PreviewViewport;
            if (!player.transform.IsChildOf(viewport) || viewport.name != "WindowMask") throw new InvalidOperationException("预览没有嵌入原WindowMask");
            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null) throw new InvalidOperationException("原WindowMask没有裁剪组件");
            foreach (var canvas in player.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas || canvas.overrideSorting) throw new InvalidOperationException("预览Canvas逃离了制作Canvas排序");
            foreach (var rect in new[] { (RectTransform)player.transform, bindings.uiRoot.transform as RectTransform })
            {
                if (rect == null) throw new InvalidOperationException("预览没有RectTransform");
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners) { var p = viewport.InverseTransformPoint(corner); if (!new Rect(viewport.rect.x - 1, viewport.rect.y - 1, viewport.rect.width + 2, viewport.rect.height + 2).Contains(p)) throw new InvalidOperationException("预览根越过WindowMask裁剪边界"); }
            }
            CheckHit(state, Required<TMP_InputField>(_host, "DialogInputField"), "预览时仍可输入");
            CheckHit(state, Required<Button>(_host, "PlayButton"), "预览时试跑入口");
            CheckDocking(state, bindings, expected);
            state.saveCalls = _store.SaveCalls; state.unsaved = _host.Session.HasUnsavedChanges; Save(state);
        }
        private static void Capture(State state, string label, string next)
        {
            state.image = RecordFolder + "/authoring-workflow-" + state.id + "-" + label + ".png";
            state.images.Add(state.image); state.phase = next; Save(state); Record(state);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.image));
        }
        private static bool Captured(State state) => File.Exists(state.image) && new FileInfo(state.image).Length > 0;
        private static void Update()
        {
            var state = Read(); if (state == null || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("制作输入闭环验收超过180秒");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
                var game = Game(); if (game == null) game = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                game.maximized = true; game.Repaint();
                if (state.phase == "starting") { Build(state); return; }
                if (_host == null || _player == null || _store == null) throw new InvalidOperationException("验收内存事务丢失，禁止回退到真实工作区");
                if (_host.LastError != "") throw new InvalidOperationException(_host.LastError);
                if (EditorApplication.timeSinceStartup < state.next) return;
                switch (state.phase)
                {
                    case "input": CheckToggles(state, true); Input(state, TextA); Capture(state, "input", "input-capture"); return;
                    case "input-capture":
                        if (!Captured(state)) return;
                        Click(state, Required<Button>(_host, "PlayButton"), "原试跑按钮"); state.phase = "preview"; state.next = EditorApplication.timeSinceStartup + 1; Save(state); return;
                    case "preview": CheckPreview(state, TextA); state.previewChecks++; Capture(state, "preview", "preview-capture"); return;
                    case "preview-capture":
                        if (!Captured(state)) return;
                        Input(state, TextB); Click(state, Required<Button>(_host, "RefreshPlayDataButton"), "原刷新播放数据按钮"); state.phase = "refresh"; state.next = EditorApplication.timeSinceStartup + 1; Save(state); return;
                    case "refresh":
                        CheckPreview(state, TextB); state.refreshChecks++;
                        CheckToggles(state, false);
                        Click(state, Required<Button>(_host, "DataExchange"), "当前工作区表格入口");
                        state.exchangeMatched = _exchangeMatched;
                        if (!state.exchangeMatched) throw new InvalidOperationException("表格入口没有传递当前内存工作区");
                        Capture(state, "refresh", "refresh-capture"); return;
                    case "refresh-capture":
                        if (!Captured(state)) return;
                        state.completed = true; state.phase = "stopping"; Save(state); Record(state); Cleanup(); EditorApplication.isPlaying = false; return;
                }
            }
            catch (Exception error)
            {
                if (_store != null) state.saveCalls = _store.SaveCalls;
                if (_host != null && _host.Session != null) state.unsaved = _host.Session.HasUnsavedChanges;
                state.error = error.ToString(); state.phase = "stopping"; Save(state); Record(state); Cleanup();
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false; else Finish(state);
                Debug.LogException(error);
            }
        }
        private static void Cleanup()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            else _player?.Dispose();
            if (_events != null) UnityEngine.Object.DestroyImmediate(_events);
            foreach (var item in Memory) if (item != null && !EditorUtility.IsPersistent(item)) UnityEngine.Object.DestroyImmediate(item);
            Memory.Clear(); _root = null; _events = null; _host = null; _player = null; _store = null; _exchangeMatched = false;
        }
        private static void Finish(State state)
        {
            try
            {
                Cleanup(); Application.runInBackground = state.background;
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.previousScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene);
                var game = Game(); if (game != null) { if (state.hadView) game.maximized = state.maximized; else game.Close(); }
                var focus = EditorUtility.InstanceIDToObject(state.focusWindow) as EditorWindow; if (focus != null) focus.Focus();
                var expectedFolder = "Assets/PlotWeaveWorkflowMemory_" + state.id;
                if (state.memoryFolder != expectedFolder || state.draftPath != Draft(expectedFolder)) throw new InvalidOperationException("拒绝清理身份不匹配的草稿路径");
                var expected = Path.GetFullPath(Draft(expectedFolder)); var recovery = Path.GetFullPath("Library/PlotWeaveRecovery") + Path.DirectorySeparatorChar;
                if (!expected.StartsWith(recovery, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("恢复草稿超出Library范围");
                if (File.Exists(expected)) File.Delete(expected);
                if (Directory.Exists(expectedFolder) || File.Exists(expectedFolder + ".meta")) throw new InvalidOperationException("内存测试意外创建了Assets路径");
                foreach (var source in state.sources)
                {
                    source.after = File.Exists(source.path) ? Hash(source.path) : "<missing>";
                    source.memoryAfter = string.IsNullOrEmpty(source.memoryBefore) ? "" : DataMemoryHash(source.path);
                }
                state.sourceUnchanged = state.sources.Count > 0 && state.sources.All(s => s.before == s.after);
                state.sourceMemoryUnchanged = state.sources.Where(s => !string.IsNullOrEmpty(s.memoryBefore)).All(s => s.memoryBefore == s.memoryAfter);
                if (!state.sourceUnchanged || !state.sourceMemoryUnchanged) throw new InvalidOperationException("原工作区的磁盘或制作数据内存SHA256前后不一致");
                if (!state.completed || state.images.Count != 3 || !state.images.All(File.Exists) || state.inputChecks != 2 || state.previewChecks != 1 || state.refreshChecks != 1
                    || state.layoutChecks != 2 || state.inputToggleChecks != 1 || state.guideToggleChecks != 1 || !state.actualSceneBindings || !state.unsaved || state.saveCalls != 0 || !state.exchangeMatched)
                    throw new InvalidOperationException("原制作输入闭环验收未完整完成");
                state.phase = state.error == "" ? "passed" : "failed";
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
            public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
            { SaveCalls++; throw new InvalidOperationException("安全工作流验收禁止保存任何资产"); }
            public PlotAtomRowData CreateRow(int id) => throw new NotSupportedException("安全验收禁止创建节点");
            public PlotAtomRowData RenameRow(int oldId, int newId) => throw new NotSupportedException("安全验收禁止重命名节点");
            public bool DeleteRow(int id) => throw new NotSupportedException("安全验收禁止删除节点");
        }
    }
}
