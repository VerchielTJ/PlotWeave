using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlotWeave.Chapters;
using PlotWeave.Playback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PlotWeave.Legacy.Editor
{
    [InitializeOnLoad] public static class PlotLegacyCampaignPreviewVerification
    {
        public const string Key="PlotWeave.Stage16.Preview";
        [Serializable] private sealed class State {public string id,phase,scene,previousScene,error="",image;public bool background,maximized,hadView;public int route,returns,rewardChecks,returnTitleChecks;public double deadline,next;public List<string> images=new List<string>();public List<string> uiHits=new List<string>();}
        static PlotLegacyCampaignPreviewVerification()
        {EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=change=>{if(change!=PlayModeStateChange.EnteredEditMode)return;var s=Read();if(s==null)return;Restore(s);s.phase=s.error==""&&s.returns==3&&s.images.Count==3&&s.images.All(File.Exists)&&s.rewardChecks>=3&&s.returnTitleChecks>=5&&s.uiHits.Count>=13?"passed":"failed";if(s.phase=="failed"&&s.error=="")s.error="原章节界面检查未完成";Record(s);SessionState.EraseString(Key);};}
        private static State Read(){var text=SessionState.GetString(Key,"");return text==""?null:JsonUtility.FromJson<State>(text);}
        private static void Save(State s)=>SessionState.SetString(Key,JsonUtility.ToJson(s));
        private static void Record(State s){Directory.CreateDirectory("Doc/Records/Stage16");File.WriteAllText("Doc/Records/Stage16/preview-"+s.id+".json",JsonUtility.ToJson(s,true));}
        private static EditorWindow Game()=>Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.GameView");
        private static void Restore(State s){Application.runInBackground=s.background;EditorSceneManager.playModeStartScene=s.previousScene==""?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(s.previousScene);var game=Game();if(game!=null&&s.hadView)game.maximized=s.maximized;}
        public static void Start()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Read()!=null)throw new InvalidOperationException("已有运行中的检查");for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("当前场景未保存");var report=JsonUtility.FromJson<PlotLegacyCampaignMigration.Report>(File.ReadAllText(PlotLegacyCampaignMigration.LatestPath));var s=new State{id=Guid.NewGuid().ToString("N"),phase="start",scene=report.scene,previousScene=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),background=Application.runInBackground,deadline=EditorApplication.timeSinceStartup+180};Save(s);Record(s);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(s.scene);EditorApplication.isPlaying=true;
        }
        private static void Capture(State s,string name,string next)
        {s.image="Tools/PlotWeaveMigration/Inputs/Images/campaign-"+name+"-"+s.id+".png";Directory.CreateDirectory(Path.GetDirectoryName(s.image));s.images.Add(s.image);s.phase=next;Save(s);Record(s);ScreenCapture.CaptureScreenshot(Path.GetFullPath(s.image));}
        private static Button Button(Component root,string name)=>root.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
        private static string HierarchyPath(Transform transform)
        {var names=new Stack<string>();for(var current=transform;current!=null;current=current.parent)names.Push(current.name);return string.Join("/",names);}
        private static void Click(State state,Button button,string label)
        {
            if(button==null||!button.IsActive()||!button.IsInteractable())throw new InvalidOperationException("按钮未处于可交互状态: "+label);var system=EventSystem.current;if(system==null||!system.isActiveAndEnabled)throw new InvalidOperationException("场景没有活动EventSystem");Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.rootCanvas.worldCamera:null;var center=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            if(center.x<0||center.x>Screen.width||center.y<0||center.y>Screen.height)throw new InvalidOperationException("按钮中心超出屏幕: "+label+" @"+center);
            var pointer=new PointerEventData(system){position=center,pressPosition=center,button=PointerEventData.InputButton.Left,pointerId=-1,clickCount=1,eligibleForClick=true};var hits=new List<RaycastResult>();system.RaycastAll(pointer,hits);var top=hits.FirstOrDefault(hit=>hit.gameObject!=null);if(top.gameObject==null)throw new InvalidOperationException("按钮中心没有UI射线命中: "+label+" @"+center);
            var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);if(handler!=button.gameObject)throw new InvalidOperationException("按钮中心被其他UI遮挡: "+label+"; top="+HierarchyPath(top.gameObject.transform)+"; handler="+(handler!=null?HierarchyPath(handler.transform):"<none>"));
            pointer.pointerCurrentRaycast=top;pointer.pointerPressRaycast=top;pointer.pointerPress=handler;pointer.rawPointerPress=top.gameObject;
            if(!ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler))throw new InvalidOperationException("命中的UI没有响应点击: "+label);state.uiHits.Add(label+" -> "+HierarchyPath(top.gameObject.transform)+" @"+center);Save(state);
        }
        private static void ClickChapter(State state,PlotLegacyCampaignHost browser)
        {var list=browser.GetComponentsInChildren<PlotLegacyVirtualList>().Single(l=>l.name=="StoryScrollRect"&&l.GetComponent<ScrollRect>().horizontal);Click(state,list.GetComponentsInChildren<Button>().First(b=>b.IsInteractable()),browser.Kind+"章节项");}
        private static void ClickEpisode(State state,PlotLegacyCampaignHost browser,int id)
        {
            var name=browser.campaign.data.Episode(browser.Kind,id).name;var constant=browser.content.data.Constants.Find(t=>t.Key==name);if(constant!=null&&constant.Languages.Count>0&&!string.IsNullOrEmpty(constant.Languages[0]))name=constant.Languages[0];
            var list=browser.GetComponentsInChildren<PlotLegacyVirtualList>().Single(l=>l.name=="StoryScrollRect"&&l.GetComponent<ScrollRect>().vertical);var button=list.GetComponentsInChildren<Button>().Single(b=>b.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.name=="PlotName"&&t.text==name));Click(state,button,"重看小节"+id);
        }
        private static void CheckReturnTitle(State state,PlotLegacyCampaignHost browser)
        {
            var bar=browser.GetComponentsInChildren<Transform>().Single(t=>t.name=="ReturnBtn");var direct=bar.Find("Title");var title=direct!=null?direct.GetComponent<TMP_Text>():null;if(title==null)title=bar.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Title");var expected=browser.Kind==PlotChapterKind.Main?"主线故事":"日常故事";if(title.text!=expected)throw new InvalidOperationException("章节返回栏标题错误: "+title.text+"，预期 "+expected);state.returnTitleChecks++;
        }
        private static void VisibleInViewport(RectTransform rect,RectTransform viewport,string label)
        {
            if(!rect.gameObject.activeInHierarchy||rect.rect.width<=.5f||rect.rect.height<=.5f)throw new InvalidOperationException("奖励控件没有可见范围: "+label);var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(viewport.InverseTransformPoint).ToArray();var bounds=UnityEngine.Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));if(!viewport.rect.Overlaps(bounds,true))throw new InvalidOperationException("奖励控件位于viewport外: "+label);
        }
        private static void CheckRewards(State state,PlotLegacyCampaignHost browser)
        {
            Canvas.ForceUpdateCanvases();CheckReturnTitle(state,browser);var list=browser.GetComponentsInChildren<PlotLegacyVirtualList>().Single(l=>l.name=="AwardScrollRect");var scroll=list.GetComponent<ScrollRect>();var texts=list.GetComponentsInChildren<TMP_Text>();var counts=texts.Where(t=>t.name=="Count").ToList();if(counts.Count!=2||counts.Any(t=>t.text!="10"))throw new InvalidOperationException("奖励数量UI应为两个10");var names=texts.Where(t=>t.name=="RewardItemName").ToList();if(names.Count!=2||!names.Any(t=>t.text.Contains("1001"))||!names.Any(t=>t.text.Contains("1002")))throw new InvalidOperationException("缺图奖励未显示两个道具ID");
            foreach(var count in counts){var slot=count.transform;while(slot.parent!=scroll.content){slot=slot.parent;if(slot==null)throw new InvalidOperationException("奖励控件不属于列表内容");}var back=slot.GetComponentsInChildren<Image>(true).Single(image=>image.name=="Back");VisibleInViewport(back.rectTransform,scroll.viewport,"Back");VisibleInViewport(count.rectTransform,scroll.viewport,"Count");}foreach(var name in names)VisibleInViewport(name.rectTransform,scroll.viewport,"RewardItemName");state.rewardChecks++;
        }
        private static void Begin(State state,PlotLegacyCampaignHost browser)
        {Click(state,Button(browser,"SelectBtn"),browser.Kind+"开始/重看");state.phase="playing";state.next=EditorApplication.timeSinceStartup+1.2;Save(state);}
        private static void Update()
        {
            var s=Read();if(s==null||EditorApplication.isCompiling)return;
            try
            {
                if(EditorApplication.timeSinceStartup>s.deadline)throw new TimeoutException("原章节界面运行检查超时");if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();var game=Game();if(game!=null){if(!s.hadView){s.hadView=true;s.maximized=game.maximized;game.maximized=true;Save(s);}game.Repaint();}
                var browser=UnityEngine.Object.FindObjectOfType<PlotLegacyCampaignHost>();if(browser==null||browser.Session==null)return;if(browser.LastError!="")throw new Exception(browser.LastError);var player=browser.GetComponent<PlotPlayerHost>();
                if(s.phase=="start"){s.phase="main-frame";s.next=EditorApplication.timeSinceStartup+1.2;Save(s);return;}
                if(s.phase=="main-frame"){if(EditorApplication.timeSinceStartup<s.next)return;CheckReturnTitle(s,browser);Capture(s,"chapters","main-capture");return;}
                if(s.phase=="main-capture"){if(!File.Exists(s.image))return;ClickChapter(s,browser);s.phase="opening";Save(s);return;}
                if(s.phase=="opening"){if(browser.SelectedEpisode!=1001)return;s.phase="episode-frame";s.next=EditorApplication.timeSinceStartup+1;Save(s);return;}
                if(s.phase=="episode-frame"){if(EditorApplication.timeSinceStartup<s.next)return;CheckRewards(s,browser);Capture(s,"episodes","episode-capture");return;}
                if(s.phase=="episode-capture"){if(!File.Exists(s.image))return;Begin(s,browser);return;}
                if(s.phase=="replay-select"){if(EditorApplication.timeSinceStartup<s.next)return;ClickEpisode(s,browser,1001);s.phase="replay-frame";s.next=EditorApplication.timeSinceStartup+1;Save(s);return;}
                if(s.phase=="replay-frame"){if(EditorApplication.timeSinceStartup<s.next)return;CheckRewards(s,browser);Begin(s,browser);return;}
                if(s.phase=="daily-switch"){if(EditorApplication.timeSinceStartup<s.next)return;Click(s,Button(browser,"DailyStory"),"切换日常故事");s.phase="daily-chapters";s.next=EditorApplication.timeSinceStartup+1.2;Save(s);return;}
                if(s.phase=="daily-chapters"){if(EditorApplication.timeSinceStartup<s.next)return;CheckReturnTitle(s,browser);ClickChapter(s,browser);s.phase="daily-opening";Save(s);return;}
                if(s.phase=="daily-opening"){if(browser.Kind!=PlotChapterKind.Daily||browser.SelectedEpisode!=1001)return;s.phase="daily-episode-frame";s.next=EditorApplication.timeSinceStartup+1;Save(s);return;}
                if(s.phase=="daily-episode-frame"){if(EditorApplication.timeSinceStartup<s.next)return;CheckRewards(s,browser);Begin(s,browser);return;}
                if(s.phase=="playing")
                {if(browser.Session.State!=PlotChapterState.Playing||player.Core==null)return;if(player.Core.State==PlotPlayerState.Faulted)throw new Exception("故事播放失败");player.Core.Tick(1);if(player.Core.GetCurrentNodeID()==0||!player.Core.IsReady||EditorApplication.timeSinceStartup<s.next)return;Click(s,player.View.GetComponent<PlotLegacySkipController>().skipButton,"打开跳过确认");s.next=EditorApplication.timeSinceStartup+1;s.phase="skip-frame";Save(s);return;}
                if(s.phase=="skip-frame")
                {if(EditorApplication.timeSinceStartup<s.next)return;if(s.route==0){Capture(s,"skip-confirmation","skip-capture");return;}s.phase="skip-capture";Save(s);}
                if(s.phase=="skip-capture")
                {
                    if(!File.Exists(s.image))return;Click(s,Button(player.View,"ConfirmBtn"),"确认跳过");if(browser.Session.State!=PlotChapterState.Completed)throw new Exception("确认跳过未完成结算");s.returns=browser.ReturnCount;
                    var progress=(PlotCampaignProgressStore)browser.Progress;var expected=s.route<2?10:20;if(progress.ItemCount(1001)!=expected||progress.ItemCount(1002)!=expected)throw new Exception("首次奖励数量错误或重看重复发奖");if(!progress.IsRead(PlotChapterKind.Main,1001))throw new Exception("主线进度未提交");
                    s.route++;if(s.route==1){s.phase="replay-select";s.next=EditorApplication.timeSinceStartup+1.2;}else if(s.route==2){s.phase="daily-switch";s.next=EditorApplication.timeSinceStartup+1.2;}else{if(!progress.IsRead(PlotChapterKind.Daily,1001)||browser.ReturnCount!=3)throw new Exception("日常进度或返回次数错误");Save(s);Record(s);EditorApplication.isPlaying=false;return;}Save(s);
                }
            }
            catch(Exception error){s.error=error.ToString();Save(s);Record(s);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;else{Restore(s);SessionState.EraseString(Key);}Debug.LogException(error);}
        }
    }
}
