using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Interaction;
using Hecton8.Tools;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string HarpoonCategory = "HARPOON";
        private static readonly int _TetherPositionsId = Shader.PropertyToID("_TetherPositions");
        private static readonly int _TetherSegmentTensionsId = Shader.PropertyToID("_TetherSegmentTensions");
        private static readonly int _TetherDrawParamsId = Shader.PropertyToID("_TetherDrawParams");
        private enum TetherRegistrationResult : byte
        {
            None = 0,
            Reel = 1,
            HeavyTow = 2,
            Grapple = 3
        }

        public readonly struct HarpoonTextSegment
        {
            public const byte HasStringArg0 = 1 << 0;
            public const byte HasFloatArg1 = 1 << 1;
            public const byte HasFloatArg2 = 1 << 2;

            public readonly string Template;
            private readonly string _arg0;
            private readonly float _arg1;
            private readonly float _arg2;
            private readonly byte _argumentMask;

            public HarpoonTextSegment(string template)
            {
                Template = template;
                _arg0 = null;
                _arg1 = 0f;
                _arg2 = 0f;
                _argumentMask = 0;
            }

            private HarpoonTextSegment(string template, string arg0, float arg1, float arg2, byte argumentMask)
            {
                Template = template;
                _arg0 = arg0;
                _arg1 = arg1;
                _arg2 = arg2;
                _argumentMask = argumentMask;
            }

            public static HarpoonTextSegment FormatString(string template, string arg0)
            {
                return new HarpoonTextSegment(template, arg0, 0f, 0f, HasStringArg0);
            }

            public static HarpoonTextSegment FormatStringFloat(string template, string arg0, float arg1)
            {
                return new HarpoonTextSegment(template, arg0, arg1, 0f, HasStringArg0 | HasFloatArg1);
            }

            public static HarpoonTextSegment FormatStringFloatFloat(string template, string arg0, float arg1, float arg2)
            {
                return new HarpoonTextSegment(template, arg0, arg1, arg2, HasStringArg0 | HasFloatArg1 | HasFloatArg2);
            }

            public bool TryWrite(ref FixedCharBuffer buffer)
            {
                return AppendFormattedText(ref buffer, Template, _arg0, _arg1, _arg2, _argumentMask);
            }
        }

        private readonly struct HarpoonAssessment
        {
            public readonly HarpoonTextSegment HeadlineText;
            public readonly HarpoonTextSegment SummaryText;
            public readonly HarpoonTextSegment RecommendationText;
            public readonly string Severity;

            public HarpoonAssessment(string headline, string summary, string recommendation, string severity)
                : this(
                    new HarpoonTextSegment(headline),
                    new HarpoonTextSegment(summary),
                    new HarpoonTextSegment(recommendation),
                    severity)
            {
            }

            public HarpoonAssessment(string headline, HarpoonTextSegment summary, HarpoonTextSegment recommendation, string severity)
                : this(new HarpoonTextSegment(headline), summary, recommendation, severity)
            {
            }

            public HarpoonAssessment(
                HarpoonTextSegment headline,
                HarpoonTextSegment summary,
                HarpoonTextSegment recommendation,
                string severity)
            {
                HeadlineText = headline;
                SummaryText = summary;
                RecommendationText = recommendation;
                Severity = severity;
            }

            public string Headline => HeadlineText.Template;
            public string Summary => SummaryText.Template;
            public string Recommendation => RecommendationText.Template;

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                return TryWriteHeadline(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       TryWriteSummary(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       TryWriteRecommendation(ref buffer);
            }

            public bool TryWriteHeadline(ref FixedCharBuffer buffer) => HeadlineText.TryWrite(ref buffer);
            public bool TryWriteSummary(ref FixedCharBuffer buffer) => SummaryText.TryWrite(ref buffer);
            public bool TryWriteRecommendation(ref FixedCharBuffer buffer) => RecommendationText.TryWrite(ref buffer);
        }

        [Header("Harpoon")]
        [SerializeField] private float range = 36f;
        [SerializeField] private float damage = 42f;
        [SerializeField] private float impulse = 18f;
        [SerializeField] private float reelImpulse = 14f;
        [SerializeField] private float maxReelMass = 55f;
        private const float PlayerRecoilEquivalentMassKg = 80f;
        [SerializeField] private float shotCooldown = 0.85f;
        [SerializeField] private LayerMask targetMask = Hecton8.Core.HectonLayerMasks.FieldToolSurfaceLayerMask;
        [SerializeField] private float feedbackInterval = 0.35f;
        [SerializeField] private float tetherDuration = 5f;
        [SerializeField] private float tetherPullBonus = 1.35f;

        [Header("Tracer")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private float tracerLifetime = 0.08f;
        [SerializeField] private Color tracerColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        [SerializeField, Range(0.002f, 0.05f)] private float tracerRadius = 0.012f;

        private float _cooldown;
        private float _tracerTimer;
        private float _tracerShaderTime;
        private bool _tracerActive;
        private Vector3 _tracerStartPoint;
        private Vector3 _tracerEndPoint;
        private float _feedbackCooldownRemaining;
        private Rigidbody _tetheredBody;
        private Collider _tetheredCollider;
        private HeavyTowWinch _heavyTowWinch;
        private HectonPlayerMovement _playerMovement;
        private string _tetheredName;
        private string _tetheredNameUpper;
        private Collider _grappleAnchorCollider;
        private Transform _grappleAnchorTransform;
        private Vector3 _grappleAnchorLocalPoint;
        private string _grappleAnchorName;
        private string _grappleAnchorNameUpper;
        private uint _assessmentEvaluationStamp;
        private uint _cachedAssessmentStamp;
        private bool _cachedAssessmentValid;
        private HarpoonAssessment _cachedAssessment;
        private float _tetherRemaining;
        private ILocalizationTextReadModel _localization;
        private IPhysicsService _physicsService;
        private FixedCharBuffer _hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - harpoon HUD staging buffer - owner: HarpoonLauncherTool
        private FixedCharBuffer _logTitleBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - harpoon operation log title staging buffer - owner: HarpoonLauncherTool
        private FixedCharBuffer _logSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - harpoon operation log summary staging buffer - owner: HarpoonLauncherTool
        private GraphicsBuffer _tracerPositionBuffer;
        private GraphicsBuffer _tracerTensionBuffer;
        private GraphicsBuffer _tracerDrawParamsBuffer;
        private MaterialPropertyBlock _tracerPropertyBlock;
        private bool _lateFrameRegistered;

        private void Awake()
        {
            ResolveHeavyTowWinch();
            EnsureTracer();
            SetTracer(false, Vector3.zero);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _localization = GlobalRegistry.LocalizationText;
            _physicsService = GlobalRegistry.Physics;
            ResolveHeavyTowWinch();
            ResolvePlayerMovement();
            EnsureTracer();
            _feedbackCooldownRemaining = 0f;
            _tracerShaderTime = 0f;
            TryRegisterLateFrameTick();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _localization = GlobalRegistry.LocalizationText;
            _physicsService = GlobalRegistry.Physics;
            ResolveHeavyTowWinch();
            ResolvePlayerMovement();
            EnsureTracer();
            InvalidateAssessmentCache();
        }

        public override void OnDespawn()
        {
            TryUnregisterLateFrameTick();
            base.OnDespawn();
            _localization = null;
            _physicsService = null;
            _feedbackCooldownRemaining = 0f;
            ClearTether();
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            base.OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool needsLateFrameTick = _lateFrameRegistered || _tracerActive || isActiveAndEnabled;
                    TryUnregisterLateFrameTick();
                    if (needsLateFrameTick && currentService != null && isActiveAndEnabled)
                        TryRegisterLateFrameTick();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
            }
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, range);
            profile.PowerScalar = Mathf.Max(0.1f, damage);
            profile.RecoilImpulse = Mathf.Max(0f, impulse);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryResolveToolPose(out Vector3 toolOrigin, out Vector3 toolForward))
            {
                WarnReel(StableText(LocalizationKeys.HARPOON_HUD_SHOT_CLEAR, "HARPOON - SHOT RETURNED CLEAR"));
                return;
            }

            float runtimeRange = GetRuntimeMaxRange(range);
            float runtimeDamage = GetRuntimePowerScalar(damage);
            Vector3 endPoint = toolOrigin + toolForward * runtimeRange;

            if (RequestPrimarySurfaceHit(toolOrigin, toolForward, runtimeRange, ResolveTargetSurfaceMask(), QueryTriggerInteraction.Ignore, out InteractionSurfaceHit hit))
            {
                endPoint = hit.point;
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    runtimeDamage * GetEfficiency(),
                    hit.point,
                    toolForward,
                    impulse,
                    DamageSourceIds.Harpoon,
                    CombatDamageTypes.Impact,
                    0u,
                    0f,
                    ToolCapabilityMasks.Grab | ToolCapabilityMasks.Bash);

                TetherRegistrationResult tetherResult = TryRegisterTether(hit);

                if (IsFeedbackReady())
                {
                    bool lightTetherReady = tetherResult == TetherRegistrationResult.Reel;
                    bool heavyTowReady = tetherResult == TetherRegistrationResult.HeavyTow;
                    bool grappleReady = tetherResult == TetherRegistrationResult.Grapple;
                    HarpoonAssessment assessment = BuildAssessment(hit.collider, hit.distance, lightTetherReady || heavyTowReady || grappleReady);
                    HarpoonAssessment outboundAssessment = lightTetherReady
                        ? new HarpoonAssessment(
                            HarpoonTextSegment.FormatString(
                                StableText(LocalizationKeys.HARPOON_HEADLINE_TETHER_LOCK, "HARPOON - TETHER LOCK [{0}]"),
                                _tetheredNameUpper ?? StableText(LocalizationKeys.HARPOON_TARGET, "TARGET")),
                            assessment.SummaryText,
                            assessment.RecommendationText,
                            assessment.Severity)
                        : heavyTowReady
                            ? new HarpoonAssessment(
                                HarpoonTextSegment.FormatString(
                                    "HARPOON - HEAVY TOW LOCK [{0}]",
                                    _heavyTowWinch != null ? _heavyTowWinch.CurrentTargetNameUpper ?? "CARGO" : "CARGO"),
                                assessment.SummaryText,
                                new HarpoonTextSegment("Throttle gently. Tow drag is now loading the suit and scooter."),
                                assessment.Severity)
                        : grappleReady
                            ? new HarpoonAssessment(
                                HarpoonTextSegment.FormatString(
                                    "HARPOON - EXOSUIT GRAPPLE LOCK [{0}]",
                                    _grappleAnchorNameUpper ?? "ANCHOR"),
                                assessment.SummaryText,
                                new HarpoonTextSegment("Secondary reels the exosuit toward the locked structure. Release input to bleed the line."),
                                assessment.Severity)
                        : new HarpoonAssessment(
                            StableText(LocalizationKeys.HARPOON_HEADLINE_TARGET_PINNED, "HARPOON - TARGET PINNED"),
                            assessment.SummaryText,
                            assessment.RecommendationText,
                            assessment.Severity);
                    PublishAssessment(outboundAssessment);
                    RecordAssessmentLog(outboundAssessment, assessment);
                    ArmFeedbackCooldown();
                }
            }
            else if (IsFeedbackReady())
            {
                InvalidateAssessmentCache();
                PublishWarningMessage(StableText(LocalizationKeys.HARPOON_HUD_SHOT_CLEAR, "HARPOON - SHOT RETURNED CLEAR"));
                FieldOperationLogSystem.RecordOperation(
                    StableText(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                    StableText(LocalizationKeys.HARPOON_LOG_SHOT_CLEAR_TITLE, "HARPOON SHOT RETURNED CLEAR"),
                    StableText(LocalizationKeys.HARPOON_LOG_SHOT_CLEAR_MESSAGE, "No target intersected the last harpoon firing lane."),
                    "WARN");
                ArmFeedbackCooldown();
            }

            ApplyLaunchRecoil(toolForward, runtimeDamage);
            SetTracer(true, endPoint);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
            {
                _heavyTowWinch.ReleaseTow(false);
                PublishInfoMessage("HARPOON - HEAVY TOW RELEASED");
                _cooldown = shotCooldown * 0.35f;
                return;
            }

            if (TryReelTetheredTarget())
                return;

            if (TryReelExosuitGrapple())
                return;

            if (!TryQueueTargetHit(out InteractionSurfaceHit hit, out Vector3 toolOrigin, out Vector3 toolForward))
            {
                InvalidateAssessmentCache();
                WarnReel(StableText(LocalizationKeys.HARPOON_HUD_NO_REEL_LOCK, "HARPOON - NO REEL LOCK"));
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                PublishAssessment(BuildAssessment(hit.collider, hit.distance, false));
                ArmFeedbackCooldown();
                return;
            }

            if (body == null || body.isKinematic || body.mass > maxReelMass)
            {
                if (body != null)
                {
                    PublishAssessment(BuildAssessment(hit.collider, hit.distance, false));
                    ArmFeedbackCooldown();
                }
                else
                {
                    WarnReel(StableText(LocalizationKeys.HARPOON_HUD_REEL_LOCK_INVALID, "HARPOON - REEL LOCK INVALID"));
                }
                return;
            }

            Vector3 direction = ResolveSafeDirection(toolOrigin - body.worldCenterOfMass, toolForward);
            _physicsService?.QueueForce(body, direction * reelImpulse, ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(direction, reelImpulse);

            if (IsFeedbackReady())
            {
                HarpoonAssessment reelAssessment = new HarpoonAssessment(
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_HEADLINE_REEL_IMPULSE, "HARPOON - REEL IMPULSE APPLIED")),
                    HarpoonTextSegment.FormatStringFloat(
                        StableText(LocalizationKeys.HARPOON_SUMMARY_REEL_IMPULSE, "{0} is inside safe reel mass at {1:0.0} kg."),
                        ResolveTargetLabel(),
                        body.mass),
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_RECOMMEND_REEL_IMPULSE, "Pull it into reach or keep pressure until it drifts clear.")),
                    "INFO");
                PublishAssessment(reelAssessment);
                RecordOperationLog(
                    StableText(LocalizationKeys.HARPOON_LOG_REEL_IMPULSE_TITLE, "HARPOON REEL IMPULSE"),
                    HarpoonTextSegment.FormatStringFloat(
                        StableText(LocalizationKeys.HARPOON_LOG_REEL_IMPULSE_MESSAGE, "{0} reeled with impulse on {1:0.0} kg target mass."),
                        ResolveTargetLabel(),
                        body.mass),
                    "INFO");
                ArmFeedbackCooldown();
            }

            SetTracer(true, hit.point);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.65f;
        }

        public override void ToolTick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;

            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - safeDeltaTime);

            if (_feedbackCooldownRemaining > 0f)
                _feedbackCooldownRemaining = math.max(0f, _feedbackCooldownRemaining - safeDeltaTime);

            if (_tetherRemaining > 0f)
            {
                _tetherRemaining -= safeDeltaTime;
                if (_tetherRemaining <= 0f || (!IsTetherValid() && !IsGrappleValid()))
                    ClearTether();
            }

            if (_tracerTimer > 0f)
            {
                _tracerTimer -= safeDeltaTime;
                if (_tracerTimer <= 0f)
                    SetTracer(false, Vector3.zero);
            }

            _tracerShaderTime += safeDeltaTime;
            if (!math.isfinite(_tracerShaderTime) || _tracerShaderTime > 4096f)
                _tracerShaderTime = 0f;

            if (!IsGrappleValid() && _grappleAnchorCollider != null)
                ClearTether();
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalSummaryString()
        {
            return HarpoonCategory;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                if (!TryAppendFloatTemplate(
                        ref buffer,
                        StableText(LocalizationKeys.HARPOON_OPERATIONAL_RECHARGING, "HARPOON // RECHARGING {0:0.0}S"),
                        _cooldown))
                {
                    buffer.Clear();
                    AppendText(ref buffer, "HARPOON // RECHARGING ");
                    buffer.AppendFloat(_cooldown, 1);
                    AppendText(ref buffer, "S");
                }

                return;
            }

            if (IsTetherValid())
            {
                string targetName = _tetheredNameUpper ?? StableText(LocalizationKeys.HARPOON_TARGET, "TARGET");
                if (!TryAppendSingleArgumentTemplate(
                        ref buffer,
                        StableText(LocalizationKeys.HARPOON_OPERATIONAL_TETHER_LOCK, "HARPOON // TETHER LOCK // {0}"),
                        targetName))
                {
                    buffer.Clear();
                    AppendText(ref buffer, "HARPOON // TETHER LOCK // ");
                    AppendText(ref buffer, targetName);
                }

                return;
            }

            if (IsGrappleValid())
            {
                AppendText(ref buffer, "HARPOON // EXOSUIT GRAPPLE // ");
                AppendText(ref buffer, _grappleAnchorNameUpper ?? "ANCHOR");
                return;
            }

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
            {
                AppendText(ref buffer, "HARPOON // HEAVY TOW // ");
                AppendText(ref buffer, _heavyTowWinch.CurrentTargetNameUpper ?? "CARGO");
                return;
            }

            if (TryReadCachedAssessmentSnapshot(out HarpoonAssessment assessment))
            {
                if (!TryAppendSingleArgumentTemplate(
                        ref buffer,
                        StableText(LocalizationKeys.HARPOON_OPERATIONAL_ASSESSMENT, "HARPOON // {0}"),
                        assessment.Headline))
                {
                    buffer.Clear();
                    AppendText(ref buffer, "HARPOON // ");
                    AppendText(ref buffer, assessment.Headline);
                }

                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.HARPOON_OPERATIONAL_READY, "HARPOON // READY"));
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override string BuildLegacyOperationalDirectiveString()
        {
            if (_cooldown > 0f)
                return StableText(LocalizationKeys.HARPOON_DIRECTIVE_RECHARGING, "Winch and launcher are resetting for the next shot.");

            if (IsTetherValid())
                return StableText(LocalizationKeys.HARPOON_DIRECTIVE_TETHERED, "Secondary reels the tethered target. Keep distance or break the line if needed.");

            if (IsGrappleValid())
                return "Secondary reels the exosuit toward the locked anchor. Stop reeling to let the line relax.";

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
                return "Secondary releases the heavy tow. Keep thrust smooth or the cable will snap.";

            if (TryReadCachedAssessmentSnapshot(out HarpoonAssessment assessment))
                return assessment.Recommendation;

            return StableText(LocalizationKeys.HARPOON_DIRECTIVE_READY, "Primary fires and tags a lane. Secondary reels a light target or an active tether.");
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, StableText(LocalizationKeys.HARPOON_DIRECTIVE_RECHARGING, "Winch and launcher are resetting for the next shot."));
                return;
            }

            if (IsTetherValid())
            {
                AppendText(ref buffer, StableText(LocalizationKeys.HARPOON_DIRECTIVE_TETHERED, "Secondary reels the tethered target. Keep distance or break the line if needed."));
                return;
            }

            if (IsGrappleValid())
            {
                AppendText(ref buffer, "Secondary reels the exosuit toward the locked anchor. Stop reeling to let the line relax.");
                return;
            }

            if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
            {
                AppendText(ref buffer, "Secondary releases the heavy tow. Keep thrust smooth or the cable will snap.");
                return;
            }

            if (TryReadCachedAssessmentSnapshot(out HarpoonAssessment assessment))
            {
                AppendText(ref buffer, assessment.Recommendation);
                return;
            }

            AppendText(ref buffer, StableText(LocalizationKeys.HARPOON_DIRECTIVE_READY, "Primary fires and tags a lane. Secondary reels a light target or an active tether."));
        }

        private void SetTracer(bool active, Vector3 endPoint)
        {
            _tracerActive = active;
            if (!active)
                return;

            _tracerStartPoint = TryResolveToolPose(out Vector3 origin, out _)
                ? origin
                : (IsFinite(_tracerStartPoint) ? _tracerStartPoint : Vector3.zero);
            _tracerEndPoint = IsFinite(endPoint) ? endPoint : _tracerStartPoint;
        }

        private void EnsureTracer()
        {
            if (_tracerPositionBuffer != null && _tracerTensionBuffer != null && _tracerDrawParamsBuffer != null)
                return;

            _tracerPositionBuffer ??= new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                2,
                UnsafeUtility.SizeOf<GpuCableSplinePointDTO>()); // COLD ALLOC: GraphicsBuffer[2] - harpoon GPU tracer points - owner: HarpoonLauncherTool
            _tracerTensionBuffer ??= new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<float>()); // COLD ALLOC: GraphicsBuffer[1] - harpoon tracer stress scalar - owner: HarpoonLauncherTool
            _tracerDrawParamsBuffer ??= new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - harpoon tracer draw constants - owner: HarpoonLauncherTool
            _tracerPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - harpoon tracer GPU bindings - owner: HarpoonLauncherTool
        }

        private bool HasTracerReady()
        {
            return tracerMaterial != null &&
                   _tracerPositionBuffer != null &&
                   _tracerTensionBuffer != null &&
                   _tracerDrawParamsBuffer != null &&
                   _tracerPropertyBlock != null;
        }

        public void LateFrameTick()
        {
            RenderTracer();
        }

        private void RenderTracer()
        {
            if (!_tracerActive || _tracerTimer <= 0f)
                return;

            if (!HasTracerReady())
                return;

            Material material = tracerMaterial;
            if (material == null)
                return;

            Vector3 start = IsFinite(_tracerStartPoint) ? _tracerStartPoint : Vector3.zero;
            Vector3 end = IsFinite(_tracerEndPoint) ? _tracerEndPoint : start;
            UploadTracerGpuData(start, end);

            _tracerPropertyBlock.SetBuffer(_TetherPositionsId, _tracerPositionBuffer);
            _tracerPropertyBlock.SetBuffer(_TetherSegmentTensionsId, _tracerTensionBuffer);
            _tracerPropertyBlock.SetBuffer(_TetherDrawParamsId, _tracerDrawParamsBuffer);

            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 size = AbsVector(end - start) + Vector3.one * Mathf.Max(0.1f, tracerRadius * 8f);
            RenderParams renderParams = new RenderParams(material)
            {
                matProps = _tracerPropertyBlock,
                worldBounds = new Bounds(midpoint, size),
                layer = gameObject.layer
            };
            UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, 1);
        }

        private void UploadTracerGpuData(Vector3 start, Vector3 end)
        {
            NativeArray<GpuCableSplinePointDTO> points = _tracerPositionBuffer.LockBufferForWrite<GpuCableSplinePointDTO>(0, 2);
            try
            {
                points[0] = new GpuCableSplinePointDTO
                {
                    Position = new float3(start.x, start.y, start.z),
                    Tension01 = 0f
                };
                points[1] = new GpuCableSplinePointDTO
                {
                    Position = new float3(end.x, end.y, end.z),
                    Tension01 = 0f
                };
            }
            finally
            {
                _tracerPositionBuffer.UnlockBufferAfterWrite<GpuCableSplinePointDTO>(2);
            }

            NativeArray<float> tensions = _tracerTensionBuffer.LockBufferForWrite<float>(0, 1);
            try
            {
                tensions[0] = 0f;
            }
            finally
            {
                _tracerTensionBuffer.UnlockBufferAfterWrite<float>(1);
            }

            Color safeColor = tracerColor;
            NativeArray<GpuCableDrawParamsDTO> drawParams = _tracerDrawParamsBuffer.LockBufferForWrite<GpuCableDrawParamsDTO>(0, 1);
            try
            {
                drawParams[0] = new GpuCableDrawParamsDTO
                {
                    Color = new float4(safeColor.r, safeColor.g, safeColor.b, safeColor.a),
                    StressColor = new float4(1f, 0.55f, 0.18f, 0.95f),
                    Params0 = new float4(0f, 1f, 2f, Mathf.Max(0.002f, tracerRadius)),
                    Params1 = new float4(0f, 0f, 0f, 0f),
                    Params2 = new float4(_tracerShaderTime, 0f, 0f, 0f)
                };
            }
            finally
            {
                _tracerDrawParamsBuffer.UnlockBufferAfterWrite<GpuCableDrawParamsDTO>(1);
            }
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrameTick();
            ReleaseTracerResources();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private void ReleaseTracerResources()
        {
            if (_tracerPositionBuffer != null)
            {
                _tracerPositionBuffer.Release();
                _tracerPositionBuffer = null;
            }

            if (_tracerTensionBuffer != null)
            {
                _tracerTensionBuffer.Release();
                _tracerTensionBuffer = null;
            }

            if (_tracerDrawParamsBuffer != null)
            {
                _tracerDrawParamsBuffer.Release();
                _tracerDrawParamsBuffer = null;
            }
        }

        private void ResolveHeavyTowWinch()
        {
            if (_heavyTowWinch == null)
                TryResolveComponentInParents(transform.parent, out _heavyTowWinch);
        }

        private static bool TryResolveComponentInParents<T>(Transform current, out T component) where T : Component
        {
            component = null;

            for (; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out component))
                    return true;
            }

            return false;
        }

        private void ResolvePlayerMovement()
        {
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext))
            {
                _playerMovement = playerContext.PlayerMovement;
                return;
            }

            _playerMovement = null;
        }

        private void WarnReel(string message)
        {
            if (!IsFeedbackReady())
                return;

            PublishWarningMessage(message);
            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                message,
                StableText(LocalizationKeys.HARPOON_LOG_REEL_FAILED_MESSAGE, "Secondary reel command failed for the current target."),
                "WARN");
            ArmFeedbackCooldown();
        }

        private TetherRegistrationResult TryRegisterTether(InteractionSurfaceHit hit)
        {
            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
            {
                if (TryRegisterExosuitGrapple(hit, null))
                    return TetherRegistrationResult.Grapple;

                ClearTether();
                return TetherRegistrationResult.None;
            }

            if (body == null || body.isKinematic)
            {
                if (TryRegisterExosuitGrapple(hit, body))
                    return TetherRegistrationResult.Grapple;

                ClearTether();
                return TetherRegistrationResult.None;
            }

            if (body.mass <= maxReelMass)
            {
                if (_heavyTowWinch != null && _heavyTowWinch.HasActiveTow)
                    _heavyTowWinch.ReleaseTow(false);

                _tetheredBody = body;
                _tetheredCollider = hit.collider;
                _tetheredName = ResolveTargetLabel();
                _tetheredNameUpper = string.IsNullOrWhiteSpace(_tetheredName)
                    ? StableText(LocalizationKeys.HARPOON_TARGET, "TARGET")
                    : _tetheredName;
                InvalidateAssessmentCache();
                _tetherRemaining = tetherDuration;
                return TetherRegistrationResult.Reel;
            }

            ClearTether();
            if (_heavyTowWinch != null && _heavyTowWinch.TryAttach(body, hit.collider, hit.distance))
                return TetherRegistrationResult.HeavyTow;

            if (TryRegisterExosuitGrapple(hit, body))
                return TetherRegistrationResult.Grapple;

            return TetherRegistrationResult.None;
        }

        private bool TryRegisterExosuitGrapple(InteractionSurfaceHit hit, Rigidbody body)
        {
            if (_playerMovement == null ||
                _playerMovement.CurrentLocomotionMode != PlayerLocomotionMode.ExosuitLocomotion ||
                hit.collider == null)
            {
                return false;
            }

            if (body != null && !body.isKinematic && body.mass <= maxReelMass)
                return false;

            _grappleAnchorCollider = hit.collider;
            _grappleAnchorTransform = hit.collider.transform;
            _grappleAnchorLocalPoint = _grappleAnchorTransform != null
                ? _grappleAnchorTransform.InverseTransformPoint(hit.point)
                : hit.point;
            _grappleAnchorName = ResolveAnchorLabel();
            _grappleAnchorNameUpper = _grappleAnchorName;
            _tetherRemaining = tetherDuration;
            InvalidateAssessmentCache();
            return true;
        }

        private bool TryReelTetheredTarget()
        {
            if (!IsTetherValid())
                return false;

            if (!TryResolveToolPose(out Vector3 toolOrigin, out Vector3 toolForward))
            {
                ClearTether();
                WarnReel(StableText(LocalizationKeys.HARPOON_HUD_REEL_LOCK_INVALID, "HARPOON - REEL LOCK INVALID"));
                return true;
            }

            Vector3 direction = ResolveSafeDirection(toolOrigin - _tetheredBody.worldCenterOfMass, toolForward);
            float impulseAmount = reelImpulse * tetherPullBonus;
            _physicsService?.QueueForce(_tetheredBody, direction * impulseAmount, ForceMode.Impulse);
            ToolHitUtility.TryApplyRelativeCarrierImpulse(direction, impulseAmount);

            if (IsFeedbackReady())
            {
                HarpoonAssessment tetherAssessment = new HarpoonAssessment(
                    HarpoonTextSegment.FormatString(
                        StableText(LocalizationKeys.HARPOON_HEADLINE_TETHER_REEL, "HARPOON - TETHER REEL [{0}]"),
                        _tetheredNameUpper ?? StableText(LocalizationKeys.HARPOON_TARGET, "TARGET")),
                    HarpoonTextSegment.FormatString(
                        StableText(LocalizationKeys.HARPOON_SUMMARY_TETHER_REEL, "{0} remains inside tether control range."),
                        _tetheredName),
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_RECOMMEND_TETHER_REEL, "Keep reeling for control or release to reset the lane.")),
                    "INFO");
                PublishAssessment(tetherAssessment);
                RecordOperationLog(
                    StableText(LocalizationKeys.HARPOON_LOG_TETHER_REEL_TITLE, "HARPOON TETHER REEL"),
                    HarpoonTextSegment.FormatString(
                        StableText(LocalizationKeys.HARPOON_LOG_TETHER_REEL_MESSAGE, "{0} reeled through active tether lock."),
                        _tetheredName),
                    "INFO");
                ArmFeedbackCooldown();
            }

            SetTracer(true, _tetheredBody.worldCenterOfMass);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.5f;
            _tetherRemaining = tetherDuration;
            return true;
        }

        private bool TryReelExosuitGrapple()
        {
            if (!IsGrappleValid() || _playerMovement == null || !TryGetGrappleAnchorPoint(out Vector3 anchorPointWS))
                return false;

            _playerMovement.ApplyExosuitGrappleAnchor(anchorPointWS);
            SetTracer(true, anchorPointWS);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.35f;
            _tetherRemaining = tetherDuration;

            if (IsFeedbackReady())
            {
                Vector3 toolOrigin = TryResolveToolPose(out Vector3 resolvedOrigin, out _) ? resolvedOrigin : anchorPointWS;
                PublishAssessment(new HarpoonAssessment(
                    HarpoonTextSegment.FormatString("HARPOON - EXOSUIT REEL [{0}]", _grappleAnchorNameUpper ?? "ANCHOR"),
                    HarpoonTextSegment.FormatStringFloat("{0} is holding the climb line at {1:0.0} m.", _grappleAnchorName, ApproximateDistanceMeters(toolOrigin, anchorPointWS)),
                    new HarpoonTextSegment("Keep reeling to climb. Stop reeling to bleed force before the next ledge move."),
                    "INFO"));
                ArmFeedbackCooldown();
            }

            return true;
        }

        private bool IsTetherValid()
        {
            return _tetheredBody != null &&
                   _tetheredCollider != null &&
                   _tetheredBody.gameObject.activeInHierarchy &&
                   !_tetheredBody.isKinematic &&
                   _tetheredBody.mass <= maxReelMass;
        }

        private bool IsGrappleValid()
        {
            return _grappleAnchorCollider != null &&
                   _grappleAnchorTransform != null &&
                   _grappleAnchorCollider.gameObject.activeInHierarchy;
        }

        private bool TryGetGrappleAnchorPoint(out Vector3 anchorPointWS)
        {
            if (!IsGrappleValid())
            {
                anchorPointWS = Vector3.zero;
                return false;
            }

            anchorPointWS = _grappleAnchorTransform.TransformPoint(_grappleAnchorLocalPoint);
            return true;
        }

        private void ClearTether()
        {
            _tetheredBody = null;
            _tetheredCollider = null;
            _tetheredName = null;
            _tetheredNameUpper = null;
            _grappleAnchorCollider = null;
            _grappleAnchorTransform = null;
            _grappleAnchorLocalPoint = Vector3.zero;
            _grappleAnchorName = null;
            _grappleAnchorNameUpper = null;
            if (_playerMovement != null)
                _playerMovement.ClearExosuitGrappleAnchor();
            InvalidateAssessmentCache();
            _tetherRemaining = 0f;
        }

        private bool TryQueueTargetHit(out InteractionSurfaceHit hit, out Vector3 origin, out Vector3 forward)
        {
            if (!TryResolveToolPose(out origin, out forward))
            {
                hit = default;
                return false;
            }

            return RequestPrimarySurfaceHit(origin, forward, GetRuntimeMaxRange(range), ResolveTargetSurfaceMask(), QueryTriggerInteraction.Ignore, out hit);
        }

        private int ResolveTargetSurfaceMask()
        {
            return HectonLayerMasks.ResolveSurfaceInteractionLayerMask(targetMask.value);
        }

        private bool TryResolveToolPose(out Vector3 origin, out Vector3 forward)
        {
            origin = default;
            forward = Vector3.forward;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 rawForward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(rawForward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(rawForward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f)
            {
                return false;
            }

            float inverseForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            forward = new Vector3(
                rawForward.x * inverseForwardLength,
                rawForward.y * inverseForwardLength,
                rawForward.z * inverseForwardLength);
            return true;
        }

        private void ApplyLaunchRecoil(Vector3 direction, float runtimeDamage)
        {
            Vector3 fallback = TryResolveToolPose(out _, out Vector3 toolForward) ? toolForward : Vector3.forward;
            Vector3 safeDirection = ResolveSafeDirection(direction, fallback);
            float mass = PlayerRecoilEquivalentMassKg;
            float runtimeRecoil = GetRuntimeRecoilImpulse(impulse);
            float impulseMagnitude = Mathf.Min(12f, (runtimeRecoil * Mathf.Max(0.1f, runtimeDamage / Mathf.Max(damage, 0.1f))) / mass);
            if (impulseMagnitude <= 0.0001f)
                return;

            TryQueuePlayerToolRecoil(safeDirection, impulseMagnitude);
            QueueToolHapticFeedback(runtimeDamage, Mathf.Max(damage, 0.1f));
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction))
                return IsFinite(fallback) ? fallback : Vector3.forward;

            float lengthSq = direction.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return IsFinite(fallback) ? fallback : Vector3.forward;

            float invLength = math.rsqrt(math.max(lengthSq, 0.0001f));
            return direction * invLength;
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private bool TryReadCachedAssessmentSnapshot(out HarpoonAssessment assessment)
        {
            assessment = _cachedAssessment;
            return _cachedAssessmentValid && _cachedAssessmentStamp == _assessmentEvaluationStamp;
        }

        private void StoreAssessment(HarpoonAssessment assessment)
        {
            AdvanceAssessmentStamp();
            _cachedAssessment = assessment;
            _cachedAssessmentStamp = _assessmentEvaluationStamp;
            _cachedAssessmentValid = true;
        }

        private void InvalidateAssessmentCache()
        {
            AdvanceAssessmentStamp();
            _cachedAssessmentStamp = 0u;
            _cachedAssessmentValid = false;
            _cachedAssessment = default;
        }

        private void AdvanceAssessmentStamp()
        {
            unchecked
            {
                _assessmentEvaluationStamp++;
                if (_assessmentEvaluationStamp == 0u)
                    _assessmentEvaluationStamp = 1u;
            }
        }

        private HarpoonAssessment BuildAssessment(Collider target, float distance, bool tetherReady)
        {
            if (target == null)
            {
                return new HarpoonAssessment(
                    StableText(LocalizationKeys.HARPOON_HEADLINE_NO_TARGET_DATA, "HARPOON - NO TARGET DATA"),
                    StableText(LocalizationKeys.HARPOON_SUMMARY_NO_TARGET_DATA, "Contact data collapsed before assessment completed."),
                    StableText(LocalizationKeys.HARPOON_RECOMMEND_NO_TARGET_DATA, "Sweep a new lane and reacquire."),
                    "WARN");
            }

            if (!ToolHitUtility.TryGetRigidbody(target, out Rigidbody body))
            {
                if (TryBuildDescriptorAssessment(target, null, distance, tetherReady, out HarpoonAssessment noBodyDescriptorAssessment))
                    return noBodyDescriptorAssessment;

                if (tetherReady && IsGrappleValid())
                {
                    return new HarpoonAssessment(
                        new HarpoonTextSegment("HARPOON - EXOSUIT GRAPPLE LOCK"),
                        HarpoonTextSegment.FormatStringFloat("{0} accepted a static grapple lane at {1:0.0} m.", ResolveAnchorLabel(), distance),
                        new HarpoonTextSegment("Secondary reels the exosuit toward the anchor. Release input to stop climbing."),
                        "INFO");
                }

                return new HarpoonAssessment(
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_HEADLINE_CANNOT_REEL, "HARPOON - TARGET CANNOT BE REELED")),
                    HarpoonTextSegment.FormatString(
                        StableText(LocalizationKeys.HARPOON_SUMMARY_CANNOT_REEL, "{0} has no valid mass body for tether control."),
                        ResolveTargetLabel()),
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_RECOMMEND_CANNOT_REEL, "Use cutter, builder, or move on.")),
                    "WARN");
            }

            if (body == null || body.isKinematic)
            {
                if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment staticDescriptorAssessment))
                    return staticDescriptorAssessment;

                if (tetherReady && IsGrappleValid())
                {
                    return new HarpoonAssessment(
                        new HarpoonTextSegment("HARPOON - EXOSUIT GRAPPLE LOCK"),
                        HarpoonTextSegment.FormatStringFloat("{0} is fixed hard enough to hold a climb line at {1:0.0} m.", ResolveAnchorLabel(), distance),
                        new HarpoonTextSegment("Secondary reels the exosuit toward the anchor. Release input to stop climbing."),
                        "INFO");
                }

                return new HarpoonAssessment(
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_HEADLINE_LOCKED_STRUCTURE, "HARPOON - TARGET LOCKED TO STRUCTURE")),
                    HarpoonTextSegment.FormatString(
                        StableText(LocalizationKeys.HARPOON_SUMMARY_LOCKED_STRUCTURE, "{0} is fixed in place and will not reel."),
                        ResolveTargetLabel()),
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_RECOMMEND_LOCKED_STRUCTURE, "Do not waste reel force on anchored structures.")),
                    "WARN");
            }

            if (body.mass > maxReelMass)
            {
                if (_heavyTowWinch != null && _heavyTowWinch.CanTowMass(body.mass))
                {
                    return new HarpoonAssessment(
                        new HarpoonTextSegment(tetherReady
                            ? "HARPOON - HEAVY TOW LOCKED"
                            : "HARPOON - HEAVY TOW CANDIDATE"),
                        HarpoonTextSegment.FormatStringFloatFloat("{0} weighs {1:0.0} kg at {2:0.0} m.", ResolveCargoLabel(), body.mass, distance),
                        new HarpoonTextSegment(tetherReady
                            ? "Maintain thrust discipline. Excess speed delta will snap the cable."
                            : "Primary fire can lock a heavy tow line. Expect major drag and current drift."),
                        "WARN");
                }

                if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment descriptorAssessment))
                    return descriptorAssessment;

                return new HarpoonAssessment(
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_HEADLINE_MASS_EXCEEDS, "HARPOON - MASS EXCEEDS REEL LIMIT")),
                    HarpoonTextSegment.FormatStringFloatFloat(
                        StableText(LocalizationKeys.HARPOON_SUMMARY_MASS_EXCEEDS, "{0} weighs {1:0.0} kg at {2:0.0} m."),
                        ResolveCargoLabel(),
                        body.mass,
                        distance),
                    new HarpoonTextSegment(StableText(LocalizationKeys.HARPOON_RECOMMEND_MASS_EXCEEDS, "Use propulsion or another route; reel force is not enough.")),
                    "WARN");
            }

            if (TryBuildDescriptorAssessment(target, body, distance, tetherReady, out HarpoonAssessment authoredAssessment))
                return authoredAssessment;

            return new HarpoonAssessment(
                new HarpoonTextSegment(tetherReady
                    ? StableText(LocalizationKeys.HARPOON_HEADLINE_CARGO_TETHERED, "HARPOON - CARGO TETHERED")
                    : StableText(LocalizationKeys.HARPOON_HEADLINE_CARGO_CONTACT, "HARPOON - CARGO CONTACT")),
                HarpoonTextSegment.FormatStringFloat(
                    StableText(LocalizationKeys.HARPOON_SUMMARY_CARGO_CONTACT, "{0} is reel-safe at {1:0.0} kg."),
                    ResolveCargoLabel(),
                    body.mass),
                new HarpoonTextSegment(tetherReady
                    ? StableText(LocalizationKeys.HARPOON_RECOMMEND_CARGO_TETHERED, "Pull it into position or keep it off your path.")
                    : StableText(LocalizationKeys.HARPOON_RECOMMEND_CARGO_CONTACT, "Fire again only if you need a tether lock.")),
                "INFO");
        }

        private bool TryBuildDescriptorAssessment(Collider target, Rigidbody body, float distance, bool tetherReady, out HarpoonAssessment assessment)
        {
            assessment = default;
            if (target == null || !FieldTargetDescriptor.TryResolve(target, out FieldTargetDescriptor descriptor))
                return false;

            float mass = body != null && math.isfinite(body.mass)
                ? math.max(0f, body.mass)
                : 0f;

            if (FieldTargetSemantics.TryBuildHarpoonAssessment(descriptor, distance, mass, tetherReady, out FieldTargetSemantics.SemanticAssessment semantic))
            {
                assessment = new HarpoonAssessment(
                    semantic.Headline,
                    semantic.Summary,
                    semantic.Recommendation,
                    semantic.Severity);
                return true;
            }

            return false;
        }

        private void RecordAssessmentLog(HarpoonAssessment titleAssessment, HarpoonAssessment detailAssessment)
        {
            _logTitleBuffer.Clear();
            _logSummaryBuffer.Clear();

            if (!titleAssessment.TryWriteHeadline(ref _logTitleBuffer) ||
                !TryWriteAssessmentLogSummary(ref _logSummaryBuffer, detailAssessment))
            {
                return;
            }

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                in _logTitleBuffer,
                in _logSummaryBuffer,
                detailAssessment.Severity);
        }

        private void RecordOperationLog(string title, HarpoonTextSegment summary, string severity)
        {
            _logSummaryBuffer.Clear();
            if (!summary.TryWrite(ref _logSummaryBuffer))
                return;

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.HARPOON_CATEGORY, HarpoonCategory),
                title,
                in _logSummaryBuffer,
                severity);
        }

        private bool IsFeedbackReady()
        {
            return _feedbackCooldownRemaining <= 0f;
        }

        private void ArmFeedbackCooldown()
        {
            float baseInterval = math.isfinite(feedbackInterval) ? math.max(0.01f, feedbackInterval) : 0.35f;
            float quality = ResolveGlobalQualityWeight();
            float curve = Smooth01(quality);
            _feedbackCooldownRemaining = baseInterval * math.lerp(1.65f, 0.85f, curve);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private void PublishAssessment(HarpoonAssessment assessment)
        {
            StoreAssessment(assessment);
            _hudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref _hudBuffer))
                return;

            if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in _hudBuffer);
            else
                ToolHitUtility.ShowInfo(in _hudBuffer);
        }

        private void PublishWarningMessage(string message)
        {
            _hudBuffer.Clear();
            if (AppendText(ref _hudBuffer, message))
                ToolHitUtility.ShowWarning(in _hudBuffer);
        }

        private void PublishInfoMessage(string message)
        {
            _hudBuffer.Clear();
            if (AppendText(ref _hudBuffer, message))
                ToolHitUtility.ShowInfo(in _hudBuffer);
        }

        private string ResolveTargetLabel()
        {
            return StableText(LocalizationKeys.HARPOON_TARGET, "TARGET");
        }

        private string ResolveAnchorLabel()
        {
            return "ANCHOR";
        }

        private string ResolveBioformLabel()
        {
            return "BIOFORM";
        }

        private string ResolveCargoLabel()
        {
            return "CARGO";
        }

        private string StableText(string key, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool AppendFormattedText(
            ref FixedCharBuffer buffer,
            string template,
            string arg0,
            float arg1,
            float arg2,
            byte argumentMask)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> templateSpan = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length)
                    continue;

                char tokenIndex = templateSpan[i + 1];
                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wroteArgument = false;
                if (tokenIndex == '0' && (argumentMask & HarpoonTextSegment.HasStringArg0) != 0)
                {
                    if (!AppendText(ref buffer, arg0))
                        return false;

                    wroteArgument = true;
                }
                else if (tokenIndex == '1' && (argumentMask & HarpoonTextSegment.HasFloatArg1) != 0)
                {
                    if (!buffer.AppendFloat(arg1, 1))
                        return false;

                    wroteArgument = true;
                }
                else if (tokenIndex == '2' && (argumentMask & HarpoonTextSegment.HasFloatArg2) != 0)
                {
                    if (!buffer.AppendFloat(arg2, 1))
                        return false;

                    wroteArgument = true;
                }

                if (!wroteArgument && !buffer.Append(templateSpan.Slice(i, tokenEnd - i + 1)))
                    return false;

                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private bool TryWriteAssessmentLogSummary(ref FixedCharBuffer buffer, HarpoonAssessment assessment)
        {
            string template = StableText(LocalizationKeys.HARPOON_LOG_ASSESSMENT, "{0} | {1}");
            if (string.IsNullOrEmpty(template))
                return assessment.TryWriteSummary(ref buffer);

            ReadOnlySpan<char> templateSpan = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 2 >= templateSpan.Length)
                    continue;

                char tokenIndex = templateSpan[i + 1];
                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wroteToken = tokenIndex == '0'
                    ? assessment.TryWriteSummary(ref buffer)
                    : tokenIndex == '1' && assessment.TryWriteRecommendation(ref buffer);

                if (!wroteToken && !buffer.Append(templateSpan.Slice(i, tokenEnd - i + 1)))
                    return false;

                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendSingleArgumentTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 2 >= templateSpan.Length || templateSpan[i + 1] != '0' || templateSpan[i + 2] != '}')
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendText(ref buffer, value))
                    return false;

                wroteTemplateToken = true;
                i += 2;
                segmentStart = i + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendFloatTemplate(
            ref FixedCharBuffer buffer,
            string template,
            float value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return buffer.AppendFloat(value, 1);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length || templateSpan[i + 1] != '0')
                    continue;

                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!buffer.AppendFloat(value, 1))
                    return false;

                wroteTemplateToken = true;
                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static float ApproximateDistanceMeters(Vector3 from, Vector3 to)
        {
            float3 delta = (float3)(from - to);
            float3 absDelta = math.abs(delta);
            float max = math.cmax(absDelta);
            float min = math.cmin(absDelta);
            float mid = absDelta.x + absDelta.y + absDelta.z - max - min;
            return max + mid * 0.375f + min * 0.125f;
        }

    }
}

