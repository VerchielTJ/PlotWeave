using UnityEditor;
using UnityEngine;

namespace PlotWeave.Authoring
{
    // 第一批提供原生序列化字段和检查入口；原UGUI动态编辑面板在后续批次迁移。
    [CustomEditor(typeof(PlotAuthoringAsset), true)]
    public sealed class PlotAuthoringAssetEditor : UnityEditor.Editor
    {
        private AuthoringValidationResult _result;
        private readonly PlotAuthoringInspectorControls _controls = new PlotAuthoringInspectorControls();
        public override void OnInspectorGUI()
        {
            Undo.RecordObject(target, "编辑剧情作者数据");
            EditorGUI.BeginChangeCheck();
            if (!_controls.Draw((PlotAuthoringAsset)target, this)) DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) { EditorUtility.SetDirty(target); _result = null; }
            if (GUILayout.Button("校验数据（不修改内容）")) _result = ((PlotAuthoringAsset)target).Validate();
            if (GUILayout.Button("校验并保存资产"))
            {
                _result = ((PlotAuthoringAsset)target).Validate();
                if (_result.IsValid) ((PlotAuthoringAsset)target).SetDirtyAndSave();
            }
            if (target is PlotAtomDataHeaderAsset header && GUILayout.Button("从Bean定义同步成员列数"))
            {
                Undo.RecordObject(header, "同步Bean成员列数");
                header.SyncCustomBeanMembersCount();
                EditorUtility.SetDirty(header); serializedObject.Update(); _result = header.Validate();
            }
            if (_result != null) EditorGUILayout.HelpBox(_result.ToString(), _result.IsValid ? MessageType.Info : MessageType.Error);
        }
    }

    public static class PlotAuthoringPersistence
    {
        // 将保存能力留在Editor程序集，Runtime模型不引用UnityEditor。
        public static void SetDirtyAndSave(this PlotAuthoringAsset asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
