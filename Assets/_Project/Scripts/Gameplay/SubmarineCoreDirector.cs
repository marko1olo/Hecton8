using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton.Localization;
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
        private const int UpgradeSlotCount = 4;
        private const float DefaultBaseMassKilograms = 1200f;
        private const float DefaultMaxThrustNewtons = 16000f;
        private const float DefaultTurnSpeedDegreesPerSecond = 35f;
        private const float DefaultMaxDepthMeters = 400f;
        private const float DefaultBaseIntegrity = 250f;
        private const float PressureCompensatorDepthBonusMeters = 220f;
        private const float AbyssalStabilizerDepthBonusMeters = 110f;
        private const float HullArmorIntegrityBonus = 55f;
        private const float ShockMountIntegrityBonus = 22f;
        private const float EngineOverdriveThrustMultiplier = 1.18f;
        private const float BallastOptimizerThrustMultiplier = 1.08f;
        private const float ReactorBypassThrustMultiplier = 1.06f;
        private const float EngineOverdriveTurnMultiplier = 1.08f;
        private const float BallastOptimizerTurnMultiplier = 1.12f;
        private const float AbyssalStabilizerTurnMultiplier = 1.05f;
        private const int MaxRegisteredSubmarineRoots = 8;

        private static readonly int _PressureCompensatorHashId = LocHash.Compute("Comp_PressureCompensator");
        private static readonly int _EngineOverdriveHashId = LocHash.Compute("Comp_EngineOverdriveManifold");
        private static readonly int _HullArmorLatticeHashId = LocHash.Compute("Comp_HullArmorLattice");
        private static readonly int _ShockMountArrayHashId = LocHash.Compute("Comp_ShockMountArray");
        private static readonly int _BallastOptimizerHashId = LocHash.Compute("Comp_BallastOptimizer");
        private static readonly int _ReactorBypassCouplerHashId = LocHash.Compute("Comp_ReactorBypassCoupler");
        private static readonly int _AbyssalStabilizerHashId = LocHash.Compute("Comp_AbyssalStabilizer");

        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.Submarine.CoreDirector.FixedTick");
        // COLD ALLOC: RegistryBucket<SubmarineCoreDirector>[8] - active submarine roots for runtime installers without scene scans - owner: SubmarineCoreDirector
        private static readonly RegistryBucket<SubmarineCoreDirector> _registeredSubmarineRoots = new RegistryBucket<SubmarineCoreDirector>(MaxRegisteredSubmarineRoots);

        [Header("── Vehicle Profile ────────────────")]
        [Tooltip("Authored baseline hull, thrust, turn, depth, and integrity data for this submarine.")]
        [SerializeField] private SubmarineProfile submarineProfile;

        [Header("── Upgrade Slots ──────────────────")]
        [Tooltip("Fixed generic upgrade slots storing installed submarine item hash IDs.")]
        [SerializeField] private int[] installedUpgradeItemHashIds = new int[UpgradeSlotCount];

        [Header("â”€â”€ Frame â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Optional explicit transform used as the rider-space reference frame. Defaults to this root transform.")]
        [SerializeField] private Transform platformFrame;

        [Tooltip("When true, player yaw inherits submarine hull rotation through the shared transport pipeline.")]
        [SerializeField] private bool inheritPlatformRotation = true;

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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
        private bool _registeredRuntimeRoot;
        private bool _profileMassApplied;

        // COLD ALLOC: NativeArray<float>[4] â€” submarine root hull summary buffer for registry-facing readback without crawling child systems â€” owner: SubmarineCoreDirector
        private NativeArray<float> _hullIntegritySummaryNative;
        // COLD ALLOC: NativeArray<SubmarinePhysicsBindingState>[1] â€” authoritative rigidbody motion snapshot for submarine consumers â€” owner: SubmarineCoreDirector
        private NativeArray<SubmarinePhysicsBindingState> _physicsBindingsNative;
        // COLD ALLOC: NativeArray<SubmarineGridState>[1] â€” subsystem readiness flags packed at the submarine root â€” owner: SubmarineCoreDirector
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

        /// <summary>Baseline authored submarine stat asset.</summary>
        public SubmarineProfile Profile => submarineProfile;

        /// <summary>Number of active submarine roots registered through lifecycle events.</summary>
        internal static int RegisteredRootCount => _registeredSubmarineRoots.Count;

        /// <summary>Resolved submarine hull mass in kilograms after profile evaluation.</summary>
        public float BaseMass => submarineProfile != null ? submarineProfile.BaseMass : DefaultBaseMassKilograms;

        /// <summary>Resolved maximum submarine thrust in Newtons after upgrade modifiers.</summary>
        public float MaxThrust => ResolveMaxThrust();

        /// <summary>Resolved yaw-turn speed in degrees per second after upgrade modifiers.</summary>
        public float TurnSpeed => ResolveTurnSpeed();

        /// <summary>Resolved certified operating depth in meters after upgrade modifiers.</summary>
        public float MaxDepth => ResolveMaxDepth();

        /// <summary>Resolved structural integrity ceiling after upgrade modifiers.</summary>
        public float BaseIntegrity => ResolveBaseIntegrity();

        /// <summary>
        /// Returns the active submarine root at a dense registry index.
        /// </summary>
        /// <param name="index">Dense registry index.</param>
        /// <returns>Registered submarine root, or null when the index is invalid in development builds.</returns>
        internal static SubmarineCoreDirector GetRegisteredRootAt(int index)
        {
            return _registeredSubmarineRoots.GetAt(index);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            EnsureUpgradeSlots();
            CacheReferences();
            ApplyProfileMassToHull();
            EnsureNativeState();
            RefreshNativeState();
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            EnsureUpgradeSlots();
            CacheReferences();
            ApplyProfileMassToHull();
            EnsureNativeState();
            RefreshNativeState();
            TryRegisterRuntimeRoot();
            GlobalRegistry.RegisterSubmarine(this);
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntimeRoot();
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntimeRoot();
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

        /// <summary>
        /// Returns the installed item hash ID in one of the four fixed upgrade slots.
        /// </summary>
        public int GetInstalledUpgradeHash(int slotIndex)
        {
            EnsureUpgradeSlots();
            if ((uint)slotIndex >= UpgradeSlotCount)
                return 0;

            return installedUpgradeItemHashIds[slotIndex];
        }

        /// <summary>
        /// Installs an upgrade item hash into a fixed slot.
        /// </summary>
        public bool TryInstallUpgrade(int slotIndex, int itemHashId)
        {
            EnsureUpgradeSlots();
            if ((uint)slotIndex >= UpgradeSlotCount)
                return false;

            installedUpgradeItemHashIds[slotIndex] = itemHashId;
            ApplyProfileMassToHull();
            RefreshNativeState();
            return true;
        }

        /// <summary>
        /// Clears one fixed submarine upgrade slot.
        /// </summary>
        public void ClearUpgradeSlot(int slotIndex)
        {
            EnsureUpgradeSlots();
            if ((uint)slotIndex >= UpgradeSlotCount)
                return;

            installedUpgradeItemHashIds[slotIndex] = 0;
            ApplyProfileMassToHull();
            RefreshNativeState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cachedTransform = transform;
            EnsureUpgradeSlots();
            CacheReferences();
            ApplyProfileMassToHull();
            if (Application.isPlaying)
            {
                EnsureNativeState();
                RefreshNativeState();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubmarineRootRegistry()
        {
            _registeredSubmarineRoots.Clear();
        }

        private void TryRegisterRuntimeRoot()
        {
            if (_registeredRuntimeRoot || !Application.isPlaying)
                return;

            _registeredRuntimeRoot = _registeredSubmarineRoots.TryRegister(this);
        }

        private void TryUnregisterRuntimeRoot()
        {
            if (!_registeredRuntimeRoot)
                return;

            _registeredSubmarineRoots.Unregister(this);
            _registeredRuntimeRoot = false;
        }

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

        private void EnsureUpgradeSlots()
        {
            if (installedUpgradeItemHashIds == null || installedUpgradeItemHashIds.Length != UpgradeSlotCount)
            {
                int[] resizedSlots = new int[UpgradeSlotCount]; // COLD ALLOC: int[4] — fixed submarine upgrade-slot state — owner: SubmarineCoreDirector
                if (installedUpgradeItemHashIds != null)
                {
                    int copyCount = math.min(installedUpgradeItemHashIds.Length, UpgradeSlotCount);
                    for (int i = 0; i < copyCount; i++)
                        resizedSlots[i] = installedUpgradeItemHashIds[i];
                }

                installedUpgradeItemHashIds = resizedSlots;
            }
        }

        private void ApplyProfileMassToHull()
        {
            if (hullRigidbody == null)
            {
                _profileMassApplied = false;
                return;
            }

            float resolvedMass = math.max(1f, BaseMass);
            if (_profileMassApplied && math.abs(hullRigidbody.mass - resolvedMass) <= 0.01f)
                return;

            hullRigidbody.mass = resolvedMass;
            _profileMassApplied = true;
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
            if (_registeredFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

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

        private uint ComposeInstalledUpgradeMask()
        {
            EnsureUpgradeSlots();

            uint mask = 0u;
            for (int i = 0; i < UpgradeSlotCount; i++)
            {
                int itemHashId = installedUpgradeItemHashIds[i];
                if (itemHashId == _PressureCompensatorHashId)
                    mask |= (uint)VehicleUpgradeBits.PressureCompensator;
                else if (itemHashId == _EngineOverdriveHashId)
                    mask |= (uint)VehicleUpgradeBits.EngineOverdrive;
                else if (itemHashId == _HullArmorLatticeHashId)
                    mask |= (uint)VehicleUpgradeBits.HullArmorLattice;
                else if (itemHashId == _ShockMountArrayHashId)
                    mask |= (uint)VehicleUpgradeBits.ShockMountArray;
                else if (itemHashId == _BallastOptimizerHashId)
                    mask |= (uint)VehicleUpgradeBits.BallastOptimizer;
                else if (itemHashId == _ReactorBypassCouplerHashId)
                    mask |= (uint)VehicleUpgradeBits.ReactorBypassCoupler;
                else if (itemHashId == _AbyssalStabilizerHashId)
                    mask |= (uint)VehicleUpgradeBits.AbyssalStabilizer;
            }

            return mask;
        }

        private float ResolveMaxThrust()
        {
            uint mask = ComposeInstalledUpgradeMask();
            float thrust = submarineProfile != null ? submarineProfile.MaxThrust : DefaultMaxThrustNewtons;
            if ((mask & (uint)VehicleUpgradeBits.EngineOverdrive) != 0u)
                thrust *= EngineOverdriveThrustMultiplier;
            if ((mask & (uint)VehicleUpgradeBits.BallastOptimizer) != 0u)
                thrust *= BallastOptimizerThrustMultiplier;
            if ((mask & (uint)VehicleUpgradeBits.ReactorBypassCoupler) != 0u)
                thrust *= ReactorBypassThrustMultiplier;
            return math.max(0f, thrust);
        }

        private float ResolveTurnSpeed()
        {
            uint mask = ComposeInstalledUpgradeMask();
            float turnSpeed = submarineProfile != null ? submarineProfile.TurnSpeed : DefaultTurnSpeedDegreesPerSecond;
            if ((mask & (uint)VehicleUpgradeBits.EngineOverdrive) != 0u)
                turnSpeed *= EngineOverdriveTurnMultiplier;
            if ((mask & (uint)VehicleUpgradeBits.BallastOptimizer) != 0u)
                turnSpeed *= BallastOptimizerTurnMultiplier;
            if ((mask & (uint)VehicleUpgradeBits.AbyssalStabilizer) != 0u)
                turnSpeed *= AbyssalStabilizerTurnMultiplier;
            return math.max(0f, turnSpeed);
        }

        private float ResolveMaxDepth()
        {
            uint mask = ComposeInstalledUpgradeMask();
            float maxDepth = submarineProfile != null ? submarineProfile.MaxDepth : DefaultMaxDepthMeters;
            if ((mask & (uint)VehicleUpgradeBits.PressureCompensator) != 0u)
                maxDepth += PressureCompensatorDepthBonusMeters;
            if ((mask & (uint)VehicleUpgradeBits.AbyssalStabilizer) != 0u)
                maxDepth += AbyssalStabilizerDepthBonusMeters;
            return math.max(0f, maxDepth);
        }

        private float ResolveBaseIntegrity()
        {
            uint mask = ComposeInstalledUpgradeMask();
            float integrity = submarineProfile != null ? submarineProfile.BaseIntegrity : DefaultBaseIntegrity;
            if ((mask & (uint)VehicleUpgradeBits.HullArmorLattice) != 0u)
                integrity += HullArmorIntegrityBonus;
            if ((mask & (uint)VehicleUpgradeBits.ShockMountArray) != 0u)
                integrity += ShockMountIntegrityBonus;
            return math.max(1f, integrity);
        }
    }
}
