using Hecton8.AI;
using Hecton8.Input;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StunPistolTool : PlayerTool
    {
        private readonly struct StunAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public StunAssessment(string headline, string summary, string recommendation, string severity)
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

        [Header("Stun Shot")]
        [SerializeField] private float range = 22f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float impulse = 9f;
        [SerializeField] private float stunDuration = 2.5f;
        [SerializeField] private float shotCooldown = 0.6f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float feedbackInterval = 0.35f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _nextFeedbackAt;
        private bool _secondaryLatched;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    damage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);

                HectonBaseAI ai = hit.collider.GetComponent<HectonBaseAI>();
                if (ai == null)
                    ai = hit.collider.GetComponentInParent<HectonBaseAI>();

                if (ai != null)
                {
                    StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
                    if (stunState == null)
                        stunState = ai.gameObject.AddComponent<StunTargetRuntime>();

                    StunAssessment assessment = BuildAssessment(ai, stunState);
                    stunState.Apply(ai, stunDuration);
                    if (Time.time >= _nextFeedbackAt)
                    {
                        PublishAssessment(assessment);
                        FieldOperationLogSystem.RecordOperation(
                            "STUN",
                            assessment.Headline,
                            $"{assessment.Summary} | {assessment.Recommendation}",
                            assessment.Severity);
                        _nextFeedbackAt = Time.time + feedbackInterval;
                    }
                }
                else if (Time.time >= _nextFeedbackAt)
                {
                    if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                    {
                        PublishAssessment(descriptorAssessment);
                        FieldOperationLogSystem.RecordOperation(
                            "STUN",
                            descriptorAssessment.Headline,
                            $"{descriptorAssessment.Summary} | {descriptorAssessment.Recommendation}",
                            descriptorAssessment.Severity);
                    }
                    else
                    {
                        ToolHitUtility.ShowWarning("STUN PISTOL - NO BIOFORM CIRCUIT");
                        FieldOperationLogSystem.RecordOperation(
                            "STUN",
                            "STUN SHOT HIT NON-BIOFORM TARGET",
                            $"{hit.collider.gameObject.name} absorbed a stun shot without a compatible AI circuit.",
                            "WARN");
                    }
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning("STUN PISTOL - NO TARGET LOCK");
                FieldOperationLogSystem.RecordOperation(
                    "STUN",
                    "STUN SHOT RETURNED CLEAR",
                    "No valid target was present in the stun pistol engagement cone.",
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            _cooldown = shotCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            if (_secondaryLatched && !(InputManager.Instance?.IsSecondaryActionHeld ?? false))
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return $"STUN PISTOL // RECHARGING {_cooldown:0.0}S";

            if (TryReadAssessment(out StunAssessment assessment))
                return $"STUN PISTOL // {assessment.Headline}";

            return "STUN PISTOL // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return "Capacitors are recharging for the next disruption shot.";

            if (TryReadAssessment(out StunAssessment assessment))
                return assessment.Recommendation;

            return "Primary disrupts. Secondary checks whether the target is worth stunning.";
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f || _secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                WarnSecondary("STUN PISTOL - NO TARGET LOCK");
                return;
            }

            HectonBaseAI ai = hit.collider.GetComponent<HectonBaseAI>();
            if (ai == null)
                ai = hit.collider.GetComponentInParent<HectonBaseAI>();

            if (ai == null)
            {
                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                {
                    PublishAssessment(descriptorAssessment);
                    FieldOperationLogSystem.RecordOperation(
                        "STUN",
                        descriptorAssessment.Headline,
                        $"{descriptorAssessment.Summary} | {descriptorAssessment.Recommendation}",
                        descriptorAssessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else
                {
                    WarnSecondary("STUN PISTOL - TARGET HAS NO BIO CIRCUIT");
                }
                return;
            }

            StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
            StunAssessment assessment = BuildAssessment(ai, stunState);
            PublishAssessment(assessment);
            FieldOperationLogSystem.RecordOperation(
                "STUN",
                assessment.Headline,
                $"{assessment.Summary} | {assessment.Recommendation}",
                assessment.Severity);

            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void WarnSecondary(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            ToolHitUtility.ShowWarning(message);
            FieldOperationLogSystem.RecordOperation(
                "STUN",
                message,
                "Secondary target check could not confirm a valid disruption candidate.",
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryReadAssessment(out StunAssessment assessment)
        {
            assessment = default;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            HectonBaseAI ai = hit.collider.GetComponent<HectonBaseAI>();
            if (ai == null)
                ai = hit.collider.GetComponentInParent<HectonBaseAI>();

            if (ai == null)
            {
                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out assessment))
                    return true;

                assessment = new StunAssessment(
                    "TARGET HAS NO BIO CIRCUIT",
                    "The current contact does not expose a valid disruption target.",
                    "Switch tools or reacquire a bioform.",
                    "WARN");
                return true;
            }

            StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
            assessment = BuildAssessment(ai, stunState);
            return true;
        }

        private static StunAssessment BuildAssessment(HectonBaseAI ai, StunTargetRuntime stunState)
        {
            if (ai == null)
            {
                return new StunAssessment(
                    "STUN PISTOL - NO BIOFORM",
                    "No compatible bioform circuit was detected.",
                    "Sweep a valid creature target.",
                    "WARN");
            }

            if (stunState != null && stunState.IsArmed)
            {
                return new StunAssessment(
                    $"STUN PISTOL - TARGET DISRUPTED {stunState.RemainingTime:0.0}S",
                    $"{ai.gameObject.name} is already offline and unable to act.",
                    "Reposition, retreat, or finish another target.",
                    "INFO");
            }

            if (ai.IsDead || ai.CurrentHealth <= 0.01f)
            {
                return new StunAssessment(
                    "STUN PISTOL - TARGET DOWN",
                    $"{ai.gameObject.name} no longer presents an active threat.",
                    "Recover samples or move on.",
                    "INFO");
            }

            if (ai.IsSleeping)
            {
                return new StunAssessment(
                    "STUN PISTOL - DORMANT CONTACT",
                    $"{ai.gameObject.name} is dormant and can be disrupted before wake-up.",
                    "Take the shot now or bypass quietly.",
                    "INFO");
            }

            if (ai.HealthNormalized <= 0.25f)
            {
                return new StunAssessment(
                    "STUN PISTOL - FRACTURED TARGET",
                    $"{ai.gameObject.name} is heavily weakened and close to collapse.",
                    "Disrupt, then finish or disengage safely.",
                    "WARN");
            }

            switch (ai.CurrentState)
            {
                case HectonBaseAI.AIState.Aggressive:
                    return new StunAssessment(
                        "STUN PISTOL - AGGRESSIVE THREAT",
                        $"{ai.gameObject.name} is actively attacking and should be disrupted immediately.",
                        "Fire now, then create distance.",
                        "CRITICAL");

                case HectonBaseAI.AIState.Threaten:
                    return new StunAssessment(
                        "STUN PISTOL - TERRITORIAL WARNING",
                        $"{ai.gameObject.name} is pressuring you and may escalate if you keep pushing forward.",
                        "A disruption shot can break the warning spiral before it becomes a direct attack.",
                        "WARN");

                case HectonBaseAI.AIState.Stalk:
                {
                    bool packHunt = ai.UsesPackHuntBehavior;
                    bool feintCapable = ai.UsesFeintRushBehavior;
                    return new StunAssessment(
                        packHunt ? "STUN PISTOL - PACK HUNT TRACKING" : "STUN PISTOL - PREDATOR TRACKING",
                        packHunt
                            ? $"{ai.gameObject.name} is tracking your movement as part of a hunting group."
                            : (feintCapable
                                ? $"{ai.gameObject.name} is tracking your movement and can fake a charge before the real commit."
                                : $"{ai.gameObject.name} is tracking your movement and building toward a commit."),
                        packHunt
                            ? "Disrupt now and break the group before the flank closes."
                            : (feintCapable
                                ? "Disrupt only if the fake charge becomes a real entry. Do not waste the shot too early."
                                : "Disrupt now if you want to deny the opening rush."),
                        "WARN");
                }

                case HectonBaseAI.AIState.Loom:
                {
                    bool ambushLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.AmbushBurst;
                    bool sentinelLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.SentinelPressure;
                    return new StunAssessment(
                        ambushLeviathan
                            ? "STUN PISTOL - LEVIATHAN AMBUSH"
                            : (sentinelLeviathan
                                ? "STUN PISTOL - LEVIATHAN SENTINEL"
                                : "STUN PISTOL - LEVIATHAN PRESSURE"),
                        ambushLeviathan
                            ? $"{ai.gameObject.name} is coiling for a burst attack with very little warning."
                            : (sentinelLeviathan
                                ? $"{ai.gameObject.name} is holding a guarded route and may force you off the line."
                                : $"{ai.gameObject.name} is holding a heavy pressure circle and may crash into direct contact if you close distance."),
                        ambushLeviathan
                            ? "Disrupt only to break the burst window, then move immediately."
                            : (sentinelLeviathan
                                ? "Use disruption only if you are forcing passage through the guarded corridor."
                                : "Disrupt only if you need a break window, then disengage hard."),
                        "CRITICAL");
                }

                case HectonBaseAI.AIState.Feint:
                    return new StunAssessment(
                        "STUN PISTOL - FALSE CHARGE",
                        $"{ai.gameObject.name} is in a false-charge pass and may peel away or crash into a real hit if you hold the line.",
                        "Hold the shot until the pass tightens or the return swing begins.",
                        "CRITICAL");

                case HectonBaseAI.AIState.Escape:
                    return new StunAssessment(
                        "STUN PISTOL - PANIC RESPONSE",
                        $"{ai.gameObject.name} is fleeing and can be stopped for recovery or control.",
                        "Disrupt if pursuit matters, otherwise hold fire.",
                        "INFO");

                case HectonBaseAI.AIState.Wander:
                    return new StunAssessment(
                        "STUN PISTOL - PATROL CONTACT",
                        $"{ai.gameObject.name} is mobile but not yet committed to attack.",
                        "Open with disruption before it closes distance.",
                        "INFO");

                default:
                    return new StunAssessment(
                        "STUN PISTOL - TARGET VULNERABLE",
                        $"{ai.gameObject.name} is stable and susceptible to a disruption shot.",
                        "Take a clean shot when ready.",
                        "INFO");
            }
        }

        private static bool TryBuildDescriptorAssessment(Collider target, float distance, out StunAssessment assessment)
        {
            assessment = default;
            if (!FieldTargetDescriptor.TryResolve(target, out FieldTargetDescriptor descriptor))
                return false;

            if (!FieldTargetSemantics.TryBuildStunAssessment(descriptor, distance, out FieldTargetSemantics.SemanticAssessment semantic))
                return false;

            assessment = new StunAssessment(
                semantic.Headline,
                semantic.Summary,
                semantic.Recommendation,
                semantic.Severity);
            return true;
        }

        private static void PublishAssessment(StunAssessment assessment)
        {
            if (assessment.Severity == "CRITICAL" || assessment.Severity == "WARN")
                ToolHitUtility.ShowWarning(assessment.BuildHudMessage());
            else
                ToolHitUtility.ShowInfo(assessment.BuildHudMessage());
        }
    }

    public sealed class StunTargetRuntime : MonoBehaviour
    {
        private HectonBaseAI _target;
        private float _remaining;
        private bool _armed;

        public float RemainingTime => _remaining;
        public bool IsArmed => _armed;

        public void Apply(HectonBaseAI target, float duration)
        {
            _target = target;
            _remaining = Mathf.Max(_remaining, duration);

            if (_target != null && _target.enabled)
            {
                _target.enabled = false;
                _armed = true;
            }
        }

        private void Update()
        {
            if (!_armed)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
                return;

            if (_target != null)
                _target.enabled = true;

            if (_target != null)
            {
                FieldOperationLogSystem.RecordOperation(
                    "STUN",
                    "BIOFORM RECOVERED",
                    $"{_target.gameObject.name} recovered from disruption and resumed activity.",
                    "INFO");
            }

            _armed = false;
            _remaining = 0f;
        }

        private void OnDisable()
        {
            if (_target != null)
                _target.enabled = true;

            _armed = false;
            _remaining = 0f;
        }
    }
}
