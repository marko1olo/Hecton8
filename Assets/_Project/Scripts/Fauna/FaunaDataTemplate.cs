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

    [Flags]
    public enum FaunaDietMask : uint
    {
        None = 0u,
        Plankton = 1u << 0,
        Flora = 1u << 1,
        SmallFauna = 1u << 2,
        MediumFauna = 1u << 3,
        LargeFauna = 1u << 4,
        Carcass = 1u << 5,
        Player = 1u << 6,
        Machine = 1u << 7
    }

    public enum FaunaFoodChainTier : byte
    {
        Microfauna = 0,
        SmallHerbivore = 1,
        SwarmPassive = 2,
        SmallPredator = 3,
        MediumPredator = 4,
        LargePredator = 5,
        Leviathan = 6
    }

    public enum FaunaLightReactionMode : byte
    {
        None = 0,
        Aversion = 1,
        Frenzy = 2
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
        public SpeciesCognitionTuning(
            float hungerWeight,
            float fearWeight,
            float curiosityWeight,
            FaunaLightReactionMode lightReactionMode,
            float lightReactionRangeMeters,
            float lightReactionDotThreshold,
            float lightFrenzySpeedMultiplier,
            float lightReactionFearBoost01)
        {
            HungerWeight = math.max(0.1f, hungerWeight);
            FearWeight = math.max(0.1f, fearWeight);
            CuriosityWeight = math.max(0.1f, curiosityWeight);
            LightReactionMode = lightReactionMode;
            LightReactionRangeMeters = math.max(1f, lightReactionRangeMeters);
            LightReactionDotThreshold = math.clamp(lightReactionDotThreshold, -1f, 1f);
            LightFrenzySpeedMultiplier = math.max(1f, lightFrenzySpeedMultiplier);
            LightReactionFearBoost01 = math.saturate(lightReactionFearBoost01);
        }

        public float HungerWeight { get; }
        public float FearWeight { get; }
        public float CuriosityWeight { get; }
        public FaunaLightReactionMode LightReactionMode { get; }
        public float LightReactionRangeMeters { get; }
        public float LightReactionDotThreshold { get; }
        public float LightFrenzySpeedMultiplier { get; }
        public float LightReactionFearBoost01 { get; }
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

        [Header("Movement And Perception")]
        [SerializeField, Min(0.01f), Tooltip("Authoritative authored swim speed used by wander, flee and chase steering.")]
        private float swimSpeed = 2.5f;

        [SerializeField, Min(0.01f), Tooltip("Authoritative authored turn rate used by fauna steering.")]
        private float turnRate = 3f;

        [SerializeField, Range(10f, 360f), Tooltip("Horizontal vision cone angle in degrees used for direct player sight checks.")]
        private float visionConeAngle = 135f;

        [SerializeField, Min(0f), Tooltip("Authoritative aggro radius used by chase-state acquisition.")]
        private float aggroRadius = 20f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Normalized health threshold below which the fauna strongly prefers fleeing.")]
        private float fleeHealthThreshold = 0.3f;

        [SerializeField, Tooltip("High-level food-chain band used by ecosystem tuning and authoring review.")]
        private FaunaFoodChainTier foodChainTier = FaunaFoodChainTier.SmallHerbivore;

        [SerializeField, Tooltip("Bitmask describing what this fauna will consume when evaluating prey targets.")]
        private FaunaDietMask dietMask = FaunaDietMask.None;

        [SerializeField, Tooltip("Bitmask describing what this fauna counts as when other predators evaluate prey targets.")]
        private FaunaDietMask preyMask = FaunaDietMask.SmallFauna;

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

        [Header("Advanced Behaviors")]
        [SerializeField, Tooltip("If enabled, this fauna can path inside solid seabed density and breach upward for an ambush.")]
        private bool canBurrowAmbush;

        [SerializeField, Min(1f), Tooltip("Player distance to the seabed needed before the burrow ambush route arms.")]
        private float burrowSeabedTriggerDistanceMeters = 8f;

        [SerializeField, Min(0.5f), Tooltip("Maximum predator-to-breach distance before the sand-worm breach and grab sequence is allowed.")]
        private float burrowBreachDistanceMeters = 3f;

        [SerializeField, Min(0f), Tooltip("Acceleration magnitude injected into the player toward the breach point during the grab phase.")]
        private float burrowPullAcceleration = 18f;

        [SerializeField, Min(0f), Tooltip("Seconds the player locomotion stays partially locked after the ambush breach connects.")]
        private float burrowLockDurationSeconds = 2.5f;

        [SerializeField, Tooltip("If enabled, the fauna can pulse a lure field that drags the player when stared at directly.")]
        private bool canDazzleHypnotize;

        [SerializeField, Min(0.5f), Tooltip("Maximum lure distance for the dazzle hypnosis pull.")]
        private float dazzleRangeMeters = 14f;

        [SerializeField, Range(-1f, 1f), Tooltip("Required camera-vs-creature dot product before hypnosis pull activates.")]
        private float dazzleLookDotThreshold = 0.82f;

        [SerializeField, Min(0f), Tooltip("Acceleration magnitude applied toward the fauna while the hypnosis gaze lock is active.")]
        private float dazzlePullAcceleration = 6f;

        [SerializeField, Tooltip("How this species reacts when caught in the player flashlight cone.")]
        private FaunaLightReactionMode lightReactionMode = FaunaLightReactionMode.None;

        [SerializeField, Min(1f), Tooltip("Maximum flashlight distance that can trigger aversion or frenzy cognition.")]
        private float lightReactionRangeMeters = 35f;

        [SerializeField, Range(-1f, 1f), Tooltip("Required player-forward dot product before flashlight reaction is considered active.")]
        private float lightReactionDotThreshold = 0.65f;

        [SerializeField, Min(1f), Tooltip("Speed multiplier used by mutated light-frenzy species when illuminated.")]
        private float lightFrenzySpeedMultiplier = 2f;

        [SerializeField, Range(0f, 1f), Tooltip("Fear contribution applied to light-averse species when illuminated.")]
        private float lightReactionFearBoost01 = 0.55f;

        [SerializeField, Tooltip("If enabled, taking damage emits a parental-defense chemical alarm to nearby adults of the same species.")]
        private bool emitsParentalDefenseSignal;

        [SerializeField, Tooltip("If enabled, this fauna can escalate into a hunt when a same-species parental defense signal is nearby.")]
        private bool respondsToParentalDefenseSignal;

        [SerializeField, Min(1f), Tooltip("Radius used by the parental defense chemical alert.")]
        private float parentalDefenseRadiusMeters = 200f;

        [SerializeField, Min(0f), Tooltip("Duration of the forced hunt/retaliation state after a parental defense alert.")]
        private float parentalDefenseHuntDurationSeconds = 18f;

        [SerializeField, Min(0f), Tooltip("Seconds of sensor blindness applied when this fauna lands an EMP or sonic pulse attack.")]
        private float empBlindDurationSeconds = 15f;

        [SerializeField, Range(0f, 1f), Tooltip("Clarity suppression intensity injected into the trauma dispatcher during the EMP blind window.")]
        private float empClaritySuppression01 = 1f;

        [Header("Echolocation Mimicry")]
        [SerializeField, Tooltip("If enabled, apex fauna can publish a false distress beacon into sonar before ambush.")]
        private bool canEmitMimicDistressPing;

        [SerializeField, Min(1f), Tooltip("Radius of the false acoustic ping registered into sonar/acoustic density maps.")]
        private float mimicPingRadiusMeters = 60f;

        [SerializeField, Range(0f, 1f), Tooltip("Normalized signal strength of the false distress beacon.")]
        private float mimicPingIntensity01 = 0.8f;

        [SerializeField, Min(0.1f), Tooltip("Seconds the false beacon remains visible if the player does not enter the kill radius.")]
        private float mimicPingLifetimeSeconds = 7f;

        [SerializeField, Min(0.1f), Tooltip("Minimum seconds between false beacon emissions.")]
        private float mimicPingCooldownSeconds = 18f;

        [SerializeField, Min(1f), Tooltip("Player distance at which the false beacon vanishes and the predator commits to attack.")]
        private float mimicPingVanishDistanceMeters = 40f;

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
        /// Authoritative authored swim speed for runtime steering.
        /// </summary>
        public float SwimSpeed => math.max(0.01f, swimSpeed);

        /// <summary>
        /// Authoritative authored turn rate for runtime steering.
        /// </summary>
        public float TurnRate => math.max(0.01f, turnRate);

        /// <summary>
        /// Horizontal vision cone angle in degrees used for direct line-of-sight tests.
        /// </summary>
        public float VisionConeAngle => math.clamp(visionConeAngle, 10f, 360f);

        /// <summary>
        /// Authoritative aggro acquisition radius for chase-state transitions.
        /// </summary>
        public float AggroRadius => math.max(0f, aggroRadius);

        /// <summary>
        /// Normalized low-health threshold that biases the fauna into fleeing.
        /// </summary>
        public float FleeHealthThreshold => math.clamp(fleeHealthThreshold, 0.05f, 1f);

        /// <summary>
        /// High-level food-chain band used by ecosystem authoring.
        /// </summary>
        public FaunaFoodChainTier FoodChainTier => foodChainTier;

        /// <summary>
        /// Authored diet bitmask used to filter valid prey.
        /// </summary>
        public uint DietMaskBits => (uint)dietMask;

        /// <summary>
        /// Authored prey identity bitmask used by predators to decide whether this fauna is edible.
        /// </summary>
        public uint PreyMaskBits => (uint)preyMask;

        /// <summary>
        /// Cruise speed exported for runtime steering and descriptor generation.
        /// </summary>
        public float CruiseSpeedMetersPerSecond => math.max(0.01f, cruiseSpeedMetersPerSecond);

        /// <summary>
        /// Maximum speed exported for chase and flee acceleration.
        /// </summary>
        public float MaxSpeedMetersPerSecond => math.max(CruiseSpeedMetersPerSecond, maxSpeedMetersPerSecond);

        /// <summary>
        /// Scalar exported for utility steering response.
        /// </summary>
        public float SteeringResponse => math.max(0.01f, steeringResponse);

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

        public bool CanBurrowAmbush => canBurrowAmbush;

        public float BurrowSeabedTriggerDistanceMeters => math.max(1f, burrowSeabedTriggerDistanceMeters);

        public float BurrowBreachDistanceMeters => math.max(0.5f, burrowBreachDistanceMeters);

        public float BurrowPullAcceleration => math.max(0f, burrowPullAcceleration);

        public float BurrowLockDurationSeconds => math.max(0f, burrowLockDurationSeconds);

        public bool CanDazzleHypnotize => canDazzleHypnotize;

        public float DazzleRangeMeters => math.max(0.5f, dazzleRangeMeters);

        public float DazzleLookDotThreshold => math.clamp(dazzleLookDotThreshold, -1f, 1f);

        public float DazzlePullAcceleration => math.max(0f, dazzlePullAcceleration);

        public FaunaLightReactionMode LightReactionMode => lightReactionMode;

        public float LightReactionRangeMeters => math.max(1f, lightReactionRangeMeters);

        public float LightReactionDotThreshold => math.clamp(lightReactionDotThreshold, -1f, 1f);

        public float LightFrenzySpeedMultiplier => math.max(1f, lightFrenzySpeedMultiplier);

        public float LightReactionFearBoost01 => math.saturate(lightReactionFearBoost01);

        public bool EmitsParentalDefenseSignal => emitsParentalDefenseSignal;

        public bool RespondsToParentalDefenseSignal => respondsToParentalDefenseSignal;

        public float ParentalDefenseRadiusMeters => math.max(1f, parentalDefenseRadiusMeters);

        public float ParentalDefenseHuntDurationSeconds => math.max(0f, parentalDefenseHuntDurationSeconds);

        public float EmpBlindDurationSeconds => math.max(0f, empBlindDurationSeconds);

        public float EmpClaritySuppression01 => math.saturate(empClaritySuppression01);

        /// <summary>
        /// True when this template can publish false distress-beacon sonar returns.
        /// </summary>
        public bool CanEmitMimicDistressPing => canEmitMimicDistressPing || foodChainTier == FaunaFoodChainTier.Leviathan;

        /// <summary>
        /// Radius of the false acoustic ping in authored meters.
        /// </summary>
        public float MimicPingRadiusMeters => math.max(1f, mimicPingRadiusMeters);

        /// <summary>
        /// Normalized false-ping intensity.
        /// </summary>
        public float MimicPingIntensity01 => math.saturate(mimicPingIntensity01);

        /// <summary>
        /// Seconds the false sonar return remains visible before expiring.
        /// </summary>
        public float MimicPingLifetimeSeconds => math.max(0.1f, mimicPingLifetimeSeconds);

        /// <summary>
        /// Minimum seconds between false beacon emissions.
        /// </summary>
        public float MimicPingCooldownSeconds => math.max(0.1f, mimicPingCooldownSeconds);

        /// <summary>
        /// Player proximity that cancels the beacon and commits the ambush.
        /// </summary>
        public float MimicPingVanishDistanceMeters => math.max(1f, mimicPingVanishDistanceMeters);

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
                ResolveDriveWeight(FaunaDriveChannel.Curiosity, 1f),
                LightReactionMode,
                LightReactionRangeMeters,
                LightReactionDotThreshold,
                LightFrenzySpeedMultiplier,
                LightReactionFearBoost01);
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

        /// <summary>
        /// Returns true when this fauna can consume the supplied prey classification bitmask.
        /// </summary>
        public bool CanConsumePrey(uint candidatePreyMaskBits)
        {
            uint dietBits = (uint)dietMask;
            return dietBits != 0u &&
                   candidatePreyMaskBits != 0u &&
                   (dietBits & candidatePreyMaskBits) != 0u;
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
            swimSpeed = math.max(0.01f, swimSpeed);
            turnRate = math.max(0.01f, turnRate);
            visionConeAngle = math.clamp(visionConeAngle, 10f, 360f);
            aggroRadius = math.max(0f, aggroRadius);
            fleeHealthThreshold = math.clamp(fleeHealthThreshold, 0.05f, 1f);
            burrowSeabedTriggerDistanceMeters = math.max(1f, burrowSeabedTriggerDistanceMeters);
            burrowBreachDistanceMeters = math.max(0.5f, burrowBreachDistanceMeters);
            burrowPullAcceleration = math.max(0f, burrowPullAcceleration);
            burrowLockDurationSeconds = math.max(0f, burrowLockDurationSeconds);
            dazzleRangeMeters = math.max(0.5f, dazzleRangeMeters);
            dazzleLookDotThreshold = math.clamp(dazzleLookDotThreshold, -1f, 1f);
            dazzlePullAcceleration = math.max(0f, dazzlePullAcceleration);
            lightReactionRangeMeters = math.max(1f, lightReactionRangeMeters);
            lightReactionDotThreshold = math.clamp(lightReactionDotThreshold, -1f, 1f);
            lightFrenzySpeedMultiplier = math.max(1f, lightFrenzySpeedMultiplier);
            lightReactionFearBoost01 = math.saturate(lightReactionFearBoost01);
            parentalDefenseRadiusMeters = math.max(1f, parentalDefenseRadiusMeters);
            parentalDefenseHuntDurationSeconds = math.max(0f, parentalDefenseHuntDurationSeconds);
            empBlindDurationSeconds = math.max(0f, empBlindDurationSeconds);
            empClaritySuppression01 = math.saturate(empClaritySuppression01);
            mimicPingRadiusMeters = math.max(1f, mimicPingRadiusMeters);
            mimicPingIntensity01 = math.saturate(mimicPingIntensity01);
            mimicPingLifetimeSeconds = math.max(0.1f, mimicPingLifetimeSeconds);
            mimicPingCooldownSeconds = math.max(0.1f, mimicPingCooldownSeconds);
            mimicPingVanishDistanceMeters = math.max(1f, mimicPingVanishDistanceMeters);

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
