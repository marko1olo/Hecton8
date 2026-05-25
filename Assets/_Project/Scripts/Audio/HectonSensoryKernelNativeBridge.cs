using System;
using System.Runtime.InteropServices;
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
        public const int ReadIndexSlot = 0;
        public const int WriteIndexSlot = 1;
        public const int CapacityFramesSlot = 2;
        public const int CapacityMaskSlot = 3;
        public const int GuardValueSlotA = 4;
        public const int GuardValueSlotB = 5;
        public const int SharedStateSlotCount = 6;
        public const int SharedStateGuardValueA = unchecked((int)0x48454354);
        public const int SharedStateGuardValueB = unchecked((int)0x4F4E2D38);

        [FieldOffset(0)]
        public uint DescriptorMagic;
#pragma warning disable 0169
        [FieldOffset(4)]
        private uint _descriptorPad0;
#pragma warning restore 0169
        [FieldOffset(8)]
        public IntPtr Frames;
        [FieldOffset(16)]
        public IntPtr SharedState;
        [FieldOffset(24)]
        public IntPtr ReadIndex;
        [FieldOffset(32)]
        public IntPtr WriteIndex;
        [FieldOffset(40)]
        public int CapacityFrames;
        [FieldOffset(44)]
        public int CapacityMask;
        [FieldOffset(48)]
        public int SharedStateLengthInts;
#pragma warning disable 0169
        [FieldOffset(52)]
        private uint _reserved0;
#pragma warning restore 0169
    }

    internal static class HectonSensoryKernelNativeBridge
    {
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
                descriptor.CapacityMask != descriptor.CapacityFrames - 1 ||
                !IsPowerOfTwo(descriptor.CapacityFrames))
            {
                status |= NativeAudioKernelBridgeStatus.CapacityInvalid;
            }

            if (descriptor.SharedStateLengthInts < NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount)
                status |= NativeAudioKernelBridgeStatus.SharedStateInvalid;

            if (status == NativeAudioKernelBridgeStatus.None)
                status = NativeAudioKernelBridgeStatus.Active;

            return status == NativeAudioKernelBridgeStatus.Active;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string PluginName = "HectonAudioKernel";

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_RegisterSharedRingBuffer")]
        private static extern void RegisterSharedRingBuffer(ref NativeAudioKernelRingBufferDescriptor descriptor);

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_ClearSharedRingBuffer")]
        private static extern void ClearSharedRingBuffer();

        [DllImport(PluginName, EntryPoint = "HectonSensoryKernel_GetSharedRingBufferStatus")]
        private static extern int GetSharedRingBufferStatusNative();

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
                RegisterSharedRingBuffer(ref descriptor);
                status = GetStatus();
                return (status & NativeAudioKernelBridgeStatus.Active) != 0;
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
                ClearSharedRingBuffer();
                status = GetStatus();
                return (status & NativeAudioKernelBridgeStatus.Active) == 0;
            }
            catch (Exception exception) when (HectonNativeBridge.IsNativeLoadFailure(exception))
            {
                HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.AudioKernel, exception);
                status = NativeAudioKernelBridgeStatus.PluginUnavailable;
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
#endif

        private static bool IsAligned(IntPtr pointer, int alignmentBytes)
        {
            if (pointer == IntPtr.Zero || alignmentBytes <= 0)
                return false;

            long mask = alignmentBytes - 1L;
            return (pointer.ToInt64() & mask) == 0L;
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
