using Hecton8.AI;
using Hecton8.Scavenging;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class KnifeTool : PlayerTool
    {
        private const string KnifeCategory = "KNIFE";
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

            public string BuildHudMessage()
            {
                return string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_HUD_ASSESSMENT, "{0} | {1} | {2}"),
                    Headline,
                    Summary,
                    Recommendation);
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
                    ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.KNIFE_HUD_CONTACT, "SURVIVAL BLADE - CONTACT"));
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_CONTACT_TITLE, "MELEE CONTACT REGISTERED"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.KNIFE_LOG_CONTACT_MESSAGE, "{0} engaged at {1:0.0} m."),
                            bestCollider.gameObject.name,
                            bestDistance),
                        "INFO");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.KNIFE_HUD_NO_CONTACT, "SURVIVAL BLADE - NO CONTACT"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_CLEAR_TITLE, "MELEE SWING RETURNED CLEAR"),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_CLEAR_MESSAGE, "No valid target entered the blade envelope during the last swing."),
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            for (int i = 0; i < hitCount; i++)
                HitBuffer[i] = default;

            _cooldown = swingCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_OPERATIONAL_RECOVERING, "SURVIVAL BLADE // RECOVERING {0:0.0}S"),
                    _cooldown);

            if (TryGetBestHitCached(out _, out _, out float distance))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_OPERATIONAL_CONTACT, "SURVIVAL BLADE // CONTACT {0:0.0}M"),
                    distance);

            return ResolveLocalized(LocalizationKeys.KNIFE_OPERATIONAL_READY, "SURVIVAL BLADE // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_RECOVERING, "Reset your stance before the next strike.");

            if (TryGetBestHitCached(out Collider target, out _, out float distance))
            {
                if (TryBuildAssessment(target, distance, out KnifeAssessment assessment))
                    return assessment.Recommendation;

                return ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_CONTACT, "Target is inside blade range. Strike or switch tools if the contact is armored.");
            }

            return ResolveLocalized(LocalizationKeys.KNIFE_DIRECTIVE_READY, "Primary swings. Secondary reads the contact before you commit.");
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
                _cooldown = swingCooldown / Mathf.Max(0.25f, GetSpeed());
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
                ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.KNIFE_HUD_PRECISION_STRIKE, "SURVIVAL BLADE - PRECISION STRIKE"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_PRECISION_TITLE, "PRECISION STRIKE CONFIRMED"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_PRECISION_MESSAGE, "{0} finished or weakened at {1:0.0} m."),
                        target.gameObject.name,
                        distance),
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            return true;
        }

        private void ShowTacticalReadout(Collider target, float distance)
        {
            if (target == null)
                return;

            if (target.TryGetComponent(out Hecton8.AI.FaunaBrain ai) || target.GetComponentInParent<Hecton8.AI.FaunaBrain>() != null)
            {
                ai = ai != null ? ai : target.GetComponentInParent<Hecton8.AI.FaunaBrain>();
                KnifeAssessment assessment = BuildBioformAssessment(ai, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    assessment.Headline,
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_ASSESSMENT, "{0} | {1}"),
                        assessment.Summary,
                        assessment.Recommendation),
                    assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (target.TryGetComponent(out ResourceNode node) || target.GetComponentInParent<ResourceNode>() != null)
            {
                node = node != null ? node : target.GetComponentInParent<ResourceNode>();
                KnifeAssessment assessment = BuildResourceAssessment(node, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    assessment.Headline,
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_ASSESSMENT, "{0} | {1}"),
                        assessment.Summary,
                        assessment.Recommendation),
                    assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (target.TryGetComponent(out BaseModule module) || target.GetComponentInParent<BaseModule>() != null)
            {
                module = module != null ? module : target.GetComponentInParent<BaseModule>();
                KnifeAssessment assessment = BuildModuleAssessment(module, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    assessment.Headline,
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_ASSESSMENT, "{0} | {1}"),
                        assessment.Summary,
                        assessment.Recommendation),
                    assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.KNIFE_HUD_TARGET_PROFILE_UNKNOWN, "SURVIVAL BLADE - TARGET PROFILE UNKNOWN"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    ResolveLocalized(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_TITLE, "UNKNOWN TARGET PROFILE"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_MESSAGE, "{0} does not expose a tactical vitality profile."),
                        target.gameObject.name),
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private float GetTargetNormalizedVital(Collider target)
        {
            if (target == null)
                return -1f;

            Hecton8.AI.FaunaBrain ai = target.GetComponent<Hecton8.AI.FaunaBrain>();
            if (ai == null)
                ai = target.GetComponentInParent<Hecton8.AI.FaunaBrain>();
            if (ai != null)
                return ai.HealthNormalized;

            ResourceNode node = target.GetComponent<ResourceNode>();
            if (node == null)
                node = target.GetComponentInParent<ResourceNode>();
            if (node != null)
                return node.HealthNormalized;

            BaseModule module = target.GetComponent<BaseModule>();
            if (module == null)
                module = target.GetComponentInParent<BaseModule>();
            if (module != null && module.MaxIntegrity > 0f)
                return module.CurrentIntegrity / module.MaxIntegrity;

            return -1f;
        }

        private void WarnNoContact(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            ToolHitUtility.ShowWarning(message);
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

            FaunaBrain ai = target.GetComponent<FaunaBrain>() ?? target.GetComponentInParent<FaunaBrain>();
            if (ai != null)
            {
                assessment = BuildBioformAssessment(ai, distance);
                return true;
            }

            ResourceNode node = target.GetComponent<ResourceNode>() ?? target.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                assessment = BuildResourceAssessment(node, distance);
                return true;
            }

            BaseModule module = target.GetComponent<BaseModule>() ?? target.GetComponentInParent<BaseModule>();
            if (module != null)
            {
                assessment = BuildModuleAssessment(module, distance);
                return true;
            }

            if (TryBuildDescriptorAssessment(target, distance, out assessment))
                return true;

            return false;
        }

        private KnifeAssessment BuildBioformAssessment(FaunaBrain ai, float distance)
        {
            float healthPercent = ai.HealthNormalized * 100f;
            if (ai.IsSleeping)
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_DORMANT_BIOFORM, "BLADE READ - DORMANT BIOFORM {0:0}%"),
                        healthPercent),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_DORMANT_BIOFORM, "{0} is dormant at {1:0.0} m and has not committed to attack."),
                        ai.gameObject.name,
                        distance),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_DORMANT_BIOFORM, "Strike only if you need a silent opener or a clean sample window."),
                    "INFO");
            }

            if (ai.CurrentState == FaunaBrain.AIState.Aggressive)
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_HOSTILE, "BLADE READ - HOSTILE {0:0}%"),
                        healthPercent),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_HOSTILE, "{0} is already aggressive and inside close-quarters danger range."),
                        ai.gameObject.name),
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
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_PRESSURE_CONTACT, "BLADE READ - PRESSURE CONTACT {0:0}%"),
                        healthPercent),
                    ai.CurrentState == FaunaBrain.AIState.Feint
                        ? string.Format(
                            ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_FEINT, "{0} is in a false-charge pass and can swing back into a real hit if you misread the opening."),
                            ai.gameObject.name)
                        :
                    ai.CurrentState == FaunaBrain.AIState.Loom
                        ? (ambushLeviathan
                            ? string.Format(
                                ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_AMBUSH, "{0} is setting up a burst ambush and can snap into close range with little warning."),
                                ai.gameObject.name)
                            : (sentinelLeviathan
                                ? string.Format(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_SENTINEL, "{0} is holding a guarded route and pushing you out of its corridor."),
                                    ai.gameObject.name)
                                : string.Format(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_LOOM, "{0} is holding a heavy pressure circle and can crash into close range fast."),
                                    ai.gameObject.name)))
                        : ai.CurrentState == FaunaBrain.AIState.Threaten
                        ? string.Format(
                            ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_THREATEN, "{0} is warning and pressuring you around its protected space."),
                            ai.gameObject.name)
                        : (packHunt
                            ? string.Format(
                                ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_PACK, "{0} is shadowing you as part of a group hunt and may rush from the flank."),
                                ai.gameObject.name)
                            : (feintCapable
                                ? string.Format(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_TRACKING_FEINT, "{0} is shadowing you and may throw a fake entry before the real bite."),
                                    ai.gameObject.name)
                                : string.Format(
                                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_PRESSURE_TRACKING, "{0} is shadowing you and may commit to attack soon."),
                                    ai.gameObject.name))),
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
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_FRACTURED_TARGET, "BLADE READ - FRACTURED TARGET {0:0}%"),
                        healthPercent),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_FRACTURED_TARGET, "{0} is close to collapse and vulnerable to a finishing strike."),
                        ai.gameObject.name),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_FRACTURED_TARGET, "Go for a precision hit if you need the target down now."),
                    "INFO");
            }

            return new KnifeAssessment(
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_BIOFORM_STABLE, "BLADE READ - BIOFORM STABLE {0:0}%"),
                    healthPercent),
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_BIOFORM_STABLE, "{0} is alive, mobile, and not yet in an easy finish window."),
                    ai.gameObject.name),
                ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_BIOFORM_STABLE, "Observe, soften it first, or disengage."),
                "INFO");
        }

        private KnifeAssessment BuildResourceAssessment(ResourceNode node, float distance)
        {
            if (node.IsDepleted)
            {
                return new KnifeAssessment(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_DEPLETED, "BLADE READ - NODE DEPLETED 0%"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_DEPLETED, "{0} is exhausted at {1:0.0} m and will not pay back another strike."),
                        node.gameObject.name,
                        distance),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_DEPLETED, "Leave it and move to a fresh resource lane."),
                    "WARN");
            }

            float nodePercent = node.HealthNormalized * 100f;
            if (node.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_READY, "BLADE READ - NODE READY TO BREAK {0:0}%"),
                        nodePercent),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_READY, "{0} is one clean strike away from opening at {1:0.0} m."),
                        node.gameObject.name,
                        distance),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_READY, "Finish it now if you want a fast recovery window."),
                    "INFO");
            }

            if (node.HealthNormalized <= 0.65f)
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_WEAKENED, "BLADE READ - NODE WEAKENED {0:0}%"),
                        nodePercent),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_WEAKENED, "{0} is partially cracked and reacting to tool pressure."),
                        node.gameObject.name),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_WEAKENED, "Another strike or a dedicated extraction tool is worthwhile."),
                    "INFO");
            }

            return new KnifeAssessment(
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_NODE_DENSE, "BLADE READ - NODE DENSE {0:0}%"),
                    nodePercent),
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_NODE_DENSE, "{0} still has a dense shell at {1:0.0} m."),
                    node.gameObject.name,
                    distance),
                ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_NODE_DENSE, "Use repeated strikes only if no better extraction tool is available."),
                "INFO");
        }

        private KnifeAssessment BuildModuleAssessment(BaseModule module, float distance)
        {
            float normalized = module.MaxIntegrity > 0f ? module.CurrentIntegrity / module.MaxIntegrity : 0f;
            if (module.IsBreached)
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_BREACHED, "BLADE READ - MODULE BREACHED {0:0}%"),
                        normalized * 100f),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_BREACHED, "{0} is already compromised and unsafe at {1:0.0} m."),
                        module.gameObject.name,
                        distance),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_MODULE_BREACHED, "Repair, salvage, or leave it. The blade is not the main tool here."),
                    "WARN");
            }

            if (module.CanDeconstruct())
            {
                return new KnifeAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_SALVAGEABLE, "BLADE READ - MODULE SALVAGEABLE {0:0}%"),
                        normalized * 100f),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_SALVAGEABLE, "{0} exposes reclaim paths, but not for blade work."),
                        module.gameObject.name),
                    ResolveLocalized(LocalizationKeys.KNIFE_RECOMMEND_MODULE_SALVAGEABLE, "Swap to the cutter if recovery is the goal."),
                    "INFO");
            }

            return new KnifeAssessment(
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_HEADLINE_MODULE_SEALED, "BLADE READ - MODULE SEALED {0:0}%"),
                    normalized * 100f),
                string.Format(
                    ResolveLocalized(LocalizationKeys.KNIFE_SUMMARY_MODULE_SEALED, "{0} is structurally sealed and not a valid blade target."),
                    module.gameObject.name),
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

        private static void PublishAssessment(KnifeAssessment assessment)
        {
            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(assessment.BuildHudMessage());
            else
                ToolHitUtility.ShowInfo(assessment.BuildHudMessage());
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}

