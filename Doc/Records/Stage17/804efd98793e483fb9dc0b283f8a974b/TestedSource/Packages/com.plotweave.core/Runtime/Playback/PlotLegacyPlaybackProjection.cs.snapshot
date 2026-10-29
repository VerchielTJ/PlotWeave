using System;
using System.Collections;
using System.Collections.Generic;
using PlotWeave.Data;

namespace PlotWeave.Playback
{
    /// <summary>旧剧院按演员复用对象，空演员槽不显示，同演员后续配置更新已有角色；不修改作者数据。</summary>
    public sealed class PlotLegacyPlaybackProjection : IPlotValueSource
    {
        private readonly IPlotValueSource _source;
        public PlotLegacyPlaybackProjection(object source) { _source = PlotValueReader.Wrap(source); }
        public bool IsValid => _source.IsValid;
        public bool TryGetValue(string member, out object value)
        {
            if (!_source.TryGetValue(member, out value)) return false;
            if (member != "RoleAction" || value == null) return true;
            if (value is string || !(value is IEnumerable sequence)) throw new FormatException("RoleAction应为列表");
            var result = new List<object>(); var indices = new Dictionary<int, int>();
            foreach (var action in sequence)
            {
                var id = PlotValueReader.Required<int>(PlotValueReader.Wrap(action), "RoleID"); if (id == 0) continue;
                if (indices.TryGetValue(id, out var index)) result[index] = action;
                else { indices.Add(id, result.Count); result.Add(action); }
            }
            value = result; return true;
        }
    }
}
