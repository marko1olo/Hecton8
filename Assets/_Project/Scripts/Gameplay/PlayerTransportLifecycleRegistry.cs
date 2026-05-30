using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Fixed-capacity cold registry for transport lifecycle owners that must be discovered without PhysX trigger callbacks.
    /// </summary>
    public static class PlayerTransportLifecycleRegistry
    {
        private const int Capacity = 64;

        private static readonly IPlayerTransportLifecycleOwner[] s_owners = new IPlayerTransportLifecycleOwner[Capacity];
        private static readonly MonoBehaviour[] s_behaviours = new MonoBehaviour[Capacity];
        private static readonly Rigidbody[] s_bodies = new Rigidbody[Capacity];
        private static readonly VehicleMotor[] s_vehicleMotors = new VehicleMotor[Capacity];
        private static readonly VehicleUpgradeModule[] s_vehicleUpgradeModules = new VehicleUpgradeModule[Capacity];
        private static readonly ITransportDockControlLock[] s_dockControlLocks = new ITransportDockControlLock[Capacity];
        private static readonly IDockedExternalMassSink[] s_externalMassSinks = new IDockedExternalMassSink[Capacity];

        public static int SlotCapacity => Capacity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            for (int i = 0; i < Capacity; i++)
            {
                s_owners[i] = null;
                s_behaviours[i] = null;
                s_bodies[i] = null;
                s_vehicleMotors[i] = null;
                s_vehicleUpgradeModules[i] = null;
                s_dockControlLocks[i] = null;
                s_externalMassSinks[i] = null;
            }
        }

        public static bool Register(IPlayerTransportLifecycleOwner owner, MonoBehaviour behaviour)
        {
            if (owner == null || behaviour == null)
                return false;

            int freeSlot = -1;
            for (int i = 0; i < Capacity; i++)
            {
                MonoBehaviour existingBehaviour = s_behaviours[i];
                IPlayerTransportLifecycleOwner existingOwner = s_owners[i];
                if (ReferenceEquals(existingOwner, owner) || ReferenceEquals(existingBehaviour, behaviour))
                    return true;

                if (freeSlot < 0 && (existingOwner == null ||
                                     existingBehaviour == null ||
                                     !existingBehaviour.gameObject.activeInHierarchy))
                    freeSlot = i;
            }

            if (freeSlot < 0)
                return false;

            s_owners[freeSlot] = owner;
            s_behaviours[freeSlot] = behaviour;
            CacheTransportRuntimeReferences(
                owner,
                behaviour,
                out s_bodies[freeSlot],
                out s_vehicleMotors[freeSlot],
                out s_vehicleUpgradeModules[freeSlot],
                out s_dockControlLocks[freeSlot],
                out s_externalMassSinks[freeSlot]);
            return true;
        }

        public static void Unregister(IPlayerTransportLifecycleOwner owner, MonoBehaviour behaviour)
        {
            if (owner == null && behaviour == null)
                return;

            for (int i = 0; i < Capacity; i++)
            {
                bool ownerMatches = owner != null && ReferenceEquals(s_owners[i], owner);
                bool behaviourMatches = behaviour != null && ReferenceEquals(s_behaviours[i], behaviour);
                if (!ownerMatches && !behaviourMatches)
                    continue;

                s_owners[i] = null;
                s_behaviours[i] = null;
                s_bodies[i] = null;
                s_vehicleMotors[i] = null;
                s_vehicleUpgradeModules[i] = null;
                s_dockControlLocks[i] = null;
                s_externalMassSinks[i] = null;
            }
        }

        public static bool TryGetAt(
            int slot,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour)
        {
            return TryGetAt(slot, out owner, out behaviour, out _, out _, out _);
        }

        public static bool TryGetAt(
            int slot,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out ITransportDockControlLock dockControlLock,
            out IDockedExternalMassSink externalMassSink)
        {
            return TryGetAt(slot, out owner, out behaviour, out body, out vehicleMotor, out _, out dockControlLock, out externalMassSink);
        }

        public static bool TryGetAt(
            int slot,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out VehicleUpgradeModule vehicleUpgradeModule,
            out ITransportDockControlLock dockControlLock,
            out IDockedExternalMassSink externalMassSink)
        {
            owner = null;
            behaviour = null;
            body = null;
            vehicleMotor = null;
            vehicleUpgradeModule = null;
            dockControlLock = null;
            externalMassSink = null;
            if (slot < 0 || slot >= Capacity)
                return false;

            owner = s_owners[slot];
            behaviour = s_behaviours[slot];
            if (owner == null || behaviour == null || !behaviour.gameObject.activeInHierarchy)
            {
                owner = null;
                behaviour = null;
                return false;
            }

            body = s_bodies[slot];
            vehicleMotor = s_vehicleMotors[slot];
            vehicleUpgradeModule = s_vehicleUpgradeModules[slot];
            dockControlLock = s_dockControlLocks[slot];
            externalMassSink = s_externalMassSinks[slot];
            return true;
        }

        public static bool TryGetRegistered(
            IPlayerTransportLifecycleOwner requestedOwner,
            MonoBehaviour requestedBehaviour,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out ITransportDockControlLock dockControlLock,
            out IDockedExternalMassSink externalMassSink)
        {
            return TryGetRegistered(
                requestedOwner,
                requestedBehaviour,
                out owner,
                out behaviour,
                out body,
                out vehicleMotor,
                out _,
                out dockControlLock,
                out externalMassSink);
        }

        public static bool TryGetRegistered(
            IPlayerTransportLifecycleOwner requestedOwner,
            MonoBehaviour requestedBehaviour,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out VehicleUpgradeModule vehicleUpgradeModule,
            out ITransportDockControlLock dockControlLock,
            out IDockedExternalMassSink externalMassSink)
        {
            owner = null;
            behaviour = null;
            body = null;
            vehicleMotor = null;
            vehicleUpgradeModule = null;
            dockControlLock = null;
            externalMassSink = null;
            if (requestedOwner == null && requestedBehaviour == null)
                return false;

            for (int i = 0; i < Capacity; i++)
            {
                bool ownerMatches = requestedOwner != null && ReferenceEquals(s_owners[i], requestedOwner);
                bool behaviourMatches = requestedBehaviour != null && ReferenceEquals(s_behaviours[i], requestedBehaviour);
                if (!ownerMatches && !behaviourMatches)
                    continue;

                return TryGetAt(
                    i,
                    out owner,
                    out behaviour,
                    out body,
                    out vehicleMotor,
                    out vehicleUpgradeModule,
                    out dockControlLock,
                    out externalMassSink);
            }

            return false;
        }

        public static bool TryGetAt(
            int slot,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out ITransportDockControlLock dockControlLock)
        {
            return TryGetAt(slot, out owner, out behaviour, out body, out vehicleMotor, out dockControlLock, out _);
        }

        private static void CacheTransportRuntimeReferences(
            IPlayerTransportLifecycleOwner owner,
            MonoBehaviour behaviour,
            out Rigidbody body,
            out VehicleMotor vehicleMotor,
            out VehicleUpgradeModule vehicleUpgradeModule,
            out ITransportDockControlLock dockControlLock,
            out IDockedExternalMassSink externalMassSink)
        {
            body = null;
            vehicleMotor = null;
            vehicleUpgradeModule = null;
            dockControlLock = null;
            externalMassSink = null;

            if (owner is ITransportPredictiveVoxelProxySource predictiveProxy)
                predictiveProxy.TryResolvePredictiveVoxelProxy(out body, out _);

            if (body == null && behaviour != null)
                behaviour.TryGetComponent(out body);

            if (behaviour != null)
                behaviour.TryGetComponent(out vehicleMotor);

            if (behaviour != null)
                behaviour.TryGetComponent(out vehicleUpgradeModule);

            dockControlLock = owner as ITransportDockControlLock;
            if (dockControlLock == null && behaviour != null)
                behaviour.TryGetComponent(out dockControlLock);

            externalMassSink = owner as IDockedExternalMassSink;
            if (externalMassSink == null && behaviour != null)
                behaviour.TryGetComponent(out externalMassSink);
        }
    }
}
