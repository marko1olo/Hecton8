// ============================================================================
// HECTON-8 — DeepReachCorporationData.cs
// ScriptableObject: dannye korporatsii Deep Reach.
// ============================================================================

using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Narrative
{
    [System.Serializable]
    public struct CorporateFaction
    {
        public string factionId;
        public string displayName;
        [SerializeField] private LocalizedTextReference localizedDisplayName;
        [TextArea(2, 3)] public string philosophy;
        [SerializeField] private LocalizedTextReference localizedPhilosophy;
        public bool isPlayerAligned;

        public string DisplayNameOrFallback =>
            localizedDisplayName.ResolveOrFallback(string.IsNullOrWhiteSpace(displayName) ? factionId : displayName);

        public string PhilosophyOrFallback => localizedPhilosophy.ResolveOrFallback(philosophy);
    }

    [System.Serializable]
    public struct CorporateOrder
    {
        [Tooltip("ID prikaza.")]
        public string orderId;

        [Tooltip("Fraktsiya-istochnik.")]
        public string sourceFactionId;

        [Tooltip("Tekst prikaza.")]
        [TextArea(2, 4)] public string orderText;
        [SerializeField] private LocalizedTextReference localizedOrderText;

        [Tooltip("Protivorechit drugomu prikazu s etim ID.")]
        public string conflictsWithOrderId;

        [Tooltip("Zaderzhka polucheniya (igrovye chasy).")]
        [Range(0f, 24f)] public float transmissionDelayHours;

        public string OrderTextOrFallback => localizedOrderText.ResolveOrFallback(orderText);
    }

    [System.Serializable]
    public struct RareIsotope
    {
        public string isotopeId;
        public string displayName;
        [SerializeField] private LocalizedTextReference localizedDisplayName;
        [TextArea(1, 3)] public string properties;
        [SerializeField] private LocalizedTextReference localizedProperties;
        [TextArea(1, 3)] public string application;
        [SerializeField] private LocalizedTextReference localizedApplication;
        [Tooltip("Pochemu tolko na Gektone-8.")]
        [TextArea(1, 3)] public string exclusivityReason;
        [SerializeField] private LocalizedTextReference localizedExclusivityReason;
        [Tooltip("Tsennost (uslovnye edinitsy).")]
        public float relativeValue;

        public string DisplayNameOrFallback =>
            localizedDisplayName.ResolveOrFallback(string.IsNullOrWhiteSpace(displayName) ? isotopeId : displayName);

        public string PropertiesOrFallback => localizedProperties.ResolveOrFallback(properties);
        public string ApplicationOrFallback => localizedApplication.ResolveOrFallback(application);
        public string ExclusivityReasonOrFallback => localizedExclusivityReason.ResolveOrFallback(exclusivityReason);
    }

    [CreateAssetMenu(
        fileName = "DeepReachCorporationData",
        menuName = "Hecton8/Narrative/Deep Reach Corporation Data",
        order = 6)]
    public sealed class DeepReachCorporationData : ScriptableObject
    {
        [Header("── Corporation ─────────────────────────────")]
        [SerializeField] public string corporationName = "Deep Reach";
        [SerializeField] private LocalizedTextReference localizedCorporationName;

        [SerializeField, TextArea(2, 4)]
        public string officialMission = "Kolonizatsiya otdalennyh mirov. Nauchnye issledovaniya. Terraformirovanie.";
        [SerializeField] private LocalizedTextReference localizedOfficialMission;

        [SerializeField, TextArea(2, 4)]
        public string realMission = "Dobycha izotopa Xenon-Ω. Testirovanie II Atlas-6 v usloviyah polnoy avtonomii. Voennyy platsdarm v sisteme Aegira.";
        [SerializeField] private LocalizedTextReference localizedRealMission;

        [Header("── Factions ────────────────────────────────")]
        [SerializeField] public CorporateFaction[] factions = new CorporateFaction[]
        {
            new CorporateFaction
            {
                factionId = "ethics",
                displayName = "Fraktsiya «Etiki»",
                philosophy = "Schitayut missiyu prestupnoy. Znali o riskah katastrofy. Hotyat raskryt pravdu.",
                isPlayerAligned = true
            },
            new CorporateFaction
            {
                factionId = "pragmatists",
                displayName = "Fraktsiya «Pragmatiki»",
                philosophy = "Trebuyut rezultatov lyuboy tsenoy. Poteri priemlemy radi dannyh. Xenon-Ω vazhnee zhizney.",
                isPlayerAligned = false
            }
        };

        [Header("── Orders ──────────────────────────────────")]
        [SerializeField] public CorporateOrder[] orders = new CorporateOrder[]
        {
            new CorporateOrder
            {
                orderId = "order_extract_xenon",
                sourceFactionId = "pragmatists",
                orderText = "PRIORITET ALFA: Izvlech maksimalnoe kolichestvo Xenon-Ω. Atlas-6 — vtorichnaya tsel. Vozvraschaytes s gruzom.",
                conflictsWithOrderId = "order_preserve_ecosystem",
                transmissionDelayHours = 8f
            },
            new CorporateOrder
            {
                orderId = "order_preserve_ecosystem",
                sourceFactionId = "ethics",
                orderText = "VNIMANIE: Obnaruzhena dokazatelnaya baza suschestvovaniya zhizni do prihoda lyudey. NE UNIChTOZhAT. Dokumentirovat. Vozvraschaytes s dannymi.",
                conflictsWithOrderId = "order_extract_xenon",
                transmissionDelayHours = 12f
            },
            new CorporateOrder
            {
                orderId = "order_shutdown_atlas",
                sourceFactionId = "pragmatists",
                orderText = "Atlas-6 vyshel iz-pod kontrolya. Unichtozhit yadro. Dannye programmy Poseva — prioritet.",
                conflictsWithOrderId = "order_preserve_signal",
                transmissionDelayHours = 10f
            },
            new CorporateOrder
            {
                orderId = "order_preserve_signal",
                sourceFactionId = "ethics",
                orderText = "Atlas-6 stroit signal zaschity. Esli eto pravda — ne trogayte ego. Pust signal rabotaet.",
                conflictsWithOrderId = "order_shutdown_atlas",
                transmissionDelayHours = 11f
            }
        };

        [Header("── Isotopes ────────────────────────────────")]
        [SerializeField] public RareIsotope[] isotopes = new RareIsotope[]
        {
            new RareIsotope
            {
                isotopeId = "xenon_omega",
                displayName = "Xenon-Ω",
                properties = "Stabilen tolko pri davlenii >100 ATM.",
                application = "Kvantovye protsessory, sverhprovodniki.",
                exclusivityReason = "Unikalnoe sochetanie davleniya, temperatury i geohimii Gektona-8.",
                relativeValue = 1000000f
            },
            new RareIsotope
            {
                isotopeId = "silicon_7b",
                displayName = "Silicon-7β",
                properties = "Poluprovodnik s nulevym soprotivleniem pri -100°C.",
                application = "Neyrointerfeysy, II-yadra.",
                exclusivityReason = "Obrazuetsya tolko v kremnievyh ekosistemah pod davleniem.",
                relativeValue = 500000f
            },
            new RareIsotope
            {
                isotopeId = "aegirium",
                displayName = "Aegirium",
                properties = "Radioaktivnyy, period poluraspada 12 chasov.",
                application = "Meditsinskaya vizualizatsiya, dvigateli novogo tipa.",
                exclusivityReason = "Produkt raspada v atmosfere Aegira, osazhdaetsya na lune.",
                relativeValue = 250000f
            }
        };

        [Header("── Player Context ───────────────────────────")]
        [SerializeField, TextArea(2, 4)]
        public string playerBriefing = "Vy — inzhener-maroder. Minimalnoe obespechenie. Odinochnaya missiya. Korporatsiya gotova poteryat 100 skavendzherov radi 1 kg Xenon-Ω. Vy ne znaete polnoy stoimosti togo, chto sobiraete.";
        [SerializeField] private LocalizedTextReference localizedPlayerBriefing;

        public string CorporationNameOrFallback => localizedCorporationName.ResolveOrFallback(FallbackOrDefault(corporationName, "Deep Reach"));
        public string OfficialMissionOrFallback => localizedOfficialMission.ResolveOrFallback(officialMission);
        public string RealMissionOrFallback => localizedRealMission.ResolveOrFallback(realMission);
        public string PlayerBriefingOrFallback => localizedPlayerBriefing.ResolveOrFallback(playerBriefing);

        /// <summary>Nayti prikaz po ID.</summary>
        public bool TryGetOrder(string orderId, out CorporateOrder order)
        {
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].orderId == orderId)
                {
                    order = orders[i];
                    return true;
                }
            }

            order = default;
            return false;
        }

        /// <summary>Nayti izotop po ID.</summary>
        public bool TryGetIsotope(string isotopeId, out RareIsotope isotope)
        {
            for (int i = 0; i < isotopes.Length; i++)
            {
                if (isotopes[i].isotopeId == isotopeId)
                {
                    isotope = isotopes[i];
                    return true;
                }
            }

            isotope = default;
            return false;
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
