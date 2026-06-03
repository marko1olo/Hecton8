namespace Hecton8.Inventory
{
    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    public enum InventoryContainerKind : byte
    {
        Loot = 0,
        Container = 1,
        Locker = 2
    }

    /// <summary>
    /// Passive prefab metadata for storage/locker lid motion and capacity identity.
    /// It stores baked hinge data only; it does not own runtime inventory truth.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Inventory/Container Metadata")]
    public sealed class ContainerMetadata : MonoBehaviour
    {
        [SerializeField] private uint containerId;
        [SerializeField] private uint bakeHash;
        [SerializeField] private int itemHashId;
        [SerializeField] private InventoryContainerKind containerKind = InventoryContainerKind.Container;
        [SerializeField] private Transform lidTransform;
        [SerializeField] private Transform ikHandle;
        [SerializeField] private Vector3 localLidPivot;
        [SerializeField] private Vector3 localLidAxis = Vector3.right;
        [SerializeField] private Vector3 localLidClosedForward = Vector3.forward;
        [SerializeField] private float minOpenDegrees;
        [SerializeField] private float maxOpenDegrees = 95f;
        [SerializeField] private float baseWeightKg = 1f;
        [SerializeField] private float capacityWeightKg = 20f;
        [SerializeField] private ushort slotCapacity = 8;
        [SerializeField] private int[] slotConnectivity = Array.Empty<int>();
        [SerializeField] private byte flags;
        [SerializeField] private float authoredQualityWeight = 1f;

        public uint ContainerId => containerId;
        public uint BakeHash => bakeHash;
        public int ItemHashId => itemHashId;
        public InventoryContainerKind ContainerKind => containerKind;
        public Transform LidTransform => lidTransform;
        public Transform IkHandle => ikHandle;
        public Vector3 LocalLidPivot => localLidPivot;
        public Vector3 LocalLidAxis => localLidAxis;
        public Vector3 LocalLidClosedForward => localLidClosedForward;
        public float MinOpenDegrees => minOpenDegrees;
        public float MaxOpenDegrees => maxOpenDegrees;
        public float BaseWeightKg => baseWeightKg;
        public float CapacityWeightKg => capacityWeightKg;
        public ushort SlotCapacity => slotCapacity;
        public int SlotConnectivityCount => slotConnectivity != null ? slotConnectivity.Length : 0;
        public ReadOnlySpan<int> SlotConnectivity =>
            slotConnectivity == null || slotConnectivity.Length == 0
                ? ReadOnlySpan<int>.Empty
                : new ReadOnlySpan<int>(slotConnectivity);
        public byte Flags => flags;
        public float AuthoredQualityWeight => authoredQualityWeight;
        public ulong ContainerHash64 => ((ulong)containerId << 32) | bakeHash;
        public bool IsValid =>
            containerId != 0u &&
            itemHashId != 0 &&
            ikHandle != null &&
            slotCapacity != 0 &&
            HasValidSlotConnectivity() &&
            IsFiniteVector(localLidPivot) &&
            IsFiniteVector(localLidAxis) &&
            localLidAxis.sqrMagnitude > 0.000001f &&
            IsFiniteVector(localLidClosedForward) &&
            localLidClosedForward.sqrMagnitude > 0.000001f &&
            IsFinite(minOpenDegrees) &&
            IsFinite(maxOpenDegrees) &&
            IsFinite(baseWeightKg) &&
            baseWeightKg > 0f &&
            IsFinite(capacityWeightKg) &&
            capacityWeightKg >= 0f &&
            IsFinite(authoredQualityWeight) &&
            maxOpenDegrees >= minOpenDegrees + 1f;

        public bool TryGetLidAxis(out Vector3 axis)
        {
            axis = localLidAxis;
            return IsFiniteVector(axis) && axis.sqrMagnitude > 0.000001f;
        }

        public bool TryBuildContainerRange(int slotStart, ulong containerAupHash, int activeSlotCount, out InventoryContainerRangeDTO range)
        {
            range = default;
            if (!IsValid || slotCapacity == 0)
                return false;

            int safeSlotStart = Mathf.Max(0, slotStart);
            int safeActiveSlots = Mathf.Clamp(activeSlotCount, 0, slotCapacity);
            range.ContainerHash = ContainerHash64;
            range.ContainerAUPHash = containerAupHash;
            range.SlotStart = safeSlotStart;
            range.SlotCapacity = slotCapacity;
            range.ActiveSlotCount = safeActiveSlots;
            range.StateFlags = InventoryRoutingNetwork.ContainerRangeActive;
            return true;
        }

        public int CopySlotConnectivityTo(NativeArray<int> destination)
        {
            if (!destination.IsCreated || slotConnectivity == null)
                return 0;

            int count = Mathf.Min(destination.Length, slotConnectivity.Length);
            for (int i = 0; i < count; i++)
                destination[i] = slotConnectivity[i];
            return count;
        }

        public static bool ValidateContainerRangeDtoLayout()
        {
            int size = UnsafeUtility.SizeOf<InventoryContainerRangeDTO>();
            return size == InventoryRoutingNetwork.InventoryContainerRangeDtoSizeBytes && (size & 7) == 0;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            if (!IsFiniteVector(localLidAxis) || localLidAxis.sqrMagnitude <= 0.000001f)
                localLidAxis = Vector3.right;
            else
                localLidAxis.Normalize();

            if (!IsFiniteVector(localLidClosedForward) || localLidClosedForward.sqrMagnitude <= 0.000001f)
                localLidClosedForward = Vector3.forward;
            else
                localLidClosedForward.Normalize();

            if (!IsFiniteVector(localLidPivot))
                localLidPivot = Vector3.zero;

            if (!IsFinite(minOpenDegrees))
                minOpenDegrees = 0f;
            if (!IsFinite(maxOpenDegrees))
                maxOpenDegrees = 95f;
            if (maxOpenDegrees < minOpenDegrees + 1f)
                maxOpenDegrees = minOpenDegrees + 1f;

            if (!IsFinite(baseWeightKg) || baseWeightKg < 0.05f)
                baseWeightKg = 0.05f;
            if (!IsFinite(capacityWeightKg) || capacityWeightKg < 0f)
                capacityWeightKg = 0f;
            if (slotCapacity == 0)
                slotCapacity = 1;

            SanitizeSlotConnectivity();
            authoredQualityWeight = Mathf.Clamp01(IsFinite(authoredQualityWeight) ? authoredQualityWeight : 1f);
        }

        private void SanitizeSlotConnectivity()
        {
            int count = Mathf.Max(1, slotCapacity);
            if (slotConnectivity == null || slotConnectivity.Length != count || !HasValidSlotConnectivity())
            {
                slotConnectivity = new int[count];
                for (int i = 0; i < count; i++)
                    slotConnectivity[i] = i;
                return;
            }
        }

        private bool HasValidSlotConnectivity()
        {
            if (slotCapacity == 0 || slotConnectivity == null || slotConnectivity.Length != slotCapacity)
                return false;

            for (int i = 0; i < slotConnectivity.Length; i++)
            {
                int value = slotConnectivity[i];
                if ((uint)value >= slotCapacity)
                    return false;

                for (int j = i + 1; j < slotConnectivity.Length; j++)
                {
                    if (slotConnectivity[j] == value)
                        return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            uint authoredContainerId,
            uint authoredBakeHash,
            int authoredItemHashId,
            InventoryContainerKind authoredKind,
            Transform authoredLidTransform,
            Transform authoredIkHandle,
            Vector3 authoredLocalLidPivot,
            Vector3 authoredLocalLidAxis,
            Vector3 authoredLocalClosedForward,
            float authoredMinOpenDegrees,
            float authoredMaxOpenDegrees,
            float authoredBaseWeightKg,
            float authoredCapacityWeightKg,
            ushort authoredSlotCapacity,
            byte authoredFlags,
            float globalQualityWeight)
        {
            ConfigureEditorBake(
                authoredContainerId,
                authoredBakeHash,
                authoredItemHashId,
                authoredKind,
                authoredLidTransform,
                authoredIkHandle,
                authoredLocalLidPivot,
                authoredLocalLidAxis,
                authoredLocalClosedForward,
                authoredMinOpenDegrees,
                authoredMaxOpenDegrees,
                authoredBaseWeightKg,
                authoredCapacityWeightKg,
                authoredSlotCapacity,
                null,
                authoredFlags,
                globalQualityWeight);
        }

        public void ConfigureEditorBake(
            uint authoredContainerId,
            uint authoredBakeHash,
            int authoredItemHashId,
            InventoryContainerKind authoredKind,
            Transform authoredLidTransform,
            Transform authoredIkHandle,
            Vector3 authoredLocalLidPivot,
            Vector3 authoredLocalLidAxis,
            Vector3 authoredLocalClosedForward,
            float authoredMinOpenDegrees,
            float authoredMaxOpenDegrees,
            float authoredBaseWeightKg,
            float authoredCapacityWeightKg,
            ushort authoredSlotCapacity,
            int[] authoredSlotConnectivity,
            byte authoredFlags,
            float globalQualityWeight)
        {
            containerId = authoredContainerId;
            bakeHash = authoredBakeHash;
            itemHashId = authoredItemHashId;
            containerKind = authoredKind;
            lidTransform = authoredLidTransform;
            ikHandle = authoredIkHandle;
            localLidPivot = authoredLocalLidPivot;
            localLidAxis = authoredLocalLidAxis;
            localLidClosedForward = authoredLocalClosedForward;
            minOpenDegrees = authoredMinOpenDegrees;
            maxOpenDegrees = authoredMaxOpenDegrees;
            baseWeightKg = authoredBaseWeightKg;
            capacityWeightKg = authoredCapacityWeightKg;
            slotCapacity = authoredSlotCapacity;
            slotConnectivity = authoredSlotConnectivity ?? Array.Empty<int>();
            flags = authoredFlags;
            authoredQualityWeight = globalQualityWeight;
            SanitizeSerializedState();
        }
#endif
    }
}
