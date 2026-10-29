using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Legacy
{
    [Serializable] public sealed class PlotLegacyObjectReference { public string path; public UnityEngine.Object value; }
    [Serializable] public sealed class PlotLegacyObjectId { public string sourceId; public UnityEngine.Object value; }
    public sealed class PlotLegacyTemplateBindings : MonoBehaviour
    {
        public string sourceLayout;
        public List<PlotLegacyObjectId> objects = new List<PlotLegacyObjectId>();
        public List<PlotLegacyBehaviourProxy> behaviours = new List<PlotLegacyBehaviourProxy>();
        public IEnumerable<PlotLegacyBehaviourProxy> Scripts(string className) => behaviours.Where(b => b != null && b.sourceScript.EndsWith("/" + className + ".cs", StringComparison.Ordinal));
        public T ByName<T>(string name) where T : Component => GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
        private void Awake() => ConfigureFonts();
        public void ConfigureFonts()
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.font = PlotTMPText.DefaultFont;
            foreach (var input in GetComponentsInChildren<TMP_InputField>(true)) input.fontAsset = PlotTMPText.DefaultFont;
            var font = Resources.Load<Font>("PlotWeaveFonts/NotoSansCJKsc-Regular");
            if (font != null) foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
        }
    }
}
