using Hecton8.Core;
using Hecton.Localization;
using Hecton8.Interaction;
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
        private const byte TemplateStringArg0 = 0x01;
        private const byte TemplateStringArg1 = 0x02;
        private const byte TemplateFloatArg0 = 0x04;

        private readonly struct StunText
        {
            public readonly string Template;
            public readonly string StringArg0;
            public readonly string StringArg1;
            public readonly float FloatArg0;
            public readonly int FloatDecimals;
            public readonly byte ArgumentMask;

            public StunText(
                string template,
                string stringArg0,
                string stringArg1,
                float floatArg0,
                int floatDecimals,
                byte argumentMask)
            {
                Template = template;
                StringArg0 = stringArg0;
                StringArg1 = stringArg1;
                FloatArg0 = floatArg0;
                FloatDecimals = floatDecimals;
                ArgumentMask = argumentMask;
            }

            public static StunText Plain(string text)
            {
                return new StunText(text, null, null, 0f, 0, 0);
            }

            public bool TryWrite(ref FixedCharBuffer buffer)
            {
                if (ArgumentMask == 0)
                    return AppendText(ref buffer, Template);

                return TryAppendStunTemplate(
                    ref buffer,
                    Template,
                    StringArg0,
                    StringArg1,
                    FloatArg0,
                    FloatDecimals,
                    ArgumentMask);
            }
        }

        private readonly struct StunAssessment
        {
            public readonly StunText Headline;
            public readonly StunText Summary;
            public readonly StunText Recommendation;
            public readonly string Severity;

            public StunAssessment(StunText headline, StunText summary, StunText recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public StunAssessment(string headline, string summary, string recommendation, string severity)
                : this(StunText.Plain(headline), StunText.Plain(summary), StunText.Plain(recommendation), severity)
            {
            }

            public StunAssessment(string headline, StunText summary, string recommendation, string severity)
                : this(StunText.Plain(headline), summary, StunText.Plain(recommendation), severity)
            {
            }

            public StunAssessment(StunText headline, StunText summary, string recommendation, string severity)
                : this(headline, summary, StunText.Plain(recommendation), severity)
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

        [Header("Stun Shot")]
        [SerializeField] private float range = 22f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float impulse = 9f;
        [SerializeField] private float stunDuration = 2.5f;
        [SerializeField] private float shotCooldown = 0.6f;
        [SerializeField] private LayerMask targetMask = Hecton8.Core.HectonLayerMasks.FieldToolSurfaceLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;

        private float _cooldown;
        private float _feedbackCooldownRemaining;
        private bool _secondaryLatched;
        private bool _cachedAssessmentValid;
        private StunAssessment _cachedAssessment;
        private ILocalizationTextReadModel _localization;
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol HUD staging buffer - owner: StunPistolTool
        private static FixedCharBuffer s_logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol field log staging buffer - owner: StunPistolTool
        private static FixedCharBuffer s_logTitleBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - stun pistol log title staging buffer - owner: StunPistolTool
        private static FixedCharBuffer s_legacySummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - stun pistol legacy summary/directive bridge - owner: StunPistolTool

        public override void OnSpawn()
        {
            base.OnSpawn();
            RefreshLocalization(GlobalRegistry.LocalizationText);
            _cooldown = 0f;
            _feedbackCooldownRemaining = 0f;
            _secondaryLatched = false;
            InvalidateAssessmentCache();
        }

        public override void OnDespawn()
        {
            _localization = null;
            _cooldown = 0f;
            _feedbackCooldownRemaining = 0f;
            _secondaryLatched = false;
            InvalidateAssessmentCache();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            RefreshLocalization(GlobalRegistry.LocalizationText);
            InvalidateAssessmentCache();
        }

        protected override void OnToolRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
                RefreshLocalization(currentService as ILocalizationTextReadModel);
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
                RefreshLocalization(currentService as ILocalizationTextReadModel);
        }

        private void RefreshLocalization(ILocalizationTextReadModel localization)
        {
            if (ReferenceEquals(_localization, localization))
                return;

            _localization = localization;
            InvalidateAssessmentCache();
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryQueueTargetHit(out InteractionSurfaceHit hit, out Vector3 shotDirection))
            {
                float effectiveDamage = ResolveEffectiveDamage();
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    effectiveDamage,
                    hit.point,
                    shotDirection,
                    ResolveImpulse(),
                    DamageSourceIds.StunPistol,
                    CombatDamageTypes.Emp,
                    CombatStatusBits.Stunned,
                    ResolveStunDuration(),
                    ToolCapabilityMasks.Stun);

                if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
                {
                    StoreAssessment(descriptorAssessment);
                    if (TryConsumeFeedbackGate())
                    {
                        PublishAssessment(descriptorAssessment);
                        RecordAssessmentLog(descriptorAssessment);
                    }
                }
                else if (TryConsumeFeedbackGate())
                {
                    InvalidateAssessmentCache();
                    PublishWarningMessage(StableText(LocalizationKeys.STUN_HUD_NO_BIOFORM_CIRCUIT, "STUN PISTOL - NO BIOFORM CIRCUIT"));
                    RecordNonBioformLog();
                }
            }
            else if (TryConsumeFeedbackGate())
            {
                InvalidateAssessmentCache();
                PublishWarningMessage(StableText(LocalizationKeys.STUN_HUD_NO_TARGET_LOCK, "STUN PISTOL - NO TARGET LOCK"));
                FieldOperationLogSystem.RecordOperation(
                    StableText(LocalizationKeys.STUN_CATEGORY, StunCategory),
                    StableText(LocalizationKeys.STUN_LOG_CLEAR_TITLE, "STUN SHOT RETURNED CLEAR"),
                    StableText(LocalizationKeys.STUN_LOG_CLEAR_MESSAGE, "No valid target was present in the stun pistol engagement cone."),
                    "WARN");
            }

            _cooldown = ResolveShotCooldown();
        }

        public override void ToolTick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - safeDeltaTime);

            if (_feedbackCooldownRemaining > 0f)
                _feedbackCooldownRemaining = math.max(0f, _feedbackCooldownRemaining - safeDeltaTime);

            PlayerInputState inputState = TryGetInputService(out IInputService inputService) && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (_secondaryLatched && !inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalSummaryString()
        {
            return StunCategory;
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

            if (TryReadCachedAssessmentSnapshot(out StunAssessment assessment))
            {
                AppendText(ref buffer, "STUN PISTOL // ");
                assessment.TryWriteHeadline(ref buffer);
                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.STUN_OPERATIONAL_READY, "STUN PISTOL // READY"));
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Primary disrupts. Secondary checks whether the target is worth stunning.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, StableText(LocalizationKeys.STUN_DIRECTIVE_RECHARGING, "Capacitors are recharging for the next disruption shot."));
                return;
            }

            if (TryReadCachedAssessmentSnapshot(out StunAssessment assessment))
            {
                assessment.TryWriteRecommendation(ref buffer);
                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.STUN_DIRECTIVE_READY, "Primary disrupts. Secondary checks whether the target is worth stunning."));
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f || _secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!TryQueueTargetHit(out InteractionSurfaceHit hit))
            {
                WarnSecondary(StableText(LocalizationKeys.STUN_HUD_NO_TARGET_LOCK, "STUN PISTOL - NO TARGET LOCK"));
                InvalidateAssessmentCache();
                return;
            }

            if (TryBuildDescriptorAssessment(hit.collider, hit.distance, out StunAssessment descriptorAssessment))
            {
                StoreAssessment(descriptorAssessment);
                PublishAssessment(descriptorAssessment);
                RecordAssessmentLog(descriptorAssessment);
                ArmFeedbackCooldown();
                return;
            }

            WarnSecondary(StableText(LocalizationKeys.STUN_HUD_TARGET_NO_BIO_CIRCUIT, "STUN PISTOL - TARGET HAS NO BIO CIRCUIT"));
            InvalidateAssessmentCache();
        }

        private void WarnSecondary(string message)
        {
            if (!TryConsumeFeedbackGate())
                return;

            PublishWarningMessage(message);
            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.STUN_CATEGORY, StunCategory),
                message,
                StableText(LocalizationKeys.STUN_LOG_SECONDARY_FAILED, "Secondary target check could not confirm a valid disruption candidate."),
                "WARN");
        }

        private bool TryReadCachedAssessmentSnapshot(out StunAssessment assessment)
        {
            assessment = _cachedAssessment;
            return _cachedAssessmentValid;
        }

        private void StoreAssessment(StunAssessment assessment)
        {
            _cachedAssessment = assessment;
            _cachedAssessmentValid = true;
        }

        private void InvalidateAssessmentCache()
        {
            _cachedAssessmentValid = false;
            _cachedAssessment = default;
        }

        private bool TryQueueTargetHit(out InteractionSurfaceHit hit)
        {
            Vector3 unusedDirection;
            return TryQueueTargetHit(out hit, out unusedDirection);
        }

        private bool TryQueueTargetHit(out InteractionSurfaceHit hit, out Vector3 direction)
        {
            hit = default;
            direction = default;
            if (!TryResolveStunRay(out Vector3 origin, out direction))
                return false;

            return RequestPrimarySurfaceHit(
                origin,
                direction,
                ResolveRuntimeRange(),
                ResolveTargetSurfaceMask(),
                QueryTriggerInteraction.Ignore,
                out hit);
        }

        private int ResolveTargetSurfaceMask()
        {
            return HectonLayerMasks.ResolveSurfaceInteractionLayerMask(targetMask.value);
        }

        private bool TryResolveStunRay(out Vector3 origin, out Vector3 direction)
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

        private float ResolveRuntimeRange()
        {
            return math.isfinite(range) ? math.max(0f, range) : 0f;
        }

        private float ResolveShotCooldown()
        {
            return math.isfinite(shotCooldown) ? math.max(0f, shotCooldown) : 0f;
        }

        private float ResolveEffectiveDamage()
        {
            float safeDamage = math.isfinite(damage) ? math.max(0f, damage) : 0f;
            float efficiency = GetEfficiency();
            float safeEfficiency = math.isfinite(efficiency) ? math.max(0f, efficiency) : 0f;
            return safeDamage * safeEfficiency;
        }

        private float ResolveImpulse()
        {
            return math.isfinite(impulse) ? math.max(0f, impulse) : 0f;
        }

        private float ResolveStunDuration()
        {
            return math.isfinite(stunDuration) ? math.max(0f, stunDuration) : 0f;
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

        private void RecordAssessmentLog(StunAssessment assessment)
        {
            s_logSummaryBuffer.Clear();
            assessment.TryWriteLogSummary(ref s_logSummaryBuffer);
            s_logTitleBuffer.Clear();
            assessment.TryWriteHeadline(ref s_logTitleBuffer);

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.STUN_CATEGORY, StunCategory),
                in s_logTitleBuffer,
                in s_logSummaryBuffer,
                assessment.Severity);
        }

        private void RecordNonBioformLog()
        {
            s_logSummaryBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_logSummaryBuffer,
                StableText(
                    LocalizationKeys.STUN_LOG_NON_BIOFORM_MESSAGE,
                    "{0} absorbed a stun shot without a compatible AI circuit."),
                GenericFieldTargetLabel);

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.STUN_CATEGORY, StunCategory),
                StableText(LocalizationKeys.STUN_LOG_NON_BIOFORM_TITLE, "STUN CHECK REJECTED TARGET"),
                in s_logSummaryBuffer,
                "WARN");
        }

        internal static void RecordRecoveryLog(ILocalizationTextReadModel localization)
        {
            s_logSummaryBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_logSummaryBuffer,
                StableText(localization, LocalizationKeys.STUN_LOG_RECOVERED_MESSAGE, "{0} recovered from disruption and resumed activity."),
                GenericBioformLabel);

            FieldOperationLogSystem.RecordOperation(
                StableText(localization, LocalizationKeys.STUN_CATEGORY, StunCategory),
                StableText(localization, LocalizationKeys.STUN_LOG_RECOVERED_TITLE, "BIOFORM RECOVERED"),
                in s_logSummaryBuffer,
                "INFO");
        }

        private string StableText(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localization;
            return StableText(manager, key, fallback);
        }

        private static string StableText(ILocalizationTextReadModel manager, string key, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private static StunText CreateSingleStringText(string template, string value)
        {
            return new StunText(template, value, null, 0f, 0, TemplateStringArg0);
        }

        private static StunText CreateSingleFloatText(string template, float value, int decimals)
        {
            float safeValue = math.isfinite(value) ? value : 0f;
            int safeDecimals = math.max(0, decimals);
            return new StunText(template, null, null, safeValue, safeDecimals, TemplateFloatArg0);
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
                TemplateStringArg0);
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
                TemplateStringArg0 | TemplateStringArg1);
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
                    '0' when (argumentMask & TemplateStringArg0) != 0 => AppendText(ref buffer, stringArg0),
                    '0' when (argumentMask & TemplateFloatArg0) != 0 => buffer.AppendFloat(floatArg0, floatDecimals),
                    '1' when (argumentMask & TemplateStringArg1) != 0 => AppendText(ref buffer, stringArg1),
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

    public sealed class StunTargetRuntime : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private ILocalizationTextReadModel _localization;
        private float _remaining;
        private bool _armed;
        private bool _registeredToTickManager;
        private bool _hotSwapRegistered;

        public float RemainingTime => _remaining;
        public bool IsArmed => _armed;

        public bool TryApply(float duration)
        {
            if (!_registeredToTickManager)
                return false;

            float safeDuration = math.isfinite(duration) ? math.max(0f, duration) : 0f;
            if (safeDuration <= 0f)
                return false;

            _remaining = math.max(_remaining, safeDuration);
            _armed = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_armed)
                return;

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _remaining = math.max(0f, _remaining - safeDeltaTime);
            if (_remaining > 0f)
                return;

            LogRecovery();
            _armed = false;
            _remaining = 0f;
        }

        private void OnEnable()
        {
            _localization = GlobalRegistry.LocalizationText;
            TryRegisterHotSwapListener();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            _armed = false;
            _remaining = 0f;
            _localization = null;
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterFromTickManager();
                    if (currentService != null && isActiveAndEnabled)
                        RegisterToTickManager();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
            }
        }

        private void LogRecovery()
        {
            StunPistolTool.RecordRecoveryLog(_localization);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
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

