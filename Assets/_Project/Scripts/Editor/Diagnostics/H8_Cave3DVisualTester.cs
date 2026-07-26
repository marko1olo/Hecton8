using System;
using System.IO;
using Hecton8.World;
using Hecton8.World.VoxelSurfaceNets;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// Isolated 3D Cave Diagnostic Harness.
    /// Evaluates 3D procedural cave SDF density fields, seals cavern terrarium borders, extracts 3D SurfaceNets geometry,
    /// bakes Analytic SDF Raymarched Ambient Occlusion into Vertex Colors, and renders 4 URP GPU visual verification passes to AI_Diagnostics/cave_render/.
    /// </summary>
    public static class H8_Cave3DVisualTester
    {
        private const int ChunkRes = VoxelSurfaceNetsConstants.ChunkResolution; // 32
        private const int DensityRes = VoxelSurfaceNetsConstants.DensityResolution; // 34
        private const float VoxelPitch = 0.5f; // 16m x 16m x 16m volume
        private const string OutputFolder = "AI_Diagnostics/cave_render";

        [MenuItem("Hecton-8/Diagnostics/Run 3D Cave Visual Tester")]
        public static void Run()
        {
            Debug.Log("[H8_Cave3DVisualTester] Starting Isolated 3D Cave Visual Diagnostic Pipeline (Phase 14 Analytic SDF Normals & Wet PBR Caves)...");

            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }

            // --------------------------------------------------------------------------------
            // STEP 1: EVALUATE 3D CAVE SDF DENSITY FIELD IN ISOLATION & APPLY TERRARIUM SEAL
            // --------------------------------------------------------------------------------
            int totalVoxels = DensityRes * DensityRes * DensityRes;
            NativeArray<float> sdfGrid = new NativeArray<float>(totalVoxels, Allocator.Persistent);
            NativeArray<sbyte> quantizedDensities = new NativeArray<sbyte>(totalVoxels, Allocator.Persistent);

            // Initialize background terrain density (solid rock depth = 25m below surface)
            for (int i = 0; i < totalVoxels; i++)
            {
                sdfGrid[i] = 25.0f;
            }

            // Set up 3D Cave Carve Job
            ProceduralCaveSdfCarveJob carveJob = new ProceduralCaveSdfCarveJob
            {
                Sdf = sdfGrid,
                SdfWidth = DensityRes,
                SdfHeight = DensityRes,
                SdfDepth = DensityRes,
                VoxelSizeMeters = VoxelPitch,
                SdfOriginAup = new double3(24000.0, -500.0, -18000.0), // Deep rock canyon sector
                PrimaryFrequency = 0.015f,
                SecondaryFrequency = 0.012f,
                CarveStrengthMeters = 16.0f,
                CaveThreshold = 0.42f,
                MaxCrustDepthMeters = 100.0f,
                SurfaceProtectionMeters = 1.0f,
                StrataLayerThicknessMeters = 8.0f,
                StrataShelvingStrength = 0.22f,
                WorldSeed = 0xC0A55123u,
                CaveEntranceMask = new NativeArray<float>(1, Allocator.Persistent),
                BrinePoolMask = new NativeArray<float>(1, Allocator.Persistent),
                SteepRockMask = new NativeArray<float>(1, Allocator.Persistent)
            };

            JobHandle carveHandle = carveJob.Schedule(totalVoxels, 64);
            carveHandle.Complete();

            // SEAL THE BORDERS: Smooth distance penalty at borders to guarantee 100% enclosed cavern terrarium
            int slice = DensityRes * DensityRes;
            for (int z = 0; z < DensityRes; z++)
            {
                for (int y = 0; y < DensityRes; y++)
                {
                    for (int x = 0; x < DensityRes; x++)
                    {
                        int idx = x + y * DensityRes + z * slice;
                        if (x <= 3 || x >= DensityRes - 4 || y <= 3 || y >= DensityRes - 4 || z <= 3 || z >= DensityRes - 4)
                        {
                            sdfGrid[idx] = Mathf.Max(sdfGrid[idx], 8.0f);
                        }
                    }
                }
            }

            // Quantize densities to sbyte [-127, 127]
            float minSdf = float.MaxValue;
            float maxSdf = float.MinValue;
            int nanCount = 0;

            for (int i = 0; i < totalVoxels; i++)
            {
                float val = sdfGrid[i];
                if (!math.isfinite(val))
                {
                    nanCount++;
                    val = 25.0f;
                }
                minSdf = math.min(minSdf, val);
                maxSdf = math.max(maxSdf, val);

                int packed = (int)math.round(math.clamp(val * (1.0f / VoxelPitch) * 127.0f, -127.0f, 127.0f));
                quantizedDensities[i] = (sbyte)packed;
            }

            // --------------------------------------------------------------------------------
            // STEP 1.5: RESOLVE TRUE INTERIOR CAMERA CENTROID IN SEALED CAVERN
            // --------------------------------------------------------------------------------
            int centerVx = DensityRes / 2; // 17
            int centerVy = DensityRes / 2; // 17
            int centerVz = DensityRes / 2; // 17
            int centerIndex = centerVx + centerVy * DensityRes + centerVz * slice;

            int cavityX = centerVx;
            int cavityY = centerVy;
            int cavityZ = centerVz;
            float bestCavitySdf = sdfGrid[centerIndex];

            float lowestVal = float.MaxValue;
            int bestIdx = centerIndex;

            for (int radius = 0; radius <= 10; radius++)
            {
                int minG = math.max(4, centerVx - radius);
                int maxG = math.min(DensityRes - 5, centerVx + radius);

                for (int z = minG; z <= maxG; z++)
                {
                    for (int y = minG; y <= maxG; y++)
                    {
                        for (int x = minG; x <= maxG; x++)
                        {
                            int idx = x + y * DensityRes + z * slice;
                            float val = sdfGrid[idx];
                            if (val < lowestVal)
                            {
                                lowestVal = val;
                                bestIdx = idx;
                            }
                        }
                    }
                }

                if (lowestVal < -1.0f) break;
            }

            if (lowestVal < float.MaxValue)
            {
                int cavityZ_found = bestIdx / slice;
                int rem_found = bestIdx - cavityZ_found * slice;
                int cavityY_found = rem_found / DensityRes;
                int cavityX_found = rem_found - cavityY_found * DensityRes;

                cavityX = cavityX_found;
                cavityY = cavityY_found;
                cavityZ = cavityZ_found;
                bestCavitySdf = lowestVal;
            }

            Vector3 cavityCenterPos = new Vector3(cavityX * VoxelPitch, cavityY * VoxelPitch, cavityZ * VoxelPitch);

            int cXp = Mathf.Clamp(cavityX + 1, 0, DensityRes - 1);
            int cXn = Mathf.Clamp(cavityX - 1, 0, DensityRes - 1);
            int cYp = Mathf.Clamp(cavityY + 1, 0, DensityRes - 1);
            int cYn = Mathf.Clamp(cavityY - 1, 0, DensityRes - 1);
            int cZp = Mathf.Clamp(cavityZ + 1, 0, DensityRes - 1);
            int cZn = Mathf.Clamp(cavityZ - 1, 0, DensityRes - 1);

            float gX = sdfGrid[cXp + cavityY * DensityRes + cavityZ * slice] - sdfGrid[cXn + cavityY * DensityRes + cavityZ * slice];
            float gY = sdfGrid[cavityX + cYp * DensityRes + cavityZ * slice] - sdfGrid[cavityX + cYn * DensityRes + cavityZ * slice];
            float gZ = sdfGrid[cavityX + cavityY * DensityRes + cZp * slice] - sdfGrid[cavityX + cavityY * DensityRes + cZn * slice];

            Vector3 sdfGradient = new Vector3(gX, gY, gZ).normalized;
            if (sdfGradient.sqrMagnitude < 0.0001f) sdfGradient = Vector3.forward;

            Vector3 tunnelDir = Vector3.Cross(sdfGradient, Vector3.up).normalized;
            if (tunnelDir.sqrMagnitude < 0.0001f) tunnelDir = Vector3.right;

            Debug.Log($"[H8_Cave3DVisualTester] SDF Evaluated. MinSdf={minSdf:F2}m, MaxSdf={maxSdf:F2}m, NaNs={nanCount}. True Interior Cavity Centroid at Grid ({cavityX}, {cavityY}, {cavityZ}) = {cavityCenterPos}, Sdf={bestCavitySdf:F2}m, TunnelDir={tunnelDir}");

            // --------------------------------------------------------------------------------
            // STEP 2: EXTRACT 3D MESH & BAKE ANALYTIC SDF RAYMARCHED AO (SMOOTH C1 CONTINUOUS)
            // --------------------------------------------------------------------------------
            NativeArray<VoxelVertexDTO> vertices = new NativeArray<VoxelVertexDTO>(VoxelSurfaceNetsConstants.MaxVertices, Allocator.Persistent);
            NativeArray<uint> indices = new NativeArray<uint>(VoxelSurfaceNetsConstants.MaxIndices, Allocator.Persistent);
            NativeArray<int> cellVertexMap = new NativeArray<int>(VoxelSurfaceNetsConstants.CellCount, Allocator.Persistent);
            NativeArray<ChunkMeshingStateDTO> states = new NativeArray<ChunkMeshingStateDTO>(1, Allocator.Persistent);
            NativeArray<VoxelMeshingTuningDTO> tuning = new NativeArray<VoxelMeshingTuningDTO>(1, Allocator.Persistent);
            NativeArray<VoxelMeshingTelemetryEntry> telemetry = new NativeArray<VoxelMeshingTelemetryEntry>(VoxelSurfaceNetsConstants.TelemetryFrames, Allocator.Persistent);
            NativeArray<int> telemetryCursor = new NativeArray<int>(1, Allocator.Persistent);
            NativeArray<float3> rawDebugVerts = new NativeArray<float3>(1, Allocator.Persistent);
            NativeArray<VoxelSurfaceIndirectArgsDTO> indirectArgs = new NativeArray<VoxelSurfaceIndirectArgsDTO>(1, Allocator.Persistent);

            tuning[0] = VoxelSurfaceNetsDefaults.BuildDefaultTuning();

            ChunkMeshingStateDTO chunkState = default;
            chunkState.VoxelSize = VoxelPitch;
            chunkState.ChunkHash = 0x87CA5Eu;
            states[0] = chunkState;

            SurfaceNetExtractionJob extractionJob = new SurfaceNetExtractionJob
            {
                Densities = quantizedDensities,
                Vertices = vertices,
                Indices = indices,
                CellVertexMap = cellVertexMap,
                States = states,
                Tuning = tuning,
                SurfaceEdgeMasks = default,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                RawDebugVertices = rawDebugVerts,
                IndirectArgs = indirectArgs,
                ChunkIndex = 0,
                Frame = 1
            };

            extractionJob.Execute();

            ChunkMeshingStateDTO resultState = states[0];
            int vertexCount = resultState.VertexCount;
            int indexCount = resultState.IndexCount;
            int triangleCount = indexCount / 3;

            Debug.Log($"[H8_Cave3DVisualTester] Mesh Extracted. Vertices={vertexCount}, Indices={indexCount}, Triangles={triangleCount}");

            Mesh caveMesh = new Mesh();
            caveMesh.name = "H8_IsolatedCaveMesh";

            Vector3[] meshVerts = new Vector3[vertexCount];
            Vector3[] meshNormals = new Vector3[vertexCount];
            Color[] meshColors = new Color[vertexCount];
            Vector2[] meshUvs = new Vector2[vertexCount];
            int[] meshTris = new int[indexCount];

            if (vertexCount > 0 && indexCount > 0)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    VoxelVertexDTO v = vertices[i];
                    Vector3 vPos = v.Position;
                    meshVerts[i] = vPos;

                    // ANALYTICAL SDF GRADIENT NORMALS: N = normalize(nabla D(x,y,z))
                    // Central differences on the trilinear SDF field for perfectly smooth C1-continuous lighting
                    const float eps = 0.35f;
                    float dX = SampleSdfTrilinear(sdfGrid, vPos + new Vector3(eps, 0, 0)) - SampleSdfTrilinear(sdfGrid, vPos - new Vector3(eps, 0, 0));
                    float dY = SampleSdfTrilinear(sdfGrid, vPos + new Vector3(0, eps, 0)) - SampleSdfTrilinear(sdfGrid, vPos - new Vector3(0, eps, 0));
                    float dZ = SampleSdfTrilinear(sdfGrid, vPos + new Vector3(0, 0, eps)) - SampleSdfTrilinear(sdfGrid, vPos - new Vector3(0, 0, eps));
                    Vector3 vNorm = new Vector3(dX, dY, dZ).normalized;
                    if (vNorm.sqrMagnitude < 0.0001f)
                    {
                        // Fallback to packed DTO normal if gradient is degenerate
                        uint np = v.NormalPacked;
                        float nx = ((np & 1023u) * (1.0f / 1023.0f) * 2.0f) - 1.0f;
                        float ny = (((np >> 10) & 1023u) * (1.0f / 1023.0f) * 2.0f) - 1.0f;
                        float nz = (((np >> 20) & 1023u) * (1.0f / 1023.0f) * 2.0f) - 1.0f;
                        vNorm = new Vector3(nx, ny, nz).normalized;
                    }
                    meshNormals[i] = vNorm;

                    // ANALYTIC SDF RAYMARCHED AO (C1-CONTINUOUS INFINITELY SMOOTH SHADOWS)
                    float rayAo = 1.0f;
                    float aoStepScale = 0.35f;

                    for (int j = 1; j <= 5; j++)
                    {
                        float dist = j * aoStepScale;
                        Vector3 raySamplePos = vPos + vNorm * dist;
                        float sampleD = SampleSdfTrilinear(sdfGrid, raySamplePos);

                        if (sampleD < dist)
                        {
                            float delta = dist - sampleD;
                            rayAo -= delta * (0.45f / j);
                        }
                    }

                    float finalAo = Mathf.Clamp(rayAo, 0.12f, 1.0f);
                    meshColors[i] = new Color(finalAo, finalAo, finalAo, 1.0f);
                    meshUvs[i] = v.UV;
                }

                for (int i = 0; i < indexCount; i++)
                {
                    meshTris[i] = (int)indices[i];
                }

                caveMesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                caveMesh.vertices = meshVerts;
                caveMesh.normals = meshNormals;
                caveMesh.colors = meshColors;
                caveMesh.uv = meshUvs;
                caveMesh.triangles = meshTris;
                caveMesh.RecalculateBounds();
                caveMesh.RecalculateTangents();
            }

            // Write raw report stats
            string reportText = $"H8 3D ISOLATED CAVE DIAGNOSTIC REPORT (PHASE 14 ANALYTIC NORMALS WET PBR)\n" +
                                $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                $"Grid Resolution: {ChunkRes}x{ChunkRes}x{ChunkRes} (Voxel Pitch: {VoxelPitch}m)\n" +
                                $"SDF Bounds: Min={minSdf:F3}m, Max={maxSdf:F3}m, NaNs={nanCount}\n" +
                                $"Cavity Centroid Grid Index: ({cavityX}, {cavityY}, {cavityZ})\n" +
                                $"Mesh Stats: Vertices={vertexCount}, Triangles={triangleCount}, Indices={indexCount}\n" +
                                $"Mesh Bounds Center: {caveMesh.bounds.center}\n" +
                                $"Mesh Bounds Extents: {caveMesh.bounds.extents}\n";

            File.WriteAllText(Path.Combine(OutputFolder, "cave_report.txt"), reportText);

            // --------------------------------------------------------------------------------
            // STEP 3: EXECUTE 4 DIAGNOSTIC RENDER PASSES (TRUE URP GPU RENDERING)
            // --------------------------------------------------------------------------------

            ComputeBuffer dummyProbeBuffer = null;
            try
            {
                dummyProbeBuffer = new ComputeBuffer(1, 64);
                Shader.SetGlobalBuffer("_H8CustomLightProbeGrid", dummyProbeBuffer);
            }
            catch { }

            // PASS 1: Cave_Debug_SdfSlices.png (3-Panel XY, XZ, YZ Heatmap Centered at Cavity)
            Generate3PanelSdfSliceHeatmap(sdfGrid, cavityX, cavityY, cavityZ, Path.Combine(OutputFolder, "Cave_Debug_SdfSlices.png"));

            // PASS 2: Cave_Render_Interior_Tunnel.png (Camera INSIDE cavern hall looking down the tunnel)
            Vector3 tunnelCamPos = cavityCenterPos - tunnelDir * 0.4f;
            Vector3 tunnelLookAt = cavityCenterPos + tunnelDir * 3.5f;
            ExecuteUrpGpuRenderPass(caveMesh, meshVerts, meshNormals, meshColors, meshTris, sdfGrid, tunnelCamPos, tunnelLookAt, 85.0f, 1024, 1024, 0, Path.Combine(OutputFolder, "Cave_Render_Interior_Tunnel.png"));

            // PASS 3: Cave_Render_LargeChamber.png (Wide chamber view inside cavern hall with 95° FOV)
            Vector3 chamberCamPos = cavityCenterPos + sdfGradient * 0.6f - tunnelDir * 0.3f;
            Vector3 chamberLookAt = cavityCenterPos - sdfGradient * 2.5f + tunnelDir * 1.5f;
            ExecuteUrpGpuRenderPass(caveMesh, meshVerts, meshNormals, meshColors, meshTris, sdfGrid, chamberCamPos, chamberLookAt, 95.0f, 1024, 1024, 1, Path.Combine(OutputFolder, "Cave_Render_LargeChamber.png"));

            // PASS 4: Cave_Debug_VoxelAO.png (Analytic Raymarched AO / Crevice Shadow Pass)
            Vector3 aoCamPos = cavityCenterPos - tunnelDir * 0.5f + Vector3.up * 0.3f;
            Vector3 aoLookAt = cavityCenterPos + tunnelDir * 2.8f;
            ExecuteUrpGpuRenderPass(caveMesh, meshVerts, meshNormals, meshColors, meshTris, sdfGrid, aoCamPos, aoLookAt, 80.0f, 1024, 1024, 2, Path.Combine(OutputFolder, "Cave_Debug_VoxelAO.png"));

            if (dummyProbeBuffer != null)
            {
                dummyProbeBuffer.Dispose();
            }

            // Dispose Native Arrays
            sdfGrid.Dispose();
            carveJob.CaveEntranceMask.Dispose();
            carveJob.BrinePoolMask.Dispose();
            carveJob.SteepRockMask.Dispose();
            quantizedDensities.Dispose();
            vertices.Dispose();
            indices.Dispose();
            cellVertexMap.Dispose();
            states.Dispose();
            tuning.Dispose();
            telemetry.Dispose();
            telemetryCursor.Dispose();
            rawDebugVerts.Dispose();
            indirectArgs.Dispose();

            Debug.Log("[H8_Cave3DVisualTester] Completed Phase 14 Diagnostic Pipeline. All 4 URP GPU PNG passes saved to AI_Diagnostics/cave_render/");
        }

        private static float SampleSdfTrilinear(NativeArray<float> sdfGrid, Vector3 pos)
        {
            float gX = pos.x * (1.0f / VoxelPitch);
            float gY = pos.y * (1.0f / VoxelPitch);
            float gZ = pos.z * (1.0f / VoxelPitch);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(gX), 0, DensityRes - 2);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(gY), 0, DensityRes - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(gZ), 0, DensityRes - 2);

            float fx = Mathf.Clamp01(gX - x0);
            float fy = Mathf.Clamp01(gY - y0);
            float fz = Mathf.Clamp01(gZ - z0);

            int s = DensityRes * DensityRes;
            float c000 = sdfGrid[x0 + y0 * DensityRes + z0 * s];
            float c100 = sdfGrid[(x0 + 1) + y0 * DensityRes + z0 * s];
            float c010 = sdfGrid[x0 + (y0 + 1) * DensityRes + z0 * s];
            float c110 = sdfGrid[(x0 + 1) + (y0 + 1) * DensityRes + z0 * s];
            float c001 = sdfGrid[x0 + y0 * DensityRes + (z0 + 1) * s];
            float c101 = sdfGrid[(x0 + 1) + y0 * DensityRes + (z0 + 1) * s];
            float c011 = sdfGrid[x0 + (y0 + 1) * DensityRes + (z0 + 1) * s];
            float c111 = sdfGrid[(x0 + 1) + (y0 + 1) * DensityRes + (z0 + 1) * s];

            float c00 = Mathf.Lerp(c000, c100, fx);
            float c10 = Mathf.Lerp(c010, c110, fx);
            float c01 = Mathf.Lerp(c001, c101, fx);
            float c11 = Mathf.Lerp(c011, c111, fx);

            float c0 = Mathf.Lerp(c00, c10, fy);
            float c1 = Mathf.Lerp(c01, c11, fy);

            return Mathf.Lerp(c0, c1, fz);
        }

        /// <summary>
        /// C# port of 3D Simplex Noise (Ashima Arts / Stefan Gustavson).
        /// Matches HectonSimplexNoise3D in Hecton_AbyssalVoxelRock.shader exactly.
        /// </summary>
        private static float SimplexNoise3D(Vector3 v)
        {
            const float C1 = 1.0f / 6.0f;
            const float C2 = 1.0f / 3.0f;

            // Skew the input space
            float s = (v.x + v.y + v.z) * C2;
            Vector3 i = new Vector3(Mathf.Floor(v.x + s), Mathf.Floor(v.y + s), Mathf.Floor(v.z + s));
            float t = (i.x + i.y + i.z) * C1;
            Vector3 x0 = new Vector3(v.x - i.x + t, v.y - i.y + t, v.z - i.z + t);

            // Determine which simplex we are in
            Vector3 i1, i2;
            if (x0.x >= x0.y)
            {
                if (x0.y >= x0.z) { i1 = new Vector3(1, 0, 0); i2 = new Vector3(1, 1, 0); }
                else if (x0.x >= x0.z) { i1 = new Vector3(1, 0, 0); i2 = new Vector3(1, 0, 1); }
                else { i1 = new Vector3(0, 0, 1); i2 = new Vector3(1, 0, 1); }
            }
            else
            {
                if (x0.y < x0.z) { i1 = new Vector3(0, 0, 1); i2 = new Vector3(0, 1, 1); }
                else if (x0.x < x0.z) { i1 = new Vector3(0, 1, 0); i2 = new Vector3(0, 1, 1); }
                else { i1 = new Vector3(0, 1, 0); i2 = new Vector3(1, 1, 0); }
            }

            Vector3 x1 = new Vector3(x0.x - i1.x + C1, x0.y - i1.y + C1, x0.z - i1.z + C1);
            Vector3 x2 = new Vector3(x0.x - i2.x + C2, x0.y - i2.y + C2, x0.z - i2.z + C2);
            Vector3 x3 = new Vector3(x0.x - 0.5f, x0.y - 0.5f, x0.z - 0.5f);

            // Wrap to [0..289) for hashing
            float ix = Mod289(i.x);
            float iy = Mod289(i.y);
            float iz = Mod289(i.z);

            float p0 = Permute(Permute(Permute(iz) + iy) + ix);
            float p1 = Permute(Permute(Permute(iz + i1.z) + iy + i1.y) + ix + i1.x);
            float p2 = Permute(Permute(Permute(iz + i2.z) + iy + i2.y) + ix + i2.x);
            float p3 = Permute(Permute(Permute(iz + 1.0f) + iy + 1.0f) + ix + 1.0f);

            // Gradients
            Vector3 g0 = Grad3(p0);
            Vector3 g1 = Grad3(p1);
            Vector3 g2 = Grad3(p2);
            Vector3 g3 = Grad3(p3);

            // Mix contributions
            float t0 = 0.6f - Vector3.Dot(x0, x0);
            float t1 = 0.6f - Vector3.Dot(x1, x1);
            float t2 = 0.6f - Vector3.Dot(x2, x2);
            float t3 = 0.6f - Vector3.Dot(x3, x3);

            float n0 = t0 < 0 ? 0f : t0 * t0 * t0 * t0 * Vector3.Dot(g0, x0);
            float n1 = t1 < 0 ? 0f : t1 * t1 * t1 * t1 * Vector3.Dot(g1, x1);
            float n2 = t2 < 0 ? 0f : t2 * t2 * t2 * t2 * Vector3.Dot(g2, x2);
            float n3 = t3 < 0 ? 0f : t3 * t3 * t3 * t3 * Vector3.Dot(g3, x3);

            return 42.0f * (n0 + n1 + n2 + n3);
        }

        private static float Mod289(float x) { return x - Mathf.Floor(x * (1.0f / 289.0f)) * 289.0f; }
        private static float Permute(float x) { return Mod289(((x * 34.0f) + 1.0f) * x); }

        private static Vector3 Grad3(float hash)
        {
            float h = Mod289(hash) % 12.0f;
            float u = h < 8.0f ? (h < 4.0f ? 1.0f : -1.0f) : (h < 10.0f ? 1.0f : -1.0f);
            float v2 = h < 4.0f ? (h < 2.0f ? 1.0f : -1.0f) : (h < 8.0f ? (h < 6.0f ? 1.0f : -1.0f) : 0.0f);
            // Simple gradient selection based on hash
            float hf = Mathf.Floor(h);
            float gx = (hf % 3.0f < 1.0f) ? u : 0.0f;
            float gy = (hf % 3.0f >= 1.0f && hf % 3.0f < 2.0f) ? u : ((hf % 3.0f < 1.0f) ? v2 : 0.0f);
            float gz = (hf % 3.0f >= 2.0f) ? u : v2;
            return new Vector3(gx, gy, gz);
        }


        private static void ExecuteUrpGpuRenderPass(
            Mesh caveMesh,
            Vector3[] verts,
            Vector3[] normals,
            Color[] colors,
            int[] tris,
            NativeArray<float> sdfGrid,
            Vector3 camPos,
            Vector3 camTarget,
            float fov,
            int width,
            int height,
            int passMode,
            string outputPath)
        {
            Texture2D outputTex = new Texture2D(width, height, TextureFormat.RGB24, false);

            Shader rockShader = null;
            if (passMode == 2)
            {
                // Unlit Vertex AO pass rendered on GPU via vertex colors
                rockShader = Shader.Find("Unlit/Color");
                if (rockShader == null) rockShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            else
            {
                rockShader = Shader.Find("Hecton8/Environment/Hecton_AbyssalVoxelRock");
                if (rockShader == null) rockShader = Shader.Find("Universal Render Pipeline/Lit");
                if (rockShader == null) rockShader = Shader.Find("Standard");
            }


            GameObject meshGo = new GameObject("H8_CaveDiagnosticMesh");
            MeshFilter mf = meshGo.AddComponent<MeshFilter>();
            MeshRenderer mr = meshGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = caveMesh;

            Material rockMat = new Material(rockShader);
            // WET ABYSSAL PBR: configure material for wet cave rock specular highlights
            rockMat.SetFloat("_Smoothness", 0.15f);
            rockMat.SetFloat("_Metallic", 0.0f);
            rockMat.SetFloat("_OcclusionStrength", 1.0f);
            rockMat.SetColor("_Instance_Color", new Color(0.85f, 0.88f, 0.92f, 1.0f));
            Color clearColor = new Color(0.04f, 0.06f, 0.09f, 1.0f);
            mr.sharedMaterial = rockMat;

            // PRIMARY LIGHT: Point light at camera position for wet specular highlights on cave walls
            GameObject lightGo = new GameObject("H8_CaveDiagnosticLight");
            Light pointLight = lightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(0.90f, 0.95f, 1.0f);
            pointLight.intensity = 8.0f;
            pointLight.range = 30.0f;
            lightGo.transform.position = camPos;

            // FILL LIGHT: Dim directional light for ambient fill
            GameObject fillLightGo = new GameObject("H8_CaveDiagnosticFillLight");
            Light fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.5f, 0.6f, 0.75f);
            fillLight.intensity = 0.3f;
            fillLightGo.transform.rotation = Quaternion.LookRotation((camTarget - camPos).normalized);

            GameObject camGo = new GameObject("H8_CaveDiagnosticCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clearColor;
            cam.transform.position = camPos;
            cam.transform.LookAt(camTarget);

            // UniversalAdditionalCameraData binding for URP GPU execution
            UniversalAdditionalCameraData camData = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderShadows = true;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            try
            {
                cam.Render();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[H8_Cave3DVisualTester] GPU Camera.Render() failed in batchmode: {ex.Message}. Software rasterizer fallback is PURGED.");
                throw;
            }

            RenderTexture.active = rt;
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();

            Color sampleP1 = outputTex.GetPixel(width / 4, height / 4);
            Color sampleP2 = outputTex.GetPixel(width / 2, height / 2);
            Color sampleP3 = outputTex.GetPixel(3 * width / 4, 3 * height / 4);

            bool isBackgroundOnly = MathAbsDiff(sampleP1, clearColor) < 0.02f &&
                                    MathAbsDiff(sampleP2, clearColor) < 0.02f &&
                                    MathAbsDiff(sampleP3, clearColor) < 0.02f;

            if (isBackgroundOnly)
            {
                Debug.LogError($"[H8_Cave3DVisualTester] GPU render pass output is background-only clear color! Check URP scene settings / material bindings. Pure GPU mandate enforced.");
            }

            byte[] bytes = outputTex.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            if (GameObject.Find("H8_CaveDiagnosticFillLight") != null)
                UnityEngine.Object.DestroyImmediate(GameObject.Find("H8_CaveDiagnosticFillLight"));
            UnityEngine.Object.DestroyImmediate(meshGo);
            UnityEngine.Object.DestroyImmediate(rockMat);
            UnityEngine.Object.DestroyImmediate(outputTex);

            Debug.Log($"[H8_Cave3DVisualTester] Rendered URP GPU pass: {outputPath}");
        }

        private static float MathAbsDiff(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        private static void Generate3PanelSdfSliceHeatmap(
            NativeArray<float> sdfGrid,
            int centerVx,
            int centerVy,
            int centerVz,
            string path)
        {
            int width = 1024;
            int height = 512;
            Texture2D heatmap = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = new Color[width * height];

            int panelWidth = width / 3;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    int idx = py * width + px;
                    int panelIndex = px / panelWidth;
                    int panelPx = px % panelWidth;

                    float sdf = 25.0f;

                    if (panelIndex == 0)
                    {
                        // Panel 1: XY Slice at cavity Z
                        int vx = (int)math.clamp(panelPx * (DensityRes / (float)panelWidth), 0, DensityRes - 1);
                        int vy = (int)math.clamp(py * (DensityRes / (float)height), 0, DensityRes - 1);
                        int gridIdx = vx + vy * DensityRes + centerVz * DensityRes * DensityRes;
                        sdf = sdfGrid[gridIdx];
                    }
                    else if (panelIndex == 1)
                    {
                        // Panel 2: XZ Slice at cavity Y
                        int vx = (int)math.clamp(panelPx * (DensityRes / (float)panelWidth), 0, DensityRes - 1);
                        int vz = (int)math.clamp(py * (DensityRes / (float)height), 0, DensityRes - 1);
                        int gridIdx = vx + centerVy * DensityRes + vz * DensityRes * DensityRes;
                        sdf = sdfGrid[gridIdx];
                    }
                    else if (panelIndex == 2)
                    {
                        // Panel 3: YZ Slice at cavity X
                        int vy = (int)math.clamp(py * (DensityRes / (float)height), 0, DensityRes - 1);
                        int vz = (int)math.clamp(panelPx * (DensityRes / (float)panelWidth), 0, DensityRes - 1);
                        int gridIdx = centerVx + vy * DensityRes + vz * DensityRes * DensityRes;
                        sdf = sdfGrid[gridIdx];
                    }

                    // Divider lines between panels
                    if (px == panelWidth || px == panelWidth * 2)
                    {
                        pixels[idx] = new Color(0.9f, 0.9f, 0.9f, 1.0f);
                        continue;
                    }

                    Color pixelColor;
                    if (math.abs(sdf) < 0.6f)
                    {
                        // Isosurface D = 0 -> Bright Yellow contour
                        pixelColor = new Color(1.0f, 0.92f, 0.05f, 1.0f);
                    }
                    else if (sdf > 0.0f)
                    {
                        // Solid rock D > 0 -> Deep Navy Blue gradient
                        float intensity = math.saturate(sdf / 16.0f);
                        pixelColor = Color.Lerp(new Color(0.12f, 0.28f, 0.55f, 1.0f), new Color(0.02f, 0.08f, 0.25f, 1.0f), intensity);
                    }
                    else
                    {
                        // Cave void cavity D < 0 -> Crimson Red gradient
                        float intensity = math.saturate(-sdf / 16.0f);
                        pixelColor = Color.Lerp(new Color(0.85f, 0.12f, 0.10f, 1.0f), new Color(0.35f, 0.02f, 0.02f, 1.0f), intensity);
                    }

                    pixels[idx] = pixelColor;
                }
            }

            heatmap.SetPixels(pixels);
            heatmap.Apply();
            byte[] bytes = heatmap.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            UnityEngine.Object.DestroyImmediate(heatmap);
            Debug.Log($"[H8_Cave3DVisualTester] Saved 3-Panel SDF Slice Heatmap to {path}");
        }
    }
}
