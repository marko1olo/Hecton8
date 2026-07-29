using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// VERIFIER. Reports the component composition of every terrain GameObject in the sandbox scene and fails
/// when a terrain cannot render through the custom array path. The component list it prints is a readout
/// for a human; the pass/fail is the non-null tests below.
///
/// WHAT WAS WRONG - this tool had no test in it at all. It was four <c>Debug.Log</c> calls and an
/// unconditional <c>Exit(0)</c>, so every possible state of the scene produced the same success code:
///
/// * THE SUCCESS EXIT SAT OUTSIDE THE GUARD. The entire body was inside <c>if (terrains.Length &gt; 0)</c>
///   and <c>EditorApplication.Exit(0)</c> sat after it. A scene with no terrain logged NOTHING - not even
///   "0 terrains" - and exited 0. Identical shape to the one fixed in <c>CheckAlphamaps.cs:15-18</c>;
/// * THE MATERIAL TEMPLATE WAS FORMATTED AND NEVER TESTED. The old line 23 printed
///   <c>t.materialTemplate ? name : "null"</c> and fell through to <c>Exit(0)</c> either way. A terrain
///   with a null materialTemplate falls back to Unity's built-in terrain material, which does not sample
///   the Texture2DArrays this project's terrain is built on - the exact state
///   <c>CheckSceneMat.cs</c> treats as a failure. Writing the word "null" into a log and then reporting
///   success is the defect this instrument layer exists to stop, same class as the average RGB in
///   <c>CheckAlbedoArray.cs:14-19</c>;
/// * ZERO COMPONENTS WAS A PASS. The <c>foreach</c> over the cache simply did not iterate and the tool
///   exited 0, and the count was never logged, so "I examined nothing" and "everything is fine" were the
///   same output;
/// * <c>Terrain.activeTerrains</c> RETURNS ONLY ENABLED TERRAINS, so a present-but-disabled terrain read
///   as absent. The tool then said nothing about the distinction, which lets a reader conclude the terrain
///   does not exist when all that was established is that it is not enabled;
/// * NULL COMPONENT SLOTS WERE PRINTED AS A CRASH. <c>comp.GetType()</c> on a null entry - which is what
///   <see cref="GameObject.GetComponents(System.Type)"/> yields for a MonoBehaviour whose script
///   reference is broken - threw a NullReferenceException. With no try/catch <c>Execute</c> returned
///   having set NO exit code, and under <c>-quit</c> that ends the process at 0;
/// * there was no try/catch and no output file, so the verdict lived only in a log the next batch run
///   overwrites.
///
/// NO GPU REFUSAL HERE, on purpose, and the reasoning has to hold or the gate is a defect of its own.
/// Nothing in this file renders, blits, reads pixels back, encodes a PNG or dispatches compute. It reads
/// component references, <see cref="Component.GetType"/>, and <c>Terrain.materialTemplate</c> - which is a
/// plain reference read and, unlike <c>Renderer.material</c>, does not clone. All of that is correct with
/// <c>graphicsDeviceType == Null</c>, so a gate here would refuse valid headless runs. Same decision,
/// argued the same way, at <c>CheckSceneMat.cs:70-73</c> and <c>CheckAlbedoArray.cs:36-47</c>.
///
/// SCOPE, stated so the output is not over-read: this checks that a terrain exists, that its component set
/// is readable and unbroken, and that SOMETHING is bound to materialTemplate. It deliberately does not
/// re-verify the material's array bindings or keywords - that is <c>CheckSceneMat</c>'s job and
/// duplicating it here would mean two tools drifting apart on the same invariant. Visual acceptance is
/// <c>Docs/QUALITY_GATES.md</c>'s job, never this file's.
/// </summary>
public static class CheckComponents
{
    private const string ToolName = "CheckComponents";

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    /// <summary>
    /// The shader every terrain in this project is supposed to render with, read from its real source:
    /// <c>HectonTerrain.shader:1</c> declares <c>Shader "Hecton8/URP/Terrain_TextureArray"</c>. A mismatch
    /// is a WARNING rather than a failure - the sandbox scene is legitimately used to compare against
    /// stock URP terrain, and hard-failing would refuse that run. The deep material verification belongs
    /// to <c>CheckSceneMat</c>.
    /// </summary>
    private const string ExpectedShaderName = "Hecton8/URP/Terrain_TextureArray";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not
    /// a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_components");

    private static readonly string ReportPath = Path.Combine(OutputDir, "components_report.txt");

    private static readonly List<Component> s_ComponentsCache = new List<Component>();

    public static void Execute()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"{ToolName} report");

        int terrainsExamined = 0;
        int componentsExamined = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            // Was: the return value was discarded, so a scene that failed to load was indistinguishable
            // from one that loaded empty. Under -executeMethod no scene is open unless one is opened
            // explicitly, which is why this tool opens one at all.
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Fail(report,
                    $"OpenScene('{ScenePath}') returned a scene that is not usable " +
                    $"(IsValid={scene.IsValid()}, isLoaded={scene.isLoaded}). No component was examined, " +
                    "so nothing about the terrain composition is verified by this run.");
                return;
            }

            report.AppendLine($"scene: '{scene.name}' ({scene.path})");

            // FindObjectsInactive.Include, matching CheckSceneMat.cs:106-109. Was: Terrain.activeTerrains,
            // which returns ONLY enabled terrains - so a disabled terrain was invisible and "not found"
            // could not be told apart from "present but disabled". Both counts are reported below so the
            // message never claims more than the call can support.
            UnityEngine.Terrain[] terrains =
                UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Include);
            int terrainCount = terrains == null ? 0 : terrains.Length;
            int activeTerrainCount = UnityEngine.Terrain.activeTerrains == null
                ? 0
                : UnityEngine.Terrain.activeTerrains.Length;

            report.AppendLine($"terrains found: {terrainCount} ({activeTerrainCount} active)");
            Debug.Log(
                $"[{ToolName}] scene '{scene.name}': {terrainCount} Terrain object(s), " +
                $"{activeTerrainCount} active.");

            if (terrainCount == 0)
            {
                // Was: the guard skipped the body and Exit(0) ran anyway, logging nothing whatsoever.
                Fail(report,
                    $"found 0 Terrain objects in '{ScenePath}' (inactive included), so no component was " +
                    "examined and no statement about the terrain composition is supported by this run. " +
                    "This project's terrain chunks are produced by MapMagic, so the usual cause is that " +
                    "generation never ran in this session rather than a missing scene.");
                return;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                UnityEngine.Terrain terrain = terrains[i];
                if (terrain == null)
                    continue;

                terrainsExamined++;

                bool enabledInScene = terrain.enabled && terrain.gameObject.activeInHierarchy;
                report.AppendLine(
                    $"terrain '{terrain.name}' (scene '{terrain.gameObject.scene.name}', " +
                    $"enabled={enabledInScene}):");

                // GetComponents(List<T>) clears the list first, so reusing the cache across terrains is
                // safe. Was: the same cache, but the result count was never looked at.
                terrain.GetComponents(s_ComponentsCache);
                int componentCount = s_ComponentsCache.Count;
                report.AppendLine($"  components: {componentCount}");

                if (componentCount == 0)
                {
                    // Was: the foreach did not iterate and the tool exited 0. A GameObject carrying a
                    // Terrain always has at least a Transform and that Terrain, so zero here means the
                    // object was destroyed mid-run or the call failed - never a healthy terrain.
                    Fail(report,
                        $"terrain '{terrain.name}' reported 0 components. A GameObject with a Terrain on " +
                        "it always has at least a Transform and the Terrain itself, so reading zero " +
                        "means the object is not in the state it claims and nothing was verified.");
                    return;
                }

                Hecton8.World.HectonTerrainMaterialInjector injector = null;
                for (int c = 0; c < componentCount; c++)
                {
                    Component comp = s_ComponentsCache[c];
                    if (comp == null)
                    {
                        // Was: comp.GetType() threw a NullReferenceException on this entry, which set no
                        // exit code at all. A null slot is Unity's representation of a MonoBehaviour whose
                        // script reference is missing - an invisible defect that survives every visual
                        // check.
                        Fail(report,
                            $"terrain '{terrain.name}' has a null component at slot {c} of " +
                            $"{componentCount}. That is a MonoBehaviour with a broken or missing script " +
                            "reference on a shipped terrain object. This is the entry that used to throw " +
                            "here.");
                        return;
                    }

                    componentsExamined++;

                    if (injector == null && comp is Hecton8.World.HectonTerrainMaterialInjector found)
                        injector = found;

                    // The per-component readout the old tool printed. Kept, but only for the first
                    // terrain: MapMagic produces many chunks and dumping every component of every chunk
                    // buries the verdict in the one log anyone reads.
                    if (terrainsExamined == 1)
                    {
                        report.AppendLine($"  - {comp.GetType().Name}");
                        Debug.Log($"[{ToolName}] '{terrain.name}' component: {comp.GetType().Name}");
                    }
                }

                // THE CHECK the old tool formatted into a string and threw away.
                Material materialTemplate = terrain.materialTemplate;
                if (materialTemplate == null)
                {
                    Fail(report,
                        $"terrain '{terrain.name}' has a null materialTemplate, so it renders with " +
                        "Unity's built-in terrain material, which never samples this project's " +
                        "Texture2DArrays. The old tool printed the word \"null\" here and exited 0. " +
                        "HectonTerrainMaterialInjector.ApplyMaterial returns early when its " +
                        "customTerrainMaterial is unset (HectonTerrainMaterialInjector.cs:128-129) and " +
                        "ReleaseInstance nulls the template on disable (same file, 187-188), so an inert " +
                        "or disabled injector produces exactly this state.");
                    return;
                }

                Shader shader = materialTemplate.shader;
                string shaderName = shader == null ? "<null shader>" : shader.name;
                report.AppendLine($"  materialTemplate: '{materialTemplate.name}' shader '{shaderName}'");

                if (shader == null)
                {
                    Fail(report,
                        $"terrain '{terrain.name}' materialTemplate '{materialTemplate.name}' has a null " +
                        "shader, so it cannot render at all.");
                    return;
                }

                if (shaderName != ExpectedShaderName)
                {
                    // Warning, not failure: see ExpectedShaderName. Named loudly because it is the
                    // difference between the array path and stock URP terrain.
                    Debug.LogWarning(
                        $"[{ToolName}] terrain '{terrain.name}' materialTemplate " +
                        $"'{materialTemplate.name}' uses shader '{shaderName}', not " +
                        $"'{ExpectedShaderName}' (HectonTerrain.shader:1). The terrain is bound to a " +
                        "material, so this is not a failure here, but it is not rendering through the " +
                        "Texture2DArray path either. CheckSceneMat owns the material verification.");
                    report.AppendLine(
                        $"  WARNING: shader '{shaderName}' != expected '{ExpectedShaderName}'.");
                }

                if (injector == null)
                {
                    Debug.LogWarning(
                        $"[{ToolName}] terrain '{terrain.name}' has no " +
                        "HectonTerrainMaterialInjector. Nothing on this object pushes the terrain arrays " +
                        "or the alphamap _Control bindings into the material, so whatever is bound now " +
                        "will not be refreshed when the terrain regenerates.");
                    report.AppendLine("  WARNING: no HectonTerrainMaterialInjector on this terrain.");
                }
                else
                {
                    if (injector.customTerrainMaterial == null)
                    {
                        // The injector is present but inert: ApplyMaterial and RefreshTerrainBindings both
                        // return immediately with a null customTerrainMaterial
                        // (HectonTerrainMaterialInjector.cs:128-129 and 57-58). Reported, not changed -
                        // this is scene data.
                        Debug.LogWarning(
                            $"[{ToolName}] terrain '{terrain.name}' has a " +
                            "HectonTerrainMaterialInjector whose customTerrainMaterial is unset, so the " +
                            "component is inert: ApplyMaterial returns before assigning anything " +
                            "(HectonTerrainMaterialInjector.cs:128-129) and RefreshTerrainBindings " +
                            "returns before copying the arrays (same file, 57-58). Reported, not " +
                            "changed; this is scene data.");
                        report.AppendLine(
                            "  WARNING: HectonTerrainMaterialInjector.customTerrainMaterial is unset " +
                            "(component is inert).");
                    }
                }
            }

            if (terrainsExamined == 0)
            {
                Fail(report,
                    $"all {terrainCount} Terrain reference(s) in '{ScenePath}' were null, so no component " +
                    "was examined.");
                return;
            }

            report.AppendLine(
                $"RESULT: PASS - {terrainsExamined} terrain(s), {componentsExamined} component(s) " +
                "examined; every terrain has an unbroken component set and a bound materialTemplate.");
            WriteReport(report);
            Debug.Log(
                $"[{ToolName}] PASS: examined {terrainsExamined} terrain(s) and {componentsExamined} " +
                "component(s); none had a null component slot and every terrain has a materialTemplate " +
                "bound. This does not verify the material's array bindings or keywords - that is " +
                $"CheckSceneMat. Report at {ReportPath}");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all, so OpenScene throwing on a moved scene, or the GetType() call on a
            // null component slot, left Execute with NO exit code - which under -quit ends at 0.
            Fail(report,
                $"threw after examining {terrainsExamined} terrain(s) and {componentsExamined} " +
                $"component(s); the terrain composition is NOT verified. {ex}");
        }
    }

    /// <summary>Writes the error into the report as well as the log, then exits 2.</summary>
    private static void Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        WriteReport(report);
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        EditorApplication.Exit(2);
    }

    /// <summary>
    /// A report-write failure must not replace the real verdict, so this swallows its own IO error and
    /// says so in the log rather than throwing out of a failure path.
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
