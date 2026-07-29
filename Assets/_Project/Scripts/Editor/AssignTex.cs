using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MUTATOR of shipped authored content. Binds the three terrain <see cref="Texture2DArray"/> assets into
/// <c>HectonTerrainMaterial</c>, the material the world's terrain renders with, so every outcome now speaks
/// in the batchmode log AND in the exit code: CHANGED (0), ALREADY CORRECT (5), REFUSED/FAILED (2).
///
/// What it used to do wrong - all of which produced a success report:
///   - <b>every failure exited 0.</b> "Mat not found!", "AlbedoArray not found!", "NormalArray not found!"
///     and "MaskArray not found!" were all <c>Debug.Log</c>, not LogError, and all four fell through to
///     <c>EditorApplication.Exit(0)</c>. A stale asset path made the tool do nothing, or half its job, and
///     report success - in a log where an info line is invisible.
///   - <b>it half-applied.</b> The three assignments were independent <c>if</c> blocks with the SetDirty
///     and the save after them, so a missing mask array left albedo and normal written to the shipped
///     material and the material saved in that state. It now validates all three FIRST and writes nothing
///     at all unless every one of them is resolvable.
///   - <b>it never read back what it set.</b> "Set _AlbedoArray" was logged from the fact that the C# call
///     returned, not from the material. <see cref="Material.SetTexture(string,Texture)"/> on a property the
///     current shader does not declare is a silent no-op, which is exactly how two sibling tools in this
///     family reported healthy terrain as null for years. Every write is now verified by reading the
///     property back off the material.
///   - <b><c>AssetDatabase.SaveAssets()</c></b> flushes EVERY dirty asset in the project. This is a working
///     tree shared with a concurrent authoring session; that commits their unfinished work. Now
///     <c>SaveAssetIfDirty</c> on the one material.
///   - <b>a no-op re-run still dirtied and rewrote the shared material.</b> SetDirty + SaveAssets ran
///     unconditionally, even though at audit time all three arrays were already bound to exactly these
///     assets (HectonTerrainMaterial.mat:29-30, 81-82, 101-102 hold the GUIDs of Terrain_AlbedoArray,
///     Terrain_MaskArray and Terrain_NormalArray). The whole tool was already a no-op that rewrote a
///     shipped asset every run. It now writes only what differs, and nothing at all if nothing differs.
///   - <b>no exception guard and no proof of the write.</b> An exception escaped <c>-executeMethod</c> and
///     the process never reached any Exit(), hanging a launcher without <c>-quit</c>; and a save refused
///     by a read-only file looked identical to a successful one. Both are now covered, the second by
///     <c>EditorUtility.IsDirty</c> plus the material file's mtime before/after.
///   - it wrote no artifact. A per-tool <c>Logs/assign_tex/</c> file now survives the run.
///
/// The three property names were verified against the real shader, not assumed: HectonTerrain.shader
/// declares <c>_AlbedoArray</c>/<c>_NormalArray</c>/<c>_MaskArray</c> at lines 10-12, its GUID
/// 3395ccfb18535a34fa152b5ea83a1a89 is the <c>m_Shader</c> of HectonTerrainMaterial.mat:11, and
/// Hecton8.World.HectonTerrainMaterialInjector.cs:67-72 sets the same three names at runtime. They are the
/// correct names - unlike <c>_TerrainBaseMapArray</c>/<c>_TerrainNormalMapArray</c>/<c>_TerrainMaskMapArray</c>,
/// which two sibling tools probe and which exist nowhere in this repo. <see cref="Material.HasProperty(string)"/>
/// still guards each one so that a future shader swap is a loud refusal instead of a silent no-op.
///
/// NO GPU REFUSAL BLOCK HERE, on purpose, decided from the code: this tool sets serialized object
/// references on a material asset and reads the arrays' imported metadata (name/width/height/depth). It
/// never renders, blits, reads back pixels, encodes a PNG or dispatches compute - there is no value in it
/// that degrades to zeros without a graphics device, and the dimensions come from the imported asset
/// header, not from a device query. A <c>GraphicsDeviceType.Null</c> gate here would only make the tool
/// permanently unrunnable under a <c>-nographics</c> launcher, which is a false gate blocking correct runs.
/// A tool that renders the result to prove the binding took effect would need the refusal + Exit(3).
///
/// Note ReassignTextures.cs is a near-duplicate of this tool with the same original defects plus an
/// unguarded null dereference; it is owned by another audit lane and is only reported here, not touched.
/// </summary>
public static class AssignTex
{
    private const string ToolName = "AssignTex";

    private const string MaterialPath =
        "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";

    /// <summary>
    /// Shader property name paired with the asset it must hold. Property names verified against
    /// HectonTerrain.shader:10-12; asset paths verified present on disk at audit time. Do not add a row
    /// without opening the shader and confirming the property exists.
    /// </summary>
    private struct ArrayBinding
    {
        public string Property;
        public string AssetPath;

        public ArrayBinding(string property, string assetPath)
        {
            Property = property;
            AssetPath = assetPath;
        }
    }

    private static readonly ArrayBinding[] Bindings =
    {
        new ArrayBinding("_AlbedoArray", "Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset"),
        new ArrayBinding("_NormalArray", "Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset"),
        new ArrayBinding("_MaskArray", "Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset"),
    };

    /// <summary>
    /// Per-tool subfolder, not a shared <c>Logs/</c> root: two tools in this project already wrote
    /// identical filenames into one directory and each run destroyed the other's evidence.
    /// <c>static readonly</c> rather than <c>const</c> because <see cref="Path.Combine(string,string,string)"/>
    /// is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "assign_tex");

    /// <summary>Proved it wrote at least one new array binding to the material file on disk.</summary>
    private const int ExitChanged = 0;

    /// <summary>
    /// Could not do the job, or crashed trying, or refused a precondition. Nothing is claimed about the
    /// material. Refused and Failed deliberately share one code, as in DisableErosionNodeTask: both mean
    /// "do not believe anything about the terrain material's texture arrays after this run".
    /// </summary>
    private const int ExitFailed = 2;

    /// <summary>
    /// All three arrays were already bound to exactly these assets: nothing written, nothing dirtied.
    /// Deliberately NOT 0, because "I rebound the shipped terrain material" and "it was already correct"
    /// are different facts and a caller that only reads the exit code must be able to tell them apart.
    /// Non-zero here does not mean something broke - 2 means that. Outside the reserved 0/2/3/4 taxonomy
    /// on purpose.
    /// </summary>
    private const int ExitAlreadyCorrect = 5;

    private enum Outcome
    {
        Changed,
        AlreadyCorrect,
        Refused,
    }

    /// <summary>
    /// Entry point invoked by reflection as <c>AssignTex.Execute</c> from <c>-executeMethod</c> - DO NOT
    /// RENAME. Always reaches exactly one <see cref="EditorApplication.Exit"/> in batchmode (a launcher
    /// without <c>-quit</c> would otherwise hang), and never calls Exit outside batchmode, because
    /// <c>-executeMethod</c> also works against a GUI editor and quitting a human's session mid-authoring
    /// in this shared tree is worse than losing an exit code nobody is reading. Same convention as
    /// ReassignTextures.cs:22.
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
                $"[{ToolName}] FAILED outside the guarded body, so the texture arrays bound to " +
                $"'{MaterialPath}' are UNVERIFIED and no complete verdict file was written under " +
                $"{OutputDir}. {ex}");
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
        bool saveCalled = false;
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
                    "texture array was assigned. If the material moved or was renamed, this tool's " +
                    "hardcoded path is stale. This branch used to be a Debug.Log followed by exit 0.");
            }

            Shader shader = mat.shader;
            if (shader == null)
            {
                return Refuse(report, $"'{MaterialPath}' has a NULL shader, so no property could be set.");
            }

            string header = $"material '{mat.name}' uses shader '{shader.name}'.";
            Debug.Log($"[{ToolName}] {header}");
            report.AppendLine(header);

            // VALIDATE EVERYTHING BEFORE WRITING ANYTHING. The old tool assigned each array inside its own
            // if-block, so one stale path left the shipped material half-written and still exited 0.
            phase = "resolving the three texture arrays";
            var problems = new List<string>();
            var pendingProperties = new List<string>();
            var pendingTextures = new List<Texture2DArray>();
            int alreadyCorrect = 0;

            foreach (ArrayBinding binding in Bindings)
            {
                if (!mat.HasProperty(binding.Property))
                {
                    problems.Add(
                        $"shader '{shader.name}' does not declare '{binding.Property}' (verified against " +
                        "HectonTerrain.shader:10-12, which does declare it) - SetTexture would have been a " +
                        $"silent no-op and the old tool logged 'Set {binding.Property}' anyway");
                    continue;
                }

                var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(binding.AssetPath);
                if (array == null)
                {
                    problems.Add(
                        $"no Texture2DArray at '{binding.AssetPath}' for '{binding.Property}' (missing, or " +
                        "the asset there is not a Texture2DArray)");
                    continue;
                }

                // Logged AND tested. CheckAlbedoArray in this same family computed an average RGB, logged
                // it, never tested it, and passed on uninitialised 0xCD memory reading as a plausible
                // albedo. Any real array has at least one slice and non-zero dimensions, so this cannot
                // reject a healthy asset.
                string dims =
                    $"{binding.Property} <- '{array.name}' {array.width}x{array.height} depth={array.depth}";
                if (array.width <= 0 || array.height <= 0 || array.depth <= 0)
                {
                    problems.Add(
                        $"'{binding.AssetPath}' is degenerate ({dims}) and would render as nothing; it " +
                        "failed to import or was authored empty");
                    continue;
                }

                report.AppendLine("    " + dims);

                if (mat.GetTexture(binding.Property) == array)
                {
                    alreadyCorrect++;
                    report.AppendLine("        already bound - will not be rewritten");
                    continue;
                }

                pendingProperties.Add(binding.Property);
                pendingTextures.Add(array);
            }

            if (problems.Count > 0)
            {
                return Refuse(
                    report,
                    $"{problems.Count} of {Bindings.Length} terrain texture arrays could not be resolved: " +
                    $"{string.Join(" | ", problems)}. NOTHING was written and the material was not marked " +
                    "dirty - a partial binding on the shipped terrain material renders the world wrong " +
                    "while looking like success, which is what the old tool did here.");
            }

            if (pendingProperties.Count == 0)
            {
                string already =
                    $"ALREADY CORRECT: all {alreadyCorrect} of {Bindings.Length} array(s) on " +
                    $"'{MaterialPath}' already reference exactly the intended assets. Nothing was written, " +
                    $"the material was not marked dirty, no save was issued. Exit {ExitAlreadyCorrect} " +
                    "means idempotent no-op, not failure.";
                Debug.Log($"[{ToolName}] {already}");
                report.AppendLine(already);
                WriteVerdict(report);
                return Outcome.AlreadyCorrect;
            }

            phase = "writing the material asset";
            // Resolved from Application.dataPath (always "<project>/Assets" in the editor), NOT from
            // Directory.GetCurrentDirectory(): a launcher passes -projectPath while the process cwd is
            // wherever it was invoked from, and a wrong cwd here would fail the disk proof on a write that
            // did land.
            string absoluteMaterialPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", MaterialPath.Replace('/', Path.DirectorySeparatorChar)));
            DateTime mtimeBefore = File.Exists(absoluteMaterialPath)
                ? File.GetLastWriteTimeUtc(absoluteMaterialPath)
                : DateTime.MinValue;

            var writtenNames = new List<string>();
            for (int i = 0; i < pendingProperties.Count; i++)
            {
                string property = pendingProperties[i];
                Texture2DArray array = pendingTextures[i];
                Texture previous = mat.GetTexture(property);

                mat.SetTexture(property, array);

                // Read back, do not assume. SetTexture returns void and silently does nothing when the
                // shader lacks the property; HasProperty above should have caught that, but the whole point
                // of this audit is that this tool used to claim "Set X" from the fact that a void call
                // returned.
                if (mat.GetTexture(property) != array)
                {
                    return Refuse(
                        report,
                        $"SetTexture('{property}') did NOT take: reading the property straight back off " +
                        $"'{mat.name}' returned " +
                        $"'{(mat.GetTexture(property) == null ? "<null>" : mat.GetTexture(property).name)}' " +
                        $"instead of '{array.name}'. The material was left dirty in memory and NOT saved, " +
                        "so its texture arrays are now UNVERIFIED - re-run for a clean verdict.");
                }

                writtenNames.Add(
                    $"{property}: '{(previous == null ? "<null>" : previous.name)}' -> '{array.name}'");
            }

            EditorUtility.SetDirty(mat);

            // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
            // including another session's in-flight authoring work in this shared tree.
            AssetDatabase.SaveAssetIfDirty(mat);
            saveCalled = true;

            phase = "verifying the write reached disk";
            bool stillDirty = EditorUtility.IsDirty(mat);
            DateTime mtimeAfter = File.Exists(absoluteMaterialPath)
                ? File.GetLastWriteTimeUtc(absoluteMaterialPath)
                : DateTime.MinValue;
            bool fileRewritten = mtimeAfter != mtimeBefore;

            // Exit 0 has to be earned. Both signals must agree that the asset actually got written; if
            // either disagrees this reports failure rather than guessing, so the error can only ever be a
            // false alarm, never a false success.
            if (stillDirty || !fileRewritten)
            {
                string notPersisted =
                    $"FAILED: {writtenNames.Count} array binding(s) were set IN MEMORY ONLY - the change " +
                    $"was not proven on disk. EditorUtility.IsDirty after SaveAssetIfDirty={stillDirty} " +
                    $"(expected False), '{absoluteMaterialPath}' mtime {mtimeBefore:O} -> {mtimeAfter:O} " +
                    "(expected a change). The asset may be read-only, locked by the concurrent session, or " +
                    "the save was rejected. Treat the terrain material as unknown and re-run.";
                Debug.LogError($"[{ToolName}] {notPersisted}");
                report.AppendLine(notPersisted);
                WriteVerdict(report);
                return Outcome.Refused;
            }

            string changed =
                $"CHANGED: {writtenNames.Count} of {Bindings.Length} array binding(s) rewritten on " +
                $"'{MaterialPath}' ({alreadyCorrect} were already correct and were left alone): " +
                $"{string.Join("; ", writtenNames)}. Written to disk (mtime {mtimeBefore:O} -> " +
                $"{mtimeAfter:O}). This changes what all terrain in the game renders as - re-capture any " +
                "terrain-appearance evidence taken before this run.";
            Debug.Log($"[{ToolName}] {changed}");
            report.AppendLine(changed);
            WriteVerdict(report);
            return Outcome.Changed;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED while {phase}. SaveAssetIfDirty had already been called before the " +
                $"throw: {saveCalled}. If that is False, no texture array was assigned to " +
                $"'{MaterialPath}' and nothing was written. If it is True, the material was flushed but " +
                "this run never finished proving the write, so treat it as changed-but-unverified and " +
                $"re-run for a clean verdict; no complete verdict file exists under {OutputDir}. {ex}");
            report.AppendLine($"FAILED while {phase} (saveCalled={saveCalled}): {ex}");
            TryWriteVerdict(report);
            return Outcome.Refused;
        }
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
