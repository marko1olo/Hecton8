using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core.Content
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
    public struct ObjectBatchInstance
    {
        public Matrix4x4 LocalToWorld;
        public uint AssetHash;
        public uint Flags;
        public int MeshIndex;
        public int MaterialIndex;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    public struct ObjectBatchChunk
    {
        public Bounds Bounds;
        public int StartIndex;
        public int Count;
        public uint ChunkHash;
        public byte LodLevel;
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

            meshes = bakedMeshes ?? Array.Empty<Mesh>();
            materials = bakedMaterials ?? Array.Empty<Material>();
            instances = bakedInstances ?? Array.Empty<ObjectBatchInstance>();
            chunks = bakedChunks ?? Array.Empty<ObjectBatchChunk>();
#endif
        }

        public abstract void BindToBatchRendererGroup(BatchRendererGroup group);
    }
}
