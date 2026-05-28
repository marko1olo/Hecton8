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
        public void BufferToTextureKernel_UsesCapacityAndIntegerIndexGuards()
        {
            string shader = System.IO.File.ReadAllText("Assets/GPUInstancer/Resources/Compute/CSInstancedBufferToTexture.compute");

            Assert.That(shader, Does.Contain("id.x >= argsBuffer[argsBufferIndex] || id.x >= bufferSize || maxTextureSize == 0"));
            Assert.That(shader, Does.Contain("uint indexY = id.x / maxTextureSize;"));
            Assert.That(shader, Does.Contain("instanceId >= bufferSize"));
            Assert.That(shader, Does.Contain("id.x >= bufferSize || maxTextureSize == 0"));
            Assert.That(shader, Does.Not.Contain("floor(id.x / float(maxTextureSize))"));
        }

        [Test]
        public void RuntimeCullingDispatch_ClampsLogicalInstanceCountToBufferCapacity()
        {
            string source = System.IO.File.ReadAllText("Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs");

            Assert.That(source, Does.Contain("GetSafeRuntimeInstanceCount"));
            Assert.That(source, Does.Contain("runtimeData == null || runtimeData.transformationMatrixVisibilityBuffer == null || runtimeData.instanceLODDataBuffer == null"));
            Assert.That(source, Does.Contain("runtimeData.bufferSize < safeCount"));
            Assert.That(source, Does.Contain("runtimeData.transformationMatrixVisibilityBuffer.count < safeCount"));
            Assert.That(source, Does.Contain("runtimeData.instanceLODDataBuffer.count < safeCount"));
            Assert.That(source, Does.Contain("GetComputeThreadGroupCount(safeInstanceCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(runtimeData.instanceCount)"));
            Assert.That(source, Does.Not.Contain("GetComputeThreadGroupCount(runtimeData.bufferSize)"));
            Assert.That(source, Does.Contain("BUFFER_PARAMETER_BUFFER_SIZE, safeInstanceCount"));
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
