using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PlotWeave.Chapters;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    /// <summary>只运行公开合成场景；跨PlayMode域重载保存状态，以实际UI射线和点击验收。</summary>
    [InitializeOnLoad]
    public static class PlotCampaignReferencePreviewVerification
    {
        public const string Key="PlotWeave.Stage18.ReferencePreview";
        public const string LatestPath="Doc/Records/Stage18/reference-preview-latest.json";
        private const string Images="Tools/PlotWeaveMigration/Inputs/Images";

        [Serializable] public sealed class ImageEvidence
        {public string label,path,sha256;public int width,height;}
        [Serializable] public sealed class Report
        {
            public string id,status="scheduled",phase="start",error="",scene=PlotCampaignReferenceExample.ScenePath;
            public string startedAtUtc,recordedAtUtc,previousScene="",pendingImage="",afterImage="";
            public bool background,maximized,hadView,readsOriginalContent;
            public int returnTitleChecks,rewardChecks,playerChecks,skipCancelChecks,branchChecks,inventoryChecks,lockChecks,returned;
            public double deadline,next;
            public List<ImageEvidence> images=new List<ImageEvidence>();
            public List<string> uiHits=new List<string>();
            public List<string> checks=new List<string>();
        }

        static PlotCampaignReferencePreviewVerification()
        {EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=Changed;}
        public static bool IsBusy=>Read()!=null;
        public static string GetReportPath(string requestId)
        {ValidateId(requestId);return "Doc/Records/Stage18/reference-preview-"+requestId+".json";}
        public static Report ReadReport(string requestId)
        {var path=GetReportPath(requestId);return File.Exists(path)?JsonUtility.FromJson<Report>(File.ReadAllText(path)):null;}

        /// <summary>桥保存scheduled后轮询GetReportPath(requestId)的status；会话Key释放后才可派下一个请求。</summary>
        public static void Begin(string requestId)
        {
            ValidateId(requestId);
            if(EditorApplication.isPlayingOrWillChangePlaymode||Read()!=null)throw new InvalidOperationException("已有运行中的公开样例检查");
            foreach(var key in new[]{"PlotWeave.Stage05.Preview","PlotWeave.Stage08.Preview","PlotWeave.Stage10.Preview","PlotWeave.Stage14.Preview","PlotWeave.Stage16.Preview","PlotWeave.Stage17.MobileBuild"})
                if(SessionState.GetString(key,"")!="")throw new InvalidOperationException("其他验收会话尚未释放");
            for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("当前场景未保存");
            if(File.Exists(GetReportPath(requestId)))throw new InvalidOperationException("该requestId已有报告，请使用新的请求ID");
            PlotCampaignReferenceExample.VerifyOwnedAssets();PlotCampaignReferenceExample.ValidatePublicDependencies(PlotCampaignReferenceExample.ScenePath);
            var scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(PlotCampaignReferenceExample.ScenePath);if(scene==null)throw new FileNotFoundException("先生成Stage18公开合成场景");
            var state=new Report{id=requestId,startedAtUtc=DateTime.UtcNow.ToString("O"),previousScene=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),background=Application.runInBackground,deadline=EditorApplication.timeSinceStartup+240,readsOriginalContent=false};
            Save(state);Record(state);EditorSceneManager.playModeStartScene=scene;EditorApplication.isPlaying=true;
        }
        private static void ValidateId(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("requestId须为N格式GUID");}
        private static Report Read(){var value=SessionState.GetString(Key,"");return value==""?null:JsonUtility.FromJson<Report>(value);}
        private static void Save(Report state)=>SessionState.SetString(Key,JsonUtility.ToJson(state));
        private static void Record(Report state)
        {
            state.recordedAtUtc=DateTime.UtcNow.ToString("O");Directory.CreateDirectory("Doc/Records/Stage18");var json=JsonUtility.ToJson(state,true);File.WriteAllText(GetReportPath(state.id),json);File.WriteAllText(LatestPath,json);
        }
        private static EditorWindow Game()=>Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.GameView");
        private static void Restore(Report state)
        {
            Application.runInBackground=state.background;EditorSceneManager.playModeStartScene=state.previousScene==""?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousScene);var game=Game();if(game!=null&&state.hadView)game.maximized=state.maximized;
        }
        private static void Changed(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredEditMode)return;var state=Read();if(state==null)return;
            try
            {
                Restore(state);
                var complete=state.phase=="stopping"&&state.error==""&&state.returned==5&&state.returnTitleChecks>=8&&state.rewardChecks>=6&&state.playerChecks>=5&&state.skipCancelChecks==1&&state.branchChecks==1&&state.inventoryChecks>=5&&state.lockChecks>=2&&state.uiHits.Count>=27&&state.images.Count>=8&&state.images.All(i=>i.width>100&&i.height>100&&File.Exists(i.path));
                state.status=complete?"passed":"failed";if(!complete&&state.error=="")state.error="公开合成章节运行检查未完整结束（可能手动退出了PlayMode）";state.phase=complete?"complete":"failed";Record(state);
            }
            catch(Exception error){state.status="failed";state.error=error.ToString();Record(state);}
            finally{SessionState.EraseString(Key);}
        }
        private static void Phase(Report state,string phase,double delay=.3)
        {state.phase=phase;state.next=EditorApplication.timeSinceStartup+delay;Save(state);Record(state);}
        private static void Capture(Report state,string label,string next)
        {
            Directory.CreateDirectory(Images);state.pendingImage=Images+"/reference-campaign-"+label+"-"+state.id+".png";state.afterImage=next;state.images.Add(new ImageEvidence{label=label,path=state.pendingImage});Phase(state,"capture",.2);ScreenCapture.CaptureScreenshot(Path.GetFullPath(state.pendingImage));
        }
        private static bool FinishCapture(Report state)
        {
            if(!File.Exists(state.pendingImage))return false;byte[] data;try{data=File.ReadAllBytes(state.pendingImage);}catch(IOException){return false;}if(data.Length<24)return false;
            if(data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71)throw new InvalidDataException("截图不是PNG");if(data[data.Length-8]!=73||data[data.Length-7]!=69||data[data.Length-6]!=78||data[data.Length-5]!=68)return false;var image=state.images.Last();image.width=BigEndian(data,16);image.height=BigEndian(data,20);if(image.width<=100||image.height<=100)throw new InvalidDataException("截图分辨率无效");using(var hash=SHA256.Create())image.sha256=BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant();Phase(state,state.afterImage,.2);return true;
        }
        private static int BigEndian(byte[] data,int offset)=>(data[offset]<<24)|(data[offset+1]<<16)|(data[offset+2]<<8)|data[offset+3];
        private static T Find<T>(Component root,string name)where T:Component=>root.GetComponentsInChildren<T>(false).Single(c=>c.name==name);
        private static string PathOf(Transform node){var names=new Stack<string>();for(var current=node;current!=null;current=current.parent)names.Push(current.name);return string.Join("/",names);}

        // 所有业务动作先由EventSystem射线确认最上层命中，再发送真实UGUI pointer事件；不直接Invoke onClick。
        private static void Click(Report state,Button button,string label)
        {
            if(button==null||!button.IsActive()||!button.IsInteractable())throw new InvalidOperationException("按钮不可交互: "+label);var system=EventSystem.current;if(system==null||!system.isActiveAndEnabled)throw new InvalidOperationException("没有活动EventSystem");Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.rootCanvas.worldCamera:null;var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));if(point.x<0||point.y<0||point.x>Screen.width||point.y>Screen.height)throw new InvalidOperationException("按钮中心在屏幕外: "+label);
            var pointer=new PointerEventData(system){position=point,pressPosition=point,button=PointerEventData.InputButton.Left,pointerId=-1,clickCount=1,eligibleForClick=true};var hits=new List<RaycastResult>();system.RaycastAll(pointer,hits);var top=hits.FirstOrDefault(hit=>hit.gameObject!=null);if(top.gameObject==null)throw new InvalidOperationException("按钮中心没有射线命中: "+label);var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);
            if(handler!=button.gameObject)throw new InvalidOperationException("按钮被其他UI遮挡: "+label+"; top="+PathOf(top.gameObject.transform)+"; handler="+(handler==null?"<none>":PathOf(handler.transform)));
            pointer.pointerCurrentRaycast=top;pointer.pointerPressRaycast=top;pointer.pointerPress=handler;pointer.rawPointerPress=top.gameObject;ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerUpHandler);if(!ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler))throw new InvalidOperationException("UI没有响应pointerClick: "+label);
            state.uiHits.Add(label+" -> "+PathOf(top.gameObject.transform)+" @"+point);Save(state);
        }
        private static PlotLegacyVirtualList List(PlotLegacyCampaignHost browser,string name,bool horizontal)
        {return browser.GetComponentsInChildren<PlotLegacyVirtualList>(false).Single(l=>l.name==name&&l.GetComponent<ScrollRect>().horizontal==horizontal);}
        private static Button CatalogButton(PlotLegacyCampaignHost browser,bool chapter,int id)
        {
            var name=chapter?browser.campaign.data.Chapter(browser.Kind,id).name:browser.campaign.data.Episode(browser.Kind,id).name;var list=List(browser,"StoryScrollRect",chapter);return list.GetComponentsInChildren<Button>(false).Single(b=>b.GetComponentsInChildren<TMP_Text>(false).Any(t=>t.name=="PlotName"&&t.text==name));
        }
        private static void CheckTitle(Report state,PlotLegacyCampaignHost browser)
        {
            var bar=Find<Transform>(browser,"ReturnBtn");var direct=bar.Find("Title");var title=direct==null?null:direct.GetComponent<TMP_Text>();var expected=browser.Kind==PlotChapterKind.Main?"主线故事":"日常故事";if(title==null||title.text!=expected)throw new InvalidOperationException("返回栏标题错误，预期: "+expected);Visible(title.rectTransform,null,"ReturnTitle");state.returnTitleChecks++;Save(state);
        }
        private static void Visible(RectTransform rect,RectTransform viewport,string label)
        {
            if(rect==null||!rect.gameObject.activeInHierarchy||rect.rect.width<=.5f||rect.rect.height<=.5f)throw new InvalidOperationException("控件没有可见几何: "+label);var corners=new Vector3[4];rect.GetWorldCorners(corners);
            if(viewport!=null){var points=corners.Select(viewport.InverseTransformPoint).ToArray();var bounds=UnityEngine.Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));if(!viewport.rect.Overlaps(bounds,true))throw new InvalidOperationException("控件位于viewport外: "+label);}
            var canvas=rect.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.rootCanvas.worldCamera:null;var screen=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();var screenBounds=UnityEngine.Rect.MinMaxRect(screen.Min(p=>p.x),screen.Min(p=>p.y),screen.Max(p=>p.x),screen.Max(p=>p.y));if(!new UnityEngine.Rect(0,0,Screen.width,Screen.height).Overlaps(screenBounds,true))throw new InvalidOperationException("控件位于屏幕外: "+label);
            foreach(var group in rect.GetComponentsInParent<CanvasGroup>(true))if(group.alpha<=.01f)throw new InvalidOperationException("控件被CanvasGroup隐藏: "+label);foreach(var graphic in rect.GetComponents<Graphic>())if(graphic.color.a<=.01f)throw new InvalidOperationException("控件透明: "+label);
        }
        private static void CheckRewards(Report state,PlotLegacyCampaignHost browser)
        {
            Canvas.ForceUpdateCanvases();CheckTitle(state,browser);var list=List(browser,"AwardScrollRect",true);var scroll=list.GetComponent<ScrollRect>();var expected=browser.campaign.data.Reward(browser.campaign.data.Episode(browser.Kind,browser.SelectedEpisode).awardID);var roots=new List<Transform>();for(var i=0;i<scroll.content.childCount;i++){var child=scroll.content.GetChild(i);if(child.gameObject.activeInHierarchy)roots.Add(child);}if(roots.Count!=expected.Count)throw new InvalidOperationException("奖励可见项数量不符");
            for(var i=0;i<roots.Count;i++)
            {
                var root=roots[i];var count=Find<TMP_Text>(root,"Count");var icon=Find<Image>(root,"Icon");var back=Find<Image>(root,"Back");var item=expected[i];var presentation=browser.itemPresentations.Single(p=>p.id==item.itemID);if(count.text!=item.count.ToString(CultureInfo.InvariantCulture))throw new InvalidOperationException("奖励显示数量错误");if(icon.sprite!=presentation.icon||icon.sprite==null||back.color!=presentation.quality)throw new InvalidOperationException("奖励图标/品质表现未绑定");Visible(count.rectTransform,scroll.viewport,"RewardCount");Visible(icon.rectTransform,scroll.viewport,"RewardIcon");Visible(back.rectTransform,scroll.viewport,"RewardBack");
            }
            state.rewardChecks++;state.checks.Add("奖励UI kind="+browser.Kind+" episode="+browser.SelectedEpisode+" items="+expected.Count);Save(state);
        }
        private static void CheckLocks(Report state,PlotLegacyCampaignHost browser)
        {
            foreach(var id in new[]{80,90}){var button=CatalogButton(browser,true,id);if(button.interactable)throw new InvalidOperationException("受日期/区域限制的章节仍可点击");var lockText=Find<TMP_Text>(button,"LockText");if(string.IsNullOrEmpty(lockText.text)||!lockText.gameObject.activeInHierarchy)throw new InvalidOperationException("章节锁说明没有显示");Visible(lockText.rectTransform,List(browser,"StoryScrollRect",true).GetComponent<ScrollRect>().viewport,"ChapterLock");state.lockChecks++;}Save(state);
        }
        private static void CheckPlayer(Report state,PlotPlayerHost player,string expectedBackground)
        {
            Canvas.ForceUpdateCanvases();var view=player.View;if(view==null||player.Core==null||player.Core.State==PlotPlayerState.Faulted||!player.Core.IsReady)throw new InvalidOperationException("播放器没有处于可见就绪状态");var document=PlotCampaignReferenceExample.CreateDocument();var node=document.Nodes.Single(n=>n.ScenesID==player.Core.GetCurrentNodeID());var expected=document.Dialogs.Single(t=>t.Key==node.Dialog).Languages[0];if(view.FullText!=expected||view.VisibleText!=expected||view.BackgroundKey!=expectedBackground||view.ActorCount!=1)throw new InvalidOperationException("公开剧情的文字/背景/角色显示不完整或与节点内容不符");Visible(view.DialogText.rectTransform,null,"PlayerDialog");view.DialogText.ForceMeshUpdate();if(view.DialogText.textInfo.characterCount==0)throw new InvalidOperationException("对白没有生成字形几何");var background=Find<Image>(view,"StageBack");if(background.sprite==null)throw new InvalidOperationException("背景图片缺失");Visible(background.rectTransform,null,"PlayerBackground");foreach(var actor in view.ActorObjects){var image=actor.GetComponentInChildren<Image>();if(image==null||image.sprite==null)throw new InvalidOperationException("合成角色图片缺失");Visible(image.rectTransform,null,"PlayerActor");}state.playerChecks++;Save(state);
        }
        private static void CheckInventory(Report state,PlotLegacyCampaignHost browser,int coin,int gem,int returned)
        {
            var progress=browser.Progress as PlotCampaignProgressStore;if(progress==null||progress.ItemCount(7)!=coin||progress.ItemCount(9)!=gem||browser.ReturnCount!=returned||browser.Session.State!=PlotChapterState.Completed)throw new InvalidOperationException("首次奖励/重看账本或返回次数不符，预期coin="+coin+" gem="+gem+" returns="+returned);state.returned=returned;state.inventoryChecks++;state.checks.Add("奖励账本 coin="+coin+" gem="+gem+" returns="+returned);Save(state);
        }
        private static void BeginSelected(Report state,PlotLegacyCampaignHost browser,string phase)
        {Click(state,Find<Button>(browser,"SelectBtn"),browser.Kind+"开始/重看"+browser.SelectedEpisode);Phase(state,phase,.7);}
        private static bool Ready(PlotLegacyCampaignHost browser,PlotPlayerHost player)
        {
            if(browser.Session.State!=PlotChapterState.Playing||player.Core==null)return false;if(player.Core.State==PlotPlayerState.Faulted)throw new InvalidOperationException("公开合成故事播放失败");player.Core.Tick(.2f);return player.Core.IsReady;
        }
        private static void Stop(Report state)
        {state.status="stopping";Phase(state,"stopping",0);EditorApplication.isPlaying=false;}

        private static void Update()
        {
            var state=Read();if(state==null||EditorApplication.isCompiling)return;
            try
            {
                if(state.phase=="stopping")return;if(EditorApplication.timeSinceStartup>state.deadline)throw new TimeoutException("公开合成章节视觉验收超时: "+state.phase);if(!EditorApplication.isPlaying)return;
                Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();var game=Game();if(game!=null){if(!state.hadView){state.hadView=true;state.maximized=game.maximized;game.maximized=true;Save(state);}game.Repaint();}
                var browser=UnityEngine.Object.FindObjectsOfType<PlotLegacyCampaignHost>().SingleOrDefault(b=>b.gameObject.scene.path==PlotCampaignReferenceExample.ScenePath);if(browser==null||browser.Session==null)return;if(browser.LastError!="")throw new InvalidOperationException(browser.LastError);var player=browser.GetComponent<PlotPlayerHost>();if(EditorApplication.timeSinceStartup<state.next)return;
                switch(state.phase)
                {
                    case "start":state.status="running";Phase(state,"main-frame",1.2);break;
                    case "main-frame":CheckTitle(state,browser);CheckLocks(state,browser);Capture(state,"main-chapters","main-click");break;
                    case "capture":FinishCapture(state);break;
                    case "main-click":Click(state,CatalogButton(browser,true,10),"主线章节10");Phase(state,"main-opening",.8);break;
                    case "main-opening":if(browser.SelectedEpisode!=100)break;CheckRewards(state,browser);Capture(state,"main-episodes","reward-detail-click");break;
                    case "reward-detail-click":Click(state,List(browser,"AwardScrollRect",true).GetComponentsInChildren<Button>().First(),"查看奖励道具详情");Phase(state,"reward-detail");break;
                    case "reward-detail":
                        var detail=Find<Transform>(browser,"StoryDetail");if(!Find<TMP_Text>(detail,"Title").text.Contains("旅途徽章")||!Find<TMP_Text>(detail,"Body").text.Contains("3"))throw new InvalidOperationException("奖励详情内容不符");Visible(Find<TMP_Text>(detail,"Body").rectTransform,null,"RewardDetail");Click(state,Find<Button>(detail,"CloseDetail"),"关闭奖励详情");Phase(state,"first-begin");break;
                    case "first-begin":BeginSelected(state,browser,"first-playing");break;
                    case "first-playing":if(!Ready(browser,player))break;CheckPlayer(state,player,"reference.blue");Capture(state,"player-dialog","first-skip");break;
                    case "first-skip":Click(state,player.View.GetComponent<PlotLegacySkipController>().skipButton,"首次打开跳过确认");Phase(state,"skip-cancel-frame");break;
                    case "skip-cancel-frame":
                        var skip=player.View.GetComponent<PlotLegacySkipController>();if(!skip.IsShowing||!player.Core.IsPaused)throw new InvalidOperationException("跳过确认没有暂停播放器");var message=Find<TMP_Text>(player.View,"ContentText");if(message.text!=browser.content.data.Constants.Single(t=>t.Key=="reference.skip").Languages[0])throw new InvalidOperationException("跳过确认没有显示本地化提示");Visible(message.rectTransform,null,"SkipMessage");Capture(state,"skip-confirmation","skip-cancel");break;
                    case "skip-cancel":Click(state,Find<Button>(player.View,"CancelBtn"),"取消跳过");Phase(state,"skip-cancelled");break;
                    case "skip-cancelled":
                        if(player.View.GetComponent<PlotLegacySkipController>().IsShowing||player.Core.IsPaused||browser.Session.State!=PlotChapterState.Playing||browser.ReturnCount!=0||browser.Progress.IsRead(PlotChapterKind.Main,100))throw new InvalidOperationException("取消跳过改变了播放状态或进度");state.skipCancelChecks++;Click(state,player.View.GetComponent<PlotLegacySkipController>().skipButton,"再次打开跳过确认");Phase(state,"first-confirm");break;
                    case "first-confirm":Click(state,Find<Button>(player.View,"ConfirmBtn"),"首次确认跳过并结算");Phase(state,"first-return",.6);break;
                    case "first-return":
                        CheckInventory(state,browser,3,1,1);if(!browser.Progress.IsRead(PlotChapterKind.Main,100)||browser.SelectedEpisode!=500)throw new InvalidOperationException("首次完成未推进到非连续ID500");CheckRewards(state,browser);Click(state,CatalogButton(browser,false,100),"选择已读小节100重看");Phase(state,"replay-frame");break;
                    case "replay-frame":
                        CheckRewards(state,browser);var begin=Find<Button>(browser,"SelectBtn");if(Find<TMP_Text>(begin,"Title").text!="重看"||!Find<TMP_Text>(browser,"AwardTitle").text.Contains("已领取"))throw new InvalidOperationException("重看标识或已领取奖励提示错误");Capture(state,"replay-episodes","replay-begin");break;
                    case "replay-begin":BeginSelected(state,browser,"replay-playing");break;
                    case "replay-playing":if(!Ready(browser,player))break;CheckPlayer(state,player,"reference.blue");Click(state,player.View.GetComponent<PlotLegacySkipController>().skipButton,"重看打开跳过确认");Phase(state,"replay-confirm");break;
                    case "replay-confirm":Click(state,Find<Button>(player.View,"ConfirmBtn"),"重看确认跳过");Phase(state,"replay-return",.6);break;
                    case "replay-return":CheckInventory(state,browser,3,1,2);CheckRewards(state,browser);Click(state,CatalogButton(browser,false,500),"选择分支小节500");Phase(state,"branch-begin");break;
                    case "branch-begin":BeginSelected(state,browser,"branch-playing");break;
                    case "branch-playing":
                        if(!Ready(browser,player))break;CheckPlayer(state,player,"reference.green");if(player.Core.GetCurrentNodeID()!=2401)throw new InvalidOperationException("分支小节没有进入指定入口");Click(state,Find<Button>(player.View,"Next"),"继续至剧情分支");Phase(state,"branch-frame");break;
                    case "branch-frame":
                        if(player.Core.State!=PlotPlayerState.WaitingForChoice||player.Core.Choices.Count!=2)throw new InvalidOperationException("分支选项没有出现");foreach(var name in new[]{"Choice_0","Choice_1"})Visible((RectTransform)Find<Button>(player.View,name).transform,null,name);Capture(state,"branch-choices","branch-select");break;
                    case "branch-select":Click(state,Find<Button>(player.View,"Choice_0"),"选择海边分支");Phase(state,"branch-result",.7);break;
                    case "branch-result":
                        if(!Ready(browser,player))break;if(player.Core.GetCurrentNodeID()!=2407)throw new InvalidOperationException("实际点击未进入分支目标");CheckPlayer(state,player,"reference.blue");state.branchChecks++;Click(state,Find<Button>(player.View,"Next"),"完成分支末节点");Phase(state,"branch-return",.6);break;
                    case "branch-return":
                        CheckInventory(state,browser,8,1,3);CheckRewards(state,browser);if(browser.campaign.data.Progress(browser.campaign.data.Chapter(PlotChapterKind.Main,10),browser.Progress)!=100)throw new InvalidOperationException("主线首章未完成");Click(state,Find<Button>(browser,"BackBtn"),"小节返回主线章节");Phase(state,"main-return");break;
                    case "main-return":CheckTitle(state,browser);Click(state,Find<Button>(browser,"DailyStory"),"切换日常故事");Phase(state,"daily-frame",.5);break;
                    case "daily-frame":CheckTitle(state,browser);Capture(state,"daily-chapters","daily-click");break;
                    case "daily-click":Click(state,CatalogButton(browser,true,20),"日常章节20");Phase(state,"daily-opening",.8);break;
                    case "daily-opening":if(browser.Kind!=PlotChapterKind.Daily||browser.SelectedEpisode!=700)break;CheckRewards(state,browser);Capture(state,"daily-episodes","daily-begin");break;
                    case "daily-begin":BeginSelected(state,browser,"daily-playing");break;
                    case "daily-playing":if(!Ready(browser,player))break;CheckPlayer(state,player,"reference.gold");Click(state,player.View.GetComponent<PlotLegacySkipController>().skipButton,"日常首次打开跳过确认");Phase(state,"daily-confirm");break;
                    case "daily-confirm":Click(state,Find<Button>(player.View,"ConfirmBtn"),"日常首次确认跳过");Phase(state,"daily-return",.6);break;
                    case "daily-return":
                        CheckInventory(state,browser,11,2,4);CheckRewards(state,browser);if(!browser.Progress.IsRead(PlotChapterKind.Daily,700)||browser.SelectedEpisode!=1200)throw new InvalidOperationException("日常进度未推进到非连续ID1200");Click(state,CatalogButton(browser,false,1200),"选择日常第二小节1200");Phase(state,"daily-last-begin");break;
                    case "daily-last-begin":BeginSelected(state,browser,"daily-last-playing");break;
                    case "daily-last-playing":if(!Ready(browser,player))break;CheckPlayer(state,player,"reference.green");Click(state,player.View.GetComponent<PlotLegacySkipController>().skipButton,"日常末节打开跳过确认");Phase(state,"daily-last-confirm");break;
                    case "daily-last-confirm":Click(state,Find<Button>(player.View,"ConfirmBtn"),"日常末节确认跳过");Phase(state,"daily-last-return",.6);break;
                    case "daily-last-return":
                        CheckInventory(state,browser,11,4,5);CheckRewards(state,browser);if(browser.campaign.data.Progress(browser.campaign.data.Chapter(PlotChapterKind.Daily,20),browser.Progress)!=100)throw new InvalidOperationException("日常章未全部完成");Capture(state,"daily-completed","finish");break;
                    case "finish":CheckTitle(state,browser);Stop(state);break;
                    default:throw new InvalidOperationException("未知公开样例验收阶段: "+state.phase);
                }
            }
            catch(Exception error)
            {
                state.error=error.ToString();state.status="failed";Save(state);Record(state);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;else{Restore(state);SessionState.EraseString(Key);}Debug.LogException(error);
            }
        }
    }
}
