#if UNITY_EDITOR
using Hecton8.World.VoxelTerrainSeamBinder;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.VoxelTerrainSeamBinder.Editor
{
    [InitializeOnLoad]
    public static class VoxelTerrainSeamPreviewStore
    {
        private const int MaxPreviewLines = 4096;
        private static Mesh s_previewMesh;
        private static Material s_previewMaterial;
        private static int s_count;

        static VoxelTerrainSeamPreviewStore()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            SceneView.duringSceneGui += DrawScenePreview;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposePreviewResources;
            AssemblyReloadEvents.beforeAssemblyReload += DisposePreviewResources;
            EditorApplication.quitting -= DisposePreviewResources;
            EditorApplication.quitting += DisposePreviewResources;
        }

        public static int Count
        {
            get { return s_count; }
        }

        public static void Clear()
        {
            s_count = 0;
            if (s_previewMesh != null)
                s_previewMesh.Clear();
            SceneView.RepaintAll();
        }

        public static void Set(NativeArray<SeamSnapResult64> snapResults)
        {
            EnsurePreviewMesh();
            s_count = 0;
            int limit = snapResults.Length;
            int vertexCapacity = MaxPreviewLines * 4;
            int indexCapacity = MaxPreviewLines * 6;
            NativeArray<Vector3> vertices = default;
            NativeArray<int> indices = default;
            try
            {
                vertices = new NativeArray<Vector3>(vertexCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                indices = new NativeArray<int>(indexCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                float3 min = new float3(float.MaxValue);
                float3 max = new float3(float.MinValue);

                for (int i = 0; i < limit && s_count < MaxPreviewLines; i++)
                {
                    SeamSnapResult64 snap = snapResults[i];
                    if (snap.VoxelVertexIndex < 0)
                        continue;

                    float3 from = snap.OriginalLocalPosition;
                    float3 to = snap.SnappedLocalPosition;
                    if (!math.all(math.isfinite(from)) || !math.all(math.isfinite(to)))
                        continue;

                    float3 delta = to - from;
                    if (math.dot(delta, delta) <= 0.000001f)
                        continue;

                    float3 direction = math.normalizesafe(delta, new float3(1f, 0f, 0f));
                    float3 side = math.normalizesafe(math.cross(direction, new float3(0f, 1f, 0f)), float3.zero);
                    if (math.dot(side, side) <= 0.000001f)
                        side = math.normalizesafe(math.cross(direction, new float3(1f, 0f, 0f)), new float3(0f, 0f, 1f));
                    float3 offset = side * 0.035f;
                    float3 p0 = from - offset;
                    float3 p1 = from + offset;
                    float3 p2 = to + offset;
                    float3 p3 = to - offset;

                    int vertexStart = s_count * 4;
                    vertices[vertexStart] = new Vector3(p0.x, p0.y, p0.z);
                    vertices[vertexStart + 1] = new Vector3(p1.x, p1.y, p1.z);
                    vertices[vertexStart + 2] = new Vector3(p2.x, p2.y, p2.z);
                    vertices[vertexStart + 3] = new Vector3(p3.x, p3.y, p3.z);
                    int indexStart = s_count * 6;
                    indices[indexStart] = vertexStart;
                    indices[indexStart + 1] = vertexStart + 1;
                    indices[indexStart + 2] = vertexStart + 2;
                    indices[indexStart + 3] = vertexStart;
                    indices[indexStart + 4] = vertexStart + 2;
                    indices[indexStart + 5] = vertexStart + 3;
                    min = math.min(math.min(min, p0), math.min(math.min(p1, p2), p3));
                    max = math.max(math.max(max, p0), math.max(math.max(p1, p2), p3));
                    s_count++;
                }

                UploadPreviewMesh(vertices, indices, min, max, s_count * 4, s_count * 6);
            }
            finally
            {
                if (indices.IsCreated)
                    indices.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
            }

            SceneView.RepaintAll();
        }

        public static void DrawGizmos()
        {
            DrawPreviewMesh();
        }

        private static void DrawScenePreview(SceneView sceneView)
        {
            DrawPreviewMesh();
        }

        private static void UploadPreviewMesh(NativeArray<Vector3> vertices, NativeArray<int> indices, float3 min, float3 max, int vertexCount, int indexCount)
        {
            EnsurePreviewMesh();
            s_previewMesh.Clear();
            if (vertexCount <= 0 || indexCount <= 0 || !math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                return;

            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            s_previewMesh.SetVertexBufferParams(vertexCount, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0));
            s_previewMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            s_previewMesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, flags);
            s_previewMesh.SetIndexBufferData(indices, 0, 0, indexCount, flags);
            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            Bounds bounds = new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
            s_previewMesh.subMeshCount = 1;
            s_previewMesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, flags);
            s_previewMesh.bounds = bounds;
        }

        private static void DrawPreviewMesh()
        {
            if (s_count <= 0 || s_previewMesh == null)
                return;
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            EnsurePreviewMaterial();
            if (s_previewMaterial == null)
                return;

            s_previewMaterial.SetPass(0);
            UnityEngine.Graphics.DrawMeshNow(s_previewMesh, Matrix4x4.identity);
        }

        private static void EnsurePreviewMesh()
        {
            if (s_previewMesh != null)
                return;

            s_previewMesh = new Mesh
            {
                name = "SHINOBU_246_SeamPreviewLines",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32
            };
        }

        private static void EnsurePreviewMaterial()
        {
            if (s_previewMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;

            s_previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            s_previewMaterial.SetColor("_Color", new Color(1f, 0.05f, 0.02f, 0.95f));
            s_previewMaterial.SetInt("_ZWrite", 0);
            s_previewMaterial.SetInt("_Cull", (int)CullMode.Off);
            s_previewMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        private static void DisposePreviewResources()
        {
            s_count = 0;
            if (s_previewMesh != null)
            {
                Object.DestroyImmediate(s_previewMesh);
                s_previewMesh = null;
            }

            if (s_previewMaterial != null)
            {
                Object.DestroyImmediate(s_previewMaterial);
                s_previewMaterial = null;
            }
        }
    }

    /// <summary>
    /// Optional editor-only scene hook for seam-pull preview.
    /// It never mutates mesh data and is not compiled into player builds.
    /// </summary>
    public sealed class VoxelTerrainSeamPreviewGizmo : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            VoxelTerrainSeamPreviewStore.DrawGizmos();
        }
    }
}
#endif
