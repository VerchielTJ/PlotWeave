using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using PlotWeave.Chapters;
using PlotWeave.Playback;

namespace PlotWeave.Tests
{
    public sealed class PlotLocalChapterBackendTests
    {
        private static PlotChapterContent Content(PlotChapterRequest request) => new PlotChapterContent(new PlotPlaybackDocument { Nodes = new List<PlotPlaybackNode> { new PlotPlaybackNode { ScenesID = 1 } } }, 1);
        [Test] public void DurableReceiptsAreIdempotentAcrossBackendInstancesAndChapterKinds()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PlotWeaveChapter_" + Guid.NewGuid().ToString("N")); var path = Path.Combine(folder, "progress.json");
            try
            {
                var main = new PlotChapterRequest(PlotChapterKind.Main, "same-id"); var daily = new PlotChapterRequest(PlotChapterKind.Daily, "same-id"); var completion = new PlotChapterCompletion(main, "receipt1"); var first = new PlotLocalChapterBackend(Content, path); Assert.That(first.CompleteAsync(completion, CancellationToken.None).Result.NewlyCompleted, Is.True);
                var second = new PlotLocalChapterBackend(Content, path); Assert.That(second.IsCompleted(main), Is.True); Assert.That(second.LoadAsync(main, CancellationToken.None).Result.RequiresCompletionReport, Is.False); Assert.That(second.IsCompleted(daily), Is.False); Assert.That(second.CompleteAsync(completion, CancellationToken.None).Result.NewlyCompleted, Is.True); Assert.That(second.CompleteAsync(new PlotChapterCompletion(main, "receipt2"), CancellationToken.None).Result.NewlyCompleted, Is.False); Assert.That(second.CompleteAsync(new PlotChapterCompletion(daily, "receipt3"), CancellationToken.None).Result.NewlyCompleted, Is.True);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [Test] public void AReceiptCannotBeReusedForAnotherChapter()
        { var backend = new PlotLocalChapterBackend(Content); backend.CompleteAsync(new PlotChapterCompletion(new PlotChapterRequest(PlotChapterKind.Main, "one"), "same"), CancellationToken.None); Assert.Throws<InvalidOperationException>(() => backend.CompleteAsync(new PlotChapterCompletion(new PlotChapterRequest(PlotChapterKind.Main, "two"), "same"), CancellationToken.None)); }
        [Test] public void CancelledCompletionDoesNotChangeProgress()
        { var backend = new PlotLocalChapterBackend(Content); var request = new PlotChapterRequest(PlotChapterKind.Main, "one"); using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); Assert.Throws<OperationCanceledException>(() => backend.CompleteAsync(new PlotChapterCompletion(request, "receipt"), cancellation.Token)); Assert.That(backend.IsCompleted(request), Is.False); } }
    }
}
