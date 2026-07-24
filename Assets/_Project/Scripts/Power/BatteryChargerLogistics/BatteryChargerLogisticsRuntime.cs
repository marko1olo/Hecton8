using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    public sealed unsafe class BatteryChargerLogisticsRuntime : IBatteryChargerLogisticsService, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001BatteryChargerLogisticsRuntimeSignalPushDropCount;
        private const uint SystemHash = 0x53323330u; // S230
        private const uint PreSimulationHash = 0x32333050u;
        private const uint SimulationHash = 0x32333053u;
        private const uint PostSimulationHash = 0x3233304Fu;
        private const uint VisualSyncHash = 0x32333056u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_230.bin";
        private const string CsvRelativePath = "Docs/Power/battery_charger_profiles.csv";
        private const int CsvPollCadenceFrames = 128;
        private const double AcousticHumMaxAupExtentMeters = 100000.0d;
        private const float SimulationTickDeltaSeconds = 1f / 60f;
        private const float AuthoritativeQualityWeight = 1f;
        private const float MinimumCadenceHz = 5f;
        private const float MaximumCadenceHz = 60f;

        private static readonly int s_ChargerStatusBufferId = Shader.PropertyToID("_H8BatteryChargerStatusBuffer");
        private static readonly int s_ChargerStatusParamsId = Shader.PropertyToID("_H8BatteryChargerStatusParams");

        private static BatteryChargerLogisticsRuntime s_active;
        private static float s_pendingMaxChargeRate = BatteryChargerLogisticsConstants.DefaultMaxChargeRate01PerSecond;
        private static float s_pendingEfficiencyExponent = BatteryChargerLogisticsConstants.DefaultEfficiencyExponent;
        private static float s_pendingQualityOverride = -1f;
        private static readonly ulong TuningMutationGuardMask =
            MutationGuardBit(BatteryChargerLogisticsBufferIds.Tuning);
        private static readonly ulong ProfileImportMutationGuardMask =
            MutationGuardBit(BatteryChargerLogisticsBufferIds.Profiles);
        private static readonly ulong LinkMutationGuardMask =
            MutationGuardBit(BatteryChargerLogisticsBufferIds.Links) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.LinkAup) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.ExpectedPowerNodeHashes) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.VisualStates);
        private static readonly ulong LinkMutationWithPowerNodesGuardMask =
            LinkMutationGuardMask |
            MutationGuardBit(PowerGridBufferIds.Nodes);
        private static readonly ulong JobMutationGuardMask =
            LinkMutationGuardMask |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.MockInventorySlots) |
            MutationGuardBit(BufferID.ShinobuInventorySlots) |
            MutationGuardBit(PowerGridBufferIds.Nodes) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.AtomicCounters) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.Tuning);
        private static readonly ulong MockGenerationMutationGuardMask =
            LinkMutationGuardMask |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.MockInventorySlots) |
            MutationGuardBit(PowerGridBufferIds.Nodes) |
            MutationGuardBit(PowerGridBufferIds.NodeAup) |
            MutationGuardBit(BatteryChargerLogisticsBufferIds.Tuning);
#if UNITY_EDITOR
        private static readonly byte[] s_profileCsvScratchCold = new byte[BatteryChargerLogisticsConstants.CsvScratchBytes];
        private static readonly ChargerProfileDTO[] s_profileImportScratch = new ChargerProfileDTO[BatteryChargerLogisticsConstants.DefaultProfileCapacity];
        private static int s_profileCsvScratchBusy;
#endif

        private readonly string _dumpPath;
        private readonly string _csvPath;
        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly SimulationPhaseSystem _simulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;

        private IDataVault _vault;
        private BatteryChargerLogisticsHandles _handles;
        private PowerGridVaultHandles _powerHandles;
        private bool _shutdown;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredColdTick;
        private bool _hotSwapRegistered;
        private bool _vaultInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _defaultsInitialized;
        private bool _simulationScheduled;
        private bool _mockGenerationScheduled;
        private bool _hasPendingVaultRebind;
        private bool _vaultRepairRequested;
        private bool _usingMockInventorySlots;
        private bool _jobLockedMockInventorySlots;
        private bool _dumpWrittenThisFault;
        private ulong _lockedBufferMask;
        private ulong _mockLockedBufferMask;
        private int _activeCount;
        private int _powerNodeCount;
        private int _mockPendingCount;
        private int _mockPendingPowerNodeCount;
        private int _skippedCadenceFrames;
        private uint _lastFrame;
        private float _authorityAccumulator;
        private float _lastDeltaSeconds;
        private float _lastCadenceHz = 5f;
        private float _lastQualityWeight = 1f;
        private float _lastFenceElapsedMicroseconds;
        private long _jobScheduleTimestamp;
        private JobHandle _simulationHandle;
        private JobHandle _mockGenerationHandle;
        private IDataVault _pendingVault;
        private GraphicsBuffer _visualBufferA;
        private GraphicsBuffer _visualBufferB;
        private int _visualReadSlot;
        private int _visualWriteSlot = 1;
        private int _lastUploadedCount;
        private uint _lastUploadedHash;
        private bool _visualHasReadBuffer;
        private bool _visualDirty = true;
        private DateTime _csvLastWriteUtc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GlobalRegistry.ResetBatteryChargerLogisticsRuntimeForDomainReload();
            s_active = null;
            s_pendingMaxChargeRate = BatteryChargerLogisticsConstants.DefaultMaxChargeRate01PerSecond;
            s_pendingEfficiencyExponent = BatteryChargerLogisticsConstants.DefaultEfficiencyExponent;
            s_pendingQualityOverride = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            BatteryChargerLogisticsRuntime runtime = new BatteryChargerLogisticsRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private BatteryChargerLogisticsRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dumpPath = DumpRelativePath;
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        bool IBatteryChargerLogisticsService.TryRegisterChargerLink(
            uint inventorySlotIndex,
            uint powerGraphNodeIndex,
            float chargeRate,
            float efficiencyScalar,
            double3 chargerAup,
            out int linkIndex)
        {
            return TryRegisterChargerLink(
                inventorySlotIndex,
                powerGraphNodeIndex,
                chargeRate,
                efficiencyScalar,
                chargerAup,
                out linkIndex);
        }

        int IBatteryChargerLogisticsService.TryUnregisterChargerLinks(
            uint inventorySlotStartIndex,
            int slotCount,
            uint powerGraphNodeIndex)
        {
            return TryUnregisterChargerLinks(inventorySlotStartIndex, slotCount, powerGraphNodeIndex);
        }

        bool IBatteryChargerLogisticsService.TryWriteInventorySlotState(uint inventorySlotIndex, uint itemHash, float charge01)
        {
            return TryWriteInventorySlotState(inventorySlotIndex, itemHash, charge01);
        }

        bool IBatteryChargerLogisticsService.TryReadCharge01(uint inventorySlotIndex, out float charge01)
        {
            return TryReadCharge01(inventorySlotIndex, out charge01);
        }

        public static bool TryRegisterChargerLink(
            uint inventorySlotIndex,
            uint powerGraphNodeIndex,
            float chargeRate,
            float efficiencyScalar,
            double3 chargerAup,
            out int linkIndex)
        {
            linkIndex = -1;
            if (!math.all(math.isfinite(chargerAup)))
                return false;

            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveCachedVault();
            if (vault == null || !runtime.EnsureVaultState(vault, requireDefaults: false) ||
                runtime._simulationScheduled ||
                runtime._mockGenerationScheduled)
                return false;

            if (!TryReadInventorySlots(vault, out NativeArray<InventorySlotDTO> liveInventorySlots) ||
                !liveInventorySlots.IsCreated ||
                inventorySlotIndex >= (uint)liveInventorySlots.Length)
            {
                return false;
            }

            if (runtime._usingMockInventorySlots)
                runtime.DropMockNetworkForLiveRegistration();

            if (!runtime.TryLockLinkMutationBuffers(vault, includePowerNodes: true, out ulong lockMask))
                return false;

            try
            {
                if (!runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                    !runtime.Resolve(in runtime._handles.LinkAup, out NativeArray<double3> linkAups) ||
                    !runtime.Resolve(in runtime._handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes) ||
                    !runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals))
                {
                    return false;
                }

                int capacity = math.min(links.Length, math.min(linkAups.Length, math.min(expectedHashes.Length, visuals.Length)));
                int initializedCount = math.clamp(runtime._activeCount, 0, capacity);
                int targetIndex = -1;
                for (int i = 0; i < initializedCount; i++)
                {
                    ChargerLinkDTO existing = links[i];
                    if ((existing.Flags & BatteryChargerLogisticsConstants.LinkFlagActive) != 0u &&
                        (existing.Flags & BatteryChargerLogisticsConstants.LinkFlagMock) == 0u)
                    {
                        continue;
                    }

                    targetIndex = i;
                    break;
                }

                if (targetIndex < 0)
                {
                    if (initializedCount >= capacity)
                        return false;

                    targetIndex = initializedCount;
                }

                ChargerLinkDTO link = default;
                link.InventorySlotIndex = inventorySlotIndex;
                link.PowerGraphNodeIndex = powerGraphNodeIndex;
                link.ChargeRate = SanitizeNonNegative(chargeRate);
                link.EfficiencyScalar = SanitizeNonNegative(efficiencyScalar);
                link.Flags = BatteryChargerLogisticsConstants.LinkFlagActive;
                links[targetIndex] = link;
                linkAups[targetIndex] = chargerAup;
                runtime.ExtendPowerNodeWindowForLink(powerGraphNodeIndex);
                expectedHashes[targetIndex] = runtime.ReadExpectedPowerNodeHash(powerGraphNodeIndex);

                if ((uint)targetIndex < (uint)visuals.Length)
                {
                    ChargerVisualStateDTO visual = default;
                    visual.Status = 0u;
                    visual.Flags = link.Flags;
                    visual.LinkIndex = (uint)targetIndex;
                    visual.InventorySlotIndex = inventorySlotIndex;
                    visual.PowerGraphNodeIndex = powerGraphNodeIndex;
                    visuals[targetIndex] = visual;
                }

                runtime._activeCount = math.max(runtime._activeCount, targetIndex + 1);
                runtime._visualDirty = true;
                linkIndex = targetIndex;
                return true;
            }
            finally
            {
                runtime.UnlockLinkMutationBuffers(vault, lockMask);
            }
        }

        public static void TryUnregisterChargerLink(int linkIndex)
        {
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || linkIndex < 0 || runtime._simulationScheduled || runtime._mockGenerationScheduled)
                return;

            IDataVault vault = runtime._vault;
            if (vault == null || !runtime.TryLockLinkMutationBuffers(vault, includePowerNodes: false, out ulong lockMask))
                return;

            try
            {
                if (!runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                    (uint)linkIndex >= (uint)links.Length)
                {
                    return;
                }

                links[linkIndex] = default;
                if (runtime.Resolve(in runtime._handles.LinkAup, out NativeArray<double3> linkAups) &&
                    (uint)linkIndex < (uint)linkAups.Length)
                {
                    linkAups[linkIndex] = default;
                }

                if (runtime.Resolve(in runtime._handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes) &&
                    (uint)linkIndex < (uint)expectedHashes.Length)
                {
                    expectedHashes[linkIndex] = 0u;
                }

                if (runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) &&
                    (uint)linkIndex < (uint)visuals.Length)
                {
                    visuals[linkIndex] = default;
                }

                runtime.RecomputeActiveTail(links);
                runtime._visualDirty = true;
            }
            finally
            {
                runtime.UnlockLinkMutationBuffers(vault, lockMask);
            }
        }

        public static int TryUnregisterChargerLinks(uint inventorySlotStartIndex, int slotCount, uint powerGraphNodeIndex)
        {
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || slotCount <= 0 || runtime._simulationScheduled || runtime._mockGenerationScheduled)
                return 0;

            IDataVault vault = runtime._vault;
            if (vault == null || !runtime.TryLockLinkMutationBuffers(vault, includePowerNodes: false, out ulong lockMask))
                return 0;

            try
            {
                if (!runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) || !links.IsCreated)
                    return 0;

                runtime.Resolve(in runtime._handles.LinkAup, out NativeArray<double3> linkAups);
                runtime.Resolve(in runtime._handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes);
                runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals);

                uint inventorySlotEnd = inventorySlotStartIndex + (uint)slotCount;
                if (inventorySlotEnd < inventorySlotStartIndex)
                    inventorySlotEnd = uint.MaxValue;

                int scanCount = math.clamp(runtime._activeCount, 0, links.Length);
                int removed = 0;
                for (int i = 0; i < scanCount; i++)
                {
                    ChargerLinkDTO link = links[i];
                    if ((link.Flags & BatteryChargerLogisticsConstants.LinkFlagActive) == 0u ||
                        (link.Flags & BatteryChargerLogisticsConstants.LinkFlagMock) != 0u ||
                        link.PowerGraphNodeIndex != powerGraphNodeIndex ||
                        link.InventorySlotIndex < inventorySlotStartIndex ||
                        link.InventorySlotIndex >= inventorySlotEnd)
                    {
                        continue;
                    }

                    links[i] = default;
                    if (linkAups.IsCreated && (uint)i < (uint)linkAups.Length)
                        linkAups[i] = default;
                    if (expectedHashes.IsCreated && (uint)i < (uint)expectedHashes.Length)
                        expectedHashes[i] = 0u;
                    if (visuals.IsCreated && (uint)i < (uint)visuals.Length)
                        visuals[i] = default;
                    removed++;
                }

                if (removed > 0)
                {
                    runtime.RecomputeActiveTail(links);
                    runtime._visualDirty = true;
                }

                return removed;
            }
            finally
            {
                runtime.UnlockLinkMutationBuffers(vault, lockMask);
            }
        }

        public static bool TryWriteInventorySlotState(uint inventorySlotIndex, uint itemHash, float charge01)
        {
            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null || runtime._simulationScheduled || runtime._mockGenerationScheduled)
                return false;

            IDataVault vault = runtime.ResolveCachedVault();
            if (vault == null || !runtime.EnsureVaultState(vault, requireDefaults: false))
                return false;

            if (!TryAcquireInventorySlotsWrite(vault, out VaultGenerationHandle<InventorySlotDTO> inventoryHandle, out NativeArray<InventorySlotDTO> slots))
                return false;

            uint* lockPtr = null;
            bool slotLocked = false;
            try
            {
                if (!slots.IsCreated || (uint)inventorySlotIndex >= (uint)slots.Length)
                {
                    return false;
                }

                InventorySlotDTO* slotPtr = (InventorySlotDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(slots) + (int)inventorySlotIndex;
                lockPtr = &slotPtr->ReservedLock;
                if (!TryAcquireColdSlotLock(lockPtr))
                    return false;

                slotLocked = true;
                float safeCharge = itemHash == 0u ? 0f : math.saturate(math.isfinite(charge01) ? charge01 : 0f);
                slotPtr->ItemHashID = itemHash;
                slotPtr->Quantity = itemHash == 0u ? 0u : 1u;
                slotPtr->ConditionFlags = math.asuint(safeCharge);
                return true;
            }
            finally
            {
                if (slotLocked && lockPtr != null)
                    ReleaseColdSlotLock(lockPtr);
                vault.ReleaseWriteLock(in inventoryHandle, SystemID.Power);
            }
        }

        public static bool TryReadCharge01(uint inventorySlotIndex, out float charge01)
        {
            charge01 = 0f;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled || runtime._mockGenerationScheduled || runtime._usingMockInventorySlots)
                return false;

            IDataVault vault = runtime._vault;
            if (vault == null ||
                !TryReadInventorySlots(vault, out NativeArray<InventorySlotDTO> slots) ||
                !slots.IsCreated ||
                (uint)inventorySlotIndex >= (uint)slots.Length)
            {
                return false;
            }

            InventorySlotDTO slot = slots[(int)inventorySlotIndex];
            if (slot.ItemHashID == 0u || slot.Quantity == 0u || slot.ReservedLock != 0u)
                return false;

            float value = math.asfloat(slot.ConditionFlags);
            charge01 = math.saturate(math.isfinite(value) ? value : 0f);
            return true;
        }

        public static bool TryReadEditorState(out int activeCount, out float quality, out float cadenceHz, out float lastFenceElapsedMicroseconds)
        {
            activeCount = 0;
            quality = 0f;
            cadenceHz = 0f;
            lastFenceElapsedMicroseconds = 0f;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null)
                return false;

            activeCount = runtime._activeCount;
            quality = runtime._lastQualityWeight;
            cadenceHz = runtime._lastCadenceHz;
            lastFenceElapsedMicroseconds = runtime._lastFenceElapsedMicroseconds;
            return true;
        }

        public static bool TryApplyEditorTuning(float maxChargeRate, float efficiencyExponent, float qualityOverride)
        {
            s_pendingMaxChargeRate = SanitizeNonNegative(maxChargeRate);
            s_pendingEfficiencyExponent = SanitizePositive(efficiencyExponent, 0.0001f, BatteryChargerLogisticsConstants.DefaultEfficiencyExponent);
            s_pendingQualityOverride = SanitizeQualityOverride(qualityOverride);

            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null)
                return false;

            if (runtime._simulationScheduled || runtime._mockGenerationScheduled)
                return true;

            IDataVault vault = runtime._vault;
            if (vault == null || !vault.TryAcquireMutationGuard(TuningMutationGuardMask))
            {
                return true;
            }

            try
            {
                if (!runtime.Resolve(in runtime._handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) ||
                    tuning.Length == 0)
                    return true;

                ChargerTuningDTO dto = tuning[0];
                ApplyPendingTuningValues(ref dto);
                tuning[0] = dto;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        public static bool TryLoadProfilesFromCsvBytes(ReadOnlySpan<byte> csv)
        {
            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveCachedVault();
            if (vault == null || !runtime.EnsureVaultState(vault, requireDefaults: false) ||
                runtime._simulationScheduled ||
                runtime._mockGenerationScheduled)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref s_profileCsvScratchBusy, 1, 0) != 0)
                return false;

            bool guardAcquired = false;
            try
            {
                if (!BatteryChargerProfileCsvParser.TryParseProfiles(csv, s_profileImportScratch.AsSpan(), out int profileCount))
                    return false;

                if (!vault.TryAcquireMutationGuard(ProfileImportMutationGuardMask))
                    return false;

                guardAcquired = true;
                if (!runtime.Resolve(in runtime._handles.Profiles, out NativeArray<ChargerProfileDTO> profiles))
                    return false;

                CommitProfilesCsv(s_profileImportScratch.AsSpan(), profileCount, profiles);
                return true;
            }
            finally
            {
                if (guardAcquired)
                    vault.ReleaseMutationGuard(ProfileImportMutationGuardMask);
                Volatile.Write(ref s_profileCsvScratchBusy, 0);
            }
        }
#endif

#if UNITY_EDITOR
        public static bool TryGetTelemetryReadOnly(out NativeArray<ChargerTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled || runtime._mockGenerationScheduled ||
                !runtime.Resolve(in runtime._handles.TelemetryRing, out NativeArray<ChargerTelemetryEntry> telemetryBuffer) ||
                !runtime.Resolve(in runtime._handles.TelemetryCursor, out NativeArray<uint> cursorBuffer) ||
                telemetryBuffer.Length == 0 ||
                cursorBuffer.Length == 0)
            {
                return false;
            }

            telemetry = telemetryBuffer.AsReadOnly();
            cursor = unchecked((int)cursorBuffer[0]);
            return true;
        }
#endif

        public static bool TryGetGizmoLink(int index, out double3 chargerAup, out double3 nodeAup, out ChargerVisualStateDTO visual, out int count)
        {
            chargerAup = default;
            nodeAup = default;
            visual = default;
            count = 0;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled || runtime._mockGenerationScheduled)
                return false;

            if (!runtime.Resolve(in runtime._handles.LinkAup, out NativeArray<double3> linkAups) ||
                !runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                !runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) ||
                !runtime.Resolve(in runtime._powerHandles.NodeAup, out NativeArray<double3> nodeAups))
            {
                return false;
            }

            count = math.min(runtime._activeCount, math.min(links.Length, linkAups.Length));
            if ((uint)index >= (uint)count)
                return false;

            ChargerLinkDTO link = links[index];
            chargerAup = linkAups[index];
            if ((uint)link.PowerGraphNodeIndex < (uint)nodeAups.Length)
                nodeAup = nodeAups[(int)link.PowerGraphNodeIndex];
            visual = (uint)index < (uint)visuals.Length ? visuals[index] : default;
            return (link.Flags & BatteryChargerLogisticsConstants.LinkFlagActive) != 0u;
        }

        private static BatteryChargerLogisticsRuntime EnsureActiveRuntime()
        {
            if (!Application.isPlaying)
                return null;

            return s_active;
        }

        private void Initialize()
        {
            _shutdown = false;
            TryRegisterHotSwapListener();
            ApplyDataVaultRebind(GlobalRegistry.DataVault);
            SignalBus<AcousticPingSignal>.Configure(
                AcousticPingSignal.ExpectedCapacity,
                maxFrameSignals: AcousticPingSignal.MaxFrameSignals,
                lowTierFrameSignals: AcousticPingSignal.LowTierFrameSignals,
                laneHash: AcousticPingSignal.LaneHash);
            SignalBus<AcousticPingSignal>.EnsureInitialized();
            GlobalRegistry.RegisterBatteryChargerLogisticsRuntime(this);
            RegisterDispatcherPhases();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private static void ShutdownActive()
        {
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime != null)
                runtime.Shutdown();
        }

        private void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            if (_simulationScheduled)
            {
                ForceCompleteInPostSimulationWindow(ref _simulationHandle);
                _simulationScheduled = false;
            }

            if (_mockGenerationScheduled)
            {
                ForceCompleteInPostSimulationWindow(ref _mockGenerationHandle);
                _mockGenerationScheduled = false;
            }

            UnlockJobBuffers();
            UnlockMockBuffers();
            UnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            GlobalRegistry.UnregisterBatteryChargerLogisticsRuntime(this);
            _vault = null;
            _pendingVault = null;
            _hasPendingVaultRebind = false;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _simulationScheduled = false;
            _usingMockInventorySlots = false;
            _jobLockedMockInventorySlots = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
            if (!_registeredColdTick && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }

            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }

            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                QueueDataVaultRebind(currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterDispatcherPhases();
                if (currentService != null)
                    RegisterDispatcherPhases();
            }
        }

        public void ColdTick()
        {
            if (_shutdown)
                return;

            ApplyPendingVaultRebindIfIdle();

            IDataVault vault = ResolveCachedVault();
            if (vault != null)
                _vaultRepairRequested = !EnsureVaultState(vault);
            else
                _vaultRepairRequested = true;

            if (!HasGraphicsBuffersReady())
                EnsureGraphicsBuffers();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
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

        private IDataVault ResolveCachedVault()
        {
            ApplyPendingVaultRebindIfIdle();
            return _vault;
        }

        private void QueueDataVaultRebind(IDataVault vault)
        {
            _pendingVault = vault;
            _hasPendingVaultRebind = true;
            ApplyPendingVaultRebindIfIdle();
        }

        private void ApplyPendingVaultRebindIfIdle()
        {
            if (!_hasPendingVaultRebind ||
                _simulationScheduled ||
                _mockGenerationScheduled ||
                _lockedBufferMask != 0UL ||
                _mockLockedBufferMask != 0UL)
            {
                return;
            }

            IDataVault vault = _pendingVault;
            _pendingVault = null;
            _hasPendingVaultRebind = false;
            ApplyDataVaultRebind(vault);
        }

        private void ApplyDataVaultRebind(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault) && _vaultInitialized)
                return;

            UnlockJobBuffers();
            UnlockMockBuffers();

            _vault = vault;
            _handles = default;
            _powerHandles = default;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _usingMockInventorySlots = false;
            _jobLockedMockInventorySlots = false;
            _activeCount = 0;
            _powerNodeCount = 0;
            _mockPendingCount = 0;
            _mockPendingPowerNodeCount = 0;
            _skippedCadenceFrames = 0;
            _visualDirty = true;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveCachedVault();
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            ApplyTuning(in timing);
#if UNITY_EDITOR
            if ((_lastFrame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorProfileCsv();
#endif
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            if (_simulationScheduled || _mockGenerationScheduled)
                return dependsOn;

            IDataVault vault = ResolveCachedVault();
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return dependsOn;
            }

            _lastFrame = context.Frame;
            if (_activeCount <= 0)
            {
                _authorityAccumulator = 0f;
                _lastDeltaSeconds = 0f;
                return dependsOn;
            }

            float dt = ResolveSimulationTickDelta(in timing);
            _authorityAccumulator += dt;
            if (!SampleQualityWeightUnderTuningLock(vault, out float q))
                q = ResolvePendingQualityWeight();
            _lastQualityWeight = q;
            _lastCadenceHz = ResolveCadenceHz(q);
            float period = 1f / math.max(1f, _lastCadenceHz);
            if (_authorityAccumulator < period)
            {
                RecordSkippedCadenceFrame(dt);
                return dependsOn;
            }

            float integrationDt = math.min(_authorityAccumulator, 1f);

            if (!TryLockJobBuffers(vault))
                return dependsOn;

            bool keepJobGuard = false;
            try
            {
                if (!TryResolveSimulationBuffers(
                        out NativeArray<ChargerLinkDTO> links,
                        out NativeArray<double3> linkAups,
                        out NativeArray<uint> expectedHashes,
                        out NativeArray<ChargerVisualStateDTO> visuals,
                        out NativeArray<InventorySlotDTO> inventorySlots,
                        out NativeArray<PowerNodeDTO> powerNodes,
                        out NativeArray<ChargerTuningDTO> tuning,
                        out NativeArray<ChargerAtomicCountersDTO> counters))
                {
                    return dependsOn;
                }

                int linkCount = math.clamp(_activeCount, 0, math.min(links.Length, math.min(linkAups.Length, math.min(expectedHashes.Length, visuals.Length))));
                if (linkCount <= 0)
                {
                    _authorityAccumulator = 0f;
                    _lastDeltaSeconds = 0f;
                    return dependsOn;
                }

                _lastDeltaSeconds = integrationDt;
                _authorityAccumulator = math.max(0f, _authorityAccumulator - integrationDt);
                ChargerTuningDTO tune = tuning.Length > 0 ? tuning[0] : DefaultTuning();
                long start = Stopwatch.GetTimestamp();
                JobHandle handle = new ClearChargerCountersJob
                {
                    Counters = counters
                }.Schedule(dependsOn);

                ExecuteBatteryChargingJob job = new ExecuteBatteryChargingJob
                {
                    Links = (ChargerLinkDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(links),
                    LinkAup = (double3*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(linkAups),
                    ExpectedPowerNodeHashes = (uint*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(expectedHashes),
                    VisualStates = (ChargerVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(visuals),
                    InventorySlots = (InventorySlotDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(inventorySlots),
                    PowerNodes = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(powerNodes),
                    Counters = (ChargerAtomicCountersDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    LinkCount = linkCount,
                    InventorySlotCount = inventorySlots.Length,
                    PowerNodeCount = math.min(powerNodes.Length, math.max(1, _powerNodeCount)),
                    CounterLaneCount = math.max(1, math.min(counters.Length, BatteryChargerLogisticsConstants.CounterLaneCount)),
                    DeltaSeconds = integrationDt,
                    GlobalMaxChargeRate = tune.GlobalMaxChargeRate,
                    EfficiencyCurveExponent = tune.EfficiencyCurveExponent,
                    BatteryCapacity = tune.BatteryCapacity
                };
                handle = job.Schedule(linkCount, 64, handle);

                _simulationHandle = handle;
                _simulationScheduled = true;
                _jobScheduleTimestamp = start;
                H8Memory.RegisterActiveJob(SystemID.Power, handle);
                keepJobGuard = true;
                return handle;
            }
            finally
            {
                if (!keepJobGuard)
                    UnlockJobBuffers();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _vault;
            if (vault == null || !_simulationScheduled)
            {
                UnlockJobBuffers();
                return;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;

            _simulationScheduled = false;
            _lastFenceElapsedMicroseconds = ElapsedMicroseconds(_jobScheduleTimestamp);

            bool emitHum = false;
            double3 humAup = default;
            ChargerAtomicCountersDTO humAggregate = default;
            NativeArray<byte> dumpPayload = default;
            int dumpByteCount = 0;
            bool dumpPending = false;
            try
            {
                if (Resolve(in _handles.AtomicCounters, out NativeArray<ChargerAtomicCountersDTO> counters) &&
                    Resolve(in _handles.TelemetryRing, out NativeArray<ChargerTelemetryEntry> telemetry) &&
                    Resolve(in _handles.TelemetryCursor, out NativeArray<uint> cursor) &&
                    counters.Length > 0 &&
                    telemetry.Length > 0 &&
                    cursor.Length > 0)
                {
                    ChargerAtomicCountersDTO aggregate = AggregateCounters(counters);
                    WriteTelemetryFrame(telemetry, cursor, aggregate);
                    if (TryResolveHumSignalAup(aggregate, out humAup))
                    {
                        humAggregate = aggregate;
                        emitHum = true;
                    }

                    if ((_lastFenceElapsedMicroseconds > BatteryChargerLogisticsConstants.FaultDumpFenceElapsedThresholdMicroseconds ||
                         (aggregate.FaultFlags & BatteryChargerLogisticsConstants.TelemetryFlagNaN) != 0u) &&
                        !_dumpWrittenThisFault)
                    {
                        dumpPending = TryBuildDumpPayload(telemetry, cursor, out dumpPayload, out dumpByteCount);
                    }

                    if (_lastFenceElapsedMicroseconds <= BatteryChargerLogisticsConstants.FaultDumpFenceElapsedThresholdMicroseconds &&
                        (aggregate.FaultFlags & BatteryChargerLogisticsConstants.TelemetryFlagNaN) == 0u)
                    {
                        _dumpWrittenThisFault = false;
                    }
                }
            }
            finally
            {
                UnlockJobBuffers();
            }

            if (dumpPending)
            {
                try
                {
                    _dumpWrittenThisFault = NativeFaultDumpWriter.TryWriteAll(_dumpPath, dumpPayload, dumpByteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref dumpPayload,
                        nameof(BatteryChargerLogisticsRuntime),
                        "batteryChargerBlackBoxPayload");
                }
            }

            if (emitHum)
                EmitHumSignal(humAggregate, humAup);

            ApplyPendingVaultRebindIfIdle();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_activeCount <= 0 ||
                !Resolve(in _handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) ||
                !Resolve(in _handles.TelemetryRing, out NativeArray<ChargerTelemetryEntry> telemetry) ||
                !Resolve(in _handles.TelemetryCursor, out NativeArray<uint> cursor) ||
                !HasGraphicsBuffersReady())
            {
                DisableVisualGlobals();
                return;
            }

            int uploadCount = math.clamp(_activeCount, 1, math.min(visuals.Length, BatteryChargerLogisticsConstants.DefaultLinkCapacity));
            uint stateHash = ResolveLatestStateHash(telemetry, cursor);
            bool shouldUpload = _visualDirty ||
                                !_visualHasReadBuffer ||
                                _lastUploadedCount != uploadCount ||
                                _lastUploadedHash != stateHash;
            if (shouldUpload)
            {
                GraphicsBuffer writeBuffer = SelectVisualBuffer(_visualWriteSlot);
                if (UploadNativeArray(writeBuffer, visuals, uploadCount))
                {
                    _visualReadSlot = _visualWriteSlot;
                    _visualWriteSlot = 1 - _visualWriteSlot;
                    _lastUploadedCount = uploadCount;
                    _lastUploadedHash = stateHash;
                    _visualHasReadBuffer = true;
                    _visualDirty = false;
                }
            }

            GraphicsBuffer readBuffer = _visualHasReadBuffer ? SelectVisualBuffer(_visualReadSlot) : null;
            if (readBuffer == null)
            {
                DisableVisualGlobals();
                return;
            }

            Shader.SetGlobalBuffer(s_ChargerStatusBufferId, readBuffer);
            Shader.SetGlobalVector(s_ChargerStatusParamsId, new Vector4(uploadCount, _lastQualityWeight, _lastCadenceHz, _lastFenceElapsedMicroseconds));
        }

        private bool EnsureVaultState(IDataVault vault, bool requireDefaults = true)
        {
            if (!_vaultInitialized)
            {
                if (!BatteryChargerLogisticsVaultRuntime.EnsureBuffers(vault, BatteryChargerLogisticsConstants.DefaultLinkCapacity, out _handles))
                    return false;

                if (!PowerGridVaultRuntime.EnsureCoreBuffers(vault, BatteryChargerLogisticsConstants.DefaultNodeCapacity, BatteryChargerLogisticsConstants.DefaultNodeCapacity * 2, out _powerHandles))
                    return false;

                _vaultInitialized = true;
            }

            if (!_layoutChecked)
            {
#if UNITY_EDITOR
                _layoutValid = BatteryChargerLogisticsLayoutAudit.ValidateAll() && InventorySlotRuntimeLayoutValid();
#else
                _layoutValid = InventorySlotRuntimeLayoutValid();
#endif
                _layoutChecked = true;
            }

            if (!_layoutValid)
                return false;

            if (!_defaultsInitialized && _activeCount > 0)
                _defaultsInitialized = true;

            bool allowMockFallback = AllowEmergencyMockNetwork();
            if (!_defaultsInitialized && requireDefaults && allowMockFallback)
            {
                if (!_mockGenerationScheduled)
                    ScheduleEmergencyMockNetwork(vault);

                if (_mockGenerationScheduled)
                {
                    if (!DispatcherJobFence.TryFinalizeCompleted(ref _mockGenerationHandle))
                        return false;

                    _mockGenerationScheduled = false;
                    try
                    {
                        _activeCount = _mockPendingCount;
                        _powerNodeCount = _mockPendingPowerNodeCount;
                        _defaultsInitialized = true;
                        _usingMockInventorySlots = true;
                        _visualDirty = true;
                    }
                    finally
                    {
                        UnlockMockBuffers();
                    }

                    if (_hasPendingVaultRebind)
                    {
                        ApplyPendingVaultRebindIfIdle();
                        return false;
                    }
                }
            }

            return _vaultInitialized && (_defaultsInitialized || !requireDefaults || !allowMockFallback);
        }

        private bool HasVaultStateReady(bool requireDefaults = true)
        {
            if (_vault == null ||
                !_vaultInitialized ||
                !_layoutChecked ||
                !_layoutValid)
            {
                return false;
            }

            bool allowMockFallback = AllowEmergencyMockNetwork();
            if (requireDefaults && allowMockFallback && !_defaultsInitialized)
                return false;

            return TryResolveSimulationBuffers(
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private static bool InventorySlotRuntimeLayoutValid()
        {
            if (UnsafeUtility.SizeOf<InventorySlotDTO>() != 32)
                return false;

#if UNITY_EDITOR
            return InventorySlotOffset(nameof(InventorySlotDTO.ItemHashID)) == 0 &&
                   InventorySlotOffset(nameof(InventorySlotDTO.Quantity)) == 4 &&
                   InventorySlotOffset(nameof(InventorySlotDTO.ContainerAUPHash)) == 8 &&
                   InventorySlotOffset(nameof(InventorySlotDTO.ConditionFlags)) == 16 &&
                   InventorySlotOffset(nameof(InventorySlotDTO.ReservedLock)) == 20;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int InventorySlotOffset(string fieldName)
        {
            var field = typeof(InventorySlotDTO).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif

        private void ScheduleEmergencyMockNetwork(IDataVault vault)
        {
            if (_simulationScheduled || _mockGenerationScheduled || !TryLockMockBuffers(vault))
                return;

            bool keepMockGuard = false;
            try
            {
                if (!Resolve(in _handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                    !Resolve(in _handles.LinkAup, out NativeArray<double3> linkAups) ||
                    !Resolve(in _handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes) ||
                    !Resolve(in _handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) ||
                    !Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) ||
                    !Resolve(in _handles.MockInventorySlots, out NativeArray<InventorySlotDTO> inventorySlots) ||
                    !Resolve(in _powerHandles.Nodes, out NativeArray<PowerNodeDTO> powerNodes) ||
                    !Resolve(in _powerHandles.NodeAup, out NativeArray<double3> nodeAups))
                {
                    return;
                }

                if (tuning.Length > 0)
                    tuning[0] = DefaultTuning();

                int powerNodeCount = math.min(powerNodes.Length, nodeAups.IsCreated ? nodeAups.Length : powerNodes.Length);
                int linkWindow = math.min(links.Length, math.min(linkAups.Length, math.min(expectedHashes.Length, visuals.Length)));
                int count = math.min(BatteryChargerLogisticsConstants.DefaultLinkCapacity, math.min(linkWindow, inventorySlots.Length));
                if (count <= 0 || powerNodeCount <= 0)
                    return;

                GenerateMockChargerNetworkJob job = new GenerateMockChargerNetworkJob
                {
                    Links = links,
                    LinkAup = linkAups,
                    ExpectedPowerNodeHashes = expectedHashes,
                    VisualStates = visuals,
                    InventorySlots = inventorySlots,
                    PowerNodes = powerNodes,
                    PowerNodeAup = nodeAups,
                    LinkCount = count,
                    BaseAup = HectonFloatingOrigin.CurrentTotalOffsetDouble
                };
                JobHandle mockHandle = job.Schedule(count, 64);
                H8Memory.RegisterActiveJob(SystemID.Power, mockHandle);
                _mockGenerationHandle = mockHandle;
                _mockGenerationScheduled = true;
                _mockPendingCount = count;
                _mockPendingPowerNodeCount = math.min(powerNodeCount, BatteryChargerLogisticsConstants.DefaultNodeCapacity);
                keepMockGuard = true;
            }
            finally
            {
                if (!keepMockGuard)
                    UnlockMockBuffers();
            }
        }

        private static bool AllowEmergencyMockNetwork()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private void DropMockNetworkForLiveRegistration()
        {
            if (!_usingMockInventorySlots)
                return;

            _usingMockInventorySlots = false;
            _defaultsInitialized = false;
            _activeCount = 0;
            _powerNodeCount = 0;
            _mockPendingCount = 0;
            _mockPendingPowerNodeCount = 0;
            _visualDirty = true;
        }

        private void ApplyTuning(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled || _mockGenerationScheduled)
                return;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return;

            try
            {
                if (!Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) || tuning.Length == 0)
                    return;

                ChargerTuningDTO dto = tuning[0];
                ApplyPendingTuningValues(ref dto);
                tuning[0] = dto;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        private bool TryResolveSimulationBuffers(
            out NativeArray<ChargerLinkDTO> links,
            out NativeArray<double3> linkAups,
            out NativeArray<uint> expectedHashes,
            out NativeArray<ChargerVisualStateDTO> visuals,
            out NativeArray<InventorySlotDTO> inventorySlots,
            out NativeArray<PowerNodeDTO> powerNodes,
            out NativeArray<ChargerTuningDTO> tuning,
            out NativeArray<ChargerAtomicCountersDTO> counters)
        {
            links = default;
            linkAups = default;
            expectedHashes = default;
            visuals = default;
            inventorySlots = default;
            powerNodes = default;
            tuning = default;
            counters = default;

            IDataVault vault = _vault;
            bool inventoryResolved = _usingMockInventorySlots
                ? Resolve(in _handles.MockInventorySlots, out inventorySlots)
                : TryResolveInventorySlots(vault, out inventorySlots);

            return Resolve(in _handles.Links, out links) &&
                   Resolve(in _handles.LinkAup, out linkAups) &&
                   Resolve(in _handles.ExpectedPowerNodeHashes, out expectedHashes) &&
                   Resolve(in _handles.VisualStates, out visuals) &&
                   Resolve(in _powerHandles.Nodes, out powerNodes) &&
                   Resolve(in _handles.Tuning, out tuning) &&
                   Resolve(in _handles.AtomicCounters, out counters) &&
                   inventoryResolved &&
                   links.IsCreated &&
                   linkAups.IsCreated &&
                   expectedHashes.IsCreated &&
                   visuals.IsCreated &&
                   inventorySlots.IsCreated &&
                   powerNodes.IsCreated &&
                   tuning.IsCreated &&
                   counters.IsCreated;
        }

        private void RecomputeActiveTail(NativeArray<ChargerLinkDTO> links)
        {
            int scanCount = math.clamp(_activeCount, 0, links.IsCreated ? links.Length : 0);
            for (int i = scanCount - 1; i >= 0; i--)
            {
                if ((links[i].Flags & BatteryChargerLogisticsConstants.LinkFlagActive) != 0u)
                {
                    _activeCount = i + 1;
                    return;
                }
            }

            _activeCount = 0;
        }

        private static ChargerAtomicCountersDTO AggregateCounters(NativeArray<ChargerAtomicCountersDTO> counters)
        {
            ChargerAtomicCountersDTO aggregate = default;
            long active = 0;
            long full = 0;
            long unpowered = 0;
            long atomic = 0;
            long energy = 0;
            long charge = 0;
            int count = counters.IsCreated ? counters.Length : 0;
            for (int i = 0; i < count; i++)
            {
                ChargerAtomicCountersDTO lane = counters[i];
                active += math.max(0, lane.ActiveLinks);
                full += math.max(0, lane.FullLinks);
                unpowered += math.max(0, lane.UnpoweredLinks);
                atomic += math.max(0, lane.AtomicFailures);
                energy += math.max(0, lane.TotalEnergyMilli);
                charge += math.max(0, lane.ChargeMilliSum);
                aggregate.FaultFlags |= lane.FaultFlags;
                if (lane.TotalEnergyMilli > 0)
                    aggregate.LastActiveLink = lane.LastActiveLink;
                if (lane.FaultFlags != 0u)
                    aggregate.LastFaultLink = lane.LastFaultLink;
            }

            aggregate.ActiveLinks = ClampPositiveInt(active);
            aggregate.FullLinks = ClampPositiveInt(full);
            aggregate.UnpoweredLinks = ClampPositiveInt(unpowered);
            aggregate.AtomicFailures = ClampPositiveInt(atomic);
            aggregate.TotalEnergyMilli = ClampPositiveInt(energy);
            aggregate.ChargeMilliSum = ClampPositiveInt(charge);
            return aggregate;
        }

        private static int ClampPositiveInt(long value)
        {
            if (value <= 0)
                return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _vault;
            if (vault == null || handle.Generation == 0u)
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer);
        }

        private static bool TryResolveInventorySlots(IDataVault vault, out NativeArray<InventorySlotDTO> slots)
        {
            slots = default;
            if (!TryBorrowInventorySlotHandle(vault, out VaultGenerationHandle<InventorySlotDTO> handle))
                return false;

            return vault.TryResolveHandle(in handle, out slots) && slots.IsCreated;
        }

        private static bool TryReadInventorySlots(IDataVault vault, out NativeArray<InventorySlotDTO> slots)
        {
            slots = default;
            if (!TryBorrowInventorySlotHandle(vault, out VaultGenerationHandle<InventorySlotDTO> handle))
                return false;

            return vault.TryReadHandle(in handle, out slots) && slots.IsCreated;
        }

        private static bool TryAcquireInventorySlotsWrite(
            IDataVault vault,
            out VaultGenerationHandle<InventorySlotDTO> handle,
            out NativeArray<InventorySlotDTO> slots)
        {
            slots = default;
            if (!TryBorrowInventorySlotHandle(vault, out handle))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.Power, out slots))
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (slots.IsCreated)
                {
                    ownershipTransferred = true;
                    return true;
                }

                slots = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, SystemID.Power);
            }
        }

        private static bool TryBorrowInventorySlotHandle(IDataVault vault, out VaultGenerationHandle<InventorySlotDTO> handle)
        {
            handle = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<InventorySlotDTO>(BufferID.ShinobuInventorySlots, out handle) &&
                   handle.BufferID != 0u;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_lockedBufferMask != 0UL)
                return false;

            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(JobMutationGuardMask))
            {
                return false;
            }

            _lockedBufferMask = JobMutationGuardMask;
            _jobLockedMockInventorySlots = _usingMockInventorySlots;
            return true;
        }

        private bool TryLockLinkMutationBuffers(IDataVault vault, bool includePowerNodes, out ulong lockMask)
        {
            lockMask = includePowerNodes ? LinkMutationWithPowerNodesGuardMask : LinkMutationGuardMask;
            if (vault == null ||
                lockMask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(lockMask))
            {
                lockMask = 0UL;
                return false;
            }

            return true;
        }

        private void UnlockLinkMutationBuffers(IDataVault vault, ulong lockMask)
        {
            if (vault == null || lockMask == 0UL)
                return;

            vault.ReleaseMutationGuard(lockMask);
        }

        private bool TryLockMockBuffers(IDataVault vault)
        {
            if (_mockLockedBufferMask != 0UL)
                return false;

            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(MockGenerationMutationGuardMask))
            {
                return false;
            }

            _mockLockedBufferMask = MockGenerationMutationGuardMask;
            return true;
        }

        private void UnlockMockBuffers()
        {
            IDataVault vault = _vault;
            ulong lockedMask = _mockLockedBufferMask;
            _mockLockedBufferMask = 0UL;
            if (vault == null || lockedMask == 0UL)
            {
                return;
            }

            vault.ReleaseMutationGuard(lockedMask);
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _vault;
            ulong lockedMask = _lockedBufferMask;
            _lockedBufferMask = 0UL;
            _jobLockedMockInventorySlots = false;
            if (vault == null || lockedMask == 0UL)
            {
                return;
            }

            vault.ReleaseMutationGuard(lockedMask);
        }

        private static bool ForceCompleteInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ExtendPowerNodeWindowForLink(uint nodeIndex)
        {
            if (!Resolve(in _powerHandles.Nodes, out NativeArray<PowerNodeDTO> nodes) ||
                !nodes.IsCreated)
            {
                return;
            }

            int requestedNodeCount = nodeIndex >= (uint)int.MaxValue ? nodes.Length : (int)nodeIndex + 1;
            _powerNodeCount = math.max(_powerNodeCount, math.min(nodes.Length, requestedNodeCount));
        }

        private uint ReadExpectedPowerNodeHash(uint nodeIndex)
        {
            if (!Resolve(in _powerHandles.Nodes, out NativeArray<PowerNodeDTO> nodes) ||
                !nodes.IsCreated)
            {
                return 0u;
            }

            if ((uint)nodeIndex >= (uint)nodes.Length)
                return 0u;

            return nodes[(int)nodeIndex].NodeHash;
        }

        private void RecordSkippedCadenceFrame(float deltaSeconds)
        {
            _skippedCadenceFrames = math.min(_skippedCadenceFrames + 1, ushort.MaxValue);
            if (deltaSeconds > 0f && math.isfinite(deltaSeconds))
                _lastDeltaSeconds = deltaSeconds;
        }

        private void WriteTelemetryFrame(NativeArray<ChargerTelemetryEntry> telemetry, NativeArray<uint> cursor, ChargerAtomicCountersDTO aggregate)
        {
            int active = math.max(0, aggregate.ActiveLinks);
            int skippedCadenceFrames = _skippedCadenceFrames;
            ChargerTelemetryEntry entry = default;
            entry.FrameIndex = _lastFrame;
            entry.StateHash = Mix(Mix(Mix(2166136261u, (uint)active), (uint)aggregate.TotalEnergyMilli), (uint)aggregate.AtomicFailures);
            entry.Flags = aggregate.FaultFlags;
            if (_lastFenceElapsedMicroseconds > BatteryChargerLogisticsConstants.FaultDumpFenceElapsedThresholdMicroseconds)
                entry.Flags |= BatteryChargerLogisticsConstants.TelemetryFlagExceededBudget;
            if (skippedCadenceFrames > 0)
                entry.Flags |= BatteryChargerLogisticsConstants.TelemetryFlagSkippedCadence;
            entry.ActiveLinks = active;
            entry.FullLinks = math.max(0, aggregate.FullLinks);
            entry.UnpoweredLinks = math.max(0, aggregate.UnpoweredLinks);
            entry.AtomicLockFailures = math.max(0, aggregate.AtomicFailures);
            entry.FenceElapsedMicroseconds = math.max(0, (int)_lastFenceElapsedMicroseconds);
            entry.TotalEnergyDrawn = aggregate.TotalEnergyMilli * 0.001f;
            entry.GlobalQualityWeight = _lastQualityWeight;
            entry.CadenceHz = _lastCadenceHz;
            entry.DeltaSeconds = _lastDeltaSeconds;
            entry.AverageCharge01 = active > 0 ? math.saturate((aggregate.ChargeMilliSum * 0.001f) / math.max(1, active)) : 0f;
            entry.LinkCapacity = BatteryChargerLogisticsConstants.DefaultLinkCapacity;
            entry.LastFaultLink = aggregate.LastFaultLink;
            entry.SkippedCadenceFrames = (uint)math.max(0, skippedCadenceFrames);
            WriteTelemetryEntry(telemetry, cursor, in entry);
            _skippedCadenceFrames = 0;
        }

        private static void WriteTelemetryEntry(NativeArray<ChargerTelemetryEntry> telemetry, NativeArray<uint> cursor, in ChargerTelemetryEntry entry)
        {
            int index = (int)(cursor[0] % (uint)telemetry.Length);
            telemetry[index] = entry;
            cursor[0] = unchecked(cursor[0] + 1u);
        }

        private bool TryResolveHumSignalAup(ChargerAtomicCountersDTO aggregate, out double3 aup)
        {
            aup = default;
            if (aggregate.ActiveLinks <= 0 || aggregate.TotalEnergyMilli <= 0 ||
                !Resolve(in _handles.LinkAup, out NativeArray<double3> linkAups) ||
                !linkAups.IsCreated ||
                linkAups.Length == 0)
            {
                return false;
            }

            int linkIndex = (int)aggregate.LastActiveLink;
            if ((uint)linkIndex >= (uint)linkAups.Length)
                return false;

            aup = linkAups[linkIndex];
            return true;
        }

        private static void EmitHumSignal(ChargerAtomicCountersDTO aggregate, double3 aup)
        {
            AcousticPingSignal signal = default;
            if (!TryWriteAbsoluteAupFields(ref signal, aup))
                return;
            signal.RadiusMeters = 5.5f;
            signal.Intensity01 = math.saturate(aggregate.TotalEnergyMilli * 0.001f);
            signal.SourceId = BatteryChargerLogisticsConstants.HumSourceHash;
            signal.Channel = AcousticPingSignal.ChannelMetalStress;
            signal.Flags = 0;
            SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref s_x001BatteryChargerLogisticsRuntimeSignalPushDropCount);
        }

        private static bool TryWriteAbsoluteAupFields(ref AcousticPingSignal signal, double3 absolutePosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            if (math.abs(absolutePosition.x) > AcousticHumMaxAupExtentMeters ||
                math.abs(absolutePosition.y) > AcousticHumMaxAupExtentMeters ||
                math.abs(absolutePosition.z) > AcousticHumMaxAupExtentMeters)
                return false;

            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);

            double originX = gridX * cellSize;
            double originY = gridY * cellSize;
            double originZ = gridZ * cellSize;

            signal.PositionAup.GridX = gridX;
            signal.PositionAup.GridY = gridY;
            signal.PositionAup.GridZ = gridZ;
            signal.PositionAup.LocalX = (float)(absolutePosition.x - originX);
            signal.PositionAup.LocalY = (float)(absolutePosition.y - originY);
            signal.PositionAup.LocalZ = (float)(absolutePosition.z - originZ);
            return math.isfinite(signal.PositionAup.LocalX) &&
                   math.isfinite(signal.PositionAup.LocalY) &&
                   math.isfinite(signal.PositionAup.LocalZ);
        }

        private static unsafe bool TryBuildDumpPayload(
            NativeArray<ChargerTelemetryEntry> telemetry,
            NativeArray<uint> cursor,
            out NativeArray<byte> payload,
            out int byteCount)
        {
            payload = default;
            byteCount = 0;
            if (!telemetry.IsCreated)
                return false;

            Span<byte> header = stackalloc byte[20];
            WriteUInt64LittleEndian(header, 0, 0x534832333044554DuL); // SH230DUM
            WriteUInt32LittleEndian(header, 8, 1u);
            WriteUInt32LittleEndian(header, 12, (uint)telemetry.Length);
            WriteUInt32LittleEndian(header, 16, cursor.Length > 0 ? cursor[0] : 0u);

            int entrySize = UnsafeUtility.SizeOf<ChargerTelemetryEntry>();
            long totalBytes = header.Length + ((long)telemetry.Length * entrySize);
            if (totalBytes < header.Length || totalBytes > int.MaxValue)
                return false;

            payload = NativeFaultDumpWriter.CreateTransientPayload(
                (int)totalBytes,
                nameof(BatteryChargerLogisticsRuntime),
                "batteryChargerBlackBoxPayload");
            try
            {
                for (int i = 0; i < header.Length; i++)
                    payload[i] = header[i];

                if (telemetry.Length > 0)
                {
                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    void* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload) + header.Length;
                    UnsafeUtility.MemCpy(destination, telemetryPtr, telemetry.Length * entrySize);
                }

                byteCount = (int)totalBytes;
                return true;
            }
            catch
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(BatteryChargerLogisticsRuntime),
                    "batteryChargerBlackBoxPayload");

                payload = default;
                byteCount = 0;
                return false;
            }
        }

        private static bool TryAcquireColdSlotLock(uint* lockPtr)
        {
            ref int location = ref UnsafeUtility.AsRef<int>(lockPtr);
            int observed = Interlocked.CompareExchange(
                ref location,
                unchecked((int)BatteryChargerLogisticsConstants.LockToken),
                0);
            return observed == 0;
        }

        private static void ReleaseColdSlotLock(uint* lockPtr)
        {
            ref int location = ref UnsafeUtility.AsRef<int>(lockPtr);
            Interlocked.Exchange(ref location, 0);
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(Span<byte> destination, int offset, ulong value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            destination[offset + 4] = (byte)(value >> 32);
            destination[offset + 5] = (byte)(value >> 40);
            destination[offset + 6] = (byte)(value >> 48);
            destination[offset + 7] = (byte)(value >> 56);
        }

#if UNITY_EDITOR
        private void MonitorProfileCsv()
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime lastWrite = File.GetLastWriteTimeUtc(_csvPath);
            if (lastWrite == _csvLastWriteUtc)
                return;

            if (_simulationScheduled || _mockGenerationScheduled)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (Interlocked.CompareExchange(ref s_profileCsvScratchBusy, 1, 0) != 0)
                return;

            try
            {
                int totalRead = ReadCsvBytesCold(_csvPath, s_profileCsvScratchCold);
                if (totalRead <= 0)
                    return;

                if (!BatteryChargerProfileCsvParser.TryParseProfiles(
                        s_profileCsvScratchCold.AsSpan(0, totalRead),
                        s_profileImportScratch.AsSpan(),
                        out int profileCount))
                {
                    return;
                }

                if (!vault.TryAcquireMutationGuard(ProfileImportMutationGuardMask))
                    return;

                try
                {
                    if (!Resolve(in _handles.Profiles, out NativeArray<ChargerProfileDTO> profiles) ||
                        !profiles.IsCreated)
                    {
                        return;
                    }

                    CommitProfilesCsv(s_profileImportScratch.AsSpan(), profileCount, profiles);
                    _csvLastWriteUtc = lastWrite;
                }
                finally
                {
                    vault.ReleaseMutationGuard(ProfileImportMutationGuardMask);
                }
            }
            finally
            {
                Volatile.Write(ref s_profileCsvScratchBusy, 0);
            }
        }

        private static int ReadCsvBytesCold(string path, byte[] scratch)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileLength = stream.Length < 0L ? 0L : stream.Length;
            int safeLength = fileLength > scratch.Length ? scratch.Length : (int)fileLength;
            if (safeLength <= 0)
                return 0;

            int totalRead = 0;
            while (totalRead < safeLength)
            {
                int read = stream.Read(scratch, totalRead, safeLength - totalRead);
                if (read <= 0)
                    break;
                totalRead += read;
            }

            return totalRead;
        }

        private static void CommitProfilesCsv(
            ReadOnlySpan<ChargerProfileDTO> parsedProfiles,
            int parsedCount,
            NativeArray<ChargerProfileDTO> profiles)
        {
            int safeCount = math.min(math.min(parsedCount, parsedProfiles.Length), profiles.Length);
            for (int i = 0; i < safeCount; i++)
                profiles[i] = parsedProfiles[i];

            for (int i = safeCount; i < profiles.Length; i++)
                profiles[i] = default;
        }
#endif

        private static ChargerTuningDTO DefaultTuning()
        {
            ChargerTuningDTO dto = default;
            ApplyPendingTuningValues(ref dto);
            return dto;
        }

        private static void ApplyPendingTuningValues(ref ChargerTuningDTO dto)
        {
            float maxChargeRate = SanitizeNonNegative(s_pendingMaxChargeRate);
            float efficiencyExponent = SanitizePositive(s_pendingEfficiencyExponent, 0.0001f, BatteryChargerLogisticsConstants.DefaultEfficiencyExponent);
            float qualityOverride = SanitizeQualityOverride(s_pendingQualityOverride);
            s_pendingMaxChargeRate = maxChargeRate;
            s_pendingEfficiencyExponent = efficiencyExponent;
            s_pendingQualityOverride = qualityOverride;
            dto.GlobalMaxChargeRate = maxChargeRate;
            dto.EfficiencyCurveExponent = efficiencyExponent;
            dto.QualityOverride = qualityOverride;
            dto.Flags = qualityOverride >= 0f ? 1u : 0u;
            dto.GlobalQualityWeight = ResolvePendingQualityWeight();
            dto.BatteryCapacity = BatteryChargerLogisticsConstants.DefaultBatteryCapacity01;
            dto.CadenceHz = ResolveCadenceHzStatic(dto.GlobalQualityWeight);
        }

        private bool SampleQualityWeightUnderTuningLock(IDataVault vault, out float q)
        {
            q = ResolvePendingQualityWeight();
            if (vault == null || !vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return false;

            try
            {
                if (!Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) ||
                    tuning.Length == 0)
                {
                    return false;
                }

                q = MathLodApproximation.SaturateFinite(tuning[0].GlobalQualityWeight, q);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        private static float ResolvePendingQualityWeight()
        {
            float qualityOverride = SanitizeQualityOverride(s_pendingQualityOverride);
            if (qualityOverride >= 0f)
                return math.saturate(qualityOverride);

            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return AuthoritativeQualityWeight;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.max(0f, math.isfinite(value) ? value : 0f);
        }

        private static float SanitizePositive(float value, float minimum, float fallback)
        {
            float safe = math.isfinite(value) ? value : fallback;
            return math.max(minimum, safe);
        }

        private static float SanitizeQualityOverride(float value)
        {
            return math.isfinite(value) ? math.clamp(value, -1f, 1f) : -1f;
        }

        private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = math.isfinite(timing.FixedDelta) ? timing.FixedDelta : 0f;
            if (fixedDelta > 0.00001f)
                return math.clamp(fixedDelta, 1f / 240f, 1f / 5f);

            return SimulationTickDeltaSeconds;
        }

        private float ResolveCadenceHz(float quality)
        {
            return ResolveCadenceHzStatic(quality);
        }

        private static float ResolveCadenceHzStatic(float quality)
        {
            float q = MathLodApproximation.SaturateFinite(quality, AuthoritativeQualityWeight);
            float curve = MathLodApproximation.SmoothStep01(q);
            return math.lerp(MinimumCadenceHz, MaximumCadenceHz, curve);
        }

        private bool EnsureGraphicsBuffers()
        {
            int stride = UnsafeUtility.SizeOf<ChargerVisualStateDTO>();
            bool changedA = EnsureBuffer(ref _visualBufferA, BatteryChargerLogisticsConstants.DefaultLinkCapacity, stride);
            bool changedB = EnsureBuffer(ref _visualBufferB, BatteryChargerLogisticsConstants.DefaultLinkCapacity, stride);
            if (changedA || changedB)
            {
                _visualReadSlot = 0;
                _visualWriteSlot = 1;
                _visualHasReadBuffer = false;
                _visualDirty = true;
            }

            return _visualBufferA != null && _visualBufferB != null;
        }

        private bool HasGraphicsBuffersReady()
        {
            int stride = UnsafeUtility.SizeOf<ChargerVisualStateDTO>();
            return _visualBufferA != null &&
                   _visualBufferA.count == BatteryChargerLogisticsConstants.DefaultLinkCapacity &&
                   _visualBufferA.stride == stride &&
                   _visualBufferB != null &&
                   _visualBufferB.count == BatteryChargerLogisticsConstants.DefaultLinkCapacity &&
                   _visualBufferB.stride == stride;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

            ReleaseGraphicsBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
            return true;
        }

        private GraphicsBuffer SelectVisualBuffer(int slot)
        {
            return (slot & 1) == 0 ? _visualBufferA : _visualBufferB;
        }

        private static bool UploadNativeArray(GraphicsBuffer destination, NativeArray<ChargerVisualStateDTO> source, int count)
        {
            if (destination == null || !source.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<ChargerVisualStateDTO>())
                return false;

            NativeArray<ChargerVisualStateDTO> mapped = destination.LockBufferForWrite<ChargerVisualStateDTO>(0, safeCount);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<ChargerVisualStateDTO>());
            }
            finally
            {
                destination.UnlockBufferAfterWrite<ChargerVisualStateDTO>(safeCount);
            }
            return true;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void DisableVisualGlobals()
        {
            Shader.SetGlobalVector(s_ChargerStatusParamsId, Vector4.zero);
        }

        private static uint ResolveLatestStateHash(NativeArray<ChargerTelemetryEntry> telemetry, NativeArray<uint> cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || !cursor.IsCreated || cursor.Length == 0 || cursor[0] == 0u)
                return 0u;

            uint read = cursor[0] - 1u;
            return telemetry[(int)(read % (uint)telemetry.Length)].StateHash;
        }

        private static float ElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0f;

            long ticks = Stopwatch.GetTimestamp() - startTimestamp;
            return (float)((double)ticks * 1000000.0 / Stopwatch.Frequency);
        }

        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BatteryChargerLogisticsRuntime _owner;
            public PreSimulationPhaseSystem(BatteryChargerLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return PreSimulationHash; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BatteryChargerLogisticsRuntime _owner;
            public SimulationPhaseSystem(BatteryChargerLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return SimulationHash; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BatteryChargerLogisticsRuntime _owner;
            public PostSimulationPhaseSystem(BatteryChargerLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return PostSimulationHash; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly BatteryChargerLogisticsRuntime _owner;
            public VisualSyncPhaseSystem(BatteryChargerLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return VisualSyncHash; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }
}
