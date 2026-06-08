using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class DepthWaterlineRoutingEditTests
    {
        [Test]
        public void PlayerBuilderDepthPressureUsesProductionSeaLevelInsteadOfZeroPlane()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "PlayerBuilder.cs");
            string estimateDepth = ExtractMethodBody(source, "private static float EstimateDepthPressure(double3 pivotAup)");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("DefaultSeaLevelAupY - pivotAup.y", estimateDepth);
            StringAssert.DoesNotContain("math.max(0f, -(float)pivotAup.y)", source);
        }

        [Test]
        public void MetabolismDetailTelemetryUsesProductionSeaLevelForDepth()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Physiology", "ShinobuMetabolismJobs.cs");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("public static float ResolveSeaLevelDepthMeters(double absoluteY)", source);
            StringAssert.Contains("entry.PlayerDepthMeters = ShinobuMetabolismJobMath.ResolveSeaLevelDepthMeters(playerAup.y);", source);
            StringAssert.DoesNotContain("entry.PlayerDepthMeters = math.max(0f, -(float)playerAup.y);", source);
        }

        [Test]
        public void VisorAestheticProfilesPreferMovementDepthAndFallbackToProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVisorUberPostFeature.cs");
            string selectProfile = ExtractMethodBody(source, "private bool TrySelectAestheticProfileSnapshot(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveAestheticProfileDepthMeters(Camera renderCamera)");
            string fallbackDepth = ExtractMethodBody(source, "private static float ResolveCameraDepthFromProductionSeaLevel(Camera renderCamera)");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("float depthMeters = ResolveAestheticProfileDepthMeters(renderCamera);", selectProfile);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("DefaultSeaLevelY - position.y", fallbackDepth);
            StringAssert.DoesNotContain("float depthMeters = math.max(0f, -renderCamera.transform.position.y);", source);
        }

        [Test]
        public void NoirDepthFogNearSurfaceFallbackUsesProductionSeaLevelOnlyWithoutPlayerRuntimeContext()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonNoirDepthFogFeature.cs");
            string resolveWeight = ExtractMethodBody(source, "private float ResolveSurfaceFogWeight01(Camera renderCamera, float nearSurfaceBypassDepthMeters, bool attenuateNearSurface)");
            string fallbackDepth = ExtractMethodBody(source, "private static float ResolveCameraDepthFromProductionSeaLevel(Camera renderCamera)");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("playerContext.IsInitialized", resolveWeight);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveWeight);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveWeight);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveWeight);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", resolveWeight);
            StringAssert.Contains("return Smooth01(math.max(0f, movementState.DepthMeters) / safeDepth);", resolveWeight);
            StringAssert.Contains("if (playerContext != null)", resolveWeight);
            StringAssert.Contains("return 0f;", resolveWeight);
            StringAssert.Contains("float fallbackDepth = ResolveCameraDepthFromProductionSeaLevel(renderCamera);", resolveWeight);
            StringAssert.Contains("DefaultSeaLevelY - position.y", fallbackDepth);
            Assert.That(
                resolveWeight.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveWeight.IndexOf("float fallbackDepth = ResolveCameraDepthFromProductionSeaLevel(renderCamera);", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float fallbackDepth = math.max(0f, -renderCamera.transform.position.y);", source);
            StringAssert.DoesNotContain("return Smooth01(playerMovement.CurrentDepth / safeDepth);", source);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", resolveWeight);
            StringAssert.DoesNotContain("playerMovement.CurrentDepth", resolveWeight);
        }

        [Test]
        public void ScooterVolumetricShaftsNoirBlendRejectsStaleMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonScooterVolumetricShaftsFeature.cs");
            string noirBlend = ExtractMethodBody(source, "private float ResolveUnderwaterNoirBlend()");

            StringAssert.Contains("playerContext.IsInitialized", noirBlend);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", noirBlend);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", noirBlend);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", noirBlend);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", noirBlend);
            StringAssert.Contains("float depth = math.max(0f, movementState.DepthMeters);", noirBlend);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", noirBlend);
            StringAssert.DoesNotContain("playerMovement.CurrentDepth", noirBlend);
            StringAssert.DoesNotContain("playerMovement.CurrentDepth - SurfaceNoirSuppressionDepth", source);
        }

        [Test]
        public void ScooterVolumetricShaftsVelocityUsesRuntimeSnapshotInsteadOfRawMovement()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonScooterVolumetricShaftsFeature.cs");
            string velocity = ExtractMethodBody(source, "private float3 ResolvePlayerVelocity()");

            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", velocity);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u", velocity);
            StringAssert.Contains("math.all(math.isfinite(movementState.Velocity))", velocity);
            StringAssert.Contains("return movementState.Velocity;", velocity);
            StringAssert.Contains("CoreDeterminismSignals.TryGetLatestKccVelocityFloat3", velocity);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", velocity);
            StringAssert.DoesNotContain("InterpolatedLinearVelocity", velocity);
            StringAssert.DoesNotContain("ToFloat3(", source);
        }

        [Test]
        public void VolumetricFogExtinctionProfileSelectionUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonVolumetricParticulateFogFeature.cs");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("float cameraDepthMeters = ResolveCameraDepthFromProductionSeaLevel(cameraPosition);", source);
            StringAssert.Contains("private static float ResolveCameraDepthFromProductionSeaLevel(float3 cameraPosition)", source);
            StringAssert.Contains("DefaultSeaLevelY - cameraPosition.y", source);
            StringAssert.DoesNotContain("float cameraDepthMeters = ResolveFiniteNonNegative(-cameraPosition.y, 0f);", source);
        }

        [Test]
        public void AbyssalThermalDamageDepthUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDamageDepthMeters(Vector3 positionWS)");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("DefaultSeaLevelY - positionWS.y", resolveDepth);
            StringAssert.DoesNotContain("math.max(0f, -positionWS.y)", source);
        }

        [Test]
        public void WorldProceduralVolumetricDepthDoesNotReintroduceZeroPlaneFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralFieldSampler.cs");

            StringAssert.Contains("float sampleDepthMeters = math.max(depthMeters, math.max(0f, waterSurface - position.y));", source);
            StringAssert.DoesNotContain("math.max(math.max(0f, waterSurface - position.y), math.max(0f, -position.y))", source);
        }

        [Test]
        public void PdaMapDepthFallbackUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("private const double DefaultSeaLevelY = 14.02d;", source);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("biomeMatrixDirector.isActiveAndEnabled", resolveDepth);
            StringAssert.Contains("math.isfinite(biomeMatrixDirector.CurrentDepthMeters)", resolveDepth);
            StringAssert.Contains("DefaultSeaLevelY - absoluteY", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("return (float)math.max(0d, -absoluteY);", source);
        }

        [Test]
        public void ReactorThermalBaseCompromisedSignalDepthUsesReactorAupSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Thermodynamics", "ReactorThermalGridJobs.cs");
            string helper = ExtractMethodBody(source, "public static float ResolveSeaLevelDepthMeters(double3 aup)");
            string enqueue = ExtractMethodBody(source, "private bool EnqueueBaseCompromised(double3 aup, uint reactorHash, float coreTemp, float meltdownTemp)");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("DefaultSeaLevelAupY - aup.y", helper);
            StringAssert.Contains("signal.DepthMeters = ReactorThermalMath.ResolveSeaLevelDepthMeters(aup);", enqueue);
            StringAssert.DoesNotContain("signal.DepthMeters = math.max(0f, -center.y);", source);
        }

        [Test]
        public void BiolumDepthDarknessUsesProductionSeaLevelInsteadOfAupZeroPlane()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "VFX", "Bioluminescence", "BiolumPulseSyncRuntime.cs");
            string darkness = ExtractMethodBody(source, "private static float ResolveAupDepthDarknessScalar(double3 aupReference)");
            string depth = ExtractMethodBody(source, "private static float ResolveAupDepthMeters(double3 aupReference)");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("float depthMeters = ResolveAupDepthMeters(aupReference);", darkness);
            StringAssert.Contains("DefaultSeaLevelAupY - aupReference.y", depth);
            StringAssert.DoesNotContain("float depthMeters = math.max(0f, -yMeters);", source);
        }

        [Test]
        public void DirectorEncounterDepthUsesMovementDepthOrProductionSeaLevelWhenSurvivalIsMissing()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonDirectorAI.cs");
            string snapshot = ExtractMethodBody(source, "private bool TryResolvePlayerRuntimeSnapshot(");
            string snapshotDepth = ExtractMethodBody(source, "private static float ResolveSnapshotDepthMeters(float snapshotDepthMeters, Vector3 playerPosition)");
            string surface = ExtractMethodBody(source, "private float ResolveSurfaceWorldY(Vector3 playerPosition, float playerDepthMeters)");
            string depth = ExtractMethodBody(source, "private float ResolvePlayerDepth(Vector3 playerPosition, float surfaceWorldY, float playerDepthMeters)");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("out float playerDepthMeters)", source);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", snapshot);
            StringAssert.Contains("!movementState.PredictedAup.IsFinite()", snapshot);
            StringAssert.Contains("if (!math.all(math.isfinite(runtimePosition)))", snapshot);
            StringAssert.Contains("playerDepthMeters = ResolveSnapshotDepthMeters(movementState.DepthMeters, playerPosition);", snapshot);
            StringAssert.Contains("DefaultSeaLevelY - playerPosition.y", snapshotDepth);
            StringAssert.Contains("playerPosition.y + math.max(0f, math.isfinite(playerDepthMeters) ? playerDepthMeters : 0f)", surface);
            StringAssert.Contains("return math.max(0f, playerDepthMeters);", depth);
            StringAssert.DoesNotContain("if (survivalSystem == null)", surface);
            StringAssert.DoesNotContain("return 0f;", surface);
        }

        [Test]
        public void AppliedLoreAcousticGhostDepthUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Data", "Monolith", "H8AppliedLoreRuntime.cs");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveDepth01(in AbsoluteUniversePosition aup)");

            StringAssert.Contains("private const double DefaultSeaLevelY = 14.02d;", source);
            StringAssert.Contains("DefaultSeaLevelY - absoluteY", resolveDepth);
            StringAssert.DoesNotContain("math.max(0.0, -absoluteY)", source);
        }

        [Test]
        public void StormPropagationAttenuationSamplesPlayerAupAgainstRuntimeOriginSeaPlane()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Atmosphere", "StormPropagation", "ShinobuStormPropagationRuntime.cs");
            string schedule = ExtractMethodBody(source, "private void SchedulePropagationJobs(float deltaTime, float quality)");
            string sample = ExtractMethodBody(source, "private double3 ResolvePropagationSampleAupDouble(double3 fallbackAup)");
            string seaLevel = ExtractMethodBody(source, "private double3 ResolveSeaLevelAupDouble(double3 runtimeOriginAup, in WeatherStateDTO weather, bool weatherAvailable)");

            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Player, GlobalRegistry.Player);", source);
            StringAssert.Contains("double3 sampleAup = ResolvePropagationSampleAupDouble(_lastOriginFallbackAup);", schedule);
            StringAssert.Contains("SampleAup = sampleAup,", schedule);
            StringAssert.Contains("nextPlayer != null && nextPlayer.IsInitialized ? nextPlayer : null", source);
            StringAssert.Contains("playerContext.IsInitialized", sample);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", sample);
            StringAssert.Contains("movementState.PredictedAup.ToAbsoluteDouble3()", sample);
            StringAssert.Contains("runtimeOriginAup.y + seaLevelLocal", seaLevel);
            StringAssert.DoesNotContain("SampleAup = _lastOriginFallbackAup,", source);
        }

        [Test]
        public void DepthZoneDirectorUsesMovementDepthWhenSurvivalOwnerIsMissing()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "DepthZoneDirector.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string resolveDepth = ExtractMethodBody(source, "private bool TryResolveCurrentDepthMeters(out float depth)");

            StringAssert.Contains("using Unity.Mathematics;", source);
            StringAssert.Contains("if (!TryResolveCurrentDepthMeters(out float depth))", slowTick);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("movementState.DepthMeters", resolveDepth);
            StringAssert.Contains("depth = math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return false;", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = survivalSystem", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("if (survivalSystem == null)", slowTick);
            StringAssert.DoesNotContain("float depth = survivalSystem.Depth;", slowTick);
        }

        [Test]
        public void DepthZoneNotificationPushRefusalRetriesBeforeClearingPendingPresentation()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "DepthZoneDirector.cs");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string tryPush = ExtractMethodBody(source, "private bool TryPushDepthZoneNotification(");
            string retry = ExtractMethodBody(source, "private static bool ShouldDropDepthZoneNotificationAfterMiss(");
            string clear = ExtractMethodBody(source, "private void ClearPendingPresentationEvents()");
            string hullWarning = ExtractMethodBody(source, "private void CheckHullWarning(");

            StringAssert.Contains("private const int DepthZoneNotificationRetryFrameLimit = 3;", source);
            StringAssert.Contains("public int DepthZoneNotificationMissCount => _depthZoneNotificationMissCount;", source);
            StringAssert.Contains("private int _pendingZoneNotificationRetryCount;", source);
            StringAssert.Contains("private int _pendingHullWarningNotificationRetryCount;", source);

            StringAssert.Contains("if (TryPushDepthZoneNotification(", lateFrame);
            StringAssert.Contains("ShouldDropDepthZoneNotificationAfterMiss(ref _pendingZoneNotificationRetryCount)", lateFrame);
            StringAssert.Contains("ShouldDropDepthZoneNotificationAfterMiss(ref _pendingHullWarningNotificationRetryCount)", lateFrame);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(GetZoneEnterMessageSpan(_pendingZoneNotification));", lateFrame);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(GetHullWarningMessageSpan(_pendingHullWarningNotification));", lateFrame);

            StringAssert.Contains("return true;", tryPush);
            StringAssert.Contains("ReportDepthZoneNotificationMiss(", tryPush);
            StringAssert.Contains("return false;", tryPush);
            AssertTextBefore(tryPush, "ReportDepthZoneNotificationMiss(", "return false;");

            StringAssert.Contains("retryCount++;", retry);
            StringAssert.Contains("return retryCount >= DepthZoneNotificationRetryFrameLimit;", retry);

            StringAssert.Contains("_pendingZoneNotificationRetryCount = 0;", clear);
            StringAssert.Contains("_pendingHullWarningNotificationRetryCount = 0;", clear);
            StringAssert.Contains("_pendingHullWarningNotificationRetryCount = 0;", hullWarning);
            AssertTextBefore(lateFrame, "ShouldDropDepthZoneNotificationAfterMiss(ref _pendingZoneNotificationRetryCount)", "_pendingZoneNotification = null;");
            AssertTextBefore(lateFrame, "ShouldDropDepthZoneNotificationAfterMiss(ref _pendingHullWarningNotificationRetryCount)", "_pendingHullWarningNotification = null;");
        }

        [Test]
        public void DepthZoneDirectorLifecycleClearsIndependentRuntimeRegistrations()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "DepthZoneDirector.cs");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string tryUnregister = ExtractMethodBody(source, "private void TryUnregister()");

            StringAssert.Contains("TryUnregisterHotSwapListener();", onDisable);
            StringAssert.Contains("TryUnregister();", onDisable);
            StringAssert.Contains("TryUnregisterService();", onDisable);
            StringAssert.Contains("LocalizationEvents.UnregisterLanguageListener(this);", onDisable);

            StringAssert.Contains("TryUnregisterHotSwapListener();", onDestroy);
            StringAssert.Contains("TryUnregister();", onDestroy);
            StringAssert.Contains("TryUnregisterService();", onDestroy);
            StringAssert.Contains("LocalizationEvents.UnregisterLanguageListener(this);", onDestroy);
            AssertTextBefore(onDestroy, "LocalizationEvents.UnregisterLanguageListener(this);", "ClearPendingPresentationEvents();");

            StringAssert.DoesNotContain("if (!_registered)\r\n                return;", tryUnregister);
            StringAssert.DoesNotContain("if (!_registered)\n                return;", tryUnregister);
            StringAssert.Contains("if (_registered)", tryUnregister);
            StringAssert.Contains("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);", tryUnregister);
            StringAssert.Contains("if (_registeredLateFrame)", tryUnregister);
            StringAssert.Contains("GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);", tryUnregister);
            StringAssert.Contains("_registered = false;", tryUnregister);
            StringAssert.Contains("_registeredLateFrame = false;", tryUnregister);
        }

        [Test]
        public void PlayerRuntimeContextMovementSnapshotPublishesFiniteMovementDepthContract()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "PlayerRuntimeContextService.cs");
            string publishMovement = ExtractMethodBody(source, "private void PublishMovementSnapshot()");
            string tryGetMovement = ExtractMethodBody(source, "public bool TryGetMovementRuntimeState(out PlayerMovementRuntimeState state)");

            StringAssert.Contains("float depthMeters = SanitizeNonNegative(_playerMovement != null ? _playerMovement.CurrentDepth : 0f);", publishMovement);
            StringAssert.Contains("movementState.DepthMeters = depthMeters;", publishMovement);
            StringAssert.Contains("uint flags = 0u;", publishMovement);
            StringAssert.Contains("if (math.all(math.isfinite(movementState.WorldPosition))", publishMovement);
            StringAssert.Contains("movementState.PredictedAup.IsFinite())", publishMovement);
            StringAssert.Contains("flags |= (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;", publishMovement);
            Assert.That(
                publishMovement.IndexOf("movementState.PredictedAup = predictedAup;", StringComparison.Ordinal),
                Is.LessThan(publishMovement.IndexOf("flags |= (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;", StringComparison.Ordinal)));
            StringAssert.Contains("math.isfinite(state.DepthMeters)", tryGetMovement);
            StringAssert.Contains("IsFinitePredictedAup(in state)", tryGetMovement);
            StringAssert.Contains("math.all(math.isfinite(state.WorldPosition))", tryGetMovement);
            StringAssert.Contains("math.all(math.isfinite(state.PredictedWorldPosition))", tryGetMovement);
            StringAssert.Contains("math.all(math.isfinite(state.Velocity))", tryGetMovement);
            StringAssert.Contains("math.all(math.isfinite(state.Forward))", tryGetMovement);
            StringAssert.DoesNotContain("_survivalSystem != null ? _survivalSystem.Depth", publishMovement);
            StringAssert.DoesNotContain("float depthMeters = _survivalSystem", publishMovement);
        }

        [Test]
        public void SurvivalDepthOwnerSanitizesMovementDepthBeforePressureAndUnderwaterState()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonSurvivalSystem.cs");
            string computeDepth = ExtractMethodBody(source, "private void ComputeDepthAndPressure()");
            string underwaterState = ExtractMethodBody(source, "private bool ResolveSurfaceContractUnderwater()");

            StringAssert.Contains("float movementSurfaceY = _playerMovement.CurrentWaterSurfaceY;", computeDepth);
            StringAssert.Contains("if (math.isfinite(movementSurfaceY))", computeDepth);
            StringAssert.Contains("surfaceWorldY = movementSurfaceY;", computeDepth);
            StringAssert.Contains("float movementDepth = _playerMovement.CurrentDepth;", computeDepth);
            StringAssert.Contains("depth = math.isfinite(movementDepth) ? math.max(0f, movementDepth) : 0f;", computeDepth);
            StringAssert.Contains("float movementDepth = _playerMovement.CurrentDepth;", underwaterState);
            StringAssert.Contains("return (math.isfinite(movementDepth) && movementDepth > 0.01f) ||", underwaterState);
            StringAssert.Contains("_playerMovement.IsPlayerSubmerged;", underwaterState);
            StringAssert.DoesNotContain("depth = math.max(0f, _playerMovement.CurrentDepth);", source);
            StringAssert.DoesNotContain("return _playerMovement.CurrentDepth > 0.01f || _playerMovement.IsPlayerSubmerged;", source);
        }

        [Test]
        public void WorldStreamingDirectorDepthProfilesUsePlayerRuntimeSnapshotBeforeDirectMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "WorldStreamingDirector.cs");
            string resolveReferences = ExtractMethodBody(source, "private void ResolveReferences()");
            string rebindPlayer = ExtractMethodBody(source, "private void RebindPlayerRuntimeContext(IPlayerRuntimeContext runtimeContext)");
            string resolveDepth = ExtractMethodBody(source, "private bool TryResolveCurrentDepth(out float depth)");

            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", resolveReferences);
            StringAssert.Contains("RebindPlayerRuntimeContext(runtimeContext);", resolveReferences);
            StringAssert.Contains("RebindPlayerRuntimeContext(null);", resolveReferences);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", resolveReferences);
            StringAssert.DoesNotContain("_playerRuntimeContext = GlobalRegistry.Player;", resolveReferences);
            StringAssert.Contains("_playerRuntimeContext = runtimeContext;", rebindPlayer);
            StringAssert.Contains("_playerRuntimeContext = null;", rebindPlayer);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("depth = math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return false;", resolveDepth);
            StringAssert.Contains("_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("depth = math.max(0f, _playerMovement.CurrentDepth);", resolveDepth);
            StringAssert.Contains("depth = Mathf.Max(0f, ResolveWaterSurfaceLevel() - playerTransform.position.y);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("_playerMovement != null", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("_playerMovement != null", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("_playerMovement != null", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("ResolveWaterSurfaceLevel()", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("depth = Mathf.Max(0f, _playerMovement.CurrentDepth);", source);
        }

        [Test]
        public void HectonOsBootDepthLineUsesPlayerRuntimeSnapshotBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "HectonOSBootManager.cs");
            string buildText = ExtractMethodBody(source, "private int BuildSequenceText(char[] destination, BootReason reason, string slotName)");
            string resolveOwners = ExtractMethodBody(source, "private bool ResolveOwners()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveLiveDepthMeters()");

            StringAssert.Contains("float liveDepth = ResolveLiveDepthMeters();", buildText);
            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player);", resolveOwners);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", serviceReplaced);
            StringAssert.Contains("CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);", serviceReplaced);
            StringAssert.Contains("_playerRuntimeContext = playerContext;", cachePlayer);
            StringAssert.Contains("_survivalSystem = survivalSystem;", cachePlayer);
            StringAssert.Contains("_playerMovement = playerMovement;", cachePlayer);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("math.isfinite(survival.Depth)", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("math.max(0f, movement.CurrentDepth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float liveDepth = _survivalSystem != null ? _survivalSystem.Depth : 0f;", source);
        }

        [Test]
        public void ShinobuOceanCameraAupRejectsRawMovementAndOriginFallbackWhenPlayerContextExists()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Atmosphere", "ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string resolveCameraAup = ExtractMethodBody(source, "private double3 ResolveCameraAupDouble()");

            AssertTextBefore(
                resolveCameraAup,
                "player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveCameraAup);
            StringAssert.Contains("snapshot.Aup.IsFinite()", resolveCameraAup);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveCameraAup);
            StringAssert.Contains("movementState.PredictedAup.IsFinite()", resolveCameraAup);
            StringAssert.Contains("return movementState.PredictedAup.ToAbsoluteDouble3();", resolveCameraAup);
            AssertTextBefore(resolveCameraAup, "return double3.zero;", "RuntimeOriginRoute.CurrentRuntimeOriginAup()");
            StringAssert.DoesNotContain("player.PlayerMovement", resolveCameraAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", resolveCameraAup);
        }

        [Test]
        public void SuitHudDepthSignalDisplaysPlayerRuntimeSnapshotBeforeSurvivalFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SuitHUDV4CanvasOverlay.cs");
            string refreshSubscription = ExtractMethodBody(source, "private void RefreshDepthSignalSubscription()");
            string consumeSignals = ExtractMethodBody(source, "private void ConsumeDepthChangedSignals()");
            string movementFallback = ExtractMethodBody(source, "private void RefreshDepthFromMovementFallback()");
            string handleDepth = ExtractMethodBody(source, "private void HandleDepthChanged(float depth)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveHudDepthMeters(float fallbackDepth)");

            StringAssert.Contains("HandleDepthChanged(_depthSignalSource.Depth);", refreshSubscription);
            StringAssert.Contains("HandleDepthChanged(_depthSignalSource.Depth);", consumeSignals);
            StringAssert.Contains("_depthMeters = ResolveHudDepthMeters(_depthMeters);", movementFallback);
            StringAssert.Contains("_depthMeters = ResolveHudDepthMeters(depth);", handleDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("HectonPlayerMovement movement = playerMovement;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movement.CurrentDepth);", resolveDepth);
            StringAssert.Contains("return math.isfinite(fallbackDepth) ? math.max(0f, fallbackDepth) : 0f;", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("fallbackDepth", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("_depthMeters = math.max(0f, playerMovement.CurrentDepth);", source);
            StringAssert.DoesNotContain("_depthMeters = math.max(0f, depth);", source);
        }

        [Test]
        public void SuitAdvisoryDepthWarningsUsePlayerRuntimeSnapshotBeforeSurvivalFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SuitAdvisoryController.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveReferences = ExtractMethodBody(source, "private void ResolveReferences()");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)");
            string processSignal = ExtractMethodBody(source, "private void ProcessSurvivalVitalsSignal(in SurvivalVitalsChangedSignal signal)");
            string evaluateAll = ExtractMethodBody(source, "private void EvaluateAll()");
            string handleDepth = ExtractMethodBody(source, "private void HandleDepthChanged(float depth)");
            string resolveMargin = ExtractMethodBody(source, "private float ResolveSafeDepthMarginMeters(float fallbackDepthMeters)");
            string resolveSafeDepth = ExtractMethodBody(source, "private float ResolveEffectiveSafeDepthMeters()");
            string resolveAdvisoryDepth = ExtractMethodBody(source, "private float ResolveAdvisoryDepthMeters(float fallbackDepthMeters)");

            StringAssert.Contains("using Unity.Mathematics;", source);
            StringAssert.Contains("private IPlayerRuntimeContext _cachedPlayerContext;", source);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player);", awake);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player);", onEnable);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", hotSwap);
            StringAssert.Contains("CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);", hotSwap);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", resolveReferences);
            StringAssert.Contains("survival = playerContext.SurvivalSystem;", resolveReferences);
            StringAssert.Contains("_cachedPlayerContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;", cachePlayer);
            StringAssert.Contains("HandleDepthChanged(float.NaN);", processSignal);
            StringAssert.Contains("HandleDepthChanged(float.NaN);", evaluateAll);
            StringAssert.Contains("float remaining = ResolveSafeDepthMarginMeters(depth);", handleDepth);
            StringAssert.Contains("float safeDepthMeters = ResolveEffectiveSafeDepthMeters();", resolveMargin);
            StringAssert.Contains("float depthMeters = ResolveAdvisoryDepthMeters(fallbackDepthMeters);", resolveMargin);
            StringAssert.Contains("float survivalDepth = survival.Depth;", resolveSafeDepth);
            StringAssert.Contains("float margin = survival.SafeDepthMarginMeters;", resolveSafeDepth);
            StringAssert.Contains("return math.max(0f, math.max(0f, survivalDepth) + margin);", resolveSafeDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveAdvisoryDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveAdvisoryDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveAdvisoryDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveAdvisoryDepth);
            StringAssert.Contains("math.isfinite(fallbackDepthMeters)", resolveAdvisoryDepth);
            StringAssert.Contains("return survival != null && math.isfinite(survival.Depth)", resolveAdvisoryDepth);
            Assert.That(
                resolveAdvisoryDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveAdvisoryDepth.IndexOf("math.isfinite(fallbackDepthMeters)", StringComparison.Ordinal)));
            Assert.That(
                resolveAdvisoryDepth.IndexOf("math.isfinite(fallbackDepthMeters)", StringComparison.Ordinal),
                Is.LessThan(resolveAdvisoryDepth.IndexOf("return survival != null && math.isfinite(survival.Depth)", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("HandleDepthChanged(survival != null ? survival.Depth : 0f);", source);
            StringAssert.DoesNotContain("HandleDepthChanged(survival.Depth);", source);
            StringAssert.DoesNotContain("float remaining = survival.SafeDepthMarginMeters;", source);
        }

        [Test]
        public void SuitAdvisoryDeathStateSynchronizesAcrossLoadReenableAndRespawn()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SuitAdvisoryController.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string processSignal = ExtractMethodBody(source, "private void ProcessSurvivalVitalsSignal(in SurvivalVitalsChangedSignal signal)");
            string evaluateAll = ExtractMethodBody(source, "private void EvaluateAll()");
            string synchronizeDeath = ExtractMethodBody(source, "private void SynchronizeDeathState()");
            string handleDeath = ExtractMethodBody(source, "private void HandleDeath()");

            StringAssert.Contains("EvaluateAll();", onEnable);
            StringAssert.Contains("SynchronizeDeathState();", evaluateAll);
            StringAssert.Contains("SynchronizeDeathState();", processSignal);
            StringAssert.Contains("if (survival.IsAlive)", synchronizeDeath);
            StringAssert.Contains("_deathTriggered = false;", synchronizeDeath);
            StringAssert.Contains("HandleDeath();", synchronizeDeath);
            StringAssert.Contains("if (_deathTriggered)", handleDeath);
            StringAssert.DoesNotContain("SurvivalVitalsChangedSignalFlags.Death", processSignal);
            AssertTextBefore(evaluateAll, "HandleInjuryStateChanged();", "SynchronizeDeathState();");
            AssertTextBefore(synchronizeDeath, "if (survival.IsAlive)", "HandleDeath();");
            AssertTextBefore(synchronizeDeath, "_deathTriggered = false;", "return;");
        }

        [Test]
        public void VisorPressureDepthUsesPlayerRuntimeSnapshotBeforeSurvivalFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "VisorHUDController.cs");
            string pressureCrack = ExtractMethodBody(source, "private void UpdatePressureLensCrackState(float deltaTime)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("float depth = ResolvePlayerDepthMeters();", pressureCrack);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.DoesNotContain("playerMovement != null && math.isfinite(playerMovement.CurrentDepth)", resolveDepth);
            StringAssert.DoesNotContain("return math.max(0f, playerMovement.CurrentDepth);", resolveDepth);
            StringAssert.Contains("survivalSystem != null && math.isfinite(survivalSystem.Depth)", resolveDepth);
            StringAssert.Contains("? math.max(0f, survivalSystem.Depth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survivalSystem = _subscribedSurvivalSystem", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survivalSystem = _subscribedSurvivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("if (_subscribedSurvivalSystem != null)", resolveDepth);
            StringAssert.DoesNotContain("return playerMovement != null ? Mathf.Max(0f, playerMovement.CurrentDepth) : 0f;", source);
        }

        [Test]
        public void AtmosphereUnderwaterStateUsesPlayerRuntimeDepthSnapshotBeforeDirectMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonAtmosphereManager.cs");
            string movementState = ExtractMethodBody(source, "private bool ResolveMovementUnderwaterState()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepth()");
            string runtimeState = ExtractMethodBody(source, "private bool TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            string hasContext = ExtractMethodBody(source, "private bool HasPlayerRuntimeContext()");

            StringAssert.Contains("float depth = ResolvePlayerDepth();", movementState);
            StringAssert.Contains("if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))", movementState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", movementState);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", movementState);
            StringAssert.Contains("return false;", movementState);
            StringAssert.Contains("return ResolvePlayerDepth() > 0.01f || _playerMovement.IsPlayerSubmerged;", movementState);
            StringAssert.Contains("if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", resolveDepth);
            StringAssert.Contains("_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("return math.max(0f, _playerMovement.CurrentDepth);", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", runtimeState);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", runtimeState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", runtimeState);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", runtimeState);
            StringAssert.Contains("math.all(math.isfinite(movementState.WorldPosition))", runtimeState);
            StringAssert.Contains("playerContext != null", hasContext);
            Assert.That(
                movementState.IndexOf("if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(movementState.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                movementState.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(movementState.IndexOf("switch (_playerMovement.CurrentLocomotionMode)", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth)", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("return _playerMovement.CurrentDepth;", source);
            StringAssert.DoesNotContain("return _playerMovement.CurrentDepth > 0.01f || _playerMovement.IsPlayerSubmerged;", source);
        }

        [Test]
        public void UnderwaterVisualsDepthAndStateUseRuntimeSnapshotBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepth()");
            string visualState = ExtractMethodBody(source, "private bool ResolveUnderwaterVisualStateForCameraDepth(");
            string runtimeState = ExtractMethodBody(source, "private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            string hasContext = ExtractMethodBody(source, "private bool HasPlayerRuntimeContext()");
            string cacheMovement = ExtractMethodBody(source, "private void CachePlayerMovement(Transform playerTransform)");

            StringAssert.Contains("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", resolveDepth);
            StringAssert.Contains("float movementDepth = math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", resolveDepth);
            StringAssert.Contains("_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth)", StringComparison.Ordinal)));

            StringAssert.Contains("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", visualState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", visualState);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", visualState);
            StringAssert.Contains("return false;", visualState);
            StringAssert.Contains("return depth > 0.01f || _playerMovement.IsPlayerSubmerged || depthDrivenUnderwater;", visualState);
            Assert.That(
                visualState.IndexOf("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(visualState.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                visualState.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(visualState.IndexOf("switch (_playerMovement.CurrentLocomotionMode)", StringComparison.Ordinal)));

            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", runtimeState);
            StringAssert.Contains("playerContext = PlayerRuntimeContextService.ActiveRuntimeContext;", runtimeState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", runtimeState);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", runtimeState);
            StringAssert.Contains("math.all(math.isfinite(movementState.WorldPosition))", runtimeState);
            StringAssert.Contains("PlayerRuntimeContextService.ActiveRuntimeContext != null", hasContext);
            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", cacheMovement);
            StringAssert.Contains("if (runtimeContext != null && ReferenceEquals(runtimeContext.PlayerTransform, playerTransform))", cacheMovement);
            StringAssert.Contains("playerContext = _playerRuntimeContext;", cacheMovement);
            StringAssert.Contains("if (playerContext != null && ReferenceEquals(playerContext.PlayerTransform, playerTransform))", cacheMovement);
            Assert.That(
                cacheMovement.IndexOf("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", StringComparison.Ordinal),
                Is.LessThan(cacheMovement.IndexOf("playerContext = _playerRuntimeContext;", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", runtimeState);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", hasContext);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", cacheMovement);
            StringAssert.DoesNotContain("activeRuntimeContext.MovementState", runtimeState);
        }

        [Test]
        public void PlayerInventoryDepthAndSubmergedStateFailClosedWithoutMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "PlayerInventory.cs");
            string depth = ExtractMethodBody(source, "private float ResolveInventoryCarrierDepthMeters()");
            string runtimeState = ExtractMethodBody(source, "private bool TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            string submerged = ExtractMethodBody(source, "private bool ResolveInventoryCarrierSubmergedState()");

            StringAssert.Contains("if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))", depth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", depth);
            StringAssert.Contains("return 0f;", depth);
            Assert.That(
                depth.IndexOf("if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(depth.IndexOf("return 0f;", StringComparison.Ordinal)));

            StringAssert.Contains("playerContext.IsInitialized", runtimeState);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", runtimeState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", runtimeState);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", runtimeState);

            StringAssert.Contains("if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))", submerged);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", submerged);
            StringAssert.Contains("movementState.DepthMeters > 0f", submerged);
            StringAssert.Contains("return false;", submerged);
            Assert.That(
                submerged.IndexOf("if (TryResolveInventoryMovementRuntimeState(out PlayerMovementRuntimeState movementState))", StringComparison.Ordinal),
                Is.LessThan(submerged.IndexOf("return false;", StringComparison.Ordinal)));

            StringAssert.DoesNotContain("return movement != null ? math.max(0f, movement.CurrentDepth) : 0f;", source);
            StringAssert.DoesNotContain("return movement != null && movement.CurrentDepth > 0f;", source);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", depth);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", submerged);
            StringAssert.DoesNotContain("movement.CurrentDepth", depth);
            StringAssert.DoesNotContain("movement.CurrentDepth", submerged);
        }

        [Test]
        public void PlayerInventoryRadiationAndImpactAupUsePoseSnapshotBeforeFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "PlayerInventory.cs");
            string resolveAup = ExtractMethodBody(source, "private bool TryResolveInventoryPlayerAup(out AbsoluteUniversePosition playerAup)");

            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", resolveAup);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", resolveAup);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", resolveAup);
            StringAssert.Contains("snapshot.Aup.IsFinite()", resolveAup);
            StringAssert.Contains("playerAup = snapshot.Aup;", resolveAup);
            StringAssert.Contains("return false;", resolveAup);
            StringAssert.Contains("return TryResolveAupFromRuntimeOrigin(transform.position, out playerAup);", resolveAup);
            Assert.That(
                resolveAup.IndexOf("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("return false;", StringComparison.Ordinal)));
            Assert.That(
                resolveAup.IndexOf("return false;", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("return TryResolveAupFromRuntimeOrigin(transform.position, out playerAup);", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("movement.CurrentAup", resolveAup);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", resolveAup);
        }

        [Test]
        public void AcousticZoneDepthAndAupFailClosedWithoutRuntimeSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "AcousticZoneController.cs");
            string currentZone = ExtractMethodBody(source, "private AcousticZoneState ResolveCurrentZone()");
            string exteriorState = ExtractMethodBody(source, "private bool ResolveMovementDrivenExteriorState(HectonPlayerMovement movement)");
            string fallbackDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthFallback()");
            string runtimeState = ExtractMethodBody(source, "private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            string hasContext = ExtractMethodBody(source, "private bool HasPlayerRuntimeContext()");
            string resolveAup = ExtractMethodBody(source, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string graphDepth = ExtractMethodBody(source, "private float ResolveUnderwaterGraphDepth01()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string clearRegistry = ExtractMethodBody(source, "private void ClearCachedRegistryServices()");
            string clearRuntimeContext = ExtractMethodBody(source, "private void ClearCachedPlayerRuntimeContext()");
            string clearSceneBindings = ExtractMethodBody(source, "private void ClearCachedPlayerSceneBindings()");
            string bindBuoyancy = ExtractMethodBody(source, "private bool TryBindPlayerBuoyancyFromCachedContext()");

            StringAssert.Contains("bool hasMovementState = TryResolvePlayerMovementRuntimeState(out _);", currentZone);
            StringAssert.Contains("if (hasMovementState || HasPlayerRuntimeContext())", currentZone);
            StringAssert.Contains("_acousticUnderwaterState = ResolveMovementDrivenExteriorState(null);", currentZone);
            StringAssert.Contains("HectonPlayerMovement movement = ResolvePlayerMovement();", currentZone);
            StringAssert.Contains("if (movement != null)", currentZone);
            Assert.That(
                currentZone.IndexOf("bool hasMovementState = TryResolvePlayerMovementRuntimeState(out _);", StringComparison.Ordinal),
                Is.LessThan(currentZone.IndexOf("HectonPlayerMovement movement = ResolvePlayerMovement();", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("movement != null || HasPlayerRuntimeContext() || TryResolvePlayerMovementRuntimeState(out _)", currentZone);
            StringAssert.Contains("bool hasMovementState = TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState);", exteriorState);
            StringAssert.Contains("if (!hasMovementState && HasPlayerRuntimeContext())", exteriorState);
            StringAssert.Contains("return false;", exteriorState);
            StringAssert.Contains("math.max(0f, movementState.DepthMeters)", exteriorState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u", exteriorState);
            StringAssert.Contains("movement.WaterImmersionRatio", exteriorState);

            StringAssert.Contains("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", fallbackDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", fallbackDepth);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", fallbackDepth);
            StringAssert.Contains("return 0f;", fallbackDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", fallbackDepth);
            StringAssert.Contains("return math.max(0f, movement.CurrentDepth);", fallbackDepth);
            Assert.That(
                fallbackDepth.IndexOf("TryResolvePlayerMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(fallbackDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                fallbackDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(fallbackDepth.IndexOf("HectonPlayerMovement movement = ResolvePlayerMovement()", StringComparison.Ordinal)));

            StringAssert.Contains("playerContext.IsInitialized", runtimeState);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", runtimeState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", runtimeState);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", runtimeState);
            StringAssert.Contains("return _playerRuntimeContext != null;", hasContext);

            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", resolveAup);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", resolveAup);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveAup);
            StringAssert.Contains("snapshot.Aup.IsFinite()", resolveAup);
            StringAssert.Contains("return false;", resolveAup);
            StringAssert.Contains("playerAup = movement.CurrentAup;", resolveAup);
            Assert.That(
                resolveAup.IndexOf("playerContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("return false;", StringComparison.Ordinal)));
            Assert.That(
                resolveAup.IndexOf("return false;", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("HectonPlayerMovement movement = ResolvePlayerMovement()", StringComparison.Ordinal)));

            StringAssert.Contains("bool hasMovementState = TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState);", graphDepth);
            StringAssert.Contains("bool hasRuntimeContext = HasPlayerRuntimeContext();", graphDepth);
            StringAssert.Contains("HectonPlayerMovement movement = hasMovementState || hasRuntimeContext ? null : ResolvePlayerMovement();", graphDepth);
            StringAssert.Contains("math.max(0f, movementState.DepthMeters)", graphDepth);
            StringAssert.Contains("!hasRuntimeContext && movement != null", graphDepth);
            Assert.That(
                graphDepth.IndexOf("TryResolvePlayerMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(graphDepth.IndexOf("ResolvePlayerMovement()", StringComparison.Ordinal)));

            StringAssert.Contains("if (_playerRuntimeContext == null)", serviceReplaced);
            StringAssert.Contains("ClearCachedPlayerSceneBindings();", serviceReplaced);
            StringAssert.Contains("break;", serviceReplaced);
            StringAssert.Contains("ClearCachedPlayerRuntimeContext();", clearRegistry);
            StringAssert.Contains("_playerRuntimeContext = null;", clearRuntimeContext);
            StringAssert.Contains("ClearCachedPlayerSceneBindings();", clearRuntimeContext);
            StringAssert.Contains("_playerMovement = null;", clearSceneBindings);
            StringAssert.Contains("_playerBuoyancyState = null;", clearSceneBindings);
            StringAssert.Contains("playerBuoyancy = null;", clearSceneBindings);
            StringAssert.Contains("_cachedPlayerAudioListener = null;", clearSceneBindings);
            StringAssert.Contains("_cachedAmbientSource = null;", clearSceneBindings);
            StringAssert.Contains("ClearCachedPlayerSceneBindings();", bindBuoyancy);
            StringAssert.Contains("_playerMovement = playerContext.PlayerMovement;", bindBuoyancy);
            Assert.That(
                bindBuoyancy.IndexOf("ClearCachedPlayerSceneBindings();", StringComparison.Ordinal),
                Is.LessThan(bindBuoyancy.IndexOf("_playerMovement = playerContext.PlayerMovement;", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = math.max(0f, movement.CurrentDepth);", source);
            StringAssert.DoesNotContain("return movement.CurrentDepth;", source);
        }

        [Test]
        public void SpectrumSonarDepthUsesPlayerRuntimeSnapshotBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "SpectrumSystem.cs");
            string emitPulse = ExtractMethodBody(source, "private bool EmitSonarPulse(");
            string resolveSpeed = ExtractMethodBody(source, "private float ResolvePlayerSpeedMagnitudeSqr()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("float depth = ResolvePlayerDepthMeters();", emitPulse);
            StringAssert.Contains("float abyssalDistortion = ResolveAbyssalDistortion(depth);", emitPulse);
            StringAssert.Contains("playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveSpeed);
            StringAssert.Contains("math.all(math.isfinite(movementState.Velocity))", resolveSpeed);
            StringAssert.Contains("return math.lengthsq(movementState.Velocity);", resolveSpeed);
            Assert.That(
                resolveSpeed.IndexOf("return 0f;", StringComparison.Ordinal),
                Is.LessThan(resolveSpeed.IndexOf("if (_playerMovement != null)", StringComparison.Ordinal)));
            StringAssert.Contains("playerRuntimeContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("? math.max(0f, movement.CurrentDepth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("return 0f;", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = ResolvePlayerMovement()", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = ResolvePlayerMovement() != null ? math.max(0f, _playerMovement.CurrentDepth) : 0f;", source);
        }

        [Test]
        public void AudioLogNarrativeRadioDepthRequiresPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "AudioLog", "AudioLogSystem.cs");
            string interference = ExtractMethodBody(source, "private float ResolveNarrativeRadioInterference01()");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolvePlayerDepthMeters(IPlayerRuntimeContext playerContext)");

            StringAssert.Contains("float depthMeters = ResolvePlayerDepthMeters(playerContext);", interference);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.DoesNotContain("HectonSurvivalSystem survivalSystem = playerContext.SurvivalSystem", resolveDepth);
            StringAssert.DoesNotContain("survivalSystem.Depth", resolveDepth);
            StringAssert.Contains("ResolveInitializedPlayerContext", source);
            StringAssert.DoesNotContain("float rawDepthMeters = survivalSystem != null ? survivalSystem.Depth : 0f;", source);
        }

        [Test]
        public void SoundscapeDepthTierSyncFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SoundscapeSystem.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters()");
            string tryResolveDepth = ExtractMethodBody(source, "private bool TryResolveCurrentDepthMeters(out float depthMeters)");
            string onDepthTier = ExtractMethodBody(source, "void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)");

            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player, null);", onEnable);
            StringAssert.Contains("SyncMusicDirectorSoundscapeContext(_currentTier, ResolveCurrentDepthMeters());", onEnable);
            StringAssert.Contains("float depth = ResolveCurrentDepthMeters();", slowTick);
            StringAssert.Contains("return TryResolveCurrentDepthMeters(out float depthMeters)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", tryResolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", tryResolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", tryResolveDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", tryResolveDepth);
            StringAssert.Contains("return true;", tryResolveDepth);
            StringAssert.Contains("if (playerContext != null)", tryResolveDepth);
            StringAssert.Contains("return false;", tryResolveDepth);
            StringAssert.Contains("HectonSurvivalSystem currentSurvival = survivalSystem;", tryResolveDepth);
            StringAssert.Contains("depthMeters = math.max(0f, currentSurvival.Depth);", tryResolveDepth);
            StringAssert.Contains("TryResolveCurrentDepthMeters(out float playerDepthMeters)", onDepthTier);
            StringAssert.Contains("? playerDepthMeters", onDepthTier);
            StringAssert.Contains(": math.max(0f, math.isfinite(depthMeters) ? depthMeters : 0f)", onDepthTier);
            StringAssert.Contains("director.SetSoundscapeTierContext(CalculateTier(resolvedDepthMeters, _currentTier), resolvedDepthMeters);", onDepthTier);
            Assert.That(
                tryResolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(tryResolveDepth.IndexOf("HectonSurvivalSystem currentSurvival = survivalSystem", StringComparison.Ordinal)));
            Assert.That(
                tryResolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(tryResolveDepth.IndexOf("HectonSurvivalSystem currentSurvival = survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f);", source);
            StringAssert.DoesNotContain("float depth = survivalSystem != null ? survivalSystem.Depth : 0f;", source);
            StringAssert.DoesNotContain("director.SetSoundscapeTierContext(CalculateTier(depthMeters, _currentTier), depthMeters);", source);
        }

        [Test]
        public void BiolumDepthIntensityFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonBiolumController.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters()");

            StringAssert.Contains("float depth = ResolveCurrentDepthMeters();", slowTick);
            StringAssert.Contains("currentSurvival != null && math.isfinite(currentSurvival.Depth)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem currentSurvival = survivalSystem", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem currentSurvival = survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = survivalSystem != null ? survivalSystem.Depth : 0f;", source);
        }

        [Test]
        public void RandomEventLoopKeepsTickingAndUsesMovementDepthWhenSurvivalIsMissing()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "RandomEventSystem.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerRuntimeContext(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("ResolveSurvivalSystem();", slowTick);
            StringAssert.Contains("float depth = ResolveCurrentDepthMeters();", slowTick);
            StringAssert.Contains("TickMeteorWaterBoomDelay(dt);", slowTick);
            StringAssert.DoesNotContain("if (survivalSystem == null && !ResolveSurvivalSystem())", slowTick);
            StringAssert.Contains("ReferenceEquals(survivalSystem, previousPlayerContext.SurvivalSystem)", cachePlayer);
            StringAssert.Contains("CachePlayerRuntimeContext(", hotSwap);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem currentSurvival = survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = survivalSystem != null ? survivalSystem.Depth : 0f;", source);
        }

        [Test]
        public void EndingConditionDepthFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "EndingSystem.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string cacheDependencies = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters()");

            StringAssert.Contains("using Unity.Mathematics;", source);
            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("float depth = ResolveCurrentDepthMeters();", slowTick);
            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player, null);", cacheDependencies);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", hotSwap);
            StringAssert.Contains("CachePlayerRuntimeContext(", hotSwap);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _survivalSystem", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _survivalSystem", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;", source);
        }

        [Test]
        public void FirstHourShadowMilestoneDepthFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "FirstHourDirector.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerContext(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters()");
            string resolveTier = ExtractMethodBody(source, "private int ResolveCurrentDepthTier(float depthMeters)");
            string resolveLiveBiome = ExtractMethodBody(source, "private bool TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector)");
            string fallbackTier = ExtractMethodBody(source, "private static int ResolveFallbackDepthTier(float depth)");
            string contextualGuidance = ExtractMethodBody(source, "private void TryIssueContextualGuidance()");
            string currentProfile = ExtractMethodBody(source, "private HectonBiomeMatrixProfile ResolveCurrentBiomeProfile(WorldZoneAnchor currentZone)");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("float depth = ResolveCurrentDepthMeters();", slowTick);
            StringAssert.Contains("int currentDepthTier = ResolveCurrentDepthTier(depth);", slowTick);
            StringAssert.Contains("ReferenceEquals(_survivalSystem, previousPlayerContext.SurvivalSystem)", cachePlayer);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", hotSwap);
            StringAssert.Contains("CachePlayerContext(", hotSwap);
            StringAssert.Contains("GlobalRegistryServiceSlot.BiomeMatrixRuntime", hotSwap);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);", hotSwap);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _survivalSystem", StringComparison.Ordinal)));
            StringAssert.Contains("if (math.isfinite(depthMeters) && depthMeters >= 0f)", resolveTier);
            StringAssert.Contains("return ResolveFallbackDepthTier(depthMeters);", resolveTier);
            StringAssert.Contains("TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector)", resolveTier);
            StringAssert.Contains("ResolveFallbackDepthTier(depthMeters)", resolveTier);
            Assert.That(
                resolveTier.IndexOf("return ResolveFallbackDepthTier(depthMeters);", StringComparison.Ordinal),
                Is.LessThan(resolveTier.IndexOf("TryResolveLiveBiomeMatrixDirector", StringComparison.Ordinal)));
            StringAssert.Contains("matrixDirector == null || !matrixDirector.isActiveAndEnabled", resolveLiveBiome);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref matrixDirector);", resolveLiveBiome);
            StringAssert.Contains("matrixDirector != null && matrixDirector.isActiveAndEnabled", resolveLiveBiome);
            StringAssert.Contains("depth <= 300f", fallbackTier);
            StringAssert.Contains("depth >= 14000f", fallbackTier);
            StringAssert.Contains("ResolveWorldContext();", contextualGuidance);
            StringAssert.Contains("int currentDepthTier = ResolveCurrentDepthTier(ResolveCurrentDepthMeters());", contextualGuidance);
            StringAssert.Contains("TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector)", currentProfile);
            StringAssert.DoesNotContain("float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;", source);
            StringAssert.DoesNotContain("int currentDepthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 1;", source);
            StringAssert.DoesNotContain("return _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;", source);
        }

        [Test]
        public void HazardClaritySignalDepthFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "HazardZoneManager.cs");
            string dispatch = ExtractMethodBody(source, "private void DispatchClarityHazardSignal(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerSignalDepthMeters()");

            StringAssert.Contains("signal.depth = ResolvePlayerSignalDepthMeters();", dispatch);
            StringAssert.Contains("survival != null && math.isfinite(survival.Depth)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _playerSurvival", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _playerSurvival", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("signal.depth = _playerSurvival != null ? _playerSurvival.Depth : 0f;", source);
        }

        [Test]
        public void AtlasSignalRevealDepthUsesMovementOrProductionSeaLevelFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "AtlasSignal", "AtlasSignalSystem.cs");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentDepthMeters(in AbsoluteUniversePosition playerAup)");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext == null)", resolveDepth);
            StringAssert.Contains("playerMovement != null && math.isfinite(playerMovement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("biomeMatrixDirector.isActiveAndEnabled", resolveDepth);
            StringAssert.Contains("math.isfinite(biomeMatrixDirector.CurrentDepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, biomeMatrixDirector.CurrentDepthMeters);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext == null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement playerMovement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.Contains("DefaultSeaLevelAupY - absoluteY", resolveDepth);
            StringAssert.DoesNotContain("return math.max(0f, (float)-absoluteY);", source);
        }

        [Test]
        public void CelestialAbyssCullFallbackUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonCelestialEngine.cs");
            string cull = ExtractMethodBody(source, "private bool ShouldCullCelestialForAbyss(out float depthMeters)");
            string matrixDepth = ExtractMethodBody(source, "private bool TryResolveBiomeMatrixDepthMeters(out float depthMeters)");
            string activeMatrix = ExtractMethodBody(source, "private bool TryResolveActiveBiomeMatrix(out BiomeMatrixDirector biomeMatrix)");
            string resolveDepth = ExtractMethodBody(source, "private static float ResolveProductionDepthFromRuntimeY(float runtimeY)");

            StringAssert.Contains("private const float DefaultSeaLevelY = 14.02f;", source);
            StringAssert.Contains("if (TryResolveBiomeMatrixDepthMeters(out float currentDepthMeters))", source);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _cachedBiomeMatrix);", source);
            StringAssert.Contains("float predictedDepthMeters = ResolveProductionDepthFromRuntimeY(predictedRuntime.y);", cull);
            StringAssert.Contains("predictedDepthMeters >= math.abs(AbyssalCelestialCullY)", cull);
            StringAssert.Contains("return false;", cull);
            StringAssert.DoesNotContain("float currentDepthMeters = ResolveProductionDepthFromRuntimeY(currentRuntime.y);", cull);
            StringAssert.DoesNotContain("currentDepthMeters >= math.abs(AbyssalCelestialCullY)", cull);
            StringAssert.Contains("TryResolveBiomeMatrixDepthMeters(out float biomeMatrixDepthMeters)", cull);
            StringAssert.Contains("depthMeters = math.max(depthMeters, biomeMatrixDepthMeters);", cull);
            StringAssert.Contains("TryResolveActiveBiomeMatrix(out BiomeMatrixDirector biomeMatrix)", matrixDepth);
            StringAssert.Contains("math.isfinite(biomeMatrix.CurrentDepthMeters)", matrixDepth);
            StringAssert.Contains("depthMeters = math.max(0f, biomeMatrix.CurrentDepthMeters);", matrixDepth);
            StringAssert.Contains("biomeMatrix == null || !biomeMatrix.isActiveAndEnabled", activeMatrix);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrix);", activeMatrix);
            StringAssert.Contains("biomeMatrix != null && biomeMatrix.isActiveAndEnabled", activeMatrix);
            StringAssert.Contains("DefaultSeaLevelY - runtimeY", resolveDepth);
            StringAssert.DoesNotContain("math.max(depthMeters, -predictedRuntime.y)", source);
            StringAssert.DoesNotContain("math.max(depthMeters, -currentRuntime.y)", source);
            StringAssert.DoesNotContain("_currentDepthMeters = Mathf.Max(0f, director.CurrentDepthMeters);", source);
        }

        [Test]
        public void GiRelayAupDepthFallbackUsesProductionSeaLevel()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Lighting", "HectonGIRelaySystem.cs");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveDepthMetersAbsolute()");

            StringAssert.Contains("private const double DefaultSeaLevelAupY = 14.02d;", source);
            StringAssert.Contains("biomeMatrix != null &&", resolveDepth);
            StringAssert.Contains("biomeMatrix.isActiveAndEnabled", resolveDepth);
            StringAssert.Contains("math.isfinite(biomeMatrix.CurrentDepthMeters)", resolveDepth);
            StringAssert.Contains("player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("movementState.PredictedAup.IsFinite()", resolveDepth);
            StringAssert.Contains("DefaultSeaLevelAupY - absolute.y", resolveDepth);
            StringAssert.DoesNotContain("movement.CurrentAup.ToAbsoluteDouble3()", resolveDepth);
            StringAssert.DoesNotContain("return math.max(0f, (float)-absolute.y);", source);
        }

        [Test]
        public void GlobalWeatherBiomeLutDepthUsesPlayerRuntimeSnapshotBeforeMatrixFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Environment", "GlobalWeatherDirector.cs");
            string resolveProfiles = ExtractMethodBody(source, "private void ResolveBiomeLutProfiles(out WeatherProfile sourceProfile, out WeatherProfile targetProfile, out float blend)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentBiomeDepthMeters()");

            StringAssert.Contains("float currentDepthMeters = ResolveCurrentBiomeDepthMeters();", resolveProfiles);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("BiomeMatrixDirector biomeMatrix = _cachedBiomeMatrix;", resolveDepth);
            StringAssert.Contains("math.isfinite(biomeMatrix.CurrentDepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, biomeMatrix.CurrentDepthMeters);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("BiomeMatrixDirector biomeMatrix = _cachedBiomeMatrix", StringComparison.Ordinal)));
        }

        [Test]
        public void MusicDirectorDepthContextRejectsInactiveBiomeMatrixAndUsesSoundscapeHint()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "HectonMusicDirector.cs");
            string refresh = ExtractMethodBody(source, "private void RefreshObservedBiomeMatrixState()");
            string layerDepth = ExtractMethodBody(source, "private float ResolveLayerDepthMeters()");
            string playerDepth = ExtractMethodBody(source, "private bool TryResolvePlayerMovementDepthMeters(out float depthMeters)");
            string resolveContext = ExtractMethodBody(source, "private bool TryResolveBiomeMatrixContext(");
            string resolveProfile = ExtractMethodBody(source, "private HectonMusicBiomeProfile ResolveProfile(bool baseContext)");
            string depthBlend = ExtractMethodBody(source, "private void ResolveDepthBlendProfile(");

            StringAssert.Contains("TryResolveBiomeMatrixContext(", refresh);
            StringAssert.Contains("_observedMatrixDepthMeters = math.max(0f, _soundscapeDepthHintMeters);", refresh);
            StringAssert.Contains("TryResolvePlayerMovementDepthMeters(out float playerDepthMeters)", layerDepth);
            StringAssert.Contains("return math.max(playerDepthMeters, soundscapeDepthMeters);", layerDepth);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", layerDepth);
            StringAssert.Contains("if (playerContext != null)", layerDepth);
            StringAssert.Contains("return 0f;", layerDepth);
            StringAssert.Contains("TryResolveBiomeMatrixContext(out _, out _, out float biomeDepthMeters)", layerDepth);
            StringAssert.Contains("float soundscapeDepthMeters = math.max(0f, _soundscapeDepthHintMeters);", layerDepth);
            StringAssert.Contains("math.max(biomeDepthMeters, soundscapeDepthMeters)", layerDepth);
            StringAssert.Contains("if (soundscapeDepthMeters > 0f)", layerDepth);
            StringAssert.Contains("return soundscapeDepthMeters;", layerDepth);
            StringAssert.Contains("_survivalSystem != null && math.isfinite(_survivalSystem.Depth)", layerDepth);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();", playerDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", playerDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", playerDepth);
            StringAssert.Contains("!math.isfinite(movementState.DepthMeters)", playerDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", playerDepth);
            Assert.That(
                layerDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(layerDepth.IndexOf("TryResolveBiomeMatrixContext(out _, out _, out float biomeDepthMeters)", StringComparison.Ordinal)));
            Assert.That(
                layerDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(layerDepth.IndexOf("if (soundscapeDepthMeters > 0f)", StringComparison.Ordinal)));
            StringAssert.Contains("!biomeMatrix.isActiveAndEnabled", resolveContext);
            StringAssert.Contains("currentDepthMeters = math.max(0f, _soundscapeDepthHintMeters);", resolveContext);
            StringAssert.Contains("currentDepthTier = math.max(0, biomeMatrix.CurrentDepthTier);", resolveContext);
            StringAssert.Contains("currentDepthMeters = math.max(0f, biomeMatrix.CurrentDepthMeters);", resolveContext);
            StringAssert.Contains("TryResolveBiomeMatrixContext(out _, out int depthTier, out _)", resolveProfile);
            StringAssert.Contains("!TryResolveBiomeMatrixContext(out _, out _, out float depthMeters)", depthBlend);
            StringAssert.DoesNotContain("_biomeMatrixDirector.CurrentDepthMeters", source);
            StringAssert.DoesNotContain("_biomeMatrixDirector.CurrentDepthTier", source);
            Assert.That(
                layerDepth.IndexOf("TryResolvePlayerMovementDepthMeters", StringComparison.Ordinal),
                Is.LessThan(layerDepth.IndexOf("TryResolveBiomeMatrixContext(out _, out _, out float biomeDepthMeters)", StringComparison.Ordinal)));
            Assert.That(
                layerDepth.IndexOf("TryResolvePlayerMovementDepthMeters", StringComparison.Ordinal),
                Is.LessThan(layerDepth.IndexOf("if (soundscapeDepthMeters > 0f)", StringComparison.Ordinal)));
            Assert.That(
                layerDepth.IndexOf("if (soundscapeDepthMeters > 0f)", StringComparison.Ordinal),
                Is.LessThan(layerDepth.IndexOf("if (_survivalSystem != null && math.isfinite(_survivalSystem.Depth))", StringComparison.Ordinal)));
        }

        [Test]
        public void AbyssalThermalVentContextRevalidatesBiomeMatrixLifecycle()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "AbyssalThermalManager.cs");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebuild = ExtractMethodBody(source, "private void RebuildVentField()");
            string isContext = ExtractMethodBody(source, "private bool IsAbyssalThermalContext()");
            string resolveFamily = ExtractMethodBody(source, "private bool TryResolveAbyssalThermalFamily(out HectonBiomeFamilyProfile family)");
            string depthGate = ExtractMethodBody(source, "private bool IsAbyssalThermalDepthGateSatisfied(");
            string playerDepth = ExtractMethodBody(source, "private bool TryResolvePlayerDepthMeters(out float depthMeters)");
            string thermalAnchor = ExtractMethodBody(source, "private bool IsThermalAnchor(WorldZoneAnchor anchor)");

            StringAssert.Contains("case GlobalRegistryServiceSlot.BiomeMatrixRuntime:", hotSwap);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);", hotSwap);
            StringAssert.Contains("if (HasSlowTickStorageReady())", hotSwap);
            StringAssert.Contains("RebuildVentField();", hotSwap);
            StringAssert.Contains("ResolveDependencies();", rebuild);
            StringAssert.Contains("TryResolveAbyssalThermalFamily(out HectonBiomeFamilyProfile family)", isContext);
            StringAssert.Contains("!IsAbyssalThermalDepthGateSatisfied(matrixDirector)", resolveFamily);
            StringAssert.Contains("TryResolvePlayerDepthMeters(out float playerDepthMeters)", depthGate);
            StringAssert.Contains("playerDepthMeters >= abyssalVentStartDepthMeters", depthGate);
            StringAssert.Contains("matrixDirector.isActiveAndEnabled", depthGate);
            StringAssert.Contains("math.isfinite(matrixDirector.CurrentDepthMeters)", depthGate);
            StringAssert.Contains("matrixDirector.CurrentDepthMeters >= abyssalVentStartDepthMeters", depthGate);
            StringAssert.Contains("return false;", depthGate);
            Assert.That(
                depthGate.IndexOf("TryResolvePlayerDepthMeters(out float playerDepthMeters)", StringComparison.Ordinal),
                Is.LessThan(depthGate.IndexOf("matrixDirector != null", StringComparison.Ordinal)));
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", playerDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", playerDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", playerDepth);
            StringAssert.Contains("if (playerContext != null)", playerDepth);
            StringAssert.Contains("return false;", playerDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", playerDepth);
            Assert.That(
                playerDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(playerDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.Contains("zoneDirector != null && zoneDirector.isActiveAndEnabled", resolveFamily);
            StringAssert.Contains("matrixDirector.CurrentFamilyProfile", resolveFamily);
            StringAssert.Contains("biomeMatrixDirector != null && biomeMatrixDirector.isActiveAndEnabled", thermalAnchor);
            StringAssert.DoesNotContain("if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentDepthMeters < abyssalVentStartDepthMeters)", source);
        }

        [Test]
        public void WorldReadabilityDepthGuidanceFallsBackToPlayerMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldReadabilityDirector.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string resolveDepth = ExtractMethodBody(source, "private void ResolveCurrentDepthContext(out int depthTier, out float depthMeters)");
            string fallbackTier = ExtractMethodBody(source, "private static int ResolveFallbackDepthTier(float depth)");
            string depthGuidance = ExtractMethodBody(source, "private static string ResolveDepthGuidanceMessage(");
            string routeLoss = ExtractMethodBody(source, "private static string ResolveRouteLossMessage(");
            string diagnostics = ExtractMethodBody(source, "private void UpdateDiagnostics()");

            StringAssert.Contains("using Unity.Mathematics;", source);
            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Player:", source);
            StringAssert.Contains("_playerRuntimeContext = GlobalRegistry.Player;", source);
            StringAssert.Contains("ResolveCurrentDepthContext(out int currentDepthTier, out float currentDepthMeters);", slowTick);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("depthTier = ResolveFallbackDepthTier(depthMeters);", resolveDepth);
            StringAssert.Contains("biomeMatrix != null &&", resolveDepth);
            StringAssert.Contains("biomeMatrix.isActiveAndEnabled", resolveDepth);
            StringAssert.Contains("math.isfinite(biomeMatrix.CurrentDepthMeters)", resolveDepth);
            StringAssert.Contains("depthTier = math.max(1, biomeMatrix.CurrentDepthTier);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("BiomeMatrixDirector biomeMatrix = biomeMatrixDirector", StringComparison.Ordinal)));
            StringAssert.Contains("depth >= 14000f", fallbackTier);
            StringAssert.Contains("if (depthTier <= 1 || depthMeters <= 0f)", depthGuidance);
            StringAssert.Contains("depthZone.requiredHullTier >= 2", depthGuidance);
            StringAssert.Contains("if (profile == null)", depthGuidance);
            StringAssert.Contains("zone.RouteCritical", depthGuidance);
            StringAssert.DoesNotContain("profile == null || depthTier <= 1", depthGuidance);
            Assert.That(
                depthGuidance.IndexOf("depthZone.requiredHullTier >= 2", StringComparison.Ordinal),
                Is.LessThan(depthGuidance.IndexOf("if (profile == null)", StringComparison.Ordinal)));
            StringAssert.Contains("if (depthTier <= 1)", routeLoss);
            StringAssert.Contains("depthZone.requiredHullTier >= 2", routeLoss);
            StringAssert.Contains("if (profile == null)", routeLoss);
            StringAssert.DoesNotContain("profile == null || depthTier <= 1", routeLoss);
            Assert.That(
                routeLoss.IndexOf("depthZone.requiredHullTier >= 2", StringComparison.Ordinal),
                Is.LessThan(routeLoss.IndexOf("if (profile == null)", StringComparison.Ordinal)));
            StringAssert.Contains("ResolveCurrentDepthContext(out _debugDepthTier, out _debugDepthMeters);", diagnostics);
            StringAssert.DoesNotContain("int currentDepthTier = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthTier : 1;", source);
            StringAssert.DoesNotContain("float currentDepthMeters = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthMeters : 0f;", source);
            StringAssert.DoesNotContain("_debugDepthMeters = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthMeters : 0f;", source);
        }

        [Test]
        public void WorldReadabilityNotificationPushRefusalKeepsPendingUntilBoundedRetry()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldReadabilityDirector.cs");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string clear = ExtractMethodBody(source, "private void ClearPendingMessage()");
            string queue = ExtractMethodBody(source, "private void QueueOrPublish(");
            string tryPublish = ExtractMethodBody(source, "private void TryPublishPending()");
            string publish = ExtractMethodBody(source, "private bool PublishNotification(");
            string retry = ExtractMethodBody(source, "private bool ShouldDropPendingNotificationAfterMiss()");
            string report = ExtractMethodBody(source, "private void ReportReadabilityNotificationMiss(");

            StringAssert.Contains("using Hecton.Localization;", source);
            StringAssert.Contains("private const int NotificationPublishRetryFrameLimit = 3;", source);
            StringAssert.Contains("private static readonly uint _NotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationContextHash", source);
            StringAssert.Contains("private int _notificationMissCount;", source);
            StringAssert.Contains("private int _pendingNotificationRetryCount;", source);
            StringAssert.Contains("public int NotificationMissCount => _notificationMissCount;", source);

            StringAssert.Contains("_pendingNotificationRetryCount = 0;", onDisable);
            StringAssert.Contains("_notificationMissCount = 0;", onDisable);
            StringAssert.Contains("_pendingNotificationRetryCount = 0;", clear);
            StringAssert.Contains("_pendingNotificationRetryCount = 0;", queue);

            StringAssert.Contains("if (!PublishNotification(_pendingMessage, _pendingSeverity))", tryPublish);
            StringAssert.Contains("if (ShouldDropPendingNotificationAfterMiss())", tryPublish);
            StringAssert.Contains("ClearPendingMessage();", tryPublish);
            AssertTextBefore(tryPublish, "if (!PublishNotification(_pendingMessage, _pendingSeverity))", "return;");

            StringAssert.Contains("bool pushed;", publish);
            StringAssert.Contains("pushed = NotificationEvents.TryPushCritical(message.AsSpan());", publish);
            StringAssert.Contains("pushed = NotificationEvents.TryPushWarning(message.AsSpan());", publish);
            StringAssert.Contains("pushed = NotificationEvents.TryPushInfo(message.AsSpan());", publish);
            StringAssert.Contains("ReportReadabilityNotificationMiss(severity);", publish);
            StringAssert.Contains("return false;", publish);
            StringAssert.Contains("return true;", publish);
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(message.AsSpan());\r\n                    break;", publish);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(message.AsSpan());\r\n                    break;", publish);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(message.AsSpan());\r\n                    break;", publish);
            AssertTextBefore(publish, "if (!pushed)", "_debugLastPublishedMessage = message;");

            StringAssert.Contains("_pendingNotificationRetryCount++;", retry);
            StringAssert.Contains("return _pendingNotificationRetryCount >= NotificationPublishRetryFrameLimit;", retry);
            StringAssert.Contains("_notificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning", report);
            StringAssert.Contains("_NotificationMissWarningHash", report);
            StringAssert.Contains("_NotificationContextHash ^ unchecked((uint)math.max(0, severity))", report);
            StringAssert.Contains("math.max(1, _notificationMissCount)", report);
        }

        [Test]
        public void SargassumParasiteModeDepthGateRevalidatesOwnersAndUsesPlayerDepthFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "SargassumMicroFaunaBoids.cs");
            string parasiteMode = ExtractMethodBody(source, "private bool IsParasiteModeActive()");
            string depthGate = ExtractMethodBody(source, "private bool IsParasiteDepthGateSatisfied()");
            string zoneContext = ExtractMethodBody(source, "private bool TryResolveParasiteModeZones(");
            string playerDepth = ExtractMethodBody(source, "private bool TryResolvePlayerDepthMeters(out float depthMeters)");

            StringAssert.Contains("private const float ParasiteModeMinDepthMeters = 2000f;", source);
            StringAssert.Contains("!IsParasiteDepthGateSatisfied()", parasiteMode);
            StringAssert.Contains("TryResolveParasiteModeZones(out WorldZoneAnchor primaryZone, out WorldZoneAnchor secondaryZone)", parasiteMode);
            StringAssert.Contains("matrixDirector == null || !matrixDirector.isActiveAndEnabled", depthGate);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref matrixDirector);", depthGate);
            StringAssert.Contains("matrixDirector.isActiveAndEnabled", depthGate);
            StringAssert.Contains("math.isfinite(matrixDirector.CurrentDepthMeters)", depthGate);
            StringAssert.Contains("matrixDirector.CurrentDepthMeters >= ParasiteModeMinDepthMeters", depthGate);
            StringAssert.Contains("TryResolvePlayerDepthMeters(out float playerDepthMeters)", depthGate);
            StringAssert.Contains("playerDepthMeters >= ParasiteModeMinDepthMeters", depthGate);
            Assert.That(
                depthGate.IndexOf("TryResolvePlayerDepthMeters(out float playerDepthMeters)", StringComparison.Ordinal),
                Is.LessThan(depthGate.IndexOf("BiomeMatrixDirector matrixDirector = _biomeMatrixDirector", StringComparison.Ordinal)));
            StringAssert.Contains("return false;", depthGate);
            StringAssert.Contains("zoneDirector == null || !zoneDirector.isActiveAndEnabled", zoneContext);
            StringAssert.Contains("WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref zoneDirector);", zoneContext);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState", source);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", playerDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", playerDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", playerDepth);
            StringAssert.Contains("if (_playerRuntimeContext != null)", playerDepth);
            StringAssert.Contains("return false;", playerDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", playerDepth);
            Assert.That(
                playerDepth.IndexOf("if (_playerRuntimeContext != null)", StringComparison.Ordinal),
                Is.LessThan(playerDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("_worldZoneDirector == null || _biomeMatrixDirector == null || _biomeMatrixDirector.CurrentDepthMeters < 2000f", source);
        }

        [Test]
        public void PdaSpectrumDepthStatusUsesPlayerMovementWhenBiomeMatrixUnavailable()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDASpectrumTab.cs");
            string status = ExtractMethodBody(source, "private void RefreshStatusLabel(int modeIndex)");
            string diagnostics = ExtractMethodBody(source, "private void RefreshBiomeDiagnostics()");
            string biomeData = ExtractMethodBody(source, "private static bool TryResolveBiomeData(");
            string displayDepth = ExtractMethodBody(source, "private bool TryResolveDisplayDepthMeters(");
            string matrixDepth = ExtractMethodBody(source, "private static bool TryResolveMatrixDepthMeters(");
            string playerDepth = ExtractMethodBody(source, "private bool TryResolvePlayerDepthMeters(out float depthMeters)");

            StringAssert.Contains("TryResolvePlayerDepthMeters(out float fallbackDepthMeters)", status);
            StringAssert.Contains("TryResolveDisplayDepthMeters(biomeDirector, out float depthMeters)", status);
            StringAssert.Contains("TryResolvePlayerDepthMeters(out float playerDepthMeters)", diagnostics);
            StringAssert.Contains("Append(\"DEPTH // \");", diagnostics);
            StringAssert.Contains("Append(\" // MATRIX N/A\");", diagnostics);
            StringAssert.Contains("return biomeDirector != null && biomeDirector.isActiveAndEnabled && matrixProfile != null;", biomeData);
            StringAssert.Contains("TryResolvePlayerDepthMeters(out depthMeters)", displayDepth);
            StringAssert.Contains("return TryResolveMatrixDepthMeters(biomeDirector, out depthMeters);", displayDepth);
            Assert.That(
                displayDepth.IndexOf("TryResolvePlayerDepthMeters(out depthMeters)", StringComparison.Ordinal),
                Is.LessThan(displayDepth.IndexOf("TryResolveMatrixDepthMeters(biomeDirector, out depthMeters)", StringComparison.Ordinal)));
            StringAssert.Contains("!biomeDirector.isActiveAndEnabled", matrixDepth);
            StringAssert.Contains("math.isfinite(biomeDirector.CurrentDepthMeters)", matrixDepth);
            StringAssert.Contains("depthMeters = math.max(0f, biomeDirector.CurrentDepthMeters);", matrixDepth);
            StringAssert.Contains("playerContext.IsInitialized", playerDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", playerDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", playerDepth);
            StringAssert.Contains("depthMeters = math.max(0f, movementState.DepthMeters);", playerDepth);
            StringAssert.Contains("if (playerContext != null)", playerDepth);
            StringAssert.Contains("return false;", playerDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", playerDepth);
            Assert.That(
                playerDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(playerDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("AppendDistance((int)math.round(biomeDirector.CurrentDepthMeters));", source);
        }

        [Test]
        public void DeepPsychosisDepthStressUsesMovementSnapshotBeforeSurvivalFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "DeepPsychosisController.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("float depthMeters = ResolvePlayerDepthMeters();", slowTick);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("survival != null && math.isfinite(survival.Depth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _survivalSystem", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depthMeters = _survivalSystem != null ? math.max(0f, _survivalSystem.Depth) : 0f;", source);
        }

        [Test]
        public void PlayerCriticalProceduralAudioDepthUsesPlayerRuntimeSnapshotBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "PlayerCriticalProceduralAudioRenderer.cs");
            string tick = ExtractMethodBody(source, "public void Tick(float deltaTime)");
            string thrusterTargets = ExtractMethodBody(source, "private void UpdateThrusterTargets(float deltaTime)");
            string sonarPressure = ExtractMethodBody(source, "private float ResolveSonarAmbientPressureScalar()");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("_hullPressureDepthTickValue = ResolveHullPressureDepth01(ResolvePlayerDepthMeters());", tick);
            StringAssert.Contains("float depth = ResolvePlayerDepthMeters();", thrusterTargets);
            StringAssert.Contains("depthMeters = ResolvePlayerDepthMeters();", sonarPressure);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("? math.max(0f, movement.CurrentDepth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("_hullPressureDepthTickValue = ResolveHullPressureDepth01(playerMovement.CurrentDepth);", source);
            StringAssert.DoesNotContain("float depth = math.max(0f, playerMovement.CurrentDepth);", source);
            StringAssert.DoesNotContain("depthMeters = math.max(0f, playerMovement.CurrentDepth);", source);
        }

        [Test]
        public void SwimPresentationImmersionDepthUsesPlayerRuntimeSnapshotBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "PlayerSwimPresentationController.cs");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerContextReferencesCold(IPlayerRuntimeContext playerContext, bool replaceExisting = false)");
            string waveBridge = ExtractMethodBody(source, "private void UpdateWaveAnimationBridge(bool activeSwimPresentation, float dt)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters()");

            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("_playerRuntimeContext = null;", cachePlayer);
            StringAssert.Contains("_playerRuntimeContext = playerContext;", cachePlayer);
            StringAssert.Contains("targetImmersionDepth = ResolvePlayerDepthMeters() * shorelineWeight;", waveBridge);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (playerContext != null)", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("? math.max(0f, movement.CurrentDepth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (playerContext != null)", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("targetImmersionDepth = math.max(0f, playerMovement.CurrentDepth) * shorelineWeight;", source);
        }

        [Test]
        public void MantaScooterDepthCacheRequiresInitializedPlayerRootSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "MantaScooter.cs");
            string refreshSnapshot = ExtractMethodBody(source, "private void RefreshSeaglideMovementStateSnapshot(float deltaTime)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveCachedDepthMeters()");

            StringAssert.Contains("!playerContext.IsInitialized", refreshSnapshot);
            StringAssert.Contains("!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState publishedState)", refreshSnapshot);
            StringAssert.Contains("(publishedState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", refreshSnapshot);
            StringAssert.Contains("!math.isfinite(publishedState.DepthMeters)", refreshSnapshot);
            StringAssert.Contains("movementState.DepthMeters = math.max(0f, publishedState.DepthMeters);", refreshSnapshot);
            StringAssert.Contains("TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("TryResolveSeaglideMovementState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("TryGetPlayerRuntimeContext", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("playerContext.IsInitialized", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal)));
        }

        [Test]
        public void FloraEnvironmentGlobalsUsePlayerRuntimeDepthBeforeVisualOrMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs");
            string publishGlobals = ExtractMethodBody(source, "private void PublishEnvironmentGlobals(Vector3 samplePositionWS)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveVegetationDepthMeters(HectonUnderwaterVisuals underwaterVisuals)");
            string runtimeState = ExtractMethodBody(source, "private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            string hasContext = ExtractMethodBody(source, "private bool HasPlayerRuntimeContext()");

            StringAssert.Contains("float depth = ResolveVegetationDepthMeters(underwaterVisuals);", publishGlobals);
            StringAssert.Contains("if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", resolveDepth);
            StringAssert.Contains("return 0f;", resolveDepth);
            StringAssert.Contains("underwaterVisuals != null && math.isfinite(underwaterVisuals.CurrentDepth)", resolveDepth);
            StringAssert.Contains("return math.max(0f, underwaterVisuals.CurrentDepth);", resolveDepth);
            StringAssert.Contains("HectonPlayerMovement movement = _playerMovement;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("? math.max(0f, movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", runtimeState);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out movementState)", runtimeState);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", runtimeState);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", runtimeState);
            StringAssert.Contains("return _playerRuntimeContext != null;", hasContext);
            Assert.That(
                resolveDepth.IndexOf("TryResolvePlayerMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("underwaterVisuals != null && math.isfinite", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("underwaterVisuals != null && math.isfinite", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = _playerMovement", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("float depth = underwaterVisuals != null ? underwaterVisuals.CurrentDepth : 0f;", source);
        }

        [Test]
        public void FloraPlayerAupRoutesUsePoseSnapshotBeforeMovementOrTransformFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraInteractionManager.cs");
            string wake = ExtractMethodBody(source, "private void PublishPlayerWakeSignal(Vector3 playerPosition, Vector3 playerVelocity, float velocityMagnitude)");
            string sway = ExtractMethodBody(source, "private bool TryResolveFloraSwayAnchorAup(float cellSize, out AbsoluteUniversePosition fieldCenterAup, out float3 fieldCenter)");
            string snapshot = ExtractMethodBody(source, "private bool TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)");
            string toxic = ExtractMethodBody(source, "private bool TryResolveToxicSporePlayerAup(Vector3 playerPositionWS, out AbsoluteUniversePosition playerAup)");

            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", snapshot);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", snapshot);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", snapshot);
            StringAssert.Contains("snapshot.Aup.IsFinite()", snapshot);
            StringAssert.Contains("return false;", snapshot);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", snapshot);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", snapshot);

            StringAssert.Contains("if (!TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))", wake);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", wake);
            StringAssert.Contains("playerAup = movement.CurrentAup;", wake);
            StringAssert.Contains("TryResolveAupFromRuntimeOrigin(playerPosition, out playerAup)", wake);
            Assert.That(
                wake.IndexOf("TryResolvePlayerAupSnapshot", StringComparison.Ordinal),
                Is.LessThan(wake.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                wake.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(wake.IndexOf("playerAup = movement.CurrentAup;", StringComparison.Ordinal)));
            Assert.That(
                wake.IndexOf("playerAup = movement.CurrentAup;", StringComparison.Ordinal),
                Is.LessThan(wake.IndexOf("TryResolveAupFromRuntimeOrigin(playerPosition, out playerAup)", StringComparison.Ordinal)));

            StringAssert.Contains("if (TryResolvePlayerAupSnapshot(out rawAup))", sway);
            StringAssert.Contains("else if (HasPlayerRuntimeContext())", sway);
            StringAssert.Contains("rawAup = _playerMovement.CurrentAup;", sway);
            StringAssert.Contains("TryResolveAupFromRuntimeOrigin(_floraSwayFieldCenterWS, out rawAup)", sway);
            Assert.That(
                sway.IndexOf("TryResolvePlayerAupSnapshot", StringComparison.Ordinal),
                Is.LessThan(sway.IndexOf("else if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                sway.IndexOf("else if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(sway.IndexOf("rawAup = _playerMovement.CurrentAup;", StringComparison.Ordinal)));

            StringAssert.Contains("if (TryResolvePlayerAupSnapshot(out playerAup))", toxic);
            StringAssert.Contains("if (HasPlayerRuntimeContext())", toxic);
            StringAssert.Contains("playerAup = movement.CurrentAup;", toxic);
            StringAssert.Contains("return TryResolveAupFromRuntimeOrigin(playerPositionWS, out playerAup);", toxic);
            Assert.That(
                toxic.IndexOf("TryResolvePlayerAupSnapshot", StringComparison.Ordinal),
                Is.LessThan(toxic.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal)));
            Assert.That(
                toxic.IndexOf("if (HasPlayerRuntimeContext())", StringComparison.Ordinal),
                Is.LessThan(toxic.IndexOf("playerAup = movement.CurrentAup;", StringComparison.Ordinal)));
        }

        [Test]
        public void MountableTransportHydrodynamicsAndDamageUsePlayerRuntimeDepthBeforeMovementFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "MountablePlayerTransport.cs");
            string kinematics = ExtractMethodBody(source, "private void ApplyMountedVehicleKinematics(");
            string damageSignal = ExtractMethodBody(source, "private HabitatDamageSignal BuildDamageSignal(");
            string resolveRiderRefs = ExtractMethodBody(source, "private bool ResolveRiderReferences(Transform interactor)");
            string clearRiderRefs = ExtractMethodBody(source, "private void ClearRiderReferences()");
            string cacheRiderContext = ExtractMethodBody(source, "private void CacheRiderPlayerRuntimeContext(IPlayerRuntimeContext playerContext)");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveDepth = ExtractMethodBody(source, "private float ResolveRiderDepthMeters()");

            StringAssert.Contains("float hydrodynamicDepthMeters = ResolveRiderDepthMeters();", kinematics);
            StringAssert.Contains("signal.depth = ResolveRiderDepthMeters();", damageSignal);
            StringAssert.Contains("private IPlayerRuntimeContext _riderPlayerRuntimeContext;", source);
            StringAssert.Contains("CacheRiderPlayerRuntimeContext(GlobalRegistry.Player);", resolveRiderRefs);
            StringAssert.Contains("_riderPlayerRuntimeContext = null;", clearRiderRefs);
            StringAssert.Contains("GlobalRegistryServiceSlot.Player", serviceReplaced);
            StringAssert.Contains("CacheRiderPlayerRuntimeContext(currentService as IPlayerRuntimeContext);", serviceReplaced);
            StringAssert.Contains("_riderPlayerRuntimeContext = IsRiderPlayerRuntimeContext(playerContext)", cacheRiderContext);
            StringAssert.Contains("HectonPlayerMovement contextMovement = _riderPlayerRuntimeContext.PlayerMovement;", cacheRiderContext);
            StringAssert.Contains("HectonSurvivalSystem contextSurvival = _riderPlayerRuntimeContext.SurvivalSystem;", cacheRiderContext);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _riderPlayerRuntimeContext;", resolveDepth);
            StringAssert.Contains("IsRiderPlayerRuntimeContext(playerContext)", resolveDepth);
            StringAssert.Contains("playerContext.IsInitialized", resolveDepth);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveDepth);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveDepth);
            StringAssert.Contains("math.isfinite(movementState.DepthMeters)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", resolveDepth);
            StringAssert.Contains("HectonPlayerMovement movement = _riderMovement;", resolveDepth);
            StringAssert.Contains("movement != null && math.isfinite(movement.CurrentDepth)", resolveDepth);
            StringAssert.Contains("return math.max(0f, movement.CurrentDepth);", resolveDepth);
            StringAssert.Contains("HectonSurvivalSystem survival = _riderSurvival;", resolveDepth);
            StringAssert.Contains("survival != null && math.isfinite(survival.Depth)", resolveDepth);
            Assert.That(
                resolveDepth.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonPlayerMovement movement = _riderMovement", StringComparison.Ordinal)));
            Assert.That(
                resolveDepth.IndexOf("HectonPlayerMovement movement = _riderMovement", StringComparison.Ordinal),
                Is.LessThan(resolveDepth.IndexOf("HectonSurvivalSystem survival = _riderSurvival", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("private HabitatDamageSignal BuildHabitatDamageSignal(", source);
            StringAssert.DoesNotContain("? math.max(0f, _riderSurvival.Depth)", source);
            StringAssert.DoesNotContain("signal.depth = _riderSurvival != null ? math.max(0f, _riderSurvival.Depth) : 0f;", source);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Could not extract method body for " + signature);
            return string.Empty;
        }

        private static void AssertTextBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThanOrEqualTo(0), second);
            Assert.That(firstIndex, Is.LessThan(secondIndex), first + " before " + second);
        }
    }
}
