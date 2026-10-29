using UnityEngine;

namespace PlotWeave
{
    /// <summary>全部为演示用新建数据，不包含原项目剧情、美术或音频。</summary>
    public static class PlotSample
    {
        public static PlotDocument Create()
        {
            var data = new PlotDocument { id = "plotweave_demo", title = "织幕演示 从资产到表格", entryNodeId = "welcome" };
            data.actors.Add(new PlotActor { id = "guide", name = "向导", resourceKey = "demo_guide" });
            data.variables.Add(new PlotVariable { key = "player", value = "旅人" });
            data.texts.Add(new PlotText { key = "welcome.line", value = "你好，${player}。这段剧情可以直接预览，也可以导成表格再编回 JSON。" });
            var first = new PlotNode { id = "welcome", title = "欢迎", actorId = "guide", textKey = "welcome.line", position = new Vector2(80, 100), nextNodeId = "route" };
            first.commands.Add(new PlotCommand { key = "background" }.With("key", "demo_evening"));
            first.commands.Add(new PlotCommand { key = "actor.show" }.With("id", "guide").With("position", "0.5"));
            var choice = new PlotNode { id = "route", title = "选择路线", actorId = "guide", text = "你想先探索哪部分？", position = new Vector2(400, 100) };
            choice.choices.Add(new PlotChoice { label = "查看剧情事件", targetNodeId = "events" });
            choice.choices.Add(new PlotChoice { label = "直接结束", targetNodeId = "end" });
            var events = new PlotNode { id = "events", title = "事件与变量", actorId = "guide", text = "${player}，事件、文本和分支仍然由同一套播放核心处理。", position = new Vector2(730, 60), nextNodeId = "end" };
            events.commands.Add(new PlotCommand { key = "set" }.With("name", "player").With("value", "探索者"));
            events.commands.Add(new PlotCommand { key = "wait" }.With("seconds", "0.3"));
            events.commands.Add(new PlotCommand { key = "actor.emote" }.With("id", "guide").With("emote", "微笑"));
            events.commands.Add(new PlotCommand { key = "signal" }.With("name", "demo.visited"));
            var end = new PlotNode { id = "end", title = "结束", text = "演示结束。你可以修改节点、连线和表格，验证自己的工作流。", position = new Vector2(1060, 180) };
            data.nodes.AddRange(new[] { first, choice, events, end });
            Define(data, "wait", "等待", new PlotParameterDefinition { key = "seconds", displayName = "秒数", type = PlotValueType.Float, defaultValue = "0", required = true });
            Define(data, "set", "设置变量", new PlotParameterDefinition { key = "name", type = PlotValueType.String, required = true }, new PlotParameterDefinition { key = "value", type = PlotValueType.String });
            Define(data, "signal", "游戏事件", new PlotParameterDefinition { key = "name", type = PlotValueType.String, required = true });
            return data;
        }
        private static void Define(PlotDocument data, string key, string label, params PlotParameterDefinition[] parameters)
        {
            var definition = new PlotCommandDefinition { key = key, displayName = label };
            definition.parameters.AddRange(parameters); data.commands.Add(definition);
        }
    }
}
