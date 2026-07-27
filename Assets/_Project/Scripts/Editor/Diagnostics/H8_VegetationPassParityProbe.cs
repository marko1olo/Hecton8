using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Batchmode gate for the four indirect-vegetation passes.
    ///
    /// ForwardLit, Shadow, DepthOnly and MotionVectors each carry their own copy of the instanced
    /// vertex animation. A shadow, depth or motion-vector pass may simplify SHADING, but it must
    /// never compute a different vertex POSITION than ForwardLit - if it does, plants cast shadows
    /// from a pose they are not in, self-occlude against a depth buffer that disagrees with them,
    /// and smear motion vectors that describe a movement that never happened.
    ///
    /// Five position-affecting functions had silently drifted apart before this probe existed, and
    /// every one was found by hand. The fix in each case was to move the function into a shared
    /// .hlsl so the copies cannot drift again - which only holds as long as all four passes keep
    /// including it. That is what SharedIncludes below asserts.
    ///
    /// Also reports every shader message Unity produced for the four passes, so a batchmode run has
    /// a single authoritative error/warning count instead of a hand-grepped log.
    ///
    /// Read-only. Reimports, never writes source.
    /// </summary>
    public static class H8_VegetationPassParityProbe
    {
        private const string Marker = "[H8_VEG_PARITY]";
        private const string ShaderDir = "Assets/_Project/Art/Shaders/";

        private static readonly string[] Passes =
        {
            ShaderDir + "Hecton_IndirectVegetation.shader",
            ShaderDir + "Hecton_IndirectVegetationShadow.shader",
            ShaderDir + "Hecton_IndirectVegetationDepthOnly.shader",
            ShaderDir + "Hecton_IndirectVegetationMotionVectors.shader",
        };

        // Every .hlsl that exists purely to keep the four passes byte-identical. A pass that stops
        // including one of these has, by definition, grown a private copy again.
        private static readonly string[] SharedIncludes =
        {
            "HectonIndirectVegetationHash.hlsl",
            "HectonIndirectVegetationWave.hlsl",
            "HectonIndirectVegetationAbyssalFlow.hlsl",
            "HectonIndirectVegetationWakeTrail.hlsl",
            "HectonIndirectVegetationInteraction.hlsl",
            "HectonIndirectVegetationPlayerBend.hlsl",
            "HectonIndirectVegetationBillboard.hlsl",
            "HectonIndirectVegetationPlanarFlow.hlsl",
        };

        // Functions that have been converged into a shared include. None of them may reappear as a
        // local definition in any pass - that is exactly how the last five divergences were born.
        private static readonly string[] MustNotBeRedefinedLocally =
        {
            "ResolveAbyssalFlowField",
            "ResolveWakeTrailOffset",
            "ResolveInteractionOffset",
            "ResolvePlayerBendOffset",
            "ResolveBillboardPositionWS",
            "ResolvePlanarOceanFlowDirection",
            "ResolvePlanarCurrentDirection",
            "ResolveInteractionDistance",
            "ResolveInteractionTypeScale",
            "FastVegetationPower01",
            "FastSinApprox",
            "FastCosApprox",
            "WrapPhasePi",
        };

        public static void Run()
        {
            var failures = 0;

            try
            {
                failures += ReimportAndReportMessages();
                failures += AuditSharedIncludes();
                failures += AuditLocalRedefinitions();
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} FAILED {ex.GetType().Name}: {ex.Message}");
                failures++;
            }

            Debug.Log($"{Marker} RESULT failures={failures}");
            Debug.Log($"{Marker} DONE");
        }

        private static int ReimportAndReportMessages()
        {
            // Reimport the includes first: touching a .shader does not rebuild it if only the .hlsl
            // it pulls in changed, so a stale variant can otherwise report clean.
            foreach (var include in SharedIncludes)
            {
                var path = ShaderDir + include;
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var errors = 0;
            var warnings = 0;

            foreach (var path in Passes)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    Debug.Log($"{Marker} MISSING {path}");
                    errors++;
                    continue;
                }

                var count = ShaderUtil.GetShaderMessageCount(shader);
                var passErrors = 0;
                var passWarnings = 0;

                if (count > 0)
                {
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    {
                        if (message.severity == ShaderCompilerMessageSeverity.Error)
                            passErrors++;
                        else
                            passWarnings++;

                        Debug.Log(
                            $"{Marker}   {message.severity.ToString().ToUpperInvariant()} " +
                            $"{Path.GetFileName(path)}:{message.line} [{message.platform}] {message.message}");
                    }
                }

                errors += passErrors;
                warnings += passWarnings;

                Debug.Log(
                    $"{Marker} PASS {Path.GetFileName(path),-46} " +
                    $"supported={shader.isSupported} errors={passErrors} warnings={passWarnings}");
            }

            Debug.Log($"{Marker} SHADER TOTALS errors={errors} warnings={warnings}");

            // Warnings are reported but do not fail the gate; a shader warning is not a wrong vertex.
            return errors;
        }

        private static int AuditSharedIncludes()
        {
            var failures = 0;

            foreach (var include in SharedIncludes)
            {
                var includePath = ShaderDir + include;
                if (!File.Exists(includePath))
                {
                    Debug.Log($"{Marker} INCLUDE MISSING {includePath}");
                    failures++;
                    continue;
                }

                var missingFrom = Passes
                    .Where(p => !File.Exists(p) || !File.ReadAllText(p).Contains(include))
                    .Select(Path.GetFileName)
                    .ToArray();

                if (missingFrom.Length == 0)
                {
                    Debug.Log($"{Marker} INCLUDE OK {include,-46} all 4 passes");
                }
                else
                {
                    Debug.Log($"{Marker} INCLUDE DROPPED {include} not included by: {string.Join(", ", missingFrom)}");
                    failures++;
                }
            }

            return failures;
        }

        private static int AuditLocalRedefinitions()
        {
            var failures = 0;

            foreach (var fn in MustNotBeRedefinedLocally)
            {
                // A definition, not a call: return type, name, open paren, and no trailing semicolon
                // (which would make it a forward declaration).
                var pattern = new Regex(
                    @"^\s*(?:float|float2|float3|float4|half|half2|half3|half4|void|int|uint)\s+" +
                    Regex.Escape(fn) + @"\s*\([^;]*$",
                    RegexOptions.Multiline);

                var offenders = new List<string>();
                foreach (var path in Passes)
                {
                    if (!File.Exists(path))
                        continue;

                    if (pattern.IsMatch(File.ReadAllText(path)))
                        offenders.Add(Path.GetFileName(path));
                }

                if (offenders.Count == 0)
                {
                    Debug.Log($"{Marker} NO LOCAL COPY {fn,-38} (shared include is the only definition)");
                }
                else
                {
                    Debug.Log($"{Marker} LOCAL REDEFINITION {fn} defined inside: {string.Join(", ", offenders)}");
                    failures++;
                }
            }

            return failures;
        }
    }
}
