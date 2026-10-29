using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring.Playback
{
    [CustomEditor(typeof(PlotNodeEventTrackAsset))]
    public sealed class PlotEventTrackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var asset = (PlotNodeEventTrackAsset)target;
            EditorGUILayout.HelpBox("executionOrder=-1按列表顺序执行；非负值是回调ID，仅在callbackList引用时执行。等待、变量、信号和表现事件共用可取消调度。", MessageType.Info);
            if (GUILayout.Button("补齐缺失或重复的事件GUID"))
            {
                Undo.RecordObject(asset, "修正事件GUID"); var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var track in asset.tracks) if (track?.events != null) foreach (var data in track.events) if (data != null && (string.IsNullOrWhiteSpace(data.eventId) || !seen.Add(data.eventId))) { data.eventId = Guid.NewGuid().ToString(); seen.Add(data.eventId); }
                EditorUtility.SetDirty(asset);
            }
            if (GUILayout.Button("校验轨道并保存")) { asset.Validate().ThrowIfInvalid(); AssetDatabase.SaveAssetIfDirty(asset); Debug.Log("事件轨道校验通过"); }
            if (GUILayout.Button("导出独立轨道JSON"))
            { var json = asset.ExportJson(); var path = EditorUtility.SaveFilePanel("导出事件轨道", "", "PlotEvents.json", "json"); if (path != "") File.WriteAllText(path, json); }
            if (GUILayout.Button("从JSON检查并导入"))
            { var path = EditorUtility.OpenFilePanel("导入事件轨道", "", "json"); if (path != "") { Undo.RecordObject(asset, "导入事件轨道"); asset.ImportJson(File.ReadAllText(path)); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); } }
        }
    }
}
