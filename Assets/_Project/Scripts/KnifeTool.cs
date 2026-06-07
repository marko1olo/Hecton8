using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Hecton.Localization;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class KnifeTool : PlayerTool
    {
        private const string KnifeCategory = "KNIFE";
        private const string GenericBladeTargetLabel = "FIELD TARGET";
        private const string GenericResourceNodeLabel = "RESOURCE NODE";
        private const string GenericBaseModuleLabel = "BASE MODULE";
        private const byte TemplateStringArg0 = 0x01;
        private const byte TemplateStringArg1 = 0x02;
        private const byte TemplateFloatArg0 = 0x04;
        private const byte TemplateFloatArg1 = 0x08;

        private readonly struct KnifeText
        {
            public readonly string Template;
            public readonly string StringArg0;
            public readonly string StringArg1;
            public readonly float FloatArg0;
            public readonly float FloatArg1;
            public readonly int FloatDecimals;
            public readonly byte ArgumentMask;

            public KnifeText(
                string template,
                string stringArg0,
                string stringArg1,
                float floatArg0,
                float floatArg1,
                int floatDecimals,
                byte argumentMask)
            {
                Template = template;
                StringArg0 = stringArg0;
                StringArg1 = stringArg1;
                FloatArg0 = floatArg0;
                FloatArg1 = floatArg1;
                FloatDecimals = floatDecimals;
                ArgumentMask = argumentMask;
            }

            public static KnifeText Plain(string text)
            {
                return new KnifeText(text, null, null, 0f, 0f, 0, 0);
            }

            public bool TryWrite(ref FixedCharBuffer buffer)
            {
                if (ArgumentMask == 0)
                    return AppendText(ref buffer, Template);

                return TryAppendKnifeTemplate(
                    ref buffer,
                    Template,
                    StringArg0,
                    StringArg1,
                    FloatArg0,
                    FloatArg1,
                    FloatDecimals,
                    ArgumentMask);
            }
        }

        private readonly struct KnifeAssessment
        {
            public readonly KnifeText Headline;
            public readonly KnifeText Summary;
            public readonly KnifeText Recommendation;
            public readonly string Severity;

            public KnifeAssessment(KnifeText headline, KnifeText summary, KnifeText recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public KnifeAssessment(string headline, string summary, string recommendation, string severity)
                : this(KnifeText.Plain(headline), KnifeText.Plain(summary), KnifeText.Plain(recommendation), severity)
            {
            }

            public KnifeAssessment(string headline, KnifeText summary, string recommendation, string severity)
                : this(KnifeText.Plain(headline), summary, KnifeText.Plain(recommendation), severity)
            {
            }

            public KnifeAssessment(KnifeText headline, KnifeText summary, string recommendation, string severity)
                : this(headline, summary, KnifeText.Plain(recommendation), severity)
            {
            }

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                return Headline.TryWrite(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       Summary.TryWrite(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       Recommendation.TryWrite(ref buffer);
            }

            public bool TryWriteHeadline(ref FixedCharBuffer buffer)
            {
                return Headline.TryWrite(ref buffer);
            }

            public bool TryWriteRecommendation(ref FixedCharBuffer buffer)
            {
                return Recommendation.TryWrite(ref buffer);
            }

            public bool TryWriteLogSummary(ref FixedCharBuffer buffer)
            {
                return Summary.TryWrite(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       Recommendation.TryWrite(ref buffer);
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
        [SerializeField] private LayerMask hitMask = Hecton8.Core.HectonLayerMasks.FieldToolSurfaceLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;

        private float _cooldown;
        private float _feedbackCooldownRemaining;
        private uint _hitEvaluationStamp;
        private uint _cachedHitStamp = uint.MaxValue;
        private bool _cachedHitValid;
        private Collider _cachedHitCollider;
        private Vector3 _cachedHitPoint;
        private float _cachedHitDistance;
        private Transform _toolTransform;
        private ILocalizationTextReadModel _localization;
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — survival blade HUD staging buffer — owner: KnifeTool

        private static FixedCharBuffer s_logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — survival blade field log staging buffer — owner: KnifeTool
        private static FixedCharBuffer s_legacySummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - survival blade legacy summary/directive bridge - owner: KnifeTool
        private static FixedCharBuffer s_assessmentTitleBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - survival blade assessment title staging buffer - owner: KnifeTool

        public override void OnSpawn()
        {
            base.OnSpawn();
            _toolTransform = transform;
            _localization = GlobalRegistry.LocalizationText;
            _feedbackCooldownRemaining = 0f;
            InvalidateHitCache();
        }

        public override void OnDespawn()
        {
            _toolTransform = null;
            _localization = null;
            _feedbackCooldownRemaining = 0f;
            InvalidateHitCache();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _toolTransform = transform;
            _localization = GlobalRegistry.LocalizationText;
            InvalidateHitCache();
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryFindBestHit(out Collider bestCollider, out Vector3 bestPoint, out float bestDistance, out Vector3 direction))
            {
                float effectiveDamage = ResolveEffectiveDamage(1f);
                bool applied = ToolHitUtility.ApplyDamage(
                    bestCollider,
                    effectiveDamage,
                    bestPoint,
                    direction,
                    ResolveImpulse(1f),
                    DamageSourceIds.SurvivalBlade,
                    CombatDamageTypes.Impact,
                    0u,
                    0f,
                    ToolCapabilityMasks.Cut);
                if (applied && TryConsumeFeedbackGate())
                {
                    PublishInfoMessage(StableText(LocalizationKeys.KNIFE_HUD_CONTACT, "SURVIVAL BLADE - CONTACT"));
                    RecordContactLog(bestDistance);
                }
            }
            else if (TryConsumeFeedbackGate())
            {
                PublishWarningMessage(StableText(LocalizationKeys.KNIFE_HUD_NO_CONTACT, "SURVIVAL BLADE - NO CONTACT"));
                FieldOperationLogSystem.RecordOperation(
                    StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                    StableText(LocalizationKeys.KNIFE_LOG_CLEAR_TITLE, "MELEE SWING RETURNED CLEAR"),
                    StableText(LocalizationKeys.KNIFE_LOG_CLEAR_MESSAGE, "No valid target entered the blade envelope during the last swing."),
                    "WARN");
            }

            InvalidateHitCache();
            _cooldown = ResolveCooldownSeconds(1f, true);
        }

        public override void ToolTick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - safeDeltaTime);

            if (_feedbackCooldownRemaining > 0f)
                _feedbackCooldownRemaining = math.max(0f, _feedbackCooldownRemaining - safeDeltaTime);

            unchecked
            {
                _hitEvaluationStamp++;
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalSummaryString()
        {
            return KnifeCategory;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "SURVIVAL BLADE // RECOVERING ");
                buffer.AppendFloat(_cooldown, 1);
                AppendText(ref buffer, "S");
                return;
            }

            if (TryGetBestHitCached(out _, out _, out float distance))
            {
                AppendText(ref buffer, "SURVIVAL BLADE // CONTACT ");
                buffer.AppendFloat(distance, 1);
                AppendText(ref buffer, "M");
                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.KNIFE_OPERATIONAL_READY, "SURVIVAL BLADE // READY"));
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Primary swings. Secondary reads the contact before you commit.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, StableText(LocalizationKeys.KNIFE_DIRECTIVE_RECOVERING, "Reset your stance before the next strike."));
                return;
            }

            if (TryGetBestHitCached(out Collider target, out _, out float distance))
            {
                if (TryBuildAssessment(target, distance, out KnifeAssessment assessment))
                {
                    assessment.TryWriteRecommendation(ref buffer);
                    return;
                }

                AppendText(ref buffer, StableText(LocalizationKeys.KNIFE_DIRECTIVE_CONTACT, "Target is inside blade range. Strike or switch tools if the contact is armored."));
                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.KNIFE_DIRECTIVE_READY, "Primary swings. Secondary reads the contact before you commit."));
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryFindBestHit(out Collider target, out Vector3 point, out float distance, out Vector3 direction))
            {
                WarnNoContact(StableText(LocalizationKeys.KNIFE_HUD_NO_TARGET_READ, "SURVIVAL BLADE - NO TARGET READ"));
                _cooldown = ResolveCooldownSeconds(0.5f, false);
                return;
            }

            if (TryPrecisionStrike(target, point, distance, direction))
            {
                InvalidateHitCache();
                _cooldown = ResolveCooldownSeconds(1f, true);
                return;
            }

            ShowTacticalReadout(target, distance);
            InvalidateHitCache();
            _cooldown = ResolveCooldownSeconds(0.5f, false);
        }

        private bool TryFindBestHit(out Collider bestCollider, out Vector3 bestPoint, out float bestDistance, out Vector3 direction)
        {
            direction = default;
            if (!TryResolveKnifeRay(out Vector3 origin, out direction))
            {
                bestCollider = null;
                bestPoint = default;
                bestDistance = 0f;
                return false;
            }

            return TryFindBestHit(origin, direction, out bestCollider, out bestPoint, out bestDistance);
        }

        private bool TryFindBestHit(Vector3 origin, Vector3 direction, out Collider bestCollider, out Vector3 bestPoint, out float bestDistance)
        {
            bestCollider = null;
            float queryRange = ResolveQueryRange();
            bestPoint = origin + direction * queryRange;
            bestDistance = queryRange;
            if (queryRange <= 0f)
                return false;

            if (!RequestPrimarySurfaceHit(origin, direction, queryRange, ResolveHitSurfaceMask(), QueryTriggerInteraction.Ignore, out InteractionSurfaceHit hit))
                return false;

            Collider candidate = hit.collider;
            if (candidate == null || IsOwnToolCollider(candidate))
            {
                return false;
            }

            bestCollider = candidate;
            bestPoint = hit.point;
            bestDistance = hit.distance;

            return bestCollider != null;
        }

        private int ResolveHitSurfaceMask()
        {
            return HectonLayerMasks.ResolveSurfaceInteractionLayerMask(hitMask.value);
        }

        private bool TryGetBestHitCached(out Collider bestCollider, out Vector3 bestPoint, out float bestDistance)
        {
            uint currentStamp = _hitEvaluationStamp;
            if (_cachedHitStamp == currentStamp)
            {
                bestCollider = _cachedHitCollider;
                bestPoint = _cachedHitPoint;
                bestDistance = _cachedHitDistance;
                return _cachedHitValid;
            }

            bool valid = TryFindBestHit(out bestCollider, out bestPoint, out bestDistance, out _);
            _cachedHitStamp = currentStamp;
            _cachedHitValid = valid;
            _cachedHitCollider = bestCollider;
            _cachedHitPoint = bestPoint;
            _cachedHitDistance = bestDistance;
            return valid;
        }

        private bool TryPrecisionStrike(Collider target, Vector3 point, float distance, Vector3 direction)
        {
            float normalized = GetTargetNormalizedVital(target);
            if (normalized < 0f || normalized > criticalHealthThreshold)
                return false;

            float effectiveDamage = ResolveEffectiveDamage(precisionStrikeMultiplier);
            bool applied = ToolHitUtility.ApplyDamage(
                target,
                effectiveDamage,
                point,
                direction,
                ResolveImpulse(1.5f),
                DamageSourceIds.SurvivalBlade,
                CombatDamageTypes.Impact,
                0u,
                0f,
                ToolCapabilityMasks.Cut);
            if (!applied)
                return false;

            if (TryConsumeFeedbackGate())
            {
                PublishInfoMessage(StableText(LocalizationKeys.KNIFE_HUD_PRECISION_STRIKE, "SURVIVAL BLADE - PRECISION STRIKE"));
                RecordPrecisionLog(distance);
            }

            return true;
        }

        private void ShowTacticalReadout(Collider target, float distance)
        {
            if (target == null)
                return;

            if (TryBuildDescriptorAssessment(target, distance, out KnifeAssessment descriptorAssessment))
            {
                PublishAssessment(descriptorAssessment);
                RecordAssessmentLog(descriptorAssessment);
                ArmFeedbackCooldown();
                return;
            }

            ResourceNode node = FindResourceNodeAdapter(target);
            if (node != null)
            {
                KnifeAssessment assessment = BuildResourceAssessment(node, distance);
                PublishAssessment(assessment);
                RecordAssessmentLog(assessment);
                ArmFeedbackCooldown();
                return;
            }

            BaseModule module = FindBaseModuleAdapter(target);
            if (module != null)
            {
                KnifeAssessment assessment = BuildModuleAssessment(module, distance);
                PublishAssessment(assessment);
                RecordAssessmentLog(assessment);
                ArmFeedbackCooldown();
                return;
            }

            if (TryConsumeFeedbackGate())
            {
                PublishInfoMessage(StableText(LocalizationKeys.KNIFE_HUD_TARGET_PROFILE_UNKNOWN, "SURVIVAL BLADE - TARGET PROFILE UNKNOWN"));
                RecordUnknownProfileLog();
            }
        }

        private float GetTargetNormalizedVital(Collider target)
        {
            if (target == null)
                return -1f;

            ResourceNode node = FindResourceNodeAdapter(target);
            if (node != null)
                return node.HealthNormalized;

            BaseModule module = FindBaseModuleAdapter(target);
            if (module != null && module.MaxIntegrity > 0f)
                return module.CurrentIntegrity / module.MaxIntegrity;

            return -1f;
        }

        private bool TryResolveKnifeRay(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(forward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f)
            {
                return false;
            }

            float invForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            direction = new Vector3(
                forward.x * invForwardLength,
                forward.y * invForwardLength,
                forward.z * invForwardLength);
            return true;
        }

        private bool IsOwnToolCollider(Collider candidate)
        {
            if (candidate == null)
                return true;

            Transform candidateTransform = candidate.transform;
            Transform toolTransform = _toolTransform != null ? _toolTransform : transform;
            return toolTransform != null &&
                   candidateTransform != null &&
                   (ReferenceEquals(candidateTransform, toolTransform) ||
                    candidateTransform.IsChildOf(toolTransform));
        }

        private bool TryConsumeFeedbackGate()
        {
            if (_feedbackCooldownRemaining > 0f)
                return false;

            ArmFeedbackCooldown();
            return true;
        }

        private void ArmFeedbackCooldown()
        {
            _feedbackCooldownRemaining = ResolveFeedbackInterval();
        }

        private float ResolveFeedbackInterval()
        {
            return math.isfinite(feedbackInterval)
                ? math.max(0.05f, feedbackInterval)
                : 0.35f;
        }

        private float ResolveQueryRange()
        {
            float safeRange = math.isfinite(range) ? math.max(0f, range) : 0f;
            float safeRadius = math.isfinite(radius) ? math.max(0f, radius) : 0f;
            return safeRange + safeRadius;
        }

        private float ResolveCooldownSeconds(float multiplier, bool scaleBySpeed)
        {
            float safeSwingCooldown = math.isfinite(swingCooldown) ? math.max(0f, swingCooldown) : 0f;
            float safeMultiplier = math.isfinite(multiplier) ? math.max(0f, multiplier) : 0f;
            float cooldown = safeSwingCooldown * safeMultiplier;
            if (!scaleBySpeed)
                return cooldown;

            float speed = GetSpeed();
            float safeSpeed = math.isfinite(speed) ? math.max(0.25f, speed) : 0.25f;
            return cooldown / safeSpeed;
        }

        private float ResolveEffectiveDamage(float multiplier)
        {
            float safeDamage = math.isfinite(damage) ? math.max(0f, damage) : 0f;
            float safeMultiplier = math.isfinite(multiplier) ? math.max(0f, multiplier) : 0f;
            float efficiency = GetEfficiency();
            float safeEfficiency = math.isfinite(efficiency) ? math.max(0f, efficiency) : 0f;
            return safeDamage * safeMultiplier * safeEfficiency;
        }

        private float ResolveImpulse(float multiplier)
        {
            float safeImpulse = math.isfinite(impulse) ? math.max(0f, impulse) : 0f;
            float safeMultiplier = math.isfinite(multiplier) ? math.max(0f, multiplier) : 0f;
            return safeImpulse * safeMultiplier;
        }

        private void InvalidateHitCache()
        {
            _cachedHitStamp = uint.MaxValue;
            _cachedHitValid = false;
            _cachedHitCollider = null;
            _cachedHitPoint = default;
            _cachedHitDistance = 0f;
        }

        private void WarnNoContact(string message)
        {
            if (!TryConsumeFeedbackGate())
                return;

            PublishWarningMessage(message);
            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                message,
                StableText(LocalizationKeys.KNIFE_LOG_NO_TARGET_READ_MESSAGE, "No valid target entered the blade envelope during the tactical read."),
                "WARN");
        }

        private bool TryBuildAssessment(Collider target, float distance, out KnifeAssessment assessment)
        {
            assessment = default;

            if (TryBuildDescriptorAssessment(target, distance, out assessment))
            {
                return true;
            }

            ResourceNode node = FindResourceNodeAdapter(target);
            if (node != null)
            {
                assessment = BuildResourceAssessment(node, distance);
                return true;
            }

            BaseModule module = FindBaseModuleAdapter(target);
            if (module != null)
            {
                assessment = BuildModuleAssessment(module, distance);
                return true;
            }

            return false;
        }

        private static ResourceNode FindResourceNodeAdapter(Collider target)
        {
            if (target == null)
                return null;

            if (InteractableRegistry.TryResolve(target, out InteractableRegistry.TargetInfo targetInfo) &&
                targetInfo.ResourceNode != null)
            {
                return targetInfo.ResourceNode;
            }

            return null;
        }

        private static BaseModule FindBaseModuleAdapter(Collider target)
        {
            if (target == null)
                return null;

            if (InteractableRegistry.TryResolve(target, out InteractableRegistry.TargetInfo targetInfo) &&
                targetInfo.BaseModule != null)
            {
                return targetInfo.BaseModule;
            }

            return null;
        }

        private KnifeAssessment BuildResourceAssessment(ResourceNode node, float distance)
        {
            if (node.IsDepleted)
            {
                return new KnifeAssessment(
                    StableText(LocalizationKeys.KNIFE_HEADLINE_NODE_DEPLETED, "BLADE READ - NODE DEPLETED 0%"),
                    CreateStringFloatText(
                        StableText(LocalizationKeys.KNIFE_SUMMARY_NODE_DEPLETED, "{0} is exhausted at {1:0.0} m and will not pay back another strike."),
                        GenericResourceNodeLabel,
                        distance,
                        1),
                    StableText(LocalizationKeys.KNIFE_RECOMMEND_NODE_DEPLETED, "Leave it and move to a fresh resource lane."),
                    "WARN");
            }

            float nodePercent = node.HealthNormalized * 100f;
            if (node.HealthNormalized <= criticalHealthThreshold)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        StableText(LocalizationKeys.KNIFE_HEADLINE_NODE_READY, "BLADE READ - NODE READY TO BREAK {0:0}%"),
                        nodePercent,
                        0),
                    CreateStringFloatText(
                        StableText(LocalizationKeys.KNIFE_SUMMARY_NODE_READY, "{0} is one clean strike away from opening at {1:0.0} m."),
                        GenericResourceNodeLabel,
                        distance,
                        1),
                    StableText(LocalizationKeys.KNIFE_RECOMMEND_NODE_READY, "Finish it now if you want a fast recovery window."),
                    "INFO");
            }

            if (node.HealthNormalized <= 0.65f)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        StableText(LocalizationKeys.KNIFE_HEADLINE_NODE_WEAKENED, "BLADE READ - NODE WEAKENED {0:0}%"),
                        nodePercent,
                        0),
                    CreateSingleStringText(
                        StableText(LocalizationKeys.KNIFE_SUMMARY_NODE_WEAKENED, "{0} is partially cracked and reacting to tool pressure."),
                        GenericResourceNodeLabel),
                    StableText(LocalizationKeys.KNIFE_RECOMMEND_NODE_WEAKENED, "Another strike or a dedicated extraction tool is worthwhile."),
                    "INFO");
            }

            return new KnifeAssessment(
                CreateSingleFloatText(
                    StableText(LocalizationKeys.KNIFE_HEADLINE_NODE_DENSE, "BLADE READ - NODE DENSE {0:0}%"),
                    nodePercent,
                    0),
                CreateStringFloatText(
                    StableText(LocalizationKeys.KNIFE_SUMMARY_NODE_DENSE, "{0} still has a dense shell at {1:0.0} m."),
                    GenericResourceNodeLabel,
                    distance,
                    1),
                StableText(LocalizationKeys.KNIFE_RECOMMEND_NODE_DENSE, "Use repeated strikes only if no better extraction tool is available."),
                "INFO");
        }

        private KnifeAssessment BuildModuleAssessment(BaseModule module, float distance)
        {
            float normalized = module.MaxIntegrity > 0f ? module.CurrentIntegrity / module.MaxIntegrity : 0f;
            if (module.IsBreached)
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        StableText(LocalizationKeys.KNIFE_HEADLINE_MODULE_BREACHED, "BLADE READ - MODULE BREACHED {0:0}%"),
                        normalized * 100f,
                        0),
                    CreateStringFloatText(
                        StableText(LocalizationKeys.KNIFE_SUMMARY_MODULE_BREACHED, "{0} is already compromised and unsafe at {1:0.0} m."),
                        GenericBaseModuleLabel,
                        distance,
                        1),
                    StableText(LocalizationKeys.KNIFE_RECOMMEND_MODULE_BREACHED, "Repair, salvage, or leave it. The blade is not the main tool here."),
                    "WARN");
            }

            if (module.CanDeconstruct())
            {
                return new KnifeAssessment(
                    CreateSingleFloatText(
                        StableText(LocalizationKeys.KNIFE_HEADLINE_MODULE_SALVAGEABLE, "BLADE READ - MODULE SALVAGEABLE {0:0}%"),
                        normalized * 100f,
                        0),
                    CreateSingleStringText(
                        StableText(LocalizationKeys.KNIFE_SUMMARY_MODULE_SALVAGEABLE, "{0} exposes reclaim paths, but not for blade work."),
                        GenericBaseModuleLabel),
                    StableText(LocalizationKeys.KNIFE_RECOMMEND_MODULE_SALVAGEABLE, "Swap to the cutter if recovery is the goal."),
                    "INFO");
            }

            return new KnifeAssessment(
                CreateSingleFloatText(
                    StableText(LocalizationKeys.KNIFE_HEADLINE_MODULE_SEALED, "BLADE READ - MODULE SEALED {0:0}%"),
                    normalized * 100f,
                    0),
                CreateSingleStringText(
                    StableText(LocalizationKeys.KNIFE_SUMMARY_MODULE_SEALED, "{0} is structurally sealed and not a valid blade target."),
                    GenericBaseModuleLabel),
                StableText(LocalizationKeys.KNIFE_RECOMMEND_MODULE_SEALED, "Use repair, builder, or cutter tools instead."),
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

        private void RecordContactLog(float distance)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendStringFloatTemplate(
                    ref s_logSummaryBuffer,
                    StableText(LocalizationKeys.KNIFE_LOG_CONTACT_MESSAGE, "{0} engaged at {1:0.0} m."),
                    GenericBladeTargetLabel,
                    distance,
                    1))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " engaged at ");
                s_logSummaryBuffer.AppendFloat(distance, 1);
                AppendText(ref s_logSummaryBuffer, " m.");
            }

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                StableText(LocalizationKeys.KNIFE_LOG_CONTACT_TITLE, "MELEE CONTACT REGISTERED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        private void RecordPrecisionLog(float distance)
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendStringFloatTemplate(
                    ref s_logSummaryBuffer,
                    StableText(LocalizationKeys.KNIFE_LOG_PRECISION_MESSAGE, "{0} finished or weakened at {1:0.0} m."),
                    GenericBladeTargetLabel,
                    distance,
                    1))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " finished or weakened at ");
                s_logSummaryBuffer.AppendFloat(distance, 1);
                AppendText(ref s_logSummaryBuffer, " m.");
            }

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                StableText(LocalizationKeys.KNIFE_LOG_PRECISION_TITLE, "PRECISION STRIKE CONFIRMED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        private void RecordAssessmentLog(KnifeAssessment assessment)
        {
            s_logSummaryBuffer.Clear();
            assessment.TryWriteLogSummary(ref s_logSummaryBuffer);
            s_assessmentTitleBuffer.Clear();
            assessment.TryWriteHeadline(ref s_assessmentTitleBuffer);

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                in s_assessmentTitleBuffer,
                in s_logSummaryBuffer,
                assessment.Severity);
        }

        private void RecordUnknownProfileLog()
        {
            s_logSummaryBuffer.Clear();
            if (!TryAppendSingleStringTemplate(
                    ref s_logSummaryBuffer,
                    StableText(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_MESSAGE, "{0} does not expose a tactical vitality profile."),
                    GenericBladeTargetLabel))
            {
                s_logSummaryBuffer.Clear();
                AppendText(ref s_logSummaryBuffer, GenericBladeTargetLabel);
                AppendText(ref s_logSummaryBuffer, " does not expose a tactical vitality profile.");
            }

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.KNIFE_CATEGORY, KnifeCategory),
                StableText(LocalizationKeys.KNIFE_LOG_UNKNOWN_PROFILE_TITLE, "UNKNOWN TARGET PROFILE"),
                in s_logSummaryBuffer,
                "WARN");
        }

        private static void PublishAssessment(KnifeAssessment assessment)
        {
            s_hudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref s_hudBuffer))
                return;

            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishInfoMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishWarningMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowWarning(in s_hudBuffer);
        }

        private string StableText(string key, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private static KnifeText CreateSingleFloatText(string template, float value, int decimals)
        {
            return new KnifeText(template, null, null, value, 0f, decimals, TemplateFloatArg0);
        }

        private static KnifeText CreateSingleStringText(string template, string value)
        {
            return new KnifeText(template, value, null, 0f, 0f, 0, TemplateStringArg0);
        }

        private static KnifeText CreateStringFloatText(string template, string stringValue, float floatValue, int decimals)
        {
            return new KnifeText(template, stringValue, null, 0f, floatValue, decimals, TemplateStringArg0 | TemplateFloatArg1);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool TryAppendSingleStringTemplate(ref FixedCharBuffer buffer, string template, string value)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                value,
                null,
                0f,
                0f,
                0,
                TemplateStringArg0);
        }

        private static bool TryAppendTwoStringTemplate(ref FixedCharBuffer buffer, string template, string value0, string value1)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                value0,
                value1,
                0f,
                0f,
                0,
                TemplateStringArg0 | TemplateStringArg1);
        }

        private static bool TryAppendSingleFloatTemplate(ref FixedCharBuffer buffer, string template, float value, int decimals)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                null,
                null,
                value,
                0f,
                decimals,
                TemplateFloatArg0);
        }

        private static bool TryAppendStringFloatTemplate(ref FixedCharBuffer buffer, string template, string stringValue, float floatValue, int decimals)
        {
            return TryAppendKnifeTemplate(
                ref buffer,
                template,
                stringValue,
                null,
                0f,
                floatValue,
                decimals,
                TemplateStringArg0 | TemplateFloatArg1);
        }

        private static bool TryAppendKnifeTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            string stringArg1,
            float floatArg0,
            float floatArg1,
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
                    '0' when (argumentMask & TemplateStringArg0) != 0 => AppendText(ref buffer, stringArg0),
                    '0' when (argumentMask & TemplateFloatArg0) != 0 => buffer.AppendFloat(floatArg0, floatDecimals),
                    '1' when (argumentMask & TemplateStringArg1) != 0 => AppendText(ref buffer, stringArg1),
                    '1' when (argumentMask & TemplateFloatArg1) != 0 => buffer.AppendFloat(floatArg1, floatDecimals),
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
}

