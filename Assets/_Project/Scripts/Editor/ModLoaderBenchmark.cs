using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Hecton8.Modding;

namespace Hecton8.Editor
{
    public static class ModLoaderBenchmark
    {
        [MenuItem("Hecton8/Verification/Benchmark ModLoader")]
        public static async void RunBenchmark()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string modsDir = Path.Combine(projectRoot, "Mods");
            string benchmarkDir = Path.Combine(modsDir, "BenchmarkMods");

            if (Directory.Exists(benchmarkDir))
                Directory.Delete(benchmarkDir, true);
            Directory.CreateDirectory(benchmarkDir);

            // Generate 100 fake mods
            for (int i = 0; i < 100; i++)
            {
                string modDir = Path.Combine(benchmarkDir, $"BenchMod_{i}");
                Directory.CreateDirectory(modDir);
                string manifestPath = Path.Combine(modDir, "mod.json");
                File.WriteAllText(manifestPath, $@"{{
                    ""Id"": ""bench.mod.{i}"",
                    ""DisplayName"": ""Benchmark Mod {i}"",
                    ""Version"": ""1.0.0"",
                    ""Author"": ""AutomatedTest"",
                    ""RequiredAPIVersion"": 1,
                    ""ModPriority"": 0
                }}");
            }

            UnityEngine.Debug.Log($"[Benchmark] Created 100 mods. Running benchmark...");

            var type = typeof(ModLoader);
            var initMethod = type.GetMethod("DiscoverAndLoadMods", BindingFlags.NonPublic | BindingFlags.Static);

            var stopwatch = new Stopwatch();

            // Warmup
            ResetAndBootstrap(type);
            if (initMethod.Invoke(null, null) is Awaitable warmupTask)
                await warmupTask;

            // Benchmark
            stopwatch.Start();
            for (int i = 0; i < 10; i++)
            {
                ResetAndBootstrap(type);
                if (initMethod.Invoke(null, null) is Awaitable benchTask)
                    await benchTask;
            }
            stopwatch.Stop();

            UnityEngine.Debug.Log($"[Benchmark] DiscoverAndLoadMods took {stopwatch.ElapsedMilliseconds} ms for 10 iterations (1000 mod loads total).");

            Cleanup(benchmarkDir);
        }

        private static void Cleanup(string testModDir)
        {
            if (Directory.Exists(testModDir))
                Directory.Delete(testModDir, true);
        }

        private static void ResetAndBootstrap(Type type)
        {
            var runtimeInfosField = type.GetField("_runtimeInfos", BindingFlags.NonPublic | BindingFlags.Static);
            if (runtimeInfosField != null)
            {
                var list = runtimeInfosField.GetValue(null) as System.Collections.IList;
                if (list != null) list.Clear();
            }

            var indexField = type.GetField("_runtimeInfoIndexByHash", BindingFlags.NonPublic | BindingFlags.Static);
            if (indexField != null)
            {
                var dict = indexField.GetValue(null) as System.Collections.IDictionary;
                if (dict != null) dict.Clear();
            }
        }
    }
}
