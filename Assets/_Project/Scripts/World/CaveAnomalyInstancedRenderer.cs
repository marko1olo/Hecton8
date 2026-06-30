using UnityEngine;
using System.Collections.Generic;

namespace Hecton8.World
{
    [ExecuteAlways]
    public class CaveAnomalyInstancedRenderer : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public List<Matrix4x4> instances = new List<Matrix4x4>();

        private Matrix4x4[][] _batches;
        private int _batchCount;

        private static readonly Bounds _cachedBounds = new Bounds(Vector3.zero, new Vector3(100000, 100000, 100000));

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
            _batchCount = Mathf.CeilToInt(total / 1023f);
            _batches = new Matrix4x4[_batchCount][];

            for (int i = 0; i < _batchCount; i++)
            {
                int start = i * 1023;
                int count = Mathf.Min(1023, total - start);
                _batches[i] = new Matrix4x4[count];
                for (int j = 0; j < count; j++)
                {
                    _batches[i][j] = instances[start + j];
                }
            }
        }

        private void Update()
        {
            if (mesh == null || material == null || _batches == null) return;

            RenderParams rp = new RenderParams(material);
            rp.worldBounds = _cachedBounds;
            rp.receiveShadows = false;
            rp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            for (int i = 0; i < _batchCount; i++)
            {
                UnityEngine.Graphics.RenderMeshInstanced(rp, mesh, 0, _batches[i]);
            }
        }
    }
}
