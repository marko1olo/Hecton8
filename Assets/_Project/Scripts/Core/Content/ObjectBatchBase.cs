using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core.Content
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct ObjectBatchInstance
    {
        [FieldOffset(0)] public Matrix4x4 LocalToWorld;
        [FieldOffset(64)] public uint AssetHash;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public int MeshIndex;
        [FieldOffset(76)] public int MaterialIndex;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ObjectBatchChunk
    {
        [FieldOffset(0)] public Bounds Bounds;
        [FieldOffset(24)] public int StartIndex;
        [FieldOffset(28)] public int Count;
        [FieldOffset(32)] public uint ChunkHash;
        [FieldOffset(36)] public byte LodLevel;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
    }

    /// <summary>
    /// Static wreck/debris chunk payload for BRG-compatible renderers. No GameObject scanning after bake.
    /// </summary>
    public abstract class ObjectBatchBase : ScriptableObject
    {
        [SerializeField] private Mesh[] meshes = Array.Empty<Mesh>();
        [SerializeField] private Material[] materials = Array.Empty<Material>();
        [SerializeField] private ObjectBatchInstance[] instances = Array.Empty<ObjectBatchInstance>();
        [SerializeField] private ObjectBatchChunk[] chunks = Array.Empty<ObjectBatchChunk>();

        public int MeshCount => meshes != null ? meshes.Length : 0;
        public int MaterialCount => materials != null ? materials.Length : 0;
        public int InstanceCount => instances != null ? instances.Length : 0;
        public int ChunkCount => chunks != null ? chunks.Length : 0;

        public Mesh GetMesh(int index)
        {
            return meshes[index];
        }

        public Material GetMaterial(int index)
        {
            return materials[index];
        }

        public ObjectBatchInstance GetInstance(int index)
        {
            return instances[index];
        }

        public ObjectBatchChunk GetChunk(int index)
        {
            return chunks[index];
        }

        public void ReplacePayload(
            Mesh[] bakedMeshes,
            Material[] bakedMaterials,
            ObjectBatchInstance[] bakedInstances,
            ObjectBatchChunk[] bakedChunks)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            ValidatePayloadInput(bakedMeshes, bakedMaterials, bakedInstances, bakedChunks);
            meshes = bakedMeshes ?? Array.Empty<Mesh>();
            materials = bakedMaterials ?? Array.Empty<Material>();
            instances = bakedInstances ?? Array.Empty<ObjectBatchInstance>();
            chunks = bakedChunks ?? Array.Empty<ObjectBatchChunk>();
#endif
        }

#if UNITY_EDITOR
        private static void ValidatePayloadInput(
            Mesh[] bakedMeshes,
            Material[] bakedMaterials,
            ObjectBatchInstance[] bakedInstances,
            ObjectBatchChunk[] bakedChunks)
        {
            int meshCount = bakedMeshes != null ? bakedMeshes.Length : 0;
            int materialCount = bakedMaterials != null ? bakedMaterials.Length : 0;
            int instanceCount = bakedInstances != null ? bakedInstances.Length : 0;
            int chunkCount = bakedChunks != null ? bakedChunks.Length : 0;

            if (meshCount == 0)
                throw new ArgumentException("Object batch payload rejected: mesh table is empty.");
            if (materialCount == 0)
                throw new ArgumentException("Object batch payload rejected: material table is empty.");
            if (instanceCount == 0)
                throw new ArgumentException("Object batch payload rejected: instance table is empty.");
            if (chunkCount == 0)
                throw new ArgumentException("Object batch payload rejected: chunk table is empty.");

            for (int i = 0; i < meshCount; i++)
            {
                if (bakedMeshes[i] == null)
                    throw new ArgumentException("Object batch payload rejected: null mesh at index " + i + ".");
            }

            for (int i = 0; i < materialCount; i++)
            {
                if (bakedMaterials[i] == null)
                    throw new ArgumentException("Object batch payload rejected: null material at index " + i + ".");
            }

            byte[] coverage = new byte[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                ObjectBatchInstance instance = bakedInstances[i];
                if (instance.AssetHash == 0u)
                    throw new ArgumentException("Object batch payload rejected: zero asset hash at instance " + i + ".");
                if (instance.MeshIndex < 0 || instance.MeshIndex >= meshCount)
                    throw new ArgumentException("Object batch payload rejected: invalid mesh index at instance " + i + ".");
                if (instance.MaterialIndex < 0 || instance.MaterialIndex >= materialCount)
                    throw new ArgumentException("Object batch payload rejected: invalid material index at instance " + i + ".");
                if (!IsFinite(instance.LocalToWorld))
                    throw new ArgumentException("Object batch payload rejected: non-finite transform at instance " + i + ".");
            }

            for (int i = 0; i < chunkCount; i++)
            {
                ObjectBatchChunk chunk = bakedChunks[i];
                if (chunk.ChunkHash == 0u)
                    throw new ArgumentException("Object batch payload rejected: zero chunk hash at chunk " + i + ".");
                if (chunk.Count <= 0)
                    throw new ArgumentException("Object batch payload rejected: empty chunk at index " + i + ".");
                if (chunk.StartIndex < 0 || chunk.Count > instanceCount || chunk.StartIndex > instanceCount - chunk.Count)
                    throw new ArgumentException("Object batch payload rejected: chunk range exceeds instances at chunk " + i + ".");
                if (chunk.LodLevel > 2)
                    throw new ArgumentException("Object batch payload rejected: unsupported LOD at chunk " + i + ".");
                if (!IsFinite(chunk.Bounds))
                    throw new ArgumentException("Object batch payload rejected: non-finite bounds at chunk " + i + ".");

                int end = chunk.StartIndex + chunk.Count;
                for (int instanceIndex = chunk.StartIndex; instanceIndex < end; instanceIndex++)
                {
                    if (coverage[instanceIndex] != 0)
                        throw new ArgumentException("Object batch payload rejected: overlapping chunk coverage at instance " + instanceIndex + ".");

                    coverage[instanceIndex] = 1;
                }
            }

            for (int i = 0; i < instanceCount; i++)
            {
                if (coverage[i] == 0)
                    throw new ArgumentException("Object batch payload rejected: uncovered instance " + i + ".");
            }
        }

        private static bool IsFinite(Matrix4x4 matrix)
        {
            return IsFinite(matrix.m00) && IsFinite(matrix.m01) &&
                   IsFinite(matrix.m02) && IsFinite(matrix.m03) &&
                   IsFinite(matrix.m10) && IsFinite(matrix.m11) &&
                   IsFinite(matrix.m12) && IsFinite(matrix.m13) &&
                   IsFinite(matrix.m20) && IsFinite(matrix.m21) &&
                   IsFinite(matrix.m22) && IsFinite(matrix.m23) &&
                   IsFinite(matrix.m30) && IsFinite(matrix.m31) &&
                   IsFinite(matrix.m32) && IsFinite(matrix.m33);
        }

        private static bool IsFinite(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
                   IsFinite(extents.x) && IsFinite(extents.y) && IsFinite(extents.z) &&
                   extents.x >= 0f && extents.y >= 0f && extents.z >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
#endif

        public abstract void BindToBatchRendererGroup(BatchRendererGroup group);
    }
}
