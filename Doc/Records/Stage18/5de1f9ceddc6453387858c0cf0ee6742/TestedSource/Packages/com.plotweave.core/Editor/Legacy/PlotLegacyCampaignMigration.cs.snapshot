using System;
using System.IO;
using System.Linq;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using PlotWeave.Chapters;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Legacy.Editor
{
    public static class PlotLegacyCampaignMigration
    {
        public const string LatestPath="Tools/PlotWeaveMigration/campaign-ui.json";
        [Serializable] public sealed class Report{public string id,status,error,root,scene,campaign,content,visuals;public int chapters,episodes,rewards;}
        [MenuItem("Tools/PlotWeave/迁移工具/连接原章节与奖励界面")]
        public static void PrepareVerification()
        {
            var ui=JsonUtility.FromJson<PlotLegacyUiReport>(File.ReadAllText(PlotLegacyUiMigration.LatestPath));var data=JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));var interaction=JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            var report=new Report{id=Guid.NewGuid().ToString("N"),status="running",campaign=data.catalog,visuals=interaction.catalog};report.root=ui.root+"/Campaign_"+report.id.Substring(0,8);Directory.CreateDirectory(report.root);AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                GameObject Prefab(string ending){var item=ui.layouts.SingleOrDefault(l=>l.source.EndsWith(ending,StringComparison.Ordinal));if(item==null)throw new InvalidOperationException("原布局尚未重建: "+ending);return AssetDatabase.LoadAssetAtPath<GameObject>(item.asset);}
                var browser=new GameObject("OriginalStoryBrowser",typeof(PlotPlayerHost),typeof(PlotLegacyCampaignHost)).GetComponent<PlotLegacyCampaignHost>();var player=browser.GetComponent<PlotPlayerHost>();player.playOnStart=false;browser.campaign=AssetDatabase.LoadAssetAtPath<PlotCampaignAsset>(report.campaign);browser.visuals=AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(report.visuals);browser.content=ScriptableObject.CreateInstance<PlotPlaybackAsset>();browser.content.data=PlotPlaybackCompiler.Compile(AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(data.roundtripWorkspace));report.content=report.root+"/Content.asset";AssetDatabase.CreateAsset(browser.content,report.content);
                browser.chapterPrefab=Prefab("/PlotMianUI.prefab");browser.episodePrefab=Prefab("/PlotChapterUI.prefab");browser.chapterItemPrefab=Prefab("/PlotItemNode.prefab");browser.episodeItemPrefab=Prefab("/PlotItemSelect.prefab");browser.rewardItemPrefab=Prefab("/PropNode_Award.prefab");browser.persistProgress=false;browser.unlockedAreas=browser.campaign.data.chapters.Select(c=>c.unlockArea).Where(a=>!string.IsNullOrEmpty(a)).Distinct().ToList();
                report.scene=report.root+"/OriginalCampaign.unity";EditorSceneManager.SaveScene(scene,report.scene);
                var progress=new PlotCampaignProgressStore();browser.Initialize(new PlotCampaignLocalBackend(browser.campaign.data,browser.content.data,progress,()=>new DateTime(2030,1,1),_=>true),progress,()=>new DateTime(2030,1,1),_=>true);browser.SelectChapter(browser.campaign.data.chapters.First(c=>c.kind==PlotChapterKind.Main).id);if(browser.SelectedEpisode!=1001)throw new InvalidOperationException("原主线小节初始选择错误");browser.OpenKind(PlotChapterKind.Daily);browser.SelectChapter(browser.campaign.data.chapters.First(c=>c.kind==PlotChapterKind.Daily).id);if(browser.SelectedEpisode!=1001)throw new InvalidOperationException("原日常小节初始选择错误");
                if(browser.LastError!="")throw new InvalidOperationException(browser.LastError);report.chapters=browser.campaign.data.chapters.Count;report.episodes=browser.campaign.data.episodes.Count;report.rewards=browser.campaign.data.rewards.Count;report.status="prepared-runtime-verification-pending";File.WriteAllText(LatestPath,JsonUtility.ToJson(report,true));
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();throw;}
            finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);Directory.CreateDirectory("Doc/Records/Stage16");File.WriteAllText("Doc/Records/Stage16/campaign-"+report.id+".json",JsonUtility.ToJson(report,true));}
        }
    }
}
