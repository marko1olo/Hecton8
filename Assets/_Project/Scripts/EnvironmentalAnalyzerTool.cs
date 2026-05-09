using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Hecton8.UI;
using Unity.Mathematics;
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

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                return AppendText(ref buffer, Headline) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Summary) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Recommendation);
            }

            public bool TryWriteArchiveSummary(ref FixedCharBuffer buffer)
            {
                return AppendText(ref buffer, Category) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Severity) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Summary) &&
                       AppendText(ref buffer, " Recommendation: ") &&
                       AppendText(ref buffer, Recommendation);
            }
        }

        [Header("Analysis")]
        [SerializeField] private float range = 14f;
        [SerializeField] private float analysisCooldown = 0.4f;
        [SerializeField] private LayerMask analysisMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private float feedbackInterval = 0.45f;

        private Transform _cachedTransform;
        private HectonSurvivalSystem _survival;
        private HUDNotification _notification;
        private float _cooldown;
        private float _nextFeedbackAt;
        private int _cachedTargetAssessmentFrame = -1;
        private bool _cachedTargetAssessmentValid;
        private AnalyzerAssessment _cachedTargetAssessment;
        private FixedCharBuffer _hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — environmental analyzer HUD staging buffer — owner: EnvironmentalAnalyzerTool
        private FixedCharBuffer _logBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — environmental analyzer operation log staging buffer — owner: EnvironmentalAnalyzerTool

        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - environmental analyzer legacy string bridge - owner: EnvironmentalAnalyzerTool

        [SerializeField] private string _debugLastMessage;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        private bool TryResolveSurvival()
        {
            if (_survival != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            if (!playerTransform.TryGetComponent(out HectonSurvivalSystem survival))
                return false;

            _survival = survival;
            return true;
        }

        public override void OnEquip()
        {
            base.OnEquip();

            TryResolveSurvival();

            if (_notification == null)
                HUDNotification.TryGetActive(out _notification);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (TryGetAnalysisHit(out RaycastHit hit))
            {
                AnalyzerAssessment assessment = BuildTargetAssessment(hit);
                Publish(assessment);
                ArchiveTargetIntel(hit, assessment);

                if (Time.time >= _nextFeedbackAt)
                {
                    RecordOperationAssessment(assessment);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }
            else
            {
                PublishWarningMessage("ANALYZER: NO TARGET RETURN | Sweep a valid object to classify risk and opportunity.");
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

            InvalidateTargetAssessmentCache();
            _cooldown = analysisCooldown;
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            TryResolveSurvival();

            if (_survival == null)
            {
                PublishWarningMessage("ANALYZER: SURVIVAL LINK OFFLINE");
            }
            else
            {
                AnalyzerAssessment assessment = BuildSuitAssessment();
                Publish(assessment);
                ArchiveSuitDiagnostic(assessment);

                if (Time.time >= _nextFeedbackAt)
                {
                    RecordOperationAssessment(assessment);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
            }

            InvalidateTargetAssessmentCache();
            _cooldown = analysisCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - deltaTime);
        }

        public override string GetOperationalSummary()
        {
            _legacyOperationalBuffer.Clear();
            WriteOperationalSummary(ref _legacyOperationalBuffer);
            return CreateLegacyString(in _legacyOperationalBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "ANALYZER // CYCLING ");
                buffer.AppendFloat(_cooldown, 1);
                buffer.Append("S");
                return;
            }

            if (_cachedTargetAssessmentFrame == Time.frameCount && _cachedTargetAssessmentValid)
            {
                AppendText(ref buffer, "ANALYZER // ");
                AppendText(ref buffer, _cachedTargetAssessment.Headline);
                return;
            }

            AppendText(ref buffer, "ANALYZER // READY");
        }

        public override string GetOperationalDirective()
        {
            _legacyOperationalBuffer.Clear();
            WriteOperationalDirective(ref _legacyOperationalBuffer);
            return CreateLegacyString(in _legacyOperationalBuffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "Hold position while the analyzer finishes the last sweep.");
                return;
            }

            if (_cachedTargetAssessmentFrame == Time.frameCount && _cachedTargetAssessmentValid)
            {
                AppendText(ref buffer, _cachedTargetAssessment.Recommendation);
                return;
            }

            AppendText(ref buffer, "Primary reads the target. Secondary diagnoses suit risk and expedition state.");
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

            if (TryBuildDescriptorAssessment(collider, hit.distance, out AnalyzerAssessment descriptorAssessment))
                return descriptorAssessment;

            if (collider.TryGetComponent(out HectonItem item))
            {
                return BuildItemAssessment(item.Data, item.Quantity);
            }

            if (collider.TryGetComponent(out PickupItem pickup))
            {
                return BuildItemAssessment(pickup.ItemData, pickup.Quantity);
            }

            if (collider.TryGetComponent(out ScannableTarget scannable))
            {
                return new AnalyzerAssessment(
                    ResolveScannableHeadline(scannable),
                    scannable.EntrySummary,
                    "INFO",
                    scannable.EntryCategory,
                    BuildScannableRecommendation(scannable));
            }

            if (collider.TryGetComponent(out ResourceNode node))
            {
                return new AnalyzerAssessment(
                    node.IsDepleted ? "RESOURCE NODE DEPLETED" : "RESOURCE NODE STABLE",
                    node.IsDepleted
                        ? "The node has been exhausted and is no longer worth tool time."
                        : "Mineral density is stable and extraction worthy.",
                    node.IsDepleted ? "WARN" : "INFO",
                    "Resource",
                    node.IsDepleted
                        ? "Leave it and move to a fresh resource lane."
                        : "Mark for salvage, cutter work, or later recovery.");
            }

            if (collider.TryGetComponent(out BaseModule module))
            {
                return BuildModuleAssessment(module);
            }

            if (collider.TryGetComponent(out FaunaBrain ai))
                return BuildBioformAssessment(ai);

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
                    body.mass > 60f ? "MASS OBJECT HEAVY" : "MASS OBJECT HANDLING RANGE",
                    summary,
                    severity,
                    "Logistics",
                    recommendation);
            }

            return new AnalyzerAssessment(
                "UNCLASSIFIED SIGNATURE",
                "Signature does not match a known expedition profile.",
                "WARN",
                "Analyzer",
                "Hold position and inspect manually.");
        }

        private bool TryBuildDescriptorAssessment(Collider collider, float distance, out AnalyzerAssessment assessment)
        {
            assessment = default;
            if (!FieldTargetDescriptor.TryResolveDirect(collider, out FieldTargetDescriptor descriptor))
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

        private void Publish(AnalyzerAssessment assessment)
        {
            _hudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref _hudBuffer))
                return;

            if (_notification == null)
                HUDNotification.TryGetActive(out _notification);

            if (_notification != null)
                PublishBySeverity(_notification, in _hudBuffer, assessment.Severity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugLastMessage = assessment.Headline;
#endif
        }

        private void PublishWarningMessage(string message)
        {
            _hudBuffer.Clear();
            if (!AppendText(ref _hudBuffer, message))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugLastMessage = message;
#endif

            if (_notification == null)
                HUDNotification.TryGetActive(out _notification);

            if (_notification != null)
                _notification.ShowWarning(in _hudBuffer);
        }

        private bool TryGetAnalysisHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, range, analysisMask.value, QueryTriggerInteraction.Collide, out hit);
        }

        private void RecordOperationAssessment(AnalyzerAssessment assessment)
        {
            _logBuffer.Clear();
            if (!AppendText(ref _logBuffer, assessment.Summary) ||
                !AppendText(ref _logBuffer, " | ") ||
                !AppendText(ref _logBuffer, assessment.Recommendation))
            {
                return;
            }

            FieldOperationLogSystem.RecordOperation(
                "ANALYZER",
                assessment.Headline,
                in _logBuffer,
                assessment.Severity);
        }

        private void ArchiveTargetIntel(RaycastHit hit, AnalyzerAssessment assessment)
        {
            if (Hecton8.Core.GlobalRegistry.ScanLog == null || hit.collider == null)
                return;

            Collider collider = hit.collider;
            if (!TryBuildArchiveSummary(assessment, out string archiveSummary))
                return;

            if (FieldTargetDescriptor.TryResolveDirect(collider, out FieldTargetDescriptor descriptor))
            {
                Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                    ResolveDescriptorArchiveId(descriptor.Role),
                    ResolveDescriptorArchiveTitle(descriptor.Role),
                    assessment.Category,
                    archiveSummary);
                return;
            }

            if (collider.TryGetComponent(out HectonItem item))
            {
                if (item.Data == null)
                    return;

                string itemId = item.Data.PersistentId;
                if (string.IsNullOrWhiteSpace(itemId))
                    return;

                if (!TryCreateArchiveText("analyzer.item.", itemId, out string entryId))
                    return;

                Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                    entryId,
                    "ITEM PROFILE",
                    assessment.Category,
                    archiveSummary);
                return;
            }

            if (collider.TryGetComponent(out BaseModule _))
            {
                ModuleMarker marker = null;
                collider.TryGetComponent(out marker);
                BuildableData buildableData = marker != null ? marker.Data : null;
                string moduleId = buildableData != null && !string.IsNullOrWhiteSpace(buildableData.PersistentId)
                    ? buildableData.PersistentId
                    : "base_module";

                if (!TryCreateArchiveText("analyzer.module.", moduleId, out string entryId))
                    return;

                Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                    entryId,
                    "BASE MODULE ANALYSIS",
                    assessment.Category,
                    archiveSummary);
                return;
            }

            if (collider.TryGetComponent(out FaunaBrain _))
            {
                Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                    "analyzer.bioform.local",
                    "BIOFORM SIGNATURE",
                    assessment.Category,
                    archiveSummary);
                return;
            }

            if (collider.TryGetComponent(out ResourceNode _))
            {
                Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                    "analyzer.resource_node",
                    "RESOURCE NODE ANALYSIS",
                    assessment.Category,
                    archiveSummary);
                return;
            }

            Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                "analyzer.misc.unclassified",
                "UNCLASSIFIED ANALYSIS",
                assessment.Category,
                archiveSummary);
        }

        private void ArchiveSuitDiagnostic(AnalyzerAssessment assessment)
        {
            if (Hecton8.Core.GlobalRegistry.ScanLog == null || _survival == null)
                return;

            if (!TryBuildArchiveSummary(assessment, out string archiveSummary))
                return;

            Hecton8.Core.GlobalRegistry.ScanLog.ArchiveEntry(
                "analyzer.suit_status",
                assessment.Headline,
                assessment.Category,
                archiveSummary);
        }

        private AnalyzerAssessment BuildSuitAssessment()
        {
            float safeDepth = _survival.Stats != null ? _survival.Stats.SafeDepth : 0f;
            float depth = math.max(0f, _survival.Depth);

            if (_survival.IntegrityNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    "SUIT CRITICAL",
                    "Hull stability is near collapse under current operating load.",
                    "CRITICAL",
                    "Suit",
                    "Repair immediately and abort deep-water exposure.");
            }

            if (_survival.OxygenNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    "SUIT OXYGEN ALERT",
                    "Breathing reserve is critically low for continued field work.",
                    "CRITICAL",
                    "Suit",
                    "Ascend or refill before another task cycle.");
            }

            if (_survival.EnergyNormalized <= 0.2f)
            {
                return new AnalyzerAssessment(
                    "SUIT POWER ALERT",
                    "Power reserve is too low for confident tool deployment.",
                    "WARN",
                    "Suit",
                    "Recharge before scanner, cutter, or propulsion use.");
            }

            if (_survival.IntegrityNormalized <= 0.45f)
            {
                return new AnalyzerAssessment(
                    "SUIT HULL WARNING",
                    "Hull reserve is falling into a risky expedition band.",
                    "WARN",
                    "Suit",
                    "Repair before another deep push or combat encounter.");
            }

            if (_survival.OxygenNormalized <= 0.45f)
            {
                return new AnalyzerAssessment(
                    "SUIT OXYGEN WATCH",
                    "Breathing reserve is no longer comfortable for exploration tempo.",
                    "WARN",
                    "Suit",
                    "Plan the return path now or refill before committing deeper.");
            }

            if (_survival.EnergyNormalized <= 0.4f)
            {
                return new AnalyzerAssessment(
                    "SUIT POWER WATCH",
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
                    "PRESSURE EXCEEDANCE",
                    summary,
                    severity,
                    "Suit",
                    recommendation);
            }

            if (safeDepth > 0f && depth >= safeDepth * 0.85f)
            {
                return new AnalyzerAssessment(
                    "PRESSURE WATCH",
                    "You are approaching the suit's safe-depth boundary.",
                    "WARN",
                    "Suit",
                    "Continue only if the current objective is worth the pressure margin.");
            }

            return new AnalyzerAssessment(
                "SUIT GREEN",
                "Suit diagnostics remain stable for continued expedition work.",
                "INFO",
                "Suit",
                "Current loadout is clear for another field action.");
        }

        private static AnalyzerAssessment BuildItemAssessment(ItemData itemData, int quantity)
        {
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
                categoryLabel,
                summary,
                severity,
                "Analyzer",
                recommendation);
        }

        private static AnalyzerAssessment BuildModuleAssessment(BaseModule module)
        {
            if (module.IsFlooded)
            {
                return new AnalyzerAssessment(
                    "MODULE FLOODED",
                    "The module is waterlogged and operationally compromised.",
                    "WARN",
                    "Structure",
                    "Repair, restore power, and drain before reuse.");
            }

            if (module.CanDeconstruct())
            {
                return new AnalyzerAssessment(
                    "MODULE SALVAGEABLE",
                    "Cut lines and salvage points are available on this structure.",
                    "INFO",
                    "Structure",
                    "Use cutter if reclaiming materials is the goal.");
            }

            return new AnalyzerAssessment(
                "MODULE SEALED",
                module.HasPower
                    ? "Structural profile is stable and powered."
                    : "Structural profile is stable but awaiting power.",
                "INFO",
                "Structure",
                module.HasPower
                    ? "Keep in service or inspect for expansion."
                    : "Reconnect power before assigning work.");
        }

        private static AnalyzerAssessment BuildBioformAssessment(FaunaBrain ai)
        {
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
                ResolveBioformHeadline(ai.CurrentState, sleeping, lethalWindow),
                summary,
                severity,
                "Bioform",
                recommendation);
        }

        private static void PublishBySeverity(HUDNotification notification, in FixedCharBuffer messageBuffer, string severity)
        {
            if (severity == "CRITICAL")
            {
                notification.ShowCritical(in messageBuffer);
                return;
            }

            if (severity == "WARN" || severity == "WARNING")
            {
                notification.ShowWarning(in messageBuffer);
                return;
            }

            notification.ShowInfo(in messageBuffer);
        }

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static string ResolveScannableHeadline(ScannableTarget scannable)
        {
            return scannable != null && !string.IsNullOrWhiteSpace(scannable.EntryTitle)
                ? scannable.EntryTitle
                : "SCANNABLE TARGET";
        }

        private static string ResolveBioformHeadline(FaunaBrain.AIState state, bool sleeping, bool lethalWindow)
        {
            if (sleeping)
                return "BIOFORM DORMANT";

            if (lethalWindow)
                return "BIOFORM FRACTURED";

            return state switch
            {
                FaunaBrain.AIState.Aggressive => "BIOFORM AGGRESSIVE",
                FaunaBrain.AIState.Threaten => "BIOFORM WARNING",
                FaunaBrain.AIState.Stalk => "BIOFORM STALKING",
                FaunaBrain.AIState.Loom => "BIOFORM LOOMING",
                FaunaBrain.AIState.Feint => "BIOFORM FEINT",
                FaunaBrain.AIState.Escape => "BIOFORM EVASIVE",
                _ => "BIOFORM OBSERVATION"
            };
        }

        private bool TryBuildArchiveSummary(AnalyzerAssessment assessment, out string summary)
        {
            _logBuffer.Clear();
            if (!assessment.TryWriteArchiveSummary(ref _logBuffer))
            {
                summary = string.Empty;
                return false;
            }

            summary = CreateLegacyString(in _logBuffer);
            return !string.IsNullOrEmpty(summary);
        }

        private bool TryCreateArchiveText(string prefix, string value, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            _logBuffer.Clear();
            if (!AppendText(ref _logBuffer, prefix) || !AppendText(ref _logBuffer, value))
                return false;

            text = CreateLegacyString(in _logBuffer);
            return !string.IsNullOrEmpty(text);
        }

        private static string ResolveDescriptorArchiveId(FieldTargetRole role)
        {
            return role switch
            {
                FieldTargetRole.RouteAnchor => "analyzer.descriptor.route_anchor",
                FieldTargetRole.RouteRelay => "analyzer.descriptor.route_relay",
                FieldTargetRole.RouteFrontier => "analyzer.descriptor.route_frontier",
                FieldTargetRole.CargoLight => "analyzer.descriptor.cargo_light",
                FieldTargetRole.CargoWork => "analyzer.descriptor.cargo_work",
                FieldTargetRole.CargoHeavy => "analyzer.descriptor.cargo_heavy",
                FieldTargetRole.CargoOverweight => "analyzer.descriptor.cargo_overweight",
                FieldTargetRole.ResourceNodeActive => "analyzer.descriptor.resource_active",
                FieldTargetRole.ResourceNodeDepleted => "analyzer.descriptor.resource_depleted",
                FieldTargetRole.ServiceDamaged => "analyzer.descriptor.service_damaged",
                FieldTargetRole.ServiceFlooded => "analyzer.descriptor.service_flooded",
                FieldTargetRole.ServiceControl => "analyzer.descriptor.service_control",
                FieldTargetRole.HazardProbe => "analyzer.descriptor.hazard_probe",
                FieldTargetRole.ResourceCache => "analyzer.descriptor.resource_cache",
                FieldTargetRole.StructureRelay => "analyzer.descriptor.structure_relay",
                FieldTargetRole.ExpeditionCheckpoint => "analyzer.descriptor.expedition_checkpoint",
                FieldTargetRole.BioformDormant => "analyzer.descriptor.bioform_dormant",
                FieldTargetRole.BioformAggressive => "analyzer.descriptor.bioform_aggressive",
                FieldTargetRole.BioformFractured => "analyzer.descriptor.bioform_fractured",
                FieldTargetRole.BioformDown => "analyzer.descriptor.bioform_down",
                FieldTargetRole.ConstructionSocket => "analyzer.descriptor.construction_socket",
                FieldTargetRole.ConstructionBlocked => "analyzer.descriptor.construction_blocked",
                FieldTargetRole.ConstructionClear => "analyzer.descriptor.construction_clear",
                FieldTargetRole.PowerGeneration => "analyzer.descriptor.power_generation",
                FieldTargetRole.PowerRelay => "analyzer.descriptor.power_relay",
                FieldTargetRole.PowerLoad => "analyzer.descriptor.power_load",
                FieldTargetRole.DistressBeacon => "analyzer.descriptor.distress_beacon",
                _ => "analyzer.descriptor.generic"
            };
        }

        private static string ResolveDescriptorArchiveTitle(FieldTargetRole role)
        {
            return role switch
            {
                FieldTargetRole.RouteAnchor => "ROUTE ANCHOR ANALYSIS",
                FieldTargetRole.RouteRelay => "ROUTE RELAY ANALYSIS",
                FieldTargetRole.RouteFrontier => "ROUTE FRONTIER ANALYSIS",
                FieldTargetRole.CargoLight => "LIGHT CARGO ANALYSIS",
                FieldTargetRole.CargoWork => "WORK CARGO ANALYSIS",
                FieldTargetRole.CargoHeavy => "HEAVY CARGO ANALYSIS",
                FieldTargetRole.CargoOverweight => "OVERWEIGHT CARGO ANALYSIS",
                FieldTargetRole.ResourceNodeActive => "ACTIVE RESOURCE ANALYSIS",
                FieldTargetRole.ResourceNodeDepleted => "DEPLETED RESOURCE ANALYSIS",
                FieldTargetRole.ServiceDamaged => "DAMAGED SERVICE ANALYSIS",
                FieldTargetRole.ServiceFlooded => "FLOODED SERVICE ANALYSIS",
                FieldTargetRole.ServiceControl => "SERVICE CONTROL ANALYSIS",
                FieldTargetRole.HazardProbe => "HAZARD PROBE ANALYSIS",
                FieldTargetRole.ResourceCache => "RESOURCE CACHE ANALYSIS",
                FieldTargetRole.StructureRelay => "STRUCTURE RELAY ANALYSIS",
                FieldTargetRole.ExpeditionCheckpoint => "EXPEDITION CHECKPOINT ANALYSIS",
                FieldTargetRole.BioformDormant => "DORMANT BIOFORM ANALYSIS",
                FieldTargetRole.BioformAggressive => "AGGRESSIVE BIOFORM ANALYSIS",
                FieldTargetRole.BioformFractured => "FRACTURED BIOFORM ANALYSIS",
                FieldTargetRole.BioformDown => "DOWNED BIOFORM ANALYSIS",
                FieldTargetRole.ConstructionSocket => "CONSTRUCTION SOCKET ANALYSIS",
                FieldTargetRole.ConstructionBlocked => "BLOCKED CONSTRUCTION ANALYSIS",
                FieldTargetRole.ConstructionClear => "CLEAR CONSTRUCTION ANALYSIS",
                FieldTargetRole.PowerGeneration => "POWER GENERATION ANALYSIS",
                FieldTargetRole.PowerRelay => "POWER RELAY ANALYSIS",
                FieldTargetRole.PowerLoad => "POWER LOAD ANALYSIS",
                FieldTargetRole.DistressBeacon => "DISTRESS BEACON ANALYSIS",
                _ => "FIELD TARGET ANALYSIS"
            };
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

    }
}

