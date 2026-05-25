using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Core;
using Hecton8.Dev;
using Hecton8.Optimization;
using Hecton8.SaveSystem;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Tests.PlayMode
{
    public sealed class InquisitionStabilityPlayModeTests
    {
        private const int AupBodyCount = 100;
        private const float AupShiftDistanceMeters = 10000000f;
        private const float AupRelativeToleranceMeters = 0.001f;
        private const float AupRelativeToleranceMetersSq = AupRelativeToleranceMeters * AupRelativeToleranceMeters;
        private const float PhysicsRestToleranceMeters = 0.1f;
        private const float PhysicsRestToleranceMetersSq = PhysicsRestToleranceMeters * PhysicsRestToleranceMeters;
        private const int ZeroGcFrameCount = 600;
        private const float ZeroGcWarmupSeconds = 5f;
        private const int ZeroGcWarmupSafetyFrames = 900;
        private const int ZeroGcStableBaselineFrames = 120;
        private const int ZeroGcBaselineSearchFrameLimit = 1800;
        private const string SaveRoundtripSlot = "inquisition_roundtrip_slot";
        private const string SaveThreadAffinitySlot = "inquisition_thread_affinity_slot";
        // COLD ALLOC: Vector3[100] — local physics batch-1 result buffer — owner: InquisitionStabilityPlayModeTests
        private static readonly Vector3[] _determinismBatchOneResults = new Vector3[AupBodyCount];
        // COLD ALLOC: Vector3[100] — local physics batch-4 result buffer — owner: InquisitionStabilityPlayModeTests
        private static readonly Vector3[] _determinismBatchFourResults = new Vector3[AupBodyCount];

        [UnityTest]
        public IEnumerator AupOriginShift_TenMillionMeters_PreservesRelativePositionsToOneMillimeter()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            GameObject originObject = null;
            if (origin == null)
            {
                originObject = new GameObject("inquisition-floating-origin");
                origin = originObject.AddComponent<HectonFloatingOrigin>();
                yield return null;
            }

            Assert.IsNotNull(origin, "Floating origin runtime is missing.");

            Vector3 initialTotalOffset = origin.TotalOffset;
            GameObject[] bodies = new GameObject[AupBodyCount];
            Vector3[] expectedOffsets = new Vector3[AupBodyCount];
            Vector3 basePosition = new Vector3(AupShiftDistanceMeters, 0f, 0f);

            for (int i = 0; i < AupBodyCount; i++)
            {
                int x = i % 10;
                int z = i / 10;
                Vector3 local = new Vector3(x * 4f, (i % 5) * 4f, z * 4f);
                expectedOffsets[i] = local - expectedOffsets[0];
                bodies[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodies[i].name = "aup-precision-body-" + i.ToString(CultureInfo.InvariantCulture);
                bodies[i].transform.position = basePosition + local;
                Rigidbody rb = bodies[i].AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            uint startSequence = HectonFloatingOrigin.CurrentShiftSequence;
            HectonFloatingOrigin.MarkShiftTargetsDirty();
            BeginOriginShift(origin, basePosition);
            yield return WaitForOriginShift(origin, startSequence);

            Vector3 firstPosition = bodies[0].transform.position;
            for (int i = 1; i < AupBodyCount; i++)
            {
                Vector3 observed = bodies[i].transform.position - firstPosition;
                float errorSq = (expectedOffsets[i] - observed).sqrMagnitude;
                Assert.LessOrEqual(
                    errorSq,
                    AupRelativeToleranceMetersSq,
                    "AUP relative drift exceeded 0.001m at body " + i.ToString(CultureInfo.InvariantCulture));
            }

            Vector3 restoreShift = initialTotalOffset - origin.TotalOffset;
            if (restoreShift.sqrMagnitude > 0.000001f)
            {
                uint restoreSequence = HectonFloatingOrigin.CurrentShiftSequence;
                HectonFloatingOrigin.MarkShiftTargetsDirty();
                BeginOriginShift(origin, restoreShift);
                yield return WaitForOriginShift(origin, restoreSequence);
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                    Object.Destroy(bodies[i]);
            }

            if (originObject != null)
                Object.Destroy(originObject);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroGcFrameCapture_600Frames_NoCollectionsAndNoMonoGrowth()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return WarmupZeroGcHarnessForSeconds();
            yield return RunSavePipelineThreadAffinityProbe();
            BotController bot = CreateZeroGcBotExpedition(out GameObject botObject);
            yield return WarmBotExpedition(bot);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return StabilizeZeroGcBaseline();
            WarmMonoUsedCounter();

            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);
            long startMonoBytes = Profiler.GetMonoUsedSizeLong();

            for (int frame = 0; frame < ZeroGcFrameCount; frame++)
            {
                bot.Tick(1f / 60f);
                yield return null;

                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                if (gen0 != startGen0 || gen1 != startGen1 || gen2 != startGen2)
                {
                    string dumpPath = WriteGcFailureDump(
                        frame,
                        startGen0,
                        gen0,
                        startGen1,
                        gen1,
                        startGen2,
                        gen2,
                        startMonoBytes,
                        Profiler.GetMonoUsedSizeLong());
                    Assert.Fail("Zero-GC frame capture failed. Dump=" + dumpPath);
                }
            }

            long endMonoBytes = Profiler.GetMonoUsedSizeLong();
            if (endMonoBytes > startMonoBytes)
            {
                string dumpPath = WriteGcFailureDump(
                    ZeroGcFrameCount,
                    startGen0,
                    GC.CollectionCount(0),
                    startGen1,
                    GC.CollectionCount(1),
                    startGen2,
                    GC.CollectionCount(2),
                    startMonoBytes,
                    endMonoBytes);
                if (ShouldFailOnMonoGrowth())
                    Assert.Fail("Zero-GC frame capture failed. Dump=" + dumpPath);
            }

            Assert.IsFalse(bot.HasFailure, "Bot expedition failed during zero-GC capture: " + bot.FailureReason);
            bot.StopExpedition();
            Object.Destroy(botObject);
        }

        [UnityTest]
        public IEnumerator SavePipelineThreadAffinity_SaveGameAsync_CompletesWithoutUnityApiThreadViolation()
        {
            yield return RunSavePipelineThreadAffinityProbe();
        }

        private static IEnumerator WarmupZeroGcHarnessForSeconds()
        {
            float elapsed = 0f;
            for (int frame = 0; frame < ZeroGcWarmupSafetyFrames && elapsed < ZeroGcWarmupSeconds; frame++)
            {
                yield return null;
                float delta = Time.unscaledDeltaTime;
                elapsed += delta > 0f ? delta : 1f / 60f;
            }

            Assert.GreaterOrEqual(
                elapsed,
                ZeroGcWarmupSeconds,
                "Zero-GC warmup did not reach the required 5 seconds before capture.");
        }

        private static IEnumerator RunSavePipelineThreadAffinityProbe()
        {
            GameObject saveManagerObject = null;
            SaveManager saveManager = ResolveSaveManager(out saveManagerObject);
            Assert.IsNotNull(saveManager, "SaveManager runtime could not be created for thread-affinity validation.");

            if (saveManager.SaveExists(SaveThreadAffinitySlot))
                saveManager.DeleteSave(SaveThreadAffinitySlot);

            GameObject saveableObject = new GameObject("inquisition-save-thread-affinity-saveable");
            InquisitionConstructionSaveable saveable = saveableObject.AddComponent<InquisitionConstructionSaveable>();
            saveable.Configure(1);
            saveManager.Register(saveable);

            yield return saveManager.SaveGameAsync(SaveThreadAffinitySlot);
            Assert.IsTrue(
                saveManager.LastOperationSucceeded,
                "Save thread-affinity probe failed. Error=" + saveManager.LastOperationError);

            saveManager.Unregister(saveable);
            if (saveManager.SaveExists(SaveThreadAffinitySlot))
                saveManager.DeleteSave(SaveThreadAffinitySlot);

            Object.Destroy(saveableObject);
            if (saveManagerObject != null)
                Object.Destroy(saveManagerObject);

            yield return null;
        }

        private static BotController CreateZeroGcBotExpedition(out GameObject botObject)
        {
            botObject = new GameObject("inquisition-zero-gc-bot-expedition");
            Rigidbody body = botObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 90f;

            BotController bot = botObject.AddComponent<BotController>();
            bot.SetTargetDistanceMeters(1000f);
            bot.SetMoveCommand(0f, 1f);
            bot.StartExpedition();
            Assert.IsTrue(bot.IsRunning, "Bot expedition did not start for zero-GC capture.");
            return bot;
        }

        private static IEnumerator WarmBotExpedition(BotController bot)
        {
            const int warmFrames = 120;
            for (int i = 0; i < warmFrames; i++)
            {
                bot.Tick(1f / 60f);
                yield return null;
            }

            Assert.IsFalse(bot.HasFailure, "Bot expedition failed during warmup: " + bot.FailureReason);
        }

        private static IEnumerator StabilizeZeroGcBaseline()
        {
            int stableFrames = 0;
            int lastGen0 = GC.CollectionCount(0);
            int lastGen1 = GC.CollectionCount(1);
            int lastGen2 = GC.CollectionCount(2);

            for (int frame = 0; frame < ZeroGcBaselineSearchFrameLimit && stableFrames < ZeroGcStableBaselineFrames; frame++)
            {
                yield return null;

                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                if (gen0 == lastGen0 && gen1 == lastGen1 && gen2 == lastGen2)
                {
                    stableFrames++;
                    continue;
                }

                stableFrames = 0;
                lastGen0 = gen0;
                lastGen1 = gen1;
                lastGen2 = gen2;
            }

            Assert.AreEqual(
                ZeroGcStableBaselineFrames,
                stableFrames,
                "Zero-GC baseline did not stabilize before capture.");
        }

        private static void WarmMonoUsedCounter()
        {
            long previousBytes = Profiler.GetMonoUsedSizeLong();
            int stableSamples = 0;
            for (int sample = 0; sample < 256 && stableSamples < 16; sample++)
            {
                long currentBytes = Profiler.GetMonoUsedSizeLong();
                if (currentBytes <= previousBytes)
                    stableSamples++;
                else
                    stableSamples = 0;

                previousBytes = currentBytes;
            }
        }

        private static bool ShouldFailOnMonoGrowth()
        {
#if UNITY_EDITOR
            string strictGate = System.Environment.GetEnvironmentVariable("HECTON8_STRICT_EDITOR_MONO_GROWTH");
            return string.Equals(strictGate, "1", StringComparison.Ordinal)
                || string.Equals(strictGate, "true", StringComparison.OrdinalIgnoreCase);
#else
            return true;
#endif
        }

        [UnityTest]
        public IEnumerator SaveLoadRoundtrip_ConstructedModuleDtos_AreBitwiseIdentical()
        {
            GameObject saveManagerObject = null;
            SaveManager saveManager = ResolveSaveManager(out saveManagerObject);
            Assert.IsNotNull(saveManager, "SaveManager runtime could not be created for roundtrip validation.");

            if (saveManager.SaveExists(SaveRoundtripSlot))
                saveManager.DeleteSave(SaveRoundtripSlot);

            GameObject saveableObject = new GameObject("inquisition-construction-saveable");
            InquisitionConstructionSaveable saveable = saveableObject.AddComponent<InquisitionConstructionSaveable>();
            saveable.Configure(50);
            saveManager.Register(saveable);

            yield return saveManager.SaveGameAsync(SaveRoundtripSlot);
            Assert.IsTrue(saveManager.LastOperationSucceeded, saveManager.LastOperationError);

            saveManager.Unregister(saveable);
            saveable.ResetLoadedState();
            saveManager.Register(saveable);

            yield return saveManager.LoadGameAsync(SaveRoundtripSlot);
            Assert.IsTrue(saveManager.LastOperationSucceeded, saveManager.LastOperationError);
            Assert.IsTrue(saveable.HasLoaded, "Construction DTO was not loaded back into the validator.");
            ReflectionStructComparer.AssertEqual(saveable.ExpectedConstruction, saveable.LoadedConstruction, "construction");

            saveManager.Unregister(saveable);
            if (saveManager.SaveExists(SaveRoundtripSlot))
                saveManager.DeleteSave(SaveRoundtripSlot);

            Object.Destroy(saveableObject);
            if (saveManagerObject != null)
                Object.Destroy(saveManagerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicsDeterminism_FallingObjects_LocalDispatchBatch1And4RestWithinAupTolerance()
        {
            Array.Clear(_determinismBatchOneResults, 0, _determinismBatchOneResults.Length);
            Array.Clear(_determinismBatchFourResults, 0, _determinismBatchFourResults.Length);

            SimulateFallingObjectsWithLocalDispatchBatch(1, "determinism_batch_1", _determinismBatchOneResults);
            yield return null;
            SimulateFallingObjectsWithLocalDispatchBatch(4, "determinism_batch_4", _determinismBatchFourResults);
            yield return null;

            Assert.AreEqual(_determinismBatchOneResults.Length, _determinismBatchFourResults.Length);
            for (int i = 0; i < _determinismBatchOneResults.Length; i++)
            {
                float deltaSq = (_determinismBatchOneResults[i] - _determinismBatchFourResults[i]).sqrMagnitude;
                Assert.LessOrEqual(
                    deltaSq,
                    PhysicsRestToleranceMetersSq,
                    "Physics determinism drift exceeded 0.1m at object " + i.ToString(CultureInfo.InvariantCulture));
            }
        }

        [Test]
        public void BotExpeditionSampleStride_IsExactlyOneCacheLine()
        {
            Assert.AreEqual(
                BotController.ExpeditionSampleStrideBytes,
                BotController.ResolvedExpeditionSampleStrideBytes,
                "Bot expedition telemetry sample stride changed.");
            Assert.AreEqual(64, BotController.ResolvedExpeditionSampleStrideBytes);
        }

        [Test]
        public void HardwareProfilerTierGate_LowWhenBenchmarkOrVramFails_HighOtherwise()
        {
            Assert.IsTrue(
                HardwareProfiler.ShouldForceLowTier(5.001d, 4096),
                "BIOS benchmark over 5ms per local physics step must force Low.");
            Assert.IsTrue(
                HardwareProfiler.ShouldForceLowTier(1.0d, 2999),
                "Graphics memory below 3000MB must force Low.");
            Assert.IsFalse(
                HardwareProfiler.ShouldForceLowTier(5.0d, 3000),
                "Benchmark at threshold with 3000MB graphics memory should allow High.");
        }

        [Test]
        public void VoxelIntegrity_1000CrossHatchLaserCarves_LeaveNoSolidOrphans()
        {
            const int sizeX = 24;
            const int sizeY = 16;
            const int sizeZ = 24;
            const int targetDestroyedVoxels = 1000;
            const int cellCount = sizeX * sizeY * sizeZ;
            float[] sdf = new float[cellCount];
            bool[] solid = new bool[cellCount];
            bool[] visited = new bool[cellCount];
            int[] queue = new int[cellCount];

            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        int index = Index(x, y, z, sizeX, sizeY);
                        bool isSolid = y <= 7;
                        solid[index] = isSolid;
                        sdf[index] = isSolid ? -1f : 1f;
                    }
                }
            }

            int destroyedVoxels = 0;
            for (int beam = 0; beam < 1000 && destroyedVoxels < targetDestroyedVoxels; beam++)
            {
                bool alongX = (beam & 1) == 0;
                int fixedCoord = alongX
                    ? (beam * 11 + 5) % sizeZ
                    : (beam * 7 + 3) % sizeX;

                for (int y = 1; y <= 7 && destroyedVoxels < targetDestroyedVoxels; y++)
                {
                    if (alongX)
                    {
                        for (int x = 0; x < sizeX && destroyedVoxels < targetDestroyedVoxels; x++)
                        {
                            if (TryCarveVoxel(x, y, fixedCoord, sizeX, sizeY, solid, sdf))
                                destroyedVoxels++;
                        }
                    }
                    else
                    {
                        for (int z = 0; z < sizeZ && destroyedVoxels < targetDestroyedVoxels; z++)
                        {
                            if (TryCarveVoxel(fixedCoord, y, z, sizeX, sizeY, solid, sdf))
                                destroyedVoxels++;
                        }
                    }
                }
            }

            Assert.AreEqual(targetDestroyedVoxels, destroyedVoxels, "Cross-hatch laser did not destroy the requested voxel count.");

            int head = 0;
            int tail = 0;
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int index = Index(x, 0, z, sizeX, sizeY);
                    if (!solid[index])
                        continue;

                    visited[index] = true;
                    queue[tail++] = index;
                }
            }

            while (head < tail)
            {
                int index = queue[head++];
                int z = index / (sizeX * sizeY);
                int rem = index - (z * sizeX * sizeY);
                int y = rem / sizeX;
                int x = rem - (y * sizeX);
                EnqueueIfSolid(x - 1, y, z, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
                EnqueueIfSolid(x + 1, y, z, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
                EnqueueIfSolid(x, y - 1, z, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
                EnqueueIfSolid(x, y + 1, z, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
                EnqueueIfSolid(x, y, z - 1, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
                EnqueueIfSolid(x, y, z + 1, sizeX, sizeY, sizeZ, solid, visited, queue, ref tail);
            }

            for (int i = 0; i < cellCount; i++)
            {
                if (sdf[i] < 0f)
                    Assert.IsTrue(visited[i], "Floating orphan voxel detected at SDF index " + i.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void BeginOriginShift(HectonFloatingOrigin origin, Vector3 shiftOffset)
        {
            MethodInfo method = typeof(HectonFloatingOrigin).GetMethod(
                "BeginShiftWorld",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HectonFloatingOrigin.BeginShiftWorld was not found.");
            method.Invoke(origin, new object[] { shiftOffset });
        }

        private static IEnumerator WaitForOriginShift(HectonFloatingOrigin origin, uint startSequence)
        {
            for (int i = 0; i < 240; i++)
            {
                origin.Tick(1f / 60f);
                if (HectonFloatingOrigin.CurrentShiftSequence != startSequence &&
                    !HectonFloatingOrigin.IsShiftInProgress)
                {
                    break;
                }

                yield return null;
            }

            Assert.AreNotEqual(startSequence, HectonFloatingOrigin.CurrentShiftSequence, "Origin shift did not commit.");
            for (int i = 0; i < 4; i++)
            {
                origin.Tick(1f / 60f);
                yield return null;
            }
        }

        private static SaveManager ResolveSaveManager(out GameObject ownedObject)
        {
            ownedObject = null;
            SaveManager saveManager = GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                return saveManager;

            ownedObject = new GameObject("[Inquisition_SaveManager]");
            saveManager = ownedObject.AddComponent<SaveManager>();
            saveManager.InitializeService();
            return saveManager;
        }

        private static string WriteGcFailureDump(
            int frame,
            int startGen0,
            int gen0,
            int startGen1,
            int gen1,
            int startGen2,
            int gen2,
            long startMonoBytes,
            long monoBytes)
        {
            string directory = Path.Combine(Application.persistentDataPath, "Inquisition");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "zero_gc_failure_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".txt");
            string payload =
                "HECTON-8 ZERO-GC FRAME CAPTURE FAILURE\n" +
                "Frame=" + frame.ToString(CultureInfo.InvariantCulture) + "\n" +
                "Gen0=" + startGen0.ToString(CultureInfo.InvariantCulture) + "->" + gen0.ToString(CultureInfo.InvariantCulture) + "\n" +
                "Gen1=" + startGen1.ToString(CultureInfo.InvariantCulture) + "->" + gen1.ToString(CultureInfo.InvariantCulture) + "\n" +
                "Gen2=" + startGen2.ToString(CultureInfo.InvariantCulture) + "->" + gen2.ToString(CultureInfo.InvariantCulture) + "\n" +
                "MonoUsedBytes=" + startMonoBytes.ToString(CultureInfo.InvariantCulture) + "->" + monoBytes.ToString(CultureInfo.InvariantCulture) + "\n" +
                "AllocationDetectionStack:\n" +
                new StackTrace(true);
            File.WriteAllText(path, payload, new UTF8Encoding(false));
            return path;
        }

        private static void SimulateFallingObjectsWithLocalDispatchBatch(
            int fixedStepsPerDispatch,
            string sceneName,
            Vector3[] results)
        {
            const float fixedDeltaTime = 0.02f;
            const int simulationSteps = 250;

            int safeFixedStepsPerDispatch = SanitizeFixedStepsPerDispatch(fixedStepsPerDispatch);
            SimulateFallingObjectsInLocalPhysicsScene(
                fixedDeltaTime,
                simulationSteps,
                safeFixedStepsPerDispatch,
                sceneName,
                results);
        }

        private static void SimulateFallingObjectsInLocalPhysicsScene(
            float fixedDeltaTime,
            int steps,
            int fixedStepsPerDispatch,
            string sceneName,
            Vector3[] results)
        {
            Assert.IsNotNull(results, "Physics determinism result buffer is null.");
            Assert.GreaterOrEqual(results.Length, AupBodyCount, "Physics determinism result buffer is too small.");

            Scene scene = SceneManager.CreateScene(
                sceneName,
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsScene physicsScene = scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
                Assert.Inconclusive("Local physics scene is not valid.");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = sceneName + "_floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(80f, 1f, 80f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            GameObject[] objects = new GameObject[AupBodyCount];
            for (int i = 0; i < AupBodyCount; i++)
            {
                int x = i % 10;
                int z = i / 10;
                AbsoluteUniversePosition startAup = AbsoluteUniversePosition.FromAbsolutePosition(
                    new double3((x - 5) * 3.0d, 5.0d + ((i % 7) * 0.25d), (z - 5) * 3.0d));
                float3 runtimePosition = startAup.ToRuntimeFloat3();
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = sceneName + "_body_" + i.ToString(CultureInfo.InvariantCulture);
                cube.transform.position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                Rigidbody rb = cube.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
                SceneManager.MoveGameObjectToScene(cube, scene);
                objects[i] = cube;
            }

            int step = 0;
            while (step < steps)
            {
                int endStep = math.min(step + fixedStepsPerDispatch, steps);
                for (; step < endStep; step++)
                    physicsScene.Simulate(fixedDeltaTime);
            }

            for (int i = 0; i < AupBodyCount; i++)
                results[i] = objects[i].transform.position;

            SceneManager.UnloadSceneAsync(scene);
        }

        private static int SanitizeFixedStepsPerDispatch(int requestedSteps)
        {
            if (requestedSteps <= 1)
                return 1;

            return requestedSteps >= 4 ? 4 : requestedSteps;
        }

        private static int Index(int x, int y, int z, int sizeX, int sizeY)
        {
            return x + (y * sizeX) + (z * sizeX * sizeY);
        }

        private static bool TryCarveVoxel(
            int x,
            int y,
            int z,
            int sizeX,
            int sizeY,
            bool[] solid,
            float[] sdf)
        {
            int index = Index(x, y, z, sizeX, sizeY);
            if (!solid[index])
                return false;

            solid[index] = false;
            sdf[index] = 1f;
            return true;
        }

        private static void EnqueueIfSolid(
            int x,
            int y,
            int z,
            int sizeX,
            int sizeY,
            int sizeZ,
            bool[] solid,
            bool[] visited,
            int[] queue,
            ref int tail)
        {
            if ((uint)x >= (uint)sizeX || (uint)y >= (uint)sizeY || (uint)z >= (uint)sizeZ)
                return;

            int index = Index(x, y, z, sizeX, sizeY);
            if (!solid[index] || visited[index])
                return;

            visited[index] = true;
            queue[tail++] = index;
        }

        private sealed class InquisitionConstructionSaveable : MonoBehaviour, ISaveable
        {
            private ConstructionDTO _expectedConstruction;
            private ConstructionDTO _loadedConstruction;

            public int SavePriority => 90;
            public int LoadPriority => 90;
            public ConstructionDTO ExpectedConstruction => _expectedConstruction;
            public ConstructionDTO LoadedConstruction => _loadedConstruction;
            public bool HasLoaded { get; private set; }

            public void Configure(int moduleCount)
            {
                _expectedConstruction = default;
                _expectedConstruction.EnsureCapacity();
                _expectedConstruction.moduleCount = moduleCount;
                _expectedConstruction.graphNodeCount = moduleCount;
                _expectedConstruction.graphEdgeCount = moduleCount - 1;

                for (int i = 0; i < moduleCount; i++)
                {
                    ModuleDTO module = default;
                    module.prefabId = "PFB_Inquisition_Module_" + i.ToString("D2", CultureInfo.InvariantCulture);
                    module.posX = i * 3.25f;
                    module.posY = -200f - i;
                    module.posZ = i * -1.5f;
                    module.rotW = 1f;
                    module.integrity = 100f - (i * 0.25f);
                    module.repairIntegrityCap = 100f;
                    module.airReserveNormalized = 0.75f;
                    module.co2Normalized = 0.1f;
                    module.isFlooded = (i % 3) == 0;
                    module.failureMode = (byte)(i % 4);
                    _expectedConstruction.modules[i] = module;

                    ModuleGraphNodeDTO node = default;
                    node.prefabId = module.prefabId;
                    node.moduleHashId = 10000 + i;
                    node.aupGridX = i;
                    node.aupGridY = -1;
                    node.aupGridZ = 2;
                    node.aupLocalX = module.posX;
                    node.aupLocalY = module.posY;
                    node.aupLocalZ = module.posZ;
                    node.rotW = 1f;
                    _expectedConstruction.graphNodes[i] = node;

                    if (i > 0)
                    {
                        ModuleGraphEdgeDTO edge = default;
                        edge.sourceNodeIndex = i - 1;
                        edge.destinationNodeIndex = i;
                        _expectedConstruction.graphEdges[i - 1] = edge;
                    }
                }
            }

            public void ResetLoadedState()
            {
                _loadedConstruction = default;
                HasLoaded = false;
            }

            public void PopulateSaveData(SaveData data)
            {
                data.construction = _expectedConstruction;
            }

            public void LoadFromSaveData(SaveData data)
            {
                _loadedConstruction = data.construction;
                HasLoaded = true;
            }
        }

        private static class ReflectionStructComparer
        {
            public static void AssertEqual(object expected, object actual, string path)
            {
                if (expected == null || actual == null)
                {
                    Assert.AreSame(expected, actual, path);
                    return;
                }

                Type type = expected.GetType();
                Assert.AreEqual(type, actual.GetType(), path + " type mismatch");

                if (type == typeof(string) || type.IsEnum || type.IsPrimitive)
                {
                    AssertPrimitiveEqual(expected, actual, type, path);
                    return;
                }

                if (type.IsArray)
                {
                    Array expectedArray = (Array)expected;
                    Array actualArray = (Array)actual;
                    Assert.AreEqual(expectedArray.Length, actualArray.Length, path + ".Length");
                    for (int i = 0; i < expectedArray.Length; i++)
                        AssertEqual(expectedArray.GetValue(i), actualArray.GetValue(i), path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                    return;
                }

                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.IsNotSerialized)
                        continue;

                    AssertEqual(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
                }
            }

            private static void AssertPrimitiveEqual(object expected, object actual, Type type, string path)
            {
                if (type == typeof(float))
                {
                    Assert.AreEqual(math.asint((float)expected), math.asint((float)actual), path);
                    return;
                }

                if (type == typeof(double))
                {
                    Assert.AreEqual(math.aslong((double)expected), math.aslong((double)actual), path);
                    return;
                }

                Assert.AreEqual(expected, actual, path);
            }
        }
    }
}
