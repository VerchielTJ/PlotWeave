using System.Collections.Generic;
using System.IO;
using PlotWeave.Authoring;
using UnityEditor;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Generation
{
    public static class PlotGenerationExample
    {
        public const string GeneratedRoot = "Assets/PlotWeaveMigration/Stage02_Generated";
        public const string DefaultData = "Assets/PlotWeaveMigration/Stage02_DefaultData";
        public static PlotAuthoringExample CreateDefault()
        {
            var data = PlotAuthoringExample.Create();
            data.Enums.dynamicLists.Clear();
            data.Enums.dynamicLists.Add(Enum("RoleActionType", "角色动作", "Default", "Shake"));
            data.Enums.dynamicLists.Add(Enum("RoleEmote", "角色表情", "Neutral", "Happy"));
            data.Enums.dynamicLists.Add(Enum("SetBackType", "背景设置类型", "Default", "FadeIn"));
            data.Beans.eventTypes.Clear();
            data.Beans.eventTypes.Add(Bean("PlotRoleAction", "角色行为", Param("RoleID", ValueType.Int), Param("ActionType", ValueType.CustomEnum, "RoleActionType"), Param("Emote", ValueType.CustomEnum, "RoleEmote"), Param("RolePos", ValueType.Int)));
            data.Beans.eventTypes.Add(Bean("PlotSetBackground", "背景设置", Param("SetType", ValueType.CustomEnum, "SetBackType"), Param("BackName", ValueType.String)));
            data.Beans.eventTypes.Add(Bean("PlotBranch", "分支选项", Param("Key", ValueType.String), Param("Target", ValueType.Int)));
            data.Header.sheets.Clear();
            data.Header.sheets.AddRange(new[]
            {
                Column("ScenesID", ValueType.Int, 1), Column("PlotRoleID", ValueType.Int, 2), Column("Dialog", ValueType.String, 3), Column("Next", ValueType.Int, 4),
                Column("*RoleAction", ValueType.CustomBean, 5, "PlotRoleAction", true, 4),
                Column("*PlotSetBack", ValueType.CustomBean, 9, "PlotSetBackground", true, 2),
                Column("*Branch", ValueType.CustomBean, 11, "PlotBranch", true, 2)
            });
            data.Row.columns.Clear();
            data.Row.SetColumnSingleValue("ScenesID", "1001"); data.Row.SetColumnSingleValue("PlotRoleID", "101");
            data.Row.SetColumnSingleValue("Dialog", "demo.start"); data.Row.SetColumnSingleValue("Next", "1002");
            data.Row.AddRowToColumn("*RoleAction", new List<string> { "101", "Default", "Neutral", "0" });
            data.Row.AddRowToColumn("*RoleAction", new List<string> { "102", "Shake", "Happy", "2" });
            data.Row.AddRowToColumn("*PlotSetBack", new List<string> { "Default", "demo.stage" });
            data.Row.AddRowToColumn("*Branch", new List<string> { "demo.choice.forward", "1002" });
            data.Row.AddRowToColumn("*Branch", new List<string> { "demo.choice.wait", "1003" });
            data.Actors.tableName = "PlotThespianData"; data.Actors.customDataName = "PlotThespianData"; data.Actors.customLubanName = "PlotThespianData";
            data.Actors.headers[1].columnName = "Thespian"; data.Actors.rows.Clear();
            data.Actors.SetRowData(101, new Dictionary<string, string> { { "Name", "引导员" }, { "Thespian", "demo.guide" } });
            data.Actors.SetRowData(102, new Dictionary<string, string> { { "Name", "旅人" }, { "Thespian", "demo.traveler" } });
            return data;
        }
        private static PlotEnumAsset.ParameterEnum Enum(string name, string display, params string[] values)
        {
            var result = new PlotEnumAsset.ParameterEnum { enumName = name, displayName = display };
            foreach (var value in values) result.values.Add(new PlotEnumAsset.ParameterEnumValue { key = value, label = value });
            return result;
        }
        private static PlotEventTypeAsset.EventParameter Param(string name, ValueType type, string enumName = "") => new PlotEventTypeAsset.EventParameter { paramKey = name, displayName = name, valueType = type, enumType = enumName };
        private static PlotEventTypeAsset.EventTypeDefinition Bean(string key, string display, params PlotEventTypeAsset.EventParameter[] parameters) => new PlotEventTypeAsset.EventTypeDefinition { eventKey = key, displayName = display, parameters = new List<PlotEventTypeAsset.EventParameter>(parameters) };
        private static PlotAtomDataHeaderAsset.ColumnDefinition Column(string name, ValueType type, int index, string typeKey = "", bool list = false, int members = 1) => new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = name, description = name, valueType = type, columnIndex = index, typeKey = typeKey, IsList = list, members = members };
        public static PlotGeneratedBundle Bundle(PlotAuthoringExample data, string ns) => PlotDataStructureGenerator.GenerateAllDataStructures(data.Header,
            new[] { new PlotTableGeneration { Asset = data.Actors, IsThespianTable = data.Actors.tableName == "PlotThespianData" } },
            new[] { new PlotKeyValueGeneration { Name = "PlotDialogKey" }, new PlotKeyValueGeneration { Name = "PlotConstantKey" } }, ns);

        [MenuItem("Tools/PlotWeave/迁移样例/生成双端数据契约验证样例")]
        public static void PrepareVerification()
        {
            var originalShape = CreateDefault();
            var customShape = PlotAuthoringExample.Create();
            try
            {
                // 同时输出原节点形状和自定义列形状，证明生成器不是固定7字段模板。
                if (!Directory.Exists(DefaultData)) originalShape.Save(DefaultData);
                Bundle(originalShape, "PlotWeave.Generated.Stage02.Default").Write(GeneratedRoot + "/Default");
                Bundle(customShape, "PlotWeave.Generated.Stage02.Custom").Write(GeneratedRoot + "/Custom");
                PrepareEdgeCases();
                AssetDatabase.Refresh();
                var profilePath = DefaultData + "/GenerationProfile.asset";
                if (!File.Exists(profilePath))
                {
                    var profile = ScriptableObject.CreateInstance<PlotGenerationProfile>();
                    profile.header = AssetDatabase.LoadAssetAtPath<PlotAtomDataHeaderAsset>(DefaultData + "/NodeHeader.asset");
                    profile.targetNamespace = "PlotWeave.Generated.Stage02.Default";
                    profile.outputFolder = GeneratedRoot + "/Default";
                    profile.tables.Add(new PlotTableGeneration { Asset = AssetDatabase.LoadAssetAtPath<PlotTableAsset>(DefaultData + "/Actors.asset"), IsThespianTable = true });
                    profile.texts.Add(new PlotKeyValueGeneration { Name = "PlotDialogKey" });
                    profile.texts.Add(new PlotKeyValueGeneration { Name = "PlotConstantKey" });
                    AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssets();
                }
                Debug.Log("PLOTWEAVE_STAGE02_GENERATED " + GeneratedRoot);
            }
            finally
            {
                foreach (var data in new[] { originalShape, customShape })
                    foreach (var asset in data.Assets) if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }
        public static PlotAuthoringExample CreateEdgeCases()
        {
            var data = PlotAuthoringExample.Create();
            data.Header.sheets.Add(Column("Cue", ValueType.CustomBean, 9, "ActorCue", false, 3));
            data.Header.sheets.Add(Column("*class", ValueType.String, 12));
            data.Row.SetColumnRowValues("Cue", new List<string> { "101", "happy", "(2,3,4)" });
            data.Row.SetColumnSingleValue("*class", "keyword scalar");
            data.Actors.SetRowData("007", new Dictionary<string, string> { { "Name", "字符串ID演员" }, { "Portrait", "demo.stringid" } });
            return data;
        }
        private static void PrepareEdgeCases()
        {
            var data = CreateEdgeCases();
            try
            {
                PlotDataStructureGenerator.GenerateAllDataStructures(data.Header,
                    new[] { new PlotTableGeneration { Asset = data.Actors, Name = "ActorByString", KeyType = "string" } },
                    new[] { new PlotKeyValueGeneration { Name = "NumericSetting", ValueType = "float", IsMultiLanguage = false } },
                    "PlotWeave.Generated.Stage02.EdgeCases").Write(GeneratedRoot + "/EdgeCases");
            }
            finally { foreach (var asset in data.Assets) Object.DestroyImmediate(asset); }
        }
    }
}
