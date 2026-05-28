namespace Hecton8.Items
{
    using System;
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Inventory;
    using UnityEngine;
    using UnityEngine.Serialization;
    using BuoyancyProfile = Hecton8.Physics.BuoyancyProfile;

    public enum ItemCategory
    {
        Miscellaneous = 0,
        Material = 1,
        Tool = 2,
        Equipment = 3,
        Consumable = 4,
        Component = 5,
        Organic = 6
    }

    public enum ResourceFamily
    {
        None = 0,
        StructuralMetal = 1,
        ElectronicsMetal = 2,
        Chemical = 3,
        Organic = 4,
        Crystal = 5,
        DeepMaterial = 6,
        Component = 7,
        Power = 8
    }

    public enum ProgressionTier
    {
        None = 0,
        Tier0 = 1,
        Tier1 = 2,
        Tier2 = 3,
        Tier3 = 4
    }

    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct DeconstructYieldEntry
    {
        public ItemData item;
        [Min(0)] public int minYield;
        [Min(0)] public int maxYield;

        public ItemData Item => item;

        /// <summary>
        /// Resolves an authored salvage range to one predictable midpoint value. No runtime RNG.
        /// </summary>
        public int ResolveDeterministicAmount()
        {
            int min = Mathf.Max(0, minYield);
            int max = Mathf.Max(min, maxYield);
            return min + ((max - min) >> 1);
        }

        /// <summary>
        /// Compatibility shim for old callers. Kept deterministic by design.
        /// </summary>
        public int ResolveRandomAmount()
        {
            return ResolveDeterministicAmount();
        }
    }

    /// <summary>
    /// Data-only item asset with localization-aware display fields.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Hecton/Item Data", order = 0)]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Legacy fallback item name used when no localization key is configured.")]
        [FormerlySerializedAs("itemName")]
        [SerializeField] private string legacyItemName = "Unnamed Item";
        [Tooltip("Stable item ID used by saves, quests, scanner archives, and future content packs. Leave empty to fall back to the asset name.")]
        [SerializeField] private string stableId = string.Empty;
        [Tooltip("Localized display name reference.")]
        [SerializeField] private LocalizedTextReference localizedItemName;
        public Sprite icon;
        [Tooltip("Legacy fallback description used when no localization key is configured.")]
        [FormerlySerializedAs("description")]
        [SerializeField, TextArea(2, 5)] private string legacyDescription = string.Empty;
        [Tooltip("Localized description reference.")]
        [SerializeField] private LocalizedTextReference localizedDescription;

        [Header("Properties")]
        public float weight = 1f;
        public bool stackable = true;
        public int maxStack = 64;

        [Header("Physical Metadata")]
        [Tooltip("When enabled, vulnerability, impact material, and rigidbody mass are derived from category/resource heuristics.")]
        [SerializeField] private bool autoResolvePhysicalMetadata = true;
        [Tooltip("Bitmask of tool capabilities that can physically affect this item in-world.")]
        [SerializeField] private uint vulnerabilityMask;
        [Tooltip("Procedural impact-audio family consumed by the DSP collision path.")]
        [SerializeField] private ItemAudioMaterialId audioMaterialId = ItemAudioMaterialId.Organic;
        [Tooltip("Exact rigidbody mass applied when the item is hydrated into world physics.")]
        [SerializeField, Min(0.05f)] private float massKg;
        [Tooltip("Physical occupied volume used by cargo and balance systems.")]
        [SerializeField, Min(0.0005f)] private float volumeM3;
        [Tooltip("Stable world-physics material family used to select a shared PhysicMaterial on dropped items.")]
        [SerializeField] private ItemPhysicsMaterialTag physicsMaterialTag = ItemPhysicsMaterialTag.Default;
        [Tooltip("Optional shared PhysicMaterial override applied to world colliders. Leave null to preserve the prefab default.")]
        [SerializeField] private PhysicsMaterial worldPhysicMaterial;
        [Tooltip("Inventory radiation dose emitted per second while carried. Zero disables isotope half-life logic.")]
        [SerializeField, Min(0f)] private float radiationSvPerSecond;

        [Header("Classification")]
        [Tooltip("Category used by UI filters and fabrication rules.")]
        public ItemCategory category = ItemCategory.Miscellaneous;
        [Tooltip("Resource family used by economy and scan logic.")]
        public ResourceFamily resourceFamily = ResourceFamily.None;
        [Tooltip("Progression band for world placement and crafting.")]
        public ProgressionTier progressionTier = ProgressionTier.None;
        [Tooltip("True when the item is a raw world resource.")]
        public bool isRawResource;

        [Header("Vertical Economy")]
        [Tooltip("Minimum authored depth in meters.")]
        public float minDepth;
        [Tooltip("Maximum authored depth in meters. Zero means no authored cap.")]
        public float maxDepth;

        [Header("Grid")]
        [Tooltip("Grid width in inventory cells.")]
        public int width = 1;
        [Tooltip("Grid height in inventory cells.")]
        public int height = 1;

        [Header("Consumable")]
        [Tooltip("Whether the item can be consumed.")]
        public bool isConsumable;
        [Tooltip("Time in seconds to consume this item (0 = instant).")]
        [SerializeField, Range(0f, 10f)] private float useDuration;
        [Tooltip("Oxygen restored on use.")]
        public float oxygenRestore;
        [Tooltip("Energy restored on use.")]
        public float energyRestore;
        [Tooltip("Suit integrity restored on use.")]
        public float integrityRestore;
        [Tooltip("Hunger restored on use.")]
        public float hungerRestore;
        [Tooltip("Thirst restored on use.")]
        public float thirstRestore;
        [Tooltip("Audio clip played when the item is consumed.")]
        public AudioClip useSound;
        [Tooltip("One-based SpatialAudioManager audio-event table id played when this item is consumed. Zero keeps the legacy clip fallback.")]
        [SerializeField, Min(0)] private uint useAudioEventId;

        [Header("Interaction")]
        [Tooltip("Legacy fallback interaction verb used when no localization key is configured.")]
        [FormerlySerializedAs("interactVerb")]
        [SerializeField] private string legacyInteractVerb = "Take";
        [Tooltip("Localized interaction verb reference.")]
        [SerializeField] private LocalizedTextReference localizedInteractVerb;

        [Header("World")]
        [Tooltip("Optional world prefab for dropping the item into the scene.")]
        public GameObject worldPrefab;
        [Tooltip("Buoyancy profile applied when this item exists in the world.")]
        public BuoyancyProfile worldBuoyancyProfile;

        [Header("Deconstruction")]
        [Tooltip("Direct salvage outputs. Recycling reads this authored table once; no recursive recipe expansion is performed.")]
        [SerializeField] private DeconstructYieldEntry[] deconstructYields = new DeconstructYieldEntry[0];

        private int _persistentHashId;
        private int _legacyNameHashId;
        private int _descriptionTableHashId;
        private GameLanguage _cachedLanguage = (GameLanguage)(-1);
        private string _cachedItemName = string.Empty;
        private string _cachedDescription = string.Empty;
        private string _cachedInteractVerb = string.Empty;

        public string itemName
        {
            get
            {
                EnsureLocalizedCache();
                return _cachedItemName;
            }
            set
            {
                legacyItemName = value ?? string.Empty;
                InvalidateLocalizedCache();
            }
        }

        public string description
        {
            get
            {
                EnsureLocalizedCache();
                return _cachedDescription;
            }
            set
            {
                legacyDescription = value ?? string.Empty;
                InvalidateLocalizedCache();
            }
        }

        public string interactVerb
        {
            get
            {
                EnsureLocalizedCache();
                return _cachedInteractVerb;
            }
            set
            {
                legacyInteractVerb = value ?? string.Empty;
                InvalidateLocalizedCache();
            }
        }

        /// <summary>
        /// Localized description with fallback to legacy text.
        /// </summary>
        public string DescriptionOrFallback
        {
            get
            {
                EnsureLocalizedCache();
                return _cachedDescription;
            }
        }

        /// <summary>
        /// Localized interaction verb with fallback to legacy text.
        /// </summary>
        public string InteractVerbOrFallback
        {
            get
            {
                EnsureLocalizedCache();
                return _cachedInteractVerb;
            }
        }

        /// <summary>
        /// Localization table key bound to the item name reference.
        /// </summary>
        public string ItemNameTableKey => localizedItemName.TableKey;

        /// <summary>
        /// Localization table key bound to the item description reference.
        /// </summary>
        public string DescriptionTableKey => localizedDescription.TableKey;
        public int DescriptionTableHashId => _descriptionTableHashId;

        /// <summary>
        /// Stable content identifier used by persistence-facing systems.
        /// </summary>
        public string PersistentId => string.IsNullOrWhiteSpace(stableId) ? name : stableId;
        public int PersistentHashId => _persistentHashId;
        public int LegacyNameHashId => _legacyNameHashId;
        public uint VulnerabilityMask => autoResolvePhysicalMetadata
            ? ItemPhysicalMetadataUtility.ResolveDefaultVulnerabilityMask(category, resourceFamily, PersistentId)
            : vulnerabilityMask;
        public ItemAudioMaterialId AudioMaterialId => autoResolvePhysicalMetadata
            ? ItemPhysicalMetadataUtility.ResolveDefaultAudioMaterialId(category, resourceFamily, PersistentId)
            : audioMaterialId;
        public byte AudioMaterialByte => (byte)AudioMaterialId;
        public float MassKg => autoResolvePhysicalMetadata
            ? ItemPhysicalMetadataUtility.ResolveDefaultMassKg(weight, width, height, category)
            : Mathf.Max(0.05f, massKg);
        public float VolumeM3 => autoResolvePhysicalMetadata
            ? ItemPhysicalMetadataUtility.ResolveDefaultVolumeM3(MassKg, width, height, category)
            : Mathf.Max(0.0005f, volumeM3);
        public ItemPhysicsMaterialTag PhysicsMaterialTag => autoResolvePhysicalMetadata
            ? ItemPhysicalMetadataUtility.ResolveDefaultPhysicsMaterialTag(category, resourceFamily, PersistentId)
            : physicsMaterialTag;
        public PhysicsMaterial WorldPhysicMaterial => worldPhysicMaterial;
        public float RadiationSvPerSecond => Mathf.Max(
            0f,
            radiationSvPerSecond > 0f
                ? radiationSvPerSecond
                : ItemPhysicalMetadataUtility.ResolveDefaultRadiationSvPerSecond(category, resourceFamily, PersistentId));
        public bool IsRadioactive => RadiationSvPerSecond > 0f;

        public int CellArea => width * height;

        /// <summary>Time in seconds to consume this item. 0 = instant.</summary>
        public float UseDuration => useDuration;
        public uint UseAudioEventId => useAudioEventId;

        public int DeconstructYieldCount => deconstructYields != null ? deconstructYields.Length : 0;

        public bool TryGetDeconstructYield(int index, out DeconstructYieldEntry entry)
        {
            entry = default;
            if (deconstructYields == null || (uint)index >= (uint)deconstructYields.Length)
                return false;

            entry = deconstructYields[index];
            return entry.Item != null && entry.maxYield > 0;
        }

        /// <summary>
        /// Returns true when the supplied ID matches the authored stable ID or the legacy asset name.
        /// </summary>
        public bool MatchesPersistentId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return MatchesPersistentHash(LocHash.Compute(id));
        }

        public bool MatchesPersistentHash(int hashId)
        {
            return hashId != 0 &&
                   (hashId == _persistentHashId || hashId == _legacyNameHashId);
        }

        private void OnEnable()
        {
            RefreshPersistentHash();
            InvalidateLocalizedCache();
            EnsureLocalizedCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RefreshPersistentHash();

            if (width < 1)
                width = 1;

            if (height < 1)
                height = 1;

            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            if (!autoResolvePhysicalMetadata && massKg < 0.05f)
                massKg = 0.05f;

            if (!autoResolvePhysicalMetadata && volumeM3 < 0.0005f)
                volumeM3 = 0.0005f;

            if (radiationSvPerSecond < 0f)
                radiationSvPerSecond = 0f;

            if (deconstructYields != null)
            {
                for (int i = 0; i < deconstructYields.Length; i++)
                {
                    if (deconstructYields[i].minYield < 0)
                        deconstructYields[i].minYield = 0;

                    if (deconstructYields[i].maxYield < deconstructYields[i].minYield)
                        deconstructYields[i].maxYield = deconstructYields[i].minYield;
                }
            }

            InvalidateLocalizedCache();
            EnsureLocalizedCache();
        }
#endif

        private void RefreshPersistentHash()
        {
            _persistentHashId = LocHash.Compute(PersistentId);
            _legacyNameHashId = LocHash.Compute(name);
            string descriptionTableKey = localizedDescription.TableKey;
            _descriptionTableHashId = string.IsNullOrWhiteSpace(descriptionTableKey)
                ? 0
                : LocHash.Compute(descriptionTableKey);
        }

        /// <summary>
        /// Returns the cached interaction prompt.
        /// </summary>
        public string GetInteractText()
        {
            EnsureLocalizedCache();
            return _cachedItemName;
        }

        /// <summary>
        /// Returns the cached interaction prompt through a cached localization owner.
        /// </summary>
        public string GetInteractText(ILocalizationTextReadModel manager)
        {
            EnsureLocalizedCache(manager);
            return _cachedItemName;
        }

        public bool TryWriteInteractText(ILocalizationTextReadModel manager, Span<char> destination, out int length)
        {
            length = 0;
            ReadOnlySpan<char> itemNameSpan = localizedItemName.ResolveSpanOrFallback(
                manager,
                FallbackOrDefault(legacyItemName, "Unnamed Item"));
            ReadOnlySpan<char> verbSpan = localizedInteractVerb.ResolveSpanOrFallback(
                manager,
                FallbackOrDefault(legacyInteractVerb, "Take"));

            if (verbSpan.IsEmpty)
                return TryAppendSpan(itemNameSpan, destination, ref length) && length > 0;

            return TryAppendSpan(verbSpan, destination, ref length) &&
                   TryAppendChar(' ', destination, ref length) &&
                   TryAppendSpan(itemNameSpan, destination, ref length) &&
                   length > 0;
        }

        /// <summary>
        /// Returns the localized item name through a cached localization owner.
        /// </summary>
        public string GetItemName(ILocalizationTextReadModel manager)
        {
            EnsureLocalizedCache(manager);
            return _cachedItemName;
        }

        /// <summary>
        /// Returns the localized description through a cached localization owner.
        /// </summary>
        public string GetDescription(ILocalizationTextReadModel manager)
        {
            EnsureLocalizedCache(manager);
            return _cachedDescription;
        }

        /// <summary>
        /// Returns the localized description as a span for caller-owned UI buffers.
        /// </summary>
        public ReadOnlySpan<char> GetDescriptionSpan(ILocalizationTextReadModel manager)
        {
            return localizedDescription.ResolveSpanOrFallback(manager, legacyDescription, _descriptionTableHashId);
        }

        /// <summary>
        /// Returns the localized interaction verb through a cached localization owner.
        /// </summary>
        public string GetInteractVerb(ILocalizationTextReadModel manager)
        {
            EnsureLocalizedCache(manager);
            return _cachedInteractVerb;
        }

        private void EnsureLocalizedCache()
        {
            ILocalizationTextReadModel manager = Hecton8.Core.GlobalRegistry.LocalizationText;
            EnsureLocalizedCache(manager);
        }

        private void EnsureLocalizedCache(ILocalizationTextReadModel manager)
        {
            GameLanguage language = manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;

            if (_cachedLanguage == language &&
                !string.IsNullOrEmpty(_cachedItemName))
            {
                return;
            }

            _cachedLanguage = language;
            _cachedItemName = ResolveLegacyFallback(localizedItemName, language, manager, legacyItemName, "Unnamed Item");
            _cachedDescription = ResolveLegacyFallback(localizedDescription, language, manager, legacyDescription, string.Empty);
            _cachedInteractVerb = ResolveLegacyFallback(localizedInteractVerb, language, manager, legacyInteractVerb, "Take");
        }

        private void InvalidateLocalizedCache()
        {
            _cachedLanguage = (GameLanguage)(-1);
            _cachedItemName = string.Empty;
            _cachedDescription = string.Empty;
            _cachedInteractVerb = string.Empty;
        }

        private static bool TryAppendSpan(ReadOnlySpan<char> source, Span<char> destination, ref int length)
        {
            if (source.Length == 0)
                return true;

            if (length < 0 || destination.Length - length < source.Length)
                return false;

            source.CopyTo(destination.Slice(length));
            length += source.Length;
            return true;
        }

        private static bool TryAppendChar(char value, Span<char> destination, ref int length)
        {
            if ((uint)length >= (uint)destination.Length)
                return false;

            destination[length++] = value;
            return true;
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ResolveLegacyFallback(
            LocalizedTextReference reference,
            GameLanguage language,
            ILocalizationTextReadModel manager,
            string legacyFallback,
            string hardFallback)
        {
            if (!string.IsNullOrWhiteSpace(legacyFallback))
                return legacyFallback;

            return hardFallback ?? string.Empty;
        }
    }
}
