using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SeafloorDrillTool : PlayerTool, IToolModule
    {
        private const string DrillSummaryReady = "SEAFLOOR DRILL // READY";
        private const string DrillSummaryCycling = "SEAFLOOR DRILL // CYCLING ";
        private const string DrillSummaryNoTarget = "SEAFLOOR DRILL // NO DRILLABLE CONTACT";
        private const string DrillDirectiveReady = "Primary drives a short controlled bore into Drill-gated resource nodes.";
        private const uint FallbackToolId = 0x5344524Cu; // SDRL

        [Header("Drill")]
        [SerializeField] private float range = 3.4f;
        [SerializeField] private float boreDamage = 22f;
        [SerializeField] private float cooldownSeconds = 0.28f;
        [SerializeField] private float recoilImpulse = 2.2f;
        [SerializeField] private float hapticRatedPower = 28f;
        [SerializeField] private LayerMask drillMask = HectonLayerMasks.FieldToolSurfaceLayerMask;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private float _cooldownRemaining;
        private uint _frameIndex;
        private bool _active;
        private bool _lastUseHadTarget;
        private IInteractionSignalService _interactionSignals;

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheColdDependencies();
            ResetRuntimeState();
        }

        public override void OnDespawn()
        {
            _interactionSignals = null;
            ResetRuntimeState();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            CacheColdDependencies();
            _lastUseHadTarget = true;
        }

        public override void OnUnequip()
        {
            _active = false;
            base.OnUnequip();
        }

        protected override void OnToolRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.InteractionSignals)
                _interactionSignals = currentService as IInteractionSignalService;
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.InteractionSignals)
                _interactionSignals = currentService as IInteractionSignalService;
        }

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped || _cooldownRemaining > 0f || !TryBeginToolUse(deltaTime, true))
                return;

            bool published = TryPublishDrillSignal(out Vector3 drillDirection, out float deliveredPower);
            _lastUseHadTarget = published;
            if (published)
            {
                TryQueuePlayerToolRecoil(drillDirection, ResolveRecoilImpulse());
                QueueToolHapticFeedback(deliveredPower, ResolveHapticRatedPower(), 2);
            }

            _cooldownRemaining = ResolveCooldownSeconds();
        }

        public override void ToolTick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_cooldownRemaining > 0f)
                _cooldownRemaining = math.max(0f, _cooldownRemaining - safeDeltaTime);
        }

        public void Activate()
        {
            _active = true;
        }

        public void Deactivate()
        {
            _active = false;
        }

        public void CancelAction()
        {
            _active = false;
            _cooldownRemaining = 0f;
        }

        public uint GetCapabilityMask()
        {
            return ToolCapabilityMasks.Drill;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalSummaryString()
        {
            return _cooldownRemaining > 0f
                ? DrillSummaryCycling
                : (_lastUseHadTarget ? DrillSummaryReady : DrillSummaryNoTarget);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldownRemaining > 0f)
            {
                AppendText(ref buffer, DrillSummaryCycling);
                buffer.AppendFloat(_cooldownRemaining, 1);
                AppendText(ref buffer, "S");
                return;
            }

            AppendText(ref buffer, _lastUseHadTarget ? DrillSummaryReady : DrillSummaryNoTarget);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalDirectiveString()
        {
            return DrillDirectiveReady;
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, DrillDirectiveReady);
        }

        private void CacheColdDependencies()
        {
            _interactionSignals = GlobalRegistry.InteractionSignals;
        }

        private void ResetRuntimeState()
        {
            _cooldownRemaining = 0f;
            _active = false;
            _lastUseHadTarget = true;
        }

        private bool TryPublishDrillSignal(out Vector3 drillDirection, out float deliveredPower)
        {
            drillDirection = default;
            deliveredPower = 0f;

            IInteractionSignalService interactionService = _interactionSignals;
            if (interactionService == null || !interactionService.IsInitialized)
                return false;

            if (!TryResolveDrillRay(out Vector3 origin, out drillDirection))
                return false;

            float runtimeRange = GetRuntimeMaxRange(ResolveRange());
            if (runtimeRange <= 0f)
                return false;

            if (!RequestPrimarySurfaceHit(
                    origin,
                    drillDirection,
                    runtimeRange,
                    ResolveDrillSurfaceMask(),
                    triggerInteraction,
                    out InteractionSurfaceHit hit) ||
                hit.collider == null)
            {
                return false;
            }

            if (!TryResolveRuntimeAup(origin, out double3 originAup) ||
                !TryResolveRuntimeAup(hit.point, out double3 hitAup))
            {
                return false;
            }

            float runtimePower = ResolveDeliveredPower();
            if (runtimePower <= 0f)
                return false;

            float3 originAbsolute = new float3((float)originAup.x, (float)originAup.y, (float)originAup.z);
            float3 hitAbsolute = new float3((float)hitAup.x, (float)hitAup.y, (float)hitAup.z);
            float3 normal = IsFiniteVector(hit.normal) ? (float3)hit.normal : new float3(0f, 1f, 0f);
            InteractionPacket packet = new InteractionPacket(
                ResolveRuntimeToolId(),
                originAbsolute,
                (float3)drillDirection,
                runtimePower,
                runtimeRange,
                (byte)ToolActionMode.Primary,
                (byte)(_active || IsEquipped ? ToolStateBits.Active : ToolStateBits.Idle),
                NextFrameIndex());

            InteractionSignal signal = new InteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(hit.collider.GetEntityId())),
                hitAbsolute,
                normal,
                runtimePower,
                (byte)InteractionEffectType.Drill,
                0,
                hitAup,
                InteractionSignal.HitPointAupDoubleValid);

            if (!interactionService.Publish(in signal, hit.collider))
                return false;

            deliveredPower = runtimePower;
            return true;
        }

        private int ResolveDrillSurfaceMask()
        {
            return HectonLayerMasks.ResolveSurfaceInteractionLayerMask(drillMask.value) | HectonLayerMasks.VoxelProxyLayerMask;
        }

        private bool TryResolveDrillRay(out Vector3 origin, out Vector3 direction)
        {
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                float3 forward = snapshot.Forward;
                float forwardLengthSq = math.lengthsq(forward);
                if (math.all(math.isfinite(snapshot.RuntimePosition)) &&
                    math.all(math.isfinite(forward)) &&
                    math.isfinite(forwardLengthSq) &&
                    forwardLengthSq > 0.0001f)
                {
                    forward *= math.rsqrt(math.max(forwardLengthSq, 0.0001f));
                    origin = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
                    direction = new Vector3(forward.x, forward.y, forward.z);
                    return true;
                }
            }

            Transform cachedTransform = transform;
            origin = cachedTransform != null ? cachedTransform.position : Vector3.zero;
            direction = cachedTransform != null ? cachedTransform.forward : Vector3.forward;
            if (!IsFiniteVector(origin) || !IsFiniteVector(direction))
                return false;

            float lengthSq = direction.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                direction = Vector3.forward;
            else
                direction *= math.rsqrt(math.max(lengthSq, 0.0001f));

            return IsFiniteVector(direction);
        }

        private float ResolveDeliveredPower()
        {
            float safeDamage = math.isfinite(boreDamage) ? math.max(0f, boreDamage) : 0f;
            float power = GetRuntimePowerScalar(safeDamage) * GetEfficiency() * GetConditionPerformanceScale();
            return math.isfinite(power) ? math.max(0f, power) : 0f;
        }

        private float ResolveRange()
        {
            return math.isfinite(range) ? math.max(0f, range) : 0f;
        }

        private float ResolveCooldownSeconds()
        {
            float cooldown = math.isfinite(cooldownSeconds) ? math.max(0f, cooldownSeconds) : 0f;
            float speed = GetSpeed();
            return cooldown / math.max(0.25f, math.isfinite(speed) ? speed : 1f);
        }

        private float ResolveRecoilImpulse()
        {
            float fallback = math.isfinite(recoilImpulse) ? math.max(0f, recoilImpulse) : 0f;
            return GetRuntimeRecoilImpulse(fallback);
        }

        private float ResolveHapticRatedPower()
        {
            return math.isfinite(hapticRatedPower) ? math.max(1f, hapticRatedPower) : 28f;
        }

        private uint ResolveRuntimeToolId()
        {
            return RuntimeToolId != 0u ? RuntimeToolId : FallbackToolId;
        }

        private uint NextFrameIndex()
        {
            uint next = _frameIndex + 1u;
            _frameIndex = next != 0u ? next : 1u;
            return _frameIndex;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static void AppendText(ref FixedCharBuffer buffer, string text)
        {
            if (!string.IsNullOrEmpty(text))
                buffer.Append(text);
        }
    }
}
