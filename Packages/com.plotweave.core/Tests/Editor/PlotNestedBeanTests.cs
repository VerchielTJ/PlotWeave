using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Excel;
using PlotWeave.Data;
using PlotWeave.Generation;
using PlotWeave.Legacy;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotNestedBeanTests
    {
        private PlotAuthoringExample _data;
        [SetUp] public void SetUp() { _data = PlotNestedBeanExample.Create(); }
        [TearDown] public void TearDown() { foreach (var asset in _data.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        [Test] public void NestedTypesFlattenToLeavesAndRebuildTypedValueSources()
        {
            Assert.That(_data.Header.Validate().IsValid, Is.True); var leaves = PlotBeanLayout.Leaves(_data.Beans, "CameraCue"); Assert.That(leaves.Select(l => l.Path), Is.EqualTo(new[] { "ActorID", "Transform.X", "Transform.Visible" }));
            var source = AuthoringPlotValueSource.FromRow(_data.Row); var camera = PlotValueReader.Required<IPlotValueSource>(source, "CameraCue"); Assert.That(PlotValueReader.Required<int>(camera, "ActorID"), Is.EqualTo(101)); var transform = PlotValueReader.Required<IPlotValueSource>(camera, "Transform"); Assert.That(PlotValueReader.Required<float>(transform, "X"), Is.EqualTo(1.25f)); Assert.That(PlotValueReader.Required<bool>(transform, "Visible"), Is.True); Assert.That(_data.Row.Validate().IsValid, Is.True);
        }
        [Test] public void CyclesAndDanglingReferencesAreRejectedBeforeLayoutExpansion()
        {
            var nested = _data.Beans.GetEventTypeByKey("CameraCue").parameters[1]; nested.beanType = "CameraCue"; Assert.That(_data.Beans.Validate().IsValid, Is.False); Assert.Throws<FormatException>(() => PlotBeanLayout.Leaves(_data.Beans, "CameraCue")); nested.beanType = "Missing"; Assert.That(_data.Beans.Validate().IsValid, Is.False);
        }
        [Test] public void NestedRowValidationChecksEachLeafType()
        { _data.Row.GetColumnRows("CameraCue")[0].values[1] = "not-a-float"; Assert.That(_data.Row.Validate().IsValid, Is.False); }
        [Test] public void NestedBeansAndRowsRoundTripThroughRealWorkbookFiles()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PlotWeaveNested_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); var beans = ScriptableObject.CreateInstance<PlotEventTypeAsset>(); var header = ScriptableObject.CreateInstance<PlotAtomDataHeaderAsset>(); var rows = new List<PlotAtomRowData>();
            try
            {
                var beanFile = Path.Combine(folder, "beans.xlsx"); PlotExcelFile.Write(beanFile, PlotAuthoringExcel.ExportBeans(_data.Beans)); PlotAuthoringExcel.ReadBeans(PlotExcelFile.Read(beanFile), beans, _data.Enums); Assert.That(beans.GetEventTypeByKey("CameraCue").parameters[1].beanType, Is.EqualTo("TransformCue"));
                var nodeFile = Path.Combine(folder, "nodes.xlsx"); PlotExcelFile.Write(nodeFile, PlotAuthoringExcel.ExportNodes(_data.Header, new[] { _data.Row })); var sheet = PlotExcelFile.Read(nodeFile); PlotAuthoringExcel.ReadHeader(sheet, header, _data.Enums, beans); rows = PlotAuthoringExcel.ReadNodes(sheet, header); Assert.That(header.GetColumnByName("CameraCue").members, Is.EqualTo(3)); Assert.That(rows.Single().GetColumnRows("*CameraCues").Select(r => string.Join("|", r.values)), Is.EqualTo(_data.Row.GetColumnRows("*CameraCues").Select(r => string.Join("|", r.values))));
            }
            finally { foreach (var row in rows) UnityEngine.Object.DestroyImmediate(row); UnityEngine.Object.DestroyImmediate(beans); UnityEngine.Object.DestroyImmediate(header); Directory.Delete(folder, true); }
        }
        [Test] public void NestedEventDefaultsUseTheSamePhysicalMemberOrder()
        { var value = new PlotEventJsonData(); value.InitFromEventType(_data.Beans.GetEventTypeByKey("CameraCue"), _data.Beans); Assert.That(value.parameters, Is.EqualTo(new[] { "0", "0", "false" })); var source = PlotBeanLayout.Read(_data.Beans, "CameraCue", new[] { "12", "2.5", "true" }); Assert.That(PlotValueReader.Required<int>(source, "ActorID"), Is.EqualTo(12)); }
        private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("需先生成嵌套样例并等待编译: " + name);
        [Test] public void ActualGeneratedEditorAndGameContractsConsumeNestedObjects()
        {
            var adapterType = Type("PlotWeave.Generated.Stage12.Nested.EditorPlotAtomDataAdapter"); var adapter = Activator.CreateInstance(adapterType, new object[] { _data.Row }); var camera = adapterType.GetProperty("CameraCue").GetValue(adapter); var transform = camera.GetType().GetProperty("Transform").GetValue(camera); Assert.That(transform.GetType().GetProperty("X").GetValue(transform), Is.EqualTo(1.25f)); var list = (IList)adapterType.GetProperty("CameraCues").GetValue(adapter); Assert.That(list.Count, Is.EqualTo(2));
        }
        public sealed class Loader<T> { public T Load(string name) => (T)Activator.CreateInstance(typeof(T), File.ReadAllBytes(PlotNestedBeanExample.Root + "/LubanGenerated/Data/" + name + ".bytes")); }
        [Test] public void ActualLubanBinaryPreservesNestedScalarAndRepeatedBeans()
        {
            var buf = Type("Luban.ByteBuf"); var loader = Activator.CreateInstance(typeof(Loader<>).MakeGenericType(buf)); var callback = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), buf), loader, "Load"); var tables = Activator.CreateInstance(Type("PlotWeaveLubanNested.Tables"), callback); var table = tables.GetType().GetProperty("TbPlotAtom").GetValue(tables); var raw = table.GetType().GetMethod("Get").Invoke(table, new object[] { 1001 });
            var adapter = Activator.CreateInstance(Type("PlotWeave.Generated.Stage12.Nested.GamePlotAtomDataAdapter"), raw); var camera = adapter.GetType().GetProperty("CameraCue").GetValue(adapter); var transform = camera.GetType().GetProperty("Transform").GetValue(camera); Assert.That(transform.GetType().GetProperty("X").GetValue(transform), Is.EqualTo(1.25f)); var list = (IList)adapter.GetType().GetProperty("CameraCues").GetValue(adapter); Assert.That(list.Count, Is.EqualTo(2));
        }
    }
}
