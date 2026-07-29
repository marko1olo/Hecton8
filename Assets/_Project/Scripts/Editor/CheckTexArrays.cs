using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Inspects the terrain material's <c>_AlbedoArray</c> and dumps every slice to PNG so the array's
/// CONTENTS can be checked, not just its dimensions.
///
/// WHAT WAS WRONG, and why it mattered more here than in most of these tools:
///
/// * both failure branches - "No mat" and "No array" - logged an error and then `return`ed WITHOUT
///   calling <c>EditorApplication.Exit</c> at all. Under <c>-quit</c> that ends the process at 0; without
///   <c>-quit</c> it hangs the batch forever. Either way the run did not report that the array it exists
///   to inspect was never found;
/// * with a non-empty material and a zero-depth array the loop body never ran and the tool exited 0
///   having written no slice. "Verified" with no evidence produced;
/// * the slices went to another agent's private brain directory
///   (<c>~/.gemini/antigravity/brain/389e4a53-.../</c>), which TerrainShaderVerify, DumpSplat,
///   DumpSplatmaps, ExportArraySlices and FixAndShoot ALSO wrote into. Five tools, one unversioned
///   folder outside the repo;
/// * <see cref="Texture2DArray.GetPixels"/> reads the CPU-side copy of the slice. The arrays this
///   project builds are filled with <see cref="Graphics.CopyTexture"/> on the GPU
///   (<c>TerrainShaderVerify.cs</c> BuildTextureArray), so a slice can legitimately have NO CPU data -
///   and the dump then contains zeros that look exactly like a black texture. That is the same symptom
///   as every unexplained terrain result in this project, so a uniform slice is now called out by name
///   instead of being handed over as a measurement.
/// </summary>
public static class CheckTexArrays
{
    private const string ToolName = "CheckTexArrays";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is
    /// not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_tex_arrays");

    private const string MaterialPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";

    /// <summary>A PNG smaller than this is a truncated or empty write, not a picture of anything.</summary>
    private const int MinimumPngBytes = 512;

    public static void Execute()
    {
        // PART 4. This tool calls GetPixels on a GPU-populated Texture2DArray and EncodeToPNG on the
        // result. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 - "compute shaders and
        // Graphics.Blit return zeros with no GPU context". Under -nographics the array under inspection
        // cannot have been built in the first place, so every slice this tool writes would be a black
        // PNG that reads as a genuine finding about the terrain textures.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). The texture array " +
                "this inspects is built on the GPU, so every slice would dump as zeros and read as a " +
                "black albedo texture rather than as a failed run. Remove -nographics from the batch " +
                "script.");
            EditorApplication.Exit(3);
            return;
        }

        int slicesWritten = 0;
        int uniformSlices = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: no material at '{MaterialPath}'. No texture array was " +
                    "inspected and no slice was written - nothing about the terrain arrays is verified " +
                    "by this run.");
                EditorApplication.Exit(2);
                return;
            }

            Texture2DArray albedoArray = mat.GetTexture("_AlbedoArray") as Texture2DArray;
            if (albedoArray == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{MaterialPath}' has no Texture2DArray bound to _AlbedoArray " +
                    "(property missing, unset, or bound to a plain Texture2D). No slice was written.");
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log(
                $"[{ToolName}] _AlbedoArray {albedoArray.width}x{albedoArray.height} " +
                $"depth={albedoArray.depth} format={albedoArray.format} mips={albedoArray.mipmapCount}");

            if (albedoArray.depth < 1)
            {
                // Was: the for loop simply did not execute and the tool exited 0.
                Debug.LogError(
                    $"[{ToolName}] FAILED: _AlbedoArray has depth {albedoArray.depth}, so there is " +
                    "nothing to dump. An array with no slices cannot texture anything; this is a real " +
                    "failure, not an empty success.");
                EditorApplication.Exit(2);
                return;
            }

            for (int i = 0; i < albedoArray.depth; i++)
            {
                Color[] px = albedoArray.GetPixels(i, 0);
                if (px == null || px.Length == 0)
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: GetPixels returned no data for slice {i}. The array has " +
                        "no readable CPU copy (it was filled on the GPU, or 'Read/Write Enabled' is off " +
                        "on the source textures), so its contents cannot be inspected this way.");
                    EditorApplication.Exit(2);
                    return;
                }

                // A slice where every pixel is identical is what a failed upload looks like. Name it -
                // handing it over as a PNG is how "zeros that look like data" gets reasoned from.
                bool uniform = true;
                for (int p = 1; p < px.Length; p++)
                {
                    if (px[p] != px[0]) { uniform = false; break; }
                }

                if (uniform)
                {
                    uniformSlices++;
                    Debug.LogWarning(
                        $"[{ToolName}] slice {i} is UNIFORM {px[0]} across all {px.Length} pixels. That " +
                        "is the shape of a slice that was never populated, not of an albedo texture. Do " +
                        "not read the PNG as evidence about the source texture.");
                }

                // RGBA32 rather than albedoArray.format: SetPixels throws on a compressed destination
                // format (BC7/DXT), which is exactly what a terrain albedo array is imported as, so the
                // previous `new Texture2D(w, h, albedoArray.format, false)` could not survive the very
                // case this tool exists for.
                Texture2D tex = new Texture2D(albedoArray.width, albedoArray.height, TextureFormat.RGBA32, false);
                string slicePath = Path.Combine(OutputDir, $"Slice_{i}.png");
                try
                {
                    tex.SetPixels(px);
                    tex.Apply();
                    File.WriteAllBytes(slicePath, tex.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                if (!ArtifactIsPlausible(slicePath, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: slice {i} was encoded but the artifact is not usable - " +
                        $"{slicePath} {detail}.");
                    EditorApplication.Exit(2);
                    return;
                }

                slicesWritten++;
                Debug.Log($"[{ToolName}] wrote slice {i} -> {slicePath} ({detail})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED after writing {slicesWritten} slice(s) to {OutputDir}; the array " +
                $"inspection is incomplete. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log(
            $"[{ToolName}] wrote {slicesWritten} verified slice(s) to {OutputDir}" +
            (uniformSlices > 0 ? $"; {uniformSlices} of them are UNIFORM and prove nothing." : "."));
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Confirms the write actually landed. This proves the file exists and is not empty or truncated;
    /// it cannot prove the image is not black - the uniform-slice check above is what covers that, and
    /// visual acceptance is <c>Docs/QUALITY_GATES.md:176</c>'s job, never this file's.
    /// </summary>
    private static bool ArtifactIsPlausible(string path, out string detail)
    {
        if (!File.Exists(path))
        {
            detail = "does not exist on disk after the write";
            return false;
        }

        long length = new FileInfo(path).Length;
        if (length < MinimumPngBytes)
        {
            detail = $"is {length} bytes, below the {MinimumPngBytes}-byte floor for a real PNG";
            return false;
        }

        detail = $"{length} bytes";
        return true;
    }
}
