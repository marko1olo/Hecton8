using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    public sealed unsafe class BatteryChargerLogisticsRuntime
    {
        private const uint SystemHash = 0x53323330u; // S230
        private const uint PreSimulationHash = 0x32333050u;
        private const uint SimulationHash = 0x32333053u;
        private const uint PostSimulationHash = 0x3233304Fu;
        private const uint VisualSyncHash = 0x32333056u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_230.bin";
        private const string CsvRelativePath = "Docs/Power/battery_charger_profiles.csv";
        private const int CsvPollCadenceFrames = 128;

        private static readonly int s_ChargerStatusBufferId = Shader.PropertyToID("_H8BatteryChargerStatusBuffer");
        private static readonly int s_ChargerStatusParamsId = Shader.PropertyToID("_H8BatteryChargerStatusParams");

        private static BatteryChargerLogisticsRuntime s_active;
        private static float s_pendingMaxChargeRate = BatteryChargerLogisticsConstants.DefaultMaxChargeRate01PerSecond;
        private static float s_pendingEfficiencyExponent = BatteryChargerLogisticsConstants.DefaultEfficiencyExponent;
        private static float s_pendingQualityOverride = -1f;

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
        private bool _vaultInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _defaultsInitialized;
        private bool _simulationScheduled;
        private bool _dumpWrittenThisFault;
        private int _lockedBufferMask;
        private int _activeCount;
        private int _powerNodeCount;
        private uint _lastFrame;
        private float _authorityAccumulator;
        private float _lastDeltaSeconds;
        private float _lastCadenceHz = 5f;
        private float _lastQualityWeight = 1f;
        private float _lastScheduleMicroseconds;
        private long _jobScheduleTimestamp;
        private JobHandle _simulationHandle;
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
            s_active = null;
            s_pendingMaxChargeRate = BatteryChargerLogisticsConstants.DefaultMaxChargeRate01PerSecond;
            s_pendingEfficiencyExponent = BatteryChargerLogisticsConstants.DefaultEfficiencyExponent;
            s_pendingQualityOverride = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
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
            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultState(vault))
                return false;

            if (!runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                !runtime.Resolve(in runtime._handles.LinkAup, out NativeArray<double3> linkAups) ||
                !runtime.Resolve(in runtime._handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes) ||
                !runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals))
            {
                return false;
            }

            int capacity = math.min(links.Length, math.min(linkAups.Length, expectedHashes.Length));
            for (int i = 0; i < capacity; i++)
            {
                ChargerLinkDTO existing = links[i];
                if ((existing.Flags & BatteryChargerLogisticsConstants.LinkFlagActive) != 0u &&
                    (existing.Flags & BatteryChargerLogisticsConstants.LinkFlagMock) == 0u)
                {
                    continue;
                }

                ChargerLinkDTO link = default;
                link.InventorySlotIndex = inventorySlotIndex;
                link.PowerGraphNodeIndex = powerGraphNodeIndex;
                link.ChargeRate = math.max(0f, chargeRate);
                link.EfficiencyScalar = math.max(0f, efficiencyScalar);
                link.Flags = BatteryChargerLogisticsConstants.LinkFlagActive;
                links[i] = link;
                linkAups[i] = chargerAup;
                expectedHashes[i] = runtime.ResolveExpectedPowerNodeHash(powerGraphNodeIndex);

                if ((uint)i < (uint)visuals.Length)
                {
                    ChargerVisualStateDTO visual = default;
                    visual.Status = 0u;
                    visual.Flags = link.Flags;
                    visual.LinkIndex = (uint)i;
                    visual.InventorySlotIndex = inventorySlotIndex;
                    visual.PowerGraphNodeIndex = powerGraphNodeIndex;
                    visuals[i] = visual;
                }

                runtime._activeCount = math.max(runtime._activeCount, i + 1);
                runtime._visualDirty = true;
                linkIndex = i;
                return true;
            }

            return false;
        }

        public static void TryUnregisterChargerLink(int linkIndex)
        {
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || linkIndex < 0 || runtime._simulationScheduled)
                return;

            if (!runtime.Resolve(in runtime._handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                (uint)linkIndex >= (uint)links.Length)
            {
                return;
            }

            links[linkIndex] = default;
            if (runtime.Resolve(in runtime._handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) &&
                (uint)linkIndex < (uint)visuals.Length)
            {
                visuals[linkIndex] = default;
            }

            runtime._visualDirty = true;
        }

        public static bool TryWriteInventorySlotState(uint inventorySlotIndex, uint itemHash, float charge01)
        {
            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null || runtime._simulationScheduled)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultState(vault))
                return false;

            if (!vault.TryGetBuffer(BufferID.ShinobuInventorySlots, out NativeArray<InventorySlotDTO> slots) ||
                !slots.IsCreated ||
                (uint)inventorySlotIndex >= (uint)slots.Length)
            {
                return false;
            }

            InventorySlotDTO slot = slots[(int)inventorySlotIndex];
            slot.ItemHashID = itemHash;
            slot.Quantity = itemHash == 0u ? 0u : 1u;
            slot.ConditionFlags = math.asuint(math.saturate(math.isfinite(charge01) ? charge01 : 0f));
            slot.ReservedLock = 0u;
            slots[(int)inventorySlotIndex] = slot;
            return true;
        }

        public static bool TryReadCharge01(uint inventorySlotIndex, out float charge01)
        {
            charge01 = 0f;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.ShinobuInventorySlots, out NativeArray<InventorySlotDTO> slots) ||
                !slots.IsCreated ||
                (uint)inventorySlotIndex >= (uint)slots.Length)
            {
                return false;
            }

            InventorySlotDTO slot = slots[(int)inventorySlotIndex];
            if (slot.ItemHashID == 0u || slot.Quantity == 0u)
                return false;

            float value = math.asfloat(slot.ConditionFlags);
            charge01 = math.saturate(math.isfinite(value) ? value : 0f);
            return true;
        }

        public static bool TryReadEditorState(out int activeCount, out float quality, out float cadenceHz, out float lastScheduleMicroseconds)
        {
            activeCount = 0;
            quality = 0f;
            cadenceHz = 0f;
            lastScheduleMicroseconds = 0f;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null)
                return false;

            activeCount = runtime._activeCount;
            quality = runtime._lastQualityWeight;
            cadenceHz = runtime._lastCadenceHz;
            lastScheduleMicroseconds = runtime._lastScheduleMicroseconds;
            return true;
        }

        public static bool TryApplyEditorTuning(float maxChargeRate, float efficiencyExponent, float qualityOverride)
        {
            s_pendingMaxChargeRate = math.max(0f, maxChargeRate);
            s_pendingEfficiencyExponent = math.max(0.0001f, efficiencyExponent);
            s_pendingQualityOverride = math.clamp(qualityOverride, -1f, 1f);

            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled ||
                !runtime.Resolve(in runtime._handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) ||
                tuning.Length == 0)
            {
                return runtime != null;
            }

            ChargerTuningDTO dto = tuning[0];
            dto.GlobalMaxChargeRate = s_pendingMaxChargeRate;
            dto.EfficiencyCurveExponent = s_pendingEfficiencyExponent;
            dto.QualityOverride = s_pendingQualityOverride;
            dto.Flags = s_pendingQualityOverride >= 0f ? 1u : 0u;
            tuning[0] = dto;
            return true;
        }

        public static bool TryLoadProfilesFromCsvBytes(ReadOnlySpan<byte> csv)
        {
            BatteryChargerLogisticsRuntime runtime = EnsureActiveRuntime();
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultState(vault) ||
                !runtime.Resolve(in runtime._handles.Profiles, out NativeArray<ChargerProfileDTO> profiles))
            {
                return false;
            }

            return BatteryChargerProfileCsvParser.TryParseProfiles(csv, profiles, out _);
        }

#if UNITY_EDITOR
        public static bool TryGetTelemetryReadOnly(out NativeArray<ChargerTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime == null || runtime._simulationScheduled ||
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
            if (runtime == null || runtime._simulationScheduled)
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

            BatteryChargerLogisticsRuntime runtime = s_active;
            if (runtime != null)
                return runtime;

            runtime = new BatteryChargerLogisticsRuntime();
            s_active = runtime;
            runtime.Initialize();
            return runtime;
        }

        private void Initialize()
        {
            _shutdown = false;
            _vault = GlobalRegistry.DataVault;
            SignalBus<AcousticPingSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 8, laneHash: BatteryChargerLogisticsConstants.HumSourceHash);
            SignalBus<AcousticPingSignal>.EnsureInitialized();
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
            UnlockJobBuffers();
            UnregisterDispatcherPhases();
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            _vault = null;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _simulationScheduled = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
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
        }

        private IDataVault ResolveVault()
        {
            IDataVault vault = _vault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            _vault = vault;
            return vault;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return;

            ApplyTuning(in timing);
#if UNITY_EDITOR
            if ((_lastFrame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorProfileCsv();
#endif
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return dependsOn;

            _lastFrame = context.Frame;
            float dt = math.max(0f, math.isfinite(timing.FrameDelta) ? timing.FrameDelta : 0f);
            _authorityAccumulator += dt;
            float q = ResolveQualityWeight();
            _lastQualityWeight = q;
            _lastCadenceHz = ResolveCadenceHz(q);
            float period = 1f / math.max(1f, _lastCadenceHz);
            if (_authorityAccumulator < period)
                return dependsOn;

            float integrationDt = math.min(_authorityAccumulator, 1f);
            _authorityAccumulator = 0f;
            _lastDeltaSeconds = integrationDt;

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

            int linkCount = math.clamp(_activeCount, 0, math.min(links.Length, math.min(linkAups.Length, expectedHashes.Length)));
            if (linkCount <= 0)
                return dependsOn;

            if (!TryLockJobBuffers(vault))
                return dependsOn;

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
            return handle;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !_simulationScheduled)
            {
                UnlockJobBuffers();
                return;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;

            _simulationScheduled = false;
            _lastScheduleMicroseconds = ElapsedMicroseconds(_jobScheduleTimestamp);

            if (Resolve(in _handles.AtomicCounters, out NativeArray<ChargerAtomicCountersDTO> counters) &&
                Resolve(in _handles.TelemetryRing, out NativeArray<ChargerTelemetryEntry> telemetry) &&
                Resolve(in _handles.TelemetryCursor, out NativeArray<uint> cursor) &&
                counters.Length > 0 &&
                telemetry.Length > 0 &&
                cursor.Length > 0)
            {
                ChargerAtomicCountersDTO aggregate = counters[0];
                WriteTelemetryFrame(telemetry, cursor, aggregate);
                TryEmitHumSignal(aggregate);
                if ((_lastScheduleMicroseconds > BatteryChargerLogisticsConstants.FaultDumpThresholdMicroseconds ||
                     (aggregate.FaultFlags & BatteryChargerLogisticsConstants.TelemetryFlagNaN) != 0u) &&
                    !_dumpWrittenThisFault)
                {
                    WriteDump(telemetry, cursor);
                }

                if (_lastScheduleMicroseconds <= BatteryChargerLogisticsConstants.FaultDumpThresholdMicroseconds &&
                    (aggregate.FaultFlags & BatteryChargerLogisticsConstants.TelemetryFlagNaN) == 0u)
                {
                    _dumpWrittenThisFault = false;
                }
            }

            UnlockJobBuffers();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_activeCount <= 0 ||
                !Resolve(in _handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) ||
                !Resolve(in _handles.TelemetryRing, out NativeArray<ChargerTelemetryEntry> telemetry) ||
                !Resolve(in _handles.TelemetryCursor, out NativeArray<uint> cursor) ||
                !EnsureGraphicsBuffers())
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
            Shader.SetGlobalVector(s_ChargerStatusParamsId, new Vector4(uploadCount, _lastQualityWeight, _lastCadenceHz, _lastScheduleMicroseconds));
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (!_vaultInitialized)
            {
                if (!BatteryChargerLogisticsVaultRuntime.EnsureBuffers(vault, BatteryChargerLogisticsConstants.DefaultLinkCapacity, out _handles))
                    return false;

                InventoryRoutingNetwork.EnsureBuffers(vault, BatteryChargerLogisticsConstants.DefaultLinkCapacity);
                if (!PowerGridVaultRuntime.EnsureCoreBuffers(vault, BatteryChargerLogisticsConstants.DefaultNodeCapacity, BatteryChargerLogisticsConstants.DefaultNodeCapacity * 2, out _powerHandles))
                    return false;

                _vaultInitialized = true;
            }

            if (!_layoutChecked)
            {
#if UNITY_EDITOR
                _layoutValid = BatteryChargerLogisticsLayoutAudit.ValidateAll() && InventoryRoutingNetwork.RuntimeLayoutValid();
#else
                _layoutValid = InventoryRoutingNetwork.RuntimeLayoutValid();
#endif
                _layoutChecked = true;
            }

            if (!_defaultsInitialized || !_layoutValid)
                GenerateEmergencyMockNetwork(vault);

            return _vaultInitialized;
        }

        private void GenerateEmergencyMockNetwork(IDataVault vault)
        {
            if (!Resolve(in _handles.Links, out NativeArray<ChargerLinkDTO> links) ||
                !Resolve(in _handles.LinkAup, out NativeArray<double3> linkAups) ||
                !Resolve(in _handles.ExpectedPowerNodeHashes, out NativeArray<uint> expectedHashes) ||
                !Resolve(in _handles.VisualStates, out NativeArray<ChargerVisualStateDTO> visuals) ||
                !Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) ||
                !Resolve(in _powerHandles.Nodes, out NativeArray<PowerNodeDTO> powerNodes) ||
                !Resolve(in _powerHandles.NodeAup, out NativeArray<double3> nodeAups) ||
                !vault.TryGetBuffer(BufferID.ShinobuInventorySlots, out NativeArray<InventorySlotDTO> inventorySlots))
            {
                return;
            }

            if (tuning.Length > 0)
                tuning[0] = DefaultTuning();

            int count = math.min(BatteryChargerLogisticsConstants.DefaultLinkCapacity, math.min(links.Length, inventorySlots.Length));
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
            for (int i = 0; i < count; i++)
                job.Execute(i);

            _activeCount = count;
            _powerNodeCount = math.min(powerNodes.Length, BatteryChargerLogisticsConstants.DefaultNodeCapacity);
            _defaultsInitialized = true;
            _visualDirty = true;
        }

        private void ApplyTuning(in DispatcherTimingDTO timing)
        {
            if (!Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) || tuning.Length == 0)
                return;

            ChargerTuningDTO dto = tuning[0];
            dto.GlobalMaxChargeRate = math.max(0f, s_pendingMaxChargeRate);
            dto.EfficiencyCurveExponent = math.max(0.0001f, s_pendingEfficiencyExponent);
            dto.QualityOverride = s_pendingQualityOverride;
            dto.Flags = s_pendingQualityOverride >= 0f ? 1u : 0u;
            dto.GlobalQualityWeight = ResolveQualityWeight();
            dto.CadenceHz = ResolveCadenceHz(dto.GlobalQualityWeight);
            dto.BatteryCapacity = BatteryChargerLogisticsConstants.DefaultBatteryCapacity01;
            tuning[0] = dto;
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
            inventorySlots = default;
            IDataVault vault = ResolveVault();
            return Resolve(in _handles.Links, out links) &&
                   Resolve(in _handles.LinkAup, out linkAups) &&
                   Resolve(in _handles.ExpectedPowerNodeHashes, out expectedHashes) &&
                   Resolve(in _handles.VisualStates, out visuals) &&
                   Resolve(in _powerHandles.Nodes, out powerNodes) &&
                   Resolve(in _handles.Tuning, out tuning) &&
                   Resolve(in _handles.AtomicCounters, out counters) &&
                   vault != null &&
                   vault.TryGetBuffer(BufferID.ShinobuInventorySlots, out inventorySlots) &&
                   links.IsCreated &&
                   linkAups.IsCreated &&
                   expectedHashes.IsCreated &&
                   visuals.IsCreated &&
                   inventorySlots.IsCreated &&
                   powerNodes.IsCreated &&
                   tuning.IsCreated &&
                   counters.IsCreated;
        }

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveVault();
            if (vault == null || handle.Generation == 0u)
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer);
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            UnlockJobBuffers();
            if (!TryLock(vault, BatteryChargerLogisticsBufferIds.Links, 1 << 0)) return false;
            if (!TryLock(vault, BatteryChargerLogisticsBufferIds.VisualStates, 1 << 1)) return false;
            if (!TryLock(vault, BufferID.ShinobuInventorySlots, 1 << 2)) return false;
            if (!TryLock(vault, PowerGridBufferIds.Nodes, 1 << 3)) return false;
            if (!TryLock(vault, BatteryChargerLogisticsBufferIds.AtomicCounters, 1 << 4)) return false;
            return true;
        }

        private bool TryLock(IDataVault vault, BufferID id, int bit)
        {
            if (!vault.TryLockBuffer(id, SystemID.Power))
            {
                UnlockJobBuffers();
                return false;
            }

            _lockedBufferMask |= bit;
            return true;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null || _lockedBufferMask == 0)
            {
                _lockedBufferMask = 0;
                return;
            }

            if ((_lockedBufferMask & (1 << 4)) != 0) vault.TryUnlockBuffer(BatteryChargerLogisticsBufferIds.AtomicCounters, SystemID.Power);
            if ((_lockedBufferMask & (1 << 3)) != 0) vault.TryUnlockBuffer(PowerGridBufferIds.Nodes, SystemID.Power);
            if ((_lockedBufferMask & (1 << 2)) != 0) vault.TryUnlockBuffer(BufferID.ShinobuInventorySlots, SystemID.Power);
            if ((_lockedBufferMask & (1 << 1)) != 0) vault.TryUnlockBuffer(BatteryChargerLogisticsBufferIds.VisualStates, SystemID.Power);
            if ((_lockedBufferMask & 1) != 0) vault.TryUnlockBuffer(BatteryChargerLogisticsBufferIds.Links, SystemID.Power);
            _lockedBufferMask = 0;
        }

        private uint ResolveExpectedPowerNodeHash(uint nodeIndex)
        {
            if (!Resolve(in _powerHandles.Nodes, out NativeArray<PowerNodeDTO> nodes) ||
                !nodes.IsCreated ||
                (uint)nodeIndex >= (uint)nodes.Length)
            {
                return 0u;
            }

            return nodes[(int)nodeIndex].NodeHash;
        }

        private void WriteTelemetryFrame(NativeArray<ChargerTelemetryEntry> telemetry, NativeArray<uint> cursor, ChargerAtomicCountersDTO aggregate)
        {
            int index = (int)(cursor[0] % (uint)telemetry.Length);
            int active = math.max(0, aggregate.ActiveLinks);
            ChargerTelemetryEntry entry = default;
            entry.FrameIndex = _lastFrame;
            entry.StateHash = Mix(Mix(Mix(2166136261u, (uint)active), (uint)aggregate.TotalEnergyMilli), (uint)aggregate.AtomicFailures);
            entry.Flags = aggregate.FaultFlags;
            if (_lastScheduleMicroseconds > BatteryChargerLogisticsConstants.FaultDumpThresholdMicroseconds)
                entry.Flags |= BatteryChargerLogisticsConstants.TelemetryFlagExceededBudget;
            entry.ActiveLinks = active;
            entry.FullLinks = math.max(0, aggregate.FullLinks);
            entry.UnpoweredLinks = math.max(0, aggregate.UnpoweredLinks);
            entry.AtomicLockFailures = math.max(0, aggregate.AtomicFailures);
            entry.BurstMicroseconds = math.max(0, (int)_lastScheduleMicroseconds);
            entry.TotalEnergyDrawn = aggregate.TotalEnergyMilli * 0.001f;
            entry.GlobalQualityWeight = _lastQualityWeight;
            entry.CadenceHz = _lastCadenceHz;
            entry.DeltaSeconds = _lastDeltaSeconds;
            entry.AverageCharge01 = active > 0 ? math.saturate((aggregate.ChargeMilliSum * 0.001f) / math.max(1, active)) : 0f;
            entry.LinkCapacity = BatteryChargerLogisticsConstants.DefaultLinkCapacity;
            entry.LastFaultLink = aggregate.LastFaultLink;
            telemetry[index] = entry;
            cursor[0] = unchecked(cursor[0] + 1u);
        }

        private void TryEmitHumSignal(ChargerAtomicCountersDTO aggregate)
        {
            if (aggregate.ActiveLinks <= 0 || aggregate.TotalEnergyMilli <= 0 ||
                !Resolve(in _handles.LinkAup, out NativeArray<double3> linkAups) ||
                !linkAups.IsCreated ||
                linkAups.Length == 0)
            {
                return;
            }

            double3 aup = linkAups[0];
            AcousticPingSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(aup);
            signal.RadiusMeters = 5.5f;
            signal.Intensity01 = math.saturate(aggregate.TotalEnergyMilli * 0.001f);
            signal.SourceId = BatteryChargerLogisticsConstants.HumSourceHash;
            signal.Channel = AcousticPingSignal.ChannelMetalStress;
            signal.Flags = 0;
            SignalBus<AcousticPingSignal>.TryPush(in signal);
        }

        private void WriteDump(NativeArray<ChargerTelemetryEntry> telemetry, NativeArray<uint> cursor)
        {
            _dumpWrittenThisFault = true;
            string directory = Path.GetDirectoryName(_dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x534832333044554DuL); // SH230DUM
            writer.Write(1);
            writer.Write(telemetry.Length);
            writer.Write(cursor.Length > 0 ? cursor[0] : 0u);
            for (int i = 0; i < telemetry.Length; i++)
            {
                ChargerTelemetryEntry entry = telemetry[i];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.StateHash);
                writer.Write(entry.Flags);
                writer.Write(entry.ActiveLinks);
                writer.Write(entry.FullLinks);
                writer.Write(entry.UnpoweredLinks);
                writer.Write(entry.AtomicLockFailures);
                writer.Write(entry.BurstMicroseconds);
                writer.Write(entry.TotalEnergyDrawn);
                writer.Write(entry.GlobalQualityWeight);
                writer.Write(entry.CadenceHz);
                writer.Write(entry.DeltaSeconds);
                writer.Write(entry.AverageCharge01);
                writer.Write(entry.LinkCapacity);
                writer.Write(entry.LastFaultLink);
                writer.Write(entry.Reserved0);
            }
        }

#if UNITY_EDITOR
        private void MonitorProfileCsv()
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime lastWrite = File.GetLastWriteTimeUtc(_csvPath);
            if (lastWrite == _csvLastWriteUtc)
                return;

            byte[] bytes = File.ReadAllBytes(_csvPath);
            if (TryLoadProfilesFromCsvBytes(bytes))
                _csvLastWriteUtc = lastWrite;
        }
#endif

        private static ChargerTuningDTO DefaultTuning()
        {
            ChargerTuningDTO dto = default;
            dto.GlobalMaxChargeRate = s_pendingMaxChargeRate;
            dto.EfficiencyCurveExponent = s_pendingEfficiencyExponent;
            dto.GlobalQualityWeight = ResolveGlobalQualityWeight();
            dto.BatteryCapacity = BatteryChargerLogisticsConstants.DefaultBatteryCapacity01;
            dto.CadenceHz = ResolveCadenceHzStatic(dto.GlobalQualityWeight);
            dto.QualityOverride = s_pendingQualityOverride;
            dto.Flags = s_pendingQualityOverride >= 0f ? 1u : 0u;
            return dto;
        }

        private float ResolveQualityWeight()
        {
            if (Resolve(in _handles.Tuning, out NativeArray<ChargerTuningDTO> tuning) && tuning.Length > 0)
            {
                ChargerTuningDTO dto = tuning[0];
                if ((dto.Flags & 1u) != 0u && math.isfinite(dto.QualityOverride) && dto.QualityOverride >= 0f)
                    return math.saturate(dto.QualityOverride);
            }

            return ResolveGlobalQualityWeight();
        }

        private float ResolveCadenceHz(float quality)
        {
            return ResolveCadenceHzStatic(quality);
        }

        private static float ResolveCadenceHzStatic(float quality)
        {
            float q = math.smoothstep(0f, 1f, math.saturate(math.isfinite(quality) ? quality : 1f));
            return math.lerp(5f, 60f, q);
        }

        private static float ResolveGlobalQualityWeight()
        {
            return math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
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
            void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<ChargerVisualStateDTO>());
            destination.UnlockBufferAfterWrite<ChargerVisualStateDTO>(safeCount);
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
