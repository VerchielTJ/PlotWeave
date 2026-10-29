#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Authoring.UI
{
    public sealed class PlotNodeButton : MonoBehaviour
    {
        public int NodeId { get; private set; }
        public void Bind(int nodeId, string caption, bool selected, Action<int> action)
        {
            NodeId = nodeId;
            var button = GetComponent<Button>(); button.onClick.RemoveAllListeners();
            PlotWeave.Legacy.PlotLegacyWidgets.Caption(this,caption);
            var image=GetComponent<Image>();if(image!=null)image.color = selected ? PlotUiFactory.Accent : PlotUiFactory.Field;
            button.onClick.AddListener(() => action(NodeId));
        }
    }
}
#endif
