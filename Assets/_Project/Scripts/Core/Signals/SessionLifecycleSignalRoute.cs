using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Hash-only first-party route for save-load and player-spawn lifecycle notifications.
    /// Managed lifecycle events remain a mod/API bridge only.
    /// </summary>
    public static class SessionLifecycleSignalRoute
    {
        private static int s_sequence;
        private static int s_x001SessionLifecycleSignalRouteSignalPushDropCount;

        public static bool PublishGameLoadedHash(uint slotHash)
        {
            return Publish(SessionLifecycleSignal.KindGameLoaded, 0ul, float3.zero, slotHash);
        }

        public static bool PublishPlayerSpawned(ulong playerEntityId, Vector3 playerPosition)
        {
            if (playerEntityId == 0ul)
                return false;

            return Publish(
                SessionLifecycleSignal.KindPlayerSpawned,
                playerEntityId,
                new float3(playerPosition.x, playerPosition.y, playerPosition.z),
                0u);
        }

        private static bool Publish(byte kind, ulong playerEntityId, float3 playerPosition, uint slotHash)
        {
            if (kind == 0)
                return false;

            SignalCorridorRuntime.EnsureInitialized();
            SessionLifecycleSignal signal = new SessionLifecycleSignal
            {
                PlayerEntityId = playerEntityId,
                PlayerPosition = math.all(math.isfinite(playerPosition)) ? playerPosition : float3.zero,
                SlotHash = slotHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = unchecked((uint)Interlocked.Increment(ref s_sequence)),
                Kind = kind
            };

            return SignalBus<SessionLifecycleSignal>.TryPushTracked(in signal, ref s_x001SessionLifecycleSignalRouteSignalPushDropCount);
        }
    }
}
