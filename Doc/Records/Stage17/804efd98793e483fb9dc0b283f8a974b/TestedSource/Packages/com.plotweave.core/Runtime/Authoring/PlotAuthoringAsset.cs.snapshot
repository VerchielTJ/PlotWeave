using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave.Authoring
{
    public abstract class PlotAuthoringAsset : ScriptableObject
    {
        public abstract AuthoringValidationResult Validate();
        public bool CheckAll() => Validate().IsValid;
    }

    /// <summary>校验只报告问题，不修改配置；修复操作由使用者显式触发。</summary>
    public sealed class AuthoringValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void Require(bool condition, string message) { if (!condition) Errors.Add(message); }
        public void Include(string prefix, AuthoringValidationResult other)
        { foreach (var error in other.Errors) Errors.Add(prefix + ": " + error); }
        public void ThrowIfInvalid() { if (!IsValid) throw new FormatException(string.Join("\n", Errors)); }
        public override string ToString() => IsValid ? "校验通过" : string.Join("\n", Errors);
        internal void Unique(HashSet<string> seen, string key, string location)
        {
            if (string.IsNullOrWhiteSpace(key)) Errors.Add(location + "不能为空");
            else if (!seen.Add(key)) Errors.Add(location + "重复: " + key);
        }
    }

    // 顺序对应原语言数据列表，不能通过重新排序改变存量语言索引。
    public enum PlotDialogLanguageType { SimpleChinese = 0, TraditionalChinese = 1, English = 2, Japanese = 3, Korean = 4 }
}
