using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlotWeave
{
    public enum PlotPlaybackState { Stopped, Running, WaitingForInput, Completed, Faulted }

    public interface IPlotOperation : IDisposable
    {
        bool IsCompleted { get; }
        void Tick(float deltaTime);
    }

    public interface IPlotCommandHandler
    {
        bool CanHandle(string commandKey);
        IPlotOperation Execute(PlotCommand command, PlotContext context);
    }

    public sealed class PlotContext
    {
        private readonly Dictionary<string, string> _variables;
        public PlotDocument Document { get; }
        public string NodeId { get; internal set; }
        internal PlotContext(PlotDocument data)
        {
            Document = data;
            _variables = data.variables.ToDictionary(v => v.key, v => v.value, StringComparer.Ordinal);
        }
        public string Get(string key, string fallback = "") => _variables.TryGetValue(key, out var value) ? value : fallback;
        public void Set(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("变量名不能为空。");
            _variables[key] = value ?? "";
        }
        public string Expand(string text)
        {
            var output = text ?? "";
            foreach (var item in _variables) output = output.Replace("${" + item.Key + "}", item.Value ?? "");
            return output;
        }
    }

    /// <summary>编辑器预览与 Player 共用的播放状态机，不查询任何游戏单例或资源系统。</summary>
    public sealed class PlotSession : IDisposable
    {
        private readonly IPlotCommandHandler _handler;
        private IPlotOperation _operation;
        private int _commandIndex;
        private int _revision;
        private readonly List<PlotChoice> _choices = new List<PlotChoice>();
        public PlotPlaybackState State { get; private set; }
        public PlotContext Context { get; private set; }
        public PlotNode Current { get; private set; }
        public string Error { get; private set; } = "";
        public string Line => Current == null ? "" : Context.Expand(Context.Document.ResolveText(Current.textKey, Current.text));
        public string Speaker => Current == null ? "" : Context.Document.actors.Find(a => a.id == Current.actorId)?.name ?? Current.actorId;
        public IReadOnlyList<PlotChoice> Choices => _choices;
        public event Action Changed;
        public event Action<PlotCommand, PlotContext> PresentationRequested;

        public PlotSession(IPlotCommandHandler handler = null) { _handler = handler; }

        public void Start(IPlotDataSource source, string fromNodeId = null)
        {
            Stop();
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                var data = source.Read();
                PlotValidation.Check(data).ThrowIfInvalid();
                Context = new PlotContext(data.Clone());
                Error = "";
                Enter(string.IsNullOrEmpty(fromNodeId) ? Context.Document.entryNodeId : fromNodeId);
            }
            catch (Exception ex) { Fail(ex); }
        }

        public void Tick(float deltaTime)
        {
            if (State != PlotPlaybackState.Running) return;
            try
            {
                if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
                if (_operation != null)
                {
                    var beforeTick = _revision;
                    var pending = _operation;
                    pending.Tick(deltaTime);
                    if (_revision != beforeTick || State != PlotPlaybackState.Running) return;
                    if (!_operation.IsCompleted) return;
                    _operation.Dispose(); _operation = null;
                }
                var revision = _revision;
                // 每帧有预算，避免大量同步事件占满一帧；每个节点仍要求明确继续/选项。
                for (var budget = 0; budget < 128 && _commandIndex < Current.commands.Count; budget++)
                {
                    var command = Current.commands[_commandIndex++];
                    Execute(command);
                    if (_revision != revision || State != PlotPlaybackState.Running) return;
                    if (_operation != null)
                    {
                        if (!_operation.IsCompleted) return;
                        _operation.Dispose(); _operation = null;
                    }
                }
                if (_commandIndex < Current.commands.Count) return;
                _choices.Clear();
                foreach (var choice in Current.choices)
                    if (string.IsNullOrEmpty(choice.conditionVariable) || Context.Get(choice.conditionVariable) == choice.conditionValue) _choices.Add(choice);
                if (Current.choices.Count > 0 && _choices.Count == 0 && string.IsNullOrEmpty(Current.nextNodeId))
                    throw new InvalidOperationException("没有满足条件的选项，也没有默认 Next：" + Current.id);
                State = PlotPlaybackState.WaitingForInput;
                Changed?.Invoke();
            }
            catch (Exception ex) { Fail(ex); }
        }

        public void Advance()
        {
            if (State != PlotPlaybackState.WaitingForInput || _choices.Count != 0) return;
            Enter(Current.nextNodeId);
        }
        public void Choose(int visibleChoiceIndex)
        {
            if (State != PlotPlaybackState.WaitingForInput) return;
            if (visibleChoiceIndex < 0 || visibleChoiceIndex >= _choices.Count) throw new ArgumentOutOfRangeException(nameof(visibleChoiceIndex));
            Enter(_choices[visibleChoiceIndex].targetNodeId);
        }
        public string ChoiceText(PlotChoice choice) => Context.Expand(Context.Document.ResolveText(choice.textKey, choice.label));

        public void Stop()
        {
            _revision++;
            try { _operation?.Dispose(); }
            catch (Exception ex) { Error = "停止操作时发生错误：" + ex.Message; }
            finally { _operation = null; Current = null; Context = null; _choices.Clear(); State = PlotPlaybackState.Stopped; }
        }
        public void Dispose() { Stop(); Changed = null; PresentationRequested = null; }

        private void Enter(string nodeId)
        {
            _revision++;
            _choices.Clear();
            _commandIndex = 0;
            if (string.IsNullOrEmpty(nodeId)) { State = PlotPlaybackState.Completed; Changed?.Invoke(); return; }
            Current = Context.Document.FindNode(nodeId);
            if (Current == null) throw new InvalidOperationException("节点不存在：" + nodeId);
            Context.NodeId = nodeId;
            State = PlotPlaybackState.Running;
            Changed?.Invoke();
        }
        private void Execute(PlotCommand original)
        {
            // 在拷贝上补默认值，避免改变源资产和已编译数据。
            var command = new PlotCommand { key = original.key };
            var definition = Context.Document.commands.Find(d => d.key == original.key);
            if (definition != null) foreach (var p in definition.parameters) command.With(p.key, Context.Expand(p.defaultValue));
            foreach (var a in original.arguments) command.With(a.key, Context.Expand(a.value));
            if (command.key == "set") { Context.Set(command.Get("name"), command.Get("value")); return; }
            if (command.key == "wait")
            {
                if (!float.TryParse(command.Get("seconds", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                    throw new FormatException("等待秒数无效。");
                _operation = new DelayOperation(seconds); return;
            }
            if (_handler != null && _handler.CanHandle(command.key))
            {
                var revision = _revision;
                var operation = _handler.Execute(command, Context);
                if (_revision == revision && State == PlotPlaybackState.Running) _operation = operation;
                else operation?.Dispose();
                return;
            }
            if (command.key == "signal" || command.key.StartsWith("actor.", StringComparison.Ordinal) || command.key == "background" || command.key.StartsWith("audio.", StringComparison.Ordinal) || command.key == "effect")
            {
                PresentationRequested?.Invoke(command, Context); return;
            }
            throw new NotSupportedException("没有命令处理器：" + command.key);
        }
        private void Fail(Exception exception)
        {
            try { _operation?.Dispose(); } catch { /* 保留最初的异常。 */ }
            _operation = null;
            Error = exception.Message;
            State = PlotPlaybackState.Faulted;
            Changed?.Invoke();
        }
        private sealed class DelayOperation : IPlotOperation
        {
            private float _remaining;
            public DelayOperation(float seconds) { _remaining = seconds; }
            public bool IsCompleted => _remaining <= 0;
            public void Tick(float deltaTime) { _remaining -= deltaTime; }
            public void Dispose() { _remaining = 0; }
        }
    }
}
