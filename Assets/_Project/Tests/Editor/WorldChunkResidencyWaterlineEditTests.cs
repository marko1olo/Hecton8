using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldChunkResidencyWaterlineEditTests
    {
        [Test]
        public void BiomeDepthSelectionUsesProductionSeaLevelInsteadOfZeroPlane()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");
            string selectBiome = ExtractMethodBody(source, "private unsafe bool TrySelectBiomeRecordForChunk(int index, out H8BiomeRecord record)");
            string resolveDepth = ExtractMethodBody(source, "private double ResolveChunkDepthMeters(in AbsoluteUniversePositionBlit centerAup)");

            StringAssert.Contains("private const double DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("double depthMeters = ResolveChunkDepthMeters(in centerAup);", selectBiome);
            StringAssert.Contains("math.max(0d, ResolveChunkSeaLevelY() - centerY)", resolveDepth);
            StringAssert.DoesNotContain("double depthMeters = math.max(0d, -ToAbsoluteY(in centerAup));", source);
        }

        [Test]
        public void WorldResidencyPlayerAupSnapshotsRequirePlayerRootBeforePredictedAup()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");
            string motion = ExtractMethodBody(source, "private bool TryCapturePlayerMotionSnapshot(out AbsoluteUniversePosition playerAup, out float3 velocity)");
            string motionCore = ExtractMethodBody(source, "private static bool TryCapturePlayerMotionSnapshot(");
            string aupOnly = ExtractMethodBody(source, "private bool TryCapturePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)");
            string aupOnlyCore = ExtractMethodBody(source, "private static bool TryCapturePlayerAupSnapshot(");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", motion);
            StringAssert.Contains("IsPlayerRuntimeContextBound(runtimeContext)", motion);
            StringAssert.Contains("TryCapturePlayerMotionSnapshot(runtimeContext, out playerAup, out velocity);", motion);
            StringAssert.Contains("runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot)", motionCore);
            StringAssert.Contains("(poseSnapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", motionCore);
            StringAssert.Contains("poseAup = poseSnapshot.Aup;", motionCore);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", motionCore);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", motionCore);
            StringAssert.Contains("movementAup = movementState.PredictedAup;", motionCore);
            StringAssert.Contains("playerAup = poseAup;", motionCore);
            StringAssert.Contains("playerAup = movementAup;", motionCore);
            Assert.That(
                motionCore.IndexOf("runtimeContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(motionCore.IndexOf("runtimeContext.TryGetMovementRuntimeState", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement.CurrentAup", motion);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", motion);
            StringAssert.DoesNotContain("runtimeContext.MovementState", motion);
            StringAssert.DoesNotContain("runtimeContext.MovementState", motionCore);

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", aupOnly);
            StringAssert.Contains("IsPlayerRuntimeContextBound(runtimeContext)", aupOnly);
            StringAssert.Contains("TryCapturePlayerAupSnapshot(runtimeContext, out playerAup);", aupOnly);
            StringAssert.Contains("TryCapturePlayerMotionSnapshot(runtimeContext, out playerAup, out _)", aupOnlyCore);
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement.CurrentAup", aupOnly);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", aupOnly);
            StringAssert.DoesNotContain("runtimeContext.MovementState", aupOnly);
        }

        [Test]
        public void TerrainPagerAndMantaWreckPlayerAupRequirePlayerRootBeforePredictedAup()
        {
            string pagerSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "TerrainChunkPagerRuntime.cs");
            string pagerSnapshot = ExtractMethodBody(pagerSource, "private bool TryReadCameraAupSnapshot(out double3 cameraAup)");
            string pagerRuntimeSnapshot = ExtractMethodBody(pagerSource, "private static bool TryReadCameraAupFromRuntimeContext(");
            string wreckSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "MantaEmergencyWreck.cs");
            string wreckPlayerAup = ExtractMethodBody(wreckSource, "private static bool TryResolveCurrentPlayerAup(out AbsoluteUniversePosition playerAup)");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = ResolveCachedPlayerRuntimeContext();", pagerSnapshot);
            StringAssert.Contains("TryReadCameraAupFromRuntimeContext(runtimeContext, out cameraAup)", pagerSnapshot);
            StringAssert.Contains("runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", pagerRuntimeSnapshot);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", pagerRuntimeSnapshot);
            StringAssert.Contains("snapshot.Aup.IsFinite()", pagerRuntimeSnapshot);
            StringAssert.Contains("cameraAup = snapshot.Aup.ToAbsoluteDouble3();", pagerRuntimeSnapshot);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", pagerRuntimeSnapshot);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", pagerRuntimeSnapshot);
            StringAssert.Contains("movementState.PredictedAup.IsFinite()", pagerRuntimeSnapshot);
            StringAssert.Contains("cameraAup = movementState.PredictedAup.ToAbsoluteDouble3();", pagerRuntimeSnapshot);
            Assert.That(
                pagerRuntimeSnapshot.IndexOf("runtimeContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(pagerRuntimeSnapshot.IndexOf("runtimeContext.TryGetMovementRuntimeState", StringComparison.Ordinal)));
            Assert.That(
                pagerRuntimeSnapshot.IndexOf("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", StringComparison.Ordinal),
                Is.LessThan(pagerRuntimeSnapshot.IndexOf("cameraAup = movementState.PredictedAup.ToAbsoluteDouble3();", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", pagerSnapshot);
            StringAssert.DoesNotContain("runtimeContext.MovementState", pagerSnapshot);
            StringAssert.DoesNotContain("runtimeContext.MovementState", pagerRuntimeSnapshot);

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", wreckPlayerAup);
            StringAssert.Contains("runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", wreckPlayerAup);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", wreckPlayerAup);
            StringAssert.Contains("playerAup = snapshot.Aup;", wreckPlayerAup);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", wreckPlayerAup);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", wreckPlayerAup);
            StringAssert.Contains("playerAup = movementState.PredictedAup;", wreckPlayerAup);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", wreckPlayerAup);
            StringAssert.DoesNotContain("runtimeContext.MovementState", wreckPlayerAup);
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement", wreckPlayerAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", wreckPlayerAup);
            Assert.That(
                wreckPlayerAup.IndexOf("runtimeContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(wreckPlayerAup.IndexOf("runtimeContext.TryGetMovementRuntimeState", StringComparison.Ordinal)));
            Assert.That(
                wreckPlayerAup.IndexOf("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", StringComparison.Ordinal),
                Is.LessThan(wreckPlayerAup.IndexOf("playerAup = movementState.PredictedAup;", StringComparison.Ordinal)));
            Assert.That(
                wreckPlayerAup.IndexOf("playerAup = movementState.PredictedAup;", StringComparison.Ordinal),
                Is.LessThan(wreckPlayerAup.IndexOf("return false;", StringComparison.Ordinal)));
        }

        [Test]
        public void RadiationHazardGridPlayerAupUsesRuntimeContextSnapshotsBeforeFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "RadiationHazardGrid.cs");
            string preSimulation = ExtractMethodBody(source, "private JobHandle PreSimulationRadiation(");
            string resolvePlayerAup = ExtractMethodBody(source, "private static bool TryResolvePlayerAup(");
            string mutableContext = ExtractMethodBody(source, "private PlayerRuntimeContext ResolveMutablePlayerRuntimeContext()");
            string gizmos = ExtractMethodBody(source, "private void OnDrawGizmos()");

            StringAssert.Contains("IPlayerRuntimeContext playerReadContext = ResolveActivePlayerRuntimeContext();", preSimulation);
            StringAssert.Contains("bool hasPlayerAup = TryResolvePlayerAup(playerReadContext, out AbsoluteUniversePosition playerAup);", preSimulation);
            StringAssert.Contains("private JobHandle ScheduleRadiationExposureKernel(", source);
            StringAssert.Contains("IPlayerRuntimeContext playerContext,", source);
            StringAssert.Contains("private static uint ResolvePlayerCombatTargetId(IPlayerRuntimeContext playerContext)", source);
            AssertTokensInOrder(
                resolvePlayerAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "if (snapshotAup.IsFinite())",
                "playerAup = snapshotAup;",
                "return true;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition predictedAup = movementState.PredictedAup;",
                "if (predictedAup.IsFinite())",
                "playerAup = predictedAup;",
                "return true;",
                "return false;",
                "TryResolveAupFromRuntimeOrigin(Vector3.zero, out playerAup)");
            StringAssert.Contains("PlayerRuntimeContextService.TryGetActiveRuntimeContext", mutableContext);
            StringAssert.Contains("TryResolvePlayerAup(ResolveActivePlayerRuntimeContext(), out AbsoluteUniversePosition playerAup)", gizmos);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", resolvePlayerAup);
            StringAssert.DoesNotContain("playerContext.MovementState", resolvePlayerAup);
            StringAssert.DoesNotContain("runtimeContext.MovementState", resolvePlayerAup);
        }

        [Test]
        public void HectonXrHeadRuntimePositionUsesInterfaceRuntimeContext()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "HectonXRRuntimeState.cs");
            string entry = ExtractMethodBody(source, "private static bool TryResolveHeadRuntimePosition(out Vector3 runtimePosition, out XRRuntimeAup48 headAup)");
            string resolver = ExtractMethodBody(source, "IPlayerRuntimeContext runtimeContext,");

            AssertTokensInOrder(
                entry,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "if (runtimeContext != null)",
                "return TryResolveHeadRuntimePosition(runtimeContext, out runtimePosition, out headAup);",
                "IPlayerRuntimeContext playerContext = _coldPlayerContextFallback;");
            AssertTokensInOrder(
                resolver,
                "runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState)",
                "(lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "math.all(math.isfinite(lookState.EyePosition))",
                "runtimePosition = new Vector3(lookState.EyePosition.x, lookState.EyePosition.y, lookState.EyePosition.z);",
                "Camera playerCamera = runtimeContext.PlayerCamera;",
                "runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot)",
                "Transform playerTransform = runtimeContext.PlayerTransform;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", entry);
            StringAssert.DoesNotContain("PlayerRuntimeContext runtimeContext", entry);
            StringAssert.DoesNotContain("runtimeContext.LookState", resolver);
            StringAssert.DoesNotContain("runtimeContext.MovementState", resolver);
        }

        [Test]
        public void WorldViewerAupRoutesRejectNonFinitePredictedSnapshots()
        {
            string spatialSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldSpatialHashGrid.cs");
            string spatialResolve = ExtractMethodBody(spatialSource, "private static bool TryResolveActivePlayerAup(out AbsoluteUniversePosition playerAup)");
            string impostorSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ImpostorSystem.cs");
            string impostorResolve = ExtractMethodBody(impostorSource, "private AbsoluteUniversePosition ResolveViewerAup()");
            string impostorRuntimeResolve = ExtractMethodBody(impostorSource, "private static bool TryResolvePlayerAupFromRuntimeContext(");
            string impostorContextCache = ExtractMethodBody(impostorSource, "private bool TryResolveCachedPlayerRuntimeContext(");
            string lodSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "LODSystemManager.cs");
            string lodResolve = ExtractMethodBody(lodSource, "private AbsoluteUniversePosition ResolveViewerAup()");
            string ecosystemSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string ecosystemResolve = ExtractMethodBody(ecosystemSource, "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string ecosystemRuntimeResolve = ExtractMethodBody(ecosystemSource, "private static bool TryResolvePlayerAupFromRuntimeContext(");

            AssertTokensInOrder(
                spatialResolve,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "if (snapshotAup.IsFinite())",
                "playerAup = snapshotAup;",
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition predictedAup = movementState.PredictedAup;",
                "if (predictedAup.IsFinite())",
                "playerAup = predictedAup;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", spatialResolve);
            StringAssert.DoesNotContain("runtimeContext.MovementState", spatialResolve);
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement", spatialResolve);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", spatialResolve);

            AssertTokensInOrder(
                impostorResolve,
                "IPlayerRuntimeContext playerContext = _playerRuntimeContext;",
                "TryResolveCachedPlayerRuntimeContext(out playerContext);",
                "TryResolvePlayerAupFromRuntimeContext(playerContext, out AbsoluteUniversePosition playerAup)",
                "_viewerAupCache = playerAup;");
            AssertTokensInOrder(
                impostorRuntimeResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "if (snapshotAup.IsFinite())",
                "playerAup = snapshotAup;",
                "return true;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition predictedAup = movementState.PredictedAup;",
                "if (predictedAup.IsFinite())",
                "playerAup = predictedAup;",
                "return true;");
            AssertTokensInOrder(
                impostorContextCache,
                "_playerRuntimeContext != null",
                "PlayerRuntimeContextService.ActiveRuntimeContext");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", impostorContextCache);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", impostorResolve);
            StringAssert.DoesNotContain("PlayerMovementRuntimeState movementState = runtimeContext.MovementState;", impostorResolve);
            StringAssert.DoesNotContain("HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;", impostorResolve);
            StringAssert.DoesNotContain("playerContext.MovementState", impostorRuntimeResolve);
            StringAssert.DoesNotContain("runtimeContext.MovementState", impostorRuntimeResolve);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", impostorRuntimeResolve);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", impostorRuntimeResolve);

            AssertTokensInOrder(
                lodResolve,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "_viewerAupCache = movementState.PredictedAup;");
            StringAssert.DoesNotContain("playerMovement.CurrentAup", lodResolve);

            AssertTokensInOrder(
                ecosystemResolve,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "TryResolvePlayerAupFromRuntimeContext(runtimeContext, out playerAup)",
                "runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState)",
                "return false;",
                "TryResolvePlayerAupFromRuntimeContext(playerContext, out playerAup);");
            AssertTokensInOrder(
                ecosystemRuntimeResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "playerAup = snapshotAup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState fallbackMovementState)",
                "(fallbackMovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition fallbackPredictedAup = fallbackMovementState.PredictedAup;",
                "playerAup = fallbackPredictedAup;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", ecosystemResolve);
            StringAssert.DoesNotContain("runtimeContext.MovementState", ecosystemResolve);
            StringAssert.DoesNotContain("runtimeContext.LookState", ecosystemResolve);
        }

        [Test]
        public void FloraBrainPlayerRuntimeResolutionUsesInterfaceContext()
        {
            string floraSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "FloraBrain.cs");
            string resolve = ExtractMethodBody(floraSource, "private bool TryResolvePlayerRuntime(float deltaTime)");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", resolve);
            StringAssert.Contains("!runtimeContext.IsInitialized", resolve);
            StringAssert.Contains("runtimeContext.PlayerTransform == null", resolve);
            StringAssert.Contains("runtimeContext.SurvivalSystem == null", resolve);
            StringAssert.Contains("_playerTransform = runtimeContext.PlayerTransform;", resolve);
            StringAssert.Contains("_survivalSystem = runtimeContext.SurvivalSystem;", resolve);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", resolve);
            StringAssert.DoesNotContain("out PlayerRuntimeContext runtimeContext", resolve);
        }

        [Test]
        public void FaunaPlayerAupRoutesRequireFinitePredictedAup()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs");
            string perception = ExtractMethodBody(source, "private FaunaPerceptionSnapshot BuildFaunaPerceptionSnapshot()");
            string predictedAup = ExtractMethodBody(source, "private bool TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)");
            string listener = ExtractMethodBody(source, "private bool TryResolvePlayerListenerPosition(out Vector3 listenerPosition, out Transform playerRoot)");
            string logicalLod = ExtractMethodBody(source, "private void ResolveLogicalLodTier()");
            string photophobia = ExtractMethodBody(source, "private void RefreshPredatorPhotophobiaCache()");
            string directorSource = ReadProjectFile("Assets", "_Project", "Scripts", "FaunaDirector.cs");
            string directorFind = ExtractMethodBody(directorSource, "private void FindPlayer(bool force = false)");
            string directorView = ExtractMethodBody(directorSource, "private void ResolvePlayerViewTransform()");
            string directorPose = ExtractMethodBody(directorSource, "private bool TryResolvePlayerLogicPose(out Vector3 playerPosition, out AbsoluteUniversePosition playerAup)");
            string directorCache = ExtractMethodBody(directorSource, "private bool TryResolveCachedPlayerRuntimeContext(out FaunaDirectorPlayerRuntimeContextSnapshot runtimeContext)");
            string directorResolver = ExtractMethodBody(directorSource, "private IPlayerRuntimeContext ResolveActivePlayerRuntimeContext()");

            StringAssert.Contains("PlayerRuntimePoseSnapshot poseSnapshot = hasRuntimeContext && runtimeContext.HasPoseSnapshot", perception);
            StringAssert.Contains("snapshot.PlayerAup = poseSnapshot.Aup;", perception);
            StringAssert.Contains("snapshot.PlayerPosition = ToVector3(poseSnapshot.RuntimePosition);", perception);
            StringAssert.Contains("snapshot.PlayerForward = ToVector3(poseSnapshot.Forward);", perception);
            StringAssert.Contains("if (currentTool != null && hasPoseSnapshot)", perception);
            StringAssert.DoesNotContain("movementState.PredictedAup", perception);
            StringAssert.DoesNotContain("if (currentTool != null && (hasLookState || hasMovementState))", perception);

            StringAssert.Contains("runtimeContext.HasPoseSnapshot", predictedAup);
            StringAssert.Contains("playerAup = runtimeContext.PoseSnapshot.Aup;", predictedAup);
            StringAssert.DoesNotContain("runtimeContext.MovementState", predictedAup);
            StringAssert.DoesNotContain("movementState.PredictedAup", predictedAup);

            AssertTokensInOrder(
                listener,
                "TryResolveCachedLookState(in runtimeContext, out PlayerLookState lookState)",
                "listenerPosition = ToVector3(lookState.EyePosition);",
                "runtimeContext.HasPoseSnapshot",
                "listenerPosition = ToVector3(runtimeContext.PoseSnapshot.RuntimePosition);");
            StringAssert.DoesNotContain("movementState.PredictedAup", listener);

            AssertTokensInOrder(
                logicalLod,
                "runtimeContext.IsBound",
                "runtimeContext.HasPoseSnapshot",
                "AbsoluteUniversePosition playerAup = runtimeContext.PoseSnapshot.Aup;",
                "FaunaLogicalLodTier resolvedTier = ecosystemDirector.ResolveLogicalLodTier(in playerAup, in selfAup);");
            StringAssert.DoesNotContain("runtimeContext.MovementState", logicalLod);

            StringAssert.Contains("PlayerRuntimePoseSnapshot poseSnapshot = hasRuntimeContext && runtimeContext.HasPoseSnapshot", photophobia);
            StringAssert.Contains("TryResolveCachedLookState(in runtimeContext, out PlayerLookState lookState)", photophobia);
            StringAssert.Contains("lightAup = poseSnapshot.Aup;", photophobia);
            StringAssert.DoesNotContain("movementState.PredictedAup", photophobia);

            StringAssert.Contains("if (runtimeContext.HasActiveRuntimeContext)", directorFind);
            StringAssert.Contains("TryResolveCachedLookState(in runtimeContext, out PlayerLookState lookState)", directorView);

            AssertTokensInOrder(
                directorPose,
                "runtimeContext.HasPoseSnapshot",
                "(runtimeContext.PoseSnapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "runtimeContext.PoseSnapshot.Aup.IsFinite()",
                "math.all(math.isfinite(runtimeContext.PoseSnapshot.RuntimePosition))",
                "playerAup = runtimeContext.PoseSnapshot.Aup;",
                "playerPosition = (Vector3)runtimeContext.PoseSnapshot.RuntimePosition;",
                "TryResolveCachedMovementState(in runtimeContext, out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition predictedAup = movementState.PredictedAup;",
                "playerAup = predictedAup;",
                "playerPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);");
            StringAssert.DoesNotContain("runtimeContext.MovementState", directorPose);
            AssertTokensInOrder(
                directorCache,
                "IPlayerRuntimeContext playerContext = ResolveActivePlayerRuntimeContext();",
                "_playerRuntimeContextCache.HasActiveRuntimeContext = true;",
                "playerContext.TryGetPlayerPoseSnapshot(out _playerRuntimeContextCache.PoseSnapshot)",
                "playerContext.TryGetMovementRuntimeState(out _playerRuntimeContextCache.MovementState)",
                "_playerRuntimeContextCache.HasLookState = playerContext.TryGetLookRuntimeState(out _playerRuntimeContextCache.LookState)",
                "_playerRuntimeContextCacheValid = _playerRuntimeContextCache.HasActiveRuntimeContext;");
            AssertTokensInOrder(
                directorResolver,
                "IPlayerRuntimeContext activeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "if (IsUsablePlayerRuntimeContext(activeContext))",
                "IPlayerRuntimeContext cachedContext = _playerRuntimeContext;",
                "IPlayerRuntimeContext registryContext = GlobalRegistry.Player;");
        }

        [Test]
        public void WorldAndGameplayPlayerAupFallbacksRejectNonFiniteDirectMovement()
        {
            string noiseSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "PlayerNoiseEmitter.cs");
            string noiseTick = ExtractMethodBody(noiseSource, "public void Tick(float dt)");
            string noiseResolve = ExtractMethodBody(noiseSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string noiseRuntimeGuard = ExtractMethodBody(noiseSource, "private bool HasPlayerRuntimeContext()");
            string noiseRuntimeContextResolve = ExtractMethodBody(noiseSource, "private static bool TryResolvePlayerAupFromRuntimeContext(");
            string noiseStateResolve = ExtractMethodBody(noiseSource, "private static bool TryResolvePlayerAupFromMovementState(");
            string voxelSource = ReadProjectFile("Assets", "_Project", "Scripts", "HectonVoxelEngine.cs");
            string voxelResolve = ExtractMethodBody(voxelSource, "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string voxelOverhang = ExtractMethodBody(voxelSource, "static bool ShouldApplyCameraFacingOverhangNoise(VoxelPipelineData data)");
            string vegetationSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonMapMagicVegetationBridge.cs");
            string vegetationResolve = ExtractMethodBody(vegetationSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string wreckSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WreckMaterialRegistry.cs");
            string wreckResolve = ExtractMethodBody(wreckSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string ecosystemSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "EcosystemDirector.cs");
            string ecosystemResolve = ExtractMethodBody(ecosystemSource, "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string ecosystemRuntimeResolve = ExtractMethodBody(ecosystemSource, "private static bool TryResolvePlayerAupFromRuntimeContext(");
            string ecosystemStress = ExtractMethodBody(ecosystemSource, "private static bool TryResolveDirectorPlayerStress01(out float stress01)");
            string lifepodSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "LifePodTactilePrologueController.cs");
            string lifepodObserver = ExtractMethodBody(lifepodSource, "private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)");
            string acousticZoneSource = ReadProjectFile("Assets", "_Project", "Scripts", "AcousticZoneController.cs");
            string acousticZoneResolve = ExtractMethodBody(acousticZoneSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string atmosphereSource = ReadProjectFile("Assets", "_Project", "Scripts", "SubmarineAtmosphereSystem.cs");
            string atmosphereResolve = ExtractMethodBody(atmosphereSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string atlasSource = ReadProjectFile("Assets", "_Project", "Scripts", "AtlasSignal", "AtlasSignalSystem.cs");
            string atlasResolve = ExtractMethodBody(atlasSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string atlas6Source = ReadProjectFile("Assets", "_Project", "Scripts", "AtlasSignal", "Atlas6DirectiveSystem.cs");
            string atlas6Resolve = ExtractMethodBody(atlas6Source, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string fabricatorSource = ReadProjectFile("Assets", "_Project", "Scripts", "Fabricator.cs");
            string fabricatorResolve = ExtractMethodBody(fabricatorSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string worldInterestSource = ReadProjectFile("Assets", "_Project", "Scripts", "WorldInterestDirector.cs");
            string worldInterestResolve = ExtractMethodBody(worldInterestSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string spectrumSource = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "SpectrumSystem.cs");
            string spectrumResolve = ExtractMethodBody(spectrumSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string explorationSource = ReadProjectFile("Assets", "_Project", "Scripts", "PDA", "PlayerExplorationTracker.cs");
            string explorationResolve = ExtractMethodBody(explorationSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string explorationCached = ExtractMethodBody(explorationSource, "private bool TryResolveCachedPlayerRuntimeAup(out AbsoluteUniversePosition playerAup)");
            string explorationCache = ExtractMethodBody(explorationSource, "private void CachePlayerContext(IPlayerRuntimeContext playerContext)");
            string pdaMarkerSource = ReadProjectFile("Assets", "_Project", "Scripts", "PDA", "PDAMarkerHUDElement.cs");
            string pdaMarkerResolve = ExtractMethodBody(pdaMarkerSource, "private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)");
            string pdaMapSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAMapTab.cs");
            string pdaMapResolve = ExtractMethodBody(pdaMapSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string pdaSpectrumSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDASpectrumTab.cs");
            string pdaSpectrumResolve = ExtractMethodBody(pdaSpectrumSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string itemHighlightSource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "ItemHighlight.cs");
            string itemHighlightResolve = ExtractMethodBody(itemHighlightSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string beaconSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "BeaconHUDElement.cs");
            string beaconResolve = ExtractMethodBody(beaconSource, "private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)");
            string relaySource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "RelayHUDElement.cs");
            string relayResolve = ExtractMethodBody(relaySource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string missionSource = ReadProjectFile("Assets", "_Project", "Scripts", "Quest", "MissionMarkerSystem.cs");
            string missionResolve = ExtractMethodBody(missionSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string achievementSource = ReadProjectFile("Assets", "_Project", "Scripts", "Progression", "PlayerAchievementRegistry.cs");
            string achievementResolve = ExtractMethodBody(achievementSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string narrativeSource = ReadProjectFile("Assets", "_Project", "Scripts", "Progression", "NarrativeProgressionBridge.cs");
            string narrativeResolve = ExtractMethodBody(narrativeSource, "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string ladderSource = ReadProjectFile("Assets", "_Project", "Scripts", "Animation", "Locomotion", "ProceduralLadderClimbRuntime.cs");
            string ladderEntryAup = ExtractMethodBody(ladderSource, "private bool TryResolveEntryPointAup(");
            string crashSource = ReadProjectFile("Assets", "_Project", "Scripts", "CrashTelemetryBuffer.cs");
            string crashPlayerPosition = ExtractMethodBody(crashSource, "private float3 SamplePlayerPosition(out bool hasPlayer)");
            string crashPoseSnapshot = ExtractMethodBody(crashSource, "private static bool TryReadPlayerPoseSnapshot(");
            string crashMovementSnapshot = ExtractMethodBody(crashSource, "private static bool TryReadPlayerMovementAupSnapshot(");
            string physicsSource = ReadProjectFile("Assets", "_Project", "Scripts", "GlobalPhysicsStateManager.cs");
            string physicsSafeTeleport = ExtractMethodBody(physicsSource, "private void ArmSafeTeleportSpeculativeCcdForSafeTeleportInternal()");
            string physicsJitter = ExtractMethodBody(physicsSource, "private void ApplyAupJitterSentinel()");
            string physicsCullingPlayer = ExtractMethodBody(physicsSource, "private static bool TryResolvePhysicsCullingPlayerState(");
            string physicsPlayerAup = ExtractMethodBody(physicsSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string physicsCullingPartialSource = ReadProjectFile("Assets", "_Project", "Scripts", "Physics", "GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs");
            string physicsCullingCameraAup = ExtractMethodBody(physicsCullingPartialSource, "private AbsoluteUniversePosition ResolvePhysicsCullingCameraAup(");
            string physicsCullingFrustum = ExtractMethodBody(physicsCullingPartialSource, "private bool TryResolvePhysicsCullingFrustumPlanes(");

            AssertTokensInOrder(
                noiseTick,
                "TryResolvePlayerAup(out playerAup)",
                "HasPlayerRuntimeContext()",
                "return;",
                "playerPosition = ResolveCachedRuntimePosition();");
            StringAssert.Contains("_cachedPlayerContext != null", noiseRuntimeGuard);
            AssertTokensInOrder(
                noiseResolve,
                "IPlayerRuntimeContext playerContext = _cachedPlayerContext;",
                "TryResolvePlayerAupFromRuntimeContext(playerContext, out playerAup)",
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "TryResolvePlayerAupFromRuntimeContext(runtimeContext, out playerAup)",
                "AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;");
            AssertTokensInOrder(
                noiseRuntimeContextResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "TryResolvePlayerAupFromMovementState(in movementState, out playerAup);");
            StringAssert.Contains("PlayerRuntimeContextService.ActiveRuntimeContext != null", noiseRuntimeGuard);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", noiseResolve);
            StringAssert.DoesNotContain("runtimeContext.MovementState", noiseResolve);
            AssertFinitePredictedAupBeforeSuccess(noiseStateResolve);

            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", voxelResolve);
            StringAssert.Contains("playerRuntimeContext.IsInitialized", voxelResolve);
            StringAssert.Contains("AbsoluteUniversePosition.IsFinite(in movementState.PredictedAup)", voxelResolve);
            StringAssert.Contains("playerAup = movementState.PredictedAup;", voxelResolve);
            StringAssert.DoesNotContain("HectonPlayerMovement playerMovement", voxelResolve);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", voxelResolve);
            StringAssert.Contains("playerContext.IsInitialized", voxelOverhang);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", voxelOverhang);
            StringAssert.Contains("!AbsoluteUniversePosition.IsFinite(in movementState.PredictedAup)", voxelOverhang);
            StringAssert.Contains("(lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", voxelOverhang);
            Assert.That(
                voxelOverhang.IndexOf("!AbsoluteUniversePosition.IsFinite(in movementState.PredictedAup)", StringComparison.Ordinal),
                Is.LessThan(voxelOverhang.IndexOf("AbsoluteUniversePosition playerAup = movementState.PredictedAup;", StringComparison.Ordinal)));

            AssertTokensInOrder(
                vegetationResolve,
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return playerAup.IsFinite();");
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement", vegetationResolve);
            StringAssert.DoesNotContain("movement.PredictedAup", vegetationResolve);

            AssertTokensInOrder(
                wreckResolve,
                "runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "if (snapshotAup.IsFinite())",
                "playerAup = snapshotAup;",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition predictedAup = movementState.PredictedAup;",
                "if (predictedAup.IsFinite())",
                "playerAup = predictedAup;");
            StringAssert.DoesNotContain("runtimeContext.PlayerMovement", wreckResolve);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", wreckResolve);

            AssertTokensInOrder(
                ecosystemResolve,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "TryResolvePlayerAupFromRuntimeContext(runtimeContext, out playerAup)",
                "runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState)",
                "return false;",
                "TryResolvePlayerAupFromRuntimeContext(playerContext, out playerAup);");
            AssertTokensInOrder(
                ecosystemRuntimeResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition snapshotAup = snapshot.Aup;",
                "playerAup = snapshotAup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState fallbackMovementState)",
                "(fallbackMovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "AbsoluteUniversePosition fallbackPredictedAup = fallbackMovementState.PredictedAup;",
                "playerAup = fallbackPredictedAup;");
            StringAssert.DoesNotContain("playerContext.PlayerMovement", ecosystemResolve);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", ecosystemResolve);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", ecosystemResolve);
            StringAssert.DoesNotContain("runtimeContext.MovementState", ecosystemResolve);
            StringAssert.DoesNotContain("runtimeContext.LookState", ecosystemResolve);

            AssertTokensInOrder(
                ecosystemStress,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState)",
                "runtimeContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState)",
                "(stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "math.isfinite(stressState.UnderwaterStressIntensity01)",
                "stress01 = math.saturate(stress01);",
                "return resolved;",
                "IPlayerRuntimeContext playerContext = ActiveRuntimeInstance != null",
                "playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState)",
                "playerContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState)");
            StringAssert.DoesNotContain("playerContext.SurvivalSystem", ecosystemStress);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", ecosystemStress);
            StringAssert.DoesNotContain("runtimeContext.MovementState", ecosystemStress);
            StringAssert.DoesNotContain("CurrentUnderwaterStressIntensity01", ecosystemStress);

            AssertTokensInOrder(
                lifepodObserver,
                "AbsoluteUniversePosition predictedAup = _cachedObserverMovement.PredictedAup;",
                "if (predictedAup.IsFinite())",
                "observerAup = predictedAup;");

            AssertNoUnguardedCurrentAupSuccess(acousticZoneResolve);
            AssertNoUnguardedCurrentAupSuccess(atmosphereResolve);
            AssertFinitePredictedAupBeforeSuccess(atmosphereResolve);
            StringAssert.DoesNotContain("playerContext.PlayerMovement.CurrentAup", atmosphereResolve);
            AssertNoUnguardedCurrentAupSuccess(atlasResolve);
            AssertFinitePredictedAupBeforeSuccess(atlasResolve);
            AssertNoUnguardedCurrentAupSuccess(atlas6Resolve);
            AssertFinitePredictedAupBeforeSuccess(atlas6Resolve);
            AssertNoUnguardedCurrentAupSuccess(fabricatorResolve);
            AssertTokensInOrder(
                fabricatorResolve,
                "IPlayerRuntimeContext playerContext = _cachedPlayerContext;",
                "if (playerContext != null)",
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "return true;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;",
                "return false;",
                "if (_playerMovement != null)");
            StringAssert.DoesNotContain("playerContext.PlayerMovement", fabricatorResolve);
            AssertNoUnguardedCurrentAupSuccess(worldInterestResolve);
            AssertFinitePredictedAupBeforeSuccess(worldInterestResolve);
            StringAssert.DoesNotContain("_playerMovement.CurrentAup", worldInterestResolve);
            AssertNoUnguardedCurrentAupSuccess(spectrumResolve);
            AssertTokensInOrder(
                spectrumResolve,
                "IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;",
                "if (playerRuntimeContext != null)",
                "playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;",
                "return false;",
                "if (_playerMovement != null)");
            AssertNoUnguardedCurrentAupSuccess(explorationResolve);
            AssertTokensInOrder(
                explorationResolve,
                "TryResolveCachedPlayerRuntimeAup(out playerAup)",
                "if (_cachedPlayerContext != null)",
                "return false;",
                "playerAup = _playerMovement.CurrentAup;");
            AssertTokensInOrder(
                explorationCached,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "return true;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;");
            StringAssert.DoesNotContain("playerContext.PlayerMovement", explorationCached);
            AssertNoUnguardedCurrentAupSuccess(explorationCache);
            AssertTokensInOrder(
                explorationCache,
                "if (playerContext == null)",
                "_playerMovement = null;",
                "return;",
                "TryResolveCachedPlayerRuntimeAup(out AbsoluteUniversePosition snapshotAup)",
                "_lastSampledAup = snapshotAup;");
            StringAssert.DoesNotContain("CurrentAup", explorationCache);
            AssertNoUnguardedCurrentAupSuccess(pdaMarkerResolve);
            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", itemHighlightResolve);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", itemHighlightResolve);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", itemHighlightResolve);
            StringAssert.Contains("snapshot.Aup.IsFinite()", itemHighlightResolve);
            StringAssert.Contains("return false;", itemHighlightResolve);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", itemHighlightResolve);
            Assert.That(
                itemHighlightResolve.IndexOf("playerContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(itemHighlightResolve.IndexOf("return false;", StringComparison.Ordinal)));
            Assert.That(
                itemHighlightResolve.IndexOf("return false;", StringComparison.Ordinal),
                Is.LessThan(itemHighlightResolve.IndexOf("HectonPlayerMovement playerMovement = _cachedPlayerMovement;", StringComparison.Ordinal)));
            AssertTokensInOrder(
                pdaMarkerResolve,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "observerAup = movementState.PredictedAup;",
                "return true;");
            AssertNoUnguardedCurrentAupSuccess(pdaMapResolve);
            AssertNoUnguardedCurrentAupSuccess(pdaSpectrumResolve);
            AssertFinitePredictedAupBeforeSuccess(pdaSpectrumResolve);
            AssertTokensInOrder(
                pdaSpectrumResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "if (playerContext != null)",
                "return false;",
                "playerAup = _playerMovement.CurrentAup;");
            AssertNoUnguardedCurrentAupSuccess(beaconResolve);
            AssertTokensInOrder(
                beaconResolve,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "observerAup = movementState.PredictedAup;",
                "return true;");
            AssertNoUnguardedCurrentAupSuccess(relayResolve);
            AssertFinitePredictedAupBeforeSuccess(relayResolve);
            AssertTokensInOrder(
                relayResolve,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "if (playerContext != null)",
                "return false;",
                "playerAup = _playerMovement.CurrentAup;");
            AssertNoUnguardedCurrentAupSuccess(missionResolve);
            AssertFinitePredictedAupBeforeSuccess(missionResolve);
            StringAssert.DoesNotContain("_playerMovement.CurrentAup", missionResolve);
            AssertNoUnguardedCurrentAupSuccess(achievementResolve);
            AssertFinitePredictedAupBeforeSuccess(achievementResolve);
            StringAssert.DoesNotContain("_playerMovement.CurrentAup", achievementResolve);
            AssertNoUnguardedCurrentAupSuccess(narrativeResolve);
            AssertFinitePredictedAupBeforeSuccess(pdaMapResolve);
            AssertFinitePredictedAupBeforeSuccess(spectrumResolve);
            AssertTokensInOrder(
                ladderEntryAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "IsFinite(snapshot.RuntimePosition)",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!movementState.PredictedAup.IsFinite()",
                "!IsFinite(movementState.WorldPosition)",
                "AbsoluteUniversePosition playerAup = movementState.PredictedAup;",
                "float3 playerRuntime = movementState.WorldPosition;",
                "return TryOffsetAupByRuntimeDelta(");
            StringAssert.DoesNotContain("playerContext.PlayerMovement", ladderEntryAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", ladderEntryAup);

            AssertTokensInOrder(
                crashPlayerPosition,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "if (runtimeContext != null)",
                "TryReadPlayerPoseSnapshot(runtimeContext, out _, out float3 poseAup)",
                "TryReadPlayerMovementAupSnapshot(runtimeContext, out float3 movementAup)",
                "hasPlayer = false;",
                "return float3.zero;",
                "if (_playerTransform == null)",
                "AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;");
            AssertTokensInOrder(
                crashPoseSnapshot,
                "runtimeContext.TryGetPlayerPoseSnapshot(out pose)",
                "(pose.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "TryConvertAupToFloat3(pose.Aup, out playerAup);");
            AssertTokensInOrder(
                crashMovementSnapshot,
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "TryConvertAupToFloat3(movementState.PredictedAup, out playerAup);");
            StringAssert.DoesNotContain("TryReadPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose)", crashPlayerPosition);

            AssertTokensInOrder(
                physicsCullingPlayer,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!IsFinite(in movementState.PredictedAup)",
                "playerAup = movementState.PredictedAup;",
                "cameraForward = NormalizeWithRsqrtGuard(movementState.CameraForward, new float3(0f, 0f, 1f));",
                "depthMeters = math.isfinite(rawDepthMeters) ? math.max(0f, rawDepthMeters) : 0f;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsCullingPlayer);
            StringAssert.DoesNotContain("runtimeContext.MovementState", physicsCullingPlayer);
            AssertTokensInOrder(
                physicsSafeTeleport,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "Rigidbody playerBody = runtimeContext != null ? runtimeContext.PlayerRigidbody : null;");
            AssertTokensInOrder(
                physicsJitter,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "Rigidbody playerBody = runtimeContext != null ? runtimeContext.PlayerRigidbody : null;");
            AssertTokensInOrder(
                physicsCullingCameraAup,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "Camera camera = runtimeContext != null ? runtimeContext.PlayerCamera : null;");
            AssertTokensInOrder(
                physicsCullingFrustum,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "Camera camera = runtimeContext != null ? runtimeContext.PlayerCamera : null;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsSafeTeleport);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsJitter);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsCullingCameraAup);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsCullingFrustum);
            AssertTokensInOrder(
                physicsPlayerAup,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "IsFinite(in snapshot.Aup)",
                "playerAup = snapshot.Aup;",
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "IsFinite(in movementState.PredictedAup)",
                "playerAup = movementState.PredictedAup;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", physicsPlayerAup);
            StringAssert.DoesNotContain("runtimeContext.MovementState", physicsPlayerAup);
        }

        [Test]
        public void WorldAndVisorAupProjectionRoutesRejectInvalidObserverOrigins()
        {
            string chemicalSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ChemicalInfluenceGrid.cs");
            string chemicalFocus = ExtractMethodBody(chemicalSource, "private double3 ResolveFocusAup()");
            string scatterSource = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralScatterDirectorSpatialHelpers.cs");
            string scatterObserver = ExtractMethodBody(scatterSource, "private bool TryResolveObserverAbsolutePosition(out Vector3 absolutePosition)");
            string biosSource = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonBiosDiagnosticFeature.cs");
            string biosPass = ExtractMethodBody(biosSource, "public override void AddRenderPasses(");
            string biosObserver = ExtractMethodBody(biosSource, "private bool TryResolvePlayerObserverAup(out AbsoluteUniversePosition observerAup)");
            string scanRegistrySource = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "HectonScanRenderRegistry.cs");
            string nearestLoot = ExtractMethodBody(scanRegistrySource, "public static bool TryFindNearestLootSphereAup(");

            AssertTokensInOrder(
                chemicalFocus,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "double3 playerAup = movementState.PredictedAup.ToAbsoluteDouble3();",
                "if (math.all(math.isfinite(playerAup)))",
                "return playerAup;");
            StringAssert.DoesNotContain("playerContext.PlayerMovement.CurrentAup.ToAbsoluteDouble3()", chemicalFocus);

            AssertTokensInOrder(
                scatterObserver,
                "player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "double3 aup = movementState.PredictedAup.ToAbsoluteDouble3();",
                "return math.isfinite(absolutePosition.x)");
            StringAssert.DoesNotContain("movement.CurrentAup.ToAbsoluteDouble3()", scatterObserver);

            StringAssert.Contains("TryResolvePlayerObserverAup(out AbsoluteUniversePosition observerAup)", biosPass);
            StringAssert.Contains("CachePlayerContext(Hecton8.Core.GlobalRegistry.Player)", biosSource);
            StringAssert.Contains("CachePlayerContext(currentService as IPlayerRuntimeContext, allowRegistryFallback: false)", biosSource);
            StringAssert.Contains("private IPlayerRuntimeContext ResolvePlayerContext()", biosSource);
            StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", biosSource);
            StringAssert.Contains("private void InvalidateLootCache()", biosSource);
            StringAssert.Contains("playerContext is Behaviour behaviour", biosSource);
            StringAssert.Contains("InvalidateLootCache();", biosSource);
            AssertTokensInOrder(
                biosObserver,
                "IPlayerRuntimeContext playerContext = ResolvePlayerContext();",
                "!IsPlayerContextUsable(playerContext)",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!movementState.PredictedAup.IsFinite()",
                "observerAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", biosSource);
            StringAssert.DoesNotContain("playerMovement.PredictedAup", biosPass);

            AssertTokensInOrder(
                nearestLoot,
                "!observerAup.IsFinite()",
                "return false;",
                "AbsoluteUniversePosition centerAup = s_lootCenterAups[i];",
                "if (!centerAup.IsFinite())",
                "continue;",
                "double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in centerAup);");
        }

        [Test]
        public void UiAupProjectionRoutesRejectNonFiniteOffsetsAndMovementFallbacks()
        {
            string radarSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "AcousticRadarSphereRenderer.cs");
            string radarListener = ExtractMethodBody(radarSource, "private bool TryResolveListenerAup(Vector3 listenerPosition, out AbsoluteUniversePosition listenerAup)");
            string compassSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "SonarHoloCompass.cs");
            string compassView = ExtractMethodBody(compassSource, "private bool TryResolveViewAup(Vector3 viewPosition, out AbsoluteUniversePosition viewAup)");
            string echoSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "AcousticEcholocationTranslator.cs");
            string echoClassification = ExtractMethodBody(echoSource, "private bool TryResolveClassificationOriginAup(out AbsoluteUniversePosition originAup)");
            string echoCaption = ExtractMethodBody(echoSource, "private AbsoluteUniversePosition ResolveCaptionOriginAup(Vector3 viewPosition, out bool hasOriginAup)");

            AssertTokensInOrder(
                radarListener,
                "listenerAup = OffsetAupLocal(",
                "return listenerAup.IsFinite();");

            AssertTokensInOrder(
                compassView,
                "viewAup = OffsetAupLocal(",
                "return viewAup.IsFinite();");

            AssertTokensInOrder(
                echoClassification,
                "originAup = movementState.PredictedAup;",
                "return originAup.IsFinite();",
                "if (playerContext != null)",
                "return false;");

            AssertTokensInOrder(
                echoCaption,
                "AbsoluteUniversePosition originAup = OffsetAupLocal(",
                "if (originAup.IsFinite())",
                "hasOriginAup = true;",
                "if (playerContext != null)",
                "hasOriginAup = false;",
                "return default;");
            StringAssert.DoesNotContain("AbsoluteUniversePosition currentAup = movement.CurrentAup;", echoClassification);
            StringAssert.DoesNotContain("AbsoluteUniversePosition currentAup = movement.CurrentAup;", echoCaption);
            StringAssert.DoesNotContain("hasOriginAup = true;\r\n                return movement.CurrentAup;", echoCaption);
            StringAssert.DoesNotContain("hasOriginAup = true;\n                return movement.CurrentAup;", echoCaption);

            string fakeRadarSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "FakeRadarBlipController.cs");
            string fakeRadarResolve = ExtractMethodBody(fakeRadarSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            AssertFinitePredictedAupBeforeSuccess(fakeRadarResolve);
            StringAssert.DoesNotContain("playerAup = playerMovement.CurrentAup;", fakeRadarResolve);
        }

        [Test]
        public void UiAndWorldPlayerAupRoutesRejectRawMovementWhenRuntimeContextExists()
        {
            string arSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "ARWaypointOverlay.cs");
            string arCamera = ExtractMethodBody(arSource, "private bool TryResolveCameraAup(out AbsoluteUniversePosition cameraAup)");
            string wristHudSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "WristHologramHudRuntime.cs");
            string wristHudHotSwap = ExtractMethodBody(wristHudSource, "public void OnGlobalRegistryServiceReplaced(");
            string wristHudRefresh = ExtractMethodBody(wristHudSource, "private void RefreshCachedRegistryServices()");
            string pdaProjectorSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "WristHologramHudRuntime_PdaScreenProjector.cs");
            string pdaProjectionInput = ExtractMethodBody(pdaProjectorSource, "private bool BuildPdaProjectionInput(");
            string pdaRealInput = ExtractMethodBody(pdaProjectorSource, "private bool TryBuildRealPdaProjectionInput(");
            string pdaCameraAup = ExtractMethodBody(pdaProjectorSource, "private bool TryResolveCameraAupAbsoluteDouble3(");
            string pdaPlayerAup = ExtractMethodBody(pdaProjectorSource, "private static bool TryResolvePdaProjectionPlayerAup(");
            string dynamicDecalSource = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "DynamicDecalVaultRuntime.cs");
            string dynamicDecalCameraAup = ExtractMethodBody(dynamicDecalSource, "private static double3 ResolveCameraAup(Camera camera)");
            string deferredDecalSource = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "DeferredDecalPass.cs");
            string deferredDecalHotSwap = ExtractMethodBody(deferredDecalSource, "public void OnGlobalRegistryServiceReplaced(");
            string baseHudSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "BaseIntegrityHUD.cs");
            string baseHudAup = ExtractMethodBody(baseHudSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string voxelBridgeSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "HectonVoxelStreamingBridge.cs");
            string voxelPlayerAup = ExtractMethodBody(voxelBridgeSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string oreSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "Resources", "ProceduralOreSpawner.cs");
            string oreCapturePose = ExtractMethodBody(oreSource, "private static bool TryCapturePlayerPose(");
            string persistentSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "PersistentWorldRegistry.cs");
            string persistentPlayerAup = ExtractMethodBody(persistentSource, "private bool TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)");
            string resourceSource = ReadProjectFile("Assets", "_Project", "Scripts", "World", "ResourceDistributionDirector.cs");
            string resourcePlayerAup = ExtractMethodBody(resourceSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string scarcitySource = ReadProjectFile("Assets", "_Project", "Scripts", "Economy", "ResourceScarcityDirector.cs");
            string scarcityPlayerAup = ExtractMethodBody(scarcitySource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string narrativeSource = ReadProjectFile("Assets", "_Project", "Scripts", "Progression", "NarrativeProgressionBridge.cs");
            string narrativePlayerAup = ExtractMethodBody(narrativeSource, "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string pdaAtlasSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAAtlasSignalTab.cs");
            string pdaAtlasDirection = ExtractMethodBody(pdaAtlasSource, "private Vector3 ResolveAtlasDirection(");
            string pdaAtlasDistance = ExtractMethodBody(pdaAtlasSource, "private bool TryResolveAtlasCoreDistanceMeters(");
            string pdaAtlasAup = ExtractMethodBody(pdaAtlasSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string pdaIntrusionSource = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "PDAIntrusionManager.cs");
            string pdaIntrusionOrigin = ExtractMethodBody(pdaIntrusionSource, "private bool TryResolveIntrusionOriginAup(out AbsoluteUniversePosition originAup)");
            string worldSliceSource = ReadProjectFile("Assets", "_Project", "Scripts", "WorldSliceDirector.cs");
            string worldSliceAup = ExtractMethodBody(worldSliceSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string scatterPipelineSource = ReadProjectFile("Assets", "_Project", "Scripts", "WorldProceduralScatterDirectorSamplingPipeline.cs");
            string scatterPipelineAup = ExtractMethodBody(scatterPipelineSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");

            AssertTokensInOrder(
                arCamera,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "cameraAup = snapshot.Aup;",
                "return cameraAup.IsFinite();",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "cameraAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("cameraAup = playerMovement.CurrentAup;", arCamera);

            AssertTokensInOrder(
                wristHudHotSwap,
                "serviceSlot == GlobalRegistryServiceSlot.Player",
                "RefreshPlayerToxicityTargetHash(currentService as IPlayerRuntimeContext);",
                "PdaProjectorRebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);");
            AssertTokensInOrder(
                wristHudRefresh,
                "RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);",
                "PdaProjectorRebindPlayerRuntimeContext(GlobalRegistry.Player);");
            AssertTokensInOrder(
                pdaProjectionInput,
                "if (!TryResolveCameraAupAbsoluteDouble3(out double3 cameraAup))",
                "return false;",
                "BuildMockPdaProjectionInput(");
            AssertTokensInOrder(
                pdaRealInput,
                "if (!TryResolvePdaProjectionPlayerAupGuard(out _, out _))",
                "return false;",
                "TryResolveRuntimeAup(camera.transform.position",
                "TryResolveRuntimeAup(wrist.position");
            AssertTokensInOrder(
                pdaCameraAup,
                "if (!TryResolvePdaProjectionPlayerAupGuard(out bool hasPlayerContext, out AbsoluteUniversePosition playerAup))",
                "return false;",
                "TryResolveRuntimeAup(camera.transform.position",
                "if (hasPlayerContext)",
                "cameraAup = playerAup.ToAbsoluteDouble3();",
                "return true;",
                "RuntimeOriginRoute.CurrentRuntimeOriginAup()");
            AssertTokensInOrder(
                pdaPlayerAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "return true;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("PlayerMovement.CurrentAup", pdaProjectorSource);
            StringAssert.DoesNotContain("PlayerMovement.PredictedAup", pdaProjectorSource);

            AssertTokensInOrder(
                dynamicDecalCameraAup,
                "IPlayerRuntimeContext playerContext = ResolveCachedPlayerContext();",
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "return snapshot.Aup.ToAbsoluteDouble3();",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "return movementState.PredictedAup.ToAbsoluteDouble3();",
                "return double3.zero;",
                "if (camera != null)",
                "RuntimeOriginRoute.TryRuntimePositionToAup(position, ref cameraAup)",
                "return ResolveCurrentRuntimeOriginAup();");
            StringAssert.Contains("CachePlayerContext(GlobalRegistry.Player)", dynamicDecalSource);
            StringAssert.Contains("public static void RefreshColdPlayerContext(IPlayerRuntimeContext playerContext)", dynamicDecalSource);
            StringAssert.Contains("_cachedPlayerContext = IsPlayerContextUsable(playerContext) ? playerContext : null;", dynamicDecalSource);
            StringAssert.Contains("private static IPlayerRuntimeContext ResolveCachedPlayerContext()", dynamicDecalSource);
            StringAssert.Contains("private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)", dynamicDecalSource);
            StringAssert.Contains("private static bool IsCameraUsable(Camera camera)", dynamicDecalSource);
            StringAssert.Contains("playerContext is Behaviour behaviour", dynamicDecalSource);
            StringAssert.Contains("DynamicDecalVaultRuntime.RefreshColdPlayerContext(currentService as IPlayerRuntimeContext)", deferredDecalHotSwap);
            StringAssert.DoesNotContain("_cachedPlayerContext = GlobalRegistry.Player;", dynamicDecalSource);
            StringAssert.DoesNotContain("IPlayerRuntimeContext playerContext = _cachedPlayerContext;", dynamicDecalSource);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", dynamicDecalCameraAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", dynamicDecalCameraAup);

            AssertFinitePredictedAupBeforeSuccess(baseHudAup);
            AssertTokensInOrder(
                baseHudAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "playerAup = snapshot.Aup;",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "if (playerContext != null)",
                "return false;",
                "HectonPlayerMovement playerMovement = _playerMovement;");

            AssertFinitePredictedAupBeforeSuccess(voxelPlayerAup);
            AssertTokensInOrder(
                voxelPlayerAup,
                "if (playerContext != null)",
                "return false;",
                "Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;");

            AssertTokensInOrder(
                oreCapturePose,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "playerAup = snapshot.Aup;",
                "return playerAup.IsFinite() && math.all(math.isfinite(runtimePosition));",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return math.all(math.isfinite(runtimePosition));");
            StringAssert.DoesNotContain("playerMovement.CurrentAup", oreCapturePose);

            AssertTokensInOrder(
                persistentPlayerAup,
                "player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "playerAup = AbsoluteUniversePosition.Sanitize(in snapshotAup, in invalidAup);",
                "return playerAup.IsFinite();",
                "player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "AbsoluteUniversePosition movementAup = movementState.PredictedAup;",
                "return playerAup.IsFinite();");
            StringAssert.DoesNotContain("playerMovement.CurrentAup", persistentPlayerAup);

            AssertTokensInOrder(
                resourcePlayerAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "playerAup = snapshot.Aup;",
                "return playerAup.IsFinite();",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("playerAup = playerMovement.CurrentAup;", resourcePlayerAup);

            AssertTokensInOrder(
                scarcityPlayerAup,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!IsFiniteAup(in movementState.PredictedAup)",
                "playerAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("PlayerMovement.CurrentAup", scarcityPlayerAup);

            AssertTokensInOrder(
                narrativePlayerAup,
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u",
                "!movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;");
            StringAssert.DoesNotContain("PlayerMovement.CurrentAup", narrativePlayerAup);

            StringAssert.Contains("TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)", pdaAtlasDirection);
            StringAssert.Contains("TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)", pdaAtlasDistance);
            AssertFinitePredictedAupBeforeSuccess(pdaAtlasAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", pdaAtlasDirection);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", pdaAtlasDistance);

            AssertTokensInOrder(
                pdaIntrusionOrigin,
                "IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;",
                "runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "snapshot.Aup.IsFinite()",
                "originAup = snapshot.Aup;",
                "runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)",
                "(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "movementState.PredictedAup.IsFinite()",
                "originAup = movementState.PredictedAup;",
                "return false;",
                "HectonPlayerMovement playerMovement = _playerMovement;");
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", pdaIntrusionOrigin);
            StringAssert.DoesNotContain("runtimeContext.MovementState", pdaIntrusionOrigin);

            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", worldSliceAup);
            AssertFinitePredictedAupBeforeSuccess(worldSliceAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", worldSliceAup);

            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", scatterPipelineAup);
            AssertFinitePredictedAupBeforeSuccess(scatterPipelineAup);
            StringAssert.DoesNotContain("playerMovement.CurrentAup", scatterPipelineAup);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static void AssertTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                Assert.That(found, Is.GreaterThanOrEqualTo(0), tokens[i]);
                index = found + tokens[i].Length;
            }
        }

        private static void AssertNoUnguardedCurrentAupSuccess(string text)
        {
            Assert.That(text, Does.Not.Match(@"CurrentAup;\s*return true;"));
            Assert.That(text, Does.Not.Match(@"CurrentAup;\s*_hasLastSampledAup\s*=\s*true;"));
        }

        private static void AssertFinitePredictedAupBeforeSuccess(string text)
        {
            AssertTokensInOrder(
                text,
                "movementState.PredictedAup.IsFinite()",
                "playerAup = movementState.PredictedAup;",
                "return true;");
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
    }
}
