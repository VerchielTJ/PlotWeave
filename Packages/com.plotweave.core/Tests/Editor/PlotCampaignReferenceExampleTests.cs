using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using PlotWeave.Authoring.Playback;
using PlotWeave.Chapters;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    /// <summary>公开合成样例独立于本地原剧情、美术与布局输入。</summary>
    public sealed class PlotCampaignReferenceExampleTests
    {
        private static readonly DateTime Today=new DateTime(2026,10,28);
        private static PlotChapterRequest Request(PlotChapterKind kind,int id)=>new PlotChapterRequest(kind,id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        [Test] public void SyntheticCatalogSupportsOrderedNonContinuousIdsLocksFirstRewardAndReplay()
        {
            var catalog=PlotCampaignReferenceExample.CreateCatalog();var document=PlotCampaignReferenceExample.CreateDocument();catalog.Validate(document);var progress=new PlotCampaignProgressStore();var backend=new PlotCampaignLocalBackend(catalog,document,progress,()=>Today,area=>area=="studio");
            Assert.Throws<InvalidOperationException>(()=>backend.LoadAsync(Request(PlotChapterKind.Main,500),CancellationToken.None));
            Assert.That(catalog.LockReason(catalog.Chapter(PlotChapterKind.Main,80),progress,Today,area=>area=="studio"),Does.Contain("2099"));
            Assert.That(catalog.LockReason(catalog.Chapter(PlotChapterKind.Main,90),progress,Today,area=>area=="studio"),Does.Contain("observatory"));
            Assert.That(backend.LoadAsync(Request(PlotChapterKind.Daily,700),CancellationToken.None).Result.EntryNode,Is.EqualTo(8101));
            var request=Request(PlotChapterKind.Main,100);Assert.That(backend.LoadAsync(request,CancellationToken.None).Result.RequiresCompletionReport,Is.True);backend.CompleteAsync(new PlotChapterCompletion(request,"public-first"),CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(progress.ItemCount(7),Is.EqualTo(3));Assert.That(progress.ItemCount(9),Is.EqualTo(1));Assert.That(catalog.InitialEpisode(catalog.Chapter(PlotChapterKind.Main,10),progress),Is.EqualTo(500));Assert.That(catalog.Progress(catalog.Chapter(PlotChapterKind.Main,10),progress),Is.EqualTo(50));
            Assert.That(backend.LoadAsync(request,CancellationToken.None).Result.RequiresCompletionReport,Is.False);backend.CompleteAsync(new PlotChapterCompletion(request,"public-replay"),CancellationToken.None).GetAwaiter().GetResult();Assert.That(progress.ItemCount(7),Is.EqualTo(3));Assert.That(progress.ItemCount(9),Is.EqualTo(1));
            Assert.That(backend.LoadAsync(Request(PlotChapterKind.Main,500),CancellationToken.None).Result.EntryNode,Is.EqualTo(2401));
        }

        [Test] public void EverySyntheticEpisodeReachesAnEndAndAllTextHasFiveLanguages()
        {
            var catalog=PlotCampaignReferenceExample.CreateCatalog();var document=PlotCampaignReferenceExample.CreateDocument();var nodes=document.Nodes.ToDictionary(n=>n.ScenesID);var visited=new HashSet<int>();
            foreach(var episode in catalog.episodes){var active=new HashSet<int>();Assert.That(Ends(episode.entryNode,nodes,active,visited),Is.True,"公开样例存在不结束的路径");}Assert.That(visited.Count,Is.EqualTo(nodes.Count));Assert.That(document.Dialogs.Concat(document.Constants).All(t=>t.Languages.Count==5&&t.Languages.All(v=>!string.IsNullOrWhiteSpace(v))),Is.True);Assert.That(document.Actors.All(a=>a.Thespian.StartsWith("reference.",StringComparison.Ordinal)),Is.True);
        }
        private static bool Ends(int id,Dictionary<int,PlotPlaybackNode> nodes,HashSet<int> active,HashSet<int> visited)
        {
            if(id==0)return true;if(!nodes.TryGetValue(id,out var node)||!active.Add(id))return false;visited.Add(id);var result=node.Branch.Count>0?node.Branch.All(b=>Ends(b.Target,nodes,active,visited)):Ends(node.Next,nodes,active,visited);active.Remove(id);return result;
        }

        [Test] public void PublicTemplatesSupplyTheHostNamedControlsAndOwnScrollContracts()
        {
            var templates=PlotCampaignReferenceExample.CreateTemplates(null);try
            {
                foreach(var pair in templates){Assert.That(pair.Value.transform,Is.InstanceOf<RectTransform>());Assert.That(pair.Value.GetComponentsInChildren<PlotLegacyBehaviourProxy>(true),Is.Empty);}
                foreach(var name in new[]{"ChapterPanel","EpisodePanel"}){var panel=templates[name];Assert.That(Find<Button>(panel,"BackBtn"),Is.Not.Null);Assert.That(Find<Button>(panel,"MainBtn"),Is.Not.Null);Assert.That(Find<Button>(panel,"HelpBtn"),Is.Not.Null);CheckScroll(Find<ScrollRect>(panel,"StoryScrollRect"));}
                CheckScroll(Find<ScrollRect>(templates["EpisodePanel"],"AwardScrollRect"));foreach(var name in new[]{"Flyleaf_Titile_txt","Desc","AwardTitle"})Assert.That(Find<TMP_Text>(templates["EpisodePanel"],name),Is.Not.Null);Assert.That(Find<Button>(templates["EpisodePanel"],"SelectBtn"),Is.Not.Null);
                foreach(var name in new[]{"ChapterCard","EpisodeCard","RewardCard"})Assert.That(templates[name].GetComponent<Button>(),Is.Not.Null);
                foreach(var name in new[]{"Back","Icon"})Assert.That(Find<Image>(templates["RewardCard"],name),Is.Not.Null);Assert.That(Find<TMP_Text>(templates["RewardCard"],"Count"),Is.Not.Null);
            }
            finally{foreach(var root in templates.Values)UnityEngine.Object.DestroyImmediate(root);}
        }
        private static T Find<T>(GameObject root,string name)where T:Component=>PlotLegacyWidgets.Find<T>(root.transform,name);
        private static void CheckScroll(ScrollRect scroll){Assert.That(scroll,Is.Not.Null);Assert.That(scroll.viewport,Is.Not.Null);Assert.That(scroll.content,Is.Not.Null);Assert.That(scroll.content.parent,Is.EqualTo(scroll.viewport));Assert.That(scroll.viewport.GetComponent<RectMask2D>(),Is.Not.Null);}

        [Test] public void GeneratedPublicAssetsPlayBrowseRewardReplayAndDailyWithoutLocalInputs()
        {
            if(!File.Exists(PlotCampaignReferenceExample.ScenePath)){Assert.Ignore("先运行公开合成章节样例生成入口");return;}PlotCampaignReferenceExample.VerifyOwnedAssets();PlotCampaignReferenceExample.ValidatePublicDependencies(PlotCampaignReferenceExample.ScenePath);
            // Editor预览场景不要求保存TestRunner当前未命名的临时场景。
            var scene=EditorSceneManager.NewPreviewScene();
            var previousInputs=Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>();
            try
            {
                var browser=new GameObject("Public Sample Integration",typeof(PlotPlayerHost),typeof(PlotLegacyCampaignHost)).GetComponent<PlotLegacyCampaignHost>();SceneManager.MoveGameObjectToScene(browser.gameObject,scene);browser.campaign=AssetDatabase.LoadAssetAtPath<PlotCampaignAsset>(PlotCampaignReferenceExample.CampaignPath);browser.content=AssetDatabase.LoadAssetAtPath<PlotPlaybackAsset>(PlotCampaignReferenceExample.ContentPath);browser.visuals=AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(PlotCampaignReferenceExample.CatalogPath);browser.chapterPrefab=Prefab("ChapterPanel");browser.episodePrefab=Prefab("EpisodePanel");browser.chapterItemPrefab=Prefab("ChapterCard");browser.episodeItemPrefab=Prefab("EpisodeCard");browser.rewardItemPrefab=Prefab("RewardCard");browser.persistProgress=false;browser.unlockedAreas.Add("studio");var progress=new PlotCampaignProgressStore();browser.Initialize(new PlotCampaignLocalBackend(browser.campaign.data,browser.content.data,progress,()=>Today,area=>area=="studio"),progress,()=>Today,area=>area=="studio");
                Assert.That(browser.visuals.fontAsset,Is.Not.Null);Assert.That(AssetDatabase.Contains(browser.visuals.fontAsset.material),Is.True);Assert.That(browser.visuals.fontAsset.atlasTextures.All(AssetDatabase.Contains),Is.True);Assert.That(browser.visuals.sprites.All(s=>s.sprite!=null),Is.True);browser.SelectChapter(10);Assert.That(browser.SelectedEpisode,Is.EqualTo(100));PlayToEnd(browser);Assert.That(progress.ItemCount(7),Is.EqualTo(3));Assert.That(progress.ItemCount(9),Is.EqualTo(1));Assert.That(browser.SelectedEpisode,Is.EqualTo(500));
                browser.SelectEpisode(100);PlayToEnd(browser);Assert.That(progress.ItemCount(7),Is.EqualTo(3));Assert.That(browser.ReturnCount,Is.EqualTo(2));browser.SelectEpisode(500);PlayToEnd(browser);Assert.That(progress.ItemCount(7),Is.EqualTo(8));Assert.That(browser.campaign.data.Progress(browser.campaign.data.Chapter(PlotChapterKind.Main,10),progress),Is.EqualTo(100));
                browser.OpenKind(PlotChapterKind.Daily);browser.SelectChapter(20);PlayToEnd(browser);Assert.That(progress.ItemCount(7),Is.EqualTo(11));Assert.That(progress.ItemCount(9),Is.EqualTo(2));Assert.That(browser.SelectedEpisode,Is.EqualTo(1200));Assert.That(browser.LastError,Is.Empty);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach(var input in Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>())
                    if(!previousInputs.Contains(input)&&input.gameObject.scene.IsValid())UnityEngine.Object.DestroyImmediate(input.gameObject);
            }
        }
        private static GameObject Prefab(string name){var result=AssetDatabase.LoadAssetAtPath<GameObject>(PlotCampaignReferenceExample.Root+"/"+name+".prefab");Assert.That(result,Is.Not.Null,name);return result;}
        private static void PlayToEnd(PlotLegacyCampaignHost browser)
        {
            browser.BeginSelectedAsync().GetAwaiter().GetResult();var core=browser.GetComponent<PlotPlayerHost>().Core;Assert.That(core,Is.Not.Null);for(var i=0;i<32&&browser.Session.State==PlotChapterState.Playing;i++){core.Tick(10);if(core.State==PlotPlayerState.WaitingForChoice)core.Choose(0);else core.OnPlayerInput();}browser.Session.PendingReport.GetAwaiter().GetResult();Assert.That(browser.Session.State,Is.EqualTo(PlotChapterState.Completed));Assert.That(browser.LastError,Is.Empty);
        }
    }
}
