using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
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
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public readonly struct PhysicsImpactSignal
    {
        [FieldOffset(0)] public readonly ulong PrimaryBodyId;
        [FieldOffset(8)] public readonly ulong SecondaryBodyId;
        [FieldOffset(16)] private readonly AbsoluteUniversePosition _pointAup;
        [FieldOffset(64)] public readonly Vector3 Point;
        [FieldOffset(76)] public readonly Vector3 Normal;
        [FieldOffset(88)] public readonly float Force;
        [FieldOffset(92)] public readonly float Intensity;
        [FieldOffset(96)] public readonly float MassVelocity;
        [FieldOffset(100)] public readonly PhysicsImpactWeightClass WeightClass;
        [FieldOffset(101)] public readonly byte PrimaryAudioMaterialId;
        [FieldOffset(102)] public readonly byte SecondaryAudioMaterialId;
        [FieldOffset(103)] private readonly byte _hasPointAup;
        [FieldOffset(104)] private readonly uint _pad0;
        [FieldOffset(108)] private readonly uint _pad1;
        [FieldOffset(112)] private readonly ulong _pad2;
        [FieldOffset(120)] private readonly ulong _pad3;

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
            if (TryResolveAupFromRuntimeOrigin(point, out AbsoluteUniversePosition pointAup))
            {
                _pointAup = pointAup;
                _hasPointAup = 1;
            }
            else
            {
                _pointAup = default;
                _hasPointAup = 0;
            }

            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
            _pad0 = 0u;
            _pad1 = 0u;
            _pad2 = 0UL;
            _pad3 = 0UL;
        }

        public PhysicsImpactSignal(
            ulong primaryBodyId,
            ulong secondaryBodyId,
            Vector3 point,
            in AbsoluteUniversePosition pointAup,
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
            _pointAup = pointAup;
            _hasPointAup = 1;
            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
            _pad0 = 0u;
            _pad1 = 0u;
            _pad2 = 0UL;
            _pad3 = 0UL;
        }

        public static bool HasPointAup(in PhysicsImpactSignal signal)
        {
            return signal._hasPointAup != 0;
        }

        public AbsoluteUniversePosition ResolvePointAup()
        {
            return _hasPointAup != 0
                ? _pointAup
                : TryResolveAupFromRuntimeOrigin(Point, out AbsoluteUniversePosition pointAup)
                    ? pointAup
                    : default;
        }

        public static bool IsHeavy(in PhysicsImpactSignal signal)
        {
            return signal.WeightClass == PhysicsImpactWeightClass.Heavy;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFinite(in positionAup);
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFinite(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                   math.isfinite(value.LocalY) &&
                   math.isfinite(value.LocalZ);
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
