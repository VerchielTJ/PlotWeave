using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Tests
{
    public sealed class PlotAdvancedPlaybackTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private Texture2D _texture;
        private Sprite _blue, _orange;
        private AudioClip _clip;
        private PlotPlaybackDocument _data;
        [SetUp] public void SetUp()
        {
            _texture = new Texture2D(16, 16); _blue = Sprite.Create(_texture, new Rect(0, 0, 8, 8), Vector2.one * .5f); _orange = Sprite.Create(_texture, new Rect(8, 8, 8, 8), Vector2.one * .5f); _clip = AudioClip.Create("test", 800, 1, 8000, false);
            _data = new PlotPlaybackDocument();
            _data.Nodes.Add(new PlotPlaybackNode { ScenesID = 1, Dialog = "<b>line</b>", Next = 2, RoleAction = new List<PlotRoleAction> { new PlotRoleAction { RoleID = 101, RolePos = 0 } }, PlotSetBack = new List<PlotSetBackground> { new PlotSetBackground { BackName = "bg0" } }, Branch = new List<PlotBranch> { new PlotBranch { Key = "first", Target = 2 }, new PlotBranch { Key = "end", Target = 0 } } });
            _data.Nodes.Add(new PlotPlaybackNode { ScenesID = 2, Dialog = "done" }); _data.Actors.Add(new PlotPlaybackActor { ID = 101, Name = "actor", Thespian = "actor" });
        }
        [TearDown] public void TearDown()
        { foreach (var value in _objects) if (value != null) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); UnityEngine.Object.DestroyImmediate(_blue); UnityEngine.Object.DestroyImmediate(_orange); UnityEngine.Object.DestroyImmediate(_texture); UnityEngine.Object.DestroyImmediate(_clip); }
        private PlotPlayerHost Host(bool play = true, Action<PlotPlayerHost> configure = null)
        {
            var go = new GameObject("AdvancedPlayer"); _objects.Add(go); var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false; host.config.TextSpeed = 0; host.config.AutoDelay = .1f; host.config.CharacterAnimationDuration = .5f; host.config.BackgroundTransitionTime = .5f;
            foreach (var key in new[] { "actor", "bg0" }) host.sprites.Add(new PlotPlaybackSpriteBinding { key = key, sprite = _blue }); host.sprites.Add(new PlotPlaybackSpriteBinding { key = "bg1", sprite = _orange }); host.audioBindings.Add(new PlotPlaybackAudioBinding { key = "sound", clip = _clip }); configure?.Invoke(host);
            host.Initialize(_data, PlotPlayerRuntimeMode.Editor); if (play) host.Play(1); return host;
        }
        private static PlotEventJsonData Event(string name, int callbackID = -1, string callbacks = "", params string[] parameters) => new PlotEventJsonData(name, callbackID) { callbackList = callbacks, parameters = new List<string>(parameters) };
        [Test] public void ChoiceButtonLoadsItsTargetAndStaleClicksCannotAdvanceAgain()
        {
            var host = Host(); host.Core.OnPlayerInput(); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.WaitingForChoice)); Assert.That(host.Core.Choices.Count, Is.EqualTo(2));
            var button = host.View.GetComponentsInChildren<Button>(true).First(b => b.name == "Choice_0"); var old = button.onClick; old.Invoke(); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(2)); old.Invoke(); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(2));
        }
        [Test] public void AutomaticPlaybackWaitsForChoiceAndTerminalChoiceCompletesOnce()
        {
            var host = Host(); var ended = 0; host.Completed += () => ended++; host.Core.SetAutoPlay(true); host.Core.Tick(.2f); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.WaitingForChoice)); host.Core.Tick(10); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(1)); host.Core.Choose(1); Assert.That(ended, Is.EqualTo(1)); host.Core.OnPlayerInput(); Assert.That(ended, Is.EqualTo(1));
        }
        [Test] public void PausedChoicesAreDisabledAndCloseInvalidatesTheirCallbacks()
        {
            var host = Host(); host.Core.OnPlayerInput(); var button = host.View.GetComponentsInChildren<Button>(true).First(b => b.name == "Choice_0"); var old = button.onClick; host.Core.Pause(); Assert.That(button.interactable, Is.False); host.Core.Choose(0); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(1)); host.Core.Resume(); Assert.That(button.interactable, Is.True); host.Core.Close(); old.Invoke(); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.Closed));
        }
        [Test] public void WaitAndCallbacksGateInputAndCarryVariablesToTheNextNode()
        {
            _data.Nodes[0].Branch.Clear(); _data.Nodes[0].Events.Add(Event("wait", -1, "1", ".2")); _data.Nodes[0].Events.Add(Event("set", 1, "2", "route", "forest")); _data.Nodes[0].Events.Add(Event("signal", 2, "", "done", "${route}")); _data.Nodes[1].Dialog = "route=${route}";
            var host = Host(); var signals = new List<string>(); host.Core.SignalReceived += (name, value) => signals.Add(name + ":" + value); host.Core.Tick(0); host.Core.OnPlayerInput(); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(1)); host.Core.Pause(); host.Core.Tick(10); Assert.That(signals, Is.Empty); host.Core.Resume(); host.Core.Tick(.3f); Assert.That(signals.Single(), Is.EqualTo("done:forest")); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Is.EqualTo("route=forest"));
        }
        [Test] public void MovementAndEmotionSpritesActuallyChangeTheActorAndPauseCorrectly()
        {
            _data.Nodes[0].RoleAction[0].ActionType = "Move"; _data.Nodes[0].RoleAction[0].RolePos = 2; _data.Nodes[0].RoleAction[0].Emote = "Happy";
            var host = Host(configure: h => h.emotions.Add(new PlotPlaybackEmotionBinding { actorKey = "actor", emote = "Happy", sprite = _orange })); var actor = (RectTransform)host.View.ActorObjects.Single().transform;
            host.Core.Tick(.25f); var halfway = actor.anchoredPosition; Assert.That(halfway.x, Is.GreaterThan(-200).And.LessThan(0)); Assert.That(actor.GetComponentInChildren<Image>().sprite, Is.SameAs(_orange)); host.Core.Pause(); host.Core.Tick(5); Assert.That(actor.anchoredPosition, Is.EqualTo(halfway)); host.Core.Resume(); host.Core.Tick(.3f); Assert.That(actor.anchoredPosition, Is.EqualTo(host.config.Positions[2]));
        }
        [Test] public void BackgroundCrossFadeProgressesAndCommitsOnlyOnCompletion()
        {
            _data.Nodes[1].PlotSetBack.Add(new PlotSetBackground { BackName = "bg1", SetType = "CrossFade" }); var host = Host(); host.Core.LoadPlot(2); host.Core.Tick(.25f);
            Assert.That(host.View.BackgroundKey, Is.EqualTo("bg0")); var overlay = host.View.GetComponentsInChildren<Image>().Single(i => i.name == "BackgroundTransition"); Assert.That(overlay.color.a, Is.EqualTo(.5f).Within(.01)); host.Core.Pause(); host.Core.Tick(5); Assert.That(overlay.color.a, Is.EqualTo(.5f).Within(.01)); host.Core.Resume(); host.Core.Tick(.3f); Assert.That(host.View.BackgroundKey, Is.EqualTo("bg1"));
        }
        [Test] public void SoundOperationWaitsAndDisposesItsSourceBeforeCallback()
        {
            _data.Nodes[0].Events.Add(Event("sound", -1, "1", "sound")); _data.Nodes[0].Events.Add(Event("signal", 1, "", "audio.done")); var host = Host(); var signals = 0; host.Core.SignalReceived += (name, value) => signals++;
            host.Core.Tick(0); Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(1)); host.Core.Pause(); host.Core.Tick(10); Assert.That(signals, Is.Zero); host.Core.Resume(); host.Core.Tick(.2f); Assert.That(signals, Is.EqualTo(1)); Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.Zero);
        }
        [Test] public void EffectInstancesAreRemovedWhenPlaybackIsClosed()
        {
            var prefab = new GameObject("EffectTemplate", typeof(RectTransform), typeof(Image)); prefab.SetActive(false); _objects.Add(prefab); _data.Nodes[0].Events.Add(Event("effect", -1, "", "flash", "10")); var host = Host(configure: h => h.effects.Add(new PlotPlaybackEffectBinding { key = "flash", prefab = prefab })); host.Core.Tick(0);
            Assert.That(host.View.GetComponentsInChildren<Image>(true).Any(i => i.name == "EffectTemplate(Clone)"), Is.True); host.Core.Close(); Assert.That(host.View.GetComponentsInChildren<Image>(true).Any(i => i.name == "EffectTemplate(Clone)"), Is.False);
        }
        [Test] public void JumpFromEventHandlerCancelsTheOldSchedulerWithoutContaminatingTheNewNode()
        {
            _data.Nodes[0].Events.Add(Event("reenter")); var host = Host(false); var cleaned = false; host.Core.EventRegistry.Register("reenter", (data, context) => { context.Player.JumpToNode(2); return new PlotTweenOperation(1, null, complete => cleaned = true); }); host.Play(1); host.Core.Tick(0); Assert.That(host.Core.GetCurrentNodeID(), Is.EqualTo(2)); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.Playing)); Assert.That(cleaned, Is.True);
        }
        [Test] public void UnknownEventFailsBeforeAnyActorIsPresented()
        {
            _data.Nodes[0].Events.Add(Event("unregistered")); var host = Host(false); var errors = new List<string>(); host.Core.OnError += errors.Add; host.Play(1); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.Faulted)); Assert.That(host.View.ActorCount, Is.Zero); Assert.That(errors.Count, Is.EqualTo(1));
        }
        [TestCase("Smooth", .5f)][TestCase("Accelerating", .25f)][TestCase("Decelerating", .75f)][TestCase("Capering", .5f)]
        public void EventMovementModesUseTheirDeclaredCurves(string mode, float progress)
        {
            _data.Nodes[0].Events.Add(Event("actor.move", -1, "", "101", "2", "1", mode)); var host = Host(); var actor = (RectTransform)host.View.ActorObjects.Single().transform; host.Core.Tick(0); var origin = actor.anchoredPosition; host.Core.Tick(.5f); Assert.That(actor.anchoredPosition.x, Is.EqualTo(Mathf.Lerp(origin.x, host.config.Positions[2].x, progress)).Within(.01)); host.Core.Tick(.6f); Assert.That(actor.anchoredPosition, Is.EqualTo(host.config.Positions[2]));
        }
        [Test] public void LegacyMoveRoleUsesTheActualThreeParameterMapping()
        { _data.Nodes[0].Events.Add(Event("MoveRole", -1, "", "101", "Capering", "2")); var host = Host(); host.Core.Tick(0); host.Core.Tick(1); Assert.That(host.Core.State, Is.EqualTo(PlotPlayerState.Playing)); Assert.That(((RectTransform)host.View.ActorObjects.Single().transform).anchoredPosition, Is.EqualTo(host.config.Positions[2])); }
    }
}
