using System;
using System.IO;
using System.Linq;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Legacy.Editor
{
    [InitializeOnLoad] public static class PlotLegacyPreviewVerification
    {
        public const string Key="PlotWeave.Stage14.Preview";
        [Serializable] private sealed class State
        {public string id,mode,phase,scene,image,error="",previousScene;public bool background,maximized,hadView,skipChecked;public int frames,nodes;public double deadline,next,started;}
        static PlotLegacyPreviewVerification()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=change=>{if(change!=PlayModeStateChange.EnteredEditMode)return;var state=Read();if(state==null)return;Restore(state);state.phase=state.error==""&&File.Exists(state.image)?"passed":"failed";Record(state);SessionState.EraseString(Key);};
        }
        private static State Read(){var json=SessionState.GetString(Key,"");return json==""?null:JsonUtility.FromJson<State>(json);}
        private static void Save(State s)=>SessionState.SetString(Key,JsonUtility.ToJson(s));
        private static void Record(State s){Directory.CreateDirectory("Doc/Records/Stage14");File.WriteAllText("Doc/Records/Stage14/preview-"+s.id+".json",JsonUtility.ToJson(s,true));}
        private static EditorWindow Game()=>Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.GameView");
        private static void Restore(State s){Application.runInBackground=s.background;EditorSceneManager.playModeStartScene=s.previousScene==""?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(s.previousScene);var game=Game();if(game!=null&&s.hadView)game.maximized=s.maximized;}
        [Serializable] private sealed class Widget{public string path,sprite,controller;public bool active,enabled,openState;public Vector2 size,anchorMin,anchorMax,pivot;public Vector3 position,local,scale;public Color color;public float alpha;}
        [Serializable] private sealed class Geometry{public string uiRoot;public System.Collections.Generic.List<Widget> widgets=new System.Collections.Generic.List<Widget>();}
        private static void Diagnose(PlotPlayerHost host,string id)
        {
            var output=new Geometry{uiRoot=host.View.GetComponent<PlotPlayerViewBindings>().uiRoot.name};
            foreach(var rect in host.View.GetComponentsInChildren<RectTransform>(true))
            {
                if(rect.name.StartsWith("Position_",StringComparison.Ordinal))continue;var image=rect.GetComponent<Image>();var animator=rect.GetComponent<Animator>();var group=rect.GetComponent<CanvasGroup>();
                output.widgets.Add(new Widget{path=AnimationUtility.CalculateTransformPath(rect,host.View.transform),active=rect.gameObject.activeInHierarchy,size=rect.rect.size,anchorMin=rect.anchorMin,anchorMax=rect.anchorMax,pivot=rect.pivot,position=rect.position,local=rect.localPosition,scale=rect.lossyScale,color=image!=null?image.color:Color.clear,sprite=image!=null&&image.sprite!=null?image.sprite.name:"",alpha=group!=null?group.alpha:-1,enabled=image!=null?image.enabled:animator!=null&&animator.enabled,controller=animator!=null&&animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.name:"",openState=animator!=null&&animator.HasState(0,Animator.StringToHash("Open"))});
            }
            File.WriteAllText("Tools/PlotWeaveMigration/Inputs/legacy-geometry-"+id+".json",JsonUtility.ToJson(output,true));
            var background=host.View.GetComponent<PlotPlayerViewBindings>().background;if(background.sprite!=null&&background.sprite.texture.isReadable)File.WriteAllBytes("Tools/PlotWeaveMigration/Inputs/legacy-texture-"+id+".png",background.sprite.texture.EncodeToPNG());
        }
        public static void Start(string mode)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Read()!=null)throw new InvalidOperationException("已有运行中的检查");for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("当前场景未保存");
            if(mode!="editor"&&mode!="player"&&mode!="luban")throw new ArgumentException("未知原布局验证模式");var report=JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));var scene=mode=="luban"?JsonUtility.FromJson<PlotLegacyLubanPlaybackMigration.Report>(File.ReadAllText(PlotLegacyLubanPlaybackMigration.LatestPath)).scene:mode=="editor"?report.authoringScene:report.playbackScene;var s=new State{id=Guid.NewGuid().ToString("N"),mode=mode,phase="start",scene=scene,background=Application.runInBackground,previousScene=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),deadline=EditorApplication.timeSinceStartup+180};
            s.started=EditorApplication.timeSinceStartup;s.image="Tools/PlotWeaveMigration/Inputs/Images/legacy-"+mode+"-"+s.id+".png";Directory.CreateDirectory(Path.GetDirectoryName(s.image));Save(s);Record(s);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(s.scene);EditorApplication.isPlaying=true;
        }
        private static void Update()
        {
            var s=Read();if(s==null||EditorApplication.isCompiling)return;
            try
            {
                if(EditorApplication.timeSinceStartup>s.deadline)throw new TimeoutException("原布局运行检查超时");if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();var game=Game();if(game!=null){if(!s.hadView){s.hadView=true;s.maximized=game.maximized;game.maximized=true;Save(s);}game.Repaint();}
                if(s.phase=="capture"){if(!File.Exists(s.image)||new FileInfo(s.image).Length==0)return;if(s.mode=="editor"){EditorApplication.isPlaying=false;return;}s.phase="walk";Save(s);}
                if(s.mode=="editor")
                {
                    var host=UnityEngine.Object.FindObjectOfType<PlotLegacyAuthoringHost>();if(host==null||host.Session==null)return;if(host.LastError!="")throw new Exception(host.LastError);if(host.Session.Store.ListRows().Count!=485||host.NodeView.PoolSize==0)throw new Exception("原制作数据/虚拟列表未装载");
                    s.nodes=485;s.frames++;if(s.frames<10){Save(s);return;}var viewport=host.NodeView.GetComponent<ScrollRect>().viewport;var center=RectTransformUtility.WorldToScreenPoint(null,viewport.TransformPoint(viewport.rect.center));if(center.x<0||center.x>Screen.width||center.y<0||center.y>Screen.height)throw new Exception("节点列表位于屏幕之外");s.phase="capture";Save(s);Record(s);ScreenCapture.CaptureScreenshot(Path.GetFullPath(s.image));return;
                }
                var player=UnityEngine.Object.FindObjectsOfType<PlotPlayerHost>().FirstOrDefault(h=>h.Core!=null&&h.gameObject.scene.path==s.scene);if(player==null){if(EditorApplication.timeSinceStartup-s.started>30)throw new Exception("原播放器30秒内未初始化");return;}if(player.Core.State==PlotPlayerState.Faulted)throw new Exception("原剧情播放进入Faulted");
                if(s.phase=="walk")
                {
                    if(player.Core.State==PlotPlayerState.Completed)
                    {if(s.nodes<20)throw new Exception("连续播放节点数量不足");player.Play(20000);var skip=player.View.GetComponent<PlotLegacySkipController>();skip.Show();if(!skip.IsShowing||!player.Core.IsPaused)throw new Exception("跳过确认未暂停播放");skip.Cancel();if(skip.IsShowing||player.Core.IsPaused)throw new Exception("取消跳过未恢复播放");skip.Show();skip.Confirm();if(player.Core.State!=PlotPlayerState.Completed)throw new Exception("确认跳过未完成剧情");s.skipChecked=true;Save(s);Record(s);EditorApplication.isPlaying=false;return;}
                    if(player.Core.State==PlotPlayerState.WaitingForChoice){player.Core.Choose(0);s.nodes++;Save(s);return;}
                    player.Core.Tick(.6f);if(player.Core.IsTyping)player.Core.OnPlayerInput();if(!player.Core.IsReady)return;player.Core.OnPlayerInput();s.nodes++;Save(s);return;
                }
                if(player.Core.IsTyping)player.Core.OnPlayerInput();if(!player.Core.IsReady)return;if(player.Core.GetCurrentNodeID()<20003){player.Core.OnPlayerInput();s.nodes++;Save(s);return;}if(s.phase!="frame"){s.phase="frame";s.next=EditorApplication.timeSinceStartup+1.2;Save(s);return;}if(EditorApplication.timeSinceStartup<s.next)return;
                if(player.Core.GetCurrentNodeID()!=20003||player.View.ActorCount==0||player.View.BackgroundKey=="")throw new Exception("原布局演员或背景未显示");Diagnose(player,s.id);s.phase="capture";Save(s);Record(s);ScreenCapture.CaptureScreenshot(Path.GetFullPath(s.image));
            }
            catch(Exception error){s.error=error.ToString();Save(s);Record(s);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;else{Restore(s);SessionState.EraseString(Key);}Debug.LogException(error);}
        }
    }
}
