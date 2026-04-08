using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PropulsionTool : PlayerTool
    {
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
                return $"{Headline} | {Summary} | {Recommendation}";
            }
        }

        [Header("Propulsion")]
        [SerializeField] private float range = 18f;
        [SerializeField] private float pushForce = 85f;
        [SerializeField] private float pullForce = 62f;
        [SerializeField] private float maxTargetMass = 400f;
        [SerializeField] private LayerMask targetMask = ~0;
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
                    ReleaseLockedTarget("TRACTOR LOCK RELEASED", "Locked target was released by operator command.", "INFO");
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
                return $"PROPULSION // TRACTOR HOLD // {_lockedNameUpper ?? "CARGO"}";

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
                return $"PROPULSION // {assessment.Headline}";

            return "PROPULSION // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_lockedBody != null)
                return "Secondary releases. Primary launches the locked cargo forward.";

            if (TryGetAssessmentCached(out PropulsionAssessment assessment))
                return assessment.Recommendation;

            return "Primary pushes mass. Secondary locks and reels mobile cargo.";
        }

        private void ApplyDirectedForce(float force, bool pushAway)
        {
            if (!IsEquipped || force <= 0f)
                return;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                Warn(pushAway ? "PROPULSION - NO MASS LOCK" : "TRACTOR - NO MASS LOCK");
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                Warn("PROPULSION - TARGET NOT MOBILE");
                return;
            }

            if (body == null || body.isKinematic || body.mass > maxTargetMass)
            {
                if (body != null)
                    PublishAssessment(BuildAssessment(body, hit.distance, false));
                else
                    Warn("PROPULSION - TARGET LOCK INVALID");
                return;
            }

            Vector3 direction = pushAway
                ? _cachedTransform.forward
                : (_cachedTransform.position - body.worldCenterOfMass);

            if (direction.sqrMagnitude < 0.0001f)
                return;

            body.AddForce(direction.normalized * force, ForceMode.Force);
            if (Time.time >= _nextFeedbackAt)
            {
                string title = pushAway ? "PROPULSION IMPULSE APPLIED" : "TRACTOR IMPULSE APPLIED";
                PropulsionAssessment assessment = BuildAssessment(body, hit.distance, true);
                PublishAssessment(pushAway
                    ? new PropulsionAssessment(
                        "PROPULSION - IMPULSE APPLIED",
                        assessment.Summary,
                        pushAway ? "Create space or clear the lane." : assessment.Recommendation,
                        "INFO")
                    : new PropulsionAssessment(
                        "TRACTOR - MASS REELING",
                        assessment.Summary,
                        "Hold the line until the cargo stabilizes.",
                        "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    "PROPULSION",
                    title,
                    $"{body.gameObject.name} | MASS {body.mass:0.0} kg | RANGE {hit.distance:0.0} m",
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }
        }

        private void TryAcquireLock()
        {
            if (!IsEquipped)
                return;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                Warn("TRACTOR - NO MASS LOCK");
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                Warn("TRACTOR - TARGET NOT MOBILE");
                return;
            }

            if (body == null || body.isKinematic)
            {
                if (body != null)
                    PublishAssessment(BuildAssessment(body, hit.distance, true));
                else
                    Warn("TRACTOR - TARGET LOCK INVALID");
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
                ? "CARGO"
                : _lockedName.ToUpperInvariant();
            InvalidateAssessmentCache();
            PublishAssessment(new PropulsionAssessment(
                $"TRACTOR LOCK - {_lockedNameUpper}",
                $"Mass {body.mass:0.0} kg secured at {hit.distance:0.0} m.",
                "Hold steady, then launch or reposition on demand.",
                "INFO"));
            FieldOperationLogSystem.RecordOperation(
                "PROPULSION",
                "TRACTOR LOCK ACQUIRED",
                $"{_lockedName} | MASS {body.mass:0.0} kg | RANGE {hit.distance:0.0} m",
                "INFO");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void MaintainLock(float deltaTime)
        {
            if (_lockedBody == null)
                return;

            if (!_lockedBody.gameObject.activeInHierarchy || _lockedBody.isKinematic)
            {
                ReleaseLockedTarget("TRACTOR LOCK LOST", "Locked mass became invalid during field handling.", "WARN");
                return;
            }

            Vector3 holdPoint = _cachedTransform.position + _cachedTransform.forward * holdDistance;
            Vector3 toHold = holdPoint - _lockedBody.worldCenterOfMass;
            float distance = toHold.magnitude;

            if (distance > maxHoldBreakDistance)
            {
                ReleaseLockedTarget("TRACTOR LOCK LOST", "Locked mass drifted outside the stable handling envelope.", "WARN");
                return;
            }

            Vector3 desiredVelocity = toHold * holdSpringForce * Mathf.Max(0.25f, GetEfficiency());
            Vector3 correctiveVelocity = desiredVelocity - _lockedBody.linearVelocity;
            _lockedBody.AddForce(correctiveVelocity * holdDamping * deltaTime, ForceMode.VelocityChange);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new PropulsionAssessment(
                    $"TRACTOR HOLD - {_lockedNameUpper ?? "CARGO"}",
                    $"Cargo remains stabilized inside the handling envelope.",
                    "Release to drop or primary-fire to launch.",
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
                lockedNameUpper = string.IsNullOrWhiteSpace(lockedName) ? "CARGO" : lockedName.ToUpperInvariant();
            body.AddForce(_cachedTransform.forward * (launchImpulse * Mathf.Max(0.5f, GetEfficiency())), ForceMode.Impulse);
            ForceReleaseWithoutFeedback();

            PublishAssessment(new PropulsionAssessment(
                $"PROPULSION LAUNCH - {lockedNameUpper}",
                "Locked cargo was released as a forward kinetic projectile.",
                "Confirm impact path and reacquire the next target.",
                "INFO"));
            FieldOperationLogSystem.RecordOperation(
                "PROPULSION",
                "PROPULSION LAUNCH",
                $"{lockedName} was launched from tractor hold.",
                "INFO");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private void ReleaseLockedTarget(string hudMessage, string summary, string severity)
        {
            string lockedName = _lockedBody != null
                ? (string.IsNullOrWhiteSpace(_lockedName) ? _lockedBody.gameObject.name : _lockedName)
                : "UNKNOWN MASS";

            ForceReleaseWithoutFeedback();

            if (!string.IsNullOrWhiteSpace(hudMessage))
                ToolHitUtility.ShowInfo(hudMessage);

            FieldOperationLogSystem.RecordOperation(
                "PROPULSION",
                hudMessage,
                $"{lockedName}. {summary}",
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
                "PROPULSION",
                message,
                "Directed force command could not be completed on the current target.",
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryReadAssessment(out PropulsionAssessment assessment)
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

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                assessment = new PropulsionAssessment(
                    "TARGET NOT MOBILE",
                    "The current contact does not expose a movable rigidbody.",
                    "Switch tools or sweep for free cargo.",
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

        private PropulsionAssessment BuildAssessment(Rigidbody body, float distance, bool tractorIntent)
        {
            if (body == null)
            {
                return new PropulsionAssessment(
                    tractorIntent ? "TRACTOR - TARGET LOCK INVALID" : "PROPULSION - TARGET LOCK INVALID",
                    "Mass signature collapsed before handling began.",
                    "Sweep for another movable object.",
                    "WARN");
            }

            if (body.isKinematic)
            {
                return new PropulsionAssessment(
                    tractorIntent ? "TRACTOR - STRUCTURE ANCHORED" : "PROPULSION - STRUCTURE ANCHORED",
                    $"{body.gameObject.name} is fixed in place and cannot be manipulated.",
                    "Use cutter, builder, or move on.",
                    "WARN");
            }

            if (body.mass > maxTargetMass)
            {
                if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new PropulsionAssessment(
                    tractorIntent ? "TRACTOR - MASS EXCEEDS SAFE LOCK" : "PROPULSION - MASS EXCEEDS SAFE THRUST",
                    $"{body.gameObject.name} weighs {body.mass:0.0} kg at {distance:0.0} m.",
                    "Do not force it. Use planning, deconstruction, or another route.",
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(body, distance, tractorIntent, out PropulsionAssessment authoredAssessment))
                return authoredAssessment;

            if (body.mass <= 20f)
            {
                return new PropulsionAssessment(
                    tractorIntent ? "TRACTOR - LIGHT CARGO" : "PROPULSION - LIGHT CARGO",
                    $"{body.gameObject.name} is a light utility object ({body.mass:0.0} kg).",
                    tractorIntent
                        ? "Stable lock is easy. Reposition or carry it through hazards."
                        : "Use short impulses for precise path clearing.",
                    "INFO");
            }

            if (body.mass <= 90f)
            {
                return new PropulsionAssessment(
                    tractorIntent ? "TRACTOR - WORKLOAD MASS" : "PROPULSION - WORKLOAD MASS",
                    $"{body.gameObject.name} sits in the normal handling band ({body.mass:0.0} kg).",
                    tractorIntent
                        ? "Lock, steady, then launch or place it deliberately."
                        : "Impulse is safe, but keep the lane clear.",
                    "INFO");
            }

            return new PropulsionAssessment(
                tractorIntent ? "TRACTOR - HEAVY CARGO" : "PROPULSION - HEAVY CARGO",
                $"{body.gameObject.name} is heavy but still inside the safe operating envelope ({body.mass:0.0} kg).",
                tractorIntent
                    ? "Expect sluggish response and keep the hold distance stable."
                    : "Use controlled impulses. Avoid close-range rebounds.",
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
    }
}
