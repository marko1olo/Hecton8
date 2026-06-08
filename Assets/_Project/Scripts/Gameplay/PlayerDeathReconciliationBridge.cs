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
        private static int s_x001DirectSignalPushDropCount_PlayerDeathReconciliationBridge;

        private const uint DefaultPlayerHash = 0x504C5952u; // PLYR
        private static uint s_sequence;
        private static bool s_lanesConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_x001DirectSignalPushDropCount_PlayerDeathReconciliationBridge = 0;
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
            return RequestRespawn(deathAup, damageHash, DefaultPlayerHash, out _);
        }

        internal static bool RequestRespawn(double3 deathAup, uint damageHash, uint playerHash)
        {
            return RequestRespawn(deathAup, damageHash, playerHash, out _);
        }

        internal static bool RequestRespawn(double3 deathAup, uint damageHash, uint playerHash, out uint sequence)
        {
            ConfigureSignalLanes();
            sequence = 0u;

            uint nextSequence = ++s_sequence;
            if (nextSequence == 0u)
                nextSequence = ++s_sequence;
            sequence = nextSequence;

            bool deathAupFinite = math.all(math.isfinite(deathAup));
            double3 safeDeathAup = deathAupFinite ? deathAup : DefaultFallbackAup();

            PlayerRespawnSignal signal = default;
            signal.DeathAUP = safeDeathAup;
            signal.RespawnAUP = signal.DeathAUP;
            signal.PlayerHash = playerHash != 0u ? playerHash : DefaultPlayerHash;
            signal.DamageHash = damageHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Sequence = nextSequence;
            signal.Flags = PlayerRespawnSignalFlags.Requested | PlayerRespawnSignalFlags.SuspendCollision;
            if (!deathAupFinite)
                signal.Flags |= PlayerRespawnSignalFlags.InvalidDeathAup | PlayerRespawnSignalFlags.InvalidTargetAup;
            signal.Phase = PlayerRespawnSignalPhase.Request;
            signal.SuspendCollisionFrames = 1;

            bool pushed = SignalBus<PlayerRespawnSignal>.TryPushTracked(
                in signal,
                ref s_x001DirectSignalPushDropCount_PlayerDeathReconciliationBridge);
            if (!pushed)
            {
                ConfigureSignalLanes();
                pushed = SignalBus<PlayerRespawnSignal>.TryPushTracked(
                    in signal,
                    ref s_x001DirectSignalPushDropCount_PlayerDeathReconciliationBridge);
            }

            return pushed;
        }

        internal static bool IsAcceptedCommittedRespawnSignal(
            in PlayerRespawnSignal signal,
            uint expectedSequence,
            uint playerHash)
        {
            if (expectedSequence == 0u ||
                signal.Sequence != expectedSequence ||
                signal.Phase != PlayerRespawnSignalPhase.Committed)
            {
                return false;
            }

            uint flags = signal.Flags;
            if ((flags & PlayerRespawnSignalFlags.Committed) == 0u ||
                !math.all(math.isfinite(signal.DeathAUP)) ||
                !math.all(math.isfinite(signal.RespawnAUP)))
            {
                return false;
            }

            return playerHash == 0u || signal.PlayerHash == 0u || signal.PlayerHash == playerHash;
        }

        private static void ConfigureSignalLanes()
        {
            if (s_lanesConfigured && SignalBus<PlayerRespawnSignal>.HasNativeStorage)
                return;

            SignalBus<PlayerRespawnSignal>.Configure(
                PlayerRespawnSignal.ExpectedCapacity,
                maxFrameSignals: PlayerRespawnSignal.MaxFrameSignals,
                lowTierFrameSignals: PlayerRespawnSignal.LowTierFrameSignals,
                laneHash: PlayerRespawnSignal.LaneHash);
            SignalBus<PlayerRespawnSignal>.EnsureInitialized();
            s_lanesConfigured = SignalBus<PlayerRespawnSignal>.HasNativeStorage;
        }

        private static double3 DefaultFallbackAup()
        {
            double3 fallback = default;
            fallback.y = -18d;
            return fallback;
        }
    }
}
