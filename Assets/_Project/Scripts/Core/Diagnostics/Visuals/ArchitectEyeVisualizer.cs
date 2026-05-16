using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace Hecton8.Core.Diagnostics.Visuals
{
    [Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
    public struct ArchitectEyeQuadInstance
    {
        public float4 CenterHalfX;
        public float4 AxisYHalfY;
        public float4 Color;
        public float4 UvMode;
        public float4 Aux;
    }

    [Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ArchitectEyeBlackBoxEntry
    {
        public uint Frame;
        public ushort QuadCount;
        public ushort SignalLaneCount;
        public float SignalPressure01;
        public float VaultPressure01;
        public float MemoryFragmentation01;
        public float SystemHealth01;
        public float FrameTimeMs;
        public int NonFiniteCount;
        public uint KillSwitchMask;
        public uint Flags;
        public float3 LastFaultPosition;
        public float GasCo201;
        public float GasO201;
        public float StpScale01;
    }

    [Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ArchitectEyeRuntimeState
    {
        public int TickPhase;
        public int BlackBoxCursor;
        public int WaterfallCursor;
        public int LastQuadCount;
        public uint Flags;
        public uint LastFrame;
        public float LastBuildMicroseconds;
        public float LastHealth01;
        public float LastFrameMs;
        public float LastStpScale01;
        public float LastGasCo201;
        public float LastGasO201;
        public int LastSignalLaneCount;
        public int LastNonFiniteCount;
        public int Reserved0;
        public int Reserved1;
    }

    [Preserve]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5800)]
    public sealed class ArchitectEyeVisualizer : MonoBehaviour, ISlowTickable, IRenderable
    {
        private const int BlackBoxFrameCount = 300;
        private const int SignalLaneCapacity = 256;
        private const int SectorHashCapacity = 512;
        private const int GlyphCellPixels = 8;
        private const int GlyphAtlasColumns = 16;
        private const int GlyphAtlasRows = 8;
        private const int GlyphAtlasPixels = GlyphCellPixels * GlyphAtlasColumns * GlyphCellPixels * GlyphAtlasRows;
        private const int DefaultMaxQuads = 8192;
        private const float ScreenDepth = 0.25f;
        private const uint StateFlagRawStp = 1u << 0;
        private const uint StateFlagNonFinite = 1u << 1;
        private static readonly int InstancesId = Shader.PropertyToID("_H8EyeQuads");
        private static readonly int GlyphAtlasId = Shader.PropertyToID("_H8EyeGlyphAtlas");

        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _maxQuads = DefaultMaxQuads;
        [SerializeField] private int _lowTierEntityBudget = 64;
        [SerializeField] private int _midTierEntityBudget = 128;
        [SerializeField] private int _highTierEntityBudget = 512;
        [SerializeField] private int _ultraTierEntityBudget = 1024;
        [SerializeField] private float _labelMeters = 0.18f;
        [SerializeField] private float _vectorScale = 0.08f;
        [SerializeField] private float _lineThicknessMeters = 0.025f;

        private readonly Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(20000f, 20000f, 20000f));
        private readonly uint[] _argsScratch = new uint[5];
        private readonly char[] _labelScratch = new char[128];
        private readonly byte[] _glyphPixels = new byte[GlyphAtlasPixels];
        private Mesh _quadMesh;
        private Material _material;
        private Texture2D _glyphAtlas;
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _argsBuffer;
        private int _frontCount;
        private bool _registered;
        private bool _rawStpDebug;
        private bool _dumpWrittenThisFault;

        private void Awake()
        {
            EnsureResources();
        }

        private void OnEnable()
        {
            EnsureResources();

            if (!Application.isPlaying)
                return;

            GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
            GlobalRegistry.Renderables.TryRegister(this);
            _registered = true;
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                GlobalRegistry.Renderables.TryUnregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        public void SlowTick()
        {
            if (!_enabled)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            NativeArray<ArchitectEyeRuntimeState> stateBuffer = vault.GetBuffer<ArchitectEyeRuntimeState>(
                BufferID.ArchitectEyeRuntimeState,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
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
            NativeArray<ArchitectEyeQuadInstance> quads = vault.GetBuffer<ArchitectEyeQuadInstance>(
                BufferID.ArchitectEyeQuadInstances,
                quadCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<SignalLaneTelemetry> telemetry = vault.GetBuffer<SignalLaneTelemetry>(
                BufferID.ArchitectEyeSignalTelemetry,
                SignalLaneCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<ulong> sectorHashes = vault.GetBuffer<ulong>(
                BufferID.ArchitectEyeSectorHashes,
                SectorHashCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<ArchitectEyeBlackBoxEntry> blackBox = vault.GetBuffer<ArchitectEyeBlackBoxEntry>(
                BufferID.ArchitectEyeBlackBox,
                BlackBoxFrameCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);

            if (!quads.IsCreated || !telemetry.IsCreated || !sectorHashes.IsCreated || !blackBox.IsCreated)
                return;

            int count = 0;
            int nonFiniteCount = 0;
            float3 lastFaultPosition = default;
            float signalPressure = 0f;
            float health01 = ResolveSystemHealth01(out float frameTimeMs, out uint killSwitchMask);
            float stpScale01 = ResolveStpScale01(out float stpStress01);

            BuildEntityLabels(vault, quads, ref count, quadCapacity, ref nonFiniteCount, ref lastFaultPosition);
            BuildSdfWireframe(vault, quads, ref count, quadCapacity);
            int laneCount = SignalBusRegistry.CopyTelemetry(telemetry);
            signalPressure = BuildSignalFlow(quads, ref count, quadCapacity, telemetry, laneCount, blackBox, in state);
            BuildSectorMap(vault, quads, ref count, quadCapacity, sectorHashes);
            BuildKineticVectorTrails(vault, quads, ref count, quadCapacity, ref nonFiniteCount, ref lastFaultPosition);
            BuildGasHeatmap(quads, ref count, quadCapacity, out float gasCo201, out float gasO201, ref nonFiniteCount);
            float fragmentation01 = BuildMemoryMap(vault, quads, ref count, quadCapacity);
            BuildHeartbeat(quads, ref count, quadCapacity, blackBox, in state, health01, frameTimeMs);
            BuildStpPanel(quads, ref count, quadCapacity, stpScale01, stpStress01);

            if (nonFiniteCount > 0)
                BuildNanWarning(quads, ref count, quadCapacity, lastFaultPosition);

            float buildMicroseconds = ElapsedMicroseconds(beginTicks);
            state.Flags = _rawStpDebug ? StateFlagRawStp : 0u;
            state.Flags |= nonFiniteCount > 0 ? StateFlagNonFinite : 0u;
            state.LastFrame = unchecked((uint)Mathf.Max(0, Time.frameCount));
            state.LastQuadCount = count;
            state.LastBuildMicroseconds = buildMicroseconds;
            state.LastHealth01 = health01;
            state.LastFrameMs = frameTimeMs;
            state.LastStpScale01 = stpScale01;
            state.LastGasCo201 = gasCo201;
            state.LastGasO201 = gasO201;
            state.LastSignalLaneCount = laneCount;
            state.LastNonFiniteCount = nonFiniteCount;
            RecordBlackBox(blackBox, ref state, count, laneCount, signalPressure, vault.CapacityPressure01, fragmentation01, health01, frameTimeMs, nonFiniteCount, killSwitchMask, lastFaultPosition, gasCo201, gasO201, stpScale01);
            stateBuffer[0] = state;

            Upload(quads, count);
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
            if (!_enabled || _frontCount <= 0 || _quadMesh == null || _material == null || _argsBuffer == null)
                return;

            Graphics.DrawMeshInstancedIndirect(
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
            if (!vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs, out NativeArray<AbsoluteUniversePosition> aups) || !aups.IsCreated)
                return;

            int budget = math.min(ResolveEntityBudget(), aups.Length);
            int step = budget > 0 ? math.max(1, aups.Length / budget) : 1;
            int emitted = 0;
            for (int i = 0; i < aups.Length && emitted < budget; i += step)
            {
                AbsoluteUniversePosition aup = aups[i];
                if (!VaultProbeUtility.IsFinite(in aup))
                {
                    nonFiniteCount++;
                    continue;
                }

                float3 position = aup.ToRuntimeFloat3();
                if (!math.all(math.isfinite(position)))
                {
                    nonFiniteCount++;
                    lastFaultPosition = new float3(aup.LocalX, aup.LocalY, aup.LocalZ);
                    continue;
                }

                int length = 0;
                AppendLiteral(_labelScratch, ref length, "E");
                AppendInt(_labelScratch, ref length, i);
                AppendLiteral(_labelScratch, ref length, " AUP");
                EmitWorldText(quads, ref count, capacity, position + new float3(0f, 1.2f, 0f), _labelScratch, length, _labelMeters, new float4(0.55f, 0.95f, 1f, 0.9f));
                emitted++;
            }
        }

        private void BuildSdfWireframe(IDataVault vault, NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity)
        {
            float density = 0.15f;
            if (vault.TryGetBuffer<float>(BufferID.VoxelSdfTexture3D, out NativeArray<float> sdf) && sdf.IsCreated && sdf.Length > 0)
            {
                int samples = math.min(sdf.Length, 64);
                int positive = 0;
                for (int i = 0; i < samples; i++)
                    positive += sdf[i] > 0f && math.isfinite(sdf[i]) ? 1 : 0;
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
                float lanePressure = math.saturate((lane.QueuedBeforeFlush + lane.SnapshotCount + lane.DroppedCount * 4) * (1f / 64f));
                pressure = math.max(pressure, lanePressure);
                float y = 0.72f - i * 0.022f;
                float x = -0.93f + lanePressure * 0.16f;
                float4 color = math.select(
                    new float4(0.12f, 0.75f, 1f, 0.6f),
                    new float4(1f, 0.12f, 0.05f, 0.85f),
                    lane.DroppedCount > 0 || (lane.Flags & 1) != 0);
                EmitScreenQuad(quads, ref count, capacity, new float2(x, y), new float2(math.max(0.003f, lanePressure * 0.16f), 0.007f), color, 0f, new float4(0f, 0f, 1f, 1f));
            }

            int history = math.min(blackBox.Length, 48);
            for (int i = 0; i < history; i++)
            {
                int index = state.BlackBoxCursor - 1 - i;
                while (index < 0)
                    index += blackBox.Length;
                ArchitectEyeBlackBoxEntry entry = blackBox[index % blackBox.Length];
                float p = math.saturate(entry.SignalPressure01);
                float x = -0.93f + (history - 1 - i) * 0.012f;
                float4 color = math.select(new float4(0.05f, 0.7f, 1f, 0.42f), new float4(1f, 0.2f, 0.05f, 0.7f), p > 0.66f);
                EmitScreenQuad(quads, ref count, capacity, new float2(x, 0.86f + p * 0.035f), new float2(0.004f, math.max(0.004f, p * 0.04f)), color, 0f, new float4(0f, 0f, 1f, 1f));
            }

            return pressure;
        }

        private void BuildSectorMap(
            IDataVault vault,
            NativeArray<ArchitectEyeQuadInstance> quads,
            ref int count,
            int capacity,
            NativeArray<ulong> sectorHashes)
        {
            IMacroDatabaseService macro = GlobalRegistry.MacroDatabase;
            if (macro == null || !sectorHashes.IsCreated)
                return;

            MacroDatabaseAup anchor = default;
            if (vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs, out NativeArray<AbsoluteUniversePosition> aups) &&
                aups.IsCreated &&
                aups.Length > 0)
            {
                AbsoluteUniversePosition aup = aups[0];
                anchor.GridX = aup.GridX;
                anchor.GridY = aup.GridY;
                anchor.GridZ = aup.GridZ;
                anchor.LocalX = aup.LocalX;
                anchor.LocalY = aup.LocalY;
                anchor.LocalZ = aup.LocalZ;
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
            bool hasAup = vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.RigidbodyAUPs, out NativeArray<AbsoluteUniversePosition> aups) && aups.IsCreated;
            if (!hasAup)
                hasAup = vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs, out aups) && aups.IsCreated;
            if (!hasAup ||
                !vault.TryGetBuffer<float3>(BufferID.EntityVelocities, out NativeArray<float3> velocities) ||
                !velocities.IsCreated)
            {
                return;
            }

            int samples = math.min(math.min(aups.Length, velocities.Length), ResolveEntityBudget());
            for (int i = 0; i < samples; i++)
            {
                AbsoluteUniversePosition aup = aups[i];
                float3 velocity = velocities[i];
                if (!VaultProbeUtility.IsFinite(in aup) || !math.all(math.isfinite(velocity)))
                {
                    nonFiniteCount++;
                    lastFaultPosition = new float3(aup.LocalX, aup.LocalY, aup.LocalZ);
                    continue;
                }

                float speedSq = math.lengthsq(velocity);
                if (speedSq < 0.0001f)
                    continue;

                float3 start = aup.ToRuntimeFloat3();
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
            IGasDynamicsSolver gas = GlobalRegistry.GasDynamics;
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

                float roomO2 = math.saturate(oxygen * SafeRcp(21f));
                float roomCo2 = math.saturate(carbonDioxide * SafeRcp(8f));
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
            float fragmentation01 = freeBytes > 0L ? math.saturate((float)((double)(freeBytes - largest) / freeBytes)) : 0f;
            int descriptorCount = math.min(H8Memory.BlockDescriptorCount, 72);
            float x = -0.95f;
            for (int i = 0; i < descriptorCount; i++)
            {
                if (!H8Memory.TryGetBlockDescriptor(i, out BlockDescriptor descriptor))
                    continue;

                bool free = descriptor.State == (byte)H8BlockState.Free;
                float w = math.clamp((float)(descriptor.Bytes / 1048576.0), 0.004f, 0.035f);
                float4 color = free
                    ? new float4(1f, 0.9f, 0.05f, 0.62f)
                    : new float4(0.1f, 0.45f, 1f, 0.5f);
                EmitScreenQuad(quads, ref count, capacity, new float2(x + w, -0.94f), new float2(w, 0.012f), color, 0f, new float4(0f, 0f, 1f, 1f));
                x += w * 2f + 0.004f;
                if (x > 0.95f)
                    break;
            }

            EmitScreenQuad(quads, ref count, capacity, new float2(-0.95f + vault.CapacityPressure01 * 0.18f, -0.89f), new float2(math.max(0.004f, vault.CapacityPressure01 * 0.18f), 0.009f), new float4(0.15f, 0.8f, 1f, 0.55f), 0f, new float4(0f, 0f, 1f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(-0.95f + fragmentation01 * 0.18f, -0.865f), new float2(math.max(0.004f, fragmentation01 * 0.18f), 0.009f), new float4(1f, 0.92f, 0.08f, 0.7f), 0f, new float4(0f, 0f, 1f, 1f));
            return fragmentation01;
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
                float h = math.saturate(entry.SystemHealth01);
                float t = math.saturate(entry.FrameTimeMs * (1f / 33.3f));
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

        private void BuildStpPanel(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float stpScale01, float stress01)
        {
            float4 color = _rawStpDebug
                ? new float4(1f, 0.05f, 0.65f, 0.72f)
                : new float4(0.15f, 0.65f, 1f, 0.45f);
            EmitScreenQuad(quads, ref count, capacity, new float2(0.72f + stpScale01 * 0.18f, 0.68f), new float2(math.max(0.004f, stpScale01 * 0.18f), 0.012f), color, 0f, new float4(0f, 0f, 1f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(0.72f + stress01 * 0.18f, 0.64f), new float2(math.max(0.004f, stress01 * 0.18f), 0.012f), new float4(1f, 0.65f, 0.12f, 0.55f), 0f, new float4(0f, 0f, 1f, 1f));
        }

        private void BuildNanWarning(NativeArray<ArchitectEyeQuadInstance> quads, ref int count, int capacity, float3 faultPosition)
        {
            int length = 0;
            AppendLiteral(_labelScratch, ref length, "NON FINITE VAULT");
            EmitWorldText(quads, ref count, capacity, faultPosition + new float3(0f, 2.2f, 0f), _labelScratch, length, _labelMeters * 2.5f, new float4(1f, 0f, 0f, 1f));
            EmitScreenQuad(quads, ref count, capacity, new float2(0f, 0f), new float2(0.72f, 0.12f), new float4(1f, 0f, 0f, 0.18f), 0f, new float4(0f, 0f, 1f, 1f));
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
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                QuadCount = (ushort)math.min(ushort.MaxValue, math.max(0, quadCount)),
                SignalLaneCount = (ushort)math.min(ushort.MaxValue, math.max(0, laneCount)),
                SignalPressure01 = math.saturate(signalPressure01),
                VaultPressure01 = math.saturate(vaultPressure01),
                MemoryFragmentation01 = math.saturate(fragmentation01),
                SystemHealth01 = math.saturate(health01),
                FrameTimeMs = math.max(0f, frameTimeMs),
                NonFiniteCount = math.max(0, nonFiniteCount),
                KillSwitchMask = killSwitchMask,
                Flags = state.Flags,
                LastFaultPosition = lastFaultPosition,
                GasCo201 = math.saturate(gasCo201),
                GasO201 = math.saturate(gasO201),
                StpScale01 = math.saturate(stpScale01)
            };

            index++;
            state.BlackBoxCursor = index >= blackBox.Length ? 0 : index;
            state.WaterfallCursor = (state.WaterfallCursor + 1) & 63;
        }

        private void Upload(NativeArray<ArchitectEyeQuadInstance> quads, int count)
        {
            if (_instanceBuffer == null || _argsBuffer == null || _quadMesh == null || !quads.IsCreated)
                return;

            int uploadCount = math.min(count, math.min(quads.Length, _maxQuads));
            if (uploadCount > 0)
                _instanceBuffer.SetData(quads, 0, 0, uploadCount);

            _argsScratch[0] = _quadMesh.GetIndexCount(0);
            _argsScratch[1] = (uint)uploadCount;
            _argsScratch[2] = _quadMesh.GetIndexStart(0);
            _argsScratch[3] = _quadMesh.GetBaseVertex(0);
            _argsScratch[4] = 0u;
            _argsBuffer.SetData(_argsScratch);
            _frontCount = uploadCount;
        }

        private void DumpBlackBox(NativeArray<ArchitectEyeBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated)
                return;

            try
            {
                string root = Directory.GetCurrentDirectory();
                string directory = Path.Combine(root, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_ARCHITECT_EYE_VISUALIZER.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                unsafe
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                    int bytes = blackBox.Length * UnsafeUtility.SizeOf<ArchitectEyeBlackBoxEntry>();
                    stream.Write(new ReadOnlySpan<byte>(source, bytes));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        }

        private float ResolveSystemHealth01(out float frameTimeMs, out uint killSwitchMask)
        {
            float health = HomeostasisBrain.SystemHealthIndex01;
            frameTimeMs = 0f;
            killSwitchMask = GlobalRegistry.SystemKillSwitchMask;

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            if (healthSignals.Length > 0)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[healthSignals.Length - 1];
                health = signal.SystemHealthIndex01;
                killSwitchMask = (uint)signal.KillSwitchMask;
            }

            ReadOnlySpan<FrameTimeSignal> frameSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            if (frameSignals.Length > 0)
                frameTimeMs = frameSignals[frameSignals.Length - 1].FrameTimeEwmaMs;

            return math.saturate(math.isfinite(health) ? health : 0f);
        }

        private float ResolveStpScale01(out float stress01)
        {
            stress01 = 0f;
            IResolutionScalerService scaler = GlobalRegistry.ResolutionScaler;
            if (scaler == null || !scaler.TryGetScaleState(out ResolutionScaleState state))
                return 1f;

            stress01 = math.saturate(math.isfinite(state.SystemStressEwma01) ? state.SystemStressEwma01 : 0f);
            return math.saturate(math.isfinite(state.CurrentRenderScale01) ? state.CurrentRenderScale01 : 1f);
        }

        private int ResolveEntityBudget()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return math.max(16, _lowTierEntityBudget);
                case HectonQualityTier.Mid:
                    return math.max(_lowTierEntityBudget, _midTierEntityBudget);
                case HectonQualityTier.Ultra:
                    return math.max(_highTierEntityBudget, _ultraTierEntityBudget);
                default:
                    return math.max(_midTierEntityBudget, _highTierEntityBudget);
            }
        }

        private int ResolveGasBudget()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return 48;
                case HectonQualityTier.Mid:
                    return 96;
                case HectonQualityTier.Ultra:
                    return 384;
                default:
                    return 192;
            }
        }

        private int ResolveQuadCapacity()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return math.min(_maxQuads, 2048);
                case HectonQualityTier.Mid:
                    return math.min(_maxQuads, 4096);
                case HectonQualityTier.Ultra:
                    return math.max(_maxQuads, DefaultMaxQuads);
                default:
                    return math.min(_maxQuads, 8192);
            }
        }

        private MacroDatabaseTier ResolveMacroTier()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return MacroDatabaseTier.Low;
                case HectonQualityTier.Ultra:
                    return MacroDatabaseTier.Ultra;
                case HectonQualityTier.High:
                    return MacroDatabaseTier.High;
                default:
                    return MacroDatabaseTier.Middle;
            }
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

            float invLen = math.rsqrt(math.max(lenSq, 0.000001f));
            if (!math.isfinite(invLen))
                return;

            float3 axisX = delta * invLen;
            float3 axisY = math.cross(axisX, new float3(0f, 1f, 0f));
            float axisYSq = math.lengthsq(axisY);
            axisY = math.select(new float3(1f, 0f, 0f), axisY * math.rsqrt(math.max(axisYSq, 0.000001f)), axisYSq > 0.000001f);
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
            _quadMesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, 1f, 0f)
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
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
            if (_instanceBuffer == null || _argsBuffer == null)
                CreateBuffers();

            if (_material != null)
            {
                _material.SetTexture(GlyphAtlasId, _glyphAtlas);
                if (_instanceBuffer != null)
                    _material.SetBuffer(InstancesId, _instanceBuffer);
            }
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Hidden/Hecton8/Diagnostics/ArchitectEyeIndirectQuads");
            if (shader == null)
                return;

            _material = new Material(shader)
            {
                name = "ArchitectEyeIndirectQuads",
                enableInstancing = true
            };
            _material.SetTexture(GlyphAtlasId, _glyphAtlas);
        }

        private void CreateBuffers()
        {
            ReleaseBuffersOnly();
            int stride = UnsafeUtility.SizeOf<ArchitectEyeQuadInstance>();
            _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _maxQuads, stride);
            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
            if (_material != null)
                _material.SetBuffer(InstancesId, _instanceBuffer);
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
            _instanceBuffer?.Dispose();
            _argsBuffer?.Dispose();
            _instanceBuffer = null;
            _argsBuffer = null;
            _frontCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeRcp(float value)
        {
            return math.abs(value) > 0.000001f && math.isfinite(value) ? 1f / value : 0f;
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

            if (value < 0)
            {
                buffer[length++] = '-';
                value = -value;
            }

            Span<char> tmp = stackalloc char[12];
            int n = 0;
            while (value > 0 && n < tmp.Length)
            {
                tmp[n++] = (char)('0' + value % 10);
                value /= 10;
            }

            for (int i = n - 1; i >= 0 && length < buffer.Length; i--)
                buffer[length++] = tmp[i];
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
                result = hex ? (result << 4) | (uint)digit : result * 10u + (uint)digit;
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
