using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Fatal-damage seam: emits only contract signals, never scene reloads.
    /// </summary>
    internal static class PlayerDeathReconciliationBridge
    {
        private const uint DefaultPlayerHash = 0x504C5952u; // PLYR
        private static uint s_sequence;
        private static bool s_lanesConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_sequence = 0u;
            s_lanesConfigured = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureSignalLaneOnBoot()
        {
            ConfigureSignalLanes();
        }

        internal static bool RequestRespawn(double3 deathAup, uint damageHash)
        {
            if (!math.all(math.isfinite(deathAup)))
                return false;

            ConfigureSignalLanes();

            uint sequence = ++s_sequence;
            if (sequence == 0u)
                sequence = ++s_sequence;

            PlayerRespawnSignal signal = default;
            signal.DeathAUP = deathAup;
            signal.RespawnAUP = signal.DeathAUP;
            signal.PlayerHash = DefaultPlayerHash;
            signal.DamageHash = damageHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Sequence = sequence;
            signal.Flags = PlayerRespawnSignalFlags.Requested | PlayerRespawnSignalFlags.SuspendCollision;
            signal.Phase = PlayerRespawnSignalPhase.Request;
            signal.SuspendCollisionFrames = 1;

            return SignalBus<PlayerRespawnSignal>.TryPush(in signal);
        }

        private static void ConfigureSignalLanes()
        {
            if (s_lanesConfigured)
                return;

            SignalBus<PlayerRespawnSignal>.Configure(
                PlayerRespawnSignal.ExpectedCapacity,
                maxFrameSignals: PlayerRespawnSignal.MaxFrameSignals,
                lowTierFrameSignals: PlayerRespawnSignal.LowTierFrameSignals,
                laneHash: PlayerRespawnSignal.LaneHash);
            SignalBus<PlayerRespawnSignal>.EnsureInitialized();
            s_lanesConfigured = true;
        }
    }
}
