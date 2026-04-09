// ============================================================================
// HECTON-8 - FlashlightTool.cs
// Hand-tool adapter over the existing PlayerFlashlight system.
// Does not create a second flashlight pipeline.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Bootstrap;
    using Hecton8.Input;
    using Hecton8.Interaction;
    using Hecton8.Scavenging;
    using Hecton8.UI;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Flashlight Tool")]
    public sealed class FlashlightTool : PlayerTool
    {
        private readonly struct LampAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public LampAssessment(string headline, string summary, string recommendation, string severity)
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

        [Header("Adapter")]
        [SerializeField] private bool autoTurnOffOnUnequip = true;
        [SerializeField] private bool secondaryCyclesBeamMode = true;
        [SerializeField] private float contextProbeRange = 18f;
        [SerializeField] private LayerMask contextMask = ~0;

        private PlayerFlashlight _flashlight;
        private HUDNotification _hudNotification;
        private bool _stateBeforeEquip;
        private bool _primaryLatched;
        private bool _secondaryLatched;
        private bool _missingFlashlightWarned;
        private int _cachedSnapshotFrame = -1;
        private string _cachedOperationalSummary;
        private string _cachedOperationalRecommendation;
        private int _cachedContextDirectiveFrame = -1;
        private bool _cachedHasContextDirective;
        private string _cachedContextDirective;
        private readonly RaycastHit[] _contextRaycastHits = new RaycastHit[1]; // COLD ALLOC: single-hit flashlight context probe buffer.

        public override void OnEquip()
        {
            base.OnEquip();

            ResolveRuntimeReferences();
            _stateBeforeEquip = _flashlight != null && _flashlight.IsOn;
            _primaryLatched = false;
            _secondaryLatched = false;
            InvalidateSnapshotCache();
        }

        public override void OnUnequip()
        {
            if (autoTurnOffOnUnequip &&
                _flashlight != null &&
                !_stateBeforeEquip &&
                _flashlight.IsOn)
            {
                _flashlight.TurnOff();
            }

            _primaryLatched = false;
            _secondaryLatched = false;
            InvalidateSnapshotCache();
            base.OnUnequip();
        }

        public override void UsePrimary(float deltaTime)
        {
            if (_primaryLatched)
                return;

            _primaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            if (_flashlight.IsOverheated)
            {
                InvalidateSnapshotCache();
                LampAssessment cooling = BuildAssessment();
                PublishAssessment(cooling);
                FieldOperationLogSystem.RecordOperation(
                    "FLASHLIGHT",
                    "DIVE LAMP COOLING",
                    $"{cooling.Summary} | {cooling.Recommendation}",
                    "WARN");
                return;
            }

            _flashlight.Toggle();
            FieldOperationLogSystem.RecordOperation(
                "FLASHLIGHT",
                _flashlight.IsOn ? "DIVE LAMP ACTIVATED" : "DIVE LAMP STOWED",
                _flashlight.IsOn
                    ? "Hand lamp is now contributing to the active field visibility stack."
                    : "Hand lamp returned to standby to preserve expedition power discipline.",
                "INFO");
            InvalidateSnapshotCache();
            PublishAssessment(BuildAssessment());
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            if (secondaryCyclesBeamMode)
            {
                _flashlight.CycleBeamMode();
                InvalidateSnapshotCache();
                string mode = _flashlight.BeamModeLabel;
                LampAssessment assessment = BuildAssessment();
                FieldOperationLogSystem.RecordOperation(
                    "FLASHLIGHT",
                    $"DIVE LAMP {mode} PROFILE",
                    $"{assessment.Summary} | {assessment.Recommendation}",
                    "INFO");
                PublishAssessment(assessment);
                return;
            }

            LampAssessment status = BuildAssessment();
            FieldOperationLogSystem.RecordOperation(
                "FLASHLIGHT",
                "DIVE LAMP STATUS QUERY",
                $"{status.Summary} | {status.Recommendation}",
                status.Severity);
            PublishAssessment(status);
        }

        public override void ToolTick(float deltaTime)
        {
            InputManager input = InputManager.Instance;
            if (input == null)
                return;

            if (!input.IsPrimaryActionHeld)
                _primaryLatched = false;

            if (!input.IsSecondaryActionHeld)
                _secondaryLatched = false;
        }

        private void ResolveRuntimeReferences()
        {
            if (_flashlight == null)
                _flashlight = GetComponentInParent<PlayerFlashlight>();

            if (_flashlight == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    _flashlight = playerTransform.GetComponentInChildren<PlayerFlashlight>(true);
                }
            }

            if (_hudNotification == null)
                HUDNotification.TryGetActive(out _hudNotification);
        }

        private bool TryResolveFlashlight()
        {
            ResolveRuntimeReferences();

            if (_flashlight != null)
                return true;

            if (!_missingFlashlightWarned)
            {
                Debug.LogWarning("[FlashlightTool] No PlayerFlashlight found in scene.");
                _missingFlashlightWarned = true;
            }

            return false;
        }

        private void ShowInfo(string message)
        {
            if (_hudNotification != null)
                _hudNotification.ShowInfo(message);
            else
                Debug.Log(message);
        }

        public override string GetOperationalSummary()
        {
            if (!TryResolveFlashlight())
                return "DIVE LAMP // LINK OFFLINE";

            if (TryGetOperationalSnapshot(out string summary, out _))
                return summary;

            return _flashlight.BuildOperationalSummary();
        }

        public override string GetOperationalDirective()
        {
            if (!TryResolveFlashlight())
                return "Restore the lamp link before field deployment.";

            if (TryGetOperationalSnapshot(out _, out string recommendation))
            {
                if (TryGetForwardContextDirectiveCached(out string contextDirective))
                    return contextDirective;

                return recommendation;
            }

            if (TryGetForwardContextDirective(out string directive))
                return directive;

            return _flashlight.BuildOperationalRecommendation();
        }

        private string BuildStatusSnapshot()
        {
            if (_flashlight == null)
                return "Lamp diagnostics unavailable.";

            if (TryGetOperationalSnapshot(out string summary, out _))
                return summary;

            return _flashlight.BuildOperationalSummary();
        }

        private LampAssessment BuildAssessment()
        {
            if (_flashlight == null)
            {
                return new LampAssessment(
                    "DIVE LAMP - LINK OFFLINE",
                    "Flashlight diagnostics are unavailable.",
                    "Re-establish the lamp link before field deployment.",
                    "WARN");
            }

            if (!TryGetOperationalSnapshot(out string summary, out string recommendation))
            {
                summary = _flashlight.BuildOperationalSummary();
                recommendation = _flashlight.BuildOperationalRecommendation();
            }

            if (_flashlight.IsOverheated)
            {
                return new LampAssessment(
                    $"DIVE LAMP - COOLING {Mathf.CeilToInt(_flashlight.CooldownRemaining)}S",
                    summary,
                    recommendation,
                    "WARN");
            }

            if (_flashlight.EnergyPercent <= 10f)
            {
                return new LampAssessment(
                    $"DIVE LAMP - LOW ENERGY [{_flashlight.BeamModeLabel}]",
                    summary,
                    recommendation,
                    "WARN");
            }

            if (_flashlight.HeatLevel >= 0.7f)
            {
                return new LampAssessment(
                    $"DIVE LAMP - HEAT RISING [{_flashlight.BeamModeLabel}]",
                    summary,
                    recommendation,
                    "WARN");
            }

            string contextualRecommendation = TryGetForwardContextDirectiveCached(out string contextDirective)
                ? contextDirective
                : recommendation;

            return new LampAssessment(
                _flashlight.IsOn
                    ? $"DIVE LAMP - ON [{_flashlight.BeamModeLabel}]"
                    : $"DIVE LAMP - STANDBY [{_flashlight.BeamModeLabel}]",
                summary,
                contextualRecommendation,
                "INFO");
        }

        private bool TryGetOperationalSnapshot(out string summary, out string recommendation)
        {
            summary = null;
            recommendation = null;

            if (_flashlight == null)
                return false;

            int currentFrame = Time.frameCount;
            if (_cachedSnapshotFrame == currentFrame)
            {
                summary = _cachedOperationalSummary;
                recommendation = _cachedOperationalRecommendation;
                return true;
            }

            summary = _flashlight.BuildOperationalSummary();
            recommendation = _flashlight.BuildOperationalRecommendation();
            _cachedSnapshotFrame = currentFrame;
            _cachedOperationalSummary = summary;
            _cachedOperationalRecommendation = recommendation;
            return true;
        }

        private bool TryGetForwardContextDirectiveCached(out string contextDirective)
        {
            contextDirective = null;

            if (_flashlight == null)
                return false;

            int currentFrame = Time.frameCount;
            if (_cachedContextDirectiveFrame == currentFrame)
            {
                contextDirective = _cachedContextDirective;
                return _cachedHasContextDirective;
            }

            bool hasDirective = TryGetForwardContextDirective(out contextDirective);
            _cachedContextDirectiveFrame = currentFrame;
            _cachedHasContextDirective = hasDirective;
            _cachedContextDirective = contextDirective;
            return hasDirective;
        }

        private void InvalidateSnapshotCache()
        {
            _cachedSnapshotFrame = -1;
            _cachedOperationalSummary = null;
            _cachedOperationalRecommendation = null;
            _cachedContextDirectiveFrame = -1;
            _cachedHasContextDirective = false;
            _cachedContextDirective = null;
        }

        private bool TryGetForwardContextDirective(out string directive)
        {
            directive = null;

            Transform probeOrigin = transform;
            if (_flashlight == null || probeOrigin == null)
                return false;

            var cache = Hecton8.Physics.GlobalQueryCacheManager.GetContext("PlayerLook");
            Ray ray = new Ray(probeOrigin.position, probeOrigin.forward);
            
            if (!cache.TryGet(ray, contextProbeRange, contextMask, out Hecton8.Physics.QueryResult qResult))
            {
                int hitCount = Physics.RaycastNonAlloc(
                    ray,
                    _contextRaycastHits,
                    contextProbeRange,
                    contextMask,
                    QueryTriggerInteraction.Collide);

                if (hitCount <= 0)
                {
                    return false;
                }

                RaycastHit hit = _contextRaycastHits[0];
                qResult = new Hecton8.Physics.QueryResult { hasHit = true, hit = hit };
                cache.Set(ray, contextProbeRange, contextMask, qResult);
            }

            if (!qResult.hasHit) 
                return false;

            RaycastHit finalHit = qResult.hit;

            Collider collider = finalHit.collider;
            if (collider == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(collider, out FieldTargetDescriptor descriptor))
            {
                if (FieldTargetSemantics.TryBuildFlashlightDirective(descriptor, finalHit.distance, out directive))
                    return true;
            }

            if (collider.GetComponent<ScannableTarget>() != null || collider.GetComponentInParent<ScannableTarget>() != null)
            {
                directive = finalHit.distance >= 10f
                    ? "Use FOCUS to read distant probes and hazard points before closing in."
                    : "Use STANDARD while you classify the probe and keep route awareness.";
                return true;
            }

            PickupItem pickup = collider.GetComponent<PickupItem>() ?? collider.GetComponentInParent<PickupItem>();
            if (pickup != null)
            {
                directive = finalHit.distance <= 5f
                    ? "Use FLOOD to sweep the nearby salvage pocket without overshooting the pickup."
                    : "Use STANDARD until the pickup lane tightens, then widen to FLOOD.";
                return true;
            }

            ResourceNode node = collider.GetComponent<ResourceNode>() ?? collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                directive = finalHit.distance >= 9f
                    ? "Use FOCUS to probe the node edge before committing cutter or sampler."
                    : "Use STANDARD to hold visibility on the extraction face.";
                return true;
            }

            BaseModule module = collider.GetComponent<BaseModule>() ?? collider.GetComponentInParent<BaseModule>();
            if (module != null)
            {
                directive = finalHit.distance >= 9f
                    ? "Use FOCUS for distant module reads and service planning."
                    : "Use STANDARD to maintain service visibility on the module face.";
                return true;
            }

            return false;
        }

        private void PublishAssessment(LampAssessment assessment)
        {
            if (_hudNotification != null)
            {
                if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                    _hudNotification.ShowWarning(assessment.BuildHudMessage());
                else
                    _hudNotification.ShowInfo(assessment.BuildHudMessage());
                return;
            }

            Debug.Log(assessment.BuildHudMessage());
        }
    }
}
