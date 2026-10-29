using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring.Excel
{
    public static class PlotExcelExchange
    {
        public const string EnumFile = "__plotenums__.xlsx";
        public const string BeanFile = "__plotbeans__.xlsx";
        public const string NodeFile = "剧情基础节点.xlsx";
        public const string DialogFile = "剧情对话文本表.xlsx";
        public const string ConstantFile = "剧情常量文本表.xlsx";
        public const string ActorFile = "剧情资源演员表.xlsx";
        public static readonly string[] FileNames = { EnumFile, BeanFile, NodeFile, DialogFile, ConstantFile, ActorFile };

        /// <summary>导出到空目录；不覆盖用户工作簿或原项目表格。</summary>
        public static string[] Export(PlotAuthoringProject project, string folder)
        {
            RequireProject(project); var root = Path.GetFullPath(folder);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("请选择空目录；导出会生成独立的6份工作簿");
            var store = new PlotEditorManager(project);
            var sheets = new[] { PlotAuthoringExcel.ExportEnums(project.header.parameterEnums), PlotAuthoringExcel.ExportBeans(project.header.parameterBean),
                PlotAuthoringExcel.ExportNodes(project.header, store.ListRows(), project.idColumn), PlotAuthoringExcel.ExportTexts(project.dialogs),
                PlotAuthoringExcel.ExportTexts(project.constants), PlotAuthoringExcel.ExportTable(project.actors) };
            // 所有数据通过校验后才生成文件；失败只清理本次新建的工作簿。
            Directory.CreateDirectory(root); var written = new List<string>();
            try
            {
                for (var i = 0; i < sheets.Length; i++) { var path = Path.Combine(root, FileNames[i]); PlotExcelFile.Write(path, sheets[i]); written.Add(path); }
                return written.ToArray();
            }
            catch { foreach (var path in written) if (File.Exists(path)) File.Delete(path); throw; }
        }
        internal static void RequireProject(PlotAuthoringProject project)
        {
            if (project == null || project.header == null || project.header.parameterEnums == null || project.header.parameterBean == null || project.dialogs == null || project.constants == null || project.actors == null)
                throw new ArgumentException("工作区需要完整的枚举、Bean、表头、对话、常量和演员表引用");
            if (project.header.parameterBean.parameterEnums != project.header.parameterEnums) throw new ArgumentException("Bean与表头必须共享枚举引用");
            _ = new PlotEditorManager(project);
        }
        public static PlotExcelImportPlan PlanImport(PlotAuthoringProject project, string folder) => new PlotExcelImportPlan(project, folder);
        internal static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }

        /// <summary>对应原OutputPlotDataToLubanData的文件交接职责；不声称已执行Luban编译。</summary>
        public static string CopyToLubanData(string source, string destination, string backupRoot)
        {
            var from = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
            var to = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || from.StartsWith(to + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || to.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("交接源目录与目标目录不得相同或嵌套");
            foreach (var name in FileNames)
            {
                var file = Path.Combine(from, name); if (!File.Exists(file)) throw new FileNotFoundException("缺少完整导出文件", file);
                PlotExcelFile.Read(file, null); // 完整读取，拒绝公式及无效OpenXML输入。
            }
            var backup = Path.Combine(Path.GetFullPath(backupRoot), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup); Directory.CreateDirectory(to);
            var existing = FileNames.Where(n => File.Exists(Path.Combine(to, n))).ToHashSet(StringComparer.Ordinal);
            foreach (var name in existing) File.Copy(Path.Combine(to, name), Path.Combine(backup, name));
            var copied = new List<string>();
            try
            {
                foreach (var name in FileNames)
                { copied.Add(name); File.Copy(Path.Combine(from, name), Path.Combine(to, name), true); if (Hash(Path.Combine(from, name)) != Hash(Path.Combine(to, name))) throw new IOException("交接哈希不一致: " + name); }
                File.WriteAllText(Path.Combine(backup, "handoff.txt"), "6份工作簿已复制并验证SHA256。未执行Luban编译。\n" + from + "\n" + to);
                return backup;
            }
            catch
            {
                foreach (var name in copied) if (existing.Contains(name)) File.Copy(Path.Combine(backup, name), Path.Combine(to, name), true); else if (File.Exists(Path.Combine(to, name))) File.Delete(Path.Combine(to, name));
                throw;
            }
        }
    }

    /// <summary>全量解析后可审阅的导入计划。未提交前不改资产；提交只合并表中节点，不删除缺席节点。</summary>
    public sealed class PlotExcelImportPlan : IDisposable
    {
        private readonly PlotAuthoringProject _project;
        private readonly Dictionary<UnityEngine.Object, string> _baselines = new Dictionary<UnityEngine.Object, string>();
        private readonly Dictionary<string, string> _inputs = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _assetFiles = new Dictionary<string, string>();
        private readonly List<ScriptableObject> _temporary = new List<ScriptableObject>();
        private readonly List<Change> _changes = new List<Change>();
        private readonly List<PlotAtomRowData> _newNodes = new List<PlotAtomRowData>();
        private readonly string _projectBaseline;
        private readonly string _nodeInventory;
        private bool _disposed, _committed;
        private sealed class Change { public PlotAuthoringAsset Target, After, Before; }
        public int ImportedNodes { get; private set; }
        public int UpdatedNodes { get; private set; }
        public int CreatedNodes => _newNodes.Count;
        public int RetainedNodes { get; private set; }
        public string Summary => "节点：更新" + UpdatedNodes + "，新增" + CreatedNodes + "，保留表外节点" + RetainedNodes + "；枚举、Bean、表头、两类文本与演员表整体更新。";
        public string LastBackupDirectory { get; private set; }

        internal PlotExcelImportPlan(PlotAuthoringProject project, string folder)
        {
            PlotExcelExchange.RequireProject(project); _project = project; _projectBaseline = EditorJsonUtility.ToJson(project);
            try
            {
                PlotExcelSheet Read(string name, string sheet = "Sheet1")
                { var path = Path.GetFullPath(Path.Combine(folder, name)); var value = PlotExcelFile.Read(path, sheet); _inputs.Add(path, PlotExcelExchange.Hash(path)); return value; }
                var enums = Clone(project.header.parameterEnums); PlotAuthoringExcel.ReadEnums(Read(PlotExcelExchange.EnumFile), enums);
                var beans = Clone(project.header.parameterBean); PlotAuthoringExcel.ReadBeans(Read(PlotExcelExchange.BeanFile), beans, enums);
                var header = Clone(project.header); var nodesSheet = Read(PlotExcelExchange.NodeFile); PlotAuthoringExcel.ReadHeader(nodesSheet, header, enums, beans);
                var dialogs = Clone(project.dialogs); PlotAuthoringExcel.ReadTexts(Read(PlotExcelExchange.DialogFile), dialogs);
                var constants = Clone(project.constants); PlotAuthoringExcel.ReadTexts(Read(PlotExcelExchange.ConstantFile), constants);
                var actors = Clone(project.actors); PlotAuthoringExcel.ReadTable(Read(PlotExcelExchange.ActorFile, project.actors.sheetName), actors);
                AddChange(project.header.parameterEnums, enums); AddChange(project.header.parameterBean, beans); AddChange(project.header, header);
                AddChange(project.dialogs, dialogs); AddChange(project.constants, constants); AddChange(project.actors, actors);
                var imported = PlotAuthoringExcel.ReadNodes(nodesSheet, header, project.idColumn); _temporary.AddRange(imported); ImportedNodes = imported.Count;
                var existing = new PlotEditorManager(project).ListRows().ToDictionary(n => n.GetColumnSingleValue(project.idColumn), StringComparer.Ordinal);
                _nodeInventory = Inventory(existing.Values);
                foreach (var node in imported)
                {
                    var id = node.GetColumnSingleValue(project.idColumn);
                    if (existing.TryGetValue(id, out var target))
                    {
                        node.rowId = target.rowId; node.rowTitle = target.rowTitle;
                        // Excel空单元格无法表达“缺省列表”与“显式空列表”的区别；更新已有资产时保留其容器与列顺序。
                        // 原有列表被清空时也保留空容器，不保留任何旧列表项。
                        foreach(var column in target.columns)
                            if(node.GetColumn(column.columnName)==null&&header.GetColumnByName(column.columnName)?.IsList==true)
                                node.columns.Add(new PlotAtomRowData.SerializedColumnData{columnName=column.columnName,rows=new List<PlotAtomRowData.StringList>()});
                        var importedColumns=node.columns.ToDictionary(c=>c.columnName,StringComparer.Ordinal);
                        var ordered=target.columns.Where(c=>importedColumns.ContainsKey(c.columnName)).Select(c=>importedColumns[c.columnName]).ToList();
                        ordered.AddRange(node.columns.Where(c=>!ordered.Contains(c)));node.columns=ordered;
                        AddChange(target, node); UpdatedNodes++;
                    }
                    else _newNodes.Add(node);
                }
                // 新表头也必须兼容保留下来的节点，避免只检验被导入的半份数据。
                var importedIds = imported.Select(n => n.GetColumnSingleValue(project.idColumn)).ToHashSet(StringComparer.Ordinal);
                foreach (var node in existing.Values)
                {
                    Baseline(node);
                    if (importedIds.Contains(node.GetColumnSingleValue(project.idColumn))) continue;
                    var check = Clone(node); check.headerDefinition = header; check.Validate().ThrowIfInvalid();
                    foreach (var column in header.sheets) if (!column.IsList && !check.HasColumn(column.columnName)) throw new FormatException("保留节点缺少新表头中的标量列: " + node.name + "/" + column.columnName);
                    RetainedNodes++;
                }
            }
            catch { Dispose(); throw; }
        }
        private T Clone<T>(T value) where T : ScriptableObject
        { var clone = UnityEngine.Object.Instantiate(value); clone.hideFlags = HideFlags.HideAndDontSave; _temporary.Add(clone); return clone; }
        private void Baseline(UnityEngine.Object value)
        {
            if (_baselines.ContainsKey(value)) return;
            _baselines.Add(value, EditorJsonUtility.ToJson(value));
            var path = AssetDatabase.GetAssetPath(value); if (path != "") _assetFiles[path] = PlotExcelExchange.Hash(path);
        }
        private void AddChange(PlotAuthoringAsset target, PlotAuthoringAsset after)
        { Baseline(target); _changes.Add(new Change { Target = target, After = after, Before = Clone(target) }); }
        private void EnsureAvailable()
        { if (_disposed) throw new ObjectDisposedException(nameof(PlotExcelImportPlan)); if (_committed) throw new InvalidOperationException("本计划已经提交"); }
        private string NewPath(PlotAtomRowData row) => _project.nodeFolder.TrimEnd('/', '\\') + "/" + row.GetColumnSingleValue(_project.idColumn) + ".asset";
        private static string Inventory(System.Collections.Generic.IEnumerable<PlotAtomRowData> rows) => string.Join("\n", rows.Select(AssetDatabase.GetAssetPath).OrderBy(p => p, StringComparer.Ordinal));
        public void Commit(string backupRoot)
        {
            EnsureAvailable();
            if (EditorJsonUtility.ToJson(_project) != _projectBaseline) throw new InvalidOperationException("工作区配置已改变，请重新检查导入");
            foreach (var baseline in _baselines) if (baseline.Key == null || EditorJsonUtility.ToJson(baseline.Key) != baseline.Value) throw new InvalidOperationException("源资产已改变，请重新检查导入");
            foreach (var input in _inputs) if (!File.Exists(input.Key) || PlotExcelExchange.Hash(input.Key) != input.Value) throw new InvalidOperationException("工作簿已改变，请重新检查导入");
            foreach (var asset in _assetFiles) if (!File.Exists(asset.Key) || PlotExcelExchange.Hash(asset.Key) != asset.Value) throw new InvalidOperationException("源资产文件已改变，请重新检查导入");
            if (Inventory(new PlotEditorManager(_project).ListRows()) != _nodeInventory) throw new InvalidOperationException("工作区节点集合已改变，请重新检查导入");
            foreach (var row in _newNodes) if (File.Exists(NewPath(row))) throw new IOException("新增节点路径已被占用: " + NewPath(row));
            LastBackupDirectory = Path.Combine(Path.GetFullPath(backupRoot), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(LastBackupDirectory);
            foreach (var change in _changes)
            {
                var path = AssetDatabase.GetAssetPath(change.Target);
                if (path == "") throw new InvalidOperationException("导入目标必须为持久化资产");
                var backup = Path.Combine(LastBackupDirectory, AssetDatabase.AssetPathToGUID(path) + ".asset");
                File.Copy(path, backup); File.WriteAllText(backup + ".source.txt", path);
            }
            var created = new List<string>(); var touched = new List<Change>();
            try
            {
                Directory.CreateDirectory(_project.nodeFolder); AssetDatabase.Refresh();
                Undo.IncrementCurrentGroup(); var group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("PlotWeave Excel导入");
                foreach (var change in _changes)
                { Undo.RecordObject(change.Target, "PlotWeave Excel导入"); touched.Add(change); CopyFields(change.After, change.Target); EditorUtility.SetDirty(change.Target); }
                foreach (var row in _newNodes)
                {
                    var asset = UnityEngine.Object.Instantiate(row); asset.hideFlags = HideFlags.None; asset.name = row.GetColumnSingleValue(_project.idColumn); asset.headerDefinition = _project.header;
                    var path = NewPath(row); created.Add(path); AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "PlotWeave Excel导入");
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
                Undo.FlushUndoRecordObjects(); foreach (var change in _changes) AssetDatabase.SaveAssetIfDirty(change.Target);
                Undo.CollapseUndoOperations(group);
                File.WriteAllText(Path.Combine(LastBackupDirectory, "import.txt"), Summary + "\n节点采用合并策略，不删除表外节点。\n" + string.Join("\n", _inputs.Select(p => p.Key + " SHA256=" + p.Value)));
                _committed = true;
            }
            catch
            {
                foreach (var path in created) AssetDatabase.DeleteAsset(path);
                foreach (var change in touched) { CopyFields(change.Before, change.Target); EditorUtility.SetDirty(change.Target); Undo.ClearUndo(change.Target); AssetDatabase.SaveAssetIfDirty(change.Target); }
                throw;
            }
        }
        private static void CopyFields(PlotAuthoringAsset from, PlotAuthoringAsset to)
        {
            if (from is PlotEnumAsset enums) ((PlotEnumAsset)to).dynamicLists = enums.dynamicLists;
            else if (from is PlotEventTypeAsset beans) ((PlotEventTypeAsset)to).eventTypes = beans.eventTypes;
            else if (from is PlotAtomDataHeaderAsset header) ((PlotAtomDataHeaderAsset)to).sheets = header.sheets;
            else if (from is PlotDialogKeyAsset dialogs) ((PlotDialogKeyAsset)to).dialogKeyDataList = dialogs.dialogKeyDataList;
            else if (from is PlotConstantKeyAsset constants) ((PlotConstantKeyAsset)to).constantKeyDataList = constants.constantKeyDataList;
            else if (from is PlotTableAsset table) { var target = (PlotTableAsset)to; target.headers = table.headers; target.rows = table.rows; target.sheetName = table.sheetName; }
            else if (from is PlotAtomRowData row) { var target = (PlotAtomRowData)to; target.columns = row.columns; target.rowId = row.rowId; target.rowTitle = row.rowTitle; }
            else throw new NotSupportedException("未知的导入资产类型");
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var value in _temporary) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _temporary.Clear();
        }
    }
}
