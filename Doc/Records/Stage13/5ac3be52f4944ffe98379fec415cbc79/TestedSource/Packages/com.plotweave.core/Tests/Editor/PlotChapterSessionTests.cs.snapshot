using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlotWeave.Chapters;
using PlotWeave.Playback;

namespace PlotWeave.Tests
{
    public sealed class PlotChapterSessionTests
    {
        private sealed class Player : IPlotChapterPlayer
        { public event Action Completed; public event Action Closed; public event Action<string> Failed; public int Plays, Closes; public void Play(PlotChapterContent content) { Plays++; } public void Close() { Closes++; Closed?.Invoke(); } public void Finish() => Completed?.Invoke(); public void Fault() => Failed?.Invoke("playback failed"); }
        private sealed class Backend : IPlotChapterBackend
        {
            public List<PlotChapterRequest> Loads = new List<PlotChapterRequest>(); public List<PlotChapterCompletion> Reports = new List<PlotChapterCompletion>(); public TaskCompletionSource<PlotChapterContent> DeferredLoad; public TaskCompletionSource<PlotChapterAcknowledgement> DeferredReport; public bool FailReport, Replay;
            public Task<PlotChapterContent> LoadAsync(PlotChapterRequest request, CancellationToken token) { Loads.Add(request); return DeferredLoad?.Task ?? Task.FromResult(new PlotChapterContent(Content().Document, 1, !Replay)); }
            public Task<PlotChapterAcknowledgement> CompleteAsync(PlotChapterCompletion completion, CancellationToken token) { Reports.Add(completion); return FailReport ? Task.FromException<PlotChapterAcknowledgement>(new Exception("offline")) : DeferredReport?.Task ?? Task.FromResult(new PlotChapterAcknowledgement(true)); }
        }
        private sealed class Navigation : IPlotChapterNavigation
        { public int Entered, Returned, Failures; public bool New, Retry; public PlotChapterExit Exit; public void Enter(PlotChapterRequest request) => Entered++; public void Return(PlotChapterRequest request, PlotChapterExit reason, bool newlyCompleted) { Returned++; New = newlyCompleted; Exit = reason; } public void ShowFailure(string message, bool canRetryReport) { Failures++; Retry = canRetryReport; } }
        private static void Complete(Task task) { Assert.That(task.IsCompleted, Is.True, "受控后端完成后，任务应已在同一Unity线程内继续"); task.GetAwaiter().GetResult(); }
        private static PlotChapterContent Content() => new PlotChapterContent(new PlotPlaybackDocument { Nodes = new List<PlotPlaybackNode> { new PlotPlaybackNode { ScenesID = 1 } } }, 1);
        [TestCase(PlotChapterKind.Main)][TestCase(PlotChapterKind.Daily)] public void CompletionReportsTheCorrectChapterAndReturnsOnlyAfterAcknowledgement(PlotChapterKind kind)
        {
            var backend = new Backend { DeferredReport = new TaskCompletionSource<PlotChapterAcknowledgement>() }; var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav))
            { Complete(session.StartAsync(new PlotChapterRequest(kind, "chapter1"))); Assert.That(player.Plays, Is.EqualTo(1)); player.Finish(); player.Finish(); Assert.That(backend.Reports.Count, Is.EqualTo(1)); Assert.That(backend.Reports[0].Request.Kind, Is.EqualTo(kind)); Assert.That(nav.Returned, Is.Zero); backend.DeferredReport.SetResult(new PlotChapterAcknowledgement(true)); Complete(session.PendingReport); Assert.That(session.State, Is.EqualTo(PlotChapterState.Completed)); Assert.That(nav.Returned, Is.EqualTo(1)); Assert.That(nav.New, Is.True); }
        }
        [Test] public void FailedReportRetriesWithTheSameIdWithoutReplaying()
        {
            var backend = new Backend { FailReport = true }; var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav))
            { Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "chapter1"))); player.Finish(); Complete(session.PendingReport); Assert.That(session.State, Is.EqualTo(PlotChapterState.ReportFailed)); Assert.That(nav.Retry, Is.True); Assert.That(nav.Returned, Is.Zero); backend.FailReport = false; Complete(session.RetryReportAsync()); Assert.That(backend.Reports[0].CompletionID, Is.EqualTo(backend.Reports[1].CompletionID)); Assert.That(player.Plays, Is.EqualTo(1)); Assert.That(nav.Returned, Is.EqualTo(1)); }
        }
        [Test] public void CancelledLoadCannotOpenThePlayerWhenTheBackendIgnoresCancellation()
        {
            var backend = new Backend { DeferredLoad = new TaskCompletionSource<PlotChapterContent>() }; var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav))
            { var task = session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "chapter1")); session.Cancel(); backend.DeferredLoad.SetResult(Content()); Complete(task); Assert.That(player.Plays, Is.Zero); Assert.That(nav.Entered, Is.Zero); Assert.That(session.State, Is.EqualTo(PlotChapterState.Cancelled)); }
        }
        [Test] public void ClosingPlaybackCancelsWithoutReportingACompletion()
        {
            var backend = new Backend(); var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav))
            { Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Daily, "daily1"))); player.Close(); Assert.That(session.State, Is.EqualTo(PlotChapterState.Cancelled)); Assert.That(backend.Reports, Is.Empty); Assert.That(nav.Exit, Is.EqualTo(PlotChapterExit.Cancelled)); Assert.That(nav.Returned, Is.EqualTo(1)); }
        }
        [Test] public void LateReportCannotReturnFromANewerChapter()
        {
            var backend = new Backend { DeferredReport = new TaskCompletionSource<PlotChapterAcknowledgement>() }; var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav))
            { Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "first"))); player.Finish(); var oldReport = session.PendingReport; Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Daily, "second"))); backend.DeferredReport.SetResult(new PlotChapterAcknowledgement(true)); Complete(oldReport); Assert.That(session.State, Is.EqualTo(PlotChapterState.Playing)); Assert.That(nav.Returned, Is.EqualTo(1)); Assert.That(nav.New, Is.False); }
        }
        [Test] public void PlaybackFailureReturnsOnceAndNeverReportsSuccess()
        { var backend = new Backend(); var player = new Player(); var nav = new Navigation(); using (var session = new PlotChapterSession(backend, player, nav)) { Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "first"))); player.Fault(); player.Fault(); Assert.That(session.State, Is.EqualTo(PlotChapterState.Failed)); Assert.That(nav.Returned, Is.EqualTo(1)); Assert.That(nav.Exit, Is.EqualTo(PlotChapterExit.Failed)); Assert.That(backend.Reports, Is.Empty); } }
        [Test] public void DisposingDetachesCallbacksAndRejectsNewRequests()
        { var backend = new Backend(); var player = new Player(); var session = new PlotChapterSession(backend, player, new Navigation()); Complete(session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "first"))); session.Dispose(); player.Finish(); Assert.That(backend.Reports, Is.Empty); Assert.That(session.State, Is.EqualTo(PlotChapterState.Disposed)); Assert.Throws<ObjectDisposedException>(() => session.StartAsync(new PlotChapterRequest(PlotChapterKind.Main, "second")).GetAwaiter().GetResult()); }
        [Test] public void ReplayingAnAcknowledgedChapterReturnsWithoutReportingOrClaimingItAsNew()
        {
            var request = new PlotChapterRequest(PlotChapterKind.Main, "replay"); var backend = new Backend(); var player = new Player(); var nav = new Navigation();
            using (var session = new PlotChapterSession(backend, player, nav))
            { Complete(session.StartAsync(request)); player.Finish(); Complete(session.PendingReport); Assert.That(nav.New, Is.True); backend.Replay = true; Complete(session.StartAsync(request)); player.Finish(); Assert.That(session.State, Is.EqualTo(PlotChapterState.Completed)); Assert.That(nav.New, Is.False); Assert.That(nav.Returned, Is.EqualTo(2)); Assert.That(player.Plays, Is.EqualTo(2)); Assert.That(backend.Reports.Count, Is.EqualTo(1)); }
        }
    }
}
