using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    internal unsafe sealed class AudioFrameSpscRingBuffer : IDisposable
    {
        internal const int AudioBufferCapacity = 65536;
        private const int MinimumCapacityFrames = 256;
        private const int TelemetryCapacity = 300;
        private const uint TelemetryMagic = 0x41313331u;
        private const int TelemetryHeaderBytes = 16;
        private const int TelemetryEntryBytes = 64;
        private const int TelemetryDumpBytes = TelemetryHeaderBytes + TelemetryCapacity * TelemetryEntryBytes;
        private const int TelemetryStatusWrite = 1 << 16;
        private const int TelemetryStatusOverflow = 1 << 17;
        private const int TelemetryStatusNonFinite = 1 << 18;
        private const int TelemetryStatusBridgeFailure = 1 << 19;
        private const int TelemetryStatusSharedStateInvalid = 1 << 20;
        private const SystemID VaultOwner = SystemID.AudioFrameRing;
        private static readonly ulong TelemetryMutationGuardMask = AudioFrameRingMutationGuardBit(BufferID.AudioFrameRingTelemetry);
        private const int AudioBufferCapacityPowerOfTwoGuard =
            1 / ((AudioBufferCapacity > 1 &&
                  (AudioBufferCapacity & (AudioBufferCapacity - 1)) == 0) ? 1 : 0);

        private ref struct RingVaultViews
        {
            public NativeArray<float> Frames;
            public NativeArray<int> SharedState;
            public NativeArray<AudioBridgeTelemetryEntry> Telemetry;
            public NativeArray<byte> DumpBytes;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct AudioBridgeTelemetryEntry
        {
            [FieldOffset(0)] public long ProducedSampleCount;
            [FieldOffset(8)] public long DspExecutionTicks;
            [FieldOffset(16)] public int WriteIndex;
            [FieldOffset(20)] public int ReadIndex;
            [FieldOffset(24)] public int DroppedSampleCount;
            [FieldOffset(28)] public int BufferedFrames;
            [FieldOffset(32)] public int WritableFrames;
            [FieldOffset(36)] public int StatusBits;
            [FieldOffset(40)] public uint Sequence;
            [FieldOffset(44)] public int CapacityFrames;
            [FieldOffset(48)] public int SourceChannels;
            [FieldOffset(52)] public int NonFiniteCount;
            [FieldOffset(56)] public uint StateHash;
#pragma warning disable 0169
            [FieldOffset(60)] private uint _pad0;
#pragma warning restore 0169
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryHeaderBytes)]
        private struct AudioBridgeTelemetryDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint EntryCount;
            [FieldOffset(8)] public uint StructSizeBytes;
            [FieldOffset(12)] public uint Reason;
        }

        private IDataVault _dataVault;
        private VaultGenerationHandle<AudioBridgeTelemetryEntry> _telemetryHandle;
        private void* _framesPtr;
        private void* _sharedStatePtr;
        private void* _telemetryPtr;
        private void* _telemetryDumpBytesPtr;
        private int _frameSampleCapacity;
        private int _capacityFrames;
        private int _capacityMask;
        private int _sourceChannels = 1;
        private int _overflowDropCount;
        private int _lastTelemetryOverflowDropCount;
        private int _telemetryWriteIndex;
        private int _telemetrySequence;
        private int _telemetryDumpQueued;
        private long _producedSampleCount;
        private long _lastDspExecutionTicks;

        public bool IsCreated => TryResolveRingViews(out _);
        public int CapacityFrames => _capacityFrames;
        public int SourceChannels => _sourceChannels;
        public int OverflowDropCount => Volatile.Read(ref _overflowDropCount);

        public int BufferedFrames
        {
            get
            {
                if (!TryResolveRingViews(out RingVaultViews views))
                    return 0;

                if (!HasValidPowerOfTwoState())
                    return 0;

                if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex) ||
                    !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex))
                    return 0;

                return (writeIndex - readIndex) & _capacityMask;
            }
        }

        public int WritableFrames
        {
            get
            {
                if (!TryResolveRingViews(out RingVaultViews views) || !HasValidPowerOfTwoState())
                    return 0;

                if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex) ||
                    !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex))
                    return 0;

                int bufferedFrames = (writeIndex - readIndex) & _capacityMask;
                return math.max(0, _capacityFrames - bufferedFrames - 1);
            }
        }

        public void GetState(out int bufferedFrames, out int writableFrames)
        {
            if (!TryResolveRingViews(out RingVaultViews views))
            {
                bufferedFrames = 0;
                writableFrames = 0;
                return;
            }

            if (!HasValidPowerOfTwoState())
            {
                bufferedFrames = 0;
                writableFrames = 0;
                return;
            }

            if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex) ||
                !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex))
            {
                bufferedFrames = 0;
                writableFrames = 0;
                return;
            }

            bufferedFrames = (writeIndex - readIndex) & _capacityMask;
            writableFrames = _capacityFrames - bufferedFrames - 1;
        }

        public void Initialize(int capacityFrames, int sourceChannels = 1)
        {
            if (sourceChannels < 1 || sourceChannels > 2)
            {
                RecordBridgeFailure(NativeAudioKernelBridgeStatus.SharedStateInvalid);
                return;
            }

            int resolvedCapacity = ResolvePowerOfTwoCapacity(capacityFrames);
            int resolvedChannels = sourceChannels;
            if (IsCreated && _capacityFrames == resolvedCapacity && _sourceChannels == resolvedChannels)
            {
                Clear();
                return;
            }

            if (!TryDispose())
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (resolvedCapacity > int.MaxValue / resolvedChannels)
                return;

            int frameSampleCapacity = resolvedCapacity * resolvedChannels;
            _dataVault = vault;
            _telemetryHandle = vault.EnsureGenerationHandle<AudioBridgeTelemetryEntry>(
                BufferID.AudioFrameRingTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            if (!TryAllocateNativeBridgeBuffers(frameSampleCapacity))
            {
                Dispose();
                return;
            }

            if (!TryResolveRingViews(out _))
            {
                Dispose();
                return;
            }

            _capacityFrames = resolvedCapacity;
            _capacityMask = resolvedCapacity - 1;
            if (!HasPowerOfTwoCapacity(_capacityFrames, _capacityMask))
            {
                Dispose();
                return;
            }

            _sourceChannels = resolvedChannels;
            Volatile.Write(ref _overflowDropCount, 0);
            Volatile.Write(ref _lastTelemetryOverflowDropCount, 0);
            Volatile.Write(ref _telemetryWriteIndex, 0);
            Volatile.Write(ref _telemetrySequence, 0);
            Volatile.Write(ref _telemetryDumpQueued, 0);
            Interlocked.Exchange(ref _producedSampleCount, 0L);
            Interlocked.Exchange(ref _lastDspExecutionTicks, 0L);
            Clear();
        }

        public void Clear()
        {
            if (!TryResolveRingViews(out RingVaultViews views))
                return;

            if (!HasPowerOfTwoCapacity(_capacityFrames, _capacityMask))
                return;

            WriteSharedMetadata(ref views);
            WriteSharedIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, 0);
            WriteSharedIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, 0);
        }

        public bool TryWrite(NativeArray<float> source, int frameCount)
        {
            return TryWriteInterleaved(source, frameCount, 1);
        }

        public bool TryWriteInterleaved(NativeArray<float> source, int frameCount, int sourceChannels)
        {
            if (!TryResolveRingViews(out RingVaultViews views) || !source.IsCreated || frameCount <= 0)
                return false;

            if (!HasValidPowerOfTwoState())
                return false;

            if (sourceChannels < 1 || sourceChannels > 2)
                return false;

            int safeChannels = sourceChannels;
            if (safeChannels != _sourceChannels)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length / safeChannels);
            if (safeFrameCount != frameCount || safeFrameCount <= 0)
                return false;

            NativeArray<float> frames = views.Frames;
            if (!frames.IsCreated || _frameSampleCapacity <= 0 || frames.Length < _frameSampleCapacity)
                return false;

            if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex) ||
                !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex))
            {
                RecordTelemetry(ref views, 0, 0, TelemetryStatusSharedStateInvalid, 0);
                RequestTelemetryDump(ref views, (uint)TelemetryStatusSharedStateInvalid);
                return false;
            }

            int availableFrames = (writeIndex - readIndex) & _capacityMask;
            int freeFrames = _capacityFrames - availableFrames - 1;
            if (safeFrameCount > freeFrames)
            {
                int overflowDropCount = Interlocked.Increment(ref _overflowDropCount);
                bool shouldReport = overflowDropCount == 1 ||
                                    (overflowDropCount & (overflowDropCount - 1)) == 0;
                if (shouldReport &&
                    overflowDropCount != Volatile.Read(ref _lastTelemetryOverflowDropCount) &&
                    Interlocked.Exchange(ref _lastTelemetryOverflowDropCount, overflowDropCount) != overflowDropCount)
                {
                    CrashTelemetryBuffer.ReportAudioOverflowDropWarning(
                        overflowDropCount,
                        availableFrames,
                        freeFrames);
                }

                RecordTelemetry(ref views, readIndex, writeIndex, TelemetryStatusOverflow, 0);
                return false;
            }

            NativeArray<float> sourceCopy = source;
            float* sourcePtr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceCopy);
            float* framesPtr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(frames);
            int nonFiniteCount = 0;
            if (safeChannels == 2)
            {
                for (int i = 0; i < safeFrameCount; i++)
                {
                    int frameWriteIndex = ((writeIndex + i) & _capacityMask) << 1;
                    int frameSourceIndex = i << 1;
                    float left = sourcePtr[frameSourceIndex];
                    float right = sourcePtr[frameSourceIndex + 1];
                    if (!math.isfinite(left))
                    {
                        left = 0f;
                        nonFiniteCount++;
                    }

                    if (!math.isfinite(right))
                    {
                        right = 0f;
                        nonFiniteCount++;
                    }

                    framesPtr[frameWriteIndex] = left;
                    framesPtr[frameWriteIndex + 1] = right;
                }
            }
            else
            {
                for (int i = 0; i < safeFrameCount; i++)
                {
                    float sample = sourcePtr[i];
                    if (!math.isfinite(sample))
                    {
                        sample = 0f;
                        nonFiniteCount++;
                    }

                    framesPtr[(writeIndex + i) & _capacityMask] = sample;
                }
            }

            int nextWriteIndex = (writeIndex + safeFrameCount) & _capacityMask;
            WriteSharedIndex(
                ref views,
                NativeAudioKernelRingBufferDescriptor.WriteIndexSlot,
                nextWriteIndex);
            Interlocked.Add(ref _producedSampleCount, (long)safeFrameCount * safeChannels);
            int statusBits = TelemetryStatusWrite;
            if (nonFiniteCount > 0)
                statusBits |= TelemetryStatusNonFinite;
            RecordTelemetry(ref views, readIndex, nextWriteIndex, statusBits, nonFiniteCount);
            if (nonFiniteCount > 0)
                RequestTelemetryDump(ref views, (uint)TelemetryStatusNonFinite);
            return true;
        }

        public void RecordDspExecutionTicks(long ticks)
        {
            if (ticks < 0L)
                ticks = 0L;

            Interlocked.Exchange(ref _lastDspExecutionTicks, ticks);
            if (!TryResolveRingViews(out RingVaultViews views))
                return;

            if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex) ||
                !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex))
            {
                RecordTelemetry(ref views, 0, 0, TelemetryStatusSharedStateInvalid, 0);
                RequestTelemetryDump(ref views, (uint)TelemetryStatusSharedStateInvalid);
                return;
            }

            RecordTelemetry(ref views, readIndex, writeIndex, 0, 0);
        }

        public void RecordBridgeFailure(NativeAudioKernelBridgeStatus status)
        {
            if (!TryResolveRingViews(out RingVaultViews views))
                return;

            int statusBits = TelemetryStatusBridgeFailure | (int)status;
            if (!TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, out int readIndex) ||
                !TryReadSharedFrameIndex(ref views, NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, out int writeIndex))
            {
                readIndex = 0;
                writeIndex = 0;
                statusBits |= TelemetryStatusSharedStateInvalid;
            }

            RecordTelemetry(ref views, readIndex, writeIndex, statusBits, 0);
            RequestTelemetryDump(ref views, (uint)statusBits);
        }

        public NativeAudioKernelRingBufferDescriptor CreateNativeDescriptor()
        {
            return TryCreateNativeDescriptor(out NativeAudioKernelRingBufferDescriptor descriptor, out _)
                ? descriptor
                : default;
        }

        public bool TryCreateNativeDescriptor(
            out NativeAudioKernelRingBufferDescriptor descriptor,
            out NativeAudioKernelBridgeStatus status)
        {
            descriptor = default;
            if (!TryResolveRingViews(out RingVaultViews views))
            {
                status = NativeAudioKernelBridgeStatus.SharedStateInvalid;
                return false;
            }

            if (!HasPowerOfTwoCapacity(_capacityFrames, _capacityMask))
            {
                status = NativeAudioKernelBridgeStatus.CapacityInvalid;
                return false;
            }

            NativeArray<float> frames = views.Frames;
            NativeArray<int> sharedState = views.SharedState;
            if (!frames.IsCreated ||
                !sharedState.IsCreated ||
                _frameSampleCapacity <= 0 ||
                frames.Length < _frameSampleCapacity ||
                sharedState.Length < NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount)
            {
                status = NativeAudioKernelBridgeStatus.SharedStateInvalid;
                return false;
            }

            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sharedState);
            IntPtr sharedStateBase = (IntPtr)sharedStatePtr;
            IntPtr readIndexPtr = (IntPtr)(sharedStatePtr + NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
            IntPtr writeIndexPtr = (IntPtr)(sharedStatePtr + NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
            descriptor = default;
            descriptor.DescriptorMagic = NativeAudioKernelRingBufferDescriptor.DescriptorMagicValue;
            descriptor.Frames = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(frames);
            descriptor.SharedState = sharedStateBase;
            descriptor.ReadIndex = readIndexPtr;
            descriptor.WriteIndex = writeIndexPtr;
            descriptor.CapacityFrames = _capacityFrames;
            descriptor.CapacityMask = _capacityMask;
            descriptor.SharedStateLengthInts = sharedState.Length;
            descriptor.SourceChannels = _sourceChannels;

            bool valid = HectonSensoryKernelNativeBridge.IsDescriptorValid(in descriptor, out status);
            if (!valid)
                RecordBridgeFailure(status);
            return valid;
        }

        internal static int ResolvePowerOfTwoCapacity(int capacityFrames)
        {
            int requestedCapacity = math.max(MinimumCapacityFrames, capacityFrames);
            if (requestedCapacity >= AudioBufferCapacity)
                return AudioBufferCapacity;

            return math.max(MinimumCapacityFrames, NextPowerOfTwo(requestedCapacity));
        }

        public void Dispose()
        {
            TryDispose();
        }

        public bool TryDispose()
        {
            IDataVault vault = _dataVault;
            if (HasNativeBridgeBuffers())
            {
                bool cleared = HectonSensoryKernelNativeBridge.TryClear(out NativeAudioKernelBridgeStatus clearStatus);
                if (!cleared)
                {
                    RecordBridgeFailure(clearStatus);
                    if (H8Memory.IsInitialized &&
                        (clearStatus & NativeAudioKernelBridgeStatus.PluginUnavailable) == 0)
                    {
                        return false;
                    }
                }
            }

            if (TryResolveRingViews(out RingVaultViews views))
                TryMirrorTelemetryToDataVault(ref views);

            ReleaseNativeBridgeBuffers();
            ReleaseVaultBuffer(vault, ref _telemetryHandle, BufferID.AudioFrameRingTelemetry);

            _dataVault = null;
            _capacityFrames = 0;
            _capacityMask = 0;
            _sourceChannels = 1;
            _frameSampleCapacity = 0;
            Volatile.Write(ref _overflowDropCount, 0);
            Volatile.Write(ref _telemetryDumpQueued, 0);
            return true;
        }

        private bool HasNativeBridgeBuffers()
        {
            return _framesPtr != null ||
                   _sharedStatePtr != null ||
                   _telemetryPtr != null ||
                   _telemetryDumpBytesPtr != null;
        }

        private bool TryAllocateNativeBridgeBuffers(int frameSampleCapacity)
        {
            if (frameSampleCapacity <= 0)
                return false;

            _framesPtr = H8Memory.AllocateRaw(
                (long)frameSampleCapacity * sizeof(float),
                NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes,
                VaultOwner,
                Allocator.Persistent,
                clearMemory: true);
            if (_framesPtr == null)
                return false;

            _sharedStatePtr = H8Memory.AllocateRaw(
                (long)NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount * sizeof(int),
                NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes,
                VaultOwner,
                Allocator.Persistent,
                clearMemory: true);
            if (_sharedStatePtr == null)
            {
                ReleaseNativeBridgeBuffers();
                return false;
            }

            _telemetryPtr = H8Memory.AllocateRaw(
                (long)TelemetryCapacity * TelemetryEntryBytes,
                NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes,
                VaultOwner,
                Allocator.Persistent,
                clearMemory: true);
            if (_telemetryPtr == null)
            {
                ReleaseNativeBridgeBuffers();
                return false;
            }

            _telemetryDumpBytesPtr = H8Memory.AllocateRaw(
                TelemetryDumpBytes,
                NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes,
                VaultOwner,
                Allocator.Persistent,
                clearMemory: true);
            if (_telemetryDumpBytesPtr == null)
            {
                ReleaseNativeBridgeBuffers();
                return false;
            }

            _frameSampleCapacity = frameSampleCapacity;
            return true;
        }

        private void ReleaseNativeBridgeBuffers()
        {
            if (!H8Memory.IsInitialized)
            {
                _telemetryDumpBytesPtr = null;
                _telemetryPtr = null;
                _sharedStatePtr = null;
                _framesPtr = null;
                _frameSampleCapacity = 0;
                return;
            }

            if (_telemetryDumpBytesPtr != null)
            {
                H8Memory.FreeRaw(_telemetryDumpBytesPtr, Allocator.Persistent, VaultOwner);
                _telemetryDumpBytesPtr = null;
            }

            if (_telemetryPtr != null)
            {
                H8Memory.FreeRaw(_telemetryPtr, Allocator.Persistent, VaultOwner);
                _telemetryPtr = null;
            }

            if (_sharedStatePtr != null)
            {
                H8Memory.FreeRaw(_sharedStatePtr, Allocator.Persistent, VaultOwner);
                _sharedStatePtr = null;
            }

            if (_framesPtr != null)
            {
                H8Memory.FreeRaw(_framesPtr, Allocator.Persistent, VaultOwner);
                _framesPtr = null;
            }

            _frameSampleCapacity = 0;
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID == (uint)expectedBufferId &&
                handle.SystemID == (uint)VaultOwner &&
                handle.Generation != 0u)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private bool TryResolveRingViews(out RingVaultViews views)
        {
            views = default;
            if (!H8Memory.IsInitialized ||
                _framesPtr == null ||
                _sharedStatePtr == null ||
                _telemetryPtr == null ||
                _telemetryDumpBytesPtr == null ||
                _frameSampleCapacity <= 0)
                return false;

            views.Frames = H8Memory.CreateNativeArrayView<float>(_framesPtr, _frameSampleCapacity);
            views.SharedState = H8Memory.CreateNativeArrayView<int>(
                _sharedStatePtr,
                NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount);
            views.Telemetry = H8Memory.CreateNativeArrayView<AudioBridgeTelemetryEntry>(
                _telemetryPtr,
                TelemetryCapacity);
            views.DumpBytes = H8Memory.CreateNativeArrayView<byte>(_telemetryDumpBytesPtr, TelemetryDumpBytes);
            if (!views.Frames.IsCreated ||
                !views.SharedState.IsCreated ||
                !views.Telemetry.IsCreated ||
                !views.DumpBytes.IsCreated)
            {
                views = default;
                return false;
            }

            return true;
        }

        private static int ReadSharedIndex(ref RingVaultViews views, int slot)
        {
            NativeArray<int> sharedState = views.SharedState;
            if (!sharedState.IsCreated || (uint)slot >= (uint)sharedState.Length)
                return 0;

            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sharedState);
            return Volatile.Read(ref sharedStatePtr[slot]);
        }

        private bool TryReadSharedFrameIndex(ref RingVaultViews views, int slot, out int value)
        {
            value = 0;
            if (!HasValidPowerOfTwoState())
                return false;

            int rawIndex = ReadSharedIndex(ref views, slot);
            if ((uint)rawIndex >= (uint)_capacityFrames)
                return false;

            value = rawIndex;
            return true;
        }

        private static void WriteSharedIndex(ref RingVaultViews views, int slot, int value)
        {
            NativeArray<int> sharedState = views.SharedState;
            if (!sharedState.IsCreated || (uint)slot >= (uint)sharedState.Length)
                return;

            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sharedState);
            Volatile.Write(ref sharedStatePtr[slot], value);
        }

        private void RecordTelemetry(
            ref RingVaultViews views,
            int readIndex,
            int writeIndex,
            int statusBits,
            int nonFiniteCount)
        {
            WriteTelemetryEntry(views.Telemetry, readIndex, writeIndex, statusBits, nonFiniteCount);
        }

        private void RecordTelemetry(
            int readIndex,
            int writeIndex,
            int statusBits,
            int nonFiniteCount)
        {
            if (!TryResolveRingViews(out RingVaultViews views))
                return;

            RecordTelemetry(ref views, readIndex, writeIndex, statusBits, nonFiniteCount);
        }

        private void WriteTelemetryEntry(
            NativeArray<AudioBridgeTelemetryEntry> telemetry,
            int readIndex,
            int writeIndex,
            int statusBits,
            int nonFiniteCount)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int safeCapacity = math.min(TelemetryCapacity, telemetry.Length);
            int index = Interlocked.Increment(ref _telemetryWriteIndex) - 1;
            if (index < 0)
            {
                Volatile.Write(ref _telemetryWriteIndex, 1);
                index = 0;
            }

            if (index >= safeCapacity)
                index %= safeCapacity;

            int bufferedFrames = HasValidPowerOfTwoState()
                ? (writeIndex - readIndex) & _capacityMask
                : 0;
            int writableFrames = HasValidPowerOfTwoState()
                ? math.max(0, _capacityFrames - bufferedFrames - 1)
                : 0;
            long producedSamples = Interlocked.Read(ref _producedSampleCount);
            long dspTicks = Interlocked.Read(ref _lastDspExecutionTicks);
            int droppedSamples = Volatile.Read(ref _overflowDropCount);
            uint sequence = unchecked((uint)Interlocked.Increment(ref _telemetrySequence));
            if (sequence == 0u)
                sequence = unchecked((uint)Interlocked.Increment(ref _telemetrySequence));

            AudioBridgeTelemetryEntry* telemetryPtr = (AudioBridgeTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry);
            if (telemetryPtr == null)
                return;

            ref AudioBridgeTelemetryEntry target = ref telemetryPtr[index];
            Volatile.Write(ref target.Sequence, 0u);
            target.ProducedSampleCount = producedSamples;
            target.DspExecutionTicks = dspTicks;
            target.WriteIndex = writeIndex;
            target.ReadIndex = readIndex;
            target.DroppedSampleCount = droppedSamples;
            target.BufferedFrames = bufferedFrames;
            target.WritableFrames = writableFrames;
            target.StatusBits = statusBits;
            target.CapacityFrames = _capacityFrames;
            target.SourceChannels = _sourceChannels;
            target.NonFiniteCount = nonFiniteCount;
            target.StateHash = HashTelemetryState(readIndex, writeIndex, bufferedFrames, writableFrames, droppedSamples, statusBits, sequence);
            Volatile.Write(ref target.Sequence, sequence);
        }

        private void RequestTelemetryDump(ref RingVaultViews views, uint reason)
        {
            NativeArray<byte> dumpBytes = views.DumpBytes;
            NativeArray<AudioBridgeTelemetryEntry> telemetry = views.Telemetry;
            if (!dumpBytes.IsCreated ||
                !telemetry.IsCreated ||
                dumpBytes.Length < TelemetryDumpBytes)
                return;

            if (UnsafeUtility.SizeOf<AudioBridgeTelemetryEntry>() != TelemetryEntryBytes ||
                UnsafeUtility.SizeOf<AudioBridgeTelemetryDumpHeader>() != TelemetryHeaderBytes)
                return;

            if (Interlocked.CompareExchange(ref _telemetryDumpQueued, 1, 0) != 0)
                return;

            try
            {
                byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(dumpBytes);
                UnsafeUtility.MemClear(snapshotPtr, TelemetryDumpBytes);
                WriteTelemetryDumpHeader(snapshotPtr, 0, reason);

                WriteTelemetrySnapshot(telemetry, snapshotPtr, reason);
                TryMirrorTelemetryToDataVault(ref views);

                HectonSensoryKernelNativeBridge.TryDumpAudioBridgeTelemetry(snapshotPtr, TelemetryDumpBytes);
            }
            finally
            {
                Volatile.Write(ref _telemetryDumpQueued, 0);
            }
        }

        private void TryMirrorTelemetryToDataVault(ref RingVaultViews views)
        {
            NativeArray<AudioBridgeTelemetryEntry> source = views.Telemetry;
            if (!source.IsCreated ||
                !TryAcquireTelemetryMutationView(out NativeArray<AudioBridgeTelemetryEntry> destination, out IDataVault guardVault))
            {
                return;
            }

            try
            {
                int count = math.min(math.min(TelemetryCapacity, source.Length), destination.Length);
                AudioBridgeTelemetryEntry* sourcePtr = (AudioBridgeTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                if (sourcePtr == null)
                    return;

                for (int i = 0; i < count; i++)
                {
                    if (TryReadTelemetryEntryStable(sourcePtr + i, out AudioBridgeTelemetryEntry entry))
                        destination[i] = entry;
                    else
                        destination[i] = default;
                }
            }
            finally
            {
                ReleaseTelemetryMutationGuard(guardVault);
            }
        }

        private bool TryAcquireTelemetryMutationView(out NativeArray<AudioBridgeTelemetryEntry> telemetry, out IDataVault guardVault)
        {
            telemetry = default;
            guardVault = _dataVault;
            if (guardVault == null ||
                _telemetryHandle.BufferID == 0u ||
                guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
            {
                return false;
            }

            bool acquired = true;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    _telemetryHandle.BufferID != (uint)BufferID.AudioFrameRingTelemetry ||
                    _telemetryHandle.SystemID != (uint)VaultOwner ||
                    _telemetryHandle.Generation == 0u ||
                    !guardVault.TryResolveHandle(in _telemetryHandle, out telemetry) ||
                    !telemetry.IsCreated)
                {
                    return false;
                }

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    ReleaseTelemetryMutationGuard(guardVault);
            }
        }

        private static void ReleaseTelemetryMutationGuard(IDataVault guardVault)
        {
            guardVault?.ReleaseMutationGuard(TelemetryMutationGuardMask);
        }

        private static ulong AudioFrameRingMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void WriteTelemetrySnapshot(NativeArray<AudioBridgeTelemetryEntry> telemetry, byte* snapshotPtr, uint reason)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || snapshotPtr == null)
                return;

            int count = math.min(TelemetryCapacity, telemetry.Length);
            WriteTelemetryDumpHeader(snapshotPtr, count, reason);
            byte* entryPtr = snapshotPtr + TelemetryHeaderBytes;
            AudioBridgeTelemetryEntry* telemetryPtr = (AudioBridgeTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            if (telemetryPtr == null)
                return;

            int currentWrite = Volatile.Read(ref _telemetryWriteIndex);
            int start = currentWrite >= count ? currentWrite % count : 0;
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = start + i;
                if (sourceIndex >= count)
                    sourceIndex -= count;

                if (!TryReadTelemetryEntryStable(telemetryPtr + sourceIndex, out AudioBridgeTelemetryEntry entry))
                    entry = default;

                UnsafeUtility.MemCpy(entryPtr + i * TelemetryEntryBytes, &entry, TelemetryEntryBytes);
            }
        }

        private static bool TryReadTelemetryEntryStable(
            AudioBridgeTelemetryEntry* source,
            out AudioBridgeTelemetryEntry entry)
        {
            entry = default;
            if (source == null)
                return false;

            uint sequenceBefore = Volatile.Read(ref source->Sequence);
            if (sequenceBefore == 0u)
                return false;

            AudioBridgeTelemetryEntry copy = default;
            UnsafeUtility.MemCpy(&copy, source, TelemetryEntryBytes);
            uint sequenceAfter = Volatile.Read(ref source->Sequence);
            if (sequenceBefore != sequenceAfter ||
                sequenceAfter == 0u ||
                copy.Sequence != sequenceAfter)
            {
                entry = default;
                return false;
            }

            entry = copy;

            uint expectedHash = HashTelemetryState(
                entry.ReadIndex,
                entry.WriteIndex,
                entry.BufferedFrames,
                entry.WritableFrames,
                entry.DroppedSampleCount,
                entry.StatusBits,
                sequenceAfter);
            if (entry.StateHash != expectedHash)
            {
                entry = default;
                return false;
            }

            return true;
        }

        private static void WriteTelemetryDumpHeader(byte* snapshotPtr, int count, uint reason)
        {
            if (snapshotPtr == null)
                return;

            AudioBridgeTelemetryDumpHeader header = default;
            header.Magic = TelemetryMagic;
            header.EntryCount = (uint)count;
            header.StructSizeBytes = (uint)TelemetryEntryBytes;
            header.Reason = reason;
            UnsafeUtility.MemCpy(snapshotPtr, &header, TelemetryHeaderBytes);
        }

        private static uint HashTelemetryState(
            int readIndex,
            int writeIndex,
            int bufferedFrames,
            int writableFrames,
            int droppedSamples,
            int statusBits,
            uint sequence)
        {
            uint hash = 2166136261u;
            hash = HashStep(hash, (uint)readIndex);
            hash = HashStep(hash, (uint)writeIndex);
            hash = HashStep(hash, (uint)bufferedFrames);
            hash = HashStep(hash, (uint)writableFrames);
            hash = HashStep(hash, (uint)droppedSamples);
            hash = HashStep(hash, (uint)statusBits);
            return HashStep(hash, sequence);
        }

        private static uint HashStep(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private void WriteSharedMetadata(ref RingVaultViews views)
        {
            if (!HasPowerOfTwoCapacity(_capacityFrames, _capacityMask))
                return;

            WriteSharedIndex(ref views, NativeAudioKernelRingBufferDescriptor.CapacityFramesSlot, _capacityFrames);
            WriteSharedIndex(ref views, NativeAudioKernelRingBufferDescriptor.CapacityMaskSlot, _capacityMask);
            WriteSharedIndex(
                ref views,
                NativeAudioKernelRingBufferDescriptor.GuardValueSlotA,
                NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueA);
            WriteSharedIndex(
                ref views,
                NativeAudioKernelRingBufferDescriptor.GuardValueSlotB,
                NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueB);
            WriteSharedIndex(ref views, NativeAudioKernelRingBufferDescriptor.SourceChannelsSlot, _sourceChannels);
        }

        private bool HasValidPowerOfTwoState()
        {
            return HasPowerOfTwoCapacity(_capacityFrames, _capacityMask);
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            int power = 1;
            int growthWatchdog = 31;
            while (power < value && growthWatchdog-- > 0)
            {
                if (power > (int.MaxValue >> 1))
                    return AudioBufferCapacity;

                power <<= 1;
            }

            return power;
        }

        private static bool HasPowerOfTwoCapacity(int capacityFrames, int capacityMask)
        {
            return capacityFrames > 1 &&
                   capacityMask == capacityFrames - 1 &&
                   IsPowerOfTwo(capacityFrames);
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

    }
}
