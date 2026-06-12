using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Input
{
    public static class InputBindingContractLayout
    {
        public const uint Version = 1u;
        public const int InputActionStateStrideBytes = 64;
        public const int AccessibilityConfigStrideBytes = 16;
        public const int InputBindingTelemetryStrideBytes = 64;
        public const int ControlRemapIoResultStrideBytes = 88;
        public const int InputBindingTelemetryCapacity = 300;

        public const BufferID InputBindingTelemetryRingBufferId = BufferID.ShinobuLogisticsComponentIds;
        public const BufferID InputBindingTelemetryCursorBufferId = BufferID.ShinobuLogisticsPressureFront;
    }

    [Flags]
    public enum InputActionStateFlags : byte
    {
        None = 0,
        HasOverridePath = 1 << 0,
        CompositePart = 1 << 1
    }

    public enum AccessibilityColorFilterMode : uint
    {
        Off = 0u,
        Protanopia = 1u,
        Deuteranopia = 2u,
        Tritanopia = 3u
    }

    [Flags]
    public enum AccessibilityConfigFlags : uint
    {
        None = 0u,
        Enabled = 1u << 0,
        ContinuousQualityWeight = 1u << 1
    }

    [StructLayout(LayoutKind.Explicit, Size = InputBindingContractLayout.InputActionStateStrideBytes)]
    public struct InputActionStateDTO
    {
        [FieldOffset(0)] public ulong BindingGuidHash64;
        [FieldOffset(8)] public ulong EffectivePathHash64;
        [FieldOffset(16)] public ulong OverridePathHash64;
        [FieldOffset(24)] public ulong ActionIdentityHash64;
        [FieldOffset(32)] public uint ActionNameHash;
        [FieldOffset(36)] public uint ActionMapHash;
        [FieldOffset(40)] public uint BindingGroupHash;
        [FieldOffset(44)] public uint ControlPathHash;
        [FieldOffset(48)] public int BindingIndex;
        [FieldOffset(52)] public ushort PathByteOffset;
        [FieldOffset(54)] public ushort PathByteLength;
        [FieldOffset(56)] public ushort CompositeDepth;
        [FieldOffset(58)] public ushort DeviceMask;
        [FieldOffset(60)] public byte DisplayStyle;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private byte _pad0;
        [FieldOffset(63)] private byte _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = InputBindingContractLayout.AccessibilityConfigStrideBytes)]
    public struct AccessibilityConfigDTO
    {
        [FieldOffset(0)] public uint ColorMode;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float FilterStrength01;
        [FieldOffset(12)] public float GlobalQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = InputBindingContractLayout.InputBindingTelemetryStrideBytes)]
    public struct InputBindingTelemetryEntry
    {
        [FieldOffset(0)] public double RealtimeSeconds;
        [FieldOffset(8)] public ulong PayloadHash64;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Operation;
        [FieldOffset(24)] public uint Result;
        [FieldOffset(28)] public uint Bytes;
        [FieldOffset(32)] public uint DurationMicroseconds;
        [FieldOffset(36)] public uint FaultFlags;
        [FieldOffset(40)] public int BindingIndex;
        [FieldOffset(44)] public ushort RecordCount;
        [FieldOffset(46)] public ushort PathBytes;
        [FieldOffset(48)] public byte IoPhase;
        [FieldOffset(49)] private byte _pad0;
        [FieldOffset(50)] private byte _pad1;
        [FieldOffset(51)] private byte _pad2;
        [FieldOffset(52)] private byte _pad3;
        [FieldOffset(53)] private byte _pad4;
        [FieldOffset(54)] private byte _pad5;
        [FieldOffset(55)] private byte _pad6;
        [FieldOffset(56)] private byte _pad7;
        [FieldOffset(57)] private byte _pad8;
        [FieldOffset(58)] private byte _pad9;
        [FieldOffset(59)] private byte _pad10;
        [FieldOffset(60)] private byte _pad11;
        [FieldOffset(61)] private byte _pad12;
        [FieldOffset(62)] private byte _pad13;
        [FieldOffset(63)] private byte _pad14;
    }

    public static class InputBindingTelemetryOperation
    {
        public const uint None = 0u;
        public const uint Save = 1u;
        public const uint Load = 2u;
        public const uint Delete = 3u;
        public const uint Parse = 4u;
        public const uint Apply = 5u;
    }

    public static class InputBindingTelemetryResult
    {
        public const uint None = 0u;
        public const uint Success = 1u;
        public const uint NoOverrides = 2u;
        public const uint FileMissing = 3u;
        public const uint InvalidJson = 4u;
        public const uint IoFailure = 5u;
        public const uint UnsupportedPath = 6u;
        public const uint ConcurrentOperation = 7u;
    }

    public static class InputBindingFaultFlags
    {
        public const uint None = 0u;
        public const uint BufferOverflow = 1u << 0;
        public const uint InvalidUtf8 = 1u << 1;
        public const uint InvalidSchema = 1u << 2;
        public const uint IoException = 1u << 3;
        public const uint PathTooLong = 1u << 4;
        public const uint ActionMissing = 1u << 5;
        public const uint BindingMissing = 1u << 6;
        public const uint UnsupportedPath = 1u << 7;
        public const uint ConcurrentOperation = 1u << 8;
    }

    [StructLayout(LayoutKind.Explicit, Size = InputBindingContractLayout.ControlRemapIoResultStrideBytes)]
    public struct ControlRemapIoResult
    {
        [FieldOffset(0)] public InputBindingTelemetryEntry Telemetry;
        [FieldOffset(64)] public uint ResultCode;
        [FieldOffset(68)] public uint FaultFlags;
        [FieldOffset(72)] public int RecordCount;
        [FieldOffset(76)] public int ByteCount;
        [FieldOffset(80)] public int PathBytes;
        [FieldOffset(84)] private byte _pad0;
        [FieldOffset(85)] private byte _pad1;
        [FieldOffset(86)] private byte _pad2;
        [FieldOffset(87)] private byte _pad3;
    }

    public static class InputBindingLayoutGuard
    {
        public static uint Validate()
        {
            uint mask = 0u;
            mask |= UnsafeUtility.SizeOf<InputActionStateDTO>() == InputBindingContractLayout.InputActionStateStrideBytes ? 0u : 1u << 0;
            mask |= UnsafeUtility.SizeOf<AccessibilityConfigDTO>() == InputBindingContractLayout.AccessibilityConfigStrideBytes ? 0u : 1u << 1;
            mask |= UnsafeUtility.SizeOf<InputBindingTelemetryEntry>() == InputBindingContractLayout.InputBindingTelemetryStrideBytes ? 0u : 1u << 2;
            mask |= UnsafeUtility.SizeOf<ControlRemapIoResult>() == InputBindingContractLayout.ControlRemapIoResultStrideBytes ? 0u : 1u << 3;
            mask |= ValidateInputActionStateOffsets() ? 0u : 1u << 4;
            mask |= ValidateAccessibilityConfigOffsets() ? 0u : 1u << 5;
            mask |= ValidateInputBindingTelemetryOffsets() ? 0u : 1u << 6;
            mask |= ValidateControlRemapIoResultOffsets() ? 0u : 1u << 7;
            return mask;
        }

        private static bool ValidateInputActionStateOffsets()
        {
            return OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.BindingGuidHash64)) == 0 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.EffectivePathHash64)) == 8 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.OverridePathHash64)) == 16 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.ActionIdentityHash64)) == 24 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.ActionNameHash)) == 32 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.ActionMapHash)) == 36 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.BindingGroupHash)) == 40 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.ControlPathHash)) == 44 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.BindingIndex)) == 48 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.PathByteOffset)) == 52 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.PathByteLength)) == 54 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.CompositeDepth)) == 56 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.DeviceMask)) == 58 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.DisplayStyle)) == 60 &&
                   OffsetOf<InputActionStateDTO>(nameof(InputActionStateDTO.Flags)) == 61;
        }

        private static bool ValidateAccessibilityConfigOffsets()
        {
            return OffsetOf<AccessibilityConfigDTO>(nameof(AccessibilityConfigDTO.ColorMode)) == 0 &&
                   OffsetOf<AccessibilityConfigDTO>(nameof(AccessibilityConfigDTO.Flags)) == 4 &&
                   OffsetOf<AccessibilityConfigDTO>(nameof(AccessibilityConfigDTO.FilterStrength01)) == 8 &&
                   OffsetOf<AccessibilityConfigDTO>(nameof(AccessibilityConfigDTO.GlobalQualityWeight)) == 12;
        }

        private static bool ValidateInputBindingTelemetryOffsets()
        {
            return OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.RealtimeSeconds)) == 0 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.PayloadHash64)) == 8 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.Frame)) == 16 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.Operation)) == 20 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.Result)) == 24 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.Bytes)) == 28 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.DurationMicroseconds)) == 32 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.FaultFlags)) == 36 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.BindingIndex)) == 40 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.RecordCount)) == 44 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.PathBytes)) == 46 &&
                   OffsetOf<InputBindingTelemetryEntry>(nameof(InputBindingTelemetryEntry.IoPhase)) == 48;
        }

        private static bool ValidateControlRemapIoResultOffsets()
        {
            return OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.Telemetry)) == 0 &&
                   OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.ResultCode)) == 64 &&
                   OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.FaultFlags)) == 68 &&
                   OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.RecordCount)) == 72 &&
                   OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.ByteCount)) == 76 &&
                   OffsetOf<ControlRemapIoResult>(nameof(ControlRemapIoResult.PathBytes)) == 80;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
