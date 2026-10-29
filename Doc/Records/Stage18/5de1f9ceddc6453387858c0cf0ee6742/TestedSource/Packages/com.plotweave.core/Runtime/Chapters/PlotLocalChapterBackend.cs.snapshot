using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PlotWeave.Chapters
{
    /// <summary>离线示例后端。正式游戏实现IPlotChapterBackend接入自己的网络协议。</summary>
    public sealed class PlotLocalChapterBackend : IPlotChapterBackend
    {
        [Serializable] private sealed class Saved { public List<string> chapters = new List<string>(); public List<Receipt> receipts = new List<Receipt>(); }
        [Serializable] private sealed class Receipt { public string id, chapter; public bool newlyCompleted; }
        private readonly Func<PlotChapterRequest, PlotChapterContent> _load; private readonly string _path; private Saved _state;
        public PlotLocalChapterBackend(Func<PlotChapterRequest, PlotChapterContent> load, string savePath = null)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load)); _path = string.IsNullOrEmpty(savePath) ? null : Path.GetFullPath(savePath);
            _state = _path != null && File.Exists(_path) ? JsonUtility.FromJson<Saved>(File.ReadAllText(_path)) : new Saved();
            if (_state == null || _state.chapters == null || _state.receipts == null) throw new FormatException("章节进度存档无效");
        }
        private static string Key(PlotChapterRequest request) => request.Kind + ":" + request.ChapterID;
        public bool IsCompleted(PlotChapterRequest request) => _state.chapters.Contains(Key(request));
        public Task<PlotChapterContent> LoadAsync(PlotChapterRequest request, CancellationToken cancellation)
        { cancellation.ThrowIfCancellationRequested(); var content = _load(request); return Task.FromResult(new PlotChapterContent(content.Document, content.EntryNode, content.RequiresCompletionReport && !IsCompleted(request),content.TopTitle,content.BottomTitle,content.SkipMessage)); }
        public Task<PlotChapterAcknowledgement> CompleteAsync(PlotChapterCompletion completion, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested(); var key = Key(completion.Request); var previous = _state.receipts.Find(r => r.id == completion.CompletionID);
            if (previous != null) { if (previous.chapter != key) throw new InvalidOperationException("结算ID被另一章节重复使用"); return Task.FromResult(new PlotChapterAcknowledgement(previous.newlyCompleted)); }
            var candidate = JsonUtility.FromJson<Saved>(JsonUtility.ToJson(_state)); var newlyCompleted = !candidate.chapters.Contains(key); if (newlyCompleted) candidate.chapters.Add(key); candidate.receipts.Add(new Receipt { id = completion.CompletionID, chapter = key, newlyCompleted = newlyCompleted });
            if (_path != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)); var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllText(temp, JsonUtility.ToJson(candidate, true)); cancellation.ThrowIfCancellationRequested(); if (File.Exists(_path)) File.Replace(temp, _path, null); else File.Move(temp, _path); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            _state = candidate; return Task.FromResult(new PlotChapterAcknowledgement(newlyCompleted));
        }
    }
}
