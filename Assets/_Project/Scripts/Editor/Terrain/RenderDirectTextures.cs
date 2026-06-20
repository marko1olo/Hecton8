using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Mathematics;
using System.Threading.Tasks;
using Hecton8.World;

namespace Hecton8.Editor.Terrain
{
    public static class RenderDirectTextures
    {
        public static void Run()
        {
            try
            {
                int size = 512;
                Texture2D splatTex = new Texture2D(size, size, TextureFormat.RGB24, false);
                Texture2D macroTex = new Texture2D(size, size, TextureFormat.RGB24, false);

                WorldMacroGeologyParams p = new WorldMacroGeologyParams
                {
                    Seed = WorldMacroGeologyFields.DefaultAuthoringSeed + 1,
                    WorldExtentMeters = 30000f,
                    ChunkSizeMeters = 512f,
                    WaterSurfaceY = 0f,
                    ShelfDepthMeters = 40f,
                    ShelfBreakWidthMeters = 1500f,
                    AbyssDepthMeters = 1200f,
                    HadalDepthMeters = 6000f,
                    RidgeHeightMeters = 250f,
                    RidgeWidthMeters = 1200f,
                    TrenchDepthMeters = 1000f,
                    TrenchWidthMeters = 1500f,
                    BasinDepthMeters = 300f,
                    DetailProbeMeters = 15f
                };

                RenderScale(p, -5000f, -5000f, 10000f, "10km");
                Debug.Log("[RenderDirectTextures] Generated 10km scale.");
                
                RenderScale(p, -1000f, -1000f, 2000f, "2km");
                Debug.Log("[RenderDirectTextures] Generated 2km scale.");
                
                RenderScale(p, -250f, -250f, 500f, "500m");
                Debug.Log("[RenderDirectTextures] Generated 500m scale.");

                Debug.Log("[RenderDirectTextures] Generated all multi-scale textures.");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                File.WriteAllText("C:/hades/Hecton8/Logs/RenderCrash.txt", e.ToString());
                Debug.LogError(e.ToString());
                EditorApplication.Exit(1);
            }
        }

        private static void RenderScale(WorldMacroGeologyParams p, float startX, float startZ, float extent, string suffix)
        {
            int size = 512;
            Texture2D splatTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            Texture2D macroTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            float step = extent / size;

            Color[] macroColors = new Color[size * size];
            Color[] splatColors = new Color[size * size];

            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float worldX = startX + x * step;
                    float worldZ = startZ + y * step;

                    var sample = WorldMacroGeologyFields.Evaluate(worldX, worldZ, in p);
                    float h = sample.HeightMeters;
                    float hRight = WorldMacroGeologyFields.EvaluateHeightMeters(worldX + 1f, worldZ, in p);
                    float hUp = WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ + 1f, in p);
                    
                    float dx = hRight - h;
                    float dz = hUp - h;
                    Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
                    Vector3 lightDir = new Vector3(1f, 1f, 1f).normalized;
                    float NdotL = Mathf.Max(0.2f, Vector3.Dot(normal, lightDir));

                    macroColors[y * size + x] = new Color(0.6f * NdotL, 0.6f * NdotL, 0.6f * NdotL);

                    WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(in sample, worldX, worldZ, p.Seed);
                    WorldTerrainControlMapSplats splats = WorldTerrainSurfaceMaterialResolver.ResolveControlSplats(in weights);
                    
                    Color splatColor = new Color(splats.Control1.x, splats.Control1.y, splats.Control1.z);
                    if (splats.Control1.w > 0.1f)
                    {
                        splatColor = Color.Lerp(splatColor, Color.magenta, splats.Control1.w);
                    }
                    
                    splatColors[y * size + x] = splatColor;
                }
            });

            splatTex.SetPixels(splatColors);
            macroTex.SetPixels(macroColors);

            splatTex.Apply();
            macroTex.Apply();

            File.WriteAllBytes($"C:/hades/Hecton8/Logs/Macro_{suffix}.png", macroTex.EncodeToPNG());
            File.WriteAllBytes($"C:/hades/Hecton8/Logs/Splatmap_{suffix}.png", splatTex.EncodeToPNG());
            Object.DestroyImmediate(splatTex);
            Object.DestroyImmediate(macroTex);

        }
    }
}
