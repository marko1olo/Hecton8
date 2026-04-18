using Hecton8.AI;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool
    {
        private const string HarpoonCategory = "HARPOON";
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
        [SerializeField] private LayerMask targetMask = ~0;
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
        private readonly RaycastHit[] _targetHits = new RaycastHit[1]; // COLD ALLOC: harpoon resolves only the nearest target per shot.
        private Rigidbody _tetheredBody;
        private Collider _tetheredCollider;
        private string _tetheredName;
        private string _tetheredNameUpper;
        private int _cachedAssessmentFrame = -1;
        private bool _cachedAssessmentValid;
        private HarpoonAssessment _cachedAssessment;
        private float _tetherRemaining;

        private void Awake()
        {
            _cachedTransform = transform;
            EnsureTracer();
            SetTracer(false, Vector3.zero);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 endPoint = _cachedTransform.position + _cachedTransform.forward * range;

            if (TryGetTargetHit(out RaycastHit hit))
            {
                endPoint = hit.point;
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    damage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);

                TryRegisterTether(hit);

                if (Time.time >= _nextFeedbackAt)
                {
                    HarpoonAssessment assessment = BuildAssessment(hit.collider, hit.distance, _tetheredBody != null);
                    PublishAssessment(_tetheredBody != null
                        ? new HarpoonAssessment(
                            string.Format(
                                ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TETHER_LOCK, "HARPOON - TETHER LOCK [{0}]"),
                                _tetheredNameUpper ?? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET")),
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity)
                        : new HarpoonAssessment(
                            ResolveLocalized(LocalizationKeys.HARPOON_HEADLINE_TARGET_PINNED, "HARPOON - TARGET PINNED"),
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity));
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                        assessment.Headline,
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

            SetTracer(true, endPoint);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryReelTetheredTarget())
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
            body.AddForce(direction * reelImpulse, ForceMode.Impulse);

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
                if (_tetherRemaining <= 0f || !IsTetherValid())
                    ClearTether();
            }

            if (_tracerTimer > 0f)
            {
                _tracerTimer -= deltaTime;
                if (_tracerTimer <= 0f)
                    SetTracer(false, Vector3.zero);
            }
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_RECHARGING, "HARPOON // RECHARGING {0:0.0}S"),
                    _cooldown);

            if (IsTetherValid())
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_TETHER_LOCK, "HARPOON // TETHER LOCK // {0}"),
                    _tetheredNameUpper ?? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET"));

            if (TryGetAssessmentCached(out HarpoonAssessment assessment))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_ASSESSMENT, "HARPOON // {0}"),
                    assessment.Headline);

            return ResolveLocalized(LocalizationKeys.HARPOON_OPERATIONAL_READY, "HARPOON // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return ResolveLocalized(LocalizationKeys.HARPOON_DIRECTIVE_RECHARGING, "Winch and launcher are resetting for the next shot.");

            if (IsTetherValid())
                return ResolveLocalized(LocalizationKeys.HARPOON_DIRECTIVE_TETHERED, "Secondary reels the tethered target. Keep distance or break the line if needed.");

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

        private void TryRegisterTether(RaycastHit hit)
        {
            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                ClearTether();
                return;
            }

            if (body == null || body.isKinematic || body.mass > maxReelMass)
            {
                ClearTether();
                return;
            }

            _tetheredBody = body;
            _tetheredCollider = hit.collider;
            _tetheredName = body.gameObject.name;
            _tetheredNameUpper = string.IsNullOrWhiteSpace(_tetheredName)
                ? ResolveLocalized(LocalizationKeys.HARPOON_TARGET, "TARGET")
                : _tetheredName.ToUpperInvariant();
            InvalidateAssessmentCache();
            _tetherRemaining = tetherDuration;
        }

        private bool TryReelTetheredTarget()
        {
            if (!IsTetherValid())
                return false;

            Vector3 direction = (_cachedTransform.position - _tetheredBody.worldCenterOfMass).normalized;
            float impulseAmount = reelImpulse * tetherPullBonus;
            _tetheredBody.AddForce(direction * impulseAmount, ForceMode.Impulse);

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

        private bool IsTetherValid()
        {
            return _tetheredBody != null &&
                   _tetheredCollider != null &&
                   _tetheredBody.gameObject.activeInHierarchy &&
                   !_tetheredBody.isKinematic &&
                   _tetheredBody.mass <= maxReelMass;
        }

        private void ClearTether()
        {
            _tetheredBody = null;
            _tetheredCollider = null;
            _tetheredName = null;
            _tetheredNameUpper = null;
            InvalidateAssessmentCache();
            _tetherRemaining = 0f;
        }

        private bool TryGetTargetHit(out RaycastHit hit)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cachedTransform.position,
                _cachedTransform.forward,
                _targetHits,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                hit = _targetHits[0];
                return true;
            }

            hit = default;
            return false;
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

