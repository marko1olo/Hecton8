namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Construction;
    using Hecton8.Core;
    using Hecton8.Tools;
    using Unity.Mathematics;
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
        private ILogisticsService _constructionLogistics;
        private int _selectedSourceModuleHashId;
        private FixedCharBuffer _hudBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] — logic spanner HUD staging buffer — owner: LogicSpannerTool

        private enum SpannerState : byte
        {
            Idle = 0,
            SourceArmed = 1,
            LinkCommitted = 2
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _constructionLogistics = GlobalRegistry.Logistics;
            ClearSelectionInternal();
        }

        public override void OnDespawn()
        {
            ClearSelectionInternal();
            _constructionLogistics = null;
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _constructionLogistics = GlobalRegistry.Logistics;
            ConnectionSplineBatchRenderer.SetLogisticsPathHighlightActive(true);
        }

        public override void OnUnequip()
        {
            ConnectionSplineBatchRenderer.SetLogisticsPathHighlightActive(false);
            ClearSelectionInternal();
            base.OnUnequip();
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = math.max(0.1f, wiringRange);
            profile.PowerScalar = 1f;
            profile.HeatGenerationRate = math.max(0f, authoredHeatGenerationRate);
            profile.CooldownRate = math.max(0f, authoredCooldownRate);
            profile.RecoilImpulse = math.max(0f, authoredRecoilImpulse);
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, true))
                return;

            if (!TryResolveTargetModule(out BaseModule targetModule))
            {
                PublishWarning(InvalidTargetMessage);
                _state = _selectedSource != null ? SpannerState.SourceArmed : SpannerState.Idle;
                return;
            }

            int targetModuleHashId = ResolveModuleHashId(targetModule);
            if (targetModuleHashId == 0)
            {
                PublishWarning(InvalidTargetMessage);
                _state = _selectedSource != null ? SpannerState.SourceArmed : SpannerState.Idle;
                return;
            }

            if (_selectedSource == null)
            {
                _selectedSource = targetModule;
                _selectedSourceModuleHashId = targetModuleHashId;
                _state = SpannerState.SourceArmed;
                PublishInfo(SourceArmedMessage);
                return;
            }

            if (ReferenceEquals(_selectedSource, targetModule))
            {
                PublishWarning(InvalidTargetMessage);
                return;
            }

            ILogisticsService logistics = _constructionLogistics;
            if (logistics == null)
            {
                PublishWarning(InvalidTargetMessage);
                ClearSelectionInternal();
                return;
            }

            if (_selectedSourceModuleHashId == 0)
            {
                PublishWarning(InvalidTargetMessage);
                ClearSelectionInternal();
                return;
            }

            if (!logistics.TryCreateTemporaryBypass(
                    _selectedSource,
                    targetModule))
            {
                PublishWarning(DuplicateLinkMessage);
                return;
            }

            _state = SpannerState.LinkCommitted;
            _selectedSource = null;
            _selectedSourceModuleHashId = 0;
            QueueToolHapticFeedback(math.max(0.1f, GetRuntimePowerScalar(1f)), 1f);
            PublishInfo(LinkCreatedMessage);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, "LOGIC SPANNER // ");
            if (IsBroken)
            {
                AppendText(ref buffer, "BROKEN");
                return;
            }

            switch (_state)
            {
                case SpannerState.SourceArmed:
                    AppendText(ref buffer, "SOURCE ARMED");
                    break;
                case SpannerState.LinkCommitted:
                    AppendText(ref buffer, WasRecentlyUsed(0.75f) ? "BYPASS LINKED" : "STANDBY");
                    break;
                default:
                    AppendText(ref buffer, _selectedSource != null ? "SOURCE ARMED" : "STANDBY");
                    break;
            }

            AppendText(ref buffer, " // RNG ");
            buffer.AppendFloat(GetRuntimeMaxRange(wiringRange), 1);
            AppendText(ref buffer, "M");
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, false))
                return;

            ClearSelectionInternal();
            PublishInfo(SelectionClearedMessage);
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            if (IsBroken)
                return base.BuildLegacyOperationalDirectiveString();

            switch (_state)
            {
                case SpannerState.SourceArmed:
                    return ArmedDirective;

                case SpannerState.LinkCommitted:
                    return WasRecentlyUsed(0.75f) ? LinkedDirective : IdleDirective;

                default:
                    return _selectedSource != null ? ArmedDirective : IdleDirective;
            }
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (IsBroken)
            {
                base.WriteOperationalDirective(ref buffer);
                return;
            }

            switch (_state)
            {
                case SpannerState.SourceArmed:
                    AppendText(ref buffer, ArmedDirective);
                    return;

                case SpannerState.LinkCommitted:
                    AppendText(ref buffer, WasRecentlyUsed(0.75f) ? LinkedDirective : IdleDirective);
                    return;

                default:
                    AppendText(ref buffer, _selectedSource != null ? ArmedDirective : IdleDirective);
                    return;
            }
        }

        private bool TryResolveTargetModule(out BaseModule module)
        {
            if (!TryResolveActionRay(out Vector3 origin, out Vector3 direction))
            {
                module = null;
                return false;
            }

            if (!TryResolvePrimarySurfaceHit(
                    origin,
                    direction,
                    GetRuntimeMaxRange(wiringRange),
                    wiringMask.value,
                    QueryTriggerInteraction.Ignore,
                    out InteractionSurfaceHit hit))
            {
                module = null;
                return false;
            }

            module = null;
            if (hit.collider == null)
                return false;

            if (hit.collider.TryGetComponent(out module))
                return module != null;

            Transform current = hit.collider.transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent(out module))
                    return module != null;

                current = current.parent;
            }

            return module != null;
        }

        private bool TryResolveActionRay(out Vector3 origin, out Vector3 direction)
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

        private static int ResolveModuleHashId(BaseModule module)
        {
            if (module != null &&
                module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data.ModuleHashId;
            }

            return 0;
        }

        private void ClearSelectionInternal()
        {
            _selectedSource = null;
            _selectedSourceModuleHashId = 0;
            _state = SpannerState.Idle;
        }

        private void PublishInfo(string message)
        {
            _hudBuffer.Clear();
            if (AppendText(ref _hudBuffer, message))
                ToolHitUtility.ShowInfo(in _hudBuffer);
        }

        private void PublishWarning(string message)
        {
            _hudBuffer.Clear();
            if (AppendText(ref _hudBuffer, message))
                ToolHitUtility.ShowWarning(in _hudBuffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }
    }
}
