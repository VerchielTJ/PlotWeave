using System;
using PlotWeave.Playback;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Legacy
{
    public sealed class PlotLegacySkipController:MonoBehaviour
    {
        public Button skipButton;
        public GameObject popupPrefab;
        public string skipMessage="跳过本节剧情？";
        private PlotPlayerCore _core;
        private GameObject _popup;
        private bool _wasPaused,_showing;
        public bool IsShowing=>_showing;
        public GameObject Popup=>_popup;
        public void Bind(PlotPlayerCore core)
        {Cancel();if(_core!=null)_core.OnClosed-=Closed;_core=core;_core.OnClosed+=Closed;PlotLegacyWidgets.Click(skipButton,Show);}
        public void Show()
        {
            if(_core==null||_showing||(_core.State!=PlotPlayerState.Playing&&_core.State!=PlotPlayerState.WaitingForChoice&&_core.State!=PlotPlayerState.Idle))return;
            if(popupPrefab==null)throw new InvalidOperationException("缺少跳过确认弹窗");
            _wasPaused=_core.IsPaused;_core.Pause();_showing=true;
            if(_popup==null){_popup=Instantiate(popupPrefab,transform,false);PlotLegacyWidgets.Click(PlotLegacyWidgets.Find<Button>(_popup.transform,"ConfirmBtn"),Confirm);PlotLegacyWidgets.Click(PlotLegacyWidgets.Find<Button>(_popup.transform,"CancelBtn"),Cancel);foreach(var close in _popup.GetComponentsInChildren<Button>(true))if(close.name=="Close")PlotLegacyWidgets.Click(close,Cancel);}
            var rect=(RectTransform)_popup.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;PlotLegacyWidgets.OpenLayout(_popup);LayoutImportedPopup();_popup.transform.SetAsLastSibling();PlotLegacyWidgets.Caption(PlotLegacyWidgets.Find<TMPro.TMP_Text>(_popup.transform,"ContentText"),skipMessage);
        }
        public void Cancel(){if(!_showing)return;_showing=false;if(_popup!=null)_popup.SetActive(false);if(_core!=null&&!_wasPaused)_core.Resume();}
        public void Confirm(){if(!_showing)return;_showing=false;if(_popup!=null)_popup.SetActive(false);if(!_wasPaused)_core.Resume();_core.Skip();}
        private void LayoutImportedPopup()
        {
            var template=_popup.GetComponent<PlotLegacyTemplateBindings>();if(template==null||!(template.sourceLayout??"").EndsWith("/PlotSkipTop.prefab",StringComparison.Ordinal))return;
            var panel=PlotLegacyWidgets.Find<RectTransform>(_popup.transform,"BG");if(panel==null)return;
            foreach(var image in _popup.GetComponentsInChildren<Image>(true))if(image.name=="Image"&&image.transform.parent.name=="PlotSkipTop"){image.sprite=null;image.color=new Color(0,0,0,.55f);var r=image.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(Mathf.Min(1400,((RectTransform)_popup.transform).rect.width-100),Mathf.Min(500,((RectTransform)_popup.transform).rect.height-100));panel.anchoredPosition=Vector2.zero;
            var tip=panel.Find("Tip");if(tip!=null)tip.gameObject.SetActive(false);
            foreach(var label in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))if(label.name=="Title"&&label.transform.parent==panel){label.text="跳过剧情";label.fontSize=32;label.alignment=TMPro.TextAlignmentOptions.MidlineLeft;var r=label.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-120,56);r.anchoredPosition=new Vector2(0,-28);}
            var content=PlotLegacyWidgets.Find<TMPro.TMP_Text>(panel,"ContentText");if(content!=null){content.fontSize=30;content.alignment=TMPro.TextAlignmentOptions.Center;var r=content.rectTransform;r.anchorMin=new Vector2(0,.32f);r.anchorMax=new Vector2(1,.72f);r.offsetMin=new Vector2(60,0);r.offsetMax=new Vector2(-60,0);}
            var index=0;foreach(var name in new[]{"CancelBtn","ConfirmBtn"}){var button=PlotLegacyWidgets.Find<Button>(panel,name);if(button==null)continue;var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=new Vector2(index++==0?.25f:.75f,.13f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(panel.rect.width*.4f,90);r.anchoredPosition=Vector2.zero;var label=button.GetComponentInChildren<TMPro.TMP_Text>(true);if(label!=null){label.fontSize=30;label.alignment=TMPro.TextAlignmentOptions.Center;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;}}
            foreach(var close in panel.GetComponentsInChildren<Button>(true))if(close.name=="Close"){var r=(RectTransform)close.transform;r.anchorMin=r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(40,40);r.anchoredPosition=new Vector2(-36,-36);}
        }
        private void Closed(){_showing=false;if(_popup!=null)_popup.SetActive(false);}
        private void OnDestroy(){if(_core!=null)_core.OnClosed-=Closed;}
    }
}
