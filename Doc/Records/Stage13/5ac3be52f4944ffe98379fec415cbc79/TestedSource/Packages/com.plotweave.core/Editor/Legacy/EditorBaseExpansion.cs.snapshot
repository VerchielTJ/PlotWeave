using System;
using UnityEditor.Experimental.GraphView;

namespace PlotWeave.Legacy.Editor
{
    public static class EditorBaseExpansion
    { public static void Run() => PlotWeave.Authoring.PlotAuthoringBootstrap.OpenExample(); }
    public static class EditorStringExtensions
    { public static PlotNodeType ToPlotNodeType(this string value) => Enum.TryParse<PlotNodeType>(value, true, out var type) && Enum.IsDefined(typeof(PlotNodeType), type) ? type : throw new ArgumentException("未知节点类型: " + value); }
    public static class EdgeExtensions
    { public static void ClearConnections(this Edge edge) { if (edge == null) return; edge.output?.Disconnect(edge); edge.input?.Disconnect(edge); edge.RemoveFromHierarchy(); } }
}
