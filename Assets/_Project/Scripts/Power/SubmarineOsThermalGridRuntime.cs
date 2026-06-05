using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Power
{
    public static class SubmarineThermalGridStatusFlags
    {
        public const uint None = 0u;
        public const uint Source = 1u << 0;
        public const uint Brownout = 1u << 1;
        public const uint Overheating = 1u << 2;
        public const uint MicroDamage = 1u << 3;
        public const uint Isolated = 1u << 4;
        public const uint ExternalHeat = 1u << 5;
        public const uint ShortCircuit = 1u << 6;
        public const uint FaultDivergent = 1u << 7;
    }

    public static class SolverConvergenceFaultFlags
    {
        public const ushort None = 0;
        public const ushort Converged = 1 << 0;
        public const ushort Divergent = 1 << 1;
        public const ushort NonFinite = 1 << 2;
        public const ushort MaxIterations = 1 << 3;
    }

    public static class PowerSolverConvergenceMath
    {
        public const float AuthoritativeQualityWeight = 1f;
        public const int MinPropagationIterations = 2;
        public const int MaxPropagationIterations = 50;
        private const float Epsilon = 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolvePropagationIterations(float globalQualityWeight)
        {
            float q = MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight);
            float curve = MathLodApproximation.SmoothStep01(q);
            return math.clamp((int)math.round(math.lerp(MinPropagationIterations, MaxPropagationIterations, curve)), MinPropagationIterations, MaxPropagationIterations);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSolverTargetTolerance(float baseTolerance, float globalQualityWeight)
        {
            float safeBase = math.max(Epsilon, MathLodApproximation.FiniteOr(baseTolerance, 0.001f));
            float q = MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight);
            float curve = MathLodApproximation.SmoothStep01(q);
            float survivalTolerance = math.min(0.05f, safeBase * 32f);
            float overkillTolerance = math.max(Epsilon * 0.25f, safeBase * 0.5f);
            return math.lerp(survivalTolerance, overkillTolerance, curve);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSolverOmega(float globalQualityWeight)
        {
            float q = MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight);
            float curve = MathLodApproximation.SmoothStep01(q);
            return math.lerp(0.55f, 0.92f, curve);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveResidualSampleMask(float globalQualityWeight)
        {
            float q = MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight);
            float curve = MathLodApproximation.SmoothStep01(q);
            return math.clamp((int)math.round(math.lerp(7f, 0f, curve)), 0, 7);
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GridNodeDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Potential;
        [FieldOffset(8)] public float Resistance;
        [FieldOffset(12)] public float ThermalLoad;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int AdjacencyOffset;
        [FieldOffset(24)] public int AdjacencyCount;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct PowerEdgeDTO
    {
        [FieldOffset(0)] public int TargetIndex;
        [FieldOffset(4)] public float Conductance;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ThermalGridAnchorDTO
    {
        [FieldOffset(0)] public float3 LocalOffset;
        [FieldOffset(12)] public uint NodeHash;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SubmarineGridSpecDTO
    {
        [FieldOffset(0)] public uint ComponentHash;
        [FieldOffset(4)] public float BaseConductance;
        [FieldOffset(8)] public float ThermalLimit;
        [FieldOffset(12)] public float BaseResistance;
        [FieldOffset(16)] public float ExternalHeatScale;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineThermalGridTuningDTO
    {
        [FieldOffset(0)] public float BaseResistance;
        [FieldOffset(4)] public float ThermalDissipationRate;
        [FieldOffset(8)] public float JacobiTolerance;
        [FieldOffset(12)] public float DamageThreshold;
        [FieldOffset(16)] public float CriticalThermalThreshold;
        [FieldOffset(20)] public float HeatGainScale;
        [FieldOffset(24)] public float ResistanceDriftRate;
        [FieldOffset(28)] public float ExternalHeatScale;
        [FieldOffset(32)] public float BrownoutVoltageThreshold;
        [FieldOffset(36)] public float FlickerScale;
        [FieldOffset(40)] public float VisualOverkillScalar;
        [FieldOffset(44)] public float SimulationTickDeltaSeconds;
        [FieldOffset(48)] public uint CsvRevision;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float BaseOmegaFactor;
        [FieldOffset(60)] public float ToleranceMultiplier;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermalGridVisualStateDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Voltage01;
        [FieldOffset(8)] public float Thermal01;
        [FieldOffset(12)] public float FlickerPhase01;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float VisualOverkill01;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermalPowerGridTelemetrySnapshot
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float TotalGeneratedPower;
        [FieldOffset(20)] public float TotalLoad;
        [FieldOffset(24)] public float MaximumThermalStress;
        [FieldOffset(28)] public float JacobiResidual;
        [FieldOffset(32)] public int IterationCount;
        [FieldOffset(36)] public int NodeCount;
        [FieldOffset(40)] public int EdgeCount;
        [FieldOffset(44)] public int MicroDamageCount;
        [FieldOffset(48)] public int BrownoutCount;
        [FieldOffset(52)] public int ExternalHeatNodeCount;
        [FieldOffset(56)] public float SolverOmega;
        [FieldOffset(60)] public float TargetTolerance;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SolverConvergenceStateDTO
    {
        [FieldOffset(0)] public float MaxResidualFloat;
        [FieldOffset(4)] public float PreviousResidualFloat;
        [FieldOffset(8)] public float Omega;
        [FieldOffset(12)] public ushort IterationCount;
        [FieldOffset(14)] public ushort FaultFlags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SolverResidualSlot64
    {
        [FieldOffset(0)] public float MaxResidualFloat;
        [FieldOffset(4)] public uint FaultFlags;
        [FieldOffset(8)] private ulong _pad0;
        [FieldOffset(16)] private ulong _pad1;
        [FieldOffset(24)] private ulong _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    public interface IContinuousPowerComponent
    {
        float Voltage01 { get; }
        void OnVoltageChanged(float voltage01);
    }

    public sealed unsafe class SubmarineOsThermalGridRuntime : IDisposable
    {
        public const int MaxNodes = 512;
        public const int MaxEdges = MaxNodes * 6;
        public const int EmergencyMockNodeCount = 100;
        public const int EmergencyMockEdgeCount = (EmergencyMockNodeCount - 1) * 2;
        public const int TelemetryFrameCount = 300;
        public const int CsvSpecCapacity = 256;
        public const int CsvByteCapacity = 16 * 1024;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_THERMAL_GRID.bin";
        public const string ShinobuDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_203.bin";

        public const int GridNodeSizeBytes = 32;
        public const int PowerEdgeSizeBytes = 8;
        public const int TelemetrySizeBytes = 64;
        public const int SolverConvergenceStateSizeBytes = 16;
        public const int SolverResidualSlotSizeBytes = 64;
        public const int ResidualThreadSlotCount = 128;
#if UNITY_EDITOR
        private const int StandaloneVaultBufferCapacity = 32;
        private const long StandaloneVaultArenaBytes = 2L * 1024L * 1024L;
#endif

        private const uint SourceHash = 0x53313036u; // S106
        private const uint DumpMagic = 0x54484752u; // THGR
        private const uint DumpVersion = 1u;
        private const int DumpHeaderBytes = 28;
        private const int DumpTelemetryEntryBytes = 64;
        private const uint ResidualSlotFaultNonFinite = 1u;
        private const float Epsilon = 0.0001f;

        private const int CounterNodeCount = 0;
        private const int CounterEdgeCount = 1;
        private const int CounterTelemetryCursor = 2;
        private const int CounterFaultFlags = 3;
        private const int CounterCsvSpecCount = 4;
        private const int CounterMaxIterationStreak = 5;
        private const int CounterDumpedFaultMask = 6;
        private const int CounterCount = 8;

        private static readonly int s_ThermalGridNodeCountId = Shader.PropertyToID("_H8ThermalGridNodeCount");
        private static readonly int s_ThermalGridBrownoutId = Shader.PropertyToID("_H8ThermalGridBrownout01");
        private static readonly int s_ThermalGridMaxHeatId = Shader.PropertyToID("_H8ThermalGridMaxHeat01");
        private static readonly int s_ThermalGridFlickerId = Shader.PropertyToID("_H8ThermalGridFlicker01");
        private static readonly int s_ThermalGridVisualOverkillId = Shader.PropertyToID("_H8ThermalGridVisualOverkill01");
        private static SubmarineOsThermalGridRuntime s_active;
#if UNITY_EDITOR
        private static GlobalDataVault s_standaloneVault;
#endif

        private static readonly BufferID NodesAId = (BufferID)731060;
        private static readonly BufferID NodesBId = (BufferID)731061;
        private static readonly BufferID EdgesId = (BufferID)731062;
        private static readonly BufferID InjectionsId = (BufferID)731063;
        private static readonly BufferID ExternalHeatId = (BufferID)731064;
        private static readonly BufferID AnchorsId = (BufferID)731065;
        private static readonly BufferID TuningId = (BufferID)731066;
        private static readonly BufferID TelemetryId = (BufferID)731067;
        private static readonly BufferID CountersId = (BufferID)731068;
        private static readonly BufferID SpecsId = (BufferID)731069;
        private static readonly BufferID CsvBytesId = (BufferID)731070;
        private static readonly BufferID VisualStateId = (BufferID)731071;
        private static readonly BufferID PendingNodesId = (BufferID)731072;
        private static readonly BufferID PendingEdgesId = (BufferID)731073;
        private static readonly BufferID PendingInjectionsId = (BufferID)731074;
        private static readonly BufferID PendingAnchorsId = (BufferID)731075;
        private static readonly BufferID PendingVisualStateId = (BufferID)731076;
        private static readonly BufferID PendingCountersId = (BufferID)731077;
        private static readonly BufferID ConvergenceStateId = (BufferID)731078;
        private static readonly BufferID ResidualSamplesId = (BufferID)731079;
        private static readonly ulong TopologyRebuildMutationGuardMask =
            ThermalGridBufferGuardBit(PendingNodesId) |
            ThermalGridBufferGuardBit(PendingEdgesId) |
            ThermalGridBufferGuardBit(PendingInjectionsId) |
            ThermalGridBufferGuardBit(PendingAnchorsId) |
            ThermalGridBufferGuardBit(PendingVisualStateId) |
            ThermalGridBufferGuardBit(PendingCountersId);
        private static readonly ulong TopologyCommitMutationGuardMask =
            ThermalGridBufferGuardBit(NodesAId) |
            ThermalGridBufferGuardBit(NodesBId) |
            ThermalGridBufferGuardBit(EdgesId) |
            ThermalGridBufferGuardBit(InjectionsId) |
            ThermalGridBufferGuardBit(AnchorsId) |
            ThermalGridBufferGuardBit(VisualStateId) |
            ThermalGridBufferGuardBit(CountersId);
        private static readonly ulong SolveMutationGuardMask =
            ThermalGridBufferGuardBit(NodesAId) |
            ThermalGridBufferGuardBit(NodesBId) |
            ThermalGridBufferGuardBit(EdgesId) |
            ThermalGridBufferGuardBit(InjectionsId) |
            ThermalGridBufferGuardBit(ExternalHeatId) |
            ThermalGridBufferGuardBit(VisualStateId) |
            ThermalGridBufferGuardBit(ConvergenceStateId) |
            ThermalGridBufferGuardBit(ResidualSamplesId) |
            ThermalGridBufferGuardBit(TelemetryId) |
            ThermalGridBufferGuardBit(CountersId);
        private static readonly ulong ExternalHeatMutationGuardMask =
            ThermalGridBufferGuardBit(ExternalHeatId) |
            ThermalGridBufferGuardBit(AnchorsId);
        private static readonly ulong CsvImportMutationGuardMask =
            ThermalGridBufferGuardBit(CsvBytesId) |
            ThermalGridBufferGuardBit(SpecsId) |
            ThermalGridBufferGuardBit(TuningId) |
            ThermalGridBufferGuardBit(CountersId);

        private IDataVault _vault;
        private VaultGenerationHandle<GridNodeDTO> _nodesAHandle;
        private VaultGenerationHandle<GridNodeDTO> _nodesBHandle;
        private VaultGenerationHandle<PowerEdgeDTO> _edgesHandle;
        private VaultGenerationHandle<float> _injectionsHandle;
        private VaultGenerationHandle<float> _externalHeatHandle;
        private VaultGenerationHandle<ThermalGridAnchorDTO> _anchorsHandle;
        private VaultGenerationHandle<SubmarineThermalGridTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ThermalPowerGridTelemetrySnapshot> _telemetryHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<SubmarineGridSpecDTO> _specsHandle;
        private VaultGenerationHandle<byte> _csvBytesHandle;
        private VaultGenerationHandle<ThermalGridVisualStateDTO> _visualStateHandle;
        private VaultGenerationHandle<SolverConvergenceStateDTO> _convergenceStateHandle;
        private VaultGenerationHandle<SolverResidualSlot64> _residualSamplesHandle;
        private VaultGenerationHandle<GridNodeDTO> _pendingNodesHandle;
        private VaultGenerationHandle<PowerEdgeDTO> _pendingEdgesHandle;
        private VaultGenerationHandle<float> _pendingInjectionsHandle;
        private VaultGenerationHandle<ThermalGridAnchorDTO> _pendingAnchorsHandle;
        private VaultGenerationHandle<ThermalGridVisualStateDTO> _pendingVisualStateHandle;
        private VaultGenerationHandle<int> _pendingCountersHandle;

        private JobHandle _solveHandle;
        private JobHandle _topologyRebuildHandle;
        private JobHandle _externalHeatJobHandle;
        private bool _initialized;
        private bool _solvePending;
        private bool _topologyRebuildPending;
        private bool _externalHeatPending;
        private bool _activeFrontIsA = true;
        private bool _pendingFrontIsA = true;
        private int _pendingIterations;
        private int _solveLockedBufferCount;
        private int _topologyLockedBufferCount;
        private int _externalHeatLockedBufferCount;
        private uint _frame;

        public static SubmarineOsThermalGridRuntime Active => s_active;

        public bool IsInitialized => _initialized;

        public int NodeCount => ResolveNodeCount();

        public int EdgeCount => ResolveEdgeCount();

        private ref struct VaultViews
        {
            public NativeArray<GridNodeDTO> NodesA;
            public NativeArray<GridNodeDTO> NodesB;
            public NativeArray<PowerEdgeDTO> Edges;
            public NativeArray<float> Injections;
            public NativeArray<float> ExternalHeat;
            public NativeArray<ThermalGridAnchorDTO> Anchors;
            public NativeArray<SubmarineThermalGridTuningDTO> Tuning;
            public NativeArray<ThermalPowerGridTelemetrySnapshot> Telemetry;
            public NativeArray<int> Counters;
            public NativeArray<SubmarineGridSpecDTO> Specs;
            public NativeArray<byte> CsvBytes;
            public NativeArray<ThermalGridVisualStateDTO> VisualState;
            public NativeArray<SolverConvergenceStateDTO> ConvergenceState;
            public NativeArray<SolverResidualSlot64> ResidualSamples;
            public NativeArray<GridNodeDTO> PendingNodes;
            public NativeArray<PowerEdgeDTO> PendingEdges;
            public NativeArray<float> PendingInjections;
            public NativeArray<ThermalGridAnchorDTO> PendingAnchors;
            public NativeArray<ThermalGridVisualStateDTO> PendingVisualState;
            public NativeArray<int> PendingCounters;
        }

        private ref struct CsvImportViews
        {
            public NativeArray<byte> CsvBytes;
            public NativeArray<SubmarineGridSpecDTO> Specs;
            public NativeArray<SubmarineThermalGridTuningDTO> Tuning;
            public NativeArray<int> Counters;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActiveRuntimeForEditorReload();
            s_active = null;
#if UNITY_EDITOR
            DisposeStandaloneVaultForEditorReload();
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ShutdownActiveRuntimeForEditorReload();
            }
        }

        private static void DisposeStandaloneVaultForEditorReload()
        {
            s_standaloneVault?.Dispose();
            s_standaloneVault = null;
        }
#endif

        private static void ShutdownActiveRuntimeForEditorReload()
        {
            SubmarineOsThermalGridRuntime runtime = s_active;
            if (runtime != null)
                runtime.Dispose();
#if UNITY_EDITOR
            DisposeStandaloneVaultForEditorReload();
#endif
        }

        public void InjectDataVault(IDataVault vault)
        {
            if (!_initialized)
                _vault = vault;
        }

        public bool EnsureInitialized()
        {
            if (_initialized)
                return true;

            _vault ??= ResolveDataVault();

            if (_vault == null || !ValidateLayouts(out _, out _, out _, out _))
                return false;

            if (!ResolveVaultBuffers(out VaultViews views))
                return false;

            SubmarineThermalGridTuningDTO tuning = CreateDefaultTuning();
            views.Tuning[0] = tuning;
            ClearActiveRangeJob clear = new ClearActiveRangeJob
            {
                NodesA = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesA),
                NodesB = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesB),
                Edges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Edges),
                Injections = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Injections),
                ExternalHeat = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ExternalHeat),
                Anchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Anchors),
                VisualState = (ThermalGridVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.VisualState),
                ConvergenceState = (SolverConvergenceStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ConvergenceState),
                ResidualSamples = (SolverResidualSlot64*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ResidualSamples),
                PendingNodes = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingNodes),
                PendingEdges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingEdges),
                PendingInjections = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingInjections),
                PendingAnchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingAnchors),
                PendingVisualState = (ThermalGridVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingVisualState),
                PendingCounters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingCounters),
                Count = MaxNodes,
                EdgeCount = MaxEdges,
                ResidualSlotCount = ResidualThreadSlotCount,
                CounterCount = CounterCount
            };
            // COLD SYNC JOB: one-time vault bootstrap clear before any gameplay tick can observe the buffers.
            int clearCount = math.max(MaxNodes, MaxEdges);
            for (int i = 0; i < clearCount; i++)
                clear.Execute(i);

            for (int i = 0; i < CounterCount; i++)
                views.Counters[i] = 0;
            for (int i = 0; i < views.Telemetry.Length; i++)
                views.Telemetry[i] = default;

            _initialized = true;
            s_active = this;
            ScheduleEmergencyMockGrid(default);
            if (_topologyRebuildPending)
            {
                // COLD SYNC JOB: fallback mock must be materialized before the runtime can expose a readback handle.
                ForceCompleteTopologyRebuildInPostSimulationWindow();
                _topologyRebuildPending = false;
                if (TryLockTopologyCommitTargetBuffers(out int commitLockedCount))
                {
                    bool committed = false;
                    try
                    {
                        committed = TryCommitPendingTopologySnapshot();
                        if (committed)
                        {
                            _activeFrontIsA = true;
                            _pendingFrontIsA = true;
                        }
                    }
                    finally
                    {
                        UnlockTopologyCommitTargetBuffers(commitLockedCount);
                        UnlockTopologyRebuildBuffers();
                    }

                    if (!committed)
                    {
                        _initialized = false;
                        if (ReferenceEquals(s_active, this))
                            s_active = null;
                        return false;
                    }
                }
                else
                {
                    UnlockTopologyRebuildBuffers();
                    _initialized = false;
                    if (ReferenceEquals(s_active, this))
                        s_active = null;
                    return false;
                }
            }
            return true;
        }

        private static IDataVault ResolveDataVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null)
                return vault;

#if UNITY_EDITOR
            s_standaloneVault ??= GlobalDataVault.Create(StandaloneVaultBufferCapacity, StandaloneVaultArenaBytes);
            return s_standaloneVault;
#else
            return null;
#endif
        }

        public bool ScheduleEmergencyMockGrid(JobHandle dependency)
        {
            return ScheduleEmergencyMockGridInternal(dependency, 0f);
        }

        public bool ScheduleEmergencyMockOscillatorGrid(JobHandle dependency)
        {
            return ScheduleEmergencyMockGridInternal(dependency, 1f);
        }

        private bool ScheduleEmergencyMockGridInternal(JobHandle dependency, float oscillator01)
        {
            if (!EnsureInitialized() || _topologyRebuildPending)
                return false;

            if (!TryLockTopologyRebuildBuffers(out _topologyLockedBufferCount))
                return false;
            if (!TryResolveVaultViews(out VaultViews views))
            {
                UnlockTopologyRebuildBuffers();
                return false;
            }

            _topologyRebuildHandle = new EmergencyMockGridJob
            {
                Nodes = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingNodes),
                Edges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingEdges),
                Injections = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingInjections),
                Anchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingAnchors),
                VisualState = (ThermalGridVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingVisualState),
                Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingCounters),
                Tuning = views.Tuning[0],
                NodeCount = EmergencyMockNodeCount,
                Oscillator01 = math.saturate(FiniteOr(oscillator01, 0f))
            }.Schedule(EmergencyMockNodeCount, 32, dependency);

            _topologyRebuildPending = true;
            return true;
        }

        public bool ScheduleTopologyRebuildFromSnapshot(
            NativeArray<GridNodeDTO> nodes,
            NativeArray<PowerEdgeDTO> edges,
            NativeArray<float> injections,
            NativeArray<ThermalGridAnchorDTO> anchors,
            int nodeCount,
            int edgeCount,
            JobHandle dependency)
        {
            if (!EnsureInitialized() || _topologyRebuildPending)
                return false;

            nodeCount = math.clamp(nodeCount, 0, math.min(nodes.IsCreated ? nodes.Length : 0, MaxNodes));
            edgeCount = math.clamp(edgeCount, 0, math.min(edges.IsCreated ? edges.Length : 0, MaxEdges));
            if (nodeCount <= 0 ||
                edgeCount < 0 ||
                !injections.IsCreated ||
                !anchors.IsCreated ||
                injections.Length < nodeCount ||
                anchors.Length < nodeCount)
            {
                return false;
            }

            if (!TryLockTopologyRebuildBuffers(out _topologyLockedBufferCount))
                return false;
            if (!TryResolveVaultViews(out VaultViews views))
            {
                UnlockTopologyRebuildBuffers();
                return false;
            }

            int scheduleCount = math.max(nodeCount, math.max(edgeCount, CounterCount));
            _topologyRebuildHandle = new TopologySnapshotRebuildJob
            {
                SourceNodes = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                SourceEdges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(edges),
                SourceInjections = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(injections),
                SourceAnchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(anchors),
                PendingNodes = (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingNodes),
                PendingEdges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingEdges),
                PendingInjections = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingInjections),
                PendingAnchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingAnchors),
                PendingVisualState = (ThermalGridVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingVisualState),
                PendingCounters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(views.PendingCounters),
                NodeCount = nodeCount,
                EdgeCount = edgeCount
            }.Schedule(scheduleCount, 64, dependency);

            _topologyRebuildPending = true;
            return true;
        }

        public bool TryCommitTopologyRebuildPostSimulation()
        {
            if (!_topologyRebuildPending)
                return true;
            if (!_topologyRebuildHandle.IsCompleted)
                return false;
            if (_solvePending)
                return false;
            if (!TryLockTopologyCommitTargetBuffers(out int commitLockedCount))
                return false;

            bool finalized = false;
            try
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _topologyRebuildHandle))
                    return false;

                finalized = true;
                bool committed = TryCommitPendingTopologySnapshot();
                _topologyRebuildPending = false;
                if (committed)
                {
                    _activeFrontIsA = true;
                    _pendingFrontIsA = true;
                }

                return committed;
            }
            finally
            {
                UnlockTopologyCommitTargetBuffers(commitLockedCount);
                if (finalized)
                {
                    _topologyRebuildPending = false;
                    UnlockTopologyRebuildBuffers();
                }
            }
        }

        public bool ScheduleExternalThermalInjection(
            double3 submarineBaseAup,
            double3 hazardAup,
            float hazardTemperatureCelsius,
            float hazardRadiusMeters,
            float globalQualityWeight,
            JobHandle dependency,
            out JobHandle handle)
        {
            handle = dependency;
            if (!EnsureInitialized() || _solvePending || _externalHeatPending)
                return false;

            int nodeCount = NodeCount;
            if (nodeCount <= 0)
                return false;

            if (!TryLockExternalHeatBuffers(out _externalHeatLockedBufferCount))
                return false;
            if (!TryResolveVaultViews(out VaultViews views))
            {
                UnlockExternalHeatBuffers();
                return false;
            }

            handle = new ExternalThermalInjectionJob
            {
                ExternalHeat = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ExternalHeat),
                Anchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Anchors),
                NodeCount = nodeCount,
                SubmarineBaseAup = submarineBaseAup,
                HazardAup = hazardAup,
                HazardTemperatureCelsius = math.select(40f, hazardTemperatureCelsius, math.isfinite(hazardTemperatureCelsius)),
                HazardRadiusMeters = math.max(1f, math.select(1f, hazardRadiusMeters, math.isfinite(hazardRadiusMeters)))
            }.Schedule(nodeCount, 64, dependency);
            _externalHeatJobHandle = handle;
            _externalHeatPending = true;
            return true;
        }

        public bool ScheduleSolve(
            float simulationTickDeltaSeconds,
            float globalQualityWeight,
            uint simulationFrame,
            JobHandle dependency,
            out JobHandle handle)
        {
            handle = dependency;
            if (!EnsureInitialized() || _solvePending)
                return false;
            if (_externalHeatPending && !TryCompleteExternalThermalInjectionPostSimulation())
                return false;

            int nodeCount = NodeCount;
            int edgeCount = EdgeCount;
            if (nodeCount <= 0)
                return false;

            float qualityWeight = MathLodApproximation.SaturateFinite(globalQualityWeight, PowerSolverConvergenceMath.AuthoritativeQualityWeight);
            int iterations = ResolvePropagationIterations(qualityWeight);
            if (!TryLockSolveBuffers(out _solveLockedBufferCount))
                return false;
            if (!TryResolveVaultViews(out VaultViews views))
            {
                UnlockSolveBuffers();
                return false;
            }

            SubmarineThermalGridTuningDTO tuning = views.Tuning[0];
            tuning.SimulationTickDeltaSeconds = math.max(Epsilon, simulationTickDeltaSeconds);
            tuning.VisualOverkillScalar = qualityWeight;
            tuning = SanitizeTuning(in tuning);
            float solverTolerance = ResolveSolverTargetTolerance(tuning.JacobiTolerance, qualityWeight) * math.max(Epsilon, tuning.ToleranceMultiplier);
            float solverOmega = math.clamp(ResolveSolverOmega(qualityWeight) * math.max(0.25f, tuning.BaseOmegaFactor), 0.55f, 1f);
            int residualSampleMask = ResolveResidualSampleMask(qualityWeight);
            views.Tuning[0] = tuning;

            bool inputIsA = _activeFrontIsA;
            JobHandle chain = dependency;
            float thermalSignalHeat01 = ResolveThermalStateSignalHeat01();
            if (thermalSignalHeat01 > Epsilon)
            {
                chain = new ThermalStateSignalInjectionJob
                {
                    ExternalHeat = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ExternalHeat),
                    Anchors = (ThermalGridAnchorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Anchors),
                    NodeCount = nodeCount,
                    SignalHeat01 = thermalSignalHeat01,
                    GlobalQualityWeight = qualityWeight
                }.Schedule(nodeCount, 64, chain);
            }

            SolverConvergenceStateDTO* convergencePtr = (SolverConvergenceStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ConvergenceState);
            SolverResidualSlot64* residualSamplesPtr = (SolverResidualSlot64*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ResidualSamples);
            chain = new InitializeSolverConvergenceJob
            {
                ConvergenceState = convergencePtr,
                ResidualSamples = residualSamplesPtr,
                ResidualSlotCount = ResidualThreadSlotCount,
                BaseOmega = solverOmega
            }.Schedule(ResidualThreadSlotCount, 64, chain);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                chain = new ClearSolverResidualSlotsJob
                {
                    ResidualSamples = residualSamplesPtr,
                    ResidualSlotCount = ResidualThreadSlotCount
                }.Schedule(ResidualThreadSlotCount, 64, chain);

                GridNodeDTO* readPtr = inputIsA
                    ? (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.NodesA)
                    : (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.NodesB);
                GridNodeDTO* writePtr = inputIsA
                    ? (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesB)
                    : (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesA);

                PowerGridRelaxationJob job = new PowerGridRelaxationJob
                {
                    NodesRead = readPtr,
                    NodesWrite = writePtr,
                    Edges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Edges),
                    Injections = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Injections),
                    ExternalHeat = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(views.ExternalHeat),
                    VisualState = (ThermalGridVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.VisualState),
                    ConvergenceState = convergencePtr,
                    ResidualSamples = residualSamplesPtr,
                    Tuning = views.Tuning[0],
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    DeltaSeconds = simulationTickDeltaSeconds,
                    Frame = simulationFrame,
                    IterationIndex = iteration,
                    IterationCount = iterations,
                    TargetTolerance = solverTolerance,
                    ResidualSampleMask = residualSampleMask,
                    ResidualSlotCount = ResidualThreadSlotCount
                };
                chain = job.Schedule(nodeCount, 64, chain);
                chain = new ConvergenceResidualReductionJob
                {
                    ConvergenceState = convergencePtr,
                    ResidualSamples = residualSamplesPtr,
                    TargetTolerance = solverTolerance,
                    BaseOmega = solverOmega,
                    ResidualSlotCount = ResidualThreadSlotCount,
                    FinalIteration = iteration == iterations - 1 ? (byte)1 : (byte)0
                }.Schedule(chain);
                inputIsA = !inputIsA;
            }

            GridNodeDTO* finalPtr = inputIsA
                ? (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesA)
                : (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesB);
            GridNodeDTO* residualBaselinePtr = inputIsA
                ? (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.NodesB)
                : (GridNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.NodesA);
            chain = new ShortCircuitIsolationJob
            {
                Nodes = finalPtr,
                Edges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Edges),
                NodeCount = nodeCount
            }.Schedule(nodeCount, 64, chain);

            chain = new ThermalGridTelemetryJob
            {
                Nodes = finalPtr,
                PreviousNodes = residualBaselinePtr,
                Edges = (PowerEdgeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Edges),
                Injections = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.Injections),
                Telemetry = (ThermalPowerGridTelemetrySnapshot*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Telemetry),
                ConvergenceState = convergencePtr,
                Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(views.Counters),
                NodeCount = nodeCount,
                EdgeCount = edgeCount,
                Iterations = iterations,
                Frame = simulationFrame,
                TargetTolerance = solverTolerance
            }.Schedule(chain);

            _solveHandle = chain;
            _solvePending = true;
            _pendingFrontIsA = inputIsA;
            _pendingIterations = iterations;
            _frame = simulationFrame;
            handle = chain;
            return true;
        }

        public bool TryCompleteSolvePostSimulation()
        {
            if (!_solvePending)
                return true;
            if (!_solveHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _solveHandle))
                return false;

            _solvePending = false;
            try
            {
                _activeFrontIsA = _pendingFrontIsA;

                const SubmarineThermalGridFaultFlags dumpFaultMask =
                    SubmarineThermalGridFaultFlags.CriticalThermalFailure |
                    SubmarineThermalGridFaultFlags.NonFinite |
                    SubmarineThermalGridFaultFlags.Divergent |
                    SubmarineThermalGridFaultFlags.MaxIterations;
                NativeArray<int> counters;
                if (TryResolveCounters(out counters))
                {
                    int activeFaultMask = counters[CounterFaultFlags] & (int)dumpFaultMask;
                    if (activeFaultMask != 0)
                    {
                        int dumpedFaultMask = counters[CounterDumpedFaultMask];
                        int newFaultMask = activeFaultMask & ~dumpedFaultMask;
                        if (newFaultMask != 0)
                        {
                            DumpBlackBox();
                            counters[CounterDumpedFaultMask] = dumpedFaultMask | activeFaultMask;
                        }
                    }
                    else
                    {
                        counters[CounterDumpedFaultMask] = 0;
                    }
                }
            }
            finally
            {
                UnlockSolveBuffers();
            }

            return true;
        }

        public bool TryCompleteExternalThermalInjectionPostSimulation()
        {
            if (!_externalHeatPending)
                return true;
            if (!_externalHeatJobHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _externalHeatJobHandle))
                return false;

            _externalHeatPending = false;
            try
            {
                return true;
            }
            finally
            {
                UnlockExternalHeatBuffers();
            }
        }

        public bool TryGetGridReadback(
            out NativeArray<GridNodeDTO>.ReadOnly nodes,
            out NativeArray<ThermalGridAnchorDTO>.ReadOnly anchors,
            out NativeArray<ThermalGridVisualStateDTO>.ReadOnly visualState,
            out int nodeCount)
        {
            nodes = default;
            anchors = default;
            visualState = default;
            nodeCount = 0;

            if (!TryGetGridReadbackMutable(
                    out NativeArray<GridNodeDTO> mutableNodes,
                    out NativeArray<ThermalGridAnchorDTO> mutableAnchors,
                    out NativeArray<ThermalGridVisualStateDTO> mutableVisualState,
                    out nodeCount))
            {
                return false;
            }

            nodes = mutableNodes.AsReadOnly();
            anchors = mutableAnchors.AsReadOnly();
            visualState = mutableVisualState.AsReadOnly();
            return true;
        }

        private bool TryGetGridReadbackMutable(
            out NativeArray<GridNodeDTO> nodes,
            out NativeArray<ThermalGridAnchorDTO> anchors,
            out NativeArray<ThermalGridVisualStateDTO> visualState,
            out int nodeCount)
        {
            nodes = default;
            anchors = default;
            visualState = default;
            nodeCount = 0;
            if (!_initialized || _solvePending)
                return false;

            if (!TryResolveVaultViews(out VaultViews views))
                return false;

            nodes = _activeFrontIsA ? views.NodesA : views.NodesB;
            anchors = views.Anchors;
            visualState = views.VisualState;
            nodeCount = NodeCount;
            return nodes.IsCreated && anchors.IsCreated && visualState.IsCreated && nodeCount > 0;
        }

        public bool TryGetConvergenceReadback(
            out SolverConvergenceStateDTO state,
            out ThermalPowerGridTelemetrySnapshot latestTelemetry)
        {
            state = default;
            latestTelemetry = default;
            if (!_initialized || _solvePending)
                return false;
            if (!TryResolveVaultViews(out VaultViews views) || !views.ConvergenceState.IsCreated || !views.Telemetry.IsCreated)
                return false;

            state = views.ConvergenceState[0];
            int cursor = views.Counters.IsCreated ? views.Counters[CounterTelemetryCursor] : 0;
            int readIndex = math.abs(cursor + TelemetryFrameCount - 1) % TelemetryFrameCount;
            latestTelemetry = views.Telemetry[readIndex];
            return true;
        }

        public bool TryReadTuning(out SubmarineThermalGridTuningDTO tuning)
        {
            tuning = default;
            if (!_initialized)
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                !IsHandleValid(in _tuningHandle) ||
                !vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<SubmarineThermalGridTuningDTO>.ReadOnly tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public bool TryApplyTuning(in SubmarineThermalGridTuningDTO tuning)
        {
            if (!EnsureInitialized())
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsHandleValid(in _tuningHandle))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.CoreDiagnostics, out NativeArray<SubmarineThermalGridTuningDTO> tuningBuffer))
                return false;

            try
            {
                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = SanitizeTuning(in tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.CoreDiagnostics);
            }
        }

        public bool TryUploadVisualScalars(GraphicsBuffer targetBuffer)
        {
            if (targetBuffer == null ||
                !TryGetGridReadback(out _, out _, out NativeArray<ThermalGridVisualStateDTO>.ReadOnly visual, out int nodeCount))
            {
                return false;
            }

            int count = math.min(math.min(nodeCount, visual.Length), targetBuffer.count);
            if (count <= 0 || targetBuffer.stride != UnsafeUtility.SizeOf<ThermalGridVisualStateDTO>())
                return false;

            NativeArray<ThermalGridVisualStateDTO> mapped = targetBuffer.LockBufferForWrite<ThermalGridVisualStateDTO>(0, count);
            try
            {
                for (int i = 0; i < count; i++)
                    mapped[i] = visual[i];
            }
            finally
            {
                targetBuffer.UnlockBufferAfterWrite<ThermalGridVisualStateDTO>(count);
            }
            Shader.SetGlobalInt(s_ThermalGridNodeCountId, count);
            return true;
        }

        public bool TryPublishVisualShaderScalars()
        {
            if (!TryGetGridReadback(out _, out _, out NativeArray<ThermalGridVisualStateDTO>.ReadOnly visual, out int nodeCount))
                return false;

            int count = math.min(nodeCount, visual.Length);
            if (count <= 0)
                return false;

            float minVoltage = 1f;
            float maxHeat = 0f;
            float maxFlicker = 0f;
            float maxVisualOverkill = 0f;
            for (int i = 0; i < count; i++)
            {
                ThermalGridVisualStateDTO state = visual[i];
                minVoltage = math.min(minVoltage, math.saturate(FiniteOr(state.Voltage01, 1f)));
                maxHeat = math.max(maxHeat, math.saturate(FiniteOr(state.Thermal01, 0f)));
                maxFlicker = math.max(maxFlicker, math.saturate(FiniteOr(state.FlickerPhase01, 0f)));
                maxVisualOverkill = math.max(maxVisualOverkill, math.saturate(FiniteOr(state.VisualOverkill01, 0f)));
            }

            Shader.SetGlobalInt(s_ThermalGridNodeCountId, count);
            Shader.SetGlobalFloat(s_ThermalGridBrownoutId, math.saturate(1f - minVoltage));
            Shader.SetGlobalFloat(s_ThermalGridMaxHeatId, maxHeat);
            Shader.SetGlobalFloat(s_ThermalGridFlickerId, maxFlicker);
            Shader.SetGlobalFloat(s_ThermalGridVisualOverkillId, maxVisualOverkill);
            return true;
        }

#if UNITY_EDITOR
        public bool TryLoadCsvFromFile(string path)
        {
            if (!EnsureInitialized() || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryAcquireCsvImportViews(out CsvImportViews views, out int lockedCount))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long streamLength = stream.Length;
                int length = streamLength > views.CsvBytes.Length ? views.CsvBytes.Length : (int)streamLength;
                if (length <= 0)
                    return false;

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(views.CsvBytes);
                Span<byte> buffer = new Span<byte>(ptr, length);
                int read = 0;
                while (read < length)
                {
                    int chunk = stream.Read(buffer.Slice(read));
                    if (chunk <= 0)
                        break;
                    read += chunk;
                }

                if (read <= 0)
                    return false;

                int parsed = SubmarineThermalGridCsvParser.ParseGridSpecsCsv(
                    buffer.Slice(0, read),
                    views.Specs,
                    views.Tuning,
                    0);
                if (views.Counters.IsCreated)
                    views.Counters[CounterCsvSpecCount] = parsed;
                return parsed > 0;
            }
            catch (Exception exception)
            {
                if (views.Counters.IsCreated && views.Counters.Length > CounterFaultFlags)
                    views.Counters[CounterFaultFlags] |= (int)SubmarineThermalGridFaultFlags.AuthoringImportFault;
                GlobalTelemetryBus.PublishPerformanceWarning(0x53314353u, SourceHash, exception.HResult);
                return false;
            }
            finally
            {
                ReleaseCsvImportViews(lockedCount);
            }
        }
#endif

        public void ForceDumpBlackBox()
        {
            if (EnsureInitialized())
                DumpBlackBox();
        }

        public void Dispose()
        {
            ForceCompletePendingJobsInPostSimulationWindow();

            _nodesAHandle = default;
            _nodesBHandle = default;
            _edgesHandle = default;
            _injectionsHandle = default;
            _externalHeatHandle = default;
            _anchorsHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _countersHandle = default;
            _specsHandle = default;
            _csvBytesHandle = default;
            _visualStateHandle = default;
            _convergenceStateHandle = default;
            _residualSamplesHandle = default;
            _pendingNodesHandle = default;
            _pendingEdgesHandle = default;
            _pendingInjectionsHandle = default;
            _pendingAnchorsHandle = default;
            _pendingVisualStateHandle = default;
            _pendingCountersHandle = default;
            _initialized = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private bool ForceCompleteTopologyRebuildInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref _topologyRebuildHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void ForceCompletePendingJobsInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (_solvePending)
                {
                    // TEARDOWN FENCE: dispose cannot leave vault buffers locked behind live worker pointers.
                    DispatcherJobFence.TryComplete(ref _solveHandle, forceComplete: true);
                    _solvePending = false;
                    UnlockSolveBuffers();
                }

                if (_topologyRebuildPending)
                {
                    // TEARDOWN FENCE: topology staging buffers must be released before the runtime drops its vault aliases.
                    DispatcherJobFence.TryComplete(ref _topologyRebuildHandle, forceComplete: true);
                    _topologyRebuildPending = false;
                    UnlockTopologyRebuildBuffers();
                }

                if (_externalHeatPending)
                {
                    // TEARDOWN FENCE: external heat writes must finish before buffer aliases are cleared.
                    DispatcherJobFence.TryComplete(ref _externalHeatJobHandle, forceComplete: true);
                    _externalHeatPending = false;
                    UnlockExternalHeatBuffers();
                }
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        public static int ResolvePropagationIterations(float globalQualityWeight)
        {
            return PowerSolverConvergenceMath.ResolvePropagationIterations(globalQualityWeight);
        }

        public static float ResolveSolverTargetTolerance(float baseTolerance, float globalQualityWeight)
        {
            return PowerSolverConvergenceMath.ResolveSolverTargetTolerance(baseTolerance, globalQualityWeight);
        }

        public static float ResolveSolverOmega(float globalQualityWeight)
        {
            return PowerSolverConvergenceMath.ResolveSolverOmega(globalQualityWeight);
        }

        public static int ResolveResidualSampleMask(float globalQualityWeight)
        {
            return PowerSolverConvergenceMath.ResolveResidualSampleMask(globalQualityWeight);
        }

        public static SubmarineThermalGridTuningDTO CreateDefaultTuning()
        {
            return new SubmarineThermalGridTuningDTO
            {
                BaseResistance = 0.06f,
                ThermalDissipationRate = 0.18f,
                JacobiTolerance = 0.001f,
                DamageThreshold = 0.72f,
                CriticalThermalThreshold = 1.0f,
                HeatGainScale = 0.018f,
                ResistanceDriftRate = 0.025f,
                ExternalHeatScale = 0.12f,
                BrownoutVoltageThreshold = 0.2f,
                FlickerScale = 0.35f,
                VisualOverkillScalar = 0.5f,
                SimulationTickDeltaSeconds = 0.05f,
                BaseOmegaFactor = 1f,
                ToleranceMultiplier = 1f
            };
        }

        public static bool ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes, out int telemetryBytes)
        {
            nodeBytes = UnsafeUtility.SizeOf<GridNodeDTO>();
            edgeBytes = UnsafeUtility.SizeOf<PowerEdgeDTO>();
            tuningBytes = UnsafeUtility.SizeOf<SubmarineThermalGridTuningDTO>();
            telemetryBytes = UnsafeUtility.SizeOf<ThermalPowerGridTelemetrySnapshot>();
            int convergenceBytes = UnsafeUtility.SizeOf<SolverConvergenceStateDTO>();
            int residualSlotBytes = UnsafeUtility.SizeOf<SolverResidualSlot64>();

            return nodeBytes == GridNodeSizeBytes &&
                   edgeBytes == PowerEdgeSizeBytes &&
                   tuningBytes == 64 &&
                   telemetryBytes == TelemetrySizeBytes &&
                   convergenceBytes == SolverConvergenceStateSizeBytes &&
                   residualSlotBytes == SolverResidualSlotSizeBytes &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.NodeHash)) == 0 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.Potential)) == 4 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.Resistance)) == 8 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.ThermalLoad)) == 12 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.Flags)) == 16 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.AdjacencyOffset)) == 20 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO.AdjacencyCount)) == 24 &&
                   UnsafeFieldOffset<GridNodeDTO>(nameof(GridNodeDTO._pad0)) == 28 &&
                   UnsafeFieldOffset<PowerEdgeDTO>(nameof(PowerEdgeDTO.TargetIndex)) == 0 &&
                   UnsafeFieldOffset<PowerEdgeDTO>(nameof(PowerEdgeDTO.Conductance)) == 4 &&
                   UnsafeFieldOffset<SolverConvergenceStateDTO>(nameof(SolverConvergenceStateDTO.MaxResidualFloat)) == 0 &&
                   UnsafeFieldOffset<SolverConvergenceStateDTO>(nameof(SolverConvergenceStateDTO.PreviousResidualFloat)) == 4 &&
                   UnsafeFieldOffset<SolverConvergenceStateDTO>(nameof(SolverConvergenceStateDTO.Omega)) == 8 &&
                   UnsafeFieldOffset<SolverConvergenceStateDTO>(nameof(SolverConvergenceStateDTO.IterationCount)) == 12 &&
                   UnsafeFieldOffset<SolverConvergenceStateDTO>(nameof(SolverConvergenceStateDTO.FaultFlags)) == 14 &&
                   UnsafeFieldOffset<SolverResidualSlot64>(nameof(SolverResidualSlot64.MaxResidualFloat)) == 0 &&
                   UnsafeFieldOffset<SolverResidualSlot64>(nameof(SolverResidualSlot64.FaultFlags)) == 4 &&
                   UnsafeFieldOffset<SubmarineThermalGridTuningDTO>(nameof(SubmarineThermalGridTuningDTO.BaseOmegaFactor)) == 56 &&
                   UnsafeFieldOffset<SubmarineThermalGridTuningDTO>(nameof(SubmarineThermalGridTuningDTO.ToleranceMultiplier)) == 60;
        }

        public static bool SelfAuditArchitecture(out uint auditHash)
        {
            bool layoutsValid = ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes, out int telemetryBytes);
            float lowTolerance = ResolveSolverTargetTolerance(0.001f, 0f);
            float midTolerance = ResolveSolverTargetTolerance(0.001f, 0.5f);
            float highTolerance = ResolveSolverTargetTolerance(0.001f, 1f);
            float lowOmega = ResolveSolverOmega(0f);
            float midOmega = ResolveSolverOmega(0.5f);
            float highOmega = ResolveSolverOmega(1f);
            int lowIterations = ResolvePropagationIterations(0f);
            int midIterations = ResolvePropagationIterations(0.5f);
            int highIterations = ResolvePropagationIterations(1f);
            int lowMask = ResolveResidualSampleMask(0f);
            int midMask = ResolveResidualSampleMask(0.5f);
            int highMask = ResolveResidualSampleMask(1f);
            auditHash = 2166136261u;
            auditHash = (auditHash ^ (uint)nodeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)edgeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)tuningBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)telemetryBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)SolverConvergenceStateSizeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)SolverResidualSlotSizeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)MaxNodes) * 16777619u;
            auditHash = (auditHash ^ (uint)TelemetryFrameCount) * 16777619u;
            auditHash = (auditHash ^ (uint)lowIterations) * 16777619u;
            auditHash = (auditHash ^ (uint)highIterations) * 16777619u;
            auditHash = (auditHash ^ (uint)lowMask) * 16777619u;
            auditHash = (auditHash ^ (uint)highMask) * 16777619u;
            return layoutsValid &&
                   MaxNodes == 512 &&
                   MaxEdges == MaxNodes * 6 &&
                   TelemetryFrameCount == 300 &&
                   SolverConvergenceStateSizeBytes == 16 &&
                   SolverResidualSlotSizeBytes == 64 &&
                   lowIterations == PowerSolverConvergenceMath.MinPropagationIterations &&
                   lowIterations < midIterations &&
                   midIterations < highIterations &&
                   highIterations == PowerSolverConvergenceMath.MaxPropagationIterations &&
                   lowTolerance >= midTolerance &&
                   midTolerance > highTolerance &&
                   lowOmega < midOmega &&
                   midOmega < highOmega &&
                   lowMask >= midMask &&
                   midMask >= highMask;
        }

        private bool ResolveVaultBuffers(out VaultViews views)
        {
            views = default;
            IDataVault vault = _vault;
            return ResolveVaultBuffer(vault, ref _nodesAHandle, NodesAId, MaxNodes, out views.NodesA) &&
                   ResolveVaultBuffer(vault, ref _nodesBHandle, NodesBId, MaxNodes, out views.NodesB) &&
                   ResolveVaultBuffer(vault, ref _edgesHandle, EdgesId, MaxEdges, out views.Edges) &&
                   ResolveVaultBuffer(vault, ref _injectionsHandle, InjectionsId, MaxNodes, out views.Injections) &&
                   ResolveVaultBuffer(vault, ref _externalHeatHandle, ExternalHeatId, MaxNodes, out views.ExternalHeat) &&
                   ResolveVaultBuffer(vault, ref _anchorsHandle, AnchorsId, MaxNodes, out views.Anchors) &&
                   ResolveVaultBuffer(vault, ref _tuningHandle, TuningId, 1, out views.Tuning) &&
                   ResolveVaultBuffer(vault, ref _telemetryHandle, TelemetryId, TelemetryFrameCount, out views.Telemetry) &&
                   ResolveVaultBuffer(vault, ref _countersHandle, CountersId, CounterCount, out views.Counters) &&
                   ResolveVaultBuffer(vault, ref _specsHandle, SpecsId, CsvSpecCapacity, out views.Specs) &&
                   ResolveVaultBuffer(vault, ref _csvBytesHandle, CsvBytesId, CsvByteCapacity, out views.CsvBytes) &&
                   ResolveVaultBuffer(vault, ref _visualStateHandle, VisualStateId, MaxNodes, out views.VisualState) &&
                   ResolveVaultBuffer(vault, ref _convergenceStateHandle, ConvergenceStateId, 1, out views.ConvergenceState) &&
                   ResolveVaultBuffer(vault, ref _residualSamplesHandle, ResidualSamplesId, ResidualThreadSlotCount, out views.ResidualSamples) &&
                   ResolveVaultBuffer(vault, ref _pendingNodesHandle, PendingNodesId, MaxNodes, out views.PendingNodes) &&
                   ResolveVaultBuffer(vault, ref _pendingEdgesHandle, PendingEdgesId, MaxEdges, out views.PendingEdges) &&
                   ResolveVaultBuffer(vault, ref _pendingInjectionsHandle, PendingInjectionsId, MaxNodes, out views.PendingInjections) &&
                   ResolveVaultBuffer(vault, ref _pendingAnchorsHandle, PendingAnchorsId, MaxNodes, out views.PendingAnchors) &&
                   ResolveVaultBuffer(vault, ref _pendingVisualStateHandle, PendingVisualStateId, MaxNodes, out views.PendingVisualState) &&
                   ResolveVaultBuffer(vault, ref _pendingCountersHandle, PendingCountersId, CounterCount, out views.PendingCounters);
        }

        private static bool ResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!IsHandleValid(in handle) && !vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;
            }
            else
            {
                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.Power,
                    NativeArrayOptions.UninitializedMemory);
            }

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveVaultViews(out VaultViews views)
        {
            views = default;
            return TryResolveVaultBuffer(_nodesAHandle, MaxNodes, out views.NodesA) &&
                   TryResolveVaultBuffer(_nodesBHandle, MaxNodes, out views.NodesB) &&
                   TryResolveVaultBuffer(_edgesHandle, MaxEdges, out views.Edges) &&
                   TryResolveVaultBuffer(_injectionsHandle, MaxNodes, out views.Injections) &&
                   TryResolveVaultBuffer(_externalHeatHandle, MaxNodes, out views.ExternalHeat) &&
                   TryResolveVaultBuffer(_anchorsHandle, MaxNodes, out views.Anchors) &&
                   TryResolveVaultBuffer(_tuningHandle, 1, out views.Tuning) &&
                   TryResolveVaultBuffer(_telemetryHandle, TelemetryFrameCount, out views.Telemetry) &&
                   TryResolveVaultBuffer(_countersHandle, CounterCount, out views.Counters) &&
                   TryResolveVaultBuffer(_specsHandle, CsvSpecCapacity, out views.Specs) &&
                   TryResolveVaultBuffer(_csvBytesHandle, CsvByteCapacity, out views.CsvBytes) &&
                   TryResolveVaultBuffer(_visualStateHandle, MaxNodes, out views.VisualState) &&
                   TryResolveVaultBuffer(_convergenceStateHandle, 1, out views.ConvergenceState) &&
                   TryResolveVaultBuffer(_residualSamplesHandle, ResidualThreadSlotCount, out views.ResidualSamples) &&
                   TryResolveVaultBuffer(_pendingNodesHandle, MaxNodes, out views.PendingNodes) &&
                   TryResolveVaultBuffer(_pendingEdgesHandle, MaxEdges, out views.PendingEdges) &&
                   TryResolveVaultBuffer(_pendingInjectionsHandle, MaxNodes, out views.PendingInjections) &&
                   TryResolveVaultBuffer(_pendingAnchorsHandle, MaxNodes, out views.PendingAnchors) &&
                   TryResolveVaultBuffer(_pendingVisualStateHandle, MaxNodes, out views.PendingVisualState) &&
                   TryResolveVaultBuffer(_pendingCountersHandle, CounterCount, out views.PendingCounters);
        }

        private bool TryResolveCounters(out NativeArray<int> counters)
        {
            return TryResolveVaultBuffer(_countersHandle, CounterCount, out counters);
        }

        private int ResolveNodeCount()
        {
            return TryResolveCounters(out NativeArray<int> counters)
                ? math.clamp(counters[CounterNodeCount], 0, MaxNodes)
                : 0;
        }

        private int ResolveEdgeCount()
        {
            return TryResolveCounters(out NativeArray<int> counters)
                ? math.clamp(counters[CounterEdgeCount], 0, MaxEdges)
                : 0;
        }

        private bool TryAcquireCsvImportViews(out CsvImportViews views, out int lockedCount)
        {
            views = default;
            lockedCount = 0;
            if (!TryAcquireThermalGridMutationGuard(CsvImportMutationGuardMask, out lockedCount))
                return false;

            if (TryResolveVaultBuffer(_csvBytesHandle, CsvByteCapacity, out views.CsvBytes) &&
                TryResolveVaultBuffer(_specsHandle, CsvSpecCapacity, out views.Specs) &&
                TryResolveVaultBuffer(_tuningHandle, 1, out views.Tuning) &&
                TryResolveVaultBuffer(_countersHandle, CounterCount, out views.Counters))
            {
                return true;
            }

            ReleaseCsvImportViews(lockedCount);
            lockedCount = 0;
            views = default;
            return false;
        }

        private bool TryResolveVaultBuffer<T>(
            VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null || !IsHandleValid(in handle))
                return false;

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        private bool TryLockTopologyRebuildBuffers(out int lockedCount)
        {
            return TryAcquireThermalGridMutationGuard(TopologyRebuildMutationGuardMask, out lockedCount);
        }

        private bool TryLockTopologyCommitTargetBuffers(out int lockedCount)
        {
            return TryAcquireThermalGridMutationGuard(TopologyCommitMutationGuardMask, out lockedCount);
        }

        private bool TryLockSolveBuffers(out int lockedCount)
        {
            return TryAcquireThermalGridMutationGuard(SolveMutationGuardMask, out lockedCount);
        }

        private bool TryLockExternalHeatBuffers(out int lockedCount)
        {
            return TryAcquireThermalGridMutationGuard(ExternalHeatMutationGuardMask, out lockedCount);
        }

        private bool TryAcquireThermalGridMutationGuard(ulong mutationGuardMask, out int lockedCount)
        {
            lockedCount = 0;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                return false;
            }

            lockedCount = 1;
            return true;
        }

        private void UnlockTopologyRebuildBuffers()
        {
            UnlockTopologyRebuildBuffers(_topologyLockedBufferCount);
            _topologyLockedBufferCount = 0;
        }

        private void UnlockSolveBuffers()
        {
            UnlockSolveBuffers(_solveLockedBufferCount);
            _solveLockedBufferCount = 0;
        }

        private void UnlockExternalHeatBuffers()
        {
            UnlockExternalHeatBuffers(_externalHeatLockedBufferCount);
            _externalHeatLockedBufferCount = 0;
        }

        private void ReleaseCsvImportViews(int lockedCount)
        {
            ReleaseThermalGridMutationGuard(CsvImportMutationGuardMask, lockedCount);
        }

        private void UnlockTopologyRebuildBuffers(int lockedCount)
        {
            ReleaseThermalGridMutationGuard(TopologyRebuildMutationGuardMask, lockedCount);
        }

        private void UnlockTopologyCommitTargetBuffers(int lockedCount)
        {
            ReleaseThermalGridMutationGuard(TopologyCommitMutationGuardMask, lockedCount);
        }

        private void UnlockSolveBuffers(int lockedCount)
        {
            ReleaseThermalGridMutationGuard(SolveMutationGuardMask, lockedCount);
        }

        private void UnlockExternalHeatBuffers(int lockedCount)
        {
            ReleaseThermalGridMutationGuard(ExternalHeatMutationGuardMask, lockedCount);
        }

        private void ReleaseThermalGridMutationGuard(ulong mutationGuardMask, int lockedCount)
        {
            IDataVault vault = _vault;
            if (vault == null || lockedCount <= 0)
                return;

            vault.ReleaseMutationGuard(mutationGuardMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ThermalGridBufferGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryCommitPendingTopologySnapshot()
        {
            if (!TryResolveVaultViews(out VaultViews views))
                return false;

            long nodeBytes = (long)UnsafeUtility.SizeOf<GridNodeDTO>() * MaxNodes;
            long edgeBytes = (long)UnsafeUtility.SizeOf<PowerEdgeDTO>() * MaxEdges;
            long injectionBytes = (long)UnsafeUtility.SizeOf<float>() * MaxNodes;
            long anchorBytes = (long)UnsafeUtility.SizeOf<ThermalGridAnchorDTO>() * MaxNodes;
            long visualBytes = (long)UnsafeUtility.SizeOf<ThermalGridVisualStateDTO>() * MaxNodes;

            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesA),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingNodes),
                nodeBytes);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.NodesB),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingNodes),
                nodeBytes);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.Edges),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingEdges),
                edgeBytes);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.Injections),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingInjections),
                injectionBytes);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.Anchors),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingAnchors),
                anchorBytes);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(views.VisualState),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.PendingVisualState),
                visualBytes);

            views.Counters[CounterNodeCount] = views.PendingCounters[CounterNodeCount];
            views.Counters[CounterEdgeCount] = views.PendingCounters[CounterEdgeCount];
            views.Counters[CounterFaultFlags] = views.PendingCounters[CounterFaultFlags];
            views.Counters[CounterMaxIterationStreak] = 0;
            views.Counters[CounterDumpedFaultMask] = 0;
            return true;
        }

        private void DumpBlackBox()
        {
            try
            {
                WriteBlackBoxFile(DumpRelativePath);
                WriteBlackBoxFile(ShinobuDumpRelativePath);
            }
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x5331444Du, SourceHash, exception.HResult);
            }
        }

        private void WriteBlackBoxFile(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            NativeArray<ThermalPowerGridTelemetrySnapshot> telemetry =
                TryResolveVaultBuffer(_telemetryHandle, TelemetryFrameCount, out NativeArray<ThermalPowerGridTelemetrySnapshot> resolvedTelemetry)
                    ? resolvedTelemetry
                    : default;
            int telemetryCount = telemetry.IsCreated ? math.min(telemetry.Length, TelemetryFrameCount) : 0;

            long totalBytes = DumpHeaderBytes + ((long)telemetryCount * DumpTelemetryEntryBytes);
            if (totalBytes < DumpHeaderBytes || totalBytes > int.MaxValue)
                return;

            const string dumpPayloadLabel = "thermalGridBlackBoxDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)totalBytes,
                    nameof(SubmarineOsThermalGridRuntime),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                WriteUInt32LittleEndian(payload, 0, DumpMagic);
                WriteUInt32LittleEndian(payload, 4, DumpVersion);
                WriteUInt32LittleEndian(payload, 8, _frame);
                WriteInt32LittleEndian(payload, 12, NodeCount);
                WriteInt32LittleEndian(payload, 16, EdgeCount);
                WriteInt32LittleEndian(payload, 20, _pendingIterations);
                WriteInt32LittleEndian(payload, 24, telemetryCount);

                int cursor = DumpHeaderBytes;
                for (int i = 0; i < telemetryCount; i++)
                {
                    WriteThermalTelemetryEntry(payload, cursor, telemetry[i]);
                    cursor += DumpTelemetryEntryBytes;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, (int)totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SubmarineOsThermalGridRuntime),
                    dumpPayloadLabel);
            }
        }

        private static void WriteThermalTelemetryEntry(
            NativeArray<byte> destination,
            int offset,
            ThermalPowerGridTelemetrySnapshot entry)
        {
            WriteUInt64LittleEndian(destination, offset, entry.StateHash);
            WriteUInt32LittleEndian(destination, offset + 8, entry.Frame);
            WriteUInt32LittleEndian(destination, offset + 12, entry.Flags);
            WriteFloat32LittleEndian(destination, offset + 16, entry.TotalGeneratedPower);
            WriteFloat32LittleEndian(destination, offset + 20, entry.TotalLoad);
            WriteFloat32LittleEndian(destination, offset + 24, entry.MaximumThermalStress);
            WriteFloat32LittleEndian(destination, offset + 28, entry.JacobiResidual);
            WriteInt32LittleEndian(destination, offset + 32, entry.IterationCount);
            WriteInt32LittleEndian(destination, offset + 36, entry.NodeCount);
            WriteInt32LittleEndian(destination, offset + 40, entry.EdgeCount);
            WriteInt32LittleEndian(destination, offset + 44, entry.MicroDamageCount);
            WriteInt32LittleEndian(destination, offset + 48, entry.BrownoutCount);
            WriteInt32LittleEndian(destination, offset + 52, entry.ExternalHeatNodeCount);
            WriteFloat32LittleEndian(destination, offset + 56, entry.SolverOmega);
            WriteFloat32LittleEndian(destination, offset + 60, entry.TargetTolerance);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static SubmarineThermalGridTuningDTO SanitizeTuning(in SubmarineThermalGridTuningDTO tuning)
        {
            return new SubmarineThermalGridTuningDTO
            {
                BaseResistance = math.max(Epsilon, FiniteOr(tuning.BaseResistance, 0.06f)),
                ThermalDissipationRate = math.clamp(FiniteOr(tuning.ThermalDissipationRate, 0.18f), 0f, 10f),
                JacobiTolerance = math.max(Epsilon, FiniteOr(tuning.JacobiTolerance, 0.001f)),
                DamageThreshold = math.clamp(FiniteOr(tuning.DamageThreshold, 0.72f), Epsilon, 10f),
                CriticalThermalThreshold = math.clamp(FiniteOr(tuning.CriticalThermalThreshold, 1f), Epsilon, 20f),
                HeatGainScale = math.clamp(FiniteOr(tuning.HeatGainScale, 0.018f), 0f, 10f),
                ResistanceDriftRate = math.clamp(FiniteOr(tuning.ResistanceDriftRate, 0.025f), 0f, 10f),
                ExternalHeatScale = math.clamp(FiniteOr(tuning.ExternalHeatScale, 0.12f), 0f, 10f),
                BrownoutVoltageThreshold = math.clamp(FiniteOr(tuning.BrownoutVoltageThreshold, 0.2f), 0f, 1f),
                FlickerScale = math.clamp(FiniteOr(tuning.FlickerScale, 0.35f), 0f, 4f),
                VisualOverkillScalar = math.saturate(FiniteOr(tuning.VisualOverkillScalar, 0.5f)),
                SimulationTickDeltaSeconds = math.max(Epsilon, FiniteOr(tuning.SimulationTickDeltaSeconds, 0.05f)),
                CsvRevision = tuning.CsvRevision,
                Flags = tuning.Flags,
                BaseOmegaFactor = math.clamp(FiniteOr(tuning.BaseOmegaFactor, 1f), 0.25f, 1.1f),
                ToleranceMultiplier = math.clamp(FiniteOr(tuning.ToleranceMultiplier, 1f), 0.1f, 64f)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static float ResolveThermalStateSignalHeat01()
        {
            ReadOnlySpan<ThermalStateChangedSignal> signals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            byte maxSeverity = 0;
            short maxTemperatureTenths = short.MinValue;
            uint actionMask = 0u;
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ThermalStateChangedSignal signal = ref signals[i];
                maxSeverity = signal.Severity > maxSeverity ? signal.Severity : maxSeverity;
                if (signal.TemperatureTenthsCelsius != short.MinValue &&
                    signal.TemperatureTenthsCelsius > maxTemperatureTenths)
                {
                    maxTemperatureTenths = signal.TemperatureTenthsCelsius;
                }

                actionMask |= signal.ActionMask;
            }

            float severity01 = math.saturate(maxSeverity / 3f);
            float temperature01 = maxTemperatureTenths == short.MinValue
                ? 0f
                : math.saturate((maxTemperatureTenths - 390f) / math.max(1f, 480f - 390f));
            float action01 = (actionMask == 0u ? 0f : 1f) * 0.125f;
            return math.saturate(math.max(severity01, temperature01) + action01);
        }

        private static int UnsafeFieldOffset<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        [Flags]
        private enum SubmarineThermalGridFaultFlags
        {
            None = 0,
            NonFinite = 1 << 0,
            CriticalThermalFailure = 1 << 1,
            AuthoringImportFault = 1 << 2,
            Divergent = 1 << 3,
            MaxIterations = 1 << 4
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ClearActiveRangeJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* NodesA;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* NodesB;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* Edges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Injections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* ExternalHeat;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* Anchors;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridVisualStateDTO* VisualState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverConvergenceStateDTO* ConvergenceState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverResidualSlot64* ResidualSamples;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* PendingNodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* PendingEdges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* PendingInjections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* PendingAnchors;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridVisualStateDTO* PendingVisualState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* PendingCounters;
            public int Count;
            public int EdgeCount;
            public int ResidualSlotCount;
            public int CounterCount;

            public void Execute(int index)
            {
                if ((uint)index < (uint)Count)
                {
                    NodesA[index] = default;
                    NodesB[index] = default;
                    Injections[index] = 0f;
                    ExternalHeat[index] = 0f;
                    Anchors[index] = default;
                    VisualState[index] = default;
                    PendingNodes[index] = default;
                    PendingInjections[index] = 0f;
                    PendingAnchors[index] = default;
                    PendingVisualState[index] = default;
                }

                int residualSlotCount = math.clamp(ResidualSlotCount, 1, ResidualThreadSlotCount);
                if ((uint)index < (uint)residualSlotCount)
                    ResidualSamples[index] = default;

                if ((uint)index < (uint)EdgeCount)
                {
                    Edges[index] = default;
                    PendingEdges[index] = default;
                }

                if ((uint)index < (uint)CounterCount)
                    PendingCounters[index] = 0;
                if (index == 0)
                    ConvergenceState[0] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct TopologySnapshotRebuildJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* SourceNodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* SourceEdges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* SourceInjections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* SourceAnchors;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* PendingNodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* PendingEdges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* PendingInjections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* PendingAnchors;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridVisualStateDTO* PendingVisualState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* PendingCounters;
            public int NodeCount;
            public int EdgeCount;

            public void Execute(int index)
            {
                if ((uint)index < (uint)NodeCount)
                {
                    GridNodeDTO node = SourceNodes[index];
                    node.Potential = math.saturate(FiniteOr(node.Potential, 0f));
                    node.Resistance = math.max(Epsilon, FiniteOr(node.Resistance, Epsilon));
                    node.ThermalLoad = math.max(0f, FiniteOr(node.ThermalLoad, 0f));
                    node.AdjacencyOffset = math.clamp(node.AdjacencyOffset, 0, EdgeCount);
                    node.AdjacencyCount = math.clamp(node.AdjacencyCount, 0, math.max(0, EdgeCount - node.AdjacencyOffset));
                    PendingNodes[index] = node;
                    PendingInjections[index] = FiniteOr(SourceInjections[index], 0f);
                    PendingAnchors[index] = SourceAnchors[index];
                    PendingVisualState[index] = new ThermalGridVisualStateDTO
                    {
                        NodeHash = node.NodeHash,
                        Voltage01 = node.Potential,
                        Thermal01 = math.saturate(node.ThermalLoad),
                        Flags = node.Flags
                    };
                }

                if ((uint)index < (uint)EdgeCount)
                {
                    PowerEdgeDTO edge = SourceEdges[index];
                    edge.TargetIndex = (uint)edge.TargetIndex < (uint)NodeCount ? edge.TargetIndex : 0;
                    edge.Conductance = math.max(0f, FiniteOr(edge.Conductance, 0f));
                    PendingEdges[index] = edge;
                }

                if (index == CounterNodeCount)
                    PendingCounters[CounterNodeCount] = NodeCount;
                else if (index == CounterEdgeCount)
                    PendingCounters[CounterEdgeCount] = EdgeCount;
                else if (index == CounterFaultFlags)
                    PendingCounters[CounterFaultFlags] = 0;
                else if (index == CounterDumpedFaultMask)
                    PendingCounters[CounterDumpedFaultMask] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EmergencyMockGridJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* Nodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* Edges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Injections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* Anchors;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridVisualStateDTO* VisualState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* Counters;
            public SubmarineThermalGridTuningDTO Tuning;
            public int NodeCount;
            public float Oscillator01;

            public void Execute(int index)
            {
                int nodeCount = math.clamp(NodeCount, 1, EmergencyMockNodeCount);
                if ((uint)index >= (uint)nodeCount)
                    return;

                uint hash = HashNode(index);
                float oscillator = math.saturate(FiniteOr(Oscillator01, 0f));
                int edgeOffset;
                int edgeCount;
                if (index == 0)
                {
                    edgeOffset = 0;
                    edgeCount = nodeCount > 1 ? 1 : 0;
                    if (edgeCount > 0)
                        Edges[0] = new PowerEdgeDTO { TargetIndex = 1, Conductance = ResolveMockConductance(index, oscillator) };
                }
                else if (index + 1 == nodeCount)
                {
                    edgeOffset = 1 + (index - 1) * 2;
                    edgeCount = 1;
                    Edges[edgeOffset] = new PowerEdgeDTO { TargetIndex = index - 1, Conductance = ResolveMockConductance(index, oscillator) };
                }
                else
                {
                    edgeOffset = 1 + (index - 1) * 2;
                    edgeCount = 2;
                    Edges[edgeOffset] = new PowerEdgeDTO { TargetIndex = index - 1, Conductance = ResolveMockConductance(index, oscillator) };
                    Edges[edgeOffset + 1] = new PowerEdgeDTO { TargetIndex = index + 1, Conductance = ResolveMockConductance(index + 1, oscillator) };
                }

                float parity = (index & 1) == 0 ? 1f : -1f;
                float stablePotential = index == 0 ? 1f : math.saturate(1f - index * 0.006f);
                float oscillatorPotential = index == 0 ? 1f : math.saturate(0.5f + parity * 0.45f);
                float potential = math.lerp(stablePotential, oscillatorPotential, oscillator);
                uint flags = index == 0 ? SubmarineThermalGridStatusFlags.Source : SubmarineThermalGridStatusFlags.None;
                GridNodeDTO node = new GridNodeDTO
                {
                    NodeHash = hash,
                    Potential = potential,
                    Resistance = math.max(Epsilon, Tuning.BaseResistance),
                    ThermalLoad = 0f,
                    Flags = flags,
                    AdjacencyOffset = edgeOffset,
                    AdjacencyCount = edgeCount
                };
                Nodes[index] = node;
                float stableInjection = index == 0 ? 0.5f : -0.0025f;
                float oscillatorInjection = index == 0 ? 1.35f : parity * 0.85f;
                Injections[index] = math.lerp(stableInjection, oscillatorInjection, oscillator);
                Anchors[index] = new ThermalGridAnchorDTO
                {
                    LocalOffset = new float3(index * 1.5f, 0f, 0f),
                    NodeHash = hash
                };
                VisualState[index] = new ThermalGridVisualStateDTO
                {
                    NodeHash = hash,
                    Voltage01 = potential,
                    Thermal01 = 0f,
                    FlickerPhase01 = 0f,
                    Flags = flags
                };

                if (index == 0)
                {
                    Counters[CounterNodeCount] = nodeCount;
                    Counters[CounterEdgeCount] = math.max(0, (nodeCount - 1) * 2);
                    Counters[CounterFaultFlags] = 0;
                    Counters[CounterMaxIterationStreak] = 0;
                    Counters[CounterDumpedFaultMask] = 0;
                }
            }

            private static uint HashNode(int index)
            {
                uint hash = 2166136261u;
                hash = (hash ^ SourceHash) * 16777619u;
                hash = (hash ^ (uint)index) * 16777619u;
                return hash;
            }

            private static float ResolveMockConductance(int index, float oscillator)
            {
                float parityConductance = (index & 1) == 0 ? 0.18f : 2.4f;
                return math.lerp(1f, parityConductance, oscillator);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ThermalStateSignalInjectionJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* ExternalHeat;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* Anchors;
            public int NodeCount;
            public float SignalHeat01;
            public float GlobalQualityWeight;

            public void Execute(int nodeIndex)
            {
                if ((uint)nodeIndex >= (uint)NodeCount)
                    return;

                float heat01 = math.saturate(math.select(0f, SignalHeat01, math.isfinite(SignalHeat01)));
                float quality = math.saturate(math.select(0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                float3 localOffset = Anchors[nodeIndex].LocalOffset;
                float distanceSq = math.lengthsq(localOffset);
                float radial01 = 1f - math.saturate(distanceSq / math.max(1f, 900f));
                float smoothRadial = radial01 * radial01 * (3f - 2f * radial01);
                float lowTierUniform = math.saturate(heat01 * math.rcp(Epsilon));
                float shape = math.lerp(lowTierUniform, smoothRadial, quality);
                float ambientHeat = heat01 * math.lerp(0.55f, 1f, shape);
                ExternalHeat[nodeIndex] = math.max(ExternalHeat[nodeIndex], ambientHeat);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct InitializeSolverConvergenceJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverConvergenceStateDTO* ConvergenceState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverResidualSlot64* ResidualSamples;
            public int ResidualSlotCount;
            public float BaseOmega;

            public void Execute(int nodeIndex)
            {
                int slotCount = math.clamp(ResidualSlotCount, 1, ResidualThreadSlotCount);
                if ((uint)nodeIndex < (uint)slotCount)
                    ResidualSamples[nodeIndex] = default;
                if (nodeIndex == 0)
                {
                    ConvergenceState[0] = new SolverConvergenceStateDTO
                    {
                        MaxResidualFloat = 0f,
                        PreviousResidualFloat = 0f,
                        Omega = math.clamp(FiniteOr(BaseOmega, 1f), 0.55f, 1f),
                        IterationCount = 0,
                        FaultFlags = SolverConvergenceFaultFlags.None
                    };
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ClearSolverResidualSlotsJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverResidualSlot64* ResidualSamples;
            public int ResidualSlotCount;

            public void Execute(int index)
            {
                int slotCount = math.clamp(ResidualSlotCount, 1, ResidualThreadSlotCount);
                if ((uint)index < (uint)slotCount)
                    ResidualSamples[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ConvergenceResidualReductionJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverConvergenceStateDTO* ConvergenceState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverResidualSlot64* ResidualSamples;
            public float TargetTolerance;
            public float BaseOmega;
            public int ResidualSlotCount;
            public byte FinalIteration;

            public void Execute()
            {
                ref SolverConvergenceStateDTO state = ref UnsafeUtility.AsRef<SolverConvergenceStateDTO>(ConvergenceState);
                ushort flags = state.FaultFlags;
                const ushort terminalFlags = (ushort)(
                    SolverConvergenceFaultFlags.Converged |
                    SolverConvergenceFaultFlags.Divergent |
                    SolverConvergenceFaultFlags.NonFinite);
                if ((flags & terminalFlags) != 0)
                    return;

                float maxResidual = 0f;
                bool nonFiniteResidual = false;
                int slotCount = math.clamp(ResidualSlotCount, 1, ResidualThreadSlotCount);
                for (int i = 0; i < slotCount; i++)
                {
                    SolverResidualSlot64 slot = ResidualSamples[i];
                    float residual = slot.MaxResidualFloat;
                    if ((slot.FaultFlags & ResidualSlotFaultNonFinite) != 0u ||
                        !math.isfinite(residual) ||
                        residual >= float.MaxValue * 0.5f)
                    {
                        nonFiniteResidual = true;
                        maxResidual = math.max(maxResidual, 1f);
                        break;
                    }

                    maxResidual = math.max(maxResidual, math.max(0f, residual));
                }

                float tolerance = math.max(Epsilon * 0.25f, FiniteOr(TargetTolerance, 0.001f));
                float baseOmega = math.clamp(FiniteOr(BaseOmega, 1f), 0.55f, 1f);
                float previous = FiniteOr(state.PreviousResidualFloat, maxResidual);
                bool previousValid = state.IterationCount > 0 && previous < float.MaxValue * 0.5f;
                bool grew = previousValid && maxResidual > math.max(previous + tolerance * 0.25f, previous * 1.08f);
                bool runaway = previousValid && maxResidual > math.max(0.5f, previous * 2f);
                float omega = math.clamp(FiniteOr(state.Omega, baseOmega), 0.55f, 1f);

                if (nonFiniteResidual)
                {
                    flags = (ushort)(flags | SolverConvergenceFaultFlags.NonFinite | SolverConvergenceFaultFlags.Divergent);
                    omega = 0.55f;
                }
                else if (runaway)
                {
                    flags = (ushort)(flags | SolverConvergenceFaultFlags.Divergent);
                    omega = 0.55f;
                }
                else if (grew)
                {
                    omega = math.max(0.55f, omega * 0.86f);
                }
                else
                {
                    omega = math.min(baseOmega, omega + (baseOmega - omega) * 0.125f);
                }

                if (!nonFiniteResidual && maxResidual <= tolerance)
                    flags = (ushort)(flags | SolverConvergenceFaultFlags.Converged);
                else if (FinalIteration != 0)
                    flags = (ushort)(flags | SolverConvergenceFaultFlags.MaxIterations);

                state.MaxResidualFloat = maxResidual;
                state.PreviousResidualFloat = maxResidual;
                state.Omega = omega;
                state.IterationCount = (ushort)math.min(ushort.MaxValue, state.IterationCount + 1);
                state.FaultFlags = flags;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct PowerGridRelaxationJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* NodesRead;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* NodesWrite;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* Edges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Injections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* ExternalHeat;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridVisualStateDTO* VisualState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverConvergenceStateDTO* ConvergenceState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverResidualSlot64* ResidualSamples;
            public SubmarineThermalGridTuningDTO Tuning;
            public int NodeCount;
            public int EdgeCount;
            public float DeltaSeconds;
            public uint Frame;
            public int IterationIndex;
            public int IterationCount;
            public float TargetTolerance;
            public int ResidualSampleMask;
            public int ResidualSlotCount;
            [NativeSetThreadIndex] public int ThreadIndex;

            public void Execute(int nodeIndex)
            {
                if ((uint)nodeIndex >= (uint)NodeCount)
                    return;

                ref GridNodeDTO source = ref UnsafeUtility.AsRef<GridNodeDTO>(NodesRead + nodeIndex);
                ref GridNodeDTO target = ref UnsafeUtility.AsRef<GridNodeDTO>(NodesWrite + nodeIndex);
                SolverConvergenceStateDTO convergenceState = ConvergenceState[0];
                const ushort terminalFlags = (ushort)(
                    SolverConvergenceFaultFlags.Converged |
                    SolverConvergenceFaultFlags.Divergent |
                    SolverConvergenceFaultFlags.NonFinite);
                if ((convergenceState.FaultFlags & terminalFlags) != 0)
                {
                    target = source;
                    return;
                }

                float sourcePotential = math.saturate(FiniteOr(source.Potential, 0f));
                float resistance = math.max(Epsilon, FiniteOr(source.Resistance, Tuning.BaseResistance));
                float thermalDeltaSeconds = math.max(0f, DeltaSeconds) / math.max(1, IterationCount);
                float heatGainScale = math.clamp(FiniteOr(Tuning.HeatGainScale, 0.018f), 0f, 10f);
                float externalHeatScale = math.clamp(FiniteOr(Tuning.ExternalHeatScale, 0.12f), 0f, 10f);
                float thermalDissipationRate = math.clamp(FiniteOr(Tuning.ThermalDissipationRate, 0.18f), 0f, 10f);
                float visualOverkill = math.saturate(FiniteOr(Tuning.VisualOverkillScalar, 0.5f));
                float resistanceDriftRate = math.clamp(FiniteOr(Tuning.ResistanceDriftRate, 0.025f), 0f, 10f);
                float flickerScale = math.clamp(FiniteOr(Tuning.FlickerScale, 0.35f), 0f, 4f);
                float brownoutThreshold = math.saturate(FiniteOr(Tuning.BrownoutVoltageThreshold, 0.2f));
                float weightedPotential = 0f;
                float conductanceSum = 0f;
                float thermalLoad = math.max(0f, FiniteOr(source.ThermalLoad, 0f));
                uint flags = source.Flags & ~(SubmarineThermalGridStatusFlags.Brownout | SubmarineThermalGridStatusFlags.Overheating | SubmarineThermalGridStatusFlags.ExternalHeat);
                bool isolated = (source.Flags & (SubmarineThermalGridStatusFlags.Isolated | SubmarineThermalGridStatusFlags.ShortCircuit)) != 0;
                const float ExternalHeatRetention = 0.55f;

                int edgeStart = math.clamp(source.AdjacencyOffset, 0, EdgeCount);
                int edgeEnd = math.clamp(edgeStart + math.max(0, source.AdjacencyCount), edgeStart, EdgeCount);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    PowerEdgeDTO edge = Edges[edgeIndex];
                    int targetIndex = edge.TargetIndex;
                    if ((uint)targetIndex >= (uint)NodeCount)
                        continue;

                    float conductance = math.max(0f, FiniteOr(edge.Conductance, 0f));
                    if (conductance <= Epsilon)
                        continue;

                    GridNodeDTO neighbor = NodesRead[targetIndex];
                    if ((neighbor.Flags & (SubmarineThermalGridStatusFlags.Isolated | SubmarineThermalGridStatusFlags.ShortCircuit)) != 0)
                        continue;

                    float neighborPotential = math.saturate(FiniteOr(neighbor.Potential, 0f));
                    float voltageDrop = sourcePotential - neighborPotential;
                    float current = voltageDrop * conductance;
                    thermalLoad += current * current * resistance * thermalDeltaSeconds * heatGainScale;
                    weightedPotential += conductance * neighborPotential;
                    conductanceSum += conductance;
                }

                float externalHeat = math.max(0f, FiniteOr(ExternalHeat[nodeIndex], 0f));
                if (IterationIndex == 0)
                    ExternalHeat[nodeIndex] = externalHeat * ExternalHeatRetention;
                if (externalHeat > Epsilon)
                    flags |= SubmarineThermalGridStatusFlags.ExternalHeat;
                thermalLoad += externalHeat * externalHeatScale * thermalDeltaSeconds;
                thermalLoad = math.max(0f, thermalLoad - thermalDissipationRate * thermalDeltaSeconds);

                float nextPotential;
                if (isolated)
                {
                    nextPotential = 0f;
                }
                else if ((source.Flags & SubmarineThermalGridStatusFlags.Source) != 0)
                {
                    nextPotential = 1f;
                }
                else
                {
                    float injection = FiniteOr(Injections[nodeIndex], 0f);
                    float denominator = math.max(conductanceSum, Epsilon);
                    nextPotential = (weightedPotential + injection) / denominator;
                }

                float omega = math.clamp(FiniteOr(convergenceState.Omega, 1f), 0.55f, 1f);
                bool sourceAnchored = isolated || (source.Flags & SubmarineThermalGridStatusFlags.Source) != 0;
                if (!sourceAnchored)
                    nextPotential = sourcePotential + (nextPotential - sourcePotential) * omega;

                bool potentialFault = !math.isfinite(nextPotential) || math.abs(nextPotential) > 16f;
                if (potentialFault)
                {
                    nextPotential = sourcePotential;
                    flags |= SubmarineThermalGridStatusFlags.FaultDivergent;
                }

                nextPotential = math.saturate(FiniteOr(nextPotential, sourcePotential));
                float damageThreshold = math.max(Epsilon, FiniteOr(Tuning.DamageThreshold, 0.72f));
                float criticalThreshold = math.max(damageThreshold + Epsilon, FiniteOr(Tuning.CriticalThermalThreshold, 1f));
                if (thermalLoad >= damageThreshold)
                {
                    flags |= SubmarineThermalGridStatusFlags.Overheating;
                    float overheat01 = math.saturate((thermalLoad - damageThreshold) / math.max(Epsilon, criticalThreshold - damageThreshold));
                    resistance = math.min(16f, resistance * (1f + resistanceDriftRate * thermalDeltaSeconds * overheat01));
                    nextPotential *= math.lerp(1f, 0.35f, overheat01);
                    if (thermalLoad >= criticalThreshold)
                        flags |= SubmarineThermalGridStatusFlags.MicroDamage | SubmarineThermalGridStatusFlags.ShortCircuit;
                }

                if (nextPotential < brownoutThreshold)
                    flags |= SubmarineThermalGridStatusFlags.Brownout;

                target = source;
                target.Potential = nextPotential;
                target.Resistance = resistance;
                target.ThermalLoad = FiniteOr(thermalLoad, 0f);
                target.Flags = flags;

                float residual = math.abs(nextPotential - sourcePotential);
                float sampledResidual = math.max(0f, FiniteOr(residual, 1f));
                if (potentialFault)
                    sampledResidual = math.max(sampledResidual, 1f);
                if (sampledResidual > 0f)
                {
                    int slotCount = math.clamp(ResidualSlotCount, 1, ResidualThreadSlotCount);
                    int slot = math.clamp(ThreadIndex, 0, slotCount - 1);
                    ref SolverResidualSlot64 slotRef = ref UnsafeUtility.AsRef<SolverResidualSlot64>(ResidualSamples + slot);
                    slotRef.MaxResidualFloat = math.max(slotRef.MaxResidualFloat, sampledResidual);
                    if (potentialFault)
                        slotRef.FaultFlags |= ResidualSlotFaultNonFinite;
                }

                float thermal01 = math.saturate(target.ThermalLoad / criticalThreshold);
                float flicker = Triangle01((Frame * 0.017f) + (nodeIndex * 0.071f) + IterationIndex * 0.013f);
                flicker *= math.saturate(1f - nextPotential) * flickerScale;
                VisualState[nodeIndex] = new ThermalGridVisualStateDTO
                {
                    NodeHash = target.NodeHash,
                    Voltage01 = nextPotential,
                    Thermal01 = thermal01,
                    FlickerPhase01 = flicker,
                    Flags = flags,
                    VisualOverkill01 = visualOverkill
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ShortCircuitIsolationJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* Nodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* Edges;
            public int NodeCount;

            public void Execute(int nodeIndex)
            {
                if ((uint)nodeIndex >= (uint)NodeCount)
                    return;

                GridNodeDTO node = Nodes[nodeIndex];
                bool isolateSource = (node.Flags & (SubmarineThermalGridStatusFlags.MicroDamage | SubmarineThermalGridStatusFlags.Isolated | SubmarineThermalGridStatusFlags.ShortCircuit)) != 0;
                int edgeStart = node.AdjacencyOffset;
                int edgeEnd = edgeStart + math.max(0, node.AdjacencyCount);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    PowerEdgeDTO edge = Edges[edgeIndex];
                    bool isolateEdge = isolateSource;
                    if ((uint)edge.TargetIndex < (uint)NodeCount)
                    {
                        GridNodeDTO target = Nodes[edge.TargetIndex];
                        isolateEdge |= (target.Flags & (SubmarineThermalGridStatusFlags.MicroDamage | SubmarineThermalGridStatusFlags.Isolated | SubmarineThermalGridStatusFlags.ShortCircuit)) != 0;
                    }

                    if (isolateEdge)
                    {
                        edge.Conductance = 0f;
                        Edges[edgeIndex] = edge;
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ExternalThermalInjectionJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* ExternalHeat;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalGridAnchorDTO* Anchors;
            public int NodeCount;
            public double3 SubmarineBaseAup;
            public double3 HazardAup;
            public float HazardTemperatureCelsius;
            public float HazardRadiusMeters;

            public void Execute(int nodeIndex)
            {
                if ((uint)nodeIndex >= (uint)NodeCount)
                    return;

                float3 local = Anchors[nodeIndex].LocalOffset;
                double3 nodeAup = SubmarineBaseAup + new double3(local.x, local.y, local.z);
                double distanceSqDouble = AupPrecisionMath.DistanceSqSafeDouble(HazardAup, nodeAup);
                float distanceSq = distanceSqDouble >= float.MaxValue ? float.MaxValue : (float)distanceSqDouble;
                float radius = math.max(1f, HazardRadiusMeters);
                float near01 = math.saturate(1f - distanceSq / math.max(Epsilon, radius * radius));
                float sample01 = near01 * near01 * (3f - 2f * near01);
                float hazardTemperature = FiniteOr(HazardTemperatureCelsius, 40f);
                ExternalHeat[nodeIndex] = math.max(0f, hazardTemperature - 40f) * 0.01f * sample01;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ThermalGridTelemetryJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* Nodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public GridNodeDTO* PreviousNodes;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public PowerEdgeDTO* Edges;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Injections;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ThermalPowerGridTelemetrySnapshot* Telemetry;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SolverConvergenceStateDTO* ConvergenceState;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* Counters;
            public int NodeCount;
            public int EdgeCount;
            public int Iterations;
            public uint Frame;
            public float TargetTolerance;

            public void Execute()
            {
                float totalGenerated = 0f;
                float totalLoad = 0f;
                float maxThermal = 0f;
                float residual = 0f;
                int microDamage = 0;
                int brownout = 0;
                int externalHeat = 0;
                ulong hash = 1469598103934665603UL;
                uint flags = 0u;

                for (int i = 0; i < NodeCount; i++)
                {
                    GridNodeDTO node = Nodes[i];
                    float injection = Injections[i];
                    if (injection > 0f)
                        totalGenerated += injection;
                    else
                        totalLoad += -injection;
                    maxThermal = math.max(maxThermal, math.max(0f, node.ThermalLoad));
                    residual = math.max(residual, math.abs(node.Potential - PreviousNodes[i].Potential));
                    if ((node.Flags & SubmarineThermalGridStatusFlags.MicroDamage) != 0)
                        microDamage++;
                    if ((node.Flags & SubmarineThermalGridStatusFlags.Brownout) != 0)
                        brownout++;
                    if ((node.Flags & SubmarineThermalGridStatusFlags.ExternalHeat) != 0)
                        externalHeat++;
                    if ((node.Flags & SubmarineThermalGridStatusFlags.FaultDivergent) != 0)
                        flags |= (uint)SubmarineThermalGridFaultFlags.Divergent;
                    if (!math.isfinite(node.Potential) || !math.isfinite(node.ThermalLoad) || !math.isfinite(node.Resistance))
                        flags |= (uint)SubmarineThermalGridFaultFlags.NonFinite;
                    hash = HashNode(hash, node);
                }

                SolverConvergenceStateDTO convergenceState = ConvergenceState[0];
                if (math.isfinite(convergenceState.MaxResidualFloat))
                    residual = math.max(residual, math.max(0f, convergenceState.MaxResidualFloat));
                else
                    flags |= (uint)SubmarineThermalGridFaultFlags.NonFinite;
                if ((convergenceState.FaultFlags & SolverConvergenceFaultFlags.NonFinite) != 0)
                    flags |= (uint)SubmarineThermalGridFaultFlags.NonFinite;
                if ((convergenceState.FaultFlags & SolverConvergenceFaultFlags.Divergent) != 0)
                    flags |= (uint)SubmarineThermalGridFaultFlags.Divergent;
                bool maxIterationFault = (convergenceState.FaultFlags & SolverConvergenceFaultFlags.MaxIterations) != 0 &&
                                         residual > math.max(Epsilon, TargetTolerance);
                if (maxIterationFault)
                {
                    int streak = math.min(1000000, Counters[CounterMaxIterationStreak] + 1);
                    Counters[CounterMaxIterationStreak] = streak;
                    if (streak >= 5)
                        flags |= (uint)SubmarineThermalGridFaultFlags.MaxIterations;
                }
                else
                {
                    Counters[CounterMaxIterationStreak] = 0;
                }

                if (maxThermal >= 1f || microDamage > 0)
                    flags |= (uint)SubmarineThermalGridFaultFlags.CriticalThermalFailure;
                if ((flags & (uint)SubmarineThermalGridFaultFlags.NonFinite) != 0)
                    Counters[CounterFaultFlags] |= (int)SubmarineThermalGridFaultFlags.NonFinite;
                if ((flags & (uint)SubmarineThermalGridFaultFlags.CriticalThermalFailure) != 0)
                    Counters[CounterFaultFlags] |= (int)SubmarineThermalGridFaultFlags.CriticalThermalFailure;
                if ((flags & (uint)SubmarineThermalGridFaultFlags.Divergent) != 0)
                    Counters[CounterFaultFlags] |= (int)SubmarineThermalGridFaultFlags.Divergent;
                if ((flags & (uint)SubmarineThermalGridFaultFlags.MaxIterations) != 0)
                    Counters[CounterFaultFlags] |= (int)SubmarineThermalGridFaultFlags.MaxIterations;

                int cursor = Counters[CounterTelemetryCursor];
                int writeIndex = math.abs(cursor) % TelemetryFrameCount;
                int actualIterations = convergenceState.IterationCount > 0 ? convergenceState.IterationCount : Iterations;
                Telemetry[writeIndex] = new ThermalPowerGridTelemetrySnapshot
                {
                    StateHash = hash,
                    Frame = Frame,
                    Flags = flags,
                    TotalGeneratedPower = totalGenerated,
                    TotalLoad = totalLoad,
                    MaximumThermalStress = maxThermal,
                    JacobiResidual = residual,
                    IterationCount = actualIterations,
                    NodeCount = NodeCount,
                    EdgeCount = EdgeCount,
                    MicroDamageCount = microDamage,
                    BrownoutCount = brownout,
                    ExternalHeatNodeCount = externalHeat,
                    SolverOmega = math.clamp(FiniteOr(convergenceState.Omega, 1f), 0.55f, 1f),
                    TargetTolerance = math.max(Epsilon * 0.25f, FiniteOr(TargetTolerance, 0.001f))
                };
                Counters[CounterTelemetryCursor] = (cursor + 1) % TelemetryFrameCount;
            }

            private static ulong HashNode(ulong hash, in GridNodeDTO node)
            {
                hash = (hash ^ node.NodeHash) * 1099511628211UL;
                hash = (hash ^ math.asuint(node.Potential)) * 1099511628211UL;
                hash = (hash ^ math.asuint(node.ThermalLoad)) * 1099511628211UL;
                hash = (hash ^ node.Flags) * 1099511628211UL;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Triangle01(float value)
        {
            float t = math.frac(value);
            return 1f - math.abs(t * 2f - 1f);
        }
    }

#if UNITY_EDITOR
    public static class SubmarineThermalGridCsvParser
    {
        public static int ParseGridSpecsCsv(
            ReadOnlySpan<byte> csv,
            NativeArray<SubmarineGridSpecDTO> specs,
            NativeArray<SubmarineThermalGridTuningDTO> tuning,
            int existingCount)
        {
            if (csv.Length <= 0 || !specs.IsCreated)
                return math.max(0, existingCount);

            int writeIndex = math.clamp(existingCount, 0, specs.Length);
            int index = 0;
            while (index < csv.Length && writeIndex < specs.Length)
            {
                SkipLineNoise(csv, ref index);
                if (index >= csv.Length)
                    break;

                int nameStart = index;
                int nameLength = ReadToken(csv, ref index);
                if (nameLength <= 0)
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                if (IsHeaderName(csv, nameStart, nameLength))
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                float conductance = ReadFloat(csv, ref index);
                float thermalLimit = ReadFloat(csv, ref index);
                float resistance = ReadFloat(csv, ref index);
                float externalHeatScale = ReadFloat(csv, ref index);
                SkipLine(csv, ref index);

                uint hash = HashToken(csv, nameStart, nameLength);
                specs[writeIndex++] = new SubmarineGridSpecDTO
                {
                    ComponentHash = hash,
                    BaseConductance = math.max(0f, conductance),
                    ThermalLimit = math.max(0.001f, thermalLimit),
                    BaseResistance = math.max(0.0001f, resistance),
                    ExternalHeatScale = math.max(0f, externalHeatScale)
                };
            }

            if (tuning.IsCreated && tuning.Length > 0)
            {
                SubmarineThermalGridTuningDTO value = tuning[0];
                value.CsvRevision = unchecked(value.CsvRevision + 1u);
                tuning[0] = value;
            }

            return writeIndex;
        }

        public static int ParseRelaxationProfilesCsv(
            ReadOnlySpan<byte> csv,
            NativeArray<SubmarineThermalGridTuningDTO> tuning,
            uint systemHash)
        {
            if (csv.Length <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return 0;

            int applied = 0;
            int index = 0;
            while (index < csv.Length)
            {
                SkipLineNoise(csv, ref index);
                if (index >= csv.Length)
                    break;

                int nameStart = index;
                int nameLength = ReadToken(csv, ref index);
                if (nameLength <= 0)
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                if (IsHeaderName(csv, nameStart, nameLength))
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                float tolerance = ReadFloat(csv, ref index);
                float omegaFactor = ReadFloat(csv, ref index);
                float toleranceMultiplier = ReadFloat(csv, ref index);
                SkipLine(csv, ref index);

                uint rowHash = HashToken(csv, nameStart, nameLength);
                if (systemHash != 0u && rowHash != systemHash)
                    continue;

                SubmarineThermalGridTuningDTO value = tuning[0];
                value.JacobiTolerance = math.max(0.0001f, tolerance);
                value.BaseOmegaFactor = math.clamp(omegaFactor <= 0f ? 1f : omegaFactor, 0.25f, 1.1f);
                value.ToleranceMultiplier = math.clamp(toleranceMultiplier <= 0f ? 1f : toleranceMultiplier, 0.1f, 64f);
                value.CsvRevision = unchecked(value.CsvRevision + 1u);
                tuning[0] = value;
                applied++;
            }

            return applied;
        }

        public static uint HashToken(ReadOnlySpan<byte> bytes, int start, int length)
        {
            uint hash = 2166136261u;
            int end = math.min(bytes.Length, start + math.max(0, length));
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value == (byte)'_' || value == (byte)' ' || value == (byte)'-')
                    continue;
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash = (hash ^ value) * 16777619u;
            }

            return hash;
        }

        private static void SkipLineNoise(ReadOnlySpan<byte> csv, ref int index)
        {
            while (index < csv.Length)
            {
                byte value = csv[index];
                if (value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n' || value == (byte)',')
                {
                    index++;
                    continue;
                }

                break;
            }
        }

        private static int ReadToken(ReadOnlySpan<byte> csv, ref int index)
        {
            int start = index;
            while (index < csv.Length)
            {
                byte value = csv[index];
                if (value == (byte)',' || value == (byte)'=' || value == (byte)'\n' || value == (byte)'\r')
                    break;
                index++;
            }

            int end = index;
            while (end > start && (csv[end - 1] == (byte)' ' || csv[end - 1] == (byte)'\t'))
                end--;
            if (index < csv.Length && (csv[index] == (byte)',' || csv[index] == (byte)'='))
                index++;
            return math.max(0, end - start);
        }

        private static float ReadFloat(ReadOnlySpan<byte> csv, ref int index)
        {
            while (index < csv.Length && (csv[index] == (byte)' ' || csv[index] == (byte)'\t' || csv[index] == (byte)','))
                index++;

            bool negative = false;
            if (index < csv.Length && csv[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            float value = 0f;
            while (index < csv.Length && csv[index] >= (byte)'0' && csv[index] <= (byte)'9')
            {
                value = value * 10f + (csv[index] - (byte)'0');
                index++;
            }

            if (index < csv.Length && csv[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < csv.Length && csv[index] >= (byte)'0' && csv[index] <= (byte)'9')
                {
                    value += (csv[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            return negative ? -value : value;
        }

        private static bool IsHeaderName(ReadOnlySpan<byte> csv, int start, int length)
        {
            return length == 4 &&
                   start + 3 < csv.Length &&
                   ToLower(csv[start]) == (byte)'n' &&
                   ToLower(csv[start + 1]) == (byte)'a' &&
                   ToLower(csv[start + 2]) == (byte)'m' &&
                   ToLower(csv[start + 3]) == (byte)'e';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static void SkipLine(ReadOnlySpan<byte> csv, ref int index)
        {
            while (index < csv.Length && csv[index] != (byte)'\n')
                index++;
            if (index < csv.Length)
                index++;
        }
    }
#endif
}
