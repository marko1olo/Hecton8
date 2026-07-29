using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Dumps the sandbox terrain's alphamap (splatmap) textures to PNG.
///
/// WHAT WAS WRONG - this file had four separate ways to report success while producing nothing:
///
/// * <c>if (terrains.Length == 0) return;</c> - no exit code, no error. Under <c>-quit</c> the process
///   ends at 0; without it the batch hangs. "The scene contained no terrain" and "here are your
///   splatmaps" were the same outcome to a log reader;
/// * <c>if (t.terrainData == null) return;</c> - same shape, same silence;
/// * an alphamap array of length 0 skipped the loop entirely and fell into
///   <c>EditorApplication.Exit(0)</c>;
/// * the PNGs went to <c>~/.gemini/antigravity/brain/389e4a53-.../Splat_{i}.png</c>, another agent's
///   private scratch directory, with no <c>Directory.CreateDirectory</c> - so on a machine without that
///   folder the first write threw uncaught and no exit code was set at all. DumpSplatmaps wrote
///   <c>Splatmap_{i}.png</c> into the SAME folder; two tools, one unversioned directory, near-identical
///   filenames.
///
/// It also called <see cref="EditorSceneManager.OpenScene"/> unconditionally, which discards unsaved
/// scene work without asking in batchmode. That guard is now present for the same reason
/// <c>H8_RouteCaptureStation.cs:459-471</c> carries it: with several lanes in one working tree, opening
/// over a dirty scene destroys another lane's work.
/// </summary>
public static class DumpSplat
{
    private const string ToolName = "DumpSplat";

    /// <summary>
    /// Per-tool subfolder inside the repo. `static readonly` rather than `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "dump_splat");

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    private const int MinimumPngBytes = 512;

    public static void Execute()
    {
        // PART 4. Graphics.Blit + ReadPixels + EncodeToPNG.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37: "compute shaders and Graphics.Blit
        // return zeros with no GPU context". A splatmap dump of zeros is a picture of a terrain with no
        // layer coverage anywhere - a specific, plausible, entirely fabricated finding.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). Graphics.Blit and " +
                "ReadPixels return zeros here, so every dumped splatmap would show zero layer coverage " +
                "while telling you nothing about the terrain. Remove -nographics from the batch script.");
            EditorApplication.Exit(3);
            return;
        }

        int dumped = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            if (!TryOpenSceneWithoutDiscardingWork(ScenePath))
            {
                EditorApplication.Exit(2);
                return;
            }

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{ScenePath}' has no active Terrain, so no splatmap was " +
                    "dumped. Nothing was measured.");
                EditorApplication.Exit(2);
                return;
            }

            Terrain t = terrains[0];
            if (t.terrainData == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: terrain '{t.name}' has no TerrainData, so it has no " +
                    "alphamaps to dump.");
                EditorApplication.Exit(2);
                return;
            }

            Texture2D[] maps = t.terrainData.alphamapTextures;
            if (maps == null || maps.Length == 0)
            {
                // Was: the loop skipped and the tool exited 0.
                Debug.LogError(
                    $"[{ToolName}] FAILED: terrain '{t.name}' has no alphamap textures " +
                    $"(alphamapResolution={t.terrainData.alphamapResolution}). A terrain with no " +
                    "splatmap cannot be textured; dumping nothing is not a pass.");
                EditorApplication.Exit(2);
                return;
            }

            for (int i = 0; i < maps.Length; i++)
            {
                Texture2D tex = maps[i];
                if (tex == null)
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: alphamap texture {i} of '{t.name}' is null. The dump is " +
                        "incomplete and the remaining maps were not written.");
                    EditorApplication.Exit(2);
                    return;
                }

                string path = Path.Combine(OutputDir, $"Splat_{i}.png");
                RenderTexture rt = new RenderTexture(tex.width, tex.height, 0);
                Texture2D t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);

                try
                {
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    t2d.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    t2d.Apply();
                    File.WriteAllBytes(path, t2d.EncodeToPNG());
                }
                finally
                {
                    // Was leaked whenever the write threw: active RT left set, RenderTexture undestroyed.
                    RenderTexture.active = null;
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(t2d);
                }

                if (!ArtifactIsPlausible(path, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: splatmap {i} encoded without throwing but the artifact " +
                        $"is not usable - {path} {detail}.");
                    EditorApplication.Exit(2);
                    return;
                }

                dumped++;
                Debug.Log($"[{ToolName}] dumped splatmap {i} ({tex.width}x{tex.height}) -> {path} ({detail})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED after dumping {dumped} splatmap(s) to {OutputDir}; the dump is " +
                $"incomplete. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log($"[{ToolName}] dumped {dumped} verified splatmap(s) to {OutputDir}");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Opens the scene only when nothing would be lost. <see cref="EditorSceneManager.OpenScene"/>
    /// discards unsaved edits without asking in batchmode; refusing is the correct outcome.
    /// </summary>
    private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isDirty)
                continue;

            Debug.LogError(
                $"[{ToolName}] REFUSED to open '{scenePath}': scene '{scene.name}' has unsaved changes " +
                "and opening would discard them. Save or discard deliberately, then re-run. No splatmap " +
                "was dumped.");
            return false;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        return true;
    }

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
