using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// MUTATOR of shipped authored content. Assigns <c>HectonTerrainMaterial</c> to every
/// <see cref="UnityEngine.Terrain"/> in two shipped scenes and SAVES those scenes, so every outcome now
/// speaks in the batchmode log AND in the exit code: CHANGED (0), ALREADY CORRECT (5), REFUSED/FAILED (2).
///
/// What it used to do wrong - all of which produced a success report:
///   - <b>the null-material branch had no <c>return</c></b>. It logged "Material not found!" and called
///     <c>EditorApplication.Exit(1)</c>, then FELL THROUGH into the loop and executed
///     <c>t.materialTemplate = null</c> on every terrain followed by <c>SaveScene</c>. Exit() requests a
///     quit; it does not unwind the current stack frame. So the single most likely failure of this tool -
///     a moved or renamed material - was also the one path that could strip the material off the shipped
///     world and write that to disk.
///   - <b>zero terrains was a success</b>. <c>count == 0</c> logged "Applied material to 0 terrains" and
///     ran on to <c>Exit(0)</c>. Zero means a stale scene path, a renamed object, or - the most likely
///     real invocation - no scene actually loaded. It is now a refusal, and the examined count is logged.
///   - <b>it enumerated <c>Terrain.activeTerrains</c></b>, which contains only ENABLED Terrain components
///     on ACTIVE GameObjects. A disabled terrain silently kept its old material while the log claimed the
///     scene was done. It now walks the opened scene's root objects with
///     <c>GetComponentsInChildren&lt;Terrain&gt;(true)</c>, which is scene-scoped (activeTerrains is
///     process-global and can carry survivors from a previously open scene) and includes inactive ones,
///     and it reports how many of the targets were inactive.
///   - <b>it saved unconditionally</b>. <c>SaveScene</c> ran even when <c>count</c> was 0 or when every
///     terrain already had the material, rewriting two shipped scene files in a working tree shared with a
///     concurrent authoring session. An idempotent re-run now writes nothing at all.
///   - <b>it ignored the <c>bool</c> that <see cref="EditorSceneManager.SaveScene(UnityEngine.SceneManagement.Scene)"/>
///     returns</b>, so a refused write (read-only file, lock) was indistinguishable from a real one. The
///     save is now proven by return value + <c>scene.isDirty</c> + the scene file's mtime before/after.
///   - <b>no exception guard at all.</b> <c>OpenScene</c> throws on a stale path; the throw escaped
///     <c>-executeMethod</c>, so the process never reached any Exit() and a launcher without <c>-quit</c>
///     hung forever.
///   - <b>it never checked the material it was about to bind.</b> This is the TerrainShaderVerify defect
///     in mutator form: a HectonTerrainMaterial whose <c>_AlbedoArray</c>/<c>_NormalArray</c>/<c>_MaskArray</c>
///     are unbound renders wrong on every terrain in the game, and this tool would have bound it to the
///     shipped world and exited 0. All three are now verified present-in-shader AND non-null before
///     anything is written, and the refusal names the fix (run <c>AssignTex</c> first).
///   - it wrote no artifact. A per-tool <c>Logs/apply_terrain_material/</c> file now survives the run.
///
/// The three property names were verified against the real shader, not assumed: HectonTerrain.shader
/// declares <c>_AlbedoArray</c>/<c>_NormalArray</c>/<c>_MaskArray</c> at lines 10-12, its GUID
/// 3395ccfb18535a34fa152b5ea83a1a89 is the <c>m_Shader</c> of HectonTerrainMaterial.mat:11, and
/// Hecton8.World.HectonTerrainMaterialInjector.cs:67-72 uses the same three names at runtime. Sibling
/// tools in this family were probing <c>_TerrainBaseMapArray</c>/<c>_TerrainNormalMapArray</c>/
/// <c>_TerrainMaskMapArray</c>, which exist nowhere in this repo. <see cref="Material.HasProperty(string)"/>
/// now guards each one, so a future shader swap is a loud refusal instead of a silent null.
///
/// NO GPU REFUSAL BLOCK HERE, on purpose, decided from the code: this tool assigns a serialized object
/// reference (<c>Terrain.materialTemplate</c>) and saves scene files. It never renders, blits, reads back
/// a texture, encodes a PNG or dispatches compute, so there is no number in it that degrades to zeros
/// without a graphics device. A <c>GraphicsDeviceType.Null</c> gate here would only make the tool
/// permanently unrunnable under a <c>-nographics</c> launcher - a false gate blocking correct runs. If
/// anyone later makes this task capture the terrain to prove the material took effect, that capture needs
/// the refusal + Exit(3) the render tools carry.
///
/// Scenes and materials are mutated only through the deserialized Unity objects, never by text-editing the
/// YAML: 02_HECTON_WORLD.unity is serialized as binary and hand-editing scene assets is banned here.
/// </summary>
public static class ApplyTerrainMaterial
{
    private const string ToolName = "ApplyTerrainMaterial";

    private const string MaterialPath =
        "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";

    /// <summary>
    /// Verified present on disk at audit time. A missing path is a refusal, not a crash and not a pass.
    /// Note that 020_RENDER_SANDBOX_V2.unity also exists; if the sandbox is ever superseded, this list is
    /// what goes stale, and the refusal below is what says so.
    /// </summary>
    private static readonly string[] ScenePaths =
    {
        "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity",
        "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
    };

    /// <summary>Verified against HectonTerrain.shader:10-12. Do not add a name without opening the shader.</summary>
    private static readonly string[] RequiredArrayProperties =
    {
        "_AlbedoArray",
        "_NormalArray",
        "_MaskArray",
    };

    /// <summary>
    /// Per-tool subfolder, not a shared <c>Logs/</c> root: two tools in this project already wrote
    /// identical filenames into one directory and each run destroyed the other's evidence.
    /// <c>static readonly</c> rather than <c>const</c> because <see cref="Path.Combine(string,string,string)"/>
    /// is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "apply_terrain_material");

    /// <summary>Proved it wrote the material assignment to at least one scene file on disk.</summary>
    private const int ExitChanged = 0;

    /// <summary>
    /// Could not do the job, or crashed trying, or refused a precondition. Nothing is claimed about the
    /// terrains. Refused and Failed deliberately share one code, as in DisableErosionNodeTask: both mean
    /// "do not believe anything about the terrain material after this run".
    /// </summary>
    private const int ExitFailed = 2;

    /// <summary>
    /// Every terrain in every listed scene already had this material: nothing written, nothing dirtied.
    /// Deliberately NOT 0, because "I re-bound the shipped world's terrain material" and "it was already
    /// correct" are different facts and a caller that only reads the exit code must be able to tell them
    /// apart. Non-zero here does not mean something broke - 2 means that. Outside the reserved 0/2/3/4
    /// taxonomy on purpose.
    /// </summary>
    private const int ExitAlreadyCorrect = 5;

    private enum Outcome
    {
        Changed,
        AlreadyCorrect,
        Refused,
    }

    /// <summary>
    /// Entry point invoked by reflection as <c>ApplyTerrainMaterial.Execute</c> from
    /// <c>-executeMethod</c> - DO NOT RENAME. Always reaches exactly one <see cref="EditorApplication.Exit"/>
    /// in batchmode (a launcher without <c>-quit</c> would otherwise hang), and never calls Exit outside
    /// batchmode, because <c>-executeMethod</c> also works against a GUI editor and quitting a human's
    /// session mid-authoring in this shared tree is worse than losing an exit code nobody is reading.
    /// Same convention as ReassignTextures.cs:22.
    /// </summary>
    public static void Execute()
    {
        Outcome outcome;
        int exitCode;

        try
        {
            outcome = Run();
            exitCode = ExitCodeFor(outcome);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED outside the guarded body. The terrain material state of " +
                $"{ScenePaths.Length} scene(s) is UNVERIFIED - some scenes may have been saved and others " +
                $"not - and no complete verdict file was written under {OutputDir}. {ex}");
            outcome = Outcome.Refused;
            exitCode = ExitFailed;
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
            return;
        }

        Debug.Log(
            $"[{ToolName}] interactive run finished: {outcome}. In batchmode this same outcome would be " +
            $"exit code {exitCode}. Exit() was not called so this editor session stays alive.");
    }

    private static int ExitCodeFor(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Changed: return ExitChanged;
            case Outcome.AlreadyCorrect: return ExitAlreadyCorrect;
            default: return ExitFailed;
        }
    }

    private static Outcome Run()
    {
        string phase = "startup";
        var report = new StringBuilder();

        try
        {
            phase = "creating the output directory";
            Directory.CreateDirectory(OutputDir);

            phase = "loading the terrain material";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                return Refuse(
                    report,
                    $"no Material at '{MaterialPath}' (missing, or the asset there is not a Material). NO " +
                    "terrain was touched and no scene was saved. If the material moved or was renamed, " +
                    "this tool's hardcoded path is stale. This branch used to log an error, call Exit(1), " +
                    "fall through without returning, and assign materialTemplate = null to every terrain " +
                    "in both scenes before saving them.");
            }

            phase = "verifying the material's texture arrays before binding it to shipped terrain";
            string bindingProblem = DescribeMaterialProblem(mat, report);
            if (bindingProblem != null)
            {
                return Refuse(
                    report,
                    $"{bindingProblem} Binding this material to the shipped terrain and exiting 0 would " +
                    "report a healthy world while every terrain rendered from an unbound texture array. " +
                    "Nothing was touched. Fix the material first - AssignTex.Execute assigns the three " +
                    "arrays - then re-run this tool.");
            }

            phase = "checking for unsaved scene edits belonging to another session";
            string dirtyScene = FindDirtySavedScene();
            if (dirtyScene != null)
            {
                return Refuse(
                    report,
                    $"scene '{dirtyScene}' is open with UNSAVED changes. EditorSceneManager.OpenScene " +
                    "replaces the open scene without prompting, which would silently discard them, and " +
                    "this working tree is shared with a concurrent authoring session. Nothing was touched. " +
                    "Save or discard that scene, then re-run.");
            }

            int totalExamined = 0;
            int totalChanged = 0;
            int totalAlready = 0;
            var failures = new List<string>();

            foreach (string scenePath in ScenePaths)
            {
                phase = $"applying the material in '{scenePath}'";

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    failures.Add(
                        $"no SceneAsset at '{scenePath}' - stale path, so this scene's terrain was NOT " +
                        "touched. OpenScene would have thrown here and the old tool had no catch.");
                    continue;
                }

                int examined;
                int changed;
                int already;
                string failure = ApplyToScene(scenePath, mat, report, out examined, out changed, out already);

                totalExamined += examined;
                totalChanged += changed;
                totalAlready += already;

                if (failure != null)
                {
                    failures.Add(failure);
                }
            }

            string tally =
                $"{totalExamined} terrain(s) examined across {ScenePaths.Length} scene path(s), " +
                $"{totalChanged} changed, {totalAlready} already correct, {failures.Count} scene(s) failed.";

            if (failures.Count > 0)
            {
                return Refuse(
                    report,
                    $"{tally} Per-scene failures: {string.Join(" | ", failures)}. Because at least one " +
                    "scene could not be proven, the terrain material state of this project is MIXED - do " +
                    "not read this run as an applied material anywhere except the scenes reported CHANGED " +
                    "above.");
            }

            if (totalExamined == 0)
            {
                return Refuse(
                    report,
                    "ZERO terrains examined. Nothing was changed and no scene was saved. Zero means a " +
                    "stale scene path, a renamed or deleted terrain object, or a scene that did not " +
                    "load - never a healthy project. The old tool logged 'Applied material to 0 " +
                    "terrains' and exited 0 here.");
            }

            if (totalChanged == 0)
            {
                string already =
                    $"ALREADY CORRECT: all {totalAlready} terrain(s) already reference '{mat.name}' " +
                    $"({MaterialPath}). Nothing was written, no object was marked dirty, no scene was " +
                    $"saved. Exit {ExitAlreadyCorrect} means idempotent no-op, not failure. {tally}";
                Debug.Log($"[{ToolName}] {already}");
                report.AppendLine(already);
                WriteVerdict(report);
                return Outcome.AlreadyCorrect;
            }

            string changedMsg =
                $"CHANGED: {totalChanged} terrain(s) now reference '{mat.name}' ({MaterialPath}) and every " +
                $"modified scene was proven written to disk. {tally} This changes what the shipped world " +
                "renders as - re-capture any terrain-appearance evidence taken before this run.";
            Debug.Log($"[{ToolName}] {changedMsg}");
            report.AppendLine(changedMsg);
            WriteVerdict(report);
            return Outcome.Changed;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED while {phase}. Scenes already reported CHANGED above are written; " +
                "any scene not reported is UNVERIFIED and may have been modified in memory without being " +
                $"saved. No complete verdict file exists under {OutputDir}. {ex}");
            report.AppendLine($"FAILED while {phase}: {ex}");
            TryWriteVerdict(report);
            return Outcome.Refused;
        }
    }

    /// <summary>
    /// Opens one scene, assigns the material to every Terrain in it (including inactive ones), and proves
    /// the save. Returns null on success, or a human-readable failure string naming what was NOT produced.
    /// Deliberately does NOT save when nothing changed: a no-op re-run must not rewrite a shipped scene
    /// file in a shared tree.
    /// </summary>
    private static string ApplyToScene(
        string scenePath,
        Material mat,
        StringBuilder report,
        out int examined,
        out int changed,
        out int already)
    {
        examined = 0;
        changed = 0;
        already = 0;

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return $"'{scenePath}' did not open (IsValid={scene.IsValid()}, isLoaded={scene.isLoaded}), so " +
                   "no terrain in it was examined or changed.";
        }

        // Scene-scoped and inactive-inclusive on purpose. Terrain.activeTerrains - what this tool used to
        // read - is process-global and holds only enabled components on active objects, so it both misses
        // disabled terrains and can include leftovers from another scene.
        var terrains = new List<UnityEngine.Terrain>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            terrains.AddRange(root.GetComponentsInChildren<UnityEngine.Terrain>(true));
        }

        examined = terrains.Count;
        int inactive = 0;
        int injected = 0;
        var changedNames = new List<string>();

        foreach (UnityEngine.Terrain t in terrains)
        {
            if (!t.enabled || !t.gameObject.activeInHierarchy)
            {
                inactive++;
            }

            // Reported, not "fixed": on a terrain carrying this component the authored materialTemplate is
            // replaced at runtime by an instanced copy (HectonTerrainMaterialInjector.cs:54-55), so the
            // scene-level assignment this tool makes is not the last word on what the player sees. Bound
            // to the real type so a rename is a compile error, not a silent zero.
            if (t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>() != null)
            {
                injected++;
            }

            if (t.materialTemplate == mat)
            {
                already++;
                continue;
            }

            Material previous = t.materialTemplate;
            changedNames.Add($"{t.name}: '{(previous == null ? "<null>" : previous.name)}' -> '{mat.name}'");

            t.materialTemplate = mat;
            EditorUtility.SetDirty(t);
            changed++;
        }

        string scanned =
            $"'{scenePath}': examined {examined} terrain(s) ({inactive} of them inactive or with a " +
            "disabled Terrain component - those are included, the old activeTerrains scan skipped them), " +
            $"{changed} changed, {already} already correct, {injected} carry " +
            "HectonTerrainMaterialInjector and will have this material replaced by an instanced copy at " +
            "runtime.";
        Debug.Log($"[{ToolName}] {scanned}");
        report.AppendLine(scanned);
        if (changedNames.Count > 0)
        {
            report.AppendLine("    " + string.Join("; ", changedNames));
        }

        if (examined == 0)
        {
            return $"'{scenePath}' opened but contains ZERO Terrain components, so nothing was applied and " +
                   "the scene was not saved. Zero means the wrong scene, a renamed object or a deleted " +
                   "terrain - the old tool reported this as success.";
        }

        if (changed == 0)
        {
            report.AppendLine($"    '{scenePath}' not saved: nothing needed changing.");
            return null;
        }

        // Resolved from Application.dataPath (always "<project>/Assets" in the editor), NOT from
        // Directory.GetCurrentDirectory(): a launcher passes -projectPath while the process cwd is wherever
        // it was invoked from, and a wrong cwd here would fail the disk proof on a write that did land.
        string absoluteScenePath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", scenePath.Replace('/', Path.DirectorySeparatorChar)));
        DateTime mtimeBefore = File.Exists(absoluteScenePath)
            ? File.GetLastWriteTimeUtc(absoluteScenePath)
            : DateTime.MinValue;

        // SetDirty on a component should already have dirtied the scene; marking it explicitly removes any
        // doubt that SaveScene has something to write, so the mtime proof below cannot fail spuriously.
        EditorSceneManager.MarkSceneDirty(scene);

        // Deliberately NOT SaveOpenScenes(): only the one scene this call opened and modified.
        bool saveReturned = EditorSceneManager.SaveScene(scene);

        DateTime mtimeAfter = File.Exists(absoluteScenePath)
            ? File.GetLastWriteTimeUtc(absoluteScenePath)
            : DateTime.MinValue;
        bool stillDirty = scene.isDirty;
        bool fileRewritten = mtimeAfter != mtimeBefore;

        // Exit 0 has to be earned. All three signals must agree, so the error can only ever be a false
        // alarm, never a false success.
        if (!saveReturned || stillDirty || !fileRewritten)
        {
            return $"'{scenePath}': {changed} terrain(s) were re-materialled IN MEMORY ONLY - the write was " +
                   $"not proven on disk. SaveScene returned {saveReturned} (expected True), scene.isDirty " +
                   $"after save = {stillDirty} (expected False), '{absoluteScenePath}' mtime " +
                   $"{mtimeBefore:O} -> {mtimeAfter:O} (expected a change). The file may be read-only or " +
                   "locked by the concurrent session. Treat this scene's terrain material as unknown.";
        }

        string saved =
            $"'{scenePath}' written to disk (mtime {mtimeBefore:O} -> {mtimeAfter:O}) with {changed} " +
            "terrain material assignment(s).";
        Debug.Log($"[{ToolName}] {saved}");
        report.AppendLine("    " + saved);
        return null;
    }

    /// <summary>
    /// Returns null if the material is safe to bind to shipped terrain, otherwise what is wrong with it.
    /// Checks all three arrays, not just albedo: TerrainShaderVerify null-checked only the albedo array,
    /// so a null normal or mask array reached the shipped material and it still exited 0. HasProperty
    /// separates "the shader does not declare this name" from "the name is right but the slot is empty" -
    /// two sibling tools reported healthy terrain as broken for years because they probed property names
    /// that do not exist in this repo.
    /// </summary>
    private static string DescribeMaterialProblem(Material mat, StringBuilder report)
    {
        Shader shader = mat.shader;
        if (shader == null)
        {
            return $"'{MaterialPath}' has a NULL shader.";
        }

        var missingProperties = new List<string>();
        var unboundProperties = new List<string>();
        var bound = new List<string>();

        foreach (string property in RequiredArrayProperties)
        {
            if (!mat.HasProperty(property))
            {
                missingProperties.Add(property);
                continue;
            }

            Texture texture = mat.GetTexture(property);
            if (texture == null)
            {
                unboundProperties.Add(property);
                continue;
            }

            bound.Add($"{property}='{texture.name}'");
        }

        string state =
            $"material '{mat.name}' uses shader '{shader.name}'; array slots bound: " +
            $"{(bound.Count == 0 ? "none" : string.Join(", ", bound))}.";
        Debug.Log($"[{ToolName}] {state}");
        report.AppendLine(state);

        if (missingProperties.Count > 0)
        {
            return $"shader '{shader.name}' on '{MaterialPath}' does not declare " +
                   $"{string.Join(", ", missingProperties)} (verified against HectonTerrain.shader:10-12, " +
                   "which does declare all three). The material's shader has been swapped, so this tool " +
                   "cannot tell whether the terrain would render.";
        }

        if (unboundProperties.Count > 0)
        {
            return $"'{MaterialPath}' has NULL {string.Join(", ", unboundProperties)}.";
        }

        return null;
    }

    /// <summary>
    /// The path of an open, on-disk scene with unsaved edits, or null. Untitled scenes (empty path) are
    /// ignored on purpose: batchmode starts with one and it has nothing on disk to lose, so treating it as
    /// a hazard would be a false gate that makes this tool unrunnable.
    /// </summary>
    private static string FindDirtySavedScene()
    {
        int openCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        for (int i = 0; i < openCount; i++)
        {
            UnityEngine.SceneManagement.Scene open =
                UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (open.isDirty && !string.IsNullOrEmpty(open.path))
            {
                return open.path;
            }
        }

        return null;
    }

    private static Outcome Refuse(StringBuilder report, string why)
    {
        Debug.LogError($"[{ToolName}] REFUSED: {why}");
        report.AppendLine("REFUSED: " + why);
        WriteVerdict(report);
        return Outcome.Refused;
    }

    private static void WriteVerdict(StringBuilder report)
    {
        string reportPath = Path.Combine(OutputDir, "verdict.txt");
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log($"[{ToolName}] verdict file: {reportPath}");
    }

    /// <summary>
    /// Verdict write for the outer catch, where the output directory itself may be what failed. A failure
    /// to write evidence must not replace the original error in the log.
    /// </summary>
    private static void TryWriteVerdict(StringBuilder report)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            WriteVerdict(report);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{ToolName}] could not write the verdict file under {OutputDir}: {ex}");
        }
    }
}
