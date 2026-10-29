using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PlotWeave.Authoring.Luban;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>真实原剧情二进制的两步播放场景生成；生成程序集与核心包解耦。</summary>
    public static class PlotLegacyLubanPlaybackMigration
    {
        public const string LatestPath = "Tools/PlotWeaveMigration/original-luban-playback.json";
        [Serializable] public sealed class FileStamp { public string path, sha256; }
        [Serializable] public sealed class Report
        {
            public string id, status, error, root, scene, profile, generatedRoot, generatedNamespace, generatedAssembly, playbackNamespace, playbackAssembly, catalog;
            public int entryNode = 20000, binaries;
            public List<FileStamp> files = new List<FileStamp>();
        }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        private static string LocalPath(string path)
        {
            var value = (path ?? "").Replace('\\', '/').TrimEnd('/');
            if (!value.StartsWith("Assets/PlotWeaveLocal/", StringComparison.Ordinal) || value.Split('/').Any(p => p == ".." || p == "." || p == "")) throw new ArgumentException("完整原剧情场景只写入本地验证目录");
            return value;
        }
        private static void Save(Report report)
        { Directory.CreateDirectory("Doc/Records/Stage17"); File.WriteAllText("Doc/Records/Stage17/luban-playback-" + report.id + ".json", JsonUtility.ToJson(report, true)); }
        private static PlotLubanProfile Profile(string path)
        { var profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(LocalPath(path)); if (profile == null || profile.project == null) throw new InvalidOperationException("完整原数据Luban配置尚未生成"); return profile; }
        [MenuItem("Tools/PlotWeave/迁移工具/生成完整原数据Luban播放器源码")]
        public static void PrepareSource()
        {
            var data = JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));
            var interaction = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            var profile = Profile(data.lubanProfile);
            PlotLubanSchema.Identifier(profile.generatedNamespace, true); PlotLubanSchema.Identifier(profile.assemblyName, true); PlotLubanSchema.Identifier(profile.runtimeAssembly, true);
            var report = new Report { id = Guid.NewGuid().ToString("N"), status = "running", profile = data.lubanProfile, generatedRoot = LocalPath(profile.generatedRoot), generatedNamespace = profile.generatedNamespace, generatedAssembly = profile.assemblyName, catalog = LocalPath(interaction.catalog) };
            report.root = LocalPath(data.root) + "/LubanPlayback_" + report.id.Substring(0, 10);
            report.playbackNamespace = profile.generatedNamespace + ".Playback" + report.id.Substring(0, 8);
            report.playbackAssembly = profile.assemblyName + ".PlaybackRun" + report.id.Substring(0, 8);
            report.scene = report.root + "/OriginalLubanPlayback.unity";
            try
            {
                if (Directory.Exists(report.root)) throw new IOException("播放器生成目录已存在，不覆盖");
                var binaries = Directory.GetFiles(report.generatedRoot + "/Data", "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
                if (binaries.Length != 4) throw new InvalidOperationException("完整原数据应有节点、演员、对白、常量四份Luban二进制");
                if (AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(report.catalog) == null) throw new InvalidOperationException("原剧情资源配置不存在");
                Directory.CreateDirectory(report.root + "/Player");
                var asmdef = "{\"name\":\"" + report.playbackAssembly + "\",\"references\":[\"PlotWeave.Runtime\",\"" + profile.assemblyName + "\",\"" + profile.runtimeAssembly + "\"],\"autoReferenced\":true}";
                var source = @"using System;
using System.Collections.Generic;
using System.Linq;
using Luban;
using PlotWeave.Playback;
using UnityEngine;
namespace " + report.playbackNamespace + @" {
[RequireComponent(typeof(PlotPlayerHost))]
public sealed class PlotOriginalLubanPlayer : MonoBehaviour {
 public List<TextAsset> binaries = new List<TextAsset>();
 public int entryNode = 20000;
 public bool Loaded { get; private set; }
 public int NodeCount { get; private set; }
 void Start() {
  var files = binaries.ToDictionary(b => b.name, b => b.bytes, StringComparer.Ordinal);
  var tables = new global::" + profile.generatedNamespace + @".Tables(name => new ByteBuf(files[name]));
  var document = PlotLubanTableReader.Read(tables.TbPlotAtom.DataList, tables.TbActor.DataList, tables.TbDialog.DataList, tables.TbConstant.DataList, null, true);
  NodeCount = document.Nodes.Count;
  var host = GetComponent<PlotPlayerHost>();
  host.Initialize(document, PlotPlayerRuntimeMode.GameRuntime);
  Loaded = true;
  host.Play(entryNode);
 }
}
}
";
                File.WriteAllText(report.root + "/Player/PlotOriginalLubanPlayer.cs", source, new UTF8Encoding(false));
                File.WriteAllText(report.root + "/Player/" + report.playbackAssembly + ".asmdef", asmdef, new UTF8Encoding(false));
                foreach (var path in Directory.GetFiles(report.root + "/Player").Concat(binaries)) report.files.Add(new FileStamp { path = path.Replace('\\', '/'), sha256 = Hash(path) });
                report.binaries = binaries.Length; report.status = "source-generated-awaiting-unity-compilation";
                Directory.CreateDirectory(Path.GetDirectoryName(LatestPath)); File.WriteAllText(LatestPath, JsonUtility.ToJson(report, true)); AssetDatabase.Refresh();
            }
            catch (Exception error) { report.status = "failed"; report.error = error.ToString(); throw; }
            finally { Save(report); }
        }
        [MenuItem("Tools/PlotWeave/迁移工具/创建完整原数据Luban播放场景")]
        public static void PrepareScene()
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(LatestPath)); LocalPath(report.root); LocalPath(report.scene);
            foreach (var file in report.files) if (!File.Exists(file.path) || Hash(file.path) != file.sha256) throw new IOException("生成源码或Luban数据已修改，拒绝使用过期配置: " + file.path);
            if (File.Exists(report.scene))
            { if (report.status == "scene-ready-runtime-verification-pending") return; throw new IOException("场景文件已存在，未覆盖"); }
            var profile = Profile(report.profile);
            if (profile.generatedRoot != report.generatedRoot || profile.generatedNamespace != report.generatedNamespace || profile.assemblyName != report.generatedAssembly) throw new InvalidOperationException("Luban配置已更改，请重新生成播放器");
            var type = Type.GetType(report.playbackNamespace + ".PlotOriginalLubanPlayer, " + report.playbackAssembly);
            if (type == null) throw new InvalidOperationException("请等待刚生成的完整Luban播放器编译完成，再创建场景");
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var visual = AssetDatabase.LoadAssetAtPath<PlotPlaybackVisualCatalog>(report.catalog); if (visual == null) throw new InvalidOperationException("原剧情资源配置不存在");
                var go = new GameObject("OriginalLubanBinaryPlayer", typeof(PlotPlayerHost), typeof(AudioListener)); var host = go.GetComponent<PlotPlayerHost>(); host.playOnStart = false;
                host.sprites = visual.sprites; host.viewPrefab = visual.viewPrefab; host.actorPrefabs = visual.actorPrefabs; host.audioBindings = visual.audioBindings; host.effects = visual.effects; host.emotions = visual.emotions; host.fontAsset = visual.fontAsset; host.inlineSprites = visual.inlineSprites;
                if (visual.playerConfig != null) host.config = JsonUtility.FromJson<PlotPlayerConfig>(JsonUtility.ToJson(visual.playerConfig));
                var component = go.AddComponent(type); var serialized = new SerializedObject(component); var entries = serialized.FindProperty("binaries");
                var paths = report.files.Where(f => f.path.EndsWith(".bytes", StringComparison.Ordinal)).Select(f => f.path).OrderBy(p => p, StringComparer.Ordinal).ToArray(); entries.arraySize = paths.Length;
                for (var i = 0; i < paths.Length; i++) { var binary = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]); if (binary == null) throw new IOException("Luban二进制未导入: " + paths[i]); entries.GetArrayElementAtIndex(i).objectReferenceValue = binary; }
                serialized.FindProperty("entryNode").intValue = report.entryNode; serialized.ApplyModifiedPropertiesWithoutUndo();
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                if (!EditorSceneManager.SaveScene(scene, report.scene)) throw new IOException("完整Luban播放场景保存失败");
                report.status = "scene-ready-runtime-verification-pending"; report.files.Add(new FileStamp { path = report.scene, sha256 = Hash(report.scene) }); File.WriteAllText(LatestPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception error) { report.status = "failed"; report.error = error.ToString(); throw; }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); Save(report); }
        }
    }
}
