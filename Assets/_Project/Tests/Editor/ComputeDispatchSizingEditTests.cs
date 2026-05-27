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

        private static int CeilDividePositive(int value, int divisor)
        {
            if (value <= 0)
                return 0;

            int safeDivisor = divisor > 0 ? divisor : 1;
            return (int)(((long)value + safeDivisor - 1L) / safeDivisor);
        }
    }
}
