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
        public void LocRegistryOverrideCsv_UsesH8ScratchWithoutDataVaultScratchLock()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs"));

            string apply = ExtractMethodBody(source, "public static unsafe bool TryApplyLocOverridesCsv(");
            string ensure = ExtractMethodBody(source, "private static void EnsureOverrideCsvScratch()");
            string dispose = ExtractMethodBody(source, "private static void DisposeOverrideCsvScratch()");

            StringAssert.DoesNotContain("TryLockBuffer", apply);
            StringAssert.DoesNotContain("TryUnlockBuffer", apply);
            StringAssert.Contains("mutationGuarded = _babelVault.TryAcquireMutationGuard", apply);
            StringAssert.Contains("_babelVault.ReleaseMutationGuard(BabelOverrideMutationGuardMask)", apply);
            StringAssert.Contains("H8Memory.Allocate<byte>", ensure);
            StringAssert.Contains("H8Memory.Release(ref _overrideCsvScratch", dispose);
        }

        [Test]
        public void LocRegistryBabelStage_UsesH8ScratchAcrossAsyncBoundary()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs"));
            string begin = ExtractMethodBody(source, "public static unsafe bool TryBeginBabelDictionaryStage(");
            string commit = ExtractMethodBody(source, "public static unsafe bool TryCommitStagedBabelDictionary(");
            string abort = ExtractMethodBody(source, "private static void AbortBabelDictionaryStage()");

            StringAssert.DoesNotContain("TryLockBuffer", begin);
            StringAssert.DoesNotContain("TryUnlockBuffer", begin);
            StringAssert.Contains("H8Memory.Allocate<byte>", begin);
            StringAssert.Contains("H8Memory.Release(ref _stagedLocaleBytes", begin);
            StringAssert.DoesNotContain("TryResolveBabelBuffer", commit);
            StringAssert.Contains("NativeArray<byte> staged = _stagedLocaleBytes;", commit);
            StringAssert.Contains("H8Memory.Release(ref _stagedLocaleBytes", abort);
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

        [Test]
        public void RuntimeUiAudioBabelDomain_HasNoForbiddenManagedTextBridgePatterns()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project/Scripts");
            AssertRuntimeSourceTreeHasNoForbiddenTextBridges(Path.Combine(scriptsRoot, "UI"));
            AssertRuntimeSourceTreeHasNoForbiddenTextBridges(Path.Combine(scriptsRoot, "Audio"));
            AssertRuntimeSourceFileHasNoForbiddenTextBridges(Path.Combine(scriptsRoot, "LocRegistry.cs"));
            AssertRuntimeSourceFileHasNoForbiddenTextBridges(Path.Combine(scriptsRoot, "SpatialAudioManager.cs"));
        }

        [Test]
        public void RuntimeUiAudioBabelHotMethods_DoNotResolveColdDependencies()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project/Scripts");
            AssertRuntimeHotMethodsHaveNoColdLookups(Path.Combine(scriptsRoot, "UI"));
            AssertRuntimeHotMethodsHaveNoColdLookups(Path.Combine(scriptsRoot, "Audio"));
            AssertRuntimeHotMethodFileHasNoColdLookups(Path.Combine(scriptsRoot, "LocRegistry.cs"));
            AssertRuntimeHotMethodFileHasNoColdLookups(Path.Combine(scriptsRoot, "SpatialAudioManager.cs"));
        }

        [Test]
        public void SubtitleManager_UsesHashSpanEntrypointsAndColdCachedLoreDatabase()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/SubtitleManager.cs"));
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string audioLogPrepare = ExtractMethodBody(source, "private bool TryPrepareAudioLogBuffers(");

            StringAssert.DoesNotContain("DisplaySubtitle(string", source);
            StringAssert.DoesNotContain("private void Enqueue(string", source);
            StringAssert.Contains("public bool DisplaySubtitle(ReadOnlySpan<char> text, float duration)", source);
            StringAssert.Contains("public bool DisplaySubtitle(uint textHash, float duration)", source);
            StringAssert.Contains("private ILoreDatabaseReadModel _cachedLoreDatabase;", source);
            StringAssert.Contains("_cachedLoreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabaseReadModel;", cache);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.LoreDatabaseRuntime", serviceReplaced);
            StringAssert.Contains("_cachedLoreDatabase = currentService as ILoreDatabaseReadModel;", serviceReplaced);
            StringAssert.Contains("ILoreDatabaseReadModel database = _cachedLoreDatabase;", audioLogPrepare);
            StringAssert.DoesNotContain("Hecton8.Core.GlobalRegistry.LoreDatabaseReadModel", audioLogPrepare);
        }

        [Test]
        public void HudNotification_UsesSpanAndFixedBufferEntrypointsOnly()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/HUDNotification.cs"));
            string toolHit = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/ToolHitUtility.cs"));
            string moddingApi = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/ModdingAPI/HectonAPI.cs"));
            string acquireQueue = ExtractMethodBody(source, "private bool TryAcquireQueueWrite(");

            StringAssert.DoesNotContain("ShowWarning(string", source);
            StringAssert.DoesNotContain("ShowCritical(string", source);
            StringAssert.DoesNotContain("ShowInfo(string", source);
            StringAssert.DoesNotContain("private void Enqueue(string", source);
            StringAssert.Contains("public void ShowWarning(ReadOnlySpan<char> message)", source);
            StringAssert.Contains("public void ShowCritical(ReadOnlySpan<char> message)", source);
            StringAssert.Contains("public void ShowInfo(ReadOnlySpan<char> message)", source);
            StringAssert.Contains("public void ShowWarning(in FixedCharBuffer messageBuffer)", source);
            StringAssert.Contains("public void ShowCritical(in FixedCharBuffer messageBuffer)", source);
            StringAssert.Contains("public void ShowInfo(in FixedCharBuffer messageBuffer)", source);
            StringAssert.Contains("s_notification.ShowInfo(message.AsSpan());", toolHit);
            StringAssert.Contains("s_notification.ShowWarning(message.AsSpan());", toolHit);
            StringAssert.Contains("notification.ShowInfo(messageSpan);", moddingApi);
            StringAssert.Contains("notification.ShowWarning(messageSpan);", moddingApi);
            StringAssert.Contains("notification.ShowCritical(messageSpan);", moddingApi);
            Assert.AreEqual(1, CountToken(acquireQueue, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(acquireQueue, "ReleaseWriteLock("));
            Assert.AreEqual(1, CountToken(acquireQueue, "finally"));
            StringAssert.Contains("bool releaseOnExit = true;", acquireQueue);
            StringAssert.Contains("releaseOnExit = false;", acquireQueue);
        }

        [Test]
        public void ArWaypointLabels_UseHashLengthCacheAndSpanTmpSink()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/ARWaypointOverlay.cs"));
            string contracts = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/Core/GlobalRegistryContracts.cs"));
            string narrative = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/HectonNarrativeDirector.cs"));
            string applyLabel = ExtractMethodBody(source, "private void ApplyLabelText(");
            string copyLabel = ExtractMethodBody(source, "private static int CopyLabelToBuffer(");
            string copyLabelBank = ExtractMethodBody(source, "private static int CopyLabelToBuffer(ReadOnlySpan<char> value, char[] destination, int destinationOffset, int capacity)");

            StringAssert.DoesNotContain("public string CachedLabel;", source);
            StringAssert.DoesNotContain("public string Label;", source);
            StringAssert.DoesNotContain("runtimeWaypoint.Label =", source);
            StringAssert.DoesNotContain("externalWaypoint.Label =", source);
            StringAssert.DoesNotContain("string.Equals(slot.CachedLabel", source);
            StringAssert.Contains("public uint LabelHash;", source);
            StringAssert.Contains("public int LabelOffset;", source);
            StringAssert.Contains("public int LabelLength;", source);
            StringAssert.Contains("public uint LabelRevision;", source);
            StringAssert.Contains("public int LabelSlotIndex;", source);
            StringAssert.Contains("public bool HasLabel;", source);
            StringAssert.Contains("public uint CachedLabelHash;", source);
            StringAssert.Contains("public int CachedLabelLength;", source);
            StringAssert.Contains("public int CachedLabelSlotIndex;", source);
            StringAssert.Contains("public uint CachedLabelRevision;", source);
            StringAssert.Contains("private readonly char[] _externalWaypointLabelBuffer = new char[MaxExternalWaypoints * MaximumLabelCharacters];", source);
            StringAssert.Contains("private int CopyExternalLabelToBank(int waypointIndex, ReadOnlySpan<char> value)", source);
            StringAssert.Contains("private void ApplyLabelText(TextMeshProUGUI label, ReadOnlySpan<char> value)", source);
            StringAssert.Contains("private static int CopyLabelToBuffer(ReadOnlySpan<char> value, char[] destination)", source);
            StringAssert.Contains("private static int CopyLabelToBuffer(ReadOnlySpan<char> value, char[] destination, int destinationOffset, int capacity)", source);
            StringAssert.Contains("private static uint ResolveLabelHash(ReadOnlySpan<char> label)", source);
            StringAssert.Contains("private static int ResolveRenderedLabelLength(int sourceLength)", source);
            StringAssert.Contains("runtimeWaypoint.LabelHash = externalWaypoint.HasLabel ? externalWaypoint.LabelHash : DefaultExternalLabelHash;", source);
            StringAssert.Contains("runtimeWaypoint.HasLabel = externalWaypoint.HasLabel;", source);
            StringAssert.Contains("runtimeWaypoint.LabelSlotIndex = i;", source);
            StringAssert.Contains("runtimeWaypoint.LabelRevision = externalWaypoint.LabelRevision;", source);
            StringAssert.Contains("slot.CachedLabelSlotIndex != waypoint.LabelSlotIndex", source);
            StringAssert.Contains("slot.CachedLabelRevision != waypoint.LabelRevision", source);
            StringAssert.Contains("externalWaypoint.LabelRevision = labelRevision;", source);
            StringAssert.Contains("if (waypoint.HasLabel &&", source);
            StringAssert.Contains("labelSpan = new ReadOnlySpan<char>(_externalWaypointLabelBuffer, waypoint.LabelOffset, waypoint.LabelLength);", source);
            StringAssert.DoesNotContain("LabelHash != 0u", source);
            StringAssert.DoesNotContain("string.IsNullOrEmpty(externalWaypoint.Label)", source);
            StringAssert.Contains("void SetWaypoint(int id, Transform target, uint labelHash, ReadOnlySpan<char> label, Color color);", contracts);
            StringAssert.Contains("void SetWaypoint(int id, Vector3 worldPosition, uint labelHash, ReadOnlySpan<char> label, Color color);", contracts);
            StringAssert.Contains("NarrativeWaypointLabelHash", narrative);
            StringAssert.Contains("NarrativeWaypointLabel.AsSpan()", narrative);
            StringAssert.Contains("label.SetCharArray(_labelCharBuffer, 0, length);", applyLabel);
            StringAssert.DoesNotContain("string.IsNullOrEmpty", copyLabel);
            StringAssert.DoesNotContain("string.IsNullOrEmpty", copyLabelBank);
        }

        [Test]
        public void VehicleSubOsButtonKinematics_HoldsOnlyOneDataVaultWriteLockPerHelper()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs"));

            StringAssert.DoesNotContain("ButtonKinematicJob", source);
            StringAssert.DoesNotContain("IJobParallelFor", source);
            StringAssert.DoesNotContain("_buttonJob", source);
            StringAssert.DoesNotContain("TryAcquireButtonJobBuffers", source);
            StringAssert.DoesNotContain("ReleaseButtonJobBufferLocks", source);

            string update = ExtractMethodBody(source, "private void UpdateButtonKinematics(");
            StringAssert.DoesNotContain("TryAcquireWriteLock", update);
            StringAssert.DoesNotContain("ReleaseWriteLock", update);
            StringAssert.DoesNotContain(".Schedule(", update);

            string press = ExtractMethodBody(source, "private void PressCockpitButton(");
            StringAssert.DoesNotContain("TryAcquireWriteLock", press);
            StringAssert.DoesNotContain("ReleaseWriteLock", press);

            string byteWriter = ExtractMethodBody(source, "private bool TryWriteButtonByteValue(");
            string bufferWriter = ExtractMethodBody(source, "private bool TryWriteCockpitVaultBuffer<T>(");
            string telemetryAcquire = ExtractMethodBody(source, "private bool TryAcquireTelemetryWriteBuffer(");
            Assert.AreEqual(1, CountToken(byteWriter, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(byteWriter, "ReleaseWriteLock("));
            Assert.AreEqual(1, CountToken(bufferWriter, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(bufferWriter, "ReleaseWriteLock("));
            Assert.AreEqual(1, CountToken(telemetryAcquire, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(telemetryAcquire, "ReleaseWriteLock("));
            StringAssert.Contains("finally", byteWriter);
            StringAssert.Contains("finally", bufferWriter);
            StringAssert.Contains("finally", telemetryAcquire);
        }

        [Test]
        public void PdaDecryptionSpectrogramNativeInit_FlattensStageAndTelemetryLocks()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs"));
            string init = ExtractMethodBody(source, "private void EnsureNativeResources()");

            Assert.AreEqual(1, CountToken(init, "TryAcquireStageTargetsWrite("));
            Assert.AreEqual(1, CountToken(init, "TryAcquireTelemetryRingWrite("));
            Assert.AreEqual(2, CountToken(init, "finally"));
            StringAssert.DoesNotContain("telemetryLocked", init);
            StringAssert.DoesNotContain("ClearNativeState", source);
            StringAssert.Contains("private static void ClearStageTargets(", source);
            StringAssert.Contains("private static void ClearTelemetryRing(", source);

            int stageAcquire = init.IndexOf("TryAcquireStageTargetsWrite(", StringComparison.Ordinal);
            int stageRelease = init.IndexOf("vault.ReleaseWriteLock(in _stageTargetsHandle", StringComparison.Ordinal);
            int telemetryAcquire = init.IndexOf("TryAcquireTelemetryRingWrite(", StringComparison.Ordinal);
            int telemetryRelease = init.IndexOf("vault.ReleaseWriteLock(in _telemetryRingHandle", StringComparison.Ordinal);
            Assert.GreaterOrEqual(stageAcquire, 0);
            Assert.Greater(stageRelease, stageAcquire);
            Assert.Greater(telemetryAcquire, stageRelease);
            Assert.Greater(telemetryRelease, telemetryAcquire);
        }

        [Test]
        public void WristHudFrameBuild_UsesScratchBuffersAndSingleLockPublishers()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/WristHologramHudRuntime.cs"));
            string build = ExtractMethodBody(source, "private void BuildTextQuadsOwnerPhase(");

            StringAssert.DoesNotContain("TryAcquireHudFrameBuffers", source);
            StringAssert.DoesNotContain("TryAcquireAcousticWriteBuffers", source);
            StringAssert.DoesNotContain("WristHudWriteMask", source);
            StringAssert.DoesNotContain("ReleaseWristHudAcquiredBuffers", source);
            StringAssert.DoesNotContain("ReleaseWristHudAcquiredBuffers(acquiredMask)", build);
            StringAssert.DoesNotContain("TryAcquireWristHudVaultBuffer", build);
            StringAssert.Contains("PublishWristHudScratch(state.ActiveQuadCount);", build);
            string publish = ExtractMethodBody(source, "private void PublishWristHudScratch(");
            StringAssert.Contains("if (!FlushWristHudQuadScratch(quadCount))", publish);
            StringAssert.Contains("safeState.ActiveQuadCount = 0;", publish);
            StringAssert.Contains("safeState.Flags |= StateFlagGpuUploadFault;", publish);
            StringAssert.Contains("public Span<WristHudStateDTO> States;", source);
            StringAssert.Contains("public Span<WristHudQuadTransformDTO> Quads;", source);
            StringAssert.Contains("public Span<WristHudTelemetryEntry> Telemetry;", source);
            StringAssert.Contains("public Span<uint> Counters;", source);
            StringAssert.Contains("public ReadOnlySpan<AcousticEchoTap> AcousticTaps;", source);

            AssertWristHudSingleLockPublisher(source, "private bool FlushWristHudStateScratch(");
            AssertWristHudSingleLockPublisher(source, "private bool FlushWristHudQuadScratch(");
            AssertWristHudSingleLockPublisher(source, "private bool FlushWristHudTelemetryScratch(");
            AssertWristHudSingleLockPublisher(source, "private bool FlushWristHudCounterScratch(");
            AssertWristHudSingleLockPublisher(source, "private bool FlushAcousticTapScratchToVault(");
        }

        [Test]
        public void UiDataVaultAcquireHelpers_ReleaseFailedValidationInsideFinally()
        {
            AssertWriteAcquireHelperTransfersLockOnSuccessOnly(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs")),
                "private static bool TryAcquireGlitchVaultWriteBuffer<T>(");
            AssertWriteAcquireHelperTransfersLockOnSuccessOnly(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs")),
                "private static bool TryAcquireExistingVaultWriteBuffer<T>(");
            AssertWriteAcquireHelperTransfersLockOnSuccessOnly(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/WristHologramHudRuntime.cs")),
                "private bool TryAcquireWristHudVaultBuffer<T>(");
            AssertWriteAcquireHelperTransfersLockOnSuccessOnly(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs")),
                "private bool TryAcquireBlackBoxWriteBuffer(");
            AssertWriteAcquireHelperTransfersLockOnSuccessOnly(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs")),
                "private static bool TryAcquireVaultWriteBuffer<T>(");
        }

        [Test]
        public void TopographicalSonarTelemetryCursor_AdvancesOnlyAfterCursorVaultWrite()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs"));
            string body = ExtractMethodBody(source, "private void WriteTelemetry(uint flags)");

            int ringWrite = body.IndexOf("telemetry[index] = entry;");
            int cursorWrite = body.IndexOf("cursor[0] = nextIndex;");
            int localAdvance = body.IndexOf("_telemetryWriteIndex = nextIndex;");

            Assert.GreaterOrEqual(ringWrite, 0);
            Assert.Greater(cursorWrite, ringWrite);
            Assert.Greater(localAdvance, cursorWrite);
            StringAssert.DoesNotContain("cursor[0] = _telemetryWriteIndex;", body);
        }

        [Test]
        public void DiegeticGlitchColdWriters_UseSingleWriteLocksNotMutableResolveAliases()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs"));
            string initialize = ExtractMethodBody(source, "private void InitializeVaultDefaults()");
            string seedText = ExtractMethodBody(source, "private void SeedMockText()");
            string csvOverride = ExtractMethodBody(source, "private bool TryApplyCsvOverride(");
            string reloadTable = ExtractMethodBody(source, "public void ReloadGlitchTableForEditor()");

            StringAssert.DoesNotContain("TryResolveGlitchVaultBuffer", initialize);
            StringAssert.Contains("TrySeedGlitchStateDefaults()", initialize);
            StringAssert.Contains("TrySeedGlitchTuningDefaults()", initialize);
            StringAssert.Contains("TrySeedGlitchTableDefaults()", initialize);
            StringAssert.DoesNotContain("TryResolveGlitchVaultBuffer", seedText);
            StringAssert.DoesNotContain("TryResolveGlitchVaultBuffer", csvOverride);
            StringAssert.DoesNotContain("TryResolveGlitchVaultBuffer", reloadTable);
            StringAssert.DoesNotContain("TryLockBuffer", csvOverride);
            StringAssert.DoesNotContain("TryLockBuffer", reloadTable);
            StringAssert.Contains("stackalloc byte[CsvScratchCapacity]", csvOverride);

            AssertGlitchSingleLockWriter(source, "private bool TrySeedGlitchStateDefaults()");
            AssertGlitchSingleLockWriter(source, "private bool TrySeedGlitchTuningDefaults()");
            AssertGlitchSingleLockWriter(source, "private bool TrySeedGlitchTableDefaults()");
            AssertGlitchSingleLockWriter(source, "private bool TryWriteMockTextBuffer(");
            AssertGlitchSingleLockWriter(source, "private bool TryApplyCsvOverride(");
            AssertGlitchSingleLockWriter(source, "public void ReloadGlitchTableForEditor()");
        }

        [Test]
        public void DiegeticGlitchScheduledJobs_UseScratchAndSingleLockPublishers()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs"));
            string schedule = ExtractMethodBody(source, "private void ScheduleGlitchFrameJobs(");
            string drain = ExtractMethodBody(source, "private bool TryDrainActiveJobIfReady()");
            string resolve = ExtractMethodBody(source, "private bool TryResolveFramePointers(");
            string publish = ExtractMethodBody(source, "private bool PublishFrameScratchToVault()");

            StringAssert.DoesNotContain("TryLockBuffer", source);
            StringAssert.DoesNotContain("TryUnlockBuffer", source);
            StringAssert.DoesNotContain("TryLockScheduledBuffers", source);
            StringAssert.DoesNotContain("UnlockScheduledBuffers", source);
            StringAssert.DoesNotContain("private NativeArray<", source);
            StringAssert.DoesNotContain("H8Memory.Allocate<", source);
            StringAssert.Contains("TryLoadFrameScratchFromVault()", schedule);
            StringAssert.Contains("PublishFrameScratchToVault()", drain);
            StringAssert.Contains("state = _stateScratch;", resolve);
            StringAssert.Contains("table = _glitchTableScratch;", resolve);
            StringAssert.DoesNotContain("TryResolveGlitchVaultBuffer", resolve);
            StringAssert.Contains("H8Memory.AllocateRaw(", source);
            StringAssert.Contains("H8Memory.FreeRaw(buffer, Allocator.Persistent, SystemID.UI);", source);
            StringAssert.Contains("PublishGlitchScratchBuffer(in _stateHandle", publish);
            StringAssert.Contains("PublishGlitchScratchBuffer(in _telemetryCursorHandle", publish);
            AssertGlitchSingleLockWriter(source, "private bool PublishGlitchScratchBuffer<T>(");
        }

        [Test]
        public void PdaProjectionProjector_UsesScratchBuffersAndSingleLockVaultPublisher()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs"));

            StringAssert.DoesNotContain("TryAcquirePdaProjectionFrameBuffers", source);
            StringAssert.DoesNotContain("TryAcquirePdaProjectionWriteBuffers", source);
            StringAssert.DoesNotContain("ReleasePdaProjectionAcquiredBuffers", source);
            StringAssert.DoesNotContain("PdaProjectionWriteMask", source);

            string lateFrame = ExtractMethodBody(source, "private void PdaProjectorLateFrameTick()");
            StringAssert.DoesNotContain("TryAcquireWriteLock", lateFrame);
            StringAssert.DoesNotContain("ReleaseWriteLock", lateFrame);
            StringAssert.DoesNotContain("TryReadOnlyPdaProjectionVaultBuffer", lateFrame);
            StringAssert.Contains("_pdaProjectionStateScratch.AsSpan()", lateFrame);
            StringAssert.Contains("PublishPdaProjectionFrameScratch(in stateSnapshot)", lateFrame);

            string nativeInit = ExtractMethodBody(source, "private bool EnsurePdaProjectionNativeBuffers()");
            StringAssert.DoesNotContain("TryAcquireWriteLock", nativeInit);
            StringAssert.DoesNotContain("ReleaseWriteLock", nativeInit);
            StringAssert.Contains("FlushPdaProjectionTuningScratch()", nativeInit);
            StringAssert.Contains("FlushPdaProjectionProfilesScratch()", nativeInit);

            string publish = ExtractMethodBody(source, "private bool PublishPdaProjectionFrameScratch(");
            StringAssert.Contains("safeState.PdaFlags |= PdaProjectionFlagGpuUploadFault;", publish);
            StringAssert.Contains("_ = FlushPdaProjectionStateScratch();", publish);

            string writer = ExtractMethodBody(source, "private bool TryWritePdaProjectionVaultBuffer<T>(");
            Assert.AreEqual(1, CountToken(writer, "TryAcquireWriteLock("));
            Assert.AreEqual(1, CountToken(writer, "ReleaseWriteLock("));
            Assert.AreEqual(1, CountToken(writer, "finally"));
        }

        [Test]
        public void BeaconHudDistanceUnitCache_UsesSpanLabelRouteNotManagedStringResolver()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/BeaconHUDElement.cs"));
            string formatterSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocalizedMeasurementFormatter.cs"));
            string handler = ExtractMethodBody(source, "private void HandleLanguageChanged(");
            string registryRebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string applyPending = ExtractMethodBody(source, "private void ApplyPendingLocalizationRefresh()");
            string cache = ExtractMethodBody(source, "private void RebuildLocalizationCache(");

            StringAssert.Contains("System.ReadOnlySpan<char> unitLabel = LocalizedMeasurementFormatter.ResolveDistanceUnitLabelSpan(_distanceLanguage, manager);", cache);
            StringAssert.Contains("if (unitLabel.Length == 0)", cache);
            StringAssert.DoesNotContain("string unitLabel", cache);
            StringAssert.DoesNotContain("string.IsNullOrEmpty(unitLabel)", cache);
            StringAssert.DoesNotContain("ResolveDistanceUnitLabel(_distanceLanguage)", cache);
            StringAssert.Contains("public static ReadOnlySpan<char> ResolveDistanceUnitLabelSpan", formatterSource);
            StringAssert.Contains("public static ReadOnlySpan<char> ResolveTemperatureUnitLabelSpan", formatterSource);
            StringAssert.DoesNotContain("public static string ResolveDistanceUnitLabel", formatterSource);
            StringAssert.DoesNotContain("public static string ResolveTemperatureUnitLabel", formatterSource);
            StringAssert.DoesNotContain("GlobalRegistry.LocalizationText", formatterSource);

            StringAssert.Contains("QueueLocalizationPresentationRefresh(language);", handler);
            StringAssert.DoesNotContain("RebuildLocalizationCache", handler);
            StringAssert.DoesNotContain("InvalidateDisplayCaches", handler);
            StringAssert.Contains("QueueLocalizationPresentationRefresh(ResolveCachedDistanceLanguage());", registryRebind);
            StringAssert.DoesNotContain("RebuildLocalizationCache();", registryRebind);
            StringAssert.DoesNotContain("InvalidateDisplayCaches();", registryRebind);
            StringAssert.Contains("ApplyPendingLocalizationRefresh();", lateFrame);
            StringAssert.Contains("RebuildLocalizationCache(_pendingDistanceLanguage);", applyPending);
            StringAssert.Contains("InvalidateDisplayCaches();", applyPending);
        }

        [Test]
        public void InteractionPromptLocalizationPresentation_QueuesLateFrameRefresh()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/InteractionUI.cs"));
            string languageHandler = ExtractMethodBody(source, "private void HandleLanguageChanged(");
            string inputHandler = ExtractMethodBody(source, "private void HandleInputDisplayStyleChanged(");
            string registryRebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string queue = ExtractMethodBody(source, "private void QueuePromptPresentationRefresh(");
            string applyPending = ExtractMethodBody(source, "private void ApplyPendingPromptPresentationRefresh()");

            StringAssert.Contains("QueuePromptPresentationRefresh(resetPrompt: true);", languageHandler);
            StringAssert.DoesNotContain("ConfigurePromptText", languageHandler);
            StringAssert.DoesNotContain("RefreshLocalizedPromptCache", languageHandler);
            StringAssert.Contains("QueuePromptPresentationRefresh(resetPrompt: true);", inputHandler);
            StringAssert.DoesNotContain("RefreshLocalizedPromptCache", inputHandler);
            StringAssert.Contains("QueuePromptPresentationRefresh(resetPrompt: true);", registryRebind);
            StringAssert.DoesNotContain("RefreshLocalizedPromptCache();", registryRebind);
            StringAssert.Contains("ApplyPendingPromptPresentationRefresh();", lateFrame);
            StringAssert.Contains("_promptPresentationDirty = true;", queue);
            StringAssert.Contains("ClearPromptBuildCache();", queue);
            StringAssert.Contains("ConfigurePromptText();", applyPending);
            StringAssert.Contains("RefreshLocalizedPromptCache();", applyPending);
        }

        [Test]
        public void InteractionPromptRuntime_UsesPromptSourceSpanRouteNotStringBuilders()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/InteractionUI.cs"));
            string samplePromptState = ExtractMethodBody(source, "private void SamplePromptState(");
            string tryApply = ExtractMethodBody(source, "private bool TryApplyPromptSource(");
            string applySpan = ExtractMethodBody(source, "private void ApplyPromptSpan(");
            string textProvider = ExtractMethodBody(source, "private bool TryUpdatePromptFromTextProvider(");

            StringAssert.Contains("private enum PromptSource : byte", source);
            StringAssert.Contains("private PromptSource _currentPromptSource;", source);
            StringAssert.Contains("private PromptSource _cachedPromptSource;", source);
            StringAssert.DoesNotContain("private string _currentPromptSource;", source);
            StringAssert.DoesNotContain("private string _cachedPrompt;", source);
            StringAssert.DoesNotContain("private string BuildPrompt(", source);
            StringAssert.DoesNotContain("private string BuildPromptUncached(", source);
            StringAssert.DoesNotContain("private void UpdatePrompt(string prompt)", source);
            StringAssert.DoesNotContain("private void ApplyPromptText(string prompt)", source);

            StringAssert.Contains("TryResolvePromptSource(promptCollider, in targetInfo, out PromptSource promptSource)", samplePromptState);
            StringAssert.Contains("TryApplyPromptSource(promptSource)", samplePromptState);
            StringAssert.DoesNotContain("string prompt =", samplePromptState);
            StringAssert.DoesNotContain("BuildPrompt(", samplePromptState);
            StringAssert.DoesNotContain("UpdatePrompt(", samplePromptState);

            StringAssert.Contains("ReadOnlySpan<char> prompt = ResolvePromptSourceSpan(promptSource, out string eventPrompt);", tryApply);
            StringAssert.Contains("ApplyPromptSpan(prompt);", tryApply);
            StringAssert.Contains("OnPromptChanged?.Invoke(eventPrompt);", tryApply);
            StringAssert.Contains("localization.TryExpandText(prompt, _promptCharBuffer, out int expandedLength)", applySpan);
            StringAssert.Contains("prompt.Slice(0, copyLength).CopyTo(_promptCharBuffer);", applySpan);
            StringAssert.Contains("promptText.SetCharArray(_promptCharBuffer, 0, copyLength);", applySpan);
            StringAssert.Contains("textProvider.TryCopyInteractText(_promptCharBuffer, out int length)", textProvider);
            StringAssert.Contains("promptText.SetCharArray(_promptCharBuffer, 0, safeLength);", textProvider);
        }

        [Test]
        public void SuitHudLocalizationPresentation_QueuesLateFrameRefresh()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs"));
            string languageHandler = ExtractMethodBody(source, "private void HandleLanguageChanged(");
            string registryRebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string queue = ExtractMethodBody(source, "private void QueueLocalizedPresentationRefresh(");
            string process = ExtractMethodBody(source, "private void ProcessPendingRuntimeCanvasRefresh()");

            StringAssert.Contains("QueueLocalizedPresentationRefresh(forceResolve: false, refreshDepthSignal: false);", languageHandler);
            StringAssert.DoesNotContain("RebuildLocalizationCache", languageHandler);
            StringAssert.DoesNotContain("InvalidateVisualCaches", languageHandler);
            StringAssert.Contains("QueueLocalizedPresentationRefresh(forceResolve: false, refreshDepthSignal: false);", registryRebind);
            StringAssert.DoesNotContain("RebuildLocalizationCache();", registryRebind);
            StringAssert.Contains("_localizedPresentationDirty = true;", queue);
            StringAssert.Contains("QueueRuntimeCanvasRefresh(forceResolve, refreshDepthSignal);", queue);
            StringAssert.Contains("TryRegisterRuntimeTick();", queue);
            StringAssert.Contains("_localizedPresentationDirty ||", process);
            StringAssert.Contains("RebuildLocalizationCache();", process);
            StringAssert.Contains("InvalidateVisualCaches();", process);
        }

        [Test]
        public void AcousticEcholocationLocalizationPresentation_QueuesLateFrameRefresh()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/AcousticEcholocationTranslator.cs"));
            string languageHandler = ExtractMethodBody(source, "private void HandleLanguageChanged(");
            string registryRebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string queue = ExtractMethodBody(source, "private void QueueLocalizationPresentationRefresh()");
            string applyPending = ExtractMethodBody(source, "private void ApplyPendingLocalizationRefresh()");

            StringAssert.Contains("QueueLocalizationPresentationRefresh();", languageHandler);
            StringAssert.DoesNotContain("RefreshLocalizedCache", languageHandler);
            StringAssert.Contains("QueueLocalizationPresentationRefresh();", registryRebind);
            StringAssert.DoesNotContain("RefreshLocalizedCache();", registryRebind);
            StringAssert.Contains("ApplyPendingLocalizationRefresh();", lateFrame);
            StringAssert.Contains("_localizedPresentationDirty = true;", queue);
            StringAssert.Contains("RegisterToTickManager();", queue);
            StringAssert.Contains("RefreshLocalizedCache();", applyPending);
        }

        [Test]
        public void FontStreamingVisiblePrefetch_UsesLateFrameSlicesWithoutDataVaultOrJobLocks()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/FontStreamingManager.cs"));
            string schedulerSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/LabelSwapScheduler.cs"));
            string registrySource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs"));

            StringAssert.DoesNotContain("TryAcquireWriteLock", source);
            StringAssert.DoesNotContain("ReleaseWriteLock", source);
            StringAssert.DoesNotContain("TryScheduleVisibleTextOffsetPrefetch", source);
            StringAssert.DoesNotContain("JobHandle", source);
            StringAssert.DoesNotContain("IDataVault", source);
            StringAssert.DoesNotContain("VisibleHashPrefetchBufferId", source);
            string languageChangeBody = ExtractMethodBody(source, "private void HandleLanguageChanged(");
            StringAssert.DoesNotContain("UpdateStatusLabel", languageChangeBody);
            StringAssert.DoesNotContain("ApplyVisibleAlpha", languageChangeBody);
            StringAssert.Contains("LocRegistry.ResolveVisibleTextOffsetPrefetchBudget(registeredCount)", source);
            StringAssert.Contains("LocRegistry.TryResolveVisibleTextOffsetSlice(keyHash, out prefetchedSlice)", source);
            StringAssert.Contains("_swapScheduler.Enqueue(entry, prefetchedSlice, hasPrefetchedSlice)", source);
            StringAssert.Contains("public bool Enqueue(TMP_TextEntry entry, int2 utf8Slice, bool hasPrefetchedSlice)", schedulerSource);
            StringAssert.Contains("public static int ResolveVisibleTextOffsetPrefetchBudget(int requestedCount)", registrySource);
            StringAssert.Contains("public static bool TryResolveVisibleTextOffsetSlice(uint keyHash, out int2 slice)", registrySource);
        }

        [Test]
        public void LocalizationLanguageChangedPresentation_QueuesLateFrameForPdaChromeAndDataLog()
        {
            string dataLogSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADataLogTab.cs"));
            string chromeSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDAShellChrome.cs"));

            string dataLogHandler = ExtractMethodBody(dataLogSource, "private void HandleLanguageChanged(");
            StringAssert.Contains("_localizedPresentationDirty = true", dataLogHandler);
            StringAssert.DoesNotContain("ResetDetailNarrativeState", dataLogHandler);
            StringAssert.DoesNotContain("RebuildLocalizationCache", dataLogHandler);
            StringAssert.DoesNotContain("ApplyLocalizedStaticText", dataLogHandler);
            StringAssert.DoesNotContain("RefreshList", dataLogHandler);
            StringAssert.DoesNotContain("RefreshDetail", dataLogHandler);
            StringAssert.DoesNotContain("RefreshPlayButton", dataLogHandler);

            string dataLogLateFrame = ExtractMethodBody(dataLogSource, "public void LateFrameTick()");
            StringAssert.Contains("_localizedPresentationDirty", dataLogLateFrame);
            StringAssert.Contains("ResetDetailNarrativeState(clearPendingDecryption: false)", dataLogLateFrame);
            StringAssert.Contains("RebuildLocalizationCache();", dataLogLateFrame);
            StringAssert.Contains("ApplyLocalizedStaticText();", dataLogLateFrame);
            StringAssert.Contains("RefreshList();", dataLogLateFrame);
            StringAssert.Contains("RefreshDetail();", dataLogLateFrame);
            StringAssert.Contains("RefreshPlayButton();", dataLogLateFrame);

            string chromeHandler = ExtractMethodBody(chromeSource, "private void HandleLanguageChanged(");
            StringAssert.Contains("QueueLocalizedChromeRefresh();", chromeHandler);
            StringAssert.DoesNotContain("RefreshLocalizedTextCache", chromeHandler);
            StringAssert.DoesNotContain("RefreshChrome", chromeHandler);

            string chromeQueue = ExtractMethodBody(chromeSource, "private void QueueLocalizedChromeRefresh()");
            StringAssert.Contains("_localizedChromeDirty = true", chromeQueue);
            StringAssert.Contains("InvalidateAppliedLabelVersions();", chromeQueue);

            string chromeLateFrame = ExtractMethodBody(chromeSource, "public void LateFrameTick()");
            StringAssert.Contains("_localizedChromeDirty", chromeLateFrame);
            StringAssert.Contains("RefreshLocalizedTextCache();", chromeLateFrame);
            StringAssert.Contains("RefreshChrome();", chromeLateFrame);
        }

        private static void AssertRuntimeSourceTreeHasNoForbiddenTextBridges(string root)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsEditorSourcePath(files[i]))
                    continue;

                AssertRuntimeSourceFileHasNoForbiddenTextBridges(files[i]);
            }
        }

        private static bool IsEditorSourcePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AssertRuntimeSourceFileHasNoForbiddenTextBridges(string path)
        {
            if (!File.Exists(path))
                return;

            string source = File.ReadAllText(path);
            AssertForbiddenTextBridgeAbsent(source, path, "string.Format");
            AssertForbiddenTextBridgeAbsent(source, path, ".ToString(");
            AssertForbiddenTextBridgeAbsent(source, path, "TMP_Text.text =");
            AssertForbiddenTextBridgeAbsent(source, path, ".text =");
            AssertForbiddenTextBridgeAbsent(source, path, "new string(");
            AssertForbiddenTextBridgeAbsent(source, path, "StringBuilder");
            AssertForbiddenTextBridgeAbsent(source, path, "Array.Resize");
            AssertForbiddenTextBridgeAbsent(source, path, "foreach (");
            AssertForbiddenTextBridgeAbsent(source, path, ".ToCharArray()");
        }

        private static void AssertForbiddenTextBridgeAbsent(string source, string path, string token)
        {
            Assert.Less(source.IndexOf(token, StringComparison.Ordinal), 0, path + " contains forbidden token " + token);
        }

        private static void AssertRuntimeHotMethodsHaveNoColdLookups(string root)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsEditorSourcePath(files[i]))
                    continue;

                AssertRuntimeHotMethodFileHasNoColdLookups(files[i]);
            }
        }

        private static void AssertRuntimeHotMethodFileHasNoColdLookups(string path)
        {
            if (!File.Exists(path))
                return;

            string source = File.ReadAllText(path);
            AssertHotMethodsHaveNoColdLookups(source, path, "Tick");
            AssertHotMethodsHaveNoColdLookups(source, path, "FixedUpdate");
            AssertHotMethodsHaveNoColdLookups(source, path, "LateFrameTick");
            AssertHotMethodsHaveNoColdLookups(source, path, "Execute");
        }

        private static void AssertHotMethodsHaveNoColdLookups(string source, string path, string methodName)
        {
            string needle = methodName + "(";
            int search = 0;
            while (search < source.Length)
            {
                int nameIndex = source.IndexOf(needle, search, StringComparison.Ordinal);
                if (nameIndex < 0)
                    return;

                search = nameIndex + needle.Length;
                if (!LooksLikeMethodDeclaration(source, nameIndex))
                    continue;

                int bodyStart = source.IndexOf('{', nameIndex);
                if (bodyStart < 0)
                    continue;

                int bodyEnd = FindMatchingBrace(source, bodyStart);
                if (bodyEnd <= bodyStart)
                    continue;

                string body = source.Substring(bodyStart, bodyEnd - bodyStart + 1);
                AssertForbiddenTextBridgeAbsent(body, path + "::" + methodName, "GlobalRegistry.Get<");
                AssertForbiddenTextBridgeAbsent(body, path + "::" + methodName, "FindObjectOfType");
                AssertForbiddenTextBridgeAbsent(body, path + "::" + methodName, "FindObjectsOfType");
                AssertForbiddenTextBridgeAbsent(body, path + "::" + methodName, "GameObject.Find");
                AssertForbiddenTextBridgeAbsent(body, path + "::" + methodName, "Camera.main");
                AssertNoGetComponentCall(body, path + "::" + methodName);
                search = bodyEnd + 1;
            }
        }

        private static bool LooksLikeMethodDeclaration(string source, int nameIndex)
        {
            int previous = nameIndex - 1;
            if (previous >= 0)
            {
                char c = source[previous];
                if (c == '.' || c == '_' || char.IsLetterOrDigit(c))
                    return false;
            }

            int lineStart = source.LastIndexOf('\n', nameIndex);
            int statementStart = Math.Max(0, lineStart + 1);
            string prefix = source.Substring(statementStart, nameIndex - statementStart);
            return prefix.IndexOf("=>", StringComparison.Ordinal) < 0 &&
                   prefix.IndexOf("=", StringComparison.Ordinal) < 0;
        }

        private static void AssertNoGetComponentCall(string source, string label)
        {
            int search = 0;
            while (search < source.Length)
            {
                int index = source.IndexOf("GetComponent", search, StringComparison.Ordinal);
                if (index < 0)
                    return;

                search = index + "GetComponent".Length;
                if (index >= 3 && source.Substring(index - 3, 3) == "Try")
                    continue;

                Assert.Fail(label + " contains forbidden GetComponent call");
            }
        }

        private static string ExtractMethodBody(string source, string marker)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, marker);
            int bodyStart = source.IndexOf('{', start);
            Assert.GreaterOrEqual(bodyStart, start, marker);
            int bodyEnd = FindMatchingBrace(source, bodyStart);
            Assert.Greater(bodyEnd, bodyStart, marker);
            return source.Substring(bodyStart, bodyEnd - bodyStart + 1);
        }

        private static int FindMatchingBrace(string source, int bodyStart)
        {
            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int search = 0;
            while (search < source.Length)
            {
                int index = source.IndexOf(token, search, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                search = index + token.Length;
            }

            return count;
        }

        private static void AssertWristHudSingleLockPublisher(string source, string marker)
        {
            string body = ExtractMethodBody(source, marker);
            Assert.AreEqual(1, CountToken(body, "TryAcquireWristHudVaultBuffer("), marker);
            Assert.AreEqual(1, CountToken(body, "ReleaseWriteLock("), marker);
            Assert.AreEqual(1, CountToken(body, "finally"), marker);
        }

        private static void AssertWriteAcquireHelperTransfersLockOnSuccessOnly(string source, string marker)
        {
            string body = ExtractMethodBody(source, marker);
            Assert.AreEqual(1, CountToken(body, "TryAcquireWriteLock("), marker);
            Assert.AreEqual(1, CountToken(body, "ReleaseWriteLock("), marker);
            Assert.AreEqual(1, CountToken(body, "finally"), marker);
            StringAssert.Contains("bool releaseOnExit = true;", body);
            StringAssert.Contains("releaseOnExit = false;", body);
            StringAssert.Contains("if (releaseOnExit)", body);
            int finallyIndex = body.IndexOf("finally", StringComparison.Ordinal);
            int releaseIndex = body.IndexOf("ReleaseWriteLock(", StringComparison.Ordinal);
            Assert.Greater(releaseIndex, finallyIndex, marker);
        }

        private static void AssertGlitchSingleLockWriter(string source, string marker)
        {
            string body = ExtractMethodBody(source, marker);
            Assert.AreEqual(1, CountToken(body, "TryAcquireGlitchVaultWriteBuffer("), marker);
            Assert.AreEqual(1, CountToken(body, "ReleaseGlitchVaultWriteBuffer("), marker);
            Assert.AreEqual(1, CountToken(body, "finally"), marker);
            int acquireIndex = body.IndexOf("TryAcquireGlitchVaultWriteBuffer(", StringComparison.Ordinal);
            int finallyIndex = body.IndexOf("finally", StringComparison.Ordinal);
            int releaseIndex = body.IndexOf("ReleaseGlitchVaultWriteBuffer(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(acquireIndex, 0, marker);
            Assert.Greater(finallyIndex, acquireIndex, marker);
            Assert.Greater(releaseIndex, finallyIndex, marker);
        }
    }
}
