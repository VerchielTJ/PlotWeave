using System; using System.Collections.Generic; using System.Linq; using Luban; using PlotWeave.Playback; using UnityEngine;
namespace PlotWeave.LubanSample {
public sealed class PlotLubanSamplePlayer : MonoBehaviour {
 public List<TextAsset> binaries = new List<TextAsset>();
 void Start() {
  var files = binaries.ToDictionary(b => b.name, b => b.bytes, StringComparer.Ordinal);
  var tables = new global::PlotWeaveLuban.Tables(name => new ByteBuf(files[name]));
  var data = PlotLubanTableReader.Read(tables.TbPlotAtom.DataList, tables.TbActor.DataList, tables.TbDialog.DataList, tables.TbConstant.DataList);
  var host = GetComponent<PlotPlayerHost>(); host.Initialize(data, PlotPlayerRuntimeMode.GameRuntime); host.Play(1001);
 }
}
}
