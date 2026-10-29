using System;
using System.Collections.Generic;
using System.IO;
using PlotWeave.Authoring;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Generation
{
    [CreateAssetMenu(menuName = "PlotWeave/生成/数据契约生成配置")]
    public sealed class PlotGenerationProfile : ScriptableObject
    {
        public PlotAtomDataHeaderAsset header;
        public string targetNamespace = "PlotWeave.Generated";
        public string nodeName = "PlotAtomData";
        public string outputFolder = "Assets/PlotWeaveGenerated";
        public List<PlotTableGeneration> tables = new List<PlotTableGeneration>();
        public List<PlotKeyValueGeneration> texts = new List<PlotKeyValueGeneration>();
        public void Generate()
        {
            var root = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var output = Path.GetFullPath(outputFolder);
            if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("界面生成目录必须位于本项目Assets子目录");
            PlotDataStructureGenerator.GenerateAllDataStructures(header, tables, texts, targetNamespace, nodeName).Write(output);
            AssetDatabase.Refresh();
            Debug.Log("PlotWeave 数据契约与双端适配器已生成: " + outputFolder, this);
        }
    }

    [CustomEditor(typeof(PlotGenerationProfile))]
    public sealed class PlotGenerationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!GUILayout.Button("生成共同契约与Editor/Game适配器")) return;
            try { ((PlotGenerationProfile)target).Generate(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
