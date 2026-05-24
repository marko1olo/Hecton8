using System;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Items;
using Hecton.Localization;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SalvageSamplerTool : PlayerTool
    {
        private const string SamplerCategory = "SALVAGE";
        private const string SamplerNoTargetHeadline = "NO TARGET";
        private const string SamplerRecoveryReadyHeadline = "RECOVERY READY";
        private const string SamplerNodeDepletedHeadline = "NODE DEPLETED";
        private const string SamplerProcessTargetHeadline = "PROCESS TARGET";
        private const string SamplerInvalidTargetHeadline = "INVALID TARGET";
        private const byte TemplateStringArg0 = 1 << 0;
        private const byte TemplateIntArg1 = 1 << 1;
        private const byte TemplateFloatArg0 = 1 << 2;

        private struct SamplerDiagnosis
        {
            public string headline;
            public string summary;
            public string severity;
        }

        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - sampler HUD staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_logTitleBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - sampler operation log title staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - sampler operation log summary staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_archiveIdBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - sampler scan archive id staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_archiveTitleBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - sampler scan archive title staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_archiveSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - sampler scan archive summary staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_diagnosisTextBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - sampler diagnosis dynamic text staging buffer - owner: SalvageSamplerTool
        private static FixedCharBuffer s_legacySummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - sampler legacy summary/directive bridge staging buffer - owner: SalvageSamplerTool
        [Header("Sampling")]
        [SerializeField] private float samplingRange = 3.2f;
        [SerializeField] private float sampleDamage = 18f;
        [SerializeField] private float sampleImpulse = 1.5f;
        [SerializeField] private float sampleCooldown = 0.3f;
        [SerializeField] private LayerMask samplingMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private float feedbackInterval = 0.45f;

        private float _cooldown;
        private float _feedbackCooldownRemaining;
        private bool _secondaryLatched;
        private uint _diagnosisEvaluationStamp;
        private uint _cachedDiagnosisStamp = uint.MaxValue;
        private bool _cachedDiagnosisValid;
        private SamplerDiagnosis _cachedDiagnosis;
        private ScanLogSystem _scanLog;
        private LocalizationManager _localization;

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheColdDependencies();
            InvalidateDiagnosisCache();
        }

        public override void OnDespawn()
        {
            _scanLog = null;
            _localization = null;
            _feedbackCooldownRemaining = 0f;
            _secondaryLatched = false;
            InvalidateDiagnosisCache();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            CacheColdDependencies();
            InvalidateDiagnosisCache();
        }

        private void CacheColdDependencies()
        {
            _scanLog = GlobalRegistry.ScanLog;
            _localization = Hecton.Localization.LocalizationManager.ActiveRuntimeInstance;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetSamplingHit(out RaycastHit hit, out Vector3 sampleDirection))
            {
                float effectiveDamage = sampleDamage * GetEfficiency();
                bool applied = ToolHitUtility.ApplyDamage(
                    hit.collider,
                    effectiveDamage,
                    hit.point,
                    sampleDirection,
                    sampleImpulse);

                if (!applied && TryConsumeFeedbackGate())
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_VIABLE_TARGET, "SAMPLER - NO VIABLE TARGET"));
                }
                else if (applied && TryConsumeFeedbackGate())
                {
                    PublishInfoMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_EXTRACTION_IN_PROGRESS, "SAMPLER - EXTRACTION IN PROGRESS"));
                }
            }
            else if (TryConsumeFeedbackGate())
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_TARGET_LOCK, "SAMPLER - NO TARGET LOCK"));
            }

            InvalidateDiagnosisCache();
            _cooldown = sampleCooldown / math.max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetSamplingHit(out RaycastHit hit, out _) &&
                TryResolveInteractorRoot(out Transform interactorRoot))
            {
                bool collected = ToolHitUtility.TryCollectItem(hit.collider, interactorRoot, out ItemData recoveredItem);
                if (collected)
                {
                    ArchiveRecoveredItem(recoveredItem);
                    s_logSummaryBuffer.Clear();
                    if (recoveredItem != null)
                    {
                        TryAppendSingleStringTemplate(
                            ref s_logSummaryBuffer,
                            ResolveLocalized(
                                LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_MESSAGE,
                                "Sampler retrieved {0} from a recoverable field target."),
                            recoveredItem.itemName,
                            false);
                    }
                    else
                    {
                        AppendText(
                            ref s_logSummaryBuffer,
                            ResolveLocalized(
                                LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_UNKNOWN_MESSAGE,
                                "Sampler retrieved an unidentified salvage package."));
                    }

                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.SAMPLER_CATEGORY, SamplerCategory),
                        ResolveLocalized(LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_TITLE, "SALVAGE PACKAGE RECOVERED"),
                        in s_logSummaryBuffer,
                        "INFO");
                    if (recoveredItem != null)
                        PublishRecoveredItemMessage(recoveredItem.itemName);
                    else
                        PublishInfoMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_SALVAGE_RECOVERED, "SAMPLER - SALVAGE RECOVERED"));
                    ArmFeedbackCooldown();
                }
                else
                {
                    SamplerDiagnosis diagnosis = BuildDiagnosis(hit.collider);
                    PublishDiagnosis(diagnosis);
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.SAMPLER_CATEGORY, SamplerCategory),
                        GetDiagnosisLogTitle(diagnosis.headline),
                        diagnosis.summary,
                        diagnosis.severity);
                }
            }
            else if (TryConsumeFeedbackGate())
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_SALVAGE_LOCK, "SAMPLER - NO SALVAGE LOCK"));
            }

            InvalidateDiagnosisCache();
            _cooldown = sampleCooldown / math.max(0.25f, GetSpeed());
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
            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;

            unchecked
            {
                _diagnosisEvaluationStamp++;
            }
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            s_legacySummaryBuffer.Clear();
            WriteOperationalSummary(ref s_legacySummaryBuffer);
            return CreateLegacyString(in s_legacySummaryBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "SAMPLER // CYCLING ");
                buffer.AppendFloat(_cooldown, 1);
                AppendText(ref buffer, "S");
                return;
            }

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
            {
                AppendText(ref buffer, "SAMPLER // ");
                AppendText(ref buffer, diagnosis.headline);
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_OPERATIONAL_READY, "SAMPLER // READY"));
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            s_legacySummaryBuffer.Clear();
            WriteOperationalDirective(ref s_legacySummaryBuffer);
            return CreateLegacyString(in s_legacySummaryBuffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(
                    ref buffer,
                    ResolveLocalized(
                        LocalizationKeys.SAMPLER_OPERATIONAL_CYCLING_DIRECTIVE,
                        "Hold position while the sampling head resets."));
                return;
            }

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
            {
                AppendText(ref buffer, diagnosis.summary);
                return;
            }

            AppendText(
                ref buffer,
                ResolveLocalized(
                    LocalizationKeys.SAMPLER_OPERATIONAL_READY_DIRECTIVE,
                    "Primary extracts. Secondary checks or recovers salvage packages."));
        }

        private void ArchiveRecoveredItem(ItemData item)
        {
            ScanLogSystem scanLog = _scanLog;
            if (item == null || scanLog == null)
                return;

            string itemId = item.PersistentId;
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            s_archiveIdBuffer.Clear();
            AppendText(ref s_archiveIdBuffer, "recovery.");
            AppendLowerInvariant(ref s_archiveIdBuffer, itemId);

            s_archiveTitleBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_archiveTitleBuffer,
                ResolveLocalized(LocalizationKeys.SAMPLER_ARCHIVE_RECOVERY_TITLE, "{0} RECOVERY"),
                item.itemName,
                false);

            s_archiveSummaryBuffer.Clear();
            TryAppendSingleStringTemplate(
                ref s_archiveSummaryBuffer,
                ResolveLocalized(
                    LocalizationKeys.SAMPLER_ARCHIVE_RECOVERY_SUMMARY,
                    "Recovered field salvage package containing {0}. Archive updated from sampler retrieval."),
                item.itemName,
                false);

            scanLog.ArchiveEntry(
                CreateLegacyString(in s_archiveIdBuffer),
                CreateLegacyString(in s_archiveTitleBuffer),
                GetCategoryLabel(item.category),
                CreateLegacyString(in s_archiveSummaryBuffer));
        }

        private bool TryReadDiagnosis(out SamplerDiagnosis diagnosis)
        {
            diagnosis = default;

            if (!TryGetSamplingHit(out RaycastHit hit, out _))
            {
                return false;
            }

            diagnosis = BuildDiagnosis(hit.collider);
            return true;
        }

        private bool TryGetDiagnosisCached(out SamplerDiagnosis diagnosis)
        {
            uint currentStamp = _diagnosisEvaluationStamp;
            if (_cachedDiagnosisStamp == currentStamp)
            {
                diagnosis = _cachedDiagnosis;
                return _cachedDiagnosisValid;
            }

            bool valid = TryReadDiagnosis(out diagnosis);
            _cachedDiagnosisStamp = currentStamp;
            _cachedDiagnosisValid = valid;
            _cachedDiagnosis = diagnosis;
            return valid;
        }

        private void InvalidateDiagnosisCache()
        {
            _cachedDiagnosisStamp = uint.MaxValue;
            _cachedDiagnosisValid = false;
            _cachedDiagnosis = default;
        }

        private SamplerDiagnosis BuildDiagnosis(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return new SamplerDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_NO_TARGET, SamplerNoTargetHeadline),
                    summary = ResolveLocalized(
                        LocalizationKeys.SAMPLER_SUMMARY_NO_TARGET,
                        "No salvage contact was detected inside sampler range."),
                    severity = "WARN"
                };
            }

            if (ToolHitUtility.TryPeekCollectible(hitCollider, out ItemData recoverableItem, out int quantity))
            {
                string itemLabel = recoverableItem != null
                    ? recoverableItem.itemName
                    : ResolveLocalized(LocalizationKeys.SAMPLER_UNKNOWN_PACKAGE, "UNKNOWN PACKAGE");
                return new SamplerDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_RECOVERY_READY, SamplerRecoveryReadyHeadline),
                    summary = CreateRecoveryReadySummary(itemLabel, math.max(1, quantity)),
                    severity = "INFO"
                };
            }

            Hecton8.Scavenging.ResourceNode node =
                hitCollider.GetComponent<Hecton8.Scavenging.ResourceNode>() ??
                hitCollider.GetComponentInParent<Hecton8.Scavenging.ResourceNode>();
            if (node != null)
            {
                float integrityPercent = node.HealthNormalized * 100f;
                return new SamplerDiagnosis
                {
                    headline = node.IsDepleted
                        ? ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_NODE_DEPLETED, SamplerNodeDepletedHeadline)
                        : CreateResourceNodeHeadline(integrityPercent),
                    summary = node.IsDepleted
                        ? ResolveLocalized(
                            LocalizationKeys.SAMPLER_SUMMARY_NODE_DEPLETED,
                            "Resource node is already exhausted. No further salvage packet is expected.")
                        : integrityPercent <= 30f
                            ? ResolveLocalized(
                                LocalizationKeys.SAMPLER_SUMMARY_NODE_CRITICAL,
                                "Resource node is fragile and close to opening. Finish sampling now for a fast recovery window.")
                            : integrityPercent <= 65f
                                ? ResolveLocalized(
                                    LocalizationKeys.SAMPLER_SUMMARY_NODE_WEAKENED,
                                    "Resource node is weakened. Another controlled extraction pass is worthwhile.")
                                : ResolveLocalized(
                                    LocalizationKeys.SAMPLER_SUMMARY_NODE_ACTIVE,
                                    "Resource node is still active. Use primary action to continue sampling."),
                    severity = node.IsDepleted ? "WARN" : "INFO"
                };
            }

            if (hitCollider.TryGetComponent(out ICuttable _))
            {
                return new SamplerDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_PROCESS_TARGET, SamplerProcessTargetHeadline),
                    summary = ResolveLocalized(
                        LocalizationKeys.SAMPLER_SUMMARY_PROCESS_TARGET,
                        "Target can be processed, but no recoverable package is ready yet."),
                    severity = "WARN"
                };
            }

            return new SamplerDiagnosis
            {
                headline = ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_INVALID_TARGET, SamplerInvalidTargetHeadline),
                summary = ResolveLocalized(
                    LocalizationKeys.SAMPLER_SUMMARY_INVALID_TARGET,
                    "Target is inside sampler range but does not support salvage recovery."),
                severity = "WARN"
            };
        }

        private void PublishDiagnosis(SamplerDiagnosis diagnosis)
        {
            s_hudBuffer.Clear();
            if (!TryWriteDiagnosisHudMessage(ref s_hudBuffer, diagnosis.headline))
                return;

            if (diagnosis.severity == "WARN" || diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private void PublishRecoveredItemMessage(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                PublishInfoMessage(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_SALVAGE_RECOVERED, "SAMPLER - SALVAGE RECOVERED"));
                return;
            }

            s_hudBuffer.Clear();
            if (!TryAppendSingleStringTemplate(
                    ref s_hudBuffer,
                    ResolveLocalized(LocalizationKeys.SAMPLER_HUD_RECOVERED_ITEM, "SAMPLER - RECOVERED {0}"),
                    itemName,
                    true))
            {
                s_hudBuffer.Clear();
                AppendText(ref s_hudBuffer, ResolveLocalized(LocalizationKeys.SAMPLER_HUD_SALVAGE_RECOVERED, "SAMPLER - SALVAGE RECOVERED"));
            }

            if (s_hudBuffer.Length > 0)
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

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private bool TryGetSamplingHit(out RaycastHit hit, out Vector3 direction)
        {
            direction = default;
            if (!TryResolveSamplingRay(out Vector3 origin, out direction))
            {
                hit = default;
                return false;
            }

            return TryQueuePrimaryRaycast(origin, direction, samplingRange, samplingMask.value, QueryTriggerInteraction.Collide, out hit);
        }

        private bool TryResolveSamplingRay(out Vector3 origin, out Vector3 direction)
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

        private bool TryResolveInteractorRoot(out Transform interactorRoot)
        {
            interactorRoot = null;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext))
                return false;

            Transform playerTransform = playerContext.PlayerTransform;
            if (playerTransform == null)
                return false;

            interactorRoot = playerTransform.root;
            return interactorRoot != null;
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
            float safeInterval = math.isfinite(feedbackInterval) ? math.max(0.05f, feedbackInterval) : 0.35f;
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.isfinite(quality) ? math.saturate(quality) : 0.5f;
            float qualityCurve = math.smoothstep(0f, 1f, quality);
            return safeInterval * math.lerp(1.65f, 0.85f, qualityCurve);
        }

        /// <summary>
        /// Writes sampler diagnosis text into a fixed HUD buffer.
        /// Uses localized fixed-buffer append paths for dynamic fallback labels.
        /// </summary>
        private bool TryWriteDiagnosisHudMessage(ref FixedCharBuffer buffer, string headline)
        {
            switch (headline)
            {
                case SamplerNoTargetHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_NO_TARGET, "SAMPLER DIAG - NO TARGET"));
                case SamplerRecoveryReadyHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_RECOVERY_READY, "SAMPLER DIAG - RECOVERY READY"));
                case SamplerNodeDepletedHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_NODE_DEPLETED, "SAMPLER DIAG - NODE DEPLETED"));
                case SamplerProcessTargetHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_PROCESS_TARGET, "SAMPLER DIAG - PROCESS TARGET"));
                case SamplerInvalidTargetHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_INVALID_TARGET, "SAMPLER DIAG - INVALID TARGET"));
                default:
                    return AppendText(ref buffer, "SAMPLER DIAG - ") &&
                           AppendText(ref buffer, headline);
            }
        }

        private string GetDiagnosisLogTitle(string headline)
        {
            switch (headline)
            {
                case SamplerNoTargetHeadline:
                    return ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_NO_TARGET, "SAMPLER DIAG - NO TARGET");
                case SamplerRecoveryReadyHeadline:
                    return ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_RECOVERY_READY, "SAMPLER DIAG - RECOVERY READY");
                case SamplerNodeDepletedHeadline:
                    return ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_NODE_DEPLETED, "SAMPLER DIAG - NODE DEPLETED");
                case SamplerProcessTargetHeadline:
                    return ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_PROCESS_TARGET, "SAMPLER DIAG - PROCESS TARGET");
                case SamplerInvalidTargetHeadline:
                    return ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_INVALID_TARGET, "SAMPLER DIAG - INVALID TARGET");
                default:
                    s_logTitleBuffer.Clear();
                    if (!TryAppendSingleStringTemplate(
                            ref s_logTitleBuffer,
                            ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_GENERIC, "SAMPLER DIAG - {0}"),
                            headline,
                            false))
                    {
                        return "SAMPLER DIAG";
                    }

                    return CreateLegacyString(in s_logTitleBuffer);
            }
        }

        private string CreateRecoveryReadySummary(string itemLabel, int quantity)
        {
            s_diagnosisTextBuffer.Clear();
            if (!TryAppendStringIntTemplate(
                    ref s_diagnosisTextBuffer,
                    ResolveLocalized(
                        LocalizationKeys.SAMPLER_SUMMARY_RECOVERY_READY,
                        "{0} is ready for collection. Cached quantity: {1}."),
                    itemLabel,
                    quantity,
                    true))
            {
                s_diagnosisTextBuffer.Clear();
                AppendText(ref s_diagnosisTextBuffer, "Recoverable package is ready for collection.");
            }

            return CreateLegacyString(in s_diagnosisTextBuffer);
        }

        private string CreateResourceNodeHeadline(float integrityPercent)
        {
            s_diagnosisTextBuffer.Clear();
            if (!TryAppendSingleFloatTemplate(
                    ref s_diagnosisTextBuffer,
                    ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_RESOURCE_NODE, "RESOURCE NODE {0:0}%"),
                    integrityPercent,
                    0))
            {
                s_diagnosisTextBuffer.Clear();
                AppendText(ref s_diagnosisTextBuffer, "RESOURCE NODE");
            }

            return CreateLegacyString(in s_diagnosisTextBuffer);
        }

        private string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = _localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool TryAppendSingleStringTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string argument,
            bool upperInvariant)
        {
            return TryAppendSamplerTemplate(
                ref buffer,
                template,
                argument,
                0,
                0f,
                0,
                TemplateStringArg0,
                upperInvariant);
        }

        private static bool TryAppendStringIntTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            int intArg1,
            bool uppercaseStringArg0)
        {
            return TryAppendSamplerTemplate(
                ref buffer,
                template,
                stringArg0,
                intArg1,
                0f,
                0,
                TemplateStringArg0 | TemplateIntArg1,
                uppercaseStringArg0);
        }

        private static bool TryAppendSingleFloatTemplate(ref FixedCharBuffer buffer, string template, float value, int decimals)
        {
            return TryAppendSamplerTemplate(
                ref buffer,
                template,
                null,
                0,
                value,
                decimals,
                TemplateFloatArg0,
                false);
        }

        private static bool TryAppendSamplerTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            int intArg1,
            float floatArg0,
            int floatDecimals,
            byte argumentMask,
            bool uppercaseStringArg0)
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
                    '0' when (argumentMask & TemplateStringArg0) != 0 => AppendStringArgument(ref buffer, stringArg0, uppercaseStringArg0),
                    '0' when (argumentMask & TemplateFloatArg0) != 0 => buffer.AppendFloat(floatArg0, floatDecimals),
                    '1' when (argumentMask & TemplateIntArg1) != 0 => buffer.AppendInt(intArg1),
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

        private static bool AppendStringArgument(ref FixedCharBuffer buffer, string value, bool uppercase)
        {
            return uppercase
                ? AppendUpperInvariant(ref buffer, value)
                : AppendText(ref buffer, value);
        }

        private static bool AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            ReadOnlySpan<char> source = value.AsSpan();
            Span<char> scratch = stackalloc char[64];
            int cursor = 0;
            while (cursor < source.Length)
            {
                int count = math.min(scratch.Length, source.Length - cursor);
                for (int i = 0; i < count; i++)
                    scratch[i] = char.ToUpperInvariant(source[cursor + i]);

                if (!buffer.Append(scratch.Slice(0, count)))
                    return false;

                cursor += count;
            }

            return true;
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static bool AppendLowerInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            ReadOnlySpan<char> source = value.AsSpan();
            Span<char> scratch = stackalloc char[64];
            int cursor = 0;
            while (cursor < source.Length)
            {
                int count = math.min(scratch.Length, source.Length - cursor);
                for (int i = 0; i < count; i++)
                    scratch[i] = char.ToLowerInvariant(source[cursor + i]);

                if (!buffer.Append(scratch.Slice(0, count)))
                    return false;

                cursor += count;
            }

            return true;
        }

        private string GetCategoryLabel(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Material => ResolveLocalized("ITEM_CATEGORY_MATERIAL", "Material"),
                ItemCategory.Tool => ResolveLocalized("ITEM_CATEGORY_TOOL", "Tool"),
                ItemCategory.Equipment => ResolveLocalized("ITEM_CATEGORY_EQUIPMENT", "Equipment"),
                ItemCategory.Consumable => ResolveLocalized("ITEM_CATEGORY_CONSUMABLE", "Consumable"),
                ItemCategory.Component => ResolveLocalized("ITEM_CATEGORY_COMPONENT", "Component"),
                ItemCategory.Organic => ResolveLocalized("ITEM_CATEGORY_ORGANIC", "Organic"),
                _ => ResolveLocalized("ITEM_CATEGORY_MISC", "Miscellaneous")
            };
        }

    }
}
