using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PlotWeave.Playback
{
    public enum PlotPlayerRuntimeMode { Editor, GameRuntime }
    public interface IPlotPlayerDataProvider
    {
        bool TryGetNode(int id, out PlotPlaybackNode node);
        bool TryGetActor(int id, out PlotPlaybackActor actor);
        bool TryGetText(string key, bool constant, int language, out string text);
    }
    public sealed class PlotPlayerDataProvider : IPlotPlayerDataProvider
    {
        private Dictionary<int, PlotPlaybackNode> _nodes;
        private Dictionary<int, PlotPlaybackActor> _actors;
        private Dictionary<string, PlotPlaybackText> _dialogs, _constants;
        public PlotPlayerDataProvider(PlotPlaybackDocument data) { Replace(data); }
        public void Replace(PlotPlaybackDocument data)
        {
            var snapshot = (data ?? throw new ArgumentNullException(nameof(data))).Snapshot();
            var nodes = snapshot.Nodes.ToDictionary(n => n.ScenesID); var actors = snapshot.Actors.ToDictionary(a => a.ID);
            var dialogs = snapshot.Dialogs.ToDictionary(t => t.Key, StringComparer.Ordinal); var constants = snapshot.Constants.ToDictionary(t => t.Key, StringComparer.Ordinal);
            _nodes = nodes; _actors = actors; _dialogs = dialogs; _constants = constants;
        }
        public bool TryGetNode(int id, out PlotPlaybackNode node)
        { _nodes.TryGetValue(id, out var value); node = value == null ? null : JsonUtility.FromJson<PlotPlaybackNode>(JsonUtility.ToJson(value)); return node != null; }
        public bool TryGetActor(int id, out PlotPlaybackActor actor)
        { _actors.TryGetValue(id, out var value); actor = value == null ? null : new PlotPlaybackActor { ID = value.ID, Name = value.Name, Thespian = value.Thespian }; return actor != null; }
        public bool TryGetText(string key, bool constant, int language, out string text)
        {
            if (language < 0 || language >= 5) throw new ArgumentOutOfRangeException(nameof(language));
            var entry = key != null && (constant ? _constants : _dialogs).TryGetValue(key, out var value) ? value : null;
            text = entry == null ? "" : entry.Languages[language] ?? ""; return entry != null;
        }
    }
    public interface IPlotTextProcessor { int Language { get; set; } string Process(string value, bool constant = false); }
    public sealed class PlotPlayerTextProcessor : IPlotTextProcessor
    {
        private readonly IPlotPlayerDataProvider _data;
        private int _language;
        public int Language { get => _language; set { if (value < 0 || value >= 5) throw new ArgumentOutOfRangeException(nameof(value)); _language = value; } }
        public PlotPlayerTextProcessor(IPlotPlayerDataProvider data) { _data = data ?? throw new ArgumentNullException(nameof(data)); }
        public string Process(string value, bool constant = false)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = _data.TryGetText(value, constant, Language, out var translated) ? translated : value;
            var output = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\\' || i + 1 >= text.Length) { output.Append(text[i]); continue; }
                var next = text[i + 1];
                if (next == 'n' || next == 'r' || next == 't' || next == '\\' || next == '\'' || next == '"') { i++; output.Append(next == 'n' ? '\n' : next == 'r' ? '\r' : next == 't' ? '\t' : next); }
                else output.Append(text[i]);
            }
            return output.ToString();
        }
    }
    public interface IPlotRuntimeEnvironment { PlotPlayerRuntimeMode Mode { get; } float DeltaTime { get; } }
    public sealed class PlotPlayerRuntimeEnvironment : IPlotRuntimeEnvironment
    {
        public PlotPlayerRuntimeMode Mode { get; }
        public float DeltaTime => Time.unscaledDeltaTime;
        public PlotPlayerRuntimeEnvironment(PlotPlayerRuntimeMode mode) { Mode = mode; }
    }
    [Serializable] public sealed class PlotPlayerConfig
    {
        public float TextSpeed = .05f;
        public float AutoDelay = 1;
        public bool AutoPlay;
        public bool EnableBranches = true;
        public float CurtainFadeTime = 2;
        public float CurtainDisplayTime = 1;
        public float CharacterAnimationSpeed = 1;
        public float CharacterAnimationDuration = .35f;
        public float BackgroundTransitionTime = .4f;
        public Vector2[] Positions = { new Vector2(-600, -10), new Vector2(-400, -10), new Vector2(-200, -10), new Vector2(0, -10), new Vector2(200, -10), new Vector2(400, -10), new Vector2(600, -10) };
        public void Validate()
        {
            foreach (var number in new[] { TextSpeed, AutoDelay, CurtainFadeTime, CurtainDisplayTime, CharacterAnimationSpeed, CharacterAnimationDuration, BackgroundTransitionTime }) if (float.IsNaN(number) || float.IsInfinity(number) || number < 0) throw new ArgumentException("播放时间参数必须为有限非负数");
            if (Positions == null || Positions.Length == 0 || Positions.Any(p => float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y))) throw new ArgumentException("角色位置配置无效");
        }
    }
    public interface IPlotResourceLoader
    { IDisposable LoadSprite(string key, Action<Sprite, string> completed); }
    public sealed class PlotPlayerResourceLoader : IPlotResourceLoader, IPlotAudioResourceLoader, IPlotPrefabResourceLoader
    {
        private readonly IDictionary<string, Sprite> _bindings;
        private readonly IDictionary<string, AudioClip> _audio;
        private readonly IDictionary<string, GameObject> _prefabs;
        public PlotPlayerResourceLoader(IDictionary<string, Sprite> bindings = null, IDictionary<string, AudioClip> audio = null, IDictionary<string, GameObject> prefabs = null)
        { _bindings = bindings == null ? new Dictionary<string, Sprite>() : new Dictionary<string, Sprite>(bindings); _audio = audio == null ? new Dictionary<string, AudioClip>() : new Dictionary<string, AudioClip>(audio); _prefabs = prefabs == null ? new Dictionary<string, GameObject>() : new Dictionary<string, GameObject>(prefabs); }
        private sealed class Request : IDisposable { public bool Cancelled; public void Dispose() { Cancelled = true; } }
        public IDisposable LoadSprite(string key, Action<Sprite, string> completed)
            => Load(key, _bindings, completed);
        public IDisposable LoadAudio(string key, Action<AudioClip, string> completed) => Load(key, _audio, completed);
        public IDisposable LoadPrefab(string key, Action<GameObject, string> completed) => Load(key, _prefabs, completed);
        private static IDisposable Load<T>(string key, IDictionary<string, T> bindings, Action<T, string> completed) where T : UnityEngine.Object
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed)); var ticket = new Request();
            if (bindings.TryGetValue(key ?? "", out var sprite)) { completed(sprite, sprite == null ? "资源绑定为空: " + key : ""); return ticket; }
            if (string.IsNullOrEmpty(key)) { completed(null, "资源Key为空"); return ticket; }
            var request = Resources.LoadAsync<T>(key);
            request.completed += ignored => { if (!ticket.Cancelled) completed(request.asset as T, request.asset == null ? "未找到资源: " + key : ""); };
            return ticket;
        }
    }
    public interface IPlotObjectPool : IDisposable
    { GameObject Rent(string key, Transform parent, Func<GameObject> create); void Return(GameObject value); }
    public sealed class PlotPlayerObjectPool : IPlotObjectPool
    {
        private readonly Dictionary<string, Stack<GameObject>> _available = new Dictionary<string, Stack<GameObject>>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, string> _keys = new Dictionary<GameObject, string>();
        private readonly HashSet<GameObject> _rented = new HashSet<GameObject>();
        private bool _disposed;
        public int CreatedCount => _keys.Count;
        public GameObject Rent(string key, Transform parent, Func<GameObject> create)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PlotPlayerObjectPool));
            key = key ?? ""; if (!_available.TryGetValue(key, out var pool)) _available.Add(key, pool = new Stack<GameObject>());
            GameObject value = null; while (pool.Count > 0 && value == null) value = pool.Pop();
            if (value == null) { value = create() ?? throw new InvalidOperationException("角色工厂返回空对象"); _keys.Add(value, key); }
            _rented.Add(value); value.transform.SetParent(parent, false); value.SetActive(true); return value;
        }
        public void Return(GameObject value)
        {
            if (value == null) return; if (!_keys.TryGetValue(value, out var key) || !_rented.Remove(value)) throw new InvalidOperationException("归还了不属于本池或已经归还的对象");
            value.SetActive(false); _available[key].Push(value);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var value in _keys.Keys) if (value != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
            _keys.Clear(); _available.Clear(); _rented.Clear();
        }
    }
}
