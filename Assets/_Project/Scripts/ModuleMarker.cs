// ============================================================================
// HECTON-8 — ModuleMarker.cs
// Лёгкий компонент-маркер на каждом построенном модуле базы.
//
// Задачи:
//   1. Хранить ссылку на BuildableData (для UI, деконструкции)
//   2. Хранить кэшированный prefabId (для сериализации)
//   3. Zero overhead: нет Update, нет аллокаций
//
// Добавляется на finalPrefab модуля в редакторе.
// Если забыли — ConstructionManager добавит автоматически.
// ============================================================================

using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Маркер построенного модуля базы.
    /// Хранит идентификационные данные для save/load и UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Construction/Module Marker")]
    public sealed class ModuleMarker : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Module Identity ───────────────────────────")]
        [Tooltip("Ссылка на BuildableData этого модуля. " +
                 "Назначается в префабе или программно.")]
        [SerializeField] private BuildableData buildableData;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Кэшированный строковый ID для сериализации.
        /// Строится один раз в Initialize / Awake.
        /// </summary>
        private string _prefabId;
        private bool   _initialized;
        private int _spatialHandle;
        private FieldTargetRole _spatialRole = FieldTargetRole.Generic;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>BuildableData этого модуля.</summary>
        public BuildableData Data => buildableData;

        /// <summary>
        /// Строковый ID для save/load.
        /// Использует BuildableData.PersistentId с legacy fallback на имя ассета.
        /// Zero alloc при повторных вызовах.
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

        /// <summary>
        /// Программная инициализация (если маркер добавлен в рантайме).
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
            _initialized = true;
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
