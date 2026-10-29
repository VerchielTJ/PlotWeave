using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/类型/Bean与事件类型")]
    public sealed class PlotEventTypeAsset : PlotAuthoringAsset
    {
        // 保留原枚举值的数字，避免类型定义序列化后发生错位。
        public enum ValueType { Int = 0, Float = 1, String = 2, Bool = 3, Vector3 = 4, CustomEnum = 5, CustomBean = 6 }
        [Serializable] public sealed class EventParameter
        {
            public string paramSign = "param";
            public string paramKey = "";
            public string displayName = "";
            public ValueType valueType = ValueType.String;
            public string enumType = "";
            public string enumValue = "";
            public string beanType = "";
        }
        [Serializable] public sealed class EventTypeDefinition
        {
            public string eventKey = "";
            public string displayName = "";
            public List<EventParameter> parameters = new List<EventParameter>();
            public int defaultCallbackValue = -1;
            public string defaultCallbackList = "";
        }
        public PlotEnumAsset parameterEnums;
        public List<EventTypeDefinition> eventTypes = new List<EventTypeDefinition>();
        public Color parameterPanelColor = Color.white;
        public Color eventPanelColor = Color.white;
        public EventTypeDefinition GetEventTypeByKey(string eventKey) => eventTypes?.Find(x => x != null && x.eventKey == eventKey);
        public string[] GetAllEventKeys() => eventTypes?.Where(x => x != null).Select(x => x.eventKey).ToArray() ?? Array.Empty<string>();
        public string[] GetAllEventDisplayNames() => eventTypes?.Where(x => x != null).Select(x => x.displayName).ToArray() ?? Array.Empty<string>();
        public int GetEventParameterCount(string eventKey) => GetEventTypeByKey(eventKey)?.parameters?.Count ?? 0;
        public string[] GetEventParameterKeys(string eventKey) => GetEventTypeByKey(eventKey)?.parameters?.Where(x => x != null).Select(x => x.paramKey).ToArray() ?? Array.Empty<string>();
        private EventParameter Find(string eventKey, string key) => GetEventTypeByKey(eventKey)?.parameters?.Find(p => p != null && p.paramKey == key);
        public ValueType GetEventParameterType(string eventKey, string paramKey) => Find(eventKey, paramKey)?.valueType ?? ValueType.String;
        public string GetEventParameterEnumName(string eventKey, string paramKey) => Find(eventKey, paramKey)?.enumType ?? "";
        public Dictionary<string, string> GetEventParameterDisplayAndKeyNameMap(string eventKey) => ParameterMap(eventKey, true);
        public Dictionary<string, string> GetEventParameterKeyAndDisplayNameMap(string eventKey) => ParameterMap(eventKey, false);
        private Dictionary<string, string> ParameterMap(string eventKey, bool reverse)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var parameters = GetEventTypeByKey(eventKey)?.parameters;
            if (parameters != null) foreach (var p in parameters) if (p != null) result[reverse ? p.displayName : p.paramKey] = reverse ? p.paramKey : p.displayName;
            return result;
        }
        public void CheckAndFixInvalidEnumNames()
        {
            if (eventTypes == null) return;
            foreach (var bean in eventTypes.Where(x => x?.parameters != null))
                foreach (var p in bean.parameters.Where(x => x != null && x.valueType == ValueType.CustomEnum))
                {
                    var keys = parameterEnums?.GetEnumKeys(p.enumType) ?? Array.Empty<string>();
                    if (!keys.Contains(p.enumValue)) p.enumValue = keys.FirstOrDefault() ?? "";
                }
        }
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult();
            if (eventTypes == null) { result.Errors.Add("Bean列表为空引用"); return result; }
            if (parameterEnums != null) result.Include("枚举定义", parameterEnums.Validate());
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bean in eventTypes)
            {
                if (bean == null) { result.Errors.Add("Bean为空引用"); continue; }
                result.Unique(keys, bean.eventKey, "Bean键");
                result.Unique(labels, bean.displayName, "Bean显示名");
                if (bean.parameters == null) { result.Errors.Add(bean.eventKey + "参数列表为空引用"); continue; }
                var parameters = new HashSet<string>(StringComparer.Ordinal);
                var display = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in bean.parameters)
                {
                    if (p == null) { result.Errors.Add(bean.eventKey + "包含空参数"); continue; }
                    var location = bean.eventKey + "." + p.paramKey;
                    result.Unique(parameters, p.paramKey, bean.eventKey + "参数键");
                    result.Unique(display, p.displayName, bean.eventKey + "参数显示名");
                    result.Require(Enum.IsDefined(typeof(ValueType), p.valueType), location + "类型无效");
                    if (p.valueType == ValueType.CustomEnum)
                    {
                        var definition = parameterEnums?.GetEnum(p.enumType);
                        result.Require(definition != null, location + "引用不存在的枚举: " + p.enumType);
                        if (definition != null && !string.IsNullOrEmpty(p.enumValue))
                            result.Require(parameterEnums.GetEnumKeys(p.enumType).Contains(p.enumValue), location + "默认枚举值不存在: " + p.enumValue);
                    }
                    if (p.valueType == ValueType.CustomBean) result.Require(GetEventTypeByKey(p.beanType) != null, location + "引用不存在的嵌套Bean: " + p.beanType);
                }
            }
            foreach (var bean in eventTypes.Where(b => b != null)) try { PlotBeanLayout.Leaves(this, bean.eventKey); } catch (Exception error) { result.Errors.Add(error.Message); }
            return result;
        }
    }
}
