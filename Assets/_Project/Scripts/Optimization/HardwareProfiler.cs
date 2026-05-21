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
        public const double LowTierMillisecondsPerStep = 5.0d;
        public const double MaxBenchmarkWallMilliseconds = 500.0d;
        public const int LowTierGraphicsMemoryMegabytes = 3000;
        public const int LowTierProcessorCount = 6;

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
                bool forceLowTier)
            {
                GraphicsMemoryMegabytes = graphicsMemoryMegabytes;
                SystemMemoryMegabytes = systemMemoryMegabytes;
                ProcessorCount = processorCount;
                HardwareScore = hardwareScore;
                ForceLowTier = forceLowTier ? (byte)1 : (byte)0;
            }

            /// <summary>Detected graphics memory in megabytes.</summary>
            public readonly int GraphicsMemoryMegabytes;

            /// <summary>Detected system memory in megabytes.</summary>
            public readonly int SystemMemoryMegabytes;

            /// <summary>Detected logical CPU core count.</summary>
            public readonly int ProcessorCount;

            /// <summary>Deterministic 0-100 BIOS hardware score.</summary>
            public readonly int HardwareScore;

            /// <summary>1 when the BIOS benchmark requests a conservative startup tier.</summary>
            public readonly byte ForceLowTier;
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
            bool forceLowTier =
                (graphicsMemoryMb > 0 && graphicsMemoryMb < LowTierGraphicsMemoryMegabytes) ||
                processorCount < LowTierProcessorCount;

            return new HardwareProfilerSnapshot(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                hardwareScore,
                forceLowTier);
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
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "BIOS_Profile_Floor";
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

                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = "BIOS_Profile_Capsule";
                    capsule.hideFlags = HideFlags.HideAndDontSave;
                    capsule.transform.position = new Vector3(localX, localY, localZ);
                    MeshRenderer renderer = capsule.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.enabled = false;

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
        /// Returns the benchmark-forced scalability tier before higher-tier expansion.
        /// </summary>
        public static bool ShouldForceLowTier(double millisecondsPerStep, int graphicsMemoryMegabytes)
        {
            return (graphicsMemoryMegabytes > 0 & graphicsMemoryMegabytes < LowTierGraphicsMemoryMegabytes) |
                (millisecondsPerStep > LowTierMillisecondsPerStep);
        }

        private static int ResolveHardwareScore(int graphicsMemoryMegabytes, int systemMemoryMegabytes, int processorCount)
        {
            int score = 0;

            if (graphicsMemoryMegabytes >= 8200)
                score += 50;
            else if (graphicsMemoryMegabytes >= 4200)
                score += 35;
            else if (graphicsMemoryMegabytes >= LowTierGraphicsMemoryMegabytes)
                score += 25;
            else if (graphicsMemoryMegabytes >= 1800)
                score += 15;
            else
                score += 5;

            if (processorCount >= 12)
                score += 30;
            else if (processorCount >= 8)
                score += 22;
            else if (processorCount >= LowTierProcessorCount)
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

        private static void DestroyBenchmarkObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
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
