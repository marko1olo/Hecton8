using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.Mathematics;
using Random = UnityEngine.Random;

/// <summary>
/// NASA-Punk HUD скафандра Hecton — Enterprise Edition.
///
/// АРХИТЕКТУРА:
/// • Никакого кода в Update — только обработчики событий HectonSurvivalSystem.
/// • Zero-Allocation: StringBuilder для всех строк, String.Format запрещён.
/// • Progress Bars: Image.fillAmount обновляется в каждом обработчике.
/// • Color Coding: normalColor → warningColor (< 30%) → criticalColor (< 15%).
/// • DOTween пульсация: при крите O₂ и Integrity бар + текст мигают.
/// • Digital Noise: корутина раз в 5-10 сек глитчует символы на 0.1 сек.
/// • Clean integer format: "O2 80 %", "DEPTH: 145 m", "ATM: 15 atm"
/// </summary>
[DisallowMultipleComponent]
public sealed class HectonSuitHUD : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — CORE
    // ══════════════════════════════════════════════════════════════════

    [Header("── Core ──────────────────────────────────────────────────")]
    [Tooltip("Ссылка на HectonSurvivalSystem игрока")]
    [SerializeField] private HectonSurvivalSystem survival;

    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — LIFE SUPPORT PANEL
    // ══════════════════════════════════════════════════════════════════

    [Header("Life Support Panel")]

    [Tooltip("TMP-лейбл кислорода, например: 'O2 75 %'")]
    [SerializeField] private TextMeshProUGUI oxygenLabel;

    [Tooltip("TMP-лейбл энергии, например: 'PWR 88 %'")]
    [SerializeField] private TextMeshProUGUI energyLabel;

    [Tooltip("TMP-лейбл прочности скафандра, например: 'HULL 100 %'")]
    [SerializeField] private TextMeshProUGUI integrityLabel;

    [Tooltip("Image (Filled) для полосы кислорода")]
    [SerializeField] private Image oxygenBar;

    [Tooltip("Image (Filled) для полосы энергии")]
    [SerializeField] private Image energyBar;

    [Tooltip("Image (Filled) для полосы прочности")]
    [SerializeField] private Image integrityBar;

    [Tooltip("Опциональный статус-лейбл: SYS NOMINAL / O2 CRITICAL / SIGNAL LOST")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — ENVIRONMENT PANEL
    // ══════════════════════════════════════════════════════════════════

    [Header("Environment Panel")]

    [Tooltip("TMP-лейбл глубины, например: 'DEPTH: 145 m'")]
    [SerializeField] private TextMeshProUGUI depthLabel;

    [Tooltip("TMP-лейбл давления, например: 'ATM: 15 atm'")]
    [SerializeField] private TextMeshProUGUI pressureLabel;

    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — COLOR CODING
    // ══════════════════════════════════════════════════════════════════

    [Header("── Color Coding ───────────────────────────────────────────")]

    [Tooltip("Нормальный цвет (> 30%)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color normalColor = new Color(0f, 0.898f, 1f, 1f);

    [Tooltip("Цвет предупреждения (< 30%)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color warningColor = new Color(1f, 0.878f, 0f, 1f);

    [Tooltip("Критический цвет (< 15%)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color criticalColor = new Color(1f, 0.384f, 0f, 1f);

    // Порог предупреждения и критического состояния (нормализованные, 0..1)
    private const float WarningThreshold  = 0.30f;
    private const float CriticalThreshold = 0.15f;

    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — CRITICAL ALERT OVERLAY
    // ══════════════════════════════════════════════════════════════════

    [Header("── Critical Alert ──────────────────────────────────────────")]

    [Tooltip("CanvasGroup полноэкранного оверлея, мигающего при крите")]
    [SerializeField] private CanvasGroup alertOverlay;

    [Tooltip("Период одного цикла пульсации (сек)")]
    [SerializeField] private float pulsePeriod = 1.2f;

    // ══════════════════════════════════════════════════════════════════
    // INSPECTOR — DIGITAL NOISE
    // ══════════════════════════════════════════════════════════════════

    [Header("── Digital Noise ───────────────────────────────────────────")]

    [SerializeField, Range(3f, 15f)]  private float noiseMinInterval = 5f;
    [SerializeField, Range(5f, 20f)]  private float noiseMaxInterval = 10f;
    [SerializeField, Range(0.05f, 0.3f)] private float noiseDuration = 0.1f;

    // ══════════════════════════════════════════════════════════════════
    // PRIVATE — STRINGBUILDERS (Zero-Allocation)
    // ══════════════════════════════════════════════════════════════════

    // sbLabel  — для построения строк в обработчиках событий
    // sbGlitch — только для корутины глитча, чтобы не конфликтовать
    private readonly StringBuilder sbLabel  = new StringBuilder(64);
    private readonly StringBuilder sbGlitch = new StringBuilder(64);

    // ══════════════════════════════════════════════════════════════════
    // PRIVATE — LABEL CACHE (для глитч-корутины)
    // ══════════════════════════════════════════════════════════════════

    // Индексы: 0-O₂  1-PWR  2-DEPTH  3-HULL  4-ATM
    private const int LabelCount = 5;
    private readonly string[]           cleanCache = new string[LabelCount];
    private          TextMeshProUGUI[]  labels;

    // ══════════════════════════════════════════════════════════════════
    // PRIVATE — ГЛИТЧ-ГЛИФЫ
    // ══════════════════════════════════════════════════════════════════

    private static readonly char[] Glyphs =
    {
        '&', '%', '$', '#', '@', '¥', '§', '†',
        '∆', '◊', '░', '▒', '█', '¿', '⌂', 'Ω'
    };

    // ══════════════════════════════════════════════════════════════════
    // PRIVATE — DOTween state
    // ══════════════════════════════════════════════════════════════════

    // Последовательности для O₂-крита
    private Sequence oxygenPulseSeq;
    private Sequence overlayPulseSeq;
    private bool     oxygenCriticalActive;

    // Последовательности для Integrity-крита
    private Sequence integrityPulseSeq;
    private bool     integrityCriticalActive;

    // Базовые цвета баров и текстов (восстанавливаем после крита)
    private Color oxygenBaseColor;
    private Color integrityBaseColor;

    // ══════════════════════════════════════════════════════════════════
    // PRIVATE — Coroutine handle
    // ══════════════════════════════════════════════════════════════════

    private Coroutine noiseHandle;

    // ══════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Собираем массив лейблов для глитч-корутины
        labels = new TextMeshProUGUI[LabelCount]
        {
            oxygenLabel, energyLabel, depthLabel, integrityLabel, pressureLabel
        };

        // Кэшируем исходные цвета, чтобы восстанавливать после DOTween
        if (oxygenLabel    != null) oxygenBaseColor    = oxygenLabel.color;
        if (integrityLabel != null) integrityBaseColor = integrityLabel.color;
    }

    private void OnEnable()
    {
        Subscribe();
        ForceRefreshAll();                          // мгновенно рисуем текущее состояние
        noiseHandle = StartCoroutine(NoiseLoop());  // запускаем глитч
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (noiseHandle != null)
        {
            StopCoroutine(noiseHandle);
            noiseHandle = null;
        }

        KillAllCritTweens();
    }

    private void OnDestroy() => KillAllCritTweens();

    // ══════════════════════════════════════════════════════════════════
    // EVENT WIRING
    // ══════════════════════════════════════════════════════════════════

    private void Subscribe()
    {
        if (survival == null) return;
        survival.OnOxygenChanged    += HandleOxygen;
        survival.OnEnergyChanged    += HandleEnergy;
        survival.OnDepthChanged     += HandleDepth;
        survival.OnIntegrityChanged += HandleIntegrity;
        survival.OnPressureChanged  += HandlePressure;
        survival.OnOxygenCritical   += HandleOxygenCritical;
        survival.OnDeath            += HandleDeath;
    }

    private void Unsubscribe()
    {
        if (survival == null) return;
        survival.OnOxygenChanged    -= HandleOxygen;
        survival.OnEnergyChanged    -= HandleEnergy;
        survival.OnDepthChanged     -= HandleDepth;
        survival.OnIntegrityChanged -= HandleIntegrity;
        survival.OnPressureChanged  -= HandlePressure;
        survival.OnOxygenCritical   -= HandleOxygenCritical;
        survival.OnDeath            -= HandleDeath;
    }

    // ══════════════════════════════════════════════════════════════════
    // EVENT HANDLERS — весь рендеринг только здесь, не в Update
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// O₂ изменился. Обновляем текст, бар и цвет.
    /// При выходе из крита — убиваем пульсацию.
    /// Output: "80 %" (no prefix — static label handles "O2")
    /// </summary>
    private void HandleOxygen(float value)
    {
        float normalized = value / survival.Stats.MaxOxygen;

        sbLabel.Clear();
        sbLabel.Append((int)(normalized * 100f));
        sbLabel.Append(" %");
        SetLabel(0, oxygenLabel, sbLabel.ToString());

        if (oxygenBar != null)
            oxygenBar.fillAmount = normalized;

        if (!oxygenCriticalActive)
            ApplyColor(oxygenLabel, oxygenBar, normalized);

        if (normalized >= CriticalThreshold && oxygenCriticalActive)
            KillOxygenCrit();
    }

    /// <summary>
    /// Энергия изменилась.
    /// Output: "88 %" (no prefix — static label handles "PWR")
    /// </summary>
    private void HandleEnergy(float value)
    {
        float normalized = value / survival.Stats.MaxEnergy;

        sbLabel.Clear();
        sbLabel.Append((int)(normalized * 100f));
        sbLabel.Append(" %");
        SetLabel(1, energyLabel, sbLabel.ToString());

        if (energyBar != null)
            energyBar.fillAmount = normalized;

        ApplyColor(energyLabel, energyBar, normalized);
    }

    /// <summary>
    /// Прочность изменилась.
    /// При крите (< 15%) — запускаем DOTween пульсацию.
    /// Output: "100 %" (no prefix — static label handles "HULL")
    /// </summary>
    private void HandleIntegrity(float value)
    {
        float normalized = value / survival.Stats.MaxIntegrity;

        sbLabel.Clear();
        sbLabel.Append((int)(normalized * 100f));
        sbLabel.Append(" %");
        SetLabel(3, integrityLabel, sbLabel.ToString());

        if (integrityBar != null)
            integrityBar.fillAmount = normalized;

        if (normalized < CriticalThreshold)
        {
            if (!integrityCriticalActive)
                StartIntegrityCritPulse();
        }
        else
        {
            if (integrityCriticalActive)
                KillIntegrityCrit();

            ApplyColor(integrityLabel, integrityBar, normalized);
        }
    }
    /// <summary>
    /// Глубина изменилась.
    /// Формат: "DEPTH: 145 m"
    /// </summary>
    private void HandleDepth(float value)
    {
        sbLabel.Clear();
        sbLabel.Append("DEPTH: ");
        sbLabel.Append((int)value);
        sbLabel.Append(" m");
        SetLabel(2, depthLabel, sbLabel.ToString());
    }

    /// <summary>
    /// Давление изменилось.
    /// Формат: "ATM: 15 atm"
    /// </summary>
    private void HandlePressure(float value)
    {
        sbLabel.Clear();
        sbLabel.Append("ATM: ");
        sbLabel.Append((int)value);
        sbLabel.Append(" atm");
        SetLabel(4, pressureLabel, sbLabel.ToString());
    }

    /// <summary>
    /// Кислород упал ниже 15 % — запускаем или поддерживаем критическую пульсацию.
    /// Вызывается из HectonSurvivalSystem каждый раз, когда O₂ в зоне крита.
    /// </summary>
    private void HandleOxygenCritical(float normalizedPct)
    {
        if (!oxygenCriticalActive)
            StartOxygenCritPulse();

        if (statusLabel != null)
            statusLabel.SetText(normalizedPct < 0.05f
                ? ">> LIFE SUPPORT FAILURE <<"
                : ">> O2 CRITICAL <<");
    }

    /// <summary>Игрок погиб — останавливаем всё.</summary>
    private void HandleDeath()
    {
        if (noiseHandle != null)
        {
            StopCoroutine(noiseHandle);
            noiseHandle = null;
        }
        KillAllCritTweens();

        if (statusLabel != null)
            statusLabel.SetText(">> SIGNAL LOST <<");
    }

    // ══════════════════════════════════════════════════════════════════
    // COLOR CODING
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Устанавливает цвет текста и бара по нормализованному значению (0..1).
    ///  > 30%  → normalColor
    /// 15-30%  → warningColor
    ///  < 15%  → criticalColor
    /// Не вызывать, когда активна DOTween-пульсация — она управляет цветом сама.
    /// </summary>
    private void ApplyColor(TextMeshProUGUI label, Image bar, float normalized)
    {
        Color target;
        if      (normalized > WarningThreshold)  target = normalColor;
        else if (normalized > CriticalThreshold) target = warningColor;
        else                                     target = criticalColor;

        if (label != null) label.color = target;
        if (bar   != null) bar.color   = target;
    }

    // ══════════════════════════════════════════════════════════════════
    // DOTWEEN — CRITICAL PULSE (O₂)
    // ══════════════════════════════════════════════════════════════════

    private void StartOxygenCritPulse()
    {
        oxygenCriticalActive = true;
        float half = pulsePeriod * 0.5f;

        if (oxygenBar != null) oxygenBar.color = criticalColor;

        oxygenPulseSeq?.Kill();
        if (oxygenLabel != null)
        {
            oxygenPulseSeq = DOTween.Sequence()
                .Append(oxygenLabel.DOColor(Color.black,    half).SetEase(Ease.InOutSine))
                .Append(oxygenLabel.DOColor(criticalColor,  half).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (oxygenBar != null)
        {
            DOTween.Kill(oxygenBar);
            oxygenBar
                .DOColor(Color.black,   half).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (alertOverlay != null)
        {
            alertOverlay.alpha = 0f;
            overlayPulseSeq?.Kill();
            overlayPulseSeq = DOTween.Sequence()
                .Append(alertOverlay.DOFade(0.35f, half).SetEase(Ease.InOutSine))
                .Append(alertOverlay.DOFade(0f,    half).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    private void KillOxygenCrit()
    {
        oxygenCriticalActive = false;

        oxygenPulseSeq?.Kill();
        oxygenPulseSeq = null;

        overlayPulseSeq?.Kill();
        overlayPulseSeq = null;

        if (oxygenLabel != null) oxygenLabel.color = oxygenBaseColor;
        if (oxygenBar   != null) { DOTween.Kill(oxygenBar); oxygenBar.color = oxygenBaseColor; }
        if (alertOverlay!= null) alertOverlay.alpha = 0f;

        if (statusLabel != null) statusLabel.SetText("SYS NOMINAL");
    }

    // ══════════════════════════════════════════════════════════════════
    // DOTWEEN — CRITICAL PULSE (Integrity)
    // ══════════════════════════════════════════════════════════════════

    private void StartIntegrityCritPulse()
    {
        integrityCriticalActive = true;
        float half = pulsePeriod * 0.5f;

        if (integrityBar != null) integrityBar.color = criticalColor;

        integrityPulseSeq?.Kill();
        if (integrityLabel != null)
        {
            integrityPulseSeq = DOTween.Sequence()
                .Append(integrityLabel.DOColor(Color.black,   half).SetEase(Ease.InOutSine))
                .Append(integrityLabel.DOColor(criticalColor, half).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (integrityBar != null)
        {
            DOTween.Kill(integrityBar);
            integrityBar
                .DOColor(Color.black,  half).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    private void KillIntegrityCrit()
    {
        integrityCriticalActive = false;

        integrityPulseSeq?.Kill();
        integrityPulseSeq = null;

        if (integrityLabel != null) integrityLabel.color = integrityBaseColor;
        if (integrityBar   != null) { DOTween.Kill(integrityBar); integrityBar.color = integrityBaseColor; }
    }

    private void KillAllCritTweens()
    {
        KillOxygenCrit();
        KillIntegrityCrit();
    }

    // ══════════════════════════════════════════════════════════════════
    // STRING HELPERS — Zero-Allocation label caching
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Записывает строку в лейбл и кэширует для глитч-корутины.
    /// Единственная аллокация (ToString) — только по событию, не каждый кадр.
    /// </summary>
    private void SetLabel(int cacheIdx, TextMeshProUGUI tmp, string text)
    {
        if (tmp == null) return;
        cleanCache[cacheIdx] = text;
        tmp.SetText(text);
    }

    /// <summary>
    /// Принудительно обновляет все лейблы/бары при включении HUD.
    /// Вызывается один раз в OnEnable.
    /// </summary>
    private void ForceRefreshAll()
    {
        if (survival == null) return;
        HandleOxygen(survival.Oxygen);
        HandleEnergy(survival.Energy);
        HandleDepth(survival.Depth);
        HandleIntegrity(survival.Integrity);
        HandlePressure(survival.Pressure);
        if (statusLabel != null) statusLabel.SetText("SYS NOMINAL");
    }

    // ══════════════════════════════════════════════════════════════════
    // DIGITAL NOISE — NASA-Punk Glitch Effect
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Корутина: каждые 5-10 сек глитчует 1-3 случайных лейбла
    /// на noiseDuration секунд, затем восстанавливает чистый кэш.
    /// </summary>
    private IEnumerator NoiseLoop()
    {
        var glitchPause = new WaitForSeconds(noiseDuration);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(noiseMinInterval, noiseMaxInterval));

            int count  = Random.Range(1, math.min(4, LabelCount + 1));
            int[] picks = new int[count];
            for (int i = 0; i < count; i++)
                picks[i] = Random.Range(0, LabelCount);

            for (int i = 0; i < picks.Length; i++)
            {
                int idx = picks[i];
                if (cleanCache[idx] == null || labels[idx] == null) continue;
                labels[idx].SetText(Corrupt(cleanCache[idx]));
            }

            yield return glitchPause;

            for (int i = 0; i < picks.Length; i++)
            {
                int idx = picks[i];
                if (cleanCache[idx] == null || labels[idx] == null) continue;
                labels[idx].SetText(cleanCache[idx]);
            }
        }
    }

    /// <summary>
    /// Заменяет ~33% символов строки случайными глифами.
    /// Использует sbGlitch, не sbLabel, чтобы не конфликтовать с обработчиками.
    /// </summary>
    private string Corrupt(string src)
    {
        sbGlitch.Clear();
        sbGlitch.Append(src);

        int hits = math.max(1, sbGlitch.Length / 3);
        for (int i = 0; i < hits; i++)
        {
            int pos = Random.Range(0, sbGlitch.Length);
            sbGlitch[pos] = Glyphs[Random.Range(0, Glyphs.Length)];
        }

        return sbGlitch.ToString();
    }
}