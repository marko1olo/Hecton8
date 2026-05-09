using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Scavenging;
using Hecton.Localization;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class KnifeTool : PlayerTool
    {
        private const string KnifeCategory = "KNIFE";
        private const string GenericBladeTargetLabel = "FIELD TARGET";
        private const string GenericBioformLabel = "BIOFORM";
        private const string GenericResourceNodeLabel = "RESOURCE NODE";
        private const string GenericBaseModuleLabel = "BASE MODULE";
        private const byte TemplateStringArg0 = 0x01;
        private const byte TemplateStringArg1 = 0x02;
        private const byte TemplateFloatArg0 = 0x04;
        private const byte TemplateFloatArg1 = 0x08;

        private readonly struct KnifeAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public KnifeAssessment(string headline, string summary, string recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                return AppendText(ref buffer, Headline) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Summary) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Recommendation);
            }
        }

        [Header("Melee")]
        [SerializeField] private float range = 2.15f;
        [SerializeField] private float radius = 0.28f;
        [SerializeField] private float damage = 32f;
        [SerializeField] private float impulse = 4f;
        [SerializeField] private float swingCooldown = 0.35f;
        [SerializeField] private float precisionStrikeMultiplier = 1.65f;
        [SerializeField] private float criticalHealthThreshold = 0.35f;
        [SerializeField] private LayerMask hitMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        private Transform _cachedTransform;
        private float _cooldown;
        private float _nextFeedbackAt;
        private int _cachedHitFrame = -1;
        private bool _cachedHitValid;
        private Collider _cachedHitCollider;
        private Vector3 _cachedHitPoint;
        private float _cachedHitDistance;
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — survival blade HUD staging buffer — owner: KnifeTool

        private static FixedCharBuffer s_logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — survival blade field log staging buffer — owner: KnifeTool
        private static FixedCharBuffer s_legacySummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - survival blade legacy summary/directive bridge - owner: KnifeTool
        private static FixedCharBuffer s_assessmentTextBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — survival blade assessment staging buffer — owner: KnifeTool

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                HitBuffer,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore);

            Collider bestCollider = null;
            Vector3 bestPoint = origin + direction * range;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = HitBuffer[i].collider;
                if (candidate == null || candidate.transform == _cachedTransform || candidate.transform.IsChildOf(_cachedTransform))
                    continue;

                if (HitBuffer[i].distance < bestDistance)
                {
                    bestDistance = HitBuffer[i].distance;
                    bestCollider = candidate;
                    bestPoint = HitBuffer[i].point;
                }
            }

            if (bestCollider != null)
            {
                float effectiveDamage = damage * GetEfficiency();
                bool applied = ToolHitUtility.ApplyDamage(bestCollider, effectiveDamage, bestPoint, direction, impulse);
                if (applied && Time.time >= _nextFeedbackAt)
                {
                    PublishInfoMessage(ResolveLocalized(LocalizationKeys.KNIFE_HUD_CONTACT, "SURVIVAL BLADE - CONTACT"));
                    RecordContactLog(bestDistance);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.KNIFE_HUD_NO_CONTACT, "SURVIVAL BLADE - NO CONTACT"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_CLEAR_TITLE, "MELEE SWING RETURNED CLEAR"),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_CLEAR_MESSAGE, "No valid target entered the blade envelope during the last swing."),
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            for (int i = 0; i < hitCount; i++)
                HitBuffer[i] = default;

            _cooldown = swingCooldown / math.max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - deltaTime);
        }

        public override string GetOperationalSummary()
        {
            s_legacySummaryBuffer.Clear();
            WriteOperationalSummary(ref s_legacySummaryBuffer);
            return CreateLegacyString(in s_legacySummaryBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "SURVIVAL BLADE // RECOVERING ");
                buffer.AppendFloat(_cooldown, 1);
                AppendText(ref buffer, "S");
                return;
            }

            if (TryGetBestHitCached(out _, out _, out float distance))
            {
                AppendText(ref buffer, "SURVIVAL BLADE // CONTACT ");
                buffer.AppendFloat(distance, 1);
                AppendText(ref buffer, "M");
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.KNIFE_OPERATIONAL_READY, "SURVIVAL BLADE // READY"));
        }

        public override string GetOperationalDirective()
        {
            s_legacySummaryBuffer.Clear();
            WriteOperationalDirective(ref s_legacySummaryBuffer);
            return CreateLegacyString(in s_legacySummaryBuffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_RECOVERING, "Reset your stance before the next strike."));
                return;
            }

            if (TryGetBestHitCached(out Collider target, out _, out float distance))
            {
                if (TryBuildAssessment(target, distance, out KnifeAssessment assessment))
                {
                    AppendText(ref buffer, assessment.Recommendation);
                    return;
                }

                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_CONTACT, "Target is inside blade range. Strike or switch tools if the contact is armored."));
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_READY, "Primary swings. Secondary reads the contact before you commit."));
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryFindBestHit(out Collider target, out Vector3 point, out float distance))
            {
                WarnNoContact(ResolveLocalized(LocalizationKeys.KNIFE_HUD_NO_TARGET_READ, "SURVIVAL BLADE - NO TARGET READ"));
                _cooldown = swingCooldown * 0.5f;
                return;
            }

            if (TryPrecisionStrike(target, point, distance))
            {
                _cooldown = swingCooldown / math.max(0.25f, GetSpeed());
                return;
            }

            ShowTacticalReadout(target, distance);
            _cooldown = swingCooldown * 0.5f;
        }

        private bool TryFindBestHit(out Collider bestCollider, out Vector3 bestPoint, out float bestDistance)
        {
            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                HitBuffer,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore);

            bestCollider = null;
            bestPoint = origin + direction * range;
            bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = HitBuffer[i].collider;
                if (candidate == null || candidate.transform == _cachedTransform || candidate.transform.IsChildOf(_cachedTransform))
                    continue;

                if (HitBuffer[i].distance < bestDistance)
                {
                    bestDistance = HitBuffer[i].distance;
                    bestCollider = candidate;
                    bestPoint = HitBuffer[i].point;
                }
            }

            for (int i = 0; i < hitCount; i++)
                HitBuffer[i] = default;

            return bestCollider != null;
        }

        private bool TryGetBestHitCached(out Collider bestCollider, out Vector3 bestPoint, out float bestDistance)
        {
            int currentFrame = Time.frameCount;
            if (_cachedHitFrame == currentFrame)
            {
                bestCollider = _cachedHitCollider;
                bestPoint = _cachedHitPoint;
                bestDistance = _cachedHitDistance;
                return _cachedHitValid;
            }

            bool valid = TryFindBestHit(out bestCollider, out bestPoint, out bestDistance);
            _cachedHitFrame = currentFrame;
            _cachedHitValid = valid;
            _cachedHitCollider = bestCollider;
            _cachedHitPoint = bestPoint;
            _cachedHitDistance = bestDistance;
            return valid;
        }

        private bool TryPrecisionStrike(Collider target, Vector3 point, float distance)
        {
            float normalized = GetTargetNormalizedVital(target);
            if (normalized < 0f || normalized > criticalHealthThreshold)
                return false;

            float effectiveDamage = damage * precisionStrikeMultiplier * GetEfficiency();
            bool applied = ToolHitUtility.ApplyDamage(target, effectiveDamage, point, _cachedTransform.forward, impulse * 1.5f);
            if (!applied)
                return false;

            if (Time.time >= _nextFeedbackAt)
            {
                PublishInfoMessage(ResolveLocalized(LocalizationKeys.KNIFE_HUD_PRECISION_STRIKE, "SURVIVAL BLADE - PRECISION STRIKE"));
                RecordPrecisionLog(distance);
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            return true;
        }

        private void ShowTacticalReadout(Collider target, float distance)
        {
            if (target == null)
                return;

            FaunaBrain ai = ResolveFaunaBrain(target);
            if (ai != null)
            {
                KnifeAssessment assessment = BuildBioformAssessment(ai, distance);
                PublishAssessment(assessment);
                RecordAssessmentLog(assessment);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            ResourceNode node = ResolveResourceNode(target);
            if (node != null)
            {
                KnifeAssessment assessment = BuildResourceAssessment(node, distance);
                PublishAssessment(assessment);
                RecordAssessmentLog(assessment);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            BaseModule module = ResolveBaseModule(target);
            if (module != null)
            {
                KnifeAssessment assessment = BuildModuleAssessment(module, distance);
                PublishAssessment(assessment);
                RecordAssessmentLog(assessment);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (Time.time >= _nextFeedbackAt)
            {
                PublishInfoMessage(ResolveLocalized(LocalizationKeys.KNIFE_HUD_TARGET_PROFILE_UNKNOWN, "SURVIVAL BLADE - TARGET PROFILE UNKNOWN"));
                RecordUnknownProfileLog();
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private float GetTargetNormalizedVital(Collider target)
        {
            if (target == null)
                return -1f;

            FaunaBrain ai = ResolveFaunaBrain(target);
            if (ai != null)
                return ai.HealthNormalized;

            ResourceNode node = ResolveResourceNode(target);
            if (node != null)
                return node.HealthNormalized;

            BaseModule module = ResolveBaseModule(target);
            if (module != null && module.MaxIntegrity > 0f)
                return module.CurrentIntegrity / module.MaxIntegrity;

            return -1f;
        }

        private void WarnNoContact(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            PublishWarningMessage(message);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                message,
                ResolveLocalized(LocalizationKeys.KNIFE_LOG_NO_TARGET_READ_MESSAGE, "No valid target entered the blade envelope during the tactical read."),
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryBuildAssessment(Collider target, float distance, out KnifeAssessment assessment)
        {
            assessment = default;

            FaunaBrain ai = ResolveFaunaBrain(target);
            if (ai != null)
            {
                assessment = BuildBioformAssessment(ai, distance);
                return true;
            }

            ResourceNode node = ResolveResourceNode(target);
            if (node != null)
            {
                assessment = BuildResourceAssessment(node, distance);
                return true;
            }

            BaseModule module = ResolveBaseModule(target);
            if (module != null)
            {
                assessment = BuildModuleAssessment(module, distance);
                return true;
            }

            if (TryBuildDescriptorAssessment(target, distance, out assessment))
                return true;

            return false;
        }

        private static FaunaBrain ResolveFaunaBrain(Collider target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out FaunaBrain ai))
                return ai;

            return target.GetComponentInParent<FaunaBrain>();
        }

        private static ResourceNode ResolveResourceNode(Collider target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out ResourceNode node))
                return node;

            return target.GetComponentInParent<ResourceNode>();
        }

        private static BaseModule ResolveBaseModule(Collider target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out BaseModule module))
                return module;

            return target.GetComponentInParent<BaseModule>();
        }

        private KnifeAssessment BuildBioformAssessment(FaunaBrain ai, float distance)
        {
            float healthPercent = ai.HealthNormalized * 100f;
            if (ai.IsSleeping)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_DORMANT_BIOFORM, "BLADE READ - DORMANT BIOFORM {0:0}%"),
                        healthPercent,
                        0),
                    CreateStringFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_DORMANT_BIOFORM, "{0} is dormant at {1:0.0} m and has not committed to attack."),
                        GenericBioformLabel,
                        distance,
                        1),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_DORMANT_BIOFORM, "Strike only if you need a silent opener or a clean sample window."),
                    "INFO");
            }

            if (ai.CurrentState == FaunaBrain.AIState.Aggressive)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_HOSTILE, "BLADE READ - HOSTILE {0:0}%"),
                        healthPercent,
                        0),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_HOSTILE, "{0} is already aggressive and inside close-quarters danger range."),
                        GenericBioformLabel),
                    ai.HealthNormalized <= criticalHealthThreshold
                        ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_HOSTILE_CRITICAL, "Precision strike is viable, but commit fast.")
                        : ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_HOSTILE, "Use stun or create space before relying on the blade."),
                    "WARN");
            }

            if (ai.CurrentState == FaunaBrain.AIState.Threaten || ai.CurrentState == FaunaBrain.AIState.Stalk || ai.CurrentState == FaunaBrain.AIState.Loom || ai.CurrentState == FaunaBrain.AIState.Feint)
            {
                bool packHunt = ai.UsesPackHuntBehavior && ai.CurrentState == FaunaBrain.AIState.Stalk;
                bool feintCapable = ai.UsesFeintRushBehavior && (ai.CurrentState == FaunaBrain.AIState.Stalk || ai.CurrentState == FaunaBrain.AIState.Loom || ai.CurrentState == FaunaBrain.AIState.Feint);
                bool ambushLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.AmbushBurst;
                bool sentinelLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.SentinelPressure;
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_PRESSURE_CONTACT, "BLADE READ - PRESSURE CONTACT {0:0}%"),
                        healthPercent,
                        0),
                    ai.CurrentState == FaunaBrain.AIState.Feint
                        ? CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_FEINT, "{0} is in a false-charge pass and can swing back into a real hit if you misread the opening."),
                            GenericBioformLabel)
                        :
                    ai.CurrentState == FaunaBrain.AIState.Loom
                        ? (ambushLeviathan
                            ? CreateSingleStringText(
                                ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_AMBUSH, "{0} is setting up a burst ambush and can snap into close range with little warning."),
                                GenericBioformLabel)
                            : (sentinelLeviathan
                                ? CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_SENTINEL, "{0} is holding a guarded route and pushing you out of its corridor."),
                                    GenericBioformLabel)
                                : CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_LOOM, "{0} is holding a heavy pressure circle and can crash into close range fast."),
                                    GenericBioformLabel)))
                        : ai.CurrentState == FaunaBrain.AIState.Threaten
                        ? CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_THREATEN, "{0} is warning and pressuring you around its protected space."),
                            GenericBioformLabel)
                        : (packHunt
                            ? CreateSingleStringText(
                                ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_PACK, "{0} is shadowing you as part of a group hunt and may rush from the flank."),
                                GenericBioformLabel)
                            : (feintCapable
                                ? CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_TRACKING_FEINT, "{0} is shadowing you and may throw a fake entry before the real bite."),
                                    GenericBioformLabel)
                                : CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_TRACKING, "{0} is shadowing you and may commit to attack soon."),
                                    GenericBioformLabel))),
                    ai.CurrentState == FaunaBrain.AIState.Feint
                        ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_FEINT, "Do not knife into the pass. Sidestep the fake run and wait for the turn.")
                        :
                    ai.CurrentState == FaunaBrain.AIState.Loom
                        ? (ambushLeviathan
                            ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_AMBUSH, "Do not trust a knife opener here. Break the angle before it bursts.")
                            : (sentinelLeviathan
                                ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_SENTINEL, "This is a bad knife lane. Leave the corridor or force a hard opening first.")
                                : ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_LOOM, "Do not trust a knife opener here. Break distance first.")))
                        : packHunt
                        ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_PACK, "Do not trust a knife opener here unless you have already broken the group shape.")
                        : (feintCapable
                            ? ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_TRACKING_FEINT, "Do not bite on the fake entry. Hold the blade for the real commit window.")
                            : ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_PRESSURE_TRACKING, "Do not rely on a knife opener here unless you are forcing a close-range break.")),
                    "WARN");
            }

            if (ai.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_FRACTURED_TARGET, "BLADE READ - FRACTURED TARGET {0:0}%"),
                        healthPercent,
                        0),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_FRACTURED_TARGET, "{0} is close to collapse and vulnerable to a finishing strike."),
                        GenericBioformLabel),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_FRACTURED_TARGET, "Go for a precision hit if you need the target down now."),
                    "INFO");
            }

            return new KnifeAssessment(
                CreateSingleFloatText(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_BIOFORM_STABLE, "BLADE READ - BIOFORM STABLE {0:0}%"),
                    healthPercent,
                    0),
                CreateSingleStringText(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_BIOFORM_STABLE, "{0} is alive, mobile, and not yet in an easy finish window."),
                    GenericBioformLabel),
                ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_BIOFORM_STABLE, "Observe, soften it first, or disengage."),
                "INFO");
        }

        private KnifeAssessment BuildResourceAssessment(ResourceNode node, float distance)
        {
            if (node.IsDepleted)
            {
                return new KnifeAssessment(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_DEPLETED, "BLADE READ - NODE DEPLETED 0%"),
                    CreateStringFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_DEPLETED, "{0} is exhausted at {1:0.0} m and will not pay back another strike."),
                        GenericResourceNodeLabel,
                        distance,
                        1),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_DEPLETED, "Leave it and move to a fresh resource lane."),
                    "WARN");
            }

            float nodePercent = node.HealthNormalized * 100f;
            if (node.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_READY, "BLADE READ - NODE READY TO BREAK {0:0}%"),
                        nodePercent,
                        0),
                    CreateStringFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_READY, "{0} is one clean strike away from opening at {1:0.0} m."),
                        GenericResourceNodeLabel,
                        distance,
                        1),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_READY, "Finish it now if you want a fast recovery window."),
                    "INFO");
            }

            if (node.HealthNormalized <= 0.65f)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_WEAKENED, "BLADE READ - NODE WEAKENED {0:0}%"),
                        nodePercent,
                        0),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_WEAKENED, "{0} is partially cracked and reacting to tool pressure."),
                        GenericResourceNodeLabel),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_WEAKENED, "Another strike or a dedicated extraction tool is worthwhile."),
                    "INFO");
            }

            return new KnifeAssessment(
                CreateSingleFloatText(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_DENSE, "BLADE READ - NODE DENSE {0:0}%"),
                    nodePercent,
                    0),
                CreateStringFloatText(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_DENSE, "{0} still has a dense shell at {1:0.0} m."),
                    GenericResourceNodeLabel,
                    distance,
                    1),
                ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_DENSE, "Use repeated strikes only if no better extraction tool is available."),
                "INFO");
        }

        private KnifeAssessment BuildModuleAssessment(BaseModule module, float distance)
        {
            float normalized = module.MaxIntegrity > 0f ? module.CurrentIntegrity / module.MaxIntegrity : 0f;
            if (module.IsBreached)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_BREACHED, "BLADE READ - MODULE BREACHED {0:0}%"),
                        normalized * 100f,
                        0),
                    CreateStringFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_BREACHED, "{0} is already compromised and unsafe at {1:0.0} m."),
                        GenericBaseModuleLabel,
                        distance,
                        1),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_MODULE_BREACHED, "Repair, salvage, or leave it. The blade is not the main tool here."),
                    "WARN");
            }

            if (module.CanDeconstruct())
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_SALVAGEABLE, "BLADE READ - MODULE SALVAGEABLE {0:0}%"),
                        normalized * 100f,
                        0),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_SALVAGEABLE, "{0} exposes reclaim paths, but not for blade work."),
                        GenericBaseModuleLabel),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_MODULE_SALVAGEABLE, "Swap to the cutter if recovery is the goal."),
                    "INFO");
            }

            return new KnifeAssessment(
                CreateSingleFloatText(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_SEALED, "BLADE READ - MODULE SEALED {0:0}%"),
                    normalized * 100f,
                    0),
                CreateSingleStringText(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_SEALED, "{0} is structurally sealed and not a valid blade target."),
                    GenericBaseModuleLabel),
                ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_MODULE_SEALED, "Use repair, builder, or cutter tools instead."),
                "INFO");
        }

        private static bool TryBuildDescriptorAssessment(Collider target, float distance, out KnifeAssessment assessment)
        {
            assessment = default;
            if (!FieldTargetDescriptor.TryResolve(target, out FieldTargetDescriptor descriptor))
                return false;

            if (!FieldTargetSemantics.TryBuildKnifeAssessment(descriptor, distance, out FieldTargetSemantics.SemanticAssessment semantic))
                return false;

            assessment = new KnifeAssessment(
                semantic.Headline,
                semantic.Summary,
                semantic.Recommendation,
                semantic.Severity);
            return true;
        }

        private static void RecordContactLog(float distance)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendStringFloatTemplate(
                    ref s_logSummaryBuffer,
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_CONTACT_MESSAGE, "{0} engaged at {1:0.0} m."),
                    GenericBladeTargetLabel,
                    distance,
                    1))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " engaged at ");
                s_logSummaryBuffer.AppendFloat(distance, 1);
                AppendText(ref s_logSummaryBuffer, " m.");
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                ResolveLocalized(LocalizationKeys.KNIFE_LOG_CONTACT_TITLE, "MELEE CONTACT REGISTERED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        private static void RecordPrecisionLog(float distance)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendStringFloatTemplate(
                    ref s_logSummaryBuffer,
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_PRECISION_MESSAGE, "{0} finished or weakened at {1:0.0} m."),
                    GenericBladeTargetLabel,
                    distance,
                    1))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " finished or weakened at ");
                s_logSummaryBuffer.AppendFloat(distance, 1);
                AppendText(ref s_logSummaryBuffer, " m.");
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                ResolveLocalized(LocalizationKeys.KNIFE_LOG_PRECISION_TITLE, "PRECISION STRIKE CONFIRMED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        private static void RecordAssessmentLog(KnifeAssessment assessment)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendTwoStringTemplate(
                    ref s_logSummaryBuffer,
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_ASSESSMENT, "{0} | {1}"),
                    assessment.Summary,
                    assessment.Recommendation))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, assessment.Summary);
                AppendText(ref s_logSummaryBuffer, " | ");
                AppendText(ref s_logSummaryBuffer, assessment.Recommendation);
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                assessment.Headline,
                in s_logSummaryBuffer,
                assessment.Severity);
        }

        private static void RecordUnknownProfileLog()
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendSingleStringTemplate(
                    ref s_logSummaryBuffer,
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_MESSAGE, "{0} does not expose a tactical vitality profile."),
                    GenericBladeTargetLabel))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " does not expose a tactical vitality profile.");
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                ResolveLocalized(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_TITLE, "UNKNOWN TARGET PROFILE"),
                in s_logSummaryBuffer,
                "WARN");
        }

        private static void PublishAssessment(KnifeAssessment assessment)
        {
            s_hudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref s_hudBuffer))
                return;

            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishInfoMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishWarningMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowWarning(in s_hudBuffer);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static string CreateSingleFloatText(string template, float value, int decimals)
        {
            s_assessmentTextBuffer.Clear();
            if (!TryAppendSingleFloatTemplate(ref s_assessmentTextBuffer, template, value, decimals))
                return template;

            return CreateLegacyString(in s_assessmentTextBuffer);
        }

        private static string CreateSingleStringText(string template, string value)
        {
            s_assessmentTextBuffer.Clear();
            if (!TryAppendSingleStringTemplate(ref s_assessmentTextBuffer, template, value))
                return template;

            return CreateLegacyString(in s_assessmentTextBuffer);
        }

        private static string CreateStringFloatText(string template, string stringValue, float floatValue, int decimals)
        {
            s_assessmentTextBuffer.Clear();
            if (!TryAppendStringFloatTemplate(ref s_assessmentTextBuffer, template, stringValue, floatValue, decimals))
                return template;

            return CreateLegacyString(in s_assessmentTextBuffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool TryAppendSingleStringTemplate(ref FixedCharBuffer buffer, string template, string value)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                value,
                null,
                0f,
                0f,
                0,
                TemplateStringArg0);
        }

        private static bool TryAppendTwoStringTemplate(ref FixedCharBuffer buffer, string template, string value0, string value1)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                value0,
                value1,
                0f,
                0f,
                0,
                TemplateStringArg0 | TemplateStringArg1);
        }

        private static bool TryAppendSingleFloatTemplate(ref FixedCharBuffer buffer, string template, float value, int decimals)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                null,
                null,
                value,
                0f,
                decimals,
                TemplateFloatArg0);
        }

        private static bool TryAppendStringFloatTemplate(ref FixedCharBuffer buffer, string template, string stringValue, float floatValue, int decimals)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                stringValue,
                null,
                0f,
                floatValue,
                decimals,
                TemplateStringArg0 | TemplateFloatArg1);
        }

        private static bool TryAppendKnifeTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            string stringArg1,
            float floatArg0,
            float floatArg1,
            int floatDecimals,
            byte argumentMask)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> span = template.AsSpan();
            bool wroteToken = false;
            int segmentStart = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '{' || i + 1 >= span.Length)
                    continue;

                char token = span[i + 1];
                if (token != '0' && token != '1')
                    continue;

                int closeIndex = i + 2;
                while (closeIndex < span.Length && span[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= span.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(span.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wrote = token switch
                {
                    '0' when (argumentMask & TemplateStringArg0) != 0 => AppendText(ref buffer, stringArg0),
                    '0' when (argumentMask & TemplateFloatArg0) != 0 => buffer.AppendFloat(floatArg0, floatDecimals),
                    '1' when (argumentMask & TemplateStringArg1) != 0 => AppendText(ref buffer, stringArg1),
                    '1' when (argumentMask & TemplateFloatArg1) != 0 => buffer.AppendFloat(floatArg1, floatDecimals),
                    _ => buffer.Append(span.Slice(i, closeIndex - i + 1))
                };

                if (!wrote)
                    return false;

                wroteToken = true;
                i = closeIndex;
                segmentStart = closeIndex + 1;
            }

            if (!wroteToken)
                return buffer.Append(span);

            return segmentStart >= span.Length || buffer.Append(span.Slice(segmentStart));
        }
    }
}

