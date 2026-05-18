using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
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
        public const int TelemetryFrameCapacity = 300;
        public const int MaxMockModuleCapacity = 512;
        public const int MaxDamageSignals = 32;

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
    /// Play-mode tuning block edited through the Hull Integrity Tuner and read by jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HullIntegrityTuningDTO
    {
        [FieldOffset(0)] public float BaseSipMultiplier;
        [FieldOffset(4)] public float CrushDepthGradient;
        [FieldOffset(8)] public float DentRadius;
        [FieldOffset(12)] public float DentDepth;
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

    [BurstCompile]
    internal struct HullIntegrityEmergencyMockJob : IJob
    {
        public NativeArray<BaseModuleStateDTO> Modules;
        public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        public NativeArray<int> Counters;
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

    [BurstCompile]
    internal struct HullIntegrityMockDepthJob : IJob
    {
        public NativeArray<MockDepthSignal> DepthSignal;
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

    [BurstCompile]
    internal struct HullIntegrityDamageJob : IJob
    {
        public NativeArray<BaseModuleStateDTO> Modules;
        [ReadOnly] public NativeArray<MockCombatDamageSignal> DamageSignals;
        public NativeArray<int> Counters;
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

    [BurstCompile]
    internal struct HullIntegritySipAggregationJob : IJob
    {
        public NativeArray<BaseModuleStateDTO> Modules;
        public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        public NativeArray<int> Counters;
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

    [BurstCompile]
    internal struct HullIntegrityHydrostaticPressureJob : IJob
    {
        public NativeArray<BaseModuleStateDTO> Modules;
        public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        [ReadOnly] public NativeArray<MockDepthSignal> DepthSignal;
        public NativeArray<int> Counters;
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

    [BurstCompile]
    internal struct HullIntegrityRepairDentJob : IJob
    {
        public NativeArray<HullDentDTO> Dents;
        public NativeArray<int> Counters;
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

    [BurstCompile]
    internal struct HullIntegritySubmarineCrushDentJob : IJob
    {
        public NativeArray<HullDentDTO> Dents;
        [ReadOnly] public NativeArray<BaseIntegrityLedgerDTO> Ledger;
        public NativeArray<int> Counters;
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
            int slot = ReadCounter(HullIntegrityConstants.CounterWriteCursor) & (capacity - 1);
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

            WriteCounter(HullIntegrityConstants.CounterWriteCursor, (slot + 1) & (capacity - 1));
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

    [BurstCompile]
    internal struct HullIntegrityArenaBfsProofJob : IJob
    {
        public NativeArray<int> Queue;
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

    [BurstCompile]
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
}
