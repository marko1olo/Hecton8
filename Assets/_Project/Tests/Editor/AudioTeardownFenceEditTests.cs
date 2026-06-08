using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AudioTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs",
            "private void CompletePendingVocalWarningJobsForTeardown()",
            "DispatcherJobFence.TryComplete(ref handle, forceComplete: true)",
            "_pendingVocalWarningJobHandle = default;")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs",
            "private void ForceFlushSynthJobForShutdown()",
            "DispatcherJobFence.TryComplete(ref _synthJobHandle, forceComplete: true)",
            "Volatile.Write(ref _synthJobPending, 0);")]
        public void AudioTeardownForceCompletesInsideLateFrameSwapWindow(
            string relativePath,
            string signature,
            string completeCall,
            string releaseSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            Assert.That(method, Does.Contain("DispatcherJobFence.BeginLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain("DispatcherJobFence.EndLateFrameSwapWindow();"));
            Assert.That(method, Does.Contain(releaseSignal));

            Assert.Less(
                method.IndexOf("DispatcherJobFence.BeginLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(completeCall, StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf(completeCall, StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.EndLateFrameSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(releaseSignal, StringComparison.Ordinal));
        }

        [Test]
        public void VocalWarningSignalProducerDiagnosticsResetOnSubsystemRegistration()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Audio/VocalWarningSystem.cs");
            string reset = ExtractMethodBlock(source, "private static void ResetStaticSignalDiagnostics()");

            Assert.That(source, Does.Contain("internal static int SignalPushDropCount =>"));
            Assert.That(source, Does.Contain("Volatile.Read(ref s_x001DirectSignalPushDropCount_VocalWarningSystem)"));
            Assert.That(source, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]"));
            Assert.That(reset, Does.Contain("Volatile.Write(ref s_x001DirectSignalPushDropCount_VocalWarningSystem, 0);"));
            Assert.That(source, Does.Contain("SignalBus<VocalCueSignal>.TryPushTracked(in cue, ref s_x001DirectSignalPushDropCount_VocalWarningSystem)"));
            Assert.That(source, Does.Contain("SignalBus<SubtitleCueSignal>.TryPushTracked(in subtitle, ref s_x001DirectSignalPushDropCount_VocalWarningSystem)"));
        }

        [Test]
        public void VocalWarningDataVaultRebindClearsPlaybackAndDispatchState()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Audio/VocalWarningSystem.cs");
            string rebind = ExtractMethodBlock(source, "private void RebindDataVault(IDataVault vault)");
            string dispose = ExtractMethodBlock(source, "private void DisposeNativeStorage()");
            string clearPresentation = ExtractMethodBlock(source, "private void ClearPresentationState(bool clearPendingFaults)");
            string clearLastDispatch = ExtractMethodBlock(source, "private void ClearLastDispatchRoute()");

            Assert.That(rebind, Does.Contain("CompletePendingVocalWarningJobsForTeardown();"));
            Assert.That(rebind, Does.Contain("ReleaseVaultBackedStorage();"));
            Assert.That(rebind, Does.Contain("_dataVault = vault;"));
            Assert.That(rebind, Does.Contain("Volatile.Write(ref _nativeAllocated, 0);"));
            Assert.That(rebind, Does.Contain("ClearPresentationState(true);"));
            Assert.That(rebind, Does.Contain("ClearLastDispatchRoute();"));
            Assert.That(rebind, Does.Contain("EnsureNativeStorage();"));
            AssertSourceOrder(rebind, "CompletePendingVocalWarningJobsForTeardown();", "ReleaseVaultBackedStorage();");
            AssertSourceOrder(rebind, "ReleaseVaultBackedStorage();", "_dataVault = vault;");
            AssertSourceOrder(rebind, "_dataVault = vault;", "Volatile.Write(ref _nativeAllocated, 0);");
            AssertSourceOrder(rebind, "Volatile.Write(ref _nativeAllocated, 0);", "ClearPresentationState(true);");
            AssertSourceOrder(rebind, "ClearPresentationState(true);", "ClearLastDispatchRoute();");
            AssertSourceOrder(rebind, "ClearLastDispatchRoute();", "EnsureNativeStorage();");

            Assert.That(dispose, Does.Contain("CompletePendingVocalWarningJobsForTeardown();"));
            Assert.That(dispose, Does.Contain("ReleaseVaultBackedStorage();"));
            Assert.That(dispose, Does.Contain("_dataVault = null;"));
            Assert.That(dispose, Does.Contain("ClearPresentationState(true);"));
            Assert.That(dispose, Does.Contain("ClearLastDispatchRoute();"));
            AssertSourceOrder(dispose, "CompletePendingVocalWarningJobsForTeardown();", "ReleaseVaultBackedStorage();");
            AssertSourceOrder(dispose, "ReleaseVaultBackedStorage();", "_dataVault = null;");
            AssertSourceOrder(dispose, "_dataVault = null;", "ClearPresentationState(true);");
            AssertSourceOrder(dispose, "ClearPresentationState(true);", "ClearLastDispatchRoute();");

            Assert.That(clearPresentation, Does.Contain("_queueCount = 0;"));
            Assert.That(clearPresentation, Does.Contain("_currentWarningId = 0;"));
            Assert.That(clearPresentation, Does.Contain("_currentAudioBankHashID = 0u;"));
            Assert.That(clearPresentation, Does.Contain("_currentPriorityScore = 0f;"));
            Assert.That(clearPresentation, Does.Contain("_warningPlaybackRemainingSeconds = 0f;"));
            Assert.That(clearPresentation, Does.Contain("Interlocked.Exchange(ref _pendingCancelRequest, 0);"));
            Assert.That(clearPresentation, Does.Contain("Interlocked.Exchange(ref _visualSyncPresentationPending, 0);"));
            Assert.That(clearPresentation, Does.Contain("Interlocked.Exchange(ref _pendingExternalFaultFlags, 0);"));
            Assert.That(clearPresentation, Does.Contain("_pendingPresentationFrame = 0u;"));

            Assert.That(clearLastDispatch, Does.Contain("_lastDispatchedAudioBankHashID = 0u;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastDispatchedWarningId = 0;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastDirectionHash = 0;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupGridX = 0L;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupGridY = 0L;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupGridZ = 0L;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupLocalX = 0f;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupLocalY = 0f;"));
            Assert.That(clearLastDispatch, Does.Contain("_lastSourceAupLocalZ = 0f;"));
        }

        [TestCase(
            "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs",
            "private void UnregisterRuntime()",
            "Interlocked.Exchange(ref _registeredHotSwap, 0) != 0")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs",
            "private void UnregisterRuntime()",
            "Interlocked.Exchange(ref _registeredHotSwap, 0) != 0")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs",
            "private void UnregisterRuntime()",
            "Interlocked.Exchange(ref _registeredHotSwap, 0) != 0")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs",
            "private void UnregisterRuntime()",
            "Interlocked.Exchange(ref _registeredHotSwap, 0) != 0")]
        [TestCase(
            "Assets/_Project/Scripts/Audio/AcousticReverbPresetTrigger.cs",
            "private void TryUnregisterHotSwapListener()",
            "if (!_hotSwapRegistered)")]
        public void AudioHotSwapTeardownUsesTryUnregisterWithoutLegacyRegistryMissPath(
            string relativePath,
            string unregisterSignature,
            string registrationGate)
        {
            string source = ReadProjectFile(relativePath);
            string unregister = ExtractMethodBlock(source, unregisterSignature);

            Assert.That(source, Does.Contain("GlobalRegistry.TryRegisterHotSwapListener(this)"));
            Assert.That(unregister, Does.Contain(registrationGate));
            Assert.That(unregister, Does.Contain("GlobalRegistry.TryUnregisterHotSwapListener(this);"));
            Assert.That(source, Does.Not.Contain("GlobalRegistry.RegisterHotSwapListener(this);"));
            Assert.That(unregister, Does.Not.Contain("GlobalRegistry.UnregisterHotSwapListener(this);"));
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

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }
    }
}
