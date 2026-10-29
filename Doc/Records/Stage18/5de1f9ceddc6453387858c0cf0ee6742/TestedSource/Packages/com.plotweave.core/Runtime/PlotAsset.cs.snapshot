using UnityEngine;

namespace PlotWeave
{
    [CreateAssetMenu(menuName = "PlotWeave/剧情资产", fileName = "NewStory")]
    public sealed class PlotAsset : ScriptableObject, IPlotDataSource
    {
        public PlotDocument document = new PlotDocument();
        public PlotDocument Read() => document.Clone();
    }
}
