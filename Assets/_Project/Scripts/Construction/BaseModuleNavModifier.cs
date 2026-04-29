using System;
using Hecton8.Gameplay;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Registers authored primitive colliders as cold-path voxel-nav obstacles so fauna pathing respects habitat shells.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BaseModule))]
    public sealed class BaseModuleNavModifier : MonoBehaviour
    {
        private const float MinimumFloraExclusionRadiusMeters = 1f;
        private const float FloraExclusionRadiusPaddingMeters = 1.25f;

        [Header("── Primitive Sources ──────────────────")]
        [Tooltip("Box colliders that represent the module shell for voxel-nav carving.")]
        [SerializeField] private BoxCollider[] obstacleBoxes = Array.Empty<BoxCollider>();

        [Tooltip("Capsule colliders that represent the module shell for voxel-nav carving.")]
        [SerializeField] private CapsuleCollider[] obstacleCapsules = Array.Empty<CapsuleCollider>();

        private int _obstacleId;
        private int _terrainHoleHandle;

        private void Awake()
        {
            _obstacleId = unchecked((int)EntityId.ToULong(GetEntityId()));
        }

        private void OnEnable()
        {
            VoxelDynamicNavGridRuntime.RegisterModuleObstacle(_obstacleId, obstacleBoxes, obstacleCapsules);
            RefreshVegetationExclusion();
        }

        private void OnDisable()
        {
            VoxelDynamicNavGridRuntime.UnregisterModuleObstacle(_obstacleId);
            ClearVegetationExclusion();
        }

        /// <summary>
        /// Applies the prefab-authored primitive collider sources used for voxel-nav carving.
        /// </summary>
        /// <param name="boxes">Box-collider sources owned by this prefab.</param>
        /// <param name="capsules">Capsule-collider sources owned by this prefab.</param>
        public void ConfigureColliderSources(BoxCollider[] boxes, CapsuleCollider[] capsules)
        {
            obstacleBoxes = boxes ?? Array.Empty<BoxCollider>();
            obstacleCapsules = capsules ?? Array.Empty<CapsuleCollider>();
        }

        internal void RefreshVegetationExclusion()
        {
            ClearVegetationExclusion();
            if (!TryResolveObstacleEnvelope(out Vector3 worldCenter, out float horizontalRadius))
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                _terrainHoleHandle = vegetationBridge.RegisterTerrainHoleHandle(worldCenter, horizontalRadius);

            DestructibleOrganicManager.ActiveRuntimeInstance?.ApplyConstructionDecomposition(worldCenter, horizontalRadius);
        }

        private void ClearVegetationExclusion()
        {
            if (_terrainHoleHandle <= 0)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                vegetationBridge.UnregisterTerrainHole(_terrainHoleHandle);

            _terrainHoleHandle = 0;
        }

        private bool TryResolveObstacleEnvelope(out Vector3 worldCenter, out float horizontalRadius)
        {
            horizontalRadius = 0f;
            Bounds combinedBounds = default;
            bool hasBounds = false;

            hasBounds |= TryAccumulateBounds(obstacleBoxes, ref combinedBounds);
            hasBounds |= TryAccumulateBounds(obstacleCapsules, ref combinedBounds);
            if (!hasBounds)
            {
                worldCenter = transform.position;
                return false;
            }

            Vector3 extents = combinedBounds.extents;
            float planarRadius = Mathf.Sqrt((extents.x * extents.x) + (extents.z * extents.z));
            horizontalRadius = Mathf.Max(MinimumFloraExclusionRadiusMeters, planarRadius + FloraExclusionRadiusPaddingMeters);
            worldCenter = combinedBounds.center;
            return true;
        }

        private static bool TryAccumulateBounds<T>(T[] colliders, ref Bounds combinedBounds)
            where T : Collider
        {
            if (colliders == null || colliders.Length <= 0)
                return false;

            bool hasBounds = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                T collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                combinedBounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            obstacleBoxes ??= Array.Empty<BoxCollider>();
            obstacleCapsules ??= Array.Empty<CapsuleCollider>();
        }
#endif
    }
}
