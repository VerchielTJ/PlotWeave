using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlotWeave.Chapters;
using PlotWeave.Playback;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyNetworkBackendTests
    {
        private sealed class Transport:IPlotLegacyStoryTransport
        {
            public bool SupportsIdempotentCompletion{get;set;}public int reads,sends,lastCommand,lastType;public int? responseNodeID;public List<string> tokens=new List<string>();public bool fail,commitBeforeFailure,wrongType;public PlotLegacyStoryProgress progress=new PlotLegacyStoryProgress();public TaskCompletionSource<PlotLegacyStoryResponse> deferred;
            public Task<PlotLegacyStoryProgress> ReadProgressAsync(int command,CancellationToken token){reads++;lastCommand=command;return Task.FromResult(progress);}
            public Task<PlotLegacyStoryResponse> CompleteAsync(int command,int type,int episodeID,string completionID,CancellationToken token)
            {sends++;lastCommand=command;lastType=type;tokens.Add(completionID);if(commitBeforeFailure){if(type==0)progress.mainNodeID=episodeID;else progress.dailyNodeID=episodeID;}if(fail)return Task.FromException<PlotLegacyStoryResponse>(new IOException("回包丢失"));return deferred?.Task??Task.FromResult(new PlotLegacyStoryResponse{type=wrongType?1-type:type,nodeID=responseNodeID??episodeID,newlyCompleted=true});}
        }
        private static PlotCampaignCatalog Catalog()=>new PlotCampaignCatalog{chapters=new List<PlotCampaignChapter>{new PlotCampaignChapter{kind=PlotChapterKind.Main,id=1,episodes=new List<int>{1001}},new PlotCampaignChapter{kind=PlotChapterKind.Daily,id=1,episodes=new List<int>{1001}}},episodes=new List<PlotCampaignEpisode>{new PlotCampaignEpisode{kind=PlotChapterKind.Main,id=1001,entryNode=1,awardID=1},new PlotCampaignEpisode{kind=PlotChapterKind.Daily,id=1001,entryNode=1,awardID=1}},rewards=new List<PlotCampaignReward>{new PlotCampaignReward{id=1,items=new List<PlotCampaignItem>{new PlotCampaignItem{itemID=7,count=10}}}}};
        private static PlotLegacyNetworkChapterBackend Backend(Transport transport,PlotCampaignProgressStore mirror,PlotCampaignCatalog catalog=null)=>new PlotLegacyNetworkChapterBackend(catalog??Catalog(),new PlotPlaybackDocument{Nodes=new List<PlotPlaybackNode>{new PlotPlaybackNode{ScenesID=1}}},mirror,transport);
        private static PlotCampaignCatalog NonIncreasingCatalog()
        {var catalog=Catalog();catalog.chapters[0].episodes=new List<int>{500,100};catalog.episodes.RemoveAll(e=>e.kind==PlotChapterKind.Main);catalog.episodes.Add(new PlotCampaignEpisode{kind=PlotChapterKind.Main,id=500,entryNode=1,awardID=1});catalog.episodes.Add(new PlotCampaignEpisode{kind=PlotChapterKind.Main,id=100,entryNode=1,awardID=1});return catalog;}
        private static PlotChapterCompletion Completion(PlotChapterKind kind=PlotChapterKind.Main)=>new PlotChapterCompletion(new PlotChapterRequest(kind,"1001"),"same-receipt");
        [TestCase(PlotChapterKind.Main,0)][TestCase(PlotChapterKind.Daily,1)] public void OriginalCommandAndTypeMappingAreExactAndRemoteRewardsAreNotGrantedLocally(PlotChapterKind kind,int wireType)
        {var transport=new Transport();var mirror=new PlotCampaignProgressStore();var backend=Backend(transport,mirror);var applied=0;backend.ResponseApplied+=_=>applied++;var completion=Completion(kind);var content=backend.LoadAsync(completion.Request,CancellationToken.None).GetAwaiter().GetResult();Assert.That(content.RequiresCompletionReport,Is.True);Assert.That(transport.lastCommand,Is.EqualTo(1503));backend.CompleteAsync(completion,CancellationToken.None).GetAwaiter().GetResult();Assert.That(transport.lastCommand,Is.EqualTo(1504));Assert.That(transport.lastType,Is.EqualTo(wireType));Assert.That(mirror.IsRead(kind,1001),Is.True);Assert.That(mirror.ItemCount(7),Is.Zero);Assert.That(applied,Is.EqualTo(1));backend.CompleteAsync(completion,CancellationToken.None).GetAwaiter().GetResult();Assert.That(transport.sends,Is.EqualTo(1));}
        [Test] public void MismatchedServerResponseCannotAdvanceProgress()
        {var transport=new Transport{wrongType=true};var mirror=new PlotCampaignProgressStore();var backend=Backend(transport,mirror);Assert.Throws<FormatException>(()=>backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult());Assert.That(mirror.IsRead(PlotChapterKind.Main,1001),Is.False);}
        [TestCase(500,500,false)][TestCase(500,100,true)][TestCase(100,100,true)]
        [TestCase(500,501,true)][TestCase(100,501,true)]
        public void CompletionAcceptsTheRequestedOrLaterConfiguredPrefixDespiteNumericIdOrder(int episodeID,int responseID,bool secondRead)
        {
            var catalog=NonIncreasingCatalog();var transport=new Transport{responseNodeID=responseID};var mirror=new PlotCampaignProgressStore();var backend=Backend(transport,mirror,catalog);var applied=0;backend.ResponseApplied+=_=>applied++;
            var completion=new PlotChapterCompletion(new PlotChapterRequest(PlotChapterKind.Main,episodeID.ToString()),"configured-prefix");var ack=backend.CompleteAsync(completion,CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(ack.NewlyCompleted,Is.True);Assert.That(mirror.IsRead(PlotChapterKind.Main,500),Is.True);Assert.That(mirror.IsRead(PlotChapterKind.Main,100),Is.EqualTo(secondRead));Assert.That(mirror.IsRead(PlotChapterKind.Daily,1001),Is.False);Assert.That(mirror.ItemCount(7),Is.Zero);Assert.That(applied,Is.EqualTo(1));
        }
        [TestCase(100,500)][TestCase(500,0)][TestCase(100,0)]
        [TestCase(500,300)][TestCase(100,300)][TestCase(500,-1)][TestCase(100,-1)]
        public void IncompleteEmptyOrUnknownCompletionPrefixCannotPublishProgressOrResponse(int episodeID,int responseID)
        {
            var catalog=NonIncreasingCatalog();var transport=new Transport{responseNodeID=responseID};var mirror=new PlotCampaignProgressStore();mirror.ImportLegacyProgress(catalog,PlotChapterKind.Daily,1001);var backend=Backend(transport,mirror,catalog);var applied=0;backend.ResponseApplied+=_=>applied++;
            var completion=new PlotChapterCompletion(new PlotChapterRequest(PlotChapterKind.Main,episodeID.ToString()),"rejected-prefix");Assert.Throws<FormatException>(()=>backend.CompleteAsync(completion,CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(mirror.IsRead(PlotChapterKind.Main,500),Is.False);Assert.That(mirror.IsRead(PlotChapterKind.Main,100),Is.False);Assert.That(mirror.IsRead(PlotChapterKind.Daily,1001),Is.True);Assert.That(mirror.ItemCount(7),Is.Zero);Assert.That(applied,Is.Zero);
        }
        [Test] public void CachedReadProgressCannotMakeABackwardCompletionResponseValid()
        {
            var catalog=NonIncreasingCatalog();var transport=new Transport{responseNodeID=500};var mirror=new PlotCampaignProgressStore();mirror.ImportLegacyProgress(catalog,PlotChapterKind.Main,100);var backend=Backend(transport,mirror,catalog);var applied=0;backend.ResponseApplied+=_=>applied++;
            var completion=new PlotChapterCompletion(new PlotChapterRequest(PlotChapterKind.Main,"100"),"cached-progress");Assert.Throws<FormatException>(()=>backend.CompleteAsync(completion,CancellationToken.None).GetAwaiter().GetResult());Assert.That(applied,Is.Zero);Assert.That(mirror.IsRead(PlotChapterKind.Main,100),Is.True);
        }
        [Test] public void LostAcknowledgementIsReconciledFromServerProgressWithoutResending()
        {var transport=new Transport{fail=true,commitBeforeFailure=true};var mirror=new PlotCampaignProgressStore();var backend=Backend(transport,mirror);Assert.Throws<IOException>(()=>backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult());var ack=backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult();Assert.That(ack.NewlyCompleted,Is.False);Assert.That(transport.sends,Is.EqualTo(1));Assert.That(mirror.IsRead(PlotChapterKind.Main,1001),Is.True);}
        [Test] public void OriginalNonIdempotentProtocolIsNotBlindlyRetriedAfterAnUnknownOutcome()
        {var transport=new Transport{fail=true};var backend=Backend(transport,new PlotCampaignProgressStore());Assert.Throws<IOException>(()=>backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult());transport.fail=false;Assert.Throws<InvalidOperationException>(()=>backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult());Assert.That(transport.sends,Is.EqualTo(1));}
        [Test] public void ExplicitlyIdempotentTransportRetriesWithTheSameCompletionIdentity()
        {var transport=new Transport{fail=true,SupportsIdempotentCompletion=true};var backend=Backend(transport,new PlotCampaignProgressStore());Assert.Throws<IOException>(()=>backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult());transport.fail=false;backend.CompleteAsync(Completion(),CancellationToken.None).GetAwaiter().GetResult();Assert.That(transport.tokens,Is.EqualTo(new[]{"same-receipt","same-receipt"}));}
        [Test] public void ConcurrentDuplicateCompletionSharesOneRequestAndCannotChangeItsChapter()
        {var transport=new Transport{deferred=new TaskCompletionSource<PlotLegacyStoryResponse>()};var backend=Backend(transport,new PlotCampaignProgressStore());var one=backend.CompleteAsync(Completion(),CancellationToken.None);var two=backend.CompleteAsync(Completion(),CancellationToken.None);Assert.That(one,Is.SameAs(two));Assert.That(transport.sends,Is.EqualTo(1));Assert.Throws<InvalidOperationException>(()=>backend.CompleteAsync(Completion(PlotChapterKind.Daily),CancellationToken.None));transport.deferred.SetResult(new PlotLegacyStoryResponse{type=0,nodeID=1001,newlyCompleted=true});one.GetAwaiter().GetResult();}
    }
}
