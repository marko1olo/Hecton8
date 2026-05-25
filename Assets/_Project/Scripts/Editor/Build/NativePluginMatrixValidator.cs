#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Reports missing platform-native binaries before a player build leaves the editor.
    /// </summary>
    internal sealed class NativePluginMatrixValidator : IPreprocessBuildWithReport
    {
        private const string StrictDefine = "HECTON_STRICT_NATIVE_PLUGIN_BUILD";
        private const string ProjectPluginRoot = "Assets/_Project/Plugins";
        private const string VendorPluginRoot = "Assets/Plugins";

        public int callbackOrder => -4640;

        public void OnPreprocessBuild(BuildReport report)
        {
            Validate(report.summary.platform, strictBuild: HasStrictDefine(report.summary.platform));
        }

        [MenuItem("HECTON-8/Platform/Validate Native Plugin Matrix")]
        private static void ValidateFromMenu()
        {
            Validate(EditorUserBuildSettings.activeBuildTarget, strictBuild: false);
        }

        private static void Validate(BuildTarget target, bool strictBuild)
        {
            StringBuilder blockers = new StringBuilder(512);
            int blockerCount = 0;

            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    RequireFile(ProjectPluginRoot + "/Windows/x86_64/liblz4.dll", "LZ4 Windows x64", blockers, ref blockerCount);
                    RequireFile(VendorPluginRoot + "/x86_64/HectonAudioKernel.dll", "Audio Kernel Windows x64", blockers, ref blockerCount);
                    break;

                case BuildTarget.StandaloneLinux64:
                    RequireAny(new[]
                    {
                        ProjectPluginRoot + "/Linux/x86_64/liblz4.so",
                        ProjectPluginRoot + "/Linux/x86_64/lz4.so"
                    }, "LZ4 Linux x64", blockers, ref blockerCount);
                    RequireAny(new[]
                    {
                        VendorPluginRoot + "/x86_64/libHectonAudioKernel.so",
                        VendorPluginRoot + "/x86_64/HectonAudioKernel.so"
                    }, "Audio Kernel Linux x64", blockers, ref blockerCount);
                    RequireAny(new[]
                    {
                        VendorPluginRoot + "/Steamworks/Linux/x86_64/libsteam_api.so",
                        VendorPluginRoot + "/Steamworks/redistributable_bin/linux64/libsteam_api.so"
                    }, "Steamworks Linux x64", blockers, ref blockerCount);
                    break;

                case BuildTarget.StandaloneOSX:
                    RequireAny(new[]
                    {
                        ProjectPluginRoot + "/Mac/Universal/liblz4.dylib",
                        ProjectPluginRoot + "/Mac/x86_64/liblz4.dylib",
                        ProjectPluginRoot + "/Mac/arm64/liblz4.dylib"
                    }, "LZ4 macOS", blockers, ref blockerCount);
                    RequireAny(new[]
                    {
                        VendorPluginRoot + "/Mac/Universal/libHectonAudioKernel.dylib",
                        VendorPluginRoot + "/Mac/x86_64/libHectonAudioKernel.dylib",
                        VendorPluginRoot + "/Mac/arm64/libHectonAudioKernel.dylib"
                    }, "Audio Kernel macOS", blockers, ref blockerCount);
                    RequireAny(new[]
                    {
                        VendorPluginRoot + "/Steamworks/osx/libsteam_api.dylib",
                        VendorPluginRoot + "/Steamworks/redistributable_bin/osx/libsteam_api.dylib"
                    }, "Steamworks macOS", blockers, ref blockerCount);
                    break;

                case BuildTarget.Android:
                    RequireAny(new[]
                    {
                        ProjectPluginRoot + "/Android/arm64-v8a/liblz4.so",
                        ProjectPluginRoot + "/Android/libs/arm64-v8a/liblz4.so"
                    }, "LZ4 Android arm64", blockers, ref blockerCount);
                    break;
            }

            if (blockerCount <= 0)
            {
                H8Debug.Log("[PLATFORM] Native plugin matrix validation passed for " + target + ".");
                return;
            }

            string message = "[PLATFORM] Native plugin matrix has " +
                             blockerCount +
                             " blocker(s) for " +
                             target +
                             ":\n" +
                             blockers;
            if (strictBuild)
                throw new BuildFailedException(message);

            H8Debug.LogWarning(message + "\nDefine " + StrictDefine + " to convert this report into a hard build failure.");
        }

        private static void RequireFile(string assetPath, string label, StringBuilder blockers, ref int blockerCount)
        {
            if (File.Exists(assetPath))
                return;

            blockerCount++;
            blockers.Append("- Missing ")
                .Append(label)
                .Append(": ")
                .Append(assetPath)
                .Append('\n');
        }

        private static void RequireAny(string[] assetPaths, string label, StringBuilder blockers, ref int blockerCount)
        {
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (File.Exists(assetPaths[i]))
                    return;
            }

            blockerCount++;
            blockers.Append("- Missing ")
                .Append(label)
                .Append(". Expected one of:");
            for (int i = 0; i < assetPaths.Length; i++)
            {
                blockers.Append('\n')
                    .Append("  ")
                    .Append(assetPaths[i]);
            }

            blockers.Append('\n');
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
