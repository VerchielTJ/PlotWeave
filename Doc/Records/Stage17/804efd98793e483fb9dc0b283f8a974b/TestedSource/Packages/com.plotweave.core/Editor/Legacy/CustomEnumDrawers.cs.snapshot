using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Authoring;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    public static class PlotLegacyEnumSource
    {
        public static PlotEnumAsset Resolve(SerializedObject owner, string member)
        {
            if (owner.targetObject is PlotEnumAsset asset) return asset;
            var source = owner.FindProperty(member);
            return source != null && source.propertyType == SerializedPropertyType.ObjectReference ? source.objectReferenceValue as PlotEnumAsset : null;
        }
        public static void Draw(Rect rect, SerializedProperty property, GUIContent label, string member, string enumName)
        {
            if (property.propertyType != SerializedPropertyType.String) { EditorGUI.LabelField(rect, label.text, "枚举标记用于字符串字段"); return; }
            var asset = Resolve(property.serializedObject, member);
            if (asset == null) { EditorGUI.PropertyField(rect, property, label); return; }
            asset.Validate().ThrowIfInvalid();
            var keys = enumName == null ? asset.GetAllEnumNames().ToList() : asset.GetEnumKeys(enumName).ToList();
            var labels = enumName == null ? asset.GetAllEnumDisplayNames().ToList() : asset.GetEnumLabels(enumName).ToList();
            var index = keys.IndexOf(property.stringValue);
            if (index < 0) { keys.Insert(0, property.stringValue); labels.Insert(0, "未绑定：" + property.stringValue); index = 0; }
            EditorGUI.BeginChangeCheck(); var selected = EditorGUI.Popup(rect, label, index, labels.Select(s => new GUIContent(s)).ToArray());
            if (EditorGUI.EndChangeCheck() && selected >= 0 && selected < keys.Count) property.stringValue = keys[selected];
        }
    }
    [CustomPropertyDrawer(typeof(CustomEnumAttribute))]
    public sealed class CustomEnumDrawer : PropertyDrawer
    { public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label) { var option = (CustomEnumAttribute)attribute; PlotLegacyEnumSource.Draw(rect, property, label, option.SourceMember, option.EnumName); } }
    [CustomPropertyDrawer(typeof(EnumTypeAttribute))]
    public sealed class EnumTypeDrawer : PropertyDrawer
    { public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label) => PlotLegacyEnumSource.Draw(rect, property, label, ((EnumTypeAttribute)attribute).SourceMember, null); }
}
