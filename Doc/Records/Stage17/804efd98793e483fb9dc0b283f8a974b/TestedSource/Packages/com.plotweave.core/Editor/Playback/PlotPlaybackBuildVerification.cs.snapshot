using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotPlaybackBuildVerification
    {
        public static void Build()
        {
            var scenes = new List<string>(); if (File.Exists(PlotChapterExample.ScenePath)) scenes.Add(PlotChapterExample.ScenePath); if (File.Exists(PlotAdvancedExample.ScenePath)) scenes.Add(PlotAdvancedExample.ScenePath); if (File.Exists(PlotWeave.Authoring.Luban.PlotLubanExample.ScenePath)) scenes.Add(PlotWeave.Authoring.Luban.PlotLubanExample.ScenePath); scenes.Add(PlotPlaybackExample.GameScene); scenes.Add(PlotPlaybackExample.JsonScene);
            foreach (var scene in scenes) if (!File.Exists(scene)) throw new FileNotFoundException("请先生成共享播放样例", scene);
            Directory.CreateDirectory("Builds/PlotWeavePlayback");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes.ToArray(), locationPathName = "Builds/PlotWeavePlayback/PlotWeavePlayback.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0) throw new InvalidOperationException("共享播放场景构建失败: " + report.summary.result + ", errors=" + report.summary.totalErrors);
            Debug.Log("PLOTWEAVE_SHARED_PLAYER_BUILD Succeeded; errors=0; scenes=" + string.Join(",", scenes));
        }
    }
}
