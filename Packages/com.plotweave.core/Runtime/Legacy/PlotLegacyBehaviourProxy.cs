using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave.Legacy
{
    /// <summary>保留旧组件字段与引用，由明确的行为适配器接管，不加载旧程序集。</summary>
    public sealed class PlotLegacyBehaviourProxy : MonoBehaviour
    {
        public string sourceScript;
        [TextArea] public string valuesJson;
        public List<PlotLegacyObjectReference> references = new List<PlotLegacyObjectReference>();
        public T Field<T>(string path) where T : UnityEngine.Object
        {
            var value = references.Find(r => r.path == path)?.value; if (value is T exact) return exact;
            var go = value is GameObject obj ? obj : value is Component component ? component.gameObject : null;
            return go != null && typeof(Component).IsAssignableFrom(typeof(T)) ? go.GetComponent(typeof(T)) as T : go as T;
        }
    }
}
