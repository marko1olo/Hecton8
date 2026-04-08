using UnityEngine;
using Hecton8.Items;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SalvageSamplerTool : PlayerTool
    {
        private const int RecoveredItemMessageCacheSize = 16;
        private const string RecoveredItemMessagePrefix = "SAMPLER - RECOVERED ";
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

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                samplingRange,
                samplingMask,
                QueryTriggerInteraction.Collide))
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
                    ToolHitUtility.ShowWarning("SAMPLER - NO VIABLE TARGET");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else if (applied && Time.time >= _nextFeedbackAt)
                {
                    ToolHitUtility.ShowInfo("SAMPLER - EXTRACTION IN PROGRESS");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning("SAMPLER - NO TARGET LOCK");
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

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                samplingRange,
                samplingMask,
                QueryTriggerInteraction.Collide))
            {
                bool collected = ToolHitUtility.TryCollectItem(hit.collider, _cachedTransform.root, out ItemData recoveredItem);
                if (collected)
                {
                    ArchiveRecoveredItem(recoveredItem);
                    FieldOperationLogSystem.RecordOperation(
                        "SALVAGE",
                        "SALVAGE PACKAGE RECOVERED",
                        recoveredItem != null
                            ? $"Sampler retrieved {recoveredItem.itemName} from a recoverable field target."
                            : "Sampler retrieved an unidentified salvage package.",
                        "INFO");
                    ToolHitUtility.ShowInfo(
                        recoveredItem != null
                            ? GetRecoveredItemMessage(recoveredItem.itemName)
                            : "SAMPLER - SALVAGE RECOVERED");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                else
                {
                    SamplerDiagnosis diagnosis = BuildDiagnosis(hit.collider);
                    PublishDiagnosis(diagnosis);
                    FieldOperationLogSystem.RecordOperation(
                        "SALVAGE",
                        GetDiagnosisLogTitle(diagnosis.headline),
                        diagnosis.summary,
                        diagnosis.severity);
                }
            }
            else if (Time.time >= _nextFeedbackAt)
            {
                ToolHitUtility.ShowWarning("SAMPLER - NO SALVAGE LOCK");
                _nextFeedbackAt = Time.time + feedbackInterval;
            }

            InvalidateDiagnosisCache();
            _cooldown = sampleCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            Hecton8.Input.InputManager input = Hecton8.Input.InputManager.Instance;
            if (input != null && !input.IsSecondaryActionHeld)
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return $"SAMPLER // CYCLING {_cooldown:0.0}S";

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
                return $"SAMPLER // {diagnosis.headline}";

            return "SAMPLER // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return "Hold position while the sampling head resets.";

            if (TryGetDiagnosisCached(out SamplerDiagnosis diagnosis))
                return diagnosis.summary;

            return "Primary extracts. Secondary checks or recovers salvage packages.";
        }

        private static void ArchiveRecoveredItem(ItemData item)
        {
            if (item == null || ScanLogSystem.Instance == null)
                return;

            string entryId = $"recovery.{item.name.ToLowerInvariant()}";
            string title = $"{item.itemName} RECOVERY";
            string category = item.category.ToString();
            string summary = $"Recovered field salvage package containing {item.itemName}. Archive updated from sampler retrieval.";
            ScanLogSystem.Instance.ArchiveEntry(entryId, title, category, summary);
        }

        private bool TryReadDiagnosis(out SamplerDiagnosis diagnosis)
        {
            diagnosis = default;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                samplingRange,
                samplingMask,
                QueryTriggerInteraction.Collide))
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
                    headline = "NO TARGET",
                    summary = "No salvage contact was detected inside sampler range.",
                    severity = "WARN"
                };
            }

            if (ToolHitUtility.TryPeekCollectible(hitCollider, out ItemData recoverableItem, out int quantity))
            {
                string itemLabel = recoverableItem != null
                    ? CachedToUpperInvariant(recoverableItem.itemName)
                    : "UNKNOWN PACKAGE";
                return new SamplerDiagnosis
                {
                    headline = "RECOVERY READY",
                    summary = $"{itemLabel} is ready for collection. Cached quantity: {Mathf.Max(1, quantity)}.",
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
                    headline = node.IsDepleted ? "NODE DEPLETED" : $"RESOURCE NODE {integrityPercent:0}%",
                    summary = node.IsDepleted
                        ? "Resource node is already exhausted. No further salvage packet is expected."
                        : integrityPercent <= 30f
                            ? "Resource node is fragile and close to opening. Finish sampling now for a fast recovery window."
                            : integrityPercent <= 65f
                                ? "Resource node is weakened. Another controlled extraction pass is worthwhile."
                                : "Resource node is still active. Use primary action to continue sampling.",
                    severity = node.IsDepleted ? "WARN" : "INFO"
                };
            }

            if (hitCollider.TryGetComponent(out ICuttable _))
            {
                return new SamplerDiagnosis
                {
                    headline = "PROCESS TARGET",
                    summary = "Target can be processed, but no recoverable package is ready yet.",
                    severity = "WARN"
                };
            }

            return new SamplerDiagnosis
            {
                headline = "INVALID TARGET",
                summary = "Target is inside sampler range but does not support salvage recovery.",
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

        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string GetRecoveredItemMessage(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "SAMPLER - SALVAGE RECOVERED";

            int cacheIndex = (itemName.GetHashCode() & int.MaxValue) % RecoveredItemMessageCacheSize;
            string cachedItemName = _recoveredItemNameCache[cacheIndex];
            if (!string.IsNullOrEmpty(cachedItemName) && string.Equals(cachedItemName, itemName, System.StringComparison.Ordinal))
                return _recoveredItemMessageCache[cacheIndex];

            string message = RecoveredItemMessagePrefix + CachedToUpperInvariant(itemName);
            _recoveredItemNameCache[cacheIndex] = itemName;
            _recoveredItemMessageCache[cacheIndex] = message;
            return message;
        }

        private static string GetDiagnosisHudMessage(string headline)
        {
            switch (headline)
            {
                case SamplerNoTargetHeadline:
                    return "SAMPLER DIAG - NO TARGET";
                case SamplerRecoveryReadyHeadline:
                    return "SAMPLER DIAG - RECOVERY READY";
                case SamplerNodeDepletedHeadline:
                    return "SAMPLER DIAG - NODE DEPLETED";
                case SamplerProcessTargetHeadline:
                    return "SAMPLER DIAG - PROCESS TARGET";
                case SamplerInvalidTargetHeadline:
                    return "SAMPLER DIAG - INVALID TARGET";
                default:
                    return "SAMPLER DIAG - " + headline;
            }
        }

        private static string GetDiagnosisLogTitle(string headline)
        {
            switch (headline)
            {
                case SamplerNoTargetHeadline:
                    return "SAMPLER DIAG - NO TARGET";
                case SamplerRecoveryReadyHeadline:
                    return "SAMPLER DIAG - RECOVERY READY";
                case SamplerNodeDepletedHeadline:
                    return "SAMPLER DIAG - NODE DEPLETED";
                case SamplerProcessTargetHeadline:
                    return "SAMPLER DIAG - PROCESS TARGET";
                case SamplerInvalidTargetHeadline:
                    return "SAMPLER DIAG - INVALID TARGET";
                default:
                    return "SAMPLER DIAG - " + headline;
            }
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
