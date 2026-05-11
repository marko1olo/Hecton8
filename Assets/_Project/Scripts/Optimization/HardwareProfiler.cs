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

        private const float FixedStepSeconds = 0.02f;
        private const int GridColumns = 40;
        private const float GridSpacingMeters = 1.75f;
        private const double InvBenchmarkStepCount = 1.0d / BenchmarkStepCount;
        private const string SceneName = "HECTON8_BIOS_HARDWARE_PROFILER";

        /// <summary>
        /// Runs the local PhysicsScene capsule benchmark and returns milliseconds per step.
        /// </summary>
        public static double RunBiosPhysicsBenchmarkMillisecondsPerStep()
        {
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

                for (int i = 0; i < BenchmarkCapsuleCount; i++)
                {
                    int x = i % GridColumns;
                    int z = i / GridColumns;
                    float localX = (x - (GridColumns >> 1)) * GridSpacingMeters;
                    float localZ = (z - 12) * GridSpacingMeters;
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

                SceneManager.UnloadSceneAsync(scene);
            }
        }

        /// <summary>
        /// Returns the benchmark-forced scalability tier before higher-tier expansion.
        /// </summary>
        public static bool ShouldForceLowTier(double millisecondsPerStep, int graphicsMemoryMegabytes)
        {
            if (graphicsMemoryMegabytes > 0 && graphicsMemoryMegabytes < LowTierGraphicsMemoryMegabytes)
                return true;

            return millisecondsPerStep > LowTierMillisecondsPerStep;
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
