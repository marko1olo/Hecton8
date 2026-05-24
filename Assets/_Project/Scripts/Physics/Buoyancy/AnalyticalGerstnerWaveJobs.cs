using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockWaveSpectrumJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<GerstnerWaveParamsDTO> Spectrum;
        [NoAlias] public NativeArray<GerstnerWaveTuningDTO> Tuning;
        public float WindDirectionRadians;
        public float WindSpeedMetersPerSecond;
        public float GlobalQualityWeight;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if (!Spectrum.IsCreated || (uint)index >= (uint)Spectrum.Length)
                return;

            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float wind = math.max(0.01f, math.select(10f, WindSpeedMetersPerSecond, math.isfinite(WindSpeedMetersPerSecond)));
            float windDirection = math.select(0.35f, WindDirectionRadians, math.isfinite(WindDirectionRadians));
            GerstnerWaveParamsDTO row = default;
            row.Wave1 = BuildWave(index * 4 + 0, windDirection, wind, q, FrameIndex);
            row.Wave2 = BuildWave(index * 4 + 1, windDirection, wind, q, FrameIndex);
            row.Wave3 = BuildWave(index * 4 + 2, windDirection, wind, q, FrameIndex);
            row.Wave4 = BuildWave(index * 4 + 3, windDirection, wind, q, FrameIndex);
            Spectrum[index] = row;

            if (index == 0 && Tuning.IsCreated && Tuning.Length > 0)
            {
                GerstnerWaveTuningDTO tuning = Tuning[0];
                tuning.GlobalQualityWeight = q;
                tuning.WindDirectionRadians = windDirection;
                tuning.WindSpeedMetersPerSecond = wind;
                tuning.StormWeight01 = math.saturate((wind - 2f) * math.rcp(30f));
                tuning.TotalOctaves = AnalyticalGerstnerWaveConstants.MaxOctaves;
                tuning.MaxOctaveLimit = math.clamp(tuning.MaxOctaveLimit <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.MaxOctaveLimit, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
                tuning.LargestWavelengthMeters = math.max(16f, tuning.LargestWavelengthMeters);
                tuning.FrameIndex = FrameIndex;
                Tuning[0] = tuning;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 BuildWave(int octave, float windDirection, float windSpeed, float quality, uint frame)
        {
            float octave01 = math.saturate(octave * (1f / math.max(1f, AnalyticalGerstnerWaveConstants.MaxOctaves - 1f)));
            float angleJitter = HashToSigned01((uint)(octave + 1) * 0x9E3779B9u + frame) * math.lerp(0.08f, 0.28f, quality);
            float wavelength = math.lerp(128f, 6f, octave01);
            float steepness = math.lerp(0.018f, 0.11f, quality) * math.lerp(1f, 0.42f, octave01);
            float speed = math.lerp(0.18f, 1.45f, octave01) * math.lerp(0.65f, 1.55f, math.saturate(windSpeed * (1f / 28f)));
            return new float4(windDirection + angleJitter + octave * 0.37f, steepness, wavelength, speed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash32(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return math.select(1u, x, x != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToSigned01(uint x)
        {
            return ((Hash32(x) & 0x00FFFFFFu) * (1f / 16777215f)) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockWaveRequestsJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<OceanSampleRequestDTO> Requests;
        public int Count;
        public double3 OriginAUP;
        public uint FrameIndex;
        public uint OriginShiftSequence;

        public void Execute(int index)
        {
            if (!Requests.IsCreated)
                return;

            int count = math.min(math.max(0, Count), Requests.Length);
            if ((uint)index >= (uint)count)
                return;

            float x = ((index & 255) - 127.5f) * 1.5f;
            float z = (((index >> 8) & 255) - 127.5f) * 1.5f;
            uint hash = ((uint)index + 1u) * 2654435761u;
            OceanSampleRequestDTO request = default;
            request.SampleAUP = OriginAUP + new double3(x, 0d, z);
            request.EntityHashID = math.select(1u, hash, hash != 0u);
            request.Priority = (byte)(index & 255);
            request.Flags = (byte)(AnalyticalGerstnerWaveConstants.FlagActive | AnalyticalGerstnerWaveConstants.FlagMock);
            request.MinSpatialLengthMeters = 0.25f;
            request.RadiusMeters = 0.5f;
            request.ShiftFrameID = OriginShiftSequence;
            request.RequestFrame = FrameIndex;
            Requests[index] = request;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BuildMacroSwellGridJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<GerstnerWaveParamsDTO> Spectrum;
        [WriteOnly, NoAlias] public NativeArray<float> MacroGrid;
        public GerstnerWaveTuningDTO Tuning;

        public void Execute(int index)
        {
            if (!MacroGrid.IsCreated || !Spectrum.IsCreated)
                return;

            int resolution = ResolveGridResolution(Tuning.MacroGridResolution);
            int cellCount = resolution * resolution;
            if ((uint)index >= (uint)math.min(cellCount, MacroGrid.Length))
                return;

            int x = index % resolution;
            int z = index / resolution;
            float cellSize = math.max(0.25f, Sanitize(Tuning.MacroGridCellSizeMeters, AnalyticalGerstnerWaveConstants.DefaultMacroGridCellSizeMeters));
            float2 local = new float2(
                Tuning.MacroGridOriginX + (x - resolution * 0.5f) * cellSize,
                Tuning.MacroGridOriginZ + (z - resolution * 0.5f) * cellSize);
            int octaves = math.min(2, AnalyticalGerstnerWaveMath.ResolveActiveOctaves(in Tuning));
            float height = 0f;
            float3 normal;
            float3 displacement;
            AnalyticalGerstnerWaveMath.EvaluateScalar(
                local,
                Spectrum,
                octaves,
                in Tuning,
                out height,
                out normal,
                out displacement);
            MacroGrid[index] = math.select(0f, height, math.isfinite(height));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveGridResolution(int requested)
        {
            return math.clamp(requested <= 0 ? 32 : requested, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct EvaluateAnalyticalWavesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<OceanSampleRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<GerstnerWaveParamsDTO> Spectrum;
        [ReadOnly, NoAlias] public NativeArray<float> MacroGrid;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute index owns the four result rows [index*4, index*4+3]. Unity's default
        // ParallelFor safety only understands Results[index], so a vectorized four-lane write is
        // flagged even though the lane ranges are disjoint.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected scalar IJobParallelFor because the assignment demands explicit float4 trig
        // batching. Rejected a temporary vector output buffer because it adds a second write pass
        // and another Vault route for data that already has a single owner.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: scheduled length is ceil(SampleCount / 4). Two different job indices cannot
        // write the same result lane because baseIndex = index * 4 is injective. Counters use
        // Interlocked increments into 64-byte lanes. No other job writes Results while this handle is live.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<OceanSampleResultDTO> Results;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<WaveMathCounterLane> Counters;
        public GerstnerWaveTuningDTO Tuning;
        public int SampleCount;

        public void Execute(int groupIndex)
        {
            if (!Requests.IsCreated || !Results.IsCreated || !Spectrum.IsCreated)
                return;

            int count = math.min(math.max(0, SampleCount), math.min(Requests.Length, Results.Length));
            int baseIndex = groupIndex * 4;
            if ((uint)baseIndex >= (uint)count)
                return;

            OceanSampleRequestDTO* requestPtr = (OceanSampleRequestDTO*)Requests.GetUnsafeReadOnlyPtr();
            OceanSampleResultDTO* resultPtr = (OceanSampleResultDTO*)Results.GetUnsafePtr();
            int counterLength = Counters.IsCreated ? Counters.Length : 0;
            WaveMathCounterLane* counterPtr = counterLength > 0 ? (WaveMathCounterLane*)Counters.GetUnsafePtr() : null;

            bool4 laneActive = new bool4(
                baseIndex < count,
                baseIndex + 1 < count,
                baseIndex + 2 < count,
                baseIndex + 3 < count);

            OceanSampleRequestDTO r0 = laneActive.x ? UnsafeUtility.AsRef<OceanSampleRequestDTO>(requestPtr + baseIndex) : default;
            OceanSampleRequestDTO r1 = laneActive.y ? UnsafeUtility.AsRef<OceanSampleRequestDTO>(requestPtr + baseIndex + 1) : default;
            OceanSampleRequestDTO r2 = laneActive.z ? UnsafeUtility.AsRef<OceanSampleRequestDTO>(requestPtr + baseIndex + 2) : default;
            OceanSampleRequestDTO r3 = laneActive.w ? UnsafeUtility.AsRef<OceanSampleRequestDTO>(requestPtr + baseIndex + 3) : default;

            bool4 shiftMatch = new bool4(
                laneActive.x & (r0.ShiftFrameID == Tuning.OriginShiftSequence),
                laneActive.y & (r1.ShiftFrameID == Tuning.OriginShiftSequence),
                laneActive.z & (r2.ShiftFrameID == Tuning.OriginShiftSequence),
                laneActive.w & (r3.ShiftFrameID == Tuning.OriginShiftSequence));
            bool4 solveActive = laneActive & shiftMatch;
            StoreStaleResult(resultPtr, baseIndex, r0, laneActive.x & !shiftMatch.x);
            StoreStaleResult(resultPtr, baseIndex + 1, r1, laneActive.y & !shiftMatch.y);
            StoreStaleResult(resultPtr, baseIndex + 2, r2, laneActive.z & !shiftMatch.z);
            StoreStaleResult(resultPtr, baseIndex + 3, r3, laneActive.w & !shiftMatch.w);

            float2 l0 = solveActive.x ? LocalizeAupXZ(r0.SampleAUP, in Tuning) : float2.zero;
            float2 l1 = solveActive.y ? LocalizeAupXZ(r1.SampleAUP, in Tuning) : float2.zero;
            float2 l2 = solveActive.z ? LocalizeAupXZ(r2.SampleAUP, in Tuning) : float2.zero;
            float2 l3 = solveActive.w ? LocalizeAupXZ(r3.SampleAUP, in Tuning) : float2.zero;
            float4 x = new float4(l0.x, l1.x, l2.x, l3.x);
            float4 z = new float4(l0.y, l1.y, l2.y, l3.y);

            float octaveBudget = AnalyticalGerstnerWaveMath.ResolveOctaveBudget(in Tuning);
            int activeOctaves = AnalyticalGerstnerWaveMath.ResolveActiveOctaves(octaveBudget, in Tuning);
            float quality = AnalyticalGerstnerWaveMath.ResolveQuality01(in Tuning);
            bool4 useCoarse = ResolveCoarseMask(r0, r1, r2, r3, solveActive);
            float4 height = new float4(0f);
            float4 dispX = new float4(0f);
            float4 dispZ = new float4(0f);
            float4 slopeX = new float4(0f);
            float4 slopeZ = new float4(0f);

            if (math.any(solveActive & !useCoarse))
            {
                for (int octave = 0; octave < activeOctaves; octave++)
                {
                    float4 wave = AnalyticalGerstnerWaveMath.ReadWave(Spectrum, octave);
                    float angle = wave.x;
                    float steepness = math.max(0f, math.select(0f, wave.y, math.isfinite(wave.y)));
                    float wavelength = math.max(0.01f, math.select(1f, wave.z, math.isfinite(wave.z)));
                    float speed = math.max(0.01f, math.select(1f, wave.w, math.isfinite(wave.w)));
                    float2 direction = new float2(
                        AnalyticalGerstnerWaveMath.CosPolynomial(angle, quality),
                        AnalyticalGerstnerWaveMath.SinPolynomial(angle, quality));
                    float waveNumber = AnalyticalGerstnerWaveConstants.TwoPi * math.rcp(wavelength);
                    float amplitude = AnalyticalGerstnerWaveMath.ResolveAmplitude(steepness, wavelength, in Tuning) *
                        AnalyticalGerstnerWaveMath.ResolveOctaveWeight(octave, octaveBudget);
                    float phaseVelocity = AnalyticalGerstnerWaveMath.ResolveDeepWaterPhaseVelocity(speed, waveNumber);
                    float timePhase = AnalyticalGerstnerWaveMath.ResolveTimePhaseModulo(phaseVelocity, waveNumber, in Tuning);
                    float originProjectionMeters = AnalyticalGerstnerWaveMath.ResolveOriginProjectionModulo(direction, wavelength, in Tuning);
                    float4 projectedMeters = (direction.x * x) + (direction.y * z) + new float4(originProjectionMeters);
                    float4 phase = WrapPhase((waveNumber * projectedMeters) - new float4(timePhase) + octave * 0.173f);
                    float4 sinPhase = AnalyticalGerstnerWaveMath.SinPolynomial(phase, quality);
                    float4 cosPhase = AnalyticalGerstnerWaveMath.CosPolynomial(phase, quality);
                    height += amplitude * cosPhase;
                    float slope = amplitude * waveNumber;
                    slopeX += direction.x * slope * sinPhase;
                    slopeZ += direction.y * slope * sinPhase;
                    dispX += steepness * amplitude * direction.x * cosPhase;
                    dispZ += steepness * amplitude * direction.y * cosPhase;
                }
            }

            float4 coarseHeight = SampleMacroHeight4(l0, l1, l2, l3);
            height = math.select(height + new float4(Tuning.SeaLevelY), coarseHeight, useCoarse);
            dispX = math.select(dispX, new float4(0f), useCoarse);
            dispZ = math.select(dispZ, new float4(0f), useCoarse);
            slopeX = math.select(slopeX, new float4(0f), useCoarse);
            slopeZ = math.select(slopeZ, new float4(0f), useCoarse);

            StoreResult(resultPtr, baseIndex, r0, height.x, slopeX.x, slopeZ.x, dispX.x, dispZ.x, useCoarse.x, solveActive.x);
            StoreResult(resultPtr, baseIndex + 1, r1, height.y, slopeX.y, slopeZ.y, dispX.y, dispZ.y, useCoarse.y, solveActive.y);
            StoreResult(resultPtr, baseIndex + 2, r2, height.z, slopeX.z, slopeZ.z, dispX.z, dispZ.z, useCoarse.z, solveActive.z);
            StoreResult(resultPtr, baseIndex + 3, r3, height.w, slopeX.w, slopeZ.w, dispX.w, dispZ.w, useCoarse.w, solveActive.w);

            if (counterPtr != null)
            {
                int activeLanes = math.select(0, 1, solveActive.x) + math.select(0, 1, solveActive.y) + math.select(0, 1, solveActive.z) + math.select(0, 1, solveActive.w);
                int coarseLanes = math.select(0, 1, useCoarse.x & solveActive.x) + math.select(0, 1, useCoarse.y & solveActive.y) + math.select(0, 1, useCoarse.z & solveActive.z) + math.select(0, 1, useCoarse.w & solveActive.w);
                int nonFinite = math.select(0, 1, !math.isfinite(height.x) & solveActive.x) + math.select(0, 1, !math.isfinite(height.y) & solveActive.y) + math.select(0, 1, !math.isfinite(height.z) & solveActive.z) + math.select(0, 1, !math.isfinite(height.w) & solveActive.w);
                int staleOrigin = math.select(0, 1, laneActive.x & !shiftMatch.x) + math.select(0, 1, laneActive.y & !shiftMatch.y) + math.select(0, 1, laneActive.z & !shiftMatch.z) + math.select(0, 1, laneActive.w & !shiftMatch.w);
                Interlocked.Add(ref counterPtr[0].Value, activeLanes);
                if (counterLength > 1)
                    Interlocked.Add(ref counterPtr[1].Value, coarseLanes);
                if (counterLength > 2)
                    Interlocked.Add(ref counterPtr[2].Value, nonFinite);
                if (counterLength > 3)
                    Interlocked.Add(ref counterPtr[3].Value, staleOrigin);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool4 ResolveCoarseMask(
            in OceanSampleRequestDTO r0,
            in OceanSampleRequestDTO r1,
            in OceanSampleRequestDTO r2,
            in OceanSampleRequestDTO r3,
            bool4 laneActive)
        {
            bool gridReady = MacroGrid.IsCreated && MacroGrid.Length > 0 && Tuning.MacroGridResolution > 1;
            byte threshold = (byte)math.clamp((int)math.round(math.max(0f, Tuning.CoarsePriorityThreshold)), 0, 255);
            return new bool4(
                gridReady & laneActive.x & r0.Priority <= threshold,
                gridReady & laneActive.y & r1.Priority <= threshold,
                gridReady & laneActive.z & r2.Priority <= threshold,
                gridReady & laneActive.w & r3.Priority <= threshold);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 LocalizeAupXZ(double3 aup, in GerstnerWaveTuningDTO tuning)
        {
            double dx = aup.x - tuning.LocalOriginAUP.x;
            double dz = aup.z - tuning.LocalOriginAUP.z;
            float2 local = new float2((float)dx, (float)dz);
            return math.select(float2.zero, local, math.isfinite(local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 WrapPhase(float4 phase)
        {
            float4 safe = math.select(new float4(0f), phase, math.isfinite(phase));
            return safe - math.floor((safe + new float4(math.PI)) * AnalyticalGerstnerWaveConstants.InvTwoPi) * AnalyticalGerstnerWaveConstants.TwoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float4 SampleMacroHeight4(float2 l0, float2 l1, float2 l2, float2 l3)
        {
            return new float4(SampleMacroHeight(l0), SampleMacroHeight(l1), SampleMacroHeight(l2), SampleMacroHeight(l3));
        }

        private float SampleMacroHeight(float2 local)
        {
            if (!MacroGrid.IsCreated || MacroGrid.Length <= 0)
                return Tuning.SeaLevelY;

            int resolution = math.clamp(Tuning.MacroGridResolution <= 0 ? 2 : Tuning.MacroGridResolution, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
            int cellCount = resolution * resolution;
            if (MacroGrid.Length < cellCount)
                return Tuning.SeaLevelY;

            float cellSize = math.max(0.25f, Tuning.MacroGridCellSizeMeters);
            float gx = (local.x - Tuning.MacroGridOriginX) * math.rcp(cellSize) + resolution * 0.5f;
            float gz = (local.y - Tuning.MacroGridOriginZ) * math.rcp(cellSize) + resolution * 0.5f;
            int x0 = math.clamp((int)math.floor(gx), 0, resolution - 1);
            int z0 = math.clamp((int)math.floor(gz), 0, resolution - 1);
            int x1 = math.min(x0 + 1, resolution - 1);
            int z1 = math.min(z0 + 1, resolution - 1);
            float tx = math.saturate(gx - x0);
            float tz = math.saturate(gz - z0);
            float h00 = MacroGrid[z0 * resolution + x0];
            float h10 = MacroGrid[z0 * resolution + x1];
            float h01 = MacroGrid[z1 * resolution + x0];
            float h11 = MacroGrid[z1 * resolution + x1];
            float h0 = math.lerp(h00, h10, tx);
            float h1 = math.lerp(h01, h11, tx);
            float height = Tuning.SeaLevelY + math.lerp(h0, h1, tz);
            return math.select(Tuning.SeaLevelY, height, math.isfinite(height));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreResult(
            OceanSampleResultDTO* resultPtr,
            int index,
            in OceanSampleRequestDTO request,
            float height,
            float slopeX,
            float slopeZ,
            float dispX,
            float dispZ,
            bool coarse,
            bool laneActive)
        {
            if (!laneActive)
                return;

            float safeHeight = math.select(0f, height, math.isfinite(height));
            float3 normal = ResolveNormal(new float3(slopeX, 1f, slopeZ));
            float3 displacement = new float3(dispX, safeHeight, dispZ);
            OceanSampleResultDTO result = default;
            result.SampleAUP = request.SampleAUP;
            result.WaterHeight = safeHeight;
            result.SurfaceNormal = normal;
            result.Displacement = math.select(float3.zero, displacement, math.isfinite(displacement));
            result.EntityHashID = request.EntityHashID;
            result.Flags = AnalyticalGerstnerWaveConstants.FlagActive |
                           AnalyticalGerstnerWaveConstants.FlagAnalytical |
                           AnalyticalGerstnerWaveConstants.FlagDearLie |
                           math.select(0u, AnalyticalGerstnerWaveConstants.FlagCoarseGrid, coarse) |
                           math.select(0u, AnalyticalGerstnerWaveConstants.FlagNonFinite, !math.isfinite(height));
            result.OriginShiftSequence = request.ShiftFrameID;
            resultPtr[index] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreStaleResult(
            OceanSampleResultDTO* resultPtr,
            int index,
            in OceanSampleRequestDTO request,
            bool stale)
        {
            if (!stale)
                return;

            OceanSampleResultDTO result = default;
            result.SampleAUP = request.SampleAUP;
            result.WaterHeight = 0f;
            result.SurfaceNormal = new float3(0f, 1f, 0f);
            result.Displacement = float3.zero;
            result.EntityHashID = request.EntityHashID;
            result.Flags = AnalyticalGerstnerWaveConstants.FlagStaleOrigin;
            result.OriginShiftSequence = request.ShiftFrameID;
            resultPtr[index] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveNormal(float3 value)
        {
            bool finite = math.all(math.isfinite(value));
            float3 safe = math.select(new float3(0f, 1f, 0f), value, finite);
            float lengthSq = math.lengthsq(safe);
            return math.select(new float3(0f, 1f, 0f), safe * math.rsqrt(math.max(lengthSq, 0.000001f)), lengthSq > 0.000001f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RecordWaveMathTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<OceanSampleResultDTO> Results;
        [ReadOnly, NoAlias] public NativeArray<WaveMathCounterLane> Counters;
        [WriteOnly, NoAlias] public NativeArray<WaveMathTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public GerstnerWaveTuningDTO Tuning;
        public int SampleCount;
        public float BurstMicros;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            int count = math.min(math.max(0, SampleCount), Results.IsCreated ? Results.Length : 0);
            uint lastHash = 0u;
            float maxAbsHeight = 0f;
            uint flags = 0u;
            int resultWindow = math.min(count, 1024);
            for (int i = 0; i < resultWindow; i++)
            {
                OceanSampleResultDTO result = Results[i];
                lastHash = math.select(lastHash, result.EntityHashID, result.EntityHashID != 0u);
                maxAbsHeight = math.max(maxAbsHeight, math.abs(math.select(0f, result.WaterHeight, math.isfinite(result.WaterHeight))));
                flags |= result.Flags & AnalyticalGerstnerWaveConstants.FlagNonFinite;
            }

            int evaluated = Counters.IsCreated && Counters.Length > 0 ? Counters[0].Value : count;
            int coarse = Counters.IsCreated && Counters.Length > 1 ? Counters[1].Value : 0;
            int nonFinite = Counters.IsCreated && Counters.Length > 2 ? Counters[2].Value : 0;
            int staleOrigin = Counters.IsCreated && Counters.Length > 3 ? Counters[3].Value : 0;
            int cursor = math.max(0, TelemetryCursor[0]);
            int slot = cursor % TelemetryRing.Length;
            WaveMathTelemetryEntry entry = default;
            entry.FrameIndex = Tuning.FrameIndex;
            entry.EvaluatedCoordinates = math.max(0, evaluated);
            entry.ActiveOctaves = AnalyticalGerstnerWaveMath.ResolveActiveOctaves(in Tuning);
            entry.CoarseGridSamples = math.max(0, coarse);
            entry.BurstMicros = math.max(0f, math.select(0f, BurstMicros, math.isfinite(BurstMicros)));
            entry.GlobalQualityWeight = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight)));
            entry.Flags = flags |
                          math.select(0u, AnalyticalGerstnerWaveConstants.FlagNonFinite, nonFinite > 0) |
                          math.select(0u, AnalyticalGerstnerWaveConstants.FlagStaleOrigin, staleOrigin > 0);
            entry.NonFiniteCount = math.max(0, nonFinite);
            entry.LastEntityHashID = lastHash;
            entry.MaxAbsHeight = maxAbsHeight;
            entry.MacroGridResolution = math.max(0, Tuning.MacroGridResolution);
            entry.RequestCount = count;
            entry.KernelHash = AnalyticalGerstnerWaveConstants.KernelHash;
            entry.ProfileHash = Tuning.ProfileHash;
            entry.OriginShiftSequence = Tuning.OriginShiftSequence;
            TelemetryRing[slot] = entry;
            TelemetryCursor[0] = cursor >= int.MaxValue - 1 ? TelemetryRing.Length : cursor + 1;
        }
    }

    public static class AnalyticalGerstnerWaveMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveOctaves(in GerstnerWaveTuningDTO tuning)
        {
            return ResolveActiveOctaves(ResolveOctaveBudget(in tuning), in tuning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveOctaves(float octaveBudget, in GerstnerWaveTuningDTO tuning)
        {
            int total = math.clamp(tuning.TotalOctaves <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.TotalOctaves, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
            int maxLimit = math.clamp(tuning.MaxOctaveLimit <= 0 ? total : tuning.MaxOctaveLimit, 1, total);
            float budget = math.select(1f, octaveBudget, math.isfinite(octaveBudget));
            return math.clamp((int)math.ceil(budget), 1, maxLimit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOctaveBudget(in GerstnerWaveTuningDTO tuning)
        {
            float q = math.smoothstep(0f, 1f, ResolveQuality01(in tuning));
            int total = math.clamp(tuning.TotalOctaves <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.TotalOctaves, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
            int maxLimit = math.clamp(tuning.MaxOctaveLimit <= 0 ? total : tuning.MaxOctaveLimit, 1, total);
            return math.lerp(1f, maxLimit, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOctaveWeight(int octave, in GerstnerWaveTuningDTO tuning)
        {
            return ResolveOctaveWeight(octave, ResolveOctaveBudget(in tuning));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOctaveWeight(int octave, float octaveBudget)
        {
            float budget = math.select(1f, octaveBudget, math.isfinite(octaveBudget));
            float fade = math.saturate(budget - octave);
            return math.smoothstep(0f, 1f, fade);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQuality01(in GerstnerWaveTuningDTO tuning)
        {
            return math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveAmplitude(float steepness, float wavelength, in GerstnerWaveTuningDTO tuning)
        {
            float multiplier = math.max(0f, math.select(AnalyticalGerstnerWaveConstants.DefaultAmplitudeMultiplier, tuning.WaveAmplitudeMultiplier, math.isfinite(tuning.WaveAmplitudeMultiplier)));
            float storm = math.lerp(0.75f, 1.6f, math.saturate(math.select(0f, tuning.StormWeight01, math.isfinite(tuning.StormWeight01))));
            return math.max(0f, steepness * wavelength * multiplier * storm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDeepWaterPhaseVelocity(float speed, float waveNumber)
        {
            float safeSpeed = math.max(0f, math.select(0f, speed, math.isfinite(speed)));
            float safeWaveNumber = math.max(0.0001f, math.select(0.0001f, waveNumber, math.isfinite(waveNumber)));
            float phaseVelocitySq = 9.80665f * math.rcp(safeWaveNumber);
            phaseVelocitySq = math.max(0.0001f, math.select(0.0001f, phaseVelocitySq, math.isfinite(phaseVelocitySq)));
            return safeSpeed * phaseVelocitySq * math.rsqrt(math.max(phaseVelocitySq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ResolvePhaseTimeSeconds(in GerstnerWaveTuningDTO tuning)
        {
            double legacyTime = (double)tuning.TimeSeconds;
            legacyTime = math.select(0d, legacyTime, math.isfinite(legacyTime) && legacyTime > 0d);
            return math.select(legacyTime, tuning.PhaseTimeSeconds, math.isfinite(tuning.PhaseTimeSeconds) && tuning.PhaseTimeSeconds > 0d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTimePhaseModulo(float phaseVelocity, float waveNumber, in GerstnerWaveTuningDTO tuning)
        {
            double phaseTime = ResolvePhaseTimeSeconds(in tuning);
            double phase = (double)phaseVelocity * (double)waveNumber * phaseTime;
            phase = math.select(0d, phase, math.isfinite(phase));
            double wrapped = phase - math.floor((phase + AnalyticalGerstnerWaveConstants.PiDouble) * AnalyticalGerstnerWaveConstants.InvTwoPiDouble) * AnalyticalGerstnerWaveConstants.TwoPiDouble;
            float value = (float)wrapped;
            return math.select(0f, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 SinPolynomial(float4 angle, float quality)
        {
            float4 safe = math.select(new float4(0f), angle, math.isfinite(angle));
            float4 x = safe - math.floor((safe + new float4(math.PI)) * AnalyticalGerstnerWaveConstants.InvTwoPi) * AnalyticalGerstnerWaveConstants.TwoPi;
            x = math.select(x, new float4(math.PI) - x, x > new float4(1.57079632679f));
            x = math.select(x, new float4(-math.PI) - x, x < new float4(-1.57079632679f));
            float4 x2 = x * x;
            float4 x4 = x2 * x2;
            float4 cubic = x * (new float4(1f) - (x2 * 0.16666666667f));
            float4 seventh = x * (new float4(1f) - (x2 * 0.16666666667f) + (x4 * 0.00833333333f) - (x4 * x2 * 0.00019841269f));
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            float blend = q * q * (3f - (2f * q));
            return math.lerp(cubic, seventh, new float4(blend));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 CosPolynomial(float4 angle, float quality)
        {
            return SinPolynomial(angle + new float4(1.57079632679f), quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial(float angle, float quality)
        {
            return SinPolynomial(new float4(angle), quality).x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial(float angle, float quality)
        {
            return CosPolynomial(new float4(angle), quality).x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ReadWave(NativeArray<GerstnerWaveParamsDTO> spectrum, int octave)
        {
            if (!spectrum.IsCreated || spectrum.Length <= 0)
                return default;

            int row = math.clamp(octave >> 2, 0, spectrum.Length - 1);
            int lane = octave & 3;
            GerstnerWaveParamsDTO pack = spectrum[row];
            if (lane == 0) return pack.Wave1;
            if (lane == 1) return pack.Wave2;
            if (lane == 2) return pack.Wave3;
            return pack.Wave4;
        }

        public static void EvaluateScalar(
            float2 local,
            NativeArray<GerstnerWaveParamsDTO> spectrum,
            int activeOctaves,
            in GerstnerWaveTuningDTO tuning,
            out float height,
            out float3 normal,
            out float3 displacement)
        {
            height = 0f;
            float slopeX = 0f;
            float slopeZ = 0f;
            float dispX = 0f;
            float dispZ = 0f;
            float quality = ResolveQuality01(in tuning);
            float octaveBudget = ResolveOctaveBudget(in tuning);
            int count = math.clamp(activeOctaves, 0, AnalyticalGerstnerWaveConstants.MaxOctaves);
            for (int i = 0; i < count; i++)
            {
                float4 wave = ReadWave(spectrum, i);
                float angle = wave.x;
                float steepness = math.max(0f, math.select(0f, wave.y, math.isfinite(wave.y)));
                float wavelength = math.max(0.01f, math.select(1f, wave.z, math.isfinite(wave.z)));
                float speed = math.max(0.01f, math.select(1f, wave.w, math.isfinite(wave.w)));
                float2 direction = new float2(
                    CosPolynomial(angle, quality),
                    SinPolynomial(angle, quality));
                float waveNumber = AnalyticalGerstnerWaveConstants.TwoPi * math.rcp(wavelength);
                float amplitude = ResolveAmplitude(steepness, wavelength, in tuning) *
                    ResolveOctaveWeight(i, octaveBudget);
                float phaseVelocity = ResolveDeepWaterPhaseVelocity(speed, waveNumber);
                float timePhase = ResolveTimePhaseModulo(phaseVelocity, waveNumber, in tuning);
                float originProjectionMeters = ResolveOriginProjectionModulo(direction, wavelength, in tuning);
                float phase = WrapPhase((waveNumber * (math.dot(direction, local) + originProjectionMeters)) - timePhase + i * 0.173f);
                float sinPhase = SinPolynomial(phase, quality);
                float cosPhase = CosPolynomial(phase, quality);
                height += amplitude * cosPhase;
                float slope = amplitude * waveNumber;
                slopeX += direction.x * slope * sinPhase;
                slopeZ += direction.y * slope * sinPhase;
                dispX += steepness * amplitude * direction.x * cosPhase;
                dispZ += steepness * amplitude * direction.y * cosPhase;
            }

            height = math.select(0f, height, math.isfinite(height));
            float3 safeDisplacement = new float3(dispX, height, dispZ);
            displacement = math.select(float3.zero, safeDisplacement, math.isfinite(safeDisplacement));
            normal = ResolveNormal(new float3(slopeX, 1f, slopeZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOriginProjectionModulo(float2 direction, float wavelength, in GerstnerWaveTuningDTO tuning)
        {
            double safeWavelength = math.max(0.01d, (double)math.select(1f, wavelength, math.isfinite(wavelength)));
            double projection = tuning.LocalOriginAUP.x * (double)direction.x + tuning.LocalOriginAUP.z * (double)direction.y;
            double wrapped = projection - math.floor(projection / safeWavelength) * safeWavelength;
            double half = safeWavelength * 0.5d;
            wrapped = math.select(wrapped, wrapped - safeWavelength, wrapped > half);
            float value = (float)wrapped;
            return math.select(0f, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPhase(float phase)
        {
            float safe = math.select(0f, phase, math.isfinite(phase));
            return safe - math.floor((safe + math.PI) * AnalyticalGerstnerWaveConstants.InvTwoPi) * AnalyticalGerstnerWaveConstants.TwoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveNormal(float3 value)
        {
            bool finite = math.all(math.isfinite(value));
            float3 safe = math.select(new float3(0f, 1f, 0f), value, finite);
            float lengthSq = math.lengthsq(safe);
            return math.select(new float3(0f, 1f, 0f), safe * math.rsqrt(math.max(lengthSq, 0.000001f)), lengthSq > 0.000001f);
        }
    }
}
