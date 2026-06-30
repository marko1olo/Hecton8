using NUnit.Framework;
using Unity.Collections;
using System.IO;
using Unity.Mathematics;
using Hecton8.Animation.IK;

namespace Hecton8.Tests.Animation.IK
{
    public sealed class LeviathanTerrainIkBlackBoxTests
    {
        [Test]
        public void TryDumpTelemetry_ReturnsFalseWhenPathIsInvalid_AndCatchesExceptionGracefully()
        {
            // invalid path with a null character
            string badPath = "invalid\0_path.bin";

            int capacity = LeviathanTerrainIkConstants.TelemetryCapacity;
            NativeArray<LeviathanTerrainIkTelemetryEntry> ring = new NativeArray<LeviathanTerrainIkTelemetryEntry>(capacity, Allocator.Temp);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.Temp);

            // set valid initial state
            cursor[0] = 0;

            try
            {
                bool result = LeviathanTerrainIkBlackBox.TryDumpTelemetry(badPath, ring, cursor);
                Assert.IsFalse(result);
            }
            finally
            {
                ring.Dispose();
                cursor.Dispose();
            }
        }

        [Test]
        public void TryDumpTelemetryOnFault_ReturnsFalseWhenPathIsInvalid_AndCatchesExceptionGracefully()
        {
            // invalid path with a null character
            string badPath = "invalid\0_path.bin";

            int capacity = LeviathanTerrainIkConstants.TelemetryCapacity;
            NativeArray<LeviathanTerrainIkTelemetryEntry> ring = new NativeArray<LeviathanTerrainIkTelemetryEntry>(capacity, Allocator.Temp);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.Temp);

            // set valid initial state
            cursor[0] = 0;

            try
            {
                bool result = LeviathanTerrainIkBlackBox.TryDumpTelemetryOnFault(badPath, ring, cursor);
                Assert.IsFalse(result);
            }
            finally
            {
                ring.Dispose();
                cursor.Dispose();
            }
        }
    }
}
