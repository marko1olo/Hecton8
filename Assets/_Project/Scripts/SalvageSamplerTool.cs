using UnityEngine;
using Hecton8.Core;
using Hecton8.Items;
using Hecton.Localization;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SalvageSamplerTool : PlayerTool
    {
        private const int RecoveredItemMessageCacheSize = 16;
        private const string SamplerCategory = "SALVAGE";
        private const string SamplerNoTargetHeadline = "NO TARGET";
        private const string SamplerRecoveryReadyHeadline = "RECOVERY READY";
        private const string SamplerNodeDepletedHeadline = "NODE DEPLETED";
        private const string SamplerProcessTargetHeadline = "PROCESS TARGET";
        private const string SamplerInvalidTargetHeadline = "INVALID TARGET";

        private struct SamplerDiagnosis
        {
            public string headline;
            public string summary;
            public string severity;
        }

        private static readonly string[] _recoveredItemNameCache = new string[RecoveredItemMessageCacheSize];
        private static readonly string[] _recoveredItemMessageCache = new string[RecoveredItemMessageCacheSize];

        [Header("Sampling")]
        [SerializeField] private float samplingRange = 3.2f;
        [SerializeField] private float sampleDamage = 18f;
        [SerializeField] private float sampleImpulse = 1.5f;
        [SerializeField] private float sampleCooldown = 0.3f;
        [SerializeField] private LayerMask samplingMask = ~0;
        [SerializeField] private float feedbackInterval = 0.45f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _nextFeedbackAt;
        private bool _secondaryLatched;
        private int _cachedDiagnosisFrame = -1;
        private bool _cachedDiagnosisValid;
        private SamplerDiagnosis _cachedDiagnosis;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetSamplingHit(out RaycastHit hit))
            {
                float effectiveDamage = sampleDamage * GetEfficiency();
                bool applied = ToolHitUtility.ApplyDamage(
                    hit.collider,
                    effectiveDamage,
                    hit.point,
                    _cachedTransform.forward,
                    sampleImpulse);

                if (!applied && Time.time >= _nextFeedbackAt)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_VIABLE_TARGET, "SAMPLER - NO VIABLE TARGET"));
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else if (applied && Time.time >= _nextFeedbackAt)
                {
                    ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_EXTRACTION_IN_PROGRESS, "SAMPLER - EXTRACTION IN PROGRESS"));
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_TARGET_LOCK, "SAMPLER - NO TARGET LOCK"));
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            InvalidateDiagnosisCache();
            _cooldown = sampleCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetSamplingHit(out RaycastHit hit))
            {
                bool collected = ToolHitUtility.TryCollectItem(hit.collider, _cachedTransform.root, out ItemData recoveredItem);
                if (collected)
                {
                    ArchiveRecoveredItem(recoveredItem);
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.SAMPLER_CATEGORY, SamplerCategory),
                        ResolveLocalized(LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_TITLE, "SALVAGE PACKAGE RECOVERED"),
                        recoveredItem != null
                            ? string.Format(
                                ResolveLocalized(
                                    LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_MESSAGE,
                                    "Sampler retrieved {0} from a recoverable field target."),
                                recoveredItem.itemName)
                            : ResolveLocalized(
                                LocalizationKeys.SAMPLER_LOG_PACKAGE_RECOVERED_UNKNOWN_MESSAGE,
                                "Sampler retrieved an unidentified salvage package."),
                        "INFO");
                    ToolHitUtility.ShowInfo(
                        recoveredItem != null
                            ? GetRecoveredItemMessage(recoveredItem.itemName)
                            : ResolveLocalized(LocalizationKeys.SAMPLER_HUD_SALVAGE_RECOVERED, "SAMPLER - SALVAGE RECOVERED"));
                    _nextFeedbackAt = Time.time + feedbackInterval;
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
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.SAMPLER_HUD_NO_SALVAGE_LOCK, "SAMPLER - NO SALVAGE LOCK"));
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            InvalidateDiagnosisCache();
            _cooldown = sampleCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SAMPLER_OPERATIONAL_CYCLING, "SAMPLER // CYCLING {0:0.0}S"),
                    _cooldown);

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.SAMPLER_OPERATIONAL_DIAGNOSIS, "SAMPLER // {0}"),
                    diagnosis.headline);

            return ResolveLocalized(LocalizationKeys.SAMPLER_OPERATIONAL_READY, "SAMPLER // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return ResolveLocalized(
                    LocalizationKeys.SAMPLER_OPERATIONAL_CYCLING_DIRECTIVE,
                    "Hold position while the sampling head resets.");

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
                return diagnosis.summary;

            return ResolveLocalized(
                LocalizationKeys.SAMPLER_OPERATIONAL_READY_DIRECTIVE,
                "Primary extracts. Secondary checks or recovers salvage packages.");
        }

        private static void ArchiveRecoveredItem(ItemData item)
        {
            if (item == null || ScanLogSystem.Instance == null)
                return;

            string itemId = item.PersistentId;
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            string entryId = $"recovery.{itemId}".ToLowerInvariant();
            string title = string.Format(
                ResolveLocalized(LocalizationKeys.SAMPLER_ARCHIVE_RECOVERY_TITLE, "{0} RECOVERY"),
                item.itemName);
            string category = item.category.ToString();
            string summary = string.Format(
                ResolveLocalized(
                    LocalizationKeys.SAMPLER_ARCHIVE_RECOVERY_SUMMARY,
                    "Recovered field salvage package containing {0}. Archive updated from sampler retrieval."),
                item.itemName);
            ScanLogSystem.Instance.ArchiveEntry(entryId, title, category, summary);
        }

        private bool TryReadDiagnosis(out SamplerDiagnosis diagnosis)
        {
            diagnosis = default;

            if (!TryGetSamplingHit(out RaycastHit hit))
            {
                return false;
            }

            diagnosis = BuildDiagnosis(hit.collider);
            return true;
        }

        private bool TryGetDiagnosisCached(out SamplerDiagnosis diagnosis)
        {
            int currentFrame = Time.frameCount;
            if (_cachedDiagnosisFrame == currentFrame)
            {
                diagnosis = _cachedDiagnosis;
                return _cachedDiagnosisValid;
            }

            bool valid = TryReadDiagnosis(out diagnosis);
            _cachedDiagnosisFrame = currentFrame;
            _cachedDiagnosisValid = valid;
            _cachedDiagnosis = diagnosis;
            return valid;
        }

        private void InvalidateDiagnosisCache()
        {
            _cachedDiagnosisFrame = -1;
            _cachedDiagnosisValid = false;
            _cachedDiagnosis = default;
        }

        private static SamplerDiagnosis BuildDiagnosis(Collider hitCollider)
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
                    ? CachedToUpperInvariant(recoverableItem.itemName)
                    : ResolveLocalized(LocalizationKeys.SAMPLER_UNKNOWN_PACKAGE, "UNKNOWN PACKAGE");
                return new SamplerDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_RECOVERY_READY, SamplerRecoveryReadyHeadline),
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.SAMPLER_SUMMARY_RECOVERY_READY,
                            "{0} is ready for collection. Cached quantity: {1}."),
                        itemLabel,
                        Mathf.Max(1, quantity)),
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
                        : string.Format(
                            ResolveLocalized(LocalizationKeys.SAMPLER_HEADLINE_RESOURCE_NODE, "RESOURCE NODE {0:0}%"),
                            integrityPercent),
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

        private static void PublishDiagnosis(SamplerDiagnosis diagnosis)
        {
            if (diagnosis.severity == "WARN" || diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(GetDiagnosisHudMessage(diagnosis.headline));
            else
                ToolHitUtility.ShowInfo(GetDiagnosisHudMessage(diagnosis.headline));
        }

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private bool TryGetSamplingHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, samplingRange, samplingMask.value, QueryTriggerInteraction.Collide, out hit);
        }

        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string GetRecoveredItemMessage(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return ResolveLocalized(LocalizationKeys.SAMPLER_HUD_SALVAGE_RECOVERED, "SAMPLER - SALVAGE RECOVERED");

            int cacheIndex = (itemName.GetHashCode() & int.MaxValue) % RecoveredItemMessageCacheSize;
            string cachedItemName = _recoveredItemNameCache[cacheIndex];
            if (!string.IsNullOrEmpty(cachedItemName) && string.Equals(cachedItemName, itemName, System.StringComparison.Ordinal))
                return _recoveredItemMessageCache[cacheIndex];

            string message = string.Format(
                ResolveLocalized(LocalizationKeys.SAMPLER_HUD_RECOVERED_ITEM, "SAMPLER - RECOVERED {0}"),
                CachedToUpperInvariant(itemName));
            _recoveredItemNameCache[cacheIndex] = itemName;
            _recoveredItemMessageCache[cacheIndex] = message;
            return message;
        }

        private static string GetDiagnosisHudMessage(string headline)
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
                    return string.Format(
                        ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_GENERIC, "SAMPLER DIAG - {0}"),
                        headline);
            }
        }

        private static string GetDiagnosisLogTitle(string headline)
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
                    return string.Format(
                        ResolveLocalized(LocalizationKeys.SAMPLER_DIAG_GENERIC, "SAMPLER DIAG - {0}"),
                        headline);
            }
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetOrFallback(LocalizationManager.Instance.CurrentLanguage, key, fallback)
                : fallback;
        }

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
