using System.Runtime.CompilerServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Lighting
{
    internal static unsafe class DynamicPointLightNativeAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T ReadOnlyRef<T>(NativeArray<T> array, int index) where T : unmanaged
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            return ref UnsafeUtility.AsRef<T>((byte*)ptr + index * UnsafeUtility.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T WriteRef<T>(NativeArray<T> array, int index) where T : unmanaged
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            return ref UnsafeUtility.AsRef<T>((byte*)ptr + index * UnsafeUtility.SizeOf<T>());
        }
    }

    /// <summary>
    /// Fills Vault-owned light source/state buffers with deterministic stress data.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockLightCullingDataJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DynamicPointLightSourceDTO> Sources;
        [NoAlias] public NativeArray<LightCullStateDTO> States;
        public DynamicPointLightCullingSettingsDTO Settings;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Sources.Length || (uint)index >= (uint)States.Length || index >= Count)
                return;

            uint hash = DynamicPointLightCullingMath.Hash32((uint)index + 0x51A151u);
            int x = index % 25;
            int y = (index / 25) % 10;
            int z = (index / 250) % 20;
            float jitterX = ((hash & 255u) * (1f / 255f) - 0.5f) * 1.6f;
            float jitterY = (((hash >> 8) & 255u) * (1f / 255f) - 0.5f) * 0.8f;
            float jitterZ = (((hash >> 16) & 255u) * (1f / 255f) - 0.5f) * 1.6f;
            float3 local = new float3(
                (x - 12) * 4.0f + jitterX,
                (y - 5) * 2.25f + jitterY,
                (z - 10) * 4.0f + jitterZ);

            float colorSeedR = 0.25f + ((hash & 31u) * (0.75f / 31f));
            float colorSeedG = 0.35f + (((hash >> 5) & 31u) * (0.55f / 31f));
            float colorSeedB = 0.55f + (((hash >> 10) & 31u) * (0.40f / 31f));
            float range = 5.5f + ((hash >> 15) & 31u) * (18.5f / 31f);
            float intensity = 0.12f + ((hash >> 20) & 31u) * (2.4f / 31f);
            float priority = 0.15f + ((hash >> 25) & 31u) * (0.85f / 31f);

            DynamicPointLightSourceDTO source = default;
            source.AUP = Settings.CameraAup + new double3(local.x, local.y, local.z);
            source.Color = new float3(colorSeedR, colorSeedG, colorSeedB);
            source.RangeMeters = range;
            source.BaseIntensity = intensity;
            source.Priority = priority;
            source.Direction = math.normalizesafe(new float3(jitterZ, -0.35f, -jitterX), new float3(0f, -1f, 0f));
            source.SpotCosine = ((hash & 3u) == 0u) ? 0.62f : -1f;
            source.LightHash = hash == 0u ? 1u : hash;
            source.Flags = DynamicPointLightCullingFlags.MockSource | (((hash & 3u) == 0u) ? DynamicPointLightCullingFlags.Spot : 0u);
            source.FadeDistanceSq = math.max(1f, range * range);
            source.ProfileHash = DynamicPointLightCullingMath.Hash32(hash ^ 0xC15E551u);
            source.ShadowPhase01 = (index & 3) * 0.25f;
            source.BounceWeight = 0.03f + (((hash >> 7) & 15u) * (0.17f / 15f));
            source.ThermalFadeBias = 0.25f + (((hash >> 11) & 15u) * (0.75f / 15f));
            DynamicPointLightNativeAccess.WriteRef(Sources, index) = source;

            LightCullStateDTO state = default;
            state.LightHash = source.LightHash;
            state.BaseIntensity = intensity;
            state.DistanceSq = math.lengthsq(local);
            state.ComputedIntensity = 0f;
            state.Flags = source.Flags;
            DynamicPointLightNativeAccess.WriteRef(States, index) = state;
        }
    }

    /// <summary>
    /// Evaluates mathematical visibility and importance for point/spot proxy lights.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateLightCullingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<DynamicPointLightSourceDTO> Sources;
        [ReadOnly, NoAlias] public NativeArray<float4> FrustumPlanes;
        [ReadOnly, NoAlias] public NativeArray<float> SdfSamples;
        [ReadOnly, NoAlias] public NativeArray<DynamicPointLightProfileRuleDTO> ProfileRules;
        [NoAlias] public NativeArray<LightCullStateDTO> States;
        [NoAlias] public NativeArray<uint> ImportanceKeys;
        [NoAlias] public NativeArray<int> ImportanceIndices;
        public DynamicPointLightCullingSettingsDTO Settings;
        public int ProfileRuleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Sources.Length ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)ImportanceKeys.Length ||
                (uint)index >= (uint)ImportanceIndices.Length)
                return;

            ref readonly DynamicPointLightSourceDTO source = ref DynamicPointLightNativeAccess.ReadOnlyRef(Sources, index);
            LightCullStateDTO state = default;
            state.LightHash = source.LightHash;
            state.BaseIntensity = math.max(0f, math.isfinite(source.BaseIntensity) ? source.BaseIntensity : 0f);
            state.Flags = source.Flags;

            float3 local = new float3(
                (float)(source.AUP.x - Settings.CameraAup.x),
                (float)(source.AUP.y - Settings.CameraAup.y),
                (float)(source.AUP.z - Settings.CameraAup.z));

            if (!math.all(math.isfinite(local)) ||
                !math.isfinite(source.RangeMeters) ||
                !math.isfinite(source.FadeDistanceSq))
            {
                state.Flags |= DynamicPointLightCullingFlags.NonFinite;
                state.DistanceSq = 0f;
                state.ComputedIntensity = 0f;
                DynamicPointLightNativeAccess.WriteRef(States, index) = state;
                ImportanceKeys[index] = uint.MaxValue;
                ImportanceIndices[index] = index;
                return;
            }

            float range = math.max(0.01f, math.min(Settings.MaxRangeMeters > 0f ? Settings.MaxRangeMeters : 4096f, source.RangeMeters));
            float distanceSq = math.lengthsq(local);
            state.DistanceSq = distanceSq;
            uint flags = state.Flags;
            DynamicPointLightProfileRuleDTO rule = ResolveProfileRule(source.ProfileHash);
            if (rule.ProfileHash != 0u)
                flags |= DynamicPointLightCullingFlags.ProfileOverridden;

            bool culled = !PassesFrustum(local, range);
            if (culled)
                flags |= DynamicPointLightCullingFlags.CulledByFrustum;

            float fadeDistanceSq = source.FadeDistanceSq > 0f ? source.FadeDistanceSq : Settings.BaseFadeDistanceSq;
            fadeDistanceSq *= math.max(0.01f, rule.FadeDistanceMultiplier) * math.max(0.01f, rule.FadeDistanceMultiplier);
            fadeDistanceSq = math.max(1f, fadeDistanceSq);
            float fade = ResolveSquaredDistanceFade(distanceSq, fadeDistanceSq);
            if (fade <= 0f)
            {
                flags |= DynamicPointLightCullingFlags.CulledByDistance;
                culled = true;
            }

            if (!culled && IsSdfBlocked(local, rule.SdfBias))
            {
                flags |= DynamicPointLightCullingFlags.CulledBySdf;
                culled = true;
                fade = 0f;
            }

            float quality = DynamicPointLightCullingMath.Sanitize01(Settings.GlobalQualityWeight, 1f);
            float thermal = DynamicPointLightCullingMath.Sanitize01(Settings.ThermalPressure01, 0f);
            float priority = math.max(0.0001f, (math.isfinite(source.Priority) ? source.Priority : 0.0001f) * math.max(0f, rule.PriorityMultiplier));
            float pressureBias = math.saturate(1f - thermal * math.saturate(Settings.ThermalFadeStrength + source.ThermalFadeBias * (1f - priority)));
            float qualityGain = math.lerp(0.28f, 1f + math.saturate(Settings.NearFieldOverkillBoost) * math.saturate(1f - distanceSq * 0.0009f), quality);
            float intensity = culled ? 0f : state.BaseIntensity * math.max(0f, rule.IntensityMultiplier) * fade * pressureBias * qualityGain;
            intensity = math.isfinite(intensity) ? math.max(0f, intensity) : 0f;

            if (intensity > math.max(0.000001f, Settings.SubmitIntensityEpsilon))
                flags |= DynamicPointLightCullingFlags.Active;

            state.ComputedIntensity = intensity;
            state.Flags = flags;
            DynamicPointLightNativeAccess.WriteRef(States, index) = state;

            ImportanceKeys[index] = intensity <= 0f
                ? uint.MaxValue
                : DynamicPointLightCullingMath.BuildImportanceKey(intensity, priority, distanceSq, Settings.ImportanceWeight);
            ImportanceIndices[index] = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PassesFrustum(float3 local, float radius)
        {
            int count = math.min(math.min(Settings.FrustumPlaneCount, FrustumPlanes.Length), 6);
            if (count <= 0)
                return true;

            for (int i = 0; i < count; i++)
            {
                float4 plane = FrustumPlanes[i];
                float side = math.dot(plane.xyz, local) + plane.w;
                if (side < -radius)
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSquaredDistanceFade(float distanceSq, float fadeDistanceSq)
        {
            if (distanceSq >= fadeDistanceSq)
                return 0f;

            float fadeStartSq = fadeDistanceSq * 0.81f;
            if (distanceSq <= fadeStartSq)
                return 1f;

            float denom = math.max(0.0001f, fadeDistanceSq - fadeStartSq);
            float t = math.saturate((fadeDistanceSq - distanceSq) * math.rcp(denom));
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DynamicPointLightProfileRuleDTO ResolveProfileRule(uint profileHash)
        {
            int count = math.min(ProfileRuleCount, ProfileRules.Length);
            if (profileHash == 0u || count <= 0)
                return DefaultProfileRule();

            for (int i = 0; i < count; i++)
            {
                DynamicPointLightProfileRuleDTO rule = ProfileRules[i];
                if (rule.ProfileHash == profileHash)
                    return SanitizeRule(rule);
            }

            return DefaultProfileRule();
        }

        private static DynamicPointLightProfileRuleDTO DefaultProfileRule()
        {
            DynamicPointLightProfileRuleDTO rule = default;
            rule.PriorityMultiplier = 1f;
            rule.FadeDistanceMultiplier = 1f;
            rule.IntensityMultiplier = 1f;
            return rule;
        }

        private static DynamicPointLightProfileRuleDTO SanitizeRule(DynamicPointLightProfileRuleDTO rule)
        {
            rule.PriorityMultiplier = math.isfinite(rule.PriorityMultiplier) ? math.max(0f, rule.PriorityMultiplier) : 1f;
            rule.FadeDistanceMultiplier = math.isfinite(rule.FadeDistanceMultiplier) ? math.max(0.01f, rule.FadeDistanceMultiplier) : 1f;
            rule.IntensityMultiplier = math.isfinite(rule.IntensityMultiplier) ? math.max(0f, rule.IntensityMultiplier) : 1f;
            rule.SdfBias = math.isfinite(rule.SdfBias) ? rule.SdfBias : 0f;
            return rule;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSdfBlocked(float3 lightLocal, float sdfBias)
        {
            int resolution = Settings.SdfGridResolution;
            int sampleCount = math.min(Settings.SdfSampleCount, SdfSamples.Length);
            if (resolution <= 1 || sampleCount <= 0)
                return false;

            float cellSize = math.max(0.01f, Settings.SdfCellSizeMeters);
            float threshold = Settings.SdfOcclusionThreshold + sdfBias;
            for (int step = 1; step <= 4; step++)
            {
                float t = step * 0.2f;
                double3 sampleAup = Settings.CameraAup + new double3(lightLocal.x * t, lightLocal.y * t, lightLocal.z * t);
                float3 grid = new float3(
                    (float)((sampleAup.x - Settings.SdfOriginAup.x) / cellSize),
                    (float)((sampleAup.y - Settings.SdfOriginAup.y) / cellSize),
                    (float)((sampleAup.z - Settings.SdfOriginAup.z) / cellSize));
                int3 coord = (int3)math.floor(grid + new float3(resolution * 0.5f));
                if (math.any(coord < 0) || math.any(coord >= resolution))
                    continue;

                int voxelIndex = coord.x + coord.y * resolution + coord.z * resolution * resolution;
                if ((uint)voxelIndex >= (uint)sampleCount)
                    continue;

                if (SdfSamples[voxelIndex] < threshold)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Generates deterministic signed-distance samples for isolated culling tests.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockLightSdfSamplesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float> Samples;
        public int Resolution;
        public float CellSizeMeters;

        public void Execute(int index)
        {
            int resolution = math.max(2, Resolution);
            if ((uint)index >= (uint)Samples.Length)
                return;

            int x = index % resolution;
            int y = (index / resolution) % resolution;
            int z = index / (resolution * resolution);
            float3 p = (new float3(x, y, z) - new float3(resolution * 0.5f)) * math.max(0.01f, CellSizeMeters);
            float caveWall = math.abs(p.x + 7.5f) - 0.9f;
            float ceiling = p.y + 9.0f;
            float radialBudgetSq = 18.0f * 18.0f - math.lengthsq(p.xz);
            float side = radialBudgetSq * math.rcp(18.0f);
            Samples[index] = math.min(math.min(caveWall, ceiling), side);
        }
    }

    /// <summary>
    /// Sorts importance keys ascending. Lower key means more important light.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SortLightImportanceJob : IJob
    {
        [NoAlias] public NativeArray<uint> Keys;
        [NoAlias] public NativeArray<int> Indices;
        [NoAlias] public NativeArray<uint> ScratchKeys;
        [NoAlias] public NativeArray<int> ScratchIndices;
        public int Count;

        public void Execute()
        {
            int count = math.min(Count, math.min(math.min(Keys.Length, Indices.Length), math.min(ScratchKeys.Length, ScratchIndices.Length)));
            if (count <= 1)
                return;

            for (int pass = 0; pass < 4; pass++)
            {
                int* buckets = stackalloc int[256];
                for (int i = 0; i < 256; i++)
                    buckets[i] = 0;

                int shift = pass << 3;
                for (int i = 0; i < count; i++)
                    buckets[(Keys[i] >> shift) & 255u]++;

                int sum = 0;
                for (int i = 0; i < 256; i++)
                {
                    int c = buckets[i];
                    buckets[i] = sum;
                    sum += c;
                }

                for (int i = 0; i < count; i++)
                {
                    uint key = Keys[i];
                    int bucket = (int)((key >> shift) & 255u);
                    int write = buckets[bucket]++;
                    ScratchKeys[write] = key;
                    ScratchIndices[write] = Indices[i];
                }

                for (int i = 0; i < count; i++)
                {
                    Keys[i] = ScratchKeys[i];
                    Indices[i] = ScratchIndices[i];
                }
            }
        }
    }

    /// <summary>
    /// Builds the bounded GPU payload and probe-bounce stream from sorted light states.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BuildLightGpuPayloadJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<DynamicPointLightSourceDTO> Sources;
        [NoAlias] public NativeArray<LightCullStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<int> SortedIndices;
        [NoAlias] public NativeArray<DynamicPointLightGpuDTO> GpuPayload;
        [NoAlias] public NativeArray<CustomDynamicProbeLightDTO> DynamicProbeLights;
        [NoAlias] public NativeArray<DynamicPointLightRuntimeCountersDTO> Counters;
        public DynamicPointLightCullingSettingsDTO Settings;
        public int Count;
        public int GpuCapacity;

        public void Execute()
        {
            int count = math.min(Count, math.min(Sources.Length, States.Length));
            int capacity = math.min(math.min(GpuCapacity, GpuPayload.Length), Settings.MaxActiveLights);
            int submitted = 0;
            int visible = 0;
            int culled = 0;
            float intensitySum = 0f;
            float maxDistanceSq = 0f;
            uint stateHash = 2166136261u;
            uint flags = Settings.Flags;
            uint firstHash = 0u;
            uint lastHash = 0u;

            for (int i = 0; i < count; i++)
            {
                ref readonly LightCullStateDTO state = ref DynamicPointLightNativeAccess.ReadOnlyRef(States, i);
                if ((state.Flags & DynamicPointLightCullingFlags.Active) != 0u)
                    visible++;
                else
                    culled++;

                if ((state.Flags & DynamicPointLightCullingFlags.NonFinite) != 0u)
                    flags |= DynamicPointLightCullingFlags.NonFinite;

                stateHash = DynamicPointLightCullingMath.FnvaByte(stateHash, (byte)state.LightHash);
                stateHash = DynamicPointLightCullingMath.FnvaByte(stateHash, (byte)(state.LightHash >> 8));
                stateHash = DynamicPointLightCullingMath.FnvaByte(stateHash, (byte)(state.Flags));
            }

            for (int slot = 0; slot < capacity; slot++)
            {
                int sourceIndex = slot < SortedIndices.Length ? SortedIndices[slot] : -1;
                if ((uint)sourceIndex >= (uint)count)
                    continue;

                ref LightCullStateDTO stateRef = ref DynamicPointLightNativeAccess.WriteRef(States, sourceIndex);
                LightCullStateDTO state = stateRef;
                if (state.ComputedIntensity <= math.max(0.000001f, Settings.SubmitIntensityEpsilon))
                    continue;

                ref readonly DynamicPointLightSourceDTO source = ref DynamicPointLightNativeAccess.ReadOnlyRef(Sources, sourceIndex);
                float3 local = new float3(
                    (float)(source.AUP.x - Settings.CameraAup.x),
                    (float)(source.AUP.y - Settings.CameraAup.y),
                    (float)(source.AUP.z - Settings.CameraAup.z));
                if (!math.all(math.isfinite(local)))
                {
                    flags |= DynamicPointLightCullingFlags.NonFinite;
                    continue;
                }

                int write = submitted;
                DynamicPointLightGpuDTO gpu = default;
                gpu.PositionRange = new float4(local, math.max(0.01f, source.RangeMeters));
                gpu.ColorIntensity = new float4(SanitizeColor(source.Color), state.ComputedIntensity);
                gpu.DirectionSpot = new float4(math.normalizesafe(source.Direction, new float3(0f, 0f, 1f)), source.SpotCosine);
                gpu.LightHash = source.LightHash;
                gpu.Flags = state.Flags | DynamicPointLightCullingFlags.Submitted;
                gpu.DistanceSq = state.DistanceSq;
                gpu.BounceIntensity = math.max(0f, state.ComputedIntensity * math.max(0f, source.BounceWeight) * math.max(0f, Settings.BounceGain));
                DynamicPointLightNativeAccess.WriteRef(GpuPayload, write) = gpu;
                state.Flags = gpu.Flags;
                stateRef = state;

                if (write < DynamicProbeLights.Length)
                {
                    CustomDynamicProbeLightDTO probeLight = default;
                    probeLight.AUP = source.AUP;
                    probeLight.Color = gpu.ColorIntensity.xyz;
                    probeLight.Intensity = gpu.BounceIntensity;
                    probeLight.RadiusMeters = math.max(0.01f, source.RangeMeters);
                    probeLight.Flags = gpu.Flags;
                    probeLight.Direction = gpu.DirectionSpot.xyz;
                    DynamicPointLightNativeAccess.WriteRef(DynamicProbeLights, write) = probeLight;
                }

                if (submitted == 0)
                    firstHash = source.LightHash;
                lastHash = source.LightHash;
                intensitySum += state.ComputedIntensity;
                maxDistanceSq = math.max(maxDistanceSq, state.DistanceSq);
                submitted++;
                if (submitted >= capacity)
                    break;
            }

            for (int i = submitted; i < GpuPayload.Length && i < GpuCapacity; i++)
                DynamicPointLightNativeAccess.WriteRef(GpuPayload, i) = default;
            for (int i = submitted; i < DynamicProbeLights.Length && i < GpuCapacity; i++)
                DynamicPointLightNativeAccess.WriteRef(DynamicProbeLights, i) = default;

            if (Counters.IsCreated && Counters.Length > 0)
            {
                DynamicPointLightRuntimeCountersDTO counters = default;
                counters.TotalLights = count;
                counters.VisibleLights = visible;
                counters.CulledLights = culled;
                counters.SubmittedLights = submitted;
                counters.AverageSubmittedIntensity = submitted > 0 ? intensitySum * math.rcp(submitted) : 0f;
                counters.MaxDistanceSq = maxDistanceSq;
                counters.Flags = flags | (submitted > 0 ? DynamicPointLightCullingFlags.GpuDirty | DynamicPointLightCullingFlags.ProbeBouncePublished : 0u);
                counters.StateHash = stateHash;
                counters.Frame = Settings.FrameIndex;
                counters.MaxActiveLights = Settings.MaxActiveLights;
                counters.QualityWeight = Settings.GlobalQualityWeight;
                counters.ThermalPressure01 = Settings.ThermalPressure01;
                counters.FirstSubmittedHash = firstHash;
                counters.LastSubmittedHash = lastHash;
                DynamicPointLightNativeAccess.WriteRef(Counters, 0) = counters;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeColor(float3 color)
        {
            return math.select(new float3(1f), math.max(new float3(0f), color), math.all(math.isfinite(color)));
        }
    }

    /// <summary>
    /// Allocation-free CSV parser for light profile rules. The caller owns file IO and byte scratch storage.
    /// </summary>
    #if UNITY_EDITOR
    public static class DynamicPointLightProfileCsvParser
    {
        public static int Parse(
            NativeArray<byte> bytes,
            int byteCount,
            NativeArray<DynamicPointLightProfileRuleDTO> rules,
            int maxRules,
            out int rowsRejected)
        {
            rowsRejected = 0;
            if (!bytes.IsCreated || !rules.IsCreated || byteCount <= 0 || maxRules <= 0)
                return 0;

            int limit = math.min(byteCount, bytes.Length);
            int capacity = math.min(maxRules, rules.Length);
            int rowStart = 0;
            int parsed = 0;
            for (int i = 0; i <= limit; i++)
            {
                bool end = i == limit || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!end)
                    continue;

                if (i > rowStart)
                {
                    if (parsed < capacity && TryParseLine(bytes, rowStart, i, out DynamicPointLightProfileRuleDTO rule))
                    {
                        rules[parsed] = rule;
                        parsed++;
                    }
                    else
                    {
                        rowsRejected++;
                    }
                }

                rowStart = i + 1;
                if (i < limit && bytes[i] == (byte)'\r' && i + 1 < limit && bytes[i + 1] == (byte)'\n')
                {
                    i++;
                    rowStart = i + 1;
                }
            }

            return parsed;
        }

        private static bool TryParseLine(NativeArray<byte> bytes, int start, int end, out DynamicPointLightProfileRuleDTO rule)
        {
            rule = default;
            int tokenStart = start;
            int column = 0;
            uint hash = 0u;
            float priority = 1f;
            float fade = 1f;
            float intensity = 1f;
            float sdfBias = 0f;
            uint flags = 0u;

            for (int i = start; i <= end; i++)
            {
                if (i != end && bytes[i] != (byte)',')
                    continue;

                int tokenEnd = TrimRight(bytes, tokenStart, i);
                int trimmedStart = TrimLeft(bytes, tokenStart, tokenEnd);
                switch (column)
                {
                    case 0:
                        hash = HashToken(bytes, trimmedStart, tokenEnd);
                        break;
                    case 1:
                        if (!TryParseFloat(bytes, trimmedStart, tokenEnd, out priority))
                            return false;
                        break;
                    case 2:
                        if (!TryParseFloat(bytes, trimmedStart, tokenEnd, out fade))
                            return false;
                        break;
                    case 3:
                        if (!TryParseFloat(bytes, trimmedStart, tokenEnd, out intensity))
                            return false;
                        break;
                    case 4:
                        if (!TryParseFloat(bytes, trimmedStart, tokenEnd, out sdfBias))
                            return false;
                        break;
                    case 5:
                        if (!TryParseUInt(bytes, trimmedStart, tokenEnd, out flags))
                            return false;
                        break;
                }

                column++;
                tokenStart = i + 1;
            }

            if (hash == 0u || column < 4)
                return false;

            rule.ProfileHash = hash;
            rule.PriorityMultiplier = math.max(0f, priority);
            rule.FadeDistanceMultiplier = math.max(0.01f, fade);
            rule.IntensityMultiplier = math.max(0f, intensity);
            rule.SdfBias = sdfBias;
            rule.Flags = flags;
            return true;
        }

        private static int TrimLeft(NativeArray<byte> bytes, int start, int end)
        {
            int i = start;
            while (i < end && IsWhitespace(bytes[i]))
                i++;
            return i;
        }

        private static int TrimRight(NativeArray<byte> bytes, int start, int end)
        {
            int i = end;
            while (i > start && IsWhitespace(bytes[i - 1]))
                i--;
            return i;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static uint HashToken(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash = DynamicPointLightCullingMath.FnvaByte(hash, value);
            }
            return hash;
        }

        private static bool TryParseUInt(NativeArray<byte> bytes, int start, int end, out uint value)
        {
            value = 0u;
            if (start >= end)
                return false;

            int i = start;
            int numberBase = 10;
            if (i + 1 < end && bytes[i] == (byte)'0' && (bytes[i + 1] == (byte)'x' || bytes[i + 1] == (byte)'X'))
            {
                numberBase = 16;
                i += 2;
            }

            for (; i < end; i++)
            {
                int digit = Digit(bytes[i]);
                if (digit < 0 || digit >= numberBase)
                    return false;
                value = value * (uint)numberBase + (uint)digit;
            }

            return true;
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            if (start >= end)
                return false;

            int i = start;
            float sign = 1f;
            if (bytes[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (bytes[i] == (byte)'+')
            {
                i++;
            }

            float integer = 0f;
            int digits = 0;
            while (i < end)
            {
                int d = Digit(bytes[i]);
                if (d < 0 || d > 9)
                    break;
                integer = integer * 10f + d;
                i++;
                digits++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < end && bytes[i] == (byte)'.')
            {
                i++;
                while (i < end)
                {
                    int d = Digit(bytes[i]);
                    if (d < 0 || d > 9)
                        return false;
                    scale *= 0.1f;
                    fraction += d * scale;
                    i++;
                    digits++;
                }
            }

            if (digits == 0 || i != end)
                return false;

            value = sign * (integer + fraction);
            return math.isfinite(value);
        }

        private static int Digit(byte value)
        {
            if (value >= (byte)'0' && value <= (byte)'9')
                return value - (byte)'0';
            if (value >= (byte)'a' && value <= (byte)'f')
                return 10 + value - (byte)'a';
            if (value >= (byte)'A' && value <= (byte)'F')
                return 10 + value - (byte)'A';
            return -1;
        }
    }
    #endif
}
