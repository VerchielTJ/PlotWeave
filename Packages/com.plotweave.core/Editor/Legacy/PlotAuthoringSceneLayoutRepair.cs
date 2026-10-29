using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>Explicit migration of the owned authoring scene's static canvas. Never initializes a session in EditMode.</summary>
    public static class PlotAuthoringSceneLayoutRepair
    {
        [Serializable] private sealed class Source { public string path = "", after = ""; }
        [Serializable] private sealed class Evidence { public string phase = ""; public List<Source> sources = new List<Source>(); }
        [Serializable] private sealed class Result
        {
            public string id, scene, before, after, backup, status;
            public string previousMode;
            public Vector2 previousReference, reference;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("Tools/PlotWeave/制作工具/修正本地制作场景静态缩放")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先结束制作模式，再修正保存场景的缩放设置");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存的场景，拒绝替你保存或切换");
            var interaction = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            var path = interaction.authoringScene;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/PlotWeaveLocal/OriginalUI/", StringComparison.Ordinal) || !File.Exists(path))
                throw new InvalidOperationException("不是已登记的本地迁移制作场景");
            var evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(PlotAuthoringWorkflowVerification.LatestPath));
            var source = evidence.sources?.SingleOrDefault(s => s.path == path);
            if (evidence.phase != "passed" || source == null || source.after != Hash(path))
                throw new InvalidOperationException("制作场景与上轮记录不符；拒绝覆盖可能已保存的个人修改");
            var result = new Result { id = Guid.NewGuid().ToString("N"), scene = path, before = Hash(path), status = "started" };
            var scene = SceneManager.GetActiveScene();
            if (scene.path != path) scene = EditorSceneManager.OpenScene(path);
            var canvases = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Where(c => c.name == "PlotEditorCanvas").ToArray();
            if (canvases.Length != 1) throw new InvalidOperationException("制作场景Canvas不唯一");
            var scaler = canvases[0].GetComponent<CanvasScaler>();
            if (scaler == null) throw new InvalidOperationException("制作场景缺少CanvasScaler");
            result.previousMode = scaler.uiScaleMode.ToString(); result.previousReference = scaler.referenceResolution;
            result.reference = new Vector2(3200, 1500);
            result.backup = "Library/PlotWeaveRecovery/StaticLayout/" + result.id + "/OriginalAuthoring.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(result.backup));
            File.Copy(path, result.backup, false); File.Copy(path + ".meta", result.backup + ".meta", false);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = result.reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("仅保存制作场景失败；原文件备份位于 " + result.backup);
            result.after = Hash(path); result.status = "passed";
            const string folder = "Doc/Records/Stage20";
            Directory.CreateDirectory(folder);
            var json = JsonUtility.ToJson(result, true);
            File.WriteAllText(folder + "/static-layout-" + result.id + ".json", json);
            File.WriteAllText("Tools/PlotWeaveMigration/authoring-static-layout.json", json);
            Debug.Log("PlotWeave制作场景静态缩放已修正；实际编辑请从 Tools/PlotWeave/打开剧情编辑器 进入制作模式。");
        }
    }
}
