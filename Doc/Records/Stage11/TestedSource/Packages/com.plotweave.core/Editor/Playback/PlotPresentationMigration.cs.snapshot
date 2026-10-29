using System;
using System.IO;
using TMPro;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotPresentationMigration
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage07_Migration";
        public const string PrefabPath = Root + "/TMPPlayer.prefab";
        public static void EnsureTMPResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            var archive = Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(archive)) throw new FileNotFoundException("找不到已安装TMP包的基础资源", archive);
            AssetDatabase.ImportPackage(archive, false); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        [MenuItem("Tools/PlotWeave/迁移样例/补齐TMP与可编辑播放预制体")]
        public static void PrepareVerification()
        {
            EnsureTMPResources();
            if (Resources.Load<TMP_Settings>("TMP Settings") == null) throw new InvalidOperationException("TMP资源仍在导入，请稍后重试本入口");
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            if (!File.Exists(PrefabPath))
            {
                var root = new GameObject("PlotWeave TMP Player"); var pool = new PlotPlayerObjectPool();
                try
                {
                    var view = root.AddComponent<PlotPlayerView>(); view.ConfigureText(TMP_Settings.defaultFontAsset, TMP_Settings.defaultSpriteAsset);
                    view.Initialize(new PlotPlayerDataProvider(new PlotPlaybackDocument()), new PlotPlayerResourceLoader(), pool, new PlotPlayerConfig());
                    view.CaptureBindings().Validate();
                    foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); pool.Dispose(); }
            }
            PlotPlaybackExample.PrepareVerification(); Debug.Log("PLOTWEAVE_STAGE07_READY " + Root);
        }
    }
}
