namespace Hecton8.Tests.Editor
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Hecton8.Vehicles.DropPod;
    using NUnit.Framework;

    public sealed class DropPodStaticAudit1602EditTests
    {
        private const string DropPodRuntimeDir = "Assets/_Project/Scripts/Vehicles/DropPod";
        private const string InteractionRuntimeDir = "Assets/_Project/Scripts/Interaction";

        [Test]
        public void DropPodRuntimeDoesNotUseLegacyInteractionRoutes()
        {
            string body = ReadDropPodRuntime();
            Assert.IsFalse(body.Contains("OnMouseDown", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("SendMessage", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("BroadcastMessage", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("GraphicRaycaster", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("ScreenSpaceOverlay", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("ScreenSpaceCamera", StringComparison.Ordinal));
        }

        [Test]
        public void DropPodRuntimePublishesTypedSignalBusCommands()
        {
            string body = ReadDropPodRuntime();
            StringAssert.Contains("SignalBus<DropPodCommandSignal>.TryPushTracked", body);
            StringAssert.Contains("SignalBus<DropPodStatusSignal>.TryPushTracked", body);
            Assert.IsFalse(body.Contains("GlobalSignals.Publish(in DropPodCommandSignal", StringComparison.Ordinal));
        }

        [Test]
        public void DashboardTextUsesSetCharArray()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            StringAssert.Contains("SetCharArray", body);
            Assert.IsFalse(body.Contains(".text =", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("ToString(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("string.Format", StringComparison.Ordinal));
        }

        [Test]
        public void DashboardTextSkipsStableTextMeshWrites()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            StringAssert.Contains("_textDirty", body);
            StringAssert.Contains("_lastRenderedStatusId", body);
            StringAssert.Contains("_lastOxygenValue", body);
            StringAssert.Contains("ResolvePercentMetric", body);
            StringAssert.Contains("ResolveVelocityMetric", body);
            StringAssert.Contains("ResolvePercent01", body);
            StringAssert.Contains("ResolveVelocity01", body);
            StringAssert.Contains("math.isfinite(value)", body);

            string renderNow = ExtractMethodBody(body, "private void RenderNow()");
            string lateFrame = ExtractMethodBody(body, "public void LateFrameTick()");
            StringAssert.Contains("if (_refreshTimer > 0f && !_textDirty)", lateFrame);
            StringAssert.Contains("if (_textDirty || _lastOxygenValue != oxygenValue)", renderNow);
            StringAssert.Contains("if (_textDirty || _lastVelocityValue != velocityValue)", renderNow);
            StringAssert.Contains("if (_textDirty || _lastIntegrityValue != integrityValue)", renderNow);
            StringAssert.Contains("_textDirty = false;", renderNow);
            StringAssert.Contains("ApplyNeedles();", renderNow);
        }

        [Test]
        public void DropPodRuntimeDoesNotCrossDomainsWithManagedEvents()
        {
            string body = ReadDropPodRuntime();
            Assert.IsFalse(body.Contains("event ", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Action<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Action ", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("delegate ", StringComparison.Ordinal));
            StringAssert.Contains("DropPodCommandSignal", body);
            StringAssert.Contains("DropPodStatusSignal", body);
        }

        [Test]
        public void DropPodHotPathsOnlyUseCachedDependencies()
        {
            string body = ReadDropPodRuntime();
            AssertCleanHotPathBodies(body, "public void Tick(");
            AssertCleanHotPathBodies(body, "public void FixedTick(");
            AssertCleanHotPathBodies(body, "void IFixedTickable.FixedTick(");
            AssertCleanHotPathBodies(body, "public void LateFrameTick(");
            AssertCleanHotPathBodies(body, "void Execute(");
        }

        [Test]
        public void DropPodRuntimeUsesDispatcherPhasesOnly()
        {
            string body = ReadDropPodRuntime();
            Assert.IsFalse(body.Contains("void " + "Update(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("void " + "FixedUpdate(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("void " + "LateUpdate(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("IEnumerator", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Start" + "Coroutine", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("System.Linq", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains(".Select(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains(".Where(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains(".ToArray(", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains(".ToList(", StringComparison.Ordinal));
        }

        [Test]
        public void AirlockPromptNamesNextPhysicalAction()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            StringAssert.Contains("private string openPrompt = \"Seal Hatch\";", body);
            StringAssert.Contains("private string sealedPrompt = \"Unseal Hatch\";", body);
            Assert.IsFalse(body.Contains("private string sealedPrompt = \"Hatch Sealed\";", StringComparison.Ordinal));
            StringAssert.Contains("return _sealed ? sealedPrompt : openPrompt;", ExtractMethodBody(body, "public string GetInteractText()"));
            StringAssert.Contains("_sealed ? sealedPrompt : openPrompt", ExtractMethodBody(body, "public bool TryCopyInteractText("));
        }

        [Test]
        public void DropPodRuntimeDoesNotUseDataVaultWriteLocks()
        {
            string body = ReadDropPodRuntime();
            Assert.IsFalse(body.Contains("GlobalDataVault", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("DataVault", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("lock (", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Monitor.Enter", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("SpinLock", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("AcquireWrite", StringComparison.Ordinal));
        }

        [Test]
        public void DropPodSignalsKeepExplicitStableLayout()
        {
            Assert.AreEqual(16, Marshal.SizeOf<DropPodCommandSignal>());
            Assert.AreEqual(0, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.Frame)).ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.CommandId)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.SourceId)).ToInt32());
            Assert.AreEqual(12, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.Flags)).ToInt32());
            Assert.AreEqual(13, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.QualityByte)).ToInt32());
            Assert.AreEqual(14, Marshal.OffsetOf<DropPodCommandSignal>(nameof(DropPodCommandSignal.Sequence)).ToInt32());

            Assert.AreEqual(16, Marshal.SizeOf<DropPodStatusSignal>());
            Assert.AreEqual(0, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.Frame)).ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.StatusId)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.SourceId)).ToInt32());
            Assert.AreEqual(12, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.Flags)).ToInt32());
            Assert.AreEqual(13, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.QualityByte)).ToInt32());
            Assert.AreEqual(14, Marshal.OffsetOf<DropPodStatusSignal>(nameof(DropPodStatusSignal.Sequence)).ToInt32());
            Assert.AreEqual(9u, (uint)DropPodStatusId.FailClosed);
            Assert.AreEqual(10u, (uint)DropPodStatusId.SeatBlockedAirlockOpen);
        }

        [Test]
        public void DropPodSignalBootstrapRechecksDisposedNativeStorage()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodTransitSignals.cs"));
            StringAssert.Contains("SignalBus<DropPodCommandSignal>.HasNativeStorage", body);
            StringAssert.Contains("SignalBus<DropPodStatusSignal>.HasNativeStorage", body);
        }

        [Test]
        public void DropPodSignalsUseFrameLocalMonotonicSequenceCursor()
        {
            string signals = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodTransitSignals.cs"));
            string body = ReadDropPodRuntime();
            StringAssert.Contains("s_signalSequenceState", signals);
            StringAssert.Contains("Interlocked.CompareExchange(ref s_signalSequenceState", signals);
            StringAssert.Contains("observedFrame == frame", signals);
            StringAssert.Contains("observedSequence >= ushort.MaxValue ? ushort.MaxValue : observedSequence + 1", signals);
            StringAssert.Contains("IsNewerSignal", signals);
            StringAssert.Contains("EnsureConfigured();", ExtractMethodBody(signals, "public static ushort NextSequence(uint frame)"));
            StringAssert.Contains("return DropPodSignalLaneBootstrap.NextSequence(frame);", body);
            StringAssert.Contains("signal.Sequence = NextSequence(signal.Frame);", body);
            Assert.IsFalse(body.Contains("private ushort _sequence", StringComparison.Ordinal));
            Assert.IsFalse(signals.Contains("math.clamp(next", StringComparison.Ordinal));
        }

        [Test]
        public void DropPodFeedbackAndQualityInputsRejectNonFiniteValues()
        {
            string body = ReadDropPodRuntime();
            string mathBody = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSplineMath.cs"));
            StringAssert.Contains("public static float SanitizeUnit01", mathBody);
            StringAssert.Contains("public static float SanitizeRange", mathBody);
            StringAssert.Contains("math.isfinite(value) ? value : 0f", mathBody);

            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01)", body);
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(audioVolume, 0f, 1f, 0f)", body);
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(audioPitch, 0.25f, 2.5f, 1f)", body);
            Assert.IsFalse(body.Contains("math.saturate(SignalBusRegistry.GlobalQualityWeight01)", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("math.saturate(audioVolume)", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("math.clamp(audioPitch", StringComparison.Ordinal));
        }

        [Test]
        public void StatusConsumersUseFrameAndSequenceCursor()
        {
            string dashboard = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string lighting = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));
            StringAssert.Contains("_lastStatusSequence", dashboard);
            StringAssert.Contains("_lastStatusSequence", lighting);
            StringAssert.Contains("IsNewerSignal(signal.Frame, signal.Sequence, _lastStatusFrame, _lastStatusSequence)", dashboard);
            StringAssert.Contains("IsNewerSignal(signal.Frame, signal.Sequence, _lastStatusFrame, _lastStatusSequence)", lighting);
            AssertStatusCursorResetsOnEnable(dashboard);
            AssertStatusCursorResetsOnEnable(lighting);
        }

        [Test]
        public void StatusConsumersDrainSnapshotBeforeFirstEnablePresentationWrite()
        {
            string dashboard = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string lighting = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));

            string dashboardEnable = ExtractMethodBody(dashboard, "private void OnEnable()");
            StringAssert.Contains("bool lateRouteReady = TryRegisterLate();", dashboardEnable);
            AssertMethodCallOrder(
                dashboardEnable,
                "DrainStatusSignals();",
                "MarkFailClosedPresentationFallback();",
                "RenderNow();");
            StringAssert.Contains("Application.isPlaying && !lateRouteReady", dashboardEnable);
            AssertMethodCallOrder(
                dashboardEnable,
                "ResetStatusCursor();",
                "DrainStatusSignals();",
                "RenderNow();");

            string lightingEnable = ExtractMethodBody(lighting, "private void OnEnable()");
            StringAssert.Contains("bool lateRouteReady = TryRegisterLate();", lightingEnable);
            AssertMethodCallOrder(
                lightingEnable,
                "DrainStatusSignals();",
                "MarkFailClosedPresentationFallback();",
                "_emergency01 = _targetEmergency01;");
            StringAssert.Contains("Application.isPlaying && !lateRouteReady", lightingEnable);
            AssertMethodCallOrder(
                lightingEnable,
                "ResetStatusCursor();",
                "DrainStatusSignals();",
                "_emergency01 = _targetEmergency01;");
            AssertMethodCallOrder(
                lightingEnable,
                "DrainStatusSignals();",
                "_emergency01 = _targetEmergency01;",
                "ApplyLightingIfNeeded(_emergency01, true);");
        }

        [Test]
        public void PhysicalReceiversUnregisterTheRegisteredCollider()
        {
            string body = ReadDropPodRuntime();
            StringAssert.Contains("_registeredCollider", body);
            Assert.IsFalse(body.Contains("PhysicalHandReceiverRegistry.Unregister(activationCollider, this)", StringComparison.Ordinal));
        }

        [Test]
        public void DropPodRuntimeDoesNotUseObviousReferenceAllocators()
        {
            string body = ReadDropPodRuntime();
            Assert.IsFalse(body.Contains("new List<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("new Dictionary<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("new HashSet<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("new Queue<", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("new RaycastHit[", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("new Collider[", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Start" + "Coroutine", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("GameObject." + "Find", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("FindObjectOfType", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("Camera.main", StringComparison.Ordinal));
        }

        [Test]
        public void CabinColliderValidatorAvoidsMeshColliderArrayAllocation()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "Editor", "DropPodCabinColliderValidator.cs"));
            StringAssert.Contains("s_meshColliderScratch", body);
            StringAssert.Contains("COLD ALLOC: List<MeshCollider>[16]", body);
            StringAssert.Contains("GetComponentsInChildren(true, s_meshColliderScratch)", body);
            StringAssert.Contains("finally", body);
            Assert.IsFalse(body.Contains("MeshCollider[]", StringComparison.Ordinal));
            Assert.IsFalse(body.Contains("GetComponentsInChildren<MeshCollider>(true)", StringComparison.Ordinal));
        }

        [Test]
        public void SensoryFeedbackIsDeferredToLateFrame()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            StringAssert.Contains("public void LateFrameTick()", airlock);
            StringAssert.Contains("DispatchCompletionFeedback();", airlock);
            StringAssert.Contains("public void LateFrameTick()", toggle);
            StringAssert.Contains("DispatchCompletionFeedback();", toggle);
            Assert.IsFalse(ExtractMethodBody(airlock, "private bool QueueSealToggle(").Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(airlock, "private bool QueueSealToggle(").Contains("DispatchHandTarget(", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(toggle, "private bool Toggle(").Contains("QueueAudio(", StringComparison.Ordinal));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            StringAssert.Contains("public void LateFrameTick()", seat);
            StringAssert.Contains("DispatchPendingFeedback();", seat);
            Assert.IsFalse(ExtractMethodBody(seat, "private bool TryBeginTransit(").Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(seat, "private bool TryBeginTransit(").Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(seat, "public bool TryQueueHandPress(").Contains("EnqueueTransitHaptic(", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(seat, "public bool TryQueueHandPress(").Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(ExtractMethodBody(seat, "public bool TryQueueHandPress(").Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
        }

        [Test]
        public void SeatPendingFeedbackSurvivesDispatcherHotSwap()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string hotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("_feedbackPending", hotSwap);
            StringAssert.Contains("TryRegisterTicks();", hotSwap);
        }

        [Test]
        public void SeatEnablePublishesAvailabilityInsteadOfBlindArmedState()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string onEnable = ExtractMethodBody(seat, "private void OnEnable()");
            StringAssert.Contains("PublishSeatAvailabilityStatus(DropPodSignalFlags.VisualOnly);", onEnable);
            Assert.IsFalse(onEnable.Contains("PublishStatus(DropPodStatusId.SeatTransitArmed, DropPodSignalFlags.VisualOnly)", StringComparison.Ordinal));

            string availability = ExtractMethodBody(seat, "private void PublishSeatAvailabilityStatus(");
            StringAssert.Contains("if (IsSeatAvailable())", availability);
            StringAssert.Contains("PublishStatus(DropPodStatusId.SeatTransitArmed, flags);", availability);
            StringAssert.Contains("PublishStatus(DropPodStatusId.SeatBlockedAirlockOpen, (byte)(flags | DropPodSignalFlags.FailClosed));", availability);
        }

        [Test]
        public void DispatcherHotSwapUnregistersPreviousDropPodLanesBeforeRebind()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string dashboard = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string lighting = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));

            string airlockHotSwap = ExtractMethodBody(airlock, "public void OnGlobalRegistryServiceReplaced(");
            AssertMethodCallOrder(airlockHotSwap, "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", "UnregisterTicks();", "if (!isActiveAndEnabled)");
            Assert.IsFalse(airlockHotSwap.Contains("_registeredFixed = false;", StringComparison.Ordinal));
            Assert.IsFalse(airlockHotSwap.Contains("_registeredLate = false;", StringComparison.Ordinal));

            string toggleHotSwap = ExtractMethodBody(toggle, "public void OnGlobalRegistryServiceReplaced(");
            AssertMethodCallOrder(toggleHotSwap, "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", "UnregisterTicks();", "if (!isActiveAndEnabled)");
            Assert.IsFalse(toggleHotSwap.Contains("_registeredLate = false;", StringComparison.Ordinal));

            string seatHotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            AssertMethodCallOrder(seatHotSwap, "case GlobalRegistryServiceSlot.Dispatcher:", "UnregisterTicks();", "if (!isActiveAndEnabled)");
            Assert.IsFalse(seatHotSwap.Contains("_registeredFixed = false;", StringComparison.Ordinal));
            Assert.IsFalse(seatHotSwap.Contains("_registeredLate = false;", StringComparison.Ordinal));

            string dashboardHotSwap = ExtractMethodBody(dashboard, "public void OnGlobalRegistryServiceReplaced(");
            AssertMethodCallOrder(dashboardHotSwap, "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", "UnregisterLate();", "if (!isActiveAndEnabled)");
            StringAssert.Contains("currentService == null || !TryRegisterLate()", dashboardHotSwap);
            StringAssert.Contains("MarkFailClosedPresentationFallback();", dashboardHotSwap);
            Assert.IsFalse(dashboardHotSwap.Contains("_registeredLate = false;", StringComparison.Ordinal));
            string dashboardFallback = ExtractMethodBody(dashboard, "private void MarkFailClosedPresentationFallback()");
            StringAssert.Contains("_statusId = DropPodStatusId.FailClosed;", dashboardFallback);
            StringAssert.Contains("_textDirty = true;", dashboardFallback);
            Assert.IsFalse(dashboardFallback.Contains("RenderNow();", StringComparison.Ordinal));
            Assert.IsFalse(dashboardFallback.Contains("SetCharArray", StringComparison.Ordinal));

            string lightingHotSwap = ExtractMethodBody(lighting, "public void OnGlobalRegistryServiceReplaced(");
            AssertMethodCallOrder(lightingHotSwap, "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", "UnregisterLate();", "if (!isActiveAndEnabled)");
            StringAssert.Contains("currentService == null || !TryRegisterLate()", lightingHotSwap);
            StringAssert.Contains("MarkFailClosedPresentationFallback();", lightingHotSwap);
            Assert.IsFalse(lightingHotSwap.Contains("_registeredLate = false;", StringComparison.Ordinal));
            string lightingFallback = ExtractMethodBody(lighting, "private void MarkFailClosedPresentationFallback()");
            StringAssert.Contains("SetTargetEmergency01(FullAlertLightWeight);", lightingFallback);
            Assert.IsFalse(lightingFallback.Contains("ApplyLighting", StringComparison.Ordinal));
        }

        [Test]
        public void ActiveMotionCancelsIfDispatcherRouteCannotRecover()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));

            string airlockHotSwap = ExtractMethodBody(airlock, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("if (_moving)", airlockHotSwap);
            StringAssert.Contains("currentService == null || !TryRegisterTicks()", airlockHotSwap);
            StringAssert.Contains("CancelMotionForLostDispatcherRoute();", airlockHotSwap);
            StringAssert.Contains("if (currentService == null || !TryRegisterLate())", airlockHotSwap);
            StringAssert.Contains("ClearLateOnlyStateForLostDispatcherRoute();", airlockHotSwap);

            string airlockCancel = ExtractMethodBody(airlock, "private void CancelMotionForLostDispatcherRoute()");
            StringAssert.Contains("_moving = false;", airlockCancel);
            StringAssert.Contains("_feedbackPending = false;", airlockCancel);
            StringAssert.Contains("FreezeMotionAtCurrentSealPose();", airlockCancel);
            Assert.IsFalse(airlockCancel.Contains("SnapToCommittedSealState();", StringComparison.Ordinal));
            StringAssert.Contains("ClearHandTarget();", airlockCancel);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);", airlockCancel);
            StringAssert.Contains("UnregisterTicks();", airlockCancel);
            Assert.IsFalse(airlockCancel.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(airlockCancel.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
            string airlockFreeze = ExtractMethodBody(airlock, "private void FreezeMotionAtCurrentSealPose()");
            StringAssert.Contains("_seal01 = DropPodSplineMath.SanitizeUnit01(_seal01);", airlockFreeze);
            StringAssert.Contains("_targetSeal01 = _seal01;", airlockFreeze);
            StringAssert.Contains("_sealed = _seal01 >= 0.995f;", airlockFreeze);
            StringAssert.Contains("ApplyHatchRotation(DropPodSplineMath.SmoothStep01(_seal01));", airlockFreeze);
            string airlockLateOnlyClear = ExtractMethodBody(airlock, "private void ClearLateOnlyStateForLostDispatcherRoute()");
            StringAssert.Contains("_feedbackPending = false;", airlockLateOnlyClear);
            StringAssert.Contains("ClearHandTarget();", airlockLateOnlyClear);
            StringAssert.Contains("UnregisterTicks();", airlockLateOnlyClear);
            Assert.IsFalse(airlockLateOnlyClear.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(airlockLateOnlyClear.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));

            string toggleHotSwap = ExtractMethodBody(toggle, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("if (_moving)", toggleHotSwap);
            StringAssert.Contains("currentService == null || !TryRegisterTicks()", toggleHotSwap);
            StringAssert.Contains("CancelMotionForLostDispatcherRoute();", toggleHotSwap);
            StringAssert.Contains("if (currentService == null || !TryRegisterTicks())", toggleHotSwap);
            StringAssert.Contains("ClearLateOnlyStateForLostDispatcherRoute();", toggleHotSwap);

            string toggleCancel = ExtractMethodBody(toggle, "private void CancelMotionForLostDispatcherRoute()");
            StringAssert.Contains("_moving = false;", toggleCancel);
            StringAssert.Contains("_feedbackPending = false;", toggleCancel);
            StringAssert.Contains("SnapVisualToCommittedState();", toggleCancel);
            StringAssert.Contains("UnregisterTicks();", toggleCancel);
            Assert.IsFalse(toggleCancel.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(toggleCancel.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
            string toggleLateOnlyClear = ExtractMethodBody(toggle, "private void ClearLateOnlyStateForLostDispatcherRoute()");
            StringAssert.Contains("_feedbackPending = false;", toggleLateOnlyClear);
            StringAssert.Contains("SnapVisualToCommittedState();", toggleLateOnlyClear);
            StringAssert.Contains("UnregisterTicks();", toggleLateOnlyClear);
            Assert.IsFalse(toggleLateOnlyClear.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(toggleLateOnlyClear.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));

            string seatHotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("if (_transiting)", seatHotSwap);
            StringAssert.Contains("currentService == null || !TryRegisterTicks()", seatHotSwap);
            StringAssert.Contains("AbortTransitForLostDispatcherRoute();", seatHotSwap);
            StringAssert.Contains("if (currentService == null || !TryRegisterLate())", seatHotSwap);
            StringAssert.Contains("ClearPendingFeedback();", seatHotSwap);

            string seatAbort = ExtractMethodBody(seat, "private void AbortTransitForLostDispatcherRoute()");
            StringAssert.Contains("AbortTransitLocal(false);", seatAbort);
            StringAssert.Contains("RestoreInputBlock();", seatAbort);
            StringAssert.Contains("_feedbackPending = false;", seatAbort);
            StringAssert.Contains("_cameraRouteFailClosedPending = false;", seatAbort);
            StringAssert.Contains("_pendingFeedbackEventId = 0u;", seatAbort);
            StringAssert.Contains("_pendingFeedbackMotorMask = 0;", seatAbort);
            StringAssert.Contains("PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);", seatAbort);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);", seatAbort);
            StringAssert.Contains("UnregisterTicks();", seatAbort);
            Assert.IsFalse(seatAbort.Contains("QueueFeedback(", StringComparison.Ordinal));
            Assert.IsFalse(seatAbort.Contains("_seated = true", StringComparison.Ordinal));
        }

        [Test]
        public void SeatTransitAbortsIfCameraRouteDisappearsBeforeCompletion()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string lateFrame = ExtractMethodBody(seat, "public void LateFrameTick()");
            int firstFeedbackIndex = lateFrame.IndexOf("DispatchPendingFeedback();", StringComparison.Ordinal);
            int cameraLossIndex = lateFrame.IndexOf("if (_cameraTransform == null)", StringComparison.Ordinal);
            int abortIndex = lateFrame.IndexOf("AbortTransitForLostCameraRoute();", StringComparison.Ordinal);
            int splineIndex = lateFrame.IndexOf("float t = DropPodSplineMath.ResolveTransitT", StringComparison.Ordinal);
            StringAssert.Contains("DispatchPendingCameraRouteFailure()", lateFrame);
            Assert.GreaterOrEqual(firstFeedbackIndex, 0);
            Assert.Greater(cameraLossIndex, firstFeedbackIndex);
            Assert.Greater(abortIndex, cameraLossIndex);
            Assert.Greater(splineIndex, abortIndex);

            string abortWrapper = ExtractMethodBody(seat, "private void AbortTransitForLostCameraRoute()");
            StringAssert.Contains("AbortTransitForLostCameraRoute(true);", abortWrapper);

            string abort = ExtractMethodBody(seat, "private void AbortTransitForLostCameraRoute(bool queueFeedback)");
            StringAssert.Contains("_cameraRouteFailClosedPending = false;", abort);
            StringAssert.Contains("AbortTransitLocal(false);", abort);
            StringAssert.Contains("RestoreInputBlock();", abort);
            StringAssert.Contains("PublishCommand(DropPodCommandId.AbortTransit", abort);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed", abort);
            StringAssert.Contains("if (queueFeedback)", abort);
            StringAssert.Contains("QueueFeedback(", abort);
            StringAssert.Contains("UnregisterFixed();", abort);
            Assert.IsFalse(abort.Contains("_seated = true", StringComparison.Ordinal));

            string hotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("case GlobalRegistryServiceSlot.Player:", hotSwap);
            StringAssert.Contains("Transform previousCameraTransform = _cameraTransform;", hotSwap);
            StringAssert.Contains("_transiting && _cameraTransform != previousCameraTransform", hotSwap);
            StringAssert.Contains("_cameraRouteFailClosedPending = true;", hotSwap);
            StringAssert.Contains("UnregisterFixed();", hotSwap);
            StringAssert.Contains("TryRegisterLate();", hotSwap);
            StringAssert.Contains("if (!TryRegisterLate())", hotSwap);
            StringAssert.Contains("AbortTransitForLostCameraRoute(false);", hotSwap);
            Assert.IsFalse(hotSwap.Contains("AbortTransitForLostCameraRoute();", StringComparison.Ordinal));
            Assert.IsFalse(hotSwap.Contains("QueueFeedback(", StringComparison.Ordinal));

            string cameraDispatch = ExtractMethodBody(seat, "private bool DispatchPendingCameraRouteFailure()");
            StringAssert.Contains("if (!_cameraRouteFailClosedPending)", cameraDispatch);
            StringAssert.Contains("return false;", cameraDispatch);
            StringAssert.Contains("AbortTransitForLostCameraRoute();", cameraDispatch);
            StringAssert.Contains("return true;", cameraDispatch);
        }

        [Test]
        public void SeatInputRestorePreservesForeignInputBlocks()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            StringAssert.Contains("TransitInputBlockMask", seat);
            StringAssert.Contains("_ownedInputBlockBits", seat);
            Assert.IsFalse(seat.Contains("_previousInputBlockMask", StringComparison.Ordinal));

            StringAssert.Contains("private bool TryBlockInput()", seat);
            Assert.IsFalse(seat.Contains("private void BlockInput()", StringComparison.Ordinal));

            string tryBeginTransit = ExtractMethodBody(seat, "private bool TryBeginTransit(");
            int inputBlockIndex = tryBeginTransit.IndexOf("if (!TryBlockInput())", StringComparison.Ordinal);
            int tickRegisterIndex = tryBeginTransit.IndexOf("if (!TryRegisterTicks())", StringComparison.Ordinal);
            int transitStartIndex = tryBeginTransit.IndexOf("_transiting = true;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(inputBlockIndex, 0);
            Assert.Greater(tickRegisterIndex, inputBlockIndex);
            Assert.Greater(transitStartIndex, tickRegisterIndex);

            string blockInput = ExtractMethodBody(seat, "private bool TryBlockInput()");
            StringAssert.Contains("_ownedInputBlockBits = TransitInputBlockMask & ~currentMask;", blockInput);
            StringAssert.Contains("currentMask | TransitInputBlockMask", blockInput);
            StringAssert.Contains("return false;", blockInput);
            StringAssert.Contains("return true;", blockInput);

            string restore = ExtractMethodBody(seat, "private void RestoreInputBlock()");
            StringAssert.Contains("currentMask", restore);
            StringAssert.Contains("~_ownedInputBlockBits", restore);
            StringAssert.Contains("_ownedInputBlockBits = 0u;", restore);
            Assert.IsFalse(restore.Contains("SetInputBlockMask(_previousInputBlockMask)", StringComparison.Ordinal));

            string registerTicks = ExtractMethodBody(seat, "private bool TryRegisterTicks()");
            StringAssert.Contains("GlobalRegistry.Dispatcher == null", registerTicks);
            StringAssert.Contains("_seatLockMotor.HasControllableBody", registerTicks);
            StringAssert.Contains("if (fixedReady && TryRegisterLate())", registerTicks);
            StringAssert.Contains("UnregisterTicks();", registerTicks);

            string registerLate = ExtractMethodBody(seat, "private bool TryRegisterLate()");
            StringAssert.Contains("GlobalRegistry.Dispatcher == null", registerLate);
            StringAssert.Contains("return false;", registerLate);
            StringAssert.Contains("return true;", registerLate);
        }

        [Test]
        public void SeatInputHotSwapRebindsInputBlockWithoutCallbackPresentation()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            StringAssert.Contains("private IInputDeterminismService _inputBlockService;", seat);
            StringAssert.Contains("_inputRouteFailClosedPending", seat);
            StringAssert.Contains("_cameraRouteFailClosedPending", seat);

            string hotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("case GlobalRegistryServiceSlot.Input:", hotSwap);
            StringAssert.Contains("RebindInputBlockService(currentService as IInputDeterminismService);", hotSwap);

            string rebind = ExtractMethodBody(seat, "private void RebindInputBlockService(");
            StringAssert.Contains("bool shouldKeepBlocked = _transiting;", rebind);
            StringAssert.Contains("RestoreInputBlock();", rebind);
            StringAssert.Contains("_inputBlockService = currentService;", rebind);
            StringAssert.Contains("InputDispatcher.TryResolveActiveRuntime(ref dispatcher)", rebind);
            StringAssert.Contains("if (TryBlockInput())", rebind);
            StringAssert.Contains("AbortTransitForLostInputRoute();", rebind);
            Assert.IsFalse(rebind.Contains("PublishCommand(", StringComparison.Ordinal));
            Assert.IsFalse(rebind.Contains("PublishStatus(", StringComparison.Ordinal));
            Assert.IsFalse(rebind.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(rebind.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));

            string abort = ExtractMethodBody(seat, "private void AbortTransitForLostInputRoute()");
            StringAssert.Contains("_feedbackPending = false;", abort);
            StringAssert.Contains("_inputRouteFailClosedPending = true;", abort);
            StringAssert.Contains("_cameraRouteFailClosedPending = false;", abort);
            StringAssert.Contains("UnregisterFixed();", abort);
            StringAssert.Contains("if (TryRegisterLate())", abort);
            StringAssert.Contains("return;", abort);
            StringAssert.Contains("_inputRouteFailClosedPending = false;", abort);
            StringAssert.Contains("AbortTransitLocal(false);", abort);
            StringAssert.Contains("RestoreInputBlock();", abort);
            StringAssert.Contains("UnregisterTicks();", abort);
            StringAssert.Contains("PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);", abort);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);", abort);
            Assert.IsFalse(abort.Contains("ApplyCameraPose(", StringComparison.Ordinal));
            Assert.IsFalse(abort.Contains("QueueFeedback(", StringComparison.Ordinal));
            Assert.IsFalse(abort.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(abort.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));

            string lateFrame = ExtractMethodBody(seat, "public void LateFrameTick()");
            StringAssert.Contains("DispatchPendingInputRouteFailure() || DispatchPendingCameraRouteFailure()", lateFrame);

            string dispatch = ExtractMethodBody(seat, "private bool DispatchPendingInputRouteFailure()");
            StringAssert.Contains("AbortTransitLocal();", dispatch);
            StringAssert.Contains("RestoreInputBlock();", dispatch);
            StringAssert.Contains("QueueFeedback(", dispatch);
            StringAssert.Contains("UnregisterFixed();", dispatch);
            StringAssert.Contains("PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);", dispatch);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);", dispatch);
            Assert.IsFalse(dispatch.Contains("QueueAudio(", StringComparison.Ordinal));
            Assert.IsFalse(dispatch.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
        }

        [Test]
        public void SeatTransitRejectsInvalidSplineAndRotationState()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string tryBeginTransit = ExtractMethodBody(seat, "private bool TryBeginTransit(");
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_startPosition)", tryBeginTransit);
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_controlA)", tryBeginTransit);
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_controlB)", tryBeginTransit);
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_endPosition)", tryBeginTransit);
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_startRotation)", tryBeginTransit);
            StringAssert.Contains("!DropPodSplineMath.IsFinite(_endRotation)", tryBeginTransit);
            StringAssert.Contains("PublishStatus(DropPodStatusId.SeatBlockedAirlockOpen", tryBeginTransit);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed", tryBeginTransit);
            StringAssert.Contains("QueueFeedbackIfLateRouteAvailable(", tryBeginTransit);
            Assert.IsFalse(tryBeginTransit.Contains("QueueFeedback(_cachedTransform", StringComparison.Ordinal));
            StringAssert.Contains("return false;", tryBeginTransit);

            string feedbackFallback = ExtractMethodBody(seat, "private void QueueFeedbackIfLateRouteAvailable(");
            StringAssert.Contains("QueueFeedback(position, eventId, motorMask);", feedbackFallback);
            StringAssert.Contains("if (!TryRegisterLate())", feedbackFallback);
            StringAssert.Contains("ClearPendingFeedback();", feedbackFallback);

            string clearFeedback = ExtractMethodBody(seat, "private void ClearPendingFeedback()");
            StringAssert.Contains("_feedbackPending = false;", clearFeedback);
            StringAssert.Contains("_pendingFeedbackEventId = 0u;", clearFeedback);
            StringAssert.Contains("_pendingFeedbackMotorMask = 0;", clearFeedback);

            string resolveSeatRotation = ExtractMethodBody(seat, "private Quaternion ResolveSeatRotation()");
            StringAssert.Contains("!DropPodSplineMath.IsFinite(target)", resolveSeatRotation);
            StringAssert.Contains("float rollBlend = DropPodSplineMath.SanitizeUnit01(cameraRollBlend);", resolveSeatRotation);
            StringAssert.Contains("rollBlend <= 0.0001f", resolveSeatRotation);
            StringAssert.Contains("forward.sqrMagnitude <= 0.000001f", resolveSeatRotation);
            StringAssert.Contains("DropPodSplineMath.ResolveNlerp(noRoll, target, rollBlend)", resolveSeatRotation);
            Assert.IsFalse(resolveSeatRotation.Contains("ResolveNlerp(noRoll, target, cameraRollBlend)", StringComparison.Ordinal));
        }

        [Test]
        public void SeatFallbackInteractUsesBothMotorFeedback()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string interact = ExtractMethodBody(seat, "public void Interact(");
            string complete = ExtractMethodBody(seat, "private void CompleteTransit()");
            StringAssert.Contains("TryBeginTransit(DropPodSignalFlags.PlayerFallback, BothMotorMask);", interact);
            StringAssert.Contains("QueueFeedback(_endPosition, completeAudioEventId, BothMotorMask);", complete);
            Assert.IsFalse(interact.Contains("TryBeginTransit(DropPodSignalFlags.PlayerFallback, 0)", StringComparison.Ordinal));
            Assert.IsFalse(complete.Contains("ResolveMotorMask(PhysicalHandSide.Right)", StringComparison.Ordinal));
        }

        [Test]
        public void SeatTransitDurationIsFiniteAndBounded()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string lateFrame = ExtractMethodBody(seat, "public void LateFrameTick()");
            StringAssert.Contains("ResolveTransitDuration(transitSeconds)", lateFrame);

            string duration = ExtractMethodBody(seat, "private static float ResolveTransitDuration(");
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(seconds, MinTransitSeconds, MaxTransitSeconds, FallbackTransitSeconds)", duration);
            StringAssert.Contains("MinTransitSeconds", seat);
            StringAssert.Contains("MaxTransitSeconds", seat);
            StringAssert.Contains("FallbackTransitSeconds", seat);
        }

        [Test]
        public void SeatFixedTickDropsStaleMotorRegistration()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string fixedTick = ExtractMethodBody(seat, "public void FixedTick(");
            StringAssert.Contains("_seatLockMotor == null", fixedTick);
            StringAssert.Contains("!_seatLockMotor.HasControllableBody", fixedTick);
            StringAssert.Contains("UnregisterFixed();", fixedTick);

            string hotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("GlobalRegistryServiceSlot.PlayerMotor", hotSwap);
            StringAssert.Contains("RefreshSeatLockMotorRegistration();", hotSwap);

            string refresh = ExtractMethodBody(seat, "private void RefreshSeatLockMotorRegistration()");
            StringAssert.Contains("UnregisterFixed();", refresh);
            StringAssert.Contains("TryRegisterTicks();", refresh);
            StringAssert.Contains("_transiting", refresh);
            StringAssert.Contains("HasControllableBody", refresh);
        }

        [Test]
        public void LocalEulerHelpersRejectNonFiniteAuthoringRotations()
        {
            string math = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSplineMath.cs"));
            string resolveLocalEuler = ExtractMethodBody(math, "public static Quaternion ResolveLocalEulerNoAlloc(");
            StringAssert.Contains("if (!IsFinite(degrees))", resolveLocalEuler);
            StringAssert.Contains("return Quaternion.identity;", resolveLocalEuler);
            StringAssert.Contains("SanitizeAuthoringEulerDegrees(degrees.x)", resolveLocalEuler);
            StringAssert.Contains("SanitizeAuthoringEulerDegrees(degrees.y)", resolveLocalEuler);
            StringAssert.Contains("SanitizeAuthoringEulerDegrees(degrees.z)", resolveLocalEuler);
            StringAssert.Contains("return Quaternion.Euler(safeDegrees);", resolveLocalEuler);

            string sanitizer = ExtractMethodBody(math, "private static float SanitizeAuthoringEulerDegrees(");
            StringAssert.Contains("SanitizeRange(degrees, -MaxAuthoringEulerDegrees, MaxAuthoringEulerDegrees, 0f)", sanitizer);
            StringAssert.Contains("private const float MaxAuthoringEulerDegrees = 360f;", math);
        }

        [Test]
        public void DashboardToggleSanitizesMotionDurationAndNonFiniteStep()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string toggle = ExtractMethodBody(body, "private bool Toggle(");
            StringAssert.Contains("if (!isActiveAndEnabled || !DropPodSplineMath.IsFinite(worldPosition))", toggle);
            StringAssert.Contains("_moving = math.abs(_target01 - _position01) > 0.001f;", toggle);
            Assert.IsFalse(toggle.Contains("_moving = true;", StringComparison.Ordinal));

            string advance = ExtractMethodBody(body, "private void AdvanceVisualMotion(");
            StringAssert.Contains("ResolveMotionDuration(motionSeconds)", advance);
            StringAssert.Contains("!math.isfinite(next)", advance);
            StringAssert.Contains("next = _target01;", advance);
            StringAssert.Contains("_moving = false;", advance);

            string duration = ExtractMethodBody(body, "private static float ResolveMotionDuration(");
            StringAssert.Contains("math.isfinite(seconds)", duration);
            StringAssert.Contains("MinimumMotionSeconds", duration);
            StringAssert.Contains("MaximumMotionSeconds", duration);
            StringAssert.Contains("math.clamp", duration);
        }

        [Test]
        public void DashboardToggleUsesBothMotorsForUnknownHandFallback()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string interact = ExtractMethodBody(body, "public void Interact(");
            StringAssert.Contains("Toggle(position, BothMotorMask, DropPodSignalFlags.PlayerFallback);", interact);

            string queue = ExtractMethodBody(body, "public bool TryQueueHandPress(");
            StringAssert.Contains("Toggle(handPosition, ResolveMotorMask(fallbackHandSide), DropPodSignalFlags.PhysicalHand);", queue);
            StringAssert.Contains("private byte _pendingFeedbackMotorMask = BothMotorMask;", body);

            string toggle = ExtractMethodBody(body, "private bool Toggle(");
            StringAssert.Contains("_pendingFeedbackMotorMask = feedbackMotorMask;", toggle);

            string haptic = ExtractMethodBody(body, "private void EnqueueClickHaptic(");
            StringAssert.Contains("motorMask);", haptic);
            Assert.IsFalse(haptic.Contains("ResolveMotorMask(handSide)", StringComparison.Ordinal));
            Assert.IsFalse(haptic.Contains("handSide == PhysicalHandSide.Left ? (byte)0b0001 : (byte)0b0010", StringComparison.Ordinal));

            string resolver = ExtractMethodBody(body, "private static byte ResolveMotorMask(");
            StringAssert.Contains("if (handSide == PhysicalHandSide.Left)", resolver);
            StringAssert.Contains("if (handSide == PhysicalHandSide.Right)", resolver);
            StringAssert.Contains("return BothMotorMask;", resolver);
        }

        [Test]
        public void AirlockSanitizesMotionDurationAndIkTargetTuning()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string fixedTick = ExtractMethodBody(body, "void IFixedTickable.FixedTick(");
            StringAssert.Contains("ResolveSealDuration(sealSeconds)", fixedTick);
            StringAssert.Contains("!math.isfinite(next)", fixedTick);
            StringAssert.Contains("next = _targetSeal01;", fixedTick);
            StringAssert.Contains("_moving = false;", fixedTick);

            string handTarget = ExtractMethodBody(body, "private void DispatchHandTarget()");
            StringAssert.Contains("ResolveHandTargetHoldSeconds(handTargetHoldSeconds)", handTarget);
            StringAssert.Contains("ResolveHandTargetBlend(handTargetBlend)", handTarget);

            string queue = ExtractMethodBody(body, "private bool QueueSealToggle(");
            StringAssert.Contains("PhysicalHandSide ikHandSide = ResolveIkHandSide(handSide);", queue);
            StringAssert.Contains("_activeHandSide = ikHandSide;", queue);
            StringAssert.Contains("_pendingFeedbackMotorMask = feedbackMotorMask;", queue);
            StringAssert.Contains("private byte _pendingFeedbackMotorMask = BothMotorMask;", body);

            string interact = ExtractMethodBody(body, "public void Interact(");
            StringAssert.Contains("QueueSealToggle(samplePosition, Vector3.forward, PhysicalHandSide.Right, BothMotorMask, DropPodSignalFlags.PlayerFallback);", interact);

            string physicalPress = ExtractMethodBody(body, "public bool TryQueueHandPress(");
            StringAssert.Contains("ResolveMotorMask(fallbackHandSide)", physicalPress);

            string handSide = ExtractMethodBody(body, "private static PhysicalHandSide ResolveIkHandSide(");
            StringAssert.Contains("PhysicalHandSide.Left || handSide == PhysicalHandSide.Right", handSide);
            StringAssert.Contains("return PhysicalHandSide.Right;", handSide);

            string duration = ExtractMethodBody(body, "private static float ResolveSealDuration(");
            StringAssert.Contains("math.isfinite(seconds)", duration);
            StringAssert.Contains("MinMotionSeconds", duration);
            StringAssert.Contains("MaxMotionSeconds", duration);
            StringAssert.Contains("math.clamp", duration);

            string hold = ExtractMethodBody(body, "private static float ResolveHandTargetHoldSeconds(");
            StringAssert.Contains("math.isfinite(seconds)", hold);
            StringAssert.Contains("0.02f", hold);
            StringAssert.Contains("0.5f", hold);
            StringAssert.Contains("math.clamp", hold);

            string blend = ExtractMethodBody(body, "private static float ResolveHandTargetBlend(");
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(blend)", blend);
        }

        [Test]
        public void DashboardTextSanitizesRefreshCadenceAndNeedleAuthoringValues()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string lateFrame = ExtractMethodBody(body, "public void LateFrameTick()");
            StringAssert.Contains("ResolveRefreshInterval(quality)", lateFrame);

            string applyNeedles = ExtractMethodBody(body, "private void ApplyNeedles()");
            StringAssert.Contains("ResolveNeedleJitterDegrees(maxJitterDegrees)", applyNeedles);

            string applyNeedle = ExtractMethodBody(body, "private void ApplyNeedle(");
            StringAssert.Contains("ResolveNeedleSweepDegrees(needleSweepDegrees)", applyNeedle);
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(jitterDegrees, -MaxNeedleJitterDegrees, MaxNeedleJitterDegrees, 0f)", applyNeedle);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(value01)", applyNeedle);

            string refreshInterval = ExtractMethodBody(body, "private float ResolveRefreshInterval(");
            StringAssert.Contains("ResolveRefreshSeconds(lowTierRefreshSeconds)", refreshInterval);
            StringAssert.Contains("ResolveRefreshSeconds(highTierRefreshSeconds)", refreshInterval);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(quality)", refreshInterval);

            string refreshSeconds = ExtractMethodBody(body, "private static float ResolveRefreshSeconds(");
            StringAssert.Contains("math.isfinite(seconds)", refreshSeconds);
            StringAssert.Contains("0.24f", refreshSeconds);

            string sweep = ExtractMethodBody(body, "private static float ResolveNeedleSweepDegrees(");
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(value, 1f, MaxNeedleSweepDegrees, 126f)", sweep);
            StringAssert.Contains("private const float MaxNeedleSweepDegrees = 220f;", body);

            string jitter = ExtractMethodBody(body, "private static float ResolveNeedleJitterDegrees(");
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(value, 0f, MaxNeedleJitterDegrees, 0f)", jitter);
            StringAssert.Contains("private const float MaxNeedleJitterDegrees = 12f;", body);

            string append = ExtractMethodBody(body, "private static int Append(");
            StringAssert.Contains("if (cursor >= buffer.Length)", append);
            StringAssert.Contains("return buffer.Length;", append);
            StringAssert.Contains("if (cursor < 0)", append);
        }

        [Test]
        public void LateFrameDeltaClampsRejectNonFiniteDispatcherTime()
        {
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));
            string dashboard = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string lighting = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));

            string seatLateFrame = ExtractMethodBody(seat, "public void LateFrameTick()");
            StringAssert.Contains("ResolveLateDeltaSeconds();", seatLateFrame);
            string seatDelta = ExtractMethodBody(seat, "private float ResolveLateDeltaSeconds()");
            StringAssert.Contains("rawDeltaSeconds", seatDelta);
            StringAssert.Contains("math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f", seatDelta);

            string dashboardDelta = ExtractMethodBody(dashboard, "private float ResolveLateDeltaSeconds(");
            StringAssert.Contains("rawDeltaSeconds", dashboardDelta);
            StringAssert.Contains("math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f", dashboardDelta);

            string lightingLateFrame = ExtractMethodBody(lighting, "public void LateFrameTick()");
            StringAssert.Contains("ResolveLateDeltaSeconds();", lightingLateFrame);
            string lightingDelta = ExtractMethodBody(lighting, "private float ResolveLateDeltaSeconds()");
            StringAssert.Contains("rawDeltaSeconds", lightingDelta);
            StringAssert.Contains("math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f", lightingDelta);
        }

        [Test]
        public void KinematicTerminalBridgeSanitizesCadenceAndHapticFallback()
        {
            string body = File.ReadAllText("Assets/_Project/Scripts/Interaction/KinematicTerminalInteractionBridge.cs");

            string tick = ExtractMethodBody(body, "public void Tick(");
            StringAssert.Contains("math.isfinite(deltaTime) ? deltaTime : 0f", tick);

            string actionFlags = ExtractMethodBody(body, "private TerminalActionFlags ResolveTerminalActionFlags(");
            StringAssert.Contains("ResolveFiniteAnalogDelta(new float2(state.ScrollDelta.x, state.ScrollDelta.y))", actionFlags);

            string onDisable = ExtractMethodBody(body, "private void OnDisable()");
            StringAssert.Contains("_pendingPressHaptic = false;", onDisable);
            StringAssert.Contains("_pendingPressHapticMotorMask = 0;", onDisable);

            string onEnable = ExtractMethodBody(body, "private void OnEnable()");
            StringAssert.Contains("if (!TryRegister())", onEnable);
            StringAssert.Contains("ClearRuntimeStateForLostDispatcherRoute();", onEnable);
            Assert.Less(onEnable.IndexOf("TryRegisterHotSwapListener();", StringComparison.Ordinal), onEnable.IndexOf("if (!TryRegister())", StringComparison.Ordinal));

            string onDestroy = ExtractMethodBody(body, "private void OnDestroy()");
            StringAssert.Contains("ClearHandTarget();", onDestroy);
            StringAssert.Contains("TryUnregister();", onDestroy);
            StringAssert.Contains("TryUnregisterHotSwapListener();", onDestroy);
            StringAssert.Contains("_pressedLastTick = false;", onDestroy);
            StringAssert.Contains("_pendingPressHaptic = false;", onDestroy);
            StringAssert.Contains("_pendingPressHapticMotorMask = 0;", onDestroy);
            StringAssert.Contains("_tickAccumulator = 0f;", onDestroy);

            string hotSwap = ExtractMethodBody(body, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("case GlobalRegistryServiceSlot.Dispatcher:", hotSwap);
            StringAssert.Contains("TryUnregister();", hotSwap);
            StringAssert.Contains("currentService == null || !TryRegister()", hotSwap);
            StringAssert.Contains("ClearRuntimeStateForLostDispatcherRoute();", hotSwap);
            Assert.Less(hotSwap.IndexOf("TryUnregister();", StringComparison.Ordinal), hotSwap.IndexOf("currentService == null || !TryRegister()", StringComparison.Ordinal));
            Assert.IsFalse(hotSwap.Contains("_registered = false;", StringComparison.Ordinal));
            Assert.IsFalse(hotSwap.Contains("_registeredLateFrame = false;", StringComparison.Ordinal));
            Assert.False(hotSwap.Contains("TryEnqueueSinusoidalCommand"));

            string routeLoss = ExtractMethodBody(body, "private void ClearRuntimeStateForLostDispatcherRoute()");
            StringAssert.Contains("ClearHandTarget();", routeLoss);
            StringAssert.Contains("_pressedLastTick = false;", routeLoss);
            StringAssert.Contains("_pendingPressHaptic = false;", routeLoss);
            StringAssert.Contains("_pendingPressHapticMotorMask = 0;", routeLoss);
            StringAssert.Contains("_tickAccumulator = 0f;", routeLoss);

            string register = ExtractMethodBody(body, "private bool TryRegister()");
            StringAssert.Contains("return _registered && _registeredLateFrame;", register);
            StringAssert.Contains("if (_registered && _registeredLateFrame)", register);
            StringAssert.Contains("TryUnregister();", register);
            StringAssert.Contains("return false;", register);

            string lateFrame = ExtractMethodBody(body, "public void LateFrameTick()");
            StringAssert.Contains("byte motorMask = _pendingPressHapticMotorMask;", lateFrame);
            StringAssert.Contains("_pendingPressHapticMotorMask = 0;", lateFrame);
            StringAssert.Contains("motorMask);", lateFrame);

            string interval = ExtractMethodBody(body, "private float ResolveTickInterval()");
            StringAssert.Contains("SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01)", interval);

            string runTick = ExtractMethodBody(body, "private void RunTerminalTick()");
            StringAssert.Contains("ResolveReachMeters(maxInteractionDistance)", runTick);
            StringAssert.Contains("!IsFinite(canvasPosition) || !IsFinite(worldHitPosition)", runTick);
            StringAssert.Contains("IsFinite(snapCanvasPosition)", runTick);
            StringAssert.Contains("ResolveSurfaceOffsetMeters(handSurfaceOffsetMeters)", runTick);

            string handTarget = ExtractMethodBody(body, "private void DispatchHandIkTarget(");
            StringAssert.Contains("ResolveIkHandSide(handSide)", handTarget);
            StringAssert.Contains("ResolveSnapHoldSeconds(snapHoldSeconds)", handTarget);

            string sanitize = ExtractMethodBody(body, "private static float SanitizeUnit01(");
            StringAssert.Contains("math.isfinite(value) ? value : 0f", sanitize);

            string motor = ExtractMethodBody(body, "private static byte ResolveMotorMask(");
            StringAssert.Contains("private const byte BothMotorMask = 0b0011;", body);
            StringAssert.Contains("return BothMotorMask;", motor);

            string ikSide = ExtractMethodBody(body, "private static PhysicalHandSide ResolveIkHandSide(");
            StringAssert.Contains("return PhysicalHandSide.Right;", ikSide);

            string reach = ExtractMethodBody(body, "private static float ResolveReachMeters(");
            StringAssert.Contains("math.isfinite(meters) ? math.clamp(meters, MinimumReachMeters, MaximumReachMeters) : MinimumReachMeters", reach);

            string offset = ExtractMethodBody(body, "private static float ResolveSurfaceOffsetMeters(");
            StringAssert.Contains("DefaultSurfaceOffsetMeters", offset);

            string snap = ExtractMethodBody(body, "private static float ResolveSnapHoldSeconds(");
            StringAssert.Contains("DefaultSnapDurationSeconds", snap);

            string analog = ExtractMethodBody(body, "private static float2 ResolveFiniteAnalogDelta(");
            StringAssert.Contains("math.all(math.isfinite(value)) ? value : float2.zero", analog);

            string finiteFloat2 = ExtractMethodBody(body, "private static bool IsFinite(float2 value)");
            StringAssert.Contains("math.all(math.isfinite(value))", finiteFloat2);
        }

        [Test]
        public void LifePodStrapHotSwapUnregistersPreviousDispatcherLanes()
        {
            string coordinator = File.ReadAllText(Path.Combine(InteractionRuntimeDir, "LifePodSeatStrapCoordinator.cs"));
            string latch = File.ReadAllText(Path.Combine(InteractionRuntimeDir, "LifePodSeatStrapLatch.cs"));

            string coordinatorHotSwap = ExtractMethodBody(coordinator, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("case GlobalRegistryServiceSlot.Dispatcher:", coordinatorHotSwap);
            StringAssert.Contains("TryUnregisterFixedTick();", coordinatorHotSwap);
            StringAssert.Contains("TryUnregisterLateFrame();", coordinatorHotSwap);
            StringAssert.Contains("bool shouldRestoreFixedTick = _seatLockActive;", coordinatorHotSwap);
            StringAssert.Contains("currentService == null || !isActiveAndEnabled || !TryRegisterFixedTick()", coordinatorHotSwap);
            StringAssert.Contains("ReleaseSeatLockForLostRuntimeRoute();", coordinatorHotSwap);
            StringAssert.Contains("currentService == null || !isActiveAndEnabled || !TryRegisterLateFrame()", coordinatorHotSwap);
            StringAssert.Contains("ClearPendingHaptics();", coordinatorHotSwap);
            Assert.IsFalse(coordinatorHotSwap.Contains("_registeredFixedTick = false;", StringComparison.Ordinal));
            Assert.IsFalse(coordinatorHotSwap.Contains("_registeredLateFrame = false;", StringComparison.Ordinal));
            Assert.Less(
                coordinatorHotSwap.IndexOf("TryUnregisterFixedTick();", StringComparison.Ordinal),
                coordinatorHotSwap.IndexOf("TryRegisterFixedTick();", StringComparison.Ordinal));
            Assert.Less(
                coordinatorHotSwap.IndexOf("TryUnregisterLateFrame();", StringComparison.Ordinal),
                coordinatorHotSwap.IndexOf("TryRegisterLateFrame()", StringComparison.Ordinal));

            StringAssert.Contains("ILateFrameTickable", coordinator);
            string coordinatorEnable = ExtractMethodBody(coordinator, "private void OnEnable()");
            StringAssert.Contains("bool hotSwapReady = TryRegisterHotSwapListener();", coordinatorEnable);
            StringAssert.Contains("if (!_seatLockActive)", coordinatorEnable);
            StringAssert.Contains("!hotSwapReady", coordinatorEnable);
            StringAssert.Contains("!TryCacheSeatLockPose()", coordinatorEnable);
            StringAssert.Contains("!TryEnsurePlayerMotor()", coordinatorEnable);
            StringAssert.Contains("!TryRegisterFixedTick()", coordinatorEnable);
            StringAssert.Contains("ReleaseSeatLockForLostRuntimeRoute();", coordinatorEnable);
            string tryLatch = ExtractMethodBody(coordinator, "public bool TryLatch(");
            StringAssert.Contains("QueueLatchHaptic(handSide, side);", tryLatch);
            Assert.IsFalse(tryLatch.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
            string engageLock = ExtractMethodBody(coordinator, "private void EngageSeatLock()");
            StringAssert.Contains("if (!TryEnsurePlayerMotor())", engageLock);
            StringAssert.Contains("if (!TryRegisterHotSwapListener())", engageLock);
            StringAssert.Contains("if (!TryRegisterFixedTick())", engageLock);
            StringAssert.Contains("ReleaseSeatLockForLostRuntimeRoute();", engageLock);
            StringAssert.Contains("QueueLockHaptic();", engageLock);
            Assert.IsFalse(engageLock.Contains("TryEnqueueSinusoidalCommand", StringComparison.Ordinal));
            AssertMethodCallOrder(
                engageLock,
                "if (!TryEnsurePlayerMotor())",
                "_seatLockActive = true;",
                "QueueLockHaptic();");
            string fixedRegister = ExtractMethodBody(coordinator, "private bool TryRegisterFixedTick()");
            StringAssert.Contains("return true;", fixedRegister);
            StringAssert.Contains("return false;", fixedRegister);
            string coordinatorHotSwapRegister = ExtractMethodBody(coordinator, "private bool TryRegisterHotSwapListener()");
            StringAssert.Contains("return true;", coordinatorHotSwapRegister);
            StringAssert.Contains("return false;", coordinatorHotSwapRegister);
            StringAssert.Contains("return _registeredHotSwapListener;", coordinatorHotSwapRegister);
            string lostRuntimeRoute = ExtractMethodBody(coordinator, "private void ReleaseSeatLockForLostRuntimeRoute()");
            StringAssert.Contains("_seatLockActive = false;", lostRuntimeRoute);
            StringAssert.Contains("ClearPendingLockHaptic();", lostRuntimeRoute);
            StringAssert.Contains("TryUnregisterFixedTick();", lostRuntimeRoute);
            string coordinatorLateFrame = ExtractMethodBody(coordinator, "public void LateFrameTick()");
            StringAssert.Contains("DispatchPendingHaptics();", coordinatorLateFrame);
            string hapticDispatch = ExtractMethodBody(coordinator, "private void DispatchPendingHaptics()");
            StringAssert.Contains("ToolHapticsRuntime.TryEnqueueSinusoidalCommand", hapticDispatch);
            string release = ExtractMethodBody(coordinator, "public void ReleaseSeatLock()");
            StringAssert.Contains("ClearPendingHaptics();", release);
            StringAssert.Contains("TryUnregisterLateFrame();", release);
            string coordinatorDestroy = ExtractMethodBody(coordinator, "private void OnDestroy()");
            StringAssert.Contains("TryUnregisterLateFrame();", coordinatorDestroy);

            string latchHotSwap = ExtractMethodBody(latch, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("bool shouldRestoreTick = (_registeredTick && !_tickDormant) || ShouldRunLatchTick();", latchHotSwap);
            StringAssert.Contains("TryUnregisterTick();", latchHotSwap);
            StringAssert.Contains("_tickDormant = false;", latchHotSwap);
            StringAssert.Contains("currentService != null && isActiveAndEnabled && TryRegisterTick()", latchHotSwap);
            StringAssert.Contains("ClearTransientHoldStateForLostDispatcherRoute();", latchHotSwap);
            Assert.IsFalse(latchHotSwap.Contains("_registeredTick = false;", StringComparison.Ordinal));
            Assert.Less(
                latchHotSwap.IndexOf("bool shouldRestoreTick", StringComparison.Ordinal),
                latchHotSwap.IndexOf("TryUnregisterTick();", StringComparison.Ordinal));
            string queueHold = ExtractMethodBody(latch, "private void QueueHoldSample(");
            StringAssert.Contains("if (!TryRegisterTick())", queueHold);
            StringAssert.Contains("ClearTransientHoldStateForLostDispatcherRoute();", queueHold);
            string latchRegister = ExtractMethodBody(latch, "private bool TryRegisterTick()");
            StringAssert.Contains("!TryRegisterHotSwapListener()", latchRegister);
            StringAssert.Contains("return true;", latchRegister);
            StringAssert.Contains("return false;", latchRegister);
            string latchHotSwapRegister = ExtractMethodBody(latch, "private bool TryRegisterHotSwapListener()");
            StringAssert.Contains("return true;", latchHotSwapRegister);
            StringAssert.Contains("return false;", latchHotSwapRegister);
            StringAssert.Contains("return _registeredHotSwap;", latchHotSwapRegister);
            string clearHold = ExtractMethodBody(latch, "private void ClearTransientHoldStateForLostDispatcherRoute()");
            StringAssert.Contains("_contactThisTick = false;", clearHold);
            StringAssert.Contains("_holdProgressSeconds = 0f;", clearHold);
            StringAssert.Contains("_tickDormant = false;", clearHold);

            string latchDestroy = ExtractMethodBody(latch, "private void OnDestroy()");
            StringAssert.Contains("TryUnregisterTick();", latchDestroy);
            StringAssert.Contains("TryUnregisterHotSwapListener();", latchDestroy);
            StringAssert.Contains("_contactThisTick = false;", latchDestroy);
            StringAssert.Contains("_holdProgressSeconds = 0f;", latchDestroy);
        }

        [Test]
        public void AirlockDoesNotPublishMovingWhenNoMotionStarts()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            StringAssert.Contains("PublishCommittedSealStatus", airlock);
            string queue = ExtractMethodBody(airlock, "private bool QueueSealToggle(");
            int noMotionIndex = queue.IndexOf("if (!_moving)", StringComparison.Ordinal);
            int movingStatusIndex = queue.IndexOf("PublishStatus(DropPodStatusId.AirlockMoving", StringComparison.Ordinal);
            Assert.GreaterOrEqual(noMotionIndex, 0);
            Assert.Greater(movingStatusIndex, noMotionIndex);
            StringAssert.Contains("PublishCommittedSealStatus(sourceFlags);", queue);
        }

        [Test]
        public void AirlockToggleUsesMotionTargetForMidTravelReversal()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string queue = ExtractMethodBody(airlock, "private bool QueueSealToggle(");
            StringAssert.Contains("ResolveNextSealTarget();", queue);
            Assert.IsFalse(queue.Contains("!_sealed || _targetSeal01 < 0.5f", StringComparison.Ordinal));

            string resolver = ExtractMethodBody(airlock, "private bool ResolveNextSealTarget()");
            StringAssert.Contains("if (_moving)", resolver);
            StringAssert.Contains("return _targetSeal01 < 0.5f;", resolver);
            StringAssert.Contains("return !_sealed;", resolver);
        }

        [Test]
        public void AirlockPublishesOpenOrSealedOnlyAfterMotionSettles()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string fixedTick = ExtractMethodBody(airlock, "void IFixedTickable.FixedTick(");
            string queue = ExtractMethodBody(airlock, "private bool QueueSealToggle(");

            StringAssert.Contains("if (!_moving)", fixedTick);
            StringAssert.Contains("_sealed = _seal01 >= 0.995f;", fixedTick);
            StringAssert.Contains("PublishStatus(_sealed ? DropPodStatusId.AirlockSealed : DropPodStatusId.AirlockOpen", fixedTick);
            StringAssert.Contains("if (!targetSealed)", queue);
            StringAssert.Contains("_sealed = false;", queue);
            Assert.IsFalse(fixedTick.Contains("bool sealedNow = _seal01 >= 0.995f;", StringComparison.Ordinal));
        }

        [Test]
        public void InteractiveMotionRequiresDispatcherRegistration()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));

            string airlockQueue = ExtractMethodBody(airlock, "private bool QueueSealToggle(");
            int airlockRegistrationIndex = airlockQueue.IndexOf("if (!TryRegisterTicks())", StringComparison.Ordinal);
            int airlockFailureCleanupIndex = airlockQueue.IndexOf("UnregisterTicks();", StringComparison.Ordinal);
            int airlockMovingStatusIndex = airlockQueue.IndexOf("PublishStatus(DropPodStatusId.AirlockMoving", StringComparison.Ordinal);
            Assert.GreaterOrEqual(airlockRegistrationIndex, 0);
            Assert.Greater(airlockFailureCleanupIndex, airlockRegistrationIndex);
            Assert.Greater(airlockMovingStatusIndex, airlockRegistrationIndex);
            StringAssert.Contains("PublishStatus(DropPodStatusId.FailClosed", airlockQueue);
            StringAssert.Contains("private bool TryRegisterTicks()", airlock);
            string airlockRegister = ExtractMethodBody(airlock, "private bool TryRegisterTicks()");
            StringAssert.Contains("if (_registeredFixed && TryRegisterLate())", airlockRegister);
            StringAssert.Contains("UnregisterTicks();", airlockRegister);
            StringAssert.Contains("return false;", airlockRegister);

            string toggleBody = ExtractMethodBody(toggle, "private bool Toggle(");
            int toggleRegistrationIndex = toggleBody.IndexOf("if (!TryRegisterTicks())", StringComparison.Ordinal);
            int toggleStateIndex = toggleBody.IndexOf("_isOn = !_isOn;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(toggleRegistrationIndex, 0);
            Assert.Greater(toggleStateIndex, toggleRegistrationIndex);
            StringAssert.Contains("private bool TryRegisterTicks()", toggle);

            string seatHotSwap = ExtractMethodBody(seat, "public void OnGlobalRegistryServiceReplaced(");
            StringAssert.Contains("GlobalRegistryServiceSlot.PlayerMotor", seatHotSwap);
            StringAssert.Contains("RefreshSeatLockMotorRegistration();", seatHotSwap);
            string seatRegister = ExtractMethodBody(seat, "private bool TryRegisterTicks()");
            StringAssert.Contains("if (fixedReady && TryRegisterLate())", seatRegister);
            StringAssert.Contains("UnregisterTicks();", seatRegister);
            StringAssert.Contains("return false;", seatRegister);
        }

        [Test]
        public void SeatBlockedAirlockWarningIsSpecificAndFailClosedIsGeneric()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string label = ExtractMethodBody(body, "private ReadOnlySpan<char> ResolveStatusLabel()");
            StringAssert.Contains("private const string HatchOpenLabel = \"HATCH OPEN\";", body);
            StringAssert.Contains("private const string FailLabel = \"FAULT\";", body);
            StringAssert.Contains("case DropPodStatusId.SeatBlockedAirlockOpen:", label);
            StringAssert.Contains("return HatchOpenLabel.AsSpan();", label);
            StringAssert.Contains("case DropPodStatusId.FailClosed:", label);
            StringAssert.Contains("return FailLabel.AsSpan();", label);
        }

        [Test]
        public void DashboardMapsIgnitionStatusToDiegeticLabel()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string label = ExtractMethodBody(body, "private ReadOnlySpan<char> ResolveStatusLabel()");
            StringAssert.Contains("private const string IgnitionLabel = \"IGNITION\";", body);
            StringAssert.Contains("case DropPodStatusId.EngineIgnitionArmed:", label);
            StringAssert.Contains("return IgnitionLabel.AsSpan();", label);
        }

        [Test]
        public void DashboardMovingStateUsesNeutralMotionLabel()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string label = ExtractMethodBody(body, "private ReadOnlySpan<char> ResolveStatusLabel()");
            StringAssert.Contains("private const string MovingLabel = \"MOVING\";", body);
            StringAssert.Contains("case DropPodStatusId.AirlockMoving:", label);
            StringAssert.Contains("return MovingLabel.AsSpan();", label);
            Assert.IsFalse(body.Contains("private const string MovingLabel = \"LOCK\";", StringComparison.Ordinal));
        }

        [Test]
        public void DashboardMapsArmedSeatStatusToDiegeticLabel()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardTextRenderer.cs"));
            string label = ExtractMethodBody(body, "private ReadOnlySpan<char> ResolveStatusLabel()");
            StringAssert.Contains("private const string ArmedLabel = \"ARMED\";", body);
            StringAssert.Contains("case DropPodStatusId.SeatTransitArmed:", label);
            StringAssert.Contains("return ArmedLabel.AsSpan();", label);
        }

        [Test]
        public void EmergencyLightingSkipsStableGpuWrites()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));
            StringAssert.Contains("LightingApplyEpsilon", body);
            StringAssert.Contains("_lightingDirty", body);
            StringAssert.Contains("_lastAppliedEmergency01", body);
            StringAssert.Contains("private const float TransitAlertLightWeight = 0.45f;", body);
            StringAssert.Contains("private const float ArmedAlertLightWeight = 0.7f;", body);
            StringAssert.Contains("private const float FullAlertLightWeight = 1f;", body);
            StringAssert.Contains("case DropPodStatusId.SeatBlockedAirlockOpen:", body);
            StringAssert.Contains("case DropPodStatusId.FailClosed:", body);
            StringAssert.Contains("SetTargetEmergency01(TransitAlertLightWeight);", body);
            StringAssert.Contains("SetTargetEmergency01(ArmedAlertLightWeight);", body);
            StringAssert.Contains("SetTargetEmergency01(FullAlertLightWeight);", body);

            string lateFrame = ExtractMethodBody(body, "public void LateFrameTick()");
            StringAssert.Contains("settleDelta", lateFrame);
            StringAssert.Contains("math.isfinite(transitionSharpness)", lateFrame);
            StringAssert.Contains("ApplyLightingIfNeeded(_emergency01, false);", lateFrame);
            Assert.IsFalse(lateFrame.Contains("ApplyLighting(_emergency01)", StringComparison.Ordinal));

            string gate = ExtractMethodBody(body, "private void ApplyLightingIfNeeded(");
            StringAssert.Contains("return;", gate);
            StringAssert.Contains("_lastAppliedEmergency01", gate);
            StringAssert.Contains("_lightingDirty = false;", gate);

            string applyLighting = ExtractMethodBody(body, "private void ApplyLighting(");
            StringAssert.Contains("ResolveFiniteIntensity(cabinBaseIntensity)", applyLighting);
            StringAssert.Contains("ResolveFiniteIntensity(emergencyBaseIntensity)", applyLighting);
            StringAssert.Contains("ResolveFiniteColor(emergencyColor)", applyLighting);

            string intensity = ExtractMethodBody(body, "private static float ResolveFiniteIntensity(");
            StringAssert.Contains("DropPodSplineMath.SanitizeRange(value, 0f, MaxLightIntensity, 0f)", intensity);
            StringAssert.Contains("private const float MaxLightIntensity = 8f;", body);

            string color = ExtractMethodBody(body, "private static Color ResolveFiniteColor(");
            StringAssert.Contains("return Color.black;", color);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(value.r)", color);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(value.g)", color);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(value.b)", color);
            StringAssert.Contains("DropPodSplineMath.SanitizeUnit01(value.a)", color);
        }

        [Test]
        public void EmergencyLightingClearsExternalPresentationWritesOnDisable()
        {
            string body = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodEmergencyLightingController.cs"));
            StringAssert.Contains("ClearPresentationLighting();", ExtractMethodBody(body, "private void OnDisable()"));
            StringAssert.Contains("ClearPresentationLighting();", ExtractMethodBody(body, "private void OnDestroy()"));
            string clear = ExtractMethodBody(body, "private void ClearPresentationLighting()");
            StringAssert.Contains("ApplyLighting(0f);", clear);
            StringAssert.Contains("_lastAppliedEmergency01 = 0f;", clear);
            StringAssert.Contains("_lightingDirty = true;", clear);
        }

        [Test]
        public void DisablePathsDoNotLeaveMidMotionPresentationState()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));

            StringAssert.Contains("SnapToCommittedSealState();", ExtractMethodBody(airlock, "private void OnDisable()"));
            StringAssert.Contains("_seal01 = _sealed ? 1f : 0f;", ExtractMethodBody(airlock, "private void SnapToCommittedSealState()"));
            StringAssert.Contains("ApplyHatchRotation(_seal01);", ExtractMethodBody(airlock, "private void SnapToCommittedSealState()"));

            StringAssert.Contains("SnapVisualToCommittedState();", ExtractMethodBody(toggle, "private void OnDisable()"));
            StringAssert.Contains("_position01 = _isOn ? 1f : 0f;", ExtractMethodBody(toggle, "private void SnapVisualToCommittedState()"));
            StringAssert.Contains("ApplyVisual(_pendingVisual01);", ExtractMethodBody(toggle, "private void SnapVisualToCommittedState()"));

            StringAssert.Contains("AbortTransitLocal();", ExtractMethodBody(seat, "private void OnDisable()"));
            string abortWrapper = ExtractMethodBody(seat, "private void AbortTransitLocal()");
            StringAssert.Contains("AbortTransitLocal(true);", abortWrapper);
            string abort = ExtractMethodBody(seat, "private void AbortTransitLocal(bool restoreCameraPose)");
            StringAssert.Contains("if (restoreCameraPose)", abort);
            StringAssert.Contains("ApplyCameraPose(_startPosition, _startRotation);", abort);
            StringAssert.Contains("_transiting = false;", abort);
            Assert.IsFalse(abort.Contains("QueueFeedback(", StringComparison.Ordinal));
            Assert.IsFalse(abort.Contains("PublishStatus(", StringComparison.Ordinal));
        }

        [Test]
        public void DestroyPathsMirrorDisableMotionCleanup()
        {
            string airlock = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodAirlockController.cs"));
            string toggle = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodDashboardToggleSwitch.cs"));
            string seat = File.ReadAllText(Path.Combine(DropPodRuntimeDir, "DropPodSeatController.cs"));

            string airlockDestroy = ExtractMethodBody(airlock, "private void OnDestroy()");
            StringAssert.Contains("ClearHandTarget();", airlockDestroy);
            StringAssert.Contains("_feedbackPending = false;", airlockDestroy);
            StringAssert.Contains("_moving = false;", airlockDestroy);
            StringAssert.Contains("SnapToCommittedSealState();", airlockDestroy);

            string toggleDestroy = ExtractMethodBody(toggle, "private void OnDestroy()");
            StringAssert.Contains("_feedbackPending = false;", toggleDestroy);
            StringAssert.Contains("_moving = false;", toggleDestroy);
            StringAssert.Contains("SnapVisualToCommittedState();", toggleDestroy);

            string seatDestroy = ExtractMethodBody(seat, "private void OnDestroy()");
            StringAssert.Contains("AbortTransitLocal();", seatDestroy);
            StringAssert.Contains("RestoreInputBlock();", seatDestroy);
            StringAssert.Contains("_feedbackPending = false;", seatDestroy);
            StringAssert.Contains("_pendingFeedbackEventId = 0u;", seatDestroy);
            StringAssert.Contains("_pendingFeedbackMotorMask = 0;", seatDestroy);
        }

        private static string ReadDropPodRuntime()
        {
            string[] files = Directory.GetFiles(DropPodRuntimeDir, "*.cs", SearchOption.AllDirectories);
            string body = string.Empty;
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    files[i].IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                body += File.ReadAllText(files[i]);
            }

            return body;
        }

        private static void AssertCleanHotPathBodies(string body, string signature)
        {
            int index = body.IndexOf(signature, StringComparison.Ordinal);
            while (index >= 0)
            {
                string method = ExtractMethodBody(body, signature, index);
                Assert.IsFalse(method.Contains("GetComponent(", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("TryGetComponent(", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GlobalRegistry.Get<", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GlobalRegistry.Input", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GlobalRegistry.Player", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GlobalRegistry.Audio", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GetComponents", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("FindObject", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("GameObject." + "Find", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("Camera.main", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("Start" + "Coroutine", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("foreach", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains(".ToString(", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("string.Format", StringComparison.Ordinal), signature);
                Assert.IsFalse(method.Contains("$\"", StringComparison.Ordinal), signature);
                index = body.IndexOf(signature, index + signature.Length, StringComparison.Ordinal);
            }
        }

        private static void AssertStatusCursorResetsOnEnable(string body)
        {
            StringAssert.Contains("ResetStatusCursor();", ExtractMethodBody(body, "private void OnEnable()"));
            string reset = ExtractMethodBody(body, "private void ResetStatusCursor()");
            StringAssert.Contains("_lastStatusFrame = 0u;", reset);
            StringAssert.Contains("_lastStatusSequence = 0;", reset);
        }

        private static void AssertMethodCallOrder(string methodBody, string first, string second, string third)
        {
            int firstIndex = methodBody.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = methodBody.IndexOf(second, StringComparison.Ordinal);
            int thirdIndex = methodBody.IndexOf(third, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, first);
            Assert.Greater(secondIndex, firstIndex, second);
            Assert.Greater(thirdIndex, secondIndex, third);
        }

        private static string ExtractMethodBody(string body, string signature)
        {
            return ExtractMethodBody(body, signature, 0);
        }

        private static string ExtractMethodBody(string body, string signature, int searchStart)
        {
            int signatureIndex = body.IndexOf(signature, searchStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);
            int openBrace = body.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, signature);
            int depth = 0;
            for (int i = openBrace; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return body.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail(signature + " body was not balanced.");
            return string.Empty;
        }
    }
}
