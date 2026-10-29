using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlotWeave.Authoring.UI;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring
{
    /// <summary>每个制作工作区独立的资产存取服务，不再通过全局路径和单例串工作区。</summary>
    public sealed class PlotEditorManager : IPlotAuthoringStore
    {
        public PlotAuthoringProject Project { get; }
        private string NodeFolder
        {
            get
            {
                var folder = Path.GetFullPath(Project.nodeFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!folder.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("节点目录必须位于本项目Assets子目录");
                return folder;
            }
        }
        private string Relative(string full) => "Assets/" + Path.GetFullPath(full).Substring(Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
        public PlotEditorManager(PlotAuthoringProject project)
        {
            Project = project != null ? project : throw new ArgumentNullException(nameof(project));
            if (project.header == null) throw new ArgumentException("工作区缺少节点表头");
            project.header.Validate().ThrowIfInvalid();
            var id = project.header.GetColumnByName(project.idColumn);
            if (id == null || id.IsList || id.valueType != PlotEventTypeAsset.ValueType.Int) throw new ArgumentException("节点ID列必须是标量Int");
            _ = NodeFolder;
        }
        public List<PlotAtomRowData> ListRows(int? from = null, int? to = null)
        {
            if (from > to) throw new ArgumentException("起始ID不能大于结束ID");
            var list = new List<PlotAtomRowData>();
            if (!Directory.Exists(NodeFolder)) return list;
            var ids = new HashSet<int>();
            foreach (var path in Directory.GetFiles(NodeFolder, "*.asset", SearchOption.AllDirectories))
            {
                var row = AssetDatabase.LoadAssetAtPath<PlotAtomRowData>(Relative(path));
                if (row == null || row.headerDefinition != Project.header) continue;
                var id = ReadId(row);
                if (!ids.Add(id)) throw new FormatException("工作区存在重复节点ID: " + id);
                if ((!from.HasValue || id >= from.Value) && (!to.HasValue || id <= to.Value)) list.Add(row);
            }
            return list.OrderBy(ReadId).ToList();
        }
        private int ReadId(PlotAtomRowData row)
        {
            if (!int.TryParse(row.GetColumnSingleValue(Project.idColumn), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new FormatException("节点ID必须为正整数: " + row.name);
            return id;
        }
        private static void Id(int id) { if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), "节点ID必须大于0；0保留为结束值"); }
        public PlotAtomRowData FindRow(int id) => ListRows().Find(row => ReadId(row) == id);
        public PlotAtomRowData CreateRow(int id)
        {
            Id(id);
            if (FindRow(id) != null) throw new InvalidOperationException("节点ID已存在: " + id);
            var path = Relative(Path.Combine(NodeFolder, id.ToString(CultureInfo.InvariantCulture) + ".asset"));
            if (File.Exists(path)) throw new IOException("同名资产已存在: " + path);
            Directory.CreateDirectory(NodeFolder); AssetDatabase.Refresh();
            var row = ScriptableObject.CreateInstance<PlotAtomRowData>(); row.headerDefinition = Project.header;
            row.rowTitle = id.ToString(CultureInfo.InvariantCulture);
            foreach (var column in Project.header.sheets)
                if (!column.IsList) row.SetColumnRowValues(column.columnName, PlotEditingSession.DefaultValues(column, Project.header));
            row.SetColumnSingleValue(Project.idColumn, row.rowTitle);
            row.Validate().ThrowIfInvalid();
            AssetDatabase.CreateAsset(row, path); AssetDatabase.SaveAssetIfDirty(row);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<PlotAtomRowData>(path);
        }
        private void RequireOwned(PlotAtomRowData row)
        {
            var path = AssetDatabase.GetAssetPath(row);
            if (string.IsNullOrEmpty(path) || !Path.GetFullPath(path).StartsWith(NodeFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || row.headerDefinition != Project.header)
                throw new InvalidOperationException("拒绝修改当前工作区之外的节点");
            if (AssetDatabase.LoadAssetAtPath<PlotAtomRowData>(path) != row) throw new InvalidOperationException("节点资产实例已重载，请重新选择后编辑");
        }
        public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
            => SaveChanges(target, working, dialogs, constants, true);
        public void SaveChanges(PlotAtomRowData target, PlotAtomRowData working, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants, bool recordUndo)
        {
            RequireOwned(target);
            working.Validate().ThrowIfInvalid(); dialogs?.Validate().ThrowIfInvalid(); constants?.Validate().ThrowIfInvalid();
            if (working.headerDefinition != Project.header || ReadId(working) != ReadId(target) || working.rowId != target.rowId) throw new InvalidOperationException("更换表头、修改ID或行标识必须使用专门操作");
            var objects = new List<UnityEngine.Object> { target };
            if (Project.dialogs != null && dialogs != null) objects.Add(Project.dialogs);
            if (Project.constants != null && constants != null) objects.Add(Project.constants);
            if (recordUndo) Undo.RecordObjects(objects.ToArray(), "保存剧情编辑");
            var copiedColumns = working.columns.Select(c => new PlotAtomRowData.SerializedColumnData
            { columnName = c.columnName, rows = c.rows.Select(r => new PlotAtomRowData.StringList { values = new List<string>(r.values) }).ToList() }).ToList();
            target.rowTitle = working.rowTitle; target.columns = copiedColumns;
            if (Project.dialogs != null && dialogs != null) CopyTexts(dialogs, Project.dialogs);
            if (Project.constants != null && constants != null) CopyTexts(constants, Project.constants);
            foreach (var asset in objects) EditorUtility.SetDirty(asset);
            if (recordUndo) Undo.FlushUndoRecordObjects();
            foreach (var asset in objects) AssetDatabase.SaveAssetIfDirty(asset);
        }
        private static void CopyTexts(PlotTextKeyAsset source, PlotTextKeyAsset target)
        {
            if (source is PlotDialogKeyAsset dialogs && target is PlotDialogKeyAsset targetDialogs)
                targetDialogs.dialogKeyDataList = dialogs.dialogKeyDataList.Select(e => new PlotDialogKeyAsset.DialogKeyData { key = e.key, value = new List<string>(e.value) }).ToList();
            else if (source is PlotConstantKeyAsset constants && target is PlotConstantKeyAsset targetConstants)
                targetConstants.constantKeyDataList = constants.constantKeyDataList.Select(e => new PlotConstantKeyAsset.ConstantKeyData { key = e.key, value = new List<string>(e.value) }).ToList();
            else throw new ArgumentException("文本表类型不匹配");
        }
        public PlotAtomRowData RenameRow(int oldId, int newId)
        {
            Id(newId);
            var row = FindRow(oldId) ?? throw new KeyNotFoundException("节点不存在"); RequireOwned(row);
            if (oldId == newId) return row;
            if (FindRow(newId) != null) throw new InvalidOperationException("新ID已存在");
            var path = AssetDatabase.GetAssetPath(row);
            var destination = Path.Combine(Path.GetDirectoryName(path), newId.ToString(CultureInfo.InvariantCulture) + ".asset");
            if (File.Exists(destination)) throw new IOException("新文件名已被占用");
            var error = AssetDatabase.RenameAsset(path, newId.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            row.rowTitle = newId.ToString(CultureInfo.InvariantCulture); row.SetColumnSingleValue(Project.idColumn, row.rowTitle);
            EditorUtility.SetDirty(row); AssetDatabase.SaveAssetIfDirty(row); return row;
        }
        public bool DeleteRow(int id)
        {
            var row = FindRow(id); if (row == null) return false;
            RequireOwned(row); return AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(row));
        }
    }
}
