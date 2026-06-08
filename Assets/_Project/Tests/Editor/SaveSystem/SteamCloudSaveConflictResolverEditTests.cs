using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SteamCloudSaveConflictResolverEditTests
    {
        [Test]
        public void Resolve_PrefersOlderReadableLocalOverNewerUnreadableCloud()
        {
            SteamCloudSaveCandidate local = new SteamCloudSaveCandidate(1000UL, 120f);
            SteamCloudSaveCandidate cloud = new SteamCloudSaveCandidate(
                2000UL,
                240f,
                isReadable: false,
                integrityVerified: false);

            SteamCloudSaveResolution resolution = SteamCloudSaveConflictResolver.Resolve(in local, in cloud);

            Assert.AreEqual(SteamCloudSaveChoice.Local, resolution.SuggestedChoice);
            Assert.AreEqual(SteamCloudSaveChoice.None, resolution.AlternateChoice);
        }

        [Test]
        public void Resolve_PrefersOlderReadableLocalOverNewerChecksumFailedCloud()
        {
            SteamCloudSaveCandidate local = new SteamCloudSaveCandidate(1000UL, 120f);
            SteamCloudSaveCandidate cloud = new SteamCloudSaveCandidate(
                2000UL,
                240f,
                isReadable: true,
                integrityVerified: false);

            SteamCloudSaveResolution resolution = SteamCloudSaveConflictResolver.Resolve(in local, in cloud);

            Assert.AreEqual(SteamCloudSaveChoice.Local, resolution.SuggestedChoice);
            Assert.AreEqual(SteamCloudSaveChoice.None, resolution.AlternateChoice);
        }

        [Test]
        public void Resolve_PrefersReadableCloudWhenLocalIsUnreadable()
        {
            SteamCloudSaveCandidate local = new SteamCloudSaveCandidate(
                2000UL,
                240f,
                isReadable: false,
                integrityVerified: false);
            SteamCloudSaveCandidate cloud = new SteamCloudSaveCandidate(1000UL, 120f);

            SteamCloudSaveResolution resolution = SteamCloudSaveConflictResolver.Resolve(in local, in cloud);

            Assert.AreEqual(SteamCloudSaveChoice.Cloud, resolution.SuggestedChoice);
            Assert.AreEqual(SteamCloudSaveChoice.None, resolution.AlternateChoice);
        }

        [Test]
        public void Resolve_FallsBackToLocalAndNoAlternateWhenBothCandidatesAreInvalid()
        {
            SteamCloudSaveCandidate local = new SteamCloudSaveCandidate(
                1000UL,
                120f,
                isReadable: false,
                integrityVerified: false);
            SteamCloudSaveCandidate cloud = new SteamCloudSaveCandidate(
                2000UL,
                240f,
                isReadable: false,
                integrityVerified: false);

            SteamCloudSaveResolution resolution = SteamCloudSaveConflictResolver.Resolve(in local, in cloud);

            Assert.AreEqual(SteamCloudSaveChoice.Local, resolution.SuggestedChoice);
            Assert.AreEqual(SteamCloudSaveChoice.None, resolution.AlternateChoice);
        }

        [Test]
        public void Resolve_UsesTimestampAndPlaytimeWhenBothCandidatesAreUsable()
        {
            SteamCloudSaveCandidate local = new SteamCloudSaveCandidate(1000UL, 400f);
            SteamCloudSaveCandidate cloud = new SteamCloudSaveCandidate(2000UL, 120f);

            SteamCloudSaveResolution timestampResolution = SteamCloudSaveConflictResolver.Resolve(in local, in cloud);

            Assert.AreEqual(SteamCloudSaveChoice.Cloud, timestampResolution.SuggestedChoice);
            Assert.AreEqual(SteamCloudSaveChoice.Local, timestampResolution.AlternateChoice);

            SteamCloudSaveCandidate equalTimeLocal = new SteamCloudSaveCandidate(3000UL, 400f);
            SteamCloudSaveCandidate equalTimeCloud = new SteamCloudSaveCandidate(3000UL, 500f);

            SteamCloudSaveResolution playtimeResolution = SteamCloudSaveConflictResolver.Resolve(
                in equalTimeLocal,
                in equalTimeCloud);

            Assert.AreEqual(SteamCloudSaveChoice.Cloud, playtimeResolution.SuggestedChoice);
        }
    }
}
