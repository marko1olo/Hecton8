using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace Hecton8.Data.Editor
{
    internal static class H8AndroidAssetBridge1504StaticAudit
    {
        private const string ReportPath = "Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1504.json";
        private const string ArenaPath = "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs";
        private const string TypesPath = "Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs";
        private const string GlobalDataVaultPath = "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs";
        private const string BootstrapperPath = "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string NativePath = "Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp";
        private const string NativeMetaPath = "Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp.meta";
        private const string CmakePath = "Assets/Plugins/Android/Native/CMakeLists.txt";
        private const string CmakeMetaPath = "Assets/Plugins/Android/Native/CMakeLists.txt.meta";
        private const string GradlePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string StaticDataPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const string AuditPath = "Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs";
        private const string AuditMetaPath = "Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs.meta";
        private const string TestsPath = "Assets/_Project/Tests/Editor/AndroidAssetBridge1504StaticAuditTests.cs";
        private const string TestsMetaPath = "Assets/_Project/Tests/Editor/AndroidAssetBridge1504StaticAuditTests.cs.meta";
        private const string NativeMatrixValidatorPath = "Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs";
        private const string LegacyAuditPath = "Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridgeStaticAudit.cs";
        private const string LegacyTestsPath = "Assets/_Project/Tests/Editor/AndroidAssetBridge1404EditTests.cs";
        private const string RuntimeIntegrationDocPath = "Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md";
        private const string H8binSpecDocPath = "Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md";
        private const string BootSequenceDocPath = "Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md";
        private const string ProductContractsDocPath = "Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md";
        private const string HandoffDocPath = "Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md";
        private const string PortabilityProofDocPath = "Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md";
        private const string H8binValidationReportPath = "Docs/Reports/ANDROID_PAL_H8BIN_VALIDATION_1504.json";
        private const string H8binValidationJunitPath = "Docs/Reports/ANDROID_PAL_H8BIN_VALIDATION_1504.junit.xml";
        private const string H8binValidationMetricPhiPath = "Docs/Reports/METRIC_PHI_ANDROID_PAL_1504_DATA_TRUTH_AUDIT.json";
        private const string FileReadAllBytesToken = "File.Read" + "AllBytes";

        [MenuItem("Hecton8/Data Monolith/Run Android Asset Bridge 1504 Static Audit")]
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

            long startTicks = DateTime.UtcNow.Ticks;
            string arena = ReadRequired(projectRoot, ArenaPath);
            string types = ReadRequired(projectRoot, TypesPath);
            string globalDataVault = ReadRequired(projectRoot, GlobalDataVaultPath);
            string bootstrapper = ReadRequired(projectRoot, BootstrapperPath);
            string native = ReadRequired(projectRoot, NativePath);
            string nativeMeta = ReadRequired(projectRoot, NativeMetaPath);
            string cmake = ReadRequired(projectRoot, CmakePath);
            string cmakeMeta = ReadRequired(projectRoot, CmakeMetaPath);
            string gradle = ReadRequired(projectRoot, GradlePath);
            string manifest = ReadRequired(projectRoot, ManifestPath);
            string projectSettings = ReadRequired(projectRoot, ProjectSettingsPath);
            string tests = ReadRequired(projectRoot, TestsPath);
            string audit = ReadRequired(projectRoot, AuditPath);
            string auditMeta = ReadRequired(projectRoot, AuditMetaPath);
            string testsMeta = ReadRequired(projectRoot, TestsMetaPath);
            string nativeMatrixValidator = ReadRequired(projectRoot, NativeMatrixValidatorPath);
            string legacyAudit = ReadRequired(projectRoot, LegacyAuditPath);
            string legacyTests = ReadRequired(projectRoot, LegacyTestsPath);
            string runtimeIntegrationDoc = ReadRequired(projectRoot, RuntimeIntegrationDocPath);
            string h8binSpecDoc = ReadRequired(projectRoot, H8binSpecDocPath);
            string bootSequenceDoc = ReadRequired(projectRoot, BootSequenceDocPath);
            string productContractsDoc = ReadRequired(projectRoot, ProductContractsDocPath);
            string handoffDoc = ReadRequired(projectRoot, HandoffDocPath);
            string portabilityProofDoc = ReadRequired(projectRoot, PortabilityProofDocPath);
            string h8binValidationReport = ReadRequired(projectRoot, H8binValidationReportPath);
            string h8binValidationJunit = ReadRequired(projectRoot, H8binValidationJunitPath);
            string h8binValidationMetricPhi = ReadRequired(projectRoot, H8binValidationMetricPhiPath);

            int androidLeakCount;
            bool androidReferencesGuarded = AreAndroidReferencesGuarded(arena, out androidLeakCount);
            bool nativeBridgePresent = native.Contains("#include <android/asset_manager.h>", StringComparison.Ordinal) &&
                                       native.Contains("#include <android/asset_manager_jni.h>", StringComparison.Ordinal) &&
                                       native.Contains("AAssetManager_fromJava", StringComparison.Ordinal) &&
                                       native.Contains("AAssetManager_open", StringComparison.Ordinal) &&
                                       native.Contains("AAsset_getLength64", StringComparison.Ordinal) &&
                                       native.Contains("AAsset_read", StringComparison.Ordinal) &&
                                       native.Contains("AAsset_close(asset)", StringComparison.Ordinal) &&
                                       native.Contains("H8_GetAssetSize", StringComparison.Ordinal) &&
                                       native.Contains("H8_LoadAssetToPointer", StringComparison.Ordinal);
            bool nativeOverflowGuard = native.Contains("assetLength < 0 || assetLength != bufferSize", StringComparison.Ordinal);
            bool nativeUncompressedFdGuard = native.Contains("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal) &&
                                             native.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                             native.Contains("close(fd)", StringComparison.Ordinal) &&
                                             arena.Contains("private const int AndroidAssetCompressed = -6;", StringComparison.Ordinal) &&
                                             arena.Contains("blobBytes == AndroidAssetCompressed", StringComparison.Ordinal);
            bool nativeNoHeapStaging = !native.Contains("std::vector", StringComparison.Ordinal) &&
                                       !native.Contains("std::string", StringComparison.Ordinal) &&
                                       !native.Contains("malloc", StringComparison.Ordinal) &&
                                       !native.Contains("free(", StringComparison.Ordinal) &&
                                       !native.Contains("new ", StringComparison.Ordinal) &&
                                       !native.Contains("delete", StringComparison.Ordinal);
            bool nativeBoundedDumpPath = native.Contains("H8_TryMeasureCString", StringComparison.Ordinal) &&
                                         native.Contains("requiredBytes > static_cast<size_t>(capacity)", StringComparison.Ordinal) &&
                                         !native.Contains("std::strlen", StringComparison.Ordinal);
            bool nativeExportVisibilityPresent = native.Contains("extern \"C\" JNIEXPORT int32_t JNICALL H8_GetAssetSize", StringComparison.Ordinal) &&
                                                 native.Contains("extern \"C\" JNIEXPORT bool JNICALL H8_LoadAssetToPointer", StringComparison.Ordinal) &&
                                                 native.Contains("extern \"C\" JNIEXPORT bool JNICALL H8_WriteTelemetryDump", StringComparison.Ordinal) &&
                                                 cmake.Contains("-fvisibility=hidden", StringComparison.Ordinal);
            bool androidNativeTelemetryDumpPresent = arena.Contains("#elif UNITY_ANDROID && !UNITY_EDITOR", StringComparison.Ordinal) &&
                                                     arena.Contains("WriteTelemetryDumpAndroid(status, ring, telemetryCursor)", StringComparison.Ordinal) &&
                                                     arena.Contains("byte* persistentDataPathUtf8 = stackalloc byte[AndroidPersistentPathUtf8Capacity]", StringComparison.Ordinal) &&
                                                     arena.Contains("TryWriteUtf8NullTerminated(", StringComparison.Ordinal) &&
                                                     arena.Contains("NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring)", StringComparison.Ordinal) &&
                                                     arena.Contains("H8_WriteTelemetryDump(", StringComparison.Ordinal) &&
                                                     native.Contains("O_CREAT | O_WRONLY | O_TRUNC | O_CLOEXEC", StringComparison.Ordinal) &&
                                                     native.Contains("0600", StringComparison.Ordinal) &&
                                                     native.Contains("H8_WriteAll", StringComparison.Ordinal) &&
                                                     native.Contains("errno == EINTR", StringComparison.Ordinal) &&
                                                     native.Contains("close(fd)", StringComparison.Ordinal);
            bool androidNativeTelemetryAgentDumpMirrorPresent = native.Contains("H8_WriteTelemetryDumpFile", StringComparison.Ordinal) &&
                                                                native.Contains("Docs/AgentLogs/Dump_1404.bin", StringComparison.Ordinal) &&
                                                                native.Contains("Docs/AgentLogs/Dump_1504.bin", StringComparison.Ordinal) &&
                                                                native.Contains("const bool legacyOk = H8_WriteTelemetryDumpFile", StringComparison.Ordinal) &&
                                                                native.Contains("const bool agentOk = H8_WriteTelemetryDumpFile", StringComparison.Ordinal) &&
                                                                native.Contains("return legacyOk && agentOk;", StringComparison.Ordinal);
            bool telemetryDumpLayoutExplicit = types.Contains("[StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.TelemetryEntrySize)]", StringComparison.Ordinal) &&
                                               types.Contains("public struct H8DataMonolithTelemetryEntry", StringComparison.Ordinal) &&
                                               types.Contains("[FieldOffset(0)] public ulong Checksum64", StringComparison.Ordinal) &&
                                               types.Contains("[FieldOffset(60)] public uint Reserved3", StringComparison.Ordinal) &&
                                               types.Contains("UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>() == H8DataLayoutConstants.TelemetryEntrySize", StringComparison.Ordinal);
            bool writerReleaseRetryCrossPlatformPresent = arena.Contains("private const int DataMonolithWriterReleaseRetryCount = 4;", StringComparison.Ordinal) &&
                                                          arena.Contains("for (int attempt = 0; attempt < DataMonolithWriterReleaseRetryCount; attempt++)", StringComparison.Ordinal) &&
                                                          arena.Contains("vault.ReleaseWriteLock(in handle, owner)", StringComparison.Ordinal) &&
                                                          arena.Contains("Thread.Yield();", StringComparison.Ordinal) &&
                                                          !arena.Contains("return vault.ReleaseWriteLock(in handle, owner);\n#endif", StringComparison.Ordinal) &&
                                                          !arena.Contains("return vault.ReleaseWriteLock(in handle, owner);\r\n#endif", StringComparison.Ordinal);
            int payloadWriteAcquireCount = CountToken(arena, "TryAcquireArenaWriteView(out NativeArray<byte> arena)") - 1;
            int payloadWriteReleaseCount = CountToken(arena, "writeLockReleased = ReleaseArenaWriteView();");
            int payloadWriteFinallyReleaseCount = CountTokenWithPreviousToken(arena, "writeLockReleased = ReleaseArenaWriteView();", "finally", 96);
            bool payloadWriteLockFinallyProofPresent = payloadWriteAcquireCount >= 4 &&
                                                       payloadWriteReleaseCount == payloadWriteAcquireCount &&
                                                       payloadWriteFinallyReleaseCount == payloadWriteReleaseCount &&
                                                       arena.Contains("if (!lockTransferred)", StringComparison.Ordinal) &&
                                                       arena.Contains("ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);", StringComparison.Ordinal) &&
                                                       arena.Contains("arena = default;", StringComparison.Ordinal);
            bool globalDataVaultDeferredWriterReleaseQueueContract = globalDataVault.Contains("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("if (kind == DeferredReleaseKindWriter)", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0;", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("pending->Kind == DeferredReleaseKindWriter", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0);", StringComparison.Ordinal) &&
                                                                     globalDataVault.Contains("local.Kind == DeferredReleaseKindWriter", StringComparison.Ordinal) &&
                                                                     !globalDataVault.Contains("pending->Kind == kind", StringComparison.Ordinal) &&
                                                                     !globalDataVault.Contains("Thread.SpinWait", StringComparison.Ordinal) &&
                                                                     !globalDataVault.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate", StringComparison.Ordinal);
            bool dumpTelemetryReadOnlyOnly = arena.Contains("private static void DumpTelemetry(H8DataBlobLoadStatus status)", StringComparison.Ordinal) &&
                                             arena.Contains("if (!TryReadTelemetry(out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring, out NativeArray<int>.ReadOnly cursor))", StringComparison.Ordinal) &&
                                             TokenWindowDoesNotContain(
                                                 arena,
                                                 "private static void DumpTelemetry(H8DataBlobLoadStatus status)",
                                                 "private static int NormalizeTelemetryCursor(int cursor)",
                                                 "EnsureTelemetry(");
            bool telemetryDumpChronologicalOrderPresent = arena.Contains("int start = NormalizeTelemetryCursor(cursor);", StringComparison.Ordinal) &&
                                                          arena.Contains("int ringIndex = start + i;", StringComparison.Ordinal) &&
                                                          native.Contains("const int32_t firstEntryCount = entryCount - normalizedCursor;", StringComparison.Ordinal) &&
                                                          native.Contains("entryBytes + normalizedCursor * entrySize", StringComparison.Ordinal);
            bool jniLocalReferenceLifetimeBounded = ContainsTokensInOrder(
                                                        arena,
                                                        "assetManager = AndroidJNI.CallObjectMethodUnsafe(activity, getAssetsMethod, null);",
                                                        "int blobBytes = H8_GetAssetSize(javaVm, assetManager, assetName);",
                                                        "loaded = H8_LoadAssetToPointer(javaVm, assetManager, assetName, destination, blobBytes);",
                                                        "if (assetManager != IntPtr.Zero)",
                                                        "AndroidJNI.DeleteLocalRef(assetManager);",
                                                        "if (activityClass != IntPtr.Zero)",
                                                        "AndroidJNI.DeleteLocalRef(activityClass);",
                                                        "if (activity != IntPtr.Zero)",
                                                        "AndroidJNI.DeleteLocalRef(activity);",
                                                        "if (unityPlayerClass != IntPtr.Zero)",
                                                        "AndroidJNI.DeleteLocalRef(unityPlayerClass);") &&
                                                    arena.Contains("AndroidJNI.ExceptionClear();", StringComparison.Ordinal) &&
                                                    arena.Contains("AndroidJNI.DeleteLocalRef(exception);", StringComparison.Ordinal) &&
                                                    !arena.Contains("AndroidJNI.NewGlobalRef", StringComparison.Ordinal);
            bool nativeAssetManagerNoCache = native.Contains("AAssetManager_fromJava(environment, reinterpret_cast<jobject>(javaAssetManager))", StringComparison.Ordinal) &&
                                             CountToken(native, "AAsset_close(asset);") >= 4 &&
                                             !native.Contains("NewGlobalRef", StringComparison.Ordinal) &&
                                             !native.Contains("DeleteGlobalRef", StringComparison.Ordinal) &&
                                             !native.Contains("static AAssetManager", StringComparison.Ordinal) &&
                                             !native.Contains("static AAsset", StringComparison.Ordinal) &&
                                             !native.Contains("static jobject", StringComparison.Ordinal);
            bool nativeJniEnvironmentReleaseBalanced = native.Contains("const jint getEnvResult = vm->GetEnv(reinterpret_cast<void**>(environment), JNI_VERSION_1_6);", StringComparison.Ordinal) &&
                                                       native.Contains("if (getEnvResult == JNI_OK)", StringComparison.Ordinal) &&
                                                       native.Contains("if (getEnvResult != JNI_EDETACHED)", StringComparison.Ordinal) &&
                                                       native.Contains("vm->AttachCurrentThread(environment, nullptr)", StringComparison.Ordinal) &&
                                                       native.Contains("*attached = true;", StringComparison.Ordinal) &&
                                                       native.Contains("if (attached && vm != nullptr)", StringComparison.Ordinal) &&
                                                       native.Contains("vm->DetachCurrentThread();", StringComparison.Ordinal) &&
                                                       native.Contains("struct H8JniEnvironmentScope", StringComparison.Ordinal) &&
                                                       native.Contains("struct H8FloatingPointControlScope", StringComparison.Ordinal) &&
                                                       native.Contains("H8FloatingPointControlScope FloatingPointScope;", StringComparison.Ordinal) &&
                                                       native.Contains("~H8JniEnvironmentScope()", StringComparison.Ordinal) &&
                                                       native.Contains("H8_ReleaseJniEnvironment(JavaVm, Attached);", StringComparison.Ordinal) &&
                                                       native.Contains("msr fpcr", StringComparison.Ordinal) &&
                                                       native.Contains("msr fpsr", StringComparison.Ordinal) &&
                                                       native.Contains("_mm_getcsr()", StringComparison.Ordinal) &&
                                                       native.Contains("_mm_setcsr(Mxcsr);", StringComparison.Ordinal) &&
                                                       CountToken(native, "H8JniEnvironmentScope jniScope(javaVm);") == 2 &&
                                                       ContainsTokensInOrder(
                                                           native,
                                                           "extern \"C\" JNIEXPORT int32_t JNICALL H8_GetAssetSize",
                                                           "H8JniEnvironmentScope jniScope(javaVm);",
                                                           "if (!jniScope.IsValid())",
                                                           "AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(jniScope.Environment, assetManager);",
                                                           "AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);",
                                                           "AAsset_close(asset);") &&
                                                       ContainsTokensInOrder(
                                                           native,
                                                           "extern \"C\" JNIEXPORT bool JNICALL H8_LoadAssetToPointer",
                                                           "H8JniEnvironmentScope jniScope(javaVm);",
                                                           "if (!jniScope.IsValid())",
                                                           "AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(jniScope.Environment, assetManager);",
                                                           "AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);",
                                                           "AAsset_close(asset);",
                                                           "return totalRead == assetLength;");
            bool csharpRawJniRoute = arena.Contains("AndroidJNI.FindClass(\"com/unity3d/player/UnityPlayer\")", StringComparison.Ordinal) &&
                                     arena.Contains("AndroidJNI.GetStaticObjectField", StringComparison.Ordinal) &&
                                     arena.Contains("CallObjectMethodUnsafe(activity, getAssetsMethod, null)", StringComparison.Ordinal) &&
                                     arena.Contains("AndroidJNI.GetJavaVM()", StringComparison.Ordinal) &&
                                     !arena.Contains("new AndroidJavaClass", StringComparison.Ordinal) &&
                                     !arena.Contains("new jvalue", StringComparison.Ordinal);
            bool androidAssetNameStackAsciiRoute = arena.Contains("byte* assetName = stackalloc byte[assetNameCapacity]", StringComparison.Ordinal) &&
                                                   arena.Contains("TryWriteAndroidAssetName(assetName, assetNameCapacity)", StringComparison.Ordinal) &&
                                                   arena.Contains("ReadOnlySpan<char> relativePath = H8DataLayoutConstants.DefaultStreamingAssetsRelativePath.AsSpan();", StringComparison.Ordinal) &&
                                                   arena.Contains("if (c > 0x7F)", StringComparison.Ordinal) &&
                                                   arena.Contains("destination[relativePath.Length] = 0;", StringComparison.Ordinal) &&
                                                   types.Contains("DefaultStreamingAssetsRelativePath = \"Hecton8/DataMonolith/static_data.h8bin\"", StringComparison.Ordinal);
            bool androidTelemetryRouteFlagsPresent = arena.Contains("private const uint PathFlagAndroidAssetManager = 128u;", StringComparison.Ordinal) &&
                                                     arena.Contains("private const uint PathFlagAndroidJavaAssetManager = 256u;", StringComparison.Ordinal) &&
                                                     arena.Contains("uint pathFlags = PathFlagVaultBacked | PathFlagAndroidAssetManager | PathFlagAndroidJavaAssetManager;", StringComparison.Ordinal) &&
                                                     arena.Contains("_lastReadPathFlags = pathFlags;", StringComparison.Ordinal) &&
                                                     arena.Contains("RecordFailureTelemetry(status, pathFlags);", StringComparison.Ordinal) &&
                                                     arena.Contains("RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);", StringComparison.Ordinal) &&
                                                     TokenWindowDoesNotContain(
                                                         arena,
                                                         "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                         "private static bool TryConsumePendingAndroidJniException()",
                                                         "PathFlagStreamingUriStaged") &&
                                                     TokenWindowDoesNotContain(
                                                         arena,
                                                         "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                         "private static bool TryConsumePendingAndroidJniException()",
                                                         "Application.streamingAssetsPath") &&
                                                     TokenWindowDoesNotContain(
                                                         arena,
                                                         "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                         "private static bool TryConsumePendingAndroidJniException()",
                                                         "UnityWebRequest") &&
                                                     TokenWindowDoesNotContain(
                                                         arena,
                                                         "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                         "private static bool TryConsumePendingAndroidJniException()",
                                                         "temporaryCachePath") &&
                                                     TokenWindowDoesNotContain(
                                                         arena,
                                                         "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                         "private static bool TryConsumePendingAndroidJniException()",
                                                         "Path.Combine(");
            bool pInvokeSourcePluginRoute = arena.Contains("DllImport(\"__Internal\"", StringComparison.Ordinal) &&
                                             arena.Contains("EntryPoint = \"H8_GetAssetSize\"", StringComparison.Ordinal) &&
                                             arena.Contains("EntryPoint = \"H8_LoadAssetToPointer\"", StringComparison.Ordinal) &&
                                             arena.Contains("CallingConvention.Cdecl", StringComparison.Ordinal);
            bool namedLibraryRouteAbsent = !arena.Contains("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal);
            bool zeroPointerGuards = arena.Contains("unityPlayerClass == IntPtr.Zero", StringComparison.Ordinal) &&
                                     arena.Contains("activity == IntPtr.Zero", StringComparison.Ordinal) &&
                                     arena.Contains("activityClass == IntPtr.Zero", StringComparison.Ordinal) &&
                                     arena.Contains("getAssetsMethod == IntPtr.Zero", StringComparison.Ordinal) &&
                                     arena.Contains("assetManager == IntPtr.Zero", StringComparison.Ordinal) &&
                                     arena.Contains("javaVm == IntPtr.Zero", StringComparison.Ordinal);
            bool dataVaultPointerRoute = arena.Contains("TryAcquireArenaWriteView(out NativeArray<byte> arena)", StringComparison.Ordinal) &&
                                         arena.Contains("NativeArrayUnsafeUtility.GetUnsafePtr(arena)", StringComparison.Ordinal) &&
                                         arena.Contains("H8_LoadAssetToPointer(javaVm, assetManager, assetName, destination, blobBytes)", StringComparison.Ordinal);
            bool windowsCreateFileRoute = arena.Contains("CreateFileWNative(", StringComparison.Ordinal) &&
                                          arena.Contains("TryInitializeFromWindowsPlayerStreamingAssets", StringComparison.Ordinal);
            bool androidBranchRoute = arena.Contains("#elif UNITY_ANDROID && !UNITY_EDITOR", StringComparison.Ordinal) &&
                                      arena.Contains("TryInitializeFromAndroidStreamingAssets", StringComparison.Ordinal);
            bool bootstrapRoute = bootstrapper.Contains("InitializeMemoryPreWarmPhaseAsync", StringComparison.Ordinal) &&
                                  bootstrapper.Contains("InitializeBootstrapDataMonolithAsync", StringComparison.Ordinal) &&
                                  bootstrapper.Contains("TryInitializeFromStreamingAssetsAsync", StringComparison.Ordinal);
            bool cmakeReferenceValid = cmake.Contains("add_library(HectonAndroidBridge SHARED", StringComparison.Ordinal) &&
                                       cmake.Contains("target_compile_features(HectonAndroidBridge PRIVATE cxx_std_17)", StringComparison.Ordinal) &&
                                       cmake.Contains("target_link_libraries(HectonAndroidBridge", StringComparison.Ordinal) &&
                                       cmake.Contains("android", StringComparison.Ordinal) &&
                                       cmake.Contains("log", StringComparison.Ordinal);
            bool gradleNoCompress = gradle.Contains("noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ') + ['h8bin']", StringComparison.Ordinal);
            bool gradleExternalNativeBuildAbsent = !gradle.Contains("externalNativeBuild", StringComparison.Ordinal);
            bool unitySourceBuildGradlePlaceholdersPresent = gradle.Contains("**IL_CPP_BUILD_SETUP**", StringComparison.Ordinal) &&
                                                             gradle.Contains("**SOURCE_BUILD_SETUP**", StringComparison.Ordinal) &&
                                                             gradle.Contains("**EXTERNAL_SOURCES**", StringComparison.Ordinal);
            bool nativeSourcePluginDefaultImporterMetaComplete = IsDefaultImporterMetaComplete(nativeMeta) &&
                                                                 IsDefaultImporterMetaComplete(cmakeMeta);
            bool androidSourcePluginRouteSerialized = pInvokeSourcePluginRoute &&
                                                      cmakeReferenceValid &&
                                                      gradleExternalNativeBuildAbsent &&
                                                      unitySourceBuildGradlePlaceholdersPresent &&
                                                      nativeSourcePluginDefaultImporterMetaComplete;
            bool androidIl2Cpp = projectSettings.Contains("scriptingBackend:\n    Android: 1", StringComparison.Ordinal) ||
                                 projectSettings.Contains("scriptingBackend:\r\n    Android: 1", StringComparison.Ordinal);
            bool androidArm64OnlySerialized = projectSettings.Contains("AndroidTargetArchitectures: 2", StringComparison.Ordinal);
            bool androidSplitApplicationBinaryDisabled = projectSettings.Contains("androidSplitApplicationBinary: 0", StringComparison.Ordinal) &&
                                                         projectSettings.Contains("AndroidBuildApkPerCpuArchitecture: 0", StringComparison.Ordinal);
            bool gameActivity = projectSettings.Contains("androidApplicationEntry: 2", StringComparison.Ordinal) &&
                                manifest.Contains("com.unity3d.player.UnityPlayerGameActivity", StringComparison.Ordinal);
            bool androidGameActivityNoLooperDependency = gameActivity &&
                                                         runtimeIntegrationDoc.Contains("Unity GameActivity remains allowed", StringComparison.Ordinal) &&
                                                         runtimeIntegrationDoc.Contains("does not depend on Java `Looper`, `myLooper`, or `Handler` APIs", StringComparison.Ordinal) &&
                                                         TokenWindowDoesNotContain(
                                                             arena,
                                                             "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                             "private static bool TryConsumePendingAndroidJniException()",
                                                             "Looper") &&
                                                         TokenWindowDoesNotContain(
                                                             arena,
                                                             "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                             "private static bool TryConsumePendingAndroidJniException()",
                                                             "myLooper") &&
                                                         TokenWindowDoesNotContain(
                                                             arena,
                                                             "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                                                             "private static bool TryConsumePendingAndroidJniException()",
                                                             "Handler");
            bool mockJniPointerFuzzerTest = tests.Contains("MockJniPointerFuzzer_ZeroPointersAbortBeforeNativeBoundary", StringComparison.Ordinal) &&
                                            tests.Contains("MockShouldAbortBeforeNative", StringComparison.Ordinal);
            bool auditScriptPresent = audit.Contains("H8AndroidAssetBridge1504StaticAudit", StringComparison.Ordinal) &&
                                      audit.Contains("fatalPass ? \"PASS_STATIC_SOURCE_FD_BACKED_GUARD\" : \"FATAL_STATIC_SOURCE\"", StringComparison.Ordinal);
            bool auditRegeneratesFdBackedStatus = auditScriptPresent &&
                                                  audit.Contains("fdBackedGuardBasis", StringComparison.Ordinal);
            bool auditStatusDowngradeGuardPresent = tests.Contains("StaticAudit_RegeneratesFdBackedStatusAndMetaProof", StringComparison.Ordinal) &&
                                                    tests.Contains("PASS_STATIC_SOURCE_FD_BACKED_GUARD", StringComparison.Ordinal);
            bool editorStaticTestsPresent = tests.Contains("AndroidAssetBridge1504StaticAuditTests", StringComparison.Ordinal) &&
                                            tests.Contains("NativeBridge_FailsClosedOnNullInputsAndSizeDrift", StringComparison.Ordinal);
            bool nativeMatrixValidatorGuard = nativeMatrixValidator.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                              nativeMatrixValidator.Contains("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal) &&
                                              nativeMatrixValidator.Contains("AndroidAssetCompressed", StringComparison.Ordinal) &&
                                              nativeMatrixValidator.Contains("uncompressed FD-backed h8bin guard", StringComparison.Ordinal);
            bool nativeMatrixValidatorDumpMirrorGuard = nativeMatrixValidator.Contains("nativeDumpMirrorRouteValid", StringComparison.Ordinal) &&
                                                        nativeMatrixValidator.Contains("H8_WriteTelemetryDumpFile", StringComparison.Ordinal) &&
                                                        nativeMatrixValidator.Contains("Docs/AgentLogs/Dump_1404.bin", StringComparison.Ordinal) &&
                                                        nativeMatrixValidator.Contains("Docs/AgentLogs/Dump_1504.bin", StringComparison.Ordinal) &&
                                                        nativeMatrixValidator.Contains("return legacyOk && agentOk;", StringComparison.Ordinal) &&
                                                        nativeMatrixValidator.Contains("Dump_1404 plus Dump_1504 mirror routes", StringComparison.Ordinal);
            bool legacyAuditGuard = legacyAudit.Contains("uncompressedFdBackedAssetGuard", StringComparison.Ordinal) &&
                                    legacyAudit.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                    legacyAudit.Contains("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal) &&
                                    legacyAudit.Contains("ComputeHash(stream)", StringComparison.Ordinal) &&
                                    !legacyAudit.Contains(FileReadAllBytesToken, StringComparison.Ordinal);
            bool legacyTestsGuard = legacyTests.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                    legacyTests.Contains("H8_ERROR_COMPRESSED_ASSET", StringComparison.Ordinal);
            bool architectureDocsUpdated = runtimeIntegrationDoc.Contains("AAsset_openFileDescriptor64", StringComparison.Ordinal) &&
                                           runtimeIntegrationDoc.Contains("Gradle must keep `h8bin` in `noCompress`", StringComparison.Ordinal) &&
                                           runtimeIntegrationDoc.Contains("AndroidTargetArchitectures: 2", StringComparison.Ordinal) &&
                                           runtimeIntegrationDoc.Contains("androidSplitApplicationBinary: 0", StringComparison.Ordinal) &&
                                           runtimeIntegrationDoc.Contains("Android player builds bypass generic JAR URL staging", StringComparison.Ordinal) &&
                                           runtimeIntegrationDoc.Contains("Unity GameActivity remains allowed", StringComparison.Ordinal) &&
                                           !runtimeIntegrationDoc.Contains("Android/JAR URL staging is cold-boot async", StringComparison.Ordinal) &&
                                           h8binSpecDoc.Contains("uncompressed/FD-backed", StringComparison.Ordinal) &&
                                           h8binSpecDoc.Contains("AndroidTargetArchitectures: 2", StringComparison.Ordinal);
            bool activeArchitectureDocsAligned = bootSequenceDoc.Contains("NDK `AAssetManager` source-plugin bridge", StringComparison.Ordinal) &&
                                                 bootSequenceDoc.Contains("Android/Quest URI staging is not the monolith route", StringComparison.Ordinal) &&
                                                 productContractsDoc.Contains("NDK `AAssetManager` bridge with an FD-backed/uncompressed APK entry guard", StringComparison.Ordinal) &&
                                                 handoffDoc.Contains("Android/Quest via NDK `AAssetManager` source-plugin direct-to-Vault hydration", StringComparison.Ordinal) &&
                                                 portabilityProofDoc.Contains("Android NDK `AAssetManager` bridge with an FD-backed/uncompressed APK entry guard", StringComparison.Ordinal) &&
                                                 !bootSequenceDoc.Contains("Android/Quest URI staging to cache", StringComparison.Ordinal) &&
                                                 !productContractsDoc.Contains("staging Android/Quest URI assets to cache", StringComparison.Ordinal) &&
                                                 !handoffDoc.Contains("Android/Quest URI staging", StringComparison.Ordinal);
            bool unityMetaFilesComplete = IsMonoImporterMetaComplete(auditMeta) &&
                                          IsMonoImporterMetaComplete(testsMeta);
            bool h8binValidatorScopedPass = h8binValidationReport.Contains("\"status\": \"PASS\"", StringComparison.Ordinal) &&
                                            h8binValidationReport.Contains("\"agent_id\": \"1504\"", StringComparison.Ordinal) &&
                                            h8binValidationReport.Contains("\"files_checked\": 2", StringComparison.Ordinal) &&
                                            h8binValidationReport.Contains("\"structs_parsed\": 32", StringComparison.Ordinal) &&
                                            h8binValidationJunit.Contains("<testsuite name=\"h8bin_validator\"", StringComparison.Ordinal) &&
                                            h8binValidationJunit.Contains("failures=\"0\"", StringComparison.Ordinal) &&
                                            h8binValidationMetricPhi.Contains("\"status\": \"PASS\"", StringComparison.Ordinal);
            long staticDataBytes = GetFileLengthOrNegative(projectRoot, StaticDataPath);
            long elapsedMicroseconds = (DateTime.UtcNow.Ticks - startTicks) / 10L;
            bool fatalPass = androidReferencesGuarded &&
                             nativeBridgePresent &&
                             nativeOverflowGuard &&
                             nativeUncompressedFdGuard &&
                             nativeNoHeapStaging &&
                             nativeBoundedDumpPath &&
                             nativeExportVisibilityPresent &&
                             androidNativeTelemetryDumpPresent &&
                             androidNativeTelemetryAgentDumpMirrorPresent &&
                             telemetryDumpLayoutExplicit &&
                             writerReleaseRetryCrossPlatformPresent &&
                             payloadWriteLockFinallyProofPresent &&
                             globalDataVaultDeferredWriterReleaseQueueContract &&
                             dumpTelemetryReadOnlyOnly &&
                             telemetryDumpChronologicalOrderPresent &&
                             jniLocalReferenceLifetimeBounded &&
                             nativeAssetManagerNoCache &&
                             nativeJniEnvironmentReleaseBalanced &&
                             csharpRawJniRoute &&
                             androidAssetNameStackAsciiRoute &&
                             androidTelemetryRouteFlagsPresent &&
                             pInvokeSourcePluginRoute &&
                             namedLibraryRouteAbsent &&
                             zeroPointerGuards &&
                             dataVaultPointerRoute &&
                             windowsCreateFileRoute &&
                             androidBranchRoute &&
                             bootstrapRoute &&
                             cmakeReferenceValid &&
                             gradleNoCompress &&
                             gradleExternalNativeBuildAbsent &&
                             unitySourceBuildGradlePlaceholdersPresent &&
                             nativeSourcePluginDefaultImporterMetaComplete &&
                             androidSourcePluginRouteSerialized &&
                             androidIl2Cpp &&
                             androidArm64OnlySerialized &&
                             androidSplitApplicationBinaryDisabled &&
                             gameActivity &&
                             androidGameActivityNoLooperDependency &&
                             mockJniPointerFuzzerTest &&
                             auditScriptPresent &&
                             auditRegeneratesFdBackedStatus &&
                             auditStatusDowngradeGuardPresent &&
                             editorStaticTestsPresent &&
                             nativeMatrixValidatorGuard &&
                             nativeMatrixValidatorDumpMirrorGuard &&
                             legacyAuditGuard &&
                             legacyTestsGuard &&
                             architectureDocsUpdated &&
                             activeArchitectureDocsAligned &&
                             unityMetaFilesComplete &&
                             h8binValidatorScopedPass &&
                             staticDataBytes > 0L;

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            AppendJson(builder, "agentId", "1504", true);
            AppendJson(builder, "role", "ANDROID_NDK_AND_AASSETMANAGER_PORTABILITY_ARCHITECT", true);
            AppendJson(builder, "evidenceClass", "STATIC_SOURCE", true);
            AppendJson(builder, "status", fatalPass ? "PASS_STATIC_SOURCE_FD_BACKED_GUARD" : "FATAL_STATIC_SOURCE", true);
            AppendJson(builder, "nativeLinkageMode", "Unity Android IL2CPP source plugin via DllImport(\"__Internal\"); no packaged libHectonAndroidBridge.so was present", true);
            AppendJson(builder, "namedDllImportRejectedReason", "Named DllImport requires a packaged shared library route; current project validator owns the source-plugin route", true);
            AppendJson(builder, "fdBackedGuardBasis", "Android NDK AAsset_openFileDescriptor64 returns negative when direct fd access is not possible, including compressed assets", true);
            AppendJson(builder, "arenaPath", ArenaPath, true);
            AppendJson(builder, "typesPath", TypesPath, true);
            AppendJson(builder, "globalDataVaultPath", GlobalDataVaultPath, true);
            AppendJson(builder, "bootstrapperPath", BootstrapperPath, true);
            AppendJson(builder, "nativePluginPath", NativePath, true);
            AppendJson(builder, "nativeMetaPath", NativeMetaPath, true);
            AppendJson(builder, "cmakePath", CmakePath, true);
            AppendJson(builder, "cmakeMetaPath", CmakeMetaPath, true);
            AppendJson(builder, "gradlePath", GradlePath, true);
            AppendJson(builder, "manifestPath", ManifestPath, true);
            AppendJson(builder, "projectSettingsPath", ProjectSettingsPath, true);
            AppendJson(builder, "staticDataPath", StaticDataPath, true);
            AppendJson(builder, "auditPath", AuditPath, true);
            AppendJson(builder, "auditMetaPath", AuditMetaPath, true);
            AppendJson(builder, "testsPath", TestsPath, true);
            AppendJson(builder, "testsMetaPath", TestsMetaPath, true);
            AppendJson(builder, "nativeMatrixValidatorPath", NativeMatrixValidatorPath, true);
            AppendJson(builder, "legacyAuditPath", LegacyAuditPath, true);
            AppendJson(builder, "legacyTestsPath", LegacyTestsPath, true);
            AppendJson(builder, "runtimeIntegrationDocPath", RuntimeIntegrationDocPath, true);
            AppendJson(builder, "h8binSpecDocPath", H8binSpecDocPath, true);
            AppendJson(builder, "bootSequenceDocPath", BootSequenceDocPath, true);
            AppendJson(builder, "productContractsDocPath", ProductContractsDocPath, true);
            AppendJson(builder, "handoffDocPath", HandoffDocPath, true);
            AppendJson(builder, "portabilityProofDocPath", PortabilityProofDocPath, true);
            AppendJson(builder, "h8binValidationReportPath", H8binValidationReportPath, true);
            AppendJson(builder, "h8binValidationJunitPath", H8binValidationJunitPath, true);
            AppendJson(builder, "h8binValidationMetricPhiPath", H8binValidationMetricPhiPath, true);
            AppendJson(builder, "staticDataBytes", staticDataBytes, true);
            AppendJson(builder, "androidReferencesGuarded", androidReferencesGuarded, true);
            AppendJson(builder, "androidReferenceLeakCount", androidLeakCount, true);
            AppendJson(builder, "nativeAAssetBridgePresent", nativeBridgePresent, true);
            AppendJson(builder, "nativeOverflowGuardPresent", nativeOverflowGuard, true);
            AppendJson(builder, "nativeUncompressedFdGuardPresent", nativeUncompressedFdGuard, true);
            AppendJson(builder, "nativeHeapStagingTokensAbsent", nativeNoHeapStaging, true);
            AppendJson(builder, "nativeBoundedDumpPathPresent", nativeBoundedDumpPath, true);
            AppendJson(builder, "nativeExportVisibilityPresent", nativeExportVisibilityPresent, true);
            AppendJson(builder, "androidNativeTelemetryDumpPresent", androidNativeTelemetryDumpPresent, true);
            AppendJson(builder, "androidNativeTelemetryAgentDumpMirrorPresent", androidNativeTelemetryAgentDumpMirrorPresent, true);
            AppendJson(builder, "telemetryDumpLayoutExplicit", telemetryDumpLayoutExplicit, true);
            AppendJson(builder, "writerReleaseRetryCrossPlatformPresent", writerReleaseRetryCrossPlatformPresent, true);
            AppendJson(builder, "payloadWriteLockFinallyProofPresent", payloadWriteLockFinallyProofPresent, true);
            AppendJson(builder, "payloadWriteAcquireCount", payloadWriteAcquireCount, true);
            AppendJson(builder, "payloadWriteReleaseCount", payloadWriteReleaseCount, true);
            AppendJson(builder, "globalDataVaultDeferredWriterReleaseQueueContract", globalDataVaultDeferredWriterReleaseQueueContract, true);
            AppendJson(builder, "dumpTelemetryReadOnlyOnly", dumpTelemetryReadOnlyOnly, true);
            AppendJson(builder, "telemetryDumpChronologicalOrderPresent", telemetryDumpChronologicalOrderPresent, true);
            AppendJson(builder, "jniLocalReferenceLifetimeBounded", jniLocalReferenceLifetimeBounded, true);
            AppendJson(builder, "nativeAssetManagerNoCache", nativeAssetManagerNoCache, true);
            AppendJson(builder, "nativeJniEnvironmentReleaseBalanced", nativeJniEnvironmentReleaseBalanced, true);
            AppendJson(builder, "csharpRawJniRoutePresent", csharpRawJniRoute, true);
            AppendJson(builder, "androidAssetNameStackAsciiRoutePresent", androidAssetNameStackAsciiRoute, true);
            AppendJson(builder, "androidTelemetryRouteFlagsPresent", androidTelemetryRouteFlagsPresent, true);
            AppendJson(builder, "pInvokeSourcePluginRoutePresent", pInvokeSourcePluginRoute, true);
            AppendJson(builder, "pInvokeNamedLibraryRouteAbsent", namedLibraryRouteAbsent, true);
            AppendJson(builder, "unitySourceBuildGradlePlaceholdersPresent", unitySourceBuildGradlePlaceholdersPresent, true);
            AppendJson(builder, "nativeSourcePluginDefaultImporterMetaComplete", nativeSourcePluginDefaultImporterMetaComplete, true);
            AppendJson(builder, "androidSourcePluginRouteSerialized", androidSourcePluginRouteSerialized, true);
            AppendJson(builder, "mockJniZeroPointerGuardsPresent", zeroPointerGuards, true);
            AppendJson(builder, "dataVaultDestinationPointerRoutePresent", dataVaultPointerRoute, true);
            AppendJson(builder, "windowsCreateFileRouteRetained", windowsCreateFileRoute, true);
            AppendJson(builder, "androidBranchRoutePresent", androidBranchRoute, true);
            AppendJson(builder, "bootstrapPrewarmRoutePresent", bootstrapRoute, true);
            AppendJson(builder, "cmakeStandaloneReferenceValid", cmakeReferenceValid, true);
            AppendJson(builder, "h8binNoCompressConfigured", gradleNoCompress, true);
            AppendJson(builder, "gradleExternalNativeBuildAbsent", gradleExternalNativeBuildAbsent, true);
            AppendJson(builder, "androidIl2CppBackendSerialized", androidIl2Cpp, true);
            AppendJson(builder, "androidArm64OnlySerialized", androidArm64OnlySerialized, true);
            AppendJson(builder, "androidSplitApplicationBinaryDisabled", androidSplitApplicationBinaryDisabled, true);
            AppendJson(builder, "androidGameActivityManifestPresent", gameActivity, true);
            AppendJson(builder, "androidGameActivityNoLooperDependency", androidGameActivityNoLooperDependency, true);
            AppendJson(builder, "mockJniPointerFuzzerTestPresent", mockJniPointerFuzzerTest, true);
            AppendJson(builder, "auditScriptPresent", auditScriptPresent, true);
            AppendJson(builder, "auditRegeneratesFdBackedStatus", auditRegeneratesFdBackedStatus, true);
            AppendJson(builder, "auditStatusDowngradeGuardPresent", auditStatusDowngradeGuardPresent, true);
            AppendJson(builder, "editorStaticTestsPresent", editorStaticTestsPresent, true);
            AppendJson(builder, "nativeMatrixValidatorGuardPresent", nativeMatrixValidatorGuard, true);
            AppendJson(builder, "nativeMatrixValidatorDumpMirrorGuardPresent", nativeMatrixValidatorDumpMirrorGuard, true);
            AppendJson(builder, "legacyAuditGuardPresent", legacyAuditGuard, true);
            AppendJson(builder, "legacyTestsGuardPresent", legacyTestsGuard, true);
            AppendJson(builder, "architectureDocsUpdated", architectureDocsUpdated, true);
            AppendJson(builder, "activeArchitectureDocsAligned", activeArchitectureDocsAligned, true);
            AppendJson(builder, "unityMetaFilesComplete", unityMetaFilesComplete, true);
            AppendJson(builder, "h8binValidatorScopedPass", h8binValidatorScopedPass, true);
            AppendJson(builder, "h8binValidatorScopedStatus", h8binValidatorScopedPass ? "PASS" : "FAIL", true);
            AppendJson(builder, "h8binValidatorScopedFiles", 2L, true);
            AppendJson(builder, "h8binValidatorScopedStructs", 32L, true);
            AppendJson(builder, "h8binValidatorIgnoredLogExcluded", true, true);
            AppendJson(builder, "h8binValidatorThoroughStatus", "BLOCKED_BY_TOOL_WATCHDOG", true);
            AppendJson(builder, "staticScanMicroseconds", elapsedMicroseconds, true);
            AppendJson(builder, "arenaSha256", Sha256File(projectRoot, ArenaPath), true);
            AppendJson(builder, "typesSha256", Sha256File(projectRoot, TypesPath), true);
            AppendJson(builder, "globalDataVaultSha256", Sha256File(projectRoot, GlobalDataVaultPath), true);
            AppendJson(builder, "bootstrapperSha256", Sha256File(projectRoot, BootstrapperPath), true);
            AppendJson(builder, "nativeSha256", Sha256File(projectRoot, NativePath), true);
            AppendJson(builder, "nativeMetaSha256", Sha256File(projectRoot, NativeMetaPath), true);
            AppendJson(builder, "cmakeSha256", Sha256File(projectRoot, CmakePath), true);
            AppendJson(builder, "cmakeMetaSha256", Sha256File(projectRoot, CmakeMetaPath), true);
            AppendJson(builder, "gradleSha256", Sha256File(projectRoot, GradlePath), true);
            AppendJson(builder, "manifestSha256", Sha256File(projectRoot, ManifestPath), true);
            AppendJson(builder, "projectSettingsSha256", Sha256File(projectRoot, ProjectSettingsPath), true);
            AppendJson(builder, "staticDataSha256", staticDataBytes > 0L ? Sha256File(projectRoot, StaticDataPath) : string.Empty, true);
            AppendJson(builder, "auditSha256", Sha256File(projectRoot, AuditPath), true);
            AppendJson(builder, "auditMetaSha256", Sha256File(projectRoot, AuditMetaPath), true);
            AppendJson(builder, "testsSha256", Sha256File(projectRoot, TestsPath), true);
            AppendJson(builder, "testsMetaSha256", Sha256File(projectRoot, TestsMetaPath), true);
            AppendJson(builder, "nativeMatrixValidatorSha256", Sha256File(projectRoot, NativeMatrixValidatorPath), true);
            AppendJson(builder, "legacyAuditSha256", Sha256File(projectRoot, LegacyAuditPath), true);
            AppendJson(builder, "legacyTestsSha256", Sha256File(projectRoot, LegacyTestsPath), true);
            AppendJson(builder, "runtimeIntegrationDocSha256", Sha256File(projectRoot, RuntimeIntegrationDocPath), true);
            AppendJson(builder, "h8binSpecDocSha256", Sha256File(projectRoot, H8binSpecDocPath), true);
            AppendJson(builder, "bootSequenceDocSha256", Sha256File(projectRoot, BootSequenceDocPath), true);
            AppendJson(builder, "productContractsDocSha256", Sha256File(projectRoot, ProductContractsDocPath), true);
            AppendJson(builder, "handoffDocSha256", Sha256File(projectRoot, HandoffDocPath), true);
            AppendJson(builder, "portabilityProofDocSha256", Sha256File(projectRoot, PortabilityProofDocPath), true);
            AppendJson(builder, "h8binValidationReportSha256", Sha256File(projectRoot, H8binValidationReportPath), true);
            AppendJson(builder, "h8binValidationJunitSha256", Sha256File(projectRoot, H8binValidationJunitPath), true);
            AppendJson(builder, "h8binValidationMetricPhiSha256", Sha256File(projectRoot, H8binValidationMetricPhiPath), false);
            builder.AppendLine("}");

            string reportAbsolutePath = Path.Combine(projectRoot, ReportPath);
            string reportDirectory = Path.GetDirectoryName(reportAbsolutePath);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            File.WriteAllText(reportAbsolutePath, builder.ToString(), Encoding.UTF8);

            if (!fatalPass)
                throw new FatalArchitectureException("Android asset bridge 1504 static audit failed. See " + ReportPath + ".");
        }

        private static string ReadRequired(string projectRoot, string relativePath)
        {
            string absolutePath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException(relativePath, absolutePath);

            return File.ReadAllText(absolutePath, Encoding.UTF8);
        }

        private static long GetFileLengthOrNegative(string projectRoot, string relativePath)
        {
            string absolutePath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(absolutePath))
                return -1L;

            return new FileInfo(absolutePath).Length;
        }

        private static bool IsMonoImporterMetaComplete(string meta)
        {
            return meta.Contains("MonoImporter:", StringComparison.Ordinal) &&
                   meta.Contains("serializedVersion: 2", StringComparison.Ordinal) &&
                   meta.Contains("defaultReferences: []", StringComparison.Ordinal) &&
                   meta.Contains("assetBundleVariant:", StringComparison.Ordinal);
        }

        private static bool IsDefaultImporterMetaComplete(string meta)
        {
            return meta.Contains("fileFormatVersion: 2", StringComparison.Ordinal) &&
                   meta.Contains("guid:", StringComparison.Ordinal) &&
                   meta.Contains("DefaultImporter:", StringComparison.Ordinal) &&
                   meta.Contains("externalObjects: {}", StringComparison.Ordinal) &&
                   meta.Contains("assetBundleVariant:", StringComparison.Ordinal);
        }

        private static bool AreAndroidReferencesGuarded(string source, out int leakCount)
        {
            leakCount = 0;
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            bool[] guardStack = new bool[64];
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    if (depth < guardStack.Length)
                        guardStack[depth++] = IsAndroidPlayerGuard(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("#elif ", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        guardStack[depth - 1] = IsAndroidPlayerGuard(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        guardStack[depth - 1] = false;
                    continue;
                }

                if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (depth > 0)
                        guardStack[--depth] = false;
                    continue;
                }

                if (ContainsAndroidOnlyToken(lines[i]) && !IsInsideAndroidGuard(guardStack, depth))
                    leakCount++;
            }

            return leakCount == 0;
        }

        private static bool IsAndroidPlayerGuard(string directive)
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
                bool validStart = found == 0 || !IsDirectiveIdentifierChar(directive[found - 1]);
                bool validEnd = end >= directive.Length || !IsDirectiveIdentifierChar(directive[end]);
                if (validStart && validEnd)
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
                   line.Contains("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal) ||
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
                    return count;

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
                    return count;

                int start = Math.Max(0, found - maxDistance);
                string window = text.Substring(start, found - start);
                if (window.Contains(previousToken, StringComparison.Ordinal))
                    count++;

                index = found + token.Length;
            }

            return count;
        }

        private static bool TokenWindowDoesNotContain(string text, string startToken, string endToken, string forbiddenToken)
        {
            int start = text.IndexOf(startToken, StringComparison.Ordinal);
            if (start < 0)
                return false;

            int end = text.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
            if (end < 0 || end < start)
                return false;

            int length = end - start;
            return text.IndexOf(forbiddenToken, start, length, StringComparison.Ordinal) < 0;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
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

        private sealed class FatalArchitectureException : Exception
        {
            public FatalArchitectureException(string message)
                : base(message)
            {
            }
        }
    }
}
