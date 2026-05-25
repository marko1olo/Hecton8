using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    public static class HadalTrenchMockBenchmark
    {
        private const string ReportPath = "Docs/Reports/TRENCH_MOCK_BENCHMARK_SHINOBU_241.json";

        [MenuItem("HECTON-8/Hadal Trench Forge/Run 256 Mock Benchmark")]
        public static void RunMenu()
        {
            Run(HadalTrenchBakeConstants.DefaultVoxelResolution);
        }

        public static void Run(int resolution)
        {
            HadalTrenchBakeConfigDTO config = HadalTrenchBakePipeline.DefaultConfig();
            int safeResolution = math.clamp(resolution, 32, HadalTrenchBakeConstants.DefaultVoxelResolution);
            config.Resolution = new int3(safeResolution);
            int voxelCount = safeResolution * safeResolution * safeResolution;
            NativeArray<float> densities = new NativeArray<float>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                JobHandle handle = new GenerateMockTrenchJob
                {
                    Densities = densities,
                    Config = config
                }.Schedule(voxelCount, 64);
                handle.Complete();
                stopwatch.Stop();
                WriteReport(voxelCount, safeResolution, stopwatch.Elapsed.TotalMilliseconds);
            }
            finally
            {
                if (densities.IsCreated)
                    densities.Dispose();
            }
        }

        private static void WriteReport(int voxelCount, int resolution, double milliseconds)
        {
            Directory.CreateDirectory("Docs/Reports");
            StringBuilder builder = new StringBuilder(512);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_241\",\n");
            builder.Append("  \"benchmark\": \"GenerateMockTrenchJob\",\n");
            builder.Append("  \"allocator\": \"TempJob\",\n");
            builder.Append("  \"nativeArrayOptions\": \"UninitializedMemory\",\n");
            builder.Append("  \"resolution\": ").Append(resolution).Append(",\n");
            builder.Append("  \"voxelCount\": ").Append(voxelCount).Append(",\n");
            builder.Append("  \"elapsedMs\": ").Append(milliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"runtimeGameplayCostUs\": 0\n");
            builder.Append("}\n");
            File.WriteAllText(ReportPath, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }
    }
}
