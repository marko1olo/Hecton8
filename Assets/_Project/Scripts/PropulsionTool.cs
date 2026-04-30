using UnityEngine;
using Hecton.Localization;
using Hecton8.Physics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PropulsionTool : PlayerTool
    {
        private const string PropulsionCategory = "PROPULSION";
        private readonly struct PropulsionAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public PropulsionAssessment(string headline, string summary, string recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public string BuildHudMessage()
            {
                return string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HUD_ASSESSMENT, "{0} | {1} | {2}"),
                    Headline,
                    Summary,
                    Recommendation);
            }
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

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);
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
            base.UseSecondary(deltaTime);
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
            if (_lockedBody != null)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_HOLD, "PROPULSION // TRACTOR HOLD // {0}"),
                    _lockedNameUpper ?? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO"));

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_ASSESSMENT, "PROPULSION // {0}"),
                    assessment.Headline);

            return ResolveLocalized(LocalizationKeys.PROPULSION_OPERATIONAL_READY, "PROPULSION // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_lockedBody != null)
                return ResolveLocalized(LocalizationKeys.PROPULSION_DIRECTIVE_HOLD, "Secondary releases. Primary launches the locked cargo forward.");

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
                return assessment.Recommendation;

            return ResolveLocalized(LocalizationKeys.PROPULSION_DIRECTIVE_READY, "Primary pushes mass. Secondary locks and reels mobile cargo.");
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

            PhysicsForceRouter.QueueForce(body, direction.normalized * force, ForceMode.Force);
            if (Time.time >= _nextFeedbackAt)
            {
                string title = pushAway
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_LOG_IMPULSE_APPLIED_TITLE, "PROPULSION IMPULSE APPLIED")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_LOG_TRACTOR_IMPULSE_TITLE, "TRACTOR IMPULSE APPLIED");
                PropulsionAssessment assessment = BuildAssessment(body, hit.distance, true);
                PublishAssessment(pushAway
                    ? new PropulsionAssessment(
                        ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_IMPULSE_APPLIED, "PROPULSION - IMPULSE APPLIED"),
                        assessment.Summary,
                        pushAway ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_IMPULSE_APPLIED, "Create space or clear the lane.") : assessment.Recommendation,
                        "INFO")
                    : new PropulsionAssessment(
                        ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_MASS_REELING, "TRACTOR - MASS REELING"),
                        assessment.Summary,
                        ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_MASS_REELING, "Hold the line until the cargo stabilizes."),
                        "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                    title,
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_LOG_MASS_RANGE, "{0} | MASS {1:0.0} kg | RANGE {2:0.0} m"),
                        body.gameObject.name,
                        body.mass,
                        hit.distance),
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
                : _lockedName.ToUpperInvariant();
            InvalidateAssessmentCache();
            PublishAssessment(new PropulsionAssessment(
                string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_LOCK, "TRACTOR LOCK - {0}"),
                    _lockedNameUpper),
                string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TRACTOR_LOCK, "Mass {0:0.0} kg secured at {1:0.0} m."),
                    body.mass,
                    hit.distance),
                ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TRACTOR_LOCK, "Hold steady, then launch or reposition on demand."),
                "INFO"));
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_LOCK_ACQUIRED_TITLE, "TRACTOR LOCK ACQUIRED"),
                string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_LOG_MASS_RANGE, "{0} | MASS {1:0.0} kg | RANGE {2:0.0} m"),
                    _lockedName,
                    body.mass,
                    hit.distance),
                "INFO");
            _nextFeedbackAt = Time.time + feedbackInterval;
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
            Vector3 toHold = holdPoint - _lockedBody.worldCenterOfMass;
            float distance = toHold.magnitude;

            if (distance > maxHoldBreakDistance)
            {
                ReleaseLockedTarget(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HUD_LOCK_LOST, "TRACTOR LOCK LOST"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LOCK_DRIFTED, "Locked mass drifted outside the stable handling envelope."),
                    "WARN");
                return;
            }

            Vector3 desiredVelocity = toHold * holdSpringForce * Mathf.Max(0.25f, GetEfficiency());
            Vector3 correctiveVelocity = desiredVelocity - _lockedBody.linearVelocity;
            PhysicsForceRouter.QueueForce(
                _lockedBody,
                correctiveVelocity * holdDamping * deltaTime,
                ForceMode.VelocityChange);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new PropulsionAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_HOLD, "TRACTOR HOLD - {0}"),
                        _lockedNameUpper ?? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO")),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TRACTOR_HOLD, "Cargo remains stabilized inside the handling envelope."),
                    ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TRACTOR_HOLD, "Release to drop or primary-fire to launch."),
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
                lockedNameUpper = string.IsNullOrWhiteSpace(lockedName) ? ResolveLocalized(LocalizationKeys.PROPULSION_CARGO, "CARGO") : lockedName.ToUpperInvariant();
            float appliedImpulse = launchImpulse * Mathf.Max(0.5f, GetEfficiency());
            PhysicsForceRouter.QueueForce(
                body,
                _cachedTransform.forward * appliedImpulse,
                ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(_cachedTransform.forward, appliedImpulse);
            ForceReleaseWithoutFeedback();

            PublishAssessment(new PropulsionAssessment(
                string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_LAUNCH, "PROPULSION LAUNCH - {0}"),
                    lockedNameUpper),
                ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LAUNCH, "Locked cargo was released as a forward kinetic projectile."),
                ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LAUNCH, "Confirm impact path and reacquire the next target."),
                "INFO"));
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_LAUNCH_TITLE, "PROPULSION LAUNCH"),
                string.Format(
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
                ToolHitUtility.ShowInfo(hudMessage);

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                hudMessage,
                string.Format(
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

            ToolHitUtility.ShowWarning(message);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.PROPULSION_CATEGORY, PropulsionCategory),
                message,
                ResolveLocalized(LocalizationKeys.PROPULSION_LOG_WARN_MESSAGE, "Directed force command could not be completed on the current target."),
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
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
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_TARGET_LOCK_INVALID, "TRACTOR - TARGET LOCK INVALID")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TARGET_LOCK_INVALID, "PROPULSION - TARGET LOCK INVALID"),
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_TARGET_LOCK_INVALID, "Mass signature collapsed before handling began."),
                    ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_TARGET_LOCK_INVALID, "Sweep for another movable object."),
                    "WARN");
            }

            if (body.isKinematic)
            {
                return new PropulsionAssessment(
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_STRUCTURE_ANCHORED, "TRACTOR - STRUCTURE ANCHORED")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_STRUCTURE_ANCHORED, "PROPULSION - STRUCTURE ANCHORED"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_STRUCTURE_ANCHORED, "{0} is fixed in place and cannot be manipulated."),
                        body.gameObject.name),
                    ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_STRUCTURE_ANCHORED, "Use cutter, builder, or move on."),
                    "WARN");
            }

            if (body.mass > maxTargetMass)
            {
                if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new PropulsionAssessment(
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_MASS_EXCEEDS, "TRACTOR - MASS EXCEEDS SAFE LOCK")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_MASS_EXCEEDS, "PROPULSION - MASS EXCEEDS SAFE THRUST"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_MASS_EXCEEDS, "{0} weighs {1:0.0} kg at {2:0.0} m."),
                        body.gameObject.name,
                        body.mass,
                        distance),
                    ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_MASS_EXCEEDS, "Do not force it. Use planning, deconstruction, or another route."),
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment authoredAssessment))
                return authoredAssessment;

            if (body.mass <= 20f)
            {
                return new PropulsionAssessment(
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_LIGHT_CARGO, "TRACTOR - LIGHT CARGO")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_LIGHT_CARGO, "PROPULSION - LIGHT CARGO"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_LIGHT_CARGO, "{0} is a light utility object ({1:0.0} kg)."),
                        body.gameObject.name,
                        body.mass),
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LIGHT_CARGO_TRACTOR, "Stable lock is easy. Reposition or carry it through hazards.")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_LIGHT_CARGO, "Use short impulses for precise path clearing."),
                    "INFO");
            }

            if (body.mass <= 90f)
            {
                return new PropulsionAssessment(
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_WORKLOAD, "TRACTOR - WORKLOAD MASS")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_WORKLOAD, "PROPULSION - WORKLOAD MASS"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_WORKLOAD, "{0} sits in the normal handling band ({1:0.0} kg)."),
                        body.gameObject.name,
                        body.mass),
                    tractorIntent
                        ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_WORKLOAD_TRACTOR, "Lock, steady, then launch or place it deliberately.")
                        : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_WORKLOAD, "Impulse is safe, but keep the lane clear."),
                    "INFO");
            }

            return new PropulsionAssessment(
                tractorIntent
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_TRACTOR_HEAVY_CARGO, "TRACTOR - HEAVY CARGO")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_HEADLINE_HEAVY_CARGO, "PROPULSION - HEAVY CARGO"),
                string.Format(
                    ResolveLocalized(LocalizationKeys.PROPULSION_SUMMARY_HEAVY_CARGO, "{0} is heavy but still inside the safe operating envelope ({1:0.0} kg)."),
                    body.gameObject.name,
                    body.mass),
                tractorIntent
                    ? ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_HEAVY_CARGO_TRACTOR, "Expect sluggish response and keep the hold distance stable.")
                    : ResolveLocalized(LocalizationKeys.PROPULSION_RECOMMEND_HEAVY_CARGO, "Use controlled impulses. Avoid close-range rebounds."),
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

        private void PublishAssessment(PropulsionAssessment assessment)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(assessment.BuildHudMessage());
            else
                ToolHitUtility.ShowInfo(assessment.BuildHudMessage());

            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetOrFallback(LocalizationManager.Instance.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
