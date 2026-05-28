using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hecton8.Audio;
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
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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
            string methodBody = ExtractMethodBody(source, "private bool TryResolveVwsOwnerViews");

            StringAssert.Contains("TryResolveHandle", methodBody);
            StringAssert.DoesNotContain("TryAcquireWriteLock", methodBody);
            StringAssert.DoesNotContain("ReleaseWriteLock", methodBody);
            StringAssert.DoesNotContain("TryLockBuffer", methodBody);
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

        private static Type[] GetLoadableTypes(Assembly assembly)
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
