using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Thin runtime root for the active submarine transport frame.
    /// </summary>
    /// <remarks>
    /// This owner exposes hull motion and subsystem references through a narrow runtime contract.
    /// It does not simulate flooding, atmosphere, damage diffusion, or presentation directly.
    /// Those behaviors remain owned by their dedicated components to prevent a submarine God Object.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Core Director")]
    public sealed class SubmarineCoreDirector : MonoBehaviour, ISubmarineRuntimeContext, IFixedTickable
    {
        public struct SubmarinePhysicsBindingState
        {
            public float3 LinearVelocity;
            public float3 AngularVelocity;
            public float3 CenterOfMass;
        }

        public struct SubmarineGridState
        {
            public byte HasStructuralGrid;
            public byte HasFluidDynamics;
            public byte HasAtmosphereSystem;
            public byte IsTransportPlatformActive;
        }

        private const int HullSummarySlotCount = 4;
        private const int HullSummaryTotalBreachArea = 0;
        private const int HullSummaryMaxCompartmentBreachArea = 1;
        private const int HullSummaryCompartmentCount = 2;
        private const int HullSummaryReadyFlag = 3;

        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.Submarine.CoreDirector.FixedTick");

        [Header("── Frame ──────────────────")]
        [Tooltip("Optional explicit transform used as the rider-space reference frame. Defaults to this root transform.")]
        [SerializeField] private Transform platformFrame;

        [Tooltip("When true, player yaw inherits submarine hull rotation through the shared transport pipeline.")]
        [SerializeField] private bool inheritPlatformRotation = true;

        [Header("── References ──────────────────")]
        [Tooltip("Optional explicit rigidbody driving hull motion. Defaults to the owned Rigidbody on this root.")]
        [SerializeField] private Rigidbody hullRigidbody;

        [Tooltip("Optional explicit flooding owner. Defaults to the owned SubmarineFluidDynamics on this root.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;

        [Tooltip("Optional explicit atmosphere owner. Defaults to the owned SubmarineAtmosphereSystem on this root.")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;

        [Tooltip("Optional explicit structural owner. Defaults to the owned SubmarineStructuralGrid on this root.")]
        [SerializeField] private SubmarineStructuralGrid structuralGrid;

        private Transform _cachedTransform;
        private bool _registeredFixedTick;

        // COLD ALLOC: NativeArray<float>[4] — submarine root hull summary buffer for registry-facing readback without crawling child systems — owner: SubmarineCoreDirector
        private NativeArray<float> _hullIntegritySummaryNative;
        // COLD ALLOC: NativeArray<SubmarinePhysicsBindingState>[1] — authoritative rigidbody motion snapshot for submarine consumers — owner: SubmarineCoreDirector
        private NativeArray<SubmarinePhysicsBindingState> _physicsBindingsNative;
        // COLD ALLOC: NativeArray<SubmarineGridState>[1] — subsystem readiness flags packed at the submarine root — owner: SubmarineCoreDirector
        private NativeArray<SubmarineGridState> _gridStatesNative;

        /// <inheritdoc />
        public bool IsTransportPlatformActive => isActiveAndEnabled && PlatformTransform != null && hullRigidbody != null;

        /// <inheritdoc />
        public Transform PlatformTransform => platformFrame != null ? platformFrame : _cachedTransform;

        /// <inheritdoc />
        public bool InheritPlatformRotation => inheritPlatformRotation;

        /// <inheritdoc />
        public Rigidbody HullRigidbody => hullRigidbody;

        /// <inheritdoc />
        public SubmarineFluidDynamics FluidDynamics => fluidDynamics;

        /// <inheritdoc />
        public SubmarineAtmosphereSystem AtmosphereSystem => atmosphereSystem;

        /// <inheritdoc />
        public SubmarineStructuralGrid StructuralGrid => structuralGrid;

        /// <summary>Published hull summary owned by the submarine root.</summary>
        public NativeArray<float> HullIntegritySummaryNative => _hullIntegritySummaryNative;

        /// <summary>Published rigidbody motion snapshot owned by the submarine root.</summary>
        public NativeArray<SubmarinePhysicsBindingState> PhysicsBindingsNative => _physicsBindingsNative;

        /// <summary>Published subsystem readiness snapshot owned by the submarine root.</summary>
        public NativeArray<SubmarineGridState> GridStatesNative => _gridStatesNative;

        private void Awake()
        {
            _cachedTransform = transform;
            CacheReferences();
            EnsureNativeState();
            RefreshNativeState();
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            CacheReferences();
            EnsureNativeState();
            RefreshNativeState();
            GlobalRegistry.RegisterSubmarine(this);
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
            DisposeNativeState();
        }

        /// <inheritdoc />
        public Vector3 GetPlatformPointVelocity(Vector3 worldPoint)
        {
            Rigidbody body = hullRigidbody;
            if (body == null)
                return Vector3.zero;

            Vector3 centerOfMass = body.worldCenterOfMass;
            Vector3 radialOffset = worldPoint - centerOfMass;
            Vector3 pointVelocity = body.linearVelocity + Vector3.Cross(body.angularVelocity, radialOffset);
            return IsFinite(pointVelocity) ? pointVelocity : Vector3.zero;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            using (_fixedTickProfilerMarker.Auto())
            {
                RefreshNativeState();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cachedTransform = transform;
            CacheReferences();
            if (Application.isPlaying)
            {
                EnsureNativeState();
                RefreshNativeState();
            }
        }
#endif

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (platformFrame == null)
                platformFrame = _cachedTransform;

            if (hullRigidbody == null)
                TryGetComponent(out hullRigidbody);

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);

            if (structuralGrid == null)
                TryGetComponent(out structuralGrid);
        }

        private void EnsureNativeState()
        {
            if (!_hullIntegritySummaryNative.IsCreated)
                _hullIntegritySummaryNative = new NativeArray<float>(HullSummarySlotCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (!_physicsBindingsNative.IsCreated)
                _physicsBindingsNative = new NativeArray<SubmarinePhysicsBindingState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (!_gridStatesNative.IsCreated)
                _gridStatesNative = new NativeArray<SubmarineGridState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void RefreshNativeState()
        {
            if (!_hullIntegritySummaryNative.IsCreated ||
                !_physicsBindingsNative.IsCreated ||
                !_gridStatesNative.IsCreated)
            {
                return;
            }

            Rigidbody body = hullRigidbody;
            if (body != null)
            {
                _physicsBindingsNative[0] = new SubmarinePhysicsBindingState
                {
                    LinearVelocity = body.linearVelocity,
                    AngularVelocity = body.angularVelocity,
                    CenterOfMass = body.worldCenterOfMass
                };
            }
            else
            {
                _physicsBindingsNative[0] = default;
            }

            _gridStatesNative[0] = new SubmarineGridState
            {
                HasStructuralGrid = structuralGrid != null && structuralGrid.IsReady ? (byte)1 : (byte)0,
                HasFluidDynamics = fluidDynamics != null && fluidDynamics.isActiveAndEnabled ? (byte)1 : (byte)0,
                HasAtmosphereSystem = atmosphereSystem != null && atmosphereSystem.isActiveAndEnabled ? (byte)1 : (byte)0,
                IsTransportPlatformActive = IsTransportPlatformActive ? (byte)1 : (byte)0
            };

            float totalBreachArea = 0f;
            float maxCompartmentBreachArea = 0f;
            int compartmentCount = 0;
            if (structuralGrid != null && structuralGrid.IsReady && fluidDynamics != null)
            {
                compartmentCount = fluidDynamics.CompartmentCount;
                for (int i = 0; i < compartmentCount; i++)
                {
                    float breachArea = math.max(0f, structuralGrid.GetCompartmentBreachAreaSquareMeters(i));
                    totalBreachArea += breachArea;
                    maxCompartmentBreachArea = math.max(maxCompartmentBreachArea, breachArea);
                }
            }

            _hullIntegritySummaryNative[HullSummaryTotalBreachArea] = totalBreachArea;
            _hullIntegritySummaryNative[HullSummaryMaxCompartmentBreachArea] = maxCompartmentBreachArea;
            _hullIntegritySummaryNative[HullSummaryCompartmentCount] = compartmentCount;
            _hullIntegritySummaryNative[HullSummaryReadyFlag] = structuralGrid != null && structuralGrid.IsReady ? 1f : 0f;
        }

        private void TryRegister()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        private void DisposeNativeState()
        {
            if (_hullIntegritySummaryNative.IsCreated)
            {
                _hullIntegritySummaryNative.Dispose();
                _hullIntegritySummaryNative = default;
            }

            if (_physicsBindingsNative.IsCreated)
            {
                _physicsBindingsNative.Dispose();
                _physicsBindingsNative = default;
            }

            if (_gridStatesNative.IsCreated)
            {
                _gridStatesNative.Dispose();
                _gridStatesNative = default;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }
}
