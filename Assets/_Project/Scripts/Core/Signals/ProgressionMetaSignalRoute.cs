using System.Threading;
using Hecton8.Core.Contracts.Signals;
using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Hash-only first-party route for meta progression signals that still need a managed mod bridge.
    /// </summary>
    public static class ProgressionMetaSignalRoute
    {
        private static int _sequence;
        private static int s_x001ProgressionMetaSignalRouteSignalPushDropCount;

        [Obsolete("Use TryPublishAchievementUnlocked(uint) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishAchievementUnlocked(uint achievementHash)
        {
            TryPublishAchievementUnlocked(achievementHash);
        }

        public static bool TryPublishAchievementUnlocked(uint achievementHash)
        {
            return TryPublish(ProgressionMetaSignal.KindAchievementUnlocked, achievementHash, 0u);
        }

        [Obsolete("Use TryPublishAdvisoryIssued(uint) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishAdvisoryIssued(uint advisoryHash)
        {
            TryPublishAdvisoryIssued(advisoryHash);
        }

        public static bool TryPublishAdvisoryIssued(uint advisoryHash)
        {
            return TryPublish(ProgressionMetaSignal.KindAdvisoryIssued, advisoryHash, 0u);
        }

        [Obsolete("Use TryPublishBiomeDiscovered(int) so overflow/drop semantics stay visible at the producer.", true)]
        public static void PublishBiomeDiscovered(int biomeId)
        {
            TryPublishBiomeDiscovered(biomeId);
        }

        public static bool TryPublishBiomeDiscovered(int biomeId)
        {
            if (biomeId <= 0)
                return false;

            uint biomeHash = unchecked(0xB10C0000u ^ (uint)biomeId * 16777619u);
            return TryPublish(ProgressionMetaSignal.KindBiomeDiscovered, biomeHash, unchecked((uint)biomeId));
        }

        private static bool TryPublish(byte kind, uint eventHash, uint contextHash)
        {
            if (kind == 0 || eventHash == 0u)
                return false;

            SignalCorridorRuntime.EnsureInitialized();
            ProgressionMetaSignal signal = new ProgressionMetaSignal
            {
                EventHash = eventHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = unchecked((uint)Interlocked.Increment(ref _sequence)),
                Kind = kind,
                ContextHash = contextHash
            };

            return SignalBus<ProgressionMetaSignal>.TryPushTracked(in signal, ref s_x001ProgressionMetaSignalRouteSignalPushDropCount);
        }
    }
}
