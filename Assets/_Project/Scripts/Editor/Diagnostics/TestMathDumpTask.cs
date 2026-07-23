using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hecton8.World;

public static class TestMathDump
{
    [MenuItem("Hecton8/Diagnostics/Test Math Dump")]
    public static void Dump()
    {
        int res = 1024;
        float size = 10000f;
        float pixelSize = size / res;

        Texture2D texHill = new Texture2D(res, res, TextureFormat.RGB24, false);
        float[] heights = new float[res * res];

        HectonSandboxAbyssalShelfParams p = new HectonSandboxAbyssalShelfParams
        {
            AupCellSizeMeters = 9.765625,
            LowWorldY = -6000f,
            HighWorldY = 2000f,
            Seed = 12345,
            MacroGeologyArtifactVersion = 3,
            DescentRadiusMeters = 15000,
            PlateCellSizeMeters = 1,
            RidgeHeightMeters = 0,
            RidgeMultiplier = 0,
            RidgeWidthMeters = 0.001f,
            JunctionWidthMeters = 0.001f,
            PlateUniformity = 0,
            DomainWarpMeters = 0,
            DomainWarpFrequency = 0.000001f,
            SlopeNoiseFrequency = 0.000001f,
            MacroExponentialFalloff = 0.1f,
            ShelfRunMeters = 1,
            ShelfTargetSlopeDegrees = 1,
            TrenchDepthMeters = 5000f,
            TrenchWidthMeters = 780f,
            TrenchSharpness = 2.4f,
            IslandCenterRadiusMeters = 2600f,
            IslandJunctionThreshold = 0.58f
        };

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float absoluteX = x * pixelSize;
                float absoluteZ = z * pixelSize;

                AbsoluteUniversePosition aup = HectonSandboxAbyssalShelfMath.BuildAupXZ(absoluteX, absoluteZ, p.AupCellSizeMeters);
                float h = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in aup, in p);
                heights[z * res + x] = h;
            }
        }

        Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;

        for (int z = 1; z < res - 1; z++)
        {
            for (int x = 1; x < res - 1; x++)
            {
                float hC = heights[(z) * res + (x)];
                float hL = heights[(z) * res + (x - 1)];
                float hR = heights[(z) * res + (x + 1)];
                float hD = heights[(z - 1) * res + (x)];
                float hU = heights[(z + 1) * res + (x)];

                Vector3 n = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                float intensity = Mathf.Max(0f, Vector3.Dot(n, lightDir));
                texHill.SetPixel(x, z, new Color(intensity, intensity, intensity, 1f));
            }
        }

        texHill.Apply();
        File.WriteAllBytes(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\test_math_hillshade.png", texHill.EncodeToPNG());

        EditorApplication.Exit(0);
    }
}
