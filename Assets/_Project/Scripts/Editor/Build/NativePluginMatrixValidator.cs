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
        private const string AudioKernelNativeAndroidBuildScriptPath = "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernelAndroid.bat";
        private const string DataMonolithArenaPath = "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs";
        private const string GlobalDataVaultPath = "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs";
        private const string DataMonolithAndroidNativeSourcePath = "Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp";
        private const string DataMonolithAndroidNativeSourceMetaPath = "Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp.meta";
        private const string DataMonolithAndroidCmakePath = "Assets/Plugins/Android/Native/CMakeLists.txt";
        private const string DataMonolithAndroidCmakeMetaPath = "Assets/Plugins/Android/Native/CMakeLists.txt.meta";
        private const string AndroidMainGradleTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

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
                {
                    RequireAnyPlugin(new[]
                    {
                        ProjectPluginRoot + "/Android/arm64-v8a/liblz4.so",
                        ProjectPluginRoot + "/Android/libs/arm64-v8a/liblz4.so"
                    }, "LZ4 Android arm64", target, blockers, ref blockerCount);

                    string[] androidAudioKernelPaths =
                    {
                        VendorPluginRoot + "/Android/arm64-v8a/libHectonAudioKernel.so",
                        VendorPluginRoot + "/Android/libs/arm64-v8a/libHectonAudioKernel.so"
                    };
                    RequireAnyPlugin(androidAudioKernelPaths, "Audio Kernel Android arm64", target, blockers, ref blockerCount);
                    RequireAnyCompatiblePluginFreshness(
                        androidAudioKernelPaths,
                        AudioKernelNativeSourcePath,
                        "Audio Kernel Android arm64 native source",
                        target,
                        blockers,
                        ref blockerCount);
                    RequireAnyCompatiblePluginFreshness(
                        androidAudioKernelPaths,
                        AudioKernelNativeAndroidBuildScriptPath,
                        "Audio Kernel Android arm64 native build script",
                        target,
                        blockers,
                        ref blockerCount);
                    RequireAndroidDataMonolithBridgeSourceRoute(blockers, ref blockerCount);
                    break;
                }
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

        private static void RequireAnyCompatiblePluginFreshness(
            string[] assetPaths,
            string referencePath,
            string label,
            BuildTarget target,
            StringBuilder blockers,
            ref int blockerCount)
        {
            bool hasCompatibleAsset = false;
            DateTime newestAssetTimestamp = DateTime.MinValue;
            string newestAssetPath = string.Empty;

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (!AssetFileExists(assetPath) || !HasPluginImporter(assetPath, target))
                    continue;

                hasCompatibleAsset = true;
                DateTime assetTimestamp = File.GetLastWriteTimeUtc(ToProjectAbsolutePath(assetPath));
                if (assetTimestamp > newestAssetTimestamp)
                {
                    newestAssetTimestamp = assetTimestamp;
                    newestAssetPath = assetPath;
                }
            }

            if (!hasCompatibleAsset)
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

            DateTime referenceTimestamp = File.GetLastWriteTimeUtc(ToProjectAbsolutePath(referencePath));
            if (newestAssetTimestamp >= referenceTimestamp)
                return;

            blockerCount++;
            blockers.Append("- Stale ")
                .Append(label)
                .Append(": newest compatible asset ")
                .Append(newestAssetPath)
                .Append(" utc=")
                .Append(newestAssetTimestamp.ToString("O"))
                .Append(" older than ")
                .Append(referencePath)
                .Append(" utc=")
                .Append(referenceTimestamp.ToString("O"))
                .Append(". Rebuild native plugin before player build.")
                .Append('\n');
        }

        private static void RequireAndroidDataMonolithBridgeSourceRoute(StringBuilder blockers, ref int blockerCount)
        {
            if (!AssetFileExists(DataMonolithAndroidNativeSourcePath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Data Monolith Android AAsset native source", DataMonolithAndroidNativeSourcePath);
                return;
            }

            if (!AssetFileExists(DataMonolithAndroidNativeSourceMetaPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Data Monolith Android AAsset native source meta", DataMonolithAndroidNativeSourceMetaPath);
                return;
            }

            if (!AssetFileExists(DataMonolithArenaPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Data Monolith C# arena binding", DataMonolithArenaPath);
                return;
            }

            if (!AssetFileExists(GlobalDataVaultPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "GlobalDataVault writer-release contract", GlobalDataVaultPath);
                return;
            }

            if (!AssetFileExists(DataMonolithAndroidCmakePath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Data Monolith Android CMake build script", DataMonolithAndroidCmakePath);
                return;
            }

            if (!AssetFileExists(DataMonolithAndroidCmakeMetaPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Data Monolith Android CMake build script meta", DataMonolithAndroidCmakeMetaPath);
                return;
            }

            if (!AssetFileExists(AndroidMainGradleTemplatePath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Android main Gradle template", AndroidMainGradleTemplatePath);
                return;
            }

            if (!AssetFileExists(AndroidManifestPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "Android main manifest", AndroidManifestPath);
                return;
            }

            if (!AssetFileExists(ProjectSettingsPath))
            {
                AppendMissingSourceRouteBlocker(blockers, ref blockerCount, "ProjectSettings Android scripting backend", ProjectSettingsPath);
                return;
            }

            string nativeSource = ReadProjectText(DataMonolithAndroidNativeSourcePath);
            string nativeSourceMeta = ReadProjectText(DataMonolithAndroidNativeSourceMetaPath);
            string arenaSource = ReadProjectText(DataMonolithArenaPath);
            string globalDataVaultSource = ReadProjectText(GlobalDataVaultPath);
            string cmake = ReadProjectText(DataMonolithAndroidCmakePath);
            string cmakeMeta = ReadProjectText(DataMonolithAndroidCmakeMetaPath);
            string gradle = ReadProjectText(AndroidMainGradleTemplatePath);
            string manifest = ReadProjectText(AndroidManifestPath);
            string projectSettings = ReadProjectText(ProjectSettingsPath);

            bool nativeBridgeValid =
                nativeSource.IndexOf("AAssetManager_fromJava", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("AAssetManager_open", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("AAsset_getLength64", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("AAsset_openFileDescriptor64", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("AAsset_read", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("AAsset_close", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("assetLength < 0 || assetLength != bufferSize", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("close(fd)", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("H8_TryMeasureCString", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("requiredBytes > static_cast<size_t>(capacity)", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("std::strlen", StringComparison.Ordinal) < 0 &&
                nativeSource.IndexOf("H8_WriteTelemetryDump", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("open(dumpPath", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("write(fd", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("close(fd)", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("S_ISDIR", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("normalizedCursor", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("firstEntryCount", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("entryBytes + normalizedCursor * entrySize", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("extern \"C\" JNIEXPORT", StringComparison.Ordinal) >= 0;
            bool csharpBindingValid =
                arenaSource.IndexOf("DllImport(\"__Internal\"", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("EntryPoint = \"H8_GetAssetSize\"", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("EntryPoint = \"H8_LoadAssetToPointer\"", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("EntryPoint = \"H8_WriteTelemetryDump\"", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("private const int AndroidAssetCompressed = -6;", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("blobBytes == AndroidAssetCompressed", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal) < 0;
            bool csharpJniExceptionFenceValid =
                arenaSource.IndexOf("TryConsumePendingAndroidJniException()", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("AndroidJNI.ExceptionOccurred()", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("AndroidJNI.ExceptionClear()", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("AndroidJNI.DeleteLocalRef(exception)", StringComparison.Ordinal) >= 0;
            bool writerReleaseRetryCrossPlatformValid =
                arenaSource.IndexOf("private const int DataMonolithWriterReleaseRetryCount = 4;", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("for (int attempt = 0; attempt < DataMonolithWriterReleaseRetryCount; attempt++)", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("Thread.Yield();", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("return vault.ReleaseWriteLock(in handle, owner);\r\n#endif", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("return vault.ReleaseWriteLock(in handle, owner);\n#endif", StringComparison.Ordinal) < 0;
            bool dataVaultDeferredWriterReleaseQueueContract =
                globalDataVaultSource.IndexOf("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("if (kind == DeferredReleaseKindWriter)", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("pending->Kind == DeferredReleaseKindWriter", StringComparison.Ordinal) >= 0 &&
                globalDataVaultSource.IndexOf("pending->Kind == kind", StringComparison.Ordinal) < 0 &&
                globalDataVaultSource.IndexOf("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate", StringComparison.Ordinal) < 0 &&
                globalDataVaultSource.IndexOf("Thread.SpinWait", StringComparison.Ordinal) < 0;
            bool dumpRouteValid =
                arenaSource.IndexOf("Dump_1404.bin", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("Application.persistentDataPath", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("WriteTelemetryDumpAndroid", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("H8_WriteTelemetryDump(", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("System.IO.Path.Combine(Application.persistentDataPath", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("Dump_SHINOBU_103.bin", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("Dump_X_002.bin", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("Dump_1313.bin", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("Dump_1330.bin", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("Dump_DATA_MONOLITH.bin", StringComparison.Ordinal) < 0;
            bool dumpTelemetryReadOnlyOnly =
                arenaSource.IndexOf("private static void DumpTelemetry", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("if (!TryReadTelemetry(out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("private static void DumpTelemetry(H8DataBlobLoadStatus status)\n        {\n            if (!EnsureTelemetry()", StringComparison.Ordinal) < 0 &&
                arenaSource.IndexOf("private static void DumpTelemetry(H8DataBlobLoadStatus status)\r\n        {\r\n            if (!EnsureTelemetry()", StringComparison.Ordinal) < 0;
            bool telemetryDumpChronologicalOrder =
                arenaSource.IndexOf("NormalizeTelemetryCursor", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("int ringIndex = start + i", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("firstEntryCount", StringComparison.Ordinal) >= 0;
            bool nativeDumpMirrorRouteValid =
                nativeSource.IndexOf("H8_WriteTelemetryDumpFile", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("Docs/AgentLogs/Dump_1404.bin", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("Docs/AgentLogs/Dump_1504.bin", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("const bool legacyOk = H8_WriteTelemetryDumpFile", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("const bool agentOk = H8_WriteTelemetryDumpFile", StringComparison.Ordinal) >= 0 &&
                nativeSource.IndexOf("return legacyOk && agentOk;", StringComparison.Ordinal) >= 0;
            bool cmakeValid =
                cmake.IndexOf("add_library(HectonAndroidBridge SHARED", StringComparison.Ordinal) >= 0 &&
                cmake.IndexOf("HectonAndroidAssetBridge.cpp", StringComparison.Ordinal) >= 0 &&
                cmake.IndexOf("target_link_libraries(HectonAndroidBridge", StringComparison.Ordinal) >= 0 &&
                cmake.IndexOf("android", StringComparison.Ordinal) >= 0 &&
                cmake.IndexOf("log", StringComparison.Ordinal) >= 0;
            bool gradleValid =
                gradle.IndexOf("apply plugin: 'com.android.library'", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("apply from: '../shared/common.gradle'", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("minSdk **MINSDK**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("targetSdk **TARGETSDK**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ') + ['h8bin']", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("**IL_CPP_BUILD_SETUP**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("**SOURCE_BUILD_SETUP**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("**EXTERNAL_SOURCES**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("com.android.application", StringComparison.Ordinal) < 0 &&
                gradle.IndexOf("MINSDKVERSION", StringComparison.Ordinal) < 0 &&
                gradle.IndexOf("TARGETSDKVERSION", StringComparison.Ordinal) < 0 &&
                gradle.IndexOf("PACKAGING_OPTIONS", StringComparison.Ordinal) < 0 &&
                gradle.IndexOf("externalNativeBuild", StringComparison.Ordinal) < 0;
            bool sourcePluginAssetImportValid =
                IsDefaultImporterMetaComplete(nativeSourceMeta) &&
                IsDefaultImporterMetaComplete(cmakeMeta) &&
                arenaSource.IndexOf("DllImport(\"__Internal\"", StringComparison.Ordinal) >= 0 &&
                arenaSource.IndexOf("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal) < 0 &&
                gradle.IndexOf("**SOURCE_BUILD_SETUP**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("**EXTERNAL_SOURCES**", StringComparison.Ordinal) >= 0 &&
                gradle.IndexOf("externalNativeBuild", StringComparison.Ordinal) < 0;
            bool androidIl2CppBackend =
                projectSettings.IndexOf("scriptingBackend:\n    Android: 1", StringComparison.Ordinal) >= 0 ||
                projectSettings.IndexOf("scriptingBackend:\r\n    Android: 1", StringComparison.Ordinal) >= 0;
            bool androidArm64PackageValid =
                projectSettings.IndexOf("AndroidTargetArchitectures: 2", StringComparison.Ordinal) >= 0 &&
                projectSettings.IndexOf("AndroidBuildApkPerCpuArchitecture: 0", StringComparison.Ordinal) >= 0 &&
                projectSettings.IndexOf("androidSplitApplicationBinary: 0", StringComparison.Ordinal) >= 0;
            bool androidGameActivityManifestValid =
                projectSettings.IndexOf("androidApplicationEntry: 2", StringComparison.Ordinal) >= 0 &&
                manifest.IndexOf("com.unity3d.player.UnityPlayerGameActivity", StringComparison.Ordinal) >= 0 &&
                manifest.IndexOf("@style/BaseUnityGameActivityTheme", StringComparison.Ordinal) >= 0 &&
                manifest.IndexOf("android:name=\"android.app.lib_name\" android:value=\"game\"", StringComparison.Ordinal) >= 0 &&
                manifest.IndexOf("com.unity3d.player.UnityPlayerActivity", StringComparison.Ordinal) < 0;

            if (nativeBridgeValid && csharpBindingValid && csharpJniExceptionFenceValid && writerReleaseRetryCrossPlatformValid && dataVaultDeferredWriterReleaseQueueContract && dumpRouteValid && dumpTelemetryReadOnlyOnly && telemetryDumpChronologicalOrder && nativeDumpMirrorRouteValid && cmakeValid && gradleValid && sourcePluginAssetImportValid && androidIl2CppBackend && androidArm64PackageValid && androidGameActivityManifestValid)
                return;

            blockerCount++;
            blockers.Append("- Invalid Data Monolith Android source-built native bridge route.")
                .Append(" Required: AAsset native source, DefaultImporter source-plugin metas, exact-size read guard, uncompressed FD-backed h8bin guard, bounded native dump path strings, directory EEXIST/S_ISDIR guard, cursor-rotated telemetry dump, read-only dump path, C# DllImport(\"__Internal\"), raw JNI exception fence, cross-platform DataVault writer-release retry, deferred DataVault writer-release queue acceptance with serialized writer-release de-duplication, owner-local native Dump_1404 plus Dump_1504 mirror routes under persistentDataPath, Android IL2CPP backend, ARM64-only non-split APK package settings, Unity GameActivity manifest, Unity 6000 library Gradle template, IL2CPP source-build placeholders, standalone CMake reference, and h8bin noCompress.")
                .Append('\n');
        }

        private static void AppendMissingSourceRouteBlocker(StringBuilder blockers, ref int blockerCount, string label, string assetPath)
        {
            blockerCount++;
            blockers.Append("- Missing ")
                .Append(label)
                .Append(": ")
                .Append(assetPath)
                .Append('\n');
        }

        private static string ReadProjectText(string assetPath)
        {
            return File.ReadAllText(ToProjectAbsolutePath(assetPath), Encoding.UTF8);
        }

        private static bool AssetFileExists(string assetPath)
        {
            return File.Exists(ToProjectAbsolutePath(assetPath));
        }

        private static bool IsDefaultImporterMetaComplete(string meta)
        {
            return meta.IndexOf("fileFormatVersion: 2", StringComparison.Ordinal) >= 0 &&
                   meta.IndexOf("guid:", StringComparison.Ordinal) >= 0 &&
                   meta.IndexOf("DefaultImporter:", StringComparison.Ordinal) >= 0 &&
                   meta.IndexOf("externalObjects: {}", StringComparison.Ordinal) >= 0 &&
                   meta.IndexOf("assetBundleVariant:", StringComparison.Ordinal) >= 0;
        }

        private static string ToProjectAbsolutePath(string assetPath)
        {
            string normalizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalizedPath));
        }

    }
}
#endif
