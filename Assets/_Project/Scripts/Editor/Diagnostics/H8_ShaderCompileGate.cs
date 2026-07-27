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
// .compute assets go through the same measurement. ShaderUtil has a separate entry
// point for them (GetComputeShaderMessages), returning the identical
// UnityEditor.ShaderMessage[] the .shader path reads, so both asset kinds contribute
// real error and warning counts to the RESULT line. There is no compute analogue of
// Shader.isSupported, so the kernel roster stands in for it: a kernel that fails to
// build is dropped from the loaded asset while the asset itself still loads.
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
        private static readonly char[] KernelTokenSeparators = { ' ', '\t' };

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

            if (path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
                return InspectComputeAsset(path, ref warnings);

            return InspectShaderAsset(path, ref warnings);
        }

        private static int InspectComputeAsset(string path, ref int warnings)
        {
            ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            if (compute == null)
            {
                Debug.LogError($"{Marker} COMPUTE_LOAD_FAILED {path}");
                return 1;
            }

            int assetErrors = 0;
            int assetWarnings = 0;

            if (ShaderUtil.GetComputeShaderMessageCount(compute) > 0)
            {
                CountAndLogMessages(
                    path, ShaderUtil.GetComputeShaderMessages(compute), ref assetErrors, ref assetWarnings);
            }

            // Stands in for the missing Shader.isSupported: a kernel the compiler rejected is
            // absent from the built asset even though the asset still loads non-null, which
            // would otherwise read as a clean pass.
            List<string> declaredKernels = ReadDeclaredKernelNames(path);
            int liveKernels = 0;

            for (int kernelIndex = 0; kernelIndex < declaredKernels.Count; kernelIndex++)
            {
                string kernelName = declaredKernels[kernelIndex];
                if (compute.HasKernel(kernelName))
                {
                    liveKernels++;
                    continue;
                }

                Debug.LogError($"{Marker} MISSING_KERNEL {Path.GetFileName(path)}:{kernelName}");
                assetErrors++;
            }

            warnings += assetWarnings;

            Debug.Log(
                $"{Marker} COMPUTE {Path.GetFileName(path),-46} " +
                $"kernels={liveKernels}/{declaredKernels.Count} errors={assetErrors} warnings={assetWarnings}");

            return assetErrors;
        }

        private static int InspectShaderAsset(string path, ref int warnings)
        {
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
                CountAndLogMessages(
                    path, ShaderUtil.GetShaderMessages(shader), ref assetErrors, ref assetWarnings);
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

        // Both ShaderUtil entry points return the same UnityEditor.ShaderMessage[], so the
        // severity split lives in one place instead of drifting between the two asset kinds.
        private static void CountAndLogMessages(
            string path, ShaderMessage[] messages, ref int errors, ref int warnings)
        {
            for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++)
            {
                ShaderMessage message = messages[messageIndex];
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                    errors++;
                else
                    warnings++;

                Debug.Log(
                    $"{Marker}   {message.severity.ToString().ToUpperInvariant()} " +
                    $"{Path.GetFileName(path)}:{message.line} [{message.platform}] {message.message}");
            }
        }

        // ShaderUtil exposes no kernel enumeration, so the roster comes from the source text.
        // Pragmas inside a preprocessor conditional are skipped on purpose: that branch may
        // legitimately be off, and reporting those kernels as missing would make the gate cry wolf.
        private static List<string> ReadDeclaredKernelNames(string path)
        {
            List<string> kernelNames = new List<string>(8);
            string[] lines;

            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"{Marker} KERNEL_SCAN_FAILED {path} {exception.Message}");
                return kernelNames;
            }

            int conditionalDepth = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];

                int commentStart = line.IndexOf("//", StringComparison.Ordinal);
                if (commentStart >= 0)
                    line = line.Substring(0, commentStart);

                line = line.Trim();
                if (line.Length == 0 || line[0] != '#')
                    continue;

                // Tolerate `#  pragma kernel Foo`, which HLSL accepts.
                string directive = line.Substring(1).TrimStart();

                if (directive.StartsWith("if", StringComparison.Ordinal))
                {
                    conditionalDepth++;
                    continue;
                }

                if (directive.StartsWith("endif", StringComparison.Ordinal))
                {
                    if (conditionalDepth > 0)
                        conditionalDepth--;
                    continue;
                }

                if (conditionalDepth > 0 || !directive.StartsWith("pragma", StringComparison.Ordinal))
                    continue;

                string pragmaBody = directive.Substring("pragma".Length);
                if (pragmaBody.Length == 0 || !char.IsWhiteSpace(pragmaBody[0]))
                    continue;

                pragmaBody = pragmaBody.TrimStart();
                if (!pragmaBody.StartsWith("kernel", StringComparison.Ordinal))
                    continue;

                // Whitespace guard so a future `#pragma kernelsomething` is not read as a kernel.
                string kernelBody = pragmaBody.Substring("kernel".Length);
                if (kernelBody.Length == 0 || !char.IsWhiteSpace(kernelBody[0]))
                    continue;

                // `#pragma kernel Name DEFINE=1` declares one kernel; trailing tokens are defines.
                string[] tokens = kernelBody.Split(KernelTokenSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                    kernelNames.Add(tokens[0]);
            }

            return kernelNames;
        }

        private static List<string> ResolveRequestedPaths()
        {
            List<string> resolved = new List<string>(8);
            // Fully qualified: the project has its own Hecton8.Environment namespace, which wins over
            // System inside a Hecton8.* namespace and resolves to something with no
            // GetCommandLineArgs. Same trap caught H8_HeadlessPlayModeProbe.
            string[] commandLineArguments = System.Environment.GetCommandLineArgs();

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
