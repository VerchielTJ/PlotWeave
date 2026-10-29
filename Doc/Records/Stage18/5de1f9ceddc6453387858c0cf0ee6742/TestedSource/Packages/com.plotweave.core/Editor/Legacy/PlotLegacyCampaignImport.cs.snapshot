using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Excel;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using PlotWeave.Chapters;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    public static class PlotLegacyCampaignImport
    {
        public const string InputPath="Tools/PlotWeaveMigration/Inputs/original-tables.json",LatestPath="Tools/PlotWeaveMigration/imported-campaign.json";
        [Serializable] public sealed class Report {public string id,status,error,root,catalog,roundtripWorkspace,lubanProfile,lubanReport;public int workbooks,chapters,episodes,rewards,nodes;public List<string> warnings=new List<string>();}
        private static string Cell(PlotLegacyTable table,int row,int col)=>row<table.rows.Count&&col<table.rows[row].values.Count?table.rows[row].values[col]??"":"";
        private static IEnumerable<Dictionary<string,object>> Rows(PlotLegacyTable table)
        {
            if(Cell(table,0,0)!="##var")throw new FormatException("原章节工作簿缺少表头: "+table.source);
            for(var r=3;r<table.rows.Count;r++)
            {
                if(Cell(table,r,1)==""||Cell(table,r,0).StartsWith("##",StringComparison.Ordinal))continue;
                var data=new Dictionary<string,object>(StringComparer.Ordinal);
                for(var c=1;c<table.rows[0].values.Count;c++){var name=Cell(table,0,c);if(name=="")continue;var text=Cell(table,r,c);data[name]=name=="PlotIDs"?(object)text.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(v=>(object)int.Parse(v.Trim(),CultureInfo.InvariantCulture)).ToList():text;}
                yield return data;
            }
        }
        public static PlotCampaignCatalog Read(PlotLegacyBundle bundle)
        {
            PlotLegacyTable Table(string name)=>bundle.tables.Single(t=>t.source=="Datas/"+name+".xlsx");
            var catalog=new PlotCampaignCatalog();foreach(var kind in new[]{PlotChapterKind.Main,PlotChapterKind.Daily})
            {var prefix=kind==PlotChapterKind.Main?"主线":"日常";catalog.chapters.AddRange(Rows(Table(prefix+"剧情章节表")).Select(r=>PlotCampaignCatalog.ReadChapter(r,kind)));catalog.episodes.AddRange(Rows(Table(prefix+"剧情小节表")).Select(r=>PlotCampaignCatalog.ReadEpisode(r,kind)));}
            var rewards=Table("剧情奖励表");PlotCampaignReward current=null;
            for(var r=3;r<rewards.rows.Count;r++)
            {
                if(rewards.rows[r].values.All(string.IsNullOrEmpty)||Cell(rewards,r,0).StartsWith("##",StringComparison.Ordinal))continue;
                var id=Cell(rewards,r,1);if(id!=""){current=new PlotCampaignReward{id=int.Parse(id,CultureInfo.InvariantCulture)};catalog.rewards.Add(current);}
                if(Cell(rewards,r,2)==""&&Cell(rewards,r,3)=="")continue;if(current==null)throw new FormatException("奖励续行没有所属ID");current.items.Add(new PlotCampaignItem{itemID=int.Parse(Cell(rewards,r,2),CultureInfo.InvariantCulture),count=int.Parse(Cell(rewards,r,3),CultureInfo.InvariantCulture)});
            }
            catalog.Validate();return catalog;
        }
        [MenuItem("Tools/PlotWeave/迁移工具/迁入全部原表版本与故事目录")]
        public static void PrepareVerification()
        {
            var bundle=JsonUtility.FromJson<PlotLegacyBundle>(File.ReadAllText(InputPath));var report=new Report{id=Guid.NewGuid().ToString("N"),status="running"};report.root="Assets/PlotWeaveLocal/Campaign/Import_"+report.id.Substring(0,10);Directory.CreateDirectory(report.root);AssetDatabase.Refresh();
            try
            {
                foreach(var table in bundle.tables)
                {
                    var sheet=new PlotExcelSheet{Rows=table.rows.Select(r=>r.values.ToArray()).ToList()};var relative=table.source.Replace('/','_');var path=report.root+"/OriginalVersions/"+relative;PlotExcelFile.Write(path,sheet);var read=PlotExcelFile.Read(path);for(var r=0;r<sheet.Height;r++)for(var c=0;c<sheet.Width;c++)if(sheet.Get(r,c)!=read.Get(r,c))throw new InvalidOperationException("重建工作簿单元格不一致: "+table.source+":"+r+","+c);report.workbooks++;
                }
                var authoringJson=File.ReadAllText(PlotLegacyAssetImport.InputPath);var authoringBundle=JsonUtility.FromJson<PlotLegacyBundle>(authoringJson);var imported=PlotLegacyAssetImport.Import(authoringBundle,authoringJson,PlotLegacyAssetImport.LocalRoot+"/Roundtrip_"+report.id.Substring(0,10),false);report.roundtripWorkspace=imported.projectPath;var project=AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(imported.projectPath);
                var rows=new PlotEditorManager(project).ListRows();var before=rows.ToDictionary(r=>r.rowId,PlotLegacyAssetImport.RowHash,StringComparer.Ordinal);var folder=report.root+"/AuthoringRoundtrip";PlotExcelExchange.Export(project,folder);using(var plan=PlotExcelExchange.PlanImport(project,folder)){if(plan.ImportedNodes!=rows.Count)throw new Exception("六表回读节点数量不符");plan.Commit("Library/PlotWeaveRecovery/OriginalRoundtrip/"+report.id);}
                foreach(var row in new PlotEditorManager(project).ListRows())if(!before.TryGetValue(row.rowId,out var hash)||hash!=PlotLegacyAssetImport.RowHash(row))throw new Exception("六表往返改变节点语义或身份: "+row.rowTitle);report.nodes=rows.Count;
                var document=PlotPlaybackCompiler.Compile(project);var catalog=ScriptableObject.CreateInstance<PlotCampaignAsset>();catalog.data=Read(bundle);catalog.data.Validate(document);report.catalog=report.root+"/Campaign.asset";AssetDatabase.CreateAsset(catalog,report.catalog);report.chapters=catalog.data.chapters.Count;report.episodes=catalog.data.episodes.Count;report.rewards=catalog.data.rewards.Count;
                foreach(var chapter in catalog.data.chapters)foreach(var text in new[]{chapter.name,chapter.desc})if(text.StartsWith("Zero_",StringComparison.Ordinal)&&!project.constants.HasKey(text))report.warnings.Add("unresolved-original-chapter-text-key:"+text);
                var profile=ScriptableObject.CreateInstance<PlotLubanProfile>();profile.project=project;profile.generatedRoot=report.root+"/LubanGenerated";profile.generatedNamespace="PlotWeaveOriginalLuban"+report.id.Substring(0,8);profile.assemblyName="PlotWeave.Luban.Original.Run"+report.id.Substring(0,8);profile.dotnetPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet/dotnet.exe");report.lubanProfile=report.root+"/LubanProfile.asset";AssetDatabase.CreateAsset(profile,report.lubanProfile);AssetDatabase.SaveAssets();var generated=PlotLubanPipeline.Run(profile);report.lubanReport=generated.id;
                report.status="tables-roundtrip-luban-generated-runtime-read-pending";File.WriteAllText(LatestPath,JsonUtility.ToJson(report,true));
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();throw;}
            finally{Directory.CreateDirectory("Doc/Records/Stage15");File.WriteAllText("Doc/Records/Stage15/tables-"+report.id+".json",JsonUtility.ToJson(report,true));}
        }
    }
}
