using Hecton8.Core;
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
            for (int i = 0; i < packets.Length; i++)
                packets[i] = default;
            return true;
        }

        internal static void DrainSeaglideForcePackets(
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

            PhysicsApplySystem system = EnsureRuntimeInstance();
            GlobalPhysicsStateManager.TryGetBuoyancyBodyResolver(out GlobalPhysicsStateManager bodyResolver);
            int packetCount = math.clamp(counters[0].ForcePackets, 0, packets.Length);
            int budget = math.min(math.max(0, maxPackets), packetCount);
            for (int i = 0; i < budget; i++)
            {
                SeaglideForcePacketDTO packet = packets[i];
                if (system == null ||
                    packet.TargetEntityHash == 0u ||
                    !math.all(math.isfinite(packet.NetForce)) ||
                    !TryResolveSeaglideBody(packet, bodyBindings, bodyResolver, out Rigidbody body))
                {
                    unresolved++;
                    continue;
                }

                Vector3 force = new Vector3(packet.NetForce.x, packet.NetForce.y, packet.NetForce.z);
                if (system.QueueForce(
                        body,
                        force,
                        ForceMode.Force,
                        ForcePacketPriority.Critical,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
            }
        }

        private static bool TryResolveSeaglideBody(
            SeaglideForcePacketDTO packet,
            NativeArray<SeaglideBodyBindingDTO> bodyBindings,
            GlobalPhysicsStateManager bodyResolver,
            out Rigidbody body)
        {
            body = null;
            int stateIndex = packet.StateIndex;
            if (bodyResolver != null &&
                bodyBindings.IsCreated &&
                (uint)stateIndex < (uint)bodyBindings.Length)
            {
                SeaglideBodyBindingDTO binding = bodyBindings[stateIndex];
                bool bindingMatches = binding.TargetEntityHash == packet.TargetEntityHash &&
                                      binding.StateIndex == stateIndex &&
                                      binding.RigidbodyIndex >= 0 &&
                                      (binding.Flags & SeaglideHydrodynamicsConstants.FlagActive) != 0u;
                if (bindingMatches &&
                    GlobalPhysicsStateManager.TryResolveTrackedBodyByIndex(
                        bodyResolver,
                        binding.RigidbodyIndex,
                        packet.TargetEntityHash,
                        out body))
                {
                    return true;
                }
            }

            if (bodyResolver != null &&
                GlobalPhysicsStateManager.TryResolveTrackedBodyByFoldedEntityHash(
                    bodyResolver,
                    packet.TargetEntityHash,
                    out body,
                    out int bodyIndex))
            {
                if (bodyBindings.IsCreated && (uint)stateIndex < (uint)bodyBindings.Length)
                {
                    SeaglideBodyBindingDTO binding = default;
                    binding.TargetEntityHash = packet.TargetEntityHash;
                    binding.StateIndex = stateIndex;
                    binding.RigidbodyIndex = bodyIndex;
                    binding.Flags = SeaglideHydrodynamicsConstants.FlagActive;
                    bodyBindings[stateIndex] = binding;
                }

                return true;
            }

            if (packet.TargetEntityHash == SeaglideHydrodynamicsConstants.PlayerBodyTargetHash &&
                PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext.PlayerRigidbody != null)
            {
                body = runtimeContext.PlayerRigidbody;
                return true;
            }

            return false;
        }

        internal static void ShutdownSeaglideForceQueue()
        {
            // Force packet lifetime is owned by GlobalDataVault.
        }
    }
}
