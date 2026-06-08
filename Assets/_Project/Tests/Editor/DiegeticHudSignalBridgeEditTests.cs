using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DiegeticHudSignalBridgeEditTests
    {
        [Test]
        public void DiegeticHudTextNode_ConsumesSignalLaneWithLifecycleAndFailureVisibility()
        {
            string textNode = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "DiegeticHudTextNode.cs");
            string prologueBridge = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "PrologueSequenceRegistryBridge.cs");

            string onEnable = ExtractMethodBody(textNode, "private void OnEnable()");
            string onDisable = ExtractMethodBody(textNode, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(textNode, "private void OnDestroy()");
            string lateFrame = ExtractMethodBody(textNode, "public void LateFrameTick()");
            string hotSwap = ExtractMethodBody(textNode, "public void OnGlobalRegistryServiceReplaced(");
            string claim = ExtractMethodBody(textNode, "private void TryClaimSignalOwner()");
            string release = ExtractMethodBody(textNode, "private void ReleaseSignalOwner()");
            string register = ExtractMethodBody(textNode, "private void TryRegisterLateFrame()");
            string unregister = ExtractMethodBody(textNode, "private void UnregisterLateFrame()");
            string drain = ExtractMethodBody(textNode, "private void DrainDiegeticHudSignalLane()");
            string apply = ExtractMethodBody(textNode, "private void ApplyDiegeticHudSignal(");
            string write = ExtractMethodBody(textNode, "private bool TryWriteDiegeticSignalMessage(");
            string fallback = ExtractMethodBody(textNode, "private static bool TryWriteDiegeticSignalFallback(");
            string reportMessageMiss = ExtractMethodBody(textNode, "private void ReportDiegeticSignalMessageMiss(");
            string reportWriteMiss = ExtractMethodBody(textNode, "private void ReportDiegeticSignalWriteMiss(");
            string reportDuplicate = ExtractMethodBody(textNode, "private void ReportDuplicateSignalOwner()");
            string clearRuntime = ExtractMethodBody(textNode, "private void ClearDiegeticSignalRuntimeState()");
            string clearDiagnostics = ExtractMethodBody(textNode, "private void ClearDiegeticHudSignalDiagnostics()");
            string publishManualPrompt = ExtractMethodBody(prologueBridge, "public void PublishManualReleasePrompt()");

            StringAssert.Contains("using Hecton8.Core.Contracts.Signals;", textNode);
            StringAssert.Contains("public sealed class DiegeticHudTextNode : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener", textNode);
            StringAssert.Contains("[SerializeField] private bool consumeDiegeticHudSignals = true;", textNode);
            StringAssert.Contains("private static DiegeticHudTextNode s_signalOwner;", textNode);
            StringAssert.Contains("ResetStaticState()", textNode);
            StringAssert.Contains("s_signalOwner = null;", textNode);

            StringAssert.Contains("public int ConsumedDiegeticSignalCount => _consumedDiegeticSignalCount;", textNode);
            StringAssert.Contains("public int DiegeticSignalMessageMissCount => _diegeticSignalMessageMissCount;", textNode);
            StringAssert.Contains("public int DiegeticSignalWriteMissCount => _diegeticSignalWriteMissCount;", textNode);
            StringAssert.Contains("public int DuplicateSignalOwnerCount => _duplicateSignalOwnerCount;", textNode);
            StringAssert.Contains("public uint LastDiegeticSignalMessageHash => _lastDiegeticSignalMessageHash;", textNode);
            StringAssert.Contains("public uint LastDiegeticSignalContextHash => _lastDiegeticSignalContextHash;", textNode);

            StringAssert.Contains("TryRegisterHotSwapListener();", onEnable);
            StringAssert.Contains("TryClaimSignalOwner();", onEnable);
            StringAssert.Contains("ReleaseSignalOwner();", onDisable);
            StringAssert.Contains("TryUnregisterHotSwapListener();", onDisable);
            StringAssert.Contains("ClearDiegeticSignalRuntimeState();", onDisable);
            StringAssert.Contains("ClearDiegeticHudSignalDiagnostics();", onDisable);
            StringAssert.Contains("ReleaseSignalOwner();", onDestroy);
            StringAssert.Contains("ClearDiegeticSignalRuntimeState();", onDestroy);
            StringAssert.Contains("ClearDiegeticHudSignalDiagnostics();", onDestroy);

            StringAssert.Contains("if (!ReferenceEquals(s_signalOwner, this))", lateFrame);
            StringAssert.Contains("DrainDiegeticHudSignalLane();", lateFrame);
            StringAssert.Contains("GlobalRegistryServiceSlot.Dispatcher", hotSwap);
            StringAssert.Contains("UnregisterLateFrame();", hotSwap);
            StringAssert.Contains("TryRegisterLateFrame();", hotSwap);

            StringAssert.Contains("!consumeDiegeticHudSignals || !Application.isPlaying", claim);
            StringAssert.Contains("s_signalOwner != null && !ReferenceEquals(s_signalOwner, this)", claim);
            StringAssert.Contains("ReportDuplicateSignalOwner();", claim);
            StringAssert.Contains("s_signalOwner = this;", claim);
            StringAssert.Contains("TryRegisterLateFrame();", claim);
            StringAssert.Contains("ReferenceEquals(s_signalOwner, this)", release);
            StringAssert.Contains("s_signalOwner = null;", release);
            StringAssert.Contains("UnregisterLateFrame();", release);
            StringAssert.Contains("SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)", register);
            StringAssert.Contains("SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);", unregister);

            StringAssert.Contains("SignalBus<DiegeticHudSignal>.TryConsumeFrame(out DiegeticHudSignal signal)", drain);
            StringAssert.Contains("ApplyDiegeticHudSignal(in signal);", drain);
            StringAssert.Contains("TryWriteDiegeticSignalMessage(in signal, out int length)", apply);
            StringAssert.Contains("SetSpan(_signalDecodeBuffer.AsSpan(0, length))", apply);
            StringAssert.Contains("ReportDiegeticSignalWriteMiss(in signal);", apply);
            StringAssert.Contains("_lastDiegeticSignalMessageHash = signal.MessageHash;", apply);
            StringAssert.Contains("_lastDiegeticSignalContextHash = signal.ContextHash;", apply);
            StringAssert.Contains("_consumedDiegeticSignalCount++;", apply);

            StringAssert.Contains("if (signal.MessageHash == 0u)", write);
            StringAssert.Contains("ReportDiegeticSignalMessageMiss(in signal);", write);
            AssertTextBefore(write, "ReportDiegeticSignalMessageMiss(in signal);", "return false;");
            StringAssert.Contains("LocRegistry.TryWriteVisualSpanFromUtf8(", write);
            StringAssert.Contains("stripRichText: true", write);
            StringAssert.Contains("TryWriteDiegeticSignalFallback(signal.MessageHash", write);
            StringAssert.Contains("DiegeticSignalFallbackPrefix.AsSpan().CopyTo(target);", fallback);
            StringAssert.Contains("ToUpperHexNibble", fallback);

            StringAssert.Contains("_diegeticSignalMessageMissCount++;", reportMessageMiss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportMessageMiss);
            StringAssert.Contains("DiegeticHudSignalMissWarningHash", reportMessageMiss);
            StringAssert.Contains("signal.MessageHash ^ signal.ContextHash", reportMessageMiss);
            StringAssert.Contains("math.max(1, _diegeticSignalMessageMissCount)", reportMessageMiss);
            StringAssert.Contains("_diegeticSignalWriteMissCount++;", reportWriteMiss);
            StringAssert.Contains("DiegeticHudSignalWriteMissWarningHash", reportWriteMiss);
            StringAssert.Contains("math.max(1, _diegeticSignalWriteMissCount)", reportWriteMiss);
            StringAssert.Contains("_duplicateSignalOwnerCount++;", reportDuplicate);
            StringAssert.Contains("DiegeticHudDuplicateOwnerWarningHash", reportDuplicate);
            StringAssert.Contains("math.max(1, _duplicateSignalOwnerCount)", reportDuplicate);

            StringAssert.Contains("_lastDiegeticSignalMessageHash = 0u;", clearRuntime);
            StringAssert.Contains("_lastDiegeticSignalContextHash = 0u;", clearRuntime);
            StringAssert.Contains("_consumedDiegeticSignalCount = 0;", clearRuntime);
            StringAssert.Contains("_diegeticSignalMessageMissCount = 0;", clearDiagnostics);
            StringAssert.Contains("_diegeticSignalWriteMissCount = 0;", clearDiagnostics);
            StringAssert.Contains("_duplicateSignalOwnerCount = 0;", clearDiagnostics);

            StringAssert.Contains("SignalBus<DiegeticHudSignal>.TryPushTracked(in diegetic", publishManualPrompt);
            StringAssert.Contains("SignalBus<HUDNotificationSignal>.TryPushTracked(in hud", publishManualPrompt);
            AssertTextBefore(publishManualPrompt, "diegetic.MessageHash = ManualReleaseHash;", "SignalBus<DiegeticHudSignal>.TryPushTracked(in diegetic");
        }

        private static string ReadProjectFile(params string[] relativeParts)
        {
            string path = Path.Combine(Application.dataPath, "..");
            for (int i = 0; i < relativeParts.Length; i++)
                path = Path.Combine(path, relativeParts[i]);

            return File.ReadAllText(Path.GetFullPath(path));
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, beforeIndex >= 0 ? beforeIndex : 0, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing token: " + before);
            Assert.Greater(afterIndex, beforeIndex, "Expected token order: " + before + " before " + after);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
