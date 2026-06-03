// ============================================================================
// HECTON-8 - EquipmentMetadata.cs
// Cold prefab component for interactive equipment socket metadata.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class EquipmentMetadata : MonoBehaviour
    {
        private const int MaxPendingRuntimeSocketPublications = VRInteractionKinematicBridgeConstants.SocketCapacity;

        // COLD ALLOC: EquipmentMetadata[128] - bounded retry queue for metadata enabled before Interaction/DataVault readiness - owner: EquipmentMetadata
        private static readonly EquipmentMetadata[] s_pendingRuntimeSocketPublications = new EquipmentMetadata[MaxPendingRuntimeSocketPublications];
        private static int s_pendingRuntimeSocketPublicationCount;

        [SerializeField] private uint equipmentId;
        [SerializeField] private uint bakeHash;
        [SerializeField] private float authoredQualityWeight;
        [SerializeField] private InteractionAnchorData[] interactionAnchors = Array.Empty<InteractionAnchorData>();

        private EquipmentInteractionHandler _runtimeSocketHandler;
        private int _runtimeSocketStartIndex = -1;
        private int _runtimeSocketSlotCount;
        private bool _runtimeSocketRegistered;

        public uint EquipmentId => equipmentId;
        public uint BakeHash => bakeHash;
        public float AuthoredQualityWeight => authoredQualityWeight;
        public int AnchorCount => interactionAnchors == null ? 0 : interactionAnchors.Length;

        public ReadOnlySpan<InteractionAnchorData> InteractionAnchors =>
            interactionAnchors == null
                ? ReadOnlySpan<InteractionAnchorData>.Empty
                : new ReadOnlySpan<InteractionAnchorData>(interactionAnchors);

        internal static bool HasPendingRuntimeSocketPublications => s_pendingRuntimeSocketPublicationCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeSocketPublicationState()
        {
            Array.Clear(s_pendingRuntimeSocketPublications, 0, s_pendingRuntimeSocketPublications.Length);
            s_pendingRuntimeSocketPublicationCount = 0;
        }

        private void OnEnable()
        {
            TryPublishRuntimeSockets();
        }

        private void Start()
        {
            TryPublishRuntimeSockets();
        }

        private void OnDisable()
        {
            UnpublishRuntimeSockets();
        }

        private void OnDestroy()
        {
            UnpublishRuntimeSockets();
        }

        public static bool ValidateStaticLayout()
        {
            int size = UnsafeUtility.SizeOf<InteractionAnchorData>();
            return size == EquipmentInteractionContractLayout.InteractionAnchorDataStrideBytes &&
                   (size & 7) == 0 &&
                   OffsetOf(nameof(InteractionAnchorData.LocalPosition)) == 0 &&
                   OffsetOf(nameof(InteractionAnchorData.LocalForward)) == 12 &&
                   OffsetOf(nameof(InteractionAnchorData.LocalUp)) == 24 &&
                   OffsetOf(nameof(InteractionAnchorData.SnapRadiusMeters)) == 36 &&
                   OffsetOf(nameof(InteractionAnchorData.AnchorId)) == 40 &&
                   OffsetOf(nameof(InteractionAnchorData.Flags)) == 44 &&
                   OffsetOf(nameof(InteractionAnchorData.HandMask)) == 48 &&
                   OffsetOf(nameof(InteractionAnchorData.SurfaceKind)) == 49;
        }

        public static bool ValidateAnchorSet(InteractionAnchorData[] anchors, out string failureReason)
        {
            if (anchors == null || anchors.Length == 0)
            {
                failureReason = "EquipmentMetadata anchor set is empty.";
                return false;
            }

            int activeCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                InteractionAnchorData anchor = anchors[i];
                if (anchor.AnchorId == 0u ||
                    !IsFinite(anchor.LocalPosition) ||
                    !IsFinite(anchor.LocalForward) ||
                    !IsFinite(anchor.LocalUp) ||
                    !math.isfinite(anchor.SnapRadiusMeters) ||
                    anchor.SnapRadiusMeters <= 0f ||
                    math.lengthsq(anchor.LocalForward) <= 0.000001f ||
                    math.lengthsq(anchor.LocalUp) <= 0.000001f ||
                    !IsAnchorOrientationUsable(anchor.LocalForward, anchor.LocalUp) ||
                    !IsKnownHandMask(anchor.HandMask) ||
                    !IsKnownSurfaceKind(anchor.SurfaceKind))
                {
                    failureReason = "EquipmentMetadata anchor validation failed.";
                    return false;
                }

                if ((anchor.Flags & InteractionAnchorData.FlagActive) != 0u)
                    activeCount++;

                for (int j = i + 1; j < anchors.Length; j++)
                {
                    if (anchors[j].AnchorId == anchor.AnchorId)
                    {
                        failureReason = "EquipmentMetadata duplicate anchor id.";
                        return false;
                    }
                }
            }

            if (activeCount == 0 || activeCount > VRInteractionKinematicBridgeConstants.SocketCapacity)
            {
                failureReason = "EquipmentMetadata active anchor count invalid.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryGetAnchor(int index, out InteractionAnchorData anchor)
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null || (uint)index >= (uint)anchors.Length)
            {
                anchor = default;
                return false;
            }

            anchor = anchors[index];
            return true;
        }

        public bool TryGetAnchorById(uint anchorId, out InteractionAnchorData anchor)
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null || anchorId == 0u)
            {
                anchor = default;
                return false;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                InteractionAnchorData candidate = anchors[i];
                if (candidate.AnchorId == anchorId)
                {
                    anchor = candidate;
                    return true;
                }
            }

            anchor = default;
            return false;
        }

        public int CountActiveAnchors()
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null)
                return 0;

            int count = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                if ((anchors[i].Flags & InteractionAnchorData.FlagActive) != 0u)
                    count++;
            }

            return count;
        }

        public int CopyAnchorsTo(NativeArray<InteractionAnchorData> destination)
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null || !destination.IsCreated)
                return 0;

            int count = anchors.Length < destination.Length ? anchors.Length : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = anchors[i];

            return count;
        }

        public int CopyAnchorsToSockets(
            NativeArray<VRInteractionSocketDTO> destination,
            int destinationStartIndex,
            double3 rootAup,
            quaternion localToWorldRotation)
        {
            return CopyAnchorsToSockets(
                destination,
                destinationStartIndex,
                rootAup,
                float3.zero,
                float4x4.TRS(float3.zero, localToWorldRotation, new float3(1f, 1f, 1f)));
        }

        public int CopyAnchorsToSockets(
            NativeArray<VRInteractionSocketDTO> destination,
            int destinationStartIndex,
            double3 rootAup,
            float3 rootRuntimePosition,
            float4x4 localToWorldMatrix)
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null ||
                !destination.IsCreated ||
                destinationStartIndex < 0 ||
                destinationStartIndex >= destination.Length ||
                !math.all(math.isfinite(rootAup)) ||
                !IsFinite(rootRuntimePosition) ||
                !IsFinite(localToWorldMatrix))
            {
                return 0;
            }

            int write = destinationStartIndex;
            for (int i = 0; i < anchors.Length && write < destination.Length; i++)
            {
                if (!TryBuildSocket(
                        in anchors[i],
                        rootAup,
                        rootRuntimePosition,
                        localToWorldMatrix,
                        out VRInteractionSocketDTO socket))
                {
                    continue;
                }

                destination[write++] = socket;
            }

            return write - destinationStartIndex;
        }

        public int CopyAnchorsToSockets(
            VRInteractionSocketDTO[] destination,
            int destinationStartIndex,
            double3 rootAup,
            quaternion localToWorldRotation)
        {
            return CopyAnchorsToSockets(
                destination,
                destinationStartIndex,
                rootAup,
                float3.zero,
                float4x4.TRS(float3.zero, localToWorldRotation, new float3(1f, 1f, 1f)));
        }

        public int CopyAnchorsToSockets(
            VRInteractionSocketDTO[] destination,
            int destinationStartIndex,
            double3 rootAup,
            float3 rootRuntimePosition,
            float4x4 localToWorldMatrix)
        {
            InteractionAnchorData[] anchors = interactionAnchors;
            if (anchors == null ||
                destination == null ||
                destinationStartIndex < 0 ||
                destinationStartIndex >= destination.Length ||
                !math.all(math.isfinite(rootAup)) ||
                !IsFinite(rootRuntimePosition) ||
                !IsFinite(localToWorldMatrix))
            {
                return 0;
            }

            int write = destinationStartIndex;
            for (int i = 0; i < anchors.Length && write < destination.Length; i++)
            {
                if (!TryBuildSocket(
                        in anchors[i],
                        rootAup,
                        rootRuntimePosition,
                        localToWorldMatrix,
                        out VRInteractionSocketDTO socket))
                {
                    continue;
                }

                destination[write++] = socket;
            }

            return write - destinationStartIndex;
        }

        internal static void FlushPendingRuntimeSocketPublications(EquipmentInteractionHandler handler, int maxAttempts)
        {
            if (!Application.isPlaying || handler == null || maxAttempts <= 0)
                return;

            int index = 0;
            int attempts = 0;
            while (index < s_pendingRuntimeSocketPublicationCount && attempts < maxAttempts)
            {
                attempts++;
                EquipmentMetadata metadata = s_pendingRuntimeSocketPublications[index];
                if (metadata == null || !metadata.isActiveAndEnabled)
                {
                    RemovePendingRuntimeSocketPublicationAt(index);
                    continue;
                }

                if (metadata.TryPublishRuntimeSocketsFromHandler(handler))
                {
                    if (index < s_pendingRuntimeSocketPublicationCount &&
                        ReferenceEquals(s_pendingRuntimeSocketPublications[index], metadata))
                    {
                        RemovePendingRuntimeSocketPublicationAt(index);
                    }

                    continue;
                }

                index++;
            }
        }

        internal bool TryPublishRuntimeSocketsFromHandler(EquipmentInteractionHandler handler)
        {
            if (!Application.isPlaying || _runtimeSocketRegistered || handler == null)
                return false;

            if (!handler.TryRegisterEquipmentSockets(this, transform, out int startIndex, out int slotCount))
                return false;

            _runtimeSocketHandler = handler;
            _runtimeSocketStartIndex = startIndex;
            _runtimeSocketSlotCount = slotCount;
            _runtimeSocketRegistered = true;
            RemovePendingRuntimeSocketPublication(this);
            return true;
        }

        private bool TryPublishRuntimeSockets()
        {
            if (!Application.isPlaying || _runtimeSocketRegistered)
                return false;

            EquipmentInteractionHandler handler = EquipmentInteractionHandler.ActiveRuntimeInstance;
            if (TryPublishRuntimeSocketsFromHandler(handler))
                return true;

            RegisterPendingRuntimeSocketPublication(this);
            return false;
        }

        private void UnpublishRuntimeSockets()
        {
            RemovePendingRuntimeSocketPublication(this);
            if (!_runtimeSocketRegistered)
                return;

            EquipmentInteractionHandler handler = _runtimeSocketHandler;
            if (handler != null)
                handler.UnregisterEquipmentSockets(this, _runtimeSocketStartIndex, _runtimeSocketSlotCount);

            _runtimeSocketHandler = null;
            _runtimeSocketStartIndex = -1;
            _runtimeSocketSlotCount = 0;
            _runtimeSocketRegistered = false;
        }

        private static void RegisterPendingRuntimeSocketPublication(EquipmentMetadata metadata)
        {
            if (metadata == null || !Application.isPlaying)
                return;

            for (int i = 0; i < s_pendingRuntimeSocketPublicationCount; i++)
            {
                if (ReferenceEquals(s_pendingRuntimeSocketPublications[i], metadata))
                    return;
            }

            if (s_pendingRuntimeSocketPublicationCount >= s_pendingRuntimeSocketPublications.Length)
                return;

            s_pendingRuntimeSocketPublications[s_pendingRuntimeSocketPublicationCount++] = metadata;
        }

        private static void RemovePendingRuntimeSocketPublication(EquipmentMetadata metadata)
        {
            if (metadata == null)
                return;

            for (int i = 0; i < s_pendingRuntimeSocketPublicationCount; i++)
            {
                if (ReferenceEquals(s_pendingRuntimeSocketPublications[i], metadata))
                {
                    RemovePendingRuntimeSocketPublicationAt(i);
                    return;
                }
            }
        }

        private static void RemovePendingRuntimeSocketPublicationAt(int index)
        {
            if ((uint)index >= (uint)s_pendingRuntimeSocketPublicationCount)
                return;

            int last = s_pendingRuntimeSocketPublicationCount - 1;
            for (int i = index; i < last; i++)
                s_pendingRuntimeSocketPublications[i] = s_pendingRuntimeSocketPublications[i + 1];

            s_pendingRuntimeSocketPublications[last] = null;
            s_pendingRuntimeSocketPublicationCount = last;
        }

        private static uint ResolveSocketFlags(InteractionAnchorData anchor)
        {
            uint flags = (anchor.Flags & InteractionAnchorData.FlagActive) != 0u
                ? VRInteractionKinematicBridgeConstants.SocketFlagActive
                : 0u;
            if ((anchor.Flags & InteractionAnchorData.FlagTwoHanded) != 0u)
                flags |= VRInteractionKinematicBridgeConstants.SocketFlagTwoHanded;

            byte handMask = (byte)(anchor.HandMask & InteractionAnchorData.HandMaskBoth);
            handMask = handMask == 0
                ? InteractionAnchorData.HandMaskBoth
                : handMask;
            if ((handMask & InteractionAnchorData.HandMaskLeft) != 0)
                flags |= VRInteractionKinematicBridgeConstants.SocketFlagHandLeft;
            if ((handMask & InteractionAnchorData.HandMaskRight) != 0)
                flags |= VRInteractionKinematicBridgeConstants.SocketFlagHandRight;

            byte surfaceKind = IsKnownSurfaceKind(anchor.SurfaceKind) ? anchor.SurfaceKind : (byte)0;
            flags |= ((uint)surfaceKind << VRInteractionKinematicBridgeConstants.SocketSurfaceKindShift) &
                     VRInteractionKinematicBridgeConstants.SocketSurfaceKindMask;
            return flags;
        }

        private static bool TryBuildSocket(
            in InteractionAnchorData anchor,
            double3 rootAup,
            float3 rootRuntimePosition,
            float4x4 localToWorldMatrix,
            out VRInteractionSocketDTO socket)
        {
            socket = default;
            if (!IsFinite(anchor.LocalPosition) ||
                !IsFinite(anchor.LocalForward) ||
                !IsFinite(anchor.LocalUp) ||
                !math.isfinite(anchor.SnapRadiusMeters) ||
                anchor.SnapRadiusMeters <= 0f)
            {
                return false;
            }

            uint socketFlags = ResolveSocketFlags(anchor);
            if ((socketFlags & VRInteractionKinematicBridgeConstants.SocketFlagActive) == 0u)
                return false;

            float3 worldPosition = math.transform(localToWorldMatrix, anchor.LocalPosition);
            if (!IsFinite(worldPosition))
                return false;

            float3 worldOffset = worldPosition - rootRuntimePosition;
            float3 forward = math.normalizesafe(TransformVector(localToWorldMatrix, anchor.LocalForward), new float3(0f, 0f, 1f));
            float3 up = ResolveOrthonormalUp(forward, TransformVector(localToWorldMatrix, anchor.LocalUp));
            socket = new VRInteractionSocketDTO
            {
                SocketAUP = rootAup + new double3(worldOffset.x, worldOffset.y, worldOffset.z),
                Orientation = quaternion.LookRotationSafe(forward, up),
                Normal = forward,
                SnapRadiusMeters = anchor.SnapRadiusMeters,
                SocketId = anchor.AnchorId,
                Flags = socketFlags
            };
            return true;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        private static float3 TransformVector(float4x4 matrix, float3 vector)
        {
            return new float3(
                matrix.c0.x * vector.x + matrix.c1.x * vector.y + matrix.c2.x * vector.z,
                matrix.c0.y * vector.x + matrix.c1.y * vector.y + matrix.c2.y * vector.z,
                matrix.c0.z * vector.x + matrix.c1.z * vector.y + matrix.c2.z * vector.z);
        }

        private static bool IsKnownHandMask(byte handMask)
        {
            return handMask == 0 || (handMask & ~InteractionAnchorData.HandMaskBoth) == 0;
        }

        private static bool IsKnownSurfaceKind(byte surfaceKind)
        {
            return surfaceKind == InteractionAnchorData.SurfaceKindLever ||
                   surfaceKind == InteractionAnchorData.SurfaceKindValve ||
                   surfaceKind == InteractionAnchorData.SurfaceKindToggle;
        }

        private static bool IsAnchorOrientationUsable(float3 forward, float3 up)
        {
            float3 f = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            float3 u = math.normalizesafe(up, new float3(0f, 1f, 0f));
            return math.abs(math.dot(f, u)) < 0.985f;
        }

        private static float3 ResolveOrthonormalUp(float3 forward, float3 up)
        {
            float3 projected = up - forward * math.dot(up, forward);
            float lengthSq = math.lengthsq(projected);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
            {
                float3 helper = math.abs(forward.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                projected = helper - forward * math.dot(helper, forward);
            }

            return math.normalizesafe(projected, new float3(0f, 1f, 0f));
        }

        private static int OffsetOf(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(InteractionAnchorData).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

#if UNITY_EDITOR
        public void SetEditorBakeData(
            uint authoredEquipmentId,
            uint authoredBakeHash,
            float globalQualityWeight,
            InteractionAnchorData[] authoredAnchors)
        {
            equipmentId = authoredEquipmentId;
            bakeHash = authoredBakeHash;
            authoredQualityWeight = globalQualityWeight < 0f ? 0f : globalQualityWeight > 1f ? 1f : globalQualityWeight;
            interactionAnchors = authoredAnchors ?? Array.Empty<InteractionAnchorData>();
        }
#endif
    }
}
