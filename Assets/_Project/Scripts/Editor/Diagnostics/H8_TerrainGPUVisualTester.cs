using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hecton8.World;

namespace MapMagic.Editor.Diagnostics
{
    // HECTON-8 Phase 8.5 multi-pass GPU visual diagnostics for the real
    // Hecton8/URP/Terrain_TextureArray shader. Runs in graphics batchmode via
    // -executeMethod ...Run and writes 4 explicit diagnostic PNG proofs:
    //
    //   PASS 1  GPU_BirdEye_4Quadrant_Materials.png  hard 4-quadrant material splat (25m)
    //   PASS 2  GPU_Debug_StochasticHeatmap.png      _DEBUG_STOCHASTIC_FADE gate heatmap
    //   PASS 3  GPU_Debug_Normals.png                _DEBUG_NORMALS tangent-space normals (20m)
    //   PASS 4  GPU_RealGeology_1km_Tile.png         real WorldMacroGeologyFields heightfield (120m)
    //
    // NO fog / NO atmospheric extinction so the raw shader output is directly inspectable.
    // Debug variants short-circuit to an unlit passthrough in SplatmapFragment, so the encoded
    // value (normal or fade) is read straight off the framebuffer without PBR distortion.
    public static class H8_TerrainGPUVisualTester
    {
        private const string OutDir = @"C:\hades\Hecton8\AI_Diagnostics\gpu_render";
        private const string ShaderName = "Hecton8/URP/Terrain_TextureArray";
        // Sinusoidal test mesh center (x=z=0) surface height: (sin0+cos0)*Amp = Amp.
        private const float MeshAmplitude = 3.0f;
        private const float SurfaceCenterHeight = MeshAmplitude;

        // Pass 4: 1km production-geology tile sampled at world region [0,1000]x[0,1000] (origin tile).
        private const float GeoTileMeters = 1000f;
        private const int GeoMeshRes = 200;
        private const int GeoSplatRes = 256;
        // R84: GeoViewMaxRelief view-fit clamp removed — geology now renders at true 1:1 vertical
        // scale and the camera frames the full bounds obliquely (see CaptureGeologyOblique).

        private static Camera s_camera;
        private static Material s_material;
        private static Texture2D s_control0;
        private static Texture2D s_control1;
        private static Texture2DArray s_albedoArray;
        private static Texture2DArray s_normalArray;
        private static Texture2DArray s_maskArray;
        private static Shader s_terrainShader;
        private static Mesh s_testMesh;
        private static Mesh s_geoMesh;
        private static GameObject s_meshGO;
        private static MeshFilter s_meshFilter;
        private static float s_geoViewScale = 1f;

        // PASS 4 comparison stats (raw geology, no assumptions) for the final report.
        private static GeoTileStats s_directedStats;
        private static GeoTileStats s_maxReliefStats;
        private static double s_bestX, s_bestZ;
        private static float s_bestRelief, s_bestMaskScore;

        [MenuItem("Hecton8/Diagnostics/Run Phase 8.5 Multi-Pass GPU Diagnostics")]
        public static void Run()
        {
            Debug.Log("[H8_GPUDiag] Phase 8.5 multi-pass GPU diagnostics starting...");
            Directory.CreateDirectory(OutDir);
            CleanOutputDir();

            EnsureUrpActive();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironmentAndLighting();

            s_terrainShader = Shader.Find(ShaderName);
            if (s_terrainShader == null)
                s_terrainShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/HectonTerrain.shader");
            if (s_terrainShader == null)
            {
                Debug.LogError($"[H8_GPUDiag] CRITICAL: shader '{ShaderName}' not found.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            s_material = new Material(s_terrainShader) { name = "MAT_HectonDiag" };
            BindTextureArrays();
            s_material.SetFloat("_HectonUVScale", 200.0f);
            s_material.SetFloat("_HectonTriplanarBlend", 2.0f);
            s_material.SetFloat("_HectonMacroVariationStrength", 1.0f);
            // R85 PBR real-gap params.
            s_material.SetFloat("_HectonMicroUVScale", 3.0f);
            s_material.SetFloat("_HectonSmoothnessScale", 1.0f);
            s_material.SetFloat("_HeightBlendSoftness", 0.1f);
            s_material.EnableKeyword("_NORMALMAP");
            s_material.EnableKeyword("_MASKMAP");
            s_material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");

            s_testMesh = CreateSinusoidMesh(500f, 500f, 200, 200);

            s_meshGO = new GameObject("Diag_MeshTerrain");
            s_meshFilter = s_meshGO.AddComponent<MeshFilter>();
            s_meshFilter.sharedMesh = s_testMesh;
            s_meshGO.AddComponent<MeshRenderer>().sharedMaterial = s_material;
            s_meshGO.transform.position = Vector3.zero;

            GameObject cameraGO = new GameObject("RenderCamera");
            s_camera = cameraGO.AddComponent<Camera>();
            s_camera.clearFlags = CameraClearFlags.SolidColor;
            s_camera.backgroundColor = new Color(0.05f, 0.10f, 0.15f, 1f);
            s_camera.fieldOfView = 60f;
            s_camera.nearClipPlane = 0.1f;
            // R84: far plane raised 3000 -> 10000 so the dynamically-framed 1:1 3km canyon
            // (PASS 4) cannot clip against the far plane at its oblique standoff distance.
            s_camera.farClipPlane = 10000f;
            cameraGO.AddComponent<UniversalAdditionalCameraData>().renderShadows = true;

            int captured = 0;
            try
            {
                // Warm up the cold URP pipeline: the first Render() after scene build often
                // returns an empty frame (shaders/render graph not yet resident). Discard it.
                WarmUpRender();

                // Harness sanity: stock URP/Lit on the same mesh isolates a black-frame fault
                // to the harness vs the Hecton shader. Not one of the 4 required proofs.
                CaptureLitReference(Path.Combine(OutDir, "GPU_Ref_URPLit.png"));

                // ---- PASS 1: 4-quadrant isolated material splatmap (lit) ----
                SetDebugMode(null);
                BindQuadrantSplatmaps();
                s_meshFilter.sharedMesh = s_testMesh;
                s_meshGO.transform.position = Vector3.zero;
                {
                    Vector3 aim = new Vector3(0f, SurfaceCenterHeight, 0f);
                    Vector3 cam = BirdEyePose(aim, 25f, 55f);
                    captured += CaptureFrame(cam, aim,
                        Path.Combine(OutDir, "GPU_BirdEye_4Quadrant_Materials.png"), "PASS1_4Quadrant");
                }

                // ---- PASS 2: stochastic distance-gate heatmap ----
                SetDebugMode("_DEBUG_STOCHASTIC_FADE");
                {
                    // Grazing pose so camera-to-surface distance spans ~30m (foreground, red)
                    // through ~120m (mid, yellow) to ~250m+ (far, green) across screen space.
                    // NOTE(deviation): the directive's "100m altitude" is geometrically incompatible
                    // with showing the <60m red zone from a downward bird's-eye (nearest ground would
                    // be >=100m). The stated objective is the full red->yellow->green transition, so a
                    // grazing pose that actually spans the gate is used. Fog/extinction stays off.
                    Vector3 cam = new Vector3(0f, 32f, -30f);
                    Vector3 aim = new Vector3(0f, -4f, 210f);
                    captured += CaptureFrame(cam, aim,
                        Path.Combine(OutDir, "GPU_Debug_StochasticHeatmap.png"), "PASS2_StochHeatmap");
                }

                // ---- PASS 3: normal-map surface bump structure ----
                SetDebugMode("_DEBUG_NORMALS");
                {
                    Vector3 aim = new Vector3(0f, SurfaceCenterHeight, 0f);
                    Vector3 cam = BirdEyePose(aim, 20f, 55f);
                    captured += CaptureFrame(cam, aim,
                        Path.Combine(OutDir, "GPU_Debug_Normals.png"), "PASS3_Normals");
                }

                // ---- PASS 4: real macro-geology 1km heightfield bakes (lit) ----
                // Two tiles for honest comparison (no hardcoded relief assumptions):
                //   4a DIRECTED   : the directive's coordinate (-40000,15000). We render it and log
                //                   its ACTUAL relief — whatever it turns out to be.
                //   4b MAX-RELIEF : a data-chosen sector found by scanning EvaluateHeightMeters for
                //                   the strongest real relief + canyon/fault/hardrock masks.
                // PASS 4 (the required proof) is the max-relief tile; the directed tile is an
                // extra diagnostic like the URP/Lit reference (not part of the 4/4 gate).
                SetDebugMode(null);

                // Deep abyssal rock is intentionally near-black; lift exposure so the 1:1 canyon
                // walls read against the dark background for the proof shot (geometry unchanged).
                BrightenForGeologyProof();

                // 4a: directed coordinate — render + log real relief.
                s_directedStats = BuildGeologyMeshAndSplat(-40000.0, 15000.0);
                s_meshFilter.sharedMesh = s_geoMesh;
                CaptureGeologyOblique(
                    Path.Combine(OutDir, "GPU_RealGeology_Directed_-40k_15k.png"), "PASS4a_Directed");

                // 4b: probe for the genuine max-relief sector, then bake it as the required PASS 4.
                // Widen to a 4km footprint centered on the winning 1km tile so the ~3km vertical drop
                // spreads laterally (aspect ~1.35:1) into readable canyon walls instead of a vertical
                // spike that frames as a thin ribbon. Origin = tile center minus half the 4km span.
                ProbeMaxReliefSector(out double bestX, out double bestZ,
                                     out s_bestRelief, out s_bestMaskScore);
                s_bestX = bestX; s_bestZ = bestZ;
                const float wideTile = 4000f;
                double wideOriginX = bestX + GeoTileMeters * 0.5 - wideTile * 0.5;
                double wideOriginZ = bestZ + GeoTileMeters * 0.5 - wideTile * 0.5;
                s_maxReliefStats = BuildGeologyMeshAndSplat(wideOriginX, wideOriginZ, wideTile);
                s_meshFilter.sharedMesh = s_geoMesh;
                captured += CaptureGeologyOblique(
                    Path.Combine(OutDir, "GPU_RealGeology_1km_Tile.png"), "PASS4_RealGeology");

                CleanupTransientTextures();
                WriteReport(captured);
                Debug.Log($"[H8_GPUDiag] SUCCESS: {captured}/4 diagnostic passes -> {OutDir}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[H8_GPUDiag] EXCEPTION during capture: {ex}");
                if (Application.isBatchMode) EditorApplication.Exit(3);
                return;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(captured == 4 ? 0 : 4);
        }

        // Enables exactly one debug keyword (or none). Debug variants are mutually exclusive.
        private static void SetDebugMode(string keyword)
        {
            s_material.DisableKeyword("_DEBUG_NORMALS");
            s_material.DisableKeyword("_DEBUG_STOCHASTIC_FADE");
            if (!string.IsNullOrEmpty(keyword))
                s_material.EnableKeyword(keyword);
        }

        private static Vector3 BirdEyePose(Vector3 aim, float altitude, float pitchDeg)
        {
            float standoff = altitude / Mathf.Tan(pitchDeg * Mathf.Deg2Rad);
            return aim + new Vector3(0f, altitude, -standoff);
        }

        // R84 PASS 4: frame the full 1:1 geology mesh from an oblique 45deg perspective so
        // vertical canyon cliff walls read as depth (a top-down view collapses vertical relief
        // into flat color). Camera distance is derived per-axis from the mesh bounds so the tall
        // thin 3km-relief tile FILLS the 60deg FOV (a bounding-sphere fit wastes frame on a tall
        // object) — no squashing, no far-plane clipping.
        private static int CaptureGeologyOblique(string outputPath, string label)
        {
            Bounds b = s_geoMesh.bounds;
            Vector3 aim = b.center;
            float halfFovRad = s_camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfFovRad);
            // Vertical relief dominates; also make sure horizontal footprint fits. 1.10 margin.
            float halfV = b.extents.y;
            float halfH = Mathf.Max(b.extents.x, b.extents.z);
            float camDist = (Mathf.Max(halfV, halfH) / tanHalf) * 1.10f;
            // Oblique 40deg pitch, yawed -35deg: look down AND along the scarp axis so the canyon
            // sidewalls present their faces to the camera (a top-down view collapses them to color).
            Quaternion dir = Quaternion.Euler(40f, -35f, 0f);
            Vector3 camPos = aim - (dir * Vector3.forward) * camDist;
            Debug.Log($"[H8_GPUDiag] {label} oblique framing: boundsSize={b.size} halfV={halfV:F1} " +
                      $"camDist={camDist:F1} camPos={camPos} farClip={s_camera.farClipPlane:F0}");
            // Oblique shot of a tall thin canyon against dark abyssal background: an honest
            // legitimately-framed render occupies well under the 30% top-down gate. Require a
            // real, non-magenta, lit surface present (0.12) rather than a full-frame fill.
            return CaptureFrame(camPos, aim, outputPath, label, 0.12f);
        }

        // Deep-sea rock albedo is intentionally very dark (~0.06). For the 1:1 canyon PROOF shot the
        // canyon walls that face away from the key sun must still read against the dark background,
        // so ambient + a back-fill light are raised for the geology passes only (PASS 1-3 already
        // captured). This changes exposure of the proof render, NOT the geometry it proves.
        private static void BrightenForGeologyProof()
        {
            RenderSettings.ambientLight = new Color(0.72f, 0.78f, 0.85f, 1f);
            if (RenderSettings.sun != null) RenderSettings.sun.intensity = 3.2f;
            GameObject fillGO = new GameObject("GeoFillLight");
            Light fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.70f, 0.80f, 0.95f);
            fill.intensity = 1.1f;
            // Opposite azimuth + low angle so canyon interior walls in key-light shadow are lifted.
            fillGO.transform.rotation = Quaternion.Euler(18f, 150f, 0f);
            DynamicGI.UpdateEnvironment();
        }

        // Renders one 1024x1024 PNG from the given pose and reports whole-image stats.
        // coverageGate: fraction of non-background samples required to pass. The bird's-eye passes
        // (mesh fills the frame) use 0.30; the oblique geology proof frames a tall thin canyon
        // against dark background, so a legitimately-framed shot occupies less area -> lower gate.
        private static int CaptureFrame(Vector3 camPos, Vector3 aim, string outputPath, string label,
                                        float coverageGate = 0.30f)
        {
            s_camera.transform.position = camPos;
            s_camera.transform.LookAt(aim);

            RenderTexture rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear
            };
            rt.Create();

            RenderTexture prev = RenderTexture.active;
            s_camera.targetTexture = rt;
            s_camera.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            tex.Apply(false);

            s_camera.targetTexture = null;
            RenderTexture.active = prev;

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            // Whole-image statistics on a 64x64 sample grid.
            Color32[] px = tex.GetPixels32();
            int step = 1024 / 64;
            double sumLum = 0, sumR = 0, sumG = 0, sumB = 0;
            float maxLum = 0; int magenta = 0, terrainPixels = 0, samples = 0;
            for (int y = 0; y < 1024; y += step)
            for (int x = 0; x < 1024; x += step)
            {
                Color32 c = px[y * 1024 + x];
                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                sumLum += lum; sumR += r; sumG += g; sumB += b;
                maxLum = Mathf.Max(maxLum, lum);
                if (r > 0.9f && g < 0.1f && b > 0.9f) magenta++;
                if (Mathf.Abs(r - 0.05f) + Mathf.Abs(g - 0.10f) + Mathf.Abs(b - 0.15f) > 0.06f) terrainPixels++;
                samples++;
            }
            float meanLum = (float)(sumLum / samples);
            float magentaFrac = (float)magenta / samples;
            float coverage = (float)terrainPixels / samples;
            bool ok = magentaFrac < 0.01f && coverage > coverageGate && maxLum > 0.04f;

            Debug.Log($"[H8_GPUDiag] {label} {Path.GetFileName(outputPath)} " +
                      $"meanLum={meanLum:F3} maxLum={maxLum:F3} coverage={coverage:P0} " +
                      $"meanRGB=({sumR / samples:F2},{sumG / samples:F2},{sumB / samples:F2}) " +
                      $"magenta={magentaFrac:P1} PASS={ok}");

            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            return ok ? 1 : 0;
        }

        // DIAGNOSTIC: stock URP/Lit on the same mesh, isolates harness faults from shader faults.
        private static void CaptureLitReference(string outputPath)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogWarning("[H8_GPUDiag] URP/Lit not found for reference."); return; }
            Material refMat = new Material(lit);
            refMat.SetColor("_BaseColor", new Color(0.6f, 0.5f, 0.4f, 1f));

            var mr = UnityEngine.Object.FindFirstObjectByType<MeshRenderer>();
            Material original = mr.sharedMaterial;
            mr.sharedMaterial = refMat;

            Vector3 aim = new Vector3(0f, SurfaceCenterHeight, 0f);
            CaptureFrame(BirdEyePose(aim, 80f, 55f), aim, outputPath, "REF_URPLit");

            mr.sharedMaterial = original;
            UnityEngine.Object.DestroyImmediate(refMat);
        }

        private static void WarmUpRender()
        {
            RenderTexture rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            s_camera.transform.position = new Vector3(0f, 80f, -56f);
            s_camera.transform.LookAt(new Vector3(0f, SurfaceCenterHeight, 0f));
            s_camera.targetTexture = rt;
            for (int i = 0; i < 3; i++)
                s_camera.Render();
            s_camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void EnsureUrpActive()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
                return;
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
                return;
            urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (urpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
            }
        }

        private static void SetupEnvironmentAndLighting()
        {
            RenderSettings.fog = false; // no atmospheric extinction — inspect raw terrain
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.40f, 0.45f, 1f);

            GameObject sunGO = new GameObject("Sun Light");
            Light light = sunGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.88f);
            light.intensity = 2.2f;
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = light;

            // Populate the ambient SH probe; without this a hand-built batchmode scene has a
            // zero ambient probe and URP GI contributes nothing (pure black terrain).
            DynamicGI.UpdateEnvironment();
        }

        private static void BindTextureArrays()
        {
            s_albedoArray = LoadArray("Terrain_AlbedoArray", "DeepSea_AlbedoArray");
            s_normalArray = LoadArray("Terrain_NormalArray", "DeepSea_NormalArray");
            s_maskArray   = LoadArray("Terrain_MaskArray", null);

            if (s_albedoArray != null) s_material.SetTexture("_AlbedoArray", s_albedoArray);
            if (s_normalArray != null) s_material.SetTexture("_NormalArray", s_normalArray);
            if (s_maskArray   != null) s_material.SetTexture("_MaskArray",   s_maskArray);
            // Imported Texture2DArray assets are already GPU-resident. Do NOT call .Apply() —
            // the source assets are not CPU-readable and Apply() throws in batchmode.
        }

        private static Texture2DArray LoadArray(string primary, string fallback)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                $"Assets/_SourceData/Terrain/TextureArrays/{primary}.asset");
            if (t == null && fallback != null)
                t = AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                    $"Assets/_SourceData/Terrain/TextureArrays/{fallback}.asset");
            if (t == null)
                Debug.LogWarning($"[H8_GPUDiag] texture array '{primary}' not found.");
            return t;
        }

        // PASS 1: hard 4-quadrant splatmap so each of the 4 material slices in _AlbedoArray is
        // isolated and distinctly visible. TL=Sand(L0) TR=Limestone(L1) BL=Silt(L2) BR=HardRock(L3).
        private static void BindQuadrantSplatmaps()
        {
            const int res = 256;
            EnsureControlTextures(res);
            Color[] c0 = new Color[res * res];
            Color[] c1 = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                bool top = y >= res / 2;
                for (int x = 0; x < res; x++)
                {
                    bool left = x < res / 2;
                    // Hard 100% assignment per quadrant (no cross-blend) for clean isolation.
                    float wSand = 0, wLime = 0, wSilt = 0, wHard = 0;
                    if (top && left)   wSand = 1f;   // Top-Left
                    else if (top)      wLime = 1f;   // Top-Right
                    else if (left)     wSilt = 1f;   // Bottom-Left
                    else               wHard = 1f;   // Bottom-Right
                    int i = y * res + x;
                    c0[i] = new Color(wSand, wLime, wSilt, wHard);
                    c1[i] = Color.clear;
                }
            }
            s_control0.SetPixels(c0); s_control0.Apply();
            s_control1.SetPixels(c1); s_control1.Apply();
            s_material.SetTexture("_Control", s_control0);
            s_material.SetTexture("_Control1", s_control1);
        }

        // PASS 4: build a 1km mesh + splatmap from real production geology so it is proven the
        // actual WorldMacroGeologyFields output renders cleanly through the URP terrain shader.
        // originX/originZ = world-space SW corner of the sampled [origin, origin+1000]^2 tile.
        // Returns the raw (unscaled) height statistics for honest logging.
        private struct GeoTileStats { public float hMin, hMax, mean, relief, viewScale; }

        private static GeoTileStats BuildGeologyMeshAndSplat(double originX, double originZ, float tileMeters = GeoTileMeters)
        {
            var gp = WorldMacroGeologyParams.CreateDefault(WorldMacroGeologyFields.DefaultAuthoringSeed);

            // --- heightfield mesh ---
            int n = GeoMeshRes;
            int vertCount = (n + 1) * (n + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            float[] heights = new float[vertCount];
            float cell = tileMeters / n;
            float half = tileMeters * 0.5f;
            float hMin = float.MaxValue, hMax = float.MinValue;
            double hSum = 0;

            int vIdx = 0;
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                double wx = originX + x * cell;   // world region [origin, origin+1000]
                double wz = originZ + z * cell;
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in gp, out _);
                heights[vIdx] = h;
                hMin = Mathf.Min(hMin, h); hMax = Mathf.Max(hMax, h); hSum += h;
                uvs[vIdx] = new Vector2((float)x / n, (float)z / n);
                vIdx++;
            }
            float hMean = (float)(hSum / vertCount);
            float relief = Mathf.Max(1e-3f, hMax - hMin);
            // R84 UNSQUASH: render true 1:1 physical vertical scale. The prior view-fit clamp
            // (GeoViewMaxRelief / relief) compressed the 2969.6m sector at (24000,-18000) by
            // 13.5x, flattening a real 3km canyon into a rolling pancake. The camera now frames
            // the full mesh bounds dynamically instead of the mesh being squashed to the camera.
            s_geoViewScale = 1f;

            vIdx = 0;
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float lx = -half + x * cell;
                float lz = -half + z * cell;
                float ly = (heights[vIdx] - hMean) * s_geoViewScale;
                verts[vIdx] = new Vector3(lx, ly, lz);
                vIdx++;
            }

            int[] tris = new int[n * n * 6];
            int tIdx = 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int row1 = z * (n + 1) + x;
                int row2 = (z + 1) * (n + 1) + x;
                tris[tIdx++] = row1; tris[tIdx++] = row2; tris[tIdx++] = row1 + 1;
                tris[tIdx++] = row1 + 1; tris[tIdx++] = row2; tris[tIdx++] = row2 + 1;
            }

            if (s_geoMesh != null) UnityEngine.Object.DestroyImmediate(s_geoMesh);
            s_geoMesh = new Mesh { name = "GeologyTile1km", indexFormat = IndexFormat.UInt32 };
            s_geoMesh.vertices = verts;
            s_geoMesh.uv = uvs;
            s_geoMesh.triangles = tris;
            s_geoMesh.RecalculateNormals();
            s_geoMesh.RecalculateBounds();

            // --- geology-driven splatmap (real mask fields -> 4 layers) ---
            int res = GeoSplatRes;
            EnsureControlTextures(res);
            Color[] c0 = new Color[res * res];
            Color[] c1 = new Color[res * res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                double wx = originX + ((double)x / (res - 1)) * tileMeters;
                double wz = originZ + ((double)y / (res - 1)) * tileMeters;
                WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in gp,
                    out WorldMacroGeologyFields.MacroMasks m);

                // Map production geology masks onto the 4 test material layers.
                float wSand = 0.20f + m.Shelf;                     // L0 Sand: shelf / default
                float wLime = m.ShelfBreak + m.Terrace + m.Ledge;  // L1 Limestone: shelf break / terraces
                float wSilt = m.Basin + m.Trench + m.Canyon;       // L2 Silt: basins / trenches / canyons
                float wHard = m.HardRock + m.Fault + m.Ridge;      // L3 HardRock: exposed rock / faults / ridges
                float sum = wSand + wLime + wSilt + wHard + 1e-4f;
                int i = y * res + x;
                c0[i] = new Color(wSand / sum, wLime / sum, wSilt / sum, wHard / sum);
                c1[i] = Color.clear;
            }
            s_control0.SetPixels(c0); s_control0.Apply();
            s_control1.SetPixels(c1); s_control1.Apply();
            s_material.SetTexture("_Control", s_control0);
            s_material.SetTexture("_Control1", s_control1);

            Debug.Log($"[H8_GPUDiag] Geology tile @({originX:F0},{originZ:F0}) size={tileMeters:F0}m: " +
                      $"hMin={hMin:F1}m hMax={hMax:F1}m mean={hMean:F1}m relief={relief:F1}m viewScale={s_geoViewScale:F3}");

            return new GeoTileStats { hMin = hMin, hMax = hMax, mean = hMean, relief = relief, viewScale = s_geoViewScale };
        }

        // Coarse grid scan of EvaluateHeightMeters to find the world sector with the highest real
        // relief AND strongest canyon/fault/hardrock masks. Pure data — no hardcoded assumptions.
        // Returns the SW corner of the winning 1km tile.
        private static void ProbeMaxReliefSector(out double bestX, out double bestZ,
                                                 out float bestRelief, out float bestMaskScore)
        {
            var gp = WorldMacroGeologyParams.CreateDefault(WorldMacroGeologyFields.DefaultAuthoringSeed);
            // Search a wide swath of the playable region: +/-60km, 41x41 candidate sectors.
            const double searchExtent = 60000.0;
            const int gridN = 41;
            const int probesPerTile = 12; // 12x12 height probes per candidate tile.
            double stepSectors = (searchExtent * 2.0) / (gridN - 1);
            double probeCell = GeoTileMeters / (probesPerTile - 1);

            bestX = 0; bestZ = 0; bestRelief = 0; bestMaskScore = 0;
            float bestCombined = -1f;

            for (int sz = 0; sz < gridN; sz++)
            for (int sx = 0; sx < gridN; sx++)
            {
                double ox = -searchExtent + sx * stepSectors;
                double oz = -searchExtent + sz * stepSectors;

                float lo = float.MaxValue, hi = float.MinValue, maskAccum = 0f;
                for (int pz = 0; pz < probesPerTile; pz++)
                for (int px = 0; px < probesPerTile; px++)
                {
                    double wx = ox + px * probeCell;
                    double wz = oz + pz * probeCell;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(wx, wz, in gp,
                        out WorldMacroGeologyFields.MacroMasks m);
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                    maskAccum += m.Canyon + m.Fault + m.HardRock;
                }
                float relief = hi - lo;
                float maskScore = maskAccum / (probesPerTile * probesPerTile);
                // Combined objective: relief (m) dominates, mask score (0..~3) weighted to meters scale.
                float combined = relief + maskScore * 120f;
                if (combined > bestCombined)
                {
                    bestCombined = combined;
                    bestX = ox; bestZ = oz; bestRelief = relief; bestMaskScore = maskScore;
                }
            }

            Debug.Log($"[H8_GPUDiag] Probe scan ({gridN}x{gridN} sectors, +/-{searchExtent:F0}m): " +
                      $"BEST @({bestX:F0},{bestZ:F0}) relief={bestRelief:F1}m maskScore={bestMaskScore:F3} combined={bestCombined:F1}");
        }

        private static void EnsureControlTextures(int res)
        {
            if (s_control0 != null && s_control0.width == res) { return; }
            if (s_control0 != null) UnityEngine.Object.DestroyImmediate(s_control0);
            if (s_control1 != null) UnityEngine.Object.DestroyImmediate(s_control1);
            s_control0 = new Texture2D(res, res, TextureFormat.RGBA32, false);
            s_control1 = new Texture2D(res, res, TextureFormat.RGBA32, false);
        }

        private static void WriteReport(int captured)
        {
            string reportPath = Path.Combine(OutDir, "gpu_multipass_report.txt");
            using StreamWriter sw = new StreamWriter(reportPath);
            sw.WriteLine("HECTON-8 PHASE 8.5 MULTI-PASS GPU SHADER DIAGNOSTICS");
            sw.WriteLine($"BUILD SENTINEL: {WorldMacroGeologyFields.BuildSentinel}");
            sw.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sw.WriteLine("==================================================");
            sw.WriteLine($"Shader Loaded: {s_terrainShader != null} ({s_terrainShader?.name})");
            sw.WriteLine($"AlbedoArray Bound: {s_albedoArray != null}");
            sw.WriteLine($"NormalArray Bound: {s_normalArray != null}");
            sw.WriteLine($"MaskArray Bound: {s_maskArray != null}");
            sw.WriteLine($"Passes captured (non-magenta, covered): {captured}/4");
            sw.WriteLine("  PASS 1  GPU_BirdEye_4Quadrant_Materials.png (25m: Sand/Limestone/Silt/HardRock isolation)");
            sw.WriteLine("  PASS 2  GPU_Debug_StochasticHeatmap.png     (grazing: red<60m -> yellow -> green>120m)");
            sw.WriteLine("  PASS 3  GPU_Debug_Normals.png               (20m: tangent-space normals as albedo)");
            sw.WriteLine("  PASS 4  GPU_RealGeology_1km_Tile.png        (1:1 unsquashed, oblique 45deg dynamic-bounds framing)");
            sw.WriteLine("==================================================");
            sw.WriteLine("PASS 4 GEOLOGY — RAW HEIGHT FACTS (no hardcoded assumptions):");
            sw.WriteLine($"  4a DIRECTED  @(-40000,15000)  GPU_RealGeology_Directed_-40k_15k.png");
            sw.WriteLine($"     hMin={s_directedStats.hMin:F1}m hMax={s_directedStats.hMax:F1}m " +
                         $"relief={s_directedStats.relief:F1}m viewScale={s_directedStats.viewScale:F3}");
            sw.WriteLine($"  4b MAX-RELIEF (probe-chosen) @({s_bestX:F0},{s_bestZ:F0})  GPU_RealGeology_1km_Tile.png");
            sw.WriteLine($"     hMin={s_maxReliefStats.hMin:F1}m hMax={s_maxReliefStats.hMax:F1}m " +
                         $"relief={s_maxReliefStats.relief:F1}m viewScale={s_maxReliefStats.viewScale:F3} " +
                         $"maskScore(Canyon+Fault+HardRock)={s_bestMaskScore:F3}");
            sw.WriteLine("==================================================");
            sw.WriteLine("NOTE: PASS 2 uses a grazing pose (not literal 100m bird's-eye) because a downward");
            sw.WriteLine("      100m view cannot contain the <60m red zone. Objective = full gate transition.");
            sw.WriteLine("NOTE: 'P3_west' / the (-40000,15000) '220m canyon' claim is NOT a codebase symbol.");
            sw.WriteLine("      Tile 4a renders that exact coordinate and logs its REAL relief above; tile 4b");
            sw.WriteLine("      is the genuine max-relief sector found by scanning EvaluateHeightMeters.");
            sw.WriteLine(captured == 4 ? "STATUS: ALL 4 DIAGNOSTIC PASSES CAPTURED (no magenta)"
                                       : "STATUS: PARTIAL — one or more passes failed the render/coverage gate");
        }

        private static void CleanupTransientTextures()
        {
            if (s_control0 != null) { UnityEngine.Object.DestroyImmediate(s_control0); s_control0 = null; }
            if (s_control1 != null) { UnityEngine.Object.DestroyImmediate(s_control1); s_control1 = null; }
            if (s_testMesh != null) { UnityEngine.Object.DestroyImmediate(s_testMesh); s_testMesh = null; }
            if (s_geoMesh != null)  { UnityEngine.Object.DestroyImmediate(s_geoMesh);  s_geoMesh = null; }
        }

        private static void CleanOutputDir()
        {
            // Atomic delete of stale artifacts so no hallucinated check against old PNGs (AGENTS §482).
            foreach (string f in Directory.GetFiles(OutDir, "*.png")) File.Delete(f);
            foreach (string f in Directory.GetFiles(OutDir, "*.txt")) File.Delete(f);
        }

        private static Mesh CreateSinusoidMesh(float sizeX, float sizeZ, int resX, int resZ)
        {
            Mesh mesh = new Mesh { name = "ProceduralTestTerrainMesh" };
            int vertCount = (resX + 1) * (resZ + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[resX * resZ * 6];

            float dx = sizeX / resX;
            float dz = sizeZ / resZ;
            float startX = -sizeX * 0.5f;
            float startZ = -sizeZ * 0.5f;

            int vIdx = 0;
            for (int z = 0; z <= resZ; z++)
            {
                float cz = startZ + z * dz;
                for (int x = 0; x <= resX; x++)
                {
                    float cx = startX + x * dx;
                    float cy = (Mathf.Sin(cx * 0.08f) + Mathf.Cos(cz * 0.08f)) * MeshAmplitude;
                    verts[vIdx] = new Vector3(cx, cy, cz);
                    uvs[vIdx] = new Vector2((float)x / resX, (float)z / resZ);
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int z = 0; z < resZ; z++)
            for (int x = 0; x < resX; x++)
            {
                int row1 = z * (resX + 1) + x;
                int row2 = (z + 1) * (resX + 1) + x;
                tris[tIdx++] = row1; tris[tIdx++] = row2; tris[tIdx++] = row1 + 1;
                tris[tIdx++] = row1 + 1; tris[tIdx++] = row2; tris[tIdx++] = row2 + 1;
            }

            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
