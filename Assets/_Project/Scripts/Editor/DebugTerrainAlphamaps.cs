using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// DIAGNOSTIC AND MUTATOR for the terrain splatmap binding. It opens 020_RENDER_SANDBOX, reports the first
/// terrain's alphamap textures and what its materialTemplate currently has bound to <c>_Control</c> /
/// <c>_Control1</c>, then force-assigns those two properties from the terrain's own alphamap textures and
/// flushes the material to disk. Sibling of <c>ForceSplatmaps</c>, which pushes the same two textures into a
/// per-terrain <see cref="MaterialPropertyBlock"/> instead of into the shared material asset.
///
/// WHAT EXIT 0 MEANS NOW. A terrain was found, it has at least one alphamap texture, its materialTemplate is
/// a real asset on disk, the material declares every property this tool writes, the textures being bound are
/// themselves persisted assets (so the reference can actually serialize), the SetTexture calls were read
/// back and match, and the material is provably clean on disk afterwards. It does NOT mean the terrain
/// renders correctly - nothing here draws a pixel.
///
/// WHAT WAS WRONG:
///
///   * THE HEADLINE, the exact shape fixed in <c>CheckAlphamaps.cs:15-18</c> and
///     <c>DumpSplatmaps.cs:13-16</c>: the entire body sat inside <c>if (terrains.Length &gt; 0)</c> while
///     <c>EditorApplication.Exit(0)</c> sat OUTSIDE it. A scene with no terrain logged nothing at all and
///     reported success;
///   * A FALSE SUCCESS CLAIM ON AN EMPTY ASSIGNMENT. With zero alphamap textures both
///     <c>if (Length &gt; 0)</c> / <c>if (Length &gt; 1)</c> guards were false, so no SetTexture ran - and
///     the tool then logged "Forced assigned alphamaps to _Control and _Control1", saved, and exited 0.
///     With exactly one texture, <c>_Control1</c> was never written and the same sentence still claimed
///     both. The message claimed more than the calls could support;
///   * <c>AssetDatabase.SaveAssets()</c> flushes EVERY dirty asset in the project and would commit a
///     concurrent session's unfinished authoring in this shared working tree. Now
///     <c>SaveAssetIfDirty</c> on the one material, proven with <c>EditorUtility.IsDirty</c> and an
///     mtime comparison - the same idiom as <c>TerrainShaderVerify.cs:279-290</c> and
///     <c>DisableErosionNodeTask.cs:234-259</c>;
///   * THE WRITE WAS NEVER VERIFIED. SetTexture on a property the shader does not declare is silently
///     dropped, and a reference from a project asset to a NON-persisted object (which is what a
///     procedurally generated TerrainData's alphamap textures are) serializes as <c>fileID: 0</c>. Either
///     way the save "succeeds" and the material on disk stays unbound. Both are now preconditions, checked
///     before anything is dirtied, and the assignment is read back afterwards;
///   * <c>t.terrainData</c> and <c>t.terrainData.alphamapTextures</c> were dereferenced with no null check
///     and there was no try/catch, so a NullReferenceException left <c>Execute</c> with NO exit code at all
///     - which under <c>-quit</c> ends the process at 0;
///   * "No material template on terrain!" was a <c>Debug.Log</c>, not a LogError, and fell through to the
///     same <c>Exit(0)</c>. A terrain with no material cannot be textured, and an ordinary Log is invisible
///     in a scan of a batchmode log for errors;
///   * <c>mat.GetTexture("_Control")</c> had no <c>HasProperty</c> guard while the very next line did have
///     one for <c>_Control1</c>. GetTexture on an undeclared property returns null, so a material whose
///     shader simply does not declare <c>_Control</c> was reported as "_Control: null" - indistinguishable
///     from a declared-but-unbound property. That is the defect that made CheckMat and CheckSceneMat report
///     healthy terrain as null on every run;
///   * <c>EditorSceneManager.OpenScene</c> was unconditional, discarding a concurrent authoring session's
///     unsaved edits, and its result was never checked;
///   * it logged under the tag <c>[FAS]</c>, which is FixAndShoot's (see <c>DumpSplatmaps.cs:22-23</c>), and
///     wrote no report file anywhere.
///
/// PROPERTY NAMES: VERIFIED, NOT ASSUMED. <c>_Control</c> and <c>_Control1</c> are really declared -
/// <c>HectonTerrain.shader:14-15</c> (and sampled in <c>HectonTerrainSampling.hlsl:6,423-424</c>), and both
/// are present in <c>HectonTerrainMaterial.mat</c>, which uses that shader. Unlike CheckMat/CheckSceneMat,
/// this tool's strings were correct; the <c>HasProperty</c> guards are there because materialTemplate can be
/// any material, not because these two names are wrong.
///
/// SHIPPED ART: the write is PRESERVED, not removed, and is now announced in the log BEFORE it happens
/// rather than as a success line afterwards - same treatment as <c>TerrainShaderVerify.cs:209-214</c>. Two
/// things about it are reported and deliberately NOT changed: (1) the shader declares
/// <c>_Control.._Control7</c> (32 layers) and this tool binds only the first two, so a terrain with more
/// than 8 layers keeps sampling nothing above layer 7; (2) materialTemplate is normally ONE shared asset
/// across all terrain chunks, so writing chunk[0]'s alphamaps into it points every other chunk at chunk[0]'s
/// splatmap. That is exactly what <c>ForceSplatmaps</c>'s per-terrain
/// <c>Terrain.SetSplatMaterialPropertyBlock</c> exists to avoid. Both are warned about, loudly, and left to
/// the owner.
///
/// NO GPU REFUSAL HERE, decided per file: this tool does not blit, does not read back a texture, encodes no
/// PNG, renders no camera and dispatches no compute. Everything it reads is CPU-side descriptor data
/// (<c>Texture2D.width/height/format</c>) and reference plumbing (Material.GetTexture/SetTexture). More to
/// the point, its only exit-0 path requires the alphamap textures to be persisted assets, which are loaded
/// from disk regardless of graphics device - so a device gate here would refuse the only runs that can
/// legitimately pass, and a false gate is its own defect. Its sibling <c>AnalyzeSplatmaps</c> DOES carry the
/// gate, because it tests generated weight VALUES rather than texture identity.
/// </summary>
public static class DebugTerrainAlphamaps
{
    private const string ToolName = "DebugTerrainAlphamaps";

    /// <summary>
    /// Per-tool subfolder inside the repo. The old tool wrote no file at all. <c>static readonly</c> rather
    /// than <c>const</c> because <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "debug_terrain_alphamaps");

    private static readonly string ReportPath = Path.Combine(OutputDir, "alphamap_binding.txt");

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    /// <summary>
    /// The two properties this tool writes, in alphamap-texture order. Verified against
    /// HectonTerrain.shader:14-15. Not extended to _Control2.._Control7: that would be a new write to
    /// shipped art, so the shortfall is reported instead.
    /// </summary>
    private static readonly string[] ControlProperties = { "_Control", "_Control1" };

    /// <summary>Proved the binding was written, read back, and flushed to disk.</summary>
    private const int ExitVerified = 0;

    /// <summary>Could not do the work, or crashed trying. Nothing is claimed about the binding.</summary>
    private const int ExitFailed = 2;

    /// <summary>
    /// Batch entry point. Called by reflection name from <c>Tools/BatchTasks</c> - do not rename.
    /// </summary>
    public static void Execute()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(ToolName);
        report.AppendLine($"Scene: {ScenePath}");

        int exitCode;
        try
        {
            exitCode = Run(report);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all. A throw between OpenScene and the save left no exit code set, and
            // the material may be dirty in memory with a half-written binding - which a concurrent
            // authoring session could then save. Say that in the Unity log; it is the only channel anyone
            // reads out of a batch run.
            report.AppendLine($"RESULT: FAILED - threw mid-run. {ex}");
            Debug.LogError(
                $"[{ToolName}] FAILED mid-run: no verified alphamap binding was produced. If the throw " +
                $"landed after SetTexture, the terrain material is dirty in memory with an unverified " +
                $"_Control binding - reload it rather than saving from anywhere else. {ex}");
            exitCode = ExitFailed;
        }

        WriteReport(report);
        EditorApplication.Exit(exitCode);
    }

    private static int Run(StringBuilder report)
    {
        Directory.CreateDirectory(OutputDir);

        if (!TryOpenSceneWithoutDiscardingWork(ScenePath))
        {
            report.AppendLine("RESULT: FAILED - refused to open the scene; nothing was inspected or bound.");
            return ExitFailed;
        }

        // Signature left exactly as it was: this overload is already proven to compile in this assembly
        // (DumpSplatmaps.cs:72), and the cheap lock-free compile gate emits phantom CS0433/CS0656 on
        // Hecton8.Editor.
        Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>();

        // Was never logged. The old tool could not distinguish "no terrain" from "healthy terrain".
        report.AppendLine($"Terrains found: {terrains.Length}");
        Debug.Log($"[{ToolName}] found {terrains.Length} Terrain(s) in '{ScenePath}'.");

        if (terrains.Length == 0)
        {
            // WAS THE HEADLINE ISSUE: the whole body was inside `if (terrains.Length > 0)` and Exit(0) sat
            // outside it, so this path logged nothing and reported success. FindObjectsByType skips
            // inactive objects, so this cannot claim the scene has no terrain - only that no ACTIVE one was
            // returned.
            return Fail(report,
                $"'{ScenePath}' returned no active Terrain from FindObjectsByType, so no alphamap was " +
                "inspected and nothing was bound to any material. Inactive terrains are not returned by " +
                "this call, so the terrains may exist but be disabled. If the scene is right, generation " +
                "has not run or has not settled (hecton8-shaders-compute.md:36-39 requires Terrain " +
                "length == 9 and alphamaps loaded before any measurement).");
        }

        Terrain t = terrains[0];
        report.AppendLine($"Inspecting first terrain: '{t.name}' at {t.transform.position} (of {terrains.Length})");

        TerrainData td = t.terrainData;
        if (td == null)
        {
            // Was dereferenced unguarded on the next line.
            return Fail(report,
                $"terrain '{t.name}' has no TerrainData, so it has no alphamap textures and there is " +
                "nothing to bind.");
        }

        Texture2D[] alphamaps = td.alphamapTextures;
        int textureCount = alphamaps == null ? 0 : alphamaps.Length;

        report.AppendLine($"alphamapResolution: {td.alphamapResolution}");
        report.AppendLine($"alphamapLayers: {td.alphamapLayers}");
        report.AppendLine($"alphamapTextures: {textureCount}");
        Debug.Log(
            $"[{ToolName}] '{t.name}' alphamapResolution={td.alphamapResolution} " +
            $"alphamapLayers={td.alphamapLayers} alphamapTextures={textureCount}");

        if (textureCount == 0)
        {
            // Was: logged "Terrain has 0 alphamaps.", skipped both SetTexture guards, and then logged
            // "Forced assigned alphamaps to _Control and _Control1" before exiting 0.
            return Fail(report,
                $"terrain '{t.name}' exposes ZERO alphamap textures while reporting " +
                $"{td.alphamapLayers} layer(s) at resolution {td.alphamapResolution}, so there is nothing " +
                "to bind. This path used to claim it had assigned _Control and _Control1 and exit 0.");
        }

        for (int i = 0; i < textureCount; i++)
        {
            Texture2D tex = alphamaps[i];
            if (tex == null)
            {
                return Fail(report,
                    $"alphamap texture {i} of '{t.name}' is null, so the binding would be incomplete. The " +
                    "old loop dereferenced it for .width and threw with no exit code set.");
            }

            report.AppendLine($"  alphamap {i}: {tex.width}x{tex.height} format={tex.format} name='{tex.name}'");
            Debug.Log($"[{ToolName}] alphamap {i}: {tex.width}x{tex.height} format={tex.format}");
        }

        Material mat = t.materialTemplate;
        if (mat == null)
        {
            // Was a plain Debug.Log that fell through to Exit(0).
            return Fail(report,
                $"terrain '{t.name}' has no materialTemplate, so there is no material to bind the " +
                $"{textureCount} alphamap texture(s) to and the terrain cannot be textured at all. This " +
                "path used to be an ordinary Debug.Log followed by exit 0.");
        }

        string matPath = AssetDatabase.GetAssetPath(mat);
        report.AppendLine(
            $"materialTemplate: '{mat.name}' shader='{(mat.shader != null ? mat.shader.name : "NULL")}' " +
            $"assetPath='{matPath}'");

        // The diagnostic half of the tool: report what is bound NOW, distinguishing "the shader does not
        // declare this property" from "declared but unbound". The old code only made that distinction for
        // _Control1, and reported "_Control: null" for both cases.
        for (int i = 0; i < ControlProperties.Length; i++)
        {
            string prop = ControlProperties[i];
            string state = !mat.HasProperty(prop)
                ? "NO SUCH PROPERTY on this shader (GetTexture would return null either way)"
                : (mat.GetTexture(prop) != null ? $"bound to '{mat.GetTexture(prop).name}'" : "declared but UNBOUND (null)");
            report.AppendLine($"  before: {prop} -> {state}");
            Debug.Log($"[{ToolName}] before: {prop} -> {state}");
        }

        if (mat.shader == null)
        {
            return Fail(report,
                $"materialTemplate '{mat.name}' on '{t.name}' has a null shader, so no property it declares " +
                "can be trusted and nothing can be bound.");
        }

        // ---- preconditions for the write. All checked BEFORE anything is dirtied, so a refusal never
        // leaves a shipped material dirty with a value a concurrent session might save. ----

        // AssetDatabase.Contains rather than LoadAssetAtPath(matPath) == mat: a material can legitimately be
        // a SUB-asset, where LoadAssetAtPath returns the main asset and the comparison would refuse a
        // perfectly writable material.
        if (string.IsNullOrEmpty(matPath) || !AssetDatabase.Contains(mat))
        {
            return Fail(report,
                $"materialTemplate '{mat.name}' on '{t.name}' is not a project asset " +
                $"(GetAssetPath returned '{matPath}'), so a SetTexture on it would live only in this " +
                "editor session - and this tool exits the editor immediately afterwards, so nothing would " +
                "ever observe it. Nothing was bound. Use ForceSplatmaps' per-terrain " +
                "SetSplatMaterialPropertyBlock for a scene-local binding.");
        }

        int assignable = Mathf.Min(textureCount, ControlProperties.Length);
        for (int i = 0; i < assignable; i++)
        {
            string prop = ControlProperties[i];

            // SetTexture for a property the shader does not declare is SILENTLY dropped. Without this the
            // save "succeeds" against a material this tool never actually configured - the failure that
            // made TerrainShaderVerify verify an unbound material (TerrainShaderVerify.cs:33-34).
            if (!mat.HasProperty(prop))
            {
                return Fail(report,
                    $"material '{matPath}' with shader '{mat.shader.name}' does not declare {prop}, so " +
                    "SetTexture would be silently dropped and nothing would be bound. Nothing was written. " +
                    "HectonTerrain.shader:14-21 declares _Control.._Control7; this material uses a " +
                    "different shader.");
            }

            // A reference from a PROJECT ASSET to a non-persisted object cannot serialize: Unity writes
            // fileID: 0 and the save reports success. Procedurally generated TerrainData (MapMagic) has
            // exactly this property, and HectonTerrainMaterial.mat currently holds
            // `_Control: {m_Texture: {fileID: 0}}` on disk, which is what such a write leaves behind.
            string texPath = AssetDatabase.GetAssetPath(alphamaps[i]);
            if (string.IsNullOrEmpty(texPath))
            {
                return Fail(report,
                    $"alphamap texture {i} of '{t.name}' is not a persisted asset " +
                    $"(GetAssetPath is empty - the TerrainData is generated or scene-embedded), while " +
                    $"'{matPath}' IS a project asset. Unity cannot serialize a project-asset reference to " +
                    $"a non-persisted object: it would write fileID: 0 for {prop} and the save would still " +
                    "report success. Nothing was written, because this write cannot reach disk. Bind it " +
                    "per-terrain instead (ForceSplatmaps' SetSplatMaterialPropertyBlock), or pin the " +
                    "TerrainData as an asset first.");
            }

            report.AppendLine($"  plan: {prop} <- alphamap {i} '{texPath}'");
        }

        // Announced BEFORE the write, not as a success line afterwards. This is a shipped asset.
        Debug.LogWarning(
            $"[{ToolName}] MUTATES SHIPPED ART: about to overwrite {assignable} control texture " +
            $"binding(s) on '{matPath}' from terrain '{t.name}'. This is a persistent edit to a versioned " +
            "material asset, not a scene-local diagnostic.");

        int sharers = 0;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i].materialTemplate == mat) sharers++;
        }
        if (sharers > 1)
        {
            // Reported, not blocked, and not "fixed" - fixing it means per-terrain binding, which is a
            // different tool (ForceSplatmaps). Silence here is what would be indefensible.
            Debug.LogWarning(
                $"[{ToolName}] {sharers} of {terrains.Length} terrain(s) in '{ScenePath}' share " +
                $"materialTemplate '{matPath}'. Writing '{t.name}'s alphamap textures into that ONE shared " +
                $"asset points all {sharers} chunks at this chunk's splatmap. Per-chunk control textures " +
                "cannot be expressed in a shared material - that is what " +
                "Terrain.SetSplatMaterialPropertyBlock is for (ForceSplatmaps.cs:10-14).");
            report.AppendLine(
                $"WARNING: materialTemplate is shared by {sharers} terrain(s); this binding is per-material, " +
                "not per-chunk.");
        }

        if (textureCount > ControlProperties.Length)
        {
            Debug.LogWarning(
                $"[{ToolName}] '{t.name}' has {textureCount} alphamap texture(s) but this tool only binds " +
                $"{ControlProperties.Length} ({string.Join(", ", ControlProperties)}). Layers " +
                $"{ControlProperties.Length * 4} and above stay unbound even after this run. " +
                "HectonTerrain.shader:14-21 declares _Control.._Control7; extending the write is an owner " +
                "decision, not this cleanup's.");
            report.AppendLine(
                $"WARNING: {textureCount} alphamap texture(s) present, only {ControlProperties.Length} bound.");
        }

        // ---- the write ----

        string absoluteMatPath = Path.Combine(Directory.GetCurrentDirectory(), matPath);
        System.DateTime mtimeBefore = File.Exists(absoluteMatPath)
            ? File.GetLastWriteTimeUtc(absoluteMatPath)
            : System.DateTime.MinValue;

        bool bindingChanged = false;
        for (int i = 0; i < assignable; i++)
        {
            if (mat.GetTexture(ControlProperties[i]) != alphamaps[i]) bindingChanged = true;
            mat.SetTexture(ControlProperties[i], alphamaps[i]);
        }

        EditorUtility.SetDirty(mat);

        // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
        // including a concurrent session's in-flight authoring in this shared working tree.
        AssetDatabase.SaveAssetIfDirty(mat);

        if (EditorUtility.IsDirty(mat))
        {
            return Fail(report,
                $"'{matPath}' is STILL DIRTY after AssetDatabase.SaveAssetIfDirty, so the {assignable} " +
                "control binding(s) exist in memory only and nothing reached disk. The asset is probably " +
                "read-only or locked by the concurrent session. The in-memory material no longer matches " +
                "the file - reload it rather than saving from anywhere else.");
        }

        // Read back what was actually bound. "SetTexture did not throw" is not proof of a binding.
        // `assignedProperties` names ONLY the properties this run actually wrote: with a single alphamap
        // texture the old tool still claimed it had assigned both _Control and _Control1.
        StringBuilder assignedProperties = new StringBuilder();
        for (int i = 0; i < assignable; i++)
        {
            Texture bound = mat.GetTexture(ControlProperties[i]);
            if (bound != alphamaps[i])
            {
                return Fail(report,
                    $"{ControlProperties[i]} reads back as " +
                    $"'{(bound != null ? bound.name : "null")}' after being set to " +
                    $"'{alphamaps[i].name}', so the assignment did not take. Treat the material as " +
                    "changed-but-unverified.");
            }
            if (assignedProperties.Length > 0) assignedProperties.Append(", ");
            assignedProperties.Append(ControlProperties[i]);
            report.AppendLine($"  after: {ControlProperties[i]} -> bound to '{bound.name}' (verified)");
        }

        System.DateTime mtimeAfter = File.Exists(absoluteMatPath)
            ? File.GetLastWriteTimeUtc(absoluteMatPath)
            : System.DateTime.MinValue;
        bool fileRewritten = mtimeAfter != mtimeBefore;

        if (bindingChanged && !fileRewritten)
        {
            // Only a failure when the binding actually changed. A re-run against an already-correct
            // material legitimately leaves the bytes alone, and failing on that would be a false alarm.
            return Fail(report,
                $"the {assignable} control binding(s) changed in memory but '{absoluteMatPath}' was not " +
                $"rewritten (mtime {mtimeBefore:O} unchanged) and IsDirty is already false. The change " +
                "cannot be proven on disk; treat the material state as unknown and re-run.");
        }

        if (!bindingChanged)
        {
            Debug.Log(
                $"[{ToolName}] the {assignable} control binding(s) on '{matPath}' were ALREADY correct " +
                "before this run; nothing had to change.");
            report.AppendLine("NOTE: bindings were already correct before this run.");
        }

        report.AppendLine(
            $"RESULT: PASS - {assignable} control binding(s) ({assignedProperties}) verified on '{matPath}' " +
            $"from terrain '{t.name}' (mtime {mtimeBefore:O} -> {mtimeAfter:O}).");
        Debug.Log(
            $"[{ToolName}] VERIFIED: bound {assignable} of {textureCount} alphamap texture(s) from " +
            $"'{t.name}' to {assignedProperties} on '{matPath}', read back and proven clean on disk. Only " +
            $"the properties named here were written. This is evidence that the binding exists, NOT that " +
            $"the terrain renders - nothing here draws a pixel. Report at {ReportPath}");
        return ExitVerified;
    }

    /// <summary>
    /// Opens the scene only when nothing would be lost, mirroring <c>DumpSplatmaps.cs:170-186</c>. In a
    /// shared working tree an unconditional OpenScene silently destroys another lane's unsaved edits - and
    /// this tool goes on to write a versioned asset, which makes it the more dangerous of the pair.
    /// </summary>
    private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isDirty)
                continue;

            Debug.LogError(
                $"[{ToolName}] REFUSED to open '{scenePath}': scene '{scene.name}' has unsaved changes and " +
                "opening would discard them. No alphamap was inspected and nothing was bound.");
            return false;
        }

        UnityEngine.SceneManagement.Scene opened =
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!opened.IsValid() || !opened.isLoaded)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: '{scenePath}' did not open (IsValid={opened.IsValid()}, " +
                $"isLoaded={opened.isLoaded}). No alphamap was inspected and nothing was bound.");
            return false;
        }

        return true;
    }

    /// <summary>Writes the failure into the report as well as the log, then returns the failure code.</summary>
    private static int Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        return ExitFailed;
    }

    /// <summary>
    /// A report-write failure must not replace the real verdict, so this swallows its own IO error and says
    /// so in the log rather than throwing out of a failure path.
    /// </summary>
    private static void WriteReport(StringBuilder report)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[{ToolName}] could not write {ReportPath}: {ex.Message}");
        }
    }
}
