using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancer
{
    /// <summary>
    /// Add this to the prefabs of GameObjects you want to GPU Instance at runtime.
    /// </summary>
    public class GPUInstancerPrefab : MonoBehaviour
    {
        [HideInInspector]
        public GPUInstancerPrefabPrototype prefabPrototype;
        [NonSerialized]
        public int gpuInstancerID;
        [NonSerialized]
        public PrefabInstancingState state = PrefabInstancingState.None;
        public Dictionary<string, object> variationDataList;

        protected bool _isTransformSet;
        protected Transform _instanceTransform;

        protected bool _isMatrixSet;
        protected Matrix4x4 _localToWorldMatrix;
        private bool _isColliderCached;
        private Collider _cachedCollider;
        private bool _isRigidbodyCached;
        private Rigidbody _cachedRigidbody;

        private bool _isMeshRenderersCached;
        private MeshRenderer[] _cachedMeshRenderers;
        private bool _isBillboardRenderersCached;
        private BillboardRenderer[] _cachedBillboardRenderers;
        private bool _isLODGroupCached;
        private LODGroup _cachedLODGroup;

        public virtual void AddVariation<T>(string bufferName, T value)
        {
            if (variationDataList == null)
                variationDataList = new Dictionary<string, object>();
            if (variationDataList.ContainsKey(bufferName))
                variationDataList[bufferName] = value;
            else
                variationDataList.Add(bufferName, value);
        }

        public virtual Transform GetInstanceTransform(bool forceNew = false)
        {
            if (!_isTransformSet || forceNew)
            {
                _instanceTransform = transform;
                _isTransformSet = true;
            }
            return _instanceTransform;
        }

        public virtual Matrix4x4 GetLocalToWorldMatrix(bool forceNew = false)
        {
            if (!_isMatrixSet || forceNew)
            {
                _localToWorldMatrix = GetInstanceTransform(forceNew).localToWorldMatrix;
                _isMatrixSet = true;
            }
            return _localToWorldMatrix;
        }

        public virtual Collider GetCachedCollider(bool forceNew = false)
        {
            if (!_isColliderCached || forceNew)
            {
                TryGetComponent(out _cachedCollider);
                _isColliderCached = true;
            }

            return _cachedCollider;
        }

        public virtual Rigidbody GetCachedRigidbody(bool forceNew = false)
        {
            if (!_isRigidbodyCached || forceNew)
            {
                TryGetComponent(out _cachedRigidbody);
                _isRigidbodyCached = true;
            }

            return _cachedRigidbody;
        }

        public virtual MeshRenderer[] GetCachedMeshRenderers(bool forceNew = false)
        {
            if (!_isMeshRenderersCached || forceNew)
            {
                _cachedMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
                _isMeshRenderersCached = true;
            }
            return _cachedMeshRenderers;
        }

        public virtual BillboardRenderer[] GetCachedBillboardRenderers(bool forceNew = false)
        {
            if (!_isBillboardRenderersCached || forceNew)
            {
                _cachedBillboardRenderers = GetComponentsInChildren<BillboardRenderer>(true);
                _isBillboardRenderersCached = true;
            }
            return _cachedBillboardRenderers;
        }

        public virtual LODGroup GetCachedLODGroup(bool forceNew = false)
        {
            if (!_isLODGroupCached || forceNew)
            {
                TryGetComponent(out _cachedLODGroup);
                _isLODGroupCached = true;
            }
            return _cachedLODGroup;
        }
        public virtual void SetupPrefabInstance(GPUInstancerRuntimeData runtimeData, bool forceNew = false)
        {

        }
    }

    public enum PrefabInstancingState
    {
        None,
        Disabled,
        Instanced
    }
}
