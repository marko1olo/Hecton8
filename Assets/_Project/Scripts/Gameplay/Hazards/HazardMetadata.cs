using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VfxAnchorRuntimeData
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 LocalForward;
        [FieldOffset(24)] public uint EffectHash;
        [FieldOffset(28)] public uint HazardHash;
        [FieldOffset(32)] public ushort AnchorIndex;
        [FieldOffset(34)] public ushort Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [Serializable]
    public struct VfxAnchorBinding
    {
        public Transform Anchor;
        public Vector3 LocalPosition;
        public Vector3 LocalForward;
        public uint EffectHash;
        public string EffectId;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hazards/Hazard Metadata")]
    public sealed class HazardMetadata : MonoBehaviour
    {
        public const int VfxAnchorRuntimeDataSizeBytes = 64;
        private const int VfxAnchorRuntimeDataAlignmentBytes = 8;

        [SerializeField] private uint hazardHash;
        [SerializeField] private HazardType hazardType = HazardType.Heat;
        [SerializeField] private string effectId = "hazard_steam";
        [SerializeField] private uint effectHash;
        [SerializeField] private DamageRouter damageRouter;
        [SerializeField] private VfxAnchorBinding[] vfxAnchors;
        [SerializeField] private Light[] lights;
        [SerializeField] private DecalProjector[] decalProjectors;
        [SerializeField] private float triggerRadiusMeters;
        [SerializeField] private float presentationCullDistanceMeters;

        public uint HazardHash => hazardHash;
        public HazardType HazardTypeValue => hazardType;
        public string EffectId => effectId;
        public uint EffectHash => effectHash;
        public DamageRouter Router => damageRouter;
        public int AnchorCount => vfxAnchors != null ? vfxAnchors.Length : 0;
        public int LightCount => lights != null ? lights.Length : 0;
        public int DecalCount => decalProjectors != null ? decalProjectors.Length : 0;
        public float TriggerRadiusMeters => triggerRadiusMeters;
        public float PresentationCullDistanceMeters => presentationCullDistanceMeters;
        public static bool IsVfxAnchorRuntimeDataLayoutValid
        {
            get
            {
                int size = ResolveVfxAnchorRuntimeDataSizeBytes();
                return size == VfxAnchorRuntimeDataSizeBytes && (size & (VfxAnchorRuntimeDataAlignmentBytes - 1)) == 0;
            }
        }

        public static int ResolvedVfxAnchorRuntimeDataSizeBytes => ResolveVfxAnchorRuntimeDataSizeBytes();

        public void ConfigureForEditor(
            uint newHazardHash,
            HazardType newHazardType,
            string newEffectId,
            uint newEffectHash,
            DamageRouter newDamageRouter,
            VfxAnchorBinding[] newVfxAnchors,
            Light[] newLights,
            DecalProjector[] newDecalProjectors,
            float newTriggerRadiusMeters,
            float newPresentationCullDistanceMeters)
        {
            hazardHash = newHazardHash;
            hazardType = newHazardType;
            effectId = string.IsNullOrWhiteSpace(newEffectId) ? "hazard_steam" : newEffectId;
            effectHash = newEffectHash;
            damageRouter = newDamageRouter;
            vfxAnchors = newVfxAnchors;
            lights = newLights;
            decalProjectors = newDecalProjectors;
            triggerRadiusMeters = SanitizeNonNegative(newTriggerRadiusMeters);
            presentationCullDistanceMeters = SanitizeNonNegative(newPresentationCullDistanceMeters);
            SanitizeAnchorBindings(vfxAnchors);
        }

        public bool TryGetAnchor(int index, out VfxAnchorBinding anchor)
        {
            VfxAnchorBinding[] anchors = vfxAnchors;
            if (anchors == null || (uint)index >= (uint)anchors.Length)
            {
                anchor = default;
                return false;
            }

            anchor = anchors[index];
            return anchor.Anchor != null;
        }

        public bool TryGetAnchorRuntimeData(int index, out Transform anchor, out VfxAnchorRuntimeData runtimeData)
        {
            VfxAnchorBinding[] anchors = vfxAnchors;
            if (anchors == null || (uint)index >= (uint)anchors.Length)
            {
                anchor = null;
                runtimeData = default;
                return false;
            }

            VfxAnchorBinding binding = anchors[index];
            anchor = binding.Anchor;
            Vector3 localPosition = SanitizeVector(binding.LocalPosition);
            Vector3 localForward = SanitizeStoredDirection(binding.LocalForward);
            uint resolvedEffectHash = binding.EffectHash != 0u ? binding.EffectHash : effectHash;
            runtimeData = new VfxAnchorRuntimeData
            {
                LocalPosition = new float3(localPosition.x, localPosition.y, localPosition.z),
                LocalForward = new float3(localForward.x, localForward.y, localForward.z),
                EffectHash = resolvedEffectHash,
                HazardHash = hazardHash,
                AnchorIndex = (ushort)Mathf.Min(index, ushort.MaxValue),
                Flags = 0
            };
            return anchor != null && resolvedEffectHash != 0u;
        }

        public bool TryGetAnchorRuntimeData(
            int index,
            out Transform anchor,
            out Vector3 localPosition,
            out Vector3 localForward,
            out uint anchorEffectHash)
        {
            if (!TryGetAnchorRuntimeData(index, out anchor, out VfxAnchorRuntimeData runtimeData))
            {
                localPosition = Vector3.zero;
                localForward = Vector3.up;
                anchorEffectHash = 0u;
                return false;
            }

            localPosition = new Vector3(runtimeData.LocalPosition.x, runtimeData.LocalPosition.y, runtimeData.LocalPosition.z);
            localForward = new Vector3(runtimeData.LocalForward.x, runtimeData.LocalForward.y, runtimeData.LocalForward.z);
            anchorEffectHash = runtimeData.EffectHash;
            return true;
        }

        public bool TryGetLight(int index, out Light light)
        {
            Light[] resolvedLights = lights;
            if (resolvedLights == null || (uint)index >= (uint)resolvedLights.Length)
            {
                light = null;
                return false;
            }

            light = resolvedLights[index];
            return light != null;
        }

        public bool TryGetDecalProjector(int index, out DecalProjector decalProjector)
        {
            DecalProjector[] projectors = decalProjectors;
            if (projectors == null || (uint)index >= (uint)projectors.Length)
            {
                decalProjector = null;
                return false;
            }

            decalProjector = projectors[index];
            return decalProjector != null;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(effectId))
                effectId = "hazard_steam";
            triggerRadiusMeters = SanitizeNonNegative(triggerRadiusMeters);
            presentationCullDistanceMeters = SanitizeNonNegative(presentationCullDistanceMeters);
            SanitizeAnchorBindings(vfxAnchors);
        }

        private static float SanitizeNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) ? value : Vector3.zero;
        }

        private static Vector3 SanitizeDirection(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && value.sqrMagnitude > 0.0001f
                ? value.normalized
                : Vector3.up;
        }

        private static Vector3 SanitizeStoredDirection(Vector3 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
                return Vector3.up;

            float sqrMagnitude = value.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return Vector3.up;

            return sqrMagnitude > 0.99f && sqrMagnitude < 1.01f ? value : value.normalized;
        }

        private static void SanitizeAnchorBindings(VfxAnchorBinding[] anchors)
        {
            if (anchors == null)
                return;

            for (int i = 0; i < anchors.Length; i++)
            {
                VfxAnchorBinding binding = anchors[i];
                binding.LocalPosition = SanitizeVector(binding.LocalPosition);
                binding.LocalForward = SanitizeDirection(binding.LocalForward);
                anchors[i] = binding;
            }
        }

        private static int ResolveVfxAnchorRuntimeDataSizeBytes()
        {
            return UnsafeUtility.SizeOf<VfxAnchorRuntimeData>();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
