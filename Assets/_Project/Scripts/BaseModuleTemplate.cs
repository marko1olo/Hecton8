using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Building
{
    public enum BaseModuleVfxSocketType : byte
    {
        Leak = 0,
        Spark = 1,
        Vent = 2
    }

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

        [Serializable]
        public struct VfxSocket
        {
            [Tooltip("Module-local socket coordinate in template space.")]
            [SerializeField] private float3 localPosition;

            [Tooltip("VFX semantic routed when the module degrades below the authored threshold.")]
            [SerializeField] private BaseModuleVfxSocketType socketType;

            /// <summary>Module-local socket coordinate in template space.</summary>
            public float3 LocalPosition => localPosition;

            /// <summary>VFX semantic routed when the module degrades below the authored threshold.</summary>
            public BaseModuleVfxSocketType SocketType => socketType;
        }

        [Header("── Identity ──────────────────")]
        [Tooltip("Stable authoring ID used for hashes, ledgers, and external references.")]
        [SerializeField] private string stableId = string.Empty;

        [Tooltip("Stable hash resolved from stableId. Generated automatically in OnValidate.")]
        [SerializeField] private int templateHashId;

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

        [Header("── Integrity Authoring ───────")]
        [Tooltip("Default normalized integrity state used by procedural abandoned-habitat spawning. 1.0 = pristine.")]
        [SerializeField, Range(0f, 1f)] private float defaultIntegrityState = 1f;

        [Tooltip("Integrity threshold below which the module should begin in a flooded state.")]
        [SerializeField, Range(0f, 1f)] private float floodedBelowIntegrityState = 0.45f;

        [Tooltip("Integrity threshold below which breathable reserve should be considered offline.")]
        [SerializeField, Range(0f, 1f)] private float oxygenOfflineBelowIntegrityState = 0.35f;

        [Header("── VFX Hardpoints ────────────")]
        [Tooltip("Pre-authored module-local VFX sockets used by degradation routing.")]
        [SerializeField] private VfxSocket[] vfxSockets = Array.Empty<VfxSocket>();

        /// <summary>Stable authoring ID used for hashes, ledgers, and external references.</summary>
        public string PersistentId => stableId;

        /// <summary>Stable hash resolved from stableId.</summary>
        public int TemplateHashId => templateHashId;

        /// <summary>Module-local snap points expressed in relative float3 coordinates.</summary>
        public float3[] SnapPoints => snapPoints;

        /// <summary>Data-oriented build cost expressed as stable item hashes.</summary>
        public ItemHashCost[] BuildCost => buildCost;

        /// <summary>Continuous power draw in kilowatts for this template.</summary>
        public float PowerDrawKW => powerDrawKW;

        /// <summary>Pressurized air volume contributed by this module in cubic meters.</summary>
        public float AirVolumeM3 => airVolumeM3;

        /// <summary>Default normalized integrity state used by procedural abandoned-habitat spawning.</summary>
        public float DefaultIntegrityState => defaultIntegrityState;

        /// <summary>Integrity threshold below which the module should begin flooded.</summary>
        public float FloodedBelowIntegrityState => floodedBelowIntegrityState;

        /// <summary>Integrity threshold below which breathable reserve should collapse.</summary>
        public float OxygenOfflineBelowIntegrityState => oxygenOfflineBelowIntegrityState;

        /// <summary>Pre-authored module-local VFX sockets used by degradation routing.</summary>
        public VfxSocket[] VfxSockets => vfxSockets;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = name;

            powerDrawKW = math.max(0f, powerDrawKW);
            airVolumeM3 = math.max(0f, airVolumeM3);
            defaultIntegrityState = math.clamp(defaultIntegrityState, 0f, 1f);
            floodedBelowIntegrityState = math.clamp(floodedBelowIntegrityState, 0f, 1f);
            oxygenOfflineBelowIntegrityState = math.clamp(oxygenOfflineBelowIntegrityState, 0f, 1f);
            if (oxygenOfflineBelowIntegrityState > floodedBelowIntegrityState)
                oxygenOfflineBelowIntegrityState = floodedBelowIntegrityState;

            templateHashId = string.IsNullOrWhiteSpace(stableId)
                ? 0
                : Hecton.Localization.LocHash.Compute(stableId);
        }
    }
}
