using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave.Legacy
{
    public enum PlotNodeType { Atom, Select }
    public enum PlotDataType { String, Int, Float, PlotNodeType, Bool, Vector2, Vector3, ListString, Color, Enum, InPortData, OutPortData, RoleInfo, RoleName, PlotEvent, RoleCountenance, BackgroundName, Object }
    public interface INodeDataControl { string GetData(string name); void SetData(string name, string value); }
    public interface INodeLifecycle { void NodeCreate(); void NodeDestroy(); }
    public interface INodeMoveable { void NodeMove(Vector2 delta); }
    /// <summary>旧节点协议的可用兼容实现；与主UGUI的PlotAtomRowData分别持有数据。</summary>
    public abstract class PlotNodeDataBase : ScriptableObject, INodeDataControl, INodeLifecycle, INodeMoveable
    {
        [Serializable] public sealed class Field { public string name = ""; public string value = ""; }
        [Serializable] public sealed class Member { public string name = ""; public List<Field> fields = new List<Field>(); }
        [Serializable] private sealed class Values { public List<string> values = new List<string>(); }
        public List<string> describe = new List<string>();
        public List<string> memberNames = new List<string>();
        public List<PlotDataType> plotDataType = new List<PlotDataType>();
        public List<string> plotData = new List<string>();
        public List<Member> members = new List<Member>();
        public string nodeId = "";
        public PlotNodeType nodeType;
        public Vector2 position;
        public bool IsCreated { get; private set; }
        public event Action Changed;
        public virtual T Init<T>(string id, Vector2 at) where T : PlotNodeDataBase
        { if (!(this is T typed)) throw new InvalidOperationException("Init类型与资产实际类型不一致"); if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("节点ID为空"); nodeId = id; position = at; NodeCreate(); return typed; }
        public virtual void NodeCreate() { IsCreated = true; Changed?.Invoke(); }
        public virtual void NodeDestroy() { IsCreated = false; Changed?.Invoke(); }
        public virtual void NodeMove(Vector2 delta) { position += delta; Changed?.Invoke(); }
        private int Index(string name)
        { var index = memberNames.IndexOf(name); if (index < 0) throw new KeyNotFoundException("未定义成员: " + name); if (index >= plotData.Count) throw new FormatException("成员名和值数量不一致"); return index; }
        public void Define(string name, PlotDataType type, string description = "")
        {
            if (string.IsNullOrWhiteSpace(name) || memberNames.Contains(name)) throw new ArgumentException("成员名为空或重复");
            memberNames.Add(name); plotDataType.Add(type); describe.Add(description); plotData.Add(""); Changed?.Invoke();
        }
        public virtual string GetData(string name) => plotData[Index(name)];
        public virtual void SetData(string name, string value) { plotData[Index(name)] = value ?? ""; Changed?.Invoke(); }
        public virtual string GetTypeStarID() => nodeType + "*" + nodeId;
        public virtual object GetMember(string name, string fieldName)
        { var member = members.Find(m => m.name == name); return member?.fields.Find(f => f.name == fieldName)?.value; }
        public void SetMember(string name, string fieldName, string value)
        {
            _ = Index(name); if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("字段名为空");
            var member = members.Find(m => m.name == name); if (member == null) { member = new Member { name = name }; members.Add(member); }
            var field = member.fields.Find(f => f.name == fieldName); if (field == null) { field = new Field { name = fieldName }; member.fields.Add(field); } field.value = value ?? ""; Changed?.Invoke();
        }
        private Values List(string name)
        {
            var index = Index(name); var type = plotDataType[index]; if (type != PlotDataType.ListString && type != PlotDataType.InPortData && type != PlotDataType.OutPortData) throw new InvalidOperationException("成员不是列表/端口: " + name);
            var text = plotData[index]; if (text == "") return new Values(); var values = JsonUtility.FromJson<Values>(text); if (values?.values == null) throw new FormatException("列表JSON无效: " + name); return values;
        }
        public virtual string GetCertain(string name, int index) { var values = List(name).values; if (index < 0 || index >= values.Count) throw new ArgumentOutOfRangeException(nameof(index)); return values[index]; }
        public virtual void AddPortData(string name, string value) { var values = List(name); if (!values.values.Contains(value)) values.values.Add(value); SetData(name, JsonUtility.ToJson(values)); }
        public virtual void DeletePortData(string name, string value) { var values = List(name); values.values.Remove(value); SetData(name, JsonUtility.ToJson(values)); }
        public virtual void CleanPortData(string name) { _ = List(name); SetData(name, JsonUtility.ToJson(new Values())); }
    }
}
