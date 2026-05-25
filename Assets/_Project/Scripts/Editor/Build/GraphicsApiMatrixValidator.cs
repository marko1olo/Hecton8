#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.Core;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Verifies the graphics API order needed for Steam Deck, Windows legacy/modern, macOS Metal, and Android VR.
    /// </summary>
    internal sealed class GraphicsApiMatrixValidator : IPreprocessBuildWithReport
    {
        private const string StrictDefine = "HECTON_STRICT_GRAPHICS_API_BUILD";

        public int callbackOrder => -4650;

        public void OnPreprocessBuild(BuildReport report)
        {
            Validate(report.summary.platform, HasStrictDefine(report.summary.platform));
        }

        [MenuItem("HECTON-8/Platform/Validate Graphics API Matrix")]
        private static void ValidateFromMenu()
        {
            Validate(EditorUserBuildSettings.activeBuildTarget, strictBuild: false);
        }

        private static void Validate(BuildTarget target, bool strictBuild)
        {
            StringBuilder findings = new StringBuilder(512);
            int blockerCount = 0;

            switch (target)
            {
                case BuildTarget.StandaloneLinux64:
                    ValidateRequiredFirstApi(
                        target,
                        GraphicsDeviceType.Vulkan,
                        "Linux/Steam Deck must put Vulkan first. OpenGLCore can only be a fallback after device proof.",
                        findings,
                        ref blockerCount);
                    break;

                case BuildTarget.StandaloneOSX:
                    ValidateRequiredFirstApi(
                        target,
                        GraphicsDeviceType.Metal,
                        "macOS must put Metal first. OpenGL fallback is not acceptable for release validation.",
                        findings,
                        ref blockerCount);
                    break;

                case BuildTarget.StandaloneWindows64:
                    ValidateWindowsApis(target, findings, ref blockerCount);
                    break;

                case BuildTarget.Android:
                    ValidateAndroidApis(target, findings, ref blockerCount);
                    break;
            }

            if (blockerCount <= 0)
            {
                H8Debug.Log("[PLATFORM] Graphics API matrix validation passed for " + target + ".");
                return;
            }

            string message = "[PLATFORM] Graphics API matrix has " +
                             blockerCount +
                             " blocker(s) for " +
                             target +
                             ":\n" +
                             findings;
            if (strictBuild || target == BuildTarget.StandaloneLinux64)
                throw new BuildFailedException(message);

            H8Debug.LogWarning(message + "\nDefine " + StrictDefine + " to convert this report into a hard build failure.");
        }

        private static void ValidateRequiredFirstApi(
            BuildTarget target,
            GraphicsDeviceType requiredFirstApi,
            string message,
            StringBuilder findings,
            ref int blockerCount)
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(target);
            if (apis == null || apis.Length == 0)
            {
                AppendFinding(findings, ref blockerCount, target + " has no explicit graphics API list. " + message);
                return;
            }

            if (apis[0] != requiredFirstApi)
                AppendFinding(findings, ref blockerCount, target + " first graphics API is " + apis[0] + ", expected " + requiredFirstApi + ". " + message);
        }

        private static void ValidateWindowsApis(BuildTarget target, StringBuilder findings, ref int blockerCount)
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(target);
            if (apis == null || apis.Length == 0)
            {
                AppendFinding(findings, ref blockerCount, "Windows has no explicit graphics API list. Require Direct3D12 plus Direct3D11 fallback.");
                return;
            }

            bool hasD3D12 = Contains(apis, GraphicsDeviceType.Direct3D12);
            bool hasD3D11 = Contains(apis, GraphicsDeviceType.Direct3D11);
            if (!hasD3D12)
                AppendFinding(findings, ref blockerCount, "Windows modern path is missing Direct3D12.");

            if (!hasD3D11)
                AppendFinding(findings, ref blockerCount, "Windows legacy path is missing Direct3D11 fallback.");
        }

        private static void ValidateAndroidApis(BuildTarget target, StringBuilder findings, ref int blockerCount)
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(target);
            if (apis == null || apis.Length == 0)
            {
                AppendFinding(findings, ref blockerCount, "Android has no explicit graphics API list. Require Vulkan first for standalone VR with GLES3 fallback only after device proof.");
                return;
            }

            if (apis[0] != GraphicsDeviceType.Vulkan)
                AppendFinding(findings, ref blockerCount, "Android first graphics API is " + apis[0] + ", expected Vulkan for standalone VR.");

            if (!Contains(apis, GraphicsDeviceType.OpenGLES3))
                H8Debug.LogWarning("[PLATFORM] Android graphics APIs do not include OpenGLES3 fallback. This can be correct for strict Vulkan VR, but needs device proof.");
        }

        private static bool Contains(GraphicsDeviceType[] apis, GraphicsDeviceType expected)
        {
            for (int i = 0; i < apis.Length; i++)
            {
                if (apis[i] == expected)
                    return true;
            }

            return false;
        }

        private static void AppendFinding(StringBuilder findings, ref int blockerCount, string message)
        {
            blockerCount++;
            findings.Append("- ").Append(message).Append('\n');
        }

        private static bool HasStrictDefine(BuildTarget target)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            return defines.IndexOf(StrictDefine, StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
