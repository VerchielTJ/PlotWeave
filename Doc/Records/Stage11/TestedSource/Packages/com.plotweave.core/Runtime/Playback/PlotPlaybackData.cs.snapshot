using System;
using System.Collections.Generic;
using System.Linq;
using PlotWeave.Data;
using UnityEngine;

namespace PlotWeave.Playback
{
    [Serializable] public sealed class PlotRoleAction
    {
        public int RoleID;
        public string ActionType = "";
        public string Emote = "";
        public int RolePos;
    }
    [Serializable] public sealed class PlotSetBackground { public string SetType = ""; public string BackName = ""; }
    [Serializable] public sealed class PlotBranch { public string Key = ""; public int Target; }
    [Serializable] public sealed class PlotPlaybackNode : IPlotDataBase
    {
        public int ScenesID;
        public int PlotRoleID;
        public string Dialog = "";
        public int Next;
        public List<PlotRoleAction> RoleAction = new List<PlotRoleAction>();
        public List<PlotSetBackground> PlotSetBack = new List<PlotSetBackground>();
        public List<PlotBranch> Branch = new List<PlotBranch>();
        public List<PlotWeave.Legacy.PlotEventJsonData> Events = new List<PlotWeave.Legacy.PlotEventJsonData>();
        public bool IsValid => ScenesID > 0;
        public string DataSource => "PlaybackSnapshot";
        public void Validate()
        {
            if (ScenesID <= 0 || Next < 0 || PlotRoleID < 0 || RoleAction == null || PlotSetBack == null || Branch == null) throw new FormatException("播放节点结构无效: " + ScenesID);
            if (RoleAction.Any(a => a == null || a.RoleID <= 0 || a.RolePos < 0) || RoleAction.Select(a => a.RoleID).Distinct().Count() != RoleAction.Count) throw new FormatException("角色行为无效或重复: " + ScenesID);
            if (PlotSetBack.Any(b => b == null) || Branch.Any(b => b == null || b.Target < 0)) throw new FormatException("背景或分支数据无效: " + ScenesID);
            PlotEventScheduler.Validate(Events ?? new List<PlotWeave.Legacy.PlotEventJsonData>());
        }
        /// <summary>作者值源、生成的Editor/Game适配器或公开配表对象均经同一读口进入Core。</summary>
        public static PlotPlaybackNode Read(object data, bool legacyRoleSlots = false)
        {
            var source = legacyRoleSlots ? new PlotLegacyPlaybackProjection(data) : PlotValueReader.Wrap(data);
            var node = new PlotPlaybackNode {
                ScenesID = PlotValueReader.Required<int>(source, "ScenesID"), PlotRoleID = PlotValueReader.Required<int>(source, "PlotRoleID"),
                Dialog = PlotValueReader.Required<string>(source, "Dialog"), Next = PlotValueReader.Required<int>(source, "Next"),
                RoleAction = PlotValueReader.List(source, "RoleAction", item => { var s = PlotValueReader.Wrap(item); return new PlotRoleAction { RoleID = PlotValueReader.Required<int>(s, "RoleID"), ActionType = PlotValueReader.Value<string>(s, "ActionType"), Emote = PlotValueReader.Value<string>(s, "Emote"), RolePos = PlotValueReader.Required<int>(s, "RolePos") }; }),
                PlotSetBack = PlotValueReader.List(source, "PlotSetBack", item => { var s = PlotValueReader.Wrap(item); return new PlotSetBackground { SetType = PlotValueReader.Value<string>(s, "SetType"), BackName = PlotValueReader.Required<string>(s, "BackName") }; }),
                Branch = PlotValueReader.List(source, "Branch", item => { var s = PlotValueReader.Wrap(item); return new PlotBranch { Key = PlotValueReader.Required<string>(s, "Key"), Target = PlotValueReader.Required<int>(s, "Target") }; })
            };
            node.Validate(); return node;
        }
    }
    [Serializable] public sealed class PlotPlaybackActor
    { public int ID; public string Name = ""; public string Thespian = ""; }
    [Serializable] public sealed class PlotPlaybackText
    { public string Key = ""; public List<string> Languages = new List<string>(); }
    [Serializable] public sealed class PlotPlaybackDocument
    {
        public int FormatVersion = 1;
        public List<PlotPlaybackNode> Nodes = new List<PlotPlaybackNode>();
        public List<PlotPlaybackActor> Actors = new List<PlotPlaybackActor>();
        public List<PlotPlaybackText> Dialogs = new List<PlotPlaybackText>();
        public List<PlotPlaybackText> Constants = new List<PlotPlaybackText>();
        public void Validate()
        {
            if (FormatVersion != 1 || Nodes == null || Actors == null || Dialogs == null || Constants == null) throw new FormatException("播放文档版本或容器无效");
            var ids = new HashSet<int>(); foreach (var node in Nodes) { if (node == null) throw new FormatException("播放节点为空"); node.Validate(); if (!ids.Add(node.ScenesID)) throw new FormatException("播放节点重复: " + node.ScenesID); }
            ids.Clear(); foreach (var actor in Actors) if (actor == null || actor.ID <= 0 || !ids.Add(actor.ID)) throw new FormatException("演员ID无效或重复");
            foreach (var table in new[] { Dialogs, Constants })
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var text in table) if (text == null || string.IsNullOrWhiteSpace(text.Key) || !keys.Add(text.Key) || text.Languages == null || text.Languages.Count != 5) throw new FormatException("文本KEY、语言数量或记录无效");
            }
        }
        public PlotPlaybackDocument Snapshot() { Validate(); return JsonUtility.FromJson<PlotPlaybackDocument>(JsonUtility.ToJson(this)); }
    }
}
