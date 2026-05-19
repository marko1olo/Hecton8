using System;
using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Mining.Contracts;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay.Mining
{
    /// <summary>
    /// Deployable thumper drill runtime. Owns mining inventory, macro hydration, power gating, and typed signal output.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Deployable SDF Drill")]
    public sealed class DeployableSdfDrillRuntime : MonoBehaviour,
        IColdTickable,
        IOriginShiftListener,
        IPoolable,
        ICuttable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IScalabilityChangedEventListener
    {
        private const int InventorySlotCount = 4;
        private const int BlackBoxCapacity = 300;
        private const int SnapCommandCount = 1;
        private const int MaxVaultDrillInstances = 256;
        private const float DefaultPowerDrawWatts = 50000f;
        private const float WirelessDrainQueueCapWattSeconds = 4096f;
        private const float DefaultExtractionCycleSeconds = 60f;
        private const float DefaultMathLodHysteresisSeconds = 3f;
        private const uint DrillToolHash = 0xD2111D8u;
        private const uint DrillDamageTypeHash = 0xD4A611EDu;
        private const uint DrillDebrisSpeciesHash = 0xD211B10Bu;
        private const byte AcousticChannelThumper = 7;
        private const byte AcousticFlagThreat = 1 << 3;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_VAULT_SOVEREIGNTY_ENFORCER_DEPLOYABLE_SDF_DRILL.bin";

        private static int s_activeDrills;

        [Header("Snap")]
        [Tooltip("Physics layers accepted as terrain/seabed when the drill is deployed.")]
        [SerializeField] private LayerMask seabedLayerMask = ~0;
        [Tooltip("Height above the current transform used as the RaycastCommand origin.")]
        [SerializeField] private float snapProbeHeightMeters = 18f;
        [Tooltip("Depth below the current transform searched by the deploy snap probe.")]
        [SerializeField] private float snapProbeDepthMeters = 36f;
        [Tooltip("Minimum terrain normal Y accepted for stable drill placement.")]
        [SerializeField] private float minimumSeabedNormalY = 0.45f;

        [Header("Mining")]
        [Tooltip("Optional authored voxel delta processor. If empty, the cached voxel runtime is used during cold dependency binding.")]
        [SerializeField] private VoxelDeltaProcessor voxelDeltaProcessor;
        [Tooltip("Optional authored voxel volume. If empty, the drill resolves and caches a public SDF hit on the 60 second carve cadence.")]
        [SerializeField] private HectonVoxelVolume explicitVoxelVolume;
        [Tooltip("Power draw required for active mining. Mandate floor is 50kW.")]
        [SerializeField] private float activePowerDrawWatts = DefaultPowerDrawWatts;
        [Tooltip("Minimum generation surplus before the drill leaves dormant state. Mandate floor is 50kW.")]
        [SerializeField] private float powerSurplusThresholdWatts = DefaultPowerDrawWatts;
        [Tooltip("Seconds per mining and visual carve cycle.")]
        [SerializeField] private float extractionCycleSeconds = DefaultExtractionCycleSeconds;
        [Tooltip("Ore units added per successful extraction cycle.")]
        [SerializeField] private ushort quantityPerCycle = 1;
        [Tooltip("Capacity per fixed inventory lane.")]
        [SerializeField] private ushort slotCapacity = 160;
        [Tooltip("Visual SDF sphere radius sent to the voxel delta processor.")]
        [SerializeField] private float carveRadiusMeters = 2.4f;
        [Tooltip("Blend strength used by the downstream voxel carve presentation.")]
        [SerializeField] private float carveBlendStrengthMeters = 0.9f;
        [Tooltip("Maximum distance for resolving the public voxel volume under the drill.")]
        [SerializeField] private float sdfRaymarchDistanceMeters = 12f;
        [Tooltip("Step size for public SDF volume resolution. Used only on the sparse carve cadence.")]
        [SerializeField] private float sdfRaymarchStepMeters = 0.35f;
        [Tooltip("When true, Low/MX350 skips visible SDF carve packets while extraction continues.")]
        [SerializeField] private bool skipSdfVisualOnLowTier = true;
        [Tooltip("Minimum seconds before a scalability tier switch changes drill math or visual output.")]
        [SerializeField] private float mathLodHysteresisSeconds = DefaultMathLodHysteresisSeconds;

        [Header("Threat")]
        [Tooltip("Acoustic investigation radius emitted while the drill is powered.")]
        [SerializeField] private float acousticRadiusMeters = 420f;
        [Tooltip("Normalized thumper intensity emitted while the drill is powered.")]
        [SerializeField] private float acousticIntensity01 = 0.82f;
        [Tooltip("Damage capacity before the drill breaks and dumps its blackbox.")]
        [SerializeField] private float maxHealth = 800f;

        [Header("Ore Hashes")]
        [Tooltip("Item hash emitted by inventory slot 0.")]
        [SerializeField] private uint slot0ItemHash = 0xB457A17Eu;
        [Tooltip("Item hash emitted by inventory slot 1.")]
        [SerializeField] private uint slot1ItemHash = 0xC0FFEE12u;
        [Tooltip("Item hash emitted by inventory slot 2.")]
        [SerializeField] private uint slot2ItemHash = 0x717A1E1Du;
        [Tooltip("Item hash emitted by inventory slot 3.")]
        [SerializeField] private uint slot3ItemHash = 0x0C2157A1u;
        [Tooltip("Ore hash seed emitted by inventory slot 0.")]
        [SerializeField] private uint slot0OreHash = 0xA826F165u;
        [Tooltip("Ore hash seed emitted by inventory slot 1.")]
        [SerializeField] private uint slot1OreHash = 0xA826F166u;
        [Tooltip("Ore hash seed emitted by inventory slot 2.")]
        [SerializeField] private uint slot2OreHash = 0xA826F167u;
        [Tooltip("Ore hash seed emitted by inventory slot 3.")]
        [SerializeField] private uint slot3OreHash = 0xA826F168u;

        [Header("Diegetic Screen")]
        [Tooltip("Optional TMP label updated through SetCharArray with the internal inventory fill percentage.")]
        [SerializeField] private TMP_Text fillPercentageLabel;

        // COLD ALLOC: char[8] - fixed TMP SetCharArray staging buffer for drill fill percent - owner: DeployableSdfDrillRuntime
        private readonly char[] _fillTextBuffer = new char[8];

        private Transform _cachedTransform;
        private AbsoluteUniversePosition _anchorAup;
        private float3 _anchorRuntimePosition;
        private uint _sourceId;
        private uint _lcgState;
        private uint _sectorHash;
        private float _health;
        private uint _oresExtracted;
        private int _lastFillPercent = -1;
        private double _lastMacroUpdateUnscaledTime;
        private double _lastCarveUnscaledTime;
        private bool _registeredColdTick;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _registeredScalability;
        private bool _countedActive;
        private bool _snapPending;
        private bool _snappedToTerrain;
        private bool _extractionPending;
        private bool _broken;
        private bool _faultDumped;
        private DeployableSdfDrillFlags _stateFlags;
        private IPowerGridService _powerGrid;
        private VoxelDeltaProcessor _cachedVoxelDeltaProcessor;
        private MapMagicBridge _mapMagic;
        private DeployableSdfDrillMathLod _cachedMathLod = DeployableSdfDrillMathLod.Low;
        private DeployableSdfDrillMathLod _targetMathLod = DeployableSdfDrillMathLod.Low;
        private double _mathLodChangeEligibleUnscaledTime;
        private HectonVoxelVolume _resolvedVoxelVolume;
        private JobHandle _snapHandle;
        private JobHandle _extractionHandle;

        private int _vaultSlotIndex = -1;
        private VaultBufferHandle<uint> _slotOwnersHandle;
        private VaultBufferHandle<ushort> _inventoryQuantitiesHandle;
        private VaultBufferHandle<ushort> _inventoryCapacitiesHandle;
        private VaultBufferHandle<uint> _inventoryItemHashesHandle;
        private VaultBufferHandle<uint> _inventoryOreHashesHandle;
        private VaultBufferHandle<DeployableSdfDrillExtractionResult> _extractionResultHandle;
        private VaultBufferHandle<DeployableSdfDrillTelemetryEntry> _blackBoxHandle;
        private VaultBufferHandle<RaycastCommand> _snapCommandsHandle;
        private VaultBufferHandle<RaycastHit> _snapHitsHandle;
        private int _blackBoxCursor;

        /// <summary>True once the drill has been destroyed by damage or a fatal state fault.</summary>
        public bool IsBroken => _broken;

        /// <summary>True while the drill is unable to reserve the required power budget.</summary>
        public bool IsDormant => (_stateFlags & DeployableSdfDrillFlags.DormantNoPower) != 0;

        /// <summary>True after the terrain snap ray has found an acceptable seabed normal.</summary>
        public bool IsSnapped => _snappedToTerrain;

        /// <summary>Total ore units produced by this runtime instance.</summary>
        public uint OresExtracted => _oresExtracted;

        /// <summary>Last computed internal inventory fill percentage.</summary>
        public int InventoryFillPercent => _lastFillPercent >= 0 ? _lastFillPercent : 0;

        private void Awake()
        {
            _cachedTransform = transform;
            _sourceId = unchecked((uint)UnityEngine.EntityId.ToULong(gameObject.GetEntityId()));
            _lcgState = DeployableSdfDrillMath.Mix(_sourceId, DrillToolHash);
            _health = math.max(1f, maxHealth);
            AllocateNativeState();
            ConfigureInventorySlots();
            CaptureAnchorFromTransform();
        }

        private void OnValidate()
        {
            snapProbeHeightMeters = SanitizeAtLeast(snapProbeHeightMeters, 0.1f, 18f);
            snapProbeDepthMeters = SanitizeAtLeast(snapProbeDepthMeters, 0.1f, 36f);
            minimumSeabedNormalY = SanitizeRange(minimumSeabedNormalY, 0f, 1f, 0.45f);
            activePowerDrawWatts = SanitizeAtLeast(activePowerDrawWatts, DefaultPowerDrawWatts, DefaultPowerDrawWatts);
            powerSurplusThresholdWatts = SanitizeAtLeast(powerSurplusThresholdWatts, DefaultPowerDrawWatts, DefaultPowerDrawWatts);
            extractionCycleSeconds = SanitizeAtLeast(extractionCycleSeconds, 1f, DefaultExtractionCycleSeconds);
            quantityPerCycle = (ushort)math.max(1, (int)quantityPerCycle);
            slotCapacity = (ushort)math.max(1, (int)slotCapacity);
            carveRadiusMeters = SanitizeRange(carveRadiusMeters, 0.25f, 8f, 2.4f);
            carveBlendStrengthMeters = SanitizeAtLeast(carveBlendStrengthMeters, 0.05f, 0.9f);
            sdfRaymarchDistanceMeters = SanitizeAtLeast(sdfRaymarchDistanceMeters, 1f, 12f);
            sdfRaymarchStepMeters = SanitizeRange(sdfRaymarchStepMeters, 0.05f, 2f, 0.35f);
            mathLodHysteresisSeconds = SanitizeAtLeast(mathLodHysteresisSeconds, DefaultMathLodHysteresisSeconds, DefaultMathLodHysteresisSeconds);
            acousticRadiusMeters = SanitizeAtLeast(acousticRadiusMeters, 1f, 420f);
            acousticIntensity01 = SanitizeRange(acousticIntensity01, 0f, 1f, 0.82f);
            maxHealth = SanitizeAtLeast(maxHealth, 1f, 800f);

            if (Application.isPlaying && !_extractionPending && TryResolveInventoryState(out _, out _, out _, out _))
                ConfigureInventorySlots();
        }

        private void OnEnable()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            AllocateNativeState();
            ConfigureInventorySlots();
            CacheRuntimeDependencies();
            if (_lastMacroUpdateUnscaledTime <= 0.0001d)
                _lastMacroUpdateUnscaledTime = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (_lastCarveUnscaledTime <= 0.0001d)
                _lastCarveUnscaledTime = _lastMacroUpdateUnscaledTime;

            RegisterRuntimeHooks();
            CaptureAnchorFromTransform();
            ScheduleTerrainSnap();
            RegisterActiveInstance();
        }

        private void OnDisable()
        {
            CancelTerrainSnap();
            CompleteExtractionJob(true);
            UnregisterRuntimeHooks();
            ReleaseActiveInstance();
        }

        private void OnDestroy()
        {
            CancelTerrainSnap();
            CompleteExtractionJob(true);
            UnregisterRuntimeHooks();
            ReleaseActiveInstance();
            DisposeNativeState();
        }

        /// <summary>
        /// Resets pooled drill state after the object pool activates the prefab.
        /// </summary>
        public void OnSpawn()
        {
            AllocateNativeState();
            ConfigureInventorySlots();
            CacheRuntimeDependencies();
            _broken = false;
            _faultDumped = false;
            _stateFlags = DeployableSdfDrillFlags.None;
            _health = math.max(1f, maxHealth);
            _oresExtracted = 0u;
            _snappedToTerrain = false;
            _resolvedVoxelVolume = null;
            _lastFillPercent = -1;
            _lastMacroUpdateUnscaledTime = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _lastCarveUnscaledTime = _lastMacroUpdateUnscaledTime;
            ClearInventoryQuantities();
            ClearBlackBox();
            CancelTerrainSnap();
            CaptureAnchorFromTransform();
            ScheduleTerrainSnap();
            UpdateFillLabel(true);
        }

        /// <summary>
        /// Stops jobs and unregisters runtime hooks before the object pool deactivates the prefab.
        /// </summary>
        public void OnDespawn()
        {
            CancelTerrainSnap();
            CompleteExtractionJob(true);
            UnregisterRuntimeHooks();
            ReleaseActiveInstance();
            _stateFlags = DeployableSdfDrillFlags.None;
            _snapPending = false;
            _extractionPending = false;
            _snappedToTerrain = false;
            _broken = false;
            _faultDumped = false;
            _health = math.max(1f, maxHealth);
            _oresExtracted = 0u;
            _lastMacroUpdateUnscaledTime = 0d;
            _lastCarveUnscaledTime = 0d;
            ClearInventoryQuantities();
            ClearBlackBox();
            _resolvedVoxelVolume = null;
            _lastFillPercent = -1;
        }

        /// <summary>
        /// Executes the drill's 1 Hz post-simulation maintenance lane.
        /// </summary>
        public void ColdTick()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            CompleteTerrainSnap(false);
            CompleteExtractionJob(false);
            CaptureAnchorFromTransform();
            UpdateMathLodHysteresis(now);

            if (!ValidateFiniteState())
            {
                SetFlag(DeployableSdfDrillFlags.Broken, true);
                WriteBlackBox(0);
                return;
            }

            DeployableSdfDrillMathLod mathLod = ResolveMathLod();
            bool lowTierSdfSkipped = skipSdfVisualOnLowTier && mathLod == DeployableSdfDrillMathLod.Low;
            SetFlag(DeployableSdfDrillFlags.LowTierSdfSkipped, lowTierSdfSkipped);

            bool hasPower = !_broken && _snappedToTerrain && TryReservePower();
            SetFlag(DeployableSdfDrillFlags.DormantNoPower, !hasPower);
            SetFlag(DeployableSdfDrillFlags.Active, hasPower);
            SetFlag(DeployableSdfDrillFlags.Snapped, _snappedToTerrain);

            if (!hasPower)
            {
                UpdateFillLabel(false);
                WriteBlackBox(0);
                return;
            }

            ScheduleExtractionJob(now, mathLod, ResolveMaxRuntimeCycles(mathLod));
            PublishAcousticThreat();
            if (!lowTierSdfSkipped && now - _lastCarveUnscaledTime >= math.max(1f, extractionCycleSeconds))
            {
                TryEmitVoxelCarveEvent();
                _lastCarveUnscaledTime = now;
            }

            UpdateFillLabel(false);
            WriteBlackBox(0);
        }

        /// <summary>
        /// Reprojects the authoritative AUP anchor after a floating-origin shift.
        /// </summary>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 runtime = _anchorAup.ToRuntimeFloat3();
            if (!IsFiniteVector3(runtime))
            {
                FaultInvalidRuntimePosition();
                return;
            }

            _anchorRuntimePosition = new float3(runtime.x, runtime.y, runtime.z);
            if (_cachedTransform != null)
                _cachedTransform.position = runtime;
        }

        /// <summary>
        /// Rebinds cached service references when GlobalRegistry replaces a service slot.
        /// </summary>
        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGrid = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    RebindVoxelDependencies(currentService as HectonVoxelEngine);
                    break;
            }
        }

        /// <summary>
        /// Compatibility hot-swap callback. Ref callback owns the actual rebind to avoid duplicate work.
        /// </summary>
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
        }

        /// <summary>
        /// Defers drill math LOD transitions through a hysteresis window.
        /// </summary>
        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            DeployableSdfDrillMathLod nextLod = ToMathLod(payload.CurrentQualityTier);
            if (nextLod == _targetMathLod)
                return;

            _targetMathLod = nextLod;
            _mathLodChangeEligibleUnscaledTime = SystemDispatcher.CurrentUnscaledTimeSeconds +
                math.max(DefaultMathLodHysteresisSeconds, mathLodHysteresisSeconds);
        }

        /// <summary>
        /// Applies generic cut damage through the Leviathan-safe damage path.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            ApplyLeviathanDamage(damage, hitPoint, 0u);
        }

        /// <summary>
        /// Applies predator damage and emits typed combat/debris signals when the drill breaks.
        /// </summary>
        public void ApplyLeviathanDamage(float damage, Vector3 hitPoint, uint sourceHash)
        {
            if (!math.isfinite(damage) || damage <= 0f || _broken)
                return;

            Vector3 safeHitPoint = ResolveSafeRuntimePoint(hitPoint);
            _health = math.max(0f, _health - damage);
            PublishCombatDamage(damage, safeHitPoint, sourceHash);
            if (_health <= 0.0001f)
                MarkBroken(safeHitPoint);
        }

        /// <summary>
        /// Captures a blittable macro record for unloaded-sector persistence.
        /// </summary>
        public void CaptureMacroRecord(out DeployableSdfDrillMacroRecord record)
        {
            CompleteExtractionJob(true);
            CaptureAnchorFromTransform();
            bool hasInventory = TryResolveInventoryState(
                out NativeSlice<ushort> quantities,
                out NativeSlice<ushort> capacities,
                out NativeSlice<uint> itemHashes,
                out NativeSlice<uint> oreHashes);

            record = new DeployableSdfDrillMacroRecord
            {
                GridX = _anchorAup.GridX,
                GridY = _anchorAup.GridY,
                GridZ = _anchorAup.GridZ,
                LocalX = _anchorAup.LocalX,
                LocalY = _anchorAup.LocalY,
                LocalZ = _anchorAup.LocalZ,
                LastUnscaledTimeSeconds = _lastMacroUpdateUnscaledTime,
                DrillSeed = _lcgState,
                SectorHash = _sectorHash,
                Health = _health,
                Flags = (ushort)_stateFlags,
                Slot0Quantity = hasInventory ? quantities[0] : (ushort)0,
                Slot1Quantity = hasInventory ? quantities[1] : (ushort)0,
                Slot2Quantity = hasInventory ? quantities[2] : (ushort)0,
                Slot3Quantity = hasInventory ? quantities[3] : (ushort)0,
                Slot0Capacity = hasInventory ? capacities[0] : (ushort)0,
                Slot1Capacity = hasInventory ? capacities[1] : (ushort)0,
                Slot2Capacity = hasInventory ? capacities[2] : (ushort)0,
                Slot3Capacity = hasInventory ? capacities[3] : (ushort)0,
                Slot0ItemHash = hasInventory ? itemHashes[0] : 0u,
                Slot1ItemHash = hasInventory ? itemHashes[1] : 0u,
                Slot2ItemHash = hasInventory ? itemHashes[2] : 0u,
                Slot3ItemHash = hasInventory ? itemHashes[3] : 0u,
                Slot0OreHash = hasInventory ? oreHashes[0] : 0u,
                Slot1OreHash = hasInventory ? oreHashes[1] : 0u,
                Slot2OreHash = hasInventory ? oreHashes[2] : 0u,
                Slot3OreHash = hasInventory ? oreHashes[3] : 0u,
                OresExtracted = _oresExtracted
            };
        }

        /// <summary>
        /// Restores a macro record and applies capped offline extraction.
        /// </summary>
        public void RestoreMacroRecord(in DeployableSdfDrillMacroRecord record)
        {
            CancelTerrainSnap();
            CompleteExtractionJob(true);
            AllocateNativeState();
            CacheRuntimeDependencies();
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _faultDumped = false;
            _stateFlags = DeployableSdfDrillFlags.None;
            _broken = false;
            _snappedToTerrain = false;
            _resolvedVoxelVolume = null;
            ClearInventoryQuantities();

            if (!TryRestoreAnchorFromMacroRecord(in record))
            {
                _lastMacroUpdateUnscaledTime = now;
                _lastCarveUnscaledTime = now;
                FaultInvalidRuntimePosition();
                UpdateFillLabel(true);
                return;
            }

            if (_cachedTransform == null)
                _cachedTransform = transform;
            if (_cachedTransform != null)
                _cachedTransform.position = ToVector3(_anchorRuntimePosition);

            _lcgState = record.DrillSeed != 0u ? record.DrillSeed : DeployableSdfDrillMath.Mix(_sourceId, DrillToolHash);
            _sectorHash = record.SectorHash != 0u ? record.SectorHash : DeployableSdfDrillMath.ResolveSectorHash(
                _anchorAup.GridX,
                _anchorAup.GridY,
                _anchorAup.GridZ,
                _anchorAup.LocalX,
                _anchorAup.LocalY,
                _anchorAup.LocalZ);
            float recordHealth = float.IsFinite(record.Health) ? record.Health : 0f;
            _health = math.clamp(recordHealth, 0f, math.max(1f, maxHealth));
            _broken = _health <= 0.0001f || ((DeployableSdfDrillFlags)record.Flags & DeployableSdfDrillFlags.Broken) != 0;
            _stateFlags = (DeployableSdfDrillFlags)record.Flags;
            _snappedToTerrain = (_stateFlags & DeployableSdfDrillFlags.Snapped) != 0;
            SetFlag(DeployableSdfDrillFlags.Broken, _broken);
            SetFlag(DeployableSdfDrillFlags.Snapped, _snappedToTerrain);
            SetFlag(DeployableSdfDrillFlags.Active, false);
            SetFlag(DeployableSdfDrillFlags.DormantNoPower, false);
            _oresExtracted = record.OresExtracted;
            _lastMacroUpdateUnscaledTime = record.LastUnscaledTimeSeconds > 0d && double.IsFinite(record.LastUnscaledTimeSeconds)
                ? record.LastUnscaledTimeSeconds
                : now;
            _lastCarveUnscaledTime = now;

            RestoreSlot(0, record.Slot0Quantity, record.Slot0Capacity, record.Slot0ItemHash, record.Slot0OreHash);
            RestoreSlot(1, record.Slot1Quantity, record.Slot1Capacity, record.Slot1ItemHash, record.Slot1OreHash);
            RestoreSlot(2, record.Slot2Quantity, record.Slot2Capacity, record.Slot2ItemHash, record.Slot2OreHash);
            RestoreSlot(3, record.Slot3Quantity, record.Slot3Capacity, record.Slot3ItemHash, record.Slot3OreHash);

            ApplyOfflineMacroDelta(now, ResolveMaxOfflineCycles(ResolveMathLod()));
            if (!_broken)
                ScheduleTerrainSnap();
            UpdateFillLabel(true);
        }

        /// <summary>
        /// Places the drill and schedules a terrain snap probe.
        /// </summary>
        public void DeployAt(Vector3 runtimePosition)
        {
            DeployAt(runtimePosition, Quaternion.identity);
        }

        /// <summary>
        /// Places the drill with an authored rotation and schedules a terrain snap probe.
        /// </summary>
        public void DeployAt(Vector3 runtimePosition, Quaternion runtimeRotation)
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (!IsFiniteVector3(runtimePosition) || !IsFiniteQuaternion(runtimeRotation))
            {
                FaultInvalidRuntimePosition();
                return;
            }

            CancelTerrainSnap();
            _cachedTransform.SetPositionAndRotation(runtimePosition, runtimeRotation);
            _snappedToTerrain = false;
            SetFlag(DeployableSdfDrillFlags.Snapped, false);
            _resolvedVoxelVolume = null;
            CaptureAnchorFromTransform();
            ScheduleTerrainSnap();
        }

        /// <summary>
        /// Rebinds GlobalRegistry and bridge dependencies from a cold/manual path.
        /// </summary>
        public void RefreshCachedDependencies()
        {
            CacheRuntimeDependencies();
        }

        private void AllocateNativeState()
        {
            if (!TryPrepareNativeState(
                    out NativeSlice<ushort> quantities,
                    out _,
                    out _,
                    out _,
                    out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox,
                    out NativeArray<RaycastCommand> snapCommands,
                    out NativeArray<RaycastHit> snapHits,
                    out bool assignedNewSlot))
            {
                return;
            }

            if (!assignedNewSlot)
                return;

            ClearSlice(quantities);
            ClearSlice(blackBox);
            ClearNativeArray(snapCommands);
            ClearNativeArray(snapHits);
            if (TryResolveExtractionResultBuffer(out NativeSlice<DeployableSdfDrillExtractionResult> extractionResult))
                extractionResult[0] = default;
        }

        private void ConfigureInventorySlots()
        {
            if (!TryResolveInventoryState(
                    out NativeSlice<ushort> quantities,
                    out NativeSlice<ushort> capacities,
                    out NativeSlice<uint> itemHashes,
                    out NativeSlice<uint> oreHashes))
            {
                return;
            }

            ushort capacity = (ushort)math.max(1, (int)slotCapacity);
            SetSlot(capacities, itemHashes, oreHashes, 0, slot0ItemHash, slot0OreHash, capacity);
            SetSlot(capacities, itemHashes, oreHashes, 1, slot1ItemHash, slot1OreHash, capacity);
            SetSlot(capacities, itemHashes, oreHashes, 2, slot2ItemHash, slot2OreHash, capacity);
            SetSlot(capacities, itemHashes, oreHashes, 3, slot3ItemHash, slot3OreHash, capacity);
            for (int i = 0; i < InventorySlotCount; i++)
            {
                if (quantities[i] > capacities[i])
                    quantities[i] = capacities[i];
            }
        }

        private static void SetSlot(
            NativeSlice<ushort> capacities,
            NativeSlice<uint> itemHashes,
            NativeSlice<uint> oreHashes,
            int index,
            uint itemHash,
            uint oreHash,
            ushort capacity)
        {
            capacities[index] = capacity;
            itemHashes[index] = itemHash != 0u ? itemHash : DeployableSdfDrillMath.DefaultItemHash;
            oreHashes[index] = oreHash != 0u ? oreHash : DeployableSdfDrillMath.DefaultOreHash;
        }

        private void RestoreSlot(int index, ushort quantity, ushort capacity, uint itemHash, uint oreHash)
        {
            if (!TryResolveInventoryState(
                    out NativeSlice<ushort> quantities,
                    out NativeSlice<ushort> capacities,
                    out NativeSlice<uint> itemHashes,
                    out NativeSlice<uint> oreHashes))
            {
                return;
            }

            ushort safeCapacity = capacity > 0 ? capacity : (ushort)math.max(1, (int)slotCapacity);
            capacities[index] = safeCapacity;
            quantities[index] = quantity <= safeCapacity ? quantity : safeCapacity;
            itemHashes[index] = itemHash != 0u ? itemHash : DeployableSdfDrillMath.DefaultItemHash;
            oreHashes[index] = oreHash != 0u ? oreHash : DeployableSdfDrillMath.DefaultOreHash;
        }

        private bool TryRestoreAnchorFromMacroRecord(in DeployableSdfDrillMacroRecord record)
        {
            if (!float.IsFinite(record.LocalX) || !float.IsFinite(record.LocalY) || !float.IsFinite(record.LocalZ))
                return false;

            AbsoluteUniversePosition restoredAup = new AbsoluteUniversePosition
            {
                GridX = record.GridX,
                GridY = record.GridY,
                GridZ = record.GridZ,
                LocalX = record.LocalX,
                LocalY = record.LocalY,
                LocalZ = record.LocalZ
            };
            Vector3 restoredRuntime = restoredAup.ToRuntimeFloat3();
            if (!IsFiniteVector3(restoredRuntime))
                return false;

            _anchorAup = restoredAup;
            _anchorRuntimePosition = new float3(restoredRuntime.x, restoredRuntime.y, restoredRuntime.z);
            return true;
        }

        private void RegisterRuntimeHooks()
        {
            if (!_registeredColdTick && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap && Application.isPlaying)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (!_registeredScalability && Application.isPlaying)
            {
                ScalabilityEvents.Register(this);
                _registeredScalability = true;
            }
        }

        private void UnregisterRuntimeHooks()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            if (_registeredScalability)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalability = false;
            }
        }

        private void CacheRuntimeDependencies()
        {
            _powerGrid = GlobalRegistry.PowerGrid;
            _mapMagic = MapMagicBridge.Instance;
            _cachedMathLod = ToMathLod(GlobalRegistry.ScalabilityTier);
            _targetMathLod = _cachedMathLod;
            _mathLodChangeEligibleUnscaledTime = 0d;
            RebindVoxelDependencies(GlobalRegistry.VoxelEngine);
        }

        private void RebindVoxelDependencies(HectonVoxelEngine voxelEngine)
        {
            _cachedVoxelDeltaProcessor = voxelDeltaProcessor;
            if (_cachedVoxelDeltaProcessor == null && voxelEngine != null)
                voxelEngine.TryGetComponent(out _cachedVoxelDeltaProcessor);

            _resolvedVoxelVolume = null;
        }

        private void RegisterActiveInstance()
        {
            if (_countedActive || !Application.isPlaying)
                return;

            s_activeDrills++;
            _countedActive = true;
        }

        private void ReleaseActiveInstance()
        {
            if (!_countedActive)
                return;

            s_activeDrills = math.max(0, s_activeDrills - 1);
            _countedActive = false;
        }

        private void ScheduleTerrainSnap()
        {
            if (_snapPending)
                return;

            _snappedToTerrain = false;
            SetFlag(DeployableSdfDrillFlags.Snapped, false);

            if (!TryResolveSnapBuffers(out NativeArray<RaycastCommand> snapCommands, out NativeArray<RaycastHit> snapHits) ||
                _cachedTransform == null)
            {
                return;
            }

            Vector3 position = _cachedTransform.position;
            if (!IsFiniteVector3(position))
            {
                FaultInvalidRuntimePosition();
                return;
            }

            QueryParameters parameters = new QueryParameters
            {
                layerMask = seabedLayerMask.value,
                hitTriggers = QueryTriggerInteraction.Ignore,
                hitBackfaces = false,
                hitMultipleFaces = false
            };

            snapCommands[0] = new RaycastCommand
            {
                from = position + Vector3.up * math.max(0.1f, snapProbeHeightMeters),
                direction = Vector3.down,
                distance = math.max(0.1f, snapProbeHeightMeters + snapProbeDepthMeters),
                queryParameters = parameters
            };
            _snapHandle = RaycastCommand.ScheduleBatch(snapCommands, snapHits, SnapCommandCount, default);
            _snapPending = true;
        }

        private void CompleteTerrainSnap(bool forceComplete)
        {
            if (!_snapPending)
                return;

            if (!forceComplete && !_snapHandle.IsCompleted)
                return;

            _snapHandle.Complete();
            _snapPending = false;
            if (!TryResolveSnapBuffers(out _, out NativeArray<RaycastHit> snapHits))
                return;

            RaycastHit hit = snapHits[0];
            if (hit.collider == null)
            {
                _snappedToTerrain = false;
                SetFlag(DeployableSdfDrillFlags.Snapped, false);
                return;
            }

            float3 normal = NormalizeSafe(new float3(hit.normal.x, hit.normal.y, hit.normal.z), math.up());
            if (normal.y < minimumSeabedNormalY)
            {
                _snappedToTerrain = false;
                SetFlag(DeployableSdfDrillFlags.Snapped, false);
                return;
            }

            quaternion rotation = quaternion.LookRotationSafe(normal, math.up());
            _cachedTransform.SetPositionAndRotation(hit.point, ToQuaternion(rotation));
            _snappedToTerrain = true;
            SetFlag(DeployableSdfDrillFlags.Snapped, true);
            CaptureAnchorFromTransform();
        }

        private void CancelTerrainSnap()
        {
            if (!_snapPending)
                return;

            _snapHandle.Complete();
            _snapPending = false;
            if (TryResolveSnapBuffers(out _, out NativeArray<RaycastHit> snapHits))
                snapHits[0] = default;
        }

        private void CaptureAnchorFromTransform()
        {
            if (_cachedTransform == null)
                return;

            Vector3 position = _cachedTransform.position;
            if (!IsFiniteVector3(position))
            {
                FaultInvalidRuntimePosition();
                return;
            }

            _anchorRuntimePosition = new float3(position.x, position.y, position.z);
            _anchorAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            _sectorHash = DeployableSdfDrillMath.ResolveSectorHash(
                _anchorAup.GridX,
                _anchorAup.GridY,
                _anchorAup.GridZ,
                _anchorAup.LocalX,
                _anchorAup.LocalY,
                _anchorAup.LocalZ);
        }

        private bool TryReservePower()
        {
            IPowerGridService powerGrid = _powerGrid;
            if (powerGrid == null || powerGrid.GridCount <= 0)
                return false;

            BatteryRuntimeSnapshot battery = powerGrid.BatterySnapshot;
            float demand = math.max(DefaultPowerDrawWatts, activePowerDrawWatts);
            bool generationAvailable = powerGrid.TotalGeneration - powerGrid.TotalConsumption >= math.max(DefaultPowerDrawWatts, powerSurplusThresholdWatts);
            bool storedEnergyAvailable = battery.TotalStoredEnergyWattSeconds >= demand;
            if (!generationAvailable && !storedEnergyAvailable)
                return false;

            float queueDemand = math.min(WirelessDrainQueueCapWattSeconds, demand);
            return powerGrid.TryQueueWirelessToolDrain(queueDemand, out float granted) && granted >= queueDemand * 0.95f;
        }

        private void ScheduleExtractionJob(double now, DeployableSdfDrillMathLod mathLod, ushort maxCycles)
        {
            if (_extractionPending ||
                _broken ||
                !TryResolveInventoryState(
                    out NativeSlice<ushort> quantities,
                    out NativeSlice<ushort> capacities,
                    out NativeSlice<uint> itemHashes,
                    out NativeSlice<uint> oreHashes))
            {
                return;
            }

            double elapsed = math.max(0d, now - _lastMacroUpdateUnscaledTime);
            if (elapsed < math.max(1f, extractionCycleSeconds))
                return;

            if (!TryResolveExtractionResultBuffer(out NativeSlice<DeployableSdfDrillExtractionResult> extractionResult))
                return;

            int biomeId = ResolveBiomeId();
            DeployableSdfDrillExtractionInput input = new DeployableSdfDrillExtractionInput
            {
                GridX = _anchorAup.GridX,
                GridY = _anchorAup.GridY,
                GridZ = _anchorAup.GridZ,
                LocalX = _anchorAup.LocalX,
                LocalY = _anchorAup.LocalY,
                LocalZ = _anchorAup.LocalZ,
                ElapsedSeconds = elapsed,
                CycleSeconds = math.max(1f, extractionCycleSeconds),
                DrillSeed = _lcgState,
                SectorHash = _sectorHash,
                BiomeId = biomeId,
                MaxCycles = maxCycles,
                QuantityPerCycle = (ushort)math.max(1, (int)quantityPerCycle),
                SlotCount = InventorySlotCount,
                MathLod = (byte)mathLod,
                Flags = (ushort)_stateFlags
            };

            DeployableSdfDrillExtractionJob job = new DeployableSdfDrillExtractionJob
            {
                Input = input,
                Quantities = quantities,
                Capacities = capacities,
                ItemHashes = itemHashes,
                OreHashes = oreHashes,
                Result = extractionResult
            };
            _extractionHandle = job.Schedule();
            _extractionPending = true;
        }

        private void CompleteExtractionJob(bool forceComplete)
        {
            if (!_extractionPending)
                return;

            if (!forceComplete && !_extractionHandle.IsCompleted)
                return;

            _extractionHandle.Complete();
            _extractionPending = false;
            CommitExtractionResult(SystemDispatcher.CurrentUnscaledTimeSeconds);
        }

        private void CommitExtractionResult(double now)
        {
            if (!TryResolveExtractionResultBuffer(out NativeSlice<DeployableSdfDrillExtractionResult> extractionResult))
                return;

            DeployableSdfDrillExtractionResult result = extractionResult[0];
            _lcgState = result.NewSeed != 0u ? result.NewSeed : _lcgState;
            bool inventoryFull = (result.Flags & (ushort)DeployableSdfDrillFlags.InventoryFull) != 0;
            SetFlag(DeployableSdfDrillFlags.InventoryFull, inventoryFull);

            if (inventoryFull)
            {
                _lastMacroUpdateUnscaledTime = now;
            }
            else if (result.CyclesProcessed > 0)
            {
                double consumed = result.CyclesProcessed * math.max(1f, extractionCycleSeconds);
                _lastMacroUpdateUnscaledTime = math.min(now, _lastMacroUpdateUnscaledTime + consumed);
            }

            if (result.TotalQuantity > 0)
            {
                _oresExtracted = unchecked(_oresExtracted + result.TotalQuantity);
                PublishItemAcquired(in result);
                UpdateFillLabel(true);
            }

            if (result.CyclesProcessed > 0)
                WriteBlackBox(result.CyclesProcessed);
        }

        private void ApplyOfflineMacroDelta(double now, ushort maxCycles)
        {
            if (_extractionPending ||
                _broken ||
                !TryResolveInventoryState(
                    out NativeSlice<ushort> quantities,
                    out NativeSlice<ushort> capacities,
                    out NativeSlice<uint> itemHashes,
                    out NativeSlice<uint> oreHashes))
            {
                return;
            }

            double elapsed = math.max(0d, now - _lastMacroUpdateUnscaledTime);
            if (elapsed < math.max(1f, extractionCycleSeconds))
                return;

            if (!TryResolveExtractionResultBuffer(out NativeSlice<DeployableSdfDrillExtractionResult> extractionResult))
                return;

            DeployableSdfDrillExtractionInput input = new DeployableSdfDrillExtractionInput
            {
                GridX = _anchorAup.GridX,
                GridY = _anchorAup.GridY,
                GridZ = _anchorAup.GridZ,
                LocalX = _anchorAup.LocalX,
                LocalY = _anchorAup.LocalY,
                LocalZ = _anchorAup.LocalZ,
                ElapsedSeconds = elapsed,
                CycleSeconds = math.max(1f, extractionCycleSeconds),
                DrillSeed = _lcgState,
                SectorHash = _sectorHash,
                BiomeId = ResolveBiomeId(),
                MaxCycles = maxCycles,
                QuantityPerCycle = (ushort)math.max(1, (int)quantityPerCycle),
                SlotCount = InventorySlotCount,
                MathLod = (byte)ResolveMathLod(),
                Flags = (ushort)(_stateFlags | DeployableSdfDrillFlags.MacroResident)
            };

            DeployableSdfDrillExtractionJob job = new DeployableSdfDrillExtractionJob
            {
                Input = input,
                Quantities = quantities,
                Capacities = capacities,
                ItemHashes = itemHashes,
                OreHashes = oreHashes,
                Result = extractionResult
            };
            // COLD SYNC JOB: Macro hydration is an unload/load boundary, not a frame tick; completes once to cap offline inventory.
            JobHandle handle = job.Schedule();
            handle.Complete();
            CommitExtractionResult(now);
        }

        private bool TryEmitVoxelCarveEvent()
        {
            if (!ResolveVoxelBridge(out VoxelDeltaProcessor deltaProcessor, out HectonVoxelVolume volume))
                return false;

            Vector3 runtimePoint = _cachedTransform != null ? _cachedTransform.position : ToVector3(_anchorRuntimePosition);
            if (!IsFiniteVector3(runtimePoint))
            {
                FaultInvalidRuntimePosition();
                return false;
            }

            double3 absolutePoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePoint);
            double carveDepth = math.max(0.25f, carveRadiusMeters);
            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = new float3((float)absolutePoint.x, (float)absolutePoint.y, (float)absolutePoint.z),
                AbsoluteSegmentEnd = new float3((float)absolutePoint.x, (float)(absolutePoint.y - carveDepth), (float)absolutePoint.z),
                AbsoluteHalfExtents = new float3(carveRadiusMeters, carveRadiusMeters, carveRadiusMeters),
                AbsoluteImpulseDirection = new float3(0f, -1f, 0f),
                AbsoluteHitPointDouble = absolutePoint,
                AbsoluteSegmentEndDouble = new double3(absolutePoint.x, absolutePoint.y - carveDepth, absolutePoint.z),
                RadiusMeters = math.clamp(carveRadiusMeters, 0.9f, 4f),
                BlendStrengthMeters = math.max(0.25f, carveBlendStrengthMeters),
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Sphere,
                MaterialId = 0,
                SourceFlags = 0
            };

            return deltaProcessor.TryQueueCarveEvent(volume, in carveEvent);
        }

        private bool ResolveVoxelBridge(out VoxelDeltaProcessor deltaProcessor, out HectonVoxelVolume volume)
        {
            deltaProcessor = voxelDeltaProcessor;
            if (deltaProcessor == null)
                deltaProcessor = _cachedVoxelDeltaProcessor;

            volume = explicitVoxelVolume != null ? explicitVoxelVolume : _resolvedVoxelVolume;
            if (volume == null && _cachedTransform != null)
            {
                Vector3 origin = _cachedTransform.position + Vector3.up * 0.5f;
                if (HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                        origin,
                        Vector3.down,
                        math.max(1f, sdfRaymarchDistanceMeters),
                        math.max(0.05f, sdfRaymarchStepMeters),
                        out HectonVoxelVolume raymarchVolume,
                        out VoxelSdfRaycastHit _))
                {
                    volume = raymarchVolume;
                    _resolvedVoxelVolume = raymarchVolume;
                }
            }

            return deltaProcessor != null && volume != null && volume.HasRuntimeData && volume.BakeState == VoxelBakeState.Complete;
        }

        private void PublishAcousticThreat()
        {
            AcousticPingSignal signal = new AcousticPingSignal
            {
                PositionAup = _anchorAup,
                RadiusMeters = math.max(1f, acousticRadiusMeters),
                Intensity01 = math.saturate(acousticIntensity01),
                SourceId = _sourceId,
                Channel = AcousticChannelThumper,
                Flags = AcousticFlagThreat
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishItemAcquired(in DeployableSdfDrillExtractionResult result)
        {
            PublishItemAcquiredSlot(result.Slot0Delta, result.Slot0ItemHash, result.Slot0OreHash);
            PublishItemAcquiredSlot(result.Slot1Delta, result.Slot1ItemHash, result.Slot1OreHash);
            PublishItemAcquiredSlot(result.Slot2Delta, result.Slot2ItemHash, result.Slot2OreHash);
            PublishItemAcquiredSlot(result.Slot3Delta, result.Slot3ItemHash, result.Slot3OreHash);
        }

        private void PublishItemAcquiredSlot(ushort quantity, uint itemHash, uint oreHash)
        {
            if (quantity == 0)
                return;

            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = _anchorAup,
                ItemHash = itemHash != 0u ? itemHash : DeployableSdfDrillMath.DefaultItemHash,
                OreHash = oreHash != 0u ? oreHash : DeployableSdfDrillMath.DefaultOreHash,
                Quantity = quantity,
                SourceKind = 7,
                Flags = 0,
                Frame = unchecked((uint)Time.frameCount)
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishCombatDamage(float damage, Vector3 hitPoint, uint sourceHash)
        {
            float3 point = new float3(hitPoint.x, hitPoint.y, hitPoint.z);
            float3 direction = NormalizeSafe(point - _anchorRuntimePosition, math.up());
            Hecton8.Core.Contracts.Signals.CombatDamageSignal signal = new Hecton8.Core.Contracts.Signals.CombatDamageSignal
            {
                ImpactAup = Hecton8.Core.Contracts.Signals.CombatDamageSignalCodec.FromRuntimePoint(point),
                Direction = direction,
                Magnitude = damage,
                DamageType = DrillDamageTypeHash,
                TargetHash = _sourceId,
                SourceHash = sourceHash,
                Frame = unchecked((uint)Time.frameCount),
                SourceId = unchecked((ushort)(sourceHash & 0xFFFFu)),
                TargetId = unchecked((ushort)(_sourceId & 0xFFFFu)),
                Channel = 7,
                Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag,
                IntegrityDelta = (byte)math.clamp((int)math.ceil(damage * math.rcp(math.max(1f, maxHealth)) * 255f), 1, 255)
            };
            GlobalSignals.Publish(in signal);
        }

        private void MarkBroken(Vector3 hitPoint)
        {
            _broken = true;
            SetFlag(DeployableSdfDrillFlags.Broken, true);
            SetFlag(DeployableSdfDrillFlags.Active, false);
            PublishDebris(hitPoint);
            DumpBlackBox();
        }

        private void PublishDebris(Vector3 hitPoint)
        {
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(hitPoint),
                SpeciesHash = DrillDebrisSpeciesHash,
                SourceEntityId = _sourceId,
                Intensity01 = 1f,
                DebrisKind = DebrisSpawnSignal.DebrisKindSparks,
                Flags = DebrisSpawnSignal.FlagComputeShard | DebrisSpawnSignal.FlagToolSparks,
                Quantity = 7
            };
            SignalBus<DebrisSpawnSignal>.Push(in signal);
        }

        private int ResolveBiomeId()
        {
            MapMagicBridge mapMagic = _mapMagic;
            if (mapMagic == null || !mapMagic.IsAvailable)
                return 0;

            Vector3 position = _cachedTransform != null ? _cachedTransform.position : ToVector3(_anchorRuntimePosition);
            if (mapMagic.TryGetMatrixBiomeId(position.x, position.z, out int matrixBiomeId))
                return matrixBiomeId;

            return mapMagic.CurrentBiomeID;
        }

        private DeployableSdfDrillMathLod ResolveMathLod()
        {
            return _cachedMathLod;
        }

        private void UpdateMathLodHysteresis(double now)
        {
            if (_cachedMathLod == _targetMathLod)
                return;

            if (now < _mathLodChangeEligibleUnscaledTime)
                return;

            _cachedMathLod = _targetMathLod;
        }

        private static DeployableSdfDrillMathLod ToMathLod(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Ultra:
                    return DeployableSdfDrillMathLod.Ultra;
                case HectonQualityTier.High:
                    return DeployableSdfDrillMathLod.High;
                case HectonQualityTier.Mid:
                    return DeployableSdfDrillMathLod.Middle;
                default:
                    return DeployableSdfDrillMathLod.Low;
            }
        }

        private static ushort ResolveMaxRuntimeCycles(DeployableSdfDrillMathLod lod)
        {
            switch (lod)
            {
                case DeployableSdfDrillMathLod.Ultra:
                    return 8;
                case DeployableSdfDrillMathLod.High:
                    return 4;
                case DeployableSdfDrillMathLod.Middle:
                    return 2;
                default:
                    return 1;
            }
        }

        private static ushort ResolveMaxOfflineCycles(DeployableSdfDrillMathLod lod)
        {
            switch (lod)
            {
                case DeployableSdfDrillMathLod.Ultra:
                    return 512;
                case DeployableSdfDrillMathLod.High:
                    return 256;
                case DeployableSdfDrillMathLod.Middle:
                    return 128;
                default:
                    return 64;
            }
        }

        private bool ValidateFiniteState()
        {
            bool finite = math.all(math.isfinite(_anchorRuntimePosition)) &&
                          math.isfinite(_health) &&
                          math.all(math.isfinite(new float3(_anchorAup.LocalX, _anchorAup.LocalY, _anchorAup.LocalZ)));
            if (_cachedTransform != null)
                finite = finite && IsFiniteVector3(_cachedTransform.position);

            if (!finite)
            {
                FaultInvalidRuntimePosition();
            }

            return finite;
        }

        private void UpdateFillLabel(bool force)
        {
            if (fillPercentageLabel == null)
                return;

            int percent = ComputeFillPercent();
            if (!force && percent == _lastFillPercent)
                return;

            _lastFillPercent = percent;
            int cursor = 0;
            _fillTextBuffer[cursor++] = 'F';
            _fillTextBuffer[cursor++] = ' ';
            cursor = WriteThreeDigits(_fillTextBuffer, cursor, percent);
            _fillTextBuffer[cursor++] = '%';
            fillPercentageLabel.SetCharArray(_fillTextBuffer, 0, cursor);
        }

        private int ComputeFillPercent()
        {
            if (!TryResolveInventoryState(out NativeSlice<ushort> quantities, out NativeSlice<ushort> capacities, out _, out _))
                return 0;

            int quantity = 0;
            int capacity = 0;
            for (int i = 0; i < InventorySlotCount; i++)
            {
                quantity += quantities[i];
                capacity += capacities[i];
            }

            if (capacity <= 0)
                return 0;

            return math.clamp((int)math.floor(quantity * 100f * math.rcp((float)capacity)), 0, 100);
        }

        private int ResolveFillPermille()
        {
            if (!TryResolveInventoryState(out NativeSlice<ushort> quantities, out NativeSlice<ushort> capacities, out _, out _))
                return 0;

            int quantity = 0;
            int capacity = 0;
            for (int i = 0; i < InventorySlotCount; i++)
            {
                quantity += quantities[i];
                capacity += capacities[i];
            }

            if (capacity <= 0)
                return 0;

            return math.clamp((int)math.floor(quantity * 1000f * math.rcp((float)capacity)), 0, 1000);
        }

        private void WriteBlackBox(ushort jobCycles)
        {
            if (!TryResolveBlackBox(out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox))
                return;

            int cursor = _blackBoxCursor;
            blackBox[cursor] = new DeployableSdfDrillTelemetryEntry
            {
                GridX = _anchorAup.GridX,
                GridY = _anchorAup.GridY,
                GridZ = _anchorAup.GridZ,
                LocalX = _anchorAup.LocalX,
                LocalY = _anchorAup.LocalY,
                LocalZ = _anchorAup.LocalZ,
                Frame = unchecked((uint)Time.frameCount),
                ActiveDrills = unchecked((uint)math.max(0, s_activeDrills)),
                OresExtracted = _oresExtracted,
                FillPermille = (ushort)ResolveFillPermille(),
                HealthPermille = (ushort)math.clamp((int)math.round(_health * math.rcp(math.max(1f, maxHealth)) * 1000f), 0, 1000),
                Flags = (ushort)_stateFlags,
                JobCycles = jobCycles
            };
            _blackBoxCursor = (cursor + 1) % BlackBoxCapacity;
        }

        private void DumpBlackBox()
        {
            if (_faultDumped || !TryResolveBlackBox(out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox))
                return;

            _faultDumped = true;
            SetFlag(DeployableSdfDrillFlags.FaultDumped, true);
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string path = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int start = _blackBoxCursor % blackBox.Length;
                    for (int i = 0; i < blackBox.Length; i++)
                    {
                        int index = (start + i) % blackBox.Length;
                        DeployableSdfDrillTelemetryEntry entry = blackBox[index];
                        writer.Write(entry.GridX);
                        writer.Write(entry.GridY);
                        writer.Write(entry.GridZ);
                        writer.Write(entry.LocalX);
                        writer.Write(entry.LocalY);
                        writer.Write(entry.LocalZ);
                        writer.Write(entry.Frame);
                        writer.Write(entry.ActiveDrills);
                        writer.Write(entry.OresExtracted);
                        writer.Write(entry.FillPermille);
                        writer.Write(entry.HealthPermille);
                        writer.Write(entry.Flags);
                        writer.Write(entry.JobCycles);
                    }
                }
            }
            catch (Exception)
            {
                _faultDumped = false;
                SetFlag(DeployableSdfDrillFlags.FaultDumped, false);
            }
        }

        private void ClearInventoryQuantities()
        {
            if (!TryResolveInventoryState(out NativeSlice<ushort> quantities, out _, out _, out _))
                return;

            for (int i = 0; i < quantities.Length; i++)
                quantities[i] = 0;
        }

        private void ClearBlackBox()
        {
            _blackBoxCursor = 0;
            if (!TryResolveBlackBox(out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox))
                return;

            for (int i = 0; i < blackBox.Length; i++)
                blackBox[i] = default;
        }

        private void SetFlag(DeployableSdfDrillFlags flag, bool enabled)
        {
            _stateFlags = enabled ? _stateFlags | flag : _stateFlags & ~flag;
        }

        private static int WriteThreeDigits(char[] buffer, int cursor, int value)
        {
            int safe = math.clamp(value, 0, 100);
            buffer[cursor++] = (char)('0' + (safe / 100));
            buffer[cursor++] = (char)('0' + ((safe / 10) % 10));
            buffer[cursor++] = (char)('0' + (safe % 10));
            return cursor;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f && math.isfinite(lengthSq) ? value * math.rsqrt(lengthSq) : fallback;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Quaternion ToQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private Vector3 ResolveSafeRuntimePoint(Vector3 candidate)
        {
            if (IsFiniteVector3(candidate))
                return candidate;

            Vector3 anchor = ToVector3(_anchorRuntimePosition);
            return IsFiniteVector3(anchor) ? anchor : Vector3.zero;
        }

        private void FaultInvalidRuntimePosition()
        {
            _broken = true;
            _health = 0f;
            _snappedToTerrain = false;
            SetFlag(DeployableSdfDrillFlags.Broken, true);
            SetFlag(DeployableSdfDrillFlags.Active, false);
            SetFlag(DeployableSdfDrillFlags.Snapped, false);
            SetFlag(DeployableSdfDrillFlags.DormantNoPower, false);
            WriteBlackBox(0);
            DumpBlackBox();
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static float SanitizeAtLeast(float value, float minimum, float fallback)
        {
            return float.IsFinite(value) ? math.max(minimum, value) : fallback;
        }

        private static float SanitizeRange(float value, float minimum, float maximum, float fallback)
        {
            float safe = float.IsFinite(value) ? value : fallback;
            return math.clamp(safe, minimum, maximum);
        }

        private void DisposeNativeState()
        {
            ReleaseVaultSlot();
            _slotOwnersHandle = default;
            _inventoryQuantitiesHandle = default;
            _inventoryCapacitiesHandle = default;
            _inventoryItemHashesHandle = default;
            _inventoryOreHashesHandle = default;
            _extractionResultHandle = default;
            _blackBoxHandle = default;
            _snapCommandsHandle = default;
            _snapHitsHandle = default;
            _blackBoxCursor = 0;
        }

        private bool TryPrepareNativeState(
            out NativeSlice<ushort> quantities,
            out NativeSlice<ushort> capacities,
            out NativeSlice<uint> itemHashes,
            out NativeSlice<uint> oreHashes,
            out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox,
            out NativeArray<RaycastCommand> snapCommands,
            out NativeArray<RaycastHit> snapHits,
            out bool assignedNewSlot)
        {
            quantities = default;
            capacities = default;
            itemHashes = default;
            oreHashes = default;
            blackBox = default;
            snapCommands = default;
            snapHits = default;
            assignedNewSlot = false;

            if (!TryResolveVaultBuffer(
                    ref _slotOwnersHandle,
                    BufferID.DeployableSdfDrillSlotOwners,
                    MaxVaultDrillInstances,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> slotOwners) ||
                !TryResolveVaultBuffer(
                    ref _inventoryQuantitiesHandle,
                    BufferID.DeployableSdfDrillInventoryQuantities,
                    MaxVaultDrillInstances * InventorySlotCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ushort> quantityBuffer) ||
                !TryResolveVaultBuffer(
                    ref _inventoryCapacitiesHandle,
                    BufferID.DeployableSdfDrillInventoryCapacities,
                    MaxVaultDrillInstances * InventorySlotCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ushort> capacityBuffer) ||
                !TryResolveVaultBuffer(
                    ref _inventoryItemHashesHandle,
                    BufferID.DeployableSdfDrillInventoryItemHashes,
                    MaxVaultDrillInstances * InventorySlotCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> itemHashBuffer) ||
                !TryResolveVaultBuffer(
                    ref _inventoryOreHashesHandle,
                    BufferID.DeployableSdfDrillInventoryOreHashes,
                    MaxVaultDrillInstances * InventorySlotCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> oreHashBuffer) ||
                !TryResolveVaultBuffer(
                    ref _blackBoxHandle,
                    BufferID.DeployableSdfDrillBlackBox,
                    MaxVaultDrillInstances * BlackBoxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<DeployableSdfDrillTelemetryEntry> blackBoxBuffer) ||
                !TryResolveVaultBuffer(
                    ref _snapCommandsHandle,
                    BufferID.DeployableSdfDrillSnapCommands,
                    MaxVaultDrillInstances * SnapCommandCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<RaycastCommand> snapCommandBuffer) ||
                !TryResolveVaultBuffer(
                    ref _snapHitsHandle,
                    BufferID.DeployableSdfDrillSnapHits,
                    MaxVaultDrillInstances * SnapCommandCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<RaycastHit> snapHitBuffer) ||
                !EnsureVaultSlot(slotOwners, out assignedNewSlot))
            {
                return false;
            }

            int inventoryOffset = _vaultSlotIndex * InventorySlotCount;
            int blackBoxOffset = _vaultSlotIndex * BlackBoxCapacity;
            int snapOffset = _vaultSlotIndex * SnapCommandCount;
            return TryBuildSlice(quantityBuffer, inventoryOffset, InventorySlotCount, out quantities) &&
                TryBuildSlice(capacityBuffer, inventoryOffset, InventorySlotCount, out capacities) &&
                TryBuildSlice(itemHashBuffer, inventoryOffset, InventorySlotCount, out itemHashes) &&
                TryBuildSlice(oreHashBuffer, inventoryOffset, InventorySlotCount, out oreHashes) &&
                TryBuildSlice(blackBoxBuffer, blackBoxOffset, BlackBoxCapacity, out blackBox) &&
                TryBuildSubArray(snapCommandBuffer, snapOffset, SnapCommandCount, out snapCommands) &&
                TryBuildSubArray(snapHitBuffer, snapOffset, SnapCommandCount, out snapHits);
        }

        private bool TryResolveInventoryState(
            out NativeSlice<ushort> quantities,
            out NativeSlice<ushort> capacities,
            out NativeSlice<uint> itemHashes,
            out NativeSlice<uint> oreHashes)
        {
            return TryPrepareNativeState(
                out quantities,
                out capacities,
                out itemHashes,
                out oreHashes,
                out _,
                out _,
                out _,
                out _);
        }

        private bool TryResolveBlackBox(out NativeSlice<DeployableSdfDrillTelemetryEntry> blackBox)
        {
            bool resolved = TryPrepareNativeState(
                out _,
                out _,
                out _,
                out _,
                out blackBox,
                out _,
                out _,
                out _);
            return resolved;
        }

        private bool TryResolveSnapBuffers(
            out NativeArray<RaycastCommand> snapCommands,
            out NativeArray<RaycastHit> snapHits)
        {
            return TryPrepareNativeState(
                out _,
                out _,
                out _,
                out _,
                out _,
                out snapCommands,
                out snapHits,
                out _);
        }

        private bool TryResolveExtractionResultBuffer(out NativeSlice<DeployableSdfDrillExtractionResult> extractionResult)
        {
            extractionResult = default;
            if (!TryResolveVaultBuffer(
                    ref _extractionResultHandle,
                    BufferID.DeployableSdfDrillExtractionResult,
                    MaxVaultDrillInstances,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<DeployableSdfDrillExtractionResult> extractionResults))
            {
                return false;
            }

            if (!EnsureVaultSlot())
                return false;

            return TryBuildSlice(extractionResults, _vaultSlotIndex, 1, out extractionResult);
        }

        private bool EnsureVaultSlot()
        {
            if (!TryResolveVaultBuffer(
                    ref _slotOwnersHandle,
                    BufferID.DeployableSdfDrillSlotOwners,
                    MaxVaultDrillInstances,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> slotOwners))
            {
                return false;
            }

            return EnsureVaultSlot(slotOwners, out _);
        }

        private bool EnsureVaultSlot(NativeArray<uint> slotOwners, out bool assignedNewSlot)
        {
            assignedNewSlot = false;
            if (!slotOwners.IsCreated || slotOwners.Length < MaxVaultDrillInstances)
                return false;

            uint ownerHash = ResolveVaultOwnerHash();
            if (_vaultSlotIndex >= 0 &&
                _vaultSlotIndex < MaxVaultDrillInstances &&
                slotOwners[_vaultSlotIndex] == ownerHash)
            {
                return true;
            }

            for (int i = 0; i < MaxVaultDrillInstances; i++)
            {
                if (slotOwners[i] == ownerHash)
                {
                    _vaultSlotIndex = i;
                    return true;
                }
            }

            for (int i = 0; i < MaxVaultDrillInstances; i++)
            {
                if (slotOwners[i] != 0u)
                    continue;

                slotOwners[i] = ownerHash;
                _vaultSlotIndex = i;
                assignedNewSlot = true;
                return true;
            }

            return false;
        }

        private uint ResolveVaultOwnerHash()
        {
            if (_sourceId != 0u)
                return _sourceId;

            return DeployableSdfDrillMath.Mix(DrillToolHash, unchecked((uint)GetInstanceID()));
        }

        private void ReleaseVaultSlot()
        {
            int slotIndex = _vaultSlotIndex;
            if (slotIndex < 0)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null && _slotOwnersHandle.IsCreated)
            {
                NativeArray<uint> slotOwners = _slotOwnersHandle.Resolve(vault);
                if (slotOwners.IsCreated &&
                    slotIndex < slotOwners.Length &&
                    slotOwners[slotIndex] == ResolveVaultOwnerHash())
                {
                    slotOwners[slotIndex] = 0u;
                }
            }

            _vaultSlotIndex = -1;
        }

        private static bool TryResolveVaultBuffer<T>(
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!handle.IsCreated || handle.Length < requiredLength)
                handle = vault.GetBufferHandle<T>(bufferId, requiredLength, SystemID.GameplayTools, options);

            if (!handle.IsCreated)
                return false;

            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool TryBuildSlice<T>(
            NativeArray<T> buffer,
            int offset,
            int length,
            out NativeSlice<T> slice) where T : struct
        {
            slice = default;
            if (!buffer.IsCreated || offset < 0 || length <= 0 || offset > buffer.Length - length)
                return false;

            slice = new NativeSlice<T>(buffer, offset, length);
            return slice.Length == length;
        }

        private static bool TryBuildSubArray<T>(
            NativeArray<T> buffer,
            int offset,
            int length,
            out NativeArray<T> subArray) where T : struct
        {
            subArray = default;
            if (!buffer.IsCreated || offset < 0 || length <= 0 || offset > buffer.Length - length)
                return false;

            subArray = buffer.GetSubArray(offset, length);
            return subArray.IsCreated && subArray.Length == length;
        }

        private static void ClearSlice<T>(NativeSlice<T> slice) where T : struct
        {
            for (int i = 0; i < slice.Length; i++)
                slice[i] = default;
        }

        private static void ClearNativeArray<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }
    }
}
