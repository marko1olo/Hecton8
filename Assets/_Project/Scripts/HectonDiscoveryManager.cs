// ============================================================================
// HECTON-8 - HectonDiscoveryManager.cs
// Отслеживает открытые биомы и сохраняет последнее корректно подтвержденное
// открытие для PDA и других систем прогрессии.
//
// ВЕРСИЯ: production pass с восстановлением latest biome и кэшированием HUD
// ============================================================================

using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Централизованный реестр открытых игроком биомов.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hecton Discovery Manager")]
    public sealed class HectonDiscoveryManager : MonoBehaviour, ISaveable
    {
        private const int MinBiomeId = BiomeDiscoveryBitMask.MinBiomeId;
        private const int MaxBiomeId = BiomeDiscoveryBitMask.MaxBiomeId;
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR - REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("Реестр всех 108 биомов для именования и PDA-представления.")]
        [SerializeField] private HectonBiomeRegistry _registry;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private readonly HashSet<int> _discoveredBiomeIds = new HashSet<int>();
        private bool _registeredWithSaveManager;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public static HectonDiscoveryManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>
        /// Последний корректно подтвержденный ID открытого биома.
        /// </summary>
        public int LastDiscoveredId { get; private set; } = InvalidBiomeId;

        /// <summary>
        /// Количество открытых биомов.
        /// </summary>
        public int TotalDiscovered => _discoveredBiomeIds.Count;

        /// <inheritdoc />
        public int SavePriority => 20;

        /// <inheritdoc />
        public int LoadPriority => 20;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается один раз при первом открытии нового биома.
        /// </summary>
        public event Action<int> OnBiomeDiscovered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            TryRegisterWithSaveManager();
        }

        private void Start()
        {
            TryRegisterWithSaveManager();
        }

        private void OnDisable()
        {
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Помечает биом как открытый, если игрок зашел в него впервые.
        /// </summary>
        /// <param name="biomeId">Идентификатор биома из матрицы 1..108.</param>
        public void DiscoverBiome(int biomeId)
        {
            if (!IsValidBiomeId(biomeId))
                return;

            if (!_discoveredBiomeIds.Add(biomeId))
                return;

            LastDiscoveredId = biomeId;

            string biomeName = GetBiomeName(biomeId);
            LogBiomeDiscovered(biomeName, biomeId, this);

            OnBiomeDiscovered?.Invoke(biomeId);
            HectonEventBus.Publish(new BiomeDiscoveredEvent(biomeId, biomeName));

            NotificationEvents.PushInfo(string.Format(
                ResolveLocalized(LocalizationKeys.DISCOVERY_NEW_BIOME, "NEW BIOME DISCOVERED: {0}"),
                biomeName));
        }

        /// <summary>
        /// Проверяет, открыт ли указанный биом.
        /// </summary>
        public bool IsDiscovered(int biomeId)
        {
            return _discoveredBiomeIds.Contains(biomeId);
        }

        /// <summary>
        /// Возвращает отображаемое имя биома.
        /// </summary>
        public string GetBiomeName(int id)
        {
            if (!IsValidBiomeId(id))
                return "NO RECENT BIOME";

            if (_registry != null)
            {
                HectonBiomeRegistry.BiomeEntry entry = _registry.GetBiome(id);
                if (!string.IsNullOrEmpty(entry.name))
                    return entry.name.ToUpperInvariant();
            }

            return $"BIOME {id}";
        }

        /// <summary>
        /// Возвращает данные биома из реестра.
        /// </summary>
        public HectonBiomeRegistry.BiomeEntry GetBiomeData(int id)
        {
            if (_registry == null || !IsValidBiomeId(id))
                return default;

            return _registry.GetBiome(id);
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            BiomeDiscoveryBitMask.EnsureCapacity(ref data.discoveredBiomeBitWords);
            BiomeDiscoveryBitMask.Pack(_discoveredBiomeIds, data.discoveredBiomeBitWords);
            data.discoveredBiomeIds = null;
            data.lastDiscoveredBiomeId = IsValidBiomeId(LastDiscoveredId) &&
                                         _discoveredBiomeIds.Contains(LastDiscoveredId)
                ? LastDiscoveredId
                : ResolveFallbackLastDiscoveredId();
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _discoveredBiomeIds.Clear();
            LastDiscoveredId = InvalidBiomeId;

            if (data == null)
                return;

            if (BiomeDiscoveryBitMask.HasAnySet(data.discoveredBiomeBitWords))
            {
                BiomeDiscoveryBitMask.Unpack(data.discoveredBiomeBitWords, _discoveredBiomeIds);
            }
            else if (data.discoveredBiomeIds != null)
            {
                foreach (int biomeId in data.discoveredBiomeIds)
                {
                    if (IsValidBiomeId(biomeId))
                        _discoveredBiomeIds.Add(biomeId);
                }
            }

            if (IsValidBiomeId(data.lastDiscoveredBiomeId) &&
                _discoveredBiomeIds.Contains(data.lastDiscoveredBiomeId))
            {
                LastDiscoveredId = data.lastDiscoveredBiomeId;
                return;
            }

            LastDiscoveredId = ResolveFallbackLastDiscoveredId();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private static bool IsValidBiomeId(int biomeId)
        {
            return biomeId >= MinBiomeId && biomeId <= MaxBiomeId;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private int ResolveFallbackLastDiscoveredId()
        {
            for (int biomeId = MinBiomeId; biomeId <= MaxBiomeId; biomeId++)
            {
                if (_discoveredBiomeIds.Contains(biomeId))
                    return biomeId;
            }

            return InvalidBiomeId;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogBiomeDiscovered(string biomeName, int biomeId, UnityEngine.Object context)
        {
            UnityEngine.Debug.Log($"[Discovery] New biome discovered: {biomeName} (ID {biomeId}).", context);
        }


        private void TryRegisterWithSaveManager()
        {
            if (_registeredWithSaveManager)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredWithSaveManager = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredWithSaveManager)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredWithSaveManager = false;
        }
    }
}
