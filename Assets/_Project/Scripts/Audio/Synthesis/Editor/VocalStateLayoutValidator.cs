using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Synthesis.Editor
{
    public static class VocalStateLayoutValidator
    {
        private const int VocalBankHeaderStrideBytes = 64;
        private const int VocalBankIndexRecordStrideBytes = 32;
        private const int VocalDialogueMetadataStrideBytes = 16;
        private const int VocalStateStrideBytes = 32;
        private const int VocalCodecStateStrideBytes = 64;
        private const int VocalTelemetryStrideBytes = 64;
        private const int VocalDecodeCountersStrideBytes = 64;
        private const int VocalCueSignalStrideBytes = 64;

        [MenuItem("Hecton8/Audio/Validate Vocal Bank ABI")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Hecton8.Core.H8Debug.Log("[1308] Vocal bank ABI validated: header=64, record=32, state=32, codec=64, telemetry=64, cue=64.");
        }

        public static void ValidateOrThrow()
        {
            AssertSize<VocalBankHeaderDTO>(VocalBankHeaderStrideBytes);
            AssertSize<VocalBankIndexRecordDTO>(VocalBankIndexRecordStrideBytes);
            AssertSize<VocalDialogueMetadataDTO>(VocalDialogueMetadataStrideBytes);
            AssertSize<VocalStateDTO>(VocalStateStrideBytes);
            AssertSize<VocalCodecStateDTO>(VocalCodecStateStrideBytes);
            AssertSize<VocalTelemetryEntryDTO>(VocalTelemetryStrideBytes);
            AssertSize<VocalDecodeCounters64>(VocalDecodeCountersStrideBytes);
            AssertSize<VocalCueSignal>(VocalCueSignalStrideBytes);

            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.PhraseHashID), 0);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.CurrentSampleIndex), 4);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.TotalSamples), 8);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.PlaybackSpeed), 12);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.VolumeScalar), 16);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.Flags), 20);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.DuckingEnvelope01), 24);
            AssertOffset<VocalStateDTO>(nameof(VocalStateDTO.SpeakerFloodDistortion01), 28);

            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.PayloadOffset), 0);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.PayloadByteLength), 8);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SampleRate), 12);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Priority), 16);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.RadioDistortion01), 20);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.QualityWeight01), 24);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SpatialGain), 28);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.SourcePosition), 32);
            AssertOffset<VocalCodecStateDTO>(nameof(VocalCodecStateDTO.Codec), 55);

            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.HashID), 0);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.ByteLength), 4);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.ByteOffset), 8);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.TotalSamples), 16);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.SampleRate), 20);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Codec), 24);
            AssertOffset<VocalBankIndexRecordDTO>(nameof(VocalBankIndexRecordDTO.Flags), 28);

            AssertOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.HashID), 0);
            AssertOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.Priority), 4);
            AssertOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.RadioDistortion01), 8);
            AssertOffset<VocalDialogueMetadataDTO>(nameof(VocalDialogueMetadataDTO.Flags), 12);
        }

        private static void AssertSize<T>(int expected)
            where T : struct
        {
            int actual = UnsafeUtility.SizeOf<T>();
            if (actual != expected)
                throw new InvalidOperationException(typeof(T).Name + " size " + actual + " != " + expected);
        }

        private static void AssertOffset<T>(string field, int expected)
            where T : struct
        {
            int actual = Marshal.OffsetOf<T>(field).ToInt32();
            if (actual != expected)
                throw new InvalidOperationException(typeof(T).Name + "." + field + " offset " + actual + " != " + expected);
        }
    }
}
