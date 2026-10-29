using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Data;

namespace PlotWeave.Authoring
{
    public sealed class PlotBeanLeaf
    { public string Path, Label; public PlotEventTypeAsset.EventParameter Parameter; }
    /// <summary>有界嵌套Bean在作者数据和Excel中按叶成员展开，在适配器中恢复类型结构。</summary>
    public static class PlotBeanLayout
    {
        public static List<string> Defaults(PlotEventTypeAsset schema, string beanKey)
        {
            return Leaves(schema, beanKey).Select(leaf =>
            { var p = leaf.Parameter; switch (p.valueType) { case PlotEventTypeAsset.ValueType.Int: case PlotEventTypeAsset.ValueType.Float: return "0"; case PlotEventTypeAsset.ValueType.Bool: return "false"; case PlotEventTypeAsset.ValueType.Vector3: return "0,0,0"; case PlotEventTypeAsset.ValueType.CustomEnum: return string.IsNullOrEmpty(p.enumValue) ? schema.parameterEnums?.GetEnumKeys(p.enumType).FirstOrDefault() ?? "" : p.enumValue; default: return ""; } }).ToList();
        }
        public static List<PlotBeanLeaf> Leaves(PlotEventTypeAsset schema, string beanKey)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema)); var result = new List<PlotBeanLeaf>(); var stack = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key, string path, string label, int depth)
            {
                if (depth > 64 || !stack.Add(key)) throw new FormatException("Bean包含循环类型或嵌套超过64层: " + key);
                var bean = schema.GetEventTypeByKey(key) ?? throw new FormatException("嵌套Bean类型不存在: " + key);
                if (bean.parameters == null) throw new FormatException("Bean参数列表为空: " + key);
                foreach (var p in bean.parameters)
                {
                    if (p == null) throw new FormatException("Bean参数为空: " + key); var member = path + p.paramKey; var display = label + (string.IsNullOrEmpty(p.displayName) ? p.paramKey : p.displayName);
                    if (p.valueType == PlotEventTypeAsset.ValueType.CustomBean) Visit(p.beanType, member + ".", display + "/", depth + 1);
                    else { result.Add(new PlotBeanLeaf { Path = member, Label = display, Parameter = p }); if (result.Count > 16384) throw new FormatException("Bean展开成员超过16384列"); }
                }
                stack.Remove(key);
            }
            Visit(beanKey, "", "", 0); return result;
        }
        public static IPlotValueSource Read(PlotEventTypeAsset schema, string beanKey, IList<string> values, int offset = 0)
        {
            var leaves = Leaves(schema, beanKey); if (values == null || offset < 0 || values.Count - offset < leaves.Count) throw new FormatException("嵌套Bean成员值不足: " + beanKey);
            var cursor = offset;
            foreach (var leaf in leaves)
            { var p = leaf.Parameter; if (!AuthoringValueParser.IsValid(values[cursor++], p.valueType, schema.parameterEnums, p.enumType)) throw new FormatException("嵌套Bean成员类型无效: " + beanKey + "." + leaf.Path); }
            cursor = offset;
            IPlotValueSource Build(string key)
            {
                var data = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var p in schema.GetEventTypeByKey(key).parameters) data.Add(p.paramKey, p.valueType == PlotEventTypeAsset.ValueType.CustomBean ? (object)Build(p.beanType) : values[cursor++]);
                return new DictionaryPlotValueSource(data);
            }
            return Build(beanKey);
        }
    }
}
