using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    public sealed partial class PhysicsApplySystem
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBuoyancyForceQueue()
        {
            ShutdownBuoyancyForceQueue();
        }

        internal static bool TryPrepareBuoyancyForcePackets(
            NativeArray<BuoyancyForcePacketDTO> packets,
            NativeArray<BuoyancyCounterDTO> counters)
        {
            if (!packets.IsCreated ||
                packets.Length <= 0 ||
                !counters.IsCreated ||
                counters.Length <= 0)
            {
                return false;
            }

            BuoyancyCounterDTO counter = counters[0];
            counter.ForcePackets = 0;
            counter.Flags &= ~BuoyancyDisplacementConstants.FlagForcePacketOverflow;
            counters[0] = counter;
            return true;
        }

        internal static void DrainBuoyancyForcePackets(
            NativeArray<BuoyancyForcePacketDTO> packets,
            NativeArray<BuoyancyCounterDTO> counters,
            NativeArray<BuoyancyBodyBindingDTO> bodyBindings,
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

            PhysicsApplySystem system = EnsureRuntimeInstance();
            GlobalPhysicsStateManager.TryGetBuoyancyBodyResolver(out GlobalPhysicsStateManager bodyResolver);
            int packetCount = math.clamp(counters[0].ForcePackets, 0, packets.Length);
            int budget = math.min(math.max(0, maxPackets), packetCount);
            if (system == null || bodyResolver == null)
            {
                unresolved = budget;
                return;
            }

            for (int i = 0; i < budget; i++)
            {
                BuoyancyForcePacketDTO packet = packets[i];
                if (packet.EntityHashID == 0u ||
                    !math.all(math.isfinite(packet.NetForce)) ||
                    !EnsureBuoyancyBodyBinding(packet, bodyBindings, bodyResolver, out Rigidbody body))
                {
                    unresolved++;
                    continue;
                }

                Vector3 force = new Vector3(packet.NetForce.x, packet.NetForce.y, packet.NetForce.z);
                if (system.QueueForce(
                        body,
                        force,
                        ForceMode.Force,
                        ForcePacketPriority.Ambient,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
            }
        }

        private static bool EnsureBuoyancyBodyBinding(
            BuoyancyForcePacketDTO packet,
            NativeArray<BuoyancyBodyBindingDTO> bodyBindings,
            GlobalPhysicsStateManager bodyResolver,
            out Rigidbody body)
        {
            body = null;
            if (bodyResolver == null)
                return false;

            int stateIndex = packet.StateIndex;
            if (bodyBindings.IsCreated && (uint)stateIndex < (uint)bodyBindings.Length)
            {
                BuoyancyBodyBindingDTO binding = bodyBindings[stateIndex];
                bool bindingMatches = binding.EntityHashID == packet.EntityHashID &&
                                      binding.StateIndex == stateIndex &&
                                      (binding.Flags & BuoyancyDisplacementConstants.FlagActive) != 0u;
                if (bindingMatches &&
                    GlobalPhysicsStateManager.TryResolveTrackedBodyByIndex(
                        bodyResolver,
                        binding.RigidbodyIndex,
                        packet.EntityHashID,
                        out body))
                {
                    return true;
                }
            }

            if (!GlobalPhysicsStateManager.TryFindTrackedBodyByFoldedEntityHash(
                    bodyResolver,
                    packet.EntityHashID,
                    out body,
                    out int bodyIndex))
            {
                return false;
            }

            if (bodyBindings.IsCreated && (uint)stateIndex < (uint)bodyBindings.Length)
            {
                BuoyancyBodyBindingDTO binding = default;
                binding.EntityHashID = packet.EntityHashID;
                binding.StateIndex = stateIndex;
                binding.RigidbodyIndex = bodyIndex;
                binding.Flags = BuoyancyDisplacementConstants.FlagActive;
                bodyBindings[stateIndex] = binding;
            }

            return true;
        }

        internal static void ShutdownBuoyancyForceQueue()
        {
            // Force packet lifetime is owned by GlobalDataVault.
        }
    }
}
