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
