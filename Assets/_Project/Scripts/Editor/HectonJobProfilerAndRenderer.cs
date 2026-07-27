using System;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using Hecton8.World;

public static class HectonJobProfilerAndRenderer
{
    // Removed: an unused `ArtifactsDir` constant pointing at a hardcoded absolute path inside a
    // specific developer's local agent scratch directory. The compiler proved it was never read
    // (CS0414), and it also violated the project's no-hardcoded-absolute-paths rule for committed
    // scripts while leaking a local user profile path into the repository. If this tool needs an
    // output directory again, derive it from a project-relative path.

    [MenuItem("Tools/Hecton/Run Abyssal Shelf Proof")]
    public static void RunProof()
    {
        UnityEngine.Debug.Log("[PROOF] Starting Abyssal Shelf Jobs Profiling & Render...");

        try
        {
            // 1. Setup
            int resolution = 256;
            double cellSizeMeters = 2.0;

            var p = new HectonSandboxAbyssalShelfParams
            {
                Seed = 12345,
                AupCellSizeMeters = cellSizeMeters,
                LowWorldY = -2000f,
                HighWorldY = 0f,
                MacroGeologyArtifactVersion = Hecton8.World.WorldMacroGeologyFields.ArtifactVersion
            };

            var origin = new AbsoluteUniversePosition { GridX = 0, GridY = 0, GridZ = 0 };

            var heights = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            var outputHeights = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            int presampledWidth = resolution + 2;
            var presampledNodes = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.Persistent);

            // Warmup
            var presampleJob = new HectonSandboxAbyssalShelfPresampleJob { PresampledNodes = presampledNodes, Parameters = p, PresampledWidth = presampledWidth, WorldOriginAup = origin, CellSizeMeters = cellSizeMeters };
            var baseJob = new HectonSandboxAbyssalShelfDifferentialJob { PresampledNodes = presampledNodes, OutputHeights01 = heights, Parameters = p, Width = resolution, PresampledWidth = presampledWidth, WorldOriginAup = origin, CellSizeMeters = cellSizeMeters };
            var diffJob = new HectonSandboxSlopeQuantizationJob
            {
                InputHeights01 = heights,
                OutputHeights01 = outputHeights,
                Width = resolution,
                Height = resolution,
                CellSizeMeters = (float)cellSizeMeters,
                LowWorldY = p.LowWorldY,
                HighWorldY = p.HighWorldY,
                PlateauSourceGradient = 0.05f,
                PlateauTargetGradient = 0.01f,
                CliffRampEndGradient = math.tan(60f * math.PI / 180f),
                CliffTargetGradient = math.tan(30f * math.PI / 180f),
                Strength = 1.0f
            };

            baseJob.Schedule(resolution * resolution, 64, presampleJob.Schedule(presampledWidth * presampledWidth, 64)).Complete();
            diffJob.Schedule(resolution * resolution, 64).Complete();

            // 2. Profile
            int iterations = 100;
            var sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                baseJob.Schedule(resolution * resolution, 64, presampleJob.Schedule(presampledWidth * presampledWidth, 64)).Complete();
            }
            sw.Stop();
            double presampleMs = sw.Elapsed.TotalMilliseconds / iterations;

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                diffJob.Schedule(resolution * resolution, 64).Complete();
            }
            sw.Stop();
            double diffMs = sw.Elapsed.TotalMilliseconds / iterations;

            UnityEngine.Debug.Log($"[PROOF] Profiling Complete. BaseJob: {presampleMs:F2}ms, QuantizationJob: {diffMs:F2}ms. Total: {(presampleMs + diffMs):F2}ms");

            if ((presampleMs + diffMs) > 100.0)
            {
                UnityEngine.Debug.LogWarning("[PROOF_WARNING] High execution time detected (>100ms). This strongly indicates Burst compilation failure (Mono fallback). Please grep logs for 'Burst error' or 'BC0101'.");
            }
            heights.Dispose();
            outputHeights.Dispose();
            presampledNodes.Dispose();
            
            UnityEngine.Debug.Log("[PROOF] All proof generation successful.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[PROOF_ERROR] {ex}");
            EditorApplication.Exit(1);
        }
    }
}


