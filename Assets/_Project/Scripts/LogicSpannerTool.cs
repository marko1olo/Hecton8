namespace Hecton8.Gameplay
{
    using Hecton8.Construction;
    using Hecton8.Core;
    using Hecton8.Tools;
    using UnityEngine;

    /// <summary>
    /// Handheld bypass-wiring tool for inserting temporary habitat graph links between two base modules.
    /// </summary>
    [AddComponentMenu("Hecton8/Gameplay/Tools/Logic Spanner")]
    public sealed class LogicSpannerTool : PlayerTool
    {
        private const string IdleDirective = "Acquire a source node to arm a bypass cable.";
        private const string ArmedDirective = "Source node armed. Acquire a second node to route a bypass.";
        private const string LinkedDirective = "Temporary bypass cable inserted into the habitat graph.";
        private const string InvalidTargetDirective = "Target a powered habitat module to reroute base topology.";
        private const string SourceArmedMessage = "LOGIC SPANNER - SOURCE NODE ARMED";
        private const string LinkCreatedMessage = "LOGIC SPANNER - BYPASS LINKED";
        private const string DuplicateLinkMessage = "LOGIC SPANNER - BYPASS ALREADY PRESENT";
        private const string InvalidTargetMessage = "LOGIC SPANNER - INVALID NODE";
        private const string SelectionClearedMessage = "LOGIC SPANNER - SOURCE CLEARED";

        [Header("── Wiring ──────────────────────────")]
        [Tooltip("Maximum node-acquisition range for bypass linking.")]
        [SerializeField] private float wiringRange = 8f;

        [Tooltip("Collision mask used to find habitat modules for bypass routing.")]
        [SerializeField] private LayerMask wiringMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Fallback heat-generation rate authored into the modular runtime.")]
        [SerializeField] private float authoredHeatGenerationRate = 0.12f;

        [Tooltip("Fallback cooldown-rate authored into the modular runtime.")]
        [SerializeField] private float authoredCooldownRate = 0.45f;

        [Tooltip("Fallback recoil impulse authored into the modular runtime.")]
        [SerializeField] private float authoredRecoilImpulse = 0.35f;

        private SpannerState _state;
        private BaseModule _selectedSource;
        private float _lastLinkPulse;

        private enum SpannerState : byte
        {
            Idle = 0,
            SourceArmed = 1,
            LinkCommitted = 2
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ClearSelectionInternal();
        }

        public override void OnDespawn()
        {
            ClearSelectionInternal();
            base.OnDespawn();
        }

        public override void OnUnequip()
        {
            ClearSelectionInternal();
            base.OnUnequip();
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, wiringRange);
            profile.PowerScalar = 1f;
            profile.HeatGenerationRate = Mathf.Max(0f, authoredHeatGenerationRate);
            profile.CooldownRate = Mathf.Max(0f, authoredCooldownRate);
            profile.RecoilImpulse = Mathf.Max(0f, authoredRecoilImpulse);
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, true))
                return;

            if (!TryResolveTargetModule(out BaseModule targetModule))
            {
                ToolHitUtility.ShowWarning(InvalidTargetMessage);
                _state = _selectedSource != null ? SpannerState.SourceArmed : SpannerState.Idle;
                return;
            }

            if (_selectedSource == null)
            {
                _selectedSource = targetModule;
                _state = SpannerState.SourceArmed;
                ToolHitUtility.ShowInfo(SourceArmedMessage);
                return;
            }

            if (ReferenceEquals(_selectedSource, targetModule))
            {
                ToolHitUtility.ShowWarning(InvalidTargetMessage);
                return;
            }

            ConstructionManager constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            if (constructionManager == null)
            {
                ToolHitUtility.ShowWarning(InvalidTargetMessage);
                ClearSelectionInternal();
                return;
            }

            if (!constructionManager.TryCreateTemporaryBypass(_selectedSource, targetModule))
            {
                ToolHitUtility.ShowWarning(DuplicateLinkMessage);
                return;
            }

            _state = SpannerState.LinkCommitted;
            _lastLinkPulse = Time.time;
            _selectedSource = null;
            QueueToolHapticFeedback(Mathf.Max(0.1f, GetRuntimePowerScalar(1f)), 1f);
            ToolHitUtility.ShowInfo(LinkCreatedMessage);
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, false))
                return;

            ClearSelectionInternal();
            ToolHitUtility.ShowInfo(SelectionClearedMessage);
        }

        public override string GetOperationalDirective()
        {
            if (IsBroken)
                return base.GetOperationalDirective();

            switch (_state)
            {
                case SpannerState.SourceArmed:
                    return ArmedDirective;

                case SpannerState.LinkCommitted:
                    return Time.time - _lastLinkPulse <= 0.75f ? LinkedDirective : IdleDirective;

                default:
                    return _selectedSource != null ? ArmedDirective : IdleDirective;
            }
        }

        private bool TryResolveTargetModule(out BaseModule module)
        {
            Transform cachedTransform = transform;
            if (!TryResolveQueuedRaycast(
                    cachedTransform.position,
                    cachedTransform.forward,
                    GetRuntimeMaxRange(wiringRange),
                    wiringMask.value,
                    QueryTriggerInteraction.Ignore,
                    out RaycastHit hit))
            {
                module = null;
                return false;
            }

            module = hit.collider != null
                ? hit.collider.GetComponent<BaseModule>() ?? hit.collider.GetComponentInParent<BaseModule>()
                : null;
            return module != null;
        }

        private void ClearSelectionInternal()
        {
            _selectedSource = null;
            _state = SpannerState.Idle;
        }
    }
}
