using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct PhysicsCullingDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public int InstanceId;
        [FieldOffset(28)] public float ActivationRadiusSq;
        [FieldOffset(32)] public uint CullingFlags;
        [FieldOffset(36)] public byte IsAsleep;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FrozenVelocityDTO
    {
        [FieldOffset(0)] public float3 LinearVelocity;
        [FieldOffset(12)] public float3 AngularVelocity;
        [FieldOffset(24)] public byte HasVelocity;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private byte _pad1;
        [FieldOffset(27)] private byte _pad2;
        [FieldOffset(28)] private byte _pad3;
        [FieldOffset(29)] private byte _pad4;
        [FieldOffset(30)] private byte _pad5;
        [FieldOffset(31)] private byte _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PhysicsCullingTargetWakeRequestSignal
    {
        [FieldOffset(0)] public float3 ImpulseVector;
        [FieldOffset(12)] public uint TargetInstanceId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public partial struct MockSeismicShockwaveSignal
    {
        [FieldOffset(0)] public double3 EpicenterAup;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public uint Seed;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte Fire;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
        [FieldOffset(40)] private byte _pad3;
        [FieldOffset(41)] private byte _pad4;
        [FieldOffset(42)] private byte _pad5;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysicsCullingFrameTelemetry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public int TotalTrackedBodies;
        [FieldOffset(8)] public int ActiveBodies;
        [FieldOffset(12)] public int AsleepBodies;
        [FieldOffset(16)] public float StateSyncTimeMs;
        [FieldOffset(20)] public float StateSyncMicroseconds;
        [FieldOffset(24)] public float JobMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public int ChangedIndices;
        [FieldOffset(36)] public int LockContentions;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public float RadiusSqScale;
        [FieldOffset(52)] public uint FrameHash;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysicsCullingCounter64
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] private byte _pad0;
        [FieldOffset(9)] private byte _pad1;
        [FieldOffset(10)] private byte _pad2;
        [FieldOffset(11)] private byte _pad3;
        [FieldOffset(12)] private byte _pad4;
        [FieldOffset(13)] private byte _pad5;
        [FieldOffset(14)] private byte _pad6;
        [FieldOffset(15)] private byte _pad7;
        [FieldOffset(16)] private byte _pad8;
        [FieldOffset(17)] private byte _pad9;
        [FieldOffset(18)] private byte _pad10;
        [FieldOffset(19)] private byte _pad11;
        [FieldOffset(20)] private byte _pad12;
        [FieldOffset(21)] private byte _pad13;
        [FieldOffset(22)] private byte _pad14;
        [FieldOffset(23)] private byte _pad15;
        [FieldOffset(24)] private byte _pad16;
        [FieldOffset(25)] private byte _pad17;
        [FieldOffset(26)] private byte _pad18;
        [FieldOffset(27)] private byte _pad19;
        [FieldOffset(28)] private byte _pad20;
        [FieldOffset(29)] private byte _pad21;
        [FieldOffset(30)] private byte _pad22;
        [FieldOffset(31)] private byte _pad23;
        [FieldOffset(32)] private byte _pad24;
        [FieldOffset(33)] private byte _pad25;
        [FieldOffset(34)] private byte _pad26;
        [FieldOffset(35)] private byte _pad27;
        [FieldOffset(36)] private byte _pad28;
        [FieldOffset(37)] private byte _pad29;
        [FieldOffset(38)] private byte _pad30;
        [FieldOffset(39)] private byte _pad31;
        [FieldOffset(40)] private byte _pad32;
        [FieldOffset(41)] private byte _pad33;
        [FieldOffset(42)] private byte _pad34;
        [FieldOffset(43)] private byte _pad35;
        [FieldOffset(44)] private byte _pad36;
        [FieldOffset(45)] private byte _pad37;
        [FieldOffset(46)] private byte _pad38;
        [FieldOffset(47)] private byte _pad39;
        [FieldOffset(48)] private byte _pad40;
        [FieldOffset(49)] private byte _pad41;
        [FieldOffset(50)] private byte _pad42;
        [FieldOffset(51)] private byte _pad43;
        [FieldOffset(52)] private byte _pad44;
        [FieldOffset(53)] private byte _pad45;
        [FieldOffset(54)] private byte _pad46;
        [FieldOffset(55)] private byte _pad47;
        [FieldOffset(56)] private byte _pad48;
        [FieldOffset(57)] private byte _pad49;
        [FieldOffset(58)] private byte _pad50;
        [FieldOffset(59)] private byte _pad51;
        [FieldOffset(60)] private byte _pad52;
        [FieldOffset(61)] private byte _pad53;
        [FieldOffset(62)] private byte _pad54;
        [FieldOffset(63)] private byte _pad55;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PhysicsCullingTuningDTO
    {
        [FieldOffset(0)] public float DebrisWakeRadiusMeters;
        [FieldOffset(4)] public float VehicleWakeRadiusMeters;
        [FieldOffset(8)] public float FrustumClampDistanceMeters;
        [FieldOffset(12)] public float HysteresisDelaySeconds;
        [FieldOffset(16)] public float SpatialCellSizeMeters;
        [FieldOffset(20)] public float MockShockwaveRadiusMeters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct PhysicsCullingDebugBody
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float AgeSeconds;
        [FieldOffset(28)] public int InstanceId;
        [FieldOffset(32)] public byte IsAsleep;
        [FieldOffset(33)] public byte IsHysteresisLocked;
        [FieldOffset(34)] private byte _pad0;
        [FieldOffset(35)] private byte _pad1;
        [FieldOffset(36)] private byte _pad2;
        [FieldOffset(37)] private byte _pad3;
        [FieldOffset(38)] private byte _pad4;
        [FieldOffset(39)] private byte _pad5;
    }

    public static class PhysicsCullingLayout1337
    {
        public const int PhysicsCullingDtoStrideBytes = 40;
        public const int FrozenVelocityStrideBytes = 32;
        public const int TargetWakeSignalStrideBytes = 16;
        public const int MockSeismicSignalStrideBytes = 48;
        public const int FrameTelemetryStrideBytes = 64;
        public const int CounterStrideBytes = 64;
        public const int TuningStrideBytes = 32;
        public const int DebugBodyStrideBytes = 40;
        public const int BodyTelemetryStrideBytes = 64;
        private const int ExternalLayoutFailureBit = 31;

        public static bool Validate(out int failureMask)
        {
            failureMask = 0;
            ExpectSize<PhysicsCullingDTO>(PhysicsCullingDtoStrideBytes, 0, ref failureMask);
            ExpectOffset<PhysicsCullingDTO>(nameof(PhysicsCullingDTO.AUP), 0, 1, ref failureMask);
            ExpectOffset<PhysicsCullingDTO>(nameof(PhysicsCullingDTO.InstanceId), 24, 2, ref failureMask);
            ExpectOffset<PhysicsCullingDTO>(nameof(PhysicsCullingDTO.ActivationRadiusSq), 28, 3, ref failureMask);
            ExpectOffset<PhysicsCullingDTO>(nameof(PhysicsCullingDTO.CullingFlags), 32, 4, ref failureMask);
            ExpectOffset<PhysicsCullingDTO>(nameof(PhysicsCullingDTO.IsAsleep), 36, 5, ref failureMask);
            ExpectSize<FrozenVelocityDTO>(FrozenVelocityStrideBytes, 6, ref failureMask);
            ExpectOffset<FrozenVelocityDTO>(nameof(FrozenVelocityDTO.LinearVelocity), 0, 7, ref failureMask);
            ExpectOffset<FrozenVelocityDTO>(nameof(FrozenVelocityDTO.AngularVelocity), 12, 8, ref failureMask);
            ExpectOffset<FrozenVelocityDTO>(nameof(FrozenVelocityDTO.HasVelocity), 24, 9, ref failureMask);
            ExpectSize<PhysicsCullingTargetWakeRequestSignal>(TargetWakeSignalStrideBytes, 10, ref failureMask);
            ExpectSize<MockSeismicShockwaveSignal>(MockSeismicSignalStrideBytes, 11, ref failureMask);
            ExpectSize<PhysicsCullingFrameTelemetry>(FrameTelemetryStrideBytes, 12, ref failureMask);
            ExpectOffset<PhysicsCullingFrameTelemetry>(nameof(PhysicsCullingFrameTelemetry.JobMicroseconds), 24, 13, ref failureMask);
            ExpectOffset<PhysicsCullingFrameTelemetry>(nameof(PhysicsCullingFrameTelemetry.GlobalQualityWeight), 28, 14, ref failureMask);
            ExpectOffset<PhysicsCullingFrameTelemetry>(nameof(PhysicsCullingFrameTelemetry.LockContentions), 36, 15, ref failureMask);
            ExpectSize<PhysicsCullingCounter64>(CounterStrideBytes, 16, ref failureMask);
            ExpectOffset<PhysicsCullingCounter64>(nameof(PhysicsCullingCounter64.Value), 0, 17, ref failureMask);
            ExpectOffset<PhysicsCullingCounter64>(nameof(PhysicsCullingCounter64.Flags), 4, 18, ref failureMask);
            ExpectSize<PhysicsCullingTuningDTO>(TuningStrideBytes, 19, ref failureMask);
            ExpectSize<PhysicsCullingDebugBody>(DebugBodyStrideBytes, 20, ref failureMask);
            ExpectOffset<PhysicsCullingDebugBody>(nameof(PhysicsCullingDebugBody.Aup), 0, 26, ref failureMask);
            ExpectOffset<PhysicsCullingDebugBody>(nameof(PhysicsCullingDebugBody.AgeSeconds), 24, 27, ref failureMask);
            ExpectOffset<PhysicsCullingDebugBody>(nameof(PhysicsCullingDebugBody.InstanceId), 28, 28, ref failureMask);
            ExpectOffset<PhysicsCullingDebugBody>(nameof(PhysicsCullingDebugBody.IsAsleep), 32, 29, ref failureMask);
            ExpectOffset<PhysicsCullingDebugBody>(nameof(PhysicsCullingDebugBody.IsHysteresisLocked), 33, 30, ref failureMask);
            if (!GlobalPhysicsStateManager.ValidatePhysicsCullingPrivateTelemetryLayout1337())
                failureMask |= 1 << 21;
            if (!GlobalPhysicsStateManager.ValidatePhysicsImpactEventLayout1337())
                MarkFailure(ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<PhysicsImpactSignal>(128, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.PrimaryBodyId), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.SecondaryBodyId), 8, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>("_pointAupMeters", 16, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.Point), 64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.Normal), 76, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.Force), 88, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.Intensity), 92, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.MassVelocity), 96, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.WeightClass), 100, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.PrimaryAudioMaterialId), 101, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>(nameof(PhysicsImpactSignal.SecondaryAudioMaterialId), 102, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<PhysicsImpactSignal>("_hasPointAup", 103, ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<WakeRequestSignal>(64, 22, ref failureMask);
            ExpectOffset<WakeRequestSignal>(nameof(WakeRequestSignal.OriginAup), 0, 23, ref failureMask);
            ExpectOffset<WakeRequestSignal>(nameof(WakeRequestSignal.RadiusMeters), 24, 24, ref failureMask);
            ExpectOffset<WakeRequestSignal>(nameof(WakeRequestSignal.Flags), 36, 25, ref failureMask);
            ExpectSize<ForcePacket>(64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.Force), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.Torque), 12, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.PointOffset), 24, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.Mode), 36, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.RigidbodyIndex), 40, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.Flags), 44, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>(nameof(ForcePacket.Priority), 45, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>("_padding0", 46, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<ForcePacket>("_padding17", 63, ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<AcousticPingEvent>(64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.RuntimePosition), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.RadiusMeters), 12, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.Intensity01), 16, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.LifetimeSeconds), 20, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.SignalRole), 24, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.SourceSpeciesId), 28, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>(nameof(AcousticPingEvent.EnergyJoules), 32, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>("_pad0", 36, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticPingEvent>("_pad27", 63, ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<AcousticImpulseEvent>(64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>(nameof(AcousticImpulseEvent.RuntimePosition), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>(nameof(AcousticImpulseEvent.Direction), 12, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>(nameof(AcousticImpulseEvent.KineticEnergyJoules), 24, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>(nameof(AcousticImpulseEvent.AudioMaterialId), 44, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>(nameof(AcousticImpulseEvent.Flags), 45, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>("_pad0", 46, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<AcousticImpulseEvent>("_pad17", 63, ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<LargeAcousticImpulseEvent>(64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>(nameof(LargeAcousticImpulseEvent.RuntimePosition), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>(nameof(LargeAcousticImpulseEvent.Direction), 12, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>(nameof(LargeAcousticImpulseEvent.KineticEnergyJoules), 24, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>(nameof(LargeAcousticImpulseEvent.AudioMaterialId), 44, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>(nameof(LargeAcousticImpulseEvent.Flags), 45, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>("_pad0", 46, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<LargeAcousticImpulseEvent>("_pad17", 63, ExternalLayoutFailureBit, ref failureMask);
            ExpectSize<RemovedPhysicsEventPayload>(128, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<RemovedPhysicsEventPayload>(nameof(RemovedPhysicsEventPayload.RuntimePosition), 0, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<RemovedPhysicsEventPayload>(nameof(RemovedPhysicsEventPayload.PrimaryId), 64, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<RemovedPhysicsEventPayload>(nameof(RemovedPhysicsEventPayload.EventType), 76, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<RemovedPhysicsEventPayload>("_pad0", 80, ExternalLayoutFailureBit, ref failureMask);
            ExpectOffset<RemovedPhysicsEventPayload>("_pad47", 127, ExternalLayoutFailureBit, ref failureMask);

            return failureMask == 0;
        }

        public static bool ValidateForEditor()
        {
            bool valid = Validate(out int failureMask);
            if (!valid)
                H8Debug.LogError("[1337] Physics culling DTO layout violation.");

            return valid;
        }

        private static void ExpectSize<T>(int expected, int bit, ref int failureMask)
            where T : struct
        {
            if (UnsafeUtility.SizeOf<T>() != expected)
                MarkFailure(bit, ref failureMask);
        }

        private static void ExpectOffset<T>(string fieldName, int expected, int bit, ref int failureMask)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed != expected)
                MarkFailure(bit, ref failureMask);
        }

        private static void MarkFailure(int bit, ref int failureMask)
        {
            failureMask |= bit == 31 ? unchecked((int)0x80000000) : 1 << bit;
        }
    }

    public sealed partial class GlobalPhysicsStateManager
    {
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct PhysicsCullingBlackBoxDumpHeader1337
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint ReasonHash;
            [FieldOffset(12)] public uint Flags;
            [FieldOffset(16)] public int FrameIndex;
            [FieldOffset(20)] public int BodyEntryCount;
            [FieldOffset(24)] public int FrameEntryCount;
            [FieldOffset(28)] public int BodyEntryStride;
            [FieldOffset(32)] public int FrameEntryStride;
            [FieldOffset(36)] public float ScalarValue;
            [FieldOffset(40)] public float GlobalQualityWeight;
            [FieldOffset(44)] public float LastJobMicroseconds;
            [FieldOffset(48)] public uint StateHash;
            [FieldOffset(52)] public uint BodyRingWriteIndex;
            [FieldOffset(56)] public uint FrameRingWriteIndex;
            [FieldOffset(60)] public uint Reserved0;
        }

        private const int PhysicsCullingFrameTelemetryCapacity = 300;
        private const int PhysicsCullingTargetWakeQueueCapacity = 64;
        private const int PhysicsCullingMockSeismicSignalCapacity = 16;
#if UNITY_EDITOR
        private const int PhysicsCullingCsvScratchCapacity = 4096;
        private const int PhysicsCullingLegacyRadiiHeaderBytes = 64;
#endif
        private const int PhysicsCullingSpatialBucketCapacity = MaxTrackedBodies * 2;
        private const int PhysicsCullingMockBodiesPerGenerate = 1000;
        private const float PhysicsCullingDefaultDebrisWakeRadiusMeters = 50f;
        private const float PhysicsCullingDefaultVehicleWakeRadiusMeters = 200f;
        private const float PhysicsCullingDefaultFrustumClampDistanceMeters = 150f;
        private const float PhysicsCullingDefaultHysteresisSeconds = 2f;
        private const float PhysicsCullingStateSyncDumpThresholdMs = 1f;
        private const float PhysicsCullingSpatialCellSizeMeters = 50f;
        private const float PhysicsCullingInvSpatialCellSizeMeters = 1f / PhysicsCullingSpatialCellSizeMeters;
        private const float PhysicsCullingSpatialHashRebuildIntervalSeconds = 1f;
#if UNITY_EDITOR
        private const float PhysicsCullingCsvPollIntervalSeconds = 1f;
#endif
        private const float PhysicsCullingFrustumInnerSphereRadiusMeters = 20f;
        private const double PhysicsCullingLocalDeltaClampMeters = 1000000d;
        private const uint PhysicsCullingBlackBoxDumpMagic1337 = 0x50433744u;
        private const uint PhysicsCullingBlackBoxDumpVersion1337 = 1u;
        private const uint PhysicsCullingWakeRegionSourceExternal = 0x57524B45u;
        private const string PhysicsCullingBlackBoxRelativePath1337 = "Docs/AgentLogs/Dump_1337_PhysicsCulling.bin";
        private const float PhysicsCullingWakeRadiusSqScale = 0.81f;
        private const uint PhysicsCullingDtoExemptFlag = 1u;
        private const uint PhysicsCullingFrameTelemetryMockBodiesFlag = 1u;
        private const uint PhysicsCullingFrameTelemetryColliderTransitionsFlag = 1u << 1;
#if UNITY_EDITOR
        private const string PhysicsCullingProfilesRelativePath = "Docs/Modding/physics_culling_profiles.csv";
#endif
        private static readonly ulong PhysicsTrackedBodyLaneMutationGuardMask1337 =
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyLastValidPositions) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyAUPs) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingDtos) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingFrozenVelocities) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingStateAges);
        private static readonly ulong PhysicsTargetWakeMutationGuardMask1337 =
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingWakeRequestMirror) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingWakeRequestCount);
        private static readonly ulong PhysicsMockBodyGenerationMutationGuardMask1337 =
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingDtos) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingFrozenVelocities) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingStateAges) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingState);

        private VaultBufferBinding<PhysicsCullingDTO> _physicsCullingDtos =
            new VaultBufferBinding<PhysicsCullingDTO>(BufferID.ShinobuPhysicsCullingDtos, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<FrozenVelocityDTO> _physicsFrozenVelocities =
            new VaultBufferBinding<FrozenVelocityDTO>(BufferID.ShinobuPhysicsCullingFrozenVelocities, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<float> _physicsCullingStateAges =
            new VaultBufferBinding<float>(BufferID.ShinobuPhysicsCullingStateAges, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<int> _physicsCullingSpatialCandidates =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingSpatialCandidates, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _physicsCullingSpatialCandidateMask =
            new VaultBufferBinding<byte>(BufferID.ShinobuPhysicsCullingSpatialCandidateMask, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingFrameTelemetry> _physicsCullingFrameTelemetry =
            new VaultBufferBinding<PhysicsCullingFrameTelemetry>(BufferID.ShinobuPhysicsCullingFrameTelemetry, PhysicsCullingFrameTelemetryCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTuningDTO> _physicsCullingTuning =
            new VaultBufferBinding<PhysicsCullingTuningDTO>(BufferID.ShinobuPhysicsCullingTuning, 1, OwnerSystemId);
        private VaultBufferBinding<MockSeismicShockwaveSignal> _physicsMockSeismicSignals =
            new VaultBufferBinding<MockSeismicShockwaveSignal>(BufferID.ShinobuPhysicsCullingMockSeismicSignals, PhysicsCullingMockSeismicSignalCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTargetWakeRequestSignal> _physicsWakeRequestMirror =
            new VaultBufferBinding<PhysicsCullingTargetWakeRequestSignal>(BufferID.ShinobuPhysicsCullingWakeRequestMirror, PhysicsCullingTargetWakeQueueCapacity, OwnerSystemId);
        private VaultBufferBinding<int> _physicsSpatialBucketHeads =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingSpatialBucketHeads, PhysicsCullingSpatialBucketCapacity, OwnerSystemId);
        private VaultBufferBinding<int> _physicsSpatialNext =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingSpatialNext, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<int> _physicsSpatialCellHashes =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingSpatialCellHashes, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<int> _physicsStateChangedIndices =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingChangedIndices, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingCounter64> _physicsStateChangedCount =
            new VaultBufferBinding<PhysicsCullingCounter64>(BufferID.ShinobuPhysicsCullingChangedCount, 1, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingCounter64> _physicsTargetWakeRequestCount =
            new VaultBufferBinding<PhysicsCullingCounter64>(BufferID.ShinobuPhysicsCullingWakeRequestCount, 1, OwnerSystemId);
        // COLD ALLOC: fixed-size managed scratch keeps targeted wake queue locks away from Rigidbody/Collider side effects.
        private readonly PhysicsCullingTargetWakeRequestSignal[] _physicsTargetWakeApplyScratch =
            new PhysicsCullingTargetWakeRequestSignal[PhysicsCullingTargetWakeQueueCapacity];
        private readonly Plane[] _physicsFrustumPlaneScratch = new Plane[6]; // COLD ALLOC: Plane[6] - Unity frustum API scratch for GeometryUtility.CalculateFrustumPlanes(Camera, Plane[]) - owner: GlobalPhysicsStateManager
        private int _physicsCullingFrameTelemetryWriteIndex;
        private int _physicsCullingMockBodyCount;
        private int _physicsCullingColliderToggleTransitionsThisFrame;
        private uint _physicsCullingSimulationFrame;
        private byte _physicsMockSeismicPending;
        private int _physicsSpatialHashLastCount = -1;
#if UNITY_EDITOR
        private long _physicsCullingCsvLastWriteTicks;
        private string _physicsCullingCsvAbsolutePath;
#endif
        private float _physicsSpatialHashRebuildAccumulator;
#if UNITY_EDITOR
        private float _physicsCullingCsvPollAccumulator;
#endif
        private bool _physicsCullingTuningInitialized;
        private bool _physicsSpatialHashDirty = true;

        private void BindShinobu37PhysicsCullingDataVault(IDataVault dataVault)
        {
            _physicsCullingDtos.BindDataVault(dataVault);
            _physicsFrozenVelocities.BindDataVault(dataVault);
            _physicsCullingStateAges.BindDataVault(dataVault);
            _physicsCullingSpatialCandidates.BindDataVault(dataVault);
            _physicsCullingSpatialCandidateMask.BindDataVault(dataVault);
            _physicsCullingFrameTelemetry.BindDataVault(dataVault);
            _physicsCullingTuning.BindDataVault(dataVault);
            _physicsMockSeismicSignals.BindDataVault(dataVault);
            _physicsWakeRequestMirror.BindDataVault(dataVault);
            _physicsSpatialBucketHeads.BindDataVault(dataVault);
            _physicsSpatialNext.BindDataVault(dataVault);
            _physicsSpatialCellHashes.BindDataVault(dataVault);
            _physicsStateChangedIndices.BindDataVault(dataVault);
            _physicsStateChangedCount.BindDataVault(dataVault);
            _physicsTargetWakeRequestCount.BindDataVault(dataVault);
        }

        private void EnsureShinobu37PhysicsCullingState()
        {
            if (!_physicsCullingDtos.IsCreated)
                _physicsCullingDtos.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsFrozenVelocities.IsCreated)
                _physicsFrozenVelocities.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingStateAges.IsCreated)
                _physicsCullingStateAges.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingSpatialCandidates.IsCreated)
                _physicsCullingSpatialCandidates.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingSpatialCandidateMask.IsCreated)
                _physicsCullingSpatialCandidateMask.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingFrameTelemetry.IsCreated)
                _physicsCullingFrameTelemetry.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingTuning.IsCreated)
                _physicsCullingTuning.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsMockSeismicSignals.IsCreated)
                _physicsMockSeismicSignals.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsWakeRequestMirror.IsCreated)
                _physicsWakeRequestMirror.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsSpatialBucketHeads.IsCreated)
                _physicsSpatialBucketHeads.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsSpatialNext.IsCreated)
                _physicsSpatialNext.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsSpatialCellHashes.IsCreated)
                _physicsSpatialCellHashes.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsStateChangedIndices.IsCreated)
                _physicsStateChangedIndices.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsStateChangedCount.IsCreated)
                _physicsStateChangedCount.Ensure(NativeArrayOptions.ClearMemory);
            if (!_physicsTargetWakeRequestCount.IsCreated)
                _physicsTargetWakeRequestCount.Ensure(NativeArrayOptions.ClearMemory);
            InitializePhysicsCullingTuningIfNeeded();
        }

        private bool HasUndersizedShinobu37PhysicsCullingState()
        {
            bool undersized =
                (_physicsCullingDtos.IsCreated && _physicsCullingDtos.Length < MaxTrackedBodies) ||
                (_physicsFrozenVelocities.IsCreated && _physicsFrozenVelocities.Length < MaxTrackedBodies) ||
                (_physicsCullingStateAges.IsCreated && _physicsCullingStateAges.Length < MaxTrackedBodies) ||
                (_physicsCullingSpatialCandidates.IsCreated && _physicsCullingSpatialCandidates.Length < MaxTrackedBodies) ||
                (_physicsCullingSpatialCandidateMask.IsCreated && _physicsCullingSpatialCandidateMask.Length < MaxTrackedBodies) ||
                (_physicsCullingFrameTelemetry.IsCreated && _physicsCullingFrameTelemetry.Length < PhysicsCullingFrameTelemetryCapacity) ||
                (_physicsCullingTuning.IsCreated && _physicsCullingTuning.Length < 1) ||
                (_physicsMockSeismicSignals.IsCreated && _physicsMockSeismicSignals.Length < PhysicsCullingMockSeismicSignalCapacity) ||
                (_physicsWakeRequestMirror.IsCreated && _physicsWakeRequestMirror.Length < PhysicsCullingTargetWakeQueueCapacity) ||
                (_physicsSpatialBucketHeads.IsCreated && _physicsSpatialBucketHeads.Length < PhysicsCullingSpatialBucketCapacity) ||
                (_physicsSpatialNext.IsCreated && _physicsSpatialNext.Length < MaxTrackedBodies) ||
                (_physicsSpatialCellHashes.IsCreated && _physicsSpatialCellHashes.Length < MaxTrackedBodies) ||
                (_physicsStateChangedIndices.IsCreated && _physicsStateChangedIndices.Length < MaxTrackedBodies) ||
                (_physicsStateChangedCount.IsCreated && _physicsStateChangedCount.Length < 1) ||
                (_physicsTargetWakeRequestCount.IsCreated && _physicsTargetWakeRequestCount.Length < 1);
            return undersized;
        }

        private void ReleaseUndersizedShinobu37PhysicsCullingState()
        {
            ReleaseUndersizedVaultBuffer(ref _physicsCullingDtos, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsFrozenVelocities, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingStateAges, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingSpatialCandidates, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingSpatialCandidateMask, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingFrameTelemetry, PhysicsCullingFrameTelemetryCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingTuning, 1);
            ReleaseUndersizedVaultBuffer(ref _physicsMockSeismicSignals, PhysicsCullingMockSeismicSignalCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsWakeRequestMirror, PhysicsCullingTargetWakeQueueCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsSpatialBucketHeads, PhysicsCullingSpatialBucketCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsSpatialNext, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsSpatialCellHashes, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsStateChangedIndices, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsStateChangedCount, 1);
            ReleaseUndersizedVaultBuffer(ref _physicsTargetWakeRequestCount, 1);
            _physicsCullingTuningInitialized = false;
        }

        private bool HasRequiredShinobu37PhysicsCullingState()
        {
            bool ready =
                _physicsCullingDtos.IsCreated &&
                _physicsCullingDtos.Length >= MaxTrackedBodies &&
                _physicsFrozenVelocities.IsCreated &&
                _physicsFrozenVelocities.Length >= MaxTrackedBodies &&
                _physicsCullingStateAges.IsCreated &&
                _physicsCullingStateAges.Length >= MaxTrackedBodies &&
                _physicsCullingSpatialCandidates.IsCreated &&
                _physicsCullingSpatialCandidates.Length >= MaxTrackedBodies &&
                _physicsCullingSpatialCandidateMask.IsCreated &&
                _physicsCullingSpatialCandidateMask.Length >= MaxTrackedBodies &&
                _physicsCullingFrameTelemetry.IsCreated &&
                _physicsCullingFrameTelemetry.Length >= PhysicsCullingFrameTelemetryCapacity &&
                _physicsCullingTuning.IsCreated &&
                _physicsCullingTuning.Length >= 1 &&
                _physicsMockSeismicSignals.IsCreated &&
                _physicsMockSeismicSignals.Length >= PhysicsCullingMockSeismicSignalCapacity &&
                _physicsWakeRequestMirror.IsCreated &&
                _physicsWakeRequestMirror.Length >= PhysicsCullingTargetWakeQueueCapacity &&
                _physicsSpatialBucketHeads.IsCreated &&
                _physicsSpatialBucketHeads.Length >= PhysicsCullingSpatialBucketCapacity &&
                _physicsSpatialNext.IsCreated &&
                _physicsSpatialNext.Length >= MaxTrackedBodies &&
                _physicsSpatialCellHashes.IsCreated &&
                _physicsSpatialCellHashes.Length >= MaxTrackedBodies &&
                _physicsStateChangedIndices.IsCreated &&
                _physicsStateChangedIndices.Length >= MaxTrackedBodies &&
                _physicsStateChangedCount.IsCreated &&
                _physicsStateChangedCount.Length >= 1 &&
                _physicsTargetWakeRequestCount.IsCreated &&
                _physicsTargetWakeRequestCount.Length >= 1;
            return ready;
        }

        private void ReleaseShinobu37PhysicsCullingState()
        {
            _physicsCullingDtos.ReleaseView();
            _physicsFrozenVelocities.ReleaseView();
            _physicsCullingStateAges.ReleaseView();
            _physicsCullingSpatialCandidates.ReleaseView();
            _physicsCullingSpatialCandidateMask.ReleaseView();
            _physicsCullingFrameTelemetry.ReleaseView();
            _physicsCullingTuning.ReleaseView();
            _physicsMockSeismicSignals.ReleaseView();
            _physicsWakeRequestMirror.ReleaseView();
            _physicsSpatialBucketHeads.ReleaseView();
            _physicsSpatialNext.ReleaseView();
            _physicsSpatialCellHashes.ReleaseView();
            _physicsStateChangedIndices.ReleaseView();
            _physicsStateChangedCount.ReleaseView();
            _physicsTargetWakeRequestCount.ReleaseView();
            _physicsCullingTuningInitialized = false;
        }

        private void InitializePhysicsCullingTuningIfNeeded()
        {
            if (_physicsCullingTuningInitialized || !_physicsCullingTuning.IsCreated)
                return;

#if UNITY_EDITOR
            if (!TryLoadLegacyPhysicsCullingRadii(out PhysicsCullingTuningDTO tuning))
                tuning = GenerateEmergencyMockRadii();
            else
                _physicsCullingTuning[0] = tuning;
#else
            GenerateEmergencyMockRadii();
#endif

            _physicsCullingTuningInitialized = true;
        }

        private PhysicsCullingTuningDTO GenerateEmergencyMockRadii()
        {
            PhysicsCullingTuningDTO tuning = DefaultPhysicsCullingTuning();
            if (_physicsCullingTuning.IsCreated)
                _physicsCullingTuning[0] = tuning;

            return tuning;
        }

#if UNITY_EDITOR
        private bool TryLoadLegacyPhysicsCullingRadii(out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (TryLoadLegacyPhysicsCullingRadiiFromRoot(Path.Combine(projectRoot, "Docs", "Archive"), out tuning))
                return true;

            string streamingAssetsPath = Application.streamingAssetsPath;
            return !string.IsNullOrEmpty(streamingAssetsPath) &&
                TryLoadLegacyPhysicsCullingRadiiFromRoot(streamingAssetsPath, out tuning);
        }

        private bool TryLoadLegacyPhysicsCullingRadiiFromRoot(string root, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return false;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "physics_culling_radii.h8bin", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }

            for (int i = 0; i < files.Length; i++)
            {
                if (TryReadLegacyPhysicsCullingRadiiHeader(files[i], out tuning))
                    return true;
            }

            return false;
        }

        private bool TryReadLegacyPhysicsCullingRadiiHeader(string path, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                Span<byte> scratch = stackalloc byte[PhysicsCullingLegacyRadiiHeaderBytes];
                int bytesRead;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, PhysicsCullingLegacyRadiiHeaderBytes, FileOptions.SequentialScan))
                {
                    bytesRead = stream.Read(scratch);
                }

                if (bytesRead <= 0)
                    return false;

                int safeBytes = math.min(bytesRead, scratch.Length);
                return TryParseLegacyPhysicsCullingRadiiHeader(scratch.Slice(0, safeBytes), out tuning);
            }
            catch (IOException)
            {
                tuning = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                tuning = default;
                return false;
            }
            catch (System.Security.SecurityException)
            {
                tuning = default;
                return false;
            }
        }
#endif

        private static bool TryParseLegacyPhysicsCullingRadiiHeader(ReadOnlySpan<byte> bytes, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (bytes.Length < 16)
                return false;

            if (bytes.Length >= 28 &&
                TryBuildPhysicsCullingTuningFromLegacyFloats(
                    ReadSingleLittleEndian(bytes, 8),
                    ReadSingleLittleEndian(bytes, 12),
                    ReadSingleLittleEndian(bytes, 16),
                    ReadSingleLittleEndian(bytes, 20),
                    ReadSingleLittleEndian(bytes, 24),
                    out tuning))
            {
                return true;
            }

            return TryBuildPhysicsCullingTuningFromLegacyFloats(
                ReadSingleLittleEndian(bytes, 0),
                ReadSingleLittleEndian(bytes, 4),
                ReadSingleLittleEndian(bytes, 8),
                ReadSingleLittleEndian(bytes, 12),
                ImpactWakeMaximumRadiusMeters,
                out tuning);
        }

        private static bool TryBuildPhysicsCullingTuningFromLegacyFloats(
            float debrisRadiusMeters,
            float vehicleRadiusMeters,
            float frustumClampDistanceMeters,
            float hysteresisDelaySeconds,
            float mockShockwaveRadiusMeters,
            out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (!math.isfinite(debrisRadiusMeters) ||
                !math.isfinite(vehicleRadiusMeters) ||
                !math.isfinite(frustumClampDistanceMeters) ||
                !math.isfinite(hysteresisDelaySeconds) ||
                debrisRadiusMeters <= 0f ||
                vehicleRadiusMeters <= 0f ||
                frustumClampDistanceMeters <= 0f ||
                hysteresisDelaySeconds <= 0f)
            {
                return false;
            }

            tuning = new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = math.clamp(debrisRadiusMeters, 1f, 500f),
                VehicleWakeRadiusMeters = math.clamp(vehicleRadiusMeters, 1f, 1000f),
                FrustumClampDistanceMeters = math.clamp(frustumClampDistanceMeters, 20f, 1000f),
                HysteresisDelaySeconds = math.clamp(hysteresisDelaySeconds, 0.1f, 10f),
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = math.isfinite(mockShockwaveRadiusMeters)
                    ? math.clamp(mockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters)
                    : ImpactWakeMaximumRadiusMeters,
                Flags = 2u
            };
            return true;
        }

        private static float ReadSingleLittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return float.NaN;

            uint bits =
                (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
            return math.asfloat(bits);
        }

        private void ClearShinobu37PhysicsCullingState()
        {
            bool clearLocksAcquired = TryAcquirePhysicsCullingClearLocks1337(out ulong clearLockMask);
            if (!clearLocksAcquired)
            {
                _physicsCullingLockContentionsThisFrame++;
                _physicsMockSeismicPending = 0;
                _physicsCullingMockBodyCount = 0;
                _physicsSpatialHashLastCount = -1;
                _physicsSpatialHashRebuildAccumulator = 0f;
#if UNITY_EDITOR
                _physicsCullingCsvPollAccumulator = 0f;
#endif
                _physicsSpatialHashDirty = true;
                return;
            }

            try
            {
                ClearPhysicsCullingSpatialHash();
                ClearPhysicsStateChangedQueue();
                ClearPhysicsTargetWakeRequests();
                _physicsMockSeismicPending = 0;

                int clearCount = math.min(MaxTrackedBodies, _physicsCullingDtos.IsCreated ? _physicsCullingDtos.Length : 0);
                for (int i = 0; i < clearCount; i++)
                {
                    _physicsCullingDtos[i] = default;
                    _physicsFrozenVelocities[i] = default;
                    _physicsCullingStateAges[i] = default;
                    _physicsCullingSpatialCandidates[i] = default;
                    _physicsCullingSpatialCandidateMask[i] = default;
                    if (_physicsSpatialNext.IsCreated)
                        _physicsSpatialNext[i] = -1;
                    if (_physicsSpatialCellHashes.IsCreated)
                        _physicsSpatialCellHashes[i] = 0;
                }

                if (_physicsCullingFrameTelemetry.IsCreated)
                {
                    int telemetryCount = math.min(_physicsCullingFrameTelemetry.Length, PhysicsCullingFrameTelemetryCapacity);
                    for (int i = 0; i < telemetryCount; i++)
                        _physicsCullingFrameTelemetry[i] = default;
                }

                if (_physicsMockSeismicSignals.IsCreated)
                {
                    int signalCount = math.min(_physicsMockSeismicSignals.Length, PhysicsCullingMockSeismicSignalCapacity);
                    for (int i = 0; i < signalCount; i++)
                        _physicsMockSeismicSignals[i] = default;
                }

                _physicsCullingFrameTelemetryWriteIndex = 0;
                _physicsCullingMockBodyCount = 0;
                _physicsCullingSimulationFrame = 0u;
                _physicsSpatialHashLastCount = -1;
                _physicsSpatialHashRebuildAccumulator = 0f;
#if UNITY_EDITOR
                _physicsCullingCsvPollAccumulator = 0f;
#endif
                _physicsSpatialHashDirty = true;
            }
            finally
            {
                if (clearLocksAcquired)
                    ReleasePhysicsCullingClearLocks1337(clearLockMask);
            }
        }

        private bool TryAcquirePhysicsCullingClearLocks1337(out ulong acquiredLockMask)
        {
            acquiredLockMask = ResolvePhysicsCullingClearMutationGuardMask1337();
            if (acquiredLockMask == 0UL)
                return true;

            if (TryAcquirePhysicsMutationGuard(acquiredLockMask))
                return true;

            acquiredLockMask = 0UL;
            return false;
        }

        private void ReleasePhysicsCullingClearLocks1337(ulong acquiredLockMask)
        {
            ReleasePhysicsMutationGuard(acquiredLockMask);
        }

        private ulong ResolvePhysicsCullingClearMutationGuardMask1337()
        {
            ulong mutationGuardMask = 0UL;
            if (_physicsCullingDtos.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingDtos);
            if (_physicsFrozenVelocities.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingFrozenVelocities);
            if (_physicsCullingStateAges.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingStateAges);
            if (_physicsCullingSpatialCandidates.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCandidates);
            if (_physicsCullingSpatialCandidateMask.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCandidateMask);
            if (_physicsSpatialBucketHeads.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialBucketHeads);
            if (_physicsSpatialNext.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialNext);
            if (_physicsSpatialCellHashes.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCellHashes);
            if (_physicsStateChangedCount.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingChangedCount);
            if (_physicsCullingFrameTelemetry.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingFrameTelemetry);
            if (_physicsMockSeismicSignals.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingMockSeismicSignals);
            if (_physicsWakeRequestMirror.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingWakeRequestMirror);
            if (_physicsTargetWakeRequestCount.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingWakeRequestCount);
            return mutationGuardMask;
        }

        private void InitializePhysicsCullingDtoForBody(int bodyIndex, Rigidbody body, in RigidbodyState bodyState)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            AbsoluteUniversePosition bodyAup = bodyState.HasLastValidAup != 0
                ? bodyState.LastValidAup
                : body != null && TryResolveAupFromRuntimeOrigin(body.position, out AbsoluteUniversePosition resolvedBodyAup)
                    ? resolvedBodyAup
                    : default;
            WritePhysicsCullingDto(bodyIndex, body, in bodyState, in bodyAup);
            _physicsFrozenVelocities[bodyIndex] = default;
            _physicsCullingStateAges[bodyIndex] = ResolvePhysicsCullingHysteresisSeconds();
            MarkPhysicsCullingSpatialHashDirty();
        }

        private void WritePhysicsCullingDto(int bodyIndex, Rigidbody body, in RigidbodyState bodyState, in AbsoluteUniversePosition bodyAup)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            uint flags = (uint)bodyState.CullingFlags;
            if ((bodyState.CullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
                flags |= PhysicsCullingDtoExemptFlag;

            _physicsCullingDtos[bodyIndex] = new PhysicsCullingDTO
            {
                AUP = bodyAup.ToAbsoluteDouble3(),
                InstanceId = body != null ? body.GetEntityId().GetHashCode() : 0,
                ActivationRadiusSq = ResolvePhysicsCullingActivationRadiusSq(body, in bodyState),
                IsAsleep = bodyState.DistanceSleepActive != 0 ? (byte)1 : (byte)0,
                CullingFlags = flags
            };
        }

        private void MarkPhysicsCullingSpatialHashDirty()
        {
            _physicsSpatialHashDirty = true;
        }

        private void WritePhysicsCullingDtoSleepState(int bodyIndex, byte isAsleep)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            PhysicsCullingDTO dto = _physicsCullingDtos[bodyIndex];
            dto.IsAsleep = isAsleep;
            _physicsCullingDtos[bodyIndex] = dto;
            if (_physicsCullingStateAges.IsCreated)
                _physicsCullingStateAges[bodyIndex] = 0f;
        }

        private float ResolvePhysicsCullingActivationRadiusSq(Rigidbody body, in RigidbodyState bodyState)
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            float radiusMeters = tuning.DebrisWakeRadiusMeters;
            if (body != null && (body.mass >= 250f || (bodyState.CullingFlags & PhysicsCullingFlags.HeavyCollider) != 0))
                radiusMeters = tuning.VehicleWakeRadiusMeters;

            radiusMeters = math.max(1f, radiusMeters);
            return radiusMeters * radiusMeters;
        }

        private PhysicsCullingTuningDTO ResolvePhysicsCullingTuning()
        {
            return _physicsCullingTuning.IsCreated && _physicsCullingTuningInitialized
                ? _physicsCullingTuning[0]
                : DefaultPhysicsCullingTuning();
        }

        private static PhysicsCullingTuningDTO DefaultPhysicsCullingTuning()
        {
            return new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = PhysicsCullingDefaultDebrisWakeRadiusMeters,
                VehicleWakeRadiusMeters = PhysicsCullingDefaultVehicleWakeRadiusMeters,
                FrustumClampDistanceMeters = PhysicsCullingDefaultFrustumClampDistanceMeters,
                HysteresisDelaySeconds = PhysicsCullingDefaultHysteresisSeconds,
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = ImpactWakeMaximumRadiusMeters,
                Flags = 1u
            };
        }

        private int BuildPhysicsCullingSpatialCandidates(in AbsoluteUniversePosition cameraAup, int jobCount)
        {
            if (!_physicsSpatialBucketHeads.IsCreated ||
                !_physicsSpatialNext.IsCreated ||
                !_physicsSpatialCellHashes.IsCreated ||
                !_physicsCullingDtos.IsCreated)
            {
                return 0;
            }

            int count = math.min(jobCount, math.min(_physicsCullingDtos.Length, MaxTrackedBodies));
            for (int i = 0; i < count; i++)
                _physicsCullingSpatialCandidateMask[i] = 0;

            RefreshPhysicsCullingSpatialHashIfNeeded(count);

            double3 cameraAbsolute = cameraAup.ToAbsoluteDouble3();
            int3 cameraCell = ResolvePhysicsCullingCell(cameraAbsolute);

            int candidateCount = 0;
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    int cellHash = HashPhysicsCullingCell(new int3(cameraCell.x + x, cameraCell.y, cameraCell.z + z));
                    int bucket = ResolvePhysicsCullingSpatialBucket(cellHash);
                    int bodyIndex = _physicsSpatialBucketHeads[bucket];
                    int guard = 0;
                    while (bodyIndex >= 0 && guard < MaxTrackedBodies)
                    {
                        int currentIndex = bodyIndex;
                        bodyIndex = (uint)currentIndex < (uint)_physicsSpatialNext.Length ? _physicsSpatialNext[currentIndex] : -1;
                        guard++;
                        if ((uint)currentIndex >= (uint)count || _physicsSpatialCellHashes[currentIndex] != cellHash)
                            continue;

                        TryAddPhysicsCullingCandidate(currentIndex, ref candidateCount);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                PhysicsCullingDTO dto = _physicsCullingDtos[i];
                byte currentState = i < _rigidbodyCullingStateSnapshot.Length ? _rigidbodyCullingStateSnapshot[i] : (byte)0;
                if (dto.IsAsleep == 0 || (currentState & CullingStateSleepActive) == 0)
                    TryAddPhysicsCullingCandidate(i, ref candidateCount);
            }

            return candidateCount;
        }

        private void RefreshPhysicsCullingSpatialHashIfNeeded(int count)
        {
            _physicsSpatialHashRebuildAccumulator += PhysicsCullingSlowTickIntervalSeconds;
            if (!_physicsSpatialHashDirty &&
                _physicsSpatialHashLastCount == count &&
                _physicsSpatialHashRebuildAccumulator < PhysicsCullingSpatialHashRebuildIntervalSeconds)
            {
                return;
            }

            RebuildPhysicsCullingSpatialHash(count);
            _physicsSpatialHashDirty = false;
            _physicsSpatialHashLastCount = count;
            _physicsSpatialHashRebuildAccumulator = 0f;
        }

        private void RebuildPhysicsCullingSpatialHash(int count)
        {
            ClearPhysicsCullingSpatialHash();
            int safeCount = math.min(count, math.min(_physicsCullingDtos.Length, MaxTrackedBodies));
            for (int i = 0; i < safeCount; i++)
            {
                PhysicsCullingDTO dto = _physicsCullingDtos[i];
                if (dto.InstanceId == 0 && i >= _trackedBodyCount)
                    continue;

                int cellHash = HashPhysicsCullingCell(ResolvePhysicsCullingCell(dto.AUP));
                int bucket = ResolvePhysicsCullingSpatialBucket(cellHash);
                _physicsSpatialCellHashes[i] = cellHash;
                _physicsSpatialNext[i] = _physicsSpatialBucketHeads[bucket];
                _physicsSpatialBucketHeads[bucket] = i;
            }
        }

        private void ClearPhysicsCullingSpatialHash()
        {
            if (_physicsSpatialBucketHeads.IsCreated)
            {
                int bucketCount = math.min(_physicsSpatialBucketHeads.Length, PhysicsCullingSpatialBucketCapacity);
                for (int i = 0; i < bucketCount; i++)
                    _physicsSpatialBucketHeads[i] = -1;
            }

            if (_physicsSpatialNext.IsCreated)
            {
                int nextCount = math.min(_physicsSpatialNext.Length, MaxTrackedBodies);
                for (int i = 0; i < nextCount; i++)
                    _physicsSpatialNext[i] = -1;
            }

            if (_physicsSpatialCellHashes.IsCreated)
            {
                int hashCount = math.min(_physicsSpatialCellHashes.Length, MaxTrackedBodies);
                for (int i = 0; i < hashCount; i++)
                    _physicsSpatialCellHashes[i] = 0;
            }
        }

        private static int ResolvePhysicsCullingSpatialBucket(int cellHash)
        {
            return cellHash & (PhysicsCullingSpatialBucketCapacity - 1);
        }

        private void TryAddPhysicsCullingCandidate(int bodyIndex, ref int candidateCount)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies ||
                candidateCount >= MaxTrackedBodies ||
                _physicsCullingSpatialCandidateMask[bodyIndex] != 0)
            {
                return;
            }

            _physicsCullingSpatialCandidateMask[bodyIndex] = 1;
            _physicsCullingSpatialCandidates[candidateCount] = bodyIndex;
            candidateCount++;
        }

        private static int3 ResolvePhysicsCullingCell(double3 absoluteAup)
        {
            return new int3(
                (int)math.floor(absoluteAup.x * PhysicsCullingInvSpatialCellSizeMeters),
                (int)math.floor(absoluteAup.y * PhysicsCullingInvSpatialCellSizeMeters),
                (int)math.floor(absoluteAup.z * PhysicsCullingInvSpatialCellSizeMeters));
        }

        private static int HashPhysicsCullingCell(int3 cell)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 73856093) ^ cell.x;
                hash = (hash * 19349663) ^ cell.y;
                hash = (hash * 83492791) ^ cell.z;
                return hash;
            }
        }

        private void ClearPhysicsStateChangedQueue()
        {
            if (_physicsStateChangedCount.IsCreated)
                _physicsStateChangedCount[0] = default;
        }

        private JobHandle SchedulePhysicsChangedIndexClear(int scanCount, JobHandle inputDependency)
        {
            ClearPhysicsStateChangedQueue();
            if (!_physicsStateChangedIndices.IsCreated || scanCount <= 0)
                return inputDependency;

            int count = math.min(scanCount, _physicsStateChangedIndices.Length);
            if (count <= 0)
                return inputDependency;

            ClearPhysicsChangedIndicesJob job = new ClearPhysicsChangedIndicesJob
            {
                ChangedIndices = _physicsStateChangedIndices,
                Count = count
            };
            return job.Schedule(count, 64, inputDependency);
        }

        private JobHandle SchedulePhysicsChangedIndexCompaction(int scanCount, JobHandle inputDependency)
        {
            if (!_physicsStateChangedIndices.IsCreated || !_physicsStateChangedCount.IsCreated || scanCount <= 0)
                return inputDependency;

            CompactPhysicsChangedIndicesJob job = new CompactPhysicsChangedIndicesJob
            {
                ChangedIndices = _physicsStateChangedIndices,
                ChangedCount = _physicsStateChangedCount,
                Count = math.min(scanCount, _physicsStateChangedIndices.Length)
            };
            return job.Schedule(inputDependency);
        }

        private AbsoluteUniversePosition ResolvePhysicsCullingCameraAup(in AbsoluteUniversePosition playerAup, ref float3 cameraForward)
        {
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            Camera camera = runtimeContext != null ? runtimeContext.PlayerCamera : null;
            if (camera == null)
                return playerAup;

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            if (IsFinite(forward))
                cameraForward = NormalizeWithRsqrtGuard(new float3(forward.x, forward.y, forward.z), cameraForward);

            return IsFinite(position) && TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition cameraAup)
                ? cameraAup
                : playerAup;
        }

        private bool TryResolvePhysicsCullingFrustumPlanes(
            in AbsoluteUniversePosition cameraAup,
            out float4 plane0,
            out float4 plane1,
            out float4 plane2,
            out float4 plane3,
            out float4 plane4,
            out float4 plane5)
        {
            plane0 = plane1 = plane2 = plane3 = plane4 = plane5 = default;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            Camera camera = runtimeContext != null ? runtimeContext.PlayerCamera : null;
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, _physicsFrustumPlaneScratch);
            Vector3 cameraPosition = camera.transform.position;
            if (!IsFinite(cameraPosition))
                return false;

            plane0 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[0], cameraPosition);
            plane1 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[1], cameraPosition);
            plane2 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[2], cameraPosition);
            plane3 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[3], cameraPosition);
            plane4 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[4], cameraPosition);
            plane5 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[5], cameraPosition);
            return math.all(math.isfinite(plane0)) &&
                math.all(math.isfinite(plane1)) &&
                math.all(math.isfinite(plane2)) &&
                math.all(math.isfinite(plane3)) &&
                math.all(math.isfinite(plane4)) &&
                math.all(math.isfinite(plane5));
        }

        private static float4 ConvertPlaneToCameraRelative(Plane plane, Vector3 cameraPosition)
        {
            Vector3 normal = plane.normal;
            float localDistance = plane.distance + Vector3.Dot(normal, cameraPosition);
            return new float4(normal.x, normal.y, normal.z, localDistance);
        }

        private static float ResolvePhysicsCullingHardwareRadiusSqScale()
        {
            return ResolvePhysicsCullingHardwareRadiusSqScale(ResolvePhysicsCullingQualityWeight01());
        }

        private static float ResolvePhysicsCullingHardwareRadiusSqScale(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float smooth = q * q * (3f - 2f * q);
            float radiusScale = math.lerp(0.5f, 1.5f, smooth);
            return radiusScale * radiusScale;
        }

        private static double ResolveColliderLodCompoundToSimpleDistanceSq(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float smooth = q * q * (3f - 2f * q);
            float meters = math.lerp(20f, ColliderLodCompoundToSimpleDistanceMeters, smooth);
            return (double)meters * meters;
        }

        private static double ResolveColliderLodSimpleToCompoundDistanceSq(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float smooth = q * q * (3f - 2f * q);
            float compoundMeters = math.lerp(20f, ColliderLodCompoundToSimpleDistanceMeters, smooth);
            float restoreGapMeters = math.lerp(4f, ColliderLodCompoundToSimpleDistanceMeters - ColliderLodSimpleToCompoundDistanceMeters, smooth);
            float meters = math.max(4f, compoundMeters - restoreGapMeters);
            return (double)meters * meters;
        }

        private static float ResolvePhysicsCullingQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
        }

        private static uint ComputePhysicsCullingBodyTelemetryHash(
            int frame,
            ulong entityId,
            float distanceSq,
            byte command,
            byte awakeResult)
        {
            uint hash = 2166136261u;
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)frame));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)entityId));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)(entityId >> 32)));
            hash = MixPhysicsCullingTelemetryHash(hash, math.asuint(distanceSq));
            hash = MixPhysicsCullingTelemetryHash(hash, command);
            return MixPhysicsCullingTelemetryHash(hash, awakeResult);
        }

        private static uint ComputePhysicsCullingFrameTelemetryHash(
            int frame,
            int trackedBodies,
            int activeBodies,
            int asleepBodies,
            int changedIndices,
            int lockContentions,
            float jobMicroseconds,
            float stateSyncMicroseconds,
            float qualityWeight)
        {
            uint hash = 2166136261u;
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)frame));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)trackedBodies));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)activeBodies));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)asleepBodies));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)changedIndices));
            hash = MixPhysicsCullingTelemetryHash(hash, unchecked((uint)lockContentions));
            hash = MixPhysicsCullingTelemetryHash(hash, math.asuint(jobMicroseconds));
            hash = MixPhysicsCullingTelemetryHash(hash, math.asuint(stateSyncMicroseconds));
            return MixPhysicsCullingTelemetryHash(hash, math.asuint(qualityWeight));
        }

        private static uint MixPhysicsCullingTelemetryHash(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private float ResolvePhysicsCullingHysteresisSeconds()
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            return math.max(0.1f, tuning.HysteresisDelaySeconds);
        }

        private float ResolvePhysicsCullingFrustumInnerSphereSq()
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            float radius = math.max(PhysicsCullingFrustumInnerSphereRadiusMeters, tuning.FrustumClampDistanceMeters * 0.1f);
            return radius * radius;
        }

        private int CountCulledTrackedBodies(out int activeBodies, out int asleepBodies)
        {
            int culled = 0;
            int active = 0;
            int asleep = 0;
            for (int i = 0; i < _trackedBodyCount; i++)
            {
                RigidbodyState bodyState = _bodyStates[i];
                bool isCulled = bodyState.DistanceSleepActive != 0 ||
                    bodyState.DistanceKinematicSleepActive != 0 ||
                    bodyState.MeshColliderStripActive != 0;
                if (isCulled)
                    culled++;
                if (bodyState.DistanceSleepActive != 0)
                    asleep++;
                else
                    active++;
            }

            activeBodies = active;
            asleepBodies = asleep;
            return culled;
        }

        private void FreezeBodyVelocityForDistanceSleep(int bodyIndex, Rigidbody body)
        {
            if (body == null || body.isKinematic || (uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsFrozenVelocities.IsCreated)
                return;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            if (frozen.HasVelocity == 0)
            {
                Vector3 linearVelocity = body.linearVelocity;
                Vector3 angularVelocity = body.angularVelocity;
                frozen.LinearVelocity = IsFinite(linearVelocity) ? new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z) : float3.zero;
                frozen.AngularVelocity = IsFinite(angularVelocity) ? new float3(angularVelocity.x, angularVelocity.y, angularVelocity.z) : float3.zero;
                frozen.HasVelocity = 1;
                _physicsFrozenVelocities[bodyIndex] = frozen;
            }

            PhysicsForceRouter.QueueLinearVelocitySet(body, Vector3.zero, wake: false);
            PhysicsForceRouter.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
        }

        private void RestoreFrozenVelocityForDistanceSleep(int bodyIndex, Rigidbody body)
        {
            if (body == null || body.isKinematic || (uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsFrozenVelocities.IsCreated)
                return;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            if (frozen.HasVelocity == 0)
                return;

            if (math.all(math.isfinite(frozen.LinearVelocity)))
            {
                PhysicsForceRouter.QueueLinearVelocitySet(
                    body,
                    new Vector3(frozen.LinearVelocity.x, frozen.LinearVelocity.y, frozen.LinearVelocity.z));
            }

            if (math.all(math.isfinite(frozen.AngularVelocity)))
            {
                PhysicsForceRouter.QueueAngularVelocitySet(
                    body,
                    new Vector3(frozen.AngularVelocity.x, frozen.AngularVelocity.y, frozen.AngularVelocity.z));
            }

            _physicsFrozenVelocities[bodyIndex] = default;
        }

        private byte CacheSleepCollidersForBody(Rigidbody body, int bodyIndex)
        {
            if (body == null || (uint)bodyIndex >= (uint)MaxTrackedBodies)
                return 0;

            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                int slot = baseIndex + i;
                _trackedSleepColliders[slot] = null;
                _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
            }

            if (!TryResolvePhysicsCullingColliderCache(body, out IPhysicsCullingColliderCache colliderCache) ||
                !colliderCache.TryGetPhysicsCullingColliders(out Collider[] colliders, out int colliderCount))
            {
                return 0;
            }

            int readCount = colliders != null ? math.min(colliderCount, colliders.Length) : 0;
            int count = 0;
            for (int i = 0; i < readCount && count < MaxSleepCollidersPerBody; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider is MeshCollider)
                    continue;

                _trackedSleepColliders[baseIndex + count] = collider;
                count++;
            }

            return (byte)count;
        }

        private static bool TryResolvePhysicsCullingColliderCache(Rigidbody body, out IPhysicsCullingColliderCache colliderCache)
        {
            colliderCache = null;
            if (body == null)
                return false;

            if (body.TryGetComponent(out colliderCache) && colliderCache != null)
                return true;

            Transform bodyTransform = body.transform;
            return bodyTransform != null &&
                   TryResolveComponentInParents(bodyTransform.parent, out colliderCache) &&
                   colliderCache != null;
        }

        private void DisableSleepColliders(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.SleepColliderCount == 0 || bodyState.CollidersDisabledByDistanceSleep != 0)
                return;

            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            int count = math.min((int)bodyState.SleepColliderCount, MaxSleepCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int slot = baseIndex + i;
                Collider collider = _trackedSleepColliders[slot];
                if (collider == null)
                {
                    _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
                    continue;
                }

                bool wasEnabled = collider.enabled;
                _trackedSleepColliderEnabledBeforeSleep[slot] = wasEnabled ? (byte)1 : (byte)0;
                if (wasEnabled)
                {
                    collider.enabled = false;
                    RecordPhysicsColliderToggleTransition();
                }
            }

            bodyState.CollidersDisabledByDistanceSleep = 1;
        }

        private void RestoreSleepColliders(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.CollidersDisabledByDistanceSleep == 0)
                return;

            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            int count = math.min((int)bodyState.SleepColliderCount, MaxSleepCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int slot = baseIndex + i;
                Collider collider = _trackedSleepColliders[slot];
                if (collider != null)
                {
                    bool shouldEnable = _trackedSleepColliderEnabledBeforeSleep[slot] != 0;
                    if (collider.enabled != shouldEnable)
                    {
                        collider.enabled = shouldEnable;
                        RecordPhysicsColliderToggleTransition();
                    }
                }

                _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
            }

            bodyState.CollidersDisabledByDistanceSleep = 0;
        }

        private void RecordPhysicsColliderToggleTransition()
        {
            if (_physicsCullingColliderToggleTransitionsThisFrame < int.MaxValue)
                _physicsCullingColliderToggleTransitionsThisFrame++;
        }

        private void RecordPhysicsColliderToggleTransitions(int transitionCount)
        {
            if (transitionCount <= 0 || _physicsCullingColliderToggleTransitionsThisFrame >= int.MaxValue)
                return;

            int remaining = int.MaxValue - _physicsCullingColliderToggleTransitionsThisFrame;
            _physicsCullingColliderToggleTransitionsThisFrame += math.min(transitionCount, remaining);
        }

        private void MoveSleepColliderRefs(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
                return;

            int fromBase = fromIndex * MaxSleepCollidersPerBody;
            int toBase = toIndex * MaxSleepCollidersPerBody;
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                _trackedSleepColliders[toBase + i] = _trackedSleepColliders[fromBase + i];
                _trackedSleepColliderEnabledBeforeSleep[toBase + i] = _trackedSleepColliderEnabledBeforeSleep[fromBase + i];
            }
        }

        private void ClearSleepColliderRefs(int bodyIndex)
        {
            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                _trackedSleepColliders[baseIndex + i] = null;
                _trackedSleepColliderEnabledBeforeSleep[baseIndex + i] = 0;
            }
        }

        private void MovePhysicsCullingDtoLane(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || !_physicsCullingDtos.IsCreated)
                return;

            _physicsCullingDtos[toIndex] = _physicsCullingDtos[fromIndex];
            _physicsFrozenVelocities[toIndex] = _physicsFrozenVelocities[fromIndex];
            _physicsCullingStateAges[toIndex] = _physicsCullingStateAges[fromIndex];
            _physicsCullingDtos[fromIndex] = default;
            _physicsFrozenVelocities[fromIndex] = default;
            _physicsCullingStateAges[fromIndex] = default;
            MarkPhysicsCullingSpatialHashDirty();
        }

        private bool TryAcquirePhysicsTrackedBodyLaneMutationLocks1337()
        {
            if (!TryAcquirePhysicsMutationGuard(PhysicsTrackedBodyLaneMutationGuardMask1337))
                return false;

            if (_lastValidPositions.HasValidView() &&
                _rigidbodyAUPs.HasValidView() &&
                _physicsCullingDtos.HasValidView() &&
                _physicsFrozenVelocities.HasValidView() &&
                _physicsCullingStateAges.HasValidView())
            {
                return true;
            }

            ReleasePhysicsMutationGuard(PhysicsTrackedBodyLaneMutationGuardMask1337);
            return false;
        }

        private void ReleasePhysicsTrackedBodyLaneMutationLocks1337()
        {
            ReleasePhysicsMutationGuard(PhysicsTrackedBodyLaneMutationGuardMask1337);
        }

        private bool TryAcquirePhysicsTargetWakeFlushLocks1337()
        {
            if (!TryAcquirePhysicsMutationGuard(PhysicsTargetWakeMutationGuardMask1337))
                return false;

            if (_physicsWakeRequestMirror.HasValidView() &&
                _physicsTargetWakeRequestCount.HasValidView())
            {
                return true;
            }

            ReleasePhysicsMutationGuard(PhysicsTargetWakeMutationGuardMask1337);
            return false;
        }

        private void ReleasePhysicsTargetWakeFlushLocks1337()
        {
            ReleasePhysicsMutationGuard(PhysicsTargetWakeMutationGuardMask1337);
        }

        public void QueueTargetedPhysicsWakeRequest(in PhysicsCullingTargetWakeRequestSignal request)
        {
            if (!_physicsWakeRequestMirror.IsCreated || !_physicsTargetWakeRequestCount.IsCreated)
                return;

            if (!TryAcquirePhysicsTargetWakeFlushLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                if (!_physicsWakeRequestMirror.TryResolve(out NativeArray<PhysicsCullingTargetWakeRequestSignal> wakeMirror) ||
                    !_physicsTargetWakeRequestCount.TryResolve(out NativeArray<PhysicsCullingCounter64> wakeCounter) ||
                    wakeCounter.Length <= 0)
                {
                    _physicsCullingLockContentionsThisFrame++;
                    return;
                }

                PhysicsCullingCounter64 counter = wakeCounter[0];
                int capacity = math.min(wakeMirror.Length, PhysicsCullingTargetWakeQueueCapacity);
                int writeIndex = counter.Value;
                if (capacity <= 0 || writeIndex < 0)
                {
                    counter.Flags |= 1u;
                    counter.Value = 0;
                    wakeCounter[0] = counter;
                    _physicsCullingLockContentionsThisFrame++;
                    return;
                }

                if (writeIndex >= capacity)
                {
                    counter.Flags |= 1u;
                    counter.Value = capacity;
                    wakeCounter[0] = counter;
                    _physicsCullingLockContentionsThisFrame++;
                    return;
                }

                wakeMirror[writeIndex] = request;
                counter.Value = writeIndex + 1;
                wakeCounter[0] = counter;
            }
            finally
            {
                ReleasePhysicsTargetWakeFlushLocks1337();
            }
        }

        public bool TryGetPhysicsCullingTuning(out PhysicsCullingTuningDTO tuning)
        {
            if (_physicsCullingTuning.IsCreated && _physicsCullingTuningInitialized)
            {
                tuning = _physicsCullingTuning[0];
                return true;
            }

            tuning = default;
            return false;
        }

        public void SetPhysicsCullingTuning(in PhysicsCullingTuningDTO tuning)
        {
            EnsureNativeState();
            if (!_physicsCullingTuning.IsCreated)
                return;

            if (!_physicsCullingTuning.TryAcquireWriteLock(out _))
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                _physicsCullingTuning[0] = new PhysicsCullingTuningDTO
                {
                    DebrisWakeRadiusMeters = math.clamp(tuning.DebrisWakeRadiusMeters, 1f, 500f),
                    VehicleWakeRadiusMeters = math.clamp(tuning.VehicleWakeRadiusMeters, 1f, 1000f),
                    FrustumClampDistanceMeters = math.clamp(tuning.FrustumClampDistanceMeters, 20f, 1000f),
                    HysteresisDelaySeconds = math.clamp(tuning.HysteresisDelaySeconds, 0.1f, 10f),
                    SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                    MockShockwaveRadiusMeters = math.clamp(tuning.MockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters),
                    Flags = tuning.Flags
                };
                _physicsCullingTuningInitialized = true;
            }
            finally
            {
                _physicsCullingTuning.ReleaseWriteLock();
            }
        }

        public int PhysicsCullingDebugBodyCount
        {
            get
            {
                int mockEnd = _trackedBodyCount + _physicsCullingMockBodyCount;
                return math.min(MaxTrackedBodies, math.max(_trackedBodyCount, mockEnd));
            }
        }

        public bool TryGetPhysicsCullingDebugBody(int index, out PhysicsCullingDebugBody debugBody)
        {
            debugBody = default;
            if ((uint)index >= (uint)PhysicsCullingDebugBodyCount || !_physicsCullingDtos.IsCreated)
                return false;

            PhysicsCullingDTO dto = _physicsCullingDtos[index];
            if (dto.InstanceId == 0 && index >= _trackedBodyCount)
                return false;

            debugBody = new PhysicsCullingDebugBody
            {
                Aup = dto.AUP,
                AgeSeconds = _physicsCullingStateAges.IsCreated ? _physicsCullingStateAges[index] : 0f,
                InstanceId = dto.InstanceId,
                IsAsleep = dto.IsAsleep,
                IsHysteresisLocked = _physicsCullingStateAges.IsCreated &&
                    _physicsCullingStateAges[index] < ResolvePhysicsCullingHysteresisSeconds() ? (byte)1 : (byte)0
            };
            return true;
        }

        private void FlushPhysicsTargetWakeRequests()
        {
            if (!_physicsWakeRequestMirror.IsCreated || !_physicsTargetWakeRequestCount.IsCreated)
                return;

            if (!TryAcquirePhysicsTargetWakeFlushLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            int requestCount = 0;
            try
            {
                PhysicsCullingCounter64 counter = _physicsTargetWakeRequestCount[0];
                int queueCapacity = math.max(0, math.min(_physicsWakeRequestMirror.Length, PhysicsCullingTargetWakeQueueCapacity));
                requestCount = math.clamp(counter.Value, 0, queueCapacity);
                for (int i = 0; i < requestCount; i++)
                {
                    _physicsTargetWakeApplyScratch[i] = _physicsWakeRequestMirror[i];
                    _physicsWakeRequestMirror[i] = default;
                }

                _physicsTargetWakeRequestCount[0] = default;
            }
            finally
            {
                ReleasePhysicsTargetWakeFlushLocks1337();
            }

            for (int i = 0; i < requestCount; i++)
            {
                PhysicsCullingTargetWakeRequestSignal request = _physicsTargetWakeApplyScratch[i];
                ProcessTargetedPhysicsWakeRequest(in request);
                _physicsTargetWakeApplyScratch[i] = default;
            }
        }

        private void ClearPhysicsTargetWakeRequests()
        {
            if (_physicsTargetWakeRequestCount.IsCreated)
                _physicsTargetWakeRequestCount[0] = default;

            if (!_physicsWakeRequestMirror.IsCreated)
                return;

            int requestCount = math.min(_physicsWakeRequestMirror.Length, PhysicsCullingTargetWakeQueueCapacity);
            for (int i = 0; i < requestCount; i++)
                _physicsWakeRequestMirror[i] = default;
        }

        private void ProcessTargetedPhysicsWakeRequest(in PhysicsCullingTargetWakeRequestSignal request)
        {
            int instanceId = unchecked((int)request.TargetInstanceId);
            if (!_trackedBodyIndexByInstanceId.TryGetValue(instanceId, out int bodyIndex) ||
                (uint)bodyIndex >= (uint)_trackedBodyCount)
            {
                return;
            }

            float3 impulse = request.ImpulseVector;
            if (!math.all(math.isfinite(impulse)))
                impulse = float3.zero;

            Rigidbody body = _trackedBodies[bodyIndex];
            if (body == null)
                return;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            bool cullingActive = bodyState.DistanceSleepActive != 0 ||
                bodyState.DistanceKinematicSleepActive != 0 ||
                bodyState.MeshColliderStripActive != 0;
            if (!cullingActive)
                return;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            frozen.LinearVelocity += impulse;
            frozen.HasVelocity = 1;
            _physicsFrozenVelocities[bodyIndex] = frozen;

            RestoreAllPhysicsCullingState(bodyIndex, body, ref bodyState, forceWake: true);
            _bodyStates[bodyIndex] = bodyState;
            _physicsCullingStateAges[bodyIndex] = 0f;
        }

        public int GenerateMockPhysicsBodies(int count = PhysicsCullingMockBodiesPerGenerate)
        {
            EnsureNativeState();
            int available = MaxTrackedBodies - _trackedBodyCount;
            int mockCount = math.clamp(count, 0, available);
            if (mockCount <= 0)
                return 0;

            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            double3 baseAup = default;
            if (TryResolvePhysicsCullingPlayerState(out AbsoluteUniversePosition playerAup, out _, out _))
                baseAup = playerAup.ToAbsoluteDouble3();

            if (!TryAcquirePhysicsMockBodyGenerationLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return 0;
            }

            try
            {
                for (int i = 0; i < mockCount; i++)
                {
                    uint h = HashMockPhysicsBody((uint)(i + 1));
                    float x = ((int)(h & 255u) - 128) * 4f;
                    float z = ((int)((h >> 8) & 255u) - 128) * 4f;
                    float y = ((int)((h >> 16) & 31u) - 16) * 2f;
                    int bodyIndex = _trackedBodyCount + i;
                    _physicsCullingDtos[bodyIndex] = new PhysicsCullingDTO
                    {
                        AUP = baseAup + new double3(x, y, z),
                        InstanceId = -1000000 - i,
                        ActivationRadiusSq = PhysicsCullingDefaultDebrisWakeRadiusMeters * PhysicsCullingDefaultDebrisWakeRadiusMeters,
                        IsAsleep = 0,
                        CullingFlags = 0u
                    };
                    _physicsCullingStateAges[bodyIndex] = ResolvePhysicsCullingHysteresisSeconds();
                    _physicsFrozenVelocities[bodyIndex] = default;
                    _rigidbodyCullingStateSnapshot[bodyIndex] = 0;
                }

                _physicsCullingMockBodyCount = mockCount;
                MarkPhysicsCullingSpatialHashDirty();
                return mockCount;
            }
            finally
            {
                ReleasePhysicsMockBodyGenerationLocks1337();
            }
        }

        private bool TryAcquirePhysicsMockBodyGenerationLocks1337()
        {
            if (!TryAcquirePhysicsMutationGuard(PhysicsMockBodyGenerationMutationGuardMask1337))
                return false;

            if (_physicsCullingDtos.HasValidView() &&
                _physicsFrozenVelocities.HasValidView() &&
                _physicsCullingStateAges.HasValidView() &&
                _rigidbodyCullingStateSnapshot.HasValidView())
            {
                return true;
            }

            ReleasePhysicsMutationGuard(PhysicsMockBodyGenerationMutationGuardMask1337);
            return false;
        }

        private void ReleasePhysicsMockBodyGenerationLocks1337()
        {
            ReleasePhysicsMutationGuard(PhysicsMockBodyGenerationMutationGuardMask1337);
        }

        private static uint HashMockPhysicsBody(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        public void FireMockSeismicShockwave(uint seed)
        {
            EnsureNativeState();
            if (!_physicsMockSeismicSignals.IsCreated || !_physicsCullingDtos.IsCreated)
                return;

            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            AbsoluteUniversePosition playerAup = default;
            if (!TryResolvePhysicsCullingPlayerState(out playerAup, out _, out _))
                return;

            uint h = HashMockPhysicsBody(seed == 0u ? 1u : seed);
            double3 jitter = new double3(
                ((int)(h & 63u) - 32) * 2.0,
                0.0,
                ((int)((h >> 6) & 63u) - 32) * 2.0);
            TryQueuePhysicsCullingWakeRegion(
                playerAup.ToAbsoluteDouble3() + jitter,
                math.clamp(tuning.MockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters),
                seed);
        }

        private bool TryQueuePhysicsCullingWakeRegion(
            in AbsoluteUniversePosition originAup,
            float radiusMeters,
            uint sourceHash = PhysicsCullingWakeRegionSourceExternal)
        {
            return IsFinite(in originAup) &&
                TryQueuePhysicsCullingWakeRegion(originAup.ToAbsoluteDouble3(), radiusMeters, sourceHash);
        }

        private bool TryQueuePhysicsCullingWakeRegion(double3 epicenterAup, float radiusMeters, uint sourceHash)
        {
            if (HectonFloatingOrigin.IsShiftInProgress ||
                !_physicsMockSeismicSignals.IsCreated ||
                !math.all(math.isfinite(epicenterAup)) ||
                radiusMeters <= 0f ||
                !math.isfinite(radiusMeters))
            {
                return false;
            }

            if (!_physicsMockSeismicSignals.TryAcquireWriteLock(out NativeArray<MockSeismicShockwaveSignal> wakeSignals))
            {
                _physicsCullingLockContentionsThisFrame++;
                return false;
            }

            try
            {
                int capacity = math.min(wakeSignals.Length, PhysicsCullingMockSeismicSignalCapacity);
                int writeIndex = _physicsMockSeismicPending;
                if ((uint)writeIndex >= (uint)capacity)
                {
                    _physicsCullingLockContentionsThisFrame++;
                    return false;
                }

                wakeSignals[writeIndex] = new MockSeismicShockwaveSignal
                {
                    EpicenterAup = epicenterAup,
                    RadiusMeters = math.min(radiusMeters, ImpactWakeMaximumRadiusMeters),
                    Seed = sourceHash,
                    Frame = ResolvePhysicsCullingSimulationFrame(),
                    Fire = 1
                };
                _physicsMockSeismicPending = (byte)(writeIndex + 1);
                return true;
            }
            finally
            {
                _physicsMockSeismicSignals.ReleaseWriteLock();
            }
        }

        private bool TrySchedulePendingMockSeismicShockwave(int jobCount)
        {
            if (_physicsMockSeismicPending == 0)
                return false;

            if (!_physicsMockSeismicSignals.IsCreated ||
                _physicsMockSeismicSignals.Length <= 0 ||
                !_physicsCullingDtos.IsCreated ||
                jobCount <= 0)
            {
                _physicsMockSeismicPending = 0;
                return false;
            }

            int pendingCount = math.min(
                _physicsMockSeismicPending,
                math.min(_physicsMockSeismicSignals.Length, PhysicsCullingMockSeismicSignalCapacity));
            _physicsMockSeismicPending = 0;
            MockSeismicShockwaveSignal signal0 = ReadPendingPhysicsWakeRegionSignal(0, pendingCount);
            MockSeismicShockwaveSignal signal1 = ReadPendingPhysicsWakeRegionSignal(1, pendingCount);
            MockSeismicShockwaveSignal signal2 = ReadPendingPhysicsWakeRegionSignal(2, pendingCount);
            MockSeismicShockwaveSignal signal3 = ReadPendingPhysicsWakeRegionSignal(3, pendingCount);
            MockSeismicShockwaveSignal signal4 = ReadPendingPhysicsWakeRegionSignal(4, pendingCount);
            MockSeismicShockwaveSignal signal5 = ReadPendingPhysicsWakeRegionSignal(5, pendingCount);
            MockSeismicShockwaveSignal signal6 = ReadPendingPhysicsWakeRegionSignal(6, pendingCount);
            MockSeismicShockwaveSignal signal7 = ReadPendingPhysicsWakeRegionSignal(7, pendingCount);
            MockSeismicShockwaveSignal signal8 = ReadPendingPhysicsWakeRegionSignal(8, pendingCount);
            MockSeismicShockwaveSignal signal9 = ReadPendingPhysicsWakeRegionSignal(9, pendingCount);
            MockSeismicShockwaveSignal signal10 = ReadPendingPhysicsWakeRegionSignal(10, pendingCount);
            MockSeismicShockwaveSignal signal11 = ReadPendingPhysicsWakeRegionSignal(11, pendingCount);
            MockSeismicShockwaveSignal signal12 = ReadPendingPhysicsWakeRegionSignal(12, pendingCount);
            MockSeismicShockwaveSignal signal13 = ReadPendingPhysicsWakeRegionSignal(13, pendingCount);
            MockSeismicShockwaveSignal signal14 = ReadPendingPhysicsWakeRegionSignal(14, pendingCount);
            MockSeismicShockwaveSignal signal15 = ReadPendingPhysicsWakeRegionSignal(15, pendingCount);
            for (int i = 0; i < pendingCount; i++)
                _physicsMockSeismicSignals[i] = default;

            bool hasSignal = (signal0.Fire != 0) |
                (signal1.Fire != 0) |
                (signal2.Fire != 0) |
                (signal3.Fire != 0) |
                (signal4.Fire != 0) |
                (signal5.Fire != 0) |
                (signal6.Fire != 0) |
                (signal7.Fire != 0) |
                (signal8.Fire != 0) |
                (signal9.Fire != 0) |
                (signal10.Fire != 0) |
                (signal11.Fire != 0) |
                (signal12.Fire != 0) |
                (signal13.Fire != 0) |
                (signal14.Fire != 0) |
                (signal15.Fire != 0);
            if (!hasSignal)
                return false;

            JobHandle clearHandle = SchedulePhysicsChangedIndexClear(jobCount, default);
            MockSeismicShockwaveWakeJob job = new MockSeismicShockwaveWakeJob
            {
                Dtos = _physicsCullingDtos,
                AwakeResults = _rigidbodyAwakeResults,
                CommandResults = _rigidbodyCullingCommandResults,
                StateAges = _physicsCullingStateAges,
                Signal0 = signal0,
                Signal1 = signal1,
                Signal2 = signal2,
                Signal3 = signal3,
                Signal4 = signal4,
                Signal5 = signal5,
                Signal6 = signal6,
                Signal7 = signal7,
                Signal8 = signal8,
                Signal9 = signal9,
                Signal10 = signal10,
                Signal11 = signal11,
                Signal12 = signal12,
                Signal13 = signal13,
                Signal14 = signal14,
                Signal15 = signal15,
                ChangedIndices = _physicsStateChangedIndices
            };

            _physicsCullingJobCount = jobCount;
            _physicsCullingJobDiscardRequested = false;
            _physicsCullingJobScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            JobHandle wakeHandle = job.Schedule(jobCount, 64, clearHandle);
            _physicsCullingJobHandle = SchedulePhysicsChangedIndexCompaction(jobCount, wakeHandle);
            _physicsCullingJobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, _physicsCullingJobHandle);
            JobHandle.ScheduleBatchedJobs();
            return true;
        }

        private MockSeismicShockwaveSignal ReadPendingPhysicsWakeRegionSignal(int index, int pendingCount)
        {
            return (uint)index < (uint)pendingCount && (uint)index < (uint)_physicsMockSeismicSignals.Length
                ? _physicsMockSeismicSignals[index]
                : default;
        }

        private void RecordShinobu37PhysicsCullingFrameTelemetry(float stateSyncTimeMs, int changedIndices)
        {
            CountCulledTrackedBodies(out int activeBodies, out int asleepBodies);
            RecordShinobu37PhysicsCullingFrameTelemetry(stateSyncTimeMs, changedIndices, activeBodies, asleepBodies);
        }

        private void RecordShinobu37PhysicsCullingFrameTelemetry(float stateSyncTimeMs, int changedIndices, int activeBodies, int asleepBodies)
        {
            if (!_physicsCullingFrameTelemetry.IsCreated)
                return;

            float safeStateSyncTimeMs = math.isfinite(stateSyncTimeMs) ? stateSyncTimeMs : 0f;
            float safeStateSyncMicroseconds = safeStateSyncTimeMs * 1000f;
            float safeJobMicroseconds = math.isfinite(_physicsCullingLastJobMicroseconds)
                ? math.max(0f, _physicsCullingLastJobMicroseconds)
                : 0f;
            float qualityWeight = ResolvePhysicsCullingQualityWeight01();
            float radiusSqScale = ResolvePhysicsCullingHardwareRadiusSqScale(qualityWeight);
            int lockContentions = _physicsCullingLockContentionsThisFrame;
            int colliderToggleTransitions = _physicsCullingColliderToggleTransitionsThisFrame;

            if (!_physicsCullingFrameTelemetry.TryAcquireWriteLock(out NativeArray<PhysicsCullingFrameTelemetry> frameTelemetry))
            {
                _physicsCullingLockContentionsThisFrame = lockContentions < int.MaxValue ? lockContentions + 1 : lockContentions;
                return;
            }

            try
            {
                if (!frameTelemetry.IsCreated)
                    return;

                int capacity = math.min(frameTelemetry.Length, PhysicsCullingFrameTelemetryCapacity);
                if (capacity <= 0)
                    return;

                uint frame = AdvancePhysicsCullingSimulationFrame();
                int index = _physicsCullingFrameTelemetryWriteIndex;
                if ((uint)index >= (uint)capacity)
                    index = 0;

                uint stateHash = ComputePhysicsCullingFrameTelemetryHash(
                    unchecked((int)frame),
                    _trackedBodyCount,
                    activeBodies,
                    asleepBodies,
                    changedIndices,
                    lockContentions,
                    safeJobMicroseconds,
                    safeStateSyncMicroseconds,
                    qualityWeight);

                frameTelemetry[index] = new PhysicsCullingFrameTelemetry
                {
                    FrameIndex = unchecked((int)frame),
                    TotalTrackedBodies = _trackedBodyCount,
                    ActiveBodies = activeBodies,
                    AsleepBodies = asleepBodies,
                    StateSyncTimeMs = safeStateSyncTimeMs,
                    StateSyncMicroseconds = safeStateSyncMicroseconds,
                    JobMicroseconds = safeJobMicroseconds,
                    GlobalQualityWeight = qualityWeight,
                    ChangedIndices = changedIndices,
                    LockContentions = lockContentions,
                    Flags = (_physicsCullingMockBodyCount > 0 ? PhysicsCullingFrameTelemetryMockBodiesFlag : 0u) |
                            (colliderToggleTransitions > 0 ? PhysicsCullingFrameTelemetryColliderTransitionsFlag : 0u),
                    StateHash = stateHash,
                    RadiusSqScale = radiusSqScale,
                    FrameHash = stateHash ^ unchecked(frame * 16777619u),
                    Reserved0 = unchecked((uint)colliderToggleTransitions)
                };

                _physicsCullingLastJobMicroseconds = 0f;
                _physicsCullingLockContentionsThisFrame = 0;
                _physicsCullingColliderToggleTransitionsThisFrame = 0;
                int next = index + 1;
                _physicsCullingFrameTelemetryWriteIndex = next >= capacity ? 0 : next;
            }
            finally
            {
                _physicsCullingFrameTelemetry.ReleaseWriteLock();
            }
        }

        private uint AdvancePhysicsCullingSimulationFrame()
        {
            uint next = _physicsCullingSimulationFrame + 1u;
            if (next == 0u)
                next = 1u;

            _physicsCullingSimulationFrame = next;
            return next;
        }

        private uint ResolvePhysicsCullingSimulationFrame()
        {
            return _physicsCullingSimulationFrame != 0u ? _physicsCullingSimulationFrame : 1u;
        }

        internal static bool ValidatePhysicsCullingPrivateTelemetryLayout1337()
        {
            return UnsafeUtility.SizeOf<PhysicsCullingTelemetryEntry>() == PhysicsCullingLayout1337.BodyTelemetryStrideBytes &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.FrameIndex)) == 0 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.LockContentions)) == 12 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.BodyId)) == 16 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.StateHash)) == 20 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.JobMicroseconds)) == 28 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.GlobalQualityWeight)) == 36 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.CullingFlags)) == 40 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.FrameHash)) == 44 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.Reserved0)) == 48 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.CcdInterventions)) == 52 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.Command)) == 54 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.AwakeResult)) == 55 &&
                OffsetOfPhysicsCullingPrivateTelemetry(nameof(PhysicsCullingTelemetryEntry.Flags)) == 56;
        }

        internal static bool ValidatePhysicsImpactEventLayout1337()
        {
            return UnsafeUtility.SizeOf<PhysicsImpactEventData>() == 112 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.PointAup)) == 0 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.PrimaryBodyId)) == 48 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.SecondaryBodyId)) == 56 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.Point)) == 64 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.Normal)) == 76 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.Force)) == 88 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.Intensity)) == 92 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.MassVelocity)) == 96 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.WeightClass)) == 100 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.PrimaryAudioMaterialId)) == 101 &&
                OffsetOfPhysicsImpactEvent(nameof(PhysicsImpactEventData.SecondaryAudioMaterialId)) == 102;
        }

        private static int OffsetOfPhysicsCullingPrivateTelemetry(string fieldName)
        {
            FieldInfo field = typeof(PhysicsCullingTelemetryEntry).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static int OffsetOfPhysicsImpactEvent(string fieldName)
        {
            FieldInfo field = typeof(PhysicsImpactEventData).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private void TryDumpPhysicsCullingBlackBoxToFile(uint reasonHash, float safeScalar)
        {
            if (reasonHash == 0u)
                return;

            NativeArray<PhysicsCullingTelemetryEntry> bodyRing = _physicsCullingTelemetry.AsNativeArray();
            NativeArray<PhysicsCullingFrameTelemetry> frameRing = _physicsCullingFrameTelemetry.AsNativeArray();
            if ((!bodyRing.IsCreated || bodyRing.Length <= 0) &&
                (!frameRing.IsCreated || frameRing.Length <= 0))
            {
                return;
            }

            int bodyEntryCount = bodyRing.IsCreated ? math.min(bodyRing.Length, PhysicsCullingTelemetryCapacity) : 0;
            int frameEntryCount = frameRing.IsCreated ? math.min(frameRing.Length, PhysicsCullingFrameTelemetryCapacity) : 0;
            float qualityWeight = ResolvePhysicsCullingQualityWeight01();
            int frameIndex = ResolveCurrentDispatcherFrameIndex();
            uint stateHash = ComputePhysicsCullingFrameTelemetryHash(
                frameIndex,
                _trackedBodyCount,
                0,
                _culledBodyCount,
                0,
                _physicsCullingLockContentionsThisFrame,
                _physicsCullingLastJobMicroseconds,
                0f,
                qualityWeight);

            PhysicsCullingBlackBoxDumpHeader1337 header = new PhysicsCullingBlackBoxDumpHeader1337
            {
                Magic = PhysicsCullingBlackBoxDumpMagic1337,
                Version = PhysicsCullingBlackBoxDumpVersion1337,
                ReasonHash = reasonHash,
                Flags = 0u,
                FrameIndex = frameIndex,
                BodyEntryCount = bodyEntryCount,
                FrameEntryCount = frameEntryCount,
                BodyEntryStride = UnsafeUtility.SizeOf<PhysicsCullingTelemetryEntry>(),
                FrameEntryStride = UnsafeUtility.SizeOf<PhysicsCullingFrameTelemetry>(),
                ScalarValue = safeScalar,
                GlobalQualityWeight = qualityWeight,
                LastJobMicroseconds = _physicsCullingLastJobMicroseconds,
                StateHash = stateHash,
                BodyRingWriteIndex = unchecked((uint)_physicsCullingTelemetryWriteIndex),
                FrameRingWriteIndex = unchecked((uint)_physicsCullingFrameTelemetryWriteIndex)
            };

            int headerBytes = UnsafeUtility.SizeOf<PhysicsCullingBlackBoxDumpHeader1337>();
            int bodyBytes = bodyEntryCount * UnsafeUtility.SizeOf<PhysicsCullingTelemetryEntry>();
            int frameBytes = frameEntryCount * UnsafeUtility.SizeOf<PhysicsCullingFrameTelemetry>();
            long totalBytes = (long)headerBytes + bodyBytes + frameBytes;
            if (totalBytes < headerBytes || totalBytes > int.MaxValue)
                return;

            const string dumpPayloadLabel = "physicsCullingBlackBoxDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)totalBytes,
                    nameof(GlobalPhysicsStateManager),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    UnsafeUtility.CopyStructureToPtr(ref header, destination);

                    int cursor = headerBytes;
                    if (bodyBytes > 0)
                    {
                        UnsafeUtility.MemCpy(
                            destination + cursor,
                            NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bodyRing),
                            bodyBytes);
                        cursor += bodyBytes;
                    }

                    if (frameBytes > 0)
                    {
                        UnsafeUtility.MemCpy(
                            destination + cursor,
                            NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(frameRing),
                            frameBytes);
                    }
                }

                NativeFaultDumpWriter.TryWriteAll(ResolvePhysicsCullingBlackBoxAbsolutePath(), payload, (int)totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(GlobalPhysicsStateManager),
                    dumpPayloadLabel);
            }
        }

        private static string ResolvePhysicsCullingBlackBoxAbsolutePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, PhysicsCullingBlackBoxRelativePath1337);
        }

        private void TickPhysicsCullingCsvOverrideMonitor()
        {
#if UNITY_EDITOR
            _physicsCullingCsvPollAccumulator += PhysicsCullingSlowTickIntervalSeconds;
            if (_physicsCullingCsvPollAccumulator < PhysicsCullingCsvPollIntervalSeconds)
                return;

            _physicsCullingCsvPollAccumulator = 0f;
            string path = ResolvePhysicsCullingCsvAbsolutePath();
            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _physicsCullingCsvLastWriteTicks)
                return;

            Span<byte> scratch = stackalloc byte[PhysicsCullingCsvScratchCapacity];
            int bytesRead;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(scratch);
            }

            if (bytesRead > 0 &&
                TryIngestPhysicsCullingCsv(scratch.Slice(0, math.min(bytesRead, scratch.Length))))
            {
                _physicsCullingCsvLastWriteTicks = ticks;
            }
#endif
        }

#if UNITY_EDITOR
        private string ResolvePhysicsCullingCsvAbsolutePath()
        {
            if (!string.IsNullOrEmpty(_physicsCullingCsvAbsolutePath))
                return _physicsCullingCsvAbsolutePath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _physicsCullingCsvAbsolutePath = Path.Combine(projectRoot, PhysicsCullingProfilesRelativePath);
            return _physicsCullingCsvAbsolutePath;
        }

        public bool TryIngestPhysicsCullingCsv(ReadOnlySpan<byte> csv)
        {
            if (!_physicsCullingTuning.IsCreated)
                return false;

            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            int cursor = 0;
            bool changed = false;
            while (TryReadCsvLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                if (!TryReadCsvKeyValue(line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value))
                    continue;

                if (!TryParseAsciiFloat(value, out float parsedValue))
                    continue;

                uint hash = HashLowerAscii(key);
                if (hash == HashLowerAsciiLiteral("debris_wake_radius"))
                {
                    tuning.DebrisWakeRadiusMeters = math.clamp(parsedValue, 1f, 500f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("vehicle_wake_radius"))
                {
                    tuning.VehicleWakeRadiusMeters = math.clamp(parsedValue, 1f, 1000f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("frustum_clamp_distance"))
                {
                    tuning.FrustumClampDistanceMeters = math.clamp(parsedValue, 20f, 1000f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("hysteresis_delay"))
                {
                    tuning.HysteresisDelaySeconds = math.clamp(parsedValue, 0.1f, 10f);
                    changed = true;
                }
            }

            if (changed)
                _physicsCullingTuning[0] = tuning;

            return changed;
        }

        private static bool TryReadCsvLine(ReadOnlySpan<byte> csv, ref int cursor, out ReadOnlySpan<byte> line)
        {
            int start = cursor;
            while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < csv.Length && csv[cursor] == (byte)'\n')
                cursor++;
            if (end > start && csv[end - 1] == (byte)'\r')
                end--;

            line = TrimAscii(csv.Slice(start, end - start));
            return line.Length > 0;
        }

        private static bool TryReadCsvKeyValue(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            key = default;
            value = default;
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int delimiter = -1;
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=' || b == (byte)';')
                {
                    delimiter = i;
                    break;
                }
            }

            if (delimiter <= 0 || delimiter >= line.Length - 1)
                return false;

            key = TrimAscii(line.Slice(0, delimiter));
            value = TrimAscii(line.Slice(delimiter + 1));
            return key.Length > 0 && value.Length > 0;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool hasDigit = false;
            while (index < value.Length)
            {
                byte b = value[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                result = (result * 10f) + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length)
                {
                    byte b = value[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    result += (b - (byte)'0') * place;
                    place *= 0.1f;
                    hasDigit = true;
                    index++;
                }
            }

            parsed = result * sign;
            return hasDigit && math.isfinite(parsed);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static uint HashLowerAsciiLiteral(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                byte b = (byte)(c >= 'A' && c <= 'Z' ? c + 32 : c);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearPhysicsChangedIndicesJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<int> ChangedIndices;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index < (uint)Count && (uint)index < (uint)ChangedIndices.Length)
                    ChangedIndices[index] = -1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CompactPhysicsChangedIndicesJob : IJob
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // ChangedIndices is first written as an index-addressed sparse marker lane by the distance culling
            // or shockwave jobs, then compacted in this single follow-up IJob after their returned JobHandle
            // dependency completes. Unity cannot infer that phase split from the NativeArray field alone.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A second temporary compact buffer was rejected because it would double memory bandwidth and add
            // another Vault lane for the same fact. In-place compaction preserves the one owner, one route,
            // one proof artifact rule while keeping the memory walk linear.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: no parallel writer touches ChangedIndices while this IJob executes, and the valid
            // prefix is exported only through ChangedCount after compaction. The lane is not read by consumers
            // until the dispatcher observes this job's output handle.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ChangedIndices;
            [WriteOnly, NoAlias] public NativeArray<PhysicsCullingCounter64> ChangedCount;
            public int Count;

            public void Execute()
            {
                int count = math.min(Count, ChangedIndices.Length);
                int write = 0;
                for (int i = 0; i < count; i++)
                {
                    int value = ChangedIndices[i];
                    bool valid = value == i;
                    ChangedIndices[write] = math.select(-1, value, valid);
                    write += math.select(0, 1, valid);
                }

                PhysicsCullingCounter64 counter = default;
                counter.Value = write;
                counter.Flags = 0u;
                ChangedCount[0] = counter;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct MockSeismicShockwaveWakeJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Each Execute index owns exactly one DTO row and matching result rows after the length guards.
            // The job mutates those rows through pointer/ref access so Burst avoids 40-byte DTO defensive
            // copies; Unity's safety layer cannot prove the one-index-to-one-row relation.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A main-thread shockwave pass or GameObject wake broadcast was rejected because it would serialize
            // thousands of culling rows and reintroduce scene authority into the data lane. Keeping wake results
            // in the same parallel pass preserves deterministic batch ownership.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: Dtos, AwakeResults, CommandResults, StateAges, and ChangedIndices are disjoint Vault
            // lanes scheduled only by the physics culling owner. ChangedIndices is sparse and later compacted by
            // CompactPhysicsChangedIndicesJob after this job's handle.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<PhysicsCullingDTO> Dtos;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> AwakeResults;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> CommandResults;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> StateAges;
            [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ChangedIndices;
            public MockSeismicShockwaveSignal Signal0;
            public MockSeismicShockwaveSignal Signal1;
            public MockSeismicShockwaveSignal Signal2;
            public MockSeismicShockwaveSignal Signal3;
            public MockSeismicShockwaveSignal Signal4;
            public MockSeismicShockwaveSignal Signal5;
            public MockSeismicShockwaveSignal Signal6;
            public MockSeismicShockwaveSignal Signal7;
            public MockSeismicShockwaveSignal Signal8;
            public MockSeismicShockwaveSignal Signal9;
            public MockSeismicShockwaveSignal Signal10;
            public MockSeismicShockwaveSignal Signal11;
            public MockSeismicShockwaveSignal Signal12;
            public MockSeismicShockwaveSignal Signal13;
            public MockSeismicShockwaveSignal Signal14;
            public MockSeismicShockwaveSignal Signal15;

            public unsafe void Execute(int index)
            {
                if ((uint)index >= (uint)Dtos.Length ||
                    (uint)index >= (uint)AwakeResults.Length ||
                    (uint)index >= (uint)CommandResults.Length ||
                    (uint)index >= (uint)StateAges.Length ||
                    (uint)index >= (uint)ChangedIndices.Length)
                    return;

                ref PhysicsCullingDTO dto = ref UnsafeUtility.ArrayElementAsRef<PhysicsCullingDTO>(Dtos.GetUnsafePtr(), index);
                bool exempt = (dto.CullingFlags & PhysicsCullingDtoExemptFlag) != 0u;
                bool wake = (!exempt) &
                    (dto.IsAsleep != 0) &
                    (ShouldWakeBySignal(in dto, in Signal0) |
                    ShouldWakeBySignal(in dto, in Signal1) |
                    ShouldWakeBySignal(in dto, in Signal2) |
                    ShouldWakeBySignal(in dto, in Signal3) |
                    ShouldWakeBySignal(in dto, in Signal4) |
                    ShouldWakeBySignal(in dto, in Signal5) |
                    ShouldWakeBySignal(in dto, in Signal6) |
                    ShouldWakeBySignal(in dto, in Signal7) |
                    ShouldWakeBySignal(in dto, in Signal8) |
                    ShouldWakeBySignal(in dto, in Signal9) |
                    ShouldWakeBySignal(in dto, in Signal10) |
                    ShouldWakeBySignal(in dto, in Signal11) |
                    ShouldWakeBySignal(in dto, in Signal12) |
                    ShouldWakeBySignal(in dto, in Signal13) |
                    ShouldWakeBySignal(in dto, in Signal14) |
                    ShouldWakeBySignal(in dto, in Signal15));
                dto.IsAsleep = (byte)math.select((int)dto.IsAsleep, 0, wake);
                AwakeResults[index] = (byte)math.select(0, 1, wake);
                CommandResults[index] = (byte)math.select(0, (int)CullingCommandAwake, wake);
                StateAges[index] = math.select(StateAges[index], 0f, wake);
                ChangedIndices[index] = math.select(-1, index, wake);
            }

            private static bool ShouldWakeBySignal(
                in PhysicsCullingDTO dto,
                in MockSeismicShockwaveSignal signal)
            {
                double3 delta = dto.AUP - signal.EpicenterAup;
                bool deltaFinite = math.all(math.isfinite(delta));
                delta = math.select(default(double3), delta, deltaFinite);
                double radius = math.max(0f, signal.RadiusMeters);
                double radiusSq = radius * radius;
                return (signal.Fire != 0) & deltaFinite & (math.lengthsq(delta) <= radiusSq);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct PhysicsDistanceCullingJobShinobu37 : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // CandidateIndices maps each scheduled lane to one authoritative DTO/result row; the owner builds
            // the candidate list without duplicates before scheduling. Unity safety cannot express that indirect
            // uniqueness proof, so it sees potential parallel writes to the result lanes.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Splitting DTO mutation, command output, age update, and changed-index marking into separate jobs
            // was rejected because it would add repeated AUP/frustum math and extra memory passes. One fused
            // job keeps culling data-local and returns a single dependency to the dispatcher.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: Dtos, AwakeResults, CommandResults, DistanceSqResults, StateAges, and ChangedIndices
            // are separate buffers owned by GlobalPhysicsStateManager. Consumers observe them only after the
            // returned JobHandle and the subsequent compaction handle complete.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<PhysicsCullingDTO> Dtos;
            [ReadOnly, NoAlias] public NativeArray<byte> CurrentStates;
            [ReadOnly, NoAlias] public NativeArray<int> CandidateIndices;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> AwakeResults;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> CommandResults;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> DistanceSqResults;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> StateAges;
            [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ChangedIndices;
            public double3 CameraAbsoluteAup;
            public float3 CameraForward;
            public float KinematicSleepDistanceMeters;
            public float KinematicWakeDistanceMeters;
            public float MeshColliderStripDistanceMeters;
            public float MeshColliderRestoreDistanceMeters;
            public float4 FrustumPlane0;
            public float4 FrustumPlane1;
            public float4 FrustumPlane2;
            public float4 FrustumPlane3;
            public float4 FrustumPlane4;
            public float4 FrustumPlane5;
            public float FrustumInnerSphereSq;
            public float HardwareRadiusSqScale;
            public float HysteresisSeconds;
            public float DeltaTimeSeconds;
            public byte AbyssalDepthCull;
            public byte UseFrustum;

            public unsafe void Execute(int candidateIndex)
            {
                if ((uint)candidateIndex >= (uint)CandidateIndices.Length)
                    return;

                int index = CandidateIndices[candidateIndex];
                if ((uint)index >= (uint)Dtos.Length ||
                    (uint)index >= (uint)CurrentStates.Length ||
                    (uint)index >= (uint)AwakeResults.Length ||
                    (uint)index >= (uint)CommandResults.Length ||
                    (uint)index >= (uint)DistanceSqResults.Length ||
                    (uint)index >= (uint)StateAges.Length ||
                    (uint)index >= (uint)ChangedIndices.Length)
                    return;

                ref PhysicsCullingDTO dto = ref UnsafeUtility.ArrayElementAsRef<PhysicsCullingDTO>(Dtos.GetUnsafePtr(), index);
                byte currentState = CurrentStates[index];
                bool ignoreCulling = ((currentState & CullingStateIgnoreCulling) != 0) |
                    ((dto.CullingFlags & PhysicsCullingDtoExemptFlag) != 0u);
                float age = StateAges[index];
                bool sleepActive = ((currentState & CullingStateSleepActive) != 0) | (dto.IsAsleep != 0);
                bool hysteresisLocked = age < HysteresisSeconds;
                float nextAge = math.min(HysteresisSeconds, age + math.max(0f, DeltaTimeSeconds));

                double3 deltaDouble = dto.AUP - CameraAbsoluteAup;
                bool deltaFinite = math.all(math.isfinite(deltaDouble));
                double3 clampedDeltaDouble = math.clamp(
                    deltaDouble,
                    new double3(-PhysicsCullingLocalDeltaClampMeters),
                    new double3(PhysicsCullingLocalDeltaClampMeters));
                clampedDeltaDouble = math.select(default(double3), clampedDeltaDouble, deltaFinite);
                float3 delta = new float3(
                    (float)clampedDeltaDouble.x,
                    (float)clampedDeltaDouble.y,
                    (float)clampedDeltaDouble.z);
                float rawDistanceSq = math.lengthsq(delta);
                bool distanceFinite = math.isfinite(rawDistanceSq);
                bool invalidInput = (!ignoreCulling) & ((!deltaFinite) | (!distanceFinite));
                bool forceAwake = ignoreCulling | invalidInput;
                float distanceSq = math.select(0f, rawDistanceSq, deltaFinite & distanceFinite);

                DistanceSqResults[index] = math.select(distanceSq, 0f, forceAwake);
                float activationRadiusSq = math.max(1f, dto.ActivationRadiusSq) * math.max(0.01f, HardwareRadiusSqScale);
                activationRadiusSq *= math.select(
                    1f,
                    AbyssalDepthSleepDistanceScale * AbyssalDepthSleepDistanceScale,
                    AbyssalDepthCull != 0);

                float3 safeCameraForward = NormalizeWithRsqrtGuard(CameraForward, new float3(0f, 0f, 1f));
                activationRadiusSq *= math.select(
                    1f,
                    BehindCameraSleepDistanceScale * BehindCameraSleepDistanceScale,
                    math.dot(delta, safeCameraForward) < 0f);

                float wakeRadiusSq = math.max(1f, activationRadiusSq * PhysicsCullingWakeRadiusSqScale);
                bool outsideFrustum = (UseFrustum != 0) & (distanceSq > FrustumInnerSphereSq) & IsOutsideFrustum(delta);
                float sleepThresholdSq = math.select(activationRadiusSq, wakeRadiusSq, sleepActive);
                bool shouldSleep = (distanceSq > sleepThresholdSq) | outsideFrustum;

                bool kinematicActive = (currentState & CullingStateKinematicActive) != 0;
                float kinematicSleepSq = KinematicSleepDistanceMeters * KinematicSleepDistanceMeters;
                float kinematicWakeSq = KinematicWakeDistanceMeters * KinematicWakeDistanceMeters;
                bool shouldKinematic = distanceSq > math.select(kinematicSleepSq, kinematicWakeSq, kinematicActive);

                bool meshStripActive = (currentState & CullingStateMeshColliderStripped) != 0;
                bool hasHeavyCollider = (currentState & CullingStateHeavyCollider) != 0;
                float stripSq = MeshColliderStripDistanceMeters * MeshColliderStripDistanceMeters;
                float restoreSq = MeshColliderRestoreDistanceMeters * MeshColliderRestoreDistanceMeters;
                bool shouldStripMeshColliders = hasHeavyCollider &
                    (distanceSq > math.select(stripSq, restoreSq, meshStripActive));

                bool finalSleep = ((shouldSleep & (!hysteresisLocked)) | (sleepActive & hysteresisLocked)) & (!forceAwake);
                byte newSleep = (byte)math.select(0, 1, finalSleep);
                byte previousSleep = dto.IsAsleep;
                dto.IsAsleep = newSleep;
                AwakeResults[index] = (byte)math.select(1, 0, finalSleep);

                bool commandExtensionsEnabled = (!forceAwake) & (!hysteresisLocked);
                int command = math.select((int)CullingCommandAwake, 0, finalSleep);
                command |= math.select(0, (int)CullingCommandKinematic, shouldKinematic & commandExtensionsEnabled);
                command |= math.select(0, (int)CullingCommandStripMeshColliders, shouldStripMeshColliders & commandExtensionsEnabled);
                command |= math.select(0, (int)CullingCommandInvalidInput, invalidInput);
                byte commandByte = (byte)command;
                CommandResults[index] = commandByte;

                byte previousCommand = ResolvePreviousCommand(currentState, previousSleep);
                bool stateChanged = invalidInput |
                    ((!hysteresisLocked) & (!forceAwake) & ((newSleep != previousSleep) | (commandByte != previousCommand)));
                StateAges[index] = math.select(0f, nextAge, (!forceAwake) & (!stateChanged));
                ChangedIndices[index] = math.select(-1, index, stateChanged);
            }

            private bool IsOutsideFrustum(float3 localPoint)
            {
                return (PlaneDistance(FrustumPlane0, localPoint) < 0f) |
                    (PlaneDistance(FrustumPlane1, localPoint) < 0f) |
                    (PlaneDistance(FrustumPlane2, localPoint) < 0f) |
                    (PlaneDistance(FrustumPlane3, localPoint) < 0f) |
                    (PlaneDistance(FrustumPlane4, localPoint) < 0f) |
                    (PlaneDistance(FrustumPlane5, localPoint) < 0f);
            }

            private static float PlaneDistance(float4 plane, float3 point)
            {
                return (plane.x * point.x) + (plane.y * point.y) + (plane.z * point.z) + plane.w;
            }

            private static byte ResolvePreviousCommand(byte currentState, byte sleep)
            {
                int command = math.select((int)CullingCommandAwake, 0, sleep != 0);
                command |= math.select(0, (int)CullingCommandKinematic, (currentState & CullingStateKinematicActive) != 0);
                command |= math.select(0, (int)CullingCommandStripMeshColliders, (currentState & CullingStateMeshColliderStripped) != 0);
                return (byte)command;
            }
        }
    }
}
