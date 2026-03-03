using System;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Core survival simulation for the Hecton diving suit.
/// Attach to the player GameObject and assign a SurvivalStats asset.
///
/// Performance guarantees:
/// ─ Zero GC allocations in Update (no string ops, no boxing, no LINQ).
/// ─ Events are throttled: only fire when |current − lastPublished| > ε.
/// ─ All math uses Unity.Mathematics (burst-friendly, no Mathf overhead).
///
/// Data-driven: every tunable parameter comes from the SurvivalStats
/// ScriptableObject, so designers can create multiple difficulty profiles
/// without touching code.
/// </summary>
[DisallowMultipleComponent]
public sealed class HectonSurvivalSystem : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────

    [Header("Data")]
    [Tooltip("Drag a SurvivalStats .asset here to configure all suit parameters.")]
    [SerializeField] private SurvivalStats stats;

    [Header("Scene")]
    [Tooltip("World-space Y coordinate of the water surface.")]
    [SerializeField] private float surfaceWorldY;

    // ─── Runtime state (stack-allocated, no GC) ──────────────

    private float oxygen;
    private float energy;
    private float depth;
    private float integrity;
    private float pressure;
    private float weight;          // set externally by inventory system

    private bool  alive = true;

    // ─── Throttling: last published values ───────────────────
    //     Event fires only when |current − last| > Epsilon.

    private float lastPubOxygen;
    private float lastPubEnergy;
    private float lastPubDepth;
    private float lastPubIntegrity;
    private float lastPubPressure;

    /// <summary>
    /// Absolute-unit threshold for event throttling.
    /// 0.1 ≈ 0.1 % when max stat value is 100.
    /// </summary>
    private const float Epsilon = 0.1f;

    /// <summary>
    /// Sentinel value that guarantees the first event always fires.
    /// </summary>
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

    /// <summary>Equipment weight in kg. Set via SetWeight().</summary>
    public event Action<float> OnWeightChanged;

    /// <summary>
    /// Fires when O₂ drops below 15 %.
    /// Parameter: normalised value (0 … 1).
    /// </summary>
    public event Action<float> OnOxygenCritical;

    /// <summary>
    /// Player has died (O₂ = 0 or Integrity = 0).
    /// Component disables itself after this event.
    /// </summary>
    public event Action OnDeath;

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

    // ═════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═════════════════════════════════════════════════════════

    private void Awake()
    {
        // ── Guard: missing asset ─────────────────────────────
        if (stats == null)
        {
            Debug.LogError(
                $"[HectonSurvival] SurvivalStats asset is not assigned on \"{name}\". " +
                "Disabling component.");
            enabled = false;
            return;
        }

        // ── Initialise runtime state from data asset ─────────
        oxygen    = stats.MaxOxygen;
        energy    = stats.MaxEnergy;
        integrity = stats.MaxIntegrity;
        depth     = 0f;
        pressure  = 1f;
        weight    = 0f;
        alive     = true;

        // Force-dirty so the very first PublishDirty() fires all events
        lastPubOxygen    = DirtySentinel;
        lastPubEnergy    = DirtySentinel;
        lastPubDepth     = DirtySentinel;
        lastPubIntegrity = DirtySentinel;
        lastPubPressure  = DirtySentinel;
    }

    private void Update()
    {
        if (!alive) return;

        float dt = Time.deltaTime;

        // ── Simulation pipeline (order matters) ──────────────
        ComputeDepthAndPressure();
        DrainOxygen(dt);
        DrainEnergy(dt);
        ApplyPressureDamage(dt);

        // ── Output ───────────────────────────────────────────
        PublishDirty();
        CheckLethalConditions();
    }

    // ═════════════════════════════════════════════════════════
    //  SIMULATION STEPS (private, zero-alloc)
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Derives depth from world position and computes ambient pressure.
    /// Simplified model: +1 ATM per 10 m of water column.
    /// </summary>
    private void ComputeDepthAndPressure()
    {
        depth    = math.max(0f, surfaceWorldY - transform.position.y);
        pressure = 1f + depth * 0.1f;
    }

    /// <summary>
    /// Drains oxygen proportional to ambient pressure.
    /// At 1 ATM factor = 1.0, at 6 ATM factor = 3.0.
    /// Formula: drain = OxygenConsumptionRate × max(1, pressure × 0.5) × dt
    /// </summary>
    private void DrainOxygen(float dt)
    {
        float pressureFactor = math.max(1f, pressure * 0.5f);
        oxygen = math.max(0f, oxygen - stats.OxygenConsumptionRate * pressureFactor * dt);
    }

    /// <summary>
    /// Drains energy proportional to carried equipment weight.
    /// Formula: drain = EnergyConsumptionRate × (1 + weight × 0.005) × dt
    /// </summary>
    private void DrainEnergy(float dt)
    {
        float weightFactor = 1f + weight * 0.005f;
        energy = math.max(0f, energy - stats.EnergyConsumptionRate * weightFactor * dt);
    }

    /// <summary>
    /// Damages hull integrity when depth exceeds the safe threshold.
    /// Formula: damage = PressureDamageRate × (1 + excess × PressureScalePerMeter) × dt
    /// </summary>
    private void ApplyPressureDamage(float dt)
    {
        if (depth <= stats.SafeDepth) return;

        float excess = depth - stats.SafeDepth;
        float scale  = 1f + excess * stats.PressureScalePerMeter;
        integrity = math.max(0f, integrity - stats.PressureDamageRate * scale * dt);
    }

    // ═════════════════════════════════════════════════════════
    //  EVENT PUBLISHING  (throttled — fires only on meaningful change)
    // ═════════════════════════════════════════════════════════

    private void PublishDirty()
    {
        // Each block: compare with last published value → invoke only on delta > ε.
        // Null-conditional (?.) on delegate — zero cost when no listeners.

        // ── Oxygen ───────────────────────────────────────────
        if (math.abs(oxygen - lastPubOxygen) > Epsilon)
        {
            lastPubOxygen = oxygen;
            OnOxygenChanged?.Invoke(oxygen);

            if (OxygenNormalized < 0.15f)
                OnOxygenCritical?.Invoke(OxygenNormalized);
        }

        // ── Energy ───────────────────────────────────────────
        if (math.abs(energy - lastPubEnergy) > Epsilon)
        {
            lastPubEnergy = energy;
            OnEnergyChanged?.Invoke(energy);
        }

        // ── Depth ────────────────────────────────────────────
        if (math.abs(depth - lastPubDepth) > Epsilon)
        {
            lastPubDepth = depth;
            OnDepthChanged?.Invoke(depth);
        }

        // ── Integrity ────────────────────────────────────────
        if (math.abs(integrity - lastPubIntegrity) > Epsilon)
        {
            lastPubIntegrity = integrity;
            OnIntegrityChanged?.Invoke(integrity);
        }

        // ── Pressure ─────────────────────────────────────────
        if (math.abs(pressure - lastPubPressure) > Epsilon)
        {
            lastPubPressure = pressure;
            OnPressureChanged?.Invoke(pressure);
        }
    }

    /// <summary>
    /// Checks for lethal conditions and triggers death sequence.
    /// </summary>
    private void CheckLethalConditions()
    {
        if (oxygen > 0f && integrity > 0f) return;

        alive = false;
        OnDeath?.Invoke();
        enabled = false;          // stop further Updates
    }

    // ═════════════════════════════════════════════════════════
    //  PUBLIC API  (items, inventory, triggers)
    // ═════════════════════════════════════════════════════════

    /// <summary>Replenish O₂ (e.g., from an oxygen tank pickup).</summary>
    public void RefillOxygen(float amount)
    {
        oxygen = math.min(stats.MaxOxygen, oxygen + math.max(0f, amount));
        ForceDirty(ref lastPubOxygen);
    }

    /// <summary>Recharge the suit battery.</summary>
    public void RechargeEnergy(float amount)
    {
        energy = math.min(stats.MaxEnergy, energy + math.max(0f, amount));
        ForceDirty(ref lastPubEnergy);
    }

    /// <summary>Repair hull integrity (e.g., repair kit).</summary>
    public void Repair(float amount)
    {
        integrity = math.min(stats.MaxIntegrity, integrity + math.max(0f, amount));
        ForceDirty(ref lastPubIntegrity);
    }

    /// <summary>
    /// Sets current equipment weight in kg.
    /// Called by the inventory system when items are added or removed.
    /// </summary>
    public void SetWeight(float kg)
    {
        weight = math.max(0f, kg);
        OnWeightChanged?.Invoke(weight);
    }

    /// <summary>Update the water surface Y-coordinate at runtime.</summary>
    public void SetSurfaceY(float y) => surfaceWorldY = y;

    /// <summary>
    /// Swap the stats profile at runtime (e.g., suit upgrade).
    /// Re-initialises all values from the new asset.
    /// </summary>
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

        // Force all events to re-publish with the new data context
        lastPubOxygen    = DirtySentinel;
        lastPubEnergy    = DirtySentinel;
        lastPubDepth     = DirtySentinel;
        lastPubIntegrity = DirtySentinel;
        lastPubPressure  = DirtySentinel;
    }

    // ─── Internal utility ────────────────────────────────────

    /// <summary>
    /// Resets a last-published tracker to the dirty sentinel,
    /// ensuring the next PublishDirty() call fires the corresponding event.
    /// </summary>
    private static void ForceDirty(ref float lastPub) => lastPub = DirtySentinel;
}