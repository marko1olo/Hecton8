using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton.Localization;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

using SubmarineFluidDynamics = global::Hecton8.Physics.SubmarineFluidDynamics;
using SubmarineStructuralGrid = global::Hecton8.Physics.SubmarineStructuralGrid;

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
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Core Director")]
    public sealed class SubmarineCoreDirector : MonoBehaviour, ISubmarineRuntimeContext, IFixedTickable, IGlobalRegistryHotSwapListener
    {
        public static class SubmarineGridStateBits
        {
            public const uint StructuralGrid = 1u << 0;
            public const uint FluidDynamics = 1u << 1;
            public const uint AtmosphereSystem = 1u << 2;
            public const uint TransportPlatformActive = 1u << 3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct SubmarinePhysicsBindingState
        {
            [FieldOffset(0)] public float3 LinearVelocity;
            [FieldOffset(12)] public float3 AngularVelocity;
            [FieldOffset(24)] public float3 CenterOfMass;
            [FieldOffset(36)] private uint _pad0;
            [FieldOffset(40)] private ulong _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct SubmarineGridState
        {
            [FieldOffset(0)] public uint StatusFlags;
            [FieldOffset(4)] private uint _pad0;
            [FieldOffset(8)] private ulong _pad1;
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
        private const float MinThermalSpeedMultiplier = 0.1f;
        private const float MaxThermalSpeedMultiplier = 1f;
        private const int MaxRegisteredSubmarineRoots = 8;
        private const SystemID VaultOwnerSystemId = SystemID.VehiclesPhysics;
        private const BufferID HullIntegritySummaryBufferId = BufferID.SubmarineCoreHullIntegritySummary;
        private const BufferID PhysicsBindingBufferId = BufferID.SubmarineCorePhysicsBinding;
        private const BufferID GridStateBufferId = BufferID.SubmarineCoreGridState;

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

        [Tooltip("Legacy fallback only. SHINOBU kinematic dynamics should be authoritative for new submarines.")]
        [SerializeField] private bool enableLegacyPhysXAutoLevelInstall;

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
        private SubmarineAutoLevelBallastController _ballastController;
        private bool _registeredFixedTick;
        private bool _registeredRuntimeRoot;
        private bool _profileMassApplied;
        private float _thermalSpeedMultiplier = 1f;

        // COLD ALLOC: NativeArray<float>[4] â€” submarine root hull summary buffer for registry-facing readback without crawling child systems â€” owner: SubmarineCoreDirector
        private VaultGenerationHandle<float> _hullIntegritySummaryHandle;
        // COLD ALLOC: NativeArray<SubmarinePhysicsBindingState>[1] â€” authoritative rigidbody motion snapshot for submarine consumers â€” owner: SubmarineCoreDirector
        private VaultGenerationHandle<SubmarinePhysicsBindingState> _physicsBindingsHandle;
        // COLD ALLOC: NativeArray<SubmarineGridState>[1] â€” subsystem readiness flags packed at the submarine root â€” owner: SubmarineCoreDirector
        private VaultGenerationHandle<SubmarineGridState> _gridStatesHandle;
        private IDataVault _dataVault;

        /// <inheritdoc />
        public bool IsTransportPlatformActive => isActiveAndEnabled && PlatformTransform != null;

        /// <inheritdoc />
        public Transform PlatformTransform => platformFrame != null ? platformFrame : _cachedTransform;

        /// <inheritdoc />
        public bool InheritPlatformRotation => inheritPlatformRotation;

        /// <inheritdoc />
        public Rigidbody HullRigidbody => hullRigidbody;

        /// <inheritdoc />
        public SubmarineFluidDynamics FluidDynamics => fluidDynamics;

        /// <inheritdoc />
        public IWaterHeatInjectionService WaterHeatInjectionService => fluidDynamics;

        /// <inheritdoc />
        public ISubmarineAtmosphereRoomReadModel AtmosphereSystem => atmosphereSystem;

        /// <inheritdoc />
        public SubmarineStructuralGrid StructuralGrid => structuralGrid;

        /// <inheritdoc />
        public float ThermalSpeedMultiplier => _thermalSpeedMultiplier;

        /// <summary>Published hull summary owned by the submarine root.</summary>
        public NativeArray<float>.ReadOnly HullIntegritySummaryNative =>
            TryReadHullIntegritySummary(out NativeArray<float>.ReadOnly summary) ? summary : default;

        /// <summary>Published rigidbody motion snapshot owned by the submarine root.</summary>
        public NativeArray<SubmarinePhysicsBindingState>.ReadOnly PhysicsBindingsNative =>
            TryReadPhysicsBindings(out NativeArray<SubmarinePhysicsBindingState>.ReadOnly bindings) ? bindings : default;

        /// <summary>Published subsystem readiness snapshot owned by the submarine root.</summary>
        public NativeArray<SubmarineGridState>.ReadOnly GridStatesNative =>
            TryReadGridStates(out NativeArray<SubmarineGridState>.ReadOnly gridStates) ? gridStates : default;

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

        /// <inheritdoc />
        public float MaxDepthMeters => ResolveMaxDepth();

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
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            TryUnregisterRuntimeRoot();
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
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
        public void SetThermalSpeedMultiplier(float multiplier)
        {
            float safeMultiplier = math.isfinite(multiplier)
                ? math.clamp(multiplier, MinThermalSpeedMultiplier, MaxThermalSpeedMultiplier)
                : 1f;
            if (math.abs(_thermalSpeedMultiplier - safeMultiplier) <= 0.001f)
                return;

            _thermalSpeedMultiplier = safeMultiplier;
            RefreshNativeState();
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

        public bool TrySubmitBallastLeverAngle(float leverAngleDegrees, uint sourceHash)
        {
            SubmarineAutoLevelBallastController controller = _ballastController;
            return controller != null && controller.TrySubmitSomaticBallastLever(leverAngleDegrees, sourceHash);
        }

        public bool TryRecordVesselMaintenanceAction(uint panelBitIndex, uint sourceHash)
        {
            SubmarineAutoLevelBallastController controller = _ballastController;
            return controller != null && controller.TryRecordVesselMaintenanceAction(panelBitIndex, sourceHash);
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
            if (Application.isPlaying && HasNativeState())
            {
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

            if (_ballastController == null)
                TryGetComponent(out _ballastController);

            if (Application.isPlaying &&
                enableLegacyPhysXAutoLevelInstall &&
                _ballastController == null)
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                _ballastController = gameObject.AddComponent<SubmarineAutoLevelBallastController>();
            }
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

        private bool EnsureNativeState()
        {
            IDataVault vault = CacheDataVaultCold();
            return vault != null &&
                   EnsureSubmarineVaultBuffer(ref _hullIntegritySummaryHandle, HullIntegritySummaryBufferId, HullSummarySlotCount) &&
                   EnsureSubmarineVaultBuffer(ref _physicsBindingsHandle, PhysicsBindingBufferId, 1) &&
                   EnsureSubmarineVaultBuffer(ref _gridStatesHandle, GridStateBufferId, 1);
        }

        private void RefreshNativeState()
        {
            if (!TryResolveNativeStateWriteBuffers(
                    out NativeArray<float> hullIntegritySummary,
                    out NativeArray<SubmarinePhysicsBindingState> physicsBindings,
                    out NativeArray<SubmarineGridState> gridStates))
            {
                return;
            }

            Rigidbody body = hullRigidbody;
            if (body != null)
            {
                physicsBindings[0] = new SubmarinePhysicsBindingState
                {
                    LinearVelocity = body.linearVelocity,
                    AngularVelocity = body.angularVelocity,
                    CenterOfMass = body.worldCenterOfMass
                };
            }
            else
            {
                physicsBindings[0] = default;
            }

            gridStates[0] = new SubmarineGridState
            {
                StatusFlags =
                    (structuralGrid != null && structuralGrid.IsReady ? SubmarineGridStateBits.StructuralGrid : 0u) |
                    (fluidDynamics != null && fluidDynamics.isActiveAndEnabled ? SubmarineGridStateBits.FluidDynamics : 0u) |
                    (atmosphereSystem != null && atmosphereSystem.isActiveAndEnabled ? SubmarineGridStateBits.AtmosphereSystem : 0u) |
                    (IsTransportPlatformActive ? SubmarineGridStateBits.TransportPlatformActive : 0u)
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

            hullIntegritySummary[HullSummaryTotalBreachArea] = totalBreachArea;
            hullIntegritySummary[HullSummaryMaxCompartmentBreachArea] = maxCompartmentBreachArea;
            hullIntegritySummary[HullSummaryCompartmentCount] = compartmentCount;
            hullIntegritySummary[HullSummaryReadyFlag] = structuralGrid != null && structuralGrid.IsReady ? 1f : 0f;
        }

        private void TryRegister()
        {
            if (_registeredFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DisposeNativeState(previousService as IDataVault);
                _dataVault = currentService as IDataVault;
                if (isActiveAndEnabled)
                {
                    EnsureNativeState();
                    RefreshNativeState();
                }
            }
        }

        private void DisposeNativeState()
        {
            DisposeNativeState(_dataVault);
        }

        private void DisposeNativeState(IDataVault vault)
        {
            ReleaseSubmarineVaultHandle(vault, ref _gridStatesHandle);
            ReleaseSubmarineVaultHandle(vault, ref _physicsBindingsHandle);
            ReleaseSubmarineVaultHandle(vault, ref _hullIntegritySummaryHandle);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureSubmarineVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            ReleaseSubmarineVaultHandle(vault, ref handle);
            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private bool HasNativeState()
        {
            return TryReadHullIntegritySummary(out _) &&
                   TryReadPhysicsBindings(out _) &&
                   TryReadGridStates(out _);
        }

        private bool TryReadHullIntegritySummary(out NativeArray<float>.ReadOnly summary)
        {
            return TryReadSubmarineVaultBuffer(in _hullIntegritySummaryHandle, HullIntegritySummaryBufferId, HullSummarySlotCount, out summary);
        }

        private bool TryReadPhysicsBindings(out NativeArray<SubmarinePhysicsBindingState>.ReadOnly bindings)
        {
            return TryReadSubmarineVaultBuffer(in _physicsBindingsHandle, PhysicsBindingBufferId, 1, out bindings);
        }

        private bool TryReadGridStates(out NativeArray<SubmarineGridState>.ReadOnly gridStates)
        {
            return TryReadSubmarineVaultBuffer(in _gridStatesHandle, GridStateBufferId, 1, out gridStates);
        }

        private bool TryReadSubmarineVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveNativeStateWriteBuffers(
            out NativeArray<float> hullIntegritySummary,
            out NativeArray<SubmarinePhysicsBindingState> physicsBindings,
            out NativeArray<SubmarineGridState> gridStates)
        {
            hullIntegritySummary = default;
            physicsBindings = default;
            gridStates = default;
            if (!EnsureNativeState())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return IsExactVaultHandle(in _hullIntegritySummaryHandle, HullIntegritySummaryBufferId) &&
                   IsExactVaultHandle(in _physicsBindingsHandle, PhysicsBindingBufferId) &&
                   IsExactVaultHandle(in _gridStatesHandle, GridStateBufferId) &&
                   vault.TryResolveHandle(in _hullIntegritySummaryHandle, out hullIntegritySummary) &&
                   vault.TryResolveHandle(in _physicsBindingsHandle, out physicsBindings) &&
                   vault.TryResolveHandle(in _gridStatesHandle, out gridStates) &&
                   hullIntegritySummary.IsCreated &&
                   hullIntegritySummary.Length >= HullSummarySlotCount &&
                   physicsBindings.IsCreated &&
                   physicsBindings.Length >= 1 &&
                   gridStates.IsCreated &&
                   gridStates.Length >= 1;
        }

        private static void ReleaseSubmarineVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private ulong ComposeInstalledUpgradeMask()
        {
            EnsureUpgradeSlots();

            ulong mask = 0UL;
            for (int i = 0; i < UpgradeSlotCount; i++)
            {
                int itemHashId = installedUpgradeItemHashIds[i];
                mask |= SelectVehicleBit(itemHashId, _PressureCompensatorHashId, VehicleUpgradeBits.PressureCompensator) |
                        SelectVehicleBit(itemHashId, _EngineOverdriveHashId, VehicleUpgradeBits.EngineOverdrive) |
                        SelectVehicleBit(itemHashId, _HullArmorLatticeHashId, VehicleUpgradeBits.HullArmorLattice) |
                        SelectVehicleBit(itemHashId, _ShockMountArrayHashId, VehicleUpgradeBits.ShockMountArray) |
                        SelectVehicleBit(itemHashId, _BallastOptimizerHashId, VehicleUpgradeBits.BallastOptimizer) |
                        SelectVehicleBit(itemHashId, _ReactorBypassCouplerHashId, VehicleUpgradeBits.ReactorBypassCoupler) |
                        SelectVehicleBit(itemHashId, _AbyssalStabilizerHashId, VehicleUpgradeBits.AbyssalStabilizer);
            }

            return mask;
        }

        private static ulong SelectVehicleBit(int itemHashId, int expectedHashId, VehicleUpgradeBits bit)
        {
            ulong selected = (ulong)math.select(0, 1, itemHashId == expectedHashId);
            return (ulong)bit & (0UL - selected);
        }

        private static float SelectUpgradeMultiplier(float multiplier, float enabled01)
        {
            return 1f + ((math.max(0.0001f, multiplier) - 1f) * enabled01);
        }

        private static float UpgradeBit01(ulong mask, VehicleUpgradeBits bit)
        {
            return math.select(0f, 1f, (mask & (ulong)bit) != 0UL);
        }

        private float ResolveMaxThrust()
        {
            ulong mask = ComposeInstalledUpgradeMask();
            float thrust = submarineProfile != null ? submarineProfile.MaxThrust : DefaultMaxThrustNewtons;
            thrust *= SelectUpgradeMultiplier(EngineOverdriveThrustMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.EngineOverdrive));
            thrust *= SelectUpgradeMultiplier(BallastOptimizerThrustMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.BallastOptimizer));
            thrust *= SelectUpgradeMultiplier(ReactorBypassThrustMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.ReactorBypassCoupler));
            thrust *= _thermalSpeedMultiplier;
            return math.max(0f, thrust);
        }

        private float ResolveTurnSpeed()
        {
            ulong mask = ComposeInstalledUpgradeMask();
            float turnSpeed = submarineProfile != null ? submarineProfile.TurnSpeed : DefaultTurnSpeedDegreesPerSecond;
            turnSpeed *= SelectUpgradeMultiplier(EngineOverdriveTurnMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.EngineOverdrive));
            turnSpeed *= SelectUpgradeMultiplier(BallastOptimizerTurnMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.BallastOptimizer));
            turnSpeed *= SelectUpgradeMultiplier(AbyssalStabilizerTurnMultiplier, UpgradeBit01(mask, VehicleUpgradeBits.AbyssalStabilizer));
            return math.max(0f, turnSpeed);
        }

        private float ResolveMaxDepth()
        {
            ulong mask = ComposeInstalledUpgradeMask();
            float maxDepth = submarineProfile != null ? submarineProfile.MaxDepth : DefaultMaxDepthMeters;
            maxDepth += PressureCompensatorDepthBonusMeters * UpgradeBit01(mask, VehicleUpgradeBits.PressureCompensator);
            maxDepth += AbyssalStabilizerDepthBonusMeters * UpgradeBit01(mask, VehicleUpgradeBits.AbyssalStabilizer);
            return math.max(0f, maxDepth);
        }

        private float ResolveBaseIntegrity()
        {
            ulong mask = ComposeInstalledUpgradeMask();
            float integrity = submarineProfile != null ? submarineProfile.BaseIntegrity : DefaultBaseIntegrity;
            integrity += HullArmorIntegrityBonus * UpgradeBit01(mask, VehicleUpgradeBits.HullArmorLattice);
            integrity += ShockMountIntegrityBonus * UpgradeBit01(mask, VehicleUpgradeBits.ShockMountArray);
            return math.max(1f, integrity);
        }
    }
}
