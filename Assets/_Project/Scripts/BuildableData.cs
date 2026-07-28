// ============================================================================
// HECTON-8 — BuildableData.cs
// Dannye stroitelnogo modulya podvodnoy bazy.
//
// REFAKTORING v2 — ENERGOSISTEMA:
//   • Dobavleny polya powerRating i powerPriority.
//   • PowerNode chitaet eti dannye pri spavne modulya.
//   • Data-Driven: potreblenie/generatsiya nastraivaetsya v assete.
//
// ScriptableObject — odin asset na tip modulya.
// Sozdaetsya cherez: Hecton → Buildable Module.
// ===========================================================================

using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Building
{
    public enum BuildableFamily
    {
        Structure = 0,
        Habitat = 1,
        Utility = 2,
        Fabrication = 3,
        Logistics = 4,
        Defense = 5
    }

    // ══════════════════════════════════════════════════════════════════
    //  InventoryCost — stoimost odnogo resursa
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Odna pozitsiya v spiske stoimosti postroyki.
    /// </summary>
    [Serializable]
    public sealed class InventoryCost
    {
        [Tooltip("Resurs (ScriptableObject ItemData)")]
        public ItemData item;

        [Tooltip("Kolichestvo edinits etogo resursa")]
        [Min(1)]
        public int amount = 1;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuildableData — dannye stroitelnogo modulya
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Chistye dannye odnogo stroitelnogo modulya.
    /// Nikakoy logiki — tolko opisanie.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewModule",
        menuName = "Hecton/Buildable Module",
        order    = 10)]
    public sealed class BuildableData : ScriptableObject
    {
        private static IQuestSystem s_blueprintQuestSystem;

        // ─────────────────────── Identity ────────────────────────
        [Header("Identity")]
        [Tooltip("Nazvanie modulya dlya UI: 'Fundament', 'Koridor'")]
        public string moduleName = "Module";
        [Tooltip("Stable module ID used by saves, scanner archives, and future content packs. Leave empty to fall back to the asset name.")]
        [SerializeField] private string stableId = string.Empty;
        private int _persistentHashId;

        [Tooltip("Ikonka dlya menyu stroitelstva (optsionalno)")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("Opisanie modulya dlya podskazki")]
        public string description = "";

        [Tooltip("Semeystvo modulya dlya browser/filter/directive logic.")]
        public BuildableFamily family = BuildableFamily.Structure;

        [Tooltip("Packed QuestState flag required before this blueprint is visible. 0 = visible by default.")]
        [SerializeField] private uint blueprintQuestFlagId;

        // ─────────────────────── Prefabs ─────────────────────────
        [Header("Prefabs")]
        [Tooltip("Legacy preview prefab reference retained for old assets. Runtime builder holography ignores this field.")]
        public GameObject ghostPrefab;

        [Tooltip("Finalnyy prefab, ustanavlivaemyy v mir")]
        public GameObject finalPrefab;

        [Tooltip("Optional standardized habitat template that owns socket math, proxy bounds, integrity defaults, and stable hash IDs.")]
        [SerializeField] private BaseModuleTemplate moduleTemplate;

        // ─────────────────────── Cost ────────────────────────────
        [Header("Build Cost")]
        [Tooltip("Spisok resursov dlya postroyki")]
        public List<InventoryCost> buildCost = new List<InventoryCost>(4);

        // ─────────────────────── Power ───────────────────────────
        [Header("Power")]
        [Tooltip("Energeticheskiy reyting modulya (Vatty).\n" +
                 "• Polozhitelnoe = generatsiya (solnechnaya panel: +200)\n" +
                 "• Otritsatelnoe = potreblenie (zhilaya komnata: -30)\n" +
                 "• Nol = passivnyy (koridor, stena)\n\n" +
                 "Eto BAZOVOE potreblenie modulya.\n" +
                 "Dopolnitelnye potrebiteli (Fabricator)\n" +
                 "dobavlyayut svoe cherez IPowerComponent.")]
        public float powerRating;

        [Tooltip("Prioritet otklyucheniya pri defitsite energii.\n" +
                 "0 = kriticheskiy (zhizneobespechenie)\n" +
                 "50 = obychnyy\n" +
                 "100 = roskosh (dekor)")]
        [Range(0, 100)]
        public int powerPriority = 50;

        // ─────────────────────── Cache ───────────────────────────

        private static readonly char[] BuildPrefix = { 'P', 'o', 's', 't', 'r', 'o', 'i', 't', ' ' };

        // ═════════════════════════════════════════════════════════
        //  ScriptableObject Lifecycle
        // ═════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            RebuildCache();
        }
#endif

        // ═════════════════════════════════════════════════════════
        //  Public API
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Legacy string route for cold compatibility. Runtime UI should use <see cref="TryWriteBuildText"/>.
        /// </summary>
        public string GetBuildText()
        {
            return string.IsNullOrWhiteSpace(moduleName) ? "Build" : moduleName;
        }

        /// <summary>
        /// Writes the construction prompt into a caller-owned buffer.
        /// </summary>
        /// <param name="destination">Destination buffer for the visible prompt.</param>
        /// <param name="length">Number of characters written.</param>
        /// <returns>True when the prompt fits in the provided buffer.</returns>
        public bool TryWriteBuildText(Span<char> destination, out int length)
        {
            length = 0;
            if (!TryAppend(BuildPrefix, destination, ref length))
                return false;

            ReadOnlySpan<char> nameSpan = string.IsNullOrWhiteSpace(moduleName)
                ? "Module".AsSpan()
                : moduleName.AsSpan();
            return TryAppend(nameSpan, destination, ref length) && length > BuildPrefix.Length;
        }

        /// <summary>
        /// Summarnoe kolichestvo resursnyh edinits dlya postroyki.
        /// </summary>
        public int TotalResourceCount
        {
            get
            {
                int total = 0;
                for (int i = 0, count = buildCost.Count; i < count; i++)
                    total += buildCost[i].amount;
                return total;
            }
        }

        /// <summary>
        /// true esli modul generiruet energiyu (powerRating > 0).
        /// Udobno dlya UI-filtratsii.
        /// </summary>
        public bool IsGenerator => powerRating > 0f;

        /// <summary>
        /// true esli modul potreblyaet energiyu (powerRating &lt; 0).
        /// </summary>
        public bool IsConsumer => powerRating < 0f;

        public bool IsPassive => Mathf.Approximately(powerRating, 0f);

        /// <summary>
        /// Stable content identifier used by persistence-facing systems.
        /// </summary>
        public string PersistentId => ResolveCanonicalPersistentId(stableId, name);

        public BaseModuleTemplate ModuleTemplate => moduleTemplate;

        public int ModuleHashId
        {
            get
            {
                // ResolvePersistentHashId(), never the serialized templateHashId field: that field is
                // baked at import and is 0 on a template that never ran OnValidate, so reading it
                // directly hands out a second, different identity for the same module.
                if (moduleTemplate != null)
                    return moduleTemplate.ResolvePersistentHashId();

                return _persistentHashId;
            }
        }

        public string FamilyLabel => ResolveFamilyLabel(family);

        /// <summary>
        /// Packed QuestState flag required before this construction blueprint is visible.
        /// </summary>
        public uint BlueprintQuestFlagId => blueprintQuestFlagId;

        /// <summary>
        /// True when this blueprint depends on a QuestDAG flag.
        /// </summary>
        public bool RequiresBlueprintQuestFlag => blueprintQuestFlagId != 0u;

        /// <summary>
        /// Returns true when this blueprint is allowed to appear in builder-facing catalogs.
        /// </summary>
        public bool IsBlueprintViewable()
        {
            return IsBlueprintViewable(s_blueprintQuestSystem);
        }

        internal static void ConfigureBlueprintQuestSystem(IQuestSystem questSystem)
        {
            s_blueprintQuestSystem = questSystem;
        }

        /// <summary>
        /// Returns true when this blueprint is visible through an already-cached quest owner.
        /// </summary>
        public bool IsBlueprintViewable(IQuestSystem questSystem)
        {
            if (blueprintQuestFlagId == 0u)
                return true;

            return questSystem != null && questSystem.GetFlag(blueprintQuestFlagId);
        }

        /// <summary>
        /// Returns true when the supplied ID matches the authored stable ID or the legacy asset name.
        /// </summary>
        public bool MatchesPersistentId(string id)
        {
            // IsNullOrWhiteSpace, not IsNullOrEmpty: a whitespace-only id is not empty, so the old
            // guard let "   " through as a real lookup key and a whitespace-named module would answer
            // to it, restoring an arbitrary blueprint for a blank persisted prefabId.
            if (string.IsNullOrWhiteSpace(id))
                return false;

            id = id.Trim();
            string persistentId = PersistentId;
            if (string.Equals(persistentId, id, StringComparison.Ordinal))
                return true;

            string legacyName = ResolveCanonicalPersistentId(name, null);
            return legacyName.Length != 0 &&
                   !string.Equals(legacyName, persistentId, StringComparison.Ordinal) &&
                   string.Equals(legacyName, id, StringComparison.Ordinal);
        }

        public string FamilyShortCode
        {
            get
            {
                switch (family)
                {
                    case BuildableFamily.Structure: return "STR";
                    case BuildableFamily.Habitat: return "HAB";
                    case BuildableFamily.Utility: return "UTL";
                    case BuildableFamily.Fabrication: return "FAB";
                    case BuildableFamily.Logistics: return "LOG";
                    case BuildableFamily.Defense: return "DEF";
                    default: return "UNK";
                }
            }
        }

        private static string ResolveFamilyLabel(BuildableFamily value)
        {
            switch (value)
            {
                case BuildableFamily.Structure: return "STRUCTURE";
                case BuildableFamily.Habitat: return "HABITAT";
                case BuildableFamily.Utility: return "UTILITY";
                case BuildableFamily.Fabrication: return "FABRICATION";
                case BuildableFamily.Logistics: return "LOGISTICS";
                case BuildableFamily.Defense: return "DEFENSE";
                default: return "UNKNOWN";
            }
        }

        // ═════════════════════════════════════════════════════════
        //  Private
        // ═════════════════════════════════════════════════════════

        private void RebuildCache()
        {
            _persistentHashId = ComputeCanonicalPersistentHashId(PersistentId);
        }

        /// <summary>
        /// Canonical form of an authored stable id.
        /// This must stay behaviourally identical to <c>SaveData.SanitizePersistenceString</c>
        /// (SaveData.cs:99-102), which the save layer applies to every persisted module id through
        /// <c>ModuleDTO.SanitizePersistenceId</c>, and which <c>ModuleCatalog.FindDataById</c>
        /// (ModuleCatalog.cs:93-102) applies to the id it is handed before the dictionary probe. If
        /// this form diverges, a module authored with a padded stable id is persisted under its
        /// trimmed id and looked up under the padded one, so it resolves to no BuildableData on load.
        /// A blank or whitespace-only id resolves to <see cref="string.Empty"/> and is refused rather
        /// than hashed, because a real hash over a blank id is one identity every blank module shares.
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

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static bool TryAppend(ReadOnlySpan<char> source, Span<char> destination, ref int length)
        {
            if (destination.Length - length < source.Length)
                return false;

            source.CopyTo(destination.Slice(length));
            length += source.Length;
            return true;
        }
    }
}
