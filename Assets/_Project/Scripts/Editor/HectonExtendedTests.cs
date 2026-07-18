using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using Hecton8.World;

public static class HectonExtendedTests
{
    [MenuItem("Tools/Hecton/Run Abyssal Shelf Extended Tests")]
    public static void RunExtendedTests()
    {
        UnityEngine.Debug.Log("[PROOF] Starting Extended Tests (Seams & GC)...");
        
        try
        {
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

            var originA = new AbsoluteUniversePosition { GridX = 0, GridY = 0, GridZ = 0 };
            
            var originB = new AbsoluteUniversePosition { GridX = 0, GridY = 0, GridZ = 0, LocalX = (resolution - 1) * (float)cellSizeMeters };
            var originC = new AbsoluteUniversePosition { GridX = 0, GridY = 0, GridZ = 0, LocalZ = (resolution - 1) * (float)cellSizeMeters };

            var heightsA = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            var heightsB = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            var heightsC = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            
            int presampledWidth = resolution + 2;
            var presampledNodesA = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.Persistent);
            var presampledNodesB = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.Persistent);
            var presampledNodesC = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.Persistent);

            // Generate A
            new HectonSandboxAbyssalShelfDifferentialJob { 
                PresampledNodes = presampledNodesA, OutputHeights01 = heightsA, Parameters = p, Width = resolution, PresampledWidth = presampledWidth, WorldOriginAup = originA, CellSizeMeters = cellSizeMeters 
            }.Schedule(resolution * resolution, 64, 
                new HectonSandboxAbyssalShelfPresampleJob { PresampledNodes = presampledNodesA, Parameters = p, PresampledWidth = presampledWidth, WorldOriginAup = originA, CellSizeMeters = cellSizeMeters }.Schedule(presampledWidth * presampledWidth, 64)
            ).Complete();

            // Generate B
            new HectonSandboxAbyssalShelfDifferentialJob { 
                PresampledNodes = presampledNodesB, OutputHeights01 = heightsB, Parameters = p, Width = resolution, PresampledWidth = presampledWidth, WorldOriginAup = originB, CellSizeMeters = cellSizeMeters 
            }.Schedule(resolution * resolution, 64, 
                new HectonSandboxAbyssalShelfPresampleJob { PresampledNodes = presampledNodesB, Parameters = p, PresampledWidth = presampledWidth, WorldOriginAup = originB, CellSizeMeters = cellSizeMeters }.Schedule(presampledWidth * presampledWidth, 64)
            ).Complete();

            // Generate C
            new HectonSandboxAbyssalShelfDifferentialJob { 
                PresampledNodes = presampledNodesC, OutputHeights01 = heightsC, Parameters = p, Width = resolution, PresampledWidth = presampledWidth, WorldOriginAup = originC, CellSizeMeters = cellSizeMeters 
            }.Schedule(resolution * resolution, 64, 
                new HectonSandboxAbyssalShelfPresampleJob { PresampledNodes = presampledNodesC, Parameters = p, PresampledWidth = presampledWidth, WorldOriginAup = originC, CellSizeMeters = cellSizeMeters }.Schedule(presampledWidth * presampledWidth, 64)
            ).Complete();

            float maxDeltaX = 0f;
            float sumDeltaX = 0f;
            for (int z = 0; z < resolution; z++) {
                float valA = heightsA[z * resolution + (resolution - 1)] * 2000f; 
                float valB = heightsB[z * resolution + 0] * 2000f;
                float delta = math.abs(valA - valB);
                if (delta > maxDeltaX) maxDeltaX = delta;
                sumDeltaX += delta;
            }
            
            float maxDeltaZ = 0f;
            float sumDeltaZ = 0f;
            for (int x = 0; x < resolution; x++) {
                float valA = heightsA[(resolution - 1) * resolution + x] * 2000f;
                float valC = heightsC[0 * resolution + x] * 2000f;
                float delta = math.abs(valA - valC);
                if (delta > maxDeltaZ) maxDeltaZ = delta;
                sumDeltaZ += delta;
            }
            
            UnityEngine.Debug.Log($"[SEAMS_TEST] Edge X Delta: Max {maxDeltaX:F6}m, Mean {(sumDeltaX / resolution):F6}m");
            UnityEngine.Debug.Log($"[SEAMS_TEST] Edge Z Delta: Max {maxDeltaZ:F6}m, Mean {(sumDeltaZ / resolution):F6}m");

            heightsA.Dispose(); heightsB.Dispose(); heightsC.Dispose();
            presampledNodesA.Dispose(); presampledNodesB.Dispose(); presampledNodesC.Dispose();

            // GC Test
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            long startBytes = System.GC.GetTotalMemory(false);
            
            var heightsGC = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
            var presampledNodesGC = new NativeArray<PresampledMacroNode>(presampledWidth * presampledWidth, Allocator.Persistent);
            
            new HectonSandboxAbyssalShelfDifferentialJob { 
                PresampledNodes = presampledNodesGC, OutputHeights01 = heightsGC, Parameters = p, Width = resolution, PresampledWidth = presampledWidth, WorldOriginAup = originA, CellSizeMeters = cellSizeMeters 
            }.Schedule(resolution * resolution, 64, 
                new HectonSandboxAbyssalShelfPresampleJob { PresampledNodes = presampledNodesGC, Parameters = p, PresampledWidth = presampledWidth, WorldOriginAup = originA, CellSizeMeters = cellSizeMeters }.Schedule(presampledWidth * presampledWidth, 64)
            ).Complete();
            
            heightsGC.Dispose();
            presampledNodesGC.Dispose();
            
            long endBytes = System.GC.GetTotalMemory(false);
            long diffBytes = endBytes - startBytes;
            
            UnityEngine.Debug.Log($"[GC_TEST] Managed allocations during tile generation: {diffBytes} bytes");
            
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[PROOF_ERROR] {ex}");
            EditorApplication.Exit(1);
        }
    }
}
