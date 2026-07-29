using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.World;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// NOISE ISOLATOR. Renders the RAW output of each fractal noise function on a flat
    /// grid — NO geology, NO domain warp, NO masks, NO depth composition. This isolates
    /// which noise primitive carries the directional "velvet/hatching" artifact and at
    /// which octave count, instead of guessing through 15 layers of terrain composition.
    ///
    /// For each function (FractalSimplex, Ridged, Billow) × octave count (1,2,3,5) it
    /// writes a hillshade PNG + a hatching index (high-pass gradient-orientation anisotropy).
    /// A single-octave map that is already directional proves the artifact is in the base
    /// snoise lattice; a map that only becomes directional at high octave counts proves it
    /// is the octave-accumulation/rotation that builds it up.
    /// </summary>
    public static class NoiseIsolatorTask
    {
        // Was another agent's private brain directory - outside the repo and unversioned. This is
        // `static readonly` rather than `const` because Path.Combine is not a compile-time constant.
        private static readonly string OutDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "noise_iso");

        private const int Res = 512;
        // Sample the noise over a domain matching the meso scale where hatching appears.
        // 1km-window terrain samples RidgedMultifractal at warpedNorm*~3..7; we sweep a
        // comparable normalized domain span so the octave structure matches gameplay scale.
        private const float DomainSpan = 6f;

        [MenuItem("Hecton8/Diagnostics/Noise Isolator")]
        public static void Run()
        {
            // Emits hillshade PNGs, so it depends on a graphics device.
            // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for these tests:
            // compute shaders and Graphics.Blit return zeros with no GPU context, and a hatching study
            // made of zeros would read as "no hatching found" - the exact false negative this task exists
            // to rule out.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[NoiseIsolator] REFUSED: no GPU context (graphicsDeviceType == Null). Every hillshade " +
                    "would be zeros, which reads as a clean result rather than as a failed run. Remove " +
                    "-nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                Directory.CreateDirectory(OutDir);
                DoRun();
            }
            catch (Exception ex)
            {
                // Previously exited 0 from a finally block, so a run that isolated nothing still reported
                // success to whatever read the exit code.
                Debug.LogError($"[NoiseIsolator] FAILED, the report is incomplete or absent: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoRun()
        {
            uint seed = 880031u;
            int[] octaveCounts = { 1, 2, 3, 5 };
            string[] funcs = { "simplex", "ridged", "billow" };

            StringBuilder report = new StringBuilder();
            report.AppendLine("NOISE ISOLATOR — raw fractal output, flat grid, no geology/warp/masks");
            report.AppendLine($"Seed={seed}  Res={Res}  DomainSpan={DomainSpan} (normalized units)");
            report.AppendLine("HATCHING index: high-pass gradient-orientation anisotropy. 1.0=isotropic, >1.8=directional.");
            report.AppendLine("A directional result at octaves=1 => artifact is in base snoise lattice itself.");
            report.AppendLine("Directional only at high octaves => artifact is octave rotation/accumulation.");
            report.AppendLine("==================================================");

            float[] field = new float[Res * Res];

            // Two coordinate regimes to isolate large-world-coordinate aliasing.
            //  near: sample domain 0..DomainSpan (small numbers, clean float precision).
            //  far : reproduce the REAL terrain regime — a large base world offset plus a
            //        1km-window fine span. Real terrain feeds ridged/billow with
            //        warpedPos * 0.0022 at world X~300000 => base ~660; a 1km window spans
            //        1000m * 0.0022 = 2.2 units across the tile. If the base snoise lattice
            //        aliases at large coordinates, THIS regime will show hatching that the
            //        near regime does not — proving the artifact is coordinate magnitude,
            //        not the fractal math.
            (string tag, float baseOff, float span)[] regimes =
            {
                ("near", 0f, DomainSpan),
                ("far", 660f, 2.2f),
            };

            foreach (var rg in regimes)
            {
                foreach (string fn in funcs)
                {
                    foreach (int oct in octaveCounts)
                    {
                        for (int z = 0; z < Res; z++)
                        {
                            float nz = rg.baseOff + (z / (float)(Res - 1)) * rg.span;
                            for (int x = 0; x < Res; x++)
                            {
                                float nx = rg.baseOff + (x / (float)(Res - 1)) * rg.span;
                                float2 s = new float2(nx, nz);
                                float v;
                                switch (fn)
                                {
                                    case "simplex": v = WorldMacroGeologyFields.FractalSimplexNoise01(s, seed, oct); break;
                                    case "ridged":  v = WorldMacroGeologyFields.RidgedMultifractal01(s, seed, oct); break;
                                    default:        v = WorldMacroGeologyFields.BillowNoise01(s, seed, oct); break;
                                }
                                field[z * Res + x] = v;
                            }
                        }

                        RenderAndScore(field, $"{rg.tag}_{fn}_oct{oct}", report);
                    }
                }
            }

            File.WriteAllText(Path.Combine(OutDir, "noise_iso_report.txt"), report.ToString());
        }

        private static void RenderAndScore(float[] field, string label, StringBuilder report)
        {
            // Hillshade so the human eye can see corduroy directly.
            Texture2D hill = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            Vector3 lightDir = new Vector3(-1f, 0.6f, 1f).normalized;
            // Amplify: raw noise is 0..1; treat as meters over the tile for visible relief.
            const float vScale = 300f;
            float px = 1f;

            float S(int x, int z)
            {
                int qx = math.clamp(x, 0, Res - 1);
                int qz = math.clamp(z, 0, Res - 1);
                return field[qz * Res + qx] * vScale;
            }

            // High-pass residual (subtract box blur) for honest anisotropy of the fine detail.
            const int BlurR = 6;
            float[] blur = new float[Res * Res];
            float[] resid = new float[Res * Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                {
                    float acc = 0; int c = 0;
                    for (int dx = -BlurR; dx <= BlurR; dx++) { acc += field[z * Res + math.clamp(x + dx, 0, Res - 1)]; c++; }
                    blur[z * Res + x] = acc / c;
                }
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                {
                    float acc = 0; int c = 0;
                    for (int dz = -BlurR; dz <= BlurR; dz++) { acc += blur[math.clamp(z + dz, 0, Res - 1) * Res + x]; c++; }
                    resid[z * Res + x] = field[z * Res + x] - acc / c;
                }
            float RS(int x, int z) => resid[math.clamp(z, 0, Res - 1) * Res + math.clamp(x, 0, Res - 1)];

            const int OriBins = 18;
            double[] ori = new double[OriBins];

            for (int z = 0; z < Res; z++)
            {
                for (int x = 0; x < Res; x++)
                {
                    float hL = S(x - 1, z), hR = S(x + 1, z), hD = S(x, z - 1), hU = S(x, z + 1);
                    Vector3 nrm = new Vector3(hL - hR, 2f * px, hD - hU).normalized;
                    float inten = Mathf.Clamp01(Vector3.Dot(nrm, lightDir)) * 0.85f + 0.15f;
                    hill.SetPixel(x, z, new Color(inten, inten, inten, 1f));

                    float gx = RS(x + 1, z) - RS(x - 1, z);
                    float gz = RS(x, z + 1) - RS(x, z - 1);
                    float gm = Mathf.Sqrt(gx * gx + gz * gz);
                    if (gm > 1e-6f)
                    {
                        float a = Mathf.Atan2(gz, gx);
                        if (a < 0f) a += Mathf.PI;
                        int b = Mathf.Clamp((int)(a / Mathf.PI * OriBins), 0, OriBins - 1);
                        ori[b] += gm;
                    }
                }
            }

            hill.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, label + "_hillshade.png"), hill.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(hill);

            double sum = 0, peak = 0; int peakBin = 0;
            for (int b = 0; b < OriBins; b++) { sum += ori[b]; if (ori[b] > peak) { peak = ori[b]; peakBin = b; } }
            double mean = sum / OriBins;
            double idx = mean > 1e-12 ? peak / mean : 0;
            double angle = (peakBin + 0.5) * (180.0 / OriBins);

            report.AppendLine($"[{label}]  HATCHING index={idx:F2}  peak@{angle:F0}deg");
        }
    }
}
