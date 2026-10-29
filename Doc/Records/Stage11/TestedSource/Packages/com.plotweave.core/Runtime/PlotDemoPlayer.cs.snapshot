using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlotWeave
{
    [Serializable] public sealed class PlotSpriteBinding { public string key; public Sprite sprite; }
    [Serializable] public sealed class PlotAudioBinding { public string key; public AudioClip clip; }

    /// <summary>零美术依赖的参考播放壳。实际项目可用自己的 UI 订阅 Session。</summary>
    public sealed class PlotDemoPlayer : MonoBehaviour
    {
        public PlotAsset story;
        public TextAsset compiledJson;
        public bool playOnStart = true;
        public float charactersPerSecond = 35;
        public bool autoAdvance;
        public float autoAdvanceDelay = 1.5f;
        public List<PlotSpriteBinding> sprites = new List<PlotSpriteBinding>();
        public List<PlotAudioBinding> audioClips = new List<PlotAudioBinding>();
        private readonly Dictionary<string, string> _actors = new Dictionary<string, string>();
        private string _background = "";
        private string _event = "";
        private PlotSession _session;
        private float _visible;
        private float _autoTime;
        private AudioSource _bgm;
        private AudioSource _sfx;
        public PlotSession Session => _session;

        private void Start() { if (playOnStart) Play(); }
        public void Play()
        {
            _session?.Dispose(); _actors.Clear(); _background = ""; _event = "";
            _session = new PlotSession();
            _session.PresentationRequested += Present;
            _session.Changed += () => { _visible = 0; _autoTime = 0; };
            IPlotDataSource source = compiledJson != null ? new JsonPlotDataSource(compiledJson.text) : story;
            if (source == null) source = new JsonPlotDataSource(JsonUtility.ToJson(PlotSample.Create()));
            _session.Start(source);
        }
        private void Update()
        {
            if (_session == null) return;
            _session.Tick(Time.unscaledDeltaTime);
            if (_session.State != PlotPlaybackState.WaitingForInput) return;
            _visible += Time.unscaledDeltaTime * Mathf.Max(1, charactersPerSecond);
            if (autoAdvance && _session.Choices.Count == 0 && _visible >= _session.Line.Length)
            {
                _autoTime += Time.unscaledDeltaTime;
                if (_autoTime >= autoAdvanceDelay) _session.Advance();
            }
        }
        private void OnDisable() { _session?.Stop(); _bgm?.Stop(); _sfx?.Stop(); }
        private void OnDestroy() { _session?.Dispose(); }

        private void Present(PlotCommand command, PlotContext context)
        {
            _event = command.key + " " + command.Get("name", command.Get("key"));
            var id = command.Get("id");
            if (command.key == "actor.show") _actors[id] = command.Get("position", "0.5");
            if (command.key == "actor.hide") _actors.Remove(id);
            if (command.key == "actor.move") _actors[id] = command.Get("position", "0.5");
            if (command.key == "actor.emote") _event = id + " · " + command.Get("emote");
            if (command.key == "background") _background = command.Get("key");
            if (command.key == "audio.bgm" || command.key == "audio.sfx")
            {
                var binding = audioClips.Find(a => a.key == command.Get("key"));
                if (binding == null) { Debug.LogWarning("未绑定音频资源：" + command.Get("key"), this); return; }
                if (command.key == "audio.bgm")
                {
                    if (_bgm == null) _bgm = gameObject.AddComponent<AudioSource>();
                    _bgm.clip = binding.clip; _bgm.loop = true; _bgm.Play();
                }
                else { if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>(); _sfx.PlayOneShot(binding.clip); }
            }
        }

        private void OnGUI()
        {
            if (_session == null) return;
            var width = Mathf.Min(880, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 20, width, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("PlotWeave 织幕 · 参考播放器");
            GUILayout.Label("背景：" + _background);
            var background = sprites.Find(s => s.key == _background)?.sprite;
            if (background != null) GUILayout.Label(background.texture, GUILayout.Height(110));
            foreach (var actor in _actors)
            {
                var data = _session.Context?.Document.actors.Find(a => a.id == actor.Key);
                GUILayout.Label((data?.name ?? actor.Key) + " 位置 " + actor.Value);
                var image = sprites.Find(s => s.key == data?.resourceKey)?.sprite;
                if (image != null) GUILayout.Label(image.texture, GUILayout.Height(110));
            }
            GUILayout.Label("事件：" + _event);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_session.Speaker);
            var text = _session.Line;
            GUILayout.Label(text.Substring(0, Mathf.Clamp((int)_visible, 0, text.Length)), GUILayout.MinHeight(90));
            if (_session.State == PlotPlaybackState.WaitingForInput)
            {
                if (_session.Choices.Count > 0)
                {
                    for (var i = 0; i < _session.Choices.Count; i++)
                        if (GUILayout.Button(_session.ChoiceText(_session.Choices[i]), GUILayout.Height(36))) { _session.Choose(i); break; }
                }
                else if (GUILayout.Button("继续", GUILayout.Height(36)))
                { if (_visible < text.Length) _visible = text.Length; else _session.Advance(); }
            }
            if (_session.State == PlotPlaybackState.Faulted) GUILayout.Label("播放失败：" + _session.Error);
            if (_session.State == PlotPlaybackState.Completed) GUILayout.Label("剧情结束");
            autoAdvance = GUILayout.Toggle(autoAdvance, "自动播放");
            if (GUILayout.Button("重新播放")) Play();
            GUILayout.EndArea();
        }
    }
}
