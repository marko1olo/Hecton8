using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [Flags]
    public enum InputBlockMaskFlags : uint
    {
        None = 0u,
        BlockMovement = 1u << 0,
        BlockLook = 1u << 1,
        BlockTools = 1u << 2,
        BlockDiscrete = 1u << 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct InputStateDTO
    {
        [FieldOffset(0)] public float2 LookDelta;
        [FieldOffset(8)] public float2 MoveAxis;
        [FieldOffset(16)] public uint ButtonMask;
        [FieldOffset(20)] private uint _pad0;
    }

    public static class PredictedInputFlags
    {
        public const uint None = 0u;
        public const uint Local = 1u << 0;
        public const uint Remote = 1u << 1;
        public const uint Predicted = 1u << 2;
        public const uint Authoritative = 1u << 3;
        public const uint ExtrapolatedDearLie = 1u << 4;
        public const uint HasTargetAup = 1u << 5;
        public const uint NonFiniteSanitized = 1u << 6;
        public const uint MockGenerated = 1u << 7;
        public const uint Valid = 1u << 31;
    }

    /// <summary>
    /// ARM64-aligned unmanaged input prediction slot consumed by rollback.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PredictedInputDTO
    {
        [FieldOffset(0)] public uint TickNumber;
        [FieldOffset(4)] public float3 LocalMoveVector;
        [FieldOffset(16)] public float2 LookDelta;
        [FieldOffset(24)] public uint ActionButtonsMask;
        [FieldOffset(28)] public uint _pad0;
    }

    /// <summary>
    /// Parallel AUP payload for targeted predicted inputs. Kept separate to preserve the 32-byte input ABI.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PredictedInputAupTargetDTO
    {
        [FieldOffset(0)] public uint TickNumber;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public double3 TargetAupAbsolute;
    }

    /// <summary>
    /// Three-hundred-frame black-box record for cooperative input prediction.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InputPredictionTelemetryEntry
    {
        [FieldOffset(0)] public uint TickNumber;
        [FieldOffset(4)] public uint PredictedInputCount;
        [FieldOffset(8)] public uint DesyncCount;
        [FieldOffset(12)] public uint PacketRedundancyCount;
        [FieldOffset(16)] public uint BurstExecutionMicroseconds;
        [FieldOffset(20)] public uint BufferCapacity;
        [FieldOffset(24)] public uint ExtrapolatedInputCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong LastPredictedHash64;
        [FieldOffset(40)] public ulong LastAuthoritativeHash64;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public float LatencyFactor01;
        [FieldOffset(56)] public uint WriteIndex;
        [FieldOffset(60)] public uint _pad0;
    }

    public static class PredictedInputLayoutGuard
    {
        public static uint Validate()
        {
            uint mask = 0u;
            mask |= UnsafeUtility.SizeOf<PredictedInputDTO>() == 32 ? 0u : 1u << 0;
            mask |= OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.TickNumber)) == 0 ? 0u : 1u << 1;
            mask |= OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.LocalMoveVector)) == 4 ? 0u : 1u << 2;
            mask |= OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.LookDelta)) == 16 ? 0u : 1u << 3;
            mask |= OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.ActionButtonsMask)) == 24 ? 0u : 1u << 4;
            mask |= OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO._pad0)) == 28 ? 0u : 1u << 5;
            mask |= UnsafeUtility.SizeOf<PredictedInputAupTargetDTO>() == 32 ? 0u : 1u << 6;
            mask |= OffsetOf<PredictedInputAupTargetDTO>(nameof(PredictedInputAupTargetDTO.TargetAupAbsolute)) == 8 ? 0u : 1u << 7;
            mask |= UnsafeUtility.SizeOf<InputPredictionTelemetryEntry>() == 64 ? 0u : 1u << 8;
            return mask;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    public static unsafe class PredictedInputRingWriter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLocalInput(
            NativeArray<PredictedInputDTO> predictedInputs,
            NativeArray<PredictedInputAupTargetDTO> targetAups,
            in InputStateDTO sourceInput,
            in double3 targetAupAbsolute,
            uint tickNumber,
            uint targetFlags)
        {
            if (!predictedInputs.IsCreated || predictedInputs.Length <= 0)
                return;

            PredictedInputDTO input = default;
            input.TickNumber = tickNumber;
            input.LocalMoveVector = new float3(sourceInput.MoveAxis.x, 0f, sourceInput.MoveAxis.y);
            input.LookDelta = sourceInput.LookDelta;
            input.ActionButtonsMask = sourceInput.ButtonMask;
            input._pad0 = PredictedInputFlags.Local | PredictedInputFlags.Predicted | PredictedInputFlags.Valid;

            if (!math.all(math.isfinite(input.LocalMoveVector)) || !math.all(math.isfinite(input.LookDelta)))
            {
                input.LocalMoveVector = float3.zero;
                input.LookDelta = float2.zero;
                input._pad0 |= PredictedInputFlags.NonFiniteSanitized;
            }

            int index = (int)(tickNumber % (uint)predictedInputs.Length);
            PredictedInputDTO* basePtr = (PredictedInputDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(predictedInputs);
            basePtr[index] = input;

            if (!targetAups.IsCreated || targetAups.Length <= 0)
                return;

            PredictedInputAupTargetDTO target = default;
            target.TickNumber = tickNumber;
            target.Flags = (targetFlags & PredictedInputFlags.HasTargetAup) != 0u &&
                math.all(math.isfinite(targetAupAbsolute))
                ? PredictedInputFlags.HasTargetAup | PredictedInputFlags.Valid
                : PredictedInputFlags.None;
            target.TargetAupAbsolute = target.Flags != 0u ? targetAupAbsolute : double3.zero;
            targetAups[(int)(tickNumber % (uint)targetAups.Length)] = target;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct QueueLocalInputJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<PredictedInputDTO> PredictedInputs;
        [WriteOnly, NoAlias] public NativeArray<PredictedInputAupTargetDTO> TargetAups;
        public InputStateDTO SourceInput;
        public double3 TargetAupAbsolute;
        public uint TickNumber;
        public uint TargetFlags;

        public void Execute()
        {
            PredictedInputRingWriter.WriteLocalInput(PredictedInputs, TargetAups, in SourceInput, in TargetAupAbsolute, TickNumber, TargetFlags);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GetHistoricalInputJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<PredictedInputDTO> PredictedInputs;
        [WriteOnly, NoAlias] public NativeArray<PredictedInputDTO> Output;
        public uint TargetTick;

        public void Execute()
        {
            if (!PredictedInputs.IsCreated || !Output.IsCreated || PredictedInputs.Length <= 0 || Output.Length <= 0)
                return;

            PredictedInputDTO* basePtr = (PredictedInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(PredictedInputs);
            PredictedInputDTO* input = basePtr + (TargetTick % (uint)PredictedInputs.Length);
            Output[0] = UnsafeUtility.AsRef<PredictedInputDTO>(input);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializePredictedInputRingJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<PredictedInputDTO> PredictedInputs;
        [WriteOnly, NoAlias] public NativeArray<PredictedInputAupTargetDTO> TargetAups;
        public uint StartTick;
        public uint DefaultFlags;

        public void Execute()
        {
            if (!PredictedInputs.IsCreated || PredictedInputs.Length <= 0)
                return;

            uint flags = (DefaultFlags | PredictedInputFlags.Predicted | PredictedInputFlags.Valid) &
                ~PredictedInputFlags.HasTargetAup;
            PredictedInputDTO* inputPtr = (PredictedInputDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(PredictedInputs);
            for (int i = 0; i < PredictedInputs.Length; i++)
            {
                PredictedInputDTO input = default;
                input.TickNumber = StartTick + (uint)i;
                input.LocalMoveVector = float3.zero;
                input.LookDelta = float2.zero;
                input.ActionButtonsMask = 0u;
                input._pad0 = flags;
                inputPtr[i] = input;
            }

            if (!TargetAups.IsCreated || TargetAups.Length <= 0)
                return;

            for (int i = 0; i < TargetAups.Length; i++)
            {
                PredictedInputAupTargetDTO target = default;
                target.TickNumber = StartTick + (uint)i;
                target.Flags = PredictedInputFlags.None;
                target.TargetAupAbsolute = double3.zero;
                TargetAups[i] = target;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockInputHistoryJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<PredictedInputDTO> PredictedInputs;
        [WriteOnly, NoAlias] public NativeArray<PredictedInputAupTargetDTO> TargetAups;
        public uint StartTick;
        public uint Count;
        public uint Seed;

        public void Execute()
        {
            if (!PredictedInputs.IsCreated || PredictedInputs.Length <= 0)
                return;

            uint count = math.min(Count, (uint)PredictedInputs.Length);
            uint deterministicSeed = math.hash(new uint3(Seed == 0u ? 0x9E3779B9u : Seed, StartTick, count));
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(deterministicSeed == 0u ? 0x85EBCA6Bu : deterministicSeed);
            PredictedInputDTO* inputPtr = (PredictedInputDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(PredictedInputs);
            for (uint i = 0u; i < count; i++)
            {
                uint tick = StartTick + i;
                uint jitterBits = rng.NextUInt();
                float phase = (tick & 1023u) * 0.017453292f;
                float jitterX = (((jitterBits >> 8) & 255u) * (1f / 127.5f)) - 1f;
                float jitterY = (((jitterBits >> 16) & 255u) * (1f / 127.5f)) - 1f;

                PredictedInputDTO input = default;
                input.TickNumber = tick;
                input.LocalMoveVector = math.normalizesafe(new float3(math.sin(phase) + (jitterX * 0.35f), 0f, math.cos(phase * 1.37f) + (jitterY * 0.35f)));
                input.LookDelta = new float2(jitterY * 18.5f, jitterX * 12.25f);
                input.ActionButtonsMask = ((jitterBits & 7u) == 0u ? 1u : 0u) | ((jitterBits & 31u) == 0u ? 4u : 0u);
                input._pad0 = PredictedInputFlags.Local | PredictedInputFlags.MockGenerated | PredictedInputFlags.Valid;
                inputPtr[(int)(tick % (uint)PredictedInputs.Length)] = input;

                if (TargetAups.IsCreated && TargetAups.Length > 0)
                {
                    PredictedInputAupTargetDTO target = default;
                    target.TickNumber = tick;
                    target.Flags = PredictedInputFlags.HasTargetAup | PredictedInputFlags.Valid;
                    target.TargetAupAbsolute = new double3(100000.0 + tick * 0.125, -420.0 + (jitterBits & 15u), 88000.0 - tick * 0.0625);
                    TargetAups[(int)(tick % (uint)TargetAups.Length)] = target;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HapticCommandDTO
    {
        [FieldOffset(0)] public float LowFreqIntensity;
        [FieldOffset(4)] public float HighFreqIntensity;
        [FieldOffset(8)] public float DecayRate;
        [FieldOffset(12)] public uint MotorMask;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InputProfileDTO
    {
        [FieldOffset(0)] public float InnerDeadzone;
        [FieldOffset(4)] public float OuterDeadzone;
        [FieldOffset(8)] public float MoveExponent;
        [FieldOffset(12)] public float MouseSensitivity;
        [FieldOffset(16)] public float MouseAcceleration;
        [FieldOffset(20)] public float HapticPowerScale;
        [FieldOffset(24)] public float HapticDispatchIntervalSeconds;
        [FieldOffset(28)] public float HapticThermalAmplitudeScale;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InputTelemetryEntryDTO
    {
        [FieldOffset(0)] public double InputSystemTimeSeconds;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint ButtonMask;
        [FieldOffset(20)] public uint CurrentInputSchemeHash;
        [FieldOffset(24)] public uint PollingTimeMicroseconds;
        [FieldOffset(28)] public uint BufferedInputsConsumed;
        [FieldOffset(32)] public ushort HapticCommandsActive;
        [FieldOffset(34)] public ushort Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockCollisionSignal
    {
        [FieldOffset(0)] public float Magnitude01;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockToolEquipSignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Slot;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockPlayerKinematicsSignal
    {
        [FieldOffset(0)] public double2 AupLocalCell;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }
}
