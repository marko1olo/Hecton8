using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay.Atlas6Liability
{
    public enum Atlas6LiabilityEventCode : ushort
    {
        None = 0,
        WorkerTagRecovered = 1,
        CorporateCreditDeducted = 2,
        ActuarialThreatRaised = 3,
        TetherDeniedPreviouslySevered = 4,
        TetherDeniedInsufficientYield = 5,
        TetherSeveredSatoRen = 6,
        TetherRequested = 7,
        BoardingDeniedInvalidCarrierState = 8,
        HaldaneLockoutRaised = 9,
        CarrierArrived = 10,
        DecontaminationProcessed = 11,
        ArendtBulkheadLockdown = 12,
        ArendtManualOverride = 13,
        DisasterEvidenceCollected = 14,
        DisasterEvidenceDiscarded = 15,
        ThreatLevelChanged = 16,
        DynamicHaldaneMonitorRejected = 17,
        RegistryRegistrationChanged = 18,
        InvalidXenonOmegaYieldReported = 19,
        XenonOmegaYieldReported = 20,
        InvalidBiomatterExposureReported = 21,
        InvalidDecontaminationReported = 22,
        InvalidGhostPDADataReported = 23,
        InvalidDirectiveWeightingInput = 24
    }

    public enum Atlas6LiabilityEventSeverity : ushort
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    [Flags]
    public enum Atlas6LiabilityFaultFlags : uint
    {
        None = 0u,
        NonFiniteInput = 1u << 0,
        CarrierStateRejected = 1u << 1,
        RepeatedFaultSuppressed = 1u << 2,
        EventConsumerNotified = 1u << 3,
        InvalidRangeInput = 1u << 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct Atlas6LiabilityTelemetryRecord
    {
        [FieldOffset(0)] public uint Sequence;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint ContextHash;
        [FieldOffset(12)] public uint SubjectHash;
        [FieldOffset(16)] public float Value0;
        [FieldOffset(20)] public float Value1;
        [FieldOffset(24)] public ushort EventCode;
        [FieldOffset(26)] public ushort Severity;
        [FieldOffset(28)] public ushort CarrierState;
        [FieldOffset(30)] public ushort ThreatLevel;
        [FieldOffset(32)] public uint FaultFlags;
        [FieldOffset(36)] private uint _pad0;
    }

    public sealed class Atlas6LiabilityTelemetry
    {
        public const int Capacity = 300;

        public static readonly uint ActuarialContextHash = ComputeStableHash("Atlas6Liability.Actuarial");
        public static readonly uint ExtractionContextHash = ComputeStableHash("Atlas6Liability.Extraction");
        public static readonly uint DirectiveContextHash = ComputeStableHash("Atlas6Liability.Directive");
        public static readonly uint ManagerContextHash = ComputeStableHash("Atlas6Liability.Manager");

        private readonly Atlas6LiabilityTelemetryRecord[] _records;
        private int _nextIndex;
        private int _count;
        private uint _sequence;

        public Atlas6LiabilityTelemetry()
        {
            _records = new Atlas6LiabilityTelemetryRecord[Capacity]; // COLD ALLOC: Atlas6LiabilityTelemetryRecord[300] - owner-local black-box ring - owner: Atlas6LiabilityTelemetry
        }

        public int Count => _count;

        public uint LatestSequence => _sequence;

        public void Clear()
        {
            Array.Clear(_records, 0, _records.Length);
            _nextIndex = 0;
            _count = 0;
            _sequence = 0u;
        }

        public void Record(
            Atlas6LiabilityEventCode eventCode,
            Atlas6LiabilityEventSeverity severity,
            uint contextHash,
            uint subjectHash = 0u,
            float value0 = 0f,
            float value1 = 0f,
            ExtractionCarrierState carrierState = ExtractionCarrierState.Offline,
            Atlas6ThreatLevel threatLevel = Atlas6ThreatLevel.Nominal,
            Atlas6LiabilityFaultFlags faultFlags = Atlas6LiabilityFaultFlags.None)
        {
            Atlas6LiabilityFaultFlags safeFaultFlags = faultFlags;
            if (!math.isfinite(value0))
            {
                value0 = 0f;
                safeFaultFlags |= Atlas6LiabilityFaultFlags.NonFiniteInput;
            }

            if (!math.isfinite(value1))
            {
                value1 = 0f;
                safeFaultFlags |= Atlas6LiabilityFaultFlags.NonFiniteInput;
            }

            _records[_nextIndex] = new Atlas6LiabilityTelemetryRecord
            {
                Sequence = ++_sequence,
                Frame = unchecked((uint)Time.frameCount),
                ContextHash = contextHash,
                SubjectHash = subjectHash,
                Value0 = value0,
                Value1 = value1,
                EventCode = (ushort)eventCode,
                Severity = (ushort)severity,
                CarrierState = (ushort)carrierState,
                ThreatLevel = (ushort)threatLevel,
                FaultFlags = (uint)safeFaultFlags
            };

            _nextIndex++;
            if (_nextIndex >= _records.Length)
                _nextIndex = 0;

            if (_count < _records.Length)
                _count++;
        }

        public bool TryCopyLatest(out Atlas6LiabilityTelemetryRecord record)
        {
            return TryCopyNewest(0, out record);
        }

        public bool TryCopyNewest(int newestOffset, out Atlas6LiabilityTelemetryRecord record)
        {
            if ((uint)newestOffset >= (uint)_count)
            {
                record = default;
                return false;
            }

            int index = _nextIndex - 1 - newestOffset;
            if (index < 0)
                index += _records.Length;

            record = _records[index];
            return true;
        }

        public static uint ComputeStableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                unchecked
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }
}
