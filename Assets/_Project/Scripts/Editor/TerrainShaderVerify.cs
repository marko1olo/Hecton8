using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// VERIFIER AND MUTATOR for the custom terrain shader path. It opens 020_RENDER_SANDBOX, builds
/// Texture2DArrays from the reference terrain's layer textures, writes them over
/// Assets/_Project/Art/Materials/Terrain/Terrain_*Array.asset, points HectonTerrainMaterial.mat at them,
/// assigns that material to every terrain in the scene, saves the scene, and captures two 1920x1080 PNGs.
///
/// WHAT EXIT 0 MEANS NOW. All three arrays were built at a format every slice could actually be written
/// in, the material was saved and proven clean on disk, it was assigned to every terrain, BOTH PNGs exist
/// on disk above a byte floor, and the scene write returned true. It does NOT mean the terrain looks
/// right: a raw diagnostic capture can only REJECT, never accept
/// (C:\hades\.claude\rules\hecton8-shaders-compute.md:24-26).
///
/// WHAT WAS WRONG. This is a verifier, so every one of these reported a pass it had not earned - or could
/// not report anything at all:
///
///   * both screenshots were written to
///     $USERPROFILE/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4, another agent's
///     private scratch directory outside the repo. It was assembled from the user profile at RUNTIME, so
///     a text search for the literal path did not find it. Nothing ever created that directory, so on any
///     machine where it is absent the first File.WriteAllBytes threw, the exception escaped Execute
///     uncaught, and NO exit code was ever set at all.
///   * "No camera found!" was a LogError followed by fall-through to EditorApplication.Exit(0): zero
///     screenshots, reported as success. That single branch is the entire defect class.
///   * shader.isSupported was LOGGED and never checked. A shader the platform cannot compile yields a
///     magenta capture and exit 0.
///   * only the albedo array was null-checked. A null normal or mask array was assigned to the material
///     and the run still exited 0.
///   * SetTexture was called without HasProperty, so if the shader's array property names ever drift the
///     assignments are silently dropped and the "verification" is of an unbound material.
///   * Graphics.CopyTexture is a logged NO-OP when source and destination formats differ, and ReadPixels
///     cannot write into a compressed texture. The array format was taken from the LAST non-null layer
///     texture while the missing-layer fallback was always RGBA32 - so a mixed layer set (one layer with
///     a mask map, one without, which is the normal case) produced an array of uninitialised slices and
///     exited 0. That is the silent degeneracy named in
///     C:\hades\.claude\rules\hecton8-runtime-source.md: plausible-looking output from an inert system.
///   * the four precondition failures exited 1, which is outside the 0/2/3/4 vocabulary this instrument
///     layer uses.
///   * AssetDatabase.SaveAssets() flushes EVERY dirty asset in the project and would commit a concurrent
///     session's unfinished authoring in this shared tree. Now SaveAssetIfDirty on the one material,
///     proven with EditorUtility.IsDirty.
///   * EditorSceneManager.SaveOpenScenes() returns a bool that was discarded and the next line logged
///     "Done! Scene saved." unconditionally. Now SaveScene on the one scene this tool opened, result
///     checked.
///
/// SHIPPED ART: REPORTED, NOT CHANGED. Every write target is preserved byte-for-byte as it was; only the
/// reporting changed. For the owner's attention: HectonTerrainMaterial.mat currently references the 89 MB
/// arrays under Assets/_SourceData/Terrain/TextureArrays/, while this tool repoints it at arrays it
/// rebuilds under Assets/_Project/Art/Materials/Terrain/ (the previous generation of those files is
/// 2.8 MB, i.e. 512 px). Running it is therefore a terrain-art downgrade plus a 60 MB scene rewrite. It
/// now names both in the log BEFORE it does it, instead of afterwards as a success line.
///
/// It also flips Read/Write Enabled on every layer texture it touches and reimports them, which
/// permanently edits shipped .meta files and doubles those textures' memory cost. No code path here needs
/// it (Graphics.Blit and Graphics.CopyTexture are GPU-side and ReadPixels reads the RenderTexture, not the
/// source), but removing it would be a behaviour change on shipped art, so it is preserved and logged.
/// </summary>
public static class TerrainShaderVerify
{
    private const string ToolName = "TerrainShaderVerify";

    /// <summary>
    /// Per-tool subfolder inside the repo. Was a foreign brain directory. The subfolder is not cosmetic:
    /// two tools in this project already wrote identical filenames into one shared directory and each run
    /// destroyed the other's evidence. <c>static readonly</c> rather than <c>const</c> because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "terrain_shader_verify");

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
    private const string ShaderName = "Hecton8/URP/Terrain_TextureArray";
    private const string MaterialPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";
    private const string ArrayFolder = "Assets/_Project/Art/Materials/Terrain/";

    private const string AlbedoProperty = "_AlbedoArray";
    private const string NormalProperty = "_NormalArray";
    private const string MaskProperty = "_MaskArray";

    private const int ShotWidth = 1920;
    private const int ShotHeight = 1080;

    /// <summary>A 1920x1080 PNG cannot be this small. Below it, EncodeToPNG produced nothing usable.</summary>
    private const int MinimumPngBytes = 1024;

    /// <summary>Proved it built the arrays, bound the material, and captured both verified shots.</summary>
    private const int ExitVerified = 0;

    /// <summary>Could not do the work, or crashed trying. Nothing is claimed about the terrain.</summary>
    private const int ExitFailed = 2;

    /// <summary>Refused: no GPU context, so every measurement here would be a fabricated zero.</summary>
    private const int ExitNoGpu = 3;

    /// <summary>
    /// Batch entry point. Called by reflection name - do not rename.
    /// </summary>
    public static void Execute()
    {
        // PART 4. This tool calls Graphics.Blit, Graphics.CopyTexture, Camera.Render, ReadPixels and
        // EncodeToPNG. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 names the trap directly:
        // "compute shaders and Graphics.Blit return zeros with no GPU context". Under -nographics this
        // tool would build three arrays of zeros, bind them to the shipped terrain material, save the
        // scene, write two black PNGs and exit 0 - and the black PNGs would then be read as a finding
        // about the terrain shader rather than as an editor launched with the wrong flags. Fully qualified
        // on purpose so this guard needs no using directive.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context, would return zeros. Remove -nographics. " +
                "Graphics.CopyTexture, Camera.Render and ReadPixels all produce zeros here, so this run " +
                "would overwrite the shipped terrain arrays and material with black data and call it a " +
                "verification.");
            EditorApplication.Exit(ExitNoGpu);
            return;
        }

        int exitCode;
        try
        {
            exitCode = Run();
        }
        catch (System.Exception ex)
        {
            // A throw can land anywhere between "opened the scene" and "captured the second shot", so
            // nothing downstream is verified and the shipped material may be half-rewritten. Say that in
            // the Unity log, which is the only channel anyone reads out of a batch run.
            Debug.LogError(
                $"[{ToolName}] FAILED mid-run: no verified terrain shader evidence was produced. The " +
                $"material at '{MaterialPath}', the arrays under '{ArrayFolder}' and the terrains in " +
                $"'{ScenePath}' must all be treated as changed-but-unverified. No usable screenshot exists " +
                $"under {OutputDir}. {ex}");
            exitCode = ExitFailed;
        }

        EditorApplication.Exit(exitCode);
    }

    private static int Run()
    {
        Directory.CreateDirectory(OutputDir);

        Debug.Log($"[{ToolName}] Starting terrain shader verification. Artifacts -> {OutputDir}");

        // Opens in Single mode, so unsaved edits in any currently open scene are discarded without a
        // prompt. Preserved as-is; a batchmode editor holds the only lock on this project.
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return Fail(
                $"scene '{ScenePath}' did not open (IsValid={scene.IsValid()}, isLoaded={scene.isLoaded}). " +
                "No array was built, no material was touched and no screenshot was taken.");
        }

        var terrains = Terrain.activeTerrains;
        Debug.Log($"[{ToolName}] Found {terrains.Length} active terrains in '{ScenePath}'.");

        if (terrains.Length == 0)
        {
            return Fail(
                $"no active terrains in '{ScenePath}', so there is nothing to assign the shader to and " +
                "nothing to photograph.");
        }

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            return Fail(
                $"Shader.Find(\"{ShaderName}\") returned null. The custom terrain shader is missing, " +
                "renamed, or failed to import - it cannot be verified.");
        }

        // Was logged and ignored. An unsupported shader renders magenta, which is exactly the kind of
        // result a verifier must never hand back with exit 0.
        if (!shader.isSupported)
        {
            return Fail(
                $"shader \"{ShaderName}\" reports isSupported=false on this device " +
                $"(graphicsDeviceType={SystemInfo.graphicsDeviceType}). It will render as the error shader, " +
                "so any capture of it is evidence about the platform, not about the terrain.");
        }
        Debug.Log($"[{ToolName}] Shader \"{ShaderName}\" found, isSupported=True.");

        Terrain refTerrain = null;
        foreach (var t in terrains)
        {
            if (t.terrainData != null && t.terrainData.terrainLayers != null && t.terrainData.terrainLayers.Length > 0)
            {
                refTerrain = t;
                break;
            }
        }

        if (refTerrain == null)
        {
            return Fail(
                $"none of the {terrains.Length} terrains in '{ScenePath}' carries terrainLayers, so no " +
                "texture array can be built from this scene.");
        }

        var layers = refTerrain.terrainData.terrainLayers;
        int layerCount = Mathf.Min(layers.Length, 4); // Our shader supports 4 layers in base pass
        Debug.Log($"[{ToolName}] Using {layerCount} of {layers.Length} layers from '{refTerrain.name}'.");

        // Announced BEFORE the writes, not as a success line afterwards. These are shipped assets.
        Debug.LogWarning(
            $"[{ToolName}] MUTATES SHIPPED ART: it is about to overwrite {ArrayFolder}Terrain_*Array.asset, " +
            $"repoint '{MaterialPath}' at them, set materialTemplate on all {terrains.Length} terrains and " +
            $"rewrite '{ScenePath}'. It also flips Read/Write Enabled on the layer textures it reads and " +
            "reimports them, which permanently edits their .meta files.");

        string reason;
        var albedoArray = BuildTextureArray(layers, layerCount, TextureType.Albedo, out reason);
        if (albedoArray == null)
        {
            return Fail($"could not build the albedo array: {reason}");
        }

        var normalArray = BuildTextureArray(layers, layerCount, TextureType.Normal, out reason);
        if (normalArray == null)
        {
            return Fail($"could not build the normal array: {reason}");
        }

        // Was never null-checked at all - only albedo was. A null mask array was bound to the shipped
        // material and the run exited 0.
        var maskArray = BuildTextureArray(layers, layerCount, TextureType.Mask, out reason);
        if (maskArray == null)
        {
            return Fail($"could not build the mask array: {reason}");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            var dir = Path.GetDirectoryName(MaterialPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log($"[{ToolName}] Created new material at '{MaterialPath}'.");

            mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                return Fail(
                    $"AssetDatabase.CreateAsset did not produce a loadable material at '{MaterialPath}'. " +
                    "The path may be outside the Assets folder or not writable.");
            }
        }
        else
        {
            mat.shader = shader;
            Debug.Log($"[{ToolName}] Set shader \"{ShaderName}\" on the existing '{MaterialPath}'.");
        }

        // Was SetTexture with no HasProperty guard. Unity silently ignores a SetTexture for a property the
        // shader does not declare, so a rename would leave all three arrays unbound and every capture
        // would be of a material this tool never actually configured.
        if (!mat.HasProperty(AlbedoProperty) || !mat.HasProperty(NormalProperty) || !mat.HasProperty(MaskProperty))
        {
            return Fail(
                $"'{MaterialPath}' with shader \"{ShaderName}\" does not declare all three array " +
                $"properties (HasProperty {AlbedoProperty}={mat.HasProperty(AlbedoProperty)}, " +
                $"{NormalProperty}={mat.HasProperty(NormalProperty)}, " +
                $"{MaskProperty}={mat.HasProperty(MaskProperty)}). SetTexture would be silently dropped " +
                "and nothing would be bound.");
        }

        mat.SetTexture(AlbedoProperty, albedoArray);
        mat.SetTexture(NormalProperty, normalArray);
        mat.SetTexture(MaskProperty, maskArray);
        EditorUtility.SetDirty(mat);

        // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
        // including a concurrent session's in-flight authoring in this shared working tree.
        AssetDatabase.SaveAssetIfDirty(mat);

        bool matStillDirty = EditorUtility.IsDirty(mat);
        if (matStillDirty)
        {
            return Fail(
                $"'{MaterialPath}' is STILL DIRTY after AssetDatabase.SaveAssetIfDirty, so the three array " +
                "bindings exist in memory only. The asset may be read-only or the save was rejected. Any " +
                "capture taken now would not correspond to anything on disk.");
        }
        Debug.Log($"[{ToolName}] Material '{MaterialPath}' configured with three arrays and flushed to disk.");

        foreach (var t in terrains)
        {
            t.materialTemplate = mat;
        }
        Debug.Log($"[{ToolName}] Assigned the material to all {terrains.Length} terrains.");

        var cam = Camera.main;
        if (cam == null)
        {
            var cams = Object.FindObjectsByType<Camera>();
            if (cams.Length > 0) cam = cams[0];
        }

        // WAS THE HEADLINE BUG: LogError, then fall through to Exit(0) with zero screenshots on disk.
        if (cam == null)
        {
            return Fail(
                $"no camera in '{ScenePath}', so no screenshot could be taken. The material and the three " +
                "arrays HAVE already been written and the terrains reassigned in memory - this run changed " +
                "shipped art and produced no visual evidence for it. This branch used to exit 0.");
        }

        Debug.Log($"[{ToolName}] Camera '{cam.name}' at {cam.transform.position}.");

        string shot0 = Path.Combine(OutputDir, "TerrainVerify_0.png");
        if (!CaptureFromCamera(cam, shot0, out reason))
        {
            return Fail($"the first capture (scene camera as authored) produced no usable artifact: {reason}");
        }

        var terrainPos = refTerrain.transform.position;
        var terrainSize = refTerrain.terrainData.size;
        var center = terrainPos + terrainSize * 0.5f;
        center.y = terrainPos.y + terrainSize.y * 0.3f;
        cam.transform.position = center + Vector3.up * 50f + Vector3.back * 100f;
        cam.transform.LookAt(center);
        Debug.Log($"[{ToolName}] Repositioned camera to {cam.transform.position} looking at {center}.");

        string shot1 = Path.Combine(OutputDir, "TerrainVerify_1.png");
        if (!CaptureFromCamera(cam, shot1, out reason))
        {
            return Fail($"the second capture (framed on '{refTerrain.name}') produced no usable artifact: {reason}");
        }

        // Was SaveOpenScenes() with its bool discarded, followed by an unconditional "Done! Scene saved."
        // SaveScene names the one scene this tool opened rather than flushing everything open - the same
        // shared-tree reasoning as CreateSandboxV2.cs:93-95, whose two-argument call proves this overload
        // compiles in this project.
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            return Fail(
                $"EditorSceneManager.SaveScene returned false for '{ScenePath}': the materialTemplate " +
                "assignment on every terrain exists in memory only. Both screenshots were captured and are " +
                $"valid, but they show a scene state that is not on disk. Artifacts: {OutputDir}");
        }

        Debug.Log(
            $"[{ToolName}] VERIFIED: {layerCount}-slice albedo/normal/mask arrays built under " +
            $"'{ArrayFolder}', bound to '{MaterialPath}' and flushed, assigned to {terrains.Length} " +
            $"terrains, '{ScenePath}' saved, and two verified captures written to {OutputDir}. This is " +
            "evidence that the pipeline ran, NOT visual acceptance - a raw capture can only reject " +
            "(hecton8-shaders-compute.md:24-26). Open both PNGs before judging the terrain.");
        return ExitVerified;
    }

    /// <summary>
    /// One place that logs a failure in the Unity log naming what was NOT produced, and returns the
    /// non-zero code. Every precondition failure used to exit 1, which is outside this layer's vocabulary.
    /// </summary>
    private static int Fail(string what)
    {
        Debug.LogError($"[{ToolName}] FAILED: {what}");
        return ExitFailed;
    }

    private enum TextureType { Albedo, Normal, Mask }

    /// <summary>
    /// Builds one Texture2DArray and writes it as an asset. Returns null with a reason rather than
    /// handing back an array whose slices were never actually written.
    /// </summary>
    private static Texture2DArray BuildTextureArray(TerrainLayer[] layers, int count, TextureType type, out string reason)
    {
        reason = null;

        // These two used to be overwritten by EVERY non-null source in the scan loop, so the array ended
        // up with the LAST layer's size and format and every earlier slice silently mismatched. First
        // valid source wins now, and any source that disagrees with it is a hard failure below.
        int size = 512;
        TextureFormat fmt = TextureFormat.RGBA32;
        bool sizeAndFormatChosen = false;
        string formatSource = "no layer texture at all (default 512 RGBA32)";

        Texture2D[] sources = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            Texture2D src = null;
            switch (type)
            {
                case TextureType.Albedo:
                    src = layers[i]?.diffuseTexture;
                    break;
                case TextureType.Normal:
                    src = layers[i]?.normalMapTexture;
                    break;
                case TextureType.Mask:
                    src = layers[i]?.maskMapTexture;
                    break;
            }

            if (src != null)
            {
                // Preserved, and now stated out loud: this permanently edits the texture's .meta and
                // doubles its memory cost. Nothing in this method needs a CPU-readable source.
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    Debug.LogWarning(
                        $"[{ToolName}] permanently setting Read/Write Enabled on shipped texture " +
                        $"'{AssetDatabase.GetAssetPath(src)}' and reimporting it. This edits the .meta and " +
                        "doubles that texture's runtime memory; it is not reverted after the run.");
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                sources[i] = src;
                if (!sizeAndFormatChosen)
                {
                    size = src.width;
                    fmt = src.format;
                    sizeAndFormatChosen = true;
                    formatSource = $"layer[{i}] '{src.name}' ({src.width}x{src.height} {src.format})";
                }
            }

            Debug.Log($"[{ToolName}] {type} layer[{i}]: " +
                      (src != null ? $"{src.name} {src.width}x{src.height} {src.format}" : "NULL"));
        }

        // Graphics.CopyTexture is a NO-OP (it only logs) when the formats differ, and ReadPixels cannot
        // write into a compressed texture. Both of those failure modes leave a slice UNINITIALISED while
        // every log line above still reads healthy, which is how this tool produced garbage arrays and
        // exited 0. Everything that cannot be written correctly is now a hard failure instead.
        bool canFillOnCpu = IsCpuWritableFormat(fmt);
        for (int i = 0; i < count; i++)
        {
            Texture2D src = sources[i];

            if (src == null)
            {
                if (!canFillOnCpu)
                {
                    reason =
                        $"layer[{i}] has no {type} texture, so slice {i} would have to be filled with a " +
                        $"default colour - but the array format is {fmt} (taken from {formatSource}), " +
                        "which SetPixels cannot write. The old code copied an RGBA32 fill texture into " +
                        $"this array anyway; Unity logged a format mismatch, the copy did nothing, slice " +
                        $"{i} stayed uninitialised, and the run exited 0. Give every layer a {type} map, " +
                        "or build this array from uncompressed sources.";
                    return null;
                }
                continue;
            }

            if (src.format != fmt)
            {
                reason =
                    $"layer[{i}] '{src.name}' is {src.format} but the array is {fmt} (from {formatSource}). " +
                    $"Graphics.CopyTexture refuses mismatched formats and only logs, so slice {i} would be " +
                    "uninitialised. Reimport the layer textures to one common format.";
                return null;
            }

            if ((src.width != size || src.height != size) && !canFillOnCpu)
            {
                reason =
                    $"layer[{i}] '{src.name}' is {src.width}x{src.height} but the array is {size}x{size}, " +
                    $"so it needs a Blit and a ReadPixels - and ReadPixels cannot write into the compressed " +
                    $"array format {fmt}. Reimport the layer textures at one common resolution.";
                return null;
            }
        }

        var arr = new Texture2DArray(size, size, count, fmt, true);

        for (int i = 0; i < count; i++)
        {
            if (sources[i] != null)
            {
                var src = sources[i];
                if (src.width != size || src.height != size)
                {
                    var rt = RenderTexture.GetTemporary(size, size);
                    var resized = new Texture2D(size, size, fmt, true);
                    try
                    {
                        Graphics.Blit(src, rt);
                        RenderTexture.active = rt;
                        resized.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                        resized.Apply();
                        RenderTexture.active = null;
                        Graphics.CopyTexture(resized, 0, arr, i);
                    }
                    finally
                    {
                        // Was leaked on any throw: RenderTexture.active left dangling and the temporary
                        // never returned to the pool.
                        RenderTexture.active = null;
                        RenderTexture.ReleaseTemporary(rt);
                        Object.DestroyImmediate(resized);
                    }
                }
                else
                {
                    Graphics.CopyTexture(src, 0, arr, i);
                }
            }
            else
            {
                // Fill with a default colour. Guarded above: only reachable when fmt is CPU-writable, so
                // the fill texture and the array agree on format and the copy is real.
                var fallback = new Texture2D(size, size, fmt, true);
                try
                {
                    Color fillColor = type == TextureType.Normal ? new Color(0.5f, 0.5f, 1f, 1f) :
                                      type == TextureType.Mask ? new Color(0f, 1f, 0f, 0.5f) :
                                      Color.gray;
                    var pixels = new Color[size * size];
                    for (int p = 0; p < pixels.Length; p++) pixels[p] = fillColor;
                    fallback.SetPixels(pixels);
                    fallback.Apply();
                    Graphics.CopyTexture(fallback, 0, arr, i);
                }
                finally
                {
                    Object.DestroyImmediate(fallback);
                }
            }
        }

        arr.Apply();

        string arrayName = "Terrain_" + type + "Array";
        string path = ArrayFolder + arrayName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(arr, path);

        // CreateAsset writes immediately, so this is a real on-disk check and not a guess. Without it a
        // failed write is indistinguishable from a good one until the shader samples nothing.
        var written = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (written == null)
        {
            reason = $"AssetDatabase.CreateAsset did not produce a loadable Texture2DArray at '{path}'.";
            return null;
        }
        if (written.depth != count)
        {
            reason = $"'{path}' came back with depth {written.depth}, expected {count} slices.";
            return null;
        }

        Debug.Log($"[{ToolName}] Wrote {type} array '{path}': {size}x{size}x{count} {fmt}.");
        return arr;
    }

    /// <summary>
    /// TextureFormat values that <c>Texture2D.SetPixels</c> and <c>Texture2D.ReadPixels</c> can actually
    /// write into. A whitelist rather than GraphicsFormatUtility.IsCompressedFormat because that type
    /// lives in a namespace this assembly does not import, and Hecton8.Editor cannot be checked by the
    /// cheap compile gate (it emits phantom CS0433/CS0656 there), so every type used here is one this
    /// file can prove by reading. A format missing from this list is treated as unwritable, which
    /// produces a loud refusal rather than a silent uninitialised slice.
    /// </summary>
    private static bool IsCpuWritableFormat(TextureFormat fmt)
    {
        switch (fmt)
        {
            case TextureFormat.Alpha8:
            case TextureFormat.R8:
            case TextureFormat.R16:
            case TextureFormat.RHalf:
            case TextureFormat.RFloat:
            case TextureFormat.RG16:
            case TextureFormat.RGHalf:
            case TextureFormat.RGFloat:
            case TextureFormat.RGB24:
            case TextureFormat.RGB565:
            case TextureFormat.RGBA32:
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
            case TextureFormat.RGBAHalf:
            case TextureFormat.RGBAFloat:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Renders one shot and returns false with a reason unless the PNG is on disk and non-trivially
    /// sized. "EncodeToPNG did not throw" is not proof that a capture happened.
    /// </summary>
    private static bool CaptureFromCamera(Camera cam, string path, out string reason)
    {
        var rt = new RenderTexture(ShotWidth, ShotHeight, 24);
        var tex = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
        byte[] bytes;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
            tex.Apply();

            bytes = tex.EncodeToPNG();
        }
        finally
        {
            // Restored even on a throw. The old code only reset these on the success path, so a failed
            // capture left the camera rendering into a destroyed RenderTexture.
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        if (bytes == null || bytes.Length == 0)
        {
            reason = "EncodeToPNG returned no bytes";
            return false;
        }

        File.WriteAllBytes(path, bytes);

        if (!File.Exists(path))
        {
            reason = $"'{path}' does not exist on disk after File.WriteAllBytes";
            return false;
        }

        long length = new FileInfo(path).Length;
        if (length < MinimumPngBytes)
        {
            reason = $"'{path}' is {length} bytes, below the {MinimumPngBytes}-byte floor for a real " +
                     $"{ShotWidth}x{ShotHeight} PNG";
            return false;
        }

        reason = null;
        Debug.Log($"[{ToolName}] Screenshot verified on disk: {path} ({length} bytes).");
        return true;
    }
}
