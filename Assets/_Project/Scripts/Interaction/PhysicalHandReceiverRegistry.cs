namespace Hecton8.Interaction
{
    using UnityEngine;

    /// <summary>
    /// Shared collider-to-hand-receiver table for physical cockpit controls.
    /// </summary>
    public static class PhysicalHandReceiverRegistry
    {
        private const int MaxReceivers = 128;
        private const int ReceiverCacheMask = MaxReceivers - 1;
        private const byte CacheSlotEmpty = 0;
        private const byte CacheSlotOccupied = 1;

        private struct ReceiverSlot
        {
            public IPhysicalPanelButtonReceiver Receiver;

            public void Clear()
            {
                Receiver = null;
            }
        }

        // COLD ALLOC: ulong[128] - fixed collider entity id keys for physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly ulong[] s_receiverKeys = new ulong[MaxReceivers];
        // COLD ALLOC: Collider[128] - fixed collider refs for identity validation in physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly Collider[] s_receiverColliders = new Collider[MaxReceivers];
        // COLD ALLOC: ReceiverSlot[128] - fixed physical hand receiver values - owner: PhysicalHandReceiverRegistry
        private static readonly ReceiverSlot[] s_receivers = new ReceiverSlot[MaxReceivers];
        // COLD ALLOC: byte[128] - fixed open-address slot states for physical hand receiver lookup - owner: PhysicalHandReceiverRegistry
        private static readonly byte[] s_receiverStates = new byte[MaxReceivers];
        private static int s_registeredReceiverCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_saturationLogged;
#endif

        /// <summary>
        /// True when at least one collider-backed physical hand receiver is registered.
        /// </summary>
        public static bool HasReceivers => s_registeredReceiverCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < MaxReceivers; i++)
            {
                s_receiverKeys[i] = 0UL;
                s_receiverColliders[i] = null;
                s_receivers[i].Clear();
                s_receiverStates[i] = CacheSlotEmpty;
            }

            s_registeredReceiverCount = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_saturationLogged = false;
#endif
        }

        /// <summary>
        /// Registers a collider-backed receiver for physical hand lookup.
        /// </summary>
        /// <param name="collider">Trigger collider returned by the hand overlap probe.</param>
        /// <param name="receiver">Receiver that handles the physical hand press.</param>
        /// <remarks>
        /// Compatibility wrapper for legacy callers. New systems should use <see cref="TryRegister"/>
        /// when local lifecycle state depends on the write succeeding.
        /// </remarks>
        public static void Register(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            TryRegister(collider, receiver);
        }

        /// <summary>
        /// Attempts to register a collider-backed receiver and reports fixed-table saturation.
        /// </summary>
        /// <param name="collider">Trigger collider returned by the hand overlap probe.</param>
        /// <param name="receiver">Receiver that handles the physical hand press.</param>
        /// <returns>True when the receiver table was updated; false for null inputs or saturation.</returns>
        public static bool TryRegister(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null || receiver == null)
                return false;

            return WriteReceiver(collider, receiver);
        }

        /// <summary>
        /// Removes a collider-backed receiver when the exact collider and receiver pair is still registered.
        /// </summary>
        /// <param name="collider">Collider key previously registered with this receiver.</param>
        /// <param name="receiver">Receiver expected in the fixed table slot.</param>
        public static void Unregister(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null)
                return;

            RemoveReceiver(collider, receiver);
        }

        /// <summary>
        /// Resolves a hand-overlap collider to its registered receiver without component traversal.
        /// </summary>
        /// <param name="collider">Collider returned by the physical hand overlap probe.</param>
        /// <param name="receiver">Registered receiver when the collider key is present.</param>
        /// <returns>True when the collider maps to a non-null receiver.</returns>
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
                    receiver = s_receivers[index].Receiver;
                    return receiver != null;
                }

                index = (index + 1) & ReceiverCacheMask;
            }

            return false;
        }

        /// <summary>
        /// Queries registered panel receivers by bounds distance without touching Unity Physics overlap APIs.
        /// </summary>
        public static int QuerySphere(Vector3 center, float radius, int layerMask, Collider[] results)
        {
            if (results == null || results.Length == 0 || radius <= 0f)
                return 0;

            float radiusSq = radius * radius;
            int count = 0;
            for (int i = 0; i < MaxReceivers && count < results.Length; i++)
            {
                if (s_receiverStates[i] != CacheSlotOccupied)
                    continue;

                Collider collider = s_receiverColliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy ||
                    ((1 << collider.gameObject.layer) & layerMask) == 0)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.extents) || bounds.SqrDistance(center) > radiusSq)
                    continue;

                results[count++] = collider;
            }

            return count;
        }

        private static bool WriteReceiver(Collider collider, IPhysicalPanelButtonReceiver receiver)
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
                        s_receivers[index].Receiver = receiver;
                        return true;
                    }
                }
                else
                {
                    WriteReceiverSlot(index, key, collider, receiver);
                    return true;
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
            return false;
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
                    ReferenceEquals(s_receivers[index].Receiver, receiver))
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
            s_receivers[index].Receiver = receiver;
            s_receiverStates[index] = CacheSlotOccupied;
            s_registeredReceiverCount++;
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
            s_registeredReceiverCount--;
        }

        private static void ClearReceiverSlot(int index)
        {
            s_receiverKeys[index] = 0UL;
            s_receiverColliders[index] = null;
            s_receivers[index].Clear();
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

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
