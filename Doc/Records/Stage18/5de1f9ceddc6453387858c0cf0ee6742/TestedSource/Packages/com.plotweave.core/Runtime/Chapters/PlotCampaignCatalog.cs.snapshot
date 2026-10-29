using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PlotWeave.Data;
using PlotWeave.Playback;

namespace PlotWeave.Chapters
{
    public enum PlotCampaignItemState { Read, Current, Locked }
    public interface IPlotCampaignChapter {int ID{get;}string Name{get;}IReadOnlyList<int> PlotIDs{get;}}
    public interface IPlotCampaignEpisode {int ID{get;}string Name{get;}string Desc{get;}}
    [Serializable] public sealed class PlotCampaignChapter:IPlotCampaignChapter
    {
        public PlotChapterKind kind;public int id;public string name="",desc="",unlockArea="",unlockDate="",thumbnail="",background="";public List<int> episodes=new List<int>();
        public int ID=>id;public string Name=>name;public IReadOnlyList<int> PlotIDs=>episodes;
    }
    [Serializable] public sealed class PlotCampaignEpisode:IPlotCampaignEpisode
    {
        public PlotChapterKind kind;public int id,awardID,entryNode;public string name="",desc="",topTitle="",bottomTitle="",skipKey="";
        public int ID=>id;public string Name=>name;public string Desc=>desc;
    }
    [Serializable] public sealed class PlotCampaignItem {public int itemID;public int count;}
    [Serializable] public sealed class PlotCampaignReward {public int id;public List<PlotCampaignItem> items=new List<PlotCampaignItem>();}
    public interface IPlotCampaignProgress {bool IsRead(PlotChapterKind kind,int episodeID);}
    [Serializable] public sealed class PlotCampaignCatalog
    {
        public List<PlotCampaignChapter> chapters=new List<PlotCampaignChapter>();public List<PlotCampaignEpisode> episodes=new List<PlotCampaignEpisode>();public List<PlotCampaignReward> rewards=new List<PlotCampaignReward>();
        public PlotCampaignChapter Chapter(PlotChapterKind kind,int id)=>chapters.SingleOrDefault(c=>c.kind==kind&&c.id==id)??throw new KeyNotFoundException("章节不存在: "+kind+":"+id);
        public PlotCampaignEpisode Episode(PlotChapterKind kind,int id)=>episodes.SingleOrDefault(c=>c.kind==kind&&c.id==id)??throw new KeyNotFoundException("小节不存在: "+kind+":"+id);
        public PlotCampaignChapter Owner(PlotChapterKind kind,int episodeID)=>chapters.Single(c=>c.kind==kind&&c.episodes.Contains(episodeID));
        public IReadOnlyList<PlotCampaignItem> Reward(int id)=>id==0?Array.Empty<PlotCampaignItem>():(rewards.SingleOrDefault(r=>r.id==id)??throw new KeyNotFoundException("奖励不存在: "+id)).items;
        public void Validate(PlotPlaybackDocument document=null)
        {
            if(chapters==null||episodes==null||rewards==null)throw new FormatException("剧情目录为空引用");var chapterKeys=new HashSet<string>();var episodeKeys=new HashSet<string>();var rewardKeys=new HashSet<int>();var owned=new HashSet<string>();
            foreach(var reward in rewards){if(reward==null||reward.id<=0||!rewardKeys.Add(reward.id)||reward.items==null||reward.items.Any(i=>i==null||i.itemID<=0||i.count<=0))throw new FormatException("奖励结构无效或重复");}
            foreach(var episode in episodes){if(episode==null||episode.id<=0||!Enum.IsDefined(typeof(PlotChapterKind),episode.kind)||!episodeKeys.Add(episode.kind+":"+episode.id)||episode.entryNode<=0)throw new FormatException("小节结构无效或重复");if(episode.awardID!=0&&!rewardKeys.Contains(episode.awardID))throw new FormatException("小节奖励缺失: "+episode.id);if(document!=null&&!document.Nodes.Any(n=>n.ScenesID==episode.entryNode))throw new FormatException("小节播放入口缺失: "+episode.id);}
            foreach(var chapter in chapters){if(chapter==null||chapter.id<=0||!Enum.IsDefined(typeof(PlotChapterKind),chapter.kind)||!chapterKeys.Add(chapter.kind+":"+chapter.id)||chapter.episodes==null||chapter.episodes.Count==0)throw new FormatException("章节无效、为空或重复");ParseDate(chapter.unlockDate);foreach(var episode in chapter.episodes){var key=chapter.kind+":"+episode;if(!episodeKeys.Contains(key)||!owned.Add(key))throw new FormatException("章节小节缺失或重复归属: "+key);}}
            if(owned.Count!=episodeKeys.Count)throw new FormatException("存在未归属的小节");
        }
        private static DateTime? ParseDate(string value)
        {if(string.IsNullOrWhiteSpace(value))return null;if(!DateTime.TryParseExact(value,new[]{"yyyy-M-d","yyyy-MM-dd","yyyy/M/d"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new FormatException("解锁日期无效: "+value);return date.Date;}
        public string LockReason(PlotCampaignChapter chapter,IPlotCampaignProgress progress,DateTime today,Func<string,bool> hasArea)
        {
            var date=ParseDate(chapter.unlockDate);if(date.HasValue&&today.Date<date.Value)return chapter.unlockDate+"后解锁";
            if(!string.IsNullOrEmpty(chapter.unlockArea)&&(hasArea==null||!hasArea(chapter.unlockArea)))return "需要解锁："+chapter.unlockArea;
            foreach(var previous in chapters.Where(c=>c.kind==chapter.kind)){if(ReferenceEquals(previous,chapter))break;if(previous.episodes.Any(id=>!progress.IsRead(chapter.kind,id)))return "请先完成前一章节";}return "";
        }
        public PlotCampaignItemState ChapterState(PlotCampaignChapter chapter,IPlotCampaignProgress progress,DateTime today,Func<string,bool> hasArea)
        {if(chapter.episodes.All(id=>progress.IsRead(chapter.kind,id)))return PlotCampaignItemState.Read;return LockReason(chapter,progress,today,hasArea)==""?PlotCampaignItemState.Current:PlotCampaignItemState.Locked;}
        public PlotCampaignItemState EpisodeState(PlotCampaignChapter chapter,int id,IPlotCampaignProgress progress,DateTime today,Func<string,bool> hasArea)
        {if(!chapter.episodes.Contains(id))throw new ArgumentException("小节不属于当前章节");if(progress.IsRead(chapter.kind,id))return PlotCampaignItemState.Read;if(LockReason(chapter,progress,today,hasArea)!="")return PlotCampaignItemState.Locked;foreach(var previous in chapter.episodes){if(previous==id)return PlotCampaignItemState.Current;if(!progress.IsRead(chapter.kind,previous))return PlotCampaignItemState.Locked;}throw new InvalidOperationException();}
        public int Progress(PlotCampaignChapter chapter,IPlotCampaignProgress progress)=>chapter.episodes.Count==0?0:chapter.episodes.Count(id=>progress.IsRead(chapter.kind,id))*100/chapter.episodes.Count;
        public int InitialEpisode(PlotCampaignChapter chapter,IPlotCampaignProgress progress)=>chapter.episodes.FirstOrDefault(id=>!progress.IsRead(chapter.kind,id)) is int id&&id!=0?id:chapter.episodes.Last();
        public PlotChapterContent Content(PlotPlaybackDocument document,PlotCampaignEpisode episode,bool requiresReport)
        {
            var owner=Owner(episode.kind,episode.id);bool MissingKey(string value)=>(value??"").StartsWith("Zero_",StringComparison.Ordinal)||(value??"").StartsWith("Plot_",StringComparison.Ordinal);
            string Display(string value,string fallback){if(string.IsNullOrWhiteSpace(value))return fallback;if(document.Constants.Any(t=>t.Key==value))return value;return MissingKey(value)?fallback:value;}
            var chapter=Display(owner.name,(episode.kind==PlotChapterKind.Main?"主线故事":"日常故事")+" · 第"+owner.id+"章");return new PlotChapterContent(document,episode.entryNode,requiresReport,string.IsNullOrWhiteSpace(episode.topTitle)?"":Display(episode.topTitle,chapter),string.IsNullOrWhiteSpace(episode.bottomTitle)?"":Display(episode.bottomTitle,episode.name),Display(episode.skipKey,"跳过本节剧情？"));
        }
        public static PlotCampaignChapter ReadChapter(object value,PlotChapterKind kind)
        {var s=PlotValueReader.Wrap(value);return new PlotCampaignChapter{kind=kind,id=PlotValueReader.Required<int>(s,"ID"),name=PlotValueReader.Required<string>(s,"Name"),desc=PlotValueReader.Value<string>(s,"Desc"),episodes=PlotValueReader.List(s,"PlotIDs",PlotValueReader.ConvertValue<int>),unlockArea=PlotValueReader.Value<string>(s,"UnlockAreaType"),unlockDate=PlotValueReader.Value<string>(s,"UnlockDate"),thumbnail=PlotValueReader.Value<string>(s,"ThumbnailPath"),background=PlotValueReader.Value<string>(s,"Background")};}
        public static PlotCampaignEpisode ReadEpisode(object value,PlotChapterKind kind)
        {var s=PlotValueReader.Wrap(value);return new PlotCampaignEpisode{kind=kind,id=PlotValueReader.Required<int>(s,"ID"),name=PlotValueReader.Required<string>(s,"Name"),desc=PlotValueReader.Value<string>(s,"Desc"),awardID=PlotValueReader.Required<int>(s,"AwardID"),entryNode=PlotValueReader.Required<int>(s,"AtomTargetID"),topTitle=PlotValueReader.Value<string>(s,"TopTitle"),bottomTitle=PlotValueReader.Value<string>(s,"BottomTitle"),skipKey=PlotValueReader.Value<string>(s,"SkipKey")};}
    }
}
