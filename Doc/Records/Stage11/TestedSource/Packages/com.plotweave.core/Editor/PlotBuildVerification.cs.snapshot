using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlotWeave.Editor
{
    public static class PlotBuildVerification
    {
        public static void BuildPlayer()
        {
            var directory = Environment.GetEnvironmentVariable("PLOTWEAVE_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("PLOTWEAVE_BUILD_OUTPUT is required.");
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/PlotWeaveDemo/DemoCompiled.unity" },
                locationPathName = Path.Combine(directory, "PlotWeave.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log("PLOTWEAVE_BUILD_RESULT " + report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Player build failed.");
        }
    }
}
