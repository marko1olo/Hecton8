// ============================================================================
// HECTON-8 â€” ConstructionManager.cs
// ÐœÐµÐ½ÐµÐ´Ð¶ÐµÑ€ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð±Ð°Ð·Ñ‹.
//
// GlobalRegistry service, ISaveable (Priority 90 â€” Ð¿Ð¾ÑÐ»ÐµÐ´Ð½Ð¸Ð¹ Ð¿Ñ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ).
//
// Ð’ÐµÐ´Ñ‘Ñ‚ Ñ€ÐµÐµÑÑ‚Ñ€ Ð²ÑÐµÑ… Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹. ÐŸÑ€Ð¸ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ð¸ Ð·Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚
// ID + Ñ‚Ñ€Ð°Ð½ÑÑ„Ð¾Ñ€Ð¼ + Ð´Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¾Ðµ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ (integrity, isFlooded)
// ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»Ñ. ÐŸÑ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ â€” ÑƒÐ´Ð°Ð»ÑÐµÑ‚ ÑÑ‚Ð°Ñ€Ñ‹Ðµ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ» Ð¸
// ÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ Ð½Ð¾Ð²Ñ‹Ðµ Ð¸Ð· ÑÐµÐ¹Ð²Ð° Ñ Ð²Ð¾ÑÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸ÐµÐ¼ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ñ.
//
// ZERO GC Ð² Ñ€Ð°Ð½Ñ‚Ð°Ð¹Ð¼Ðµ:
//   â€¢ Register/Unregister: O(1) Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ°, no LINQ.
//   â€¢ List<GameObject> pre-allocated Ñ Ð·Ð°Ð¿Ð°ÑÐ¾Ð¼.
//   â€¢ Swap-remove Ð´Ð»Ñ O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ñ.
//   â€¢ PopulateSaveData: for-Ñ†Ð¸ÐºÐ»Ñ‹, TryGetComponent.
//
// Ð˜ÐÐ¢Ð•Ð“Ð ÐÐ¦Ð˜Ð¯:
//   â€¢ PlayerBuilder Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ RegisterModule() Ð¿Ð¾ÑÐ»Ðµ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ.
//   â€¢ ClearAllModules() Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð² LoadFromSaveData Ð¿ÐµÑ€ÐµÐ´ Ñ€ÐµÑÐ¿Ð°Ð²Ð½Ð¾Ð¼.
//   â€¢ ObjectPoolManager Ð´Ð»Ñ Spawn/Despawn Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹.
//   â€¢ BaseModule: integrity Ð¸ isFlooded ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÑŽÑ‚ÑÑ/Ð²Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÑŽÑ‚ÑÑ Ð·Ð´ÐµÑÑŒ.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class ConstructionManager : MonoBehaviour, IUpdatable, ISaveable, ISlowTickable, ILogisticsService
    {
        private const float SlowTickDeltaTime = 0.5f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SERVICE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Catalog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐšÐ°Ñ‚Ð°Ð»Ð¾Ð³ Ð²ÑÐµÑ… ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹. " +
                 "ÐÑƒÐ¶ÐµÐ½ Ð´Ð»Ñ Ð¿Ð¾Ð¸ÑÐºÐ° Ð¿Ñ€ÐµÑ„Ð°Ð±Ð¾Ð² Ð¿Ð¾ ID Ð¿Ñ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ.")]
        [SerializeField] private ModuleCatalog catalog;

        [Header("â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐÐ°Ñ‡Ð°Ð»ÑŒÐ½Ð°Ñ Ñ‘Ð¼ÐºÐ¾ÑÑ‚ÑŒ ÑÐ¿Ð¸ÑÐºÐ° Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹. " +
                 "Ð£Ð²ÐµÐ»Ð¸Ñ‡ÑŒ Ð´Ð»Ñ Ð±Ð¾Ð»ÑŒÑˆÐ¸Ñ… Ð±Ð°Ð·.")]
        [SerializeField] private int initialCapacity = 64;

        [Header("Ambient Accidents")]
        [Tooltip("Ð Ð°Ð·Ñ€ÐµÑˆÐ°ÐµÑ‚ Ñ€ÐµÐ´ÐºÐ¸Ðµ ÑÐµÑ€Ð²Ð¸ÑÐ½Ñ‹Ðµ Ð°Ð²Ð°Ñ€Ð¸Ð¸ Ð½Ð° ÑƒÐ¶Ðµ Ñ€Ð°Ð·Ð¼ÐµÑ‰Ñ‘Ð½Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÑÑ… Ð±Ð°Ð·Ñ‹.")]
        [SerializeField] private bool enableAmbientAccidents = true;
        [Tooltip("Ð˜Ð½Ñ‚ÐµÑ€Ð²Ð°Ð» Ð¼ÐµÐ¶Ð´Ñƒ cold-path Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ°Ð¼Ð¸ Ð½Ð° ÑÐ»ÑƒÑ‡Ð°Ð¹Ð½ÑƒÑŽ ÑÐµÑ€Ð²Ð¸ÑÐ½ÑƒÑŽ Ð°Ð²Ð°Ñ€Ð¸ÑŽ.")]
        [SerializeField] private float ambientAccidentCheckInterval = 90f;
        [Tooltip("Ð‘Ð°Ð·Ð¾Ð²Ñ‹Ð¹ ÑˆÐ°Ð½Ñ Ð°Ð²Ð°Ñ€Ð¸Ð¸ Ð½Ð° Ð¾Ð´Ð½Ñƒ cold-path Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÑƒ. Ð¤Ð¸Ð½Ð°Ð»ÑŒÐ½Ñ‹Ð¹ ÑˆÐ°Ð½Ñ ÑƒÐ¼Ð½Ð¾Ð¶Ð°ÐµÑ‚ÑÑ Ð½Ð° risk score ÐºÐ°Ð½Ð´Ð¸Ð´Ð°Ñ‚Ð°.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentBaseChance = 0.25f;
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ risk score, Ð¿Ñ€Ð¸ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¼ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ ÑÑ‡Ð¸Ñ‚Ð°ÐµÑ‚ÑÑ Ð°Ð²Ð°Ñ€Ð¸Ð¹Ð½Ñ‹Ð¼ ÐºÐ°Ð½Ð´Ð¸Ð´Ð°Ñ‚Ð¾Ð¼.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentMinRisk = 0.2f;
        [Tooltip("ÐŸÐ¾Ñ€Ð¾Ð³ integrity, Ð½Ð¸Ð¶Ðµ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ ÑÑ‡Ð¸Ñ‚Ð°ÐµÑ‚ÑÑ Ð¸Ð·Ð½Ð¾ÑˆÐµÐ½Ð½Ñ‹Ð¼ Ð´Ð»Ñ accident scheduler.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentIntegrityThreshold = 0.8f;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugModuleCount;
        [Tooltip("Runtime timer until the next ambient accident evaluation.")]
        [SerializeField] private float _debugAmbientAccidentTimer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REGISTRY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð ÐµÐµÑÑ‚Ñ€ Ð²ÑÐµÑ… Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹.
        /// Pre-allocated. Swap-remove Ð´Ð»Ñ O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ñ.
        /// </summary>
        private List<GameObject> _spawnedModules;
        private HabitatGraphManager _habitatGraphManager;
        private bool _tickRegistered;
        private bool _logisticsServiceRegistered;
        private bool _saveRegistered;
        private float _slowTickAccumulator;
        private float _ambientAccidentTimer;
        private int _ambientAccidentCursor;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CONSTANTS â€” DEFAULT MODULE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð”ÐµÑ„Ð¾Ð»Ñ‚Ð½Ð°Ñ Ñ†ÐµÐ»Ð¾ÑÑ‚Ð½Ð¾ÑÑ‚ÑŒ Ð´Ð»Ñ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð±ÐµÐ· BaseModule (Ð¾Ð¿Ð¾Ñ€Ñ‹ Ð¸ Ñ‚.Ð¿.)
        /// Ð¸ Ð´Ð»Ñ Ð¼Ð¸Ð³Ñ€Ð°Ñ†Ð¸Ð¸ ÑÑ‚Ð°Ñ€Ñ‹Ñ… ÑÐµÐ¹Ð²Ð¾Ð² (v1 â†’ v2).
        /// </summary>
        private const float DefaultIntegrity = 100f;

        /// <summary>Ð”ÐµÑ„Ð¾Ð»Ñ‚Ð½Ð¾Ðµ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ Ð·Ð°Ñ‚Ð¾Ð¿Ð»ÐµÐ½Ð¸Ñ.</summary>
        private const bool  DefaultIsFlooded = false;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” QUERIES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹.</summary>
        public int ModuleCount => _spawnedModules != null ? _spawnedModules.Count : 0;

        /// <summary>Read-only Ð´Ð¾ÑÑ‚ÑƒÐ¿ Ðº ÑÐ¿Ð¸ÑÐºÑƒ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ (Ð´Ð»Ñ UI, minimap).</summary>
        public IReadOnlyList<GameObject> SpawnedModules => _spawnedModules;

        /// <summary>Read-only Ð´Ð¾ÑÑ‚ÑƒÐ¿ Ðº ÐºÐ°Ñ‚Ð°Ð»Ð¾Ð³Ñƒ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð´Ð»Ñ build tools/UI.</summary>
        public ModuleCatalog Catalog => catalog;

        /// <summary>
        /// True once the logistics owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.Logistics, this);

        /// <summary>
        /// Registers the construction/logistics service with bootstrap-owned runtime systems.
        /// </summary>
        public void InitializeService()
        {
            TryRegisterLogisticsService();
            TryRegisterTick();
            TryRegisterSaveParticipant();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            // â”€â”€ Service â”€â”€
            // â”€â”€ Pre-allocate â”€â”€
            _spawnedModules = new List<GameObject>(initialCapacity);
            // COLD ALLOC: HabitatGraphManager[1] — persistent placed-module CSR adjacency owner — owner: ConstructionManager
            _habitatGraphManager = new HabitatGraphManager(initialCapacity);
            _ambientAccidentTimer = 0f;
        }

        private void OnEnable()
        {
            _slowTickAccumulator = 0f;
            TryRegisterTick();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterLogisticsService();
            _slowTickAccumulator = 0f;
            TryUnregisterSaveParticipant();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterLogisticsService();
            TryUnregisterSaveParticipant();
            if (_habitatGraphManager != null)
            {
                _habitatGraphManager.Dispose();
                _habitatGraphManager = null;
            }

        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < SlowTickDeltaTime)
                return;

            _slowTickAccumulator -= SlowTickDeltaTime;
            if (_slowTickAccumulator > SlowTickDeltaTime)
                _slowTickAccumulator = SlowTickDeltaTime;

            SlowTick();
        }

        public void SlowTick()
        {
            if (_habitatGraphManager != null)
                _habitatGraphManager.ApplyHydrodynamicStress(SlowTickDeltaTime);

            if (!enableAmbientAccidents || ambientAccidentCheckInterval <= 0f)
                return;

            _ambientAccidentTimer += SlowTickDeltaTime;
            _debugAmbientAccidentTimer = _ambientAccidentTimer;

            if (_ambientAccidentTimer < ambientAccidentCheckInterval)
                return;

            _ambientAccidentTimer = 0f;
            _debugAmbientAccidentTimer = 0f;

            TryTriggerAmbientAccident();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” REGISTER / UNREGISTER
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ð¹ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ð² Ñ€ÐµÐµÑÑ‚Ñ€Ðµ.
        ///
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ:
        ///   â€¢ PlayerBuilder.TryPlaceModule() Ð¿Ð¾ÑÐ»Ðµ ÑƒÑÐ¿ÐµÑˆÐ½Ð¾Ð³Ð¾ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ
        ///   â€¢ LoadFromSaveData() Ð¿Ñ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ
        ///
        /// ÐÐ²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ Ð´Ð¾Ð±Ð°Ð²Ð»ÑÐµÑ‚ ModuleMarker, ÐµÑÐ»Ð¸ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚.
        /// Ð”ÑƒÐ±Ð»Ð¸ÐºÐ°Ñ‚Ñ‹ Ð¸Ð³Ð½Ð¾Ñ€Ð¸Ñ€ÑƒÑŽÑ‚ÑÑ (ReferenceEquals Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ°).
        /// </summary>
        /// <param name="module">GameObject Ñ„Ð¸Ð½Ð°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»Ñ.</param>
        public void RegisterModule(GameObject module)
        {
            if (module == null) return;

            // â”€â”€ ÐŸÑ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð´ÑƒÐ±Ð»Ð¸ÐºÐ°Ñ‚Ð¾Ð² â”€â”€
            if (ContainsRef(module)) return;

            // â”€â”€ Ð”Ð¾Ð±Ð°Ð²Ð»ÑÐµÐ¼ Ð² Ñ€ÐµÐµÑÑ‚Ñ€ â”€â”€
            _spawnedModules.Add(module);
            RefreshHabitatGraph();
            if (module.TryGetComponent(out BaseModuleNavModifier navModifier))
                navModifier.RefreshVegetationExclusion();

            UpdateDiagnostics();
        }

        /// <summary>
        /// Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¾Ð¹ Ðº BuildableData.
        /// ÐÐ²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ Ð½Ð°ÑÑ‚Ñ€Ð°Ð¸Ð²Ð°ÐµÑ‚ ModuleMarker.
        ///
        /// ÐŸÑ€ÐµÐ´Ð¿Ð¾Ñ‡Ñ‚Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¹ Ð¼ÐµÑ‚Ð¾Ð´: Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€ÑƒÐµÑ‚ Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ðµ Ð¼Ð°Ñ€ÐºÐµÑ€Ð°.
        /// </summary>
        /// <param name="module">GameObject Ñ„Ð¸Ð½Ð°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»Ñ.</param>
        /// <param name="data">BuildableData Ð´Ð»Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¸.</param>
        public void RegisterModule(GameObject module, BuildableData data)
        {
            if (module == null) return;

            // â”€â”€ Ð“Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€ÑƒÐµÐ¼ Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ðµ ModuleMarker â”€â”€
            if (!module.TryGetComponent(out ModuleMarker marker))
            {
                marker = module.AddComponent<ModuleMarker>();
            }

            // â”€â”€ Ð˜Ð½Ð¸Ñ†Ð¸Ð°Ð»Ð¸Ð·Ð¸Ñ€ÑƒÐµÐ¼ Ð¼Ð°Ñ€ÐºÐµÑ€, ÐµÑÐ»Ð¸ data Ð¿Ñ€ÐµÐ´Ð¾ÑÑ‚Ð°Ð²Ð»ÐµÐ½Ð° â”€â”€
            if (data != null)
                marker.Initialize(data);

            if (data != null && module.TryGetComponent(out BaseModule baseModule))
                baseModule.ApplyBuildableTemplate(data);

            RegisterModule(module);
        }

        /// <summary>
        /// Ð£Ð´Ð°Ð»ÑÐµÑ‚ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ð¸Ð· Ñ€ÐµÐµÑÑ‚Ñ€Ð°. ÐÐ• Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ ÐµÐ³Ð¾.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐ¹ Ð´Ð»Ñ Ð´ÐµÐºÐ¾Ð½ÑÑ‚Ñ€ÑƒÐºÑ†Ð¸Ð¸: Unregister + Pool.Despawn.
        ///
        /// Swap-remove: O(1).
        /// </summary>
        public void UnregisterModule(GameObject module)
        {
            if (module == null) return;

            SwapRemove(module);
            RefreshHabitatGraph();

            UpdateDiagnostics();
        }

        /// <summary>
        /// Ð£Ð´Ð°Ð»ÑÐµÑ‚ Ð¸Ð· Ñ€ÐµÐµÑÑ‚Ñ€Ð° Ð˜ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ».
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ Ð´ÐµÐºÐ¾Ð½ÑÑ‚Ñ€ÑƒÐºÑ†Ð¸Ð¸ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹.
        /// </summary>
        public void DestroyModule(GameObject module)
        {
            if (module == null) return;

            UnregisterModule(module);

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            DespawnOrDestroyModuleInstance(module, pool);
        }

        /// <summary>
        /// Inserts a temporary external bypass cable between two placed habitat modules and rebuilds the runtime graph.
        /// </summary>
        public bool TryCreateTemporaryBypass(BaseModule sourceModule, BaseModule destinationModule)
        {
            if (_habitatGraphManager == null || sourceModule == null || destinationModule == null)
                return false;

            if (!_habitatGraphManager.TryAddTemporaryBypass(sourceModule.gameObject, destinationModule.gameObject))
                return false;

            RefreshHabitatGraph();
            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” CLEAR ALL
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð”ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ Ð’Ð¡Ð• Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ» Ð¸ Ð¾Ñ‡Ð¸Ñ‰Ð°ÐµÑ‚ Ñ€ÐµÐµÑÑ‚Ñ€.
        ///
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ:
        ///   â€¢ LoadFromSaveData() Ð¿ÐµÑ€ÐµÐ´ Ñ€ÐµÑÐ¿Ð°Ð²Ð½Ð¾Ð¼ Ð¸Ð· ÑÐµÐ¹Ð²Ð°
        ///   â€¢ New Game (ÐµÑÐ»Ð¸ Ð½ÑƒÐ¶Ð½Ð¾ Ð½Ð°Ñ‡Ð°Ñ‚ÑŒ Ñ Ñ‡Ð¸ÑÑ‚Ð¾Ð³Ð¾ Ð¼Ð¸Ñ€Ð°)
        ///
        /// Ð˜Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ñ Ð¾Ð±Ñ€Ð°Ñ‚Ð½Ñ‹Ð¼ Ñ†Ð¸ÐºÐ»Ð¾Ð¼: Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ Ð¿Ñ€Ð¸ Despawn,
        /// ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ð¹ Ð¼Ð¾Ð¶ÐµÑ‚ Ð²Ñ‹Ð·Ð²Ð°Ñ‚ÑŒ OnDisable Ð½Ð° Ð¼Ð¾Ð´ÑƒÐ»ÑÑ….
        /// </summary>
        public void ClearAllModules()
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;

            // â”€â”€ ÐžÐ±Ñ€Ð°Ñ‚Ð½Ñ‹Ð¹ Ñ†Ð¸ÐºÐ»: Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ Ð¿Ñ€Ð¸ Ð¼Ð¾Ð´Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ð¸ ÑÐ¿Ð¸ÑÐºÐ° â”€â”€
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                GameObject module = _spawnedModules[i];

                if (module == null) continue; // ÑƒÐ¶Ðµ ÑƒÐ½Ð¸Ñ‡Ñ‚Ð¾Ð¶ÐµÐ½

                DespawnOrDestroyModuleInstance(module, pool);
            }

            _spawnedModules.Clear();
            RefreshHabitatGraph();

            UpdateDiagnostics();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ISaveable â€” SAVE / LOAD (Priority 90)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Construction Ð·Ð°Ð³Ñ€ÑƒÐ¶Ð°ÐµÑ‚ÑÑ ÐŸÐžÐ¡Ð›Ð•Ð”ÐÐ•Ð™ (Ð·Ð°Ð²Ð¸ÑÐ¸Ñ‚ Ð¾Ñ‚ Ð¼Ð¸Ñ€Ð°).</summary>
        public int SavePriority => 90;
        public int LoadPriority => 90;

        /// <summary>
        /// Ð—Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚ Ð²ÑÐµ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ð² ConstructionDTO.
        ///
        /// Ð”Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»Ñ:
        ///   1. ÐŸÐ¾Ð»ÑƒÑ‡Ð°ÐµÑ‚ ModuleMarker â†’ PrefabId
        ///   2. Ð§Ð¸Ñ‚Ð°ÐµÑ‚ transform.position Ð¸ rotation
        ///   3. Ð§Ð¸Ñ‚Ð°ÐµÑ‚ BaseModule.CurrentIntegrity Ð¸ IsFlooded (ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ)
        ///   4. Ð—Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚ Ð² dto.modules[]
        ///
        /// ÐœÐ¾Ð´ÑƒÐ»Ð¸ Ð±ÐµÐ· ModuleMarker â€” Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÑŽÑ‚ÑÑ Ñ Warning.
        /// ÐœÐ¾Ð´ÑƒÐ»Ð¸ Ð±ÐµÐ· BaseModule â€” Ð·Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÑŽÑ‚ÑÑ Ñ Ð´ÐµÑ„Ð¾Ð»Ñ‚Ð½Ñ‹Ð¼Ð¸ Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸ÑÐ¼Ð¸
        /// (100% HP, Ð½Ðµ Ð·Ð°Ñ‚Ð¾Ð¿Ð»ÐµÐ½). Ð­Ñ‚Ð¾ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾ Ð´Ð»Ñ Ð¿Ð°ÑÑÐ¸Ð²Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ (Ð¾Ð¿Ð¾Ñ€Ñ‹).
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            ref ConstructionDTO dto = ref data.construction;
            dto.EnsureCapacity();
            dto.graphNodeCount = 0;
            dto.graphEdgeCount = 0;

            int moduleIndex = 0;
            int count = _spawnedModules.Count;

            for (int i = 0; i < count; i++)
            {
                GameObject module = _spawnedModules[i];

                // â”€â”€ Guard: destroyed reference â”€â”€
                if (module == null) continue;

                // â”€â”€ Guard: missing marker â”€â”€
                if (!module.TryGetComponent(out ModuleMarker marker))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has no ModuleMarker. " +
                        "Skipping save for this module.");
                    continue;
                }

                // â”€â”€ Guard: empty ID â”€â”€
                string prefabId = marker.PrefabId;
                if (string.IsNullOrEmpty(prefabId))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has empty PrefabId. " +
                        "Skipping.");
                    continue;
                }

                // â”€â”€ Guard: capacity â”€â”€
                if (moduleIndex >= ConstructionDTO.MaxModules)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Max modules ({ConstructionDTO.MaxModules}) reached. " +
                        $"Truncating save: {count - moduleIndex} modules not saved.");
                    break;
                }

                // â”€â”€ Serialize transform â”€â”€
                Transform t = module.transform;
                ModuleDTO moduleDto = new ModuleDTO();
                moduleDto.prefabId = prefabId;
                moduleDto.SetPosition(t.position);
                moduleDto.SetRotation(t.rotation);
                moduleDto.slottedToolItemId = string.Empty;

                ModuleGraphNodeDTO graphNodeDto = new ModuleGraphNodeDTO();
                graphNodeDto.prefabId = prefabId;
                graphNodeDto.moduleHashId = marker.Data != null ? marker.Data.ModuleHashId : 0;
                graphNodeDto.SetAup(AbsoluteUniversePosition.FromRuntimePosition(t.position));
                graphNodeDto.SetRotation(t.rotation);

                // â”€â”€ Serialize dynamic state â”€â”€
                // ÐŸÐ°ÑÑÐ¸Ð²Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ (Ð¾Ð¿Ð¾Ñ€Ñ‹, Ð´ÐµÐºÐ¾Ñ€) Ð½Ðµ Ð¸Ð¼ÐµÑŽÑ‚ BaseModule.
                // Ð”Ð»Ñ Ð½Ð¸Ñ… Ð¿Ð¸ÑˆÐµÐ¼ Ð´ÐµÑ„Ð¾Ð»Ñ‚Ð½Ñ‹Ðµ Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ñ â€” Ð¿Ñ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ Ð¾Ð½Ð¸ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    moduleDto.integrity = baseModule.CurrentIntegrity;
                    moduleDto.repairIntegrityCap = baseModule.MaxRecoverableIntegrity;
                    moduleDto.airReserveNormalized = baseModule.AirReserveNormalized;
                    moduleDto.co2Normalized = baseModule.Co2Normalized;
                    moduleDto.isFlooded = baseModule.IsFlooded;
                    moduleDto.failureMode = (byte)baseModule.CurrentFailureMode;
                    moduleDto.floodedReefFloodSeconds = baseModule.FloodedReefFloodSeconds;
                    moduleDto.interiorReefInfestationActive = baseModule.InteriorReefInfestationActive;
                }
                else
                {
                    moduleDto.integrity = DefaultIntegrity;
                    moduleDto.repairIntegrityCap = DefaultIntegrity;
                    moduleDto.airReserveNormalized = 1f;
                    moduleDto.co2Normalized = 0f;
                    moduleDto.isFlooded = DefaultIsFlooded;
                    moduleDto.failureMode = (byte)BaseModuleFailureMode.None;
                    moduleDto.floodedReefFloodSeconds = 0f;
                    moduleDto.interiorReefInfestationActive = false;
                }

                if (module.TryGetComponent(out MaintenanceStationModule maintenanceStation) && maintenanceStation.HasSlottedTool)
                    moduleDto.slottedToolItemId = maintenanceStation.SlottedToolPersistentId;

                if (module.TryGetComponent(out LogisticsSorterModule logisticsSorter))
                    logisticsSorter.PopulateSaveData(ref moduleDto);

                if (module.TryGetComponent(out DeepDrillModule deepDrill))
                    deepDrill.PopulateSaveData(ref moduleDto);

                if (module.TryGetComponent(out CultivationManager cultivationManager))
                    cultivationManager.PopulateSaveData(ref moduleDto, ResolvePlayerItemCatalog());

                if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))
                    logisticsPipe.PopulateSaveData(ref moduleDto);

                dto.modules[moduleIndex] = moduleDto;
                dto.graphNodes[moduleIndex] = graphNodeDto;
                moduleIndex++;
            }

            dto.moduleCount = moduleIndex;
            dto.graphNodeCount = moduleIndex;
            PopulateGraphEdges(ref dto, moduleIndex);
        }

        /// <summary>
        /// Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ð¸Ð· ConstructionDTO.
        ///
        /// ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº:
        ///   1. ClearAllModules() â€” ÑƒÐ´Ð°Ð»Ð¸Ñ‚ÑŒ Ñ‚ÐµÐºÑƒÑ‰ÑƒÑŽ Ð±Ð°Ð·Ñƒ
        ///   2. Ð”Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ ModuleDTO:
        ///      a. ÐÐ°Ð¹Ñ‚Ð¸ Ð¿Ñ€ÐµÑ„Ð°Ð± Ñ‡ÐµÑ€ÐµÐ· ModuleCatalog
        ///      b. Spawn Ñ‡ÐµÑ€ÐµÐ· ObjectPoolManager
        ///      c. Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð¾Ð²Ð¸Ñ‚ÑŒ Ð´Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¾Ðµ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ (integrity, isFlooded)
        ///         Ð”Ðž Ð¿ÐµÑ€Ð²Ð¾Ð³Ð¾ SlowTick (ÑÐ¸Ð½Ñ…Ñ€Ð¾Ð½Ð½Ð¾, Ð² Ñ‚Ð¾Ð¼ Ð¶Ðµ ÐºÐ°Ð´Ñ€Ðµ)
        ///      d. RegisterModule (Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¾Ð¹ BuildableData)
        ///
        /// ÐœÐ¸Ð³Ñ€Ð°Ñ†Ð¸Ñ v1 â†’ v2: ÐµÑÐ»Ð¸ integrity == 0f (Ð´ÐµÑ„Ð¾Ð»Ñ‚ Ð´Ð»Ñ float Ð¿Ñ€Ð¸
        /// Ð´ÐµÑÐµÑ€Ð¸Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾ ÑÐµÐ¹Ð²Ð° Ð±ÐµÐ· ÑÑ‚Ð¾Ð³Ð¾ Ð¿Ð¾Ð»Ñ), Ñ‚Ñ€Ð°ÐºÑ‚ÑƒÐµÐ¼ ÐºÐ°Ðº 100%.
        ///
        /// ÐŸÑ€Ð¸ Ð¾ÑˆÐ¸Ð±ÐºÐ°Ñ…: Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÐµÑ‚ÑÑ, Ð¸Ð³Ñ€Ð° Ð½Ðµ ÐºÑ€Ð°ÑˆÐ¸Ñ‚ÑÑ.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            // â”€â”€ Ð’Ð°Ð»Ð¸Ð´Ð°Ñ†Ð¸Ñ â”€â”€
            if (catalog == null)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog not assigned! " +
                    "Cannot load construction data.");
                return;
            }

            if (catalog.HasLookupAmbiguity)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog has ambiguous ID aliases. " +
                    $"Construction load aborted: {catalog.LookupAmbiguitySummary}");
                return;
            }

            ConstructionDTO dto = data.construction;
            ItemCatalog itemCatalog = ResolvePlayerItemCatalog();
            bool hasGraphTopology = data.version >= 47 &&
                                    dto.graphNodes != null &&
                                    dto.graphNodeCount > 0;

            // â”€â”€ 1. Ð£Ð´Ð°Ð»ÑÐµÐ¼ Ñ‚ÐµÐºÑƒÑ‰ÑƒÑŽ Ð±Ð°Ð·Ñƒ â”€â”€

            // â”€â”€ Guard: Ð¿ÑƒÑÑ‚Ñ‹Ðµ Ð´Ð°Ð½Ð½Ñ‹Ðµ â”€â”€
            if ((!hasGraphTopology && (dto.modules == null || dto.moduleCount <= 0)) ||
                (hasGraphTopology && dto.graphNodeCount <= 0))
            {
                ClearAllModules();
                Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // â”€â”€ 2. Ð ÐµÑÐ¿Ð°Ð²Ð½ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð¸Ð· ÑÐµÐ¹Ð²Ð° â”€â”€
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            ClearAllModules();
            int count = hasGraphTopology
                ? Mathf.Min(dto.graphNodeCount, dto.graphNodes.Length)
                : Mathf.Min(dto.moduleCount, dto.modules.Length);
            int loadedCount   = 0;
            int skippedCount  = 0;

            for (int i = 0; i < count; i++)
            {
                ModuleGraphNodeDTO graphNodeDto = hasGraphTopology ? dto.graphNodes[i] : default;
                bool hasLegacyModuleState = dto.modules != null && i >= 0 && i < dto.moduleCount && i < dto.modules.Length;
                ModuleDTO moduleDto = hasLegacyModuleState ? dto.modules[i] : default;

                // â”€â”€ ÐŸÐ¾Ð¸ÑÐº Ð¿Ñ€ÐµÑ„Ð°Ð±Ð° â”€â”€
                string prefabId = hasGraphTopology && !string.IsNullOrEmpty(graphNodeDto.prefabId)
                    ? graphNodeDto.prefabId
                    : moduleDto.prefabId;

                if (string.IsNullOrEmpty(prefabId) && (!hasGraphTopology || graphNodeDto.moduleHashId == 0))
                {
                    skippedCount++;
                    continue;
                }

                BuildableData buildData = !string.IsNullOrEmpty(prefabId)
                    ? catalog.FindDataById(prefabId)
                    : null;

                if (buildData == null && hasGraphTopology && graphNodeDto.moduleHashId != 0)
                    buildData = catalog.FindDataByHashId(graphNodeDto.moduleHashId);

                if (buildData == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' " +
                        "not found in catalog. Skipping.");
                    skippedCount++;
                    continue;
                }

                // â”€â”€ Ð’Ð°Ð»Ð¸Ð´Ð°Ñ†Ð¸Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ â”€â”€
                Vector3 pos = hasGraphTopology
                    ? graphNodeDto.GetAup().ToRuntimeFloat3()
                    : moduleDto.GetPosition();
                Quaternion rot = hasGraphTopology
                    ? graphNodeDto.GetRotation()
                    : moduleDto.GetRotation();

                if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) ||
                    float.IsNaN(pos.y) || float.IsInfinity(pos.y) ||
                    float.IsNaN(pos.z) || float.IsInfinity(pos.z))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "has invalid position. Skipping.");
                    skippedCount++;
                    continue;
                }

                // ÐÐ¾Ñ€Ð¼Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ quaternion (Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ float-Ð´Ñ€Ð¸Ñ„Ñ‚Ð° Ð² ÑÐµÐ¹Ð²Ðµ)
                if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                    rot = Quaternion.identity;
                else
                    rot.Normalize();

                // â”€â”€ Spawn â”€â”€
                GameObject module;
                if (buildData.finalPrefab != null)
                {
                    if (pool == null)
                    {
                        Debug.LogWarning(
                            $"[ConstructionManager] ObjectPoolManager unavailable while loading '{prefabId}'. Skipping pooled prefab.");
                        skippedCount++;
                        continue;
                    }

                    module = pool.Spawn(buildData.finalPrefab, pos, rot);
                }
                else if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(buildData, pos, rot, out module))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' has no finalPrefab and proxy generation failed. Skipping.");
                    skippedCount++;
                    continue;
                }

                if (module == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Failed to spawn '{prefabId}'.");
                    skippedCount++;
                    continue;
                }

                // â”€â”€ Restore dynamic state â”€â”€
                // Ð’ÐÐ–ÐÐž: Ð²Ñ‹Ð¿Ð¾Ð»Ð½ÑÐµÑ‚ÑÑ ÑÐ¸Ð½Ñ…Ñ€Ð¾Ð½Ð½Ð¾, Ð”Ðž Ð¿ÐµÑ€Ð²Ð¾Ð³Ð¾ SlowTick.
                // BaseModule.OnEnable() Ñ€ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ SlowTick, Ð½Ð¾ Ð¿ÐµÑ€Ð²Ñ‹Ð¹
                // Ð²Ñ‹Ð·Ð¾Ð² Ð¿Ñ€Ð¾Ð¸Ð·Ð¾Ð¹Ð´Ñ‘Ñ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² ÑÐ»ÐµÐ´ÑƒÑŽÑ‰ÐµÐ¼ Ð¸Ð½Ñ‚ÐµÑ€Ð²Ð°Ð»Ðµ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð°.
                // Ðš ÑÑ‚Ð¾Ð¼Ñƒ Ð¼Ð¾Ð¼ÐµÐ½Ñ‚Ñƒ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ ÑƒÐ¶Ðµ Ð±ÑƒÐ´ÐµÑ‚ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¾.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    baseModule.ApplyBuildableTemplate(buildData);

                    // ÐœÐ¸Ð³Ñ€Ð°Ñ†Ð¸Ñ v1 â†’ v2: integrity == 0f Ð¾Ð·Ð½Ð°Ñ‡Ð°ÐµÑ‚,
                    // Ñ‡Ñ‚Ð¾ Ð¿Ð¾Ð»Ðµ Ð½Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð¾Ð²Ð°Ð»Ð¾ Ð² ÑÑ‚Ð°Ñ€Ð¾Ð¼ ÑÐµÐ¹Ð²Ðµ.
                    // Ð¢Ñ€Ð°ÐºÑ‚ÑƒÐµÐ¼ ÐºÐ°Ðº Â«Ð¿Ð¾Ð»Ð½Ð¾Ðµ Ð·Ð´Ð¾Ñ€Ð¾Ð²ÑŒÐµÂ».
                    float loadedIntegrity = moduleDto.integrity;
                    if (loadedIntegrity <= 0f)
                        loadedIntegrity = DefaultIntegrity;

                    float loadedRepairCap = moduleDto.repairIntegrityCap;
                    if (loadedRepairCap <= 0f)
                        loadedRepairCap = baseModule.MaxIntegrity;

                    float loadedAirReserveNormalized = data.version >= 28
                        ? Mathf.Clamp01(moduleDto.airReserveNormalized)
                        : 1f;
                    float loadedCo2Normalized = data.version >= 34
                        ? Mathf.Clamp01(moduleDto.co2Normalized)
                        : 0f;
                    float loadedFloodedReefFloodSeconds = data.version >= 49
                        ? Mathf.Max(0f, moduleDto.floodedReefFloodSeconds)
                        : 0f;
                    bool loadedInteriorReefInfestationActive = data.version >= 49 && moduleDto.interiorReefInfestationActive;

                    baseModule.SetState(
                        loadedIntegrity,
                        moduleDto.isFlooded,
                        (BaseModuleFailureMode)moduleDto.failureMode,
                        loadedRepairCap,
                        loadedAirReserveNormalized,
                        loadedCo2Normalized,
                        loadedFloodedReefFloodSeconds,
                        loadedInteriorReefInfestationActive);
                }

                if (hasLegacyModuleState &&
                    data.version >= 35 &&
                    itemCatalog != null &&
                    !string.IsNullOrWhiteSpace(moduleDto.slottedToolItemId) &&
                    module.TryGetComponent(out MaintenanceStationModule maintenanceStation))
                {
                    ItemData slottedToolItem = itemCatalog.FindById(moduleDto.slottedToolItemId);
                    if (slottedToolItem != null)
                        maintenanceStation.TryRestoreSlottedTool(slottedToolItem);
                }

                // â”€â”€ Register Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¾Ð¹ Ðº BuildableData â”€â”€
                if (hasLegacyModuleState && data.version >= 36 && itemCatalog != null)
                {
                    if (module.TryGetComponent(out LogisticsSorterModule logisticsSorter))
                        logisticsSorter.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out DeepDrillModule deepDrill))
                        deepDrill.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out CultivationManager cultivationManager))
                        cultivationManager.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))
                        logisticsPipe.RestoreFromSaveData(moduleDto, itemCatalog);
                }

                RegisterModule(module, buildData);
                loadedCount++;
            }

            Debug.Log(
                $"[ConstructionManager] Loaded {loadedCount} modules" +
                (skippedCount > 0 ? $", skipped {skippedCount}." : "."));

            UpdateDiagnostics();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” COLLECTION HELPERS (Zero GC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÑ‚ Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ñ Ð¿Ð¾ ÑÑÑ‹Ð»ÐºÐµ. O(n), Ð½Ð¾ Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ
        /// Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿Ñ€Ð¸ Register (Ñ€ÐµÐ´ÐºÐ¾). Zero GC.
        /// </summary>
        private void PopulateGraphEdges(ref ConstructionDTO dto, int savedNodeCount)
        {
            dto.graphEdgeCount = 0;
            if (_habitatGraphManager == null || savedNodeCount <= 0 || _habitatGraphManager.NodeCount != savedNodeCount)
                return;

            NativeArray<int> edgeOffsets = _habitatGraphManager.EdgeOffsets;
            NativeArray<int> edgeDestinations = _habitatGraphManager.EdgeDestinations;
            int edgeWriteIndex = 0;

            for (int sourceIndex = 0; sourceIndex < savedNodeCount; sourceIndex++)
            {
                int edgeStart = edgeOffsets[sourceIndex];
                int edgeEnd = edgeOffsets[sourceIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationIndex = edgeDestinations[edgeIndex];
                    if (destinationIndex <= sourceIndex || destinationIndex >= savedNodeCount)
                        continue;

                    if (edgeWriteIndex >= ConstructionDTO.MaxGraphEdges)
                    {
                        Debug.LogWarning(
                            $"[ConstructionManager] Habitat graph edge budget ({ConstructionDTO.MaxGraphEdges}) exceeded during save. Truncating persisted topology.");
                        dto.graphEdgeCount = edgeWriteIndex;
                        return;
                    }

                    dto.graphEdges[edgeWriteIndex] = new ModuleGraphEdgeDTO
                    {
                        sourceNodeIndex = sourceIndex,
                        destinationNodeIndex = destinationIndex
                    };
                    edgeWriteIndex++;
                }
            }

            dto.graphEdgeCount = edgeWriteIndex;
        }

        private static void DespawnOrDestroyModuleInstance(GameObject module, ObjectPoolManager pool)
        {
            if (module == null)
                return;

            if (module.TryGetComponent(out ConstructionRuntimeProxyTag _))
            {
                Destroy(module);
                return;
            }

            if (pool != null)
                pool.Despawn(module);
            else
                Destroy(module);
        }

        private static ItemCatalog ResolvePlayerItemCatalog()
        {
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            return inventory != null ? inventory.ItemCatalog : null;
        }

        private bool ContainsRef(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Swap-remove: O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ Ð±ÐµÐ· ÑÐ´Ð²Ð¸Ð³Ð° Ð¼Ð°ÑÑÐ¸Ð²Ð°.
        /// ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð½Ðµ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½ (Ð´Ð¾Ð¿ÑƒÑÑ‚Ð¸Ð¼Ð¾ Ð´Ð»Ñ ÑÑ‚Ð¾Ð¹ ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹).
        /// </summary>
        private void SwapRemove(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                {
                    int last = count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                    return;
                }
            }
        }

        /// <summary>
        /// ÐžÑ‡Ð¸Ñ‰Ð°ÐµÑ‚ null-ÑÑÑ‹Ð»ÐºÐ¸ Ð¸Ð· ÑÐ¿Ð¸ÑÐºÐ° (Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ Destroy Ð¸Ð·Ð²Ð½Ðµ).
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¿ÐµÑ€ÐµÐ´ Save Ð´Ð»Ñ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ð¸ Ñ†ÐµÐ»Ð¾ÑÑ‚Ð½Ð¾ÑÑ‚Ð¸.
        /// </summary>
        private void PurgeNullEntries()
        {
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                if (_spawnedModules[i] == null)
                {
                    int last = _spawnedModules.Count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void TryRegisterLogisticsService()
        {
            if (_logisticsServiceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterLogisticsService(this);
            _logisticsServiceRegistered = true;
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = GlobalRegistry.Save;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = GlobalRegistry.Save;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
        }

        private void TryUnregisterLogisticsService()
        {
            if (!_logisticsServiceRegistered)
                return;

            GlobalRegistry.UnregisterLogisticsService(this);
            _logisticsServiceRegistered = false;
        }

        private void TryTriggerAmbientAccident()
        {
            PurgeNullEntries();

            int count = _spawnedModules.Count;
            if (count <= 0)
                return;

            BaseModule candidate = null;
            float bestRisk = ambientAccidentMinRisk;
            int startIndex = _ambientAccidentCursor % count;

            for (int offset = 0; offset < count; offset++)
            {
                int index = (startIndex + offset) % count;
                GameObject moduleObject = _spawnedModules[index];
                if (moduleObject == null || !moduleObject.TryGetComponent(out BaseModule module))
                    continue;

                if (!TryEvaluateAmbientAccidentRisk(module, out float risk))
                    continue;

                if (risk <= bestRisk)
                    continue;

                bestRisk = risk;
                candidate = module;
                _ambientAccidentCursor = index + 1;
            }

            if (candidate == null)
                return;

            float accidentChance = Mathf.Clamp01(ambientAccidentBaseChance * bestRisk);
            if (UnityEngine.Random.value > accidentChance)
                return;

            TriggerAmbientAccident(candidate, bestRisk);
        }

        private bool TryEvaluateAmbientAccidentRisk(BaseModule module, out float risk)
        {
            risk = 0f;

            if (module == null)
                return false;

            if (module.HasCascadeFailure || module.CurrentIntegrity <= 0f || module.MaxIntegrity <= 0f)
                return false;

            float integrity01 = module.CurrentIntegrity / module.MaxIntegrity;
            if (integrity01 >= 0.999f && module.HasPower && !module.IsFlooded)
                return false;

            risk = 1f - integrity01;

            if (integrity01 <= ambientAccidentIntegrityThreshold)
                risk += 0.25f;

            if (!module.HasPower)
                risk += 0.2f;

            if (module.IsFlooded)
                risk += 0.35f;

            return risk >= ambientAccidentMinRisk;
        }

        private static void TriggerAmbientAccident(BaseModule module, float risk)
        {
            if (module == null)
                return;

            string source = ResolveModuleSource(module);
            string summary = BuildAmbientAccidentSummary(module, risk);
            FieldOperationLogSystem.RecordOperation(source, "SERVICE ACCIDENT", summary, "WARN");

            module.ApplyDamage(module.CurrentIntegrity + 1f);
        }

        private static string ResolveModuleSource(BaseModule module)
        {
            if (module != null && module.TryGetComponent(out ModuleMarker marker) && marker.Data != null)
            {
                string moduleName = marker.Data.moduleName;
                if (!string.IsNullOrWhiteSpace(moduleName))
                    return moduleName;
            }

            return "BASE";
        }

        private static string BuildAmbientAccidentSummary(BaseModule module, float risk)
        {
            if (module == null)
                return "Neglected service hardware destabilized and rolled into a cascade failure.";

            float integrity01 = module.MaxIntegrity > 0f
                ? module.CurrentIntegrity / module.MaxIntegrity
                : 0f;

            string condition;
            if (module.IsFlooded)
                condition = "Residual flooding was left unresolved.";
            else if (!module.HasPower)
                condition = "Power loss left pumps and service recovery offline.";
            else
                condition = "Hull fatigue crossed the unattended maintenance margin.";

            return $"Integrity {integrity01:0%}. {condition} Risk {Mathf.Clamp01(risk):0%} converted into a live compartment incident.";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugModuleCount = _spawnedModules.Count;
        }

        private void RefreshHabitatGraph()
        {
            if (_habitatGraphManager == null || _spawnedModules == null)
                return;

            _habitatGraphManager.Rebuild(_spawnedModules);
        }

        internal void NotifyModuleEmergencyStateChanged(BaseModule module)
        {
            if (_habitatGraphManager == null)
                return;

            _habitatGraphManager.NotifyModuleEmergencyStateChanged(module);
        }

        internal void NotifyModuleParasiteRootStateChanged(BaseModule module)
        {
            if (_habitatGraphManager == null || module == null)
                return;

            RefreshHabitatGraph();
        }
    }
}
