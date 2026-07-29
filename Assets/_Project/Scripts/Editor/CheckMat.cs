using System.Text;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Verifies the terrain material ASSET on disk at <see cref="MaterialPath"/>: that it exists, that it
/// still points at the Hecton terrain shader, and that the texture-array and splat-control slots the
/// shader samples are actually bound.
///
/// THIS IS THE ASSET HALF OF A PAIR. <c>CheckSceneMat.cs</c> is the scene half, and the split is real
/// rather than duplication: <c>HectonTerrainMaterialInjector.cs:133</c> does
/// <c>new Material(customTerrainMaterial)</c> and binds that CLONE to <c>Terrain.materialTemplate</c>
/// (<c>:145</c>), so the material the renderer actually samples is never the asset this file opens. This
/// file therefore cannot see, and deliberately does not judge, anything the injector does to the clone -
/// see the keyword note below.
///
/// WHAT WAS WRONG:
///
/// * THE THREE HEADLINE PROBES NAMED PROPERTIES THAT DO NOT EXIST IN THIS PROJECT. The file asked for
///   <c>_TerrainBaseMapArray</c>, <c>_TerrainNormalMapArray</c> and <c>_TerrainMaskMapArray</c>. The
///   shader declares <c>_AlbedoArray</c>, <c>_NormalArray</c> and <c>_MaskArray</c>
///   (<c>Assets/_Project/Shaders/HectonTerrain.shader:10-12</c>), the material serialises those three
///   names, and the injector sets those three names
///   (<c>HectonTerrainMaterialInjector.cs:67-72</c>). The old names appear nowhere else in the repo
///   except the two sibling verifiers that were copy-pasted from this one. <see cref="Material.GetTexture"/>
///   on an undeclared property returns null, so all three lines printed "null" on EVERY run no matter how
///   healthy the terrain was - a permanent fabricated diagnosis, handed over with exit code 0. Every probe
///   now goes through <see cref="Material.HasProperty"/> first, so a name that the shader does not declare
///   is reported as a BROKEN PROBE and never again as a missing texture;
/// * every value was logged and NOTHING was tested. The file wrote five texture names and a keyword list
///   into a text dump and then exited 0 unconditionally (old <c>:17</c>). A material with all three arrays
///   unbound produced the same exit code as a perfect one;
/// * <c>if (mat == null) return;</c> (old <c>:8</c>) - the "material is missing entirely" branch was the
///   quietest branch in the file. No log, no exit code, and under <c>-quit</c> that ends the process at 0;
/// * it checked <c>_Control1</c> and <c>_Control2</c> but NOT <c>_Control</c>. <c>_Control</c> is
///   alphamap 0, the one that always exists (<c>HectonTerrainMaterialInjector.cs:115</c>);
///   <c>_Control1</c>/<c>_Control2</c> only carry data past 4 splat layers and this material ships
///   <c>_NumLayersCount: 1</c>, so the two slots it printed are expected-empty by design while the one
///   that matters went unread. They are now reported as informational and <c>_Control</c> is required;
/// * output went to <c>C:/hades/Hecton8/terrain_mat_dump2.txt</c> - a hardcoded absolute path, in the repo
///   root, and the "2" is the fossil of an earlier collision. Now a per-tool subfolder under
///   <c>Logs/</c>, which shares no filename with CheckSceneMat's.
/// </summary>
public static class CheckMat
{
    private const string ToolName = "CheckMat";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is
    /// not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_mat");

    private const string MaterialPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";

    /// <summary>From <c>Assets/_Project/Shaders/HectonTerrain.shader:1</c>.</summary>
    private const string ExpectedShaderName = "Hecton8/URP/Terrain_TextureArray";

    /// <summary>
    /// Slots the shader samples that must be bound for the terrain to render as authored. The three
    /// arrays are the real property names; <c>_Control</c> is splat alphamap 0.
    /// </summary>
    private static readonly string[] RequiredArrayProperties =
        { "_AlbedoArray", "_NormalArray", "_MaskArray" };

    /// <summary>
    /// Legitimately empty at 4 or fewer splat layers, so these are REPORTED and never failed on. This
    /// material ships <c>_NumLayersCount: 1</c>.
    /// </summary>
    private static readonly string[] InformationalControlProperties = { "_Control1", "_Control2" };

    public static void Execute()
    {
        // PART 4 DELIBERATELY OMITTED. This tool reads material property references and keyword strings
        // through AssetDatabase - no Blit, no readback, no GetPixels, no EncodeToPNG, no compute
        // dispatch - so it returns identical, correct results with no GPU context. A graphicsDeviceType
        // gate here would refuse runs that are perfectly valid. Its sibling CheckTexArrays.cs DOES need
        // the gate, because it calls GetPixels on a GPU-populated array; do not copy that gate here.

        string reportPath = Path.Combine(OutputDir, "terrain_material_report.txt");
        int propertiesExamined = 0;
        int propertiesBound = 0;
        int failures = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                // Was: a bare `return`, which is exit 0 under -quit.
                Debug.LogError(
                    $"[{ToolName}] FAILED: no material asset at '{MaterialPath}'. Nothing was examined, " +
                    "so this run verifies nothing about the terrain material. Either the asset was moved " +
                    "or renamed, or this path is stale.");
                EditorApplication.Exit(2);
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine($"{ToolName} report");
            report.AppendLine($"material asset: {MaterialPath}");

            Shader shader = mat.shader;
            if (shader == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{MaterialPath}' has no shader assigned. It cannot render, and " +
                    "no texture slot on it means anything.");
                EditorApplication.Exit(2);
                return;
            }

            report.AppendLine($"shader: {shader.name}");

            // A material whose shader failed to compile or went missing silently resolves to Unity's
            // error shader. Reading texture slots off it would produce a report about nothing.
            if (shader.name.Contains("InternalErrorShader"))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{MaterialPath}' resolved to '{shader.name}' - its real shader " +
                    "is missing or failed to compile. Texture slots read off the error shader are " +
                    "meaningless, so no verdict on the terrain material is possible from this run.");
                EditorApplication.Exit(2);
                return;
            }

            if (shader.name != ExpectedShaderName)
            {
                // Not fatal to the read, but it is a failure: the slots below are this shader's, and the
                // terrain will not look as authored under a different one.
                failures++;
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{MaterialPath}' uses shader '{shader.name}', expected " +
                    $"'{ExpectedShaderName}'. The texture-array slots checked below belong to the " +
                    "expected shader; under a different shader they may not be sampled at all.");
            }

            foreach (string property in RequiredArrayProperties)
            {
                propertiesExamined++;

                if (!mat.HasProperty(property))
                {
                    // THE ORIGINAL BUG, now loud: an undeclared name is a broken probe, not a missing
                    // texture. Distinguishing these two is the whole point of the HasProperty call.
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: shader '{shader.name}' does not declare '{property}', so " +
                        "this probe cannot say anything about the material. Fix the property name in this " +
                        "tool, or the shader lost the slot.");
                    report.AppendLine($"{property}: PROBE BROKEN - not declared by shader");
                    continue;
                }

                Texture bound = mat.GetTexture(property);
                if (bound == null)
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{property}' is declared by the shader but nothing is bound " +
                        "to it on the asset. The terrain cannot sample a texture that is not there.");
                    report.AppendLine($"{property}: UNBOUND");
                    continue;
                }

                // The shader declares these three as 2DArray. A plain Texture2D in the slot compiles and
                // binds but samples wrong, and the old code would have printed its name as a success.
                if (!(bound is Texture2DArray array))
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{property}' is bound to '{bound.name}' of type " +
                        $"{bound.GetType().Name}, but the shader declares it as 2DArray. A non-array " +
                        "texture in an array slot does not sample correctly.");
                    report.AppendLine($"{property}: WRONG TYPE {bound.GetType().Name} ({bound.name})");
                    continue;
                }

                if (array.depth < 1)
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{property}' is bound to array '{array.name}' with depth " +
                        $"{array.depth}. An array with no slices textures nothing.");
                    report.AppendLine($"{property}: EMPTY ARRAY {array.name} depth=0");
                    continue;
                }

                propertiesBound++;
                report.AppendLine(
                    $"{property}: OK {array.name} {array.width}x{array.height} depth={array.depth} " +
                    $"format={array.format}");
            }

            // _Control is alphamap 0 - the splatmap that decides which layer shows where. Without it the
            // terrain renders as layer 0 everywhere, which is the single most common "terrain looks wrong"
            // symptom in this project, and the old tool never looked at it.
            propertiesExamined++;
            if (!mat.HasProperty("_Control"))
            {
                failures++;
                Debug.LogError(
                    $"[{ToolName}] FAILED: shader '{shader.name}' does not declare '_Control'. The splat " +
                    "control map probe is broken.");
                report.AppendLine("_Control: PROBE BROKEN - not declared by shader");
            }
            else
            {
                Texture control = mat.GetTexture("_Control");
                if (control == null)
                {
                    // Note this is expected on the ASSET when the injector owns the binding at runtime,
                    // but the asset is also what a human opens and what a fresh terrain inherits, so an
                    // unbound _Control here is still reported as the defect it is.
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '_Control' (splat alphamap 0) is unbound on " +
                        $"'{MaterialPath}'. With no control map the terrain samples layer 0 everywhere " +
                        "regardless of how many layers are bound.");
                    report.AppendLine("_Control: UNBOUND");
                }
                else
                {
                    propertiesBound++;
                    report.AppendLine($"_Control: OK {control.name}");
                }
            }

            // Reported, never failed on: empty past 4 splat layers is correct, and this material ships
            // _NumLayersCount: 1. The old file failed to distinguish these from the required slots.
            foreach (string property in InformationalControlProperties)
            {
                if (!mat.HasProperty(property))
                {
                    report.AppendLine($"{property}: not declared by shader (informational)");
                    continue;
                }

                Texture control = mat.GetTexture(property);
                report.AppendLine(
                    $"{property}: {(control != null ? control.name : "unbound")} (informational - " +
                    "expected empty at 4 or fewer splat layers)");
            }

            // INFORMATIONAL ONLY, and this is a real distinction rather than laziness. The renderer never
            // uses the asset's keyword set: HectonTerrainMaterialInjector.cs:135-137 enables _NORMALMAP,
            // _TERRAIN_BLEND_HEIGHT and _MASKMAP on the CLONE it binds to the terrain. The shader declares
            // all three as shader_feature_local (HectonTerrain.shader:116-118), so whether the arrays are
            // actually sampled is decided on the clone - which is CheckSceneMat's job, and where a
            // bound-but-unsampled array IS a hard failure. Failing on the asset's keywords here would
            // reject materials that render correctly.
            string keywords = mat.shaderKeywords.Length > 0
                ? string.Join(", ", mat.shaderKeywords)
                : "<none>";
            report.AppendLine($"asset keywords: {keywords}");
            report.AppendLine(
                "  (informational - the rendered keyword set belongs to the injector's material clone, " +
                "not to this asset; CheckSceneMat verifies that)");

            report.AppendLine(
                $"examined {propertiesExamined} required slot(s), {propertiesBound} correctly bound, " +
                $"{failures} failure(s)");

            File.WriteAllText(reportPath, report.ToString());

            // Exit 0 has to mean the evidence exists on disk, not merely that no branch threw.
            if (!File.Exists(reportPath) || new FileInfo(reportPath).Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: the report at {reportPath} is missing or empty after the " +
                    "write, so this run produced no evidence.");
                EditorApplication.Exit(2);
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: no complete terrain material report was produced (intended " +
                $"{reportPath}); {propertiesExamined} slot(s) had been examined. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        // A verifier that examined nothing has proved nothing. Guarding this separately from `failures`
        // matters: if RequiredArrayProperties were ever emptied, every other counter would read clean.
        if (propertiesExamined == 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: examined 0 material slots. Nothing was verified.");
            EditorApplication.Exit(2);
            return;
        }

        if (failures > 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: {failures} problem(s) across {propertiesExamined} examined slot(s) " +
                $"on '{MaterialPath}'; {propertiesBound} were correctly bound. Details in {reportPath}.");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log(
            $"[{ToolName}] PASS: '{MaterialPath}' uses '{ExpectedShaderName}' and all " +
            $"{propertiesExamined} required slot(s) are bound ({propertiesBound} verified). Report: " +
            $"{reportPath}. This covers the ASSET only - CheckSceneMat covers what the scene binds.");
        EditorApplication.Exit(0);
    }
}
