using System;
using NUnit.Framework;
using UnityEngine;

namespace VNovelizer.Tests
{
    public class RemoteContentFailurePolicyTests
    {
        [TestCase("HTTP 404 Not Found", RemoteContentErrorKind.ResourceMissing)]
        [TestCase("CRC mismatch", RemoteContentErrorKind.IntegrityFailure)]
        [TestCase("storage quota exceeded", RemoteContentErrorKind.StorageFailure)]
        [TestCase("HTTP 503 Service Unavailable", RemoteContentErrorKind.ServerUnavailable)]
        [TestCase("DNS resolution failed", RemoteContentErrorKind.ServerUnavailable)]
        public void ClassifiesKnownFailures(string message, RemoteContentErrorKind expected)
        {
            RemoteContentErrorKind actual = RemoteContentPreloadManager.ClassifyFailure(
                new Exception(message),
                false,
                NetworkReachability.ReachableViaLocalAreaNetwork);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void OfflineTakesPrecedenceOverGenericNetworkError()
        {
            RemoteContentErrorKind actual = RemoteContentPreloadManager.ClassifyFailure(
                new Exception("connection failed"),
                false,
                NetworkReachability.NotReachable);

            Assert.That(actual, Is.EqualTo(RemoteContentErrorKind.Offline));
        }

        [Test]
        public void ExplicitTimeoutTakesPrecedenceOverReachability()
        {
            RemoteContentErrorKind actual = RemoteContentPreloadManager.ClassifyFailure(
                null,
                true,
                NetworkReachability.NotReachable);

            Assert.That(actual, Is.EqualTo(RemoteContentErrorKind.Timeout));
        }

        [TestCase(RemoteContentErrorKind.Offline, true)]
        [TestCase(RemoteContentErrorKind.Timeout, true)]
        [TestCase(RemoteContentErrorKind.ServerUnavailable, true)]
        [TestCase(RemoteContentErrorKind.ResourceMissing, false)]
        [TestCase(RemoteContentErrorKind.StorageFailure, false)]
        public void RetryPolicyMatchesFailureKind(RemoteContentErrorKind kind, bool expected)
        {
            Assert.That(RemoteContentPreloadManager.IsRetryable(kind), Is.EqualTo(expected));
        }
    }
}

namespace VNovelizer.Tests
{
    // These tests use only in-memory operations/settings; no asset or cache deletion.
    public class RemoteContentRegressionTests
    {
        private RemoteContentPreloadManager manager;
        private System.Action<UnityEngine.Networking.UnityWebRequest> previousOverride;
        private const System.Reflection.BindingFlags PrivateInstance =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            previousOverride = UnityEngine.AddressableAssets.Addressables.ResourceManager.WebRequestOverride;
            manager = new RemoteContentPreloadManager();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.AddressableAssets.Addressables.ResourceManager.WebRequestOverride = previousOverride;
        }

        private object Invoke(string name, params object[] args)
        {
            return typeof(RemoteContentPreloadManager).GetMethod(name, PrivateInstance).Invoke(manager, args);
        }

        [TestCase(true, RemoteContentLocationState.NotFound, false, true)]
        [TestCase(false, RemoteContentLocationState.NotFound, false, false)]
        [TestCase(true, RemoteContentLocationState.Found, true, false)]
        [TestCase(false, RemoteContentLocationState.Found, true, false)]
        [TestCase(true, RemoteContentLocationState.Failed, false, true)]
        [TestCase(false, RemoteContentLocationState.Failed, false, true)]
        public void MissingRequiredLocationsFailButAbsentOptionalContentCanBeSkipped(
            bool required, RemoteContentLocationState state, bool usable, bool failed)
        {
            var label = required ? "chapter1_required" : "chapter2_optional";
            Assert.That(Invoke("ValidateLocationState", label, required, state), Is.EqualTo(usable));
            Assert.That(typeof(RemoteContentPreloadManager).GetField("lastSizeQueryFailed", PrivateInstance)
                .GetValue(manager), Is.EqualTo(failed));
            if (required && state == RemoteContentLocationState.NotFound)
            {
                Assert.That(manager.LastOperationResult.Stage, Is.EqualTo(RemoteContentOperationStage.LocationLookup));
                Assert.That(manager.LastErrorKind, Is.EqualTo(RemoteContentErrorKind.ResourceMissing));
                Assert.That(manager.LastOperationResult.Label, Is.EqualTo(label));
            }
        }

        [Test]
        public void CompletedOperationRemainsReadableUntilExplicitRelease()
        {
            using (var resources = new UnityEngine.ResourceManagement.ResourceManager())
            {
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle handle =
                    resources.CreateCompletedOperation<string>("ready", null);
                var wait = (System.Collections.IEnumerator)Invoke("WaitForAddressablesOperation", handle, 20f);
                Assert.That(wait.MoveNext(), Is.False);
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(handle.Status, Is.EqualTo(
                    UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded));
                Invoke("ReleaseIfValid", handle);
                Assert.That(handle.IsValid(), Is.False);
                Assert.DoesNotThrow(() => Invoke("ReleaseIfValid", handle));
            }
        }

        private sealed class PendingOperation :
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase<string>
        {
            protected override void Execute() { }
            public void Finish() { Complete("ready", true, null); }
        }

        [Test]
        public void TimeoutStopsWaitingWithoutReleasingOperationStillInFlight()
        {
            using (var resources = new UnityEngine.ResourceManagement.ResourceManager())
            {
                var operation = new PendingOperation();
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle handle =
                    resources.StartOperation(operation, default);
                var wait = (System.Collections.IEnumerator)Invoke("WaitForAddressablesOperation", handle, 0f);
                Assert.That(wait.MoveNext(), Is.False);
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(handle.IsDone, Is.False);
                operation.Finish();
                Assert.That(handle.Status, Is.EqualTo(
                    UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded));
                Invoke("ReleaseIfValid", handle);
                Assert.That(handle.IsValid(), Is.False);
            }
        }
    }

    public class CdnBuildRegressionTests
    {
        private UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings;
        private UnityEditor.AddressableAssets.Settings.AddressableAssetGroup group;
        private UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema schema;

        [SetUp]
        public void SetUp()
        {
            settings = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create(
                "UnusedInMemoryConfig", "CdnRegression", false, false);
            settings.BuildRemoteCatalog = true;
            settings.CatalogRequestsTimeout = 15;
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", "ServerData/[BuildTarget]");
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath",
                "https://res.huizi888.com/vnovelizer/prod/1.1.3/[BuildTarget]");
            group = settings.CreateGroup("Regression-Remote", false, false, false, null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            schema = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            schema.Timeout = 30;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(schema);
            UnityEngine.Object.DestroyImmediate(group);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private void Validate(string miniGameText = null)
        {
            try
            {
                typeof(CdnBuildValidation).GetMethod("ValidateSettings",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { settings, "1.1.3", miniGameText });
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        [Test]
        public void ProfileVariablesAreExpandedBeforeComparingPaths()
        {
            Assert.DoesNotThrow(() => Validate());
        }

        [Test]
        public void LocalPathIsRejectedWithExpectedAndActualPaths()
        {
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            var error = Assert.Throws<UnityEditor.Build.BuildFailedException>(() => Validate());
            StringAssert.Contains("Regression-Remote", error.Message);
            StringAssert.Contains("expected", error.Message);
            StringAssert.Contains("actual", error.Message);
        }

        [Test]
        public void VersionMismatchIsRejected()
        {
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath",
                "https://res.huizi888.com/vnovelizer/prod/0.0.0/[BuildTarget]");
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => Validate());
        }

        [Test]
        public void MissingRequestTimeoutIsRejected()
        {
            schema.Timeout = 0;
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => Validate());
        }

        [Test]
        public void DisabledRemoteCatalogIsRejected()
        {
            settings.BuildRemoteCatalog = false;
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => Validate());
        }

        [Test]
        public void MiniGameVersionMismatchIsRejected()
        {
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() =>
                Validate("CDN: https://res.huizi888.com/vnovelizer/prod/0.0.0/WebGL/"));
        }
    }
}