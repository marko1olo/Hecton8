using UnityEngine;

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

        public static int SlotCapacity => Capacity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            for (int i = 0; i < Capacity; i++)
            {
                s_owners[i] = null;
                s_behaviours[i] = null;
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
            }
        }

        public static bool TryGetAt(
            int slot,
            out IPlayerTransportLifecycleOwner owner,
            out MonoBehaviour behaviour)
        {
            owner = null;
            behaviour = null;
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

            return true;
        }
    }
}
