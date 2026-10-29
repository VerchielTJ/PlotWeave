using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlotWeave.Authoring.Playback;
using PlotWeave.Chapters;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyRewardPresentationTests
    {
        private static GameObject Rect(string name,Transform parent=null,params Type[] components)
        {var value=new GameObject(name,typeof(RectTransform));if(parent!=null)value.transform.SetParent(parent,false);foreach(var type in components)value.AddComponent(type);return value;}
        private static PlotLegacyVirtualList List(GameObject owner,GameObject template,out RectTransform viewport)
        {var scroll=Rect("Scroll",owner.transform,typeof(ScrollRect)).GetComponent<ScrollRect>();viewport=(RectTransform)Rect("Viewport",scroll.transform).transform;viewport.sizeDelta=new Vector2(400,204);var content=(RectTransform)Rect("Content",viewport).transform;scroll.viewport=viewport;scroll.content=content;var list=scroll.gameObject.AddComponent<PlotLegacyVirtualList>();list.Initialize(scroll,template,true,130);return list;}
        private static void Bind(PlotLegacyCampaignHost host,GameObject item,int id,int count)
        {typeof(PlotLegacyCampaignHost).GetMethod("BindReward",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(host,new object[]{item,new PlotCampaignItem{itemID=id,count=count}});}
        [Test] public void ImportedItemRootIsPlacedInsideTheCellAndResizingKeepsItsAuthoredSize()
        {
            var owner=Rect("ImportedRewardListReview");var template=Rect("WrappedReward",owner.transform);template.SetActive(false);template.AddComponent<PlotLegacyTemplateBindings>().sourceLayout="fixture/Reward.prefab";var original=(RectTransform)Rect("ActualReward",template.transform).transform;original.anchorMin=original.anchorMax=Vector2.zero;original.pivot=new Vector2(.5f,.5f);original.sizeDelta=new Vector2(145,155);original.anchoredPosition=new Vector2(5,2);
            try
            {
                var list=List(owner,template,out var viewport);list.Set(2,(_,__)=>{});var content=list.GetComponent<ScrollRect>().content;var first=(RectTransform)content.GetChild(0);var actual=(RectTransform)first.GetChild(0);Assert.That(actual.anchorMin,Is.EqualTo(new Vector2(.5f,.5f)));Assert.That(actual.anchorMax,Is.EqualTo(new Vector2(.5f,.5f)));Assert.That(actual.anchoredPosition,Is.EqualTo(Vector2.zero));Assert.That(actual.rect.width,Is.EqualTo(130).Within(.01));Assert.That(actual.rect.height,Is.EqualTo(155).Within(.01));var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(first,actual);Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(-.01));Assert.That(bounds.max.x,Is.LessThanOrEqualTo(first.rect.width+.01));Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(-first.rect.height-.01));Assert.That(bounds.max.y,Is.LessThanOrEqualTo(.01));
                viewport.sizeDelta=new Vector2(400,100);list.Refresh();Assert.That(actual.rect.height,Is.EqualTo(100).Within(.01));viewport.sizeDelta=new Vector2(400,204);list.Refresh();Assert.That(actual.rect.height,Is.EqualTo(155).Within(.01));Assert.That(list.PoolSize,Is.EqualTo(2));Assert.That(original.anchoredPosition,Is.EqualTo(new Vector2(5,2)));Assert.That(original.sizeDelta,Is.EqualTo(new Vector2(145,155)));
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test] public void UserAndPublicTemplatesWithoutAnImportMarkerKeepTheirChildLayout()
        {
            var owner=Rect("OwnedRewardListReview");var template=Rect("OwnedReward",owner.transform);template.SetActive(false);var art=(RectTransform)Rect("Art",template.transform).transform;art.anchorMin=art.anchorMax=Vector2.zero;art.sizeDelta=new Vector2(66,55);art.anchoredPosition=new Vector2(12,34);
            try{var list=List(owner,template,out _);list.Set(1,(_,__)=>{});var clone=(RectTransform)list.GetComponent<ScrollRect>().content.GetChild(0).GetChild(0);Assert.That(clone.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(clone.sizeDelta,Is.EqualTo(new Vector2(66,55)));Assert.That(clone.anchoredPosition,Is.EqualTo(new Vector2(12,34)));}
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [TestCase(true)][TestCase(false)] public void OldImportedAspectIsRestoredOnlyFromExplicitMetadata(bool hasMetadata)
        {
            var root=Rect("AspectCompatibilityReview");var host=root.AddComponent<PlotLegacyCampaignHost>();var item=Rect("Reward",root.transform);((RectTransform)item.transform).sizeDelta=new Vector2(130,204);var frame=Rect("Back",item.transform,typeof(Image)).GetComponent<Image>();frame.rectTransform.sizeDelta=Vector2.zero;Rect("Icon",frame.transform,typeof(Image));Rect("Count",frame.transform,typeof(TextMeshProUGUI));
            if(hasMetadata){var proxy=frame.gameObject.AddComponent<PlotLegacyBehaviourProxy>();proxy.sourceScript="UnityPackage/AspectRatioFitter.cs";proxy.valuesJson="{\"m_AspectMode\":3,\"m_AspectRatio\":0.82905984}";}
            try{Bind(host,item,1001,10);var fitter=frame.GetComponent<AspectRatioFitter>();Assert.That(fitter!=null,Is.EqualTo(hasMetadata));if(hasMetadata){Assert.That(fitter.aspectMode,Is.EqualTo(AspectRatioFitter.AspectMode.FitInParent));Assert.That(fitter.aspectRatio,Is.EqualTo(.82905984f).Within(.000001));Assert.That(frame.rectTransform.rect.width,Is.EqualTo(130).Within(.001));Assert.That(frame.rectTransform.rect.height,Is.EqualTo(130/.82905984f).Within(.001));Bind(host,item,1001,10);Assert.That(frame.GetComponents<AspectRatioFitter>().Length,Is.EqualTo(1));((RectTransform)item.transform).sizeDelta=new Vector2(260,204);Canvas.ForceUpdateCanvases();Assert.That(fitter.aspectRatio,Is.EqualTo(.82905984f).Within(.000001));Assert.That(frame.rectTransform.rect.width,Is.EqualTo(204*.82905984f).Within(.001));Assert.That(frame.rectTransform.rect.height,Is.EqualTo(204).Within(.001));}else Assert.That(frame.rectTransform.sizeDelta,Is.EqualTo(Vector2.zero),"无明确原生布局信息时不猜测Back尺寸");}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void ReusedRewardWithoutAnIconAlwaysShowsItsIdAndQuantity()
        {
            var root=Rect("FallbackRewardReview");var host=root.AddComponent<PlotLegacyCampaignHost>();var item=Rect("Reward",root.transform);((RectTransform)item.transform).sizeDelta=new Vector2(130,204);Rect("Back",item.transform,typeof(Image));var icon=Rect("Icon",item.transform,typeof(Image)).GetComponent<Image>();var count=Rect("Count",item.transform,typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();Texture2D texture=null;Sprite sprite=null;
            try
            {
                Bind(host,item,1001,10);var label=item.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="RewardItemName");Assert.That(label.text,Does.Contain("1001"));Assert.That(label.gameObject.activeSelf,Is.True);Assert.That(label.rectTransform.rect.width,Is.GreaterThan(0));Assert.That(label.rectTransform.rect.height,Is.GreaterThan(0));Assert.That(count.text,Is.EqualTo("10"));Bind(host,item,1002,7);Assert.That(label.text,Does.Contain("1002"));Assert.That(count.text,Is.EqualTo("7"));Assert.That(item.GetComponentsInChildren<TMP_Text>(true).Count(t=>t.name=="RewardItemName"),Is.EqualTo(1));
                texture=new Texture2D(2,2);texture.Apply();sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,2,2),new Vector2(.5f,.5f));host.itemPresentations.Add(new PlotCampaignItemPresentation{id=1002,name="Token",icon=sprite});Bind(host,item,1002,7);Assert.That(label.gameObject.activeSelf,Is.False);Assert.That(icon.sprite,Is.SameAs(sprite));Assert.That(count.text,Is.EqualTo("7"));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);if(sprite!=null)UnityEngine.Object.DestroyImmediate(sprite);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);}
        }
        [Test] public void PublicSyntheticEpisodeTemplateShowsRewardsAndUsesTheReturnBarHeader()
        {
            var templates=PlotCampaignReferenceExample.CreateTemplates(PlotTMPText.DefaultFont);var root=Rect("SyntheticEpisodeReview");var browser=root.AddComponent<PlotLegacyCampaignHost>();var campaign=ScriptableObject.CreateInstance<PlotCampaignAsset>();campaign.data=PlotCampaignReferenceExample.CreateCatalog();var content=ScriptableObject.CreateInstance<PlotPlaybackAsset>();content.data=PlotCampaignReferenceExample.CreateDocument();browser.campaign=campaign;browser.content=content;browser.chapterPrefab=templates["ChapterPanel"];browser.episodePrefab=templates["EpisodePanel"];browser.chapterItemPrefab=templates["ChapterCard"];browser.episodeItemPrefab=templates["EpisodeCard"];browser.rewardItemPrefab=templates["RewardCard"];var progress=new PlotCampaignProgressStore();
            try
            {
                browser.Initialize(new PlotCampaignLocalBackend(campaign.data,content.data,progress,()=>new DateTime(2030,1,1),_=>true),progress,()=>new DateTime(2030,1,1),_=>true);browser.SelectChapter(10);Canvas.ForceUpdateCanvases();var panel=(GameObject)typeof(PlotLegacyCampaignHost).GetField("_episodes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(browser);var bar=PlotLegacyWidgets.Find<Transform>(panel.transform,"ReturnBtn");Assert.That(bar.Find("Title").GetComponent<TMP_Text>().text,Is.EqualTo("主线故事"));Assert.That(bar.Find("BackBtn/Title").GetComponent<TMP_Text>().text,Is.EqualTo("返回"));var rewards=panel.GetComponentsInChildren<PlotLegacyVirtualList>(true).Single(l=>l.name=="AwardScrollRect");rewards.Refresh();var labels=rewards.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="RewardItemName"&&t.gameObject.activeSelf).ToList();Assert.That(labels.Count,Is.EqualTo(2));Assert.That(labels.Any(t=>t.text.Contains("7")),Is.True);Assert.That(labels.Any(t=>t.text.Contains("9")),Is.True);Assert.That(rewards.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Count").Select(t=>t.text),Is.EquivalentTo(new[]{"3","1"}));
                browser.OpenKind(PlotChapterKind.Daily);browser.SelectChapter(20);Assert.That(bar.Find("Title").GetComponent<TMP_Text>().text,Is.EqualTo("日常故事"));Assert.That(browser.LastError,Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);foreach(var template in templates.Values)UnityEngine.Object.DestroyImmediate(template);UnityEngine.Object.DestroyImmediate(campaign);UnityEngine.Object.DestroyImmediate(content);}
        }
    }
}
