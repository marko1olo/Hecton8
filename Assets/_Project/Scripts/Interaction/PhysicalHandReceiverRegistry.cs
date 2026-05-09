namespace Hecton8.Interaction
{
    using UnityEngine;

    /// <summary>
    /// Shared collider-to-hand-receiver table for physical cockpit controls.
    /// </summary>
    internal static class PhysicalHandReceiverRegistry
    {
        private const int MaxReceivers = 128;
        private const int ReceiverCacheMask = MaxReceivers - 1;
        private const byte CacheSlotEmpty = 0;
        private const byte CacheSlotOccupied = 1;

        // COLD ALLOC: ulong[128] - fixed collider entity id keys for physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly ulong[] s_receiverKeys = new ulong[MaxReceivers];
        // COLD ALLOC: Collider[128] - fixed collider refs for identity validation in physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly Collider[] s_receiverColliders = new Collider[MaxReceivers];
        // COLD ALLOC: IPhysicalPanelButtonReceiver[128] - fixed physical hand receiver values - owner: PhysicalHandReceiverRegistry
        private static readonly IPhysicalPanelButtonReceiver[] s_receivers = new IPhysicalPanelButtonReceiver[MaxReceivers];
        // COLD ALLOC: byte[128] - fixed open-address slot states for physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly byte[] s_receiverStates = new byte[MaxReceivers];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_saturationLogged;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < MaxReceivers; i++)
            {
                s_receiverKeys[i] = 0UL;
                s_receiverColliders[i] = null;
                s_receivers[i] = null;
                s_receiverStates[i] = CacheSlotEmpty;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_saturationLogged = false;
#endif
        }

        public static void Register(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null || receiver == null)
                return;

            WriteReceiver(collider, receiver);
        }

        public static void Unregister(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null)
                return;

            RemoveReceiver(collider, receiver);
        }

        public static bool TryResolve(Collider collider, out IPhysicalPanelButtonReceiver receiver)
        {
            receiver = null;
            if (collider == null)
                return false;

            ulong key = EntityId.ToULong(collider.GetEntityId());
            int index = HashKey(key);
            for (int probe = 0; probe < MaxReceivers; probe++)
            {
                byte state = s_receiverStates[index];
                if (state == CacheSlotEmpty)
                    return false;

                if (state == CacheSlotOccupied &&
                    s_receiverKeys[index] == key &&
                    ReferenceEquals(s_receiverColliders[index], collider))
                {
                    receiver = s_receivers[index];
                    return receiver != null;
                }

                index = (index + 1) & ReceiverCacheMask;
            }

            return false;
        }

        private static void WriteReceiver(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            ulong key = EntityId.ToULong(collider.GetEntityId());
            int index = HashKey(key);
            for (int probe = 0; probe < MaxReceivers; probe++)
            {
                byte state = s_receiverStates[index];
                if (state == CacheSlotOccupied)
                {
                    if (s_receiverKeys[index] == key &&
                        ReferenceEquals(s_receiverColliders[index], collider))
                    {
                        s_receivers[index] = receiver;
                        return;
                    }
                }
                else
                {
                    WriteReceiverSlot(index, key, collider, receiver);
                    return;
                }

                index = (index + 1) & ReceiverCacheMask;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!s_saturationLogged)
            {
                s_saturationLogged = true;
                Debug.LogWarning("[PhysicalHandReceiverRegistry] Fixed receiver cache saturated. Increase MaxReceivers.");
            }
#endif
        }

        private static void RemoveReceiver(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            ulong key = EntityId.ToULong(collider.GetEntityId());
            int index = HashKey(key);
            for (int probe = 0; probe < MaxReceivers; probe++)
            {
                byte state = s_receiverStates[index];
                if (state == CacheSlotEmpty)
                    return;

                if (state == CacheSlotOccupied &&
                    s_receiverKeys[index] == key &&
                    ReferenceEquals(s_receiverColliders[index], collider) &&
                    ReferenceEquals(s_receivers[index], receiver))
                {
                    RemoveReceiverSlot(index);
                    return;
                }

                index = (index + 1) & ReceiverCacheMask;
            }
        }

        private static void WriteReceiverSlot(
            int index,
            ulong key,
            Collider collider,
            IPhysicalPanelButtonReceiver receiver)
        {
            s_receiverKeys[index] = key;
            s_receiverColliders[index] = collider;
            s_receivers[index] = receiver;
            s_receiverStates[index] = CacheSlotOccupied;
        }

        private static void RemoveReceiverSlot(int removeIndex)
        {
            int holeIndex = removeIndex;
            int index = (holeIndex + 1) & ReceiverCacheMask;
            for (int probe = 0; probe < MaxReceivers - 1; probe++)
            {
                if (s_receiverStates[index] != CacheSlotOccupied)
                    break;

                int idealIndex = HashKey(s_receiverKeys[index]);
                if (ProbeDistance(idealIndex, index, ReceiverCacheMask) >= ProbeDistance(idealIndex, holeIndex, ReceiverCacheMask))
                {
                    s_receiverKeys[holeIndex] = s_receiverKeys[index];
                    s_receiverColliders[holeIndex] = s_receiverColliders[index];
                    s_receivers[holeIndex] = s_receivers[index];
                    s_receiverStates[holeIndex] = CacheSlotOccupied;
                    holeIndex = index;
                }

                index = (index + 1) & ReceiverCacheMask;
            }

            ClearReceiverSlot(holeIndex);
        }

        private static void ClearReceiverSlot(int index)
        {
            s_receiverKeys[index] = 0UL;
            s_receiverColliders[index] = null;
            s_receivers[index] = null;
            s_receiverStates[index] = CacheSlotEmpty;
        }

        private static int ProbeDistance(int idealIndex, int currentIndex, int mask)
        {
            return (currentIndex - idealIndex) & mask;
        }

        private static int HashKey(ulong key)
        {
            unchecked
            {
                key ^= key >> 33;
                key *= 0xff51afd7ed558ccdUL;
                key ^= key >> 33;
                key *= 0xc4ceb9fe1a85ec53UL;
                key ^= key >> 33;
                return (int)key & ReceiverCacheMask;
            }
        }
    }
}
