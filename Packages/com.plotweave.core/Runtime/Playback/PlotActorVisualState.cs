using UnityEngine;

namespace PlotWeave.Playback
{
    public sealed class PlotActorVisualState : MonoBehaviour
    {
        private Vector3 _scale;
        private Quaternion _rotation;
        private bool _captured;
        public CanvasGroup Group { get; private set; }
        public void Capture()
        {
            if (!_captured) { _captured = true; _scale = transform.localScale; _rotation = transform.localRotation; }
            // Unity的缺失/已销毁组件使用自定义null语义，不能用??判断。
            if (Group == null) { Group = GetComponent<CanvasGroup>(); if (Group == null) Group = gameObject.AddComponent<CanvasGroup>(); }
        }
        public void ResetVisual()
        { Capture(); transform.localScale = _scale; transform.localRotation = _rotation; Group.alpha = 1; var bubble = transform.Find("PlotEmote"); if (bubble != null) bubble.gameObject.SetActive(false); }
        public Vector3 OriginalScale { get { Capture(); return _scale; } }
    }
}
