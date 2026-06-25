using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    public enum DroneAttachmentKind : byte
    {
        None = 0,
        ToolSocket = 1,
        Thruster = 2,
        Sensor = 3,
        StatusLight = 4
    }

    [Flags]
    public enum DroneAttachmentFlags : byte
    {
        None = 0,
        Active = 1,
        ToolSnap = 2,
        EmitsVfx = 4,
        VisualOnly = 8
    }

    [Serializable]
    public struct DroneAttachmentAnchorDescriptor
    {
        public int AnchorIndex;
        public int BoneIndex;
        public uint AnchorHash;
        public DroneAttachmentKind Kind;
        public DroneBoneTierMask TierMask;
        public DroneAttachmentFlags Flags;
        public Vector3 LocalPosition;
        public Vector3 LocalForward;
        public Vector3 LocalUp;
        public float MinQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct DroneAttachmentRuntimeData
    {
        [FieldOffset(0)] public int AnchorIndex;
        [FieldOffset(4)] public int BoneIndex;
        [FieldOffset(8)] public uint AnchorHash;
        [FieldOffset(12)] public byte Kind;
        [FieldOffset(13)] public byte TierMask;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(15)] public byte Reserved0;
        [FieldOffset(16)] public float3 LocalPosition;
        [FieldOffset(28)] public float3 LocalForward;
        [FieldOffset(40)] public float3 LocalUp;
        [FieldOffset(52)] public float MinQualityWeight;
        [FieldOffset(56)] public ulong Reserved1;
        [FieldOffset(64)] public ulong Reserved2;
        [FieldOffset(72)] public ulong Reserved3;
        [FieldOffset(80)] public ulong Reserved4;
        [FieldOffset(88)] public ulong Reserved5;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Drone Attachment Metadata")]
    public sealed class DroneAttachmentMetadata : MonoBehaviour
    {
        public const int RuntimeDataStrideBytes = 96;
        private const int RuntimeDataAlignmentBytes = 8;

        private static readonly Transform[] s_emptyAnchors = Array.Empty<Transform>();
        private static readonly DroneAttachmentAnchorDescriptor[] s_emptyDescriptors = Array.Empty<DroneAttachmentAnchorDescriptor>();
        private static readonly Renderer[] s_emptyRenderers = Array.Empty<Renderer>();

        [Header("Authoring Identity")]
        [SerializeField] private uint droneId;
        [SerializeField] private uint bakeHash;
        [SerializeField, Range(0f, 1f)] private float authoredQualityWeight = 1f;

        [Header("Attachment Table")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform[] anchors = s_emptyAnchors;
        [SerializeField] private DroneAttachmentAnchorDescriptor[] descriptors = s_emptyDescriptors;
        [SerializeField] private Renderer[] emissionRenderers = s_emptyRenderers;

        [Header("Presentation Colors")]
        [SerializeField] private Color idleEmissionColor = new Color(0.05f, 0.75f, 1f, 1f);
        [SerializeField] private Color activeEmissionColor = new Color(1f, 0.55f, 0.08f, 1f);
        [SerializeField] private Color faultEmissionColor = new Color(1f, 0.05f, 0.02f, 1f);

        public uint DroneId => droneId;
        public uint BakeHash => bakeHash;
        public float AuthoredQualityWeight => authoredQualityWeight;
        public Transform RigRoot => rigRoot;
        public int AnchorCount => anchors == null ? 0 : anchors.Length;
        public int DescriptorCount => descriptors == null ? 0 : descriptors.Length;
        public int EmissionRendererCount => emissionRenderers == null ? 0 : emissionRenderers.Length;
        public Color IdleEmissionColor => idleEmissionColor;
        public Color ActiveEmissionColor => activeEmissionColor;
        public Color FaultEmissionColor => faultEmissionColor;
        public Color IdleEmission => idleEmissionColor;
        public Color ActiveEmission => activeEmissionColor;
        public Color FaultEmission => faultEmissionColor;

        public ReadOnlySpan<DroneAttachmentAnchorDescriptor> Descriptors =>
            descriptors == null ? ReadOnlySpan<DroneAttachmentAnchorDescriptor>.Empty : new ReadOnlySpan<DroneAttachmentAnchorDescriptor>(descriptors);

        public bool TryGetAnchorTransform(int index, out Transform anchor)
        {
            Transform[] source = anchors;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                anchor = null;
                return false;
            }

            anchor = source[index];
            return anchor != null;
        }

        public bool TryGetDescriptor(int index, out DroneAttachmentAnchorDescriptor descriptor)
        {
            DroneAttachmentAnchorDescriptor[] source = descriptors;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                descriptor = default;
                return false;
            }

            descriptor = source[index];
            return true;
        }

        public bool TryGetEmissionRenderer(int index, out Renderer renderer)
        {
            Renderer[] source = emissionRenderers;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                renderer = null;
                return false;
            }

            renderer = source[index];
            return renderer != null;
        }

        public int CopyAttachmentTableTo(NativeArray<DroneAttachmentRuntimeData> destination)
        {
            DroneAttachmentAnchorDescriptor[] source = descriptors;
            if (source == null || !destination.IsCreated)
                return 0;

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = ToRuntimeData(source[i]);

            return count;
        }

        public int CopyAttachmentTableTo(DroneAttachmentRuntimeData[] destination)
        {
            DroneAttachmentAnchorDescriptor[] source = descriptors;
            if (source == null || destination == null)
                return 0;

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = ToRuntimeData(source[i]);

            return count;
        }

        public bool TryExportRuntimeAttachment(int index, out DroneAttachmentRuntimeData runtimeData)
        {
            if (!TryGetDescriptor(index, out DroneAttachmentAnchorDescriptor descriptor))
            {
                runtimeData = default;
                return false;
            }

            runtimeData = ToRuntimeData(descriptor);
            return true;
        }

        public static bool ValidateStaticLayout()
        {
            int stride = UnsafeUtility.SizeOf<DroneAttachmentRuntimeData>();
            return stride == RuntimeDataStrideBytes && (stride & (RuntimeDataAlignmentBytes - 1)) == 0;
        }

        public static bool ValidateDescriptorSet(
            Transform[] anchorRefs,
            DroneAttachmentAnchorDescriptor[] authoredDescriptors,
            Renderer[] authoredEmissionRenderers,
            out string failureReason)
        {
            if (!ValidateStaticLayout())
            {
                failureReason = "DroneAttachmentRuntimeData layout is invalid.";
                return false;
            }

            if (anchorRefs == null || anchorRefs.Length == 0)
            {
                failureReason = "DroneAttachmentMetadata has no anchor refs.";
                return false;
            }

            if (authoredDescriptors == null || authoredDescriptors.Length == 0)
            {
                failureReason = "DroneAttachmentMetadata has no descriptors.";
                return false;
            }

            bool hasToolSocket = false;
            bool hasThruster = false;
            for (int i = 0; i < authoredDescriptors.Length; i++)
            {
                DroneAttachmentAnchorDescriptor descriptor = authoredDescriptors[i];
                if (descriptor.AnchorIndex != i)
                {
                    failureReason = "DroneAttachmentMetadata table is not anchor-index ordered.";
                    return false;
                }

                if ((uint)descriptor.AnchorIndex >= (uint)anchorRefs.Length ||
                    anchorRefs[descriptor.AnchorIndex] == null ||
                    descriptor.AnchorHash == 0u ||
                    descriptor.Kind == DroneAttachmentKind.None ||
                    descriptor.TierMask == 0 ||
                    descriptor.Flags == 0 ||
                    descriptor.BoneIndex < -1 ||
                    !IsFinite(descriptor.LocalPosition) ||
                    !IsFinite(descriptor.LocalForward) ||
                    !IsFinite(descriptor.LocalUp) ||
                    math.lengthsq((float3)(descriptor.LocalForward)) <= 0.000001f ||
                    math.lengthsq((float3)(descriptor.LocalUp)) <= 0.000001f ||
                    !math.isfinite(descriptor.MinQualityWeight) ||
                    descriptor.MinQualityWeight < 0f ||
                    descriptor.MinQualityWeight > 1f)
                {
                    failureReason = "DroneAttachmentMetadata descriptor validation failed.";
                    return false;
                }

                hasToolSocket |= descriptor.Kind == DroneAttachmentKind.ToolSocket;
                hasThruster |= descriptor.Kind == DroneAttachmentKind.Thruster;

                for (int j = i + 1; j < authoredDescriptors.Length; j++)
                {
                    if (authoredDescriptors[j].AnchorIndex == descriptor.AnchorIndex ||
                        authoredDescriptors[j].AnchorHash == descriptor.AnchorHash)
                    {
                        failureReason = "DroneAttachmentMetadata duplicate anchor index or hash.";
                        return false;
                    }
                }
            }

            if (!hasToolSocket)
            {
                failureReason = "DroneAttachmentMetadata is missing Socket_Tool.";
                return false;
            }

            if (!hasThruster)
            {
                failureReason = "DroneAttachmentMetadata is missing VFX_Thruster.";
                return false;
            }

            if (authoredEmissionRenderers == null || authoredEmissionRenderers.Length == 0)
            {
                failureReason = "DroneAttachmentMetadata has no emission renderer refs.";
                return false;
            }

            if (authoredEmissionRenderers != null)
            {
                for (int i = 0; i < authoredEmissionRenderers.Length; i++)
                {
                    if (authoredEmissionRenderers[i] == null)
                    {
                        failureReason = "DroneAttachmentMetadata emission renderer ref is null.";
                        return false;
                    }
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            authoredQualityWeight = math.saturate(math.isfinite(authoredQualityWeight) ? authoredQualityWeight : 1f);
            if (anchors == null)
                anchors = s_emptyAnchors;
            if (descriptors == null)
                descriptors = s_emptyDescriptors;
            if (emissionRenderers == null)
                emissionRenderers = s_emptyRenderers;

            idleEmissionColor = SanitizeColor(idleEmissionColor, new Color(0.05f, 0.75f, 1f, 1f));
            activeEmissionColor = SanitizeColor(activeEmissionColor, new Color(1f, 0.55f, 0.08f, 1f));
            faultEmissionColor = SanitizeColor(faultEmissionColor, new Color(1f, 0.05f, 0.02f, 1f));

            for (int i = 0; i < descriptors.Length; i++)
            {
                DroneAttachmentAnchorDescriptor descriptor = descriptors[i];
                descriptor.AnchorIndex = math.max(0, descriptor.AnchorIndex);
                descriptor.BoneIndex = descriptor.BoneIndex < 0 ? -1 : descriptor.BoneIndex;
                descriptor.Kind = descriptor.Kind == DroneAttachmentKind.None ? DroneAttachmentKind.ToolSocket : descriptor.Kind;
                descriptor.TierMask = descriptor.TierMask == 0 ? DroneBoneTierMask.All : descriptor.TierMask;
                descriptor.Flags = descriptor.Flags == 0 ? DroneAttachmentFlags.Active : descriptor.Flags;
                descriptor.LocalPosition = SanitizeVector(descriptor.LocalPosition, Vector3.zero);
                descriptor.LocalForward = SanitizeDirection(descriptor.LocalForward, Vector3.forward);
                descriptor.LocalUp = SanitizeDirection(descriptor.LocalUp, Vector3.up);
                descriptor.MinQualityWeight = math.saturate(SanitizeFinite(descriptor.MinQualityWeight, 0f));
                descriptors[i] = descriptor;
            }
        }

        private static DroneAttachmentRuntimeData ToRuntimeData(DroneAttachmentAnchorDescriptor descriptor)
        {
            DroneAttachmentRuntimeData runtime = default;
            runtime.AnchorIndex = descriptor.AnchorIndex;
            runtime.BoneIndex = descriptor.BoneIndex;
            runtime.AnchorHash = descriptor.AnchorHash;
            runtime.Kind = (byte)descriptor.Kind;
            runtime.TierMask = (byte)descriptor.TierMask;
            runtime.Flags = (byte)descriptor.Flags;
            runtime.LocalPosition = (float3)(descriptor.LocalPosition);
            runtime.LocalForward = math.normalizesafe((float3)(descriptor.LocalForward), new float3(0f, 0f, 1f));
            runtime.LocalUp = math.normalizesafe((float3)(descriptor.LocalUp), new float3(0f, 1f, 0f));
            runtime.MinQualityWeight = math.saturate(descriptor.MinQualityWeight);
            return runtime;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static Vector3 SanitizeDirection(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
                return fallback;

            return value.normalized;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static Color SanitizeColor(Color value, Color fallback)
        {
            return math.isfinite(value.r) &&
                   math.isfinite(value.g) &&
                   math.isfinite(value.b) &&
                   math.isfinite(value.a)
                ? value
                : fallback;
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            uint authoredDroneId,
            uint authoredBakeHash,
            float globalQualityWeight,
            Transform authoredRigRoot,
            Transform[] authoredAnchors,
            DroneAttachmentAnchorDescriptor[] authoredDescriptors,
            Renderer[] authoredEmissionRenderers,
            Color idleColor,
            Color activeColor,
            Color faultColor)
        {
            droneId = authoredDroneId;
            bakeHash = authoredBakeHash;
            authoredQualityWeight = globalQualityWeight;
            rigRoot = authoredRigRoot;
            anchors = authoredAnchors ?? s_emptyAnchors;
            descriptors = authoredDescriptors ?? s_emptyDescriptors;
            emissionRenderers = authoredEmissionRenderers ?? s_emptyRenderers;
            idleEmissionColor = idleColor;
            activeEmissionColor = activeColor;
            faultEmissionColor = faultColor;
            SanitizeSerializedState();
        }

#endif
    }
}
