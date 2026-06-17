using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using Hecton8.Core;
using Hecton8.World;

public static class TerrainSlopeTest
{
    [MenuItem("Hecton8/Run Slope Test")]
    public static void RunTest()
    {
        int resolution = 1000;
        float extentMeters = 50000f; // 50km
        float spacing = extentMeters / resolution;

        WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(880031);
        p.WaterSurfaceY = 0f;
        p.DetailProbeMeters = 64f;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        double sumHeight = 0;
        
        float minSlope = float.MaxValue;
        float maxSlope = float.MinValue;
        double sumSlope = 0;

        int over60 = 0;
        int under10 = 0;
        int totalValid = 0;

        int[] histogram = new int[10];

        // First pass: collect heights
        float[,] heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            float absoluteZ = -extentMeters/2f + z * spacing;
            for (int x = 0; x < resolution; x++)
            {
                float absoluteX = -extentMeters/2f + x * spacing;
                float height = WorldMacroGeologyFields.EvaluateHeightMeters(absoluteX, absoluteZ, in p);
                heights[x, z] = height;

                if (height < minHeight) minHeight = height;
                if (height > maxHeight) maxHeight = height;
                sumHeight += height;
            }
        }

        float delta = maxHeight - minHeight;

        // Second pass: compute slopes and histogram
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[x, z];
                int bucket = (int)(((height - minHeight) / (delta + 0.001f)) * 10);
                if (bucket < 0) bucket = 0;
                if (bucket > 9) bucket = 9;
                histogram[bucket]++;

                if (x > 0 && x < resolution - 1 && z > 0 && z < resolution - 1)
                {
                    float dx = (heights[x + 1, z] - heights[x - 1, z]) / (2f * spacing);
                    float dz = (heights[x, z + 1] - heights[x, z - 1]) / (2f * spacing);
                    float gradient = math.sqrt(dx * dx + dz * dz);
                    float slopeDegrees = math.atan(gradient) * 57.29578f;

                    if (slopeDegrees < minSlope) minSlope = slopeDegrees;
                    if (slopeDegrees > maxSlope) maxSlope = slopeDegrees;
                    sumSlope += slopeDegrees;

                    if (slopeDegrees > 60f) over60++;
                    if (slopeDegrees < 10f) under10++;
                    totalValid++;
                }
            }
        }

        float avgHeight = (float)(sumHeight / (resolution * resolution));
        float avgSlope = (float)(sumSlope / totalValid);
        float pctOver60 = (over60 / (float)totalValid) * 100f;
        float pctUnder10 = (under10 / (float)totalValid) * 100f;

        using (StreamWriter writer = new StreamWriter("c:/hades/Hecton8/SlopeTestResult.txt"))
        {
            writer.WriteLine($"Heights: min={minHeight:F2}, max={maxHeight:F2}, avg={avgHeight:F2}, delta={delta:F2}");
            writer.WriteLine($"Histogram: {string.Join(", ", histogram)}");
            writer.WriteLine($"Slopes: min={minSlope:F2}, max={maxSlope:F2}, avg={avgSlope:F2}");
            writer.WriteLine($"Over 60 degrees: {pctOver60:F2}%");
            writer.WriteLine($"Under 10 degrees: {pctUnder10:F2}%");
        }

        Debug.Log($"Heights: min={minHeight:F2}, max={maxHeight:F2}, avg={avgHeight:F2}, delta={delta:F2}\n" +
                  $"Histogram: {string.Join(", ", histogram)}\n" +
                  $"Slopes: min={minSlope:F2}, max={maxSlope:F2}, avg={avgSlope:F2}\n" +
                  $"Over 60 degrees: {pctOver60:F2}%\n" +
                  $"Under 10 degrees: {pctUnder10:F2}%");

        // Do not exit if run from menu
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }
}
