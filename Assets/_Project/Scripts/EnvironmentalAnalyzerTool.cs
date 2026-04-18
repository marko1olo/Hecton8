using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Items;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnvironmentalAnalyzerTool : PlayerTool
    {
        private readonly struct AnalyzerAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Severity;
            public readonly string Category;
            public readonly string Recommendation;

            public AnalyzerAssessment(string headline, string summary, string severity, string category, string recommendation)
            {
                Headline = headline;
                Summary = summary;
                Severity = severity;
                Category = category;
                Recommendation = recommendation;
            }

            public string BuildHudMessage()
            {
                return $"{Headline} | {Summary} | {Recommendation}";
            }

            public string BuildArchiveSummary()
            {
                return $"{Category} | {Severity} | {Summary} Recommendation: {Recommendation}";
            }
        }

        [Header("Analysis")]
        [SerializeField] private float range = 14f;
        [SerializeField] private float analysisCooldown = 0.4f;
        [SerializeField] private LayerMask analysisMask = ~0;
        [SerializeField] private float feedbackInterval = 0.45f;

        private Transform _cachedTransform;
        private HectonSurvivalSystem _survival;
        private HUDNotification _notification;
        private float _cooldown;
        private float _nextFeedbackAt;
        private readonly RaycastHit[] _analysisHits = new RaycastHit[1]; // COLD ALLOC: analyzer samples only the nearest target per sweep.
        private int _cachedTargetAssessmentFrame = -1;
        private bool _cachedTargetAssessmentValid;
        private AnalyzerAssessment _cachedTargetAssessment;

        [SerializeField] private string _debugLastMessage;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void OnEquip()
        {
            base.OnEquip();

            if (_survival == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    _survival = playerTransform.GetComponent<HectonSurvivalSystem>();
                }
            }

            if (_notification == null)
                HUDNotification.TryGetActive(out _notification);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            string message;

            if (TryGetAnalysisHit(out RaycastHit hit))
            {
                AnalyzerAssessment assessment = BuildTargetAssessment(hit);
                message = assessment.BuildHudMessage();
                ArchiveTargetIntel(hit, assessment);

                if (Time.time >= _nextFeedbackAt)
                {
                    FieldOperationLogSystem.RecordOperation(
                        "ANALYZER",
                        assessment.Headline,
                        $"{assessment.Summary} | {assessment.Recommendation}",
                        assessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else
            {
                message = "ANALYZER: NO TARGET RETURN | Sweep a valid object to classify risk and opportunity.";
                if (Time.time >= _nextFeedbackAt)
                {
                    FieldOperationLogSystem.RecordOperation(
                        "ANALYZER",
                        "NO TARGET RETURN",
                        "Sweep a valid object to classify risk and opportunity.",
                        "WARN");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }

            Publish(message);
            InvalidateTargetAssessmentCache();
            _cooldown = analysisCooldown;
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (_survival == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    _survival = playerTransform.GetComponent<HectonSurvivalSystem>();
                }
            }

            if (_survival == null)
            {
                Publish("ANALYZER: SURVIVAL LINK OFFLINE");
            }
            else
            {
                AnalyzerAssessment assessment = BuildSuitAssessment();
                Publish(assessment.BuildHudMessage());
                ArchiveSuitDiagnostic(assessment);

                if (Time.time >= _nextFeedbackAt)
                {
                    FieldOperationLogSystem.RecordOperation(
                        "ANALYZER",
                        assessment.Headline,
                        $"{assessment.Summary} | {assessment.Recommendation}",
                        assessment.Severity);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }

            InvalidateTargetAssessmentCache();
            _cooldown = analysisCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }

        public override string GetOperationalSummary()
        {
            if (_cooldown > 0f)
                return $"ANALYZER // CYCLING {_cooldown:0.0}S";

            if (TryGetTargetAssessmentCached(out AnalyzerAssessment assessment))
                return $"ANALYZER // {assessment.Headline}";

            return "ANALYZER // READY";
        }

        public override string GetOperationalDirective()
        {
            if (_cooldown > 0f)
                return "Hold position while the analyzer finishes the last sweep.";

            if (TryGetTargetAssessmentCached(out AnalyzerAssessment assessment))
                return assessment.Recommendation;

            return "Primary reads the target. Secondary diagnoses suit risk and expedition state.";
        }

        private AnalyzerAssessment BuildTargetAssessment(RaycastHit hit)
        {
            Collider collider = hit.collider;
            if (collider == null)
            {
                return new AnalyzerAssessment(
                    "ANALYZER RETURN INVALID",
                    "Signal collapsed before classification completed.",
                    "WARN",
                    "Analyzer",
                    "Reacquire a stable target.");
            }

            if (collider.TryGetComponent(out HectonItem item))
            {
                return BuildItemAssessment(item.Data, item.Quantity, hit.distance, collider.gameObject.name);
            }

            HectonItem parentItem = collider.GetComponentInParent<HectonItem>();
            if (parentItem != null)
                return BuildItemAssessment(parentItem.Data, parentItem.Quantity, hit.distance, parentItem.gameObject.name);

            if (collider.TryGetComponent(out PickupItem pickup))
            {
                return BuildItemAssessment(
                    pickup.ItemData,
                    pickup.Quantity,
                    hit.distance,
                    pickup.gameObject.name);
            }

            PickupItem parentPickup = collider.GetComponentInParent<PickupItem>();
            if (parentPickup != null)
            {
                return BuildItemAssessment(
                    parentPickup.ItemData,
                    parentPickup.Quantity,
                    hit.distance,
                    parentPickup.gameObject.name);
            }

            if (collider.TryGetComponent(out ScannableTarget scannable))
            {
                return new AnalyzerAssessment(
                    $"{scannable.EntryTitle} | RANGE {hit.distance:0.0} M",
                    scannable.EntrySummary,
                    "INFO",
                    scannable.EntryCategory,
                    BuildScannableRecommendation(scannable));
            }

            ScannableTarget parentScannable = collider.GetComponentInParent<ScannableTarget>();
            if (parentScannable != null)
            {
                return new AnalyzerAssessment(
                    $"{parentScannable.EntryTitle} | RANGE {hit.distance:0.0} M",
                    parentScannable.EntrySummary,
                    "INFO",
                    parentScannable.EntryCategory,
                    BuildScannableRecommendation(parentScannable));
            }

            ResourceNode node = collider.GetComponent<ResourceNode>() ?? collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                return new AnalyzerAssessment(
                    node.IsDepleted
                        ? $"RESOURCE NODE DEPLETED | RANGE {hit.distance:0.0} M"
                        : $"RESOURCE NODE STABLE | RANGE {hit.distance:0.0} M",
                    node.IsDepleted
                        ? "The node has been exhausted and is no longer worth tool time."
                        : "Mineral density is stable and extraction worthy.",
                    node.IsDepleted ? "WARN" : "INFO",
                    "Resource",
                    node.IsDepleted
                        ? "Leave it and move to a fresh resource lane."
                        : "Mark for salvage, cutter work, or later recovery.");
            }

            if (TryBuildDescriptorAssessment(collider, hit.distance, out AnalyzerAssessment descriptorAssessment))
                return descriptorAssessment;

            if (collider.TryGetComponent(out BaseModule module))
            {
                return BuildModuleAssessment(module, hit.distance);
            }

            if (collider.TryGetComponent(out FaunaBrain ai))
                return BuildBioformAssessment(ai, hit.distance);

            FaunaBrain aiParent = collider.GetComponentInParent<FaunaBrain>();
            if (aiParent != null)
                return BuildBioformAssessment(aiParent, hit.distance);

            if (ToolHitUtility.TryGetRigidbody(collider, out Rigidbody body))
            {
                string severity = body.mass > 60f ? "WARN" : "INFO";
                string summary = body.mass > 60f
                    ? "Mass profile is high. Manual handling is discouraged."
                    : "Mass profile is within utility-tool handling range.";
                string recommendation = body.mass > 60f
                    ? "Use structural planning or avoid contact."
                    : "Propulsion or harpoon handling is viable.";
                return new AnalyzerAssessment(
                    $"MASS OBJECT | {body.mass:0.0} KG | RANGE {hit.distance:0.0} M",
                    summary,
                    severity,
                    "Logistics",
                    recommendation);
            }

            return new AnalyzerAssessment(
                $"UNCLASSIFIED | {CachedToUpperInvariant(collider.gameObject.name)} | RANGE {hit.distance:0.0} M",
                "Signature does not match a known expedition profile.",
                "WARN",
                "Analyzer",
                "Hold position and inspect manually.");
        }

        private bool TryBuildDescriptorAssessment(Collider collider, float distance, out AnalyzerAssessment assessment)
        {
            assessment = default;
            if (!FieldTargetDescriptor.TryResolve(collider, out FieldTargetDescriptor descriptor))
                return false;

            float? mass = null;
            if (ToolHitUtility.TryGetRigidbody(collider, out Rigidbody cargoBody) && cargoBody != null)
                mass = cargoBody.mass;

            if (FieldTargetSemantics.TryBuildAnalyzerAssessment(descriptor, distance, mass, out FieldTargetSemantics.SemanticAssessment semantic))
            {
                assessment = new AnalyzerAssessment(
                    semantic.Headline,
                    semantic.Summary,
                    semantic.Severity,
                    semantic.Category,
                    semantic.Recommendation);
                return true;
            }

            return false;
        }

        private static string BuildScannableRecommendation(ScannableTarget scannable)
        {
            if (scannable == null)
                return "Hold position and inspect manually.";

            switch (ScannableCategoryUtility.Classify(scannable.EntryCategory))
            {
                case ScannableCategoryUtility.CategoryKind.Hazard:
                    return "Switch to focused observation and approach with caution.";
                case ScannableCategoryUtility.CategoryKind.Resource:
                    return "Mark the pocket and prepare salvage or resource recovery.";
                case ScannableCategoryUtility.CategoryKind.Structure:
                    return "Hold this route for scanner structure mode or return navigation.";
                case ScannableCategoryUtility.CategoryKind.Flora:
                    return "Treat this as a flora intel contact. Inspect shelter value, harvest utility, and silhouette readability before moving on.";
                case ScannableCategoryUtility.CategoryKind.Expedition:
                    return "Use this as a route checkpoint before pushing deeper.";
            }

            return "Archive the contact and inspect before committing another tool.";
        }

        private bool TryReadTargetAssessment(out AnalyzerAssessment assessment)
        {
            assessment = default;

            if (!TryGetAnalysisHit(out RaycastHit hit))
            {
                return false;
            }

            assessment = BuildTargetAssessment(hit);
            return true;
        }

        private bool TryGetTargetAssessmentCached(out AnalyzerAssessment assessment)
        {
            int currentFrame = Time.frameCount;
            if (_cachedTargetAssessmentFrame == currentFrame)
            {
                assessment = _cachedTargetAssessment;
                return _cachedTargetAssessmentValid;
            }

            bool valid = TryReadTargetAssessment(out assessment);
            _cachedTargetAssessmentFrame = currentFrame;
            _cachedTargetAssessmentValid = valid;
            _cachedTargetAssessment = assessment;
            return valid;
        }

        private void InvalidateTargetAssessmentCache()
        {
            _cachedTargetAssessmentFrame = -1;
            _cachedTargetAssessmentValid = false;
            _cachedTargetAssessment = default;
        }

        private void Publish(string message)
        {
            _debugLastMessage = message;

            if (_notification == null)
                HUDNotification.TryGetActive(out _notification);

            if (_notification != null)
                PublishBySeverity(_notification, _debugLastMessage);
            else
                Debug.Log($"[EnvironmentalAnalyzer] {message}");
        }

        private bool TryGetAnalysisHit(out RaycastHit hit)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cachedTransform.position,
                _cachedTransform.forward,
                _analysisHits,
                range,
                analysisMask,
                QueryTriggerInteraction.Collide);

            if (hitCount > 0)
            {
                hit = _analysisHits[0];
                return true;
            }

            hit = default;
            return false;
        }

        private void ArchiveTargetIntel(RaycastHit hit, AnalyzerAssessment assessment)
        {
            if (ScanLogSystem.Instance == null || hit.collider == null)
                return;

            Collider collider = hit.collider;

            HectonItem item = null;
            if (!collider.TryGetComponent(out item))
                item = collider.GetComponentInParent<HectonItem>();

            if (item != null)
            {
                if (item.Data == null)
                    return;

                string itemId = string.IsNullOrWhiteSpace(item.Data.itemName)
                    ? item.Data.name
                    : item.Data.itemName;
                ScanLogSystem.Instance.ArchiveEntry(
                    $"analyzer.item.{itemId}".ToLowerInvariant(),
                    $"{item.Data.itemName.ToUpperInvariant()} PROFILE",
                    assessment.Category,
                    assessment.BuildArchiveSummary());
                return;
            }

            BaseModule module = null;
            if (!collider.TryGetComponent(out module))
                module = collider.GetComponentInParent<BaseModule>();

            if (module != null)
            {
                string entryId = $"analyzer.module.{module.name}".ToLowerInvariant();
                ScanLogSystem.Instance.ArchiveEntry(
                    entryId,
                    $"{module.name.ToUpperInvariant()} ANALYSIS",
                    assessment.Category,
                    assessment.BuildArchiveSummary());
                return;
            }

            FaunaBrain ai = null;
            if (!collider.TryGetComponent(out ai))
                ai = collider.GetComponentInParent<FaunaBrain>();

            if (ai != null)
            {
                ScanLogSystem.Instance.ArchiveEntry(
                    $"analyzer.bioform.{ai.GetType().Name}".ToLowerInvariant(),
                    $"{ai.GetType().Name.ToUpperInvariant()} SIGNATURE",
                    assessment.Category,
                    assessment.BuildArchiveSummary());
                return;
            }

            if (collider.TryGetComponent(out ResourceNode _) || collider.GetComponentInParent<ResourceNode>() != null)
            {
                ScanLogSystem.Instance.ArchiveEntry(
                    "analyzer.resource_node",
                    "RESOURCE NODE ANALYSIS",
                    assessment.Category,
                    assessment.BuildArchiveSummary());
                return;
            }

            ScanLogSystem.Instance.ArchiveEntry(
                $"analyzer.misc.{collider.gameObject.name}".ToLowerInvariant(),
                $"{collider.gameObject.name.ToUpperInvariant()} ANALYSIS",
                assessment.Category,
                assessment.BuildArchiveSummary());
        }

        private void ArchiveSuitDiagnostic(AnalyzerAssessment assessment)
        {
            if (ScanLogSystem.Instance == null || _survival == null)
                return;

            ScanLogSystem.Instance.ArchiveEntry(
                "analyzer.suit_status",
                assessment.Headline,
                assessment.Category,
                assessment.BuildArchiveSummary());
        }

        private AnalyzerAssessment BuildSuitAssessment()
        {
            float safeDepth = _survival.Stats != null ? _survival.Stats.SafeDepth : 0f;
            float depth = Mathf.Max(0f, _survival.Depth);
            float oxygenPercent = _survival.OxygenNormalized * 100f;
            float energyPercent = _survival.EnergyNormalized * 100f;
            float integrityPercent = _survival.IntegrityNormalized * 100f;

            if (_survival.IntegrityNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    $"SUIT CRITICAL | HULL {integrityPercent:0}% | DEPTH {depth:0} M",
                    "Hull stability is near collapse under current operating load.",
                    "CRITICAL",
                    "Suit",
                    "Repair immediately and abort deep-water exposure.");
            }

            if (_survival.OxygenNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    $"SUIT OXYGEN ALERT | O2 {oxygenPercent:0}% | DEPTH {depth:0} M",
                    "Breathing reserve is critically low for continued field work.",
                    "CRITICAL",
                    "Suit",
                    "Ascend or refill before another task cycle.");
            }

            if (_survival.EnergyNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    $"SUIT POWER ALERT | EN {energyPercent:0}% | P {_survival.Pressure:0.0} ATM",
                    "Power reserve is too low for confident tool deployment.",
                    "WARN",
                    "Suit",
                    "Recharge before scanner, cutter, or propulsion use.");
            }

            if (_survival.IntegrityNormalized <= 0.45f)
            {
                return new AnalyzerAssessment(
                    $"SUIT HULL WARNING | HULL {integrityPercent:0}% | DEPTH {depth:0} M",
                    "Hull reserve is falling into a risky expedition band.",
                    "WARN",
                    "Suit",
                    "Repair before another deep push or combat encounter.");
            }

            if (_survival.OxygenNormalized <= 0.45f)
            {
                return new AnalyzerAssessment(
                    $"SUIT OXYGEN WATCH | O2 {oxygenPercent:0}% | DEPTH {depth:0} M",
                    "Breathing reserve is no longer comfortable for exploration tempo.",
                    "WARN",
                    "Suit",
                    "Plan the return path now or refill before committing deeper.");
            }

            if (_survival.EnergyNormalized <= 0.4f)
            {
                return new AnalyzerAssessment(
                    $"SUIT POWER WATCH | EN {energyPercent:0}% | P {_survival.Pressure:0.0} ATM",
                    "Power reserve is sliding out of the safe tool-usage band.",
                    "WARN",
                    "Suit",
                    "Favor essential tools only and queue a recharge window.");
            }

            if (safeDepth > 0f && depth > safeDepth)
            {
                float excess = depth - safeDepth;
                string severity = excess > 25f ? "CRITICAL" : "WARN";
                string summary = excess > 25f
                    ? "Pressure load is far beyond the certified safe envelope."
                    : "Pressure load is above the safe operating envelope.";
                string recommendation = excess > 25f
                    ? "Ascend now or expect rapid integrity loss."
                    : "Reduce depth or limit field time until conditions stabilize.";
                return new AnalyzerAssessment(
                    $"PRESSURE EXCEEDANCE | SAFE {safeDepth:0} M | LIVE {depth:0} M",
                    summary,
                    severity,
                    "Suit",
                    recommendation);
            }

            if (safeDepth > 0f && depth >= safeDepth * 0.85f)
            {
                return new AnalyzerAssessment(
                    $"PRESSURE WATCH | SAFE {safeDepth:0} M | LIVE {depth:0} M",
                    "You are approaching the suit's safe-depth boundary.",
                    "WARN",
                    "Suit",
                    "Continue only if the current objective is worth the pressure margin.");
            }

            return new AnalyzerAssessment(
                $"SUIT GREEN | O2 {oxygenPercent:0}% | EN {energyPercent:0}% | HULL {integrityPercent:0}%",
                "Suit diagnostics remain stable for continued expedition work.",
                "INFO",
                "Suit",
                "Current loadout is clear for another field action.");
        }

        private static AnalyzerAssessment BuildItemAssessment(ItemData itemData, int quantity, float distance, string fallbackName)
        {
            string itemName = itemData != null && !string.IsNullOrWhiteSpace(itemData.itemName)
                ? itemData.itemName
                : fallbackName;
            ItemCategory category = itemData != null ? itemData.category : ItemCategory.Miscellaneous;
            string severity = "INFO";
            string summary;
            string recommendation;
            string categoryLabel;

            switch (category)
            {
                case ItemCategory.Tool:
                    categoryLabel = "TOOL PACKAGE";
                    summary = "This asset expands field capability and quick-slot value.";
                    recommendation = "Recover if the loadout or inventory still has tool demand.";
                    break;
                case ItemCategory.Equipment:
                    categoryLabel = "EQUIPMENT PACKAGE";
                    summary = "This asset supports a gear or suit workflow rather than raw barter mass.";
                    recommendation = "Secure it if the expedition lacks role coverage.";
                    break;
                case ItemCategory.Consumable:
                    categoryLabel = "CONSUMABLE CACHE";
                    summary = "Portable reserve can stabilize oxygen, energy, or suit condition.";
                    recommendation = "Keep it accessible if the route ahead is uncertain.";
                    break;
                case ItemCategory.Component:
                    categoryLabel = "COMPONENT PACKAGE";
                    summary = "Refined component stock is useful for construction, exchange, or upgrades.";
                    recommendation = "Recover if fabrication or barter plans are active.";
                    break;
                case ItemCategory.Material:
                    categoryLabel = "MATERIAL STOCK";
                    summary = "Raw material stock is suitable for construction and salvage chains.";
                    recommendation = quantity > 1
                        ? "High-value pickup. Recover while cargo space remains."
                        : "Recover if a build, barter, or crafting path needs it.";
                    break;
                default:
                    categoryLabel = "FIELD ITEM";
                    summary = "Portable asset is stable and available for expedition use.";
                    recommendation = "Recover if inventory space allows.";
                    break;
            }

            return new AnalyzerAssessment(
                $"{categoryLabel} | {itemName.ToUpperInvariant()} | QTY {quantity} | RANGE {distance:0.0} M",
                summary,
                severity,
                "Analyzer",
                recommendation);
        }

        private static AnalyzerAssessment BuildModuleAssessment(BaseModule module, float distance)
        {
            float integrityPercent = module.MaxIntegrity > 0.001f
                ? (module.CurrentIntegrity / module.MaxIntegrity) * 100f
                : 0f;

            if (module.IsFlooded)
            {
                return new AnalyzerAssessment(
                    $"MODULE FLOODED | HULL {integrityPercent:0}% | RANGE {distance:0.0} M",
                    "The module is waterlogged and operationally compromised.",
                    "WARN",
                    "Structure",
                    "Repair, restore power, and drain before reuse.");
            }

            if (module.CanDeconstruct())
            {
                return new AnalyzerAssessment(
                    $"MODULE SALVAGEABLE | HULL {integrityPercent:0}% | RANGE {distance:0.0} M",
                    "Cut lines and salvage points are available on this structure.",
                    "INFO",
                    "Structure",
                    "Use cutter if reclaiming materials is the goal.");
            }

            return new AnalyzerAssessment(
                $"MODULE SEALED | HULL {integrityPercent:0}% | RANGE {distance:0.0} M",
                module.HasPower
                    ? "Structural profile is stable and powered."
                    : "Structural profile is stable but awaiting power.",
                "INFO",
                "Structure",
                module.HasPower
                    ? "Keep in service or inspect for expansion."
                    : "Reconnect power before assigning work.");
        }

        private static AnalyzerAssessment BuildBioformAssessment(FaunaBrain ai, float distance)
        {
            float healthPercent = ai.MaxHealth > 0.001f
                ? ai.HealthNormalized * 100f
                : 0f;
            bool lethalWindow = ai.HealthNormalized <= 0.3f;
            bool hostile = ai.CurrentState == FaunaBrain.AIState.Aggressive;
            bool warning = ai.CurrentState == FaunaBrain.AIState.Threaten;
            bool stalking = ai.CurrentState == FaunaBrain.AIState.Stalk;
            bool looming = ai.CurrentState == FaunaBrain.AIState.Loom;
            bool feinting = ai.CurrentState == FaunaBrain.AIState.Feint;
            bool evasive = ai.CurrentState == FaunaBrain.AIState.Escape;
            bool sleeping = ai.IsSleeping;
            bool packHunt = ai.UsesPackHuntBehavior && (hostile || stalking);
            bool feintCapable = ai.UsesFeintRushBehavior && (stalking || looming || feinting);
            bool ambushLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.AmbushBurst;
            bool sentinelLeviathan = ai.LeviathanEncounter == Hecton8.AI.LeviathanEncounterType.SentinelPressure;
            string severity = hostile ? "WARN" : (warning || stalking || looming || feinting ? "WARN" : "INFO");
            string summary;
            string recommendation;

            if (sleeping)
            {
                summary = "Bioform is dormant and can be observed, scanned, or bypassed before wake-up.";
                recommendation = "Approach carefully or act before it becomes active.";
            }
            else if (hostile)
            {
                summary = packHunt
                    ? (lethalWindow
                        ? "Pack-hunting bioform is weakened but the attack pattern is still active."
                        : "Pack-hunting bioform is in an active kill phase.")
                    : (lethalWindow
                        ? "Hostile bioform is weakened but still dangerous at close range."
                        : "Hostile bioform remains combat-capable.");
                recommendation = lethalWindow
                    ? "Knife finish or stun follow-up is viable."
                    : (packHunt
                        ? "Break line, watch the flanks, and prepare a fast stun response."
                        : "Keep distance and prepare stun or harpoon control.");
            }
            else if (warning || stalking || looming || feinting)
            {
                summary = feinting
                    ? "Large bioform is in a false-charge run and may peel away or snap into a real hit if you drift too close."
                    : looming
                    ? (ambushLeviathan
                        ? "Large bioform is setting up a burst ambush and may snap into direct contact without a long warning."
                        : (sentinelLeviathan
                            ? "Large bioform is controlling a guarded route and pressing you away from its corridor."
                            : "Large bioform is holding a pressure circle and may convert into a direct attack."))
                    : warning
                    ? "Bioform is warning you and holding pressure around its zone."
                    : (packHunt
                        ? "Predatory bioform is tracking you as part of a group attack pattern."
                        : (feintCapable
                            ? "Predatory bioform is tracking you and can throw a false charge before the real commit."
                            : "Predatory bioform is tracking you and building attack pressure."));
                recommendation = feinting
                    ? "Do not counter-rush. Break the angle, let the pass go wide, and prepare for the second move."
                    : looming
                    ? (ambushLeviathan
                        ? "Do not drift into close range. Break the angle and prepare for a sudden rush."
                        : (sentinelLeviathan
                            ? "Back off from the guarded route or prepare for a forced passage."
                            : "Break line of sight, avoid closing distance, and prepare a stun or hard disengage."))
                    : warning
                    ? "Back off, avoid the protected area, or prepare to defend yourself."
                    : (packHunt
                        ? "Expect a flank or follow-up rush and keep stun or escape ready."
                        : (feintCapable
                            ? "Expect a fake entry before the real commit and do not spend your tool too early."
                            : "Expect a fast commit soon and keep stun or escape ready."));
            }
            else
            {
                summary = evasive
                    ? "Bioform is in an evasive state and likely to break contact."
                    : (lethalWindow
                        ? "Bioform is stressed and likely to flee."
                        : "Bioform shows no immediate attack posture.");
                recommendation = evasive
                    ? "Scanner pass or cautious pursuit is viable."
                    : (lethalWindow
                        ? "Observe carefully or disengage."
                        : "Scanner pass is safe if range is maintained.");
            }

            return new AnalyzerAssessment(
                $"BIOFORM {CachedToUpperInvariant(ai.CurrentState.ToString())} | HP {healthPercent:0}% | RANGE {distance:0.0} M",
                summary,
                severity,
                "Bioform",
                recommendation);
        }

        private static void PublishBySeverity(HUDNotification notification, string message)
        {
            if (message.Contains("CRITICAL", System.StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ALERT", System.StringComparison.OrdinalIgnoreCase) ||
                message.Contains("EXCEEDANCE", System.StringComparison.OrdinalIgnoreCase))
            {
                notification.ShowCritical(message);
                return;
            }

            if (message.Contains("WARN", System.StringComparison.OrdinalIgnoreCase) ||
                message.Contains("NO TARGET", System.StringComparison.OrdinalIgnoreCase) ||
                message.Contains("OFFLINE", System.StringComparison.OrdinalIgnoreCase))
            {
                notification.ShowWarning(message);
                return;
            }

            notification.ShowInfo(message);
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

