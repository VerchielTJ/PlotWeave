using System;
using System.Collections.Generic;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using UnityEngine;

namespace PlotWeave.Authoring
{
    [Serializable] public sealed class PlotNodeEventTrack { public int nodeID; public List<PlotEventJsonData> events = new List<PlotEventJsonData>(); }
    [Serializable] public sealed class PlotEventTracksDocument { public List<PlotNodeEventTrack> tracks = new List<PlotNodeEventTrack>(); }
    [CreateAssetMenu(menuName = "PlotWeave/数据/节点事件轨道")]
    public sealed class PlotNodeEventTrackAsset : PlotAuthoringAsset
    {
        public List<PlotNodeEventTrack> tracks = new List<PlotNodeEventTrack>();
        public override AuthoringValidationResult Validate()
        {
            var result = new AuthoringValidationResult(); var ids = new HashSet<int>();
            if (tracks == null) { result.Errors.Add("事件轨道列表为空"); return result; }
            foreach (var track in tracks)
            {
                if (track == null || track.nodeID <= 0 || !ids.Add(track.nodeID)) { result.Errors.Add("轨道节点ID无效或重复"); continue; }
                try { PlotEventScheduler.Validate(track.events); } catch (Exception error) { result.Errors.Add(track.nodeID + ": " + error.Message); }
            }
            return result;
        }
        public string ExportJson() { Validate().ThrowIfInvalid(); return JsonUtility.ToJson(new PlotEventTracksDocument { tracks = tracks }, true); }
        public void ImportJson(string json)
        {
            var data = JsonUtility.FromJson<PlotEventTracksDocument>(json) ?? throw new FormatException("事件轨道JSON为空");
            var temporary = CreateInstance<PlotNodeEventTrackAsset>();
            try { temporary.tracks = data.tracks; temporary.Validate().ThrowIfInvalid(); tracks = data.tracks; } finally { DestroyImmediate(temporary); }
        }
    }
}
