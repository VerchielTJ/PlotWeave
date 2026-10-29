using UnityEngine;

namespace PlotWeave.Authoring
{
    [CreateAssetMenu(menuName = "PlotWeave/制作工作区")]
    public sealed class PlotAuthoringProject : ScriptableObject
    {
        public PlotAtomDataHeaderAsset header;
        public PlotDialogKeyAsset dialogs;
        public PlotConstantKeyAsset constants;
        public PlotTableAsset actors;
        public string nodeFolder = "Assets/PlotWeaveWorkspace/Nodes";
        public string idColumn = "ScenesID";
        public string dialogColumn = "Dialog";
        public PlotWeave.Playback.PlotPlaybackVisualCatalog visuals;
        public PlotNodeEventTrackAsset eventTracks;
        [Tooltip("旧角色槽：0不显示，同演员重复配置取最后一次；保留作者数据，仅转换播放投影")]
        public bool legacyEmptyActorSlots;
    }
}
