using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    public static class CleanRoomTerrainTest
    {
        private const int GridRadius = 1;
        private const int TerrainGridSize = 3;
        private const int HeightResolution = 1025;
        private const int AlphaResolution = 1024;
        private const int TerrainLayerCount = 8;
        private const float ChunkSizeMeters = 1000f;
        private const float TerrainHeightRangeMeters = 5200f;
        private const float TerrainBaseY = -5000f;
        private const float MinTerrainHeightMeters = -5000f;
        private const float MaxTerrainHeightMeters = 200f;

        private const string SiteArgument = "-cleanRoomSite";

        /// <summary>
        /// Where in the world the terrain is SAMPLED from. The meshes always stand at the local origin;
        /// only the coordinates fed to WorldMacroGeologyFields move. Keeping the two apart is not
        /// tidiness, it is required: a Terrain transform at x = 777 000 leaves a float32 mantissa step
        /// of about 0.06 m, so the render would quantise before the geology was ever in question.
        ///
        /// Why this exists at all. Until 2026-08-10 the clean room could only ever look at
        /// (0,0)..(1000,1000), and WorldMacroGeologyCleanRoomCoverageTests measured what is there:
        /// mean slope 12.2 deg, Shelf mask mean 0.001, and Trench, PlateEdge, Canyon, Terrace, River,
        /// Lake, Strata, Fold, Mesa, Continentality, Reef, Ledge and BrinePool all identically zero -
        /// 15 of 24 masks dead. It is a quiet patch of abyssal basin in the corner of the world, calmer
        /// than the P3_west control site. Every X-Ray anyone has looked at came from there.
        ///
        /// That is how a re-render taken after 126 lines of change to EvaluateHeightMeters came back
        /// BIT-IDENTICAL on all four deterministic X-Rays: the work was on the shelf break and the
        /// trench, and neither is inside the frame. The pictures were not wrong, they were of somewhere
        /// else.
        /// </summary>
        private static double2 s_SampleOriginXZ = new double2(0.0, 0.0);

        private static string s_SiteLabel = "origin";

        /// <summary>
        /// Probe sites INSIDE the world, chosen by percentile of the in-world 1 km slope distribution
        /// by WorldMacroGeologyInWorldAtlasTests rather than by hand.
        ///
        /// WorldExtentMeters is 30000 and no scene, prefab or asset in the project overrides it;
        /// ResolveMinimumChunkRange bounds the chunk grid to +/-15000 m. The world is a 30 km square.
        /// The previous site list here (p1..p5, up to 777 km out) put four of five renders outside it,
        /// so a picture taken at 'p5' showed a place the game will never emit.
        ///
        /// Mean slope over a 1 km window at each, measured 2026-08-10 at seed 880031, with the
        /// in-world percentile each represents:
        ///   w1 p2   9.3 deg,   65 m relief      w4 p75 43.4 deg, 1136 m
        ///   w2 p25 18.2 deg,  372 m             w5 p98 57.0 deg, 1910 m
        ///   w3 p50 31.1 deg,  701 m
        /// The world spans 7.6 deg at (11896, -14400) to 63.0 deg at (-11896, 5635).
        /// </summary>
        private static readonly (string Name, double X, double Z)[] KnownSites =
        {
            ("origin", 0.0, 0.0),
            ("w1", 11896.0, -13148.0),
            ("w2", 5635.0, -3130.0),
            ("w3", 9391.0, -10643.0),
            ("w4", 6887.0, -6887.0),
            ("w5", -11896.0, 4383.0)
        };

        [MenuItem("Hecton8/Tests/Clean Room Terrain")]
        public static void RunTest()
        {
            ExecuteInternal(exitOnFinish: false);
        }

        public static void Execute()
        {
            ResolveRequestedSite();
            ExecuteInternal(exitOnFinish: true);
        }

        /// <summary>
        /// Reads -cleanRoomSite from the command line: either a name from KnownSites, or a raw
        /// "x,z" pair in metres. Absent or unparseable, the site stays at the origin and says so.
        ///
        /// System.Environment is spelled out in full deliberately. This file sits in namespace
        /// Hecton8.Editor, the project declares its own Hecton8.Environment namespace
        /// (HectonBiomeProfile.cs and others), and that one wins over System inside any Hecton8.*
        /// namespace - resolving to a type with no GetCommandLineArgs. The same trap is recorded at
        /// H8_ShaderCompileGate.cs:317-321 and it caught H8_HeadlessPlayModeProbe before that.
        /// </summary>
        private static void ResolveRequestedSite()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], SiteArgument, StringComparison.OrdinalIgnoreCase))
                    continue;

                string requested = arguments[i + 1].Trim();

                foreach ((string Name, double X, double Z) site in KnownSites)
                {
                    if (!string.Equals(site.Name, requested, StringComparison.OrdinalIgnoreCase))
                        continue;

                    s_SampleOriginXZ = new double2(site.X, site.Z);
                    s_SiteLabel = site.Name;
                    return;
                }

                string[] pair = requested.Split(',');
                if (pair.Length == 2
                    && double.TryParse(pair[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double x)
                    && double.TryParse(pair[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double z))
                {
                    s_SampleOriginXZ = new double2(x, z);
                    s_SiteLabel = $"{x:0}_{z:0}";
                    return;
                }

                Debug.LogWarning(
                    $"[CleanRoom] {SiteArgument} '{requested}' is neither a known site nor an " +
                    "'x,z' metre pair, so the render stays at the world origin - which is a quiet " +
                    "basin tile with no shelf break and no trench in it. Known sites: " +
                    "origin, w1 (flattest), w2, w3 (typical), w4, w5 (steepest).");
                return;
            }
        }

        private static void ExecuteInternal(bool exitOnFinish)
        {
            int exitCode = 0;
            VolumeProfile profile = null;
            try
            {
                string artifactDir = ResolveArtifactDirectory();
                Directory.CreateDirectory(artifactDir);
                Debug.Log(
                    $"[CleanRoom] Starting clean-room terrain proof. Site '{s_SiteLabel}' at world " +
                    $"({s_SampleOriginXZ.x:F0}, {s_SampleOriginXZ.y:F0})m, sampling " +
                    $"{TerrainGridSize * ChunkSizeMeters:F0}m of ground with the X-Rays cut from the " +
                    $"WHOLE {TerrainGridSize}x{TerrainGridSize} grid, i.e. world " +
                    $"({s_SampleOriginXZ.x - ChunkSizeMeters:F0}, {s_SampleOriginXZ.y - ChunkSizeMeters:F0}) " +
                    $"to ({s_SampleOriginXZ.x + 2 * ChunkSizeMeters:F0}, {s_SampleOriginXZ.y + 2 * ChunkSizeMeters:F0}). " +
                    $"Artifacts: {artifactDir}");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                Selection.activeGameObject = null;
                Selection.objects = Array.Empty<Object>();

                ConfigureLighting(out profile);
                Material baseMaterial = BuildBaseTerrainMaterial();
                TerrainLayer[] terrainLayers = BuildTerrainLayers();
                WorldMacroGeologyParams macroParams = WorldMacroGeologyParams.CreateDefault(WorldMacroGeologyFields.DefaultAuthoringSeed);
                macroParams.WaterSurfaceY = 0f;
                macroParams.DetailProbeMeters = 16f;

                UnityEngine.Terrain[,] terrains = new UnityEngine.Terrain[TerrainGridSize, TerrainGridSize];

                // X-Rays are cut from the WHOLE 3x3 grid, not the centre chunk.
                //
                // WHY THE FRAME HAD TO GROW, measured 2026-08-10. The centre chunk is 1000 m across
                // and the shelf break's delivered 0.1-to-0.9 band measures 3150 m
                // (WorldMacroGeologyShelfWidthDeliveryTests). A 1000 m window therefore CANNOT
                // contain a shelf transition - not at the origin, and not at any of the five
                // in-world atlas sites either: the shelf mask was measured over a 1 km tile at all
                // six and not one of them crosses from below 0.25 to above 0.75. The picture was
                // structurally incapable of showing the single largest vertical move in the
                // generator, whatever it was aimed at.
                //
                // That is why CleanRoomTile_ContainsTheShelfBreak failed and why the fix is a wider
                // frame rather than a looser threshold. Its own history records what loosening
                // costs: an earlier version asked only for a 0.05 peak-to-trough swing and PASSED
                // on 0.057 while the mask's mean over the tile was 0.001 - the shelf grazing one
                // corner, reported as coverage.
                //
                // 3000 m is the smallest frame the existing grid can give and it clears the 3150 m
                // band's half-crossing with room to spare. The grid was already being built; only
                // the centre chunk's diagnostics were being kept, so eight ninths of the terrain
                // this tool generates was being discarded before anyone looked at it.
                const int StitchedHeightResolution =
                    HeightResolution + (TerrainGridSize - 1) * (HeightResolution - 1);
                const int StitchedAlphaResolution = AlphaResolution * TerrainGridSize;

                float[,] gridWorldHeights = new float[StitchedHeightResolution, StitchedHeightResolution];
                float[,] gridSlope = new float[StitchedAlphaResolution, StitchedAlphaResolution];
                float[,] gridCurvature = new float[StitchedAlphaResolution, StitchedAlphaResolution];
                int[,] gridMaterial = new int[StitchedAlphaResolution, StitchedAlphaResolution];

                // Vertical extent of the WHOLE grid, not the centre chunk. On a continental slope the
                // neighbouring chunks continue the ramp for another kilometre each way, so framing the
                // camera on the centre chunk's mid-height puts it inside the hillside. See BuildCamera.
                float gridMinY = float.MaxValue;
                float gridMaxY = float.MinValue;

                for (int row = -GridRadius; row <= GridRadius; row++)
                {
                    for (int col = -GridRadius; col <= GridRadius; col++)
                    {
                        float originX = col * ChunkSizeMeters;
                        float originZ = row * ChunkSizeMeters;
                        UnityEngine.Terrain terrain = BuildTerrainChunk(
                            row,
                            col,
                            originX,
                            originZ,
                            in macroParams,
                            terrainLayers,
                            baseMaterial,
                            true,
                            out float[,] worldHeights,
                            out float[,] slope01,
                            out float[,] curvature01,
                            out int[,] dominantMaterial,
                            out float chunkMinY,
                            out float chunkMaxY);

                        gridMinY = math.min(gridMinY, chunkMinY);
                        gridMaxY = math.max(gridMaxY, chunkMaxY);

                        terrains[row + GridRadius, col + GridRadius] = terrain;

                        // Terrain buffers are indexed [z, x], so the grid row is the first index.
                        // Heights carry a shared edge sample between neighbours: each chunk
                        // contributes HeightResolution-1 rows and the last chunk adds the closing
                        // edge, which is why the stitched size is 1025 + 2*1024 rather than 3*1025.
                        int heightOffsetZ = (row + GridRadius) * (HeightResolution - 1);
                        int heightOffsetX = (col + GridRadius) * (HeightResolution - 1);
                        bool lastRow = row == GridRadius;
                        bool lastCol = col == GridRadius;
                        int heightRows = lastRow ? HeightResolution : HeightResolution - 1;
                        int heightCols = lastCol ? HeightResolution : HeightResolution - 1;
                        for (int z = 0; z < heightRows; z++)
                            for (int x = 0; x < heightCols; x++)
                                gridWorldHeights[heightOffsetZ + z, heightOffsetX + x] = worldHeights[z, x];

                        int alphaOffsetZ = (row + GridRadius) * AlphaResolution;
                        int alphaOffsetX = (col + GridRadius) * AlphaResolution;
                        for (int z = 0; z < AlphaResolution; z++)
                        {
                            for (int x = 0; x < AlphaResolution; x++)
                            {
                                gridSlope[alphaOffsetZ + z, alphaOffsetX + x] = slope01[z, x];
                                gridCurvature[alphaOffsetZ + z, alphaOffsetX + x] = curvature01[z, x];
                                gridMaterial[alphaOffsetZ + z, alphaOffsetX + x] = dominantMaterial[z, x];
                            }
                        }
                    }
                }

                StitchTerrainGrid(terrains);

                // Aim the camera at the surface that was actually generated, not at a constant, and
                // measure that surface across the whole grid rather than the centre chunk alone.
                Camera camera = BuildCamera(gridMinY, gridMaxY);

                // Per-site filenames. Without them each render silently overwrites the last and the
                // only way to compare two sites is to remember what the previous picture looked like.
                // The origin keeps its historical unsuffixed names so nothing that references them
                // breaks.
                string suffix = string.Equals(s_SiteLabel, "origin", StringComparison.Ordinal)
                    ? string.Empty
                    : "_" + s_SiteLabel;

                ExportSplatmapComposite(terrains, Path.Combine(artifactDir, $"Debug_Splatmap_3x3{suffix}.png"));
                RenderBeauty(camera, Path.Combine(artifactDir, $"CleanRoom_Beauty{suffix}.png"));
                BiomeTransitionShot transitionShot = FindBiomeTransitionShot(terrains, in macroParams);
                RenderTransitionBeauty(camera, transitionShot, Path.Combine(artifactDir, $"Naked_Biome_Transition{suffix}.png"));
                ExportXRayMaps(artifactDir, suffix, gridWorldHeights, gridSlope, gridCurvature, gridMaterial);

                Debug.Log("[CleanRoom] Clean-room terrain proof complete.");
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogError($"[CleanRoom] FAILURE: {ex}");
            }
            finally
            {
                if (profile != null)
                    Object.DestroyImmediate(profile);

                if (exitOnFinish && Application.isBatchMode)
                    EditorApplication.Exit(exitCode);
            }
        }

        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ResolveArtifactDirectory()
        {
            return Path.Combine(ResolveProjectRoot(), "Docs", "Reports", "CleanRoom");
        }

        private static void ConfigureLighting(out VolumeProfile profile)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.065f, 0.085f, 1f);
            RenderSettings.fog = false;

            GameObject sunGo = new GameObject("CleanRoom_WhiteSun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 2.35f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(42f, 53f, 0f);

            GameObject fillGo = new GameObject("CleanRoom_AbyssFill");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.40f, 0.60f, 0.82f, 1f);
            fill.intensity = 0.38f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(18f, -135f, 0f);

            GameObject volumeGo = new GameObject("CleanRoom_ACESVolume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(2.55f);
            color.contrast.Override(-8f);
            color.saturation.Override(4f);
        }

        private static Material BuildBaseTerrainMaterial()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
            Material material;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                Shader shader = Shader.Find("Hecton8/URP/Terrain_TextureArray");
                if (shader == null)
                    shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/HectonTerrain.shader");
                if (shader == null)
                    throw new InvalidOperationException("Hecton terrain shader could not be loaded.");

                material = new Material(shader);
            }

            material.name = "CleanRoomTerrainMaterial_Runtime";
            Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
            Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");
            if (albedo != null) material.SetTexture("_AlbedoArray", albedo);
            if (normal != null) material.SetTexture("_NormalArray", normal);
            if (mask != null) material.SetTexture("_MaskArray", mask);
            material.SetFloat("_HectonUVScale", 400f);
            material.SetFloat("_HectonTriplanarBlend", 8f);
            if (material.HasProperty("_HectonMacroVariationStrength"))
                material.SetFloat("_HectonMacroVariationStrength", 1.0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_MASKMAP");
            material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            return material;
        }

        private static TerrainLayer[] BuildTerrainLayers()
        {
            string[] candidatePaths =
            {
                "Assets/_Project/Art/TEXTURES/Terrain Textures/sand/L_Sand.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/L_Gravel.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/silt/L_Silt.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/rocks/L_Rocks.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/salt/L_Salt.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/nodules/L_Nodules.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/reef/L_ReefRubble.terrainlayer",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/seep/L_SeepCrust.terrainlayer"
            };

            TerrainLayer[] layers = new TerrainLayer[TerrainLayerCount];
            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayer loaded = AssetDatabase.LoadAssetAtPath<TerrainLayer>(candidatePaths[i]);
                layers[i] = loaded != null ? loaded : CreateTransientLayer(i);
            }

            return layers;
        }

        private static TerrainLayer CreateTransientLayer(int index)
        {
            Color[] colors =
            {
                new Color(0.55f, 0.58f, 0.52f),
                new Color(0.45f, 0.50f, 0.46f),
                new Color(0.25f, 0.32f, 0.40f),
                new Color(0.16f, 0.18f, 0.21f),
                new Color(0.68f, 0.62f, 0.50f),
                new Color(0.08f, 0.08f, 0.09f),
                new Color(0.42f, 0.45f, 0.40f),
                new Color(0.26f, 0.20f, 0.15f)
            };

            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Color color = colors[math.clamp(index, 0, colors.Length - 1)];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                    texture.SetPixel(x, y, color * (0.92f + ((x + y) & 1) * 0.10f));
            }
            texture.Apply();

            TerrainLayer layer = new TerrainLayer
            {
                name = $"CleanRoom_FallbackLayer_{index}",
                diffuseTexture = texture,
                tileSize = new Vector2(4f, 4f),
                smoothness = 0.18f,
                metallic = 0f,
                normalScale = 1f,
                maskMapRemapMax = new Vector4(1f, 1f, 1f, 1f)
            };
            layer.hideFlags = HideFlags.HideAndDontSave;
            return layer;
        }

        private static UnityEngine.Terrain BuildTerrainChunk(
            int row,
            int col,
            float originX,
            float originZ,
            in WorldMacroGeologyParams macroParams,
            TerrainLayer[] layers,
            Material baseMaterial,
            bool exportDiagnostics,
            out float[,] worldHeights,
            out float[,] slope01,
            out float[,] curvature01,
            out int[,] dominantMaterial,
            out float chunkMinY,
            out float chunkMaxY)
        {
            GameObject terrainGo = new GameObject($"CleanRoom_Terrain_{row}_{col}");
            terrainGo.transform.position = new Vector3(originX, TerrainBaseY, originZ);

            UnityEngine.Terrain terrain = terrainGo.AddComponent<UnityEngine.Terrain>();
            TerrainCollider collider = terrainGo.AddComponent<TerrainCollider>();
            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = HeightResolution,
                alphamapResolution = AlphaResolution,
                size = new Vector3(ChunkSizeMeters, TerrainHeightRangeMeters, ChunkSizeMeters),
                terrainLayers = layers
            };
            terrain.terrainData = terrainData;
            collider.terrainData = terrainData;
            terrain.basemapDistance = 100000f;

            // Default heightmapPixelError is 5 screen pixels of allowed geometric error. At this
            // render's standoff that is roughly 10 m of licensed deviation, which is enough to erase
            // exactly the metre-scale detail these pictures exist to judge. A proof render should show
            // the mesh, not the LOD system's opinion of it.
            terrain.heightmapPixelError = 1f;

            WorldMacroGeologyParams localMacroParams = macroParams;
            float[,] heights01 = new float[HeightResolution, HeightResolution];
            float[,] localWorldHeights = exportDiagnostics ? new float[HeightResolution, HeightResolution] : null;
            float[,] localSlope01 = exportDiagnostics ? new float[AlphaResolution, AlphaResolution] : null;
            float[,] localCurvature01 = exportDiagnostics ? new float[AlphaResolution, AlphaResolution] : null;
            int[,] localDominantMaterial = exportDiagnostics ? new int[AlphaResolution, AlphaResolution] : null;
            float[,,] alphamaps = new float[AlphaResolution, AlphaResolution, TerrainLayerCount];
            int heightSampleCount = HeightResolution * HeightResolution;
            int alphaSampleCount = AlphaResolution * AlphaResolution;
            int diagnosticSampleCount = exportDiagnostics ? alphaSampleCount : 1;
            NativeArray<float> heightBuffer = default;
            NativeArray<float4> primary = default;
            NativeArray<float4> secondary = default;
            NativeArray<float4> control1 = default;
            NativeArray<float4> control2 = default;
            NativeArray<float> slopeBuffer = default;
            NativeArray<float> curvatureBuffer = default;
            NativeArray<int> dominantBuffer = default;
            float localMinY = 0f;
            float localMaxY = 0f;
            try
            {
                heightBuffer = new NativeArray<float>(heightSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                primary = new NativeArray<float4>(alphaSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                secondary = new NativeArray<float4>(alphaSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                control1 = new NativeArray<float4>(alphaSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                control2 = new NativeArray<float4>(alphaSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                slopeBuffer = new NativeArray<float>(diagnosticSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                curvatureBuffer = new NativeArray<float>(diagnosticSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                dominantBuffer = new NativeArray<int>(diagnosticSampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                // Sample where the site is; stand the mesh at the local origin. See s_SampleOriginXZ
                // for why the two must not be the same number.
                double2 sampleOrigin = s_SampleOriginXZ + new double2(originX, originZ);

                var heightJob = new CleanRoomMacroHeightBufferJob
                {
                    HeightBufferMeters = heightBuffer,
                    Resolution = HeightResolution,
                    ChunkSizeMeters = ChunkSizeMeters,
                    WorldOriginXZ = sampleOrigin,
                    MacroGeologyParams = localMacroParams
                };
                heightJob.Schedule(heightSampleCount, ResolveJobBatchCount(heightSampleCount)).Complete();

                var splatJob = new WorldTerrainSurfaceMaterialMaskJob
                {
                    HeightBufferMeters = heightBuffer,
                    Primary = primary,
                    Secondary = secondary,
                    Control1 = control1,
                    Control2 = control2,
                    Slope01 = slopeBuffer,
                    Curvature01 = curvatureBuffer,
                    DominantMaterialIndex = dominantBuffer,
                    Width = AlphaResolution,
                    Height = AlphaResolution,
                    HeightBufferResolution = HeightResolution,
                    CellSizeMeters = ChunkSizeMeters / (HeightResolution - 1),
                    HeightCellSizeMeters = ChunkSizeMeters / (HeightResolution - 1),
                    WorldOriginXZ = sampleOrigin,
                    MacroGeologyParams = localMacroParams,
                    MaskContrast = 1f
                };
                splatJob.Schedule(alphaSampleCount, ResolveJobBatchCount(alphaSampleCount)).Complete();

                CopyHeightBufferToManaged(
                    heightBuffer, heights01, localWorldHeights,
                    out float rawMinMeters, out float rawMaxMeters, out int clippedSamples);
                localMinY = rawMinMeters;
                localMaxY = rawMaxMeters;

                if (clippedSamples > 0)
                {
                    Debug.LogWarning(
                        $"[CleanRoom] Chunk ({row},{col}) has {clippedSamples} of {heightSampleCount} " +
                        $"samples ({100.0 * clippedSamples / heightSampleCount:F2}%) outside the " +
                        $"{MinTerrainHeightMeters:F0}..{MaxTerrainHeightMeters:F0}m terrain window " +
                        $"(raw range {rawMinMeters:F0}..{rawMaxMeters:F0}m). Those samples are clipped " +
                        "flat onto the boundary, and a clipped plateau looks exactly like authored " +
                        "flat ground in every render and X-Ray downstream.");
                }

                CopyControlBuffersToManaged(control1, control2, alphamaps, slopeBuffer, curvatureBuffer, dominantBuffer, localSlope01, localCurvature01, localDominantMaterial);
            }
            finally
            {
                if (dominantBuffer.IsCreated) dominantBuffer.Dispose();
                if (curvatureBuffer.IsCreated) curvatureBuffer.Dispose();
                if (slopeBuffer.IsCreated) slopeBuffer.Dispose();
                if (control2.IsCreated) control2.Dispose();
                if (control1.IsCreated) control1.Dispose();
                if (secondary.IsCreated) secondary.Dispose();
                if (primary.IsCreated) primary.Dispose();
                if (heightBuffer.IsCreated) heightBuffer.Dispose();
            }

            worldHeights = localWorldHeights;
            slope01 = localSlope01;
            curvature01 = localCurvature01;
            dominantMaterial = localDominantMaterial;
            chunkMinY = localMinY;
            chunkMaxY = localMaxY;
            terrainData.SetHeightsDelayLOD(0, 0, heights01);

            // SetHeightsDelayLOD defers the LOD and collider rebuild and Unity requires SyncHeightmap
            // to finish it. Without this call the terrain renders from mesh data that was never built
            // for these heights.
            //
            // What that looked like, 2026-08-10, the first time the clean room was aimed at a site
            // with real relief: two enormous triangular sheets meeting at a point with the background
            // visible between them. A heightmap is a function of x and z and cannot have a hole, so
            // the picture could not have been the terrain - it was the unbuilt mesh. At the world
            // origin the same missing call produced a perfectly plausible picture, because a 12.2
            // degree plain survives almost any tessellation.
            terrainData.SyncHeightmap();
            terrainData.SetAlphamaps(0, 0, alphamaps);

            Material material = new Material(baseMaterial);
            material.name = $"CleanRoomTerrainMaterial_{row}_{col}";
            Texture2D[] controlTextures = terrainData.alphamapTextures;
            if (controlTextures.Length > 0 && controlTextures[0] != null)
                material.SetTexture("_Control", controlTextures[0]);
            if (controlTextures.Length > 1 && controlTextures[1] != null)
                material.SetTexture("_Control1", controlTextures[1]);
            if (controlTextures.Length > 2 && controlTextures[2] != null)
                material.SetTexture("_Control2", controlTextures[2]);
            material.SetFloat("_NumLayersCount", terrainData.alphamapLayers);
            material.SetVector("_TerrainSize", new Vector4(terrainData.size.x, terrainData.size.y, terrainData.size.z, 0f));
            terrain.materialTemplate = material;
            terrain.Flush();
            return terrain;
        }

        private static int ResolveJobBatchCount(int cellCount)
        {
            return math.max(32, math.min(256, math.max(1, cellCount / 1024)));
        }

        /// <summary>
        /// Copies the raw metre heights into the 0..1 heightmap Unity wants, and reports the raw
        /// extent alongside it.
        ///
        /// The extent is reported RAW, before the saturate, because the saturate is a silent clip: any
        /// geology outside MinTerrainHeightMeters..MaxTerrainHeightMeters is flattened onto the
        /// boundary and the resulting plateau is indistinguishable in the picture from terrain that
        /// was authored flat. clippedSamples exists to tell those two apart.
        /// </summary>
        private static void CopyHeightBufferToManaged(
            NativeArray<float> heightBuffer,
            float[,] heights01,
            float[,] worldHeights,
            out float rawMinMeters,
            out float rawMaxMeters,
            out int clippedSamples)
        {
            float invRange = 1f / math.max(0.0001f, MaxTerrainHeightMeters - MinTerrainHeightMeters);
            rawMinMeters = float.MaxValue;
            rawMaxMeters = float.MinValue;
            clippedSamples = 0;

            for (int z = 0; z < HeightResolution; z++)
            {
                int rowBase = z * HeightResolution;
                for (int x = 0; x < HeightResolution; x++)
                {
                    float heightMeters = heightBuffer[rowBase + x];
                    if (worldHeights != null)
                        worldHeights[z, x] = heightMeters;

                    if (heightMeters < rawMinMeters) rawMinMeters = heightMeters;
                    if (heightMeters > rawMaxMeters) rawMaxMeters = heightMeters;
                    if (heightMeters < MinTerrainHeightMeters || heightMeters > MaxTerrainHeightMeters)
                        clippedSamples++;

                    heights01[z, x] = math.saturate((heightMeters - MinTerrainHeightMeters) * invRange);
                }
            }
        }

        private static void CopyControlBuffersToManaged(
            NativeArray<float4> control1,
            NativeArray<float4> control2,
            float[,,] alphamaps,
            NativeArray<float> slopeBuffer,
            NativeArray<float> curvatureBuffer,
            NativeArray<int> dominantBuffer,
            float[,] slope01,
            float[,] curvature01,
            int[,] dominantMaterial)
        {
            for (int z = 0; z < AlphaResolution; z++)
            {
                int rowBase = z * AlphaResolution;
                for (int x = 0; x < AlphaResolution; x++)
                {
                    int i = rowBase + x;
                    float4 c1 = control1[i];
                    float4 c2 = control2[i];
                    alphamaps[z, x, 0] = c1.x;
                    alphamaps[z, x, 1] = c1.y;
                    alphamaps[z, x, 2] = c1.z;
                    alphamaps[z, x, 3] = c1.w;
                    alphamaps[z, x, 4] = c2.x;
                    alphamaps[z, x, 5] = c2.y;
                    alphamaps[z, x, 6] = c2.z;
                    alphamaps[z, x, 7] = c2.w;
                    if (slope01 != null && slopeBuffer.IsCreated)
                        slope01[z, x] = slopeBuffer[i];
                    if (curvature01 != null && curvatureBuffer.IsCreated)
                        curvature01[z, x] = curvatureBuffer[i];
                    if (dominantMaterial != null && dominantBuffer.IsCreated)
                        dominantMaterial[z, x] = dominantBuffer[i];
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CleanRoomMacroHeightBufferJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<float> HeightBufferMeters;
            public int Resolution;
            public float ChunkSizeMeters;
            public double2 WorldOriginXZ;
            public WorldMacroGeologyParams MacroGeologyParams;

            public void Execute(int index)
            {
                int safeResolution = math.max(1, Resolution);
                if ((uint)index >= (uint)HeightBufferMeters.Length)
                    return;

                int x = index % safeResolution;
                int z = index / safeResolution;
                float pitch = ChunkSizeMeters / math.max(1, safeResolution - 1);
                float worldX = (float)(WorldOriginXZ.x + x * (double)pitch);
                float worldZ = (float)(WorldOriginXZ.y + z * (double)pitch);
                HeightBufferMeters[index] = WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ, in MacroGeologyParams);
            }
        }

        private static void StitchTerrainGrid(UnityEngine.Terrain[,] terrains)
        {
            for (int r = 0; r < TerrainGridSize; r++)
            {
                for (int c = 0; c < TerrainGridSize; c++)
                {
                    UnityEngine.Terrain left = c > 0 ? terrains[r, c - 1] : null;
                    UnityEngine.Terrain right = c < TerrainGridSize - 1 ? terrains[r, c + 1] : null;
                    UnityEngine.Terrain bottom = r > 0 ? terrains[r - 1, c] : null;
                    UnityEngine.Terrain top = r < TerrainGridSize - 1 ? terrains[r + 1, c] : null;
                    terrains[r, c].SetNeighbors(left, top, right, bottom);
                }
            }
        }

        /// <summary>
        /// Bird's-eye camera framing the whole generated grid, aimed at the terrain's MEASURED
        /// surface height.
        ///
        /// It used to be hardcoded: position (260, 850, -920) looking at
        /// <c>(0, TerrainBaseY + 2700, 0)</c> = y -2300. The clean-room surface actually sits near
        /// y -3750 (measured: min -3831.48, max -3681.89), so the aim point was about 1450 m ABOVE
        /// the ground and the view axis passed over the terrain entirely. The terrain then entered
        /// frame only as a receding sheet at the bottom against empty background - which is exactly
        /// how the beauty render read, and it was mistaken for the geology being flat and waxy
        /// rather than for the camera being pointed at nothing.
        ///
        /// Aim comes from the height buffer rather than from a constant so it cannot drift again
        /// when the vertical extent changes: <paramref name="surfaceY"/> is the mean of the
        /// measured min/max, and the standoff is derived from the grid's own footprint and the
        /// camera's field of view so the whole grid is framed at any chunk size.
        ///
        /// Elevation is 38 degrees, not straight down. A nadir view flattens relief to a texture
        /// because every face is lit and foreshortened equally; an oblique bird's-eye keeps
        /// silhouettes, shadowed faces and scarp edges readable, which is what terrain.md:247-250
        /// asks a scale card to show. Ground-level framing is deliberately NOT used here: with
        /// 40-70 degree slopes over most of the surface, a 2 m eye height looks into a wall.
        /// </summary>
        /// <summary>
        /// A bird's-eye camera that frames the whole generated grid.
        ///
        /// Both of the corrections below were made on 2026-08-10, when the clean room was first aimed
        /// at a site with real relief. Neither could be seen before that: at the world origin the tile
        /// is a 12.2 degree basin with 304 m of relief, and a camera that is badly aimed and badly
        /// sized still produces a plausible picture of a flat plain.
        ///
        /// 1. AIM AT THE GRID CENTRE. The chunks are laid out at col * 1000 for col in -1..1 and each
        ///    spans [origin, origin + 1000], so the grid covers -1000..2000 on both axes and its centre
        ///    is (500, 500) - not (0, 0). The old aim was 707 m off-centre diagonally, a quarter of the
        ///    frame.
        ///
        /// 2. SIZE THE STANDOFF TO THE VERTICAL EXTENT AS WELL. The old standoff came from the
        ///    horizontal span alone and the old aim height came from the CENTRE chunk's mid-height. At
        ///    P5_deepfar the centre chunk spans -3183..-1925 m while the full grid spans far more,
        ///    because the neighbouring chunks continue the same continental slope for another kilometre
        ///    each way. Framing a 3 km grid on the middle chunk's mid-height put the camera below the
        ///    upslope terrain: the render came back as two enormous triangular sheets meeting at a
        ///    point, with the background visible through the gap between them. That was the camera
        ///    inside the hillside, not a hole in the terrain - a heightmap cannot have one.
        /// </summary>
        private static Camera BuildCamera(float gridMinY, float gridMaxY)
        {
            GameObject cameraGo = new GameObject("CleanRoom_Camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.010f, 0.016f, 1f);

            const float FieldOfViewDegrees = 42f;
            const float ElevationDegrees = 38f;

            // Full span of the generated grid, plus margin so the edges are not flush with frame.
            float gridSpanMeters = ChunkSizeMeters * TerrainGridSize;
            float verticalSpanMeters = math.max(0f, gridMaxY - gridMinY);

            // The larger of the two extents is what has to fit. On a continental slope the vertical
            // one wins: at P5_deepfar the grid drops further than it is wide.
            float framedSpanMeters = math.max(gridSpanMeters, verticalSpanMeters) * 1.15f;

            // Distance at which framedSpan subtends the vertical FOV.
            float standoffMeters = (framedSpanMeters * 0.5f) /
                                   math.tan(math.radians(FieldOfViewDegrees * 0.5f));

            float elevationRad = math.radians(ElevationDegrees);

            // Centre of the grid in XZ, centre of the measured surface in Y. Chunks sit at
            // col * ChunkSizeMeters for col in -GridRadius..GridRadius and each spans one chunk
            // further positive, so the grid covers [-R*C, R*C + C] and its centre is at C/2 - the
            // GridRadius terms cancel and it is half a chunk, whatever the radius.
            float gridCenterXZ = ChunkSizeMeters * 0.5f;
            float surfaceY = (gridMinY + gridMaxY) * 0.5f;
            Vector3 aim = new Vector3(gridCenterXZ, surfaceY, gridCenterXZ);
            Vector3 offset = new Vector3(
                0f,
                standoffMeters * math.sin(elevationRad),
                -standoffMeters * math.cos(elevationRad));

            camera.transform.position = aim + offset;
            camera.transform.LookAt(aim);
            camera.nearClipPlane = 0.5f;

            // Far plane follows the standoff instead of a fixed 12000: a larger grid would
            // otherwise be clipped away, and the clip distance is not where vertical extent is
            // decided.
            camera.farClipPlane = math.max(2000f, standoffMeters * 3f);
            camera.fieldOfView = FieldOfViewDegrees;

            Debug.Log(
                $"[CleanRoom] Bird's-eye camera: aim=({aim.x:F1}, {surfaceY:F1}, {aim.z:F1}) " +
                $"pos=({camera.transform.position.x:F1}, {camera.transform.position.y:F1}, " +
                $"{camera.transform.position.z:F1}) standoff={standoffMeters:F1}m " +
                $"elevation={ElevationDegrees:F0}deg framing {framedSpanMeters:F0}m of a " +
                $"{gridSpanMeters:F0}m grid that drops {verticalSpanMeters:F0}m " +
                $"({gridMinY:F0}..{gridMaxY:F0}m).");

            UniversalAdditionalCameraData urp = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.renderShadows = true;
            return camera;
        }

        private static void RenderBeauty(Camera camera, string path)
        {
            CaptureCamera(camera, path, 1920, 1080);
            Debug.Log($"[CleanRoom] Beauty render written: {path}");
        }

        private static void RenderTransitionBeauty(Camera camera, in BiomeTransitionShot shot, string path)
        {
            Vector3 horizontalNormal = new Vector3(shot.Normal.x, 0f, shot.Normal.z);
            if (horizontalNormal.sqrMagnitude < 0.0001f)
                horizontalNormal = new Vector3(0f, 0f, -1f);
            horizontalNormal.Normalize();
            Vector3 tangent = Vector3.Cross(Vector3.up, horizontalNormal).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.right;

            camera.transform.position = shot.Position + horizontalNormal * 4.2f + tangent * 1.4f + Vector3.up * 2.3f;
            camera.transform.LookAt(shot.Position - horizontalNormal * 1.6f + Vector3.up * 0.55f);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 900f;
            camera.fieldOfView = 54f;
            camera.orthographic = false;

            GameObject sunGo = GameObject.Find("CleanRoom_WhiteSun");
            Quaternion originalSunRot = Quaternion.identity;
            if (sunGo != null)
            {
                originalSunRot = sunGo.transform.rotation;
                sunGo.transform.rotation = Quaternion.Euler(65f, 15f, 0f);
            }

            GameObject keyGo = new GameObject("CleanRoom_TransitionKey");
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Point;
            key.color = Color.white;
            key.intensity = 8.5f;
            key.range = 25f;
            key.shadows = LightShadows.None;
            keyGo.transform.position = camera.transform.position + Vector3.up * 2.0f;

            CaptureCamera(camera, path, 1920, 1080);
            Object.DestroyImmediate(keyGo);

            if (sunGo != null)
            {
                sunGo.transform.rotation = originalSunRot;
            }

            float totalSum = shot.ShellSand + shot.LimestoneShelf + shot.ClaySilt + shot.HardRock +
                             shot.BrineSaltCrust + shot.ManganeseNodulePlain + shot.ReefRubble + shot.SeepCrust;
            Debug.Log(FormattableString.Invariant($"[BiomeTransition] Sum={totalSum:0.000} ShellSand={shot.ShellSand:0.000} LimestoneShelf={shot.LimestoneShelf:0.000} ClaySilt={shot.ClaySilt:0.000} HardRock={shot.HardRock:0.000} BrineSaltCrust={shot.BrineSaltCrust:0.000} ManganeseNodulePlain={shot.ManganeseNodulePlain:0.000} ReefRubble={shot.ReefRubble:0.000} SeepCrust={shot.SeepCrust:0.000}"));
            Debug.Log($"[CleanRoom] Biome transition render written: {path} score={shot.Score:0.000} " +
                      $"sand={shot.ShellSand:0.000} silt={shot.ClaySilt:0.000} rock={shot.HardRock:0.000} limestone={shot.LimestoneShelf:0.000} " +
                      $"brine={shot.BrineSaltCrust:0.000} nodule={shot.ManganeseNodulePlain:0.000} reef={shot.ReefRubble:0.000} seep={shot.SeepCrust:0.000} " +
                      $"totalSum={totalSum:0.000} sum={totalSum:0.000} pos={shot.Position}");
        }

        private static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(rt);
        }

        private static BiomeTransitionShot FindBiomeTransitionShot(UnityEngine.Terrain[,] terrainGrid, in WorldMacroGeologyParams macroParams)
        {
            BiomeTransitionShot best = default;
            best.Score = -1f;
            foreach (UnityEngine.Terrain terrain in terrainGrid)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                TerrainData td = terrain.terrainData;
                int steps = 96;
                for (int z = 4; z < steps - 4; z++)
                {
                    float nz = z / (float)(steps - 1);
                    float worldZ = terrain.transform.position.z + nz * td.size.z;
                    for (int x = 4; x < steps - 4; x++)
                    {
                        float nx = x / (float)(steps - 1);
                        float worldX = terrain.transform.position.x + nx * td.size.x;

                        // The mesh stands at the local origin but the geology lives at the site, so
                        // the sample coordinate and the camera coordinate are different numbers.
                        // Sampling at the transform alone would score the origin basin and then aim
                        // the camera at a shelf it had never looked at.
                        double sampleX = s_SampleOriginXZ.x + worldX;
                        double sampleZ = s_SampleOriginXZ.y + worldZ;

                        WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(sampleX, sampleZ, in macroParams);
                        WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(in sample, (float)sampleX, (float)sampleZ, macroParams.Seed);
                        WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(macroParams.Seed);
                        mesoParams.PreviewExtentMeters = ChunkSizeMeters;
                        mesoParams.MaxMesoDeltaMeters = 24f;
                        WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(in sample, (float)sampleX, (float)sampleZ, in mesoParams);
                        weights = WorldTerrainSurfaceMaterialResolver.ApplyMesoDetailBias(weights, in meso);

                        float sediment = math.max(weights.ShellSand, weights.ClaySilt);
                        float rock = weights.HardRock;
                        float balance = 1f - math.abs(sediment - rock);
                        float topology = math.saturate(sample.NegativeCurvature01 * 0.48f + sample.PositiveCurvature01 * 0.48f + sample.Slope01 * 0.28f);
                        float threshold = math.saturate(math.min(sediment, rock) * 2.0f);
                        float score = balance * 0.35f + topology * 0.45f + threshold * 0.20f;
                        if (sediment < 0.18f || rock < 0.18f)
                            score *= 0.35f;
                        if (score <= best.Score)
                            continue;

                        Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                        float height = td.GetInterpolatedHeight(nx, nz) + terrain.transform.position.y;
                        best = new BiomeTransitionShot
                        {
                            Position = new Vector3(worldX, height, worldZ),
                            Normal = normal,
                            Score = score,
                            ShellSand = weights.ShellSand,
                            LimestoneShelf = weights.LimestoneShelf,
                            ClaySilt = weights.ClaySilt,
                            HardRock = weights.HardRock,
                            BrineSaltCrust = weights.BrineSaltCrust,
                            ManganeseNodulePlain = weights.ManganeseNodulePlain,
                            ReefRubble = weights.ReefRubble,
                            SeepCrust = weights.SeepCrust
                        };
                    }
                }
            }

            if (best.Score < 0f)
                best = new BiomeTransitionShot { Position = new Vector3(0f, TerrainBaseY + 2500f, 0f), Normal = Vector3.up, Score = 0f };
            return best;
        }

        private static void ExportSplatmapComposite(UnityEngine.Terrain[,] terrainGrid, string path)
        {
            const int tilePixels = 512;
            int gridRows = terrainGrid.GetLength(0);
            int gridCols = terrainGrid.GetLength(1);
            int width = tilePixels * gridCols;
            int height = tilePixels * gridRows;
            NativeArray<Color32> pixels = default;
            NativeArray<float4> control1 = default;
            NativeArray<float4> control2 = default;
            try
            {
                pixels = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                for (int row = 0; row < gridRows; row++)
                {
                    for (int col = 0; col < gridCols; col++)
                    {
                        UnityEngine.Terrain terrain = terrainGrid[row, col];
                        if (terrain == null || terrain.terrainData == null)
                            continue;

                        TerrainData td = terrain.terrainData;
                        int alphaResolution = td.alphamapResolution;
                        int alphaCount = alphaResolution * alphaResolution;
                        control1 = new NativeArray<float4>(alphaCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                        control2 = new NativeArray<float4>(alphaCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                        float[,,] alpha = td.GetAlphamaps(0, 0, alphaResolution, alphaResolution);
                        CopyAlphaToControls(alpha, control1, control2, alphaResolution);
                        var job = new CleanRoomSplatCompositeTileJob
                        {
                            Control1 = control1,
                            Control2 = control2,
                            Pixels = pixels,
                            AlphaResolution = alphaResolution,
                            TilePixels = tilePixels,
                            ImageWidth = width,
                            TileOriginX = col * tilePixels,
                            TileOriginY = row * tilePixels
                        };
                        job.Schedule(tilePixels * tilePixels, ResolveJobBatchCount(tilePixels * tilePixels)).Complete();
                        control2.Dispose();
                        control2 = default;
                        control1.Dispose();
                        control1 = default;
                    }
                }

                WriteColor32Png(path, pixels, width, height);
            }
            finally
            {
                if (control2.IsCreated) control2.Dispose();
                if (control1.IsCreated) control1.Dispose();
                if (pixels.IsCreated) pixels.Dispose();
            }
            Debug.Log($"[CleanRoom] Splatmap composite written: {path}");
        }

        private static void CopyAlphaToControls(float[,,] alpha, NativeArray<float4> control1, NativeArray<float4> control2, int resolution)
        {
            for (int z = 0; z < resolution; z++)
            {
                int rowBase = z * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    int i = rowBase + x;
                    control1[i] = new float4(alpha[z, x, 0], alpha[z, x, 1], alpha[z, x, 2], alpha[z, x, 3]);
                    control2[i] = new float4(alpha[z, x, 4], alpha[z, x, 5], alpha[z, x, 6], alpha[z, x, 7]);
                }
            }
        }

        private struct BiomeTransitionShot
        {
            public Vector3 Position;
            public Vector3 Normal;
            public float Score;
            public float ShellSand;
            public float LimestoneShelf;
            public float ClaySilt;
            public float HardRock;
            public float BrineSaltCrust;
            public float ManganeseNodulePlain;
            public float ReefRubble;
            public float SeepCrust;
        }

        private static void ExportXRayMaps(
            string artifactDir,
            string suffix,
            float[,] heights,
            float[,] slope,
            float[,] curvature,
            int[,] material)
        {
            if (heights == null || slope == null || curvature == null || material == null)
                throw new InvalidOperationException("Center diagnostic buffers were not generated.");

            // Height is normalised against the MEASURED extent of this tile, not against the
            // world's full vertical window.
            //
            // It used to pass MinTerrainHeightMeters..MaxTerrainHeightMeters, a fixed 5200 m range.
            // The clean-room tile sits near -3700 m with a few hundred metres of local relief, so
            // real geology occupied about 4% of the greyscale and the map rendered as a uniform
            // grey field. terrain.md:244 makes X-Ray maps "the only accepted terrain truth", and an
            // instrument that reports flat for terrain that is not flat inverts that rule: it is
            // the map most likely to be read as proof of a defect that is not there.
            //
            // The measured span is printed alongside, because a self-normalised map cannot be
            // compared between runs without it - full black to full white says nothing until you
            // know whether it spans 8 m or 800 m. terrain.md:248-250 asks for a per-scale verdict,
            // and that verdict needs the number, not only the picture.
            MeasureExtent(heights, out float heightMin, out float heightMax);
            float heightSpan = heightMax - heightMin;
            Debug.Log(
                $"[CleanRoom] Height X-Ray extent: min={heightMin:F2}m max={heightMax:F2}m " +
                $"span={heightSpan:F2}m (self-normalised). Authored window for reference: " +
                $"{MinTerrainHeightMeters:F0}..{MaxTerrainHeightMeters:F0}m " +
                $"({MaxTerrainHeightMeters - MinTerrainHeightMeters:F0}m), so this tile occupies " +
                $"{(heightSpan / math.max(0.0001f, MaxTerrainHeightMeters - MinTerrainHeightMeters)) * 100f:F1}% " +
                "of it.");

            // A degenerate span would make the self-normalised map pure black and hide the very
            // failure it exists to expose, so it is reported as a number rather than drawn.
            if (heightSpan < 0.01f)
            {
                Debug.LogError(
                    $"[CleanRoom] Height X-Ray is degenerate: span={heightSpan:F6}m over the whole " +
                    "tile. The terrain really is flat here - this is not a normalisation artifact.");
            }

            WriteScalarMap(Path.Combine(artifactDir, $"CleanRoom_XRay_Height{suffix}.png"), heights, heightMin, heightMax);
            WriteScalarMap(Path.Combine(artifactDir, $"CleanRoom_XRay_Slope{suffix}.png"), slope, 0f, 1f);
            WriteScalarMap(Path.Combine(artifactDir, $"CleanRoom_XRay_Curvature{suffix}.png"), curvature, 0f, 1f);
            WriteMaterialMap(Path.Combine(artifactDir, $"CleanRoom_XRay_MaterialDominant{suffix}.png"), material);
        }

        /// <summary>
        /// Measured extent of a scalar field, ignoring non-finite samples so one NaN cannot swallow
        /// the whole range and render every other sample as a single flat value.
        /// </summary>
        private static void MeasureExtent(float[,] values, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;

            int height = values.GetLength(0);
            int width = values.GetLength(1);
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = values[z, x];
                    if (!math.isfinite(v))
                        continue;

                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (!math.isfinite(min) || !math.isfinite(max))
            {
                min = MinTerrainHeightMeters;
                max = MaxTerrainHeightMeters;
                Debug.LogError(
                    "[CleanRoom] Height buffer held no finite sample; fell back to the authored " +
                    "window for normalisation. The map below is not evidence.");
            }
        }

        private static void WriteScalarMap(string path, float[,] values, float min, float max)
        {
            int height = values.GetLength(0);
            int width = values.GetLength(1);
            NativeArray<float> source = default;
            NativeArray<Color32> pixels = default;
            try
            {
                int length = width * height;
                source = new NativeArray<float>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                pixels = new NativeArray<Color32>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                CopyScalarToNative(values, source, width, height);
                var job = new CleanRoomScalarXRayPixelsJob
                {
                    Values = source,
                    Pixels = pixels,
                    Min = min,
                    InvRange = 1f / math.max(0.0001f, max - min)
                };
                job.Schedule(length, ResolveJobBatchCount(length)).Complete();
                WriteColor32Png(path, pixels, width, height);
            }
            finally
            {
                if (pixels.IsCreated) pixels.Dispose();
                if (source.IsCreated) source.Dispose();
            }
            Debug.Log($"[CleanRoom] X-Ray written: {path}");
        }

        private static void WriteMaterialMap(string path, int[,] material)
        {
            int height = material.GetLength(0);
            int width = material.GetLength(1);
            NativeArray<int> source = default;
            NativeArray<Color32> pixels = default;
            try
            {
                int length = width * height;
                source = new NativeArray<int>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                pixels = new NativeArray<Color32>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                CopyMaterialToNative(material, source, width, height);
                var job = new CleanRoomMaterialXRayPixelsJob
                {
                    Materials = source,
                    Pixels = pixels
                };
                job.Schedule(length, ResolveJobBatchCount(length)).Complete();
                WriteColor32Png(path, pixels, width, height);
            }
            finally
            {
                if (pixels.IsCreated) pixels.Dispose();
                if (source.IsCreated) source.Dispose();
            }
            Debug.Log($"[CleanRoom] Material X-Ray written: {path}");
        }

        private static void CopyScalarToNative(float[,] values, NativeArray<float> target, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                    target[rowBase + x] = values[y, x];
            }
        }

        private static void CopyMaterialToNative(int[,] values, NativeArray<int> target, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                    target[rowBase + x] = values[y, x];
            }
        }

        private static void WriteColor32Png(string path, NativeArray<Color32> pixels, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            NativeArray<byte> png = default;
            try
            {
                texture.SetPixelData(pixels, 0);
                texture.Apply(false, true);
                png = ImageConversion.EncodeNativeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0u);
                if (!png.IsCreated || png.Length == 0)
                    throw new InvalidOperationException($"Native PNG encode returned no bytes for {path}.");

                WriteNativeBytes(path, png);
            }
            finally
            {
                if (png.IsCreated) png.Dispose();
                Object.DestroyImmediate(texture);
            }
        }

        private static unsafe void WriteNativeBytes(string path, NativeArray<byte> bytes)
        {
            unsafe
            {
                byte* pointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.SequentialScan);
                stream.Write(new ReadOnlySpan<byte>(pointer, bytes.Length));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CleanRoomSplatCompositeTileJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float4> Control1;
            [ReadOnly, NoAlias] public NativeArray<float4> Control2;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<Color32> Pixels;
            public int AlphaResolution;
            public int TilePixels;
            public int ImageWidth;
            public int TileOriginX;
            public int TileOriginY;

            public void Execute(int index)
            {
                int px = index % TilePixels;
                int py = index / TilePixels;
                int ax = math.clamp((int)math.round(px / (float)math.max(1, TilePixels - 1) * (AlphaResolution - 1)), 0, AlphaResolution - 1);
                int ay = math.clamp((int)math.round(py / (float)math.max(1, TilePixels - 1) * (AlphaResolution - 1)), 0, AlphaResolution - 1);
                int ai = ay * AlphaResolution + ax;
                float4 c1 = math.saturate(Control1[ai]);
                float4 c2 = math.saturate(Control2[ai]);
                float3 color = new float3(
                    c1.x * 0.78f + c1.y * 0.40f + c2.x * 0.85f + c2.z * 0.45f,
                    c1.z * 0.52f + c1.x * 0.68f + c2.z * 0.62f + c1.y * 0.46f + c2.w * 0.18f,
                    c1.w * 0.92f + c1.z * 0.90f + c2.y * 0.55f + c2.w * 0.22f);
                color = math.saturate(color);
                int outputIndex = (TileOriginY + py) * ImageWidth + TileOriginX + px;
                Pixels[outputIndex] = new Color32(
                    (byte)math.clamp((int)math.round(color.x * 255f), 0, 255),
                    (byte)math.clamp((int)math.round(color.y * 255f), 0, 255),
                    (byte)math.clamp((int)math.round(color.z * 255f), 0, 255),
                    255);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CleanRoomScalarXRayPixelsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> Values;
            [WriteOnly, NoAlias] public NativeArray<Color32> Pixels;
            public float Min;
            public float InvRange;

            public void Execute(int index)
            {
                float v = math.saturate((Values[index] - Min) * InvRange);
                byte b = (byte)math.clamp((int)math.round(v * 255f), 0, 255);
                Pixels[index] = new Color32(b, b, b, 255);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CleanRoomMaterialXRayPixelsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<int> Materials;
            [WriteOnly, NoAlias] public NativeArray<Color32> Pixels;

            public void Execute(int index)
            {
                Pixels[index] = ResolveColor(math.clamp(Materials[index], 0, 7));
            }

            private static Color32 ResolveColor(int material)
            {
                switch (material)
                {
                    case 0: return new Color32(199, 194, 158, 255);
                    case 1: return new Color32(148, 173, 148, 255);
                    case 2: return new Color32(56, 97, 148, 255);
                    case 3: return new Color32(20, 23, 28, 255);
                    case 4: return new Color32(217, 184, 122, 255);
                    case 5: return new Color32(5, 5, 8, 255);
                    case 6: return new Color32(140, 158, 128, 255);
                    default: return new Color32(122, 56, 31, 255);
                }
            }
        }
    }
}
