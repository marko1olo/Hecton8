using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;
using FluidCompartmentFlags = global::Hecton8.Core.Contracts.Physics.FluidCompartmentFlags;
using StructuralIntegrityStateDTO = Hecton8.Habitat.Deformation.IntegrityStateDTO;

namespace Hecton8.Construction
{
    public static class HatchLockMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTickIntervalSeconds(float quality)
        {
            float q = Sanitize01(quality, 0f);
            return math.lerp(
                HatchLockConstants.UltraTickIntervalSeconds,
                HatchLockConstants.SurvivalTickIntervalSeconds,
                1f - q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveAuthorityTickIntervalSeconds()
        {
            return ResolveTickIntervalSeconds(HatchLockConstants.AuthoritativeQualityWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint seed, uint value)
        {
            uint hash = seed ^ value;
            hash *= 16777619u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition PackAup(double3 absolute)
        {
            const double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            long gridX = (long)math.floor(absolute.x / cell);
            long gridY = (long)math.floor(absolute.y / cell);
            long gridZ = (long)math.floor(absolute.z / cell);
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolute.x - gridX * cell),
                LocalY = (float)(absolute.y - gridY * cell),
                LocalZ = (float)(absolute.z - gridZ * cell)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PressureProxyATM(in FluidCompartmentDTO compartment, out uint faultMask)
        {
            faultMask = 0u;
            if (!math.isfinite(compartment.CurrentWaterVolume) ||
                !math.isfinite(compartment.MaxWaterVolume) ||
                !math.isfinite(compartment.WaterLevelHeight01))
            {
                faultMask |= HatchFsmStateMask.NonFinite;
                return 1f;
            }

            float maxWater = math.max(0.0001f, compartment.MaxWaterVolume);
            float volumeFill = math.saturate(compartment.CurrentWaterVolume / maxWater);
            float heightFill = math.saturate(compartment.WaterLevelHeight01);
            float fill01 = math.max(volumeFill, heightFill);
            float breachBonus = (compartment.Flags & FluidCompartmentFlags.Breached) != 0u ? 0.25f : 0f;
            return 1f + fill01 + breachBonus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float StructuralHealth01(in StructuralIntegrityStateDTO state, out uint faultMask)
        {
            faultMask = 0u;
            if (!math.isfinite(state.BaseStrength) ||
                !math.isfinite(state.CurrentStress) ||
                !math.isfinite(state.AppliedPressure) ||
                !math.isfinite(state.BucklingScalar))
            {
                faultMask |= HatchFsmStateMask.NonFinite;
                return 1f;
            }

            if ((state.Flags & Hecton8.Habitat.Deformation.StructuralIntegrityConstants.StateFlagCollapsed) != 0u)
                return 0f;

            float baseStrength = math.max(0.0001f, math.abs(state.BaseStrength));
            float stress01 = math.saturate(math.abs(state.CurrentStress) / baseStrength);
            float pressurePenalty = math.saturate(math.abs(state.AppliedPressure) * 0.02f);
            float bucklePenalty = math.saturate(state.BucklingScalar);
            return math.saturate(1f - math.max(stress01, math.max(pressurePenalty, bucklePenalty)));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SyncHatchRowsFromBulkheadsJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchStateDTO* Hatches;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* Bulkheads;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                Hatches == null ||
                Bulkheads == null)
            {
                return;
            }

            ref HatchStateDTO hatch = ref UnsafeUtility.AsRef<HatchStateDTO>(Hatches + index);
            BulkheadStateDTO bulkhead = Bulkheads[index];
            if ((bulkhead.Flags & BulkheadStateFlags.Active) == 0u || bulkhead.EdgeHashID == 0u)
            {
                hatch = default;
                return;
            }

            uint preserved = hatch.FsmStateMask &
                             (HatchFsmStateMask.PressureLocked |
                              HatchFsmStateMask.StructurallyJammed |
                              HatchFsmStateMask.CatastrophicFlood |
                              HatchFsmStateMask.MissingCompartment |
                              HatchFsmStateMask.NonFinite);
            uint manual = (bulkhead.Flags & BulkheadStateFlags.ManualOverride) != 0u
                ? HatchFsmStateMask.ManualOverride
                : (hatch.FsmStateMask & HatchFsmStateMask.ManualOverride);
            uint motion = bulkhead.AssociatedLock != 0u || bulkhead.ClosureProgress >= 0.5f
                ? HatchFsmStateMask.Closed
                : HatchFsmStateMask.Open;

            hatch.RoomAHashID = bulkhead.EdgeHashID;
            hatch.RoomBHashID = bulkhead.SiblingNodeHash != 0u
                ? bulkhead.SiblingNodeHash
                : HatchLockMath.Hash(bulkhead.EdgeHashID, 0x524F4F4Du);
            hatch.PressureDifferentialATM = HatchLockMath.SanitizePositive(hatch.PressureDifferentialATM, 0f);
            hatch.FsmStateMask = HatchFsmStateMask.Active | preserved | manual | motion;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateHatchPressureJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchStateDTO* Hatches;
        [NativeDisableUnsafePtrRestriction, NoAlias] public FluidCompartmentDTO* Compartments;
        public int HatchCount;
        public int CompartmentCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)HatchCount ||
                Hatches == null ||
                Compartments == null ||
                CompartmentCount <= 0)
            {
                return;
            }

            ref HatchStateDTO hatch = ref UnsafeUtility.AsRef<HatchStateDTO>(Hatches + index);
            if ((hatch.FsmStateMask & HatchFsmStateMask.Active) == 0u)
                return;

            uint faultMaskA;
            uint faultMaskB;
            bool foundA;
            bool foundB;
            int pairedCompartmentIndex = index * HatchLockConstants.PairedFluidRowsPerHatch;
            float pressureA = ResolvePressure(hatch.RoomAHashID, pairedCompartmentIndex, index, out foundA, out faultMaskA);
            float pressureB = ResolvePressure(hatch.RoomBHashID, pairedCompartmentIndex + 1, index + 1, out foundB, out faultMaskB);
            float delta = math.abs(pressureA - pressureB);
            uint mask = hatch.FsmStateMask & ~(HatchFsmStateMask.NonFinite | HatchFsmStateMask.MissingCompartment);
            if (!foundA || !foundB)
            {
                mask |= HatchFsmStateMask.MissingCompartment;
                delta = 0f;
            }
            if ((faultMaskA | faultMaskB) != 0u || !math.isfinite(delta))
            {
                mask |= HatchFsmStateMask.NonFinite;
                delta = 0f;
            }

            hatch.PressureDifferentialATM = delta;
            hatch.FsmStateMask = mask;
        }

        private float ResolvePressure(uint nodeHash, int primaryIndex, int secondaryIndex, out bool found, out uint faultMask)
        {
            found = false;
            faultMask = 0u;
            if (nodeHash == 0u)
                return 1f;

            if (TryResolvePressureAt(nodeHash, primaryIndex, out float pressure, out faultMask))
            {
                found = true;
                return pressure;
            }

            if (secondaryIndex != primaryIndex &&
                TryResolvePressureAt(nodeHash, secondaryIndex, out pressure, out faultMask))
            {
                found = true;
                return pressure;
            }

            for (int i = 0; i < CompartmentCount; i++)
            {
                FluidCompartmentDTO compartment = Compartments[i];
                if (compartment.NodeHashID != nodeHash)
                    continue;

                found = true;
                return HatchLockMath.PressureProxyATM(in compartment, out faultMask);
            }

            return 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolvePressureAt(uint nodeHash, int index, out float pressure, out uint faultMask)
        {
            pressure = 1f;
            faultMask = 0u;
            if ((uint)index >= (uint)CompartmentCount ||
                Compartments[index].NodeHashID != nodeHash)
            {
                return false;
            }

            pressure = HatchLockMath.PressureProxyATM(in Compartments[index], out faultMask);
            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MarkHatchFluidUnavailableJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchStateDTO* Hatches;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* Bulkheads;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                Hatches == null ||
                Bulkheads == null)
            {
                return;
            }

            ref HatchStateDTO hatch = ref UnsafeUtility.AsRef<HatchStateDTO>(Hatches + index);
            ref BulkheadStateDTO bulkhead = ref UnsafeUtility.AsRef<BulkheadStateDTO>(Bulkheads + index);
            if ((hatch.FsmStateMask & HatchFsmStateMask.Active) == 0u ||
                (bulkhead.Flags & BulkheadStateFlags.Active) == 0u)
            {
                return;
            }

            hatch.PressureDifferentialATM = 0f;
            uint preserved = hatch.FsmStateMask &
                             (HatchFsmStateMask.Active |
                              HatchFsmStateMask.ManualOverride |
                              HatchFsmStateMask.StructurallyJammed |
                              HatchFsmStateMask.CatastrophicFlood |
                              HatchFsmStateMask.NonFinite);
            bool destroyedOrFlooded = (preserved & HatchFsmStateMask.CatastrophicFlood) != 0u ||
                                      (bulkhead.Flags & BulkheadStateFlags.Destroyed) != 0u;
            uint failMask = HatchFsmStateMask.MissingCompartment;
            if (destroyedOrFlooded)
                failMask |= HatchFsmStateMask.Open;
            else
                failMask |= HatchFsmStateMask.PressureLocked | HatchFsmStateMask.Closed;

            hatch.FsmStateMask = preserved | failMask;

            if (!destroyedOrFlooded)
            {
                bulkhead.AssociatedLock = 1u;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateHatchFsmJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchStateDTO* Hatches;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* Bulkheads;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ModuleIntegrity01;
        [NativeDisableUnsafePtrRestriction, NoAlias] public StructuralIntegrityStateDTO* StructuralStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* HatchAups;
        [NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<MovementAcousticSignal>.ParallelWriter AcousticWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> AcousticWriterBudget;
        public int Count;
        public int ModuleIntegrityCount;
        public int StructuralStateCount;
        public float SafePressureDifferentialATM;
        public float StructuralJamThreshold01;
        public float CatastrophicPressureDifferentialATM;
        public float AcousticAuthorityWeight;
        public uint Frame;
        public byte EmitAcousticSignals;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                Hatches == null ||
                Bulkheads == null)
            {
                return;
            }

            ref HatchStateDTO hatch = ref UnsafeUtility.AsRef<HatchStateDTO>(Hatches + index);
            ref BulkheadStateDTO bulkhead = ref UnsafeUtility.AsRef<BulkheadStateDTO>(Bulkheads + index);
            if ((hatch.FsmStateMask & HatchFsmStateMask.Active) == 0u ||
                (bulkhead.Flags & BulkheadStateFlags.Active) == 0u)
            {
                return;
            }

            float safePressure = HatchLockMath.SanitizePositive(
                SafePressureDifferentialATM,
                HatchLockConstants.DefaultSafePressureDifferentialATM);
            float catastrophicPressure = math.max(
                safePressure,
                HatchLockMath.SanitizePositive(
                    CatastrophicPressureDifferentialATM,
                    HatchLockConstants.DefaultCatastrophicPressureDifferentialATM));
            float jamThreshold = HatchLockMath.Sanitize01(
                StructuralJamThreshold01,
                HatchLockConstants.DefaultStructuralJamThreshold01);
            float delta = HatchLockMath.SanitizePositive(hatch.PressureDifferentialATM, 0f);
            uint previousMask = hatch.FsmStateMask;
            bool dataValid = (previousMask & (HatchFsmStateMask.NonFinite | HatchFsmStateMask.MissingCompartment)) == 0u;
            bool pressureLocked = dataValid && delta > safePressure;
            bool alreadyJammed = (previousMask & HatchFsmStateMask.StructurallyJammed) != 0u ||
                                 (bulkhead.Flags & BulkheadStateFlags.Jammed) != 0u;
            float structuralHealth = alreadyJammed
                ? 0f
                : ResolveStructuralHealth(hatch.RoomAHashID, hatch.RoomBHashID, index, jamThreshold);
            bool structuralJammed = alreadyJammed || structuralHealth < jamThreshold;
            bool manualOverride = (previousMask & HatchFsmStateMask.ManualOverride) != 0u ||
                                  (bulkhead.Flags & BulkheadStateFlags.ManualOverride) != 0u;
            bool catastrophicFlood = (previousMask & HatchFsmStateMask.CatastrophicFlood) != 0u ||
                                     (manualOverride && dataValid && delta > catastrophicPressure);

            uint nextMask = previousMask &
                            (HatchFsmStateMask.Active |
                             HatchFsmStateMask.ManualOverride |
                             HatchFsmStateMask.CatastrophicFlood |
                             HatchFsmStateMask.MissingCompartment |
                             HatchFsmStateMask.NonFinite);
            if (pressureLocked)
                nextMask |= HatchFsmStateMask.PressureLocked;
            if (structuralJammed)
                nextMask |= HatchFsmStateMask.StructurallyJammed;
            if (manualOverride)
                nextMask |= HatchFsmStateMask.ManualOverride;
            if (catastrophicFlood)
                nextMask |= HatchFsmStateMask.CatastrophicFlood;

            if (catastrophicFlood)
            {
                bulkhead.Flags |= BulkheadStateFlags.CatastrophicDamage | BulkheadStateFlags.Destroyed;
                bulkhead.Flags &= ~BulkheadStateFlags.Sealed;
                bulkhead.AssociatedLock = 0u;
                bulkhead.ClosureProgress = math.min(BulkheadContainmentMath.Sanitize01(bulkhead.ClosureProgress, 0f), 0.73f);
                nextMask |= HatchFsmStateMask.Open;
            }
            else
            {
                if (pressureLocked || structuralJammed)
                    bulkhead.AssociatedLock = 1u;
                if (structuralJammed)
                    bulkhead.Flags |= BulkheadStateFlags.Jammed;
                bool closed = bulkhead.AssociatedLock != 0u || bulkhead.ClosureProgress >= 0.5f;
                nextMask |= closed ? HatchFsmStateMask.Closed : HatchFsmStateMask.Open;
            }

            bool slamEdge = (previousMask & HatchFsmStateMask.Open) != 0u &&
                            (nextMask & HatchFsmStateMask.Closed) != 0u &&
                            delta > safePressure;
            if (slamEdge && EmitAcousticSignals != 0 && HatchAups != null)
            {
                double3 hatchAup = HatchAups[index];
                if (math.all(math.isfinite(hatchAup)))
                {
                    float q = HatchLockMath.Sanitize01(AcousticAuthorityWeight, HatchLockConstants.AuthoritativeQualityWeight);
                    MovementAcousticSignal signal = default;
                    signal.PositionAup = HatchLockMath.PackAup(hatchAup);
                    signal.Volume = math.saturate(0.25f + delta * (0.25f + q * 0.5f));
                    signal.VelocitySq = delta * delta;
                    signal.SourceId = hatch.RoomAHashID != 0u ? hatch.RoomAHashID : HatchLockConstants.SourceHash;
                    signal.LocomotionMode = 0;
                    signal.SurfaceMode = 0;
                    signal.Flags = 1;
                    SignalBus<MovementAcousticSignal>.TryEnqueueBounded(AcousticWriter, AcousticWriterBudget, signal);
                    nextMask |= HatchFsmStateMask.AcousticQueued;
                }
            }

            hatch.FsmStateMask = nextMask;
        }

        private float ResolveStructuralHealth(uint roomA, uint roomB, int preferredIndex, float jamThreshold)
        {
            float health = 1f;
            bool found = false;
            float earlyExitHealth = HatchLockMath.Sanitize01(jamThreshold, HatchLockConstants.DefaultStructuralJamThreshold01);
            if (StructuralStates != null && StructuralStateCount > 0)
            {
                uint fault;
                if ((uint)preferredIndex < (uint)StructuralStateCount)
                {
                    StructuralIntegrityStateDTO state = StructuralStates[preferredIndex];
                    if (state.NodeHash == roomA || state.NodeHash == roomB)
                    {
                        health = math.min(health, HatchLockMath.StructuralHealth01(in state, out fault));
                        found = true;
                        if (health < earlyExitHealth)
                            return health;
                    }
                }

                for (int i = 0; i < StructuralStateCount; i++)
                {
                    StructuralIntegrityStateDTO state = StructuralStates[i];
                    if (state.NodeHash != roomA && state.NodeHash != roomB)
                        continue;

                    health = math.min(health, HatchLockMath.StructuralHealth01(in state, out fault));
                    found = true;
                    if (health < earlyExitHealth)
                        return health;
                }
            }

            if (!found && ModuleIntegrity01 != null && (uint)preferredIndex < (uint)ModuleIntegrityCount)
                health = math.min(health, HatchLockMath.Sanitize01(ModuleIntegrity01[preferredIndex], 1f));

            return health;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordHatchTelemetryJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchStateDTO* Hatches;
        [NativeDisableUnsafePtrRestriction, NoAlias] public HatchTelemetryEntry* Telemetry;
        [NativeDisableUnsafePtrRestriction, NoAlias] public uint* Cursor;
        public int Count;
        public int TelemetryCount;
        public uint Frame;
        public float GlobalQualityWeight;
        public float TickIntervalSeconds;
        public float LastScheduleMicroseconds;
        public uint ExtraFlags;

        public void Execute()
        {
            if (Telemetry == null || Cursor == null || TelemetryCount <= 0)
                return;

            uint activeCount = 0u;
            uint evaluatedCount = 0u;
            uint pressureLockedCount = 0u;
            uint jammedCount = 0u;
            uint catastrophicCount = 0u;
            uint flags = ExtraFlags;
            uint faultRoomHash = 0u;
            float maxDelta = 0f;
            float sumDelta = 0f;
            uint hash = 2166136261u;

            if (Hatches != null && Count > 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    HatchStateDTO hatch = Hatches[i];
                    uint mask = hatch.FsmStateMask;
                    if ((mask & HatchFsmStateMask.Active) == 0u)
                        continue;

                    activeCount++;
                    evaluatedCount++;
                    float delta = hatch.PressureDifferentialATM;
                    if (!math.isfinite(delta))
                    {
                        flags |= HatchTelemetryFlags.NonFinite | HatchTelemetryFlags.DumpRequested;
                        faultRoomHash = hatch.RoomAHashID;
                        delta = 0f;
                    }

                    if ((mask & HatchFsmStateMask.NonFinite) != 0u)
                    {
                        flags |= HatchTelemetryFlags.NonFinite | HatchTelemetryFlags.DumpRequested;
                        faultRoomHash = hatch.RoomAHashID;
                    }

                    if ((mask & HatchFsmStateMask.MissingCompartment) != 0u)
                    {
                        flags |= HatchTelemetryFlags.MissingCompartment;
                        faultRoomHash = hatch.RoomAHashID;
                    }

                    if ((mask & HatchFsmStateMask.PressureLocked) != 0u)
                        pressureLockedCount++;
                    if ((mask & HatchFsmStateMask.StructurallyJammed) != 0u)
                        jammedCount++;
                    if ((mask & HatchFsmStateMask.CatastrophicFlood) != 0u)
                    {
                        catastrophicCount++;
                        flags |= HatchTelemetryFlags.CatastrophicFlood | HatchTelemetryFlags.DumpRequested;
                    }

                    maxDelta = math.max(maxDelta, delta);
                    sumDelta += delta;
                    hash = HatchLockMath.Hash(hash, hatch.RoomAHashID);
                    hash = HatchLockMath.Hash(hash, hatch.RoomBHashID);
                    hash = HatchLockMath.Hash(hash, math.asuint(delta));
                    hash = HatchLockMath.Hash(hash, mask);
                }
            }

            if (LastScheduleMicroseconds > HatchLockConstants.DumpThresholdMicroseconds)
                flags |= HatchTelemetryFlags.SlowTickOverBudget | HatchTelemetryFlags.DumpRequested;

            uint cursor = Cursor[0];
            int telemetryIndex = (int)(cursor % (uint)TelemetryCount);
            Telemetry[telemetryIndex] = new HatchTelemetryEntry
            {
                Frame = Frame,
                ActiveCount = activeCount,
                PressureLockedCount = pressureLockedCount,
                JammedCount = jammedCount,
                CatastrophicFloodCount = catastrophicCount,
                MaxPressureDifferentialATM = maxDelta,
                AveragePressureDifferentialATM = evaluatedCount > 0u ? sumDelta / evaluatedCount : 0f,
                LastScheduleMicroseconds = HatchLockMath.SanitizePositive(LastScheduleMicroseconds, 0f),
                StateHash = hash,
                LastFaultRoomHash = faultRoomHash,
                Flags = flags,
                GlobalQualityWeight = HatchLockMath.Sanitize01(GlobalQualityWeight, 0f),
                TickIntervalSeconds = HatchLockMath.SanitizePositive(TickIntervalSeconds, HatchLockConstants.SurvivalTickIntervalSeconds),
                EvaluatedCount = evaluatedCount
            };
            Cursor[0] = unchecked(cursor + 1u);
        }
    }
}
