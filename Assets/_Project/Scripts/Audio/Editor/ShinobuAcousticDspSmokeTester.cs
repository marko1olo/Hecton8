#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Audio.Virtualization;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for SHINOBU acoustic DSP virtualization invariants.
    /// </summary>
    public static class ShinobuAcousticDspSmokeTester
    {
        private const string ContractsPath = "Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs";
        private const string JobsPath = "Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs";
        private const string CoreAsmdefPath = "Assets/_Project/Scripts/Hecton8.Core.asmdef";
        private const string VirtualizationContractsAsmdefPath = "Assets/_Project/Scripts/Audio/Virtualization/Contracts/Hecton8.Audio.Virtualization.Contracts.asmdef";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string AcousticAupPath = "Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs";
        private const string AbyssalTunerPath = "Assets/_Project/Scripts/Audio/Editor/AbyssalAcousticsTunerWindow.cs";
        private const string AcousticMaterialsCsvPath = "Assets/_Project/Data/Audio/acoustic_materials.csv";
        private static readonly string TuningDefaultPropertyNeedle = "VirtualVoiceTuningSnapshot." + "Default";
        private static readonly string PropagationAssemblyNeedle = "Hecton8.Audio." + "Propagation";
        private static readonly string VirtualizationAssemblyNeedle = "\"Hecton8.Audio." + "Virtualization\"";
        private static readonly string PropagationUsingNeedle = "using Hecton8.Audio." + "Propagation;";
        private static readonly string BurstFastModeNeedle = "FloatMode = FloatMode." + "Fast";
        private static readonly string CompactPackWithSpacesNeedle = "Pack " + "= 1";
        private static readonly string CompactPackNeedle = "Pack" + "=1";

        [MenuItem("Hecton8/Audio/Run SHINOBU Acoustic DSP Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Hecton8.Core.H8Debug.Log(report);
            else
                Hecton8.Core.H8Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failures = 0;
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("[ShinobuAcousticDspSmokeTester]");

            string contracts = ReadAssetText(ContractsPath, builder, ref failures);
            string jobs = ReadAssetText(JobsPath, builder, ref failures);
            string coreAsmdef = ReadAssetText(CoreAsmdefPath, builder, ref failures);
            string virtualizationContractsAsmdef = ReadAssetText(VirtualizationContractsAsmdefPath, builder, ref failures);
            string spatial = ReadAssetText(SpatialAudioPath, builder, ref failures);
            string acousticAup = ReadAssetText(AcousticAupPath, builder, ref failures);
            string abyssalTuner = ReadAssetText(AbyssalTunerPath, builder, ref failures);
            string materialCsv = ReadAssetText(AcousticMaterialsCsvPath, builder, ref failures);

            AssertNotContains(contracts, CompactPackWithSpacesNeedle, "Virtualization contracts use natural packing", builder, ref failures);
            AssertNotContains(contracts, CompactPackNeedle, "Virtualization contracts do not use compact packing syntax", builder, ref failures);
            AssertNotContains(acousticAup, CompactPackWithSpacesNeedle, "AcousticAup uses natural packing", builder, ref failures);
            AssertNotContains(acousticAup, CompactPackNeedle, "AcousticAup does not use compact packing syntax", builder, ref failures);
            AssertContains(acousticAup, "public struct AcousticAup", "AcousticAup remains in an included Core contract source", builder, ref failures);
            AssertContains(acousticAup, "[StructLayout(LayoutKind.Explicit, Size = 40)]", "AcousticAup remains exact 40 bytes", builder, ref failures);
            AssertContains(acousticAup, "[FieldOffset(24)] public float3 Local;", "AcousticAup keeps local float3 at offset 24", builder, ref failures);
            AssertContains(contracts, "[StructLayout(LayoutKind.Explicit, Size = 48)]", "VirtualVoiceDTO remains exact 48 bytes", builder, ref failures);
            AssertContains(contracts, "public double3 AupMeters;", "VirtualVoiceDTO keeps double3 AUP first", builder, ref failures);
            AssertContains(contracts, "[StructLayout(LayoutKind.Explicit, Size = 64)]", "AcousticSourceDTO/output DTO are explicit one-cache-line layouts", builder, ref failures);
            AssertContains(contracts, "[FieldOffset(16)] public double3 AUP_Position;", "AcousticSourceDTO keeps double3 AUP at offset 16", builder, ref failures);
            AssertContains(contracts, "public float3 SourceVelocityMetersPerSecond;", "Selected voice DTO preserves AUP velocity for Doppler without ingress scans", builder, ref failures);
            AssertContains(contracts, "MaxPhysicalVoiceCount = 64", "Burst acoustic voice budget reaches 64 voices", builder, ref failures);
            AssertContains(contracts, "LowTierPhysicalVoiceCount = 12", "Survival acoustic voice budget bottoms at 12 voices", builder, ref failures);
            AssertContains(contracts, "ResolveContinuousVoiceBudget", "GlobalQualityWeight drives continuous voice budget", builder, ref failures);
            AssertContains(contracts, "CreateDefault()", "VirtualVoiceTuningSnapshot uses a factory method instead of a struct property", builder, ref failures);
            AssertContains(contracts, "public struct AcousticTelemetryEntry", "SDF acoustic recorder owns a dedicated AcousticTelemetryEntry DTO", builder, ref failures);
            AssertContains(contracts, "public float AcousticOcclusionTimeMs;", "Acoustic telemetry carries SDF occlusion compute time", builder, ref failures);
            AssertNotContains(contracts, TuningDefaultPropertyNeedle, "VirtualVoiceTuningSnapshot exposes no static Default property", builder, ref failures);
            AssertContains(contracts, "TryReadTuning(ReadOnlySpan<byte>", "CSV tuning parser accepts bytes without managed line splitting", builder, ref failures);
            AssertContains(contracts, "NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO>", "Material acoustics can hydrate a Vault-owned native hash map", builder, ref failures);
            AssertContains(contracts, "GenerateEmergencyMockAcoustics", "Missing acoustic material binary has deterministic fallback rows", builder, ref failures);
            AssertContains(contracts, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "VirtualVoiceSortKey is one 16-byte cache key", builder, ref failures);
            AssertContains(contracts, "public enum VirtualVoicePortalFlags : byte", "Virtualization owns byte portal mirror without propagation assembly coupling", builder, ref failures);
            AssertNotContains(contracts, PropagationUsingNeedle, "Virtualization contracts do not import sibling propagation runtime", builder, ref failures);
            AssertNotContains(coreAsmdef, VirtualizationAssemblyNeedle, "Core owns virtualization jobs without a concrete virtualization assembly reference", builder, ref failures);
            AssertNotContains(virtualizationContractsAsmdef, PropagationAssemblyNeedle, "Virtualization contracts asmdef avoids direct propagation sibling reference", builder, ref failures);
            AssertContains(jobs, "[NoAlias] public NativeArray<VirtualVoiceSortKey> SortKeys;", "Burst job sorts compact no-alias keys, not full voices", builder, ref failures);
            AssertNotContains(jobs, "SortVoicesDescending(NativeArray<VirtualVoice>", "Burst job does not swap 160-byte voice structs", builder, ref failures);
            AssertContains(jobs, "public struct MockAcousticEmitterJob : IJobParallelFor", "Mock acoustic emitter is a parallel Burst job", builder, ref failures);
            AssertContains(jobs, "public struct AcousticOcclusionJob : IJobParallelFor", "SDF acoustic occlusion is a parallel Burst job", builder, ref failures);
            AssertContains(jobs, "[ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly SdfVoxels;", "SDF acoustic kernel consumes encoded Vault byte SDF voxels", builder, ref failures);
            AssertContains(jobs, "((SdfVoxels[index] * 0.0039215686f) * 2f - 1f)", "SDF acoustic kernel decodes byte SDF into signed meters", builder, ref failures);
            AssertContains(jobs, "FloatMode = FloatMode.Deterministic", "Rollback-compatible acoustic kernel uses deterministic Burst floats", builder, ref failures);
            AssertNotContains(jobs, BurstFastModeNeedle, "Rollback-visible audio jobs avoid fast float drift", builder, ref failures);
            AssertContains(jobs, "RollbackActive", "Rollback frames clamp DSP output", builder, ref failures);
            AssertContains(spatial, "SpatialAudioVirtualVoiceSortKeyPoolBufferId", "Sort key buffer is GlobalDataVault-backed", builder, ref failures);
            AssertContains(spatial, "SpatialAudioAcousticSourceWritePoolBufferId", "AcousticSourceDTO write pool is GlobalDataVault-backed", builder, ref failures);
            AssertContains(spatial, "SpatialAudioAcousticDspOutputPoolBufferId", "Acoustic DSP output pool is GlobalDataVault-backed", builder, ref failures);
            AssertContains(spatial, "SpatialAudioAcousticSelectedSourcePoolBufferId", "Selected physical voice acoustic lane is GlobalDataVault-backed", builder, ref failures);
            AssertContains(spatial, "VaultGenerationHandle<AcousticOcclusionTelemetryEntry> _virtualVoiceBlackBoxHandle", "SDF recorder ring keeps only the dedicated acoustic telemetry descriptor", builder, ref failures);
            AssertContains(spatial, "out NativeArray<AcousticOcclusionTelemetryEntry> blackBox", "SDF recorder ring writes through a scoped DataVault view", builder, ref failures);
            AssertContains(spatial, "BufferID.VoxelSdfTexture3D", "Spatial audio aliases the owner-published voxel SDF buffer", builder, ref failures);
            AssertContains(spatial, "TrySnapshotAcousticSdfPayload", "Spatial audio snapshots owner-published SDF metadata before scheduling", builder, ref failures);
            AssertContains(spatial, "HectonVoxelVolume.TryAcquireClosestPublishedSonarSdfPayloadReadLease", "Spatial audio uses the SDF owner's leased origin/dimensions/cell/range", builder, ref failures);
            AssertContains(spatial, "SdfVoxels = hasVoxelSdf ? sdfVoxels : default", "SDF kernel receives validated owner voxels before mock fallback", builder, ref failures);
            AssertContains(spatial, "SdfOriginMeters = hasVoxelSdf ? sdfOrigin - _virtualListenerSdfProbePosition : float3.zero", "SDF origin is converted into listener-relative space after AUP subtraction", builder, ref failures);
            AssertContains(spatial, "PopulateSelectedAcousticSources", "SDF kernel runs on sorted selected voices, not unsorted ingress rows", builder, ref failures);
            AssertContains(spatial, "ScheduleAcousticOcclusionJob", "Runtime schedules the analytical SDF acoustic kernel", builder, ref failures);
            AssertContains(spatial, "ApplyAcousticDspOutputToSelection", "VISUAL_SYNC voice injection consumes unmanaged acoustic DSP output rows", builder, ref failures);
            AssertContains(spatial, "int limit = math.clamp(_acousticOcclusionOutputCount, 0, _acousticDspOutputPool.Length);", "DSP upload scans only the selected acoustic lane, never stale virtual rows", builder, ref failures);
            AssertContains(spatial, "ReloadAcousticMaterialRowsFromCsvCold", "Spatial audio exposes a cold material CSV reload facade", builder, ref failures);
            AssertContains(spatial, "NativeArrayOptions.UninitializedMemory", "Fully overwritten virtual voice pools bypass zero-init", builder, ref failures);
            AssertContains(spatial, "SignalBus<AcousticPingSignal>.GetFrameSnapshot()", "Audio consumes AcousticPingSignal lane", builder, ref failures);
            AssertContains(spatial, "ResolveVirtualVoiceGizmoColor", "Scene gizmos color-shift by SDF occlusion state", builder, ref failures);
            AssertContains(spatial, "DrawSelectedAcousticSourceDtoGizmos", "Scene gizmos read computed AcousticSourceDTO occlusion values", builder, ref failures);
            AssertContains(spatial, "TryCompleteVirtualVoiceSort(false)", "FastTick uses non-blocking virtual sort completion", builder, ref failures);
            AssertContains(spatial, "TryCompleteVirtualVoiceSort(true)", "Late/structural handoff keeps explicit blocking boundary", builder, ref failures);
            AssertContains(spatial, "_acousticOcclusionStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();", "SDF acoustic job records its schedule timestamp", builder, ref failures);
            AssertContains(spatial, "if (!_acousticOcclusionScheduled)", "Telemetry is pushed once after SDF completion, with a zero-voice fallback only", builder, ref failures);
            AssertContains(spatial, "statistics.AcousticOcclusionTimeMs > 1.0f", "Blackbox dumps when SDF occlusion compute exceeds 1ms", builder, ref failures);
            AssertContains(spatial, "VirtualVoiceBlackBoxFrameCount = 300", "Acoustic blackbox remains 300 frames", builder, ref failures);
            AssertContains(spatial, "Dump_ACOUSTIC_SURGEON.bin", "Virtual voice fatal dump path matches SHINOBU recorder contract", builder, ref failures);
            AssertContains(abyssalTuner, "using UnityEngine.UIElements;", "Abyssal Acoustics tuner uses UI Toolkit", builder, ref failures);
            AssertContains(abyssalTuner, "ReloadMaterialCsv", "Abyssal Acoustics tuner can reload acoustic_materials.csv into Vault rows", builder, ref failures);
            AssertContains(abyssalTuner, "File.ReadAllBytes", "Editor material CSV reload is explicit cold I/O, not runtime polling", builder, ref failures);
            AssertContains(materialCsv, "rock,0.32,0.55,0.85,2100", "Material CSV seeds deterministic rock acoustics", builder, ref failures);
            AssertContains(materialCsv, "metal,0.18,0.28,1.00,3400", "Material CSV seeds deterministic metal acoustics", builder, ref failures);
            AssertContains(materialCsv, "flesh,0.62,0.75,0.45,1200", "Material CSV seeds deterministic flesh acoustics", builder, ref failures);

            AssertEqual(UnsafeUtility.SizeOf<AcousticSourceDTO>(), 64, "AcousticSourceDTO runtime size is exactly 64 bytes", builder, ref failures);
            AssertEqual(UnsafeUtility.SizeOf<AcousticDspOutputDTO>(), 64, "AcousticDspOutputDTO runtime size is exactly 64 bytes", builder, ref failures);
            AssertEqual(UnsafeUtility.SizeOf<AcousticTelemetryEntry>(), 64, "AcousticTelemetryEntry runtime size is exactly 64 bytes", builder, ref failures);
            AssertEqual(UnsafeUtility.SizeOf<VirtualVoiceSelection>(), 144, "VirtualVoiceSelection retains fixed ABI size after selected-lane velocity preservation", builder, ref failures);

            builder.Append("STATUS: ");
            builder.AppendLine(failures == 0 ? "PASS" : "FAIL");
            report = builder.ToString();
            return failures == 0;
        }

        private static string ReadAssetText(string assetPath, StringBuilder builder, ref int failures)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendFailure(builder, ref failures, "Missing asset: " + assetPath);
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static void AssertContains(string source, string needle, string message, StringBuilder builder, ref int failures)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failures, message + " :: missing `" + needle + "`");
        }

        private static void AssertNotContains(string source, string needle, string message, StringBuilder builder, ref int failures)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) < 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failures, message + " :: found forbidden `" + needle + "`");
        }

        private static void AssertEqual(int actual, int expected, string message, StringBuilder builder, ref int failures)
        {
            if (actual == expected)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failures, message + " :: expected " + expected + ", got " + actual);
        }

        private static void AppendFailure(StringBuilder builder, ref int failures, string message)
        {
            failures++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
#endif
