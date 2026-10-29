using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    public static class PlotLegacyWorkspaceMenus
    {
        public static bool HasAuthoringScene => TryGetAuthoringScene(out _);
        public static bool TryGetAuthoringScene(out string scene)
        {
            scene = null;
            if (!File.Exists(PlotLegacyInteractionMigration.LatestPath)) return false;
            var report = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            if (report == null || string.IsNullOrEmpty(report.authoringScene))
                throw new InvalidDataException("本地制作场景记录缺少authoringScene: " + PlotLegacyInteractionMigration.LatestPath);
            var root = Path.GetFullPath("Assets/PlotWeaveLocal").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!report.authoringScene.StartsWith("Assets/PlotWeaveLocal/", StringComparison.Ordinal)
                || !Path.GetFullPath(report.authoringScene).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("本地制作场景必须位于Assets/PlotWeaveLocal目录");
            if (!File.Exists(report.authoringScene)) return false;
            scene = report.authoringScene;
            return true;
        }
        private static void Open(string scene)
        {if(string.IsNullOrEmpty(scene)||!scene.StartsWith("Assets/PlotWeaveLocal/",StringComparison.Ordinal)||!File.Exists(scene))throw new FileNotFoundException("请先生成本地迁移场景",scene);if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(scene);}
        [MenuItem("Tools/PlotWeave/本地原布局/打开制作界面")]
        public static void OpenAuthoring()
        {
            if (!TryGetAuthoringScene(out var scene)) throw new FileNotFoundException("请先生成本地迁移制作场景");
            PlotWeave.Authoring.PlotAuthoringStartup.OpenSceneOnly(scene);
        }
        [MenuItem("Tools/PlotWeave/本地原布局/打开剧情播放")]
        public static void OpenPlayback()=>Open(JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath)).playbackScene);
        [MenuItem("Tools/PlotWeave/本地原布局/打开章节与奖励浏览")]
        public static void OpenCampaign()=>Open(JsonUtility.FromJson<PlotLegacyCampaignMigration.Report>(File.ReadAllText(PlotLegacyCampaignMigration.LatestPath)).scene);
        [MenuItem("Tools/PlotWeave/本地原布局/打开完整Luban二进制播放")]
        public static void OpenLuban()=>Open(JsonUtility.FromJson<PlotLegacyLubanPlaybackMigration.Report>(File.ReadAllText(PlotLegacyLubanPlaybackMigration.LatestPath)).scene);
    }
}
