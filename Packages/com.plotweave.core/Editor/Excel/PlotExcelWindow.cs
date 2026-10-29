using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring.Excel
{
    public sealed class PlotExcelWindow : EditorWindow
    {
        private PlotAuthoringProject _project;
        private string _folder = "";
        private string _status = "选择工作区后导出；在Excel中编辑副本，再检查导入。";
        private PlotExcelImportPlan _plan;
        private Vector2 _scroll;
        private Func<bool> _resolveEdits;
        private Action _afterImport;
        public PlotAuthoringProject Project => _project;
        [MenuItem("Tools/PlotWeave/制作工具/Excel与Luban交换")]
        public static void Open() => GetWindow<PlotExcelWindow>("PlotWeave 表格交换");
        public static PlotExcelWindow OpenFor(PlotAuthoringProject project, Func<bool> resolveEdits = null, Action afterImport = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var window = GetWindow<PlotExcelWindow>("PlotWeave 表格交换");
            window._plan?.Dispose(); window._plan = null;
            window._project = project; window._resolveEdits = resolveEdits; window._afterImport = afterImport;
            window._status = "已绑定当前制作工作区；导出/导入前会处理尚未保存的输入。";
            window.Show(); window.Focus(); return window;
        }
        private bool PrepareCurrentInput() => _resolveEdits == null || _resolveEdits();
        private void OnDisable() { _plan?.Dispose(); _plan = null; }
        private void Run(Action action)
        { try { action(); } catch (Exception error) { _status = error.Message; Debug.LogException(error); } }
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("原布局双向交换", EditorStyles.boldLabel);
            var previousProject = _project;
            EditorGUI.BeginChangeCheck(); _project = (PlotAuthoringProject)EditorGUILayout.ObjectField("工作区", _project, typeof(PlotAuthoringProject), false);
            _folder = EditorGUILayout.TextField("工作簿目录", _folder);
            if (EditorGUI.EndChangeCheck()) { _plan?.Dispose(); _plan = null; }
            if (previousProject != _project) { _resolveEdits = null; _afterImport = null; }
            if (GUILayout.Button("选择已有工作簿目录"))
            { var selected = EditorUtility.OpenFolderPanel("包含6份配套工作簿的目录", _folder, ""); if (selected != "") { _folder = selected; _plan?.Dispose(); _plan = null; } }
            using (new EditorGUI.DisabledScope(_project == null))
            {
                if (GUILayout.Button("导出工作区到新目录")) Run(() =>
                {
                    if (!PrepareCurrentInput()) return;
                    var parent = EditorUtility.OpenFolderPanel("选择导出父目录", "", ""); if (parent == "") return;
                    _folder = Path.Combine(parent, "PlotWeave_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                    PlotExcelExchange.Export(_project, _folder); _plan?.Dispose(); _plan = null; _status = "已导出6份独立工作簿：" + _folder;
                });
                if (GUILayout.Button("检查导入（暂不修改资产）")) Run(() =>
                { if (!PrepareCurrentInput()) return; _plan?.Dispose(); _plan = null; _plan = PlotExcelExchange.PlanImport(_project, _folder); _status = _plan.Summary; });
            }
            using (new EditorGUI.DisabledScope(_plan == null))
                if (GUILayout.Button("应用已检查的导入计划")) Run(() =>
                { if (!PrepareCurrentInput()) return; _plan.Commit("Library/PlotWeaveRecovery/ExcelImportBackups"); _status = _plan.Summary + "\n备份：" + _plan.LastBackupDirectory; _plan.Dispose(); _plan = null; _afterImport?.Invoke(); });
            EditorGUILayout.Space();
            if (GUILayout.Button("复制工作簿到Luban数据目录")) Run(() =>
            {
                var destination = EditorUtility.OpenFolderPanel("Luban数据目录", "", ""); if (destination == "") return;
                var backup = PlotExcelExchange.CopyToLubanData(_folder, destination, "Library/PlotWeaveRecovery/ExcelHandoffBackups");
                _status = "已复制6份工作簿并核对SHA256。目标原文件备份：" + backup + "\n本工具未执行Luban编译。";
            });
            EditorGUILayout.HelpBox(_status, MessageType.Info);
            EditorGUILayout.HelpBox("导入采用节点合并：更新表中已有节点、新增表中新节点，保留未出现在表中的节点。类型、表头、文本与演员表按表内容整体更新。仅导出数据布局，不保留既有工作簿样式、宏或其他工作表。", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    public static class PlotExcelExample
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage04_Exchange";
        [MenuItem("Tools/PlotWeave/迁移样例/验证原布局表格往返")]
        public static void PrepareVerification()
        {
            var sourcePath = Root + "/Source/Workspace.asset";
            var source = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(sourcePath);
            if (source == null) source = PlotAuthoringBootstrap.CreateWorkspace(Root + "/Source");
            var run = Guid.NewGuid().ToString("N");
            var folder = Root + "/Runs/" + run;
            var exported = folder + "/Workbooks"; PlotExcelExchange.Export(source, exported);
            var target = PlotAuthoringBootstrap.CreateWorkspace(folder + "/RoundTrip");
            using (var plan = PlotExcelExchange.PlanImport(target, exported))
            {
                plan.Commit("Library/PlotWeaveRecovery/ExcelImportBackups");
                PlotExcelExchange.CopyToLubanData(exported, folder + "/LubanData", "Library/PlotWeaveRecovery/ExcelHandoffBackups");
                Directory.CreateDirectory("Doc/Records/Stage04");
                File.WriteAllText("Doc/Records/Stage04/exchange-" + run + ".txt", "Unity内真实Asset→XLSX→新工作区资产往返通过\n" + plan.Summary + "\n" + exported + "\nLuban交接6份文件SHA256一致；未执行外部编译器。\n" + string.Join("\n", Array.ConvertAll(PlotExcelExchange.FileNames, name => name + " SHA256=" + PlotExcelExchange.Hash(Path.Combine(exported, name)))));
            }
            AssetDatabase.Refresh(); Debug.Log("PlotWeave原布局交换验证完成: " + folder);
        }
    }
}
