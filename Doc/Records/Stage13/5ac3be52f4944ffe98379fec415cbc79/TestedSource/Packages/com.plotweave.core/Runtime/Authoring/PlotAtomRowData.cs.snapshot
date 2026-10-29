using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/数据/节点行")]
    public sealed class PlotAtomRowData : PlotAuthoringAsset
    {
        [Serializable] public sealed class StringList { public List<string> values = new List<string>(); }
        [Serializable] public sealed class SerializedColumnData
        {
            public string columnName = "";
            public List<StringList> rows = new List<StringList>();
        }
        public PlotAtomDataHeaderAsset headerDefinition;
        public string rowId = Guid.NewGuid().ToString();
        public string rowTitle = "";
        public List<SerializedColumnData> columns = new List<SerializedColumnData>();
        public SerializedColumnData GetColumn(string columnName) => columns?.Find(c => c != null && c.columnName == columnName);
        public bool HasColumn(string columnName) => GetColumn(columnName) != null;
        public List<StringList> GetColumnRows(string columnName) => GetColumn(columnName)?.rows ?? new List<StringList>();
        public List<string> GetColumnFirstRowValues(string columnName) => GetColumnRows(columnName).FirstOrDefault()?.values ?? new List<string>();
        public string GetColumnSingleValue(string columnName) => GetColumnFirstRowValues(columnName).FirstOrDefault() ?? "";
        private SerializedColumnData EnsureColumn(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("列名不能为空", nameof(name));
            if (columns == null) columns = new List<SerializedColumnData>();
            var column = GetColumn(name);
            if (column == null) { column = new SerializedColumnData { columnName = name }; columns.Add(column); }
            if (column.rows == null) column.rows = new List<StringList>();
            return column;
        }
        public void SetColumnRows(string columnName, List<StringList> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            EnsureColumn(columnName).rows = rows;
        }
        public void SetColumnRowValues(string columnName, List<string> rowValues, int rowIndex = 0)
        {
            if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            if (rowValues == null) throw new ArgumentNullException(nameof(rowValues));
            var column = EnsureColumn(columnName);
            while (column.rows.Count <= rowIndex) column.rows.Add(new StringList());
            if (column.rows[rowIndex] == null) column.rows[rowIndex] = new StringList();
            column.rows[rowIndex].values = rowValues;
        }
        public void SetColumnSingleValue(string columnName, string value)
        {
            var column = EnsureColumn(columnName);
            if (column.rows.Count == 0) column.rows.Add(new StringList());
            if (column.rows[0] == null) column.rows[0] = new StringList();
            if (column.rows[0].values == null) column.rows[0].values = new List<string>();
            if (column.rows[0].values.Count == 0) column.rows[0].values.Add(value ?? "");
            else column.rows[0].values[0] = value ?? "";
        }
        public void AddRowToColumn(string columnName, List<string> rowValues)
        {
            if (rowValues == null) throw new ArgumentNullException(nameof(rowValues));
            EnsureColumn(columnName).rows.Add(new StringList { values = rowValues });
        }
        public void RemoveRowFromColumn(string columnName, int rowIndex)
        {
            var rows = GetColumn(columnName)?.rows;
            if (rows != null && rowIndex >= 0 && rowIndex < rows.Count) rows.RemoveAt(rowIndex);
        }
        public int GetColumnRowCount(string columnName) => GetColumn(columnName)?.rows?.Count ?? 0;
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult();
            result.Require(!string.IsNullOrWhiteSpace(rowId), "行资产rowId不能为空");
            if (headerDefinition == null) { result.Errors.Add("缺少表头引用"); return result; }
            result.Include("表头", headerDefinition.Validate());
            if (columns == null) { result.Errors.Add("行列列表为空引用"); return result; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in columns)
            {
                if (column == null) { result.Errors.Add("行列为空引用"); continue; }
                result.Unique(names, column.columnName, "行列名");
                var definition = headerDefinition.GetColumnByName(column.columnName);
                if (definition == null) { result.Errors.Add("行包含未定义列: " + column.columnName); continue; }
                if (column.rows == null) { result.Errors.Add(column.columnName + "行列表为空引用"); continue; }
                result.Require(definition.IsList || column.rows.Count <= 1, column.columnName + "非列表列包含多行");
                var bean = definition.valueType == ValueType.CustomBean ? headerDefinition.parameterBean?.GetEventTypeByKey(definition.typeKey) : null;
                List<PlotBeanLeaf> leaves = null;
                if (bean != null) try { leaves = PlotBeanLayout.Leaves(headerDefinition.parameterBean, definition.typeKey); } catch (Exception error) { result.Errors.Add(error.Message); continue; }
                var memberCount = leaves?.Count ?? definition.members;
                for (var rowIndex = 0; rowIndex < column.rows.Count; rowIndex++)
                {
                    var values = column.rows[rowIndex]?.values;
                    var location = column.columnName + "[" + rowIndex + "]";
                    if (values == null) { result.Errors.Add(location + "成员值列表为空引用"); continue; }
                    if (values.Count != memberCount) { result.Errors.Add(location + "成员数错误，期望" + memberCount + "，实际" + values.Count); continue; }
                    for (var i = 0; i < values.Count; i++)
                    {
                        var p = leaves?[i].Parameter;
                        var type = p?.valueType ?? definition.valueType;
                        var enumName = p?.enumType ?? definition.typeKey;
                        var enums = bean == null ? headerDefinition.parameterEnums : headerDefinition.parameterBean?.parameterEnums;
                        result.Require(AuthoringValueParser.IsValid(values[i], type, enums, enumName), location + "." + (p?.paramKey ?? "value") + "值与类型不符: " + values[i]);
                    }
                }
            }
            return result;
        }
    }

    public static class AuthoringValueParser
    {
        public static bool IsValid(string text, ValueType type, PlotEnumAsset enums = null, string enumName = "")
        {
            switch (type)
            {
                case ValueType.String: return true;
                case ValueType.Int: return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case ValueType.Float: return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && !float.IsNaN(number) && !float.IsInfinity(number);
                case ValueType.Bool: return bool.TryParse(text, out _) || text == "0" || text == "1";
                case ValueType.CustomEnum: return enums?.GetEnumKeys(enumName).Contains(text) == true;
                case ValueType.Vector3:
                    var parts = (text ?? "").Trim().Trim('(', ')').Split(',');
                    return parts.Length == 3 && parts.All(p => IsValid(p, ValueType.Float));
                default: return false;
            }
        }
    }
}
