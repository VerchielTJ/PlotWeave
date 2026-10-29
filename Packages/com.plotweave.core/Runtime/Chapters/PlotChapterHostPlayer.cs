using System;
using PlotWeave.Playback;

namespace PlotWeave.Chapters
{
    public sealed class PlotChapterHostPlayer : IPlotChapterPlayer, IDisposable
    {
        private readonly PlotPlayerHost _host; private PlotPlayerCore _bound;
        public event Action Completed; public event Action Closed; public event Action<string> Failed;
        public PlotChapterHostPlayer(PlotPlayerHost host) { _host = host != null ? host : throw new ArgumentNullException(nameof(host)); }
        private void Unbind() { if (_bound == null) return; _bound.OnPlotCompleted -= Complete; _bound.OnClosed -= CloseEvent; _bound.OnError -= Error; _bound = null; }
        private void Complete() => Completed?.Invoke(); private void CloseEvent() => Closed?.Invoke(); private void Error(string message) => Failed?.Invoke(message);
        public void Play(PlotChapterContent content)
        {
            Unbind(); _host.Initialize(content.Document, PlotPlayerRuntimeMode.GameRuntime); _bound = _host.Core; _bound.OnPlotCompleted += Complete; _bound.OnClosed += CloseEvent; _bound.OnError += Error;
            var skip=_host.View.GetComponent<PlotWeave.Legacy.PlotLegacySkipController>();if(skip!=null){var text=content.Document.Constants.Find(t=>t.Key==content.SkipMessage);skip.skipMessage=string.IsNullOrEmpty(content.SkipMessage)?"跳过本节剧情？":text!=null&&text.Languages.Count>0?text.Languages[0]:content.SkipMessage;}
            _bound.Open();if(content.TopTitle!=""||content.BottomTitle!="")_bound.ShowCurtainAndLoadNode(content.TopTitle,content.BottomTitle,content.EntryNode);else _bound.LoadPlot(content.EntryNode);
        }
        public void Close() { if (_host != null) _host.Core?.Close(); }
        public void Dispose() { Unbind(); Close(); }
    }
}
