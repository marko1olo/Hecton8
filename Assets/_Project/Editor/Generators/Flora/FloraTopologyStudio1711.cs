#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Editor.ColliderOptimization1716;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorTools.Generators.Flora
{
    internal enum FloraBakeTier1711 : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraTopologyProfile1711
    {
        public FloraBakeTier1711 Tier;
        public float GlobalQualityWeight;
        public int LSystemIterations;
        public int SpaceColonizationAttractors;
        public int HemisphereAoSamples;
        public int UvAtlasResolution;
        public int Lod0TriangleBudget;
        public int LayoutPadding0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraProbeVertex1711
    {
        public float3 Position;
        public float3 Normal;
        public float Wear;
        public uint PackedColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraUvRect1711
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    internal static class FloraTopologyStudio1711
    {
        private const string MeshOutputRoot = "Assets/_Project/Art/Generated/Flora/Topology1711";
        private const string PrefabOutputRoot = "Assets/_Project/Prefabs/Nature/Flora/Topology1711";
        private const string KelpMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat";
        private const string CoralMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat";
        private const string MassiveCoralMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat";
        private const float DegenerateTriangleAreaThreshold = 0.00001f;
        private const float NormalLengthSqTolerance = 0.06f;
        private const float BoundsExtentEpsilonSq = 0.000001f;
        private const float AnchorPinnedMaskEpsilon = 0.02f;

        // Span, not an absolute tip value. The L-system truncates against MaxNodeCount whenever a
        // preset's child fan-out outruns its capacity, and for the coral presets that is the normal
        // case, so a stunted plant's geodesic maximum sits well below 1. A span gate still catches
        // the failure that matters -- a constant V, which zeroes every shader sway term -- without
        // turning a capacity limit into a hard bake failure.
        private const float MinimumMaskGradientSpan = 0.05f;

        [MenuItem("Hecton8/Authoring/Flora Topology 1711/Open Studio", priority = 191)]
        public static void OpenStudio()
        {
            FloraTopologyStudioWindow1711.Open();
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1711/Generate Static Seed Pack", priority = 192)]
        public static void GenerateStaticSeedPack()
        {
            TryGenerateStaticSeedPack();
        }

        /// <summary>
        /// Runs the full static seed pack and reports whether every entry survived its contract
        /// gates. This is the single owner of the preset/seed/tier roster: the menu command and the
        /// headless batchmode gate both route through it so a batch run and a human click can never
        /// bake a different set of assets.
        /// </summary>
        internal static bool TryGenerateStaticSeedPack()
        {
            if (!ValidateUnmanagedLayouts())
                return false;

            bool ok = true;
            ok &= GenerateAndSave(FloraTopologyPreset.KelpForestFrond, 17110042u, ResolveProfile(FloraBakeTier1711.High), false);
            ok &= GenerateAndSave(FloraTopologyPreset.AbyssalBrainCoral, 17111042u, ResolveProfile(FloraBakeTier1711.Ultra), false);
            ok &= GenerateAndSave(FloraTopologyPreset.ThermalTubeWorm, 17112042u, ResolveProfile(FloraBakeTier1711.Middle), false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ok)
            {
                Debug.LogError("[FloraTopology1711] Seed pack generation failed. Missing authored output must be fixed before player build.");
                return false;
            }

            Debug.Log("[FloraTopology1711] Static seed pack generated under Topology1711.");
            return true;
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1711/Run Dry Verification", priority = 193)]
        public static void RunDryVerification()
        {
            if (!ValidateUnmanagedLayouts())
                return;

            bool ok = true;
            ok &= RunDeterministicProbe(FloraBakeTier1711.Low);
            ok &= RunDeterministicProbe(FloraBakeTier1711.Middle);
            ok &= RunDeterministicProbe(FloraBakeTier1711.High);
            ok &= RunDeterministicProbe(FloraBakeTier1711.Ultra);
            ok &= TryPackDryRunAtlas(ResolveProfile(FloraBakeTier1711.Ultra));

            if (ok)
                Debug.Log("[FloraTopology1711] Dry verification passed. Runtime geometry factories remain editor-only.");
        }

        internal static FloraTopologyProfile1711 ResolveProfile(FloraBakeTier1711 tier)
        {
            float q = tier switch
            {
                FloraBakeTier1711.Low => 0.12f,
                FloraBakeTier1711.Middle => 0.42f,
                FloraBakeTier1711.High => 0.72f,
                FloraBakeTier1711.Ultra => 1f,
                _ => 0.42f
            };

            return new FloraTopologyProfile1711
            {
                Tier = tier,
                GlobalQualityWeight = q,
                LSystemIterations = math.clamp((int)math.round(math.lerp(2f, 7f, q)), 2, 7),
                SpaceColonizationAttractors = math.clamp((int)math.round(math.lerp(16f, 512f, q)), 16, 512),
                HemisphereAoSamples = math.clamp((int)math.round(math.lerp(4f, 32f, q)), 4, 32),
                UvAtlasResolution = math.clamp((int)math.round(math.lerp(256f, 2048f, q)), 256, 2048),
                Lod0TriangleBudget = math.clamp((int)math.round(math.lerp(2500f, 80000f, q)), 2500, 80000),
                LayoutPadding0 = 0
            };
        }

        internal static bool GenerateAndSave(FloraTopologyPreset preset, uint seed, FloraTopologyProfile1711 profile, bool saveAssets = true)
        {
            if (!ValidateUnmanagedLayouts())
                return false;

            EnsureAssetFolder(MeshOutputRoot);
            EnsureAssetFolder(PrefabOutputRoot);

            FloraGenomeDTO genome = FloraTopologyGenerator1604.CreateGenome(
                preset,
                seed,
                profile.GlobalQualityWeight,
                ResolveBudgetForPreset(preset, profile));

            string assetName = "GEN_FloraTopology1711_" + preset + "_" + profile.Tier + "_" + seed.ToString("X8");
            if (!FloraTopologyGenerator1604.TryGenerateMeshes(genome, assetName, out Mesh[] meshes, out FloraTopologyMetrics metrics))
                return false;

            try
            {
                if (!ValidateMeshArray(assetName, meshes) ||
                    !ValidateMetrics(assetName, metrics, profile) ||
                    !BakeAndValidateMeshContracts(meshes))
                {
                    return false;
                }

                Mesh lod0 = SaveMeshAsset(meshes[0], MeshOutputRoot + "/" + assetName + "_LOD0.asset");
                Mesh lod1 = SaveMeshAsset(meshes[1], MeshOutputRoot + "/" + assetName + "_LOD1.asset");
                Mesh lod2 = SaveMeshAsset(meshes[2], MeshOutputRoot + "/" + assetName + "_LOD2.asset");
                if (lod0 == null || lod1 == null || lod2 == null)
                    return false;

                Material material = ResolveMaterial(preset);
                if (material == null)
                    return false;

                if (!SavePrefab(assetName, lod0, lod1, lod2, material))
                    return false;

                if (saveAssets)
                    AssetDatabase.SaveAssets();

                return true;
            }
            finally
            {
                int meshCount = meshes != null ? meshes.Length : 0;
                for (int i = 0; i < meshCount; i++)
                {
                    if (meshes[i] != null && !AssetDatabase.Contains(meshes[i]))
                        UnityEngine.Object.DestroyImmediate(meshes[i]);
                }
            }
        }

        private static int ResolveBudgetForPreset(FloraTopologyPreset preset, FloraTopologyProfile1711 profile)
        {
            int defaultBudget = FloraTopologyGenerator1604.ResolveDefaultTriangleBudget(preset);
            return math.clamp(math.max(defaultBudget, profile.Lod0TriangleBudget), 192, 80000);
        }

        private static bool ValidateMetrics(string label, FloraTopologyMetrics metrics, FloraTopologyProfile1711 profile)
        {
            if (metrics.Lod0Vertices <= 0 || metrics.Lod1Vertices <= 0 || metrics.Lod2Vertices <= 0)
            {
                Debug.LogError("[FloraTopology1711] " + label + " emitted empty LOD mesh.");
                return false;
            }

            if (metrics.Lod0Triangles > profile.Lod0TriangleBudget)
            {
                Debug.LogError("[FloraTopology1711] " + label + " exceeded LOD0 budget. tris=" + metrics.Lod0Triangles + " budget=" + profile.Lod0TriangleBudget);
                return false;
            }

            if (metrics.Lod1Triangles > metrics.Lod0Triangles || metrics.Lod2Triangles > metrics.Lod1Triangles)
            {
                Debug.LogError("[FloraTopology1711] " + label + " LOD chain is not monotonic.");
                return false;
            }

            return true;
        }

        private static bool ValidateMeshArray(string label, Mesh[] meshes)
        {
            if (meshes != null && meshes.Length >= 3 && meshes[0] != null && meshes[1] != null && meshes[2] != null)
                return true;

            Debug.LogError("[FloraTopology1711] " + label + " did not emit the required three static LOD meshes.");
            return false;
        }

        private static bool BakeAndValidateMeshContracts(Mesh[] meshes)
        {
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                Mesh mesh = meshes[meshIndex];
                if (mesh == null)
                    return false;

                NativeArray<Color32> remappedColors = default;
                int colorStream = -1;
                bool readyToApplyColors = false;
                try
                {
                    using Mesh.MeshDataArray readOnlyMeshData = Mesh.AcquireReadOnlyMeshData(mesh);
                    Mesh.MeshData meshData = readOnlyMeshData[0];
                    if (!TryResolveMeshStreams(meshData, mesh.name, out int positionStream, out int normalStream, out colorStream, out int maskStream))
                        return false;

                    int vertexCount = meshData.vertexCount;
                    NativeArray<float3> positions = meshData.GetVertexData<float3>(positionStream);
                    NativeArray<float3> normals = meshData.GetVertexData<float3>(normalStream);
                    NativeArray<Color32> sourceColors = meshData.GetVertexData<Color32>(colorStream);
                    NativeArray<FloraInterleavedStream2Vertex> maskVertices = meshData.GetVertexData<FloraInterleavedStream2Vertex>(maskStream);
                    if (positions.Length < vertexCount || normals.Length < vertexCount || sourceColors.Length < vertexCount || maskVertices.Length < vertexCount)
                    {
                        Debug.LogError("[FloraTopology1711] MeshData stream length mismatch on " + mesh.name);
                        return false;
                    }

                    if (!ValidateGeodesicMaskGradient(mesh.name, maskVertices, vertexCount, out float minMaskV, out float maxMaskV))
                        return false;

                    remappedColors = new NativeArray<Color32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    if (!RemapAndValidateSemanticColors(mesh.name, positions, normals, sourceColors, maskVertices, remappedColors, vertexCount, minMaskV, maxMaskV))
                        return false;

                    if (!ValidateTriangleTopology(mesh.name, meshData, positions))
                        return false;

                    readyToApplyColors = true;
                }
                finally
                {
                    if (remappedColors.IsCreated && !readyToApplyColors)
                        remappedColors.Dispose();
                }

                if (!remappedColors.IsCreated)
                    return false;

                try
                {
                    mesh.SetVertexBufferData(
                        remappedColors,
                        0,
                        0,
                        remappedColors.Length,
                        colorStream,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                    mesh.RecalculateBounds();
                    if (!ValidateFiniteBounds(mesh))
                        return false;
                }
                finally
                {
                    remappedColors.Dispose();
                }
            }

            return true;
        }

        private static bool TryResolveMeshStreams(
            Mesh.MeshData meshData,
            string label,
            out int positionStream,
            out int normalStream,
            out int colorStream,
            out int maskStream)
        {
            bool ok = true;
            ok &= TryResolveExactMeshAttribute(meshData, label, VertexAttribute.Position, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<float3>(), out positionStream);
            ok &= TryResolveExactMeshAttribute(meshData, label, VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<float3>(), out normalStream);
            ok &= TryResolveExactMeshAttribute(meshData, label, VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, UnsafeUtility.SizeOf<Color32>(), out colorStream);
            ok &= TryResolveMaskStream(meshData, label, out maskStream);
            return ok;
        }

        /// <summary>
        /// Resolves the interleaved stream that carries Tangent, TexCoord0 and TexCoord1. TexCoord1
        /// is the shader-side "UVMask" set: Hecton_KelpMaster.shader binds it as
        /// <c>Attributes.uvMask : TEXCOORD1</c> and multiplies every sway, prop-wash, submarine-wash
        /// and player-interaction displacement term by a height mask derived from its V component.
        /// A mesh without TexCoord1 does not fail to render; Unity feeds the missing stream as zero
        /// and every one of those terms silently becomes exactly zero, which is the "Root vertices
        /// sway as much as tips" inversion of the 3DMODEL_FLORA_CORAL.md section 8 rejection gate.
        /// Fail closed here instead.
        /// </summary>
        private static bool TryResolveMaskStream(Mesh.MeshData meshData, string label, out int maskStream)
        {
            maskStream = -1;
            if (!meshData.HasVertexAttribute(VertexAttribute.TexCoord1) ||
                meshData.GetVertexAttributeFormat(VertexAttribute.TexCoord1) != VertexAttributeFormat.Float32 ||
                meshData.GetVertexAttributeDimension(VertexAttribute.TexCoord1) != 2)
            {
                Debug.LogError("[FloraTopology1711] Missing or incompatible TexCoord1 UVMask stream on " + label
                    + ". Every shader sway term reads zero without it.");
                return false;
            }

            int stream = meshData.GetVertexAttributeStream(VertexAttribute.TexCoord1);
            if (stream < 0)
            {
                Debug.LogError("[FloraTopology1711] TexCoord1 UVMask stream unresolved on " + label);
                return false;
            }

            int expectedStride = UnsafeUtility.SizeOf<FloraInterleavedStream2Vertex>();
            int stride = meshData.GetVertexBufferStride(stream);
            int tangentOffset = meshData.GetVertexAttributeOffset(VertexAttribute.Tangent);
            int uv0Offset = meshData.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
            int maskOffset = meshData.GetVertexAttributeOffset(VertexAttribute.TexCoord1);
            if (stride != expectedStride ||
                meshData.GetVertexAttributeStream(VertexAttribute.Tangent) != stream ||
                meshData.GetVertexAttributeStream(VertexAttribute.TexCoord0) != stream ||
                tangentOffset != 0 ||
                uv0Offset != 16 ||
                maskOffset != 24)
            {
                Debug.LogError("[FloraTopology1711] UVMask stream layout drift on " + label
                    + ". stride=" + stride + " expected=" + expectedStride
                    + " tangentOffset=" + tangentOffset + " uv0Offset=" + uv0Offset + " maskOffset=" + maskOffset);
                return false;
            }

            maskStream = stream;
            return true;
        }

        /// <summary>
        /// Fails loudly when the geodesic root-to-tip mask has collapsed. A constant V is the
        /// dominant silent-degeneracy mode for this pipeline: nothing throws, the plant simply stops
        /// moving and the holdfast stops being pinned.
        /// </summary>
        private static bool ValidateGeodesicMaskGradient(
            string label,
            NativeArray<FloraInterleavedStream2Vertex> maskVertices,
            int vertexCount,
            out float minV,
            out float maxV)
        {
            minV = float.MaxValue;
            maxV = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                float v = maskVertices[i].UVMask.y;
                if (!math.isfinite(v) || v < 0f || v > 1f)
                {
                    Debug.LogError("[FloraTopology1711] Non-finite or out-of-range UVMask V on " + label + " vertex=" + i + " v=" + v);
                    return false;
                }

                minV = math.min(minV, v);
                maxV = math.max(maxV, v);
            }

            if (minV <= AnchorPinnedMaskEpsilon && (maxV - minV) >= MinimumMaskGradientSpan)
                return true;

            Debug.LogError("[FloraTopology1711] UVMask V gradient collapsed on " + label
                + ". minV=" + minV.ToString("F4") + " maxV=" + maxV.ToString("F4")
                + " span=" + (maxV - minV).ToString("F4")
                + ". The anchor must be pinned at 0 and V must carry a real root-to-tip gradient,"
                + " otherwise every shader sway, prop-wash and player-interaction term reads zero.");
            return false;
        }

        private static bool TryResolveExactMeshAttribute(
            Mesh.MeshData meshData,
            string label,
            VertexAttribute attribute,
            VertexAttributeFormat expectedFormat,
            int expectedDimension,
            int expectedStride,
            out int stream)
        {
            stream = -1;
            if (!meshData.HasVertexAttribute(attribute) ||
                meshData.GetVertexAttributeFormat(attribute) != expectedFormat ||
                meshData.GetVertexAttributeDimension(attribute) != expectedDimension)
            {
                Debug.LogError("[FloraTopology1711] Missing or incompatible " + attribute + " stream on " + label);
                return false;
            }

            stream = meshData.GetVertexAttributeStream(attribute);
            int offset = meshData.GetVertexAttributeOffset(attribute);
            int stride = stream >= 0 ? meshData.GetVertexBufferStride(stream) : 0;
            if (stream < 0 || offset != 0 || stride != expectedStride)
            {
                Debug.LogError("[FloraTopology1711] Non-dedicated " + attribute + " stream on " + label + ". offset=" + offset + " stride=" + stride);
                return false;
            }

            return true;
        }

        private static bool RemapAndValidateSemanticColors(
            string label,
            NativeArray<float3> positions,
            NativeArray<float3> normals,
            NativeArray<Color32> sourceColors,
            NativeArray<FloraInterleavedStream2Vertex> maskVertices,
            NativeArray<Color32> remappedColors,
            int vertexCount,
            float minMaskV,
            float maxMaskV)
        {
            // Normalise the geodesic mask against the extent this organism actually reached. The
            // absolute 0.62 cut below means "the distal 38 percent of the body", and the L-system
            // truncates against MaxNodeCount often enough on the coral presets that an absolute cut
            // against the estimated maximum length would place every vertex below it and hand the
            // whole family a zero bioluminescence mask, which the contract check then rejects.
            // A stunted coral still has terminal polyps.
            float maskSpan = math.max(maxMaskV - minMaskV, 0.0001f);
            float inverseMaskSpan = math.rcp(maskSpan);

            bool hasSway = false;
            bool hasBio = false;
            bool hasAo = false;
            bool hasWear = false;
            for (int i = 0; i < vertexCount; i++)
            {
                float3 position = positions[i];
                float3 normal = normals[i];
                float normalLengthSq = math.lengthsq(normal);
                if (!math.all(math.isfinite(position)) ||
                    !math.isfinite(normalLengthSq) ||
                    math.abs(normalLengthSq - 1f) > NormalLengthSqTolerance)
                {
                    Debug.LogError("[FloraTopology1711] Invalid vertex position/normal on " + label + " vertex=" + i);
                    return false;
                }

                Color32 source = sourceColors[i];
                // Terminal bloom, occlusion depth and wear are all functions of how far a vertex sits
                // from the anchor, which is the geodesic UVMask V, NOT the source red channel. Red is
                // the family-scaled sway AMPLITUDE: 3DMODEL_FLORA_CORAL.md section 2 caps rigid
                // mineralized coral at 32/255, so deriving these from red made every mineralized
                // family read as if its whole body were rooted and silently zeroed its emission.
                float geodesic01 = math.saturate((maskVertices[i].UVMask.y - minMaskV) * inverseMaskSpan);
                float phase01 = source.g * (1f / 255f);
                float normalExposure01 = math.saturate(math.abs(normal.y));
                float terminalBloom01 = math.saturate((geodesic01 - 0.62f) * 2.65f);
                byte sway = source.r;
                byte bioluminescence = (byte)math.clamp((int)math.round(source.b * terminalBloom01), 0, 255);
                byte ambientOcclusion = (byte)math.clamp((int)math.round(math.saturate(0.18f + geodesic01 * 0.54f + normalExposure01 * 0.28f) * 255f), 1, 255);
                byte wear = (byte)math.clamp((int)math.round(math.saturate(0.16f + geodesic01 * 0.58f + phase01 * 0.26f) * 255f), 1, 255);
                remappedColors[i] = new Color32(sway, bioluminescence, ambientOcclusion, wear);
                hasSway |= sway > 0;
                hasBio |= bioluminescence > 0;
                hasAo |= ambientOcclusion > 0;
                hasWear |= wear > 0;
            }

            if (hasSway && hasBio && hasAo && hasWear)
                return true;

            Debug.LogError("[FloraTopology1711] Vertex color contract failed on " + label + ". Expected R=sway, G=bioluminescence, B=AO, A=wear.");
            return false;
        }

        private static bool ValidateTriangleTopology(
            string label,
            Mesh.MeshData meshData,
            NativeArray<float3> positions)
        {
            if (meshData.indexFormat == IndexFormat.UInt16)
                return ValidateTriangleTopology16(label, meshData, positions, meshData.GetIndexData<ushort>());

            return ValidateTriangleTopology32(label, meshData, positions, meshData.GetIndexData<uint>());
        }

        private static bool ValidateTriangleTopology16(
            string label,
            Mesh.MeshData meshData,
            NativeArray<float3> positions,
            NativeArray<ushort> indices)
        {
            int vertexCount = positions.Length;
            for (int subMeshIndex = 0; subMeshIndex < meshData.subMeshCount; subMeshIndex++)
            {
                SubMeshDescriptor descriptor = meshData.GetSubMesh(subMeshIndex);
                if (!ValidateSubMeshDescriptor(label, descriptor, indices.Length))
                    return false;

                int end = descriptor.indexStart + descriptor.indexCount;
                for (int index = descriptor.indexStart; index < end; index += 3)
                {
                    int a = descriptor.baseVertex + indices[index];
                    int b = descriptor.baseVertex + indices[index + 1];
                    int c = descriptor.baseVertex + indices[index + 2];
                    if (!ValidateTriangleArea(label, positions, vertexCount, a, b, c))
                        return false;
                }
            }

            return true;
        }

        private static bool ValidateTriangleTopology32(
            string label,
            Mesh.MeshData meshData,
            NativeArray<float3> positions,
            NativeArray<uint> indices)
        {
            int vertexCount = positions.Length;
            for (int subMeshIndex = 0; subMeshIndex < meshData.subMeshCount; subMeshIndex++)
            {
                SubMeshDescriptor descriptor = meshData.GetSubMesh(subMeshIndex);
                if (!ValidateSubMeshDescriptor(label, descriptor, indices.Length))
                    return false;

                int end = descriptor.indexStart + descriptor.indexCount;
                for (int index = descriptor.indexStart; index < end; index += 3)
                {
                    long a = descriptor.baseVertex + (long)indices[index];
                    long b = descriptor.baseVertex + (long)indices[index + 1];
                    long c = descriptor.baseVertex + (long)indices[index + 2];
                    if ((ulong)a >= (ulong)vertexCount ||
                        (ulong)b >= (ulong)vertexCount ||
                        (ulong)c >= (ulong)vertexCount)
                    {
                        Debug.LogError("[FloraTopology1711] Index out of range on " + label + " submesh=" + subMeshIndex);
                        return false;
                    }

                    if (!ValidateTriangleArea(label, positions, vertexCount, (int)a, (int)b, (int)c))
                        return false;
                }
            }

            return true;
        }

        private static bool ValidateSubMeshDescriptor(string label, SubMeshDescriptor descriptor, int indexBufferLength)
        {
            if (descriptor.topology != MeshTopology.Triangles ||
                descriptor.indexStart < 0 ||
                descriptor.indexCount <= 0 ||
                descriptor.indexCount % 3 != 0 ||
                (long)descriptor.indexStart + descriptor.indexCount > indexBufferLength)
            {
                Debug.LogError("[FloraTopology1711] Invalid submesh topology/index range on " + label);
                return false;
            }

            return true;
        }

        private static bool ValidateTriangleArea(
            string label,
            NativeArray<float3> positions,
            int vertexCount,
            int a,
            int b,
            int c)
        {
            if ((uint)a >= (uint)vertexCount ||
                (uint)b >= (uint)vertexCount ||
                (uint)c >= (uint)vertexCount)
            {
                Debug.LogError("[FloraTopology1711] Index out of range on " + label);
                return false;
            }

            float3 p0 = positions[a];
            float3 p1 = positions[b];
            float3 p2 = positions[c];
            float area = math.length(math.cross(p1 - p0, p2 - p0)) * 0.5f;
            if (math.isfinite(area) && area >= DegenerateTriangleAreaThreshold)
                return true;

            Debug.LogError("[FloraTopology1711] Degenerate triangle detected on " + label);
            return false;
        }

        private static bool ValidateFiniteBounds(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            if (IsFinite(center) && IsFinite(extents) && extents.sqrMagnitude > BoundsExtentEpsilonSq)
                return true;

            Debug.LogError("[FloraTopology1711] Invalid recalculated bounds on " + mesh.name);
            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string path)
        {
            if (mesh == null)
                return null;

            mesh.name = Path.GetFileNameWithoutExtension(path);
            mesh.UploadMeshData(true);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = mesh.name;
                EditorUtility.SetDirty(existing);
                Mesh savedExisting = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (savedExisting == null)
                    Debug.LogError("[FloraTopology1711] Failed to update mesh asset at " + path);

                return savedExisting;
            }

            AssetDatabase.CreateAsset(mesh, path);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
                Debug.LogError("[FloraTopology1711] Failed to create mesh asset at " + path);

            return saved;
        }

        private static bool SavePrefab(string assetName, Mesh lod0, Mesh lod1, Mesh lod2, Material material)
        {
            GameObject root = new GameObject(assetName);
            try
            {
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                Renderer r0 = AddRenderer(root.transform, "__LOD0", lod0, material);
                Renderer r1 = AddRenderer(root.transform, "__LOD1", lod1, material);
                Renderer r2 = AddRenderer(root.transform, "__LOD2", lod2, material);
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.62f, new[] { r0 }),
                    new LOD(0.22f, new[] { r1 }),
                    new LOD(0.06f, new[] { r2 })
                });
                lodGroup.RecalculateBounds();
                string path = PrefabOutputRoot + "/" + assetName + ".prefab";
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                {
                    Debug.LogError("[FloraTopology1711] Collider topology rejected before prefab save. path=" + path + " reason=" + colliderFailure);
                    return false;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved != null)
                {
                    if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(path, out colliderFailure))
                    {
                        Debug.LogError("[FloraTopology1711] Collider topology rejected after prefab save. path=" + path + " reason=" + colliderFailure);
                        return false;
                    }

                    return true;
                }

                Debug.LogError("[FloraTopology1711] Failed to create prefab asset at " + path);
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Renderer AddRenderer(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        /// <summary>
        /// Resolves the family material and fails closed when its shader cannot read the vertex-colour
        /// contract this studio bakes. Loading the material without checking the shader was the silent
        /// failure: a flora material sitting on URP Lit reads none of R sway, G bioluminescence or
        /// B ambient occlusion and has no vertex sway pass at all, so the pack would write a
        /// contract-correct mesh into a prefab that renders as a static untextured mass and report
        /// success.
        /// </summary>
        private static Material ResolveMaterial(FloraTopologyPreset preset)
        {
            string path = preset switch
            {
                FloraTopologyPreset.KelpForestFrond => KelpMaterialPath,
                FloraTopologyPreset.AbyssalBrainCoral => MassiveCoralMaterialPath,
                _ => CoralMaterialPath
            };

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogError("[FloraTopology1711] Required flora material missing. path=" + path);
                return null;
            }

            if (FloraTopologyGenerator1604.IsExpectedFloraShader(preset, material.shader))
                return material;

            Debug.LogError("[FloraTopology1711] Flora material does not use a Hecton8 flora master shader, so it cannot"
                + " consume the baked vertex-colour contract. path=" + path
                + " shader=" + (material.shader != null ? material.shader.name : "<null>")
                + " preset=" + preset);
            return null;
        }

        private static bool RunDeterministicProbe(FloraBakeTier1711 tier)
        {
            FloraTopologyProfile1711 profile = ResolveProfile(tier);
            int count = math.max(8, profile.SpaceColonizationAttractors);
            NativeArray<uint> lSystemTokens = default;
            NativeArray<FloraProbeVertex1711> probes = default;
            NativeArray<float> ao = default;
            NativeArray<int> quadricKeep = default;
            try
            {
                lSystemTokens = new NativeArray<uint>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                probes = new NativeArray<FloraProbeVertex1711>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                ao = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                quadricKeep = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle handle = new LSystemExpansionJob1711
                {
                    Seed = 0x17110000u + (uint)tier,
                    Iterations = profile.LSystemIterations,
                    Tokens = lSystemTokens
                }.Schedule(count, 32);

                handle = new SpaceColonizationProbeJob1711
                {
                    Seed = 0xA1711000u + (uint)tier,
                    Tokens = lSystemTokens,
                    Vertices = probes,
                    Quality = profile.GlobalQualityWeight
                }.Schedule(count, 32, handle);

                handle = new HemisphereAoJob1711
                {
                    Vertices = probes,
                    AmbientOcclusion = ao,
                    SampleCount = profile.HemisphereAoSamples
                }.Schedule(count, 32, handle);

                handle = new SemanticVertexBakeJob1711
                {
                    Vertices = probes,
                    AmbientOcclusion = ao,
                    Quality = profile.GlobalQualityWeight
                }.Schedule(count, 32, handle);

                handle = new QuadricDecimationProxyJob1711
                {
                    Vertices = probes,
                    KeepFlags = quadricKeep,
                    Quality = profile.GlobalQualityWeight
                }.Schedule(count, 32, handle);

                handle.Complete();
                return probes[0].PackedColor != 0u && quadricKeep[0] != 0;
            }
            finally
            {
                if (quadricKeep.IsCreated) quadricKeep.Dispose();
                if (ao.IsCreated) ao.Dispose();
                if (probes.IsCreated) probes.Dispose();
                if (lSystemTokens.IsCreated) lSystemTokens.Dispose();
            }
        }

        private static bool TryPackDryRunAtlas(FloraTopologyProfile1711 profile)
        {
            MaxRectsPacker1711 packer = new MaxRectsPacker1711(profile.UvAtlasResolution, profile.UvAtlasResolution);
            bool ok = true;
            ok &= packer.TryPack(96, 192, out _);
            ok &= packer.TryPack(128, 128, out _);
            ok &= packer.TryPack(64, 256, out _);
            return ok;
        }

        private static bool ValidateUnmanagedLayouts()
        {
            bool ok = true;
            ok &= ValidateUnmanagedLayout<FloraTopologyProfile1711>(nameof(FloraTopologyProfile1711));
            ok &= ValidateUnmanagedLayout<FloraProbeVertex1711>(nameof(FloraProbeVertex1711));
            ok &= ValidateUnmanagedLayout<FloraUvRect1711>(nameof(FloraUvRect1711));
            return ok;
        }

        private static bool ValidateUnmanagedLayout<T>(string label) where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            if ((size & 7) == 0)
                return true;

            Debug.LogError("[FloraTopology1711] " + label + " layout size must be 8-byte aligned. size=" + size);
            return false;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct LSystemExpansionJob1711 : IJobParallelFor
    {
        public uint Seed;
        public int Iterations;
        [WriteOnly] public NativeArray<uint> Tokens;

        public void Execute(int index)
        {
            uint value = Seed ^ (uint)(index * 747796405u);
            int iterations = math.clamp(Iterations, 1, 8);
            for (int i = 0; i < iterations; i++)
                value = (value ^ (value >> 16)) * 2246822519u + 3266489917u;
            Tokens[index] = value;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct SpaceColonizationProbeJob1711 : IJobParallelFor
    {
        public uint Seed;
        [ReadOnly] public NativeArray<uint> Tokens;
        [WriteOnly] public NativeArray<FloraProbeVertex1711> Vertices;
        public float Quality;

        public void Execute(int index)
        {
            uint token = Tokens[index] ^ Seed;
            float angle = ((token & 1023u) / 1023f) * math.PI * 2f;
            float radius = math.lerp(0.18f, 1.75f, math.saturate(Quality)) * (0.25f + ((token >> 10) & 255u) / 255f);
            float3 position = new float3(math.cos(angle) * radius, index * 0.00625f, math.sin(angle) * radius);
            float3 normal = math.normalizesafe(new float3(position.x, 0.35f, position.z), new float3(0f, 1f, 0f));
            Vertices[index] = new FloraProbeVertex1711
            {
                Position = position,
                Normal = normal,
                Wear = ((token >> 18) & 255u) / 255f,
                PackedColor = 0u
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct HemisphereAoJob1711 : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FloraProbeVertex1711> Vertices;
        [WriteOnly] public NativeArray<float> AmbientOcclusion;
        public int SampleCount;

        public void Execute(int index)
        {
            FloraProbeVertex1711 vertex = Vertices[index];
            float upWeight = math.saturate(vertex.Normal.y);
            float samplePenalty = math.rcp(math.max(1, SampleCount));
            AmbientOcclusion[index] = math.saturate(0.35f + upWeight * 0.55f - samplePenalty);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct SemanticVertexBakeJob1711 : IJobParallelFor
    {
        public NativeArray<FloraProbeVertex1711> Vertices;
        [ReadOnly] public NativeArray<float> AmbientOcclusion;
        public float Quality;

        public void Execute(int index)
        {
            FloraProbeVertex1711 vertex = Vertices[index];
            byte sway = (byte)math.clamp((int)math.round(math.saturate(vertex.Position.y * 0.18f + Quality * 0.42f) * 255f), 0, 255);
            byte bio = (byte)math.clamp((int)math.round(math.saturate(0.18f + Quality * 0.62f) * 255f), 0, 255);
            byte ao = (byte)math.clamp((int)math.round(math.saturate(AmbientOcclusion[index]) * 255f), 0, 255);
            byte wear = (byte)math.clamp((int)math.round(math.saturate(vertex.Wear) * 255f), 0, 255);
            vertex.PackedColor = (uint)(sway | (bio << 8) | (ao << 16) | (wear << 24));
            Vertices[index] = vertex;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct QuadricDecimationProxyJob1711 : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FloraProbeVertex1711> Vertices;
        [WriteOnly] public NativeArray<int> KeepFlags;
        public float Quality;

        public void Execute(int index)
        {
            FloraProbeVertex1711 vertex = Vertices[index];
            float curvatureProxy = math.lengthsq(vertex.Normal - new float3(0f, 1f, 0f));
            float threshold = math.lerp(0.62f, 0.18f, math.saturate(Quality));
            KeepFlags[index] = curvatureProxy >= threshold || (index & 3) == 0 ? 1 : 0;
        }
    }

    internal struct MaxRectsPacker1711
    {
        private readonly int _width;
        private readonly int _height;
        private int _cursorX;
        private int _cursorY;
        private int _rowHeight;

        public MaxRectsPacker1711(int width, int height)
        {
            _width = math.max(1, width);
            _height = math.max(1, height);
            _cursorX = 0;
            _cursorY = 0;
            _rowHeight = 0;
        }

        public bool TryPack(int width, int height, out FloraUvRect1711 rect)
        {
            width = math.max(1, width);
            height = math.max(1, height);
            if (width > _width || height > _height)
            {
                rect = default;
                return false;
            }

            if (_cursorX + width > _width)
            {
                _cursorX = 0;
                _cursorY += _rowHeight;
                _rowHeight = 0;
            }

            if (_cursorY + height > _height)
            {
                rect = default;
                return false;
            }

            rect = new FloraUvRect1711
            {
                X = _cursorX,
                Y = _cursorY,
                Width = width,
                Height = height
            };
            _cursorX += width;
            _rowHeight = math.max(_rowHeight, height);
            return true;
        }
    }

    internal sealed class FloraTopologyStudioWindow1711 : EditorWindow
    {
        private FloraTopologyPreset _preset = FloraTopologyPreset.KelpForestFrond;
        private FloraBakeTier1711 _tier = FloraBakeTier1711.High;
        private uint _seed = 17110042u;

        public static void Open()
        {
            FloraTopologyStudioWindow1711 window = GetWindow<FloraTopologyStudioWindow1711>();
            window.titleContent = new GUIContent("Flora 1711");
            window.minSize = new Vector2(360f, 180f);
            window.Show();
        }

        private void OnGUI()
        {
            _preset = (FloraTopologyPreset)EditorGUILayout.EnumPopup("Preset", _preset);
            _tier = (FloraBakeTier1711)EditorGUILayout.EnumPopup("Quality Tier", _tier);
            _seed = (uint)EditorGUILayout.LongField("Seed", _seed);

            FloraTopologyProfile1711 profile = FloraTopologyStudio1711.ResolveProfile(_tier);
            EditorGUILayout.LabelField("GlobalQualityWeight", profile.GlobalQualityWeight.ToString("0.00"));
            EditorGUILayout.LabelField("LOD0 Triangle Budget", profile.Lod0TriangleBudget.ToString());
            EditorGUILayout.LabelField("Vertex Colors", "R sway, G bio, B AO, A wear");

            if (GUILayout.Button("Generate Static Mesh + Prefab"))
                FloraTopologyStudio1711.GenerateAndSave(_preset, _seed, profile);

            if (GUILayout.Button("Run Dry Verification"))
                FloraTopologyStudio1711.RunDryVerification();
        }
    }
}
#endif
