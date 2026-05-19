using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Habitat.Deformation
{
    /// <summary>
    /// Structural integrity constants shared by the runtime, jobs, shader bridge, and editor tuner.
    /// </summary>
    public static class HullIntegrityConstants
    {
        public const int MaxDentCapacity = 512;
        public const int LowTierDentCapacity = 16;
        public const int MediumTierDentCapacity = 64;
        public const int HighTierDentCapacity = 256;
        public const int UltraTierDentCapacity = 512;
        public const int MinShaderDentCapacity = 4;
        public const int MaxShaderDentCapacity = 256;
        public const int TelemetryFrameCapacity = 300;
        public const int MaxMockModuleCapacity = 512;
        public const int MaxDamageSignals = 32;
        public const int MaxMockHullImpactCount = 256;
        public const int MaxBreachJets = 128;

        public const int CounterActiveDentCount = 0;
        public const int CounterWriteCursor = 1;
        public const int CounterPendingDamageCount = 2;
        public const int CounterBreachPending = 3;
        public const int CounterBreachedNodeId = 4;
        public const int CounterBreachedModuleIndex = 5;
        public const int CounterBreachedCount = 6;
        public const int CounterWeakestModuleIndex = 7;
        public const int CounterFaultFlags = 8;
        public const int CounterDentDirty = 9;
        public const int CounterDiscardedImpactCount = 10;
        public const int CounterBreachJetCount = 11;
        public const int CounterMaxObservedDentCount = 12;
        public const int CounterActiveDeformationCount = 13;
        public const int CounterCount = 16;

        public const byte ModuleFlagBreached = 1 << 0;
        public const byte ModuleFlagReinforced = 1 << 1;
        public const byte ModuleFlagSubmarine = 1 << 2;

        public const uint AgentHash = 0x53323048u; // S20H
        public const uint DefaultBaseHash = 0x48384253u; // H8BS
        public const uint DefaultSubmarineHash = 0x48385355u; // H8SU
    }

    /// <summary>
    /// GPU hull dent payload. Layout is exactly 32 bytes: float3 position + radius, float3 normal + depth.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HullDentDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public float Depth;
    }

    /// <summary>
    /// Authoritative visual-impact payload. Layout is exactly 32 bytes:
    /// double3 AUP impact point + float magnitude + uint damage hash.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HullImpactDTO
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint DamageTypeHash;
    }

    /// <summary>
    /// GPU deformation payload. Layout is exactly 64 bytes and uses raw public fields only.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeformationStateDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public float Depth;
        [FieldOffset(32)] public float Age;
        [FieldOffset(36)] public float Severity;
        [FieldOffset(40)] public uint DamageTypeHash;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;

        /// <summary>Returns a mutable reference into vault-owned deformation memory.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref DeformationStateDTO AsRef(NativeArray<DeformationStateDTO> states, int index)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(states);
            void* elementPtr = (byte*)ptr + (index * UnsafeUtility.SizeOf<DeformationStateDTO>());
            return ref UnsafeUtility.AsRef<DeformationStateDTO>(elementPtr);
        }

        /// <summary>Returns the raw address for ref-based mutation helpers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe DeformationStateDTO* AddressOf(ref DeformationStateDTO state)
        {
            return (DeformationStateDTO*)UnsafeUtility.AddressOf(ref state);
        }
    }

    public static class DeformationStateFlags
    {
        public const uint Active = 1u << 0;
        public const uint Breach = 1u << 1;
        public const uint Pressure = 1u << 2;
        public const uint Mock = 1u << 3;
    }

    /// <summary>
    /// Breach-jet instance payload. Layout is exactly 64 bytes for clean cache-line fetch.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BreachJetDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public float Intensity01;
        [FieldOffset(32)] public float Age;
        [FieldOffset(36)] public uint DamageTypeHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public uint Reserved2;
        [FieldOffset(60)] public uint Reserved3;
    }

    /// <summary>
    /// DrawProceduralIndirect argument row. Layout is 16 bytes: verts, instances, start vertex, start instance.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BreachJetIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    /// <summary>
    /// Deformation black-box frame entry. Layout is exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeformationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveDentCount;
        [FieldOffset(8)] public uint DiscardedImpactCount;
        [FieldOffset(12)] public uint BreachJetCount;
        [FieldOffset(16)] public float MaxCrushDepth;
        [FieldOffset(20)] public float MaxDentDepth;
        [FieldOffset(24)] public float GpuUploadMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float3 LastDentLocalPosition;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint FaultFlags;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    /// <summary>
    /// Cold material-strength tuning row. Layout is exactly 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HullMaterialStrengthDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float Plasticity;
        [FieldOffset(8)] public float MaxDentDepth;
        [FieldOffset(12)] public float PressureBuckleThreshold01;
        [FieldOffset(16)] public float RepairRelaxation;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    /// <summary>
    /// Per-base scalar integrity ledger. Layout is exactly 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BaseIntegrityLedgerDTO
    {
        [FieldOffset(0)] public uint BaseHash;
        [FieldOffset(4)] public float TotalSIP;
        [FieldOffset(8)] public float DepthPressure;
        [FieldOffset(12)] public int BreachedNodeCount;
    }

    /// <summary>
    /// Raw mutable module state for Burst jobs. No properties: jobs write CurrentSIP directly.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseModuleStateDTO
    {
        [FieldOffset(0)] public uint NodeId;
        [FieldOffset(4)] public uint ModuleHash;
        [FieldOffset(8)] public float3 LocalCenter;
        [FieldOffset(20)] public float3 LocalNormal;
        [FieldOffset(32)] public float BaseSIP;
        [FieldOffset(36)] public float CurrentSIP;
        [FieldOffset(40)] public float ReinforcementMultiplier;
        [FieldOffset(44)] public float DepthMeters;
        [FieldOffset(48)] public uint BreachFrame;
        [FieldOffset(52)] public float Stress01;
        [FieldOffset(56)] public float PeakStress01;
        [FieldOffset(60)] public ushort Reserved0;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ModuleKind;

        /// <summary>
        /// Returns a mutable reference into the unmanaged module array, preventing CS1612 copies.
        /// </summary>
        /// <param name="modules">Vault-owned module array.</param>
        /// <param name="index">Element index.</param>
        /// <returns>Direct reference to the element in native memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref BaseModuleStateDTO AsRef(NativeArray<BaseModuleStateDTO> modules, int index)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(modules);
            void* elementPtr = (byte*)ptr + (index * UnsafeUtility.SizeOf<BaseModuleStateDTO>());
            return ref UnsafeUtility.AsRef<BaseModuleStateDTO>(elementPtr);
        }
    }

    /// <summary>
    /// Blind WFC base descriptor used when the real generator is absent.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockWFCBaseArray
    {
        [FieldOffset(0)] public uint BaseHash;
        [FieldOffset(4)] public int ModuleOffset;
        [FieldOffset(8)] public int ModuleCount;
        [FieldOffset(12)] public float SipMultiplier;
    }

    /// <summary>
    /// Blind combat payload proving dent generation without a combat-router dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockCombatDamageSignal
    {
        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float Magnitude;
        [FieldOffset(16)] public float3 LocalNormal;
        [FieldOffset(28)] public float Radius;
        [FieldOffset(32)] public uint TargetHash;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint DamageType;
        [FieldOffset(48)] public float Depth;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// Blind pressure-depth payload. Defined partial to allow other agents to extend without direct coupling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockDepthSignal
    {
        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public float DepthMeters;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Seed;
    }

    /// <summary>
    /// Blind repair-laser payload used by the repair job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockRepairLaserSignal
    {
        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public uint TargetHash;
        [FieldOffset(20)] public float DepthPerSecond;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>
    /// Compact breach payload retained for local proof when external flood systems are absent.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockHullBreachSignal
    {
        [FieldOffset(0)] public uint BaseHash;
        [FieldOffset(4)] public uint NodeId;
        [FieldOffset(8)] public uint ModuleHash;
        [FieldOffset(12)] public float Pressure;
        [FieldOffset(16)] public float TotalSIP;
        [FieldOffset(20)] public float3 LocalPoint;
    }

    /// <summary>
    /// Play-mode tuning block edited through the Hull Deformation Tuner and read by jobs.
    /// Layout is exactly 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HullIntegrityTuningDTO
    {
        [FieldOffset(0)] public float BaseSipMultiplier;
        [FieldOffset(4)] public float CrushDepthGradient;
        [FieldOffset(8)] public float DentRadius;
        [FieldOffset(12)] public float DentDepth;
        [FieldOffset(16)] public float MetalPlasticity;
        [FieldOffset(20)] public float MaxDentDepth;
        [FieldOffset(24)] public float PressureBuckleThreshold01;
        [FieldOffset(28)] public float VisualOverkillLimit;
    }

    /// <summary>
    /// Black-box frame entry. Retains the last 300 frames of high-level integrity state.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HullIntegrityTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint BaseHash;
        [FieldOffset(8)] public float AverageBaseSIP;
        [FieldOffset(12)] public float ActiveDentCount;
        [FieldOffset(16)] public float MaxPressureExperienced;
        [FieldOffset(20)] public float TotalSIP;
        [FieldOffset(24)] public float DepthPressure;
        [FieldOffset(28)] public float PressureRatio;
        [FieldOffset(32)] public float3 LastDentLocalPosition;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint WeakestNodeId;
        [FieldOffset(52)] public float LastDentDepth;
        [FieldOffset(56)] public uint DentCount;
        [FieldOffset(60)] public uint StateHash;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityEmergencyMockJob : IJob
    {
        [NoAlias] public NativeArray<BaseModuleStateDTO> Modules;
        [NoAlias] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [NoAlias] public NativeArray<int> Counters;
        public int ModuleCount;
        public uint BaseHash;
        public float SipMultiplier;

        public void Execute()
        {
            int count = math.clamp(ModuleCount, 1, math.min(HullIntegrityConstants.MaxMockModuleCapacity, Modules.Length));
            float safeMultiplier = math.isfinite(SipMultiplier) ? math.max(0.01f, SipMultiplier) : 1f;
            float total = 0f;

            for (int i = 0; i < count; i++)
            {
                int column = i & 7;
                int row = (i >> 3) & 7;
                int deck = i >> 6;
                byte moduleKind = (byte)(i % 5);
                float baseSip = moduleKind == 0 ? 10f : moduleKind == 1 ? 100f : moduleKind == 2 ? 60f : moduleKind == 3 ? 150f : 40f;
                float reinforcement = moduleKind == 3 ? 1.45f : 1f;
                byte flags = moduleKind == 3 ? HullIntegrityConstants.ModuleFlagReinforced : (byte)0;
                float3 center = new float3((column - 3.5f) * 4f, (deck - 1) * 3.2f, (row - 3.5f) * 4f);

                Modules[i] = new BaseModuleStateDTO
                {
                    NodeId = (uint)(i + 1),
                    ModuleHash = HashModule(moduleKind, i),
                    LocalCenter = center,
                    LocalNormal = math.normalizesafe(center, new float3(0f, 1f, 0f)),
                    BaseSIP = baseSip * safeMultiplier,
                    CurrentSIP = baseSip * safeMultiplier,
                    ReinforcementMultiplier = reinforcement,
                    DepthMeters = 0f,
                    Flags = flags,
                    ModuleKind = moduleKind,
                    BreachFrame = 0u,
                    Stress01 = 0f,
                    PeakStress01 = 0f
                };

                total += baseSip * safeMultiplier * reinforcement;
            }

            for (int i = count; i < Modules.Length; i++)
                Modules[i] = default;

            if (Ledger.IsCreated && Ledger.Length > 0)
            {
                Ledger[0] = new BaseIntegrityLedgerDTO
                {
                    BaseHash = BaseHash,
                    TotalSIP = total,
                    DepthPressure = 0f,
                    BreachedNodeCount = 0
                };
            }

            if (Counters.IsCreated && Counters.Length >= HullIntegrityConstants.CounterCount)
            {
                Counters[HullIntegrityConstants.CounterWeakestModuleIndex] = 0;
                Counters[HullIntegrityConstants.CounterBreachedCount] = 0;
                Counters[HullIntegrityConstants.CounterBreachPending] = 0;
                Counters[HullIntegrityConstants.CounterBreachedModuleIndex] = -1;
                Counters[HullIntegrityConstants.CounterFaultFlags] = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashModule(byte moduleKind, int index)
        {
            uint hash = 2166136261u;
            hash = (hash ^ moduleKind) * 16777619u;
            hash = (hash ^ (uint)index) * 16777619u;
            return hash == 0u ? 1u : hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityMockDepthJob : IJob
    {
        [NoAlias] public NativeArray<MockDepthSignal> DepthSignal;
        public uint BaseHash;
        public uint Frame;
        public float BaseDepthMeters;
        public float DepthJitterMeters;

        public void Execute()
        {
            if (!DepthSignal.IsCreated || DepthSignal.Length == 0)
                return;

            uint seed = math.hash(new uint3(BaseHash, Frame, 0xD375u));
            float phase = (seed & 1023u) * (1f / 1023f);
            float triangle = math.abs(phase * 2f - 1f);
            float depth = math.max(0f, BaseDepthMeters + triangle * math.max(0f, DepthJitterMeters));
            if (!math.isfinite(depth))
                depth = 0f;

            DepthSignal[0] = new MockDepthSignal
            {
                TargetHash = BaseHash,
                DepthMeters = depth,
                Frame = Frame,
                Seed = seed
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityDamageJob : IJob
    {
        [NoAlias] public NativeArray<BaseModuleStateDTO> Modules;
        [ReadOnly] [NoAlias] public NativeArray<MockCombatDamageSignal> DamageSignals;
        [NoAlias] public NativeArray<int> Counters;
        public int ModuleCount;
        public int DamageCount;
        public uint BaseHash;
        public float DamageToSipScale;

        public void Execute()
        {
            int moduleCount = math.clamp(ModuleCount, 0, Modules.Length);
            int damageCount = math.clamp(DamageCount, 0, DamageSignals.IsCreated ? DamageSignals.Length : 0);
            if (moduleCount <= 0 || damageCount <= 0)
                return;

            int faultFlags = ReadCounter(HullIntegrityConstants.CounterFaultFlags);
            float safeDamageScale = math.isfinite(DamageToSipScale) ? math.max(0f, DamageToSipScale) : 0f;

            for (int damageIndex = 0; damageIndex < damageCount; damageIndex++)
            {
                MockCombatDamageSignal damage = DamageSignals[damageIndex];
                if (damage.TargetHash != 0u && damage.TargetHash != BaseHash)
                    continue;

                if (!math.all(math.isfinite(damage.LocalPoint)) ||
                    !math.isfinite(damage.Magnitude))
                {
                    faultFlags |= 1;
                    continue;
                }

                int nearestIndex = -1;
                float nearestSq = float.MaxValue;
                for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
                {
                    BaseModuleStateDTO module = Modules[moduleIndex];
                    float3 delta = damage.LocalPoint - module.LocalCenter;
                    float distanceSq = math.lengthsq(delta);
                    if (distanceSq < nearestSq)
                    {
                        nearestSq = distanceSq;
                        nearestIndex = moduleIndex;
                    }
                }

                if (nearestIndex < 0)
                    continue;

                BaseModuleStateDTO target = Modules[nearestIndex];
                float sipLoss = math.max(0f, damage.Magnitude) * safeDamageScale;
                float currentSip = math.isfinite(target.CurrentSIP) ? math.max(0f, target.CurrentSIP) : 0f;
                target.CurrentSIP = math.max(0f, currentSip - sipLoss);
                float baseSip = math.isfinite(target.BaseSIP) ? math.max(target.BaseSIP, 0.0001f) : 0.0001f;
                target.Stress01 = math.saturate(1f - target.CurrentSIP / baseSip);
                float peakStress = math.isfinite(target.PeakStress01) ? math.max(0f, target.PeakStress01) : 0f;
                target.PeakStress01 = math.max(peakStress, target.Stress01);
                Modules[nearestIndex] = target;
            }

            WriteCounter(HullIntegrityConstants.CounterFaultFlags, faultFlags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegritySipAggregationJob : IJob
    {
        [NoAlias] public NativeArray<BaseModuleStateDTO> Modules;
        [NoAlias] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [NoAlias] public NativeArray<int> Counters;
        public int ModuleCount;
        public uint BaseHash;
        public float BaseSipMultiplier;

        public void Execute()
        {
            int count = math.clamp(ModuleCount, 0, Modules.Length);
            float totalSip = 0f;
            int breached = 0;
            int weakestIndex = -1;
            float weakestSip = float.MaxValue;
            int faultFlags = ReadCounter(HullIntegrityConstants.CounterFaultFlags);
            float multiplier = math.isfinite(BaseSipMultiplier) ? math.max(0.01f, BaseSipMultiplier) : 1f;

            for (int i = 0; i < count; i++)
            {
                BaseModuleStateDTO module = Modules[i];
                float currentSip = math.isfinite(module.CurrentSIP) ? math.max(0f, module.CurrentSIP) : 0f;
                float reinforcement = math.isfinite(module.ReinforcementMultiplier) ? math.max(1f, module.ReinforcementMultiplier) : 1f;
                module.CurrentSIP = currentSip;

                if ((module.Flags & HullIntegrityConstants.ModuleFlagBreached) != 0)
                {
                    breached++;
                }
                else if (currentSip < weakestSip)
                {
                    weakestSip = currentSip;
                    weakestIndex = i;
                }

                totalSip += currentSip * reinforcement * multiplier;
                Modules[i] = module;
            }

            if (!math.isfinite(totalSip))
            {
                totalSip = 0f;
                faultFlags |= 2;
            }

            if (Ledger.IsCreated && Ledger.Length > 0)
            {
                BaseIntegrityLedgerDTO previous = Ledger[0];
                Ledger[0] = new BaseIntegrityLedgerDTO
                {
                    BaseHash = BaseHash,
                    TotalSIP = totalSip,
                    DepthPressure = previous.DepthPressure,
                    BreachedNodeCount = breached
                };
            }

            WriteCounter(HullIntegrityConstants.CounterWeakestModuleIndex, weakestIndex);
            WriteCounter(HullIntegrityConstants.CounterBreachedCount, breached);
            WriteCounter(HullIntegrityConstants.CounterFaultFlags, faultFlags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityHydrostaticPressureJob : IJob
    {
        [NoAlias] public NativeArray<BaseModuleStateDTO> Modules;
        [NoAlias] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [ReadOnly] [NoAlias] public NativeArray<MockDepthSignal> DepthSignal;
        [NoAlias] public NativeArray<int> Counters;
        public int ModuleCount;
        public uint Frame;
        public uint BaseHash;
        public float WaterDensity;
        public float Gravity;
        public float CrushDepthGradient;

        public void Execute()
        {
            if (!Ledger.IsCreated || Ledger.Length == 0)
                return;

            int faultFlags = ReadCounter(HullIntegrityConstants.CounterFaultFlags);
            float depth = DepthSignal.IsCreated && DepthSignal.Length > 0 ? DepthSignal[0].DepthMeters : 0f;
            depth = math.isfinite(depth) ? math.max(0f, depth) : 0f;
            float density = math.isfinite(WaterDensity) ? math.max(0f, WaterDensity) : 1025f;
            float gravity = math.isfinite(Gravity) ? math.max(0f, Gravity) : 9.80665f;
            float gradient = math.isfinite(CrushDepthGradient) ? math.max(0.000001f, CrushDepthGradient) : 1f;
            float pressure = density * gravity * depth * gradient;
            if (!math.isfinite(pressure))
            {
                pressure = 0f;
                faultFlags |= 4;
            }

            BaseIntegrityLedgerDTO ledger = Ledger[0];
            float totalSip = math.isfinite(ledger.TotalSIP) ? math.max(0f, ledger.TotalSIP) : 0f;
            int breachedCount = math.max(0, ledger.BreachedNodeCount);
            int weakestIndex = ReadCounter(HullIntegrityConstants.CounterWeakestModuleIndex);

            WriteCounter(HullIntegrityConstants.CounterBreachPending, 0);
            WriteCounter(HullIntegrityConstants.CounterBreachedModuleIndex, -1);

            if (pressure > totalSip && weakestIndex >= 0 && weakestIndex < math.min(ModuleCount, Modules.Length))
            {
                BaseModuleStateDTO weakest = Modules[weakestIndex];
                if ((weakest.Flags & HullIntegrityConstants.ModuleFlagBreached) == 0)
                {
                    weakest.Flags |= HullIntegrityConstants.ModuleFlagBreached;
                    weakest.CurrentSIP = 0f;
                    weakest.Stress01 = 1f;
                    weakest.PeakStress01 = 1f;
                    weakest.DepthMeters = depth;
                    weakest.BreachFrame = Frame;
                    Modules[weakestIndex] = weakest;
                    breachedCount++;

                    WriteCounter(HullIntegrityConstants.CounterBreachPending, 1);
                    WriteCounter(HullIntegrityConstants.CounterBreachedNodeId, (int)weakest.NodeId);
                    WriteCounter(HullIntegrityConstants.CounterBreachedModuleIndex, weakestIndex);
                }
            }

            Ledger[0] = new BaseIntegrityLedgerDTO
            {
                BaseHash = BaseHash,
                TotalSIP = totalSip,
                DepthPressure = pressure,
                BreachedNodeCount = breachedCount
            };

            WriteCounter(HullIntegrityConstants.CounterBreachedCount, breachedCount);
            WriteCounter(HullIntegrityConstants.CounterFaultFlags, faultFlags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityRepairDentJob : IJob
    {
        [NoAlias] public NativeArray<HullDentDTO> Dents;
        [NoAlias] public NativeArray<int> Counters;
        public MockRepairLaserSignal Repair;
        public int Capacity;
        public float DeltaTime;

        public void Execute()
        {
            if ((Repair.Flags & 1u) == 0u || !Dents.IsCreated)
                return;

            int capacity = math.clamp(Capacity, 0, Dents.Length);
            float radius = math.isfinite(Repair.Radius) ? math.max(Repair.Radius, 0.0001f) : 0.0001f;
            float radiusSq = radius * radius;
            float depthPerSecond = math.isfinite(Repair.DepthPerSecond) ? math.max(0f, Repair.DepthPerSecond) : 0f;
            float deltaTime = math.isfinite(DeltaTime) ? math.max(0f, DeltaTime) : 0f;
            float repairDepth = depthPerSecond * deltaTime;
            if (!math.all(math.isfinite(Repair.LocalPoint)) || !math.isfinite(repairDepth))
            {
                WriteCounter(HullIntegrityConstants.CounterFaultFlags, ReadCounter(HullIntegrityConstants.CounterFaultFlags) | 8);
                return;
            }

            int repaired = 0;
            for (int i = 0; i < capacity; i++)
            {
                HullDentDTO dent = Dents[i];
                if (!math.all(math.isfinite(dent.Position)) ||
                    !math.isfinite(dent.Radius) ||
                    !math.all(math.isfinite(dent.Normal)) ||
                    !math.isfinite(dent.Depth))
                {
                    Dents[i] = default;
                    repaired++;
                    continue;
                }

                if (dent.Depth <= 0f)
                    continue;

                float3 delta = dent.Position - Repair.LocalPoint;
                if (math.lengthsq(delta) > radiusSq)
                    continue;

                dent.Depth = math.max(0f, dent.Depth - repairDepth);
                if (dent.Depth <= 0.0001f)
                {
                    dent.Depth = 0f;
                    dent.Radius = 0f;
                    repaired++;
                }

                Dents[i] = dent;
            }

            if (repaired > 0)
            {
                WriteCounter(HullIntegrityConstants.CounterDentDirty, 1);
                int active = math.max(0, ReadCounter(HullIntegrityConstants.CounterActiveDentCount) - repaired);
                WriteCounter(HullIntegrityConstants.CounterActiveDentCount, active);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegritySubmarineCrushDentJob : IJob
    {
        [NoAlias] public NativeArray<HullDentDTO> Dents;
        [ReadOnly] [NoAlias] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [NoAlias] public NativeArray<int> Counters;
        public int Capacity;
        public uint Frame;
        public float SubmarineSIP;
        public float3 HullExtents;
        public float DentRadius;
        public float DentDepth;
        public int Enabled;

        public void Execute()
        {
            if (Enabled == 0 || !Dents.IsCreated || !Counters.IsCreated || !Ledger.IsCreated || Ledger.Length == 0)
                return;

            BaseIntegrityLedgerDTO ledger = Ledger[0];
            float pressure = math.isfinite(ledger.DepthPressure) ? math.max(0f, ledger.DepthPressure) : 0f;
            float submarineSip = math.isfinite(SubmarineSIP) ? math.max(0f, SubmarineSIP) : float.MaxValue;
            if (pressure <= submarineSip)
                return;

            int capacity = math.clamp(Capacity, 1, math.min(Dents.Length, HullIntegrityConstants.MaxDentCapacity));
            uint hash = math.hash(new uint3(Frame, 0x8BADF00Du, (uint)capacity));
            int cursor = ReadCounter(HullIntegrityConstants.CounterWriteCursor);
            int slot = cursor % capacity;
            if (slot < 0)
                slot += capacity;
            float3 finiteExtents = math.all(math.isfinite(HullExtents)) ? HullExtents : new float3(3f, 2f, 8f);
            float3 extents = math.max(finiteExtents, new float3(0.25f, 0.25f, 0.25f));
            float safeRadius = math.isfinite(DentRadius) ? math.max(0.05f, DentRadius) : 0.05f;
            float safeDepth = math.isfinite(DentDepth) ? math.max(0.001f, DentDepth) : 0.001f;
            float u = ((hash >> 8) & 1023u) * (1f / 1023f) * 2f - 1f;
            float v = ((hash >> 20) & 1023u) * (1f / 1023f) * 2f - 1f;
            int face = (int)(hash % 6u);
            float3 normal = face == 0 ? new float3(1f, 0f, 0f) :
                face == 1 ? new float3(-1f, 0f, 0f) :
                face == 2 ? new float3(0f, 1f, 0f) :
                face == 3 ? new float3(0f, -1f, 0f) :
                face == 4 ? new float3(0f, 0f, 1f) :
                new float3(0f, 0f, -1f);
            float3 point = new float3(
                normal.x != 0f ? normal.x * extents.x : u * extents.x,
                normal.y != 0f ? normal.y * extents.y : v * extents.y,
                normal.z != 0f ? normal.z * extents.z : ((face & 1) == 0 ? u : v) * extents.z);

            Dents[slot] = new HullDentDTO
            {
                Position = point,
                Radius = safeRadius,
                Normal = normal,
                Depth = safeDepth
            };

            WriteCounter(HullIntegrityConstants.CounterWriteCursor, (slot + 1) % capacity);
            WriteCounter(HullIntegrityConstants.CounterActiveDentCount, math.min(capacity, ReadCounter(HullIntegrityConstants.CounterActiveDentCount) + 1));
            WriteCounter(HullIntegrityConstants.CounterDentDirty, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HullIntegrityArenaBfsProofJob : IJob
    {
        [NoAlias] public NativeArray<int> Queue;
        public int NodeCount;

        public void Execute()
        {
            if (!Queue.IsCreated)
                return;

            int count = math.clamp(NodeCount, 0, Queue.Length);
            for (int i = 0; i < count; i++)
                Queue[i] = i + 1 < count ? i + 1 : 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockHullImpactsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HullImpactDTO> Impacts;
        public double3 SubmarineAup;
        public float3 HullExtents;
        public uint Frame;
        public uint SectorHash;
        public int ImpactCount;
        public float GlobalQualityWeight;
        public float MinMagnitude;
        public float MaxMagnitude;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ImpactCount || !Impacts.IsCreated || index >= Impacts.Length)
                return;

            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            uint seed = math.hash(new uint4(SectorHash, Frame, (uint)index, 0x53483109u));
            Random random = Random.CreateFromIndex(seed == 0u ? 1u : seed);
            float3 extents = math.max(
                math.select(new float3(3.4f, 2.2f, 8.5f), math.abs(HullExtents), math.all(math.isfinite(HullExtents))),
                new float3(0.25f, 0.25f, 0.25f));
            int face = (int)(random.NextUInt() % 6u);
            float2 uv = new float2(random.NextFloat(-1f, 1f), random.NextFloat(-1f, 1f));
            float3 normal = SelectFaceNormal(face);
            float3 local = new float3(
                normal.x != 0f ? normal.x * extents.x : uv.x * extents.x,
                normal.y != 0f ? normal.y * extents.y : uv.y * extents.y,
                normal.z != 0f ? normal.z * extents.z : (((face & 1) == 0) ? uv.x : uv.y) * extents.z);

            float minMagnitude = math.isfinite(MinMagnitude) ? math.max(0f, MinMagnitude) : 75f;
            float maxMagnitude = math.isfinite(MaxMagnitude) ? math.max(minMagnitude, MaxMagnitude) : 750f;
            float overkillCurve = q * q * (3f - 2f * q);
            float magnitude = math.lerp(minMagnitude, maxMagnitude, random.NextFloat()) * math.lerp(0.35f, 1.45f, overkillCurve);

            Impacts[index] = new HullImpactDTO
            {
                ImpactAup = SubmarineAup + new double3(local),
                Magnitude = magnitude,
                DamageTypeHash = math.hash(new uint3(seed, (uint)face, 0xD3710109u))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SelectFaceNormal(int face)
        {
            return face == 0 ? new float3(1f, 0f, 0f) :
                face == 1 ? new float3(-1f, 0f, 0f) :
                face == 2 ? new float3(0f, 1f, 0f) :
                face == 3 ? new float3(0f, -1f, 0f) :
                face == 4 ? new float3(0f, 0f, 1f) :
                new float3(0f, 0f, -1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AccumulateHullDamageJob : IJob
    {
        [NativeDisableContainerSafetyRestriction]
        [NoAlias]
        public NativeQueue<HullImpactDTO> Impacts;
        [NoAlias] public NativeArray<DeformationStateDTO> States;
        [NoAlias] public NativeArray<int> Counters;
        [ReadOnly] [NoAlias] public NativeArray<HullMaterialStrengthDTO> MaterialStrengths;
        public double3 SubmarineAup;
        public float3 HullExtents;
        public int Capacity;
        public int MaxActiveDents;
        public float MetalPlasticity;
        public float MaxDentDepth;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute()
        {
            if (!Impacts.IsCreated || !States.IsCreated || !Counters.IsCreated)
                return;

            DeformationStateDTO* statesPtr = (DeformationStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
            int capacity = math.clamp(Capacity, 0, math.min(States.Length, HullIntegrityConstants.MaxDentCapacity));
            int shaderLimit = math.clamp(MaxActiveDents, HullIntegrityConstants.MinShaderDentCapacity, math.min(capacity, HullIntegrityConstants.MaxShaderDentCapacity));
            int active = math.clamp(ReadCounter(HullIntegrityConstants.CounterActiveDeformationCount), 0, capacity);
            int discarded = math.max(0, ReadCounter(HullIntegrityConstants.CounterDiscardedImpactCount));
            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float plasticity = math.isfinite(MetalPlasticity) ? math.max(0.0001f, MetalPlasticity) : 1f;
            float maxDepth = math.isfinite(MaxDentDepth) ? math.max(0.001f, MaxDentDepth) : 0.35f;
            float3 extents = math.max(
                math.select(new float3(3.4f, 2.2f, 8.5f), math.abs(HullExtents), math.all(math.isfinite(HullExtents))),
                new float3(0.25f, 0.25f, 0.25f));
            int dirty = 0;

            while (Impacts.TryDequeue(out HullImpactDTO impact))
            {
                if (!TryLocalizeImpact(impact, SubmarineAup, out float3 local))
                {
                    discarded++;
                    continue;
                }

                local = math.clamp(local, -extents, extents);
                float magnitude = math.isfinite(impact.Magnitude) ? math.max(0f, impact.Magnitude) : 0f;
                float severity = math.saturate(magnitude * 0.01f);
                HullMaterialStrengthDTO material = ResolveMaterial(impact.DamageTypeHash, plasticity, maxDepth);
                float impactPlasticity = math.isfinite(material.Plasticity) ? math.max(0.0001f, material.Plasticity) : plasticity;
                float impactMaxDepth = math.isfinite(material.MaxDentDepth) ? math.max(0.001f, material.MaxDentDepth) : maxDepth;
                float depth = math.min(impactMaxDepth, magnitude * impactPlasticity * math.lerp(0.00055f, 0.0026f, q));
                float radius = math.lerp(0.28f, 2.6f, math.sqrt(severity)) * math.lerp(0.75f, 1.25f, q);
                radius = math.max(0.05f, radius);
                float3 normal = ResolveHullNormal(local, extents);
                int mergedIndex = FindMergeCandidate(statesPtr, active, local, radius);

                if (mergedIndex >= 0)
                {
                    ref DeformationStateDTO dent = ref UnsafeUtility.AsRef<DeformationStateDTO>(statesPtr + mergedIndex);
                    dent.Depth = math.min(impactMaxDepth, math.max(0f, dent.Depth) + depth * 0.72f);
                    dent.Radius = math.min(8f, math.max(math.max(0.05f, dent.Radius), radius) + radius * 0.12f);
                    dent.Normal = math.normalizesafe(dent.Normal + normal * 0.35f, normal);
                    dent.Age = 0f;
                    dent.Severity = math.max(dent.Severity, severity);
                    dent.DamageTypeHash = impact.DamageTypeHash;
                    dent.Frame = Frame;
                    dent.Flags = DeformationStateFlags.Active | math.select(0u, DeformationStateFlags.Breach, dent.Depth >= impactMaxDepth * 0.92f);
                    dirty = 1;
                    continue;
                }

                if (active >= shaderLimit)
                {
                    discarded++;
                    continue;
                }

                statesPtr[active] = new DeformationStateDTO
                {
                    LocalPosition = local,
                    Radius = radius,
                    Normal = normal,
                    Depth = depth,
                    Age = 0f,
                    Severity = severity,
                    DamageTypeHash = impact.DamageTypeHash,
                    SourceHash = HullIntegrityConstants.AgentHash,
                    Frame = Frame,
                    Flags = DeformationStateFlags.Active |
                        DeformationStateFlags.Mock |
                        math.select(0u, DeformationStateFlags.Breach, depth >= impactMaxDepth * 0.92f)
                };
                active++;
                dirty = 1;
            }

            WriteCounter(HullIntegrityConstants.CounterActiveDeformationCount, active);
            WriteCounter(HullIntegrityConstants.CounterDiscardedImpactCount, discarded);
            WriteCounter(HullIntegrityConstants.CounterMaxObservedDentCount, math.max(ReadCounter(HullIntegrityConstants.CounterMaxObservedDentCount), active));
            if (dirty != 0)
                WriteCounter(HullIntegrityConstants.CounterDentDirty, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryLocalizeImpact(in HullImpactDTO impact, double3 submarineAup, out float3 local)
        {
            local = default;
            double3 delta = impact.ImpactAup - submarineAup;
            if (!math.all(math.isfinite(delta)))
                return false;

            local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.all(math.isfinite(local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int FindMergeCandidate(DeformationStateDTO* states, int active, float3 local, float radius)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            float mergeRadius = math.max(0.08f, radius * 0.72f);
            float mergeSq = mergeRadius * mergeRadius;
            for (int i = 0; i < active; i++)
            {
                DeformationStateDTO existing = states[i];
                if ((existing.Flags & DeformationStateFlags.Active) == 0u)
                    continue;

                float distSq = math.lengthsq(existing.LocalPosition - local);
                if (distSq <= mergeSq && distSq < bestSq)
                {
                    bestSq = distSq;
                    best = i;
                }
            }

            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HullMaterialStrengthDTO ResolveMaterial(uint materialHash, float fallbackPlasticity, float fallbackMaxDepth)
        {
            if (MaterialStrengths.IsCreated)
            {
                for (int i = 0; i < MaterialStrengths.Length; i++)
                {
                    HullMaterialStrengthDTO material = MaterialStrengths[i];
                    if (material.MaterialHash == materialHash &&
                        material.MaterialHash != 0u &&
                        math.isfinite(material.Plasticity) &&
                        math.isfinite(material.MaxDentDepth) &&
                        material.Plasticity > 0f &&
                        material.MaxDentDepth > 0f)
                    {
                        return material;
                    }
                }
            }

            return new HullMaterialStrengthDTO
            {
                MaterialHash = materialHash,
                Plasticity = fallbackPlasticity,
                MaxDentDepth = fallbackMaxDepth,
                PressureBuckleThreshold01 = 0.82f,
                RepairRelaxation = 0.0025f
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveHullNormal(float3 local, float3 extents)
        {
            float3 scaled = local / math.max(extents, new float3(0.0001f));
            float3 a = math.abs(scaled);
            if (a.x >= a.y && a.x >= a.z)
                return new float3(math.select(-1f, 1f, scaled.x >= 0f), 0f, 0f);
            if (a.y >= a.z)
                return new float3(0f, math.select(-1f, 1f, scaled.y >= 0f), 0f);
            return new float3(0f, 0f, math.select(-1f, 1f, scaled.z >= 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DecayDeformationJob : IJob
    {
        [NoAlias] public NativeArray<DeformationStateDTO> States;
        [NoAlias] public NativeArray<int> Counters;
        public int Capacity;
        public float DeltaTime;
        public float RelaxDepthPerSecond;
        public float RepairDepthPerSecond;
        public float3 RepairLocalPosition;
        public float RepairRadius;
        public int RepairEnabled;

        public void Execute()
        {
            if (!States.IsCreated || !Counters.IsCreated || Counters.Length <= HullIntegrityConstants.CounterActiveDeformationCount)
                return;

            int active = math.clamp(ReadCounter(HullIntegrityConstants.CounterActiveDeformationCount), 0, math.min(Capacity, States.Length));
            DeformationStateDTO* statesPtr = (DeformationStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
            float dt = math.isfinite(DeltaTime) ? math.max(0f, DeltaTime) : 0f;
            float relax = math.isfinite(RelaxDepthPerSecond) ? math.max(0f, RelaxDepthPerSecond) : 0f;
            float repair = math.isfinite(RepairDepthPerSecond) ? math.max(0f, RepairDepthPerSecond) : 0f;
            float repairRadius = math.isfinite(RepairRadius) ? math.max(0.0001f, RepairRadius) : 0.0001f;
            float repairSq = repairRadius * repairRadius;
            int i = 0;
            int dirty = 0;

            while (i < active)
            {
                ref DeformationStateDTO dent = ref UnsafeUtility.AsRef<DeformationStateDTO>(statesPtr + i);
                if ((dent.Flags & DeformationStateFlags.Active) == 0u || !IsFinite(dent))
                {
                    active = RemoveAtSwapBack(statesPtr, i, active);
                    dirty = 1;
                    continue;
                }

                float depthLoss = relax * dt;
                if (RepairEnabled != 0 && math.lengthsq(dent.LocalPosition - RepairLocalPosition) <= repairSq)
                    depthLoss += repair * dt;

                dent.Age = math.max(0f, dent.Age + dt);
                if (depthLoss > 0f)
                {
                    dent.Depth = math.max(0f, dent.Depth - depthLoss);
                    dent.Radius = math.max(0f, dent.Radius - depthLoss * 0.2f);
                    dirty = 1;
                }
                if (dent.Depth <= 0.0001f || dent.Radius <= 0.0001f)
                {
                    active = RemoveAtSwapBack(statesPtr, i, active);
                    dirty = 1;
                    continue;
                }

                i++;
            }

            WriteCounter(HullIntegrityConstants.CounterActiveDeformationCount, active);
            if (dirty != 0)
                WriteCounter(HullIntegrityConstants.CounterDentDirty, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int RemoveAtSwapBack(DeformationStateDTO* states, int index, int active)
        {
            int last = active - 1;
            if (index < last)
                states[index] = states[last];

            ref DeformationStateDTO cleared = ref UnsafeUtility.AsRef<DeformationStateDTO>(states + last);
            cleared.Flags = 0u;
            cleared.Depth = 0f;
            cleared.Radius = 0f;
            return last;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in DeformationStateDTO dent)
        {
            return math.all(math.isfinite(dent.LocalPosition)) &&
                math.all(math.isfinite(dent.Normal)) &&
                math.isfinite(dent.Depth) &&
                math.isfinite(dent.Radius) &&
                math.isfinite(dent.Age);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadCounter(int index)
        {
            return Counters.IsCreated && Counters.Length > index ? Counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCounter(int index, int value)
        {
            if (Counters.IsCreated && Counters.Length > index)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BuildBreachJetsJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<DeformationStateDTO> States;
        [NoAlias] public NativeArray<BreachJetDTO> Jets;
        [NoAlias] public NativeArray<BreachJetIndirectArgsDTO> Args;
        [NoAlias] public NativeArray<int> Counters;
        public int Capacity;
        public uint Frame;
        public float MaxDentDepth;
        public float PressureBuckleThreshold01;
        public float GlobalQualityWeight;
        public uint VertexCountPerJet;

        public void Execute()
        {
            if (!States.IsCreated || !Jets.IsCreated || !Args.IsCreated || Args.Length == 0)
                return;

            int active = Counters.IsCreated && Counters.Length > HullIntegrityConstants.CounterActiveDeformationCount
                ? math.clamp(Counters[HullIntegrityConstants.CounterActiveDeformationCount], 0, math.min(Capacity, States.Length))
                : 0;
            int jetCount = 0;
            float maxDepth = math.isfinite(MaxDentDepth) ? math.max(0.001f, MaxDentDepth) : 0.35f;
            float threshold = math.saturate(math.select(0.82f, PressureBuckleThreshold01, math.isfinite(PressureBuckleThreshold01)));
            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            DeformationStateDTO* statesPtr = (DeformationStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(States);
            BreachJetDTO* jetsPtr = (BreachJetDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(Jets);
            BreachJetIndirectArgsDTO* argsPtr = (BreachJetIndirectArgsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(Args);

            for (int i = 0; i < active && jetCount < Jets.Length; i++)
            {
                DeformationStateDTO dent = statesPtr[i];
                if ((dent.Flags & DeformationStateFlags.Active) == 0u)
                    continue;

                float depth01 = math.saturate(dent.Depth / math.max(maxDepth, 0.0001f));
                if (depth01 < threshold)
                    continue;

                jetsPtr[jetCount] = new BreachJetDTO
                {
                    LocalPosition = dent.LocalPosition,
                    Radius = math.max(0.05f, dent.Radius),
                    Normal = math.normalizesafe(-dent.Normal, new float3(0f, -1f, 0f)),
                    Intensity01 = math.saturate(depth01 * math.lerp(0.65f, 1.35f, q)),
                    Age = dent.Age,
                    DamageTypeHash = dent.DamageTypeHash,
                    Frame = Frame,
                    Flags = 1u
                };
                jetCount++;
            }

            argsPtr[0] = new BreachJetIndirectArgsDTO
            {
                VertexCountPerInstance = math.max(3u, VertexCountPerJet),
                InstanceCount = (uint)jetCount,
                StartVertex = 0u,
                StartInstance = 0u
            };

            if (Counters.IsCreated && Counters.Length > HullIntegrityConstants.CounterBreachJetCount)
                Counters[HullIntegrityConstants.CounterBreachJetCount] = jetCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyPressureBucklingJob : IJob
    {
        [NoAlias] public NativeArray<DeformationStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [ReadOnly] [NoAlias] public NativeArray<float> ExternalPressure01;
        [NoAlias] public NativeArray<int> Counters;
        public int Capacity;
        public int MaxActiveDents;
        public float3 HullExtents;
        public float PressureBuckleThreshold01;
        public float MaxDentDepth;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute()
        {
            if (!States.IsCreated || !Counters.IsCreated || Counters.Length <= HullIntegrityConstants.CounterActiveDeformationCount)
                return;

            float pressure01 = 0f;
            if (ExternalPressure01.IsCreated && ExternalPressure01.Length > 0 && math.isfinite(ExternalPressure01[0]))
                pressure01 = math.saturate(ExternalPressure01[0]);
            else if (Ledger.IsCreated && Ledger.Length > 0)
            {
                BaseIntegrityLedgerDTO ledger = Ledger[0];
                float pressure = math.isfinite(ledger.DepthPressure) ? math.max(0f, ledger.DepthPressure) : 0f;
                float totalSip = math.isfinite(ledger.TotalSIP) ? math.max(ledger.TotalSIP, 0.0001f) : 0.0001f;
                pressure01 = math.saturate(pressure / totalSip);
            }

            float threshold = math.saturate(math.select(0.82f, PressureBuckleThreshold01, math.isfinite(PressureBuckleThreshold01)));
            if (pressure01 < threshold)
                return;

            int capacity = math.clamp(Capacity, 0, math.min(States.Length, HullIntegrityConstants.MaxDentCapacity));
            int limit = math.clamp(MaxActiveDents, HullIntegrityConstants.MinShaderDentCapacity, math.min(capacity, HullIntegrityConstants.MaxShaderDentCapacity));
            int active = math.clamp(Counters[HullIntegrityConstants.CounterActiveDeformationCount], 0, capacity);
            DeformationStateDTO* statesPtr = (DeformationStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float pressureOver = math.saturate((pressure01 - threshold) / math.max(1f - threshold, 0.0001f));
            int desiredBuckles = math.clamp((int)math.ceil(math.lerp(1f, 8f, q) * pressureOver), 1, 8);
            float maxDepth = math.isfinite(MaxDentDepth) ? math.max(0.001f, MaxDentDepth) : 0.35f;
            float3 extents = math.max(
                math.select(new float3(3.4f, 2.2f, 8.5f), math.abs(HullExtents), math.all(math.isfinite(HullExtents))),
                new float3(0.25f, 0.25f, 0.25f));

            int existingPressure = 0;
            for (int i = 0; i < active; i++)
            {
                ref DeformationStateDTO dent = ref UnsafeUtility.AsRef<DeformationStateDTO>(statesPtr + i);
                if ((dent.Flags & DeformationStateFlags.Pressure) == 0u)
                    continue;

                WritePressureDent(ref dent, existingPressure, extents, pressureOver, maxDepth, q);
                dent.Frame = Frame;
                existingPressure++;
                if (existingPressure >= desiredBuckles)
                    break;
            }

            while (existingPressure < desiredBuckles && active < limit)
            {
                ref DeformationStateDTO dent = ref UnsafeUtility.AsRef<DeformationStateDTO>(statesPtr + active);
                dent = default;
                WritePressureDent(ref dent, existingPressure, extents, pressureOver, maxDepth, q);
                dent.DamageTypeHash = 0x50525353u; // PRSS
                dent.SourceHash = HullIntegrityConstants.AgentHash;
                dent.Frame = Frame;
                dent.Flags = DeformationStateFlags.Active | DeformationStateFlags.Pressure;
                active++;
                existingPressure++;
            }

            Counters[HullIntegrityConstants.CounterActiveDeformationCount] = active;
            Counters[HullIntegrityConstants.CounterDentDirty] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WritePressureDent(
            ref DeformationStateDTO dent,
            int index,
            float3 extents,
            float pressureOver,
            float maxDepth,
            float q)
        {
            int face = index % 6;
            float phase = (index + 1) * 1.6180339f;
            float u = math.frac(phase) * 2f - 1f;
            float v = math.frac(phase * 0.731f) * 2f - 1f;
            float3 normal = face == 0 ? new float3(1f, 0f, 0f) :
                face == 1 ? new float3(-1f, 0f, 0f) :
                face == 2 ? new float3(0f, 1f, 0f) :
                face == 3 ? new float3(0f, -1f, 0f) :
                face == 4 ? new float3(0f, 0f, 1f) :
                new float3(0f, 0f, -1f);
            dent.LocalPosition = new float3(
                normal.x != 0f ? normal.x * extents.x : u * extents.x,
                normal.y != 0f ? normal.y * extents.y : v * extents.y,
                normal.z != 0f ? normal.z * extents.z : ((face & 1) == 0 ? u : v) * extents.z);
            dent.Normal = normal;
            dent.Radius = math.lerp(1.4f, 6.5f, q) * math.lerp(0.65f, 1.35f, pressureOver);
            dent.Depth = maxDepth * math.lerp(0.12f, 0.55f, pressureOver) * math.lerp(0.55f, 1.1f, q);
            dent.Age = math.max(0f, dent.Age);
            dent.Severity = pressureOver;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ClearDeformationActiveFlagsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DeformationStateDTO> States;

        public void Execute(int index)
        {
            if (!States.IsCreated || index >= States.Length)
                return;

            ref DeformationStateDTO state = ref DeformationStateDTO.AsRef(States, index);
            state.Flags = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct HullIntegrityMemClearJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public void* Ptr;
        public long Bytes;

        public void Execute()
        {
            if (Ptr != null && Bytes > 0)
                UnsafeUtility.MemClear(Ptr, Bytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct HullIntegrityMappedCopyJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] [NoAlias] public void* Source;
        [NativeDisableUnsafePtrRestriction] [NoAlias] public void* Destination;
        public long Bytes;

        public void Execute()
        {
            if (Source != null && Destination != null && Bytes > 0)
                UnsafeUtility.MemCpy(Destination, Source, Bytes);
        }
    }

}
