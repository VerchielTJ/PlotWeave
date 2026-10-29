using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/类型/动态枚举")]
    public sealed class PlotEnumAsset : PlotAuthoringAsset
    {
        [Serializable] public sealed class ParameterEnum
        {
            public string enumName = "";
            public string displayName = "";
            public List<ParameterEnumValue> values = new List<ParameterEnumValue>();
        }
        [Serializable] public sealed class ParameterEnumValue { public string key = ""; public string label = ""; }
        public List<ParameterEnum> dynamicLists = new List<ParameterEnum>();
        public Color enumPanelColor = Color.white;
        public Color ValuePanelColor = Color.white;
        public ParameterEnum GetEnum(string enumName) => dynamicLists?.Find(x => x != null && x.enumName == enumName);
        public string GetDisplayName(string enumName, string key) => GetEnum(enumName)?.values?.Find(x => x != null && x.key == key)?.label ?? key;
        public string[] GetEnumKeys(string enumName) => GetEnum(enumName)?.values?.Where(x => x != null).Select(x => x.key).ToArray() ?? Array.Empty<string>();
        public string[] GetEnumLabels(string enumName) => GetEnum(enumName)?.values?.Where(x => x != null).Select(x => x.label).ToArray() ?? Array.Empty<string>();
        public string[] GetAllEnumNames() => dynamicLists?.Where(x => x != null).Select(x => x.enumName).ToArray() ?? Array.Empty<string>();
        public string[] GetAllEnumDisplayNames() => dynamicLists?.Where(x => x != null).Select(x => x.displayName).ToArray() ?? Array.Empty<string>();
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult();
            if (dynamicLists == null) { result.Errors.Add("枚举列表为空引用"); return result; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            var displays = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in dynamicLists)
            {
                if (item == null) { result.Errors.Add("枚举定义为空引用"); continue; }
                result.Unique(names, item.enumName, "枚举名");
                result.Unique(displays, item.displayName, "枚举显示名");
                if (item.values == null) { result.Errors.Add(item.enumName + "的值列表为空引用"); continue; }
                var keys = new HashSet<string>(StringComparer.Ordinal);
                var labels = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in item.values)
                    if (value == null) result.Errors.Add(item.enumName + "包含空枚举值");
                    else
                    {
                        result.Unique(keys, value.key, item.enumName + "枚举键");
                        if (!string.IsNullOrEmpty(value.label) && !labels.Add(value.label)) result.Errors.Add(item.enumName + "枚举显示名重复: " + value.label);
                    }
            }
            return result;
        }
    }
}
