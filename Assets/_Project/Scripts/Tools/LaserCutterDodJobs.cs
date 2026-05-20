namespace Hecton8.Tools
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Hecton8.Core.Contracts;
    using Unity.Burst;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GenerateMockCutterTriggersJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
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
                ParentEntityID = ParentEntityID,
                Frame = Frame,
                Flags = LaserCutterDodConstants.RequestFlagValid | LaserCutterDodConstants.RequestFlagMock,
                RequestSequence = unchecked(Frame * 131u + (uint)index)
            };
        }

        private static uint Hash(int index, uint seed)
        {
            uint value = unchecked((uint)index * 747796405u + seed + 2891336453u);
            value = ((value >> (int)((value >> 28) + 4u)) ^ value) * 277803737u;
            return (value >> 22) ^ value;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct ManageCutterCooldownJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [NoAlias] public NativeArray<LaserCutCooldownDTO> Cooldowns;
        public uint Frame;
        public uint CooldownFrames;

        public void Execute(int index)
        {
            LaserCutRequestDTO request = Requests[index];
            if ((request.Flags & LaserCutterDodConstants.RequestFlagValid) == 0u)
                return;

            LaserCutCooldownDTO cooldown = Cooldowns[index];
            if (Frame < cooldown.CooldownUntilFrame)
            {
                request.Flags |= LaserCutterDodConstants.RequestFlagSuppressedByCooldown;
                Requests[index] = request;
                return;
            }

            cooldown.ToolHashID = request.ToolHashID;
            cooldown.ParentEntityID = request.ParentEntityID;
            cooldown.LastAppliedFrame = Frame;
            cooldown.CooldownUntilFrame = unchecked(Frame + math.max(1u, CooldownFrames));
            cooldown.Accumulator01 = math.saturate(cooldown.Accumulator01 + request.CuttingPower);
            cooldown.Flags = request.Flags;
            Cooldowns[index] = cooldown;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BuildCutterRaycastsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [NoAlias] public NativeArray<RaycastCommand> Commands;
        public double3 PresentationOriginAUP;
        public int LayerMask;
        public byte HitTriggers;

        public void Execute(int index)
        {
            LaserCutRequestDTO request = Requests[index];
            if ((request.Flags & LaserCutterDodConstants.RequestFlagValid) == 0u ||
                (request.Flags & LaserCutterDodConstants.RequestFlagSuppressedByCooldown) != 0u ||
                request.CuttingPower <= 0f ||
                request.MaximumDistance <= 0f ||
                !math.all(math.isfinite(request.RayOriginAUP)) ||
                !math.all(math.isfinite(request.RayDirection)))
            {
                WriteDisabled(index);
                return;
            }

            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(request.RayOriginAUP, PresentationOriginAUP);
            float3 localOrigin = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
            float3 direction = SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f));
            QueryParameters query = new QueryParameters(LayerMask, false, HitTriggers != 0 ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore, false);
            Commands[index] = new RaycastCommand(ToVector3(localOrigin), ToVector3(direction), query, math.max(0.01f, request.MaximumDistance));
        }

        private void WriteDisabled(int index)
        {
            QueryParameters query = new QueryParameters(0, false, QueryTriggerInteraction.Ignore, false);
            Commands[index] = new RaycastCommand(Vector3.zero, Vector3.forward, query, 0.0f);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct EvaluateCutterRaycastHitsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<LaserCutRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<RaycastHit> RaycastHits;
        [WriteOnly, NoAlias] public NativeArray<LaserCutHitDTO> HitResults;
        [WriteOnly, NoAlias] public NativeArray<LaserCutDeformationStateDTO> DeformationStates;
        [WriteOnly, NoAlias] public NativeArray<LaserCutBatteryDrainRequest> BatteryDrainRequests;
        [WriteOnly, NoAlias] public NativeArray<LaserCutGlowDecalRequestDTO> GlowDecalRequests;
        [WriteOnly, NoAlias] public NativeArray<LaserCutImpactVfxDTO> ImpactVfxRequests;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<LaserCutTelemetryEntry> TelemetryRing;
        public double3 PresentationOriginAUP;
        public uint TelemetryCursorBase;
        public float GlobalQualityWeight;
        public float Heat01;

        public void Execute(int index)
        {
            LaserCutRequestDTO request = Requests[index];
            RaycastHit hit = RaycastHits[index];
            bool requestValid = (request.Flags & LaserCutterDodConstants.RequestFlagValid) != 0u &&
                                (request.Flags & LaserCutterDodConstants.RequestFlagSuppressedByCooldown) == 0u;
            bool hasHit = requestValid && HasHit(in hit);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float heat = math.saturate(math.isfinite(Heat01) ? Heat01 : request.CuttingPower);
            float distance = hasHit ? math.max(0f, hit.distance) : 0f;
            float3 hitPoint = hasHit ? ToFloat3(hit.point) : float3.zero;
            float3 normal = hasHit ? SafeNormalize(ToFloat3(hit.normal), new float3(0f, 1f, 0f)) : new float3(0f, 1f, 0f);
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

            float carve01 = hasHit ? EstimateSdfCarve01(in request, hitAup, distance, quality) : 0f;
            float sparkCountFloat = math.lerp((float)LaserCutterDodConstants.LowSparkCount, (float)LaserCutterDodConstants.UltraSparkCount, quality);
            uint sparkCount = hasHit ? (uint)math.clamp((int)math.round(sparkCountFloat * math.saturate(0.25f + request.CuttingPower * 0.75f)), 0, LaserCutterDodConstants.UltraSparkCount) : 0u;

            HitResults[index] = new LaserCutHitDTO
            {
                HitAUP = hitAup,
                RayOriginAUP = request.RayOriginAUP,
                Normal = normal,
                DistanceMeters = distance,
                ColliderInstanceID = 0u,
                MaterialHash = HashMaterial(in request, hitAup),
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                CuttingPower = math.saturate(request.CuttingPower),
                Heat01 = heat,
                Frame = request.Frame,
                Flags = flags
            };

            DeformationStates[index] = new LaserCutDeformationStateDTO
            {
                CenterAUP = hitAup,
                Normal = normal,
                RadiusMeters = math.lerp(0.045f, 0.32f, math.saturate(request.CuttingPower * quality)),
                DentDepthMeters = hasHit ? math.lerp(0.002f, 0.028f, carve01) : 0f,
                Heat01 = heat,
                Progress01 = carve01,
                TargetHash = HashMaterial(in request, hitAup),
                Frame = request.Frame,
                Flags = flags
            };

            BatteryDrainRequests[index] = new LaserCutBatteryDrainRequest
            {
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                Watts = hasHit ? math.lerp(85f, 220f, math.saturate(request.CuttingPower)) : 0f,
                Seconds = hasHit ? 1f / 60f : 0f,
                Progress01 = carve01,
                Frame = request.Frame,
                Flags = flags,
                Reserved0 = 0u
            };

            GlowDecalRequests[index] = new LaserCutGlowDecalRequestDTO
            {
                CenterAUP = hitAup,
                Normal = normal,
                RadiusMeters = hasHit ? math.lerp(0.08f, 0.48f, quality) : 0f,
                Glow01 = hasHit ? math.saturate(heat + request.CuttingPower * 0.5f) : 0f,
                LifetimeSeconds = hasHit ? math.lerp(0.35f, 1.5f, quality) : 0f,
                ToolHashID = request.ToolHashID,
                MaterialHash = HashMaterial(in request, hitAup),
                Frame = request.Frame,
                Flags = flags
            };

            ImpactVfxRequests[index] = new LaserCutImpactVfxDTO
            {
                CenterAUP = hitAup,
                Normal = normal,
                Intensity01 = hasHit ? math.saturate(request.CuttingPower * (0.6f + quality * 0.4f)) : 0f,
                SparkCount = sparkCount,
                Heat01 = heat,
                ToolHashID = request.ToolHashID,
                Frame = request.Frame,
                Flags = flags,
                SpeciesHash = LaserCutterDodConstants.SparkSpeciesHash
            };

            WriteTelemetry(index, in request, hitAup, normal, distance, quality, heat, sparkCount, flags);
        }

        private void WriteTelemetry(int index, in LaserCutRequestDTO request, double3 hitAup, float3 normal, float distance, float quality, float heat, uint sparkCount, uint flags)
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
                Frame = request.Frame,
                RequestSequence = request.RequestSequence,
                ToolHashID = request.ToolHashID,
                ParentEntityID = request.ParentEntityID,
                ColliderInstanceID = 0u,
                Flags = flags,
                SparkCount = sparkCount,
                CooldownUntilFrame = 0u,
                LayoutMagic = LaserCutterDodConstants.LayoutMagic,
                Heat01 = heat,
                StateHash = HashState(in request, hitAup, normal, flags),
                Reserved0 = 0UL
            };
        }

        private static bool HasHit(in RaycastHit hit)
        {
            return hit.distance > 0f || math.lengthsq(ToFloat3(hit.normal)) > 0.0001f;
        }

        private static bool IsFiniteRequest(in LaserCutRequestDTO request)
        {
            return math.all(math.isfinite(request.RayOriginAUP)) &&
                   math.all(math.isfinite(request.RayDirection)) &&
                   math.isfinite(request.CuttingPower) &&
                   math.isfinite(request.MaximumDistance);
        }

        private static float EstimateSdfCarve01(in LaserCutRequestDTO request, double3 hitAup, float distance, float quality)
        {
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(hitAup, request.RayOriginAUP);
            float3 local = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
            float axial = math.dot(local, SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f)));
            float radial = math.length(local - request.RayDirection * axial);
            float slab01 = math.saturate(1f - radial * math.lerp(12f, 4f, quality));
            float range01 = math.saturate(distance * math.rcp(math.max(0.01f, request.MaximumDistance)));
            return math.saturate((slab01 * 0.72f + (1f - range01) * 0.28f) * request.CuttingPower);
        }

        private static uint HashMaterial(in LaserCutRequestDTO request, double3 hitAup)
        {
            uint hash = request.ToolHashID ^ 2166136261u;
            hash = (hash ^ (uint)math.asint((float)hitAup.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint((float)hitAup.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint((float)hitAup.z)) * 16777619u;
            return hash == 0u ? LaserCutterDodConstants.LaserCutterHash : hash;
        }

        private static ulong HashState(in LaserCutRequestDTO request, double3 hitAup, float3 normal, uint flags)
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, request.ToolHashID);
            hash = Mix(hash, request.ParentEntityID);
            hash = Mix(hash, request.RequestSequence);
            hash = Mix(hash, (uint)math.asint((float)hitAup.x));
            hash = Mix(hash, (uint)math.asint((float)hitAup.y));
            hash = Mix(hash, (uint)math.asint((float)hitAup.z));
            hash = Mix(hash, (uint)math.asint(normal.x));
            hash = Mix(hash, (uint)math.asint(normal.y));
            hash = Mix(hash, (uint)math.asint(normal.z));
            hash = Mix(hash, flags);
            return hash;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
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
        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }
    }
}
