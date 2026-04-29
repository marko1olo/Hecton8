using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    public enum FaunaDriveChannel : byte
    {
        Hunger = 0,
        Fear = 1,
        Curiosity = 2
    }

    public enum FaunaInteractionKind : byte
    {
        None = 0,
        Stun = 1,
        Cut = 2
    }

    public enum FaunaAttackPattern : byte
    {
        Ram = 0,
        Bite = 1,
        TailWhip = 2,
        SonicPulse = 3,
        Emp = 4
    }

    [Serializable]
    public struct FaunaInteractionMatrixEntry
    {
        [Tooltip("Interaction channel resolved by fauna damage/utility responders.")]
        public FaunaInteractionKind interactionKind;

        [Min(0f)]
        [Tooltip("Damage multiplier applied when this interaction is translated into health loss.")]
        public float damageMultiplier;

        [Min(0f)]
        [Tooltip("Retreat duration forced into the cognition bridge after this interaction.")]
        public float retreatDurationSeconds;

        [Range(0f, 1f)]
        [Tooltip("Additional fear burst injected into the fauna state after the interaction.")]
        public float fearImpulse01;

        [Tooltip("If true, the interaction forces a retreat response instead of aggression.")]
        public bool forceRetreat;
    }

    public readonly struct SpeciesCognitionTuning
    {
        public SpeciesCognitionTuning(float hungerWeight, float fearWeight, float curiosityWeight)
        {
            HungerWeight = math.max(0.1f, hungerWeight);
            FearWeight = math.max(0.1f, fearWeight);
            CuriosityWeight = math.max(0.1f, curiosityWeight);
        }

        public float HungerWeight { get; }
        public float FearWeight { get; }
        public float CuriosityWeight { get; }
    }

    public readonly struct FaunaInteractionResponse
    {
        public FaunaInteractionResponse(
            FaunaInteractionKind interactionKind,
            float damageMultiplier,
            float retreatDurationSeconds,
            float fearImpulse01,
            bool forceRetreat)
        {
            InteractionKind = interactionKind;
            DamageMultiplier = math.max(0f, damageMultiplier);
            RetreatDurationSeconds = math.max(0f, retreatDurationSeconds);
            FearImpulse01 = math.saturate(fearImpulse01);
            ForceRetreat = forceRetreat;
        }

        public FaunaInteractionKind InteractionKind { get; }
        public float DamageMultiplier { get; }
        public float RetreatDurationSeconds { get; }
        public float FearImpulse01 { get; }
        public bool ForceRetreat { get; }
    }

    /// <summary>
    /// Authoring template for fauna spawn/runtime descriptors.
    /// Builds a blittable payload that can be copied into SOA lanes without pulling managed authoring state into hot paths.
    /// </summary>
    [CreateAssetMenu(fileName = "FaunaDataTemplate_", menuName = "Hecton8/Fauna/Data Template")]
    public sealed class FaunaDataTemplate : ScriptableObject
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        public struct RuntimeDescriptor
        {
            public int SpeciesId;
            public float MassKg;
            public float BodyRadiusMeters;
            public float CruiseSpeedMetersPerSecond;
            public float MaxSpeedMetersPerSecond;
            public float SteeringResponse;
            public float4 VatPositionScaleBias;
            public float4 VatNormalScaleBias;
            public float4 VatPhaseOffsetScale;
            public uint DefaultBoidStateMask;
            public uint SpawnFlags;
            public int MaxSchoolCount;
            public int Reserved0;
        }

        [Header("Identity")]
        [SerializeField, Tooltip("Stable species identifier mirrored into runtime SOA descriptors.")]
        private int speciesId;

        [SerializeField, Tooltip("Optional high-level behavior profile linked to this spawn template.")]
        private FaunaSpeciesProfile speciesProfile;

        [SerializeField, Tooltip("Primary creature archetype that owns this fauna template.")]
        private CreatureArchetypeData archetype;

        [Header("Physics")]
        [SerializeField, Min(0.01f), Tooltip("Baseline body mass used by custom fauna kinematics and pushback lanes.")]
        private float massKg = 12f;

        [SerializeField, Min(0.01f), Tooltip("Broadphase radius used by fauna separation, steering and avoidance.")]
        private float bodyRadiusMeters = 0.65f;

        [SerializeField, Min(0.01f), Tooltip("Nominal cruise speed written into the runtime descriptor.")]
        private float cruiseSpeedMetersPerSecond = 2.5f;

        [SerializeField, Min(0.01f), Tooltip("Maximum chase/flee speed written into the runtime descriptor.")]
        private float maxSpeedMetersPerSecond = 4.5f;

        [SerializeField, Min(0.01f), Tooltip("Scalar used by steering jobs to tune turn response without reading authoring assets.")]
        private float steeringResponse = 1.25f;

        [Header("VAT")]
        [SerializeField, Tooltip("Scale/bias payload for VAT position sampling: xy = scale, zw = bias.")]
        private Vector4 vatPositionScaleBias = new Vector4(1f, 1f, 0f, 0f);

        [SerializeField, Tooltip("Scale/bias payload for VAT normal sampling: xy = scale, zw = bias.")]
        private Vector4 vatNormalScaleBias = new Vector4(1f, 1f, 0f, 0f);

        [SerializeField, Tooltip("Per-spawn VAT phase and offset payload: x = phase scale, y = frame offset, z = playback bias, w = reserved.")]
        private Vector4 vatPhaseOffsetScale = new Vector4(1f, 0f, 0f, 0f);

        [Header("Boid Defaults")]
        [SerializeField, Tooltip("Default boid-state bitmask copied into runtime descriptors for GPU or Burst flock lanes.")]
        private uint defaultBoidStateMask = 0x00000003u;

        [SerializeField, Tooltip("Template-level spawn flags packed into the runtime descriptor for zero-branch initialization.")]
        private uint spawnFlags;

        [SerializeField, Min(1), Tooltip("Upper bound for local school size spawned from this template.")]
        private int maxSchoolCount = 12;

        [Header("Cognition")]
        [SerializeField, Tooltip("Drive weights indexed as Hunger, Fear, Curiosity.")]
        private float[] driveWeights = { 1f, 1f, 1f };

        [SerializeField, Tooltip("Species-specific response table used when fauna is hit by stun/cut interaction channels.")]
        private FaunaInteractionMatrixEntry[] interactionMatrix =
        {
            new FaunaInteractionMatrixEntry
            {
                interactionKind = FaunaInteractionKind.Stun,
                damageMultiplier = 1f,
                retreatDurationSeconds = 4f,
                fearImpulse01 = 0.6f,
                forceRetreat = true
            },
            new FaunaInteractionMatrixEntry
            {
                interactionKind = FaunaInteractionKind.Cut,
                damageMultiplier = 1.2f,
                retreatDurationSeconds = 6f,
                fearImpulse01 = 0.85f,
                forceRetreat = true
            }
        };

        [Header("Scanner Sync")]
        [SerializeField, Tooltip("Stable scan entry ID emitted by ScannableTarget when this fauna is discovered.")]
        private string scanEntryId = "fauna.unknown";

        [SerializeField, Tooltip("PDA-facing scan entry title.")]
        private string scanEntryTitle = "UNIDENTIFIED BIOFORM";

        [SerializeField, Tooltip("Scanner category written into scan metadata.")]
        private string scanEntryCategory = "Fauna";

        [TextArea(2, 5)]
        [SerializeField, Tooltip("Summary archived by the scanner and research systems.")]
        private string scanEntrySummary = "Passive fauna contact. Further xenobiology classification pending.";

        [SerializeField, Tooltip("Stable lore/research hashes unlocked when the fauna scan is archived.")]
        private uint[] loreUnlockHashes = Array.Empty<uint>();

        [SerializeField, Tooltip("Primary lore hash that unlocks full scanner behavior prediction for this fauna.")]
        private uint fullLoreHash;

        [SerializeField, Tooltip("Supported combat patterns for this fauna. Used by scanner prediction and downstream behavior authoring.")]
        private FaunaAttackPattern[] attackPatterns =
        {
            FaunaAttackPattern.Ram,
            FaunaAttackPattern.Bite,
            FaunaAttackPattern.TailWhip,
            FaunaAttackPattern.SonicPulse,
            FaunaAttackPattern.Emp
        };

        /// <summary>
        /// Stable species identifier for gameplay-side lookups.
        /// </summary>
        public int SpeciesId => speciesId;

        /// <summary>
        /// Optional high-level species profile linked to this template.
        /// </summary>
        public FaunaSpeciesProfile SpeciesProfile => speciesProfile;

        /// <summary>
        /// Authored broadphase body radius used by dodge and separation logic.
        /// </summary>
        public float BodyRadiusMeters => math.max(0.01f, bodyRadiusMeters);

        /// <summary>
        /// Primary creature archetype linked to this data template.
        /// </summary>
        public CreatureArchetypeData Archetype => archetype;

        /// <summary>
        /// Stable scanner entry identifier used by fauna scan registration.
        /// </summary>
        public string ScanEntryId => string.IsNullOrWhiteSpace(scanEntryId)
            ? $"fauna.species.{speciesId}"
            : scanEntryId.Trim();

        /// <summary>
        /// Optional authored scanner title override.
        /// </summary>
        public string ScanEntryTitle => scanEntryTitle;

        /// <summary>
        /// Optional authored scanner category override.
        /// </summary>
        public string ScanEntryCategory => scanEntryCategory;

        /// <summary>
        /// Optional authored scanner summary override.
        /// </summary>
        public string ScanEntrySummary => scanEntrySummary;

        /// <summary>
        /// Stable lore/research hashes emitted when this fauna scan is resolved.
        /// </summary>
        public uint[] LoreUnlockHashes => loreUnlockHashes;

        /// <summary>
        /// Stable lore hash that upgrades scanner prediction from generic contact intel to explicit combat behavior intel.
        /// </summary>
        public uint FullLoreHash => fullLoreHash != 0u
            ? fullLoreHash
            : ResolvePrimaryLoreHash();

        /// <summary>
        /// Authored combat-pattern catalog for this fauna template.
        /// </summary>
        public FaunaAttackPattern[] AttackPatterns => attackPatterns;

        /// <summary>
        /// Builds the blittable runtime descriptor consumed by SOA-friendly fauna systems.
        /// </summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                SpeciesId = speciesId,
                MassKg = math.max(0.01f, massKg),
                BodyRadiusMeters = math.max(0.01f, bodyRadiusMeters),
                CruiseSpeedMetersPerSecond = math.max(0.01f, cruiseSpeedMetersPerSecond),
                MaxSpeedMetersPerSecond = math.max(cruiseSpeedMetersPerSecond, maxSpeedMetersPerSecond),
                SteeringResponse = math.max(0.01f, steeringResponse),
                VatPositionScaleBias = new float4(vatPositionScaleBias.x, vatPositionScaleBias.y, vatPositionScaleBias.z, vatPositionScaleBias.w),
                VatNormalScaleBias = new float4(vatNormalScaleBias.x, vatNormalScaleBias.y, vatNormalScaleBias.z, vatNormalScaleBias.w),
                VatPhaseOffsetScale = new float4(vatPhaseOffsetScale.x, vatPhaseOffsetScale.y, vatPhaseOffsetScale.z, vatPhaseOffsetScale.w),
                DefaultBoidStateMask = defaultBoidStateMask,
                SpawnFlags = spawnFlags,
                MaxSchoolCount = math.max(1, maxSchoolCount),
                Reserved0 = 0
            };
        }

        /// <summary>
        /// Builds the species cognition tuning consumed by the shared Burst cognition table.
        /// </summary>
        public SpeciesCognitionTuning BuildSpeciesCognitionTuning()
        {
            return new SpeciesCognitionTuning(
                ResolveDriveWeight(FaunaDriveChannel.Hunger, 1f),
                ResolveDriveWeight(FaunaDriveChannel.Fear, 1f),
                ResolveDriveWeight(FaunaDriveChannel.Curiosity, 1f));
        }

        /// <summary>
        /// Resolves the authored scanner title with a fallback display name.
        /// </summary>
        public string ResolveScanTitle(string fallbackTitle)
        {
            return string.IsNullOrWhiteSpace(scanEntryTitle)
                ? fallbackTitle
                : scanEntryTitle.Trim();
        }

        /// <summary>
        /// Resolves the authored scanner category with a fallback category.
        /// </summary>
        public string ResolveScanCategory(string fallbackCategory)
        {
            return string.IsNullOrWhiteSpace(scanEntryCategory)
                ? fallbackCategory
                : scanEntryCategory.Trim();
        }

        /// <summary>
        /// Resolves the authored scanner summary with a fallback summary.
        /// </summary>
        public string ResolveScanSummary(string fallbackSummary)
        {
            return string.IsNullOrWhiteSpace(scanEntrySummary)
                ? fallbackSummary
                : scanEntrySummary.Trim();
        }

        /// <summary>
        /// Resolves one interaction response entry from the authored matrix.
        /// </summary>
        public bool TryGetInteractionResponse(FaunaInteractionKind interactionKind, out FaunaInteractionResponse response)
        {
            if (interactionMatrix != null)
            {
                for (int i = 0; i < interactionMatrix.Length; i++)
                {
                    FaunaInteractionMatrixEntry entry = interactionMatrix[i];
                    if (entry.interactionKind != interactionKind)
                        continue;

                    response = new FaunaInteractionResponse(
                        entry.interactionKind,
                        entry.damageMultiplier,
                        entry.retreatDurationSeconds,
                        entry.fearImpulse01,
                        entry.forceRetreat);
                    return true;
                }
            }

            response = default;
            return false;
        }

        /// <summary>
        /// Resolves one authored drive weight with a safe fallback.
        /// </summary>
        public float ResolveDriveWeight(FaunaDriveChannel channel, float fallbackValue)
        {
            int index = (int)channel;
            if (driveWeights == null || index < 0 || index >= driveWeights.Length)
                return math.max(0.1f, fallbackValue);

            return math.max(0.1f, driveWeights[index]);
        }

        /// <summary>
        /// Resolves whether the authored attack-pattern catalog contains a specific pattern.
        /// </summary>
        public bool SupportsAttackPattern(FaunaAttackPattern attackPattern)
        {
            if (attackPatterns == null)
                return false;

            for (int i = 0; i < attackPatterns.Length; i++)
            {
                if (attackPatterns[i] == attackPattern)
                    return true;
            }

            return false;
        }

        private uint ResolvePrimaryLoreHash()
        {
            if (loreUnlockHashes == null || loreUnlockHashes.Length == 0)
                return 0u;

            return loreUnlockHashes[0];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            speciesId = math.max(0, speciesId);
            massKg = math.max(0.01f, massKg);
            bodyRadiusMeters = math.max(0.01f, bodyRadiusMeters);
            cruiseSpeedMetersPerSecond = math.max(0.01f, cruiseSpeedMetersPerSecond);
            maxSpeedMetersPerSecond = math.max(cruiseSpeedMetersPerSecond, maxSpeedMetersPerSecond);
            steeringResponse = math.max(0.01f, steeringResponse);
            maxSchoolCount = math.max(1, maxSchoolCount);

            if (driveWeights == null || driveWeights.Length != 3)
            {
                float hunger = driveWeights != null && driveWeights.Length > 0 ? driveWeights[0] : 1f;
                float fear = driveWeights != null && driveWeights.Length > 1 ? driveWeights[1] : 1f;
                float curiosity = driveWeights != null && driveWeights.Length > 2 ? driveWeights[2] : 1f;
                driveWeights = new[] { hunger, fear, curiosity }; // COLD ALLOC: float[3] - fixed fauna drive-weight channel bank - owner: FaunaDataTemplate
            }

            for (int i = 0; i < driveWeights.Length; i++)
                driveWeights[i] = math.max(0.1f, driveWeights[i]);

            if (attackPatterns == null || attackPatterns.Length == 0)
            {
                attackPatterns = new[]
                {
                    FaunaAttackPattern.Ram,
                    FaunaAttackPattern.Bite,
                    FaunaAttackPattern.TailWhip,
                    FaunaAttackPattern.SonicPulse,
                    FaunaAttackPattern.Emp
                }; // COLD ALLOC: FaunaAttackPattern[5] - default authored combat-pattern catalog - owner: FaunaDataTemplate
            }

            if (fullLoreHash == 0u)
                fullLoreHash = ResolvePrimaryLoreHash();
        }
#endif
    }
}
