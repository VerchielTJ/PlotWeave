using System;
using System.Linq;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using PlotWeave.Authoring.Playback;
using PlotWeave.Authoring.Excel;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    [InitializeOnLoad] public static class PlotLegacyAuthoringBootstrap
    {
        static PlotLegacyAuthoringBootstrap(){PlotLegacyAuthoringHost.InitializationRequested=Bind;EditorApplication.update+=Poll;}
        private static void Poll(){if(!Application.isPlaying||EditorApplication.isCompiling)return;foreach(var host in UnityEngine.Object.FindObjectsOfType<PlotLegacyAuthoringHost>().Where(h=>h.Session==null&&!h.InitializationFailed&&h.project!=null))Bind(host);}
        private static void Bind(PlotLegacyAuthoringHost host)
        {
            if(host==null||host.InitializationReady||host.InitializationFailed||host.Session!=null)return;
            EditorPlotPlayerUI player=null;var attached=false;
            try
            {
            host.Initialize(new PlotEditorManager(host.project),()=>{var result=EditorUtility.DisplayDialogComplex("未保存的剧情编辑","保存当前修改后继续？","保存","取消","丢弃");return result==0?UnsavedChoice.Save:result==2?UnsavedChoice.Discard:UnsavedChoice.Cancel;},text=>EditorUtility.DisplayDialog("PlotWeave",text,"确认","取消"));
            player = new EditorPlotPlayerUI(host.project, () => PlotPlaybackCompiler.Compile(host.Session), host.PreviewViewport);
            host.Preview = player.Play;
            host.ClosePreview = player.Dispose;attached=true;
            host.ValidatePreview = () => PlotPlaybackCompiler.Compile(host.Session);
            host.RefreshPreview = () => player.Refresh();
            host.OpenDataExchange = () => PlotExcelWindow.OpenFor(host.project, host.PrepareDataExchange, host.ReloadAfterImport);
            host.CompleteInitialization();
            Debug.Log("PLOTWEAVE_AUTHORING_READY workspace="+host.project.name+" node="+(host.Session.CurrentAsset==null?"<none>":host.Session.CurrentAsset.name),host);
            }
            catch(Exception error)
            {
                if(!attached)player?.Dispose();host.AbortInitialization(error);
                Debug.LogError("PLOTWEAVE_AUTHORING_INIT_FAILED workspace="+(host.project==null?"<none>":host.project.name)+"; 本实例停止自动重试，请退出Play后重新打开。\n"+error,host);
            }
        }
    }
}
