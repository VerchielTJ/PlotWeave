using System;
using System.IO;
using PlotWeave.Authoring.Luban;
using PlotWeave.Generation;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    public static class PlotAotPreservationMigration
    {
        [Serializable] private sealed class Report{public string id,status,error,sampleLuban,originalLuban;}
        [MenuItem("Tools/PlotWeave/迁移验收/更新生成契约与Luban裁剪保留")]
        public static void PrepareVerification()
        {
            var report=new Report{id=Guid.NewGuid().ToString("N"),status="running"};
            try
            {
                PlotGenerationExample.PrepareVerification();PlotNestedBeanExample.PrepareVerification();
                var sample=AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(PlotLubanExample.Root+"/LubanProfile.asset");if(sample==null)throw new InvalidOperationException("先生成真实Luban样例");report.sampleLuban=PlotLubanPipeline.Run(sample).id;
                var original=JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));var profile=AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(original.lubanProfile);if(profile==null)throw new InvalidOperationException("先生成完整原数据Luban");report.originalLuban=PlotLubanPipeline.Run(profile).id;report.status="generated-awaiting-unity-compilation";
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();throw;}
            finally{Directory.CreateDirectory("Doc/Records/Stage17");File.WriteAllText("Doc/Records/Stage17/aot-"+report.id+".json",JsonUtility.ToJson(report,true));}
        }
    }
}
