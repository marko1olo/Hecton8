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
        private const string ProjectPluginRoot = "Assets/_Project/Plugins";
        private const string VendorPluginRoot = "Assets/Plugins";
        private const string AudioKernelNativeSourcePath = "NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp";
        private const string AudioKernelNativeBuildScriptPath = "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernel.bat";

        public int callbackOrder => -4640;

        public void OnPreprocessBuild(BuildReport report)
        {
            Validate(report.summary.platform, strictBuild: true);
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
                    RequirePlugin(ProjectPluginRoot + "/Windows/x86_64/liblz4.dll", "LZ4 Windows x64", target, blockers, ref blockerCount);
                    RequirePlugin(VendorPluginRoot + "/x86_64/HectonAudioKernel.dll", "Audio Kernel Windows x64", target, blockers, ref blockerCount);
                    RequirePluginFreshness(
                        VendorPluginRoot + "/x86_64/HectonAudioKernel.dll",
                        AudioKernelNativeSourcePath,
                        "Audio Kernel Windows x64 native source",
                        blockers,
                        ref blockerCount);
                    RequirePluginFreshness(
                        VendorPluginRoot + "/x86_64/HectonAudioKernel.dll",
                        AudioKernelNativeBuildScriptPath,
                        "Audio Kernel Windows x64 native build script",
                        blockers,
                        ref blockerCount);
                    break;

                case BuildTarget.StandaloneLinux64:
                    RequireAnyPlugin(new[]
                    {
                        ProjectPluginRoot + "/Linux/x86_64/liblz4.so",
                        ProjectPluginRoot + "/Linux/x86_64/lz4.so"
                    }, "LZ4 Linux x64", target, blockers, ref blockerCount);
                    RequireAnyPlugin(new[]
                    {
                        VendorPluginRoot + "/x86_64/libHectonAudioKernel.so",
                        VendorPluginRoot + "/x86_64/HectonAudioKernel.so"
                    }, "Audio Kernel Linux x64", target, blockers, ref blockerCount);
                    RequireAnyPlugin(new[]
                    {
                        VendorPluginRoot + "/Steamworks/Linux/x86_64/libsteam_api.so",
                        VendorPluginRoot + "/Steamworks/redistributable_bin/linux64/libsteam_api.so"
                    }, "Steamworks Linux x64", target, blockers, ref blockerCount);
                    break;

                case BuildTarget.StandaloneOSX:
                    RequireAnyPlugin(new[]
                    {
                        ProjectPluginRoot + "/Mac/Universal/liblz4.dylib",
                        ProjectPluginRoot + "/Mac/x86_64/liblz4.dylib",
                        ProjectPluginRoot + "/Mac/arm64/liblz4.dylib"
                    }, "LZ4 macOS", target, blockers, ref blockerCount);
                    RequireAnyPlugin(new[]
                    {
                        VendorPluginRoot + "/Mac/Universal/libHectonAudioKernel.dylib",
                        VendorPluginRoot + "/Mac/x86_64/libHectonAudioKernel.dylib",
                        VendorPluginRoot + "/Mac/arm64/libHectonAudioKernel.dylib"
                    }, "Audio Kernel macOS", target, blockers, ref blockerCount);
                    RequireAnyPlugin(new[]
                    {
                        VendorPluginRoot + "/Steamworks/osx/libsteam_api.dylib",
                        VendorPluginRoot + "/Steamworks/redistributable_bin/osx/libsteam_api.dylib"
                    }, "Steamworks macOS", target, blockers, ref blockerCount);
                    break;

                case BuildTarget.Android:
                    RequireAnyPlugin(new[]
                    {
                        ProjectPluginRoot + "/Android/arm64-v8a/liblz4.so",
                        ProjectPluginRoot + "/Android/libs/arm64-v8a/liblz4.so"
                    }, "LZ4 Android arm64", target, blockers, ref blockerCount);
                    RequireAnyPlugin(new[]
                    {
                        VendorPluginRoot + "/Android/arm64-v8a/libHectonAudioKernel.so",
                        VendorPluginRoot + "/Android/libs/arm64-v8a/libHectonAudioKernel.so"
                    }, "Audio Kernel Android arm64", target, blockers, ref blockerCount);
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

            H8Debug.LogWarning(message + "\nActual player builds fail on this matrix; this editor menu scan is advisory only.");
        }

        private static void RequirePlugin(string assetPath, string label, BuildTarget target, StringBuilder blockers, ref int blockerCount)
        {
            if (!AssetFileExists(assetPath))
            {
                blockerCount++;
                blockers.Append("- Missing ")
                    .Append(label)
                    .Append(": ")
                    .Append(assetPath)
                    .Append('\n');
                return;
            }

            if (HasPluginImporter(assetPath, target))
                return;

            blockerCount++;
            blockers.Append("- Invalid ")
                .Append(label)
                .Append(" importer for ")
                .Append(target)
                .Append(": ")
                .Append(assetPath)
                .Append('\n');
        }

        private static void RequireAnyPlugin(
            string[] assetPaths,
            string label,
            BuildTarget target,
            StringBuilder blockers,
            ref int blockerCount)
        {
            int existingCount = 0;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (!AssetFileExists(assetPaths[i]))
                    continue;

                existingCount++;
                if (HasPluginImporter(assetPaths[i], target))
                    return;
            }

            blockerCount++;
            if (existingCount <= 0)
            {
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
                return;
            }

            blockers.Append("- Invalid ")
                .Append(label)
                .Append(" importer for ")
                .Append(target)
                .Append(". Existing file(s) are not enabled for this build target:");
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (!AssetFileExists(assetPaths[i]))
                    continue;

                blockers.Append('\n')
                    .Append("  ")
                    .Append(assetPaths[i]);
            }

            blockers.Append('\n');
        }

        private static bool HasPluginImporter(string assetPath, BuildTarget target)
        {
            PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
            return importer != null && importer.GetCompatibleWithPlatform(target);
        }

        private static void RequirePluginFreshness(
            string assetPath,
            string referencePath,
            string label,
            StringBuilder blockers,
            ref int blockerCount)
        {
            if (!AssetFileExists(assetPath))
                return;

            if (!AssetFileExists(referencePath))
            {
                blockerCount++;
                blockers.Append("- Missing freshness reference for ")
                    .Append(label)
                    .Append(": ")
                    .Append(referencePath)
                    .Append('\n');
                return;
            }

            DateTime assetTimestamp = File.GetLastWriteTimeUtc(ToProjectAbsolutePath(assetPath));
            DateTime referenceTimestamp = File.GetLastWriteTimeUtc(ToProjectAbsolutePath(referencePath));
            if (assetTimestamp >= referenceTimestamp)
                return;

            blockerCount++;
            blockers.Append("- Stale ")
                .Append(label)
                .Append(": ")
                .Append(assetPath)
                .Append(" utc=")
                .Append(assetTimestamp.ToString("O"))
                .Append(" older than ")
                .Append(referencePath)
                .Append(" utc=")
                .Append(referenceTimestamp.ToString("O"))
                .Append(". Rebuild native plugin before player build.")
                .Append('\n');
        }

        private static bool AssetFileExists(string assetPath)
        {
            return File.Exists(ToProjectAbsolutePath(assetPath));
        }

        private static string ToProjectAbsolutePath(string assetPath)
        {
            string normalizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalizedPath));
        }

    }
}
#endif
