using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Vault-owned compass state. Runtime writers keep this as the single mutable compass authority.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 176)]
    public struct CompassStateDTO
    {
        [FieldOffset(0)] public double3 ActualAUP;
        [FieldOffset(24)] public double3 RawEstimatedAUP;
        [FieldOffset(48)] public double3 EstimatedAUP;
        [FieldOffset(72)] public double3 PreviousActualAUP;
        [FieldOffset(96)] public float3 Velocity;
        [FieldOffset(108)] public float ActualHeadingDegrees;
        [FieldOffset(112)] public float CurrentHeadingDegrees;
        [FieldOffset(116)] public float DriftDegrees;
        [FieldOffset(120)] public float AnomalyInterference01;
        [FieldOffset(124)] public float Power01;
        [FieldOffset(128)] public float Glitch01;
        [FieldOffset(132)] public float RecalibrationHold01;
        [FieldOffset(136)] public float MaxGyroDriftDegrees;
        [FieldOffset(140)] public float DeltaSeconds;
        [FieldOffset(144)] public float SystemStress01;
        [FieldOffset(148)] public float NoiseClockSeconds;
        [FieldOffset(152)] public uint Frame;
        [FieldOffset(156)] public uint Flags;
        [FieldOffset(160)] public uint LastAupShiftFrameId;
        [FieldOffset(164)] public int BlackBoxCursor;
        [FieldOffset(168)] public int CalibrationCount;
        [FieldOffset(172)] public uint Reserved0;
    }

    /// <summary>
    /// SOA slots for the vault-owned compass float output buffer.
    /// </summary>
    public enum CompassOutputSlot : int
    {
        CurrentHeadingDegrees = 0,
        ActualHeadingDegrees = 1,
        DriftDegrees = 2,
        AnomalyInterference01 = 3,
        Power01 = 4,
        Glitch01 = 5,
        CardinalIndex = 6,
        MaxGyroDriftDegrees = 7,
        Count = 8
    }

    /// <summary>
    /// Registry-facing dead-reckoning state exposed to cockpit and UI consumers without concrete navigation runtime coupling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 120)]
    public struct InertialNavigationSnapshot
    {
        /// <summary>Actual submarine AUP resolved from the authoritative motion read model.</summary>
        [FieldOffset(0)] public double3 ActualAUP;

        /// <summary>Integrated estimate before transient gyro error presentation is applied.</summary>
        [FieldOffset(24)] public double3 RawEstimatedAUP;

        /// <summary>Presented estimate after gyro drift rotation has falsified the translation.</summary>
        [FieldOffset(48)] public double3 EstimatedAUP;

        /// <summary>Submarine velocity used by the last integration step.</summary>
        [FieldOffset(72)] public float3 SubmarineVelocity;

        /// <summary>Accumulated gyro drift in degrees.</summary>
        [FieldOffset(84)] public float GyroDriftError;

        /// <summary>False cockpit bearing produced from the drifted estimated AUP.</summary>
        [FieldOffset(88)] public float FalseBearingDegrees;

        /// <summary>Hold-progress scalar for the physical recalibration control.</summary>
        [FieldOffset(92)] public float RecalibrationHold01;

        /// <summary>Navigation-driven UI glitch scalar consumed by visor post processing.</summary>
        [FieldOffset(96)] public float DriftGlitch01;

        /// <summary>Total completed recalibrations since runtime boot or loaded save state.</summary>
        [FieldOffset(100)] public int CalibrationCount;

        /// <summary>State flags for telemetry and UI diagnostics.</summary>
        [FieldOffset(104)] public uint Flags;

        /// <summary>Latest consumed AUP shift frame id.</summary>
        [FieldOffset(108)] public uint LastAupShiftFrameId;

        /// <summary>Latest consumed impact frame.</summary>
        [FieldOffset(112)] public uint LastImpactFrame;

        /// <summary>Latest consumed brownout frame.</summary>
        [FieldOffset(116)] public uint LastBrownoutFrame;
    }

    /// <summary>
    /// Dead-reckoning navigation service contract. Implementations are registry-owned and must avoid singleton access.
    /// </summary>
    public interface IInertialNavigationService
    {
        /// <summary>Latest completed inertial navigation snapshot.</summary>
        InertialNavigationSnapshot Snapshot { get; }

        /// <summary>Current presented estimated AUP.</summary>
        double3 EstimatedAUP { get; }

        /// <summary>Current gyro drift error in degrees.</summary>
        float GyroDriftError { get; }

        /// <summary>Returns the latest completed snapshot when the service has been initialized.</summary>
        bool TryGetSnapshot(out InertialNavigationSnapshot snapshot);

        /// <summary>Requests immediate recalibration during the next integration pass.</summary>
        void RequestRecalibration();

        /// <summary>Accumulates physical hold time toward the 3 second recalibration threshold.</summary>
        bool TryAccumulateRecalibrationHold(float deltaTime, out float progress01);

        /// <summary>Cancels any in-progress physical recalibration hold.</summary>
        void CancelRecalibrationHold();
    }
}
