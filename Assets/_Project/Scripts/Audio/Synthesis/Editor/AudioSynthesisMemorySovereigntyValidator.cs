using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.Audio.Synthesis.Editor
{
    public static unsafe class AudioSynthesisMemorySovereigntyValidator
    {
        private const int AgentId = 1308;
        private const int MockBankBytes = 196608;
        private const int MockRecordCount = 1;
        private const int OutputSamples = 1024;
        private const int Channels = 2;
        private const int WaveformSamples = 2048;
        private const int HotLoopIterations = 32;
        private const int MockLoadIterations = 4096;
        private const uint DefaultPhraseHash = 0x05203E88u;
        private const string RuntimeRoot = "Assets/_Project/Scripts/Audio/Synthesis";
        private const string ReportPath = "Docs/Reports/AUDIO_SYNTHESIS_MEMORY_SOVEREIGNTY_1308.json";
        private const string FailureMessage = "Audio synthesis memory sovereignty validation failed; see Docs/Reports/AUDIO_SYNTHESIS_MEMORY_SOVEREIGNTY_1308.json";

        [MenuItem("Hecton8/Audio/Run Memory Sovereignty Validator 1308")]
        public static void RunMenu()
        {
            bool passed = Run(out AudioSynthesisMemorySovereigntyResult result);
            WriteReport(in result);
            if (!passed)
                throw new InvalidOperationException(FailureMessage);

            Hecton8.Core.H8Debug.Log("[1308] Audio synthesis memory sovereignty validator passed.");
        }

        public static bool Run(out AudioSynthesisMemorySovereigntyResult result)
        {
            result = default;
            result.AgentId = AgentId;
            Stopwatch stopwatch = Stopwatch.StartNew();

            ValidateSourceAliases(ref result);
            ValidateLayouts(ref result);
            RunMockDecodeHarness(ref result);
            RunVaultRelocationHandleProbe(ref result);

            stopwatch.Stop();
            result.ElapsedMicroseconds = stopwatch.ElapsedTicks * (1000000.0 / Stopwatch.Frequency);
            result.Passed = result.FailureFlags == 0u ? 1 : 0;
            return result.Passed != 0;
        }

        private static void ValidateSourceAliases(ref AudioSynthesisMemorySovereigntyResult result)
        {
            if (!Directory.Exists(RuntimeRoot))
            {
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureSourceMissing;
                return;
            }

            string[] files = Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories);
            bool dumpRouteFound = false;
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string normalized = file.Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                result.SourceFilesScanned++;
                string source = File.ReadAllText(file);
                if (source.Contains("Dump_1308_Synthesis.bin"))
                    dumpRouteFound = true;

                if (HasForbiddenPersistentSourceAlias(source))
                    result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailurePersistentAlias;

                result.RuntimeForbiddenTokenMatches += CountRuntimeForbiddenTokens(source);
                result.RuntimeManagedBootstrapCalls += CountOccurrences(source, "AddComponent<");
                result.RuntimeColdCatchBranches += CountOccurrences(source, "catch (Exception)");
                result.RuntimeBroadMutableViewSymbolMatches += CountOccurrences(source, "TryResolveViews");
                result.RuntimeBroadMutableViewSymbolMatches += CountOccurrences(source, "TryResolveSynthViews");
            }

            if (result.SourceFilesScanned <= 0 || !dumpRouteFound)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureSourceMissing;

            if (result.RuntimeForbiddenTokenMatches > 0 || result.RuntimeBroadMutableViewSymbolMatches > 0)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureRuntimeSourcePurity;
        }

        private static bool HasForbiddenPersistentSourceAlias(string source)
        {
            return source.Contains("System.IO." + "MemoryMapped" + "Files") ||
                   source.Contains("_state" + "Ptr") ||
                   source.Contains("_codec" + "Ptr") ||
                   source.Contains("_telemetry" + "Ptr") ||
                   source.Contains("_counters" + "Ptr") ||
                   source.Contains("_waveform" + "Ptr") ||
                   source.Contains("_mockBank" + "Ptr") ||
                   source.Contains("_bank" + "Ptr") ||
                   source.Contains("_mmf" + "Pointer") ||
                   source.Contains("RefreshUnsafe" + "Pointers");
        }

        private static int CountRuntimeForbiddenTokens(string source)
        {
            int count = 0;
            using (StringReader reader = new StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                        trimmed.StartsWith("*", StringComparison.Ordinal))
                        continue;

                    if (ContainsWordNew(trimmed) ||
                        trimmed.Contains("string.Format") ||
                        trimmed.Contains(".ToString(") ||
                        trimmed.Contains("System.Linq") ||
                        trimmed.Contains("foreach") ||
                        trimmed.Contains("$\"") ||
                        trimmed.Contains("+ \"") ||
                        trimmed.Contains("\" +") ||
                        trimmed.Contains(".Complete(") ||
                        trimmed.Contains("FindObjectOfType") ||
                        trimmed.Contains("GameObject.Find") ||
                        trimmed.Contains("Camera.main") ||
                        trimmed.Contains("GetComponent<") ||
                        trimmed.Contains("GetComponents<") ||
                        trimmed.Contains("StartCoroutine") ||
                        trimmed.Contains("AudioClip.Create") ||
                        trimmed.Contains("Resources.Load") ||
                        trimmed.Contains("Instantiate(") ||
                        trimmed.Contains("Debug.Log") ||
                        trimmed.Contains("H8Debug.Log") ||
                        trimmed.Contains("throw new") ||
                        trimmed.Contains("NativeList<") ||
                        trimmed.Contains("NativeHashMap<") ||
                        trimmed.Contains("NativeQueue<") ||
                        trimmed.Contains("UnsafeList<"))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool ContainsWordNew(string line)
        {
            int index = line.IndexOf("new", StringComparison.Ordinal);
            while (index >= 0)
            {
                bool left = index == 0 || !IsIdentifierChar(line[index - 1]);
                int rightIndex = index + 3;
                bool right = rightIndex >= line.Length || !IsIdentifierChar(line[rightIndex]);
                if (left && right)
                    return true;

                index = line.IndexOf("new", index + 3, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return (value >= 'A' && value <= 'Z') ||
                   (value >= 'a' && value <= 'z') ||
                   (value >= '0' && value <= '9') ||
                   value == '_';
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = source.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = source.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return count;
        }

        private static void ValidateLayouts(ref AudioSynthesisMemorySovereigntyResult result)
        {
            VocalStateLayoutValidator.ValidateOrThrow();
            AssertSizeMultipleOfEight<VocalBankHeaderDTO>(ref result);
            AssertSizeMultipleOfEight<VocalBankIndexRecordDTO>(ref result);
            AssertSizeMultipleOfEight<VocalDialogueMetadataDTO>(ref result);
            AssertSizeMultipleOfEight<VocalStateDTO>(ref result);
            AssertSizeMultipleOfEight<VocalCodecStateDTO>(ref result);
            AssertSizeMultipleOfEight<VocalTelemetryEntryDTO>(ref result);
            AssertSizeMultipleOfEight<VocalDecodeCounters64>(ref result);
            AssertSizeMultipleOfEight<SynthVoiceDTO>(ref result);
            AssertSizeMultipleOfEight<DynamicMusicSynthScalarDTO>(ref result);
            AssertSizeMultipleOfEight<DynamicMusicSynthTuningDTO>(ref result);
            AssertSizeMultipleOfEight<DynamicMusicBiquadStateDTO>(ref result);
            AssertSizeMultipleOfEight<DynamicMusicPresetRuleDTO>(ref result);
            AssertSizeMultipleOfEight<DynamicMusicSharedStateDTO>(ref result);
            AssertSizeMultipleOfEight<AudioDSPTelemetryEntry>(ref result);

            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.Magic), 0, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.Version), 4, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.HeaderSize), 8, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.RecordSize), 12, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.RecordCount), 16, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.Flags), 20, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.PayloadOffset), 24, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.PayloadBytes), 32, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.SampleRate), 40, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.DefaultCodec), 44, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.DefaultChannels), 45, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.EndianMarker), 46, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.BankHash), 48, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.BlockSamples), 52, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.CreatedUnixSeconds), 56, ref result);
            AssertFieldOffset<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.Reserved0), 60, ref result);
            AssertFieldAligned<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.PayloadOffset), 8, ref result);
            AssertFieldAligned<VocalBankHeaderDTO>(nameof(VocalBankHeaderDTO.PayloadBytes), 8, ref result);

            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.HashID), 0, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.ByteLength), 4, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.ByteOffset), 8, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.TotalSamples), 16, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.SampleRate), 20, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Codec), 24, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Channels), 25, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Priority), 26, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.RadioDistortionByte), 27, ref result);
            AssertFieldOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Flags), 28, ref result);
            AssertFieldAligned<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.ByteOffset), 8, ref result);

            AssertFieldOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.HashID), 0, ref result);
            AssertFieldOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.Priority), 4, ref result);
            AssertFieldOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.RadioDistortion01), 8, ref result);
            AssertFieldOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.Flags), 12, ref result);

            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.PhraseHashID), 0, ref result);
            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.CurrentSampleIndex), 4, ref result);
            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.TotalSamples), 8, ref result);
            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.PlaybackSpeed), 12, ref result);
            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.VolumeScalar), 16, ref result);
            AssertFieldOffset<VocalStateDTO>(nameof(VocalStateDTO.Flags), 20, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad0", 24, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad1", 25, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad2", 26, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad3", 27, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad4", 28, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad5", 29, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad6", 30, ref result);
            AssertFieldOffset<VocalStateDTO>("_pad7", 31, ref result);

            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.PayloadOffset), 0, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.PayloadByteLength), 8, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SampleRate), 12, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Priority), 16, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.RadioDistortion01), 20, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.QualityWeight01), 24, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SpatialGain), 28, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SourcePosition), 32, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.LowState), 36, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.BandState), 40, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.LastSample), 44, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.DecodedSampleIndex), 48, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Predictor), 52, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Step), 54, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Codec), 55, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.ActivePhraseHashID), 56, ref result);
            AssertFieldOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.FaultFlags), 60, ref result);
            AssertFieldAligned<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.PayloadOffset), 8, ref result);

            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.Frame), 0, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.PhraseHashID), 4, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.CurrentSampleIndex), 8, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.TotalSamples), 12, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.DspMicroseconds), 16, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.OutputPeak), 20, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.OutputRms), 24, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.QualityWeight01), 28, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.RadioDistortion01), 32, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.Priority), 36, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.Flags), 40, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.UnderrunCount), 44, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.PayloadByteLength), 48, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.SampleRate), 52, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>(nameof(VocalTelemetryEntryDTO.Codec), 56, ref result);
            AssertFieldOffset<VocalTelemetryEntryDTO>("_pad0", 60, ref result);

            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.TelemetryCursor), 0, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.WaveformCursor), 4, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.FaultCount), 8, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.MissCount), 12, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.LastFaultFlags), 16, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.LastPhraseHashID), 20, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.LastDspMicroseconds), 24, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.LastPeak), 28, ref result);
            AssertFieldOffset<VocalDecodeCounters64>(nameof(VocalDecodeCounters64.LastRms), 32, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad0", 36, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad1", 40, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad2", 44, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad3", 48, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad4", 52, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad5", 56, ref result);
            AssertFieldOffset<VocalDecodeCounters64>("_pad6", 60, ref result);

            ValidateDynamicMusicLayouts(ref result);
        }

        private static void ValidateDynamicMusicLayouts(ref AudioSynthesisMemorySovereigntyResult result)
        {
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.CurrentPhase), 0, ref result);
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.PhaseIncrement), 4, ref result);
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.EnvelopeState), 8, ref result);
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.SoundHash), 12, ref result);
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.TargetPitch), 16, ref result);
            AssertFieldOffset<SynthVoiceDTO>(nameof(SynthVoiceDTO.TargetVolume), 20, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad0", 24, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad1", 28, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad2", 32, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad3", 36, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad4", 40, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad5", 44, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad6", 48, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad7", 52, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad8", 56, ref result);
            AssertFieldOffset<SynthVoiceDTO>("_pad9", 60, ref result);

            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.Frame), 0, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.Flags), 4, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.TensionIndex), 8, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.DepthMeters), 12, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.Depth01), 16, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.GlobalQualityWeight), 20, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.DamageImpulse01), 24, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.StingerImpulse), 28, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.BaseDensity), 32, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.TargetPitch), 36, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.TargetVolume), 40, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.LfoFrequency), 44, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.LpfCutoffHz), 48, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.ActiveVoices), 52, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.OutputPeak), 56, ref result);
            AssertFieldOffset<DynamicMusicSynthScalarDTO>(nameof(DynamicMusicSynthScalarDTO.OutputRms), 60, ref result);

            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.BasePitchHz), 0, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.BaseGrainDensity), 4, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.TensionMultiplier), 8, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.LfoFrequency), 12, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.BaseVolume), 16, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.GrainSizeSeconds), 20, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.QualityMin), 24, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.QualityMax), 28, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.DepthMaxMeters), 32, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.LpfMinHz), 36, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.LpfDepthHzPerMeter), 40, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.StereoWidth), 44, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.DensityTensionScale), 48, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.DetuneCentsMax), 52, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.StingerDecaySeconds), 56, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.NoiseFoldback), 60, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.SeedBase), 64, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.WaveformHash), 68, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.PresetHash), 72, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>(nameof(DynamicMusicSynthTuningDTO.Flags), 76, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad0", 80, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad1", 84, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad2", 88, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad3", 92, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad4", 96, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad5", 100, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad6", 104, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad7", 108, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad8", 112, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad9", 116, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad10", 120, ref result);
            AssertFieldOffset<DynamicMusicSynthTuningDTO>("_pad11", 124, ref result);

            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.Z1Left), 0, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.Z2Left), 4, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.Z1Right), 8, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.Z2Right), 12, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.LastCutoffHz), 16, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.A0), 20, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.A1), 24, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.A2), 28, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.B1), 32, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.B2), 36, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.LastSampleRate), 40, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>(nameof(DynamicMusicBiquadStateDTO.Flags), 44, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>("_pad0", 48, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>("_pad1", 52, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>("_pad2", 56, ref result);
            AssertFieldOffset<DynamicMusicBiquadStateDTO>("_pad3", 60, ref result);

            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.PresetHash), 0, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.BiomeHash), 4, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.NarrativeHash), 8, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.WaveformHash), 12, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.BasePitchHz), 16, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.GrainSizeSeconds), 20, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.BaseDensity), 24, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.TensionMultiplier), 28, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.LfoFrequency), 32, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.BaseVolume), 36, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.QualityMin), 40, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.QualityMax), 44, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.DepthMaxMeters), 48, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.LpfMinHz), 52, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.LpfDepthHzPerMeter), 56, ref result);
            AssertFieldOffset<DynamicMusicPresetRuleDTO>(nameof(DynamicMusicPresetRuleDTO.Flags), 60, ref result);

            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.ReadyBufferIndex), 0, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.ReadySampleCount), 4, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.PendingBufferIndex), 8, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.AudioCopyBufferIndex), 12, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.PublishedFrame), 16, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.Channels), 20, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.AudioUnderrunCount), 24, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.AudioOverflowCount), 28, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.LastDspMicroseconds), 32, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.LastActiveVoices), 36, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.LastTensionIndex), 40, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.LastDepthMeters), 44, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.LastCutoffHz), 48, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.Flags), 52, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>(nameof(DynamicMusicSharedStateDTO.MusicActivity01), 56, ref result);
            AssertFieldOffset<DynamicMusicSharedStateDTO>("_pad1", 60, ref result);

            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.Frame), 0, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.ActiveVoices), 4, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.Flags), 8, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.ReadyBufferIndex), 12, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.TensionIndex), 16, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.DepthMeters), 20, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.LpfCutoffHz), 24, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.DspJobMicroseconds), 28, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.QualityWeight), 32, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.GrainDensity), 36, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.TargetPitch), 40, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.StingerImpulse), 44, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.OutputPeak), 48, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.OutputRms), 52, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.AudioUnderrunCount), 56, ref result);
            AssertFieldOffset<AudioDSPTelemetryEntry>(nameof(AudioDSPTelemetryEntry.OutputSampleCount), 60, ref result);
        }

        private static void AssertSizeMultipleOfEight<T>(ref AudioSynthesisMemorySovereigntyResult result)
            where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            if ((size & 7) != 0)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureLayout;
        }

        private static void AssertFieldOffset<T>(string fieldName, int expectedOffset, ref AudioSynthesisMemorySovereigntyResult result)
            where T : struct
        {
            int offset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (offset != expectedOffset)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureLayout;
        }

        private static void AssertFieldAligned<T>(string fieldName, int alignment, ref AudioSynthesisMemorySovereigntyResult result)
            where T : struct
        {
            int offset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if ((offset % alignment) != 0)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureLayout;
        }

        private static void RunMockDecodeHarness(ref AudioSynthesisMemorySovereigntyResult result)
        {
            NativeArray<byte> bank = default;
            NativeArray<VocalBankIndexRecordDTO> records = default;
            NativeArray<float> output = default;
            NativeArray<VocalStateDTO> state = default;
            NativeArray<VocalCodecStateDTO> codec = default;
            NativeArray<VocalTelemetryEntryDTO> telemetry = default;
            NativeArray<VocalDecodeCounters64> counters = default;
            NativeArray<float> waveform = default;

            try
            {
                bank = new NativeArray<byte>(MockBankBytes, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                records = new NativeArray<VocalBankIndexRecordDTO>(MockRecordCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                output = new NativeArray<float>(OutputSamples * Channels, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                state = new NativeArray<VocalStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                codec = new NativeArray<VocalCodecStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                telemetry = new NativeArray<VocalTelemetryEntryDTO>((int)VocalBankConstants.TelemetryRingCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<VocalDecodeCounters64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                waveform = new NativeArray<float>(WaveformSamples, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                GenerateMockVocalBankJob build = new GenerateMockVocalBankJob
                {
                    BankBytes = bank,
                    Records = records,
                    PhraseHashID = DefaultPhraseHash,
                    SampleRate = 48000u,
                    TotalSamples = 32000u
                };
                build.Execute();

                GenerateMockSynthesisLoadJob mockLoad = default;
                mockLoad.Telemetry = telemetry;
                mockLoad.Counters = counters;
                mockLoad.Waveform = waveform;
                mockLoad.PhraseHashID = DefaultPhraseHash;
                mockLoad.Iterations = MockLoadIterations;
                mockLoad.Execute();
                result.MockLoadIterationsExecuted = MockLoadIterations;

                byte* bankPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bank);
                if (!VocalBankReader.TryReadHeader(bankPtr, bank.Length, out _) ||
                    !VocalBankReader.TryFindRecord(bankPtr, bank.Length, DefaultPhraseHash, out VocalBankIndexRecordDTO record))
                {
                    result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureMockBank;
                    return;
                }

                DecodeOnce(0.5f, 0u, in record, output, bank, state, codec, telemetry, counters, waveform, ref result);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < HotLoopIterations; i++)
                    DecodeOnce(0.5f, (uint)(i + 1), in record, output, bank, state, codec, telemetry, counters, waveform, ref result);
                long after = GC.GetAllocatedBytesForCurrentThread();
                result.ManagedAllocBytes = after - before;
                if (result.ManagedAllocBytes != 0L)
                    result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureManagedAllocation;

                float[] probes = { 0f, 0.5f, 1f };
                for (int i = 0; i < probes.Length; i++)
                {
                    DecodeOnce(probes[i], (uint)(100 + i), in record, output, bank, state, codec, telemetry, counters, waveform, ref result);
                    if (math.isfinite(result.LastPeak) && result.LastPeak > 0f)
                        result.QualityProbeMask |= 1u << i;
                }

                if (result.QualityProbeMask != 0x7u)
                    result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureQualityProbe;
            }
            finally
            {
                if (waveform.IsCreated) waveform.Dispose();
                if (counters.IsCreated) counters.Dispose();
                if (telemetry.IsCreated) telemetry.Dispose();
                if (codec.IsCreated) codec.Dispose();
                if (state.IsCreated) state.Dispose();
                if (output.IsCreated) output.Dispose();
                if (records.IsCreated) records.Dispose();
                if (bank.IsCreated) bank.Dispose();
            }
        }

        private static void RunVaultRelocationHandleProbe(ref AudioSynthesisMemorySovereigntyResult result)
        {
            using GlobalDataVault vault = GlobalDataVault.Create();
            VaultGenerationHandle<VocalStateDTO> stateHandle = vault.EnsureGenerationHandle<VocalStateDTO>(
                BufferID.AudioVocalSynthesisState,
                1,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<VocalTelemetryEntryDTO> telemetryHandle = vault.EnsureGenerationHandle<VocalTelemetryEntryDTO>(
                BufferID.AudioVocalSynthesisTelemetry,
                (int)VocalBankConstants.TelemetryRingCapacity,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<byte> bankHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.AudioVocalSynthesisMockBankBytes,
                MockBankBytes,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);

            if (stateHandle.BufferID == 0u ||
                telemetryHandle.BufferID == 0u ||
                bankHandle.BufferID == 0u)
            {
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureRelocationProbe;
                return;
            }

            if (!vault.TryAcquireWriteLock(in stateHandle, SystemID.AudioVocalSynthesis, out NativeArray<VocalStateDTO> state))
            {
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureRelocationProbe;
                return;
            }

            try
            {
                state[0] = new VocalStateDTO
                {
                    PhraseHashID = DefaultPhraseHash,
                    TotalSamples = 1u,
                    PlaybackSpeed = 1f,
                    VolumeScalar = 1f,
                    Flags = VocalBankConstants.StateFlagPlaying
                };
            }
            finally
            {
                vault.ReleaseWriteLock(in stateHandle, SystemID.AudioVocalSynthesis);
            }

            bool relocated = vault.GenerateMockVaultRelocationForValidation(
                0x13081308u,
                8,
                MemoryDefragPhase.PreSimulation,
                vault.ActiveBurstLockMask);
            result.RelocationRecordCount = vault.LastRelocationRecordCount;
            result.RelocationMovedBytes = vault.LastDefragMovedBytes;
            result.RelocationOldStateHandleAccepted = vault.TryReadOnlyHandle(in stateHandle, out NativeArray<VocalStateDTO>.ReadOnly _) ? 1 : 0;

            stateHandle = vault.EnsureGenerationHandle<VocalStateDTO>(
                BufferID.AudioVocalSynthesisState,
                1,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);
            telemetryHandle = vault.EnsureGenerationHandle<VocalTelemetryEntryDTO>(
                BufferID.AudioVocalSynthesisTelemetry,
                (int)VocalBankConstants.TelemetryRingCapacity,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);
            bankHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.AudioVocalSynthesisMockBankBytes,
                MockBankBytes,
                SystemID.AudioVocalSynthesis,
                NativeArrayOptions.ClearMemory);

            if (!relocated ||
                !vault.TryReadOnlyHandle(in stateHandle, out NativeArray<VocalStateDTO>.ReadOnly stateRead) ||
                !vault.TryReadOnlyHandle(in telemetryHandle, out NativeArray<VocalTelemetryEntryDTO>.ReadOnly telemetryRead) ||
                !vault.TryReadOnlyHandle(in bankHandle, out NativeArray<byte>.ReadOnly bankRead) ||
                stateRead.Length <= 0 ||
                telemetryRead.Length < (int)VocalBankConstants.TelemetryRingCapacity ||
                bankRead.Length < MockBankBytes ||
                stateRead[0].PhraseHashID != DefaultPhraseHash)
            {
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureRelocationProbe;
                return;
            }

            result.RelocationProbePassed = 1;
        }

        private static void DecodeOnce(
            float quality,
            uint frame,
            in VocalBankIndexRecordDTO record,
            NativeArray<float> output,
            NativeArray<byte> bank,
            NativeArray<VocalStateDTO> state,
            NativeArray<VocalCodecStateDTO> codec,
            NativeArray<VocalTelemetryEntryDTO> telemetry,
            NativeArray<VocalDecodeCounters64> counters,
            NativeArray<float> waveform,
            ref AudioSynthesisMemorySovereigntyResult result)
        {
            state[0] = new VocalStateDTO
            {
                PhraseHashID = DefaultPhraseHash,
                CurrentSampleIndex = 0u,
                TotalSamples = record.TotalSamples,
                PlaybackSpeed = 1f,
                VolumeScalar = 1f,
                Flags = VocalBankConstants.StateFlagPlaying
            };

            codec[0] = new VocalCodecStateDTO
            {
                PayloadOffset = record.ByteOffset,
                PayloadByteLength = record.ByteLength,
                SampleRate = record.SampleRate,
                Priority = record.Priority,
                RadioDistortion01 = record.RadioDistortionByte / 255f,
                QualityWeight01 = math.saturate(quality),
                SpatialGain = 1f,
                Codec = record.Codec
            };

            counters[0] = default;
            for (int i = 0; i < output.Length; i++)
                output[i] = 0f;

            DecodeVocalStreamJob decode = new DecodeVocalStreamJob
            {
                Output = output,
                Bank = bank,
                State = state,
                Codec = codec,
                Telemetry = telemetry,
                Counters = counters,
                Waveform = waveform,
                Channels = Channels,
                MixIntoExistingOutput = 0,
                Frame = frame
            };
            decode.Execute();

            float peak = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                float sample = output[i];
                if (!math.isfinite(sample))
                {
                    result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureNonFinite;
                    sample = 0f;
                }

                peak = math.max(peak, math.abs(sample));
            }

            VocalDecodeCounters64 counter = counters[0];
            result.LastPeak = peak;
            result.LastTelemetryCursor = counter.TelemetryCursor;
            result.LastFaultFlags = counter.LastFaultFlags;
            if (counter.TelemetryCursor <= 0 || peak <= 0f)
                result.FailureFlags |= AudioSynthesisMemorySovereigntyResult.FailureDecode;
        }

        private static void WriteReport(in AudioSynthesisMemorySovereigntyResult result)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("{");
            AppendJson(builder, "schema", "hecton8.audio_synthesis_memory_sovereignty.v1", comma: true);
            AppendJson(builder, "agentId", result.AgentId, comma: true);
            AppendJson(builder, "passed", result.Passed, comma: true);
            AppendJson(builder, "failureFlags", result.FailureFlags, comma: true);
            AppendJson(builder, "sourceFilesScanned", result.SourceFilesScanned, comma: true);
            AppendJson(builder, "runtimeForbiddenTokenMatches", result.RuntimeForbiddenTokenMatches, comma: true);
            AppendJson(builder, "runtimeManagedBootstrapCalls", result.RuntimeManagedBootstrapCalls, comma: true);
            AppendJson(builder, "runtimeColdCatchBranches", result.RuntimeColdCatchBranches, comma: true);
            AppendJson(builder, "runtimeBroadMutableViewSymbolMatches", result.RuntimeBroadMutableViewSymbolMatches, comma: true);
            AppendJson(builder, "managedAllocBytes", result.ManagedAllocBytes, comma: true);
            AppendJson(builder, "qualityProbeMask", result.QualityProbeMask, comma: true);
            AppendJson(builder, "mockLoadIterationsExecuted", result.MockLoadIterationsExecuted, comma: true);
            AppendJson(builder, "relocationProbePassed", result.RelocationProbePassed, comma: true);
            AppendJson(builder, "relocationOldStateHandleAccepted", result.RelocationOldStateHandleAccepted, comma: true);
            AppendJson(builder, "relocationRecordCount", result.RelocationRecordCount, comma: true);
            AppendJson(builder, "relocationMovedBytes", result.RelocationMovedBytes, comma: true);
            AppendJson(builder, "lastTelemetryCursor", result.LastTelemetryCursor, comma: true);
            AppendJson(builder, "lastFaultFlags", result.LastFaultFlags, comma: true);
            AppendJson(builder, "lastPeak", result.LastPeak, comma: true);
            AppendJson(builder, "elapsedMicroseconds", result.ElapsedMicroseconds, comma: false);
            builder.AppendLine("}");
            File.WriteAllText(ReportPath, builder.ToString());
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(value).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, uint value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, long value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, float value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, double value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockSynthesisLoadJob : IJob
    {
        public NativeArray<VocalTelemetryEntryDTO> Telemetry;
        public NativeArray<VocalDecodeCounters64> Counters;
        public NativeArray<float> Waveform;
        public uint PhraseHashID;
        public int Iterations;

        public void Execute()
        {
            if (!Telemetry.IsCreated ||
                !Counters.IsCreated ||
                !Waveform.IsCreated ||
                Telemetry.Length <= 0 ||
                Counters.Length <= 0 ||
                Waveform.Length <= 0)
                return;

            int telemetryCapacity = math.min((int)VocalBankConstants.TelemetryRingCapacity, Telemetry.Length);
            int waveformCapacity = Waveform.Length;
            int iterations = math.max(0, Iterations);
            VocalDecodeCounters64 counters = Counters[0];
            for (int i = 0; i < iterations; i++)
            {
                uint frame = (uint)i;
                int telemetryIndex = i % telemetryCapacity;
                float q = (i & 1023) * (1f / 1023f);
                VocalTelemetryEntryDTO entry = default;
                entry.Frame = frame;
                entry.PhraseHashID = PhraseHashID;
                entry.CurrentSampleIndex = (uint)(i * 37);
                entry.TotalSamples = 32000u;
                entry.DspMicroseconds = 8f + q * 12f;
                entry.OutputPeak = math.saturate(q);
                entry.OutputRms = math.saturate(q * 0.70710677f);
                entry.QualityWeight01 = q;
                entry.RadioDistortion01 = 1f - q;
                entry.Priority = (int)(i & 7);
                entry.Flags = (i & 127) == 0 ? VocalBankConstants.StateFlagBankMiss : 0u;
                entry.UnderrunCount = (uint)(i >> 8);
                entry.PayloadByteLength = 64000u;
                entry.SampleRate = 48000u;
                entry.Codec = VocalBankConstants.CodecPcm16;
                Telemetry[telemetryIndex] = entry;

                int waveformIndex = i % waveformCapacity;
                Waveform[waveformIndex] = math.saturate(q * 2f - 0.5f);
            }

            counters.TelemetryCursor = telemetryCapacity > 0 ? iterations % telemetryCapacity : 0;
            counters.WaveformCursor = waveformCapacity > 0 ? iterations % waveformCapacity : 0;
            counters.FaultCount = iterations >> 7;
            counters.MissCount = iterations >> 8;
            counters.LastFaultFlags = VocalBankConstants.StateFlagBankMiss;
            counters.LastPhraseHashID = PhraseHashID;
            counters.LastDspMicroseconds = 20f;
            counters.LastPeak = 1f;
            counters.LastRms = 0.70710677f;
            Counters[0] = counters;
        }
    }

    public struct AudioSynthesisMemorySovereigntyResult
    {
        public const uint FailureSourceMissing = 1u << 0;
        public const uint FailurePersistentAlias = 1u << 1;
        public const uint FailureLayout = 1u << 2;
        public const uint FailureMockBank = 1u << 3;
        public const uint FailureDecode = 1u << 4;
        public const uint FailureNonFinite = 1u << 5;
        public const uint FailureManagedAllocation = 1u << 6;
        public const uint FailureQualityProbe = 1u << 7;
        public const uint FailureRelocationProbe = 1u << 8;
        public const uint FailureRuntimeSourcePurity = 1u << 9;

        public int AgentId;
        public int Passed;
        public uint FailureFlags;
        public int SourceFilesScanned;
        public int RuntimeForbiddenTokenMatches;
        public int RuntimeManagedBootstrapCalls;
        public int RuntimeColdCatchBranches;
        public int RuntimeBroadMutableViewSymbolMatches;
        public long ManagedAllocBytes;
        public uint QualityProbeMask;
        public int MockLoadIterationsExecuted;
        public int RelocationProbePassed;
        public int RelocationOldStateHandleAccepted;
        public int RelocationRecordCount;
        public long RelocationMovedBytes;
        public int LastTelemetryCursor;
        public uint LastFaultFlags;
        public float LastPeak;
        public double ElapsedMicroseconds;
    }
}
