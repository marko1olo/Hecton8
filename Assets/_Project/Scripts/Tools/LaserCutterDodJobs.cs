namespace Hecton8.Tools
{
    using System.Runtime.CompilerServices;
    using Hecton8.Core.Contracts;
    using Unity.Burst;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockCutterTriggersJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [NoAlias] public NativeArray<LaserCutRequestMetaDTO> RequestMetas;
        public double3 OriginAUP;
        public uint Frame;
        public uint ToolHashID;
        public uint ParentEntityID;
        public uint Seed;
        public float MaximumDistanceMeters;

        public void Execute(int index)
        {
            uint hash = Hash(index, Seed);
            float lane = ((int)(hash & 7u) - 3.5f) * 0.18f;
            float vertical = (((int)((hash >> 3) & 7u)) - 3.5f) * 0.11f;
            float phase = ((hash >> 8) & 1023u) * (1f / 1023f);
            float3 direction = SafeNormalize(new float3(lane, vertical, 1f), new float3(0f, 0f, 1f));
            double3 origin = OriginAUP + new double3(lane * 0.25f, vertical * 0.25f, (index & 3) * 0.04f);

            Requests[index] = new LaserCutRequestDTO
            {
                RayOriginAUP = origin,
                RayDirection = direction,
                CuttingPower = math.saturate(0.35f + phase * 0.65f),
                MaximumDistance = math.max(0.1f, MaximumDistanceMeters),
                ToolHashID = ToolHashID == 0u ? LaserCutterDodConstants.LaserCutterHash : ToolHashID,
                ParentEntityID = ParentEntityID
            };

            if (!RequestMetas.IsCreated || index >= RequestMetas.Length)
                return;

            RequestMetas[index] = new LaserCutRequestMetaDTO
            {
                Frame = Frame,
                Flags = LaserCutterDodConstants.RequestFlagValid | LaserCutterDodConstants.RequestFlagMock,
                RequestSequence = unchecked(Frame * 131u + (uint)index),
                CooldownUntilFrame = 0u,
                LastAppliedFrame = 0u,
                Reserved0 = 0u,
                StateHash = Mix(1469598103934665603UL, unchecked(Frame * 131u + (uint)index)),
                Reserved1 = 0UL,
                Reserved2 = 0UL,
                Reserved3 = 0UL,
                Reserved4 = 0UL
            };
        }

        private static uint Hash(int index, uint seed)
        {
            uint value = unchecked((uint)index * 747796405u + seed + 2891336453u);
            value = ((value >> (int)((value >> 28) + 4u)) ^ value) * 277803737u;
            return (value >> 22) ^ value;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ManageCutterCooldownJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [NoAlias] public NativeArray<LaserCutRequestMetaDTO> RequestMetas;
        [NoAlias] public NativeArray<LaserCutCooldownDTO> Cooldowns;
        public uint Frame;
        public uint CooldownFrames;

        public void Execute(int index)
        {
            LaserCutRequestDTO request = Requests[index];
            LaserCutRequestMetaDTO meta = RequestMetas[index];
            if ((meta.Flags & LaserCutterDodConstants.RequestFlagValid) == 0u)
                return;

            LaserCutCooldownDTO cooldown = Cooldowns[index];
            if (Frame < cooldown.CooldownUntilFrame)
            {
                meta.Flags |= LaserCutterDodConstants.RequestFlagSuppressedByCooldown;
                meta.CooldownUntilFrame = cooldown.CooldownUntilFrame;
                RequestMetas[index] = meta;
                return;
            }

            cooldown.ToolHashID = request.ToolHashID;
            cooldown.ParentEntityID = request.ParentEntityID;
            cooldown.LastAppliedFrame = Frame;
            cooldown.CooldownUntilFrame = unchecked(Frame + math.max(1u, CooldownFrames));
            cooldown.Accumulator01 = math.saturate(cooldown.Accumulator01 + request.CuttingPower);
            cooldown.Flags = meta.Flags;
            Cooldowns[index] = cooldown;
            meta.LastAppliedFrame = Frame;
            meta.CooldownUntilFrame = cooldown.CooldownUntilFrame;
            meta.StateHash = Mix(meta.StateHash == 0UL ? 1469598103934665603UL : meta.StateHash, cooldown.CooldownUntilFrame);
            RequestMetas[index] = meta;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCutterSdfProbeHitsJob : IJobParallelFor
    {
        private const float InvEncodedByteMax = 1f / 255f;
        private const float LengthEpsilonSq = 0.00000001f;

        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestMetaDTO> RequestMetas;
        [NoAlias] public NativeArray<VoxelSonarSdfRaycastHit> SdfHits;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
        public double3 PresentationOriginAUP;
        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public float StepMeters;
        public int MaxSteps;
        public int LayerMask;
        public int VoxelLayerMask;

        public void Execute(int index)
        {
            WriteMiss(index);
            LaserCutRequestDTO request = Requests[index];
            LaserCutRequestMetaDTO meta = RequestMetas[index];
            if ((meta.Flags & LaserCutterDodConstants.RequestFlagValid) == 0u ||
                (meta.Flags & LaserCutterDodConstants.RequestFlagSuppressedByCooldown) != 0u ||
                request.CuttingPower <= 0f ||
                request.MaximumDistance <= 0f ||
                !math.all(math.isfinite(request.RayOriginAUP)) ||
                !math.all(math.isfinite(request.RayDirection)) ||
                !IncludesAnyLayer(LayerMask, VoxelLayerMask) ||
                !SdfIsValid())
            {
                return;
            }

            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(request.RayOriginAUP, PresentationOriginAUP);
            float3 localOrigin = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
            float3 direction = SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f));
            float maxDistance = ResolveBoundedSdfDistance(math.max(0.01f, request.MaximumDistance), localOrigin);
            float requestedStep = math.max(0.025f, math.isfinite(StepMeters) ? StepMeters : 0.1f);
            int maxSteps = math.clamp(MaxSteps, 1, 128);
            float step = ResolveBoundedStep(maxDistance, requestedStep, maxSteps);
            float previousDensity = 0f;
            float previousDistance = 0f;
            float3 previousPosition = localOrigin;
            bool hasPrevious = false;

            for (int i = 0; i <= maxSteps; i++)
            {
                float distance = math.min(maxDistance, i * step);
                float3 position = localOrigin + direction * distance;
                if (!TrySampleSdf(position, out float density))
                    continue;

                bool nearSurface = math.abs(density) <= 0.0001f;
                bool crossedSurface =
                    hasPrevious &&
                    ((previousDensity < -0.0001f && density >= 0.0001f) ||
                     (previousDensity > 0.0001f && density <= -0.0001f));

                if (nearSurface || crossedSurface)
                {
                    float resolvedDistance = distance;
                    float3 resolvedPoint = position;
                    if (crossedSurface)
                    {
                        float previousAbsDensity = math.abs(previousDensity);
                        float currentAbsDensity = math.abs(density);
                        float t = math.saturate(previousAbsDensity * math.rcp(math.max(0.0001f, previousAbsDensity + currentAbsDensity)));
                        resolvedDistance = math.lerp(previousDistance, distance, t);
                        resolvedPoint = math.lerp(previousPosition, position, t);
                    }

                    float3 normal = ResolveSdfGradient(resolvedPoint);
                    if (math.dot(normal, direction) > 0f)
                        normal = -normal;

                    SdfHits[index] = new VoxelSonarSdfRaycastHit
                    {
                        Point = resolvedPoint,
                        Normal = normal,
                        Distance = math.max(0f, resolvedDistance),
                        Density = 0f,
                        Density01 = 0f,
                        SdfRange = SdfRange,
                        Version = 0,
                        Flags = VoxelSonarSdfRaycastHit.FlagHit
                    };
                    return;
                }

                previousDensity = density;
                previousDistance = distance;
                previousPosition = position;
                hasPrevious = true;
                if (distance >= maxDistance)
                    break;
            }
        }

        private void WriteMiss(int index)
        {
            if (SdfHits.IsCreated && index < SdfHits.Length)
                SdfHits[index] = default;
        }

        private float ResolveBoundedSdfDistance(float maxDistance, float3 localOrigin)
        {
            float3 safeCell = math.max(math.abs(CellSize), new float3(0.0001f));
            float3 gridSpan = safeCell * math.max((float3)(GridDimensions - new int3(1)), new float3(1f));
            float originDistanceSq = math.lengthsq(localOrigin - VolumeOrigin);
            float gridSpanSq = math.lengthsq(gridSpan);
            bool payloadFinite = math.isfinite(originDistanceSq) && math.isfinite(gridSpanSq);
            float payloadDistance = FastLengthFromSq(originDistanceSq) + FastLengthFromSq(gridSpanSq) + math.cmax(safeCell) * 2f;
            return payloadFinite && math.isfinite(payloadDistance) && payloadDistance > 0.01f
                ? math.min(maxDistance, payloadDistance)
                : maxDistance;
        }

        private static float ResolveBoundedStep(float maxDistance, float requestedStep, int maxSteps)
        {
            float capStep = maxDistance * math.rcp(math.max(1, maxSteps));
            return math.max(requestedStep, math.isfinite(capStep) ? capStep : requestedStep);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.select(0f, lengthSq, math.isfinite(lengthSq) && lengthSq > 0f);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, LengthEpsilonSq));
        }

        private bool SdfIsValid()
        {
            if (!EncodedSdf.IsCreated ||
                GridDimensions.x <= 1 ||
                GridDimensions.y <= 1 ||
                GridDimensions.z <= 1 ||
                !math.all(math.isfinite(VolumeOrigin)) ||
                !math.all(math.isfinite(CellSize)) ||
                !math.isfinite(SdfRange) ||
                SdfRange <= 0.0001f)
            {
                return false;
            }

            long expected = (long)GridDimensions.x * GridDimensions.y * GridDimensions.z;
            return expected > 0L &&
                   expected <= int.MaxValue &&
                   EncodedSdf.Length >= expected;
        }

        private bool TrySampleSdf(float3 worldPosition, out float density)
        {
            density = SdfRange;
            float3 safeCell = math.max(CellSize, new float3(0.0001f));
            float3 sample = (worldPosition - VolumeOrigin) * math.rcp(safeCell);
            if (!math.all(math.isfinite(sample)))
                return false;

            sample = math.clamp(
                sample,
                float3.zero,
                new float3(GridDimensions.x - 1.001f, GridDimensions.y - 1.001f, GridDimensions.z - 1.001f));
            int3 p0 = new int3((int)math.floor(sample.x), (int)math.floor(sample.y), (int)math.floor(sample.z));
            int3 p1 = math.min(p0 + 1, GridDimensions - 1);
            float3 t = sample - p0;

            float c000 = DecodeSdfAt(p0.x, p0.y, p0.z);
            float c100 = DecodeSdfAt(p1.x, p0.y, p0.z);
            float c010 = DecodeSdfAt(p0.x, p1.y, p0.z);
            float c110 = DecodeSdfAt(p1.x, p1.y, p0.z);
            float c001 = DecodeSdfAt(p0.x, p0.y, p1.z);
            float c101 = DecodeSdfAt(p1.x, p0.y, p1.z);
            float c011 = DecodeSdfAt(p0.x, p1.y, p1.z);
            float c111 = DecodeSdfAt(p1.x, p1.y, p1.z);
            float c00 = math.lerp(c000, c100, t.x);
            float c10 = math.lerp(c010, c110, t.x);
            float c01 = math.lerp(c001, c101, t.x);
            float c11 = math.lerp(c011, c111, t.x);
            density = math.lerp(math.lerp(c00, c10, t.y), math.lerp(c01, c11, t.y), t.z);
            return math.isfinite(density);
        }

        private float3 ResolveSdfGradient(float3 worldPosition)
        {
            float3 step = math.max(CellSize, new float3(0.0001f));
            TrySampleSdf(worldPosition + new float3(step.x, 0f, 0f), out float px);
            TrySampleSdf(worldPosition - new float3(step.x, 0f, 0f), out float nx);
            TrySampleSdf(worldPosition + new float3(0f, step.y, 0f), out float py);
            TrySampleSdf(worldPosition - new float3(0f, step.y, 0f), out float ny);
            TrySampleSdf(worldPosition + new float3(0f, 0f, step.z), out float pz);
            TrySampleSdf(worldPosition - new float3(0f, 0f, step.z), out float nz);
            return SafeNormalize(new float3(px - nx, py - ny, pz - nz), new float3(0f, 1f, 0f));
        }

        private float DecodeSdfAt(int x, int y, int z)
        {
            long indexLong = ((long)z * GridDimensions.y + y) * GridDimensions.x + x;
            if (indexLong < 0L || indexLong >= EncodedSdf.Length)
                return SdfRange;

            return ((EncodedSdf[(int)indexLong] * InvEncodedByteMax) * 2f - 1f) * SdfRange;
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateCutterProbeHitsJob : IJobParallelFor
    {
        private const float LengthEpsilonSq = 0.00000001f;

        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestMetaDTO> RequestMetas;
        [ReadOnly, NoAlias] public NativeArray<VoxelSonarSdfRaycastHit> ProbeHits;
        [WriteOnly, NoAlias] public NativeArray<LaserCutHitDTO> HitResults;
        [WriteOnly, NoAlias] public NativeArray<LaserCutBatteryDrainRequest> BatteryDrainRequests;
        [WriteOnly, NoAlias] public NativeArray<LaserCutGlowDecalRequestDTO> GlowDecalRequests;
        [WriteOnly, NoAlias] public NativeArray<LaserCutImpactVfxDTO> ImpactVfxRequests;
        // TelemetryRing is a separate Vault lane. The modulo index is unique while
        // scheduled count <= ring length, so parallel writes do not alias.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<LaserCutTelemetryEntry> TelemetryRing;
        public double3 PresentationOriginAUP;
        public uint TelemetryCursorBase;
        public float GlobalQualityWeight;
        public float Heat01;
        public float DentRadiusMinMeters;
        public float DentRadiusMaxMeters;
        public float GlowLifetimeSeconds;
        public float BatteryWattsAtPowerOne;
        public float SparkIntensityScale;
        public float LowSparkCount;
        public float UltraSparkCount;

        public void Execute(int index)
        {
            LaserCutRequestDTO request = Requests[index];
            LaserCutRequestMetaDTO meta = RequestMetas[index];
            VoxelSonarSdfRaycastHit hit = ProbeHits[index];
            bool requestValid = (meta.Flags & LaserCutterDodConstants.RequestFlagValid) != 0u &&
                                (meta.Flags & LaserCutterDodConstants.RequestFlagSuppressedByCooldown) == 0u;
            bool hasHit = requestValid && HasHit(in hit);
            float quality = SaturateFinite(GlobalQualityWeight, 1f);
            float qualityCurve = Smooth01(quality);
            float power = SaturateFinite(request.CuttingPower, 0f);
            float heat = SaturateFinite(Heat01, power);
            float radiusMin = math.max(0f, math.isfinite(DentRadiusMinMeters) ? DentRadiusMinMeters : 0.045f);
            float radiusMax = math.max(radiusMin, math.isfinite(DentRadiusMaxMeters) ? DentRadiusMaxMeters : 0.32f);
            float glowLifetime = math.max(0f, math.isfinite(GlowLifetimeSeconds) ? GlowLifetimeSeconds : 0.9f);
            float wattsAtPowerOne = math.max(0f, math.isfinite(BatteryWattsAtPowerOne) ? BatteryWattsAtPowerOne : 180f);
            float sparkScale = math.max(0f, math.isfinite(SparkIntensityScale) ? SparkIntensityScale : 1f);
            float lowSparkCount = math.max(0f, math.isfinite(LowSparkCount) ? LowSparkCount : LaserCutterDodConstants.LowSparkCount);
            float ultraSparkCount = math.max(lowSparkCount, math.isfinite(UltraSparkCount) ? UltraSparkCount : LaserCutterDodConstants.UltraSparkCount);
            int sparkCap = math.clamp((int)math.ceil(ultraSparkCount), 0, LaserCutterDodConstants.UltraSparkCount);
            float distance = hasHit ? math.max(0f, hit.Distance) : 0f;
            float3 hitPoint = hasHit ? hit.Point : float3.zero;
            float3 normal = hasHit ? SafeNormalize(hit.Normal, new float3(0f, 1f, 0f)) : new float3(0f, 1f, 0f);
            double3 hitAup = hasHit ? PresentationOriginAUP + new double3(hitPoint.x, hitPoint.y, hitPoint.z) : double3.zero;
            bool finite = IsFiniteRequest(in request) && (!hasHit || (math.all(math.isfinite(hitAup)) && math.all(math.isfinite(normal)) && math.isfinite(distance)));
            uint flags = hasHit ? LaserCutterDodConstants.ResultFlagHit : 0u;
            if (!finite)
                flags |= LaserCutterDodConstants.ResultFlagNonFinite;
            if (hasHit)
                flags |= LaserCutterDodConstants.ResultFlagShaderDentOnly |
                         LaserCutterDodConstants.ResultFlagGpuSparkOnly |
                         LaserCutterDodConstants.ResultFlagBatteryDrainQueued |
                         LaserCutterDodConstants.ResultFlagDecalQueued;

            const float authoritativeCarveCurve = 1f;
            float carve01 = hasHit ? EstimateSdfCarve01(in request, hitAup, distance, authoritativeCarveCurve, power) : 0f;
            float sparkCountFloat = math.lerp(lowSparkCount, ultraSparkCount, qualityCurve);
            uint sparkCount = hasHit ? (uint)math.clamp((int)math.round(sparkCountFloat * math.saturate(0.25f + power * 0.75f) * sparkScale), 0, sparkCap) : 0u;
            float batteryWatts = hasHit ? wattsAtPowerOne * power : 0f;
            int burstWorkEstimate = 8 + (int)(sparkCount >> 4) + (int)math.round(qualityCurve * 10f);
            uint burstWorkEstimateMicros = hasHit ? (uint)math.clamp(burstWorkEstimate, 0, 65535) : 2u;

            // One hash per row: the three consumers below key on the same (request, hitAup) pair, so
            // recomputing it per consumer was three FNV folds over the same six doubles every hit.
            uint materialHash = HashMaterial(in request, hitAup);

            HitResults[index] = new LaserCutHitDTO
            {
                HitAUP = hitAup,
                RayOriginAUP = request.RayOriginAUP,
                Normal = normal,
                DistanceMeters = distance,
                ColliderInstanceID = 0u,
                MaterialHash = materialHash,
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                CuttingPower = power,
                Heat01 = heat,
                Frame = meta.Frame,
                Flags = flags
            };

            // NO DEFORMATION ROW IS PRODUCED HERE, DELIBERATELY. This job used to fill a 64-row
            // deformation buffer with a real RadiusMeters/DentDepthMeters/Progress01 per hit, and the
            // runtime then bound that buffer as `out _` and dropped it: no consumer existed anywhere in
            // the project, so the DTO, its BufferID and its layout gate were removed with the write.
            // Neither deformation owner can legally take the row from here.
            // (1) HullIntegrityRuntime owns hull dents (its own HullDentDTO ring plus the GPU upload) and
            //     ingests through SignalBus<CombatDamageSignal>, which the cutter already publishes - but
            //     this cutter marches the voxel SDF only (LaserCutterDodRuntime CutterProbeLayerMask =
            //     VoxelCaveLayerMask | VoxelProxyLayerMask), so reporting its cut as a hull dent would
            //     tell VehicleSubOsCockpitRuntime - which gates HullDeformedSignal on finiteness alone -
            //     that the submarine took damage when the player cut a rock.
            // (2) VoxelDeltaProcessor owns voxel carves and publishes VoxelCarveEvent, but its only
            //     enqueue entry point takes a HectonVoxelVolume MonoBehaviour, which every existing carve
            //     producer holds as a serialized field. A static DOD runtime cannot obtain that without a
            //     scene search or Awake wiring, and no carve write contract exists in Hecton8.*.Contracts.
            // Until that contract exists, computing the row is pure waste, so it is not computed. Restore
            // the write only together with the consumer that reads it.

            BatteryDrainRequests[index] = new LaserCutBatteryDrainRequest
            {
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                Watts = batteryWatts,
                Seconds = hasHit ? 1f / 60f : 0f,
                Progress01 = carve01,
                Frame = meta.Frame,
                Flags = flags,
                Reserved0 = 0u
            };

            GlowDecalRequests[index] = new LaserCutGlowDecalRequestDTO
            {
                CenterAUP = hitAup,
                Normal = normal,
                RadiusMeters = hasHit ? math.lerp(math.max(0.01f, radiusMin * 1.75f), math.max(radiusMax, radiusMax * 1.5f), qualityCurve) : 0f,
                Glow01 = hasHit ? math.saturate(heat + power * 0.5f) : 0f,
                LifetimeSeconds = hasHit ? glowLifetime * math.lerp(0.45f, 1.85f, qualityCurve) : 0f,
                ToolHashID = request.ToolHashID,
                MaterialHash = materialHash,
                Frame = meta.Frame,
                Flags = flags
            };

            ImpactVfxRequests[index] = new LaserCutImpactVfxDTO
            {
                CenterAUP = hitAup,
                Normal = normal,
                Intensity01 = hasHit ? math.saturate(power * (0.6f + qualityCurve * 0.4f)) : 0f,
                SparkCount = sparkCount,
                Heat01 = heat,
                ToolHashID = request.ToolHashID,
                Frame = meta.Frame,
                Flags = flags,
                SpeciesHash = LaserCutterDodConstants.SparkSpeciesHash
            };

            WriteTelemetry(index, in request, in meta, hitAup, normal, distance, quality, heat, batteryWatts, sparkCount, burstWorkEstimateMicros, flags);
        }

        private void WriteTelemetry(int index, in LaserCutRequestDTO request, in LaserCutRequestMetaDTO meta, double3 hitAup, float3 normal, float distance, float quality, float heat, float batteryWatts, uint sparkCount, uint burstWorkEstimateMicros, uint flags)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            uint ringLength = (uint)TelemetryRing.Length;
            int ringIndex = (int)((TelemetryCursorBase + (uint)index) % ringLength);
            TelemetryRing[ringIndex] = new LaserCutTelemetryEntry
            {
                RayOriginAUP = request.RayOriginAUP,
                HitAUP = hitAup,
                RayDirection = request.RayDirection,
                DistanceMeters = distance,
                CuttingPower = math.saturate(request.CuttingPower),
                QualityWeight = quality,
                Frame = meta.Frame,
                RequestSequence = meta.RequestSequence,
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                ColliderInstanceID = 0u,
                Flags = flags,
                SparkCount = sparkCount,
                CooldownUntilFrame = meta.CooldownUntilFrame,
                LayoutMagic = LaserCutterDodConstants.LayoutMagic,
                Heat01 = heat,
                StateHash = HashState(in request, in meta, hitAup, normal, flags),
                BatteryWatts = batteryWatts,
                BurstWorkEstimateMicros = burstWorkEstimateMicros
            };
        }

        private static bool HasHit(in VoxelSonarSdfRaycastHit hit)
        {
            return (hit.Flags & VoxelSonarSdfRaycastHit.FlagHit) != 0u &&
                   (hit.Distance > 0f || math.lengthsq(hit.Normal) > 0.0001f);
        }

        private static bool IsFiniteRequest(in LaserCutRequestDTO request)
        {
            return math.all(math.isfinite(request.RayOriginAUP)) &&
                   math.all(math.isfinite(request.RayDirection)) &&
                   math.isfinite(request.CuttingPower) &&
                   math.isfinite(request.MaximumDistance);
        }

        private static float EstimateSdfCarve01(in LaserCutRequestDTO request, double3 hitAup, float distance, float qualityCurve, float power)
        {
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(hitAup, request.RayOriginAUP);
            float3 local = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
            float3 axis = SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f));
            float axial = math.dot(local, axis);
            float radial = FastLengthFromSq(math.lengthsq(local - axis * axial));
            float slab01 = math.saturate(1f - radial * math.lerp(12f, 4f, qualityCurve));
            float range01 = math.saturate(distance * math.rcp(math.max(0.01f, request.MaximumDistance)));
            return math.saturate((slab01 * 0.72f + (1f - range01) * 0.28f) * power);
        }

        private static uint HashMaterial(in LaserCutRequestDTO request, double3 hitAup)
        {
            uint hash = request.ToolHashID ^ 2166136261u;
            hash = (hash ^ request.ParentEntityID) * 16777619u;
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(hitAup, request.RayOriginAUP);
            hash = MixDoubleToUInt(hash, localDelta.x);
            hash = MixDoubleToUInt(hash, localDelta.y);
            hash = MixDoubleToUInt(hash, localDelta.z);
            return hash == 0u ? LaserCutterDodConstants.LaserCutterHash : hash;
        }

        private static ulong HashState(in LaserCutRequestDTO request, in LaserCutRequestMetaDTO meta, double3 hitAup, float3 normal, uint flags)
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, request.ToolHashID);
            hash = Mix(hash, request.ParentEntityID);
            hash = Mix(hash, meta.RequestSequence);
            hash = Mix(hash, meta.Frame);
            hash = MixDouble(hash, request.RayOriginAUP.x);
            hash = MixDouble(hash, request.RayOriginAUP.y);
            hash = MixDouble(hash, request.RayOriginAUP.z);
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(hitAup, request.RayOriginAUP);
            hash = MixDouble(hash, localDelta.x);
            hash = MixDouble(hash, localDelta.y);
            hash = MixDouble(hash, localDelta.z);
            hash = Mix(hash, (uint)math.asint(normal.x));
            hash = Mix(hash, (uint)math.asint(normal.y));
            hash = Mix(hash, (uint)math.asint(normal.z));
            hash = Mix(hash, flags);
            return hash;
        }

        private static uint MixDoubleToUInt(uint hash, double value)
        {
            ulong bits = unchecked((ulong)math.aslong(math.isfinite(value) ? value : 0d));
            hash = (hash ^ (uint)bits) * 16777619u;
            return (hash ^ (uint)(bits >> 32)) * 16777619u;
        }

        private static ulong MixDouble(ulong hash, double value)
        {
            ulong bits = unchecked((ulong)math.aslong(math.isfinite(value) ? value : 0d));
            hash = Mix(hash, (uint)bits);
            return Mix(hash, (uint)(bits >> 32));
        }

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.select(0f, lengthSq, math.isfinite(lengthSq) && lengthSq > 0f);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, LengthEpsilonSq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SaturateFinite(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 0f);
            return math.smoothstep(0f, 1f, t);
        }

    }
}
