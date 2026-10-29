using System;
using System.Collections.Generic;

namespace PlotWeave.Playback
{
    public sealed class PlotChoiceOption
    {
        public int Index { get; }
        public string Key { get; }
        public string Text { get; }
        public int Target { get; }
        public PlotChoiceOption(int index, string key, string text, int target) { Index = index; Key = key; Text = text; Target = target; }
    }
    public interface IPlotBranchView
    {
        void ShowChoices(IReadOnlyList<PlotChoiceOption> choices, Action<int> choose);
        void ClearChoices();
    }
    public interface IPlotPausableView { void SetPaused(bool paused); }
}
