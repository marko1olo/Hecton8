using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Input;
using Hecton.Localization;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StunPistolTool : PlayerTool
    {
        public const string StunCategory = "STUN";
        private const string GenericBioformLabel = "BIOFORM";
        private const string GenericFieldTargetLabel = "FIELD TARGET";

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

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                return AppendText(ref buffer, Headline) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Summary) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Recommendation);
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
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol HUD staging buffer - owner: StunPistolTool
        private static FixedCharBuffer s_logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol field log staging buffer - owner: StunPistolTool
        private static FixedCharBuffer s_legacySummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol legacy summary/directive bridge - owner: StunPistolTool
        private static FixedCharBuffer s_assessmentTextBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol assessment text staging buffer - owner: StunPistolTool

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

                FaunaBrain ai = ResolveFaunaBrain(hit.collider);
                if (ai != null)
                {
                    StunTargetRuntime stunState = ResolveStunRuntime(ai);
                    if (stunState == null)
                        stunState = ai.gameObject.AddComponent<StunTargetRuntime>();

                    StunAssessment assessment = BuildAssessment(ai, stunState);
                    stunState.Apply(ai, stunDuration);
                    ai.ApplyFaunaInteraction(FaunaInteractionKind.Stun, hit.point, damage * GetEfficiency());
                    if (Time.time >= _nextFeedbackAt)
                    {
                        PublishAssessment(assessment);
                        RecordAssessmentLog(assessment);
                        _nextFeedbackAt = Time.time + feedbackInterval;
                    }
                }
                else if (Time.time >= _nextFeedbackAt)
                {
                    if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                    {
                        PublishAssessment(descriptorAssessment);
                        RecordAssessmentLog(descriptorAssessment);
                    }
                    else
                    {
                        PublishWarningMessage(ResolveLocalized(LocalizationKeys.STUN_HUD_NO_BIOFORM_CIRCUIT, "STUN PISTOL - NO BIOFORM CIRCUIT"));
                        RecordNonBioformLog();
                    }
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.STUN_HUD_NO_TARGET_LOCK, "STUN PISTOL - NO TARGET LOCK"));
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
                _cooldown = math.max(0f, _cooldown - deltaTime);

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (_secondaryLatched && !inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
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
                AppendText(ref buffer, "STUN PISTOL // RECHARGING ");
                buffer.AppendFloat(_cooldown, 1);
                AppendText(ref buffer, "S");
                return;
            }

            if (TryGetAssessmentCached(out StunAssessment assessment))
            {
                AppendText(ref buffer, "STUN PISTOL // ");
                AppendText(ref buffer, assessment.Headline);
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.STUN_OPERATIONAL_READY, "STUN PISTOL // READY"));
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
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.STUN_DIRECTIVE_RECHARGING, "Capacitors are recharging for the next disruption shot."));
                return;
            }

            if (TryGetAssessmentCached(out StunAssessment assessment))
            {
                AppendText(ref buffer, assessment.Recommendation);
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.STUN_DIRECTIVE_READY, "Primary disrupts. Secondary checks whether the target is worth stunning."));
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

            FaunaBrain ai = ResolveFaunaBrain(hit.collider);
            if (ai == null)
            {
                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                {
                    PublishAssessment(descriptorAssessment);
                    RecordAssessmentLog(descriptorAssessment);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else
                {
                    WarnSecondary(ResolveLocalized(LocalizationKeys.STUN_HUD_TARGET_NO_BIO_CIRCUIT, "STUN PISTOL - TARGET HAS NO BIO CIRCUIT"));
                }
                InvalidateAssessmentCache();
                return;
            }

            StunTargetRuntime stunState = ResolveStunRuntime(ai);
            StunAssessment assessment = BuildAssessment(ai, stunState);
            PublishAssessment(assessment);
            RecordAssessmentLog(assessment);

            _nextFeedbackAt = Time.time + feedbackInterval;
            InvalidateAssessmentCache();
        }

        private void WarnSecondary(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            PublishWarningMessage(message);
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

            FaunaBrain ai = ResolveFaunaBrain(hit.collider);
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

            StunTargetRuntime stunState = ResolveStunRuntime(ai);
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

        private static FaunaBrain ResolveFaunaBrain(Collider target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out FaunaBrain ai))
                return ai;

            return target.GetComponentInParent<FaunaBrain>();
        }

        private static StunTargetRuntime ResolveStunRuntime(FaunaBrain ai)
        {
            if (ai == null)
                return null;

            ai.TryGetComponent(out StunTargetRuntime stunState);
            return stunState;
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
                    CreateSingleFloatText(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_DISRUPTED, "STUN PISTOL - TARGET DISRUPTED {0:0.0}S"),
                        stunState.RemainingTime,
                        1),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_DISRUPTED, "{0} is already offline and unable to act."),
                        GenericBioformLabel),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TARGET_DISRUPTED, "Reposition, retreat, or finish another target."),
                    "INFO");
            }

            if (ai.IsDead || ai.CurrentHealth <= 0.01f)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_DOWN, "STUN PISTOL - TARGET DOWN"),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_DOWN, "{0} no longer presents an active threat."),
                        GenericBioformLabel),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_TARGET_DOWN, "Recover samples or move on."),
                    "INFO");
            }

            if (ai.IsSleeping)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_DORMANT_CONTACT, "STUN PISTOL - DORMANT CONTACT"),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_DORMANT_CONTACT, "{0} is dormant and can be disrupted before wake-up."),
                        GenericBioformLabel),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_DORMANT_CONTACT, "Take the shot now or bypass quietly."),
                    "INFO");
            }

            if (ai.HealthNormalized <= 0.25f)
            {
                return new StunAssessment(
                    ResolveLocalized(LocalizationKeys.STUN_HEADLINE_FRACTURED_TARGET, "STUN PISTOL - FRACTURED TARGET"),
                    CreateSingleStringText(
                        ResolveLocalized(LocalizationKeys.STUN_SUMMARY_FRACTURED_TARGET, "{0} is heavily weakened and close to collapse."),
                        GenericBioformLabel),
                    ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_FRACTURED_TARGET, "Disrupt, then finish or disengage safely."),
                    "WARN");
            }

            switch (ai.CurrentState)
            {
                case FaunaBrain.AIState.Aggressive:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_AGGRESSIVE_THREAT, "STUN PISTOL - AGGRESSIVE THREAT"),
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_AGGRESSIVE_THREAT, "{0} is actively attacking and should be disrupted immediately."),
                            GenericBioformLabel),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_AGGRESSIVE_THREAT, "Fire now, then create distance."),
                        "CRITICAL");

                case FaunaBrain.AIState.Threaten:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TERRITORIAL_WARNING, "STUN PISTOL - TERRITORIAL WARNING"),
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TERRITORIAL_WARNING, "{0} is pressuring you and may escalate if you keep pushing forward."),
                            GenericBioformLabel),
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
                            ? CreateSingleStringText(
                                ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PACK_HUNT, "{0} is tracking your movement as part of a hunting group."),
                                GenericBioformLabel)
                            : (feintCapable
                                ? CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PREDATOR_FEINT, "{0} is tracking your movement and can fake a charge before the real commit."),
                                    GenericBioformLabel)
                                : CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PREDATOR_TRACKING, "{0} is tracking your movement and building toward a commit."),
                                    GenericBioformLabel)),
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
                            ? CreateSingleStringText(
                                ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_AMBUSH, "{0} is coiling for a burst attack with very little warning."),
                                GenericBioformLabel)
                            : (sentinelLeviathan
                                ? CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_SENTINEL, "{0} is holding a guarded route and may force you off the line."),
                                    GenericBioformLabel)
                                : CreateSingleStringText(
                                    ResolveLocalized(LocalizationKeys.STUN_SUMMARY_LEVIATHAN_PRESSURE, "{0} is holding a heavy pressure circle and may crash into direct contact if you close distance."),
                                    GenericBioformLabel)),
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
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_FALSE_CHARGE, "{0} is in a false-charge pass and may peel away or crash into a real hit if you hold the line."),
                            GenericBioformLabel),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_FALSE_CHARGE, "Hold the shot until the pass tightens or the return swing begins."),
                        "CRITICAL");

                case FaunaBrain.AIState.Escape:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PANIC_RESPONSE, "STUN PISTOL - PANIC RESPONSE"),
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PANIC_RESPONSE, "{0} is fleeing and can be stopped for recovery or control."),
                            GenericBioformLabel),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PANIC_RESPONSE, "Disrupt if pursuit matters, otherwise hold fire."),
                        "INFO");

                case FaunaBrain.AIState.Wander:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_PATROL_CONTACT, "STUN PISTOL - PATROL CONTACT"),
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_PATROL_CONTACT, "{0} is mobile but not yet committed to attack."),
                            GenericBioformLabel),
                        ResolveLocalized(LocalizationKeys.STUN_RECOMMEND_PATROL_CONTACT, "Open with disruption before it closes distance."),
                        "INFO");

                default:
                    return new StunAssessment(
                        ResolveLocalized(LocalizationKeys.STUN_HEADLINE_TARGET_VULNERABLE, "STUN PISTOL - TARGET VULNERABLE"),
                        CreateSingleStringText(
                            ResolveLocalized(LocalizationKeys.STUN_SUMMARY_TARGET_VULNERABLE, "{0} is stable and susceptible to a disruption shot."),
                            GenericBioformLabel),
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
            s_hudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref s_hudBuffer))
                return;

            if (assessment.Severity == "CRITICAL" || assessment.Severity == "WARN")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishWarningMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowWarning(in s_hudBuffer);
        }

        private static void RecordAssessmentLog(StunAssessment assessment)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendTwoStringTemplate(
                    ref s_logSummaryBuffer,
                    ResolveLocalized(LocalizationKeys.STUN_LOG_ASSESSMENT, "{0} | {1}"),
                    assessment.Summary,
                    assessment.Recommendation))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, assessment.Summary);
                AppendText(ref s_logSummaryBuffer, " | ");
                AppendText(ref s_logSummaryBuffer, assessment.Recommendation);
            }

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                assessment.Headline,
                in s_logSummaryBuffer,
                assessment.Severity);
        }

        private static void RecordNonBioformLog()
        {
            s_logSummaryBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_logSummaryBuffer,
                ResolveLocalized(
                    LocalizationKeys.STUN_LOG_NON_BIOFORM_MESSAGE,
                    "{0} absorbed a stun shot without a compatible AI circuit."),
                GenericFieldTargetLabel);

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                ResolveLocalized(LocalizationKeys.STUN_LOG_NON_BIOFORM_TITLE, "STUN CHECK REJECTED TARGET"),
                in s_logSummaryBuffer,
                "WARN");
        }

        internal static void RecordRecoveryLog()
        {
            s_logSummaryBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_logSummaryBuffer,
                ResolveLocalized(LocalizationKeys.STUN_LOG_RECOVERED_MESSAGE, "{0} recovered from disruption and resumed activity."),
                GenericBioformLabel);

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.STUN_CATEGORY, StunCategory),
                ResolveLocalized(LocalizationKeys.STUN_LOG_RECOVERED_TITLE, "BIOFORM RECOVERED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        public static string ResolveLocalized(string key, string fallback)
        {
            return Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.GetOrFallback(Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static string CreateSingleStringText(string template, string value)
        {
            s_assessmentTextBuffer.Clear();
            if (!TryAppendSingleStringTemplate(ref s_assessmentTextBuffer, template, value))
                return template;

            return CreateLegacyString(in s_assessmentTextBuffer);
        }

        private static string CreateSingleFloatText(string template, float value, int decimals)
        {
            s_assessmentTextBuffer.Clear();
            if (!TryAppendSingleFloatTemplate(ref s_assessmentTextBuffer, template, value, decimals))
                return template;

            return CreateLegacyString(in s_assessmentTextBuffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool TryAppendSingleStringTemplate(ref FixedCharBuffer buffer, string template, string value)
        {
            return TryAppendStunTemplate(
                ref buffer,
                template,
                value,
                null,
                0f,
                0,
                0x01);
        }

        private static bool TryAppendTwoStringTemplate(ref FixedCharBuffer buffer, string template, string value0, string value1)
        {
            return TryAppendStunTemplate(
                ref buffer,
                template,
                value0,
                value1,
                0f,
                0,
                0x03);
        }

        private static bool TryAppendSingleFloatTemplate(ref FixedCharBuffer buffer, string template, float value, int decimals)
        {
            return TryAppendStunTemplate(
                ref buffer,
                template,
                null,
                null,
                value,
                decimals,
                0x04);
        }

        private static bool TryAppendStunTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            string stringArg1,
            float floatArg0,
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
                    '0' when (argumentMask & 0x01) != 0 => AppendText(ref buffer, stringArg0),
                    '0' when (argumentMask & 0x04) != 0 => buffer.AppendFloat(floatArg0, floatDecimals),
                    '1' when (argumentMask & 0x02) != 0 => AppendText(ref buffer, stringArg1),
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
            _remaining = math.max(_remaining, duration);

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
            StunPistolTool.RecordRecoveryLog();
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
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

