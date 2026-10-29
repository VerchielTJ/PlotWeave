using System;
using System.IO;
using PlotWeave.Authoring.Playback;
using PlotWeave.Playback;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Authoring.Luban
{
    public static class PlotLubanExample
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage09_Luban";
        public const string Generated = Root + "/Generated";
        public const string Namespace = "PlotWeaveLuban";
        public const string ScenePath = Root + "/LubanPlayback.unity";
        [MenuItem("Tools/PlotWeave/迁移样例/生成真实Luban二进制样例")]
        public static void PrepareVerification()
        {
            PlotPlaybackExample.PrepareVerification(); var runtime = Root + "/LubanRuntime"; Directory.CreateDirectory(runtime);
            foreach (var name in new[] { "BeanBase.cs", "ByteBuf.cs", "ITypeId.cs", "StringUtil.cs" })
            {
                var source = "Packages/com.plotweave.core/Editor/ThirdParty~/LubanRuntime/" + name + ".txt"; var output = runtime + "/" + name;
                if (!File.Exists(source)) throw new FileNotFoundException(source);
                if (File.Exists(output) && File.ReadAllText(output) != File.ReadAllText(source)) throw new IOException("示例运行库已修改，未覆盖: " + output);
                if (!File.Exists(output)) File.Copy(source, output);
            }
            var asmdef = runtime + "/PlotWeave.LubanRuntime.Sample.asmdef"; var definition = "{\"name\":\"PlotWeave.LubanRuntime.Sample\",\"allowUnsafeCode\":true,\"autoReferenced\":true}";
            if (File.Exists(asmdef) && File.ReadAllText(asmdef) != definition) throw new IOException("示例运行库程序集已有修改"); if (!File.Exists(asmdef)) File.WriteAllText(asmdef, definition);
            foreach (var name in new[] { "SOURCE.txt", "LICENSE" }) File.Copy("Packages/com.plotweave.core/Editor/ThirdParty~/LubanRuntime/" + name, runtime + "/" + name, true);
            var profilePath = Root + "/LubanProfile.asset"; var profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlotLubanProfile>(); profile.project = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(PlotPlaybackExample.Root + "/Workspace/Workspace.asset"); profile.generatedRoot = Generated;
                profile.dotnetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet/dotnet.exe"); profile.assemblyName = "PlotWeave.Luban.Generated.Sample";
                AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssetIfDirty(profile);
            }
            var report = PlotLubanPipeline.Run(profile); PreparePlayerSource(); Debug.Log("PLOTWEAVE_STAGE09_LUBAN_GENERATED " + report.id);
        }
        private static void WriteOwned(string path, string text)
        { if (File.Exists(path) && File.ReadAllText(path) != text) throw new IOException("Luban播放示例已有修改，未覆盖: " + path); if (!File.Exists(path)) File.WriteAllText(path, text); }
        private static void PreparePlayerSource()
        {
            var folder = Root + "/Playback"; Directory.CreateDirectory(folder);
            WriteOwned(folder + "/PlotWeave.Luban.Playback.Sample.asmdef", "{\"name\":\"PlotWeave.Luban.Playback.Sample\",\"references\":[\"PlotWeave.Runtime\",\"PlotWeave.Luban.Generated.Sample\",\"PlotWeave.LubanRuntime.Sample\"]}");
            WriteOwned(folder + "/PlotLubanSamplePlayer.cs", @"using System; using System.Collections.Generic; using System.Linq; using Luban; using PlotWeave.Playback; using UnityEngine;
namespace PlotWeave.LubanSample {
public sealed class PlotLubanSamplePlayer : MonoBehaviour {
 public List<TextAsset> binaries = new List<TextAsset>();
 void Start() {
  var files = binaries.ToDictionary(b => b.name, b => b.bytes, StringComparer.Ordinal);
  var tables = new global::PlotWeaveLuban.Tables(name => new ByteBuf(files[name]));
  var data = PlotLubanTableReader.Read(tables.TbPlotAtom.DataList, tables.TbActor.DataList, tables.TbDialog.DataList, tables.TbConstant.DataList);
  var host = GetComponent<PlotPlayerHost>(); host.Initialize(data, PlotPlayerRuntimeMode.GameRuntime); host.Play(1001);
 }
}
}
");
            AssetDatabase.Refresh();
        }
        [MenuItem("Tools/PlotWeave/迁移样例/创建Luban二进制播放场景")]
        public static void PrepareScene()
        {
            var type = Type.GetType("PlotWeave.LubanSample.PlotLubanSamplePlayer, PlotWeave.Luban.Playback.Sample"); if (type == null) throw new InvalidOperationException("请先生成真实Luban样例并等待脚本编译"); if (File.Exists(ScenePath)) return;
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera"; SceneManager.MoveGameObjectToScene(camera, scene);
                var host = new GameObject("Luban Binary Player").AddComponent<PlotPlayerHost>(); SceneManager.MoveGameObjectToScene(host.gameObject, scene); host.playOnStart = false; host.sprites = AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(PlotPlaybackExample.CatalogPath).sprites;
                var player = host.gameObject.AddComponent(type); var serialized = new SerializedObject(player); var binaries = serialized.FindProperty("binaries"); var paths = Directory.GetFiles(Generated + "/Data", "*.bytes"); Array.Sort(paths, StringComparer.Ordinal); binaries.arraySize = paths.Length; for (var i = 0; i < paths.Length; i++) binaries.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]); serialized.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
