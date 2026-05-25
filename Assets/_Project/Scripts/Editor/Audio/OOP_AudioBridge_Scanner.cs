#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Hecton8.Audio;
using Hecton8.Core;
using Unity.Collections;
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
            string scanner = ReadAssetText(projectRoot, "Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs", result);

            AssertContains(result, bridge, "WriteIndexSlot = 2", "write_index_slot_even", BridgePath, "WriteIndex exported at shared-state byte offset 8.");
            AssertContains(result, bridge, "SourceChannelsSlot = 12", "source_channels_even_slot", BridgePath, "Shared-state metadata records mono/stereo ring layout in the padded shared-state contract.");
            AssertContains(result, bridge, "public int SourceChannels", "descriptor_source_channels_field", BridgePath, "Descriptor carries immutable source-channel count so native callback does not trust mutable SharedState for frame stride.");
            AssertContains(result, bridge, "SharedStateSlotCount = 14", "shared_state_padded", BridgePath, "Shared state reserves even int slots for 8-byte pointer alignment and source-channel metadata.");
            AssertContains(result, bridge, "HasValidSharedStatePointerLayout", "bridge_layout_guard", BridgePath, "Bridge rejects descriptors whose cursor pointers do not match expected shared-state offsets.");
            AssertContains(result, bridge, "HasValidSharedStateMetadata", "bridge_shared_metadata_guard", BridgePath, "Bridge local validation matches native shared-state capacity/mask/guard/source-channel checks before P/Invoke.");
            AssertContains(result, bridge, "Volatile.Read(ref sharedStatePtr", "bridge_shared_metadata_volatile_reads", BridgePath, "Bridge shared-state metadata validation reads unmanaged cursor metadata with volatile semantics.");
            AssertContains(result, bridge, "TryRegisterWithRetryGate", "bridge_retry_gate", BridgePath, "Registration retries once and fails closed through TryClear.");
            AssertContains(result, bridge, "TryDumpAudioBridgeTelemetry", "bridge_native_dump_gate", BridgePath, "Bridge exposes native telemetry dump without managed FileStream/Path/Directory use.");
            AssertContains(result, bridge, "(status & NativeAudioKernelBridgeStatus.Busy) == 0", "bridge_clear_rejects_busy", BridgePath, "TryClear does not report success while native clear is still Busy.");
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
            AssertContains(result, ring, "if (!HectonSensoryKernelNativeBridge.TryDumpAudioBridgeTelemetry", "telemetry_dump_retry_on_export_failure", RingPath, "Dump gate resets only if the native binary export fails.");
            AssertNotContains(result, ring, "return ReadSharedIndex(ref views, slot) & _capacityMask", "managed_no_masked_corrupt_index", RingPath, "Runtime no longer converts corrupt raw shared-state indices into apparently valid ring positions.");
            AssertContains(result, ring, "TryAllocateNativeBridgeBuffers(frameSampleCapacity)", "native_bridge_raw_allocate_gate", RingPath, "Native-exported frames/shared-state pointers are allocated from stable unmanaged H8Memory instead of relocatable DataVault arena memory.");
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
            AssertContains(result, ring, "GenerateMockAudioSamplesJob", "mock_audio_job", RingPath, "Burst mock-audio generator exists for stress validation.");
            AssertContains(result, scanner, "AudioBridgeConcurrencyFuzzer1314", "concurrency_fuzzer", "Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs", "Editor-only SPSC producer/consumer fuzzer exists outside runtime ring source.");
            AssertNotContains(result, ring, "MixInterleavedInto(float[]", "managed_float_array_bridge_absent", RingPath, "Managed float[] bridge path is absent.");

            AssertContains(result, renderer, "TryRegisterWithRetryGate(ref descriptor", "renderer_uses_retry_gate", RendererPath, "Renderer registers native output through retry/fail-closed gate.");
            AssertContains(result, renderer, "_sampleRingBuffer.RecordBridgeFailure(bridgeStatus)", "renderer_records_bridge_failure", RendererPath, "Registration failure writes the black-box route.");
            AssertMinimumOccurrence(result, renderer, "RefreshNativeOutputBridge();", 2, "renderer_rebinds_bridge", RendererPath, "DataVault/audio configuration refresh paths re-register the bridge.");
            AssertNotContains(result, renderer, "OnAudioFilterRead", "unity_audio_callback_absent", RendererPath, "Critical renderer does not fall back to Unity managed audio callback.");

            AssertContains(result, memory, "AudioFrameRingTelemetry", "datavault_telemetry_lane", MemoryPath, "DataVault owns the audio bridge telemetry lane.");
            AssertContains(result, memory, "public static bool IsInitialized", "h8memory_initialized_probe", MemoryPath, "H8Memory exposes a read-only shutdown probe for fail-closed raw bridge teardown.");
            AssertNotContains(result, memory, "AudioFrameRingTelemetryDumpBytes", "datavault_no_dump_byte_lane", MemoryPath, "Obsolete DataVault dump byte lane is removed; dump scratch lives in the stable unmanaged bridge pool.");

            AssertContains(result, nativePlugin, "kWriteIndexSlot = 2", "native_write_index_slot_even", NativePluginPath, "Native audio kernel expects the same write cursor byte offset 8 as C#.");
            AssertContains(result, nativePlugin, "kSourceChannelsSlot = 12", "native_source_channels_slot", NativePluginPath, "Native audio kernel reads the mono/stereo source layout from shared state.");
            AssertContains(result, nativePlugin, "kSharedStateSlotCount = 14", "native_shared_state_padded", NativePluginPath, "Native audio kernel requires the padded shared-state slot count.");
            AssertContains(result, nativePlugin, "kRequiredPointerAlignmentBytes = 8u", "native_required_alignment_8", NativePluginPath, "Native validation rejects pointers below the C# 8-byte alignment contract.");
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
            AssertContains(result, nativePlugin, "if (!WaitForProcessCallbacksToDrain())", "native_drain_fail_closed_busy", NativePluginPath, "Native registration/clear fail closed with Busy status if callback drain cannot complete.");
            AssertNotContains(result, nativePlugin, "while (AtomicRead32(&g_processCallbackDepth) != 0)", "native_no_unbounded_callback_drain", NativePluginPath, "Native callback drain has no unbounded busy loop.");
            AssertContains(result, nativePlugin, "g_debugProcessScratch[4096 * 8]", "native_debug_fixed_scratch", NativePluginPath, "Debug process export uses fixed static scratch instead of heap allocation.");
            AssertContains(result, nativePlugin, "g_debugProcessScratchInUse", "native_debug_scratch_busy_gate", NativePluginPath, "Debug process export serializes fixed scratch use with an atomic busy gate.");
            AssertContains(result, nativePlugin, "g_telemetryDumpBuffer[kTelemetryDumpMaxBytes]", "native_dump_fixed_scratch", NativePluginPath, "Native telemetry dump uses fixed static scratch before background file I/O.");
            AssertContains(result, nativePlugin, "QueueTelemetryDumpAsync", "native_dump_async_queue", NativePluginPath, "Native dump export queues file I/O onto an unmanaged background thread.");
            AssertContains(result, nativePlugin, "TelemetryDumpThreadMain", "native_dump_thread_entry", NativePluginPath, "Native dump writer has a dedicated unmanaged thread entry point.");
            AssertContains(result, nativePlugin, "g_telemetryDumpInUse", "native_dump_busy_gate", NativePluginPath, "Native dump writer has an atomic busy gate and fails closed if a dump is already in flight.");
            AssertNotContains(result, nativePlugin, "new EffectData", "native_no_effectdata_new", NativePluginPath, "Native plugin create callback does not allocate heap effectdata.");
            AssertNotContains(result, nativePlugin, "delete effectData", "native_no_effectdata_delete", NativePluginPath, "Native plugin release callback does not delete heap effectdata.");
            AssertNotContains(result, nativePlugin, "malloc(", "native_no_malloc", NativePluginPath, "Native plugin contains no malloc route in release source.");
            AssertNotContains(result, nativePlugin, "free(", "native_no_free", NativePluginPath, "Native plugin contains no free route in release source.");
            AssertNotContains(result, nativePlugin, "kWriteIndexSlot = 1", "native_old_write_index_slot_removed", NativePluginPath, "Native base+4 write cursor validation route is absent.");
            AssertContains(result, nativePlugin, "const int sourceChannels = ringBuffer.sourceChannels", "native_source_channels_from_descriptor", NativePluginPath, "Native callback consumes immutable descriptor source-channel count after validation instead of a mutable SharedState slot.");
            AssertContains(result, nativePlugin, "sourceFrameIndex << 1", "native_stereo_frame_stride", NativePluginPath, "Native callback consumes interleaved stereo frames with frame*2 addressing.");
            AssertContains(result, nativePlugin, "HectonSensoryKernel_DumpAudioBridgeTelemetry", "native_dump_export", NativePluginPath, "Native plugin exports the binary telemetry dump writer.");
            AssertContains(result, nativePlugin, "Dump_1314_AudioBridge.bin", "native_dump_file_name", NativePluginPath, "Native dump writer targets the required binary dump file.");
            AssertContains(result, nativePlugin, "return QueueTelemetryDumpAsync(bytes, byteCount);", "native_dump_export_nonblocking", NativePluginPath, "Managed bridge only queues the native dump instead of doing synchronous file I/O inline.");

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
        private const int DefaultCapacityFrames = 131072;
        private const int DefaultBlockFrames = 65536;
        private const int DefaultChannels = 2;
        private const int JobBatchCount = 64;

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
            AudioFrameSpscRingBuffer ring = new AudioFrameSpscRingBuffer();
            try
            {
                samples = new NativeArray<float>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                AudioFrameSpscRingBuffer.GenerateMockAudioSamplesJob job = default;
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
                int running = 1;
                int successfulWrites = 0;
                int failedWrites = 0;
                IntPtr readIndexAddress = descriptor.ReadIndex;
                IntPtr writeIndexAddress = descriptor.WriteIndex;
                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                Thread consumer = new Thread(() =>
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

                Thread producer = new Thread(() =>
                {
                    SpinWait wait = default;
                    for (int i = 0; i < fuzzIterations; i++)
                    {
                        int admissionGuard = 100000;
                        while (ring.WritableFrames < fuzzBlockFrames && admissionGuard-- > 0)
                            wait.SpinOnce();

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

                consumer.Start();
                producer.Start();
                producer.Join();
                Volatile.Write(ref running, 0);
                consumer.Join();

                ring.GetState(out int bufferedFrames, out int writableFrames);
                result.SuccessfulWrites = Volatile.Read(ref successfulWrites);
                result.FailedWrites = Volatile.Read(ref failedWrites);
                result.FinalBufferedFrames = bufferedFrames;
                result.FinalWritableFrames = writableFrames;
                result.OverflowDropCount = ring.OverflowDropCount;
                result.ElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                result.Passed = result.DescriptorValid &&
                                result.DescriptorAligned &&
                                result.SuccessfulWrites > 0 &&
                                result.FailedWrites == 0 &&
                                result.OverflowDropCount == 0;
                return result.Passed;
            }
            finally
            {
                if (samples.IsCreated)
                    samples.Dispose();

                ring.Dispose();
            }
        }

        private static bool IsAligned(IntPtr pointer)
        {
            return pointer != IntPtr.Zero &&
                   (pointer.ToInt64() & (NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes - 1L)) == 0L;
        }
    }
}
#endif
