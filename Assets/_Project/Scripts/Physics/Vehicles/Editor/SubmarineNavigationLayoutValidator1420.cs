#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Hecton8.Physics.Vehicles.Editor
{
    [InitializeOnLoad]
    internal static class SubmarineNavigationLayoutValidator1420
    {
        static SubmarineNavigationLayoutValidator1420()
        {
            ValidateAll();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ValidateAll();
        }

        internal static void ValidateAll()
        {
            Require(SubmarineBallastLayout.Validate(), "SubmarineBallastLayout.Validate failed.");
            ValidateSize<BallastTankDTO>(SubmarineBallastConstants.TankBytes);
            ValidateSize<BallastTankCommandDTO>(SubmarineBallastConstants.CommandBytes);
            ValidateSize<SubmarineBallastFluidSampleDTO>(SubmarineBallastConstants.FluidSampleBytes);
            ValidateSize<SubmarineBallastForcePacketDTO>(SubmarineBallastConstants.ForcePacketBytes);
            ValidateSize<SubmarineBallastTelemetryEntry>(SubmarineBallastConstants.TelemetryBytes);
            ValidateSize<SubmarineBallastTuningDTO>(SubmarineBallastConstants.TuningBytes);
            ValidateSize<SubmarineBallastProfileDTO>(SubmarineBallastConstants.ProfileBytes);

            ValidateOffset<BallastTankDTO>(nameof(BallastTankDTO.TankVolumeLiters), 0);
            ValidateOffset<BallastTankDTO>(nameof(BallastTankDTO.CurrentWaterLiters), 4);
            ValidateOffset<BallastTankDTO>(nameof(BallastTankDTO.CompressedAirPressureATM), 8);
            ValidateOffset<BallastTankDTO>(nameof(BallastTankDTO.InputStateFlags), 12);
            ValidateOffset<SubmarineBallastFluidSampleDTO>(nameof(SubmarineBallastFluidSampleDTO.ActiveSampleBudget), 148);
            ValidateOffset<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.HullAup), 0);
            ValidateOffset<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.NetForce), 24);
            ValidateOffset<SubmarineBallastForcePacketDTO>(nameof(SubmarineBallastForcePacketDTO.StateHash), 116);
            ValidateOffset<SubmarineBallastTelemetryEntry>(nameof(SubmarineBallastTelemetryEntry.ComputeMicros), 44);
            ValidateOffset<SubmarineBallastTuningDTO>(nameof(SubmarineBallastTuningDTO.SourceHash), 32);
            ValidatePidTelemetryLayout();
        }

        private static void ValidateSize<T>(int expected)
            where T : struct
        {
            int actual = UnsafeUtility.SizeOf<T>();
            Require(actual == expected, typeof(T).FullName + " size " + actual + " != " + expected);
            Require((actual & 7) == 0, typeof(T).FullName + " size is not 8-byte aligned.");
        }

        private static void ValidateOffset<T>(string fieldName, int expected)
            where T : struct
        {
            int actual = Marshal.OffsetOf<T>(fieldName).ToInt32();
            Require(actual == expected, typeof(T).FullName + "." + fieldName + " offset " + actual + " != " + expected);
        }

        private static void ValidatePidTelemetryLayout()
        {
            Type telemetry = typeof(SubmarineAutoLevelBallastController).GetNestedType(
                "SubmarinePidTelemetryEntry",
                BindingFlags.NonPublic);
            Require(telemetry != null, "Missing SubmarinePidTelemetryEntry.");
            Require(telemetry.IsExplicitLayout, "SubmarinePidTelemetryEntry must use explicit layout.");
            Require(Marshal.SizeOf(telemetry) == 128, "SubmarinePidTelemetryEntry size must be 128.");
            ValidateOffset(telemetry, "Frame", 0);
            ValidateOffset(telemetry, "Flags", 8);
            ValidateOffset(telemetry, "StateHash", 12);
            ValidateOffset(telemetry, "CriticalFloodActive", 116);
            ValidateOffset(telemetry, "LastVaultFaultCode", 117);
            ValidateOffset(telemetry, "LastVaultFaultBufferId", 120);
            ValidateOffset(telemetry, "LastVaultFaultFrame", 124);
        }

        private static void ValidateOffset(Type type, string fieldName, int expected)
        {
            int actual = Marshal.OffsetOf(type, fieldName).ToInt32();
            Require(actual == expected, type.FullName + "." + fieldName + " offset " + actual + " != " + expected);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
#endif
