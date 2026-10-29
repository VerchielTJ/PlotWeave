using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlotWeave.Authoring.Excel
{
    /// <summary>原配表布局的无引擎对象中间层；行列索引均从0开始。</summary>
    public sealed class PlotExcelSheet
    {
        public string Name = "Sheet1";
        public List<string[]> Rows = new List<string[]>();
        public List<string> MergedRanges = new List<string>();
        public int Height => Rows.Count;
        public int Width => Rows.Count == 0 ? 0 : Rows.Max(r => r?.Length ?? 0);
        public string Get(int row, int column) => row >= 0 && row < Rows.Count && Rows[row] != null && column >= 0 && column < Rows[row].Length ? Rows[row][column] ?? "" : "";
        public void Set(int row, int column, string value)
        {
            if (row < 0 || row >= 100000 || column < 0 || column >= 16384) throw new ArgumentOutOfRangeException();
            while (Rows.Count <= row) Rows.Add(Array.Empty<string>());
            var cells = Rows[row];
            if (cells.Length <= column) Array.Resize(ref cells, column + 1);
            cells[column] = value ?? ""; Rows[row] = cells;
        }
        public bool Empty(int row) => row >= Rows.Count || Rows[row].All(string.IsNullOrEmpty);
        public bool Comment(int row) => Get(row, 0).StartsWith("##", StringComparison.Ordinal);
        public void RequireHeaders(string second = "##type")
        {
            if (Get(0, 0) != "##var" || Get(1, 0) != second || !Get(2, 0).StartsWith("##", StringComparison.Ordinal))
                throw new FormatException(Name + "前三行标记不正确");
        }
        public void Merge(int row, int first, int count)
        { if (count > 1) MergedRanges.Add(Column(first) + (row + 1) + ":" + Column(first + count - 1) + (row + 1)); }
        private static string Column(int index)
        { var result = ""; for (var i = index + 1; i > 0; i = (i - 1) / 26) result = (char)('A' + (i - 1) % 26) + result; return result; }
    }

    public static class PlotExcelFile
    {
        public static PlotExcelSheet Read(string path, string name = "Sheet1")
        {
            var sheets = PlotWeave.Editor.PlotWorkbook.Read(path);
            if (name == null) { var first = sheets.First(); return new PlotExcelSheet { Name = first.Key, Rows = first.Value }; }
            if (!sheets.TryGetValue(name, out var rows)) throw new FormatException("缺少工作表: " + name);
            return new PlotExcelSheet { Name = name, Rows = rows };
        }
        /// <summary>明确导出新的数据工作簿；不承诺保留已有文件的宏、样式或其他工作表。</summary>
        public static void Write(string path, params PlotExcelSheet[] sheets)
        {
            var temp = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(temp));
            try
            {
                PlotWeave.Editor.PlotWorkbook.Write(temp, sheets.Select(s => new KeyValuePair<string, List<string[]>>(s.Name, s.Rows)).ToList(), sheets.ToDictionary(s => s.Name, s => s.MergedRanges));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
