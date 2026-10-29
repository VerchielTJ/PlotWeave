using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/数据/通用表")]
    public sealed class PlotTableAsset : PlotAuthoringAsset
    {
        [Serializable] public sealed class ColumnHeader
        {
            public string columnName = "";
            public string columnType = "string";
            public string description = "";
            public int columnIndex;
        }
        [Serializable] public sealed class TableRow { public string id = ""; public List<ColumnValue> columns = new List<ColumnValue>(); }
        [Serializable] public sealed class ColumnValue { public string columnName = ""; public string value = ""; }
        public string tableName = "";
        public string sheetName = "Sheet1";
        public string customDataName = "";
        public string customLubanName = "";
        public string tableDescription = "";
        public List<ColumnHeader> headers = new List<ColumnHeader>();
        public List<TableRow> rows = new List<TableRow>();
        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
        public TableRow GetRowById(string id) => rows?.Find(r => r != null && r.id == id);
        public TableRow GetRowById(int id) => GetRowById(Id(id));
        public string GetColumnValue(string id, string name) => GetRowById(id)?.columns?.Find(c => c != null && c.columnName == name)?.value ?? "";
        public string GetColumnValue(int id, string name) => GetColumnValue(Id(id), name);
        public T GetColumnValue<T>(string id, string name, T defaultValue = default)
        {
            var value = GetColumnValue(id, name);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            try { return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException) { return defaultValue; }
        }
        public T GetColumnValue<T>(int id, string name, T defaultValue = default) => GetColumnValue(Id(id), name, defaultValue);
        private TableRow EnsureRow(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("ID不能为空", nameof(id));
            if (rows == null) rows = new List<TableRow>();
            var row = GetRowById(id);
            if (row == null) { row = new TableRow { id = id }; rows.Add(row); }
            if (row.columns == null) row.columns = new List<ColumnValue>();
            return row;
        }
        public void SetColumnValue(string id, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("列名不能为空", nameof(name));
            var row = EnsureRow(id);
            var cell = row.columns.Find(c => c != null && c.columnName == name);
            if (cell == null) { cell = new ColumnValue { columnName = name }; row.columns.Add(cell); }
            cell.value = value ?? "";
        }
        public void SetColumnValue(int id, string name, string value) => SetColumnValue(Id(id), name, value);
        public void SetRowData(string id, Dictionary<string, string> columnData)
        {
            if (columnData == null) throw new ArgumentNullException(nameof(columnData));
            var cells = columnData.Select(p => new ColumnValue { columnName = p.Key, value = p.Value ?? "" }).ToList();
            EnsureRow(id).columns = cells;
        }
        public void SetRowData(int id, Dictionary<string, string> columnData) => SetRowData(Id(id), columnData);
        public bool RemoveRow(string id) { var row = GetRowById(id); return row != null && rows.Remove(row); }
        public bool RemoveRow(int id) => RemoveRow(Id(id));
        public bool HasRow(string id) => GetRowById(id) != null;
        public bool HasRow(int id) => HasRow(Id(id));
        public List<TableRow> GetAllRows() => rows == null ? new List<TableRow>() : new List<TableRow>(rows);
        public int GetRowCount() => rows?.Count ?? 0;
        public void ClearData() => rows?.Clear();
        public List<string> GetAllIds() => rows?.Where(r => r != null).Select(r => r.id).ToList() ?? new List<string>();
        public List<int> GetAllIdsAsInt() => GetAllIds().Where(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)).Select(id => int.Parse(id, CultureInfo.InvariantCulture)).ToList();
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult();
            result.Require(!string.IsNullOrWhiteSpace(tableName), "表名不能为空");
            result.Require(!string.IsNullOrWhiteSpace(sheetName), "Sheet名不能为空");
            if (headers == null || rows == null) { result.Errors.Add("表头或行列表为空引用"); return result; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            var indices = new HashSet<int>();
            foreach (var h in headers)
            {
                if (h == null) { result.Errors.Add("表头为空引用"); continue; }
                result.Unique(names, h.columnName, "表头列名");
                result.Require(h.columnIndex >= 0 && indices.Add(h.columnIndex), h.columnName + "列索引无效或重复");
                result.Require(!string.IsNullOrWhiteSpace(h.columnType), h.columnName + "列类型不能为空");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (r == null) { result.Errors.Add("表行为空引用"); continue; }
                result.Unique(ids, r.id, "行ID");
                if (r.columns == null) { result.Errors.Add(r.id + "列值为空引用"); continue; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in r.columns)
                {
                    if (c == null) { result.Errors.Add(r.id + "单元格为空引用"); continue; }
                    result.Unique(seen, c.columnName, r.id + "行列名");
                    result.Require(names.Contains(c.columnName), r.id + "引用未定义列: " + c.columnName);
                }
            }
            return result;
        }
    }
}
