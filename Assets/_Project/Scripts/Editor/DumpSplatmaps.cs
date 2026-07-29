using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;

/// <summary>
/// Dumps the sandbox terrain's alphamap textures to PNG, reporting the alphamap resolution alongside.
/// Sibling of <c>DumpSplat</c>, which uses <c>Terrain.activeTerrains</c> where this one uses
/// <c>FindObjectsByType&lt;Terrain&gt;</c>; they can legitimately disagree, which is why both are kept.
///
/// WHAT WAS WRONG:
///
/// * the whole body was wrapped in <c>if (terrains.Length &gt; 0) { ... }</c> and
///   <c>EditorApplication.Exit(0)</c> sat OUTSIDE it. A scene with no terrain therefore dumped nothing
///   and reported success - the single cheapest way to make "the terrain never generated" look like "the
///   splatmaps are fine";
/// * the PNGs went to a hardcoded <c>C:\Users\danat\.gemini\antigravity\brain\389e4a53-...</c> - another
///   agent's private scratch directory, on another user's profile, with no
///   <c>Directory.CreateDirectory</c>. Four other tools wrote into that same folder, one of them
///   (<c>DumpSplat</c>) with the near-identical filename <c>Splat_{i}.png</c>;
/// * there was no try/catch at all, so a throw left <c>Execute</c> with no exit code;
/// * it logged under the tag <c>[FAS]</c>, which is FixAndShoot's tag. Two unrelated tools sharing a log
///   prefix in one batchmode log is the same evidence collision as two tools sharing an output folder.
/// </summary>
public class DumpSplatmaps
{
    private const string ToolName = "DumpSplatmaps";

    /// <summary>
    /// Per-tool subfolder inside the repo. `static readonly` rather than `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "dump_splatmaps");

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    private const int MinimumPngBytes = 512;

    public static void Execute()
    {
        // PART 4. Graphics.Blit + ReadPixels + EncodeToPNG.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37: "compute shaders and Graphics.Blit
        // return zeros with no GPU context". An all-zero splatmap dump is a picture of a terrain with no
        // layer weights anywhere, and it is indistinguishable from a real one.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). Graphics.Blit and " +
                "ReadPixels return zeros here, so the dumped splatmaps would be uniformly empty and " +
                "would read as a terrain layering failure. Remove -nographics from the batch script.");
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

            // Signature left exactly as it was: this overload is the one already proven to compile in
            // this assembly, and an unverifiable "improvement" to a Find* call is not worth a CS1501 in
            // an assembly the lock-free compile gate cannot check (CONTRIBUTING.md, false CS0433/CS0656
            // on Hecton8.Editor).
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>();
            if (terrains.Length == 0)
            {
                // Was: fell straight through to Exit(0).
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{ScenePath}' contains no Terrain, so no splatmap was " +
                    "dumped. Nothing was measured by this run.");
                EditorApplication.Exit(2);
                return;
            }

            Terrain t = terrains[0];
            if (t.terrainData == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: terrain '{t.name}' has no TerrainData, so it has no alphamaps.");
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log(
                $"[{ToolName}] terrain '{t.name}' alphamapResolution=" +
                $"{t.terrainData.alphamapResolution} alphamapLayers={t.terrainData.alphamapLayers}");

            Texture2D[] alphamaps = t.terrainData.alphamapTextures;
            if (alphamaps == null || alphamaps.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: terrain '{t.name}' reports " +
                    $"alphamapResolution={t.terrainData.alphamapResolution} but exposes no alphamap " +
                    "textures, so there is nothing to dump.");
                EditorApplication.Exit(2);
                return;
            }

            for (int i = 0; i < alphamaps.Length; i++)
            {
                Texture2D tex = alphamaps[i];
                if (tex == null)
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: alphamap texture {i} of '{t.name}' is null; the dump is " +
                        "incomplete.");
                    EditorApplication.Exit(2);
                    return;
                }

                string path = Path.Combine(OutputDir, $"Splatmap_{i}.png");
                RenderTexture rt = RenderTexture.GetTemporary(
                    tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Texture2D temp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);

                try
                {
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    temp.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    temp.Apply();
                    File.WriteAllBytes(path, temp.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.DestroyImmediate(temp);
                }

                if (!ArtifactIsPlausible(path, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: splatmap {i} encoded without throwing but the artifact " +
                        $"is not usable - {path} {detail}. Not reporting a dump that produced no file.");
                    EditorApplication.Exit(2);
                    return;
                }

                dumped++;
                Debug.Log($"[{ToolName}] saved splatmap {i} ({tex.width}x{tex.height}) -> {path} ({detail})");
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
    /// Opens the scene only when nothing would be lost, mirroring
    /// <c>H8_RouteCaptureStation.cs:459-471</c>. In a shared working tree an unconditional OpenScene
    /// silently destroys another lane's unsaved edits.
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
                "and opening would discard them. No splatmap was dumped.");
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
