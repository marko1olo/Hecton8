// ============================================================================
// HECTON-8 - VRInteractionKinematicBridge.cs
// Deterministic VR hand kinematic bridge: controller AUP -> SDF projection -> socket snap.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Memory;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public static class VRInteractionKinematicBridgeConstants
    {
        public const int HandCount = 2;
        public const int LeftHandIndex = 0;
        public const int RightHandIndex = 1;
        public const int SocketCapacity = 128;
        public const int TelemetryFrameCapacity = 300;
        public const int TelemetryCapacity = TelemetryFrameCapacity * HandCount;
        public const int DefaultSdfProbeIterationsLow = 2;
        public const int DefaultSdfProbeIterationsUltra = 8;
        public const float DefaultHandRadiusMeters = 0.07f;
        public const float DefaultMaxArmLengthMeters = 0.78f;
        public const float DefaultVelocitySignalThreshold = 4.5f;
        public const float DefaultSdfRangeMeters = 2f;
        public const uint StateFlagValid = 1u << 0;
        public const uint StateFlagTracked = 1u << 1;
        public const uint StateFlagSdfResolved = 1u << 2;
        public const uint StateFlagArmClamped = 1u << 3;
        public const uint StateFlagSocketSnapped = 1u << 4;
        public const uint StateFlagVelocitySignal = 1u << 5;
        public const uint StateFlagNonFinite = 1u << 6;
        public const uint StateFlagLeftHand = 1u << 7;
        public const uint StateFlagSdfUnavailable = 1u << 8;
        public const uint StateFlagNoPhysicsProxy = 1u << 9;
        public const uint TelemetryFlagBudgetExceeded = 1u << 16;
        public const uint TelemetryFlagQualityScaled = 1u << 17;
        public const uint SocketFlagActive = 1u << 0;
        public const uint TuningFlagInitialized = 1u << 0;
        public const uint TuningFlagSdfEnabled = 1u << 1;
        public const uint TuningFlagSocketSnapEnabled = 1u << 2;
        public const uint TuningFlagVelocitySignalEnabled = 1u << 3;
        public const uint TuningFlagMockInputEnabled = 1u << 4;
        public const uint TelemetryMarker = 0x56524B42u; // VRKB
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_271.bin";

        public const BufferID HandStatesBuffer = (BufferID)73680;
        public const BufferID PreviousHandStatesBuffer = (BufferID)73681;
        public const BufferID ControllerMatrixInputsBuffer = (BufferID)73682;
        public const BufferID InteractionSocketsBuffer = (BufferID)73683;
        public const BufferID TuningBuffer = (BufferID)73684;
        public const BufferID TelemetryRingBuffer = (BufferID)73685;
        public const BufferID TelemetryCursorBuffer = (BufferID)73686;
        public const BufferID ResolvedHandMatricesBuffer = (BufferID)73687;
        public const SystemID OwnerSystemId = SystemID.GameplayPlayer;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VRHandStateDTO
    {
        [FieldOffset(0)] public double3 RawControllerAUP;
        [FieldOffset(24)] public double3 ResolvedHandAUP;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public uint InteractionFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VRControllerMatrixDTO
    {
        [FieldOffset(0)] public float4x4 ControllerLocalToWorld;
        [FieldOffset(64)] public double3 PlayerRootAUP;
        [FieldOffset(88)] public float3 ShoulderRuntimeOffset;
        [FieldOffset(100)] public float Grip01;
        [FieldOffset(104)] public uint Flags;
        [FieldOffset(108)] public uint FrameIndex;
        [FieldOffset(112)] public byte HandIndex;
        [FieldOffset(113)] public byte IsTracked;
        [FieldOffset(114)] private ushort _pad0;
        [FieldOffset(116)] private uint _pad1;
        [FieldOffset(120)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VRInteractionSocketDTO
    {
        [FieldOffset(0)] public double3 SocketAUP;
        [FieldOffset(24)] public quaternion Orientation;
        [FieldOffset(40)] public float3 Normal;
        [FieldOffset(52)] public float SnapRadiusMeters;
        [FieldOffset(56)] public uint SocketId;
        [FieldOffset(60)] public uint Flags;
        [FieldOffset(64)] private ulong _pad0;
        [FieldOffset(72)] private ulong _pad1;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VRInteractionTuningDTO
    {
        [FieldOffset(0)] public double3 PlayerRootAUP;
        [FieldOffset(24)] public double3 ShoulderAUP;
        [FieldOffset(48)] public double3 SdfOriginAUP;
        [FieldOffset(72)] public float3 SdfCellSize;
        [FieldOffset(84)] public float SdfRangeMeters;
        [FieldOffset(88)] public int3 SdfDimensions;
        [FieldOffset(100)] public float HandRadiusMeters;
        [FieldOffset(104)] public float MaxArmLengthMeters;
        [FieldOffset(108)] public float SnapRadiusScale;
        [FieldOffset(112)] public float VelocitySignalThreshold;
        [FieldOffset(116)] public float GlobalQualityWeight;
        [FieldOffset(120)] public uint FrameIndex;
        [FieldOffset(124)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VRInteractionTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint CpuTimeMicros;
        [FieldOffset(16)] public double3 RawControllerAUP;
        [FieldOffset(40)] public double3 ResolvedHandAUP;
        [FieldOffset(64)] public float3 Velocity;
        [FieldOffset(76)] public float MaxPenetrationMeters;
        [FieldOffset(80)] public float3 SurfaceNormal;
        [FieldOffset(92)] public uint SocketId;
        [FieldOffset(96)] public uint SolverIterations;
        [FieldOffset(100)] public uint HandIndex;
        [FieldOffset(104)] public uint Marker;
        [FieldOffset(108)] private uint _pad0;
        [FieldOffset(112)] private ulong _pad1;
        [FieldOffset(120)] private ulong _pad2;
    }

    public struct VRInteractionKinematicBridgeViews
    {
        public NativeArray<VRHandStateDTO> HandStates;
        public NativeArray<VRHandStateDTO> PreviousHandStates;
        public NativeArray<VRControllerMatrixDTO> ControllerMatrices;
        public NativeArray<VRInteractionSocketDTO> Sockets;
        public NativeArray<VRInteractionTuningDTO> Tuning;
        public NativeArray<VRInteractionTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<float4x4> HandMatrices;

        public bool IsValid()
        {
            return HandStates.IsCreated &&
                   PreviousHandStates.IsCreated &&
                   ControllerMatrices.IsCreated &&
                   Sockets.IsCreated &&
                   Tuning.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   HandMatrices.IsCreated &&
                   HandStates.Length >= VRInteractionKinematicBridgeConstants.HandCount &&
                   PreviousHandStates.Length >= VRInteractionKinematicBridgeConstants.HandCount &&
                   ControllerMatrices.Length >= VRInteractionKinematicBridgeConstants.HandCount &&
                   Sockets.Length >= VRInteractionKinematicBridgeConstants.SocketCapacity &&
                   Tuning.Length >= 1 &&
                   TelemetryRing.Length >= VRInteractionKinematicBridgeConstants.TelemetryCapacity &&
                   TelemetryCursor.Length >= 1 &&
                   HandMatrices.Length >= VRInteractionKinematicBridgeConstants.HandCount;
        }
    }

    public static class VRInteractionKinematicBridgeLayout
    {
        public const int HandStateBytes = 64;
        public const int ControllerMatrixBytes = 128;
        public const int SocketBytes = 128;
        public const int TuningBytes = 128;
        public const int TelemetryEntryBytes = 128;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<VRHandStateDTO>() == HandStateBytes &&
                   UnsafeUtility.SizeOf<VRControllerMatrixDTO>() == ControllerMatrixBytes &&
                   UnsafeUtility.SizeOf<VRInteractionSocketDTO>() == SocketBytes &&
                   UnsafeUtility.SizeOf<VRInteractionTuningDTO>() == TuningBytes &&
                   UnsafeUtility.SizeOf<VRInteractionTelemetryEntry>() == TelemetryEntryBytes &&
                   OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.RawControllerAUP)) == 0 &&
                   OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.ResolvedHandAUP)) == 24 &&
                   OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.Velocity)) == 48 &&
                   OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.InteractionFlags)) == 60 &&
                   OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.PlayerRootAUP)) == 0 &&
                   OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.ShoulderAUP)) == 24 &&
                   OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.SdfOriginAUP)) == 48 &&
                   OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.SdfDimensions)) == 88 &&
                   OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.RawControllerAUP)) == 16 &&
                   OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.ResolvedHandAUP)) == 40;
        }

        public static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    public static class VRInteractionKinematicBridgeVault
    {
        public static bool EnsureBuffers(IDataVault vault, out VRInteractionKinematicBridgeViews views)
        {
            views = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, out views))
                    return false;

                EnsureDefaults(views);
                return true;
            }

            VaultGenerationHandle<VRHandStateDTO> handStates = vault.GetGenerationHandle<VRHandStateDTO>(
                VRInteractionKinematicBridgeConstants.HandStatesBuffer,
                VRInteractionKinematicBridgeConstants.HandCount,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<VRHandStateDTO> previousStates = vault.GetGenerationHandle<VRHandStateDTO>(
                VRInteractionKinematicBridgeConstants.PreviousHandStatesBuffer,
                VRInteractionKinematicBridgeConstants.HandCount,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<VRControllerMatrixDTO> matrices = vault.GetGenerationHandle<VRControllerMatrixDTO>(
                VRInteractionKinematicBridgeConstants.ControllerMatrixInputsBuffer,
                VRInteractionKinematicBridgeConstants.HandCount,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<VRInteractionSocketDTO> sockets = vault.GetGenerationHandle<VRInteractionSocketDTO>(
                VRInteractionKinematicBridgeConstants.InteractionSocketsBuffer,
                VRInteractionKinematicBridgeConstants.SocketCapacity,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<VRInteractionTuningDTO> tuning = vault.GetGenerationHandle<VRInteractionTuningDTO>(
                VRInteractionKinematicBridgeConstants.TuningBuffer,
                1,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<VRInteractionTelemetryEntry> telemetry = vault.GetGenerationHandle<VRInteractionTelemetryEntry>(
                VRInteractionKinematicBridgeConstants.TelemetryRingBuffer,
                VRInteractionKinematicBridgeConstants.TelemetryCapacity,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> cursor = vault.GetGenerationHandle<int>(
                VRInteractionKinematicBridgeConstants.TelemetryCursorBuffer,
                1,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<float4x4> matricesOutput = vault.GetGenerationHandle<float4x4>(
                VRInteractionKinematicBridgeConstants.ResolvedHandMatricesBuffer,
                VRInteractionKinematicBridgeConstants.HandCount,
                VRInteractionKinematicBridgeConstants.OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);

            if (!vault.TryResolveHandle(in handStates, out views.HandStates) ||
                !vault.TryResolveHandle(in previousStates, out views.PreviousHandStates) ||
                !vault.TryResolveHandle(in matrices, out views.ControllerMatrices) ||
                !vault.TryResolveHandle(in sockets, out views.Sockets) ||
                !vault.TryResolveHandle(in tuning, out views.Tuning) ||
                !vault.TryResolveHandle(in telemetry, out views.TelemetryRing) ||
                !vault.TryResolveHandle(in cursor, out views.TelemetryCursor) ||
                !vault.TryResolveHandle(in matricesOutput, out views.HandMatrices) ||
                !views.IsValid())
            {
                views = default;
                return false;
            }

            EnsureDefaults(views);
            return true;
        }

        public static bool TryResolveExisting(IDataVault vault, out VRInteractionKinematicBridgeViews views)
        {
            views = default;
            if (vault == null)
                return false;

            return TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.HandStatesBuffer, VRInteractionKinematicBridgeConstants.HandCount, out views.HandStates) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.PreviousHandStatesBuffer, VRInteractionKinematicBridgeConstants.HandCount, out views.PreviousHandStates) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.ControllerMatrixInputsBuffer, VRInteractionKinematicBridgeConstants.HandCount, out views.ControllerMatrices) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.InteractionSocketsBuffer, VRInteractionKinematicBridgeConstants.SocketCapacity, out views.Sockets) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.TuningBuffer, 1, out views.Tuning) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.TelemetryRingBuffer, VRInteractionKinematicBridgeConstants.TelemetryCapacity, out views.TelemetryRing) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.TelemetryCursorBuffer, 1, out views.TelemetryCursor) &&
                   TryOpenExistingLane(vault, VRInteractionKinematicBridgeConstants.ResolvedHandMatricesBuffer, VRInteractionKinematicBridgeConstants.HandCount, out views.HandMatrices) &&
                   views.IsValid();
        }

        public static bool TryReadLatestHandState(IDataVault vault, PhysicalHandSide side, out VRHandStateDTO state)
        {
            state = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle<VRHandStateDTO>(VRInteractionKinematicBridgeConstants.HandStatesBuffer, out VaultGenerationHandle<VRHandStateDTO> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<VRHandStateDTO> states) ||
                !states.IsCreated)
            {
                return false;
            }

            int handIndex = side == PhysicalHandSide.Left
                ? VRInteractionKinematicBridgeConstants.LeftHandIndex
                : VRInteractionKinematicBridgeConstants.RightHandIndex;
            if ((uint)handIndex >= (uint)states.Length)
                return false;

            state = states[handIndex];
            return VRInteractionKinematicBridgeMath.IsFinite(state.RawControllerAUP) &&
                   VRInteractionKinematicBridgeMath.IsFinite(state.ResolvedHandAUP);
        }

        public static unsafe bool DumpTelemetryFaultOnly(IDataVault vault, string path = null)
        {
            if (vault == null ||
                !vault.TryGetGenerationHandle<VRInteractionTelemetryEntry>(VRInteractionKinematicBridgeConstants.TelemetryRingBuffer, out VaultGenerationHandle<VRInteractionTelemetryEntry> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<VRInteractionTelemetryEntry> ring) ||
                !ring.IsCreated ||
                ring.Length < VRInteractionKinematicBridgeConstants.TelemetryCapacity)
            {
                return false;
            }

            string resolvedPath = string.IsNullOrEmpty(path) ? VRInteractionKinematicBridgeConstants.DumpPath : path;
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int stride = UnsafeUtility.SizeOf<VRInteractionTelemetryEntry>();
            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
            using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                for (int i = 0; i < ring.Length; i++)
                    stream.Write(new ReadOnlySpan<byte>(source + (i * stride), stride));

                stream.Flush(true);
            }
            return true;
        }

        private static bool TryOpenExistingLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void EnsureDefaults(VRInteractionKinematicBridgeViews views)
        {
            VRInteractionTuningDTO tuning = views.Tuning[0];
            if ((tuning.Flags & VRInteractionKinematicBridgeConstants.TuningFlagInitialized) == 0u)
            {
                tuning.HandRadiusMeters = VRInteractionKinematicBridgeConstants.DefaultHandRadiusMeters;
                tuning.MaxArmLengthMeters = VRInteractionKinematicBridgeConstants.DefaultMaxArmLengthMeters;
                tuning.SnapRadiusScale = 1f;
                tuning.VelocitySignalThreshold = VRInteractionKinematicBridgeConstants.DefaultVelocitySignalThreshold;
                tuning.SdfRangeMeters = VRInteractionKinematicBridgeConstants.DefaultSdfRangeMeters;
                tuning.SdfCellSize = new float3(0.25f);
                tuning.SdfDimensions = int3.zero;
                tuning.GlobalQualityWeight = 1f;
                tuning.Flags =
                    VRInteractionKinematicBridgeConstants.TuningFlagInitialized |
                    VRInteractionKinematicBridgeConstants.TuningFlagSdfEnabled |
                    VRInteractionKinematicBridgeConstants.TuningFlagSocketSnapEnabled |
                    VRInteractionKinematicBridgeConstants.TuningFlagVelocitySignalEnabled;
                views.Tuning[0] = tuning;
            }
        }
    }

    public static class VRInteractionSocketCsvParser
    {
        public static int ParseSockets(ReadOnlySpan<byte> bytes, NativeArray<VRInteractionSocketDTO> output)
        {
            if (!output.IsCreated || output.Length == 0 || bytes.Length == 0)
                return 0;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int write = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out VRInteractionSocketDTO socket))
                {
                    output[write++] = socket;
                    if (write >= output.Length)
                        return write;
                }

                lineStart = i + 1;
            }

            return write;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out VRInteractionSocketDTO socket)
        {
            socket = default;
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            int field = 0;
            int start = 0;
            double x = 0d;
            double y = 0d;
            double z = 0d;
            float radius = 0f;
            uint id = 0u;

            for (int i = 0; i <= line.Length; i++)
            {
                if (i < line.Length && line[i] != (byte)',')
                    continue;

                ReadOnlySpan<byte> token = Trim(line.Slice(start, i - start));
                if (field == 0)
                {
                    id = HashToken(token);
                }
                else if (field == 1)
                {
                    if (!TryParseDouble(token, out x))
                        return false;
                }
                else if (field == 2)
                {
                    if (!TryParseDouble(token, out y))
                        return false;
                }
                else if (field == 3)
                {
                    if (!TryParseDouble(token, out z))
                        return false;
                }
                else if (field == 4)
                {
                    if (!TryParseFloat(token, out radius))
                        return false;
                }

                field++;
                start = i + 1;
            }

            if (field < 5 || radius <= 0f)
                return false;

            socket.SocketAUP = new double3(x, y, z);
            socket.Orientation = quaternion.identity;
            socket.Normal = new float3(0f, 1f, 0f);
            socket.SnapRadiusMeters = radius;
            socket.SocketId = id;
            socket.Flags = VRInteractionKinematicBridgeConstants.SocketFlagActive;
            return VRInteractionKinematicBridgeMath.IsFinite(socket.SocketAUP);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length;
            while (start < end && token[start] <= (byte)' ')
                start++;
            while (end > start && token[end - 1] <= (byte)' ')
                end--;
            return token.Slice(start, end - start);
        }

        private static uint HashToken(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
                hash = (hash ^ token[i]) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (!TryParseDouble(token, out double parsed))
                return false;

            value = (float)parsed;
            return math.isfinite(value);
        }

        private static bool TryParseDouble(ReadOnlySpan<byte> token, out double value)
        {
            value = 0d;
            if (token.Length == 0)
                return false;

            int index = 0;
            double sign = 1d;
            if (token[index] == (byte)'-')
            {
                sign = -1d;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            double integer = 0d;
            bool hasDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                integer = integer * 10d + (token[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            double fractional = 0d;
            double scale = 1d;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    fractional = fractional * 10d + (token[index] - (byte)'0');
                    scale *= 10d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit || index != token.Length)
                return false;

            value = sign * (integer + fractional / scale);
            return math.isfinite(value);
        }
    }

    public static class VRInteractionKinematicBridgeMath
    {
        private const float InvEncodedByteMax = 0.0039215686274509803f;
        private const float MinimumDeltaTime = 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveIterationCount(float globalQualityWeight)
        {
            return ResolveAuthoritativeIterationCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveAuthoritativeIterationCount()
        {
            return VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveQualityIterationHint(float globalQualityWeight)
        {
            float q = Sanitize01(globalQualityWeight, 1f);
            return math.clamp(
                (int)math.round(math.lerp(
                    VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow,
                    VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra,
                    q)),
                VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow,
                VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra);
        }

        public static bool TryResolveRuntimeAup(Vector3 runtimePosition, double3 runtimeOriginAup, out double3 aup)
        {
            aup = double3.zero;
            if (!IsFinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z)) ||
                !IsFinite(runtimeOriginAup))
            {
                return false;
            }

            aup = runtimeOriginAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return IsFinite(aup);
        }

        public static bool TryResolveRuntimePosition(double3 aup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!IsFinite(aup))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double3 delta = aup - origin;
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!IsFinite(local))
                return false;

            runtimePosition = new Vector3(local.x, local.y, local.z);
            return true;
        }

        public static bool TryResolveRuntimePosition(double3 aup, double3 runtimeOriginAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!IsFinite(aup) || !IsFinite(runtimeOriginAup))
                return false;

            double3 delta = aup - runtimeOriginAup;
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!IsFinite(local))
                return false;

            runtimePosition = new Vector3(local.x, local.y, local.z);
            return true;
        }

        public static bool TryIngestControllerMatrix(
            in VRControllerMatrixDTO input,
            int handIndex,
            out VRHandStateDTO state)
        {
            state = default;
            float4 translation = input.ControllerLocalToWorld.c3;
            float3 runtimePosition = new float3(translation.x, translation.y, translation.z);
            if (input.IsTracked == 0 ||
                !IsFinite(runtimePosition) ||
                !IsFinite(input.PlayerRootAUP))
            {
                state.InteractionFlags = VRInteractionKinematicBridgeConstants.StateFlagNonFinite;
                return false;
            }

            double3 rawAup = input.PlayerRootAUP + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            state.RawControllerAUP = rawAup;
            state.ResolvedHandAUP = rawAup;
            state.Velocity = float3.zero;
            state.InteractionFlags =
                input.Flags |
                VRInteractionKinematicBridgeConstants.StateFlagValid |
                VRInteractionKinematicBridgeConstants.StateFlagNoPhysicsProxy;
            if (handIndex == VRInteractionKinematicBridgeConstants.LeftHandIndex)
                state.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagLeftHand;

            return IsFinite(rawAup);
        }

        public static VRHandStateDTO ResolveHand(
            VRHandStateDTO input,
            VRHandStateDTO previous,
            NativeArray<byte> encodedSdf,
            NativeArray<VRInteractionSocketDTO> sockets,
            int socketCount,
            in VRInteractionTuningDTO tuning,
            int handIndex,
            float deltaTime,
            out float maxPenetration,
            out float3 surfaceNormal,
            out uint socketId,
            out int iterations)
        {
            maxPenetration = 0f;
            surfaceNormal = new float3(0f, 1f, 0f);
            socketId = 0u;
            iterations = ResolveIterationCount(tuning.GlobalQualityWeight);

            VRHandStateDTO result = input;
            result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagNoPhysicsProxy;
            if (handIndex == VRInteractionKinematicBridgeConstants.LeftHandIndex)
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagLeftHand;

            if (!IsFinite(input.RawControllerAUP))
            {
                result.RawControllerAUP = previous.ResolvedHandAUP;
                result.ResolvedHandAUP = IsFinite(previous.ResolvedHandAUP) ? previous.ResolvedHandAUP : double3.zero;
                result.Velocity = float3.zero;
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagNonFinite;
                return result;
            }

            double3 rootAup = IsFinite(tuning.PlayerRootAUP) ? tuning.PlayerRootAUP : double3.zero;
            double3 resolvedAup = input.RawControllerAUP;
            float3 rootLocal = ToLocalFloat3(resolvedAup, rootAup);
            float3 shoulderLocal = IsFinite(tuning.ShoulderAUP) ? ToLocalFloat3(tuning.ShoulderAUP, rootAup) : float3.zero;
            float maxArm = math.max(0.05f, tuning.MaxArmLengthMeters);
            float3 shoulderDelta = rootLocal - shoulderLocal;
            float armDistanceSq = math.lengthsq(shoulderDelta);
            float maxArmSq = maxArm * maxArm;
            if (math.isfinite(armDistanceSq) && armDistanceSq > maxArmSq)
            {
                rootLocal = shoulderLocal + shoulderDelta * math.rsqrt(math.max(armDistanceSq, 0.000001f)) * maxArm;
                resolvedAup = rootAup + new double3(rootLocal.x, rootLocal.y, rootLocal.z);
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagArmClamped;
            }

            bool sdfValid = SdfIsValid(encodedSdf, tuning);
            if (((tuning.Flags & VRInteractionKinematicBridgeConstants.TuningFlagSdfEnabled) != 0u) && sdfValid)
            {
                float radius = math.max(0.005f, tuning.HandRadiusMeters);
                for (int i = 0; i < iterations; i++)
                {
                    if (!TrySampleSdf(encodedSdf, tuning, resolvedAup, out float distance))
                        break;

                    float penetration = radius - distance;
                    if (!math.isfinite(penetration) || penetration <= 0f)
                        break;

                    float3 normal = ResolveSdfGradient(encodedSdf, tuning, resolvedAup);
                    if (!IsFinite(normal) || math.lengthsq(normal) <= 0.000001f)
                        normal = surfaceNormal;

                    double3 push = new double3(normal.x, normal.y, normal.z) * penetration;
                    resolvedAup += push;
                    surfaceNormal = normal;
                    maxPenetration = math.max(maxPenetration, penetration);
                    result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagSdfResolved;
                }
            }
            else
            {
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagSdfUnavailable;
            }

            if (((tuning.Flags & VRInteractionKinematicBridgeConstants.TuningFlagSocketSnapEnabled) != 0u) &&
                TrySnapToSocket(resolvedAup, sockets, socketCount, tuning, out double3 snappedAup, out socketId, out float3 socketNormal))
            {
                resolvedAup = snappedAup;
                surfaceNormal = socketNormal;
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagSocketSnapped;
            }

            result.ResolvedHandAUP = IsFinite(resolvedAup) ? resolvedAup : input.RawControllerAUP;
            result.Velocity = ResolveVelocity(previous.ResolvedHandAUP, result.ResolvedHandAUP, deltaTime);
            result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagValid;

            float speedSq = math.lengthsq(result.Velocity);
            float threshold = math.max(0f, tuning.VelocitySignalThreshold);
            if (((tuning.Flags & VRInteractionKinematicBridgeConstants.TuningFlagVelocitySignalEnabled) != 0u) &&
                math.isfinite(speedSq) &&
                speedSq > threshold * threshold)
            {
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagVelocitySignal;
            }

            if (!IsFinite(result.ResolvedHandAUP) || !IsFinite(result.Velocity))
            {
                result.ResolvedHandAUP = IsFinite(previous.ResolvedHandAUP) ? previous.ResolvedHandAUP : input.RawControllerAUP;
                result.Velocity = float3.zero;
                result.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagNonFinite;
            }

            return result;
        }

        public static uint HashState(in VRHandStateDTO state, uint handIndex)
        {
            uint hash = 2166136261u;
            MixQuantizedAup(ref hash, state.RawControllerAUP);
            MixQuantizedAup(ref hash, state.ResolvedHandAUP);
            MixQuantizedVelocity(ref hash, state.Velocity);
            hash = Mix(hash, state.InteractionFlags);
            hash = Mix(hash, handIndex);
            return hash;
        }

        public static bool TryEvaluateSocketSnap(
            double3 resolvedAup,
            NativeArray<VRInteractionSocketDTO> sockets,
            int socketCount,
            in VRInteractionTuningDTO tuning,
            out double3 snappedAup,
            out uint socketId,
            out float3 socketNormal)
        {
            return TrySnapToSocket(resolvedAup, sockets, socketCount, in tuning, out snappedAup, out socketId, out socketNormal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixQuantizedAup(ref uint hash, double3 value)
        {
            hash = Mix(hash, QuantizeMillimeters(value.x));
            hash = Mix(hash, QuantizeMillimeters(value.y));
            hash = Mix(hash, QuantizeMillimeters(value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixQuantizedVelocity(ref uint hash, float3 value)
        {
            hash = Mix(hash, QuantizeMillimeters(value.x));
            hash = Mix(hash, QuantizeMillimeters(value.y));
            hash = Mix(hash, QuantizeMillimeters(value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizeMillimeters(double value)
        {
            if (!math.isfinite(value))
                return 0u;

            double scaled = math.clamp(value * 1000d, -2147483648d, 2147483647d);
            return (uint)(int)math.round(scaled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizeMillimeters(float value)
        {
            if (!math.isfinite(value))
                return 0u;

            float scaled = math.clamp(value * 1000f, -2147483648f, 2147483647f);
            return (uint)(int)math.round(scaled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToLocalFloat3(double3 aup, double3 originAup)
        {
            double3 delta = aup - originAup;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveVelocity(double3 previousAup, double3 currentAup, float deltaTime)
        {
            if (!IsFinite(previousAup) || !IsFinite(currentAup) || deltaTime <= 0f)
                return float3.zero;

            double3 delta = currentAup - previousAup;
            float invDt = math.rcp(math.max(MinimumDeltaTime, deltaTime));
            float3 velocity = new float3((float)delta.x, (float)delta.y, (float)delta.z) * invDt;
            return IsFinite(velocity) ? velocity : float3.zero;
        }

        private static bool SdfIsValid(NativeArray<byte> encodedSdf, in VRInteractionTuningDTO tuning)
        {
            if (!encodedSdf.IsCreated ||
                tuning.SdfDimensions.x <= 1 ||
                tuning.SdfDimensions.y <= 1 ||
                tuning.SdfDimensions.z <= 1 ||
                !IsFinite(tuning.SdfOriginAUP) ||
                !IsFinite(tuning.SdfCellSize) ||
                tuning.SdfRangeMeters <= 0f)
            {
                return false;
            }

            long expected =
                (long)tuning.SdfDimensions.x *
                tuning.SdfDimensions.y *
                tuning.SdfDimensions.z;
            return expected > 0L &&
                   expected <= int.MaxValue &&
                   encodedSdf.Length >= expected;
        }

        private static bool TrySampleSdf(NativeArray<byte> encodedSdf, in VRInteractionTuningDTO tuning, double3 aup, out float distance)
        {
            distance = tuning.SdfRangeMeters;
            if (!SdfIsValid(encodedSdf, tuning) || !IsFinite(aup))
                return false;

            float3 local = ToLocalFloat3(aup, tuning.SdfOriginAUP);
            float3 cell = math.max(tuning.SdfCellSize, new float3(0.0001f));
            float3 sample = local / cell;
            if (sample.x < 0f ||
                sample.y < 0f ||
                sample.z < 0f ||
                sample.x > tuning.SdfDimensions.x - 1f ||
                sample.y > tuning.SdfDimensions.y - 1f ||
                sample.z > tuning.SdfDimensions.z - 1f)
            {
                return false;
            }

            sample = math.clamp(sample, float3.zero, new float3(
                tuning.SdfDimensions.x - 1.001f,
                tuning.SdfDimensions.y - 1.001f,
                tuning.SdfDimensions.z - 1.001f));

            int3 p0 = new int3((int)math.floor(sample.x), (int)math.floor(sample.y), (int)math.floor(sample.z));
            int3 p1 = math.min(p0 + 1, tuning.SdfDimensions - 1);
            float3 t = sample - p0;

            float c000 = DecodeSdf(encodedSdf, tuning, p0.x, p0.y, p0.z);
            float c100 = DecodeSdf(encodedSdf, tuning, p1.x, p0.y, p0.z);
            float c010 = DecodeSdf(encodedSdf, tuning, p0.x, p1.y, p0.z);
            float c110 = DecodeSdf(encodedSdf, tuning, p1.x, p1.y, p0.z);
            float c001 = DecodeSdf(encodedSdf, tuning, p0.x, p0.y, p1.z);
            float c101 = DecodeSdf(encodedSdf, tuning, p1.x, p0.y, p1.z);
            float c011 = DecodeSdf(encodedSdf, tuning, p0.x, p1.y, p1.z);
            float c111 = DecodeSdf(encodedSdf, tuning, p1.x, p1.y, p1.z);

            float c00 = math.lerp(c000, c100, t.x);
            float c10 = math.lerp(c010, c110, t.x);
            float c01 = math.lerp(c001, c101, t.x);
            float c11 = math.lerp(c011, c111, t.x);
            float c0 = math.lerp(c00, c10, t.y);
            float c1 = math.lerp(c01, c11, t.y);
            distance = math.lerp(c0, c1, t.z);
            return math.isfinite(distance);
        }

        private static float3 ResolveSdfGradient(NativeArray<byte> encodedSdf, in VRInteractionTuningDTO tuning, double3 aup)
        {
            float3 cell = math.max(tuning.SdfCellSize, new float3(0.0001f));
            double3 dx = new double3(cell.x, 0d, 0d);
            double3 dy = new double3(0d, cell.y, 0d);
            double3 dz = new double3(0d, 0d, cell.z);

            TrySampleSdf(encodedSdf, tuning, aup + dx, out float px);
            TrySampleSdf(encodedSdf, tuning, aup - dx, out float nx);
            TrySampleSdf(encodedSdf, tuning, aup + dy, out float py);
            TrySampleSdf(encodedSdf, tuning, aup - dy, out float ny);
            TrySampleSdf(encodedSdf, tuning, aup + dz, out float pz);
            TrySampleSdf(encodedSdf, tuning, aup - dz, out float nz);

            float3 gradient = new float3(px - nx, py - ny, pz - nz);
            float lengthSq = math.lengthsq(gradient);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return new float3(0f, 1f, 0f);

            return gradient * math.rsqrt(lengthSq);
        }

        private static float DecodeSdf(NativeArray<byte> encodedSdf, in VRInteractionTuningDTO tuning, int x, int y, int z)
        {
            long indexLong = ((long)z * tuning.SdfDimensions.y + y) * tuning.SdfDimensions.x + x;
            if (indexLong < 0L || indexLong >= encodedSdf.Length)
                return tuning.SdfRangeMeters;

            return ((encodedSdf[(int)indexLong] * InvEncodedByteMax) * 2f - 1f) * tuning.SdfRangeMeters;
        }

        private static bool TrySnapToSocket(
            double3 resolvedAup,
            NativeArray<VRInteractionSocketDTO> sockets,
            int socketCount,
            in VRInteractionTuningDTO tuning,
            out double3 snappedAup,
            out uint socketId,
            out float3 socketNormal)
        {
            snappedAup = resolvedAup;
            socketId = 0u;
            socketNormal = new float3(0f, 1f, 0f);
            if (!sockets.IsCreated || socketCount <= 0 || !IsFinite(resolvedAup))
                return false;

            int limit = math.min(socketCount, sockets.Length);
            float scale = math.max(0.05f, tuning.SnapRadiusScale);
            float bestSq = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < limit; i++)
            {
                VRInteractionSocketDTO socket = sockets[i];
                if ((socket.Flags & VRInteractionKinematicBridgeConstants.SocketFlagActive) == 0u ||
                    !IsFinite(socket.SocketAUP))
                {
                    continue;
                }

                float radius = math.max(0.005f, socket.SnapRadiusMeters * scale);
                double3 delta = resolvedAup - socket.SocketAUP;
                float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                float distSq = math.lengthsq(local);
                if (math.isfinite(distSq) && distSq <= radius * radius && distSq < bestSq)
                {
                    bestSq = distSq;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return false;

            VRInteractionSocketDTO best = sockets[bestIndex];
            snappedAup = best.SocketAUP;
            socketId = best.SocketId;
            socketNormal = IsFinite(best.Normal) && math.lengthsq(best.Normal) > 0.000001f
                ? math.normalize(best.Normal)
                : new float3(0f, 1f, 0f);
            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockVRInputsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VRControllerMatrixDTO> ControllerMatrices;
        public double3 PlayerRootAUP;
        public uint FrameIndex;
        public float GlobalQualityWeight;

        public void Execute(int handIndex)
        {
            if (!ControllerMatrices.IsCreated || (uint)handIndex >= (uint)ControllerMatrices.Length)
                return;

            float frame = (float)(FrameIndex & 4095u);
            float side = handIndex == VRInteractionKinematicBridgeConstants.LeftHandIndex ? -1f : 1f;
            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float jitter = math.lerp(0.035f, 0.12f, q);
            float3 local = new float3(
                side * (0.24f + Triangle01(frame * 0.037f + handIndex * 0.33f) * jitter),
                1.14f + Triangle01(frame * 0.023f + handIndex * 0.17f) * 0.1f,
                0.42f + Triangle01(frame * 0.031f + handIndex * 0.41f) * 0.16f);

            float4x4 matrix = float4x4.identity;
            matrix.c3 = new float4(local, 1f);

            VRControllerMatrixDTO dto = default;
            dto.ControllerLocalToWorld = matrix;
            dto.PlayerRootAUP = PlayerRootAUP;
            dto.ShoulderRuntimeOffset = new float3(side * 0.18f, 1.38f, 0.08f);
            dto.Grip01 = Triangle01(frame * 0.017f + handIndex * 0.5f);
            dto.Flags = VRInteractionKinematicBridgeConstants.StateFlagValid | VRInteractionKinematicBridgeConstants.StateFlagTracked;
            dto.FrameIndex = FrameIndex;
            dto.HandIndex = (byte)handIndex;
            dto.IsTracked = 1;
            ControllerMatrices[handIndex] = dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Triangle01(float value)
        {
            float f = value - math.floor(value);
            return 1f - math.abs(f * 2f - 1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct IngestVRControllerInputJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<VRControllerMatrixDTO> ControllerMatrices;
        [NoAlias] public NativeArray<VRHandStateDTO> HandStates;

        public void Execute(int handIndex)
        {
            if (!ControllerMatrices.IsCreated ||
                !HandStates.IsCreated ||
                (uint)handIndex >= (uint)ControllerMatrices.Length ||
                (uint)handIndex >= (uint)HandStates.Length)
            {
                return;
            }

            VRControllerMatrixDTO input = ControllerMatrices[handIndex];
            if (!VRInteractionKinematicBridgeMath.TryIngestControllerMatrix(in input, handIndex, out VRHandStateDTO state))
            {
                state.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagNonFinite;
                HandStates[handIndex] = state;
                return;
            }

            HandStates[handIndex] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ResolveSdfHandCollisionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<byte> EncodedSdf;
        [ReadOnly, NoAlias] public NativeArray<VRInteractionSocketDTO> Sockets;
        [ReadOnly, NoAlias] public NativeArray<VRHandStateDTO> PreviousHandStates;
        [NoAlias] public NativeArray<VRHandStateDTO> HandStates;
        public VRInteractionTuningDTO Tuning;
        public int SocketCount;
        public float DeltaTime;

        public void Execute(int handIndex)
        {
            if (!HandStates.IsCreated ||
                !PreviousHandStates.IsCreated ||
                (uint)handIndex >= (uint)HandStates.Length ||
                (uint)handIndex >= (uint)PreviousHandStates.Length)
            {
                return;
            }

            VRHandStateDTO resolved = VRInteractionKinematicBridgeMath.ResolveHand(
                HandStates[handIndex],
                PreviousHandStates[handIndex],
                EncodedSdf,
                Sockets,
                SocketCount,
                in Tuning,
                handIndex,
                DeltaTime,
                out _,
                out _,
                out _,
                out _);

            HandStates[handIndex] = resolved;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateInteractionSnappingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<VRInteractionSocketDTO> Sockets;
        [NoAlias] public NativeArray<VRHandStateDTO> HandStates;
        public VRInteractionTuningDTO Tuning;
        public int SocketCount;

        public void Execute(int handIndex)
        {
            if (!HandStates.IsCreated || (uint)handIndex >= (uint)HandStates.Length)
                return;

            VRHandStateDTO state = HandStates[handIndex];
            if (!VRInteractionKinematicBridgeMath.TryEvaluateSocketSnap(
                    state.ResolvedHandAUP,
                    Sockets,
                    SocketCount,
                    in Tuning,
                    out double3 snappedAup,
                    out _,
                    out _))
            {
                return;
            }

            state.ResolvedHandAUP = snappedAup;
            state.InteractionFlags |= VRInteractionKinematicBridgeConstants.StateFlagSocketSnapped;
            HandStates[handIndex] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ComposeResolvedHandMatricesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<VRHandStateDTO> HandStates;
        [NoAlias] public NativeArray<float4x4> HandMatrices;
        public double3 RuntimeOriginAUP;

        public void Execute(int handIndex)
        {
            if (!HandStates.IsCreated ||
                !HandMatrices.IsCreated ||
                (uint)handIndex >= (uint)HandStates.Length ||
                (uint)handIndex >= (uint)HandMatrices.Length)
            {
                return;
            }

            double3 delta = HandStates[handIndex].ResolvedHandAUP - RuntimeOriginAUP;
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!VRInteractionKinematicBridgeMath.IsFinite(local))
                local = float3.zero;

            float4x4 matrix = float4x4.identity;
            matrix.c3 = new float4(local, 1f);
            HandMatrices[handIndex] = matrix;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordVRInteractionTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<VRHandStateDTO> HandStates;
        [NoAlias] public NativeArray<VRInteractionTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint FrameIndex;
        public uint CpuTimeMicros;
        public uint SolverIterations;

        public void Execute()
        {
            if (!HandStates.IsCreated ||
                !TelemetryRing.IsCreated ||
                TelemetryRing.Length < VRInteractionKinematicBridgeConstants.TelemetryCapacity)
            {
                return;
            }

            int baseSlot = (int)(FrameIndex % VRInteractionKinematicBridgeConstants.TelemetryFrameCapacity) * VRInteractionKinematicBridgeConstants.HandCount;
            for (int handIndex = 0; handIndex < VRInteractionKinematicBridgeConstants.HandCount; handIndex++)
            {
                if ((uint)handIndex >= (uint)HandStates.Length)
                    continue;

                VRHandStateDTO state = HandStates[handIndex];
                VRInteractionTelemetryEntry entry = default;
                entry.FrameIndex = FrameIndex;
                entry.StateHash = VRInteractionKinematicBridgeMath.HashState(in state, (uint)handIndex);
                entry.Flags = state.InteractionFlags;
                if (CpuTimeMicros > 100u)
                    entry.Flags |= VRInteractionKinematicBridgeConstants.TelemetryFlagBudgetExceeded;
                entry.CpuTimeMicros = CpuTimeMicros;
                entry.RawControllerAUP = state.RawControllerAUP;
                entry.ResolvedHandAUP = state.ResolvedHandAUP;
                entry.Velocity = state.Velocity;
                entry.SurfaceNormal = new float3(0f, 1f, 0f);
                entry.SolverIterations = SolverIterations;
                entry.HandIndex = (uint)handIndex;
                entry.Marker = VRInteractionKinematicBridgeConstants.TelemetryMarker;
                TelemetryRing[baseSlot + handIndex] = entry;
            }

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = baseSlot;
        }
    }
}
