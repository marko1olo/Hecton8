using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Input;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StunPistolTool : PlayerTool
    {
        public const string StunCategory = "STUN";
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
                return string.Format(
                    ResolveLocalized(LocalizationKeys.STUN_HUD_ASSESSMENT, "{0} | {1} | {2}"),
                    Headline,
                    Summary,
                    Recommendation);
            }
        }

        [Header("Stun Shot")]
        [SerializeField] private float range = 22f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float impulse = 9f;
        [SerializeField] private float stunDuration = 2.5f;
        [SerializeField] private float shotCooldown = 0.6f;
        [SerializeField] private LayerMask targetMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _nextFeedbackAt;
        private bool _secondaryLatched;
        private int _cachedAssessmentFrame = -1;
        private bool _cachedAssessmentValid;
        private StunAssessment _cachedAssessment;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetTargetHit(out RaycastHit hit))
            {
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    damage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);

                FaunaBrain ai = hit.collider.GetComponent<FaunaBrain>();
                if (ai == null)
                    ai = hit.collider.GetComponentInParent<FaunaBrain>();

                if (ai != null)
                {
                    StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
                    if (stunState == null)
                        stunState = ai.gameObject.AddComponent<StunTargetRuntime>();

                    StunAssessment assessment = BuildAssessment(ai, stunState);
                    stunState.Apply(ai, stunDuration);
                    ai.ApplyFaunaInteraction(FaunaInteractionKind.Stun, hit.point, damage * GetEfficiency());
                    if (Time.time >= _nextFeedbackAt)
                    {
                        PublishAssessment(assessment);
                        FieldOperationLogSystem.RecordOperation(
                            ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                            assessment.Headline,
                            string.Format(
                                ResolveLocalized(LocalizationKeys.STUN_LOG_ASSESSMENT, "{0} | {1}"),
                                assessment.Summary,
                                assessment.Recommendation),
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
                            ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                            descriptorAssessment.Headline,
                            string.Format(
                                ResolveLocalized(LocalizationKeys.STUN_LOG_ASSESSMENT, "{0} | {1}"),
                                descriptorAssessment.Summary,
                                descriptorAssessment.Recommendation),
                            descriptorAssessment.Severity);
                    }
                    else
                    {
                        ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.STUN_HUD_NO_BIOFORM_CIRCUIT, "STUN PISTOL - NO BIOFORM CIRCUIT"));
                        FieldOperationLogSystem.RecordOperation(
                            ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                            ResolveLocalized(LocalizationKeys.STUN_LOG_NON_BIOFORM_TITLE, "STUN SHOT HIT NON-BIOFORM TARGET"),
                            string.Format(
                                ResolveLocalized(LocalizationKeys.STUN_LOG_NON_BIOFORM_MESSAGE, "{0} absorbed a stun shot without a compatible AI circuit."),
                                hit.collider.gameObject.name),
                            "WARN");
                    }
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.STUN_HUD_NO_TARGET_LOCK, "STUN PISTOL - NO TARGET LOCK"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                    ResolveLocalized(LocalizationKeys.STUN_LOG_CLEAR_TITLE, "STUN SHOT RETURNED CLEAR"),
                    ResolveLocalized(LocalizationKeys.STUN_LOG_CLEAR_MESSAGE, "No valid target was present in the stun pistol engagement cone."),
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            InvalidateAssessmentCache();
            _cooldown = shotCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (_secondaryLatched && !inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.STUN_OPERATIONAL_RECHARGING, "STUN PISTOL // RECHARGING {0:0.0}S"),
                    _cooldown);

            if (TryGetAssessmentCached(out StunAssessment assessment))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.STUN_OPERATIONAL_ASSESSMENT, "STUN PISTOL // {0}"),
                    assessment.Headline);

            return ResolveLocalized(LocalizationKeys.STUN_OPERATIONAL_READY, "STUN PISTOL // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return ResolveLocalized(LocalizationKeys.STUN_DIRECTIVE_RECHARGING, "Capacitors are recharging for the next disruption shot.");

            if (TryGetAssessmentCached(out StunAssessment assessment))
                return assessment.Recommendation;

            return ResolveLocalized(LocalizationKeys.STUN_DIRECTIVE_READY, "Primary disrupts. Secondary checks whether the target is worth stunning.");
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f || _secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                WarnSecondary("STUN PISTOL - NO TARGET LOCK");
                InvalidateAssessmentCache();
                return;
            }

            FaunaBrain ai = hit.collider.GetComponent<FaunaBrain>();
            if (ai == null)
                ai = hit.collider.GetComponentInParent<FaunaBrain>();

            if (ai == null)
            {
                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                {
                    PublishAssessment(descriptorAssessment);
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                        descriptorAssessment.Headline,
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_LOG_ASSESSMENT, "{0} | {1}"),
                            descriptorAssessment.Summary,
                            descriptorAssessment.Recommendation),
                        descriptorAssessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else
                {
                    WarnSecondary(ResolveLocalized(LocalizationKeys.STUN_HUD_TARGET_NO_BIO_CIRCUIT, "STUN PISTOL - TARGET HAS NO BIO CIRCUIT"));
                }
                InvalidateAssessmentCache();
                return;
            }

            StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
            StunAssessment assessment = BuildAssessment(ai, stunState);
            PublishAssessment(assessment);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                assessment.Headline,
                string.Format(
                    ResolveLocalized(LocalizationKeys.STUN_LOG_ASSESSMENT, "{0} | {1}"),
                    assessment.Summary,
                    assessment.Recommendation),
                assessment.Severity);

            _nextFeedbackAt = Time.time + feedbackInterval;
            InvalidateAssessmentCache();
        }

        private void WarnSecondary(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            ToolHitUtility.ShowWarning(message);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                message,
                ResolveLocalized(LocalizationKeys.STUN_LOG_SECONDARY_FAILED, "Secondary target check could not confirm a valid disruption candidate."),
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryReadAssessment(out StunAssessment assessment)
        {
            assessment = default;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                return false;
            }

            FaunaBrain ai = hit.collider.GetComponent<FaunaBrain>();
            if (ai == null)
                ai = hit.collider.GetComponentInParent<FaunaBrain>();

            if (ai == null)
            {
                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out assessment))
                    return true;

                assessment = new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_NO_BIO_CIRCUIT, "TARGET HAS NO BIO CIRCUIT"),
                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_NO_BIO_CIRCUIT, "The current contact does not expose a valid disruption target."),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_NO_BIO_CIRCUIT, "Switch tools or reacquire a bioform."),
                    "WARN");
                return true;
            }

            StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
            assessment = BuildAssessment(ai, stunState);
            return true;
        }

        private bool TryGetAssessmentCached(out StunAssessment assessment)
        {
            int currentFrame = Time.frameCount;
            if (_cachedAssessmentFrame == currentFrame)
            {
                assessment = _cachedAssessment;
                return _cachedAssessmentValid;
            }

            bool valid = TryReadAssessment(out assessment);
            _cachedAssessmentFrame = currentFrame;
            _cachedAssessmentValid = valid;
            _cachedAssessment = assessment;
            return valid;
        }

        private void InvalidateAssessmentCache()
        {
            _cachedAssessmentFrame = -1;
            _cachedAssessmentValid = false;
            _cachedAssessment = default;
        }

        private bool TryGetTargetHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, range, targetMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private static StunAssessment BuildAssessment(FaunaBrain ai, StunTargetRuntime stunState)
        {
            if (ai == null)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_NO_BIOFORM, "STUN PISTOL - NO BIOFORM"),
                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_NO_BIOFORM, "No compatible bioform circuit was detected."),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_NO_BIOFORM, "Sweep a valid creature target."),
                    "WARN");
            }

            if (stunState != null && stunState.IsArmed)
            {
                return new StunAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_DISRUPTED, "STUN PISTOL - TARGET DISRUPTED {0:0.0}S"),
                        stunState.RemainingTime),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_DISRUPTED, "{0} is already offline and unable to act."),
                        ai.gameObject.name),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TARGET_DISRUPTED, "Reposition, retreat, or finish another target."),
                    "INFO");
            }

            if (ai.IsDead || ai.CurrentHealth <= 0.01f)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_DOWN, "STUN PISTOL - TARGET DOWN"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_DOWN, "{0} no longer presents an active threat."),
                        ai.gameObject.name),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TARGET_DOWN, "Recover samples or move on."),
                    "INFO");
            }

            if (ai.IsSleeping)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_DORMANT_CONTACT, "STUN PISTOL - DORMANT CONTACT"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_DORMANT_CONTACT, "{0} is dormant and can be disrupted before wake-up."),
                        ai.gameObject.name),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_DORMANT_CONTACT, "Take the shot now or bypass quietly."),
                    "INFO");
            }

            if (ai.HealthNormalized <= 0.25f)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_FRACTURED_TARGET, "STUN PISTOL - FRACTURED TARGET"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_FRACTURED_TARGET, "{0} is heavily weakened and close to collapse."),
                        ai.gameObject.name),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_FRACTURED_TARGET, "Disrupt, then finish or disengage safely."),
                    "WARN");
            }

            switch (ai.CurrentState)
            {
                case FaunaBrain.AIState.Aggressive:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_AGGRESSIVE_THREAT, "STUN PISTOL - AGGRESSIVE THREAT"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_AGGRESSIVE_THREAT, "{0} is actively attacking and should be disrupted immediately."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_AGGRESSIVE_THREAT, "Fire now, then create distance."),
                        "CRITICAL");

                case FaunaBrain.AIState.Threaten:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TERRITORIAL_WARNING, "STUN PISTOL - TERRITORIAL WARNING"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TERRITORIAL_WARNING, "{0} is pressuring you and may escalate if you keep pushing forward."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TERRITORIAL_WARNING, "A disruption shot can break the warning spiral before it becomes a direct attack."),
                        "WARN");

                case FaunaBrain.AIState.Stalk:
                {
                    bool packHunt = ai.UsesPackHuntBehavior;
                    bool feintCapable = ai.UsesFeintRushBehavior;
                    return new StunAssessment(
                        packHunt
                            ? ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PACK_HUNT, "STUN PISTOL - PACK HUNT TRACKING")
                            : ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PREDATOR_TRACKING, "STUN PISTOL - PREDATOR TRACKING"),
                        packHunt
                            ? string.Format(
                                ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PACK_HUNT, "{0} is tracking your movement as part of a hunting group."),
                                ai.gameObject.name)
                            : (feintCapable
                                ? string.Format(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PREDATOR_FEINT, "{0} is tracking your movement and can fake a charge before the real commit."),
                                    ai.gameObject.name)
                                : string.Format(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PREDATOR_TRACKING, "{0} is tracking your movement and building toward a commit."),
                                    ai.gameObject.name)),
                        packHunt
                            ? ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PACK_HUNT, "Disrupt now and break the group before the flank closes.")
                            : (feintCapable
                                ? ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PREDATOR_FEINT, "Disrupt only if the fake charge becomes a real entry. Do not waste the shot too early.")
                                : ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PREDATOR_TRACKING, "Disrupt now if you want to deny the opening rush.")),
                        "WARN");
                }

                case FaunaBrain.AIState.Loom:
                {
                    bool ambushLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.AmbushBurst;
                    bool sentinelLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.SentinelPressure;
                    return new StunAssessment(
                        ambushLeviathan
                            ? ResolveLocalized(LocalizationKeys.STUN_HEADLINE_LEVIATHAN_AMBUSH, "STUN PISTOL - LEVIATHAN AMBUSH")
                            : (sentinelLeviathan
                                ? ResolveLocalized(LocalizationKeys.STUN_HEADLINE_LEVIATHAN_SENTINEL, "STUN PISTOL - LEVIATHAN SENTINEL")
                                : ResolveLocalized(LocalizationKeys.STUN_HEADLINE_LEVIATHAN_PRESSURE, "STUN PISTOL - LEVIATHAN PRESSURE")),
                        ambushLeviathan
                            ? string.Format(
                                ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_AMBUSH, "{0} is coiling for a burst attack with very little warning."),
                                ai.gameObject.name)
                            : (sentinelLeviathan
                                ? string.Format(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_SENTINEL, "{0} is holding a guarded route and may force you off the line."),
                                    ai.gameObject.name)
                                : string.Format(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_PRESSURE, "{0} is holding a heavy pressure circle and may crash into direct contact if you close distance."),
                                    ai.gameObject.name)),
                        ambushLeviathan
                            ? ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_LEVIATHAN_AMBUSH, "Disrupt only to break the burst window, then move immediately.")
                            : (sentinelLeviathan
                                ? ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_LEVIATHAN_SENTINEL, "Use disruption only if you are forcing passage through the guarded corridor.")
                                : ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_LEVIATHAN_PRESSURE, "Disrupt only if you need a break window, then disengage hard.")),
                        "CRITICAL");
                }

                case FaunaBrain.AIState.Feint:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_FALSE_CHARGE, "STUN PISTOL - FALSE CHARGE"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_FALSE_CHARGE, "{0} is in a false-charge pass and may peel away or crash into a real hit if you hold the line."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_FALSE_CHARGE, "Hold the shot until the pass tightens or the return swing begins."),
                        "CRITICAL");

                case FaunaBrain.AIState.Escape:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PANIC_RESPONSE, "STUN PISTOL - PANIC RESPONSE"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PANIC_RESPONSE, "{0} is fleeing and can be stopped for recovery or control."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PANIC_RESPONSE, "Disrupt if pursuit matters, otherwise hold fire."),
                        "INFO");

                case FaunaBrain.AIState.Wander:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PATROL_CONTACT, "STUN PISTOL - PATROL CONTACT"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PATROL_CONTACT, "{0} is mobile but not yet committed to attack."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PATROL_CONTACT, "Open with disruption before it closes distance."),
                        "INFO");

                default:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_VULNERABLE, "STUN PISTOL - TARGET VULNERABLE"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_VULNERABLE, "{0} is stable and susceptible to a disruption shot."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TARGET_VULNERABLE, "Take a clean shot when ready."),
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

        public static string ResolveLocalized(string key, string fallback)
        {
            return Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.GetOrFallback(Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage, key, fallback)
                : fallback;
        }
    }

    public sealed class StunTargetRuntime : MonoBehaviour, ITickable, IUpdatable
    {
        private FaunaBrain _target;
        private float _remaining;
        private bool _armed;
        private bool _registeredToTickManager;

        public float RemainingTime => _remaining;
        public bool IsArmed => _armed;

        public void Apply(FaunaBrain target, float duration)
        {
            _target = target;
            _remaining = Mathf.Max(_remaining, duration);

            if (_target != null && _target.enabled)
            {
                _target.enabled = false;
                _armed = true;
                RegisterToTickManager();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_armed)
                return;

            _remaining -= deltaTime;
            if (_remaining > 0f)
                return;

            if (_target != null)
                _target.enabled = true;

            if (_target != null)
                LogRecovery();

            _armed = false;
            _remaining = 0f;
            UnregisterFromTickManager();
        }

        private void OnDisable()
        {
            if (_target != null)
                _target.enabled = true;

            _armed = false;
            _remaining = 0f;
            UnregisterFromTickManager();
        }

        private void LogRecovery()
        {
            FieldOperationLogSystem.RecordOperation(
                StunPistolTool.ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunPistolTool.StunCategory),
                StunPistolTool.ResolveLocalized(LocalizationKeys.STUN_LOG_RECOVERED_TITLE, "BIOFORM RECOVERED"),
                string.Format(
                    StunPistolTool.ResolveLocalized(LocalizationKeys.STUN_LOG_RECOVERED_MESSAGE, "{0} recovered from disruption and resumed activity."),
                    _target.gameObject.name),
                "INFO");
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }
    }
}

