using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Reports the imported <see cref="TextureFormat"/> of one specific terrain mask map.
///
/// It is a VERIFIER, and it used to be the worst kind: the "texture not found" branch logged a
/// <c>Debug.Log</c> (not an error) and then fell into the same <c>EditorApplication.Exit(0)</c> as the
/// success branch. A batchmode reader - which is how every verdict in this project is actually read -
/// saw exit 0 and a clean log, so "the asset this whole check is about does not exist" and "the format
/// is BC7" were indistinguishable outcomes. A verifier that cannot run its check must never report a
/// pass.
///
/// NO GPU REFUSAL HERE, on purpose. <c>tex.format</c> is importer metadata carried by the asset, not a
/// value read back off the GPU: nothing in this file blits, renders, reads pixels, encodes a PNG or
/// dispatches a compute shader. <c>C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37</c> bans
/// <c>-nographics</c> for tools whose numbers become zeros without a graphics device; this one's number
/// does not, and refusing anyway would block a check that is honest headless. If you ever add a
/// <c>SystemInfo.SupportsTextureFormat</c> or <c>IsFormatSupported</c> call here, that stops being true
/// and this file needs the refusal block the render tools carry.
/// </summary>
public static class CheckSupported
{
    private const string ToolName = "CheckSupported";

    /// <summary>
    /// The tool used to write nothing at all, so its verdict existed only in a Unity log that gets
    /// overwritten by the next batch run. Per-tool subfolder, not a shared <c>Logs/</c> root:
    /// Stage1Check and Stage1VerifyAndRelink wrote identical filenames into one directory and each run
    /// destroyed the other's evidence. `static readonly` rather than `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_supported");

    private const string TargetTexturePath =
        "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/Tiles/gemini_Batch20260608_TextureExpansion_b34_3408_clay_silt_turbidity_slope/TX_B34_gemini_Batch20260608_TextureExpansion_b34_3408_clay_silt_turbidity_slope_MaskMap.jpg";

    public static void Execute()
    {
        string reportPath = null;

        try
        {
            Directory.CreateDirectory(OutputDir);
            reportPath = Path.Combine(OutputDir, "format_report.txt");

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetTexturePath);
            if (tex == null)
            {
                // The check could not be run. This is the branch that used to exit 0.
                string missing =
                    $"MISSING: no Texture2D at '{TargetTexturePath}'. No format was read, so nothing " +
                    "about texture support was verified by this run.";
                File.WriteAllText(reportPath, missing + "\n");
                Debug.LogError($"[{ToolName}] FAILED: {missing} Report at {reportPath}");
                EditorApplication.Exit(2);
                return;
            }

            string line =
                $"path={TargetTexturePath}\nformat={tex.format}\nsize={tex.width}x{tex.height}\n" +
                $"mipmapCount={tex.mipmapCount}\ngraphicsDevice={SystemInfo.graphicsDeviceType}\n";
            File.WriteAllText(reportPath, line);
            Debug.Log($"[{ToolName}] format={tex.format} size={tex.width}x{tex.height}. Report at {reportPath}");
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all. An IO or import failure threw out of Execute, Unity logged it
            // where nobody greps, and `-quit` still ended the process at 0.
            Debug.LogError(
                $"[{ToolName}] FAILED: no format report was produced for '{TargetTexturePath}' " +
                $"(intended report path {reportPath ?? "<not resolved>"}). {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(0);
    }
}
