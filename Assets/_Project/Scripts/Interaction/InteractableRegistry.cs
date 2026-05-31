using System.Collections.Generic;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.Tools;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Cold-built collider lookup and spatial target cache for interaction prompt routing.
    /// </summary>
    public static class InteractableRegistry
    {
        private const int MaxCachedTargets = 4096;
        private const int CacheMask = MaxCachedTargets - 1;
        private const int MaxInvalidationColliders = 256;
        private const int MaxRegisteredTargets = 4096;
        private const int MaxResolveHierarchyDepth = 32;
        private const byte CacheSlotEmpty = 0;
        private const byte CacheSlotOccupied = 1;

        // COLD ALLOC: ulong[4096] - fixed collider entity id cache keys for interaction target lookup - owner: InteractableRegistry
        private static readonly ulong[] s_targetKeys = new ulong[MaxCachedTargets];
        // COLD ALLOC: TargetInfo[4096] - fixed interaction target cache values for player look ray - owner: InteractableRegistry
        private static readonly TargetInfo[] s_targetValues = new TargetInfo[MaxCachedTargets];
        // COLD ALLOC: byte[4096] - fixed open-address slot states for interaction target lookup - owner: InteractableRegistry
        private static readonly byte[] s_targetStates = new byte[MaxCachedTargets];
        // COLD ALLOC: Collider[4096] - fixed spatial interaction target colliders - owner: InteractableRegistry
        private static readonly Collider[] s_registeredColliders = new Collider[MaxRegisteredTargets];
        // COLD ALLOC: TargetInfo[4096] - fixed spatial interaction target payloads - owner: InteractableRegistry
        private static readonly TargetInfo[] s_registeredTargets = new TargetInfo[MaxRegisteredTargets];
        // COLD ALLOC: ulong[4096] - fixed spatial interaction target collider keys - owner: InteractableRegistry
        private static readonly ulong[] s_registeredKeys = new ulong[MaxRegisteredTargets];
        // COLD ALLOC: List<Collider>[256] - teardown-time child collider invalidation scratch buffer - owner: InteractableRegistry
        private static readonly List<Collider> s_invalidationColliders = new List<Collider>(MaxInvalidationColliders);
        private static int s_registeredCount;
        private static bool s_sceneRegistryBuilt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_cacheSaturationLogged;
        private static bool s_registrationSaturationLogged;
#endif

        internal readonly struct TargetInfo
        {
            public TargetInfo(
                IInteractable interactable,
                IInventoryPickupSource pickupSource,
                IInventoryPickupPreviewSource pickupPreviewSource,
                IBatteryTool batteryTool,
                BatteryCharger charger,
                BioReactor reactor,
                StorageCrate crate,
                PickupItem pickup,
                ScannableTarget scannable,
                ResourceNode resourceNode,
                BaseModule baseModule,
                ModuleMarker moduleMarker,
                ITransportPlatform transportPlatform,
                IBaseModuleInteractionHost moduleHost,
                IRepairableModuleTarget repairableModuleTarget,
                IVoxelRepairWeldTarget voxelRepairWeldTarget,
                IVoxelPlasmaCutTarget voxelPlasmaCutTarget,
                ICuttable cuttable,
                IDamageReceiver damageReceiver,
                ISubmarineDamageControlTarget submarineDamageControlTarget,
                ISubmarineRepairRoomResolver submarineRepairRoomResolver,
                IInteractionSignalConsumer interactionSignalConsumer,
                IInteractionVulnerabilitySource interactionVulnerabilitySource,
                Collider physicsCollider,
                Rigidbody physicsBody)
            {
                Interactable = interactable;
                PickupSource = pickupSource;
                PickupPreviewSource = pickupPreviewSource;
                BatteryTool = batteryTool;
                Charger = charger;
                Reactor = reactor;
                Crate = crate;
                Pickup = pickup;
                Scannable = scannable;
                ResourceNode = resourceNode;
                BaseModule = baseModule;
                ModuleMarker = moduleMarker;
                TransportPlatform = transportPlatform;
                ModuleHost = moduleHost;
                RepairableModuleTarget = repairableModuleTarget;
                VoxelRepairWeldTarget = voxelRepairWeldTarget;
                VoxelPlasmaCutTarget = voxelPlasmaCutTarget;
                Cuttable = cuttable;
                DamageReceiver = damageReceiver;
                SubmarineDamageControlTarget = submarineDamageControlTarget;
                SubmarineRepairRoomResolver = submarineRepairRoomResolver;
                InteractionSignalConsumer = interactionSignalConsumer;
                InteractionVulnerabilitySource = interactionVulnerabilitySource;
                PhysicsCollider = physicsCollider;
                PhysicsBody = physicsBody;
            }

            public readonly IInteractable Interactable;
            public readonly IInventoryPickupSource PickupSource;
            public readonly IInventoryPickupPreviewSource PickupPreviewSource;
            public readonly IBatteryTool BatteryTool;
            public readonly BatteryCharger Charger;
            public readonly BioReactor Reactor;
            public readonly StorageCrate Crate;
            public readonly PickupItem Pickup;
            public readonly ScannableTarget Scannable;
            public readonly ResourceNode ResourceNode;
            public readonly BaseModule BaseModule;
            public readonly ModuleMarker ModuleMarker;
            public readonly ITransportPlatform TransportPlatform;
            public readonly IBaseModuleInteractionHost ModuleHost;
            public readonly IRepairableModuleTarget RepairableModuleTarget;
            public readonly IVoxelRepairWeldTarget VoxelRepairWeldTarget;
            public readonly IVoxelPlasmaCutTarget VoxelPlasmaCutTarget;
            public readonly ICuttable Cuttable;
            public readonly IDamageReceiver DamageReceiver;
            public readonly ISubmarineDamageControlTarget SubmarineDamageControlTarget;
            public readonly ISubmarineRepairRoomResolver SubmarineRepairRoomResolver;
            public readonly IInteractionSignalConsumer InteractionSignalConsumer;
            public readonly IInteractionVulnerabilitySource InteractionVulnerabilitySource;
            public readonly Collider PhysicsCollider;
            public readonly Rigidbody PhysicsBody;
            public bool HasAny =>
                Interactable != null ||
                PickupSource != null ||
                PickupPreviewSource != null ||
                BatteryTool != null ||
                Charger != null ||
                Reactor != null ||
                Crate != null ||
                Pickup != null ||
                Scannable != null ||
                ResourceNode != null ||
                BaseModule != null ||
                ModuleMarker != null ||
                TransportPlatform != null ||
                ModuleHost != null ||
                RepairableModuleTarget != null ||
                VoxelRepairWeldTarget != null ||
                VoxelPlasmaCutTarget != null ||
                Cuttable != null ||
                DamageReceiver != null ||
                SubmarineDamageControlTarget != null ||
                SubmarineRepairRoomResolver != null ||
                InteractionSignalConsumer != null ||
                InteractionVulnerabilitySource != null;
        }

        internal readonly struct SpatialHit
        {
            public SpatialHit(Collider collider, TargetInfo targetInfo, Vector3 point, Vector3 normal, float distance)
            {
                Collider = collider;
                TargetInfo = targetInfo;
                Point = point;
                Normal = normal;
                Distance = distance;
            }

            public readonly Collider Collider;
            public readonly TargetInfo TargetInfo;
            public readonly Vector3 Point;
            public readonly Vector3 Normal;
            public readonly float Distance;
            public bool HasHit => Collider != null && TargetInfo.Interactable != null && float.IsFinite(Distance) && Distance >= 0f;
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

            ClearRegisteredTargets();
            s_sceneRegistryBuilt = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_cacheSaturationLogged = false;
            s_registrationSaturationLogged = false;
#endif
            s_invalidationColliders.Clear();
        }

        internal static void EnsureSceneRegistryCold()
        {
            if (s_sceneRegistryBuilt)
                return;

            s_sceneRegistryBuilt = true;
        }

        internal static bool TryResolve(Collider collider, out TargetInfo info)
        {
            if (collider == null)
            {
                info = default;
                return false;
            }

            ulong instanceId = EntityId.ToULong(collider.GetEntityId());
            return TryGetCachedTarget(instanceId, out info) && info.HasAny;
        }

        internal static void Invalidate(Collider collider)
        {
            if (collider == null)
                return;

            ulong instanceId = EntityId.ToULong(collider.GetEntityId());
            RemoveCachedTarget(instanceId);
        }

        public static void InvalidateTree(Component owner)
        {
            if (owner == null)
                return;

            s_invalidationColliders.Clear();
            owner.GetComponentsInChildren(true, s_invalidationColliders);
            for (int i = 0; i < s_invalidationColliders.Count; i++)
            {
                Invalidate(s_invalidationColliders[i]);
                UnregisterCollider(s_invalidationColliders[i]);
            }
            s_invalidationColliders.Clear();
        }

        public static void RegisterTree(Component owner)
        {
            if (owner == null)
                return;

            s_invalidationColliders.Clear();
            owner.GetComponentsInChildren(true, s_invalidationColliders);
            for (int i = 0; i < s_invalidationColliders.Count; i++)
                RegisterCollider(s_invalidationColliders[i]);
            s_invalidationColliders.Clear();
        }

        internal static bool TryResolveSpatialTarget(
            in Ray ray,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction triggerMode,
            out SpatialHit hit)
        {
            hit = default;
            if (!IsFiniteRay(in ray) || !float.IsFinite(maxDistance) || maxDistance <= 0f)
                return false;

            Vector3 direction = ray.direction;
            float directionLengthSq = direction.sqrMagnitude;
            if (!float.IsFinite(directionLengthSq) || directionLengthSq <= 0.000001f)
                return false;

            direction *= math.rsqrt(directionLengthSq);
            Ray normalizedRay = new Ray(ray.origin, direction);
            float bestDistance = maxDistance;
            Collider bestCollider = null;
            TargetInfo bestInfo = default;
            Vector3 bestPoint = default;
            Vector3 bestNormal = Vector3.up;

            for (int i = 0; i < s_registeredCount; i++)
            {
                Collider collider = s_registeredColliders[i];
                TargetInfo targetInfo = s_registeredTargets[i];
                if (collider == null ||
                    targetInfo.Interactable == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy ||
                    (triggerMode == QueryTriggerInteraction.Ignore && collider.isTrigger) ||
                    !LayerIncluded(collider.gameObject.layer, layerMask))
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (!IsFiniteBounds(in bounds) ||
                    !bounds.IntersectRay(normalizedRay, out float distance) ||
                    !float.IsFinite(distance) ||
                    distance < 0f ||
                    distance > bestDistance)
                {
                    continue;
                }

                Vector3 point = normalizedRay.origin + normalizedRay.direction * distance;
                bestDistance = distance;
                bestCollider = collider;
                bestInfo = targetInfo;
                bestPoint = point;
                bestNormal = EstimateBoundsNormal(in bounds, point, -normalizedRay.direction);
            }

            if (bestCollider == null)
                return false;

            hit = new SpatialHit(bestCollider, bestInfo, bestPoint, bestNormal, bestDistance);
            return true;
        }

        private static TargetInfo ResolveTargetInfo(Collider collider)
        {
            if (collider == null)
                return default;

            IInteractable interactable = null;
            IInventoryPickupSource pickupSource = null;
            IInventoryPickupPreviewSource pickupPreviewSource = null;
            IBatteryTool batteryTool = null;
            BatteryCharger charger = null;
            BioReactor reactor = null;
            StorageCrate crate = null;
            PickupItem pickup = null;
            ScannableTarget scannable = null;
            ResourceNode resourceNode = null;
            BaseModule baseModule = null;
            ModuleMarker moduleMarker = null;
            ITransportPlatform transportPlatform = null;
            IBaseModuleInteractionHost moduleHost = null;
            IRepairableModuleTarget repairableModuleTarget = null;
            IVoxelRepairWeldTarget voxelRepairWeldTarget = null;
            IVoxelPlasmaCutTarget voxelPlasmaCutTarget = null;
            ICuttable cuttable = null;
            IDamageReceiver damageReceiver = null;
            ISubmarineDamageControlTarget submarineDamageControlTarget = null;
            ISubmarineRepairRoomResolver submarineRepairRoomResolver = null;
            IInteractionSignalConsumer interactionSignalConsumer = null;
            IInteractionVulnerabilitySource interactionVulnerabilitySource = null;

            Transform current = collider.transform;
            int depth = 0;
            while (current != null && depth < MaxResolveHierarchyDepth)
            {
                if (interactable == null)
                    current.TryGetComponent(out interactable);

                if (pickupSource == null)
                    current.TryGetComponent(out pickupSource);

                if (pickupPreviewSource == null)
                    current.TryGetComponent(out pickupPreviewSource);

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

                if (moduleMarker == null)
                    current.TryGetComponent(out moduleMarker);

                if (transportPlatform == null)
                    current.TryGetComponent(out transportPlatform);

                if (moduleHost == null)
                    current.TryGetComponent(out moduleHost);

                if (repairableModuleTarget == null)
                    current.TryGetComponent(out repairableModuleTarget);

                if (voxelRepairWeldTarget == null)
                    current.TryGetComponent(out voxelRepairWeldTarget);

                if (voxelPlasmaCutTarget == null)
                    current.TryGetComponent(out voxelPlasmaCutTarget);

                if (cuttable == null)
                    current.TryGetComponent(out cuttable);

                if (damageReceiver == null)
                    current.TryGetComponent(out damageReceiver);

                if (submarineDamageControlTarget == null)
                    current.TryGetComponent(out submarineDamageControlTarget);

                if (submarineRepairRoomResolver == null)
                    current.TryGetComponent(out submarineRepairRoomResolver);

                if (interactionSignalConsumer == null)
                    current.TryGetComponent(out interactionSignalConsumer);

                if (interactionVulnerabilitySource == null)
                    current.TryGetComponent(out interactionVulnerabilitySource);

                if (interactable != null &&
                    pickupSource != null &&
                    pickupPreviewSource != null &&
                    batteryTool != null &&
                    charger != null &&
                    reactor != null &&
                    crate != null &&
                    pickup != null &&
                    scannable != null &&
                    resourceNode != null &&
                    baseModule != null &&
                    moduleMarker != null &&
                    transportPlatform != null &&
                    moduleHost != null &&
                    repairableModuleTarget != null &&
                    voxelRepairWeldTarget != null &&
                    voxelPlasmaCutTarget != null &&
                    cuttable != null &&
                    damageReceiver != null &&
                    submarineDamageControlTarget != null &&
                    submarineRepairRoomResolver != null &&
                    interactionSignalConsumer != null &&
                    interactionVulnerabilitySource != null)
                {
                    break;
                }

                current = current.parent;
                depth++;
            }

            return new TargetInfo(
                interactable,
                pickupSource,
                pickupPreviewSource,
                batteryTool,
                charger,
                reactor,
                crate,
                pickup,
                scannable,
                resourceNode,
                baseModule,
                moduleMarker,
                transportPlatform,
                moduleHost,
                repairableModuleTarget,
                voxelRepairWeldTarget,
                voxelPlasmaCutTarget,
                cuttable,
                damageReceiver,
                submarineDamageControlTarget,
                submarineRepairRoomResolver,
                interactionSignalConsumer,
                interactionVulnerabilitySource,
                collider,
                collider.attachedRigidbody);
        }

        private static void RegisterCollider(Collider collider)
        {
            if (collider == null)
                return;

            ulong key = EntityId.ToULong(collider.GetEntityId());
            TargetInfo info = ResolveTargetInfo(collider);
            if (!info.HasAny)
            {
                RemoveCachedTarget(key);
                UnregisterCollider(collider);
                return;
            }

            CacheTarget(key, info);

            if (info.Interactable == null)
            {
                UnregisterCollider(collider);
                return;
            }

            for (int i = 0; i < s_registeredCount; i++)
            {
                if (s_registeredKeys[i] != key)
                    continue;

                s_registeredColliders[i] = collider;
                s_registeredTargets[i] = info;
                return;
            }

            if (s_registeredCount >= MaxRegisteredTargets)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!s_registrationSaturationLogged)
                {
                    s_registrationSaturationLogged = true;
                    Hecton8.Core.H8Debug.LogWarning("[InteractableRegistry] Fixed spatial target registry saturated. Increase MaxRegisteredTargets.");
                }
#endif
                return;
            }

            int index = s_registeredCount++;
            s_registeredKeys[index] = key;
            s_registeredColliders[index] = collider;
            s_registeredTargets[index] = info;
        }

        private static void UnregisterCollider(Collider collider)
        {
            if (collider == null)
                return;

            ulong key = EntityId.ToULong(collider.GetEntityId());
            for (int i = 0; i < s_registeredCount; i++)
            {
                if (s_registeredKeys[i] != key)
                    continue;

                int last = s_registeredCount - 1;
                s_registeredKeys[i] = s_registeredKeys[last];
                s_registeredColliders[i] = s_registeredColliders[last];
                s_registeredTargets[i] = s_registeredTargets[last];
                s_registeredKeys[last] = 0UL;
                s_registeredColliders[last] = null;
                s_registeredTargets[last] = default;
                s_registeredCount = last;
                return;
            }
        }

        private static void ClearRegisteredTargets()
        {
            for (int i = 0; i < MaxRegisteredTargets; i++)
            {
                s_registeredKeys[i] = 0UL;
                s_registeredColliders[i] = null;
                s_registeredTargets[i] = default;
            }

            s_registeredCount = 0;
        }

        private static bool LayerIncluded(int layer, int layerMask)
        {
            if (layer < 0 || layer >= 32)
                return false;

            if (layerMask == Hecton8.Core.HectonLayerMasks.EverythingLayerMaskValue)
                return true;

            return (layerMask & (1 << layer)) != 0;
        }

        private static bool IsFiniteRay(in Ray ray)
        {
            return IsFiniteVector(ray.origin) && IsFiniteVector(ray.direction);
        }

        private static bool IsFiniteBounds(in Bounds bounds)
        {
            return IsFiniteVector(bounds.center) && IsFiniteVector(bounds.extents) && bounds.extents.sqrMagnitude > 0f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static Vector3 EstimateBoundsNormal(in Bounds bounds, Vector3 point, Vector3 fallback)
        {
            Vector3 local = point - bounds.center;
            Vector3 extents = bounds.extents;
            float dx = Mathf.Abs(extents.x - Mathf.Abs(local.x));
            float dy = Mathf.Abs(extents.y - Mathf.Abs(local.y));
            float dz = Mathf.Abs(extents.z - Mathf.Abs(local.z));

            Vector3 normal;
            if (dx <= dy && dx <= dz)
                normal = new Vector3(local.x >= 0f ? 1f : -1f, 0f, 0f);
            else if (dy <= dz)
                normal = new Vector3(0f, local.y >= 0f ? 1f : -1f, 0f);
            else
                normal = new Vector3(0f, 0f, local.z >= 0f ? 1f : -1f);

            if (IsFiniteVector(normal) && normal.sqrMagnitude > 0.000001f)
                return normal;

            float fallbackLengthSq = fallback.sqrMagnitude;
            if (IsFiniteVector(fallback) && float.IsFinite(fallbackLengthSq) && fallbackLengthSq > 0.000001f)
                return fallback * math.rsqrt(fallbackLengthSq);

            return Vector3.up;
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
                Hecton8.Core.H8Debug.LogWarning("[InteractableRegistry] Fixed collider target cache saturated. Increase MaxCachedTargets.");
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
