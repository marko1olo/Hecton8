using System;
using System.Globalization;
using System.IO;
using Hecton.Localization;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ZeroGCSubtitleFormatter1423EditTests
    {
        [Test]
        public void NumericFormatter_UsesInvariantCulture_WhenThreadCultureUsesCommaDecimal()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                Span<char> buffer = stackalloc char[16];

                bool wrote = ZeroGCFormatter.TryFormatFloat(12.5f, buffer, "F1".AsSpan(), out int length);

                Assert.IsTrue(wrote);
                Assert.AreEqual(4, length);
                Assert.AreEqual('1', buffer[0]);
                Assert.AreEqual('2', buffer[1]);
                Assert.AreEqual('.', buffer[2]);
                Assert.AreEqual('5', buffer[3]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void TruncatedAppend_ClampsCursor_AndAppliesAsciiEllipsis()
        {
            Span<char> buffer = stackalloc char[5];
            int cursor = 0;

            bool fullWrite = ZeroGCFormatter.AppendToSpanTruncated(
                "ABCDEFGH".AsSpan(),
                buffer,
                ref cursor,
                out bool truncated);
            ZeroGCFormatter.AppendAsciiEllipsis(buffer, ref cursor);

            Assert.IsFalse(fullWrite);
            Assert.IsTrue(truncated);
            Assert.AreEqual(5, cursor);
            Assert.AreEqual('A', buffer[0]);
            Assert.AreEqual('B', buffer[1]);
            Assert.AreEqual('.', buffer[2]);
            Assert.AreEqual('.', buffer[3]);
            Assert.AreEqual('.', buffer[4]);
        }

        [Test]
        public void MockSubtitleSpamFormatter_StaysInsideFixedBuffer_ForFiveHundredWarnings()
        {
            ReadOnlySpan<char> template = "VWS O2 {N0:F1}%".AsSpan();
            Span<char> buffer = stackalloc char[32];

            for (int i = 0; i < 500; i++)
            {
                float value = (i % 101) * 0.1f;
                bool wrote = LocNumericBuffer.TryWrite(template, buffer, LocNumericArg.Float(value), out int length);

                Assert.IsTrue(wrote);
                Assert.Greater(length, 0);
                Assert.LessOrEqual(length, buffer.Length);
                for (int c = 0; c < length; c++)
                    Assert.AreNotEqual(',', buffer[c]);
            }
        }

        [Test]
        public void MockSubtitleOverflowFormatter_FailsClosedWithoutMovingCursorPastCapacity()
        {
            Span<char> buffer = stackalloc char[8];
            int cursor = 0;

            bool fullWrite = ZeroGCFormatter.AppendToSpanTruncated(
                "EXTREMELY_LONG_LOCALIZED_WARNING_LINE".AsSpan(),
                buffer,
                ref cursor,
                out bool truncated);
            ZeroGCFormatter.AppendAsciiEllipsis(buffer, ref cursor);

            Assert.IsFalse(fullWrite);
            Assert.IsTrue(truncated);
            Assert.AreEqual(buffer.Length, cursor);
            Assert.AreEqual('.', buffer[buffer.Length - 1]);
        }

        [Test]
        public void BabelPlaceholderOverflow_DoesNotPromoteCursorToCapacity()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("charCursor = maxGlyphs", source);
            StringAssert.Contains("charCursor = math.clamp(charCursor, 0, maxGlyphs)", source);
        }

        [Test]
        public void LocalizedSpanReadPath_DoesNotRefreshVaultBackedBytes()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs");
            string source = File.ReadAllText(sourcePath);

            int methodStart = source.IndexOf("public static bool TryGetLocalizedSpan", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBody = source.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("IsValidUtf8SliceNoRefresh(slice)", methodBody);
            StringAssert.DoesNotContain("RefreshUtf8BytesFromVault", methodBody);
            StringAssert.DoesNotContain("IsValidUtf8Slice(slice)", methodBody);
        }

        [Test]
        public void PdaDecryptLabel_DoesNotDoubleDecodeLengthBeforeBufferFetch()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("LocRegistry.GetLength(hash)", source);
            StringAssert.Contains("LocRegistry.TryGetVisualBuffer(hash, out char[] source, out int length)", source);
        }

        [Test]
        public void LabelSwapScheduler_SyncsRichTextLodPolicyBeforeCharArrayPush()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/UI/LabelSwapScheduler.cs");
            string source = File.ReadAllText(sourcePath);

            string richTextPolicy = "text.richText = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();";
            string charArrayPush = "text.SetCharArray(lease.TmpBuffer, 0, length);";
            int policyIndex = source.IndexOf(richTextPolicy, StringComparison.Ordinal);
            int pushIndex = source.IndexOf(charArrayPush, StringComparison.Ordinal);

            Assert.GreaterOrEqual(policyIndex, 0);
            Assert.Greater(pushIndex, policyIndex);
        }

        [Test]
        public void HudWorldSignAndPdaDecrypt_SyncTmpRichTextPolicyBeforeCharArrayPush()
        {
            string hudSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs"));
            string worldSignSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocalizedWorldSign.cs"));
            string pdaDecryptSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs"));

            StringAssert.Contains("bool richText = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();", hudSource);
            StringAssert.Contains("label.richText = richText;", hudSource);

            string worldSignPolicy = "targetText.richText = Hecton8.UI.BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();";
            string worldSignPush = "targetText.SetCharArray(displayBuffer, 0, displayLength);";
            int worldSignPolicyIndex = worldSignSource.IndexOf(worldSignPolicy, StringComparison.Ordinal);
            int worldSignPushIndex = worldSignSource.IndexOf(worldSignPush, StringComparison.Ordinal);
            Assert.GreaterOrEqual(worldSignPolicyIndex, 0);
            Assert.Greater(worldSignPushIndex, worldSignPolicyIndex);

            string pdaPolicy = "targetText.richText = false;";
            string pdaPush = "targetText.SetCharArray(lease.Buffer, 0, writeLength);";
            int pdaPolicyIndex = pdaDecryptSource.IndexOf(pdaPolicy, StringComparison.Ordinal);
            int pdaPushIndex = pdaDecryptSource.IndexOf(pdaPush, StringComparison.Ordinal);
            Assert.GreaterOrEqual(pdaPolicyIndex, 0);
            Assert.Greater(pdaPushIndex, pdaPolicyIndex);
        }

        [Test]
        public void BabelDictionaryStageCommit_ReleasesStageInFinally()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs");
            string source = File.ReadAllText(sourcePath);

            int methodStart = source.IndexOf("public static unsafe bool TryCommitStagedBabelDictionary", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBody = source.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("finally", methodBody);
            StringAssert.Contains("AbortBabelDictionaryStage();", methodBody);
        }

        [Test]
        public void SubtitleCueSignalContract_LivesOutsideUiRuntimeFile()
        {
            string contractSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/Core/Contracts/Signals/SubtitleCueSignal.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs"));

            StringAssert.Contains("namespace Hecton8.Core.Contracts.Signals", contractSource);
            StringAssert.Contains("public struct SubtitleCueSignal : ISignal", contractSource);
            StringAssert.Contains("StructLayout(LayoutKind.Explicit, Size = 64)", contractSource);
            StringAssert.Contains("SubtitleCueSignal.LaneHash", runtimeSource);
            StringAssert.DoesNotContain("public struct SubtitleCueSignal : ISignal", runtimeSource);
        }

        [Test]
        public void AudioCaptionRequest_DoesNotCarryManagedCaptionText()
        {
            string audioSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/SpatialAudioManager.cs"));

            int requestStart = audioSource.IndexOf("public readonly struct AudioCaptionRequest", StringComparison.Ordinal);
            Assert.GreaterOrEqual(requestStart, 0);
            int requestEnd = audioSource.IndexOf("public struct AudioCaptionPayload", requestStart, StringComparison.Ordinal);
            Assert.Greater(requestEnd, requestStart);
            string requestBody = audioSource.Substring(requestStart, requestEnd - requestStart);

            StringAssert.Contains("public uint CaptionHashId { get; }", requestBody);
            StringAssert.DoesNotContain("CaptionText", requestBody);
            StringAssert.DoesNotContain("string captionText", requestBody);
            StringAssert.Contains("TryWriteCaptionText", audioSource);
            StringAssert.DoesNotContain("public static string ResolveCaptionText", audioSource);
            StringAssert.DoesNotContain("LocHash.Compute(LowPowerCaptionText)", audioSource);
        }

        [Test]
        public void AudioCaptionEvents_PreallocatesPayloadRingsAndWritesBabelFirst()
        {
            string audioSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/SpatialAudioManager.cs"));

            int eventsStart = audioSource.IndexOf("public static class AudioCaptionEvents", StringComparison.Ordinal);
            Assert.GreaterOrEqual(eventsStart, 0);
            string eventsBody = audioSource.Substring(eventsStart);

            int enqueueStart = eventsBody.IndexOf("private static bool Enqueue(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(enqueueStart, 0);
            int promoteStart = eventsBody.IndexOf("private static void PromoteNextFrameEvents", enqueueStart, StringComparison.Ordinal);
            Assert.Greater(promoteStart, enqueueStart);
            string enqueueBody = eventsBody.Substring(enqueueStart, promoteStart - enqueueStart);

            StringAssert.Contains("private static readonly AudioCaptionPayload[] _pendingEvents = new AudioCaptionPayload[PendingEventCapacity]", eventsBody);
            StringAssert.Contains("private static readonly AudioCaptionPayload[] _nextFrameEvents = new AudioCaptionPayload[PendingEventCapacity]", eventsBody);
            StringAssert.DoesNotContain("EnsureInitialized", eventsBody);
            StringAssert.DoesNotContain("new AudioCaptionPayload[", enqueueBody);
            StringAssert.Contains("HasCaptionText(captionHashId)", eventsBody);
            StringAssert.DoesNotContain("if (!HasCaptionText(payload.CaptionHashId))", eventsBody);
            StringAssert.Contains("LocRegistry.TryGetLocalizedSpan(captionHashId", eventsBody);
            StringAssert.Contains("LocRegistry.TryWriteKnownLocalizedSpanFromUtf8(captionHashId", eventsBody);
            StringAssert.DoesNotContain("LocRegistry.TryWriteVisualSpanFromUtf8(captionHashId", eventsBody);
        }

        [Test]
        public void AudioCaptionEvents_UsePullConsumerWithoutManagedListenerCallbacks()
        {
            string audioSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/SpatialAudioManager.cs"));
            string overlaySource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AcousticEcholocationTranslator.cs"));

            int eventsStart = audioSource.IndexOf("public static class AudioCaptionEvents", StringComparison.Ordinal);
            Assert.GreaterOrEqual(eventsStart, 0);
            string eventsBody = audioSource.Substring(eventsStart);

            StringAssert.DoesNotContain("IAudioCaptionEventListener", audioSource);
            StringAssert.DoesNotContain("ListenerSlot", eventsBody);
            StringAssert.DoesNotContain("_listeners", eventsBody);
            StringAssert.DoesNotContain("OnAudioCaptionRequested", audioSource);
            StringAssert.Contains("public static bool ConsumeNextPendingCaption(out AudioCaptionRequest request)", eventsBody);
            StringAssert.Contains("public static void RegisterConsumer()", eventsBody);
            StringAssert.Contains("public static void UnregisterConsumer()", eventsBody);
            StringAssert.DoesNotContain("private static void Dispatch(in AudioCaptionPayload payload)", eventsBody);

            StringAssert.DoesNotContain("IAudioCaptionEventListener", overlaySource);
            StringAssert.DoesNotContain("AudioCaptionEvents.Register(this)", overlaySource);
            StringAssert.DoesNotContain("AudioCaptionEvents.Unregister(this)", overlaySource);
            StringAssert.DoesNotContain("OnAudioCaptionRequested", overlaySource);
            StringAssert.Contains("DrainPendingCaptionRequests", overlaySource);
            StringAssert.Contains("AudioCaptionEvents.ConsumeNextPendingCaption(out AudioCaptionRequest request)", overlaySource);
        }

        [Test]
        public void AudioCaptionEvents_DoNotOwnBuiltInEnglishCaptionLiterals()
        {
            string audioSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/SpatialAudioManager.cs"));
            string fallbackCatalog = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/VwsCaptionFallbackCatalog.cs"));

            int eventsStart = audioSource.IndexOf("public static class AudioCaptionEvents", StringComparison.Ordinal);
            Assert.GreaterOrEqual(eventsStart, 0);
            string eventsBody = audioSource.Substring(eventsStart);

            StringAssert.Contains("VwsCaptionFallbackCatalog.TryResolveCaptionTextSpan(captionHashId", eventsBody);
            StringAssert.Contains("Built-in VWS caption fallback catalog", fallbackCatalog);
            StringAssert.DoesNotContain("\"SUBMARINE LOW POWER\"", eventsBody);
            StringAssert.DoesNotContain("\"LIFE SUPPORT CRITICAL\"", eventsBody);
            StringAssert.DoesNotContain("\"MULTIPLE SYSTEM FAILURES\"", eventsBody);
            StringAssert.DoesNotContain("\"EMERGENCY LEVEL DANGER\"", eventsBody);
            StringAssert.DoesNotContain("\"ABANDON SHIP\"", eventsBody);
            StringAssert.DoesNotContain("\"HOSTILE DRONE DETECTED\"", eventsBody);
            StringAssert.DoesNotContain("\"OXYGEN LOW\"", eventsBody);
            StringAssert.DoesNotContain("\"OXYGEN CRITICAL\"", eventsBody);
            StringAssert.DoesNotContain("\"HULL BREACH\"", eventsBody);
            StringAssert.DoesNotContain("\"HULL PRESSURE HIGH\"", eventsBody);
            StringAssert.DoesNotContain("\"THERMAL STRESS\"", eventsBody);
        }

        [Test]
        public void SubmarineOsTerminalStaticText_UsesSpanLiteralsNotColdCharArrayClones()
        {
            string displaySource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/HectonSubmarineOsDisplay.cs"));
            string biosSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/BIOSMessageStreamer.cs"));

            StringAssert.DoesNotContain(".ToCharArray()", displaySource);
            StringAssert.DoesNotContain(".ToCharArray()", biosSource);
            StringAssert.Contains("private static ReadOnlySpan<char> LogPrefixWarn", displaySource);
            StringAssert.Contains("private static ReadOnlySpan<char> WarnPrefix", biosSource);
            StringAssert.Contains("AppendSpan(destination, cursor", displaySource);
            StringAssert.Contains("AppendSpan(destination, cursor", biosSource);
        }

        [Test]
        public void TerminalBootStatusText_UsesSpanStatusRoutesNotStringBridges()
        {
            string bootSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AcousticEcholocationTranslator.cs"));
            string osBootSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/HectonOSBootManager.cs"));

            int sequenceStart = bootSource.IndexOf("public sealed class TerminalBootSequence", StringComparison.Ordinal);
            Assert.GreaterOrEqual(sequenceStart, 0);
            string sequenceBody = bootSource.Substring(sequenceStart);

            StringAssert.Contains("private static ReadOnlySpan<char> StatusOkChars", sequenceBody);
            StringAssert.Contains("ReadOnlySpan<char> hullStatus = ResolveIntegrityStatusChars", sequenceBody);
            StringAssert.Contains("private static int AppendSpan(char[] buffer, int cursor, ReadOnlySpan<char> value)", sequenceBody);
            StringAssert.Contains("private static ReadOnlySpan<char> ResolveIntegrityStatusChars(float integrity01)", sequenceBody);
            StringAssert.DoesNotContain("AppendString", sequenceBody);
            StringAssert.DoesNotContain("private static string ResolveIntegrityStatus", sequenceBody);

            StringAssert.Contains("ReadOnlySpan<char> bootVector = ResolveBootVector(reason);", osBootSource);
            StringAssert.Contains("private static ReadOnlySpan<char> ResolveBootVector(BootReason reason)", osBootSource);
            StringAssert.Contains("private static ReadOnlySpan<char> ResolveHullIntegrityStatus(float integrity)", osBootSource);
            StringAssert.Contains("private static ReadOnlySpan<char> ResolvePressureBusStatus(float pressure)", osBootSource);
            StringAssert.DoesNotContain("private static string ResolveBootVector", osBootSource);
            StringAssert.DoesNotContain("private static string ResolveHullIntegrityStatus", osBootSource);
            StringAssert.DoesNotContain("private static string ResolvePressureBusStatus", osBootSource);
        }

        [Test]
        public void AudioCaptionOverlay_WritesCaptionTextIntoExistingSlotBuffer()
        {
            string overlaySource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AcousticEcholocationTranslator.cs"));

            StringAssert.Contains("AudioCaptionEvents.TryWriteCaptionText(", overlaySource);
            StringAssert.Contains("slot.TextBuffer.AsSpan()", overlaySource);
            StringAssert.Contains("slot.Label.SetCharArray(slot.TextBuffer, 0, displayLength);", overlaySource);
            StringAssert.DoesNotContain("ReadOnlySpan<char> captionText = AudioCaptionEvents.TryResolveCaptionTextSpan", overlaySource);
            StringAssert.DoesNotContain("SlotTextMatches(ref CaptionSlot slot, ReadOnlySpan<char> captionText", overlaySource);
            StringAssert.DoesNotContain("string captionText = request.CaptionText", overlaySource);
        }

        [Test]
        public void LocRegistryKnownUtf8Writer_DecodesWithoutSecondLookup()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs"));

            int methodStart = source.IndexOf(
                "public static bool TryWriteKnownLocalizedSpanFromUtf8(\r\n            uint keyHash,\r\n            ReadOnlySpan<byte> utf8Bytes,\r\n            Span<char> destination,\r\n            out int length,\r\n            BabelFormatArgs formatArgs",
                StringComparison.Ordinal);
            if (methodStart < 0)
            {
                methodStart = source.IndexOf(
                    "public static bool TryWriteKnownLocalizedSpanFromUtf8(\n            uint keyHash,\n            ReadOnlySpan<byte> utf8Bytes,\n            Span<char> destination,\n            out int length,\n            BabelFormatArgs formatArgs",
                    StringComparison.Ordinal);
            }

            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBody = source.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("DecodeUtf8VisualSpan(keyHash, true, utf8Bytes", methodBody);
            StringAssert.DoesNotContain("TrackLocalizedSpanLookupForDecode", methodBody);
            StringAssert.DoesNotContain("TryFindUtf8Slice", methodBody);
        }

        [Test]
        public void SubtitleAudioLogCueSync_UsesLateFramePullSnapshotInsteadOfManagedEvent()
        {
            string subtitleSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/SubtitleManager.cs"));
            string waveformSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AudioWaveformAnimator.cs"));

            StringAssert.DoesNotContain("OnCueChanged", subtitleSource);
            StringAssert.DoesNotContain("OnCueChanged", waveformSource);
            StringAssert.Contains("TryGetAudioLogCueSnapshot", subtitleSource);
            StringAssert.Contains("private void ConsumeCueSnapshot()", waveformSource);
            StringAssert.Contains("manager.TryGetAudioLogCueSnapshot(", waveformSource);
            StringAssert.Contains("public void LateFrameTick()", waveformSource);
            StringAssert.Contains("ConsumeCueSnapshot();", waveformSource);
            int setCharArrayIndex = waveformSource.IndexOf("optionalCueText.SetCharArray(", StringComparison.Ordinal);
            int cacheBufferIndex = waveformSource.IndexOf("_optionalCueTextCache", setCharArrayIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(setCharArrayIndex, 0);
            Assert.Greater(cacheBufferIndex, setCharArrayIndex);
        }

        [Test]
        public void LocRegistryOverrideCsv_ReleasesScratchLockBeforeMutationGuard()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs"));

            int methodStart = source.IndexOf("public static unsafe bool TryApplyLocOverridesCsv", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBody = source.Substring(methodStart, methodEnd - methodStart);

            int scratchLockIndex = methodBody.IndexOf("scratchLocked = _babelVault.TryLockBuffer", StringComparison.Ordinal);
            int scratchUnlockIndex = methodBody.IndexOf("_babelVault.TryUnlockBuffer(BabelOverrideCsvScratchBufferId", StringComparison.Ordinal);
            int mutationGuardIndex = methodBody.IndexOf("mutationGuarded = _babelVault.TryAcquireMutationGuard", StringComparison.Ordinal);
            int mutationReleaseIndex = methodBody.IndexOf("_babelVault.ReleaseMutationGuard(BabelOverrideMutationGuardMask)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(scratchLockIndex, 0);
            Assert.Greater(scratchUnlockIndex, scratchLockIndex);
            Assert.Greater(mutationGuardIndex, scratchUnlockIndex);
            Assert.Greater(mutationReleaseIndex, mutationGuardIndex);
        }

        [Test]
        public void AcousticBarkPresentation_UsesLateFramePendingCounter()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AcousticEcholocationTranslator.cs"));

            StringAssert.DoesNotContain("IAcousticEcholocationBarkListener", source);
            StringAssert.DoesNotContain("OnStorageCapacityExceededBark", source);
            StringAssert.DoesNotContain("AcousticEcholocationBarkEvents.Register", source);
            StringAssert.DoesNotContain("AcousticEcholocationBarkEvents.Unregister", source);
            StringAssert.Contains("private static int s_pendingStorageCapacityExceeded;", source);
            StringAssert.Contains("public static bool ConsumeStorageCapacityExceeded()", source);
            StringAssert.Contains("DrainStorageCapacityExceededBarks();", source);
            StringAssert.Contains("private void ShowStorageCapacityExceededBark()", source);
        }
    }
}
