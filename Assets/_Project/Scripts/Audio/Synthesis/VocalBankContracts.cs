using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Synthesis
{
    public static class VocalBankConstants
    {
        public const uint Magic = 0x42563848u; // H8VB, little-endian.
        public const uint Version = 1u;
        public const ushort LittleEndianMarker = 0xFEFF;
        public const uint DefaultBlockSamples = 64u;
        public const byte CodecPcm16 = 0;
        public const byte CodecH8Adpcm = 1;
        public const byte CodecVorbis = 2;
        public const uint StateFlagPlaying = 1u << 0;
        public const uint StateFlagVorbisUnsupported = 1u << 1;
        public const uint StateFlagNonFinite = 1u << 2;
        public const uint StateFlagBankMiss = 1u << 3;
        public const uint StateFlagInterrupted = 1u << 4;
        public const uint TelemetryRingCapacity = 300u;
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VocalBankHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderSize;
        [FieldOffset(12)] public uint RecordSize;
        [FieldOffset(16)] public uint RecordCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong PayloadOffset;
        [FieldOffset(32)] public ulong PayloadBytes;
        [FieldOffset(40)] public uint SampleRate;
        [FieldOffset(44)] public byte DefaultCodec;
        [FieldOffset(45)] public byte DefaultChannels;
        [FieldOffset(46)] public ushort EndianMarker;
        [FieldOffset(48)] public uint BankHash;
        [FieldOffset(52)] public uint BlockSamples;
        [FieldOffset(56)] public uint CreatedUnixSeconds;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VocalBankIndexRecordDTO
    {
        [FieldOffset(0)] public uint HashID;
        [FieldOffset(4)] public uint ByteLength;
        [FieldOffset(8)] public ulong ByteOffset;
        [FieldOffset(16)] public uint TotalSamples;
        [FieldOffset(20)] public uint SampleRate;
        [FieldOffset(24)] public byte Codec;
        [FieldOffset(25)] public byte Channels;
        [FieldOffset(26)] public byte Priority;
        [FieldOffset(27)] public byte RadioDistortionByte;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VocalDialogueMetadataDTO
    {
        [FieldOffset(0)] public uint HashID;
        [FieldOffset(4)] public int Priority;
        [FieldOffset(8)] public float RadioDistortion01;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VocalStateDTO
    {
        [FieldOffset(0)] public uint PhraseHashID;
        [FieldOffset(4)] public uint CurrentSampleIndex;
        [FieldOffset(8)] public uint TotalSamples;
        [FieldOffset(12)] public float PlaybackSpeed;
        [FieldOffset(16)] public float VolumeScalar;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public byte Pad0;
        [FieldOffset(25)] public byte Pad1;
        [FieldOffset(26)] public byte Pad2;
        [FieldOffset(27)] public byte Pad3;
        [FieldOffset(28)] public byte Pad4;
        [FieldOffset(29)] public byte Pad5;
        [FieldOffset(30)] public byte Pad6;
        [FieldOffset(31)] public byte Pad7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref VocalStateDTO AsRef(void* pointer)
        {
            return ref UnsafeUtility.AsRef<VocalStateDTO>(pointer);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VocalCodecStateDTO
    {
        [FieldOffset(0)] public ulong PayloadOffset;
        [FieldOffset(8)] public uint PayloadByteLength;
        [FieldOffset(12)] public uint SampleRate;
        [FieldOffset(16)] public int Priority;
        [FieldOffset(20)] public float RadioDistortion01;
        [FieldOffset(24)] public float QualityWeight01;
        [FieldOffset(28)] public float SpatialGain;
        [FieldOffset(32)] public float SourcePosition;
        [FieldOffset(36)] public float LowState;
        [FieldOffset(40)] public float BandState;
        [FieldOffset(44)] public float LastSample;
        [FieldOffset(48)] public int DecodedSampleIndex;
        [FieldOffset(52)] public short Predictor;
        [FieldOffset(54)] public byte Step;
        [FieldOffset(55)] public byte Codec;
        [FieldOffset(56)] public uint ActivePhraseHashID;
        [FieldOffset(60)] public uint FaultFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VocalTelemetryEntryDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PhraseHashID;
        [FieldOffset(8)] public uint CurrentSampleIndex;
        [FieldOffset(12)] public uint TotalSamples;
        [FieldOffset(16)] public float DspMicroseconds;
        [FieldOffset(20)] public float OutputPeak;
        [FieldOffset(24)] public float OutputRms;
        [FieldOffset(28)] public float QualityWeight01;
        [FieldOffset(32)] public float RadioDistortion01;
        [FieldOffset(36)] public int Priority;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint UnderrunCount;
        [FieldOffset(48)] public uint PayloadByteLength;
        [FieldOffset(52)] public uint SampleRate;
        [FieldOffset(56)] public uint Codec;
        [FieldOffset(60)] public uint Padding0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VocalDecodeCounters64
    {
        [FieldOffset(0)] public int TelemetryCursor;
        [FieldOffset(4)] public int WaveformCursor;
        [FieldOffset(8)] public int FaultCount;
        [FieldOffset(12)] public int MissCount;
        [FieldOffset(16)] public uint LastFaultFlags;
        [FieldOffset(20)] public uint LastPhraseHashID;
        [FieldOffset(24)] public float LastDspMicroseconds;
        [FieldOffset(28)] public float LastPeak;
        [FieldOffset(32)] public float LastRms;
        [FieldOffset(36)] public uint Padding0;
        [FieldOffset(40)] public ulong Padding1;
        [FieldOffset(48)] public ulong Padding2;
        [FieldOffset(56)] public ulong Padding3;
    }

    public static unsafe class VocalBankReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = VocalBankConstants.FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * VocalBankConstants.FnvPrime;

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32LE(byte* source)
        {
            return (uint)source[0] |
                   ((uint)source[1] << 8) |
                   ((uint)source[2] << 16) |
                   ((uint)source[3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64LE(byte* source)
        {
            uint lo = ReadUInt32LE(source);
            uint hi = ReadUInt32LE(source + 4);
            return lo | ((ulong)hi << 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16LE(byte* source)
        {
            return (ushort)(source[0] | (source[1] << 8));
        }

        public static bool TryReadHeader(byte* bank, long bankLength, out VocalBankHeaderDTO header)
        {
            header = default;
            if (bank == null || bankLength < UnsafeUtility.SizeOf<VocalBankHeaderDTO>())
                return false;

            header.Magic = ReadUInt32LE(bank);
            header.Version = ReadUInt32LE(bank + 4);
            header.HeaderSize = ReadUInt32LE(bank + 8);
            header.RecordSize = ReadUInt32LE(bank + 12);
            header.RecordCount = ReadUInt32LE(bank + 16);
            header.Flags = ReadUInt32LE(bank + 20);
            header.PayloadOffset = ReadUInt64LE(bank + 24);
            header.PayloadBytes = ReadUInt64LE(bank + 32);
            header.SampleRate = ReadUInt32LE(bank + 40);
            header.DefaultCodec = bank[44];
            header.DefaultChannels = bank[45];
            header.EndianMarker = ReadUInt16LE(bank + 46);
            header.BankHash = ReadUInt32LE(bank + 48);
            header.BlockSamples = ReadUInt32LE(bank + 52);
            header.CreatedUnixSeconds = ReadUInt32LE(bank + 56);
            header.Reserved0 = ReadUInt32LE(bank + 60);

            if (header.Magic != VocalBankConstants.Magic ||
                header.Version != VocalBankConstants.Version ||
                header.HeaderSize != 64u ||
                header.RecordSize != 32u ||
                header.EndianMarker != VocalBankConstants.LittleEndianMarker ||
                header.PayloadOffset > (ulong)bankLength ||
                header.PayloadBytes > (ulong)bankLength ||
                header.PayloadOffset + header.PayloadBytes > (ulong)bankLength)
                return false;

            ulong indexBytes = (ulong)header.RecordCount * header.RecordSize;
            return header.HeaderSize + indexBytes <= header.PayloadOffset;
        }

        public static bool TryReadRecord(byte* bank, in VocalBankHeaderDTO header, uint index, out VocalBankIndexRecordDTO record)
        {
            record = default;
            if (index >= header.RecordCount)
                return false;

            byte* source = bank + (int)(header.HeaderSize + index * header.RecordSize);
            record.HashID = ReadUInt32LE(source);
            record.ByteLength = ReadUInt32LE(source + 4);
            record.ByteOffset = ReadUInt64LE(source + 8);
            record.TotalSamples = ReadUInt32LE(source + 16);
            record.SampleRate = ReadUInt32LE(source + 20);
            record.Codec = source[24];
            record.Channels = source[25];
            record.Priority = source[26];
            record.RadioDistortionByte = source[27];
            record.Flags = ReadUInt32LE(source + 28);
            return true;
        }

        public static bool TryFindRecord(byte* bank, long bankLength, uint hash, out VocalBankIndexRecordDTO record)
        {
            record = default;
            if (!TryReadHeader(bank, bankLength, out VocalBankHeaderDTO header))
                return false;

            int lo = 0;
            int hi = (int)math.min(int.MaxValue, header.RecordCount) - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (!TryReadRecord(bank, in header, (uint)mid, out VocalBankIndexRecordDTO candidate))
                    return false;

                if (candidate.HashID == hash)
                {
                    ulong end = candidate.ByteOffset + candidate.ByteLength;
                    if (candidate.ByteOffset < header.PayloadOffset ||
                        end > header.PayloadOffset + header.PayloadBytes ||
                        end > (ulong)bankLength)
                        return false;

                    record = candidate;
                    return true;
                }

                if (candidate.HashID < hash)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockVocalBankJob : IJob
    {
        [NoAlias] public NativeArray<byte> BankBytes;
        [NoAlias] public NativeArray<VocalBankIndexRecordDTO> Records;
        public uint PhraseHashID;
        public uint SampleRate;
        public uint TotalSamples;

        public void Execute()
        {
            if (!BankBytes.IsCreated || !Records.IsCreated || Records.Length <= 0 || BankBytes.Length <= 128)
                return;

            uint safeSampleRate = math.max(8000u, SampleRate);
            uint safeTotalSamples = math.clamp(TotalSamples, 256u, 96000u);
            uint blockSamples = VocalBankConstants.DefaultBlockSamples;
            uint blockCount = (safeTotalSamples + blockSamples - 1u) / blockSamples;
            uint blockBytes = 4u + ((blockSamples - 1u + 1u) >> 1);
            uint payloadOffset = 64u + 32u;
            uint payloadBytes = blockCount * blockBytes;
            if (payloadOffset + payloadBytes > (uint)BankBytes.Length)
                payloadBytes = math.max(0u, (uint)BankBytes.Length - payloadOffset);

            WriteUInt32(0, VocalBankConstants.Magic);
            WriteUInt32(4, VocalBankConstants.Version);
            WriteUInt32(8, 64u);
            WriteUInt32(12, 32u);
            WriteUInt32(16, 1u);
            WriteUInt32(20, 0u);
            WriteUInt64(24, payloadOffset);
            WriteUInt64(32, payloadBytes);
            WriteUInt32(40, safeSampleRate);
            BankBytes[44] = VocalBankConstants.CodecH8Adpcm;
            BankBytes[45] = 1;
            WriteUInt16(46, VocalBankConstants.LittleEndianMarker);
            WriteUInt32(48, PhraseHashID ^ 0x9E3779B9u);
            WriteUInt32(52, blockSamples);
            WriteUInt32(56, 0u);
            WriteUInt32(60, 0u);

            WriteUInt32(64, PhraseHashID);
            WriteUInt32(68, payloadBytes);
            WriteUInt64(72, payloadOffset);
            WriteUInt32(80, safeTotalSamples);
            WriteUInt32(84, safeSampleRate);
            BankBytes[88] = VocalBankConstants.CodecH8Adpcm;
            BankBytes[89] = 1;
            BankBytes[90] = 16;
            BankBytes[91] = 96;
            WriteUInt32(92, 0u);

            Records[0] = new VocalBankIndexRecordDTO
            {
                HashID = PhraseHashID,
                ByteLength = payloadBytes,
                ByteOffset = payloadOffset,
                TotalSamples = safeTotalSamples,
                SampleRate = safeSampleRate,
                Codec = VocalBankConstants.CodecH8Adpcm,
                Channels = 1,
                Priority = 16,
                RadioDistortionByte = 96
            };

            int write = (int)payloadOffset;
            uint seed = PhraseHashID == 0u ? 0x6D2B79F5u : PhraseHashID;
            float baseHz = 123f + (seed & 31u);
            for (uint block = 0u; block < blockCount && write + 4 < BankBytes.Length; block++)
            {
                uint sampleStart = block * blockSamples;
                float first = GenerateMockSample(sampleStart, safeSampleRate, baseHz);
                short predictor = (short)math.clamp((int)math.round(first * 16000f), -28000, 28000);
                byte step = 9;
                WriteInt16(write, predictor);
                BankBytes[write + 2] = step;
                BankBytes[write + 3] = 0;
                write += 4;

                short current = predictor;
                byte currentStep = step;
                byte pack = 0;
                int nibbleSide = 0;
                for (uint s = 1u; s < blockSamples && sampleStart + s < safeTotalSamples && write < BankBytes.Length; s++)
                {
                    float generated = GenerateMockSample(sampleStart + s, safeSampleRate, baseHz);
                    int target = (int)math.clamp((int)math.round(generated * 16000f), -30000, 30000);
                    int delta = math.clamp((target - current) / math.max(1, currentStep), -8, 7);
                    current = (short)math.clamp(current + delta * currentStep, short.MinValue, short.MaxValue);
                    currentStep = (byte)math.clamp(currentStep + math.abs(delta) - 2, 1, 127);
                    byte encoded = (byte)(delta & 0x0F);
                    if (nibbleSide == 0)
                    {
                        pack = encoded;
                        nibbleSide = 1;
                    }
                    else
                    {
                        BankBytes[write++] = (byte)(pack | (encoded << 4));
                        nibbleSide = 0;
                    }
                }

                if (nibbleSide != 0 && write < BankBytes.Length)
                    BankBytes[write++] = pack;
            }
        }

        private float GenerateMockSample(uint sampleIndex, uint sampleRate, float baseHz)
        {
            float t = sampleIndex / math.max(1f, sampleRate);
            float env = math.saturate(math.min(t * 6f, (safeDuration(sampleRate) - t) * 4f));
            float a = math.sin(t * baseHz * 6.28318530718f);
            float b = math.sin(t * (baseHz * 1.497f) * 6.28318530718f) * 0.33f;
            return (a + b) * env * 0.42f;
        }

        private float safeDuration(uint sampleRate)
        {
            return TotalSamples / math.max(1f, sampleRate);
        }

        private void WriteUInt16(int offset, ushort value)
        {
            if (offset + 1 >= BankBytes.Length)
                return;
            BankBytes[offset] = (byte)value;
            BankBytes[offset + 1] = (byte)(value >> 8);
        }

        private void WriteInt16(int offset, short value)
        {
            WriteUInt16(offset, (ushort)value);
        }

        private void WriteUInt32(int offset, uint value)
        {
            if (offset + 3 >= BankBytes.Length)
                return;
            BankBytes[offset] = (byte)value;
            BankBytes[offset + 1] = (byte)(value >> 8);
            BankBytes[offset + 2] = (byte)(value >> 16);
            BankBytes[offset + 3] = (byte)(value >> 24);
        }

        private void WriteUInt64(int offset, ulong value)
        {
            WriteUInt32(offset, (uint)value);
            WriteUInt32(offset + 4, (uint)(value >> 32));
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void VocalDecodeDelegate(
        float* output,
        int sampleCount,
        int channels,
        int mixIntoExistingOutput,
        byte* bank,
        long bankByteLength,
        VocalStateDTO* state,
        VocalCodecStateDTO* codec,
        VocalTelemetryEntryDTO* telemetry,
        VocalDecodeCounters64* counters,
        float* waveform,
        int waveformCapacity,
        uint frame);

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct DecodeVocalStreamJob : IJob
    {
        [NoAlias] public NativeArray<float> Output;
        [NoAlias] public NativeArray<byte> Bank;
        [NoAlias] public NativeArray<VocalStateDTO> State;
        [NoAlias] public NativeArray<VocalCodecStateDTO> Codec;
        [NoAlias] public NativeArray<VocalTelemetryEntryDTO> Telemetry;
        [NoAlias] public NativeArray<VocalDecodeCounters64> Counters;
        [NoAlias] public NativeArray<float> Waveform;
        public int Channels;
        public int MixIntoExistingOutput;
        public uint Frame;

        public void Execute()
        {
            if (!Output.IsCreated || !Bank.IsCreated || !State.IsCreated || !Codec.IsCreated || !Telemetry.IsCreated || !Counters.IsCreated)
                return;

            void* output = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Output);
            void* bank = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Bank);
            void* state = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(State);
            void* codec = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Codec);
            void* telemetry = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Telemetry);
            void* counters = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Counters);
            void* waveform = Waveform.IsCreated && Waveform.Length > 0
                ? NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Waveform)
                : null;
            VocalDecodeKernel.DecodeIntoAudioBuffer(
                (float*)output,
                Output.Length / math.max(1, Channels),
                Channels,
                MixIntoExistingOutput,
                (byte*)bank,
                Bank.Length,
                (VocalStateDTO*)state,
                (VocalCodecStateDTO*)codec,
                (VocalTelemetryEntryDTO*)telemetry,
                (VocalDecodeCounters64*)counters,
                (float*)waveform,
                Waveform.IsCreated ? Waveform.Length : 0,
                Frame);
        }
    }

    public static unsafe class VocalDecodeKernel
    {
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static void DecodeIntoAudioBuffer(
            float* output,
            int sampleCount,
            int channels,
            int mixIntoExistingOutput,
            byte* bank,
            long bankByteLength,
            VocalStateDTO* state,
            VocalCodecStateDTO* codec,
            VocalTelemetryEntryDTO* telemetry,
            VocalDecodeCounters64* counters,
            float* waveform,
            int waveformCapacity,
            uint frame)
        {
            if (output == null || sampleCount <= 0 || channels <= 0 || state == null || codec == null || counters == null)
                return;

            ref VocalStateDTO stateRef = ref VocalStateDTO.AsRef(state);
            ref VocalCodecStateDTO codecRef = ref UnsafeUtility.AsRef<VocalCodecStateDTO>(codec);
            bool mixOutput = mixIntoExistingOutput != 0;
            if ((stateRef.Flags & VocalBankConstants.StateFlagPlaying) == 0u)
            {
                if (!mixOutput)
                    OverwriteSilence(output, sampleCount, math.clamp(channels, 1, 8));
                WriteTelemetry(frame, in stateRef, in codecRef, telemetry, counters);
                return;
            }

            if (bank == null || bankByteLength <= 0 ||
                codecRef.PayloadOffset >= (ulong)bankByteLength ||
                codecRef.PayloadOffset + codecRef.PayloadByteLength > (ulong)bankByteLength ||
                codecRef.PayloadOffset > 2147483647UL ||
                stateRef.TotalSamples == 0u)
            {
                stateRef.Flags &= ~VocalBankConstants.StateFlagPlaying;
                stateRef.Flags |= VocalBankConstants.StateFlagBankMiss;
                codecRef.FaultFlags |= VocalBankConstants.StateFlagBankMiss;
                counters->FaultCount++;
                counters->LastFaultFlags = stateRef.Flags;
                if (!mixOutput)
                    OverwriteSilence(output, sampleCount, math.clamp(channels, 1, 8));
                WriteTelemetry(frame, in stateRef, in codecRef, telemetry, counters);
                return;
            }

            if (codecRef.ActivePhraseHashID != stateRef.PhraseHashID)
            {
                codecRef.SourcePosition = stateRef.CurrentSampleIndex;
                codecRef.DecodedSampleIndex = -1;
                codecRef.Predictor = 0;
                codecRef.Step = 1;
                codecRef.LowState = 0f;
                codecRef.BandState = 0f;
                codecRef.LastSample = 0f;
                codecRef.ActivePhraseHashID = stateRef.PhraseHashID;
            }

            float quality = math.saturate(FiniteOrFallback(codecRef.QualityWeight01, 1f));
            float smoothQuality = quality * quality * (3f - 2f * quality);
            float speed = math.clamp(FiniteOrFallback(stateRef.PlaybackSpeed, 1f), 0.25f, 2f);
            float volume = math.saturate(FiniteOrFallback(stateRef.VolumeScalar, 1f)) *
                           math.saturate(FiniteOrFallback(codecRef.SpatialGain, 1f));
            float distortion = math.saturate(FiniteOrFallback(codecRef.RadioDistortion01, 0f));
            int sampleStride = math.clamp((int)math.round(math.lerp(4f, 1f, smoothQuality)), 1, 4);
            float sourceAdvance = speed;
            byte* payload = bank + (int)codecRef.PayloadOffset;
            uint payloadLength = codecRef.PayloadByteLength;
            int safeChannels = math.clamp(channels, 1, 8);
            float peak = 0f;
            float sumSq = 0f;
            int written = 0;
            int frameIndex = 0;

            for (; frameIndex < sampleCount; frameIndex++)
            {
                uint sourceIndex = (uint)math.max(0, (int)math.floor(codecRef.SourcePosition));
                if (sourceIndex >= stateRef.TotalSamples)
                {
                    stateRef.Flags &= ~VocalBankConstants.StateFlagPlaying;
                    break;
                }

                uint stride = (uint)sampleStride;
                uint quantizedIndex = (sourceIndex / stride) * stride;
                float decoded = DecodeSample(payload, payloadLength, quantizedIndex, ref codecRef, codecRef.Codec);
                if (sampleStride > 1 && sourceIndex != quantizedIndex)
                {
                    uint nextQuantizedIndex = math.min(stateRef.TotalSamples - 1u, quantizedIndex + stride);
                    VocalCodecStateDTO probeCodec = codecRef;
                    float nextDecoded = DecodeSample(payload, payloadLength, nextQuantizedIndex, ref probeCodec, codecRef.Codec);
                    float interpolation = (sourceIndex - quantizedIndex) / math.max(1f, nextQuantizedIndex - quantizedIndex);
                    decoded = math.lerp(decoded, nextDecoded, math.saturate(interpolation));
                }
                float filtered = ApplyDearLieRadioFilter(decoded, distortion, smoothQuality, ref codecRef);
                float finalSample = math.clamp(filtered * volume, -1f, 1f);
                if (!math.isfinite(finalSample))
                {
                    finalSample = 0f;
                    stateRef.Flags |= VocalBankConstants.StateFlagNonFinite;
                    codecRef.FaultFlags |= VocalBankConstants.StateFlagNonFinite;
                }

                int outputIndex = frameIndex * safeChannels;
                for (int ch = 0; ch < safeChannels; ch++)
                {
                    int destinationIndex = outputIndex + ch;
                    output[destinationIndex] = mixOutput
                        ? math.clamp(output[destinationIndex] + finalSample, -1f, 1f)
                        : finalSample;
                }

                if (waveform != null && waveformCapacity > 0 && (frameIndex & 3) == 0)
                {
                    int waveIndex = counters->WaveformCursor;
                    waveform[waveIndex % waveformCapacity] = finalSample;
                    counters->WaveformCursor = (waveIndex + 1) % waveformCapacity;
                }

                peak = math.max(peak, math.abs(finalSample));
                sumSq += finalSample * finalSample;
                written++;
                codecRef.SourcePosition += sourceAdvance;
                stateRef.CurrentSampleIndex = sourceIndex;
            }

            for (; frameIndex < sampleCount; frameIndex++)
            {
                int outputIndex = frameIndex * safeChannels;
                for (int ch = 0; ch < safeChannels; ch++)
                {
                    if (!mixOutput)
                        output[outputIndex + ch] = 0f;
                }
            }

            float rms = written > 0 ? math.sqrt(sumSq / math.max(1, written)) : 0f;
            counters->LastFaultFlags = stateRef.Flags | codecRef.FaultFlags;
            counters->LastPhraseHashID = stateRef.PhraseHashID;
            counters->LastPeak = peak;
            counters->LastRms = rms;
            WriteTelemetry(frame, in stateRef, in codecRef, telemetry, counters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void OverwriteSilence(float* output, int sampleCount, int channels)
        {
            int total = sampleCount * channels;
            for (int i = 0; i < total; i++)
                output[i] = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeSample(byte* payload, uint payloadLength, uint sampleIndex, ref VocalCodecStateDTO codec, byte codecId)
        {
            if (codecId == VocalBankConstants.CodecPcm16)
                return DecodePcm16(payload, payloadLength, sampleIndex);
            if (codecId == VocalBankConstants.CodecH8Adpcm)
                return DecodeH8Adpcm(payload, payloadLength, sampleIndex, ref codec);

            codec.FaultFlags |= VocalBankConstants.StateFlagVorbisUnsupported;
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodePcm16(byte* payload, uint payloadLength, uint sampleIndex)
        {
            uint byteIndex = sampleIndex * 2u;
            if (payload == null || byteIndex + 1u >= payloadLength)
                return 0f;

            int index = (int)byteIndex;
            int value = payload[index] | (payload[index + 1] << 8);
            if ((value & 0x8000) != 0)
                value -= 0x10000;
            return math.clamp(value / 32768f, -1f, 1f);
        }

        private static float DecodeH8Adpcm(byte* payload, uint payloadLength, uint sampleIndex, ref VocalCodecStateDTO codec)
        {
            const uint blockSamples = VocalBankConstants.DefaultBlockSamples;
            const uint blockBytes = 36u;
            uint blockIndex = sampleIndex / blockSamples;
            uint blockSampleStart = blockIndex * blockSamples;
            uint blockOffset = blockIndex * blockBytes;
            if (payload == null || blockOffset + 4u > payloadLength)
                return 0f;

            int blockByteOffset = (int)blockOffset;
            if (codec.DecodedSampleIndex < (int)blockSampleStart ||
                codec.DecodedSampleIndex >= (int)(blockSampleStart + blockSamples))
            {
                int predictor = payload[blockByteOffset] | (payload[blockByteOffset + 1] << 8);
                if ((predictor & 0x8000) != 0)
                    predictor -= 0x10000;
                codec.Predictor = (short)predictor;
                codec.Step = math.max((byte)1, payload[blockByteOffset + 2]);
                codec.DecodedSampleIndex = (int)blockSampleStart;
                codec.LastSample = math.clamp(codec.Predictor / 32768f, -1f, 1f);
            }

            int target = (int)sampleIndex;
            while (codec.DecodedSampleIndex < target)
            {
                int nextLocal = codec.DecodedSampleIndex + 1 - (int)blockSampleStart;
                uint packedOffset = blockOffset + 4u + (uint)((nextLocal - 1) >> 1);
                if (packedOffset >= payloadLength)
                    break;

                int packed = payload[(int)packedOffset];
                int nibble = ((nextLocal - 1) & 1) == 0 ? packed & 0x0F : (packed >> 4) & 0x0F;
                int signedDelta = nibble >= 8 ? nibble - 16 : nibble;
                int step = math.max(1, codec.Step);
                int predictor = math.clamp(codec.Predictor + signedDelta * step, short.MinValue, short.MaxValue);
                codec.Predictor = (short)predictor;
                codec.Step = (byte)math.clamp(step + math.abs(signedDelta) - 2, 1, 127);
                codec.DecodedSampleIndex++;
            }

            codec.LastSample = math.clamp(codec.Predictor / 32768f, -1f, 1f);
            return codec.LastSample;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApplyDearLieRadioFilter(float sample, float distortion, float smoothQuality, ref VocalCodecStateDTO codec)
        {
            float lowAlpha = math.lerp(0.055f, 0.18f, smoothQuality);
            float bandAlpha = math.lerp(0.22f, 0.38f, smoothQuality);
            codec.LowState += (sample - codec.LowState) * lowAlpha;
            float high = sample - codec.LowState;
            codec.BandState += (high - codec.BandState) * bandAlpha;
            float banded = codec.BandState * math.lerp(2.6f, 1.35f, smoothQuality);
            float mixed = math.lerp(sample, banded, distortion);
            float drive = math.lerp(1.1f, 3.8f, distortion) * math.lerp(1.28f, 1f, smoothQuality);
            float driven = mixed * drive;
            float soft = driven / math.max(1f, 1f + math.abs(driven));
            uint rng = (codec.ActivePhraseHashID ^ (uint)math.max(0, codec.DecodedSampleIndex)) * 1664525u + 1013904223u;
            float staticNoise = (((rng >> 9) & 0xFFFFu) / 65535f - 0.5f) * distortion * math.lerp(0.007f, 0.0015f, smoothQuality);
            float crushed = soft + staticNoise;
            float steps = math.lerp(40f, 384f, smoothQuality);
            return math.round(crushed * steps) / math.max(1f, steps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static void WriteTelemetry(
            uint frame,
            in VocalStateDTO state,
            in VocalCodecStateDTO codec,
            VocalTelemetryEntryDTO* telemetry,
            VocalDecodeCounters64* counters)
        {
            if (telemetry == null || counters == null)
                return;

            int index = counters->TelemetryCursor % (int)VocalBankConstants.TelemetryRingCapacity;
            telemetry[index] = new VocalTelemetryEntryDTO
            {
                Frame = frame,
                PhraseHashID = state.PhraseHashID,
                CurrentSampleIndex = state.CurrentSampleIndex,
                TotalSamples = state.TotalSamples,
                DspMicroseconds = counters->LastDspMicroseconds,
                OutputPeak = counters->LastPeak,
                OutputRms = counters->LastRms,
                QualityWeight01 = codec.QualityWeight01,
                RadioDistortion01 = codec.RadioDistortion01,
                Priority = codec.Priority,
                Flags = state.Flags | codec.FaultFlags,
                UnderrunCount = 0u,
                PayloadByteLength = codec.PayloadByteLength,
                SampleRate = codec.SampleRate,
                Codec = codec.Codec
            };
            counters->TelemetryCursor = (index + 1) % (int)VocalBankConstants.TelemetryRingCapacity;
        }
    }
}
