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
    // ─── Legacy / Requested Fields ───────────────────────────

    [Header("Legacy")]
    public float maxHealth = 100f;
    public float oxygenCapacity = 60f;
    public float temperatureTolerance = 15f;

    // ─── Oxygen ──────────────────────────────────────────────

    [Header("Oxygen")]
    [Tooltip("Maximum oxygen capacity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxOxygen = 100f;

    [Tooltip("Base oxygen drain per second before multiplicative pressure, movement, stress, and leak factors are applied.")]
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

    [Tooltip("Soft carry-capacity budget used by survival oxygen/strain scaling.")]
    [Min(1f)]
    [SerializeField] private float carryCapacityKg = 200f;

    // ─── Integrity ───────────────────────────────────────────

    [Header("Integrity")]
    [Tooltip("Maximum hull integrity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxIntegrity = 100f;

    // ─── Hunger ──────────────────────────────────────────────

    [Header("Hunger")]
    [Tooltip("Maximum hunger capacity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxHunger = 100f;

    [Tooltip("Hunger drain per second (slow drain).")]
    [Min(0f)]
    [SerializeField] private float hungerDrainRate = 0.1f;

    [Tooltip("Integrity damage per second when hunger reaches 0.")]
    [Min(0f)]
    [SerializeField] private float starvationDamageRate = 1f;

    // ─── Thirst ──────────────────────────────────────────────

    [Header("Thirst")]
    [Tooltip("Maximum thirst capacity (absolute units).")]
    [Min(1f)]
    [SerializeField] private float maxThirst = 100f;

    [Tooltip("Thirst drain per second (slow drain).")]
    [Min(0f)]
    [SerializeField] private float thirstDrainRate = 0.15f;

    [Tooltip("Integrity damage per second when thirst reaches 0.")]
    [Min(0f)]
    [SerializeField] private float dehydrationDamageRate = 1.5f;

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

    [Tooltip("Energy consumption multiplier per degree outside safe range. Internal suit temperature drives both heating and cooling power draw.")]
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
    public virtual float CarryCapacityKg        => carryCapacityKg;
    public virtual float MaxIntegrity           => maxIntegrity;

    public virtual float MaxHunger              => maxHunger;
    public virtual float HungerDrainRate        => hungerDrainRate;
    public virtual float StarvationDamageRate   => starvationDamageRate;

    public virtual float MaxThirst              => maxThirst;
    public virtual float ThirstDrainRate        => thirstDrainRate;
    public virtual float DehydrationDamageRate  => dehydrationDamageRate;

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
        carryCapacityKg        = Mathf.Max(1f,  carryCapacityKg);
        maxIntegrity           = Mathf.Max(1f,  maxIntegrity);
        maxHunger              = Mathf.Max(1f,  maxHunger);
        maxThirst              = Mathf.Max(1f,  maxThirst);
        oxygenConsumptionRate  = Mathf.Max(0f,  oxygenConsumptionRate);
        energyConsumptionRate  = Mathf.Max(0f,  energyConsumptionRate);
        hungerDrainRate        = Mathf.Max(0f,  hungerDrainRate);
        thirstDrainRate        = Mathf.Max(0f,  thirstDrainRate);
        starvationDamageRate   = Mathf.Max(0f,  starvationDamageRate);
        dehydrationDamageRate  = Mathf.Max(0f,  dehydrationDamageRate);
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
