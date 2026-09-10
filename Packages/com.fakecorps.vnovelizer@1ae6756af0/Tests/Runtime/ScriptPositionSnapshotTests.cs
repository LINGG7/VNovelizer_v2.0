#if UNITY_EDITOR
using System;
using NUnit.Framework;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Tests
{
    public class ScriptPositionSnapshotTests
    {
        [Test]
        public void DefaultSnapshotHasNoCurrentLineAndCannotCopyStalePosition()
        {
            var snapshot = default(VNScriptDebugSnapshot);
            Assert.That(snapshot.CurrentLine, Is.Null);
            Assert.That(snapshot.CopyPositionText(), Is.Empty);
        }

        [Test]
        public void SourceContentSurvivesRuntimeInheritanceAndMutation()
        {
            var source = new StoryLine { ID = "branch.42", Speaker = "角色", Text = "原文\n第二行", Background = "", Command = "wait(1)" };
            var line = new VNScriptDebugLine(source);
            source.ID = "changed";
            source.Text = "changed";
            source.Background = "inherited-background";
            source.Command = "jump(99)";
            Assert.That(line.ID, Is.EqualTo("branch.42"));
            Assert.That(line.Text, Is.EqualTo("原文\n第二行"));
            Assert.That(line.Command, Is.EqualTo("wait(1)"));
            Assert.That(line.FullContent, Does.Not.Contain("inherited-background"));
        }

        [Test]
        public void CopyIncludesScriptIdAndFullMultilineContentWithoutInventingSourceRow()
        {
            var rows = Array.AsReadOnly(new[] { new VNScriptDebugLine(new StoryLine { ID = "999", Speaker = "角色", Text = "第一行\n第二行", Command = "choice(A|jump(7))" }) });
            var snapshot = new VNScriptDebugSnapshot("chapter", rows, 0, "等待选择");
            string text = snapshot.CopyPositionText();
            Assert.That(text, Does.Contain("剧本: chapter"));
            Assert.That(text, Does.Contain("语句 ID: 999"));
            Assert.That(text, Does.Contain("源文件行号: 不可用"));
            Assert.That(text, Does.Contain("第一行\n第二行"));
            Assert.That(text, Does.Contain("choice(A|jump(7))"));
            Assert.That(rows[0].SourceLineNumber, Is.Null);
        }

        [Test]
        public void EndedAndInvalidPositionsNeverExposePreviousLine()
        {
            var rows = Array.AsReadOnly(new[] { new VNScriptDebugLine(new StoryLine { ID = "last" }) });
            foreach (int index in new[] { -1, 1, int.MaxValue })
            {
                var snapshot = new VNScriptDebugSnapshot("chapter", rows, index, "执行结束");
                Assert.That(snapshot.CurrentLine, Is.Null);
                Assert.That(snapshot.CopyPositionText(), Is.Empty);
            }
        }

        [Test]
        public void SelectionUsesExecutionIndexRatherThanParsingStatementId()
        {
            var rows = Array.AsReadOnly(new[] {
                new VNScriptDebugLine(new StoryLine { ID = "900" }),
                new VNScriptDebugLine(new StoryLine { ID = "branch-A" }) });
            var snapshot = new VNScriptDebugSnapshot("chapter", rows, 1, "等待点击");
            Assert.That(snapshot.CurrentLine.ID, Is.EqualTo("branch-A"));
        }

        [Test]
        public void LongSummaryIsBoundedButFullContentIsPreserved()
        {
            string longText = new string('文', 2000) + "\n尾行";
            var line = new VNScriptDebugLine(new StoryLine { Text = longText, Command = "wait(2)" });
            Assert.That(line.Summary.Length, Is.LessThanOrEqualTo(241));
            Assert.That(line.Summary, Does.Not.Contain("\n"));
            Assert.That(line.FullContent, Does.Contain(longText));
            Assert.That(line.FullContent, Does.Contain("wait(2)"));
        }

        [Test]
        public void CommandOnlyAndEmptyRecordsHaveReadableSummaries()
        {
            Assert.That(new VNScriptDebugLine(new StoryLine { Command = "jump(42)" }).Summary, Is.EqualTo("jump(42)"));
            Assert.That(new VNScriptDebugLine(null).Summary, Is.Not.Empty);
        }
    }
}
#endif