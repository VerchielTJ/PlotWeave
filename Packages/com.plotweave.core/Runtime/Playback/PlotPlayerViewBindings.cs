using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Playback
{
    /// <summary>显式绑定可保存为预制体，不依赖XUI按名称查找或旧项目GUID。</summary>
    public sealed class PlotPlayerViewBindings : MonoBehaviour
    {
        public GameObject uiRoot, dialogBox;
        public RectTransform stage;
        public Image background;
        public TMP_Text speaker, dialog, curtainTop, curtainBottom;
        public Text nodeLabel;
        public CanvasGroup curtain;
        public Button next, auto, pause, close;
        public Animator uiAnimator;
        public void Validate()
        {
            if (uiRoot == null || dialogBox == null || stage == null || background == null || speaker == null || dialog == null || curtainTop == null || curtainBottom == null || nodeLabel == null || curtain == null || next == null || auto == null || pause == null || close == null)
                throw new InvalidOperationException("播放预制体绑定不完整，请检查PlotPlayerViewBindings");
        }
    }
    [Serializable] public sealed class PlotActorPrefabBinding { public string key = ""; public GameObject prefab; }
}
