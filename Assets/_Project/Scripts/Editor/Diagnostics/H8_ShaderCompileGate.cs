// ============================================================================
// HECTON-8 — H8_ShaderCompileGate.cs
// Batchmode compile gate for an explicit list of shader/compute assets.
//
// Why this exists: editing a .shader and then running any -executeMethod proves
// nothing about that shader. Unity compiles shader variants on demand, and the
// AssetDatabase keys off CONTENT HASH, so `touch`-ing a file does not trigger a
// reimport and the compile log stays silent whether the shader is valid or not.
// BootstrapArchitectureValidator cannot cover this either: it early-returns when
// 00_BOOTSTRAP is not loaded, which in batchmode it never is.
//
// This gate forces the reimport and reads the importer's own messages back, so a
// broken shader reports a file, line, platform and message instead of nothing.
//
// Usage (paths are semicolon-separated, project-relative):
//   Unity.exe -batchmode -quit -projectPath <proj> -logFile <log> \
//     -executeMethod Hecton8.EditorTools.Diagnostics.H8_ShaderCompileGate.Run \
//     -h8ShaderPaths "Assets/_Project/Art/Shaders/A.shader;Assets/.../B.compute"
//
// Grep the log for the RESULT line. Exit code is also set, but read the log, not
// the code: batchmode shutdown can segfault in third-party packages after the
// gate has already finished.
// ============================================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    public static class H8_ShaderCompileGate
    {
        private const string Marker = "[H8_SHADER_GATE]";
        private const string PathsArgument = "-h8ShaderPaths";
        private const int MissingRequestExitCode = 3;
        private const int CompileErrorExitCode = 2;

        public static void Run()
        {
            List<string> requestedPaths = ResolveRequestedPaths();

            // Fail safe: an empty request must not read as a pass.
            if (requestedPaths.Count == 0)
            {
                Debug.LogError(
                    $"{Marker} RESULT requested=0 errors=1 warnings=0 " +
                    $"reason=no {PathsArgument} supplied");
                EditorApplication.Exit(MissingRequestExitCode);
                return;
            }

            int errors = 0;
            int warnings = 0;

            for (int pathIndex = 0; pathIndex < requestedPaths.Count; pathIndex++)
            {
                string path = requestedPaths[pathIndex];
                errors += InspectSingleAsset(path, ref warnings);
            }

            string resultLine =
                $"{Marker} RESULT requested={requestedPaths.Count} errors={errors} warnings={warnings}";

            if (errors > 0)
            {
                Debug.LogError(resultLine);
                EditorApplication.Exit(CompileErrorExitCode);
                return;
            }

            Debug.Log(resultLine);
            EditorApplication.Exit(0);
        }

        private static int InspectSingleAsset(string path, ref int warnings)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"{Marker} MISSING {path}");
                return 1;
            }

            // ForceUpdate is the whole point: without it an unchanged content hash
            // means the importer never runs and no messages are ever produced.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Compute assets are not Shaders and carry no ShaderUtil message list,
            // so they are reported as imported rather than silently counted as passing.
            if (path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
            {
                ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (compute == null)
                {
                    Debug.LogError($"{Marker} COMPUTE_LOAD_FAILED {path}");
                    return 1;
                }

                Debug.Log($"{Marker} COMPUTE {Path.GetFileName(path),-46} imported (see log for compile errors)");
                return 0;
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.LogError($"{Marker} SHADER_LOAD_FAILED {path}");
                return 1;
            }

            int assetErrors = 0;
            int assetWarnings = 0;

            if (ShaderUtil.GetShaderMessageCount(shader) > 0)
            {
                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++)
                {
                    ShaderMessage message = messages[messageIndex];
                    if (message.severity == ShaderCompilerMessageSeverity.Error)
                        assetErrors++;
                    else
                        assetWarnings++;

                    Debug.Log(
                        $"{Marker}   {message.severity.ToString().ToUpperInvariant()} " +
                        $"{Path.GetFileName(path)}:{message.line} [{message.platform}] {message.message}");
                }
            }

            // isSupported false on the editor's own graphics API means the shader cannot
            // run here at all, which the message list does not always report.
            if (!shader.isSupported)
            {
                Debug.LogError($"{Marker} UNSUPPORTED {path}");
                assetErrors++;
            }

            warnings += assetWarnings;

            Debug.Log(
                $"{Marker} SHADER {Path.GetFileName(path),-46} " +
                $"supported={shader.isSupported} errors={assetErrors} warnings={assetWarnings}");

            return assetErrors;
        }

        private static List<string> ResolveRequestedPaths()
        {
            List<string> resolved = new List<string>(8);
            string[] commandLineArguments = Environment.GetCommandLineArgs();

            for (int argumentIndex = 0; argumentIndex < commandLineArguments.Length - 1; argumentIndex++)
            {
                if (!string.Equals(commandLineArguments[argumentIndex], PathsArgument, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] split = commandLineArguments[argumentIndex + 1].Split(';');
                for (int splitIndex = 0; splitIndex < split.Length; splitIndex++)
                {
                    string candidate = split[splitIndex].Trim().Replace('\\', '/');
                    if (candidate.Length > 0)
                        resolved.Add(candidate);
                }
            }

            return resolved;
        }
    }
}

#endif
