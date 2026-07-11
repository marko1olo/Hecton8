using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [ExecuteAlways]
    public class CaveAnomalyInstancedRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;

        public Mesh mesh;
        public Material material;
        public List<Matrix4x4> instances = new List<Matrix4x4>();

        private Matrix4x4[][] _batches;
        private int _batchCount;
        private bool _subscribed;

        private static readonly Bounds _cachedBounds = new Bounds(Vector3.zero, new Vector3(100000, 100000, 100000));

        private void OnEnable()
        {
            RebuildBatches();
            SubscribeRenderPipeline();
        }

        private void OnDisable()
        {
            UnsubscribeRenderPipeline();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildBatches();
        }
#endif

        public void SetInstances(List<Matrix4x4> newInstances)
        {
            instances = newInstances;
            RebuildBatches();
        }

        private void RebuildBatches()
        {
            if (instances == null || instances.Count == 0)
            {
                _batches = null;
                _batchCount = 0;
                return;
            }

            int total = instances.Count;
            _batchCount = (total + MaxInstancesPerBatch - 1) / MaxInstancesPerBatch;
            _batches = new Matrix4x4[_batchCount][];

            for (int i = 0; i < _batchCount; i++)
            {
                int start = i * MaxInstancesPerBatch;
                int count = Mathf.Min(MaxInstancesPerBatch, total - start);
                Matrix4x4[] batch = new Matrix4x4[count];
                for (int j = 0; j < count; j++)
                    batch[j] = instances[start + j];

                _batches[i] = batch;
            }
        }

        private void SubscribeRenderPipeline()
        {
            if (_subscribed)
                return;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _subscribed = true;
        }

        private void UnsubscribeRenderPipeline()
        {
            if (!_subscribed)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _subscribed = false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled || mesh == null || material == null || _batches == null)
                return;

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = _cachedBounds,
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.Off
            };

            for (int i = 0; i < _batchCount; i++)
                global::UnityEngine.Graphics.RenderMeshInstanced(renderParams, mesh, 0, _batches[i]);
        }
    }
}
