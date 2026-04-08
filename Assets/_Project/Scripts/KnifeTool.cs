using Hecton8.AI;
using Hecton8.Scavenging;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class KnifeTool : PlayerTool
    {
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
                return $"{Headline} | {Summary} | {Recommendation}";
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
        [SerializeField] private LayerMask hitMask = ~0;
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
                    ToolHitUtility.ShowInfo("SURVIVAL BLADE - CONTACT");
                    FieldOperationLogSystem.RecordOperation(
                        "KNIFE",
                        "MELEE CONTACT REGISTERED",
                        $"{bestCollider.gameObject.name} engaged at {bestDistance:0.0} m.",
                        "INFO");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning("SURVIVAL BLADE - NO CONTACT");
                FieldOperationLogSystem.RecordOperation(
                    "KNIFE",
                    "MELEE SWING RETURNED CLEAR",
                    "No valid target entered the blade envelope during the last swing.",
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
                return $"SURVIVAL BLADE // RECOVERING {_cooldown:0.0}S";

            if (TryGetBestHitCached(out _, out _, out float distance))
                return $"SURVIVAL BLADE // CONTACT {distance:0.0}M";

            return "SURVIVAL BLADE // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return "Reset your stance before the next strike.";

            if (TryGetBestHitCached(out Collider target, out _, out float distance))
            {
                if (TryBuildAssessment(target, distance, out KnifeAssessment assessment))
                    return assessment.Recommendation;

                return "Target is inside blade range. Strike or switch tools if the contact is armored.";
            }

            return "Primary swings. Secondary reads the contact before you commit.";
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryFindBestHit(out Collider target, out Vector3 point, out float distance))
            {
                WarnNoContact("SURVIVAL BLADE - NO TARGET READ");
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
                ToolHitUtility.ShowInfo("SURVIVAL BLADE - PRECISION STRIKE");
                FieldOperationLogSystem.RecordOperation(
                    "KNIFE",
                    "PRECISION STRIKE CONFIRMED",
                    $"{target.gameObject.name} finished or weakened at {distance:0.0} m.",
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            return true;
        }

        private void ShowTacticalReadout(Collider target, float distance)
        {
            if (target == null)
                return;

            if (target.TryGetComponent(out Hecton8.AI.HectonBaseAI ai) || target.GetComponentInParent<Hecton8.AI.HectonBaseAI>() != null)
            {
                ai = ai != null ? ai : target.GetComponentInParent<Hecton8.AI.HectonBaseAI>();
                KnifeAssessment assessment = BuildBioformAssessment(ai, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation("KNIFE", assessment.Headline, $"{assessment.Summary} | {assessment.Recommendation}", assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (target.TryGetComponent(out ResourceNode node) || target.GetComponentInParent<ResourceNode>() != null)
            {
                node = node != null ? node : target.GetComponentInParent<ResourceNode>();
                KnifeAssessment assessment = BuildResourceAssessment(node, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation("KNIFE", assessment.Headline, $"{assessment.Summary} | {assessment.Recommendation}", assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (target.TryGetComponent(out BaseModule module) || target.GetComponentInParent<BaseModule>() != null)
            {
                module = module != null ? module : target.GetComponentInParent<BaseModule>();
                KnifeAssessment assessment = BuildModuleAssessment(module, distance);
                PublishAssessment(assessment);
                FieldOperationLogSystem.RecordOperation("KNIFE", assessment.Headline, $"{assessment.Summary} | {assessment.Recommendation}", assessment.Severity);
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowInfo("SURVIVAL BLADE - TARGET PROFILE UNKNOWN");
                FieldOperationLogSystem.RecordOperation(
                    "KNIFE",
                    "UNKNOWN TARGET PROFILE",
                    $"{target.gameObject.name} does not expose a tactical vitality profile.",
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private float GetTargetNormalizedVital(Collider target)
        {
            if (target == null)
                return -1f;

            Hecton8.AI.HectonBaseAI ai = target.GetComponent<Hecton8.AI.HectonBaseAI>();
            if (ai == null)
                ai = target.GetComponentInParent<Hecton8.AI.HectonBaseAI>();
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
                "KNIFE",
                message,
                "No valid target entered the blade envelope during the tactical read.",
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryBuildAssessment(Collider target, float distance, out KnifeAssessment assessment)
        {
            assessment = default;

            HectonBaseAI ai = target.GetComponent<HectonBaseAI>() ?? target.GetComponentInParent<HectonBaseAI>();
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

        private KnifeAssessment BuildBioformAssessment(HectonBaseAI ai, float distance)
        {
            float healthPercent = ai.HealthNormalized * 100f;
            if (ai.IsSleeping)
            {
                return new KnifeAssessment(
                    $"BLADE READ - DORMANT BIOFORM {healthPercent:0}%",
                    $"{ai.gameObject.name} is dormant at {distance:0.0} m and has not committed to attack.",
                    "Strike only if you need a silent opener or a clean sample window.",
                    "INFO");
            }

            if (ai.CurrentState == HectonBaseAI.AIState.Aggressive)
            {
                return new KnifeAssessment(
                    $"BLADE READ - HOSTILE {healthPercent:0}%",
                    $"{ai.gameObject.name} is already aggressive and inside close-quarters danger range.",
                    ai.HealthNormalized <= criticalHealthThreshold
                        ? "Precision strike is viable, but commit fast."
                        : "Use stun or create space before relying on the blade.",
                    "WARN");
            }

            if (ai.CurrentState == HectonBaseAI.AIState.Threaten || ai.CurrentState == HectonBaseAI.AIState.Stalk || ai.CurrentState == HectonBaseAI.AIState.Loom || ai.CurrentState == HectonBaseAI.AIState.Feint)
            {
                bool packHunt = ai.UsesPackHuntBehavior && ai.CurrentState == HectonBaseAI.AIState.Stalk;
                bool feintCapable = ai.UsesFeintRushBehavior && (ai.CurrentState == HectonBaseAI.AIState.Stalk || ai.CurrentState == HectonBaseAI.AIState.Loom || ai.CurrentState == HectonBaseAI.AIState.Feint);
                bool ambushLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.AmbushBurst;
                bool sentinelLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.SentinelPressure;
                return new KnifeAssessment(
                    $"BLADE READ - PRESSURE CONTACT {healthPercent:0}%",
                    ai.CurrentState == HectonBaseAI.AIState.Feint
                        ? $"{ai.gameObject.name} is in a false-charge pass and can swing back into a real hit if you misread the opening."
                        :
                    ai.CurrentState == HectonBaseAI.AIState.Loom
                        ? (ambushLeviathan
                            ? $"{ai.gameObject.name} is setting up a burst ambush and can snap into close range with little warning."
                            : (sentinelLeviathan
                                ? $"{ai.gameObject.name} is holding a guarded route and pushing you out of its corridor."
                                : $"{ai.gameObject.name} is holding a heavy pressure circle and can crash into close range fast."))
                        : ai.CurrentState == HectonBaseAI.AIState.Threaten
                        ? $"{ai.gameObject.name} is warning and pressuring you around its protected space."
                        : (packHunt
                            ? $"{ai.gameObject.name} is shadowing you as part of a group hunt and may rush from the flank."
                            : (feintCapable
                                ? $"{ai.gameObject.name} is shadowing you and may throw a fake entry before the real bite."
                                : $"{ai.gameObject.name} is shadowing you and may commit to attack soon.")),
                    ai.CurrentState == HectonBaseAI.AIState.Feint
                        ? "Do not knife into the pass. Sidestep the fake run and wait for the turn."
                        :
                    ai.CurrentState == HectonBaseAI.AIState.Loom
                        ? (ambushLeviathan
                            ? "Do not trust a knife opener here. Break the angle before it bursts."
                            : (sentinelLeviathan
                                ? "This is a bad knife lane. Leave the corridor or force a hard opening first."
                                : "Do not trust a knife opener here. Break distance first."))
                        : packHunt
                        ? "Do not trust a knife opener here unless you have already broken the group shape."
                        : (feintCapable
                            ? "Do not bite on the fake entry. Hold the blade for the real commit window."
                            : "Do not rely on a knife opener here unless you are forcing a close-range break."),
                    "WARN");
            }

            if (ai.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    $"BLADE READ - FRACTURED TARGET {healthPercent:0}%",
                    $"{ai.gameObject.name} is close to collapse and vulnerable to a finishing strike.",
                    "Go for a precision hit if you need the target down now.",
                    "INFO");
            }

            return new KnifeAssessment(
                $"BLADE READ - BIOFORM STABLE {healthPercent:0}%",
                $"{ai.gameObject.name} is alive, mobile, and not yet in an easy finish window.",
                "Observe, soften it first, or disengage.",
                "INFO");
        }

        private KnifeAssessment BuildResourceAssessment(ResourceNode node, float distance)
        {
            if (node.IsDepleted)
            {
                return new KnifeAssessment(
                    $"BLADE READ - NODE DEPLETED 0%",
                    $"{node.gameObject.name} is exhausted at {distance:0.0} m and will not pay back another strike.",
                    "Leave it and move to a fresh resource lane.",
                    "WARN");
            }

            float nodePercent = node.HealthNormalized * 100f;
            if (node.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    $"BLADE READ - NODE READY TO BREAK {nodePercent:0}%",
                    $"{node.gameObject.name} is one clean strike away from opening at {distance:0.0} m.",
                    "Finish it now if you want a fast recovery window.",
                    "INFO");
            }

            if (node.HealthNormalized <= 0.65f)
            {
                return new KnifeAssessment(
                    $"BLADE READ - NODE WEAKENED {nodePercent:0}%",
                    $"{node.gameObject.name} is partially cracked and reacting to tool pressure.",
                    "Another strike or a dedicated extraction tool is worthwhile.",
                    "INFO");
            }

            return new KnifeAssessment(
                $"BLADE READ - NODE DENSE {nodePercent:0}%",
                $"{node.gameObject.name} still has a dense shell at {distance:0.0} m.",
                "Use repeated strikes only if no better extraction tool is available.",
                "INFO");
        }

        private KnifeAssessment BuildModuleAssessment(BaseModule module, float distance)
        {
            float normalized = module.MaxIntegrity > 0f ? module.CurrentIntegrity / module.MaxIntegrity : 0f;
            if (module.IsBreached)
            {
                return new KnifeAssessment(
                    $"BLADE READ - MODULE BREACHED {normalized * 100f:0}%",
                    $"{module.gameObject.name} is already compromised and unsafe at {distance:0.0} m.",
                    "Repair, salvage, or leave it. The blade is not the main tool here.",
                    "WARN");
            }

            if (module.CanDeconstruct())
            {
                return new KnifeAssessment(
                    $"BLADE READ - MODULE SALVAGEABLE {normalized * 100f:0}%",
                    $"{module.gameObject.name} exposes reclaim paths, but not for blade work.",
                    "Swap to the cutter if recovery is the goal.",
                    "INFO");
            }

            return new KnifeAssessment(
                $"BLADE READ - MODULE SEALED {normalized * 100f:0}%",
                $"{module.gameObject.name} is structurally sealed and not a valid blade target.",
                "Use repair, builder, or cutter tools instead.",
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
    }
}
