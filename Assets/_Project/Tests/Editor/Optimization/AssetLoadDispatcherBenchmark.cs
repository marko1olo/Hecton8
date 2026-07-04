using NUnit.Framework;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class AssetLoadDispatcherBenchmark
    {
        private const long BytesPerMegabyte = 1024L * 1024L;

        [Test]
        public void Benchmark_VRAMDivision()
        {
            long[] testValues = new long[10000];
            for (int i = 0; i < testValues.Length; i++)
            {
                testValues[i] = UnityEngine.Random.Range(100L * BytesPerMegabyte, 8000L * BytesPerMegabyte);
            }

            var sw = new Stopwatch();

            // Baseline: Division
            sw.Start();
            float result1 = 0;
            for (int j = 0; j < 1000; j++)
            {
                for (int i = 0; i < testValues.Length; i++)
                {
                    result1 += testValues[i] / (float)BytesPerMegabyte;
                }
            }
            sw.Stop();
            long divisionMs = sw.ElapsedMilliseconds;

            // Optimization: Multiplication
            sw.Reset();
            sw.Start();
            float result2 = 0;
            const float InvBytesPerMegabyte = 1f / BytesPerMegabyte;
            for (int j = 0; j < 1000; j++)
            {
                for (int i = 0; i < testValues.Length; i++)
                {
                    result2 += testValues[i] * InvBytesPerMegabyte;
                }
            }
            sw.Stop();
            long multiplicationMs = sw.ElapsedMilliseconds;

            UnityEngine.Debug.Log($"Division Time: {divisionMs} ms");
            UnityEngine.Debug.Log($"Multiplication Time: {multiplicationMs} ms");

            // Assert they are close enough
            Assert.AreEqual(result1, result2, 1f);
        }
    }
}
