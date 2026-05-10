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
        // ─────────────────────── Identity ────────────────────────
        [Header("Identity")]
        [Tooltip("Nazvanie modulya dlya UI: 'Fundament', 'Koridor'")]
        public string moduleName = "Module";
        [Tooltip("Stable module ID used by saves, scanner archives, and future content packs. Leave empty to fall back to the asset name.")]
        [SerializeField] private string stableId = string.Empty;

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
        [Tooltip("Poluprozrachnyy prefab-prizrak (dolzhen imet PlacementGhost)")]
        public GameObject ghostPrefab;

        [Tooltip("Finalnyy prefab, ustanavlivaemyy v mir")]
        public GameObject finalPrefab;

        [Tooltip("Optional standardized habitat template that owns socket math, proxy bounds, integrity defaults, and stable hash IDs.")]
        [SerializeField] private BaseModuleTemplate moduleTemplate;

        // ─────────────────────── Cost ────────────────────────────
        [Header("Build Cost")]
        [Tooltip("Spisok resursov dlya postroyki")]
        public List<InventoryCost> buildCost = new List<InventoryCost>();

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

        /// <summary>Keshirovannaya stroka dlya UI.</summary>
        private string _cachedBuildText;

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
        /// Vozvraschaet keshirovannuyu stroku "Postroit {moduleName}".
        /// Zero allocation.
        /// </summary>
        public string GetBuildText()
        {
            if (string.IsNullOrEmpty(_cachedBuildText))
                RebuildCache();
            return _cachedBuildText;
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
        public string PersistentId => string.IsNullOrWhiteSpace(stableId) ? name : stableId;

        public BaseModuleTemplate ModuleTemplate => moduleTemplate;

        public int ModuleHashId
        {
            get
            {
                if (moduleTemplate != null)
                    return moduleTemplate.TemplateHashId;

                return Hecton.Localization.LocHash.Compute(PersistentId);
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
            if (blueprintQuestFlagId == 0u)
                return true;

            IQuestSystem questSystem = GlobalRegistry.QuestSystem;
            return questSystem != null && questSystem.GetFlag(blueprintQuestFlagId);
        }

        /// <summary>
        /// Returns true when the supplied ID matches the authored stable ID or the legacy asset name.
        /// </summary>
        public bool MatchesPersistentId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            string persistentId = PersistentId;
            if (string.Equals(persistentId, id, StringComparison.Ordinal))
                return true;

            return !string.Equals(name, persistentId, StringComparison.Ordinal) &&
                   string.Equals(name, id, StringComparison.Ordinal);
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
            _cachedBuildText = $"Postroit {moduleName}";
        }

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>
        /// Keshirovannyy ToUpperInvariant dlya izbezhaniya povtornyh allokatsiy strok.
        /// Hranit do 16 poslednih preobrazovaniy dlya povtornogo ispolzovaniya.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Prostoy hash dlya keshirovaniya (ne kriptograficheskiy)
            int hash = input.GetHashCode() & 0xF; // Maska dlya indeksa 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Sozdaem novuyu stroku i keshiruem
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }
    }
}
