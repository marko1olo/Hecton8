// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsRuntime.cs
// Vault-backed dispatcher runtime for base 3D CSR gas diffusion.
// ============================================================================

using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    public sealed unsafe class BaseAtmosphereLogisticsRuntime
    {
        private const uint SystemHash = 0x53483232u; // SH22
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
        private const string CsvRelativePath = "Docs/Atmosphere/gas_diffusion_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_221.bin";
        private const int CsvPollCadenceFrames = 128;
        private const uint ReactorSignalLaneHash = 0x52474153u; // RGAS

        private static readonly int _GasScalarsShaderId = Shader.PropertyToID("_H8BaseAtmosphereGasScalars");
        private static readonly int _GasQualityShaderId = Shader.PropertyToID("_H8BaseAtmosphereQualityWeight");

        private static BaseAtmosphereLogisticsRuntime s_active;
        private static float s_pendingBaseDiffusionRate = 0.35f;
        private static float s_pendingInhalationMultiplier = 1.0f;
        private static float s_pendingToxinDissipationSpeed = 0.005f;

        private readonly string _csvPath;
        private readonly string _dumpPath;
        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly SimulationPhaseSystem _simulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;

        private IDataVault _vault;
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
        private BufferID _lockedFrontBufferId;
        private BufferID _lockedBackBufferId;
        private uint _lastDispatcherFrame;
        private long _jobScheduleTimestamp;
        private float _lastAverageOxygen01 = AtmosphereLogisticsConstants.DefaultOxygen01;
        private float _lastMaxCarbonDioxide01 = AtmosphereLogisticsConstants.DefaultCarbonDioxide01;
        private float _lastMaxToxin01;
        private float _smoothedQualityWeight01 = 1f;
        private int _lastNodeCount;
        private int _lastIterations;
        private int _lastMicros;
        private DateTime _csvLastWriteUtc;

        private VaultGenerationHandle<AtmosphereCellDTO> _frontCells;
        private VaultGenerationHandle<AtmosphereCellDTO> _backCells;
        private VaultGenerationHandle<AtmosphereNodeDTO> _nodes;
        private VaultGenerationHandle<AtmosphereConnectionDTO> _connections;
        private VaultGenerationHandle<int> _edgeOffsets;
        private VaultGenerationHandle<int> _edgeDestinations;
        private VaultGenerationHandle<float> _edgeConductance;
        private VaultGenerationHandle<int> _edgeWriteCursor;
        private VaultGenerationHandle<AtmosphereConsumerDTO> _consumers;
        private VaultGenerationHandle<AtmosphereToxicSourceDTO> _sources;
        private VaultGenerationHandle<AtmosphereVentDTO> _vents;
        private VaultGenerationHandle<AtmosphereGraphCountersDTO> _counters;
        private VaultGenerationHandle<AtmosphereTuningDTO> _tuning;
        private VaultGenerationHandle<AtmosphereTelemetryEntry> _telemetry;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _oxygenDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _carbonDioxideDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _nitrogenDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _toxinDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _temperatureDeltaMilli;
        private VaultGenerationHandle<AtmosphereGasRemainderDTO> _remainders;
        private VaultGenerationHandle<AtmosphereShaderPayloadDTO> _shaderPayload;
        private VaultGenerationHandle<byte> _csvScratch;
        private VaultGenerationHandle<AtmosphereGasProfileDTO> _profiles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active = null;
            s_pendingBaseDiffusionRate = 0.35f;
            s_pendingInhalationMultiplier = 1.0f;
            s_pendingToxinDissipationSpeed = 0.005f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            BaseAtmosphereLogisticsRuntime runtime = new BaseAtmosphereLogisticsRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private static void ShutdownActive()
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

        private BaseAtmosphereLogisticsRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        public static bool TryGetGlobalOxygenSnapshot(out float reserve, out float capacity, out float normalized)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._lastNodeCount <= 0)
            {
                reserve = 0f;
                capacity = 0f;
                normalized = 1f;
                return false;
            }

            capacity = AtmosphereLogisticsConstants.DefaultOxygen01 * active._lastNodeCount;
            reserve = math.saturate(active._lastAverageOxygen01) * active._lastNodeCount;
            normalized = capacity > 0.0001f ? math.saturate(reserve / capacity) : 1f;
            return true;
        }

        public static void SetEditorTuning(float diffusionRate, float inhalationMultiplier, float toxinDissipationSpeed)
        {
            s_pendingBaseDiffusionRate = math.clamp(FiniteOr(diffusionRate, 0.35f), 0f, 4f);
            s_pendingInhalationMultiplier = math.clamp(FiniteOr(inhalationMultiplier, 1f), 0f, 4f);
            s_pendingToxinDissipationSpeed = math.clamp(FiniteOr(toxinDissipationSpeed, 0.005f), 0f, 1f);

            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled)
            {
                return;
            }

            IDataVault vault = active.ResolveVault();
            if (vault == null || !vault.TryLockBuffer(AtmosphereLogisticsBufferIds.Tuning, OwnerSystemId))
                return;

            if (!active.Resolve(in active._tuning, out NativeArray<AtmosphereTuningDTO> tuningBuffer) ||
                tuningBuffer.Length == 0)
            {
                vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Tuning, OwnerSystemId);
                return;
            }

            ref AtmosphereTuningDTO tuning = ref UnsafeUtility.AsRef<AtmosphereTuningDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(tuningBuffer));
            tuning.BaseDiffusionRate = s_pendingBaseDiffusionRate;
            tuning.InhalationMultiplier = s_pendingInhalationMultiplier;
            tuning.ToxinDissipationSpeed = s_pendingToxinDissipationSpeed;
            vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Tuning, OwnerSystemId);
        }

        public static bool TryGetEditorTuning(out AtmosphereTuningDTO tuning)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.Resolve(in active._tuning, out NativeArray<AtmosphereTuningDTO> tuningBuffer) ||
                tuningBuffer.Length == 0)
            {
                tuning = default;
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TryGetLatestTelemetry(out AtmosphereTelemetryEntry entry)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.Resolve(in active._telemetry, out NativeArray<AtmosphereTelemetryEntry> telemetry) ||
                !active.Resolve(in active._counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                telemetry.Length == 0 || counters.Length == 0)
            {
                entry = default;
                return false;
            }

            int cursor = counters[0].TelemetryCursor - 1;
            int index = cursor % telemetry.Length;
            if (index < 0)
                index += telemetry.Length;
            entry = telemetry[index];
            return entry.NodeCount > 0;
        }

#if UNITY_EDITOR
        public static bool TryGetTelemetryReadOnly(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            BaseAtmosphereLogisticsRuntime active = s_active;
            NativeArray<AtmosphereTelemetryEntry> telemetryBuffer;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.Resolve(in active._telemetry, out telemetryBuffer) ||
                !active.Resolve(in active._counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                telemetryBuffer.Length == 0 || counters.Length == 0)
            {
                return false;
            }

            telemetry = telemetryBuffer.AsReadOnly();
            cursor = counters[0].TelemetryCursor;
            return true;
        }
#endif

        public static bool TryGetGizmoCell(int index, out AtmosphereNodeDTO node, out AtmosphereCellDTO cell, out int nodeCount)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.Resolve(in active._nodes, out NativeArray<AtmosphereNodeDTO> nodes) ||
                !active.Resolve(in active._frontCells, out NativeArray<AtmosphereCellDTO> cells) ||
                !active.Resolve(in active._counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                counters.Length == 0)
            {
                node = default;
                cell = default;
                nodeCount = 0;
                return false;
            }

            nodeCount = math.clamp(counters[0].NodeCount, 0, math.min(nodes.Length, cells.Length));
            if ((uint)index >= (uint)nodeCount)
            {
                node = default;
                cell = default;
                return false;
            }

            node = nodes[index];
            cell = cells[index];
            return true;
        }

        private void Initialize()
        {
            _shutdown = false;
            _vault = GlobalRegistry.DataVault;
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            SignalBus<PlayerBaseEnterSignal>.EnsureInitialized();
            SignalBus<PlayerBaseExitSignal>.EnsureInitialized();
            SignalBus<ReactorDamageSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 8, laneHash: ReactorSignalLaneHash);
            SignalBus<ReactorDamageSignal>.EnsureInitialized();
            RegisterDispatcherPhases();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            UnlockJobBuffers();
            UnregisterDispatcherPhases();
            _vault = null;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _layoutChecked = false;
            _layoutValid = false;
            _simulationScheduled = false;
            _smoothedQualityWeight01 = 1f;
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

            ApplyQualityAndEditorTuning(vault, in timing);
            IngestPlayerBreathingSignals(vault);
            IngestExternalGasSignals(vault);
#if UNITY_EDITOR
            if ((_lastDispatcherFrame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorGasProfileCsv(vault);
#endif
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return dependsOn;

            _lastDispatcherFrame = context.Frame;
            if (!TryResolveSimulationBuffers(
                    out NativeArray<AtmosphereCellDTO> front,
                    out NativeArray<AtmosphereCellDTO> back,
                    out NativeArray<AtmosphereNodeDTO> nodes,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<float> edgeConductance,
                    out NativeArray<AtmosphereConsumerDTO> consumers,
                    out NativeArray<AtmosphereToxicSourceDTO> sources,
                    out NativeArray<AtmosphereVentDTO> vents,
                    out NativeArray<AtmosphereGraphCountersDTO> counters,
                    out NativeArray<AtmosphereTuningDTO> tuning,
                    out NativeArray<AtmosphereTelemetryEntry> telemetry,
                    out NativeArray<AtmosphereDeltaLane64> oxygenDelta,
                    out NativeArray<AtmosphereDeltaLane64> carbonDioxideDelta,
                    out NativeArray<AtmosphereDeltaLane64> nitrogenDelta,
                    out NativeArray<AtmosphereDeltaLane64> toxinDelta,
                    out NativeArray<AtmosphereDeltaLane64> temperatureDelta,
                    out NativeArray<AtmosphereGasRemainderDTO> remainders,
                    out NativeArray<AtmosphereShaderPayloadDTO> shaderPayload))
            {
                return dependsOn;
            }

            if (!TryLockJobBuffers(vault))
                return dependsOn;

            AtmosphereGraphCountersDTO counter = counters[0];
            int nodeCount = math.clamp(counter.NodeCount, 0, math.min(front.Length, back.Length));
            if (nodeCount <= 0)
            {
                UnlockJobBuffers();
                return dependsOn;
            }

            AtmosphereTuningDTO tune = tuning.Length > 0 ? tuning[0] : DefaultTuning();
            float quality = math.saturate(FiniteOr(tune.GlobalQualityWeight, ResolveGlobalQualityWeight()));
            int iterations = math.max(1, (int)math.lerp(1f, 8f, quality));
            float dt = ResolveSimulationTickDelta(in timing);

                JobHandle handle = new AtmosphereClearDeltaJob
                {
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta)
                }.Schedule(nodeCount, 64, dependsOn);

                handle = new AtmosphereConsumerBreathingJob
                {
                    Nodes = nodes,
                    Consumers = consumers,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                    NodeCount = nodeCount,
                    ConsumerCount = math.clamp(counter.ConsumerCount, 0, consumers.Length),
                    DeltaTime = dt,
                    InhalationMultiplier = tune.InhalationMultiplier
                }.Schedule(math.max(1, math.clamp(counter.ConsumerCount, 0, consumers.Length)), 32, handle);

                handle = new AtmosphereToxicSourceInjectionJob
                {
                    Nodes = nodes,
                    Sources = sources,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                    NodeCount = nodeCount,
                    SourceCount = math.clamp(counter.SourceCount, 0, sources.Length),
                    DeltaTime = dt
                }.Schedule(math.max(1, math.clamp(counter.SourceCount, 0, sources.Length)), 32, handle);

                handle = new AtmosphereVentLeakJob
                {
                    Nodes = nodes,
                    Vents = vents,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    NodeCount = nodeCount,
                    VentCount = math.clamp(counter.VentCount, 0, vents.Length),
                    DeltaTime = dt,
                    LeakDrainMultiplier = tune.LeakDrainMultiplier
                }.Schedule(math.max(1, math.clamp(counter.VentCount, 0, vents.Length)), 32, handle);

                VaultGenerationHandle<AtmosphereCellDTO> frontHandle = _frontCells;
                VaultGenerationHandle<AtmosphereCellDTO> backHandle = _backCells;
                NativeArray<AtmosphereCellDTO> currentFront = front;
                NativeArray<AtmosphereCellDTO> currentBack = back;

                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    handle = new AtmosphereDiffusionSolverJob
                    {
                        Front = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(currentFront),
                        Back = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(currentBack),
                        EdgeOffsets = edgeOffsets,
                        EdgeDestinations = edgeDestinations,
                        EdgeConductance = edgeConductance,
                        OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                        CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                        NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                        ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                        TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                        NodeCount = nodeCount,
                        EdgeCount = math.clamp(counter.CsrEdgeCount, 0, edgeDestinations.Length),
                        DeltaTime = dt,
                        BaseDiffusionRate = tune.BaseDiffusionRate,
                        ToxinDissipationSpeed = tune.ToxinDissipationSpeed
                    }.Schedule(nodeCount, 64, handle);

                    handle = new AtmosphereQuantizeGasJob
                    {
                        Cells = currentBack,
                        Remainders = remainders
                    }.Schedule(nodeCount, 64, handle);

                    handle = new AtmosphereConservationCorrectionJob
                    {
                        Front = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(currentFront),
                        Back = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(currentBack),
                        OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                        CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                        NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                        ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                        NodeCount = nodeCount
                    }.Schedule(handle);

                    Swap(ref currentFront, ref currentBack);
                    Swap(ref frontHandle, ref backHandle);

                    if (iteration == 0 && iterations > 1)
                    {
                        handle = new AtmosphereClearDeltaJob
                        {
                            OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                            CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                            NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                            ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                            TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta)
                        }.Schedule(nodeCount, 64, handle);
                    }
                }

                _frontCells = frontHandle;
                _backCells = backHandle;
                _lastIterations = iterations;
                handle = new AtmosphereTelemetryJob
                {
                    Cells = currentFront,
                    Telemetry = telemetry,
                    Counters = counters,
                    ShaderPayload = shaderPayload,
                    NodeCount = nodeCount,
                    SolverMicros = 0,
                    JacobiIterations = iterations,
                    FrameIndex = unchecked((int)context.Frame)
                }.Schedule(handle);

                _simulationScheduled = true;
                _jobScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
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

            _simulationScheduled = false;
            _lastMicros = ElapsedMicroseconds(_jobScheduleTimestamp);
            if (Resolve(in _telemetry, out NativeArray<AtmosphereTelemetryEntry> telemetry) &&
                Resolve(in _counters, out NativeArray<AtmosphereGraphCountersDTO> counters) &&
                telemetry.Length > 0 && counters.Length > 0)
            {
                int cursor = counters[0].TelemetryCursor - 1;
                int index = cursor % telemetry.Length;
                if (index < 0)
                    index += telemetry.Length;
                AtmosphereTelemetryEntry entry = telemetry[index];
                entry.SolverMicros = _lastMicros;
                telemetry[index] = entry;
                _lastAverageOxygen01 = entry.AverageOxygen01;
                _lastMaxCarbonDioxide01 = entry.MaxCarbonDioxide01;
                _lastMaxToxin01 = entry.MaxToxin01;
                _lastNodeCount = entry.NodeCount;
                if ((entry.FaultFlags & AtmosphereFaultFlags.NaNDetected) != 0u && !_dumpWrittenThisFault)
                    WriteDump(vault);
                if ((entry.FaultFlags & AtmosphereFaultFlags.NaNDetected) == 0u)
                    _dumpWrittenThisFault = false;
            }

            UnlockJobBuffers();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (Resolve(in _shaderPayload, out NativeArray<AtmosphereShaderPayloadDTO> payload) && payload.Length > 0)
            {
                AtmosphereShaderPayloadDTO scalar = payload[0];
                Shader.SetGlobalVector(_GasScalarsShaderId, new Vector4(scalar.Oxygen01, scalar.CarbonDioxide01, scalar.Toxin01, scalar.Flow01));
                Shader.SetGlobalFloat(_GasQualityShaderId, ResolveGlobalQualityWeight());
            }
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (!_vaultInitialized)
            {
                _frontCells = vault.GetGenerationHandle<AtmosphereCellDTO>(AtmosphereLogisticsBufferIds.CellsFront, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _backCells = vault.GetGenerationHandle<AtmosphereCellDTO>(AtmosphereLogisticsBufferIds.CellsBack, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _nodes = vault.GetGenerationHandle<AtmosphereNodeDTO>(AtmosphereLogisticsBufferIds.Nodes, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _connections = vault.GetGenerationHandle<AtmosphereConnectionDTO>(AtmosphereLogisticsBufferIds.Connections, AtmosphereLogisticsConstants.MaxMockConnections, OwnerSystemId);
                _edgeOffsets = vault.GetGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeOffsets, AtmosphereLogisticsConstants.MaxMockNodes + 1, OwnerSystemId);
                _edgeDestinations = vault.GetGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeDestinations, AtmosphereLogisticsConstants.MaxCsrEdges, OwnerSystemId);
                _edgeConductance = vault.GetGenerationHandle<float>(AtmosphereLogisticsBufferIds.EdgeConductance, AtmosphereLogisticsConstants.MaxCsrEdges, OwnerSystemId);
                _edgeWriteCursor = vault.GetGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeWriteCursor, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _consumers = vault.GetGenerationHandle<AtmosphereConsumerDTO>(AtmosphereLogisticsBufferIds.Consumers, AtmosphereLogisticsConstants.MaxConsumers, OwnerSystemId);
                _sources = vault.GetGenerationHandle<AtmosphereToxicSourceDTO>(AtmosphereLogisticsBufferIds.ToxicSources, AtmosphereLogisticsConstants.MaxToxicSources, OwnerSystemId);
                _vents = vault.GetGenerationHandle<AtmosphereVentDTO>(AtmosphereLogisticsBufferIds.Vents, AtmosphereLogisticsConstants.MaxVents, OwnerSystemId);
                _counters = vault.GetGenerationHandle<AtmosphereGraphCountersDTO>(AtmosphereLogisticsBufferIds.Counters, 1, OwnerSystemId);
                _tuning = vault.GetGenerationHandle<AtmosphereTuningDTO>(AtmosphereLogisticsBufferIds.Tuning, 1, OwnerSystemId);
                _telemetry = vault.GetGenerationHandle<AtmosphereTelemetryEntry>(AtmosphereLogisticsBufferIds.TelemetryRing, AtmosphereLogisticsConstants.TelemetryRingCapacity, OwnerSystemId);
                _oxygenDeltaUnits = vault.GetGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.OxygenDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _carbonDioxideDeltaUnits = vault.GetGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _nitrogenDeltaUnits = vault.GetGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _toxinDeltaUnits = vault.GetGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.ToxinDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _temperatureDeltaMilli = vault.GetGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _remainders = vault.GetGenerationHandle<AtmosphereGasRemainderDTO>(AtmosphereLogisticsBufferIds.GasRemainders, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _shaderPayload = vault.GetGenerationHandle<AtmosphereShaderPayloadDTO>(AtmosphereLogisticsBufferIds.ShaderPayload, 1, OwnerSystemId);
                _csvScratch = vault.GetGenerationHandle<byte>(AtmosphereLogisticsBufferIds.CsvScratch, AtmosphereLogisticsConstants.CsvScratchBytes, OwnerSystemId);
                _profiles = vault.GetGenerationHandle<AtmosphereGasProfileDTO>(AtmosphereLogisticsBufferIds.Profiles, AtmosphereLogisticsConstants.MaxProfiles, OwnerSystemId);
                _vaultInitialized = true;
            }

            if (!_layoutChecked)
            {
                _layoutValid = AtmosphereLogisticsLayout.ValidateAtmosphereCellLayout() &&
                               AtmosphereLogisticsLayout.ValidateAtmosphereDeltaLaneLayout();
                _layoutChecked = true;
            }

            if (!_defaultsInitialized || !_layoutValid)
                GenerateEmergencyMockTopology(vault);

            return _vaultInitialized;
        }

        private void GenerateEmergencyMockTopology(IDataVault vault)
        {
            if (!TryResolveInitializationBuffers(
                    out NativeArray<AtmosphereNodeDTO> nodes,
                    out NativeArray<AtmosphereConnectionDTO> connections,
                    out NativeArray<AtmosphereCellDTO> front,
                    out NativeArray<AtmosphereCellDTO> back,
                    out NativeArray<AtmosphereConsumerDTO> consumers,
                    out NativeArray<AtmosphereToxicSourceDTO> sources,
                    out NativeArray<AtmosphereVentDTO> vents,
                    out NativeArray<AtmosphereGraphCountersDTO> counters,
                    out NativeArray<AtmosphereTuningDTO> tuning,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<float> edgeConductance,
                    out NativeArray<int> edgeWriteCursor))
            {
                return;
            }

            tuning[0] = DefaultTuning();
            AtmosphereMockTopologyJob topologyJob = new AtmosphereMockTopologyJob
            {
                Nodes = nodes,
                Connections = connections,
                FrontCells = front,
                BackCells = back,
                Consumers = consumers,
                Sources = sources,
                Vents = vents,
                Counters = counters,
                Tuning = tuning,
                GridOriginAup = double3.zero
            };
            topologyJob.Execute();

            AtmosphereCsrBuildJob csrBuildJob = new AtmosphereCsrBuildJob
            {
                Connections = connections,
                EdgeOffsets = edgeOffsets,
                EdgeDestinations = edgeDestinations,
                EdgeConductance = edgeConductance,
                EdgeWriteCursor = edgeWriteCursor,
                Counters = counters
            };
            csrBuildJob.Execute();

            _lastNodeCount = counters[0].NodeCount;
            _defaultsInitialized = true;
        }

        private void ApplyQualityAndEditorTuning(IDataVault vault, in DispatcherTimingDTO timing)
        {
            if (!Resolve(in _tuning, out NativeArray<AtmosphereTuningDTO> tuning) || tuning.Length == 0)
                return;

            AtmosphereTuningDTO tune = tuning[0];
            float targetQuality = ResolveGlobalQualityWeight();
            _smoothedQualityWeight01 = SmoothQualityWeight(_smoothedQualityWeight01, targetQuality, ResolveSimulationTickDelta(in timing));
            tune.BaseDiffusionRate = math.clamp(FiniteOr(s_pendingBaseDiffusionRate, 0.35f), 0f, 4f);
            tune.InhalationMultiplier = math.clamp(FiniteOr(s_pendingInhalationMultiplier, 1f), 0f, 4f);
            tune.ToxinDissipationSpeed = math.clamp(FiniteOr(s_pendingToxinDissipationSpeed, 0.005f), 0f, 1f);
            tune.GlobalQualityWeight = _smoothedQualityWeight01;
            tune.CellSizeMeters = math.clamp(FiniteOr(tune.CellSizeMeters, 2f), AtmosphereLogisticsConstants.MinimumCellSizeMeters, AtmosphereLogisticsConstants.MaximumCellSizeMeters);
            tune.AmbientTemperatureCelsius = FiniteOr(tune.AmbientTemperatureCelsius, AtmosphereLogisticsConstants.DefaultTemperatureCelsius);
            tune.LeakDrainMultiplier = math.clamp(FiniteOr(tune.LeakDrainMultiplier, 1f), 0f, 8f);
            tuning[0] = tune;
        }

        private void IngestExternalGasSignals(IDataVault vault)
        {
            if (!Resolve(in _sources, out NativeArray<AtmosphereToxicSourceDTO> sources) ||
                !Resolve(in _counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                sources.Length == 0 || counters.Length == 0)
            {
                return;
            }

            AtmosphereGraphCountersDTO counter = counters[0];
            int write = math.min(counter.SourceCount > 0 ? 1 : 0, sources.Length);
            ReadOnlySpan<FluidIncursionSignal> incursions = SignalBus<FluidIncursionSignal>.GetFrameSnapshot();
            for (int i = 0; i < incursions.Length && write < sources.Length; i++)
            {
                FluidIncursionSignal signal = incursions[i];
                double3 aup = signal.LeakAup.ToAbsoluteDouble3();
                float flow = math.saturate(signal.FlowRate01);
                sources[write++] = new AtmosphereToxicSourceDTO
                {
                    Aup = aup,
                    ToxinPerSecond01 = 0.000016f * flow,
                    CarbonDioxidePerSecond01 = 0.000012f * flow,
                    OxygenDrainPerSecond01 = 0.00002f * flow,
                    HeatPerSecond = -0.01f * flow,
                    SourceHash = signal.CompartmentId != 0u ? signal.CompartmentId : 0x464C5544u,
                    Flags = AtmosphereCellFlags.Breached,
                    LastNodeIndex = -1,
                    RadiusMeters = 4f
                };
            }

            ReadOnlySpan<ReactorDamageSignal> reactorSignals = SignalBus<ReactorDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < reactorSignals.Length && write < sources.Length; i++)
            {
                ReactorDamageSignal signal = reactorSignals[i];
                double3 aup = signal.DamageAup;
                float severity = math.max(math.saturate(signal.Damage01), math.saturate(signal.ToxinLeak01));
                sources[write++] = new AtmosphereToxicSourceDTO
                {
                    Aup = aup,
                    ToxinPerSecond01 = 0.00006f * severity,
                    CarbonDioxidePerSecond01 = 0.000018f * severity,
                    OxygenDrainPerSecond01 = 0.000012f * severity,
                    HeatPerSecond = 0.04f * severity,
                    SourceHash = signal.ReactorHash != 0u ? signal.ReactorHash : 0x52454143u,
                    Flags = AtmosphereCellFlags.ReactorLeak,
                    LastNodeIndex = -1,
                    RadiusMeters = 5f
                };
            }

            for (int i = write; i < sources.Length; i++)
                sources[i] = default;

            if (incursions.Length + reactorSignals.Length > sources.Length)
                counter.Flags |= AtmosphereFaultFlags.SourceOverflow;

            counter.SourceCount = write;
            counters[0] = counter;
        }

        private void IngestPlayerBreathingSignals(IDataVault vault)
        {
            if (!Resolve(in _consumers, out NativeArray<AtmosphereConsumerDTO> consumers) ||
                !Resolve(in _counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                consumers.Length == 0 || counters.Length == 0)
            {
                return;
            }

            AtmosphereGraphCountersDTO counter = counters[0];
            ReadOnlySpan<PlayerBaseEnterSignal> enterSignals = SignalBus<PlayerBaseEnterSignal>.GetFrameSnapshot();
            for (int i = 0; i < enterSignals.Length; i++)
            {
                PlayerBaseEnterSignal signal = enterSignals[i];
                consumers[0] = new AtmosphereConsumerDTO
                {
                    Aup = signal.BaseCenterAup.ToAbsoluteDouble3(),
                    OxygenPerSecond01 = 0.000015f,
                    CarbonDioxidePerSecond01 = 0.000014f,
                    RadiusMeters = 4f,
                    HeatPerSecond = 0.0015f,
                    EntityHash = unchecked((uint)signal.BaseId),
                    Flags = 1u,
                    LastNodeHash = 0u,
                    LastNodeIndex = math.max(0, signal.RoomId)
                };
                counter.ConsumerCount = math.max(counter.ConsumerCount, 1);
            }

            ReadOnlySpan<PlayerBaseExitSignal> exitSignals = SignalBus<PlayerBaseExitSignal>.GetFrameSnapshot();
            for (int i = 0; i < exitSignals.Length; i++)
            {
                AtmosphereConsumerDTO consumer = consumers[0];
                consumer.Flags = 0u;
                consumers[0] = consumer;
                counter.ConsumerCount = math.max(counter.ConsumerCount, 1);
            }

            counters[0] = counter;
        }

#if UNITY_EDITOR
        private void MonitorGasProfileCsv(IDataVault vault)
        {
            if (!File.Exists(_csvPath) ||
                !Resolve(in _csvScratch, out NativeArray<byte> scratch) ||
                !Resolve(in _profiles, out NativeArray<AtmosphereGasProfileDTO> profiles))
            {
                return;
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvPath);
            if (writeUtc == _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            int bytesRead = ReadCsvFileNoStringAlloc(_csvPath, scratch);
            if (bytesRead <= 0)
                return;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>((byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch), math.min(bytesRead, scratch.Length));
            if (TryParseGasProfilesCsv(span, profiles, out int parsed) &&
                Resolve(in _tuning, out NativeArray<AtmosphereTuningDTO> tuning) &&
                tuning.Length > 0 && parsed > 0)
            {
                AtmosphereGasProfileDTO profile = profiles[0];
                AtmosphereTuningDTO tune = tuning[0];
                tune.AmbientTemperatureCelsius = profile.Temperature;
                tuning[0] = tune;
            }
        }
#endif

        public static bool TryParseGasProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<AtmosphereGasProfileDTO> profiles, out int count)
        {
            count = 0;
            if (!profiles.IsCreated || profiles.Length == 0)
                return false;

            int cursor = 0;
            bool anyMalformed = false;
            while (cursor < bytes.Length && count < profiles.Length)
            {
                SkipLineTerminators(bytes, ref cursor);
                if (cursor >= bytes.Length)
                    break;

                int lineStart = cursor;
                if (IsGasProfileHeaderLine(bytes, lineStart))
                {
                    SkipLine(bytes, ref cursor);
                    continue;
                }

                if (!TryReadProfileHash(bytes, ref cursor, out uint hash))
                {
                    SkipLine(bytes, ref cursor);
                    anyMalformed = true;
                    continue;
                }

                bool rowMalformed = false;
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float oxygen = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultOxygen01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float carbon = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultCarbonDioxide01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float nitrogen = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultNitrogen01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float toxin = ReadFloatOr(bytes, ref cursor, 0f, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float temperature = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultTemperatureCelsius, ref rowMalformed);
                SkipLine(bytes, ref cursor);
                anyMalformed |= rowMalformed;

                profiles[count++] = new AtmosphereGasProfileDTO
                {
                    ProfileHash = hash,
                    Oxygen01 = math.saturate(oxygen),
                    CarbonDioxide01 = math.saturate(carbon),
                    Nitrogen01 = math.saturate(nitrogen),
                    Toxin01 = math.saturate(toxin),
                    Temperature = math.clamp(FiniteOr(temperature, AtmosphereLogisticsConstants.DefaultTemperatureCelsius), -80f, 250f),
                    Flags = rowMalformed ? AtmosphereFaultFlags.CsvMalformed : 0u
                };
            }

            return count > 0 && !anyMalformed;
        }

        private bool TryResolveInitializationBuffers(
            out NativeArray<AtmosphereNodeDTO> nodes,
            out NativeArray<AtmosphereConnectionDTO> connections,
            out NativeArray<AtmosphereCellDTO> front,
            out NativeArray<AtmosphereCellDTO> back,
            out NativeArray<AtmosphereConsumerDTO> consumers,
            out NativeArray<AtmosphereToxicSourceDTO> sources,
            out NativeArray<AtmosphereVentDTO> vents,
            out NativeArray<AtmosphereGraphCountersDTO> counters,
            out NativeArray<AtmosphereTuningDTO> tuning,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<float> edgeConductance,
            out NativeArray<int> edgeWriteCursor)
        {
            return Resolve(in _nodes, out nodes) &&
                   Resolve(in _connections, out connections) &&
                   Resolve(in _frontCells, out front) &&
                   Resolve(in _backCells, out back) &&
                   Resolve(in _consumers, out consumers) &&
                   Resolve(in _sources, out sources) &&
                   Resolve(in _vents, out vents) &&
                   Resolve(in _counters, out counters) &&
                   Resolve(in _tuning, out tuning) &&
                   Resolve(in _edgeOffsets, out edgeOffsets) &&
                   Resolve(in _edgeDestinations, out edgeDestinations) &&
                   Resolve(in _edgeConductance, out edgeConductance) &&
                   Resolve(in _edgeWriteCursor, out edgeWriteCursor);
        }

        private bool TryResolveSimulationBuffers(
            out NativeArray<AtmosphereCellDTO> front,
            out NativeArray<AtmosphereCellDTO> back,
            out NativeArray<AtmosphereNodeDTO> nodes,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<float> edgeConductance,
            out NativeArray<AtmosphereConsumerDTO> consumers,
            out NativeArray<AtmosphereToxicSourceDTO> sources,
            out NativeArray<AtmosphereVentDTO> vents,
            out NativeArray<AtmosphereGraphCountersDTO> counters,
            out NativeArray<AtmosphereTuningDTO> tuning,
            out NativeArray<AtmosphereTelemetryEntry> telemetry,
            out NativeArray<AtmosphereDeltaLane64> oxygenDelta,
            out NativeArray<AtmosphereDeltaLane64> carbonDioxideDelta,
            out NativeArray<AtmosphereDeltaLane64> nitrogenDelta,
            out NativeArray<AtmosphereDeltaLane64> toxinDelta,
            out NativeArray<AtmosphereDeltaLane64> temperatureDelta,
            out NativeArray<AtmosphereGasRemainderDTO> remainders,
            out NativeArray<AtmosphereShaderPayloadDTO> shaderPayload)
        {
            return Resolve(in _frontCells, out front) &&
                   Resolve(in _backCells, out back) &&
                   Resolve(in _nodes, out nodes) &&
                   Resolve(in _edgeOffsets, out edgeOffsets) &&
                   Resolve(in _edgeDestinations, out edgeDestinations) &&
                   Resolve(in _edgeConductance, out edgeConductance) &&
                   Resolve(in _consumers, out consumers) &&
                   Resolve(in _sources, out sources) &&
                   Resolve(in _vents, out vents) &&
                   Resolve(in _counters, out counters) &&
                   Resolve(in _tuning, out tuning) &&
                   Resolve(in _telemetry, out telemetry) &&
                   Resolve(in _oxygenDeltaUnits, out oxygenDelta) &&
                   Resolve(in _carbonDioxideDeltaUnits, out carbonDioxideDelta) &&
                   Resolve(in _nitrogenDeltaUnits, out nitrogenDelta) &&
                   Resolve(in _toxinDeltaUnits, out toxinDelta) &&
                   Resolve(in _temperatureDeltaMilli, out temperatureDelta) &&
                   Resolve(in _remainders, out remainders) &&
                   Resolve(in _shaderPayload, out shaderPayload);
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
            _lockedFrontBufferId = ActiveFrontBufferId();
            _lockedBackBufferId = ActiveBackBufferId();
            if (!TryLock(vault, _lockedFrontBufferId, 1 << 0)) return false;
            if (!TryLock(vault, _lockedBackBufferId, 1 << 1)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.EdgeOffsets, 1 << 2)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.EdgeDestinations, 1 << 3)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.EdgeConductance, 1 << 4)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.Counters, 1 << 5)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.TelemetryRing, 1 << 6)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.OxygenDeltaUnits, 1 << 7)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, 1 << 8)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, 1 << 9)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.ToxinDeltaUnits, 1 << 10)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, 1 << 11)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.GasRemainders, 1 << 12)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.ShaderPayload, 1 << 13)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.Nodes, 1 << 14)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.Consumers, 1 << 15)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.ToxicSources, 1 << 16)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.Vents, 1 << 17)) return false;
            if (!TryLock(vault, AtmosphereLogisticsBufferIds.Tuning, 1 << 18)) return false;
            return true;
        }

        private bool TryLock(IDataVault vault, BufferID bufferId, int bit)
        {
            if (!vault.TryLockBuffer(bufferId, OwnerSystemId))
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

            if ((_lockedBufferMask & (1 << 18)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Tuning, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 17)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Vents, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 16)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.ToxicSources, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 15)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Consumers, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 14)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Nodes, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 13)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.ShaderPayload, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 12)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.GasRemainders, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 11)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 10)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.ToxinDeltaUnits, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 9)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 8)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 7)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.OxygenDeltaUnits, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 6)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.TelemetryRing, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 5)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.Counters, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 4)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.EdgeConductance, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 3)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.EdgeDestinations, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 2)) != 0) vault.TryUnlockBuffer(AtmosphereLogisticsBufferIds.EdgeOffsets, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 1)) != 0) vault.TryUnlockBuffer(_lockedBackBufferId, OwnerSystemId);
            if ((_lockedBufferMask & 1) != 0) vault.TryUnlockBuffer(_lockedFrontBufferId, OwnerSystemId);
            _lockedBufferMask = 0;
            _lockedFrontBufferId = default;
            _lockedBackBufferId = default;
        }

        private BufferID ActiveFrontBufferId()
        {
            return (BufferID)_frontCells.BufferID;
        }

        private BufferID ActiveBackBufferId()
        {
            return (BufferID)_backCells.BufferID;
        }

        private void WriteDump(IDataVault vault)
        {
            if (!Resolve(in _telemetry, out NativeArray<AtmosphereTelemetryEntry> telemetry) || telemetry.Length == 0)
                return;

            _dumpWrittenThisFault = true;
            string directory = Path.GetDirectoryName(_dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x4847415332323144UL); // HGAS221D
            writer.Write(1);
            writer.Write(telemetry.Length);
            for (int i = 0; i < telemetry.Length; i++)
            {
                AtmosphereTelemetryEntry entry = telemetry[i];
                writer.Write(entry.StateHash);
                writer.Write(entry.AverageOxygen01);
                writer.Write(entry.MaxCarbonDioxide01);
                writer.Write(entry.AverageNitrogen01);
                writer.Write(entry.MaxToxin01);
                writer.Write(entry.AverageTemperature);
                writer.Write(entry.FrameIndex);
                writer.Write(entry.NodeCount);
                writer.Write(entry.EdgeCount);
                writer.Write(entry.ConsumerCount);
                writer.Write(entry.SourceCount);
                writer.Write(entry.SolverMicros);
                writer.Write(entry.JacobiIterations);
                writer.Write(entry.FaultFlags);
                writer.Write(entry.TotalGasUnits);
            }
        }

        private static AtmosphereTuningDTO DefaultTuning()
        {
            return new AtmosphereTuningDTO
            {
                BaseDiffusionRate = s_pendingBaseDiffusionRate,
                InhalationMultiplier = s_pendingInhalationMultiplier,
                ToxinDissipationSpeed = s_pendingToxinDissipationSpeed,
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                CellSizeMeters = 2f,
                AmbientTemperatureCelsius = AtmosphereLogisticsConstants.DefaultTemperatureCelsius,
                LeakDrainMultiplier = 1f,
                Flags = 0u
            };
        }

        private static int ReadCsvFileNoStringAlloc(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length == 0)
                return 0;

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileLength = stream.Length;
            int readLength = fileLength <= 0L ? 0 : fileLength > scratch.Length ? scratch.Length : (int)fileLength;
            return stream.Read(new Span<byte>((byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch), readLength));
        }

        private static bool IsGasProfileHeaderLine(ReadOnlySpan<byte> bytes, int lineStart)
        {
            if ((uint)lineStart >= (uint)bytes.Length)
                return false;

            int start = lineStart;
            SkipSpaces(bytes, ref start);
            int end = start;
            while (end < bytes.Length && !IsTokenDelimiter(bytes[end]))
                end++;

            TrimToken(bytes, ref start, ref end);
            return TokenEqualsAscii(bytes, start, end, "profile") ||
                   TokenEqualsAscii(bytes, start, end, "profile_hash") ||
                   TokenEqualsAscii(bytes, start, end, "module") ||
                   TokenEqualsAscii(bytes, start, end, "module_type") ||
                   TokenEqualsAscii(bytes, start, end, "name");
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            SkipLineTerminators(bytes, ref cursor);
        }

        private static void SkipLineTerminators(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
        }

        private static bool ConsumeComma(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            SkipSpaces(bytes, ref cursor);
            if (cursor < bytes.Length && bytes[cursor] == (byte)',')
            {
                cursor++;
                return true;
            }

            return false;
        }

        private static bool TryReadProfileHash(ReadOnlySpan<byte> bytes, ref int cursor, out uint value)
        {
            SkipSpaces(bytes, ref cursor);
            int start = cursor;
            while (cursor < bytes.Length && !IsTokenDelimiter(bytes[cursor]))
                cursor++;

            int end = cursor;
            TrimToken(bytes, ref start, ref end);
            value = 0u;
            if (start >= end)
                return false;

            bool numeric = true;
            uint numericValue = 0u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                {
                    numeric = false;
                    break;
                }

                numericValue = numericValue * 10u + (uint)(c - (byte)'0');
            }

            if (numeric)
            {
                value = numericValue;
                return true;
            }

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = ToLowerAscii(bytes[i]);
                hash = (hash ^ c) * 16777619u;
            }

            value = hash == 0u ? 1u : hash;
            return true;
        }

        private static float ReadFloatOr(ReadOnlySpan<byte> bytes, ref int cursor, float fallback, ref bool malformed)
        {
            SkipSpaces(bytes, ref cursor);
            int sign = 1;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }

            double value = 0d;
            bool any = false;
            while (cursor < bytes.Length)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                any = true;
                value = value * 10d + (c - (byte)'0');
                cursor++;
            }

            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                double factor = 0.1d;
                while (cursor < bytes.Length)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    any = true;
                    value += (c - (byte)'0') * factor;
                    factor *= 0.1d;
                    cursor++;
                }
            }

            if (!any)
            {
                malformed = true;
                return fallback;
            }

            return (float)(value * sign);
        }

        private static void SkipSpaces(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;
        }

        private static void TrimToken(ReadOnlySpan<byte> bytes, ref int start, ref int end)
        {
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;
        }

        private static bool IsTokenDelimiter(byte value)
        {
            return value == (byte)',' || value == (byte)'\n' || value == (byte)'\r';
        }

        private static bool TokenEqualsAscii(ReadOnlySpan<byte> bytes, int start, int end, string ascii)
        {
            int length = end - start;
            if (length != ascii.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (ToLowerAscii(bytes[start + i]) != (byte)ascii[i])
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = FiniteOr(timing.FixedDelta, 0f);
            return fixedDelta > 0.00001f ? math.clamp(fixedDelta, 1f / 240f, 1f / 5f) : 1f / 60f;
        }

        private static float SmoothQualityWeight(float current, float target, float deltaTime)
        {
            float safeCurrent = math.saturate(FiniteOr(current, target));
            float safeTarget = math.saturate(FiniteOr(target, safeCurrent));
            float rate = safeTarget < safeCurrent ? 2.0f : 0.5f;
            float alpha = math.saturate(math.max(0f, deltaTime) * rate);
            float eased = math.smoothstep(0f, 1f, alpha);
            return math.lerp(safeCurrent, safeTarget, eased);
        }

        private static float ResolveGlobalQualityWeight()
        {
            return math.saturate(FiniteOr(HomeostasisBrain.GlobalQualityWeight, 1f));
        }

        private static int ElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double micros = (double)delta * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            return math.clamp((int)micros, 0, int.MaxValue);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public PreSimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323250u; }
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
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public SimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323253u; }
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
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public PostSimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x5332324Fu; }
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
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public VisualSyncPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323256u; }
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
