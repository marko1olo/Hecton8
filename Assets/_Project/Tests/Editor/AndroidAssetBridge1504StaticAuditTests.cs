using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AndroidAssetBridge1504StaticAuditTests
    {
        [Test]
        public void AndroidReferences_AreContainedInsideAndroidPlayerGuards()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");

            int leakCount;
            Assert.IsTrue(AreAndroidReferencesGuarded(arena, out leakCount), "Android-only reference leak count: " + leakCount);
        }

        [Test]
        public void MockJniPointerFuzzer_ZeroPointersAbortBeforeNativeBoundary()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            int assetManagerGuard = arena.IndexOf("assetManager == IntPtr.Zero", StringComparison.Ordinal);
            int javaVmGuard = arena.IndexOf("javaVm == IntPtr.Zero", StringComparison.Ordinal);
            int sizeCall = arena.IndexOf("H8_GetAssetSize(javaVm, assetManager, assetName)", StringComparison.Ordinal);
            int loadCall = arena.IndexOf("H8_LoadAssetToPointer(javaVm, assetManager, assetName, destination, blobBytes)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(assetManagerGuard, 0, "assetManager guard");
            Assert.GreaterOrEqual(javaVmGuard, 0, "javaVm guard");
            Assert.Greater(sizeCall, assetManagerGuard, "native size call must be after assetManager guard");
            Assert.Greater(sizeCall, javaVmGuard, "native size call must be after javaVm guard");
            Assert.Greater(loadCall, sizeCall, "native load call must be after size probe");
            Assert.IsTrue(MockShouldAbortBeforeNative(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
            Assert.IsFalse(MockShouldAbortBeforeNative(new IntPtr(1), new IntPtr(2), new IntPtr(3), new IntPtr(4), new IntPtr(5), new IntPtr(6)));
        }

        [Test]
        public void NativeBridge_FailsClosedOnNullInputsAndSizeDrift()
        {
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");

            StringAssert.Contains("javaVm == nullptr", native);
            StringAssert.Contains("assetManager == nullptr", native);
            StringAssert.Contains("filename == nullptr", native);
            StringAssert.Contains("destinationBuffer == nullptr", native);
            StringAssert.Contains("bufferSize <= 0", native);
            StringAssert.Contains("assetLength < 0 || assetLength != bufferSize", native);
            StringAssert.Contains("return totalRead == assetLength", native);
            StringAssert.Contains("H8_ERROR_COMPRESSED_ASSET", native);
            StringAssert.Contains("AAsset_openFileDescriptor64", native);
            StringAssert.Contains("close(fd)", native);
            StringAssert.Contains("H8_TryMeasureCString", native);
            StringAssert.Contains("requiredBytes > static_cast<size_t>(capacity)", native);
            Assert.IsFalse(native.Contains("std::strlen", StringComparison.Ordinal));
            StringAssert.Contains("private const int AndroidAssetCompressed = -6;", arena);
            StringAssert.Contains("blobBytes == AndroidAssetCompressed", arena);
        }

        [Test]
        public void NativeBridge_UsesAAssetDirectReadWithoutHeapStaging()
        {
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string cmake = ReadProjectFile("Assets/Plugins/Android/Native/CMakeLists.txt");

            StringAssert.Contains("AAssetManager_fromJava", native);
            StringAssert.Contains("AAssetManager_open", native);
            StringAssert.Contains("AAsset_getLength64", native);
            StringAssert.Contains("AAsset_read", native);
            StringAssert.Contains("AAsset_close(asset)", native);
            StringAssert.Contains("extern \"C\" JNIEXPORT int32_t JNICALL H8_GetAssetSize", native);
            StringAssert.Contains("extern \"C\" JNIEXPORT bool JNICALL H8_LoadAssetToPointer", native);
            StringAssert.Contains("extern \"C\" JNIEXPORT bool JNICALL H8_WriteTelemetryDump", native);
            StringAssert.Contains("-fvisibility=hidden", cmake);
            Assert.IsFalse(native.Contains("std::vector", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("std::string", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("malloc", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("free(", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("new ", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("delete", StringComparison.Ordinal));
        }

        [Test]
        public void AndroidReleaseTelemetryDump_UsesNativeWriterAndBoundedUtf8()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string types = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs");
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string nativeMatrixValidator = ReadProjectFile("Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs");

            StringAssert.Contains("WriteTelemetryDumpAndroid(status, ring, telemetryCursor)", arena);
            StringAssert.Contains("byte* persistentDataPathUtf8 = stackalloc byte[AndroidPersistentPathUtf8Capacity]", arena);
            StringAssert.Contains("TryWriteUtf8NullTerminated(", arena);
            StringAssert.Contains("NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring)", arena);
            StringAssert.Contains("H8_WriteTelemetryDump(", arena);
            Assert.IsFalse(arena.Contains("System.IO.Path.Combine(Application.persistentDataPath", StringComparison.Ordinal));
            StringAssert.Contains("O_CREAT | O_WRONLY | O_TRUNC | O_CLOEXEC", native);
            StringAssert.Contains("0600", native);
            StringAssert.Contains("H8_WriteAll", native);
            StringAssert.Contains("errno == EINTR", native);
            StringAssert.Contains("close(fd)", native);
            StringAssert.Contains("H8_WriteTelemetryDumpFile", native);
            StringAssert.Contains("Docs/AgentLogs/Dump_1404.bin", native);
            StringAssert.Contains("Docs/AgentLogs/Dump_1504.bin", native);
            StringAssert.Contains("const bool legacyOk = H8_WriteTelemetryDumpFile", native);
            StringAssert.Contains("const bool agentOk = H8_WriteTelemetryDumpFile", native);
            StringAssert.Contains("return legacyOk && agentOk;", native);
            StringAssert.Contains("nativeDumpMirrorRouteValid", nativeMatrixValidator);
            StringAssert.Contains("H8_WriteTelemetryDumpFile", nativeMatrixValidator);
            StringAssert.Contains("Docs/AgentLogs/Dump_1504.bin", nativeMatrixValidator);
            StringAssert.Contains("return legacyOk && agentOk;", nativeMatrixValidator);
            StringAssert.Contains("Dump_1404 plus Dump_1504 mirror routes", nativeMatrixValidator);
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");
            StringAssert.Contains("androidNativeTelemetryAgentDumpMirrorPresent", audit);
            StringAssert.Contains("nativeMatrixValidatorDumpMirrorGuardPresent", audit);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.TelemetryEntrySize)]", types);
            StringAssert.Contains("public struct H8DataMonolithTelemetryEntry", types);
            StringAssert.Contains("[FieldOffset(0)] public ulong Checksum64", types);
            StringAssert.Contains("[FieldOffset(60)] public uint Reserved3", types);
            StringAssert.Contains("UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>() == H8DataLayoutConstants.TelemetryEntrySize", types);
        }

        [Test]
        public void DataVaultWriterRelease_UsesRetryAndDeferredQueueWithoutHotSpin()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string globalDataVault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");

            StringAssert.Contains("private const string GlobalDataVaultPath", audit);
            StringAssert.Contains("writerReleaseRetryCrossPlatformPresent", audit);
            StringAssert.Contains("payloadWriteLockFinallyProofPresent", audit);
            StringAssert.Contains("globalDataVaultDeferredWriterReleaseQueueContract", audit);
            StringAssert.Contains("private const int DataMonolithWriterReleaseRetryCount = 4;", arena);
            StringAssert.Contains("for (int attempt = 0; attempt < DataMonolithWriterReleaseRetryCount; attempt++)", arena);
            StringAssert.Contains("vault.ReleaseWriteLock(in handle, owner)", arena);
            StringAssert.Contains("Thread.Yield();", arena);
            Assert.IsFalse(arena.Contains("return vault.ReleaseWriteLock(in handle, owner);\n#endif", StringComparison.Ordinal));

            int acquireCount = CountToken(arena, "TryAcquireArenaWriteView(out NativeArray<byte> arena)") - 1;
            int releaseCount = CountToken(arena, "writeLockReleased = ReleaseArenaWriteView();");
            Assert.GreaterOrEqual(acquireCount, 4, "payload write lock acquire sites");
            Assert.AreEqual(acquireCount, releaseCount, "payload write lock release sites");
            Assert.AreEqual(releaseCount, CountTokenWithPreviousToken(arena, "writeLockReleased = ReleaseArenaWriteView();", "finally", 96));
            StringAssert.Contains("if (!lockTransferred)", arena);
            StringAssert.Contains("ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);", arena);
            StringAssert.Contains("arena = default;", arena);

            StringAssert.Contains("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);", globalDataVault);
            StringAssert.Contains("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);", globalDataVault);
            StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", globalDataVault);
            StringAssert.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0;", globalDataVault);
            StringAssert.Contains("pending->Kind == DeferredReleaseKindWriter", globalDataVault);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0);", globalDataVault);
            StringAssert.Contains("local.Kind == DeferredReleaseKindWriter", globalDataVault);
            Assert.IsFalse(globalDataVault.Contains("pending->Kind == kind", StringComparison.Ordinal));
            Assert.IsFalse(globalDataVault.Contains("Thread.SpinWait", StringComparison.Ordinal));
            Assert.IsFalse(globalDataVault.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate", StringComparison.Ordinal));
        }

        [Test]
        public void TelemetryDump_ReadsOnlySnapshotsAndWritesChronologicalNativeBytes()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");

            StringAssert.Contains("dumpTelemetryReadOnlyOnly", audit);
            StringAssert.Contains("telemetryDumpChronologicalOrderPresent", audit);
            StringAssert.Contains("private static void DumpTelemetry(H8DataBlobLoadStatus status)", arena);
            StringAssert.Contains("if (!TryReadTelemetry(out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring, out NativeArray<int>.ReadOnly cursor))", arena);
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static void DumpTelemetry(H8DataBlobLoadStatus status)",
                "private static int NormalizeTelemetryCursor(int cursor)",
                "EnsureTelemetry("));
            StringAssert.Contains("int start = NormalizeTelemetryCursor(cursor);", arena);
            StringAssert.Contains("int ringIndex = start + i;", arena);
            StringAssert.Contains("const int32_t firstEntryCount = entryCount - normalizedCursor;", native);
            StringAssert.Contains("entryBytes + normalizedCursor * entrySize", native);
        }

        [Test]
        public void JniLocalRefs_AreBoundedToSynchronousNativeCalls()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string types = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs");
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");

            StringAssert.Contains("jniLocalReferenceLifetimeBounded", audit);
            StringAssert.Contains("nativeAssetManagerNoCache", audit);
            StringAssert.Contains("nativeJniEnvironmentReleaseBalanced", audit);
            StringAssert.Contains("androidAssetNameStackAsciiRoutePresent", audit);
            StringAssert.Contains("androidTelemetryRouteFlagsPresent", audit);
            Assert.IsTrue(ContainsTokensInOrder(
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
                "AndroidJNI.DeleteLocalRef(unityPlayerClass);"));
            StringAssert.Contains("AndroidJNI.ExceptionClear();", arena);
            StringAssert.Contains("AndroidJNI.DeleteLocalRef(exception);", arena);
            Assert.IsFalse(arena.Contains("AndroidJNI.NewGlobalRef", StringComparison.Ordinal));
            StringAssert.Contains("byte* assetName = stackalloc byte[assetNameCapacity]", arena);
            StringAssert.Contains("TryWriteAndroidAssetName(assetName, assetNameCapacity)", arena);
            StringAssert.Contains("ReadOnlySpan<char> relativePath = H8DataLayoutConstants.DefaultStreamingAssetsRelativePath.AsSpan();", arena);
            StringAssert.Contains("if (c > 0x7F)", arena);
            StringAssert.Contains("destination[relativePath.Length] = 0;", arena);
            StringAssert.Contains("DefaultStreamingAssetsRelativePath = \"Hecton8/DataMonolith/static_data.h8bin\"", types);

            StringAssert.Contains("AAssetManager_fromJava(environment, reinterpret_cast<jobject>(javaAssetManager))", native);
            Assert.GreaterOrEqual(CountToken(native, "AAsset_close(asset);"), 4);
            Assert.IsFalse(native.Contains("NewGlobalRef", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("DeleteGlobalRef", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("static AAssetManager", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("static AAsset", StringComparison.Ordinal));
            Assert.IsFalse(native.Contains("static jobject", StringComparison.Ordinal));
        }

        [Test]
        public void NativeJniEnvironment_AttachDetachIsBalancedOnAssetEntryPoints()
        {
            string native = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");

            StringAssert.Contains("nativeJniEnvironmentReleaseBalanced", audit);
            StringAssert.Contains("const jint getEnvResult = vm->GetEnv(reinterpret_cast<void**>(environment), JNI_VERSION_1_6);", native);
            StringAssert.Contains("if (getEnvResult == JNI_OK)", native);
            StringAssert.Contains("if (getEnvResult != JNI_EDETACHED)", native);
            StringAssert.Contains("vm->AttachCurrentThread(environment, nullptr)", native);
            StringAssert.Contains("*attached = true;", native);
            StringAssert.Contains("if (attached && vm != nullptr)", native);
            StringAssert.Contains("vm->DetachCurrentThread();", native);
            Assert.AreEqual(2, CountToken(native, "H8_TryAcquireJniEnvironment(javaVm, &environment, &attached)"));
            Assert.GreaterOrEqual(CountToken(native, "H8_ReleaseJniEnvironment(javaVm, attached);"), 8);
            Assert.IsTrue(ContainsTokensInOrder(
                native,
                "extern \"C\" JNIEXPORT int32_t JNICALL H8_GetAssetSize",
                "H8_TryAcquireJniEnvironment(javaVm, &environment, &attached)",
                "AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(environment, assetManager);",
                "H8_ReleaseJniEnvironment(javaVm, attached);",
                "AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);",
                "H8_ReleaseJniEnvironment(javaVm, attached);",
                "AAsset_close(asset);",
                "H8_ReleaseJniEnvironment(javaVm, attached);"));
            Assert.IsTrue(ContainsTokensInOrder(
                native,
                "extern \"C\" JNIEXPORT bool JNICALL H8_LoadAssetToPointer",
                "H8_TryAcquireJniEnvironment(javaVm, &environment, &attached)",
                "AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(environment, assetManager);",
                "H8_ReleaseJniEnvironment(javaVm, attached);",
                "AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);",
                "H8_ReleaseJniEnvironment(javaVm, attached);",
                "AAsset_close(asset);",
                "H8_ReleaseJniEnvironment(javaVm, attached);",
                "AAsset_close(asset);",
                "H8_ReleaseJniEnvironment(javaVm, attached);"));
        }

        [Test]
        public void AndroidLoaderTelemetry_UsesNativeAssetRouteFlagsWithoutUriStaging()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");

            StringAssert.Contains("androidTelemetryRouteFlagsPresent", audit);
            StringAssert.Contains("private const uint PathFlagAndroidAssetManager = 128u;", arena);
            StringAssert.Contains("private const uint PathFlagAndroidJavaAssetManager = 256u;", arena);
            StringAssert.Contains("uint pathFlags = PathFlagVaultBacked | PathFlagAndroidAssetManager | PathFlagAndroidJavaAssetManager;", arena);
            Assert.IsTrue(ContainsTokensInOrder(
                arena,
                "uint pathFlags = PathFlagVaultBacked | PathFlagAndroidAssetManager | PathFlagAndroidJavaAssetManager;",
                "_lastReadPathFlags = pathFlags;",
                "RecordFailureTelemetry(status, pathFlags);",
                "RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "PathFlagStreamingUriStaged"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "Application.streamingAssetsPath"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "UnityWebRequest"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "temporaryCachePath"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "Path.Combine("));
        }

        [Test]
        public void AndroidPackaging_KeepsH8binUncompressedAndUsesSourcePluginRoute()
        {
            string arena = ReadProjectFile("Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs");
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");
            string gradle = ReadProjectFile("Assets/Plugins/Android/mainTemplate.gradle");
            string nativeMeta = ReadProjectFile("Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp.meta");
            string cmakeMeta = ReadProjectFile("Assets/Plugins/Android/Native/CMakeLists.txt.meta");
            string projectSettings = ReadProjectFile("ProjectSettings/ProjectSettings.asset");
            string manifest = ReadProjectFile("Assets/Plugins/Android/AndroidManifest.xml");
            string runtimeIntegrationDoc = ReadProjectFile("Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md");
            string projectRoot = ProjectRoot;

            StringAssert.Contains("noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ') + ['h8bin']", gradle);
            Assert.IsFalse(gradle.Contains("externalNativeBuild", StringComparison.Ordinal));
            StringAssert.Contains("**IL_CPP_BUILD_SETUP**", gradle);
            StringAssert.Contains("**SOURCE_BUILD_SETUP**", gradle);
            StringAssert.Contains("**EXTERNAL_SOURCES**", gradle);
            StringAssert.Contains("DllImport(\"__Internal\"", arena);
            Assert.IsFalse(arena.Contains("DllImport(\"HectonAndroidBridge\"", StringComparison.Ordinal));
            StringAssert.Contains("androidSourcePluginRouteSerialized", audit);
            StringAssert.Contains("nativeSourcePluginDefaultImporterMetaComplete", audit);
            StringAssert.Contains("unitySourceBuildGradlePlaceholdersPresent", audit);
            Assert.IsTrue(IsDefaultImporterMetaComplete(nativeMeta), "native source meta");
            Assert.IsTrue(IsDefaultImporterMetaComplete(cmakeMeta), "cmake meta");
            StringAssert.Contains("AndroidTargetArchitectures: 2", projectSettings);
            StringAssert.Contains("AndroidBuildApkPerCpuArchitecture: 0", projectSettings);
            StringAssert.Contains("androidSplitApplicationBinary: 0", projectSettings);
            StringAssert.Contains("androidApplicationEntry: 2", projectSettings);
            StringAssert.Contains("com.unity3d.player.UnityPlayerGameActivity", manifest);
            StringAssert.Contains("androidGameActivityNoLooperDependency", audit);
            StringAssert.Contains("Unity GameActivity remains allowed", runtimeIntegrationDoc);
            StringAssert.Contains("does not depend on Java `Looper`, `myLooper`, or `Handler` APIs", runtimeIntegrationDoc);
            Assert.IsFalse(runtimeIntegrationDoc.Contains("Android/JAR URL staging is cold-boot async", StringComparison.Ordinal));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "Looper"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "myLooper"));
            Assert.IsTrue(TokenWindowDoesNotContain(
                arena,
                "private static unsafe bool TryInitializeFromAndroidStreamingAssets(",
                "private static bool TryConsumePendingAndroidJniException()",
                "Handler"));
            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, "Assets/Plugins/Android/arm64-v8a/libHectonAndroidBridge.so")));
            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, "Assets/Plugins/Android/libs/arm64-v8a/libHectonAndroidBridge.so")));
        }

        [Test]
        public void StaticAudit_RegeneratesFdBackedStatusAndMetaProof()
        {
            string audit = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs");
            string auditMeta = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs.meta");
            string testsMeta = ReadProjectFile("Assets/_Project/Tests/Editor/AndroidAssetBridge1504StaticAuditTests.cs.meta");

            StringAssert.Contains("fatalPass ? \"PASS_STATIC_SOURCE_FD_BACKED_GUARD\" : \"FATAL_STATIC_SOURCE\"", audit);
            StringAssert.Contains("fdBackedGuardBasis", audit);
            StringAssert.Contains("auditRegeneratesFdBackedStatus", audit);
            StringAssert.Contains("auditStatusDowngradeGuardPresent", audit);
            StringAssert.Contains("nativeExportVisibilityPresent", audit);
            StringAssert.Contains("androidNativeTelemetryDumpPresent", audit);
            StringAssert.Contains("androidNativeTelemetryAgentDumpMirrorPresent", audit);
            StringAssert.Contains("telemetryDumpLayoutExplicit", audit);
            StringAssert.Contains("writerReleaseRetryCrossPlatformPresent", audit);
            StringAssert.Contains("payloadWriteLockFinallyProofPresent", audit);
            StringAssert.Contains("globalDataVaultDeferredWriterReleaseQueueContract", audit);
            StringAssert.Contains("dumpTelemetryReadOnlyOnly", audit);
            StringAssert.Contains("telemetryDumpChronologicalOrderPresent", audit);
            StringAssert.Contains("jniLocalReferenceLifetimeBounded", audit);
            StringAssert.Contains("nativeAssetManagerNoCache", audit);
            StringAssert.Contains("nativeJniEnvironmentReleaseBalanced", audit);
            StringAssert.Contains("androidTelemetryRouteFlagsPresent", audit);
            StringAssert.Contains("androidSourcePluginRouteSerialized", audit);
            StringAssert.Contains("androidArm64OnlySerialized", audit);
            StringAssert.Contains("androidSplitApplicationBinaryDisabled", audit);
            StringAssert.Contains("androidGameActivityNoLooperDependency", audit);
            StringAssert.Contains("ComputeHash(stream)", audit);
            Assert.IsFalse(audit.Contains("File.ReadAllBytes", StringComparison.Ordinal));
            StringAssert.Contains("FileReadAllBytesToken", audit);
            StringAssert.Contains("nativeMatrixValidatorGuardPresent", audit);
            StringAssert.Contains("nativeMatrixValidatorDumpMirrorGuardPresent", audit);
            StringAssert.Contains("architectureDocsUpdated", audit);
            StringAssert.Contains("activeArchitectureDocsAligned", audit);
            StringAssert.Contains("unityMetaFilesComplete", audit);
            StringAssert.Contains("h8binValidatorScopedPass", audit);
            StringAssert.Contains("h8binValidatorIgnoredLogExcluded", audit);
            StringAssert.Contains("h8binValidatorThoroughStatus", audit);
            Assert.IsFalse(audit.Contains("CI_BINARY_VALIDATION_1504.log", StringComparison.Ordinal));
            Assert.IsTrue(IsMonoImporterMetaComplete(auditMeta), "audit meta");
            Assert.IsTrue(IsMonoImporterMetaComplete(testsMeta), "tests meta");
        }

        [Test]
        public void ArchitectureDocs_RejectStaleAndroidUriStagingContract()
        {
            string bootSequence = ReadProjectFile("Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md");
            string productContracts = ReadProjectFile("Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md");
            string handoff = ReadProjectFile("Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md");
            string portabilityProof = ReadProjectFile("Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md");
            string runtimeIntegration = ReadProjectFile("Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md");

            Assert.IsFalse(bootSequence.Contains("Android/Quest URI staging to cache", StringComparison.Ordinal));
            Assert.IsFalse(productContracts.Contains("staging Android/Quest URI assets to cache", StringComparison.Ordinal));
            Assert.IsFalse(handoff.Contains("Android/Quest URI staging", StringComparison.Ordinal));
            Assert.IsFalse(runtimeIntegration.Contains("Android/JAR URL staging is cold-boot async", StringComparison.Ordinal));
            StringAssert.Contains("NDK `AAssetManager` source-plugin bridge", bootSequence);
            StringAssert.Contains("NDK `AAssetManager` bridge with an FD-backed/uncompressed APK entry guard", productContracts);
            StringAssert.Contains("Android/Quest via NDK `AAssetManager` source-plugin direct-to-Vault hydration", handoff);
            StringAssert.Contains("Android NDK `AAssetManager` bridge with an FD-backed/uncompressed APK entry guard", portabilityProof);
            StringAssert.Contains("Android player builds bypass generic JAR URL staging", runtimeIntegration);
            StringAssert.Contains("Unity GameActivity remains allowed", runtimeIntegration);
        }

        private static string ProjectRoot
        {
            get
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    throw new InvalidOperationException("Project root could not be resolved.");

                return projectRoot;
            }
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(ProjectRoot, relativePath));
        }

        private static bool MockShouldAbortBeforeNative(
            IntPtr unityPlayerClass,
            IntPtr activity,
            IntPtr activityClass,
            IntPtr getAssetsMethod,
            IntPtr assetManager,
            IntPtr javaVm)
        {
            return unityPlayerClass == IntPtr.Zero ||
                   activity == IntPtr.Zero ||
                   activityClass == IntPtr.Zero ||
                   getAssetsMethod == IntPtr.Zero ||
                   assetManager == IntPtr.Zero ||
                   javaVm == IntPtr.Zero;
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
    }
}
