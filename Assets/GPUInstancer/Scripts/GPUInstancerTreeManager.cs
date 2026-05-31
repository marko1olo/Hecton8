using System.Collections.Generic;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GPUInstancer
{
    /// <summary>
    /// Add this to a Unity terrain for GPU Instancing terrain trees at runtime.
    /// </summary>
    [ExecuteInEditMode]
    public class GPUInstancerTreeManager : GPUInstancerTerrainManager
    {
        private static ComputeShader _treeInstantiationComputeShader;
        private static int _treeInstantiationKernelId = -1;
        private static int _treeInstantiationThreadGroupSizeX;
        public bool initializeWithCoroutine = true;
        private bool _isCoroutineActive;

        #region MonoBehaviour Methods
        public override void Awake()
        {
            base.Awake();

            EnsureTreeInstantiationComputeShader();
        }

        public override void Update()
        {
            base.Update();

            if (Application.isPlaying && _requiresTerrainUpdate && !_isCoroutineActive)
            {
                StartCoroutine(ReplaceUnityTrees());
                _requiresTerrainUpdate = false;
            }
        }

        #endregion MonoBehaviour Methods

        #region Override Methods

        public override void ClearInstancingData()
        {
            base.ClearInstancingData();

            if (_terrains != null)
            {
                foreach (Terrain terrain in _terrains)
                {
                    if (terrain != null && terrain.treeDistance == 0)
                        terrain.treeDistance = terrainSettings.maxTreeDistance;
                }
            }
        }

        public override void GeneratePrototypes(bool forceNew = false)
        {
            base.GeneratePrototypes(forceNew);

            if (terrainSettings != null && terrain != null && terrain.terrainData != null)
            {
                GPUInstancerUtility.SetTreeInstancePrototypes(gameObject, prototypeList, terrain.terrainData.treePrototypes, terrainSettings, forceNew);
            }
        }

#if UNITY_EDITOR
        public override void CheckPrototypeChanges()
        {
            base.CheckPrototypeChanges();

            if (!Application.isPlaying && terrainSettings != null && terrain != null && terrain.terrainData != null)
            {
                if (prototypeList.Count != terrain.terrainData.treePrototypes.Length)
                {
                    GeneratePrototypes();
                }

                int index = 0;
                foreach (GPUInstancerTreePrototype prototype in prototypeList)
                {
                    prototype.prototypeIndex = index;
                    index++;
                }
            }
        }
#endif

        public override void InitializeRuntimeDataAndBuffers(bool forceNew = true)
        {
            base.InitializeRuntimeDataAndBuffers(forceNew);

            if (!forceNew && isInitialized)
                return;

            if (terrainSettings == null)
                return;

            if (prototypeList != null && prototypeList.Count > 0)
            {
                GPUInstancerUtility.AddTreeInstanceRuntimeDataToList(runtimeDataList, prototypeList, terrainSettings);
            }

            StartCoroutine(ReplaceUnityTrees());

            isInitialized = true;
        }

        public override void DeletePrototype(GPUInstancerPrototype prototype, bool removeSO = true)
        {
            if (terrainSettings != null && terrain != null && terrain.terrainData != null)
            {
                int treePrototypeIndex = prototypeList.IndexOf(prototype);

                TreePrototype[] treePrototypes = terrain.terrainData.treePrototypes;
                List<TreePrototype> newTreePrototypes = new List<TreePrototype>(treePrototypes);
                List<TreeInstance> newTreeInstanceList = new List<TreeInstance>();
                TreeInstance treeInstance;

                for (int i = 0; i < terrain.terrainData.treeInstances.Length; i++)
                {
                    treeInstance = terrain.terrainData.treeInstances[i];
                    if (treeInstance.prototypeIndex < treePrototypeIndex)
                    {
                        newTreeInstanceList.Add(treeInstance);
                    }
                    else if (treeInstance.prototypeIndex > treePrototypeIndex)
                    {
                        treeInstance.prototypeIndex = treeInstance.prototypeIndex - 1;
                        newTreeInstanceList.Add(treeInstance);
                    }
                }

                if (newTreePrototypes.Count > treePrototypeIndex)
                    newTreePrototypes.RemoveAt(treePrototypeIndex);

                terrain.terrainData.treeInstances = newTreeInstanceList.ToArray();
                terrain.terrainData.treePrototypes = newTreePrototypes.ToArray();

                terrain.terrainData.RefreshPrototypes();

                if (removeSO)
                    base.DeletePrototype(prototype, removeSO);
                GeneratePrototypes(false);
                if (!removeSO)
                    base.DeletePrototype(prototype, removeSO);
            }
            else
                base.DeletePrototype(prototype, removeSO);
        }

        #endregion Override Methods

        private static bool EnsureTreeInstantiationComputeShader()
        {
            if (_treeInstantiationComputeShader != null && _treeInstantiationKernelId >= 0 && _treeInstantiationThreadGroupSizeX > 0)
                return true;

            _treeInstantiationComputeShader = Resources.Load<ComputeShader>(GPUInstancerConstants.TREE_INSTANTIATION_RESOURCE_PATH);
            if (!GPUInstancerConstants.TryFindKernel(_treeInstantiationComputeShader, GPUInstancerConstants.TREE_INSTANTIATION_KERNEL, out _treeInstantiationKernelId))
            {
                _treeInstantiationComputeShader = null;
                _treeInstantiationKernelId = -1;
                _treeInstantiationThreadGroupSizeX = 0;
                return false;
            }

            if (!TryResolveTreeInstantiationThreadGroupSize(out _treeInstantiationThreadGroupSizeX))
            {
                _treeInstantiationComputeShader = null;
                _treeInstantiationKernelId = -1;
                _treeInstantiationThreadGroupSizeX = 0;
                return false;
            }

            return true;
        }

        private static bool TryResolveTreeInstantiationThreadGroupSize(out int threadGroupSizeX)
        {
            threadGroupSizeX = 0;
            if (!GPUInstancerConstants.TryGetPortableKernelThreadGroupSizes(
                    _treeInstantiationComputeShader,
                    _treeInstantiationKernelId,
                    out int sizeX,
                    out int sizeY,
                    out int sizeZ))
                return false;

            if (sizeY != 1 || sizeZ != 1)
                return false;

            threadGroupSizeX = sizeX;
            return true;
        }

        private static int GetTreeInstantiationThreadGroupCount(int elementCount)
        {
            if (elementCount <= 0 || _treeInstantiationThreadGroupSizeX <= 0)
                return 0;

            long groupCount = ((long)elementCount + _treeInstantiationThreadGroupSizeX - 1L) / _treeInstantiationThreadGroupSizeX;
            if (groupCount <= 0L || groupCount > GPUInstancerConstants.MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groupCount;
        }

        public IEnumerator ReplaceUnityTrees()
        {
            _isCoroutineActive = true;
            try
            {
                if (!EnsureTreeInstantiationComputeShader())
                {
                    yield break;
                }

                int prototypeCount = prototypeList != null ? prototypeList.Count : 0;
                if (prototypeCount > 0)
                {
                    Vector4[] treeScales = new Vector4[prototypeCount];
                    for (int i = 0; i < prototypeCount; i++)
                    {
                        GPUInstancerTreePrototype tp = prototypeList[i] as GPUInstancerTreePrototype;
                        treeScales[i] = tp != null && tp.isApplyPrefabScale && tp.prefabObject != null ? tp.prefabObject.transform.localScale : Vector3.one;
                    }
                    int[] instanceCounts = new int[prototypeCount];

                    List<Vector4> treeDataList = new List<Vector4>(); // prototypeIndex - positionx3 - rotation - scalex2

                    int instanceTotal = 0;
                    int terrainCount = _terrains != null ? _terrains.Count : 0;
                    for (int terrainIndex = 0; terrainIndex < terrainCount; terrainIndex++)
                    {
                        Terrain terrain = _terrains[terrainIndex];
                        if (terrain == null)
                            continue;
                        TerrainData terrainData = terrain.terrainData;
                        if (terrainData == null)
                            continue;

                        TreePrototype[] treePrototypes = terrainData.treePrototypes;
                        int treePrototypeCount = treePrototypes != null ? treePrototypes.Length : 0;
                        if (treePrototypeCount > prototypeCount)
                        {
                            Debug.LogError("Additional Terrain has more Tree prototypes than defined prototypes on the Tree Manager. Tree Manager requires every Terrain to have the same Tree prototypes defined.", terrain);
                            continue;
                        }

                        terrain.treeDistance = 0f; // will not persist if called at runtime.
                        Vector3 terrainSize = terrainData.size;
                        Vector3 terrainPosition = terrain.GetPosition();
                        TreeInstance[] treeInstances = terrainData.treeInstances;
                        int treeInstanceCount = treeInstances != null ? treeInstances.Length : 0;
                        for (int treeIndex = 0; treeIndex < treeInstanceCount; treeIndex++)
                        {
                            TreeInstance treeInstance = treeInstances[treeIndex];
                            int treePrototypeIndex = treeInstance.prototypeIndex;
                            if (treePrototypeIndex < 0 || treePrototypeIndex >= prototypeCount || treePrototypeIndex >= treePrototypeCount)
                                continue;

                            treeDataList.Add(new Vector4(
                                treePrototypeIndex,
                                treeInstance.position.x * terrainSize.x + terrainPosition.x,
                                treeInstance.position.y * terrainSize.y + terrainPosition.y,
                                treeInstance.position.z * terrainSize.z + terrainPosition.z
                                ));

                            treeDataList.Add(new Vector4(
                                treeInstance.rotation,
                                treeInstance.widthScale,
                                treeInstance.heightScale,
                                0
                                ));
                            instanceCounts[treePrototypeIndex]++;
                            instanceTotal++;
                        }
                    }

                    if (instanceTotal > 0)
                    {
                        if (initializeWithCoroutine && !isInitialized)
                            yield return null;

                        ComputeBuffer treeDataBuffer = null;
                        ComputeBuffer treeScalesBuffer = null;
                        ComputeBuffer counterBuffer = null;

                        try
                        {
                            treeDataBuffer = new ComputeBuffer(treeDataList.Count, GPUInstancerConstants.STRIDE_SIZE_FLOAT4);
#if UNITY_2019_1_OR_NEWER
                            treeDataBuffer.SetData(treeDataList);
#else
                            treeDataBuffer.SetData(treeDataList.ToArray());
#endif
                            treeScalesBuffer = new ComputeBuffer(treeScales.Length, GPUInstancerConstants.STRIDE_SIZE_FLOAT4);
                            treeScalesBuffer.SetData(treeScales);
                            counterBuffer = new ComputeBuffer(1, GPUInstancerConstants.STRIDE_SIZE_INT);
                            uint[] emptyCounterData = new uint[1];

                            int treeDataLength = treeDataList.Count;
                            int treeScalesLength = treeScales.Length;
                            treeDataList = null;
                            treeScales = null;

                            GPUInstancerRuntimeData runtimeData;
                            int runtimeDataCount = runtimeDataList != null ? runtimeDataList.Count : 0;
                            int instancedPrototypeCount = runtimeDataCount < instanceCounts.Length ? runtimeDataCount : instanceCounts.Length;
                            for (int i = 0; i < instancedPrototypeCount; i++)
                            {
                                runtimeData = runtimeDataList[i];
                                GPUInstancerTreePrototype treePrototype = runtimeData != null ? runtimeData.prototype as GPUInstancerTreePrototype : null;
                                if (runtimeData == null || treePrototype == null)
                                    continue;

                                int instanceCount = instanceCounts[i];
                                runtimeData.bufferSize = instanceCount;
                                runtimeData.instanceCount = instanceCount;
                                if (instanceCount <= 0)
                                {
                                    GPUInstancerUtility.ReleaseInstanceBuffers(runtimeData);
                                    continue;
                                }

                                counterBuffer.SetData(emptyCounterData);
                                if (runtimeData.transformationMatrixVisibilityBuffer != null)
                                    runtimeData.transformationMatrixVisibilityBuffer.Release();
                                runtimeData.transformationMatrixVisibilityBuffer = new ComputeBuffer(instanceCount, GPUInstancerConstants.STRIDE_SIZE_MATRIX4X4);

                                _treeInstantiationComputeShader.SetBuffer(_treeInstantiationKernelId,
                                    GPUInstancerConstants.VisibilityKernelPoperties.INSTANCE_DATA_BUFFER, runtimeData.transformationMatrixVisibilityBuffer);
                                _treeInstantiationComputeShader.SetBuffer(_treeInstantiationKernelId,
                                    GPUInstancerConstants.TreeKernelProperties.TREE_DATA, treeDataBuffer);
                                _treeInstantiationComputeShader.SetBuffer(_treeInstantiationKernelId,
                                    GPUInstancerConstants.TreeKernelProperties.TREE_SCALES, treeScalesBuffer);
                                _treeInstantiationComputeShader.SetBuffer(_treeInstantiationKernelId,
                                    GPUInstancerConstants.GrassKernelProperties.COUNTER_BUFFER, counterBuffer);
                                _treeInstantiationComputeShader.SetInt(
                                    GPUInstancerConstants.VisibilityKernelPoperties.BUFFER_PARAMETER_BUFFER_SIZE, instanceTotal);
                                _treeInstantiationComputeShader.SetInt(
                                    GPUInstancerConstants.TreeKernelProperties.TREE_DATA_LENGTH, treeDataLength);
                                _treeInstantiationComputeShader.SetInt(
                                    GPUInstancerConstants.TreeKernelProperties.TREE_SCALES_LENGTH, treeScalesLength);
                                _treeInstantiationComputeShader.SetInt(
                                    GPUInstancerConstants.TreeKernelProperties.INSTANCE_CAPACITY, instanceCount);
                                //_treeInstantiationComputeShader.SetVector(
                                //    GPUInstancerConstants.GrassKernelProperties.TERRAIN_SIZE_DATA, terrain.terrainData.size);
                                //_treeInstantiationComputeShader.SetVector(
                                //    GPUInstancerConstants.TreeKernelProperties.TERRAIN_POSITION, terrain.GetPosition());
                                _treeInstantiationComputeShader.SetBool(
                                    GPUInstancerConstants.TreeKernelProperties.IS_APPLY_ROTATION, treePrototype.isApplyRotation);
                                _treeInstantiationComputeShader.SetBool(
                                    GPUInstancerConstants.TreeKernelProperties.IS_APPLY_TERRAIN_HEIGHT, treePrototype.isApplyTerrainHeight);
                                _treeInstantiationComputeShader.SetInt(
                                    GPUInstancerConstants.TreeKernelProperties.PROTOTYPE_INDEX, i);

                                int dispatchGroups = GetTreeInstantiationThreadGroupCount(instanceTotal);
                                if (dispatchGroups <= 0)
                                {
                                    GPUInstancerUtility.ReleaseInstanceBuffers(runtimeData);
                                    continue;
                                }

                                _treeInstantiationComputeShader.Dispatch(_treeInstantiationKernelId, dispatchGroups, 1, 1);

                                GPUInstancerUtility.InitializeGPUBuffer(runtimeData);

                                if (initializeWithCoroutine && !isInitialized)
                                    yield return null;
                            }

                            for (int i = instancedPrototypeCount; i < runtimeDataCount; i++)
                            {
                                GPUInstancerUtility.ReleaseInstanceBuffers(runtimeDataList[i]);
                            }
                        }
                        finally
                        {
                            if (treeDataBuffer != null)
                                treeDataBuffer.Release();
                            if (treeScalesBuffer != null)
                                treeScalesBuffer.Release();
                            if (counterBuffer != null)
                                counterBuffer.Release();
                        }
                    }
                    else
                    {
                        GPUInstancerUtility.ReleaseInstanceBuffers(runtimeDataList);
                    }
                }

                isInitial = true;
                if (!isInitialized)
                    GPUInstancerUtility.TriggerEvent(GPUInstancerEventType.TreeInitializationFinished);
            }
            finally
            {
                _isCoroutineActive = false;
            }
        }
    }
}
