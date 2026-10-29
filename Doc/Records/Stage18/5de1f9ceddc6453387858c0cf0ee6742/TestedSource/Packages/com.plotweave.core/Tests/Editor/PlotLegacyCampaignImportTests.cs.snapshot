using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using PlotWeave.Chapters;
using PlotWeave.Legacy.Editor;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyCampaignImportTests
    {
        public sealed class Loader<T>{public string root;public T Load(string name)=>(T)Activator.CreateInstance(typeof(T),File.ReadAllBytes(root+"/"+name+".bytes"));}
        private static Type Type(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null)??throw new InvalidOperationException("尚未编译生成的Luban程序集: "+name);
        private static string DocumentHash(PlotPlaybackDocument document)
        {
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(document)))).Replace("-","").ToLowerInvariant();
        }
        [Test] public void AllOriginalLubanBinariesDecodeToExactlyTheAuthoringPlaybackDocument()
        {
            if(!File.Exists(PlotLegacyCampaignImport.LatestPath)){Assert.Ignore("本地原表尚未导入");return;}
            var report=JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));var profile=AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(report.lubanProfile);var buffer=Type("Luban.ByteBuf");var loader=Activator.CreateInstance(typeof(Loader<>).MakeGenericType(buffer));loader.GetType().GetField("root").SetValue(loader,profile.generatedRoot+"/Data");var callback=Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string),buffer),loader,"Load");var tables=Activator.CreateInstance(Type(profile.generatedNamespace+".Tables"),callback);
            IEnumerable Rows(string property){var table=tables.GetType().GetProperty(property).GetValue(tables);return (IEnumerable)table.GetType().GetProperty("DataList").GetValue(table);}
            var runtime=PlotLubanTableReader.Read(Rows("TbPlotAtom"),Rows("TbActor"),Rows("TbDialog"),Rows("TbConstant"),null,true);var authoring=PlotPlaybackCompiler.Compile(profile.project);Assert.That(runtime.Nodes.Count,Is.EqualTo(485));Assert.That(DocumentHash(runtime),Is.EqualTo(DocumentHash(authoring)),"全部节点、演员及五语言文本的完整JSON摘要必须相同；失败仅输出摘要，不输出剧情原文");
        }
        [Test] public void OriginalCampaignContinuationRewardsAndBothStoryKindsAreImported()
        {
            if(!File.Exists(PlotLegacyCampaignImport.InputPath)){Assert.Ignore("本地原表未提取");return;}var bundle=JsonUtility.FromJson<PlotLegacyBundle>(File.ReadAllText(PlotLegacyCampaignImport.InputPath));var catalog=PlotLegacyCampaignImport.Read(bundle);Assert.That(catalog.chapters.Count,Is.EqualTo(2));Assert.That(catalog.episodes.Count,Is.EqualTo(4));Assert.That(catalog.rewards.Count,Is.EqualTo(4));Assert.That(catalog.Reward(1001).Select(i=>i.itemID),Is.EqualTo(new[]{1001,1002}));Assert.That(catalog.Reward(1001).Sum(i=>i.count),Is.EqualTo(20));Assert.That(catalog.Episode(PlotChapterKind.Main,1001).entryNode,Is.EqualTo(20000));Assert.That(catalog.Episode(PlotChapterKind.Daily,1001).entryNode,Is.EqualTo(10001));
        }
    }
}
