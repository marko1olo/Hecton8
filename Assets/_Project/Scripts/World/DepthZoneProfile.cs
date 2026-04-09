// ============================================================================
// HECTON-8 — DepthZoneProfile.cs
// ScriptableObject: профиль зоны по глубине.
//
// ЛОР (лор2 Раздел 5 — Вертикальная стратификация):
//   Зона 1: THE SPINE / SHALLOW GRAVE     0 → -100м
//   Зона 2: THE DROWNED FACTORIES         -100 → -1500м
//   Зона 3: THE DROP / ABYSSAL FACE       -1000 → -5000м
//   Зона 4: THE WOUND / THE HIVE          любая (пещеры)
//
// Подзоны (лор2 Раздел 6 — Двухслойная геология):
//   1.1: Подводные вершины                0 → -150м
//   1.2: Горные склоны                    -150 → -500м
//   1.3: Предгорья и долины               -500 → -1000м
//   Граница: Древняя береговая линия      -1000 → -1200м
//   2.1: Верхняя бездна                   -1200 → -2500м
//   2.2: Глубокое дно                     -2500 → -4000м
//   2.3: Разломы и термальные поля        -4000 → -5000м+
// ============================================================================

using UnityEngine;

namespace Hecton8.World
{
    [System.Serializable]
    public struct DepthZoneAmbience
    {
        [Tooltip("Цвет воды (fog tint).")]
        public Color waterColor;

        [Tooltip("Плотность тумана.")]
        [Range(0f, 1f)] public float fogDensity;

        [Tooltip("Интенсивность биолюминесценции [0..1].")]
        [Range(0f, 1f)] public float biolumIntensity;

        [Tooltip("Множитель громкости эмбиента.")]
        [Range(0f, 2f)] public float ambientVolumeMultiplier;

        [Tooltip("Базовая температура воды (°C).")]
        public float waterTemperature;
    }

    [CreateAssetMenu(
        fileName = "DepthZone_",
        menuName  = "Hecton8/World/Depth Zone Profile",
        order     = 40)]
    public sealed class DepthZoneProfile : ScriptableObject
    {
        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID зоны.")]
        [SerializeField] public string zoneId;

        [Tooltip("Отображаемое название (лор).")]
        [SerializeField] public string displayName = "НЕИЗВЕСТНАЯ ЗОНА";

        [Tooltip("Описание для PDA.")]
        [SerializeField, TextArea(2, 4)] public string description;

        [Header("── Depth Range ─────────────────────────────")]
        [Tooltip("Минимальная глубина зоны (метры, положительное = вниз).")]
        [SerializeField] public float minDepth;

        [Tooltip("Максимальная глубина зоны (метры).")]
        [SerializeField] public float maxDepth;

        [Header("── Ambience ────────────────────────────────")]
        [SerializeField] public DepthZoneAmbience ambience = new DepthZoneAmbience
        {
            waterColor = new Color(0.05f, 0.15f, 0.25f, 1f),
            fogDensity = 0.3f,
            biolumIntensity = 0.1f,
            ambientVolumeMultiplier = 1f,
            waterTemperature = 15f
        };

        [Header("── Gameplay ────────────────────────────────")]
        [Tooltip("Требуемый тир корпуса скафандра для безопасного погружения.")]
        [SerializeField, Range(0, 4)] public int requiredHullTier;

        [Tooltip("Опасность зоны [0..1] — влияет на Director AI tension.")]
        [SerializeField, Range(0f, 1f)] public float dangerLevel;

        [Tooltip("Зона содержит пещеры (THE WOUND).")]
        [SerializeField] public bool hasCaves;

        [Tooltip("Зона термальная (разломы, курильщики).")]
        [SerializeField] public bool isThermal;

        [Header("── Discovery ───────────────────────────────")]
        [Tooltip("discoveryId для регистрации в NarrativeDirector при первом входе.")]
        [SerializeField] public string discoveryId;

        // Pre-cached HUD string — zero GC in SlowTick
        [System.NonSerialized] public string cachedHudLabel;

        private void OnEnable()
        {
            RebuildCache();
        }

        private void RebuildCache()
        {
            cachedHudLabel = string.IsNullOrEmpty(displayName)
                ? "НЕИЗВЕСТНАЯ ЗОНА"
                : "ЗОНА: " + displayName.ToUpperInvariant();
        }

        /// <summary>Проверить, находится ли глубина в этой зоне.</summary>
        public bool ContainsDepth(float depth) => depth >= minDepth && depth < maxDepth;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(zoneId))
                zoneId = name.ToLower().Replace(" ", "_");
            if (maxDepth <= minDepth)
                maxDepth = minDepth + 100f;
        }
#endif
    }
}
