using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Tests.Editor
{
    public sealed class CrossDomainDataFlow1425EditTests
    {
        [Test]
        public void MockAudioHotSwap_RebindsReferenceWithoutRegistryPolling()
        {
            DummyAudioService oldService = new DummyAudioService(1);
            DummyAudioService newService = new DummyAudioService(2);
            AudioSwapProbe probe = new AudioSwapProbe(oldService);

            probe.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Audio, oldService, newService);

            Assert.AreSame(newService, probe.AudioService);
            Assert.AreEqual(2, probe.AudioService.TickCount);
        }

        [Test]
        public void SignalPayloadStructs_SatisfyUnmanagedSignalConstraint()
        {
            MethodInfo verifier = typeof(CrossDomainDataFlow1425EditTests).GetMethod(
                nameof(AssertUnmanagedSignalPayload),
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(verifier);

            int checkedCount = 0;
            foreach (global::System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (!type.IsValueType || type.IsEnum || type.ContainsGenericParameters)
                        continue;
                    if (!typeof(ISignal).IsAssignableFrom(type))
                        continue;

                    Assert.DoesNotThrow(
                        () => verifier.MakeGenericMethod(type).Invoke(null, null),
                        type.FullName);
                    checkedCount++;
                }
            }

            Assert.Greater(checkedCount, 0);
        }

        [Test]
        public void ShinobuDeferredReadbackCleanup_BypassesRegistryHotPath()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "public void LateFrameTick()");

            StringAssert.DoesNotContain("GlobalRegistry.UnregisterLateFrameTickable", methodBody);
            StringAssert.Contains("SystemDispatcher.UnregisterLateFrameTickableDirect", methodBody);
        }

        [Test]
        public void ShinobuTunerValues_FlattenVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "public static bool TryApplyTunerValues");

            StringAssert.DoesNotContain("TryAcquireTunerWriteView(vault, BufferID", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.Contains("TryApplyWeatherTunerValues", methodBody);
            StringAssert.Contains("TryApplyAtmosphereTunerValues", methodBody);
            StringAssert.Contains("TryApplyWaveTunerValues", methodBody);
        }

        [Test]
        public void PowerBlackBoxSample_FlattenRingAndCursorWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Power/LogisticsNetworkGraph.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "private void WritePowerBlackBoxSample");

            StringAssert.DoesNotContain("TryAcquirePowerBlackBoxWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleasePowerBlackBoxWriteLock", methodBody);
            StringAssert.Contains("TryAcquirePowerBlackBoxRingWriteLock", methodBody);
            StringAssert.Contains("TryAcquirePowerBlackBoxCursorWriteLock", methodBody);
            int ringReleaseIndex = methodBody.IndexOf("ReleaseWriteLock(in _powerBlackBoxHandle", StringComparison.Ordinal);
            int cursorAcquireIndex = methodBody.IndexOf("TryAcquirePowerBlackBoxCursorWriteLock", StringComparison.Ordinal);
            Assert.GreaterOrEqual(ringReleaseIndex, 0);
            Assert.GreaterOrEqual(cursorAcquireIndex, 0);
            Assert.Less(ringReleaseIndex, cursorAcquireIndex);
        }

        [Test]
        public void MantaResidencyHydration_BypassesHotComponentDiscovery()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/MantaEmergencyWreck.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "public void LateFrameTick()");

            StringAssert.DoesNotContain("TryGetComponent", methodBody);
            StringAssert.Contains("TryResolveLastSpawnedWreck", methodBody);
        }

        [Test]
        public void ComponentCacheValue_IsPureReadAccessor()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/ComponentCache.cs");
            string source = File.ReadAllText(sourcePath);
            string valueBody = ExtractMethodBody(source, "public T Value");
            string refreshBody = ExtractMethodBody(source, "public bool TryRefreshCold()");

            StringAssert.DoesNotContain("TryGetComponent", valueBody);
            StringAssert.DoesNotContain("GetComponent", valueBody);
            StringAssert.DoesNotContain("TryRefreshCold", valueBody);
            StringAssert.Contains("TryGetComponent", refreshBody);
        }

        [Test]
        public void WorldContentSocketZoneAnchor_IsCachedBeforeSlowTickReads()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/WorldContentSocket.cs");
            string source = File.ReadAllText(sourcePath);
            string getterBody = ExtractMethodBody(source, "public WorldZoneAnchor GetZoneAnchor()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string refreshBody = ExtractMethodBody(source, "public void RefreshZoneAnchorCold()");

            StringAssert.DoesNotContain("TryGetComponent", getterBody);
            StringAssert.DoesNotContain("GetComponentInParent", getterBody);
            StringAssert.Contains("RefreshZoneAnchorCold", onEnableBody);
            StringAssert.Contains("TryGetComponent", refreshBody);
            StringAssert.Contains("GetComponentInParent", refreshBody);
        }

        [Test]
        public void AcousticZoneTick_UsesCachedPlayerBuoyancyContext()
        {
            string acousticPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AcousticZoneController.cs");
            string acousticSource = File.ReadAllText(acousticPath);
            string tickBody = ExtractMethodBody(acousticSource, "public void Tick(float deltaTime)");
            string movementBody = ExtractMethodBody(acousticSource, "private HectonPlayerMovement ResolvePlayerMovement()");

            StringAssert.Contains("TryBindPlayerBuoyancyFromCachedContext", tickBody);
            StringAssert.DoesNotContain("FindPlayerBuoyancy", tickBody);
            StringAssert.DoesNotContain("TryGetComponent", tickBody);
            StringAssert.DoesNotContain("GetComponent", tickBody);
            StringAssert.DoesNotContain("GlobalRegistry.", tickBody);
            StringAssert.DoesNotContain("TryGetComponent", movementBody);
            StringAssert.Contains("PlayerBuoyancyAirState", acousticSource);

            string contractsPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Core/GlobalRegistryContracts.cs");
            string contractsSource = File.ReadAllText(contractsPath);
            StringAssert.Contains("IBuoyancyAirStateReadModel PlayerBuoyancyAirState", contractsSource);

            string contextPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Core/PlayerRuntimeContextService.cs");
            string contextSource = File.ReadAllText(contextPath);
            StringAssert.Contains("_playerObject.TryGetComponent(out _playerBuoyancyAirState)", contextSource);
        }

        [Test]
        public void SubmarineCoreNativeStateRefresh_AvoidsMultiVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/SubmarineCoreDirector.cs");
            string source = File.ReadAllText(sourcePath);
            string refreshBody = ExtractMethodBody(source, "private void RefreshNativeState()");
            string resolveBody = ExtractMethodBody(source, "private bool TryResolveNativeStateWriteBuffers");

            StringAssert.Contains("TryResolveNativeStateWriteBuffers", refreshBody);
            StringAssert.DoesNotContain("ReleaseNativeStateWriteBuffers", source);
            StringAssert.DoesNotContain("TryAcquireNativeStateWriteBuffers", source);
            AssertOwnerViewUsesResolveHandleOnly(resolveBody);
        }

        [Test]
        public void VocalWarningDirectQueue_UsesSignalBusInsteadOfVaultMutation()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/VocalWarningSystem.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(
                source,
                "public bool TryQueueWarning(byte warningId, float severity01, float cooldownSeconds, byte flags, uint sourceId)");

            StringAssert.Contains("SignalBus<VocalWarningSignal>.TryPushTracked", methodBody);
            StringAssert.DoesNotContain("TryResolveVwsOwnerViews", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("VocalWarningPriorityWordOps.Insert", methodBody);
        }

        [Test]
        public void VocalWarningOwnerViews_AvoidDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/VocalWarningSystem.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "private bool TryResolveVwsOwnerViews(IDataVault vault,");
            string writeTuningBody = ExtractMethodBody(source, "public unsafe bool EditorTryWriteTuning");
            string acquireTuningBody = ExtractMethodBody(source, "private bool TryAcquireTuningMutationView");
            string ensureNativeBody = ExtractMethodBody(source, "private void EnsureNativeStorage()");

            StringAssert.Contains("TryResolveHandle", methodBody);
            StringAssert.Contains("VocalWarningTuningMutationGuardMask", source);
            StringAssert.Contains("TryAcquireTuningMutationView", writeTuningBody);
            StringAssert.Contains("ReleaseVocalWarningMutationGuard", writeTuningBody);
            StringAssert.Contains("TryAcquireMutationGuard(VocalWarningTuningMutationGuardMask)", acquireTuningBody);
            StringAssert.Contains("TryResolveHandle(in _tuningHandle", acquireTuningBody);
            StringAssert.Contains("TryAcquireVocalWarningFrameGuard(vault,", ensureNativeBody);
            StringAssert.Contains("private static bool TryAcquireVocalWarningFrameGuard(IDataVault vault,", source);
            StringAssert.Contains("return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
        }

        [Test]
        public void AdaptiveStemOwnerViews_AvoidDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs");
            string source = File.ReadAllText(sourcePath);
            string tickBody = ExtractMethodBody(source, "public void Tick(float deltaTime)");
            string ownerViewBody = ExtractMethodBody(source, "private bool TryResolveStemOwnerViews");
            string ruleWriteBody = ExtractMethodBody(source, "private bool TryWriteRuleForOwnerRoute(");
            string ruleAcquireBody = ExtractMethodBody(source, "private bool TryAcquireRuleMutationView(");
            string ensureVaultStorageBody = ExtractMethodBody(source, "private void EnsureVaultStorage()");

            StringAssert.Contains("TryResolveStemOwnerViews", tickBody);
            StringAssert.Contains("AudioStemRulesMutationGuardMask", source);
            StringAssert.Contains("TryAcquireRuleMutationView", ruleWriteBody);
            StringAssert.Contains("ReleaseAdaptiveStemMutationGuard", ruleWriteBody);
            StringAssert.Contains("TryAcquireMutationGuard(AudioStemRulesMutationGuardMask)", ruleAcquireBody);
            StringAssert.Contains("TryResolveHandle(in _rulesHandle", ruleAcquireBody);
            StringAssert.Contains("AreStemVaultBuffersCreated(vault)", ensureVaultStorageBody);
            StringAssert.Contains("TryAcquireStemFrameMutationView(vault,", ensureVaultStorageBody);
            StringAssert.Contains("DisposeVaultStorage(vault)", ensureVaultStorageBody);
            StringAssert.Contains("private bool AreStemVaultBuffersCreated(IDataVault vault)", source);
            StringAssert.Contains("private bool TryAcquireStemFrameMutationView(IDataVault vault,", source);
            StringAssert.Contains("return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);", source);
            StringAssert.DoesNotContain("TryAcquireStemWriteViews", source);
            StringAssert.DoesNotContain("ReleaseStemWriteViews", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            AssertOwnerViewUsesResolveHandleOnly(ownerViewBody);
        }

        [Test]
        public void ProceduralAudioEventOwnerViews_AvoidDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/ProceduralAudioEvents.cs");
            string source = File.ReadAllText(sourcePath);
            string ownerViewBody = ExtractMethodBody(source, "private static bool TryResolveAudioEventOwnerViews(IDataVault vault,");
            string flushBody = ExtractMethodBody(source, "private static bool FlushAudioEvents()");
            string promoteBody = ExtractMethodBody(source, "private static void PromoteNextFrameEvents()");
            string ensureBody = ExtractMethodBody(source, "private static bool EnsureInitialized");

            StringAssert.DoesNotContain("TryAcquireAudioEventWriteViews", source);
            StringAssert.DoesNotContain("ReleaseAudioEventWriteViews", source);
            AssertOwnerViewUsesResolveHandleOnly(ownerViewBody);
            StringAssert.Contains("TryResolveAudioEventOwnerViews", flushBody);
            StringAssert.Contains("TryResolveAudioEventOwnerViews", promoteBody);
            StringAssert.Contains("TryResolveAudioEventOwnerViews(vault,", ensureBody);
            StringAssert.Contains("return AreAudioEventViewsCreated(vault);", ensureBody);
            StringAssert.Contains("private static bool AreAudioEventViewsCreated(IDataVault vault)", source);
            StringAssert.Contains("private static bool TryResolveAudioEventOwnerViews(IDataVault vault,", source);
        }

        [Test]
        public void BiolumEditorWriteRoutes_AvoidNestedDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs");
            string source = File.ReadAllText(sourcePath);

            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "public static bool TryWriteEditorSpeciesTuning"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "public static bool TryWriteEditorPulseControls"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "public static bool TryTriggerEditorGlobalPulse"));
        }

        [Test]
        public void BiolumColdOwnerPhaseRoutes_AvoidBufferLockStacks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs");
            string source = File.ReadAllText(sourcePath);

            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private unsafe void GenerateEmergencyMockGlows()"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private void GenerateMockLightingState()"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private unsafe void ApplyCsvOverridesIfReady()"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private void ConsumeMockPredatorSignalToPulse()"));
            AssertBiolumRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private void AdvanceSyncPulseAges"));
        }

        [Test]
        public void BiolumStateJobSchedule_AvoidsDataVaultLockLifetimePins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleStateJob");
            string teardownBody = ExtractMethodBody(source, "private void CompleteScheduledJobForTeardown()");

            StringAssert.Contains("TryResolveBiolumVaultBuffer", scheduleBody);
            StringAssert.Contains("phaseJob.Schedule()", scheduleBody);
            StringAssert.DoesNotContain("TryLockBuffer", scheduleBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", scheduleBody);
            StringAssert.DoesNotContain("_jobLocksHeld", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", teardownBody);
        }

        [Test]
        public void SymbiosisColdTick_AvoidsScheduledJobDataVaultLockPins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs");
            string source = File.ReadAllText(sourcePath);
            string coldTickBody = ExtractMethodBody(source, "public void ColdTick()");
            string bindBody = ExtractMethodBody(source, "private bool TryBindJobBuffers");
            string finishBody = ExtractMethodBody(source, "private void FinishFrameJobCompletion()");

            StringAssert.Contains("TryBindJobBuffers", coldTickBody);
            StringAssert.Contains("vault.IsCompactionFenceActive", coldTickBody);
            StringAssert.Contains("solveJob.Schedule", coldTickBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", coldTickBody);
            StringAssert.Contains("TryResolveOwnedVaultBuffer", bindBody);
            StringAssert.Contains("DispatcherJobFence.TryFinalizeCompleted", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockLockedJobBuffers", source);
            StringAssert.DoesNotContain("_jobLocksHeld", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockBuffer", coldTickBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", finishBody);
        }

        [Test]
        public void EcosystemBalancerScheduledJobs_AvoidCrossFrameDataVaultLockPins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs");
            string source = File.ReadAllText(sourcePath);
            string tickBody = ExtractMethodBody(source, "public void Tick(float deltaTime)");
            string macroBody = ExtractMethodBody(source, "private void RunMacroBiomassPass");
            string finishBody = ExtractMethodBody(source, "private void FinishFrameJobCompletion()");

            StringAssert.Contains("vault.IsCompactionFenceActive", tickBody);
            StringAssert.Contains("vault.IsCompactionFenceActive", macroBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", tickBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", macroBody);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("_jobLocksHeld", source);
            StringAssert.DoesNotContain("_jobLockedBufferCount", source);
            StringAssert.DoesNotContain("_jobLockPlan", source);
            StringAssert.DoesNotContain("TryLockBuffer", tickBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", tickBody);
            StringAssert.DoesNotContain("TryLockBuffer", macroBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", macroBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", finishBody);
        }

        [Test]
        public void EcosystemBalancerColdTelemetryRoutes_AvoidDataVaultLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("TryOpenVaultView", source);
            StringAssert.Contains("EnsureSnapshotBuffer", source);
            StringAssert.Contains("vault.IsCompactionFenceActive", ExtractMethodBody(source, "private static bool TryOpenVaultView"));
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        [Test]
        public void SpatialGridDiagnostics_AvoidDataVaultLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs");
            string source = File.ReadAllText(sourcePath);
            string failureBody = ExtractMethodBody(source, "private void RecordQueryFailure");

            StringAssert.Contains("TryResolveHandle(in TelemetryCursorHandle", failureBody);
            StringAssert.Contains("TryResolveHandle(in TelemetryHandle", failureBody);
            StringAssert.Contains("vault.IsCompactionFenceActive", failureBody);
            StringAssert.Contains("EnsureSnapshotBuffer", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        [Test]
        public void EcosystemBalancerFlockingTelemetry_AvoidsDataVaultLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs");
            string source = File.ReadAllText(sourcePath);
            string writeBody = ExtractMethodBody(source, "private void WriteFlockingTelemetryAndFaultDump");

            StringAssert.Contains("TryOpenVaultView", writeBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        [Test]
        public void EcosystemPopulationBalancerScheduledJob_AvoidsDataVaultLockPins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs");
            string source = File.ReadAllText(sourcePath);
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleBalancerJob");
            string completeBody = ExtractMethodBody(source, "private void CompleteScheduledJobForTeardown()");
            string openBody = ExtractMethodBody(source, "private static bool TryOpenVaultView");

            StringAssert.Contains("vault.IsCompactionFenceActive", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("vault.IsCompactionFenceActive", openBody);
            StringAssert.DoesNotContain("UnlockJobBuffers", completeBody);
            StringAssert.DoesNotContain("_jobLocksHeld", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        [Test]
        public void TopographicalSonarScheduledJobs_UseLocalH8MemoryBuffers()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs");
            string source = File.ReadAllText(sourcePath);
            string scheduleBody = ExtractMethodBody(source, "private void ScheduleSonarScan");
            string fadeBody = ExtractMethodBody(source, "private void TryScheduleFadeJob");
            string resolveBody = ExtractMethodBody(source, "private bool TryResolveNativeState");
            string lateBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string completeBody = ExtractMethodBody(source, "private void CompleteScheduledJobs()");

            StringAssert.Contains("EnsureJobBuffers", source);
            StringAssert.Contains("_jobPoints", resolveBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", fadeBody);
            StringAssert.Contains("MirrorCompletedScanToVault", source);
            StringAssert.Contains("MirrorCompletedPointsToVault", source);
            StringAssert.Contains("DispatcherJobFence.TryFinalizeCompleted", lateBody);
            StringAssert.DoesNotContain("TryLockScanVaultBuffers", source);
            StringAssert.DoesNotContain("UnlockScanVaultBuffers", source);
            StringAssert.DoesNotContain("TryLockFadeVaultBuffers", source);
            StringAssert.DoesNotContain("UnlockFadeVaultBuffers", source);
            StringAssert.DoesNotContain("TryLockVaultBuffer", source);
            StringAssert.DoesNotContain("_scanVaultBuffersLocked", source);
            StringAssert.DoesNotContain("_fadeVaultBuffersLocked", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryResolveVaultBuffer", scheduleBody);
            StringAssert.DoesNotContain("TryResolveVaultBuffer", fadeBody);
            StringAssert.DoesNotContain("Unlock", completeBody);
        }

        [Test]
        public void LaserCutterScheduledJobs_AvoidDataVaultLockLifetimePins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Tools/LaserCutterDodRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string scheduleBody = ExtractMethodBody(source, "public static bool TryScheduleSdfProbeBatch");
            string evaluateBody = ExtractMethodBody(source, "public static bool TryCompleteScheduledSdfProbesAndEvaluate");
            string finalizeBody = ExtractMethodBody(source, "private static bool TryFinalizeScheduledEvaluation");
            string snapshotBody = ExtractMethodBody(source, "private static bool TryCopySdfLeaseToSnapshot");

            StringAssert.Contains("BindSchedulerBuffers", scheduleBody);
            StringAssert.Contains("buildJob.Schedule", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("BindSchedulerBuffers", evaluateBody);
            StringAssert.Contains("evaluateJob.Schedule", evaluateBody);
            StringAssert.Contains("DispatcherJobFence.TryFinalizeCompleted", evaluateBody);
            StringAssert.Contains("BindSchedulerBuffers", finalizeBody);
            StringAssert.Contains("BindOrAcquireBuffer", snapshotBody);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockSdfProbeJobBuffers", source);
            StringAssert.DoesNotContain("TryLockEvaluationJobBuffers", source);
            StringAssert.DoesNotContain("ReleaseScheduledSdfProbeJobBufferLocks", source);
            StringAssert.DoesNotContain("ReleaseScheduledEvaluationJobBufferLocks", source);
            StringAssert.DoesNotContain("_scheduledSdfProbeBufferLockCount", source);
            StringAssert.DoesNotContain("_scheduledEvaluationBufferLockCount", source);
            StringAssert.DoesNotContain("_scheduledSdfSnapshotLocked", source);
        }

        [Test]
        public void FontStreamingVisiblePrefetch_AvoidsDataVaultJobLockPins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/FontStreamingManager.cs");
            string source = File.ReadAllText(sourcePath);
            string collectBody = ExtractMethodBody(source, "private void CollectSwapQueue");

            StringAssert.Contains("ResolveVisibleTextOffsetPrefetchBudget", collectBody);
            StringAssert.Contains("TryResolveVisibleTextOffsetSlice", collectBody);
            StringAssert.Contains("_swapScheduler.Enqueue(entry, prefetchedSlice, hasPrefetchedSlice)", collectBody);
            StringAssert.DoesNotContain("IDataVault", source);
            StringAssert.DoesNotContain("NativeArray", source);
            StringAssert.DoesNotContain("JobHandle", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("_visiblePrefetchBuffersLocked", source);
            StringAssert.DoesNotContain("ReleaseVisiblePrefetchJobBufferLocks", source);
            StringAssert.DoesNotContain("TryAcquireVisiblePrefetchJobBuffers", source);
            StringAssert.DoesNotContain("TryAcquireVisibleHashPrefetchWriteBuffer", source);
            StringAssert.DoesNotContain("TryScheduleVisibleTextOffsetPrefetch", source);
        }

        [Test]
        public void DiegeticGlitchColdRoutes_AvoidNestedDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs");
            string source = File.ReadAllText(sourcePath);

            AssertGlitchRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private void InitializeVaultDefaults()"));
            AssertGlitchRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private void SeedMockText()"));
            AssertGlitchRouteUsesOwnerViewsOnly(ExtractMethodBody(source, "private bool TryApplyCsvOverride"));

            string tuningSnapshotBody = ExtractMethodBody(source, "public bool TryReadTuningSnapshot");
            StringAssert.Contains("TryReadGlitchVaultBuffer", tuningSnapshotBody);
            StringAssert.DoesNotContain("TryLockBuffer", tuningSnapshotBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", tuningSnapshotBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", tuningSnapshotBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", tuningSnapshotBody);
        }

        [Test]
        public void BabelSubtitleSyncRuntime_UsesMutationGuardsInsteadOfDataVaultWriteLocks()
        {
            string subtitlePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs");
            string subtitleSource = File.ReadAllText(subtitlePath);
            string prepareBody = ExtractMethodBody(subtitleSource, "public static void PreparePresentationFrame()");
            string registerBody = ExtractMethodBody(subtitleSource, "private static bool RegisterCue");
            string scheduleBody = ExtractMethodBody(subtitleSource, "private static JobHandle ScheduleCueEvaluation");
            string telemetryBody = ExtractMethodBody(subtitleSource, "private static void WriteFrameTelemetry");
            string uiTelemetryBody = ExtractMethodBody(subtitleSource, "public static void RecordUIOptimizationFailure");
            string acquireBody = ExtractMethodBody(subtitleSource, "private static bool TryAcquireSubtitleMutationBuffer");

            AssertBabelSubtitleSourceAvoidsDataVaultWriteLocks(subtitleSource);
            AssertHotMethodAvoidsLookupAndLocks(prepareBody);
            StringAssert.Contains("CueStateMutationGuardMask", subtitleSource);
            StringAssert.Contains("TelemetryMutationGuardMask", subtitleSource);
            StringAssert.Contains("UIOptimizationTelemetryMutationGuardMask", subtitleSource);
            StringAssert.Contains("TryAcquireCueMutationBuffer", registerBody);
            StringAssert.Contains("ReleaseCueMutationBuffer", registerBody);
            StringAssert.Contains("TryAcquireCueMutationBuffer", scheduleBody);
            StringAssert.Contains("ReleaseCueMutationBuffer", scheduleBody);
            StringAssert.Contains("TryAcquireTelemetryMutationBuffer", telemetryBody);
            StringAssert.Contains("ReleaseTelemetryMutationBuffer", telemetryBody);
            StringAssert.Contains("TryAcquireUIOptimizationTelemetryMutationBuffer", uiTelemetryBody);
            StringAssert.Contains("ReleaseUIOptimizationTelemetryMutationBuffer", uiTelemetryBody);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("TryResolveHandle", acquireBody);
            StringAssert.Contains("ReleaseMutationGuard", acquireBody);
            StringAssert.Contains("finally", registerBody);
            StringAssert.Contains("finally", scheduleBody);
            StringAssert.Contains("finally", telemetryBody);
            StringAssert.Contains("finally", uiTelemetryBody);
        }

        [Test]
        public void AudioLogSystem_UsesMutationGuardsAndColdCachedAudioService()
        {
            string audioLogPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AudioLog/AudioLogSystem.cs");
            string source = File.ReadAllText(audioLogPath);
            string slowTickBody = ExtractMethodBody(source, "public void SlowTick()");
            string warningBlockerBody = ExtractMethodBody(source, "private bool TickAtmosphericWarningBlocker()");
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string flushBody = ExtractMethodBody(source, "private void FlushPendingPlaybackVisualSync()");
            string enqueueBody = ExtractMethodBody(source, "private void EnqueuePlayback");
            string startNextBody = ExtractMethodBody(source, "private void TryStartNextQueuedLog()");
            string rebuildDedupBody = ExtractMethodBody(source, "private void RebuildQueuedLogHashDedupFromQueue");
            string stopBody = ExtractMethodBody(source, "public void StopPlayback()");
            string acquireBody = ExtractMethodBody(source, "private bool TryAcquireVaultMutation");
            string encryptedClearBody = ExtractMethodBody(source, "private unsafe bool TryClearEncryptedFragmentBuffer");
            string encryptedWriteBody = ExtractMethodBody(source, "private bool TryWriteEncryptedFragmentValue");
            string encryptedPairBody = ExtractMethodBody(source, "private bool TryWriteEncryptedFragmentPair");
            string encryptedPairAcquireBody = ExtractMethodBody(source, "private bool TryAcquireEncryptedFragmentMutationView");
            string telemetryBody = ExtractMethodBody(source, "private void RecordVaultTelemetry");
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterLateFrame()");

            AssertAudioLogSourceAvoidsDataVaultWriteLocks(source);
            AssertHotMethodAvoidsLookupAndLocks(lateFrameBody);
            AssertHotMethodAvoidsLookupAndLocks(flushBody);
            StringAssert.DoesNotContain("GlobalRegistry.Audio", flushBody);
            StringAssert.Contains("SystemDispatcher.UnregisterLateFrameTickableDirect", unregisterBody);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterLateFrameTickable", unregisterBody);
            StringAssert.Contains("public bool IsNarrativeQueueBlocked => _isPlaying || _atmosphericWarningActive || _queueCount > 0", source);
            StringAssert.Contains("bool hadPlayback = _isPlaying || stoppedLog != null || _pendingPlaybackDirty || _currentPlaybackBitCrushed", stopBody);
            StringAssert.Contains("ClearPlaybackQueue();", stopBody);
            Assert.Greater(stopBody.IndexOf("ClearPlaybackQueue();", StringComparison.Ordinal),
                stopBody.IndexOf("bool hadPlayback", StringComparison.Ordinal));
            Assert.Greater(stopBody.IndexOf("if (!hadPlayback)", StringComparison.Ordinal),
                stopBody.IndexOf("ClearPlaybackQueue();", StringComparison.Ordinal));
            StringAssert.Contains("if (!_isPlaying && !_atmosphericWarningActive && _queueCount > 0)", slowTickBody);
            StringAssert.Contains("TryStartNextQueuedLog();", slowTickBody);
            Assert.Greater(slowTickBody.IndexOf("if (!_isPlaying || _currentLog == null)", StringComparison.Ordinal),
                slowTickBody.IndexOf("TryStartNextQueuedLog();", StringComparison.Ordinal));
            StringAssert.Contains("bool queuedPlaybackStarted = TickAtmosphericWarningBlocker();", slowTickBody);
            StringAssert.Contains("if (queuedPlaybackStarted)", slowTickBody);
            Assert.Greater(slowTickBody.IndexOf("if (!_isPlaying && !_atmosphericWarningActive && _queueCount > 0)", StringComparison.Ordinal),
                slowTickBody.IndexOf("if (queuedPlaybackStarted)", StringComparison.Ordinal));
            Assert.Greater(slowTickBody.IndexOf("_playbackTimer -= 0.5f", StringComparison.Ordinal),
                slowTickBody.IndexOf("if (queuedPlaybackStarted)", StringComparison.Ordinal));
            StringAssert.Contains("bool wasPlaying = _isPlaying;", warningBlockerBody);
            StringAssert.Contains("return !wasPlaying && _isPlaying;", warningBlockerBody);
            StringAssert.Contains("PlaybackQueueMutationGuardMask", source);
            StringAssert.Contains("EncryptedFragmentStateMutationGuardMask", source);
            StringAssert.Contains("TelemetryMutationGuardMask", source);
            StringAssert.Contains("TryAcquirePlaybackQueueMutationView", enqueueBody);
            StringAssert.Contains("ReleaseVaultMutation", enqueueBody);
            StringAssert.Contains("TryAcquirePlaybackQueueMutationView", startNextBody);
            StringAssert.Contains("ReleaseVaultMutation", startNextBody);
            Assert.Greater(startNextBody.LastIndexOf("ReleaseVaultMutation", StringComparison.Ordinal),
                startNextBody.IndexOf("TryAcquirePlaybackQueueMutationView", StringComparison.Ordinal));
            Assert.Greater(startNextBody.LastIndexOf("PlayLogByHash", StringComparison.Ordinal),
                startNextBody.LastIndexOf("ReleaseVaultMutation", StringComparison.Ordinal));
            StringAssert.Contains("RebuildQueuedLogHashDedupFromQueue(queue, _playbackQueueReadIndex, _queueCount)", startNextBody);
            Assert.Greater(startNextBody.LastIndexOf("ReleaseVaultMutation", StringComparison.Ordinal),
                startNextBody.IndexOf("RebuildQueuedLogHashDedupFromQueue", StringComparison.Ordinal));
            StringAssert.DoesNotContain("RemoveQueuedLogHash", source);
            StringAssert.Contains("ClearQueuedLogHashes();", rebuildDedupBody);
            StringAssert.Contains("!IsPlaybackQueued(logHash)", rebuildDedupBody);
            StringAssert.Contains("AddQueuedLogHash(logHash)", rebuildDedupBody);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("TryResolveHandle", acquireBody);
            StringAssert.Contains("ReleaseVaultMutation", acquireBody);
            StringAssert.Contains("guardVault?.ReleaseMutationGuard", source);
            StringAssert.Contains("TryAcquireVaultMutation", encryptedClearBody);
            StringAssert.Contains("EncryptedFragmentStateMutationGuardMask", encryptedClearBody);
            StringAssert.Contains("ReleaseVaultMutation", encryptedClearBody);
            StringAssert.Contains("TryAcquireVaultMutation", encryptedWriteBody);
            StringAssert.Contains("EncryptedFragmentStateMutationGuardMask", encryptedWriteBody);
            StringAssert.Contains("ReleaseVaultMutation", encryptedWriteBody);
            StringAssert.Contains("TryAcquireEncryptedFragmentMutationView", encryptedPairBody);
            StringAssert.Contains("recoveredBitBuffer[slot] = recoveredBits & EncryptedLogCompleteMask", encryptedPairBody);
            StringAssert.Contains("hashes[slot] = logHash", encryptedPairBody);
            Assert.Greater(encryptedPairBody.IndexOf("hashes[slot] = logHash", StringComparison.Ordinal),
                encryptedPairBody.IndexOf("recoveredBitBuffer[slot] = recoveredBits", StringComparison.Ordinal));
            StringAssert.Contains("ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask)", encryptedPairBody);
            StringAssert.Contains("TryAcquireMutationGuard(EncryptedFragmentStateMutationGuardMask)", encryptedPairAcquireBody);
            StringAssert.Contains("TryResolveHandle(in _encryptedFragmentLogHashesHandle", encryptedPairAcquireBody);
            StringAssert.Contains("TryResolveHandle(in _encryptedFragmentRecoveredBitsHandle", encryptedPairAcquireBody);
            StringAssert.Contains("& 31", source);
            StringAssert.Contains("TryAcquireMutationGuard", telemetryBody);
            StringAssert.Contains("TryResolveHandle", telemetryBody);
            StringAssert.Contains("ReleaseVaultMutation", telemetryBody);
            StringAssert.Contains("finally", enqueueBody);
            StringAssert.Contains("finally", startNextBody);
            StringAssert.Contains("finally", acquireBody);
            StringAssert.Contains("finally", encryptedClearBody);
            StringAssert.Contains("finally", encryptedWriteBody);
            StringAssert.Contains("finally", encryptedPairBody);
            StringAssert.Contains("finally", encryptedPairAcquireBody);
            StringAssert.Contains("finally", telemetryBody);
        }

        [Test]
        public void NativeAudioFrameRingBuffer_MirrorsTelemetryWithMutationGuard()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs");
            string source = File.ReadAllText(sourcePath);
            string mirrorBody = ExtractMethodBody(source, "private void TryMirrorTelemetryToDataVault");
            string acquireBody = ExtractMethodBody(source, "private bool TryAcquireTelemetryMutationView");

            AssertNativeAudioFrameRingBufferSourceAvoidsTelemetryWriteLocks(source);
            StringAssert.Contains("TelemetryMutationGuardMask", source);
            StringAssert.Contains("TryAcquireTelemetryMutationView", mirrorBody);
            StringAssert.Contains("ReleaseTelemetryMutationGuard", mirrorBody);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("TryResolveHandle", acquireBody);
            StringAssert.Contains("ReleaseTelemetryMutationGuard", acquireBody);
            StringAssert.Contains("ReleaseMutationGuard", source);
            StringAssert.Contains("finally", mirrorBody);
            StringAssert.Contains("finally", acquireBody);
        }

        [Test]
        public void DynamicMusicGranularSynthesizer_UsesMutationGuardsInsteadOfWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs");
            string source = File.ReadAllText(sourcePath);
            string tuningBody = ExtractMethodBody(source, "private bool TryWriteTuningSnapshot");
            string presetBody = ExtractMethodBody(source, "private bool TryWritePresetRuleSnapshot");
            string csvBody = ExtractMethodBody(source, "private bool TryReadCsvIntoScratchAndParse");
            string clearBody = ExtractMethodBody(source, "private bool TryClearMutationBuffer");
            string scalarBody = ExtractMethodBody(source, "private bool TryWriteScalarSnapshot");
            string voiceBody = ExtractMethodBody(source, "private bool TryWriteDefaultVoiceBank");
            string grainBody = ExtractMethodBody(source, "private void GenerateDefaultGrainBankCold()");
            string acquireBody = ExtractMethodBody(source, "private bool TryAcquireDynamicMusicMutationView");

            AssertDynamicMusicSourceAvoidsDataVaultWriteLocks(source);
            StringAssert.Contains("GuardPresetRules", source);
            StringAssert.Contains("GuardCsvScratch", source);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("TryResolveHandle", acquireBody);
            StringAssert.Contains("ReleaseDynamicMusicMutationGuard", acquireBody);
            StringAssert.Contains("finally", acquireBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", tuningBody);
            StringAssert.Contains("ReleaseDynamicMusicMutationGuard", tuningBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", presetBody);
            StringAssert.Contains("ReleaseDynamicMusicMutationGuard", presetBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", csvBody);
            StringAssert.Contains("ReleaseDynamicMusicMutationGuard", csvBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", clearBody);
            StringAssert.Contains("ReleaseDynamicMusicMutationGuard", clearBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", scalarBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", voiceBody);
            StringAssert.Contains("TryAcquireDynamicMusicMutationView", grainBody);
            StringAssert.Contains("finally", tuningBody);
            StringAssert.Contains("finally", presetBody);
            StringAssert.Contains("finally", csvBody);
            StringAssert.Contains("finally", clearBody);
            StringAssert.Contains("finally", scalarBody);
            StringAssert.Contains("finally", voiceBody);
            StringAssert.Contains("finally", grainBody);
        }

        [Test]
        public void PlayerCriticalAudioPrologueTelemetry_UsesMutationGuards()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs");
            string source = File.ReadAllText(sourcePath);
            string queueBody = ExtractMethodBody(source, "public bool QueuePrologueAudioTransition");
            string acquireBody = ExtractMethodBody(source, "private bool TryAcquirePlayerCriticalMutationBuffer");
            string audioTelemetryBody = ExtractMethodBody(source, "private bool RecordAudioSynthesisTelemetry");
            string granularTelemetryBody = ExtractMethodBody(source, "private void RecordGranularTelemetry");
            string prologueTelemetryBody = ExtractMethodBody(source, "private void RecordPrologueTransitionTelemetry");
            string drainBody = ExtractMethodBody(source, "private void DrainPrologueTransitionQueue()");
            string dequeueBody = ExtractMethodBody(source, "private bool TryDequeuePrologueTransitionState");
            string prewarmBody = ExtractMethodBody(source, "private void PrewarmPrologueTransitionQueue()");
            string produceBody = ExtractMethodBody(source, "private void ProduceAudioBlock");
            string canProduceBody = ExtractMethodBody(source, "private bool CanProduceAudioBlock");

            AssertPlayerCriticalSourceAvoidsLegacyWriteLocks(source);
            StringAssert.Contains("PrologueTransitionRingMutationGuardMask", source);
            StringAssert.Contains("PrologueTransitionTelemetryMutationGuardMask", source);
            StringAssert.Contains("GranularTelemetryMutationGuardMask", source);
            StringAssert.Contains("AudioSynthesisTelemetryMutationGuardMask", source);
            StringAssert.Contains("AudioBlockDspMutationGuardMask", source);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(queueBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(acquireBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(audioTelemetryBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(granularTelemetryBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(prologueTelemetryBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(drainBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(dequeueBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(prewarmBody);
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(canProduceBody);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("TryResolveHandle", acquireBody);
            StringAssert.Contains("ReleaseMutationGuard", acquireBody);
            StringAssert.Contains("TryAcquirePlayerCriticalMutationGuard(AudioBlockDspMutationGuardMask", canProduceBody);
            StringAssert.Contains("TryResolveGranularVoiceViews", canProduceBody);
            StringAssert.Contains("TryResolveBinauralFilterViews", canProduceBody);
            StringAssert.Contains("TryResolveReverbViews", canProduceBody);
            StringAssert.Contains("TryResolveTransientDelayViews", canProduceBody);
            StringAssert.Contains("TryResolveFrameScratchViews", canProduceBody);
            StringAssert.Contains("TryResolveSonarTapViews", canProduceBody);
            StringAssert.Contains("TryResolveSonarDspViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireGranularVoiceViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireBinauralFilterViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireReverbViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireTransientDelayViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireFrameScratchViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireSonarTapViews", canProduceBody);
            StringAssert.DoesNotContain("TryAcquireSonarDspViews", canProduceBody);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard", queueBody);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard", audioTelemetryBody);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard", granularTelemetryBody);
            StringAssert.Contains("ReleasePlayerCriticalMutationGuard", prologueTelemetryBody);
            StringAssert.Contains("TryDequeuePrologueTransitionState", drainBody);
            StringAssert.DoesNotContain("RecordPrologueTransitionTelemetry", dequeueBody);
            Assert.Greater(queueBody.IndexOf("PublishAudioParameterSnapshot", StringComparison.Ordinal),
                queueBody.LastIndexOf("ReleasePlayerCriticalMutationGuard", StringComparison.Ordinal));
            Assert.Greater(produceBody.LastIndexOf("RecordAudioSynthesisTelemetry", StringComparison.Ordinal),
                produceBody.LastIndexOf("ReleaseFrameScratchMutationGuard", StringComparison.Ordinal));
            StringAssert.Contains("finally", queueBody);
            StringAssert.Contains("finally", acquireBody);
            StringAssert.Contains("finally", audioTelemetryBody);
            StringAssert.Contains("finally", granularTelemetryBody);
            StringAssert.Contains("finally", prologueTelemetryBody);
            StringAssert.Contains("finally", dequeueBody);
            StringAssert.Contains("finally", prewarmBody);
            StringAssert.Contains("finally", canProduceBody);
        }

        [Test]
        public void VoxelAStarProfileImport_UsesOneMutationGuard()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime_VoxelAStar.cs");
            string source = File.ReadAllText(sourcePath);
            string importBody = ExtractMethodBody(source, "public bool TryLoadVoxelPathingProfiles");

            AssertVoxelAStarSourceAvoidsDataVaultWriteLocks(source);
            StringAssert.Contains("VoxelPathProfileMutationGuardMask", source);
            StringAssert.Contains("TryAcquirePathFunnelMutationGuard", importBody);
            StringAssert.Contains("TryResolveHandle", importBody);
            StringAssert.Contains("ReleasePathFunnelMutationGuard", importBody);
            StringAssert.Contains("finally", importBody);
            Assert.Greater(importBody.IndexOf("TryResolveHandle", StringComparison.Ordinal),
                importBody.IndexOf("TryAcquirePathFunnelMutationGuard", StringComparison.Ordinal));
            StringAssert.Contains("profileCount[0] = written", importBody);
        }

        [Test]
        public void SubmarineFluidDynamics_DoesNotDependOnVocalWarningRuntime()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SubmarineFluidDynamics.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("SignalBus<VocalWarningSignal>.TryPushTracked", source);
            StringAssert.DoesNotContain("_vocalWarningSystem", source);
            StringAssert.DoesNotContain("GlobalRegistry.VocalWarnings", source);
            StringAssert.DoesNotContain(".TryQueueWarning(", source);
        }

        [Test]
        public void CombatTargetSyncPaths_AvoidFullTargetWriteLockBundle()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string source = File.ReadAllText(sourcePath);

            string healthBody = ExtractMethodBody(source, "public static bool SyncTargetHealth");
            string protectionBody = ExtractMethodBody(source, "public static bool SyncTargetProtection");
            string hitProfileBody = ExtractMethodBody(source, "public static bool SyncTargetHitProfile");
            string refreshBody = ExtractMethodBody(source, "private static void RefreshTargetHitProfile");

            StringAssert.DoesNotContain("TryAcquireCombatTargetWriteLocks", healthBody);
            StringAssert.DoesNotContain("TryAcquireCombatTargetWriteLocks", protectionBody);
            StringAssert.DoesNotContain("TryAcquireCombatTargetWriteLocks", hitProfileBody);
            StringAssert.DoesNotContain("TryAcquireCombatTargetWriteLocks", refreshBody);
            StringAssert.Contains("TryResolveCombatTargetHealthOwnerViews", healthBody);
            StringAssert.Contains("TryResolveCombatTargetProtectionOwnerViews", protectionBody);
            StringAssert.Contains("TryResolveCombatTargetHitProfileOwnerViews", hitProfileBody);
        }

        [Test]
        public void CombatReceiverBodyResolution_RequiresCachedBodySource()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "private static Rigidbody ResolveReceiverBody");

            StringAssert.Contains("ICombatPushbackBodySource", methodBody);
            StringAssert.DoesNotContain("TryGetComponent", methodBody);
            StringAssert.DoesNotContain("GetComponent", methodBody);
        }

        [Test]
        public void BallisticsRuntime_AvoidsDataVaultLockLifetimePins()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string frameBody = ExtractMethodBody(source, "public static void FrameTick");
            string mockBody = ExtractMethodBody(source, "public static bool GenerateMockBallistics");
            string csvBody = ExtractMethodBody(source, "public static bool TryLoadPenetrationCsv");
            string completionBody = ExtractMethodBody(source, "private static void FinishScheduledCompletion");

            StringAssert.Contains("OpenVaultLane", frameBody);
            StringAssert.Contains("intersectionJob.Schedule", frameBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", frameBody);
            StringAssert.Contains("DispatcherJobSwap.TryFinalizeCompleted", source);
            StringAssert.Contains("TryAcquireMutationGuard", mockBody);
            StringAssert.Contains("TryAcquireMutationGuard", csvBody);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("_solverBuffersLocked", source);
            StringAssert.DoesNotContain("TryLockSolverBuffers", source);
            StringAssert.DoesNotContain("UnlockSolverBuffers", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", completionBody);
        }

        [Test]
        public void PlayerToolSpawnPresentation_UsesLifecycleCaches()
        {
            string managerPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/PlayerToolManager.cs");
            string managerSource = File.ReadAllText(managerPath);
            string spawnBody = ExtractMethodBody(managerSource, "private void SpawnNewToolImmediate");
            string carrierBody = ExtractMethodBody(managerSource, "private void CacheInteriorCarrierFromContext");

            StringAssert.DoesNotContain("TryGetComponent", spawnBody);
            StringAssert.DoesNotContain("GetComponent", spawnBody);
            StringAssert.Contains("PhysicalToolGripOffsets.TryResolveLastSpawned", spawnBody);
            StringAssert.Contains("PlayerTool.TryResolveLastSpawnedTool", spawnBody);
            StringAssert.Contains("BindSpawnedPresentationContractsCold", spawnBody);
            StringAssert.Contains("PlayerToolSwimContract.TryResolveLastSpawned", spawnBody);
            StringAssert.Contains("PlayerTransportFeelContract.TryResolveLastSpawned", spawnBody);
            StringAssert.DoesNotContain("TryGetComponent", carrierBody);
            StringAssert.Contains("HullRigidbody", carrierBody);

            string toolPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/PlayerTool.cs");
            string toolSource = File.ReadAllText(toolPath);
            StringAssert.Contains("s_lastSpawnedTool = this", ExtractMethodBody(toolSource, "public virtual void OnSpawn()"));
            StringAssert.Contains("TryResolveLastSpawnedTool", toolSource);
            StringAssert.DoesNotContain("TryGetComponent", ExtractMethodBody(toolSource, "public virtual void OnSpawn()"));

            string gripPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Interaction/PhysicalToolGripOffsets.cs");
            string gripSource = File.ReadAllText(gripPath);
            StringAssert.Contains("MonoBehaviour, IPoolable", gripSource);
            StringAssert.Contains("s_lastSpawnedOffsets = this", ExtractMethodBody(gripSource, "public void OnSpawn()"));
            StringAssert.Contains("TryResolveLastSpawned", gripSource);

            string swimPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/PlayerToolSwimContract.cs");
            string swimSource = File.ReadAllText(swimPath);
            StringAssert.Contains("MonoBehaviour, IPoolable", swimSource);
            StringAssert.Contains("s_lastSpawnedContract = this", ExtractMethodBody(swimSource, "public void OnSpawn()"));

            string feelPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/PlayerTransportFeelContract.cs");
            string feelSource = File.ReadAllText(feelPath);
            StringAssert.Contains("MonoBehaviour, IPoolable", feelSource);
            StringAssert.Contains("s_lastSpawnedContract = this", ExtractMethodBody(feelSource, "public void OnSpawn()"));
        }

        [Test]
        public void ParasiteTelemetryOwnerViews_AvoidDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string lateFrameBody = ExtractMethodBody(source, "public void LateFrameTick()");
            string targetBody = ExtractMethodBody(source, "private bool TryResolveTargetOwnerViews");
            string telemetryBody = ExtractMethodBody(source, "private bool TryResolveTelemetryOwnerViews");

            StringAssert.Contains("TryResolveTelemetryOwnerViews", lateFrameBody);
            StringAssert.Contains("TryResolveTargetOwnerViews", source);
            StringAssert.DoesNotContain("TryAcquireTargetWriteBuffers", source);
            StringAssert.DoesNotContain("TryAcquireTelemetryWriteBuffers", source);
            AssertOwnerViewUsesResolveHandleOnly(targetBody);
            AssertOwnerViewUsesResolveHandleOnly(telemetryBody);
        }

        [Test]
        public void ParasiteProfileCsvLoader_AvoidsNestedDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/Parasites/ParasiteSwarmContracts.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "public static int LoadProfilesFromCsv");

            StringAssert.Contains("TryResolveHandle", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
        }

        [Test]
        public void BatteryChargerProfileCsv_AvoidsNestedBufferLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs");
            string source = File.ReadAllText(sourcePath);
            string methodBody = ExtractMethodBody(source, "private void MonitorProfileCsv()");

            StringAssert.Contains("Resolve(in _handles.CsvScratch", methodBody);
            StringAssert.Contains("Resolve(in _handles.Profiles", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
        }

        [Test]
        public void CombatNarrowOwnerViews_AvoidDataVaultWriteLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_VaultViews.cs");
            string source = File.ReadAllText(sourcePath);

            string healthBody = ExtractMethodBody(source, "private static bool TryResolveCombatTargetHealthOwnerViews");
            string protectionBody = ExtractMethodBody(source, "private static bool TryResolveCombatTargetProtectionOwnerViews");
            string hitProfileBody = ExtractMethodBody(source, "private static bool TryResolveCombatTargetHitProfileOwnerViews");
            string lookupClearBody = ExtractMethodBody(source, "private static bool TryClearCombatTargetLookupOwnerView");
            string telemetryBody = ExtractMethodBody(source, "private static bool TryResolveCombatTelemetryOwnerViews");

            AssertOwnerViewUsesResolveHandleOnly(healthBody);
            AssertOwnerViewUsesResolveHandleOnly(protectionBody);
            AssertOwnerViewUsesResolveHandleOnly(hitProfileBody);
            AssertOwnerViewUsesResolveHandleOnly(lookupClearBody);
            AssertOwnerViewUsesResolveHandleOnly(telemetryBody);
        }

        [Test]
        public void CombatStructuralTargetMutation_UsesOwnerViewsInsteadOfNestedLocks()
        {
            string combatPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string combatSource = File.ReadAllText(combatPath);
            string registerBody = ExtractMethodBody(combatSource, "public static bool RegisterTarget(");
            string unregisterBody = ExtractMethodBody(combatSource, "public static bool UnregisterTarget(");
            string protectionBody = ExtractMethodBody(combatSource, "public static bool SyncTargetProtection(");

            StringAssert.Contains("TryResolveCombatTargetOwnerViews", registerBody);
            StringAssert.Contains("TryResolveStatusEffectStatesOwnerView", registerBody);
            StringAssert.Contains("TryResolveArmorTargetOwnerViews", registerBody);
            StringAssert.Contains("TryResolveCombatTargetOwnerViews", unregisterBody);
            StringAssert.Contains("TryResolveStatusEffectStatesOwnerView", unregisterBody);
            StringAssert.Contains("TryResolveArmorTargetOwnerViews", unregisterBody);
            StringAssert.Contains("TryResolveArmorTargetOwnerViews", protectionBody);
            AssertCombatStructuralBodyAvoidsWriteLocks(registerBody);
            AssertCombatStructuralBodyAvoidsWriteLocks(unregisterBody);
            AssertCombatStructuralBodyAvoidsWriteLocks(protectionBody);

            string vaultPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_VaultViews.cs");
            string vaultSource = File.ReadAllText(vaultPath);
            AssertOwnerViewUsesResolveHandleOnly(ExtractMethodBody(vaultSource, "private static bool TryResolveCombatTargetOwnerViews"));

            string armorPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs");
            string armorSource = File.ReadAllText(armorPath);
            AssertOwnerViewUsesResolveHandleOnly(ExtractMethodBody(armorSource, "private static bool TryResolveArmorTargetOwnerViews"));

            string statusPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs");
            string statusSource = File.ReadAllText(statusPath);
            AssertOwnerViewUsesResolveHandleOnly(ExtractMethodBody(statusSource, "private static bool TryResolveStatusEffectStatesOwnerView"));
        }

        [Test]
        public void CombatDamageIngress_UsesOwnerViewsInsteadOfWriterLocks()
        {
            string combatPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string combatSource = File.ReadAllText(combatPath);
            string queueBody = ExtractMethodBody(combatSource, "public static bool TryQueueDamage(in CombatDamageRequest signal, in CombatDamageSignalDetail detail, double3 impactAup)");
            string ingressBody = ExtractMethodBody(combatSource, "private static bool TryResolveDamageIngressOwnerViews");

            StringAssert.Contains("TryResolveDamageIngressOwnerViews", queueBody);
            StringAssert.DoesNotContain("TryAcquireDamageIngressWriteLocks", combatSource);
            StringAssert.DoesNotContain("ReleaseDamageIngressWriteLocks", combatSource);
            AssertCombatStructuralBodyAvoidsWriteLocks(queueBody);
            AssertOwnerViewUsesResolveHandleOnly(ingressBody);
        }

        [Test]
        public void CombatStatusColdAndIngressRoutes_UseOwnerViewsInsteadOfWriterLocks()
        {
            string statusPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs");
            string statusSource = File.ReadAllText(statusPath);

            string queueBody = ExtractMethodBody(statusSource, "public static bool TryQueueStatusEffect");
            string writeTuningBody = ExtractMethodBody(statusSource, "private static bool TryWriteStatusEffectTuningOwnerView");
            string counterBody = ExtractMethodBody(statusSource, "private static void WriteStatusCounter(int index, int value)");

            StringAssert.Contains("TryResolveHandle", queueBody);
            AssertCombatStructuralBodyAvoidsWriteLocks(queueBody);
            AssertOwnerViewUsesResolveHandleOnly(writeTuningBody);
            AssertOwnerViewUsesResolveHandleOnly(counterBody);
            StringAssert.DoesNotContain("TryWriteStatusEffectTuningLocked", statusSource);
            StringAssert.DoesNotContain("TryAcquireStatusEffectStatesWriteLock", statusSource);
        }

        [Test]
        public void ArmorPenetrationColdRoutes_UseOwnerViewsInsteadOfWriterLocks()
        {
            string armorPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs");
            string armorSource = File.ReadAllText(armorPath);

            AssertOwnerViewUsesResolveHandleOnly(ExtractMethodBody(armorSource, "private static bool TryWriteDefaultArmorTuning"));
            AssertOwnerViewUsesResolveHandleOnly(ExtractMethodBody(armorSource, "public static bool WriteArmorTuning"));

            string csvBody = ExtractMethodBody(armorSource, "public static unsafe bool ApplyArmorProfilesCsvBytes");
            StringAssert.Contains("TryResolveHandle", csvBody);
            AssertCombatStructuralBodyAvoidsWriteLocks(csvBody);
        }

        [Test]
        public void CombatScheduledJobs_UseMutationGuardLeasesInsteadOfDataVaultLocks()
        {
            string combatPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string combatSource = File.ReadAllText(combatPath);
            string frameBody = ExtractMethodBody(combatSource, "public static void FrameTick(float deltaTime)");
            string lateFrameBody = ExtractMethodBody(combatSource, "public static void LateFrameTick()");
            string clearCountersBody = ExtractMethodBody(combatSource, "private static void ClearCounters()");

            string vaultPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_VaultViews.cs");
            string vaultSource = File.ReadAllText(vaultPath);

            string statusPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs");
            string statusSource = File.ReadAllText(statusPath);
            string statusScheduleBody = ExtractMethodBody(statusSource, "private static bool TryScheduleStatusEffectJobs");
            string statusCompleteBody = ExtractMethodBody(statusSource, "private static void CompleteStatusEffectFrame()");

            string armorPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs");
            string armorSource = File.ReadAllText(armorPath);
            string armorCompleteBody = ExtractMethodBody(armorSource, "private static void FinishArmorPenetrationScheduledCompletion()");

            AssertCombatSourceAvoidsDataVaultLocks(combatSource);
            AssertCombatSourceAvoidsDataVaultLocks(vaultSource);
            AssertCombatSourceAvoidsDataVaultLocks(statusSource);
            AssertCombatSourceAvoidsDataVaultLocks(armorSource);

            StringAssert.Contains("TryAcquireDamageJobMutationGuardLease", frameBody);
            StringAssert.Contains("TryAcquireCombatDispatchMutationGuardLease", lateFrameBody);
            StringAssert.Contains("TryAcquireCombatCounterMutationGuardLease", clearCountersBody);
            StringAssert.Contains("TryAcquireStatusEffectJobMutationGuardLease", statusScheduleBody);
            StringAssert.Contains("_statusJobMutationGuardLease.Release", statusCompleteBody);
            StringAssert.Contains("_damageJobMutationGuardLease.Release", armorCompleteBody);
            StringAssert.Contains("CombatVaultMutationGuardLease", vaultSource);
            StringAssert.Contains("TryAcquireMutationGuard", vaultSource);
            StringAssert.Contains("ReleaseMutationGuard", vaultSource);
            StringAssert.Contains("ArmorMockMutationGuardMask", armorSource);
            StringAssert.Contains("ArmorEvaluatorTortureMutationGuardMask", armorSource);
            StringAssert.Contains("ArmorCasTortureMutationGuardMask", armorSource);
        }

        [Test]
        public void ScannerDataMiningRouter_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string scannerPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs");
            string scannerSource = File.ReadAllText(scannerPath);
            string fastTickBody = ExtractMethodBody(scannerSource, "public void FastTick(float deltaTime)");
            string finalizeBody = ExtractMethodBody(scannerSource, "private bool TryFinalizeScheduledQuery()");
            string processBody = ExtractMethodBody(scannerSource, "private void ProcessCompletedQuery(float deltaTime)");

            AssertScannerSourceAvoidsDataVaultLocks(scannerSource);
            StringAssert.Contains("ScannerQueryMutationGuardMask", scannerSource);
            StringAssert.Contains("ScannerCompletionMutationGuardMask", scannerSource);
            StringAssert.Contains("ScannerMutationGuardBit", scannerSource);
            StringAssert.Contains("TryAcquireQueryMutationGuard", fastTickBody);
            StringAssert.Contains("TryReadVaultViews(out views)", fastTickBody);
            StringAssert.Contains("ReleaseQueryMutationGuard", finalizeBody);
            StringAssert.Contains("try", finalizeBody);
            StringAssert.Contains("finally", finalizeBody);
            StringAssert.Contains("TryAcquireScannerMutationGuard", processBody);
            StringAssert.Contains("ReleaseScannerMutationGuard", processBody);
            StringAssert.Contains("finally", processBody);
        }

        [Test]
        public void KineticCharacterAnimatorSolver_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string kineticPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs");
            string kineticSource = File.ReadAllText(kineticPath);
            string tickBody = ExtractMethodBody(kineticSource, "public void Tick(float deltaTime)");
            string writeInputBody = ExtractMethodBody(kineticSource, "private bool WriteFrameInput");
            string finishBody = ExtractMethodBody(kineticSource, "private bool FinishPendingSolverCompletion()");
            string acquireBody = ExtractMethodBody(kineticSource, "private bool TryAcquireSolverMutationGuard(");

            AssertKineticSourceAvoidsDataVaultLocks(kineticSource);
            StringAssert.Contains("SolverMutationGuardRequiredMask", kineticSource);
            StringAssert.Contains("SolverPlayerStateReadGuardMask", kineticSource);
            StringAssert.Contains("SolverSdfMutationGuardMask", kineticSource);
            StringAssert.Contains("SolverPlayerHandIkMutationGuardMask", kineticSource);
            StringAssert.Contains("TryAcquireSolverMutationGuard(vault, ref includePlayerState, ref includeSdf, ref includePlayerHandIk)", tickBody);
            StringAssert.Contains("TryResolveRuntimeBuffers", tickBody);
            StringAssert.Contains("WriteFrameInput(vault, inputs, includePlayerState", tickBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", tickBody);
            StringAssert.Contains("ReleaseSolverMutationGuard", tickBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", writeInputBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", writeInputBody);
            StringAssert.Contains("ReleaseSolverMutationGuard", finishBody);
            StringAssert.Contains("finally", finishBody);
            StringAssert.Contains("includePlayerState = false", acquireBody);
            StringAssert.Contains("includeSdf = false", acquireBody);
            StringAssert.Contains("includePlayerHandIk = false", acquireBody);
        }

        [Test]
        public void ProceduralLadderClimbRuntime_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string ladderPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs");
            string ladderSource = File.ReadAllText(ladderPath);
            string fastTickBody = ExtractMethodBody(ladderSource, "public void FastTick(float deltaTime)");
            string lateFrameBody = ExtractMethodBody(ladderSource, "public void LateFrameTick()");
            string scheduleBody = ExtractMethodBody(ladderSource, "private void ScheduleSolve()");
            string writeAupBody = ExtractMethodBody(ladderSource, "private bool TryWriteLadderAup");
            string acquireBody = ExtractMethodBody(ladderSource, "private bool TryAcquireSolveMutationGuard");
            string completeBody = ExtractMethodBody(ladderSource, "private void CompleteOutstandingJobForBarrier()");

            AssertLadderClimbSourceAvoidsDataVaultLocks(ladderSource);
            AssertHotMethodAvoidsLookupAndLocks(fastTickBody);
            AssertHotMethodAvoidsLookupAndLocks(lateFrameBody);
            AssertHotMethodAvoidsLookupAndLocks(scheduleBody);
            StringAssert.Contains("SolveMutationGuardMask", ladderSource);
            StringAssert.Contains("LadderAupMutationGuardMask", ladderSource);
            StringAssert.Contains("TryAcquireSolveMutationGuard", scheduleBody);
            StringAssert.Contains("views.Inputs[0]", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("ReleaseSolveMutationGuard", scheduleBody);
            StringAssert.Contains("ReleaseSolveMutationGuard", lateFrameBody);
            StringAssert.Contains("ReleaseSolveMutationGuard", completeBody);
            StringAssert.Contains("finally", scheduleBody);
            StringAssert.Contains("finally", lateFrameBody);
            StringAssert.Contains("finally", completeBody);
            StringAssert.Contains("TryAcquireMutationGuard(LadderAupMutationGuardMask)", writeAupBody);
            StringAssert.Contains("ReleaseMutationGuard(LadderAupMutationGuardMask)", writeAupBody);
            StringAssert.Contains("TryResolveVaultViews(vault, out views)", acquireBody);
        }

        [Test]
        public void ProceduralBoneBlenderRuntime_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string blenderPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderRuntime.cs");
            string blenderSource = File.ReadAllText(blenderPath);
            string tickBody = ExtractMethodBody(blenderSource, "public void Tick(float deltaTime)");
            string lateFrameBody = ExtractMethodBody(blenderSource, "public void LateFrameTick()");
            string acquireBody = ExtractMethodBody(blenderSource, "private bool TryAcquireJobMutationGuardAndResolveBuffers");
            string finishBody = ExtractMethodBody(blenderSource, "private bool FinishPendingSolverCompletion()");

            AssertProceduralBoneBlenderSourceAvoidsDataVaultLocks(blenderSource);
            AssertHotMethodAvoidsLookupAndLocks(tickBody);
            AssertHotMethodAvoidsLookupAndLocks(lateFrameBody);
            AssertHotMethodAvoidsLookupAndLocks(finishBody);
            StringAssert.Contains("TryAcquireMutationGuard(tuningGuardMask)", tickBody);
            StringAssert.Contains("TryAcquireJobMutationGuardAndResolveBuffers", tickBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", tickBody);
            StringAssert.Contains("ReleaseJobMutationGuard", tickBody);
            StringAssert.Contains("ReleaseJobMutationGuard", finishBody);
            StringAssert.Contains("finally", tickBody);
            StringAssert.Contains("finally", finishBody);
            StringAssert.Contains("TryResolveRuntimeBuffers", acquireBody);
            StringAssert.Contains("TryAcquireMutationGuard", acquireBody);
            StringAssert.Contains("ReleaseMutationGuard", acquireBody);
        }

        [Test]
        public void SumpPumpPipeGridRuntime_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string sumpPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs");
            string sumpSource = File.ReadAllText(sumpPath);
            string slowTickBody = ExtractMethodBody(sumpSource, "public void SlowTick()");
            string lateFrameBody = ExtractMethodBody(sumpSource, "public void LateFrameTick()");
            string scheduleBody = ExtractMethodBody(sumpSource, "private bool ScheduleDrainageSolve");
            string completeSolverBody = ExtractMethodBody(sumpSource, "private void CompleteScheduledSolverForTeardown()");
            string profileCsvBody = ExtractMethodBody(sumpSource, "public bool TryLoadPipeProfilesFromCsv");
            string initTuningBody = ExtractMethodBody(sumpSource, "private void InitializeTuningIfNeeded()");
            string writeTuningBody = ExtractMethodBody(sumpSource, "private bool TryWriteTuning");

            AssertSumpPumpSourceAvoidsDataVaultLocks(sumpSource);
            AssertHotMethodAvoidsLookupAndLocks(slowTickBody);
            AssertHotMethodAvoidsLookupAndLocks(lateFrameBody);
            AssertHotMethodAvoidsLookupAndLocks(scheduleBody);
            StringAssert.Contains("DrainageVaultMutationGuardMask", sumpSource);
            StringAssert.DoesNotContain("Generate" + "Mock" + "PipeNetworkJob", sumpSource);
            StringAssert.DoesNotContain("Generate" + "Mock" + "DrainageNetwork", sumpSource);
            StringAssert.DoesNotContain("TryGenerate" + "Mock" + "DrainageNetwork", sumpSource);
            StringAssert.DoesNotContain("TryFinalize" + "Mock" + "SeedNoWait", sumpSource);
            StringAssert.DoesNotContain("Complete" + "Mock" + "SeedForTeardown", sumpSource);
            StringAssert.DoesNotContain("_" + "mockSeed", sumpSource);
            StringAssert.Contains("RecordTopologyUnavailable", scheduleBody);
            StringAssert.Contains("SumpDrainageTelemetryFlags.TopologyInvalid", sumpSource);
            StringAssert.Contains("TryAcquireDrainageMutationGuard", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("ReleaseDrainageMutationGuard", lateFrameBody);
            StringAssert.Contains("ReleaseDrainageMutationGuard", completeSolverBody);
            StringAssert.Contains("finally", lateFrameBody);
            StringAssert.Contains("finally", completeSolverBody);
            StringAssert.Contains("TryAcquireLocalDrainageMutationGuard", profileCsvBody);
            StringAssert.Contains("ReleaseLocalDrainageMutationGuard", profileCsvBody);
            StringAssert.Contains("TryAcquireLocalDrainageMutationGuard", initTuningBody);
            StringAssert.Contains("ReleaseLocalDrainageMutationGuard", initTuningBody);
            StringAssert.Contains("TryAcquireLocalDrainageMutationGuard", writeTuningBody);
            StringAssert.Contains("ReleaseLocalDrainageMutationGuard", writeTuningBody);
        }

        [Test]
        public void ModularBaseConstructionValidator_UsesTerrainSamplerWithoutSyntheticBoundsSeed()
        {
            string root = Path.Combine(
                Application.dataPath,
                "_Project/Scripts");
            string validatorSource = File.ReadAllText(Path.Combine(root, "Construction/ModularBaseConstructionValidator.cs"));
            string playerBuilderSource = File.ReadAllText(Path.Combine(root, "PlayerBuilder.cs"));
            string tunerSource = File.ReadAllText(Path.Combine(root, "Editor/WfcBuilderTunerWindow.cs"));

            StringAssert.DoesNotContain("Mock" + "WorldSampler", validatorSource);
            StringAssert.DoesNotContain("Create" + "Mock" + "WorldSampler", validatorSource);
            StringAssert.DoesNotContain("Generate" + "Emergency" + "MockBounds", validatorSource);
            StringAssert.DoesNotContain("Emergency" + "MockBounds" + "Count", validatorSource);
            StringAssert.DoesNotContain("Mock" + "WorldSampler", playerBuilderSource);
            StringAssert.DoesNotContain("Mock" + "WorldSampler", tunerSource);
            StringAssert.Contains("ConstructionTerrainSampler", validatorSource);
            StringAssert.Contains("CreateTerrainSampler", playerBuilderSource);
            StringAssert.Contains("EnsureBoundsOverrideBuffer(vault, out _);", validatorSource);
        }

        [Test]
        public void ConstructionTransactionKernels_DoNotExposeSyntheticSeedJobs()
        {
            string root = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Construction");
            string deconstructionSource = File.ReadAllText(Path.Combine(root, "HabitatDeconstructionTransactionKernel.cs"));
            string droneTransactionSource = File.ReadAllText(Path.Combine(root, "DroneFleetTransactionKernel.cs"));

            StringAssert.DoesNotContain("Generate" + "Mock" + "DeconstructionDataJob", deconstructionSource);
            StringAssert.DoesNotContain("Generate" + "Mock" + "DroneTransactionsJob", droneTransactionSource);
            StringAssert.Contains("EvaluateDroneTransactionsJob", droneTransactionSource);
            StringAssert.Contains("ExecuteModuleTeardownJob", deconstructionSource);
        }

        [Test]
        public void HabitatFluidIncursionDirector_UsesMutationGuardsInsteadOfDataVaultLocks()
        {
            string fluidPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs");
            string fluidSource = File.ReadAllText(fluidPath);
            string fixedTickBody = ExtractMethodBody(fluidSource, "public void FixedTick(float fixedDeltaTime)");
            string postFixedTickBody = ExtractMethodBody(fluidSource, "public void PostFixedTick(float fixedDeltaTime)");
            string completeBody = ExtractMethodBody(fluidSource, "private void CompleteScheduledSimulationForAuthoritativeWrite()");
            string breachBody = ExtractMethodBody(fluidSource, "public bool GenerateMockHullBreach");
            string floodBody = ExtractMethodBody(fluidSource, "public bool GenerateMockFloodDistribution");

            AssertHabitatFluidSourceAvoidsDataVaultLocks(fluidSource);
            AssertHotMethodAvoidsLookupAndLocks(fixedTickBody);
            AssertHotMethodAvoidsLookupAndLocks(postFixedTickBody);
            StringAssert.Contains("FluidSimulationMutationGuardMask", fluidSource);
            StringAssert.Contains("TryAcquireFluidSimulationMutationGuard", fixedTickBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", fixedTickBody);
            StringAssert.Contains("ReleaseFluidSimulationMutationGuard", fixedTickBody);
            StringAssert.Contains("ReleaseFluidSimulationMutationGuard", postFixedTickBody);
            StringAssert.Contains("ReleaseFluidSimulationMutationGuard", completeBody);
            StringAssert.Contains("finally", fixedTickBody);
            StringAssert.Contains("finally", postFixedTickBody);
            StringAssert.Contains("finally", completeBody);
            StringAssert.Contains("TryAcquireLocalFluidMutationGuard", fluidSource);
            StringAssert.Contains("ReleaseLocalFluidMutationGuard", breachBody);
            StringAssert.Contains("ReleaseLocalFluidMutationGuard", floodBody);
            StringAssert.Contains("finally", breachBody);
            StringAssert.Contains("finally", floodBody);
        }

        [Test]
        public void ShinobuPlasmaBeamRuntime_UsesMutationGuardInsteadOfDataVaultLocks()
        {
            string beamPath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs");
            string beamSource = File.ReadAllText(beamPath);
            string scheduleBody = ExtractMethodBody(beamSource, "private JobHandle ScheduleSimulation");
            string postBody = ExtractMethodBody(beamSource, "private void PostSimulationTick");
            string completeBody = ExtractMethodBody(beamSource, "private void CompleteSimulationForLifecycle()");

            AssertPlasmaBeamSourceAvoidsDataVaultLocks(beamSource);
            AssertHotMethodAvoidsLookupAndLocks(scheduleBody);
            AssertHotMethodAvoidsLookupAndLocks(postBody);
            StringAssert.Contains("PlasmaBeamJobMutationGuardMask", beamSource);
            StringAssert.Contains("TryAcquirePlasmaBeamJobMutationGuard", scheduleBody);
            StringAssert.Contains("H8Memory.RegisterActiveJob", scheduleBody);
            StringAssert.Contains("ReleasePlasmaBeamJobMutationGuard", scheduleBody);
            StringAssert.Contains("ReleasePlasmaBeamJobMutationGuard", postBody);
            StringAssert.Contains("ReleasePlasmaBeamJobMutationGuard", completeBody);
            StringAssert.Contains("finally", scheduleBody);
            StringAssert.Contains("finally", postBody);
            StringAssert.Contains("finally", completeBody);
        }

        [Test]
        public void CameraJuiceTelemetry_UsesOwnerViewInsteadOfLateFrameWriteLock()
        {
            string cameraJuicePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/VFX/CameraJuiceSystem.cs");
            string cameraJuiceSource = File.ReadAllText(cameraJuicePath);
            string lateFrameBody = ExtractMethodBody(cameraJuiceSource, "public void LateFrameTick()");
            string recordBody = ExtractMethodBody(cameraJuiceSource, "private void RecordCameraJuiceTelemetry()");
            string resolveBody = ExtractMethodBody(cameraJuiceSource, "private static bool TryResolveCameraJuiceTelemetryWriteBuffer");

            AssertCameraJuiceSourceAvoidsTelemetryWriteLocks(cameraJuiceSource);
            AssertHotMethodAvoidsLookupAndLocks(lateFrameBody);
            AssertHotMethodAvoidsLookupAndLocks(recordBody);
            AssertOwnerViewUsesResolveHandleOnly(resolveBody);
            StringAssert.Contains("RecordCameraJuiceTelemetry", lateFrameBody);
            StringAssert.Contains("TryResolveCameraJuiceTelemetryWriteBuffer", recordBody);
        }

        private static void AssertUnmanagedSignalPayload<T>()
            where T : unmanaged, ISignal
        {
            Assert.IsFalse(
                RuntimeHelpers.IsReferenceOrContainsReferences<T>(),
                typeof(T).FullName);
        }

        private static void AssertOwnerViewUsesResolveHandleOnly(string methodBody)
        {
            StringAssert.Contains("TryResolveHandle", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
        }

        private static void AssertCombatStructuralBodyAvoidsWriteLocks(string methodBody)
        {
            StringAssert.DoesNotContain("TryAcquireCombatTargetWriteLocks", methodBody);
            StringAssert.DoesNotContain("ReleaseCombatTargetWriteLocks", methodBody);
            StringAssert.DoesNotContain("TryAcquireArmorTargetWriteLocks", methodBody);
            StringAssert.DoesNotContain("ReleaseArmorTargetWriteLocks", methodBody);
            StringAssert.DoesNotContain("TryAcquireStatusEffectStatesWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseStatusEffectStatesWriteLock", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", methodBody);
        }

        private static void AssertCombatSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockCombatDamageVaultBuffersForJobs", source);
            StringAssert.DoesNotContain("UnlockCombatDamageVaultBuffersForJobs", source);
            StringAssert.DoesNotContain("TryLockStatusEffectVaultBuffersForJobs", source);
            StringAssert.DoesNotContain("UnlockStatusEffectVaultBuffersForJobs", source);
            StringAssert.DoesNotContain("TryLockArmorVaultBuffersForJobs", source);
            StringAssert.DoesNotContain("UnlockArmorVaultBuffersForJobs", source);
        }

        private static void AssertScannerSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockQueryBuffers", source);
            StringAssert.DoesNotContain("UnlockQueryBuffers", source);
            StringAssert.DoesNotContain("TryLockCompletionBuffers", source);
            StringAssert.DoesNotContain("UnlockCompletionBuffers", source);
            StringAssert.DoesNotContain("_queryBuffersLocked", source);
            StringAssert.DoesNotContain("_completionBuffersLocked", source);
        }

        private static void AssertKineticSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryAcquireWriteView", source);
            StringAssert.DoesNotContain("ReleaseWriteView", source);
            StringAssert.DoesNotContain("_lockedBuffers", source);
        }

        private static void AssertProceduralBoneBlenderSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryAcquireWriteView", source);
            StringAssert.DoesNotContain("ReleaseWriteView", source);
            StringAssert.DoesNotContain("_lockedBuffers", source);
        }

        private static void AssertLadderClimbSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryAcquireWriteView", source);
            StringAssert.DoesNotContain("ReleaseWriteView", source);
            StringAssert.DoesNotContain("TryPinSolveBuffers", source);
            StringAssert.DoesNotContain("ReleaseSolveBufferPins", source);
            StringAssert.DoesNotContain("_solveBufferPinMask", source);
            StringAssert.DoesNotContain("_solveBufferGuardVault", source);
        }

        private static void AssertSumpPumpSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("TryLockTelemetryWriteBuffers", source);
            StringAssert.DoesNotContain("_activeVaultGuardMask", source);
        }

        private static void AssertHabitatFluidSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("_lockedBufferMask", source);
        }

        private static void AssertPlasmaBeamSourceAvoidsDataVaultLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockJobBuffers", source);
            StringAssert.DoesNotContain("UnlockJobBuffers", source);
            StringAssert.DoesNotContain("_lockedBufferMask", source);
        }

        private static void AssertCameraJuiceSourceAvoidsTelemetryWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireCameraJuiceTelemetryWriteBuffer", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertBabelSubtitleSourceAvoidsDataVaultWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireCueWriteBuffer", source);
            StringAssert.DoesNotContain("TryAcquireTelemetryWriteBuffer", source);
            StringAssert.DoesNotContain("TryAcquireUIOptimizationTelemetryWriteBuffer", source);
            StringAssert.DoesNotContain("TryAcquireSubtitleWriteBuffer", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertAudioLogSourceAvoidsDataVaultWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireVaultWrite", source);
            StringAssert.DoesNotContain("ReleaseVaultWrite", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertNativeAudioFrameRingBufferSourceAvoidsTelemetryWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireTelemetryWriteView", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertDynamicMusicSourceAvoidsDataVaultWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireLockedView", source);
            StringAssert.DoesNotContain("ReleaseDynamicMusicWriteLocks", source);
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryAcquireAudioWriteBuffer", source);
            StringAssert.DoesNotContain("ReleaseAudioWriteBuffer", source);
        }

        private static void AssertPlayerCriticalSourceAvoidsLegacyWriteLocks(string source)
        {
            AssertPlayerCriticalBodyAvoidsLegacyWriteLocks(source);
            StringAssert.DoesNotContain("WriteLocks", source);
        }

        private static void AssertVoxelAStarSourceAvoidsDataVaultWriteLocks(string source)
        {
            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
        }

        private static void AssertHotMethodAvoidsLookupAndLocks(string methodBody)
        {
            StringAssert.DoesNotContain("GlobalRegistry.Get", methodBody);
            StringAssert.DoesNotContain("GetComponent", methodBody);
            StringAssert.DoesNotContain("TryGetComponent", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", methodBody);
        }

        private static void AssertBiolumRouteUsesOwnerViewsOnly(string methodBody)
        {
            StringAssert.Contains("TryResolveBiolumVaultBuffer", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
        }

        private static void AssertGlitchRouteUsesOwnerViewsOnly(string methodBody)
        {
            StringAssert.Contains("TryResolveGlitchVaultBuffer", methodBody);
            StringAssert.DoesNotContain("TryAcquireGlitchVaultWriteBuffer", methodBody);
            StringAssert.DoesNotContain("ReleaseGlitchVaultWriteBuffer", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
            StringAssert.DoesNotContain("TryUnlockBuffer", methodBody);
        }

        private static Type[] GetLoadableTypes(global::System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return Array.FindAll(ex.Types, type => type != null);
            }
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int methodStart = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);

            int braceStart = source.IndexOf('{', methodStart);
            Assert.Greater(braceStart, methodStart);

            int depth = 0;
            for (int index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                    continue;
                }

                if (source[index] != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(braceStart, index - braceStart + 1);
            }

            Assert.Fail("Method body was not closed.");
            return string.Empty;
        }

        private sealed class AudioSwapProbe : IGlobalRegistryHotSwapListener
        {
            public AudioSwapProbe(IAudioService audioService)
            {
                AudioService = audioService;
            }

            public IAudioService AudioService { get; private set; }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                    AudioService = currentService as IAudioService;
            }
        }

        private sealed class DummyAudioService : IAudioService
        {
            private readonly int _id;

            public DummyAudioService(int id)
            {
                _id = id;
            }

            public int TickCount => _id;
            public bool IsInitialized => true;
            public AudioMixerGroup InterfaceGroup => null;
            public AudioMixerGroup AmbientGroup => null;

            public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
            {
            }

            public void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
            {
            }

            public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal) => true;

            public bool QueueHullStressSignal(in HullStressSignal signal) => true;

            public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal) => true;

            public bool QueueAudioEvent(in Hecton8.Core.AudioEvent audioEvent) => true;

            public bool QueuePrologueAudioTransition(in Hecton8.Core.AudioTransitionState state) => true;

            public void PlayStatic2D(AudioClip clip, float volume = 1f)
            {
            }

            public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
            {
            }

            public bool TryGetAcousticRadarPayload(out NativeArray<float>.ReadOnly radialIntensityBins, out int radialResolution)
            {
                radialIntensityBins = default;
                radialResolution = 0;
                return false;
            }

            public bool TryUploadAcousticRadarPayload(Texture2D destination, out int uploadedSampleCount, out float peakIntensity)
            {
                uploadedSampleCount = 0;
                peakIntensity = 0f;
                return false;
            }

            public bool TryGetAcousticRadarGridPayload(
                out NativeArray<float>.ReadOnly energyGrid,
                out int azimuthBins,
                out int elevationBins,
                out GraphicsBuffer gridBuffer)
            {
                energyGrid = default;
                azimuthBins = 0;
                elevationBins = 0;
                gridBuffer = null;
                return false;
            }

            public bool TryEmitModAcousticPing(Vector3 runtimePosition, float intensity01) => false;

            public void StopAll()
            {
            }
        }
    }
}
