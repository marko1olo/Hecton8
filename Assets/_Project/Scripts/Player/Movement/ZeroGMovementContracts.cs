using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

#if UNITY_EDITOR
using System;
#endif

namespace Hecton8.Player.Movement
{
    public static class ZeroGMovementStateFlags
    {
        public const uint Active = 1u << 0;
        public const uint ExternalInput = 1u << 1;
        public const uint ThrusterActive = 1u << 2;
        public const uint SurfaceContact = 1u << 3;
        public const uint Depenetrated = 1u << 4;
        public const uint Reflected = 1u << 5;
        public const uint PushAndGlide = 1u << 6;
        public const uint HorizonLocked = 1u << 7;
        public const uint PropellantDry = 1u << 8;
        public const uint NaNDetected = 1u << 9;
        public const uint BudgetExceeded = 1u << 10;
        public const uint SignalDrop = 1u << 11;
        public const uint EmergencyMockData = 1u << 12;
        public const uint VaultAccessDenied = 1u << 13;
    }

    public static class ZeroGInputActions
    {
        public const uint Thruster = 1u << 0;
        public const uint PushAndGlide = 1u << 1;
        public const uint HorizonLock = 1u << 2;
        public const uint BrakeAssist = 1u << 3;
        public const uint ExternalAuthority = 1u << 31;
        public const uint SimulationMask = Thruster | PushAndGlide | HorizonLock | BrakeAssist;
        public const uint ValidMask = SimulationMask | ExternalAuthority;
    }

    public static class ZeroGMovementFaultCodes
    {
        public const uint None = 0u;
        public const uint NonFinite = 1u;
        public const uint VaultAccessDenied = 2u;
    }

    public static class ZeroGSurfaceHitFlags
    {
        public const uint Valid = 1u << 0;
        public const uint AnalyticOrbitWall = 1u << 1;
        public const uint Pushable = 1u << 2;
        public const uint LowTierProbe = 1u << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ZeroGMovementStateDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public long SimulationTick;
        [FieldOffset(32)] public quaternion Orientation;
        [FieldOffset(48)] public float3 LinearVelocity;
        [FieldOffset(60)] public float3 AngularMomentum;
        [FieldOffset(72)] public float SuitPropellant01;
        [FieldOffset(76)] public float RadiusMeters;
        [FieldOffset(80)] public float Restitution;
        [FieldOffset(84)] public float HorizonLockWeight;
        [FieldOffset(88)] public float LastCollisionImpulse;
        [FieldOffset(92)] public float LastDepenetration;
        [FieldOffset(96)] public uint Flags;
        [FieldOffset(100)] public uint Frame;
        [FieldOffset(104)] public uint StateHash;
        [FieldOffset(108)] public uint FaultCode;
        [FieldOffset(112)] public uint LastActionMask;
        [FieldOffset(116)] public uint Reserved0;
        [FieldOffset(120)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ZeroGInputStateDTO
    {
        [FieldOffset(0)] public long SimulationTick;
        [FieldOffset(8)] public float3 LocalThrustAxis;
        [FieldOffset(20)] public float3 LocalAngularAxis;
        [FieldOffset(32)] public quaternion ViewOrientation;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public uint ActionMask;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ZeroGTuningDTO
    {
        [FieldOffset(0)] public float ThrustAcceleration;
        [FieldOffset(4)] public float AngularAcceleration;
        [FieldOffset(8)] public float MaxSpeedMetersPerSecond;
        [FieldOffset(12)] public float MaxAngularRadiansPerSecond;
        [FieldOffset(16)] public float RadiusMeters;
        [FieldOffset(20)] public float Restitution;
        [FieldOffset(24)] public float PushImpulseVelocityChange;
        [FieldOffset(28)] public float DepenetrationSlopMeters;
        [FieldOffset(32)] public float HorizonLockStrength;
        [FieldOffset(36)] public float PropellantDrainPerSecond;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float SurfaceProbeRadiusMeters;
        [FieldOffset(48)] public float3 OrbitBoundsHalfExtents;
        [FieldOffset(60)] public float3 HorizonUp;
        [FieldOffset(72)] public uint MaxSubsteps;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public uint StateHash;
        [FieldOffset(84)] public float CameraTraumaScale;
        [FieldOffset(88)] public float HapticScale;
        [FieldOffset(92)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ZeroGSurfaceHitDTO
    {
        [FieldOffset(0)] public float3 PointLocal;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float DistanceMeters;
        [FieldOffset(28)] public float PenetrationMeters;
        [FieldOffset(32)] public float CollisionImpulse;
        [FieldOffset(36)] public float QualityProbeWeight;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public uint SurfaceHash;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ZeroGSolverOutputDTO
    {
        public const uint FlagCollision = 1u << 0;
        public const uint FlagCameraTrauma = 1u << 1;
        public const uint FlagHaptic = 1u << 2;
        public const uint FlagFault = 1u << 3;

        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 LinearVelocity;
        [FieldOffset(24)] public float3 CollisionNormal;
        [FieldOffset(36)] public float CollisionImpulse;
        [FieldOffset(40)] public float CameraTrauma01;
        [FieldOffset(44)] public float Propellant01;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint FaultCode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ZeroGTelemetryEntry
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 LinearVelocity;
        [FieldOffset(24)] public float3 AngularMomentum;
        [FieldOffset(36)] public float CollisionImpulse;
        [FieldOffset(40)] public float Propellant01;
        [FieldOffset(44)] public float SolverComputeTimeMs;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint FaultCode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ZeroGTestResultDTO
    {
        [FieldOffset(0)] public float MaxPositionError;
        [FieldOffset(4)] public float MaxVelocityError;
        [FieldOffset(8)] public float MaxOrientationError;
        [FieldOffset(12)] public uint Iterations;
        [FieldOffset(16)] public uint FaultMask;
        [FieldOffset(20)] public uint StateHash;
        [FieldOffset(24)] private ulong _pad0;
    }

    public static class ZeroGMovementLayoutVerifier
    {
        public static bool ValidateRuntimeLayouts()
        {
            bool sizeOk =
                UnsafeUtility.SizeOf<ZeroGMovementStateDTO>() == 128 &&
                UnsafeUtility.SizeOf<ZeroGInputStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<ZeroGTuningDTO>() == 96 &&
                UnsafeUtility.SizeOf<ZeroGSurfaceHitDTO>() == 64 &&
                UnsafeUtility.SizeOf<ZeroGSolverOutputDTO>() == 64 &&
                UnsafeUtility.SizeOf<ZeroGTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ZeroGTestResultDTO>() == 32;

#if UNITY_EDITOR
            return sizeOk &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.AUP_Position)) == 0 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.SimulationTick)) == 24 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.Orientation)) == 32 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.LinearVelocity)) == 48 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.AngularMomentum)) == 60 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.Flags)) == 96 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.FaultCode)) == 108 &&
                   GetOffset<ZeroGMovementStateDTO>(nameof(ZeroGMovementStateDTO.LastActionMask)) == 112 &&
                   GetOffset<ZeroGInputStateDTO>(nameof(ZeroGInputStateDTO.LocalThrustAxis)) == 8 &&
                   GetOffset<ZeroGInputStateDTO>(nameof(ZeroGInputStateDTO.LocalAngularAxis)) == 20 &&
                   GetOffset<ZeroGInputStateDTO>(nameof(ZeroGInputStateDTO.ViewOrientation)) == 32 &&
                   GetOffset<ZeroGInputStateDTO>(nameof(ZeroGInputStateDTO.Flags)) == 60 &&
                   GetOffset<ZeroGTuningDTO>(nameof(ZeroGTuningDTO.OrbitBoundsHalfExtents)) == 48 &&
                   GetOffset<ZeroGTuningDTO>(nameof(ZeroGTuningDTO.MaxSubsteps)) == 72 &&
                   GetOffset<ZeroGTuningDTO>(nameof(ZeroGTuningDTO.StateHash)) == 80 &&
                   GetOffset<ZeroGSurfaceHitDTO>(nameof(ZeroGSurfaceHitDTO.Flags)) == 40 &&
                   GetOffset<ZeroGSurfaceHitDTO>(nameof(ZeroGSurfaceHitDTO.SurfaceHash)) == 48 &&
                   GetOffset<ZeroGSolverOutputDTO>(nameof(ZeroGSolverOutputDTO.Flags)) == 48 &&
                   GetOffset<ZeroGSolverOutputDTO>(nameof(ZeroGSolverOutputDTO.FaultCode)) == 60 &&
                   GetOffset<ZeroGTestResultDTO>(nameof(ZeroGTestResultDTO.StateHash)) == 20 &&
                   GetOffset<ZeroGTelemetryEntry>(nameof(ZeroGTelemetryEntry.FaultCode)) == 60;
#else
            return sizeOk;
#endif
        }

#if UNITY_EDITOR
        private static int GetOffset<T>(string fieldName)
            where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif
    }
}
