using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BuildFoundationModulesFromSocketModulesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ConstructionSocketModuleDTO> SocketModules;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationModuleAupDTO> FoundationModules;
        public int ModuleCount;

        public void Execute(int index)
        {
            if (!SocketModules.IsCreated ||
                !FoundationModules.IsCreated ||
                (uint)index >= (uint)ModuleCount ||
                (uint)index >= (uint)SocketModules.Length ||
                (uint)index >= (uint)FoundationModules.Length)
            {
                return;
            }

            ConstructionSocketModuleDTO source = SocketModules[index];
            FoundationModuleAupDTO module;
            float3 rotatedCenter = math.rotate(source.Rotation, source.BoundsCenter);
            module.CenterAup = source.RootAup + new double3(rotatedCenter.x, rotatedCenter.y, rotatedCenter.z);
            module.Rotation = math.all(math.isfinite(source.Rotation.value)) ? source.Rotation : quaternion.identity;
            module.BoundsExtents = math.max(source.BoundsExtents, new float3(0.5f));
            module.GroundClearanceMeters = 0.05f;
            module.ModuleHash = source.ModuleHash;
            module.Flags = source.Flags | FoundationPylonFlags.Active | FoundationPylonFlags.PresentationOnly | FoundationPylonFlags.RollbackExcluded;
            if (!math.all(math.isfinite(module.CenterAup)) ||
                !math.all(math.isfinite(module.BoundsExtents)))
            {
                module.Flags |= FoundationPylonFlags.NonFinite;
                module.CenterAup = double3.zero;
                module.Rotation = quaternion.identity;
                module.BoundsExtents = new float3(0.5f);
            }

            FoundationModules[index] = module;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockSeafloorSDFJob : IJobParallelFor
    {
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Distances;
        public FoundationSdfConfigDTO Config;

        public void Execute(int index)
        {
            if (!Distances.IsCreated ||
                (uint)index >= (uint)Distances.Length ||
                Config.SizeX <= 1 ||
                Config.SizeY <= 1 ||
                Config.SizeZ <= 1)
            {
                return;
            }

            long slice64 = (long)Config.SizeX * Config.SizeY;
            long volume64 = slice64 * Config.SizeZ;
            if (slice64 <= 0L ||
                slice64 > int.MaxValue ||
                volume64 <= 0L ||
                (long)index >= volume64)
            {
                return;
            }

            int slice = (int)slice64;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / Config.SizeX;
            int x = rem - y * Config.SizeX;
            float centeredX = x - (Config.SizeX - 1) * 0.5f;
            float centeredZ = z - (Config.SizeZ - 1) * 0.5f;
            float terrainY = Config.MockBaseY + centeredX * Config.MockSlopeX + centeredZ * Config.MockSlopeZ;
            float signedDistance = (y - terrainY) * math.max(0.0001f, Config.VoxelSizeMeters);
            Distances[index] = signedDistance;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CalculateFoundationPylonsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<FoundationModuleAupDTO> Modules;
        [ReadOnly, NoAlias] public NativeArray<float> MockSdfDistances;
        [ReadOnly, NoAlias] public NativeArray<byte> EncodedVoxelSdfTexture3D;
        [ReadOnly, NoAlias] public NativeArray<FoundationRayOriginDTO> RayOrigins;
        [ReadOnly, NoAlias] public NativeArray<FoundationProfileRangeDTO> ProfileRanges;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<PylonMatrixDTO> PylonMatrices;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonSurfaceDTO> PylonSurfaces;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonFrameCounters> PerModuleCounters;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationDebugRayDTO> DebugRays;
        public FoundationSdfConfigDTO SdfConfig;
        public FoundationTuningDTO Tuning;
        public double3 CameraAup;
        public int ModuleCount;
        public int ProfileCount;
        public int RayOriginCount;
        public int UseEncodedByteSdf;

        public void Execute(int moduleIndex)
        {
            if (!Modules.IsCreated ||
                !PylonMatrices.IsCreated ||
                !PylonSurfaces.IsCreated ||
                (uint)moduleIndex >= (uint)ModuleCount ||
                (uint)moduleIndex >= (uint)Modules.Length)
            {
                return;
            }

            FoundationModuleAupDTO module = Modules[moduleIndex];
            FoundationPylonFrameCounters counters = default;
            counters.SlotCount = FoundationSnappingCalculatorRuntime.MaxRaysPerModule;
            int baseSlot = moduleIndex * FoundationSnappingCalculatorRuntime.MaxRaysPerModule;
            float rayBudget = FoundationSnappingCalculatorRuntime.ResolveRayBudget(Tuning);
            int raysPerModule = math.clamp((int)math.ceil(rayBudget), 1, FoundationSnappingCalculatorRuntime.MaxRaysPerModule);
            int maxSteps = FoundationSnappingCalculatorRuntime.ResolveMarchSteps(Tuning);
            float radius = FoundationSnappingCalculatorRuntime.ResolveRadius(Tuning);
            float flare = FoundationSnappingCalculatorRuntime.ResolveShaderFlare(Tuning);
            float maxLength = math.max(0.25f, Tuning.MaxPylonLengthMeters);
            float epsilon = math.max(0.001f, Tuning.SdfHitEpsilonMeters);
            float maxStep = math.max(epsilon, Tuning.MaxMarchStepMeters);
            float sdfInterpolationWeight = FoundationSnappingCalculatorRuntime.ResolveSdfInterpolationWeight(Tuning);
            float proxyBlend = 1f - sdfInterpolationWeight;

            if ((module.Flags & FoundationPylonFlags.NonFinite) != 0u ||
                !math.all(math.isfinite(module.CenterAup)) ||
                !math.all(math.isfinite(module.Rotation.value)) ||
                !math.all(math.isfinite(module.BoundsExtents)) ||
                !math.all(math.isfinite(CameraAup)))
            {
                counters.Flags |= FoundationPylonFlags.NonFinite;
                for (int ray = 0; ray < FoundationSnappingCalculatorRuntime.MaxRaysPerModule; ray++)
                    WriteInvisible(baseSlot + ray, module, ray, FoundationPylonFlags.NonFinite);
                WriteCounter(moduleIndex, counters);
                return;
            }

            for (int ray = 0; ray < FoundationSnappingCalculatorRuntime.MaxRaysPerModule; ray++)
            {
                int slot = baseSlot + ray;
                if ((uint)slot >= (uint)PylonMatrices.Length || (uint)slot >= (uint)PylonSurfaces.Length)
                    continue;

                float rayVisibility = math.saturate(rayBudget - ray);
                if (ray >= raysPerModule || rayVisibility <= 0.0001f)
                {
                    WriteInvisible(slot, module, ray, FoundationPylonFlags.PresentationOnly);
                    continue;
                }

                counters.RaysCast++;
                double3 topAup = ResolvePylonTopAup(module, ray);
                double3 rayStartAup = topAup + new double3(0d, math.max(0f, Tuning.RayStartYOffsetMeters), 0d);
                bool hit = false;
                bool outOfBounds = false;
                double3 hitAup = rayStartAup;
                float resolvedLength = 0f;
                float t = 0f;
                if (!TrySampleSdf(rayStartAup, sdfInterpolationWeight, out float firstDistance))
                {
                    outOfBounds = true;
                }
                else
                {
                    float startOffset = math.max(0f, Tuning.RayStartYOffsetMeters);
                    float proxyLength = math.clamp(firstDistance - startOffset, 0f, maxLength + epsilon);
                    double3 proxyHitAup = topAup + new double3(0d, -proxyLength, 0d);
                    bool proxyHit = proxyLength > epsilon && firstDistance <= maxLength + startOffset + epsilon;
                    if (sdfInterpolationWeight <= 0.0001f)
                    {
                        resolvedLength = proxyLength;
                        hitAup = proxyHit ? proxyHitAup : rayStartAup;
                        hit = proxyHit;
                    }
                    else
                    {
                        bool marchHit = false;
                        double3 marchHitAup = rayStartAup;
                        float marchLength = 0f;
                        float distance = firstDistance;
                        for (int step = 0; step < maxSteps && t <= maxLength + startOffset; step++)
                        {
                            double3 sampleAup = step == 0 ? rayStartAup : rayStartAup + new double3(0d, -t, 0d);
                            if (step > 0 && !TrySampleSdf(sampleAup, sdfInterpolationWeight, out distance))
                            {
                                outOfBounds = true;
                                break;
                            }

                            if (distance <= epsilon)
                            {
                                marchHit = true;
                                marchHitAup = sampleAup;
                                marchLength = (float)math.max(0d, topAup.y - marchHitAup.y);
                                break;
                            }

                            float march = math.clamp(math.abs(distance), epsilon * 0.5f, maxStep);
                            t += march;
                        }

                        if (marchHit && proxyHit)
                        {
                            resolvedLength = math.lerp(marchLength, proxyLength, proxyBlend);
                            double invProxyBlend = 1d - proxyBlend;
                            hitAup = new double3(
                                marchHitAup.x * invProxyBlend + proxyHitAup.x * proxyBlend,
                                marchHitAup.y * invProxyBlend + proxyHitAup.y * proxyBlend,
                                marchHitAup.z * invProxyBlend + proxyHitAup.z * proxyBlend);
                            hit = true;
                        }
                        else if (marchHit)
                        {
                            resolvedLength = marchLength;
                            hitAup = marchHitAup;
                            hit = true;
                        }
                        else if (proxyHit && proxyBlend > 0.0001f)
                        {
                            resolvedLength = proxyLength;
                            hitAup = proxyHitAup;
                            hit = true;
                        }
                    }
                }

                uint flags = FoundationPylonFlags.PresentationOnly | FoundationPylonFlags.RollbackExcluded;
                flags |= UseEncodedByteSdf != 0 ? FoundationPylonFlags.RealVoxelSdf : FoundationPylonFlags.MockSdfFallback;
                if (proxyBlend > 0.0001f)
                    flags |= FoundationPylonFlags.ApproximateSdf;
                if (outOfBounds)
                    flags |= FoundationPylonFlags.OutOfSdfBounds;

                bool nonFiniteLength = !math.isfinite(resolvedLength);
                if (!hit || resolvedLength > maxLength || nonFiniteLength || resolvedLength <= 0.0001f)
                {
                    if (nonFiniteLength)
                        flags |= FoundationPylonFlags.NonFinite;

                    if (resolvedLength > maxLength)
                    {
                        flags |= FoundationPylonFlags.ExtensionCulled;
                        counters.CulledCount++;
                    }

                    counters.Flags |= flags;
                    WriteInvisible(slot, module, ray, flags);
                    WriteDebugRay(slot, rayStartAup, hitAup, resolvedLength, module, ray, flags);
                    continue;
                }

                flags |= FoundationPylonFlags.Active | FoundationPylonFlags.HitSdf;
                float3 normal = sdfInterpolationWeight <= 0.0001f
                    ? new float3(0f, 1f, 0f)
                    : ResolveSdfNormal(hitAup, Tuning.GradientStepMeters, sdfInterpolationWeight);
                double3 centerAup = (topAup + hitAup) * 0.5d;
                double3 localDouble = centerAup - CameraAup;
                double3 hitLocalDouble = hitAup - CameraAup;
                if (!math.all(math.isfinite(localDouble)) ||
                    !math.all(math.isfinite(hitLocalDouble)) ||
                    math.any(math.abs(localDouble) > (double)float.MaxValue) ||
                    math.any(math.abs(hitLocalDouble) > (double)float.MaxValue))
                {
                    flags |= FoundationPylonFlags.NonFinite;
                    counters.Flags |= flags;
                    WriteInvisible(slot, module, ray, flags);
                    WriteDebugRay(slot, rayStartAup, hitAup, resolvedLength, module, ray, flags);
                    continue;
                }

                float3 localCenter = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
                float3 hitLocal = new float3((float)hitLocalDouble.x, (float)hitLocalDouble.y, (float)hitLocalDouble.z);
                float visibilityScale = math.smoothstep(0f, 1f, rayVisibility);
                float visibleRadius = math.max(0.0001f, radius * visibilityScale);
                float visibleDiameter = visibleRadius * 2f;
                float visibleFlare = flare * visibilityScale;
                PylonMatrixDTO matrix;
                matrix.LocalToWorld = float4x4.TRS(localCenter, quaternion.identity, new float3(visibleDiameter, resolvedLength, visibleDiameter));
                PylonMatrices[slot] = matrix;

                uint slotHash = FoundationSnappingCalculatorRuntime.FoldHash(
                    FoundationSnappingCalculatorRuntime.HashFloat3(localCenter),
                    math.asuint(resolvedLength));
                slotHash = FoundationSnappingCalculatorRuntime.FoldHash(slotHash, module.ModuleHash);
                slotHash = FoundationSnappingCalculatorRuntime.FoldHash(slotHash, (uint)ray);

                FoundationPylonSurfaceDTO surface;
                surface.SurfaceNormalFlare = new float4(normal, visibleFlare);
                surface.AxisRadius = new float4(0f, -1f, 0f, visibleRadius);
                surface.HitLocalLength = new float4(hitLocal, resolvedLength);
                surface.Flags = flags;
                surface.ModuleHash = module.ModuleHash;
                surface.RayIndex = (uint)ray;
                surface.ResultHash = slotHash;
                PylonSurfaces[slot] = surface;

                WriteDebugRay(slot, rayStartAup, hitAup, resolvedLength, module, ray, flags);
                counters.ActivePylonCount++;
                counters.HitCount++;
                counters.MaxResolvedLength = math.max(counters.MaxResolvedLength, resolvedLength);
                counters.ResultHash = FoundationSnappingCalculatorRuntime.FoldHash(counters.ResultHash == 0u ? 2166136261u : counters.ResultHash, slotHash);
                counters.Flags |= flags;
            }

            WriteCounter(moduleIndex, counters);
        }

        private double3 ResolvePylonTopAup(FoundationModuleAupDTO module, int ray)
        {
            float3 normalizedOffset = ResolveProfileOffset(module.ModuleHash, ray, out float radiusMultiplier);
            float3 safeExtents = math.max(module.BoundsExtents, new float3(0.25f));
            float3 local = new float3(
                normalizedOffset.x * safeExtents.x * radiusMultiplier,
                -safeExtents.y - math.max(0f, module.GroundClearanceMeters),
                normalizedOffset.z * safeExtents.z * radiusMultiplier);
            float3 rotated = math.rotate(module.Rotation, local);
            return module.CenterAup + new double3(rotated.x, rotated.y, rotated.z);
        }

        private float3 ResolveProfileOffset(uint moduleHash, int ray, out float radiusMultiplier)
        {
            radiusMultiplier = 0.72f;
            if (ProfileRanges.IsCreated &&
                RayOrigins.IsCreated &&
                ProfileCount > 0 &&
                RayOriginCount > 0)
            {
                int rangeLimit = math.min(ProfileCount, ProfileRanges.Length);
                for (int i = 0; i < rangeLimit; i++)
                {
                    FoundationProfileRangeDTO range = ProfileRanges[i];
                    if (range.ModuleHash != moduleHash)
                        continue;

                    int start = math.clamp(range.StartIndex, 0, RayOrigins.Length);
                    int end = math.min(RayOrigins.Length, start + math.max(0, range.Count));
                    for (int j = start; j < end && j < RayOriginCount; j++)
                    {
                        FoundationRayOriginDTO origin = RayOrigins[j];
                        if ((origin.Flags & FoundationPylonFlags.Active) == 0u)
                            continue;

                        if (origin.RayIndex != (uint)ray)
                            continue;

                        radiusMultiplier = math.max(0.001f, origin.RadiusMultiplier);
                        return origin.NormalizedOffset;
                    }
                }
            }

            float quality = FoundationSnappingCalculatorRuntime.SanitizeQuality(Tuning.GlobalQualityWeight);
            float cornerShift = math.smoothstep(0.65f, 1f, quality);
            switch (ray)
            {
                case 0:
                    radiusMultiplier = 0.72f;
                    return new float3(cornerShift, 0f, -cornerShift);
                case 1:
                    return new float3(1f, 0f, 1f);
                case 2:
                    return new float3(-1f, 0f, 1f);
                default:
                    return new float3(-1f, 0f, -1f);
            }
        }

        private bool TrySampleSdf(double3 sampleAup, float interpolationWeight, out float distance)
        {
            distance = math.max(1f, Tuning.MaxPylonLengthMeters);
            if (!math.all(math.isfinite(sampleAup)) ||
                !math.all(math.isfinite(SdfConfig.OriginAup)) ||
                SdfConfig.VoxelSizeMeters <= 0.0001f ||
                SdfConfig.SizeX <= 1 ||
                SdfConfig.SizeY <= 1 ||
                SdfConfig.SizeZ <= 1)
            {
                return false;
            }

            long sx = SdfConfig.SizeX;
            long sy = SdfConfig.SizeY;
            long sz = SdfConfig.SizeZ;
            if (sx > int.MaxValue / sy)
                return false;

            long slice = sx * sy;
            if (slice > int.MaxValue / sz)
                return false;

            long voxelCount = slice * sz;
            if (voxelCount <= 0L || voxelCount > int.MaxValue)
                return false;

            double3 local = (sampleAup - SdfConfig.OriginAup) / SdfConfig.VoxelSizeMeters;
            if (!math.all(math.isfinite(local)) ||
                local.x < 0d || local.y < 0d || local.z < 0d ||
                local.x > SdfConfig.SizeX - 1d ||
                local.y > SdfConfig.SizeY - 1d ||
                local.z > SdfConfig.SizeZ - 1d)
            {
                return false;
            }

            float3 sample = new float3((float)local.x, (float)local.y, (float)local.z);
            int3 baseIndex = new int3((int)math.floor(sample.x), (int)math.floor(sample.y), (int)math.floor(sample.z));
            int3 maxIndex = new int3(SdfConfig.SizeX - 1, SdfConfig.SizeY - 1, SdfConfig.SizeZ - 1);
            int3 nearestIndex = math.clamp(
                new int3((int)math.round(sample.x), (int)math.round(sample.y), (int)math.round(sample.z)),
                new int3(0),
                maxIndex);
            float nearest = ReadSdf(nearestIndex.x, nearestIndex.y, nearestIndex.z);
            float triWeight = math.saturate(math.isfinite(interpolationWeight) ? interpolationWeight : 1f);
            if (triWeight <= 0.0001f)
            {
                distance = nearest - SdfConfig.IsoSurface;
                return math.isfinite(distance);
            }

            int3 nextIndex = math.min(baseIndex + 1, new int3(SdfConfig.SizeX - 1, SdfConfig.SizeY - 1, SdfConfig.SizeZ - 1));
            float3 frac = sample - baseIndex;
            float d000 = ReadSdf(baseIndex.x, baseIndex.y, baseIndex.z);
            float d100 = ReadSdf(nextIndex.x, baseIndex.y, baseIndex.z);
            float d010 = ReadSdf(baseIndex.x, nextIndex.y, baseIndex.z);
            float d110 = ReadSdf(nextIndex.x, nextIndex.y, baseIndex.z);
            float d001 = ReadSdf(baseIndex.x, baseIndex.y, nextIndex.z);
            float d101 = ReadSdf(nextIndex.x, baseIndex.y, nextIndex.z);
            float d011 = ReadSdf(baseIndex.x, nextIndex.y, nextIndex.z);
            float d111 = ReadSdf(nextIndex.x, nextIndex.y, nextIndex.z);
            float d00 = math.lerp(d000, d100, frac.x);
            float d10 = math.lerp(d010, d110, frac.x);
            float d01 = math.lerp(d001, d101, frac.x);
            float d11 = math.lerp(d011, d111, frac.x);
            float d0 = math.lerp(d00, d10, frac.y);
            float d1 = math.lerp(d01, d11, frac.y);
            distance = math.lerp(nearest, math.lerp(d0, d1, frac.z), triWeight) - SdfConfig.IsoSurface;
            return math.isfinite(distance);
        }

        private float ReadSdf(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= SdfConfig.SizeX || y >= SdfConfig.SizeY || z >= SdfConfig.SizeZ)
                return SdfConfig.SdfRangeMeters;

            long yz = y + (long)SdfConfig.SizeY * z;
            if (yz < 0L || yz > int.MaxValue)
                return SdfConfig.SdfRangeMeters;

            long indexLong = x + (long)SdfConfig.SizeX * yz;
            if (indexLong < 0L || indexLong > int.MaxValue)
                return SdfConfig.SdfRangeMeters;

            int index = (int)indexLong;
            if (UseEncodedByteSdf != 0 &&
                EncodedVoxelSdfTexture3D.IsCreated &&
                (uint)index < (uint)EncodedVoxelSdfTexture3D.Length)
            {
                return ((EncodedVoxelSdfTexture3D[index] * 0.0039215686274509803f) * 2f - 1f) * SdfConfig.SdfRangeMeters;
            }

            if (MockSdfDistances.IsCreated && (uint)index < (uint)MockSdfDistances.Length)
                return MockSdfDistances[index];

            return SdfConfig.SdfRangeMeters;
        }

        private float3 ResolveSdfNormal(double3 hitAup, float gradientStep, float interpolationWeight)
        {
            if (interpolationWeight <= 0.0001f)
                return new float3(0f, 1f, 0f);

            float step = math.max(0.05f, math.isfinite(gradientStep) ? gradientStep : 0.35f);
            TrySampleSdf(hitAup + new double3(step, 0d, 0d), interpolationWeight, out float xp);
            TrySampleSdf(hitAup + new double3(-step, 0d, 0d), interpolationWeight, out float xm);
            TrySampleSdf(hitAup + new double3(0d, step, 0d), interpolationWeight, out float yp);
            TrySampleSdf(hitAup + new double3(0d, -step, 0d), interpolationWeight, out float ym);
            TrySampleSdf(hitAup + new double3(0d, 0d, step), interpolationWeight, out float zp);
            TrySampleSdf(hitAup + new double3(0d, 0d, -step), interpolationWeight, out float zm);
            float3 normal = new float3(xp - xm, yp - ym, zp - zm);
            float lenSq = math.lengthsq(normal);
            if (!math.isfinite(lenSq) || lenSq <= 0.000001f)
                return new float3(0f, 1f, 0f);
            return normal * math.rsqrt(math.max(lenSq, 0.000001f));
        }

        private void WriteInvisible(int slot, FoundationModuleAupDTO module, int ray, uint flags)
        {
            if ((uint)slot >= (uint)PylonMatrices.Length || (uint)slot >= (uint)PylonSurfaces.Length)
                return;

            PylonMatrixDTO matrix;
            matrix.LocalToWorld = float4x4.zero;
            PylonMatrices[slot] = matrix;

            FoundationPylonSurfaceDTO surface;
            surface.SurfaceNormalFlare = new float4(0f, 1f, 0f, 0f);
            surface.AxisRadius = new float4(0f, -1f, 0f, 0f);
            surface.HitLocalLength = float4.zero;
            surface.Flags = flags;
            surface.ModuleHash = module.ModuleHash;
            surface.RayIndex = (uint)ray;
            surface.ResultHash = FoundationSnappingCalculatorRuntime.FoldHash(module.ModuleHash, (uint)ray);
            PylonSurfaces[slot] = surface;
        }

        private void WriteDebugRay(int slot, double3 originAup, double3 hitAup, float length, FoundationModuleAupDTO module, int ray, uint flags)
        {
            if (!DebugRays.IsCreated || (uint)slot >= (uint)DebugRays.Length)
                return;

            FoundationDebugRayDTO debug;
            debug.OriginAup = originAup;
            debug.HitAup = hitAup;
            debug.LengthMeters = math.max(0f, math.isfinite(length) ? length : 0f);
            debug.Flags = flags;
            debug.ModuleHash = module.ModuleHash;
            debug.RayIndex = (uint)ray;
            DebugRays[slot] = debug;
        }

        private void WriteCounter(int moduleIndex, FoundationPylonFrameCounters counters)
        {
            if (!PerModuleCounters.IsCreated || (uint)moduleIndex >= (uint)PerModuleCounters.Length)
                return;

            PerModuleCounters[moduleIndex] = counters;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ReduceFoundationPylonCountersJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<FoundationPylonFrameCounters> PerModuleCounters;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonFrameCounters> FrameCounters;
        public int ModuleCount;

        public void Execute()
        {
            if (!PerModuleCounters.IsCreated ||
                !FrameCounters.IsCreated ||
                FrameCounters.Length <= 0)
            {
                return;
            }

            FoundationPylonFrameCounters aggregate = default;
            aggregate.ResultHash = 2166136261u;
            int count = math.min(math.max(0, ModuleCount), PerModuleCounters.Length);
            for (int i = 0; i < count; i++)
            {
                FoundationPylonFrameCounters source = PerModuleCounters[i];
                aggregate.ActivePylonCount += source.ActivePylonCount;
                aggregate.SlotCount += source.SlotCount;
                aggregate.RaysCast += source.RaysCast;
                aggregate.HitCount += source.HitCount;
                aggregate.CulledCount += source.CulledCount;
                aggregate.MaxResolvedLength = math.max(aggregate.MaxResolvedLength, source.MaxResolvedLength);
                aggregate.Flags |= source.Flags;
                aggregate.ResultHash = FoundationSnappingCalculatorRuntime.FoldHash(aggregate.ResultHash, source.ResultHash);
            }

            FrameCounters[0] = aggregate;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CompactFoundationPylonDrawListJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<PylonMatrixDTO> PylonMatrices;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonSurfaceDTO> PylonSurfaces;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonFrameCounters> FrameCounters;
        public int SlotCount;

        public void Execute()
        {
            if (!PylonMatrices.IsCreated ||
                !PylonSurfaces.IsCreated ||
                !FrameCounters.IsCreated ||
                FrameCounters.Length <= 0)
            {
                return;
            }

            int safeCount = math.min(
                math.max(0, SlotCount),
                math.min(PylonMatrices.Length, PylonSurfaces.Length));
            int write = 0;
            for (int i = 0; i < safeCount; i++)
            {
                FoundationPylonSurfaceDTO surface = PylonSurfaces[i];
                if ((surface.Flags & FoundationPylonFlags.Active) == 0u)
                    continue;

                if (write != i)
                {
                    PylonMatrices[write] = PylonMatrices[i];
                    PylonSurfaces[write] = surface;
                }

                write++;
            }

            FoundationPylonFrameCounters counters = FrameCounters[0];
            counters.ActivePylonCount = write;
            counters.SlotCount = write;
            FrameCounters[0] = counters;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BuildFoundationPylonIndirectArgsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<FoundationPylonFrameCounters> FrameCounters;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<FoundationPylonIndirectArgsDTO> Args;
        public int SlotCount;

        public void Execute()
        {
            if (!Args.IsCreated || Args.Length <= 0)
                return;

            uint instanceCount = (uint)math.max(0, SlotCount);
            if (FrameCounters.IsCreated && FrameCounters.Length > 0)
                instanceCount = (uint)math.max(0, FrameCounters[0].SlotCount);

            FoundationPylonIndirectArgsDTO args;
            args.VertexCountPerInstance = FoundationSnappingCalculatorRuntime.ProceduralPylonVertexCount;
            args.InstanceCount = instanceCount;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            Args[0] = args;
        }
    }
}
