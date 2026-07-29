using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

/// <summary>
/// Reports whether the Hecton terrain shader (<see cref="ShaderName"/>, declared at
/// <c>Assets/_Project/Shaders/HectonTerrain.shader:1</c>) actually compiles, by forcing its import and
/// reading the shader compiler's OWN messages back.
///
/// WHAT WAS WRONG - the old body was five statements and its headline claim was unsupported by every one
/// of them:
///
/// * IT NEVER READ THE COMPILE RESULT. It printed "Shader compiles successfully!" (old <c>:14</c>) having
///   consulted exactly one bit, <see cref="Shader.isSupported"/>. It never touched
///   <see cref="ShaderUtil.GetShaderMessages"/>, which is the only API that returns what the shader
///   compiler said. <c>isSupported</c> is not a compile verdict: it answers "does at least one subshader
///   survive on the current graphics device", so a shader carrying compile errors in a pass, or falling
///   back, or erroring on variants that were never built, reads as supported. A tool whose whole purpose
///   is the compile status was reporting a different question's answer;
/// * IT NEVER TRIGGERED A COMPILE EITHER. With no <see cref="AssetDatabase.ImportAsset"/> the importer does
///   not run - the AssetDatabase keys off content hash - so both the bit it read and the messages it did
///   not read described whatever the last import in some earlier session happened to leave behind. This is
///   the exact reason <c>Diagnostics/H8_ShaderCompileGate.cs:108-110</c> forces the reimport;
/// * ITS FAILURE MESSAGE NAMED THE WRONG CAUSE. "Shader not supported (compiled with errors)!" (old
///   <c>:11</c>) asserts a compile error from a device-support bit. Unity's own diagnosis for this shader
///   was different and is on record: <c>Docs/Reports/CompileCheck_R6.log:8279-8281</c> reads
///   <c>WARNING: Shader Unsupported: 'Hecton8/URP/Terrain_TextureArray' - All subshaders removed</c>
///   followed by <c>Did you use #pragma only_renderers and omit this platform?</c> - a stripping problem,
///   not a syntax error. Note also that Unity logged those as WARNINGS, which is why the message list alone
///   cannot be the only gate and <c>isSupported</c> has to stay a hard failure;
/// * BOTH FAILURE PATHS FELL THROUGH. <see cref="EditorApplication.Exit"/> was called (old <c>:8</c>,
///   <c>:12</c>) with no <c>return</c> after it, so the null-shader branch continued straight into
///   <c>s.isSupported</c> - a NullReferenceException on the path that had just diagnosed the problem
///   correctly. With no try/catch, that throw set no exit code from this tool at all;
/// * EXIT CODE 1 IS UNITY'S OWN. Unity returns 1 for its own startup and licence failures, so exit 1 from
///   this tool was indistinguishable from the editor never having reached it. This family uses 2;
/// * IT WROTE NO EVIDENCE ANYWHERE. Now a per-tool report under <c>Logs/compile_shader/</c>.
///
/// SCOPE, so a pass is not over-read: Unity compiles shader variants on demand. A clean forced import plus
/// <c>isSupported</c> proves the importer produced no error for the variants it built and that a subshader
/// survives on THIS machine's graphics device. It is not proof that every keyword combination compiles on
/// every target platform, and it says nothing about how the terrain looks - that is
/// <c>Docs/QUALITY_GATES.md</c>'s gate, never this file's.
///
/// This tool is the fixed-target convenience entry point for one shader.
/// <c>Diagnostics/H8_ShaderCompileGate.cs</c> is the general, argument-driven gate for an arbitrary list of
/// .shader/.compute assets and is the reference implementation for this idiom; prefer it for new work.
/// </summary>
public static class CompileShader
{
    private const string ToolName = "CompileShader";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not
    /// a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "compile_shader");

    private static readonly string ReportPath = Path.Combine(OutputDir, "compile_shader.txt");

    /// <summary>Verified against <c>Assets/_Project/Shaders/HectonTerrain.shader:1</c>.</summary>
    private const string ShaderName = "Hecton8/URP/Terrain_TextureArray";

    /// <summary>
    /// Fallback ONLY, used to tell "the asset is gone" apart from "the asset is there and the
    /// <c>Shader "..."</c> declaration was renamed", which are the same null from
    /// <see cref="Shader.Find"/>. The authoritative path comes from
    /// <see cref="AssetDatabase.GetAssetPath"/> on whatever is found.
    /// </summary>
    private const string LastKnownAssetPath = "Assets/_Project/Shaders/HectonTerrain.shader";

    public static void Execute()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"{ToolName} report");
        report.AppendLine($"requested shader: {ShaderName}");

        try
        {
            Directory.CreateDirectory(OutputDir);

            // Shader.Find searches the AssetDatabase in the editor, so it does see project shaders in
            // batchmode. A null here has two very different causes, separated below.
            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Shader atPath = AssetDatabase.LoadAssetAtPath<Shader>(LastKnownAssetPath);
                if (atPath == null)
                {
                    Fail(report,
                        $"no shader named '{ShaderName}' exists and there is no shader asset at " +
                        $"'{LastKnownAssetPath}' either. The shader was deleted or moved, so nothing was " +
                        "compiled and nothing was verified.");
                    return;
                }

                Fail(report,
                    $"no shader named '{ShaderName}' exists, but '{LastKnownAssetPath}' does contain a " +
                    $"shader declaring itself '{atPath.name}'. The Shader \"...\" name was changed and " +
                    "every material and script that binds by the old name is now bound to nothing. " +
                    "Nothing was verified about compilation.");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(shader);
            report.AppendLine($"asset path: {(string.IsNullOrEmpty(assetPath) ? "<none>" : assetPath)}");

            if (string.IsNullOrEmpty(assetPath))
            {
                // Without a path the importer cannot be forced, so any message list read here would belong
                // to an unknown earlier import. Reporting that as a compile verdict is the original bug.
                Fail(report,
                    $"'{ShaderName}' resolved to a shader with no asset path, so its import cannot be " +
                    "forced and the compiler messages cannot be attributed to this run. No compile verdict " +
                    "is possible.");
                return;
            }

            // THE FIX FOR "NEVER TRIGGERED A COMPILE". ForceUpdate because an unchanged content hash means
            // the importer never runs; allowAsyncCompilation off because async import returns before the
            // compiler has produced any message, which would have this tool read an empty list and call it
            // clean. Both mirror H8_ShaderCompileGate.cs:70-84,108-110.
            bool restoreAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = restoreAsyncCompilation;
            }

            // The reimport invalidates the object found before it, so re-resolve from the path.
            shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                Fail(report,
                    $"'{assetPath}' produced no Shader object after a forced reimport. The asset failed to " +
                    "import at all, so there is no compiled shader to judge.");
                return;
            }

            int errors = 0;
            int warnings = 0;

            // THE FIX FOR "NEVER READ THE RESULT". This is the compiler's own output.
            int messageCount = ShaderUtil.GetShaderMessageCount(shader);
            report.AppendLine($"compiler messages: {messageCount}");

            if (messageCount > 0)
            {
                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                for (int i = 0; i < messages.Length; i++)
                {
                    ShaderMessage message = messages[i];
                    bool isError = message.severity == ShaderCompilerMessageSeverity.Error;
                    if (isError)
                        errors++;
                    else
                        warnings++;

                    // Report the message's OWN file: a diagnostic raised inside an .hlsl this shader
                    // includes carries that file's line number, and printing the .shader name sends the
                    // reader to an unrelated line. Lesson recorded at H8_ShaderCompileGate.cs:229-235.
                    string origin = string.IsNullOrEmpty(message.file)
                        ? Path.GetFileName(assetPath)
                        : Path.GetFileName(message.file);

                    string line =
                        $"  {message.severity.ToString().ToUpperInvariant()} {origin}:{message.line} " +
                        $"[{message.platform}] {message.message}";
                    report.AppendLine(line);

                    if (isError)
                        Debug.LogError($"[{ToolName}]{line}");
                    else
                        Debug.LogWarning($"[{ToolName}]{line}");
                }
            }

            bool supported = shader.isSupported;
            report.AppendLine($"errors: {errors}  warnings: {warnings}");
            report.AppendLine($"isSupported: {supported}");
            report.AppendLine($"graphicsDeviceType: {SystemInfo.graphicsDeviceType}");

            // Device-independent and definitive: real compiler errors are a failure whatever device is
            // present, so this verdict is issued before the GPU question is asked.
            if (errors > 0)
            {
                Fail(report,
                    $"the shader compiler reported {errors} error(s) and {warnings} warning(s) for " +
                    $"'{assetPath}'. The shader does NOT compile. Each error is logged above with its own " +
                    "file, line and platform.");
                return;
            }

            // PART 4, PLACED DELIBERATELY LATE. This tool does not blit, read back, encode a PNG or dispatch
            // compute, so the usual criterion for a GPU gate does not apply - but its verdict rests on
            // shader.isSupported, which is ANSWERED AGAINST THE CURRENT GRAPHICS DEVICE, and with
            // GraphicsDeviceType.Null there is no device to answer it. Worse, with no device the importer
            // may build no variants, so "0 errors" can mean "nothing was compiled" rather than "nothing is
            // wrong" - a plausible clean bill of health from a run that measured nothing, which is this
            // family's signature defect. The gate sits AFTER the messages are logged and the report is
            // written, so a no-GPU run still yields its device-independent evidence and only the
            // unanswerable half is refused; that is why this is not a gate that blocks correct runs.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                report.AppendLine(
                    "RESULT: REFUSED - no GPU context; isSupported is not answerable and a 0-error " +
                    "message list may mean no variant was compiled.");
                WriteReport(report);
                Debug.LogError(
                    $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null), so no compile " +
                    $"verdict for '{ShaderName}' can be substantiated. isSupported is answered against the " +
                    "current graphics device, and with no device the importer may compile no variants at " +
                    $"all - so the {errors} error(s) / {warnings} warning(s) counted here are a floor, not " +
                    $"a measurement. Remove -nographics. Report at {ReportPath}");
                EditorApplication.Exit(3);
                return;
            }

            // Kept as a HARD failure, not a logged-and-ignored bit: this is the exact defect found in
            // sibling TerrainShaderVerify, and Unity reports "All subshaders removed" for this shader as a
            // WARNING (CompileCheck_R6.log:8279), so the error count above would not catch it.
            if (!supported)
            {
                Fail(report,
                    $"'{ShaderName}' reports isSupported == false on " +
                    $"{SystemInfo.graphicsDeviceType} after a forced reimport, with {errors} error(s) and " +
                    $"{warnings} warning(s). Every subshader was removed for this device, so the shader " +
                    "cannot run here at all and anything bound to it renders as an error surface. Unity " +
                    "logs this as a warning ('All subshaders removed'), which is why the message count " +
                    "alone reads clean. Check #pragma only_renderers and the Fallback.");
                return;
            }

            report.AppendLine(
                $"RESULT: PASS - forced reimport of '{assetPath}' produced 0 errors ({warnings} " +
                $"warning(s)) and isSupported == true on {SystemInfo.graphicsDeviceType}.");
            WriteReport(report);

            if (!File.Exists(ReportPath) || new FileInfo(ReportPath).Length == 0)
            {
                // Exit 0 has to mean the evidence exists on disk, not merely that no branch threw.
                Debug.LogError(
                    $"[{ToolName}] FAILED: the report at {ReportPath} is missing or empty after the write, " +
                    "so this run produced no durable evidence.");
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log(
                $"[{ToolName}] PASS: '{ShaderName}' ({assetPath}) was force-reimported in this run; the " +
                $"shader compiler reported 0 errors and {warnings} warning(s), and isSupported == true on " +
                $"{SystemInfo.graphicsDeviceType}. Unity compiles variants on demand, so this is not proof " +
                "that every keyword combination compiles on every target platform, and it is not a visual " +
                $"verdict - Docs/QUALITY_GATES.md owns that. Report: {ReportPath}");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch, so a throw set no exit code from this tool at all.
            Fail(report, $"threw before a compile verdict for '{ShaderName}' was produced. {ex}");
        }
    }

    /// <summary>
    /// Writes the verdict into the report as well as the log, then exits 2. Exit 2 rather than the old 1:
    /// Unity itself returns 1 for licence and startup failures, so 1 cannot be told apart from the editor
    /// never reaching this tool.
    /// </summary>
    private static void Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        WriteReport(report);
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        EditorApplication.Exit(2);
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
