using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Legacy;
using PlotWeave.Playback;

namespace PlotWeave.Tests
{
    public sealed class PlotEventSchedulerTests
    {
        private static PlotEventJsonData Event(string name, int id = -1, string callbacks = "") => new PlotEventJsonData(name, id) { callbackList = callbacks };
        [Test] public void RootsAndCallbacksExecuteInDeclaredDepthFirstOrder()
        {
            var trace = new List<string>();
            using (var scheduler = new PlotEventScheduler(new[] { Event("root", -1, "1,2"), Event("last"), Event("one", 1, "3"), Event("two", 2), Event("nested", 3) }, data => { trace.Add(data.eventName); return null; }))
            { scheduler.Tick(0); Assert.That(scheduler.IsCompleted, Is.True); CollectionAssert.AreEqual(new[] { "root", "one", "nested", "two", "last" }, trace); }
        }
        [Test] public void WaitDefersCallbacksUntilTheOperationCompletes()
        {
            var trace = new List<string>();
            using (var scheduler = new PlotEventScheduler(new[] { Event("wait", -1, "1"), Event("done", 1) }, data => { trace.Add(data.eventName); return data.eventName == "wait" ? new PlotTweenOperation(.5f) : null; }))
            { scheduler.Tick(0); scheduler.Tick(.2f); Assert.That(trace.Count, Is.EqualTo(1)); scheduler.Tick(.4f); Assert.That(trace[1], Is.EqualTo("done")); Assert.That(scheduler.IsCompleted, Is.True); }
        }
        [Test] public void CancellationDisposesActiveWorkWithoutRunningCallbacks()
        {
            var cleaned = false; var trace = new List<string>(); var scheduler = new PlotEventScheduler(new[] { Event("wait", -1, "1"), Event("callback", 1) }, data => { trace.Add(data.eventName); return new PlotTweenOperation(10, null, complete => cleaned = !complete); });
            scheduler.Tick(0); scheduler.Dispose(); scheduler.Tick(100); Assert.That(cleaned, Is.True); Assert.That(trace.Count, Is.EqualTo(1)); Assert.That(scheduler.IsCancelled, Is.True);
        }
        [TestCase("missing")][TestCase("duplicate")][TestCase("cycle")]
        public void InvalidCallbackGraphsAreRejectedBeforeExecution(string mode)
        {
            var data = mode == "missing" ? new[] { Event("root", -1, "7") } : mode == "duplicate" ? new[] { Event("one", 1), Event("two", 1) } : new[] { Event("root", -1, "1"), Event("one", 1, "2"), Event("two", 2, "1") };
            var calls = 0; Assert.Throws<FormatException>(() => new PlotEventScheduler(data, item => { calls++; return null; })); Assert.That(calls, Is.Zero);
        }
        [Test] public void ReentrantCancellationDisposesTheJustCreatedOperation()
        {
            PlotEventScheduler scheduler = null; var cleaned = false;
            scheduler = new PlotEventScheduler(new[] { Event("reenter") }, data => { scheduler.Dispose(); return new PlotTweenOperation(1, null, complete => cleaned = true); });
            scheduler.Tick(0); Assert.That(cleaned, Is.True); Assert.That(scheduler.IsCancelled, Is.True);
        }
        [Test] public void SynchronousWorkHasAPerFrameBudget()
        {
            var calls = 0; using (var scheduler = new PlotEventScheduler(Enumerable.Range(0, 70).Select(i => Event("signal")), data => { calls++; return null; }))
            { scheduler.Tick(0); Assert.That(calls, Is.EqualTo(64)); Assert.That(scheduler.IsCompleted, Is.False); scheduler.Tick(0); Assert.That(calls, Is.EqualTo(70)); }
        }
        [Test] public void ExecutionFailurePreventsLaterEvents()
        {
            var calls = 0; using (var scheduler = new PlotEventScheduler(new[] { Event("bad"), Event("later") }, data => { calls++; throw new InvalidOperationException("failure"); }))
            { scheduler.Tick(0); Assert.That(scheduler.IsFaulted, Is.True); Assert.That(scheduler.Error, Is.EqualTo("failure")); Assert.That(calls, Is.EqualTo(1)); }
        }
        [Test] public void SchedulerOwnsItsDefinitionSnapshot()
        {
            var data = Event("before"); string observed = null;
            using (var scheduler = new PlotEventScheduler(new[] { data }, item => { observed = item.eventName; return null; })) { data.eventName = "changed"; scheduler.Tick(0); Assert.That(observed, Is.EqualTo("before")); }
        }
    }
}
