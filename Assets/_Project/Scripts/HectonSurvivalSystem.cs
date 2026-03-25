using System;
using Hecton8.Core;
using Hecton8.SaveSystem;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Atmosphere;

/// <summary>
/// Core survival simulation for the Hecton diving suit.
/// Attach to the player GameObject and assign a SurvivalStats asset.
///
/// РЕФАКТОРИНГ v2 — GameTickManager:
///   • ITickable   → Tick(dt):   depth/pressure + event publishing (каждый кадр)
///   • ISlowTickable → SlowTick(): O₂/energy drain + pressure damage (2 раза/сек)
///   • НЕТ Update() — вся логика через централизованный тик-менеджер.
///
/// РЕФАКТОРИНГ v3 — ISaveable:
///   • SavePriority/LoadPriority = 10 (игрок загружается первым).
///   • PopulateSaveData: записывает статы + позицию.
///   • LoadFromSaveData: восстанавливает статы + позицию, force-dirty events.
///
/// РЕФАКТОРИНГ v4 — TakeDamage:
///   • Публичный метод TakeDamage(float) для нанесения урона извне
///     (существа, ловушки, среда).
///   • Уменьшает integrity, публикует событие, проверяет смерть.
///
/// Обоснование разделения:
///   • Depth/Pressure зависят от transform.position → нужны каждый кадр
///     для плавной шкалы глубины в UI.
///   • O₂/Energy drain — линейные процессы, 2fps неотличимо от 60fps
///     при правильном dt (slowTickInterval).
///   • CheckLethalConditions — в Tick, чтобы смерть наступала мгновенно.
///
/// Performance guarantees:
/// ─ Zero GC allocations in Tick/SlowTick.
/// ─ Events throttled: fire only when |current − lastPublished| > ε.
/// ─ All math uses Unity.Mathematics.
/// </summary>
[DisallowMultipleComponent]
public sealed class HectonSurvivalSystem : MonoBehaviour, ITickable, ISlowTickable, ISaveable
{
    // ─── Inspector ───────────────────────────────────────────

    [Header("Data")]
    [Tooltip("Drag a SurvivalStats .asset here to configure all suit parameters.")]
    [SerializeField] private SurvivalStats stats;

    [Header("Scene")]
    [Tooltip("World-space Y coordinate of the water surface.")]
    [SerializeField] private float surfaceWorldY;

    // ─── SlowTick Configuration ──────────────────────────────

    /// <summary>
    /// Интервал SlowTick в секундах. Используется как deltaTime
    /// для расчётов drain/damage в SlowTick().
    /// Синхронизируется с GameTickManager в OnEnable.
    /// Fallback: 0.5f (2 тика в секунду).
    /// </summary>
    private float _slowTickDt = 0.5f;

    // ─── Runtime state (stack-allocated, no GC) ──────────────

    private float oxygen;
    private float energy;
    private float depth;
    private float integrity;
    private float pressure;
    private float weight;

    private bool  alive = true;

    // ─── Throttling: last published values ───────────────────

    private float lastPubOxygen;
    private float lastPubEnergy;
    private float lastPubDepth;
    private float lastPubIntegrity;
    private float lastPubPressure;

    private const float Epsilon       = 0.1f;
    private const float DirtySentinel = -9999f;

    // ─── Public events ───────────────────────────────────────

    /// <summary>Current absolute O₂ level (0 … MaxOxygen).</summary>
    public event Action<float> OnOxygenChanged;

    /// <summary>Current absolute energy level (0 … MaxEnergy).</summary>
    public event Action<float> OnEnergyChanged;

    /// <summary>Current depth in metres (≥ 0).</summary>
    public event Action<float> OnDepthChanged;

    /// <summary>Current hull integrity (0 … MaxIntegrity).</summary>
    public event Action<float> OnIntegrityChanged;

    /// <summary>Current pressure in ATM (≥ 1).</summary>
    public event Action<float> OnPressureChanged;

    /// <summary>Equipment weight in kg.</summary>
    public event Action<float> OnWeightChanged;

    /// <summary>
    /// Fires when O₂ drops below 15 %.
    /// Parameter: normalised value (0 … 1).
    /// </summary>
    public event Action<float> OnOxygenCritical;

    /// <summary>
    /// Player has died (O₂ = 0 or Integrity = 0).
    /// </summary>
    public event Action OnDeath;
    /// <summary>Current depth in metres below water surface.</summary>
    public float CurrentDepth => depth;
    // ─── Public read-only properties ─────────────────────────

    public float Oxygen              => oxygen;
    public float Energy              => energy;
    public float Depth               => depth;
    public float Integrity           => integrity;
    public float Pressure            => pressure;
    public float Weight              => weight;
    public bool  IsAlive             => alive;
    public SurvivalStats Stats       => stats;

    public float OxygenNormalized    => oxygen    / stats.MaxOxygen;
    public float EnergyNormalized    => energy    / stats.MaxEnergy;
    public float IntegrityNormalized => integrity / stats.MaxIntegrity;

    // v5.0 ENTERPRISE: Percent properties for UI/HUD (0-100 range)
    public float EnergyPercent       => EnergyNormalized * 100f;

    // ═════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═════════════════════════════════════════════════════════

    private void Awake()
    {
        if (stats == null)
        {
            Debug.LogError(
                $"[HectonSurvival] SurvivalStats asset is not assigned on \"{name}\". " +
                "Disabling component.");
            enabled = false;
            return;
        }

        oxygen    = stats.MaxOxygen;
        energy    = stats.MaxEnergy;
        integrity = stats.MaxIntegrity;
        depth     = 0f;
        pressure  = 1f;
        weight    = 0f;
        alive     = true;

        lastPubOxygen    = DirtySentinel;
        lastPubEnergy    = DirtySentinel;
        lastPubDepth     = DirtySentinel;
        lastPubIntegrity = DirtySentinel;
        lastPubPressure  = DirtySentinel;
    }

    private void OnEnable()
    {
        // ── Регистрация в GameTickManager ──
        GameTickManager instance = GameTickManager.Instance;
        if (instance != null)
        {
            instance.RegisterAll(this);
            _slowTickDt = 0.5f;
        }

        // ── Регистрация в SaveManager ──
        SaveManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        // ── Отписка от GameTickManager ──
        GameTickManager.Instance?.UnregisterAll(this);

        // ── Отписка от SaveManager ──
        SaveManager.Instance?.Unregister(this);
    }

    // ═════════════════════════════════════════════════════════
    //  ITickable — КАЖДЫЙ КАДР (замена Update)
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается GameTickManager каждый кадр.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!alive) return;

        ComputeDepthAndPressure();
        PublishDirty();
        CheckLethalConditions();
    }

    // ═════════════════════════════════════════════════════════
    //  ISlowTickable — 2 РАЗА В СЕКУНДУ
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается GameTickManager с частотой slowTickInterval (≈0.5 сек).
    /// </summary>
    public void SlowTick()
    {
        if (!alive) return;

        float dt = _slowTickDt;

        DrainOxygen(dt);
        DrainEnergy(dt);
        ApplyPressureDamage(dt);
    }

    // ═════════════════════════════════════════════════════════
    //  ISaveable — SAVE / LOAD
    // ═════════════════════════════════════════════════════════

    /// <summary>Player stats загружаются первыми.</summary>
    public int SavePriority => 10;
    public int LoadPriority => 10;

    /// <summary>
    /// Записывает текущие статы и позицию в SaveData.
    /// </summary>
    public void PopulateSaveData(SaveData data)
    {
        ref PlayerStatsDTO dto = ref data.playerStats;

        dto.oxygen    = oxygen;
        dto.energy    = energy;
        dto.integrity = integrity;
        dto.weight    = weight;

        dto.SetPosition(transform.position);
        dto.SetRotation(transform.rotation);
    }

    /// <summary>
    /// Восстанавливает статы и позицию из SaveData.
    /// </summary>
    public void LoadFromSaveData(SaveData data)
    {
        PlayerStatsDTO dto = data.playerStats;

        // ── Восстановление статов с клампингом ──
        oxygen    = Mathf.Clamp(dto.oxygen,    0f, stats.MaxOxygen);
        energy    = Mathf.Clamp(dto.energy,    0f, stats.MaxEnergy);
        integrity = Mathf.Clamp(dto.integrity, 0f, stats.MaxIntegrity);
        weight    = Mathf.Max(0f, dto.weight);
        alive     = oxygen > 0f && integrity > 0f;

        // ── Восстановление позиции ──
        Vector3 loadedPos = dto.GetPosition();
        Quaternion loadedRot = dto.GetRotation();

        // Валидация: не NaN, не бесконечность
        if (!float.IsNaN(loadedPos.x) && !float.IsInfinity(loadedPos.x) &&
            !float.IsNaN(loadedPos.y) && !float.IsInfinity(loadedPos.y) &&
            !float.IsNaN(loadedPos.z) && !float.IsInfinity(loadedPos.z))
        {
            transform.SetPositionAndRotation(loadedPos, loadedRot);
        }

        // ── Force-dirty все публикации ──
        lastPubOxygen    = DirtySentinel;
        lastPubEnergy    = DirtySentinel;
        lastPubDepth     = DirtySentinel;
        lastPubIntegrity = DirtySentinel;
        lastPubPressure  = DirtySentinel;
    }

    // ═════════════════════════════════════════════════════════
    //  SIMULATION STEPS (private, zero-alloc)
    // ═════════════════════════════════════════════════════════

    private void ComputeDepthAndPressure()
    {
        depth    = math.max(0f, surfaceWorldY - transform.position.y);
        pressure = 1f + depth * 0.1f;
    }

    private void DrainOxygen(float dt)
    {
        float pressureFactor = math.max(1f, pressure * 0.5f);
        oxygen = math.max(0f, oxygen - stats.OxygenConsumptionRate * pressureFactor * dt);
    }

    private void DrainEnergy(float dt)
    {
        float weightFactor = 1f + weight * 0.005f;
        energy = math.max(0f, energy - stats.EnergyConsumptionRate * weightFactor * dt);
    }

    private void ApplyPressureDamage(float dt)
    {
        if (depth <= stats.SafeDepth) return;

        float excess = depth - stats.SafeDepth;
        float scale  = 1f + excess * stats.PressureScalePerMeter;
        integrity = math.max(0f, integrity - stats.PressureDamageRate * scale * dt);
    }

    // ═════════════════════════════════════════════════════════
    //  EVENT PUBLISHING (throttled)
    // ═════════════════════════════════════════════════════════

    private void PublishDirty()
    {
        if (math.abs(oxygen - lastPubOxygen) > Epsilon)
        {
            lastPubOxygen = oxygen;
            OnOxygenChanged?.Invoke(oxygen);

            if (OxygenNormalized < 0.15f)
                OnOxygenCritical?.Invoke(OxygenNormalized);
        }

        if (math.abs(energy - lastPubEnergy) > Epsilon)
        {
            lastPubEnergy = energy;
            OnEnergyChanged?.Invoke(energy);
        }

        if (math.abs(depth - lastPubDepth) > Epsilon)
        {
            lastPubDepth = depth;
            OnDepthChanged?.Invoke(depth);
        }

        if (math.abs(integrity - lastPubIntegrity) > Epsilon)
        {
            lastPubIntegrity = integrity;
            OnIntegrityChanged?.Invoke(integrity);
        }

        if (math.abs(pressure - lastPubPressure) > Epsilon)
        {
            lastPubPressure = pressure;
            OnPressureChanged?.Invoke(pressure);
        }
    }

    private void CheckLethalConditions()
    {
        if (oxygen > 0f && integrity > 0f) return;

        alive = false;
        OnDeath?.Invoke();

        enabled = false;
    }

    // ═════════════════════════════════════════════════════════
    //  PUBLIC API (items, inventory, triggers)
    // ═════════════════════════════════════════════════════════

    public void RefillOxygen(float amount)
    {
        oxygen = math.min(stats.MaxOxygen, oxygen + math.max(0f, amount));
        ForceDirty(ref lastPubOxygen);
    }

    public void RechargeEnergy(float amount)
    {
        energy = math.clamp(energy + amount, 0f, stats.MaxEnergy);
        ForceDirty(ref lastPubEnergy);
    }

    /// <summary>
    /// Drains energy from the suit (used by Flashlight, PDA, tools).
    /// v5.0 ENTERPRISE addition for equipment battery drain.
    /// </summary>
    /// <param name="amount">Amount to drain (positive number).</param>
    public void DrainEnergy(int amount)
    {
        if (amount <= 0) return;
        energy = math.max(0f, energy - amount);
        ForceDirty(ref lastPubEnergy);
    }

    public void Repair(float amount)
    {
        integrity = math.min(stats.MaxIntegrity, integrity + math.max(0f, amount));
        ForceDirty(ref lastPubIntegrity);
    }

    /// <summary>
    /// Наносит урон целостности костюма (integrity).
    /// Вызывается извне: существами (HectonBaseAI), ловушками, средой.
    ///
    /// <param name="amount">Абсолютное значение урона (положительное число).
    /// Отрицательные значения игнорируются (используй Repair для лечения).</param>
    ///
    /// Zero-GC: никаких аллокаций. Проверка смерти — через throttled events.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (!alive)  return;
        if (amount <= 0f) return;

        integrity = math.max(0f, integrity - amount);
        ForceDirty(ref lastPubIntegrity);

        // ── Немедленная проверка смерти ──
        // Не ждём следующий Tick — игрок может умереть между кадрами
        // при множественных попаданиях за один кадр.
        CheckLethalConditions();
    }

    public void SetWeight(float kg)
    {
        weight = math.max(0f, kg);
        OnWeightChanged?.Invoke(weight);
    }

    public void SetSurfaceY(float y) => surfaceWorldY = y;

    public void OverrideStats(SurvivalStats newStats)
    {
        if (newStats == null)
        {
            Debug.LogWarning("[HectonSurvival] Attempted to assign null SurvivalStats.");
            return;
        }

        stats     = newStats;
        oxygen    = math.min(oxygen,    stats.MaxOxygen);
        energy    = math.min(energy,    stats.MaxEnergy);
        integrity = math.min(integrity, stats.MaxIntegrity);

        lastPubOxygen    = DirtySentinel;
        lastPubEnergy    = DirtySentinel;
        lastPubDepth     = DirtySentinel;
        lastPubIntegrity = DirtySentinel;
        lastPubPressure  = DirtySentinel;
    }

    // ─── Internal utility ────────────────────────────────────

    private static void ForceDirty(ref float lastPub) => lastPub = DirtySentinel;
}