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

    [CreateAssetMenu(fileName = "BaseModuleTemplate_", menuName = "Hecton8/Building/Base Module Template", order = 18)]
    public sealed class BaseModuleTemplate : ScriptableObject
    {
        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ItemHashCost
        {
            [Tooltip("Stable item hash resolved from ItemData.PersistentId.")]
            [SerializeField] private int itemHashId;

            [Tooltip("Required quantity for this module template.")]
            [SerializeField, Min(1)] private int amount;

            public int ItemHashId => itemHashId;
            public int Amount => math.max(1, amount);
        }

        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VfxSocket
        {
            [Tooltip("Module-local socket coordinate in template space.")]
            [SerializeField] private float3 localPosition;

            [Tooltip("VFX semantic routed when the module degrades below the authored threshold.")]
            [SerializeField] private BaseModuleVfxSocketType socketType;

            public float3 LocalPosition => localPosition;
            public BaseModuleVfxSocketType SocketType => socketType;
        }

        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SocketDefinition
        {
            [Tooltip("Module-local socket coordinate in template space.")]
            [SerializeField] private Vector3 localPosition;

            [Tooltip("Canonical socket direction used by strict inverse-socket snapping.")]
            [SerializeField] private ModuleSocketDirection direction;

            [Tooltip("Semantic compatibility lane. Empty = universal socket.")]
            [SerializeField] private string compatibleType;

            public SocketDefinition(Vector3 localPosition, ModuleSocketDirection direction, string compatibleType)
            {
                this.localPosition = localPosition;
                this.direction = direction;
                this.compatibleType = compatibleType ?? string.Empty;
            }

            public Vector3 LocalPosition => localPosition;
            public ModuleSocketDirection Direction => direction;
            public string CompatibleType => compatibleType ?? string.Empty;
            public ModuleSocketMask DirectionMask => ModuleSocketTopology.ToMask(direction);
        }

        [Header("── Identity ──────────────────")]
        [Tooltip("Stable authoring ID used for hashes, ledgers, and external references.")]
        [SerializeField] private string stableId = string.Empty;

        [Tooltip("Stable hash resolved from stableId. Generated automatically in OnValidate.")]
        [SerializeField] private int templateHashId;

        [Header("── Topology ──────────────────")]
        [Tooltip("Legacy module-local snap points kept for backward compatibility with older authoring.")]
        [SerializeField] private float3[] snapPoints = Array.Empty<float3>();

        [Tooltip("Strict socket definitions used by snapping, graph rebuilds, and runtime proxy generation.")]
        [SerializeField] private SocketDefinition[] socketDefinitions = Array.Empty<SocketDefinition>();

        [Header("── Proxy Bounds ──────────────────")]
        [Tooltip("Module-local bounds center used when generating ghost or final proxy cubes.")]
        [SerializeField] private Vector3 proxyBoundsCenter = Vector3.zero;

        [Tooltip("Module-local bounds size used when generating ghost or final proxy cubes.")]
        [SerializeField] private Vector3 proxyBoundsSize = new Vector3(4f, 4f, 4f);

        [Header("── Construction ──────────────────")]
        [Tooltip("Data-oriented build cost expressed as stable item hashes.")]
        [SerializeField] private ItemHashCost[] buildCost = Array.Empty<ItemHashCost>();

        [Header("── Simulation ──────────────────")]
        [Tooltip("Continuous power draw in kilowatts for this template.")]
        [SerializeField, Min(0f)] private float powerDrawKW;

        [Tooltip("Pressurized air volume contributed by this module in cubic meters.")]
        [SerializeField, Min(0f)] private float airVolumeM3 = 1f;

        [Header("── Structural Roles ──────────────────")]
        [Tooltip("When true, this module counts as a seafloor anchor during habitat reachability traversal.")]
        [SerializeField] private bool isStructuralAnchor;

        [Tooltip("When true, adjacent breach events can hard-lock this module's airlock controls.")]
        [SerializeField] private bool isEmergencyAirlock;

        [Header("── Integrity Authoring ──────────────────")]
        [Tooltip("Default normalized integrity state used by procedural abandoned-habitat spawning. 1.0 = pristine.")]
        [SerializeField, Range(0f, 1f)] private float defaultIntegrityState = 1f;

        [Tooltip("Integrity threshold below which the module should begin in a flooded state.")]
        [SerializeField, Range(0f, 1f)] private float floodedBelowIntegrityState = 0.45f;

        [Tooltip("Integrity threshold below which breathable reserve should be considered offline.")]
        [SerializeField, Range(0f, 1f)] private float oxygenOfflineBelowIntegrityState = 0.35f;

        [Header("── Hydrodynamic Fatigue ──────────────────")]
        [Tooltip("Projected module cross-section used by abyssal drag-stress evaluation in square meters.")]
        [SerializeField, Min(0.1f)] private float projectedDragAreaSquareMeters = 12f;

        [Tooltip("Drag-force threshold in newtons above which structural fatigue starts consuming integrity.")]
        [SerializeField, Min(1f)] private float moduleYieldStrengthNewtons = 180000f;

        [Tooltip("Effective opening area routed into the depressurization system once integrity collapses to zero.")]
        [SerializeField, Min(0.05f)] private float breachAreaSquareMeters = 1.2f;

        [Header("── Unmoored Physics ──────────────────")]
        [Tooltip("Dry structural mass routed into unmoored buoyancy calculations in kilograms.")]
        [SerializeField, Min(1f)] private float structuralDryMassKilograms = 14000f;

        [Tooltip("Displacement volume used by unmoored buoyancy evaluation in cubic meters.")]
        [SerializeField, Min(0.1f)] private float buoyancyDisplacementVolumeCubicMeters = 18f;

        [Tooltip("Absolute cap applied to unmoored buoyancy acceleration in meters per second squared.")]
        [SerializeField, Min(0.1f)] private float maximumUnmooredAccelerationMetersPerSecondSquared = 24f;

        [Tooltip("Maximum local-space center-of-mass shift toward a breach point while flooding.")]
        [SerializeField, Min(0.01f)] private float maximumCenterOfMassShiftMeters = 0.85f;

        [Tooltip("Time constant used to blend the unmoored center of mass toward the breach-weighted target.")]
        [SerializeField, Min(0.01f)] private float centerOfMassShiftTauSeconds = 1.2f;

        [Header("── VFX Hardpoints ──────────────────")]
        [Tooltip("Pre-authored module-local VFX sockets used by degradation routing.")]
        [SerializeField] private VfxSocket[] vfxSockets = Array.Empty<VfxSocket>();

        public string PersistentId => ResolveCanonicalPersistentId(stableId, name);
        public int TemplateHashId => templateHashId;
        public int PersistentHashId => ResolvePersistentHashId();
        public float3[] SnapPoints => snapPoints;
        public SocketDefinition[] SocketDefinitions => socketDefinitions;
        public Vector3 ProxyBoundsCenter => proxyBoundsCenter;
        public Vector3 ProxyBoundsSize => proxyBoundsSize;
        public ModuleSocketMask SocketMask => BuildSocketMask(socketDefinitions);
        public ItemHashCost[] BuildCost => buildCost;
        public float PowerDrawKW => powerDrawKW;
        public float AirVolumeM3 => airVolumeM3;
        public bool IsStructuralAnchor => isStructuralAnchor;
        public bool IsEmergencyAirlock => isEmergencyAirlock;
        public float DefaultIntegrityState => defaultIntegrityState;
        public float FloodedBelowIntegrityState => floodedBelowIntegrityState;
        public float OxygenOfflineBelowIntegrityState => oxygenOfflineBelowIntegrityState;
        public VfxSocket[] VfxSockets => vfxSockets;
        internal float ProjectedDragAreaSquareMeters => projectedDragAreaSquareMeters;
        internal float ModuleYieldStrengthNewtons => moduleYieldStrengthNewtons;
        internal float BreachAreaSquareMeters => breachAreaSquareMeters;
        internal float StructuralDryMassKilograms => structuralDryMassKilograms;
        internal float BuoyancyDisplacementVolumeCubicMeters => buoyancyDisplacementVolumeCubicMeters;
        internal float MaximumUnmooredAccelerationMetersPerSecondSquared => maximumUnmooredAccelerationMetersPerSecondSquared;
        internal float MaximumCenterOfMassShiftMeters => maximumCenterOfMassShiftMeters;
        internal float CenterOfMassShiftTauSeconds => centerOfMassShiftTauSeconds;

        public int ResolvePersistentHashId()
        {
            if (templateHashId != 0)
                return templateHashId;

            return ComputeCanonicalPersistentHashId(PersistentId);
        }

        /// <summary>
        /// Canonical form of an authored stable id.
        /// This must stay behaviourally identical to <c>SaveData.SanitizePersistenceString</c>
        /// (SaveData.cs:99-102), which is the normalizer the save layer applies to every persisted
        /// module id through <c>ModuleDTO.SanitizePersistenceId</c> and
        /// <c>ModuleGraphNodeDTO.SanitizeForPersistence</c>, and which <c>ModuleCatalog.FindDataById</c>
        /// also applies to the id it is handed before the dictionary probe. A module carries two
        /// persisted identities - the string prefabId and the integer moduleHashId - and if this form
        /// diverges from the save layer's, the two identities describe different modules and the
        /// saved module is never restored.
        /// A blank or whitespace-only id resolves to <see cref="string.Empty"/> and is refused rather
        /// than hashed, because a real hash over a blank id is one identity every blank template would
        /// share.
        /// </summary>
        private static string ResolveCanonicalPersistentId(string authoredId, string fallbackName)
        {
            string id = !string.IsNullOrWhiteSpace(authoredId) ? authoredId : fallbackName;
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private static int ComputeCanonicalPersistentHashId(string value)
        {
            string persistentId = ResolveCanonicalPersistentId(value, null);
            return persistentId.Length == 0
                ? 0
                : Hecton.Localization.LocHash.Compute(persistentId);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = name;

            powerDrawKW = math.max(0f, powerDrawKW);
            airVolumeM3 = math.max(0f, airVolumeM3);
            defaultIntegrityState = math.clamp(defaultIntegrityState, 0f, 1f);
            floodedBelowIntegrityState = math.clamp(floodedBelowIntegrityState, 0f, 1f);
            oxygenOfflineBelowIntegrityState = math.clamp(oxygenOfflineBelowIntegrityState, 0f, 1f);
            projectedDragAreaSquareMeters = math.max(0.1f, projectedDragAreaSquareMeters);
            moduleYieldStrengthNewtons = math.max(1f, moduleYieldStrengthNewtons);
            breachAreaSquareMeters = math.max(0.05f, breachAreaSquareMeters);
            structuralDryMassKilograms = math.max(1f, structuralDryMassKilograms);
            buoyancyDisplacementVolumeCubicMeters = math.max(0.1f, buoyancyDisplacementVolumeCubicMeters);
            maximumUnmooredAccelerationMetersPerSecondSquared = math.max(0.1f, maximumUnmooredAccelerationMetersPerSecondSquared);
            maximumCenterOfMassShiftMeters = math.max(0.01f, maximumCenterOfMassShiftMeters);
            centerOfMassShiftTauSeconds = math.max(0.01f, centerOfMassShiftTauSeconds);
            if (oxygenOfflineBelowIntegrityState > floodedBelowIntegrityState)
                oxygenOfflineBelowIntegrityState = floodedBelowIntegrityState;

            if ((socketDefinitions == null || socketDefinitions.Length == 0) && snapPoints != null && snapPoints.Length > 0)
                socketDefinitions = BuildSocketDefinitionsFromSnapPoints(snapPoints);

            if ((snapPoints == null || snapPoints.Length == 0) && socketDefinitions != null && socketDefinitions.Length > 0)
                snapPoints = BuildSnapPointsFromSocketDefinitions(socketDefinitions);

            if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                DeriveProxyBoundsFromSocketsAndSnapPoints(out proxyBoundsCenter, out proxyBoundsSize);

            templateHashId = ComputeCanonicalPersistentHashId(stableId);
        }

        private static ModuleSocketMask BuildSocketMask(SocketDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return ModuleSocketMask.None;

            ModuleSocketMask mask = ModuleSocketMask.None;
            for (int i = 0; i < definitions.Length; i++)
                mask |= definitions[i].DirectionMask;

            return mask;
        }

        private static SocketDefinition[] BuildSocketDefinitionsFromSnapPoints(float3[] legacySnapPoints)
        {
            SocketDefinition[] definitions = new SocketDefinition[legacySnapPoints.Length];
            for (int i = 0; i < legacySnapPoints.Length; i++)
            {
                Vector3 localPosition = legacySnapPoints[i];
                definitions[i] = new SocketDefinition(
                    localPosition,
                    ModuleSocketTopology.QuantizeDirection(localPosition),
                    string.Empty);
            }

            return definitions;
        }

        private static float3[] BuildSnapPointsFromSocketDefinitions(SocketDefinition[] definitions)
        {
            float3[] points = new float3[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                points[i] = definitions[i].LocalPosition;

            return points;
        }

        private void DeriveProxyBoundsFromSocketsAndSnapPoints(out Vector3 center, out Vector3 size)
        {
            bool initialized = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            if (socketDefinitions != null)
            {
                for (int i = 0; i < socketDefinitions.Length; i++)
                    EncapsulatePoint(socketDefinitions[i].LocalPosition, ref initialized, ref min, ref max);
            }

            if (snapPoints != null)
            {
                for (int i = 0; i < snapPoints.Length; i++)
                    EncapsulatePoint(snapPoints[i], ref initialized, ref min, ref max);
            }

            if (!initialized)
            {
                center = Vector3.zero;
                size = new Vector3(4f, 4f, 4f);
                return;
            }

            Vector3 extents = (max - min) * 0.5f;
            center = min + extents;
            size = Vector3.Max((extents * 2f) + Vector3.one, new Vector3(1f, 1f, 1f));
        }

        private static void EncapsulatePoint(Vector3 point, ref bool initialized, ref Vector3 min, ref Vector3 max)
        {
            if (!initialized)
            {
                initialized = true;
                min = point;
                max = point;
                return;
            }

            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

    }
}
