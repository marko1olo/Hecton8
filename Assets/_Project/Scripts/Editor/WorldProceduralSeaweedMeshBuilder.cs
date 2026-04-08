using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
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
            BuildStipe(buffers, spec, scale, lod);

            int activeBladeCount = Mathf.Max(1, spec.BladeCount - lod);
            for (int bladeIndex = 0; bladeIndex < activeBladeCount; bladeIndex++)
                BuildBlade(buffers, spec, scale, lod, bladeIndex, activeBladeCount);

            int activeBulbCount = Mathf.Max(0, spec.BulbCount - lod);
            for (int bulbIndex = 0; bulbIndex < activeBulbCount; bulbIndex++)
                BuildBulb(buffers, spec, scale, lod, bulbIndex, activeBulbCount);

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
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 origin = new Vector3(0f, scale.y * 0.06f, 0f) + dir * (scale.x * 0.06f);
                float length = scale.x * Mathf.Lerp(0.36f, 0.62f, 0.5f + 0.5f * Mathf.Sin((i + 1) * 1.37f));
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

        private static void BuildStipe(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod)
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
                float wobbleX = Mathf.Sin((v * 2.6f + spec.BendDegrees * 0.02f) * Mathf.PI) * scale.x * 0.03f;
                float wobbleZ = Mathf.Sin((v * 4.3f + spec.RibCount * 0.11f) * Mathf.PI) * scale.z * 0.018f;
                Vector3 center = new Vector3(
                    Mathf.Sin(bendRadians) * scale.x * spec.BendRadiusMultiplier + wobbleX,
                    v * height,
                    Mathf.Cos(bendRadians) * scale.z * spec.ForwardOffsetMultiplier - scale.z * spec.ForwardOffsetMultiplier + wobbleZ);
                float bladeBand = EvaluateBand(v, bladeBandMin, bladeBandMax, 0.085f);
                float bulbBand = EvaluateBand(v, bulbBandMin, bulbBandMax, 0.07f);
                float nodeBulge = bladeBand * 0.22f + bulbBand * 0.14f;
                float scarNoise = Mathf.Sin((v * 8.5f + spec.BendDegrees * 0.03f) * Mathf.PI) * 0.035f;
                float radius = Mathf.Lerp(bottomRadius, topRadius, v) * (1f + nodeBulge + scarNoise);

                for (int side = 0; side <= radialSegments; side++)
                {
                    float u = side / (float)radialSegments;
                    float angle = u * TwoPi;
                    float rib = 1f + Mathf.Sin(angle * spec.RibCount + v * 2.2f) * spec.RibAmplitude;
                    float actualRadius = radius * rib;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 normal = radial.normalized;
                    Vector3 vertex = center + radial * actualRadius;
                    Vector4 tangent = new Vector4(-Mathf.Sin(angle), 0f, Mathf.Cos(angle), 1f);
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

        private static void BuildBlade(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bladeIndex, int bladeCount)
        {
            int bladeSegments = Mathf.Max(2, spec.BladeSegments - (lod * 3));
            float sequence = bladeCount <= 1 ? 0f : bladeIndex / (float)(bladeCount - 1);
            float normalized;
            if (bladeCount <= 1)
            {
                normalized = spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.62f : 0.76f;
            }
            else if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                normalized = Mathf.Lerp(0.08f, 0.98f, Mathf.SmoothStep(0f, 1f, sequence));
            }
            else
            {
                normalized = Mathf.Lerp(0.16f, 1f, Mathf.Pow(sequence, 0.9f));
            }

            float primaryAngleOffset = EvaluateBladeAngleOffset(bladeIndex, sequence);
            BladeSocket primarySocket = EvaluateBladeSocket(spec, scale, normalized, primaryAngleOffset);
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
            AddBladeRibbon(buffers, anchor, lateral, up, width, length, twist, bladeSegments, sideCurve, serration, new Color32(spec.TintByte, 208, (byte)Mathf.Lerp(40f, 210f, normalized), 255), forward);

            if (ShouldAddCompanionBlade(spec, lod, bladeIndex, normalized))
            {
                float companionSweep = primaryAngleOffset + (((bladeIndex & 1) == 0) ? 1f : -1f) * Mathf.Lerp(12f, 26f, normalized);
                BladeSocket companionSocket = EvaluateBladeSocket(spec, scale, normalized, companionSweep);
                Vector3 companionLateral = companionSocket.WidthAxis;
                Vector3 companionForward = companionSocket.ForwardAxis;
                Vector3 companionUp = companionSocket.GrowthAxis;
                Vector3 companionStemBase = companionSocket.StemBase;
                Vector3 companionAnchor = companionSocket.Anchor;
                float companionWidth = width * Mathf.Lerp(0.42f, 0.62f, 1f - normalized);
                float companionLength = length * Mathf.Lerp(0.46f, 0.68f, 1f - normalized * 0.4f);
                float companionTwist = twist + Mathf.Lerp(-12f, 16f, normalized);
                float companionCurve = sideCurve * 0.55f + Mathf.Lerp(-8f, 8f, normalized);
                float companionSerration = serration * 0.75f;

                AddBladeStem(
                    buffers,
                    companionStemBase,
                    companionAnchor + companionUp * (scale.y * 0.026f) + companionForward * (companionLength * 0.035f),
                    companionSocket.StipeTangentAxis,
                    companionForward,
                    Mathf.Max(scale.x * 0.008f, (companionAnchor - companionStemBase).magnitude * 0.18f),
                    scale.x * Mathf.Lerp(0.009f, 0.0065f, normalized),
                    lod,
                    new Color32(spec.TintByte, 172, 58, 255));
                AddBladeRibbon(
                    buffers,
                    companionAnchor,
                    companionLateral,
                    companionUp,
                    companionWidth,
                    companionLength,
                    companionTwist,
                    Mathf.Max(2, bladeSegments - 1),
                    companionCurve,
                    companionSerration,
                    new Color32((byte)Mathf.Clamp(spec.TintByte + 6, 0, 255), 214, (byte)Mathf.Lerp(56f, 196f, normalized), 255),
                    companionForward);
            }

            if (ShouldAddTertiaryBlade(spec, lod, bladeIndex, normalized))
            {
                float tertiarySweep = primaryAngleOffset + (((bladeIndex & 1) == 0) ? -1f : 1f) * Mathf.Lerp(28f, 44f, normalized);
                BladeSocket tertiarySocket = EvaluateBladeSocket(spec, scale, normalized, tertiarySweep);
                Vector3 tertiaryLateral = tertiarySocket.WidthAxis;
                Vector3 tertiaryForward = tertiarySocket.ForwardAxis;
                Vector3 tertiaryUp = tertiarySocket.GrowthAxis;
                Vector3 tertiaryStemBase = tertiarySocket.StemBase;
                Vector3 tertiaryAnchor = tertiarySocket.Anchor;
                float tertiaryWidth = width * Mathf.Lerp(0.28f, 0.46f, 1f - normalized);
                float tertiaryLength = length * Mathf.Lerp(0.34f, 0.52f, 1f - normalized * 0.28f);
                float tertiaryTwist = twist + Mathf.Lerp(-18f, 22f, normalized);
                float tertiaryCurve = sideCurve * 0.34f + Mathf.Lerp(-12f, 12f, normalized);
                float tertiarySerration = serration * 0.58f;

                AddBladeStem(
                    buffers,
                    tertiaryStemBase,
                    tertiaryAnchor + tertiaryUp * (scale.y * 0.021f) + tertiaryForward * (tertiaryLength * 0.03f),
                    tertiarySocket.StipeTangentAxis,
                    tertiaryForward,
                    Mathf.Max(scale.x * 0.006f, (tertiaryAnchor - tertiaryStemBase).magnitude * 0.14f),
                    scale.x * Mathf.Lerp(0.0075f, 0.0052f, normalized),
                    lod,
                    new Color32(spec.TintByte, 166, 62, 255));
                AddBladeRibbon(
                    buffers,
                    tertiaryAnchor,
                    tertiaryLateral,
                    tertiaryUp,
                    tertiaryWidth,
                    tertiaryLength,
                    tertiaryTwist,
                    Mathf.Max(2, bladeSegments - 2),
                    tertiaryCurve,
                    tertiarySerration,
                    new Color32((byte)Mathf.Clamp(spec.TintByte + 10, 0, 255), 220, (byte)Mathf.Lerp(64f, 188f, normalized), 255),
                    tertiaryForward);
            }
        }

        private static void BuildBulb(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bulbIndex, int bulbCount)
        {
            if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
            {
                if (bulbIndex > 0)
                    return;

                StipeFrame crownFrame = EvaluateStipeFrame(spec, scale, spec.BladeAnchorHeightMax, 0f);
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
                ? EvaluateBladeAngleOffset((bulbIndex * 2) + 1, t)
                : Mathf.Lerp(-8f, 8f, t);
            BladeSocket socket = EvaluateBladeSocket(spec, scale, bladeNormalized, bulbAngleOffset);
            float radius = scale.x * Mathf.Lerp(spec.BulbRadiusMin, spec.BulbRadiusMax, 1f - t * 0.35f);
            int latSegments = Mathf.Max(2, 5 - lod);
            int lonSegments = Mathf.Max(4, 8 - (lod * 2));
            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                Vector3 stipeCenter = socket.StemBase;
                Vector3 nodeBase = Vector3.Lerp(socket.StemBase, socket.Anchor, 0.56f);
                Vector3 bulbAxis = Vector3.Normalize(socket.GrowthAxis * 0.54f + socket.WidthAxis * 0.22f + socket.ForwardAxis * 0.08f);
                Vector3 bulbCenter = nodeBase + bulbAxis * (radius * 0.44f);
                AddBulbStem(
                    buffers,
                    stipeCenter,
                    bulbCenter - bulbAxis * (radius * 0.48f),
                    Mathf.Max(radius * 0.12f, scale.x * 0.026f),
                    radius * 0.09f,
                    lod,
                    new Color32(spec.TintByte, 192, 64, 255));
                AddSphere(buffers, bulbCenter, new Vector3(radius * 0.94f, radius * 1.22f, radius * 0.94f), latSegments, lonSegments, new Color32(spec.TintByte, 224, 118, 255));
                if (lod == 0)
                {
                    AddSphere(
                        buffers,
                        bulbCenter + bulbAxis * (radius * 0.12f) + socket.GrowthAxis * (radius * 0.06f),
                        new Vector3(radius * 0.42f, radius * 0.62f, radius * 0.42f),
                        Mathf.Max(2, latSegments - 1),
                        Mathf.Max(4, lonSegments - 2),
                        new Color32(spec.TintByte, 214, 104, 255));
                }

                return;
            }

            Vector3 offsetDir = Vector3.Normalize(socket.WidthAxis * 0.84f + socket.ForwardAxis * 0.24f);
            Vector3 bulbCenter = socket.Anchor + offsetDir * (radius * 0.42f) + socket.GrowthAxis * (scale.y * 0.018f);
            AddBulbStem(
                buffers,
                socket.StemBase,
                bulbCenter - offsetDir * (radius * 0.52f),
                Mathf.Max(radius * 0.12f, scale.x * 0.026f),
                radius * 0.09f,
                lod,
                new Color32(spec.TintByte, 192, 64, 255));
            AddSphere(buffers, bulbCenter, new Vector3(radius * 0.92f, radius * 1.26f, radius * 0.92f), latSegments, lonSegments, new Color32(spec.TintByte, 224, 118, 255));
            if (lod == 0)
            {
                AddSphere(
                    buffers,
                    bulbCenter + offsetDir * (radius * 0.22f) + Vector3.up * (radius * 0.1f),
                    new Vector3(radius * 0.48f, radius * 0.72f, radius * 0.48f),
                    Mathf.Max(2, latSegments - 1),
                    Mathf.Max(4, lonSegments - 2),
                    new Color32(spec.TintByte, 214, 104, 255));
            }
        }

        private static float EvaluateBladeAngleOffset(int bladeIndex, float sequence)
        {
            float alternating = (bladeIndex & 1) == 0 ? -1f : 1f;
            float goldenAngle = Mathf.Repeat(bladeIndex * 137.50776f, 360f);
            float centeredGolden = goldenAngle > 180f ? goldenAngle - 360f : goldenAngle;
            float noise = Mathf.Sin((bladeIndex + 1) * 1.73f) * Mathf.Lerp(4f, 14f, sequence);
            return centeredGolden * Mathf.Lerp(0.38f, 0.62f, sequence) + alternating * Mathf.Lerp(6f, 18f, sequence) + noise;
        }

        private static bool ShouldAddCompanionBlade(VariantSpec spec, int lod, int bladeIndex, float normalized)
        {
            if (lod > 1)
                return false;

            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
                return lod == 0 || normalized > 0.34f || (bladeIndex % 2 == 0);

            return bladeIndex % 2 == 0 || normalized > 0.55f;
        }

        private static bool ShouldAddTertiaryBlade(VariantSpec spec, int lod, int bladeIndex, float normalized)
        {
            if (lod > 0 || spec.GrowthStyle != GrowthStyle.GiantFrond)
                return false;

            return normalized > 0.28f && (bladeIndex % 3 != 1);
        }

        private static BladeSocket EvaluateBladeSocket(VariantSpec spec, Vector3 scale, float normalized, float angleOffsetDegrees)
        {
            float angle = spec.BladeStartYaw + normalized * spec.BladeYawArc + Mathf.Sin((normalized + 0.13f) * Mathf.PI * 3.1f) * 7f + angleOffsetDegrees;

            if (spec.GrowthStyle == GrowthStyle.CrownCanopy)
            {
                float anchorHeight = Mathf.Lerp(spec.BladeAnchorHeightMin, spec.BladeAnchorHeightMax, Mathf.Lerp(0.72f, 1f, normalized));
                StipeFrame crownFrame = EvaluateStipeFrame(spec, scale, anchorHeight, angle);
                Vector3 crownCenter = crownFrame.Center + Vector3.Normalize(crownFrame.Tangent * 0.82f + Vector3.up * 0.18f) * (scale.y * 0.08f);
                Vector3 widthAxis = crownFrame.Radial;
                Vector3 growthAxis = Vector3.Normalize(widthAxis * 0.46f + crownFrame.Tangent * 0.34f + Vector3.up * 0.2f);
                Vector3 forwardAxis = Vector3.Cross(widthAxis, growthAxis).normalized;
                Vector3 stemBase = crownCenter - widthAxis * (scale.x * 0.06f) - growthAxis * (scale.y * 0.03f);
                Vector3 anchor = crownCenter + widthAxis * (scale.x * 0.08f);
                return new BladeSocket(stemBase, anchor, widthAxis, growthAxis, forwardAxis, crownFrame.Tangent);
            }

            float anchorDistribution;
            if (spec.GrowthStyle == GrowthStyle.GiantFrond)
            {
                float lowerSpread = Mathf.Lerp(normalized, Mathf.Pow(normalized, 0.84f), 0.28f);
                float nodeRhythm = Mathf.Sin((normalized * 3.7f + spec.BendDegrees * 0.015f) * Mathf.PI) * 0.035f;
                anchorDistribution = Mathf.Clamp01(lowerSpread + nodeRhythm);
            }
            else
            {
                anchorDistribution = Mathf.Lerp(normalized, 1f - Mathf.Pow(1f - normalized, 1.85f), 0.72f);
            }

            float anchorHeightAlongStipe = Mathf.Lerp(spec.BladeAnchorHeightMin, spec.BladeAnchorHeightMax, anchorDistribution);
            StipeFrame frame = EvaluateStipeFrame(spec, scale, anchorHeightAlongStipe, angle);
            float helicalSweep = Mathf.Sin((normalized * 2.7f + spec.BendDegrees * 0.01f) * Mathf.PI) * 16f;
            Quaternion sweepRotation = Quaternion.AngleAxis(helicalSweep, frame.Tangent);
            Vector3 width = (sweepRotation * frame.Radial).normalized;
            Vector3 growth = spec.GrowthStyle == GrowthStyle.GiantFrond
                ? Vector3.Normalize(frame.Tangent * 0.58f + Vector3.up * 0.18f + width * 0.24f)
                : Vector3.Normalize(frame.Tangent * 0.66f + Vector3.up * 0.24f + width * 0.1f);
            Vector3 forward = Vector3.Cross(width, growth).normalized;
            float sheathT = spec.GrowthStyle == GrowthStyle.GiantFrond
                ? Mathf.Lerp(0.48f, 0.72f, anchorDistribution)
                : Mathf.Lerp(0.58f, 0.82f, anchorDistribution);
            Vector3 sheathBase = frame.Center
                + width * (frame.Radius * sheathT)
                - frame.Tangent * (scale.y * 0.026f)
                - forward * (scale.x * 0.012f);
            Vector3 stemBaseAlongStipe = Vector3.Lerp(
                frame.Center + width * (frame.Radius * 0.42f),
                sheathBase,
                spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.9f : 0.78f);
            Vector3 anchorAlongStipe = frame.Center
                + width * (frame.Radius * (spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.88f : 0.94f))
                + frame.Tangent * (scale.y * (spec.GrowthStyle == GrowthStyle.GiantFrond ? 0.006f : 0.012f))
                + forward * (scale.x * 0.014f);
            return new BladeSocket(stemBaseAlongStipe, anchorAlongStipe, width, growth, forward, frame.Tangent);
        }

        private static StipeFrame EvaluateStipeFrame(VariantSpec spec, Vector3 scale, float height01, float yawDegrees)
        {
            float v = Mathf.Clamp01(height01 / Mathf.Max(spec.StipeHeightMultiplier, 0.001f));
            Vector3 center = EvaluateStipeCenter(spec, scale, v);
            float sampleDelta = 0.018f;
            float prevV = Mathf.Max(0f, v - sampleDelta);
            float nextV = Mathf.Min(1f, v + sampleDelta);
            Vector3 prevCenter = EvaluateStipeCenter(spec, scale, prevV);
            Vector3 nextCenter = EvaluateStipeCenter(spec, scale, nextV);
            Vector3 tangent = (nextCenter - prevCenter).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.up;

            Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.94f ? Vector3.forward : Vector3.up;
            Vector3 baseNormal = Vector3.Cross(reference, tangent).normalized;
            if (baseNormal.sqrMagnitude < 0.0001f)
                baseNormal = Vector3.right;

            Quaternion aroundTangent = Quaternion.AngleAxis(yawDegrees, tangent);
            Vector3 radial = (aroundTangent * baseNormal).normalized;
            Vector3 binormal = Vector3.Cross(tangent, radial).normalized;
            float radius = EvaluateStipeRadius(spec, scale, v);
            return new StipeFrame(center, tangent, radial, binormal, radius);
        }

        private static Vector3 EvaluateStipeCenter(VariantSpec spec, Vector3 scale, float v)
        {
            float height = scale.y * spec.StipeHeightMultiplier;
            float bendRadians = spec.BendDegrees * Mathf.Deg2Rad * v * v;
            float wobbleX = Mathf.Sin((v * 2.6f + spec.BendDegrees * 0.02f) * Mathf.PI) * scale.x * 0.03f;
            float wobbleZ = Mathf.Sin((v * 4.3f + spec.RibCount * 0.11f) * Mathf.PI) * scale.z * 0.018f;
            return new Vector3(
                Mathf.Sin(bendRadians) * scale.x * spec.BendRadiusMultiplier + wobbleX,
                v * height,
                Mathf.Cos(bendRadians) * scale.z * spec.ForwardOffsetMultiplier - scale.z * spec.ForwardOffsetMultiplier + wobbleZ);
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
            float scarNoise = Mathf.Sin((v * 8.5f + spec.BendDegrees * 0.03f) * Mathf.PI) * 0.035f;
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
                    float rib = 1f + Mathf.Sin(angle * 3f + t * 5.7f) * ribAmplitude;
                    float actualRadius = radius * rib;
                    Vector3 radial = (normalAxis * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)).normalized;
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
            float anchorNoise = Mathf.Sin((anchor.x + anchor.z + length) * 9.7f);
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
                float widthTaper = Mathf.Lerp(1.04f, 0.08f, Mathf.Pow(t, 0.72f));
                float halfWidth = width * widthTaper;
                float edgeWave = serration * Mathf.Sin(t * Mathf.PI * 7f + anchorNoise * 2.4f);
                float edgeWaveSecondary = serration * 0.65f * Mathf.Sin(t * Mathf.PI * 11f + anchorNoise * 1.7f);
                float splitMask = Mathf.Clamp01((t - 0.82f) / 0.18f);
                float tipSplit = halfWidth * splitMask * 0.32f;
                float lateralAsymmetry = halfWidth * asymmetry * Mathf.Lerp(0.35f, 1f, t);
                float curl = centerLift * Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.8f, 0.2f, t);

                Vector3 center = anchor
                    + upDir * (length * t - droop * t * t)
                    + forwardDir * (Mathf.Sin(t * Mathf.PI) * forwardBow + Mathf.Sin((t + 0.17f) * Mathf.PI * 2.0f) * length * 0.015f);

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

        private static void AddBladeRibbon(MeshBuffers buffers, Vector3 anchor, Vector3 widthAxis, Vector3 upAxis, float width, float length, float twistDegrees, int segments, float sideCurveDegrees, float serration, Color32 color, Vector3? forwardHint = null)
        {
            Vector3 widthDir = widthAxis.sqrMagnitude > 0f ? widthAxis.normalized : Vector3.right;
            Vector3 upDir = upAxis.sqrMagnitude > 0f ? upAxis.normalized : Vector3.up;
            Vector3 forwardDir = forwardHint.HasValue && forwardHint.Value.sqrMagnitude > 0f
                ? forwardHint.Value.normalized
                : Vector3.Cross(widthDir, upDir).normalized;
            int startIndex = buffers.Vertices.Count;
            float anchorNoise = Mathf.Sin((anchor.x * 0.73f + anchor.z * 1.11f + length) * 8.4f);
            float asymmetry = anchorNoise * 0.14f;
            float forwardBow = length * Mathf.Lerp(0.09f, 0.18f, Mathf.Abs(anchorNoise));
            float droop = length * Mathf.Lerp(0.08f, 0.18f, Mathf.Abs(anchorNoise));
            float centerLift = width * Mathf.Lerp(0.06f, 0.13f, Mathf.Abs(anchorNoise));

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float baseMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.04f) / 0.18f));
                float tipMask = Mathf.Clamp01((t - 0.78f) / 0.22f);
                float twist = Mathf.Lerp(0f, twistDegrees, t);
                Quaternion rotation = Quaternion.AngleAxis(twist, upDir) * Quaternion.AngleAxis(sideCurveDegrees * t, forwardDir);
                Vector3 rotatedWidth = rotation * widthDir;
                float widthTaper = Mathf.Lerp(1.02f, 0.06f, Mathf.Pow(t, 0.72f));
                float baseNarrow = Mathf.Lerp(0.16f, 1f, baseMask);
                float halfWidth = width * widthTaper * baseNarrow;
                float innerWidth = halfWidth * Mathf.Lerp(0.38f, 0.46f, 1f - tipMask);
                float edgeWave = serration * Mathf.Sin(t * Mathf.PI * 8.2f + anchorNoise * 2.6f) * baseMask;
                float edgeWaveSecondary = serration * 0.55f * Mathf.Sin(t * Mathf.PI * 12.5f + anchorNoise * 1.9f) * baseMask;
                float tipSplit = halfWidth * tipMask * 0.34f;
                float lateralAsymmetry = halfWidth * asymmetry * Mathf.Lerp(0.25f, 1f, t);
                float curl = centerLift * Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.9f, 0.24f, t);
                float baseWrap = (1f - baseMask) * width * 0.22f;

                Vector3 center = anchor
                    + upDir * (length * t - droop * t * t)
                    + forwardDir * (Mathf.Sin(t * Mathf.PI) * forwardBow + Mathf.Sin((t + 0.17f) * Mathf.PI * 2.2f) * length * 0.022f)
                    - rotatedWidth * baseWrap * 0.18f;

                Vector3 normal = Vector3.Cross(rotatedWidth, upDir).normalized;
                if (normal.sqrMagnitude < 0.0001f)
                    normal = Vector3.Cross(rotatedWidth, forwardDir).normalized;

                Vector3 leftOuter = center
                    - rotatedWidth * (halfWidth + edgeWave + tipSplit + lateralAsymmetry)
                    - normal * (curl + edgeWaveSecondary);
                Vector3 leftInner = center
                    - rotatedWidth * (innerWidth + lateralAsymmetry * 0.28f)
                    - normal * (curl * 0.22f);
                Vector3 mid = center + normal * (curl * 0.68f) - upDir * (tipMask * length * 0.032f);
                Vector3 rightInner = center
                    + rotatedWidth * (innerWidth - lateralAsymmetry * 0.22f)
                    + normal * (curl * 0.38f);
                Vector3 rightOuter = center
                    + rotatedWidth * (halfWidth - edgeWave + tipSplit - lateralAsymmetry)
                    + normal * (curl * 0.92f + edgeWaveSecondary);

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
        }

        private static void AddSphere(MeshBuffers buffers, Vector3 center, Vector3 radii, int latSegments, int lonSegments, Color32 color)
        {
            int startIndex = buffers.Vertices.Count;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * TwoPi;
                    Vector3 normal = new Vector3(Mathf.Cos(theta) * cosPhi, sinPhi, Mathf.Sin(theta) * cosPhi).normalized;
                    Vector3 vertex = center + Vector3.Scale(normal, radii);
                    Vector3 tangentDir = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta)).normalized;
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
            switch (rootToken)
            {
                case "family_kelp_tall__stalk": spec = new VariantSpec(10, 14, 13, 10, 5, 0.94f, 0.18f, 0.08f, 4f, 0.05f, 0f, 0f, 0.18f, 0.92f, 0.18f, 0.48f, 0.18f, 0.76f, 0.12f, 0.52f, 0.10f, 10f, -14f, 78f, 18f, 6, 0.03f, 156, 3600, GrowthStyle.GiantFrond); return true;
                case "family_kelp_tall__lean": spec = new VariantSpec(9, 12, 12, 9, 4, 0.88f, 0.18f, 0.08f, 5f, 0.08f, 18f, 0.18f, 0.2f, 0.9f, 0.18f, 0.52f, 0.18f, 0.8f, 0.12f, 0.54f, 0.12f, 16f, -22f, 84f, 22f, 5, 0.035f, 148, 3400, GrowthStyle.GiantFrond); return true;
                case "family_kelp_tall__ribbon": spec = new VariantSpec(8, 13, 14, 11, 6, 0.98f, 0.16f, 0.06f, 5f, 0.1f, 24f, 0.22f, 0.22f, 0.96f, 0.22f, 0.6f, 0.22f, 0.98f, 0.10f, 0.56f, 0.14f, 22f, -28f, 92f, 28f, 4, 0.04f, 164, 4600, GrowthStyle.GiantFrond); return true;
                case "family_kelp_patch_dense__patch": spec = new VariantSpec(8, 11, 11, 12, 5, 0.84f, 0.2f, 0.09f, 4f, 0.06f, 8f, 0.12f, 0.16f, 0.84f, 0.18f, 0.42f, 0.18f, 0.68f, 0.14f, 0.48f, 0.10f, 18f, -72f, 156f, 34f, 6, 0.035f, 144, 4000, GrowthStyle.GiantFrond); return true;
                case "family_kelp_patch_dense__patch_tall": spec = new VariantSpec(9, 12, 12, 13, 5, 0.92f, 0.18f, 0.08f, 4f, 0.05f, 12f, 0.14f, 0.18f, 0.9f, 0.18f, 0.46f, 0.16f, 0.78f, 0.14f, 0.5f, 0.10f, 22f, -64f, 164f, 38f, 7, 0.034f, 150, 4320, GrowthStyle.GiantFrond); return true;
                case "family_kelp_patch_dense__ring": spec = new VariantSpec(8, 11, 11, 14, 4, 0.8f, 0.19f, 0.08f, 4f, 0.06f, 10f, 0.1f, 0.14f, 0.82f, 0.16f, 0.4f, 0.18f, 0.64f, 0.12f, 0.5f, 0.10f, 20f, 0f, 360f, 36f, 7, 0.036f, 146, 4400, GrowthStyle.GiantFrond); return true;
                case "family_kelp_canopy__crown": spec = new VariantSpec(10, 15, 14, 14, 3, 1f, 0.2f, 0.08f, 5f, 0.08f, 12f, 0.1f, 0.42f, 0.84f, 0.24f, 0.62f, 0.26f, 0.98f, 0.12f, 0.56f, 0.12f, 26f, -76f, 180f, 34f, 7, 0.038f, 170, 4600, GrowthStyle.CrownCanopy); return true;
                case "family_kelp_canopy__frond": spec = new VariantSpec(9, 13, 12, 10, 2, 0.92f, 0.18f, 0.07f, 4f, 0.06f, 6f, 0.08f, 0.34f, 0.76f, 0.22f, 0.56f, 0.24f, 0.9f, 0.12f, 0.52f, 0.10f, 18f, -54f, 118f, 32f, 5, 0.03f, 162, 3400, GrowthStyle.CrownCanopy); return true;
                case "family_kelp_canopy__fan": spec = new VariantSpec(10, 14, 13, 14, 2, 0.96f, 0.18f, 0.07f, 5f, 0.08f, 10f, 0.09f, 0.38f, 0.82f, 0.24f, 0.58f, 0.24f, 0.94f, 0.12f, 0.54f, 0.10f, 28f, -92f, 188f, 38f, 6, 0.034f, 174, 4400, GrowthStyle.CrownCanopy); return true;
                default: spec = default; return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
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

        private readonly struct VariantSpec
        {
            public VariantSpec(int stipeSides, int stipeSegments, int bladeSegments, int bladeCount, int bulbCount, float stipeHeightMultiplier, float baseRadiusMultiplier, float topRadiusMultiplier, float ribCount, float ribAmplitude, float bendDegrees, float bendRadiusMultiplier, float bladeAnchorHeightMin, float bladeAnchorHeightMax, float bladeWidthMin, float bladeWidthMax, float bladeLengthMin, float bladeLengthMax, float bladeLengthFalloff, float bladeAnchorRadius, float forwardOffsetMultiplier, float twistDegreesMax, float bladeStartYaw, float bladeYawArc, float sideCurveDegrees, int rootCount, float rootYawOffset, byte tintByte, int estimatedVertexCount, GrowthStyle growthStyle)
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
        }

        private enum GrowthStyle
        {
            GiantFrond = 0,
            CrownCanopy = 1
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
