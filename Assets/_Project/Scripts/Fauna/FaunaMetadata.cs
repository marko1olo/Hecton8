using System;
using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.AI
{
    [Flags]
    public enum FaunaMetadataFlags : uint
    {
        None = 0u,
        GpuSkinned = 1u << 0,
        VatSwarm = 1u << 1,
        PrimitiveHitboxes = 1u << 2,
        FineHitboxCulling = 1u << 3,
        RenderBounds = 1u << 4,
        BiolumPresentationLights = 1u << 5
    }

    public enum FaunaLocomotionType : byte
    {
        Unknown = 0,
        Swimmer = 1,
        Crawler = 2,
        Ambush = 3,
        Drifting = 4,
        Burrowing = 5,
        Tentacled = 6,
        Armored = 7
    }

    [Flags]
    public enum FaunaSensoryChannels : uint
    {
        None = 0u,
        Sound = 1u << 0,
        Light = 1u << 1,
        ElectricalPower = 1u << 2,
        BloodChemistry = 1u << 3,
        Territory = 1u << 4,
        SonarPing = 1u << 5
    }

    [DisallowMultipleComponent]
    public sealed class FaunaMetadata : MonoBehaviour, IPhysicsColliderLodTransitionSink, IPhysicsCullingColliderCache
    {
        public const int MaxPhysicsCullingColliderCount = 4;
        public const int MaxFineHitboxColliderCount = 16;
        public const int MaxBiolumPresentationLightCount = 4;

        [SerializeField] private Transform sensoryAnchor;
        [SerializeField] private Collider aggregateHitbox;
        [SerializeField] private Collider[] physicsCullingColliders = Array.Empty<Collider>();
        [SerializeField] private Collider[] fineHitboxColliders = Array.Empty<Collider>();
        [SerializeField] private Light[] biolumPresentationLights = Array.Empty<Light>();
        [SerializeField] private Texture2D vatPositionTexture;
        [SerializeField] private Texture2D vatNormalTexture;
        [SerializeField] private Vector4 vatPositionScaleBias = new Vector4(1f, 1f, 0f, 0f);
        [SerializeField] private Vector4 vatNormalScaleBias = new Vector4(1f, 1f, 0f, 0f);
        [SerializeField] private Vector4 vatPhaseOffsetScale = new Vector4(0f, 1f, 0f, 0f);
        [SerializeField] private Bounds localRenderBounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private FaunaLocomotionType locomotionType = FaunaLocomotionType.Unknown;
        [SerializeField] private FaunaSensoryChannels sensoryChannels = FaunaSensoryChannels.None;
        [SerializeField] private FaunaMetadataFlags flags;
        [NonSerialized] private byte fineHitboxDistanceGateOpen;
        [NonSerialized] private byte logicalColliderSuppressionOpen;

        public Transform SensoryAnchor => sensoryAnchor;
        public Collider AggregateHitbox => aggregateHitbox;
        public Texture2D VatPositionTexture => vatPositionTexture;
        public Texture2D VatNormalTexture => vatNormalTexture;
        public Vector4 VatPositionScaleBias => vatPositionScaleBias;
        public Vector4 VatNormalScaleBias => vatNormalScaleBias;
        public Vector4 VatPhaseOffsetScale => vatPhaseOffsetScale;
        public Bounds LocalRenderBounds => localRenderBounds;
        public FaunaLocomotionType LocomotionType => locomotionType;
        public FaunaSensoryChannels SensoryChannels => sensoryChannels;
        public FaunaMetadataFlags Flags => flags;

        private void OnEnable()
        {
            RestoreColliderLodState();
        }

        private void OnDisable()
        {
            if (gameObject.activeInHierarchy)
            {
                RestoreColliderLodState();
                return;
            }

            fineHitboxDistanceGateOpen = 0;
            logicalColliderSuppressionOpen = 0;
        }

        public int PhysicsCullingColliderCount
        {
            get
            {
                Collider[] colliders = physicsCullingColliders;
                return ResolveSafeCount(colliders, MaxPhysicsCullingColliderCount);
            }
        }

        public int FineHitboxColliderCount
        {
            get
            {
                Collider[] colliders = fineHitboxColliders;
                return ResolveSafeCount(colliders, MaxFineHitboxColliderCount);
            }
        }

        public int BiolumPresentationLightCount
        {
            get
            {
                Light[] lights = biolumPresentationLights;
                return ResolveSafeCount(lights, MaxBiolumPresentationLightCount);
            }
        }

        public bool TryGetPhysicsCullingColliders(out Collider[] colliders, out int count)
        {
            colliders = physicsCullingColliders;
            count = ResolveSafeCount(colliders, MaxPhysicsCullingColliderCount);
            return count > 0;
        }

        public bool TryGetFineHitboxColliders(out Collider[] colliders, out int count)
        {
            colliders = fineHitboxColliders;
            count = ResolveSafeCount(colliders, MaxFineHitboxColliderCount);
            return count > 0;
        }

        public bool TryGetBiolumPresentationLights(out Light[] lights, out int count)
        {
            lights = biolumPresentationLights;
            count = ResolveSafeCount(lights, MaxBiolumPresentationLightCount);
            return count > 0;
        }

        public bool TryGetVatTextures(out Texture2D positionTexture, out Texture2D normalTexture)
        {
            positionTexture = vatPositionTexture;
            normalTexture = vatNormalTexture;
            return positionTexture != null && normalTexture != null;
        }

        public bool TryGetLocalRenderBounds(out Bounds bounds)
        {
            bounds = localRenderBounds;
            return (flags & FaunaMetadataFlags.RenderBounds) != 0 &&
                   IsFinite(bounds.center) &&
                   IsFinite(bounds.extents);
        }

        public int SetLogicalColliderSuppression(bool suppressColliders)
        {
            byte nextState = suppressColliders ? (byte)1 : (byte)0;
            if (logicalColliderSuppressionOpen == nextState)
                return 0;

            logicalColliderSuppressionOpen = nextState;
            return ApplyColliderEnabledState();
        }

        public void SetColliderLodDistanceGate(bool allowSimplifiedColliderLod)
        {
            SetColliderLodDistanceGateAndCountTransitions(allowSimplifiedColliderLod);
        }

        public int SetColliderLodDistanceGateAndCountTransitions(bool allowSimplifiedColliderLod)
        {
            byte nextState = allowSimplifiedColliderLod ? (byte)1 : (byte)0;
            if (fineHitboxDistanceGateOpen == nextState)
                return 0;

            fineHitboxDistanceGateOpen = nextState;
            return ApplyColliderEnabledState();
        }

        private int ApplyColliderEnabledState()
        {
            int transitionCount = 0;
            bool aggregateEnabled = logicalColliderSuppressionOpen == 0;
            Collider aggregate = aggregateHitbox;
            if (aggregate != null && aggregate.enabled != aggregateEnabled)
            {
                aggregate.enabled = aggregateEnabled;
                transitionCount++;
            }

            bool fineCollidersEnabled = aggregateEnabled && fineHitboxDistanceGateOpen == 0;
            Collider[] colliders = fineHitboxColliders;
            int count = ResolveSafeCount(colliders, MaxFineHitboxColliderCount);
            for (int i = 0; i < count; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled != fineCollidersEnabled)
                {
                    collider.enabled = fineCollidersEnabled;
                    transitionCount++;
                }
            }

            return transitionCount;
        }

        private void RestoreColliderLodState()
        {
            fineHitboxDistanceGateOpen = 0;
            logicalColliderSuppressionOpen = 0;
            Collider aggregate = aggregateHitbox;
            if (aggregate != null && !aggregate.enabled)
                aggregate.enabled = true;

            Collider[] colliders = fineHitboxColliders;
            int count = ResolveSafeCount(colliders, MaxFineHitboxColliderCount);
            for (int i = 0; i < count; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && !collider.enabled)
                    collider.enabled = true;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static int ResolveSafeCount<T>(T[] values, int maxCount)
        {
            if (values == null || maxCount <= 0)
                return 0;

            return values.Length < maxCount ? values.Length : maxCount;
        }

#if UNITY_EDITOR
        public int EditorPhysicsCullingColliderSerializedLength
        {
            get
            {
                Collider[] colliders = physicsCullingColliders;
                return colliders != null ? colliders.Length : 0;
            }
        }

        public int EditorFineHitboxColliderSerializedLength
        {
            get
            {
                Collider[] colliders = fineHitboxColliders;
                return colliders != null ? colliders.Length : 0;
            }
        }

        public int EditorBiolumPresentationLightSerializedLength
        {
            get
            {
                Light[] lights = biolumPresentationLights;
                return lights != null ? lights.Length : 0;
            }
        }

        public void EditorConfigure(
            Transform newSensoryAnchor,
            Collider newAggregateHitbox,
            Collider[] newPhysicsCullingColliders,
            Collider[] newFineHitboxColliders,
            Light[] newBiolumPresentationLights,
            Texture2D newVatPositionTexture,
            Texture2D newVatNormalTexture,
            Vector4 newVatPositionScaleBias,
            Vector4 newVatNormalScaleBias,
            Vector4 newVatPhaseOffsetScale,
            Bounds newLocalRenderBounds,
            FaunaLocomotionType newLocomotionType,
            FaunaSensoryChannels newSensoryChannels,
            FaunaMetadataFlags newFlags)
        {
            sensoryAnchor = newSensoryAnchor;
            aggregateHitbox = newAggregateHitbox;
            physicsCullingColliders = newPhysicsCullingColliders ?? Array.Empty<Collider>();
            fineHitboxColliders = newFineHitboxColliders ?? Array.Empty<Collider>();
            biolumPresentationLights = newBiolumPresentationLights ?? Array.Empty<Light>();
            vatPositionTexture = newVatPositionTexture;
            vatNormalTexture = newVatNormalTexture;
            vatPositionScaleBias = newVatPositionScaleBias;
            vatNormalScaleBias = newVatNormalScaleBias;
            vatPhaseOffsetScale = newVatPhaseOffsetScale;
            localRenderBounds = newLocalRenderBounds;
            locomotionType = newLocomotionType;
            sensoryChannels = newSensoryChannels;
            flags = newFlags;
        }
#endif
    }
}
