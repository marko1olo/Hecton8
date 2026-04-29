using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Building
{
    /// <summary>
    /// Stable authoring template for habitat-module snap topology and logistics-facing scalar data.
    /// Runtime systems must treat this asset as immutable.
    /// </summary>
    [CreateAssetMenu(fileName = "BaseModuleTemplate_", menuName = "Hecton8/Building/Base Module Template", order = 18)]
    public sealed class BaseModuleTemplate : ScriptableObject
    {
        [Serializable]
        public struct ItemHashCost
        {
            [Tooltip("Stable item hash resolved from ItemData.PersistentId.")]
            [SerializeField] private int itemHashId;

            [Tooltip("Required quantity for this module template.")]
            [SerializeField, Min(1)] private int amount;

            /// <summary>Stable item hash resolved from ItemData.PersistentId.</summary>
            public int ItemHashId => itemHashId;

            /// <summary>Required quantity for this module template.</summary>
            public int Amount => math.max(1, amount);
        }

        [Header("── Topology ──────────────────")]
        [Tooltip("Module-local snap points expressed in relative float3 coordinates.")]
        [SerializeField] private float3[] snapPoints = Array.Empty<float3>();

        [Header("── Construction ──────────────")]
        [Tooltip("Data-oriented build cost expressed as stable item hashes.")]
        [SerializeField] private ItemHashCost[] buildCost = Array.Empty<ItemHashCost>();

        [Header("── Simulation ────────────────")]
        [Tooltip("Continuous power draw in kilowatts for this template.")]
        [SerializeField, Min(0f)] private float powerDrawKW;

        [Tooltip("Pressurized air volume contributed by this module in cubic meters.")]
        [SerializeField, Min(0f)] private float airVolumeM3 = 1f;

        /// <summary>Module-local snap points expressed in relative float3 coordinates.</summary>
        public float3[] SnapPoints => snapPoints;

        /// <summary>Data-oriented build cost expressed as stable item hashes.</summary>
        public ItemHashCost[] BuildCost => buildCost;

        /// <summary>Continuous power draw in kilowatts for this template.</summary>
        public float PowerDrawKW => powerDrawKW;

        /// <summary>Pressurized air volume contributed by this module in cubic meters.</summary>
        public float AirVolumeM3 => airVolumeM3;

        private void OnValidate()
        {
            powerDrawKW = math.max(0f, powerDrawKW);
            airVolumeM3 = math.max(0f, airVolumeM3);
        }
    }
}
