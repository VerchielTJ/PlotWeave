using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Editor
{
    [CustomPropertyDrawer(typeof(PlotCommand))]
    public sealed class PlotCommandDrawer : PropertyDrawer
    {
        private static float Line => EditorGUIUtility.singleLineHeight + 3;
        private static PlotCommandDefinition Definition(SerializedProperty property)
        {
            var asset = property.serializedObject.targetObject as PlotAsset;
            return asset == null ? null : asset.document.commands.Find(d => d.key == property.FindPropertyRelative("key").stringValue);
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return Line;
            var definition = Definition(property);
            return definition == null ? Line * 2 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("arguments"), true) : Line * (definition.parameters.Count + 2);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                row.y += Line;
                var key = property.FindPropertyRelative("key");
                var asset = property.serializedObject.targetObject as PlotAsset;
                var definitions = asset?.document.commands;
                if (definitions != null && definitions.Count > 0)
                {
                    var names = new[] { key.stringValue + "（当前）" }.Concat(definitions.Select(d => d.displayName + " · " + d.key)).ToArray();
                    var picked = EditorGUI.Popup(row, "事件", 0, names);
                    if (picked > 0)
                    {
                        var selected = definitions[picked - 1]; key.stringValue = selected.key;
                        var arguments = property.FindPropertyRelative("arguments"); arguments.ClearArray();
                        foreach (var p in selected.parameters) Set(arguments, p.key, p.defaultValue);
                    }
                }
                else EditorGUI.PropertyField(row, key);
                row.y += Line;
                var definition = Definition(property);
                if (definition == null)
                {
                    var arguments = property.FindPropertyRelative("arguments");
                    row.height = EditorGUI.GetPropertyHeight(arguments, true); EditorGUI.PropertyField(row, arguments, true);
                }
                else
                {
                    foreach (var parameter in definition.parameters)
                    {
                        var arguments = property.FindPropertyRelative("arguments");
                        var value = Get(arguments, parameter.key, parameter.defaultValue);
                        var display = new GUIContent(string.IsNullOrEmpty(parameter.displayName) ? parameter.key : parameter.displayName, parameter.key + " · " + parameter.type);
                        EditorGUI.BeginChangeCheck();
                        var edited = Value(row, display, value, parameter, asset);
                        if (EditorGUI.EndChangeCheck()) Set(arguments, parameter.key, edited);
                        row.y += Line;
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
        private static string Value(Rect rect, GUIContent label, string value, PlotParameterDefinition p, PlotAsset asset)
        {
            switch (p.type)
            {
                case PlotValueType.Bool: return EditorGUI.Toggle(rect, label, bool.TryParse(value, out var b) && b).ToString().ToLowerInvariant();
                case PlotValueType.Int:
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i);
                    return EditorGUI.IntField(rect, label, i).ToString(CultureInfo.InvariantCulture);
                case PlotValueType.Float:
                    float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f);
                    return EditorGUI.FloatField(rect, label, f).ToString("R", CultureInfo.InvariantCulture);
                case PlotValueType.Color:
                    ColorUtility.TryParseHtmlString(value, out var color);
                    return "#" + ColorUtility.ToHtmlStringRGBA(EditorGUI.ColorField(rect, label, color));
                case PlotValueType.Enum:
                    var values = asset.document.enums.Find(e => e.key == p.enumKey)?.values;
                    if (values != null && values.Count > 0)
                    {
                        var index = values.IndexOf(value);
                        // 不静默替换历史非法值，校验器会提示；用户选择后再写入。
                        var options = new[] { value + "（当前）" }.Concat(values).ToArray();
                        var picked = EditorGUI.Popup(rect, label.text, 0, options);
                        return picked > 0 ? values[picked - 1] : value;
                    }
                    break;
            }
            return EditorGUI.TextField(rect, label, value);
        }
        private static string Get(SerializedProperty arguments, string key, string fallback)
        {
            for (var i = 0; i < arguments.arraySize; i++)
            {
                var item = arguments.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("key").stringValue == key) return item.FindPropertyRelative("value").stringValue;
            }
            return fallback;
        }
        private static void Set(SerializedProperty arguments, string key, string value)
        {
            for (var i = 0; i < arguments.arraySize; i++)
            {
                var item = arguments.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("key").stringValue == key) { item.FindPropertyRelative("value").stringValue = value; return; }
            }
            arguments.InsertArrayElementAtIndex(arguments.arraySize);
            var added = arguments.GetArrayElementAtIndex(arguments.arraySize - 1);
            added.FindPropertyRelative("key").stringValue = key; added.FindPropertyRelative("value").stringValue = value;
        }
    }
}
