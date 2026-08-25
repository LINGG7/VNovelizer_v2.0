using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VNovelizer.Core.Commands;

namespace VNovelizer.Tests
{
    public class ParallelCommandTests
    {
        private CommandManager manager;

        [SetUp]
        public void SetUp()
        {
            manager = CommandManager.GetInstance();
            manager.InterruptAll();
            manager.Init();
            manager.RegisterCommand(new ParallelProbeCommand());
            manager.RegisterCommand(new MarkCommand());
            ParallelProbeCommand.Reset();
            MarkCommand.ExecutionCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            manager.InterruptAll();
        }

        [UnityTest]
        public IEnumerator ParallelWaitsForLongestMemberOnly()
        {
            float start = Time.realtimeSinceStartup;
            yield return manager.ExecuteCommandsAsync(
                "parallel(wait(0.1);wait(0.3))");
            float elapsed = Time.realtimeSinceStartup - start;

            Assert.That(elapsed, Is.GreaterThanOrEqualTo(0.25f));
            Assert.That(elapsed, Is.LessThan(0.8f));
        }

        [UnityTest]
        public IEnumerator NestedParallelCompletesAndPreservesSerialOrder()
        {
            yield return manager.ExecuteCommandsAsync(
                "parallel(wait(0.05);parallel(wait(0.05);wait(0.1)))&wait(0.05)");

            Assert.That(manager.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator SameCommandTypeUsesIndependentInstances()
        {
            yield return manager.ExecuteCommandsAsync(
                "parallel(parallelprobe(0.1);parallelprobe(0.1))");

            Assert.That(ParallelProbeCommand.MaxActiveCount, Is.EqualTo(2));
            Assert.That(ParallelProbeCommand.CompletionCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MalformedOrEmptyParallelDoesNotDeadlock()
        {
            yield return manager.ExecuteCommandsAsync("parallel()");
            yield return manager.ExecuteCommandsAsync("parallel(wait(0.01);");

            Assert.That(manager.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptStopsMembersAndSkipsFollowingSerialCommand()
        {
            Coroutine flow = MonoManager.GetInstance().StartCoroutine(
                manager.ExecuteCommandsAsync(
                    "parallel(parallelprobe(0.5);parallelprobe(0.5))&mark()"));

            yield return null;
            manager.InterruptAll();
            yield return null;
            yield return flow;

            Assert.That(ParallelProbeCommand.InterruptCount, Is.EqualTo(2));
            Assert.That(MarkCommand.ExecutionCount, Is.Zero);
            Assert.That(manager.IsRunning, Is.False);
        }

        public sealed class ParallelProbeCommand : VNCommand
        {
            private bool interrupted;

            public static int ActiveCount;
            public static int MaxActiveCount;
            public static int CompletionCount;
            public static int InterruptCount;

            public override string CommandName => "parallelprobe";

            public override bool Execute(string args)
            {
                return true;
            }

            public override IEnumerator ExecuteAsync(string args)
            {
                float.TryParse(args, out float duration);
                ActiveCount++;
                MaxActiveCount = Mathf.Max(MaxActiveCount, ActiveCount);

                float elapsed = 0f;
                while (!interrupted && elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                ActiveCount--;
                if (!interrupted)
                {
                    CompletionCount++;
                }
            }

            public override void Interrupt()
            {
                interrupted = true;
                InterruptCount++;
            }

            public static void Reset()
            {
                ActiveCount = 0;
                MaxActiveCount = 0;
                CompletionCount = 0;
                InterruptCount = 0;
            }
        }

        public sealed class MarkCommand : VNCommand
        {
            public static int ExecutionCount;

            public override string CommandName => "mark";

            public override bool Execute(string args)
            {
                ExecutionCount++;
                return true;
            }
        }
    }
}
