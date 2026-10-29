using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Authoring.UI;
using UnityEditor;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Authoring
{
    internal sealed class PlotAuthoringInspectorControls
    {
        private string _search = "";
        private string _newKey = "";
        private int _language;
        private int _page;
        private const int PageSize = 20;
        private bool _raw;
        private static string Value(string label, string value, ValueType type, PlotEnumAsset enums, string enumName)
        {
            if (type == ValueType.CustomEnum)
            {
                var keys = enums?.GetEnumKeys(enumName) ?? Array.Empty<string>();
                var display = keys.Select(k => { var name = enums.GetDisplayName(enumName, k); return string.IsNullOrEmpty(name) ? k : name; }).ToArray();
                var selected = Array.IndexOf(keys, value);
                if (selected < 0) { keys = new[] { value }.Concat(keys).ToArray(); display = new[] { "[未绑定] " + value }.Concat(display).ToArray(); selected = 0; }
                var next = EditorGUILayout.Popup(label, selected, display); return next == selected ? value : keys[next];
            }
            return EditorGUILayout.TextField(label + " (" + type + ")", value ?? "");
        }
        public bool Draw(PlotAuthoringAsset asset, UnityEditor.Editor editor)
        {
            if (asset is PlotAtomRowData invalidRow && (invalidRow.columns == null || (invalidRow.headerDefinition != null && invalidRow.headerDefinition.sheets == null))) return false;
            if (asset is PlotDialogKeyAsset invalidDialog && invalidDialog.dialogKeyDataList == null) return false;
            if (asset is PlotConstantKeyAsset invalidConstant && invalidConstant.constantKeyDataList == null) return false;
            if (asset is PlotTableAsset invalidTable && (invalidTable.headers == null || invalidTable.rows == null)) return false;
            if (asset is PlotAtomRowData row) DrawRow(row, editor);
            else if (asset is PlotDialogKeyAsset dialog) DrawTexts(dialog.dialogKeyDataList, dialog, () => dialog.AddDialogKey(_newKey, Math.Max(1, dialog.GetDefaultLanguageCount() == 0 ? 5 : dialog.GetDefaultLanguageCount())));
            else if (asset is PlotConstantKeyAsset constant) DrawTexts(constant.constantKeyDataList, constant, () => constant.AddConstantKey(_newKey, Math.Max(1, constant.GetDefaultLanguageCount() == 0 ? 5 : constant.GetDefaultLanguageCount())));
            else if (asset is PlotTableAsset table) DrawTable(table, editor);
            else return false;
            return true;
        }
        private void DrawRow(PlotAtomRowData asset, UnityEditor.Editor editor)
        {
            asset.headerDefinition = (PlotAtomDataHeaderAsset)EditorGUILayout.ObjectField("节点表头", asset.headerDefinition, typeof(PlotAtomDataHeaderAsset), false);
            asset.rowTitle = EditorGUILayout.TextField("行标题", asset.rowTitle);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("行资产标识", asset.rowId);
            var header = asset.headerDefinition;
            if (header == null) { EditorGUILayout.HelpBox("先指定表头，再编辑列数据。", MessageType.Info); return; }
            foreach (var column in header.sheets)
            {
                if (column == null) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.LabelField(column.description + "  [" + column.columnName + "]", EditorStyles.boldLabel);
                var rows = asset.GetColumnRows(column.columnName);
                if (!column.IsList && rows.Count == 0 && GUILayout.Button("初始化此列"))
                { asset.SetColumnRowValues(column.columnName, PlotEditingSession.DefaultValues(column, header)); GUI.changed = true; rows = asset.GetColumnRows(column.columnName); }
                for (var index = 0; index < rows.Count; index++)
                {
                    var values = rows[index]?.values;
                    if (values == null) { EditorGUILayout.HelpBox("此行结构无效，可在原始数据中修复。", MessageType.Error); continue; }
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (column.IsList)
                    {
                        EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("第 " + (index + 1) + " 项");
                        var remove = GUILayout.Button("移除", GUILayout.Width(60)); EditorGUILayout.EndHorizontal();
                        if (remove) { asset.RemoveRowFromColumn(column.columnName, index); GUI.changed = true; EditorGUILayout.EndVertical(); break; }
                    }
                    var bean = column.valueType == ValueType.CustomBean ? header.parameterBean?.GetEventTypeByKey(column.typeKey) : null;
                    var leaves = bean == null ? null : PlotBeanLayout.Leaves(header.parameterBean, column.typeKey);
                    for (var member = 0; member < values.Count; member++)
                    {
                        var p = leaves != null && member < leaves.Count ? leaves[member].Parameter : null;
                        if (bean != null && p == null) { values[member] = EditorGUILayout.TextField("未绑定成员 " + member, values[member]); continue; }
                        values[member] = Value(leaves != null && member < leaves.Count ? leaves[member].Label : p?.displayName ?? "值", values[member], p?.valueType ?? column.valueType, bean == null ? header.parameterEnums : header.parameterBean.parameterEnums, p?.enumType ?? column.typeKey);
                    }
                    EditorGUILayout.EndVertical();
                }
                if (column.IsList && GUILayout.Button("添加列表项")) { asset.AddRowToColumn(column.columnName, PlotEditingSession.DefaultValues(column, header)); GUI.changed = true; }
                EditorGUILayout.EndVertical();
            }
            _raw = EditorGUILayout.Foldout(_raw, "原始序列化数据（包含未绑定列）");
            if (_raw) { editor.serializedObject.Update(); EditorGUILayout.PropertyField(editor.serializedObject.FindProperty("columns"), true); editor.serializedObject.ApplyModifiedProperties(); }
        }
        private int Start(int count)
        {
            var pages = Math.Max(1, (count + PageSize - 1) / PageSize); _page = Mathf.Clamp(_page, 0, pages - 1);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("上一页")) _page = Math.Max(0, _page - 1);
            EditorGUILayout.LabelField((_page + 1) + " / " + pages, GUILayout.Width(70));
            if (GUILayout.Button("下一页")) _page = Math.Min(pages - 1, _page + 1);
            EditorGUILayout.EndHorizontal(); return _page * PageSize;
        }
        private void DrawTexts<T>(List<T> entries, PlotTextKeyAsset asset, Action add) where T : PlotTextEntry
        {
            _search = EditorGUILayout.TextField("搜索KEY或当前语言内容", _search);
            _language = (int)(PlotDialogLanguageType)EditorGUILayout.EnumPopup("语言", (PlotDialogLanguageType)_language);
            _newKey = EditorGUILayout.TextField("新KEY", _newKey);
            if (GUILayout.Button("添加KEY")) { add(); GUI.changed = true; }
            var selected = entries.Where(e => e != null && (string.IsNullOrEmpty(_search) || (e.key ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || (e.value != null && e.value.Count > _language && (e.value[_language] ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
            var start = Start(selected.Count);
            foreach (var entry in selected.Skip(start).Take(PageSize))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.key, EditorStyles.boldLabel);
                var remove = GUILayout.Button("移除KEY", GUILayout.Width(85)); EditorGUILayout.EndHorizontal();
                if (remove) { entries.Remove(entry); GUI.changed = true; EditorGUILayout.EndVertical(); break; }
                if (entry.value != null && _language < entry.value.Count) entry.value[_language] = EditorGUILayout.TextArea(entry.value[_language] ?? "", GUILayout.MinHeight(52));
                else EditorGUILayout.HelpBox("该KEY缺少此语言列，请检查原始数据。", MessageType.Warning);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.LabelField("KEY数：" + entries.Count + "；当前筛选：" + selected.Count);
        }
        private void DrawTable(PlotTableAsset asset, UnityEditor.Editor editor)
        {
            editor.serializedObject.Update();
            foreach (var name in new[] { "tableName", "sheetName", "customDataName", "customLubanName", "tableDescription", "headers" }) EditorGUILayout.PropertyField(editor.serializedObject.FindProperty(name), true);
            editor.serializedObject.ApplyModifiedProperties();
            _search = EditorGUILayout.TextField("筛选行ID", _search);
            if (GUILayout.Button("添加行")) { asset.rows.Add(new PlotTableAsset.TableRow()); GUI.changed = true; }
            var rows = asset.rows.Where(r => r != null && (string.IsNullOrEmpty(_search) || (r.id ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var start = Start(rows.Count);
            foreach (var row in rows.Skip(start).Take(PageSize))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox); row.id = EditorGUILayout.TextField("ID", row.id);
                if (row.columns == null)
                {
                    EditorGUILayout.HelpBox("此行成员列表为空引用，可在原始数据中修复。", MessageType.Error);
                    EditorGUILayout.EndVertical(); continue;
                }
                foreach (var header in asset.headers)
                {
                    if (header == null) continue;
                    var cell = row.columns.Find(c => c != null && c.columnName == header.columnName);
                    EditorGUI.BeginChangeCheck(); var value = EditorGUILayout.TextField(header.description + " [" + header.columnName + "]", cell?.value ?? "");
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (cell == null) { cell = new PlotTableAsset.ColumnValue { columnName = header.columnName }; row.columns.Add(cell); }
                        cell.value = value;
                    }
                }
                if (GUILayout.Button("移除此行")) { asset.rows.Remove(row); GUI.changed = true; EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.EndVertical();
            }
            _raw = EditorGUILayout.Foldout(_raw, "原始行数据（包含未绑定列）");
            if (_raw) { editor.serializedObject.Update(); EditorGUILayout.PropertyField(editor.serializedObject.FindProperty("rows"), true); editor.serializedObject.ApplyModifiedProperties(); }
        }
    }
}
