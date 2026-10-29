using System;
using System.Collections;
using System.Collections.Generic;
using PlotWeave.Data;
using PlotWeave.Authoring;
using UnityEngine;

namespace PlotWeave.Playback
{
    /// <summary>接收已由游戏的Luban Tables/ByteBuf解码的对象，不引入Luban运行库或cfg依赖。</summary>
    public static class PlotLubanTableReader
    {
        public static void ApplyEventSidecar(PlotPlaybackDocument document, string json)
        {
            if (document == null) throw new ArgumentNullException(nameof(document)); document.Validate();
            var temporary = ScriptableObject.CreateInstance<PlotNodeEventTrackAsset>();
            try
            {
                temporary.ImportJson(json); foreach (var track in temporary.tracks) if (!document.Nodes.Exists(n => n.ScenesID == track.nodeID)) throw new FormatException("事件轨道节点不存在: " + track.nodeID);
                foreach (var track in temporary.tracks) document.Nodes.Find(n => n.ScenesID == track.nodeID).Events = track.events.ConvertAll(e => e.Clone());
            }
            finally { if (Application.isPlaying) UnityEngine.Object.Destroy(temporary); else UnityEngine.Object.DestroyImmediate(temporary); }
        }
        private static readonly string[] Languages = { "SimpleChinese", "TraditionalChinese", "English", "Japanese", "Korean" };
        public static IPlotValueSource TextValueSource(object row)
        {
            var source = PlotValueReader.Wrap(row); var values = new List<string>(); foreach (var language in Languages) values.Add(PlotValueReader.Required<string>(source, language));
            return new DictionaryPlotValueSource(new Dictionary<string, object> { { "key", PlotValueReader.Required<string>(source, "KeyID") }, { "value", values } });
        }
        public static PlotPlaybackDocument Read(IEnumerable nodes, IEnumerable actors, IEnumerable dialogs, IEnumerable constants, Func<object, object> nodeAdapter = null, bool legacyRoleSlots = false)
        {
            if (nodes == null || actors == null || dialogs == null || constants == null) throw new ArgumentNullException("Luban表列表"); var document = new PlotPlaybackDocument();
            foreach (var row in nodes) document.Nodes.Add(PlotPlaybackNode.Read(nodeAdapter == null ? row : nodeAdapter(row), legacyRoleSlots));
            foreach (var row in actors) { var source = PlotValueReader.Wrap(row); document.Actors.Add(new PlotPlaybackActor { ID = PlotValueReader.Required<int>(source, "ID"), Name = PlotValueReader.Required<string>(source, "Name"), Thespian = PlotValueReader.Required<string>(source, "Thespian") }); }
            void ReadTexts(IEnumerable rows, List<PlotPlaybackText> destination) { foreach (var row in rows) { var source = TextValueSource(row); destination.Add(new PlotPlaybackText { Key = PlotValueReader.Required<string>(source, "key"), Languages = PlotValueReader.List(source, "value", PlotValueReader.ConvertValue<string>) }); } }
            ReadTexts(dialogs, document.Dialogs); ReadTexts(constants, document.Constants); document.Validate(); return document;
        }
    }
}
