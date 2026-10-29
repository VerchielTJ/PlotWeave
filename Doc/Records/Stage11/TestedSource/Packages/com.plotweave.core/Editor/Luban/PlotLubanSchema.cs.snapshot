using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PlotWeave.Authoring.Excel;

namespace PlotWeave.Authoring.Luban
{
    /// <summary>原六表格式保留于Raw；此转换层生成Luban4独立输入，绝不回写原表。</summary>
    public static class PlotLubanSchema
    {
        public static void Identifier(string value, bool qualified = false)
        { if (string.IsNullOrEmpty(value) || !(qualified ? value.Split('.').All(v => Regex.IsMatch(v, @"^[A-Za-z_][A-Za-z0-9_]*$")) : Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$"))) throw new FormatException("无效生成标识符: " + value); }
        public static PlotExcelSheet Enums(PlotExcelSheet old)
        {
            var sheet = new PlotExcelSheet(); sheet.Rows.Add(new[] { "##var", "full_name", "flags", "unique", "group", "comment", "tags", "*items", "", "", "", "" }); sheet.Rows.Add(new[] { "##var", "", "", "", "", "", "", "name", "alias", "value", "comment", "tags" }); sheet.Rows.Add(new[] { "##" }); sheet.Merge(0, 7, 5);
            for (var r = 3; r < old.Height; r++) { var root = old.Get(r, 1); sheet.Rows.Add(new[] { "", root, root == "" ? "" : "0", root == "" ? "" : "1", root == "" ? "" : "c", old.Get(r, 0) == "" ? old.Get(r, 4) : old.Get(r, 0), old.Get(r, 5), old.Get(r, 6), old.Get(r, 7) == old.Get(r, 6) ? "" : old.Get(r, 7), old.Get(r, 8), old.Get(r, 9), old.Get(r, 10) }); }
            return sheet;
        }
        public static PlotExcelSheet Beans(PlotExcelSheet old)
        {
            var sheet = new PlotExcelSheet(); sheet.Rows.Add(new[] { "##var", "full_name", "parent", "valueType", "alias", "sep", "comment", "tags", "group", "*fields", "", "", "", "", "", "" }); sheet.Rows.Add(new[] { "##var", "", "", "", "", "", "", "", "", "name", "alias", "type", "group", "comment", "tags", "variants" }); sheet.Rows.Add(new[] { "##" }); sheet.Merge(0, 9, 7);
            for (var r = 3; r < old.Height; r++) { var root = old.Get(r, 1); sheet.Rows.Add(new[] { "", root, old.Get(r, 2), root == "" ? "" : "false", old.Get(r, 4), old.Get(r, 3), old.Get(r, 5), old.Get(r, 6), root == "" ? "" : "c", old.Get(r, 7), "", old.Get(r, 8), old.Get(r, 9), old.Get(r, 10), old.Get(r, 11), "" }); }
            return sheet;
        }
        public static string Prepare(PlotAuthoringProject project, string raw, string data)
        {
            Directory.CreateDirectory(data); var module = new XElement("module", new XAttribute("name", ""));
            PlotExcelFile.Write(Path.Combine(data, PlotExcelExchange.EnumFile), Enums(PlotExcelFile.Read(Path.Combine(raw, PlotExcelExchange.EnumFile))));
            PlotExcelFile.Write(Path.Combine(data, PlotExcelExchange.BeanFile), Beans(PlotExcelFile.Read(Path.Combine(raw, PlotExcelExchange.BeanFile))));
            var files = new[] { PlotExcelExchange.NodeFile, PlotExcelExchange.DialogFile, PlotExcelExchange.ConstantFile, PlotExcelExchange.ActorFile };
            var beans = new[] { "PlotAtomData", "PlotDialogKey", "PlotConstantKey", "PlotThespianData" }; var tables = new[] { "TbPlotAtom", "TbDialog", "TbConstant", "TbActor" }; var indices = new[] { project.idColumn.TrimStart('*'), "keyID", "keyID", "ID" };
            for (var i = 0; i < files.Length; i++)
            {
                var sheet = PlotExcelFile.Read(Path.Combine(raw, files[i])); var bean = new XElement("bean", new XAttribute("name", beans[i]));
                for (var c = 1; c < sheet.Width; c++)
                {
                    var name = sheet.Get(0, c).TrimStart('*'); if (name == "") continue; Identifier(name); var type = sheet.Get(1, c);
                    // 星号是Luban跨行列表读取标记；作者模型中的IsList才是列表类型依据。
                    var list = i == 0 ? project.header.sheets.Single(h => h.columnIndex == c).IsList : type.StartsWith("list,", StringComparison.Ordinal);
                    sheet.Set(0, c, (list ? "*" : "") + name); bean.Add(new XElement("var", new XAttribute("name", name), new XAttribute("type", type)));
                }
                // 重新恢复跨成员合并，Read只读取数据内容。
                if (i == 0) foreach (var column in project.header.sheets) for (var r = 0; r < 3; r++) sheet.Merge(r, column.columnIndex, column.members);
                PlotExcelFile.Write(Path.Combine(data, files[i]), sheet); module.Add(bean); module.Add(new XElement("table", new XAttribute("name", tables[i]), new XAttribute("value", beans[i]), new XAttribute("input", files[i]), new XAttribute("index", indices[i])));
            }
            var schema = Path.Combine(Directory.GetParent(data).FullName, "schema.xml"); new XDocument(module).Save(schema); return schema;
        }
    }
}
