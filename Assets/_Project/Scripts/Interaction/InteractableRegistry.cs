using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.Tools;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Cold-built collider lookup cache for interaction prompt routing.
    /// </summary>
    internal static class InteractableRegistry
    {
        private const int MaxCachedTargets = 4096;
        private const int CacheMask = MaxCachedTargets - 1;
        private const int MaxInvalidationColliders = 64;
        private const int MaxResolveHierarchyDepth = 32;
        private const byte CacheSlotEmpty = 0;
        private const byte CacheSlotOccupied = 1;

        // COLD ALLOC: ulong[4096] - fixed collider entity id cache keys for interaction target lookup - owner: InteractableRegistry
        private static readonly ulong[] s_targetKeys = new ulong[MaxCachedTargets];
        // COLD ALLOC: TargetInfo[4096] - fixed interaction target cache values for player look ray - owner: InteractableRegistry
        private static readonly TargetInfo[] s_targetValues = new TargetInfo[MaxCachedTargets];
        // COLD ALLOC: byte[4096] - fixed open-address slot states for interaction target lookup - owner: InteractableRegistry
        private static readonly byte[] s_targetStates = new byte[MaxCachedTargets];
        // COLD ALLOC: List<Collider>[64] - teardown-time child collider invalidation scratch buffer - owner: InteractableRegistry
        private static readonly List<Collider> s_invalidationColliders = new List<Collider>(MaxInvalidationColliders);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_cacheSaturationLogged;
#endif

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct TargetInfo
        {
            public TargetInfo(
                IInteractable interactable,
                IInventoryPickupSource pickupSource,
                IBatteryTool batteryTool,
                BatteryCharger charger,
                BioReactor reactor,
                StorageCrate crate,
                PickupItem pickup,
                ScannableTarget scannable,
                ResourceNode resourceNode,
                BaseModule baseModule)
            {
                Interactable = interactable;
                PickupSource = pickupSource;
                BatteryTool = batteryTool;
                Charger = charger;
                Reactor = reactor;
                Crate = crate;
                Pickup = pickup;
                Scannable = scannable;
                ResourceNode = resourceNode;
                BaseModule = baseModule;
            }

            public readonly IInteractable Interactable;
            public readonly IInventoryPickupSource PickupSource;
            public readonly IBatteryTool BatteryTool;
            public readonly BatteryCharger Charger;
            public readonly BioReactor Reactor;
            public readonly StorageCrate Crate;
            public readonly PickupItem Pickup;
            public readonly ScannableTarget Scannable;
            public readonly ResourceNode ResourceNode;
            public readonly BaseModule BaseModule;
            public bool HasAny =>
                Interactable != null ||
                PickupSource != null ||
                BatteryTool != null ||
                Charger != null ||
                Reactor != null ||
                Crate != null ||
                Pickup != null ||
                Scannable != null ||
                ResourceNode != null ||
                BaseModule != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < MaxCachedTargets; i++)
            {
                s_targetKeys[i] = 0UL;
                s_targetValues[i] = default;
                s_targetStates[i] = CacheSlotEmpty;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_cacheSaturationLogged = false;
#endif
            s_invalidationColliders.Clear();
        }

        internal static bool TryResolve(Collider collider, out TargetInfo info)
        {
            if (collider == null)
            {
                info = default;
                return false;
            }

            ulong instanceId = EntityId.ToULong(collider.GetEntityId());
            if (TryGetCachedTarget(instanceId, out info))
                return info.HasAny;

            info = ResolveTargetInfo(collider);
            CacheTarget(instanceId, info);

            return info.HasAny;
        }

        internal static void Invalidate(Collider collider)
        {
            if (collider == null)
                return;

            ulong instanceId = EntityId.ToULong(collider.GetEntityId());
            RemoveCachedTarget(instanceId);
        }

        internal static void InvalidateTree(Component owner)
        {
            if (owner == null)
                return;

            s_invalidationColliders.Clear();
            owner.GetComponentsInChildren(true, s_invalidationColliders);
            for (int i = 0; i < s_invalidationColliders.Count; i++)
                Invalidate(s_invalidationColliders[i]);
            s_invalidationColliders.Clear();
        }

        private static TargetInfo ResolveTargetInfo(Collider collider)
        {
            if (collider == null)
                return default;

            IInteractable interactable = null;
            IInventoryPickupSource pickupSource = null;
            IBatteryTool batteryTool = null;
            BatteryCharger charger = null;
            BioReactor reactor = null;
            StorageCrate crate = null;
            PickupItem pickup = null;
            ScannableTarget scannable = null;
            ResourceNode resourceNode = null;
            BaseModule baseModule = null;

            Transform current = collider.transform;
            int depth = 0;
            while (current != null && depth < MaxResolveHierarchyDepth)
            {
                if (interactable == null)
                    current.TryGetComponent(out interactable);

                if (pickupSource == null)
                    current.TryGetComponent(out pickupSource);

                if (batteryTool == null)
                    current.TryGetComponent(out batteryTool);

                if (charger == null)
                    current.TryGetComponent(out charger);

                if (reactor == null)
                    current.TryGetComponent(out reactor);

                if (crate == null)
                    current.TryGetComponent(out crate);

                if (pickup == null)
                    current.TryGetComponent(out pickup);

                if (scannable == null)
                    current.TryGetComponent(out scannable);

                if (resourceNode == null)
                    current.TryGetComponent(out resourceNode);

                if (baseModule == null)
                    current.TryGetComponent(out baseModule);

                if (interactable != null &&
                    pickupSource != null &&
                    batteryTool != null &&
                    charger != null &&
                    reactor != null &&
                    crate != null &&
                    pickup != null &&
                    scannable != null &&
                    resourceNode != null &&
                    baseModule != null)
                {
                    break;
                }

                current = current.parent;
                depth++;
            }

            return new TargetInfo(interactable, pickupSource, batteryTool, charger, reactor, crate, pickup, scannable, resourceNode, baseModule);
        }

        private static bool TryGetCachedTarget(ulong key, out TargetInfo info)
        {
            int index = HashKey(key);
            for (int probe = 0; probe < MaxCachedTargets; probe++)
            {
                byte state = s_targetStates[index];
                if (state == CacheSlotEmpty)
                {
                    info = default;
                    return false;
                }

                if (state == CacheSlotOccupied && s_targetKeys[index] == key)
                {
                    info = s_targetValues[index];
                    return true;
                }

                index = (index + 1) & CacheMask;
            }

            info = default;
            return false;
        }

        private static void CacheTarget(ulong key, TargetInfo info)
        {
            int index = HashKey(key);
            for (int probe = 0; probe < MaxCachedTargets; probe++)
            {
                byte state = s_targetStates[index];
                if (state == CacheSlotOccupied)
                {
                    if (s_targetKeys[index] == key)
                    {
                        s_targetValues[index] = info;
                        return;
                    }
                }
                else
                {
                    WriteCacheSlot(index, key, info);
                    return;
                }

                index = (index + 1) & CacheMask;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!s_cacheSaturationLogged)
            {
                s_cacheSaturationLogged = true;
                Debug.LogWarning("[InteractableRegistry] Fixed collider target cache saturated. Increase MaxCachedTargets.");
            }
#endif
        }

        private static void WriteCacheSlot(int index, ulong key, TargetInfo info)
        {
            s_targetKeys[index] = key;
            s_targetValues[index] = info;
            s_targetStates[index] = CacheSlotOccupied;
        }

        private static void RemoveCachedTarget(ulong key)
        {
            int index = HashKey(key);
            for (int probe = 0; probe < MaxCachedTargets; probe++)
            {
                byte state = s_targetStates[index];
                if (state == CacheSlotEmpty)
                    return;

                if (state == CacheSlotOccupied && s_targetKeys[index] == key)
                {
                    RemoveCacheSlot(index);
                    return;
                }

                index = (index + 1) & CacheMask;
            }
        }

        private static void RemoveCacheSlot(int removeIndex)
        {
            int holeIndex = removeIndex;
            int index = (holeIndex + 1) & CacheMask;
            for (int probe = 0; probe < MaxCachedTargets - 1; probe++)
            {
                if (s_targetStates[index] != CacheSlotOccupied)
                    break;

                int idealIndex = HashKey(s_targetKeys[index]);
                if (ProbeDistance(idealIndex, index, CacheMask) >= ProbeDistance(idealIndex, holeIndex, CacheMask))
                {
                    s_targetKeys[holeIndex] = s_targetKeys[index];
                    s_targetValues[holeIndex] = s_targetValues[index];
                    s_targetStates[holeIndex] = CacheSlotOccupied;
                    holeIndex = index;
                }

                index = (index + 1) & CacheMask;
            }

            ClearCacheSlot(holeIndex);
        }

        private static void ClearCacheSlot(int index)
        {
            s_targetKeys[index] = 0UL;
            s_targetValues[index] = default;
            s_targetStates[index] = CacheSlotEmpty;
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
                return (int)key & CacheMask;
            }
        }
    }
}
