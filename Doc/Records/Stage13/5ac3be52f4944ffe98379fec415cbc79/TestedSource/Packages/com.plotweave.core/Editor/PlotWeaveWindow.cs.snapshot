using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlotWeave.Editor
{
    public sealed class PlotWeaveWindow : EditorWindow
    {
        [SerializeField] private PlotAsset _asset;
        [SerializeField] private string _selectedId;
        private PlotGraphView _graph;
        private IMGUIContainer _inspector;
        private bool _global;
        private bool _rebuildPending;
        private string _notice = "选择剧情资产，或创建演示。";

        [MenuItem("Tools/PlotWeave/历史原型/打开GraphView编辑器")]
        public static void Open() { GetWindow<PlotWeaveWindow>("PlotWeave 织幕"); }
        public static void Open(PlotAsset asset)
        {
            var window = GetWindow<PlotWeaveWindow>("PlotWeave 织幕"); window.SetAsset(asset);
        }
        private void OnEnable() { Undo.undoRedoPerformed += Rebuild; }
        private void OnDisable() { Undo.undoRedoPerformed -= Rebuild; }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            var picker = new ObjectField { objectType = typeof(PlotAsset), allowSceneObjects = false, value = _asset };
            picker.style.minWidth = 180;
            picker.RegisterValueChangedCallback(evt => SetAsset(evt.newValue as PlotAsset));
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(() => SetAsset(PlotMenus.CreateStory(false))) { text = "新建" });
            toolbar.Add(new ToolbarButton(() => SetAsset(PlotMenus.CreateStory(true))) { text = "示例" });
            toolbar.Add(new ToolbarButton(Validate) { text = "校验" });
            toolbar.Add(new ToolbarButton(() => { if (_asset != null) PlotPreviewWindow.Open(_asset, null); }) { text = "预览" });
            var dataMenu = new ToolbarMenu { text = "数据" };
            dataMenu.menu.AppendAction("保存资产", _ => AssetDatabase.SaveAssets());
            dataMenu.menu.AppendAction("导出 Excel", _ => WithAsset(PlotMenus.ExportWorkbook));
            dataMenu.menu.AppendAction("Excel 编译成 JSON", _ => PlotMenus.CompileWorkbook());
            dataMenu.menu.AppendAction("导出运行时 JSON", _ => WithAsset(PlotMenus.ExportJson));
            dataMenu.menu.AppendAction("导入 PlotWeave JSON 为新资产", _ => SetAsset(PlotMenus.ImportJson()));
            dataMenu.menu.AppendAction("从事件定义生成 C# 接口", _ => WithAsset(PlotCodeGenerator.Generate));
            dataMenu.menu.AppendAction("生成参考播放场景", _ => WithAsset(PlotMenus.CreateDemoScene));
            toolbar.Add(dataMenu);
            toolbar.Add(new ToolbarButton(() => { _global = !_global; _inspector.MarkDirtyRepaint(); }) { text = "节点 / 全局配置" });
            rootVisualElement.Add(toolbar);
            var split = new TwoPaneSplitView(1, 355, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            _graph = new PlotGraphView(this); split.Add(_graph);
            _inspector = new IMGUIContainer(DrawInspector); _inspector.style.minWidth = 280;
            var scroll = new ScrollView(); scroll.Add(_inspector); split.Add(scroll);
            rootVisualElement.Add(split);
            var footer = new Label { name = "notice" }; footer.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(footer);
            Rebuild();
        }

        internal PlotAsset Asset => _asset;
        internal void SelectNode(string id) { _selectedId = id; _global = false; _inspector?.MarkDirtyRepaint(); }
        internal void Change(string label, Action edit, bool rebuild = true)
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, label); edit(); EditorUtility.SetDirty(_asset);
            if (rebuild) RequestRebuild();
        }
        internal void SetNotice(string message)
        {
            _notice = message;
            var label = rootVisualElement.Q<Label>("notice"); if (label != null) label.text = message;
        }
        private void WithAsset(Action<PlotAsset> action)
        {
            if (_asset == null) { SetNotice("请先选择剧情资产。"); return; }
            try { action(_asset); SetNotice("操作完成。"); }
            catch (Exception ex) { SetNotice(ex.Message); Debug.LogException(ex); }
        }
        private void SetAsset(PlotAsset asset)
        {
            if (asset == null) return;
            _asset = asset; _selectedId = null; Rebuild();
            var field = rootVisualElement.Q<ObjectField>(); if (field != null) field.SetValueWithoutNotify(asset);
        }
        private void RequestRebuild()
        {
            if (_rebuildPending) return;
            _rebuildPending = true;
            rootVisualElement.schedule.Execute(() => { _rebuildPending = false; Rebuild(); });
        }
        private void Rebuild()
        {
            _graph?.Load(_asset); _inspector?.MarkDirtyRepaint(); SetNotice(_notice);
        }
        private void Validate()
        {
            if (_asset == null) return;
            var check = PlotValidation.Check(_asset.document);
            SetNotice(check.IsValid ? "校验通过。" + string.Join("；", check.Warnings) : string.Join("；", check.Errors));
        }
        private void DrawInspector()
        {
            if (_asset == null) { EditorGUILayout.HelpBox("右键画布创建节点。连线、位置和属性支持 Undo。", MessageType.Info); return; }
            var serialized = new SerializedObject(_asset); serialized.Update();
            var document = serialized.FindProperty("document");
            if (_global)
            {
                EditorGUILayout.LabelField("全局剧情配置", EditorStyles.boldLabel);
                foreach (var name in new[] { "id", "title", "entryNodeId", "actors", "texts", "variables", "commands", "enums" })
                    EditorGUILayout.PropertyField(document.FindPropertyRelative(name), true);
            }
            else
            {
                var index = _asset.document.nodes.FindIndex(n => n.id == _selectedId);
                if (index < 0) { EditorGUILayout.HelpBox("选择节点查看属性；全局配置中可编辑角色、文本 Key、变量、事件及枚举。", MessageType.Info); return; }
                var node = document.FindPropertyRelative("nodes").GetArrayElementAtIndex(index);
                EditorGUILayout.LabelField("节点 " + _selectedId, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(node.FindPropertyRelative("id"));
                foreach (var name in new[] { "title", "sceneId", "actorId", "textKey", "text", "nextNodeId", "choices", "commands" })
                    EditorGUILayout.PropertyField(node.FindPropertyRelative(name), true);
                if (GUILayout.Button("添加事件")) ShowCommandMenu(index);
                if (GUILayout.Button("设为入口")) Change("设置剧情入口", () => _asset.document.entryNodeId = _selectedId);
                if (GUILayout.Button("从当前节点预览")) PlotPreviewWindow.Open(_asset, _selectedId);
            }
            if (serialized.ApplyModifiedProperties()) RequestRebuild();
        }
        private void ShowCommandMenu(int nodeIndex)
        {
            var menu = new GenericMenu();
            var definitions = _asset.document.commands;
            foreach (var definition in definitions)
            {
                var captured = definition;
                menu.AddItem(new GUIContent(captured.displayName + " (" + captured.key + ")"), false, () => Change("添加剧情事件", () =>
                {
                    var command = new PlotCommand { key = captured.key };
                    foreach (var p in captured.parameters) command.With(p.key, p.defaultValue);
                    _asset.document.nodes[nodeIndex].commands.Add(command);
                }));
            }
            foreach (var key in new[] { "actor.show", "actor.move", "actor.emote", "actor.hide", "background", "audio.bgm", "audio.sfx", "effect" })
            {
                var captured = key;
                if (definitions.Any(d => d.key == captured)) continue;
                menu.AddItem(new GUIContent(captured), false, () => Change("添加表现事件", () => _asset.document.nodes[nodeIndex].commands.Add(new PlotCommand { key = captured })));
            }
            menu.ShowAsContext();
        }
    }

    internal sealed class PlotGraphView : GraphView
    {
        [Serializable] private sealed class ClipboardNodes { public List<PlotNode> nodes = new List<PlotNode>(); }
        private const string ClipboardPrefix = "PlotWeaveClipboard:";
        private readonly PlotWeaveWindow _window;
        private bool _loading;
        private readonly Dictionary<string, PlotNodeView> _nodes = new Dictionary<string, PlotNodeView>();
        public PlotGraphView(PlotWeaveWindow window)
        {
            _window = window; style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger()); this.AddManipulator(new SelectionDragger()); this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground()); graphViewChanged = Changed;
            serializeGraphElements = elements => ClipboardPrefix + JsonUtility.ToJson(new ClipboardNodes { nodes = elements.OfType<PlotNodeView>().Select(n => n.Data).ToList() });
            canPasteSerializedData = value => value != null && value.StartsWith(ClipboardPrefix, StringComparison.Ordinal);
            unserializeAndPaste = (operation, value) =>
            {
                if (!canPasteSerializedData(value)) return;
                var clipboard = JsonUtility.FromJson<ClipboardNodes>(value.Substring(ClipboardPrefix.Length));
                if (clipboard?.nodes == null) return;
                _window.Change("粘贴剧情节点", () =>
                {
                    var ids = clipboard.nodes.ToDictionary(n => n.id, _ => Guid.NewGuid().ToString("N"));
                    foreach (var node in clipboard.nodes)
                    {
                        node.id = ids[node.id]; node.position += new Vector2(40, 40);
                        node.nextNodeId = ids.TryGetValue(node.nextNodeId ?? "", out var next) ? next : "";
                        foreach (var choice in node.choices) choice.targetNodeId = ids.TryGetValue(choice.targetNodeId ?? "", out var target) ? target : "";
                        _window.Asset.document.nodes.Add(node);
                    }
                    if (string.IsNullOrEmpty(_window.Asset.document.entryNodeId) && clipboard.nodes.Count > 0) _window.Asset.document.entryNodeId = clipboard.nodes[0].id;
                });
            };
        }
        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter) => ports.Where(p => p.direction != start.direction && p.portType == start.portType).ToList();
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var position = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("创建对话节点", _ => AddNode(position, false));
            evt.menu.AppendAction("创建分支节点", _ => AddNode(position, true));
            base.BuildContextualMenu(evt);
        }
        private void AddNode(Vector2 position, bool choice)
        {
            _window.Change("创建剧情节点", () =>
            {
                var node = new PlotNode { id = Guid.NewGuid().ToString("N"), title = choice ? "分支" : "对话", position = position };
                if (choice) { node.choices.Add(new PlotChoice { label = "选项一" }); node.choices.Add(new PlotChoice { label = "选项二" }); }
                _window.Asset.document.nodes.Add(node);
                if (string.IsNullOrEmpty(_window.Asset.document.entryNodeId)) _window.Asset.document.entryNodeId = node.id;
                _window.SelectNode(node.id);
            });
        }
        public void Load(PlotAsset asset)
        {
            _loading = true;
            try
            {
                DeleteElements(graphElements.ToList()); _nodes.Clear();
                if (asset == null) return;
                foreach (var node in asset.document.nodes)
                {
                    if (node == null || _nodes.ContainsKey(node.id)) continue;
                    var view = new PlotNodeView(node, node.id == asset.document.entryNodeId, _window.SelectNode);
                    _nodes.Add(node.id, view); AddElement(view);
                }
                foreach (var view in _nodes.Values)
                {
                    var node = view.Data;
                    Connect(view.Outputs[0], node.nextNodeId);
                    for (var i = 0; i < node.choices.Count; i++) Connect(view.Outputs[i + 1], node.choices[i].targetNodeId);
                }
            }
            finally { _loading = false; }
        }
        private void Connect(Port output, string target)
        {
            if (!string.IsNullOrEmpty(target) && _nodes.TryGetValue(target, out var view)) AddElement(output.ConnectTo(view.Input));
        }
        private GraphViewChange Changed(GraphViewChange change)
        {
            if (_loading || _window.Asset == null) return change;
            if (change.elementsToRemove != null)
                _window.Change("删除剧情元素", () =>
                {
                    foreach (var edge in change.elementsToRemove.OfType<Edge>()) SetTarget(edge, "");
                    foreach (var view in change.elementsToRemove.OfType<PlotNodeView>())
                    {
                        _window.Asset.document.nodes.Remove(view.Data);
                        foreach (var n in _window.Asset.document.nodes)
                        {
                            if (n.nextNodeId == view.Data.id) n.nextNodeId = "";
                            foreach (var c in n.choices) if (c.targetNodeId == view.Data.id) c.targetNodeId = "";
                        }
                        if (_window.Asset.document.entryNodeId == view.Data.id) _window.Asset.document.entryNodeId = "";
                    }
                }, false);
            if (change.edgesToCreate != null)
                _window.Change("连接剧情节点", () => { foreach (var edge in change.edgesToCreate) SetTarget(edge, ((PlotNodeView)edge.input.node).Data.id); }, false);
            if (change.movedElements != null)
                _window.Change("移动剧情节点", () => { foreach (var view in change.movedElements.OfType<PlotNodeView>()) view.Data.position = view.GetPosition().position; }, false);
            return change;
        }
        private static void SetTarget(Edge edge, string id)
        {
            if (!(edge.output?.node is PlotNodeView source) || !(edge.output.userData is int index)) return;
            if (index < 0) source.Data.nextNodeId = id;
            else if (index < source.Data.choices.Count) source.Data.choices[index].targetNodeId = id;
        }
    }

    internal sealed class PlotNodeView : Node
    {
        public PlotNode Data { get; }
        public Port Input { get; }
        public List<Port> Outputs { get; } = new List<Port>();
        private readonly Action<string> _selected;
        public PlotNodeView(PlotNode data, bool entry, Action<string> selected)
        {
            Data = data; _selected = selected; title = (entry ? "入口 · " : "") + data.title;
            viewDataKey = data.id;
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string)); Input.portName = "输入"; inputContainer.Add(Input);
            AddOutput("Next", -1);
            for (var i = 0; i < data.choices.Count; i++) AddOutput(data.choices[i].label, i);
            var summary = new Label(string.IsNullOrEmpty(data.textKey) ? data.text : "文本 Key：" + data.textKey);
            summary.style.whiteSpace = WhiteSpace.Normal; summary.style.maxWidth = 240; extensionContainer.Add(summary);
            SetPosition(new Rect(data.position, new Vector2(260, 180))); RefreshExpandedState(); RefreshPorts();
        }
        private void AddOutput(string label, int index)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(string));
            port.portName = label; port.userData = index; outputContainer.Add(port); Outputs.Add(port);
        }
        public override void OnSelected() { base.OnSelected(); _selected(Data.id); }
    }
}
