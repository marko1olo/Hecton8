using System.Runtime.InteropServices;
using Hecton8.Animation.IK;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.Fauna
{
    /// <summary>
    /// Constants and bit flags for procedural predator bite IK.
    /// </summary>
    public static class ProceduralBiteIkConstants
    {
        public const int TargetCapacity = 1;
        public const int CurrentJawPoseCapacity = 1;
        public const int TelemetryCapacity = 300;
        public const int DefaultHeadBoneIndex = 0;
        public const int DefaultUpperJawBoneIndex = 1;
        public const int DefaultLowerJawBoneIndex = 2;
        public const int DefaultFirstTentacleBoneIndex = 3;
        public const int MaxTentacleBones = 4;
        public const float DefaultJawReachMeters = 10f;
        public const float DefaultJawOpenMeters = 0.8f;
        public const float MinLengthSq = 0.000001f;
        public const float InverseByteMax = 0.0039215689f;
        public const uint RuntimeFlagStrikeActive = 1u << 0;
        public const uint RuntimeFlagMaximumQuality = 1u << 2;
        public const uint RuntimeFlagVisualOverkill = 1u << 3;
        public const int ResultVisualOverkillWeightShift = 16;
        public const uint ResultVisualOverkillWeightMask = 0x00FF0000u;
        public const uint ResultFlagSolved = 1u << 0;
        public const uint ResultFlagContact = 1u << 1;
        public const uint ResultFlagMiss = 1u << 2;
        public const uint ResultFlagQualityWrap = 1u << 4;
        public const uint ResultFlagAudioJawSnap = 1u << 5;
        public const uint ResultFlagFeedback = 1u << 6;
        public const uint ResultFlagVisualOverkill = 1u << 7;
        public const uint ResultFlagInvalid = 1u << 31;

        public static uint PackVisualOverkillWeight(float weight01)
        {
            float weight = math.saturate(math.isfinite(weight01) ? weight01 : 0f);
            uint q8 = (uint)math.min(255, (int)math.round(weight * 255f));
            return q8 << ResultVisualOverkillWeightShift;
        }

        public static float DecodeVisualOverkillWeight01(uint flags)
        {
            return ((flags & ResultVisualOverkillWeightMask) >> ResultVisualOverkillWeightShift) * InverseByteMax;
        }
    }

    /// <summary>
    /// Vault-owned target packet for one procedural predator bite solve. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct JawIkTarget
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float3 RuntimeCenter;
        [FieldOffset(60)] public float3 Extents;
        [FieldOffset(72)] public float3 Forward;
        [FieldOffset(84)] public float3 Up;
        [FieldOffset(96)] public float3 Right;
        [FieldOffset(108)] public float MaxReachMeters;
        [FieldOffset(112)] public float CylinderRadiusMeters;
        [FieldOffset(116)] public float ContactPaddingMeters;
        [FieldOffset(120)] public uint TargetHash;
        [FieldOffset(124)] public uint Frame;
    }

    /// <summary>
    /// Vault-owned previous/current bite pose cache. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CurrentJawPos
    {
        [FieldOffset(0)] public float3 HeadPosition;
        [FieldOffset(16)] public quaternion HeadRotation;
        [FieldOffset(32)] public float3 JawTipPosition;
        [FieldOffset(44)] public float ContactDistanceMeters;
        [FieldOffset(48)] public float3 UpperMandiblePosition;
        [FieldOffset(60)] public float Reach01;
        [FieldOffset(64)] public float3 LowerMandiblePosition;
        [FieldOffset(76)] public float Blend01;
        [FieldOffset(80)] public float3 WrapAnchor0;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public float3 WrapAnchor1;
        [FieldOffset(108)] public uint TargetHash;
        [FieldOffset(112)] public uint Frame;
        [FieldOffset(116)] public float TargetDistanceMeters;
        [FieldOffset(120)] public float SystemStress01;
        [FieldOffset(124)] public uint StateHash;
    }

    /// <summary>
    /// Fixed black-box entry for the last 300 bite IK solves. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BiteIkSolveEvent
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public uint TargetHash;
        [FieldOffset(16)] public float3 JawTipPosition;
        [FieldOffset(28)] public float DistanceMeters;
        [FieldOffset(32)] public float3 ClosestPoint;
        [FieldOffset(44)] public float Reach01;
        [FieldOffset(48)] public float3 TargetLocalCenter;
        [FieldOffset(60)] public float SystemStress01;
        [FieldOffset(64)] public float3 HeadPosition;
        [FieldOffset(76)] public float ContactDistanceMeters;
        [FieldOffset(80)] public float3 WrapAnchor0;
        [FieldOffset(92)] public float Blend01;
        [FieldOffset(96)] public float3 WrapAnchor1;
        [FieldOffset(108)] public float VisualOverkillWeight01;
        [FieldOffset(112)] public float4 Padding1;
    }

    public struct TentacleWriteContext
    {
        public float3 RootWorld;
        public float3 Wrap0;
        public float3 Wrap1;
        public float3 Forward;
        public float3 Up;
        public float BodyRadius;
        public float SegmentLength;
        public float VisualOverkillWeight01;
        public int RequestedBoneCount;
    }

    /// <summary>
    /// Burst-only jaw and appendage bite solver. Inputs are AUP + bounds packets; outputs mutate the shared Leviathan bone SOA.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct ProceduralBiteJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<JawIkTarget> JawIkTargets;
        [NoAlias] public NativeArray<CurrentJawPos> CurrentJawPos;
        [NoAlias] public NativeArray<LeviathanBoneDTO> LeviathanBones;
        [NoAlias] public NativeArray<BiteIkSolveEvent> BiteIkSolveEvents;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public AbsoluteUniversePosition PredatorAup;
        public float3 PredatorPosition;
        public float3 PredatorForward;
        public float3 PredatorUp;
        public float3 PredatorRight;
        public float DeltaTime;
        public float BodyRadius;
        public float SegmentLength;
        public float JawReachMeters;
        public float JawOpenMeters;
        public float SystemStress01;
        public float VisualOverkillWeight01;
        public int TargetIndex;
        public int FrameIndex;
        public int HeadBoneIndex;
        public int UpperJawBoneIndex;
        public int LowerJawBoneIndex;
        public int FirstTentacleBoneIndex;
        public int TentacleBoneCount;
        public uint RuntimeFlags;

        public void Execute()
        {
            if (!JawIkTargets.IsCreated ||
                !CurrentJawPos.IsCreated ||
                !LeviathanBones.IsCreated ||
                JawIkTargets.Length <= 0 ||
                CurrentJawPos.Length <= 0 ||
                LeviathanBones.Length <= 0)
            {
                return;
            }

            int targetIndex = math.clamp(TargetIndex, 0, JawIkTargets.Length - 1);
            JawIkTarget target = JawIkTargets[targetIndex];
            float systemStress = math.saturate(math.select(0f, SystemStress01, math.isfinite(SystemStress01)));
            bool strikeActive = (RuntimeFlags & ProceduralBiteIkConstants.RuntimeFlagStrikeActive) != 0u && target.TargetHash != 0u;
            float visualOverkillWeight = math.saturate(math.select(0f, VisualOverkillWeight01, math.isfinite(VisualOverkillWeight01)));
            int wrapBoneCount = math.clamp(
                (int)math.round(visualOverkillWeight * ProceduralBiteIkConstants.MaxTentacleBones),
                0,
                ProceduralBiteIkConstants.MaxTentacleBones);

            float3 forward = NormalizeSafe(PredatorForward, new float3(0f, 0f, 1f));
            float3 up = NormalizeSafe(PredatorUp, new float3(0f, 1f, 0f));
            float3 right = NormalizeSafe(PredatorRight, NormalizeSafe(math.cross(forward, up), new float3(1f, 0f, 0f)));
            up = NormalizeSafe(math.cross(right, forward), up);
            float bodyRadius = SanitizePositive(BodyRadius, 1f, 0.01f);
            float segmentLength = SanitizePositive(SegmentLength, 2f, 0.05f);
            float jawReach = SanitizePositive(math.select(JawReachMeters, target.MaxReachMeters, target.MaxReachMeters > 0.01f), ProceduralBiteIkConstants.DefaultJawReachMeters, 0.1f);
            float jawOpen = SanitizePositive(JawOpenMeters, ProceduralBiteIkConstants.DefaultJawOpenMeters, 0f);
            CurrentJawPos previous = CurrentJawPos[0];
            float3 rootWorld = ResolveBonePosition(HeadBoneIndex, PredatorPosition);
            float3 targetDelta = ResolveAupDelta(in target.CenterAup, in PredatorAup, target.RuntimeCenter - PredatorPosition);
            float3 targetLocalCenter = WorldDeltaToLocal(targetDelta, right, up, forward);
            float3 targetRightLocal = NormalizeSafe(WorldDeltaToLocal(SanitizeFinite(target.Right, right), right, up, forward), new float3(1f, 0f, 0f));
            float3 targetUpLocal = NormalizeSafe(WorldDeltaToLocal(SanitizeFinite(target.Up, up), right, up, forward), new float3(0f, 1f, 0f));
            float3 targetForwardLocal = NormalizeSafe(WorldDeltaToLocal(SanitizeFinite(target.Forward, forward), right, up, forward), new float3(0f, 0f, 1f));
            OrthonormalizeTargetBasis(ref targetRightLocal, ref targetUpLocal, ref targetForwardLocal);
            float3 extents = math.max(SanitizeFinite(target.Extents, new float3(0.5f)), new float3(0.05f));
            float contactPadding = SanitizePositive(target.ContactPaddingMeters, 0.05f, 0f);
            uint resultFlags = ProceduralBiteIkConstants.PackVisualOverkillWeight(visualOverkillWeight);

            float3 desiredTipLocal;
            float3 closestLocal;
            float distanceMeters;
            float reach01;
            if (!strikeActive)
            {
                desiredTipLocal = new float3(0f, 0f, segmentLength);
                closestLocal = desiredTipLocal;
                distanceMeters = 0f;
                reach01 = 0f;
                resultFlags |= ProceduralBiteIkConstants.ResultFlagMiss;
            }
            else
            {
                closestLocal = ResolveClosestHullPointGradient(targetLocalCenter, extents, contactPadding, targetRightLocal, targetUpLocal, targetForwardLocal);
                float closestSq = math.lengthsq(closestLocal);
                distanceMeters = LengthFromSq(closestSq);
                reach01 = math.saturate(distanceMeters * math.rcp(jawReach));
                bool inReach = distanceMeters <= jawReach;
                desiredTipLocal = inReach
                    ? closestLocal
                    : NormalizeSafe(closestLocal, new float3(0f, 0f, 1f)) * jawReach;
                resultFlags |= inReach
                    ? ProceduralBiteIkConstants.ResultFlagSolved
                    : ProceduralBiteIkConstants.ResultFlagMiss;
            }

            if (strikeActive && (resultFlags & ProceduralBiteIkConstants.ResultFlagMiss) != 0u)
                desiredTipLocal = ApplySnapMissRecoveryLocal(desiredTipLocal, jawOpen, segmentLength);

            float blend = ResolveThreeFrameBlend(DeltaTime);
            float3 desiredTipWorld = LocalToWorldDelta(desiredTipLocal, right, up, forward) + PredatorPosition;
            float3 smoothedTip = SanitizeFinite(math.lerp(previous.JawTipPosition, desiredTipWorld, blend), desiredTipWorld);
            if (!math.all(math.isfinite(previous.JawTipPosition)) || math.lengthsq(previous.JawTipPosition) <= ProceduralBiteIkConstants.MinLengthSq)
                smoothedTip = desiredTipWorld;

            float3 aimWorld = NormalizeSafe(smoothedTip - rootWorld, forward);
            quaternion headRotation = quaternion.LookRotationSafe(aimWorld, up);
            if (math.all(math.isfinite(previous.HeadRotation.value)) && math.lengthsq(previous.HeadRotation.value) > ProceduralBiteIkConstants.MinLengthSq)
                headRotation = FastNlerp(previous.HeadRotation, headRotation, blend);

            WriteHeadBone(rootWorld, headRotation, bodyRadius, segmentLength);

            float3 upperWorld = rootWorld;
            float3 lowerWorld = rootWorld;
            float3 wrap0 = smoothedTip;
            float3 wrap1 = smoothedTip;
            SolveMandibles(rootWorld, smoothedTip, aimWorld, up, right, jawReach, jawOpen, bodyRadius, segmentLength, blend, previous, out upperWorld, out lowerWorld);

            if (strikeActive && wrapBoneCount > 0)
            {
                resultFlags |= ProceduralBiteIkConstants.ResultFlagQualityWrap;
                ResolveWrapAnchors(targetLocalCenter, extents, target.CylinderRadiusMeters, targetRightLocal, targetUpLocal, targetForwardLocal, right, up, forward, out wrap0, out wrap1);
                WriteTentacleBones(new TentacleWriteContext
                {
                    RootWorld = rootWorld,
                    Wrap0 = wrap0,
                    Wrap1 = wrap1,
                    Forward = aimWorld,
                    Up = up,
                    BodyRadius = bodyRadius,
                    SegmentLength = segmentLength,
                    VisualOverkillWeight01 = visualOverkillWeight,
                    RequestedBoneCount = wrapBoneCount
                });
            }

            float contactDistance = math.max(0f, distanceMeters - jawReach);
            if (strikeActive && distanceMeters <= jawReach + contactPadding)
            {
                resultFlags |= ProceduralBiteIkConstants.ResultFlagContact | ProceduralBiteIkConstants.ResultFlagFeedback;
                if (distanceMeters < 2f)
                    resultFlags |= ProceduralBiteIkConstants.ResultFlagAudioJawSnap;
            }
            else if (strikeActive && distanceMeters < 2f)
            {
                resultFlags |= ProceduralBiteIkConstants.ResultFlagAudioJawSnap;
            }

            if (!math.all(math.isfinite(smoothedTip)) || !math.all(math.isfinite(closestLocal)))
                resultFlags |= ProceduralBiteIkConstants.ResultFlagInvalid;

            CurrentJawPos pose = new CurrentJawPos
            {
                HeadPosition = SanitizeFinite(rootWorld, PredatorPosition),
                HeadRotation = headRotation,
                JawTipPosition = SanitizeFinite(smoothedTip, rootWorld + forward * segmentLength),
                ContactDistanceMeters = math.select(0f, contactDistance, math.isfinite(contactDistance)),
                UpperMandiblePosition = SanitizeFinite(upperWorld, rootWorld),
                Reach01 = reach01,
                LowerMandiblePosition = SanitizeFinite(lowerWorld, rootWorld),
                Blend01 = blend,
                WrapAnchor0 = SanitizeFinite(wrap0, smoothedTip),
                Flags = resultFlags,
                WrapAnchor1 = SanitizeFinite(wrap1, smoothedTip),
                TargetHash = target.TargetHash,
                Frame = (uint)math.max(0, FrameIndex),
                TargetDistanceMeters = math.select(0f, distanceMeters, math.isfinite(distanceMeters)),
                SystemStress01 = systemStress,
                StateHash = ComputeStateHash(smoothedTip, closestLocal, resultFlags, target.TargetHash)
            };
            CurrentJawPos[0] = pose;
            WriteTelemetry(in pose, targetLocalCenter, closestLocal);
        }

        private float3 ResolveClosestHullPointGradient(float3 center, float3 extents, float padding, float3 axisX, float3 axisY, float3 axisZ)
        {
            float3 vectorToRoot = -center;
            float3 localToRoot = new float3(
                math.dot(vectorToRoot, axisX),
                math.dot(vectorToRoot, axisY),
                math.dot(vectorToRoot, axisZ));
            float3 candidateLocal = math.clamp(localToRoot, -extents, extents);
            bool rootInside = math.all(localToRoot >= -extents) && math.all(localToRoot <= extents);
            if (rootInside)
            {
                float3 distanceToFace = extents - math.abs(localToRoot);
                if (distanceToFace.x <= distanceToFace.y && distanceToFace.x <= distanceToFace.z)
                    candidateLocal.x = math.select(extents.x, -extents.x, localToRoot.x < 0f);
                else if (distanceToFace.y <= distanceToFace.z)
                    candidateLocal.y = math.select(extents.y, -extents.y, localToRoot.y < 0f);
                else
                    candidateLocal.z = math.select(extents.z, -extents.z, localToRoot.z < 0f);
            }

            for (int i = 0; i < 3; i++)
            {
                float3 candidateWorld = center + axisX * candidateLocal.x + axisY * candidateLocal.y + axisZ * candidateLocal.z;
                float3 gradient = NormalizeSafe(candidateWorld, NormalizeSafe(center, new float3(0f, 0f, 1f)));
                float3 localGradient = new float3(
                    math.dot(gradient, axisX),
                    math.dot(gradient, axisY),
                    math.dot(gradient, axisZ));
                candidateLocal = math.clamp(candidateLocal - localGradient * (0.35f + padding * 0.25f), -extents, extents);
            }

            float3 resolvedCandidate = center + axisX * candidateLocal.x + axisY * candidateLocal.y + axisZ * candidateLocal.z;
            return SanitizeFinite(resolvedCandidate, center);
        }

        private float3 ApplySnapMissRecoveryLocal(float3 desiredTipLocal, float jawOpen, float segmentLength)
        {
            int localFrame = math.max(0, FrameIndex);
            float phase = ((localFrame & 7) + 1) * 0.125f;
            float triangle = 1f - math.abs(phase * 2f - 1f);
            float recoilMeters = math.max(0.05f, jawOpen * 0.35f) * triangle;
            float desiredLength = SanitizePositive(LengthFromSq(math.lengthsq(desiredTipLocal)), segmentLength, 0.1f);
            float reach = math.max(0.1f, desiredLength - recoilMeters);
            float3 aim = NormalizeSafe(desiredTipLocal, new float3(0f, 0f, 1f)) * reach;
            float3 visualRecoil = new float3(0f, -recoilMeters, math.max(0.1f, segmentLength * 0.75f));
            return SanitizeFinite(math.lerp(aim, visualRecoil, 0.35f), desiredTipLocal);
        }

        private void WriteHeadBone(float3 rootWorld, quaternion rotation, float bodyRadius, float segmentLength)
        {
            int index = math.clamp(HeadBoneIndex, 0, LeviathanBones.Length - 1);
            LeviathanBoneDTO dto = default;
            dto.LocalToWorld = float4x4.TRS(rootWorld, rotation, new float3(bodyRadius, bodyRadius, segmentLength));
            LeviathanBones[index] = dto;
        }

        private void SolveMandibles(
            float3 rootWorld,
            float3 tipWorld,
            float3 aimWorld,
            float3 up,
            float3 right,
            float jawReach,
            float jawOpen,
            float bodyRadius,
            float segmentLength,
            float blend,
            CurrentJawPos previous,
            out float3 upperWorld,
            out float3 lowerWorld)
        {
            float3 span = tipWorld - rootWorld;
            float spanSq = math.lengthsq(span);
            float distance = LengthFromSq(spanSq);
            float upperLength = math.max(0.05f, jawReach * 0.45f);
            float lowerLength = math.max(0.05f, jawReach * 0.55f);
            float invDenominator = math.rcp(math.max(0.0001f, 2f * upperLength * math.max(distance, 0.0001f)));
            float acosInput = math.clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) * invDenominator, -1f, 1f);
            float sinSq = math.max(0f, 1f - (acosInput * acosInput));
            float hingeOffset = sinSq * math.rsqrt(math.max(sinSq, ProceduralBiteIkConstants.MinLengthSq)) * jawOpen;
            float3 mid = rootWorld + aimWorld * (distance * 0.48f);
            upperWorld = SanitizeFinite(mid + up * hingeOffset + right * (jawOpen * 0.15f), rootWorld);
            lowerWorld = SanitizeFinite(mid - up * hingeOffset - right * (jawOpen * 0.15f), rootWorld);
            if (math.all(math.isfinite(previous.UpperMandiblePosition)) && math.lengthsq(previous.UpperMandiblePosition) > ProceduralBiteIkConstants.MinLengthSq)
                upperWorld = math.lerp(previous.UpperMandiblePosition, upperWorld, blend);
            if (math.all(math.isfinite(previous.LowerMandiblePosition)) && math.lengthsq(previous.LowerMandiblePosition) > ProceduralBiteIkConstants.MinLengthSq)
                lowerWorld = math.lerp(previous.LowerMandiblePosition, lowerWorld, blend);

            WriteBone(UpperJawBoneIndex, upperWorld, tipWorld - upperWorld, up, bodyRadius * 0.45f, segmentLength * 0.45f);
            WriteBone(LowerJawBoneIndex, lowerWorld, tipWorld - lowerWorld, up, bodyRadius * 0.45f, segmentLength * 0.45f);
        }

        private void ResolveWrapAnchors(
            float3 targetLocalCenter,
            float3 extents,
            float cylinderRadius,
            float3 targetRightLocal,
            float3 targetUpLocal,
            float3 targetForwardLocal,
            float3 right,
            float3 up,
            float3 forward,
            out float3 wrap0,
            out float3 wrap1)
        {
            float radius = SanitizePositive(cylinderRadius, math.max(extents.x, extents.z), 0.05f);
            float3 cylinderAxis = NormalizeSafe(targetForwardLocal, new float3(0f, 0f, 1f));
            float3 toPredator = -targetLocalCenter;
            float3 radialVector = toPredator - cylinderAxis * math.dot(toPredator, cylinderAxis);
            float3 radial = NormalizeSafe(radialVector, targetRightLocal);
            float3 tangent = NormalizeSafe(math.cross(cylinderAxis, radial), targetUpLocal);
            float3 local0 = targetLocalCenter + radial * radius + tangent * (radius * 0.75f);
            float3 local1 = targetLocalCenter + radial * radius - tangent * (radius * 0.75f);
            wrap0 = LocalToWorldDelta(local0, right, up, forward) + PredatorPosition;
            wrap1 = LocalToWorldDelta(local1, right, up, forward) + PredatorPosition;
        }

        private void WriteTentacleBones(in TentacleWriteContext ctx)
        {
            int maxCount = math.min(ProceduralBiteIkConstants.MaxTentacleBones, math.min(math.max(0, TentacleBoneCount), math.max(0, ctx.RequestedBoneCount)));
            if (maxCount <= 0)
                return;

            float visualWeight = math.saturate(math.select(0f, ctx.VisualOverkillWeight01, math.isfinite(ctx.VisualOverkillWeight01)));
            float radiusScale = math.lerp(0.18f, 0.35f, visualWeight);
            float lengthScale = math.lerp(0.32f, 0.5f, visualWeight);
            int first = math.max(0, FirstTentacleBoneIndex);
            for (int i = 0; i < maxCount; i++)
            {
                int boneIndex = first + i;
                if ((uint)boneIndex >= (uint)LeviathanBones.Length)
                    break;

                float t = (i + 1f) * math.rcp(maxCount + 1f);
                float3 target = (i & 1) == 0 ? ctx.Wrap0 : ctx.Wrap1;
                float3 position = math.lerp(ctx.RootWorld, target, t * visualWeight);
                WriteBone(boneIndex, position, target - position, ctx.Up, ctx.BodyRadius * radiusScale, ctx.SegmentLength * lengthScale);
            }
        }

        private void WriteBone(int index, float3 position, float3 direction, float3 up, float radius, float length)
        {
            if ((uint)index >= (uint)LeviathanBones.Length)
                return;

            float3 forward = NormalizeSafe(direction, new float3(0f, 0f, 1f));
            float safeRadius = SanitizePositive(radius, 0.35f, 0.01f);
            float safeLength = SanitizePositive(length, 0.5f, 0.05f);
            LeviathanBoneDTO dto = default;
            dto.LocalToWorld = float4x4.TRS(SanitizeFinite(position, float3.zero), quaternion.LookRotationSafe(forward, up), new float3(safeRadius, safeRadius, safeLength));
            LeviathanBones[index] = dto;
        }

        private float3 ResolveBonePosition(int index, float3 fallback)
        {
            if ((uint)index >= (uint)LeviathanBones.Length)
                return SanitizeFinite(fallback, float3.zero);

            float4 c3 = LeviathanBones[index].LocalToWorld.c3;
            return SanitizeFinite(new float3(c3.x, c3.y, c3.z), fallback);
        }

        private void WriteTelemetry(in CurrentJawPos pose, float3 targetLocalCenter, float3 closestLocal)
        {
            if (!BiteIkSolveEvents.IsCreated || !TelemetryCursor.IsCreated || BiteIkSolveEvents.Length <= 0 || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            int index = cursor % BiteIkSolveEvents.Length;
            if (index < 0)
                index += BiteIkSolveEvents.Length;

            BiteIkSolveEvents[index] = new BiteIkSolveEvent
            {
                FrameIndex = FrameIndex,
                Flags = pose.Flags,
                StateHash = pose.StateHash,
                TargetHash = pose.TargetHash,
                JawTipPosition = pose.JawTipPosition,
                DistanceMeters = pose.TargetDistanceMeters,
                ClosestPoint = LocalToWorldDelta(closestLocal, NormalizeSafe(PredatorRight, new float3(1f, 0f, 0f)), NormalizeSafe(PredatorUp, new float3(0f, 1f, 0f)), NormalizeSafe(PredatorForward, new float3(0f, 0f, 1f))) + PredatorPosition,
                Reach01 = pose.Reach01,
                TargetLocalCenter = SanitizeFinite(targetLocalCenter, float3.zero),
                SystemStress01 = pose.SystemStress01,
                HeadPosition = pose.HeadPosition,
                ContactDistanceMeters = pose.ContactDistanceMeters,
                WrapAnchor0 = pose.WrapAnchor0,
                Blend01 = pose.Blend01,
                WrapAnchor1 = pose.WrapAnchor1,
                VisualOverkillWeight01 = ProceduralBiteIkConstants.DecodeVisualOverkillWeight01(pose.Flags)
            };

            TelemetryCursor[0] = cursor == int.MaxValue ? BiteIkSolveEvents.Length : cursor + 1;
        }

        private float3 ResolveAupDelta(in AbsoluteUniversePosition target, in AbsoluteUniversePosition origin, float3 fallback)
        {
            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in target, in origin);
            if (!math.all(math.isfinite(delta)))
                return SanitizeFinite(fallback, float3.zero);

            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static float3 WorldDeltaToLocal(float3 delta, float3 right, float3 up, float3 forward)
        {
            return new float3(math.dot(delta, right), math.dot(delta, up), math.dot(delta, forward));
        }

        private static float3 LocalToWorldDelta(float3 local, float3 right, float3 up, float3 forward)
        {
            return right * local.x + up * local.y + forward * local.z;
        }

        private static float ResolveThreeFrameBlend(float deltaTime)
        {
            float safeDt = math.select(0.016666668f, math.clamp(deltaTime, 0.001f, 0.05f), math.isfinite(deltaTime) && deltaTime > 0f);
            return math.max(0.33333334f, math.saturate(safeDt * 20f));
        }

        private static quaternion FastNlerp(quaternion a, quaternion b, float t)
        {
            float4 av = a.value;
            float4 bv = b.value;
            bv = math.select(bv, -bv, math.dot(av, bv) < 0f);
            float4 value = math.lerp(av, bv, math.saturate(t));
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= ProceduralBiteIkConstants.MinLengthSq)
                return math.all(math.isfinite(b.value)) ? b : quaternion.identity;

            return new quaternion(value * math.rsqrt(lengthSq));
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= ProceduralBiteIkConstants.MinLengthSq)
                return SanitizeFinite(fallback, new float3(0f, 0f, 1f));

            return value * math.rsqrt(lengthSq);
        }

        private static float LengthFromSq(float lengthSq)
        {
            if (!math.isfinite(lengthSq) || lengthSq <= ProceduralBiteIkConstants.MinLengthSq)
                return 0f;

            return lengthSq * math.rsqrt(math.max(lengthSq, ProceduralBiteIkConstants.MinLengthSq));
        }

        private static void OrthonormalizeTargetBasis(ref float3 axisX, ref float3 axisY, ref float3 axisZ)
        {
            axisX = NormalizeSafe(axisX, new float3(1f, 0f, 0f));
            axisZ = NormalizeSafe(axisZ, new float3(0f, 0f, 1f));

            float3 yCandidate = axisY - axisX * math.dot(axisY, axisX);
            if (!math.all(math.isfinite(yCandidate)) ||
                math.lengthsq(yCandidate) <= ProceduralBiteIkConstants.MinLengthSq)
            {
                float3 zCandidate = axisZ - axisX * math.dot(axisZ, axisX);
                float3 zSafe = NormalizeSafe(zCandidate, ResolvePerpendicularFallback(axisX));
                yCandidate = math.cross(zSafe, axisX);
            }

            axisY = NormalizeSafe(yCandidate, ResolvePerpendicularFallback(axisX));
            axisZ = NormalizeSafe(math.cross(axisX, axisY), axisZ);
            axisY = NormalizeSafe(math.cross(axisZ, axisX), axisY);
        }

        private static float3 ResolvePerpendicularFallback(float3 axis)
        {
            float3 candidate = math.select(new float3(0f, 1f, 0f), new float3(1f, 0f, 0f), math.abs(axis.y) > 0.75f);
            return NormalizeSafe(candidate - axis * math.dot(candidate, axis), new float3(0f, 0f, 1f));
        }

        private static float SanitizePositive(float value, float fallback, float minValue)
        {
            return math.isfinite(value) ? math.max(value, minValue) : fallback;
        }

        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static uint ComputeStateHash(float3 tip, float3 closest, uint flags, uint targetHash)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)math.asint(tip.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(tip.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(tip.z)) * 16777619u;
            hash = (hash ^ (uint)math.asint(closest.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(closest.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(closest.z)) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            hash = (hash ^ targetHash) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }
}
