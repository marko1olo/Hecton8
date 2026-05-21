// ============================================================================
// HECTON-8 — ModuleMarker.cs
// Legkiy komponent-marker na kazhdom postroennom module bazy.
//
// Zadachi:
//   1. Hranit ssylku na BuildableData (dlya UI, dekonstruktsii)
//   2. Hranit keshirovannyy prefabId (dlya serializatsii)
//   3. Zero overhead: net Update, net allokatsiy
//
// Dobavlyaetsya na finalPrefab modulya v redaktore.
// Esli zabyli — ConstructionManager dobavit avtomaticheski.
// ============================================================================

using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Marker postroennogo modulya bazy.
    /// Hranit identifikatsionnye dannye dlya save/load i UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Construction/Module Marker")]
    public sealed class ModuleMarker : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Module Identity ───────────────────────────")]
        [Tooltip("Ssylka na BuildableData etogo modulya. " +
                 "Naznachaetsya v prefabe ili programmno.")]
        [SerializeField] private BuildableData buildableData;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Keshirovannyy strokovyy ID dlya serializatsii.
        /// Stroitsya odin raz v Initialize / Awake.
        /// </summary>
        private string _prefabId;
        private uint _scannerEntryHash;
        private bool   _initialized;
        private int _spatialHandle;
        private FieldTargetRole _spatialRole = FieldTargetRole.Generic;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>BuildableData etogo modulya.</summary>
        public BuildableData Data => buildableData;

        /// <summary>
        /// Strokovyy ID dlya save/load.
        /// Ispolzuet BuildableData.PersistentId s legacy fallback na imya asseta.
        /// Zero alloc pri povtornyh vyzovah.
        /// </summary>
        public string PrefabId
        {
            get
            {
                if (!_initialized) CacheId();
                return _prefabId;
            }
        }

        /// <summary>Current runtime field-semantics role exposed to scanner/sonar owners.</summary>
        public FieldTargetRole SpatialRole => _spatialRole;

        /// <summary>Cold-cached FNV-1a module hash for scanner lore discovery.</summary>
        public uint ScannerEntryHash => _scannerEntryHash;

        /// <summary>
        /// Programmnaya initsializatsiya (esli marker dobavlen v rantayme).
        /// </summary>
        public void Initialize(BuildableData data)
        {
            buildableData = data;
            CacheId();
        }

        /// <summary>
        /// Updates the runtime field role used by spatial scanner/sonar owners.
        /// </summary>
        public void SetSpatialRole(FieldTargetRole role)
        {
            if (_spatialRole == role)
                return;

            _spatialRole = role;

            if (_spatialHandle != 0)
                WorldSpatialHashGrid.UpdateSignalRole(_spatialHandle, _spatialRole);
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (!_initialized) CacheId();
        }

        private void OnEnable()
        {
            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterModule(this);
        }

        private void OnDisable()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void OnDestroy()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void CacheId()
        {
            _prefabId    = buildableData != null ? buildableData.PersistentId : string.Empty;
            _scannerEntryHash = ResolveScannerEntryHash(buildableData);
            _initialized = true;
        }

        private static uint ResolveScannerEntryHash(BuildableData data)
        {
            if (data == null)
                return 0u;

            BaseModuleTemplate template = data.ModuleTemplate;
            int hashId = template != null ? template.TemplateHashId : data.ModuleHashId;
            return hashId == 0 ? 0u : unchecked((uint)hashId);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            CacheId();
        }
#endif
    }
}
