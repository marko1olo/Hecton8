using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Vehicles
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeVehicleGridJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Cells is an owner-resolved contiguous grid pointer; Unity cannot attach a safety handle after pointer lowering.
        // Execute validates index < grid cell count before the single Cells[index] initialization write.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A managed grid was rejected because vehicle damage is rollback-facing and Burst-only. A temporary NativeArray copy
        // was rejected because it would duplicate the grid and add a copyback pass.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one grid cell per worker index and no concurrent grid reader until this initialization handle is fenced.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* Cells;
        public int GridWidth;
        public int GridHeight;
        public int GridDepth;
        public float BaseArmor;

        public void Execute(int index)
        {
            int cellCount = GridWidth * GridHeight * GridDepth;
            if (Cells == null || (uint)index >= (uint)cellCount)
                return;

            int x;
            int y;
            int z;
            Decode(index, GridWidth, GridHeight, out x, out y, out z);

            bool outer = x == 0 || y == 0 || z == 0 || x == GridWidth - 1 || y == GridHeight - 1 || z == GridDepth - 1;
            uint component = ResolveComponentHash(x, y, z, GridWidth, GridHeight, GridDepth);
            uint flags = math.select(0u, VehicleDamageConstants.CellFlagOuterHull, outer);
            if (component == VehicleDamageConstants.ComponentEngine)
                flags |= VehicleDamageConstants.CellFlagEngineCritical | VehicleDamageConstants.CellFlagFlammable;
            else if (component == VehicleDamageConstants.ComponentBallast)
                flags |= VehicleDamageConstants.CellFlagBallastCritical;
            else if (component == VehicleDamageConstants.ComponentSensors)
                flags |= VehicleDamageConstants.CellFlagSensorCritical;
            else if (component == VehicleDamageConstants.ComponentPower)
                flags |= VehicleDamageConstants.CellFlagFlammable;

            VehicleGridCellDTO cell = default;
            cell.Integrity01 = 1f;
            cell.ComponentHash = component;
            cell.StatusFlags = flags;
            cell.ArmorValue = math.max(0.01f, BaseArmor * math.select(1f, 1.3f, outer));
            UnsafeUtility.AsRef<VehicleGridCellDTO>(Cells + index) = cell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Decode(int index, int width, int height, out int x, out int y, out int z)
        {
            int layer = width * height;
            z = index / layer;
            int rem = index - (z * layer);
            y = rem / width;
            x = rem - (y * width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveComponentHash(int x, int y, int z, int width, int height, int depth)
        {
            int aftStart = (depth * 5) / 8;
            int bowSensors = depth / 4;
            if (z >= aftStart && y <= (height * 2) / 3)
                return VehicleDamageConstants.ComponentEngine;
            if (y <= height / 3 && z > bowSensors && z < aftStart)
                return VehicleDamageConstants.ComponentBallast;
            if (z <= bowSensors || y >= (height * 2) / 3)
                return VehicleDamageConstants.ComponentSensors;
            if (x == width / 2 || x == (width / 2) - 1)
                return VehicleDamageConstants.ComponentPower;
            return VehicleDamageConstants.ComponentHull;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockVehicleDamageJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Signals is a write-only deterministic mock signal lane. The job checks index < SignalCount before each write.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // NativeQueue output was rejected because downstream jobs need contiguous signal rows. Managed mock payloads were
        // rejected because they allocate and cannot enter Burst/rollback snapshots.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one signal row per worker index; downstream mapping reads Signals only through the returned handle.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Signals;
        public int SignalCount;
        public double3 RootAup;
        public uint Frame;
        public float RadiusMeters;
        public float Magnitude;

        public void Execute(int index)
        {
            if (Signals == null || (uint)index >= (uint)SignalCount)
                return;

            float quality = VehicleDamageConstants.AuthoritativeQualityWeight;
            uint rootHash = FoldAup(RootAup);
            uint hash = Hash((uint)index ^ (Frame * 747796405u) ^ rootHash ^ 0x9E3779B9u);
            Random random = Random.CreateFromIndex(hash);
            float angle = random.NextFloat(0f, 6.28318530718f);
            float side = math.select(1f, -1f, random.NextFloat() < 0.5f);
            float3 local = new float3(
                side * random.NextFloat(2.1f, 2.35f),
                random.NextFloat(-0.35f, 0.35f),
                SinPolynomial(angle, quality) * 3.3f);

            VehicleDamageSignalDTO signal = default;
            signal.ImpactAup = RootAup + new double3(local);
            signal.Direction = NormalizeOrFallback(-local, new float3(0f, 0f, -1f));
            signal.Magnitude = math.max(0f, Magnitude) * math.lerp(0.55f, 1.25f, random.NextFloat());
            signal.DamageType = VehicleDamageConstants.DamageTypeExplosiveMask;
            signal.TargetHash = 0u;
            signal.SourceHash = VehicleDamageConstants.SourceHashMock;
            signal.Frame = Frame;
            signal.SourceId = 0x152;
            signal.TargetId = 0;
            signal.Channel = 0;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
            signal.IntegrityDelta = (byte)math.clamp((int)math.round(math.lerp(8f, 28f, quality)), 1, 64);
            signal.RadiusMeters = math.max(0.05f, RadiusMeters * math.lerp(0.7f, 1.4f, quality));
            signal.Falloff = 1.2f;
            signal.ArmorPierce = 0.2f + (0.5f * quality);
            signal.GridIndex = -1;
            signal.MappedFlags = VehicleDamageConstants.DamageFlagExplosive | VehicleDamageConstants.DamageFlagFiniteAup;
            UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index) = signal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FoldAup(double3 aup)
        {
            long x = (long)math.round(math.clamp(aup.x, -9007199254740991d, 9007199254740991d) * 0.001d);
            long y = (long)math.round(math.clamp(aup.y, -9007199254740991d, 9007199254740991d) * 0.001d);
            long z = (long)math.round(math.clamp(aup.z, -9007199254740991d, 9007199254740991d) * 0.001d);
            uint hash = 2166136261u;
            hash = Mix(hash, x);
            hash = Mix(hash, y);
            hash = Mix(hash, z);
            return Hash(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, long value)
        {
            ulong bits = (ulong)value;
            hash ^= (uint)bits;
            hash *= 16777619u;
            hash ^= (uint)(bits >> 32);
            return hash * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SinPolynomial(float radians, float qualityWeight)
        {
            const float Pi = 3.14159265359f;
            const float TwoPi = 6.28318530718f;
            const float HalfPi = 1.57079632679f;

            float wrapped = radians - (TwoPi * math.floor((radians + Pi) / TwoPi));
            float absWrapped = math.abs(wrapped);
            float reflected = math.sign(wrapped) * (Pi - absWrapped);
            float x = math.select(wrapped, reflected, absWrapped > HalfPi);
            float x2 = x * x;
            float x4 = x2 * x2;
            float sin3 = x * (1f - (x2 * 0.16666666667f));
            float sin7 = x * (1f - (x2 * 0.16666666667f) + (x4 * 0.00833333333f) - (x4 * x2 * 0.00019841269f));
            return math.lerp(sin3, sin7, math.saturate(qualityWeight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) && lengthSq > 0.0001f;
            float3 normalized = value * math.rsqrt(math.max(lengthSq, 0.0001f));
            return math.select(fallback, normalized, valid);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CopyVehicleDamageSignalsJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Source and Destination are distinct signal ranges selected by the owner before scheduling. Bounds checks cover
        // SourceCount, DestinationOffset, and DestinationCapacity before the copy.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A managed copy path was rejected for GC. A queued append path was rejected because it would destroy stable signal
        // ordering and add contention.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is non-overlapping source/destination slices and one destination row per worker index.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Source;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Destination;
        public int SourceCount;
        public int DestinationOffset;
        public int DestinationCapacity;

        public void Execute(int index)
        {
            if (Source == null || Destination == null || (uint)index >= (uint)SourceCount)
                return;

            int destinationIndex = DestinationOffset + index;
            if ((uint)destinationIndex >= (uint)DestinationCapacity)
                return;

            UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Destination + destinationIndex) =
                UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Source + index);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MapImpactToGridJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Cells and Signals are owner-provided pointer lanes. Signal index and derived grid coordinates are bounded before
        // any damage contribution is written.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Physics overlap/raycast fanout was rejected as nondeterministic and CPU-heavy. A staged managed signal list was
        // rejected because it would allocate and break contiguous SIMD traversal.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is deterministic signal-to-cell mapping under a single scheduled writer phase; reducers consume the
        // grid only after this handle is chained.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* Cells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Signals;
        public int SignalCount;
        public int GridWidth;
        public int GridHeight;
        public int GridDepth;
        public double3 RootAup;
        public quaternion InverseRootRotation;
        public float3 GridCenterLocal;
        public float3 GridSizeLocal;
        public float DirectDamageScale;

        public void Execute(int index)
        {
            if (Cells == null || Signals == null || (uint)index >= (uint)SignalCount)
                return;

            VehicleDamageSignalDTO signal = UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index);
            signal.GridIndex = -1;
            signal.MappedFlags &= ~VehicleDamageConstants.DamageFlagMapped;

            if (!math.all(math.isfinite(signal.ImpactAup)) || !math.all(math.isfinite(RootAup)))
            {
                UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index) = signal;
                return;
            }

            double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(signal.ImpactAup, RootAup);
            float3 rootRelative = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
            if (!math.all(math.isfinite(rootRelative)))
            {
                UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index) = signal;
                return;
            }

            float3 local = math.mul(InverseRootRotation, rootRelative);
            signal.LocalPoint = local;
            signal.MappedFlags |= VehicleDamageConstants.DamageFlagFiniteAup;

            if (!TryResolveCell(local, GridWidth, GridHeight, GridDepth, GridCenterLocal, GridSizeLocal, out int cellIndex))
            {
                UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index) = signal;
                return;
            }

            signal.GridIndex = cellIndex;
            signal.MappedFlags |= VehicleDamageConstants.DamageFlagMapped;
            if ((signal.DamageType & VehicleDamageConstants.DamageTypeExplosiveMask) != 0u)
                signal.MappedFlags |= VehicleDamageConstants.DamageFlagExplosive;

            UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + index) = signal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveCell(
            float3 local,
            int width,
            int height,
            int depth,
            float3 center,
            float3 size,
            out int index)
        {
            index = -1;
            if (width <= 0 || height <= 0 || depth <= 0 || !math.all(math.isfinite(local)))
                return false;

            float3 safeSize = math.max(size, new float3(0.001f));
            float3 gridMin = center - (safeSize * 0.5f);
            float3 normalized = (local - gridMin) / safeSize;
            if (normalized.x < 0f || normalized.y < 0f || normalized.z < 0f ||
                normalized.x >= 1f || normalized.y >= 1f || normalized.z >= 1f)
            {
                return false;
            }

            int x = math.clamp((int)math.floor(normalized.x * width), 0, width - 1);
            int y = math.clamp((int)math.floor(normalized.y * height), 0, height - 1);
            int z = math.clamp((int)math.floor(normalized.z * depth), 0, depth - 1);
            index = x + (y * width) + (z * width * height);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDirectDamage(float magnitude, byte integrityDelta, float scale)
        {
            float signalMagnitude = math.select(0f, math.max(0f, magnitude), math.isfinite(magnitude));
            float byteDamage = integrityDelta * (1f / 255f);
            float energyDamage = math.saturate(signalMagnitude * 0.000015f);
            return math.saturate((byteDamage + energyDamage) * math.max(0f, scale));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyVehicleDamageReductionJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Cells is the mutable damage grid and Signals is read-only impact input. Unity cannot prove that distinction once
        // they are raw pointers, so the job carries explicit counts for both lanes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Duplicating Cells into a temporary reduction buffer was rejected because it adds bandwidth and copyback cost.
        // A managed dictionary by component was rejected because it is not Burst-compatible.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one grid cell per worker index for mutation; Signals remains immutable during this job's handle.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* Cells;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Signals;
        public int SignalCount;
        public int CellCount;
        public int GridWidth;
        public int GridHeight;
        public int GridDepth;
        public float3 GridSizeLocal;
        public float DirectDamageScale;
        public float ExplosionFalloff;

        public void Execute(int cellIndex)
        {
            if (Cells == null || Signals == null || (uint)cellIndex >= (uint)CellCount)
                return;

            int width = math.max(1, GridWidth);
            int height = math.max(1, GridHeight);
            int depth = math.max(1, GridDepth);
            int gridCellCount = math.min(CellCount, width * height * depth);
            if ((uint)cellIndex >= (uint)gridCellCount)
                return;

            float quality = VehicleDamageConstants.AuthoritativeQualityWeight;
            float3 safeGridSize = math.select(new float3(0.001f), GridSizeLocal, math.all(math.isfinite(GridSizeLocal)));
            float directScale = math.select(0f, DirectDamageScale, math.isfinite(DirectDamageScale));
            float explosionFalloff = math.select(0f, ExplosionFalloff, math.isfinite(ExplosionFalloff));
            float3 cellSize = safeGridSize / new float3(width, height, depth);
            float minCell = math.max(0.01f, math.cmin(math.abs(cellSize)));
            int qualityRadius = math.clamp(1 + (int)math.floor(math.lerp(0f, 5f, quality * quality)), 1, 6);
            int signalLimit = math.min(math.max(0, SignalCount), VehicleDamageConstants.MaxDamageSignals);
            Decode(cellIndex, width, height, out int x, out int y, out int z);

            float accumulatedDamage = 0f;
            for (int signalIndex = 0; signalIndex < signalLimit; signalIndex++)
            {
                VehicleDamageSignalDTO signal = UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + signalIndex);
                bool mapped = (signal.MappedFlags & VehicleDamageConstants.DamageFlagMapped) != 0u &&
                    (uint)signal.GridIndex < (uint)gridCellCount;
                float mappedMask = math.select(0f, 1f, mapped);
                int safeGridIndex = math.clamp(signal.GridIndex, 0, math.max(0, gridCellCount - 1));

                float directMatch = math.select(0f, 1f, signal.GridIndex == cellIndex);
                float directDamage = MapImpactToGridJob.ResolveDirectDamage(
                    signal.Magnitude,
                    signal.IntegrityDelta,
                    directScale) * (1f + math.saturate(signal.ArmorPierce)) * directMatch;

                bool explosive = (signal.MappedFlags & VehicleDamageConstants.DamageFlagExplosive) != 0u;
                float explosiveMask = math.select(0f, 1f, explosive);
                float signalRadius = math.select(0.01f, signal.RadiusMeters, math.isfinite(signal.RadiusMeters));
                int radiusCells = math.clamp((int)math.ceil(math.max(0.01f, signalRadius) / minCell), 1, qualityRadius);
                Decode(safeGridIndex, width, height, out int cx, out int cy, out int cz);
                int dx = x - cx;
                int dy = y - cy;
                int dz = z - cz;
                float distSq = (dx * dx) + (dy * dy) + (dz * dz);
                bool insideRadius = distSq >= 0.0001f && distSq <= radiusCells * radiusCells;
                float baseDamage = MapImpactToGridJob.ResolveDirectDamage(signal.Magnitude, signal.IntegrityDelta, 0.55f);
                float signalFalloff = math.select(0f, signal.Falloff, math.isfinite(signal.Falloff));
                float falloff = math.max(0.15f, explosionFalloff + signalFalloff);
                float attenuation = 1f / math.max(0.0001f, 1f + (distSq * falloff));
                float propagatedDamage = baseDamage * attenuation * math.select(0f, 1f, insideRadius) * explosiveMask;

                float damage = (directDamage + propagatedDamage) * mappedMask;
                accumulatedDamage += math.select(0f, damage, math.isfinite(damage));
            }

            ref VehicleGridCellDTO cell = ref UnsafeUtility.AsRef<VehicleGridCellDTO>(Cells + cellIndex);
            float current = math.saturate(math.select(0f, cell.Integrity01, math.isfinite(cell.Integrity01)));
            float armor = math.max(0.01f, math.select(1f, cell.ArmorValue, math.isfinite(cell.ArmorValue)));
            cell.Integrity01 = math.saturate(current - (math.max(0f, accumulatedDamage) / armor));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Decode(int index, int width, int height, out int x, out int y, out int z)
        {
            int layer = width * height;
            z = index / layer;
            int rem = index - (z * layer);
            y = rem / width;
            x = rem - (y * width);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateVehicleSystemsJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Cells/Signals are read lanes and StateWrite is a single owner state slot. Pointer safety cannot infer that
        // StateWrite does not alias the grid/signal payloads, so the job validates counts and uses one writer phase.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Splitting this into per-component managed objects was rejected for virtual dispatch and GC. A second state
        // shadow buffer was rejected because it creates duplicate authority.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one system-evaluation writer: only this job writes StateWrite before the publish job fences it.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* Cells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageSignalDTO* Signals;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageStateDTO* StateWrite;
        [NoAlias] public NativeArray<VehicleDamageTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<uint> TelemetryCursor;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns the vehicle hazard lane and this job receives only a producer-side ParallelWriter.
        // Unity cannot model the external one-producer queue contract, so the container safety warning is a
        // false positive for this write-only lane. [NoAlias] proves the queue writer is not overlapping the
        // pointer-backed vehicle grid, signal buffer, state write slot, or telemetry arrays.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected a managed event bridge because it allocates and escapes the deterministic job graph.
        // Rejected a post-job main-thread hazard scan because it duplicates the grid walk and delays hazard
        // publication. Rejected a local NativeArray staging lane because it adds another buffer and compaction pass.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The scheduler creates one HazardWriter for this EvaluateVehicleSystemsJob pass, drains only after
        // the returned JobHandle has completed, and does not resize or dispose the SignalBus lane while the
        // job is alive. The job never reads from the queue and no second vehicle-hazard producer is scheduled
        // against the same queue handle in this pass.
        [NoAlias, NativeDisableContainerSafetyRestriction]
        public global::Hecton8.Core.MpscSignalRingBuffer<VehicleHazardSignal>.ParallelWriter HazardWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> HazardWriterBudget;
        public int CellCount;
        public int SignalCount;
        public uint Frame;
        public uint VehicleHash;
        public double3 RootAup;
        public float FixedDeltaTime;
        public float RootDepthMeters;
        public float GlobalQualityWeight;
        public VehicleDamageTuningDTO Tuning;

        public void Execute()
        {
            if (Cells == null || StateWrite == null || CellCount <= 0)
                return;

            float engineSum = 0f;
            float ballastSum = 0f;
            float sensorSum = 0f;
            float hullSum = 0f;
            int engineCount = 0;
            int ballastCount = 0;
            int sensorCount = 0;
            int hullCount = 0;
            int damaged = 0;
            int destroyed = 0;
            int burning = 0;
            int breaches = 0;
            int signalDrops = 0;
            float ingress = 0f;
            float structuralSum = 0f;
            float visualQuality = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float authorityQuality = VehicleDamageConstants.AuthoritativeQualityWeight;
            float fireChance = math.saturate(Tuning.FireChance01 * math.lerp(0.35f, 1.65f, authorityQuality));
            float rootDepth = math.max(0f, math.select(0f, RootDepthMeters, math.isfinite(RootDepthMeters)));

            for (int i = 0; i < CellCount; i++)
            {
                ref VehicleGridCellDTO cell = ref UnsafeUtility.AsRef<VehicleGridCellDTO>(Cells + i);
                float integrity = math.saturate(math.select(0f, cell.Integrity01, math.isfinite(cell.Integrity01)));
                if (!math.isfinite(cell.Integrity01))
                    cell.Integrity01 = 0f;

                uint flags = cell.StatusFlags;
                if (integrity < 0.999f)
                    damaged++;
                if (integrity <= 0.0001f)
                {
                    flags |= VehicleDamageConstants.CellFlagDestroyed;
                    destroyed++;
                }

                if ((flags & VehicleDamageConstants.CellFlagOuterHull) != 0u && integrity < 0.35f)
                {
                    float severity = math.saturate((0.35f - integrity) / 0.35f);
                    flags |= VehicleDamageConstants.CellFlagFlooded;
                    breaches++;
                    ingress += severity * (12f + rootDepth * 0.08f) * math.max(0f, Tuning.IngressKgPerSecond);
                    if (severity > 0.5f && !EmitHazard(i, cell.ComponentHash, flags, severity, VehicleDamageConstants.HazardFlood))
                        signalDrops++;
                }

                if ((flags & VehicleDamageConstants.CellFlagFlammable) != 0u && integrity < 0.42f)
                {
                    float probability = fireChance * math.saturate((0.42f - integrity) / 0.42f);
                    uint seed = Hash((uint)i ^ (Frame * 1103515245u) ^ VehicleHash ^ 0xA53A9D31u);
                    Random random = Random.CreateFromIndex(seed);
                    if (random.NextFloat() < probability)
                        flags |= VehicleDamageConstants.CellFlagBurning;
                }

                if ((flags & VehicleDamageConstants.CellFlagBurning) != 0u)
                {
                    burning++;
                    if (!EmitHazard(i, cell.ComponentHash, flags, 1f - integrity, VehicleDamageConstants.HazardFire))
                        signalDrops++;
                }

                if ((flags & VehicleDamageConstants.CellFlagDestroyed) != 0u && integrity <= 0.0001f)
                {
                    if (!EmitHazard(i, cell.ComponentHash, flags, 1f, VehicleDamageConstants.HazardDestroyed))
                        signalDrops++;
                }

                cell.StatusFlags = flags;
                structuralSum += integrity;

                if (cell.ComponentHash == VehicleDamageConstants.ComponentEngine)
                {
                    engineSum += integrity;
                    engineCount++;
                }
                else if (cell.ComponentHash == VehicleDamageConstants.ComponentBallast)
                {
                    ballastSum += integrity;
                    ballastCount++;
                }
                else if (cell.ComponentHash == VehicleDamageConstants.ComponentSensors)
                {
                    sensorSum += integrity;
                    sensorCount++;
                }
                else
                {
                    hullSum += integrity;
                    hullCount++;
                }
            }

            VehicleDamageStateDTO state = UnsafeUtility.AsRef<VehicleDamageStateDTO>(StateWrite);
            float dt = math.clamp(math.select(0.0166667f, FixedDeltaTime, math.isfinite(FixedDeltaTime)), 0.001f, 0.05f);
            float floodLimit = math.max(0f, Tuning.FloodMassLimitKg);
            float previousFlood = math.saturate(state.FloodWaterMassKg / math.max(1f, floodLimit)) * floodLimit;
            state.FloodWaterMassKg = math.min(floodLimit, previousFlood + (ingress * dt));
            state.IngressRateKgPerSecond = ingress;

            float engine01 = math.select(1f, engineSum / math.max(1, engineCount), engineCount > 0);
            float ballast01 = math.select(1f, ballastSum / math.max(1, ballastCount), ballastCount > 0);
            float sensor01 = math.select(1f, sensorSum / math.max(1, sensorCount), sensorCount > 0);
            float hull01 = math.select(1f, hullSum / math.max(1, hullCount), hullCount > 0);
            float structural01 = structuralSum / math.max(1, CellCount);
            float flood01 = math.saturate(state.FloodWaterMassKg / math.max(1f, floodLimit));
            float breach01 = math.saturate(breaches / math.max(1f, CellCount * 0.08f));
            float fire01 = math.saturate(burning / math.max(1f, CellCount * 0.04f));

            state.MaxThrustScalar = math.saturate(math.max(Tuning.EngineMinimumScalar, engine01 - (fire01 * 0.12f)));
            state.BuoyancyScalar = math.saturate(math.max(Tuning.BallastMinimumScalar, ballast01 - (flood01 * 0.35f)));
            state.SensorScalar = math.saturate(math.max(Tuning.SensorMinimumScalar, sensor01 - (fire01 * 0.08f)));
            state.DragScalar = math.max(1f, 1f + (breach01 * math.max(0f, Tuning.DragPenaltyWeight)) + ((1f - hull01) * 0.18f));
            state.FireSeverity01 = fire01;
            state.StructuralIntegrity01 = structural01;
            state.ActiveBreaches = (uint)breaches;
            state.BurningCells = (uint)burning;
            state.DestroyedCells = (uint)destroyed;
            state.DamagedCells = (uint)damaged;
            state.Frame = Frame;
            state.SignalCount = (uint)math.max(0, SignalCount);
            state.TotalDamage01 = 1f - structural01;
            state.QualityWeight = visualQuality;
            state.Flags = VehicleDamageConstants.StateFlagInitialized;
            if (breaches > 0) state.Flags |= VehicleDamageConstants.StateFlagHasBreach;
            if (burning > 0) state.Flags |= VehicleDamageConstants.StateFlagHasFire;
            if ((Tuning.Flags & VehicleDamageConstants.TuningFlagCsvLayout) != 0u) state.Flags |= VehicleDamageConstants.StateFlagCsvLayout;
            if (signalDrops > 0) state.Flags |= VehicleDamageConstants.StateFlagSignalDrop;

            if (Signals != null && SignalCount > 0)
            {
                VehicleDamageSignalDTO last = UnsafeUtility.AsRef<VehicleDamageSignalDTO>(Signals + math.min(SignalCount - 1, VehicleDamageConstants.MaxDamageSignals - 1));
                state.LastImpactAup = last.ImpactAup;
                state.LastImpactLocal = last.LocalPoint;
            }

            bool finite = IsFinite(in state);
            if (!finite)
                state.Flags |= VehicleDamageConstants.StateFlagFatalNan;

            state.EstimatedCostUs = (CellCount * 0.012f) + (SignalCount * 0.18f) + (breaches * 0.06f);
            state.StateHash = HashState(in state);
            UnsafeUtility.AsRef<VehicleDamageStateDTO>(StateWrite) = state;
            WriteTelemetry(in state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EmitHazard(int cellIndex, uint componentHash, uint flags, float severity, byte hazardType)
        {
            VehicleHazardSignal signal = default;
            signal.LocalPosition = DecodeLocal(cellIndex);
            signal.Severity01 = math.saturate(severity);
            signal.ComponentHash = componentHash;
            signal.StatusFlags = flags;
            signal.Frame = Frame;
            signal.VehicleHash = VehicleHash;
            signal.HazardType = hazardType;
            signal.Flags = 1;
            signal.CellIndex = (ushort)math.min(cellIndex, 65535);
            return SignalBus<VehicleHazardSignal>.TryEnqueueBounded(HazardWriter, HazardWriterBudget, signal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 DecodeLocal(int index)
        {
            int width = math.max(1, Tuning.GridWidth);
            int height = math.max(1, Tuning.GridHeight);
            int layer = width * height;
            int z = index / layer;
            int rem = index - (z * layer);
            int y = rem / width;
            int x = rem - (y * width);
            float3 size = math.max(Tuning.GridSizeLocal, new float3(0.001f));
            float3 gridMin = Tuning.GridCenterLocal - (size * 0.5f);
            float3 cellSize = size / new float3(width, height, math.max(1, Tuning.GridDepth));
            return gridMin + (cellSize * (new float3(x, y, z) + 0.5f));
        }

        private void WriteTelemetry(in VehicleDamageStateDTO state)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            uint cursor = TelemetryCursor[0];
            int index = (int)(cursor % (uint)math.min(Telemetry.Length, VehicleDamageConstants.TelemetryCapacity));
            VehicleDamageTelemetryEntry entry = default;
            entry.RootAup = RootAup;
            entry.LastImpactAup = state.LastImpactAup;
            entry.LastImpactLocal = state.LastImpactLocal;
            entry.StructuralIntegrity01 = state.StructuralIntegrity01;
            entry.MaxThrustScalar = state.MaxThrustScalar;
            entry.BuoyancyScalar = state.BuoyancyScalar;
            entry.FloodWaterMassKg = state.FloodWaterMassKg;
            entry.IngressRateKgPerSecond = state.IngressRateKgPerSecond;
            entry.FireSeverity01 = state.FireSeverity01;
            entry.EstimatedCostUs = state.EstimatedCostUs;
            entry.Frame = state.Frame;
            entry.StateHash = state.StateHash;
            entry.Flags = state.Flags;
            entry.ActiveBreaches = state.ActiveBreaches;
            entry.BurningCells = state.BurningCells;
            entry.DestroyedCells = state.DestroyedCells;
            entry.DamagedCells = state.DamagedCells;
            entry.SignalCount = state.SignalCount;
            entry.TotalDamage01 = state.TotalDamage01;
            Telemetry[index] = entry;
            TelemetryCursor[0] = cursor + 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in VehicleDamageStateDTO state)
        {
            return math.isfinite(state.MaxThrustScalar) &&
                   math.isfinite(state.BuoyancyScalar) &&
                   math.isfinite(state.SensorScalar) &&
                   math.isfinite(state.DragScalar) &&
                   math.isfinite(state.FloodWaterMassKg) &&
                   math.isfinite(state.IngressRateKgPerSecond) &&
                   math.isfinite(state.FireSeverity01) &&
                   math.isfinite(state.StructuralIntegrity01) &&
                   math.all(math.isfinite(state.LastImpactAup)) &&
                   math.all(math.isfinite(state.LastImpactLocal));
        }

        private static uint HashState(in VehicleDamageStateDTO state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(state.MaxThrustScalar));
            hash = Mix(hash, math.asuint(state.BuoyancyScalar));
            hash = Mix(hash, math.asuint(state.SensorScalar));
            hash = Mix(hash, math.asuint(state.DragScalar));
            hash = Mix(hash, math.asuint(state.FloodWaterMassKg));
            hash = Mix(hash, state.ActiveBreaches);
            hash = Mix(hash, state.BurningCells);
            hash = Mix(hash, state.DestroyedCells);
            hash = Mix(hash, state.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PublishVehicleDamageStateJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Publish uses explicit read/write pointer pairs for grid and state so rollback-safe publication can swap/copy
        // without managed containers. Unity cannot model the owner-established non-overlap of those pairs.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // In-place mutation was rejected because readers need a stable prior state. Managed publication was rejected
        // because the payload is Burst/rollback-facing.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is two disjoint buffer pairs: GridWrite/StateWrite are completed producer outputs and
        // GridRead/StateRead are publication buffers overwritten by this fenced job before readers observe them.
        // Grid capacity is passed explicitly so a corrupt caller cannot turn the bulk publish copy into an overrun.
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* GridWrite;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleGridCellDTO* GridRead;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageStateDTO* StateWrite;
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleDamageStateDTO* StateRead;
        public int CellCount;
        public int GridWriteCapacity;
        public int GridReadCapacity;

        public void Execute()
        {
            if (GridWrite != null &&
                GridRead != null &&
                CellCount > 0 &&
                CellCount <= GridWriteCapacity &&
                CellCount <= GridReadCapacity)
            {
                long bytes = (long)CellCount * UnsafeUtility.SizeOf<VehicleGridCellDTO>();
                UnsafeUtility.MemCpy(GridRead, GridWrite, bytes);
            }

            if (StateWrite != null && StateRead != null)
                UnsafeUtility.MemCpy(StateRead, StateWrite, UnsafeUtility.SizeOf<VehicleDamageStateDTO>());
        }
    }
}
