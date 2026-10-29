using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Authoring;
using UnityEditor;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Tests
{
    public sealed class PlotAuthoringTests
    {
        private PlotAuthoringExample _data;
        private string _assetFolder;
        [SetUp] public void SetUp() { _data = PlotAuthoringExample.Create(); }
        [TearDown] public void TearDown()
        {
            foreach (var asset in _data.Assets)
                if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            if (_assetFolder != null && AssetDatabase.IsValidFolder(_assetFolder)) AssetDatabase.DeleteAsset(_assetFolder);
        }
        [Test] public void ComplexSampleHasIndependentValidTypeAndDataAssets()
        {
            foreach (var asset in _data.Assets) Assert.That(asset.Validate().Errors, Is.Empty, asset.name);
            Assert.That(_data.Row.GetColumnRows("*ActorCues").Count, Is.EqualTo(2));
            Assert.That(_data.Row.GetColumnRows("*ActorCues")[1].values, Is.EqualTo(new[] { "102", "happy", "1.25,0,-2" }));
        }
        [Test] public void EnumKeysRemainDifferentFromDisplayLabels()
        {
            Assert.That(_data.Enums.GetEnumKeys("Mood"), Is.EqualTo(new[] { "calm", "happy" }));
            Assert.That(_data.Enums.GetEnumLabels("Mood"), Is.EqualTo(new[] { "平静", "开心" }));
            _data.Row.GetColumnRows("*ActorCues")[0].values[1] = "平静";
            Assert.That(_data.Row.Validate().IsValid, Is.False, "显示名不能静默代替存储key");
        }
        [Test] public void DuplicateEnumAndParameterDisplayLabelsAreRejected()
        {
            _data.Enums.dynamicLists[0].values[1].label = "平静";
            Assert.That(_data.Enums.CheckAll(), Is.False);
            _data.Beans.eventTypes[0].parameters[1].displayName = "演员ID";
            Assert.That(_data.Beans.CheckAll(), Is.False);
        }
        [Test] public void MissingEnumReferencesAndDefaultsAreReported()
        {
            _data.Beans.eventTypes[0].parameters[1].enumValue = "absent";
            Assert.That(_data.Beans.Validate().Errors.Any(e => e.Contains("默认枚举值不存在")), Is.True);
            _data.Beans.eventTypes[0].parameters[1].enumType = "absent";
            Assert.That(_data.Beans.Validate().Errors.Any(e => e.Contains("引用不存在的枚举")), Is.True);
        }
        [Test] public void HeaderKeepsBeanMemberIndicesAndParameterMaps()
        {
            Assert.That(_data.Header.GetColumnByName("*ActorCues").columnIndex, Is.EqualTo(3));
            Assert.That(_data.Header.GetColumnEventParameterKeys("*ActorCues"), Is.EqualTo(new[] { "actorId", "mood", "position" }));
            Assert.That(_data.Header.GetColumnEventParameterEnumName("*ActorCues", "mood"), Is.EqualTo("Mood"));
            Assert.That(_data.Header.GetColumnEventParameterDisplayAndKeyNameMap("*ActorCues")["位置"], Is.EqualTo("position"));
            Assert.That(_data.Header.GetListTypeColumns().Count, Is.EqualTo(2));
        }
        [Test] public void HeaderValidationDoesNotSilentlyRewriteMemberCounts()
        {
            var column = _data.Header.GetColumnByName("*ActorCues"); column.members = 2;
            Assert.That(_data.Header.Validate().IsValid, Is.False);
            Assert.That(column.members, Is.EqualTo(2));
            _data.Header.SyncCustomBeanMembersCount();
            Assert.That(column.members, Is.EqualTo(3));
            Assert.That(_data.Header.Validate().IsValid, Is.True);
        }
        [Test] public void HeaderDetectsExpandedMemberColumnOverlap()
        {
            _data.Header.GetColumnByName("Speed").columnIndex = 4;
            Assert.That(_data.Header.Validate().Errors.Any(e => e.Contains("成员列重叠")), Is.True);
        }
        [Test] public void LubanConstraintPrefixDoesNotTurnScalarIntoList()
        {
            _data.Header.GetColumnByName("Speed").columnName = "*Speed";
            Assert.That(_data.Header.Validate().IsValid, Is.True);
            Assert.That(_data.Header.GetColumnByName("*Speed").IsList, Is.False);
        }
        [Test] public void RowEditingRetainsOtherMembersAndRows()
        {
            _data.Row.SetColumnSingleValue("*ActorCues", "999");
            Assert.That(_data.Row.GetColumnRows("*ActorCues")[0].values, Is.EqualTo(new[] { "999", "calm", "0,0,0" }));
            Assert.That(_data.Row.GetColumnRows("*ActorCues")[1].values[0], Is.EqualTo("102"));
            _data.Row.SetColumnRowValues("*ActorCues", new List<string> { "103", "calm", "2,0,0" }, 1);
            _data.Row.RemoveRowFromColumn("*ActorCues", 0);
            Assert.That(_data.Row.GetColumnRowCount("*ActorCues"), Is.EqualTo(1));
            Assert.That(_data.Row.Validate().IsValid, Is.True);
        }
        [Test] public void WrongBeanArityAndScalarMultipleRowsAreRejected()
        {
            _data.Row.GetColumnRows("*ActorCues")[0].values.RemoveAt(1);
            _data.Row.AddRowToColumn("ScenesID", new List<string> { "1002" });
            var errors = _data.Row.Validate().Errors;
            Assert.That(errors.Any(e => e.Contains("成员数错误")), Is.True);
            Assert.That(errors.Any(e => e.Contains("非列表列包含多行")), Is.True);
        }
        [TestCase("NaN", ValueType.Float)]
        [TestCase("Infinity", ValueType.Float)]
        [TestCase("2147483648", ValueType.Int)]
        [TestCase("maybe", ValueType.Bool)]
        [TestCase("1,2", ValueType.Vector3)]
        [TestCase("1,NaN,3", ValueType.Vector3)]
        public void InvalidTypedValuesAreRejected(string value, ValueType type)
        { Assert.That(AuthoringValueParser.IsValid(value, type), Is.False); }
        [Test] public void UnknownColumnsAreReportedWithoutDeletingTheirValues()
        {
            _data.Row.SetColumnSingleValue("FutureColumn", "keep me");
            Assert.That(_data.Row.Validate().IsValid, Is.False);
            Assert.That(_data.Row.GetColumnSingleValue("FutureColumn"), Is.EqualTo("keep me"));
        }
        [Test] public void NestedBeanIsExplicitlyUnboundRatherThanPretendingToConvert()
        {
            _data.Beans.eventTypes[0].parameters[0].valueType = ValueType.CustomBean;
            Assert.That(_data.Beans.Validate().Errors.Any(e => e.Contains("嵌套Bean")), Is.True);
        }
        [Test] public void MissingReadAndInvalidWriteDoNotCreateColumns()
        {
            var before = _data.Row.columns.Count;
            Assert.That(_data.Row.GetColumnRows("unknown"), Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => _data.Row.SetColumnRowValues("unknown", new List<string>(), -1));
            Assert.That(_data.Row.columns.Count, Is.EqualTo(before));
        }
        [Test] public void LanguageIndicesAndSeparateDialogConstantTablesArePreserved()
        {
            Assert.That((int)PlotDialogLanguageType.English, Is.EqualTo(2));
            Assert.That(_data.Dialog.GetLanguageCount("demo.start"), Is.EqualTo(5));
            Assert.That(_data.Dialog.GetDialogContent("demo.start", PlotDialogLanguageType.English), Does.StartWith("Welcome"));
            Assert.That(_data.Constant.HasKey("demo.start"), Is.False);
            Assert.That(_data.Dialog.GetDialogContent("demo.start", 99), Is.Empty);
            var snapshot = JsonUtility.ToJson(_data.Dialog);
            Assert.Throws<ArgumentOutOfRangeException>(() => _data.Dialog.SetDialogContent("demo.start", 99, "bad"));
            Assert.That(JsonUtility.ToJson(_data.Dialog), Is.EqualTo(snapshot));
        }
        [Test] public void DuplicateKeysAndDifferentLanguageCountsAreRejected()
        {
            _data.Dialog.dialogKeyDataList.Add(new PlotDialogKeyAsset.DialogKeyData { key = "demo.start", value = new List<string> { "duplicate" } });
            Assert.That(_data.Dialog.Validate().Errors.Any(e => e.Contains("重复")), Is.True);
            Assert.That(_data.Dialog.Validate().Errors.Any(e => e.Contains("语言数量不一致")), Is.True);
            _data.Constant.constantKeyDataList.Add(new PlotConstantKeyAsset.ConstantKeyData { key = "demo.title", value = new List<string>() });
            Assert.That(_data.Constant.ValidateData(), Is.False);
        }
        [Test] public void GenericTableRetainsStringIdsAndUsesInvariantNumericConversion()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                _data.Actors.SetColumnValue("007", "Name", "1.25");
                Assert.That(_data.Actors.GetColumnValue<float>("007", "Name"), Is.EqualTo(1.25f));
                Assert.That(_data.Actors.HasRow(7), Is.False);
                Assert.That(_data.Actors.HasRow("007"), Is.True);
                _data.Actors.SetRowData("007", new Dictionary<string, string> { { "Name", "旅人" }, { "Portrait", "demo.new" } });
                Assert.That(_data.Actors.GetRowById("007").columns.Count, Is.EqualTo(2));
                Assert.That(_data.Actors.Validate().IsValid, Is.True);
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test] public void NativeUnityAssetsReloadComplexColumnsReferencesAndLanguages()
        {
            _assetFolder = "Assets/PlotWeaveTest_" + Guid.NewGuid().ToString("N");
            _data.Save(_assetFolder);
            var rowPath = _assetFolder + "/Node_1001.asset";
            var textPath = _assetFolder + "/DialogKeys.asset";
            var constantPath = _assetFolder + "/ConstantKeys.asset";
            Resources.UnloadAsset(_data.Row); Resources.UnloadAsset(_data.Dialog); Resources.UnloadAsset(_data.Constant);
            var row = AssetDatabase.LoadAssetAtPath<PlotAtomRowData>(rowPath);
            var dialog = AssetDatabase.LoadAssetAtPath<PlotDialogKeyAsset>(textPath);
            var constant = AssetDatabase.LoadAssetAtPath<PlotConstantKeyAsset>(constantPath);
            Assert.That(row.Validate().Errors, Is.Empty);
            Assert.That(row.headerDefinition.parameterBean.parameterEnums, Is.SameAs(row.headerDefinition.parameterEnums));
            Assert.That(row.GetColumnRows("*ActorCues")[1].values, Is.EqualTo(new[] { "102", "happy", "1.25,0,-2" }));
            Assert.That(row.headerDefinition.parameterBean.eventTypes[0].defaultCallbackList, Is.EqualTo("4,6"));
            Assert.That(dialog.GetDialogContent("demo.start", PlotDialogLanguageType.English), Does.StartWith("Welcome"));
            Assert.That(constant.GetConstantContent("demo.title", 0), Is.EqualTo("织幕演示"));
            Assert.That(AssetDatabase.LoadAssetAtPath<PlotTableAsset>(_assetFolder + "/Actors.asset").GetColumnValue(101, "Name"), Is.EqualTo("引导员"));
        }
    }
}
