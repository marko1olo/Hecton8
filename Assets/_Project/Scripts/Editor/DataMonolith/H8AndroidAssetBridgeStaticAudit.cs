using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace Hecton8.Data.Editor
{
    internal static class H8AndroidAssetBridgeStaticAudit
    {
        private const string ReportPath = "Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json";
        private const string MemoryPath = "Assets/_Project/Scripts/Core/Memory/H8Memory.cs";
        private const string GlobalDataVaultPath = "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs";
        private const string ArenaPath = "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs";
        private const string TypesPath = "Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs";
        private const string NativePath = "Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp";
        private const string CmakePath = "Assets/Plugins/Android/Native/CMakeLists.txt";
        private const string GradlePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string NativePluginMatrixValidatorPath = "Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [MenuItem("Hecton8/Data Monolith/Run Android Asset Bridge Static Audit")]
        private static void RunFromMenu()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root could not be resolved.");

            Run(projectRoot);
        }

        internal static void Run(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
                throw new ArgumentException("Project root is empty.", nameof(projectRoot));

            long start = DateTime.UtcNow.Ticks;
            string memory = ReadRequired(projectRoot, MemoryPath);
            string globalDataVault = ReadRequired(projectRoot, GlobalDataVaultPath);
            string arena = ReadRequired(projectRoot, ArenaPath);
            string types = ReadRequired(projectRoot, TypesPath);
            string native = ReadRequired(projectRoot, NativePath);
            string cmake = ReadRequired(projectRoot, CmakePath);
            string gradle = ReadRequired(projectRoot, GradlePath);
            string manifest = ReadRequired(projectRoot, AndroidManifestPath);
            string nativePluginMatrixValidator = ReadRequired(projectRoot, NativePluginMatrixValidatorPath);
            string projectSettings = ReadRequired(projectRoot, ProjectSettingsPath);

            int leakedAndroidReferences;
            bool androidIsolation = AreAndroidReferencesGuarded(arena, out leakedAndroidReferences);
            bool nativePresence = native.Contains("AAssetManager_open", StringComparison.Ordinal) &&
                                  native.Contains("AAsset_getLength64", StringComparison.Ordinal) &&
                                  native.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                  native.Contains("AAsset_read", StringComparison.Ordinal) &&
                                  native.Contains("AAsset_close", StringComparison.Ordinal) &&
                                  native.Contains("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal) &&
                                  native.Contains("H8_WriteTelemetryDump", StringComparison.Ordinal) &&
                                  native.Contains("open(dumpPath", StringComparison.Ordinal) &&
                                  native.Contains("write(fd", StringComparison.Ordinal) &&
                                  native.Contains("close(fd)", StringComparison.Ordinal) &&
                                  native.Contains("S_ISDIR", StringComparison.Ordinal);
            bool telemetryDumpChronologicalOrder = arena.Contains("NormalizeTelemetryCursor", StringComparison.Ordinal) &&
                                                   arena.Contains("int ringIndex = start + i", StringComparison.Ordinal) &&
                                                   native.Contains("normalizedCursor", StringComparison.Ordinal) &&
                                                   native.Contains("firstEntryCount", StringComparison.Ordinal) &&
                                                   native.Contains("entryBytes + normalizedCursor * entrySize", StringComparison.Ordinal);
            bool dumpTelemetryReadOnlyOnly = arena.Contains("private static void DumpTelemetry", StringComparison.Ordinal) &&
                                             arena.Contains("if (!TryReadTelemetry(out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring", StringComparison.Ordinal) &&
                                             !arena.Contains("private static void DumpTelemetry(H8DataBlobLoadStatus status)\n        {\n            if (!EnsureTelemetry()", StringComparison.Ordinal) &&
                                             !arena.Contains("private static void DumpTelemetry(H8DataBlobLoadStatus status)\r\n        {\r\n            if (!EnsureTelemetry()", StringComparison.Ordinal);
            bool overflowGuard = native.Contains("assetLength < 0 || assetLength != bufferSize", StringComparison.Ordinal);
            bool uncompressedFdBackedAssetGuard = native.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                                  native.Contains("close(fd)", StringComparison.Ordinal) &&
                                                  arena.Contains("private const int AndroidAssetCompressed = -6;", StringComparison.Ordinal) &&
                                                  arena.Contains("blobBytes == AndroidAssetCompressed", StringComparison.Ordinal);
            bool nativeBoundedDumpPath = native.Contains("H8_TryMeasureCString", StringComparison.Ordinal) &&
                                         native.Contains("requiredBytes > static_cast<size_t>(capacity)", StringComparison.Ordinal) &&
                                         !native.Contains("std::strlen", StringComparison.Ordinal);
            bool noNativeHeap = !native.Contains("std::vector", StringComparison.Ordinal) &&
                                !native.Contains("std::string", StringComparison.Ordinal) &&
                                !native.Contains("malloc", StringComparison.Ordinal) &&
                                !native.Contains("new ", StringComparison.Ordinal);
            bool cmakeLinks = cmake.Contains("target_link_libraries(HectonAndroidBridge", StringComparison.Ordinal) &&
                              cmake.Contains("android", StringComparison.Ordinal) &&
                              cmake.Contains("log", StringComparison.Ordinal);
            bool gradleNoCompress = gradle.Contains("noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ') + ['h8bin']", StringComparison.Ordinal);
            bool gradleUnity6000Template = gradle.Contains("apply plugin: 'com.android.library'", StringComparison.Ordinal) &&
                                           gradle.Contains("apply from: '../shared/common.gradle'", StringComparison.Ordinal) &&
                                           gradle.Contains("minSdk **MINSDK**", StringComparison.Ordinal) &&
                                           gradle.Contains("targetSdk **TARGETSDK**", StringComparison.Ordinal) &&
                                           gradle.Contains("}**PACKAGING**", StringComparison.Ordinal);
            bool gradleSourceBuildPlaceholders = gradle.Contains("**IL_CPP_BUILD_SETUP**", StringComparison.Ordinal) &&
                                                 gradle.Contains("**SOURCE_BUILD_SETUP**", StringComparison.Ordinal) &&
                                                 gradle.Contains("**EXTERNAL_SOURCES**", StringComparison.Ordinal);
            bool gradleLegacyTemplateTokensAbsent = !gradle.Contains("com.android.application", StringComparison.Ordinal) &&
                                                    !gradle.Contains("MINSDKVERSION", StringComparison.Ordinal) &&
                                                    !gradle.Contains("TARGETSDKVERSION", StringComparison.Ordinal) &&
                                                    !gradle.Contains("PACKAGING_OPTIONS", StringComparison.Ordinal) &&
                                                    !gradle.Contains("externalNativeBuild", StringComparison.Ordinal);
            bool androidIl2CppBackend = projectSettings.Contains("scriptingBackend:\n    Android: 1", StringComparison.Ordinal) ||
                                        projectSettings.Contains("scriptingBackend:\r\n    Android: 1", StringComparison.Ordinal);
            bool androidGameActivityManifest = projectSettings.Contains("androidApplicationEntry: 2", StringComparison.Ordinal) &&
                                               manifest.Contains("com.unity3d.player.UnityPlayerGameActivity", StringComparison.Ordinal) &&
                                               manifest.Contains("@style/BaseUnityGameActivityTheme", StringComparison.Ordinal) &&
                                               manifest.Contains("android:name=\"android.app.lib_name\" android:value=\"game\"", StringComparison.Ordinal) &&
                                               !manifest.Contains("com.unity3d.player.UnityPlayerActivity", StringComparison.Ordinal);
            bool nativeMatrixBridgeGate = nativePluginMatrixValidator.Contains("RequireAndroidDataMonolithBridgeSourceRoute", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("DataMonolithAndroidNativeSourcePath", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("HectonAndroidAssetBridge.cpp", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("DllImport(\"__Internal\"", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("androidIl2CppBackend", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("androidGameActivityManifestValid", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("H8_WriteTelemetryDump", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("dumpTelemetryReadOnlyOnly", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("telemetryDumpChronologicalOrder", StringComparison.Ordinal) &&
                                          nativePluginMatrixValidator.Contains("h8bin", StringComparison.Ordinal);
            bool mockJniZeroGuard = arena.Contains("assetManager == IntPtr.Zero", StringComparison.Ordinal) &&
                                    arena.Contains("activity == IntPtr.Zero", StringComparison.Ordinal);
            bool pInvokePresent = arena.Contains("DllImport(\"__Internal\"", StringComparison.Ordinal) &&
                                  arena.Contains("CallingConvention.Cdecl", StringComparison.Ordinal) &&
                                  arena.Contains("MarshalAs(UnmanagedType.I1)", StringComparison.Ordinal) &&
                                  arena.Contains("EntryPoint = \"H8_WriteTelemetryDump\"", StringComparison.Ordinal);
            bool explicitJavaVm = arena.Contains("AndroidJNI.GetJavaVM()", StringComparison.Ordinal) &&
                                  native.Contains("void* javaVm", StringComparison.Ordinal);
            bool noJniOnLoadDependency = !native.Contains("JNI_OnLoad", StringComparison.Ordinal) &&
                                         !native.Contains("g_javaVm", StringComparison.Ordinal);
            bool noJniArgumentArray = arena.Contains("CallObjectMethodUnsafe(activity, getAssetsMethod, null)", StringComparison.Ordinal) &&
                                      !arena.Contains("new jvalue[0]", StringComparison.Ordinal);
            bool rawJniClassLookup = arena.Contains("AndroidJNI.FindClass(\"com/unity3d/player/UnityPlayer\")", StringComparison.Ordinal) &&
                                     !arena.Contains("new AndroidJavaClass", StringComparison.Ordinal);
            bool rawJniExceptionFence = arena.Contains("TryConsumePendingAndroidJniException()", StringComparison.Ordinal) &&
                                        arena.Contains("AndroidJNI.ExceptionOccurred()", StringComparison.Ordinal) &&
                                        arena.Contains("AndroidJNI.ExceptionClear()", StringComparison.Ordinal) &&
                                        arena.Contains("AndroidJNI.DeleteLocalRef(exception)", StringComparison.Ordinal);
            bool writerReleaseRetryCrossPlatform = arena.Contains("private const int DataMonolithWriterReleaseRetryCount = 4;", StringComparison.Ordinal) &&
                                                   arena.Contains("for (int attempt = 0; attempt < DataMonolithWriterReleaseRetryCount; attempt++)", StringComparison.Ordinal) &&
                                                   arena.Contains("Thread.Yield();", StringComparison.Ordinal) &&
                                                   !arena.Contains("return vault.ReleaseWriteLock(in handle, owner);\r\n#endif", StringComparison.Ordinal) &&
                                                   !arena.Contains("return vault.ReleaseWriteLock(in handle, owner);\n#endif", StringComparison.Ordinal);
            bool dataVaultDeferredWriterReleaseQueueContract =
                globalDataVault.Contains("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);", StringComparison.Ordinal) &&
                globalDataVault.Contains("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);", StringComparison.Ordinal) &&
                globalDataVault.Contains("if (kind == DeferredReleaseKindWriter)", StringComparison.Ordinal) &&
                globalDataVault.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0", StringComparison.Ordinal) &&
                globalDataVault.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", StringComparison.Ordinal) &&
                globalDataVault.Contains("pending->Kind == DeferredReleaseKindWriter", StringComparison.Ordinal) &&
                !globalDataVault.Contains("pending->Kind == kind", StringComparison.Ordinal) &&
                !globalDataVault.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate", StringComparison.Ordinal) &&
                !globalDataVault.Contains("Thread.SpinWait", StringComparison.Ordinal);
            bool ownerLocalDumpRoute = arena.Contains("Dump_1404.bin", StringComparison.Ordinal) &&
                                       arena.Contains("Application.persistentDataPath", StringComparison.Ordinal) &&
                                       arena.Contains("WriteTelemetryDumpAndroid", StringComparison.Ordinal) &&
                                       arena.Contains("H8_WriteTelemetryDump(", StringComparison.Ordinal) &&
                                       !arena.Contains("System.IO.Path.Combine(Application.persistentDataPath", StringComparison.Ordinal) &&
                                       !arena.Contains("Dump_SHINOBU_103.bin", StringComparison.Ordinal) &&
                                       !arena.Contains("Dump_X_002.bin", StringComparison.Ordinal) &&
                                       !arena.Contains("Dump_1313.bin", StringComparison.Ordinal) &&
                                       !arena.Contains("Dump_1330.bin", StringComparison.Ordinal) &&
                                       !arena.Contains("Dump_DATA_MONOLITH.bin", StringComparison.Ordinal);
            int writeLockAcquireCalls = CountToken(arena, "TryAcquireArenaWriteView(out NativeArray<byte> arena)") - 1;
            int writeLockReleaseCalls = CountToken(arena, "ReleaseArenaWriteView()") - 1;
            int writeLockReleaseFinallyCalls = CountTokenWithPreviousToken(
                arena,
                "writeLockReleased = ReleaseArenaWriteView();",
                "finally",
                96);
            bool payloadInvalidAcquireReleasePresent = arena.Contains("ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);\n            arena = default;", StringComparison.Ordinal) ||
                                                        arena.Contains("ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);\r\n            arena = default;", StringComparison.Ordinal);
            bool payloadWriteLockProof = writeLockAcquireCalls == writeLockReleaseCalls &&
                                         writeLockAcquireCalls >= 4 &&
                                         writeLockReleaseFinallyCalls == writeLockReleaseCalls &&
                                         payloadInvalidAcquireReleasePresent &&
                                         arena.Contains("vault.TryAcquireWriteLock(in _arenaHandle, SystemID.CoreDataVault, out arena)", StringComparison.Ordinal) &&
                                         arena.Contains("ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault)", StringComparison.Ordinal);
            bool mutableResolveRemoved = !arena.Contains("TryRefreshArenaView(", StringComparison.Ordinal);
            int mutableTryResolveHandleReferences = CountToken(arena, "TryResolveHandle(");
            bool mutableTryResolveHandleAbsent = mutableTryResolveHandleReferences == 0;
            int telemetryWriteLockAcquireCalls = CountToken(arena, "vault.TryAcquireWriteLock(in _telemetry");
            int telemetryWriteLockReleaseCalls = CountToken(arena, "ReleaseWriteLockWithRetry(vault, in _telemetry");
            int telemetryReleaseCallFinallyContexts = CountTokenWithPreviousToken(
                arena,
                "ReleaseTelemetryWriteViews();",
                "finally",
                96);
            int telemetryAcquireCleanupFinallyContexts = CountTokenWithPreviousToken(
                arena,
                "ReleaseWriteLockWithRetry(vault, in _telemetryCursorHandle, SystemID.CoreDataVault);",
                "finally",
                512) +
                CountTokenWithPreviousToken(
                    arena,
                    "ReleaseWriteLockWithRetry(vault, in _telemetryHandle, SystemID.CoreDataVault);",
                    "finally",
                    512);
            bool telemetryWriteLockProof = arena.Contains("private static bool TryAcquireTelemetryWriteViews(", StringComparison.Ordinal) &&
                                           arena.Contains("private static bool ReleaseTelemetryWriteViews()", StringComparison.Ordinal) &&
                                           arena.Contains("private static bool TryReadTelemetry(", StringComparison.Ordinal) &&
                                           telemetryWriteLockAcquireCalls == 2 &&
                                           telemetryWriteLockReleaseCalls >= 4 &&
                                           telemetryReleaseCallFinallyContexts >= 1 &&
                                           telemetryAcquireCleanupFinallyContexts >= 2;
            bool telemetryReadOnlyProof = arena.Contains("TryReadOnlyHandle(in _telemetryHandle", StringComparison.Ordinal) &&
                                          arena.Contains("TryReadOnlyHandle(in _telemetryCursorHandle", StringComparison.Ordinal);
            bool dump1404PathPresent = arena.Contains("Dump_1404.bin", StringComparison.Ordinal);
            bool layoutAuditPresent = types.Contains("public static bool ValidateBlittableSizes()", StringComparison.Ordinal) &&
                                      types.Contains("UnsafeUtility.SizeOf<H8DataBlobHeader>() == H8DataLayoutConstants.HeaderSizeBytes", StringComparison.Ordinal) &&
                                      types.Contains("UnsafeUtility.SizeOf<H8DataBlobDirectory>() == H8DataLayoutConstants.DirectorySizeBytes", StringComparison.Ordinal) &&
                                      types.Contains("UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>() == H8DataLayoutConstants.TelemetryEntrySize", StringComparison.Ordinal);
            bool telemetryLayoutExplicit = types.Contains("[StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.TelemetryEntrySize)]", StringComparison.Ordinal) &&
                                           types.Contains("[FieldOffset(0)] public ulong Checksum64", StringComparison.Ordinal) &&
                                           types.Contains("[FieldOffset(8)] public long LoadTicks", StringComparison.Ordinal) &&
                                           types.Contains("[FieldOffset(16)] public long IoTicks", StringComparison.Ordinal) &&
                                           types.Contains("[FieldOffset(60)] public uint Reserved3", StringComparison.Ordinal);
            int explicitStructLayoutDeclarationCount = CountToken(types, "[StructLayout(LayoutKind.Explicit");
            int fieldOffsetDeclarationCount = CountToken(types, "[FieldOffset(");
            int unsafeSizeOfCheckCount = CountToken(types, "UnsafeUtility.SizeOf<");
            bool dataVaultBufferIdsPresent = memory.Contains("DataMonolithPayload = 71103", StringComparison.Ordinal) &&
                                             memory.Contains("DataMonolithTelemetryRing = 71104", StringComparison.Ordinal) &&
                                             memory.Contains("DataMonolithTelemetryCursor = 71105", StringComparison.Ordinal);
            long elapsedMicroseconds = (DateTime.UtcNow.Ticks - start) / 10L;

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            AppendJson(builder, "agentId", "1404", true);
            AppendJson(builder, "evidenceClass", "STATIC_SOURCE", true);
            AppendJson(builder, "status", "PENDING_ANDROID_PLAYER_BUILD", true);
            AppendJson(builder, "memoryPath", MemoryPath, true);
            AppendJson(builder, "globalDataVaultPath", GlobalDataVaultPath, true);
            AppendJson(builder, "csharpArenaPath", ArenaPath, true);
            AppendJson(builder, "csharpTypesPath", TypesPath, true);
            AppendJson(builder, "nativePluginPath", NativePath, true);
            AppendJson(builder, "cmakePath", CmakePath, true);
            AppendJson(builder, "gradlePath", GradlePath, true);
            AppendJson(builder, "androidManifestPath", AndroidManifestPath, true);
            AppendJson(builder, "nativePluginMatrixValidatorPath", NativePluginMatrixValidatorPath, true);
            AppendJson(builder, "projectSettingsPath", ProjectSettingsPath, true);
            AppendJson(builder, "nativeLinkageMode", "Unity Android IL2CPP source plugin via DllImport(\"__Internal\"); CMake kept as standalone native reference, not active Gradle root", true);
            AppendJson(builder, "nativeAssetApisPresent", nativePresence, true);
            AppendJson(builder, "nativeDumpDirectoryIsDirectoryCheckPresent", native.Contains("S_ISDIR", StringComparison.Ordinal), true);
            AppendJson(builder, "bufferOverflowGuardPresent", overflowGuard, true);
            AppendJson(builder, "uncompressedFdBackedAssetGuardPresent", uncompressedFdBackedAssetGuard, true);
            AppendJson(builder, "nativeBoundedDumpPathPresent", nativeBoundedDumpPath, true);
            AppendJson(builder, "telemetryDumpChronologicalOrderPresent", telemetryDumpChronologicalOrder, true);
            AppendJson(builder, "dumpTelemetryReadOnlyOnly", dumpTelemetryReadOnlyOnly, true);
            AppendJson(builder, "nativeHeapAllocationTokensAbsent", noNativeHeap, true);
            AppendJson(builder, "pInvokeDeclarationsPresent", pInvokePresent, true);
            AppendJson(builder, "androidReferencesGuarded", androidIsolation, true);
            AppendJson(builder, "leakedAndroidReferenceCount", leakedAndroidReferences, true);
            AppendJson(builder, "mockJniZeroPointerGuardPresent", mockJniZeroGuard, true);
            AppendJson(builder, "androidJavaVmPassedExplicitly", explicitJavaVm, true);
            AppendJson(builder, "nativeJniOnLoadDependencyAbsent", noJniOnLoadDependency, true);
            AppendJson(builder, "jniNoArgumentArrayAllocationAbsent", noJniArgumentArray, true);
            AppendJson(builder, "rawJniFindClassWithoutAndroidJavaClass", rawJniClassLookup, true);
            AppendJson(builder, "rawJniPendingExceptionFencePresent", rawJniExceptionFence, true);
            AppendJson(builder, "writerReleaseRetryCrossPlatformPresent", writerReleaseRetryCrossPlatform, true);
            AppendJson(builder, "globalDataVaultDeferredWriterReleaseQueueContract", dataVaultDeferredWriterReleaseQueueContract, true);
            AppendJson(builder, "ownerLocalDump1404OnlyRoutePresent", ownerLocalDumpRoute, true);
            AppendJson(builder, "androidReleaseNativeDumpRoutePresent", ownerLocalDumpRoute && nativePresence, true);
            AppendJson(builder, "androidReleaseManagedDumpIoAbsent", !arena.Contains("System.IO.Path.Combine(Application.persistentDataPath", StringComparison.Ordinal), true);
            AppendJson(builder, "payloadWriteLockAcquireCallCount", writeLockAcquireCalls, true);
            AppendJson(builder, "payloadWriteLockReleaseCallCount", writeLockReleaseCalls, true);
            AppendJson(builder, "payloadWriteLockReleaseFinallyCallCount", writeLockReleaseFinallyCalls, true);
            AppendJson(builder, "payloadInvalidAcquireReleasePresent", payloadInvalidAcquireReleasePresent, true);
            AppendJson(builder, "payloadWriteLockFinallyProofPresent", payloadWriteLockProof, true);
            AppendJson(builder, "payloadMutableResolveHelperRemoved", mutableResolveRemoved, true);
            AppendJson(builder, "tryResolveHandleReferenceCount", mutableTryResolveHandleReferences, true);
            AppendJson(builder, "mutableTryResolveHandleAbsent", mutableTryResolveHandleAbsent, true);
            AppendJson(builder, "payloadBufferId", "BufferID.DataMonolithPayload", true);
            AppendJson(builder, "payloadBufferNumericId", 71103L, true);
            AppendJson(builder, "telemetryRingBufferId", "BufferID.DataMonolithTelemetryRing", true);
            AppendJson(builder, "telemetryRingBufferNumericId", 71104L, true);
            AppendJson(builder, "telemetryCursorBufferId", "BufferID.DataMonolithTelemetryCursor", true);
            AppendJson(builder, "telemetryCursorBufferNumericId", 71105L, true);
            AppendJson(builder, "dataVaultBufferIdsPresent", dataVaultBufferIdsPresent, true);
            AppendJson(builder, "telemetryWriteLockAcquireCallCount", telemetryWriteLockAcquireCalls, true);
            AppendJson(builder, "telemetryWriteLockReleaseCallCount", telemetryWriteLockReleaseCalls, true);
            AppendJson(builder, "telemetryReleaseCallFinallyContextCount", telemetryReleaseCallFinallyContexts, true);
            AppendJson(builder, "telemetryAcquireCleanupFinallyContextCount", telemetryAcquireCleanupFinallyContexts, true);
            AppendJson(builder, "telemetryWriteLockFinallyProofPresent", telemetryWriteLockProof, true);
            AppendJson(builder, "telemetryReadOnlyResolveProofPresent", telemetryReadOnlyProof, true);
            AppendJson(builder, "dump1404PathPresent", dump1404PathPresent, true);
            AppendJson(builder, "layoutAuditValidateBlittableSizesPresent", layoutAuditPresent, true);
            AppendJson(builder, "telemetryEntryExplicitLayoutProofPresent", telemetryLayoutExplicit, true);
            AppendJson(builder, "explicitStructLayoutDeclarationCount", explicitStructLayoutDeclarationCount, true);
            AppendJson(builder, "fieldOffsetDeclarationCount", fieldOffsetDeclarationCount, true);
            AppendJson(builder, "unsafeSizeOfCheckCount", unsafeSizeOfCheckCount, true);
            AppendJson(builder, "telemetryEntryLayoutBytes", "Size=64; Checksum64@0 u64; LoadTicks@8 i64; IoTicks@16 i64; FrameIndex@24 u32; BlobBytes@28 u32; SectionCount@32 u32; LoadStatus@36 u32; PathFlags@40 u32; StateHash@44 u32; Reserved0@48 u32; Reserved1@52 u32; Reserved2@56 u32; Reserved3@60 u32", true);
            AppendJson(builder, "cmakeLinksAndroidAndLog", cmakeLinks, true);
            AppendJson(builder, "h8binNoCompressConfigured", gradleNoCompress, true);
            AppendJson(builder, "gradleUnity6000LibraryTemplatePresent", gradleUnity6000Template, true);
            AppendJson(builder, "gradleUnityIl2CppSourceBuildPlaceholdersPresent", gradleSourceBuildPlaceholders, true);
            AppendJson(builder, "gradleLegacyApplicationTemplateTokensAbsent", gradleLegacyTemplateTokensAbsent, true);
            AppendJson(builder, "androidIl2CppBackendSerialized", androidIl2CppBackend, true);
            AppendJson(builder, "androidGameActivityManifestPresent", androidGameActivityManifest, true);
            AppendJson(builder, "nativePluginMatrixDataMonolithAndroidGatePresent", nativeMatrixBridgeGate, true);
            AppendJson(builder, "staticScanMicroseconds", elapsedMicroseconds, true);
            AppendJson(builder, "memorySha256", Sha256File(projectRoot, MemoryPath), true);
            AppendJson(builder, "globalDataVaultSha256", Sha256File(projectRoot, GlobalDataVaultPath), true);
            AppendJson(builder, "csharpSha256", Sha256File(projectRoot, ArenaPath), true);
            AppendJson(builder, "csharpTypesSha256", Sha256File(projectRoot, TypesPath), true);
            AppendJson(builder, "nativeSha256", Sha256File(projectRoot, NativePath), true);
            AppendJson(builder, "cmakeSha256", Sha256File(projectRoot, CmakePath), true);
            AppendJson(builder, "gradleSha256", Sha256File(projectRoot, GradlePath), true);
            AppendJson(builder, "androidManifestSha256", Sha256File(projectRoot, AndroidManifestPath), true);
            AppendJson(builder, "nativePluginMatrixValidatorSha256", Sha256File(projectRoot, NativePluginMatrixValidatorPath), true);
            AppendJson(builder, "editorAuditSha256", Sha256File(projectRoot, "Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridgeStaticAudit.cs"), false);
            builder.AppendLine("}");

            string reportAbsolutePath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportAbsolutePath));
            File.WriteAllText(reportAbsolutePath, builder.ToString(), Encoding.UTF8);
        }

        private static string ReadRequired(string projectRoot, string relativePath)
        {
            string absolutePath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException(relativePath, absolutePath);

            return File.ReadAllText(absolutePath, Encoding.UTF8);
        }

        private static bool AreAndroidReferencesGuarded(string source, out int leakCount)
        {
            leakCount = 0;
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            bool[] androidGuardStack = new bool[64];
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    if (depth < androidGuardStack.Length)
                        androidGuardStack[depth++] = IsAndroidGuard(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("#elif ", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        androidGuardStack[depth - 1] = IsAndroidGuard(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        androidGuardStack[depth - 1] = false;
                    continue;
                }

                if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        androidGuardStack[--depth] = false;
                    continue;
                }

                if (ContainsAndroidOnlyToken(lines[i]) && !IsInsideAndroidGuard(androidGuardStack, depth))
                    leakCount++;
            }

            return leakCount == 0;
        }

        private static bool IsAndroidGuard(string directive)
        {
            return ContainsPositiveDirectiveSymbol(directive, "UNITY_ANDROID") &&
                   directive.Contains("!UNITY_EDITOR", StringComparison.Ordinal);
        }

        private static bool ContainsPositiveDirectiveSymbol(string directive, string symbol)
        {
            int index = 0;
            while (index < directive.Length)
            {
                int found = directive.IndexOf(symbol, index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                int end = found + symbol.Length;
                bool symbolStart = found == 0 || !IsDirectiveIdentifierChar(directive[found - 1]);
                bool symbolEnd = end >= directive.Length || !IsDirectiveIdentifierChar(directive[end]);
                if (symbolStart && symbolEnd)
                {
                    int previous = found - 1;
                    while (previous >= 0 && char.IsWhiteSpace(directive[previous]))
                        previous--;

                    if (previous < 0 || directive[previous] != '!')
                        return true;
                }

                index = end;
            }

            return false;
        }

        private static bool IsDirectiveIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsInsideAndroidGuard(bool[] stack, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                if (stack[i])
                    return true;
            }

            return false;
        }

        private static bool ContainsAndroidOnlyToken(string line)
        {
            return line.Contains("AndroidJavaClass", StringComparison.Ordinal) ||
                   line.Contains("AndroidJavaObject", StringComparison.Ordinal) ||
                   line.Contains("AndroidJNI.", StringComparison.Ordinal) ||
                   line.Contains("DllImport(\"__Internal\"", StringComparison.Ordinal) ||
                   line.Contains("H8_GetAssetSize(", StringComparison.Ordinal) ||
                   line.Contains("H8_LoadAssetToPointer(", StringComparison.Ordinal) ||
                   line.Contains("H8_WriteTelemetryDump(", StringComparison.Ordinal);
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static int CountTokenWithPreviousToken(string text, string token, string previousToken, int maxDistance)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                int windowStart = Math.Max(0, found - maxDistance);
                int windowLength = found - windowStart;
                if (text.IndexOf(previousToken, windowStart, windowLength, StringComparison.Ordinal) >= 0)
                    count++;

                index = found + token.Length;
            }

            return count;
        }

        private static string Sha256File(string projectRoot, string relativePath)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = new FileStream(
                Path.Combine(projectRoot, relativePath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.SequentialScan);
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(EscapeJson(key)).Append("\": \"").Append(EscapeJson(value)).Append('"');
            AppendComma(builder, comma);
        }

        private static void AppendJson(StringBuilder builder, string key, bool value, bool comma)
        {
            builder.Append("  \"").Append(EscapeJson(key)).Append("\": ").Append(value ? "true" : "false");
            AppendComma(builder, comma);
        }

        private static void AppendJson(StringBuilder builder, string key, long value, bool comma)
        {
            builder.Append("  \"").Append(EscapeJson(key)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            AppendComma(builder, comma);
        }

        private static void AppendComma(StringBuilder builder, bool comma)
        {
            if (comma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
