#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.Serialization;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Tests
{
    public class BranchDebugRequestTests
    {
        // Bypass VNManager's constructor so these tests never create UI, read saves, or initialize gameplay.
        private static VNManager IsolatedManager()
        {
            return (VNManager)FormatterServices.GetUninitializedObject(typeof(VNManager));
        }

        [Test]
        public void EnumerationIncludesOnlyDirectChildrenAndStopsAtSiblingBoundary()
        {
            var manager = IsolatedManager();
            var lines = new List<StoryLine> {
                new StoryLine { ID = "root", Note = "层级=2;源类型=type:exeBC" },
                new StoryLine { ID = "A", Note = "层级=3" },
                new StoryLine { ID = "nested", Note = "层级=4" },
                new StoryLine { ID = "no-level" },
                new StoryLine { ID = "B", Note = "层级=3" },
                new StoryLine { ID = "sibling", Note = "层级=2" },
                new StoryLine { ID = "outside", Note = "层级=3" } };
            typeof(VNManager).GetProperty("StoryLines").SetValue(manager, lines);
            var method = typeof(VNManager).GetMethod("DebugGetDirectBranchIndices", BindingFlags.NonPublic | BindingFlags.Instance);
            var indices = new List<int>((IEnumerable<int>)method.Invoke(manager, new object[] { 0 }));
            Assert.That(indices, Is.EqualTo(new[] { 1, 4 }));
            lines[0].Note = "no-level";
            indices = new List<int>((IEnumerable<int>)method.Invoke(manager, new object[] { 0 }));
            Assert.That(indices, Is.Empty);
        }

        [Test]
        public void WaitingBranchBlocksClickSkipAutoAndReentrantPlayback()
        {
            var manager = IsolatedManager();
            manager.CurrentLineIndex = 4;
            typeof(VNManager).GetField("debugBranchPause", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, CreatePause());
            manager.NextLine();
            manager.NextLineWithoutAnimation();
            manager.CheckAutoPlay();
            manager.ExecuteChoiceCommand("jump(999)");
            foreach (string name in new[] { "PlayCurrentLine", "PlayCurrentLineImmediately" })
                typeof(VNManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            Assert.That(manager.CurrentLineIndex, Is.EqualTo(4));
            Assert.That(manager.DebugIsWaitingForBranch, Is.True);
            manager.DebugCancelBranchRequest();
            Assert.That(manager.DebugIsWaitingForBranch, Is.False);
            Assert.That(manager.DebugBranchPause, Is.Null);
        }
        private static VNScriptDebugBranchPause CreatePause(bool matches = false)
        {
            return new VNScriptDebugBranchPause("chapter", 4, "exe-4", "existing", new[] {
                new VNScriptDebugBranchOption(5, new StoryLine { ID = "route-A", Note = "tag=original" }, "requires missing tag", matches),
                new VNScriptDebugBranchOption(9, new StoryLine { ID = "route-B" }, "fallback", null) });
        }

        [Test]
        public void ForceSelectionAcceptsUnmatchedRouteWithoutChangingTagsOrSource()
        {
            var pause = CreatePause();
            Assert.That(pause.TryQueueResume(pause.Token, 5), Is.True);
            Assert.That(pause.TryConsumeResume(out int? target), Is.True);
            Assert.That(target, Is.EqualTo(5));
            Assert.That(pause.Tags, Is.EqualTo("existing"));
            Assert.That(pause.Options[0].Matches, Is.False);
            Assert.That(pause.Options[0].Line.FullContent, Does.Contain("tag=original"));
        }

        [Test]
        public void OriginalConditionResumeHasNoForcedTarget()
        {
            var pause = CreatePause();
            Assert.That(pause.TryQueueResume(pause.Token, null), Is.True);
            Assert.That(pause.TryConsumeResume(out int? target), Is.True);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void RepeatedClicksCannotReplaceOrRepeatAcceptedSelection()
        {
            var pause = CreatePause();
            Assert.That(pause.TryQueueResume(pause.Token, 5), Is.True);
            Assert.That(pause.TryQueueResume(pause.Token, 9), Is.False);
            Assert.That(pause.TryQueueResume(pause.Token, null), Is.False);
            Assert.That(pause.TryConsumeResume(out int? target), Is.True);
            Assert.That(target, Is.EqualTo(5));
            Assert.That(pause.TryConsumeResume(out _), Is.False);
            Assert.That(pause.TryQueueResume(pause.Token, 9), Is.False);
        }

        [Test]
        public void PreviousVisitCannotControlNewVisitToSameExeBC()
        {
            var oldPause = CreatePause();
            var newPause = CreatePause();
            Assert.That(newPause.TryQueueResume(oldPause.Token, 5), Is.False);
            Assert.That(newPause.ResumeQueued, Is.False);
            Assert.That(newPause.TryQueueResume(newPause.Token, 9), Is.True);
        }

        [Test]
        public void ArbitraryIndexCannotBypassDirectChildValidation()
        {
            var pause = CreatePause();
            foreach (int index in new[] { -1, 4, 6, int.MaxValue })
                Assert.That(pause.TryQueueResume(pause.Token, index), Is.False);
            Assert.That(pause.ResumeQueued, Is.False);
            Assert.That(pause.TryConsumeResume(out _), Is.False);
        }

        [Test]
        public void NoCandidatesStillAllowsOriginalResolution()
        {
            var pause = new VNScriptDebugBranchPause("chapter", 0, "root", "", Array.Empty<VNScriptDebugBranchOption>());
            Assert.That(pause.TryQueueResume(pause.Token, 1), Is.False);
            Assert.That(pause.TryQueueResume(pause.Token, null), Is.True);
            Assert.That(pause.TryConsumeResume(out int? target), Is.True);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void RequestKeepsStableOrderedCandidateSnapshot()
        {
            var source = new StoryLine { ID = "first", Text = "original" };
            var options = new List<VNScriptDebugBranchOption> {
                new VNScriptDebugBranchOption(8, source, "AND: A = 1, B = 0", false),
                new VNScriptDebugBranchOption(12, new StoryLine { ID = "second" }, "OR: C, D", true) };
            var pause = new VNScriptDebugBranchPause("chapter", 7, "root", "A", options);
            options.Clear();
            source.Text = "changed during execution";
            Assert.That(pause.Options.Count, Is.EqualTo(2));
            Assert.That(pause.Options[0].Index, Is.EqualTo(8));
            Assert.That(pause.Options[1].Index, Is.EqualTo(12));
            Assert.That(pause.Options[0].Line.Text, Is.EqualTo("original"));
            Assert.Throws<NotSupportedException>(() => ((IList<VNScriptDebugBranchOption>)pause.Options).Clear());
        }
    }
}
#endif