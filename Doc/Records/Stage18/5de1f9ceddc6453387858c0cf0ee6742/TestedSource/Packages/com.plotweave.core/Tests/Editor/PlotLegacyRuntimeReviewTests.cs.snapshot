using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlotWeave.Chapters;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyRuntimeReviewTests
    {
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static PlotCampaignCatalog Catalog()=>new PlotCampaignCatalog
        {
            chapters=new List<PlotCampaignChapter>{new PlotCampaignChapter{kind=PlotChapterKind.Main,id=1,episodes=new List<int>{500,100}}},
            episodes=new List<PlotCampaignEpisode>{new PlotCampaignEpisode{kind=PlotChapterKind.Main,id=500,entryNode=1},new PlotCampaignEpisode{kind=PlotChapterKind.Main,id=100,entryNode=1}}
        };
        [Test] public void LegacyProgressUsesTheConfiguredPrefixRatherThanNumericIdOrder()
        {var catalog=Catalog();var progress=new PlotCampaignProgressStore();progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,500);Assert.That(progress.IsRead(PlotChapterKind.Main,500),Is.True);Assert.That(progress.IsRead(PlotChapterKind.Main,100),Is.False);progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,100);Assert.That(progress.IsRead(PlotChapterKind.Main,100),Is.True);}
        [Test] public void ZeroAndBeyondMaximumLegacyProgressHaveExplicitMeanings()
        {var catalog=Catalog();var progress=new PlotCampaignProgressStore();progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,0);Assert.That(progress.IsRead(PlotChapterKind.Main,500),Is.False);Assert.That(progress.IsRead(PlotChapterKind.Main,100),Is.False);progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,501);Assert.That(progress.IsRead(PlotChapterKind.Main,500),Is.True);Assert.That(progress.IsRead(PlotChapterKind.Main,100),Is.True);Assert.That(progress.IsRead(PlotChapterKind.Daily,100),Is.False);}
        [Test] public void UnknownLegacyProgressCannotGuessOrPartiallyAdvanceThePrefix()
        {var catalog=Catalog();var progress=new PlotCampaignProgressStore();progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,500);Assert.Throws<FormatException>(()=>progress.ImportLegacyProgress(catalog,PlotChapterKind.Main,300));Assert.That(progress.IsRead(PlotChapterKind.Main,500),Is.True);Assert.That(progress.IsRead(PlotChapterKind.Main,100),Is.False);}
        private static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        private static object Invoke(object target,string name,params object[] arguments)=>target.GetType().GetMethod(name,Private).Invoke(target,arguments);
        private static GameObject Object(string name,Transform parent=null,params Type[] components)
        {var root=new GameObject(name,typeof(RectTransform));if(parent!=null)root.transform.SetParent(parent,false);foreach(var type in components)root.AddComponent(type);return root;}
        private static object Transition(PlotLegacyCampaignHost host,Image image,int token,Vector2 position,Vector3 scale)
        {var type=typeof(PlotLegacyCampaignHost).GetNestedType("TransitionState",BindingFlags.NonPublic);var state=Activator.CreateInstance(type,true);type.GetField("token").SetValue(state,token);type.GetField("image").SetValue(state,image);type.GetField("position").SetValue(state,position);type.GetField("scale").SetValue(state,scale);Field(host,"_activeTransition",state);return state;}
        [Test] public void CancellingATransitionRestoresItAndItsLateIteratorCannotResetANewerTransition()
        {
            var root=Object("TransitionReview");var host=root.AddComponent<PlotLegacyCampaignHost>();var image=Object("LoadBackground",root.transform,typeof(Image)).GetComponent<Image>();var position=new Vector2(12,34);var scale=new Vector3(2,3,1);image.rectTransform.anchoredPosition=position;image.transform.localScale=scale;Field(host,"_sprites",new Dictionary<string,Sprite>());
            try
            {
                var previous=Transition(host,image,0,position,scale);var iterator=(IEnumerator)Invoke(host,"Transition",previous,"stage");Assert.That(iterator.MoveNext(),Is.True);Assert.That(image.gameObject.activeSelf,Is.True);Invoke(host,"CancelTransition");Assert.That(image.gameObject.activeSelf,Is.False);Assert.That(image.rectTransform.anchoredPosition,Is.EqualTo(position));Assert.That(image.transform.localScale,Is.EqualTo(scale));
                var nextPosition=new Vector2(55,66);var nextScale=new Vector3(4,5,1);var next=Transition(host,image,1,nextPosition,nextScale);image.gameObject.SetActive(true);image.rectTransform.anchoredPosition=nextPosition;image.transform.localScale=nextScale;Assert.That(iterator.MoveNext(),Is.False);Assert.That(image.gameObject.activeSelf,Is.True);Assert.That(image.rectTransform.anchoredPosition,Is.EqualTo(nextPosition));Assert.That(image.transform.localScale,Is.EqualTo(nextScale));Assert.That(typeof(PlotLegacyCampaignHost).GetField("_activeTransition",Private).GetValue(host),Is.SameAs(next));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void RewardQualityIsAppliedToTheOriginalItemFrame()
        {
            var root=Object("QualityReview");var host=root.AddComponent<PlotLegacyCampaignHost>();var item=Object("Reward",root.transform);var frame=Object("Back",item.transform,typeof(Image)).GetComponent<Image>();Object("Icon",item.transform,typeof(Image));Object("Count",item.transform,typeof(TextMeshProUGUI));Object("None",item.transform);host.itemPresentations.Add(new PlotCampaignItemPresentation{id=7,quality=new Color(.2f,.7f,.4f,1)});
            try{Invoke(host,"BindReward",item,new PlotCampaignItem{itemID=7,count=12});Assert.That(frame.color,Is.EqualTo(new Color(.2f,.7f,.4f,1)));}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private sealed class Backend:IPlotChapterBackend
        {
            public bool fail=true;private readonly PlotPlaybackDocument _data=new PlotPlaybackDocument{Nodes=new List<PlotPlaybackNode>{new PlotPlaybackNode{ScenesID=1}}};
            public Task<PlotChapterContent> LoadAsync(PlotChapterRequest request,CancellationToken cancellation)=>Task.FromResult(new PlotChapterContent(_data,1));
            public Task<PlotChapterAcknowledgement> CompleteAsync(PlotChapterCompletion completion,CancellationToken cancellation)=>fail?Task.FromException<PlotChapterAcknowledgement>(new IOException("server unavailable")):Task.FromResult(new PlotChapterAcknowledgement(true));
        }
        [TestCase(true)][TestCase(false)] public void ReportFailureClosesTheUpperPlayerAndRequiresRetryOrExplicitCancel(bool retry)
        {
            var root=Object("ReportFailureReview");var browser=root.AddComponent<PlotLegacyCampaignHost>();var player=root.GetComponent<PlotPlayerHost>();player.playOnStart=false;var canvas=Object("Browser",root.transform,typeof(Canvas));canvas.GetComponent<Canvas>().sortingOrder=50;var main=Object("Main",canvas.transform);var episodes=Object("Episodes",canvas.transform);var panel=Object("CompletionFailure",canvas.transform);panel.SetActive(false);var status=Object("Status",canvas.transform,typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();var message=Object("Message",panel.transform,typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();var retryButton=Object("RetryCompletion",panel.transform,typeof(Button)).GetComponent<Button>();var cancelButton=Object("CancelCompletion",panel.transform,typeof(Button)).GetComponent<Button>();var mainButton=Object("MainStory",canvas.transform,typeof(Button)).GetComponent<Button>();var dailyButton=Object("DailyStory",canvas.transform,typeof(Button)).GetComponent<Button>();
            Field(browser,"_host",player);Field(browser,"_canvas",canvas);Field(browser,"_main",main);Field(browser,"_episodes",episodes);Field(browser,"_reportFailure",panel);Field(browser,"_status",status);Field(browser,"_reportFailureText",message);Field(browser,"_retry",retryButton);Field(browser,"_cancelReport",cancelButton);Field(browser,"_mainStory",mainButton);Field(browser,"_dailyStory",dailyButton);
            var backend=new Backend();var adapter=new PlotChapterHostPlayer(player);var session=new PlotChapterSession(backend,adapter,browser);Field(browser,"_player",adapter);typeof(PlotLegacyCampaignHost).GetProperty("Session").GetSetMethod(true).Invoke(browser,new object[]{session});
            try
            {
                session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main,"1")).GetAwaiter().GetResult();player.Core.Skip();session.PendingReport.GetAwaiter().GetResult();Assert.That(session.State,Is.EqualTo(PlotChapterState.ReportFailed));Assert.That(player.Core.State,Is.EqualTo(PlotPlayerState.Closed));Assert.That(canvas.activeInHierarchy,Is.True);Assert.That(panel.activeInHierarchy,Is.True);Assert.That(retryButton.interactable,Is.True);Assert.That(cancelButton.interactable,Is.True);Assert.That(mainButton.interactable,Is.False);Assert.That(dailyButton.interactable,Is.False);Assert.That(browser.IsCompletionBlocked,Is.True);Assert.Throws<InvalidOperationException>(()=>browser.BeginSelectedAsync());
                if(retry){backend.fail=false;browser.RetryCompletionAsync().GetAwaiter().GetResult();Assert.That(session.State,Is.EqualTo(PlotChapterState.Completed));}
                else{browser.CancelPendingCompletion();Assert.That(session.State,Is.EqualTo(PlotChapterState.Cancelled));}
                Assert.That(browser.IsCompletionBlocked,Is.False);Assert.That(panel.activeSelf,Is.False);Assert.That(mainButton.interactable,Is.True);Assert.That(dailyButton.interactable,Is.True);Assert.That(browser.LastError,Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
