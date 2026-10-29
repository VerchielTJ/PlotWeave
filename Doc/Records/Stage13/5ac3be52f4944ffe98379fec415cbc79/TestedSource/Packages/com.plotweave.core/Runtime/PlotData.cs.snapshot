using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave
{
    [Serializable]
    public sealed class PlotDocument
    {
        public int formatVersion = 1;
        public string id = "story";
        public string title = "新剧情";
        public string entryNodeId = "";
        public List<PlotNode> nodes = new List<PlotNode>();
        public List<PlotActor> actors = new List<PlotActor>();
        public List<PlotText> texts = new List<PlotText>();
        public List<PlotVariable> variables = new List<PlotVariable>();
        public List<PlotCommandDefinition> commands = new List<PlotCommandDefinition>();
        public List<PlotEnumDefinition> enums = new List<PlotEnumDefinition>();

        public PlotDocument Clone() => JsonUtility.FromJson<PlotDocument>(JsonUtility.ToJson(this));
        public PlotNode FindNode(string nodeId) => nodes.Find(n => n != null && n.id == nodeId);
        public string ResolveText(string key, string fallback)
        {
            var item = texts.Find(t => t != null && t.key == key);
            return string.IsNullOrEmpty(key) || item == null ? fallback ?? "" : item.value ?? "";
        }
    }

    [Serializable]
    public sealed class PlotNode
    {
        public string id = "";
        public string title = "对话";
        public Vector2 position;
        public string sceneId = "";
        public string actorId = "";
        [TextArea(2, 8)] public string text = "";
        public string textKey = "";
        public string nextNodeId = "";
        public List<PlotChoice> choices = new List<PlotChoice>();
        public List<PlotCommand> commands = new List<PlotCommand>();
    }

    [Serializable]
    public sealed class PlotChoice
    {
        public string label = "选项";
        public string textKey = "";
        public string targetNodeId = "";
        public string conditionVariable = "";
        public string conditionValue = "true";
    }

    [Serializable]
    public sealed class PlotCommand
    {
        public string key = "signal";
        public List<PlotArgument> arguments = new List<PlotArgument>();
        public string Get(string name, string fallback = "")
        {
            var item = arguments.Find(a => a != null && a.key == name);
            return item == null ? fallback : item.value ?? "";
        }
        public PlotCommand With(string name, string value)
        {
            var item = arguments.Find(a => a != null && a.key == name);
            if (item == null) arguments.Add(new PlotArgument { key = name, value = value });
            else item.value = value;
            return this;
        }
    }

    [Serializable] public sealed class PlotArgument { public string key = ""; public string value = ""; }
    [Serializable] public sealed class PlotActor { public string id = ""; public string name = ""; public string resourceKey = ""; }
    [Serializable] public sealed class PlotText { public string key = ""; [TextArea] public string value = ""; }
    [Serializable] public sealed class PlotVariable { public string key = ""; public string value = ""; }
    public enum PlotValueType { String, Int, Float, Bool, Vector2, Vector3, Color, Enum, Json }
    [Serializable]
    public sealed class PlotParameterDefinition
    {
        public string key = "";
        public string displayName = "";
        public PlotValueType type;
        public string enumKey = "";
        public string defaultValue = "";
        public bool required;
    }
    [Serializable]
    public sealed class PlotCommandDefinition
    {
        public string key = "";
        public string displayName = "";
        public List<PlotParameterDefinition> parameters = new List<PlotParameterDefinition>();
    }
    [Serializable]
    public sealed class PlotEnumDefinition
    {
        public string key = "";
        public List<string> values = new List<string>();
    }

    public interface IPlotDataSource { PlotDocument Read(); }

    public sealed class JsonPlotDataSource : IPlotDataSource
    {
        private readonly string _json;
        public JsonPlotDataSource(string json) { _json = json; }
        public PlotDocument Read()
        {
            if (string.IsNullOrWhiteSpace(_json)) throw new ArgumentException("剧情 JSON 为空。");
            return JsonUtility.FromJson<PlotDocument>(_json) ?? throw new FormatException("无法读取剧情 JSON。");
        }
    }
}
