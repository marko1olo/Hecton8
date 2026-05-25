#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only source contract checks for the LifePod tactile prologue hardening pass.
    /// </summary>
    public static class LifePodTactilePrologueSmokeTester
    {
        private const string ArtifactRelativePath = "CodexArtifacts/lifepod-tactile-prologue-smoke.json";
        private const string DamageSystemPath = "Assets/_Project/Scripts/Gameplay/LifePodDamageSystem.cs";
        private const string PrologueControllerPath = "Assets/_Project/Scripts/Gameplay/LifePodTactilePrologueController.cs";
        private const string ExtinguisherNozzlePath = "Assets/_Project/Scripts/Gameplay/LifePodFireExtinguisherNozzle.cs";
        private const string StrapCoordinatorPath = "Assets/_Project/Scripts/Interaction/LifePodSeatStrapCoordinator.cs";
        private const string StrapLatchPath = "Assets/_Project/Scripts/Interaction/LifePodSeatStrapLatch.cs";
        private const string ValveHandlePath = "Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs";
        private const string BatteryCompartmentPath = "Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs";

        [MenuItem("Hecton8/Dev/Run LifePod Tactile Prologue Smoke")]
        private static void RunMenuSmokeTest()
        {
            RunSmokeAndWriteArtifact();
        }

        public static void RunBatchModeSmokeTest()
        {
            bool pass = RunSmokeAndWriteArtifact();
            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool RunSmokeAndWriteArtifact()
        {
            string damageSystem = ReadProjectFile(DamageSystemPath);
            string prologueController = ReadProjectFile(PrologueControllerPath);
            string extinguisherNozzle = ReadProjectFile(ExtinguisherNozzlePath);
            string strapCoordinator = ReadProjectFile(StrapCoordinatorPath);
            string strapLatch = ReadProjectFile(StrapLatchPath);
            string valveHandle = ReadProjectFile(ValveHandlePath);
            string batteryCompartment = ReadProjectFile(BatteryCompartmentPath);

            bool sparkBitScanPass =
                ContainsAll(
                    damageSystem,
                    "MaxVisibleSparkInstances = 4",
                    "while (activeMask != 0u && activeCount < MaxVisibleSparkInstances)",
                    "math.tzcnt(activeMask)",
                    "int selectedCount = 0;",
                    "while (selectedCount < toggleCount && attempts < MaxShortCircuitBits * 4)",
                    "selectedCount++;",
                    "Graphics.DrawMeshInstanced") &&
                ContainsAll(
                    damageSystem,
                    "float phase01 = math.frac(_sparkPhase + bitIndex * 0.618f);",
                    "float triangle01 = 1f - math.abs((phase01 * 2f) - 1f);") &&
                SourceIndex(damageSystem, "math.countbits((uint)toggleMask)") == int.MaxValue &&
                SourceIndex(damageSystem, "math." + "sin") == int.MaxValue;

            bool sparkAnchorCachePass =
                ContainsAll(
                    damageSystem,
                    "_sparkAnchorPositions",
                    "_sparkAnchorRotations",
                    "CacheSparkAnchorPoses(_shortCircuitMask)",
                    "TryResolveCachedSparkPose",
                    "_sparkTimerSeconds = math.max(_sparkTimerSeconds, _resolvedSparkLifetimeSeconds);") &&
                SourceIndex(damageSystem, "anchor.position,") == int.MaxValue &&
                SourceIndex(damageSystem, "anchor.rotation,") == int.MaxValue;

            bool sparkAnchorSanitizePass =
                ContainsAll(
                    damageSystem,
                    "private int _resolvedRenderLayer;",
                    "_resolvedRenderLayer = ClampRenderLayer(renderLayer != 0 ? renderLayer : gameObject.layer);",
                    "_resolvedRenderLayer,",
                    "if (!IsFinite(position) || !IsFinite(rotation))",
                    "private static bool IsFinite(Quaternion value)") &&
                SourceIndex(damageSystem, "Graphics.DrawMeshInstanced") < SourceIndex(damageSystem, "_resolvedRenderLayer,");

            bool sparkScalarCachePass =
                ContainsAll(
                    damageSystem,
                    "private float _resolvedSparkLifetimeSeconds;",
                    "private void CacheScalarConfig()",
                    "_sparkPhase = math.frac(_sparkPhase + safeDeltaTime * _resolvedSparkFlickerRateHz);",
                    "_resolvedShortCircuitLowFrequency",
                    "private static float SaturateFinite01(float value)") &&
                SourceIndex(damageSystem, "safeDeltaTime = math." + "max(0f, deltaTime)") == int.MaxValue;

            bool damageExternalSeveritySanitizePass =
                ContainsAll(
                    damageSystem,
                    "private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;",
                    "float clampedSeverity = SaturateFinite01(severity01);") &&
                SourceIndex(damageSystem, "float clampedSeverity = math." + "saturate(severity01);") == int.MaxValue;

            bool damagePublicEventScalarCachePass =
                ContainsOrdered(
                    damageSystem,
                    "public void TriggerWaterImpact()",
                    "CacheScalarConfig();",
                    "TriggerWaterImpactCachedConfig(DefaultImpactSeed, _resolvedDefaultImpactSeverity01);") &&
                ContainsOrdered(
                    damageSystem,
                    "public void TriggerWaterImpact(uint impactSeed, float severity01)",
                    "CacheScalarConfig();",
                    "TriggerWaterImpactCachedConfig(impactSeed, severity01);") &&
                ContainsOrdered(
                    damageSystem,
                    "private void TriggerWaterImpactCachedConfig(uint impactSeed, float severity01)",
                    "_rngState = impactSeed != 0u ? impactSeed : DefaultImpactSeed;") &&
                ContainsOrdered(
                    damageSystem,
                    "public void SetShortCircuitBit(int bitIndex, bool active)",
                    "CacheScalarConfig();",
                    "ushort bit = (ushort)(1 << bitIndex);");

            bool biosZeroGcPass =
                ContainsAll(
                    prologueController,
                    "System.Span<char> buffer = _biosBuffer.AsSpan();",
                    "TryFormat(buffer.Slice(cursor)",
                    "biosCrtText.SetCharArray") &&
                SourceIndex(prologueController, ".text" + " =") == int.MaxValue &&
                SourceIndex(prologueController, ".To" + "String(") == int.MaxValue;

            bool biosLootCachePass =
                ContainsAll(
                    prologueController,
                    "BiosLootCacheFrameMask = 0x7u",
                    "(_biosLootCacheFrameCounter++ & BiosLootCacheFrameMask) != 0u",
                    "RefreshBiosLootCache();",
                    "_cachedLootSphereAup",
                    "private HectonPlayerMovement _cachedObserverMovement;",
                    "if (_cachedObserverMovement == null)",
                    "_cachedObserverMovement.PredictedAup",
                    "private void WriteLootLine") &&
                CountOccurrences(prologueController, "HectonScanRenderRegistry.TryFindNearestLootSphereAup") == 1 &&
                SourceIndex(prologueController, "playerTransform" + ".position") == int.MaxValue;

            bool biosLootInvalidationPass =
                ContainsAll(
                    prologueController,
                    "private void InvalidateBiosLootCache()",
                    "_biosLootCacheFrameCounter = 0u;",
                    "_cachedHasLootSphereAup = false;",
                    "_cachedLootSphereAup = default;",
                    "_cachedObserverMovement = null;") &&
                ContainsOrdered(
                    prologueController,
                    "public void InvalidateColdReferenceCache()",
                    "_seatStrapLatches.Clear();",
                    "InvalidateBiosLootCache();") &&
                ContainsOrdered(
                    prologueController,
                    "public void BeginCrashSequence(uint seed, float severity01)",
                    "_biosRefreshTimer = 0f;",
                    "InvalidateBiosLootCache();",
                    "if (_smoke01 > _resolvedVentedSmokeThreshold01)");

            bool crashResetPass =
                ContainsAll(
                    prologueController,
                    "ResetPhysicalStartState();",
                    "seatStrapCoordinator.ResetLatchState();",
                    "latch.ResetLatchVisualState();",
                    "_foam01 = 0f;",
                    "_visorVibration01 = 0f;");

            bool manualVentingPass =
                ContainsAll(
                    prologueController,
                    "RefreshValveTelemetryCache();",
                    "_cachedValveOpen01",
                    "_cachedValveAngular01",
                    "_cachedValveOpen01 * _resolvedSmokePurgeRatePerSecond",
                    "_cachedValveAngular01 * _resolvedSmokeManualTurnPurgeRatePerSecond",
                    "ventValve.AngularVelocityDegreesPerSecond",
                    "_resolvedSmokeManualTurnPurgeRatePerSecond",
                    "_resolvedFullManualVentAngularVelocityDegreesPerSecond") &&
                SourceIndex(prologueController, "float valveOpen01 = ventValve != null ? math.saturate(ventValve.IsOpen01) : 0f;") == int.MaxValue &&
                SourceIndex(prologueController, "float valveAngular01 = ResolveValveAngular01();") == int.MaxValue;

            bool controllerScalarCachePass =
                ContainsAll(
                    prologueController,
                    "private float _resolvedInitialSmoke01;",
                    "private void CacheScalarConfig()",
                    "_smoke01 = _resolvedInitialSmoke01;",
                    "_resolvedBiosRefreshSeconds",
                    "SaturateFinite01(ventValve.IsOpen01)",
                    "float safeValue = math.isfinite(value) ? value : 0f;") &&
                SourceIndex(prologueController, "float dt = math." + "max(0f, deltaTime);") == int.MaxValue &&
                SourceIndex(prologueController, "math." + "saturate(foamDelta01)") == int.MaxValue;

            bool crashNoArgSeverityCachePass =
                ContainsOrdered(
                    prologueController,
                    "public void BeginCrashSequence()",
                    "CacheScalarConfig();",
                    "BeginCrashSequence(impactSeed, _resolvedImpactSeverity01);") &&
                SourceIndex(prologueController, "BeginCrashSequence(impactSeed, impactSeverity01);") == int.MaxValue;

            bool coldReferenceScanGatePass =
                ContainsOrdered(
                    prologueController,
                    "private void ResolveColdReferences()",
                    "EnsureSeatStrapLatchCache();",
                    "if ((_coldReferenceSearchMask & ColdReferenceSearchAll) == ColdReferenceSearchAll)",
                    "return;",
                    "(_coldReferenceSearchMask & ColdReferenceSeatStrapCoordinator) == 0u",
                    "GetComponentInChildren<LifePodSeatStrapCoordinator>(true)",
                    "_coldReferenceSearchMask |= ColdReferenceSeatStrapCoordinator;") &&
                ContainsAll(
                    prologueController,
                    "private uint _coldReferenceSearchMask;",
                    "private const uint ColdReferenceSearchAll",
                    "public void InvalidateColdReferenceCache()",
                    "_coldReferenceSearchMask = 0u;",
                    "_seatStrapLatches.Clear();");

            bool controllerLifecycleInvalidationPass =
                ContainsOrdered(
                    prologueController,
                    "private void OnDisable()",
                    "_cachedObserverMovement = null;",
                    "InvalidateColdReferenceCache();",
                    "InvalidatePublishedShaderCache();") &&
                ContainsOrdered(
                    prologueController,
                    "public void BeginCrashSequence(uint seed, float severity01)",
                    "_cachedObserverMovement = null;",
                    "if (_smoke01 > _resolvedVentedSmokeThreshold01)");

            bool scrubberSocketCachePass =
                ContainsAll(
                    batteryCompartment,
                    "private IBatteryTool _cachedBatteryTool;",
                    "RefreshBatteryToolCache();",
                    "tool = _cachedBatteryTool;",
                    "_cachedBatteryTool = batteryToolOwner as IBatteryTool;",
                    "public bool HasInstalledCell => TryResolveTool(out IBatteryTool tool) && tool.HasBattery;");

            bool updatableRegistrationPass =
                ContainsAll(
                    prologueController,
                    "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment)") &&
                ContainsAll(
                    extinguisherNozzle,
                    "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player)") &&
                ContainsAll(
                    strapLatch,
                    "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player)") &&
                ContainsAll(
                    batteryCompartment,
                    "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player)") &&
                SourceIndex(strapLatch, "GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);") == int.MaxValue &&
                SourceIndex(strapLatch, "GlobalRegistry.Updatables.Contains(this)") == int.MaxValue &&
                SourceIndex(batteryCompartment, "GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);") == int.MaxValue &&
                SourceIndex(batteryCompartment, "GlobalRegistry.Updatables.Contains(this)") == int.MaxValue;

            bool podGravityCachePass =
                ContainsAll(
                    prologueController,
                    "_resolvedFakePodGravityVector",
                    "CacheFakePodGravityVector();",
                    "float3 gravity = _resolvedFakePodGravityVector;",
                    "math.abs(lengthSq - 1f) <= 0.0001f") &&
                SourceIndex(prologueController, "float3 gravity = ResolveFakePodGravityVector()") == int.MaxValue;

            bool shaderPublishCachePass =
                ContainsAll(
                    prologueController,
                    "InvalidatePublishedShaderCache();",
                    "_lastSmokeParams = Vector4.positiveInfinity;",
                    "_lastPodGravityVector = Vector4.positiveInfinity;",
                    "_lastVisorVibration = Vector4.positiveInfinity;",
                    "_lastFoamParams = Vector4.positiveInfinity;");

            bool extinguisherFakePass =
                ContainsAll(
                    extinguisherNozzle,
                    "ApplyExtinguisherFoamCachedFlow(foamDelta",
                    "ResolveFoamFlowDirection",
                    "_playerReferenceTransform = ResolvePlayerReferenceTransform();",
                    "Transform reference = _playerReferenceTransform;",
                    "ToolHapticsRuntime.EnqueueSinusoidalCommand") &&
                SourceIndex(extinguisherNozzle, "Particle" + "System") == int.MaxValue &&
                SourceIndex(extinguisherNozzle, "Physics." + "Raycast") == int.MaxValue;

            bool extinguisherColdReferenceCachePass =
                ContainsOrdered(
                    extinguisherNozzle,
                    "private void ResolveColdReferences()",
                    "if ((_coldReferenceSearchMask & ColdReferenceSearchAll) == ColdReferenceSearchAll)",
                    "return;",
                    "(_coldReferenceSearchMask & ColdReferenceTargetController) == 0u",
                    "GetComponentInParent<LifePodTactilePrologueController>()",
                    "_coldReferenceSearchMask |= ColdReferenceTargetController;") &&
                ContainsAll(
                    extinguisherNozzle,
                    "private uint _coldReferenceSearchMask;",
                    "public void InvalidateColdReferenceCache()",
                    "private const uint ColdReferenceSearchAll");

            bool extinguisherDisableInvalidationPass =
                ContainsOrdered(
                    extinguisherNozzle,
                    "private void OnDisable()",
                    "_playerReferenceTransform = null;",
                    "ResetFoamFlowCache();",
                    "InvalidateColdReferenceCache();",
                    "TryUnregisterTick();");

            bool extinguisherFlowCachePass =
                ContainsAll(
                    extinguisherNozzle,
                    "FoamFlowRefreshFrameMask = 0x3u",
                    "_cachedFoamFlowDirection",
                    "private Transform _resolvedNozzleForwardReference;",
                    "RefreshCachedFoamFlowDirectionFrame();",
                    "(_foamFlowRefreshFrameCounter++ & FoamFlowRefreshFrameMask) != 0u",
                    "Transform nozzle = _resolvedNozzleForwardReference;",
                    "private void EnsureSelfTransform()",
                    "targetController.ApplyExtinguisherFoamCachedFlow(foamDelta, _cachedFoamFlowDirection)") &&
                ContainsAll(
                    prologueController,
                    "public void ApplyExtinguisherFoamCachedFlow(float foamDelta01, float2 normalizedScreenFlowDirection)",
                    "CacheFoamFlowDirectionFast(normalizedScreenFlowDirection);",
                    "FoamFlowTrustedMinimumLengthSq",
                    "FoamFlowTrustedMaximumLengthSq",
                    "private void ApplyFoamDelta(float delta)") &&
                SourceIndex(extinguisherNozzle, "Transform nozzle = nozzleForwardReference != null ? nozzleForwardReference : transform;") == int.MaxValue &&
                SourceIndex(extinguisherNozzle, "float2 flowDirection = ResolveFoamFlowDirection();") == int.MaxValue &&
                SourceIndex(extinguisherNozzle, "new Vector2(flowDirection.x, flowDirection.y)") == int.MaxValue;

            bool extinguisherScalarCachePass =
                ContainsAll(
                    extinguisherNozzle,
                    "private float _resolvedFoamPerSecond;",
                    "private void CacheScalarConfig()",
                    "float foamDelta = _resolvedFoamPerSecond * dt;",
                    "_resolvedHapticPulseIntervalSeconds",
                    "private static float SaturateFinite01(float value)") &&
                SourceIndex(extinguisherNozzle, "float dt = math." + "max(0f, deltaTime);") == int.MaxValue &&
                SourceIndex(extinguisherNozzle, "foamPerSecond " + "* dt") == int.MaxValue;

            bool tickRetentionPass =
                ContainsAll(
                    prologueController,
                    "if (!NeedsActiveTick())",
                    "private bool NeedsActiveTick()",
                    "(_stateBits & (StateCrashActive | StateSmokeActive | StateFoamActive))",
                    "return (_stateBits & StatePowerRestored) != 0u && biosCrtText != null;") &&
                SourceIndex(prologueController, "StateCrashActive | StateSmokeActive | StateFoamActive | StatePowerRestored") == int.MaxValue;

            bool batteryDoorAxisCachePass =
                ContainsAll(
                    batteryCompartment,
                    "private Vector3 _resolvedDoorLocalAxis = Vector3.right;",
                    "CacheDoorAxis();",
                    "ApproximateAngleAxisDegreesNoTrig(_resolvedDoorTravelDegrees * _doorOpen01, _resolvedDoorLocalAxis)",
                    "private void CacheDoorAxis()") &&
                SourceIndex(batteryCompartment, "Vector3 axis = ResolveDoorAxis();") == int.MaxValue &&
                SourceIndex(batteryCompartment, "private Vector3 ResolveDoorAxis()") == int.MaxValue &&
                SourceIndex(batteryCompartment, "NormalizeVectorRsqrt") == int.MaxValue;

            bool batteryScalarCachePass =
                ContainsAll(
                    batteryCompartment,
                    "private float _resolvedDoorTravelDegrees = 72f;",
                    "private float _resolvedDoorOpenThreshold01 = 0.85f;",
                    "private float _resolvedBatterySnapDurationSeconds = 0.2f;",
                    "public bool DoorOpenEnoughForSwap => _doorOpen01 >= _resolvedDoorOpenThreshold01;",
                    "private void CacheScalarConfig()") &&
                SourceIndex(batteryCompartment, "float snapDuration = SanitizePositiveSeconds(batterySnapDurationSeconds") == int.MaxValue &&
                SourceIndex(batteryCompartment, "ResolveSafeDoorTravelDegrees() * _doorOpen01") == int.MaxValue;

            bool batteryVisualCachePass =
                ContainsAll(
                    batteryCompartment,
                    "private bool _batteryVisualStateCached;",
                    "private bool _batteryVisualActive;",
                    "if (_batteryVisualStateCached && _batteryVisualActive == hasBattery)",
                    "_batteryVisualStateCached = false;") &&
                ContainsOrdered(
                    batteryCompartment,
                    "private void OnEnable()",
                    "CacheDoorAxis();",
                    "_batteryVisualStateCached = false;",
                    "ApplyDoorVisual();") &&
                ContainsOrdered(
                    batteryCompartment,
                    "private void OnDisable()",
                    "AbortBatterySnap();",
                    "_batteryVisualStateCached = false;",
                    "TryUnregisterTick();") &&
                CountOccurrences(batteryCompartment, "gameObject.SetActive(hasBattery)") == 1;

            bool seatAupPass =
                ContainsAll(
                    strapCoordinator,
                    "AbsoluteUniversePosition.ToCameraRelativeFloat3",
                    "_playerMovement.CurrentAup",
                    "currentAup.ToRuntimeFloat3()",
                    "float distanceSq = delta.sqrMagnitude;",
                    "ApproximateMagnitudeNoSqrt(delta)",
                    "math.rcp(math.max(ApproximateMagnitudeNoSqrt(delta), 0.000001f))") &&
                SourceIndex(strapCoordinator, "Vector3." + "Distance") == int.MaxValue &&
                SourceIndex(strapCoordinator, "math.rsqrt(distanceSq)") == int.MaxValue &&
                SourceIndex(strapCoordinator, "_playerTransform") == int.MaxValue &&
                SourceIndex(strapCoordinator, "FromRuntimePosition(currentPosition)") == int.MaxValue;

            bool seatLockPoseCachePass =
                ContainsAll(
                    strapCoordinator,
                    "private bool _seatLockPoseCached;",
                    "private bool TryEnsurePlayerMotor()",
                    "_playerMotor != null && _playerMotor.Body != null && _playerMovement != null",
                    "private bool TryResolveSeatLockRuntimePosition(out Vector3 targetPosition)",
                    "float3 runtime = _seatLockAup.ToRuntimeFloat3();",
                    "targetPosition = new Vector3(runtime.x, runtime.y, runtime.z);",
                    "private bool TryCacheSeatLockPose()",
                    "_seatLockAup = AbsoluteUniversePosition.FromRuntimePosition(targetPosition);") &&
                ContainsOrdered(
                    strapCoordinator,
                    "public void FixedTick(float fixedDeltaTime)",
                    "if (!TryResolveSeatLockRuntimePosition(out Vector3 targetPosition))",
                    "return;") &&
                SourceIndex(strapCoordinator, "TryResolveSeatPosition") == int.MaxValue &&
                SourceIndex(strapCoordinator, "targetPosition = seatAnchor.position;") == int.MaxValue &&
                SourceIndex(strapCoordinator, "if (!TryCacheSeatLockPose() ||") == int.MaxValue &&
                SourceIndex(strapCoordinator, "if (!TryCacheSeatLockPose())") >
                SourceIndex(strapCoordinator, "private void EngageSeatLock()") &&
                SourceIndex(strapCoordinator, "_seatLockRuntimePosition") == int.MaxValue;

            bool seatPlayerCacheInvalidationPass =
                ContainsAll(
                    strapCoordinator,
                    "private void InvalidatePlayerCache()",
                    "_playerMotor = null;",
                    "_playerMovement = null;") &&
                ContainsOrdered(
                    strapCoordinator,
                    "private void OnDisable()",
                    "ReleaseSeatLock();",
                    "InvalidatePlayerCache();",
                    "TryUnregisterFixedTick();") &&
                ContainsOrdered(
                    strapCoordinator,
                    "public void ResetLatchState()",
                    "_rightIkAnchor = null;",
                    "InvalidatePlayerCache();");

            bool seatCoordinatorScalarCachePass =
                ContainsAll(
                    strapCoordinator,
                    "private float _resolvedMaximumCorrectionMetersPerSecond;",
                    "private float _resolvedHardSnapDistanceSq;",
                    "private void CacheScalarConfig()",
                    "float maxStep = _resolvedMaximumCorrectionMetersPerSecond * safeFixedDeltaTime;",
                    "distanceSq <= _resolvedHardSnapDistanceSq",
                    "private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;") &&
                SourceIndex(strapCoordinator, "float safeHardSnapDistance = ResolveSafeHardSnapDistanceMeters();") == int.MaxValue &&
                SourceIndex(strapCoordinator, "ResolveSafeMaximumCorrectionMetersPerSecond() * safeFixedDeltaTime") == int.MaxValue;

            bool strapLatchVisualCachePass =
                ContainsAll(
                    strapLatch,
                    "private Quaternion _latchedLocalRotation;",
                    "CacheLatchedVisualRotation();",
                    "ResolveEulerRotationNoTrig(SanitizeEulerDegrees(latchedLocalEulerDegrees))",
                    "private void OnValidate()") &&
                SourceIndex(strapLatch, "Quaternion." + "Euler") == int.MaxValue &&
                SourceIndex(strapLatch, "float safeDeltaTime = math." + "max(0f, deltaTime);") == int.MaxValue;

            bool strapLatchScalarCachePass =
                ContainsAll(
                    strapLatch,
                    "private float _resolvedRequiredHoldSeconds = MinimumHoldSeconds;",
                    "private float _resolvedHoldDecaySecondsPerSecond;",
                    "private void CacheScalarConfig()",
                    "return math.saturate(_holdProgressSeconds / _resolvedRequiredHoldSeconds);",
                    "_holdProgressSeconds - _resolvedHoldDecaySecondsPerSecond * safeDeltaTime") &&
                SourceIndex(strapLatch, "float safeRequiredHoldSeconds = ResolveSafeRequiredHoldSeconds();") == int.MaxValue &&
                SourceIndex(strapLatch, "float safeDecay = ResolveSafeHoldDecaySecondsPerSecond();") == int.MaxValue;

            bool strapReceiverLifecyclePass =
                ContainsAll(
                    strapLatch,
                    "private Collider _registeredCollider;",
                    "_highlighter.SetHighlight(false);",
                    "PhysicalHandReceiverRegistry.Unregister(_registeredCollider, this);",
                    "_registeredCollider = activationCollider;",
                    "_registeredCollider = null;") &&
                SourceIndex(strapLatch, "PhysicalHandReceiverRegistry.Unregister(activationCollider, this)") == int.MaxValue;

            bool strapResetTickReleasePass =
                ContainsOrdered(
                    strapLatch,
                    "public void ResetLatchVisualState()",
                    "_contactThisTick = false;",
                    "_highlighter.SetHighlight(false);",
                    "TryUnregisterTick();");

            bool valveGrabPoseCachePass =
                ContainsAll(
                    valveHandle,
                    "private Vector3 _cachedPivotWorldPosition;",
                    "private Vector3 _cachedWorldAxis = Vector3.forward;",
                    "public bool SampleControllerPose(Vector3 controllerWorldPosition, float sampleDeltaSeconds)",
                    "private void CacheGrabPose()",
                    "controllerWorldPosition - _cachedPivotWorldPosition",
                    "_cachedTransform.TransformDirection(_resolvedLocalAxis)") &&
                SourceIndex(valveHandle, "private Vector3 Resolve" + "WorldAxis()") == int.MaxValue &&
                SourceIndex(valveHandle, "controllerWorldPosition - _cachedTransform.position") == int.MaxValue;

            bool valveScalarCachePass =
                ContainsAll(
                    valveHandle,
                    "private float _resolvedDegreesToOpen = 360f;",
                    "private float _resolvedMaxAcceptedSampleDeltaDegrees = 80f;",
                    "private float _resolvedAngularDragPerSecond = 7.5f;",
                    "private float _resolvedMinimumMomentumDegreesPerSecond = 2f;",
                    "private void CacheScalarConfig()") &&
                SourceIndex(valveHandle, "ResolveSafeDegreesToOpen()") == int.MaxValue &&
                SourceIndex(valveHandle, "ResolveSafeAngularDragPerSecond() * safeDeltaTime") == int.MaxValue;

            bool valveQuaternionGuardPass =
                ContainsAll(
                    valveHandle,
                    "private static bool IsFiniteQuaternion(Quaternion value)",
                    "math.lengthsq(q) > 0.000001f");

            bool valveInitialOpenFinitePass =
                ContainsAll(
                    valveHandle,
                    "float safeInitialOpen01 = math.isfinite(initialOpen01) ? math.saturate(initialOpen01) : 0f;",
                    "_accumulatedDegrees = safeInitialOpen01 * _resolvedDegreesToOpen;") &&
                SourceIndex(valveHandle, "_accumulatedDegrees = math.saturate(initialOpen01) * _resolvedDegreesToOpen;") == int.MaxValue;

            bool hapticFrequencyCapPass =
                ContainsAll(
                    damageSystem,
                    "MaxToolHapticFrequencyHz = 60f",
                    "SanitizeAtLeast(shortCircuitHapticFrequencyHz, 1f)",
                    "MaxToolHapticFrequencyHz") &&
                ContainsAll(
                    extinguisherNozzle,
                    "MaxToolHapticFrequencyHz = 60f",
                    "SanitizeAtLeast(hapticFrequencyHz, 1f)",
                    "MaxToolHapticFrequencyHz") &&
                ContainsAll(
                    strapCoordinator,
                    "MaximumHapticFrequencyHz = 60f",
                    "return math.isfinite(value) ? math.clamp(value, 1f, MaximumHapticFrequencyHz) : MaximumHapticFrequencyHz;") &&
                ContainsAll(
                    prologueController,
                    "MaxToolHapticFrequencyHz = 60f",
                    "ColdStartHapticFrequencyHz = MaxToolHapticFrequencyHz",
                    "ImpactHapticFrequencyHz = MaxToolHapticFrequencyHz",
                    "ColdStartHapticFrequencyHz",
                    "ImpactHapticFrequencyHz") &&
                SourceIndex(prologueController, "104f") == int.MaxValue &&
                SourceIndex(prologueController, "78f") == int.MaxValue;

            bool lifecycleScalarRefreshPass =
                ContainsOrdered(
                    damageSystem,
                    "private void OnEnable()",
                    "CacheScalarConfig();",
                    "if (_shortCircuitMask != 0)") &&
                ContainsOrdered(
                    prologueController,
                    "private void OnEnable()",
                    "CacheScalarConfig();",
                    "if (autoBeginOnEnable)") &&
                ContainsOrdered(
                    extinguisherNozzle,
                    "private void OnEnable()",
                    "CacheScalarConfig();",
                    "ResolveColdReferences();") &&
                ContainsOrdered(
                    strapCoordinator,
                    "private void OnEnable()",
                    "CacheScalarConfig();",
                    "if (_seatLockActive)") &&
                ContainsOrdered(
                    valveHandle,
                    "private void OnEnable()",
                    "EnsureReferences();",
                    "RefreshCachedLocalAxis();",
                    "CacheScalarConfig();") &&
                ContainsOrdered(
                    batteryCompartment,
                    "private void OnEnable()",
                    "RefreshBatteryToolCache();",
                    "CacheScalarConfig();",
                    "CacheDoorAxis();",
                    "ApplyDoorVisual();",
                    "ApplyBatteryVisual();");

            bool pass = sparkBitScanPass &&
                        sparkAnchorCachePass &&
                        sparkAnchorSanitizePass &&
                        sparkScalarCachePass &&
                        damageExternalSeveritySanitizePass &&
                        damagePublicEventScalarCachePass &&
                        biosZeroGcPass &&
                        biosLootCachePass &&
                        biosLootInvalidationPass &&
                        crashResetPass &&
                        manualVentingPass &&
                        controllerScalarCachePass &&
                        crashNoArgSeverityCachePass &&
                        coldReferenceScanGatePass &&
                        controllerLifecycleInvalidationPass &&
                        scrubberSocketCachePass &&
                        updatableRegistrationPass &&
                        podGravityCachePass &&
                        shaderPublishCachePass &&
                        extinguisherFakePass &&
                        extinguisherColdReferenceCachePass &&
                        extinguisherDisableInvalidationPass &&
                        extinguisherFlowCachePass &&
                        extinguisherScalarCachePass &&
                        tickRetentionPass &&
                        batteryDoorAxisCachePass &&
                        batteryScalarCachePass &&
                        batteryVisualCachePass &&
                        seatAupPass &&
                        seatLockPoseCachePass &&
                        seatPlayerCacheInvalidationPass &&
                        seatCoordinatorScalarCachePass &&
                        strapLatchVisualCachePass &&
                        strapLatchScalarCachePass &&
                        strapReceiverLifecyclePass &&
                        strapResetTickReleasePass &&
                        valveGrabPoseCachePass &&
                        valveScalarCachePass &&
                        valveQuaternionGuardPass &&
                        valveInitialOpenFinitePass &&
                        hapticFrequencyCapPass &&
                        lifecycleScalarRefreshPass;

            WriteArtifact(
                pass,
                sparkBitScanPass,
                sparkAnchorCachePass,
                sparkAnchorSanitizePass,
                sparkScalarCachePass,
                damageExternalSeveritySanitizePass,
                damagePublicEventScalarCachePass,
                biosZeroGcPass,
                biosLootCachePass,
                biosLootInvalidationPass,
                crashResetPass,
                manualVentingPass,
                controllerScalarCachePass,
                crashNoArgSeverityCachePass,
                coldReferenceScanGatePass,
                controllerLifecycleInvalidationPass,
                scrubberSocketCachePass,
                updatableRegistrationPass,
                podGravityCachePass,
                shaderPublishCachePass,
                extinguisherFakePass,
                extinguisherColdReferenceCachePass,
                extinguisherDisableInvalidationPass,
                extinguisherFlowCachePass,
                extinguisherScalarCachePass,
                tickRetentionPass,
                batteryDoorAxisCachePass,
                batteryScalarCachePass,
                batteryVisualCachePass,
                seatAupPass,
                seatLockPoseCachePass,
                seatPlayerCacheInvalidationPass,
                seatCoordinatorScalarCachePass,
                strapLatchVisualCachePass,
                strapLatchScalarCachePass,
                strapReceiverLifecyclePass,
                strapResetTickReleasePass,
                valveGrabPoseCachePass,
                valveScalarCachePass,
                valveQuaternionGuardPass,
                valveInitialOpenFinitePass,
                hapticFrequencyCapPass,
                lifecycleScalarRefreshPass);

            if (pass)
                Debug.Log("[LifePodTactilePrologueSmokeTester] PASS artifact=" + ArtifactRelativePath);
            else
                Debug.LogError("[LifePodTactilePrologueSmokeTester] FAIL artifact=" + ArtifactRelativePath);

            return pass;
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string path = Path.Combine(System.Environment.CurrentDirectory, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool ContainsAll(string source, params string[] needles)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            for (int i = 0; i < needles.Length; i++)
            {
                if (SourceIndex(source, needles[i]) == int.MaxValue)
                    return false;
            }

            return true;
        }

        private static bool ContainsOrdered(string source, params string[] needles)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            int offset = 0;
            for (int i = 0; i < needles.Length; i++)
            {
                if (string.IsNullOrEmpty(needles[i]))
                    return false;

                int index = source.IndexOf(needles[i], offset, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                offset = index + needles[i].Length;
            }

            return true;
        }

        private static int SourceIndex(string source, string needle)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle))
                return int.MaxValue;

            int index = source.IndexOf(needle, StringComparison.Ordinal);
            return index >= 0 ? index : int.MaxValue;
        }

        private static int CountOccurrences(string source, string needle)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0;
            int offset = 0;
            while (offset < source.Length)
            {
                int index = source.IndexOf(needle, offset, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                offset = index + needle.Length;
            }

            return count;
        }

        private static void WriteArtifact(
            bool pass,
            bool sparkBitScanPass,
            bool sparkAnchorCachePass,
            bool sparkAnchorSanitizePass,
            bool sparkScalarCachePass,
            bool damageExternalSeveritySanitizePass,
            bool damagePublicEventScalarCachePass,
            bool biosZeroGcPass,
            bool biosLootCachePass,
            bool biosLootInvalidationPass,
            bool crashResetPass,
            bool manualVentingPass,
            bool controllerScalarCachePass,
            bool crashNoArgSeverityCachePass,
            bool coldReferenceScanGatePass,
            bool controllerLifecycleInvalidationPass,
            bool scrubberSocketCachePass,
            bool updatableRegistrationPass,
            bool podGravityCachePass,
            bool shaderPublishCachePass,
            bool extinguisherFakePass,
            bool extinguisherColdReferenceCachePass,
            bool extinguisherDisableInvalidationPass,
            bool extinguisherFlowCachePass,
            bool extinguisherScalarCachePass,
            bool tickRetentionPass,
            bool batteryDoorAxisCachePass,
            bool batteryScalarCachePass,
            bool batteryVisualCachePass,
            bool seatAupPass,
            bool seatLockPoseCachePass,
            bool seatPlayerCacheInvalidationPass,
            bool seatCoordinatorScalarCachePass,
            bool strapLatchVisualCachePass,
            bool strapLatchScalarCachePass,
            bool strapReceiverLifecyclePass,
            bool strapResetTickReleasePass,
            bool valveGrabPoseCachePass,
            bool valveScalarCachePass,
            bool valveQuaternionGuardPass,
            bool valveInitialOpenFinitePass,
            bool hapticFrequencyCapPass,
            bool lifecycleScalarRefreshPass)
        {
            string path = Path.Combine(System.Environment.CurrentDirectory, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] — editor-only LifePod smoke artifact writer — owner: LifePodTactilePrologueSmokeTester
            builder.Append("{\n");
            AppendJsonBool(builder, "pass", pass, true);
            AppendJsonBool(builder, "sparkBitScanPass", sparkBitScanPass, true);
            AppendJsonBool(builder, "sparkAnchorCachePass", sparkAnchorCachePass, true);
            AppendJsonBool(builder, "sparkAnchorSanitizePass", sparkAnchorSanitizePass, true);
            AppendJsonBool(builder, "sparkScalarCachePass", sparkScalarCachePass, true);
            AppendJsonBool(builder, "damageExternalSeveritySanitizePass", damageExternalSeveritySanitizePass, true);
            AppendJsonBool(builder, "damagePublicEventScalarCachePass", damagePublicEventScalarCachePass, true);
            AppendJsonBool(builder, "biosZeroGcPass", biosZeroGcPass, true);
            AppendJsonBool(builder, "biosLootCachePass", biosLootCachePass, true);
            AppendJsonBool(builder, "biosLootInvalidationPass", biosLootInvalidationPass, true);
            AppendJsonBool(builder, "crashResetPass", crashResetPass, true);
            AppendJsonBool(builder, "manualVentingPass", manualVentingPass, true);
            AppendJsonBool(builder, "controllerScalarCachePass", controllerScalarCachePass, true);
            AppendJsonBool(builder, "crashNoArgSeverityCachePass", crashNoArgSeverityCachePass, true);
            AppendJsonBool(builder, "coldReferenceScanGatePass", coldReferenceScanGatePass, true);
            AppendJsonBool(builder, "controllerLifecycleInvalidationPass", controllerLifecycleInvalidationPass, true);
            AppendJsonBool(builder, "scrubberSocketCachePass", scrubberSocketCachePass, true);
            AppendJsonBool(builder, "updatableRegistrationPass", updatableRegistrationPass, true);
            AppendJsonBool(builder, "podGravityCachePass", podGravityCachePass, true);
            AppendJsonBool(builder, "shaderPublishCachePass", shaderPublishCachePass, true);
            AppendJsonBool(builder, "extinguisherFakePass", extinguisherFakePass, true);
            AppendJsonBool(builder, "extinguisherColdReferenceCachePass", extinguisherColdReferenceCachePass, true);
            AppendJsonBool(builder, "extinguisherDisableInvalidationPass", extinguisherDisableInvalidationPass, true);
            AppendJsonBool(builder, "extinguisherFlowCachePass", extinguisherFlowCachePass, true);
            AppendJsonBool(builder, "extinguisherScalarCachePass", extinguisherScalarCachePass, true);
            AppendJsonBool(builder, "tickRetentionPass", tickRetentionPass, true);
            AppendJsonBool(builder, "batteryDoorAxisCachePass", batteryDoorAxisCachePass, true);
            AppendJsonBool(builder, "batteryScalarCachePass", batteryScalarCachePass, true);
            AppendJsonBool(builder, "batteryVisualCachePass", batteryVisualCachePass, true);
            AppendJsonBool(builder, "seatAupPass", seatAupPass, true);
            AppendJsonBool(builder, "seatLockPoseCachePass", seatLockPoseCachePass, true);
            AppendJsonBool(builder, "seatPlayerCacheInvalidationPass", seatPlayerCacheInvalidationPass, true);
            AppendJsonBool(builder, "seatCoordinatorScalarCachePass", seatCoordinatorScalarCachePass, true);
            AppendJsonBool(builder, "strapLatchVisualCachePass", strapLatchVisualCachePass, true);
            AppendJsonBool(builder, "strapLatchScalarCachePass", strapLatchScalarCachePass, true);
            AppendJsonBool(builder, "strapReceiverLifecyclePass", strapReceiverLifecyclePass, true);
            AppendJsonBool(builder, "strapResetTickReleasePass", strapResetTickReleasePass, true);
            AppendJsonBool(builder, "valveGrabPoseCachePass", valveGrabPoseCachePass, true);
            AppendJsonBool(builder, "valveScalarCachePass", valveScalarCachePass, true);
            AppendJsonBool(builder, "valveQuaternionGuardPass", valveQuaternionGuardPass, true);
            AppendJsonBool(builder, "valveInitialOpenFinitePass", valveInitialOpenFinitePass, true);
            AppendJsonBool(builder, "hapticFrequencyCapPass", hapticFrequencyCapPass, true);
            AppendJsonBool(builder, "lifecycleScalarRefreshPass", lifecycleScalarRefreshPass, false);
            builder.Append("}\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendJsonBool(StringBuilder builder, string key, bool value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(key);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }
    }
}
#endif
