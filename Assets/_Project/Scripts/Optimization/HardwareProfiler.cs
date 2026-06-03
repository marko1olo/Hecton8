using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Cold BIOS hardware benchmark. Runs inside an isolated local physics scene only.
    /// </summary>
    public static class HardwareProfiler
    {
        public const int BenchmarkCapsuleCount = 1000;
        public const int BenchmarkStepCount = 10;
        public const double MaxBenchmarkWallMilliseconds = 500.0d;
        public const int SurvivalGraphicsMemoryMegabytes = 3000;
        public const int SurvivalProcessorCount = 6;

        private const double FullPhysicsQualityMillisecondsPerStep = 2.0d;
        private const double SurvivalPhysicsQualityMillisecondsPerStep = 8.0d;
        private const float FixedStepSeconds = 0.02f;
        private const int GridColumns = 40;
        private const float GridSpacingMeters = 1.75f;
        private const double InvBenchmarkStepCount = 1.0d / BenchmarkStepCount;
        private const string SceneName = "HECTON8_BIOS_HARDWARE_PROFILER";

        /// <summary>
        /// Immutable BIOS hardware snapshot captured from Unity system facts.
        /// </summary>
        public readonly struct HardwareProfilerSnapshot
        {
            public HardwareProfilerSnapshot(
                int graphicsMemoryMegabytes,
                int systemMemoryMegabytes,
                int processorCount,
                int hardwareScore,
                float startupSurvivalPressure01)
            {
                GraphicsMemoryMegabytes = graphicsMemoryMegabytes;
                SystemMemoryMegabytes = systemMemoryMegabytes;
                ProcessorCount = processorCount;
                HardwareScore = hardwareScore;
                StartupSurvivalPressureByte = (byte)(Clamp01(startupSurvivalPressure01) * 255.0f + 0.5f);
            }

            /// <summary>Detected graphics memory in megabytes.</summary>
            public readonly int GraphicsMemoryMegabytes;

            /// <summary>Detected system memory in megabytes.</summary>
            public readonly int SystemMemoryMegabytes;

            /// <summary>Detected logical CPU core count.</summary>
            public readonly int ProcessorCount;

            /// <summary>Deterministic 0-100 BIOS hardware score.</summary>
            public readonly int HardwareScore;

            /// <summary>0-255 continuous survival pressure from immutable BIOS hardware facts.</summary>
            public readonly byte StartupSurvivalPressureByte;
        }

        /// <summary>
        /// Captures immutable Unity system hardware facts for the BIOS boot matrix.
        /// </summary>
        public static HardwareProfilerSnapshot CaptureSystemInfoSnapshot()
        {
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize : 0;
            int systemMemoryMb = SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize : 0;
            int processorCount = SystemInfo.processorCount > 0 ? SystemInfo.processorCount : 1;
            int hardwareScore = ResolveHardwareScore(graphicsMemoryMb, systemMemoryMb, processorCount);
            float startupSurvivalPressure01 = ResolveSystemInfoSurvivalPressure01(graphicsMemoryMb, processorCount);

            return new HardwareProfilerSnapshot(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                hardwareScore,
                startupSurvivalPressure01);
        }

        public static bool ShouldForceLowTier(double millisecondsPerStep, int graphicsMemoryMegabytes)
        {
            return millisecondsPerStep > 5.0d || graphicsMemoryMegabytes < SurvivalGraphicsMemoryMegabytes;
        }

        /// <summary>
        /// Runs the local PhysicsScene capsule benchmark and returns milliseconds per step.
        /// </summary>
        public static double RunBiosPhysicsBenchmarkMillisecondsPerStep()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return 0.0d;
#endif

            Scene scene = SceneManager.CreateScene(
                SceneName,
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsScene physicsScene = scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
                return double.MaxValue;

            GameObject floor = null;
            GameObject[] bodies = new GameObject[BenchmarkCapsuleCount]; // COLD ALLOC: GameObject[1000] - BIOS local physics cleanup table - owner: HardwareProfiler
            try
            {
                floor = new GameObject("BIOS_Profile_Floor", typeof(BoxCollider)); // COLD ALLOC: physics-only benchmark floor - no visual primitive mesh.
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(96f, 1f, 96f);
                SceneManager.MoveGameObjectToScene(floor, scene);

                int gridX = 0;
                int gridZ = 0;
                for (int i = 0; i < BenchmarkCapsuleCount; i++)
                {
                    float localX = (gridX - (GridColumns >> 1)) * GridSpacingMeters;
                    float localZ = (gridZ - 12) * GridSpacingMeters;
                    float localY = 4f + ((i & 7) * 0.125f);

                    GameObject capsule = new GameObject("BIOS_Profile_Capsule", typeof(CapsuleCollider)); // COLD ALLOC: physics-only benchmark body - no MeshRenderer/MeshFilter.
                    capsule.hideFlags = HideFlags.HideAndDontSave;
                    capsule.transform.position = new Vector3(localX, localY, localZ);

                    Rigidbody rigidbody = capsule.AddComponent<Rigidbody>();
                    rigidbody.useGravity = true;
                    rigidbody.mass = 1f;
                    SceneManager.MoveGameObjectToScene(capsule, scene);
                    bodies[i] = capsule;

                    gridX++;
                    if (gridX == GridColumns)
                    {
                        gridX = 0;
                        gridZ++;
                    }
                }

                int executedSteps = 0;
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int step = 0; step < BenchmarkStepCount; step++)
                {
                    physicsScene.Simulate(FixedStepSeconds);
                    executedSteps++;
                    if (stopwatch.Elapsed.TotalMilliseconds >= MaxBenchmarkWallMilliseconds)
                        break;
                }

                stopwatch.Stop();
                if (executedSteps <= 0)
                    return double.MaxValue;

                return stopwatch.Elapsed.TotalMilliseconds * ResolveStepReciprocal(executedSteps);
            }
            finally
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] != null)
                        DestroyBenchmarkObject(bodies[i]);
                }

                if (floor != null)
                    DestroyBenchmarkObject(floor);

                UnloadBenchmarkScene(scene);
            }
        }

        private static void UnloadBenchmarkScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
#else
            if (SceneManager.sceneCount > 1)
                SceneManager.UnloadSceneAsync(scene);
#endif
        }

        /// <summary>
        /// Returns the continuous BIOS startup quality weight after hardware facts and benchmark pressure.
        /// The legacy tier label is derived from this scalar by bootstrap code only.
        /// </summary>
        public static float ResolveStartupQualityWeight01(
            in HardwareProfilerSnapshot snapshot,
            double millisecondsPerStep)
        {
            float scoreWeight01 = Clamp01(snapshot.HardwareScore * 0.01f);
            float physicsWeight01 = ResolvePhysicsBenchmarkQualityWeight01(millisecondsPerStep);
            float systemInfoWeight01 = 1.0f - (snapshot.StartupSurvivalPressureByte * (1.0f / 255.0f));
            float hardwareWeight01 = scoreWeight01 < systemInfoWeight01 ? scoreWeight01 : systemInfoWeight01;
            return Clamp01(hardwareWeight01 < physicsWeight01 ? hardwareWeight01 : physicsWeight01);
        }

        private static int ResolveHardwareScore(int graphicsMemoryMegabytes, int systemMemoryMegabytes, int processorCount)
        {
            int score = 0;

            if (graphicsMemoryMegabytes >= 8200)
                score += 50;
            else if (graphicsMemoryMegabytes >= 4200)
                score += 35;
            else if (graphicsMemoryMegabytes >= SurvivalGraphicsMemoryMegabytes)
                score += 25;
            else if (graphicsMemoryMegabytes >= 1800)
                score += 15;
            else
                score += 5;

            if (processorCount >= 12)
                score += 30;
            else if (processorCount >= 8)
                score += 22;
            else if (processorCount >= SurvivalProcessorCount)
                score += 16;
            else
                score += 8;

            if (systemMemoryMegabytes >= 32000)
                score += 20;
            else if (systemMemoryMegabytes >= 16000)
                score += 14;
            else if (systemMemoryMegabytes >= 8000)
                score += 8;
            else
                score += 4;

            return score > 100 ? 100 : score;
        }

        private static float ResolveSystemInfoSurvivalPressure01(int graphicsMemoryMegabytes, int processorCount)
        {
            float graphicsPressure01 = graphicsMemoryMegabytes <= 0
                ? 0.0f
                : 1.0f - Clamp01(graphicsMemoryMegabytes / (double)SurvivalGraphicsMemoryMegabytes);
            float processorPressure01 = 1.0f - Clamp01(processorCount / (double)SurvivalProcessorCount);
            return graphicsPressure01 > processorPressure01 ? graphicsPressure01 : processorPressure01;
        }

        private static void DestroyBenchmarkObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        private static float ResolvePhysicsBenchmarkQualityWeight01(double millisecondsPerStep)
        {
            if (double.IsNaN(millisecondsPerStep) ||
                double.IsInfinity(millisecondsPerStep) ||
                millisecondsPerStep <= 0.0d)
            {
                return 1.0f;
            }

            double t = (millisecondsPerStep - FullPhysicsQualityMillisecondsPerStep) /
                       (SurvivalPhysicsQualityMillisecondsPerStep - FullPhysicsQualityMillisecondsPerStep);
            t = Clamp01(t);
            double smooth = t * t * (3.0d - (2.0d * t));
            return Clamp01((float)(1.0d - smooth));
        }

        private static float Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0f;

            if (value <= 0.0d)
                return 0.0f;

            return value >= 1.0d ? 1.0f : (float)value;
        }

        private static double ResolveStepReciprocal(int executedSteps)
        {
            switch (executedSteps)
            {
                case 1:
                    return 1.0d;
                case 2:
                    return 0.5d;
                case 3:
                    return 0.3333333333333333d;
                case 4:
                    return 0.25d;
                case 5:
                    return 0.2d;
                case 6:
                    return 0.1666666666666667d;
                case 7:
                    return 0.1428571428571429d;
                case 8:
                    return 0.125d;
                case 9:
                    return 0.1111111111111111d;
                default:
                    return InvBenchmarkStepCount;
            }
        }
    }
}
