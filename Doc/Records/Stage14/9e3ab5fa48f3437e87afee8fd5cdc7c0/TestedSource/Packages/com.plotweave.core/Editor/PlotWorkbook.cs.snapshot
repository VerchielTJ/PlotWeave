using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace PlotWeave.Editor
{
    /// <summary>小型 OpenXML 交换格式。资产为源；表格可单独编译成 Runtime JSON，不反写资产。</summary>
    public static class PlotWorkbook
    {
        [Serializable] private sealed class CommandList { public List<PlotCommand> items = new List<PlotCommand>(); }
        [Serializable] private sealed class ChoiceList { public List<PlotChoice> items = new List<PlotChoice>(); }
        [Serializable] private sealed class ParameterList { public List<PlotParameterDefinition> items = new List<PlotParameterDefinition>(); }
        [Serializable] private sealed class StringList { public List<string> items = new List<string>(); }
        private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace OfficeRelation = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelation = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static void Export(PlotDocument document, string path)
        {
            PlotValidation.Check(document).ThrowIfInvalid();
            var sheets = new List<KeyValuePair<string, List<string[]>>>();
            sheets.Add(Sheet("Story", new[] { "id", "title", "entryNodeId", "formatVersion" }, new[] { "string", "string", "string", "int" },
                new[] { new[] { document.id, document.title, document.entryNodeId, document.formatVersion.ToString(CultureInfo.InvariantCulture) } }));
            sheets.Add(Sheet("Nodes", new[] { "id", "title", "sceneId", "actorId", "text", "textKey", "nextNodeId", "x", "y", "commands", "choices" },
                new[] { "string", "string", "string", "string", "string", "string", "string", "float", "float", "string", "string" },
                document.nodes.Select(n => new[] { n.id, n.title, n.sceneId, n.actorId, n.text, n.textKey, n.nextNodeId,
                    n.position.x.ToString("R", CultureInfo.InvariantCulture), n.position.y.ToString("R", CultureInfo.InvariantCulture),
                    JsonUtility.ToJson(new CommandList { items = n.commands }), JsonUtility.ToJson(new ChoiceList { items = n.choices }) })));
            sheets.Add(Sheet("Actors", new[] { "id", "name", "resourceKey" }, null, document.actors.Select(a => new[] { a.id, a.name, a.resourceKey })));
            sheets.Add(Sheet("Texts", new[] { "key", "value" }, null, document.texts.Select(t => new[] { t.key, t.value })));
            sheets.Add(Sheet("Variables", new[] { "key", "value" }, null, document.variables.Select(v => new[] { v.key, v.value })));
            sheets.Add(Sheet("Commands", new[] { "key", "displayName", "parameters" }, null,
                document.commands.Select(c => new[] { c.key, c.displayName, JsonUtility.ToJson(new ParameterList { items = c.parameters }) })));
            sheets.Add(Sheet("Enums", new[] { "key", "values" }, null,
                document.enums.Select(e => new[] { e.key, JsonUtility.ToJson(new StringList { items = e.values }) })));
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Write(temp, sheets);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static PlotDocument Compile(string path)
        {
            var sheets = Read(path);
            var storyRows = Rows(sheets, "Story");
            if (storyRows.Count != 1) throw new FormatException("Story 必须且只能有一行。");
            var story = storyRows[0];
            var document = new PlotDocument { id = Cell(story, "id"), title = Cell(story, "title"), entryNodeId = Cell(story, "entryNodeId"), formatVersion = int.Parse(Cell(story, "formatVersion"), CultureInfo.InvariantCulture) };
            foreach (var row in Rows(sheets, "Nodes"))
                document.nodes.Add(new PlotNode { id = Cell(row, "id"), title = Cell(row, "title"), sceneId = Cell(row, "sceneId"), actorId = Cell(row, "actorId"),
                    text = Cell(row, "text"), textKey = Cell(row, "textKey"), nextNodeId = Cell(row, "nextNodeId"),
                    position = new Vector2(ParseFloat(Cell(row, "x")), ParseFloat(Cell(row, "y"))),
                    commands = Parse<CommandList>(Cell(row, "commands")).items, choices = Parse<ChoiceList>(Cell(row, "choices")).items });
            foreach (var row in Rows(sheets, "Actors")) document.actors.Add(new PlotActor { id = Cell(row, "id"), name = Cell(row, "name"), resourceKey = Cell(row, "resourceKey") });
            foreach (var row in Rows(sheets, "Texts")) document.texts.Add(new PlotText { key = Cell(row, "key"), value = Cell(row, "value") });
            foreach (var row in Rows(sheets, "Variables")) document.variables.Add(new PlotVariable { key = Cell(row, "key"), value = Cell(row, "value") });
            foreach (var row in Rows(sheets, "Commands")) document.commands.Add(new PlotCommandDefinition { key = Cell(row, "key"), displayName = Cell(row, "displayName"), parameters = Parse<ParameterList>(Cell(row, "parameters")).items });
            foreach (var row in Rows(sheets, "Enums")) document.enums.Add(new PlotEnumDefinition { key = Cell(row, "key"), values = Parse<StringList>(Cell(row, "values")).items });
            PlotValidation.Check(document).ThrowIfInvalid();
            return document;
        }

        public static void CompileToJson(string workbookPath, string jsonPath)
        {
            var data = Compile(workbookPath); // 成功校验后才覆盖输出。
            File.WriteAllText(jsonPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
        }

        private static T Parse<T>(string value) where T : new() => string.IsNullOrWhiteSpace(value) ? new T() : JsonUtility.FromJson<T>(value) ?? throw new FormatException("JSON 单元格无效。");
        private static float ParseFloat(string value)
        {
            var result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new FormatException("位置必须是有限数字。");
            return result;
        }
        private static string Cell(Dictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value ?? "" : throw new FormatException("缺少列：" + key);
        private static KeyValuePair<string, List<string[]>> Sheet(string name, string[] fields, string[] types, IEnumerable<string[]> values)
        {
            var rows = new List<string[]> { new[] { "##var" }.Concat(fields).ToArray(), new[] { "##type" }.Concat(types ?? fields.Select(_ => "string").ToArray()).ToArray(), new[] { "##desc" }.Concat(fields).ToArray() };
            rows.AddRange(values.Select(v => new[] { "" }.Concat(v).ToArray()));
            return new KeyValuePair<string, List<string[]>>(name, rows);
        }

        private static List<Dictionary<string, string>> Rows(Dictionary<string, List<string[]>> sheets, string name)
        {
            if (!sheets.TryGetValue(name, out var raw) || raw.Count < 3 || raw[0].Length == 0 || raw[0][0] != "##var") throw new FormatException("缺少工作表或表头：" + name);
            var fields = raw[0].Skip(1).ToArray();
            if (fields.Any(string.IsNullOrWhiteSpace) || fields.Distinct().Count() != fields.Length) throw new FormatException("空或重复列名：" + name);
            var rows = new List<Dictionary<string, string>>();
            foreach (var cells in raw.Skip(3))
            {
                if (cells.All(string.IsNullOrEmpty)) continue;
                var row = new Dictionary<string, string>();
                for (var i = 0; i < fields.Length; i++) row.Add(fields[i], cells.Length > i + 1 ? cells[i + 1] : "");
                rows.Add(row);
            }
            return rows;
        }

        internal static void Write(string path, List<KeyValuePair<string, List<string[]>>> sheets, Dictionary<string, List<string>> mergedRanges = null)
        {
            if (sheets.Count == 0 || sheets.Select(s => s.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sheets.Count ||
                sheets.Any(s => string.IsNullOrWhiteSpace(s.Key) || s.Key.Length > 31 || s.Key.IndexOfAny(new[] { '[', ']', ':', '*', '?', '/', '\\' }) >= 0))
                throw new FormatException("工作表名称为空、重复或不符合Excel规则。");
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
                var types = new XElement(content + "Types",
                    new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(content + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
                var workbook = new XElement(Spreadsheet + "workbook", new XAttribute(XNamespace.Xmlns + "r", OfficeRelation));
                var sheetElements = new XElement(Spreadsheet + "sheets"); workbook.Add(sheetElements);
                var relationships = new XElement(PackageRelation + "Relationships");
                Put(archive, "_rels/.rels", new XElement(PackageRelation + "Relationships", new XElement(PackageRelation + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", OfficeRelation.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
                for (var i = 0; i < sheets.Count; i++)
                {
                    var name = "sheet" + (i + 1) + ".xml";
                    sheetElements.Add(new XElement(Spreadsheet + "sheet", new XAttribute("name", sheets[i].Key), new XAttribute("sheetId", i + 1), new XAttribute(OfficeRelation + "id", "rId" + (i + 1))));
                    relationships.Add(new XElement(PackageRelation + "Relationship", new XAttribute("Id", "rId" + (i + 1)), new XAttribute("Type", OfficeRelation.NamespaceName + "/worksheet"), new XAttribute("Target", "worksheets/" + name)));
                    types.Add(new XElement(content + "Override", new XAttribute("PartName", "/xl/worksheets/" + name), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                    var sheetData = new XElement(Spreadsheet + "sheetData");
                    var rows = sheets[i].Value;
                    for (var r = 0; r < rows.Count; r++)
                    {
                        var row = new XElement(Spreadsheet + "row", new XAttribute("r", r + 1));
                        for (var c = 0; c < rows[r].Length; c++)
                        {
                            var value = rows[r][c] ?? "";
                            if (value.Length > 32767) throw new FormatException("单元格超过 Excel 的 32767 字符限制，请拆分剧情。");
                            row.Add(new XElement(Spreadsheet + "c", new XAttribute("r", Column(c) + (r + 1)), new XAttribute("t", "inlineStr"),
                                new XElement(Spreadsheet + "is", new XElement(Spreadsheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))));
                        }
                        sheetData.Add(row);
                    }
                    var worksheet = new XElement(Spreadsheet + "worksheet", sheetData);
                    if (mergedRanges != null && mergedRanges.TryGetValue(sheets[i].Key, out var ranges) && ranges.Count > 0)
                        worksheet.Add(new XElement(Spreadsheet + "mergeCells", new XAttribute("count", ranges.Count), ranges.Select(range => new XElement(Spreadsheet + "mergeCell", new XAttribute("ref", range)))));
                    Put(archive, "xl/worksheets/" + name, worksheet);
                }
                Put(archive, "[Content_Types].xml", types);
                Put(archive, "xl/workbook.xml", workbook);
                Put(archive, "xl/_rels/workbook.xml.rels", relationships);
            }
        }

        internal static Dictionary<string, List<string[]>> Read(string path)
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                if (archive.Entries.Sum(e => e.Length) > 32 * 1024 * 1024) throw new FormatException("工作簿解压体积超过 32MB。");
                var workbook = Xml(archive, "xl/workbook.xml");
                var rels = Xml(archive, "xl/_rels/workbook.xml.rels").Root.Elements(PackageRelation + "Relationship").ToDictionary(e => (string)e.Attribute("Id"), e => e);
                var shared = new List<string>();
                if (archive.GetEntry("xl/sharedStrings.xml") != null)
                    shared.AddRange(Xml(archive, "xl/sharedStrings.xml").Descendants(Spreadsheet + "si").Select(s => string.Concat(s.Descendants(Spreadsheet + "t").Select(t => t.Value))));
                var result = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
                foreach (var sheet in workbook.Descendants(Spreadsheet + "sheet"))
                {
                    var rel = rels[(string)sheet.Attribute(OfficeRelation + "id")];
                    if ((string)rel.Attribute("TargetMode") == "External") throw new FormatException("不支持外部工作表。");
                    var target = ((string)rel.Attribute("Target")).Replace('\\', '/');
                    var part = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                    if (part.Contains("..")) throw new FormatException("工作表路径无效。");
                    var rows = new List<string[]>();
                    foreach (var row in Xml(archive, part).Descendants(Spreadsheet + "sheetData").Elements(Spreadsheet + "row"))
                    {
                        var rowIndex = int.Parse((string)row.Attribute("r") ?? (rows.Count + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        if (rowIndex > 100000) throw new FormatException("行号超过限制。");
                        while (rows.Count < rowIndex) rows.Add(Array.Empty<string>());
                        var values = new Dictionary<int, string>();
                        foreach (var cell in row.Elements(Spreadsheet + "c"))
                        {
                            if (cell.Element(Spreadsheet + "f") != null) throw new FormatException("编表输入不支持公式，请粘贴为值。");
                            var index = ColumnIndex((string)cell.Attribute("r"));
                            if (index > 16383) throw new FormatException("列号超过Excel限制。");
                            var type = (string)cell.Attribute("t");
                            var value = (string)cell.Element(Spreadsheet + "v") ?? "";
                            if (type == "s") value = shared[int.Parse(value, CultureInfo.InvariantCulture)];
                            else if (type == "inlineStr") value = string.Concat(cell.Descendants(Spreadsheet + "t").Select(t => t.Value));
                            values[index] = value;
                        }
                        var count = values.Count == 0 ? 0 : values.Keys.Max() + 1;
                        var array = new string[count];
                        foreach (var value in values) array[value.Key] = value.Value;
                        rows[rowIndex - 1] = array;
                    }
                    result.Add((string)sheet.Attribute("name"), rows);
                }
                return result;
            }
        }
        private static XDocument Xml(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name) ?? throw new FormatException("工作簿缺少部件：" + name);
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) return XDocument.Load(reader);
        }
        private static void Put(ZipArchive archive, string name, XElement root)
        {
            using (var stream = archive.CreateEntry(name).Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(writer);
        }
        private static string Column(int index)
        {
            var name = ""; for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name; return name;
        }
        private static int ColumnIndex(string reference)
        {
            if (string.IsNullOrEmpty(reference)) throw new FormatException("单元格缺少坐标。");
            var index = 0;
            foreach (var c in reference) { if (c < 'A' || c > 'Z') break; index = index * 26 + c - 'A' + 1; }
            if (index < 1) throw new FormatException("单元格坐标无效。");
            return index - 1;
        }
    }
}
