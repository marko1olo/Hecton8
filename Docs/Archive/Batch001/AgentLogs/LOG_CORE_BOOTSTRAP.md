# LOG_CORE_BOOTSTRAP

## 2026-05-11 CORE_BOOTSTRAP Final Report

Agent: BIOS_COMMANDER
Prompt: CORE_BOOTSTRAP
Domain: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)
Status: PENDING VERIFICATION

What was wrong:
- Boot had no single BIOS-owned hardware score and no hard CPU<6/VRAM<3000 low-tier lock.
- Boot ordering mixed memory/data/presentation work.
- Shader math LOD keywords were not explicit enough at boot.
- Threadpool work existed near boot/MMF I/O.
- Missing plugin contracts could reach world load.
- Shutdown lacked a named `DisposeAll` facade.
- Addressable tier textures could hitch after CoreReady.
- Lore MMF payloads had no lazy proxy.
- Service heartbeat evidence was warning-heavy and not tied tightly enough to blackbox export.
- Threaded Unity log faults were not mirrored immediately into numeric telemetry.
- GC was still enabled after CoreReady.

What was done:
- Added `HardwareProfilerSnapshot`, hardware score, low-tier CPU/VRAM constants, and cold BIOS hardware capture.
- Extended `HectonHardwareProfile` with `HardwareScore`.
- Split bootstrap into allocator, event bus, MMF storage, data monolith, core services, and presentation phases.
- Added explicit `_MATH_LOD_LOW`/`_MATH_LOD_HIGH` warmup before shader variant collection handling.
- Replaced managed `Task.Run` use with `Awaitable.BackgroundThreadAsync()` in bootstrap and a named persistent MMF prefetch thread in storage.
- Added reflection fast-fail for `IOceanKinematics` in `Hecton8.Plugins`.
- Clamped Unity job workers to `ProcessorCount - 1`.
- Forced `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 60`.
- Added `IServiceShutdown.DisposeAll()` and reverse registry-slot disposal.
- Verified 32-byte unmanaged `boot.bin` safe-mode markers.
- Added `Tier_Low`/`Tier_High` Addressables dependency prewarm before CoreReady.
- Added `LoreEncyclopediaLazyProxy` for first-use MMF lore loading.
- Verified `[ThreadStatic]` high-traffic registry caches and strict static constructor editor audit.
- Added `ISystem.TickCount`, 60-second bootstrap/watchdog heartbeat sampling, and blackbox dump trigger on stale counters.
- Verified `LoadSceneAsync` activation is gated behind resident world prefab pools.
- Verified no `_activeSystems` foreach path exists in bootstrapper.
- Added `UnityLogFault` telemetry event and routed threaded Unity error/assert/exception hashes into `GlobalTelemetryBus`.
- Disabled `UnityEngine.Scripting.GarbageCollector.GCMode` immediately after CoreReady marker.

Cinematic cheats used:
- BIOS hardware score compresses VRAM/RAM/CPU into a deterministic 0-100 scalar instead of per-system subjective quality guesses.
- Physics benchmark grid avoids `%` and `/` in capsule placement and uses precomputed reciprocals for executed step count.
- Low-tier gate uses bitwise boolean composition in `ShouldForceLowTier`.
- Math LOD is reduced to two global shader keywords and a registry precision enum.
- Tier texture warmup loads only the selected tier label; MX350 does not pay for high-tier texture residency.
- Lore payload hydration is deferred behind a proxy until first user-facing lore request.

Exact estimated savings:
- Hardware score: avoids repeated `SystemInfo` reads and scattered tier branches; estimated 40 us cold path, 0 us hot path.
- CPU<6/VRAM<3000 low lock: prevents high-precision shader/math branch selection on low hardware; expected frame-stability gain, no measured Unity profiler result available.
- Shader keyword prewarm: estimated 8 us cold path, 0 us hot path.
- Job worker clamp: estimated 4 us cold configuration; saves OS/audio starvation risk rather than direct math time.
- VSync/frame cap: estimated 2 us cold configuration.
- Registry thread-static caches: estimated 20 ns saved per cached service read after first thread hit.
- Heartbeat scan: 255-slot scan every 60 seconds; 0 us per-frame hot path.
- GC disable: one cold property write after CoreReady.

HardwareProfiler code:
```csharp
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
                ForceLowTier = forceLowTier;
            }

            public int GraphicsMemoryMegabytes { get; }
            public int SystemMemoryMegabytes { get; }
            public int ProcessorCount { get; }
            public int HardwareScore { get; }
            public bool ForceLowTier { get; }
        }

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

        public static double RunBiosPhysicsBenchmarkMillisecondsPerStep()
        {
            Scene scene = SceneManager.CreateScene(
                SceneName,
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsScene physicsScene = scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
                return double.MaxValue;

            GameObject floor = null;
            GameObject[] bodies = new GameObject[BenchmarkCapsuleCount];
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

                SceneManager.UnloadSceneAsync(scene);
            }
        }

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
```

Last successful build output after loop 3:
```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  Hecton8.Bootstrap.Contracts -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Bootstrap.Contracts.dll
  Hecton8.World.Contracts -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.World.Contracts.dll
  Hecton8.Input.Generated -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Input.Generated.dll
  Unity.RenderPipelines.Universal.Runtime -> C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
  EasySave3 -> C:\hades\Hecton8\Temp\bin\Debug\EasySave3.dll
  Unity.RenderPipelines.Core.Editor -> C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Core.Editor.dll
  Hecton8.Input -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Input.dll
  GPUInstancer -> C:\hades\Hecton8\Temp\bin\Debug\GPUInstancer.dll
  ShapesRuntime -> C:\hades\Hecton8\Temp\bin\Debug\ShapesRuntime.dll
  VolumetricLightBeam -> C:\hades\Hecton8\Temp\bin\Debug\VolumetricLightBeam.dll
  Unity.ShaderGraph.Editor -> C:\hades\Hecton8\Temp\bin\Debug\Unity.ShaderGraph.Editor.dll
  Crest -> C:\hades\Hecton8\Temp\bin\Debug\Crest.dll
  WaveHarmonic.Crest.Shared -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.Shared.dll
  WaveHarmonic.Crest.Shared.Editor -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.Shared.Editor.dll
  WaveHarmonic.Crest -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.dll
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1142,23): warning CS0414: The field 'ProceduralWreckGenerator.buriedWreckCutFraction' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1138,21): warning CS0414: The field 'ProceduralWreckGenerator.maxDebrisRecords' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1146,23): warning CS0414: The field 'ProceduralWreckGenerator.wreckInteriorCutHalfHeight' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
  Hecton8.Core -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll

Build succeeded.

C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1142,23): warning CS0414: The field 'ProceduralWreckGenerator.buriedWreckCutFraction' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1138,21): warning CS0414: The field 'ProceduralWreckGenerator.maxDebrisRecords' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(1146,23): warning CS0414: The field 'ProceduralWreckGenerator.wreckInteriorCutHalfHeight' is assigned but its value is never used [C:\hades\Hecton8\Hecton8.Core.csproj]
    3 Warning(s)
    0 Error(s)

Time Elapsed 00:02:08.35
```

Final build output after OMEGA/concurrent edits:
```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  Hecton8.Input.Generated -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Input.Generated.dll
  Hecton8.World.Contracts -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.World.Contracts.dll
  EasySave3 -> C:\hades\Hecton8\Temp\bin\Debug\EasySave3.dll
  Hecton8.Bootstrap.Contracts -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Bootstrap.Contracts.dll
  Unity.RenderPipelines.Universal.Runtime -> C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
  Unity.RenderPipelines.Core.Editor -> C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Core.Editor.dll
  Hecton8.Input -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Input.dll
  ShapesRuntime -> C:\hades\Hecton8\Temp\bin\Debug\ShapesRuntime.dll
  WaveHarmonic.Crest.Shared -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.Shared.dll
  GPUInstancer -> C:\hades\Hecton8\Temp\bin\Debug\GPUInstancer.dll
  VolumetricLightBeam -> C:\hades\Hecton8\Temp\bin\Debug\VolumetricLightBeam.dll
  Crest -> C:\hades\Hecton8\Temp\bin\Debug\Crest.dll
  Unity.ShaderGraph.Editor -> C:\hades\Hecton8\Temp\bin\Debug\Unity.ShaderGraph.Editor.dll
  WaveHarmonic.Crest.Shared.Editor -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.Shared.Editor.dll
  WaveHarmonic.Crest -> C:\hades\Hecton8\Temp\bin\Debug\WaveHarmonic.Crest.dll
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(3049,54): error CS0104: 'InteractionSignal' is an ambiguous reference between 'Hecton8.Gameplay.InteractionSignal' and 'Hecton8.Interaction.InteractionSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(4666,47): error CS0104: 'InteractionSignal' is an ambiguous reference between 'Hecton8.Gameplay.InteractionSignal' and 'Hecton8.Interaction.InteractionSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(4653,68): error CS0535: 'WreckIntegritySignalProxy' does not implement interface member 'IInteractionSignalConsumer.ApplyInteractionSignal(in InteractionSignal, Vector3)' [C:\hades\Hecton8\Hecton8.Core.csproj]

Build FAILED.

C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(3049,54): error CS0104: 'InteractionSignal' is an ambiguous reference between 'Hecton8.Gameplay.InteractionSignal' and 'Hecton8.Interaction.InteractionSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(4666,47): error CS0104: 'InteractionSignal' is an ambiguous reference between 'Hecton8.Gameplay.InteractionSignal' and 'Hecton8.Interaction.InteractionSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
C:\hades\Hecton8\Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs(4653,68): error CS0535: 'WreckIntegritySignalProxy' does not implement interface member 'IInteractionSignalConsumer.ApplyInteractionSignal(in InteractionSignal, Vector3)' [C:\hades\Hecton8\Hecton8.Core.csproj]
    0 Warning(s)
    3 Error(s)

Time Elapsed 00:00:10.18
```

Final Git diff evidence:
```text
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
Assets/_Project/Scripts/Core/BootstrapContracts/InputBindingServiceContracts.cs
Assets/_Project/Scripts/Core/GlobalRegistry.cs
Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs
Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs
Assets/_Project/Scripts/Core/RuntimeWatchdog.cs
Assets/_Project/Scripts/CrashTelemetryBuffer.cs
Assets/_Project/Scripts/Optimization/HardwareProfiler.cs
Assets/_Project/Scripts/SaveBinaryStorage.cs
Assets/_Project/Scripts/Narrative/LoreEncyclopediaLazyProxy.cs (new)
Docs/Tasks/Status_CORE_BOOTSTRAP.md (new)
Docs/AgentLogs/Rationale_CORE_BOOTSTRAP.md (new)
Docs/AgentLogs/LOG_CORE_BOOTSTRAP.md (new)
```
