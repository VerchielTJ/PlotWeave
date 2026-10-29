using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PlotWeave.Playback
{
    [Serializable] public sealed class PlotPlaybackSpriteBinding { public string key = ""; public Sprite sprite; }
    public sealed class PlotPlayerHost : MonoBehaviour
    {
        public PlotPlaybackAsset source;
        public TextAsset compiledJson;
        public int entryNode = 1001;
        public bool playOnStart = true;
        public PlotPlayerConfig config = new PlotPlayerConfig();
        public List<PlotPlaybackSpriteBinding> sprites = new List<PlotPlaybackSpriteBinding>();
        public TMP_FontAsset fontAsset;
        public TMP_SpriteAsset inlineSprites;
        public GameObject viewPrefab;
        public List<PlotActorPrefabBinding> actorPrefabs = new List<PlotActorPrefabBinding>();
        public List<PlotPlaybackAudioBinding> audioBindings = new List<PlotPlaybackAudioBinding>();
        public List<PlotPlaybackEffectBinding> effects = new List<PlotPlaybackEffectBinding>();
        public List<PlotPlaybackEmotionBinding> emotions = new List<PlotPlaybackEmotionBinding>();
        private GameObject _viewInstance;
        public PlotPlayerCore Core { get; private set; }
        public PlotPlayerView View { get; private set; }
        private PlotPlayerObjectPool _pool;
        public event Action Completed;
        private void Start()
        {
            if (Core != null || !playOnStart || (source == null && compiledJson == null)) return;
            Initialize(compiledJson != null ? JsonUtility.FromJson<PlotPlaybackDocument>(compiledJson.text) : source.data, PlotPlayerRuntimeMode.GameRuntime);
            Play(entryNode);
        }
        public void Initialize(PlotPlaybackDocument data, PlotPlayerRuntimeMode mode)
        {
            config.Validate();
            var bindings = new Dictionary<string, Sprite>(StringComparer.Ordinal); foreach (var item in sprites) { if (item == null || string.IsNullOrWhiteSpace(item.key) || bindings.ContainsKey(item.key)) throw new ArgumentException("资源绑定Key为空或重复: "+item?.key); bindings.Add(item.key, item.sprite); }
            var provider = PlotPlayerFactory.CreateDataProvider(data);
            var soundBindings = new Dictionary<string, AudioClip>(StringComparer.Ordinal); foreach (var item in audioBindings) { if (item == null || string.IsNullOrWhiteSpace(item.key) || item.clip == null || soundBindings.ContainsKey(item.key)) throw new ArgumentException("音频绑定无效或重复"); soundBindings.Add(item.key, item.clip); }
            var effectBindings = new Dictionary<string, GameObject>(StringComparer.Ordinal); foreach (var item in effects) { if (item == null || string.IsNullOrWhiteSpace(item.key) || item.prefab == null || effectBindings.ContainsKey(item.key)) throw new ArgumentException("特效绑定无效或重复"); effectBindings.Add(item.key, item.prefab); }
            Core?.Dispose(); _pool?.Dispose(); Core = null; _pool = PlotPlayerFactory.CreateObjectPool();
            Core = new PlotPlayerCore(provider, PlotPlayerFactory.CreateResourceLoader(bindings, soundBindings, effectBindings), _pool, PlotPlayerFactory.CreateTextProcessor(provider), PlotPlayerFactory.CreateRuntimeEnvironment(mode), config);
            if (viewPrefab != null)
            { if (_viewInstance == null) _viewInstance = Instantiate(viewPrefab, transform, false); View = _viewInstance.GetComponent<PlotPlayerView>() ?? _viewInstance.AddComponent<PlotPlayerView>(); }
            else View = GetComponent<PlotPlayerView>() ?? gameObject.AddComponent<PlotPlayerView>();
            View.ConfigureText(fontAsset, inlineSprites); View.ConfigureActors(actorPrefabs); View.ConfigureEmotions(emotions); Core.Init(View); View.BindCore(Core);
            if (_viewInstance != null && !_viewInstance.activeSelf) _viewInstance.SetActive(true);
            var skip=View.GetComponent<PlotWeave.Legacy.PlotLegacySkipController>();if(skip!=null)skip.Bind(Core);
            Core.OnPlotCompleted += () => Completed?.Invoke(); Core.OnError += error => Debug.LogWarning("PlotWeave播放失败: " + error);
        }
        public void Play(int id) { Core.Open(); Core.LoadPlot(id); }
        private void Update() { Core?.Update(); }
        private void OnDisable() { Core?.Close(); }
        private void OnDestroy() { Core?.Dispose(); _pool?.Dispose(); }
    }
}
