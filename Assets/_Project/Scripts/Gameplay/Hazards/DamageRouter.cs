using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DamageRouterPacket
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint DamageType;
        [FieldOffset(8)] public uint StatusBits;
        [FieldOffset(12)] public float BaseDamage;
        [FieldOffset(16)] public float RadiusMeters;
        [FieldOffset(20)] public float StatusDurationSeconds;
        [FieldOffset(24)] public float LocalPointX;
        [FieldOffset(28)] public float LocalPointY;
        [FieldOffset(32)] public float LocalPointZ;
        [FieldOffset(36)] public uint EffectHash;
        [FieldOffset(40)] public ushort SourceId;
        [FieldOffset(42)] public byte Channel;
        [FieldOffset(43)] public byte Flags;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Hazards/Damage Router")]
    public sealed class DamageRouter : MonoBehaviour
    {
        public const int PacketSizeBytes = 64;
        public const int CanonicalDamagePacketSizeBytes = 48;
        private const int PacketAlignmentBytes = 8;
        private const int CanonicalDamagePacketAlignmentBytes = 8;
        private const byte IntegrityDamageChannel = (byte)Hecton8.Core.DamageChannel.Integrity;
        private const float DefaultSeaLevelY = 14.02f;
#if UNITY_EDITOR
        private const string PacketLayoutError = "Damage packet layout violation. Expected DamageRouterPacket=64 bytes and DamagePacket=48 bytes, both 8-byte aligned.";
#endif

        [SerializeField] private uint sourceHash;
        [SerializeField] private uint damageType = CombatDamageTypes.Thermal;
        [SerializeField] private uint statusBits = CombatStatusBits.Burning;
        [SerializeField] private float baseDamage = 8f;
        [SerializeField] private float radiusMeters = 3f;
        [SerializeField] private float statusDurationSeconds = 4f;
        [SerializeField] private Vector3 localPoint;
        [SerializeField] private uint effectHash;
        [SerializeField] private ushort sourceId = DamageSourceIds.EnvironmentHazard;
        [SerializeField] private byte channel = IntegrityDamageChannel;
        [SerializeField] private byte flags;

        public uint SourceHash => sourceHash;
        public uint DamageType => damageType;
        public uint StatusBits => statusBits;
        public float BaseDamage => baseDamage;
        public float RadiusMeters => radiusMeters;
        public float StatusDurationSeconds => statusDurationSeconds;
        public uint EffectHash => effectHash;
        public static bool IsPacketLayoutValid
        {
            get
            {
                int size = ResolvePacketSizeBytes();
                return size == PacketSizeBytes && (size & (PacketAlignmentBytes - 1)) == 0;
            }
        }

        public static int ResolvedPacketSizeBytes => ResolvePacketSizeBytes();
        public static bool IsCanonicalDamagePacketLayoutValid
        {
            get
            {
                int size = ResolveCanonicalDamagePacketSizeBytes();
                return size == CanonicalDamagePacketSizeBytes && (size & (CanonicalDamagePacketAlignmentBytes - 1)) == 0;
            }
        }

        public static int ResolvedCanonicalDamagePacketSizeBytes => ResolveCanonicalDamagePacketSizeBytes();

        public void ConfigureForEditor(
            uint newSourceHash,
            uint newDamageType,
            uint newStatusBits,
            float newBaseDamage,
            float newRadiusMeters,
            float newStatusDurationSeconds,
            Vector3 newLocalPoint,
            uint newEffectHash)
        {
            sourceHash = newSourceHash;
            damageType = newDamageType;
            statusBits = newStatusBits;
            baseDamage = SanitizeNonNegative(newBaseDamage, 0f);
            radiusMeters = Mathf.Max(0.05f, SanitizeNonNegative(newRadiusMeters, 0.05f));
            statusDurationSeconds = SanitizeNonNegative(newStatusDurationSeconds, 0f);
            localPoint = SanitizeVector(newLocalPoint);
            effectHash = newEffectHash;
            sourceId = DamageSourceIds.EnvironmentHazard;
            channel = IntegrityDamageChannel;
            flags = 0;
        }

        public bool TryReadPacket(out DamageRouterPacket packet)
        {
            packet = BuildPacket();
            return IsUsablePacket(in packet);
        }

        public DamageRouterPacket ReadPacket()
        {
            return BuildPacket();
        }

        public bool TryBuildDamagePacket(Transform receiverTransform, Vector3 impactPointWorld, out DamagePacket packet)
        {
            float3 receiverLocalPoint = ResolveReceiverLocalPoint(receiverTransform, impactPointWorld);
            float depthMeters = ResolveDepthMeters(impactPointWorld);
            return TryBuildDamagePacket(in receiverLocalPoint, depthMeters, out packet);
        }

        public bool TryBuildDamagePacket(in float3 receiverLocalPoint, float depthMeters, out DamagePacket packet)
        {
            DamageRouterPacket routerPacket = BuildPacket();
            if (!IsUsablePacket(in routerPacket))
            {
                packet = default;
                return false;
            }

            packet = BuildCanonicalDamagePacket(
                in routerPacket,
                SanitizeFloat3(receiverLocalPoint),
                SanitizeNonNegative(depthMeters, 0f));
            return true;
        }

        private DamageRouterPacket BuildPacket()
        {
            Vector3 point = SanitizeVector(localPoint);
            float scaledRadius = Mathf.Max(0.05f, SanitizeNonNegative(radiusMeters, 0.05f)) * ResolveMaxAbsScale(transform.lossyScale);
            return new DamageRouterPacket
            {
                SourceHash = sourceHash,
                DamageType = damageType,
                StatusBits = statusBits,
                BaseDamage = SanitizeNonNegative(baseDamage, 0f),
                RadiusMeters = scaledRadius,
                StatusDurationSeconds = SanitizeNonNegative(statusDurationSeconds, 0f),
                LocalPointX = point.x,
                LocalPointY = point.y,
                LocalPointZ = point.z,
                EffectHash = effectHash,
                SourceId = sourceId,
                Channel = channel,
                Flags = flags
            };
        }

        private static DamagePacket BuildCanonicalDamagePacket(in DamageRouterPacket routerPacket, float3 receiverLocalPoint, float depthMeters)
        {
            return new DamagePacket
            {
                Channel = ResolveDamageChannel(routerPacket.Channel),
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = routerPacket.BaseDamage,
                LocalPoint = receiverLocalPoint,
                DamageType = routerPacket.DamageType,
                IntegrityDelta = 0,
                Depth = depthMeters,
                SourceId = routerPacket.SourceId,
                TraumaLevel = 0
            };
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!IsPacketLayoutValid || !IsCanonicalDamagePacketLayoutValid)
                Debug.LogError(PacketLayoutError, this);
#endif
            baseDamage = SanitizeNonNegative(baseDamage, 0f);
            radiusMeters = Mathf.Max(0.05f, SanitizeNonNegative(radiusMeters, 0.05f));
            statusDurationSeconds = SanitizeNonNegative(statusDurationSeconds, 0f);
            localPoint = SanitizeVector(localPoint);
            if (channel == 0)
                channel = IntegrityDamageChannel;
            if (sourceId == 0)
                sourceId = DamageSourceIds.EnvironmentHazard;
        }

        private static bool IsUsablePacket(in DamageRouterPacket packet)
        {
            return packet.BaseDamage > 0f &&
                   packet.RadiusMeters > 0f &&
                   packet.DamageType != 0u &&
                   IsFinite(packet.BaseDamage) &&
                   IsFinite(packet.RadiusMeters);
        }

        private static DamageChannel ResolveDamageChannel(byte value)
        {
            return value <= (byte)DamageChannel.HullBreach ? (DamageChannel)value : DamageChannel.Integrity;
        }

        private static float3 ResolveReceiverLocalPoint(Transform receiverTransform, Vector3 impactPointWorld)
        {
            if (receiverTransform == null ||
                !IsFinite(impactPointWorld.x) ||
                !IsFinite(impactPointWorld.y) ||
                !IsFinite(impactPointWorld.z))
            {
                return float3.zero;
            }

            Vector3 local = receiverTransform.InverseTransformPoint(impactPointWorld);
            float3 localPoint = new float3(local.x, local.y, local.z);
            return math.all(math.isfinite(localPoint)) ? localPoint : float3.zero;
        }

        private static float ResolveDepthMeters(Vector3 impactPointWorld)
        {
            return IsFinite(impactPointWorld.y) ? Mathf.Max(0f, DefaultSeaLevelY - impactPointWorld.y) : 0f;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) ? value : Vector3.zero;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : fallback;
        }

        private static float ResolveMaxAbsScale(Vector3 scale)
        {
            if (!IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z))
                return 1f;

            return Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        }

        private static int ResolvePacketSizeBytes()
        {
            return UnsafeUtility.SizeOf<DamageRouterPacket>();
        }

        private static int ResolveCanonicalDamagePacketSizeBytes()
        {
            return UnsafeUtility.SizeOf<DamagePacket>();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
