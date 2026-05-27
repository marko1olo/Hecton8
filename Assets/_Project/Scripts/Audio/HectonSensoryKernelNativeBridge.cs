using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;

namespace Hecton8.Audio
{
    internal static class NativeAudioKernelBridgeLayout
    {
        public const int RingBufferDescriptorStrideBytes = 56;
    }

    [Flags]
    internal enum NativeAudioKernelBridgeStatus
    {
        None = 0,
        Active = 1 << 0,
        DescriptorMagicMismatch = 1 << 1,
        NullPointer = 1 << 2,
        AlignmentInvalid = 1 << 3,
        CapacityInvalid = 1 << 4,
        SharedStateInvalid = 1 << 5,
        Cleared = 1 << 6,
        Busy = 1 << 7,
        PluginUnavailable = 1 << 30
    }

    [StructLayout(LayoutKind.Explicit, Size = NativeAudioKernelBridgeLayout.RingBufferDescriptorStrideBytes)]
    internal struct NativeAudioKernelRingBufferDescriptor
    {
        public const uint DescriptorMagicValue = 0x484B3031u;
        public const int RequiredAlignmentBytes = 8;
        public const int MaximumCapacityFrames = 65536;
        public const int ReadIndexSlot = 0;
        public const int WriteIndexSlot = 2;
        public const int CapacityFramesSlot = 4;
        public const int CapacityMaskSlot = 6;
        public const int GuardValueSlotA = 8;
        public const int GuardValueSlotB = 10;
        public const int SourceChannelsSlot = 12;
        public const int SharedStateSlotCount = 14;
        public const int SharedStateGuardValueA = unchecked((int)0x48454354);
        public const int SharedStateGuardValueB = unchecked((int)0x4F4E2D38);

        [FieldOffset(0)]
        public IntPtr Frames;
        [FieldOffset(8)]
        public IntPtr SharedState;
        [FieldOffset(16)]
        public IntPtr ReadIndex;
        [FieldOffset(24)]
        public IntPtr WriteIndex;
        [FieldOffset(32)]
        public uint DescriptorMagic;
        [FieldOffset(36)]
        public int CapacityFrames;
        [FieldOffset(40)]
        public int CapacityMask;
        [FieldOffset(44)]
        public int SharedStateLengthInts;
#pragma warning disable 0169
        [FieldOffset(48)]
        public int SourceChannels;
        [FieldOffset(52)]
        private int _pad0;
#pragma warning restore 0169
    }

    internal static unsafe class HectonSensoryKernelNativeBridge
    {
        private const int RegisterRetryAttempts = 2;

        public static bool IsDescriptorValid(in NativeAudioKernelRingBufferDescriptor descriptor, out NativeAudioKernelBridgeStatus status)
        {
            status = NativeAudioKernelBridgeStatus.None;

            if (descriptor.DescriptorMagic != NativeAudioKernelRingBufferDescriptor.DescriptorMagicValue)
                status |= NativeAudioKernelBridgeStatus.DescriptorMagicMismatch;

            if (descriptor.Frames == IntPtr.Zero ||
                descriptor.SharedState == IntPtr.Zero ||
                descriptor.ReadIndex == IntPtr.Zero ||
                descriptor.WriteIndex == IntPtr.Zero)
            {
                status |= NativeAudioKernelBridgeStatus.NullPointer;
            }

            if (!IsAligned(descriptor.Frames, NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes) ||
                !IsAligned(descriptor.SharedState, NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes) ||
                !IsAligned(descriptor.ReadIndex, NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes) ||
                !IsAligned(descriptor.WriteIndex, NativeAudioKernelRingBufferDescriptor.RequiredAlignmentBytes))
            {
                status |= NativeAudioKernelBridgeStatus.AlignmentInvalid;
            }

            if (descriptor.CapacityFrames <= 1 ||
                descriptor.CapacityFrames > NativeAudioKernelRingBufferDescriptor.MaximumCapacityFrames ||
                descriptor.CapacityMask != descriptor.CapacityFrames - 1 ||
                !IsPowerOfTwo(descriptor.CapacityFrames))
            {
                status |= NativeAudioKernelBridgeStatus.CapacityInvalid;
            }

            if (descriptor.SourceChannels < 1 || descriptor.SourceChannels > 2)
                status |= NativeAudioKernelBridgeStatus.SharedStateInvalid;

            if (descriptor.SharedStateLengthInts < NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount)
                status |= NativeAudioKernelBridgeStatus.SharedStateInvalid;

            if (!HasValidSharedStatePointerLayout(in descriptor))
                status |= NativeAudioKernelBridgeStatus.SharedStateInvalid;

            if (status == NativeAudioKernelBridgeStatus.None &&
                !HasValidSharedStateMetadata(in descriptor))
            {
                status |= NativeAudioKernelBridgeStatus.SharedStateInvalid;
            }

            if (status == NativeAudioKernelBridgeStatus.None)
                status = NativeAudioKernelBridgeStatus.Active;

            return status == NativeAudioKernelBridgeStatus.Active;
        }

        public static bool TryRegisterWithRetryGate(
            ref NativeAudioKernelRingBufferDescriptor descriptor,
            out NativeAudioKernelBridgeStatus status)
        {
            return TryRegisterWithRetryGate(ref descriptor, RegisterRetryAttempts, out status);
        }

        public static bool TryRegisterWithRetryGate(
            ref NativeAudioKernelRingBufferDescriptor descriptor,
            int maxAttempts,
            out NativeAudioKernelBridgeStatus status)
        {
            if (!IsDescriptorValid(in descriptor, out status))
                return false;

            int attempts = Math.Max(1, maxAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (TryRegister(ref descriptor, out status))
                    return true;

                if ((status & NativeAudioKernelBridgeStatus.PluginUnavailable) != 0)
                    break;
            }

            return false;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private const string PluginName = "HectonAudioKernel";

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_RegisterSharedRingBufferAndGetStatus")]
        private static extern int RegisterSharedRingBuffer(ref NativeAudioKernelRingBufferDescriptor descriptor);

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_ClearSharedRingBufferAndGetStatus")]
        private static extern int ClearSharedRingBuffer();

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_GetSharedRingBufferStatus")]
        private static extern int GetSharedRingBufferStatusNative();

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_DumpAudioBridgeTelemetry")]
        private static extern int DumpAudioBridgeTelemetryNative(void* bytes, int byteCount);

        public static NativeAudioKernelBridgeStatus GetStatus()
        {
            if (!HectonNativeBridge.IsAvailable(HectonNativeLibrary.AudioKernel))
                return NativeAudioKernelBridgeStatus.PluginUnavailable;

            try
            {
                return (NativeAudioKernelBridgeStatus)GetSharedRingBufferStatusNative();
            }
            catch (Exception exception) when (HectonNativeBridge.IsNativeLoadFailure(exception))
            {
                HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.AudioKernel, exception);
                return NativeAudioKernelBridgeStatus.PluginUnavailable;
            }
        }

        public static bool TryRegister(ref NativeAudioKernelRingBufferDescriptor descriptor)
        {
            return TryRegister(ref descriptor, out _);
        }

        public static bool TryRegister(ref NativeAudioKernelRingBufferDescriptor descriptor, out NativeAudioKernelBridgeStatus status)
        {
            if (!IsDescriptorValid(in descriptor, out status))
                return false;

            if (!HectonNativeBridge.IsAvailable(HectonNativeLibrary.AudioKernel))
            {
                status = NativeAudioKernelBridgeStatus.PluginUnavailable;
                return false;
            }

            try
            {
                status = (NativeAudioKernelBridgeStatus)RegisterSharedRingBuffer(ref descriptor);
                return (status & NativeAudioKernelBridgeStatus.Active) != 0 &&
                       (status & NativeAudioKernelBridgeStatus.Busy) == 0;
            }
            catch (Exception exception) when (HectonNativeBridge.IsNativeLoadFailure(exception))
            {
                HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.AudioKernel, exception);
                status = NativeAudioKernelBridgeStatus.PluginUnavailable;
                return false;
            }
        }

        public static bool TryClear()
        {
            return TryClear(out _);
        }

        public static bool TryClear(out NativeAudioKernelBridgeStatus status)
        {
            if (!HectonNativeBridge.IsAvailable(HectonNativeLibrary.AudioKernel))
            {
                status = NativeAudioKernelBridgeStatus.PluginUnavailable;
                return false;
            }

            try
            {
                status = (NativeAudioKernelBridgeStatus)ClearSharedRingBuffer();
                return (status & NativeAudioKernelBridgeStatus.Active) == 0 &&
                       (status & NativeAudioKernelBridgeStatus.Busy) == 0;
            }
            catch (Exception exception) when (HectonNativeBridge.IsNativeLoadFailure(exception))
            {
                HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.AudioKernel, exception);
                status = NativeAudioKernelBridgeStatus.PluginUnavailable;
                return false;
            }
        }

        public static bool TryDumpAudioBridgeTelemetry(void* bytes, int byteCount)
        {
            if (bytes == null || byteCount <= 0)
                return false;

            if (!HectonNativeBridge.IsAvailable(HectonNativeLibrary.AudioKernel))
                return false;

            try
            {
                return DumpAudioBridgeTelemetryNative(bytes, byteCount) != 0;
            }
            catch (Exception exception) when (HectonNativeBridge.IsNativeLoadFailure(exception))
            {
                HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.AudioKernel, exception);
                return false;
            }
        }
#else
        public static NativeAudioKernelBridgeStatus GetStatus()
        {
            return NativeAudioKernelBridgeStatus.PluginUnavailable;
        }

        public static bool TryRegister(ref NativeAudioKernelRingBufferDescriptor descriptor)
        {
            return TryRegister(ref descriptor, out _);
        }

        public static bool TryRegister(ref NativeAudioKernelRingBufferDescriptor descriptor, out NativeAudioKernelBridgeStatus status)
        {
            if (!IsDescriptorValid(in descriptor, out status))
                return false;

            status = NativeAudioKernelBridgeStatus.PluginUnavailable;
            return false;
        }

        public static bool TryClear()
        {
            return TryClear(out _);
        }

        public static bool TryClear(out NativeAudioKernelBridgeStatus status)
        {
            status = NativeAudioKernelBridgeStatus.PluginUnavailable;
            return false;
        }

        public static bool TryDumpAudioBridgeTelemetry(void* bytes, int byteCount)
        {
            return false;
        }
#endif

        private static bool IsAligned(IntPtr pointer, int alignmentBytes)
        {
            if (pointer == IntPtr.Zero || alignmentBytes <= 0)
                return false;

            long mask = alignmentBytes - 1L;
            return (pointer.ToInt64() & mask) == 0L;
        }

        private static bool HasValidSharedStatePointerLayout(in NativeAudioKernelRingBufferDescriptor descriptor)
        {
            if (descriptor.SharedState == IntPtr.Zero ||
                descriptor.ReadIndex == IntPtr.Zero ||
                descriptor.WriteIndex == IntPtr.Zero ||
                descriptor.SharedStateLengthInts < NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount)
            {
                return false;
            }

            long sharedStateBase = descriptor.SharedState.ToInt64();
            long sharedStateBytes = (long)descriptor.SharedStateLengthInts * sizeof(int);
            return HasExpectedSharedStateOffset(
                       sharedStateBase,
                       sharedStateBytes,
                       descriptor.ReadIndex,
                       NativeAudioKernelRingBufferDescriptor.ReadIndexSlot) &&
                   HasExpectedSharedStateOffset(
                       sharedStateBase,
                       sharedStateBytes,
                       descriptor.WriteIndex,
                       NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
        }

        private static bool HasExpectedSharedStateOffset(
            long sharedStateBase,
            long sharedStateBytes,
            IntPtr pointer,
            int expectedSlot)
        {
            long pointerAddress = pointer.ToInt64();
            long byteOffset = pointerAddress - sharedStateBase;
            long expectedOffset = (long)expectedSlot * sizeof(int);
            return byteOffset == expectedOffset &&
                   byteOffset >= 0L &&
                   byteOffset <= sharedStateBytes - sizeof(int) &&
                   (byteOffset & (sizeof(int) - 1L)) == 0L;
        }

        private static bool HasValidSharedStateMetadata(in NativeAudioKernelRingBufferDescriptor descriptor)
        {
            if (descriptor.SharedState == IntPtr.Zero ||
                descriptor.SharedStateLengthInts < NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount)
            {
                return false;
            }

            int* sharedStatePtr = (int*)descriptor.SharedState;
            int capacityFrames = Volatile.Read(ref sharedStatePtr[NativeAudioKernelRingBufferDescriptor.CapacityFramesSlot]);
            int capacityMask = Volatile.Read(ref sharedStatePtr[NativeAudioKernelRingBufferDescriptor.CapacityMaskSlot]);
            int guardA = Volatile.Read(ref sharedStatePtr[NativeAudioKernelRingBufferDescriptor.GuardValueSlotA]);
            int guardB = Volatile.Read(ref sharedStatePtr[NativeAudioKernelRingBufferDescriptor.GuardValueSlotB]);
            int sourceChannels = Volatile.Read(ref sharedStatePtr[NativeAudioKernelRingBufferDescriptor.SourceChannelsSlot]);
            return capacityFrames == descriptor.CapacityFrames &&
                   capacityMask == descriptor.CapacityMask &&
                   guardA == NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueA &&
                   guardB == NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueB &&
                   sourceChannels == descriptor.SourceChannels;
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
