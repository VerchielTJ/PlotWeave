using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Authoring
{
    /// <summary>新的类型与复杂数据样例，不使用原游戏的剧情和资源。</summary>
    public sealed class PlotAuthoringExample
    {
        public PlotEnumAsset Enums;
        public PlotEventTypeAsset Beans;
        public PlotAtomDataHeaderAsset Header;
        public PlotAtomRowData Row;
        public PlotDialogKeyAsset Dialog;
        public PlotConstantKeyAsset Constant;
        public PlotTableAsset Actors;
        public PlotAuthoringAsset[] Assets => new PlotAuthoringAsset[] { Enums, Beans, Header, Row, Dialog, Constant, Actors };

        public static PlotAuthoringExample Create()
        {
            var data = new PlotAuthoringExample
            {
                Enums = ScriptableObject.CreateInstance<PlotEnumAsset>(),
                Beans = ScriptableObject.CreateInstance<PlotEventTypeAsset>(),
                Header = ScriptableObject.CreateInstance<PlotAtomDataHeaderAsset>(),
                Row = ScriptableObject.CreateInstance<PlotAtomRowData>(),
                Dialog = ScriptableObject.CreateInstance<PlotDialogKeyAsset>(),
                Constant = ScriptableObject.CreateInstance<PlotConstantKeyAsset>(),
                Actors = ScriptableObject.CreateInstance<PlotTableAsset>()
            };
            data.Enums.name = "Types_Enums"; data.Beans.name = "Types_Beans"; data.Header.name = "NodeHeader";
            data.Row.name = "Node_1001"; data.Dialog.name = "DialogKeys"; data.Constant.name = "ConstantKeys"; data.Actors.name = "Actors";
            data.Enums.dynamicLists.Add(new PlotEnumAsset.ParameterEnum
            {
                enumName = "Mood", displayName = "心情",
                values = { new PlotEnumAsset.ParameterEnumValue { key = "calm", label = "平静" }, new PlotEnumAsset.ParameterEnumValue { key = "happy", label = "开心" } }
            });
            data.Beans.parameterEnums = data.Enums;
            data.Beans.eventTypes.Add(new PlotEventTypeAsset.EventTypeDefinition
            {
                eventKey = "ActorCue", displayName = "演员提示", defaultCallbackValue = 2, defaultCallbackList = "4,6",
                parameters =
                {
                    new PlotEventTypeAsset.EventParameter { paramKey = "actorId", displayName = "演员ID", valueType = ValueType.Int },
                    new PlotEventTypeAsset.EventParameter { paramKey = "mood", displayName = "心情", valueType = ValueType.CustomEnum, enumType = "Mood", enumValue = "calm" },
                    new PlotEventTypeAsset.EventParameter { paramKey = "position", displayName = "位置", valueType = ValueType.Vector3 }
                }
            });
            data.Header.parameterEnums = data.Enums; data.Header.parameterBean = data.Beans;
            data.Header.sheets.AddRange(new[]
            {
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "ScenesID", description = "节点ID", valueType = ValueType.Int, columnIndex = 1 },
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "Dialog", description = "对话KEY", valueType = ValueType.String, columnIndex = 2 },
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "*ActorCues", description = "演员提示列表", valueType = ValueType.CustomBean, typeKey = "ActorCue", IsList = true, columnIndex = 3, members = 3 },
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "*MoodHistory", description = "心情列表", valueType = ValueType.CustomEnum, typeKey = "Mood", IsList = true, columnIndex = 6 },
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "Speed", description = "速度", valueType = ValueType.Float, columnIndex = 7 },
                new PlotAtomDataHeaderAsset.ColumnDefinition { columnName = "Enabled", description = "启用", valueType = ValueType.Bool, columnIndex = 8 }
            });
            data.Row.headerDefinition = data.Header; data.Row.rowTitle = "1001";
            data.Row.SetColumnSingleValue("ScenesID", "1001"); data.Row.SetColumnSingleValue("Dialog", "demo.start");
            data.Row.AddRowToColumn("*ActorCues", new List<string> { "101", "calm", "0,0,0" });
            data.Row.AddRowToColumn("*ActorCues", new List<string> { "102", "happy", "1.25,0,-2" });
            data.Row.AddRowToColumn("*MoodHistory", new List<string> { "calm" }); data.Row.AddRowToColumn("*MoodHistory", new List<string> { "happy" });
            data.Row.SetColumnSingleValue("Speed", "1.25"); data.Row.SetColumnSingleValue("Enabled", "true");
            data.Dialog.AddDialogKey("demo.start"); data.Dialog.SetDialogContent("demo.start", PlotDialogLanguageType.SimpleChinese, "欢迎使用类型与数据迁移样例。");
            data.Dialog.SetDialogContent("demo.start", PlotDialogLanguageType.English, "Welcome to the authoring data example.");
            data.Constant.AddConstantKey("demo.title"); data.Constant.SetConstantContent("demo.title", 0, "织幕演示");
            data.Actors.tableName = "Actors"; data.Actors.customDataName = "ActorData"; data.Actors.customLubanName = "DemoActor"; data.Actors.tableDescription = "新建演员数据演示表";
            data.Actors.headers.AddRange(new[]
            {
                new PlotTableAsset.ColumnHeader { columnName = "Name", columnType = "string", description = "演员名", columnIndex = 1 },
                new PlotTableAsset.ColumnHeader { columnName = "Portrait", columnType = "string", description = "资源Key", columnIndex = 2 }
            });
            data.Actors.SetRowData(101, new Dictionary<string, string> { { "Name", "引导员" }, { "Portrait", "demo.guide" } });
            data.Actors.SetRowData(102, new Dictionary<string, string> { { "Name", "旅人" }, { "Portrait", "demo.traveler" } });
            return data;
        }

        public void Save(string folder)
        {
            var path = folder.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..")) throw new ArgumentException("样例应保存到Assets下的新目录");
            if (Directory.Exists(path)) throw new IOException("样例目录已存在，请使用新的目录。");
            foreach (var asset in Assets) asset.Validate().ThrowIfInvalid();
            Directory.CreateDirectory(path); AssetDatabase.Refresh();
            foreach (var asset in Assets) AssetDatabase.CreateAsset(asset, path + "/" + asset.name + ".asset");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/PlotWeave/迁移样例/创建类型与复杂数据样例")]
        public static void CreateSampleAssets()
        {
            var data = Create();
            try
            {
                const string parent = "Assets/PlotWeaveMigration";
                Directory.CreateDirectory(parent); AssetDatabase.Refresh();
                var path = AssetDatabase.GenerateUniqueAssetPath(parent + "/Stage01_Authoring");
                data.Save(path); Selection.activeObject = data.Row; EditorGUIUtility.PingObject(data.Row);
                Debug.Log("PlotWeave 类型与数据样例已创建: " + path);
            }
            finally { foreach (var asset in data.Assets) if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
