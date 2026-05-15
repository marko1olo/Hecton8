using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Registry-facing dead-reckoning state exposed to cockpit and UI consumers without concrete navigation runtime coupling.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InertialNavigationSnapshot
    {
        /// <summary>Actual submarine AUP resolved from the authoritative motion read model.</summary>
        public double3 ActualAUP;

        /// <summary>Integrated estimate before transient gyro error presentation is applied.</summary>
        public double3 RawEstimatedAUP;

        /// <summary>Presented estimate after gyro drift rotation has falsified the translation.</summary>
        public double3 EstimatedAUP;

        /// <summary>Submarine velocity used by the last integration step.</summary>
        public float3 SubmarineVelocity;

        /// <summary>Accumulated gyro drift in degrees.</summary>
        public float GyroDriftError;

        /// <summary>False cockpit bearing produced from the drifted estimated AUP.</summary>
        public float FalseBearingDegrees;

        /// <summary>Hold-progress scalar for the physical recalibration control.</summary>
        public float RecalibrationHold01;

        /// <summary>Navigation-driven UI glitch scalar consumed by visor post processing.</summary>
        public float DriftGlitch01;

        /// <summary>Total completed recalibrations since runtime boot or loaded save state.</summary>
        public int CalibrationCount;

        /// <summary>State flags for telemetry and UI diagnostics.</summary>
        public uint Flags;

        /// <summary>Latest consumed AUP shift frame id.</summary>
        public uint LastAupShiftFrameId;

        /// <summary>Latest consumed impact frame.</summary>
        public uint LastImpactFrame;

        /// <summary>Latest consumed brownout frame.</summary>
        public uint LastBrownoutFrame;
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
