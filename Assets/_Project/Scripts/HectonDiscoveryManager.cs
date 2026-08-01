// ============================================================================
// HECTON-8 - HectonDiscoveryManager.cs
// Otslezhivaet otkrytye biomy i sohranyaet poslednee korrektno podtverzhdennoe
// otkrytie dlya PDA i drugih sistem progressii.
//
// VERSIYa: production pass s vosstanovleniem latest biome i keshirovaniem HUD
// ============================================================================

using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.AI;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Tsentralizovannyy reestr otkrytyh igrokom biomov.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hecton Discovery Manager")]
    public sealed class HectonDiscoveryManager : MonoBehaviour, ISaveable, IScanEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MinBiomeId = BiomeDiscoveryBitMask.MinBiomeId;
        private const int MaxBiomeId = BiomeDiscoveryBitMask.MaxBiomeId;
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;
        private const byte FaunaBestiaryBehaviorThreshold = 1;
        private const byte FaunaBestiaryDietThreshold = 5;
        private const byte FaunaBestiaryVulnerabilityThreshold = 10;
        private const int DiscoveredBiomeCapacity = MaxBiomeId - MinBiomeId + 1;
        private const string MissingBiomeFallbackName = "BIOME UNKNOWN";
        private static readonly uint _BiomeDiscoveryNotificationMissWarningHash =
            unchecked((uint)LocHash.Compute("HectonDiscoveryManager.BiomeNotificationMiss"));
        private static readonly uint _BiomeDiscoveryNotificationContextHash =
            unchecked((uint)LocHash.Compute("HectonDiscoveryManager.BiomeNotification"));

        // ----------------------------------------------------------
        //  INSPECTOR - REFERENCES
        // ----------------------------------------------------------

        [Header("-- References ------------------------------")]
        [Tooltip("Reestr vseh 108 biomov dlya imenovaniya i PDA-predstavleniya.")]
        [SerializeField] private HectonBiomeRegistry _registry;

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        // COLD ALLOC: HashSet<int>[DiscoveredBiomeCapacity] - discovered biome ids keyed by biome registry id - owner: HectonDiscoveryManager
        private readonly HashSet<int> _discoveredBiomeIds = new HashSet<int>(DiscoveredBiomeCapacity);
        // COLD ALLOC: Dictionary<uint,byte>[64] — runtime fauna bestiary observation counters keyed by scan entry hash — owner: HectonDiscoveryManager
        private readonly Dictionary<uint, byte> _faunaInteractionCounts = new Dictionary<uint, byte>(64);
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(160); // COLD ALLOC: char[160] - biome discovery notification staging buffer - owner: HectonDiscoveryManager
        private bool _registeredWithSaveManager;
        private bool _registeredWithScanEvents;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private int _biomeDiscoveryNotificationMissCount;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        /// <summary>
        /// Posledniy korrektno podtverzhdennyy ID otkrytogo bioma.
        /// </summary>
        public int LastDiscoveredId { get; private set; } = InvalidBiomeId;

        /// <summary>
        /// Kolichestvo otkrytyh biomov.
        /// </summary>
        public int TotalDiscovered => _discoveredBiomeIds.Count;
        public int BiomeDiscoveryNotificationMissCount => _biomeDiscoveryNotificationMissCount;

        /// <inheritdoc />
        public int SavePriority => 20;

        /// <inheritdoc />
        public int LoadPriority => 20;

        // ----------------------------------------------------------
        //  EVENTS
        // ----------------------------------------------------------

        /// <summary>
        /// Vyzyvaetsya odin raz pri pervom otkrytii novogo bioma.
        /// </summary>
        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        /// <summary>
        /// Resolve-or-create the sole HectonDiscoveryManager / GlobalRegistry.Discovery owner.
        /// Script GUID 56aa89edaf4f263419dd966a1cc4c197 has ZERO scene/prefab hits.
        /// OnEnable only registers when already present; without this factory the discovery
        /// slot stays permanently null (DynamicDifficultyDirector, GlobalProfileManager,
        /// PlayerExplorationTracker, PlayerAchievementRegistry consumers).
        /// </summary>
        public static HectonDiscoveryManager EnsureRuntimeInstance()
        {
            HectonDiscoveryManager registered = GlobalRegistry.Discovery;
            if (IsDiscoveryRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterDiscoveryRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[HectonDiscoveryManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<HectonDiscoveryManager>();
        }

        private static bool IsDiscoveryRuntimeUsable(HectonDiscoveryManager manager)
        {
            return !ReferenceEquals(manager, null) &&
                   manager != null &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        private void OnEnable()
        {
            TryRegisterService();
            _saveService = GlobalRegistry.Save;
            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
            TryRegisterWithScanEvents();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterWithSaveManager();
            TryRegisterWithScanEvents();
        }

        private void OnDisable()
        {
            UnregisterFromSaveManager();
            UnregisterFromScanEvents();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearBiomeDiscoveryNotificationDiagnostics();

        }

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

        /// <summary>
        /// Pomechaet biom kak otkrytyy, esli igrok zashel v nego vpervye.
        /// </summary>
        /// <param name="biomeId">Identifikator bioma iz matritsy 1..108.</param>
        public void DiscoverBiome(int biomeId)
        {
            if (!IsValidBiomeId(biomeId))
                return;

            if (!_discoveredBiomeIds.Add(biomeId))
                return;

            LastDiscoveredId = biomeId;

            ProgressionMetaSignalRoute.TryPublishBiomeDiscovered(biomeId);
            PushBiomeDiscoveredNotification(biomeId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogBiomeDiscovered(GetBiomeName(biomeId), biomeId, this);
#endif
        }

        /// <summary>
        /// Returns whether the biome has already been discovered.
        /// </summary>
        public bool IsDiscovered(int biomeId)
        {
            return _discoveredBiomeIds.Contains(biomeId);
        }

        /// <summary>
        /// Vozvraschaet otobrazhaemoe imya bioma.
        /// </summary>
        public string GetBiomeName(int id)
        {
            if (!IsValidBiomeId(id))
                return "NO RECENT BIOME";

            if (_registry != null)
            {
                HectonBiomeRegistry.BiomeEntry entry = _registry.GetBiome(id);
                if (!string.IsNullOrEmpty(entry.name))
                    return entry.name;
            }

            return MissingBiomeFallbackName;
        }

        /// <summary>
        /// Vozvraschaet dannye bioma iz reestra.
        /// </summary>
        public HectonBiomeRegistry.BiomeEntry GetBiomeData(int id)
        {
            if (_registry == null || !IsValidBiomeId(id))
                return default;

            return _registry.GetBiome(id);
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            ScanEventType eventType = (ScanEventType)payload.EventType;
            if (payload.EntryHash == 0u ||
                (eventType != ScanEventType.FaunaFeedingObserved &&
                 eventType != ScanEventType.FaunaMatingObserved))
            {
                return;
            }

            TryRecordFaunaObservation(payload.EntryHash);
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

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
            ClearBiomeDiscoveryNotificationDiagnostics();
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
                HashSet<int>.Enumerator biomeEnumerator = data.discoveredBiomeIds.GetEnumerator();
                while (biomeEnumerator.MoveNext())
                {
                    int biomeId = biomeEnumerator.Current;
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

        // ----------------------------------------------------------
        //  PRIVATE METHODS
        // ----------------------------------------------------------

        private static bool IsValidBiomeId(int biomeId)
        {
            return biomeId >= MinBiomeId && biomeId <= MaxBiomeId;
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = Hecton8.Core.GlobalRegistry.LocalizationText;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private void PushBiomeDiscoveredNotification(int biomeId)
        {
            _notificationBuffer.Clear();
            ReadOnlySpan<char> template = ResolveLocalizedSpan(LocalizationKeys.DISCOVERY_NEW_BIOME, "NEW BIOME DISCOVERED: {0}");

            if (!AppendTemplateSingleArgument(ref _notificationBuffer, template, ResolveBiomeNameSpan(biomeId)))
                return;

            if (NotificationEvents.TryPushInfo(_notificationBuffer.AsSpan()))
                return;

            ReportBiomeDiscoveryNotificationMiss(biomeId);
        }

        private void ReportBiomeDiscoveryNotificationMiss(int biomeId)
        {
            _biomeDiscoveryNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _BiomeDiscoveryNotificationMissWarningHash,
                _BiomeDiscoveryNotificationContextHash ^ unchecked((uint)biomeId),
                Mathf.Max(1, _biomeDiscoveryNotificationMissCount));
        }

        private void ClearBiomeDiscoveryNotificationDiagnostics()
        {
            _biomeDiscoveryNotificationMissCount = 0;
        }

        private ReadOnlySpan<char> ResolveBiomeNameSpan(int biomeId)
        {
            if (!IsValidBiomeId(biomeId) || _registry == null)
                return MissingBiomeFallbackName.AsSpan();

            HectonBiomeRegistry.BiomeEntry entry = _registry.GetBiome(biomeId);
            return string.IsNullOrWhiteSpace(entry.name)
                ? MissingBiomeFallbackName.AsSpan()
                : entry.name.AsSpan();
        }

        private static bool AppendTemplateSingleArgument(ref FixedCharBuffer buffer, ReadOnlySpan<char> template, ReadOnlySpan<char> argument)
        {
            if (template.Length <= 0)
                return buffer.Append(argument);

            int tokenIndex = IndexOfToken0(template);
            if (tokenIndex < 0)
            {
                return buffer.Append(template) &&
                       buffer.Append(" ".AsSpan()) &&
                       buffer.Append(argument);
            }

            return buffer.Append(template.Slice(0, tokenIndex)) &&
                   AppendUpper(ref buffer, argument) &&
                   buffer.Append(template.Slice(tokenIndex + 3));
        }

        private static bool AppendUpper(ref FixedCharBuffer buffer, ReadOnlySpan<char> value)
        {
            Span<char> single = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);

                single[0] = c;
                if (!buffer.Append(single))
                    return false;
            }

            return true;
        }

        private static int IndexOfToken0(ReadOnlySpan<char> text)
        {
            for (int i = 0; i <= text.Length - 3; i++)
            {
                if (text[i] == '{' && text[i + 1] == '0' && text[i + 2] == '}')
                    return i;
            }

            return -1;
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
            Hecton8.Core.H8Debug.Log("[Discovery] New biome discovered.", context);
        }


        private void TryRegisterWithSaveManager()
        {
            if (_registeredWithSaveManager || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _registeredWithSaveManager = true;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterDiscoveryRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Discovery, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterDiscoveryRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterWithScanEvents()
        {
            if (_registeredWithScanEvents)
                return;

            ScanEvents.Register(this);
            _registeredWithScanEvents = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredWithSaveManager && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _registeredWithSaveManager = false;
            _saveService = null;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            UnregisterFromSaveManager();
            _saveService = currentService as ISaveService;
            TryRegisterWithSaveManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void UnregisterFromScanEvents()
        {
            if (!_registeredWithScanEvents)
                return;

            ScanEvents.Unregister(this);
            _registeredWithScanEvents = false;
        }

        private void TryRecordFaunaObservation(uint entryHash)
        {
            if (!FaunaScanRuntimeRegistry.TryGetScanMetadata(entryHash, out FaunaScanRuntimeRegistry.FaunaScanMetadata metadata))
                return;

            byte previousCount = ResolveFaunaObservationCountFloor(entryHash, in metadata);
            byte nextCount = previousCount < byte.MaxValue
                ? (byte)(previousCount + 1)
                : byte.MaxValue;
            _faunaInteractionCounts[entryHash] = nextCount;
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryBehaviorThreshold, 0);
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryDietThreshold, 1);
            TryUnlockFaunaBestiaryMilestone(in metadata, previousCount, nextCount, FaunaBestiaryVulnerabilityThreshold, 2);
        }

        private byte ResolveFaunaObservationCountFloor(uint entryHash, in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata)
        {
            byte currentCount = 0;
            if (_faunaInteractionCounts.TryGetValue(entryHash, out byte storedCount))
                currentCount = storedCount;

            LoreDatabaseManager loreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (loreDatabase == null)
                return currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 2, out uint vulnerabilityHash) && loreDatabase.IsUnlocked(vulnerabilityHash))
                return currentCount < FaunaBestiaryVulnerabilityThreshold ? FaunaBestiaryVulnerabilityThreshold : currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 1, out uint dietHash) && loreDatabase.IsUnlocked(dietHash))
                return currentCount < FaunaBestiaryDietThreshold ? FaunaBestiaryDietThreshold : currentCount;

            if (TryResolveFaunaBestiaryLoreHash(in metadata, 0, out uint behaviorHash) && loreDatabase.IsUnlocked(behaviorHash))
                return currentCount < FaunaBestiaryBehaviorThreshold ? FaunaBestiaryBehaviorThreshold : currentCount;

            return currentCount;
        }

        private static bool TryResolveFaunaBestiaryLoreHash(in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata, int milestoneIndex, out uint loreHash)
        {
            uint[] authoredLoreHashes = metadata.LoreUnlockHashes;
            if (authoredLoreHashes != null &&
                milestoneIndex >= 0 &&
                milestoneIndex < authoredLoreHashes.Length &&
                authoredLoreHashes[milestoneIndex] != 0u)
            {
                loreHash = authoredLoreHashes[milestoneIndex];
                return true;
            }

            if (milestoneIndex == 0 && metadata.FullLoreHash != 0u)
            {
                loreHash = metadata.FullLoreHash;
                return true;
            }

            loreHash = 0u;
            return false;
        }

        private static void TryUnlockFaunaBestiaryMilestone(
            in FaunaScanRuntimeRegistry.FaunaScanMetadata metadata,
            byte previousCount,
            byte nextCount,
            byte threshold,
            int milestoneIndex)
        {
            if (previousCount >= threshold || nextCount < threshold || !TryResolveFaunaBestiaryLoreHash(in metadata, milestoneIndex, out uint loreHash))
                return;

            LoreDatabaseManager loreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (loreDatabase != null)
            {
                loreDatabase.TryUnlockByHash(loreHash);
                return;
            }

        }
    }
}
