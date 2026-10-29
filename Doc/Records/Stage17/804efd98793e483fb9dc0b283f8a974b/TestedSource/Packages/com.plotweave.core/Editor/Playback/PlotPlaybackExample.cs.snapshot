using System;
using System.IO;
using System.Linq;
using PlotWeave.Authoring.UI;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotPlaybackExample
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage05_Playback";
        public const string CatalogPath = Root + "/Visuals.asset";
        public const string GameScene = Root + "/GamePlayback.unity";
        public const string JsonScene = Root + "/JsonPlayback.unity";
        public const string EditorScene = Root + "/EditorPlayback.unity";
        [MenuItem("Tools/PlotWeave/迁移样例/创建共享播放与双宿主样例")]
        public static void PrepareVerification()
        {
            var previous = SceneManager.GetActiveScene();
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var workspace = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(Root + "/Workspace/Workspace.asset");
            if (workspace == null) workspace = PlotAuthoringBootstrap.CreateWorkspace(Root + "/Workspace");
            var catalog = AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlotPlaybackVisualCatalog>();
                catalog.sprites.Add(new PlotPlaybackSpriteBinding { key = "demo.stage", sprite = CreateSprite("Stage", new Color(.08f, .18f, .3f), true) });
                catalog.sprites.Add(new PlotPlaybackSpriteBinding { key = "demo.guide", sprite = CreateSprite("Guide", new Color(.2f, .65f, .85f), false) });
                catalog.sprites.Add(new PlotPlaybackSpriteBinding { key = "demo.traveler", sprite = CreateSprite("Traveler", new Color(.85f, .55f, .25f), false) });
                AssetDatabase.CreateAsset(catalog, CatalogPath); AssetDatabase.SaveAssetIfDirty(catalog);
            }
            if (workspace.visuals == null) { workspace.visuals = catalog; EditorUtility.SetDirty(workspace); AssetDatabase.SaveAssetIfDirty(workspace); }
            var compiled = AssetDatabase.LoadAssetAtPath<PlotPlaybackAsset>(Root + "/Compiled.asset");
            if (compiled == null)
            {
                compiled = ScriptableObject.CreateInstance<PlotPlaybackAsset>(); compiled.data = PlotPlaybackCompiler.Compile(workspace);
                AssetDatabase.CreateAsset(compiled, Root + "/Compiled.asset"); AssetDatabase.SaveAssetIfDirty(compiled);
                File.WriteAllText(Root + "/Compiled.json", JsonUtility.ToJson(compiled.data, true)); AssetDatabase.Refresh();
            }
            foreach (var path in new[] { GameScene, JsonScene, EditorScene })
            {
                if (File.Exists(path)) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .12f, .18f); SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                    if (path == EditorScene)
                    { var panel = new GameObject("PlotWeave Authoring", typeof(PlotAuthoringPanel)).GetComponent<PlotAuthoringPanel>(); panel.project = workspace; SceneManager.MoveGameObjectToScene(panel.gameObject, scene); }
                    else
                    {
                        var host = new GameObject("Shared Plot Player", typeof(PlotPlayerHost)).GetComponent<PlotPlayerHost>(); SceneManager.MoveGameObjectToScene(host.gameObject, scene);
                        host.source = path == GameScene ? compiled : null; host.compiledJson = path == JsonScene ? AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Compiled.json") : null;
                        host.sprites = catalog.sprites.Select(s => new PlotPlaybackSpriteBinding { key = s.key, sprite = s.sprite }).ToList();
                    }
                    EditorSceneManager.SaveScene(scene, path);
                }
                finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
            Debug.Log("PLOTWEAVE_STAGE05_READY " + Root);
        }
        private static Sprite CreateSprite(string name, Color color, bool stage)
        {
            var width = stage ? 640 : 160; var height = stage ? 360 : 280;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false); var pixels = new Color[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var gradient = Color.Lerp(color * .45f, color, y / (float)height); gradient.a = 1;
                if (!stage)
                { var dx = (x - width / 2f) / (width * .42f); var dy = (y - height * .68f) / (height * .28f); var head = dx * dx + dy * dy < .6f; var body = y < height * .62f && Math.Abs(x - width / 2f) < width * (.18f + .25f * (1 - y / (float)height)); gradient.a = head || body ? 1 : 0; }
                pixels[y * width + x] = gradient;
            }
            texture.SetPixels(pixels); texture.Apply(); var path = Root + "/" + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
