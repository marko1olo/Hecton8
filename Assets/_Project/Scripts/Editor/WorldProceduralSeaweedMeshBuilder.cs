using System;
using System.Collections.Generic;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    internal static class WorldProceduralSeaweedMeshBuilder
    {
        private const float TwoPi = Mathf.PI * 2f;

        public static bool CanBuild(string rootToken)
        {
            return TryResolveSpec(rootToken, out _);
        }

        public static bool TryBuild(string rootToken, Vector3 scale, int lodLevel, out Mesh mesh)
        {
            mesh = null;
            VariantSpec spec;
            if (!TryResolveSpec(rootToken, out spec))
                return false;

            int lod = Mathf.Clamp(lodLevel, 0, 3);
            MeshBuffers buffers = new MeshBuffers(spec.EstimatedVertexCount);
            BuildHoldfast(buffers, spec, scale, lod);
            int clusterCount = Mathf.Max(1, spec.ClusterCount - (lod > 1 ? 1 : 0));
            int baseBladeCount = Mathf.Max(1, spec.BladeCount - lod);
            int baseBulbCount = Mathf.Max(0, spec.BulbCount - lod);
            int activeBladeCountPerCluster = clusterCount > 1
                ? Mathf.Max(5, Mathf.CeilToInt(baseBladeCount * 0.58f))
                : baseBladeCount;
            int activeBulbCountPerCluster = clusterCount > 1
                ? Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, baseBulbCount) * 0.45f))
                : baseBulbCount;

            for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
            {
                Vector3 clusterOffset = EvaluateClusterOffset(spec, scale, clusterIndex, clusterCount);
                float clusterYawOffset = EvaluateClusterYawOffset(spec, clusterIndex, clusterCount);
                float clusterScaleFactor = EvaluateClusterScaleFactor(spec, clusterIndex, clusterCount);
                Vector3 clusterScale = scale * clusterScaleFactor;
                BuildStipe(buffers, spec, clusterScale, lod, clusterOffset, clusterYawOffset);

                for (int bladeIndex = 0; bladeIndex < activeBladeCountPerCluster; bladeIndex++)
                    BuildBlade(buffers, spec, clusterScale, lod, bladeIndex, activeBladeCountPerCluster, clusterOffset, clusterYawOffset);

                for (int bulbIndex = 0; bulbIndex < activeBulbCountPerCluster; bulbIndex++)
                    BuildBulb(buffers, spec, clusterScale, lod, bulbIndex, activeBulbCountPerCluster, clusterOffset, clusterYawOffset);
            }

            if (buffers.Indices.Count < 3)
                return false;

            mesh = CreateMesh(rootToken, lod, buffers);
            return mesh != null;
        }

        private static void BuildHoldfast(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod)
        {
            int rootCount = Mathf.Max(1, spec.RootCount - (lod * 2));
            int rootSegments = Mathf.Max(2, 4 - lod);
            float baseRadius = Mathf.Max(0.018f, scale.x * 0.14f);

            for (int i = 0; i < rootCount; i++)
            {
                float t = rootCount <= 1 ? 0f : i / (float)(rootCount - 1);
                float yaw = t * TwoPi + spec.RootYawOffset;
                Vector3 dir = new Vector3(MathLodApproximation.ApproxCosBhaskara(yaw), 0f, MathLodApproximation.ApproxSinBhaskara(yaw));
                Vector3 origin = new Vector3(0f, scale.y * 0.06f, 0f) + dir * (scale.x * 0.06f);
                float length = scale.x * Mathf.Lerp(0.36f, 0.62f, 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara((i + 1) * 1.37f));
                AddRibbon(
                    buffers,
                    origin,
                    dir,
                    Vector3.up,
                    baseRadius * Mathf.Lerp(0.82f, 1.08f, t),
                    length,
                    0.14f,
                    rootSegments,
                    0.08f,
                    0.16f,
                    new Color32(spec.TintByte, 196, 46, 255));
            }
        }

        private static void BuildStipe(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            int radialSegments = Mathf.Max(3, spec.StipeSides - (lod * 2));
            int heightSegments = Mathf.Max(2, spec.StipeSegments - (lod * 3));
            float height = scale.y * spec.StipeHeightMultiplier;
            float bottomRadius = Mathf.Max(0.02f, scale.x * spec.BaseRadiusMultiplier);
            float topRadius = Mathf.Max(bottomRadius * 0.42f, scale.x * spec.TopRadiusMultiplier);
            float bend = spec.BendDegrees;
            float bladeBandMin = Mathf.Clamp01(spec.BladeAnchorHeightMin / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bladeBandMax = Mathf.Clamp01(spec.BladeAnchorHeightMax / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bulbBandMin = Mathf.Clamp01(spec.BulbHeightMin / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bulbBandMax = Mathf.Clamp01(spec.BulbHeightMax / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = y / (float)heightSegments;
                float bendRadians = bend * Mathf.Deg2Rad * v * v;
                float wobbleX = MathLodApproximation.ApproxSinBhaskara((v * 2.6f + spec.BendDegrees * 0.02f) * Mathf.PI) * scale.x * 0.03f;
                float wobbleZ = MathLodApproximation.ApproxSinBhaskara((v * 4.3f + spec.RibCount * 0.11f) * Mathf.PI) * scale.z * 0.018f;
                Vector3 center = EvaluateStipeCenter(spec, scale, v, baseOffset, clusterYawOffsetDegrees);
                float bladeBand = EvaluateBand(v, bladeBandMin, bladeBandMax, 0.085f);
                float bulbBand = EvaluateBand(v, bulbBandMin, bulbBandMax, 0.07f);
                float nodeBulge = bladeBand * 0.22f + bulbBand * 0.14f;
                float scarNoise = MathLodApproximation.ApproxSinBhaskara((v * 8.5f + spec.BendDegrees * 0.03f) * Mathf.PI) * 0.035f;
                float radius = Mathf.Lerp(bottomRadius, topRadius, v) * (1f + nodeBulge + scarNoise);

                for (int side = 0; side <= radialSegments; side++)
                {
                    float u = side / (float)radialSegments;
                    float angle = u * TwoPi;
                    float rib = 1f + MathLodApproximation.ApproxSinBhaskara(angle * spec.RibCount + v * 2.2f) * spec.RibAmplitude;
                    float actualRadius = radius * rib;
                    Vector3 radial = new Vector3(MathLodApproximation.ApproxCosBhaskara(angle), 0f, MathLodApproximation.ApproxSinBhaskara(angle));
                    Vector3 normal = radial.normalized;
                    Vector3 vertex = center + radial * actualRadius;
                    Vector4 tangent = new Vector4(-MathLodApproximation.ApproxSinBhaskara(angle), 0f, MathLodApproximation.ApproxCosBhaskara(angle), 1f);
                    byte green = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(92f, 188f, v) + bladeBand * 10f - bulbBand * 6f), 0, 255);
                    byte blue = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(32f, 186f, v) - bladeBand * 12f + bulbBand * 8f), 0, 255);
                    buffers.AddVertex(vertex, normal, tangent, new Vector2(u, v), new Color32(spec.TintByte, green, blue, 255));
                }
            }

            int rowSize = radialSegments + 1;
            for (int y = 0; y < heightSegments; y++)
            {
                int rowStart = y * rowSize;
                int nextRowStart = (y + 1) * rowSize;
                for (int side = 0; side < radialSegments; side++)
                    buffers.AddQuad(rowStart + side, nextRowStart + side, nextRowStart + side + 1, rowStart + side + 1);
            }
        }

        private static float EvaluateBand(float value, float bandMin, float bandMax, float feather)
        {
            float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bandMin - feather, bandMin + feather, value));
            float upper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bandMax - feather, bandMax + feather, value));
            return Mathf.Clamp01(lower * upper);
        }

        private static void BuildBlade(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bladeIndex, int bladeCount, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            bool towerLaminar = IsTowerLaminarVariant(spec);
            bool canopySheet = IsCanopySheetVariant(spec);
            bool foldedSheet = IsFoldedSheetVariant(spec);
            bool foldedGiant = IsFoldedGiantVariant(spec);
            bool paddleLobed = IsPaddleLobedVariant(spec);
            bool broadleafKelp = IsBroadleafKelpVariant(spec);
            bool sailKelp = IsSailKelpVariant(spec);
            bool paddlefanKelp = IsPaddlefanVariant(spec);
            bool frilledRibbon = IsFrilledRibbonVariant(spec);
            bool deepPetal = IsDeepPetalVariant(spec);
            int bladeSegments = ResolveBladeSegments(spec, lod, foldedSheet, foldedGiant, paddleLobed, canopySheet, towerLaminar, broadleafKelp, paddlefanKelp, sailKelp, deepPetal);
            float sequence = bladeCount <= 1 ? 0f : bladeIndex / (float)(bladeCount - 1);
            float normalized;
            if (bladeCount <= 1)
            {
                normalized = spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.62f : 0.76f;
            }
            else if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                normalized = spec.ClusterCount > 1
                    ? Mathf.Lerp(0.04f, 0.88f, Mathf.SmoothStep(0f, 1f, sequence))
                    : Mathf.Lerp(0.08f, 0.98f, Mathf.SmoothStep(0f, 1f, sequence));
            }
            else
            {
                normalized = Mathf.Lerp(0.16f, 1f, MathLodApproximation.ApproxPow01Curve(sequence, 0.9f));
            }

            float primaryAngleOffset = EvaluateBladeAngleOffset(spec, bladeIndex, sequence);
            BladeSocket primarySocket = EvaluateBladeSocket(spec, scale, normalized, primaryAngleOffset, baseOffset, clusterYawOffsetDegrees);
            Vector3 lateral = primarySocket.WidthAxis;
            Vector3 forward = primarySocket.ForwardAxis;
            Vector3 up = primarySocket.GrowthAxis;
            Vector3 stemBase = primarySocket.StemBase;
            Vector3 anchor = primarySocket.Anchor;
            float width = scale.x * Mathf.Lerp(spec.BladeWidthMin, spec.BladeWidthMax, normalized);
            float length = scale.y * Mathf.Lerp(spec.BladeLengthMin, spec.BladeLengthMax, 1f - normalized * spec.BladeLengthFalloff);
            float sideCurve = Mathf.Lerp(-spec.SideCurveDegrees, spec.SideCurveDegrees, normalized);
            float twist = Mathf.Lerp(spec.TwistDegreesMin, spec.TwistDegreesMax, normalized);
            float serration = lod == 0 ? spec.SerrationAmplitude : spec.SerrationAmplitude * 0.4f;
            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                float morphologyNoise = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 2.17f + normalized * 4.9f);
                float widthMin = spec.ClusterCount > 1 ? 0.84f : 0.9f;
                float widthMax = spec.ClusterCount > 1 ? 1.22f : 1.14f;
                float lengthMin = spec.ClusterCount > 1 ? 0.88f : 0.92f;
                float lengthMax = spec.ClusterCount > 1 ? 1.16f : 1.1f;
                float curveRange = spec.ClusterCount > 1 ? 12f : 9f;
                float twistRange = spec.ClusterCount > 1 ? 18f : 14f;
                width *= Mathf.Lerp(widthMin, widthMax, morphologyNoise);
                length *= Mathf.Lerp(lengthMin, lengthMax, 1f - morphologyNoise);
                sideCurve += Mathf.Lerp(-curveRange, curveRange, morphologyNoise);
                twist += Mathf.Lerp(-twistRange, twistRange, 1f - morphologyNoise);
            }

            if (towerLaminar)
            {
                float lowerBlade = 1f - Mathf.SmoothStep(0.18f, 0.52f, normalized);
                float upperBlade = Mathf.SmoothStep(0.34f, 0.94f, normalized);
                width *= Mathf.Lerp(0.72f, 1.18f, upperBlade);
                length *= Mathf.Lerp(0.84f, 1.12f, upperBlade);
                sideCurve *= Mathf.Lerp(0.34f, 0.62f, upperBlade);
                twist *= Mathf.Lerp(0.48f, 0.72f, upperBlade);
                serration *= Mathf.Lerp(0.74f, 0.88f, upperBlade);
                length *= Mathf.Lerp(0.82f, 1f, 1f - lowerBlade * 0.55f);
            }
            else if (canopySheet)
            {
                float sheetMass = Mathf.SmoothStep(0.18f, 0.92f, normalized);
                width *= Mathf.Lerp(0.94f, 1.24f, sheetMass);
                length *= Mathf.Lerp(0.96f, 1.16f, sheetMass);
                sideCurve *= Mathf.Lerp(0.4f, 0.76f, sheetMass);
                twist *= Mathf.Lerp(0.44f, 0.68f, sheetMass);
                serration *= Mathf.Lerp(0.82f, 0.94f, sheetMass);
            }

            if (foldedGiant)
            {
                float foldedMass = Mathf.SmoothStep(0.12f, 0.86f, normalized);
                width *= Mathf.Lerp(1.04f, spec.ClusterCount > 1 ? 1.2f : 1.16f, foldedMass);
                length *= Mathf.Lerp(0.98f, spec.ClusterCount > 1 ? 1.12f : 1.08f, foldedMass);
                sideCurve *= Mathf.Lerp(0.46f, 0.66f, foldedMass);
                twist *= Mathf.Lerp(0.42f, 0.64f, foldedMass);
                serration *= Mathf.Lerp(0.72f, 0.86f, foldedMass);

                if (sailKelp)
                {
                    width *= Mathf.Lerp(1.1f, 1.24f, foldedMass);
                    length *= Mathf.Lerp(1.04f, 1.16f, foldedMass);
                    sideCurve += Mathf.Lerp(-8f, 10f, foldedMass);
                    twist += Mathf.Lerp(-5f, 8f, 1f - foldedMass);
                }
            }

            if (foldedSheet)
            {
                float tapestryMass = Mathf.SmoothStep(0.12f, 0.94f, normalized);
                width *= Mathf.Lerp(1.08f, 1.34f, tapestryMass);
                length *= Mathf.Lerp(1.02f, 1.18f, tapestryMass);
                sideCurve *= Mathf.Lerp(0.28f, 0.52f, tapestryMass);
                twist *= Mathf.Lerp(0.34f, 0.56f, tapestryMass);
                serration *= Mathf.Lerp(0.76f, 0.88f, tapestryMass);
            }

            if (paddleLobed)
            {
                float lobeMass = Mathf.SmoothStep(0.1f, 0.92f, normalized);
                width *= Mathf.Lerp(1.04f, spec.ClusterCount > 1 ? 1.18f : 1.14f, lobeMass);
                length *= Mathf.Lerp(0.92f, spec.GrowthStyle == GrowthStyle.CrownCanopy ? 1.08f : 1.01f, lobeMass);
                sideCurve *= Mathf.Lerp(0.34f, 0.62f, lobeMass);
                twist *= Mathf.Lerp(0.28f, 0.52f, lobeMass);
                serration *= Mathf.Lerp(1.02f, 1.14f, lobeMass);

                if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
                {
                    width *= Mathf.Lerp(1.04f, 1.12f, lobeMass);
                    length *= Mathf.Lerp(1f, 1.08f, lobeMass);
                    sideCurve *= Mathf.Lerp(0.82f, 0.9f, lobeMass);
                    twist *= Mathf.Lerp(0.78f, 0.88f, lobeMass);
                }
                else if (spec.ClusterCount <= 1)
                {
                    width *= Mathf.Lerp(1.02f, 1.08f, lobeMass);
                    length *= Mathf.Lerp(0.98f, 1.04f, lobeMass);
                }

                if (broadleafKelp)
                {
                    width *= Mathf.Lerp(1.08f, 1.22f, lobeMass);
                    length *= Mathf.Lerp(1.02f, 1.12f, lobeMass);
                    sideCurve += Mathf.Lerp(-8f, 12f, lobeMass);
                    twist += Mathf.Lerp(-4f, 10f, 1f - lobeMass);
                }

                if (paddlefanKelp)
                {
                    width *= Mathf.Lerp(1.08f, 1.2f, lobeMass);
                    length *= Mathf.Lerp(1.04f, 1.12f, lobeMass);
                    sideCurve += Mathf.Lerp(-10f, 14f, lobeMass);
                    twist += Mathf.Lerp(-6f, 10f, 1f - lobeMass);
                }

                if (deepPetal)
                {
                    width *= Mathf.Lerp(1.04f, 1.16f, lobeMass);
                    length *= Mathf.Lerp(1.02f, 1.1f, lobeMass);
                    sideCurve += Mathf.Lerp(-12f, 16f, lobeMass);
                    twist += Mathf.Lerp(-8f, 12f, 1f - lobeMass);
                }
            }

            if (frilledRibbon)
            {
                float frillMass = Mathf.SmoothStep(0.08f, 0.94f, normalized);
                width *= Mathf.Lerp(0.96f, spec.GrowthStyle == GrowthStyle.CrownCanopy ? 1.08f : 1.01f, frillMass);
                length *= Mathf.Lerp(1f, spec.GrowthStyle == GrowthStyle.CrownCanopy ? 1.11f : 1.05f, frillMass);
                sideCurve *= Mathf.Lerp(0.52f, 0.84f, frillMass);
                twist *= Mathf.Lerp(0.88f, 1.22f, frillMass);
                serration *= Mathf.Lerp(1.22f, 1.44f, frillMass);
            }

            BladeProfile primaryProfile = ResolveBladeProfile(spec, bladeIndex, normalized, false);
            AddBladeStem(
                buffers,
                stemBase,
                anchor + up * (scale.y * 0.032f) + forward * (length * 0.04f),
                primarySocket.StipeTangentAxis,
                forward,
                Mathf.Max(scale.x * 0.012f, (anchor - stemBase).magnitude * 0.24f),
                scale.x * Mathf.Lerp(0.015f, 0.009f, normalized),
                lod,
                new Color32(spec.TintByte, 184, 52, 255));
            AddBladeRibbon(buffers, spec, anchor, lateral, up, width, length, twist, bladeSegments, sideCurve, serration, new Color32(spec.TintByte, 208, (byte)Mathf.Lerp(40f, 210f, normalized), 255), primaryProfile, forward, lod);

            BladeBuildContext ctx = new BladeBuildContext(
                buffers, spec, scale, lod, bladeIndex, normalized, primaryAngleOffset,
                baseOffset, clusterYawOffsetDegrees, width, length, twist, sideCurve,
                serration, bladeSegments);

            BuildUnderstoryBlade(in ctx, paddleLobed, foldedGiant, frilledRibbon);

            BuildCompanionBlade(in ctx);

            BuildTertiaryBlade(in ctx);

            BuildBridgingBlade(in ctx, paddleLobed, frilledRibbon, broadleafKelp);

            BuildCurtainBlade(in ctx, foldedSheet);

            BuildSailBlade(in ctx, foldedGiant, sailKelp);

            BuildFanBlade(in ctx, paddleLobed, broadleafKelp, paddlefanKelp);

            BuildMantleBlade(in ctx, paddleLobed);

            BuildBackingBlade(in ctx, sailKelp);

            BuildLowerMantleBlade(in ctx, paddlefanKelp);

            BuildInnerBlade(in ctx, broadleafKelp);

            BuildShroudBlade(in ctx);

            BuildVeilBlade(in ctx, frilledRibbon);
        }

        private static void BuildUnderstoryBlade(in BladeBuildContext ctx, bool paddleLobed, bool foldedGiant, bool frilledRibbon)
        {
            if (ctx.Spec.ClusterCount > 1
                && !paddleLobed
                && !foldedGiant
                && !frilledRibbon
                && ctx.Lod == 0
                && (ctx.BladeIndex % 2 == 0)
                && ctx.Normalized > 0.24f
                && ctx.Normalized < 0.7f)
            {
                int understoryStemLod = ResolveSupplementalStemLod(ctx.Spec, ctx.Lod);
                int understoryBladeSegments = ResolveUnderstoryBladeSegments(ctx.Spec, ctx.BladeSegments);
                float understoryNormalized = Mathf.Max(0.06f, ctx.Normalized - 0.18f);
                float understorySweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 1) == 0) ? -1f : 1f) * 10f;
                BladeSocket understorySocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, understoryNormalized, understorySweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 understoryLateral = understorySocket.WidthAxis;
                Vector3 understoryForward = understorySocket.ForwardAxis;
                Vector3 understoryUp = understorySocket.GrowthAxis;
                Vector3 understoryStemBase = understorySocket.StemBase;
                Vector3 understoryAnchor = understorySocket.Anchor;
                float understoryWidth = ctx.Width * 0.62f;
                float understoryLength = ctx.Length * 0.58f;
                BladeProfile understoryProfile = ResolveBladeProfile(ctx.Spec, ctx.BladeIndex + 7, understoryNormalized, true);

                AddBladeStem(
                    ctx.Buffers,
                    understoryStemBase,
                    understoryAnchor + understoryUp * (ctx.Scale.y * 0.02f) + understoryForward * (understoryLength * 0.028f),
                    understorySocket.StipeTangentAxis,
                    understoryForward,
                    Mathf.Max(ctx.Scale.x * 0.007f, (understoryAnchor - understoryStemBase).magnitude * 0.16f),
                    ctx.Scale.x * 0.0062f,
                    understoryStemLod,
                    new Color32(ctx.Spec.TintByte, 176, 54, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    understoryAnchor,
                    understoryLateral,
                    understoryUp,
                    understoryWidth,
                    understoryLength,
                    ctx.Twist * 0.74f,
                    understoryBladeSegments,
                    ctx.SideCurve * 0.58f,
                    ctx.Serration * 0.62f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 4, 0, 255), 204, (byte)Mathf.Lerp(54f, 168f, understoryNormalized), 255),
                    understoryProfile,
                    understoryForward,
                    ctx.Lod);
            }
        }

        private static void BuildCompanionBlade(in BladeBuildContext ctx)
        {
            if (ShouldAddCompanionBlade(ctx.Spec, ctx.Lod, ctx.BladeIndex, ctx.Normalized))
            {
                int companionStemLod = ResolveSupplementalStemLod(ctx.Spec, ctx.Lod);
                int companionBladeSegments = ResolveCompanionBladeSegments(ctx.Spec, ctx.BladeSegments);
                float companionSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 1) == 0) ? 1f : -1f) * Mathf.Lerp(12f, 26f, ctx.Normalized);
                BladeSocket companionSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, ctx.Normalized, companionSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 companionLateral = companionSocket.WidthAxis;
                Vector3 companionForward = companionSocket.ForwardAxis;
                Vector3 companionUp = companionSocket.GrowthAxis;
                Vector3 companionStemBase = companionSocket.StemBase;
                Vector3 companionAnchor = companionSocket.Anchor;
                float companionWidth = ctx.Width * Mathf.Lerp(0.42f, 0.62f, 1f - ctx.Normalized);
                float companionLength = ctx.Length * Mathf.Lerp(0.46f, 0.68f, 1f - ctx.Normalized * 0.4f);
                float companionTwist = ctx.Twist + Mathf.Lerp(-12f, 16f, ctx.Normalized);
                float companionCurve = ctx.SideCurve * 0.55f + Mathf.Lerp(-8f, 8f, ctx.Normalized);
                float companionSerration = ctx.Serration * 0.75f;
                BladeProfile companionProfile = ResolveBladeProfile(ctx.Spec, ctx.BladeIndex + 13, ctx.Normalized, true);

                AddBladeStem(
                    ctx.Buffers,
                    companionStemBase,
                    companionAnchor + companionUp * (ctx.Scale.y * 0.026f) + companionForward * (companionLength * 0.035f),
                    companionSocket.StipeTangentAxis,
                    companionForward,
                    Mathf.Max(ctx.Scale.x * 0.008f, (companionAnchor - companionStemBase).magnitude * 0.18f),
                    ctx.Scale.x * Mathf.Lerp(0.009f, 0.0065f, ctx.Normalized),
                    companionStemLod,
                    new Color32(ctx.Spec.TintByte, 172, 58, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    companionAnchor,
                    companionLateral,
                    companionUp,
                    companionWidth,
                    companionLength,
                    companionTwist,
                    companionBladeSegments,
                    companionCurve,
                    companionSerration,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 6, 0, 255), 214, (byte)Mathf.Lerp(56f, 196f, ctx.Normalized), 255),
                    companionProfile,
                    companionForward,
                    ctx.Lod);
            }
        }

        private static void BuildTertiaryBlade(in BladeBuildContext ctx)
        {
            if (ShouldAddTertiaryBlade(ctx.Spec, ctx.Lod, ctx.BladeIndex, ctx.Normalized))
            {
                int tertiaryStemLod = ResolveSupplementalStemLod(ctx.Spec, ctx.Lod);
                int tertiaryBladeSegments = ResolveTertiaryBladeSegments(ctx.Spec, ctx.BladeSegments);
                float tertiarySweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 1) == 0) ? -1f : 1f) * Mathf.Lerp(28f, 44f, ctx.Normalized);
                BladeSocket tertiarySocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, ctx.Normalized, tertiarySweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 tertiaryLateral = tertiarySocket.WidthAxis;
                Vector3 tertiaryForward = tertiarySocket.ForwardAxis;
                Vector3 tertiaryUp = tertiarySocket.GrowthAxis;
                Vector3 tertiaryStemBase = tertiarySocket.StemBase;
                Vector3 tertiaryAnchor = tertiarySocket.Anchor;
                float tertiaryWidth = ctx.Width * Mathf.Lerp(0.28f, 0.46f, 1f - ctx.Normalized);
                float tertiaryLength = ctx.Length * Mathf.Lerp(0.34f, 0.52f, 1f - ctx.Normalized * 0.28f);
                float tertiaryTwist = ctx.Twist + Mathf.Lerp(-18f, 22f, ctx.Normalized);
                float tertiaryCurve = ctx.SideCurve * 0.34f + Mathf.Lerp(-12f, 12f, ctx.Normalized);
                float tertiarySerration = ctx.Serration * 0.58f;
                BladeProfile tertiaryProfile = ResolveBladeProfile(ctx.Spec, ctx.BladeIndex + 19, ctx.Normalized, true);

                AddBladeStem(
                    ctx.Buffers,
                    tertiaryStemBase,
                    tertiaryAnchor + tertiaryUp * (ctx.Scale.y * 0.021f) + tertiaryForward * (tertiaryLength * 0.03f),
                    tertiarySocket.StipeTangentAxis,
                    tertiaryForward,
                    Mathf.Max(ctx.Scale.x * 0.006f, (tertiaryAnchor - tertiaryStemBase).magnitude * 0.14f),
                    ctx.Scale.x * Mathf.Lerp(0.0075f, 0.0052f, ctx.Normalized),
                    tertiaryStemLod,
                    new Color32(ctx.Spec.TintByte, 166, 62, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    tertiaryAnchor,
                    tertiaryLateral,
                    tertiaryUp,
                    tertiaryWidth,
                    tertiaryLength,
                    tertiaryTwist,
                    tertiaryBladeSegments,
                    tertiaryCurve,
                    tertiarySerration,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 10, 0, 255), 220, (byte)Mathf.Lerp(64f, 188f, ctx.Normalized), 255),
                    tertiaryProfile,
                    tertiaryForward,
                    ctx.Lod);
            }
        }

        private static void BuildBridgingBlade(in BladeBuildContext ctx, bool paddleLobed, bool frilledRibbon, bool broadleafKelp)
        {
            if (ctx.Spec.GrowthStyle == GrowthStyle.GiantFrond
                && ctx.Spec.ClusterCount <= 1
                && ctx.Lod == 0
                && ctx.Normalized > (paddleLobed || frilledRibbon ? 0.36f : 0.24f)
                && ctx.Normalized < (paddleLobed || frilledRibbon ? 0.74f : 0.82f)
                && (ctx.BladeIndex % (broadleafKelp ? 3 : paddleLobed || frilledRibbon ? 4 : 2) == 1))
            {
                float bridgingNormalized = Mathf.Clamp01(ctx.Normalized - 0.08f + (((ctx.BladeIndex / 2) & 1) == 0 ? 0.03f : -0.015f));
                float bridgingSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(broadleafKelp ? 14f : 8f, broadleafKelp ? 28f : 16f, bridgingNormalized);
                BladeSocket bridgingSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, bridgingNormalized, bridgingSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 bridgingLateral = bridgingSocket.WidthAxis;
                Vector3 bridgingForward = bridgingSocket.ForwardAxis;
                Vector3 bridgingUp = bridgingSocket.GrowthAxis;
                Vector3 bridgingStemBase = bridgingSocket.StemBase;
                Vector3 bridgingAnchor = bridgingSocket.Anchor;
                float bridgingWidth = ctx.Width * Mathf.Lerp(broadleafKelp ? 0.42f : 0.34f, broadleafKelp ? 0.58f : 0.48f, 1f - bridgingNormalized);
                float bridgingLength = ctx.Length * Mathf.Lerp(broadleafKelp ? 0.46f : 0.38f, broadleafKelp ? 0.68f : 0.56f, 1f - bridgingNormalized * 0.34f);
                float bridgingTwist = ctx.Twist + Mathf.Lerp(broadleafKelp ? -14f : -8f, broadleafKelp ? 18f : 12f, bridgingNormalized);
                float bridgingCurve = ctx.SideCurve * (broadleafKelp ? 0.58f : 0.42f) + Mathf.Lerp(broadleafKelp ? -10f : -6f, broadleafKelp ? 10f : 6f, bridgingNormalized);
                float bridgingSerration = ctx.Serration * (broadleafKelp ? 0.66f : 0.54f);
                BladeProfile bridgingProfile = ResolveBladeProfile(ctx.Spec, ctx.BladeIndex + 29, bridgingNormalized, true);

                AddBladeStem(
                    ctx.Buffers,
                    bridgingStemBase,
                    bridgingAnchor + bridgingUp * (ctx.Scale.y * 0.022f) + bridgingForward * (bridgingLength * 0.032f),
                    bridgingSocket.StipeTangentAxis,
                    bridgingForward,
                    Mathf.Max(ctx.Scale.x * 0.0068f, (bridgingAnchor - bridgingStemBase).magnitude * 0.16f),
                    ctx.Scale.x * Mathf.Lerp(0.0082f, 0.0058f, bridgingNormalized),
                    1,
                    new Color32(ctx.Spec.TintByte, 170, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    bridgingAnchor,
                    bridgingLateral,
                    bridgingUp,
                    bridgingWidth,
                    bridgingLength,
                    bridgingTwist,
                    Mathf.Max(2, ctx.BladeSegments - 4),
                    bridgingCurve,
                    bridgingSerration,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 8, 0, 255), 214, (byte)Mathf.Lerp(62f, 188f, bridgingNormalized), 255),
                    bridgingProfile,
                    bridgingForward,
                    ctx.Lod);
            }
        }

        private static void BuildCurtainBlade(in BladeBuildContext ctx, bool foldedSheet)
        {
            if (foldedSheet
                && ctx.Lod == 0
                && ctx.Normalized > 0.36f
                && ctx.Normalized < 0.94f
                && (ctx.BladeIndex % 2 == 1))
            {
                float curtainNormalized = Mathf.Clamp01(ctx.Normalized - 0.04f);
                float curtainSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(3f, 7f, curtainNormalized);
                BladeSocket curtainSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, curtainNormalized, curtainSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 curtainLateral = curtainSocket.WidthAxis;
                Vector3 curtainForward = curtainSocket.ForwardAxis;
                Vector3 curtainUp = curtainSocket.GrowthAxis;
                Vector3 curtainStemBase = curtainSocket.StemBase;
                Vector3 curtainAnchor = curtainSocket.Anchor;
                float curtainWidth = ctx.Width * Mathf.Lerp(0.52f, 0.72f, 1f - curtainNormalized * 0.22f);
                float curtainLength = ctx.Length * Mathf.Lerp(0.62f, 0.84f, 1f - curtainNormalized * 0.18f);
                float curtainTwist = ctx.Twist * 0.72f + Mathf.Lerp(-4f, 7f, curtainNormalized);
                float curtainCurve = ctx.SideCurve * 0.38f + Mathf.Lerp(-4f, 4f, curtainNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    curtainStemBase,
                    curtainAnchor + curtainUp * (ctx.Scale.y * 0.024f) + curtainForward * (curtainLength * 0.026f),
                    curtainSocket.StipeTangentAxis,
                    curtainForward,
                    Mathf.Max(ctx.Scale.x * 0.0072f, (curtainAnchor - curtainStemBase).magnitude * 0.16f),
                    ctx.Scale.x * 0.0064f,
                    1,
                    new Color32(ctx.Spec.TintByte, 178, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    curtainAnchor,
                    curtainLateral,
                    curtainUp,
                    curtainWidth,
                    curtainLength,
                    curtainTwist,
                    Mathf.Max(2, ctx.BladeSegments - 2),
                    curtainCurve,
                    ctx.Serration * 0.66f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 8, 0, 255), 214, (byte)Mathf.Lerp(66f, 194f, curtainNormalized), 255),
                    BladeProfile.FoldedLamina,
                    curtainForward,
                    ctx.Lod);
            }
        }

        private static void BuildSailBlade(in BladeBuildContext ctx, bool foldedGiant, bool sailKelp)
        {
            if (foldedGiant
                && !sailKelp
                && !IsVeilwallVariant(ctx.Spec)
                && ctx.Lod == 0
                && ctx.Normalized > 0.22f
                && ctx.Normalized < 0.88f
                && (ctx.BladeIndex % 2 == 0))
            {
                float sailNormalized = Mathf.Clamp01(ctx.Normalized - 0.04f);
                float sailSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(2f, 8f, sailNormalized);
                BladeSocket sailSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, sailNormalized, sailSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 sailLateral = sailSocket.WidthAxis;
                Vector3 sailForward = sailSocket.ForwardAxis;
                Vector3 sailUp = sailSocket.GrowthAxis;
                Vector3 sailStemBase = sailSocket.StemBase;
                Vector3 sailAnchor = sailSocket.Anchor;
                float sailWidth = ctx.Width * Mathf.Lerp(0.52f, 0.74f, 1f - sailNormalized * 0.2f);
                float sailLength = ctx.Length * Mathf.Lerp(0.66f, 0.9f, 1f - sailNormalized * 0.14f);
                float sailTwist = ctx.Twist * 0.62f + Mathf.Lerp(-4f, 6f, sailNormalized);
                float sailCurve = ctx.SideCurve * 0.32f + Mathf.Lerp(-5f, 5f, sailNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    sailStemBase,
                    sailAnchor + sailUp * (ctx.Scale.y * 0.024f) + sailForward * (sailLength * 0.026f),
                    sailSocket.StipeTangentAxis,
                    sailForward,
                    Mathf.Max(ctx.Scale.x * 0.0072f, (sailAnchor - sailStemBase).magnitude * 0.16f),
                    ctx.Scale.x * 0.0064f,
                    1,
                    new Color32(ctx.Spec.TintByte, 180, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    sailAnchor,
                    sailLateral,
                    sailUp,
                    sailWidth,
                    sailLength,
                    sailTwist,
                    Mathf.Max(2, ctx.BladeSegments - 2),
                    sailCurve,
                    ctx.Serration * 0.62f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 6, 0, 255), 214, (byte)Mathf.Lerp(66f, 194f, sailNormalized), 255),
                    BladeProfile.FoldedLamina,
                    sailForward,
                    ctx.Lod);
            }
        }

        private static void BuildFanBlade(in BladeBuildContext ctx, bool paddleLobed, bool broadleafKelp, bool paddlefanKelp)
        {
            if (paddleLobed
                && ctx.Lod == 0
                && ctx.Normalized > (broadleafKelp ? 0.24f : ctx.Spec.ClusterCount > 1 ? 0.42f : 0.38f)
                && ctx.Normalized < (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.86f : 0.8f)
                && (ctx.BladeIndex % (broadleafKelp ? 2 : paddlefanKelp ? 2 : ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 3 : 4) == 0))
            {
                float fanNormalized = Mathf.Clamp01(ctx.Normalized - 0.05f);
                float fanSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(broadleafKelp ? 10f : paddlefanKelp ? 8f : 4f, broadleafKelp ? 26f : paddlefanKelp ? 22f : 11f, fanNormalized);
                BladeSocket fanSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, fanNormalized, fanSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 fanLateral = fanSocket.WidthAxis;
                Vector3 fanForward = fanSocket.ForwardAxis;
                Vector3 fanUp = fanSocket.GrowthAxis;
                Vector3 fanStemBase = fanSocket.StemBase;
                Vector3 fanAnchor = fanSocket.Anchor;
                float fanWidth = ctx.Width * Mathf.Lerp(broadleafKelp ? 0.58f : paddlefanKelp ? 0.54f : 0.46f, broadleafKelp ? 0.78f : paddlefanKelp ? 0.82f : ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.68f : 0.6f, 1f - fanNormalized * 0.18f);
                float fanLength = ctx.Length * Mathf.Lerp(broadleafKelp ? 0.56f : paddlefanKelp ? 0.52f : 0.46f, broadleafKelp ? 0.82f : paddlefanKelp ? 0.8f : ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.72f : 0.62f, 1f - fanNormalized * 0.12f);
                float fanTwist = ctx.Twist * (broadleafKelp ? 0.74f : paddlefanKelp ? 0.7f : 0.62f) + Mathf.Lerp(broadleafKelp ? -8f : paddlefanKelp ? -6f : -3f, broadleafKelp ? 10f : paddlefanKelp ? 12f : 6f, fanNormalized);
                float fanCurve = ctx.SideCurve * (broadleafKelp ? 0.46f : paddlefanKelp ? 0.4f : 0.3f) + Mathf.Lerp(broadleafKelp ? -10f : paddlefanKelp ? -12f : -5f, broadleafKelp ? 10f : paddlefanKelp ? 12f : 5f, fanNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    fanStemBase,
                    fanAnchor + fanUp * (ctx.Scale.y * 0.023f) + fanForward * (fanLength * 0.028f),
                    fanSocket.StipeTangentAxis,
                    fanForward,
                    Mathf.Max(ctx.Scale.x * 0.0068f, (fanAnchor - fanStemBase).magnitude * 0.15f),
                    ctx.Scale.x * 0.0061f,
                    1,
                    new Color32(ctx.Spec.TintByte, 178, 62, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    fanAnchor,
                    fanLateral,
                    fanUp,
                    fanWidth,
                    fanLength,
                    fanTwist,
                    Mathf.Max(2, ctx.BladeSegments - (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 3 : 4)),
                    fanCurve,
                    ctx.Serration * 0.74f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 8, 0, 255), 216, (byte)Mathf.Lerp(70f, 198f, fanNormalized), 255),
                    BladeProfile.PaddleLobed,
                    fanForward,
                    ctx.Lod);
            }
        }

        private static void BuildMantleBlade(in BladeBuildContext ctx, bool paddleLobed)
        {
            if (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy
                && paddleLobed
                && ctx.Lod == 0
                && ctx.Normalized > 0.24f
                && ctx.Normalized < 0.9f
                && (ctx.BladeIndex % 2 == 1))
            {
                float mantleNormalized = Mathf.Clamp01(ctx.Normalized - 0.04f);
                float mantleSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(8f, 18f, mantleNormalized);
                BladeSocket mantleSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, mantleNormalized, mantleSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 mantleLateral = mantleSocket.WidthAxis;
                Vector3 mantleForward = mantleSocket.ForwardAxis;
                Vector3 mantleUp = mantleSocket.GrowthAxis;
                Vector3 mantleStemBase = mantleSocket.StemBase;
                Vector3 mantleAnchor = mantleSocket.Anchor;
                float mantleWidth = ctx.Width * Mathf.Lerp(0.42f, 0.62f, 1f - mantleNormalized * 0.18f);
                float mantleLength = ctx.Length * Mathf.Lerp(0.5f, 0.7f, 1f - mantleNormalized * 0.12f);
                float mantleTwist = ctx.Twist * 0.58f + Mathf.Lerp(-6f, 8f, mantleNormalized);
                float mantleCurve = ctx.SideCurve * 0.34f + Mathf.Lerp(-7f, 7f, mantleNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    mantleStemBase,
                    mantleAnchor + mantleUp * (ctx.Scale.y * 0.022f) + mantleForward * (mantleLength * 0.026f),
                    mantleSocket.StipeTangentAxis,
                    mantleForward,
                    Mathf.Max(ctx.Scale.x * 0.0068f, (mantleAnchor - mantleStemBase).magnitude * 0.15f),
                    ctx.Scale.x * 0.0059f,
                    1,
                    new Color32(ctx.Spec.TintByte, 178, 62, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    mantleAnchor,
                    mantleLateral,
                    mantleUp,
                    mantleWidth,
                    mantleLength,
                    mantleTwist,
                    Mathf.Max(2, ctx.BladeSegments - 3),
                    mantleCurve,
                    ctx.Serration * 0.62f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 6, 0, 255), 214, (byte)Mathf.Lerp(68f, 194f, mantleNormalized), 255),
                    BladeProfile.BroadUndulate,
                    mantleForward,
                    ctx.Lod);
            }
        }

        private static void BuildBackingBlade(in BladeBuildContext ctx, bool sailKelp)
        {
            if (sailKelp
                && ctx.Lod == 0
                && ctx.Normalized > 0.46f
                && ctx.Normalized < 0.78f
                && (ctx.BladeIndex % 5 == 2))
            {
                float backingNormalized = Mathf.Clamp01(ctx.Normalized - 0.03f);
                float backingSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(8f, 20f, backingNormalized);
                BladeSocket backingSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, backingNormalized, backingSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 backingLateral = backingSocket.WidthAxis;
                Vector3 backingForward = backingSocket.ForwardAxis;
                Vector3 backingUp = backingSocket.GrowthAxis;
                Vector3 backingStemBase = backingSocket.StemBase;
                Vector3 backingAnchor = backingSocket.Anchor;
                float backingWidth = ctx.Width * Mathf.Lerp(0.3f, 0.46f, 1f - backingNormalized * 0.18f);
                float backingLength = ctx.Length * Mathf.Lerp(0.42f, 0.62f, 1f - backingNormalized * 0.12f);
                float backingTwist = ctx.Twist * 0.56f + Mathf.Lerp(-8f, 10f, backingNormalized);
                float backingCurve = ctx.SideCurve * 0.34f + Mathf.Lerp(-10f, 10f, backingNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    backingStemBase,
                    backingAnchor + backingUp * (ctx.Scale.y * 0.022f) + backingForward * (backingLength * 0.028f),
                    backingSocket.StipeTangentAxis,
                    backingForward,
                    Mathf.Max(ctx.Scale.x * 0.0068f, (backingAnchor - backingStemBase).magnitude * 0.15f),
                    ctx.Scale.x * 0.006f,
                    1,
                    new Color32(ctx.Spec.TintByte, 178, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    backingAnchor,
                    backingLateral,
                    backingUp,
                    backingWidth,
                    backingLength,
                    backingTwist,
                    Mathf.Max(2, ctx.BladeSegments - 5),
                    backingCurve,
                    ctx.Serration * 0.58f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 6, 0, 255), 212, (byte)Mathf.Lerp(68f, 188f, backingNormalized), 255),
                    BladeProfile.FoldedLamina,
                    backingForward,
                    ctx.Lod);
            }
        }

        private static void BuildLowerMantleBlade(in BladeBuildContext ctx, bool paddlefanKelp)
        {
            if (paddlefanKelp
                && ctx.Lod == 0
                && ctx.Normalized > 0.2f
                && ctx.Normalized < 0.82f
                && (ctx.BladeIndex % 3 == 0))
            {
                float lowerMantleNormalized = Mathf.Clamp01(ctx.Normalized - 0.08f);
                float lowerMantleSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(14f, 30f, lowerMantleNormalized);
                BladeSocket lowerMantleSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, lowerMantleNormalized, lowerMantleSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 lowerMantleLateral = lowerMantleSocket.WidthAxis;
                Vector3 lowerMantleForward = lowerMantleSocket.ForwardAxis;
                Vector3 lowerMantleUp = lowerMantleSocket.GrowthAxis;
                Vector3 lowerMantleStemBase = lowerMantleSocket.StemBase;
                Vector3 lowerMantleAnchor = lowerMantleSocket.Anchor;
                float lowerMantleWidth = ctx.Width * Mathf.Lerp(0.36f, 0.54f, 1f - lowerMantleNormalized * 0.16f);
                float lowerMantleLength = ctx.Length * Mathf.Lerp(0.42f, 0.66f, 1f - lowerMantleNormalized * 0.08f);
                float lowerMantleTwist = ctx.Twist * 0.54f + Mathf.Lerp(-8f, 10f, lowerMantleNormalized);
                float lowerMantleCurve = ctx.SideCurve * 0.3f + Mathf.Lerp(-10f, 10f, lowerMantleNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    lowerMantleStemBase,
                    lowerMantleAnchor + lowerMantleUp * (ctx.Scale.y * 0.02f) + lowerMantleForward * (lowerMantleLength * 0.024f),
                    lowerMantleSocket.StipeTangentAxis,
                    lowerMantleForward,
                    Mathf.Max(ctx.Scale.x * 0.0062f, (lowerMantleAnchor - lowerMantleStemBase).magnitude * 0.14f),
                    ctx.Scale.x * 0.0054f,
                    1,
                    new Color32(ctx.Spec.TintByte, 176, 62, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    lowerMantleAnchor,
                    lowerMantleLateral,
                    lowerMantleUp,
                    lowerMantleWidth,
                    lowerMantleLength,
                    lowerMantleTwist,
                    Mathf.Max(2, ctx.BladeSegments - 4),
                    lowerMantleCurve,
                    ctx.Serration * 0.56f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 4, 0, 255), 208, (byte)Mathf.Lerp(70f, 184f, lowerMantleNormalized), 255),
                    BladeProfile.BroadUndulate,
                    lowerMantleForward,
                    ctx.Lod);
            }
        }

        private static void BuildInnerBlade(in BladeBuildContext ctx, bool broadleafKelp)
        {
            if (broadleafKelp
                && ctx.Lod == 0
                && ctx.Normalized > 0.18f
                && ctx.Normalized < 0.82f
                && (ctx.BladeIndex % 2 == 1))
            {
                float innerNormalized = Mathf.Clamp01(ctx.Normalized - 0.06f);
                float innerSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(6f, 18f, innerNormalized);
                BladeSocket innerSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, innerNormalized, innerSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 innerLateral = innerSocket.WidthAxis;
                Vector3 innerForward = innerSocket.ForwardAxis;
                Vector3 innerUp = innerSocket.GrowthAxis;
                Vector3 innerStemBase = innerSocket.StemBase;
                Vector3 innerAnchor = innerSocket.Anchor;
                float innerWidth = ctx.Width * Mathf.Lerp(0.34f, 0.48f, 1f - innerNormalized * 0.2f);
                float innerLength = ctx.Length * Mathf.Lerp(0.52f, 0.72f, 1f - innerNormalized * 0.16f);
                float innerTwist = ctx.Twist * 0.66f + Mathf.Lerp(-6f, 8f, innerNormalized);
                float innerCurve = ctx.SideCurve * 0.38f + Mathf.Lerp(-8f, 8f, innerNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    innerStemBase,
                    innerAnchor + innerUp * (ctx.Scale.y * 0.022f) + innerForward * (innerLength * 0.026f),
                    innerSocket.StipeTangentAxis,
                    innerForward,
                    Mathf.Max(ctx.Scale.x * 0.0064f, (innerAnchor - innerStemBase).magnitude * 0.14f),
                    ctx.Scale.x * 0.0058f,
                    1,
                    new Color32(ctx.Spec.TintByte, 176, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    innerAnchor,
                    innerLateral,
                    innerUp,
                    innerWidth,
                    innerLength,
                    innerTwist,
                    Mathf.Max(2, ctx.BladeSegments - 4),
                    innerCurve,
                    ctx.Serration * 0.54f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 6, 0, 255), 210, (byte)Mathf.Lerp(62f, 182f, innerNormalized), 255),
                    BladeProfile.BroadUndulate,
                    innerForward,
                    ctx.Lod);
            }
        }

        private static void BuildShroudBlade(in BladeBuildContext ctx)
        {
            if (IsDeepPetalVariant(ctx.Spec)
                && ctx.Lod == 0
                && ctx.Normalized > 0.22f
                && ctx.Normalized < 0.84f
                && (ctx.BladeIndex % 2 == 1))
            {
                float shroudNormalized = Mathf.Clamp01(ctx.Normalized - 0.05f);
                float shroudSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(10f, 24f, shroudNormalized);
                BladeSocket shroudSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, shroudNormalized, shroudSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 shroudLateral = shroudSocket.WidthAxis;
                Vector3 shroudForward = shroudSocket.ForwardAxis;
                Vector3 shroudUp = shroudSocket.GrowthAxis;
                Vector3 shroudStemBase = shroudSocket.StemBase;
                Vector3 shroudAnchor = shroudSocket.Anchor;
                float shroudWidth = ctx.Width * Mathf.Lerp(0.32f, 0.44f, 1f - shroudNormalized * 0.18f);
                float shroudLength = ctx.Length * Mathf.Lerp(0.5f, 0.68f, 1f - shroudNormalized * 0.12f);
                float shroudTwist = ctx.Twist * 0.7f + Mathf.Lerp(-10f, 12f, shroudNormalized);
                float shroudCurve = ctx.SideCurve * 0.36f + Mathf.Lerp(-10f, 10f, shroudNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    shroudStemBase,
                    shroudAnchor + shroudUp * (ctx.Scale.y * 0.022f) + shroudForward * (shroudLength * 0.026f),
                    shroudSocket.StipeTangentAxis,
                    shroudForward,
                    Mathf.Max(ctx.Scale.x * 0.0062f, (shroudAnchor - shroudStemBase).magnitude * 0.14f),
                    ctx.Scale.x * 0.0056f,
                    1,
                    new Color32(ctx.Spec.TintByte, 170, 60, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    shroudAnchor,
                    shroudLateral,
                    shroudUp,
                    shroudWidth,
                    shroudLength,
                    shroudTwist,
                    Mathf.Max(2, ctx.BladeSegments - 4),
                    shroudCurve,
                    ctx.Serration * 0.56f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 10, 0, 255), 206, (byte)Mathf.Lerp(72f, 176f, shroudNormalized), 255),
                    BladeProfile.BroadUndulate,
                    shroudForward,
                    ctx.Lod);
            }
        }

        private static void BuildVeilBlade(in BladeBuildContext ctx, bool frilledRibbon)
        {
            if (frilledRibbon
                && ctx.Lod == 0
                && ctx.Normalized > (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.28f : 0.38f)
                && ctx.Normalized < (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.86f : 0.78f)
                && (ctx.BladeIndex % (ctx.Spec.GrowthStyle == GrowthStyle.CrownCanopy ? 3 : 4) == 1))
            {
                float veilNormalized = Mathf.Clamp01(ctx.Normalized - 0.06f);
                float veilSweep = ctx.PrimaryAngleOffset + (((ctx.BladeIndex & 2) == 0) ? -1f : 1f) * Mathf.Lerp(10f, 18f, veilNormalized);
                BladeSocket veilSocket = EvaluateBladeSocket(ctx.Spec, ctx.Scale, veilNormalized, veilSweep, ctx.BaseOffset, ctx.ClusterYawOffsetDegrees);
                Vector3 veilLateral = veilSocket.WidthAxis;
                Vector3 veilForward = veilSocket.ForwardAxis;
                Vector3 veilUp = veilSocket.GrowthAxis;
                Vector3 veilStemBase = veilSocket.StemBase;
                Vector3 veilAnchor = veilSocket.Anchor;
                float veilWidth = ctx.Width * Mathf.Lerp(0.26f, 0.42f, 1f - veilNormalized * 0.26f);
                float veilLength = ctx.Length * Mathf.Lerp(0.46f, 0.68f, 1f - veilNormalized * 0.14f);
                float veilTwist = ctx.Twist * 1.08f + Mathf.Lerp(-12f, 18f, veilNormalized);
                float veilCurve = ctx.SideCurve * 0.42f + Mathf.Lerp(-8f, 8f, veilNormalized);

                AddBladeStem(
                    ctx.Buffers,
                    veilStemBase,
                    veilAnchor + veilUp * (ctx.Scale.y * 0.02f) + veilForward * (veilLength * 0.028f),
                    veilSocket.StipeTangentAxis,
                    veilForward,
                    Mathf.Max(ctx.Scale.x * 0.0062f, (veilAnchor - veilStemBase).magnitude * 0.14f),
                    ctx.Scale.x * 0.0054f,
                    1,
                    new Color32(ctx.Spec.TintByte, 174, 62, 255));
                AddBladeRibbon(
                    ctx.Buffers,
                    ctx.Spec,
                    veilAnchor,
                    veilLateral,
                    veilUp,
                    veilWidth,
                    veilLength,
                    veilTwist,
                    Mathf.Max(2, ctx.BladeSegments - 3),
                    veilCurve,
                    ctx.Serration * 1.12f,
                    new Color32((byte)Mathf.Clamp(ctx.Spec.TintByte + 10, 0, 255), 216, (byte)Mathf.Lerp(72f, 190f, veilNormalized), 255),
                    BladeProfile.FrilledRibbon,
                    veilForward,
                    ctx.Lod);
            }
        }

        private static void BuildBulb(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bulbIndex, int bulbCount, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
            {
                if (bulbIndex > 0)
                    return;

                StipeFrame crownFrame = EvaluateStipeFrame(spec, scale, spec.BladeAnchorHeightMax, 0f, baseOffset, clusterYawOffsetDegrees);
                Vector3 growthAxis = Vector3.Normalize(crownFrame.Tangent * 0.82f + Vector3.up * 0.18f);
                float crownRadius = scale.x * Mathf.Lerp(spec.BulbRadiusMax, spec.BulbRadiusMax * 1.28f, lod == 0 ? 0.7f : 0.4f);
                int crownLatSegments = Mathf.Max(3, 6 - lod);
                int crownLonSegments = Mathf.Max(5, 10 - (lod * 2));
                Vector3 crownBulbCenter = crownFrame.Center + growthAxis * (scale.y * 0.1f);
                AddBulbStem(
                    buffers,
                    crownFrame.Center + crownFrame.Radial * (crownFrame.Radius * 0.16f),
                    crownBulbCenter - growthAxis * (crownRadius * 0.58f),
                    Mathf.Max(crownRadius * 0.14f, crownFrame.Radius * 0.2f),
                    crownRadius * 0.1f,
                    lod,
                    new Color32(spec.TintByte, 192, 64, 255));
                AddSphere(buffers, crownBulbCenter, new Vector3(crownRadius * 1.02f, crownRadius * 1.12f, crownRadius * 1.02f), crownLatSegments, crownLonSegments, new Color32(spec.TintByte, 224, 118, 255));
                if (lod == 0)
                {
                    AddSphere(
                        buffers,
                        crownBulbCenter + crownFrame.Binormal * (crownRadius * 0.18f) + Vector3.up * (crownRadius * 0.08f),
                        new Vector3(crownRadius * 0.44f, crownRadius * 0.56f, crownRadius * 0.44f),
                        Mathf.Max(2, crownLatSegments - 2),
                        Mathf.Max(4, crownLonSegments - 3),
                        new Color32(spec.TintByte, 214, 104, 255));
                }

                return;
            }

            float t = bulbCount <= 1 ? 0.5f : bulbIndex / (float)(bulbCount - 1);
            float bladeNormalized;
            if (bulbCount <= 1)
            {
                bladeNormalized = spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.58f : 0.55f;
            }
            else if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                bladeNormalized = Mathf.Lerp(0.14f, 0.94f, Mathf.SmoothStep(0f, 1f, t));
            }
            else
            {
                bladeNormalized = Mathf.Lerp(0.22f, 0.86f, t);
            }

                float bulbAngleOffset = spec.GrowthStyle == GrowthStyle.GiantFrond
                ? EvaluateBladeAngleOffset(spec, (bulbIndex * 2) + 1, t)
                : Mathf.Lerp(-8f, 8f, t);
            BladeSocket socket = EvaluateBladeSocket(spec, scale, bladeNormalized, bulbAngleOffset, baseOffset, clusterYawOffsetDegrees);
            float radius = scale.x * Mathf.Lerp(spec.BulbRadiusMin, spec.BulbRadiusMax, 1f - t * 0.35f);
            int latSegments = Mathf.Max(2, 5 - lod);
            int lonSegments = Mathf.Max(4, 8 - (lod * 2));
            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                Vector3 stipeCenter = socket.StemBase;
                Vector3 nodeBase = Vector3.Lerp(socket.StemBase, socket.Anchor, 0.56f);
                Vector3 bulbAxis = Vector3.Normalize(socket.GrowthAxis * 0.54f + socket.WidthAxis * 0.22f + socket.ForwardAxis * 0.08f);
                Vector3 nodeBulbCenter = nodeBase + bulbAxis * (radius * 0.44f);
                AddBulbStem(
                    buffers,
                    stipeCenter,
                    nodeBulbCenter - bulbAxis * (radius * 0.48f),
                    Mathf.Max(radius * 0.12f, scale.x * 0.026f),
                    radius * 0.09f,
                    lod,
                    new Color32(spec.TintByte, 192, 64, 255));
                AddSphere(buffers, nodeBulbCenter, new Vector3(radius * 0.94f, radius * 1.22f, radius * 0.94f), latSegments, lonSegments, new Color32(spec.TintByte, 224, 118, 255));
                if (lod == 0)
                {
                    AddSphere(
                        buffers,
                        nodeBulbCenter + bulbAxis * (radius * 0.12f) + socket.GrowthAxis * (radius * 0.06f),
                        new Vector3(radius * 0.42f, radius * 0.62f, radius * 0.42f),
                        Mathf.Max(2, latSegments - 1),
                        Mathf.Max(4, lonSegments - 2),
                        new Color32(spec.TintByte, 214, 104, 255));
                }

                return;
            }

            Vector3 offsetDir = Vector3.Normalize(socket.WidthAxis * 0.84f + socket.ForwardAxis * 0.24f);
            Vector3 sideBulbCenter = socket.Anchor + offsetDir * (radius * 0.42f) + socket.GrowthAxis * (scale.y * 0.018f);
            AddBulbStem(
                buffers,
                socket.StemBase,
                sideBulbCenter - offsetDir * (radius * 0.52f),
                Mathf.Max(radius * 0.12f, scale.x * 0.026f),
                radius * 0.09f,
                lod,
                new Color32(spec.TintByte, 192, 64, 255));
            AddSphere(buffers, sideBulbCenter, new Vector3(radius * 0.92f, radius * 1.26f, radius * 0.92f), latSegments, lonSegments, new Color32(spec.TintByte, 224, 118, 255));
            if (lod == 0)
            {
                AddSphere(
                    buffers,
                    sideBulbCenter + offsetDir * (radius * 0.22f) + Vector3.up * (radius * 0.1f),
                    new Vector3(radius * 0.48f, radius * 0.72f, radius * 0.48f),
                    Mathf.Max(2, latSegments - 1),
                    Mathf.Max(4, lonSegments - 2),
                    new Color32(spec.TintByte, 214, 104, 255));
            }
        }

        private static float EvaluateBladeAngleOffset(VariantSpec spec, int bladeIndex, float sequence)
        {
            if (IsTowerLaminarVariant(spec))
            {
                float alternatingTower = (bladeIndex & 1) == 0 ? -1f : 1f;
                float stepped = Mathf.Lerp(-12f, 12f, sequence) + alternatingTower * Mathf.Lerp(2f, 8f, sequence);
                float towerNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.31f) * Mathf.Lerp(1.5f, 4.5f, sequence);
                return stepped + towerNoise;
            }

            if (IsFoldedSheetVariant(spec))
            {
                float alternatingSheet = (bladeIndex & 1) == 0 ? -1f : 1f;
                float sheetArc = Mathf.Lerp(-5f, 5f, sequence);
                float stagger = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(0.4f, 2.1f, sequence);
                float sheetNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.43f) * Mathf.Lerp(0.45f, 1.4f, sequence);
                return sheetArc + alternatingSheet * Mathf.Lerp(1.1f, 3.8f, sequence) + stagger + sheetNoise;
            }

            if (IsPaddleLobedVariant(spec))
            {
                if (IsBroadleafKelpVariant(spec))
                {
                    float alternatingBroadleaf = (bladeIndex & 1) == 0 ? -1f : 1f;
                    float broadleafArc = Mathf.Lerp(-44f, 44f, sequence);
                    float broadleafStepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(4f, 14f, sequence);
                    float broadleafNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.22f) * Mathf.Lerp(1.4f, 5.2f, sequence);
                    float broadleafBias = MathLodApproximation.ApproxSinBhaskara(sequence * Mathf.PI) * ((((bladeIndex / 3) & 1) == 0) ? 1f : -1f) * 4.8f;
                    return broadleafArc + alternatingBroadleaf * Mathf.Lerp(8f, 20f, sequence) + broadleafStepped + broadleafNoise + broadleafBias;
                }

                if (IsPaddlefanVariant(spec))
                {
                    float alternatingPaddlefan = (bladeIndex & 1) == 0 ? -1f : 1f;
                    float paddlefanArc = Mathf.Lerp(-40f, 40f, sequence);
                    float paddlefanStepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(3f, 12f, sequence);
                    float paddlefanNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.33f) * Mathf.Lerp(1.8f, 5.8f, sequence);
                    return paddlefanArc + alternatingPaddlefan * Mathf.Lerp(8f, 22f, sequence) + paddlefanStepped + paddlefanNoise;
                }

                if (IsDeepPetalVariant(spec))
                {
                    float alternatingPetal = (bladeIndex & 1) == 0 ? -1f : 1f;
                    float petalArc = Mathf.Lerp(-34f, 34f, sequence);
                    float petalStepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(2f, 10f, sequence);
                    float petalNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.26f) * Mathf.Lerp(1.8f, 5f, sequence);
                    return petalArc + alternatingPetal * Mathf.Lerp(6f, 18f, sequence) + petalStepped + petalNoise;
                }

                float alternatingPaddle = (bladeIndex & 1) == 0 ? -1f : 1f;
                float paddleArc = Mathf.Lerp(-18f, 18f, sequence);
                float stepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(1f, 6.5f, sequence);
                float paddleNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.38f) * Mathf.Lerp(1.2f, 4.2f, sequence);
                return paddleArc + alternatingPaddle * Mathf.Lerp(2f, 8f, sequence) + stepped + paddleNoise;
            }

            if (IsFrilledRibbonVariant(spec))
            {
                float alternatingFrill = (bladeIndex & 1) == 0 ? -1f : 1f;
                float frillArc = Mathf.Lerp(-24f, 24f, sequence);
                float stepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(2f, 9f, sequence);
                float frillNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.41f) * Mathf.Lerp(2.4f, 6.2f, sequence);
                return frillArc + alternatingFrill * Mathf.Lerp(4f, 11f, sequence) + stepped + frillNoise;
            }

            if (IsCanopySheetVariant(spec))
            {
                float alternatingSheet = (bladeIndex & 1) == 0 ? -1f : 1f;
                float sheetArc = Mathf.Lerp(-8f, 8f, sequence);
                float stagger = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(0.5f, 3.5f, sequence);
                float canopyNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.47f) * Mathf.Lerp(0.75f, 2.6f, sequence);
                return sheetArc + alternatingSheet * Mathf.Lerp(1.5f, 5.5f, sequence) + stagger + canopyNoise;
            }

            if (IsFoldedGiantVariant(spec))
            {
                float alternatingFolded = (bladeIndex & 1) == 0 ? -1f : 1f;
                float foldedArc = Mathf.Lerp(-7f, 7f, sequence);
                float stepped = (((bladeIndex / 2) & 1) == 0 ? -1f : 1f) * Mathf.Lerp(0.8f, 3.4f, sequence);
                float foldedNoise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.29f) * Mathf.Lerp(0.8f, 2.4f, sequence);
                return foldedArc + alternatingFolded * Mathf.Lerp(1.8f, 5.2f, sequence) + stepped + foldedNoise;
            }

            float alternating = (bladeIndex & 1) == 0 ? -1f : 1f;
            float goldenAngle = Mathf.Repeat(bladeIndex * 137.50776f, 360f);
            float centeredGolden = goldenAngle > 180f ? goldenAngle - 360f : goldenAngle;
            float noise = MathLodApproximation.ApproxSinBhaskara((bladeIndex + 1) * 1.73f) * Mathf.Lerp(4f, 14f, sequence);
            return centeredGolden * Mathf.Lerp(0.38f, 0.62f, sequence) + alternating * Mathf.Lerp(6f, 18f, sequence) + noise;
        }

        private static bool ShouldAddCompanionBlade(VariantSpec spec, int lod, int bladeIndex, float normalized)
        {
            if (lod > 1)
                return false;

            if (IsFoldedSheetVariant(spec))
            {
                if (lod == 1)
                    return normalized > 0.58f && (bladeIndex % 4 == 0);

                return normalized > 0.28f && (bladeIndex % 2 == 0);
            }

            if (IsFoldedGiantVariant(spec))
            {
                if (IsVeilwallVariant(spec))
                    return false;

                if (IsSailKelpVariant(spec))
                    return lod == 0
                        ? normalized > 0.54f && normalized < 0.74f && (bladeIndex % 5 == 0)
                        : normalized > 0.62f && normalized < 0.78f && (bladeIndex % 6 == 0);

                if (lod == 1)
                    return normalized > 0.46f && normalized < 0.86f && (bladeIndex % 4 == 1);

                return normalized > 0.22f && (bladeIndex % 2 == 1);
            }

            if (IsPaddleLobedVariant(spec))
            {
                if (IsBroadleafKelpVariant(spec))
                {
                    if (lod == 1)
                        return normalized > 0.42f && normalized < 0.9f && (bladeIndex % 5 == 0);

                    return normalized > 0.14f && normalized < 0.94f && (bladeIndex % 3 != 1);
                }

                if (IsPaddlefanVariant(spec))
                {
                    if (lod == 1)
                        return normalized > 0.48f && normalized < 0.88f && (bladeIndex % 4 == 0);

                    return normalized > 0.24f && normalized < 0.9f && (bladeIndex % 2 == 0);
                }

                if (IsDeepPetalVariant(spec))
                {
                    if (lod == 1)
                        return normalized > 0.46f && normalized < 0.84f && (bladeIndex % 4 == 0);

                    return normalized > 0.24f && normalized < 0.88f && (bladeIndex % 2 == 0);
                }

                if (spec.ClusterCount > 1)
                    return lod == 0 && normalized > 0.42f && (bladeIndex % 3 == 0);

                if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
                {
                    if (lod == 1)
                        return normalized > 0.56f && (bladeIndex % 4 == 0);

                    return normalized > 0.34f && (bladeIndex % 2 == 0);
                }

                return lod == 0 && normalized > 0.36f && (bladeIndex % 3 != 1);
            }

            if (IsFrilledRibbonVariant(spec))
            {
                if (spec.ClusterCount > 1)
                    return lod == 0 && normalized > 0.48f && (bladeIndex % 4 == 1);

                if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
                    return lod == 0 && normalized > 0.32f && (bladeIndex % 3 != 2);

                return lod == 0 && normalized > 0.38f && (bladeIndex % 3 == 1);
            }

            if (IsTowerLaminarVariant(spec))
                return lod == 0 && normalized > 0.38f && (bladeIndex % 3 == 1);

            if (IsCanopySheetVariant(spec))
            {
                if (lod == 1)
                    return normalized > 0.66f && (bladeIndex % 5 == 1);

                return normalized > 0.56f && (bladeIndex % 3 == 1);
            }

            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                if (spec.ClusterCount > 1)
                    return lod == 0 || normalized > 0.34f || (bladeIndex % 2 == 0);

                return lod == 0 || normalized > 0.22f || (bladeIndex % 2 == 0);
            }

            return bladeIndex % 2 == 0 || normalized > 0.55f;
        }

        private static bool ShouldAddTertiaryBlade(VariantSpec spec, int lod, int bladeIndex, float normalized)
        {
            if (lod > 0 || spec.GrowthStyle != GrowthStyle.GiantFrond)
                return false;

            if (IsFoldedGiantVariant(spec))
                return IsSailKelpVariant(spec)
                    ? false
                    : IsVeilwallVariant(spec)
                    ? false
                    : normalized > 0.4f && (bladeIndex % 3 == 0);

            if (IsPaddleLobedVariant(spec))
            {
                if (IsBroadleafKelpVariant(spec))
                    return normalized > 0.34f && (bladeIndex % 3 == 0);

                if (IsPaddlefanVariant(spec))
                    return normalized > 0.4f && (bladeIndex % 3 == 1);

                if (IsDeepPetalVariant(spec))
                    return normalized > 0.38f && (bladeIndex % 3 == 0);

                if (spec.ClusterCount > 1)
                    return false;

                return normalized > 0.52f && (bladeIndex % 4 == 0);
            }

            if (IsFrilledRibbonVariant(spec))
            {
                if (spec.ClusterCount > 1)
                    return false;

                return normalized > 0.56f && (bladeIndex % 5 == 0);
            }

            if (IsTowerLaminarVariant(spec))
                return normalized > 0.54f && (bladeIndex % 4 == 0);

            return spec.ClusterCount > 1
                ? normalized > 0.46f && (bladeIndex % 4 == 0)
                : normalized > 0.18f && (bladeIndex % 4 != 2);
        }

        private static int ResolveSupplementalStemLod(VariantSpec spec, int lod)
        {
            return spec.ClusterCount > 1
                ? Mathf.Min(lod + 1, 2)
                : lod;
        }

        private static int ResolveUnderstoryBladeSegments(VariantSpec spec, int bladeSegments)
        {
            return spec.ClusterCount > 1
                ? Mathf.Max(2, bladeSegments - 4)
                : Mathf.Max(2, bladeSegments - 2);
        }

        private static int ResolveCompanionBladeSegments(VariantSpec spec, int bladeSegments)
        {
            return spec.ClusterCount > 1
                ? Mathf.Max(2, bladeSegments - 4)
                : Mathf.Max(2, bladeSegments - 1);
        }

        private static int ResolveTertiaryBladeSegments(VariantSpec spec, int bladeSegments)
        {
            return spec.ClusterCount > 1
                ? Mathf.Max(2, bladeSegments - 5)
                : Mathf.Max(2, bladeSegments - 2);
        }

        private static BladeProfile ResolveBladeProfile(VariantSpec spec, int bladeIndex, float normalized, bool supplemental)
        {
            if (IsTowerLaminarVariant(spec)
                || IsCanopySheetVariant(spec)
                || spec.BladeProfile == BladeProfile.FoldedLamina
                || spec.BladeProfile == BladeProfile.PaddleLobed
                || spec.BladeProfile == BladeProfile.FrilledRibbon)
                return spec.BladeProfile;

            if (spec.GrowthStyle != GrowthStyle.GiantFrond || spec.ClusterCount <= 1)
                return spec.BladeProfile;

            float pattern = Mathf.Repeat(bladeIndex + (supplemental ? 1.5f : 0f), 4f);
            if (pattern < 1f)
                return BladeProfile.BroadUndulate;

            if (pattern < 2f)
                return normalized > 0.58f ? BladeProfile.SplitRibbon : BladeProfile.BroadUndulate;

            if (pattern < 3f)
                return normalized > 0.46f ? BladeProfile.NarrowStrap : BladeProfile.BroadUndulate;

            return normalized < 0.34f ? BladeProfile.BroadUndulate : BladeProfile.SplitRibbon;
        }

        private static BladeSocket EvaluateBladeSocket(VariantSpec spec, Vector3 scale, float normalized, float angleOffsetDegrees, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            bool towerLaminar = IsTowerLaminarVariant(spec);
            bool canopySheet = IsCanopySheetVariant(spec);
            bool broadleafKelp = IsBroadleafKelpVariant(spec);
            bool sailKelp = IsSailKelpVariant(spec);
            bool paddlefanKelp = IsPaddlefanVariant(spec);
            bool deepPetal = IsDeepPetalVariant(spec);
            float angle = spec.BladeStartYaw + normalized * spec.BladeYawArc + MathLodApproximation.ApproxSinBhaskara((normalized + 0.13f) * Mathf.PI * 3.1f) * 7f + angleOffsetDegrees;

            if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
            {
                float anchorHeight = Mathf.Lerp(spec.BladeAnchorHeightMin, spec.BladeAnchorHeightMax, Mathf.Lerp(0.72f, 1f, normalized));
                StipeFrame crownFrame = EvaluateStipeFrame(spec, scale, anchorHeight, angle, baseOffset, clusterYawOffsetDegrees);
                Vector3 crownCenter = crownFrame.Center + Vector3.Normalize(crownFrame.Tangent * 0.82f + Vector3.up * 0.18f) * (scale.y * 0.08f);
                Vector3 widthAxis = crownFrame.Radial;
                Vector3 growthAxis = canopySheet
                    ? Vector3.Normalize(widthAxis * 0.52f + crownFrame.Tangent * 0.2f + Vector3.up * 0.28f)
                    : Vector3.Normalize(widthAxis * 0.46f + crownFrame.Tangent * 0.34f + Vector3.up * 0.2f);
                if (paddlefanKelp)
                    growthAxis = Vector3.Normalize(widthAxis * 0.58f + crownFrame.Tangent * 0.24f + Vector3.up * 0.18f);
                Vector3 forwardAxis = Vector3.Cross(widthAxis, growthAxis).normalized;
                Vector3 stemBase = crownCenter - widthAxis * (scale.x * (canopySheet ? 0.032f : 0.06f)) - growthAxis * (scale.y * (canopySheet ? 0.016f : 0.03f));
                Vector3 anchor = crownCenter + widthAxis * (scale.x * (canopySheet ? 0.038f : 0.08f));
                if (paddlefanKelp)
                {
                    stemBase = crownCenter - widthAxis * (scale.x * 0.072f) - growthAxis * (scale.y * 0.034f);
                    anchor = crownCenter + widthAxis * (scale.x * 0.094f) - forwardAxis * (scale.x * 0.012f);
                }
                return new BladeSocket(stemBase, anchor, widthAxis, growthAxis, forwardAxis, crownFrame.Tangent);
            }

            float anchorDistribution;
            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                float lowerSpread = broadleafKelp
                    ? Mathf.Lerp(normalized, MathLodApproximation.ApproxPow01Curve(normalized, 0.78f), 0.42f)
                    : spec.ClusterCount > 1
                    ? Mathf.Lerp(normalized, MathLodApproximation.ApproxPow01Curve(normalized, 0.82f), 0.34f)
                    : Mathf.Lerp(normalized, MathLodApproximation.ApproxPow01Curve(normalized, 0.84f), 0.28f);
                float nodeRhythm = MathLodApproximation.ApproxSinBhaskara((normalized * 3.7f + spec.BendDegrees * 0.015f) * Mathf.PI) * 0.032f;
                float midMassBias = broadleafKelp
                    ? MathLodApproximation.ApproxSinBhaskara(normalized * Mathf.PI) * 0.072f
                    : spec.ClusterCount > 1
                    ? MathLodApproximation.ApproxSinBhaskara(normalized * Mathf.PI) * 0.06f
                    : MathLodApproximation.ApproxSinBhaskara(normalized * Mathf.PI) * 0.035f;
                anchorDistribution = broadleafKelp
                    ? Mathf.Clamp01(Mathf.Lerp(0.05f, 0.84f, lowerSpread) + midMassBias + nodeRhythm * 0.32f)
                    : spec.ClusterCount > 1
                    ? Mathf.Clamp01(Mathf.Lerp(0.04f, 0.76f, lowerSpread) + midMassBias + nodeRhythm * 0.35f)
                    : Mathf.Clamp01(Mathf.Lerp(0.06f, 0.9f, lowerSpread) + midMassBias + nodeRhythm * 0.18f);
            }
            else
            {
                anchorDistribution = Mathf.Lerp(normalized, 1f - MathLodApproximation.ApproxPow01Curve(1f - normalized, 1.85f), 0.72f);
            }

            float anchorHeightAlongStipe = Mathf.Lerp(spec.BladeAnchorHeightMin, spec.BladeAnchorHeightMax, anchorDistribution);
            StipeFrame frame = EvaluateStipeFrame(spec, scale, anchorHeightAlongStipe, angle, baseOffset, clusterYawOffsetDegrees);
            float helicalSweep = MathLodApproximation.ApproxSinBhaskara((normalized * 2.7f + spec.BendDegrees * 0.01f) * Mathf.PI) * (towerLaminar ? 8f : 16f);
            Quaternion sweepRotation = Quaternion.AngleAxis(helicalSweep, frame.Tangent);
            Vector3 width = (sweepRotation * frame.Radial).normalized;
            Vector3 growth = spec.GrowthStyle == GrowthStyle.GiantFrond
                ? towerLaminar
                    ? Vector3.Normalize(frame.Tangent * 0.76f + Vector3.up * 0.18f + width * 0.08f)
                    : broadleafKelp
                    ? Vector3.Normalize(frame.Tangent * 0.34f + Vector3.up * 0.14f + width * 0.52f)
                    : sailKelp
                    ? Vector3.Normalize(frame.Tangent * 0.46f + Vector3.up * 0.12f + width * 0.42f)
                    : deepPetal
                    ? Vector3.Normalize(frame.Tangent * 0.4f + Vector3.up * 0.12f + width * 0.48f)
                    : spec.ClusterCount > 1
                    ? Vector3.Normalize(frame.Tangent * 0.62f + Vector3.up * 0.26f + width * 0.18f)
                    : Vector3.Normalize(frame.Tangent * 0.58f + Vector3.up * 0.18f + width * 0.24f)
                : Vector3.Normalize(frame.Tangent * 0.66f + Vector3.up * 0.24f + width * 0.1f);
            Vector3 forward = Vector3.Cross(width, growth).normalized;
            float sheathT = spec.GrowthStyle == GrowthStyle.GiantFrond
                ? spec.ClusterCount > 1
                    ? Mathf.Lerp(0.34f, 0.54f, anchorDistribution)
                    : Mathf.Lerp(0.48f, 0.72f, anchorDistribution)
                : Mathf.Lerp(0.58f, 0.82f, anchorDistribution);
            Vector3 sheathBase = frame.Center
                + width * (frame.Radius * sheathT)
                - frame.Tangent * (scale.y * 0.026f)
                - forward * (scale.x * 0.012f);
            Vector3 stemBaseAlongStipe = Vector3.Lerp(
                frame.Center + width * (frame.Radius * 0.42f),
                sheathBase,
                spec.GrowthStyle == GrowthStyle.GiantFrond
                    ? (spec.ClusterCount > 1 ? 0.78f : 0.9f)
                    : 0.78f);
            Vector3 anchorAlongStipe = frame.Center
                + width * (frame.Radius * (spec.GrowthStyle == GrowthStyle.GiantFrond
                    ? towerLaminar
                        ? 0.8f
                        : broadleafKelp
                        ? 1.08f
                        : sailKelp
                        ? 1.02f
                        : deepPetal
                        ? 1f
                        : (spec.ClusterCount > 1 ? 0.72f : 0.88f)
                    : 0.94f))
                + frame.Tangent * (scale.y * (spec.GrowthStyle == GrowthStyle.GiantFrond
                    ? towerLaminar
                        ? 0.008f
                        : broadleafKelp
                        ? 0.006f
                        : sailKelp
                        ? 0.01f
                        : deepPetal
                        ? 0.008f
                        : (spec.ClusterCount > 1 ? 0.003f : 0.006f)
                    : 0.012f))
                + forward * (scale.x * (spec.ClusterCount > 1 ? 0.008f : broadleafKelp ? 0.048f : sailKelp ? 0.03f : deepPetal ? 0.028f : 0.014f));
            return new BladeSocket(stemBaseAlongStipe, anchorAlongStipe, width, growth, forward, frame.Tangent);
        }

        private static bool IsTowerLaminarVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.ClusterCount == 1
                && IsLaminarSheetProfile(spec.BladeProfile)
                && spec.BladeWidthMax >= 0.22f
                && spec.BladeLengthMax >= 0.9f;
        }

        private static bool IsCanopySheetVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.CrownCanopy
                && IsLaminarSheetProfile(spec.BladeProfile)
                && spec.BladeWidthMax >= 0.34f;
        }

        private static bool IsFoldedSheetVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.CrownCanopy
                && spec.BladeProfile == BladeProfile.FoldedLamina
                && spec.BladeWidthMax >= 0.4f;
        }

        private static bool IsFoldedGiantVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.BladeProfile == BladeProfile.FoldedLamina
                && spec.BladeWidthMax >= 0.26f;
        }

        private static bool IsPaddleLobedVariant(VariantSpec spec)
        {
            return spec.BladeProfile == BladeProfile.PaddleLobed
                && spec.BladeWidthMax >= 0.2f;
        }

        private static bool IsBroadleafKelpVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.BladeProfile == BladeProfile.PaddleLobed
                && spec.ClusterCount <= 2
                && spec.BladeWidthMax >= 1f
                && spec.BladeYawArc <= 110f;
        }

        private static bool IsSailKelpVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.BladeProfile == BladeProfile.FoldedLamina
                && spec.ClusterCount == 2
                && spec.BladeWidthMax >= 1f
                && spec.BladeYawArc >= 40f
                && spec.BladeYawArc <= 80f;
        }

        private static bool IsPaddlefanVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.CrownCanopy
                && spec.BladeProfile == BladeProfile.PaddleLobed
                && spec.BladeWidthMax >= 0.9f
                && spec.BladeYawArc >= 140f;
        }

        private static bool IsVeilwallVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.BladeProfile == BladeProfile.FoldedLamina
                && spec.ClusterCount == 2
                && spec.BladeWidthMax >= 0.95f
                && spec.BladeLengthMax >= 1.15f
                && spec.BladeYawArc <= 24f;
        }

        private static bool IsFrilledRibbonVariant(VariantSpec spec)
        {
            return spec.BladeProfile == BladeProfile.FrilledRibbon
                && spec.BladeLengthMax >= 0.7f;
        }

        private static bool IsDeepPetalVariant(VariantSpec spec)
        {
            return spec.GrowthStyle == GrowthStyle.GiantFrond
                && spec.BladeProfile == BladeProfile.PaddleLobed
                && !IsBroadleafKelpVariant(spec)
                && spec.BladeYawArc >= 110f
                && spec.BladeWidthMax >= 0.6f;
        }

        private static bool IsLaminarSheetProfile(BladeProfile bladeProfile)
        {
            return bladeProfile == BladeProfile.BroadUndulate
                || bladeProfile == BladeProfile.FoldedLamina;
        }

        private static StipeFrame EvaluateStipeFrame(VariantSpec spec, Vector3 scale, float height01, float yawDegrees, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            float v = Mathf.Clamp01(height01 / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            Vector3 center = EvaluateStipeCenter(spec, scale, v, baseOffset, clusterYawOffsetDegrees);
            float sampleDelta = 0.018f;
            float prevV = Mathf.Max(0f, v - sampleDelta);
            float nextV = Mathf.Min(1f, v + sampleDelta);
            Vector3 prevCenter = EvaluateStipeCenter(spec, scale, prevV, baseOffset, clusterYawOffsetDegrees);
            Vector3 nextCenter = EvaluateStipeCenter(spec, scale, nextV, baseOffset, clusterYawOffsetDegrees);
            Vector3 tangent = (nextCenter - prevCenter).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.up;

            Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.94f ? Vector3.forward : Vector3.up;
            Vector3 baseNormal = Vector3.Cross(reference, tangent).normalized;
            if (baseNormal.sqrMagnitude < 0.0001f)
                baseNormal = Vector3.right;

            Quaternion aroundTangent = Quaternion.AngleAxis(yawDegrees + clusterYawOffsetDegrees, tangent);
            Vector3 radial = (aroundTangent * baseNormal).normalized;
            Vector3 binormal = Vector3.Cross(tangent, radial).normalized;
            float radius = EvaluateStipeRadius(spec, scale, v);
            return new StipeFrame(center, tangent, radial, binormal, radius);
        }

        private static Vector3 EvaluateStipeCenter(VariantSpec spec, Vector3 scale, float v, Vector3 baseOffset, float clusterYawOffsetDegrees)
        {
            float height = scale.y * spec.StipeHeightMultiplier;
            float bendRadians = spec.BendDegrees * Mathf.Deg2Rad * v * v;
            float wobbleX = MathLodApproximation.ApproxSinBhaskara((v * 2.6f + spec.BendDegrees * 0.02f) * Mathf.PI) * scale.x * 0.03f;
            float wobbleZ = MathLodApproximation.ApproxSinBhaskara((v * 4.3f + spec.RibCount * 0.11f) * Mathf.PI) * scale.z * 0.018f;
            Vector3 local = new Vector3(
                MathLodApproximation.ApproxSinBhaskara(bendRadians) * scale.x * spec.BendRadiusMultiplier + wobbleX,
                v * height,
                MathLodApproximation.ApproxCosBhaskara(bendRadians) * scale.z * spec.ForwardOffsetMultiplier - scale.z * spec.ForwardOffsetMultiplier + wobbleZ);
            Quaternion clusterRotation = Quaternion.Euler(0f, clusterYawOffsetDegrees, 0f);
            return baseOffset + clusterRotation * local;
        }

        private static Vector3 EvaluateClusterOffset(VariantSpec spec, Vector3 scale, int clusterIndex, int clusterCount)
        {
            if (clusterCount <= 1)
                return Vector3.zero;

            float angle = (clusterIndex / (float)clusterCount) * TwoPi + spec.RootYawOffset;
            float radius = scale.x * spec.ClusterSpread * Mathf.Lerp(0.82f, 1.08f, MathLodApproximation.ApproxSinBhaskara((clusterIndex + 1) * 1.23f) * 0.5f + 0.5f);
            return new Vector3(MathLodApproximation.ApproxCosBhaskara(angle) * radius, 0f, MathLodApproximation.ApproxSinBhaskara(angle) * radius);
        }

        private static float EvaluateClusterYawOffset(VariantSpec spec, int clusterIndex, int clusterCount)
        {
            if (clusterCount <= 1)
                return 0f;

            return (360f / clusterCount) * clusterIndex + MathLodApproximation.ApproxSinBhaskara((clusterIndex + 1) * 1.31f) * 10f;
        }

        private static float EvaluateClusterScaleFactor(VariantSpec spec, int clusterIndex, int clusterCount)
        {
            if (clusterCount <= 1)
                return 1f;

            return Mathf.Lerp(0.68f, 0.9f, MathLodApproximation.ApproxSinBhaskara((clusterIndex + 1) * 0.91f) * 0.5f + 0.5f);
        }

        private static float EvaluateStipeRadius(VariantSpec spec, Vector3 scale, float v)
        {
            float bottomRadius = Mathf.Max(0.02f, scale.x * spec.BaseRadiusMultiplier);
            float topRadius = Mathf.Max(bottomRadius * 0.42f, scale.x * spec.TopRadiusMultiplier);
            float bladeBandMin = Mathf.Clamp01(spec.BladeAnchorHeightMin / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bladeBandMax = Mathf.Clamp01(spec.BladeAnchorHeightMax / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bulbBandMin = Mathf.Clamp01(spec.BulbHeightMin / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bulbBandMax = Mathf.Clamp01(spec.BulbHeightMax / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            float bladeBand = EvaluateBand(v, bladeBandMin, bladeBandMax, 0.085f);
            float bulbBand = EvaluateBand(v, bulbBandMin, bulbBandMax, 0.07f);
            float nodeBulge = bladeBand * 0.22f + bulbBand * 0.14f;
            float scarNoise = MathLodApproximation.ApproxSinBhaskara((v * 8.5f + spec.BendDegrees * 0.03f) * Mathf.PI) * 0.035f;
            return Mathf.Lerp(bottomRadius, topRadius, v) * (1f + nodeBulge + scarNoise);
        }

        private static void AddBladeStem(MeshBuffers buffers, Vector3 start, Vector3 end, Vector3 stipeTangent, Vector3 forwardAxis, float startRadius, float endRadius, int lod, Color32 color)
        {
            float length = (end - start).magnitude;
            if (length <= 0.0001f)
                return;

            Vector3 tangentAxis = stipeTangent.sqrMagnitude > 0.0001f ? stipeTangent.normalized : Vector3.up;
            Vector3 forward = forwardAxis.sqrMagnitude > 0.0001f ? forwardAxis.normalized : Vector3.forward;
            Vector3 spanDir = (end - start).normalized;
            Vector3 surfaceNormal = Vector3.Cross(forward, tangentAxis).normalized;
            if (surfaceNormal.sqrMagnitude < 0.0001f)
                surfaceNormal = Vector3.up;

            Vector3 startTangent = Vector3.Normalize(tangentAxis * 0.78f + forward * 0.14f + spanDir * 0.08f);
            Vector3 endTangent = Vector3.Normalize(spanDir * 0.74f + forward * 0.18f + tangentAxis * 0.08f);
            Vector3 archLift = surfaceNormal * (length * 0.045f);
            AddTube(
                buffers,
                start,
                start + startTangent * (length * 0.22f) + archLift,
                end - endTangent * (length * 0.22f) + archLift * 0.65f,
                end,
                startRadius,
                endRadius,
                Mathf.Max(2, 3 - lod),
                Mathf.Max(3, 5 - lod),
                color,
                0.08f);
        }

        private static void AddBulbStem(MeshBuffers buffers, Vector3 start, Vector3 end, float startRadius, float endRadius, int lod, Color32 color)
        {
            Vector3 lateral = Vector3.Cross((end - start).normalized, Vector3.up);
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.right;

            lateral.Normalize();
            AddTube(
                buffers,
                start,
                Vector3.Lerp(start, end, 0.3f) + lateral * (startRadius * 1.4f),
                Vector3.Lerp(start, end, 0.68f) + lateral * (startRadius * 0.9f),
                end,
                startRadius,
                endRadius,
                Mathf.Max(2, 3 - lod),
                Mathf.Max(3, 5 - lod),
                color,
                0.06f);
        }

        private static void AddTube(MeshBuffers buffers, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float radiusStart, float radiusEnd, int pathSegments, int radialSegments, Color32 color, float ribAmplitude)
        {
            Vector3 prevNormal = Vector3.up;
            int startIndex = buffers.Vertices.Count;

            for (int pathIndex = 0; pathIndex <= pathSegments; pathIndex++)
            {
                float t = pathIndex / (float)pathSegments;
                Vector3 center = EvaluateBezier(p0, p1, p2, p3, t);
                Vector3 tangent = EvaluateBezierTangent(p0, p1, p2, p3, t).normalized;
                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = Vector3.up;

                Vector3 binormal = Vector3.Cross(prevNormal, tangent);
                if (binormal.sqrMagnitude < 0.0001f)
                {
                    binormal = Vector3.Cross(Vector3.right, tangent);
                    if (binormal.sqrMagnitude < 0.0001f)
                        binormal = Vector3.Cross(Vector3.forward, tangent);
                }

                binormal.Normalize();
                Vector3 normalAxis = Vector3.Cross(tangent, binormal).normalized;
                prevNormal = normalAxis;
                float radius = Mathf.Lerp(radiusStart, radiusEnd, t);

                for (int radialIndex = 0; radialIndex <= radialSegments; radialIndex++)
                {
                    float u = radialIndex / (float)radialSegments;
                    float angle = u * TwoPi;
                    float rib = 1f + MathLodApproximation.ApproxSinBhaskara(angle * 3f + t * 5.7f) * ribAmplitude;
                    float actualRadius = radius * rib;
                    Vector3 radial = (normalAxis * MathLodApproximation.ApproxCosBhaskara(angle) + binormal * MathLodApproximation.ApproxSinBhaskara(angle)).normalized;
                    Vector3 vertex = center + radial * actualRadius;
                    Vector4 tangent4 = new Vector4(binormal.x, binormal.y, binormal.z, 1f);
                    buffers.AddVertex(vertex, radial, tangent4, new Vector2(u, t), color);
                }
            }

            int rowSize = radialSegments + 1;
            for (int pathIndex = 0; pathIndex < pathSegments; pathIndex++)
            {
                int rowStart = startIndex + pathIndex * rowSize;
                int nextRowStart = rowStart + rowSize;
                for (int radialIndex = 0; radialIndex < radialSegments; radialIndex++)
                    buffers.AddQuad(rowStart + radialIndex, nextRowStart + radialIndex, nextRowStart + radialIndex + 1, rowStart + radialIndex + 1);
            }
        }

        private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0
                + 3f * oneMinusT * oneMinusT * t * p1
                + 3f * oneMinusT * t * t * p2
                + t * t * t * p3;
        }

        private static Vector3 EvaluateBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * (p1 - p0)
                + 6f * oneMinusT * t * (p2 - p1)
                + 3f * t * t * (p3 - p2);
        }

        private static void AddRibbon(MeshBuffers buffers, Vector3 anchor, Vector3 widthAxis, Vector3 upAxis, float width, float length, float twistDegrees, int segments, float sideCurveDegrees, float serration, Color32 color, Vector3? forwardHint = null)
        {
            Vector3 widthDir = widthAxis.sqrMagnitude > 0f ? widthAxis.normalized : Vector3.right;
            Vector3 upDir = upAxis.sqrMagnitude > 0f ? upAxis.normalized : Vector3.up;
            Vector3 forwardDir = forwardHint.HasValue && forwardHint.Value.sqrMagnitude > 0f
                ? forwardHint.Value.normalized
                : Vector3.Cross(widthDir, upDir).normalized;
            int startIndex = buffers.Vertices.Count;
            float anchorNoise = MathLodApproximation.ApproxSinBhaskara((anchor.x + anchor.z + length) * 9.7f);
            float asymmetry = anchorNoise * 0.16f;
            float forwardBow = length * Mathf.Lerp(0.06f, 0.12f, Mathf.Abs(anchorNoise));
            float droop = length * Mathf.Lerp(0.04f, 0.1f, Mathf.Abs(anchorNoise));
            float centerLift = width * Mathf.Lerp(0.04f, 0.11f, Mathf.Abs(anchorNoise));

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float twist = Mathf.Lerp(0f, twistDegrees, t);
                Quaternion rotation = Quaternion.AngleAxis(twist, upDir) * Quaternion.AngleAxis(sideCurveDegrees * t, forwardDir);
                Vector3 rotatedWidth = rotation * widthDir;
                float widthTaper = Mathf.Lerp(1.04f, 0.08f, MathLodApproximation.ApproxPow01Curve(t, 0.72f));
                float halfWidth = width * widthTaper;
                float edgeWave = serration * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 7f + anchorNoise * 2.4f);
                float edgeWaveSecondary = serration * 0.65f * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 11f + anchorNoise * 1.7f);
                float splitMask = Mathf.Clamp01((t - 0.82f) / 0.18f);
                float tipSplit = halfWidth * splitMask * 0.32f;
                float lateralAsymmetry = halfWidth * asymmetry * Mathf.Lerp(0.35f, 1f, t);
                float curl = centerLift * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI) * Mathf.Lerp(0.8f, 0.2f, t);

                Vector3 center = anchor
                    + upDir * (length * t - droop * t * t)
                    + forwardDir * (MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI) * forwardBow + MathLodApproximation.ApproxSinBhaskara((t + 0.17f) * Mathf.PI * 2.0f) * length * 0.015f);

                Vector3 normal = Vector3.Cross(rotatedWidth, upDir).normalized;
                if (normal.sqrMagnitude < 0.0001f)
                    normal = Vector3.Cross(rotatedWidth, forwardDir).normalized;

                Vector3 left = center
                    - rotatedWidth * (halfWidth + edgeWave + tipSplit + lateralAsymmetry)
                    - normal * (curl + edgeWaveSecondary);
                Vector3 mid = center + normal * (curl * 0.65f) - upDir * (splitMask * length * 0.035f);
                Vector3 right = center
                    + rotatedWidth * (halfWidth - edgeWave + tipSplit - lateralAsymmetry)
                    + normal * (curl * 0.85f + edgeWaveSecondary);

                Vector4 tangent = new Vector4(rotatedWidth.x, rotatedWidth.y, rotatedWidth.z, 1f);
                byte edgeGreen = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * Mathf.Lerp(0.88f, 0.76f, t)), 0, 255);
                byte edgeBlue = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * Mathf.Lerp(0.74f, 0.48f, t)), 0, 255);
                byte midGreen = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * Mathf.Lerp(1.04f, 1.16f, 1f - t)), 0, 255);
                byte midBlue = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * Mathf.Lerp(0.92f, 0.72f, t)), 0, 255);
                Color32 edgeColor = new Color32(color.r, edgeGreen, edgeBlue, color.a);
                Color32 midColor = new Color32((byte)Mathf.Clamp(color.r + 6, 0, 255), midGreen, midBlue, color.a);

                buffers.AddVertex(left, normal, tangent, new Vector2(0f, t), edgeColor);
                buffers.AddVertex(mid, normal, tangent, new Vector2(0.5f, t), midColor);
                buffers.AddVertex(right, normal, tangent, new Vector2(1f, t), edgeColor);
            }

            for (int i = 0; i < segments; i++)
            {
                int row = startIndex + i * 3;
                int nextRow = row + 3;
                buffers.AddQuad(row, nextRow, nextRow + 1, row + 1);
                buffers.AddQuad(row + 1, nextRow + 1, nextRow + 2, row + 2);
            }
        }

        private static void AddBladeRibbon(MeshBuffers buffers, VariantSpec spec, Vector3 anchor, Vector3 widthAxis, Vector3 upAxis, float width, float length, float twistDegrees, int segments, float sideCurveDegrees, float serration, Color32 color, BladeProfile bladeProfile, Vector3? forwardHint = null, int lodLevel = 0)
        {
            Vector3 widthDir = widthAxis.sqrMagnitude > 0f ? widthAxis.normalized : Vector3.right;
            Vector3 upDir = upAxis.sqrMagnitude > 0f ? upAxis.normalized : Vector3.up;
            Vector3 forwardDir = forwardHint.HasValue && forwardHint.Value.sqrMagnitude > 0f
                ? forwardHint.Value.normalized
                : Vector3.Cross(widthDir, upDir).normalized;
            int startIndex = buffers.Vertices.Count;
            float anchorNoise = MathLodApproximation.ApproxSinBhaskara((anchor.x * 0.73f + anchor.z * 1.11f + length) * 8.4f);
            float asymmetry = anchorNoise * 0.14f;
            float profileMidWidthBoost;
            float profileWaveBoost;
            float profileTipSplitBoost;
            float profileBowBoost;
            float profileDroopBoost;
            float profileCurlBoost;
            float profileBaseWrapScale;
            float profileCenterFoldScale;
            float profileInnerFoldScale;

            switch (bladeProfile)
            {
                case BladeProfile.BroadUndulate:
                    profileMidWidthBoost = 1.32f;
                    profileWaveBoost = 1.55f;
                    profileTipSplitBoost = 0.74f;
                    profileBowBoost = 1.18f;
                    profileDroopBoost = 0.94f;
                    profileCurlBoost = 1.3f;
                    profileBaseWrapScale = 0.52f;
                    profileCenterFoldScale = 0.14f;
                    profileInnerFoldScale = 0.08f;
                    break;
                case BladeProfile.SplitRibbon:
                    profileMidWidthBoost = 1.12f;
                    profileWaveBoost = 1.18f;
                    profileTipSplitBoost = 1.42f;
                    profileBowBoost = 1.06f;
                    profileDroopBoost = 1.04f;
                    profileCurlBoost = 1.08f;
                    profileBaseWrapScale = 0.7f;
                    profileCenterFoldScale = 0.06f;
                    profileInnerFoldScale = 0.04f;
                    break;
                case BladeProfile.FoldedLamina:
                    profileMidWidthBoost = 1.22f;
                    profileWaveBoost = 0.82f;
                    profileTipSplitBoost = 0.46f;
                    profileBowBoost = 1.08f;
                    profileDroopBoost = 0.98f;
                    profileCurlBoost = 1.48f;
                    profileBaseWrapScale = 0.42f;
                    profileCenterFoldScale = 0.32f;
                    profileInnerFoldScale = 0.2f;
                    break;
                case BladeProfile.PaddleLobed:
                    profileMidWidthBoost = 1.42f;
                    profileWaveBoost = 1.26f;
                    profileTipSplitBoost = 0.28f;
                    profileBowBoost = 1.02f;
                    profileDroopBoost = 0.9f;
                    profileCurlBoost = 1.18f;
                    profileBaseWrapScale = 0.48f;
                    profileCenterFoldScale = 0.08f;
                    profileInnerFoldScale = 0.04f;
                    break;
                case BladeProfile.FrilledRibbon:
                    profileMidWidthBoost = 1.1f;
                    profileWaveBoost = 2.14f;
                    profileTipSplitBoost = 1.36f;
                    profileBowBoost = 1.14f;
                    profileDroopBoost = 1.08f;
                    profileCurlBoost = 1.02f;
                    profileBaseWrapScale = 0.68f;
                    profileCenterFoldScale = 0.05f;
                    profileInnerFoldScale = 0.03f;
                    break;
                default:
                    profileMidWidthBoost = 1f;
                    profileWaveBoost = 1f;
                    profileTipSplitBoost = 1f;
                    profileBowBoost = 1f;
                    profileDroopBoost = 1f;
                    profileCurlBoost = 1f;
                    profileBaseWrapScale = 1f;
                    profileCenterFoldScale = 0f;
                    profileInnerFoldScale = 0f;
                    break;
            }

            float forwardBow = length * Mathf.Lerp(0.09f, 0.18f, Mathf.Abs(anchorNoise)) * profileBowBoost;
            float droop = length * Mathf.Lerp(0.08f, 0.18f, Mathf.Abs(anchorNoise)) * profileDroopBoost;
            float centerLift = width * Mathf.Lerp(0.06f, 0.13f, Mathf.Abs(anchorNoise)) * profileCurlBoost;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                bool paddleLobed = bladeProfile == BladeProfile.PaddleLobed;
                bool frilledRibbon = bladeProfile == BladeProfile.FrilledRibbon;
                float baseMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.04f) / 0.18f));
                float tipMask = Mathf.Clamp01((t - 0.78f) / 0.22f);
                float twist = Mathf.Lerp(0f, twistDegrees, t);
                Quaternion rotation = Quaternion.AngleAxis(twist, upDir) * Quaternion.AngleAxis(sideCurveDegrees * t, forwardDir);
                Vector3 rotatedWidth = rotation * widthDir;
                float midLamina = MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI);
                float taperPower = bladeProfile == BladeProfile.BroadUndulate
                    ? 0.82f
                    : paddleLobed ? 0.96f : frilledRibbon ? 0.78f : 0.72f;
                float widthBoostPower = bladeProfile == BladeProfile.BroadUndulate
                    ? 0.72f
                    : paddleLobed ? 0.42f : frilledRibbon ? 0.94f : 1.2f;
                float widthTaper = Mathf.Lerp(1.02f, paddleLobed ? 0.18f : frilledRibbon ? 0.12f : 0.06f, MathLodApproximation.ApproxPow01Curve(t, taperPower));
                widthTaper *= Mathf.Lerp(1f, profileMidWidthBoost, MathLodApproximation.ApproxPow01Curve(midLamina, widthBoostPower));
                float baseNarrow = Mathf.Lerp(0.16f, 1f, baseMask);
                float halfWidth = width * widthTaper * baseNarrow;
                float innerWidth = halfWidth * Mathf.Lerp(0.38f, 0.46f, 1f - tipMask);
                float edgeWave = serration * profileWaveBoost * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 8.2f + anchorNoise * 2.6f) * baseMask;
                float edgeWaveSecondary = serration * 0.55f * profileWaveBoost * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 12.5f + anchorNoise * 1.9f) * baseMask;
                float frillSlice = frilledRibbon ? MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 13.4f + anchorNoise * 2.1f) * halfWidth * 0.08f * baseMask : 0f;
                float laminaLobes = bladeProfile == BladeProfile.BroadUndulate
                    ? MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 3.2f + anchorNoise * 1.35f) * halfWidth * 0.12f * baseMask
                    : 0f;
                float foldMask = MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI) * baseMask;
                float centerFold = halfWidth * profileCenterFoldScale * foldMask;
                float innerFold = halfWidth * profileInnerFoldScale * foldMask;
                float tipSplit = halfWidth * tipMask * 0.34f * profileTipSplitBoost;
                float lateralAsymmetry = halfWidth * asymmetry * Mathf.Lerp(0.25f, 1f, t);
                float curl = centerLift * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI) * Mathf.Lerp(0.9f, 0.24f, t);
                float baseWrap = (1f - baseMask) * width * 0.22f * profileBaseWrapScale;
                float lobePulse = paddleLobed
                    ? (0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 4.4f + anchorNoise * 2.2f)) * foldMask
                    : 0f;
                float lobeWidth = paddleLobed ? halfWidth * 0.24f * lobePulse : 0f;
                float lobeInset = paddleLobed ? halfWidth * 0.1f * MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI * 2.2f + 0.7f) * foldMask : 0f;
                float lobePuff = paddleLobed ? centerLift * 0.38f * lobePulse : 0f;

                Vector3 normal = Vector3.Cross(rotatedWidth, upDir).normalized;
                if (normal.sqrMagnitude < 0.0001f)
                    normal = Vector3.Cross(rotatedWidth, forwardDir).normalized;

                Vector3 center = anchor
                    + upDir * (length * t - droop * t * t)
                    + forwardDir * (MathLodApproximation.ApproxSinBhaskara(t * Mathf.PI) * forwardBow + MathLodApproximation.ApproxSinBhaskara((t + 0.17f) * Mathf.PI * 2.2f) * length * 0.022f)
                    - rotatedWidth * baseWrap * 0.18f
                    + rotatedWidth * (laminaLobes * 0.12f)
                    + normal * lobePuff;

                Vector3 leftOuter = center
                    - rotatedWidth * (halfWidth + edgeWave + tipSplit + lateralAsymmetry + lobeWidth - lobeInset + frillSlice)
                    - normal * (curl + edgeWaveSecondary - innerFold * 0.18f + frillSlice * 0.2f);
                Vector3 leftInner = center
                    - rotatedWidth * (innerWidth + lateralAsymmetry * 0.28f)
                    - normal * (curl * 0.22f + innerFold);
                Vector3 mid = center + normal * (curl * 0.68f + centerFold) - upDir * (tipMask * length * 0.032f);
                Vector3 rightInner = center
                    + rotatedWidth * (innerWidth - lateralAsymmetry * 0.22f)
                    + normal * (curl * 0.38f + innerFold * 0.72f);
                Vector3 rightOuter = center
                    + rotatedWidth * (halfWidth - edgeWave + tipSplit - lateralAsymmetry + laminaLobes + lobeWidth + lobeInset * 0.72f - frillSlice)
                    + normal * (curl * 0.92f + edgeWaveSecondary - innerFold * 0.14f - frillSlice * 0.2f);

                Vector4 tangent = new Vector4(rotatedWidth.x, rotatedWidth.y, rotatedWidth.z, 1f);
                byte outerGreen = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * Mathf.Lerp(0.86f, 0.74f, t)), 0, 255);
                byte outerBlue = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * Mathf.Lerp(0.72f, 0.44f, t)), 0, 255);
                byte innerGreen = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * Mathf.Lerp(0.96f, 0.84f, t)), 0, 255);
                byte innerBlue = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * Mathf.Lerp(0.86f, 0.58f, t)), 0, 255);
                byte midGreen = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * Mathf.Lerp(1.08f, 1.2f, 1f - t)), 0, 255);
                byte midBlue = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * Mathf.Lerp(0.96f, 0.72f, t)), 0, 255);
                Color32 outerColor = new Color32(color.r, outerGreen, outerBlue, color.a);
                Color32 innerColor = new Color32((byte)Mathf.Clamp(color.r + 4, 0, 255), innerGreen, innerBlue, color.a);
                Color32 midColor = new Color32((byte)Mathf.Clamp(color.r + 8, 0, 255), midGreen, midBlue, color.a);

                buffers.AddVertex(leftOuter, normal, tangent, new Vector2(0f, t), outerColor);
                buffers.AddVertex(leftInner, normal, tangent, new Vector2(0.25f, t), innerColor);
                buffers.AddVertex(mid, normal, tangent, new Vector2(0.5f, t), midColor);
                buffers.AddVertex(rightInner, normal, tangent, new Vector2(0.75f, t), innerColor);
                buffers.AddVertex(rightOuter, normal, tangent, new Vector2(1f, t), outerColor);
            }

            for (int i = 0; i < segments; i++)
            {
                int row = startIndex + i * 5;
                int nextRow = row + 5;
                buffers.AddQuad(row, nextRow, nextRow + 1, row + 1);
                buffers.AddQuad(row + 1, nextRow + 1, nextRow + 2, row + 2);
                buffers.AddQuad(row + 2, nextRow + 2, nextRow + 3, row + 3);
                buffers.AddQuad(row + 3, nextRow + 3, nextRow + 4, row + 4);
            }

            // Large near-field leaves occasionally get a thin back shell so big kelp
            // reads as organic mass instead of a flat card. Distant LODs stay single-sided.
            float thicknessLikelihood = ResolveBladeThicknessLikelihood(spec, bladeProfile, width, length);
            if (lodLevel > 0 || thicknessLikelihood <= 0f)
                return;

            float thicknessSelector = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara((anchor.x * 1.91f + anchor.y * 0.67f + anchor.z * 1.37f + length * 0.11f) * 5.2f);
            if (thicknessSelector > thicknessLikelihood)
                return;

            float shellThickness = ResolveBladeShellThickness(width, length, bladeProfile);
            AddBladeThicknessShell(buffers, startIndex, segments, shellThickness);
        }

        private static float ResolveBladeThicknessLikelihood(VariantSpec spec, BladeProfile bladeProfile, float width, float length)
        {
            float sizeWeight = Mathf.Clamp01(Mathf.InverseLerp(1.9f, 6.2f, length)) * 0.72f
                + Mathf.Clamp01(Mathf.InverseLerp(0.16f, 0.36f, width)) * 0.42f;

            if (spec.ClusterCount > 1)
                sizeWeight -= 0.22f;

            if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
                sizeWeight -= 0.05f;

            switch (bladeProfile)
            {
                case BladeProfile.FoldedLamina:
                    return Mathf.Clamp01(sizeWeight + 0.36f);
                case BladeProfile.PaddleLobed:
                    return Mathf.Clamp01(sizeWeight + (spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.18f : 0.14f));
                case BladeProfile.FrilledRibbon:
                    return Mathf.Clamp01(sizeWeight + (spec.ClusterCount > 1 ? -0.04f : 0.08f));
                case BladeProfile.BroadUndulate:
                    return Mathf.Clamp01(sizeWeight + (spec.GrowthStyle == GrowthStyle.CrownCanopy ? 0.08f : 0.02f));
                default:
                    return Mathf.Clamp01(sizeWeight - 0.18f);
            }
        }

        private static float ResolveBladeShellThickness(float width, float length, BladeProfile bladeProfile)
        {
            float baseThickness = width * 0.026f + length * 0.0018f;
            switch (bladeProfile)
            {
                case BladeProfile.FoldedLamina:
                    baseThickness *= 1.34f;
                    break;
                case BladeProfile.PaddleLobed:
                    baseThickness *= 1.24f;
                    break;
                case BladeProfile.BroadUndulate:
                    baseThickness *= 1.08f;
                    break;
                case BladeProfile.FrilledRibbon:
                    baseThickness *= 0.94f;
                    break;
            }

            return Mathf.Clamp(baseThickness, 0.004f, 0.04f);
        }

        private static void AddBladeThicknessShell(MeshBuffers buffers, int frontStartIndex, int segments, float shellThickness)
        {
            const int RowWidth = 5;
            int backStartIndex = buffers.Vertices.Count;

            for (int rowIndex = 0; rowIndex <= segments; rowIndex++)
            {
                int frontRow = frontStartIndex + rowIndex * RowWidth;
                for (int columnIndex = 0; columnIndex < RowWidth; columnIndex++)
                {
                    int frontIndex = frontRow + columnIndex;
                    Vector3 frontPosition = buffers.Vertices[frontIndex];
                    Vector3 frontNormal = buffers.Normals[frontIndex];
                    Vector4 frontTangent = buffers.Tangents[frontIndex];
                    Vector2 frontUv = buffers.UVs[frontIndex];
                    Color32 frontColor = buffers.Colors[frontIndex];

                    Vector3 backPosition = frontPosition - frontNormal * shellThickness;
                    Vector4 backTangent = new Vector4(frontTangent.x, frontTangent.y, frontTangent.z, -1f);
                    Color32 backColor = new Color32(
                        (byte)Mathf.Clamp(frontColor.r - 8, 0, 255),
                        (byte)Mathf.Clamp(frontColor.g - 10, 0, 255),
                        (byte)Mathf.Clamp(frontColor.b - 10, 0, 255),
                        frontColor.a);
                    buffers.AddVertex(backPosition, -frontNormal, backTangent, frontUv, backColor);
                }
            }

            for (int rowIndex = 0; rowIndex < segments; rowIndex++)
            {
                int backRow = backStartIndex + rowIndex * RowWidth;
                int nextBackRow = backRow + RowWidth;

                buffers.AddQuad(backRow + 1, nextBackRow + 1, nextBackRow, backRow);
                buffers.AddQuad(backRow + 2, nextBackRow + 2, nextBackRow + 1, backRow + 1);
                buffers.AddQuad(backRow + 3, nextBackRow + 3, nextBackRow + 2, backRow + 2);
                buffers.AddQuad(backRow + 4, nextBackRow + 4, nextBackRow + 3, backRow + 3);

                int frontRow = frontStartIndex + rowIndex * RowWidth;
                int nextFrontRow = frontRow + RowWidth;
                buffers.AddQuad(frontRow, nextFrontRow, nextBackRow, backRow);
                buffers.AddQuad(frontRow + 4, backRow + 4, nextBackRow + 4, nextFrontRow + 4);
            }

            int frontTipRow = frontStartIndex + segments * RowWidth;
            int backTipRow = backStartIndex + segments * RowWidth;
            buffers.AddQuad(frontTipRow, backTipRow, backTipRow + 1, frontTipRow + 1);
            buffers.AddQuad(frontTipRow + 1, backTipRow + 1, backTipRow + 2, frontTipRow + 2);
            buffers.AddQuad(frontTipRow + 2, backTipRow + 2, backTipRow + 3, frontTipRow + 3);
            buffers.AddQuad(frontTipRow + 3, backTipRow + 3, backTipRow + 4, frontTipRow + 4);
        }

        private static void AddSphere(MeshBuffers buffers, Vector3 center, Vector3 radii, int latSegments, int lonSegments, Color32 color)
        {
            int startIndex = buffers.Vertices.Count;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float cosPhi = MathLodApproximation.ApproxCosBhaskara(phi);
                float sinPhi = MathLodApproximation.ApproxSinBhaskara(phi);
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * TwoPi;
                    Vector3 normal = new Vector3(MathLodApproximation.ApproxCosBhaskara(theta) * cosPhi, sinPhi, MathLodApproximation.ApproxSinBhaskara(theta) * cosPhi).normalized;
                    Vector3 vertex = center + Vector3.Scale(normal, radii);
                    Vector3 tangentDir = new Vector3(-MathLodApproximation.ApproxSinBhaskara(theta), 0f, MathLodApproximation.ApproxCosBhaskara(theta)).normalized;
                    buffers.AddVertex(vertex, normal, new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, 1f), new Vector2(u, v), color);
                }
            }

            int rowSize = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                int rowStart = startIndex + lat * rowSize;
                int nextRowStart = rowStart + rowSize;
                for (int lon = 0; lon < lonSegments; lon++)
                    buffers.AddQuad(rowStart + lon, nextRowStart + lon, nextRowStart + lon + 1, rowStart + lon + 1);
            }
        }

        private static Mesh CreateMesh(string rootToken, int lod, MeshBuffers buffers)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(buffers.Vertices.Count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            meshData.SetIndexBufferParams(buffers.Indices.Count, IndexFormat.UInt32);

            NativeArray<VertexData> vertexData = meshData.GetVertexData<VertexData>();
            for (int i = 0; i < buffers.Vertices.Count; i++)
            {
                vertexData[i] = new VertexData(buffers.Vertices[i], buffers.Normals[i], buffers.Tangents[i], buffers.Colors[i], buffers.UVs[i]);
            }

            NativeArray<uint> indexData = meshData.GetIndexData<uint>();
            for (int i = 0; i < buffers.Indices.Count; i++)
                indexData[i] = buffers.Indices[i];

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, buffers.Indices.Count, MeshTopology.Triangles)
            {
                bounds = buffers.Bounds,
                vertexCount = buffers.Vertices.Count
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

            Mesh mesh = new Mesh
            {
                name = rootToken + "_LOD" + lod
            };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            mesh.bounds = buffers.Bounds;
            return mesh;
        }

        private static bool TryResolveSpec(string rootToken, out VariantSpec spec)
        {
            switch (NormalizeRootToken(rootToken))
            {
                case "family_kelp_tall__stalk": spec = new VariantSpec(10, 14, 13, 12, 5, 0.94f, 0.18f, 0.08f, 4f, 0.05f, 0f, 0f, 0.14f, 0.94f, 0.18f, 0.46f, 0.18f, 0.72f, 0.1f, 0.5f, 0.09f, 12f, -18f, 92f, 18f, 6, 0.03f, 156, 4600, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_tall__lean": spec = new VariantSpec(9, 12, 12, 10, 4, 0.88f, 0.18f, 0.08f, 5f, 0.08f, 18f, 0.18f, 0.16f, 0.92f, 0.18f, 0.5f, 0.18f, 0.76f, 0.1f, 0.52f, 0.11f, 18f, -26f, 98f, 22f, 5, 0.035f, 148, 3600, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_tall__ribbon": spec = new VariantSpec(8, 13, 14, 12, 5, 0.98f, 0.16f, 0.06f, 5f, 0.1f, 24f, 0.22f, 0.18f, 0.98f, 0.2f, 0.58f, 0.22f, 0.94f, 0.1f, 0.54f, 0.13f, 26f, -34f, 112f, 28f, 4, 0.04f, 164, 4900, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon); return true;
                case "family_kelp_tall__lamina": spec = new VariantSpec(9, 14, 14, 11, 4, 0.9f, 0.19f, 0.08f, 4f, 0.06f, 6f, 0.04f, 0.12f, 0.9f, 0.24f, 0.62f, 0.2f, 0.78f, 0.08f, 0.48f, 0.08f, 10f, -18f, 88f, 16f, 6, 0.032f, 160, 4300, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_tall__rope": spec = new VariantSpec(8, 13, 12, 10, 6, 0.98f, 0.15f, 0.05f, 5f, 0.08f, 12f, 0.18f, 0.16f, 0.96f, 0.14f, 0.34f, 0.18f, 0.72f, 0.12f, 0.52f, 0.12f, 20f, -12f, 74f, 26f, 4, 0.042f, 150, 4100, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_tall__banner": spec = new VariantSpec(9, 14, 14, 12, 4, 0.96f, 0.18f, 0.07f, 4f, 0.07f, 16f, 0.18f, 0.16f, 0.98f, 0.24f, 0.66f, 0.22f, 0.98f, 0.08f, 0.52f, 0.12f, 22f, -28f, 104f, 24f, 5, 0.036f, 162, 4700, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_tall__lance": spec = new VariantSpec(8, 13, 13, 11, 3, 0.92f, 0.15f, 0.05f, 5f, 0.08f, 10f, 0.16f, 0.14f, 0.94f, 0.12f, 0.3f, 0.18f, 0.74f, 0.12f, 0.5f, 0.1f, 18f, -14f, 78f, 22f, 4, 0.038f, 152, 4000, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_tall__seedling": spec = new VariantSpec(6, 8, 9, 6, 0, 0.56f, 0.11f, 0.05f, 2f, 0.02f, 3f, 0.03f, 0.12f, 0.58f, 0.08f, 0.18f, 0.12f, 0.28f, 0.06f, 0.34f, 0.04f, 8f, -22f, 62f, 12f, 5, 0.024f, 134, 2200, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_tall__tower": spec = new VariantSpec(11, 16, 15, 16, 6, 1.08f, 0.22f, 0.09f, 5f, 0.08f, 12f, 0.16f, 0.12f, 0.98f, 0.22f, 0.58f, 0.24f, 0.94f, 0.1f, 0.56f, 0.12f, 18f, -12f, 66f, 20f, 7, 0.034f, 168, 6600, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_tall__colossus": spec = new VariantSpec(11, 15, 14, 16, 4, 1.12f, 0.24f, 0.1f, 5f, 0.08f, 14f, 0.14f, 0.08f, 0.98f, 0.22f, 0.56f, 0.24f, 0.98f, 0.06f, 0.52f, 0.1f, 16f, -8f, 54f, 16f, 7, 0.036f, 170, 6800, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate, 1, 0f); return true;
                case "family_kelp_tall__sail": spec = new VariantSpec(9, 13, 11, 10, 1, 1.04f, 0.2f, 0.07f, 4f, 0.05f, 10f, 0.1f, 0.12f, 0.92f, 0.34f, 0.9f, 0.34f, 1.08f, 0.04f, 0.54f, 0.08f, 8f, -18f, 46f, 10f, 6, 0.03f, 148, 5400, GrowthStyle.GiantFrond, BladeProfile.FoldedLamina); return true;
                case "family_kelp_tall__paddle": spec = new VariantSpec(10, 15, 15, 13, 3, 1.02f, 0.2f, 0.08f, 4f, 0.06f, 8f, 0.08f, 0.14f, 0.9f, 0.28f, 0.78f, 0.26f, 0.96f, 0.06f, 0.54f, 0.08f, 10f, -22f, 86f, 16f, 6, 0.032f, 170, 5400, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed); return true;
                case "family_kelp_tall__broadleaf": spec = new VariantSpec(10, 15, 14, 10, 2, 1.06f, 0.22f, 0.09f, 4f, 0.06f, 8f, 0.08f, 0.18f, 0.94f, 0.48f, 1.24f, 0.38f, 1.22f, 0.04f, 0.68f, 0.08f, 18f, -44f, 96f, 22f, 6, 0.032f, 190, 8600, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed, 2, 0.14f); return true;
                case "family_kelp_tall__frondcrest": spec = new VariantSpec(9, 14, 14, 13, 1, 1.02f, 0.18f, 0.07f, 4f, 0.05f, 12f, 0.12f, 0.16f, 0.9f, 0.18f, 0.48f, 0.24f, 0.88f, 0.08f, 0.5f, 0.1f, 20f, -28f, 104f, 24f, 6, 0.03f, 164, 5000, GrowthStyle.GiantFrond, BladeProfile.FrilledRibbon); return true;
                case "family_kelp_patch_dense__patch": spec = new VariantSpec(8, 11, 11, 12, 2, 0.84f, 0.2f, 0.09f, 4f, 0.06f, 8f, 0.12f, 0.16f, 0.84f, 0.18f, 0.42f, 0.18f, 0.68f, 0.14f, 0.48f, 0.10f, 18f, -72f, 156f, 34f, 6, 0.035f, 144, 5200, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate, 3, 0.18f); return true;
                case "family_kelp_patch_dense__patch_tall": spec = new VariantSpec(9, 12, 12, 13, 2, 0.92f, 0.18f, 0.08f, 4f, 0.05f, 12f, 0.14f, 0.18f, 0.9f, 0.18f, 0.46f, 0.16f, 0.78f, 0.14f, 0.5f, 0.10f, 22f, -64f, 164f, 38f, 7, 0.034f, 150, 5600, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon, 4, 0.22f); return true;
                case "family_kelp_patch_dense__ring": spec = new VariantSpec(8, 11, 12, 12, 1, 0.78f, 0.18f, 0.08f, 4f, 0.05f, 12f, 0.08f, 0.14f, 0.78f, 0.22f, 0.48f, 0.2f, 0.72f, 0.1f, 0.46f, 0.08f, 18f, -18f, 312f, 26f, 7, 0.034f, 146, 5600, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate, 4, 0.18f); return true;
                case "family_kelp_patch_dense__brush": spec = new VariantSpec(7, 10, 11, 14, 0, 0.76f, 0.18f, 0.08f, 3f, 0.04f, 6f, 0.08f, 0.12f, 0.74f, 0.16f, 0.32f, 0.14f, 0.58f, 0.1f, 0.4f, 0.07f, 18f, -76f, 154f, 24f, 7, 0.03f, 142, 5000, GrowthStyle.GiantFrond, BladeProfile.FrilledRibbon, 4, 0.18f); return true;
                case "family_kelp_patch_dense__sheet": spec = new VariantSpec(8, 11, 12, 13, 1, 0.86f, 0.2f, 0.09f, 4f, 0.05f, 6f, 0.08f, 0.14f, 0.82f, 0.2f, 0.48f, 0.18f, 0.7f, 0.1f, 0.46f, 0.09f, 16f, -78f, 170f, 30f, 6, 0.034f, 146, 5400, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate, 3, 0.2f); return true;
                case "family_kelp_patch_dense__tuft": spec = new VariantSpec(7, 10, 11, 15, 0, 0.7f, 0.18f, 0.08f, 3f, 0.04f, 4f, 0.05f, 0.08f, 0.68f, 0.14f, 0.3f, 0.12f, 0.48f, 0.1f, 0.4f, 0.06f, 16f, -102f, 196f, 22f, 8, 0.028f, 140, 4700, GrowthStyle.GiantFrond, BladeProfile.FrilledRibbon, 4, 0.18f); return true;
                case "family_kelp_patch_dense__drape": spec = new VariantSpec(8, 11, 12, 14, 1, 0.82f, 0.19f, 0.08f, 4f, 0.05f, 10f, 0.12f, 0.16f, 0.8f, 0.22f, 0.56f, 0.18f, 0.8f, 0.08f, 0.5f, 0.1f, 18f, -86f, 178f, 34f, 6, 0.034f, 148, 5600, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon, 3, 0.2f); return true;
                case "family_kelp_patch_dense__nest": spec = new VariantSpec(7, 9, 9, 13, 0, 0.62f, 0.16f, 0.07f, 3f, 0.03f, 4f, 0.04f, 0.08f, 0.58f, 0.1f, 0.2f, 0.1f, 0.34f, 0.08f, 0.34f, 0.05f, 10f, -128f, 236f, 18f, 8, 0.028f, 136, 4300, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap, 5, 0.2f); return true;
                case "family_kelp_patch_dense__sheetwall": spec = new VariantSpec(9, 12, 12, 13, 2, 0.92f, 0.2f, 0.09f, 4f, 0.05f, 8f, 0.1f, 0.14f, 0.86f, 0.18f, 0.48f, 0.18f, 0.74f, 0.08f, 0.48f, 0.08f, 14f, -92f, 188f, 30f, 7, 0.032f, 152, 5200, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate, 4, 0.22f); return true;
                case "family_kelp_patch_dense__bladder": spec = new VariantSpec(8, 11, 11, 11, 4, 0.8f, 0.19f, 0.08f, 4f, 0.05f, 6f, 0.08f, 0.12f, 0.72f, 0.18f, 0.5f, 0.16f, 0.68f, 0.08f, 0.46f, 0.08f, 14f, -96f, 196f, 24f, 6, 0.032f, 150, 5000, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed, 4, 0.2f); return true;
                case "family_kelp_patch_dense__paddlespray": spec = new VariantSpec(8, 11, 11, 13, 3, 0.8f, 0.19f, 0.08f, 4f, 0.05f, 8f, 0.1f, 0.12f, 0.72f, 0.18f, 0.54f, 0.18f, 0.72f, 0.08f, 0.48f, 0.08f, 18f, -110f, 220f, 28f, 7, 0.032f, 150, 5200, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed, 4, 0.22f); return true;
                case "family_kelp_patch_dense__frilltuft": spec = new VariantSpec(8, 11, 12, 13, 1, 0.76f, 0.18f, 0.08f, 4f, 0.05f, 10f, 0.12f, 0.1f, 0.7f, 0.15f, 0.38f, 0.16f, 0.62f, 0.1f, 0.44f, 0.08f, 24f, -118f, 228f, 30f, 7, 0.03f, 148, 5200, GrowthStyle.GiantFrond, BladeProfile.FrilledRibbon, 4, 0.22f); return true;
                case "family_kelp_canopy__crown": spec = new VariantSpec(10, 15, 14, 14, 3, 1f, 0.2f, 0.08f, 5f, 0.08f, 12f, 0.1f, 0.42f, 0.84f, 0.24f, 0.62f, 0.26f, 0.98f, 0.12f, 0.56f, 0.12f, 26f, -76f, 180f, 34f, 7, 0.038f, 170, 4600, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__frond": spec = new VariantSpec(9, 13, 12, 10, 2, 0.92f, 0.18f, 0.07f, 4f, 0.06f, 6f, 0.08f, 0.34f, 0.76f, 0.22f, 0.56f, 0.24f, 0.9f, 0.12f, 0.52f, 0.10f, 18f, -54f, 118f, 32f, 5, 0.03f, 162, 3400, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__fan": spec = new VariantSpec(10, 14, 13, 14, 2, 0.96f, 0.18f, 0.07f, 5f, 0.08f, 10f, 0.09f, 0.38f, 0.82f, 0.24f, 0.58f, 0.24f, 0.94f, 0.12f, 0.54f, 0.10f, 28f, -92f, 188f, 38f, 6, 0.034f, 174, 4400, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__mantle": spec = new VariantSpec(10, 15, 14, 15, 1, 0.98f, 0.2f, 0.08f, 5f, 0.08f, 8f, 0.08f, 0.4f, 0.86f, 0.28f, 0.68f, 0.26f, 1f, 0.1f, 0.56f, 0.12f, 22f, -84f, 196f, 30f, 7, 0.036f, 176, 6200, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__splay": spec = new VariantSpec(9, 14, 13, 13, 2, 0.94f, 0.18f, 0.07f, 5f, 0.07f, 14f, 0.1f, 0.36f, 0.8f, 0.22f, 0.56f, 0.22f, 0.92f, 0.1f, 0.54f, 0.1f, 30f, -104f, 210f, 40f, 6, 0.034f, 168, 4500, GrowthStyle.CrownCanopy, BladeProfile.SplitRibbon); return true;
                case "family_kelp_canopy__veil": spec = new VariantSpec(10, 15, 14, 15, 1, 0.98f, 0.18f, 0.07f, 5f, 0.08f, 6f, 0.08f, 0.42f, 0.88f, 0.26f, 0.7f, 0.24f, 1f, 0.08f, 0.58f, 0.1f, 18f, -88f, 208f, 28f, 6, 0.034f, 172, 4900, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__rosette": spec = new VariantSpec(9, 13, 13, 16, 1, 0.8f, 0.18f, 0.08f, 4f, 0.06f, 4f, 0.04f, 0.22f, 0.6f, 0.18f, 0.42f, 0.16f, 0.62f, 0.08f, 0.48f, 0.08f, 24f, -118f, 236f, 34f, 7, 0.032f, 166, 4300, GrowthStyle.CrownCanopy, BladeProfile.SplitRibbon); return true;
                case "family_kelp_canopy__laminaria": spec = new VariantSpec(10, 15, 15, 12, 1, 1.02f, 0.22f, 0.09f, 5f, 0.06f, 4f, 0.05f, 0.44f, 0.92f, 0.38f, 0.88f, 0.34f, 1.1f, 0.05f, 0.6f, 0.08f, 10f, -10f, 34f, 8f, 7, 0.034f, 178, 6200, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__sheetwall": spec = new VariantSpec(11, 16, 15, 14, 2, 1.06f, 0.22f, 0.08f, 5f, 0.07f, 6f, 0.05f, 0.46f, 0.96f, 0.42f, 0.96f, 0.36f, 1.16f, 0.05f, 0.62f, 0.08f, 10f, -12f, 24f, 6f, 8, 0.036f, 180, 6800, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate); return true;
                case "family_kelp_canopy__tapestry": spec = new VariantSpec(10, 12, 12, 11, 1, 1.08f, 0.22f, 0.08f, 5f, 0.06f, 18f, 0.07f, 0.26f, 0.92f, 1.06f, 1.88f, 0.82f, 1.9f, 0.01f, 0.96f, 0.14f, 26f, -112f, 228f, 28f, 6, 0.042f, 188, 8800, GrowthStyle.CrownCanopy, BladeProfile.BroadUndulate, 3, 0.24f); return true;
                case "family_kelp_canopy__windrow": spec = new VariantSpec(10, 14, 13, 12, 2, 1.04f, 0.2f, 0.08f, 5f, 0.06f, 16f, 0.09f, 0.34f, 0.84f, 0.86f, 1.48f, 0.62f, 1.54f, 0.02f, 0.88f, 0.14f, 18f, -124f, 256f, 22f, 7, 0.04f, 184, 7800, GrowthStyle.CrownCanopy, BladeProfile.SplitRibbon, 3, 0.26f); return true;
                case "family_kelp_canopy__tanglemat": spec = new VariantSpec(10, 15, 14, 15, 4, 0.94f, 0.19f, 0.08f, 5f, 0.08f, 22f, 0.18f, 0.38f, 0.86f, 0.18f, 0.54f, 0.22f, 0.92f, 0.12f, 0.58f, 0.12f, 30f, -142f, 268f, 34f, 8, 0.036f, 176, 7200, GrowthStyle.CrownCanopy, BladeProfile.FrilledRibbon, 4, 0.24f); return true;
                case "family_kelp_canopy__oar": spec = new VariantSpec(10, 15, 15, 13, 2, 1f, 0.2f, 0.08f, 5f, 0.07f, 8f, 0.08f, 0.42f, 0.9f, 0.3f, 0.86f, 0.3f, 1.02f, 0.06f, 0.58f, 0.08f, 16f, -42f, 108f, 20f, 7, 0.034f, 180, 5200, GrowthStyle.CrownCanopy, BladeProfile.PaddleLobed); return true;
                case "family_kelp_canopy__paddlefan": spec = new VariantSpec(10, 15, 14, 13, 2, 1.02f, 0.2f, 0.08f, 5f, 0.07f, 10f, 0.1f, 0.44f, 0.92f, 0.38f, 1.06f, 0.36f, 1.08f, 0.05f, 0.62f, 0.08f, 20f, -84f, 172f, 34f, 7, 0.034f, 194, 7600, GrowthStyle.CrownCanopy, BladeProfile.PaddleLobed); return true;
                case "family_kelp_canopy__featherfan": spec = new VariantSpec(10, 15, 15, 13, 1, 1.02f, 0.2f, 0.08f, 5f, 0.07f, 12f, 0.1f, 0.42f, 0.92f, 0.18f, 0.5f, 0.28f, 0.96f, 0.06f, 0.56f, 0.08f, 26f, -74f, 164f, 28f, 7, 0.032f, 178, 5200, GrowthStyle.CrownCanopy, BladeProfile.FrilledRibbon); return true;
                case "family_kelp_abyssal__strap": spec = new VariantSpec(8, 12, 13, 11, 1, 0.92f, 0.15f, 0.05f, 4f, 0.07f, 8f, 0.12f, 0.18f, 0.88f, 0.12f, 0.28f, 0.22f, 0.82f, 0.08f, 0.42f, 0.08f, 18f, -22f, 102f, 18f, 5, 0.026f, 76, 3900, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_abyssal__shroud": spec = new VariantSpec(9, 13, 14, 12, 1, 0.98f, 0.16f, 0.05f, 5f, 0.08f, 14f, 0.14f, 0.22f, 0.94f, 0.18f, 0.46f, 0.24f, 0.98f, 0.06f, 0.46f, 0.1f, 16f, -36f, 148f, 24f, 5, 0.03f, 82, 4700, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_abyssal__nodule": spec = new VariantSpec(8, 12, 13, 10, 5, 0.94f, 0.15f, 0.05f, 5f, 0.09f, 10f, 0.14f, 0.18f, 0.86f, 0.11f, 0.24f, 0.2f, 0.74f, 0.1f, 0.4f, 0.08f, 14f, -28f, 116f, 20f, 5, 0.028f, 88, 4400, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon); return true;
                case "family_kelp_abyssal__whip": spec = new VariantSpec(8, 13, 14, 10, 0, 0.98f, 0.13f, 0.04f, 5f, 0.09f, 18f, 0.18f, 0.14f, 0.96f, 0.08f, 0.2f, 0.18f, 0.72f, 0.12f, 0.56f, 0.08f, 26f, -18f, 84f, 30f, 4, 0.024f, 72, 4100, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_abyssal__mantle": spec = new VariantSpec(9, 14, 15, 13, 2, 1f, 0.17f, 0.05f, 5f, 0.08f, 20f, 0.18f, 0.24f, 1f, 0.22f, 0.54f, 0.24f, 1f, 0.06f, 0.48f, 0.1f, 14f, -42f, 164f, 22f, 5, 0.03f, 86, 5200, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_abyssal__braid": spec = new VariantSpec(10, 15, 16, 12, 3, 0.96f, 0.145f, 0.045f, 6f, 0.09f, 16f, 0.2f, 0.18f, 0.98f, 0.16f, 0.38f, 0.2f, 0.86f, 0.08f, 0.46f, 0.08f, 18f, -26f, 126f, 26f, 5, 0.028f, 84, 5000, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon); return true;
                case "family_kelp_abyssal__pennant": spec = new VariantSpec(9, 14, 15, 14, 1, 1.02f, 0.18f, 0.05f, 5f, 0.08f, 22f, 0.2f, 0.26f, 1.02f, 0.24f, 0.6f, 0.24f, 1.04f, 0.06f, 0.52f, 0.1f, 12f, -46f, 172f, 20f, 5, 0.031f, 92, 5600, GrowthStyle.GiantFrond, BladeProfile.BroadUndulate); return true;
                case "family_kelp_abyssal__reed": spec = new VariantSpec(7, 11, 11, 9, 0, 0.78f, 0.11f, 0.035f, 4f, 0.05f, 20f, 0.2f, 0.16f, 0.9f, 0.06f, 0.16f, 0.16f, 0.58f, 0.1f, 0.42f, 0.06f, 22f, -24f, 88f, 24f, 4, 0.022f, 70, 3600, GrowthStyle.GiantFrond, BladeProfile.NarrowStrap); return true;
                case "family_kelp_abyssal__cathedral": spec = new VariantSpec(10, 15, 15, 15, 4, 1.04f, 0.18f, 0.05f, 6f, 0.08f, 26f, 0.24f, 0.2f, 0.96f, 0.18f, 0.44f, 0.22f, 0.94f, 0.08f, 0.54f, 0.1f, 18f, -52f, 176f, 22f, 6, 0.03f, 90, 6600, GrowthStyle.GiantFrond, BladeProfile.SplitRibbon, 2, 0.12f); return true;
                case "family_kelp_abyssal__cowl": spec = new VariantSpec(9, 14, 15, 13, 1, 1f, 0.17f, 0.05f, 5f, 0.07f, 18f, 0.18f, 0.22f, 0.98f, 0.34f, 0.86f, 0.32f, 1.12f, 0.04f, 0.5f, 0.08f, 8f, -12f, 24f, 8f, 5, 0.03f, 92, 7000, GrowthStyle.GiantFrond, BladeProfile.FoldedLamina); return true;
                case "family_kelp_abyssal__veilwall": spec = new VariantSpec(9, 13, 11, 10, 1, 0.98f, 0.16f, 0.05f, 4f, 0.05f, 14f, 0.14f, 0.24f, 0.94f, 0.3f, 0.76f, 0.3f, 0.94f, 0.04f, 0.48f, 0.08f, 5f, -8f, 12f, 4f, 5, 0.028f, 80, 3600, GrowthStyle.GiantFrond, BladeProfile.FoldedLamina); return true;
                case "family_kelp_abyssal__lantern": spec = new VariantSpec(9, 14, 15, 12, 5, 0.98f, 0.16f, 0.05f, 5f, 0.08f, 16f, 0.18f, 0.2f, 0.92f, 0.24f, 0.68f, 0.22f, 0.92f, 0.06f, 0.48f, 0.08f, 12f, -30f, 124f, 18f, 5, 0.03f, 94, 5600, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed); return true;
                case "family_kelp_abyssal__petal": spec = new VariantSpec(9, 14, 14, 12, 4, 0.94f, 0.16f, 0.05f, 5f, 0.08f, 18f, 0.18f, 0.18f, 0.88f, 0.32f, 0.86f, 0.24f, 1f, 0.05f, 0.52f, 0.08f, 18f, -48f, 164f, 30f, 5, 0.03f, 102, 7600, GrowthStyle.GiantFrond, BladeProfile.PaddleLobed); return true;
                case "family_kelp_abyssal__tatterveil": spec = new VariantSpec(9, 14, 15, 13, 2, 0.98f, 0.15f, 0.05f, 5f, 0.08f, 22f, 0.22f, 0.18f, 0.88f, 0.15f, 0.38f, 0.2f, 0.82f, 0.08f, 0.46f, 0.08f, 24f, -38f, 156f, 24f, 5, 0.03f, 90, 5200, GrowthStyle.GiantFrond, BladeProfile.FrilledRibbon); return true;
                default: spec = default; return false;
            }
        }

        private static int ResolveBladeSegments(VariantSpec spec, int lod, bool foldedSheet, bool foldedGiant, bool paddleLobed, bool canopySheet, bool towerLaminar, bool broadleafKelp, bool paddlefanKelp, bool sailKelp, bool deepPetal)
        {
            int bladeSegments = Mathf.Max(2, spec.BladeSegments - (lod * 3));
            if (lod > 1)
                return bladeSegments;

            if (lod == 0)
            {
                if (foldedSheet || broadleafKelp || paddlefanKelp || deepPetal)
                    return Mathf.Max(bladeSegments, spec.BladeSegments + 2);

                if (foldedGiant || paddleLobed || canopySheet || towerLaminar)
                    return Mathf.Max(bladeSegments, spec.BladeSegments + 1);

                if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
                    return Mathf.Max(bladeSegments, spec.BladeSegments);

                return bladeSegments;
            }

            if (foldedSheet || broadleafKelp || paddlefanKelp || deepPetal)
                return Mathf.Max(bladeSegments, spec.BladeSegments - 1);

            if ((foldedGiant && !sailKelp) || paddleLobed || canopySheet)
                return Mathf.Max(bladeSegments, spec.BladeSegments - 2);

            if (towerLaminar)
                return Mathf.Max(bladeSegments, spec.BladeSegments - 2);

            return bladeSegments;
        }

        private static string NormalizeRootToken(string rootToken)
        {
            if (string.IsNullOrWhiteSpace(rootToken))
                return string.Empty;

            string trimmed = rootToken.Trim();
            string[] tokens = trimmed.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return trimmed;

            List<string> normalizedTokens = new List<string>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length > 1)
                {
                    char prefix = char.ToLowerInvariant(token[0]);
                    if (prefix == 's' || prefix == 'w')
                    {
                        bool hasDigit = false;
                        for (int j = 1; j < token.Length; j++)
                        {
                            if (char.IsDigit(token[j]))
                            {
                                hasDigit = true;
                                break;
                            }
                        }

                        if (hasDigit)
                            continue;
                    }
                }

                normalizedTokens.Add(token);
            }

            return normalizedTokens.Count == 0
                ? trimmed
                : string.Join("__", normalizedTokens);
        }

        private readonly struct VertexData
        {
            public VertexData(Vector3 position, Vector3 normal, Vector4 tangent, Color32 color, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                Tangent = tangent;
                Color = color;
                UV = uv;
            }

            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector4 Tangent;
            public readonly Color32 Color;
            public readonly Vector2 UV;
        }

        private readonly struct StipeFrame
        {
            public StipeFrame(Vector3 center, Vector3 tangent, Vector3 radial, Vector3 binormal, float radius)
            {
                Center = center;
                Tangent = tangent;
                Radial = radial;
                Binormal = binormal;
                Radius = radius;
            }

            public Vector3 Center { get; }
            public Vector3 Tangent { get; }
            public Vector3 Radial { get; }
            public Vector3 Binormal { get; }
            public float Radius { get; }
        }

        private readonly struct BladeSocket
        {
            public BladeSocket(Vector3 stemBase, Vector3 anchor, Vector3 widthAxis, Vector3 growthAxis, Vector3 forwardAxis, Vector3 stipeTangentAxis)
            {
                StemBase = stemBase;
                Anchor = anchor;
                WidthAxis = widthAxis;
                GrowthAxis = growthAxis;
                ForwardAxis = forwardAxis;
                StipeTangentAxis = stipeTangentAxis;
            }

            public Vector3 StemBase { get; }
            public Vector3 Anchor { get; }
            public Vector3 WidthAxis { get; }
            public Vector3 GrowthAxis { get; }
            public Vector3 ForwardAxis { get; }
            public Vector3 StipeTangentAxis { get; }
        }

        private readonly struct BladeBuildContext
        {
            public BladeBuildContext(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bladeIndex, float normalized, float primaryAngleOffset, Vector3 baseOffset, float clusterYawOffsetDegrees, float width, float length, float twist, float sideCurve, float serration, int bladeSegments)
            {
                Buffers = buffers;
                Spec = spec;
                Scale = scale;
                Lod = lod;
                BladeIndex = bladeIndex;
                Normalized = normalized;
                PrimaryAngleOffset = primaryAngleOffset;
                BaseOffset = baseOffset;
                ClusterYawOffsetDegrees = clusterYawOffsetDegrees;
                Width = width;
                Length = length;
                Twist = twist;
                SideCurve = sideCurve;
                Serration = serration;
                BladeSegments = bladeSegments;
            }

            public MeshBuffers Buffers { get; }
            public VariantSpec Spec { get; }
            public Vector3 Scale { get; }
            public int Lod { get; }
            public int BladeIndex { get; }
            public float Normalized { get; }
            public float PrimaryAngleOffset { get; }
            public Vector3 BaseOffset { get; }
            public float ClusterYawOffsetDegrees { get; }
            public float Width { get; }
            public float Length { get; }
            public float Twist { get; }
            public float SideCurve { get; }
            public float Serration { get; }
            public int BladeSegments { get; }
        }

        private readonly struct VariantSpec
        {
            public VariantSpec(int stipeSides, int stipeSegments, int bladeSegments, int bladeCount, int bulbCount, float stipeHeightMultiplier, float baseRadiusMultiplier, float topRadiusMultiplier, float ribCount, float ribAmplitude, float bendDegrees, float bendRadiusMultiplier, float bladeAnchorHeightMin, float bladeAnchorHeightMax, float bladeWidthMin, float bladeWidthMax, float bladeLengthMin, float bladeLengthMax, float bladeLengthFalloff, float bladeAnchorRadius, float forwardOffsetMultiplier, float twistDegreesMax, float bladeStartYaw, float bladeYawArc, float sideCurveDegrees, int rootCount, float rootYawOffset, byte tintByte, int estimatedVertexCount, GrowthStyle growthStyle, BladeProfile bladeProfile, int clusterCount = 1, float clusterSpread = 0f)
            {
                StipeSides = stipeSides;
                StipeSegments = stipeSegments;
                BladeSegments = bladeSegments;
                BladeCount = bladeCount;
                BulbCount = bulbCount;
                StipeHeightMultiplier = stipeHeightMultiplier;
                BaseRadiusMultiplier = baseRadiusMultiplier;
                TopRadiusMultiplier = topRadiusMultiplier;
                RibCount = ribCount;
                RibAmplitude = ribAmplitude;
                BendDegrees = bendDegrees;
                BendRadiusMultiplier = bendRadiusMultiplier;
                BladeAnchorHeightMin = bladeAnchorHeightMin;
                BladeAnchorHeightMax = bladeAnchorHeightMax;
                BladeWidthMin = bladeWidthMin;
                BladeWidthMax = bladeWidthMax;
                BladeLengthMin = bladeLengthMin;
                BladeLengthMax = bladeLengthMax;
                BladeLengthFalloff = bladeLengthFalloff;
                BladeAnchorRadius = bladeAnchorRadius;
                ForwardOffsetMultiplier = forwardOffsetMultiplier;
                TwistDegreesMin = 0f;
                TwistDegreesMax = twistDegreesMax;
                BladeStartYaw = bladeStartYaw;
                BladeYawArc = bladeYawArc;
                SideCurveDegrees = sideCurveDegrees;
                RootCount = rootCount;
                RootYawOffset = rootYawOffset;
                SerrationAmplitude = 0.015f;
                BulbHeightMin = 0.5f;
                BulbHeightMax = 0.82f;
                BulbRadiusMin = 0.18f;
                BulbRadiusMax = 0.28f;
                TintByte = tintByte;
                EstimatedVertexCount = estimatedVertexCount;
                GrowthStyle = growthStyle;
                BladeProfile = bladeProfile;
                ClusterCount = Mathf.Max(1, clusterCount);
                ClusterSpread = Mathf.Max(0f, clusterSpread);
            }

            public int StipeSides { get; }
            public int StipeSegments { get; }
            public int BladeSegments { get; }
            public int BladeCount { get; }
            public int BulbCount { get; }
            public float StipeHeightMultiplier { get; }
            public float BaseRadiusMultiplier { get; }
            public float TopRadiusMultiplier { get; }
            public float RibCount { get; }
            public float RibAmplitude { get; }
            public float BendDegrees { get; }
            public float BendRadiusMultiplier { get; }
            public float BladeAnchorHeightMin { get; }
            public float BladeAnchorHeightMax { get; }
            public float BladeWidthMin { get; }
            public float BladeWidthMax { get; }
            public float BladeLengthMin { get; }
            public float BladeLengthMax { get; }
            public float BladeLengthFalloff { get; }
            public float BladeAnchorRadius { get; }
            public float ForwardOffsetMultiplier { get; }
            public float TwistDegreesMin { get; }
            public float TwistDegreesMax { get; }
            public float BladeStartYaw { get; }
            public float BladeYawArc { get; }
            public float SideCurveDegrees { get; }
            public int RootCount { get; }
            public float RootYawOffset { get; }
            public float SerrationAmplitude { get; }
            public float BulbHeightMin { get; }
            public float BulbHeightMax { get; }
            public float BulbRadiusMin { get; }
            public float BulbRadiusMax { get; }
            public byte TintByte { get; }
            public int EstimatedVertexCount { get; }
            public GrowthStyle GrowthStyle { get; }
            public BladeProfile BladeProfile { get; }
            public int ClusterCount { get; }
            public float ClusterSpread { get; }
        }

        private enum GrowthStyle
        {
            GiantFrond = 0,
            CrownCanopy = 1
        }

        private enum BladeProfile
        {
            NarrowStrap = 0,
            BroadUndulate = 1,
            SplitRibbon = 2,
            FoldedLamina = 3,
            PaddleLobed = 4,
            FrilledRibbon = 5
        }

        private sealed class MeshBuffers
        {
            public MeshBuffers(int capacity)
            {
                Vertices = new List<Vector3>(capacity);
                Normals = new List<Vector3>(capacity);
                Tangents = new List<Vector4>(capacity);
                Colors = new List<Color32>(capacity);
                UVs = new List<Vector2>(capacity);
                Indices = new List<uint>(capacity * 3);
                Bounds = new Bounds(Vector3.zero, Vector3.zero);
                _hasBounds = false;
            }

            public List<Vector3> Vertices { get; }
            public List<Vector3> Normals { get; }
            public List<Vector4> Tangents { get; }
            public List<Color32> Colors { get; }
            public List<Vector2> UVs { get; }
            public List<uint> Indices { get; }
            public Bounds Bounds { get; private set; }

            private bool _hasBounds;

            public void AddVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv, Color32 color)
            {
                Vertices.Add(position);
                Normals.Add(normal);
                Tangents.Add(tangent);
                UVs.Add(uv);
                Colors.Add(color);
                if (!_hasBounds)
                {
                    Bounds = new Bounds(position, Vector3.zero);
                    _hasBounds = true;
                }
                else
                {
                    Bounds bounds = Bounds;
                    bounds.Encapsulate(position);
                    Bounds = bounds;
                }
            }

            public void AddQuad(int a, int b, int c, int d)
            {
                Indices.Add((uint)a);
                Indices.Add((uint)b);
                Indices.Add((uint)c);
                Indices.Add((uint)a);
                Indices.Add((uint)c);
                Indices.Add((uint)d);
            }
        }
    }
}
