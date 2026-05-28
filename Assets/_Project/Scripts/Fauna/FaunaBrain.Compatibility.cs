using System;
using System.Runtime.InteropServices;
using Hecton8.AI.Sensory;
using Hecton8.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    public partial class FaunaBrain
    {
        internal static HectonMapMagicVegetationBridge s_compatibilityVegetationBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCompatibilityStaticState()
        {
            s_compatibilityVegetationBridge = null;
        }

        private CreatureArchetypeData _archetype;
        private Vector3 _spawnPoint;
        private float _currentHealth = 1f;
        private float _maxHealth = 1f;

        public float CurrentHealth => _isDead ? 0f : _currentHealth;
        public float MaxHealth => _maxHealth;
        public float HealthNormalized => _maxHealth > 0.001f ? CurrentHealth / _maxHealth : 0f;
        public bool IsDead => _isDead || _currentHealth <= 0.001f;
        public bool IsSleeping => _foveatedSimulationTier == FoveatedSimulationTier.Frozen || _sensorSuite.isSleeping;
        public bool UsesPackHuntBehavior => _archetype != null && _archetype.usePackHunt;
        public bool UsesFeintRushBehavior => _archetype != null && _archetype.useFeintRush;
        public LeviathanEncounterType LeviathanEncounter => _archetype != null
            ? _archetype.leviathanEncounterType
            : LeviathanEncounterType.PresenceCircle;

        public void ApplyArchetype(CreatureArchetypeData archetype)
        {
            _archetype = archetype;
            ApplyFaunaDataTemplate(archetype != null && archetype.faunaDataTemplate != null ? archetype.faunaDataTemplate : _faunaDataTemplate);
            _utilityBrain.BindProfile(_speciesProfile, _archetype, _faunaDataTemplate);
            if (archetype == null)
                return;

            isAggressive = archetype.isAggressive;
            canFlee = archetype.canFlee;
            _lootProfileId = archetype.lootProfileId ?? string.Empty;

            _baseMaxHealth = math.max(1f, archetype.maxHealth);
            _maxHealth = _baseMaxHealth;
            _currentHealth = math.clamp(_currentHealth, 0f, _maxHealth);
            if (_currentHealth <= 0.001f)
                _currentHealth = _maxHealth;

            _baseAggroDistance = math.max(0f, archetype.baseAggroDistance);
            _baseDeaggroDistance = math.max(_baseAggroDistance, archetype.baseDeaggroDistance);
            _baseAttackDamage = math.max(0f, archetype.attackDamage);
            _baseCruiseSpeed = math.max(0.1f, archetype.cruiseSpeed);
            _baseBurstSpeed = math.max(_baseCruiseSpeed, archetype.burstSpeed);
            _baseTurnSpeed = math.max(0.1f, archetype.turnSpeed);

            if (_faunaDataTemplate != null)
            {
                _baseAggroDistance = _faunaDataTemplate.AggroRadius;
                _baseDeaggroDistance = math.max(_baseAggroDistance, math.max(archetype.baseDeaggroDistance, _baseAggroDistance * 1.35f));
                _baseCruiseSpeed = _faunaDataTemplate.SwimSpeed;
                _baseBurstSpeed = math.max(_baseCruiseSpeed, _faunaDataTemplate.MaxSpeedMetersPerSecond);
                _baseTurnSpeed = _faunaDataTemplate.TurnRate;
            }

            _sensorSuite.aggroDistance = _baseAggroDistance;
            _sensorSuite.deaggroDistance = _baseDeaggroDistance;
            _sensorSuite.sleepDistance = math.max(1f, archetype.sleepDistance);
            if (_faunaDataTemplate != null)
                _sensorSuite.visionConeAngle = _faunaDataTemplate.VisionConeAngle;
            _sensorSuite.reactToPlayerNoise = archetype.reactToPlayerNoise;
            _sensorSuite.reactToPlayerLight = archetype.reactToPlayerLight;

            _stateMachine.escapeDistance = math.max(0f, archetype.baseEscapeDistance);
            _stateMachine.escapeSafeDistance = math.max(_stateMachine.escapeDistance, archetype.baseEscapeSafeDistance);
            _stateMachine.stalkDuration = math.max(0f, archetype.stalkDuration);
            _stateMachine.stalkRadius = math.max(1f, archetype.stalkDistance);
            _stateMachine.useTerritory = archetype.useHomeTerritory;
            _stateMachine.patrolRadius = math.max(1f, archetype.homeReturnDistance);
            _stateMachine.wanderRadius = archetype.useHomeTerritory
                ? math.max(1f, archetype.homeWanderRadius)
                : math.max(1f, _stateMachine.wanderRadius);

            _steeringEngine.moveSpeed = _baseCruiseSpeed;
            _steeringEngine.maxSpeed = _baseBurstSpeed;
            _steeringEngine.turnSpeed = _baseTurnSpeed;
            _steeringEngine.rotationSpeed = _baseTurnSpeed;
            _steeringEngine.swimForce = math.max(_baseCruiseSpeed, _baseBurstSpeed);
            ApplyRuntimeEcosystemOverlays();
            ApplyPassiveRigidbodyCastrationIfRequired();
        }

        public void SetSpawnPoint(Vector3 spawnPoint)
        {
            _spawnPoint = spawnPoint;
            if (TryResolveAupFromRuntimeOrigin(spawnPoint, out AbsoluteUniversePosition spawnAup))
                ApplyAupPresentationPosition(in spawnAup);

            _utilityBrain.SetSpawnAnchor(spawnPoint);
        }

        public void ForceState(FaunaBrain.AIState state)
        {
            _stateMachine.currentState = state;
            _utilityBrain.ApplyExternalState(state, _cognitionTimeSeconds);
        }
    }

    /// <summary>
    /// Bitmask-driven utility evaluator used by large predator cognition.
    /// </summary>
    [Flags]
    public enum PredatorUtilityState : byte
    {
        None = 0x00,
        Prowling = 0x01,
        Stalking = 0x02,
        Attacking = 0x04,
        Fleeing = 0x08,
    }

    /// <summary>
    /// Immutable sensory snapshot consumed by <see cref="CreatureUtilityBrain"/>.
    /// This is the managed-to-native bridge payload for all fauna roles.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    public readonly struct CreatureUtilityContext
    {
        private const ushort CanFleeMask = 1 << 0;
        private const ushort HasVisualContactMask = 1 << 1;
        private const ushort HasPlayerTargetMask = 1 << 2;
        private const ushort HasThreatTargetMask = 1 << 3;
        private const ushort HasApexRivalTargetMask = 1 << 4;
        private const ushort HasPreyTargetMask = 1 << 5;
        private const ushort HasScavengeTargetMask = 1 << 6;
        private const ushort UseHomeTerritoryMask = 1 << 7;
        private const ushort IsFlockingMask = 1 << 8;
        private const ushort HasScatterDirectionMask = 1 << 9;
        private const ushort IsAggressiveMask = 1 << 10;
        private const ushort IsApexPredatorMask = 1 << 11;
        private const ushort UseAlphaLeviathanCognitionMask = 1 << 12;

        public CreatureUtilityContext(
            Vector3 selfPosition,
            Vector3 selfVelocity,
            Vector3 selfForward,
            Vector3 playerPosition,
            Vector3 playerForward,
            Vector3 playerVelocity,
            Vector3 threatPosition,
            Vector3 apexRivalPosition,
            Vector3 preyPosition,
            Vector3 scavengePosition,
            Vector3 flockCenter,
            Vector3 flockDirection,
            Vector3 flockAvoidance,
            Vector3 scatterDirection,
            float healthNormalized,
            float distanceToPlayerSqr,
            float attackRange,
            float fogEndDistanceMeters,
            float baseMaxSpeedMetersPerSecond,
            float fearPressure01,
            float fleeHealthThreshold,
            float escapeDistance,
            float escapeSafeDistance,
            float wanderRadius,
            float patrolRadius,
            float apexTerritoryRadius,
            float apexAggressionMultiplier,
            float playerLightExposure01,
            float foveatedImportanceScore,
            int flockCount,
            bool canFlee,
            bool hasVisualContact,
            bool hasPlayerTarget,
            bool hasThreatTarget,
            bool hasApexRivalTarget,
            bool hasPreyTarget,
            bool hasScavengeTarget,
            bool useHomeTerritory,
            bool isFlocking,
            bool hasScatterDirection,
            bool isAggressive,
            bool isApexPredator,
            bool useAlphaLeviathanCognition)
        {
            SelfPosition = selfPosition;
            SelfVelocity = selfVelocity;
            SelfForward = selfForward;
            PlayerPosition = playerPosition;
            PlayerForward = playerForward;
            PlayerVelocity = playerVelocity;
            ThreatPosition = threatPosition;
            ApexRivalPosition = apexRivalPosition;
            PreyPosition = preyPosition;
            ScavengePosition = scavengePosition;
            FlockCenter = flockCenter;
            FlockDirection = flockDirection;
            FlockAvoidance = flockAvoidance;
            ScatterDirection = scatterDirection;
            HealthNormalized = healthNormalized;
            DistanceToPlayerSqr = distanceToPlayerSqr;
            AttackRange = attackRange;
            FogEndDistanceMeters = math.max(1f, fogEndDistanceMeters);
            BaseMaxSpeedMetersPerSecond = math.max(0.1f, baseMaxSpeedMetersPerSecond);
            FearPressure01 = fearPressure01;
            FleeHealthThreshold = fleeHealthThreshold;
            EscapeDistance = escapeDistance;
            EscapeSafeDistance = escapeSafeDistance;
            WanderRadius = wanderRadius;
            PatrolRadius = patrolRadius;
            ApexTerritoryRadius = apexTerritoryRadius;
            ApexAggressionMultiplier = apexAggressionMultiplier;
            PlayerLightExposure01 = math.saturate(playerLightExposure01);
            FoveatedImportanceScore = foveatedImportanceScore;
            FlockCount = flockCount;
            Flags = PackFlags(
                canFlee,
                hasVisualContact,
                hasPlayerTarget,
                hasThreatTarget,
                hasApexRivalTarget,
                hasPreyTarget,
                hasScavengeTarget,
                useHomeTerritory,
                isFlocking,
                hasScatterDirection,
                isAggressive,
                isApexPredator,
                useAlphaLeviathanCognition);
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
        }

        [FieldOffset(0)] public readonly Vector3 SelfPosition;
        [FieldOffset(12)] public readonly Vector3 SelfVelocity;
        [FieldOffset(24)] public readonly Vector3 SelfForward;
        [FieldOffset(36)] public readonly Vector3 PlayerPosition;
        [FieldOffset(48)] public readonly Vector3 PlayerForward;
        [FieldOffset(60)] public readonly Vector3 PlayerVelocity;
        [FieldOffset(72)] public readonly Vector3 ThreatPosition;
        [FieldOffset(84)] public readonly Vector3 ApexRivalPosition;
        [FieldOffset(96)] public readonly Vector3 PreyPosition;
        [FieldOffset(108)] public readonly Vector3 ScavengePosition;
        [FieldOffset(120)] public readonly Vector3 FlockCenter;
        [FieldOffset(132)] public readonly Vector3 FlockDirection;
        [FieldOffset(144)] public readonly Vector3 FlockAvoidance;
        [FieldOffset(156)] public readonly Vector3 ScatterDirection;
        [FieldOffset(168)] public readonly float HealthNormalized;
        [FieldOffset(172)] public readonly float DistanceToPlayerSqr;
        [FieldOffset(176)] public readonly float AttackRange;
        [FieldOffset(180)] public readonly float FogEndDistanceMeters;
        [FieldOffset(184)] public readonly float BaseMaxSpeedMetersPerSecond;
        [FieldOffset(188)] public readonly float FearPressure01;
        [FieldOffset(192)] public readonly float FleeHealthThreshold;
        [FieldOffset(196)] public readonly float EscapeDistance;
        [FieldOffset(200)] public readonly float EscapeSafeDistance;
        [FieldOffset(204)] public readonly float WanderRadius;
        [FieldOffset(208)] public readonly float PatrolRadius;
        [FieldOffset(212)] public readonly float ApexTerritoryRadius;
        [FieldOffset(216)] public readonly float ApexAggressionMultiplier;
        [FieldOffset(220)] public readonly float PlayerLightExposure01;
        [FieldOffset(224)] public readonly float FoveatedImportanceScore;
        [FieldOffset(228)] public readonly int FlockCount;
        [FieldOffset(232)] public readonly ushort Flags;
        [FieldOffset(234)] private readonly ushort _pad0;
        [FieldOffset(236)] private readonly uint _pad1;
        [FieldOffset(240)] private readonly ulong _pad2;
        [FieldOffset(248)] private readonly ulong _pad3;

        public static bool CanFlee(in CreatureUtilityContext context) => HasFlag(in context, CanFleeMask);
        public static bool HasVisualContact(in CreatureUtilityContext context) => HasFlag(in context, HasVisualContactMask);
        public static bool HasPlayerTarget(in CreatureUtilityContext context) => HasFlag(in context, HasPlayerTargetMask);
        public static bool HasThreatTarget(in CreatureUtilityContext context) => HasFlag(in context, HasThreatTargetMask);
        public static bool HasApexRivalTarget(in CreatureUtilityContext context) => HasFlag(in context, HasApexRivalTargetMask);
        public static bool HasPreyTarget(in CreatureUtilityContext context) => HasFlag(in context, HasPreyTargetMask);
        public static bool HasScavengeTarget(in CreatureUtilityContext context) => HasFlag(in context, HasScavengeTargetMask);
        public static bool UseHomeTerritory(in CreatureUtilityContext context) => HasFlag(in context, UseHomeTerritoryMask);
        public static bool IsFlocking(in CreatureUtilityContext context) => HasFlag(in context, IsFlockingMask);
        public static bool HasScatterDirection(in CreatureUtilityContext context) => HasFlag(in context, HasScatterDirectionMask);
        public static bool IsAggressive(in CreatureUtilityContext context) => HasFlag(in context, IsAggressiveMask);
        public static bool IsApexPredator(in CreatureUtilityContext context) => HasFlag(in context, IsApexPredatorMask);
        public static bool UseAlphaLeviathanCognition(in CreatureUtilityContext context) => HasFlag(in context, UseAlphaLeviathanCognitionMask);

        private static bool HasFlag(in CreatureUtilityContext context, ushort mask)
        {
            return (context.Flags & mask) != 0;
        }

        private static ushort PackFlags(
            bool canFlee,
            bool hasVisualContact,
            bool hasPlayerTarget,
            bool hasThreatTarget,
            bool hasApexRivalTarget,
            bool hasPreyTarget,
            bool hasScavengeTarget,
            bool useHomeTerritory,
            bool isFlocking,
            bool hasScatterDirection,
            bool isAggressive,
            bool isApexPredator,
            bool useAlphaLeviathanCognition)
        {
            ushort flags = 0;
            flags |= canFlee ? CanFleeMask : (ushort)0;
            flags |= hasVisualContact ? HasVisualContactMask : (ushort)0;
            flags |= hasPlayerTarget ? HasPlayerTargetMask : (ushort)0;
            flags |= hasThreatTarget ? HasThreatTargetMask : (ushort)0;
            flags |= hasApexRivalTarget ? HasApexRivalTargetMask : (ushort)0;
            flags |= hasPreyTarget ? HasPreyTargetMask : (ushort)0;
            flags |= hasScavengeTarget ? HasScavengeTargetMask : (ushort)0;
            flags |= useHomeTerritory ? UseHomeTerritoryMask : (ushort)0;
            flags |= isFlocking ? IsFlockingMask : (ushort)0;
            flags |= hasScatterDirection ? HasScatterDirectionMask : (ushort)0;
            flags |= isAggressive ? IsAggressiveMask : (ushort)0;
            flags |= isApexPredator ? IsApexPredatorMask : (ushort)0;
            flags |= useAlphaLeviathanCognition ? UseAlphaLeviathanCognitionMask : (ushort)0;
            return flags;
        }
    }

    /// <summary>
    /// Value-type utility evaluation result used by the fauna brain tick loop.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public readonly struct CreatureUtilityEvaluation
    {
        private const ushort ShouldAttackMask = 1 << 0;
        private const ushort EmitThreatPulseMask = 1 << 1;
        private const ushort FlankingManeuverDetectedMask = 1 << 2;
        private const ushort HasAcousticHeadLookMask = 1 << 3;

        public CreatureUtilityEvaluation(
            Vector3 desiredDirection,
            PredatorUtilityState stateMask,
            FaunaBrain.AIState legacyState,
            float hungerScore,
            float aggressionScore,
            float fearScore,
            float forceMultiplier,
            float speedMultiplier,
            float turnMultiplier,
            bool shouldAttack,
            bool emitThreatPulse,
            int packRoleCode,
            bool flankingManeuverDetected,
            bool hasAcousticHeadLook,
            Vector3 acousticHeadLookTarget,
            float acousticHeadLookWeight)
        {
            DesiredDirection = desiredDirection;
            StateMask = stateMask;
            LegacyState = legacyState;
            HungerScore = hungerScore;
            AggressionScore = aggressionScore;
            FearScore = fearScore;
            ForceMultiplier = forceMultiplier;
            SpeedMultiplier = speedMultiplier;
            TurnMultiplier = turnMultiplier;
            PackRoleCode = packRoleCode;
            AcousticHeadLookTarget = acousticHeadLookTarget;
            AcousticHeadLookWeight = acousticHeadLookWeight;
            Flags = PackFlags(shouldAttack, emitThreatPulse, flankingManeuverDetected, hasAcousticHeadLook);
            _padByte = 0;
            _pad0 = 0;
            _pad1 = 0;
        }

        [FieldOffset(0)] public readonly Vector3 DesiredDirection;
        [FieldOffset(12)] public readonly Vector3 AcousticHeadLookTarget;
        [FieldOffset(24)] public readonly float HungerScore;
        [FieldOffset(28)] public readonly float AggressionScore;
        [FieldOffset(32)] public readonly float FearScore;
        [FieldOffset(36)] public readonly float ForceMultiplier;
        [FieldOffset(40)] public readonly float SpeedMultiplier;
        [FieldOffset(44)] public readonly float TurnMultiplier;
        [FieldOffset(48)] public readonly float AcousticHeadLookWeight;
        [FieldOffset(52)] public readonly int PackRoleCode;
        [FieldOffset(56)] public readonly FaunaBrain.AIState LegacyState;
        [FieldOffset(60)] public readonly ushort Flags;
        [FieldOffset(62)] public readonly PredatorUtilityState StateMask;
        [FieldOffset(63)] private readonly byte _padByte;
        [FieldOffset(64)] private readonly ulong _pad0;
        [FieldOffset(72)] private readonly ulong _pad1;

        public static bool ShouldAttack(in CreatureUtilityEvaluation evaluation) => HasFlag(in evaluation, ShouldAttackMask);
        public static bool EmitThreatPulse(in CreatureUtilityEvaluation evaluation) => HasFlag(in evaluation, EmitThreatPulseMask);
        public static bool FlankingManeuverDetected(in CreatureUtilityEvaluation evaluation) => HasFlag(in evaluation, FlankingManeuverDetectedMask);
        public static bool HasAcousticHeadLook(in CreatureUtilityEvaluation evaluation) => HasFlag(in evaluation, HasAcousticHeadLookMask);

        private static bool HasFlag(in CreatureUtilityEvaluation evaluation, ushort mask)
        {
            return (evaluation.Flags & mask) != 0;
        }

        private static ushort PackFlags(
            bool shouldAttack,
            bool emitThreatPulse,
            bool flankingManeuverDetected,
            bool hasAcousticHeadLook)
        {
            ushort flags = 0;
            flags |= shouldAttack ? ShouldAttackMask : (ushort)0;
            flags |= emitThreatPulse ? EmitThreatPulseMask : (ushort)0;
            flags |= flankingManeuverDetected ? FlankingManeuverDetectedMask : (ushort)0;
            flags |= hasAcousticHeadLook ? HasAcousticHeadLookMask : (ushort)0;
            return flags;
        }
    }

    /// <summary>
    /// Shared fauna cognition bridge. Authoring stays on the managed side; runtime decision state lives in PredatorCognitionDomain.
    /// </summary>
    public struct CreatureUtilityBrain
    {
        private const float MetabolicTickIntervalSeconds = 5f;
        private const float PredatorAcousticSightRadiusMeters = 50f;
        private const float PredatorAcousticSightRadiusMetersSqr =
            PredatorAcousticSightRadiusMeters * PredatorAcousticSightRadiusMeters;
        private const float PredatorAcousticSightThreshold01 = 0.12f;
        private const float PlayerNoiseReferenceSpeedSqr = 72.25f;
        private CreatureArchetypeData _archetype;
        private FaunaSpeciesProfile _speciesProfile;
        private FaunaDataTemplate _dataTemplate;
        private int _slot;
        private byte _initialized;
        private float _metabolicTickAccumulator;
        private int _lastConsumedAcousticPingSignalSequence;

        public PredatorUtilityState CurrentStateMask;
        public byte UsesPredatorRole;
        public byte IsActivePredator;
        public float HungerScore;
        public float AggressionScore;
        public float FearScore;
        public byte IsRegistered;

        public static int ResolveSlot(in CreatureUtilityBrain brain)
        {
            return brain._initialized != 0 ? brain._slot : -1;
        }

        public static float ResolveCurrentHunger01(in CreatureUtilityBrain brain)
        {
            return brain._initialized != 0 ? PredatorCognitionDomain.GetHunger01(brain._slot) : brain.HungerScore;
        }

        public void Initialize(Vector3 spawnAnchor, FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype, FaunaDataTemplate dataTemplate)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
            _dataTemplate = dataTemplate;
            if (_initialized == 0)
                _slot = PredatorCognitionDomain.Register();

            _initialized = _slot >= 0 ? (byte)1 : (byte)0;
            RefreshCachedFlags();
            if (_initialized != 0)
            {
                RegisterSpeciesTuning();
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
            }
        }

        public void BindProfile(FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype, FaunaDataTemplate dataTemplate)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
            _dataTemplate = dataTemplate;
            RefreshCachedFlags();
            RegisterSpeciesTuning();
        }

        public void SetSpawnAnchor(Vector3 spawnAnchor)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.SetSpawnAnchor(_slot, spawnAnchor);
        }

        public void SetRuntimeActive(bool active)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.SetSlotActive(_slot, active);
        }

        public void ResetRuntimeState(Vector3 spawnAnchor)
        {
            CurrentStateMask = PredatorUtilityState.None;
            HungerScore = 0f;
            AggressionScore = 0f;
            FearScore = 0f;
            _metabolicTickAccumulator = 0f;
            _lastConsumedAcousticPingSignalSequence = 0;
            if (_initialized != 0)
            {
                RegisterSpeciesTuning();
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
            }
        }

        public void RecordAuditoryStimulus(Vector3 worldPosition, float timeStamp)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.RecordStimulus(_slot, worldPosition, timeStamp, 1f, CognitionStimulusType.Acoustic);
        }

        public void ApplyExternalState(FaunaBrain.AIState state, float currentTime)
        {
            if (_initialized == 0)
                return;

            switch (state)
            {
                case FaunaBrain.AIState.Aggressive:
                case FaunaBrain.AIState.Starving:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Attacking, currentTime);
                    break;
                case FaunaBrain.AIState.ApexForcedRetreat:
                case FaunaBrain.AIState.Retreat:
                case FaunaBrain.AIState.Escape:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Fleeing, currentTime);
                    break;
                case FaunaBrain.AIState.Sated:
                    PredatorCognitionDomain.ForceSated(_slot, currentTime, 4f);
                    break;
                default:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Prowling, currentTime);
                    break;
            }
        }

        public void ForceRetreat(Vector3 threatPosition, float currentTime, float duration)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.ForceRetreat(_slot, threatPosition, currentTime, duration);
        }

        public void ForceSated(float currentTime, float duration)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.ForceSated(_slot, currentTime, duration);
        }

        public void ApplyFatigueRelief(float amount)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.ReduceFatigue(_slot, amount);
        }

        public void SetHunger01(float hunger01)
        {
            float clampedHunger = math.saturate(hunger01);
            HungerScore = clampedHunger;
            if (_initialized != 0)
                PredatorCognitionDomain.SetHunger01(_slot, clampedHunger);
        }

        public void NotifyAttackPerformed(float currentTime, float cooldownSeconds)
        {
            if (_initialized != 0)
                PredatorCognitionDomain.NotifyAttackPerformed(_slot, currentTime, cooldownSeconds);
        }

        public CreatureUtilityEvaluation Evaluate(int frameId, float dt, float currentTime, in CreatureUtilityContext context)
        {
            if (_initialized == 0)
                Initialize(context.SelfPosition, _speciesProfile, _archetype, _dataTemplate);

            float dispatcherDeltaTime = math.max(0f, dt);
            _metabolicTickAccumulator += dispatcherDeltaTime;
            float metabolicDeltaTime = 0f;
            if (_metabolicTickAccumulator >= MetabolicTickIntervalSeconds)
            {
                metabolicDeltaTime = _metabolicTickAccumulator;
                _metabolicTickAccumulator = 0f;
            }

            float3 fallbackForward = (float3)context.SelfForward;
            double3 floatingOriginOffset = ResolveCurrentRuntimeOriginOffset();
            float acousticPingStrength01 = 0f;
            float acousticTransmission01 = 0f;
            bool hasNoisePlayerTarget = false;
            Vector3 noisePlayerPosition = default;
            AbsoluteUniversePosition noisePlayerAup = default;
            bool hasNoisePlayerAup = false;
            AcousticEchoHuntResult acousticEchoHunt = default;
            bool hasAcousticEchoBreadcrumb = false;
            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal playerNoise))
            {
                float movement01 = math.saturate(math.max(0f, playerNoise.MovementSpeedSqr) / PlayerNoiseReferenceSpeedSqr);
                float tool01 = math.saturate(playerNoise.ToolUseNoise01);
                float transport01 = math.saturate(playerNoise.TransportBoost01 * math.max(1f, playerNoise.TransportSignature));
                float flashlight01 = NoiseSystem.PlayerNoiseSignal.IsFlashlightOn(in playerNoise) ? 0.2f : 0f;
                acousticPingStrength01 = math.saturate(math.max(movement01, math.max(tool01, transport01)) + flashlight01);
                acousticTransmission01 = math.saturate(playerNoise.AcousticTransmission01);
                if (UsesPredatorRole == 0)
                {
                    noisePlayerPosition = playerNoise.Position;
                    noisePlayerAup = playerNoise.PositionAup;
                    hasNoisePlayerAup = true;
                }
            }

            if (UsesPredatorRole != 0)
            {
                if (TryResolveRuntimeAup(context.SelfPosition, out AbsoluteUniversePosition predatorAup) &&
                    AcousticEchoLocationRuntime.TryUpdatePredatorEcho(
                        frameId,
                        in predatorAup,
                        currentTime,
                        out acousticEchoHunt))
                {
                    noisePlayerPosition = new Vector3(
                        acousticEchoHunt.RuntimePosition.x,
                        acousticEchoHunt.RuntimePosition.y,
                        acousticEchoHunt.RuntimePosition.z);
                    noisePlayerAup = acousticEchoHunt.InvestigateAup;
                    hasNoisePlayerAup = true;
                    acousticPingStrength01 = math.max(acousticPingStrength01, acousticEchoHunt.Intensity01);
                    acousticTransmission01 = math.max(acousticTransmission01, 0.35f);
                    hasNoisePlayerTarget = acousticEchoHunt.Intensity01 >= PredatorAcousticSightThreshold01;
                    hasAcousticEchoBreadcrumb = hasNoisePlayerTarget;
                }
            }

            bool contextHasPlayerTarget = CreatureUtilityContext.HasPlayerTarget(in context);
            Vector3 resolvedPlayerPosition = contextHasPlayerTarget ? context.PlayerPosition : noisePlayerPosition;
            bool hasAnyPlayerTarget = contextHasPlayerTarget || hasNoisePlayerTarget;
            AbsoluteUniversePositionBlit128 playerTargetAup = default;
            bool hasPlayerTargetAup = false;
            if (hasAnyPlayerTarget)
            {
                if (!contextHasPlayerTarget && hasNoisePlayerAup)
                {
                    playerTargetAup = noisePlayerAup.ToAlignedBlit();
                    hasPlayerTargetAup = true;
                }
                else if (TryResolveRuntimeAup(resolvedPlayerPosition, out AbsoluteUniversePosition resolvedPlayerAup))
                {
                    playerTargetAup = resolvedPlayerAup.ToAlignedBlit();
                    hasPlayerTargetAup = true;
                }

                hasAnyPlayerTarget &= hasPlayerTargetAup;
            }

            bool contextHasPreyTarget = CreatureUtilityContext.HasPreyTarget(in context);
            bool contextHasScavengeTarget = CreatureUtilityContext.HasScavengeTarget(in context);
            bool contextHasVisualContact = CreatureUtilityContext.HasVisualContact(in context);
            bool contextIsApexPredator = CreatureUtilityContext.IsApexPredator(in context);
            bool hasPackTarget = _archetype != null && _archetype.usePackHunt && ((hasAnyPlayerTarget && hasPlayerTargetAup) || contextHasPreyTarget);
            Vector3 resolvedPackTargetPosition = hasAnyPlayerTarget
                ? resolvedPlayerPosition
                : context.PreyPosition;
            Vector3 resolvedPackTargetVelocity = hasAnyPlayerTarget
                ? context.PlayerVelocity
                : Vector3.zero;
            AbsoluteUniversePositionBlit128 packTargetAup = default;
            if (hasPackTarget)
            {
                if (!contextHasPlayerTarget && hasNoisePlayerAup && hasAnyPlayerTarget)
                {
                    packTargetAup = noisePlayerAup.ToAlignedBlit();
                }
                else if (TryResolveRuntimeAup(resolvedPackTargetPosition, out AbsoluteUniversePosition resolvedPackAup))
                {
                    packTargetAup = resolvedPackAup.ToAlignedBlit();
                }
                else
                {
                    hasPackTarget = false;
                }
            }

            float chemicalSignal01 = 0f;
            if (contextHasScavengeTarget)
            {
                float scavengeDistanceSq = math.lengthsq((float3)(context.ScavengePosition - context.SelfPosition));
                chemicalSignal01 = math.saturate(1f / (1f + (scavengeDistanceSq / (28f * 28f))));
            }

            ResolveSpeciesGenetics(
                out float chemicalSensitivity,
                out float packCoordinationRadius,
                out float packFlankDistance,
                out float packCommitDistance,
                out float aggressionMultiplier);

            HectonMapMagicVegetationBridge vegetationBridge = FaunaBrain.s_compatibilityVegetationBridge;
            if (vegetationBridge == null || !vegetationBridge.isActiveAndEnabled)
            {
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
                FaunaBrain.s_compatibilityVegetationBridge = vegetationBridge;
            }

            if (vegetationBridge != null && vegetationBridge.HasPermanentThreatEcho(context.SelfPosition))
                chemicalSignal01 = math.max(chemicalSignal01, 0.35f);

            // Frame N consumes outputs produced from the fully submitted inputs of frame N-1.
            CognitionOutput output = PredatorCognitionDomain.GetOutput(_slot, fallbackForward);
            CurrentStateMask = (PredatorUtilityState)output.StateMask;
            HungerScore = output.HungerScore;
            AggressionScore = output.AggressionScore;
            FearScore = output.FearScore;

            CognitionInput input = default;
            input.Position = context.SelfPosition;
            input.Velocity = context.SelfVelocity;
            input.Forward = context.SelfForward;
            input.PlayerPosition = resolvedPlayerPosition;
            input.FloatingOriginOffset = floatingOriginOffset;
            input.PlayerTargetAup = playerTargetAup;
            input.PackTargetAup = packTargetAup;
            input.ThreatPosition = context.ThreatPosition;
            input.RivalApexPosition = context.ApexRivalPosition;
            input.PlayerForward = context.PlayerForward;
            input.PreyPosition = context.PreyPosition;
            input.ScavengePosition = context.ScavengePosition;
            input.PackTargetPosition = resolvedPackTargetPosition;
            input.PackTargetVelocity = resolvedPackTargetVelocity;
            input.FlockCenter = context.FlockCenter;
            input.FlockDirection = context.FlockDirection;
            input.FlockAvoidance = context.FlockAvoidance;
            input.ScatterDirection = context.ScatterDirection;
            input.DistanceToPlayerSqr = math.max(0f, context.DistanceToPlayerSqr);
            input.AttackRange = math.max(1f, context.AttackRange);
            input.HealthNormalized = math.saturate(context.HealthNormalized);
            input.FearPressure01 = math.saturate(context.FearPressure01);
            input.FleeHealthThreshold = math.saturate(context.FleeHealthThreshold);
            input.DeltaTime = dispatcherDeltaTime;
            input.MetabolicDeltaTime = metabolicDeltaTime;
            input.CurrentTime = currentTime;
            input.AcousticPingStrength01 = acousticPingStrength01;
            input.AcousticTransmission01 = acousticTransmission01;
            input.ChemicalSignal01 = chemicalSignal01;
            input.ChemicalSensitivity = chemicalSensitivity;
            SpeciesCognitionTuning tuning = _dataTemplate != null
                ? _dataTemplate.BuildSpeciesCognitionTuning()
                : new SpeciesCognitionTuning(
                    1f,
                    ResolveFearWeight(),
                    1f,
                    FaunaLightReactionMode.None,
                    1f,
                    0.65f,
                    1f,
                    0f);
            input.HungerWeight = tuning.HungerWeight;
            input.ThreatWeight = 1f + (ResolveAggressionWeight(aggressionMultiplier) * 0.45f);
            input.FearWeight = tuning.FearWeight;
            input.CuriosityWeight = tuning.CuriosityWeight;
            input.PlayerLightExposure01 = math.saturate(context.PlayerLightExposure01);
            input.LightReactionMode = (int)tuning.LightReactionMode;
            input.LightFrenzySpeedMultiplier = tuning.LightFrenzySpeedMultiplier;
            input.LightReactionFearBoost01 = tuning.LightReactionFearBoost01;
            input.AggressionWeight = ResolveAttackSpeedWeight(aggressionMultiplier);
            input.EscapeDistance = math.max(0f, context.EscapeDistance);
            input.EscapeSafeDistance = math.max(input.EscapeDistance, context.EscapeSafeDistance);
            input.WanderRadius = math.max(1f, context.WanderRadius);
            input.PatrolRadius = math.max(1f, context.PatrolRadius);
            input.ApexTerritoryRadius = math.max(0f, context.ApexTerritoryRadius);
            input.ApexAggressionMultiplier = math.max(1f, context.ApexAggressionMultiplier);
            input.PackCoordinationRadius = packCoordinationRadius;
            input.PackFlankDistance = packFlankDistance;
            input.PackCommitDistance = packCommitDistance;
            input.FogEndDistanceMeters = math.max(1f, context.FogEndDistanceMeters);
            input.BaseMaxSpeedMetersPerSecond = math.max(0.1f, context.BaseMaxSpeedMetersPerSecond);
            input.ImportanceScore = math.saturate(context.FoveatedImportanceScore);
            input.SpeciesId = ResolveSpeciesId();
            input.ClaimedBoidIndex = -1;
            input.FlockCount = math.max(0, context.FlockCount);
            input.Flags = (int)CognitionInputFlags.Active;
            if (UsesPredatorRole != 0)
                input.Flags |= (int)CognitionInputFlags.PredatorRole;
            if (CreatureUtilityContext.CanFlee(in context))
                input.Flags |= (int)CognitionInputFlags.CanFlee;
            if (hasAnyPlayerTarget)
                input.Flags |= (int)CognitionInputFlags.HasPlayerTarget;
            if (CreatureUtilityContext.HasThreatTarget(in context))
                input.Flags |= (int)CognitionInputFlags.HasThreatTarget;
            if (CreatureUtilityContext.HasApexRivalTarget(in context))
                input.Flags |= (int)CognitionInputFlags.HasApexRivalTarget;
            if (contextHasPreyTarget)
                input.Flags |= (int)CognitionInputFlags.HasPreyTarget;
            if (contextHasScavengeTarget)
                input.Flags |= (int)CognitionInputFlags.HasScavengeTarget;
            if (CreatureUtilityContext.UseHomeTerritory(in context))
                input.Flags |= (int)CognitionInputFlags.UseHomeTerritory;
            if (CreatureUtilityContext.IsFlocking(in context))
                input.Flags |= (int)CognitionInputFlags.IsFlocking;
            if (CreatureUtilityContext.HasScatterDirection(in context))
                input.Flags |= (int)CognitionInputFlags.HasScatterDirection;
            if (CreatureUtilityContext.IsAggressive(in context))
                input.Flags |= (int)CognitionInputFlags.IsAggressive;
            if (contextHasVisualContact)
                input.Flags |= (int)CognitionInputFlags.HasVisualPlayerHint;
            if (contextIsApexPredator)
                input.Flags |= (int)CognitionInputFlags.IsApexPredator;
            if (CreatureUtilityContext.UseAlphaLeviathanCognition(in context))
                input.Flags |= (int)CognitionInputFlags.UseAlphaLeviathanCognition;
            if (UsesApexCognitionSteering(contextIsApexPredator))
                input.Flags |= (int)CognitionInputFlags.ApexSmoothSteering;
            if ((_speciesProfile != null && _speciesProfile.isAmbusher) ||
                (_dataTemplate != null && _dataTemplate.CanBurrowAmbush))
            {
                input.Flags |= (int)CognitionInputFlags.IsAmbusher;
            }
            if (hasPackTarget)
                input.Flags |= (int)CognitionInputFlags.HasPackTarget;

            if (hasAnyPlayerTarget && contextHasVisualContact)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    resolvedPlayerPosition,
                    currentTime,
                    1f,
                    CognitionStimulusType.Visual);
            }

            if (hasAnyPlayerTarget && acousticPingStrength01 > 0.01f)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    resolvedPlayerPosition,
                    currentTime,
                    acousticPingStrength01 * math.max(0.25f, acousticTransmission01),
                    CognitionStimulusType.Acoustic);
            }

            if (contextHasScavengeTarget && chemicalSignal01 > 0.01f)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    context.ScavengePosition,
                    currentTime,
                    chemicalSignal01,
                    CognitionStimulusType.Chemical);
            }

            PredatorCognitionDomain.SubmitInput(_slot, in input);

            Vector3 desiredDirection = new Vector3(output.DesiredDirection.x, output.DesiredDirection.y, output.DesiredDirection.z);
            ResolveAcousticHeadSweepTarget(
                in context,
                in acousticEchoHunt,
                hasAcousticEchoBreadcrumb && !contextHasPlayerTarget,
                out bool hasAcousticHeadLook,
                out Vector3 acousticHeadLookTarget,
                out float acousticHeadLookWeight);
            return new CreatureUtilityEvaluation(
                desiredDirection,
                CurrentStateMask,
                (FaunaBrain.AIState)output.LegacyState,
                HungerScore,
                AggressionScore,
                FearScore,
                output.ForceMultiplier,
                output.SpeedMultiplier,
                output.TurnMultiplier,
                output.ShouldAttack != 0,
                output.EmitThreatPulse != 0,
                output.PackRoleCode,
                output.FlankingManeuverDetected != 0,
                hasAcousticHeadLook,
                acousticHeadLookTarget,
                acousticHeadLookWeight);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static double3 ResolveCurrentRuntimeOriginOffset()
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return double3.zero;

            double3 absoluteOrigin = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteOrigin)) ? absoluteOrigin : double3.zero;
        }

        private static void ResolveAcousticHeadSweepTarget(
            in CreatureUtilityContext context,
            in AcousticEchoHuntResult hunt,
            bool active,
            out bool hasHeadLook,
            out Vector3 target,
            out float weight)
        {
            hasHeadLook = false;
            target = default;
            weight = 0f;
            if (!active || math.abs(hunt.HeadSweep01) <= 0.001f)
                return;

            float3 forward = (float3)context.SelfForward;
            if (!math.all(math.isfinite(forward)) || math.lengthsq(forward) <= 0.0001f)
                forward = new float3(0f, 0f, 1f);
            else
                forward = math.normalize(forward);

            float3 right = math.cross(new float3(0f, 1f, 0f), forward);
            if (!math.all(math.isfinite(right)) || math.lengthsq(right) <= 0.0001f)
                right = new float3(1f, 0f, 0f);
            else
                right = math.normalize(right);

            float sweepMeters = 2.25f + (math.saturate(hunt.Intensity01) * 2.75f);
            float3 sweptTarget = hunt.RuntimePosition + (right * (hunt.HeadSweep01 * sweepMeters));
            if (!math.all(math.isfinite(sweptTarget)))
                return;

            target = new Vector3(sweptTarget.x, sweptTarget.y, sweptTarget.z);
            weight = math.saturate(math.abs(hunt.HeadSweep01) + hunt.Intensity01);
            hasHeadLook = weight > 0.01f;
        }

        private static bool UsesApexCognitionSteering(bool isApexPredator)
        {
            return isApexPredator;
        }

        public void Dispose()
        {
            if (_initialized != 0)
            {
                PredatorCognitionDomain.Unregister(_slot);
                _slot = -1;
            }

            _lastConsumedAcousticPingSignalSequence = 0;
            _initialized = 0;
            RefreshCachedFlags();
        }

        private void RefreshCachedFlags()
        {
            UsesPredatorRole = IsPredatorArchetype(_archetype, _speciesProfile) ? (byte)1 : (byte)0;
            IsActivePredator = UsesPredatorRole;
            IsRegistered = _initialized != 0 && _slot >= 0 ? (byte)1 : (byte)0;
        }

        private static bool IsPredatorArchetype(CreatureArchetypeData archetype, FaunaSpeciesProfile speciesProfile)
        {
            if (archetype != null)
            {
                return archetype.roleType == CreatureRoleType.Hunter ||
                       archetype.roleType == CreatureRoleType.Leviathan ||
                       archetype.isAggressive;
            }

            return speciesProfile != null && speciesProfile.isLeviathan;
        }

        private void ResolveSpeciesGenetics(
            out float chemicalSensitivity,
            out float packCoordinationRadius,
            out float packFlankDistance,
            out float packCommitDistance,
            out float aggressionMultiplier)
        {
            chemicalSensitivity = 1f;
            packCoordinationRadius = _archetype != null ? math.max(0f, _archetype.packSupportRadius) : 0f;
            packFlankDistance = _archetype != null ? math.max(0f, _archetype.packFlankDistance) : 0f;
            packCommitDistance = _archetype != null ? math.max(0f, _archetype.packCommitDistance) : 0f;
            aggressionMultiplier = 1f;

            CreatureGeneticsProfile geneticsProfile = _speciesProfile != null ? _speciesProfile.geneticsProfile : null;
            if (geneticsProfile == null || !geneticsProfile.TryResolveSpeciesTuning(ResolveSpeciesId(), out CreatureGeneticsProfile.SpeciesGeneticsTuning tuning))
                return;

            if (tuning.scentSensitivity > 0f)
                chemicalSensitivity = tuning.scentSensitivity;

            if (tuning.packHuntingRadius > 0f)
                packCoordinationRadius = tuning.packHuntingRadius;

            if (tuning.packFlankOffset > 0f)
                packFlankDistance = tuning.packFlankOffset;

            if (tuning.baseAggressionMultiplier > 0f)
                aggressionMultiplier = tuning.baseAggressionMultiplier;
        }

        private float ResolveAggressionWeight(float aggressionMultiplier)
        {
            float aggression = _speciesProfile != null ? math.max(0.1f, _speciesProfile.baseAggro) : 0.55f;
            if (_archetype == null)
                return aggression * math.max(0f, aggressionMultiplier);

            switch (_archetype.roleType)
            {
                case CreatureRoleType.Leviathan:
                    aggression *= 1.45f;
                    break;
                case CreatureRoleType.Hunter:
                    aggression *= 1.2f;
                    break;
            }

            if (_archetype.isAggressive)
                aggression *= 1.1f;

            return aggression * math.max(0f, aggressionMultiplier);
        }

        private float ResolveFearWeight()
        {
            if (_dataTemplate != null)
                return _dataTemplate.ResolveDriveWeight(FaunaDriveChannel.Fear, 1.5f);

            if (_speciesProfile == null)
                return 1.5f;

            return math.max(_speciesProfile.retreatSpeedMultiplier, _speciesProfile.escapeSpeedMultiplier);
        }

        private float ResolveAttackSpeedWeight(float aggressionMultiplier)
        {
            if (_speciesProfile == null)
                return 1.35f * math.max(0f, aggressionMultiplier);

            return math.max(1.15f, _speciesProfile.aggressiveSpeedMultiplier * math.max(0f, aggressionMultiplier));
        }

        private int ResolveSpeciesId()
        {
            if (_dataTemplate != null && _dataTemplate.SpeciesId != 0)
                return _dataTemplate.SpeciesId;

            if (_speciesProfile != null && _speciesProfile.speciesID != 0)
                return _speciesProfile.speciesID;

            if (_archetype != null && !string.IsNullOrWhiteSpace(_archetype.creatureId))
                return unchecked((int)Hecton.Localization.LocHash.Compute(_archetype.creatureId)) & int.MaxValue;

            return 0;
        }

        private void RegisterSpeciesTuning()
        {
            if (_dataTemplate == null)
                return;

            int speciesId = ResolveSpeciesId();
            if (speciesId == 0)
                return;

            PredatorCognitionDomain.RegisterSpeciesTuning(speciesId, _dataTemplate.BuildSpeciesCognitionTuning());
        }
    }
}
