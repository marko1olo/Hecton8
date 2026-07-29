using System;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.World;

namespace MapMagic.Editor.Diagnostics
{
    public static class GeologyAtlasTask
    {
        // Was C:\Users\Admin\.gemini\antigravity\brain\bdf7a07e-...\atlas - a SECOND foreign agent brain
        // directory, distinct from the 7b5d06d2-... one the other diagnostics tasks used. Outside the repo,
        // unversioned, and invisible to anyone auditing this project's geology evidence. `static readonly`
        // rather than `const` because Path.Combine is not a compile-time constant.
        private static readonly string OutDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "geology_atlas");

        private const int Res = 512;

        [MenuItem("Hecton8/Diagnostics/Geology Atlas")]
        public static void Run()
        {
            // The atlas is a set of PNGs produced from generated matrices, so a null graphics device makes
            // every tile in it zeros. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans
            // -nographics for exactly this, and Tools/BatchTasks passes it to this method.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[GeologyAtlas] REFUSED: no GPU context (graphicsDeviceType == Null). Every atlas tile " +
                    "would be zeros, which reads as flat geology rather than as a failed run. Remove " +
                    "-nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                if (Directory.Exists(OutDir))
                {
                    // This deletes files. The old version swallowed every failure with an empty `catch { }`,
                    // so a locked or read-only stale PNG survived into the new atlas and was read as fresh
                    // output. Name what could not be removed instead.
                    foreach (string file in Directory.GetFiles(OutDir, "*.png"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception deleteEx)
                        {
                            Debug.LogWarning(
                                $"[GeologyAtlas] Could not delete stale atlas tile '{file}' - it will " +
                                $"appear in this run's output as if it were fresh: {deleteEx.Message}");
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(OutDir);
                }
                DoRun();
            }
            catch (Exception ex)
            {
                // Previously exited 0 from a finally block, so an atlas that was never generated reported
                // success to whatever read the exit code.
                Debug.LogError($"[GeologyAtlas] FAILED, the atlas is incomplete or absent: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log($"[GeologyAtlas] Atlas written to {OutDir}");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Hijacks ANY batchmode Unity launch when a run.flag file is present, and Run() ends in
        /// EditorApplication.Exit - so an unrelated batchmode job (a route probe, a compile check, a build)
        /// can be killed by this diagnostic without anything naming it as the cause.
        ///
        /// The old version deleted the flag inside `try { ... } catch { }`. If the delete failed - file
        /// locked, directory read-only - the flag survived and EVERY subsequent batchmode launch on this
        /// machine was hijacked, permanently, silently. It now refuses to run in that case rather than
        /// becoming a persistent trap, and it announces the hijack when it does take over.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoRunOnBatch()
        {
            if (!Application.isBatchMode) return;

            string flag = Path.Combine(OutDir, "run.flag");
            if (!File.Exists(flag))
                return;

            try
            {
                File.Delete(flag);
            }
            catch (Exception deleteEx)
            {
                Debug.LogError(
                    $"[GeologyAtlas] REFUSING to auto-run: could not delete the trigger flag '{flag}', so " +
                    "running now would leave it in place and hijack every future batchmode launch on this " +
                    $"machine. Delete it by hand. ({deleteEx.Message})");
                return;
            }

            Debug.Log(
                $"[GeologyAtlas] AUTO-RUN: '{flag}' was present, so this batchmode session is being taken " +
                "over by the geology atlas and will exit when it finishes. If you launched Unity for " +
                "something else, this is why it ended.");
            Run();
        }

        private static void DoRun()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(
                (uint)WorldMacroGeologyFields.DefaultAuthoringSeed);

            // WAS 0f, which is also what WorldMacroGeologyFields.CreateDefault leaves it at
            // (WorldMacroGeologyFields.cs:48). Geology computes every height as `WaterSurfaceY - depth`
            // (:1381), so a zero datum drew this whole atlas 14.02 m deeper than the world it claims to
            // describe. The runtime path was corrected to the real datum in three sites in
            // HectonSandboxAbyssalShelfJobs.cs (commit 34e4591ae); this diagnostic was the fourth site of
            // the same defect and kept measuring against the old frame.
            //
            // This line needs "Hecton8.World.Contracts" in Hecton8.Editor.asmdef and did not have it when
            // it was written. WorldWaterLevelCalibrationMath lives in that assembly, not in the one that
            // owns WorldMacroGeologyParams above it (Hecton8.Core), even though BOTH are namespace
            // Hecton8.World - so the `using Hecton8.World;` at the top of this file resolves one and not
            // the other, and the failure reads as CS0103 "does not exist in the current context" rather
            // than as the missing assembly reference it actually is. It cost a whole Unity batchmode run:
            // Scripts have compiler errors -> exit 1 -> the headless simulation never started. The
            // lock-free gate in CONTRIBUTING.md cannot catch this, because it emits FALSE CS0433/CS0656
            // against Hecton8.Editor and so is untrustworthy for exactly this assembly. Nor could the unit
            // tests: WorldWaterLevelCalibrationEditTests.cs "references" this type only through
            // StringAssert.Contains on source text, which compiles whether or not the reference resolves.
            // Do not drop that asmdef reference; nothing cheaper than a real Unity compile will notice.
            p.WaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

            (float x, float z, string name)[] points = new (float, float, string)[]
            {
                (5000f, 5000f, "P1_origin"),
                (50000f, 50000f, "P2_near"),
                (-40000f, 15000f, "P3_west"),
                (300000f, 90000f, "P4_far"),
                (777000f, -333000f, "P5_deepfar"),
            };

            (float size, string tag)[] scales = new (float, string)[]
            {
                (10000f, "10km"),
                (1000f, "1km"),
                (200f, "200m"),
            };

            StringBuilder report = new StringBuilder();
            report.AppendLine("GEOLOGY ATLAS — Megabrief Overhaul Evaluation (R9 Dynamic Sentinel)");
            report.AppendLine($"BUILD SENTINEL (must match source): {WorldMacroGeologyFields.BuildSentinel}");
            report.AppendLine($"Seed={p.Seed}  extent={p.WorldExtentMeters}  WaterSurfaceY={p.WaterSurfaceY}");
            report.AppendLine($"RidgeHeight={p.RidgeHeightMeters}  AbyssDepth={p.AbyssDepthMeters}  Hadal={p.HadalDepthMeters}  Trench={p.TrenchDepthMeters}");
            report.AppendLine($"Resolution per cell: {Res}x{Res}");
            report.AppendLine("==================================================");

            foreach (var pt in points)
            {
                foreach (var sc in scales)
                {
                    RenderCellBurst(pt.x, pt.z, sc.size, $"{pt.name}_{sc.tag}", in p, report);
                    RenderStageDumpsBurst(pt.x, pt.z, sc.size, $"{pt.name}_{sc.tag}", in p);
                }
            }

            stopwatch.Stop();
            report.AppendLine($"Total Atlas Execution Time: {stopwatch.ElapsedMilliseconds} ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
            File.WriteAllText(Path.Combine(OutDir, "atlas_report.txt"), report.ToString());
            Debug.Log($"[GeologyAtlas] Completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }

        private static void RenderStageDumpsBurst(float cx, float cz, float size, string label, in WorldMacroGeologyParams p)
        {
            float pixelSize = size / Res;
            double x0 = (double)cx - size * 0.5;
            double z0 = (double)cz - size * 0.5;
            float3 lightDir = math.normalize(new float3(-1f, 0.6f, 1f));

            int[] stagesToRender = new int[] { 1, 2, 3, 4, 5, 6, 7 };
            foreach (int stage in stagesToRender)
            {
                NativeArray<float> heightBuffer = new NativeArray<float>(Res * Res, Allocator.TempJob);
                NativeArray<WorldMacroGeologyFields.MacroMasks> dummyMasks = new NativeArray<WorldMacroGeologyFields.MacroMasks>(Res * Res, Allocator.TempJob);
                NativeArray<Color32> pixels = new NativeArray<Color32>(Res * Res, Allocator.TempJob);

                AtlasHeightAndMasksJob heightJob = new AtlasHeightAndMasksJob
                {
                    x0 = x0,
                    z0 = z0,
                    pixelSize = pixelSize,
                    res = Res,
                    paramsVal = p,
                    stageDump = stage,
                    HeightBuffer = heightBuffer,
                    MasksBuffer = dummyMasks
                };
                heightJob.Schedule(Res * Res, 64).Complete();

                AtlasStageHillshadeJob hillJob = new AtlasStageHillshadeJob
                {
                    res = Res,
                    pixelSize = pixelSize,
                    lightDir = lightDir,
                    HeightBuffer = heightBuffer,
                    OutputPixels = pixels
                };
                hillJob.Schedule(Res * Res, 64).Complete();

                SavePixels(pixels, $"{label}_stage{stage}_hillshade");

                heightBuffer.Dispose();
                dummyMasks.Dispose();
                pixels.Dispose();
            }
        }

        private static void RenderCellBurst(
            float cx, float cz, float size, string label,
            in WorldMacroGeologyParams p,
            StringBuilder report)
        {
            float pixelSize = size / Res;
            double x0 = (double)cx - size * 0.5;
            double z0 = (double)cz - size * 0.5;
            float3 lightDir = math.normalize(new float3(-1f, 0.6f, 1f));

            int totalPixels = Res * Res;

            NativeArray<float> heightBuffer = new NativeArray<float>(totalPixels, Allocator.TempJob);
            NativeArray<WorldMacroGeologyFields.MacroMasks> masksBuffer = new NativeArray<WorldMacroGeologyFields.MacroMasks>(totalPixels, Allocator.TempJob);

            NativeArray<Color32> texHeight = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texHill = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texSlope = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texFeatures = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texGameplay = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texSplatmap = new NativeArray<Color32>(totalPixels, Allocator.TempJob);
            NativeArray<Color32> texVoxelControl = new NativeArray<Color32>(totalPixels, Allocator.TempJob);

            // 1. Evaluate Height & Masks via Burst Job across all CPU threads
            AtlasHeightAndMasksJob heightJob = new AtlasHeightAndMasksJob
            {
                x0 = x0,
                z0 = z0,
                pixelSize = pixelSize,
                res = Res,
                paramsVal = p,
                stageDump = 0,
                HeightBuffer = heightBuffer,
                MasksBuffer = masksBuffer
            };
            heightJob.Schedule(totalPixels, 64).Complete();

            // 2. Render all Cell Textures via Burst Job
            AtlasCellRenderJob cellJob = new AtlasCellRenderJob
            {
                res = Res,
                pixelSize = pixelSize,
                hadalDepth = p.HadalDepthMeters,
                lightDir = lightDir,
                HeightBuffer = heightBuffer,
                MasksBuffer = masksBuffer,
                TexHeight = texHeight,
                TexHill = texHill,
                TexSlope = texSlope,
                TexFeatures = texFeatures,
                TexGameplay = texGameplay,
                TexSplatmap = texSplatmap,
                TexVoxelControl = texVoxelControl
            };
            cellJob.Schedule(totalPixels, 64).Complete();

            // 3. Calculate Report Statistics
            float minH = float.MaxValue, maxH = float.MinValue;
            double sum = 0.0, sumSq = 0.0;
            double mRidge = 0, mTrench = 0, mCanyon = 0, mBasin = 0, mShelf = 0;
            double mHardRock = 0, mFault = 0, mCrater = 0, mTerrace = 0, mSlump = 0, mShelfBreak = 0, mPlateEdge = 0;
            double mRiver = 0, mLake = 0, mStrata = 0, mFold = 0, mVolcano = 0, mMesa = 0, mDune = 0;
            int nanCount = 0;
            long[] slopeBuckets = new long[4];

            for (int i = 0; i < totalPixels; i++)
            {
                float h = heightBuffer[i];
                WorldMacroGeologyFields.MacroMasks m = masksBuffer[i];

                if (float.IsNaN(h) || float.IsInfinity(h)) { nanCount++; continue; }
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
                sum += h; sumSq += (double)h * h;

                mRidge += m.Ridge; mTrench += m.Trench; mCanyon += m.Canyon; mBasin += m.Basin;
                mShelf += m.Shelf; mHardRock += m.HardRock; mFault += m.Fault; mCrater += m.Crater;
                mTerrace += m.Terrace; mSlump += m.Slump; mShelfBreak += m.ShelfBreak; mPlateEdge += m.PlateEdge;
                mRiver += m.River; mLake += m.Lake; mStrata += m.Strata; mFold += m.Fold;
                mVolcano += m.Volcano; mMesa += m.Mesa; mDune += m.Dune;

                // Calculate slope bucket from slope texture green channel or normals
                int x = i % Res;
                int z = i / Res;
                int xL = math.clamp(x - 1, 0, Res - 1);
                int xR = math.clamp(x + 1, 0, Res - 1);
                int zD = math.clamp(z - 1, 0, Res - 1);
                int zU = math.clamp(z + 1, 0, Res - 1);

                float hL = heightBuffer[z * Res + xL];
                float hR = heightBuffer[z * Res + xR];
                float hD = heightBuffer[zD * Res + x];
                float hU = heightBuffer[zU * Res + x];

                Vector3 nrm = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                float slopeDeg = Vector3.Angle(Vector3.up, nrm);
                if (slopeDeg < 15f) slopeBuckets[0]++;
                else if (slopeDeg < 40f) slopeBuckets[1]++;
                else if (slopeDeg < 70f) slopeBuckets[2]++;
                else slopeBuckets[3]++;
            }

            long n = (long)Res * Res;
            double mean = sum / n;
            double var = (sumSq / n) - (mean * mean);
            double std = var > 0 ? Math.Sqrt(var) : 0;

            SavePixels(texHeight, $"{label}_1_height");
            SavePixels(texHill, $"{label}_2_hillshade");
            SavePixels(texSlope, $"{label}_3_slope");
            SavePixels(texFeatures, $"{label}_6_features");
            SavePixels(texGameplay, $"{label}_8_gameplay");
            SavePixels(texSplatmap, $"{label}_9_splatmap");
            SavePixels(texVoxelControl, $"{label}_10_voxel_seams");

            double Pct(int b) => 100.0 * slopeBuckets[b] / n;
            double MPct(double acc) => 100.0 * acc / n;

            report.AppendLine($"[{label}]  center=({cx},{cz})  size={size}m  pixel={pixelSize:F3}m/px");
            report.AppendLine($"   Height m: min={minH:F1}  max={maxH:F1}  mean={mean:F1}  std={std:F1}  NaN={nanCount}");
            report.AppendLine($"   Slope deg %: 0-15={Pct(0):F1}  15-40={Pct(1):F1}  40-70={Pct(2):F1}  70+={Pct(3):F1}");
            report.AppendLine($"   Mask cover %: Ridge={MPct(mRidge):F1} Trench={MPct(mTrench):F1} Canyon={MPct(mCanyon):F1} Basin={MPct(mBasin):F1} Shelf={MPct(mShelf):F1} HardRock={MPct(mHardRock):F1}");
            report.AppendLine($"                 Fault={MPct(mFault):F1} Crater={MPct(mCrater):F1} River={MPct(mRiver):F1} Lake={MPct(mLake):F1} Strata={MPct(mStrata):F1} Fold={MPct(mFold):F1} Volcano={MPct(mVolcano):F1} Mesa={MPct(mMesa):F1} Dune={MPct(mDune):F1}");
            report.AppendLine("--------------------------------------------------");

            heightBuffer.Dispose();
            masksBuffer.Dispose();
            texHeight.Dispose();
            texHill.Dispose();
            texSlope.Dispose();
            texFeatures.Dispose();
            texGameplay.Dispose();
            texSplatmap.Dispose();
            texVoxelControl.Dispose();
        }

        private static void SavePixels(NativeArray<Color32> pixels, string name)
        {
            Texture2D tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            tex.SetPixelData(pixels, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        [BurstCompile]
        private struct AtlasHeightAndMasksJob : IJobParallelFor
        {
            public double x0;
            public double z0;
            public double pixelSize;
            public int res;
            public WorldMacroGeologyParams paramsVal;
            public int stageDump;

            [WriteOnly] public NativeArray<float> HeightBuffer;
            [WriteOnly] public NativeArray<WorldMacroGeologyFields.MacroMasks> MasksBuffer;

            public void Execute(int index)
            {
                int x = index % res;
                int z = index / res;
                double wx = x0 + (x + 0.5) * pixelSize;
                double wz = z0 + (z + 0.5) * pixelSize;

                WorldMacroGeologyFields.MacroMasks m;
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in paramsVal, out m, stageDump);
                HeightBuffer[index] = h;
                MasksBuffer[index] = m;
            }
        }

        [BurstCompile]
        private struct AtlasStageHillshadeJob : IJobParallelFor
        {
            public int res;
            public float pixelSize;
            public float3 lightDir;
            [ReadOnly] public NativeArray<float> HeightBuffer;
            [WriteOnly] public NativeArray<Color32> OutputPixels;

            public void Execute(int index)
            {
                int x = index % res;
                int z = index / res;

                int xL = math.clamp(x - 1, 0, res - 1);
                int xR = math.clamp(x + 1, 0, res - 1);
                int zD = math.clamp(z - 1, 0, res - 1);
                int zU = math.clamp(z + 1, 0, res - 1);

                float hL = HeightBuffer[z * res + xL];
                float hR = HeightBuffer[z * res + xR];
                float hD = HeightBuffer[zD * res + x];
                float hU = HeightBuffer[zU * res + x];

                float3 nrm = math.normalize(new float3(hL - hR, 2f * pixelSize, hD - hU));
                float intensity = math.clamp(math.dot(nrm, lightDir), 0f, 1f) * 0.85f + 0.15f;
                byte val = (byte)math.clamp((int)(intensity * 255f), 0, 255);
                OutputPixels[index] = new Color32(val, val, val, 255);
            }
        }

        [BurstCompile]
        private struct AtlasCellRenderJob : IJobParallelFor
        {
            public int res;
            public float pixelSize;
            public float hadalDepth;
            public float3 lightDir;

            [ReadOnly] public NativeArray<float> HeightBuffer;
            [ReadOnly] public NativeArray<WorldMacroGeologyFields.MacroMasks> MasksBuffer;

            [WriteOnly] public NativeArray<Color32> TexHeight;
            [WriteOnly] public NativeArray<Color32> TexHill;
            [WriteOnly] public NativeArray<Color32> TexSlope;
            [WriteOnly] public NativeArray<Color32> TexFeatures;
            [WriteOnly] public NativeArray<Color32> TexGameplay;
            [WriteOnly] public NativeArray<Color32> TexSplatmap;
            [WriteOnly] public NativeArray<Color32> TexVoxelControl;

            public void Execute(int index)
            {
                int x = index % res;
                int z = index / res;

                float hC = HeightBuffer[index];
                WorldMacroGeologyFields.MacroMasks m = MasksBuffer[index];

                // 1. Depth Ramp
                TexHeight[index] = AtlasColorRamps.DepthRamp(hC, hadalDepth);

                // 2. Hillshade
                int xL = math.clamp(x - 1, 0, res - 1);
                int xR = math.clamp(x + 1, 0, res - 1);
                int zD = math.clamp(z - 1, 0, res - 1);
                int zU = math.clamp(z + 1, 0, res - 1);

                float hL = HeightBuffer[z * res + xL];
                float hR = HeightBuffer[z * res + xR];
                float hD = HeightBuffer[zD * res + x];
                float hU = HeightBuffer[zU * res + x];

                float3 nrm = math.normalize(new float3(hL - hR, 2f * pixelSize, hD - hU));
                float intensity = math.clamp(math.dot(nrm, lightDir), 0f, 1f) * 0.85f + 0.15f;
                byte hillVal = (byte)math.clamp((int)(intensity * 255f), 0, 255);
                TexHill[index] = new Color32(hillVal, hillVal, hillVal, 255);

                // 3. Slope
                float slopeRad = math.acos(math.clamp(nrm.y, 0f, 1f));
                float slopeDeg = slopeRad * (180f / math.PI);
                TexSlope[index] = AtlasColorRamps.SlopeRamp(slopeDeg);

                // 4. Features Heatmap (Red = Volcano, Green = Reef, Blue = Crater)
                TexFeatures[index] = new Color32(
                    (byte)math.clamp((int)(math.saturate(m.Volcano * 4f) * 255f), 0, 255),
                    (byte)math.clamp((int)(math.saturate(m.Reef * 4f) * 255f), 0, 255),
                    (byte)math.clamp((int)(math.saturate(m.Crater * 4f) * 255f), 0, 255),
                    255);

                // 5. Gameplay Heatmap (Red = CaveEntrance, Green = Ledge, Blue = BrinePool)
                TexGameplay[index] = new Color32(
                    (byte)math.clamp((int)(math.saturate(m.CaveEntrance * 5f) * 255f), 0, 255),
                    (byte)math.clamp((int)(math.saturate(m.Ledge * 4f) * 255f), 0, 255),
                    (byte)math.clamp((int)(math.saturate(m.BrinePool * 4f) * 255f), 0, 255),
                    255);

                // 6. Splatmap Simulator (Red = HardRock, Green = Sediment/Sand, Blue = Lakebed)
                float sedimentMask = math.saturate(1f - m.HardRock - m.Lake);
                TexSplatmap[index] = new Color32(
                    (byte)math.clamp((int)(math.saturate(m.HardRock) * 255f), 0, 255),
                    (byte)math.clamp((int)(sedimentMask * 255f), 0, 255),
                    (byte)math.clamp((int)(math.saturate(m.Lake) * 255f), 0, 255),
                    255);

                // 7. Voxel Seam Blueprint (Red = CaveEntrance, Green = HardRockExposure, Blue = VoxelSeamMask)
                float hardRockExposure = math.saturate(m.HardRock * 1.5f);
                float voxelSeamMask = math.saturate((m.CaveEntrance + m.Ledge) * 2f);
                TexVoxelControl[index] = new Color32(
                    (byte)math.clamp((int)(math.saturate(m.CaveEntrance * 4f) * 255f), 0, 255),
                    (byte)math.clamp((int)(hardRockExposure * 255f), 0, 255),
                    (byte)math.clamp((int)(voxelSeamMask * 255f), 0, 255),
                    255);
            }
        }
    }

    [BurstCompile]
    public static class AtlasColorRamps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 DepthRamp(float h, float hadal)
        {
            if (math.isnan(h) || !math.isfinite(h)) return new Color32(255, 0, 255, 255);
            if (h >= 0f)
            {
                float t = math.saturate(h / 620f);
                byte r = (byte)math.clamp((int)math.lerp(216.75f, 255f, t), 0, 255);
                byte g = (byte)math.clamp((int)math.lerp(209.10f, 255f, t), 0, 255);
                byte b = (byte)math.clamp((int)math.lerp(140.25f, 255f, t), 0, 255);
                return new Color32(r, g, b, 255);
            }
            float d = math.saturate(-h / math.max(1f, hadal));
            float4 c;
            if (d < 0.25f)
                c = math.lerp(new float4(0.30f, 0.85f, 0.75f, 1f), new float4(0.15f, 0.60f, 0.80f, 1f), d / 0.25f);
            else if (d < 0.55f)
                c = math.lerp(new float4(0.15f, 0.60f, 0.80f, 1f), new float4(0.10f, 0.30f, 0.65f, 1f), (d - 0.25f) / 0.30f);
            else if (d < 0.80f)
                c = math.lerp(new float4(0.10f, 0.30f, 0.65f, 1f), new float4(0.05f, 0.12f, 0.40f, 1f), (d - 0.55f) / 0.25f);
            else
                c = math.lerp(new float4(0.05f, 0.12f, 0.40f, 1f), new float4(0.02f, 0.03f, 0.15f, 1f), (d - 0.80f) / 0.20f);

            return new Color32(
                (byte)math.clamp((int)(c.x * 255f), 0, 255),
                (byte)math.clamp((int)(c.y * 255f), 0, 255),
                (byte)math.clamp((int)(c.z * 255f), 0, 255),
                255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 SlopeRamp(float deg)
        {
            float t = math.saturate(deg / 90f);
            float4 c;
            if (t < 0.5f)
                c = math.lerp(new float4(0.1f, 0.8f, 0.2f, 1f), new float4(0.95f, 0.9f, 0.1f, 1f), t / 0.5f);
            else
                c = math.lerp(new float4(0.95f, 0.9f, 0.1f, 1f), new float4(0.9f, 0.1f, 0.1f, 1f), (t - 0.5f) / 0.5f);

            return new Color32(
                (byte)math.clamp((int)(c.x * 255f), 0, 255),
                (byte)math.clamp((int)(c.y * 255f), 0, 255),
                (byte)math.clamp((int)(c.z * 255f), 0, 255),
                255);
        }
    }
}
