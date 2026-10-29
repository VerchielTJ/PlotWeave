using System;

namespace PlotWeave.Playback
{
    public sealed class PlotTweenOperation : IPlotOperation
    {
        private readonly float _duration;
        private readonly Action<float> _apply;
        private readonly Action<bool> _cleanup;
        private double _elapsed;
        private bool _disposed, _finished;
        public bool IsCompleted => _finished || _disposed;
        public PlotTweenOperation(float duration, Action<float> apply = null, Action<bool> cleanup = null)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            _duration = duration; _apply = apply; _cleanup = cleanup;
            _finished = duration == 0; _apply?.Invoke(_finished ? 1 : 0);
        }
        public void Tick(float deltaTime)
        {
            if (IsCompleted) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            _elapsed += deltaTime; var t = (float)Math.Min(1, _elapsed / _duration); _apply?.Invoke(t); if (t >= 1) _finished = true;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _cleanup?.Invoke(_finished); }
    }
}
