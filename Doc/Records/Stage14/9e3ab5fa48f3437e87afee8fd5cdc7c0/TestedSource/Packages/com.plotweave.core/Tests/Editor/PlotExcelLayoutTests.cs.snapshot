using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Excel;
using PlotWeave.Generation;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Tests
{
    public sealed class PlotExcelLayoutTests
    {
        private PlotAuthoringExample _data;
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private string _folder;
        [SetUp] public void SetUp() { _data = PlotGenerationExample.CreateDefault(); _folder = Path.Combine(Path.GetTempPath(), "PlotWeaveExcel_" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown()
        { foreach (var value in _owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); _owned.Clear(); foreach (var asset in _data.Assets) UnityEngine.Object.DestroyImmediate(asset); if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
        private T Own<T>(T value) where T : UnityEngine.Object { _owned.Add(value); return value; }
        private List<PlotAtomRowData> Read(PlotExcelSheet sheet)
        { var rows = PlotAuthoringExcel.ReadNodes(sheet, _data.Header); _owned.AddRange(rows); return rows; }
        [Test] public void EnumContinuationRowsAndEmptyDefinitionsRoundTrip()
        {
            _data.Enums.dynamicLists.Add(new PlotEnumAsset.ParameterEnum { enumName = "EmptyType", displayName = "空类型" });
            var sheet = PlotAuthoringExcel.ExportEnums(_data.Enums); var target = Own(ScriptableObject.CreateInstance<PlotEnumAsset>());
            PlotAuthoringExcel.ReadEnums(sheet, target);
            CollectionAssert.AreEqual(_data.Enums.GetEnumKeys("RoleActionType"), target.GetEnumKeys("RoleActionType"));
            Assert.That(target.GetEnum("EmptyType"), Is.Not.Null); Assert.That(sheet.Get(1, 6), Is.EqualTo("name"));
        }
        [Test] public void OrphanEnumRowIsRejectedWithoutClearingDestination()
        {
            var sheet = PlotAuthoringExcel.ExportEnums(_data.Enums); sheet.Set(3, 1, "");
            var before = JsonUtility.ToJson(_data.Enums); Assert.Throws<FormatException>(() => PlotAuthoringExcel.ReadEnums(sheet, _data.Enums)); Assert.That(JsonUtility.ToJson(_data.Enums), Is.EqualTo(before));
        }
        [Test] public void BeanParametersAndUnrepresentedMetadataArePreserved()
        {
            var bean = _data.Beans.eventTypes[0]; bean.defaultCallbackValue = 5; bean.defaultCallbackList = "a,b"; bean.parameters[0].paramSign = "actor";
            var sheet = PlotAuthoringExcel.ExportBeans(_data.Beans); PlotAuthoringExcel.ReadBeans(sheet, _data.Beans, _data.Enums);
            Assert.That(_data.Beans.eventTypes[0].parameters.Count, Is.EqualTo(4)); Assert.That(_data.Beans.eventTypes[0].defaultCallbackValue, Is.EqualTo(5));
            Assert.That(_data.Beans.eventTypes[0].parameters[0].paramSign, Is.EqualTo("actor")); Assert.That(_data.Beans.eventTypes[0].defaultCallbackList, Is.EqualTo("a,b"));
        }
        [Test] public void UnknownBeanParameterTypeIsRejectedWithoutMutation()
        {
            var sheet = PlotAuthoringExcel.ExportBeans(_data.Beans); sheet.Set(3, 8, "UnknownEnum"); var before = JsonUtility.ToJson(_data.Beans);
            Assert.Throws<FormatException>(() => PlotAuthoringExcel.ReadBeans(sheet, _data.Beans, _data.Enums)); Assert.That(JsonUtility.ToJson(_data.Beans), Is.EqualTo(before));
        }
        [Test] public void HeaderPreservesStarPrefixPhysicalColumnsAndListFlag()
        {
            _data.Header.GetColumnByName("*PlotSetBack").IsList = false;
            var target = Own(ScriptableObject.CreateInstance<PlotAtomDataHeaderAsset>()); var sheet = PlotAuthoringExcel.ExportHeader(_data.Header);
            PlotAuthoringExcel.ReadHeader(sheet, target, _data.Enums, _data.Beans);
            Assert.That(target.GetColumnByName("*PlotSetBack").IsList, Is.False); Assert.That(target.GetColumnByName("*Branch").columnIndex, Is.EqualTo(11));
            Assert.That(target.GetColumnByName("*RoleAction").members, Is.EqualTo(4)); Assert.That(sheet.MergedRanges, Does.Contain("F1:I1"));
        }
        [Test] public void ListsWithDifferentLengthsReadEveryContinuationMember()
        {
            var sheet = PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }); var result = Read(sheet).Single();
            Assert.That(sheet.Get(3, 0), Is.Empty, "A列只用于控制标记"); Assert.That(sheet.Get(3, 1), Is.EqualTo("1001")); Assert.That(sheet.Get(4, 1), Is.Empty);
            CollectionAssert.AreEqual(new[] { "102", "Shake", "Happy", "2" }, result.GetColumnRows("*RoleAction")[1].values);
            Assert.That(result.GetColumnRowCount("*PlotSetBack"), Is.EqualTo(1)); Assert.That(result.GetColumnRowCount("*Branch"), Is.EqualTo(2));
        }
        [Test] public void ScalarBeanRetainsEveryMember()
        {
            _data.Header.GetColumnByName("*PlotSetBack").IsList = false;
            var rows = Read(PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }));
            CollectionAssert.AreEqual(new[] { "Default", "demo.stage" }, rows[0].GetColumnFirstRowValues("*PlotSetBack"));
        }
        [Test] public void ContinuationCannotSilentlyOverrideScalarData()
        {
            var sheet = PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }); sheet.Set(4, 2, "999");
            Assert.Throws<FormatException>(() => Read(sheet));
        }
        [Test] public void DuplicateNodeIdsAndOrphanContinuationsFail()
        {
            var sheet = PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }); sheet.Set(4, 1, "1001"); Assert.Throws<FormatException>(() => Read(sheet));
            sheet.Set(3, 1, ""); sheet.Set(4, 1, ""); Assert.Throws<FormatException>(() => Read(sheet));
        }
        [Test] public void NumericNodeIdsHaveOneCanonicalAssetIdentity()
        {
            var sheet = PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }); sheet.Set(3, 1, "001001");
            var node = Read(sheet).Single(); Assert.That(node.GetColumnSingleValue("ScenesID"), Is.EqualTo("1001")); Assert.That(node.rowId, Is.EqualTo("1001"));
        }
        [Test] public void EmptyListItemsAreRejectedInsteadOfDisappearingDuringExport()
        {
            _data.Row.AddRowToColumn("*Branch", new List<string> { "", "0" }); // 非空的0值应保留。
            Assert.That(Read(PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }))[0].GetColumnRowCount("*Branch"), Is.EqualTo(3));
            var column = _data.Header.GetColumnByName("Dialog"); column.IsList = true; _data.Row.AddRowToColumn("Dialog", new List<string> { "" });
            Assert.Throws<FormatException>(() => PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }));
        }
        [Test] public void FiveLanguagesUnicodeNewlinesAndXmlCharactersSurviveDiskRoundTrip()
        {
            _data.Dialog.SetDialogContent("demo.start", 0, "你好<&>\n第二行  "); _data.Dialog.SetDialogContent("demo.start", 4, "안녕하세요");
            var path = Path.Combine(_folder, "多语言.xlsx"); PlotExcelFile.Write(path, PlotAuthoringExcel.ExportTexts(_data.Dialog));
            var target = Own(ScriptableObject.CreateInstance<PlotDialogKeyAsset>()); PlotAuthoringExcel.ReadTexts(PlotExcelFile.Read(path), target);
            Assert.That(target.GetDialogContent("demo.start", 0), Is.EqualTo("你好<&>\n第二行  ")); Assert.That(target.GetDialogContent("demo.start", 4), Is.EqualTo("안녕하세요"));
        }
        [Test] public void DuplicateTextKeysDoNotPartiallyClearAsset()
        {
            var sheet = PlotAuthoringExcel.ExportTexts(_data.Dialog); sheet.Rows.Add((string[])sheet.Rows[3].Clone()); var before = JsonUtility.ToJson(_data.Dialog);
            Assert.Throws<FormatException>(() => PlotAuthoringExcel.ReadTexts(sheet, _data.Dialog)); Assert.That(JsonUtility.ToJson(_data.Dialog), Is.EqualTo(before));
        }
        [Test] public void FixedFiveLanguageLayoutRefusesSilentTruncation()
        {
            foreach (var entry in _data.Dialog.dialogKeyDataList) entry.value.Add("extra");
            Assert.Throws<FormatException>(() => PlotAuthoringExcel.ExportTexts(_data.Dialog));
        }
        [Test] public void GenericTableKeepsStringIdsGapsAndUnrepresentedMetadata()
        {
            var table = _data.Actors; table.headers[0].columnType = "string"; table.headers[1].columnIndex = 4; table.rows.Clear();
            table.SetRowData("007", new Dictionary<string, string> { { table.headers[0].columnName, "007" }, { table.headers[1].columnName, "demo.actor" } });
            var dataName = table.customDataName; var lubanName = table.customLubanName;
            PlotAuthoringExcel.ReadTable(PlotAuthoringExcel.ExportTable(table), table);
            Assert.That(table.GetRowById("007"), Is.Not.Null); Assert.That(table.headers[1].columnIndex, Is.EqualTo(4)); Assert.That(table.customDataName, Is.EqualTo(dataName)); Assert.That(table.customLubanName, Is.EqualTo(lubanName));
        }
        [Test] public void InvalidTableDoesNotDestroyExistingData()
        {
            var sheet = PlotAuthoringExcel.ExportTable(_data.Actors); sheet.Rows.Add((string[])sheet.Rows[3].Clone()); var before = JsonUtility.ToJson(_data.Actors);
            Assert.Throws<FormatException>(() => PlotAuthoringExcel.ReadTable(sheet, _data.Actors)); Assert.That(JsonUtility.ToJson(_data.Actors), Is.EqualTo(before));
        }
        [Test] public void XlsxWritesMergedHeadersAndRejectsFormulaInputs()
        {
            var path = Path.Combine(_folder, "Nodes.xlsx"); PlotExcelFile.Write(path, PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row }));
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml"); XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
                Assert.That(doc.Descendants(ns + "mergeCell").Any(e => (string)e.Attribute("ref") == "F1:I1"), Is.True);
                doc.Descendants(ns + "c").First(c => (string)c.Attribute("r") == "B4").Add(new XElement(ns + "f", "1+1"));
                entry.Delete(); using (var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open()) doc.Save(stream);
            }
            Assert.Throws<FormatException>(() => PlotExcelFile.Read(path));
        }
        [Test] public void FailedWorkbookWritePreservesPreviousFile()
        {
            var path = Path.Combine(_folder, "Texts.xlsx"); var sheet = PlotAuthoringExcel.ExportTexts(_data.Dialog); PlotExcelFile.Write(path, sheet); var before = File.ReadAllBytes(path);
            sheet.Set(3, 2, new string('x', 32768)); Assert.Throws<FormatException>(() => PlotExcelFile.Write(path, sheet)); CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
