using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using PlotWeave.Data;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotPlaybackCompiler
    {
        /// <summary>制作数据编译为不依赖编辑资产类型和AssetDatabase的播放快照。</summary>
        public static PlotPlaybackDocument Compile(PlotAuthoringProject project)
        {
            ValidateInputs(project, project == null ? null : project.dialogs, project == null ? null : project.constants);
            return CompileCore(project, new PlotEditorManager(project).ListRows(), project.dialogs, project.constants);
        }

        /// <summary>用当前输入构造临时播放快照；不保存节点或文本表，其他节点仍取自工作区。</summary>
        public static PlotPlaybackDocument Compile(PlotEditingSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var project = session.Store.Project;
            var current = session.CurrentAsset;
            var working = session.WorkingRow;
            if (current == null || working == null) throw new InvalidOperationException("请先选择要预览的节点；编辑事务可能已关闭");
            ValidateInputs(project, session.Dialogs, session.Constants);
            if (project.header == null || current.headerDefinition != project.header || working.headerDefinition != project.header)
                throw new InvalidOperationException("预览节点与工作区表头不一致");
            if (string.IsNullOrWhiteSpace(current.rowId) || working.rowId != current.rowId)
                throw new InvalidOperationException("预览工作副本的rowId已改变，请通过节点重命名操作修改身份");
            var id = ReadIdentity(current, project.idColumn);
            var scenesId = ReadIdentity(current, "ScenesID");
            if (ReadIdentity(working, project.idColumn) != id || ReadIdentity(working, "ScenesID") != scenesId)
                throw new InvalidOperationException("预览工作副本的节点ID已改变，请通过节点重命名操作修改身份");

            var storedRows = session.Store.ListRows();
            if (storedRows == null) throw new InvalidOperationException("工作区没有返回节点列表");
            var rows = new List<PlotAtomRowData>(storedRows.Count);
            var replaced = false;
            foreach (var row in storedRows)
            {
                if (row == null) throw new InvalidOperationException("工作区包含空节点");
                var sameRow = row.rowId == current.rowId;
                var sameId = ReadIdentity(row, project.idColumn) == id;
                if (sameRow || sameId)
                {
                    if (!sameRow || !sameId || replaced || row.headerDefinition != project.header || ReadIdentity(row, "ScenesID") != scenesId)
                        throw new InvalidOperationException("当前节点在工作区中的身份已变化或重复，请重新选择节点");
                    rows.Add(working); replaced = true;
                }
                else rows.Add(row);
            }
            if (!replaced) throw new InvalidOperationException("当前节点已不在工作区中，请重新选择节点");
            return CompileCore(project, rows, session.Dialogs, session.Constants);
        }

        private static int ReadIdentity(PlotAtomRowData row, string column)
        {
            if (string.IsNullOrWhiteSpace(column) || !int.TryParse(row.GetColumnSingleValue(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new InvalidOperationException("预览节点ID须为正整数: " + column);
            return id;
        }

        private static void ValidateInputs(PlotAuthoringProject project, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
        {
            if (project == null || dialogs == null || constants == null || project.actors == null) throw new ArgumentException("工作区播放数据引用不完整");
            project.actors.Validate().ThrowIfInvalid(); dialogs.Validate().ThrowIfInvalid(); constants.Validate().ThrowIfInvalid();
        }

        private static PlotPlaybackDocument CompileCore(PlotAuthoringProject project, IEnumerable<PlotAtomRowData> rows, PlotDialogKeyAsset dialogs, PlotConstantKeyAsset constants)
        {
            var document = new PlotPlaybackDocument();
            foreach (var row in rows)
            {
                document.Nodes.Add(PlotPlaybackNode.Read(AuthoringPlotValueSource.FromRow(row), project.legacyEmptyActorSlots));
            }
            if (project.eventTracks != null)
            {
                project.eventTracks.Validate().ThrowIfInvalid();
                foreach (var track in project.eventTracks.tracks) { var node = document.Nodes.Find(n => n.ScenesID == track.nodeID); if (node == null) throw new FormatException("事件轨道指向不存在的节点: " + track.nodeID); node.Events = track.events.Select(e => e.Clone()).ToList(); }
            }
            foreach (var actor in project.actors.rows)
            {
                if (!int.TryParse(actor.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0) throw new FormatException("演员表行ID须为正整数: " + actor.id);
                document.Actors.Add(new PlotPlaybackActor { ID = id, Name = project.actors.GetColumnValue(actor.id, "Name"), Thespian = project.actors.GetColumnValue(actor.id, "Thespian") });
            }
            foreach (var entry in dialogs.dialogKeyDataList) document.Dialogs.Add(new PlotPlaybackText { Key = entry.key, Languages = new List<string>(entry.value) });
            foreach (var entry in constants.constantKeyDataList) document.Constants.Add(new PlotPlaybackText { Key = entry.key, Languages = new List<string>(entry.value) });
            document.Validate(); return document;
        }
        [MenuItem("Tools/PlotWeave/制作工具/编译所选工作区播放数据")]
        public static void CompileSelected()
        {
            var project = Selection.activeObject as PlotAuthoringProject;
            if (project == null) throw new InvalidOperationException("请先选择制作工作区资产");
            var data = Compile(project); var path = EditorUtility.SaveFilePanelInProject("保存独立播放数据", "CompiledPlot", "asset", "生成新播放资产");
            if (path == "") return; if (File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".json"))) throw new IOException("请选择asset和json均尚未存在的文件名，避免覆盖手工配置");
            var asset = ScriptableObject.CreateInstance<PlotPlaybackAsset>(); asset.data = data; AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
            File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(data, true)); AssetDatabase.Refresh(); Selection.activeObject = asset;
        }
    }
}
