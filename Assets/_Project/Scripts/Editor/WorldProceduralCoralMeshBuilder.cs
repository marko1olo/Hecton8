using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    internal static class WorldProceduralCoralMeshBuilder
    {
        private const float TwoPi = Mathf.PI * 2f;

        public static bool CanBuild(string rootToken)
        {
            return TryResolveSpec(rootToken, out _);
        }

        public static bool TryBuild(string rootToken, Vector3 scale, int lodLevel, out Mesh mesh)
        {
            mesh = null;
            if (!TryResolveSpec(rootToken, out CoralSpec spec))
                return false;

            int lod = Mathf.Clamp(lodLevel, 0, 2);
            MeshBuffers buffers = new MeshBuffers(spec.EstimatedVertexCount);

            switch (spec.Shape)
            {
                case CoralShape.Low:
                    BuildLow(buffers, spec, scale, lod);
                    break;
                case CoralShape.Branching:
                    BuildBranching(buffers, spec, scale, lod);
                    break;
                case CoralShape.Massive:
                    BuildMassive(buffers, spec, scale, lod);
                    break;
                case CoralShape.Plate:
                    BuildPlate(buffers, spec, scale, lod);
                    break;
                default:
                    return false;
            }

            if (buffers.Indices.Count < 3)
                return false;

            mesh = CreateMesh(rootToken, lod, buffers);
            return mesh != null;
        }

        private static void BuildLow(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod)
        {
            int latSegments = Mathf.Max(lod == 0 ? 9 : 7, spec.LatitudeSegments + (lod == 0 ? 2 : 0) - (lod * 2));
            int lonSegments = Mathf.Max(lod == 0 ? 14 : 10, spec.LongitudeSegments + (lod == 0 ? 4 : 0) - (lod * 4));

            AddWarpedBlob(
                buffers,
                new Vector3(0f, scale.y * 0.26f, 0f),
                new Vector3(scale.x * 0.92f, scale.y * 0.42f, scale.z * 0.88f),
                latSegments,
                lonSegments,
                spec.WarpAmplitude,
                spec.WarpFrequencyA,
                spec.WarpFrequencyB,
                0.55f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.34f, scale.y * 0.16f, scale.z * 0.18f),
                new Vector3(scale.x * 0.44f, scale.y * 0.2f, scale.z * 0.42f),
                latSegments - 1,
                lonSegments - 2,
                spec.WarpAmplitude * 0.78f,
                spec.WarpFrequencyA + 0.8f,
                spec.WarpFrequencyB + 0.5f,
                0.65f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(scale.x * 0.28f, scale.y * 0.14f, -scale.z * 0.22f),
                new Vector3(scale.x * 0.38f, scale.y * 0.18f, scale.z * 0.36f),
                latSegments - 1,
                lonSegments - 2,
                spec.WarpAmplitude * 0.72f,
                spec.WarpFrequencyA + 0.4f,
                spec.WarpFrequencyB + 1.1f,
                0.7f,
                spec.Color);

            if (lod <= 1)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.06f, scale.y * 0.38f, -scale.z * 0.04f),
                    new Vector3(scale.x * 0.26f, scale.y * 0.16f, scale.z * 0.24f),
                    Mathf.Max(6, latSegments - 2),
                    Mathf.Max(10, lonSegments - 4),
                    spec.WarpAmplitude * 0.64f,
                    spec.WarpFrequencyA + 1.3f,
                    spec.WarpFrequencyB + 0.9f,
                    0.62f,
                    spec.Color);
            }

            if (spec.Variant == CoralVariant.Bed)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.08f, scale.y * 0.22f, -scale.z * 0.3f),
                    new Vector3(scale.x * 0.34f, scale.y * 0.12f, scale.z * 0.24f),
                    latSegments - 2,
                    lonSegments - 2,
                    spec.WarpAmplitude * 0.82f,
                    spec.WarpFrequencyA + 1.4f,
                    spec.WarpFrequencyB + 0.8f,
                    0.72f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.2f, scale.y * 0.2f, scale.z * 0.28f),
                    new Vector3(scale.x * 0.28f, scale.y * 0.11f, scale.z * 0.22f),
                    latSegments - 2,
                    lonSegments - 3,
                    spec.WarpAmplitude * 0.76f,
                    spec.WarpFrequencyA + 0.9f,
                    spec.WarpFrequencyB + 1.6f,
                    0.76f,
                    spec.Color);

                AddWarpedPlate(
                    buffers,
                    new Vector3(-scale.x * 0.02f, scale.y * 0.34f, scale.z * 0.04f),
                    Quaternion.Euler(4f, -8f, 10f),
                    scale.x * 0.58f,
                    scale.z * 0.5f,
                    scale.y * 0.042f,
                    Mathf.Max(8, lonSegments - 2),
                    spec.WarpAmplitude * 0.26f,
                    spec.Color);
            }
            else if (spec.Variant == CoralVariant.Plate)
            {
                AddWarpedPlate(
                    buffers,
                    new Vector3(scale.x * 0.04f, scale.y * 0.4f, scale.z * 0.02f),
                    Quaternion.Euler(0f, 12f, 8f),
                    scale.x * 0.86f,
                    scale.z * 0.78f,
                    scale.y * 0.06f,
                    Mathf.Max(10, lonSegments),
                    spec.WarpAmplitude * 0.4f,
                    spec.Color);

                if (lod <= 1)
                {
                    AddWarpedPlate(
                        buffers,
                        new Vector3(-scale.x * 0.18f, scale.y * 0.56f, scale.z * 0.14f),
                        Quaternion.Euler(6f, -18f, -22f),
                        scale.x * 0.58f,
                        scale.z * 0.5f,
                        scale.y * 0.042f,
                        Mathf.Max(12, lonSegments - 2),
                        spec.WarpAmplitude * 0.28f,
                        spec.Color);
                }
            }
            else if (spec.Variant == CoralVariant.Knoll)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(0f, scale.y * 0.48f, scale.z * 0.06f),
                    new Vector3(scale.x * 0.42f, scale.y * 0.22f, scale.z * 0.4f),
                    latSegments - 1,
                    lonSegments - 2,
                    spec.WarpAmplitude * 0.66f,
                    spec.WarpFrequencyA + 1f,
                    spec.WarpFrequencyB + 0.7f,
                    0.48f,
                    spec.Color);

                if (lod == 0)
                {
                    AddWarpedBlob(
                        buffers,
                        new Vector3(-scale.x * 0.18f, scale.y * 0.56f, -scale.z * 0.12f),
                        new Vector3(scale.x * 0.24f, scale.y * 0.14f, scale.z * 0.22f),
                        Mathf.Max(6, latSegments - 2),
                        Mathf.Max(10, lonSegments - 4),
                        spec.WarpAmplitude * 0.58f,
                        spec.WarpFrequencyA + 1.6f,
                        spec.WarpFrequencyB + 0.9f,
                        0.58f,
                        spec.Color);
                }
            }
        }

        private static void BuildBranching(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod)
        {
            int trunkSides = Mathf.Max(6, spec.RadialSegments + (lod == 0 ? 2 : 0) - (lod * 2));
            int trunkSegments = Mathf.Max(5, spec.PathSegments + (lod == 0 ? 2 : 0) - lod);
            int branchCount = Mathf.Max(2, spec.BranchCount + (lod == 0 ? 2 : 0) - lod);
            Vector3[] branchTips = new Vector3[16];
            int branchTipCount = 0;

            AddBezierTube(
                buffers,
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, scale.y * 0.32f, 0f),
                new Vector3(0f, scale.y * 0.72f, 0f),
                new Vector3(0f, scale.y * 1.02f, 0f),
                scale.x * 0.18f,
                scale.x * 0.08f,
                trunkSegments,
                trunkSides,
                spec.Color,
                spec.WarpAmplitude * 0.18f);

            if (spec.Variant == CoralVariant.Mass)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(0f, scale.y * 0.28f, 0f),
                    new Vector3(scale.x * 0.46f, scale.y * 0.24f, scale.z * 0.44f),
                    6,
                    10,
                    spec.WarpAmplitude * 0.52f,
                    spec.WarpFrequencyA,
                    spec.WarpFrequencyB,
                    0.54f,
                    spec.Color);
            }
            else if (lod <= 1)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(0f, scale.y * 0.3f, 0f),
                    new Vector3(scale.x * 0.22f, scale.y * 0.16f, scale.z * 0.2f),
                    lod == 0 ? 6 : 5,
                    lod == 0 ? 10 : 8,
                    spec.WarpAmplitude * 0.28f,
                    spec.WarpFrequencyA + 0.9f,
                    spec.WarpFrequencyB + 0.7f,
                    0.74f,
                    spec.Color);
            }

            float startYaw = spec.Variant == CoralVariant.Fan ? -64f : -32f;
            float yawArc = spec.Variant == CoralVariant.Fan ? 128f : 76f;
            for (int i = 0; i < branchCount; i++)
            {
                float t = branchCount <= 1 ? 0.5f : i / (float)(branchCount - 1);
                float yaw = startYaw + yawArc * t;
                float pitch = Mathf.Lerp(18f, 42f, 1f - Mathf.Abs((t * 2f) - 1f));
                Quaternion branchRotation = Quaternion.Euler(-pitch, yaw, 0f);
                Vector3 outward = branchRotation * Vector3.up;
                Vector3 lateral = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 start = new Vector3(lateral.x * scale.x * 0.08f, scale.y * Mathf.Lerp(0.42f, 0.68f, t), lateral.z * scale.z * 0.08f);
                Vector3 end = start + outward * (scale.y * Mathf.Lerp(0.48f, 0.74f, 1f - (t * 0.22f)));
                Vector3 controlA = Vector3.Lerp(start, end, 0.34f) + lateral * (scale.x * Mathf.Lerp(0.06f, 0.12f, t));
                Vector3 controlB = Vector3.Lerp(start, end, 0.68f) + lateral * (scale.x * Mathf.Lerp(0.12f, 0.18f, t));
                float branchRadiusStart = scale.x * Mathf.Lerp(0.11f, 0.08f, t);
                float branchRadiusEnd = scale.x * Mathf.Lerp(0.038f, 0.026f, t);

                AddBezierTube(
                    buffers,
                    start,
                    controlA,
                    controlB,
                    end,
                    branchRadiusStart,
                    branchRadiusEnd,
                    Mathf.Max(3, trunkSegments - 1),
                    Mathf.Max(4, trunkSides - 1),
                    spec.Color,
                    spec.WarpAmplitude * 0.24f);
                AddBranchTipCluster(buffers, end, branchRadiusEnd, spec.WarpAmplitude, spec.Color, lod);
                AddBranchKnuckle(buffers, start, branchRadiusStart * 1.28f, spec.WarpAmplitude, spec.Color, lod);

                if (branchTipCount < branchTips.Length)
                    branchTips[branchTipCount++] = end;

                if (lod <= 1 && spec.Variant != CoralVariant.Mass)
                {
                    Vector3 subYawAxis = Quaternion.Euler(0f, yaw + Mathf.Lerp(-24f, 24f, t), 0f) * Vector3.right;
                    Vector3 subStart = Vector3.Lerp(start, end, 0.58f);
                    Vector3 subEnd = subStart + (branchRotation * Quaternion.Euler(-12f, 0f, Mathf.Lerp(-26f, 26f, t)) * Vector3.up) * (scale.y * (lod == 0 ? 0.3f : 0.22f));
                    float subRadiusStart = scale.x * (lod == 0 ? 0.048f : 0.038f);
                    float subRadiusEnd = scale.x * (lod == 0 ? 0.017f : 0.013f);
                    AddBezierTube(
                        buffers,
                        subStart,
                        Vector3.Lerp(subStart, subEnd, 0.4f) + subYawAxis * (scale.x * 0.05f),
                        Vector3.Lerp(subStart, subEnd, 0.7f) + subYawAxis * (scale.x * 0.08f),
                        subEnd,
                        subRadiusStart,
                        subRadiusEnd,
                        lod == 0 ? 4 : 3,
                        lod == 0 ? 5 : 4,
                        spec.Color,
                        spec.WarpAmplitude * 0.18f);
                    AddBranchTipCluster(buffers, subEnd, subRadiusEnd, spec.WarpAmplitude * 0.8f, spec.Color, lod);

                    if (lod == 0 && (i % 2 == 0 || spec.Variant == CoralVariant.Fan))
                    {
                        Vector3 twigStart = Vector3.Lerp(subStart, subEnd, 0.62f);
                        Vector3 twigEnd = twigStart + (branchRotation * Quaternion.Euler(-18f, 0f, 26f) * Vector3.up) * (scale.y * 0.16f);
                        AddBezierTube(
                            buffers,
                            twigStart,
                            Vector3.Lerp(twigStart, twigEnd, 0.35f) + subYawAxis * (scale.x * 0.04f),
                            Vector3.Lerp(twigStart, twigEnd, 0.7f) + subYawAxis * (scale.x * 0.06f),
                            twigEnd,
                            scale.x * 0.022f,
                            scale.x * 0.009f,
                            3,
                            4,
                            spec.Color,
                            spec.WarpAmplitude * 0.12f);
                        AddBranchTipCluster(buffers, twigEnd, scale.x * 0.009f, spec.WarpAmplitude * 0.56f, spec.Color, lod);
                    }
                }
            }

            if (spec.Variant == CoralVariant.Fan && branchTipCount >= 3)
                AddFanCrossLinks(buffers, branchTips, branchTipCount, scale, spec, lod);
        }

        private static void BuildMassive(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod)
        {
            int latSegments = Mathf.Max(lod == 0 ? 9 : 7, spec.LatitudeSegments + (lod == 0 ? 2 : 0) - (lod * 2));
            int lonSegments = Mathf.Max(lod == 0 ? 14 : 10, spec.LongitudeSegments + (lod == 0 ? 4 : 0) - (lod * 4));

            AddWarpedBlob(
                buffers,
                new Vector3(0f, scale.y * 0.32f, 0f),
                new Vector3(scale.x * 1.02f, scale.y * 0.62f, scale.z * 0.96f),
                latSegments,
                lonSegments,
                spec.WarpAmplitude,
                spec.WarpFrequencyA,
                spec.WarpFrequencyB,
                0.46f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.24f, scale.y * 0.16f, scale.z * 0.18f),
                new Vector3(scale.x * 0.42f, scale.y * 0.24f, scale.z * 0.4f),
                latSegments - 1,
                lonSegments - 2,
                spec.WarpAmplitude * 0.74f,
                spec.WarpFrequencyA + 0.5f,
                spec.WarpFrequencyB + 0.7f,
                0.54f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(scale.x * 0.28f, scale.y * 0.14f, -scale.z * 0.14f),
                new Vector3(scale.x * 0.38f, scale.y * 0.22f, scale.z * 0.36f),
                latSegments - 1,
                lonSegments - 2,
                spec.WarpAmplitude * 0.7f,
                spec.WarpFrequencyA + 0.9f,
                spec.WarpFrequencyB + 0.4f,
                0.58f,
                spec.Color);

            AddMassiveRidges(buffers, spec, scale, lod);

            if (lod <= 1)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.06f, scale.y * 0.84f, scale.z * 0.04f),
                    new Vector3(scale.x * 0.34f, scale.y * 0.18f, scale.z * 0.32f),
                    Mathf.Max(6, latSegments - 2),
                    Mathf.Max(10, lonSegments - 4),
                    spec.WarpAmplitude * 0.46f,
                    spec.WarpFrequencyA + 1.4f,
                    spec.WarpFrequencyB + 0.8f,
                    0.52f,
                    spec.Color);
            }

            if (spec.Variant == CoralVariant.Boulder)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(0f, scale.y * 0.68f, -scale.z * 0.06f),
                    new Vector3(scale.x * 0.56f, scale.y * 0.26f, scale.z * 0.54f),
                    latSegments - 1,
                    lonSegments - 2,
                    spec.WarpAmplitude * 0.54f,
                    spec.WarpFrequencyA + 1.1f,
                    spec.WarpFrequencyB + 0.2f,
                    0.4f,
                    spec.Color);
            }
            else if (spec.Variant == CoralVariant.Porous)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.1f, scale.y * 0.5f, scale.z * 0.18f),
                    new Vector3(scale.x * 0.28f, scale.y * 0.14f, scale.z * 0.26f),
                    5,
                    8,
                    spec.WarpAmplitude * 1.1f,
                    spec.WarpFrequencyA + 2f,
                    spec.WarpFrequencyB + 1.6f,
                    0.64f,
                    spec.Color);

                if (lod <= 1)
                {
                    AddWarpedBlob(
                        buffers,
                        new Vector3(-scale.x * 0.18f, scale.y * 0.62f, -scale.z * 0.16f),
                        new Vector3(scale.x * 0.22f, scale.y * 0.1f, scale.z * 0.2f),
                        5,
                        8,
                        spec.WarpAmplitude * 1.18f,
                        spec.WarpFrequencyA + 2.4f,
                        spec.WarpFrequencyB + 1.2f,
                        0.7f,
                        spec.Color);
                }
            }
        }

        private static void AddMassiveRidges(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod)
        {
            int ridgeCount = lod == 0 ? 8 : (lod == 1 ? 6 : 4);
            int pathSegments = lod == 0 ? 8 : (lod == 1 ? 6 : 4);
            int radialSegments = lod == 0 ? 6 : (lod == 1 ? 5 : 4);
            float ridgeRadius = scale.x * (lod == 0 ? 0.058f : (lod == 1 ? 0.047f : 0.04f));

            for (int ridgeIndex = 0; ridgeIndex < ridgeCount; ridgeIndex++)
            {
                float t = ridgeCount <= 1 ? 0.5f : ridgeIndex / (float)(ridgeCount - 1);
                float lateral = Mathf.Lerp(-0.62f, 0.62f, t) * scale.x;
                float zWave = Mathf.Sin((t + 0.17f) * Mathf.PI * 2f) * scale.z * 0.12f;
                float crestHeight = scale.y * Mathf.Lerp(0.44f, 0.7f, 1f - Mathf.Abs((t * 2f) - 1f));
                float meander = Mathf.Sin((ridgeIndex + 1) * 1.37f) * scale.x * 0.08f;

                Vector3 start = new Vector3(lateral, scale.y * 0.28f, -scale.z * 0.24f + zWave * 0.5f);
                Vector3 end = new Vector3(lateral * 0.82f + meander, scale.y * 0.46f, scale.z * 0.28f + zWave);
                Vector3 controlA = new Vector3(lateral + meander * 0.35f, crestHeight, -scale.z * 0.1f + zWave);
                Vector3 controlB = new Vector3(lateral * 0.66f - meander * 0.28f, crestHeight + scale.y * 0.04f, scale.z * 0.1f + zWave * 0.65f);

                AddBezierTube(
                    buffers,
                    start,
                    controlA,
                    controlB,
                    end,
                    ridgeRadius,
                    ridgeRadius * 0.72f,
                    pathSegments,
                    radialSegments,
                    spec.Color,
                    spec.WarpAmplitude * 0.16f);
            }

            if (lod == 0)
            {
                for (int diagonalIndex = 0; diagonalIndex < 2; diagonalIndex++)
                {
                    float direction = diagonalIndex == 0 ? -1f : 1f;
                    AddBezierTube(
                        buffers,
                        new Vector3(-scale.x * 0.44f, scale.y * 0.34f, direction * scale.z * 0.2f),
                        new Vector3(-scale.x * 0.18f, scale.y * 0.74f, direction * scale.z * 0.08f),
                        new Vector3(scale.x * 0.14f, scale.y * 0.8f, -direction * scale.z * 0.04f),
                        new Vector3(scale.x * 0.42f, scale.y * 0.46f, -direction * scale.z * 0.18f),
                        scale.x * 0.034f,
                        scale.x * 0.022f,
                        5,
                        4,
                        spec.Color,
                        spec.WarpAmplitude * 0.12f);
                }
            }
        }

        private static void BuildPlate(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod)
        {
            bool isShelf = spec.Variant == CoralVariant.Shelf;
            bool isLedge = spec.Variant == CoralVariant.Ledge;
            bool isStack = spec.Variant == CoralVariant.Stack;
            bool isCanopy = spec.Variant == CoralVariant.Canopy;
            bool isTerrace = spec.Variant == CoralVariant.Terrace;
            bool isBastion = spec.Variant == CoralVariant.Bastion;
            bool isArchitecturalPlate = isCanopy || isTerrace || isBastion;
            int radialSegments = Mathf.Max(lod == 0 ? 16 : 12, spec.LongitudeSegments + (lod == 0 ? 4 : 0) - (lod * 4));
            if ((isShelf || isCanopy) && lod == 0)
                radialSegments += 4;
            if ((isTerrace || isBastion) && lod == 0)
                radialSegments += 4;

            float primaryThickness = isCanopy
                ? scale.y * 0.054f
                : isShelf
                ? scale.y * 0.052f
                : isTerrace
                ? scale.y * 0.062f
                : isBastion
                ? scale.y * 0.066f
                : isStack
                ? scale.y * 0.058f
                : scale.y * 0.068f;
            float secondaryThickness = isCanopy
                ? scale.y * 0.038f
                : isShelf
                ? scale.y * 0.038f
                : isTerrace
                ? scale.y * 0.046f
                : isBastion
                ? scale.y * 0.05f
                : isStack
                ? scale.y * 0.044f
                : scale.y * 0.048f;

            if (isCanopy)
            {
                BuildCanopyPlate(buffers, spec, scale, lod, radialSegments, primaryThickness, secondaryThickness);
                return;
            }

            if (isArchitecturalPlate)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.04f, scale.y * 0.18f, scale.z * 0.02f),
                    new Vector3(scale.x * (isBastion ? 0.34f : 0.28f), scale.y * (isBastion ? 0.22f : 0.2f), scale.z * (isBastion ? 0.3f : 0.26f)),
                    lod == 0 ? 6 : 5,
                    lod == 0 ? 10 : 8,
                    spec.WarpAmplitude * (isBastion ? 0.48f : 0.42f),
                    spec.WarpFrequencyA + 0.5f,
                    spec.WarpFrequencyB + 0.3f,
                    0.7f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.08f, scale.y * 0.38f, -scale.z * 0.04f),
                    new Vector3(scale.x * (isCanopy ? 0.24f : 0.26f), scale.y * 0.14f, scale.z * (isCanopy ? 0.22f : 0.24f)),
                    5,
                    8,
                    spec.WarpAmplitude * 0.34f,
                    spec.WarpFrequencyA + 0.9f,
                    spec.WarpFrequencyB + 0.7f,
                    0.74f,
                    spec.Color);

                if (lod <= 1)
                {
                    AddWarpedBlob(
                        buffers,
                        new Vector3(-scale.x * 0.02f, scale.y * 0.3f, scale.z * 0.01f),
                        new Vector3(scale.x * 0.22f, scale.y * 0.16f, scale.z * 0.2f),
                        5,
                        8,
                        spec.WarpAmplitude * 0.28f,
                        spec.WarpFrequencyA + 1.4f,
                        spec.WarpFrequencyB + 0.9f,
                        0.78f,
                        spec.Color);
                }
            }

            AddBezierTube(
                buffers,
                new Vector3(isArchitecturalPlate ? -scale.x * 0.04f : 0f, 0f, isArchitecturalPlate ? scale.z * 0.02f : 0f),
                new Vector3(
                    isCanopy ? -scale.x * 0.06f : isTerrace ? -scale.x * 0.08f : isBastion ? -scale.x * 0.06f : 0f,
                    scale.y * (isCanopy ? 0.1f : isArchitecturalPlate ? 0.14f : 0.18f),
                    isCanopy ? scale.z * 0.06f : isArchitecturalPlate ? scale.z * 0.04f : 0f),
                new Vector3(
                    isCanopy ? scale.x * 0.01f : isTerrace ? scale.x * 0.02f : isBastion ? scale.x * 0.08f : scale.x * 0.04f,
                    scale.y * (isCanopy ? 0.2f : isArchitecturalPlate ? 0.28f : 0.36f),
                    isCanopy ? -scale.z * 0.04f : isBastion ? -scale.z * 0.06f : scale.z * 0.02f),
                new Vector3(
                    isCanopy ? -scale.x * 0.01f : isTerrace ? -scale.x * 0.02f : isBastion ? scale.x * 0.04f : 0f,
                    scale.y * (isCanopy ? 0.34f : isArchitecturalPlate ? 0.46f : 0.58f),
                    isCanopy ? -scale.z * 0.02f : isTerrace ? scale.z * 0.02f : isBastion ? -scale.z * 0.04f : 0f),
                isCanopy ? scale.x * 0.24f : isArchitecturalPlate ? scale.x * 0.18f : scale.x * 0.15f,
                isCanopy ? scale.x * 0.12f : isArchitecturalPlate ? scale.x * 0.092f : scale.x * 0.08f,
                Mathf.Max(4, spec.PathSegments - lod),
                Mathf.Max(5, spec.RadialSegments - lod),
                spec.Color,
                spec.WarpAmplitude * (isCanopy ? 0.22f : isArchitecturalPlate ? 0.18f : 0.12f));

            AddWarpedPlate(
                buffers,
                new Vector3(
                    isCanopy ? -scale.x * 0.08f : isTerrace ? -scale.x * 0.12f : isBastion ? -scale.x * 0.08f : 0f,
                    scale.y * (isCanopy ? 0.48f : isTerrace ? 0.56f : isBastion ? 0.58f : 0.6f),
                    isCanopy ? scale.z * 0.02f : isTerrace ? scale.z * 0.04f : isBastion ? scale.z * 0.02f : 0f),
                Quaternion.Euler(
                    isCanopy ? -14f : isShelf ? -6f : isLedge ? -2f : isTerrace ? -10f : isBastion ? 2f : 2f,
                    isCanopy ? 18f : isShelf ? 8f : isLedge ? 14f : isTerrace ? 12f : isBastion ? 22f : 10f,
                    isCanopy ? 20f : isShelf ? 14f : isLedge ? 10f : isTerrace ? 10f : isBastion ? 18f : 6f),
                scale.x * (isCanopy ? 1.28f : isShelf ? 1.12f : isLedge ? 1.08f : isTerrace ? 1.34f : isBastion ? 1.42f : 1.04f),
                scale.z * (isCanopy ? 1.08f : isShelf ? 0.98f : isLedge ? 0.94f : isTerrace ? 1.12f : isBastion ? 1.18f : 0.9f),
                primaryThickness,
                radialSegments,
                spec.WarpAmplitude * (isCanopy ? 0.62f : isShelf ? 0.52f : isLedge ? 0.44f : isTerrace ? 0.68f : isBastion ? 0.64f : 0.4f),
                spec.Color);

            AddWarpedPlate(
                buffers,
                new Vector3(
                    isCanopy ? -scale.x * 0.14f : isTerrace ? -scale.x * 0.2f : isBastion ? -scale.x * 0.18f : -scale.x * 0.16f,
                    scale.y * (isCanopy ? 0.68f : isTerrace ? 0.84f : isBastion ? 0.86f : 0.9f),
                    isCanopy ? scale.z * 0.14f : isTerrace ? scale.z * 0.18f : isBastion ? scale.z * 0.12f : scale.z * 0.14f),
                Quaternion.Euler(
                    isCanopy ? -6f : isShelf ? 1f : isLedge ? 10f : isTerrace ? 2f : isBastion ? 0f : 7f,
                    isCanopy ? 12f : isShelf ? 18f : isLedge ? 20f : isTerrace ? 20f : isBastion ? 26f : 14f,
                    isCanopy ? -28f : isShelf ? -32f : isLedge ? -24f : isTerrace ? -28f : isBastion ? -34f : -18f),
                scale.x * (isCanopy ? 0.9f : isShelf ? 0.94f : isLedge ? 0.88f : isTerrace ? 1.02f : isBastion ? 1.08f : 0.82f),
                scale.z * (isCanopy ? 0.76f : isShelf ? 0.8f : isLedge ? 0.76f : isTerrace ? 0.84f : isBastion ? 0.88f : 0.72f),
                secondaryThickness,
                radialSegments - 2,
                spec.WarpAmplitude * (isCanopy ? 0.42f : isShelf ? 0.46f : isLedge ? 0.4f : isTerrace ? 0.48f : isBastion ? 0.5f : 0.34f),
                spec.Color);

            if (lod <= 1)
            {
                AddWarpedPlate(
                    buffers,
                    new Vector3(
                        isCanopy ? scale.x * 0.06f : isTerrace ? scale.x * 0.16f : isBastion ? scale.x * 0.1f : scale.x * 0.08f,
                        scale.y * (isCanopy ? 0.62f : isTerrace ? 0.76f : isBastion ? 0.8f : 0.78f),
                        isCanopy ? -scale.z * 0.12f : isTerrace ? -scale.z * 0.16f : isBastion ? -scale.z * 0.14f : -scale.z * 0.12f),
                    Quaternion.Euler(isCanopy ? -18f : isTerrace ? -12f : isBastion ? -14f : -6f, isCanopy ? -22f : isTerrace ? -18f : isBastion ? -20f : -12f, isCanopy ? 30f : isTerrace ? 24f : isBastion ? 26f : 18f),
                    scale.x * (isCanopy ? 0.74f : isTerrace ? 0.86f : isBastion ? 0.9f : 0.7f),
                    scale.z * (isCanopy ? 0.58f : isTerrace ? 0.64f : isBastion ? 0.7f : 0.58f),
                    scale.y * (isCanopy ? 0.036f : isTerrace ? 0.042f : isBastion ? 0.044f : 0.038f),
                    Mathf.Max(12, radialSegments - 3),
                    spec.WarpAmplitude * (isCanopy ? 0.38f : isArchitecturalPlate ? 0.34f : 0.26f),
                    spec.Color);

                if (isCanopy)
                {
                    AddWarpedBlob(
                        buffers,
                        new Vector3(scale.x * 0.08f, scale.y * 0.5f, scale.z * 0.06f),
                        new Vector3(scale.x * 0.18f, scale.y * 0.12f, scale.z * 0.16f),
                        5,
                        8,
                        spec.WarpAmplitude * 0.24f,
                        spec.WarpFrequencyA + 1.1f,
                        spec.WarpFrequencyB + 0.6f,
                        0.8f,
                        spec.Color);
                }
                else
                {
                    AddWarpedPlate(
                        buffers,
                        new Vector3(
                            isTerrace ? scale.x * 0.18f : isBastion ? scale.x * 0.24f : scale.x * 0.1f,
                            scale.y * (isTerrace ? 0.6f : isBastion ? 0.64f : 0.5f),
                            isTerrace ? scale.z * 0.12f : isBastion ? scale.z * 0.1f : scale.z * 0.12f),
                        Quaternion.Euler(isTerrace ? 10f : isBastion ? 6f : 14f, isTerrace ? -24f : isBastion ? -30f : -18f, isShelf ? 22f : isTerrace ? 28f : isBastion ? 32f : 16f),
                        scale.x * (isShelf ? 0.54f : isTerrace ? 0.7f : isBastion ? 0.76f : 0.46f),
                        scale.z * (isShelf ? 0.42f : isTerrace ? 0.5f : isBastion ? 0.54f : 0.36f),
                        scale.y * (isArchitecturalPlate ? 0.032f : 0.026f),
                        Mathf.Max(10, radialSegments - 7),
                        spec.WarpAmplitude * (isArchitecturalPlate ? 0.28f : 0.22f),
                        spec.Color);
                }
            }

            if (isCanopy)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.04f, scale.y * 0.46f, scale.z * 0.04f),
                    new Vector3(scale.x * 0.32f, scale.y * 0.2f, scale.z * 0.28f),
                    6,
                    10,
                    spec.WarpAmplitude * 0.48f,
                    spec.WarpFrequencyA + 0.8f,
                    spec.WarpFrequencyB + 0.5f,
                    0.72f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.02f, scale.y * 0.56f, scale.z * 0.02f),
                    new Vector3(scale.x * 0.22f, scale.y * 0.12f, scale.z * 0.18f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.22f,
                    spec.WarpFrequencyA + 1.2f,
                    spec.WarpFrequencyB + 0.7f,
                    0.78f,
                    spec.Color);

                AddWarpedPlate(
                    buffers,
                    new Vector3(scale.x * 0.16f, scale.y * 0.84f, -scale.z * 0.04f),
                    Quaternion.Euler(-10f, -24f, 28f),
                    scale.x * 0.8f,
                    scale.z * 0.62f,
                    scale.y * 0.036f,
                    radialSegments - 5,
                    spec.WarpAmplitude * 0.34f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.16f, scale.y * 0.62f, -scale.z * 0.02f),
                    new Vector3(scale.x * 0.24f, scale.y * 0.12f, scale.z * 0.2f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.28f,
                    spec.WarpFrequencyA + 1.3f,
                    spec.WarpFrequencyB + 0.9f,
                    0.76f,
                    spec.Color);

                if (lod == 0)
                {
                    AddWarpedPlate(
                        buffers,
                        new Vector3(-scale.x * 0.34f, scale.y * 0.72f, scale.z * 0.18f),
                        Quaternion.Euler(18f, 38f, -42f),
                        scale.x * 0.72f,
                        scale.z * 0.5f,
                        scale.y * 0.03f,
                        radialSegments - 8,
                        spec.WarpAmplitude * 0.34f,
                        spec.Color);
                }
            }

            if (isShelf || isStack)
            {
                AddWarpedPlate(
                    buffers,
                    new Vector3(scale.x * 0.2f, scale.y * 1.14f, -scale.z * 0.1f),
                    Quaternion.Euler(-8f, -18f, 22f),
                    scale.x * 0.72f,
                    scale.z * 0.64f,
                    scale.y * 0.045f,
                    radialSegments - 4,
                    spec.WarpAmplitude * 0.28f,
                    spec.Color);
            }

            if (isShelf)
            {
                AddWarpedPlate(
                    buffers,
                    new Vector3(scale.x * 0.3f, scale.y * 0.82f, -scale.z * 0.08f),
                    Quaternion.Euler(-14f, -22f, 28f),
                    scale.x * 0.66f,
                    scale.z * 0.56f,
                    scale.y * 0.034f,
                    radialSegments - 5,
                    spec.WarpAmplitude * 0.32f,
                    spec.Color);

                AddWarpedPlate(
                    buffers,
                    new Vector3(-scale.x * 0.26f, scale.y * 0.74f, scale.z * 0.18f),
                    Quaternion.Euler(10f, 34f, -36f),
                    scale.x * 0.44f,
                    scale.z * 0.34f,
                    scale.y * 0.028f,
                    radialSegments - 7,
                    spec.WarpAmplitude * 0.26f,
                    spec.Color);

                AddBezierTube(
                    buffers,
                    new Vector3(scale.x * 0.22f, scale.y * 0.16f, -scale.z * 0.14f),
                    new Vector3(scale.x * 0.24f, scale.y * 0.34f, -scale.z * 0.1f),
                    new Vector3(scale.x * 0.28f, scale.y * 0.58f, -scale.z * 0.06f),
                    new Vector3(scale.x * 0.26f, scale.y * 0.82f, -scale.z * 0.04f),
                    scale.x * 0.08f,
                    scale.x * 0.045f,
                    Mathf.Max(3, spec.PathSegments - 1),
                    Mathf.Max(4, spec.RadialSegments - 1),
                    spec.Color,
                    spec.WarpAmplitude * 0.12f);

                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.08f, scale.y * 0.46f, scale.z * 0.04f),
                    new Vector3(scale.x * 0.16f, scale.y * 0.18f, scale.z * 0.14f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.3f,
                    spec.WarpFrequencyA + 0.7f,
                    spec.WarpFrequencyB + 0.5f,
                    0.74f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(scale.x * 0.18f, scale.y * 0.68f, -scale.z * 0.1f),
                    new Vector3(scale.x * 0.14f, scale.y * 0.11f, scale.z * 0.12f),
                    4,
                    6,
                    spec.WarpAmplitude * 0.24f,
                    spec.WarpFrequencyA + 1.1f,
                    spec.WarpFrequencyB + 0.3f,
                    0.8f,
                    spec.Color);
            }
            else if (isLedge)
            {
                AddWarpedPlate(
                    buffers,
                    new Vector3(scale.x * 0.24f, scale.y * 0.72f, -scale.z * 0.18f),
                    Quaternion.Euler(-12f, -24f, 34f),
                    scale.x * 0.54f,
                    scale.z * 0.42f,
                    scale.y * 0.04f,
                    radialSegments - 3,
                    spec.WarpAmplitude * 0.24f,
                    spec.Color);

                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.1f, scale.y * 0.44f, scale.z * 0.08f),
                    new Vector3(scale.x * 0.18f, scale.y * 0.14f, scale.z * 0.16f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.42f,
                    spec.WarpFrequencyA + 0.8f,
                    spec.WarpFrequencyB + 0.6f,
                    0.8f,
                    spec.Color);

                AddWarpedPlate(
                    buffers,
                    new Vector3(-scale.x * 0.26f, scale.y * 1.02f, scale.z * 0.1f),
                    Quaternion.Euler(6f, 28f, -30f),
                    scale.x * 0.68f,
                    scale.z * 0.52f,
                    scale.y * 0.034f,
                    radialSegments - 5,
                    spec.WarpAmplitude * 0.28f,
                    spec.Color);

                AddBezierTube(
                    buffers,
                    new Vector3(-scale.x * 0.18f, scale.y * 0.18f, scale.z * 0.06f),
                    new Vector3(-scale.x * 0.24f, scale.y * 0.36f, scale.z * 0.08f),
                    new Vector3(-scale.x * 0.3f, scale.y * 0.66f, scale.z * 0.1f),
                    new Vector3(-scale.x * 0.24f, scale.y * 0.96f, scale.z * 0.12f),
                    scale.x * 0.078f,
                    scale.x * 0.038f,
                    Mathf.Max(3, spec.PathSegments - 1),
                    Mathf.Max(4, spec.RadialSegments - 1),
                    spec.Color,
                    spec.WarpAmplitude * 0.1f);
            }
            else if (isStack || isTerrace || isBastion)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.02f, scale.y * (isBastion ? 0.58f : 0.54f), scale.z * 0.02f),
                    new Vector3(scale.x * (isBastion ? 0.26f : 0.22f), scale.y * 0.14f, scale.z * (isBastion ? 0.24f : 0.2f)),
                    5,
                    8,
                    spec.WarpAmplitude * 0.32f,
                    spec.WarpFrequencyA + 1.1f,
                    spec.WarpFrequencyB + 0.4f,
                    0.76f,
                    spec.Color);

                if (isTerrace || isBastion)
                {
                    AddWarpedFanPlate(
                        buffers,
                        new Vector3(isBastion ? -scale.x * 0.08f : -scale.x * 0.14f, scale.y * (isBastion ? 0.96f : 0.98f), isBastion ? scale.z * 0.04f : scale.z * 0.12f),
                        Quaternion.Euler(isTerrace ? 2f : 6f, isBastion ? 18f : 12f, isTerrace ? -16f : -22f),
                        scale.x * (isBastion ? 0.94f : 0.88f),
                        scale.z * (isBastion ? 0.78f : 0.72f),
                        scale.y * (isBastion ? 0.056f : 0.052f),
                        radialSegments - 6,
                        spec.WarpAmplitude * (isBastion ? 0.38f : 0.34f),
                        isTerrace ? 8f : -12f,
                        isTerrace ? 208f : 182f,
                        spec.Color);
                }
                else
                {
                    AddWarpedPlate(
                        buffers,
                        new Vector3(-scale.x * 0.1f, scale.y * 1.34f, scale.z * 0.08f),
                        Quaternion.Euler(8f, 10f, -16f),
                        scale.x * 0.56f,
                        scale.z * 0.5f,
                        scale.y * 0.04f,
                        radialSegments - 6,
                        spec.WarpAmplitude * 0.22f,
                        spec.Color);
                }

                if (lod == 0)
                {
                    if (isTerrace || isBastion)
                    {
                        AddWarpedFanPlate(
                            buffers,
                            new Vector3(scale.x * (isBastion ? 0.12f : 0.08f), scale.y * (isBastion ? 1.22f : 1.14f), isBastion ? -scale.z * 0.08f : -scale.z * 0.1f),
                            Quaternion.Euler(isTerrace ? -6f : -8f, isBastion ? -14f : -20f, isTerrace ? 20f : 26f),
                            scale.x * (isBastion ? 0.72f : 0.62f),
                            scale.z * (isBastion ? 0.56f : 0.48f),
                            scale.y * (isBastion ? 0.042f : 0.036f),
                            radialSegments - 8,
                            spec.WarpAmplitude * (isBastion ? 0.3f : 0.24f),
                            isTerrace ? -24f : -36f,
                            isTerrace ? 156f : 142f,
                            spec.Color);
                    }
                    else
                    {
                        AddWarpedPlate(
                            buffers,
                            new Vector3(scale.x * 0.14f, scale.y * 1.52f, -scale.z * 0.1f),
                            Quaternion.Euler(-4f, -16f, 18f),
                            scale.x * 0.42f,
                            scale.z * 0.36f,
                            scale.y * 0.028f,
                            radialSegments - 8,
                            spec.WarpAmplitude * 0.18f,
                            spec.Color);
                    }
                }

                if (lod <= 1)
                {
                    if (isTerrace || isBastion)
                    {
                        AddWarpedFanPlate(
                            buffers,
                            new Vector3(scale.x * 0.28f, scale.y * (isBastion ? 0.82f : 0.86f), -scale.z * (isBastion ? 0.1f : 0.18f)),
                            Quaternion.Euler(isTerrace ? -10f : -12f, isBastion ? -18f : -24f, isTerrace ? 24f : 30f),
                            scale.x * (isBastion ? 0.72f : 0.66f),
                            scale.z * (isBastion ? 0.5f : 0.46f),
                            scale.y * (isBastion ? 0.04f : 0.036f),
                            radialSegments - 7,
                            spec.WarpAmplitude * (isBastion ? 0.3f : 0.28f),
                            isTerrace ? 24f : -18f,
                            isTerrace ? 214f : 168f,
                            spec.Color);
                    }
                    else
                    {
                        AddWarpedPlate(
                            buffers,
                            new Vector3(scale.x * 0.28f, scale.y * 0.82f, -scale.z * 0.1f),
                            Quaternion.Euler(-12f, -18f, 30f),
                            scale.x * 0.72f,
                            scale.z * 0.5f,
                            scale.y * 0.04f,
                            radialSegments - 7,
                            spec.WarpAmplitude * 0.3f,
                            spec.Color);
                    }
                }
            }

            if (lod == 0 && !isArchitecturalPlate)
            {
                AddBezierTube(
                    buffers,
                    new Vector3(scale.x * 0.06f, scale.y * 0.16f, -scale.z * 0.04f),
                    new Vector3(scale.x * 0.08f, scale.y * 0.34f, -scale.z * 0.02f),
                    new Vector3(scale.x * 0.12f, scale.y * 0.54f, 0f),
                    new Vector3(scale.x * 0.16f, scale.y * 0.72f, scale.z * 0.04f),
                    scale.x * 0.06f,
                    scale.x * 0.028f,
                    Mathf.Max(3, spec.PathSegments - 1),
                    Mathf.Max(4, spec.RadialSegments - 2),
                    spec.Color,
                    spec.WarpAmplitude * 0.08f);
            }
        }

        private static void BuildCanopyPlate(MeshBuffers buffers, CoralSpec spec, Vector3 scale, int lod, int radialSegments, float primaryThickness, float secondaryThickness)
        {
            int pathSegments = Mathf.Max(4, spec.PathSegments - lod);
            int radialTubeSegments = Mathf.Max(5, spec.RadialSegments - lod);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.1f, scale.y * 0.18f, scale.z * 0.03f),
                new Vector3(scale.x * 0.48f, scale.y * 0.28f, scale.z * 0.38f),
                lod == 0 ? 7 : 5,
                lod == 0 ? 12 : 8,
                spec.WarpAmplitude * 0.54f,
                spec.WarpFrequencyA + 0.45f,
                spec.WarpFrequencyB + 0.3f,
                0.66f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(scale.x * 0.1f, scale.y * 0.26f, -scale.z * 0.06f),
                new Vector3(scale.x * 0.28f, scale.y * 0.15f, scale.z * 0.24f),
                5,
                8,
                spec.WarpAmplitude * 0.3f,
                spec.WarpFrequencyA + 1.1f,
                spec.WarpFrequencyB + 0.5f,
                0.74f,
                spec.Color);

            AddBezierTube(
                buffers,
                new Vector3(-scale.x * 0.16f, 0f, scale.z * 0.02f),
                new Vector3(-scale.x * 0.22f, scale.y * 0.08f, scale.z * 0.04f),
                new Vector3(-scale.x * 0.3f, scale.y * 0.18f, scale.z * 0.02f),
                new Vector3(-scale.x * 0.34f, scale.y * 0.28f, -scale.z * 0.01f),
                scale.x * 0.22f,
                scale.x * 0.11f,
                pathSegments,
                radialTubeSegments,
                spec.Color,
                spec.WarpAmplitude * 0.16f);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.32f, scale.y * 0.32f, 0f),
                new Vector3(scale.x * 0.28f, scale.y * 0.18f, scale.z * 0.24f),
                5,
                8,
                spec.WarpAmplitude * 0.28f,
                spec.WarpFrequencyA + 0.8f,
                spec.WarpFrequencyB + 0.6f,
                0.78f,
                spec.Color);

            AddWarpedFanPlate(
                buffers,
                new Vector3(-scale.x * 0.3f, scale.y * 0.5f, scale.z * 0.04f),
                Quaternion.Euler(-14f, 12f, 22f),
                scale.x * 1.18f,
                scale.z * 0.9f,
                primaryThickness,
                radialSegments + (lod == 0 ? 4 : 1),
                spec.WarpAmplitude * 0.72f,
                32f,
                192f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.22f, scale.y * 0.44f, scale.z * 0.08f),
                new Vector3(scale.x * 0.34f, scale.y * 0.18f, scale.z * 0.28f),
                5,
                8,
                spec.WarpAmplitude * 0.26f,
                spec.WarpFrequencyA + 1.3f,
                spec.WarpFrequencyB + 0.8f,
                0.76f,
                spec.Color);

            AddWarpedFanPlate(
                buffers,
                new Vector3(-scale.x * 0.18f, scale.y * 0.66f, -scale.z * 0.02f),
                Quaternion.Euler(-2f, 8f, 18f),
                scale.x * 0.78f,
                scale.z * 0.58f,
                secondaryThickness,
                Mathf.Max(14, radialSegments - 4),
                spec.WarpAmplitude * 0.46f,
                46f,
                174f,
                spec.Color);

            AddWarpedBlob(
                buffers,
                new Vector3(-scale.x * 0.2f, scale.y * 0.58f, scale.z * 0.02f),
                new Vector3(scale.x * 0.26f, scale.y * 0.16f, scale.z * 0.2f),
                5,
                8,
                spec.WarpAmplitude * 0.22f,
                spec.WarpFrequencyA + 1.6f,
                spec.WarpFrequencyB + 0.7f,
                0.8f,
                spec.Color);

            if (lod <= 1)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.42f, scale.y * 0.32f, scale.z * 0.12f),
                    new Vector3(scale.x * 0.18f, scale.y * 0.12f, scale.z * 0.16f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.22f,
                    spec.WarpFrequencyA + 1.9f,
                    spec.WarpFrequencyB + 0.9f,
                    0.82f,
                    spec.Color);
            }

            if (lod == 0)
            {
                AddWarpedBlob(
                    buffers,
                    new Vector3(-scale.x * 0.3f, scale.y * 0.26f, scale.z * 0.18f),
                    new Vector3(scale.x * 0.2f, scale.y * 0.09f, scale.z * 0.18f),
                    5,
                    8,
                    spec.WarpAmplitude * 0.16f,
                    spec.WarpFrequencyA + 1.7f,
                    spec.WarpFrequencyB + 0.8f,
                    0.84f,
                    spec.Color);
            }
        }

        private static void AddBezierTube(MeshBuffers buffers, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float radiusStart, float radiusEnd, int pathSegments, int radialSegments, Color32 color, float ribAmplitude)
        {
            int startIndex = buffers.Vertices.Count;
            Vector3 prevNormal = Vector3.up;

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
                    float rib = 1f + Mathf.Sin(angle * 3f + t * 6f) * ribAmplitude;
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

        private static void AddWarpedPlate(MeshBuffers buffers, Vector3 center, Quaternion rotation, float radiusX, float radiusZ, float thickness, int radialSegments, float wobble, Color32 color)
        {
            int centerTopIndex = buffers.Vertices.Count;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            buffers.AddVertex(center + up * (thickness * 0.5f), up, new Vector4(right.x, right.y, right.z, 1f), new Vector2(0.5f, 0.5f), color);
            buffers.AddVertex(center - up * (thickness * 0.5f), -up, new Vector4(right.x, right.y, right.z, 1f), new Vector2(0.5f, 0.5f), color);

            for (int i = 0; i <= radialSegments; i++)
            {
                float u = i / (float)radialSegments;
                float angle = u * TwoPi;
                float wave = Mathf.Sin(angle * 3f) * wobble + Mathf.Cos(angle * 5f) * wobble * 0.45f;
                float edgeDrop = Mathf.Sin(angle * 2f + 0.6f) * thickness * 0.22f;
                Vector3 radial = right * (Mathf.Cos(angle) * (radiusX + wave)) + forward * (Mathf.Sin(angle) * (radiusZ + wave * 0.76f));
                Vector3 topVertex = center + radial + up * (thickness * 0.5f + edgeDrop);
                Vector3 bottomVertex = center + radial - up * (thickness * 0.5f - edgeDrop * 0.35f);
                Vector3 rimNormal = (radial.normalized + up * 0.18f).normalized;
                Vector4 tangent = new Vector4(right.x, right.y, right.z, 1f);

                buffers.AddVertex(topVertex, up, tangent, new Vector2((Mathf.Cos(angle) + 1f) * 0.5f, (Mathf.Sin(angle) + 1f) * 0.5f), color);
                buffers.AddVertex(bottomVertex, -up, tangent, new Vector2((Mathf.Cos(angle) + 1f) * 0.5f, (Mathf.Sin(angle) + 1f) * 0.5f), color);
                buffers.AddVertex(topVertex, rimNormal, tangent, new Vector2(u, 1f), color);
                buffers.AddVertex(bottomVertex, rimNormal, tangent, new Vector2(u, 0f), color);
            }

            for (int i = 0; i < radialSegments; i++)
            {
                int ringStart = centerTopIndex + 2 + i * 4;
                int nextRingStart = ringStart + 4;
                buffers.AddTriangle(centerTopIndex, ringStart, nextRingStart);
                buffers.AddTriangle(centerTopIndex + 1, nextRingStart + 1, ringStart + 1);
                buffers.AddQuad(ringStart + 2, nextRingStart + 2, nextRingStart + 3, ringStart + 3);
            }
        }

        private static void AddWarpedFanPlate(MeshBuffers buffers, Vector3 center, Quaternion rotation, float radiusX, float radiusZ, float thickness, int radialSegments, float wobble, float startAngleDeg, float endAngleDeg, Color32 color)
        {
            int centerTopIndex = buffers.Vertices.Count;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            Vector4 tangent = new Vector4(right.x, right.y, right.z, 1f);

            buffers.AddVertex(center + up * (thickness * 0.5f), up, tangent, new Vector2(0.5f, 0.5f), color);
            buffers.AddVertex(center - up * (thickness * 0.5f), -up, tangent, new Vector2(0.5f, 0.5f), color);

            float startAngle = startAngleDeg * Mathf.Deg2Rad;
            float endAngle = endAngleDeg * Mathf.Deg2Rad;
            for (int i = 0; i <= radialSegments; i++)
            {
                float u = i / (float)radialSegments;
                float angle = Mathf.Lerp(startAngle, endAngle, u);
                float wave = Mathf.Sin(angle * 3f) * wobble + Mathf.Cos(angle * 5f) * wobble * 0.45f;
                float edgeDrop = Mathf.Sin(angle * 2f + 0.6f) * thickness * 0.22f;
                Vector3 radial = right * (Mathf.Cos(angle) * (radiusX + wave)) + forward * (Mathf.Sin(angle) * (radiusZ + wave * 0.76f));
                Vector3 topVertex = center + radial + up * (thickness * 0.5f + edgeDrop);
                Vector3 bottomVertex = center + radial - up * (thickness * 0.5f - edgeDrop * 0.35f);
                Vector3 rimNormal = (radial.normalized + up * 0.18f).normalized;

                buffers.AddVertex(topVertex, up, tangent, new Vector2((Mathf.Cos(angle) + 1f) * 0.5f, (Mathf.Sin(angle) + 1f) * 0.5f), color);
                buffers.AddVertex(bottomVertex, -up, tangent, new Vector2((Mathf.Cos(angle) + 1f) * 0.5f, (Mathf.Sin(angle) + 1f) * 0.5f), color);
                buffers.AddVertex(topVertex, rimNormal, tangent, new Vector2(u, 1f), color);
                buffers.AddVertex(bottomVertex, rimNormal, tangent, new Vector2(u, 0f), color);
            }

            for (int i = 0; i < radialSegments; i++)
            {
                int ringStart = centerTopIndex + 2 + i * 4;
                int nextRingStart = ringStart + 4;
                buffers.AddTriangle(centerTopIndex, ringStart, nextRingStart);
                buffers.AddTriangle(centerTopIndex + 1, nextRingStart + 1, ringStart + 1);
                buffers.AddQuad(ringStart + 2, nextRingStart + 2, nextRingStart + 3, ringStart + 3);
            }

            AddFanPlateEdgeCap(buffers, centerTopIndex, centerTopIndex + 2, false, tangent, color);
            AddFanPlateEdgeCap(buffers, centerTopIndex, centerTopIndex + 2 + radialSegments * 4, true, tangent, color);
        }

        private static void AddFanPlateEdgeCap(MeshBuffers buffers, int centerTopIndex, int ringStartIndex, bool flipNormal, Vector4 tangent, Color32 color)
        {
            Vector3 centerTop = buffers.Vertices[centerTopIndex];
            Vector3 centerBottom = buffers.Vertices[centerTopIndex + 1];
            Vector3 edgeTop = buffers.Vertices[ringStartIndex];
            Vector3 edgeBottom = buffers.Vertices[ringStartIndex + 1];
            Vector3 edgeDirection = (edgeTop - centerTop).normalized;
            Vector3 edgeNormal = Vector3.Cross(edgeDirection, centerTop - centerBottom).normalized;
            if (flipNormal)
                edgeNormal = -edgeNormal;

            int edgeStartIndex = buffers.Vertices.Count;
            buffers.AddVertex(centerTop, edgeNormal, tangent, new Vector2(0f, 1f), color);
            buffers.AddVertex(centerBottom, edgeNormal, tangent, new Vector2(0f, 0f), color);
            buffers.AddVertex(edgeTop, edgeNormal, tangent, new Vector2(1f, 1f), color);
            buffers.AddVertex(edgeBottom, edgeNormal, tangent, new Vector2(1f, 0f), color);
            buffers.AddQuad(edgeStartIndex, edgeStartIndex + 2, edgeStartIndex + 3, edgeStartIndex + 1);
        }

        private static void AddWarpedBlob(MeshBuffers buffers, Vector3 center, Vector3 radii, int latSegments, int lonSegments, float warpAmplitude, float frequencyA, float frequencyB, float bottomFlattening, Color32 color)
        {
            int startIndex = buffers.Vertices.Count;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);
                float flatten = sinPhi < 0f ? Mathf.Lerp(1f, bottomFlattening, -sinPhi) : 1f;

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * TwoPi;
                    Vector3 dir = new Vector3(Mathf.Cos(theta) * cosPhi, sinPhi, Mathf.Sin(theta) * cosPhi).normalized;
                    float warp = Mathf.Sin(theta * frequencyA) * warpAmplitude
                        + Mathf.Cos((phi + 1.2f) * frequencyB) * warpAmplitude * 0.58f
                        + Mathf.Sin((theta + phi) * (frequencyA * 0.42f + 1.3f)) * warpAmplitude * 0.34f;
                    float radiusScale = 1f + warp;
                    Vector3 vertex = center + Vector3.Scale(new Vector3(dir.x, dir.y * flatten, dir.z), radii * radiusScale);
                    Vector3 normal = (new Vector3(dir.x, dir.y / Mathf.Max(bottomFlattening, 0.2f), dir.z)).normalized;
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

        private static void AddBranchTipCluster(MeshBuffers buffers, Vector3 center, float tipRadius, float warpAmplitude, Color32 color, int lod)
        {
            if (tipRadius <= 0f)
                return;

            int latSegments = lod == 0 ? 5 : (lod == 1 ? 4 : 3);
            int lonSegments = lod == 0 ? 8 : (lod == 1 ? 6 : 4);
            float clusterRadius = tipRadius * (lod == 0 ? 2.5f : (lod == 1 ? 2f : 1.7f));
            AddWarpedBlob(
                buffers,
                center,
                new Vector3(clusterRadius, clusterRadius * 0.82f, clusterRadius),
                latSegments,
                lonSegments,
                warpAmplitude * 0.36f,
                2.2f,
                3.1f,
                0.92f,
                color);
        }

        private static void AddBranchKnuckle(MeshBuffers buffers, Vector3 center, float radius, float warpAmplitude, Color32 color, int lod)
        {
            int latSegments = lod == 0 ? 5 : (lod == 1 ? 4 : 3);
            int lonSegments = lod == 0 ? 8 : (lod == 1 ? 6 : 4);
            AddWarpedBlob(
                buffers,
                center,
                new Vector3(radius, radius * 0.78f, radius),
                latSegments,
                lonSegments,
                warpAmplitude * 0.24f,
                2.8f,
                2.2f,
                0.86f,
                color);
        }

        private static void AddFanCrossLinks(MeshBuffers buffers, Vector3[] branchTips, int branchTipCount, Vector3 scale, CoralSpec spec, int lod)
        {
            int linkSegments = lod == 0 ? 4 : 2;
            int radialSegments = lod == 0 ? 5 : 3;
            float linkRadius = scale.x * (lod == 0 ? 0.022f : 0.013f);

            for (int i = 0; i < branchTipCount - 1; i++)
            {
                Vector3 start = branchTips[i];
                Vector3 end = branchTips[i + 1];
                Vector3 mid = (start + end) * 0.5f;
                Vector3 sag = Vector3.down * (scale.y * 0.08f);
                AddBezierTube(
                    buffers,
                    start,
                    Vector3.Lerp(start, mid, 0.5f) + sag,
                    Vector3.Lerp(mid, end, 0.5f) + sag,
                    end,
                    linkRadius,
                    linkRadius * 0.78f,
                    linkSegments,
                    radialSegments,
                    spec.Color,
                    spec.WarpAmplitude * 0.12f);
            }

            if (lod == 0)
            {
                for (int i = 0; i < branchTipCount - 2; i++)
                {
                    Vector3 start = branchTips[i];
                    Vector3 end = branchTips[i + 2];
                    Vector3 mid = (start + end) * 0.5f + Vector3.down * (scale.y * 0.1f);
                    AddBezierTube(
                        buffers,
                        start,
                        Vector3.Lerp(start, mid, 0.45f),
                        Vector3.Lerp(mid, end, 0.55f),
                        end,
                        scale.x * 0.014f,
                        scale.x * 0.01f,
                        3,
                        4,
                        spec.Color,
                        spec.WarpAmplitude * 0.08f);
                }
            }
        }

        private static Mesh CreateMesh(string rootToken, int lod, MeshBuffers buffers)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(
                buffers.Vertices.Count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            meshData.SetIndexBufferParams(buffers.Indices.Count, IndexFormat.UInt32);

            NativeArray<VertexData> vertexData = meshData.GetVertexData<VertexData>();
            for (int i = 0; i < buffers.Vertices.Count; i++)
                vertexData[i] = new VertexData(buffers.Vertices[i], buffers.Normals[i], buffers.Tangents[i], buffers.Colors[i], buffers.UVs[i]);

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

        private static bool TryResolveSpec(string rootToken, out CoralSpec spec)
        {
            switch (NormalizeRootToken(rootToken))
            {
                case "family_coral_low__bed": spec = new CoralSpec(CoralShape.Low, CoralVariant.Bed, 14, 24, 5, 0, 0.09f, 3.2f, 4.9f, new Color32(172, 168, 186, 255), 3600); return true;
                case "family_coral_low__plate": spec = new CoralSpec(CoralShape.Low, CoralVariant.Plate, 13, 24, 5, 0, 0.09f, 3.4f, 5.2f, new Color32(178, 154, 174, 255), 3200); return true;
                case "family_coral_low__knoll": spec = new CoralSpec(CoralShape.Low, CoralVariant.Knoll, 15, 24, 5, 0, 0.1f, 3.6f, 5.4f, new Color32(186, 162, 178, 255), 3400); return true;
                case "family_coral_low__spread": spec = new CoralSpec(CoralShape.Low, CoralVariant.Bed, 16, 28, 5, 0, 0.115f, 4.2f, 5.9f, new Color32(176, 166, 182, 255), 4200); return true;
                case "family_coral_low__mound": spec = new CoralSpec(CoralShape.Low, CoralVariant.Knoll, 17, 26, 5, 0, 0.108f, 4.1f, 6.0f, new Color32(188, 170, 180, 255), 4000); return true;
                case "family_coral_low__saucer": spec = new CoralSpec(CoralShape.Low, CoralVariant.Plate, 14, 28, 5, 0, 0.1f, 4.6f, 6.4f, new Color32(182, 160, 176, 255), 3900); return true;
                case "family_coral_branching__branch": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 8, 0, 9, 7, 0.06f, 2.4f, 4.1f, new Color32(166, 144, 172, 255), 5200); return true;
                case "family_coral_branching__mass": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Mass, 8, 0, 9, 7, 0.05f, 2.1f, 3.8f, new Color32(176, 152, 168, 255), 5000); return true;
                case "family_coral_branching__fan": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 8, 0, 9, 8, 0.055f, 2.6f, 4.4f, new Color32(164, 150, 176, 255), 6200); return true;
                case "family_coral_branching__crest": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 8, 0, 10, 9, 0.058f, 3.4f, 5.2f, new Color32(172, 154, 180, 255), 6600); return true;
                case "family_coral_branching__bouquet": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 9, 0, 10, 10, 0.052f, 3.1f, 4.9f, new Color32(184, 160, 176, 255), 6400); return true;
                case "family_coral_branching__thicket": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Mass, 9, 0, 10, 9, 0.057f, 3.6f, 5.5f, new Color32(170, 148, 170, 255), 6300); return true;
                case "family_coral_massive__head": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Head, 15, 24, 0, 0, 0.085f, 2.8f, 4.2f, new Color32(180, 158, 154, 255), 4600); return true;
                case "family_coral_massive__porous": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Porous, 15, 26, 0, 0, 0.11f, 3.8f, 5.6f, new Color32(170, 150, 160, 255), 5200); return true;
                case "family_coral_massive__boulder": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Boulder, 15, 26, 0, 0, 0.092f, 3.2f, 4.8f, new Color32(176, 164, 150, 255), 5000); return true;
                case "family_coral_massive__dome": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Head, 16, 28, 0, 0, 0.094f, 3.4f, 5.1f, new Color32(182, 162, 150, 255), 5400); return true;
                case "family_coral_massive__lobed": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Porous, 16, 28, 0, 0, 0.118f, 4.6f, 6.2f, new Color32(174, 152, 156, 255), 5800); return true;
                case "family_coral_massive__buttress": spec = new CoralSpec(CoralShape.Massive, CoralVariant.Boulder, 16, 30, 0, 0, 0.102f, 3.9f, 5.7f, new Color32(184, 168, 150, 255), 5600); return true;
                case "family_coral_plate__ledge": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Ledge, 8, 26, 6, 0, 0.06f, 2.4f, 4.9f, new Color32(186, 168, 152, 255), 3600); return true;
                case "family_coral_plate__shelf": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Shelf, 8, 24, 6, 0, 0.06f, 2.5f, 5.1f, new Color32(180, 162, 150, 255), 4200); return true;
                case "family_coral_plate__stack": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Stack, 8, 26, 6, 0, 0.065f, 2.7f, 5.4f, new Color32(182, 166, 154, 255), 3800); return true;
                case "family_coral_plate__terrace": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Terrace, 8, 30, 6, 0, 0.072f, 3.5f, 6.0f, new Color32(188, 172, 156, 255), 4600); return true;
                case "family_coral_plate__canopy": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Canopy, 8, 28, 6, 0, 0.07f, 3.1f, 5.8f, new Color32(184, 166, 154, 255), 4500); return true;
                case "family_coral_plate__bastion": spec = new CoralSpec(CoralShape.Plate, CoralVariant.Bastion, 8, 30, 6, 0, 0.074f, 3.8f, 6.3f, new Color32(190, 174, 160, 255), 4400); return true;
                case "family_coral_brittle__sprig": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 8, 0, 9, 6, 0.045f, 3.4f, 5.2f, new Color32(108, 150, 158, 255), 4400); return true;
                case "family_coral_brittle__fan": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 8, 0, 9, 7, 0.05f, 3.8f, 5.8f, new Color32(118, 166, 170, 255), 5200); return true;
                case "family_coral_brittle__spire": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 8, 0, 10, 8, 0.042f, 3.1f, 4.8f, new Color32(124, 176, 182, 255), 5200); return true;
                case "family_coral_brittle__lace": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 8, 0, 9, 8, 0.048f, 4.2f, 6.2f, new Color32(134, 188, 192, 255), 5800); return true;
                case "family_coral_brittle__crown": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 8, 0, 10, 9, 0.044f, 3.6f, 5.4f, new Color32(128, 182, 188, 255), 6000); return true;
                case "family_coral_brittle__thicket": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 9, 0, 10, 9, 0.043f, 3.9f, 5.6f, new Color32(136, 196, 198, 255), 6200); return true;
                case "family_coral_brittle__halo": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 8, 0, 10, 8, 0.05f, 4.4f, 6.6f, new Color32(144, 202, 206, 255), 6000); return true;
                case "family_coral_brittle__candelabra": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 9, 0, 11, 11, 0.046f, 4.1f, 5.9f, new Color32(146, 206, 208, 255), 6800); return true;
                case "family_coral_brittle__wreath": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Fan, 9, 0, 10, 10, 0.052f, 4.8f, 6.9f, new Color32(150, 212, 214, 255), 6600); return true;
                case "family_coral_brittle__cathedral": spec = new CoralSpec(CoralShape.Branching, CoralVariant.Branch, 9, 0, 11, 12, 0.045f, 4.4f, 6.3f, new Color32(154, 216, 218, 255), 7200); return true;
                default: spec = default; return false;
            }
        }

        private static string NormalizeRootToken(string rootToken)
        {
            if (string.IsNullOrWhiteSpace(rootToken))
                return string.Empty;

            string[] tokens = rootToken.Split(new[] { "__" }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return rootToken;

            string[] filtered = new string[tokens.Length];
            int filteredCount = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Length > 1)
                {
                    char prefix = char.ToLowerInvariant(token[0]);
                    if ((prefix == 's' || prefix == 'w') && char.IsDigit(token[1]))
                        continue;
                }

                filtered[filteredCount++] = token;
            }

            if (filteredCount == 0)
                return rootToken;

            return string.Join("__", filtered, 0, filteredCount);
        }

        private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0 + 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t * p3;
        }

        private static Vector3 EvaluateBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * (p1 - p0) + 6f * oneMinusT * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        private enum CoralShape { Low, Branching, Massive, Plate }
        private enum CoralVariant { Bed, Plate, Knoll, Branch, Mass, Fan, Head, Porous, Boulder, Ledge, Shelf, Stack, Terrace, Canopy, Bastion }

        private readonly struct CoralSpec
        {
            public CoralSpec(CoralShape shape, CoralVariant variant, int latitudeSegments, int longitudeSegments, int radialSegments, int branchCount, float warpAmplitude, float warpFrequencyA, float warpFrequencyB, Color32 color, int estimatedVertexCount)
            {
                Shape = shape;
                Variant = variant;
                LatitudeSegments = latitudeSegments;
                LongitudeSegments = longitudeSegments;
                RadialSegments = radialSegments;
                BranchCount = branchCount;
                PathSegments = shape == CoralShape.Branching ? 5 : 4;
                WarpAmplitude = warpAmplitude;
                WarpFrequencyA = warpFrequencyA;
                WarpFrequencyB = warpFrequencyB;
                Color = color;
                EstimatedVertexCount = estimatedVertexCount;
            }

            public CoralShape Shape { get; }
            public CoralVariant Variant { get; }
            public int LatitudeSegments { get; }
            public int LongitudeSegments { get; }
            public int RadialSegments { get; }
            public int BranchCount { get; }
            public int PathSegments { get; }
            public float WarpAmplitude { get; }
            public float WarpFrequencyA { get; }
            public float WarpFrequencyB { get; }
            public Color32 Color { get; }
            public int EstimatedVertexCount { get; }
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

        private sealed class MeshBuffers
        {
            public MeshBuffers(int capacity)
            {
                Vertices = new System.Collections.Generic.List<Vector3>(capacity);
                Normals = new System.Collections.Generic.List<Vector3>(capacity);
                Tangents = new System.Collections.Generic.List<Vector4>(capacity);
                Colors = new System.Collections.Generic.List<Color32>(capacity);
                UVs = new System.Collections.Generic.List<Vector2>(capacity);
                Indices = new System.Collections.Generic.List<uint>(capacity * 3);
                Bounds = new Bounds(Vector3.zero, Vector3.zero);
            }

            public System.Collections.Generic.List<Vector3> Vertices { get; }
            public System.Collections.Generic.List<Vector3> Normals { get; }
            public System.Collections.Generic.List<Vector4> Tangents { get; }
            public System.Collections.Generic.List<Color32> Colors { get; }
            public System.Collections.Generic.List<Vector2> UVs { get; }
            public System.Collections.Generic.List<uint> Indices { get; }
            public Bounds Bounds { get; private set; }

            private bool _hasBounds;

            public void AddVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv, Color32 color)
            {
                Vertices.Add(position);
                Normals.Add(normal);
                Tangents.Add(tangent);
                Colors.Add(color);
                UVs.Add(uv);
                if (!_hasBounds)
                {
                    Bounds = new Bounds(position, Vector3.zero);
                    _hasBounds = true;
                }
                else
                {
                    Bounds currentBounds = Bounds;
                    currentBounds.Encapsulate(position);
                    Bounds = currentBounds;
                }
            }

            public void AddTriangle(int a, int b, int c)
            {
                Indices.Add((uint)a);
                Indices.Add((uint)b);
                Indices.Add((uint)c);
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
