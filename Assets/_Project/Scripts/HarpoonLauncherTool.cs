using Hecton8.AI;
using Hecton.Localization;
using Hecton8.Physics;
using Hecton8.Tools;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool
    {
        private const string HarpoonCategory = "HARPOON";
        private enum TetherRegistrationResult : byte
        {
            None = 0,
            Reel = 1,
            HeavyTow = 2,
            Grapple = 3
        }

        private readonly struct HarpoonAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public HarpoonAssessment(string headline, string summary, string recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public string BuildHudMessage()
            {
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_HUD_ASSESSMENT, "{0} | {1} | {2}"),
                    Headline,
                    Summary,
                    Recommendation);
            }
        }

        private static Material s_tracerMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_tracerMaterial = null;
        }

        [Header("Harpoon")]
        [SerializeField] private float range = 36f;
        [SerializeField] private float damage = 42f;
        [SerializeField] private float impulse = 18f;
        [SerializeField] private float reelImpulse = 14f;
        [SerializeField] private float maxReelMass = 55f;
        [SerializeField] private float shotCooldown = 0.85f;
        [SerializeField] private LayerMask targetMask = (1 << 8) | (1 << 9) | (1 << 10);
        [SerializeField] private float feedbackInterval = 0.35f;
        [SerializeField] private float tetherDuration = 5f;
        [SerializeField] private float tetherPullBonus = 1.35f;

        [Header("Tracer")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private float tracerLifetime = 0.08f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _tracerTimer;
        private float _nextFeedbackAt;
        private Rigidbody _tetheredBody;
        private Collider _tetheredCollider;
        private HeavyTowWinch _heavyTowWinch;
        private HectonPlayerMovement _playerMovement;
        private Rigidbody _playerRigidbody;
        private string _tetheredName;
        private string _tetheredNameUpper;
        private Collider _grappleAnchorCollider;
        private Transform _grappleAnchorTransform;
        private Vector3 _grappleAnchorLocalPoint;
        private string _grappleAnchorName;
        private string _grappleAnchorNameUpper;
        private int _cachedAssessmentFrame = -1;
        private bool _cachedAssessmentValid;
        private HarpoonAssessment _cachedAssessment;
        private float _tetherRemaining;

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveHeavyTowWinch();
            EnsureTracer();
            SetTracer(false, Vector3.zero);
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, range);
            profile.PowerScalar = Mathf.Max(0.1f, damage);
            profile.RecoilImpulse = Mathf.Max(0f, impulse);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);
            ResolveHeavyTowWinch();
            ResolvePlayerMovement();

            if (!IsEquipped || _cooldown > 0f)
                return;

            float runtimeRange = GetRuntimeMaxRange(range);
            float runtimeDamage = GetRuntimePowerScalar(damage);
            Vector3 endPoint = _cachedTransform.position + _cachedTransform.forward * runtimeRange;

            if (TryGetTargetHit(out RaycastHit hit))
            {
                endPoint = hit.point;
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    runtimeDamage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);

                TetherRegistrationResult tetherResult = TryRegisterTether(hit);

                if (Time.time >= _nextFeedbackAt)
                {
                    bool lightTetherReady = tetherResult == TetherRegistrationResult.Reel;
                    bool heavyTowReady = tetherResult == TetherRegistrationResult.HeavyTow;
                    bool grappleReady = tetherResult == TetherRegistrationResult.Grapple;
                    HarpoonAssessment assessment = BuildAssessment(hit.collider, hit.distance, lightTetherReady || heavyTowReady || grappleReady);
                    HarpoonAssessment outboundAssessment = lightTetherReady
                        ? new HarpoonAssessment(
                            string.Format(
                                ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TETHER_LOCK, "HARPOON - TETHER LOCK [{0}]"),
                                _tetheredNameUpper ?? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET")),
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity)
                        : heavyTowReady
                            ? new HarpoonAssessment(
                                string.Format("HARPOON - HEAVY TOW LOCK [{0}]",
                                    _heavyTowWinch != null ? _heavyTowWinch.CurrentTargetNameUpper ?? "CARGO" : "CARGO"),
                                assessment.Summary,
                                "Throttle gently. Tow drag is now loading the suit and scooter.",
                                assessment.Severity)
                        : grappleReady
                            ? new HarpoonAssessment(
                                string.Format("HARPOON - EXOSUIT GRAPPLE LOCK [{0}]",
                                    _grappleAnchorNameUpper ?? "ANCHOR"),
                                assessment.Summary,
                                "Secondary reels the exosuit toward the locked structure. Release input to bleed the line.",
                                assessment.Severity)
                        : new HarpoonAssessment(
                            ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TARGET_PINNED, "HARPOON - TARGET PINNED"),
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity);
                    PublishAssessment(outboundAssessment);
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                        outboundAssessment.Headline,
                        string.Format(
                            ResolveLocalized(LocalizationKeys.HARPOON_LOG_ASSESSMENT, "{0} | {1}"),
                            assessment.Summary,
                            assessment.Recommendation),
                        assessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.HARPOON_HUD_SHOT_CLEAR, "HARPOON - SHOT RETURNED CLEAR"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                    ResolveLocalized(LocalizationKeys.HARPOON_LOG_SHOT_CLEAR_TITLE, "HARPOON SHOT RETURNED CLEAR"),
                    ResolveLocalized(LocalizationKeys.HARPOON_LOG_SHOT_CLEAR_MESSAGE, "No target intersected the last harpoon firing lane."),
                    "WARN");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            ApplyLaunchRecoil(_cachedTransform.forward, runtimeDamage);
            SetTracer(true, endPoint);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);
            ResolveHeavyTowWinch();
            ResolvePlayerMovement();

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
            {
                _heavyTowWinch.ReleaseTow(false);
                ToolHitUtility.ShowInfo("HARPOON - HEAVY TOW RELEASED");
                _cooldown = shotCooldown * 0.35f;
                return;
            }

            if (TryReelTetheredTarget())
                return;

            if (TryReelExosuitGrapple())
                return;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                WarnReel(ResolveLocalized(LocalizationKeys.HARPOON_HUD_NO_REEL_LOCK, "HARPOON - NO REEL LOCK"));
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                PublishAssessment(BuildAssessment(hit.collider, hit.distance, false));
                _nextFeedbackAt = Time.time + feedbackInterval;
                return;
            }

            if (body == null || body.isKinematic || body.mass > maxReelMass)
            {
                if (body != null)
                {
                    PublishAssessment(BuildAssessment(hit.collider, hit.distance, false));
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else
                {
                    WarnReel(ResolveLocalized(LocalizationKeys.HARPOON_HUD_REEL_LOCK_INVALID, "HARPOON - REEL LOCK INVALID"));
                }
                return;
            }

            Vector3 direction = (_cachedTransform.position - body.worldCenterOfMass).normalized;
            PhysicsForceRouter.QueueForce(body, direction * reelImpulse, ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(direction, reelImpulse);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new HarpoonAssessment(
                    ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_REEL_IMPULSE, "HARPOON - REEL IMPULSE APPLIED"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_REEL_IMPULSE, "{0} is inside safe reel mass at {1:0.0} kg."),
                        body.gameObject.name,
                        body.mass),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_REEL_IMPULSE, "Pull it into reach or keep pressure until it drifts clear."),
                    "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                    ResolveLocalized(LocalizationKeys.HARPOON_LOG_REEL_IMPULSE_TITLE, "HARPOON REEL IMPULSE"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_LOG_REEL_IMPULSE_MESSAGE, "{0} reeled with impulse on {1:0.0} kg target mass."),
                        body.gameObject.name,
                        body.mass),
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            SetTracer(true, hit.point);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.65f;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            if (_tetherRemaining > 0f)
            {
                _tetherRemaining -= deltaTime;
                if (_tetherRemaining <= 0f || (!IsTetherValid() && !IsGrappleValid()))
                    ClearTether();
            }

            if (_tracerTimer > 0f)
            {
                _tracerTimer -= deltaTime;
                if (_tracerTimer <= 0f)
                    SetTracer(false, Vector3.zero);
            }

            if (!IsGrappleValid() && _grappleAnchorCollider != null)
                ClearTether();
        }

        public override string GetOperationalSummary()
        {
            ResolveHeavyTowWinch();

            if (_cooldown > 0f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_RECHARGING, "HARPOON // RECHARGING {0:0.0}S"),
                    _cooldown);

            if (IsTetherValid())
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_TETHER_LOCK, "HARPOON // TETHER LOCK // {0}"),
                    _tetheredNameUpper ?? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET"));

            if (IsGrappleValid())
                return string.Format("HARPOON // EXOSUIT GRAPPLE // {0}", _grappleAnchorNameUpper ?? "ANCHOR");

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
                return string.Format("HARPOON // HEAVY TOW // {0}", _heavyTowWinch.CurrentTargetNameUpper ?? "CARGO");

            if (TryGetAssessmentCached(out HarpoonAssessment assessment))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_ASSESSMENT, "HARPOON // {0}"),
                    assessment.Headline);

            return ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_READY, "HARPOON // READY");
        }

        public override string GetOperationalDirective()
        {
            ResolveHeavyTowWinch();

            if (_cooldown > 0f)
                return ResolveLocalized(LocalizationKeys.HARPOON_DIRECTIVE_RECHARGING, "Winch and launcher are resetting for the next shot.");

            if (IsTetherValid())
                return ResolveLocalized(LocalizationKeys.HARPOON_DIRECTIVE_TETHERED, "Secondary reels the tethered target. Keep distance or break the line if needed.");

            if (IsGrappleValid())
                return "Secondary reels the exosuit toward the locked anchor. Stop reeling to let the line relax.";

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
                return "Secondary releases the heavy tow. Keep thrust smooth or the cable will snap.";

            if (TryGetAssessmentCached(out HarpoonAssessment assessment))
                return assessment.Recommendation;

            return ResolveLocalized(LocalizationKeys.HARPOON_DIRECTIVE_READY, "Primary fires and tags a lane. Secondary reels a light target or an active tether.");
        }

        private void SetTracer(bool active, Vector3 endPoint)
        {
            if (tracer == null)
                return;

            tracer.enabled = active;
            if (!active)
                return;

            tracer.SetPosition(0, Vector3.zero);
            tracer.SetPosition(1, _cachedTransform.InverseTransformPoint(endPoint));
        }

        private void EnsureTracer()
        {
            if (tracer != null)
                return;

            GameObject tracerRoot = new GameObject("Tracer");
            tracerRoot.transform.SetParent(transform, false);
            tracerRoot.transform.localPosition = Vector3.zero;
            tracerRoot.transform.localRotation = Quaternion.identity;

            tracer = tracerRoot.AddComponent<LineRenderer>();
            tracer.alignment = LineAlignment.View;
            tracer.useWorldSpace = false;
            tracer.positionCount = 2;
            tracer.startWidth = 0.012f;
            tracer.endWidth = 0.005f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.textureMode = LineTextureMode.Stretch;
            tracer.numCapVertices = 2;
            tracer.sharedMaterial = GetTracerMaterial();
            tracer.startColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
            tracer.endColor = new Color(0.46f, 0.98f, 0.94f, 0.2f);
            tracer.enabled = false;
        }

        private static Material GetTracerMaterial()
        {
            if (s_tracerMaterial != null)
                return s_tracerMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            s_tracerMaterial = new Material(shader);
            return s_tracerMaterial;
        }

        private void ResolveHeavyTowWinch()
        {
            if (_heavyTowWinch == null)
                _heavyTowWinch = GetComponentInParent<HeavyTowWinch>();
        }

        private void ResolvePlayerMovement()
        {
            if (_playerMovement == null)
                _playerMovement = GetComponentInParent<HectonPlayerMovement>();

            if (_playerRigidbody == null)
                _playerRigidbody = GetComponentInParent<Rigidbody>();
        }

        private void WarnReel(string message)
        {
            if (Time.time < _nextFeedbackAt)
                return;

            ToolHitUtility.ShowWarning(message);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                message,
                ResolveLocalized(LocalizationKeys.HARPOON_LOG_REEL_FAILED_MESSAGE, "Secondary reel command failed for the current target."),
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryReadAssessment(out HarpoonAssessment assessment)
        {
            assessment = default;

            if (!TryGetTargetHit(out RaycastHit hit))
            {
                return false;
            }

            assessment = BuildAssessment(hit.collider, hit.distance, false);
            return true;
        }

        private TetherRegistrationResult TryRegisterTether(RaycastHit hit)
        {
            ResolvePlayerMovement();

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                if (TryRegisterExosuitGrapple(hit, null))
                    return TetherRegistrationResult.Grapple;

                ClearTether();
                return TetherRegistrationResult.None;
            }

            if (body == null || body.isKinematic)
            {
                if (TryRegisterExosuitGrapple(hit, body))
                    return TetherRegistrationResult.Grapple;

                ClearTether();
                return TetherRegistrationResult.None;
            }

            if (body.mass <= maxReelMass)
            {
                if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
                    _heavyTowWinch.ReleaseTow(false);

                _tetheredBody = body;
                _tetheredCollider = hit.collider;
                _tetheredName = body.gameObject.name;
                _tetheredNameUpper = string.IsNullOrWhiteSpace(_tetheredName)
                    ? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET")
                    : _tetheredName.ToUpperInvariant();
                InvalidateAssessmentCache();
                _tetherRemaining = tetherDuration;
                return TetherRegistrationResult.Reel;
            }

            ClearTether();
            if (_heavyTowWinch != null && _heavyTowWinch.TryAttach(body, hit.collider, hit.distance))
                return TetherRegistrationResult.HeavyTow;

            if (TryRegisterExosuitGrapple(hit, body))
                return TetherRegistrationResult.Grapple;

            return TetherRegistrationResult.None;
        }

        private bool TryRegisterExosuitGrapple(RaycastHit hit, Rigidbody body)
        {
            if (_playerMovement == null ||
                _playerMovement.CurrentLocomotionMode != PlayerLocomotionMode.ExosuitLocomotion ||
                hit.collider == null)
            {
                return false;
            }

            if (body != null && !body.isKinematic && body.mass <= maxReelMass)
                return false;

            _grappleAnchorCollider = hit.collider;
            _grappleAnchorTransform = hit.collider.transform;
            _grappleAnchorLocalPoint = _grappleAnchorTransform != null
                ? _grappleAnchorTransform.InverseTransformPoint(hit.point)
                : hit.point;
            _grappleAnchorName = hit.collider.gameObject.name;
            _grappleAnchorNameUpper = string.IsNullOrWhiteSpace(_grappleAnchorName) ? "ANCHOR" : _grappleAnchorName.ToUpperInvariant();
            _tetherRemaining = tetherDuration;
            InvalidateAssessmentCache();
            return true;
        }

        private bool TryReelTetheredTarget()
        {
            if (!IsTetherValid())
                return false;

            Vector3 direction = (_cachedTransform.position - _tetheredBody.worldCenterOfMass).normalized;
            float impulseAmount = reelImpulse * tetherPullBonus;
            PhysicsForceRouter.QueueForce(_tetheredBody, direction * impulseAmount, ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(direction, impulseAmount);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new HarpoonAssessment(
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TETHER_REEL, "HARPOON - TETHER REEL [{0}]"),
                        _tetheredNameUpper ?? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET")),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_TETHER_REEL, "{0} remains inside tether control range."),
                        _tetheredName),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_TETHER_REEL, "Keep reeling for control or release to reset the lane."),
                    "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                    ResolveLocalized(LocalizationKeys.HARPOON_LOG_TETHER_REEL_TITLE, "HARPOON TETHER REEL"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_LOG_TETHER_REEL_MESSAGE, "{0} reeled through active tether lock."),
                        _tetheredName),
                    "INFO");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            SetTracer(true, _tetheredBody.worldCenterOfMass);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.5f;
            _tetherRemaining = tetherDuration;
            return true;
        }

        private bool TryReelExosuitGrapple()
        {
            if (!IsGrappleValid() || _playerMovement == null || !TryGetGrappleAnchorPoint(out Vector3 anchorPointWS))
                return false;

            _playerMovement.ApplyExosuitGrappleAnchor(anchorPointWS);
            SetTracer(true, anchorPointWS);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.35f;
            _tetherRemaining = tetherDuration;

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new HarpoonAssessment(
                    string.Format("HARPOON - EXOSUIT REEL [{0}]", _grappleAnchorNameUpper ?? "ANCHOR"),
                    string.Format("{0} is holding the climb line at {1:0.0} m.", _grappleAnchorName, Vector3.Distance(_cachedTransform.position, anchorPointWS)),
                    "Keep reeling to climb. Stop reeling to bleed force before the next ledge move.",
                    "INFO"));
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            return true;
        }

        private bool IsTetherValid()
        {
            return _tetheredBody != null &&
                   _tetheredCollider != null &&
                   _tetheredBody.gameObject.activeInHierarchy &&
                   !_tetheredBody.isKinematic &&
                   _tetheredBody.mass <= maxReelMass;
        }

        private bool IsGrappleValid()
        {
            return _grappleAnchorCollider != null &&
                   _grappleAnchorTransform != null &&
                   _grappleAnchorCollider.gameObject.activeInHierarchy;
        }

        private bool TryGetGrappleAnchorPoint(out Vector3 anchorPointWS)
        {
            if (!IsGrappleValid())
            {
                anchorPointWS = Vector3.zero;
                return false;
            }

            anchorPointWS = _grappleAnchorTransform.TransformPoint(_grappleAnchorLocalPoint);
            return true;
        }

        private void ClearTether()
        {
            _tetheredBody = null;
            _tetheredCollider = null;
            _tetheredName = null;
            _tetheredNameUpper = null;
            _grappleAnchorCollider = null;
            _grappleAnchorTransform = null;
            _grappleAnchorLocalPoint = Vector3.zero;
            _grappleAnchorName = null;
            _grappleAnchorNameUpper = null;
            if (_playerMovement != null)
                _playerMovement.ClearExosuitGrappleAnchor();
            InvalidateAssessmentCache();
            _tetherRemaining = 0f;
        }

        private bool TryGetTargetHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, GetRuntimeMaxRange(range), targetMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private void ApplyLaunchRecoil(Vector3 direction, float runtimeDamage)
        {
            ResolvePlayerMovement();
            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : _cachedTransform.forward;
            float mass = _playerRigidbody != null ? Mathf.Max(_playerRigidbody.mass, 0.1f) : 1f;
            float runtimeRecoil = GetRuntimeRecoilImpulse(impulse);
            float impulseMagnitude = Mathf.Min(12f, (runtimeRecoil * Mathf.Max(0.1f, runtimeDamage / Mathf.Max(damage, 0.1f))) / mass);
            if (impulseMagnitude <= 0.0001f)
                return;

            TryQueuePlayerToolRecoil(safeDirection, impulseMagnitude);
            QueueToolHapticFeedback(runtimeDamage, Mathf.Max(damage, 0.1f));
        }

        private bool TryGetAssessmentCached(out HarpoonAssessment assessment)
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

        private HarpoonAssessment BuildAssessment(Collider target, float distance, bool tetherReady)
        {
            if (target == null)
            {
                return new HarpoonAssessment(
                    ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_NO_TARGET_DATA, "HARPOON - NO TARGET DATA"),
                    ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_NO_TARGET_DATA, "Contact data collapsed before assessment completed."),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_NO_TARGET_DATA, "Sweep a new lane and reacquire."),
                    "WARN");
            }

            FaunaBrain ai = target.GetComponent<FaunaBrain>() ?? target.GetComponentInParent<FaunaBrain>();
            if (ai != null)
            {
                if (ai.IsDead || ai.CurrentHealth <= 0.01f)
                {
                    return new HarpoonAssessment(
                        ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TARGET_DOWN, "HARPOON - TARGET DOWN"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_TARGET_DOWN, "{0} is no longer an active threat."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_TARGET_DOWN, "Use the line for recovery or switch to salvage."),
                        "INFO");
                }

                if (ai.CurrentState == FaunaBrain.AIState.Aggressive)
                {
                    return new HarpoonAssessment(
                        tetherReady
                            ? ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_HOSTILE_TETHERED, "HARPOON - HOSTILE TETHERED")
                            : ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_HOSTILE_CONTACT, "HARPOON - HOSTILE CONTACT"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_HOSTILE_CONTACT, "{0} is aggressive at {1:0.0} m."),
                            ai.gameObject.name,
                            distance),
                        tetherReady
                            ? ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_HOSTILE_TETHERED, "Control its movement before it closes distance.")
                            : ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_HOSTILE_CONTACT, "Confirm the line and prepare to reel or disengage."),
                        "CRITICAL");
                }

                if (ai.HealthNormalized <= 0.35f)
                {
                    return new HarpoonAssessment(
                        tetherReady
                            ? ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_FRACTURED_TETHERED, "HARPOON - FRACTURED TARGET TETHERED")
                            : ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_FRACTURED_TARGET, "HARPOON - FRACTURED TARGET"),
                        string.Format(
                            ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_FRACTURED_TARGET, "{0} is weakened and likely to lose control under pressure."),
                            ai.gameObject.name),
                        ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_FRACTURED_TARGET, "Reel if you need control, or finish the target quickly."),
                        "WARN");
                }

                return new HarpoonAssessment(
                    tetherReady
                        ? ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_BIOFORM_TETHERED, "HARPOON - BIOFORM TETHERED")
                        : ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_BIOFORM_CONTACT, "HARPOON - BIOFORM CONTACT"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_BIOFORM_CONTACT, "{0} is under line pressure at {1:0.0} m."),
                        ai.gameObject.name,
                        distance),
                    tetherReady
                        ? ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_BIOFORM_TETHERED, "Use the tether to manage spacing and movement.")
                        : ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_BIOFORM_CONTACT, "Strike cleanly before reeling."),
                    "INFO");
            }

            if (!ToolHitUtility.TryGetRigidbody(target, out Rigidbody body))
            {
                if (tetherReady && IsGrappleValid())
                {
                    return new HarpoonAssessment(
                        "HARPOON - EXOSUIT GRAPPLE LOCK",
                        string.Format("{0} accepted a static grapple lane at {1:0.0} m.", target.gameObject.name, distance),
                        "Secondary reels the exosuit toward the anchor. Release input to stop climbing.",
                        "INFO");
                }

                return new HarpoonAssessment(
                    ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_CANNOT_REEL, "HARPOON - TARGET CANNOT BE REELED"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_CANNOT_REEL, "{0} has no valid mass body for tether control."),
                        target.gameObject.name),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_CANNOT_REEL, "Use cutter, builder, or move on."),
                    "WARN");
            }

            if (body == null || body.isKinematic)
            {
                if (tetherReady && IsGrappleValid())
                {
                    return new HarpoonAssessment(
                        "HARPOON - EXOSUIT GRAPPLE LOCK",
                        string.Format("{0} is fixed hard enough to hold a climb line at {1:0.0} m.", target.gameObject.name, distance),
                        "Secondary reels the exosuit toward the anchor. Release input to stop climbing.",
                        "INFO");
                }

                return new HarpoonAssessment(
                    ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_LOCKED_STRUCTURE, "HARPOON - TARGET LOCKED TO STRUCTURE"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_LOCKED_STRUCTURE, "{0} is fixed in place and will not reel."),
                        target.gameObject.name),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_LOCKED_STRUCTURE, "Do not waste reel force on anchored structures."),
                    "WARN");
            }

            if (body.mass > maxReelMass)
            {
                if (_heavyTowWinch != null && _heavyTowWinch.CanTowMass(body.mass))
                {
                    return new HarpoonAssessment(
                        tetherReady
                            ? "HARPOON - HEAVY TOW LOCKED"
                            : "HARPOON - HEAVY TOW CANDIDATE",
                        string.Format("{0} weighs {1:0.0} kg at {2:0.0} m.", target.gameObject.name, body.mass, distance),
                        tetherReady
                            ? "Maintain thrust discipline. Excess speed delta will snap the cable."
                            : "Primary fire can lock a heavy tow line. Expect major drag and current drift.",
                        "WARN");
                }

                if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new HarpoonAssessment(
                    ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_MASS_EXCEEDS, "HARPOON - MASS EXCEEDS REEL LIMIT"),
                    string.Format(
                        ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_MASS_EXCEEDS, "{0} weighs {1:0.0} kg at {2:0.0} m."),
                        target.gameObject.name,
                        body.mass,
                        distance),
                    ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_MASS_EXCEEDS, "Use propulsion or another route; reel force is not enough."),
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment authoredAssessment))
                return authoredAssessment;

            return new HarpoonAssessment(
                tetherReady
                    ? ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_CARGO_TETHERED, "HARPOON - CARGO TETHERED")
                    : ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_CARGO_CONTACT, "HARPOON - CARGO CONTACT"),
                string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_SUMMARY_CARGO_CONTACT, "{0} is reel-safe at {1:0.0} kg."),
                    target.gameObject.name,
                    body.mass),
                tetherReady
                    ? ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_CARGO_TETHERED, "Pull it into position or keep it off your path.")
                    : ResolveLocalized(LocalizationKeys.HARPOON_RECOMMEND_CARGO_CONTACT, "Fire again only if you need a tether lock."),
                "INFO");
        }

        private bool TryBuildDescriptorAssessment(Collider target, Rigidbody body, float distance, bool tetherReady, out HarpoonAssessment assessment)
        {
            assessment = default;
            if (target == null || !FieldTargetDescriptor.TryResolve(target, out FieldTargetDescriptor descriptor))
                return false;

            if (FieldTargetSemantics.TryBuildHarpoonAssessment(descriptor, distance, body.mass, tetherReady, out FieldTargetSemantics.SemanticAssessment semantic))
            {
                assessment = new HarpoonAssessment(
                    semantic.Headline,
                    semantic.Summary,
                    semantic.Recommendation,
                    semantic.Severity);
                return true;
            }

            return false;
        }

        private void PublishAssessment(HarpoonAssessment assessment)
        {
            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(assessment.BuildHudMessage());
            else
                ToolHitUtility.ShowInfo(assessment.BuildHudMessage());
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Простой hash для кэширования (не криптографический)
            int hash = input.GetHashCode() & 0xF; // Маска для индекса 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Создаем новую строку и кэшируем
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }
    }
}

