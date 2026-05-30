using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Coarse impact weight bucket used by downstream audio/VFX listeners.
    /// </summary>
    public enum PhysicsImpactWeightClass : byte
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    /// <summary>
    /// Immutable gameplay impact payload flushed in LateUpdate after the fixed-step collision phase.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public readonly struct PhysicsImpactSignal
    {
        [FieldOffset(0)] public readonly ulong PrimaryBodyId;
        [FieldOffset(8)] public readonly ulong SecondaryBodyId;
        [FieldOffset(16)] private readonly double3 _pointAupMeters;
        [FieldOffset(64)] public readonly Vector3 Point;
        [FieldOffset(76)] public readonly Vector3 Normal;
        [FieldOffset(88)] public readonly float Force;
        [FieldOffset(92)] public readonly float Intensity;
        [FieldOffset(96)] public readonly float MassVelocity;
        [FieldOffset(100)] public readonly PhysicsImpactWeightClass WeightClass;
        [FieldOffset(101)] public readonly byte PrimaryAudioMaterialId;
        [FieldOffset(102)] public readonly byte SecondaryAudioMaterialId;
        [FieldOffset(103)] private readonly byte _hasPointAup;
        [FieldOffset(104)] private readonly byte _pad0;
        [FieldOffset(105)] private readonly byte _pad1;
        [FieldOffset(106)] private readonly byte _pad2;
        [FieldOffset(107)] private readonly byte _pad3;
        [FieldOffset(108)] private readonly byte _pad4;
        [FieldOffset(109)] private readonly byte _pad5;
        [FieldOffset(110)] private readonly byte _pad6;
        [FieldOffset(111)] private readonly byte _pad7;
        [FieldOffset(112)] private readonly byte _pad8;
        [FieldOffset(113)] private readonly byte _pad9;
        [FieldOffset(114)] private readonly byte _pad10;
        [FieldOffset(115)] private readonly byte _pad11;
        [FieldOffset(116)] private readonly byte _pad12;
        [FieldOffset(117)] private readonly byte _pad13;
        [FieldOffset(118)] private readonly byte _pad14;
        [FieldOffset(119)] private readonly byte _pad15;
        [FieldOffset(120)] private readonly byte _pad16;
        [FieldOffset(121)] private readonly byte _pad17;
        [FieldOffset(122)] private readonly byte _pad18;
        [FieldOffset(123)] private readonly byte _pad19;
        [FieldOffset(124)] private readonly byte _pad20;
        [FieldOffset(125)] private readonly byte _pad21;
        [FieldOffset(126)] private readonly byte _pad22;
        [FieldOffset(127)] private readonly byte _pad23;

        public PhysicsImpactSignal(
            ulong primaryBodyId,
            ulong secondaryBodyId,
            Vector3 point,
            Vector3 normal,
            float force,
            float intensity,
            float massVelocity,
            PhysicsImpactWeightClass weightClass,
            byte primaryAudioMaterialId,
            byte secondaryAudioMaterialId)
        {
            PrimaryBodyId = primaryBodyId;
            SecondaryBodyId = secondaryBodyId;
            Point = point;
            _pointAupMeters = new double3(point.x, point.y, point.z);
            _hasPointAup = 0;
            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
            _pad4 = 0;
            _pad5 = 0;
            _pad6 = 0;
            _pad7 = 0;
            _pad8 = 0;
            _pad9 = 0;
            _pad10 = 0;
            _pad11 = 0;
            _pad12 = 0;
            _pad13 = 0;
            _pad14 = 0;
            _pad15 = 0;
            _pad16 = 0;
            _pad17 = 0;
            _pad18 = 0;
            _pad19 = 0;
            _pad20 = 0;
            _pad21 = 0;
            _pad22 = 0;
            _pad23 = 0;
        }

        public PhysicsImpactSignal(
            ulong primaryBodyId,
            ulong secondaryBodyId,
            Vector3 point,
            in double3 pointAupMeters,
            Vector3 normal,
            float force,
            float intensity,
            float massVelocity,
            PhysicsImpactWeightClass weightClass,
            byte primaryAudioMaterialId,
            byte secondaryAudioMaterialId)
        {
            PrimaryBodyId = primaryBodyId;
            SecondaryBodyId = secondaryBodyId;
            Point = point;
            _pointAupMeters = IsFinite(pointAupMeters) ? pointAupMeters : new double3(point.x, point.y, point.z);
            _hasPointAup = (byte)(IsFinite(pointAupMeters) ? 1 : 0);
            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
            _pad4 = 0;
            _pad5 = 0;
            _pad6 = 0;
            _pad7 = 0;
            _pad8 = 0;
            _pad9 = 0;
            _pad10 = 0;
            _pad11 = 0;
            _pad12 = 0;
            _pad13 = 0;
            _pad14 = 0;
            _pad15 = 0;
            _pad16 = 0;
            _pad17 = 0;
            _pad18 = 0;
            _pad19 = 0;
            _pad20 = 0;
            _pad21 = 0;
            _pad22 = 0;
            _pad23 = 0;
        }

        public static bool HasPointAup(in PhysicsImpactSignal signal)
        {
            return signal._hasPointAup != 0;
        }

        public double3 ResolvePointAupMeters()
        {
            return _hasPointAup != 0 ? _pointAupMeters : new double3(Point.x, Point.y, Point.z);
        }

        public static bool IsHeavy(in PhysicsImpactSignal signal)
        {
            return signal.WeightClass == PhysicsImpactWeightClass.Heavy;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }
    }

    /// <summary>
    /// Listener contract for deferred physics-impact feedback.
    /// </summary>
    public interface IPhysicsImpactEventListener
    {
        void OnPhysicsImpact(in PhysicsImpactSignal impactSignal);
    }
}
