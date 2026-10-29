using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Data;
using PlotWeave.Generation;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotContractGenerationTests
    {
        private PlotAuthoringExample _default;
        private PlotAuthoringExample _custom;
        private string _folder;
        [SetUp] public void SetUp()
        {
            _default = PlotGenerationExample.CreateDefault(); _custom = PlotGenerationExample.CreateEdgeCases();
            _folder = Path.Combine(Path.GetTempPath(), "PlotWeaveGeneration", Guid.NewGuid().ToString("N"));
        }
        [TearDown] public void TearDown()
        {
            foreach (var sample in new[] { _default, _custom }) foreach (var asset in sample.Assets) UnityEngine.Object.DestroyImmediate(asset);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }
        private static Type Generated(string dataset, string name)
        {
            var full = "PlotWeave.Generated.Stage02." + dataset + "." + name;
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(full, false)).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "请先执行迁移样例的双端数据契约生成，并等待Unity编译: " + full);
            return type;
        }
        private static object New(string dataset, string name, object data) => Activator.CreateInstance(Generated(dataset, name), new[] { data });
        private static object Get(object data, string name) => data.GetType().GetProperty(name).GetValue(data);
        private static List<object> Items(object value) => ((IEnumerable)value).Cast<object>().ToList();
        private static object Call(object data, string name, params object[] args) => data.GetType().GetMethod(name).Invoke(data, args);

        [Test] public void OriginalShapeGeneratesCommonInterfaceAndEquivalentTypedBeanLists()
        {
            var editor = New("Default", "EditorPlotAtomDataAdapter", _default.Row);
            var game = New("Default", "GamePlotAtomDataAdapter", new GameNode());
            Assert.That(Generated("Default", "IPlotAtomData").IsInstanceOfType(editor), Is.True);
            Assert.That(Generated("Default", "IPlotAtomData").IsInstanceOfType(game), Is.True);
            Assert.That(((IPlotDataBase)editor).DataSource, Is.EqualTo("Editor"));
            Assert.That(((IPlotDataBase)game).DataSource, Is.EqualTo("GameRuntime"));
            foreach (var name in new[] { "ScenesID", "PlotRoleID", "Dialog", "Next" }) Assert.That(Get(editor, name), Is.EqualTo(Get(game, name)));
            foreach (var source in new[] { editor, game })
            {
                var actions = Items(Get(source, "RoleAction")); Assert.That(actions.Count, Is.EqualTo(2));
                Assert.That(Get(actions[1], "RoleID"), Is.EqualTo(102));
                Assert.That(Get(actions[1], "ActionType").ToString(), Is.EqualTo("Shake"));
                Assert.That(Convert.ToInt32(Get(actions[1], "ActionType")), Is.EqualTo(1), "外部枚举的数值不是公共契约的绑定依据");
                Assert.That(Get(actions[1], "Emote").ToString(), Is.EqualTo("Happy"));
                Assert.That(Get(Items(Get(source, "PlotSetBack"))[0], "BackName"), Is.EqualTo("demo.stage"));
                var branches = Items(Get(source, "Branch")); Assert.That(branches.Count, Is.EqualTo(2));
                Assert.That(Get(branches[1], "Target"), Is.EqualTo(1003));
            }
        }
        [Test] public void CustomShapeSupportsFloatBoolVectorEnumsAndDifferentColumns()
        {
            var source = PlotAuthoringExample.Create();
            try
            {
                var editor = New("Custom", "EditorPlotAtomDataAdapter", source.Row);
                var game = New("Custom", "GamePlotAtomDataAdapter", new CustomNode());
                Assert.That(editor.GetType().GetProperty("RoleAction"), Is.Null);
                foreach (var node in new[] { editor, game })
                {
                    Assert.That(Get(node, "Speed"), Is.EqualTo(1.25f)); Assert.That(Get(node, "Enabled"), Is.True);
                    var cues = Items(Get(node, "ActorCues")); Assert.That(cues.Count, Is.EqualTo(2));
                    Assert.That(Get(cues[1], "position"), Is.EqualTo(new Vector3(1.25f, 0, -2)));
                    Assert.That(Get(cues[1], "mood").ToString(), Is.EqualTo("happy"));
                    Assert.That(Items(Get(node, "MoodHistory")).Select(x => x.ToString()), Is.EqualTo(new[] { "calm", "happy" }));
                }
            }
            finally { foreach (var asset in source.Assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void ScalarBeansAndConstraintPrefixedKeywordColumnsCompileAndRead()
        {
            var node = New("EdgeCases", "EditorPlotAtomDataAdapter", _custom.Row);
            Assert.That(Get(node, "class"), Is.EqualTo("keyword scalar"));
            Assert.That(Get(Get(node, "Cue"), "position"), Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(Get(Get(node, "Cue"), "mood").ToString(), Is.EqualTo("happy"));
        }
        [Test] public void EditorSnapshotAndReturnedListsDoNotChangeTheAuthoringAsset()
        {
            var node = New("Default", "EditorPlotAtomDataAdapter", _default.Row);
            _default.Row.GetColumnRows("*RoleAction")[0].values[0] = "999";
            Assert.That(Get(Items(Get(node, "RoleAction"))[0], "RoleID"), Is.EqualTo(101));
            ((IList)Get(node, "RoleAction")).Clear();
            Assert.That(Items(Get(node, "RoleAction")).Count, Is.EqualTo(2));
        }
        [Test] public void ActorTablesHaveTypedItemsAndSharedRoleContractForBothHosts()
        {
            var editor = New("Default", "EditorPlotThespianDataTableAdapter", _default.Actors);
            var game = New("Default", "GamePlotThespianDataTableAdapter", new[] { new GameActor { ID = 101, Name = "引导员", Thespian = "demo.guide" } });
            foreach (var table in new[] { editor, game })
            {
                var item = Call(table, "GetItem", 101);
                Assert.That(item, Is.InstanceOf<IPlotThespianData>());
                Assert.That(((IPlotThespianData)item).Thespian, Is.EqualTo("demo.guide"));
                Assert.That(Call(table, "HasId", 101), Is.True); Assert.That(Call(table, "GetItem", 999), Is.Null);
            }
        }
        [Test] public void ExplicitStringKeyTableRetainsLeadingZeroes()
        {
            var editor = New("EdgeCases", "EditorActorByStringTableAdapter", _custom.Actors);
            var game = New("EdgeCases", "GameActorByStringTableAdapter", new[] { new StringActor { ID = "007", Name = "字符串ID演员", Portrait = "demo.stringid" } });
            foreach (var table in new[] { editor, game })
            {
                Assert.That(Call(table, "HasId", "007"), Is.True); Assert.That(Call(table, "HasId", "7"), Is.False);
                Assert.That(Get(Call(table, "GetItem", "007"), "Name"), Is.EqualTo("字符串ID演员"));
            }
        }
        [Test] public void DialogAndConstantTablesKeepLanguagesAndImplementTextDataBridge()
        {
            var editor = New("Default", "EditorPlotDialogKeyTableAdapter", _default.Dialog.dialogKeyDataList);
            var game = New("Default", "GamePlotDialogKeyTableAdapter", new[] { new GameText { key = "demo.start", value = new List<string> { "中文", "", "English", "", "" } } });
            foreach (var table in new[] { editor, game })
            {
                var text = (IPlotTextData)table;
                Assert.That(text.HasKey("demo.start"), Is.True); Assert.That(text.GetAllKeys(), Does.Contain("demo.start"));
                Assert.That(Call(table, "GetValue", "demo.start", -1), Is.Null); Assert.That(Call(table, "GetValue", "demo.start", 99), Is.Null);
                Assert.That(text.GetText("demo.start", 2), Is.Not.Empty);
                var item = Call(table, "GetItem", "demo.start"); var values = (IList)Get(item, "value"); values.Clear();
                Assert.That(((IList)Get(item, "value")).Count, Is.EqualTo(5));
            }
            var constants = New("Default", "EditorPlotConstantKeyTableAdapter", _default.Constant.constantKeyDataList);
            Assert.That(((IPlotTextData)constants).HasKey("demo.start"), Is.False);
            Assert.That(((IPlotTextData)constants).GetText("demo.title", 0), Is.EqualTo("织幕演示"));
        }
        [Test] public void NonMultilingualKeyValueTableReadsScalarValuesForBothHosts()
        {
            var editor = New("EdgeCases", "EditorNumericSettingTableAdapter", new[] { new PlotTextEntry { key = "speed", value = new List<string> { "1.25" } } });
            var game = New("EdgeCases", "GameNumericSettingTableAdapter", new[] { new NumericSetting { key = "speed", value = 1.25f } });
            Assert.That(Call(editor, "GetValue", "speed"), Is.EqualTo(1.25f));
            Assert.That(Call(game, "GetValue", "speed"), Is.EqualTo(1.25f));
        }
        [Test] public void HostCanSupplyAValueSourceWithoutReflectingItsConfigObject()
        {
            var source = new DirectNodeSource();
            var adapter = New("Default", "GamePlotAtomDataAdapter", source);
            Assert.That(Get(adapter, "ScenesID"), Is.EqualTo(7001));
            Assert.That(source.Reads, Is.EqualTo(1));
        }
        [Test] public void TableCacheKeepsOrderAndDefensiveCollectionsAcrossUpdates()
        {
            var table = new PlotItemTable<string, DummyItem>();
            table.AddItem("b", new DummyItem()); table.AddItem("a", new DummyItem()); table.AddItem("b", new DummyItem());
            Assert.That(table.GetAllIds(), Is.EqualTo(new[] { "b", "a" }));
            table.GetAllIds().Clear(); table.GetAllItems().Clear(); Assert.That(table.GetAllItems().Count, Is.EqualTo(2));
            table.Clear(); Assert.That(table.HasId("a"), Is.False);
        }
        [Test] public void MalformedPresentValuesAreNotSilentlyConvertedToDefaults()
        {
            var source = new DictionaryPlotValueSource(new Dictionary<string, object> { { "number", "bad" } });
            Assert.Throws<FormatException>(() => PlotValueReader.Value<int>(source, "number"));
            Assert.Throws<KeyNotFoundException>(() => PlotValueReader.Required<int>(source, "missing"));
            Assert.Throws<FormatException>(() => PlotValueReader.ConvertValue<ForeignAction>(123));
            Assert.Throws<FormatException>(() => PlotValueReader.ConvertValue<ForeignAction>((ForeignAction)123));
        }
        [Test] public void NormalizedAndInfrastructureNamesCannotProduceBrokenCode()
        {
            _default.Header.sheets.Add(new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "*ScenesID", description = "conflict", valueType = PlotEventTypeAsset.ValueType.Int, columnIndex = 20 });
            Assert.Throws<FormatException>(() => PlotGenerationExample.Bundle(_default, "PlotWeave.Generated.Test"));
            Assert.Throws<FormatException>(() => PlotGenerationExample.Bundle(_custom, "Bad;Namespace"));
        }
        [Test] public void RegenerationRefusesToOverwriteManuallyEditedCode()
        {
            var bundle = PlotGenerationExample.Bundle(_default, "PlotWeave.Generated.Test"); bundle.Write(_folder);
            var path = Path.Combine(_folder, "EditorAdapters.g.cs"); File.AppendAllText(path, "\n// manually changed\n");
            var content = File.ReadAllText(path);
            Assert.Throws<IOException>(() => bundle.Write(_folder)); Assert.That(File.ReadAllText(path), Is.EqualTo(content));
        }
        [Test] public void RequestsAndResponsesRetainTheCommonDataObject()
        {
            var item = new DummyItem(); var request = new PlotDataRequest(4, "Node", null);
            Assert.That(request.Parameters, Is.Empty); Assert.That(PlotDataResponse.Success(item).Data, Is.SameAs(item));
            Assert.That(PlotDataResponse.Failure("missing").IsSuccess, Is.False);
        }
        private sealed class DummyItem : IPlotDataBase { public bool IsValid => true; public string DataSource => "Test"; }
        private sealed class DirectNodeSource : IPlotValueSource
        {
            public int Reads;
            public bool IsValid => true;
            public bool TryGetValue(string name, out object value) { Reads++; value = name == "ScenesID" ? (object)7001 : null; return value != null; }
        }
        public enum ForeignAction { Default = 90, Shake = 12 }
        public enum ForeignEmote { Neutral = 77, Happy = 88 }
        public enum ForeignBack { Default = 55 }
        public enum ForeignMood { calm = 50, happy = 99 }
        public sealed class GameAction { public int RoleID; public ForeignAction ActionType; public ForeignEmote Emote; public int RolePos; }
        public sealed class GameBack { public ForeignBack SetType; public string BackName; }
        public sealed class GameBranch { public string Key; public int Target; }
        public sealed class GameNode
        {
            public int ScenesID => 1001; public int PlotRoleID => 101; public string Dialog => "demo.start"; public int Next => 1002;
            public List<GameAction> RoleAction = new List<GameAction> { new GameAction { RoleID = 101, ActionType = ForeignAction.Default, Emote = ForeignEmote.Neutral }, new GameAction { RoleID = 102, ActionType = ForeignAction.Shake, Emote = ForeignEmote.Happy, RolePos = 2 } };
            public List<GameBack> PlotSetBack = new List<GameBack> { new GameBack { SetType = ForeignBack.Default, BackName = "demo.stage" } };
            public List<GameBranch> Branch = new List<GameBranch> { new GameBranch { Key = "demo.choice.forward", Target = 1002 }, new GameBranch { Key = "demo.choice.wait", Target = 1003 } };
        }
        public sealed class CustomCue { public int actorId; public ForeignMood mood; public Vector3 position; }
        public sealed class CustomNode
        {
            public float Speed = 1.25f; public bool Enabled = true;
            public List<CustomCue> ActorCues = new List<CustomCue> { new CustomCue { actorId = 101, mood = ForeignMood.calm }, new CustomCue { actorId = 102, mood = ForeignMood.happy, position = new Vector3(1.25f, 0, -2) } };
            public List<ForeignMood> MoodHistory = new List<ForeignMood> { ForeignMood.calm, ForeignMood.happy };
        }
        public sealed class GameActor { public int ID; public string Name; public string Thespian; }
        public sealed class StringActor { public string ID; public string Name; public string Portrait; }
        public sealed class GameText { public string key; public List<string> value; }
        public sealed class NumericSetting { public string key; public float value; }
    }
}
