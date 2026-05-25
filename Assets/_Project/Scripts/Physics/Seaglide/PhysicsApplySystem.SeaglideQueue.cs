using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    public sealed partial class PhysicsApplySystem
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSeaglideForceQueue()
        {
            ShutdownSeaglideForceQueue();
        }

        internal static bool TryPrepareSeaglideForcePackets(
            NativeArray<SeaglideForcePacketDTO> packets,
            NativeArray<SeaglideCounterDTO> counters)
        {
            if (!packets.IsCreated ||
                packets.Length <= 0 ||
                !counters.IsCreated ||
                counters.Length <= 0)
            {
                return false;
            }

            SeaglideCounterDTO counter = counters[0];
            counter.ForcePackets = 0;
            counter.Flags &= ~SeaglideHydrodynamicsConstants.FlagPacketOverflow;
            counters[0] = counter;
            return true;
        }

        internal static void DrainSeaglideForcePackets(
            IPhysicsService system,
            GlobalPhysicsStateManager bodyResolver,
            NativeArray<SeaglideForcePacketDTO> packets,
            NativeArray<SeaglideCounterDTO> counters,
            NativeArray<SeaglideBodyBindingDTO> bodyBindings,
            int maxPackets,
            out int accepted,
            out int unresolved)
        {
            accepted = 0;
            unresolved = 0;
            if (!packets.IsCreated ||
                !counters.IsCreated ||
                counters.Length <= 0 ||
                packets.Length <= 0)
            {
                return;
            }

            SeaglideCounterDTO counter = counters[0];
            int packetCount = math.clamp(counter.ForcePackets, 0, packets.Length);
            int scanWindow = math.clamp(counter.EvaluatedRequests, 0, packets.Length);
            if (scanWindow <= 0)
                scanWindow = packetCount;

            int budget = math.min(math.max(0, maxPackets), packetCount);
            int attempted = 0;
            for (int i = 0; i < scanWindow && attempted < budget; i++)
            {
                SeaglideForcePacketDTO packet = packets[i];
                if (packet.TargetEntityHash == 0u ||
                    (packet.Flags & SeaglideHydrodynamicsConstants.FlagForceQueued) == 0u ||
                    !math.all(math.isfinite(packet.NetForce)))
                {
                    continue;
                }

                attempted++;
                if (system == null || !TryResolveBoundSeaglideBodyForPacket(packet, bodyBindings, bodyResolver, out Rigidbody body))
                {
                    unresolved++;
                    continue;
                }

                Vector3 force = new Vector3(packet.NetForce.x, packet.NetForce.y, packet.NetForce.z);
                if (system.QueueForce(
                        body,
                        force,
                        ForceMode.Force,
                        wake: true))
                {
                    accepted++;
                }
            }
        }

        private static bool TryResolveBoundSeaglideBodyForPacket(
            SeaglideForcePacketDTO packet,
            NativeArray<SeaglideBodyBindingDTO> bodyBindings,
            GlobalPhysicsStateManager bodyResolver,
            out Rigidbody body)
        {
            body = null;
            int stateIndex = packet.StateIndex;
            if (bodyResolver == null ||
                !bodyBindings.IsCreated ||
                (uint)stateIndex >= (uint)bodyBindings.Length)
            {
                return false;
            }

            SeaglideBodyBindingDTO binding = bodyBindings[stateIndex];
            bool bindingMatches = binding.TargetEntityHash == packet.TargetEntityHash &&
                                  binding.StateIndex == stateIndex &&
                                  binding.RigidbodyIndex >= 0 &&
                                  (binding.Flags & SeaglideHydrodynamicsConstants.FlagActive) != 0u;
            return bindingMatches &&
                   GlobalPhysicsStateManager.TryResolveTrackedBodyByIndex(
                       bodyResolver,
                       binding.RigidbodyIndex,
                       packet.TargetEntityHash,
                       out body);
        }

        internal static void ShutdownSeaglideForceQueue()
        {
            // Force packet lifetime is owned by GlobalDataVault.
        }
    }
}
