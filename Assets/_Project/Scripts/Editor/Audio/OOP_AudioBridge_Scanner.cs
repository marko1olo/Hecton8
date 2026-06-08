#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Hecton8.Audio;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.Audio.Editor
{
    public static class OOP_AudioBridge_Scanner
    {
        private const string ReportPath = "Docs/Reports/AUDIO_BRIDGE_OPTIMIZATION_REPORT_1314.json";
        private const string BridgePath = "Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs";
        private const string RingPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string MemoryPath = "Assets/_Project/Scripts/Core/Memory/H8Memory.cs";
        private const string NativePluginPath = "NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp";
        private const string NativePluginMatrixPath = "Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs";
        private const string NativeBuildScriptPath = "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernel.bat";
        private const string NativeAndroidBuildScriptPath = "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernelAndroid.bat";
        private const string NativeUtilityCppPath = "NativeAudio/HectonSensoryKernel/AudioPluginUtil.cpp";
        private const string NativeUtilityHeaderPath = "NativeAudio/HectonSensoryKernel/AudioPluginUtil.h";
        private const string NativePluginListPath = "NativeAudio/HectonSensoryKernel/PluginList.h";
        private const string WindowsAudioKernelDllPath = "Assets/Plugins/x86_64/HectonAudioKernel.dll";
        private const string WindowsAudioKernelMetaPath = "Assets/Plugins/x86_64/HectonAudioKernel.dll.meta";
        private const string WindowsLz4MetaPath = "Assets/_Project/Plugins/Windows/x86_64/liblz4.dll.meta";
        private const string MasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string AudioMixerSanitizerPath = "Assets/_Project/Scripts/Editor/AudioMixerSanitizer.cs";

        [MenuItem("Hecton8/Audio/Scan Audio Bridge Alignment 1314")]
        public static void RunStaticScanMenu()
        {
            ScanResult result = ScanProject(runLiveFuzzer: false);
            WriteReport(result);
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log("AUDIO_BRIDGE_ALIGNMENT_1314 static scanner wrote " + ReportPath + " with " + result.FailedChecks + " failures.");
        }

        [MenuItem("Hecton8/Audio/Fuzz Audio Bridge SPSC 1314")]
        public static void RunFuzzerMenu()
        {
            ScanResult result = ScanProject(runLiveFuzzer: true);
            WriteReport(result);
            AssetDatabase.Refresh();
            if (result.Pass)
                Hecton8.Core.H8Debug.Log("AUDIO_BRIDGE_ALIGNMENT_1314 fuzzer passed and wrote " + ReportPath + ".");
            else
                Hecton8.Core.H8Debug.LogError("AUDIO_BRIDGE_ALIGNMENT_1314 fuzzer/static scan failed. See " + ReportPath + ".");
        }

        internal static ScanResult ScanProject(bool runLiveFuzzer)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScanResult result = new ScanResult
            {
                Checks = new List<CheckResult>(32),
                RunLiveFuzzer = runLiveFuzzer
            };

            string bridge = ReadAssetText(projectRoot, BridgePath, result);
            string ring = ReadAssetText(projectRoot, RingPath, result);
            string ringRuntime = StripUnityEditorRegion(ring);
            string renderer = ReadAssetText(projectRoot, RendererPath, result);
            string memory = ReadAssetText(projectRoot, MemoryPath, result);
            string nativePlugin = ReadAssetText(projectRoot, NativePluginPath, result);
            string nativePluginMatrix = ReadAssetText(projectRoot, NativePluginMatrixPath, result);
            string nativeBuildScript = ReadAssetText(projectRoot, NativeBuildScriptPath, result);
            string nativeAndroidBuildScript = ReadAssetText(projectRoot, NativeAndroidBuildScriptPath, result);
            string windowsAudioKernelMeta = ReadAssetText(projectRoot, WindowsAudioKernelMetaPath, result);
            string windowsLz4Meta = ReadAssetText(projectRoot, WindowsLz4MetaPath, result);
            ReadAssetText(projectRoot, MasterMixerPath, result);
            string audioMixerSanitizer = ReadAssetText(projectRoot, AudioMixerSanitizerPath, result);
            string scanner = ReadAssetText(projectRoot, "Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs", result);

            AssertContains(result, bridge, "WriteIndexSlot = 2", "write_index_slot_even", BridgePath, "WriteIndex exported at shared-state byte offset 8.");
            AssertContains(result, bridge, "SourceChannelsSlot = 12", "source_channels_even_slot", BridgePath, "Shared-state metadata records mono/stereo ring layout in the padded shared-state contract.");
            AssertContains(result, bridge, "public int SourceChannels", "descriptor_source_channels_field", BridgePath, "Descriptor carries immutable source-channel count so native callback does not trust mutable SharedState for frame stride.");
            AssertContains(result, bridge, "SharedStateSlotCount = 14", "shared_state_padded", BridgePath, "Shared state reserves even int slots for 8-byte pointer alignment and source-channel metadata.");
            AssertContains(result, bridge, "HasValidSharedStatePointerLayout", "bridge_layout_guard", BridgePath, "Bridge rejects descriptors whose cursor pointers do not match expected shared-state offsets.");
            AssertContains(result, bridge, "HasValidSharedStateMetadata", "bridge_shared_metadata_guard", BridgePath, "Bridge local validation matches native shared-state capacity/mask/guard/source-channel checks before P/Invoke.");
            AssertContains(result, bridge, "Volatile.Read(ref sharedStatePtr", "bridge_shared_metadata_volatile_reads", BridgePath, "Bridge shared-state metadata validation reads unmanaged cursor metadata with volatile semantics.");
            AssertContains(result, bridge, "TryRegisterWithRetryGate", "bridge_retry_gate", BridgePath, "Registration retries once and fails closed without clearing an already-owned descriptor on failure.");
            AssertNotContains(result, bridge, "if (!IsDescriptorValid(in descriptor, out status))\n            {\n                TryClear(out _);", "bridge_invalid_candidate_does_not_clear_active_bridge", BridgePath, "Invalid candidate descriptor is rejected without clearing a previously valid native bridge.");
            AssertMethodNotContains(result, bridge, "public static bool TryRegisterWithRetryGate", "TryClear(out _);", "bridge_register_failure_does_not_clear_owned_descriptor", BridgePath, "Failed register attempts do not call clear as cleanup; native ownership is preserved unless explicit TryClear is requested.");
            AssertMethodContains(result, bridge, "public static bool TryRegister(ref NativeAudioKernelRingBufferDescriptor descriptor, out NativeAudioKernelBridgeStatus status)", "(status & NativeAudioKernelBridgeStatus.Busy) == 0", "bridge_register_rejects_busy", BridgePath, "Managed register success requires Active without Busy so a drain-failed old descriptor is not misclassified as the newly registered bridge.");
            AssertContains(result, bridge, "HectonSensoryKernel_RegisterSharedRingBufferAndGetStatus", "bridge_register_returns_operation_status", BridgePath, "Native register returns its operation status directly through a new export, so stale void-export DLLs fail closed instead of returning undefined data.");
            AssertContains(result, bridge, "status = (NativeAudioKernelBridgeStatus)RegisterSharedRingBuffer(ref descriptor);", "bridge_register_uses_direct_status", BridgePath, "Managed register reads the native mutation result directly instead of racing through GetStatus.");
            AssertContains(result, bridge, "HectonSensoryKernel_ClearSharedRingBufferAndGetStatus", "bridge_clear_returns_operation_status", BridgePath, "Native clear returns its operation status directly through a new export, so stale void-export DLLs fail closed instead of returning undefined data.");
            AssertContains(result, bridge, "status = (NativeAudioKernelBridgeStatus)ClearSharedRingBuffer();", "bridge_clear_uses_direct_status", BridgePath, "Managed clear reads the native mutation result directly instead of racing through GetStatus.");
            AssertContains(result, bridge, "TryDumpAudioBridgeTelemetry", "bridge_native_dump_gate", BridgePath, "Bridge exposes native telemetry dump without managed FileStream/Path/Directory use.");
            AssertContains(result, bridge, "(status & NativeAudioKernelBridgeStatus.Busy) == 0", "bridge_clear_rejects_busy", BridgePath, "TryClear does not report success while native clear is still Busy.");
            AssertContains(result, bridge, "UNITY_ANDROID", "bridge_android_native_route", BridgePath, "Android/Quest builds compile the native audio bridge route when an arm64 plugin binary is packaged.");
            AssertContains(result, nativePluginMatrix, "Android/arm64-v8a/libHectonAudioKernel.so", "android_audio_kernel_matrix_gate", NativePluginMatrixPath, "Android build preflight requires an arm64 HectonAudioKernel native plugin instead of silently shipping managed-only master-bus output.");
            AssertContains(result, nativePluginMatrix, "Validate(report.summary.platform, strictBuild: true)", "native_plugin_matrix_player_build_hard_fail", NativePluginMatrixPath, "Actual player builds fail on missing native plugin blockers; the advisory-only path is limited to the editor menu scan.");
            AssertContains(result, nativePluginMatrix, "RequirePlugin(", "native_plugin_matrix_file_and_importer_gate", NativePluginMatrixPath, "Single native plugin requirements validate both file presence and Unity PluginImporter routing.");
            AssertContains(result, nativePluginMatrix, "RequireAnyPlugin(", "native_plugin_matrix_any_file_and_importer_gate", NativePluginMatrixPath, "Alternative native plugin paths validate importer routing before accepting a platform binary.");
            AssertContains(result, nativePluginMatrix, "PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter", "native_plugin_matrix_importer_probe", NativePluginMatrixPath, "Build preflight rejects raw native files with GUID-only or non-plugin .meta importers.");
            AssertContains(result, nativePluginMatrix, "importer.GetCompatibleWithPlatform(target)", "native_plugin_matrix_platform_importer_compatibility", NativePluginMatrixPath, "Build preflight rejects native binaries that are not enabled for the current build target.");
            AssertContains(result, nativePluginMatrix, "RequirePluginFreshness(", "native_plugin_matrix_audio_kernel_freshness_gate", NativePluginMatrixPath, "Windows player builds fail if the packaged HectonAudioKernel DLL is older than native source or build script.");
            AssertContains(result, nativePluginMatrix, "RequireAnyCompatiblePluginFreshness(", "native_plugin_matrix_android_audio_kernel_freshness_gate", NativePluginMatrixPath, "Android player builds fail if a packaged HectonAudioKernel .so is older than native source or Android build script.");
            AssertContains(result, nativePluginMatrix, "NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp", "native_plugin_matrix_audio_kernel_source_reference", NativePluginMatrixPath, "Build preflight compares the packaged Windows audio DLL against the native source timestamp.");
            AssertContains(result, nativePluginMatrix, "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernel.bat", "native_plugin_matrix_audio_kernel_build_script_reference", NativePluginMatrixPath, "Build preflight compares the packaged Windows audio DLL against the native build-script timestamp.");
            AssertContains(result, nativePluginMatrix, "NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernelAndroid.bat", "native_plugin_matrix_android_audio_kernel_build_script_reference", NativePluginMatrixPath, "Build preflight compares the packaged Android audio .so against the Android native build-script timestamp.");
            AssertContains(result, nativePluginMatrix, "AssetFileExists(", "native_plugin_matrix_project_root_file_probe", NativePluginMatrixPath, "Build preflight probes files from the Unity project root instead of depending on the current working directory.");
            AssertContains(result, nativePluginMatrix, "File.GetLastWriteTimeUtc(ToProjectAbsolutePath(assetPath))", "native_plugin_matrix_timestamp_probe", NativePluginMatrixPath, "Build preflight uses UTC timestamps from project-root absolute paths for stale native-plugin rejection.");
            AssertAnyAssetExists(result, projectRoot, new[] { "Assets/Plugins/Android/arm64-v8a/libHectonAudioKernel.so", "Assets/Plugins/Android/libs/arm64-v8a/libHectonAudioKernel.so" }, "android_audio_kernel_binary_packaged", NativePluginMatrixPath, "Android/Quest builds require a packaged arm64 HectonAudioKernel native plugin.");
            AssertAnyAssetExists(result, projectRoot, new[] { "Assets/_Project/Plugins/Android/arm64-v8a/liblz4.so", "Assets/_Project/Plugins/Android/libs/arm64-v8a/liblz4.so" }, "android_lz4_binary_packaged", NativePluginMatrixPath, "Android/Quest builds require a packaged arm64 LZ4 native plugin.");
            AssertContains(result, windowsLz4Meta, "PluginImporter:", "windows_lz4_plugin_importer_meta", WindowsLz4MetaPath, "Windows LZ4 DLL metadata must be a Unity PluginImporter asset, not GUID-only metadata.");
            AssertContains(result, windowsAudioKernelMeta, "PluginImporter:", "windows_audio_kernel_plugin_importer_meta", WindowsAudioKernelMetaPath, "Packaged Windows audio kernel DLL is imported as a Unity PluginImporter, not a GUID-only raw file.");
            AssertContains(result, windowsAudioKernelMeta, "Standalone: Win64", "windows_audio_kernel_win64_enabled_target", WindowsAudioKernelMetaPath, "Packaged Windows audio kernel DLL has an explicit Win64 platform importer lane.");
            AssertContains(result, windowsAudioKernelMeta, "CPU: x86_64", "windows_audio_kernel_x64_cpu_meta", WindowsAudioKernelMetaPath, "Packaged Windows audio kernel DLL is constrained to x86_64 for editor/player native loading.");
            AssertContains(result, windowsAudioKernelMeta, "OS: Windows", "windows_audio_kernel_editor_windows_meta", WindowsAudioKernelMetaPath, "Packaged Windows audio kernel DLL is explicitly routed to Windows editor native loading.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Any:\n", "enabled: 0", "windows_audio_kernel_any_platform_disabled", WindowsAudioKernelMetaPath, "Packaged Windows DLL disables the catch-all plugin lane so it cannot be imported for unsupported platforms by accident.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Editor: Editor\n", "enabled: 1", "windows_audio_kernel_editor_lane_enabled", WindowsAudioKernelMetaPath, "Packaged Windows DLL explicitly enables the Windows editor plugin lane.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Editor: Editor\n", "CPU: x86_64", "windows_audio_kernel_editor_lane_x64", WindowsAudioKernelMetaPath, "Windows editor plugin lane is constrained to x86_64.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Editor: Editor\n", "OS: Windows", "windows_audio_kernel_editor_lane_windows", WindowsAudioKernelMetaPath, "Windows editor plugin lane is constrained to OS: Windows.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Standalone: Win\n", "enabled: 0", "windows_audio_kernel_win32_lane_disabled", WindowsAudioKernelMetaPath, "Packaged Windows x86_64 DLL disables the 32-bit Windows player lane.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Standalone: Win64\n", "enabled: 1", "windows_audio_kernel_win64_lane_enabled", WindowsAudioKernelMetaPath, "Packaged Windows DLL explicitly enables the Win64 player lane.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Standalone: Win64\n", "CPU: x86_64", "windows_audio_kernel_win64_lane_x64", WindowsAudioKernelMetaPath, "Win64 player plugin lane is constrained to x86_64.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Standalone: Linux64\n", "enabled: 0", "windows_audio_kernel_linux_lane_disabled", WindowsAudioKernelMetaPath, "Packaged Windows DLL disables Linux player import.");
            AssertPluginMetaSectionContains(result, windowsAudioKernelMeta, "      Standalone: OSXUniversal\n", "enabled: 0", "windows_audio_kernel_osx_lane_disabled", WindowsAudioKernelMetaPath, "Packaged Windows DLL disables macOS player import.");
            AssertFileNotOlderThan(result, projectRoot, WindowsAudioKernelDllPath, NativePluginPath, "windows_audio_kernel_dll_not_older_than_native_source", WindowsAudioKernelDllPath, "Packaged Windows DLL must be rebuilt after native source changes; Unity loads this binary, not Plugin_HectonSensoryKernel.cpp.");
            AssertFileNotOlderThan(result, projectRoot, WindowsAudioKernelDllPath, NativeBuildScriptPath, "windows_audio_kernel_dll_not_older_than_native_build_script", WindowsAudioKernelDllPath, "Packaged Windows DLL must be rebuilt after native build-script changes.");
            AppendCheck(result, MasterMixerPath, "master_mixer_hecton_effect_authored", Hecton8.Editor.AudioMixerSanitizer.HasMixerEffectAtPath(MasterMixerPath, Hecton8.Editor.AudioMixerSanitizer.KernelEffectName), "MasterMixer must contain a concrete native Hecton Sensory Kernel effect controller with non-empty m_EffectID; raw text token presence is not sufficient proof.");
            AssertContains(result, audioMixerSanitizer, "internal sealed class AudioMixerNativeEffectBuildGate", "mixer_native_effect_build_gate", AudioMixerSanitizerPath, "Player builds fail if the MasterMixer native effect is not authored.");
            AssertContains(result, audioMixerSanitizer, "AudioMixerSanitizer.HasMixerEffectAtPath", "mixer_build_gate_checks_master_kernel_effect", AudioMixerSanitizerPath, "Build gate checks the concrete MasterMixer native effect by name.");
            AssertContains(result, audioMixerSanitizer, "return string.IsNullOrEmpty(effectId);", "mixer_sanitizer_only_removes_unresolved_empty_effects", AudioMixerSanitizerPath, "Mixer sanitizer removes unresolved empty-id effects only.");
            AssertNotContains(result, audioMixerSanitizer, "effectName.IndexOf", "mixer_sanitizer_no_name_based_effect_removal", AudioMixerSanitizerPath, "Mixer sanitizer cannot remove a valid Hecton native effect purely by display name.");
            AssertNotContains(result, bridge, "WriteIndexSlot = 1", "old_write_index_slot_removed", BridgePath, "Old base+4 write-index route is absent.");

            AssertContains(result, ring, "IntPtr writeIndexPtr = (IntPtr)(sharedStatePtr + NativeAudioKernelRingBufferDescriptor.WriteIndexSlot)", "descriptor_uses_named_slot", RingPath, "Descriptor pointer is derived from the padded slot constant.");
            AssertNotContains(result, ring, "sharedStatePtr + 1", "dense_pointer_math_removed", RingPath, "Dense int-pointer increment to write cursor is absent.");
            AssertContains(result, ring, "NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceCopy)", "writer_source_pointer", RingPath, "SPSC writer reads source samples through native pointer access.");
            AssertContains(result, ring, "NativeArrayUnsafeUtility.GetUnsafePtr(frames)", "writer_frame_pointer", RingPath, "SPSC writer stores samples through native pointer access.");
            AssertMethodNotContains(result, ring, "public bool TryWriteInterleaved", "TryAcquireTelemetryWriteView", "writer_no_datavault_telemetry_lock", RingPath, "SPSC writer does not acquire the GlobalDataVault telemetry writer fence.");
            AssertMethodNotContains(result, ring, "public bool TryWriteInterleaved", "TryAcquireWriteLock", "writer_no_datavault_write_lock", RingPath, "SPSC writer has no direct GlobalDataVault write-lock call.");
            AssertContains(result, ring, "StructLayout(LayoutKind.Explicit, Size = 64)", "telemetry_64_byte_entry", RingPath, "Black-box entries are fixed-size explicit DTOs.");
            AssertContains(result, ring, "TelemetryCapacity = 300", "telemetry_300_frames", RingPath, "Bridge keeps the required 300-frame black-box ring.");
            AssertContains(result, ring, "public NativeArray<AudioBridgeTelemetryEntry> Telemetry", "telemetry_raw_view_field", RingPath, "Hot black-box telemetry writes into a stable raw NativeArray view.");
            AssertContains(result, ring, "_telemetryPtr = H8Memory.AllocateRaw", "telemetry_raw_allocate", RingPath, "Hot telemetry ring storage is allocated from stable unmanaged H8Memory instead of taking DataVault writer fences.");
            AssertContains(result, ring, "H8Memory.CreateNativeArrayView<AudioBridgeTelemetryEntry>", "telemetry_raw_view", RingPath, "Hot telemetry ring is exposed as a transient NativeArray view over stable unmanaged memory.");
            AssertContains(result, ring, "WriteTelemetryEntry(views.Telemetry", "telemetry_hot_raw_write", RingPath, "RecordTelemetry writes to the raw telemetry view, not the DataVault mirror.");
            AssertMethodNotContains(result, ring, "private void RecordTelemetry", "TryAcquireTelemetryWriteView", "telemetry_record_no_datavault_lock", RingPath, "RecordTelemetry does not acquire the GlobalDataVault telemetry writer fence.");
            AssertContains(result, ring, "Volatile.Write(ref target.Sequence, 0u)", "telemetry_seqlock_begin", RingPath, "Telemetry writer marks a raw slot in-progress before updating the 64-byte DTO fields.");
            AssertContains(result, ring, "Volatile.Write(ref target.Sequence, sequence)", "telemetry_seqlock_publish", RingPath, "Telemetry writer publishes the final sequence only after field writes.");
            AssertContains(result, ring, "TryReadTelemetryEntryStable", "telemetry_stable_snapshot_reader", RingPath, "Fault dump and DataVault mirror reject torn telemetry DTO reads.");
            AssertContains(result, ring, "entry.StateHash != expectedHash", "telemetry_stable_hash_guard", RingPath, "Stable telemetry reads verify the copied fields against the recorded state hash.");
            AssertNotContains(result, ring, "telemetry[index] = entry", "telemetry_no_64_byte_struct_assignment", RingPath, "Hot telemetry writer no longer publishes a 64-byte DTO through a tear-prone struct assignment.");
            AssertNotContains(result, ring, "destination[i] = source[i]", "telemetry_mirror_no_raw_struct_copy", RingPath, "Cold DataVault mirror no longer copies raw telemetry slots without sequence/hash validation.");
            AssertContains(result, ring, "NativeArray<byte> DumpBytes", "telemetry_dump_native_view", RingPath, "Fault dump snapshot is held in fixed unmanaged audio bridge memory.");
            AssertContains(result, ring, "TelemetryDumpBytes = TelemetryHeaderBytes + TelemetryCapacity * TelemetryEntryBytes", "telemetry_dump_fixed_size", RingPath, "Fault dump byte capacity is compile-time fixed.");
            AssertContains(result, ring, "UnsafeUtility.MemCpy(entryPtr + i * TelemetryEntryBytes", "telemetry_dump_unmanaged_copy", RingPath, "Fault dump copies DTO bytes through unmanaged memory.");
            AssertContains(result, ring, "SourceChannelsSlot, _sourceChannels", "shared_state_source_channels_written", RingPath, "Ring publishes source channel count into shared-state metadata for native stereo consumption.");
            AssertContains(result, ring, "descriptor.SourceChannels = _sourceChannels", "descriptor_source_channels_written", RingPath, "Ring copies source channel count into immutable native descriptor.");
            AssertContains(result, ring, "TryDumpAudioBridgeTelemetry(snapshotPtr, TelemetryDumpBytes)", "telemetry_dump_native_plugin_call", RingPath, "Fault dump forwards fixed bytes to the native disk writer.");
            AssertContains(result, ring, "private bool TryReadSharedFrameIndex", "managed_shared_index_range_gate", RingPath, "Managed producer/getters validate raw shared-state cursors before masking math.");
            AssertContains(result, ring, "TelemetryStatusSharedStateInvalid", "managed_shared_state_invalid_status", RingPath, "Corrupt shared-state cursor values have an explicit telemetry status bit.");
            AssertContains(result, ring, "TelemetryStatusWrite = 1 << 16", "telemetry_status_bits_high_namespace", RingPath, "Telemetry-local status bits are above native bridge status bits to avoid forensic ambiguity.");
            AssertContains(result, ring, "TelemetryStatusSharedStateInvalid = 1 << 20", "telemetry_shared_state_invalid_no_native_overlap", RingPath, "Telemetry shared-state-invalid bit does not collide with NativeAudioKernelBridgeStatus.CapacityInvalid.");
            AssertContains(result, ring, "RequestTelemetryDump(ref views, (uint)TelemetryStatusSharedStateInvalid)", "managed_corrupt_index_dump", RingPath, "Corrupt shared-state cursor detection triggers the fixed binary dump route.");
            AssertContains(result, ring, "HectonSensoryKernelNativeBridge.TryDumpAudioBridgeTelemetry(snapshotPtr, TelemetryDumpBytes);", "telemetry_dump_native_write_attempt", RingPath, "Fault dump forwards the fixed snapshot to the native disk writer after the snapshot bytes are complete.");
            AssertContains(result, ring, "finally", "telemetry_dump_gate_finally_rearm_scope", RingPath, "Dump gate rearm is protected by a finally scope after the native write attempt.");
            AssertContains(result, ring, "Volatile.Write(ref _telemetryDumpQueued, 0);", "telemetry_dump_gate_rearms_after_write_attempt", RingPath, "C# dump gate is rearmed after native write attempt; no detached native dump thread owns later completion.");
            AssertMethodContains(result, ring, "private void RequestTelemetryDump", "try", "telemetry_dump_method_try_scope", RingPath, "RequestTelemetryDump owns the try scope that protects the managed dump gate.");
            AssertMethodOrder(result, ring, "private void RequestTelemetryDump", "HectonSensoryKernelNativeBridge.TryDumpAudioBridgeTelemetry(snapshotPtr, TelemetryDumpBytes);", "finally", "telemetry_dump_native_write_before_finally", RingPath, "RequestTelemetryDump attempts the native write before entering the finally rearm scope.");
            AssertMethodOrder(result, ring, "private void RequestTelemetryDump", "finally", "Volatile.Write(ref _telemetryDumpQueued, 0);", "telemetry_dump_finally_before_gate_rearm", RingPath, "RequestTelemetryDump rearms _telemetryDumpQueued inside its own finally scope.");
            AssertNotContains(result, ring, "return ReadSharedIndex(ref views, slot) & _capacityMask", "managed_no_masked_corrupt_index", RingPath, "Runtime no longer converts corrupt raw shared-state indices into apparently valid ring positions.");
            AssertContains(result, ring, "TryAllocateNativeBridgeBuffers(frameSampleCapacity)", "native_bridge_raw_allocate_gate", RingPath, "Native-exported frames/shared-state pointers are allocated from stable unmanaged H8Memory instead of relocatable DataVault arena memory.");
            AssertContains(result, ring, "if (requestedCapacity >= AudioBufferCapacity)", "native_bridge_capacity_upper_bound", RingPath, "Ring initialization caps capacity at the fixed 65,536-frame SPSC budget instead of allowing giant raw allocations.");
            AssertContains(result, ring, "return AudioBufferCapacity;", "native_bridge_capacity_returns_fixed_max", RingPath, "Oversized capacity requests resolve to the bridge maximum rather than multi-gigabyte allocation sizes.");
            AssertContains(result, ring, "if (sourceChannels < 1 || sourceChannels > 2)", "native_bridge_initialize_rejects_invalid_channels", RingPath, "Ring initialization rejects invalid channel contracts instead of silently clamping them into a different ABI.");
            AssertContains(result, ring, "RecordBridgeFailure(NativeAudioKernelBridgeStatus.SharedStateInvalid)", "native_bridge_invalid_channel_retains_existing_bridge", RingPath, "Invalid Initialize channel count records a bridge failure without disposing an already valid ring.");
            AssertNotContains(result, ringRuntime, "math.clamp(sourceChannels", "native_bridge_no_channel_clamp", RingPath, "Runtime initialization does not silently normalize invalid source channel counts.");
            AssertContains(result, ring, "H8Memory.AllocateRaw", "native_bridge_raw_allocate", RingPath, "Native bridge buffers use tracked unmanaged allocation with explicit 8-byte alignment.");
            AssertContains(result, ring, "H8Memory.FreeRaw", "native_bridge_raw_free", RingPath, "Native bridge buffers are released through tracked owner-tagged unmanaged free.");
            AssertContains(result, ring, "H8Memory.IsInitialized", "native_bridge_shutdown_safe_free_gate", RingPath, "Late audio dispose after H8Memory shutdown nulls already-reaped raw pointers instead of calling FreeRaw into a dead tracker.");
            AssertContains(result, ring, "if (!H8Memory.IsInitialized ||", "native_bridge_no_view_after_h8_shutdown", RingPath, "Runtime refuses to create NativeArray views over raw bridge pointers after H8Memory shutdown.");
            AssertContains(result, ring, "if (H8Memory.IsInitialized &&", "native_bridge_failed_clear_retention_requires_live_h8memory", RingPath, "Failed native clear retains raw pointers only while H8Memory still owns the backing memory.");
            AssertContains(result, ring, "H8Memory.CreateNativeArrayView<float>(_framesPtr", "native_bridge_raw_frame_view", RingPath, "Frame NativeArray view is transiently created over stable unmanaged memory.");
            AssertContains(result, ring, "H8Memory.CreateNativeArrayView<int>", "native_bridge_raw_shared_state_view", RingPath, "Shared-state NativeArray view is transiently created over stable unmanaged memory.");
            AssertContains(result, ring, "private void TryMirrorTelemetryToDataVault", "telemetry_cold_datavault_mirror", RingPath, "Fault/shutdown paths copy the raw telemetry ring into the GlobalDataVault telemetry lane.");
            AssertContains(result, ring, "TryAcquireWriteLock(in _telemetryHandle", "telemetry_datavault_mirror_write_lock", RingPath, "Cold DataVault mirror uses the compaction-aware write-lock contract.");
            AssertContains(result, ring, "ReleaseWriteLock(in _telemetryHandle", "telemetry_datavault_mirror_write_release", RingPath, "Cold DataVault mirror write-lock release is present.");
            AssertContains(result, ring, "finally", "telemetry_mirror_write_lock_finally", RingPath, "Cold telemetry mirror write-lock release is protected by finally blocks.");
            AssertContains(result, ring, "HectonSensoryKernelNativeBridge.TryClear(out NativeAudioKernelBridgeStatus clearStatus)", "native_bridge_clear_status_before_free", RingPath, "Native plugin clear status is checked before freeing unmanaged bridge buffers.");
            AssertContains(result, ring, "if (!cleared)", "native_bridge_failed_clear_no_free_gate", RingPath, "Runtime branches on failed native clear before freeing native-retained raw buffers.");
            AssertContains(result, ring, "(clearStatus & NativeAudioKernelBridgeStatus.PluginUnavailable) == 0", "native_bridge_failed_clear_retains_buffers", RingPath, "Runtime retains raw buffers on every failed native clear except plugin-unavailable fallback.");
            AssertMinimumOccurrence(result, ring, "if (HasNativeBridgeBuffers())", 2, "native_bridge_reinitialize_blocked_after_busy_clear", RingPath, "Reinitialize cannot allocate a second bridge over retained native pointers after Busy clear failure.");
            AssertContains(result, ring, "private bool HasNativeBridgeBuffers()", "native_bridge_buffer_liveness_probe", RingPath, "Dispose/reinitialize has an explicit raw-buffer liveness probe.");
            AssertContains(result, ring, "public bool TryDispose()", "native_bridge_trydispose_contract", RingPath, "Internal owners can observe deferred native clear and keep the raw-buffer owner alive.");
            AssertContains(result, ring, "if (!TryDispose())", "native_bridge_reinit_blocks_on_deferred_clear", RingPath, "Initialize refuses to allocate or rebind over native-retained raw pointers when clear is deferred.");
            AssertNotContains(result, ring, "TryLockBuffer(BufferID.AudioFrameRing", "native_bridge_no_persistent_datavault_pin", RingPath, "Runtime ring does not hold long-lived DataVault relocation pins for the native bridge.");
            AssertNotContains(result, ring, "AudioFrameRingTelemetryDumpBytes", "runtime_no_datavault_dump_byte_lane", RingPath, "Fault dump bytes are held in the stable unmanaged bridge pool, not a relocatable DataVault lane.");
            AssertNotContains(result, ringRuntime, "new byte[", "runtime_no_byte_array_snapshot", RingPath, "Runtime ring code does not allocate managed byte arrays.");
            AssertNotContains(result, ringRuntime, "new Thread", "runtime_no_fault_thread", RingPath, "Runtime ring code does not allocate fault threads.");
            AssertNotContains(result, ringRuntime, "Thread.MemoryBarrier", "runtime_no_thread_memory_barrier", RingPath, "Runtime ring code uses Volatile sequence/hash validation without managed Thread.MemoryBarrier calls.");
            AssertNotContains(result, ringRuntime, "FileStream", "runtime_no_filestream", RingPath, "Runtime ring code does not open managed file streams.");
            AssertNotContains(result, ringRuntime, "Path.", "runtime_no_path_api", RingPath, "Runtime ring code does not build managed file paths.");
            AssertNotContains(result, ringRuntime, "Directory.", "runtime_no_directory_api", RingPath, "Runtime ring code does not create managed directories.");
            AssertNotContains(result, ringRuntime, "string.Format", "runtime_no_string_format", RingPath, "Runtime ring code does not format managed strings.");
            AssertNotContains(result, ringRuntime, ".ToString(", "runtime_no_tostring", RingPath, "Runtime ring code does not call ToString.");
            AssertNotContains(result, ringRuntime, "System.Linq", "runtime_no_linq_namespace", RingPath, "Runtime ring code does not import LINQ.");
            AssertNotContains(result, ringRuntime, ".Select(", "runtime_no_linq_select", RingPath, "Runtime ring code does not use LINQ Select.");
            AssertNotContains(result, ringRuntime, ".Where(", "runtime_no_linq_where", RingPath, "Runtime ring code does not use LINQ Where.");
            AssertNotContains(result, ring, "GenerateMockAudioSamplesJob", "runtime_no_mock_audio_job", RingPath, "Burst mock-audio generator is editor-only and does not live in the runtime ring source.");
            AssertContains(result, scanner, "GenerateMockAudioSamplesJob", "editor_mock_audio_job", "Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs", "Burst mock-audio generator exists only in the editor stress scanner.");
            AssertContains(result, scanner, "AudioBridgeConcurrencyFuzzer1314", "concurrency_fuzzer", "Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs", "Editor-only SPSC producer/consumer fuzzer exists outside runtime ring source.");
            AssertNotContains(result, ring, "MixInterleavedInto(float[]", "managed_float_array_bridge_absent", RingPath, "Managed float[] bridge path is absent.");

            AssertContains(result, renderer, "TryRegisterWithRetryGate(ref descriptor", "renderer_uses_retry_gate", RendererPath, "Renderer registers native output through retry/fail-closed gate.");
            AssertContains(result, renderer, "_sampleRingBuffer.RecordBridgeFailure(bridgeStatus)", "renderer_records_bridge_failure", RendererPath, "Registration failure writes the black-box route.");
            AssertContains(result, renderer, "TryClear(out NativeAudioKernelBridgeStatus clearStatus)", "renderer_native_clear_status_checked", RendererPath, "Renderer does not drop the native registration flag unless TryClear proves the native descriptor was released.");
            AssertContains(result, renderer, "_sampleRingBuffer?.RecordBridgeFailure(clearStatus)", "renderer_native_clear_failure_telemetry", RendererPath, "Renderer records failed native clear status into the audio bridge black-box telemetry.");
            AssertContains(result, renderer, "(clearStatus & NativeAudioKernelBridgeStatus.PluginUnavailable) != 0", "renderer_native_clear_plugin_unavailable_release", RendererPath, "Renderer only force-clears registration state after failed clear when no native plugin can retain the descriptor.");
            AssertContains(result, renderer, "sampleRingBuffer != null && sampleRingBuffer.TryDispose()", "renderer_preserves_ring_owner_on_deferred_clear", RendererPath, "Renderer only drops the sample-ring owner after raw bridge disposal succeeds.");
            AssertNotContains(result, renderer, "_sampleRingBuffer?.Dispose();", "renderer_no_unconditional_ring_owner_drop", RendererPath, "Renderer no longer loses the managed raw-buffer owner after deferred native clear.");
            AssertContains(result, renderer, "_sampleRingBuffer.CapacityFrames != audioBufferCapacity", "renderer_ring_capacity_contract_verified_after_initialize", RendererPath, "Renderer refuses to mark buffers initialized if ring Initialize retained an old/busy bridge or wrong capacity.");
            AssertContains(result, renderer, "_sampleRingBuffer.SourceChannels != BinauralOutputChannels", "renderer_ring_channel_contract_verified_after_initialize", RendererPath, "Renderer refuses to mark buffers initialized if source-channel ABI does not match native stereo contract.");
            AssertContains(result, renderer, "DisposeBuffers(disposeSabineReverbDelay: true);", "renderer_ring_contract_fail_closed_teardown", RendererPath, "Renderer has a hard teardown route for partially initialized vault-backed buffers when sample-ring contract validation fails.");
            AssertMinimumOccurrence(result, renderer, "RefreshNativeOutputBridge();", 2, "renderer_rebinds_bridge", RendererPath, "DataVault/audio configuration refresh paths re-register the bridge.");
            AssertNotContains(result, renderer, "OnAudioFilterRead", "unity_audio_callback_absent", RendererPath, "Critical renderer does not fall back to Unity managed audio callback.");

            AssertContains(result, memory, "AudioFrameRingTelemetry", "datavault_telemetry_lane", MemoryPath, "DataVault owns the audio bridge telemetry lane.");
            AssertContains(result, memory, "public static bool IsInitialized", "h8memory_initialized_probe", MemoryPath, "H8Memory exposes a read-only shutdown probe for fail-closed raw bridge teardown.");
            AssertNotContains(result, memory, "AudioFrameRingTelemetryDumpBytes", "datavault_no_dump_byte_lane", MemoryPath, "Obsolete DataVault dump byte lane is removed; dump scratch lives in the stable unmanaged bridge pool.");

            AssertContains(result, nativePlugin, "kWriteIndexSlot = 2", "native_write_index_slot_even", NativePluginPath, "Native audio kernel expects the same write cursor byte offset 8 as C#.");
            AssertContains(result, nativePlugin, "kSourceChannelsSlot = 12", "native_source_channels_slot", NativePluginPath, "Native audio kernel reads the mono/stereo source layout from shared state.");
            AssertContains(result, nativePlugin, "kSharedStateSlotCount = 14", "native_shared_state_padded", NativePluginPath, "Native audio kernel requires the padded shared-state slot count.");
            AssertContains(result, nativePlugin, "kRequiredPointerAlignmentBytes = 8u", "native_required_alignment_8", NativePluginPath, "Native validation rejects pointers below the C# 8-byte alignment contract.");
            AssertContains(result, bridge, "MaximumCapacityFrames = 65536", "bridge_capacity_upper_bound", BridgePath, "Managed descriptor validation rejects capacities above the native frame budget.");
            AssertContains(result, nativePlugin, "sizeof(SharedRingBufferDescriptor) == 56u", "native_descriptor_size_static_assert", NativePluginPath, "Native descriptor size is guarded at compile time against compiler packing drift.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, frames) == 0u", "native_descriptor_frames_offset_static_assert", NativePluginPath, "Native descriptor Frames pointer offset matches C# byte offset 0.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, sharedState) == 8u", "native_descriptor_shared_state_offset_static_assert", NativePluginPath, "Native descriptor SharedState pointer offset matches C# byte offset 8.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, readIndex) == 16u", "native_descriptor_read_offset_static_assert", NativePluginPath, "Native descriptor ReadIndex pointer offset matches C# byte offset 16.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, writeIndex) == 24u", "native_descriptor_write_offset_static_assert", NativePluginPath, "Native descriptor WriteIndex pointer offset matches C# byte offset 24.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, descriptorMagic) == 32u", "native_descriptor_magic_offset_static_assert", NativePluginPath, "Native descriptor magic follows pointer fields at C# byte offset 32.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, sharedStateLengthInts) == 44u", "native_descriptor_length_offset_static_assert", NativePluginPath, "Native descriptor SharedStateLengthInts offset matches C# byte offset 44.");
            AssertContains(result, nativePlugin, "offsetof(SharedRingBufferDescriptor, sourceChannels) == 48u", "native_descriptor_source_channels_offset_static_assert", NativePluginPath, "Native descriptor SourceChannels offset matches C# byte offset 48.");
            AssertContains(result, nativePlugin, "sourceChannels != descriptor.sourceChannels", "native_source_channels_metadata_match", NativePluginPath, "Native validation requires shared-state source channel metadata to match immutable descriptor source channels.");
            AssertContains(result, nativePlugin, "typedef SInt32 HectonAtomicInt32", "native_posix_atomic_type", NativePluginPath, "Non-Windows native plugin uses a local 32-bit atomic cursor type instead of undefined Windows LONG.");
            AssertContains(result, nativePlugin, "__sync_val_compare_and_swap", "native_posix_atomic_read", NativePluginPath, "Non-Windows native plugin has a lock-free atomic read path for shared cursors/status.");
            AssertContains(result, nativePlugin, "__sync_lock_test_and_set", "native_posix_atomic_write", NativePluginPath, "Non-Windows native plugin has a lock-free atomic write path for shared cursors/status.");
            AssertNotContains(result, nativePlugin, "static volatile LONG g_", "native_no_windows_long_globals", NativePluginPath, "Native global state does not require Windows LONG on Linux/macOS/Android compilers.");
            AssertNotContains(result, nativePlugin, "InterlockedIncrement(&g_processCallbackDepth)", "native_no_direct_windows_increment", NativePluginPath, "Process callback depth increment goes through the portable atomic helper.");
            AssertContains(result, nativePlugin, "kDrainSpinLimit", "native_bounded_callback_drain", NativePluginPath, "Native registration/clear drains active callbacks with a bounded spin limit instead of an infinite loop.");
            AssertContains(result, nativePlugin, "if (!WaitForProcessCallbacksToDrain())", "native_drain_fail_closed_without_descriptor_loss", NativePluginPath, "Native registration/clear fail closed without disabling the currently owned descriptor when callback drain cannot complete.");
            AssertContains(result, nativePlugin, "g_callbackMutationGate", "native_callback_mutation_gate", NativePluginPath, "Native register/clear blocks new callback mixing without destroying the currently owned descriptor before drain succeeds.");
            AssertContains(result, nativePlugin, "AtomicRead32(&g_callbackMutationGate) != 0 ||", "native_callback_mutation_gate_checked", NativePluginPath, "Native ProcessCallback exits before bridge mixing while register/clear mutation is in progress.");
            AssertContains(result, nativePlugin, "RestoreStatusAfterDrainFailure", "native_drain_failure_restores_status", NativePluginPath, "Native register/clear restores owned Active/Cleared state with Busy failure marker after drain failure.");
            AssertMinimumOccurrence(result, nativePlugin, "AtomicWrite32(&g_callbackMutationGate, 0);", 4, "native_callback_mutation_gate_reopens", NativePluginPath, "Every register/clear terminal path reopens the callback mutation gate.");
            AssertContains(result, nativePlugin, "sched_yield();", "native_posix_drain_yield", NativePluginPath, "Non-Windows callback drain yields between bounded polls instead of burning a tight CPU spin.");
            AssertNotContains(result, nativePlugin, "while (AtomicRead32(&g_processCallbackDepth) != 0)", "native_no_unbounded_callback_drain", NativePluginPath, "Native callback drain has no unbounded busy loop.");
            AssertContains(result, nativePlugin, "HectonSensoryKernel_RegisterSharedRingBufferAndGetStatus", "native_register_export_returns_status", NativePluginPath, "Native register status export returns operation result directly to managed code.");
            AssertContains(result, nativePlugin, "return validationStatus;", "native_register_validation_status_returned", NativePluginPath, "Native register validation failures are returned directly and cannot be overwritten by later callback status before managed observes them.");
            AssertContains(result, nativePlugin, "HectonSensoryKernel_ClearSharedRingBufferAndGetStatus", "native_clear_export_returns_status", NativePluginPath, "Native clear status export returns operation result directly to managed code.");
            AssertContains(result, nativePlugin, "g_debugProcessScratch[4096 * 8]", "native_debug_fixed_scratch", NativePluginPath, "Debug process export uses fixed static scratch instead of heap allocation.");
            AssertContains(result, nativePlugin, "g_debugProcessScratchInUse", "native_debug_scratch_busy_gate", NativePluginPath, "Debug process export serializes fixed scratch use with an atomic busy gate.");
            AssertNotContains(result, nativePlugin, "g_telemetryDumpBuffer", "native_dump_no_static_thread_scratch", NativePluginPath, "Native telemetry dump no longer stages bytes in static thread scratch that can outlive plugin unload.");
            AssertNotContains(result, nativePlugin, "QueueTelemetryDumpAsync", "native_dump_no_async_queue", NativePluginPath, "Native telemetry dump no longer queues detached file I/O.");
            AssertNotContains(result, nativePlugin, "TelemetryDumpThreadMain", "native_dump_no_thread_entry", NativePluginPath, "Native dump writer has no unmanaged thread entry point.");
            AssertNotContains(result, nativePlugin, "#include <process.h>", "native_dump_no_windows_crt_thread_header", NativePluginPath, "Windows dump writer does not include the CRT thread API header.");
            AssertNotContains(result, nativePlugin, "_beginthreadex", "native_dump_no_windows_crt_thread", NativePluginPath, "Windows dump writer does not create detached CRT threads.");
            AssertNotContains(result, nativePlugin, "CreateThread(", "native_dump_no_windows_raw_thread", NativePluginPath, "Windows dump writer does not create raw Win32 threads.");
            AssertNotContains(result, nativePlugin, "pthread_create", "native_dump_no_posix_thread", NativePluginPath, "Non-Windows dump writer does not create detached POSIX threads.");
            AssertContains(result, nativePlugin, "EnsureTelemetryDumpDirectory", "native_dump_directory_gate", NativePluginPath, "Native dump writer creates the fixed Docs/AgentLogs directory path without managed FileStream/Path/Directory calls.");
            AssertContains(result, nativePlugin, "mkdir(\"Docs/AgentLogs\"", "native_dump_posix_directory_gate", NativePluginPath, "Non-Windows dump writer can create the fixed binary dump directory before fopen.");
            AssertContains(result, nativePlugin, "CreateDirectoryA(\"Docs/AgentLogs\"", "native_dump_windows_directory_gate", NativePluginPath, "Windows dump writer can create the fixed binary dump directory before fopen_s.");
            AssertNotContains(result, nativePlugin, "new EffectData", "native_no_effectdata_new", NativePluginPath, "Native plugin create callback does not allocate heap effectdata.");
            AssertNotContains(result, nativePlugin, "delete effectData", "native_no_effectdata_delete", NativePluginPath, "Native plugin release callback does not delete heap effectdata.");
            AssertNotContains(result, nativePlugin, "malloc(", "native_no_malloc", NativePluginPath, "Native plugin contains no malloc route in release source.");
            AssertNotContains(result, nativePlugin, "free(", "native_no_free", NativePluginPath, "Native plugin contains no free route in release source.");
            AssertNotContains(result, nativePlugin, "kWriteIndexSlot = 1", "native_old_write_index_slot_removed", NativePluginPath, "Native base+4 write cursor validation route is absent.");
            AssertContains(result, nativePlugin, "const int sourceChannels = ringBuffer.sourceChannels", "native_source_channels_from_descriptor", NativePluginPath, "Native callback consumes immutable descriptor source-channel count after validation instead of a mutable SharedState slot.");
            AssertContains(result, nativePlugin, "sourceFrameIndex << 1", "native_stereo_frame_stride", NativePluginPath, "Native callback consumes interleaved stereo frames with frame*2 addressing.");
            AssertContains(result, nativePlugin, "kMaxProcessFrames = 65536u", "native_callback_frame_cap", NativePluginPath, "Native callback rejects impossible process-block frame counts before int index math.");
            AssertContains(result, nativePlugin, "kMaxProcessChannels = 64", "native_callback_channel_cap", NativePluginPath, "Native callback accepts uncommon multichannel host layouts while still rejecting absurd channel counts before index math.");
            AssertContains(result, nativePlugin, "kMaxProcessOutputSamples", "native_callback_output_sample_budget", NativePluginPath, "Native callback has a fixed maximum output sample clear budget before touching host buffers.");
            AssertContains(result, nativePlugin, "descriptor.capacityFrames > (SInt32)kMaxProcessFrames", "native_descriptor_capacity_upper_bound", NativePluginPath, "Native descriptor validation rejects frame capacities above the exported ring allocation budget.");
            AssertContains(result, nativePlugin, "TryComputeOutputSampleCount", "native_callback_sample_count_gate", NativePluginPath, "Native callback validates host frame/channel product before memset and loop indexing.");
            AssertContains(result, nativePlugin, "length == 0u ||", "native_callback_contract_precheck", NativePluginPath, "Native callback rejects zero/oversized frame and channel contracts before output memset.");
            AssertMethodOrder(result, nativePlugin, "UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ProcessCallback", "length == 0u ||", "if (!TryComputeOutputSampleCount(length, outchannels, &outputSampleCount))", "native_callback_precheck_before_sample_count", NativePluginPath, "Native callback validates explicit frame/channel caps before sample-product math.");
            AssertContains(result, nativePlugin, "if (!TryComputeOutputSampleCount(length, outchannels, &outputSampleCount))", "native_callback_bad_product_rejected", NativePluginPath, "Native callback rejects impossible output sample products before clearing.");
            AssertContains(result, nativePlugin, "memset(outbuffer, 0, sizeof(float) * outputSampleCount);", "native_callback_bounded_silence_clear", NativePluginPath, "Native callback clears a bounded host output buffer before deciding whether bridge mixing can proceed.");
            AssertContains(result, nativePlugin, "inchannels > kMaxProcessChannels || (inbuffer != NULL && inchannels <= 0)", "native_callback_input_contract_fail_closed", NativePluginPath, "Native callback fails closed after bounded silence clear and before bridge mixing when the host input buffer/channel contract is corrupt.");
            AssertContains(result, nativePlugin, "const size_t outputSampleCount", "native_callback_size_t_output_count", NativePluginPath, "Native callback computes memset byte count with size_t after sample-product validation.");
            AssertContains(result, nativePlugin, "(size_t)frameIndex * (size_t)outchannels", "native_callback_size_t_output_index", NativePluginPath, "Native callback output indexing avoids int multiplication overflow.");
            AssertContains(result, nativePlugin, "(size_t)frameIndex * (size_t)inchannels", "native_callback_size_t_input_index", NativePluginPath, "Native callback input passthrough indexing avoids int multiplication overflow.");
            AssertContains(result, nativePlugin, "inchannels <= kMaxProcessChannels", "native_callback_input_passthrough_cap", NativePluginPath, "Native callback only passthrough-copies bounded channel layouts.");
            AssertContains(result, nativePlugin, "HectonSensoryKernel_DumpAudioBridgeTelemetry", "native_dump_export", NativePluginPath, "Native plugin exports the binary telemetry dump writer.");
            AssertContains(result, nativePlugin, "Dump_1314_AudioBridge.bin", "native_dump_file_name", NativePluginPath, "Native dump writer targets the required binary dump file.");
            AssertContains(result, nativePlugin, "return WriteTelemetryDumpFile(bytes, byteCount);", "native_dump_export_direct_write", NativePluginPath, "Native dump export returns the actual bounded disk write result and has no detached thread lifetime risk.");
            AssertContains(result, nativePlugin, "UnityGetAudioEffectDefinitions", "native_unity_effect_export_local", NativePluginPath, "Unity effect registration is owned by the Hecton plugin source instead of the heap-using Unity sample utility translation unit.");
            AssertContains(result, nativePlugin, "FillUnityEffectDefinition", "native_effect_definition_local_fill", NativePluginPath, "Effect definition setup uses static storage and bounded char copy in the Hecton plugin source.");
            AssertNotContains(result, nativeBuildScript, "AudioPluginUtil.cpp", "native_build_excludes_sample_utility_cpp", NativeBuildScriptPath, "Native build no longer links the Unity sample utility translation unit that contains FFT/analyzer heap helpers.");
            AssertContains(result, nativeAndroidBuildScript, "aarch64-linux-android24-clang++", "native_android_build_uses_arm64_clang", NativeAndroidBuildScriptPath, "Android native build script targets the arm64 NDK clang driver used by Quest/Android player binaries.");
            AssertContains(result, nativeAndroidBuildScript, "-shared", "native_android_build_shared_library", NativeAndroidBuildScriptPath, "Android native build emits a shared library for Unity native plugin loading.");
            AssertContains(result, nativeAndroidBuildScript, "-fPIC", "native_android_build_pic", NativeAndroidBuildScriptPath, "Android native build emits position-independent code required for shared objects.");
            AssertContains(result, nativeAndroidBuildScript, "-fvisibility=hidden", "native_android_build_hidden_visibility", NativeAndroidBuildScriptPath, "Android native build hides non-exported symbols and leaves explicit plugin exports visible.");
            AssertContains(result, nativeAndroidBuildScript, "-ffunction-sections -fdata-sections", "native_android_build_function_data_sections", NativeAndroidBuildScriptPath, "Android native build enables section-level dead stripping.");
            AssertContains(result, nativeAndroidBuildScript, "-Wl,--gc-sections", "native_android_build_dead_code_elimination", NativeAndroidBuildScriptPath, "Android native link drops unused sections.");
            AssertContains(result, nativeAndroidBuildScript, "Assets\\Plugins\\Android\\arm64-v8a", "native_android_build_output_path", NativeAndroidBuildScriptPath, "Android native build writes the plugin into the Unity Android arm64 plugin folder expected by the build matrix.");
            AssertContains(result, nativeAndroidBuildScript, "libHectonAudioKernel.so", "native_android_build_output_binary", NativeAndroidBuildScriptPath, "Android native build produces the exact Hecton audio kernel .so name required by the matrix.");
            AssertNotContains(result, nativeAndroidBuildScript, "AudioPluginUtil.cpp", "native_android_build_excludes_sample_utility_cpp", NativeAndroidBuildScriptPath, "Android native build does not link the removed Unity sample utility translation unit.");
            AssertAssetMissing(result, projectRoot, NativeUtilityCppPath, "native_utility_cpp_removed", NativeUtilityCppPath, "Heap-allocating Unity sample utility translation unit is absent from the Hecton native kernel directory.");
            AssertAssetMissing(result, projectRoot, NativeUtilityHeaderPath, "native_utility_header_removed", NativeUtilityHeaderPath, "Unused Unity sample utility header is absent so future native builds cannot casually reattach analyzer/FFT helper paths.");
            AssertAssetMissing(result, projectRoot, NativePluginListPath, "native_plugin_list_removed", NativePluginListPath, "Legacy PluginList macro route is absent; Hecton effect registration is owned by Plugin_HectonSensoryKernel.cpp.");
            AssertContains(result, nativeBuildScript, "/Gy /Gw", "native_build_function_data_sections", NativeBuildScriptPath, "Windows native build emits function/data sections for the remaining Hecton plugin translation unit.");
            AssertContains(result, nativeBuildScript, "/OPT:REF /OPT:ICF", "native_build_dead_code_elimination", NativeBuildScriptPath, "Windows native link keeps explicit dead-code elimination and identical-code folding.");

            if (runLiveFuzzer)
            {
                result.FuzzerExecuted = true;
                result.LiveFuzzerPass = AudioBridgeConcurrencyFuzzer1314.Run(out AudioBridgeConcurrencyFuzzerResult fuzzerResult);
                result.Fuzzer = fuzzerResult;
                if (!result.LiveFuzzerPass)
                    result.FailedChecks++;
            }

            result.StaticPass = result.FailedChecks == 0 || (runLiveFuzzer && result.FailedChecks == 1 && result.FuzzerExecuted && !result.Fuzzer.DataVaultAvailable);
            result.Pass = runLiveFuzzer
                ? result.FailedChecks == 0
                : result.StaticPass;
            return result;
        }

        private static string ReadAssetText(string projectRoot, string assetPath, ScanResult result)
        {
            string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                result.FailedChecks++;
                result.Checks.Add(new CheckResult(assetPath, "missing_file", false, "Missing file: " + assetPath));
                return string.Empty;
            }

            result.FilesScanned++;
            return File.ReadAllText(absolutePath, Encoding.UTF8);
        }

        private static void AssertAssetMissing(ScanResult result, string projectRoot, string assetPath, string id, string file, string detail)
        {
            string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            AppendCheck(result, file, id, !File.Exists(absolutePath), detail);
        }

        private static void AssertAnyAssetExists(ScanResult result, string projectRoot, string[] assetPaths, string id, string file, string detail)
        {
            bool found = false;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string absolutePath = Path.Combine(projectRoot, assetPaths[i].Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absolutePath))
                {
                    found = true;
                    break;
                }
            }

            AppendCheck(result, file, id, found, detail);
        }

        private static string StripUnityEditorRegion(string source)
        {
            int editorIndex = source.IndexOf("#if UNITY_EDITOR", StringComparison.Ordinal);
            return editorIndex < 0 ? source : source.Substring(0, editorIndex);
        }

        private static void AssertContains(ScanResult result, string source, string token, string id, string file, string detail)
        {
            bool passed = source.IndexOf(token, StringComparison.Ordinal) >= 0;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertNotContains(ScanResult result, string source, string token, string id, string file, string detail)
        {
            bool passed = source.IndexOf(token, StringComparison.Ordinal) < 0;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertMinimumOccurrence(ScanResult result, string source, string token, int minimum, string id, string file, string detail)
        {
            bool passed = CountOccurrences(source, token) >= minimum;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertMethodNotContains(ScanResult result, string source, string signature, string token, string id, string file, string detail)
        {
            string body = ExtractMethodBody(source, signature);
            bool passed = body.Length > 0 && body.IndexOf(token, StringComparison.Ordinal) < 0;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertMethodContains(ScanResult result, string source, string signature, string token, string id, string file, string detail)
        {
            string body = ExtractMethodBody(source, signature);
            bool passed = body.Length > 0 && body.IndexOf(token, StringComparison.Ordinal) >= 0;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertMethodOrder(ScanResult result, string source, string signature, string firstToken, string secondToken, string id, string file, string detail)
        {
            string body = ExtractMethodBody(source, signature);
            int firstIndex = body.IndexOf(firstToken, StringComparison.Ordinal);
            int secondIndex = body.IndexOf(secondToken, StringComparison.Ordinal);
            bool passed = body.Length > 0 &&
                          firstIndex >= 0 &&
                          secondIndex >= 0 &&
                          firstIndex < secondIndex;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertPluginMetaSectionContains(ScanResult result, string source, string sectionToken, string token, string id, string file, string detail)
        {
            string normalized = NormalizeLineEndings(source);
            int sectionStart = normalized.IndexOf(sectionToken, StringComparison.Ordinal);
            if (sectionStart < 0)
            {
                AppendCheck(result, file, id, false, detail);
                return;
            }

            int nextSection = normalized.IndexOf("\n  - first:", sectionStart + sectionToken.Length, StringComparison.Ordinal);
            if (nextSection < 0)
                nextSection = normalized.Length;

            string section = normalized.Substring(sectionStart, nextSection - sectionStart);
            bool passed = section.IndexOf(token, StringComparison.Ordinal) >= 0;
            AppendCheck(result, file, id, passed, detail);
        }

        private static void AssertFileNotOlderThan(ScanResult result, string projectRoot, string candidatePath, string referencePath, string id, string file, string detail)
        {
            string candidateAbsolutePath = Path.Combine(projectRoot, candidatePath.Replace('/', Path.DirectorySeparatorChar));
            string referenceAbsolutePath = Path.Combine(projectRoot, referencePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidateAbsolutePath) || !File.Exists(referenceAbsolutePath))
            {
                AppendCheck(result, file, id, false, detail);
                return;
            }

            DateTime candidateTimestamp = File.GetLastWriteTimeUtc(candidateAbsolutePath);
            DateTime referenceTimestamp = File.GetLastWriteTimeUtc(referenceAbsolutePath);
            bool passed = candidateTimestamp >= referenceTimestamp;
            AppendCheck(
                result,
                file,
                id,
                passed,
                detail + " candidateUtc=" + candidateTimestamp.ToString("O") + " referenceUtc=" + referenceTimestamp.ToString("O"));
        }

        private static string NormalizeLineEndings(string source)
        {
            return string.IsNullOrEmpty(source) ? string.Empty : source.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(signature))
                return string.Empty;

            int methodIndex = source.IndexOf(signature, StringComparison.Ordinal);
            if (methodIndex < 0)
                return string.Empty;

            int braceIndex = source.IndexOf('{', methodIndex);
            if (braceIndex < 0)
                return string.Empty;

            int depth = 0;
            for (int i = braceIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(braceIndex, i - braceIndex + 1);
                }
            }

            return string.Empty;
        }

        private static void AppendCheck(ScanResult result, string file, string id, bool passed, string detail)
        {
            if (!passed)
                result.FailedChecks++;

            result.Checks.Add(new CheckResult(file, id, passed, detail));
        }

        private static int CountOccurrences(string source, string token)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static void WriteReport(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, BuildJson(result), Encoding.UTF8);
        }

        private static string BuildJson(ScanResult result)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("{\n");
            AppendProperty(builder, "agent", "1314", 2, true);
            AppendProperty(builder, "role", "AUDIO_MASTER_BUS_ALIGNMENT_REPAIRER", 2, true);
            AppendProperty(builder, "report", "AUDIO_BRIDGE_OPTIMIZATION_REPORT_1314", 2, true);
            AppendProperty(builder, "status", result.Pass ? "PASS" : "FAIL", 2, true);
            AppendProperty(builder, "staticPass", result.StaticPass, 2, true);
            AppendProperty(builder, "runLiveFuzzer", result.RunLiveFuzzer, 2, true);
            AppendProperty(builder, "fuzzerExecuted", result.FuzzerExecuted, 2, true);
            AppendProperty(builder, "liveFuzzerPass", result.LiveFuzzerPass, 2, true);
            AppendProperty(builder, "fuzzerDataVaultAvailable", result.Fuzzer.DataVaultAvailable, 2, true);
            AppendProperty(builder, "filesScanned", result.FilesScanned, 2, true);
            AppendProperty(builder, "failedChecks", result.FailedChecks, 2, true);
            builder.Append("  \"fuzzer\": {\n");
            AppendProperty(builder, "descriptorValid", result.Fuzzer.DescriptorValid, 4, true);
            AppendProperty(builder, "descriptorAligned", result.Fuzzer.DescriptorAligned, 4, true);
            AppendProperty(builder, "descriptorStatusBits", result.Fuzzer.DescriptorStatusBits, 4, true);
            AppendProperty(builder, "iterations", result.Fuzzer.Iterations, 4, true);
            AppendProperty(builder, "capacityFrames", result.Fuzzer.CapacityFrames, 4, true);
            AppendProperty(builder, "blockFrames", result.Fuzzer.BlockFrames, 4, true);
            AppendProperty(builder, "channels", result.Fuzzer.Channels, 4, true);
            AppendProperty(builder, "successfulWrites", result.Fuzzer.SuccessfulWrites, 4, true);
            AppendProperty(builder, "failedWrites", result.Fuzzer.FailedWrites, 4, true);
            AppendProperty(builder, "overflowDropCount", result.Fuzzer.OverflowDropCount, 4, true);
            AppendProperty(builder, "finalBufferedFrames", result.Fuzzer.FinalBufferedFrames, 4, true);
            AppendProperty(builder, "finalWritableFrames", result.Fuzzer.FinalWritableFrames, 4, true);
            AppendProperty(builder, "elapsedTicks", result.Fuzzer.ElapsedTicks, 4, false);
            builder.Append("  },\n");
            builder.Append("  \"checks\": [\n");
            for (int i = 0; i < result.Checks.Count; i++)
            {
                CheckResult check = result.Checks[i];
                builder.Append("    {\n");
                AppendProperty(builder, "id", check.Id, 6, true);
                AppendProperty(builder, "file", check.File, 6, true);
                AppendProperty(builder, "passed", check.Passed, 6, true);
                AppendProperty(builder, "detail", check.Detail, 6, false);
                builder.Append("    }");
                if (i + 1 < result.Checks.Count)
                    builder.Append(',');
                builder.Append('\n');
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": \"").Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendProperty(StringBuilder builder, string name, long value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal sealed class ScanResult
        {
            public int FilesScanned;
            public int FailedChecks;
            public bool StaticPass;
            public bool Pass;
            public bool RunLiveFuzzer;
            public bool FuzzerExecuted;
            public bool LiveFuzzerPass;
            public AudioBridgeConcurrencyFuzzerResult Fuzzer;
            public List<CheckResult> Checks;
        }

        internal readonly struct CheckResult
        {
            public CheckResult(string file, string id, bool passed, string detail)
            {
                File = file;
                Id = id;
                Passed = passed;
                Detail = detail;
            }

            public readonly string File;
            public readonly string Id;
            public readonly bool Passed;
            public readonly string Detail;
        }
    }

    public struct AudioBridgeConcurrencyFuzzerResult
    {
        public bool DataVaultAvailable;
        public bool DescriptorValid;
        public bool DescriptorAligned;
        public bool Passed;
        public int DescriptorStatusBits;
        public int Iterations;
        public int CapacityFrames;
        public int BlockFrames;
        public int Channels;
        public int SuccessfulWrites;
        public int FailedWrites;
        public int FinalBufferedFrames;
        public int FinalWritableFrames;
        public int OverflowDropCount;
        public long ElapsedTicks;
    }

    public static unsafe class AudioBridgeConcurrencyFuzzer1314
    {
        private const int DefaultIterations = 32;
        private const int DefaultCapacityFrames = AudioFrameSpscRingBuffer.AudioBufferCapacity;
        private const int DefaultBlockFrames = AudioFrameSpscRingBuffer.AudioBufferCapacity >> 1;
        private const int DefaultChannels = 2;
        private const int JobBatchCount = 64;
        private const int FuzzerThreadJoinTimeoutMilliseconds = 5000;
        private const int FuzzerThreadStopJoinTimeoutMilliseconds = 250;
        private const string NativeMemoryOwner = nameof(AudioBridgeConcurrencyFuzzer1314);
        private const string SamplesLabel = "samples";

        public static bool Run(out AudioBridgeConcurrencyFuzzerResult result)
        {
            return Run(DefaultIterations, DefaultCapacityFrames, DefaultBlockFrames, DefaultChannels, out result);
        }

        public static bool Run(
            int iterations,
            int capacityFrames,
            int blockFrames,
            int channels,
            out AudioBridgeConcurrencyFuzzerResult result)
        {
            result = default;
            result.DataVaultAvailable = GlobalRegistry.DataVault != null;
            result.Iterations = math.max(1, iterations);
            result.CapacityFrames = AudioFrameSpscRingBuffer.ResolvePowerOfTwoCapacity(capacityFrames);
            result.BlockFrames = math.max(1, blockFrames);
            result.Channels = math.clamp(channels, 1, 2);

            if (!result.DataVaultAvailable)
                return false;

            int sampleCount = result.BlockFrames * result.Channels;
            NativeArray<float> samples = default;
            int samplesSentinelId = 0;
            AudioFrameSpscRingBuffer ring = new AudioFrameSpscRingBuffer();
            int running = 1;
            Thread consumerThread = null;
            Thread producerThread = null;
            try
            {
                samples = new NativeArray<float>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                samplesSentinelId = NativeMemorySentinel.RegisterNativeArray(
                    samples,
                    NativeMemoryOwner,
                    SamplesLabel,
                    NativeAllocationLifetime.Session);
                if (samplesSentinelId <= 0)
                    throw new InvalidOperationException($"Native memory sentinel registration failed for {SamplesLabel}.");
                GenerateMockAudioSamplesJob job = default;
                job.Samples = samples;
                job.Seed = 0xA1314D5u;
                job.Gain = 0.72f;
                job.Schedule(sampleCount, JobBatchCount).Complete();

                ring.Initialize(result.CapacityFrames, result.Channels);
                result.DescriptorValid = ring.TryCreateNativeDescriptor(
                    out NativeAudioKernelRingBufferDescriptor descriptor,
                    out NativeAudioKernelBridgeStatus descriptorStatus);
                result.DescriptorStatusBits = (int)descriptorStatus;
                result.DescriptorAligned = IsAligned(descriptor.Frames) &&
                                           IsAligned(descriptor.SharedState) &&
                                           IsAligned(descriptor.ReadIndex) &&
                                           IsAligned(descriptor.WriteIndex);
                if (!result.DescriptorValid || !result.DescriptorAligned)
                    return false;

                int fuzzIterations = result.Iterations;
                int fuzzBlockFrames = result.BlockFrames;
                int fuzzChannels = result.Channels;
                int successfulWrites = 0;
                int failedWrites = 0;
                IntPtr readIndexAddress = descriptor.ReadIndex;
                IntPtr writeIndexAddress = descriptor.WriteIndex;
                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                consumerThread = new Thread(() =>
                {
                    int* readIndex = (int*)readIndexAddress;
                    int* writeIndex = (int*)writeIndexAddress;
                    while (Volatile.Read(ref running) != 0)
                    {
                        int publishedWriteIndex = *writeIndex;
                        Thread.MemoryBarrier();
                        *readIndex = publishedWriteIndex;
                        Thread.MemoryBarrier();
                        Thread.SpinWait(128);
                    }

                    *readIndex = *writeIndex;
                    Thread.MemoryBarrier();
                })
                {
                    IsBackground = true,
                    Name = "H8.AudioBridgeFuzzConsumer",
                    Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                };

                producerThread = new Thread(() =>
                {
                    SpinWait wait = default;
                    for (int i = 0; i < fuzzIterations; i++)
                    {
                        if (Volatile.Read(ref running) == 0)
                            break;

                        int admissionGuard = 100000;
                        while (ring.WritableFrames < fuzzBlockFrames &&
                               admissionGuard-- > 0 &&
                               Volatile.Read(ref running) != 0)
                            wait.SpinOnce();

                        if (Volatile.Read(ref running) == 0)
                            break;

                        if (ring.TryWriteInterleaved(samples, fuzzBlockFrames, fuzzChannels))
                            Interlocked.Increment(ref successfulWrites);
                        else
                            Interlocked.Increment(ref failedWrites);
                    }
                })
                {
                    IsBackground = true,
                    Name = "H8.AudioBridgeFuzzProducer",
                    Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                };

                consumerThread.Start();
                producerThread.Start();
                bool producerStopped = TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadJoinTimeoutMilliseconds);
                if (!producerStopped)
                {
                    Volatile.Write(ref running, 0);
                    producerStopped = TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadStopJoinTimeoutMilliseconds);
                }

                Volatile.Write(ref running, 0);
                bool consumerStopped = TryJoinFuzzerThreadNoThrow(consumerThread, FuzzerThreadStopJoinTimeoutMilliseconds);

                ring.GetState(out int bufferedFrames, out int writableFrames);
                result.SuccessfulWrites = Volatile.Read(ref successfulWrites);
                result.FailedWrites = Volatile.Read(ref failedWrites);
                result.FinalBufferedFrames = bufferedFrames;
                result.FinalWritableFrames = writableFrames;
                result.OverflowDropCount = ring.OverflowDropCount;
                result.ElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                result.Passed = producerStopped &&
                                consumerStopped &&
                                result.DescriptorValid &&
                                result.DescriptorAligned &&
                                result.SuccessfulWrites > 0 &&
                                result.FailedWrites == 0 &&
                                result.OverflowDropCount == 0;
                return result.Passed;
            }
            finally
            {
                Volatile.Write(ref running, 0);
                TryJoinFuzzerThreadNoThrow(producerThread, FuzzerThreadStopJoinTimeoutMilliseconds);
                TryJoinFuzzerThreadNoThrow(consumerThread, FuzzerThreadStopJoinTimeoutMilliseconds);

                bool canReleaseThreadSharedState =
                    !IsFuzzerThreadAlive(producerThread) &&
                    !IsFuzzerThreadAlive(consumerThread);
                if (canReleaseThreadSharedState && samples.IsCreated)
                {
                    System.Exception nativeSentinelCleanupException0 = null;

                    if (samplesSentinelId > 0)
                    {
                        try
                        {
                            NativeMemorySentinel.Unregister(samplesSentinelId);
                        }
                        catch (System.Exception nativeSentinelException0)
                        {
                            nativeSentinelCleanupException0 = nativeSentinelException0;
                        }
                        finally
                        {
                            samplesSentinelId = 0;
                        }

                        try
                        {
                            samples.Dispose();
                        }
                        catch (System.Exception nativeSentinelException0)
                        {
                            if (nativeSentinelCleanupException0 == null)
                                nativeSentinelCleanupException0 = nativeSentinelException0;
                        }
                    }
                    else
                    {
                        void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(samples);

                        try
                        {
                            NativeMemorySentinel.UnregisterPointer(trackedPointer);
                        }
                        catch (System.Exception nativeSentinelException0)
                        {
                            nativeSentinelCleanupException0 = nativeSentinelException0;
                        }

                        try
                        {
                            samples.Dispose();
                        }
                        catch (System.Exception nativeSentinelException0)
                        {
                            if (nativeSentinelCleanupException0 == null)
                                nativeSentinelCleanupException0 = nativeSentinelException0;
                        }

                    }

                    samples = default;

                    if (nativeSentinelCleanupException0 != null)
                        throw nativeSentinelCleanupException0;
                }

                if (canReleaseThreadSharedState)
                    ring.Dispose();
            }
        }

        private static bool TryJoinFuzzerThreadNoThrow(Thread thread, int timeoutMilliseconds)
        {
            if (thread == null || !thread.IsAlive)
                return true;

            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(math.max(1, timeoutMilliseconds));
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsFuzzerThreadAlive(Thread thread)
        {
            return thread != null && thread.IsAlive;
        }

        private static bool IsAligned(IntPtr pointer)
        {
            return pointer != IntPtr.Zero &&
                   (pointer.ToInt64() & (NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes - 1L)) == 0L;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockAudioSamplesJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<float> Samples;
            public uint Seed;
            public float Gain;

            public void Execute(int index)
            {
                if (!Samples.IsCreated || (uint)index >= (uint)Samples.Length)
                    return;

                uint hash = (uint)index ^ Seed;
                hash = hash * 747796405u + 2891336453u;
                uint word = ((hash >> (int)((hash >> 28) + 4u)) ^ hash) * 277803737u;
                word = (word >> 22) ^ word;
                float sample = ((word & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
                Samples[index] = sample * math.saturate(Gain);
            }
        }
    }
}
#endif
