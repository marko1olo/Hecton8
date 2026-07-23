using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.World;

namespace MapMagic.Editor.Diagnostics
{
    public static class GeologyAtlasTask
    {
        // R19 RECOMPILE TOUCH: 2026-07-23T11:20:00
        private const string OutDir =
            @"C:\Users\Admin\.gemini\antigravity\brain\bdf7a07e-c29b-4dac-8a24-2f14ca51d3d2\atlas";

        private const int Res = 512;

        [MenuItem("Hecton8/Diagnostics/Geology Atlas")]
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                DoRun();
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(OutDir, "atlas_error.txt"), ex.ToString());
                Debug.LogError($"[GeologyAtlas] {ex}");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnBatch()
        {
            if (!Application.isBatchMode) return;
            string flag = Path.Combine(OutDir, "run.flag");
            if (File.Exists(flag))
            {
                try { File.Delete(flag); } catch { }
                EditorApplication.delayCall += Run;
            }
        }

        private static void DoRun()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(
                (uint)WorldMacroGeologyFields.DefaultAuthoringSeed);
            p.WaterSurfaceY = 0f;

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

            float[] height = new float[Res * Res];
            WorldMacroGeologyFields.MacroMasks[] masks = new WorldMacroGeologyFields.MacroMasks[Res * Res];

            foreach (var pt in points)
            {
                foreach (var sc in scales)
                {
                    RenderCell(pt.x, pt.z, sc.size, $"{pt.name}_{sc.tag}", in p, height, masks, report);
                    RenderStageDumps(pt.x, pt.z, sc.size, $"{pt.name}_{sc.tag}", in p, height);
                }
            }

            File.WriteAllText(Path.Combine(OutDir, "atlas_report.txt"), report.ToString());
        }

        // R16: render a hillshade of the depth field after each accumulation stage (1..7), ONE build.
        // The stage where the zebra/rings/hairline FIRST appears is the culprit line. stage8=full=RenderCell.
        private static void RenderStageDumps(float cx, float cz, float size, string label,
            in WorldMacroGeologyParams p, float[] height)
        {
            float pixelSize = size / Res;
            float x0 = cx - size * 0.5f;
            float z0 = cz - size * 0.5f;
            Vector3 lightDir = new Vector3(-1f, 0.6f, 1f).normalized;
            WorldMacroGeologyParams localP = p;

            for (int stage = 1; stage <= 7; stage++)
            {
                int st = stage;
                System.Threading.Tasks.Parallel.For(0, Res, z =>
                {
                    float wz = z0 + (z + 0.5f) * pixelSize;
                    for (int x = 0; x < Res; x++)
                    {
                        float wx = x0 + (x + 0.5f) * pixelSize;
                        height[z * Res + x] = WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in localP, out _, st);
                    }
                });

                Texture2D tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
                for (int z = 0; z < Res; z++)
                {
                    for (int x = 0; x < Res; x++)
                    {
                        float hL = height[z * Res + math.clamp(x - 1, 0, Res - 1)];
                        float hR = height[z * Res + math.clamp(x + 1, 0, Res - 1)];
                        float hD = height[math.clamp(z - 1, 0, Res - 1) * Res + x];
                        float hU = height[math.clamp(z + 1, 0, Res - 1) * Res + x];
                        Vector3 nrm = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                        float intensity = Mathf.Clamp01(Vector3.Dot(nrm, lightDir)) * 0.85f + 0.15f;
                        tex.SetPixel(x, z, new Color(intensity, intensity, intensity, 1f));
                    }
                }
                Save(tex, $"{label}_stage{stage}_hillshade");
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void RenderCell(
            float cx, float cz, float size, string label,
            in WorldMacroGeologyParams p,
            float[] height, WorldMacroGeologyFields.MacroMasks[] masks,
            StringBuilder report)
        {
            float pixelSize = size / Res;
            float x0 = cx - size * 0.5f;
            float z0 = cz - size * 0.5f;

            float minH = float.MaxValue, maxH = float.MinValue;
            double sum = 0.0, sumSq = 0.0;
            double mRidge = 0, mTrench = 0, mCanyon = 0, mBasin = 0, mShelf = 0;
            double mHardRock = 0, mFault = 0, mCrater = 0, mTerrace = 0, mSlump = 0, mShelfBreak = 0, mPlateEdge = 0;
            double mRiver = 0, mLake = 0, mStrata = 0, mFold = 0, mVolcano = 0, mMesa = 0, mDune = 0;
            int nanCount = 0;

            WorldMacroGeologyParams localP = p;
            System.Threading.Tasks.Parallel.For(0, Res, z =>
            {
                float wz = z0 + (z + 0.5f) * pixelSize;
                for (int x = 0; x < Res; x++)
                {
                    float wx = x0 + (x + 0.5f) * pixelSize;
                    WorldMacroGeologyFields.MacroMasks m;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in localP, out m);
                    int idx = z * Res + x;
                    height[idx] = h;
                    masks[idx] = m;
                }
            });

            for (int z = 0; z < Res; z++)
            {
                for (int x = 0; x < Res; x++)
                {
                    int idx = z * Res + x;
                    float h = height[idx];
                    WorldMacroGeologyFields.MacroMasks m = masks[idx];

                    if (float.IsNaN(h) || float.IsInfinity(h)) { nanCount++; continue; }
                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                    sum += h; sumSq += (double)h * h;
                    mRidge += m.Ridge; mTrench += m.Trench; mCanyon += m.Canyon; mBasin += m.Basin;
                    mShelf += m.Shelf; mHardRock += m.HardRock; mFault += m.Fault; mCrater += m.Crater;
                    mTerrace += m.Terrace; mSlump += m.Slump; mShelfBreak += m.ShelfBreak; mPlateEdge += m.PlateEdge;
                    mRiver += m.River; mLake += m.Lake; mStrata += m.Strata; mFold += m.Fold;
                    mVolcano += m.Volcano; mMesa += m.Mesa; mDune += m.Dune;
                }
            }

            long n = (long)Res * Res;
            double mean = sum / n;
            double var = (sumSq / n) - (mean * mean);
            double std = var > 0 ? Math.Sqrt(var) : 0;

            Texture2D texHeight = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            Texture2D texHill   = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            Texture2D texSlope  = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            // R14: height + hillshade + slope ONLY (Director: don't waste laptop time on the other 5 maps).

            Vector3 lightDir = new Vector3(-1f, 0.6f, 1f).normalized;
            long[] slopeBuckets = new long[4];

            const int OriBins = 18;
            double[] oriHist = new double[OriBins];

            float SampleH(int px, int pz)
            {
                int qx = math.clamp(px, 0, Res - 1);
                int qz = math.clamp(pz, 0, Res - 1);
                return height[qz * Res + qx];
            }

            const float Sigma = 3.0f;
            const int BlurR = 9; // 3 * Sigma
            float[] gKernel = new float[2 * BlurR + 1];
            float gSum = 0f;
            for (int i = -BlurR; i <= BlurR; i++)
            {
                float w = math.exp(-0.5f * (i * i) / (Sigma * Sigma));
                gKernel[i + BlurR] = w;
                gSum += w;
            }
            for (int i = 0; i < gKernel.Length; i++) gKernel[i] /= gSum;

            float[] blurTmp = new float[Res * Res];
            float[] residual = new float[Res * Res];
            System.Threading.Tasks.Parallel.For(0, Res, z =>
            {
                for (int x = 0; x < Res; x++)
                {
                    float acc = 0f;
                    for (int dx = -BlurR; dx <= BlurR; dx++)
                    {
                        int qx = math.clamp(x + dx, 0, Res - 1);
                        acc += height[z * Res + qx] * gKernel[dx + BlurR];
                    }
                    blurTmp[z * Res + x] = acc;
                }
            });
            System.Threading.Tasks.Parallel.For(0, Res, z =>
            {
                for (int x = 0; x < Res; x++)
                {
                    float acc = 0f;
                    for (int dz = -BlurR; dz <= BlurR; dz++)
                    {
                        int qz = math.clamp(z + dz, 0, Res - 1);
                        acc += blurTmp[qz * Res + x] * gKernel[dz + BlurR];
                    }
                    residual[z * Res + x] = height[z * Res + x] - acc;
                }
            });

            float SampleRes(int px, int pz)
            {
                int qx = math.clamp(px, 0, Res - 1);
                int qz = math.clamp(pz, 0, Res - 1);
                return residual[qz * Res + qx];
            }

            for (int z = 0; z < Res; z++)
            {
                for (int x = 0; x < Res; x++)
                {
                    int idx = z * Res + x;
                    float hC = height[idx];

                    texHeight.SetPixel(x, z, DepthRamp(hC, p.HadalDepthMeters));

                    float hL = SampleH(x - 1, z), hR = SampleH(x + 1, z);
                    float hD = SampleH(x, z - 1), hU = SampleH(x, z + 1);

                    Vector3 nrm = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                    float intensity = Mathf.Clamp01(Vector3.Dot(nrm, lightDir)) * 0.85f + 0.15f;
                    texHill.SetPixel(x, z, new Color(intensity, intensity, intensity, 1f));

                    float slopeDeg = Vector3.Angle(Vector3.up, nrm);
                    if (slopeDeg < 15f) slopeBuckets[0]++;
                    else if (slopeDeg < 40f) slopeBuckets[1]++;
                    else if (slopeDeg < 70f) slopeBuckets[2]++;
                    else slopeBuckets[3]++;
                    texSlope.SetPixel(x, z, SlopeRamp(slopeDeg));

                    float gx = SampleRes(x + 1, z) - SampleRes(x - 1, z);
                    float gz = SampleRes(x, z + 1) - SampleRes(x, z - 1);
                    float gmag = Mathf.Sqrt(gx * gx + gz * gz);
                    if (gmag > 1e-4f)
                    {
                        float ori = Mathf.Atan2(gz, gx);
                        if (ori < 0f) ori += Mathf.PI;
                        int bin = Mathf.Clamp((int)(ori / Mathf.PI * OriBins), 0, OriBins - 1);
                        oriHist[bin] += gmag;
                    }
                }
            }

            Save(texHeight, $"{label}_1_height");
            Save(texHill, $"{label}_2_hillshade");
            Save(texSlope, $"{label}_3_slope");

            double Pct(int b) => 100.0 * slopeBuckets[b] / n;
            double MPct(double acc) => 100.0 * acc / n;

            report.AppendLine($"[{label}]  center=({cx},{cz})  size={size}m  pixel={pixelSize:F3}m/px");
            report.AppendLine($"   Height m: min={minH:F1}  max={maxH:F1}  mean={mean:F1}  std={std:F1}  NaN={nanCount}");
            report.AppendLine($"   Slope deg %: 0-15={Pct(0):F1}  15-40={Pct(1):F1}  40-70={Pct(2):F1}  70+={Pct(3):F1}");

            double oriSum = 0, oriPeak = 0; int oriPeakBin = 0;
            for (int b = 0; b < OriBins; b++)
            {
                oriSum += oriHist[b];
                if (oriHist[b] > oriPeak) { oriPeak = oriHist[b]; oriPeakBin = b; }
            }
            double oriMean = oriSum / OriBins;
            double hatchIndex = oriMean > 1e-9 ? oriPeak / oriMean : 0;
            double peakAngleDeg = (oriPeakBin + 0.5) * (180.0 / OriBins);
            report.AppendLine($"   HATCHING index={hatchIndex:F2} [UNRELIABLE at 1km/200m — R13 proved a smooth sub-period ramp scores 5-8 with ZERO stripes; use EYES]  peak@{peakAngleDeg:F0}deg");
            report.AppendLine($"   Mask cover %: Ridge={MPct(mRidge):F1} Trench={MPct(mTrench):F1} Canyon={MPct(mCanyon):F1} Basin={MPct(mBasin):F1} Shelf={MPct(mShelf):F1} HardRock={MPct(mHardRock):F1}");
            report.AppendLine($"                 Fault={MPct(mFault):F1} Crater={MPct(mCrater):F1} River={MPct(mRiver):F1} Lake={MPct(mLake):F1} Strata={MPct(mStrata):F1} Fold={MPct(mFold):F1} Volcano={MPct(mVolcano):F1} Mesa={MPct(mMesa):F1} Dune={MPct(mDune):F1}");
            report.AppendLine("--------------------------------------------------");

            UnityEngine.Object.DestroyImmediate(texHeight);
            UnityEngine.Object.DestroyImmediate(texHill);
            UnityEngine.Object.DestroyImmediate(texSlope);
        }

        private static void Save(Texture2D tex, string name)
        {
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
        }

        private static Color DepthRamp(float h, float hadal)
        {
            if (float.IsNaN(h) || float.IsInfinity(h)) return new Color(1f, 0f, 1f, 1f);
            if (h >= 0f)
            {
                float t = Mathf.InverseLerp(0f, 620f, h);
                return Color.Lerp(new Color(0.85f, 0.82f, 0.55f, 1f), new Color(1f, 1f, 1f, 1f), t);
            }
            float d = Mathf.InverseLerp(0f, -hadal, h);
            if (d < 0.25f) return Color.Lerp(new Color(0.30f, 0.85f, 0.75f, 1f), new Color(0.15f, 0.60f, 0.80f, 1f), d / 0.25f);
            if (d < 0.55f) return Color.Lerp(new Color(0.15f, 0.60f, 0.80f, 1f), new Color(0.10f, 0.30f, 0.65f, 1f), (d - 0.25f) / 0.30f);
            if (d < 0.80f) return Color.Lerp(new Color(0.10f, 0.30f, 0.65f, 1f), new Color(0.05f, 0.12f, 0.40f, 1f), (d - 0.55f) / 0.25f);
            return Color.Lerp(new Color(0.05f, 0.12f, 0.40f, 1f), new Color(0.02f, 0.03f, 0.15f, 1f), (d - 0.80f) / 0.20f);
        }

        private static Color GrayRamp(float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(t, t, t, 1f);
        }

        private static Color SlopeRamp(float deg)
        {
            float t = Mathf.Clamp01(deg / 90f);
            if (t < 0.5f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f, 1f), new Color(0.95f, 0.9f, 0.1f, 1f), t / 0.5f);
            return Color.Lerp(new Color(0.95f, 0.9f, 0.1f, 1f), new Color(0.9f, 0.1f, 0.1f, 1f), (t - 0.5f) / 0.5f);
        }

        private static Color ProvinceColor(float pType, float blend)
        {
            int type = Mathf.Clamp(Mathf.RoundToInt(pType * 7f), 0, 7);
            Color baseCol = type switch
            {
                0 => new Color(0.10f, 0.15f, 0.45f, 1f), // ABYSSAL_PLAIN: Deep Indigo Navy
                1 => new Color(0.75f, 0.25f, 0.15f, 1f), // CRATERED_HIGHLANDS: Rust Red
                2 => new Color(0.15f, 0.65f, 0.30f, 1f), // RIVER_LOWLANDS: Emerald Green
                3 => new Color(0.45f, 0.30f, 0.75f, 1f), // FOLDED_MOUNTAINS: Violet Purple
                4 => new Color(0.70f, 0.10f, 0.25f, 1f), // RIFT_VALLEY: Crimson Red
                5 => new Color(0.95f, 0.45f, 0.10f, 1f), // VOLCANIC_FIELD: Amber Orange
                6 => new Color(0.85f, 0.65f, 0.15f, 1f), // MESA_TABLELANDS: Golden Ochre
                7 => new Color(0.90f, 0.85f, 0.30f, 1f), // DUNE_SEA: Desert Sand Yellow
                _ => new Color(0.50f, 0.50f, 0.50f, 1f)
            };
            float dark = Mathf.Lerp(0.35f, 1.0f, blend);
            return new Color(baseCol.r * dark, baseCol.g * dark, baseCol.b * dark, 1f);
        }
    }
}
