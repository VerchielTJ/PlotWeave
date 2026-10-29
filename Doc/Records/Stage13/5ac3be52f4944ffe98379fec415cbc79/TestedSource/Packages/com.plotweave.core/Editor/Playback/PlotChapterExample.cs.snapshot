using System.IO;
using PlotWeave.Chapters;
using PlotWeave.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlotWeave.Authoring.Playback
{
    public static class PlotChapterExample
    {
        public const string Root = "Assets/PlotWeaveMigration/Stage10_Chapters";
        public const string ScenePath = Root + "/ChapterHost.unity";
        [MenuItem("Tools/PlotWeave/迁移样例/创建主线与日常章节宿主")]
        public static void PrepareVerification()
        {
            PlotAdvancedExample.PrepareVerification(); Directory.CreateDirectory(Root); AssetDatabase.Refresh(); if (File.Exists(ScenePath)) return;
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var source = SceneManager.GetSceneByPath(PlotAdvancedExample.ScenePath); var opened = !source.IsValid() || !source.isLoaded; if (opened) source = EditorSceneManager.OpenScene(PlotAdvancedExample.ScenePath, OpenSceneMode.Additive);
                try { foreach (var root in source.GetRootGameObjects()) { var clone = UnityEngine.Object.Instantiate(root); clone.name = root.name; SceneManager.MoveGameObjectToScene(clone, scene); var host = clone.GetComponent<PlotPlayerHost>(); if (host != null) { host.playOnStart = false; var demo = clone.AddComponent<PlotChapterDemo>(); demo.content = host.source; demo.persistProgress = true; } } }
                finally { if (opened) EditorSceneManager.CloseScene(source, true); }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
