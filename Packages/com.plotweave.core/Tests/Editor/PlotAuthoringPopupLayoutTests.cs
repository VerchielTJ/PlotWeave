using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlotWeave.Legacy;
using PlotWeave.Legacy.Editor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotAuthoringPopupLayoutTests
    {
        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _scope, _toolbar;
        private PlotAuthoringPopupLayout _layout;
        private HashSet<EventSystem> _existing;
        [SetUp] public void SetUp()
        {
            _existing = new HashSet<EventSystem>(Resources.FindObjectsOfTypeAll<EventSystem>());
            _root = new GameObject("PopupTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas = _root.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scope = Rect("Workbench", _root.transform, new Vector2(800, 600)); Fill(_scope);
            _toolbar = Rect("PlotWeaveMigrationToolbar", _root.transform, new Vector2(0, 40));
            _toolbar.anchorMin = new Vector2(0, 1); _toolbar.anchorMax = Vector2.one; _toolbar.pivot = new Vector2(.5f, 1); _toolbar.anchoredPosition = Vector2.zero;
            _layout = _root.AddComponent<PlotAuthoringPopupLayout>(); _layout.Initialize(_canvas, _scope);
        }
        [TearDown] public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            foreach (var events in Resources.FindObjectsOfTypeAll<EventSystem>())
                if (!_existing.Contains(events) && events.gameObject.scene.IsValid()) UnityEngine.Object.DestroyImmediate(events.gameObject);
        }
        [UnityTest] public IEnumerator PopupInputReceivesRealRaycastAboveALaterAuthoringInputSibling()
        {
            // EditMode does not render a temporary overlay or run ordinary BaseRaycaster
            // lifecycle messages. Use the established real-render fixture, rather than
            // querying unrendered depth=-1 graphics or faking an onClick result.
            var previousTexture=RenderTexture.active;
            var cameraObject=new GameObject("PopupRaycastCamera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            var texture=new RenderTexture(800,600,24);texture.Create();camera.targetTexture=texture;camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=300;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.depth=1000;
            camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<31;
            _canvas.renderMode=RenderMode.ScreenSpaceCamera;_canvas.worldCamera=camera;_canvas.planeDistance=10;_canvas.scaleFactor=1;
            _root.GetComponent<GraphicRaycaster>().enabled=false;_root.AddComponent<PlotLegacyReviewGraphicRaycaster>();
            var input=new GameObject("PopupRaycastEventSystem",typeof(PlotLegacyReviewEventSystem));var system=input.GetComponent<EventSystem>();
            try
            {
                var popup=Popup("ParameterEditor");popup.gameObject.AddComponent<Canvas>().worldCamera=camera;
                popup.gameObject.AddComponent<PlotLegacyReviewGraphicRaycaster>();
                var field=Button("ParameterInput",popup,new Vector2(100,40));_layout.Open(popup);
                var underlying=Button("DialogInputField",_scope,new Vector2(200,80));
                foreach(var child in _root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                yield return null;Canvas.ForceUpdateCanvases();camera.Render();yield return null;Canvas.ForceUpdateCanvases();camera.Render();
                underlying.transform.position=field.transform.position;Canvas.ForceUpdateCanvases();camera.Render();
                var graphic=field.GetComponent<Image>();var popupCanvas=popup.GetComponent<Canvas>();
                Assert.That(GraphicRegistry.GetRaycastableGraphicsForCanvas(popupCanvas).Contains(graphic),Is.True,"弹层输入Graphic必须已注册到其真实Canvas");
                Assert.That(graphic.depth,Is.GreaterThanOrEqualTo(0),"射线验收前必须真实渲染，不能测试depth=-1的未绘制UI");
                Assert.That(graphic.canvasRenderer.cull,Is.False);
                var point=RectTransformUtility.WorldToScreenPoint(camera,field.transform.TransformPoint(((RectTransform)field.transform).rect.center));
                Assert.That(camera.pixelRect.Contains(point),Is.True);
                var pointer=new PointerEventData(system){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();system.RaycastAll(pointer,hits);
                Assert.That(hits,Is.Not.Empty);
                var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Assert.That(handler,Is.SameAs(field.gameObject));
                var edited=0;var blocked=0;field.onClick.AddListener(()=>edited++);underlying.onClick.AddListener(()=>blocked++);
                Assert.That(ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler),Is.True);
                Assert.That(edited,Is.EqualTo(1));Assert.That(blocked,Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(input);camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);
                RenderTexture.active=previousTexture;texture.Release();UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        [Test] public void ProtrudingListHeaderAndCloseButtonStayBelowTheActualToolbarAfterResize()
        {
            WorldSpace(new Vector2(800, 600)); var popup = Popup("ParameterList"); popup.anchoredPosition = new Vector2(380, 260);
            var header = Rect("ListHeader", popup, new Vector2(260, 50)); header.anchoredPosition = new Vector2(0, 150); header.gameObject.AddComponent<Image>();
            var parent = popup.parent; _layout.Open(popup); AssertInsideSafe(popup);
            ((RectTransform)_root.transform).sizeDelta = new Vector2(500, 400); _toolbar.sizeDelta = new Vector2(0, 82); _layout.Refresh();
            AssertInsideSafe(popup); Assert.That(popup.parent, Is.SameAs(parent));
        }
        [Test] public void OpeningAnotherPopupClosesThePreviousAndExplicitCloseRestoresItsOriginalLayout()
        {
            WorldSpace(new Vector2(800, 600)); var first = Popup("FirstParameter"); var second = Popup("SecondParameter");
            var start = first.anchoredPosition; var scale = first.localScale;
            _layout.Open(first); _layout.Open(second);
            Assert.That(first.gameObject.activeSelf, Is.False); Assert.That(_layout.ActivePopup, Is.SameAs(second));
            Assert.That(first.anchoredPosition, Is.EqualTo(start)); Assert.That(first.localScale, Is.EqualTo(scale));
            second.Find("PlotWeaveClosePopup").GetComponent<Button>().onClick.Invoke();
            Assert.That(_layout.ActivePopup, Is.Null); Assert.That(second.gameObject.activeSelf, Is.False);
        }
        [Test] public void OriginalRowUnitReferencesStillCloneToTheirOwnParameterPanel()
        {
            WorldSpace(new Vector2(800, 600)); var popup = Popup("ParameterEditor"); var row = popup.parent.gameObject;
            var parameter = popup.gameObject.AddComponent<PlotLegacyBehaviourProxy>(); parameter.sourceScript = "Memory/ParameterEditor.cs";
            var unit = row.AddComponent<PlotLegacyBehaviourProxy>(); unit.sourceScript = "Memory/RowUnit.cs";
            unit.references.Add(new PlotLegacyObjectReference { path = "ParameterEditor", value = parameter });
            _layout.Open(popup); _layout.Close(); var clone = UnityEngine.Object.Instantiate(row, _scope, false);
            var clonedPanel = clone.GetComponent<PlotLegacyBehaviourProxy>().Field<PlotLegacyBehaviourProxy>("ParameterEditor");
            Assert.That(clonedPanel, Is.Not.SameAs(parameter)); Assert.That(clonedPanel.transform.parent, Is.SameAs(clone.transform));
            Assert.That(parameter.transform.parent, Is.SameAs(row.transform));
        }
        [Test] public void InternalListViewportKeepsItsMaskAndOversizedContentDoesNotShrinkTheWholePopup()
        {
            WorldSpace(new Vector2(800, 600)); var popup = Popup("ParameterList");
            var viewport = Rect("Viewport", popup, new Vector2(100, 100)); var mask = viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("LongVirtualList", viewport, new Vector2(80, 1000)); content.gameObject.AddComponent<Image>();
            _layout.Open(popup);
            Assert.That(popup.localScale, Is.EqualTo(Vector3.one)); Assert.That(mask.enabled, Is.True);
            Assert.That(content.rect.height, Is.EqualTo(1000)); Assert.That(content.parent, Is.SameAs(viewport));
        }
        [Test] public void CloseControlKeepsReadablePhysicalSizeEvenWhenThePopupFitsASmallAncestorMask()
        {
            WorldSpace(new Vector2(800,600));_root.transform.localScale=Vector3.one*.25f;
            var mask=Rect("SmallClippedRegion",_scope,new Vector2(200,180));mask.gameObject.AddComponent<RectMask2D>();
            var popup=Popup("ParameterEditor");popup.parent.SetParent(mask,false);_layout.Open(popup);
            var close=(RectTransform)popup.Find("PlotWeaveClosePopup");var corners=new Vector3[4];close.GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            Assert.That(max.x-min.x,Is.GreaterThanOrEqualTo(43.9f));Assert.That(max.y-min.y,Is.GreaterThanOrEqualTo(27.9f));
            var unit=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,close.TransformPoint(Vector3.zero)),RectTransformUtility.WorldToScreenPoint(null,close.TransformPoint(Vector3.up)));
            Assert.That(close.GetComponentInChildren<Text>().fontSize*unit,Is.GreaterThanOrEqualTo(11.99f));
            foreach(var graphic in popup.GetComponentsInChildren<Graphic>(false))
            {
                ((RectTransform)graphic.transform).GetWorldCorners(corners);
                foreach(var corner in corners){var p=mask.InverseTransformPoint(corner);Assert.That(p.x,Is.InRange(mask.rect.xMin-.1f,mask.rect.xMax+.1f));Assert.That(p.y,Is.InRange(mask.rect.yMin-.1f,mask.rect.yMax+.1f));}
            }
        }
        [Test] public void ImportedListRowKeepsBothGridColumnsInsideItsViewport()
        {
            WorldSpace(new Vector2(800,600));var viewport=Rect("ListViewport",_scope,new Vector2(738,400));viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,new Vector2(0,176));content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            var row=GridRow(content,4);var height=PrepareRow(row,content);
            Assert.That(row.rect.width,Is.EqualTo(viewport.rect.width).Within(.01f));Assert.That(height,Is.EqualTo(176).Within(.01f));
            foreach(var graphic in row.GetComponentsInChildren<Graphic>())
            {
                var corners=new Vector3[4];((RectTransform)graphic.transform).GetWorldCorners(corners);
                foreach(var corner in corners){var p=viewport.InverseTransformPoint(corner);Assert.That(p.x,Is.InRange(viewport.rect.xMin-.01f,viewport.rect.xMax+.01f));Assert.That(p.y,Is.InRange(viewport.rect.yMin-.01f,viewport.rect.yMax+.01f));}
            }
        }
        [Test] public void LargerBeanRowsUseTheirActualGridHeightSoConsecutiveRowsDoNotOverlap()
        {
            WorldSpace(new Vector2(800,600));var content=Rect("Content",_scope,new Vector2(738,600));
            var first=GridRow(content,6);var height=PrepareRow(first,content);var second=GridRow(content,6);PrepareRow(second,content);second.anchoredPosition=new Vector2(0,-height);
            Assert.That(height,Is.EqualTo(264).Within(.01f));
            var firstCorners=new Vector3[4];var secondCorners=new Vector3[4];first.GetWorldCorners(firstCorners);second.GetWorldCorners(secondCorners);
            Assert.That(firstCorners[0].y,Is.EqualTo(secondCorners[1].y).Within(.01f));
            foreach(var graphic in first.GetComponentsInChildren<Graphic>())
            {var corners=new Vector3[4];((RectTransform)graphic.transform).GetWorldCorners(corners);Assert.That(corners.Min(c=>c.y),Is.GreaterThanOrEqualTo(firstCorners[0].y-.01f));}
        }
        private static RectTransform GridRow(RectTransform parent,int count)
        {
            var row=Rect("ImportedEvtListParameterEditor",parent,new Vector2(700,176));var grid=row.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(350,88);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            for(var i=0;i<count;i++)Rect("Cell"+i,row,new Vector2(350,88)).gameObject.AddComponent<Image>();return row;
        }
        private static float PrepareRow(RectTransform row,RectTransform content)
        {return (float)typeof(PlotLegacyAuthoringHost).GetMethod("PrepareListRow",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{row,content});}
        private void WorldSpace(Vector2 size)
        { _canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)_root.transform).sizeDelta = size; }
        private RectTransform Popup(string name)
        {
            var row = Rect("RowUnit", _scope, new Vector2(300, 200)); var popup = Rect(name, row, new Vector2(300, 200));
            popup.gameObject.AddComponent<Image>(); popup.gameObject.SetActive(false); return popup;
        }
        private void AssertInsideSafe(RectTransform popup)
        {
            var canvasRect = (RectTransform)_root.transform; var toolbarCorners = new Vector3[4]; _toolbar.GetWorldCorners(toolbarCorners);
            var toolbarBottom = canvasRect.InverseTransformPoint(toolbarCorners[0]).y;
            foreach (var graphic in popup.GetComponentsInChildren<Graphic>(false))
            {
                var corners = new Vector3[4]; ((RectTransform)graphic.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var p = canvasRect.InverseTransformPoint(corner);
                    Assert.That(p.x, Is.InRange(canvasRect.rect.xMin + 7.9f, canvasRect.rect.xMax - 7.9f));
                    Assert.That(p.y, Is.InRange(canvasRect.rect.yMin + 7.9f, toolbarBottom - 7.9f));
                }
            }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform; rect.sizeDelta = size; return rect; }
        private static void Fill(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Button Button(string name, Transform parent, Vector2 size)
        { var rect = Rect(name, parent, size); var image = rect.gameObject.AddComponent<Image>(); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; return button; }
    }
}
