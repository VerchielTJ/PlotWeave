using System;
using UnityEngine;

namespace PlotWeave.Playback
{
    public sealed class PlotResourceOperation<T> : IPlotOperation where T : UnityEngine.Object
    {
        private IDisposable _request;
        private IPlotOperation _inner;
        private bool _loaded, _disposed;
        private string _error;
        public bool IsCompleted { get { if (_error != null) throw new InvalidOperationException(_error); return _disposed || (_loaded && (_inner == null || _inner.IsCompleted)); } }
        public PlotResourceOperation(Func<Action<T, string>, IDisposable> load, Func<T, IPlotOperation> start)
        {
            var request = load((asset, error) =>
            {
                if (_disposed || _loaded) return; _loaded = true;
                if (asset == null || !string.IsNullOrEmpty(error)) { _error = string.IsNullOrEmpty(error) ? "资源为空" : error; return; }
                try { _inner = start(asset); } catch (Exception exception) { _error = exception.Message; }
            });
            if (_disposed) request?.Dispose(); else _request = request;
        }
        public void Tick(float deltaTime) { if (_error != null) throw new InvalidOperationException(_error); if (!_disposed && _loaded) _inner?.Tick(deltaTime); }
        public void Dispose() { if (_disposed) return; _disposed = true; _request?.Dispose(); _inner?.Dispose(); }
    }
    public interface IPlotAudioResourceLoader { IDisposable LoadAudio(string key, Action<AudioClip, string> completed); }
    public interface IPlotPrefabResourceLoader { IDisposable LoadPrefab(string key, Action<GameObject, string> completed); }
    [Serializable] public sealed class PlotPlaybackAudioBinding { public string key = ""; public AudioClip clip; }
    [Serializable] public sealed class PlotPlaybackEffectBinding { public string key = ""; public GameObject prefab; }
    [Serializable] public sealed class PlotPlaybackEmotionBinding { public string actorKey = ""; public string emote = ""; public Sprite sprite; }
}
