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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>BuildableData этого модуля.</summary>
        public BuildableData Data => buildableData;

        /// <summary>
        /// Строковый ID для save/load.
        /// Использует BuildableData.name (имя ассета ScriptableObject).
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

        /// <summary>
        /// Программная инициализация (если маркер добавлен в рантайме).
        /// </summary>
        public void Initialize(BuildableData data)
        {
            buildableData = data;
            CacheId();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (!_initialized) CacheId();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void CacheId()
        {
            _prefabId    = buildableData != null ? buildableData.name : string.Empty;
            _initialized = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheId();
        }
#endif
    }
}