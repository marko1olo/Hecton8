using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ComputeDispatchSizingEditTests
    {
        private static readonly int[] s_primeWorkItemCounts =
        {
            1,
            31,
            63,
            127,
            257,
            1021,
            65537,
            1000003
        };

        private static readonly int[] s_mobileSafeThreadGroupSizes =
        {
            1,
            4,
            8,
            16,
            32,
            64,
            128,
            256
        };

        [Test]
        public void PrimeWorkItemCounts_AreCoveredByIntegerCeilDispatchGroups()
        {
            for (int countIndex = 0; countIndex < s_primeWorkItemCounts.Length; countIndex++)
            {
                int workItemCount = s_primeWorkItemCounts[countIndex];
                for (int groupIndex = 0; groupIndex < s_mobileSafeThreadGroupSizes.Length; groupIndex++)
                {
                    int threadGroupSize = s_mobileSafeThreadGroupSizes[groupIndex];
                    int dispatchGroups = CeilDividePositive(workItemCount, threadGroupSize);
                    long coveredItems = (long)dispatchGroups * threadGroupSize;
                    long previousCoverage = (long)(dispatchGroups - 1) * threadGroupSize;

                    Assert.Greater(dispatchGroups, 0);
                    Assert.GreaterOrEqual(coveredItems, workItemCount);
                    Assert.Less(previousCoverage, workItemCount);
                    Assert.Less(coveredItems - workItemCount, threadGroupSize);
                }
            }
        }

        [Test]
        public void ZeroOrNegativeWorkItems_ProduceNoDispatchGroups()
        {
            Assert.AreEqual(0, CeilDividePositive(0, 64));
            Assert.AreEqual(0, CeilDividePositive(-1, 64));
        }

        [Test]
        public void DispatchCeilMath_SurvivesLargePrimeWithoutIntOverflow()
        {
            const int workItemCount = 2147483629;
            const int threadGroupSize = 256;

            int dispatchGroups = CeilDividePositive(workItemCount, threadGroupSize);
            long coveredItems = (long)dispatchGroups * threadGroupSize;

            Assert.GreaterOrEqual(coveredItems, workItemCount);
            Assert.Less(coveredItems - workItemCount, threadGroupSize);
        }

        [Test]
        public void TwoDimensionalFrameCountMultiplier_UsesLongDenominator()
        {
            const int workItemCount = int.MaxValue;
            const int threadGroupSize = 16;
            const int frameCount = int.MaxValue;

            int dispatchGroups = CeilDividePositive(workItemCount, (long)threadGroupSize * frameCount);

            Assert.AreEqual(1, dispatchGroups);
        }

        [Test]
        public void SetDataPartialKernel_UsesExplicitSourceAndDestinationCapacityGuards()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSInstancedComputeBufferSetDataPartialKernel.compute");

            Assert.That(shader, Does.Contain("uniform uint computeBufferCapacity;"));
            Assert.That(shader, Does.Contain("uniform uint managedBufferCapacity;"));
            Assert.That(shader, Does.Contain("id.x >= managedBufferCapacity"));
            Assert.That(shader, Does.Contain("destinationIndex >= computeBufferCapacity"));
            Assert.That(shader, Does.Not.Contain("gpuiInstanceData[computeBufferStartIndex + id.x]"));
        }

        [Test]
        public void CpuCopyHelpers_ClampSourceAndDestinationCapacityBeforeDispatch()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("runtimeData == null || runtimeData.bufferSize <= 0"));
            Assert.That(source, Does.Contain("runtimeData == null || runtimeData.bufferSize <= 0 || runtimeData.instanceLODs == null"));
            Assert.That(source, Does.Contain("computeBuffer == null || data == null || count <= 0"));
            Assert.That(source, Does.Contain("computeBufferStartIndex >= computeBuffer.count"));
            Assert.That(source, Does.Contain("int safeCount = count;"));
            Assert.That(source, Does.Contain("managedRemaining < safeCount"));
            Assert.That(source, Does.Contain("computeRemaining < safeCount"));
            Assert.That(source, Does.Contain("managedBuffer.count >= safeCount && managedData.Length >= safeCount"));
            Assert.That(source, Does.Contain("managedBuffer.SetData(managedData, 0, 0, safeCount)"));
            Assert.That(source, Does.Contain("managedBuffer.count < safeCount"));
            Assert.That(source, Does.Contain("GetComputeThreadGroupCount(safeCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(count), 1, 1);"));
        }

        [Test]
        public void BufferToTextureKernel_UsesCapacityAndIntegerIndexGuards()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSInstancedBufferToTexture.compute");

            Assert.That(shader, Does.Contain("uniform uint argsBufferLength;"));
            Assert.That(shader, Does.Contain("uniform uint textureCapacity;"));
            Assert.That(shader, Does.Contain("argsBufferIndex >= argsBufferLength"));
            Assert.That(shader, Does.Contain("id.x >= textureCapacity"));
            Assert.That(shader, Does.Contain("id.x >= argsBuffer[argsBufferIndex]"));
            Assert.That(shader, Does.Contain("uint indexY = id.x / maxTextureSize;"));
            Assert.That(shader, Does.Contain("instanceId >= bufferSize"));
            Assert.That(shader, Does.Not.Contain("floor(id.x / float(maxTextureSize))"));
        }

        [Test]
        public void RuntimeCullingDispatch_ClampsLogicalInstanceCountToBufferCapacity()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("GetSafeRuntimeInstanceCount"));
            Assert.That(source, Does.Contain("runtimeData == null || runtimeData.instanceLODs == null || runtimeData.instanceLODs.Count == 0"));
            Assert.That(source, Does.Contain("runtimeData.bufferSize < safeCount"));
            Assert.That(source, Does.Contain("runtimeData.transformationMatrixVisibilityBuffer.count < safeCount"));
            Assert.That(source, Does.Contain("runtimeData.instanceLODDataBuffer.count < safeCount"));
            Assert.That(source, Does.Contain("GetComputeThreadGroupCount(safeInstanceCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(runtimeData.instanceCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(runtimeData.bufferSize)"));
            Assert.That(source, Does.Contain("BUFFER_PARAMETER_BUFFER_SIZE, safeInstanceCount"));
        }

        [Test]
        public void RuntimeSubmitPaths_ClampAppendTextureAndArgsCapacity()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("GetSafeVisibilityDispatchCount"));
            Assert.That(source, Does.Contain("int lodShift, int lodAppendIndex, int safeInstanceCount"));
            Assert.That(source, Does.Contain("appendsWithoutCounterReset"));
            Assert.That(source, Does.Contain("appendBuffer.count < safeInstanceCount"));
            Assert.That(source, Does.Contain("appendBuffer.count < dispatchCount"));
            Assert.That(source, Does.Contain("GetMatrixTextureCapacity"));
            Assert.That(source, Does.Contain("GetSafeBufferToTextureDispatchCount"));
            Assert.That(source, Does.Contain("argsBufferIndex >= argsBuffer.count"));
            Assert.That(source, Does.Contain("TEXTURE_CAPACITY, matrixTextureCapacity"));
            Assert.That(source, Does.Contain("ARGS_BUFFER_LENGTH, runtimeData.argsBuffer.count"));
            Assert.That(source, Does.Contain("TryGetArgsInstanceCountByteOffset"));
            Assert.That(source, Does.Contain("TryGetArgsDrawByteOffset"));
        }

        [Test]
        public void GpuInstancerManagerComputeSetup_ResetsLodCapacityAndFailsClosedBeforeFindKernel()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs");
            int matrixAppendIndex = source.IndexOf("case GPUIMatrixHandlingType.MatrixAppend:", System.StringComparison.Ordinal);
            int copyToTextureIndex = source.IndexOf("case GPUIMatrixHandlingType.CopyToTexture:", System.StringComparison.Ordinal);
            int defaultIndex = source.IndexOf("default:", copyToTextureIndex, System.StringComparison.Ordinal);
            int matrixHandlingLocalIndex = source.IndexOf("GPUIMatrixHandlingType matrixHandlingType = GPUInstancerUtility.matrixHandlingType;", System.StringComparison.Ordinal);
            int computeSupportIndex = source.IndexOf("if (SystemInfo.supportsComputeShaders)", System.StringComparison.Ordinal);
            int visibilityNullGuardIndex = source.IndexOf("if (_visibilityComputeShader == null)", System.StringComparison.Ordinal);
            int visibilityFindKernelIndex = source.IndexOf("_visibilityComputeShader.FindKernel", System.StringComparison.Ordinal);
            int cameraNullGuardIndex = source.IndexOf("if (_cameraComputeShader == null || _cameraComputeShaderVR == null)", System.StringComparison.Ordinal);
            int cameraFindKernelIndex = source.IndexOf("_cameraComputeShader.FindKernel", System.StringComparison.Ordinal);
            int argsNullGuardIndex = source.IndexOf("if (_argsBufferComputeShader == null)", System.StringComparison.Ordinal);
            int argsFindKernelIndex = source.IndexOf("_argsBufferComputeShader.FindKernel", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(matrixAppendIndex, 0);
            Assert.Greater(copyToTextureIndex, matrixAppendIndex);
            Assert.Greater(defaultIndex, copyToTextureIndex);
            Assert.Greater(computeSupportIndex, matrixHandlingLocalIndex);
            Assert.That(source, Does.Contain("protected static GPUIMatrixHandlingType _computeShaderMatrixHandlingType = (GPUIMatrixHandlingType)(-1);"));
            Assert.That(source, Does.Contain("GPUInstancerConstants.DETAIL_STORE_INSTANCE_DATA = matrixHandlingType == GPUIMatrixHandlingType.MatrixAppend;"));
            Assert.That(source, Does.Contain("GPUInstancerConstants.COMPUTE_MAX_LOD_BUFFER = matrixHandlingType == GPUIMatrixHandlingType.MatrixAppend ? 2 : 3;"));
            Assert.That(source, Does.Contain("_computeShaderMatrixHandlingType != matrixHandlingType"));
            Assert.That(source, Does.Contain("_computeShaderMatrixHandlingType = matrixHandlingType;"));
            Assert.That(source, Does.Contain("switch (matrixHandlingType)"));
            Assert.That(source, Does.Contain("if (_bufferToTextureComputeShader == null)"));
            Assert.That(source, Does.Contain("!_bufferToTextureComputeShader.HasKernel(GPUInstancerConstants.BUFFER_TO_TEXTURE_KERNEL)"));
            Assert.That(source, Does.Contain("!_bufferToTextureComputeShader.HasKernel(GPUInstancerConstants.BUFFER_TO_TEXTURE_CROSSFADE_KERNEL)"));
            Assert.That(source, Does.Contain("static bool HasAllKernels(ComputeShader shader, string[] kernelNames)"));
            Assert.That(source, Does.Contain("!HasAllKernels(_visibilityComputeShader, GPUInstancerConstants.VISIBILITY_COMPUTE_KERNELS)"));
            Assert.That(source, Does.Contain("!HasAllKernels(_cameraComputeShaderVR, GPUInstancerConstants.CAMERA_COMPUTE_KERNELS)"));
            Assert.That(source, Does.Contain("!_argsBufferComputeShader.HasKernel(GPUInstancerConstants.ARGS_BUFFER_DOUBLE_INSTANCE_COUNT_KERNEL)"));
            Assert.Greater(visibilityFindKernelIndex, visibilityNullGuardIndex);
            Assert.Greater(cameraFindKernelIndex, cameraNullGuardIndex);
            Assert.Greater(argsFindKernelIndex, argsNullGuardIndex);
        }

        [Test]
        public void GpuInstancerStaticComputeSetups_RequireKernelProofBeforeFindKernel()
        {
            string constants = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/DataModel/GPUInstancerConstants.cs");
            string utility = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(constants, Does.Contain("computeBufferSetDataPartial.HasKernel(COMPUTE_SET_DATA_PARTIAL_KERNEL)"));
            Assert.That(constants, Does.Contain("computeBufferSetDataPartial.HasKernel(COMPUTE_SET_DATA_SINGLE_KERNEL)"));
            Assert.That(constants, Does.Contain("computeTextureUtils.HasKernel(COMPUTE_COPY_TEXTURE_KERNEL)"));
            Assert.That(constants, Does.Contain("computeTextureUtils.HasKernel(COMPUTE_REDUCE_TEXTURE_KERNEL)"));
            Assert.That(constants, Does.Contain("computeTextureUtils.HasKernel(COMPUTE_COPY_TEXTURE_ARRAY_KERNEL)"));
            Assert.That(constants, Does.Contain("computeRuntimeModification.HasKernel(COMPUTE_TRANSFORM_OFFSET_KERNEL)"));
            Assert.That(constants, Does.Contain("computeRuntimeModification.HasKernel(COMPUTE_MATRIX_OFFSET_KERNEL)"));
            Assert.That(constants, Does.Contain("computeTextureUtilsReduceTextureId = computeTextureUtils.FindKernel(COMPUTE_REDUCE_TEXTURE_KERNEL);"));
            Assert.That(constants, Does.Contain("computeTextureUtilsCopyTextureArrayId = computeTextureUtils.FindKernel(COMPUTE_COPY_TEXTURE_ARRAY_KERNEL);"));
            Assert.That(utility, Does.Contain("GPUInstancerConstants.computeBufferSetDataSingleKernelId >= 0"));
            Assert.That(utility, Does.Contain("GPUInstancerConstants.computeBufferSetDataPartialKernelId >= 0"));
            Assert.That(utility, Does.Contain("GPUInstancerConstants.computeTextureUtilsCopyTextureArrayId"));
            Assert.That(utility, Does.Contain("GPUInstancerConstants.computeTextureUtilsReduceTextureId"));
            Assert.That(utility, Does.Not.Contain("SetTexture(2,"));
            Assert.That(utility, Does.Not.Contain("Dispatch(2,"));
            Assert.That(utility, Does.Not.Contain("SetTexture(1,"));
            Assert.That(utility, Does.Not.Contain("Dispatch(1,"));
        }

        [Test]
        public void RenderedAmountGpuReadback_IsDebugBuildOnly()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(source, Does.Contain("runtimeData.argsBuffer.GetData(runtimeData.args)"));
            Assert.That(source, Does.Contain("#endif"));
        }

        [Test]
        public void BillboardDilation_FailsClosedAndReleasesTemporaryRenderTexture()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");
            int methodStart = source.IndexOf("public static Texture2D DilateBillboardTexture", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("public static void AddBillboardToRuntimeData", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string method = source.Substring(methodStart, methodEnd - methodStart);

            Assert.That(method, Does.Contain("billboardTexture == null || billboardTexture.width <= 0 || billboardTexture.height <= 0"));
            Assert.That(method, Does.Contain("dilationCompute == null || !dilationCompute.HasKernel(GPUInstancerConstants.COMPUTE_BILLBOARD_DILATION_KERNEL)"));
            Assert.That(method, Does.Contain("RenderTexture previousActive = RenderTexture.active;"));
            Assert.That(method, Does.Contain("try"));
            Assert.That(method, Does.Contain("finally"));
            Assert.That(method, Does.Contain("RenderTexture.active = previousActive;"));
            Assert.That(method, Does.Contain("RenderTexture.ReleaseTemporary(resultTexture);"));
            Assert.That(method, Does.Not.Contain("resultTexture.Release();"));
        }

        [Test]
        public void BillboardAtlasGeneration_RestoresRenderStateAndReleasesTemporaryOnFailure()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");
            int methodStart = source.IndexOf("public static void GeneratePrototypeBillboard", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("public static Texture2D DilateBillboardTexture", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string method = source.Substring(methodStart, methodEnd - methodStart);

            Assert.That(method, Does.Contain("prototype == null || prototype.prefabObject == null"));
            Assert.That(method, Does.Contain("prototype.billboard.atlasResolution <= 0 || prototype.billboard.frameCount <= 0 || prototype.billboard.atlasResolution < prototype.billboard.frameCount"));
            Assert.That(method, Does.Contain("RenderTexture currentRt = RenderTexture.active;"));
            Assert.That(method, Does.Contain("RenderTexture frameTarget = null;"));
            Assert.That(method, Does.Contain("RenderPipelineAsset renderPipelineAsset = null;"));
            Assert.That(method, Does.Contain("RenderPipelineAsset qualityPipelineAsset = null;"));
            Assert.That(method, Does.Contain("bool renderPipelineOverridden = false;"));
            Assert.That(method, Does.Contain("frameTarget = RenderTexture.GetTemporary"));
            Assert.That(method, Does.Contain("renderPipelineOverridden = true;"));
            Assert.That(method, Does.Contain("renderPipelineOverridden = false;"));
            Assert.That(method, Does.Contain("finally"));
            Assert.That(method, Does.Contain("RenderTexture.active = currentRt;"));
            Assert.That(method, Does.Contain("RenderTexture.ReleaseTemporary(frameTarget);"));
            Assert.That(method, Does.Contain("if (renderPipelineOverridden)"));
            Assert.That(method, Does.Contain("GraphicsSettings.defaultRenderPipeline = renderPipelineAsset;"));
            Assert.That(method, Does.Contain("QualitySettings.renderPipeline = qualityPipelineAsset;"));
            Assert.That(method, Does.Contain("QualitySettings.globalTextureMipmapLimit = cachedMasterTextureLimit;"));
            Assert.That(method, Does.Contain("QualitySettings.masterTextureLimit = cachedMasterTextureLimit;"));
            Assert.That(method, Does.Contain("if (sample)"));
            Assert.That(method, Does.Contain("if (billboardCameraPivot)"));
            Assert.That(method, Does.Not.Contain("throw e;"));
            Assert.That(method, Does.Not.Contain("DestroyImmediate(billboardCameraPivot); // this will also release the frameTarget RT"));
        }

        [Test]
        public void GrassInstantiationKernel_GuardsSourceBufferCapacities()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSInstancedRenderingGrassInstantiationKernel.compute");
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs");

            Assert.That(shader, Does.Contain("uniform uint detailMapCapacity;"));
            Assert.That(shader, Does.Contain("uniform uint heightMapCapacity;"));
            Assert.That(shader, Does.Contain("uniform uint hasHealthyDryNoiseTexture;"));
            Assert.That(shader, Does.Contain("detailIndex >= detailMapCapacity"));
            Assert.That(shader, Does.Contain("uint heightDataSize = heightMapCapacity;"));
            Assert.That(shader, Does.Contain("detailResolution == 0 || heightResolution == 0 || terrainSize.x == 0 || terrainSize.y == 0"));
            Assert.That(shader, Does.Contain("float randomScale = randomFloat((grassPosition.x * multiplier) + grassPosition.z);"));
            Assert.That(shader, Does.Contain("if (hasHealthyDryNoiseTexture != 0)"));
            Assert.That(source, Does.Contain("DETAIL_MAP_CAPACITY, detailMapBuffer.count"));
            Assert.That(source, Does.Contain("HEIGHT_MAP_CAPACITY, heightMapBuffer.count"));
            Assert.That(source, Does.Contain("HAS_HEALTHY_DRY_NOISE_TEXTURE, healthyDryNoiseTexture != null ? 1 : 0"));
            Assert.That(source, Does.Contain("!grassInstantiationComputeShader.HasKernel(GPUInstancerConstants.GRASS_INSTANTIATION_KERNEL)"));
        }

        [Test]
        public void ArgsBufferDoubleInstanceCount_GuardsPhysicalArgsBufferLength()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSArgsBuffer.compute");
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs");
            int countGuardIndex = shader.IndexOf("if (id.x >= count)", System.StringComparison.Ordinal);
            int argsIndexIndex = shader.IndexOf("uint argsIndex = id.x * 5 + 1;", System.StringComparison.Ordinal);

            Assert.That(shader, Does.Contain("uniform uint argsBufferLength;"));
            Assert.That(shader, Does.Contain("uint argsIndex = id.x * 5 + 1;"));
            Assert.That(shader, Does.Contain("argsIndex >= argsBufferLength"));
            Assert.That(shader, Does.Not.Contain("argsBuffer[id.x * 5 + 1] *= 2;"));
            Assert.GreaterOrEqual(countGuardIndex, 0);
            Assert.Greater(argsIndexIndex, countGuardIndex);
            Assert.That(source, Does.Contain("_argsBufferComputeShader == null || runtimeData == null || runtimeData.argsBuffer == null || runtimeData.argsBuffer.count <= 0"));
            Assert.That(source, Does.Contain("int safeArgsEntryCount = runtimeData.argsBuffer.count / 5;"));
            Assert.That(source, Does.Contain("if (safeArgsEntryCount <= 0)"));
            Assert.That(source, Does.Contain("GetComputeThreadGroupCount(safeArgsEntryCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(count), 1, 1);"));
            Assert.That(source, Does.Contain("ARGS_BUFFER_LENGTH, runtimeData.argsBuffer.count"));
        }

        [Test]
        public void InterlockedCapacityOverflow_RollsBackCounterBeforeReturn()
        {
            string treeShader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSTreeInstantiationKernel.compute");
            string grassShader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSInstancedRenderingGrassInstantiationKernel.compute");

            Assert.That(treeShader, Does.Contain("if (instanceIndex >= instanceCapacity)"));
            Assert.That(treeShader, Does.Contain("InterlockedAdd(counterBuffer[0], 0xffffffffu, ignoredCounterValue);"));
            Assert.That(grassShader, Does.Contain("if (index >= instanceCapacity)"));
            Assert.That(grassShader, Does.Contain("InterlockedAdd(counterBuffer[0], 0xffffffffu, ignoredCounterValue);"));
        }

        [Test]
        public void DetailInstanceBake_AvoidsSynchronousGpuReadbackInReleaseRoute()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs");
            int helperStart = source.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD\r\n        private static Matrix4x4[] GetInstanceDataForDetailPrototypeWithComputeShader", System.StringComparison.Ordinal);
            if (helperStart < 0)
                helperStart = source.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD\n        private static Matrix4x4[] GetInstanceDataForDetailPrototypeWithComputeShader", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(helperStart, 0);
            int readbackIndex = source.IndexOf("visibilityBuffer.GetData(result)", helperStart, System.StringComparison.Ordinal);
            Assert.Greater(readbackIndex, helperStart);
            int helperEnd = source.IndexOf("#endif", readbackIndex, System.StringComparison.Ordinal);
            Assert.Greater(helperEnd, readbackIndex);
            string helper = source.Substring(helperStart, helperEnd - helperStart);
            int nonPositiveCountGuard = helper.IndexOf("instanceCount <= 0", System.StringComparison.Ordinal);
            int resultAllocation = helper.IndexOf("new Matrix4x4[instanceCount]", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(nonPositiveCountGuard, 0);
            Assert.Greater(resultAllocation, nonPositiveCountGuard);

            int start = source.IndexOf("private static IEnumerator SetInstanceDataForDetailCells", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("callback();", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string method = source.Substring(start, end - start);

            Assert.That(method, Does.Contain("bool useCpuDetailInstanceBake ="));
            Assert.That(method, Does.Contain("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(method, Does.Contain("#else"));
            Assert.That(method, Does.Contain("if (useCpuDetailInstanceBake)"));
            Assert.That(method, Does.Contain("GetInstanceDataForDetailPrototype("));
            Assert.That(method, Does.Contain("GetInstanceDataForDetailPrototypeWithComputeShader("));
            Assert.That(method, Does.Not.Contain("foreach"));
        }

        [Test]
        public void DetailComputeBake_UsesPhysicalDetailMapCapacity()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs");
            int helperStart = source.IndexOf("private static Matrix4x4[] GetInstanceDataForDetailPrototypeWithComputeShader", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(helperStart, 0);
            int helperEnd = source.IndexOf("#endif", helperStart, System.StringComparison.Ordinal);
            Assert.Greater(helperEnd, helperStart);
            string helper = source.Substring(helperStart, helperEnd - helperStart);

            Assert.That(helper, Does.Contain("detailPrototype == null || grassInstantiationComputeShader == null || counterBuffer == null || counterData == null"));
            Assert.That(helper, Does.Contain("new ComputeBuffer(detailMap.Length, GPUInstancerConstants.STRIDE_SIZE_INT)"));
            Assert.That(helper, Does.Not.Contain("new ComputeBuffer(Mathf.CeilToInt(detailMapSize * detailMapSize)"));

            Assert.That(source, Does.Contain("heightMapBuffer.count != cell.heightMapData.Length"));
            Assert.That(source, Does.Contain("detailMapBuffer.count != cell.detailMapData[r].Length"));
            Assert.That(source, Does.Contain("new ComputeBuffer(cell.detailMapData[r].Length, GPUInstancerConstants.STRIDE_SIZE_INT)"));
            Assert.That(source, Does.Not.Contain("new ComputeBuffer(detailMapSize * detailMapSize, GPUInstancerConstants.STRIDE_SIZE_INT)"));
            Assert.That(source, Does.Contain("r < cell.totalDetailCounts.Count"));
            Assert.That(source, Does.Contain("TryGetValue(r, out ComputeBuffer detailInstanceBuffer)"));
            Assert.That(source, Does.Contain("int remainingCount = _generatingVisibilityBuffer.count - startIndex;"));
            Assert.That(source, Does.Contain("CopyComputeBuffer(startIndex, copyCount, detailInstanceBuffer)"));
        }

        [Test]
        public void DetailRuntimeModificationPaths_UseIndexedPrototypeSlotsAndFailClosedDispatch()
        {
            string detailSource = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs");
            string utilitySource = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            int boundsStart = detailSource.IndexOf("public override void RemoveInstancesInsideBounds", System.StringComparison.Ordinal);
            int colliderStart = detailSource.IndexOf("public override void RemoveInstancesInsideCollider", System.StringComparison.Ordinal);
            int offsetStart = detailSource.IndexOf("public override void SetGlobalPositionOffset", System.StringComparison.Ordinal);
            int overrideEnd = detailSource.IndexOf("#endregion Override Methods", offsetStart, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(boundsStart, 0);
            Assert.Greater(colliderStart, boundsStart);
            Assert.Greater(offsetStart, colliderStart);
            Assert.Greater(overrideEnd, offsetStart);

            string boundsMethod = detailSource.Substring(boundsStart, colliderStart - boundsStart);
            string colliderMethod = detailSource.Substring(colliderStart, offsetStart - colliderStart);
            string offsetMethod = detailSource.Substring(offsetStart, overrideEnd - offsetStart);

            Assert.That(boundsMethod, Does.Contain("spData.cellRowAndCollumnCountPerTerrain <= 0"));
            Assert.That(boundsMethod, Does.Contain("detailMapSize <= 0"));
            Assert.That(boundsMethod, Does.Contain("for (int c = 0; c < cellCount; c++)"));
            Assert.That(boundsMethod, Does.Contain("TryGetValue(i, out ComputeBuffer detailBuffer)"));
            Assert.That(boundsMethod, Does.Not.Contain("foreach"));

            Assert.That(colliderMethod, Does.Contain("if (collider == null)"));
            Assert.That(colliderMethod, Does.Contain("terrain == null || terrain.terrainData == null"));
            Assert.That(colliderMethod, Does.Contain("TryGetValue(i, out ComputeBuffer detailBuffer)"));
            Assert.That(colliderMethod, Does.Not.Contain("foreach"));

            Assert.That(offsetMethod, Does.Contain("Vector4 offsetColumn = new Vector4"));
            Assert.That(offsetMethod, Does.Contain("TryGetValue(i, out ComputeBuffer detailBuffer)"));
            Assert.That(offsetMethod, Does.Contain("GPUInstancerConstants.computeRuntimeModification == null"));
            Assert.That(offsetMethod, Does.Not.Contain("foreach"));

            Assert.That(detailSource, Does.Contain("private bool IsDetailPrototypeAllowed"));
            Assert.That(utilitySource, Does.Contain("instanceDataBuffer != null && instanceDataBuffer.count > 0 && GPUInstancerConstants.computeRuntimeModification != null"));
            Assert.That(utilitySource, Does.Contain("instanceDataBuffer != null && instanceDataBuffer.count > 0 && boxCollider != null && GPUInstancerConstants.computeRuntimeModification != null"));
        }

        [Test]
        public void CpuDetailFallback_UsesDeterministicShaderStyleRandomAndDensityGate()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/GPUInstancerDetailManager.cs");
            int start = source.IndexOf("public static Matrix4x4[] GetInstanceDataForDetailPrototype", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string method = source.Substring(start, end - start);

            Assert.That(source, Does.Contain("private static float RandomFloat(float value)"));
            Assert.That(source, Does.Contain("private static Vector2 RandomFloat2(float xValue, float yValue)"));
            Assert.That(method, Does.Contain("instanceCount <= 0"));
            Assert.That(method, Does.Contain("detailMap == null || heightMapData == null"));
            Assert.That(method, Does.Contain("int heightDataSize = heightMapData.Length;"));
            Assert.That(method, Does.Contain("int detailMapCapacity = detailMap.Length;"));
            Assert.That(method, Does.Contain("detailIndex >= detailMapCapacity"));
            Assert.That(method, Does.Contain("float detailDensity = detailPrototype.detailDensity;"));
            Assert.That(method, Does.Contain("float cornerPositionX = (x * sizeDetailXScale) + startPosition.x;"));
            Assert.That(method, Does.Contain("float cornerPositionZ = (y * sizeDetailZScale) + startPosition.z;"));
            Assert.That(method, Does.Contain("RandomFloat(((cornerPositionZ + 0.5f) * multiplier) + cornerPositionX)"));
            Assert.That(method, Does.Contain("RandomFloat2((cornerPositionX + 0.5f) * multiplier, cornerPositionZ + 0.5f)"));
            Assert.That(method, Does.Contain("densityCheck > detailDensity"));
            Assert.That(method, Does.Contain("counter++;"));
            Assert.That(method, Does.Contain("Vector3.Lerp(Vector3.up, terrainPointNormal, detailPrototype.terrainNormalEffect).normalized"));
            Assert.That(method, Does.Not.Contain("new System.Random"));
            Assert.That(method, Does.Not.Contain("randomNumberGenerator.Range"));
        }

        [Test]
        public void ReduceTextureDispatch_ClampsMipDimensionsBeforeDispatch()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSTextureUtils.compute");
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");
            int start = source.IndexOf("public static void ReduceTextureWithComputeShader", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("#endregion Texture Methods", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string method = source.Substring(start, end - start);

            Assert.That(shader, Does.Contain("sourceSizeX == 0 || sourceSizeY == 0 || destinationSizeX == 0 || destinationSizeY == 0"));
            Assert.That(method, Does.Contain("int sourceW = GetTextureMipDimension(source.width, sourceMip);"));
            Assert.That(method, Does.Contain("int sourceH = GetTextureMipDimension(source.height, sourceMip);"));
            Assert.That(method, Does.Contain("int destinationW = GetTextureMipDimension(destination.width, destinationMip);"));
            Assert.That(method, Does.Contain("int destinationH = GetTextureMipDimension(destination.height, destinationMip);"));
            Assert.That(source, Does.Contain("source == null || destination == null || GPUInstancerConstants.computeTextureUtils == null || GPUInstancerConstants.computeTextureUtilsCopyTextureId < 0"));
            Assert.That(source, Does.Contain("GPUInstancerConstants.computeTextureUtilsCopyTextureArrayId < 0"));
            Assert.That(source, Does.Contain("GPUInstancerConstants.computeTextureUtilsReduceTextureId < 0"));
            Assert.That(source, Does.Contain("textureArrayIndex < 0"));
            Assert.That(method, Does.Not.Contain("sourceW >>= 1;"));
            Assert.That(method, Does.Not.Contain("destinationW >>= 1;"));
        }

        [Test]
        public void CrestLegacyClearToBlack_UsesPhysicalTextureDimensionsAndTailGuard()
        {
            string shader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/ClearToBlack.compute");
            string source = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Helpers/TextureArrayHelpers.cs");
            int methodStart = source.IndexOf("public static void ClearToBlack(RenderTexture dst)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("public static Texture2D CreateTexture2D", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string method = source.Substring(methodStart, methodEnd - methodStart);

            Assert.That(shader, Does.Contain("uint _CrestClearWidth;"));
            Assert.That(shader, Does.Contain("uint _CrestClearHeight;"));
            Assert.That(shader, Does.Contain("uint _CrestClearDepth;"));
            Assert.That(shader, Does.Contain("id.x >= _CrestClearWidth || id.y >= _CrestClearHeight || id.z >= _CrestClearDepth"));
            Assert.That(method, Does.Contain("if (dst == null)"));
            Assert.That(method, Does.Contain("int width = dst.width;"));
            Assert.That(method, Does.Contain("int height = dst.height;"));
            Assert.That(method, Does.Contain("int depth = dst.volumeDepth;"));
            Assert.That(method, Does.Contain("krnl_ClearToBlack < 0 || width <= 0 || height <= 0 || depth <= 0"));
            Assert.That(method, Does.Contain("int groupsX = (width + LodDataMgr.THREAD_GROUP_SIZE_X - 1) / LodDataMgr.THREAD_GROUP_SIZE_X;"));
            Assert.That(method, Does.Contain("int groupsY = (height + LodDataMgr.THREAD_GROUP_SIZE_Y - 1) / LodDataMgr.THREAD_GROUP_SIZE_Y;"));
            Assert.That(method, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / LodDataMgr.THREAD_GROUP_SIZE_X"));
            Assert.That(method, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / LodDataMgr.THREAD_GROUP_SIZE_Y"));
            Assert.That(source, Does.Contain("s_clearToBlackShader != null && s_clearToBlackShader.HasKernel(CLEAR_TO_BLACK_SHADER_NAME)"));
            Assert.That(source, Does.Contain("krnl_ClearToBlack = -1;"));
        }

        [Test]
        public void CrestRuntimeDispatches_UsePhysicalDimensionsAndShaderTailGuards()
        {
            string gerstnerSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Shapes/ShapeGerstner.cs");
            string animSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrAnimWaves.cs");
            string persistentSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrPersistent.cs");
            string shapeCombineShader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/ShapeCombine.compute");
            string dynWavesShader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/UpdateDynWaves.compute");
            string foamShader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/UpdateFoam.compute");
            string gerstnerShader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/Gerstner.compute");

            int gerstnerStart = gerstnerSource.IndexOf("void UpdateGenerateWaves(CommandBuffer buf)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gerstnerStart, 0);
            int gerstnerEnd = gerstnerSource.IndexOf("public void UpdateWaveData", gerstnerStart, System.StringComparison.Ordinal);
            Assert.Greater(gerstnerEnd, gerstnerStart);
            string gerstnerMethod = gerstnerSource.Substring(gerstnerStart, gerstnerEnd - gerstnerStart);

            int animStart = animSource.IndexOf("void CombinePassCompute(CommandBuffer buf)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(animStart, 0);
            int animEnd = animSource.IndexOf("public void BindWaveBuffer", animStart, System.StringComparison.Ordinal);
            Assert.Greater(animEnd, animStart);
            string animMethod = animSource.Substring(animStart, animEnd - animStart);

            int persistentStart = persistentSource.IndexOf("buf.DispatchCompute(_shader, krnl_ShaderSim", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(persistentStart, 0);
            int persistentWindowStart = persistentSource.LastIndexOf("_renderSimProperties.SetTexture(sp_LD_TexArray_Target, current);", persistentStart, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(persistentWindowStart, 0);
            int persistentEnd = persistentSource.IndexOf("// Only add forces if we did a step", persistentStart, System.StringComparison.Ordinal);
            Assert.Greater(persistentEnd, persistentWindowStart);
            string persistentDispatchWindow = persistentSource.Substring(persistentWindowStart, persistentEnd - persistentWindowStart);

            Assert.That(gerstnerMethod, Does.Contain("int width = _waveBuffers.width;"));
            Assert.That(gerstnerMethod, Does.Contain("int height = _waveBuffers.height;"));
            Assert.That(gerstnerMethod, Does.Contain("int depth = _waveBuffers.volumeDepth;"));
            Assert.That(gerstnerMethod, Does.Contain("int groupsX = (width + LodDataMgr.THREAD_GROUP_SIZE_X - 1) / LodDataMgr.THREAD_GROUP_SIZE_X;"));
            Assert.That(gerstnerMethod, Does.Contain("int groupsY = (height + LodDataMgr.THREAD_GROUP_SIZE_Y - 1) / LodDataMgr.THREAD_GROUP_SIZE_Y;"));
            Assert.That(gerstnerMethod, Does.Not.Contain("_waveBuffers.width / LodDataMgr.THREAD_GROUP_SIZE_X"));
            Assert.That(gerstnerMethod, Does.Not.Contain("_waveBuffers.height / LodDataMgr.THREAD_GROUP_SIZE_Y"));

            Assert.That(animMethod, Does.Contain("var dataTexture = DataTexture;"));
            Assert.That(animMethod, Does.Contain("int width = dataTexture.width;"));
            Assert.That(animMethod, Does.Contain("int height = dataTexture.height;"));
            Assert.That(animMethod, Does.Contain("int groupsX = (width + THREAD_GROUP_SIZE_X - 1) / THREAD_GROUP_SIZE_X;"));
            Assert.That(animMethod, Does.Contain("int groupsY = (height + THREAD_GROUP_SIZE_Y - 1) / THREAD_GROUP_SIZE_Y;"));
            Assert.That(animMethod, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / THREAD_GROUP_SIZE_X"));
            Assert.That(animMethod, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / THREAD_GROUP_SIZE_Y"));

            Assert.That(persistentDispatchWindow, Does.Contain("int width = current != null ? current.width : 0;"));
            Assert.That(persistentDispatchWindow, Does.Contain("int height = current != null ? current.height : 0;"));
            Assert.That(persistentDispatchWindow, Does.Contain("int depth = current != null ? current.volumeDepth : 0;"));
            Assert.That(persistentDispatchWindow, Does.Contain("int lodDispatchCount = OceanRenderer.Instance.CurrentLodCount;"));
            Assert.That(persistentDispatchWindow, Does.Contain("int groupsX = (width + THREAD_GROUP_SIZE_X - 1) / THREAD_GROUP_SIZE_X;"));
            Assert.That(persistentDispatchWindow, Does.Contain("int groupsY = (height + THREAD_GROUP_SIZE_Y - 1) / THREAD_GROUP_SIZE_Y;"));
            Assert.That(persistentDispatchWindow, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / THREAD_GROUP_SIZE_X"));
            Assert.That(persistentDispatchWindow, Does.Not.Contain("OceanRenderer.Instance.LodDataResolution / THREAD_GROUP_SIZE_Y"));

            Assert.That(shapeCombineShader, Does.Contain("id.x >= width || id.y >= height || _LD_SliceIndex >= depth"));
            Assert.That(shapeCombineShader, Does.Contain("const uint2 pixelCoordMax = uint2((uint)i_width - 1u, (uint)i_height - 1u);"));
            Assert.That(shapeCombineShader, Does.Contain("const uint2 pixelCoordCentersTopRight = min(pixelCoordCentersBotLeft + uint2(1u, 1u), pixelCoordMax);"));
            Assert.That(shapeCombineShader, Does.Contain("const uint2 pixelCoordBotRight = uint2(pixelCoordCentersTopRight.x, pixelCoordCentersBotLeft.y);"));
            Assert.That(shapeCombineShader, Does.Contain("const uint2 pixelCoordTopLeft = uint2(pixelCoordCentersBotLeft.x, pixelCoordCentersTopRight.y);"));
            Assert.That(dynWavesShader, Does.Contain("id.x >= width || id.y >= height || id.z >= depth"));
            Assert.That(dynWavesShader, Does.Contain("const bool insideTarget = sliceIndexSource < depthFloat && sliceIndexSource >= 0.0;"));
            Assert.That(foamShader, Does.Contain("id.x >= width || id.y >= height || id.z >= depth"));
            Assert.That(foamShader, Does.Contain("const float sliceIndexSource = clamp(id.z + _LODChange, 0.0, depthFloat - 1.0);"));
            Assert.That(gerstnerShader, Does.Contain("id.x >= width || id.y >= height || cascadeIndex >= depth || _TextureRes <= 0.0"));
        }

        [Test]
        public void CrestComputeKernelResolution_FailsClosedBeforeFindKernelAndDispatch()
        {
            string gerstnerSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Shapes/ShapeGerstner.cs");
            string animSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrAnimWaves.cs");
            string persistentSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrPersistent.cs");
            string dynWavesSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrDynWaves.cs");
            string foamSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/LodData/LodDataMgrFoam.cs");
            string underwaterMaskSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Underwater/UnderwaterRenderer.Mask.cs");
            string queryBaseSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Collision/QueryBase.cs");

            int persistentHasKernelIndex = persistentSource.IndexOf("!_shader.HasKernel(ShaderSim)", System.StringComparison.Ordinal);
            int persistentFindKernelIndex = persistentSource.IndexOf("_krnlShaderSim = _shader.FindKernel(ShaderSim);", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(persistentHasKernelIndex, 0);
            Assert.Greater(persistentFindKernelIndex, persistentHasKernelIndex);
            Assert.That(persistentSource, Does.Contain("protected int _krnlShaderSim = -1;"));
            Assert.That(persistentSource, Does.Contain("_renderSimProperties = null;"));
            Assert.That(persistentSource, Does.Contain("if (_shader == null || _krnlShaderSim < 0)"));
            Assert.That(dynWavesSource, Does.Contain("protected override int krnl_ShaderSim => _krnlShaderSim;"));
            Assert.That(foamSource, Does.Contain("protected override int krnl_ShaderSim => _krnlShaderSim;"));
            Assert.That(dynWavesSource, Does.Not.Contain("_shader.FindKernel(ShaderSim)"));
            Assert.That(foamSource, Does.Not.Contain("_shader.FindKernel(ShaderSim)"));

            int gerstnerHasKernelIndex = gerstnerSource.IndexOf("!_shaderGerstner.HasKernel(\"Gerstner\")", System.StringComparison.Ordinal);
            int gerstnerFindKernelIndex = gerstnerSource.IndexOf("_krnlGerstner = _shaderGerstner.FindKernel(\"Gerstner\");", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gerstnerHasKernelIndex, 0);
            Assert.Greater(gerstnerFindKernelIndex, gerstnerHasKernelIndex);
            Assert.That(gerstnerSource, Does.Contain("if (_shaderGerstner == null || _krnlGerstner < 0)"));
            Assert.That(gerstnerSource, Does.Contain("if (_shaderGerstner == null || _krnlGerstner < 0 || _waveBuffers == null)"));

            Assert.That(animSource, Does.Contain("static bool TryFindCombineKernel(ComputeShader shader, string kernelName, out int kernel)"));
            Assert.That(animSource, Does.Contain("shader == null || !shader.HasKernel(kernelName)"));
            Assert.That(animSource, Does.Contain("kernel = shader.FindKernel(kernelName);"));
            Assert.That(animSource, Does.Contain("else if (_combineShader == null || _combineProperties == null)"));
            Assert.That(animSource, Does.Contain("_waveBuffers?.Release();"));
            Assert.That(animSource, Does.Contain("_combineBuffer?.Release();"));
            Assert.That(animSource, Does.Contain("if (_combineMaterial == null)"));

            int maskHasKernelIndex = underwaterMaskSource.IndexOf("!_fixMaskComputeShader.HasKernel(k_ComputeShaderKernelFillMaskArtefacts)", System.StringComparison.Ordinal);
            int maskFindKernelIndex = underwaterMaskSource.IndexOf("_fixMaskKernel = _fixMaskComputeShader.FindKernel(k_ComputeShaderKernelFillMaskArtefacts);", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(maskHasKernelIndex, 0);
            Assert.Greater(maskFindKernelIndex, maskHasKernelIndex);
            Assert.That(underwaterMaskSource, Does.Contain("_fixMaskThreadGroupSizeX == 0 || _fixMaskThreadGroupSizeY == 0"));
            Assert.That(underwaterMaskSource, Does.Contain("int groupsX = (int)(((long)descriptor.width + _fixMaskThreadGroupSizeX - 1L) / _fixMaskThreadGroupSizeX);"));
            Assert.That(underwaterMaskSource, Does.Contain("int groupsY = (int)(((long)descriptor.height + _fixMaskThreadGroupSizeY - 1L) / _fixMaskThreadGroupSizeY);"));
            Assert.That(underwaterMaskSource, Does.Not.Contain("Mathf.CeilToInt((float)descriptor.width / _fixMaskThreadGroupSizeX)"));
            Assert.That(underwaterMaskSource, Does.Not.Contain("Mathf.CeilToInt((float)descriptor.height / _fixMaskThreadGroupSizeY)"));

            int queryHasKernelIndex = queryBaseSource.IndexOf("!_shaderProcessQueries.HasKernel(QueryKernelName)", System.StringComparison.Ordinal);
            int queryFindKernelIndex = queryBaseSource.IndexOf("_kernelHandle = _shaderProcessQueries.FindKernel(QueryKernelName);", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(queryHasKernelIndex, 0);
            Assert.Greater(queryFindKernelIndex, queryHasKernelIndex);
            Assert.That(queryBaseSource, Does.Contain("protected int _kernelHandle = -1;"));
            Assert.That(queryBaseSource, Does.Contain("_shaderProcessQueries = null;"));
            Assert.That(queryBaseSource, Does.Contain("if (_shaderProcessQueries == null || _kernelHandle < 0 || _wrapper == null || _computeBufQueries == null || _computeBufResults == null)"));
        }

        [Test]
        public void CrestFftBaker_UsesCeilDispatchAndShaderTailGuard()
        {
            string source = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Shapes/FFT/FFTBaker.cs");
            string shader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/FFT/FFTBake.compute");
            int methodStart = source.IndexOf("static FFTBakedData BakeFFT", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("private static bool SaveBakedDataAsset", methodStart, System.StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string method = source.Substring(methodStart, methodEnd - methodStart);

            Assert.That(method, Does.Contain("var frameCount = (int)(resolutionTime * loopPeriod);"));
            Assert.That(method, Does.Contain("fftWaves == null || fftWaves._resolution <= 0 || lodCount <= 0 || frameCount <= 0"));
            Assert.That(method, Does.Contain("waveCombineShader == null || !waveCombineShader.HasKernel(\"FFTBakeMultiRes\")"));
            Assert.That(method, Does.Contain("var groupsX = (bakedWaves.width + 7) / 8;"));
            Assert.That(method, Does.Contain("var groupsY = (bakedWaves.height + 7) / 8;"));
            Assert.That(method, Does.Contain("buf.DispatchCompute(waveCombineShader, kernel, groupsX, groupsY, 1);"));
            Assert.That(method, Does.Contain("finally"));
            Assert.That(method, Does.Contain("buf.Release();"));
            Assert.That(method, Does.Contain("bakedWaves.Release();"));
            Assert.That(method, Does.Contain("Helpers.Destroy(stagingTexture);"));
            Assert.That(method, Does.Not.Contain("bakedWaves.width / 8"));
            Assert.That(method, Does.Not.Contain("bakedWaves.height / 8"));

            Assert.That(shader, Does.Contain("uint fftWidth;"));
            Assert.That(shader, Does.Contain("uint fftHeight;"));
            Assert.That(shader, Does.Contain("uint fftDepth;"));
            Assert.That(shader, Does.Contain("id.x >= outWidth || id.y >= outHeight || id.x >= fftWidth"));
            Assert.That(shader, Does.Contain("_MinSlice < 0"));
            Assert.That(shader, Does.Contain("slice >= fftDepth"));
            Assert.That(shader, Does.Contain("id.y % fftHeight"));
        }

        [Test]
        public void CrestFftSpectrum_UsesCeilDispatchAndShaderTailGuard()
        {
            string source = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs");
            string shapeFftSource = System.IO.File.ReadAllText("Assets/Crest/Crest/Scripts/Shapes/FFT/ShapeFFT.cs");
            string shader = System.IO.File.ReadAllText("Assets/Crest/Crest/Shaders/Resources/FFT/FFTSpectrum.compute");
            int initStart = source.IndexOf("void InitializeSpectrum(CommandBuffer buf)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(initStart, 0);
            int updateStart = source.IndexOf("void UpdateSpectrum(CommandBuffer buf, float time)", initStart, System.StringComparison.Ordinal);
            Assert.Greater(updateStart, initStart);
            int fftStart = source.IndexOf("void DispatchFFT(CommandBuffer buf)", updateStart, System.StringComparison.Ordinal);
            Assert.Greater(fftStart, updateStart);
            string initMethod = source.Substring(initStart, updateStart - initStart);
            string updateMethod = source.Substring(updateStart, fftStart - updateStart);

            Assert.That(initMethod, Does.Contain("if (_resolution <= 0)"));
            Assert.That(initMethod, Does.Contain("int groups = (int)(((long)_resolution + 7L) / 8L);"));
            Assert.That(initMethod, Does.Contain("buf.DispatchCompute(_shaderSpectrum, _kernelSpectrumInit, groups, groups, CASCADE_COUNT);"));
            Assert.That(initMethod, Does.Not.Contain("_resolution / 8"));
            Assert.That(updateMethod, Does.Contain("if (_resolution <= 0)"));
            Assert.That(updateMethod, Does.Contain("int groups = (int)(((long)_resolution + 7L) / 8L);"));
            Assert.That(updateMethod, Does.Contain("buf.DispatchCompute(_shaderSpectrum, _kernelSpectrumUpdate, groups, groups, CASCADE_COUNT);"));
            Assert.That(updateMethod, Does.Not.Contain("_resolution / 8"));

            Assert.That(source, Does.Contain("public const int MIN_SUPPORTED_RESOLUTION = 16;"));
            Assert.That(source, Does.Contain("public const int MAX_SUPPORTED_RESOLUTION = 512;"));
            Assert.That(source, Does.Contain("static bool IsSupportedResolution(int resolution)"));
            Assert.That(source, Does.Contain("return Mathf.Clamp(powerOfTwoResolution, MIN_SUPPORTED_RESOLUTION, MAX_SUPPORTED_RESOLUTION);"));
            Assert.That(source, Does.Contain("resolution = ClampSupportedResolution(resolution);"));
            Assert.That(source, Does.Contain("_shaderSpectrum == null || _shaderFFT == null ||"));
            Assert.That(source, Does.Contain("!_shaderSpectrum.HasKernel(\"SpectrumInitalize\")"));
            Assert.That(source, Does.Contain("!_shaderSpectrum.HasKernel(\"SpectrumUpdate\")"));
            Assert.That(source, Does.Contain("if (!_isInitialised)"));
            Assert.That(source, Does.Contain("if (!IsSupportedResolution(_resolution))"));
            Assert.That(shapeFftSource, Does.Contain("protected override int MaximumResolution => FFTCompute.MAX_SUPPORTED_RESOLUTION;"));
            Assert.AreEqual(16, Crest.FFTCompute.ClampSupportedResolution(0));
            Assert.AreEqual(16, Crest.FFTCompute.ClampSupportedResolution(16));
            Assert.AreEqual(512, Crest.FFTCompute.ClampSupportedResolution(512));
            Assert.AreEqual(512, Crest.FFTCompute.ClampSupportedResolution(1024));

            Assert.That(shader, Does.Contain("_ResultInit.GetDimensions( width, height, depth );"));
            Assert.That(shader, Does.Contain("id.x >= width || id.y >= height || id.z >= depth || _Size <= 0"));
            Assert.That(shader, Does.Contain("const int2 coord = (int2)id.xy - center;"));
            Assert.That(shader, Does.Contain("id.z < (depth - 1u)"));
            Assert.That(shader, Does.Contain("(maxCoord < WAVE_SAMPLE_FACTOR / 2u ||"));
            Assert.That(shader, Does.Contain("maxCoord >= WAVE_SAMPLE_FACTOR)"));
            Assert.That(shader, Does.Contain("_Init0.GetDimensions( initWidth, initHeight, initDepth );"));
            Assert.That(shader, Does.Contain("_ResultHeight.GetDimensions( heightWidth, heightHeight, heightDepth );"));
            Assert.That(shader, Does.Contain("_ResultDisplaceX.GetDimensions( displaceXWidth, displaceXHeight, displaceXDepth );"));
            Assert.That(shader, Does.Contain("_ResultDisplaceZ.GetDimensions( displaceZWidth, displaceZHeight, displaceZDepth );"));
            Assert.That(shader, Does.Contain("id.x >= initWidth || id.y >= initHeight || id.z >= initDepth"));
        }

        [Test]
        public void Crest512FftTwoElementsPerThread_MatchesSingleElementReference()
        {
            const int size = 512;
            const int passes = 9;
            ComplexSample[] source = CreateDeterministicSpectrum(size);
            ComplexSample[,] butterflies = CreateDeterministicButterflies(size, passes);
            ComplexSample[] reference = RunSingleElementPerThreadFft(source, butterflies, size, passes);
            ComplexSample[] reduced = RunTwoElementsPerThreadFft(source, butterflies, size, passes);

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(reference[i].R, reduced[i].R, 0.0000000001d, "real mismatch at " + i);
                Assert.AreEqual(reference[i].I, reduced[i].I, 0.0000000001d, "imag mismatch at " + i);
            }
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            return CeilDividePositive(value, (long)(divisor > 0 ? divisor : 1));
        }

        private static int CeilDividePositive(int value, long divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long dispatchGroups = ((long)value + divisor - 1L) / divisor;
            return dispatchGroups > int.MaxValue ? int.MaxValue : (int)dispatchGroups;
        }

        private static ComplexSample[] RunSingleElementPerThreadFft(ComplexSample[] input, ComplexSample[,] butterflies, int size, int passes)
        {
            ComplexSample[] intermediates = Copy(input);
            ComplexSample[] scratch = new ComplexSample[size];

            for (int passIndex = 0; passIndex < passes; passIndex++)
            {
                for (int coord = 0; coord < size; coord++)
                {
                    ButterflyPass(intermediates, scratch, butterflies[passIndex, coord], coord, passIndex, passes);
                }
            }

            return (passes % 2) == 0 ? intermediates : scratch;
        }

        private static ComplexSample[] RunTwoElementsPerThreadFft(ComplexSample[] input, ComplexSample[,] butterflies, int size, int passes)
        {
            const int threadCount = 256;
            ComplexSample[] intermediates = Copy(input);
            ComplexSample[] scratch = new ComplexSample[size];

            for (int passIndex = 0; passIndex < passes; passIndex++)
            {
                for (int threadId = 0; threadId < threadCount; threadId++)
                {
                    int coord = threadId;
                    int coord2 = threadId + threadCount;

                    ButterflyPass(intermediates, scratch, butterflies[passIndex, coord], coord, passIndex, passes);
                    ButterflyPass(intermediates, scratch, butterflies[passIndex, coord2], coord2, passIndex, passes);
                }
            }

            return (passes % 2) == 0 ? intermediates : scratch;
        }

        private static void ButterflyPass(ComplexSample[] intermediates, ComplexSample[] scratch, ComplexSample butterfly, int coord, int passIndex, int passes)
        {
            int offset = 1 << passIndex;
            int indexA;
            int indexB;

            if ((coord / offset) % 2 == 1)
            {
                indexA = coord - offset;
                indexB = coord;
            }
            else
            {
                indexA = coord;
                indexB = coord + offset;
            }

            if (passIndex == 0)
            {
                indexA = ReverseBits(indexA) >> (32 - passes);
                indexB = ReverseBits(indexB) >> (32 - passes);
            }

            bool pingpong = (passIndex % 2) == 0;
            ComplexSample valueA = pingpong ? intermediates[indexA] : scratch[indexA];
            ComplexSample valueB = pingpong ? intermediates[indexB] : scratch[indexB];
            ComplexSample weightedValue = ComplexMultiply(butterfly, valueB);
            ComplexSample result = new ComplexSample(valueA.R + weightedValue.R, valueA.I + weightedValue.I);

            if (pingpong)
                scratch[coord] = result;
            else
                intermediates[coord] = result;
        }

        private static ComplexSample[] CreateDeterministicSpectrum(int size)
        {
            ComplexSample[] result = new ComplexSample[size];
            for (int i = 0; i < size; i++)
            {
                double r = ((i * 37) & 255) / 255.0d;
                double im = ((i * 91 + 17) & 255) / 255.0d;
                result[i] = new ComplexSample(r, im);
            }

            return result;
        }

        private static ComplexSample[,] CreateDeterministicButterflies(int size, int passes)
        {
            ComplexSample[,] result = new ComplexSample[passes, size];
            for (int passIndex = 0; passIndex < passes; passIndex++)
            {
                for (int coord = 0; coord < size; coord++)
                {
                    double angle = ((coord + 1) * (passIndex + 1)) * 0.01227184630308513d;
                    result[passIndex, coord] = new ComplexSample(System.Math.Cos(angle), -System.Math.Sin(angle));
                }
            }

            return result;
        }

        private static ComplexSample ComplexMultiply(ComplexSample a, ComplexSample b)
        {
            return new ComplexSample(a.R * b.R - a.I * b.I, a.R * b.I + a.I * b.R);
        }

        private static ComplexSample[] Copy(ComplexSample[] source)
        {
            ComplexSample[] result = new ComplexSample[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static int ReverseBits(int value)
        {
            uint x = (uint)value;
            x = ((x >> 1) & 0x55555555u) | ((x & 0x55555555u) << 1);
            x = ((x >> 2) & 0x33333333u) | ((x & 0x33333333u) << 2);
            x = ((x >> 4) & 0x0f0f0f0fu) | ((x & 0x0f0f0f0fu) << 4);
            x = ((x >> 8) & 0x00ff00ffu) | ((x & 0x00ff00ffu) << 8);
            x = ((x >> 16) & 0xffffu) | ((x & 0xffffu) << 16);
            return (int)x;
        }

        private readonly struct ComplexSample
        {
            public readonly double R;
            public readonly double I;

            public ComplexSample(double r, double i)
            {
                R = r;
                I = i;
            }
        }
    }
}
