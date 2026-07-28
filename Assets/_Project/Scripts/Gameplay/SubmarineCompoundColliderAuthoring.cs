using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Editor-authored template for baking a compound submarine collision rig from boxes and capsules.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Compound Collider Authoring")]
    public sealed class SubmarineCompoundColliderAuthoring : MonoBehaviour, ISlowTickable, IPhysicsColliderLodTransitionSink, IPhysicsCullingColliderCache, IGlobalRegistryHotSwapListener
    {
        private const int ColliderLodOverlapCapacity = 32;
        private const int PhysicsCullingColliderCapacity = 4;

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

        [Tooltip("Simplified collision sphere used when no nearby threats are detected. Editor/Development may repair it; shipping requires an authored collider.")]
        [SerializeField] private SphereCollider simplifiedCollider;

        [Tooltip("Local center for the simplified collision sphere when the component has to create it.")]
        [SerializeField] private Vector3 simplifiedColliderCenter = Vector3.zero;

        [Tooltip("Radius for the simplified collision sphere when the component has to create it.")]
        [SerializeField, Min(0.1f)] private float simplifiedColliderRadius = 4.5f;

        [Tooltip("Editor-authored compound collider cache for runtime collider LOD. Rebuilt by OnValidate.")]
        [SerializeField] private Collider[] generatedCompoundColliders = Array.Empty<Collider>();

        [Tooltip("Editor-authored collider refs consumed by GlobalPhysicsStateManager registration gates.")]
        [SerializeField] private Collider[] physicsCullingColliders = Array.Empty<Collider>();

        private Transform _cachedTransform;
        private bool _registeredSlowTick;
        private bool _hotSwapRegistered;
        private bool _usingSimplifiedCollider;
        private bool _colliderLodStateApplied;
        private bool _ownsSimplifiedCollider;
        private bool _distanceColliderLodGateOpen;
        private float _colliderLodNoThreatSeconds;

        /// <summary>
        /// Previous monotonic dispatcher clock sample, or negative when unsampled. Re-baselined whenever
        /// the dwell accumulator is cleared, so time spent NOT accumulating is never billed on the next
        /// tick as one enormous delta.
        /// </summary>
        private double _slowTickClockSampleSeconds = UnsampledSlowTickClock;

        /// <summary>Sentinel for "no clock sample yet this dwell window".</summary>
        private const double UnsampledSlowTickClock = -1d;

        /// <summary>
        /// Largest real gap billable to one slow tick. Matches the dispatcher's own worst-case legitimate
        /// spacing (the homeostasis-emergency slow interval); anything longer is a pause, a scene load or
        /// a hitch, and the submarine did not spend that time un-threatened.
        /// </summary>
        private const float MaxSlowTickDwellAdvanceSeconds = 1f;

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
            EnsureSimplifiedCollider();
            RebuildRuntimeColliderCache();
            ApplyColliderLodState(false);
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
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTickable();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterSlowTickable();

                return;
            }

            // The TickManager slot used to be tracked here so the dwell accumulator could read its
            // nominal SlowTickIntervalSeconds. Dwell now comes from the monotonic dispatcher clock, so
            // this component has no reason to hold a TickManager reference at all - and a hot-swap
            // handler that keeps a field nobody reads is a subscription paying rent for nothing.
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
                _slowTickClockSampleSeconds = UnsampledSlowTickClock;
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
                _slowTickClockSampleSeconds = UnsampledSlowTickClock;
                ApplyColliderLodState(false);
                return;
            }

            if (_usingSimplifiedCollider)
                return;

            _colliderLodNoThreatSeconds += ResolveSlowTickDeltaSeconds();
            ApplyColliderLodState(_colliderLodNoThreatSeconds >= Mathf.Max(0f, colliderLodSimplifyHysteresisSeconds));
        }

        void IPhysicsColliderLodHysteresisSink.SetColliderLodDistanceGate(bool allowSimplifiedColliderLod)
        {
            ((IPhysicsColliderLodTransitionSink)this).SetColliderLodDistanceGateAndCountTransitions(allowSimplifiedColliderLod);
        }

        int IPhysicsColliderLodTransitionSink.SetColliderLodDistanceGateAndCountTransitions(bool allowSimplifiedColliderLod)
        {
            if (_distanceColliderLodGateOpen == allowSimplifiedColliderLod)
                return 0;

            _distanceColliderLodGateOpen = allowSimplifiedColliderLod;
            if (!allowSimplifiedColliderLod)
            {
                _colliderLodNoThreatSeconds = 0f;
                _slowTickClockSampleSeconds = UnsampledSlowTickClock;
                return ApplyColliderLodState(false);
            }

            return 0;
        }

        bool IPhysicsCullingColliderCache.TryGetPhysicsCullingColliders(out Collider[] colliders, out int count)
        {
            colliders = physicsCullingColliders;
            count = colliders != null ? colliders.Length : 0;
            return count > 0;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // COLD ALLOC: SphereCollider[1] - simplified submarine physics LOD collider - owner: SubmarineCompoundColliderAuthoring
            simplifiedCollider = gameObject.AddComponent<SphereCollider>();
            _ownsSimplifiedCollider = true;
            simplifiedCollider.center = simplifiedColliderCenter;
            simplifiedCollider.radius = Mathf.Max(0.1f, simplifiedColliderRadius);
            simplifiedCollider.isTrigger = false;
            simplifiedCollider.enabled = false;
            _colliderLodStateApplied = false;
#else
            enableRuntimeColliderLod = false;
            _ownsSimplifiedCollider = false;
            _colliderLodStateApplied = false;
#endif
        }

        private void RebuildRuntimeColliderCache()
        {
            _compoundColliderCache.Clear();
            _colliderLodStateApplied = false;
            Collider[] colliders = generatedCompoundColliders;
            int count = colliders != null ? colliders.Length : 0;
            if (count <= 0)
                return;

            for (int i = 0; i < count && _compoundColliderCache.Count < ColliderLodOverlapCapacity; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider != simplifiedCollider && !(collider is MeshCollider))
                    _compoundColliderCache.Add(collider);
            }
        }

        private int ApplyColliderLodState(bool useSimplifiedCollider)
        {
            if (_colliderLodStateApplied && _usingSimplifiedCollider == useSimplifiedCollider && simplifiedCollider != null)
                return 0;

            _colliderLodStateApplied = true;
            _usingSimplifiedCollider = useSimplifiedCollider;
            if (!useSimplifiedCollider)
                _colliderLodNoThreatSeconds = 0f;
                _slowTickClockSampleSeconds = UnsampledSlowTickClock;

            int transitionCount = 0;
            if (simplifiedCollider != null)
            {
                simplifiedCollider.center = simplifiedColliderCenter;
                simplifiedCollider.radius = Mathf.Max(0.1f, simplifiedColliderRadius);
                if (simplifiedCollider.enabled != useSimplifiedCollider)
                {
                    simplifiedCollider.enabled = useSimplifiedCollider;
                    transitionCount++;
                }
            }

            for (int i = 0; i < _compoundColliderCache.Count; i++)
            {
                Collider cachedCollider = _compoundColliderCache[i];
                bool compoundColliderEnabled = !useSimplifiedCollider;
                if (cachedCollider != null && cachedCollider.enabled != compoundColliderEnabled)
                {
                    cachedCollider.enabled = !useSimplifiedCollider;
                    transitionCount++;
                }
            }

            return transitionCount;
        }

        /// <summary>
        /// Real seconds since the previous slow tick, from the monotonic dispatcher clock.
        ///
        /// This used to return GameTickManager.SlowTickIntervalSeconds - the NOMINAL value - and 0.5f
        /// when the manager was missing. The dispatcher's ACTUAL slow interval is not that constant:
        /// SystemDispatcher.ResolveSlowTickIntervalSeconds returns 0.1 s normally, 0.2 s while
        /// thermal-critical, 1.0 s during a homeostasis emergency, and a GlobalQualityWeight-dependent
        /// lerp while the simulation bucketer idles. Up to a 10x spread. Accumulating a hysteresis
        /// DURATION out of a nominal interval therefore made the collider-LOD dwell time a function of
        /// how hot the machine is and what the graphics settings are - and collider LOD is physics, so
        /// that is gameplay truth changing with quality state, which SYSTEMS_CONTRACTS.md:141 forbids.
        /// The 0.5f fallback was wrong twice over: it was not even the nominal 0.1 s.
        ///
        /// Same defect and same fix as FirstHourDirector's pacing clock (commit 4b307afde), which is now
        /// the idiom in this codebase: sample the monotonic clock, take the delta, and cap it so a pause,
        /// a scene load or a hitch is not billed as dwell time.
        /// </summary>
        private float ResolveSlowTickDeltaSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;

            // Unsampled: this tick establishes the baseline and buys no dwell time.
            if (_slowTickClockSampleSeconds < 0d)
            {
                _slowTickClockSampleSeconds = now;
                return 0f;
            }

            double delta = now - _slowTickClockSampleSeconds;
            _slowTickClockSampleSeconds = now;

            // Negated comparison so a NaN clock reading falls through as zero instead of poisoning the
            // accumulator. Catch-up substeps inside one frame read the same time snapshot, so their
            // delta is zero and dwell advances once per frame by the real frame delta.
            if (!(delta > 0d))
                return 0f;

            return delta > MaxSlowTickDwellAdvanceSeconds
                ? MaxSlowTickDwellAdvanceSeconds
                : (float)delta;
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

            RebuildSerializedColliderCachesEditor();
        }

        private void RebuildSerializedColliderCachesEditor()
        {
            _compoundColliderCache.Clear();
            Transform rootTransform = transform;
            Transform generatedRoot = rootTransform != null ? rootTransform.Find(GeneratedRootName) : null;
            if (generatedRoot == null)
            {
                generatedCompoundColliders = Array.Empty<Collider>();
                physicsCullingColliders = Array.Empty<Collider>();
                return;
            }

            generatedRoot.GetComponentsInChildren(true, _compoundColliderCache);
            int writeCount = 0;
            for (int i = 0; i < _compoundColliderCache.Count; i++)
            {
                Collider collider = _compoundColliderCache[i];
                if (collider != null && collider != simplifiedCollider && !(collider is MeshCollider))
                    _compoundColliderCache[writeCount++] = collider;
            }

            if (writeCount == 0)
            {
                generatedCompoundColliders = Array.Empty<Collider>();
                physicsCullingColliders = Array.Empty<Collider>();
                return;
            }

            generatedCompoundColliders = CopyColliderCacheIfChanged(generatedCompoundColliders, _compoundColliderCache, writeCount);

            int cullingCount = Math.Min(writeCount, PhysicsCullingColliderCapacity);
            physicsCullingColliders = CopyColliderCacheIfChanged(physicsCullingColliders, _compoundColliderCache, cullingCount);
        }

        private static Collider[] CopyColliderCacheIfChanged(Collider[] existing, List<Collider> source, int count)
        {
            if (count <= 0)
                return Array.Empty<Collider>();

            if (existing != null && existing.Length == count)
            {
                bool unchanged = true;
                for (int i = 0; i < count; i++)
                {
                    if (existing[i] != source[i])
                    {
                        unchanged = false;
                        break;
                    }
                }

                if (unchanged)
                    return existing;
            }

            Collider[] next = new Collider[count];
            for (int i = 0; i < count; i++)
                next[i] = source[i];
            return next;
        }
#endif
    }
}
