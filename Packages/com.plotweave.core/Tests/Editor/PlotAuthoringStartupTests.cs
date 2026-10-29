using NUnit.Framework;
using PlotWeave.Authoring;

namespace PlotWeave.Tests
{
    /// <summary>只验证启动决策；不切场景、不创建窗口、不进入Play、不保存资产。</summary>
    public sealed class PlotAuthoringStartupTests
    {
        [Test]
        public void CancelledSceneChangeDoesNotEnterPlay()
        {
            Assert.AreEqual(PlotAuthoringStartupDecision.Cancel, PlotAuthoringStartup.Decide(false, false, false, false));
        }

        [Test]
        public void RunningAuthoringSessionOnlyFocusesGame()
        {
            Assert.AreEqual(PlotAuthoringStartupDecision.FocusGame, PlotAuthoringStartup.Decide(true, false, true, true));
        }

        [Test]
        public void UnrelatedPlayRejectsSceneSwitch()
        {
            Assert.AreEqual(PlotAuthoringStartupDecision.RejectUnrelatedPlay, PlotAuthoringStartup.Decide(true, false, false, true));
        }

        [Test]
        public void EnteringPlayWaitsWithoutOpeningAnotherScene()
        {
            Assert.AreEqual(PlotAuthoringStartupDecision.WaitForTransition, PlotAuthoringStartup.Decide(false, true, false, true));
        }

        [Test]
        public void AcceptedEditModeStartOpensSceneAndEntersPlay()
        {
            Assert.AreEqual(PlotAuthoringStartupDecision.OpenSceneAndPlay, PlotAuthoringStartup.Decide(false, false, false, true));
        }
    }
}
