using System;
using UnityEngine;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PropulsionTool : PlayerTool
    {
        private const string PropulsionCategory = "PROPULSION";
        private const string AssessmentSeparator = " | ";

        private readonly struct PropulsionTextSegment
        {
            public const byte HasStringArg0 = 1 << 0;
            public const byte HasStringArg1 = 1 << 1;
            public const byte HasFloatArg0 = 1 << 2;
            public const byte HasFloatArg1 = 1 << 3;
            public const byte HasFloatArg2 = 1 << 4;

            public readonly string Template;
            private readonly string _stringArg0;
            private readonly string _stringArg1;
            private readonly float _floatArg0;
            private readonly float _floatArg1;
            private readonly float _floatArg2;
            private readonly byte _argumentMask;

            public PropulsionTextSegment(string template)
            {
                Template = template;
                _stringArg0 = null;
                _stringArg1 = null;
                _floatArg0 = 0f;
                _floatArg1 = 0f;
                _floatArg2 = 0f;
                _argumentMask = 0;
            }

            private PropulsionTextSegment(
                string template,
                string stringArg0,
                string stringArg1,
                float floatArg0,
                float floatArg1,
                float floatArg2,
                byte argumentMask)
            {
                Template = template;
                _stringArg0 = stringArg0;
                _stringArg1 = stringArg1;
                _floatArg0 = floatArg0;
                _floatArg1 = floatArg1;
                _floatArg2 = floatArg2;
                _argumentMask = argumentMask;
            }

            public static PropulsionTextSegment FormatString(string template, string arg0)
            {
                return new PropulsionTextSegment(template, arg0, null, 0f, 0f, 0f, HasStringArg0);
            }

            public static PropulsionTextSegment FormatStringString(string template, string arg0, string arg1)
            {
                return new PropulsionTextSegment(template, arg0, arg1, 0f, 0f, 0f, HasStringArg0 | HasStringArg1);
            }

            public static PropulsionTextSegment FormatStringFloat(string template, string arg0, float arg1)
            {
                return new PropulsionTextSegment(template, arg0, null, 0f, arg1, 0f, HasStringArg0 | HasFloatArg1);
            }

            public static PropulsionTextSegment FormatStringFloatFloat(string template, string arg0, float arg1, float arg2)
            {
                return new PropulsionTextSegment(template, arg0, null, 0f, arg1, arg2, HasStringArg0 | HasFloatArg1 | HasFloatArg2);
            }

            public static PropulsionTextSegment FormatFloatFloat(string template, float arg0, float arg1)
            {
                return new PropulsionTextSegment(template, null, null, arg0, arg1, 0f, HasFloatArg0 | HasFloatArg1);
            }

            public bool TryWrite(ref FixedCharBuffer buffer)
            {
                return AppendFormattedText(
                    ref buffer,
                    Template,
                    _stringArg0,
                    _stringArg1,
                    _floatArg0,
                    _floatArg1,
                    _floatArg2,
                    _argumentMask);
            }
        }

        private readonly struct PropulsionAssessment
        {
            public readonly PropulsionTextSegment HeadlineText;
            public readonly PropulsionTextSegment SummaryText;
            public readonly PropulsionTextSegment RecommendationText;
            public readonly string Severity;

            public PropulsionAssessment(string headline, string summary, string recommendation, string severity)
                : this(
                    new PropulsionTextSegment(headline),
                    new PropulsionTextSegment(summary),
                    new PropulsionTextSegment(recommendation),
                    severity)
            {
            }

            public PropulsionAssessment(
                PropulsionTextSegment headline,
                PropulsionTextSegment summary,
                PropulsionTextSegment recommendation,
                string severity)
            {
                HeadlineText = headline;
                SummaryText = summary;
                RecommendationText = recommendation;
                Severity = severity;
            }

            public string Headline => HeadlineText.Template;
            public string Summary => SummaryText.Template;
            public string Recommendation => RecommendationText.Template;

            public bool TryWriteHeadline(ref FixedCharBuffer buffer) => HeadlineText.TryWrite(ref buffer);
            public bool TryWriteSummary(ref FixedCharBuffer buffer) => SummaryText.TryWrite(ref buffer);
            public bool TryWriteRecommendation(ref FixedCharBuffer buffer) => RecommendationText.TryWrite(ref buffer);
        }

        [Header("Propulsion")]
        [SerializeField] private float range = 18f;
        [SerializeField] private float pushForce = 85f;
        [SerializeField] private float pullForce = 62f;
        [SerializeField] private float maxTargetMass = 400f;
        [SerializeField] private LayerMask targetMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;
        [SerializeField] private float holdDistance = 3.8f;
        [SerializeField] private float holdSpringForce = 92f;
        [SerializeField] private float holdDamping = 9f;
        [SerializeField] private float maxHoldBreakDistance = 16f;
        [SerializeField] private float towAngleBreakBacklashImpulse = 4f;
        [SerializeField] private float launchImpulse = 22f;

        private Transform _cachedTransform;
        private float _nextFeedbackAt;
        private Rigidbody _lockedBody;
        private string _lockedName;
        private string _lockedNameUpper;
        private int _cachedAssessmentFrame = -1;
        private bool _cachedAssessmentValid;
        private PropulsionAssessment _cachedAssessment;
        private bool _primaryInvokedThisTick;
        private bool _secondaryInvokedThisTick;
        private bool _primaryHeldLastTick;
        private bool _secondaryHeldLastTick;
        private FixedCharBuffer _assessmentHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — propulsion assessment HUD staging buffer — owner: PropulsionTool
        private FixedCharBuffer _operationLogBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - propulsion operation log staging buffer - owner: PropulsionTool

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, true))
                return;

            _primaryInvokedThisTick = true;

            if (_lockedBody != null && !_primaryHeldLastTick)
            {
                LaunchLockedTarget();
                return;
            }

            ApplyDirectedForce(pushForce * GetEfficiency(), true);
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, false))
                return;

            _secondaryInvokedThisTick = true;

            if (!_secondaryHeldLastTick)
            {
                if (_lockedBody != null)
                {
                    ReleaseLockedTarget(
                        ResolveLocalized(LocalizationKeys.PROPULSION_HUD_LOCK_RELEASED, "TRACTOR LOCK RELEASED"),
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LOCK_RELEASED, "Locked target was released by operator command."),
                        "INFO");
                    return;
                }

                TryAcquireLock();
                return;
            }

            if (_lockedBody == null)
                ApplyDirectedForce(pullForce * GetEfficiency(), false);
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            ForceReleaseWithoutFeedback();
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            ForceReleaseWithoutFeedback();
        }

        public override void ToolTick(float deltaTime)
        {
            MaintainLock(deltaTime);

            _primaryHeldLastTick = _primaryInvokedThisTick;
            _secondaryHeldLastTick = _secondaryInvokedThisTick;
            _primaryInvokedThisTick = false;
            _secondaryInvokedThisTick = false;
        }

        public override string GetOperationalSummary()
        {
            _assessmentHudBuffer.Clear();
            WriteOperationalSummary(ref _assessmentHudBuffer);
            return CreateLegacyString(in _assessmentHudBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_lockedBody != null)
            {
                string lockedName = _lockedNameUpper ?? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO");
                if (!TryAppendSingleArgumentTemplate(
                        ref buffer,
                        ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_HOLD, "PROPULSION // TRACTOR HOLD // {0}"),
                        lockedName))
                {
                    buffer.Clear();
                    AppendText(ref buffer, "PROPULSION // TRACTOR HOLD // ");
                    AppendText(ref buffer, lockedName);
                }

                return;
            }

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
            {
                if (!TryAppendSingleArgumentTemplate(
                        ref buffer,
                        ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_ASSESSMENT, "PROPULSION // {0}"),
                        assessment.Headline))
                {
                    buffer.Clear();
                    AppendText(ref buffer, "PROPULSION // ");
                    AppendText(ref buffer, assessment.Headline);
                }

                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_READY, "PROPULSION // READY"));
        }

        public override string GetOperationalDirective()
        {
            _assessmentHudBuffer.Clear();
            WriteOperationalDirective(ref _assessmentHudBuffer);
            return CreateLegacyString(in _assessmentHudBuffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_lockedBody != null)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.PROPULSION_DIRECTIVE_HOLD, "Secondary releases. Primary launches the locked cargo forward."));
                return;
            }

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
            {
                AppendText(ref buffer, assessment.Recommendation);
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.PROPULSION_DIRECTIVE_READY, "Primary pushes mass. Secondary locks and reels mobile cargo."));
        }

        private void ApplyDirectedForce(float force, bool pushAway)
        {
            if (!IsEquipped || force <= 0f)
                return;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                Warn(pushAway
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_HUD_NO_MASS_LOCK, "PROPULSION - NO MASS LOCK")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TRACTOR_NO_MASS_LOCK, "TRACTOR - NO MASS LOCK"));
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                Warn(ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TARGET_NOT_MOBILE, "PROPULSION - TARGET NOT MOBILE"));
                return;
            }

            if (body == null || body.isKinematic || body.mass > maxTargetMass)
            {
                if (body != null)
                    PublishAssessment(BuildAssessment(body, hit.distance, false));
                else
                    Warn(ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TARGET_LOCK_INVALID, "PROPULSION - TARGET LOCK INVALID"));
                return;
            }

            Vector3 direction = pushAway
                ? _cachedTransform.forward
                : (_cachedTransform.position - body.worldCenterOfMass);

            if (direction.sqrMagnitude < 0.0001f)
                return;

            PhysicsForceRouter.QueueForce(body, NormalizeOrForward(direction) * force, ForceMode.Force);
            if (Time.time >= _nextFeedbackAt)
            {
                string title = pushAway
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_LOG_IMPULSE_APPLIED_TITLE, "PROPULSION IMPULSE APPLIED")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_LOG_TRACTOR_IMPULSE_TITLE, "TRACTOR IMPULSE APPLIED");
                PropulsionAssessment assessment = BuildAssessment(body, hit.distance, true);
                PublishAssessment(pushAway
                    ? new PropulsionAssessment(
                        new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_IMPULSE_APPLIED, "PROPULSION - IMPULSE APPLIED")),
                        assessment.SummaryText,
                        new PropulsionTextSegment(pushAway ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_IMPULSE_APPLIED, "Create space or clear the lane.") : assessment.Recommendation),
                        "INFO")
                    : new PropulsionAssessment(
                        new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_MASS_REELING, "TRACTOR - MASS REELING")),
                        assessment.SummaryText,
                        new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_MASS_REELING, "Hold the line until the cargo stabilizes.")),
                        "INFO"));
                RecordMassRangeLog(
                    title,
                    body.gameObject.name,
                    body.mass,
                    hit.distance,
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private void TryAcquireLock()
        {
            if (!IsEquipped)
                return;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                Warn(ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TRACTOR_NO_MASS_LOCK, "TRACTOR - NO MASS LOCK"));
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                Warn(ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TRACTOR_TARGET_NOT_MOBILE, "TRACTOR - TARGET NOT MOBILE"));
                return;
            }

            if (body == null || body.isKinematic)
            {
                if (body != null)
                    PublishAssessment(BuildAssessment(body, hit.distance, true));
                else
                    Warn(ResolveLocalized(LocalizationKeys.PROPULSION_HUD_TRACTOR_TARGET_LOCK_INVALID, "TRACTOR - TARGET LOCK INVALID"));
                return;
            }

            if (body.mass > maxTargetMass)
            {
                PublishAssessment(BuildAssessment(body, hit.distance, true));
                return;
            }

            _lockedBody = body;
            _lockedName = body.gameObject.name;
            _lockedNameUpper = string.IsNullOrWhiteSpace(_lockedName)
                ? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO")
                : _lockedName;
            InvalidateAssessmentCache();
            PublishAssessment(new PropulsionAssessment(
                PropulsionTextSegment.FormatString(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_LOCK, "TRACTOR LOCK - {0}"),
                    _lockedNameUpper),
                PropulsionTextSegment.FormatFloatFloat(
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TRACTOR_LOCK, "Mass {0:0.0} kg secured at {1:0.0} m."),
                    body.mass,
                    hit.distance),
                new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TRACTOR_LOCK, "Hold steady, then launch or reposition on demand.")),
                "INFO"));
            RecordMassRangeLog(
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_LOCK_ACQUIRED_TITLE, "TRACTOR LOCK ACQUIRED"),
                _lockedName,
                body.mass,
                hit.distance,
                "INFO");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        internal static bool ShouldBreakTractorTetherByTowAngle(Vector3 playerForward, Vector3 towVector)
        {
            if (playerForward.sqrMagnitude <= 0.0001f || towVector.sqrMagnitude <= 0.0001f)
                return false;

            return Vector3.Dot(playerForward, towVector) < 0f;
        }

        private void MaintainLock(float deltaTime)
        {
            if (_lockedBody == null)
                return;

            if (!_lockedBody.gameObject.activeInHierarchy || _lockedBody.isKinematic)
            {
                ReleaseLockedTarget(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HUD_LOCK_LOST, "TRACTOR LOCK LOST"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LOCK_INVALID, "Locked mass became invalid during field handling."),
                    "WARN");
                return;
            }

            Vector3 holdPoint = _cachedTransform.position + _cachedTransform.forward * holdDistance;
            Vector3 towVector = _lockedBody.worldCenterOfMass - _cachedTransform.position;
            if (ShouldBreakTractorTetherByTowAngle(_cachedTransform.forward, towVector))
            {
                Vector3 towDirection = towVector.sqrMagnitude > 0.0001f
                    ? NormalizeOrForward(towVector)
                    : -_cachedTransform.forward;
                ToolHitUtility.TryApplyRelativeCarrierImpulse(
                    towDirection,
                    towAngleBreakBacklashImpulse * math.max(0.5f, GetEfficiency()));
                ReleaseLockedTarget(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HUD_LOCK_LOST, "TRACTOR LOCK LOST"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LOCK_INVALID, "Tow vector crossed the carrier stern plane and snapped the tractor tether."),
                    "WARN");
                return;
            }

            Vector3 toHold = holdPoint - _lockedBody.worldCenterOfMass;

            if (toHold.sqrMagnitude > maxHoldBreakDistance * maxHoldBreakDistance)
            {
                ReleaseLockedTarget(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HUD_LOCK_LOST, "TRACTOR LOCK LOST"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LOCK_DRIFTED, "Locked mass drifted outside the stable handling envelope."),
                    "WARN");
                return;
            }

            HectonPlayerMotor playerMotor = GlobalRegistry.PlayerMotor;
            Rigidbody anchorBody = playerMotor != null ? playerMotor.Body : null;
            PhysicsForceRouter.QueueTractorBeamPd(
                anchorBody,
                _lockedBody,
                holdPoint,
                _lockedBody.worldCenterOfMass,
                holdSpringForce * math.max(0.25f, GetEfficiency()),
                math.max(1f, holdDamping),
                math.max(1f, holdSpringForce * math.max(1f, _lockedBody.mass)),
                true,
                true);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new PropulsionAssessment(
                    PropulsionTextSegment.FormatString(
                        ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_HOLD, "TRACTOR HOLD - {0}"),
                        _lockedNameUpper ?? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO")),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TRACTOR_HOLD, "Cargo remains stabilized inside the handling envelope.")),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TRACTOR_HOLD, "Release to drop or primary-fire to launch.")),
                    "INFO"));
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private void LaunchLockedTarget()
        {
            if (_lockedBody == null)
                return;

            Rigidbody body = _lockedBody;
            string lockedName = string.IsNullOrWhiteSpace(_lockedName) ? body.gameObject.name : _lockedName;
            string lockedNameUpper = _lockedNameUpper;
            if (string.IsNullOrWhiteSpace(lockedNameUpper))
                lockedNameUpper = ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO");
            float appliedImpulse = launchImpulse * math.max(0.5f, GetEfficiency());
            PhysicsForceRouter.QueueForce(
                body,
                _cachedTransform.forward * appliedImpulse,
                ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(_cachedTransform.forward, appliedImpulse);
            ForceReleaseWithoutFeedback();

            PublishAssessment(new PropulsionAssessment(
                PropulsionTextSegment.FormatString(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_LAUNCH, "PROPULSION LAUNCH - {0}"),
                    lockedNameUpper),
                new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LAUNCH, "Locked cargo was released as a forward kinetic projectile.")),
                new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LAUNCH, "Confirm impact path and reacquire the next target.")),
                "INFO"));
            RecordOperationLog(
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_LAUNCH_TITLE, "PROPULSION LAUNCH"),
                PropulsionTextSegment.FormatString(
                    ResolveLocalized(LocalizationKeys.PROPULSION_LOG_LAUNCH_MESSAGE, "{0} was launched from tractor hold."),
                    lockedName),
                "INFO");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void ReleaseLockedTarget(string hudMessage, string summary, string severity)
        {
            string lockedName = _lockedBody != null
                ? (string.IsNullOrWhiteSpace(_lockedName) ? _lockedBody.gameObject.name : _lockedName)
                : ResolveLocalized(LocalizationKeys.PROPULSION_UNKNOWN_MASS, "UNKNOWN MASS");

            ForceReleaseWithoutFeedback();

            if (!string.IsNullOrWhiteSpace(hudMessage))
                PublishInfoMessage(hudMessage);

            RecordOperationLog(
                hudMessage,
                PropulsionTextSegment.FormatStringString(
                    ResolveLocalized(LocalizationKeys.PROPULSION_LOG_RELEASE_MESSAGE, "{0}. {1}"),
                    lockedName,
                    summary),
                severity);
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void ForceReleaseWithoutFeedback()
        {
            _lockedBody = null;
            _lockedName = null;
            _lockedNameUpper = null;
            InvalidateAssessmentCache();
        }

        private void Warn(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            PublishWarningMessage(message);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                message,
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_WARN_MESSAGE, "Directed force command could not be completed on the current target."),
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void PublishInfoMessage(string message)
        {
            _assessmentHudBuffer.Clear();
            if (AppendText(ref _assessmentHudBuffer, message))
                ToolHitUtility.ShowInfo(in _assessmentHudBuffer);
        }

        private void PublishWarningMessage(string message)
        {
            _assessmentHudBuffer.Clear();
            if (AppendText(ref _assessmentHudBuffer, message))
                ToolHitUtility.ShowWarning(in _assessmentHudBuffer);
        }

        private bool TryReadAssessment(out PropulsionAssessment assessment)
        {
            assessment = default;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                return false;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                assessment = new PropulsionAssessment(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TARGET_NOT_MOBILE, "TARGET NOT MOBILE"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TARGET_NOT_MOBILE, "The current contact does not expose a movable rigidbody."),
                    ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TARGET_NOT_MOBILE, "Switch tools or sweep for free cargo."),
                    "WARN");
                return true;
            }

            assessment = BuildAssessment(body, hit.distance, _secondaryHeldLastTick || _secondaryInvokedThisTick);
            return true;
        }

        private bool TryGetAssessmentCached(out PropulsionAssessment assessment)
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

        private PropulsionAssessment BuildAssessment(Rigidbody body, float distance, bool tractorIntent)
        {
            if (body == null)
            {
                return new PropulsionAssessment(
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_TARGET_LOCK_INVALID, "TRACTOR - TARGET LOCK INVALID")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TARGET_LOCK_INVALID, "PROPULSION - TARGET LOCK INVALID")),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TARGET_LOCK_INVALID, "Mass signature collapsed before handling began.")),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TARGET_LOCK_INVALID, "Sweep for another movable object.")),
                    "WARN");
            }

            if (body.isKinematic)
            {
                return new PropulsionAssessment(
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_STRUCTURE_ANCHORED, "TRACTOR - STRUCTURE ANCHORED")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_STRUCTURE_ANCHORED, "PROPULSION - STRUCTURE ANCHORED")),
                    PropulsionTextSegment.FormatString(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_STRUCTURE_ANCHORED, "{0} is fixed in place and cannot be manipulated."),
                        body.gameObject.name),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_STRUCTURE_ANCHORED, "Use cutter, builder, or move on.")),
                    "WARN");
            }

            if (body.mass > maxTargetMass)
            {
                if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new PropulsionAssessment(
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_MASS_EXCEEDS, "TRACTOR - MASS EXCEEDS SAFE LOCK")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_MASS_EXCEEDS, "PROPULSION - MASS EXCEEDS SAFE THRUST")),
                    PropulsionTextSegment.FormatStringFloatFloat(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_MASS_EXCEEDS, "{0} weighs {1:0.0} kg at {2:0.0} m."),
                        body.gameObject.name,
                        body.mass,
                        distance),
                    new PropulsionTextSegment(ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_MASS_EXCEEDS, "Do not force it. Use planning, deconstruction, or another route.")),
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment authoredAssessment))
                return authoredAssessment;

            if (body.mass <= 20f)
            {
                return new PropulsionAssessment(
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_LIGHT_CARGO, "TRACTOR - LIGHT CARGO")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_LIGHT_CARGO, "PROPULSION - LIGHT CARGO")),
                    PropulsionTextSegment.FormatStringFloat(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LIGHT_CARGO, "{0} is a light utility object ({1:0.0} kg)."),
                        body.gameObject.name,
                        body.mass),
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LIGHT_CARGO_TRACTOR, "Stable lock is easy. Reposition or carry it through hazards.")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LIGHT_CARGO, "Use short impulses for precise path clearing.")),
                    "INFO");
            }

            if (body.mass <= 90f)
            {
                return new PropulsionAssessment(
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_WORKLOAD, "TRACTOR - WORKLOAD MASS")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_WORKLOAD, "PROPULSION - WORKLOAD MASS")),
                    PropulsionTextSegment.FormatStringFloat(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_WORKLOAD, "{0} sits in the normal handling band ({1:0.0} kg)."),
                        body.gameObject.name,
                        body.mass),
                    new PropulsionTextSegment(tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_WORKLOAD_TRACTOR, "Lock, steady, then launch or place it deliberately.")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_WORKLOAD, "Impulse is safe, but keep the lane clear.")),
                    "INFO");
            }

            return new PropulsionAssessment(
                new PropulsionTextSegment(tractorIntent
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_HEAVY_CARGO, "TRACTOR - HEAVY CARGO")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_HEAVY_CARGO, "PROPULSION - HEAVY CARGO")),
                PropulsionTextSegment.FormatStringFloat(
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_HEAVY_CARGO, "{0} is heavy but still inside the safe operating envelope ({1:0.0} kg)."),
                    body.gameObject.name,
                    body.mass),
                new PropulsionTextSegment(tractorIntent
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_HEAVY_CARGO_TRACTOR, "Expect sluggish response and keep the hold distance stable.")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_HEAVY_CARGO, "Use controlled impulses. Avoid close-range rebounds.")),
                "WARN");
        }

        private bool TryBuildDescriptorAssessment(Rigidbody body, float distance, bool tractorIntent, out PropulsionAssessment assessment)
        {
            assessment = default;
            if (!FieldTargetDescriptor.TryResolve(body, out FieldTargetDescriptor descriptor))
                return false;

            if (FieldTargetSemantics.TryBuildPropulsionAssessment(descriptor, distance, body.mass, tractorIntent, out FieldTargetSemantics.SemanticAssessment semantic))
            {
                assessment = new PropulsionAssessment(
                    semantic.Headline,
                    semantic.Summary,
                    semantic.Recommendation,
                    semantic.Severity);
                return true;
            }

            return false;
        }

        private void RecordMassRangeLog(string title, string targetName, float mass, float distance, string severity)
        {
            RecordOperationLog(
                title,
                PropulsionTextSegment.FormatStringFloatFloat(
                    ResolveLocalized(LocalizationKeys.PROPULSION_LOG_MASS_RANGE, "{0} | MASS {1:0.0} kg | RANGE {2:0.0} m"),
                    targetName,
                    mass,
                    distance),
                severity);
        }

        private void RecordOperationLog(string title, PropulsionTextSegment summary, string severity)
        {
            _operationLogBuffer.Clear();
            if (!summary.TryWrite(ref _operationLogBuffer))
                return;

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                title,
                in _operationLogBuffer,
                severity);
        }

        private static Vector3 NormalizeOrForward(Vector3 direction)
        {
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                return direction;

            return direction * math.rsqrt(sqrMagnitude);
        }

        private void PublishAssessment(PropulsionAssessment assessment)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            if (!TryBuildAssessmentHudMessage(in assessment))
                return;

            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in _assessmentHudBuffer);
            else
                ToolHitUtility.ShowInfo(in _assessmentHudBuffer);

            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryBuildAssessmentHudMessage(in PropulsionAssessment assessment)
        {
            _assessmentHudBuffer.Clear();
            string template = ResolveLocalized(LocalizationKeys.PROPULSION_HUD_ASSESSMENT, "{0} | {1} | {2}");
            if (TryAppendAssessmentTemplate(
                    ref _assessmentHudBuffer,
                    template.AsSpan(),
                    in assessment))
            {
                return _assessmentHudBuffer.Length > 0;
            }

            _assessmentHudBuffer.Clear();
            if (TryAppendDefaultAssessmentHud(
                    ref _assessmentHudBuffer,
                    in assessment))
            {
                return _assessmentHudBuffer.Length > 0;
            }

            _assessmentHudBuffer.Clear();
            return assessment.TryWriteHeadline(ref _assessmentHudBuffer) && _assessmentHudBuffer.Length > 0;
        }

        private static bool TryAppendAssessmentTemplate(
            ref FixedCharBuffer buffer,
            ReadOnlySpan<char> template,
            in PropulsionAssessment assessment)
        {
            if (template.Length <= 0)
                return TryAppendDefaultAssessmentHud(ref buffer, in assessment);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] != '{' || i + 2 >= template.Length || template[i + 2] != '}')
                    continue;

                char token = template[i + 1];
                if (token != '0' && token != '1' && token != '2')
                    continue;

                if (i > segmentStart && !buffer.Append(template.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendTemplateArgument(ref buffer, token, in assessment))
                    return false;

                wroteTemplateToken = true;
                i += 2;
                segmentStart = i + 1;
            }

            if (!wroteTemplateToken)
                return TryAppendDefaultAssessmentHud(ref buffer, in assessment);

            return segmentStart >= template.Length || buffer.Append(template.Slice(segmentStart));
        }

        private static bool TryAppendSingleArgumentTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 2 >= templateSpan.Length || templateSpan[i + 1] != '0' || templateSpan[i + 2] != '}')
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendText(ref buffer, value))
                    return false;

                wroteTemplateToken = true;
                i += 2;
                segmentStart = i + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool AppendTemplateArgument(
            ref FixedCharBuffer buffer,
            char token,
            in PropulsionAssessment assessment)
        {
            switch (token)
            {
                case '0':
                    return assessment.TryWriteHeadline(ref buffer);
                case '1':
                    return assessment.TryWriteSummary(ref buffer);
                case '2':
                    return assessment.TryWriteRecommendation(ref buffer);
                default:
                    return true;
            }
        }

        private static bool TryAppendDefaultAssessmentHud(
            ref FixedCharBuffer buffer,
            in PropulsionAssessment assessment)
        {
            return assessment.TryWriteHeadline(ref buffer) &&
                   AppendText(ref buffer, AssessmentSeparator) &&
                   assessment.TryWriteSummary(ref buffer) &&
                   AppendText(ref buffer, AssessmentSeparator) &&
                   assessment.TryWriteRecommendation(ref buffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static bool AppendFormattedText(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            string stringArg1,
            float floatArg0,
            float floatArg1,
            float floatArg2,
            byte argumentMask)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> templateSpan = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length)
                    continue;

                char tokenIndex = templateSpan[i + 1];
                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wroteArgument = false;
                if (tokenIndex == '0')
                {
                    if ((argumentMask & PropulsionTextSegment.HasStringArg0) != 0)
                    {
                        if (!AppendText(ref buffer, stringArg0))
                            return false;

                        wroteArgument = true;
                    }
                    else if ((argumentMask & PropulsionTextSegment.HasFloatArg0) != 0)
                    {
                        if (!buffer.AppendFloat(floatArg0, 1))
                            return false;

                        wroteArgument = true;
                    }
                }
                else if (tokenIndex == '1')
                {
                    if ((argumentMask & PropulsionTextSegment.HasStringArg1) != 0)
                    {
                        if (!AppendText(ref buffer, stringArg1))
                            return false;

                        wroteArgument = true;
                    }
                    else if ((argumentMask & PropulsionTextSegment.HasFloatArg1) != 0)
                    {
                        if (!buffer.AppendFloat(floatArg1, 1))
                            return false;

                        wroteArgument = true;
                    }
                }
                else if (tokenIndex == '2' && (argumentMask & PropulsionTextSegment.HasFloatArg2) != 0)
                {
                    if (!buffer.AppendFloat(floatArg2, 1))
                        return false;

                    wroteArgument = true;
                }

                if (!wroteArgument && !buffer.Append(templateSpan.Slice(i, tokenEnd - i + 1)))
                    return false;

                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            return Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.GetOrFallback(Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
