using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Editor
{
    public sealed class PlotPreviewWindow : EditorWindow
    {
        private PlotAsset _asset;
        private PlotSession _session;
        private double _lastTime;
        private readonly List<string> _events = new List<string>();
        private readonly Dictionary<string, string> _actors = new Dictionary<string, string>();
        private string _background = "";
        private Vector2 _scroll;
        private bool _paused;
        private string _fromId;
        public static void Open(PlotAsset asset, string nodeId)
        {
            var window = GetWindow<PlotPreviewWindow>("织幕预览"); window._asset = asset; window._fromId = nodeId; window.Play();
        }
        private void OnEnable() { EditorApplication.update += Tick; _lastTime = EditorApplication.timeSinceStartup; }
        private void OnDisable() { EditorApplication.update -= Tick; _session?.Dispose(); }
        private void Play()
        {
            _session?.Dispose(); _session = new PlotSession(); _events.Clear(); _actors.Clear(); _background = ""; _paused = false;
            _session.PresentationRequested += (command, context) =>
            {
                _events.Add(command.key + "  " + JsonUtility.ToJson(command));
                if (_events.Count > 50) _events.RemoveAt(0);
                if (command.key == "background") _background = command.Get("key");
                if (command.key == "actor.show" || command.key == "actor.move") _actors[command.Get("id")] = command.Get("position", "0.5");
                if (command.key == "actor.hide") _actors.Remove(command.Get("id"));
            };
            _session.Start(_asset, _fromId); _lastTime = EditorApplication.timeSinceStartup; Repaint();
        }
        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            if (!_paused && _session != null) _session.Tick((float)Math.Min(0.1, now - _lastTime));
            _lastTime = now;
            if (_session != null) Repaint();
        }
        private void OnGUI()
        {
            if (_asset == null) { EditorGUILayout.HelpBox("请从剧情编辑器打开预览。", MessageType.Info); return; }
            EditorGUILayout.LabelField(_asset.document.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("预览直接读取剧情资产，和 Player 使用同一个 PlotSession。");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重播")) Play();
                if (GUILayout.Button(_paused ? "恢复" : "暂停")) _paused = !_paused;
                if (GUILayout.Button("停止")) _session?.Stop();
            }
            if (_session == null) return;
            EditorGUILayout.LabelField("状态", _session.State.ToString());
            EditorGUILayout.LabelField("节点", _session.Current?.id ?? "");
            EditorGUILayout.LabelField("背景", _background);
            foreach (var actor in _actors) EditorGUILayout.LabelField("角色 " + actor.Key, "位置 " + actor.Value);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_session.Speaker, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_session.Line, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(85));
            if (_session.State == PlotPlaybackState.WaitingForInput && !_paused)
            {
                if (_session.Choices.Count == 0) { if (GUILayout.Button("继续")) _session.Advance(); }
                else for (var i = 0; i < _session.Choices.Count; i++)
                    if (GUILayout.Button(_session.ChoiceText(_session.Choices[i]))) { _session.Choose(i); break; }
            }
            if (_session.State == PlotPlaybackState.Faulted) EditorGUILayout.HelpBox(_session.Error, MessageType.Error);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("事件轨迹", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var item in _events) EditorGUILayout.LabelField(item, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
