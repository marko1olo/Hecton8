using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif
using DebugSignal = Hecton8.Core.Contracts.Signals.DebugSignal;
using DebugSignalKind = Hecton8.Core.Contracts.Signals.DebugSignalKind;

namespace Hecton8.Core.Diagnostics.Visuals
{
    [Preserve]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct ArchitectEyeQuadInstance
    {
        [FieldOffset(0)]
        public float4 CenterHalfX;
        [FieldOffset(16)]
        public float4 AxisYHalfY;
        [FieldOffset(32)]
        public float4 Color;
        [FieldOffset(48)]
        public float4 UvMode;
        [FieldOffset(64)]
        public float4 Aux;
    }

    [Preserve]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ArchitectEyeBlackBoxEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public ushort QuadCount;
        [FieldOffset(6)]
        public ushort SignalLaneCount;
        [FieldOffset(8)]
        public float SignalPressure01;
        [FieldOffset(12)]
        public float VaultPressure01;
        [FieldOffset(16)]
        public float MemoryFragmentation01;
        [FieldOffset(20)]
        public float SystemHealth01;
        [FieldOffset(24)]
        public float FrameTimeMs;
        [FieldOffset(28)]
        public int NonFiniteCount;
        [FieldOffset(32)]
        public uint KillSwitchMask;
        [FieldOffset(36)]
        public uint Flags;
        [FieldOffset(40)]
        public float3 LastFaultPosition;
        [FieldOffset(52)]
        public float GasCo201;
        [FieldOffset(56)]
        public float GasO201;
        [FieldOffset(60)]
        public float StpScale01;
    }

    [Preserve]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ArchitectEyeRuntimeState
    {
        [FieldOffset(0)]
        public int TickPhase;
        [FieldOffset(4)]
        public int BlackBoxCursor;
        [FieldOffset(8)]
        public int WaterfallCursor;
        [FieldOffset(12)]
        public int LastQuadCount;
        [FieldOffset(16)]
        public uint Flags;
        [FieldOffset(20)]
        public uint LastFrame;
        [FieldOffset(24)]
        public float LastBuildMicroseconds;
        [FieldOffset(28)]
        public float LastHealth01;
        [FieldOffset(32)]
        public float LastFrameMs;
        [FieldOffset(36)]
        public float LastStpScale01;
        [FieldOffset(40)]
        public float LastGasCo201;
        [FieldOffset(44)]
        public float LastGasO201;
        [FieldOffset(48)]
        public int LastSignalLaneCount;
        [FieldOffset(52)]
        public int LastNonFiniteCount;
        [FieldOffset(56)]
        public int Reserved0;
        [FieldOffset(60)]
        public int Reserved1;
    }

    [Preserve]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5800)]
    public sealed class ArchitectEyeVisualizer : MonoBehaviour, ISlowTickable, IRenderable, IGlobalRegistryHotSwapListener
    {
        private const int BlackBoxFrameCount = 300;
        private const int SignalLaneCapacity = 256;
        private const int SectorHashCapacity = 512;
        private const int BlackBoxEntrySizeBytes = 64;
        private const int GlyphCellPixels = 8;
        private const int GlyphAtlasColumns = 16;
        private const int GlyphAtlasRows = 8;
        private const int GlyphAtlasPixels = GlyphCellPixels * GlyphAtlasColumns * GlyphCellPixels * GlyphAtlasRows;
        private const int DefaultMaxQuads = 8192;
        private const string QuadShaderAssetPath = "Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeIndirectQuads.shader";
        public const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_ARCHITECT_SPATIAL_PROBE.bin";
        private const float ScreenDepth = 0.25f;
        private const float HashToUnit = 1f / 65535f;
        private const uint StateFlagRawStp = 1u << 0;
        private const uint StateFlagNonFinite = 1u << 1;
        private static readonly int InstancesId = Shader.PropertyToID("_H8EyeQuads");
        private static readonly int GlyphAtlasId = Shader.PropertyToID("_H8EyeGlyphAtlas");
        private static readonly int VisualTierId = Shader.PropertyToID("_H8EyeVisualTier");
        private static readonly ProfilerMarker SlowTickMarker = new ProfilerMarker("H8.Diagnostics.ArchitectEye.SlowTick");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("H8.Diagnostics.ArchitectEye.Upload");
        private static readonly Vector3[] QuadVertices =
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(-1f, 1f, 0f)
        }; // COLD ALLOC: Vector3[4] - shared indirect quad mesh vertices - owner: ArchitectEyeVisualizer
        private static readonly Vector2[] QuadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        }; // COLD ALLOC: Vector2[4] - shared indirect quad mesh UVs - owner: ArchitectEyeVisualizer
        private static readonly int[] QuadIndices =
        {
            0, 1, 2, 0, 2, 3
        }; // COLD ALLOC: int[6] - shared indirect quad mesh indices - owner: ArchitectEyeVisualizer

        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _maxQuads = DefaultMaxQuads;
        [SerializeField] private int _lowTierEntityBudget = 64;
        [SerializeField] private int _midTierEntityBudget = 128;
        [SerializeField] private int _highTierEntityBudget = 512;
        [SerializeField] private int _ultraTierEntityBudget = 1024;
        [SerializeField] private float _labelMeters = 0.18f;
        [SerializeField] private float _vectorScale = 0.08f;
        [SerializeField] private float _lineThicknessMeters = 0.025f;
        [SerializeField] private Shader _quadShader;

        private readonly Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(20000f, 20000f, 20000f));
        private readonly char[] _labelScratch = new char[128]; // COLD ALLOC: char[128] - fixed label formatting buffer - owner: ArchitectEyeVisualizer
        private readonly byte[] _glyphPixels = new byte[GlyphAtlasPixels]; // COLD ALLOC: byte[8192] - bitmap glyph atlas staging buffer - owner: ArchitectEyeVisualizer
        private Mesh _quadMesh;
        private Material _material;
        private Texture2D _glyphAtlas;
        private GraphicsBuffer _instanceBufferA;
        private GraphicsBuffer _instanceBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _argsBuffer;
        private int _bufferQuadCapacity;
        private int _gpuWriteBufferIndex;
        private int _frontCount;
        private int _pendingUploadCount;
        private int _pendingUploadCapacity;
        private bool _pendingGpuUpload;
        private bool _slowRegistered;
        private bool _renderRegistered;
        private bool _hotSwapRegistered;
        private bool _rawStpDebug;
        private bool _dumpWrittenThisFault;
        private IDataVault _dataVault;
        private IMacroDatabaseService _macroDatabase;
        private IGasDynamicsSolver _gasDynamics;
        private IResolutionScalerService _resolutionScaler;
        private VaultGenerationHandle<ArchitectEyeRuntimeState> _runtimeStateHandle;
        private VaultGenerationHandle<ArchitectEyeQuadInstance> _quadInstancesHandle;
        private VaultGenerationHandle<SignalLaneTelemetry> _signalTelemetryHandle;
        private VaultGenerationHandle<ulong> _sectorHashesHandle;
        private VaultGenerationHandle<ArchitectEyeBlackBoxEntry> _blackBoxHandle;
        private uint _lastKillSwitchMask;

        private void Awake()
        {
            if (!IsDiagnosticsRuntimeAllowed())
            {
                enabled = false;
                return;
            }

            ValidatePackedStructSizes();
            EnsureResources();
        }

        private void OnEnable()
        {
            if (!IsDiagnosticsRuntimeAllowed())
                return;

            EnsureResources();

            if (!Application.isPlaying)
                return;

            ArchitectEyeDebugBus.EnsureInitialized();
            CacheGlobalRegistryServicesCold();
            TryRegisterHotSwapListener();
            _slowRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
            _renderRegistered = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void OnDisable()
        {
            if (_slowRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _slowRegistered = false;
            }

            if (_renderRegistered)
            {
                GlobalRegistry.Renderables.TryUnregister(this);
                _renderRegistered = false;
            }

            TryUnregisterHotSwapListener();
            _dataVault = null;
            _macroDatabase = null;
            _gasDynamics = null;
            _resolutionScaler = null;
            ResetVaultDescriptors();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void CacheGlobalRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _macroDatabase = GlobalRegistry.MacroDatabase;
            _gasDynamics = GlobalRegistry.GasDynamics;
            _resolutionScaler = GlobalRegistry.ResolutionScaler;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    ResetVaultDescriptors();
                    break;
                case GlobalRegistryServiceSlot.MacroDatabase:
                    _macroDatabase = currentService as IMacroDatabaseService;
                    break;
                case GlobalRegistryServiceSlot.GasDynamicsRuntime:
                    _gasDynamics = currentService as IGasDynamicsSolver;
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _resolutionScaler = currentService as IResolutionScalerService;
                    break;
            }
        }

        private void ResetVaultDescriptors()
        {
            _runtimeStateHandle = default;
            _quadInstancesHandle = default;
            _signalTelemetryHandle = default;
            _sectorHashesHandle = default;
            _blackBoxHandle = default;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_quadShader == null)
                _quadShader = AssetDatabase.LoadAssetAtPath<Shader>(QuadShaderAssetPath);
        }
#endif

        private static void ValidatePackedStructSizes()
        {
            if (UnsafeUtility.SizeOf<ArchitectEyeQuadInstance>() != 80 ||
                UnsafeUtility.SizeOf<ArchitectEyeBlackBoxEntry>() != BlackBoxEntrySizeBytes ||
                UnsafeUtility.SizeOf<ArchitectEyeRuntimeState>() != 64 ||
                UnsafeUtility.SizeOf<DebugSignal>() != 64)
            {
                FatalMemoryException.ThrowAbiLayoutMismatch();
            }
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                    return true;
            }

            if (vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.CoreDiagnostics,
                options);

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        public void SlowTick()
        {
            using (SlowTickMarker.Auto())
            {
                SlowTickInternal();
            }
        }

        private void SlowTickInternal()
        {
            if (!_enabled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!OpenOrAcquireVaultBuffer(
                    vault,
                    ref _runtimeStateHandle,
                    BufferID.ArchitectEyeRuntimeState,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ArchitectEyeRuntimeState> stateBuffer))
            {
                return;
            }

            if (!stateBuffer.IsCreated || stateBuffer.Length == 0)
                return;

            ArchitectEyeRuntimeState state = stateBuffer[0];
            state.TickPhase++;
            if ((state.TickPhase & 1) != 0)
            {
                stateBuffer[0] = state;
                return;
            }

            long beginTicks = Stopwatch.GetTimestamp();
            int quadCapacity = ResolveQuadCapacity();
            bool openedQuads = OpenOrAcquireVaultBuffer(
                vault,
                ref _quadInstancesHandle,
                BufferID.ArchitectEyeQuadInstances,
                quadCapacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<ArchitectEyeQuadInstance> quads);
            bool openedTelemetry = OpenOrAcquireVaultBuffer(
                vault,
                ref _signalTelemetryHandle,
                BufferID.ArchitectEyeSignalTelemetry,
                SignalLaneCapacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<SignalLaneTelemetry> telemetry);
            bool openedSectorHashes = OpenOrAcquireVaultBuffer(
                vault,
                ref _sectorHashesHandle,
                BufferID.ArchitectEyeSectorHashes,
                SectorHashCapacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<ulong> sectorHashes);
            bool openedBlackBox = OpenOrAcquireVaultBuffer(
                vault,
                ref _blackBoxHandle,
                BufferID.ArchitectEyeBlackBox,
                BlackBoxFrameCount,
                NativeArrayOptions.ClearMemory,
                out NativeArray<ArchitectEyeBlackBoxEntry> blackBox);

            if (!openedQuads || !openedTelemetry || !openedSectorHashes || !openedBlackBox ||
                !quads.IsCreated || !telemetry.IsCreated || !sectorHashes.IsCreated || !blackBox.IsCreated)
                return;

            int count = 0;
            int nonFiniteCount = 0;
            float3 lastFaultPosition = ResolveFallbackProbePosition(vault);
            float signalPressure = 0f;
            float health01 = ResolveSystemHealth01(out float frameTimeMs, out uint killSwitchMask);
            float stpScale01 = ResolveStpScale01(out float stpStress01);

            BuildEntityLabels(vault, quads, ref count, quadCapacity, ref nonFiniteCount, ref lastFaultPosition);
            BuildSdfWireframe(vault, quads, ref count, quadCapacity);
            int laneCount = SignalBusRegistry.CopyTelemetry(telemetry);
            signalPressure = BuildSignalFlow(quads, ref count, quadCapacity, telemetry, laneCount, blackBox, in state);
            BuildDebugSignalOverlay(quads, ref count, quadCapacity, ref signalPressure, ref health01, ref nonFiniteCount, ref lastFaultPosition);
            BuildSectorMap(vault, quads, ref count, quadCapacity, sectorHashes);
            BuildKineticVectorTrails(vault, quads, ref count, quadCapacity, ref nonFiniteCount, ref lastFaultPosition);
            BuildGasHeatmap(quads, ref count, quadCapacity, out float gasCo201, out float gasO201, ref nonFiniteCount);
            float fragmentation01 = BuildMemoryMap(vault, quads, ref count, quadCapacity);
            BuildVaultRelocationLinks(vault, quads, ref count, quadCapacity);
            BuildHeartbeat(quads, ref count, quadCapacity, blackBox, in state, health01, frameTimeMs);
            BuildGhostReplayOverlay(quads, ref count, quadCapacity, blackBox, in state);
            BuildStpPanel(quads, ref count, quadCapacity, stpScale01, stpStress01);
            BuildVisualOverkillDiagnostics(quads, ref count, quadCapacity, in state, signalPressure, gasCo201, stpStress01);

            lastFaultPosition = SanitizeFaultPosition(lastFaultPosition, float3.zero);
            if (nonFiniteCount > 0)
                BuildNanWarning(quads, ref count, quadCapacity, lastFaultPosition);

            float buildMicroseconds = ElapsedMicroseconds(beginTicks);
            state.Flags = _rawStpDebug ? StateFlagRawStp : 0u;
            state.Flags |= nonFiniteCount > 0 ? StateFlagNonFinite : 0u;
            state.LastFrame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            state.LastQuadCount = count;
            state.LastBuildMicroseconds = NonNegativeFinite(buildMicroseconds);
            state.LastHealth01 = SaturateFinite(health01);
            state.LastFrameMs = NonNegativeFinite(frameTimeMs);
            state.LastStpScale01 = SaturateFinite(stpScale01);
            state.LastGasCo201 = SaturateFinite(gasCo201);
            state.LastGasO201 = SaturateFinite(gasO201);
            state.LastSignalLaneCount = laneCount;
            state.LastNonFiniteCount = nonFiniteCount;
            RecordBlackBox(blackBox, ref state, count, laneCount, signalPressure, vault.CapacityPressure01, fragmentation01, health01, frameTimeMs, nonFiniteCount, killSwitchMask, lastFaultPosition, gasCo201, gasO201, stpScale01);
            stateBuffer[0] = state;

            QueueVisualUpload(count, quadCapacity);
            if (nonFiniteCount > 0 && !_dumpWrittenThisFault)
            {
                DumpBlackBox(blackBox);
                _dumpWrittenThisFault = true;
            }
            else if (nonFiniteCount == 0)
            {
                _dumpWrittenThisFault = false;
            }
        }

        public void Render(float deltaTime)
        {
            if (!IsDiagnosticsRuntimeAllowed())
                return;

            FlushQueuedVisualUpload();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f12Key.wasPressedThisFrame)
            {
                _enabled = !_enabled;
                if (!_enabled)
                    _frontCount = 0;
            }

            if (!_enabled || _frontCount <= 0 || _quadMesh == null || _material == null || _argsBuffer == null)
                return;

            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                _quadMesh,
                0,
                _material,
                _drawBounds,
                _argsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                GlobalRenderContext.CurrentCamera);
        }

        [Preserve]
        public bool SubmitCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;

            return SubmitCommand(command.AsSpan());
        }

        [Preserve]
        public bool SubmitCommand(ReadOnlySpan<char> command)
        {
            Trim(ref command);
            if (command.Length == 0)
                return false;

            if (StartsWith(command, "eye "))
            {
                ReadOnlySpan<char> tail = command.Slice(4);
                Trim(ref tail);
                if (tail.Length == 0)
                    return false;

                if (IsToken(tail, "toggle"))
                    _enabled = !_enabled;
                else if (IsEnabledToken(tail))
                    _enabled = true;
                else if (IsDisabledToken(tail))
                    _enabled = false;
                else
                    return false;

                _frontCount = _enabled ? _frontCount : 0;
                return true;
            }

            if (StartsWith(command, "stp raw"))
            {
                ReadOnlySpan<char> tail = command.Slice(7);
                Trim(ref tail);
                _rawStpDebug = tail.Length == 0 || tail[0] == '1' || tail[0] == 'y' || tail[0] == 'Y';
                return true;
            }

            if (StartsWith(command, "ks "))
            {
                ReadOnlySpan<char> tail = command.Slice(3);
                Trim(ref tail);
                if (tail.Length < 2)
                    return false;

                bool enabled = tail[0] == '+';
                if (!enabled && tail[0] != '-')
                    return false;

                if (!TryParseHexOrDecimal(tail.Slice(1), out uint mask))
                    return false;

                GlobalRegistry.SetSystemKillSwitchBits(mask, enabled);
                return true;
            }

            return false;
        }

        private void BuildEntityLabels(
            IDataVault vault,
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            ref int nonFiniteCount,
            ref float3 lastFaultPosition)
        {
            if (!TryReadHotEntityData(vault, out NativeArray<VaultHotEntityData>.ReadOnly hotEntities, out uint generation))
                return;

            int budget = math.min(ResolveEntityBudget(), hotEntities.Length);
            int step = budget > 0 ? math.max(1, hotEntities.Length / budget) : 1;
            int emitted = 0;
            for (int i = 0; i < hotEntities.Length && emitted < budget; i += step)
            {
                VaultHotEntityData hot = hotEntities[i];
                float3 position = hot.LocalPosition;
                if (!math.all(math.isfinite(position)))
                {
                    nonFiniteCount++;
                    continue;
                }

                if (emitted == 0)
                    lastFaultPosition = position;

                int length = 0;
                AppendLiteral(_labelScratch, ref length, "E");
                if (hot.EntityId != 0u)
                    AppendUInt(_labelScratch, ref length, hot.EntityId);
                else
                    AppendInt(_labelScratch, ref length, i);
                AppendLiteral(_labelScratch, ref length, " G");
                AppendHex8(_labelScratch, ref length, generation);
                EmitWorldText(quads, ref count, capacity, position + new float3(0f, 1.2f, 0f), _labelScratch, length, _labelMeters, new float4(0.55f, 0.95f, 1f, 0.9f));
                emitted++;
            }
        }

        private void BuildSdfWireframe(IDataVault vault, NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity)
        {
            float density = 0.15f;
            if (TryOpenExistingVaultBuffer(vault, BufferID.VoxelSdfTexture3D, 1, out NativeArray<byte> sdf))
            {
                int samples = math.min(sdf.Length, 64);
                int positive = 0;
                for (int i = 0; i < samples; i++)
                    positive += sdf[i] > 127 ? 1 : 0;
                density = samples > 0 ? positive * SafeRcp(samples) : density;
            }

            float extent = math.lerp(2f, 8f, math.saturate(density));
            float4 color = new float4(0.25f, 0.9f, 1f, 0.28f);
            EmitWireCube(quads, ref count, capacity, float3.zero, new float3(extent, extent, extent), color);
        }

        private float BuildSignalFlow(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            NativeArray<SignalLaneTelemetry> telemetry,
            int laneCount,
            NativeArray<ArchitectEyeBlackBoxEntry> blackBox,
            in ArchitectEyeRuntimeState state)
        {
            int lanes = math.min(laneCount, math.min(telemetry.Length, 24));
            float pressure = 0f;
            for (int i = 0; i < lanes; i++)
            {
                SignalLaneTelemetry lane = telemetry[i];
                float lanePressure = SaturateFinite((lane.QueuedBeforeFlush + lane.SnapshotCount + lane.DroppedCount * 4) * (1f / 64f));
                pressure = math.max(pressure, lanePressure);
                float y = 0.72f - i * 0.022f;
                float x = -0.93f + lanePressure * 0.16f;
                float4 color = math.select(
                    new float4(0.12f, 0.75f, 1f, 0.6f),
                    new float4(1f, 0.12f, 0.05f, 0.85f),
                    lane.DroppedCount > 0 || (lane.Flags & 1) != 0);
                EmitScreenQuad(quads, ref count, capacity, new float2(x, y), new float2(math.max(0.003f, lanePressure * 0.16f), 0.007f), color, 0f, new float4(0f, 0f, 1f, 1f));
            }

            int history = math.min(blackBox.Length, 50);
            for (int i = 0; i < history; i++)
            {
                int index = state.BlackBoxCursor - 1 - i;
                while (index < 0)
                    index += blackBox.Length;
                ArchitectEyeBlackBoxEntry entry = blackBox[index % blackBox.Length];
                float p = SaturateFinite(entry.SignalPressure01);
                float x = -0.93f + (history - 1 - i) * 0.0115f;
                float4 color = math.select(new float4(0.05f, 0.7f, 1f, 0.42f), new float4(1f, 0.2f, 0.05f, 0.7f), p > 0.66f);
                EmitScreenQuad(quads, ref count, capacity, new float2(x, 0.86f + p * 0.035f), new float2(0.004f, math.max(0.004f, p * 0.04f)), color, 0f, new float4(0f, 0f, 1f, 1f));
            }

            return pressure;
        }

        private void BuildDebugSignalOverlay(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            ref float signalPressure01,
            ref float health01,
            ref int nonFiniteCount,
            ref float3 lastFaultPosition)
        {
            ReadOnlySpan<DebugSignal> signals = SignalBus<DebugSignal>.GetFrameSnapshot();
            int limit = math.min(signals.Length, ResolveEntityBudget());
            float vramPieCursorRadians = -1.5707964f;
            float vramPieRemaining01 = 1f;
            for (int i = 0; i < limit; i++)
            {
                DebugSignal signal = signals[i];
                if (!IsFiniteSignal(in signal))
                {
                    nonFiniteCount++;
                    lastFaultPosition = SanitizeFaultPosition(signal.Position, lastFaultPosition);
                    continue;
                }

                switch ((DebugSignalKind)signal.Kind)
                {
                    case DebugSignalKind.PointerLink:
                    {
                        float4 color = (signal.Flags & VaultRelocationRecord.FlagAddressChanged) != 0
                            ? new float4(1f, 0.92f, 0.05f, 0.95f)
                            : new float4(0.12f, 0.72f, 1f, 0.55f);
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Vector, _lineThicknessMeters, color);
                        break;
                    }
                    case DebugSignalKind.GenerationId:
                    {
                        int length = 0;
                        AppendLiteral(_labelScratch, ref length, "G");
                        AppendHex8(_labelScratch, ref length, signal.Aux0);
                        EmitWorldText(quads, ref count, capacity, signal.Position, _labelScratch, length, _labelMeters, new float4(0.7f, 0.95f, 1f, 0.9f));
                        break;
                    }
                    case DebugSignalKind.CollisionNormal:
                    {
                        float4 color = signal.Value0 > 0.5f ? new float4(1f, 0.08f, 0.02f, 0.9f) : new float4(0.08f, 1f, 0.22f, 0.82f);
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Position + signal.Vector * math.max(0.1f, signal.Value1), _lineThicknessMeters, color);
                        break;
                    }
                    case DebugSignalKind.BreadcrumbSegment:
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Vector, _lineThicknessMeters, new float4(0.05f, 0.9f, 1f, 0.82f));
                        break;
                    case DebugSignalKind.GasRoom:
                    {
                        float o2 = SaturateFinite(signal.Value0);
                        float co2 = SaturateFinite(signal.Value1);
                        float4 color = math.select(new float4(0.05f, 1f, 0.25f, 0.4f), new float4(1f, 0.06f, 0.02f, 0.62f), co2 > o2);
                        EmitBillboardQuad(quads, ref count, capacity, signal.Position, new float2(0.35f, 0.35f), color, new float4(0f, 0f, 1f, 1f));
                        break;
                    }
                    case DebugSignalKind.PressureVector:
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Position + signal.Vector * math.max(0.1f, signal.Value0), _lineThicknessMeters, new float4(1f, 0.58f, 0.1f, 0.85f));
                        break;
                    case DebugSignalKind.FluidVelocity:
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Position + signal.Vector * math.max(0.1f, signal.Value0), _lineThicknessMeters, new float4(0.14f, 0.72f, 1f, 0.75f));
                        break;
                    case DebugSignalKind.AcousticRay:
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Vector, _lineThicknessMeters, new float4(0.72f, 0.9f, 1f, 0.72f));
                        break;
                    case DebugSignalKind.SignalEvent:
                    {
                        signalPressure01 = math.max(signalPressure01, SaturateFinite(signal.Value0));
                        float x = -0.9f + ((signal.Aux0 & 63u) * 0.0125f);
                        EmitScreenQuad(quads, ref count, capacity, new float2(x, 0.73f + signalPressure01 * 0.08f), new float2(0.004f, 0.02f), new float4(0.1f, 0.75f, 1f, 0.62f), 0f, new float4(0f, 0f, 1f, 1f));
                        break;
                    }
                    case DebugSignalKind.LaneSaturation:
                    {
                        float saturation = SaturateFinite(signal.Value0);
                        float4 color = math.select(new float4(0.15f, 0.75f, 1f, 0.68f), new float4(1f, 0.08f, 0.02f, 0.85f), saturation >= 0.9f);
                        EmitScreenQuad(quads, ref count, capacity, new float2(-0.84f + saturation * 0.18f, 0.62f - (signal.Aux0 & 15u) * 0.018f), new float2(math.max(0.004f, saturation * 0.18f), 0.006f), color, 0f, new float4(0f, 0f, 1f, 1f));
                        break;
                    }
                    case DebugSignalKind.EventResonance:
                        EmitWorldLine(quads, ref count, capacity, signal.Position, signal.Vector, _lineThicknessMeters, new float4(0.82f, 0.42f, 1f, 0.76f));
                        break;
                    case DebugSignalKind.NanGeyser:
                        nonFiniteCount++;
                        lastFaultPosition = SanitizeFaultPosition(signal.Position, lastFaultPosition);
                        BuildNanPillar(quads, ref count, capacity, signal.Position);
                        break;
                    case DebugSignalKind.Homeostasis:
                        health01 = SaturateFinite(signal.Value0);
                        BuildHomeostasisDial(quads, ref count, capacity, health01);
                        break;
                    case DebugSignalKind.GhostPose:
                        EmitBillboardQuad(quads, ref count, capacity, signal.Position, new float2(0.18f, 0.42f), new float4(0.4f, 0.85f, 1f, 0.18f), new float4(0f, 0f, 1f, 1f));
                        break;
                    case DebugSignalKind.VramBudgetSlice:
                        BuildVramSlice(quads, ref count, capacity, signal.Aux0, SaturateFinite(signal.Value0), ref vramPieCursorRadians, ref vramPieRemaining01);
                        break;
                    case DebugSignalKind.AupTeleportPreview:
                        EmitWorldLine(quads, ref count, capacity, signal.Position + new float3(-1f, 0f, 0f), signal.Position + new float3(1f, 0f, 0f), _lineThicknessMeters, new float4(0.1f, 1f, 0.8f, 0.9f));
                        EmitWorldLine(quads, ref count, capacity, signal.Position + new float3(0f, 0f, -1f), signal.Position + new float3(0f, 0f, 1f), _lineThicknessMeters, new float4(0.1f, 1f, 0.8f, 0.9f));
                        break;
                }
            }
        }

        private void BuildSectorMap(
            IDataVault vault,
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            NativeArray<ulong> sectorHashes)
        {
            IMacroDatabaseService macro = _macroDatabase;
            if (macro == null || !sectorHashes.IsCreated)
                return;

            MacroDatabaseAup anchor = default;
            if (TryReadHotEntityData(vault, out NativeArray<VaultHotEntityData>.ReadOnly hotEntities, out _) &&
                hotEntities.Length > 0)
            {
                float3 localPosition = hotEntities[0].LocalPosition;
                if (math.all(math.isfinite(localPosition)))
                {
                    anchor.LocalX = localPosition.x;
                    anchor.LocalY = localPosition.y;
                    anchor.LocalZ = localPosition.z;
                }
            }

            int sectorCount = macro.BuildSectorHashWindow(in anchor, ResolveMacroTier(), sectorHashes);
            int cells = math.min(sectorCount, 100);
            for (int i = 0; i < cells; i++)
            {
                ulong hash = sectorHashes[i];
                int col = i % 10;
                int row = i / 10;
                float hashLo = ((hash >> 8) & 0xFFUL) * (1f / 255f);
                float hashHi = ((hash >> 40) & 0xFFUL) * (1f / 255f);
                float2 center = new float2(0.68f + col * 0.025f, -0.78f + row * 0.025f);
                float4 color = new float4(0.04f + hashLo * 0.25f, 0.34f + hashHi * 0.55f, 0.22f + hashLo * 0.2f, 0.58f);
                EmitScreenQuad(quads, ref count, capacity, center, new float2(0.01f, 0.01f), color, 0f, new float4(0f, 0f, 1f, 1f));
            }
        }

        private void BuildKineticVectorTrails(
            IDataVault vault,
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            ref int nonFiniteCount,
            ref float3 lastFaultPosition)
        {
            if (!TryReadHotEntityData(vault, out NativeArray<VaultHotEntityData>.ReadOnly hotEntities, out _))
            {
                return;
            }

            int samples = math.min(hotEntities.Length, ResolveEntityBudget());
            for (int i = 0; i < samples; i++)
            {
                VaultHotEntityData hot = hotEntities[i];
                float3 start = hot.LocalPosition;
                float3 velocity = hot.Velocity;
                if (!math.all(math.isfinite(start)) || !math.all(math.isfinite(velocity)))
                {
                    nonFiniteCount++;
                    lastFaultPosition = SanitizeFaultPosition(start, lastFaultPosition);
                    continue;
                }

                float speedSq = math.lengthsq(velocity);
                if (speedSq < 0.0001f)
                    continue;

                float speed = math.sqrt(math.max(speedSq, 0.0001f));
                float3 end = start + velocity * _vectorScale;
                float heat = math.saturate(speed * 0.1f);
                float4 color = new float4(heat, 0.9f - heat * 0.6f, 1f - heat, 0.75f);
                EmitWorldLine(quads, ref count, capacity, start, end, _lineThicknessMeters, color);
            }
        }

        private void BuildGasHeatmap(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            out float co201,
            out float o201,
            ref int nonFiniteCount)
        {
            co201 = 0f;
            o201 = 0f;
            IGasDynamicsSolver gas = _gasDynamics;
            if (gas == null || !gas.IsInitialized || gas.RoomCount <= 0)
                return;

            NativeArray<float>.ReadOnly o2 = gas.RoomO2;
            NativeArray<float>.ReadOnly co2 = gas.RoomCO2;
            int rooms = math.min(gas.RoomCount, math.min(o2.Length, co2.Length));
            int budget = math.min(rooms, ResolveGasBudget());
            for (int i = 0; i < budget; i++)
            {
                float oxygen = o2[i];
                float carbonDioxide = co2[i];
                if (!math.isfinite(oxygen) || !math.isfinite(carbonDioxide))
                {
                    nonFiniteCount++;
                    continue;
                }

                float roomO2 = SaturateFinite(oxygen * SafeRcp(21f));
                float roomCo2 = SaturateFinite(carbonDioxide * SafeRcp(8f));
                o201 = math.max(o201, roomO2);
                co201 = math.max(co201, roomCo2);
                float4 green = new float4(0.05f, 1f, 0.25f, 0.32f + roomO2 * 0.2f);
                float4 red = new float4(1f, 0.06f, 0.02f, 0.4f + roomCo2 * 0.25f);
                float4 color = math.select(green, red, roomCo2 > roomO2);
                int col = i % 12;
                int row = i / 12;
                EmitScreenQuad(quads, ref count, capacity, new float2(-0.2f + col * 0.03f, -0.82f + row * 0.03f), new float2(0.012f, 0.012f), color, 0f, new float4(0f, 0f, 1f, 1f));
            }
        }

        private float BuildMemoryMap(IDataVault vault, NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity)
        {
            long freeBytes = vault.TotalFreeSpaceBytes;
            long largest = vault.LargestContiguousBlockBytes;
            float fragmentation01 = freeBytes > 0L ? SaturateFinite((float)((double)(freeBytes - largest) / freeBytes)) : 0f;
            int descriptorCount = math.min(H8Memory.BlockDescriptorCount, 72);
            float x = -0.95f;
            for (int i = 0; i < descriptorCount; i++)
            {
                if (!H8Memory.TryGetBlockDescriptor(i, out BlockDescriptor descriptor))
                    continue;

                bool free = descriptor.State == (byte)H8BlockState.Free;
                float w = math.clamp((float)(descriptor.Bytes / 1048576.0), 0.004f, 0.035f);
                float4 color = free
                    ? new float4(0.35f, 0.37f, 0.39f, 0.58f)
                    : new float4(0.1f, 0.45f, 1f, 0.5f);
                if (free && descriptor.Bytes < largest && fragmentation01 > 0.35f)
                    color = new float4(1f, 0.08f, 0.02f, 0.72f);
                EmitScreenQuad(quads, ref count, capacity, new float2(x + w, -0.94f), new float2(w, 0.012f), color, 0f, new float4(0f, 0f, 1f, 1f));
                x += w * 2f + 0.004f;
                if (x > 0.95f)
                    break;
            }

            EmitScreenQuad(quads, ref count, capacity, new float2(-0.95f + vault.CapacityPressure01 * 0.18f, -0.89f), new float2(math.max(0.004f, vault.CapacityPressure01 * 0.18f), 0.009f), new float4(0.15f, 0.8f, 1f, 0.55f), 0f, new float4(0f, 0f, 1f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(-0.95f + fragmentation01 * 0.18f, -0.865f), new float2(math.max(0.004f, fragmentation01 * 0.18f), 0.009f), new float4(1f, 0.08f, 0.02f, 0.7f), 0f, new float4(0f, 0f, 1f, 1f));
            return fragmentation01;
        }

        private void BuildVaultRelocationLinks(IDataVault vault, NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity)
        {
            int relocationCount = math.min(vault.LastRelocationRecordCount, 16);
            for (int i = 0; i < relocationCount; i++)
            {
                if (!vault.TryGetLastRelocationRecord(i, out VaultRelocationRecord record))
                    continue;

                float row = i * 0.16f;
                float systemX = -5f + (record.SystemId & 7) * 0.35f;
                float bufferX = 2f + (math.abs(record.BufferId) & 15) * 0.2f;
                float3 start = new float3(systemX, 1.1f + row, -3f);
                float3 end = new float3(bufferX, 1.1f + row, -3f);
                float4 color = (record.Flags & VaultRelocationRecord.FlagAddressChanged) != 0
                    ? new float4(1f, 0.92f, 0.05f, 0.95f)
                    : new float4(0.12f, 0.72f, 1f, 0.55f);
                EmitWorldLine(quads, ref count, capacity, start, end, _lineThicknessMeters, color);

                int length = 0;
                AppendLiteral(_labelScratch, ref length, "B");
                AppendInt(_labelScratch, ref length, record.BufferId);
                AppendLiteral(_labelScratch, ref length, " G");
                AppendHex8(_labelScratch, ref length, record.Generation);
                EmitWorldText(quads, ref count, capacity, end + new float3(0f, 0.18f, 0f), _labelScratch, length, _labelMeters * 0.6f, color);
            }
        }

        private void BuildHeartbeat(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            NativeArray<ArchitectEyeBlackBoxEntry> blackBox,
            in ArchitectEyeRuntimeState state,
            float health01,
            float frameTimeMs)
        {
            int bars = math.min(blackBox.Length, 64);
            for (int i = 0; i < bars; i++)
            {
                int index = state.BlackBoxCursor - 1 - i;
                while (index < 0)
                    index += blackBox.Length;
                ArchitectEyeBlackBoxEntry entry = blackBox[index % blackBox.Length];
                float h = SaturateFinite(entry.SystemHealth01);
                float t = SaturateFinite(entry.FrameTimeMs * SafeRcp(33.3f));
                float x = 0.1f + (bars - 1 - i) * 0.012f;
                EmitScreenQuad(quads, ref count, capacity, new float2(x, 0.88f + h * 0.035f), new float2(0.004f, math.max(0.004f, h * 0.035f)), new float4(0.2f, 1f, 0.35f, 0.55f), 0f, new float4(0f, 0f, 1f, 1f));
                EmitScreenQuad(quads, ref count, capacity, new float2(x, 0.79f + t * 0.035f), new float2(0.004f, math.max(0.004f, t * 0.035f)), new float4(1f, 0.32f, 0.06f, 0.5f), 0f, new float4(0f, 0f, 1f, 1f));
            }

            int length = 0;
            AppendLiteral(_labelScratch, ref length, "H ");
            AppendFixed1(_labelScratch, ref length, health01 * 100f);
            AppendLiteral(_labelScratch, ref length, " FT ");
            AppendFixed1(_labelScratch, ref length, frameTimeMs);
            EmitScreenText(quads, ref count, capacity, new float2(0.1f, 0.94f), _labelScratch, length, 0.018f, new float4(0.7f, 1f, 0.8f, 0.8f));
        }

        private void BuildGhostReplayOverlay(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            NativeArray<ArchitectEyeBlackBoxEntry> blackBox,
            in ArchitectEyeRuntimeState state)
        {
            int history = math.min(blackBox.Length, 300);
            int step = math.clamp((int)math.round(math.lerp(8f, 2f, SmoothStep01(ResolveGlobalQualityWeight01()))), 2, 8);
            for (int i = 0; i < history; i += step)
            {
                int index = state.BlackBoxCursor - 1 - i;
                while (index < 0)
                    index += blackBox.Length;

                ArchitectEyeBlackBoxEntry entry = blackBox[index % blackBox.Length];
                float3 position = entry.LastFaultPosition;
                if (!math.all(math.isfinite(position)))
                    continue;

                float alpha = SaturateFinite(0.28f - i * (0.24f / history));
                EmitBillboardQuad(quads, ref count, capacity, position, new float2(0.12f, 0.32f), new float4(0.35f, 0.82f, 1f, alpha), new float4(0f, 0f, 1f, 1f));
            }
        }

        private void BuildStpPanel(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float stpScale01, float stress01)
        {
            float4 color = _rawStpDebug
                ? new float4(1f, 0.05f, 0.65f, 0.72f)
                : new float4(0.15f, 0.65f, 1f, 0.45f);
            EmitScreenQuad(quads, ref count, capacity, new float2(0.72f + stpScale01 * 0.18f, 0.68f), new float2(math.max(0.004f, stpScale01 * 0.18f), 0.012f), color, 0f, new float4(0f, 0f, 1f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(0.72f + stress01 * 0.18f, 0.64f), new float2(math.max(0.004f, stress01 * 0.18f), 0.012f), new float4(1f, 0.65f, 0.12f, 0.55f), 0f, new float4(0f, 0f, 1f, 1f));
        }

        private void BuildVisualOverkillDiagnostics(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            in ArchitectEyeRuntimeState state,
            float signalPressure01,
            float gasCo201,
            float stpStress01)
        {
            float visualOverkill01 = ResolveVisualOverkillWeight01();
            if (visualOverkill01 <= 0.001f)
                return;

            int saltCount = (int)math.round(math.lerp(0f, 1024f, visualOverkill01));
            int siltCount = (int)math.round(math.lerp(0f, 1536f, visualOverkill01));
            int dentCount = (int)math.round(math.lerp(0f, 768f, visualOverkill01));
            float timePhase = (state.LastFrame & 1023u) * (1f / 1024f);

            for (int i = 0; i < saltCount && count < capacity; i++)
            {
                uint index = (uint)i;
                float h0 = Hash01(index, 0x53414C54u, 0x43525953u);
                float h1 = Hash01(index, 0x43525953u, 0x41414141u);
                float growth = Triangle01(h0 + timePhase * 0.37f + signalPressure01 + gasCo201);
                float2 center = new float2(-0.96f + h0 * 0.42f, 0.44f + h1 * 0.48f);
                float size = math.lerp(0.0025f, 0.015f, growth);
                float alpha = math.lerp(0.035f, 0.22f, growth);
                EmitScreenQuad(quads, ref count, capacity, center, new float2(size * 0.45f, size), new float4(0.86f, 1f, 0.95f, alpha), 0f, new float4(0f, 0f, 1f, 1f));
            }

            for (int i = 0; i < siltCount && count < capacity; i++)
            {
                uint index = (uint)i;
                float h0 = Hash01(index, 0x53494C54u, 0x57414B45u);
                float h1 = Hash01(index, 0x57414B45u, 0x53555247u);
                float orbit = Triangle01(h0 + timePhase * 0.61f + stpStress01);
                float2 center = new float2(0.18f + (h0 - 0.5f) * 1.65f, -0.22f + (h1 - 0.5f) * 0.58f + orbit * 0.12f);
                float size = math.lerp(0.002f, 0.010f, h1);
                float alpha = math.lerp(0.025f, 0.18f, orbit);
                EmitScreenQuad(quads, ref count, capacity, center, new float2(size, size * 0.42f), new float4(0.28f, 0.72f, 0.9f, alpha), 0f, new float4(0f, 0f, 1f, 1f));
            }

            for (int i = 0; i < dentCount && count < capacity; i++)
            {
                uint index = (uint)i;
                float h0 = Hash01(index, 0x44454E54u, 0x48554C4Cu);
                float h1 = Hash01(index, 0x48554C4Cu, 0x504F4D33u);
                float stress = SaturateFinite(stpStress01 + signalPressure01 * 0.5f);
                float2 center = new float2(0.45f + h0 * 0.5f, -0.82f + h1 * 0.28f);
                float2 size = new float2(math.lerp(0.006f, 0.038f, h0), math.lerp(0.002f, 0.012f, h1));
                EmitScreenQuad(quads, ref count, capacity, center, size, new float4(1f, 0.48f, 0.16f, 0.055f + stress * 0.20f), 0f, new float4(0f, 0f, 1f, 1f));
            }
        }

        private void BuildNanWarning(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 faultPosition)
        {
            faultPosition = SanitizeFaultPosition(faultPosition, float3.zero);
            int length = 0;
            AppendLiteral(_labelScratch, ref length, "NON FINITE VAULT");
            EmitWorldText(quads, ref count, capacity, faultPosition + new float3(0f, 2.2f, 0f), _labelScratch, length, _labelMeters * 2.5f, new float4(1f, 0f, 0f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(0f, 0f), new float2(0.72f, 0.12f), new float4(1f, 0f, 0f, 0.18f), 0f, new float4(0f, 0f, 1f, 1f));
            BuildNanPillar(quads, ref count, capacity, faultPosition);
        }

        private void BuildNanPillar(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 position)
        {
            position = SanitizeFaultPosition(position, float3.zero);
            EmitWorldLine(quads, ref count, capacity, position, position + new float3(0f, 30f, 0f), 0.18f, new float4(1f, 0f, 0f, 0.92f));
        }

        private void BuildHomeostasisDial(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float health01)
        {
            float x = 0.68f;
            float y = 0.52f;
            EmitScreenQuad(quads, ref count, capacity, new float2(x, y), new float2(0.09f, 0.006f), new float4(0.15f, 0.65f, 1f, 0.35f), 0f, new float4(0f, 0f, 1f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(x - 0.08f + health01 * 0.16f, y + 0.02f), new float2(0.004f, 0.035f), new float4(1f - health01, health01, 0.12f, 0.86f), 0f, new float4(0f, 0f, 1f, 1f));
        }

        private void BuildVramSlice(
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            uint sliceId,
            float fraction01,
            ref float cursorRadians,
            ref float remaining01)
        {
            fraction01 = math.min(SaturateFinite(fraction01), math.max(0f, remaining01));
            if (fraction01 <= 0.0001f)
                return;

            float sweepRadians = fraction01 * 6.2831855f;
            float tier = ResolveVisualTierScalar();
            int fullCircleSegments = 12 + (int)(tier * 8f);
            int segments = math.clamp((int)math.ceil(fraction01 * fullCircleSegments), 1, 36);
            int rings = tier >= 2f ? 3 : 2;
            float invSegments = SafeRcp(segments);
            float invRings = SafeRcp(rings + 1f);
            float2 pieCenter = new float2(0.78f, -0.58f);
            float4 color = ResolveVramSliceColor(sliceId, fraction01);

            for (int segment = 0; segment < segments; segment++)
            {
                float segmentT = (segment + 0.5f) * invSegments;
                float angle = cursorRadians + sweepRadians * segmentT;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float s, out float c);
                float2 direction = new float2(c, s);

                for (int ring = 0; ring < rings; ring++)
                {
                    float radialT = (ring + 1f) * invRings;
                    float radius = math.lerp(0.012f, 0.058f, radialT);
                    float beadSize = math.lerp(0.0042f, 0.0072f, radialT) + fraction01 * 0.002f;
                    EmitScreenQuad(quads, ref count, capacity, pieCenter + direction * radius, new float2(beadSize, beadSize), color, 0f, new float4(0f, 0f, 1f, 1f));
                }
            }

            cursorRadians += sweepRadians;
            remaining01 = math.max(0f, remaining01 - fraction01);
        }

        private static float4 ResolveVramSliceColor(uint sliceId, float fraction01)
        {
            switch (sliceId & 3u)
            {
                case 0u:
                    return new float4(0.18f, 0.95f, 0.38f, 0.42f + fraction01 * 0.4f);
                case 1u:
                    return new float4(0.16f, 0.62f, 1f, 0.42f + fraction01 * 0.4f);
                case 2u:
                    return new float4(0.92f, 0.94f, 1f, 0.38f + fraction01 * 0.35f);
                default:
                    return new float4(0.9f, 0.32f, 1f, 0.42f + fraction01 * 0.4f);
            }
        }

        private void RecordBlackBox(
            NativeArray<ArchitectEyeBlackBoxEntry> blackBox,
            ref ArchitectEyeRuntimeState state,
            int quadCount,
            int laneCount,
            float signalPressure01,
            float vaultPressure01,
            float fragmentation01,
            float health01,
            float frameTimeMs,
            int nonFiniteCount,
            uint killSwitchMask,
            float3 lastFaultPosition,
            float gasCo201,
            float gasO201,
            float stpScale01)
        {
            if (!blackBox.IsCreated || blackBox.Length == 0)
                return;

            int index = state.BlackBoxCursor;
            if ((uint)index >= (uint)blackBox.Length)
                index = 0;

            blackBox[index] = new ArchitectEyeBlackBoxEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                QuadCount = (ushort)math.min(ushort.MaxValue, math.max(0, quadCount)),
                SignalLaneCount = (ushort)math.min(ushort.MaxValue, math.max(0, laneCount)),
                SignalPressure01 = SaturateFinite(signalPressure01),
                VaultPressure01 = SaturateFinite(vaultPressure01),
                MemoryFragmentation01 = SaturateFinite(fragmentation01),
                SystemHealth01 = SaturateFinite(health01),
                FrameTimeMs = NonNegativeFinite(frameTimeMs),
                NonFiniteCount = math.max(0, nonFiniteCount),
                KillSwitchMask = killSwitchMask,
                Flags = state.Flags,
                LastFaultPosition = SanitizeFaultPosition(lastFaultPosition, float3.zero),
                GasCo201 = SaturateFinite(gasCo201),
                GasO201 = SaturateFinite(gasO201),
                StpScale01 = SaturateFinite(stpScale01)
            };

            index++;
            state.BlackBoxCursor = index >= blackBox.Length ? 0 : index;
            state.WaterfallCursor = (state.WaterfallCursor + 1) & 63;
        }

        private unsafe void Upload(NativeArray<ArchitectEyeQuadInstance> quads, int count)
        {
            using (UploadMarker.Auto())
            {
                UploadInternal(quads, count);
            }
        }

        private void QueueVisualUpload(int count, int capacity)
        {
            _pendingUploadCount = math.max(0, count);
            _pendingUploadCapacity = math.clamp(capacity, 512, 32768);
            _pendingGpuUpload = true;
        }

        private void FlushQueuedVisualUpload()
        {
            if (!_pendingGpuUpload)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !TryOpenVaultBuffer(
                    vault,
                    in _quadInstancesHandle,
                    BufferID.ArchitectEyeQuadInstances,
                    _pendingUploadCapacity,
                    out NativeArray<ArchitectEyeQuadInstance> quads))
            {
                return;
            }

            EnsureResources();
            EnsureBufferCapacity(_pendingUploadCapacity);
            Upload(quads, _pendingUploadCount);
            _pendingGpuUpload = false;
        }

        private unsafe void UploadInternal(NativeArray<ArchitectEyeQuadInstance> quads, int count)
        {
            if (_instanceBuffer == null || _argsBuffer == null || _quadMesh == null || !quads.IsCreated)
                return;

            int uploadCount = math.min(count, math.min(quads.Length, _bufferQuadCapacity));
            _gpuWriteBufferIndex ^= 1;
            GraphicsBuffer instanceWriteBuffer = _gpuWriteBufferIndex == 0 ? _instanceBufferA : _instanceBufferB;
            GraphicsBuffer argsWriteBuffer = _gpuWriteBufferIndex == 0 ? _argsBufferA : _argsBufferB;
            if (instanceWriteBuffer == null || argsWriteBuffer == null)
                return;

            if (uploadCount > 0)
            {
                NativeArray<ArchitectEyeQuadInstance> mappedInstances = instanceWriteBuffer.LockBufferForWrite<ArchitectEyeQuadInstance>(0, uploadCount);
                UnsafeUtility.MemCpy(
                    NativeArrayUnsafeUtility.GetUnsafePtr(mappedInstances),
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(quads),
                    uploadCount * UnsafeUtility.SizeOf<ArchitectEyeQuadInstance>());
                instanceWriteBuffer.UnlockBufferAfterWrite<ArchitectEyeQuadInstance>(uploadCount);
            }

            NativeArray<uint> mappedArgs = argsWriteBuffer.LockBufferForWrite<uint>(0, 5);
            mappedArgs[0] = _quadMesh.GetIndexCount(0);
            mappedArgs[1] = (uint)uploadCount;
            mappedArgs[2] = _quadMesh.GetIndexStart(0);
            mappedArgs[3] = _quadMesh.GetBaseVertex(0);
            mappedArgs[4] = 0u;
            argsWriteBuffer.UnlockBufferAfterWrite<uint>(5);
            _instanceBuffer = instanceWriteBuffer;
            _argsBuffer = argsWriteBuffer;
            if (_material != null)
            {
                _material.SetBuffer(InstancesId, _instanceBuffer);
                _material.SetFloat(VisualTierId, ResolveVisualTierScalar());
            }
            _frontCount = uploadCount;
        }

        private void DumpBlackBox(NativeArray<ArchitectEyeBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated)
                return;

            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), BlackBoxDumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> entryBytes = stackalloc byte[BlackBoxEntrySizeBytes];
                for (int i = 0; i < blackBox.Length; i++)
                {
                    ArchitectEyeBlackBoxEntry entry = blackBox[i];
                    WriteBlackBoxEntryLittleEndian(entryBytes, in entry);
                    stream.Write(entryBytes);
                }
            }
            catch (Exception)
            {
                _dumpWrittenThisFault = true;
            }
        }

        private static void WriteBlackBoxEntryLittleEndian(Span<byte> destination, in ArchitectEyeBlackBoxEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), entry.QuadCount);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), entry.SignalLaneCount);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.SignalPressure01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.VaultPressure01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.MemoryFragmentation01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.SystemHealth01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.FrameTimeMs);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(28, 4), entry.NonFiniteCount);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.KillSwitchMask);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.LastFaultPosition.x);
            WriteFloatLittleEndian(destination.Slice(44, 4), entry.LastFaultPosition.y);
            WriteFloatLittleEndian(destination.Slice(48, 4), entry.LastFaultPosition.z);
            WriteFloatLittleEndian(destination.Slice(52, 4), entry.GasCo201);
            WriteFloatLittleEndian(destination.Slice(56, 4), entry.GasO201);
            WriteFloatLittleEndian(destination.Slice(60, 4), entry.StpScale01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(math.isfinite(value) ? value : 0f));
        }

        private float3 ResolveFallbackProbePosition(IDataVault vault)
        {
            if (TryReadHotEntityData(vault, out NativeArray<VaultHotEntityData>.ReadOnly hotEntities, out _) &&
                hotEntities.Length > 0)
            {
                float3 position = hotEntities[0].LocalPosition;
                if (math.all(math.isfinite(position)))
                    return position;
            }

            return float3.zero;
        }

        private static bool TryReadHotEntityData(
            IDataVault vault,
            out NativeArray<VaultHotEntityData>.ReadOnly hotEntities,
            out uint generation)
        {
            hotEntities = default;
            generation = 0u;
            if (vault == null ||
                !vault.TryGetGenerationHandle(
                    BufferID.VaultHotEntityData,
                    out VaultGenerationHandle<VaultHotEntityData> handle) ||
                !IsMatchingVaultHandle(in handle, BufferID.VaultHotEntityData) ||
                !vault.TryReadOnlyHandle(in handle, out hotEntities) ||
                !hotEntities.IsCreated ||
                hotEntities.Length < 1)
            {
                hotEntities = default;
                return false;
            }

            generation = handle.Generation;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteSignal(in Hecton8.Core.Contracts.Signals.DebugSignal signal)
        {
            return math.all(math.isfinite(signal.Position)) &&
                   math.all(math.isfinite(signal.Vector)) &&
                   math.isfinite(signal.Value0) &&
                   math.isfinite(signal.Value1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFaultPosition(float3 candidate, float3 fallback)
        {
            if (math.all(math.isfinite(candidate)))
                return candidate;

            return math.all(math.isfinite(fallback)) ? fallback : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SaturateFinite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NonNegativeFinite(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private float ResolveSystemHealth01(out float frameTimeMs, out uint killSwitchMask)
        {
            float health = HomeostasisBrain.SystemHealthIndex01;
            frameTimeMs = 0f;
            killSwitchMask = _lastKillSwitchMask;

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            if (healthSignals.Length > 0)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[healthSignals.Length - 1];
                health = signal.SystemHealthIndex01;
                killSwitchMask = (uint)signal.KillSwitchMask;
            }

            ReadOnlySpan<KillSwitchSignal> killSignals = SignalBus<KillSwitchSignal>.GetFrameSnapshot();
            if (killSignals.Length > 0)
                killSwitchMask = (uint)killSignals[killSignals.Length - 1].CurrentMask;

            _lastKillSwitchMask = killSwitchMask;

            ReadOnlySpan<FrameTimeSignal> frameSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            if (frameSignals.Length > 0)
                frameTimeMs = NonNegativeFinite(frameSignals[frameSignals.Length - 1].FrameTimeEwmaMs);

            return SaturateFinite(health);
        }

        private float ResolveStpScale01(out float stress01)
        {
            stress01 = 0f;
            IResolutionScalerService scaler = _resolutionScaler;
            if (scaler == null || !scaler.TryGetScaleState(out ResolutionScaleState state))
                return 1f;

            stress01 = SaturateFinite(state.SystemStressEwma01);
            return math.isfinite(state.CurrentRenderScale01) ? math.saturate(state.CurrentRenderScale01) : 1f;
        }

        private int ResolveEntityBudget()
        {
            int lowBudget = math.max(16, _lowTierEntityBudget);
            int midBudget = math.max(lowBudget, _midTierEntityBudget);
            int highBudget = math.max(midBudget, _highTierEntityBudget);
            int ultraBudget = math.max(highBudget, _ultraTierEntityBudget);
            float quality01 = SmoothStep01(ResolveGlobalQualityWeight01());
            float lowToMid = math.lerp(lowBudget, midBudget, math.saturate(quality01 * 2f));
            float midToHigh = math.lerp(midBudget, highBudget, math.saturate((quality01 - 0.5f) * 2.8571429f));
            float highToUltra = math.lerp(highBudget, ultraBudget, math.saturate((quality01 - 0.85f) * 6.666667f));
            float budget = math.select(lowToMid, midToHigh, quality01 >= 0.5f);
            budget = math.select(budget, highToUltra, quality01 >= 0.85f);
            return math.clamp((int)math.round(budget), lowBudget, ultraBudget);
        }

        private int ResolveGasBudget()
        {
            return math.clamp((int)math.round(math.lerp(48f, 384f, SmoothStep01(ResolveGlobalQualityWeight01()))), 48, 384);
        }

        private int ResolveQuadCapacity()
        {
            int minimum = math.min(_maxQuads, 2048);
            int maximum = math.max(_maxQuads, DefaultMaxQuads);
            float capacity = math.lerp(minimum, maximum, SmoothStep01(ResolveGlobalQualityWeight01()));
            return math.clamp((int)math.round(capacity), minimum, maximum);
        }

        private MacroDatabaseTier ResolveMacroTier()
        {
            float quality01 = SmoothStep01(ResolveGlobalQualityWeight01());
            if (quality01 >= 0.85f)
                return MacroDatabaseTier.Ultra;
            if (quality01 >= 0.62f)
                return MacroDatabaseTier.High;
            if (quality01 >= 0.28f)
                return MacroDatabaseTier.Middle;
            return MacroDatabaseTier.Low;
        }

        private void EmitWorldText(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 origin, char[] chars, int length, float size, float4 color)
        {
            float xStep = size * 0.68f;
            float xOrigin = -length * xStep * 0.5f;
            for (int i = 0; i < length; i++)
            {
                char c = chars[i];
                if (c == ' ')
                    continue;

                float4 uv = ResolveGlyphUv(c);
                float3 center = origin + new float3(xOrigin + i * xStep, 0f, 0f);
                EmitBillboardQuad(quads, ref count, capacity, center, new float2(size * 0.35f, size * 0.5f), color, uv);
            }
        }

        private void EmitScreenText(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float2 origin, char[] chars, int length, float size, float4 color)
        {
            float xStep = size * 0.7f;
            for (int i = 0; i < length; i++)
            {
                char c = chars[i];
                if (c == ' ')
                    continue;

                float4 uv = ResolveGlyphUv(c);
                EmitScreenQuad(quads, ref count, capacity, new float2(origin.x + i * xStep, origin.y), new float2(size * 0.35f, size * 0.5f), color, 1f, uv);
            }
        }

        private void EmitBillboardQuad(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 center, float2 halfSize, float4 color, float4 uv)
        {
            if ((uint)count >= (uint)capacity)
                return;

            quads[count++] = new ArchitectEyeQuadInstance
            {
                CenterHalfX = new float4(center, halfSize.x),
                AxisYHalfY = new float4(0f, 1f, 0f, halfSize.y),
                Color = color,
                UvMode = new float4(uv.x, uv.y, uv.z, 0f),
                Aux = new float4(0f, 0f, 0f, uv.w)
            };
        }

        private void EmitScreenQuad(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float2 center, float2 halfSize, float4 color, float mode, float4 uv)
        {
            if ((uint)count >= (uint)capacity)
                return;

            quads[count++] = new ArchitectEyeQuadInstance
            {
                CenterHalfX = new float4(center.x, center.y, ScreenDepth, halfSize.x),
                AxisYHalfY = new float4(0f, 1f, 0f, halfSize.y),
                Color = color,
                UvMode = new float4(uv.x, uv.y, uv.z, mode <= 0f ? 1f : mode),
                Aux = new float4(0f, 0f, 0f, uv.w)
            };
        }

        private void EmitWorldLine(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 start, float3 end, float thickness, float4 color)
        {
            float3 delta = end - start;
            float lenSq = math.lengthsq(delta);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                return;

            float invLen = SafeRsqrt(lenSq);
            if (invLen <= 0f)
                return;

            float3 axisX = delta * invLen;
            float3 axisY = math.cross(axisX, new float3(0f, 1f, 0f));
            float axisYSq = math.lengthsq(axisY);
            float invAxisY = SafeRsqrt(axisYSq);
            axisY = math.select(new float3(1f, 0f, 0f), axisY * invAxisY, invAxisY > 0f);
            float halfLength = math.sqrt(math.max(lenSq, 0.000001f)) * 0.5f;
            EmitOrientedQuad(quads, ref count, capacity, (start + end) * 0.5f, axisX, axisY, new float2(halfLength, thickness), color, new float4(0f, 0f, 1f, 1f));
        }

        private void EmitWireCube(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 center, float3 extents, float4 color)
        {
            float3 a = center + new float3(-extents.x, -extents.y, -extents.z);
            float3 b = center + new float3(extents.x, -extents.y, -extents.z);
            float3 c = center + new float3(extents.x, -extents.y, extents.z);
            float3 d = center + new float3(-extents.x, -extents.y, extents.z);
            float3 e = center + new float3(-extents.x, extents.y, -extents.z);
            float3 f = center + new float3(extents.x, extents.y, -extents.z);
            float3 g = center + new float3(extents.x, extents.y, extents.z);
            float3 h = center + new float3(-extents.x, extents.y, extents.z);
            EmitWorldLine(quads, ref count, capacity, a, b, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, b, c, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, c, d, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, d, a, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, e, f, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, f, g, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, g, h, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, h, e, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, a, e, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, b, f, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, c, g, _lineThicknessMeters, color);
            EmitWorldLine(quads, ref count, capacity, d, h, _lineThicknessMeters, color);
        }

        private void EmitOrientedQuad(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 center, float3 axisX, float3 axisY, float2 halfSize, float4 color, float4 uv)
        {
            if ((uint)count >= (uint)capacity)
                return;

            quads[count++] = new ArchitectEyeQuadInstance
            {
                CenterHalfX = new float4(center, halfSize.x),
                AxisYHalfY = new float4(axisY, halfSize.y),
                Color = color,
                UvMode = new float4(uv.x, uv.y, uv.z, 2f),
                Aux = new float4(axisX, uv.w)
            };
        }

        private float4 ResolveGlyphUv(char c)
        {
            int glyph = ((int)c) & 0x7F;
            int col = glyph & 15;
            int row = glyph >> 4;
            float invCols = 1f / GlyphAtlasColumns;
            float invRows = 1f / GlyphAtlasRows;
            float u0 = col * invCols;
            float v0 = row * invRows;
            return new float4(u0, v0, u0 + invCols, v0 + invRows);
        }

        private void CreateQuadMesh()
        {
            _quadMesh = new Mesh { name = "ArchitectEyeIndirectQuad" };
            _quadMesh.vertices = QuadVertices;
            _quadMesh.uv = QuadUvs;
            _quadMesh.triangles = QuadIndices;
            _quadMesh.RecalculateBounds();
        }

        private void EnsureResources()
        {
            _maxQuads = math.clamp(_maxQuads, 512, 32768);
            if (_quadMesh == null)
                CreateQuadMesh();
            if (_glyphAtlas == null)
                CreateGlyphAtlas();
            if (_material == null)
                CreateMaterial();
            if (_instanceBufferA == null || _instanceBufferB == null || _argsBufferA == null || _argsBufferB == null)
                CreateBuffers(ResolveQuadCapacity());

            if (_material != null)
            {
                _material.SetTexture(GlyphAtlasId, _glyphAtlas);
                if (_instanceBuffer != null)
                    _material.SetBuffer(InstancesId, _instanceBuffer);
            }
        }

        private void CreateMaterial()
        {
            Shader shader = ResolveQuadShader();
            if (shader == null)
                return;

            _material = new Material(shader)
            {
                name = "ArchitectEyeIndirectQuads",
                enableInstancing = true
            };
            _material.SetTexture(GlyphAtlasId, _glyphAtlas);
        }

        private Shader ResolveQuadShader()
        {
            if (_quadShader != null)
                return _quadShader;

#if UNITY_EDITOR
            _quadShader = AssetDatabase.LoadAssetAtPath<Shader>(QuadShaderAssetPath);
            return _quadShader;
#else
            return null;
#endif
        }

        private void EnsureBufferCapacity(int requiredCapacity)
        {
            int safeRequired = math.clamp(requiredCapacity, 512, 32768);
            if (_instanceBufferA != null &&
                _instanceBufferB != null &&
                _argsBufferA != null &&
                _argsBufferB != null &&
                _bufferQuadCapacity >= safeRequired)
            {
                return;
            }

            CreateBuffers(safeRequired);
        }

        private void CreateBuffers(int capacity)
        {
            ReleaseBuffersOnly();
            _bufferQuadCapacity = math.clamp(capacity, 512, 32768);
            int stride = UnsafeUtility.SizeOf<ArchitectEyeQuadInstance>();
            _instanceBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, _bufferQuadCapacity, stride);
            _instanceBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, _bufferQuadCapacity, stride);
            _argsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, sizeof(uint) * 5);
            _argsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, sizeof(uint) * 5);
            _gpuWriteBufferIndex = 0;
            _instanceBuffer = _instanceBufferA;
            _argsBuffer = _argsBufferA;
            if (_material != null)
            {
                _material.SetBuffer(InstancesId, _instanceBuffer);
                _material.SetFloat(VisualTierId, ResolveVisualTierScalar());
            }
        }

        private void CreateGlyphAtlas()
        {
            for (int glyph = 0; glyph < 128; glyph++)
            {
                int col = glyph & 15;
                int row = glyph >> 4;
                for (int y = 0; y < GlyphCellPixels; y++)
                {
                    byte bits = GlyphRow((char)glyph, y);
                    for (int x = 0; x < GlyphCellPixels; x++)
                    {
                        int pixelX = col * GlyphCellPixels + x;
                        int pixelY = row * GlyphCellPixels + y;
                        int pixelIndex = pixelY * GlyphCellPixels * GlyphAtlasColumns + pixelX;
                        bool on = (bits & (1 << (GlyphCellPixels - 1 - x))) != 0;
                        _glyphPixels[pixelIndex] = on ? (byte)255 : (byte)0;
                    }
                }
            }

            _glyphAtlas = new Texture2D(GlyphCellPixels * GlyphAtlasColumns, GlyphCellPixels * GlyphAtlasRows, TextureFormat.Alpha8, false, true)
            {
                name = "ArchitectEyeGlyphAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _glyphAtlas.SetPixelData(_glyphPixels, 0);
            _glyphAtlas.Apply(false, true);
            if (_material != null)
                _material.SetTexture(GlyphAtlasId, _glyphAtlas);
        }

        private void ReleaseResources()
        {
            ReleaseBuffersOnly();
            if (_quadMesh != null)
            {
                DestroyUnityObject(_quadMesh);
                _quadMesh = null;
            }

            if (_material != null)
            {
                DestroyUnityObject(_material);
                _material = null;
            }

            if (_glyphAtlas != null)
            {
                DestroyUnityObject(_glyphAtlas);
                _glyphAtlas = null;
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void ReleaseBuffersOnly()
        {
            _instanceBufferA?.Dispose();
            _instanceBufferB?.Dispose();
            _argsBufferA?.Dispose();
            _argsBufferB?.Dispose();
            _instanceBufferA = null;
            _instanceBufferB = null;
            _argsBufferA = null;
            _argsBufferB = null;
            _instanceBuffer = null;
            _argsBuffer = null;
            _bufferQuadCapacity = 0;
            _gpuWriteBufferIndex = 0;
            _frontCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeRcp(float value)
        {
            return math.abs(value) > 0.000001f && math.isfinite(value) ? 1f / value : 0f;
        }

        private static float ResolveVisualTierScalar()
        {
            return math.lerp(0f, 3f, SmoothStep01(ResolveGlobalQualityWeight01()));
        }

        private static float ResolveVisualOverkillWeight01()
        {
            return SmoothStep01(math.saturate((ResolveGlobalQualityWeight01() - 0.5f) * 2f));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1.0f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - (2.0f * t));
        }

        private static bool IsDiagnosticsRuntimeAllowed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeRsqrt(float value)
        {
            return value > 0.000001f && math.isfinite(value) ? math.rsqrt(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint a, uint b, uint c)
        {
            uint h = a ^ 2166136261u;
            h = (h ^ b) * 16777619u;
            h = (h ^ c) * 16777619u;
            h ^= h >> 16;
            return (h & 0xFFFFu) * HashToUnit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Triangle01(float value)
        {
            float f = value - math.floor(value);
            return 1f - math.abs(f * 2f - 1f);
        }

        private static float ElapsedMicroseconds(long beginTicks)
        {
            long elapsed = Stopwatch.GetTimestamp() - beginTicks;
            return (float)(elapsed * 1000000.0 / Stopwatch.Frequency);
        }

        private static void AppendLiteral(char[] buffer, ref int length, string literal)
        {
            int count = math.min(literal.Length, buffer.Length - length);
            for (int i = 0; i < count; i++)
                buffer[length++] = literal[i];
        }

        private static void AppendInt(char[] buffer, ref int length, int value)
        {
            if (length >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[length++] = '0';
                return;
            }

            uint magnitude;
            if (value < 0)
            {
                buffer[length++] = '-';
                magnitude = value == int.MinValue ? 2147483648u : (uint)(-value);
            }
            else
            {
                magnitude = (uint)value;
            }

            Span<char> tmp = stackalloc char[12];
            int n = 0;
            while (magnitude > 0u && n < tmp.Length)
            {
                tmp[n++] = (char)('0' + (int)(magnitude % 10u));
                magnitude /= 10u;
            }

            for (int i = n - 1; i >= 0 && length < buffer.Length; i--)
                buffer[length++] = tmp[i];
        }

        private static void AppendUInt(char[] buffer, ref int length, uint value)
        {
            if (length >= buffer.Length)
                return;

            if (value == 0u)
            {
                buffer[length++] = '0';
                return;
            }

            Span<char> tmp = stackalloc char[10];
            int n = 0;
            while (value > 0u && n < tmp.Length)
            {
                tmp[n++] = (char)('0' + (int)(value % 10u));
                value /= 10u;
            }

            for (int i = n - 1; i >= 0 && length < buffer.Length; i--)
                buffer[length++] = tmp[i];
        }

        private static void AppendHex8(char[] buffer, ref int length, uint value)
        {
            for (int shift = 28; shift >= 0 && length < buffer.Length; shift -= 4)
            {
                int digit = (int)((value >> shift) & 0xFu);
                buffer[length++] = (char)(digit < 10 ? '0' + digit : 'A' + digit - 10);
            }
        }

        private static void AppendFixed1(char[] buffer, ref int length, float value)
        {
            if (!math.isfinite(value))
            {
                AppendLiteral(buffer, ref length, "NaN");
                return;
            }

            int scaled = (int)math.round(value * 10f);
            AppendInt(buffer, ref length, scaled / 10);
            if (length < buffer.Length)
                buffer[length++] = '.';
            if (length < buffer.Length)
                buffer[length++] = (char)('0' + math.abs(scaled % 10));
        }

        private static bool StartsWith(ReadOnlySpan<char> value, string prefix)
        {
            if (value.Length < prefix.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                char a = value[i];
                char b = prefix[i];
                if (a >= 'A' && a <= 'Z')
                    a = (char)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static void Trim(ref ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && value[start] <= ' ')
                start++;
            while (end >= start && value[end] <= ' ')
                end--;
            value = start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool IsEnabledToken(ReadOnlySpan<char> value)
        {
            return IsToken(value, "on") || IsToken(value, "1") || IsToken(value, "true") || IsToken(value, "yes");
        }

        private static bool IsDisabledToken(ReadOnlySpan<char> value)
        {
            return IsToken(value, "off") || IsToken(value, "0") || IsToken(value, "false") || IsToken(value, "no");
        }

        private static bool IsToken(ReadOnlySpan<char> value, string token)
        {
            if (value.Length != token.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                char a = value[i];
                if (a >= 'A' && a <= 'Z')
                    a = (char)(a + 32);
                if (a != token[i])
                    return false;
            }

            return true;
        }

        private static bool TryParseHexOrDecimal(ReadOnlySpan<char> value, out uint result)
        {
            Trim(ref value);
            result = 0u;
            bool hex = value.Length > 2 && value[0] == '0' && (value[1] == 'x' || value[1] == 'X');
            int start = hex ? 2 : 0;
            for (int i = start; i < value.Length; i++)
            {
                int digit = DecodeDigit(value[i], hex);
                if (digit < 0)
                    return false;

                if (hex)
                {
                    if (result > 0x0FFFFFFFu)
                        return false;

                    result = (result << 4) | (uint)digit;
                }
                else
                {
                    uint nextDigit = (uint)digit;
                    if (result > (uint.MaxValue - nextDigit) / 10u)
                        return false;

                    result = result * 10u + nextDigit;
                }
            }

            return value.Length > start;
        }

        private static int DecodeDigit(char c, bool hex)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (!hex)
                return -1;
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        private static byte GlyphRow(char c, int row)
        {
            int r = math.clamp(row, 0, 7);
            if (c >= 'a' && c <= 'z')
                c = (char)(c - 32);

            switch (c)
            {
                case '0': return Pick(r, 0x7C, 0xC6, 0xCE, 0xD6, 0xE6, 0xC6, 0x7C, 0x00);
                case '1': return Pick(r, 0x30, 0x70, 0x30, 0x30, 0x30, 0x30, 0xFC, 0x00);
                case '2': return Pick(r, 0x7C, 0xC6, 0x06, 0x1C, 0x70, 0xC0, 0xFE, 0x00);
                case '3': return Pick(r, 0x7C, 0xC6, 0x06, 0x3C, 0x06, 0xC6, 0x7C, 0x00);
                case '4': return Pick(r, 0x1C, 0x3C, 0x6C, 0xCC, 0xFE, 0x0C, 0x1E, 0x00);
                case '5': return Pick(r, 0xFE, 0xC0, 0xFC, 0x06, 0x06, 0xC6, 0x7C, 0x00);
                case '6': return Pick(r, 0x3C, 0x60, 0xC0, 0xFC, 0xC6, 0xC6, 0x7C, 0x00);
                case '7': return Pick(r, 0xFE, 0xC6, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00);
                case '8': return Pick(r, 0x7C, 0xC6, 0xC6, 0x7C, 0xC6, 0xC6, 0x7C, 0x00);
                case '9': return Pick(r, 0x7C, 0xC6, 0xC6, 0x7E, 0x06, 0x0C, 0x78, 0x00);
                case 'A': return Pick(r, 0x38, 0x6C, 0xC6, 0xFE, 0xC6, 0xC6, 0xC6, 0x00);
                case 'B': return Pick(r, 0xFC, 0x66, 0x66, 0x7C, 0x66, 0x66, 0xFC, 0x00);
                case 'C': return Pick(r, 0x3C, 0x66, 0xC0, 0xC0, 0xC0, 0x66, 0x3C, 0x00);
                case 'D': return Pick(r, 0xF8, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0xF8, 0x00);
                case 'E': return Pick(r, 0xFE, 0x62, 0x68, 0x78, 0x68, 0x62, 0xFE, 0x00);
                case 'F': return Pick(r, 0xFE, 0x62, 0x68, 0x78, 0x68, 0x60, 0xF0, 0x00);
                case 'G': return Pick(r, 0x3C, 0x66, 0xC0, 0xC0, 0xCE, 0x66, 0x3E, 0x00);
                case 'H': return Pick(r, 0xC6, 0xC6, 0xC6, 0xFE, 0xC6, 0xC6, 0xC6, 0x00);
                case 'I': return Pick(r, 0x78, 0x30, 0x30, 0x30, 0x30, 0x30, 0x78, 0x00);
                case 'K': return Pick(r, 0xE6, 0x66, 0x6C, 0x78, 0x6C, 0x66, 0xE6, 0x00);
                case 'L': return Pick(r, 0xF0, 0x60, 0x60, 0x60, 0x62, 0x66, 0xFE, 0x00);
                case 'M': return Pick(r, 0xC6, 0xEE, 0xFE, 0xFE, 0xD6, 0xC6, 0xC6, 0x00);
                case 'N': return Pick(r, 0xC6, 0xE6, 0xF6, 0xDE, 0xCE, 0xC6, 0xC6, 0x00);
                case 'O': return Pick(r, 0x7C, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x7C, 0x00);
                case 'P': return Pick(r, 0xFC, 0x66, 0x66, 0x7C, 0x60, 0x60, 0xF0, 0x00);
                case 'R': return Pick(r, 0xFC, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0xE6, 0x00);
                case 'S': return Pick(r, 0x7C, 0xC6, 0xE0, 0x78, 0x0E, 0xC6, 0x7C, 0x00);
                case 'T': return Pick(r, 0xFC, 0xB4, 0x30, 0x30, 0x30, 0x30, 0x78, 0x00);
                case 'U': return Pick(r, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x7C, 0x00);
                case 'V': return Pick(r, 0xC6, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x10, 0x00);
                case 'X': return Pick(r, 0xC6, 0xC6, 0x6C, 0x38, 0x6C, 0xC6, 0xC6, 0x00);
                case 'Y': return Pick(r, 0xCC, 0xCC, 0xCC, 0x78, 0x30, 0x30, 0x78, 0x00);
                case '.': return Pick(r, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x00);
                case '-': return Pick(r, 0x00, 0x00, 0x00, 0x7C, 0x00, 0x00, 0x00, 0x00);
                default: return c == ' ' ? (byte)0x00 : Pick(r, 0x7E, 0x42, 0x5A, 0x52, 0x5A, 0x42, 0x7E, 0x00);
            }
        }

        private static byte Pick(int index, int r0, int r1, int r2, int r3, int r4, int r5, int r6, int r7)
        {
            switch (index)
            {
                case 0: return (byte)r0;
                case 1: return (byte)r1;
                case 2: return (byte)r2;
                case 3: return (byte)r3;
                case 4: return (byte)r4;
                case 5: return (byte)r5;
                case 6: return (byte)r6;
                default: return (byte)r7;
            }
        }
    }
}
