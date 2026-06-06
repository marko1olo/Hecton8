using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay.Flora
{
    public static class WorldFloraDirector
    {
        private const string GeneratedMeshNodeName = "GEN_CoralLSystemMesh";
        private const int MaxCoralDepth = 6;
        private const int BranchRadialSegments = 9;
        private const int BranchAxialSegments = 4;
        private const int CapAxialSegments = 3;
        private const float MinimumRadius = 0.015f;
        private const float BranchLengthScale = 5.5f;
        private const float FlowSampleScale = 0.017f;
        private const float BranchCurveScale = 0.075f;

        public static void GenerateBranchingCoralLSystem(Transform rootNode, float baseRadius, float taperFactor, int maxDepth)
        {
            if (rootNode == null)
                return;

            float safeBaseRadius = Mathf.Max(MinimumRadius, baseRadius);
            float safeTaper = Mathf.Clamp(taperFactor, 0.2f, 0.95f);
            int safeDepth = Mathf.Clamp(maxDepth, 0, MaxCoralDepth);
            uint seed = ResolveStableSeed(rootNode);

            ClearLegacyPrimitiveChildren(rootNode);

            GameObject generated = EnsureGeneratedMeshNode(rootNode);
            MeshFilter filter = generated.GetComponent<MeshFilter>();
            if (filter == null)
                filter = generated.AddComponent<MeshFilter>();
            MeshRenderer renderer = generated.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = generated.AddComponent<MeshRenderer>();

            MeshRenderer rootRenderer = rootNode.GetComponent<MeshRenderer>();
            if (rootRenderer != null && renderer.sharedMaterial == null)
                renderer.sharedMaterial = rootRenderer.sharedMaterial;

            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                filter.sharedMesh = mesh;
            }

            mesh.Clear();
            mesh.name = GeneratedMeshNodeName;

            CoralMeshBuilder builder = new CoralMeshBuilder(ResolveInitialMeshCapacity(safeDepth));
            if (safeDepth <= 0)
            {
                AppendHemisphereCap(ref builder, Vector3.zero, Vector3.up, safeBaseRadius, 1f, seed, safeBaseRadius);
            }
            else
            {
                BuildBranchRecursive(
                    ref builder,
                    Vector3.zero,
                    Quaternion.identity,
                    safeBaseRadius,
                    safeTaper,
                    0,
                    safeDepth,
                    seed,
                    safeBaseRadius);
            }

            mesh.indexFormat = builder.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(builder.Vertices);
            mesh.SetNormals(builder.Normals);
            mesh.SetColors(builder.Colors);
            mesh.SetUVs(0, builder.Uvs);
            mesh.SetTriangles(builder.Indices, 0);
            mesh.RecalculateBounds();
        }

        public static void ApplyHydrodynamicDragLeaning(Vector3[] splinePoints, Vector3 worldSpaceBasePos, float stalkHeight)
        {
            if (splinePoints == null || splinePoints.Length == 0 || stalkHeight <= 0f)
                return;

            Vector3 localFlowVector = QueryFluidFlowField(worldSpaceBasePos);
            Vector3 dragBias = localFlowVector.normalized;
            int denominator = Mathf.Max(1, splinePoints.Length - 1);

            for (int i = 0; i < splinePoints.Length; i++)
            {
                float heightRatio = (float)i / denominator;
                Vector3 curveOffset = dragBias * (stalkHeight * heightRatio * heightRatio);
                splinePoints[i] += curveOffset;
            }
        }

        private static void BuildBranchRecursive(
            ref CoralMeshBuilder builder,
            Vector3 start,
            Quaternion parentRotation,
            float rootRadius,
            float taperFactor,
            int depth,
            int maxDepth,
            uint seed,
            float baseRadius)
        {
            float nextRadius = Mathf.Max(MinimumRadius, rootRadius * taperFactor);
            float branchLength = Mathf.Max(nextRadius * BranchLengthScale, rootRadius * 1.75f);
            uint branchSeed = Mix(seed ^ (uint)(depth + 1) * 0x9E3779B9u);
            float yaw = ResolveSigned01(branchSeed) * Mathf.Lerp(14f, 46f, Mathf.Clamp01(depth / 4f));
            float pitch = depth == 0 ? 0f : 16f + ResolveSigned01(Mix(branchSeed ^ 0xC2B2AE35u)) * 12f;
            float roll = ResolveSigned01(Mix(branchSeed ^ 0x27D4EB2Fu)) * 18f;
            Quaternion segmentRotation = parentRotation * Quaternion.Euler(pitch, yaw, roll);
            Vector3 direction = segmentRotation * Vector3.up;
            Vector3 curve = (segmentRotation * Vector3.right) * (branchLength * BranchCurveScale * ResolveSigned01(Mix(branchSeed ^ 0x165667B1u)));
            Vector3 end = start + direction * branchLength + curve;
            float growth01 = maxDepth <= 0 ? 1f : Mathf.Clamp01((depth + 1f) / (maxDepth + 1f));

            AppendTaperedBranch(ref builder, start, end, rootRadius, nextRadius, depth, growth01, branchSeed, baseRadius);

            if (depth >= maxDepth)
            {
                AppendHemisphereCap(ref builder, end, direction, nextRadius, growth01, branchSeed, baseRadius);
                return;
            }

            int branchCount = ResolveBranchCount(depth, branchSeed);
            for (int i = 0; i < branchCount; i++)
            {
                uint childSeed = Mix(branchSeed + (uint)(i + 1) * 0x85EBCA6Bu);
                float splitYaw = branchCount == 1
                    ? ResolveSigned01(childSeed) * 18f
                    : Mathf.Lerp(-42f, 42f, branchCount == 1 ? 0.5f : (float)i / (branchCount - 1));
                float splitPitch = 20f + ResolveUnsigned01(Mix(childSeed ^ 0xA24BAED5u)) * 22f;
                Quaternion childRotation = segmentRotation * Quaternion.Euler(splitPitch, splitYaw, ResolveSigned01(childSeed) * 10f);
                BuildBranchRecursive(ref builder, end, childRotation, nextRadius, taperFactor, depth + 1, maxDepth, childSeed, baseRadius);
            }
        }

        private static int ResolveBranchCount(int depth, uint seed)
        {
            if (depth <= 1)
                return 2;
            if (depth >= MaxCoralDepth - 1)
                return 1;
            return 1 + (int)(seed & 1u);
        }

        private static int ResolveInitialMeshCapacity(int maxDepth)
        {
            int segmentEstimate = 1;
            int branchFrontier = 1;
            for (int depth = 0; depth <= maxDepth; depth++)
            {
                branchFrontier *= depth <= 1 ? 2 : 1;
                segmentEstimate += branchFrontier;
            }

            int branchVertexBudget = segmentEstimate * (BranchAxialSegments + 1) * BranchRadialSegments;
            int capVertexBudget = segmentEstimate * (CapAxialSegments + 1) * BranchRadialSegments;
            return Mathf.Clamp(branchVertexBudget + capVertexBudget, 256, 8192);
        }

        private static void AppendTaperedBranch(
            ref CoralMeshBuilder builder,
            Vector3 start,
            Vector3 end,
            float rootRadius,
            float tipRadius,
            int depth,
            float growth01,
            uint seed,
            float baseRadius)
        {
            Vector3 axis = end - start;
            float length = axis.magnitude;
            if (length <= 0.0001f)
                return;

            Vector3 direction = axis / length;
            ResolveBasis(direction, out Vector3 right, out Vector3 forward);
            int firstRing = builder.Vertices.Count;

            for (int ring = 0; ring <= BranchAxialSegments; ring++)
            {
                float t = (float)ring / BranchAxialSegments;
                float axialWave = Mathf.Sin(t * Mathf.PI);
                Vector3 center = Vector3.Lerp(start, end, t) + forward * (axialWave * length * 0.035f * ResolveSigned01(Mix(seed + (uint)ring * 0x9E3779B9u)));
                float taper = Mathf.Lerp(rootRadius * ResolveKnuckleScale(t, depth), tipRadius, t);
                float sway = Mathf.Pow(Mathf.Clamp01(growth01 * t), 1.35f);
                float phase = Mathf.Lerp(0.08f, 0.85f, ResolveUnsigned01(Mix(seed ^ 0x632BE59Bu)));
                float ao = Mathf.Lerp(0.36f, 0.82f, t) * Mathf.Lerp(0.72f, 1f, axialWave);
                float thickness = Mathf.Clamp01(taper / Mathf.Max(MinimumRadius, baseRadius));

                for (int side = 0; side < BranchRadialSegments; side++)
                {
                    float angle = side * Mathf.PI * 2f / BranchRadialSegments;
                    float asymmetry = 1f + ResolveSigned01(Mix(seed + (uint)(ring * 37 + side) * 0x27D4EB2Du)) * 0.12f;
                    Vector3 radial = Mathf.Cos(angle) * right + Mathf.Sin(angle) * forward;
                    Vector3 vertex = center + radial * taper * asymmetry;
                    builder.Vertices.Add(vertex);
                    builder.Normals.Add(radial.normalized);
                    builder.Uvs.Add(new Vector2((float)side / BranchRadialSegments, t + depth));
                    builder.Colors.Add(new Color(sway, phase, ao, thickness));
                }
            }

            for (int ring = 0; ring < BranchAxialSegments; ring++)
            {
                int current = firstRing + ring * BranchRadialSegments;
                int next = current + BranchRadialSegments;
                for (int side = 0; side < BranchRadialSegments; side++)
                {
                    int a = current + side;
                    int b = current + (side + 1) % BranchRadialSegments;
                    int c = next + side;
                    int d = next + (side + 1) % BranchRadialSegments;
                    builder.Indices.Add(a);
                    builder.Indices.Add(c);
                    builder.Indices.Add(b);
                    builder.Indices.Add(b);
                    builder.Indices.Add(c);
                    builder.Indices.Add(d);
                }
            }
        }

        private static void AppendHemisphereCap(
            ref CoralMeshBuilder builder,
            Vector3 center,
            Vector3 direction,
            float radius,
            float growth01,
            uint seed,
            float baseRadius)
        {
            ResolveBasis(direction.normalized, out Vector3 right, out Vector3 forward);
            int first = builder.Vertices.Count;

            for (int ring = 0; ring <= CapAxialSegments; ring++)
            {
                float t = (float)ring / CapAxialSegments;
                float polar = t * Mathf.PI * 0.5f;
                float ringRadius = Mathf.Cos(polar) * radius;
                Vector3 ringCenter = center + direction.normalized * (Mathf.Sin(polar) * radius * 0.85f);
                float ao = Mathf.Lerp(0.52f, 0.9f, t);
                for (int side = 0; side < BranchRadialSegments; side++)
                {
                    float angle = side * Mathf.PI * 2f / BranchRadialSegments;
                    Vector3 radial = Mathf.Cos(angle) * right + Mathf.Sin(angle) * forward;
                    float pitted = 1f + ResolveSigned01(Mix(seed + (uint)(ring * 41 + side) * 0x165667B1u)) * 0.08f;
                    builder.Vertices.Add(ringCenter + radial * ringRadius * pitted);
                    builder.Normals.Add((direction * Mathf.Sin(polar) + radial * Mathf.Cos(polar)).normalized);
                    builder.Uvs.Add(new Vector2((float)side / BranchRadialSegments, t));
                    builder.Colors.Add(new Color(Mathf.Clamp01(growth01), 0.9f, ao, Mathf.Clamp01(radius / Mathf.Max(MinimumRadius, baseRadius))));
                }
            }

            for (int ring = 0; ring < CapAxialSegments; ring++)
            {
                int current = first + ring * BranchRadialSegments;
                int next = current + BranchRadialSegments;
                for (int side = 0; side < BranchRadialSegments; side++)
                {
                    int a = current + side;
                    int b = current + (side + 1) % BranchRadialSegments;
                    int c = next + side;
                    int d = next + (side + 1) % BranchRadialSegments;
                    builder.Indices.Add(a);
                    builder.Indices.Add(c);
                    builder.Indices.Add(b);
                    builder.Indices.Add(b);
                    builder.Indices.Add(c);
                    builder.Indices.Add(d);
                }
            }
        }

        private static GameObject EnsureGeneratedMeshNode(Transform rootNode)
        {
            Transform existing = rootNode.Find(GeneratedMeshNodeName);
            if (existing != null)
                return existing.gameObject;

            GameObject generated = new GameObject(GeneratedMeshNodeName);
            Transform generatedTransform = generated.transform;
            generatedTransform.SetParent(rootNode, false);
            generatedTransform.localPosition = Vector3.zero;
            generatedTransform.localRotation = Quaternion.identity;
            generatedTransform.localScale = Vector3.one;
            return generated;
        }

        private static void ClearLegacyPrimitiveChildren(Transform rootNode)
        {
            for (int i = rootNode.childCount - 1; i >= 0; i--)
            {
                Transform child = rootNode.GetChild(i);
                string childName = child.name;
                if (childName.StartsWith("Coral_Branch_", System.StringComparison.Ordinal) ||
                    childName.StartsWith("Coral_Tip_", System.StringComparison.Ordinal) ||
                    childName == "Coral_Cap")
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private static void DestroyGeneratedObject(GameObject generated)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(generated);
                return;
            }
#endif
            Object.Destroy(generated);
        }

        private static float ResolveKnuckleScale(float axialT, int depth)
        {
            float baseKnuckle = Mathf.Exp(-axialT * 5.5f) * Mathf.Lerp(0.34f, 0.12f, Mathf.Clamp01(depth / 5f));
            return 1f + baseKnuckle;
        }

        private static void ResolveBasis(Vector3 direction, out Vector3 right, out Vector3 forward)
        {
            Vector3 helper = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
            right = Vector3.Cross(helper, direction).normalized;
            forward = Vector3.Cross(direction, right).normalized;
        }

        private static Vector3 QueryFluidFlowField(Vector3 position)
        {
            float x = Mathf.Sin((position.x + position.z * 0.37f) * FlowSampleScale);
            float z = Mathf.Cos((position.z - position.x * 0.21f) * FlowSampleScale);
            Vector3 flow = new Vector3(x, 0f, z);
            return flow.sqrMagnitude > 0.0001f ? flow.normalized : Vector3.forward;
        }

        private static uint ResolveStableSeed(Transform rootNode)
        {
            Vector3 position = rootNode.position;
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(position.x * 100f));
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(position.y * 100f));
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(position.z * 100f));
                string name = rootNode.name;
                for (int i = 0; i < name.Length; i++)
                    hash = Mix(hash ^ name[i]);
                return hash;
            }
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static float ResolveSigned01(uint value)
        {
            return ((value & 0xFFFFu) / 32767.5f) - 1f;
        }

        private static float ResolveUnsigned01(uint value)
        {
            return (value & 0xFFFFu) / 65535f;
        }

        private struct CoralMeshBuilder
        {
            public readonly List<Vector3> Vertices;
            public readonly List<Vector3> Normals;
            public readonly List<Color> Colors;
            public readonly List<Vector2> Uvs;
            public readonly List<int> Indices;

            public CoralMeshBuilder(int capacity = 512)
            {
                Vertices = new List<Vector3>(capacity);
                Normals = new List<Vector3>(capacity);
                Colors = new List<Color>(capacity);
                Uvs = new List<Vector2>(capacity);
                Indices = new List<int>(capacity * 6);
            }
        }
    }
}
