using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Editor
{
    public static class PlotMenus
    {
        [MenuItem("Tools/PlotWeave/历史原型/创建演示剧情")]
        public static void CreateExample() { var asset = CreateStory(true); if (asset != null) PlotWeaveWindow.Open(asset); }

        public static PlotAsset CreateStory(bool demo)
        {
            var path = EditorUtility.SaveFilePanelInProject("新建剧情", demo ? "PlotWeaveDemo" : "NewStory", "asset", "选择资产位置。");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<PlotAsset>();
            asset.document = demo ? PlotSample.Create() : new PlotDocument();
            if (!demo)
            {
                var first = new PlotNode { id = Guid.NewGuid().ToString("N"), title = "开始", text = "第一句对白", position = new Vector2(100, 100) };
                asset.document.nodes.Add(first); asset.document.entryNodeId = first.id;
            }
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); Selection.activeObject = asset; return asset;
        }

        public static void ExportWorkbook(PlotAsset asset)
        {
            var path = EditorUtility.SaveFilePanel("导出剧情表格", "", asset.name, "xlsx");
            if (string.IsNullOrEmpty(path)) return;
            PlotWorkbook.Export(asset.document, path); Debug.Log("PlotWeave 表格已导出：" + path);
        }

        [MenuItem("Tools/PlotWeave/历史原型/编译七表Excel为JSON")]
        public static void CompileWorkbook()
        {
            var input = EditorUtility.OpenFilePanel("选择剧情工作簿", "", "xlsx");
            if (string.IsNullOrEmpty(input)) return;
            var output = EditorUtility.SaveFilePanelInProject("编译剧情", Path.GetFileNameWithoutExtension(input), "json", "生成文件可绑定到参考播放器的 Compiled Json。");
            if (string.IsNullOrEmpty(output)) return;
            try { PlotWorkbook.CompileToJson(input, output); AssetDatabase.Refresh(); Debug.Log("PlotWeave 编表成功：" + output); }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("编表失败", ex.Message, "确定"); }
        }
        public static void ExportJson(PlotAsset asset)
        {
            PlotValidation.Check(asset.document).ThrowIfInvalid();
            var path = EditorUtility.SaveFilePanelInProject("导出剧情 JSON", asset.name, "json", "生成运行时数据。");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(asset.document, true), new UTF8Encoding(false)); AssetDatabase.Refresh();
        }
        public static PlotAsset ImportJson()
        {
            var input = EditorUtility.OpenFilePanel("导入 PlotWeave JSON", "", "json");
            if (string.IsNullOrEmpty(input)) return null;
            var data = new JsonPlotDataSource(File.ReadAllText(input)).Read(); PlotValidation.Check(data).ThrowIfInvalid();
            var path = EditorUtility.SaveFilePanelInProject("保存为新剧情资产", data.id, "asset", "不会覆盖导入前的原始资产。");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<PlotAsset>(); asset.document = data;
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); return asset;
        }

        public static void CreateDemoScene(PlotAsset asset)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = EditorUtility.SaveFilePanelInProject("参考播放场景", "PlotWeaveDemo", "unity", "创建新场景，不覆盖已有场景。");
            if (string.IsNullOrEmpty(path)) return;
            if (File.Exists(path)) throw new IOException("请使用新场景路径。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = new GameObject("PlotWeave Player").AddComponent<PlotDemoPlayer>(); player.story = asset;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            EditorSceneManager.SaveScene(scene, path);
        }

        [OnOpenAsset]
        private static bool OnOpen(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is PlotAsset asset)) return false;
            PlotWeaveWindow.Open(asset); return true;
        }

        /// <summary>批处理验收入口：生成新演示资产、表格、编译 JSON 和两个播放场景。</summary>
        public static void PrepareVerification()
        {
            const string folder = "Assets/PlotWeaveDemo";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var assetPath = folder + "/Example.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PlotAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlotAsset>(); asset.document = PlotSample.Create(); AssetDatabase.CreateAsset(asset, assetPath);
            }
            PlotValidation.Check(asset.document).ThrowIfInvalid();
            PlotWorkbook.Export(asset.document, folder + "/Example.xlsx");
            PlotWorkbook.CompileToJson(folder + "/Example.xlsx", folder + "/Example.compiled.json");
            PlotCodeGenerator.Write(asset.document, folder + "/Generated/PlotCommandAdapters.g.cs");
            AssetDatabase.Refresh();
            var compiled = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/Example.compiled.json");
            foreach (var variant in new[] { "Asset", "Compiled" })
            {
                var scenePath = folder + "/Demo" + variant + ".unity";
                if (File.Exists(scenePath)) continue;
                var previousActive = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var host = new GameObject("PlotWeave Player").AddComponent<PlotDemoPlayer>();
                    if (variant == "Asset") host.story = asset; else host.compiledJson = compiled;
                    var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                    EditorSceneManager.SaveScene(scene, scenePath);
                }
                finally
                {
                    if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            var builds = EditorBuildSettings.scenes.ToList();
            foreach (var variant in new[] { "Asset", "Compiled" })
            {
                var scenePath = folder + "/Demo" + variant + ".unity";
                if (!builds.Any(s => s.path == scenePath)) builds.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets(); Debug.Log("PLOTWEAVE_PREPARE_OK");
        }
    }
}
