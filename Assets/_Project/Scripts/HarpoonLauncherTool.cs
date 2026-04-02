using Hecton8.AI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool
    {
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
                return $"{Headline} | {Summary} | {Recommendation}";
            }
        }

        private static Material s_tracerMaterial;

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
        private Rigidbody _tetheredBody;
        private Collider _tetheredCollider;
        private string _tetheredName;
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

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
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
                            $"HARPOON - TETHER LOCK [{CachedToUpperInvariant(_tetheredName)}]",
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity)
                        : new HarpoonAssessment(
                            "HARPOON - TARGET PINNED",
                            assessment.Summary,
                            assessment.Recommendation,
                            assessment.Severity));
                    FieldOperationLogSystem.RecordOperation(
                        "HARPOON",
                        assessment.Headline,
                        $"{assessment.Summary} | {assessment.Recommendation}",
                        assessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning("HARPOON - SHOT RETURNED CLEAR");
                FieldOperationLogSystem.RecordOperation(
                    "HARPOON",
                    "HARPOON SHOT RETURNED CLEAR",
                    "No target intersected the last harpoon firing lane.",
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

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                WarnReel("HARPOON - NO REEL LOCK");
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
                    WarnReel("HARPOON - REEL LOCK INVALID");
                }
                return;
            }

            Vector3 direction = (_cachedTransform.position - body.worldCenterOfMass).normalized;
            body.AddForce(direction * reelImpulse, ForceMode.Impulse);

            if (Time.time >= _nextFeedbackAt)
            {
                PublishAssessment(new HarpoonAssessment(
                    "HARPOON - REEL IMPULSE APPLIED",
                    $"{body.gameObject.name} is inside safe reel mass at {body.mass:0.0} kg.",
                    "Pull it into reach or keep pressure until it drifts clear.",
                    "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    "HARPOON",
                    "HARPOON REEL IMPULSE",
                    $"{body.gameObject.name} reeled with impulse on {body.mass:0.0} kg target mass.",
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
                return $"HARPOON // RECHARGING {_cooldown:0.0}S";

            if (IsTetherValid())
                return $"HARPOON // TETHER LOCK // {CachedToUpperInvariant(_tetheredName) ?? "TARGET"}";

            if (TryReadAssessment(out HarpoonAssessment assessment))
                return $"HARPOON // {assessment.Headline}";

            return "HARPOON // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return "Winch and launcher are resetting for the next shot.";

            if (IsTetherValid())
                return "Secondary reels the tethered target. Keep distance or break the line if needed.";

            if (TryReadAssessment(out HarpoonAssessment assessment))
                return assessment.Recommendation;

            return "Primary fires and tags a lane. Secondary reels a light target or an active tether.";
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
                "HARPOON",
                message,
                "Secondary reel command failed for the current target.",
                "WARN");
            _nextFeedbackAt = Time.time + feedbackInterval;
        }

        private bool TryReadAssessment(out HarpoonAssessment assessment)
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
                    $"HARPOON - TETHER REEL [{CachedToUpperInvariant(_tetheredName)}]",
                    $"{_tetheredName} remains inside tether control range.",
                    "Keep reeling for control or release to reset the lane.",
                    "INFO"));
                FieldOperationLogSystem.RecordOperation(
                    "HARPOON",
                    "HARPOON TETHER REEL",
                    $"{_tetheredName} reeled through active tether lock.",
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
            _tetherRemaining = 0f;
        }

        private HarpoonAssessment BuildAssessment(Collider target, float distance, bool tetherReady)
        {
            if (target == null)
            {
                return new HarpoonAssessment(
                    "HARPOON - NO TARGET DATA",
                    "Contact data collapsed before assessment completed.",
                    "Sweep a new lane and reacquire.",
                    "WARN");
            }

            HectonBaseAI ai = target.GetComponent<HectonBaseAI>() ?? target.GetComponentInParent<HectonBaseAI>();
            if (ai != null)
            {
                if (ai.IsDead || ai.CurrentHealth <= 0.01f)
                {
                    return new HarpoonAssessment(
                        "HARPOON - TARGET DOWN",
                        $"{ai.gameObject.name} is no longer an active threat.",
                        "Use the line for recovery or switch to salvage.",
                        "INFO");
                }

                if (ai.CurrentState == HectonBaseAI.AIState.Aggressive)
                {
                    return new HarpoonAssessment(
                        tetherReady ? "HARPOON - HOSTILE TETHERED" : "HARPOON - HOSTILE CONTACT",
                        $"{ai.gameObject.name} is aggressive at {distance:0.0} m.",
                        tetherReady ? "Control its movement before it closes distance." : "Confirm the line and prepare to reel or disengage.",
                        "CRITICAL");
                }

                if (ai.HealthNormalized <= 0.35f)
                {
                    return new HarpoonAssessment(
                        tetherReady ? "HARPOON - FRACTURED TARGET TETHERED" : "HARPOON - FRACTURED TARGET",
                        $"{ai.gameObject.name} is weakened and likely to lose control under pressure.",
                        "Reel if you need control, or finish the target quickly.",
                        "WARN");
                }

                return new HarpoonAssessment(
                    tetherReady ? "HARPOON - BIOFORM TETHERED" : "HARPOON - BIOFORM CONTACT",
                    $"{ai.gameObject.name} is under line pressure at {distance:0.0} m.",
                    tetherReady ? "Use the tether to manage spacing and movement." : "Strike cleanly before reeling.",
                    "INFO");
            }

            if (!ToolHitUtility.TryGetRigidbody(target, out Rigidbody body))
            {
                return new HarpoonAssessment(
                    "HARPOON - TARGET CANNOT BE REELED",
                    $"{target.gameObject.name} has no valid mass body for tether control.",
                    "Use cutter, builder, or move on.",
                    "WARN");
            }

            if (body == null || body.isKinematic)
            {
                return new HarpoonAssessment(
                    "HARPOON - TARGET LOCKED TO STRUCTURE",
                    $"{target.gameObject.name} is fixed in place and will not reel.",
                    "Do not waste reel force on anchored structures.",
                    "WARN");
            }

            if (body.mass > maxReelMass)
            {
                if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new HarpoonAssessment(
                    "HARPOON - MASS EXCEEDS REEL LIMIT",
                    $"{target.gameObject.name} weighs {body.mass:0.0} kg at {distance:0.0} m.",
                    "Use propulsion or another route; reel force is not enough.",
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment authoredAssessment))
                return authoredAssessment;

            return new HarpoonAssessment(
                tetherReady ? "HARPOON - CARGO TETHERED" : "HARPOON - CARGO CONTACT",
                $"{target.gameObject.name} is reel-safe at {body.mass:0.0} kg.",
                tetherReady ? "Pull it into position or keep it off your path." : "Fire again only if you need a tether lock.",
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
