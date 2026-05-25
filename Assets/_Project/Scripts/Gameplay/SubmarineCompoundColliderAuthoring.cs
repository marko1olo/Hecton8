using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Editor-authored template for baking a compound submarine collision rig from boxes and capsules.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Compound Collider Authoring")]
    public sealed class SubmarineCompoundColliderAuthoring : MonoBehaviour, ISlowTickable, IPhysicsColliderLodHysteresisSink, IGlobalRegistryHotSwapListener
    {
        private const int ColliderLodOverlapCapacity = 32;

        [Serializable]
        public struct BoxShape
        {
            [Tooltip("Optional debug label for this collider part.")]
            public string Name;

            [Tooltip("Local-space center offset in the submarine root frame.")]
            public Vector3 Center;

            [Tooltip("Local-space box dimensions in meters.")]
            public Vector3 Size;

            [Tooltip("Optional physic material applied to this generated collider.")]
            public PhysicsMaterial Material;

            [Tooltip("Whether the generated collider should be a trigger.")]
            public bool IsTrigger;
        }

        [Serializable]
        public struct CapsuleShape
        {
            [Tooltip("Optional debug label for this collider part.")]
            public string Name;

            [Tooltip("Local-space center offset in the submarine root frame.")]
            public Vector3 Center;

            [Tooltip("Capsule radius in meters.")]
            public float Radius;

            [Tooltip("Capsule total height in meters.")]
            public float Height;

            [Tooltip("Capsule axis. 0 = X, 1 = Y, 2 = Z.")]
            [Range(0, 2)] public int Direction;

            [Tooltip("Optional physic material applied to this generated collider.")]
            public PhysicsMaterial Material;

            [Tooltip("Whether the generated collider should be a trigger.")]
            public bool IsTrigger;
        }

        [Header("-- Generation ----------------------")]
        [Tooltip("Name used for the generated collider root beneath this submarine.")]
        [SerializeField] private string generatedRootName = "__CompoundColliders";

        [Tooltip("When enabled, the baker clears previously generated colliders before rebuilding.")]
        [SerializeField] private bool replaceExistingGeneratedColliders = true;

        [Header("-- Box Segments --------------------")]
        [Tooltip("Authored local-space box collider segments baked by the editor tool.")]
        [SerializeField] private BoxShape[] boxShapes = Array.Empty<BoxShape>();

        [Header("-- Capsule Segments ----------------")]
        [Tooltip("Authored local-space capsule collider segments baked by the editor tool.")]
        [SerializeField] private CapsuleShape[] capsuleShapes = Array.Empty<CapsuleShape>();

        [Header("-- Runtime Collider LOD ------------")]
        [Tooltip("When enabled, distant low-risk submarine physics uses one sphere instead of the compound collider rig.")]
        [SerializeField] private bool enableRuntimeColliderLod = true;

        [Tooltip("No external obstacle or enemy inside this radius allows the simplified sphere collider.")]
        [SerializeField, Min(1f)] private float colliderLodProbeRadius = 100f;

        [Tooltip("Layers counted as nearby obstacles or enemies for keeping the detailed compound collider active.")]
        [SerializeField] private LayerMask colliderLodThreatMask = HectonLayerMasks.DefaultRaycastLayerMask;

        [Tooltip("Seconds with zero nearby threats required before swapping from compound colliders to the simplified sphere.")]
        [SerializeField, Min(0f)] private float colliderLodSimplifyHysteresisSeconds = 5f;

        [Tooltip("Simplified collision sphere used when no nearby threats are detected. Created cold if omitted.")]
        [SerializeField] private SphereCollider simplifiedCollider;

        [Tooltip("Local center for the simplified collision sphere when the component has to create it.")]
        [SerializeField] private Vector3 simplifiedColliderCenter = Vector3.zero;

        [Tooltip("Radius for the simplified collision sphere when the component has to create it.")]
        [SerializeField, Min(0.1f)] private float simplifiedColliderRadius = 4.5f;

        private Transform _cachedTransform;
        private GameTickManager _tickManager;
        private bool _registeredSlowTick;
        private bool _hotSwapRegistered;
        private bool _usingSimplifiedCollider;
        private bool _ownsSimplifiedCollider;
        private bool _distanceColliderLodGateOpen;
        private float _colliderLodNoThreatSeconds;

        // COLD ALLOC: List<Collider>[32] - generated compound collider cache for runtime collider LOD toggles - owner: SubmarineCompoundColliderAuthoring
        private readonly List<Collider> _compoundColliderCache = new List<Collider>(32);
        // COLD ALLOC: Collider[32] - nonalloc collider LOD threat probe results - owner: SubmarineCompoundColliderAuthoring
        private readonly SpatialQueryHit[] _colliderLodThreatHits = new SpatialQueryHit[ColliderLodOverlapCapacity];

        /// <summary>Name used for the generated collider root beneath this submarine.</summary>
        public string GeneratedRootName => string.IsNullOrWhiteSpace(generatedRootName) ? "__CompoundColliders" : generatedRootName;

        /// <summary>True when generated colliders should be cleared before rebuilding.</summary>
        public bool ReplaceExistingGeneratedColliders => replaceExistingGeneratedColliders;

        /// <summary>Authored box-shape definitions.</summary>
        public BoxShape[] BoxShapes => boxShapes;

        /// <summary>Authored capsule-shape definitions.</summary>
        public CapsuleShape[] CapsuleShapes => capsuleShapes;

        private void Awake()
        {
            _cachedTransform = transform;
            EnsureSimplifiedCollider();
            RebuildRuntimeColliderCache();
            ApplyColliderLodState(false);
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            CacheTickManagerCold();
            EnsureSimplifiedCollider();
            RebuildRuntimeColliderCache();
            TryRegisterHotSwapListener();
            _distanceColliderLodGateOpen = false;
            TryRegisterSlowTickable();
        }

        private void OnDisable()
        {
            _distanceColliderLodGateOpen = false;
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            ApplyColliderLodState(false);
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService != null)
                {
                    TryUnregisterSlowTickable();
                    TryRegisterSlowTickable();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.TickManager)
                _tickManager = currentService as GameTickManager;
        }

        public void SlowTick()
        {
            if (!enableRuntimeColliderLod ||
                !_distanceColliderLodGateOpen ||
                _cachedTransform == null ||
                simplifiedCollider == null ||
                _compoundColliderCache.Count <= 0)
            {
                _colliderLodNoThreatSeconds = 0f;
                ApplyColliderLodState(false);
                return;
            }

            int mask = colliderLodThreatMask.value != 0 ? colliderLodThreatMask.value : HectonLayerMasks.DefaultRaycastLayerMask;
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Signal |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                _cachedTransform.position,
                Mathf.Max(1f, colliderLodProbeRadius),
                kindMask,
                _colliderLodThreatHits);

            bool hasExternalThreat = false;
            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = _colliderLodThreatHits[i];
                _colliderLodThreatHits[i] = default;
                if (!LayerMatchesMask(hit.Layer, mask))
                    continue;

                Transform hitTransform = hit.Transform;
                if (hitTransform == null)
                    continue;

                if (hitTransform == _cachedTransform || hitTransform.IsChildOf(_cachedTransform))
                    continue;

                hasExternalThreat = true;
                break;
            }

            if (hasExternalThreat)
            {
                _colliderLodNoThreatSeconds = 0f;
                ApplyColliderLodState(false);
                return;
            }

            if (_usingSimplifiedCollider)
                return;

            _colliderLodNoThreatSeconds += ResolveSlowTickIntervalSeconds();
            ApplyColliderLodState(_colliderLodNoThreatSeconds >= Mathf.Max(0f, colliderLodSimplifyHysteresisSeconds));
        }

        void IPhysicsColliderLodHysteresisSink.SetColliderLodDistanceGate(bool allowSimplifiedColliderLod)
        {
            if (_distanceColliderLodGateOpen == allowSimplifiedColliderLod)
                return;

            _distanceColliderLodGateOpen = allowSimplifiedColliderLod;
            if (!allowSimplifiedColliderLod)
            {
                _colliderLodNoThreatSeconds = 0f;
                ApplyColliderLodState(false);
            }
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static bool LayerMatchesMask(int layer, int mask)
        {
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
        }

        private void EnsureSimplifiedCollider()
        {
            if (simplifiedCollider != null)
                return;

            // COLD ALLOC: SphereCollider[1] - simplified submarine physics LOD collider - owner: SubmarineCompoundColliderAuthoring
            simplifiedCollider = gameObject.AddComponent<SphereCollider>();
            _ownsSimplifiedCollider = true;
            simplifiedCollider.center = simplifiedColliderCenter;
            simplifiedCollider.radius = Mathf.Max(0.1f, simplifiedColliderRadius);
            simplifiedCollider.isTrigger = false;
            simplifiedCollider.enabled = false;
        }

        private void RebuildRuntimeColliderCache()
        {
            _compoundColliderCache.Clear();
            Transform generatedRoot = _cachedTransform != null ? _cachedTransform.Find(GeneratedRootName) : null;
            if (generatedRoot == null)
                return;

            generatedRoot.GetComponentsInChildren(true, _compoundColliderCache);
            for (int i = _compoundColliderCache.Count - 1; i >= 0; i--)
            {
                Collider cachedCollider = _compoundColliderCache[i];
                if (cachedCollider == null || cachedCollider == simplifiedCollider)
                    _compoundColliderCache.RemoveAt(i);
            }
        }

        private void ApplyColliderLodState(bool useSimplifiedCollider)
        {
            if (_usingSimplifiedCollider == useSimplifiedCollider && simplifiedCollider != null)
                return;

            _usingSimplifiedCollider = useSimplifiedCollider;
            if (!useSimplifiedCollider)
                _colliderLodNoThreatSeconds = 0f;
            if (simplifiedCollider != null)
            {
                simplifiedCollider.center = simplifiedColliderCenter;
                simplifiedCollider.radius = Mathf.Max(0.1f, simplifiedColliderRadius);
                simplifiedCollider.enabled = useSimplifiedCollider;
            }

            for (int i = 0; i < _compoundColliderCache.Count; i++)
            {
                Collider cachedCollider = _compoundColliderCache[i];
                if (cachedCollider != null)
                    cachedCollider.enabled = !useSimplifiedCollider;
            }
        }

        private float ResolveSlowTickIntervalSeconds()
        {
            GameTickManager tickManager = _tickManager;
            return tickManager != null
                ? Mathf.Max(0.01f, tickManager.SlowTickIntervalSeconds)
                : 0.5f;
        }

        private void CacheTickManagerCold()
        {
            _tickManager = GlobalRegistry.TickManager;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            colliderLodProbeRadius = Mathf.Max(1f, colliderLodProbeRadius);
            colliderLodSimplifyHysteresisSeconds = Mathf.Max(0f, colliderLodSimplifyHysteresisSeconds);
            simplifiedColliderRadius = Mathf.Max(0.1f, simplifiedColliderRadius);
            if (simplifiedCollider != null && _ownsSimplifiedCollider)
            {
                simplifiedCollider.center = simplifiedColliderCenter;
                simplifiedCollider.radius = simplifiedColliderRadius;
            }
        }
#endif
    }
}
