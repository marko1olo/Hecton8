using System;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public sealed partial class GlobalPhysicsStateManager
    {
        private static int s_x001DirectSignalPushDropCount_GlobalPhysicsStateManager_WakeRequests;

        private const int PhysicsWakeRequestFlushLimit = 16;

        private bool QueuePhysicsWakeRequest(in WakeRequestSignal request)
        {
            return SignalBus<WakeRequestSignal>.TryPushTracked(in request, ref s_x001DirectSignalPushDropCount_GlobalPhysicsStateManager_WakeRequests);
        }

        private void FlushPhysicsWakeRequests()
        {
            ReadOnlySpan<WakeRequestSignal> requests = SignalBus<WakeRequestSignal>.GetSignals();
            int count = math.min(requests.Length, PhysicsWakeRequestFlushLimit);
            for (int i = 0; i < count; i++)
            {
                WakeRequestSignal request = requests[i];
                if (!math.all(math.isfinite(request.OriginAup)) ||
                    !math.isfinite(request.RadiusMeters) ||
                    request.RadiusMeters <= 0f)
                {
                    continue;
                }

                AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(request.OriginAup);
                float radiusMeters = math.min(request.RadiusMeters, AcousticWakeMaximumRadiusMeters);
                WakeCulledBodiesNear(in originAup, radiusMeters);
            }
        }
    }
}
