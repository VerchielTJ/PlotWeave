using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlotWeave.Playback;
using UnityEngine;

namespace PlotWeave.Chapters
{
    /// <summary>离线独立工具的进度与首次奖励账本；一次替换提交进度、回执和奖励，重试不重复领取。</summary>
    public sealed class PlotCampaignProgressStore:IPlotCampaignProgress
    {
        [Serializable] private sealed class Receipt {public string id,key;public bool newly;}
        [Serializable] private sealed class Saved {public int version=1;public List<string> read=new List<string>();public List<Receipt> receipts=new List<Receipt>();public List<PlotCampaignItem> inventory=new List<PlotCampaignItem>();}
        private Saved _state;private readonly string _path;
        public PlotCampaignProgressStore(string path=null)
        {_path=path==null?null:Path.GetFullPath(path);_state=_path!=null&&File.Exists(_path)?JsonUtility.FromJson<Saved>(File.ReadAllText(_path)):new Saved();if(_state==null||_state.version!=1||_state.read==null||_state.receipts==null||_state.inventory==null||_state.read.Distinct().Count()!=_state.read.Count||_state.receipts.Any(r=>r==null||string.IsNullOrEmpty(r.id)||string.IsNullOrEmpty(r.key))||_state.receipts.Select(r=>r.id).Distinct().Count()!=_state.receipts.Count||_state.inventory.Any(i=>i==null||i.itemID<=0||i.count<0)||_state.inventory.Select(i=>i.itemID).Distinct().Count()!=_state.inventory.Count)throw new FormatException("剧情存档无效");}
        private static string Key(PlotChapterKind kind,int id)=>kind+":"+id;
        public bool IsRead(PlotChapterKind kind,int episodeID)=>_state.read.Contains(Key(kind,episodeID));
        public int ItemCount(int itemID)=>_state.inventory.Find(i=>i.itemID==itemID)?.count??0;
        private void Publish(Saved candidate,CancellationToken cancellation)
        {cancellation.ThrowIfCancellationRequested();if(_path!=null){Directory.CreateDirectory(Path.GetDirectoryName(_path));var temp=_path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,JsonUtility.ToJson(candidate,true));cancellation.ThrowIfCancellationRequested();if(File.Exists(_path))File.Replace(temp,_path,null);else File.Move(temp,_path);}finally{if(File.Exists(temp))File.Delete(temp);}}_state=candidate;}
        public PlotChapterAcknowledgement Commit(PlotChapterCompletion completion,IReadOnlyList<PlotCampaignItem> rewards,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();if(!int.TryParse(completion.Request.ChapterID,out var id)||id<=0)throw new FormatException("小节ID无效");var key=Key(completion.Request.Kind,id);var previous=_state.receipts.Find(r=>r.id==completion.CompletionID);if(previous!=null){if(previous.key!=key)throw new InvalidOperationException("结算ID不能复用于另一小节");return new PlotChapterAcknowledgement(previous.newly);}
            var candidate=JsonUtility.FromJson<Saved>(JsonUtility.ToJson(_state));var newly=!candidate.read.Contains(key);
            if(newly){candidate.read.Add(key);foreach(var reward in rewards){if(reward==null||reward.itemID<=0||reward.count<=0)throw new FormatException("奖励无效");var item=candidate.inventory.Find(i=>i.itemID==reward.itemID);if(item==null){item=new PlotCampaignItem{itemID=reward.itemID};candidate.inventory.Add(item);}item.count=checked(item.count+reward.count);}}
            candidate.receipts.Add(new Receipt{id=completion.CompletionID,key=key,newly=newly});Publish(candidate,cancellation);return new PlotChapterAcknowledgement(newly);
        }
        public void ImportLegacyProgress(PlotCampaignCatalog catalog,PlotChapterKind kind,int lastReadID)
        {
            if(catalog==null)throw new ArgumentNullException(nameof(catalog));if(!Enum.IsDefined(typeof(PlotChapterKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));if(lastReadID<0)throw new ArgumentOutOfRangeException(nameof(lastReadID));
            catalog.Validate();if(lastReadID==0)return;
            // 旧协议给的是最后已读小节ID；配置顺序才决定它之前的前缀，不能按ID大小猜测。
            var ordered=catalog.chapters.Where(c=>c.kind==kind).SelectMany(c=>c.episodes).ToList();if(ordered.Count==0)throw new InvalidOperationException("该剧情类型没有可导入的小节");
            var index=ordered.IndexOf(lastReadID);var count=index>=0?index+1:lastReadID>ordered.Max()?ordered.Count:throw new FormatException("旧进度ID不在当前配置中，拒绝猜测已读范围: "+lastReadID);
            var candidate=JsonUtility.FromJson<Saved>(JsonUtility.ToJson(_state));var changed=false;
            for(var i=0;i<count;i++){var key=Key(kind,ordered[i]);if(!candidate.read.Contains(key)){candidate.read.Add(key);changed=true;}}
            if(changed)Publish(candidate,CancellationToken.None);
        }
    }
    public sealed class PlotCampaignLocalBackend:IPlotChapterBackend
    {
        private readonly PlotCampaignCatalog _catalog;private readonly PlotPlaybackDocument _document;private readonly PlotCampaignProgressStore _progress;private readonly Func<DateTime> _today;private readonly Func<string,bool> _area;
        public PlotCampaignLocalBackend(PlotCampaignCatalog catalog,PlotPlaybackDocument document,PlotCampaignProgressStore progress,Func<DateTime> today=null,Func<string,bool> hasArea=null)
        {_catalog=catalog??throw new ArgumentNullException(nameof(catalog));_document=(document??throw new ArgumentNullException(nameof(document))).Snapshot();catalog.Validate(_document);_progress=progress??throw new ArgumentNullException(nameof(progress));_today=today??(()=>DateTime.Today);_area=hasArea;}
        private PlotCampaignEpisode Check(PlotChapterRequest request)
        {var id=int.Parse(request.ChapterID,System.Globalization.CultureInfo.InvariantCulture);var episode=_catalog.Episode(request.Kind,id);var owner=_catalog.Owner(request.Kind,id);if(_catalog.EpisodeState(owner,id,_progress,_today(),_area)==PlotCampaignItemState.Locked)throw new InvalidOperationException("小节尚未解锁");return episode;}
        public Task<PlotChapterContent> LoadAsync(PlotChapterRequest request,CancellationToken cancellation)
        {cancellation.ThrowIfCancellationRequested();var episode=Check(request);return Task.FromResult(_catalog.Content(_document,episode,!_progress.IsRead(request.Kind,episode.id)));}
        public Task<PlotChapterAcknowledgement> CompleteAsync(PlotChapterCompletion completion,CancellationToken cancellation)
        {cancellation.ThrowIfCancellationRequested();var episode=Check(completion.Request);return Task.FromResult(_progress.Commit(completion,_catalog.Reward(episode.awardID),cancellation));}
    }
}
