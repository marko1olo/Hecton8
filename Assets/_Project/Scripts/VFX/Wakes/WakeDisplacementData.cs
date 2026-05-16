using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX.Wakes
{
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]
    public struct WakeSource
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 PositionWS;
        [FieldOffset(60)] public float3 TargetWS;
        [FieldOffset(72)] public float3 VelocityWS;
        [FieldOffset(84)] public float Radius;
        [FieldOffset(88)] public float Intensity;
        [FieldOffset(92)] public float AgeSeconds;
        [FieldOffset(96)] public uint SourceFlags;
        [FieldOffset(100)] public uint FrameStamp;
        [FieldOffset(104)] public byte SourceKind;
        [FieldOffset(105)] public byte Active;
        [FieldOffset(106)] public ushort Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct WakeTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort ActiveWakeSourcesCount;
        [FieldOffset(6)] public ushort SlotLimit;
        [FieldOffset(8)] public float3 StrongestWakePositionWS;
        [FieldOffset(20)] public float StrongestIntensity;
        [FieldOffset(24)] public float3 StrongestVelocityWS;
        [FieldOffset(36)] public float MaxRadius;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public uint DataVaultGeneration;
        [FieldOffset(52)] public uint AupShiftSequence;
        [FieldOffset(56)] public float SystemStress01;
        [FieldOffset(60)] public float LowTier01;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WakeDecayJob : IJobParallelFor
    {
        public NativeArray<WakeSource> WakeSources;
        public float DeltaTime;
        public float DecayRate;
        public int SlotLimit;

        public void Execute(int index)
        {
            if (index >= SlotLimit)
            {
                WakeSources[index] = default;
                return;
            }

            WakeSource source = WakeSources[index];
            if (source.Active == 0)
                return;

            float safeDeltaTime = math.isfinite(DeltaTime) ? math.clamp(DeltaTime, 0f, 0.25f) : 0f;
            float safeDecayRate = math.isfinite(DecayRate) ? math.max(0f, DecayRate) : 0f;
            float decay = math.exp(-safeDeltaTime * safeDecayRate);
            source.Intensity = math.max(0f, source.Intensity * decay);
            source.AgeSeconds = math.isfinite(source.AgeSeconds)
                ? source.AgeSeconds + safeDeltaTime
                : safeDeltaTime;

            if (source.Intensity <= 0.0001f ||
                !math.isfinite(source.Radius) ||
                !math.isfinite(source.Intensity) ||
                !math.all(math.isfinite(source.PositionWS)) ||
                !math.all(math.isfinite(source.TargetWS)) ||
                !math.all(math.isfinite(source.VelocityWS)))
            {
                source = default;
            }

            WakeSources[index] = source;
        }
    }
}
