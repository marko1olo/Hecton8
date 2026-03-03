using UnityEngine;

/// <summary>
/// Immutable-at-runtime data container for Hecton suit survival parameters.
/// Create via: Assets → Create → Hecton → Survival Stats.
/// 
/// Design notes:
/// ─ All fields are readonly at runtime (no setters exposed).
/// ─ Validation in OnValidate prevents designer errors in the Inspector.
/// ─ Multiple profiles (easy / normal / hard) are separate .asset files.
/// </summary>
[CreateAssetMenu(
    fileName = "NewSurvivalStats",
    menuName = "Hecton/Survival Stats",
    order    = 0)]
public sealed class SurvivalStats : ScriptableObject
{
    // ─── Oxygen ──────────────────────────────────────────────

    [Header("Oxygen")]
    [Tooltip("Maximum oxygen capacity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxOxygen = 100f;

    [Tooltip("Base oxygen drain per second at surface pressure (1 ATM).")]
    [Min(0f)]
    [SerializeField] private float oxygenConsumptionRate = 1.5f;

    // ─── Energy ──────────────────────────────────────────────

    [Header("Energy")]
    [Tooltip("Maximum energy capacity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxEnergy = 200f;

    [Tooltip("Base energy drain per second with zero equipment weight.")]
    [Min(0f)]
    [SerializeField] private float energyConsumptionRate = 0.8f;

    // ─── Integrity ───────────────────────────────────────────

    [Header("Integrity")]
    [Tooltip("Maximum hull integrity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxIntegrity = 100f;

    // ─── Pressure ────────────────────────────────────────────

    [Header("Pressure & Depth")]
    [Tooltip("Depth (metres) below which the suit starts taking pressure damage.")]
    [Min(0f)]
    [SerializeField] private float safeDepth = 50f;

    [Tooltip("Base integrity damage per second when depth exceeds SafeDepth.")]
    [Min(0f)]
    [SerializeField] private float pressureDamageRate = 2f;

    [Tooltip("Extra damage scaling per metre beyond SafeDepth. " +
             "Formula: damage = PressureDamageRate × (1 + excess × this).")]
    [Min(0f)]
    [SerializeField] private float pressureScalePerMeter = 0.02f;

    // ─── Public read-only accessors (inlined by JIT) ─────────

    public float MaxOxygen              => maxOxygen;
    public float OxygenConsumptionRate  => oxygenConsumptionRate;
    public float MaxEnergy              => maxEnergy;
    public float EnergyConsumptionRate  => energyConsumptionRate;
    public float MaxIntegrity           => maxIntegrity;
    public float SafeDepth              => safeDepth;
    public float PressureDamageRate     => pressureDamageRate;
    public float PressureScalePerMeter  => pressureScalePerMeter;

    // ─── Editor-only validation ──────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Clamp to sane minimums — prevents division-by-zero at runtime
        maxOxygen              = Mathf.Max(1f,  maxOxygen);
        maxEnergy              = Mathf.Max(1f,  maxEnergy);
        maxIntegrity           = Mathf.Max(1f,  maxIntegrity);
        oxygenConsumptionRate  = Mathf.Max(0f,  oxygenConsumptionRate);
        energyConsumptionRate  = Mathf.Max(0f,  energyConsumptionRate);
        pressureDamageRate     = Mathf.Max(0f,  pressureDamageRate);
        pressureScalePerMeter  = Mathf.Max(0f,  pressureScalePerMeter);
        safeDepth              = Mathf.Max(0f,  safeDepth);
    }
#endif
}