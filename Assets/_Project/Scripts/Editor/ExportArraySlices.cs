using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Exports every slice of the terrain material's <c>_AlbedoArray</c> to PNG by blitting each slice
/// through a RenderTexture, which is the GPU path (unlike <c>CheckTexArrays</c>, which reads the CPU
/// copy). Both exist because either one alone can be misleading: the CPU copy may be absent, and the
/// GPU path needs a device.
///
/// WHAT WAS WRONG:
///
/// * a zero-depth array made the export loop body never run, and the tool still reached
///   <c>EditorApplication.Exit(0)</c>. Zero PNGs written, reported as success;
/// * <c>File.WriteAllBytes</c> was called against another agent's private brain directory with no
///   <c>Directory.CreateDirectory</c> anywhere. On any machine where that scratch folder is absent -
///   which is every machine but one, and this one after it is cleared - the very first write threw, the
///   exception left <c>Execute</c> uncaught, and no exit code was ever set;
/// * the two precondition failures exited 1, which the exit-code vocabulary used across this
///   instrument layer does not define. 2 is "exception / could not do the work".
/// </summary>
public static class ExportArraySlices
{
    private const string ToolName = "ExportArraySlices";

    /// <summary>
    /// Per-tool subfolder, inside the repo. Was shared with four other tools in one foreign scratch
    /// directory. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not a
    /// compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "export_array_slices");

    private const string MaterialPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";

    private const int MinimumPngBytes = 512;

    public static void Execute()
    {
        // PART 4. Graphics.Blit, ReadPixels and EncodeToPNG - the exact call set
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 names: "compute shaders and
        // Graphics.Blit return zeros with no GPU context". With -nographics this tool writes one black
        // PNG per slice and exits 0, and the folder full of black squares reads as a finding about the
        // terrain albedo rather than as an editor launched wrong.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). Graphics.Blit and " +
                "ReadPixels return zeros here, so every exported slice would be a black PNG " +
                "indistinguishable from a real export. Remove -nographics from the batch script.");
            EditorApplication.Exit(3);
            return;
        }

        int written = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: no material at '{MaterialPath}'. No slice was exported.");
                EditorApplication.Exit(2);
                return;
            }

            Texture2DArray albedoArray = mat.GetTexture("_AlbedoArray") as Texture2DArray;
            if (albedoArray == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{MaterialPath}' has no Texture2DArray on _AlbedoArray. No " +
                    "slice was exported.");
                EditorApplication.Exit(2);
                return;
            }

            if (albedoArray.depth < 1)
            {
                // Was: silent empty success.
                Debug.LogError(
                    $"[{ToolName}] FAILED: _AlbedoArray depth is {albedoArray.depth}, so there are no " +
                    "slices to export. Exporting nothing is not a pass.");
                EditorApplication.Exit(2);
                return;
            }

            for (int i = 0; i < albedoArray.depth; i++)
            {
                string slicePath = Path.Combine(OutputDir, $"AlbedoSlice_{i}.png");
                RenderTexture rt = RenderTexture.GetTemporary(
                    albedoArray.width, albedoArray.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Texture2D temp = new Texture2D(albedoArray.width, albedoArray.height, TextureFormat.RGB24, false);

                try
                {
                    Graphics.Blit(albedoArray, rt, i, 0);
                    RenderTexture.active = rt;
                    temp.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    temp.Apply();
                    File.WriteAllBytes(slicePath, temp.EncodeToPNG());
                }
                finally
                {
                    // Was leaked on any throw: RenderTexture.active left dangling and the temporary RT
                    // never returned to the pool.
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.DestroyImmediate(temp);
                }

                if (!ArtifactIsPlausible(slicePath, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: slice {i} encoded without throwing but the artifact is " +
                        $"not usable - {slicePath} {detail}. Not reporting an export that produced no file.");
                    EditorApplication.Exit(2);
                    return;
                }

                written++;
                Debug.Log($"[{ToolName}] exported slice {i} -> {slicePath} ({detail})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED after exporting {written} slice(s) to {OutputDir}; the export is " +
                $"incomplete. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log($"[{ToolName}] exported {written} verified slice(s) to {OutputDir}");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Confirms the bytes landed. Proves the file exists and is not truncated; it cannot prove the
    /// slice is not black. Visual acceptance stays with <c>Docs/QUALITY_GATES.md:176</c>.
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
