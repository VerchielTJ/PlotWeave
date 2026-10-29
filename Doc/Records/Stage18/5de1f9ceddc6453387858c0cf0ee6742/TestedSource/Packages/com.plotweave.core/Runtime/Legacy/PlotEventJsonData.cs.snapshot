using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PlotWeave.Authoring;
using UnityEngine;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Legacy
{
    /// <summary>原独立JSON事件数据的可移植载体，运行时由PlotEventScheduler执行。</summary>
    [Serializable] public sealed class PlotEventJsonData
    {
        public string eventId = Guid.NewGuid().ToString();
        public string eventName = "";
        public List<string> parameters = new List<string>();
        public int executionOrder = -1;
        public string description = "";
        public long createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string callbackList = "";
        public PlotEventJsonData() { }
        public PlotEventJsonData(string eventType, int callbackValue = -1, string description = "")
        { eventName = eventType; executionOrder = callbackValue; this.description = description; }
        public string GetParameter(int index, string defaultValue = "") => parameters != null && index >= 0 && index < parameters.Count ? parameters[index] : defaultValue;
        public void SetParameter(int index, string value)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (parameters == null) parameters = new List<string>();
            while (parameters.Count <= index) parameters.Add(""); parameters[index] = value ?? "";
        }
        public void AddParameter(string value) { if (parameters == null) parameters = new List<string>(); parameters.Add(value ?? ""); }
        public void AddParameters(params string[] values) { if (values != null) foreach (var value in values) AddParameter(value); }
        public bool RemoveParameter(int index)
        { if (parameters == null || index < 0 || index >= parameters.Count) return false; parameters.RemoveAt(index); return true; }
        public List<int> GetCallbackIds()
        {
            var result = new List<int>();
            foreach (var part in (callbackList ?? "").Split(',')) if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) result.Add(id);
            return result;
        }
        public void SetCallbackIds(List<int> ids) => callbackList = ids == null ? "" : string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        public void AddCallbackId(int id) { var ids = GetCallbackIds(); if (!ids.Contains(id)) { ids.Add(id); SetCallbackIds(ids); } }
        public bool RemoveCallbackId(int id) { var ids = GetCallbackIds(); var removed = ids.Remove(id); if (removed) SetCallbackIds(ids); return removed; }
        public void ClearCallbackIds() => callbackList = "";
        public void InitFromEventType(PlotEventTypeAsset.EventTypeDefinition definition) => InitFromEventType(definition, null);
        public void InitFromEventType(PlotEventTypeAsset.EventTypeDefinition definition, PlotEventTypeAsset schema)
        {
            if (definition == null) return;
            if (definition.parameters == null || definition.parameters.Any(p => p == null || (p.valueType == ValueType.CustomBean && schema == null))) throw new NotSupportedException("事件参数为空或嵌套Bean缺少类型绑定资产");
            var values = schema != null ? PlotBeanLayout.Defaults(schema, definition.eventKey) : definition.parameters.Select(p => p.valueType == ValueType.Int ? "0" : p.valueType == ValueType.Float ? "0.0" : p.valueType == ValueType.Bool ? "false" : p.valueType == ValueType.Vector3 ? "0,0,0" : p.valueType == ValueType.CustomEnum ? p.enumValue ?? "" : "").ToList();
            eventName = definition.eventKey; executionOrder = definition.defaultCallbackValue; callbackList = definition.defaultCallbackList ?? ""; parameters = values;
        }
        public PlotEventJsonData Clone() => new PlotEventJsonData(eventName, executionOrder, description) { eventId = eventId, createdAt = createdAt, callbackList = callbackList, parameters = parameters == null ? null : new List<string>(parameters) };
        public override string ToString() => eventName + " (order=" + executionOrder + ", parameters=" + (parameters?.Count ?? 0) + ")";
    }

    [Serializable] public sealed class PlotEventsJsonContainer
    {
        public List<PlotEventJsonData> events = new List<PlotEventJsonData>();
        public void AddEvent(PlotEventJsonData data) { if (data == null) throw new ArgumentNullException(nameof(data)); events.Add(data); }
        public void RemoveEvent(PlotEventJsonData data) => events.Remove(data);
        public int GetConut() => events.Count;
        public int Count => events.Count;
        public PlotEventJsonData FindEventById(string id) => events.Find(e => e != null && e.eventId == id);
        public List<PlotEventJsonData> FindEventsByName(string name) => events.FindAll(e => e != null && e.eventName == name);
        public void SortByExecutionOrder() => events = events.OrderBy(e => e.executionOrder).ToList();
        public AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult(); var ids = new HashSet<string>(StringComparer.Ordinal);
            if (events == null) { result.Errors.Add("事件列表为空"); return result; }
            foreach (var data in events)
            {
                if (data == null) { result.Errors.Add("事件记录为空"); continue; }
                if (string.IsNullOrWhiteSpace(data.eventId) || !ids.Add(data.eventId)) result.Errors.Add("事件ID为空或重复");
                if (string.IsNullOrWhiteSpace(data.eventName) || data.parameters == null || data.executionOrder < -1) result.Errors.Add("事件类型、参数或顺序无效: " + data.eventId);
                foreach (var part in (data.callbackList ?? "").Split(',')) if (!string.IsNullOrWhiteSpace(part) && !int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) result.Errors.Add("回调ID不是整数: " + part);
            }
            return result;
        }
        public string ToJson() { Validate().ThrowIfInvalid(); return JsonUtility.ToJson(this, true); }
        public static PlotEventsJsonContainer FromJson(string json)
        {
            PlotEventsJsonContainer result;
            try { result = JsonUtility.FromJson<PlotEventsJsonContainer>(json); }
            catch (ArgumentException error) { throw new FormatException("事件JSON无效", error); }
            if (result == null) throw new FormatException("事件JSON为空"); result.Validate().ThrowIfInvalid(); return result;
        }
    }
}
