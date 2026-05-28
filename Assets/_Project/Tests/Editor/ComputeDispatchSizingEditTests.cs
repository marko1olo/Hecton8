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
        public void RenderedAmountGpuReadback_IsDebugBuildOnly()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
            Assert.That(source, Does.Contain("runtimeData.argsBuffer.GetData(runtimeData.args)"));
            Assert.That(source, Does.Contain("#endif"));
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
            Assert.That(source, Does.Contain("if (count <= 0)"));
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
            Assert.That(source, Does.Contain("source == null || destination == null || GPUInstancerConstants.computeTextureUtils == null"));
            Assert.That(source, Does.Contain("textureArrayIndex < 0"));
            Assert.That(method, Does.Not.Contain("sourceW >>= 1;"));
            Assert.That(method, Does.Not.Contain("destinationW >>= 1;"));
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
