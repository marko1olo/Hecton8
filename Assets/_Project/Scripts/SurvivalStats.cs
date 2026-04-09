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
public class SurvivalStats : ScriptableObject
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


    // ─── Temperature ─────────────────────────────────────────

    [Header("Temperature")]
    [Tooltip("Minimum safe temperature (°C). Below this, energy drain increases and integrity may drop.")]
    [SerializeField] private float minSafeTemp = -5f;

    [Tooltip("Maximum safe temperature (°C). Above this, energy drain increases and integrity may drop.")]
    [SerializeField] private float maxSafeTemp = 45f;

    [Tooltip("Base integrity damage per second when temperature is outside safe range.")]
    [Min(0f)]
    [SerializeField] private float tempDamageRate = 1f;

    [Tooltip("Energy consumption multiplier per degree outside safe range.")]
    [Min(0f)]
    [SerializeField] private float tempEnergyScale = 0.05f;

    // ─── Radiation ───────────────────────────────────────────

    [Header("Radiation")]
    [Tooltip("Radiation threshold (Rem/h) above which the suit takes integrity damage.")]
    [Min(0f)]
    [SerializeField] private float radiationThreshold = 0.5f;

    [Tooltip("Integrity damage per Rem/h above threshold.")]
    [Min(0f)]
    [SerializeField] private float radiationDamageRate = 5f;
    // ─── Public read-only accessors (inlined by JIT) ─────────

    public virtual float MaxOxygen              => maxOxygen;
    public virtual float OxygenConsumptionRate  => oxygenConsumptionRate;
    public virtual float MaxEnergy              => maxEnergy;
    public virtual float EnergyConsumptionRate  => energyConsumptionRate;
    public virtual float MaxIntegrity           => maxIntegrity;
    public virtual float SafeDepth              => safeDepth;
    public virtual float PressureDamageRate     => pressureDamageRate;
    public virtual float PressureScalePerMeter  => pressureScalePerMeter;

    public virtual float MinSafeTemp            => minSafeTemp;
    public virtual float MaxSafeTemp            => maxSafeTemp;
    public virtual float TempDamageRate         => tempDamageRate;
    public virtual float TempEnergyScale        => tempEnergyScale;

    public virtual float RadiationThreshold     => radiationThreshold;
    public virtual float RadiationDamageRate    => radiationDamageRate;

    // ─── Editor-only validation ──────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isCompiling ||
            UnityEditor.EditorApplication.isUpdating ||
            UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Clamp to sane minimums — prevents division-by-zero at runtime
        maxOxygen              = Mathf.Max(1f,  maxOxygen);
        maxEnergy              = Mathf.Max(1f,  maxEnergy);
        maxIntegrity           = Mathf.Max(1f,  maxIntegrity);
        oxygenConsumptionRate  = Mathf.Max(0f,  oxygenConsumptionRate);
        energyConsumptionRate  = Mathf.Max(0f,  energyConsumptionRate);
        pressureDamageRate     = Mathf.Max(0f,  pressureDamageRate);
        pressureScalePerMeter  = Mathf.Max(0f,  pressureScalePerMeter);
        safeDepth              = Mathf.Max(0f,  safeDepth);

        tempDamageRate         = Mathf.Max(0f,  tempDamageRate);
        tempEnergyScale        = Mathf.Max(0f,  tempEnergyScale);
        radiationThreshold     = Mathf.Max(0f,  radiationThreshold);
        radiationDamageRate    = Mathf.Max(0f,  radiationDamageRate);

        if (maxSafeTemp < minSafeTemp) maxSafeTemp = minSafeTemp + 10f;
    }
#endif
}
