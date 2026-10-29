using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlotWeave.Legacy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    // Ordinary EventSystem/BaseRaycaster lifecycle messages do not run in EditMode.
    // These fixture-only components use Unity's normal OnEnable/OnDisable registration
    // while allowing the test to exercise the real EventSystem.RaycastAll implementation.
    [ExecuteAlways,AddComponentMenu("")] public sealed class PlotLegacyReviewEventSystem:EventSystem { }
    [ExecuteAlways,AddComponentMenu("")] public sealed class PlotLegacyReviewGraphicRaycaster:GraphicRaycaster { }
    public sealed class PlotLegacyChapterHitAreaTests
    {
        private static GameObject Rect(string name,Transform parent=null,params Type[] components)
        {var value=new GameObject(name,typeof(RectTransform));if(parent!=null)value.transform.SetParent(parent,false);foreach(var type in components)value.AddComponent(type);return value;}
        private static Button Prepare(GameObject item,int index)
        {return (Button)typeof(PlotLegacyCampaignHost).GetMethod("ChapterButton",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{item,index});}
        private static GameObject ImportedCard(Transform parent)
        {
            var wrapper=Rect("ImportedChapter",parent);wrapper.AddComponent<PlotLegacyTemplateBindings>().sourceLayout="fixture/PlotItemNode.prefab";
            var source=Rect("PlotItemNode",wrapper.transform,typeof(Button));var sourceRect=(RectTransform)source.transform;sourceRect.anchorMin=sourceRect.anchorMax=new Vector2(0,1);sourceRect.sizeDelta=new Vector2(656,854);sourceRect.anchoredPosition=new Vector2(422,-432);
            var body=(RectTransform)Rect("Item",source.transform).transform;body.sizeDelta=new Vector2(100,100);
            var frame=Rect("BG",body,typeof(Image));((RectTransform)frame.transform).sizeDelta=new Vector2(656,412);
            var thumbnail=Rect("Thumbnai",body,typeof(Image));var thumbnailRect=(RectTransform)thumbnail.transform;thumbnailRect.sizeDelta=new Vector2(618,290);thumbnailRect.anchoredPosition=new Vector2(-3,45.3f);
            foreach(var entry in new[]{new KeyValuePair<string,float>("Pos0",221),new KeyValuePair<string,float>("Pos1",-216)}){var pos=(RectTransform)Rect(entry.Key,source.transform).transform;pos.anchoredPosition=new Vector2(0,entry.Value);}
            wrapper.SetActive(false);return wrapper;
        }
        [UnityTest] public IEnumerator ImportedEvenStaggeredCardCenterHitsItsVisibleBackgroundAndInvokesTheBoundButton()=>ImportedStaggeredCardCenter(0);
        [UnityTest] public IEnumerator ImportedOddStaggeredCardCenterHitsItsVisibleBackgroundAndInvokesTheBoundButton()=>ImportedStaggeredCardCenter(1);
        private static IEnumerator ImportedStaggeredCardCenter(int selected)
        {
            var previousTexture=RenderTexture.active;var owner=Rect("ChapterHitAreaReview",null,typeof(Canvas),typeof(PlotLegacyReviewGraphicRaycaster));var canvas=owner.GetComponent<Canvas>();
            // EditMode does not guarantee that a Game View renders an overlay canvas. An unrendered
            // Graphic retains depth -1 and GraphicRaycaster correctly excludes it, even after ForceUpdateCanvases.
            // Render a real camera canvas into a fixed target so registration, depth and screen points
            // have a deterministic render surface independent of the user's open editor tabs.
            var cameraObject=new GameObject("ReviewUICamera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();var texture=new RenderTexture(800,1000,24);texture.Create();camera.targetTexture=texture;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=500;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.depth=1000;camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<31;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.sortingOrder=30000;canvas.scaleFactor=1;
            var input=new GameObject("ReviewEventSystem",typeof(PlotLegacyReviewEventSystem));var system=input.GetComponent<EventSystem>();
            try
            {
                var backdrop=Rect("Background",owner.transform,typeof(Image));var backdropRect=(RectTransform)backdrop.transform;backdropRect.anchorMin=Vector2.zero;backdropRect.anchorMax=Vector2.one;backdropRect.offsetMin=backdropRect.offsetMax=Vector2.zero;
                var scroll=Rect("StoryScrollRect",owner.transform,typeof(ScrollRect)).GetComponent<ScrollRect>();((RectTransform)scroll.transform).sizeDelta=new Vector2(650,854);
                var viewport=(RectTransform)Rect("Viewport",scroll.transform,typeof(RectMask2D)).transform;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=viewport.offsetMax=Vector2.zero;
                var content=(RectTransform)Rect("Content",viewport).transform;content.pivot=new Vector2(0,.5f);content.anchoredPosition=new Vector2(0,-427);scroll.viewport=viewport;scroll.content=content;
                var template=ImportedCard(owner.transform);var list=scroll.gameObject.AddComponent<PlotLegacyVirtualList>();list.Initialize(scroll,template,true,650);Assert.That(content.anchoredPosition,Is.EqualTo(Vector2.zero));
                var buttons=new Dictionary<int,Button>();var calls=0;list.Set(2,(item,index)=>{var button=Prepare(item,index);buttons[index]=button;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>calls++);},selected);foreach(var child in owner.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                yield return null;Canvas.ForceUpdateCanvases();camera.Render();yield return null;Canvas.ForceUpdateCanvases();camera.Render();
                Assert.That(content.anchoredPosition.y,Is.Zero);var target=buttons[selected];Assert.That(target.name,Is.EqualTo("BG"));Assert.That(target.GetComponent<Image>().raycastTarget,Is.True);var source=target.transform.parent.parent;Assert.That(source.GetComponent<Button>().enabled,Is.False);Assert.That(source.GetComponent<Button>().IsInteractable(),Is.False);Assert.That(target.transform.parent.localPosition.y,Is.EqualTo(selected==0?221:-216).Within(.01));Assert.That(target.transform.parent.GetComponent<RectTransform>().sizeDelta,Is.EqualTo(new Vector2(100,100)));Assert.That(source.Find("Item/Thumbnai").GetComponent<Image>().raycastTarget,Is.False);
                // UGUI IndexedSet intentionally does not implement enumeration; use its real IList.Contains.
                var graphic=target.GetComponent<Image>();Assert.That(GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas).Contains(graphic),Is.True,"卡片Graphic必须已注册到实际Canvas");Assert.That(graphic.depth,Is.GreaterThanOrEqualTo(0),"测试画面必须先真实渲染，不能对未绘制Graphic作射线验收");Assert.That(graphic.canvasRenderer.cull,Is.False);
                var rect=(RectTransform)target.transform;var center=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));Assert.That(camera.pixelRect.Contains(center),Is.True,"卡片中心必须位于实际渲染目标内");var pointer=new PointerEventData(system){position=center,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();system.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Assert.That(handler,Is.SameAs(target.gameObject),"卡片中心应落在实际可见BG，而不是背景或空槽");Assert.That(ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler),Is.True);Assert.That(calls,Is.EqualTo(1));
                list.Refresh();Assert.That(target.GetComponents<Button>().Length,Is.EqualTo(1));Assert.That(source.GetComponentsInChildren<Button>().Count(b=>b.IsInteractable()),Is.EqualTo(1));
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(input);camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);RenderTexture.active=previousTexture;texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
        }
        [Test] public void UnmarkedUserChapterButtonKeepsItsOwnGraphicAndRaycastConfiguration()
        {
            var item=Rect("UserChapter",null,typeof(Button),typeof(Image));var body=Rect("Item",item.transform);var frame=Rect("BG",body.transform,typeof(Image));var button=item.GetComponent<Button>();button.targetGraphic=item.GetComponent<Image>();
            try{Assert.That(Prepare(item,0),Is.SameAs(button));Assert.That(button.enabled,Is.True);Assert.That(frame.GetComponent<Image>().raycastTarget,Is.True);Assert.That(frame.GetComponent<Button>(),Is.Null);}
            finally{UnityEngine.Object.DestroyImmediate(item);}
        }
    }
}
