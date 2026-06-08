using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VRSomaticProviderLifecycleEditTests
    {
        [Test]
        public void SomaticComfortBarrierForceCompletesBeforeReleasingVaultBuffers()
        {
            string comfortSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs");
            string providerSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs");
            string barrier = ExtractMethodBlock(comfortSource, "private void CompleteSomaticComfortForBarrier()");
            string reset = ExtractMethodBlock(comfortSource, "private void ResetSomaticComfortBuffers()");
            string listener = ExtractMethodBlock(providerSource, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(barrier, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow();"));
            Assert.That(barrier, Does.Contain("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)"));
            Assert.That(barrier, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow();"));
            Assert.That(barrier, Does.Contain("PublishSomaticComfortStateFromWrite();"));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.BeginPostSimulationSwapWindow();", StringComparison.Ordinal),
                barrier.IndexOf("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)", StringComparison.Ordinal));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.TryComplete(ref _somaticComfortHandle, forceComplete: true)", StringComparison.Ordinal),
                barrier.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                barrier.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal),
                barrier.IndexOf("PublishSomaticComfortStateFromWrite();", StringComparison.Ordinal));

            Assert.That(reset, Does.Contain("CompleteSomaticComfortForBarrier();"));
            Assert.That(reset, Does.Contain("_somaticComfortWrite.Release();"));
            Assert.Less(
                reset.IndexOf("CompleteSomaticComfortForBarrier();", StringComparison.Ordinal),
                reset.IndexOf("_somaticComfortWrite.Release();", StringComparison.Ordinal));

            Assert.That(listener, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(listener, Does.Contain("DisposeNativeBuffers();"));
            Assert.That(listener, Does.Contain("_dataVault = currentService as IDataVault;"));
            Assert.Less(
                listener.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal),
                listener.IndexOf("_dataVault = currentService as IDataVault;", StringComparison.Ordinal));
        }

        [Test]
        public void VRSomaticProviderHotSwapStateFlagTracksSuccessfulRegistryLaneRegistration()
        {
            string providerSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs");
            string register = ExtractMethodBlock(providerSource, "private void TryRegisterHotSwap()");
            string unregister = ExtractMethodBlock(providerSource, "private void TryUnregisterHotSwap()");

            Assert.That(register, Does.Contain("if (!GlobalRegistry.TryRegisterHotSwapListener(this))"));
            Assert.That(register, Does.Contain("return;"));
            Assert.That(register, Does.Contain("_stateFlags |= StateRegisteredHotSwap;"));
            Assert.Less(
                register.IndexOf("if (!GlobalRegistry.TryRegisterHotSwapListener(this))", StringComparison.Ordinal),
                register.IndexOf("_stateFlags |= StateRegisteredHotSwap;", StringComparison.Ordinal));
            Assert.That(register, Does.Not.Contain("GlobalRegistry.RegisterHotSwapListener(this);"));

            Assert.That(unregister, Does.Contain("GlobalRegistry.TryUnregisterHotSwapListener(this);"));
            Assert.That(unregister, Does.Contain("_stateFlags &= ~StateRegisteredHotSwap;"));
            Assert.That(unregister, Does.Not.Contain("GlobalRegistry.UnregisterHotSwapListener(this);"));
        }

        [Test]
        public void VRSomaticPlayerSignalsRequirePlayerRootDepthBeforeFallbacks()
        {
            string providerSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs");
            string bootstrapSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/VRSomaticRuntimeBootstrap.cs");
            string refreshGlobalState = ExtractMethodBlock(providerSource, "private void RefreshCachedGlobalState()");
            string activeHmd = ExtractMethodBlock(providerSource, "private bool TryResolveActiveHmd(out Transform activeHmd)");
            string playerSignals = ExtractMethodBlock(providerSource, "private void ResolvePlayerSignals(out float stress01, out float oxygen01, out float depthMeters)");
            string bootstrapContext = ExtractMethodBlock(bootstrapSource, "private static bool TryResolvePlayerContext(out IPlayerRuntimeContext runtimeContext, out GameObject playerObject)");
            string bootstrapHmd = ExtractMethodBlock(bootstrapSource, "private static Transform ResolveHmdTransform(IPlayerRuntimeContext runtimeContext, Transform playerTransform)");

            Assert.That(refreshGlobalState, Does.Contain("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;"));
            Assert.That(refreshGlobalState, Does.Contain("runtimeContext = _playerRuntimeContext;"));
            Assert.That(activeHmd, Does.Contain("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;"));
            Assert.That(activeHmd, Does.Contain("Camera playerCamera = runtimeContext != null ? runtimeContext.PlayerCamera : null;"));
            Assert.That(bootstrapContext, Does.Contain("runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;"));
            Assert.That(bootstrapContext, Does.Contain("runtimeContext.PlayerObject"));
            Assert.That(bootstrapHmd, Does.Contain("runtimeContext.PlayerCamera"));
            Assert.That(bootstrapHmd, Does.Contain("runtimeContext.PlayerMovement"));

            Assert.That(playerSignals, Does.Contain("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;"));
            Assert.That(playerSignals, Does.Contain("runtimeContext = _playerRuntimeContext;"));
            Assert.That(playerSignals, Does.Contain("bool hasPublishedMovement = runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState);"));
            Assert.That(playerSignals, Does.Contain("bool hasPublishedSurvival = runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState);"));
            Assert.That(playerSignals, Does.Contain("bool hasMovementDepth ="));
            Assert.That(playerSignals, Does.Contain("hasPublishedMovement &&"));
            Assert.That(playerSignals, Does.Contain("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u"));
            Assert.That(playerSignals, Does.Contain("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u"));
            Assert.That(playerSignals, Does.Contain("math.isfinite(movementState.DepthMeters);"));
            Assert.That(playerSignals, Does.Contain("depthMeters = hasMovementDepth ? SanitizeNonNegative(movementState.DepthMeters) : 0f;"));
            Assert.That(playerSignals, Does.Contain("if (hasPublishedSurvival && hasSurvival)"));
            Assert.That(playerSignals, Does.Contain("if (!hasMovementDepth && math.isfinite(movement.CurrentDepth))"));
            Assert.That(playerSignals, Does.Contain("depthMeters = math.max(depthMeters, SanitizeNonNegative(movement.CurrentDepth));"));
            Assert.That(playerSignals, Does.Contain("if (!hasMovementDepth && math.isfinite(survival.Depth))"));
            Assert.That(playerSignals, Does.Contain("depthMeters = math.max(depthMeters, SanitizeNonNegative(survival.Depth));"));
            Assert.That(playerSignals, Does.Contain("if (!hasSurvival)"));
            Assert.That(playerSignals, Does.Not.Contain("bool hasMovement ="));
            Assert.That(playerSignals, Does.Not.Contain("PlayerRuntimeContextService.TryGetActiveRuntimeContext"));
            Assert.That(playerSignals, Does.Not.Contain("runtimeContext.MovementState"));
            Assert.That(playerSignals, Does.Not.Contain("runtimeContext.SurvivalState"));
            Assert.Less(
                playerSignals.IndexOf("depthMeters = hasMovementDepth", StringComparison.Ordinal),
                playerSignals.IndexOf("HectonPlayerMovement movement = runtimeContext.PlayerMovement;", StringComparison.Ordinal));
            Assert.Less(
                playerSignals.IndexOf("if (!hasMovementDepth && math.isfinite(movement.CurrentDepth))", StringComparison.Ordinal),
                playerSignals.IndexOf("HectonSurvivalSystem survival = runtimeContext.SurvivalSystem;", StringComparison.Ordinal));
            Assert.Less(
                playerSignals.IndexOf("if (!hasMovementDepth && math.isfinite(survival.Depth))", StringComparison.Ordinal),
                playerSignals.IndexOf("if (!hasSurvival)", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
