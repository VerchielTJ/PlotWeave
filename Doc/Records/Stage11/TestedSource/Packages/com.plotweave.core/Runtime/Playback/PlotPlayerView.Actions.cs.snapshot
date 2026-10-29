using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PlotWeave.Legacy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Playback
{
    public sealed partial class PlotPlayerView : IPlotEventPresentation, IPlotPausableView
    {
        private sealed class Running
        { public Ticket Ticket; public IPlotOperation Operation; public Action<bool, string> Done; }
        private readonly List<Running> _running = new List<Running>();
        private readonly List<AudioSource> _audioSources = new List<AudioSource>();
        private readonly List<GameObject> _effects = new List<GameObject>();
        private readonly Dictionary<string, Sprite> _emotions = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private AudioSource _bgm;
        private bool _presentationPaused;
        private readonly Dictionary<Animator, float> _pausedAnimators = new Dictionary<Animator, float>();
        private readonly HashSet<ParticleSystem> _pausedParticles = new HashSet<ParticleSystem>();
        public void SetPaused(bool paused)
        {
            _presentationPaused = paused;
            if (_choicePanel != null) foreach (var button in _choicePanel.GetComponentsInChildren<Button>(true)) button.interactable = !paused;
            if (!Application.isPlaying) return;
            foreach (var source in _audioSources) if (source != null) { if (paused) source.Pause(); else source.UnPause(); }
            if (paused)
            {
                foreach (var actor in _actors.Values) foreach (var animator in actor.Object.GetComponentsInChildren<Animator>()) PauseAnimator(animator);
                foreach (var effect in _effects) if (effect != null) foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>()) if (particle.isPlaying) { _pausedParticles.Add(particle); particle.Pause(); }
            }
            else
            { foreach (var item in _pausedAnimators) if (item.Key != null) item.Key.speed = item.Value; _pausedAnimators.Clear(); foreach (var particle in _pausedParticles) if (particle != null) particle.Play(); _pausedParticles.Clear(); }
        }
        private void PauseAnimator(Animator animator)
        { if (!_pausedAnimators.ContainsKey(animator)) _pausedAnimators.Add(animator, animator.speed); animator.speed = 0; }
        public void ConfigureEmotions(IEnumerable<PlotPlaybackEmotionBinding> bindings)
        {
            var validated = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (bindings != null) foreach (var item in bindings)
            { if (item == null || string.IsNullOrEmpty(item.actorKey) || string.IsNullOrEmpty(item.emote) || item.sprite == null || validated.ContainsKey(item.actorKey + "\n" + item.emote)) throw new ArgumentException("表情绑定缺少资源或Key重复"); validated.Add(item.actorKey + "\n" + item.emote, item.sprite); }
            _emotions.Clear(); foreach (var item in validated) _emotions.Add(item.Key, item.Value);
        }
        private void Track(Ticket ticket, IPlotOperation operation, Action<bool, string> done)
        {
            if (ticket.Cancelled) { operation?.Dispose(); return; }
            if (operation == null) { done(true, ""); return; }
            ticket.Requests.Add(operation);
            try { if (operation.IsCompleted) { operation.Dispose(); done(true, ""); } else _running.Add(new Running { Ticket = ticket, Operation = operation, Done = done }); }
            catch (Exception error) { operation.Dispose(); done(false, error.Message); }
        }
        private void TickOperations(float deltaTime)
        {
            foreach (var item in _running.ToArray())
            {
                if (!_running.Contains(item)) continue;
                try
                {
                    if (item.Ticket.Cancelled) { item.Operation.Dispose(); _running.Remove(item); continue; }
                    item.Operation.Tick(deltaTime); if (!item.Operation.IsCompleted) continue;
                    _running.Remove(item); item.Operation.Dispose(); item.Done(true, "");
                }
                catch (Exception error) { _running.Remove(item); item.Operation.Dispose(); item.Done(false, error.Message); }
            }
        }
        private static void RemoveObject(GameObject value)
        { if (value == null) return; value.SetActive(false); if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void ClearOperations()
        {
            SetPaused(false);
            foreach (var item in _running.ToArray()) item.Operation.Dispose(); _running.Clear();
            foreach (var audio in _audioSources) if (audio != null) { if (Application.isPlaying) audio.Stop(); RemoveObject(audio.gameObject); } _audioSources.Clear(); _bgm = null;
            foreach (var effect in _effects) RemoveObject(effect); _effects.Clear();
        }
        private void ReturnActor(GameObject value)
        { if (value == null) return; foreach (var animator in value.GetComponentsInChildren<Animator>()) if (_pausedAnimators.TryGetValue(animator, out var speed)) { animator.speed = speed; _pausedAnimators.Remove(animator); } var state = value.GetComponent<PlotActorVisualState>(); if (state != null) state.ResetVisual(); _pool?.Return(value); }
        private void ApplyEmotion(ActorSlot slot, string key)
        {
            var bubble = slot.Object.transform.Find("PlotEmote"); if (bubble != null) bubble.gameObject.SetActive(false);
            var image = ActorImage(slot.Object); image.sprite = slot.BaseSprite;
            if (string.IsNullOrEmpty(key) || key == "默认" || key == "Default" || key == "Neutral" || key == "None") return;
            if (_emotions.TryGetValue(slot.Key + "\n" + key, out var expression)) { image.sprite = expression; return; }
            TMP_Text label;
            if (bubble == null)
            { label = RichLabel("PlotEmote", slot.Object.transform, 30); var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, 35); rect.sizeDelta = new Vector2(250, 50); }
            else { bubble.gameObject.SetActive(true); label = bubble.GetComponent<TMP_Text>(); }
            label.text = key == "Happy" ? "开心" : key == "Sad" ? "难过" : key == "Angry" ? "生气" : key == "Surprised" ? "惊讶" : key;
        }
        private IPlotOperation ActorAction(PlotRoleAction action, ActorSlot slot, float? durationOverride = null)
        {
            var rect = (RectTransform)slot.Object.transform; var state = slot.Object.GetComponent<PlotActorVisualState>() ?? slot.Object.AddComponent<PlotActorVisualState>(); state.Capture();
            var origin = rect.anchoredPosition; var target = _config.Positions[action.RolePos]; var version = slot.Version;
            var duration = durationOverride ?? (_config.CharacterAnimationSpeed == 0 ? 0 : _config.CharacterAnimationDuration / _config.CharacterAnimationSpeed);
            var key = action.ActionType ?? ""; key = key == "默认" ? "Default" : key == "抖动" ? "Shake" : key; ApplyEmotion(slot, action.Emote);
            bool Current() => slot.Object != null && slot.Version == version;
            if (key == "" || key == "Default" || key == "None") { rect.anchoredPosition = target; return null; }
            if (key == "FaceLeft" || key == "FaceRight") { var scale = state.OriginalScale; scale.x = Mathf.Abs(scale.x) * (key == "FaceLeft" ? -1 : 1); rect.localScale = scale; rect.anchoredPosition = target; return null; }
            if (key != "Move" && key != "Shake" && key != "Jump" && key != "FadeIn" && key != "FadeOut" && key != "Pop" && key != "Scale")
            {
                var animator = slot.Object.GetComponentInChildren<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, Animator.StringToHash(key))) throw new NotSupportedException("未注册角色动作或Animator状态: " + key);
                animator.Play(key, 0, 0); if (_presentationPaused) PauseAnimator(animator); return new PlotTweenOperation(duration);
            }
            rect.anchoredPosition = key == "Move" ? origin : target;
            return new PlotTweenOperation(duration, t =>
            {
                if (!Current()) return; var eased = t * t * (3 - 2 * t);
                if (key == "Move") rect.anchoredPosition = Vector2.LerpUnclamped(origin, target, eased);
                else if (key == "Shake") rect.anchoredPosition = target + new Vector2(Mathf.Sin(t * Mathf.PI * 8) * 18 * (1 - t), 0);
                else if (key == "Jump") rect.anchoredPosition = target + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 65);
                else if (key == "FadeIn") state.Group.alpha = t;
                else if (key == "FadeOut") state.Group.alpha = 1 - t;
                else rect.localScale = state.OriginalScale * Mathf.Lerp(.65f, 1, eased);
            }, finished =>
            {
                if (!Current()) return; rect.anchoredPosition = target;
                if (key == "Scale" || key == "Pop") rect.localScale = state.OriginalScale;
                if (!finished && (key == "FadeIn" || key == "FadeOut")) state.Group.alpha = 1;
            });
        }
        private IPlotOperation ChangeBackground(Sprite sprite, string key, string transition, float duration)
        {
            transition = transition ?? "Default";
            transition = transition == "默认" ? "Default" : transition == "淡入" ? "FadeIn" : transition;
            if (transition == "" || transition == "Default" || transition == "Cut" || duration == 0)
            { _background.sprite = sprite; _background.color = Color.white; BackgroundKey = key; return null; }
            if (transition != "FadeIn" && transition != "CrossFade" && transition != "FadeOut" && transition != "SlideLeft" && transition != "SlideRight") throw new NotSupportedException("未知背景过渡: " + transition);
            var rect = PlotPlayerVisual.Rect("BackgroundTransition", _background.transform.parent); PlotPlayerVisual.Fill(rect); rect.SetSiblingIndex(_background.transform.GetSiblingIndex() + 1);
            var overlay = rect.gameObject.AddComponent<Image>(); overlay.sprite = sprite; overlay.raycastTarget = false;
            if (transition == "FadeIn") _background.color = Color.black;
            return new PlotTweenOperation(duration, t =>
            {
                if (overlay == null) return;
                overlay.color = new Color(1, 1, 1, transition == "FadeOut" ? 1 - t : transition == "SlideLeft" || transition == "SlideRight" ? 1 : t);
                if (transition == "SlideLeft" || transition == "SlideRight") rect.anchoredPosition = new Vector2((transition == "SlideLeft" ? -1 : 1) * rect.rect.width * (1 - t), 0);
                if (t >= 1) { _background.sprite = sprite; _background.color = transition == "FadeOut" ? new Color(1, 1, 1, 0) : Color.white; BackgroundKey = key; }
            }, finished => RemoveObject(rect.gameObject));
        }
        private ActorSlot RequireActor(int id) => _actors.TryGetValue(id, out var slot) ? slot : throw new KeyNotFoundException("角色尚未登场: " + id);
        private static int Integer(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private IPlotOperation MoveActor(int id, int position, float duration, string mode)
        {
            if (position < 0 || position >= _config.Positions.Length) throw new FormatException("移动位置越界");
            if (mode != "None" && mode != "Smooth" && mode != "Capering" && mode != "Accelerating" && mode != "Decelerating") throw new FormatException("未知移动方式: " + mode);
            var slot = RequireActor(id); var rect = (RectTransform)slot.Object.transform; var start = rect.anchoredPosition; var target = _config.Positions[position]; var version = slot.Version;
            return new PlotTweenOperation(duration, t =>
            {
                if (slot.Object == null || slot.Version != version) return;
                if (t >= 1) { rect.anchoredPosition = target; return; }
                var progress = mode == "Accelerating" ? t * t : mode == "Decelerating" ? 1 - (1 - t) * (1 - t) : t;
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, progress) + (mode == "Capering" ? new Vector2(0, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3)) * 45) : Vector2.zero);
            }, finished => { if (finished && slot.Object != null && slot.Version == version) rect.anchoredPosition = target; });
        }
        private IPlotOperation ShowActor(int id, int position, string emote, string direction)
        {
            if (position < 0 || position >= _config.Positions.Length || !_data.TryGetActor(id, out var actor)) throw new FormatException("演员或位置无效: " + id);
            if (!_actors.TryGetValue(id, out var slot))
            { var obj = _pool.Rent(actor.Thespian, _stage, () => CreateActor(actor.Thespian)); slot = new ActorSlot { Object = obj, Key = actor.Thespian, Loaded = _prefabs.ContainsKey(actor.Thespian) && ActorImage(obj).sprite != null }; if (slot.Loaded) slot.BaseSprite = ActorImage(obj).sprite; _actors.Add(id, slot); }
            slot.Version++; var target = slot; var action = new PlotRoleAction { RoleID = id, RolePos = position, Emote = emote, ActionType = direction == "Left" ? "FaceLeft" : direction == "Right" ? "FaceRight" : "Default" };
            if (slot.Loaded) return ActorAction(action, slot);
            return new PlotResourceOperation<Sprite>(done => _resources.LoadSprite(actor.Thespian, done), sprite => { if (target.Object == null) return null; ActorImage(target.Object).sprite = target.BaseSprite = sprite; target.Loaded = true; return ActorAction(action, target); });
        }
        private static bool Loop(string value) => value == "true" || value == "1" || value == "Loop";
        private IPlotOperation Audio(string key, bool loop, bool music)
        {
            if (!(_resources is IPlotAudioResourceLoader loader)) throw new NotSupportedException("资源服务未提供音频加载接口");
            if (key == "" || key == "stop") { if (music && _bgm != null) { if (Application.isPlaying) _bgm.Stop(); RemoveObject(_bgm.gameObject); _audioSources.Remove(_bgm); _bgm = null; } return null; }
            return new PlotResourceOperation<AudioClip>(done => loader.LoadAudio(key, done), clip =>
            {
                if (music && _bgm != null) { if (Application.isPlaying) _bgm.Stop(); _audioSources.Remove(_bgm); RemoveObject(_bgm.gameObject); }
                var obj = new GameObject(music ? "PlotBGM" : "PlotSound"); obj.transform.SetParent(transform, false); var source = obj.AddComponent<AudioSource>(); source.clip = clip; source.loop = loop; source.playOnAwake = false; _audioSources.Add(source); if (music) _bgm = source; if (Application.isPlaying) { source.Play(); if (_presentationPaused) source.Pause(); }
                return loop || music ? null : new PlotTweenOperation(clip.length, null, finished => { if (source != null && Application.isPlaying) source.Stop(); _audioSources.Remove(source); RemoveObject(obj); });
            });
        }
        private IPlotOperation Effect(string key, float duration, bool loop = false)
        {
            if (!(_resources is IPlotPrefabResourceLoader loader)) throw new NotSupportedException("资源服务未提供特效预制体接口");
            return new PlotResourceOperation<GameObject>(done => loader.LoadPrefab(key, done), prefab =>
            {
                var ui = prefab.transform is RectTransform; var value = ui ? Instantiate(prefab, _stage, false) : Instantiate(prefab);
                if (!ui) { var camera = Camera.main ?? UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault(c => c.enabled); if (camera != null) value.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, camera.nearClipPlane + 3)); }
                value.SetActive(true); _effects.Add(value); if (Application.isPlaying) foreach (var particle in value.GetComponentsInChildren<ParticleSystem>()) { particle.Play(); if (_presentationPaused) { particle.Pause(); _pausedParticles.Add(particle); } }
                return loop ? null : new PlotTweenOperation(duration, null, finished => { _effects.Remove(value); RemoveObject(value); });
            });
        }
        public IPlotOperation ExecuteEvent(PlotEventJsonData data, PlotEventContext context)
        {
            string P(int i, string fallback = "") => context.Parameter(data, i, fallback);
            int ID() => Integer(P(0));
            switch (data.eventName)
            {
                case "actor.show": case "PlacementRole": return ShowActor(ID(), Integer(P(1, "0")), P(2, "Neutral"), P(3));
                case "actor.hide": case "RemoveRole": { var id = ID(); var slot = RequireActor(id); ReturnActor(slot.Object); _actors.Remove(id); return null; }
                case "actor.emote": case "ChangeRole": ApplyEmotion(RequireActor(ID()), P(1, "Neutral")); return null;
                case "actor.move": case "MoveRole":
                {
                    // 原参数映射实际为(ID,移动方式,位置)，与旧注释的四参数速度形式不同；两种形状显式区分。
                    var legacy = data.eventName == "MoveRole"; var speedForm = legacy && data.parameters.Count == 4 && float.TryParse(P(1), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                    var id = ID(); var position = Integer(P(legacy ? speedForm ? 3 : 2 : 1, "0"));
                    var seconds = context.Number(data, legacy ? speedForm ? 1 : 3 : 2, _config.CharacterAnimationDuration);
                    if (speedForm) { if (seconds <= 0) throw new FormatException("移动速度必须大于零"); if (position < 0 || position >= _config.Positions.Length) throw new FormatException("移动位置越界"); seconds = Vector2.Distance(((RectTransform)RequireActor(id).Object.transform).anchoredPosition, _config.Positions[position]) / seconds; }
                    return MoveActor(id, position, seconds, P(legacy ? speedForm ? 2 : 1 : 3, "Smooth"));
                }
                case "actor.animate":
                { var id = ID(); var slot = RequireActor(id); var position = Array.FindIndex(_config.Positions, p => p == ((RectTransform)slot.Object.transform).anchoredPosition); return ActorAction(new PlotRoleAction { RoleID = id, RolePos = Math.Max(0, position), ActionType = P(1, "Default"), Emote = P(3, "Neutral") }, slot, context.Number(data, 2, _config.CharacterAnimationDuration)); }
                case "background": case "SetBackground":
                    return new PlotResourceOperation<Sprite>(done => _resources.LoadSprite(P(0), done), sprite => ChangeBackground(sprite, P(0), P(1, "CrossFade"), context.Number(data, 2, _config.BackgroundTransitionTime)));
                case "bgm": case "SetBGM": return Audio(P(0), Loop(P(1, "true")), true);
                case "sound": case "SetSoundEffect": return Audio(P(0), Loop(P(1, "false")), false);
                case "effect": return Effect(P(0), context.Number(data, 1, 1), Loop(P(2)));
                case "SetEffect": case "SetBackgroundEffect": return Effect(P(0), context.Number(data, 2, 1), Loop(P(1)));
                default: throw new NotSupportedException("表现事件未实现: " + data.eventName);
            }
        }
    }
}
