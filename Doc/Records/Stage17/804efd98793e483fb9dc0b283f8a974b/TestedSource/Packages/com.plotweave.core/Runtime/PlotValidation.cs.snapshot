using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlotWeave
{
    public sealed class PlotValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void ThrowIfInvalid()
        {
            if (!IsValid) throw new FormatException(string.Join("\n", Errors));
        }
    }

    public static class PlotValidation
    {
        public static PlotValidationResult Check(PlotDocument data)
        {
            var result = new PlotValidationResult();
            if (data == null) { result.Errors.Add("剧情为空。"); return result; }
            if (data.formatVersion != 1) result.Errors.Add("不支持的剧情格式版本。");
            if (data.nodes == null || data.actors == null || data.texts == null || data.variables == null || data.commands == null || data.enums == null)
            { result.Errors.Add("剧情集合不能为 null。"); return result; }
            Unique(data.nodes.Select(n => n?.id), "节点", result);
            Unique(data.actors.Select(a => a?.id), "角色", result);
            Unique(data.texts.Select(t => t?.key), "文本", result);
            Unique(data.variables.Select(v => v?.key), "变量", result);
            Unique(data.commands.Select(c => c?.key), "命令定义", result);
            Unique(data.enums.Select(e => e?.key), "枚举", result);
            if (string.IsNullOrWhiteSpace(data.id)) result.Errors.Add("剧情 ID 为空。");
            var nodeIds = new HashSet<string>(data.nodes.Where(n => n != null).Select(n => n.id));
            if (string.IsNullOrWhiteSpace(data.entryNodeId) || !nodeIds.Contains(data.entryNodeId)) result.Errors.Add("入口节点不存在。");
            foreach (var definition in data.commands.Where(c => c != null))
            {
                if (definition.parameters == null) { result.Errors.Add($"命令 {definition.key} 参数定义为空。"); continue; }
                Unique(definition.parameters.Select(p => p?.key), "命令参数 " + definition.key, result);
                foreach (var p in definition.parameters.Where(p => p != null))
                    if (p.type == PlotValueType.Enum && !data.enums.Any(e => e != null && e.key == p.enumKey)) result.Errors.Add($"命令 {definition.key} 的枚举 {p.enumKey} 不存在。");
            }
            foreach (var node in data.nodes.Where(n => n != null))
            {
                Target(node.nextNodeId, node.id, nodeIds, result);
                if (node.commands == null || node.choices == null) { result.Errors.Add($"节点 {node.id} 的列表为空。"); continue; }
                if (node.actorId != "" && !data.actors.Any(a => a != null && a.id == node.actorId)) result.Errors.Add($"节点 {node.id} 的角色 {node.actorId} 不存在。");
                TextKey(node.textKey, node.id, data, result);
                foreach (var choice in node.choices)
                {
                    if (choice == null) { result.Errors.Add($"节点 {node.id} 含空选项。"); continue; }
                    if (string.IsNullOrWhiteSpace(choice.targetNodeId)) result.Errors.Add($"节点 {node.id} 有未连接选项。");
                    Target(choice.targetNodeId, node.id, nodeIds, result);
                    TextKey(choice.textKey, node.id, data, result);
                }
                if (node.choices.Count > 0 && !string.IsNullOrEmpty(node.nextNodeId)) result.Warnings.Add($"节点 {node.id} 同时有分支和 Next，分支可用时优先分支。");
                foreach (var command in node.commands)
                {
                    if (command == null || string.IsNullOrWhiteSpace(command.key) || command.arguments == null) { result.Errors.Add($"节点 {node.id} 命令无效。"); continue; }
                    Unique(command.arguments.Select(a => a?.key), "命令实参 " + node.id, result);
                    var definition = data.commands.Find(c => c != null && c.key == command.key);
                    if (definition == null) continue; // Handler 可提供未声明的外部命令，运行时检查。
                    foreach (var p in definition.parameters.Where(p => p != null))
                    {
                        var value = command.Get(p.key, p.defaultValue);
                        if (p.required && string.IsNullOrEmpty(value)) result.Errors.Add($"节点 {node.id} 命令 {command.key} 缺少 {p.key}。");
                        else if (!string.IsNullOrEmpty(value) && !IsValueValid(p, value, data)) result.Errors.Add($"节点 {node.id} 参数 {p.key} 不符合 {p.type}。");
                    }
                }
            }
            if (result.IsValid)
            {
                var visited = new HashSet<string>();
                var pending = new Stack<string>(); pending.Push(data.entryNodeId);
                while (pending.Count > 0)
                {
                    var id = pending.Pop(); if (!visited.Add(id)) continue;
                    var n = data.FindNode(id);
                    if (!string.IsNullOrEmpty(n.nextNodeId)) pending.Push(n.nextNodeId);
                    foreach (var c in n.choices) pending.Push(c.targetNodeId);
                }
                foreach (var n in data.nodes) if (!visited.Contains(n.id)) result.Warnings.Add("不可达节点：" + n.id);
            }
            return result;
        }

        private static bool IsValueValid(PlotParameterDefinition p, string value, PlotDocument data)
        {
            switch (p.type)
            {
                case PlotValueType.Int: return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case PlotValueType.Float: return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f);
                case PlotValueType.Bool: return bool.TryParse(value, out _);
                case PlotValueType.Vector2: return Numbers(value, 2);
                case PlotValueType.Vector3: return Numbers(value, 3);
                case PlotValueType.Color: return UnityEngine.ColorUtility.TryParseHtmlString(value, out _);
                case PlotValueType.Enum: return data.enums.Any(e => e != null && e.key == p.enumKey && e.values != null && e.values.Contains(value));
                default: return true;
            }
        }
        private static bool Numbers(string value, int count)
        {
            var parts = value.Split(',');
            return parts.Length == count && parts.All(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f));
        }
        private static void Unique(IEnumerable<string> values, string scope, PlotValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) result.Errors.Add(scope + " 包含空或重复 ID：" + value);
        }
        private static void Target(string id, string owner, HashSet<string> ids, PlotValidationResult result)
        {
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) result.Errors.Add($"节点 {owner} 指向不存在的节点 {id}。");
        }
        private static void TextKey(string key, string owner, PlotDocument data, PlotValidationResult result)
        {
            if (!string.IsNullOrEmpty(key) && !data.texts.Any(t => t != null && t.key == key)) result.Errors.Add($"节点 {owner} 的文本 Key {key} 不存在。");
        }
    }
}
