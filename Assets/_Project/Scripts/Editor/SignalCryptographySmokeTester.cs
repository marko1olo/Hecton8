#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only source smoke coverage for Atlas signal cryptography and progression contracts.
    /// </summary>
    public static class SignalCryptographySmokeTester
    {
        private const string OutputRelativePath = "Library/SignalCryptographySmokeTester.json";
        private const string SignalBeaconPath = "Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs";
        private const string AtlasSignalDecoderPath = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs";
        private const string AtlasSignalEventsPath = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs";
        private const string Atlas6DirectiveSystemPath = "Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs";
        private const string PdaSpectrogramPath = "Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs";
        private const string PdaAtlasSignalTabPath = "Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs";
        private const string AudioLogSystemPath = "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs";
        private const string SaveDataPath = "Assets/_Project/Scripts/SaveData.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string NotificationEventsPath = "Assets/_Project/Scripts/UI/NotificationEvents.cs";
        private const string ItemTemplateRegistryPath = "Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs";
        private const string QuestStateManagerPath = "Assets/_Project/Scripts/Quest/QuestStateManager.cs";
        private const string QuestEventsPath = "Assets/_Project/Scripts/Quest/QuestEvents.cs";
        private const string QuestGraphEvaluatorPath = "Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs";
        private const string NarrativeEventsPath = "Assets/_Project/Scripts/NarrativeEvents.cs";
        private const string CraftingEventsPath = "Assets/_Project/Scripts/CraftingEvents.cs";
        private const string InteractionEventsPath = "Assets/_Project/Scripts/Interaction/InteractionEvents.cs";
        private const string BiomeMatrixDirectorPath = "Assets/_Project/Scripts/BiomeMatrixDirector.cs";
        private const string BuildableDataPath = "Assets/_Project/Scripts/BuildableData.cs";
        private const string ModuleCatalogPath = "Assets/_Project/Scripts/ModuleCatalog.cs";
        private const string PlayerBuilderPath = "Assets/_Project/Scripts/PlayerBuilder.cs";
        private const string PdaConstructionTabPath = "Assets/_Project/Scripts/UI/PDAConstructionTab.cs";
        private const string PlayerAchievementRegistryPath = "Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs";
        private const string PdaContextualAdvisorySystemPath = "Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs";

        [MenuItem("Hecton8/Atlas/Run Signal Cryptography Smoke Test", priority = 320)]
        public static void RunMenuItem()
        {
            bool passed = Execute(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);
        }

        public static void ExecuteBatch()
        {
            bool passed = Execute(out string json);
            WriteReport(json);
            Debug.Log("[SignalCryptographySmokeTester] " + json);

            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Execute(out string json)
        {
            int failureCount = 0;
            StringBuilder report = new StringBuilder(4096); // COLD ALLOC: StringBuilder[4096] — editor smoke JSON/report staging — owner: SignalCryptographySmokeTester
            report.Append("[SignalCryptographySmokeTester]");

            string beacon = ReadAssetText(SignalBeaconPath, report, ref failureCount);
            string decoder = ReadAssetText(AtlasSignalDecoderPath, report, ref failureCount);
            string atlasSignalEvents = ReadAssetText(AtlasSignalEventsPath, report, ref failureCount);
            string atlas6DirectiveSystem = ReadAssetText(Atlas6DirectiveSystemPath, report, ref failureCount);
            string spectrogram = ReadAssetText(PdaSpectrogramPath, report, ref failureCount);
            string pdaAtlasSignalTab = ReadAssetText(PdaAtlasSignalTabPath, report, ref failureCount);
            string audioLogSystem = ReadAssetText(AudioLogSystemPath, report, ref failureCount);
            string saveData = ReadAssetText(SaveDataPath, report, ref failureCount);
            string spatialAudio = ReadAssetText(SpatialAudioPath, report, ref failureCount);
            string notificationEvents = ReadAssetText(NotificationEventsPath, report, ref failureCount);
            string itemRegistry = ReadAssetText(ItemTemplateRegistryPath, report, ref failureCount);
            string questState = ReadAssetText(QuestStateManagerPath, report, ref failureCount);
            string questEvents = ReadAssetText(QuestEventsPath, report, ref failureCount);
            string questGraphEvaluator = ReadAssetText(QuestGraphEvaluatorPath, report, ref failureCount);
            string narrativeEvents = ReadAssetText(NarrativeEventsPath, report, ref failureCount);
            string craftingEvents = ReadAssetText(CraftingEventsPath, report, ref failureCount);
            string interactionEvents = ReadAssetText(InteractionEventsPath, report, ref failureCount);
            string biomeMatrixDirector = ReadAssetText(BiomeMatrixDirectorPath, report, ref failureCount);
            string buildableData = ReadAssetText(BuildableDataPath, report, ref failureCount);
            string moduleCatalog = ReadAssetText(ModuleCatalogPath, report, ref failureCount);
            string playerBuilder = ReadAssetText(PlayerBuilderPath, report, ref failureCount);
            string pdaConstructionTab = ReadAssetText(PdaConstructionTabPath, report, ref failureCount);
            string playerAchievementRegistry = ReadAssetText(PlayerAchievementRegistryPath, report, ref failureCount);
            string pdaContextualAdvisorySystem = ReadAssetText(PdaContextualAdvisorySystemPath, report, ref failureCount);

            RunSignalBeaconAudit(beacon, report, ref failureCount);
            RunAtlasSignalEventsAudit(atlasSignalEvents, report, ref failureCount);
            RunAtlas6EventsAudit(atlas6DirectiveSystem, report, ref failureCount);
            RunQuestProgressionEventAudit(questEvents, questGraphEvaluator, report, ref failureCount);
            RunUpstreamProgressionEventAudit(narrativeEvents, craftingEvents, interactionEvents, biomeMatrixDirector, report, ref failureCount);
            RunSpectrogramAudit(decoder, spectrogram, report, ref failureCount);
            RunPdaAtlasSignalAudit(pdaAtlasSignalTab, report, ref failureCount);
            RunEncryptedLogAudit(audioLogSystem, saveData, spatialAudio, notificationEvents, report, ref failureCount);
            RunBlueprintGateAudit(itemRegistry, questState, buildableData, moduleCatalog, playerBuilder, pdaConstructionTab, report, ref failureCount);
            RunProgressionNotificationCacheAudit(playerAchievementRegistry, pdaContextualAdvisorySystem, notificationEvents, report, ref failureCount);

            bool passed = failureCount == 0;
            json = "{\"tester\":\"SignalCryptographySmokeTester\",\"status\":\"" +
                   (passed ? "PASS" : "FAIL") +
                   "\",\"failureCount\":" +
                   failureCount +
                   ",\"details\":\"" +
                   EscapeJson(report.ToString()) +
                   "\"}";
            return passed;
        }

        private static void RunSignalBeaconAudit(string beacon, StringBuilder report, ref int failureCount)
        {
            if (beacon.Length == 0)
                return;

            string solveWrapperBody = ExtractMethodBody(beacon, "public static void SolveTriangulatedStrength(");
            string solveBody = ExtractMethodBody(beacon, "private static void SolveTriangulatedStrengthKernel(");
            string breadcrumbBody = ExtractMethodBody(beacon, "private void EmitBreadcrumb()");
            string runtimeCacheBody = ExtractMethodBody(beacon, "private void RefreshBeaconAupCache(bool force)");
            string tickBody = ExtractMethodBody(beacon, "public void Tick(float deltaTime)");
            string sineMatchBody = ExtractMethodBody(beacon, "private static float EvaluateSineWaveMatchKernel(");
            string registryPublishBody = ExtractMethodBody(beacon, "public static void PublishTelemetry(int slot, in SignalBeaconTelemetry telemetry)");
            string registryTryDominantTelemetryBody = ExtractMethodBody(beacon, "public static bool TryGetDominantTelemetry(out float strength01, out float static01)");
            string registryRebuildBody = ExtractMethodBody(beacon, "private static void RebuildDominantFromTelemetrySlots()");

            AssertContains(beacon, "[BurstCompile(FloatMode = FloatMode.Fast", "Signal beacon math has Burst fast-mode annotation", report, ref failureCount);
            AssertContains(beacon, "BurstCompiler.CompileFunctionPointer<SolveTriangulatedStrengthDelegate>", "Triangulation solver is compiled as Burst function pointer", report, ref failureCount);
            AssertContains(beacon, "BurstCompiler.CompileFunctionPointer<EvaluateSineWaveMatchDelegate>", "Spectrogram match solver is compiled as Burst function pointer", report, ref failureCount);
            AssertContains(beacon, "BurstCompiler.CompileFunctionPointer<MergeRecoveredBitsDelegate>", "Fragment bit merge solver is compiled as Burst function pointer", report, ref failureCount);
            AssertContains(solveWrapperBody, "_solveTriangulatedStrength.Invoke(", "Public triangulation entry invokes Burst function pointer", report, ref failureCount);
            AssertContains(solveBody, "AbsoluteUniversePosition.DistanceSq(in playerAup, in point0)", "Triangulation reads AUP distance squared for point0", report, ref failureCount);
            AssertContains(solveBody, "AbsoluteUniversePosition.DistanceSq(in playerAup, in point1)", "Triangulation reads AUP distance squared for point1", report, ref failureCount);
            AssertContains(solveBody, "AbsoluteUniversePosition.DistanceSq(in playerAup, in point2)", "Triangulation reads AUP distance squared for point2", report, ref failureCount);
            AssertContains(solveBody, "averageDistanceSq = (distanceSq0 + distanceSq1 + distanceSq2) * OneThird", "Triangulation strength uses averaged distance squared", report, ref failureCount);
            AssertContains(solveBody, "!math.isfinite(averageDistanceSq)", "Triangulation fail-closes non-finite averaged distance", report, ref failureCount);
            AssertContains(solveBody, "math.isfinite(maxRangeMeters)", "Triangulation sanitizes max range before squared math", report, ref failureCount);
            AssertContains(solveBody, "Static01 = math.saturate((1f - strength) + errorNoise)", "HUD static derives from inverse strength plus error noise", report, ref failureCount);
            AssertContains(sineMatchBody, "!math.isfinite(targetFrequencyHz)", "Sine-wave match rejects non-finite target frequency", report, ref failureCount);
            AssertContains(sineMatchBody, "float safeInputPhase01 = math.frac(inputPhase01)", "Sine-wave match wraps sanitized input phase", report, ref failureCount);
            AssertContains(sineMatchBody, "float safeFrequencyTolerance = math.isfinite(frequencyToleranceHz)", "Sine-wave match clamps non-finite frequency tolerance", report, ref failureCount);
            AssertContains(sineMatchBody, "EvaluateSineProxy(safeInputPhase01)", "Sine-wave proxy uses sanitized input phase", report, ref failureCount);
            AssertContains(beacon, "ListenerCaveInterior01", "Cave interior scalar feeds signal interference", report, ref failureCount);
            AssertContains(beacon, "caveErrorNoiseMultiplier", "Beacon exposes cave error-noise multiplier", report, ref failureCount);
            AssertContains(beacon, "math.saturate(spatialAudio.ListenerCaveInterior01)", "Cave interference scalar is saturated before lerp", report, ref failureCount);
            AssertContains(beacon, "_lastPublishedShaderStatic01", "Atlas static shader publishing is cached", report, ref failureCount);
            AssertContains(beacon, "math.abs(shaderStatic - _lastPublishedShaderStatic01) <= 0.0001f", "Atlas static shader publish skips unchanged values", report, ref failureCount);
            AssertContains(beacon, "private static void PublishDominantStaticToShaderValue()", "Atlas static shader publish has registry-level refresh helper", report, ref failureCount);
            AssertContains(beacon, "private static float _dominantStatic01", "Beacon registry tracks dominant static independently from strength", report, ref failureCount);
            AssertContains(beacon, "private static int _dominantStaticSlot = -1", "Beacon registry tracks static-owner slot for cheap invalidation", report, ref failureCount);
            AssertContains(registryTryDominantTelemetryBody, "static01 = _dominantStatic01", "PDA/shader reads max static rather than strength-dominant static", report, ref failureCount);
            AssertContains(registryPublishBody, "telemetry.Static01 >= _dominantStatic01", "Beacon publish updates max static without full scan", report, ref failureCount);
            AssertContains(registryPublishBody, "slot == _dominantStaticSlot && telemetry.Static01 < previousTelemetry.Static01", "Beacon publish rebuilds static owner after static drop", report, ref failureCount);
            AssertContains(registryRebuildBody, "candidate.Static01 >= _dominantStatic01", "Beacon registry rebuild recomputes max static across telemetry slots", report, ref failureCount);
            AssertContains(beacon, "slot == _dominantSlot || slot == _dominantStaticSlot", "Beacon telemetry clear rebuilds both dominant strength and max static", report, ref failureCount);
            AssertContains(beacon, "UnregisterBeaconAndRefreshShaderStatic()", "Beacon unregister refreshes global static state", report, ref failureCount);
            AssertContains(tickBody, "_solveTimer = math.min(_solveTimer - solvePeriod, solvePeriod)", "Beacon solve timer preserves residual cadence", report, ref failureCount);
            AssertContains(tickBody, "_bipTimer = math.min(_bipTimer - safeBipPeriod, safeBipPeriod)", "Beacon breadcrumb timer preserves residual cadence", report, ref failureCount);
            AssertContains(breadcrumbBody, "AcousticPingEvent pingEvent = new AcousticPingEvent(", "Beacon creates acoustic breadcrumb event", report, ref failureCount);
            AssertContains(breadcrumbBody, "float safeBipRadiusMeters = math.max(0f, bipRadiusMeters)", "Beacon clamps breadcrumb radius before event publish", report, ref failureCount);
            AssertContains(breadcrumbBody, "float safeBipPeriodSeconds = math.max(0.02f, bipPeriodSeconds)", "Beacon clamps breadcrumb period before event publish", report, ref failureCount);
            AssertContains(breadcrumbBody, "safeBipRadiusMeters", "Beacon event uses safe breadcrumb radius", report, ref failureCount);
            AssertContains(breadcrumbBody, "safeBipPeriodSeconds", "Beacon event uses safe breadcrumb period", report, ref failureCount);
            AssertContains(breadcrumbBody, "PhysicsEventBus.NotifyAcousticPing(in pingEvent)", "Beacon sends breadcrumb through PhysicsEventBus", report, ref failureCount);
            AssertContains(breadcrumbBody, "10f);", "Beacon breadcrumb carries the required 10 Hz bip marker", report, ref failureCount);
            AssertContains(beacon, "_cachedBeaconRuntimeFrame", "Beacon has frame-indexed runtime presentation cache", report, ref failureCount);
            AssertContains(runtimeCacheBody, "int currentFrame = Time.frameCount", "Beacon cache samples frame index once per refresh", report, ref failureCount);
            AssertOrder(runtimeCacheBody, "if (_cachedBeaconRuntimeFrame != currentFrame)", "_cachedBeaconRuntimePosition = _beaconAup.ToRuntimeFloat3()", "Beacon converts AUP to runtime position only after frame-cache miss", report, ref failureCount);
            AssertContains(runtimeCacheBody, "_cachedBeaconRuntimeFrame = currentFrame", "Beacon marks runtime position cache with current frame", report, ref failureCount);
            AssertNotContains(beacon, "Awaitable", "Signal beacon source has no Awaitable dependency", report, ref failureCount);
            AssertNotContains(beacon, "await ", "Signal beacon source has no await usage", report, ref failureCount);
        }

        private static void RunAtlasSignalEventsAudit(string atlasSignalEvents, StringBuilder report, ref int failureCount)
        {
            if (atlasSignalEvents.Length == 0)
                return;

            string registerBody = ExtractMethodBody(atlasSignalEvents, "public static void Register(IAtlasSignalEventListener listener)");
            string unregisterBody = ExtractMethodBody(atlasSignalEvents, "public static void Unregister(IAtlasSignalEventListener listener)");
            string raiseDecodedBody = ExtractMethodBody(atlasSignalEvents, "private static bool TryRaiseDecodedFromString(string messageId)");
            string enqueueBody = ExtractMethodBody(atlasSignalEvents, "private static bool Enqueue(in AtlasSignalEventPayload payload)");
            string ensureBody = ExtractMethodBody(atlasSignalEvents, "private static void EnsureInitialized()");
            string resetBody = ExtractMethodBody(atlasSignalEvents, "private static void ResetStaticState()");
            string registerImmediateBody = ExtractMethodBody(atlasSignalEvents, "private static void RegisterImmediate(IAtlasSignalEventListener listener)");
            string overflowBody = ExtractMethodBody(atlasSignalEvents, "private static void ReportQueueOverflow(ushort eventType)");
            string unregisterMissBody = ExtractMethodBody(atlasSignalEvents, "private static void ReportUnregisterMiss()");
            string decodedCollisionBody = ExtractMethodBody(atlasSignalEvents, "private static void ReportDecodedMessageHashCollision(uint messageHash)");

            AssertContains(atlasSignalEvents, "[StructLayout(LayoutKind.Explicit, Size = 32)]", "Atlas signal event payload has explicit 32-byte layout", report, ref failureCount);
            AssertContains(atlasSignalEvents, "NativeQueue<AtlasSignalEventPayload>", "Atlas signal events use NativeQueue payload lanes", report, ref failureCount);
            AssertContains(ensureBody, "PrewarmQueue(ref _pendingEvents, PendingEventCapacity)", "Atlas signal front queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
            AssertContains(ensureBody, "PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity)", "Atlas signal reentrant queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
            AssertContains(atlasSignalEvents, "private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)", "Atlas signal events own a generic native queue prewarm helper", report, ref failureCount);
            AssertContains(atlasSignalEvents, "public static int DroppedEventCount => _droppedEventCount", "Atlas signal events expose dropped-event counter", report, ref failureCount);
            AssertContains(atlasSignalEvents, "public static int DuplicateRegistrationCount => _duplicateRegistrationCount", "Atlas signal events expose duplicate listener counter", report, ref failureCount);
            AssertContains(atlasSignalEvents, "public static int ListenerRejectCount => _listenerRejectCount", "Atlas signal events expose rejected-listener counter", report, ref failureCount);
            AssertContains(atlasSignalEvents, "public static int UnregisterMissCount => _unregisterMissCount", "Atlas signal events expose unregister-miss counter", report, ref failureCount);
            AssertContains(atlasSignalEvents, "public static int DecodedMessageHashCollisionCount => _decodedMessageHashCollisionCount", "Atlas signal events expose decoded-message collision counter", report, ref failureCount);
            AssertContains(atlasSignalEvents, "internal static bool IsRegistered(IAtlasSignalEventListener listener)", "Atlas signal events expose internal listener readback for lifecycle guards", report, ref failureCount);
            AssertNotContains(registerBody, "EnsureInitialized()", "Atlas signal listener registration does not cold-allocate event queues", report, ref failureCount);
            AssertContains(registerBody, "QueueDeferredRegister(listener)", "Atlas signal listener registration defers mutation during dispatch", report, ref failureCount);
            AssertContains(registerBody, "RegisterImmediate(listener)", "Atlas signal listener registration routes through capacity-checked helper", report, ref failureCount);
            AssertContains(registerImmediateBody, "_listeners.Contains(listener)", "Atlas signal listener registration rejects duplicates before append", report, ref failureCount);
            AssertContains(registerImmediateBody, "ReportDuplicateListenerRegistration()", "Atlas signal duplicate listener registration reports telemetry", report, ref failureCount);
            AssertContains(registerImmediateBody, "_listeners.TryRegister(listener)", "Atlas signal listener registration observes registry capacity rejection", report, ref failureCount);
            AssertContains(registerImmediateBody, "ReportListenerRejected()", "Atlas signal listener capacity rejection reports telemetry", report, ref failureCount);
            AssertContains(unregisterBody, "_listeners.TryUnregister(listener)", "Atlas signal unregister avoids RegistryBucket debug string miss path", report, ref failureCount);
            AssertContains(unregisterBody, "ReportUnregisterMiss()", "Atlas signal unregister miss reports hash-only telemetry", report, ref failureCount);
            AssertContains(raiseDecodedBody, "TryRegisterDecodedMessage(messageHash, messageId, out bool hashCollision)", "Atlas decoded message binding checks existing hash first", report, ref failureCount);
            AssertContains(raiseDecodedBody, "ReportDecodedMessageHashCollision(messageHash)", "Atlas decoded message hash collision reports telemetry", report, ref failureCount);
            AssertContains(enqueueBody, "ReportQueueOverflow(payload.EventType)", "Atlas signal queue overflow preserves event-type context", report, ref failureCount);
            AssertContains(overflowBody, "_droppedEventCount++", "Atlas signal overflow increments a monotonic counter", report, ref failureCount);
            AssertContains(overflowBody, "_lastOverflowTelemetryFrame == frame", "Atlas signal overflow telemetry is frame-rate limited", report, ref failureCount);
            AssertContains(overflowBody, "_QueueContextHash ^ ((uint)eventType << 24)", "Atlas signal overflow context encodes event type", report, ref failureCount);
            AssertContains(unregisterMissBody, "_unregisterMissCount++", "Atlas signal unregister miss increments a monotonic counter", report, ref failureCount);
            AssertContains(unregisterMissBody, "_lastUnregisterMissTelemetryFrame == frame", "Atlas signal unregister miss telemetry is frame-rate limited", report, ref failureCount);
            AssertContains(decodedCollisionBody, "_decodedMessageHashCollisionCount++", "Atlas signal decoded-message collision increments a monotonic counter", report, ref failureCount);
            AssertContains(decodedCollisionBody, "_DecodedMessageContextHash ^ messageHash", "Atlas signal decoded-message collision preserves colliding hash context", report, ref failureCount);
            AssertContains(atlasSignalEvents, "GlobalTelemetryBus.PublishPerformanceWarning", "Atlas signal event failures publish hash-only performance telemetry", report, ref failureCount);
            AssertContains(resetBody, "_droppedEventCount = 0", "Atlas signal reset clears dropped-event counter", report, ref failureCount);
            AssertContains(resetBody, "_unregisterMissCount = 0", "Atlas signal reset clears unregister-miss counter", report, ref failureCount);
            AssertContains(resetBody, "_decodedMessageHashCollisionCount = 0", "Atlas signal reset clears decoded-message collision counter", report, ref failureCount);
            AssertContains(resetBody, "_lastOverflowTelemetryFrame = -1", "Atlas signal reset rearms overflow telemetry gate", report, ref failureCount);
            AssertContains(resetBody, "_lastUnregisterMissTelemetryFrame = -1", "Atlas signal reset rearms unregister-miss telemetry gate", report, ref failureCount);
            AssertContains(resetBody, "_lastDecodedMessageCollisionTelemetryFrame = -1", "Atlas signal reset rearms decoded-message collision telemetry gate", report, ref failureCount);
            AssertNotContains(enqueueBody, "Debug.Log", "Atlas signal enqueue path has no managed debug logging", report, ref failureCount);
            AssertNotContains(enqueueBody, "string", "Atlas signal enqueue path has no managed string work", report, ref failureCount);
            AssertNotContains(atlasSignalEvents, "Awaitable", "Atlas signal event source has no Awaitable dependency", report, ref failureCount);
            AssertNotContains(atlasSignalEvents, "await ", "Atlas signal event source has no await usage", report, ref failureCount);
        }

        private static void RunAtlas6EventsAudit(string atlas6DirectiveSystem, StringBuilder report, ref int failureCount)
        {
            if (atlas6DirectiveSystem.Length == 0)
                return;

            string enqueueBody = ExtractMethodBody(atlas6DirectiveSystem, "private static bool Enqueue(in Atlas6EventPayload payload)");
            string ensureBody = ExtractMethodBody(atlas6DirectiveSystem, "private static void EnsureInitialized()");

            AssertContains(atlas6DirectiveSystem, "[StructLayout(LayoutKind.Explicit, Size = 32)]", "Atlas-6 event payload has explicit 32-byte layout", report, ref failureCount);
            AssertContains(atlas6DirectiveSystem, "NativeQueue<Atlas6EventPayload>", "Atlas-6 directive events use NativeQueue payload lanes", report, ref failureCount);
            AssertContains(ensureBody, "PrewarmQueue(ref _pendingEvents, PendingEventCapacity)", "Atlas-6 front queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
            AssertContains(ensureBody, "PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity)", "Atlas-6 reentrant queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
            AssertContains(atlas6DirectiveSystem, "private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)", "Atlas-6 directive events own a generic native queue prewarm helper", report, ref failureCount);
            AssertNotContains(enqueueBody, "Debug.Log", "Atlas-6 enqueue path has no managed debug logging", report, ref failureCount);
            AssertNotContains(enqueueBody, "string", "Atlas-6 enqueue path has no managed string work", report, ref failureCount);
            AssertNotContains(enqueueBody, ".Complete(", "Atlas-6 enqueue path has no job barrier", report, ref failureCount);
            AssertNotContains(enqueueBody, ".Run(", "Atlas-6 enqueue path has no synchronous job run", report, ref failureCount);
            AssertNotContains(atlas6DirectiveSystem, "Awaitable", "Atlas-6 directive source has no Awaitable dependency", report, ref failureCount);
            AssertNotContains(atlas6DirectiveSystem, "await ", "Atlas-6 directive source has no await usage", report, ref failureCount);
        }

        private static void RunQuestProgressionEventAudit(
            string questEvents,
            string questGraphEvaluator,
            StringBuilder report,
            ref int failureCount)
        {
            if (questEvents.Length > 0)
            {
                string registerBody = ExtractMethodBody(questEvents, "public static void Register(IQuestEventListener listener)");
                string unregisterBody = ExtractMethodBody(questEvents, "public static void Unregister(IQuestEventListener listener)");
                string flushBody = ExtractMethodBody(questEvents, "public static void FlushPending()");
                string enqueueBody = ExtractMethodBody(questEvents, "private static bool Enqueue(QuestEventType type, uint questHash)");
                string ensureBody = ExtractMethodBody(questEvents, "private static void EnsureInitialized()");
                string resetBody = ExtractMethodBody(questEvents, "private static void ResetStaticState()");
                string registerImmediateBody = ExtractMethodBody(questEvents, "private static void RegisterImmediate(IQuestEventListener listener)");
                string applyDeferredBody = ExtractMethodBody(questEvents, "private static void ApplyDeferredListenerMutations()");
                string overflowBody = ExtractMethodBody(questEvents, "private static void ReportQueueOverflow(ushort eventType)");
                string unregisterMissBody = ExtractMethodBody(questEvents, "private static void ReportUnregisterMiss()");
                string listenerExceptionBody = ExtractMethodBody(questEvents, "private static void ReportListenerDispatchException()");

                AssertContains(questEvents, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "Quest event payload has explicit 16-byte layout", report, ref failureCount);
                AssertContains(questEvents, "NativeQueue<QuestEventPayload>", "Quest events use NativeQueue payload lanes", report, ref failureCount);
                AssertContains(ensureBody, "PrewarmQueue(ref _pendingEvents, PendingEventCapacity)", "Quest front queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
                AssertContains(ensureBody, "PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity)", "Quest reentrant queue is cold-prewarmed before gameplay enqueue", report, ref failureCount);
                AssertContains(questEvents, "private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)", "Quest events own a generic native queue prewarm helper", report, ref failureCount);
                AssertContains(questEvents, "public static int DroppedEventCount => _droppedEventCount", "Quest events expose dropped-event counter", report, ref failureCount);
                AssertContains(questEvents, "public static int DuplicateRegistrationCount => _duplicateRegistrationCount", "Quest events expose duplicate-listener counter", report, ref failureCount);
                AssertContains(questEvents, "public static int ListenerRejectCount => _listenerRejectCount", "Quest events expose rejected-listener counter", report, ref failureCount);
                AssertContains(questEvents, "public static int ListenerExceptionCount => _listenerExceptionCount", "Quest events expose listener-exception counter", report, ref failureCount);
                AssertContains(questEvents, "public static int UnregisterMissCount => _unregisterMissCount", "Quest events expose unregister-miss counter", report, ref failureCount);
                AssertContains(questEvents, "internal static bool IsRegistered(IQuestEventListener listener)", "Quest events expose internal listener readback for lifecycle guards", report, ref failureCount);
                AssertNotContains(registerBody, "EnsureInitialized()", "Quest listener registration does not cold-allocate event queues", report, ref failureCount);
                AssertContains(registerBody, "QueueDeferredRegister(listener)", "Quest listener registration defers mutation during dispatch", report, ref failureCount);
                AssertContains(registerBody, "RegisterImmediate(listener)", "Quest listener registration routes through capacity-checked helper", report, ref failureCount);
                AssertContains(unregisterBody, "QueueDeferredUnregister(listener)", "Quest unregister defers mutation during dispatch", report, ref failureCount);
                AssertContains(unregisterBody, "_listeners.TryUnregister(listener)", "Quest unregister avoids RegistryBucket debug string miss path", report, ref failureCount);
                AssertContains(unregisterBody, "ReportUnregisterMiss()", "Quest unregister miss reports hash-only telemetry", report, ref failureCount);
                AssertContains(registerImmediateBody, "_listeners.Contains(listener)", "Quest immediate registration rejects duplicates before append", report, ref failureCount);
                AssertContains(registerImmediateBody, "ReportDuplicateListenerRegistration()", "Quest duplicate listener registration reports telemetry", report, ref failureCount);
                AssertContains(registerImmediateBody, "_listeners.TryRegister(listener)", "Quest listener registration observes registry capacity rejection", report, ref failureCount);
                AssertContains(registerImmediateBody, "ReportListenerRejected()", "Quest listener capacity rejection reports telemetry", report, ref failureCount);
                AssertContains(flushBody, "IsDeferredUnregisterPending(listener)", "Quest dispatch skips listeners queued for same-event removal", report, ref failureCount);
                AssertContains(flushBody, "DispatchToListener(listener, in payload)", "Quest dispatch routes through guarded listener invoke", report, ref failureCount);
                AssertContains(flushBody, "ApplyDeferredListenerMutations()", "Quest dispatch applies deferred listener mutations after loop", report, ref failureCount);
                AssertContains(applyDeferredBody, "_listeners.TryUnregister(listener)", "Quest deferred unregister uses no-log registry removal", report, ref failureCount);
                AssertContains(applyDeferredBody, "RegisterImmediate(listener)", "Quest deferred register reuses duplicate/capacity guard", report, ref failureCount);
                AssertContains(enqueueBody, "ReportQueueOverflow((ushort)type)", "Quest event queue overflow preserves event-type context", report, ref failureCount);
                AssertContains(overflowBody, "_droppedEventCount++", "Quest event overflow increments monotonic counter", report, ref failureCount);
                AssertContains(overflowBody, "_lastOverflowTelemetryFrame == frame", "Quest event overflow telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(overflowBody, "_QueueContextHash ^ ((uint)eventType << 24)", "Quest event overflow context encodes event type", report, ref failureCount);
                AssertContains(unregisterMissBody, "_unregisterMissCount++", "Quest unregister miss increments monotonic counter", report, ref failureCount);
                AssertContains(unregisterMissBody, "_lastUnregisterMissTelemetryFrame == frame", "Quest unregister miss telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(listenerExceptionBody, "_listenerExceptionCount++", "Quest listener exception increments monotonic counter", report, ref failureCount);
                AssertContains(questEvents, "GlobalTelemetryBus.PublishPerformanceWarning", "Quest event failures publish hash-only performance telemetry", report, ref failureCount);
                AssertContains(resetBody, "_droppedEventCount = 0", "Quest reset clears dropped-event counter", report, ref failureCount);
                AssertContains(resetBody, "_deferredRegisterCount = 0", "Quest reset clears deferred register count", report, ref failureCount);
                AssertContains(resetBody, "_lastOverflowTelemetryFrame = -1", "Quest reset rearms overflow telemetry gate", report, ref failureCount);
                AssertNotContains(enqueueBody, "Debug.Log", "Quest enqueue path has no managed debug logging", report, ref failureCount);
                AssertNotContains(enqueueBody, "string", "Quest enqueue path has no managed string work", report, ref failureCount);
                AssertNotContains(questEvents, "Awaitable", "Quest event source has no Awaitable dependency", report, ref failureCount);
                AssertNotContains(questEvents, "await ", "Quest event source has no await usage", report, ref failureCount);
            }

            if (questGraphEvaluator.Length == 0)
                return;

            string constructorBody = ExtractMethodBody(questGraphEvaluator, "public QuestGraphEvaluator(QuestStateManager stateManager, Action onResultsAvailable)");
            string bindBody = ExtractMethodBody(questGraphEvaluator, "public void Bind()");
            string enqueueSignalBody = ExtractMethodBody(questGraphEvaluator, "private void EnqueueSignal(in QuestSignalPayload payload)");
            string resetStaticBody = ExtractMethodBody(questGraphEvaluator, "private static void ResetStaticState()");
            string pendingOverflowBody = ExtractMethodBody(questGraphEvaluator, "private void ReportPendingSignalOverflow(ushort eventType)");
            string activeRejectBody = ExtractMethodBody(questGraphEvaluator, "private static void ReportActiveEvaluatorRejected()");

            AssertContains(questGraphEvaluator, "NativeQueue<QuestSignalPayload>", "Quest graph evaluator uses a NativeQueue signal ingress lane", report, ref failureCount);
            AssertContains(constructorBody, "NativeMemorySentinel.RegisterNativeQueue", "Quest graph evaluator signal queue is registered with NativeMemorySentinel", report, ref failureCount);
            AssertContains(constructorBody, "PrewarmQueue(ref _pendingSignals, PendingSignalCapacity)", "Quest graph evaluator signal queue is cold-prewarmed before event ingress", report, ref failureCount);
            AssertContains(questGraphEvaluator, "private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)", "Quest graph evaluator owns a generic native queue prewarm helper", report, ref failureCount);
            AssertContains(questGraphEvaluator, "internal int DroppedSignalCount => _droppedSignalCount", "Quest graph evaluator exposes dropped-signal counter", report, ref failureCount);
            AssertContains(questGraphEvaluator, "internal static int ActiveEvaluatorRejectCount => _activeEvaluatorRejectCount", "Quest graph evaluator exposes active-registry rejection counter", report, ref failureCount);
            AssertContains(resetStaticBody, "_activeEvaluators.Clear()", "Quest graph evaluator clears static active registry on subsystem reset", report, ref failureCount);
            AssertContains(resetStaticBody, "_activeEvaluatorRejectCount = 0", "Quest graph evaluator reset clears active-registry rejection counter", report, ref failureCount);
            AssertContains(bindBody, "_activeEvaluators.TryRegister(this)", "Quest graph evaluator bind checks active-registry capacity", report, ref failureCount);
            AssertContains(bindBody, "ReportActiveEvaluatorRejected()", "Quest graph evaluator bind reports active-registry rejection", report, ref failureCount);
            AssertOrder(bindBody, "_activeEvaluators.TryRegister(this)", "NarrativeEvents.Register(this)", "Quest graph evaluator joins active registry before subscribing to signals", report, ref failureCount);
            AssertContains(enqueueSignalBody, "ReportPendingSignalOverflow(payload.EventType)", "Quest graph evaluator pending signal overflow is counted", report, ref failureCount);
            AssertContains(pendingOverflowBody, "_droppedSignalCount++", "Quest graph evaluator pending overflow increments monotonic counter", report, ref failureCount);
            AssertContains(pendingOverflowBody, "_lastPendingSignalOverflowTelemetryFrame == frame", "Quest graph evaluator overflow telemetry is frame-rate limited", report, ref failureCount);
            AssertContains(pendingOverflowBody, "_PendingSignalContextHash ^ ((uint)eventType << 24)", "Quest graph evaluator overflow context encodes signal type", report, ref failureCount);
            AssertContains(activeRejectBody, "_activeEvaluatorRejectCount++", "Quest graph evaluator active-registry rejection increments monotonic counter", report, ref failureCount);
            AssertContains(activeRejectBody, "GlobalTelemetryBus.PublishPerformanceWarning", "Quest graph evaluator rejection publishes hash-only telemetry", report, ref failureCount);
            AssertNotContains(enqueueSignalBody, "Debug.Log", "Quest graph enqueue path has no managed debug logging", report, ref failureCount);
            AssertNotContains(enqueueSignalBody, "string", "Quest graph enqueue path has no managed string work", report, ref failureCount);
            AssertNotContains(questGraphEvaluator, "Awaitable", "Quest graph evaluator has no Awaitable dependency", report, ref failureCount);
            AssertNotContains(questGraphEvaluator, "await ", "Quest graph evaluator has no await usage", report, ref failureCount);
        }

        private static void RunUpstreamProgressionEventAudit(
            string narrativeEvents,
            string craftingEvents,
            string interactionEvents,
            string biomeMatrixDirector,
            StringBuilder report,
            ref int failureCount)
        {
            if (narrativeEvents.Length > 0)
            {
                string registerBody = ExtractMethodBody(narrativeEvents, "public static void Register(INarrativeEventListener listener)");
                string enqueueBody = ExtractMethodBody(narrativeEvents, "private static bool Enqueue(in NarrativeEventPayload payload)");
                string narrativeResetBody = ExtractMethodBody(narrativeEvents, "private static void ResetStaticState()");
                string narrativeOverflowBody = ExtractMethodBody(narrativeEvents, "private static void ReportQueueOverflow(ushort eventType)");

                AssertContains(narrativeEvents, "public static int DroppedEventCount => _droppedEventCount", "Narrative events expose dropped-event counter", report, ref failureCount);
                AssertNotContains(registerBody, "EnsureInitialized()", "Narrative listener registration does not cold-allocate event queues", report, ref failureCount);
                AssertContains(enqueueBody, "ReportQueueOverflow(payload.EventType)", "Narrative event queue overflow preserves event-type context", report, ref failureCount);
                AssertContains(narrativeOverflowBody, "_droppedEventCount++", "Narrative event overflow increments monotonic counter", report, ref failureCount);
                AssertContains(narrativeOverflowBody, "_lastQueueOverflowTelemetryFrame == frame", "Narrative event overflow telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(narrativeOverflowBody, "NarrativeQueueContextHash ^ ((uint)eventType << 24)", "Narrative event overflow context encodes event type", report, ref failureCount);
                AssertContains(narrativeResetBody, "_droppedEventCount = 0", "Narrative reset clears dropped-event counter", report, ref failureCount);
                AssertContains(narrativeResetBody, "_lastQueueOverflowTelemetryFrame = -1", "Narrative reset rearms overflow telemetry gate", report, ref failureCount);
                AssertNotContains(enqueueBody, "Debug.Log", "Narrative enqueue path has no managed debug logging", report, ref failureCount);
                AssertNotContains(narrativeEvents, "Awaitable", "Narrative events have no Awaitable dependency", report, ref failureCount);
                AssertNotContains(narrativeEvents, "await ", "Narrative events have no await usage", report, ref failureCount);
            }

            if (craftingEvents.Length > 0)
            {
                string registerBody = ExtractMethodBody(craftingEvents, "public static void Register(ICraftingEventListener listener)");
                string enqueueBody = ExtractMethodBody(craftingEvents, "private static bool Enqueue(in CraftingEventPayload payload)");
                string reserveBody = ExtractMethodBody(craftingEvents, "private static bool TryReserveReferenceSlot(CraftingEventType eventType, out int referenceSlot)");
                string craftingResetBody = ExtractMethodBody(craftingEvents, "internal static void ResetStaticState()");
                string craftingOverflowBody = ExtractMethodBody(craftingEvents, "private static void ReportQueueOverflow(ushort eventType)");
                string slotExhaustedBody = ExtractMethodBody(craftingEvents, "private static void ReportReferenceSlotExhausted(ushort eventType)");

                AssertContains(craftingEvents, "public static int DroppedEventCount => _droppedEventCount", "Crafting events expose dropped-event counter", report, ref failureCount);
                AssertContains(craftingEvents, "public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount", "Crafting events expose reference-slot exhaustion counter", report, ref failureCount);
                AssertNotContains(registerBody, "EnsureInitialized()", "Crafting listener registration does not cold-allocate event queues", report, ref failureCount);
                AssertContains(enqueueBody, "ReportQueueOverflow(payload.EventType)", "Crafting event queue overflow preserves event-type context", report, ref failureCount);
                AssertContains(enqueueBody, "ReleaseReferenceSlot(payload.ReferenceSlot)", "Crafting queue overflow releases reserved reference slot", report, ref failureCount);
                AssertContains(reserveBody, "ReportReferenceSlotExhausted((ushort)eventType)", "Crafting reference slot exhaustion preserves event-type context", report, ref failureCount);
                AssertContains(craftingOverflowBody, "_droppedEventCount++", "Crafting event overflow increments monotonic counter", report, ref failureCount);
                AssertContains(craftingOverflowBody, "_lastQueueOverflowTelemetryFrame == frame", "Crafting event overflow telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(craftingOverflowBody, "CraftingQueueContextHash ^ ((uint)eventType << 24)", "Crafting event overflow context encodes event type", report, ref failureCount);
                AssertContains(slotExhaustedBody, "_droppedReferenceSlotCount++", "Crafting reference slot exhaustion increments monotonic counter", report, ref failureCount);
                AssertContains(slotExhaustedBody, "_lastReferenceSlotTelemetryFrame == frame", "Crafting reference slot telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(slotExhaustedBody, "CraftingReferenceSlotContextHash ^ ((uint)eventType << 24)", "Crafting reference slot context encodes event type", report, ref failureCount);
                AssertContains(craftingResetBody, "_droppedEventCount = 0", "Crafting reset clears dropped-event counter", report, ref failureCount);
                AssertContains(craftingResetBody, "_droppedReferenceSlotCount = 0", "Crafting reset clears reference-slot counter", report, ref failureCount);
                AssertContains(craftingResetBody, "_lastQueueOverflowTelemetryFrame = -1", "Crafting reset rearms queue overflow telemetry gate", report, ref failureCount);
                AssertContains(craftingResetBody, "_lastReferenceSlotTelemetryFrame = -1", "Crafting reset rearms reference-slot telemetry gate", report, ref failureCount);
                AssertNotContains(enqueueBody, "Debug.Log", "Crafting enqueue path has no managed debug logging", report, ref failureCount);
                AssertNotContains(craftingEvents, "Awaitable", "Crafting events have no Awaitable dependency", report, ref failureCount);
                AssertNotContains(craftingEvents, "await ", "Crafting events have no await usage", report, ref failureCount);
            }

            if (interactionEvents.Length > 0)
            {
                string registerBody = ExtractMethodBody(interactionEvents, "public static void Register(IInteractionEventListener listener)");
                string enqueueBody = ExtractMethodBody(interactionEvents, "private static bool Enqueue(in InteractionEventPayload payload)");
                string reserveBody = ExtractMethodBody(interactionEvents, "private static bool TryReserveReferenceSlot(InteractionEventType eventType, out int referenceSlot)");
                string interactionResetBody = ExtractMethodBody(interactionEvents, "internal static void ResetStaticState()");
                string interactionOverflowBody = ExtractMethodBody(interactionEvents, "private static void ReportQueueOverflow(ushort eventType)");
                string slotExhaustedBody = ExtractMethodBody(interactionEvents, "private static void ReportReferenceSlotExhausted(ushort eventType)");

                AssertContains(interactionEvents, "public static int DroppedEventCount => _droppedEventCount", "Interaction events expose dropped-event counter", report, ref failureCount);
                AssertContains(interactionEvents, "public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount", "Interaction events expose reference-slot exhaustion counter", report, ref failureCount);
                AssertNotContains(registerBody, "EnsureInitialized()", "Interaction listener registration does not cold-allocate event queues", report, ref failureCount);
                AssertContains(enqueueBody, "ReportQueueOverflow(payload.EventType)", "Interaction event queue overflow preserves event-type context", report, ref failureCount);
                AssertContains(enqueueBody, "ReleaseReferenceSlot(payload.ReferenceSlot)", "Interaction queue overflow releases reserved reference slot", report, ref failureCount);
                AssertContains(reserveBody, "ReportReferenceSlotExhausted((ushort)eventType)", "Interaction reference slot exhaustion preserves event-type context", report, ref failureCount);
                AssertContains(interactionOverflowBody, "_droppedEventCount++", "Interaction event overflow increments monotonic counter", report, ref failureCount);
                AssertContains(interactionOverflowBody, "_lastQueueOverflowTelemetryFrame == frame", "Interaction event overflow telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(interactionOverflowBody, "InteractionQueueContextHash ^ ((uint)eventType << 24)", "Interaction event overflow context encodes event type", report, ref failureCount);
                AssertContains(slotExhaustedBody, "_droppedReferenceSlotCount++", "Interaction reference slot exhaustion increments monotonic counter", report, ref failureCount);
                AssertContains(slotExhaustedBody, "_lastReferenceSlotTelemetryFrame == frame", "Interaction reference slot telemetry is frame-rate limited", report, ref failureCount);
                AssertContains(slotExhaustedBody, "InteractionReferenceSlotContextHash ^ ((uint)eventType << 24)", "Interaction reference slot context encodes event type", report, ref failureCount);
                AssertContains(interactionResetBody, "_droppedEventCount = 0", "Interaction reset clears dropped-event counter", report, ref failureCount);
                AssertContains(interactionResetBody, "_droppedReferenceSlotCount = 0", "Interaction reset clears reference-slot counter", report, ref failureCount);
                AssertContains(interactionResetBody, "_lastQueueOverflowTelemetryFrame = -1", "Interaction reset rearms queue overflow telemetry gate", report, ref failureCount);
                AssertContains(interactionResetBody, "_lastReferenceSlotTelemetryFrame = -1", "Interaction reset rearms reference-slot telemetry gate", report, ref failureCount);
                AssertNotContains(enqueueBody, "Debug.Log", "Interaction enqueue path has no managed debug logging", report, ref failureCount);
                AssertNotContains(interactionEvents, "Awaitable", "Interaction events have no Awaitable dependency", report, ref failureCount);
                AssertNotContains(interactionEvents, "await ", "Interaction events have no await usage", report, ref failureCount);
            }

            if (biomeMatrixDirector.Length == 0)
                return;

            string raiseMatrixBody = ExtractMethodBody(biomeMatrixDirector, "public static void RaiseMatrixBiomeChanged(HectonBiomeMatrixProfile profile)");
            string raiseDepthBody = ExtractMethodBody(biomeMatrixDirector, "public static void RaiseDepthTierChanged(int depthTier, float depthMeters)");
            string biomeResetBody = ExtractMethodBody(biomeMatrixDirector, "private static void ResetStaticState()");
            string biomeOverflowBody = ExtractMethodBody(biomeMatrixDirector, "private static void ReportQueueOverflow(byte eventType)");
            string profileOverflowBody = ExtractMethodBody(biomeMatrixDirector, "private static void ReportProfileSlotOverflow()");

            AssertContains(biomeMatrixDirector, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "Biome matrix event payload has explicit 16-byte layout", report, ref failureCount);
            AssertContains(biomeMatrixDirector, "public static int DroppedEventCount => _droppedEventCount", "Biome matrix events expose dropped-event counter", report, ref failureCount);
            AssertContains(biomeMatrixDirector, "public static int DroppedProfileSlotCount => _droppedProfileSlotCount", "Biome matrix events expose profile-slot exhaustion counter", report, ref failureCount);
            AssertContains(raiseMatrixBody, "ReportQueueOverflow(MatrixBiomeChangedEventType)", "Biome matrix biome-change queue overflow preserves event-type context", report, ref failureCount);
            AssertContains(raiseMatrixBody, "ReportProfileSlotOverflow()", "Biome matrix profile slot overflow is counted", report, ref failureCount);
            AssertContains(raiseDepthBody, "ReportQueueOverflow(DepthTierChangedEventType)", "Biome matrix depth-tier queue overflow preserves event-type context", report, ref failureCount);
            AssertContains(biomeOverflowBody, "_droppedEventCount++", "Biome matrix event overflow increments monotonic counter", report, ref failureCount);
            AssertContains(biomeOverflowBody, "_lastQueueOverflowTelemetryFrame == frame", "Biome matrix event overflow telemetry is frame-rate limited", report, ref failureCount);
            AssertContains(biomeOverflowBody, "QueueContextHash ^ ((uint)eventType << 24)", "Biome matrix event overflow context encodes event type", report, ref failureCount);
            AssertContains(profileOverflowBody, "_droppedProfileSlotCount++", "Biome matrix profile slot exhaustion increments monotonic counter", report, ref failureCount);
            AssertContains(profileOverflowBody, "_lastProfileSlotOverflowTelemetryFrame == frame", "Biome matrix profile slot telemetry is frame-rate limited", report, ref failureCount);
            AssertContains(biomeResetBody, "_droppedEventCount = 0", "Biome matrix reset clears dropped-event counter", report, ref failureCount);
            AssertContains(biomeResetBody, "_droppedProfileSlotCount = 0", "Biome matrix reset clears profile-slot counter", report, ref failureCount);
            AssertContains(biomeResetBody, "_lastQueueOverflowTelemetryFrame = -1", "Biome matrix reset rearms queue overflow telemetry gate", report, ref failureCount);
            AssertContains(biomeResetBody, "_lastProfileSlotOverflowTelemetryFrame = -1", "Biome matrix reset rearms profile-slot telemetry gate", report, ref failureCount);
        }

        private static void RunSpectrogramAudit(string decoder, string spectrogram, StringBuilder report, ref int failureCount)
        {
            if (decoder.Length > 0)
            {
                string onEnableBody = ExtractMethodBody(decoder, "private void OnEnable()");
                string onDisableBody = ExtractMethodBody(decoder, "private void OnDisable()");
                string synchronizePhaseBody = ExtractMethodBody(decoder, "private void SynchronizePhaseFromSignal(AtlasSignalSystem sys)");
                string submitWaveMatchBody = ExtractMethodBody(decoder, "public float SubmitWaveMatch(float carrierFrequencyHz, float carrierPhase01)");
                string advanceDecodeProgressBody = ExtractMethodBody(decoder, "private bool AdvanceDecodeProgress(float dt)");
                string completeDecodeBody = ExtractMethodBody(decoder, "private void CompleteDecode()");

                AssertContains(decoder, "public float SubmitWaveMatch(float carrierFrequencyHz, float carrierPhase01)", "Decoder exposes sine-wave match submission", report, ref failureCount);
                AssertContains(decoder, "SignalBeaconMath.EvaluateSineWaveMatch(", "Decoder routes wave matching through ALU/Burst math", report, ref failureCount);
                AssertContains(decoder, "private bool _atlasSignalEventRegistered", "Decoder tracks Atlas event subscription state", report, ref failureCount);
                AssertContains(onEnableBody, "TryRegisterAtlasSignalEvents()", "Decoder registers Atlas events through tracked helper", report, ref failureCount);
                AssertContains(onDisableBody, "TryUnregisterAtlasSignalEvents()", "Decoder unregisters Atlas events only when tracked", report, ref failureCount);
                AssertContains(decoder, "AtlasSignalEvents.IsRegistered(this)", "Decoder confirms Atlas event registration before setting lifecycle bit", report, ref failureCount);
                AssertContains(synchronizePhaseBody, "_decodeWindowOpen = CalculatePhase(sys.CurrentStrength) >= 4", "Decoder synchronizes decode window through sanitized phase thresholds", report, ref failureCount);
                AssertContains(submitWaveMatchBody, "SanitizeFrequencyHz(carrierFrequencyHz)", "Decoder sanitizes submitted carrier frequency", report, ref failureCount);
                AssertContains(submitWaveMatchBody, "SanitizePhase01(carrierPhase01)", "Decoder sanitizes submitted carrier phase", report, ref failureCount);
                AssertContains(submitWaveMatchBody, "_waveMatch01 = Sanitize01(_waveMatch01)", "Decoder clamps Burst wave match result before storing", report, ref failureCount);
                AssertContains(advanceDecodeProgressBody, "ResolveWaveMatchUnlockThreshold01()", "Decoder gates phase progression by sanitized wave-match threshold", report, ref failureCount);
                AssertContains(advanceDecodeProgressBody, "Sanitize01(_waveMatch01)", "Decoder sanitizes stored wave match before progress math", report, ref failureCount);
                AssertContains(advanceDecodeProgressBody, "SanitizePositive(dt, 0f)", "Decoder sanitizes decode delta before progress math", report, ref failureCount);
                AssertContains(completeDecodeBody, "if (_fullyDecoded)", "Decoder full unlock is idempotent", report, ref failureCount);
                AssertContains(completeDecodeBody, "_decodeProgress = 1f", "Decoder pins progress to complete on full unlock", report, ref failureCount);
                AssertContains(completeDecodeBody, "_decodeWindowOpen = false", "Decoder closes decode window after full unlock", report, ref failureCount);
            }

            if (spectrogram.Length == 0)
                return;

            string tickBody = ExtractMethodBody(spectrogram, "public void Tick(float deltaTime)");
            string lateFrameBody = ExtractMethodBody(spectrogram, "public void LateFrameTick()");
            string commitBody = ExtractMethodBody(spectrogram, "private void CommitWaveResult(float deltaTime, float rawError)");
            AssertContains(spectrogram, "private float EvaluateScalarWaveError()", "Frequency tuning computes scalar analytic error without waveform jobs", report, ref failureCount);
            AssertContains(spectrogram, "WaveScalarsId", "Frequency tuning binds wave frequencies/amplitudes to shader scalars", report, ref failureCount);
            AssertContains(spectrogram, "WaveLayoutId", "Frequency tuning binds wave layout to shader scalars", report, ref failureCount);
            AssertContains(spectrogram, "GlobalRegistry.Input", "Frequency tuning reads cached player input state", report, ref failureCount);
            AssertContains(tickBody, "DrainScannerToolSignals()", "Frequency tuning consumes scanner-active signal lane", report, ref failureCount);
            AssertContains(spectrogram, "TryGetLatestScannerToolActiveSignal", "Frequency tuning has latest scanner-active fallback for late PDA panel activation", report, ref failureCount);
            AssertContains(commitBody, "LockCurrentStage()", "Frequency tuning locks stages after continuous match", report, ref failureCount);
            AssertContains(spectrogram, "SignalBus<BlueprintUnlockedSignal>.TryPush", "Frequency tuning emits blueprint unlock through SignalBus", report, ref failureCount);
            AssertContains(spectrogram, "Graphics.RenderMeshIndirect", "Frequency tuning renders via indirect PDA draw path", report, ref failureCount);
            AssertContains(spectrogram, "_HectonFrequencyTuningWaveScalars", "Frequency tuning binds wave scalars to the PDA shader", report, ref failureCount);
            AssertContains(spectrogram, "UpdateDrawArgs(_gpuSegmentCapacity)", "Frequency tuning updates indirect draw args without waveform CPU jobs", report, ref failureCount);
            AssertContains(spectrogram, "ToolHapticsRuntime.EnqueueSinusoidalCommand", "Frequency tuning emits haptic feedback through fixed haptic queue", report, ref failureCount);
            AssertContains(spectrogram, "PlayerSignalEvents.TryRaiseInteractionSignal", "Frequency tuning routes audio feedback through player signal lane", report, ref failureCount);
            AssertContains(spectrogram, "_HectonFrequencyTuningError01", "Frequency tuning pushes visor-post error scalar", report, ref failureCount);
            AssertContains(spectrogram, "LowPointCount = 32", "Frequency tuning low-tier math LOD uses 32 points", report, ref failureCount);
            AssertContains(spectrogram, "TelemetryCapacity = 300", "Frequency tuning black box tracks 300 frames", report, ref failureCount);
            AssertContains(spectrogram, "private static float Sanitize01(float value)", "Spectrogram centralizes normalized scalar sanitization", report, ref failureCount);
            AssertNotContains(spectrogram, "UnityEngine.UI", "Frequency tuning has no uGUI dependency", report, ref failureCount);
            AssertNotContains(spectrogram, "LineRenderer", "Frequency tuning has no LineRenderer dependency", report, ref failureCount);
            AssertNotContains(spectrogram, "math.sin", "Frequency tuning source has no direct math.sin calls", report, ref failureCount);
            AssertNotContains(spectrogram, "Mathf.Abs", "Frequency tuning avoids Mathf.Abs in waveform math", report, ref failureCount);
            AssertNotContains(spectrogram, ".text =", "Spectrogram source does not assign TMP text strings", report, ref failureCount);
            AssertNotContains(spectrogram, "SetText(", "Spectrogram source does not call TMP SetText", report, ref failureCount);
            AssertNotContains(spectrogram, "MinigameManager.Instance", "Frequency tuning does not depend on MinigameManager singleton", report, ref failureCount);
            AssertNotContains(lateFrameBody, "Time.", "Frequency tuning late frame uses cached dispatcher-frame timing", report, ref failureCount);
            AssertNotContains(lateFrameBody, ".Run(", "Frequency tuning late-frame job recovery does not run jobs synchronously", report, ref failureCount);
            AssertNotContains(tickBody, ".text =", "Spectrogram Tick does not assign TMP text strings", report, ref failureCount);
            AssertNotContains(tickBody, ".Complete(", "Spectrogram Tick does not complete jobs in the update lane", report, ref failureCount);
        }

        private static void RunPdaAtlasSignalAudit(string pdaAtlasSignalTab, StringBuilder report, ref int failureCount)
        {
            if (pdaAtlasSignalTab.Length == 0)
                return;

            string tickBody = ExtractMethodBody(pdaAtlasSignalTab, "public void Tick(float deltaTime)");
            string pollBeaconBody = ExtractMethodBody(pdaAtlasSignalTab, "private void PollSignalBeaconDirtyState()");
            AssertContains(pdaAtlasSignalTab, "private const float BeaconTelemetryEpsilon = 0.01f", "PDA Atlas tab has beacon telemetry dirty epsilon", report, ref failureCount);
            AssertContains(tickBody, "PollSignalBeaconDirtyState()", "PDA Atlas tab polls beacon registry for live signal contact", report, ref failureCount);
            AssertContains(tickBody, "_pulseCountdown = math.max(0f, _pulseCountdown - math.max(0f, deltaTime))", "PDA Atlas countdown clamps negative delta and floor", report, ref failureCount);
            AssertContains(pollBeaconBody, "SignalBeaconRegistry.TryGetDominantTelemetry(out float strength01, out float static01)", "PDA Atlas poll reads hash-only beacon telemetry", report, ref failureCount);
            AssertContains(pollBeaconBody, "math.abs(safeStrength01 - _beaconStrength01) <= BeaconTelemetryEpsilon", "PDA Atlas poll skips unchanged beacon strength", report, ref failureCount);
            AssertContains(pollBeaconBody, "math.abs(safeStatic01 - _beaconStatic01) <= BeaconTelemetryEpsilon", "PDA Atlas poll skips unchanged beacon static", report, ref failureCount);
            AssertContains(pollBeaconBody, "_dirty = true", "PDA Atlas poll dirties UI only after telemetry change", report, ref failureCount);
            AssertNotContains(tickBody, ".text =", "PDA Atlas Tick does not assign TMP text strings", report, ref failureCount);
            AssertNotContains(tickBody, "SetText(", "PDA Atlas Tick does not call string SetText", report, ref failureCount);
        }

        private static void RunEncryptedLogAudit(
            string audioLogSystem,
            string saveData,
            string spatialAudio,
            string notificationEvents,
            StringBuilder report,
            ref int failureCount)
        {
            if (audioLogSystem.Length > 0)
            {
                string recoverFragmentBody = ExtractMethodBody(audioLogSystem, "public bool RecoverEncryptedFragment(");
                string populateSaveBody = ExtractMethodBody(audioLogSystem, "public void PopulateSaveData(SaveData data)");
                string loadSaveBody = ExtractMethodBody(audioLogSystem, "public void LoadFromSaveData(SaveData data)");
                string loadEncryptedFragmentBody = ExtractMethodBody(audioLogSystem, "private void LoadEncryptedFragmentState(SaveData data)");
                string enqueuePlaybackBody = ExtractMethodBody(audioLogSystem, "private void EnqueuePlayback(uint logHash)");
                string buildLogLookupBody = ExtractMethodBody(audioLogSystem, "private void BuildLogLookup()");
                string tryResolveLogHashBody = ExtractMethodBody(audioLogSystem, "private bool TryResolveLogHash(AudioLogData data, out uint logHash)");
                string tryBindResolvedLogHashBody = ExtractMethodBody(audioLogSystem, "private bool TryBindResolvedLogHash(uint logHash, AudioLogData data)");
                string playLogByHashBody = ExtractMethodBody(audioLogSystem, "private void PlayLogByHash(uint logHash, AudioLogData data)");
                string playEncryptedPartialPreviewBody = ExtractMethodBody(audioLogSystem, "private void PlayEncryptedPartialPreview(uint logHash, AudioLogData data)");
                string cacheDiscoveryNotificationBody = ExtractMethodBody(audioLogSystem, "private void CacheDiscoveryNotificationHash(uint logHash, AudioLogData data)");
                string resolveFallbackDiscoveryNotificationBody = ExtractMethodBody(audioLogSystem, "private uint ResolveFallbackDiscoveryNotificationHash()");
                string resolveDiscoveryNotificationBody = ExtractMethodBody(audioLogSystem, "private uint ResolveDiscoveryNotificationHash(uint logHash)");
                string trackResolvedLogHashBody = ExtractMethodBody(audioLogSystem, "private void TrackResolvedLogHash(uint logHash)");

                AssertContains(audioLogSystem, "private const uint EncryptedLogCompleteMask = 0xFu", "Encrypted logs use 4-bit completion mask", report, ref failureCount);
                AssertContains(audioLogSystem, "RecoverEncryptedFragment(uint logHash, uint fragmentHash)", "Encrypted fragment recovery API exists", report, ref failureCount);
                AssertContains(recoverFragmentBody, "SignalBeaconMath.MergeRecoveredBits", "Encrypted fragment recovery uses shared bit merge math", report, ref failureCount);
                AssertContains(recoverFragmentBody, "(recoveredBits & EncryptedLogCompleteMask) == EncryptedLogCompleteMask", "Audio log unlock requires all four recovered bits", report, ref failureCount);
                AssertContains(recoverFragmentBody, "if (!storedRecoveredBits)", "Encrypted fragment recovery checks persisted bit state", report, ref failureCount);
                AssertOrder(recoverFragmentBody, "if (!storedRecoveredBits)", "if ((recoveredBits & EncryptedLogCompleteMask) == EncryptedLogCompleteMask)", "Encrypted fragments persist recovered bits before unlock/playback", report, ref failureCount);
                AssertContains(audioLogSystem, "_encryptedFragmentRecoveredBits", "Recovered fragment state is stored outside managed dictionaries", report, ref failureCount);
                AssertContains(audioLogSystem, "TryPlayStatic2DBitCrushed(playbackClip, playbackVolume)", "Partial playback asks SpatialAudioManager for encrypted route status", report, ref failureCount);
                AssertContains(audioLogSystem, "_currentPlaybackBitCrushed = bitCrushRouteActive", "Partial playback state mirrors actual route availability", report, ref failureCount);
                AssertContains(audioLogSystem, "_EncryptedVoiceRouteMissingWarningHash", "Missing encrypted voice route publishes telemetry once", report, ref failureCount);
                AssertContains(audioLogSystem, "NotificationEvents.RegisterMessage(\"LOG DISCOVERED", "Audio log discovery notifications are pre-registered", report, ref failureCount);
                AssertContains(audioLogSystem, "NotificationEvents.PushRegisteredInfo(notificationHash)", "Audio log discovery pushes notification hashes without hot string payloads", report, ref failureCount);
                AssertContains(audioLogSystem, "private bool TryBindResolvedLogHash(uint logHash, AudioLogData data)", "Runtime-resolved audio logs use a canonical hash binding helper", report, ref failureCount);
                AssertContains(tryBindResolvedLogHashBody, "ReferenceEquals(existingData, data)", "Resolved audio log hash binding rejects asset hash collisions", report, ref failureCount);
                AssertContains(tryBindResolvedLogHashBody, "TrackResolvedLogHash(logHash)", "Canonical audio log binding tracks resolved hashes", report, ref failureCount);
                AssertContains(tryBindResolvedLogHashBody, "CacheDiscoveryNotificationHash(logHash, data)", "Canonical audio log binding caches discovery notification hash", report, ref failureCount);
                AssertContains(tryResolveLogHashBody, "return TryBindResolvedLogHash(logHash, data)", "Runtime-resolved audio logs route through canonical binding", report, ref failureCount);
                AssertContains(buildLogLookupBody, "TryBindResolvedLogHash(logHash, data)", "Authored audio log lookup skips non-canonical hash collisions", report, ref failureCount);
                AssertContains(audioLogSystem, "private const int ResolvedLogHashCapacity = 512", "Audio log resolved catalog has fixed hash capacity", report, ref failureCount);
                AssertContains(audioLogSystem, "_resolvedLogHashes = new uint[ResolvedLogHashCapacity]", "Audio log resolved catalog uses flat uint array", report, ref failureCount);
                AssertContains(audioLogSystem, "new Dictionary<uint, AudioLogData>(ResolvedLogHashCapacity)", "Audio log hash lookup is pre-sized to resolved catalog capacity", report, ref failureCount);
                AssertContains(audioLogSystem, "new Dictionary<AudioLogData, uint>(ResolvedLogHashCapacity)", "Audio log reverse lookup is pre-sized to resolved catalog capacity", report, ref failureCount);
                AssertContains(audioLogSystem, "new Dictionary<uint, uint>(ResolvedLogHashCapacity)", "Audio log notification lookup is pre-sized to resolved catalog capacity", report, ref failureCount);
                AssertContains(buildLogLookupBody, "ClearResolvedLogHashes()", "Audio log lookup rebuild clears resolved hash catalog", report, ref failureCount);
                AssertContains(buildLogLookupBody, "TryBindResolvedLogHash(logHash, data)", "Authored audio logs are tracked through canonical binding", report, ref failureCount);
                AssertContains(tryResolveLogHashBody, "TryBindResolvedLogHash(logHash, data)", "Runtime-resolved audio logs are tracked through canonical binding", report, ref failureCount);
                AssertContains(trackResolvedLogHashBody, "_ResolvedLogCatalogFullWarningHash", "Resolved audio log catalog overflow is telemetry-gated", report, ref failureCount);
                AssertContains(audioLogSystem, "private uint ResolveFallbackDiscoveryNotificationHash()", "Audio log fallback discovery notification hash is lazily cached", report, ref failureCount);
                AssertContains(resolveFallbackDiscoveryNotificationBody, "NotificationEvents.TryResolveMessage(_fallbackDiscoveryNotificationHash, out _)", "Fallback discovery notification hash re-registers after NotificationEvents reset", report, ref failureCount);
                AssertContains(cacheDiscoveryNotificationBody, "ResolveFallbackDiscoveryNotificationHash()", "Blank-title audio log discovery uses cached fallback hash", report, ref failureCount);
                AssertContains(cacheDiscoveryNotificationBody, "_discoveryNotificationHashByLogHash.Remove(logHash)", "Stale discovery notification hashes are removed before re-cache", report, ref failureCount);
                AssertContains(resolveDiscoveryNotificationBody, "CacheDiscoveryNotificationHash(logHash, data)", "Discovery notification resolution repairs stale per-log hashes", report, ref failureCount);
                AssertContains(resolveDiscoveryNotificationBody, "return ResolveFallbackDiscoveryNotificationHash()", "Discovery notification fallback guarantees registered fallback hash", report, ref failureCount);
                AssertNotContains(audioLogSystem, "Mathf.", "Audio log signal path uses Unity.Mathematics math helpers instead of Mathf", report, ref failureCount);
                AssertContains(populateSaveBody, "EnsureSaveEncryptedFragmentArrays(data)", "Audio log save path prepares fixed encrypted-fragment arrays", report, ref failureCount);
                AssertContains(populateSaveBody, "for (int i = 0; i < _resolvedLogHashCount; i++)", "Audio log save path iterates resolved hash catalog", report, ref failureCount);
                AssertNotContains(populateSaveBody, "allLogs[i]", "Audio log save path is not restricted to authored allLogs array", report, ref failureCount);
                AssertContains(populateSaveBody, "data.audioLogEncryptedFragmentCount = partialCount", "Audio log save path persists partial encrypted-fragment count", report, ref failureCount);
                AssertContains(populateSaveBody, "data.audioLogEncryptedFragmentHashes[partialCount] = logHash", "Audio log save path persists encrypted-fragment log hashes", report, ref failureCount);
                AssertContains(populateSaveBody, "data.audioLogEncryptedFragmentBits[partialCount] = recoveredBits", "Audio log save path persists encrypted-fragment recovered bits", report, ref failureCount);
                AssertContains(loadSaveBody, "BuildLogLookup()", "Audio log load path rebuilds lookup before hydrating saved hashes", report, ref failureCount);
                AssertContains(loadSaveBody, "TrackResolvedLogHash(logHash)", "Audio log load path repairs resolved catalog for saved authored hashes", report, ref failureCount);
                AssertContains(loadSaveBody, "CacheDiscoveryNotificationHash(logHash, logData)", "Audio log load path repairs notification cache for saved authored hashes", report, ref failureCount);
                AssertContains(loadSaveBody, "LoadEncryptedFragmentState(data)", "Audio log load path restores encrypted-fragment state", report, ref failureCount);
                AssertContains(loadEncryptedFragmentBody, "SetEncryptedFragmentBits(logHash, recoveredBits)", "Audio log load path hydrates encrypted-fragment NativeArray state", report, ref failureCount);
                AssertContains(enqueuePlaybackBody, "(_currentLogHash == logHash && !_currentPlaybackBitCrushed)", "Audio log queue permits full playback after same-hash partial preview", report, ref failureCount);
                AssertContains(playLogByHashBody, "if (data == null || logHash == 0u)", "Full audio log playback rejects null data and zero hashes", report, ref failureCount);
                AssertContains(playLogByHashBody, "float playbackDuration = math.max(0.5f, data.Duration)", "Full audio log playback clamps non-positive duration", report, ref failureCount);
                AssertContains(playLogByHashBody, "TrackResolvedLogHash(logHash)", "Full audio log playback refreshes resolved catalog", report, ref failureCount);
                AssertNotContains(playLogByHashBody, "CacheDiscoveryNotificationHash(logHash, data)", "Full audio log playback avoids notification string cache work", report, ref failureCount);
                AssertContains(playEncryptedPartialPreviewBody, "if (data == null || logHash == 0u)", "Partial encrypted playback rejects null data and zero hashes", report, ref failureCount);
                AssertContains(playEncryptedPartialPreviewBody, "float playbackDuration = math.max(0.5f, data.Duration)", "Partial encrypted playback clamps non-positive duration", report, ref failureCount);
                AssertContains(playEncryptedPartialPreviewBody, "TrackResolvedLogHash(logHash)", "Partial encrypted playback refreshes resolved catalog", report, ref failureCount);
                AssertNotContains(playEncryptedPartialPreviewBody, "CacheDiscoveryNotificationHash(logHash, data)", "Partial encrypted playback avoids notification string cache work", report, ref failureCount);
            }

            if (saveData.Length > 0)
            {
                AssertContains(saveData, "MaxEncryptedAudioLogFragments = 32", "SaveData caps encrypted fragment slots", report, ref failureCount);
                AssertContains(saveData, "audioLogEncryptedFragmentHashes", "SaveData persists encrypted fragment log hashes", report, ref failureCount);
                AssertContains(saveData, "audioLogEncryptedFragmentBits", "SaveData persists encrypted fragment bit masks", report, ref failureCount);
            }

            if (spatialAudio.Length > 0)
            {
                AssertContains(spatialAudio, "public bool TryPlayStatic2DBitCrushed(AudioClip clip, float volume)", "SpatialAudioManager exposes bit-crush route try API", report, ref failureCount);
                AssertContains(spatialAudio, "public bool HasEncryptedVoiceBitCrushRoute => _encryptedVoiceGroup != null", "SpatialAudioManager exposes encrypted route readiness", report, ref failureCount);
                AssertContains(spatialAudio, "hasEncryptedVoiceRoute ? _encryptedVoiceGroup : _interfaceGroup", "SpatialAudioManager preserves interface fallback", report, ref failureCount);
            }

            if (notificationEvents.Length > 0)
            {
                AssertContains(notificationEvents, "public static uint RegisterMessage(string message)", "NotificationEvents exposes cold message registration", report, ref failureCount);
                AssertContains(notificationEvents, "internal static void PushRegisteredInfo(uint messageHash)", "NotificationEvents exposes hash-only info publish path", report, ref failureCount);
                AssertContains(notificationEvents, "private static void PublishRegistered(uint messageHash", "NotificationEvents dispatches registered messages without string payloads", report, ref failureCount);
            }
        }

        private static void RunProgressionNotificationCacheAudit(
            string playerAchievementRegistry,
            string pdaContextualAdvisorySystem,
            string notificationEvents,
            StringBuilder report,
            ref int failureCount)
        {
            if (playerAchievementRegistry.Length > 0)
            {
                string tickBody = ExtractMethodBody(playerAchievementRegistry, "public void Tick(float dt)");
                string slowTickBody = ExtractMethodBody(playerAchievementRegistry, "public void SlowTick()");
                string queueUnlockBody = ExtractMethodBody(playerAchievementRegistry, "private void QueueUnlock(");
                string tryAddUnlockedHashBody = ExtractMethodBody(playerAchievementRegistry, "private bool TryAddUnlockedHash(");
                string tryPushAchievementNotificationBody = ExtractMethodBody(playerAchievementRegistry, "private void TryPushAchievementNotification(");

                AssertContains(playerAchievementRegistry, "[StructLayout(LayoutKind.Explicit, Size = 16)]", "Achievement runtime threshold row declares explicit 16-byte layout", report, ref failureCount);
                AssertContains(playerAchievementRegistry, "private readonly struct AchievementRuntimeDefinition", "Achievement runtime thresholds are tightly packed for hot evaluation", report, ref failureCount);
                AssertContains(playerAchievementRegistry, "private static readonly AchievementRuntimeDefinition[] _runtimeDefinitions", "Achievement hot evaluation uses string-free runtime definition table", report, ref failureCount);
                AssertContains(tickBody, "AbsoluteUniversePosition.DistanceSq", "Achievement swim distance gates with AUP squared distance", report, ref failureCount);
                AssertNotContains(tickBody, "transform.position", "Achievement swim distance does not read transform.position in Tick", report, ref failureCount);
                AssertNotContains(tickBody, "OnBiomeDiscovered", "Achievement Tick does not mutate discovery event subscriptions", report, ref failureCount);
                AssertContains(slowTickBody, "RefreshDiscoveryBindingCold()", "Achievement discovery binding is repaired on slow/cold path", report, ref failureCount);
                AssertContains(slowTickBody, "RefreshDiscoveredBiomeTotalCold()", "Achievement discovery total catch-up runs outside Tick", report, ref failureCount);
                AssertContains(queueUnlockBody, "ReportPendingUnlockQueueOverflow(achievementHash)", "Achievement pending side-effect queue overflow is telemetry-backed", report, ref failureCount);
                AssertContains(tryAddUnlockedHashBody, "ReportUnlockedHashCapacityOverflow(achievementHash)", "Achievement unlocked hash capacity overflow is telemetry-backed", report, ref failureCount);
                AssertContains(tryPushAchievementNotificationBody, "NotificationEvents.TryResolveMessage(notificationHash, out _)", "Achievement notification hash is resolved before registered push", report, ref failureCount);
                AssertContains(tryPushAchievementNotificationBody, "RefreshAchievementPresentation()", "Achievement notification cache repairs after NotificationEvents reset", report, ref failureCount);
                AssertContains(playerAchievementRegistry, "public int DroppedUnlockedHashCount => _droppedUnlockedHashCount", "Achievement overflow counter is exposed for post-mortem", report, ref failureCount);
                AssertContains(playerAchievementRegistry, "public int AchievementNotificationMissCount => _achievementNotificationMissCount", "Achievement notification miss counter is exposed for post-mortem", report, ref failureCount);
            }

            if (pdaContextualAdvisorySystem.Length > 0)
            {
                string pushAdvisoryBody = ExtractMethodBody(pdaContextualAdvisorySystem, "private void PushAdvisory(uint advisoryHash");
                string tryPushRegisteredAdvisoryBody = ExtractMethodBody(pdaContextualAdvisorySystem, "private bool TryPushRegisteredAdvisoryNotification(");
                string unregisterBody = ExtractMethodBody(pdaContextualAdvisorySystem, "private void UnregisterFromTickManager()");

                AssertContains(pushAdvisoryBody, "TryPushRegisteredAdvisoryNotification(advisoryHash)", "PDA advisory push uses registered hash path before string fallback", report, ref failureCount);
                AssertContains(tryPushRegisteredAdvisoryBody, "NotificationEvents.TryResolveMessage(notificationHash, out _)", "PDA advisory notification hash is resolved before registered push", report, ref failureCount);
                AssertContains(tryPushRegisteredAdvisoryBody, "RefreshAdvisoryNotifications()", "PDA advisory notification cache repairs after NotificationEvents reset", report, ref failureCount);
                AssertContains(tryPushRegisteredAdvisoryBody, "ReportAdvisoryNotificationMiss(advisoryHash)", "PDA advisory notification miss is telemetry-backed", report, ref failureCount);
                AssertContains(pdaContextualAdvisorySystem, "public int AdvisoryNotificationMissCount => _advisoryNotificationMissCount", "PDA advisory miss counter is exposed for post-mortem", report, ref failureCount);
                AssertContains(unregisterBody, "GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);", "PDA advisory slow tick unregister is explicit", report, ref failureCount);
            }

            if (notificationEvents.Length > 0)
                AssertContains(notificationEvents, "public static bool TryResolveMessage(uint messageHash, out string message)", "NotificationEvents exposes registered-message resolve for cache repair", report, ref failureCount);
        }

        private static void RunBlueprintGateAudit(
            string itemRegistry,
            string questState,
            string buildableData,
            string moduleCatalog,
            string playerBuilder,
            string pdaConstructionTab,
            StringBuilder report,
            ref int failureCount)
        {
            if (itemRegistry.Length > 0)
            {
                AssertContains(itemRegistry, "public uint BlueprintQuestFlagId => blueprintQuestFlagId", "Item templates expose blueprint quest flag ID", report, ref failureCount);
                AssertContains(itemRegistry, "GlobalRegistry.QuestSystem", "Blueprint visibility resolves quest system through GlobalRegistry", report, ref failureCount);
                AssertContains(itemRegistry, "questSystem.GetFlag(requiredFlag)", "Blueprint visibility gates on quest flag state", report, ref failureCount);
            }

            if (questState.Length > 0)
            {
                AssertContains(questState, "private const int WordCapacity = 320", "Quest state manager uses 320-word packed flag store", report, ref failureCount);
                AssertContains(questState, "new NativeArray<uint>(WordCapacity", "Quest state manager allocates packed NativeArray flag state", report, ref failureCount);
                AssertContains(questState, "public bool GetFlag(uint flagId)", "Quest state exposes hash/flag lookup for blueprint gate", report, ref failureCount);
            }

            if (buildableData.Length > 0)
            {
                AssertContains(buildableData, "[SerializeField] private uint blueprintQuestFlagId", "BuildableData stores construction blueprint quest flag as uint", report, ref failureCount);
                AssertContains(buildableData, "public uint BlueprintQuestFlagId => blueprintQuestFlagId", "BuildableData exposes blueprint quest flag ID", report, ref failureCount);
                AssertContains(buildableData, "public bool IsBlueprintViewable()", "BuildableData exposes quest-backed blueprint visibility check", report, ref failureCount);
                AssertContains(buildableData, "questSystem.GetFlag(blueprintQuestFlagId)", "BuildableData reads packed quest flag for blueprint visibility", report, ref failureCount);
            }

            if (moduleCatalog.Length > 0)
            {
                AssertContains(moduleCatalog, "public int ViewableCount", "ModuleCatalog exposes viewable-only blueprint count", report, ref failureCount);
                AssertContains(moduleCatalog, "public BuildableData GetViewableAt(int index)", "ModuleCatalog exposes viewable-only indexer", report, ref failureCount);
                AssertContains(moduleCatalog, "public int IndexOfViewable(BuildableData data)", "ModuleCatalog resolves viewable-only index", report, ref failureCount);
                AssertContains(moduleCatalog, "data.IsBlueprintViewable()", "ModuleCatalog filters locked construction blueprints", report, ref failureCount);
            }

            if (playerBuilder.Length > 0)
            {
                AssertContains(playerBuilder, "BuildableCount => _buildCatalog != null ? _buildCatalog.ViewableCount : 0", "PlayerBuilder reports viewable buildable count", report, ref failureCount);
                AssertContains(playerBuilder, "_buildCatalog.GetViewableAt(index)", "PlayerBuilder public indexer skips locked blueprints", report, ref failureCount);
                AssertContains(playerBuilder, "BuildReadiness.BlueprintLocked", "PlayerBuilder exposes locked blueprint readiness", report, ref failureCount);
                AssertContains(playerBuilder, "NotifyBuildBlocked(\"BLUEPRINT LOCKED\")", "PlayerBuilder deploy path blocks locked blueprints", report, ref failureCount);
                AssertContains(playerBuilder, "private static bool IsBuildableBlueprintViewable(BuildableData data)", "PlayerBuilder centralizes construction blueprint visibility gate", report, ref failureCount);
            }

            if (pdaConstructionTab.Length > 0)
            {
                AssertContains(pdaConstructionTab, "IPDAEventListener, IQuestEventListener", "PDA construction tab listens to quest unlock events", report, ref failureCount);
                AssertContains(pdaConstructionTab, "QuestEvents.Register(this)", "PDA construction tab registers quest event dirty path", report, ref failureCount);
                AssertContains(pdaConstructionTab, "QuestEvents.Unregister(this)", "PDA construction tab unregisters quest event dirty path", report, ref failureCount);
                AssertContains(pdaConstructionTab, "catalog.ViewableCount", "PDA construction tab renders viewable catalog count", report, ref failureCount);
                AssertContains(pdaConstructionTab, "catalog.GetViewableAt(i)", "PDA construction tab renders viewable-only cards", report, ref failureCount);
                AssertContains(pdaConstructionTab, "CountLockedBlueprintModules(catalog)", "PDA construction tab reports hidden locked blueprint count", report, ref failureCount);
            }
        }

        private static string ReadAssetText(string relativePath, StringBuilder report, ref int failureCount)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                failureCount++;
                report.Append(" MISSING ").Append(relativePath).Append(';');
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static void WriteReport(string json)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, OutputRelativePath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            File.WriteAllText(outputPath, json);
        }

        private static void AssertContains(string source, string token, string label, StringBuilder report, ref int failureCount)
        {
            if (ContainsOrdinal(source, token))
            {
                report.Append(" PASS ").Append(label).Append(';');
                return;
            }

            failureCount++;
            report.Append(" FAIL ").Append(label).Append(';');
        }

        private static void AssertNotContains(string source, string token, string label, StringBuilder report, ref int failureCount)
        {
            if (!ContainsOrdinal(source, token))
            {
                report.Append(" PASS ").Append(label).Append(';');
                return;
            }

            failureCount++;
            report.Append(" FAIL ").Append(label).Append(';');
        }

        private static bool ContainsOrdinal(string source, string token)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        private static void AssertOrder(
            string source,
            string beforeToken,
            string afterToken,
            string label,
            StringBuilder report,
            ref int failureCount)
        {
            if (!string.IsNullOrEmpty(source))
            {
                int beforeIndex = source.IndexOf(beforeToken, StringComparison.Ordinal);
                int afterIndex = source.IndexOf(afterToken, StringComparison.Ordinal);
                if (beforeIndex >= 0 && afterIndex >= 0 && beforeIndex < afterIndex)
                {
                    report.Append(" PASS ").Append(label).Append(';');
                    return;
                }
            }

            failureCount++;
            report.Append(" FAIL ").Append(label).Append(';');
        }

        private static string ExtractMethodBody(string source, string signatureToken)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(signatureToken))
                return string.Empty;

            int signatureIndex = source.IndexOf(signatureToken, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            int openBraceIndex = source.IndexOf('{', signatureIndex);
            if (openBraceIndex < 0)
                return string.Empty;

            int depth = 0;
            for (int i = openBraceIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(openBraceIndex, i - openBraceIndex + 1);
            }

            return string.Empty;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
#endif
