using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hecton8.World;

public static class TestMathDump
{
    // Was C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\test_math_hillshade.png - another agent's
    // private scratch directory, outside the repo, unversioned, and invisible to anyone auditing this
    // project's terrain evidence. `static readonly` rather than `const` because Path.Combine is not a
    // compile-time constant (a `const` here is CS0133).
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "test_math_dump");

    [MenuItem("Hecton8/Diagnostics/Test Math Dump")]
    public static void Dump()
    {
        // Encodes a PNG from a Texture2D. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans
        // -nographics for this class of tool because anything that goes through the graphics device
        // returns zeros with no GPU context, and a uniformly black hillshade reads as "the trench math
        // produces flat terrain" rather than as a failed run. Tools/BatchTasks/run_test.bat currently
        // passes -nographics to this very method, so this branch is the one it will hit until that script
        // is corrected - which is the point: an informative refusal instead of a fabricated result.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                "[TestMathDump] REFUSED: no GPU context (graphicsDeviceType == Null). The hillshade PNG " +
                "would be zeros, which reads as flat terrain rather than as a failed run. Remove " +
                "-nographics from the batch invocation and run again.");
            EditorApplication.Exit(3);
            return;
        }

        try
        {
            Directory.CreateDirectory(OutputDir);
            DoDump();
        }
        catch (Exception ex)
        {
            // Previously there was no catch at all and the method ended in an unconditional Exit(0), so a
            // throw from File.WriteAllBytes into the (possibly nonexistent) foreign brain directory left
            // no hillshade behind while the exit code said the dump had succeeded.
            Debug.LogError($"[TestMathDump] FAILED, no hillshade PNG was written: {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(0);
    }

    private static void DoDump()
    {
        int res = 1024;
        float size = 10000f;
        float pixelSize = size / res;

        Texture2D texHill = new Texture2D(res, res, TextureFormat.RGB24, false);
        float[] heights = new float[res * res];

        // Deliberately degenerate isolation parameters: ridges, plate warp and shelf run are zeroed so the
        // trench/island terms are the only thing left in the signal. Do not "correct" these towards the
        // authored graph values - that would delete the isolation this tool exists for.
        //
        // MacroGeologyArtifactVersion is set to 3 while WorldMacroGeologyFields.ArtifactVersion is 12u
        // (WorldMacroGeologyFields.cs:193). That mismatch is inert HERE because this tool calls
        // HectonSandboxAbyssalShelfMath.EvaluateHeightMeters directly, and only the JOB path consults the
        // version to choose between the live geology branch and this legacy math branch
        // (HectonSandboxAbyssalShelfJobs.cs:719, :797). So this probe measures the LEGACY function by
        // design. If anyone ever reroutes it through the job, the 3 would silently keep it on the legacy
        // branch while the report claims to describe the shipped terrain.
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

        float minH = float.MaxValue;
        float maxH = float.MinValue;
        int nonFiniteCount = 0;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float absoluteX = x * pixelSize;
                float absoluteZ = z * pixelSize;

                AbsoluteUniversePosition aup = HectonSandboxAbyssalShelfMath.BuildAupXZ(absoluteX, absoluteZ, p.AupCellSizeMeters);
                float h = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in aup, in p);
                heights[z * res + x] = h;

                if (float.IsNaN(h) || float.IsInfinity(h)) { nonFiniteCount++; continue; }
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }
        }

        // A hillshade of a constant field is a uniform grey image that looks like a plausible render. The
        // measured range is the only thing that separates "the math produced relief" from "the math
        // produced one number", so it goes in the log where the batchmode reader actually sees it.
        if (nonFiniteCount == res * res)
        {
            throw new InvalidOperationException(
                $"every one of the {res * res} samples from HectonSandboxAbyssalShelfMath." +
                "EvaluateHeightMeters was NaN or infinite, so there is no height field to shade.");
        }

        Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;

        for (int z = 1; z < res - 1; z++)
        {
            for (int x = 1; x < res - 1; x++)
            {
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
        string outPath = Path.Combine(OutputDir, "test_math_hillshade.png");
        File.WriteAllBytes(outPath, texHill.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texHill);

        Debug.Log(
            $"[TestMathDump] Wrote {outPath} ({res}x{res}, {size}m window, {pixelSize:F3} m/px). " +
            $"Height range min={minH:F1}m max={maxH:F1}m amplitude={(maxH - minH):F1}m " +
            $"nonFinite={nonFiniteCount}. A near-zero amplitude means the isolated trench/island terms " +
            "are inert, not that the render failed.");
    }
}
