#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Power.Editor
{
    public static class Charger_OOP_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json";

        private struct ScanCounts
        {
            public bool IsChargerFile;
            public int UpdateLoops;
            public int CoroutineLoops;
            public int ManagedBatteryLists;
            public int ManagedBatteryArrays;
            public int SlowTickRegistrations;
            public int ManagedChargingShadowState;
            public int LegacyGridDirty;
            public int LegacySlotFacades;
        }

        [MenuItem("Hecton/Power/Run Charger OOP Scanner")]
        public static void RunMenu()
        {
            string reportPath = RunScan();
            Debug.Log("Charger OOP scanner wrote " + reportPath);
        }

        public static string RunScan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project/Scripts");
            string reportPath = Path.GetFullPath(Path.Combine(projectRoot, ReportRelativePath));
            string binaryLedgerPath = Path.Combine(projectRoot, "Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md");
            string runtimeAsmdefPath = Path.Combine(Application.dataPath, "_Project/Scripts/Power/BatteryChargerLogistics/Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef");
            string editorAsmdefPath = Path.Combine(Application.dataPath, "_Project/Scripts/Power/Editor/Hecton8.Power.BatteryChargerLogistics.Editor.asmdef");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            int chargerFiles = 0;
            int updateLoops = 0;
            int coroutineLoops = 0;
            int managedBatteryLists = 0;
            int managedBatteryArrays = 0;
            int slowTickRegistrations = 0;
            int managedChargingShadowState = 0;
            int legacyGridDirty = 0;
            int legacySlotFacades = 0;
            bool scheduleLocksBeforeBufferResolve = false;
            bool cadenceQualitySampleUsesTuningLock = false;
            bool jobBufferLockIncludesTuning = false;
            bool coldSlotWriteLocksBeforeResolve = false;
            bool coldSlotWriteUsesGenerationHandleFence = false;
            bool chargeReadRejectsReservedLock = false;
            bool facadeRejectsUnassignedInventorySlotZero = false;
            bool facadeWritesSlotBeforeLinkRegistration = false;
            bool facadeUsesCoreFloatingOriginAup = false;
            bool facadeUsesCurrentOriginAupProof = false;
            bool facadeRejectsDirectFloatingOriginBridge = false;
            bool facadeAupFiniteGuarded = false;
            int facadeWorldImportHits = 0;
            int facadeWorldRouteHits = 0;
            int facadeGlobalOriginAupHits = 0;
            int facadeOffsetAbsoluteAupHits = 0;
            int facadeDirectFloatingOriginBridgeHits = 0;
            int facadeFromRuntimePositionHits = 0;
            bool humAupWritesContractFields = false;
            bool humAupRejectsOutOfExtent = false;
            int runtimeWorldImportHits = 0;
            int runtimeWorldRouteHits = 0;
            bool interactTextUsesCachedToolOnly = false;
            bool playerInventoryBridgeRemovesBeforeChargerCommit = false;
            bool playerInventoryBridgeReservesBeforeChargerCommit = false;
            bool playerInventoryBridgeCommitsReservationAfterChargerCommit = false;
            bool playerInventoryBridgeReleasesReservationOnFailure = false;
            bool playerInventoryBridgePreflightsAuthoredSlotRange = false;
            bool playerInventoryRollbackResultChecked = false;
            bool playerInventoryBridgeHardReservationProof = false;
            bool toolSwapRollsBackOnInsertFailure = false;
            bool toolSwapRollbackResultsChecked = false;
            bool toolSwapPreflightsBeforeToolRemoval = false;
            bool removeToInventoryPreflightsCapacity = false;
            bool facadeColdInitializesSlotObjects = false;
            int facadeConcreteInventoryToolImportHits = 0;
            int constructionModuleConcreteGameplayToolImportHits = 0;
            bool concreteFacadeBridgeContractResidual = false;
            bool inventoryRoutingSharesShinobuSlots = false;
            bool inventoryRoutingWholeSlotMaintenanceWriters = false;
            bool runtimeUsesMockInventorySlotsOwnedByPower = false;
            bool runtimeSharedInventoryAllocationHits = false;
            bool emergencyMockEditorOrDevelopmentOnly = false;
            bool liveRegistrationDropsMockFallback = false;
            bool visualBuffersPrewarmedBeforeVisualSync = false;
            bool skippedCadenceTelemetryRecorded = false;
            bool skippedCadenceTelemetryCoalesced = false;
            bool xrayDisplaysSkippedCadenceFrames = false;
            bool nanFaultProducerPresent = false;
            bool rawPointerSafetyJustificationPresent = false;
            bool csvParserRejectsMalformedRows = false;
            bool binaryPayloadLedgerRangeRegistered = false;
            bool binaryPayloadLedgerBoundaryRegistered = false;
            bool runtimeAsmdefPresent = false;
            bool runtimeAsmdefNoSiblingRuntimeRefs = false;
            bool editorAsmdefPresent = false;
            bool facadeUsesBridgeNoRuntimeCall = false;
            bool runtimeRegistersBridge = false;
            bool runtimeRegistersGlobalRegistryService = false;
            bool registryResetClearsBridgeForDomainReload = false;
            bool bridgeDirectClearEradicated = false;
            bool bridgeDelegateTableEradicated = false;
            bool bridgeUsesCachedRegistryService = false;
            bool globalRegistryBatteryServiceRoute = false;
            bool lockedSimulationTickDeltaUsed = false;
            bool frameDeltaBypassedForChargeAuthority = false;
            bool cadenceCapPreservesAccumulatorRemainder = false;
            bool authorityAccumulatorSubtractedAfterAdmission = false;
            bool editorTuningWritesResolvedQualityCadence = false;
            bool runtimeFiniteDtoWriteGuards = false;
            bool telemetryUsesFenceElapsedMicroseconds = false;
            bool telemetryContractsUseFenceElapsedMicroseconds = false;
            bool faultDumpBlockingFaultOnlyDocumented = false;
            int facadeDirectRuntimeCallHits = 0;

            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            StringBuilder findings = new StringBuilder(1024);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string fileName = Path.GetFileName(path);
                string text = File.ReadAllText(path);
                string scanText = StripNonCode(text);
                if (fileName == "BatteryChargerLogisticsRuntime.cs")
                {
                    int lockIndex = scanText.IndexOf("TryLockJobBuffers(vault)", StringComparison.Ordinal);
                    int resolveIndex = scanText.IndexOf("TryResolveSimulationBuffers(", StringComparison.Ordinal);
                    scheduleLocksBeforeBufferResolve = lockIndex >= 0 && resolveIndex > lockIndex;
                    cadenceQualitySampleUsesTuningLock =
                        scanText.IndexOf("SampleQualityWeightUnderTuningLock(vault, out float q)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("TryLockBuffer(BatteryChargerLogisticsBufferIds.Tuning, SystemID.Power)", StringComparison.Ordinal) >= 0;
                    jobBufferLockIncludesTuning =
                        scanText.IndexOf("TryLock(vault, BatteryChargerLogisticsBufferIds.Tuning, 1 << 7)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("TryUnlockBuffer(BatteryChargerLogisticsBufferIds.Tuning, SystemID.Power)", StringComparison.Ordinal) >= 0;
                    int coldAcquireCallIndex = scanText.IndexOf("TryAcquireInventorySlotsWrite(vault, out VaultGenerationHandle<InventorySlotDTO> inventoryHandle, out NativeArray<InventorySlotDTO> slots)", StringComparison.Ordinal);
                    int coldAcquireMethodIndex = scanText.IndexOf("private static bool TryAcquireInventorySlotsWrite(", StringComparison.Ordinal);
                    int coldDescriptorBorrowIndex = coldAcquireMethodIndex >= 0
                        ? scanText.IndexOf("TryBorrowInventorySlotHandle(vault, out handle)", coldAcquireMethodIndex, StringComparison.Ordinal)
                        : -1;
                    int coldWriteLockIndex = coldAcquireMethodIndex >= 0
                        ? scanText.IndexOf("vault.TryAcquireWriteLock(in handle, SystemID.Power, out slots)", coldAcquireMethodIndex, StringComparison.Ordinal)
                        : -1;
                    int coldSlotWriteIndex = coldAcquireCallIndex >= 0
                        ? scanText.IndexOf("slotPtr->ItemHashID = itemHash", coldAcquireCallIndex, StringComparison.Ordinal)
                        : -1;
                    coldSlotWriteUsesGenerationHandleFence =
                        coldAcquireCallIndex >= 0 &&
                        coldAcquireMethodIndex >= 0 &&
                        coldDescriptorBorrowIndex > coldAcquireMethodIndex &&
                        coldWriteLockIndex > coldDescriptorBorrowIndex &&
                        coldSlotWriteIndex > coldAcquireCallIndex;
                    coldSlotWriteLocksBeforeResolve = coldSlotWriteUsesGenerationHandleFence;
                    chargeReadRejectsReservedLock = scanText.IndexOf("slot.ItemHashID == 0u || slot.Quantity == 0u || slot.ReservedLock != 0u", StringComparison.Ordinal) >= 0;
                    runtimeWorldImportHits = CountToken(scanText, "using Hecton8.World");
                    runtimeWorldRouteHits = CountToken(scanText, "Hecton8.World.");
                    humAupWritesContractFields =
                        scanText.IndexOf("TryWriteAbsoluteAupFields(ref signal, aup)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("HectonPhysicsContract.AupSectorSizeMetersDouble", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("signal.PositionAup.GridX = gridX", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("AbsoluteUniversePosition.FromAbsolutePosition", StringComparison.Ordinal) < 0;
                    humAupRejectsOutOfExtent =
                        scanText.IndexOf("AcousticHumMaxAupExtentMeters", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.abs(absolutePosition.x) > AcousticHumMaxAupExtentMeters", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("return false;", StringComparison.Ordinal) >= 0;
                    runtimeUsesMockInventorySlotsOwnedByPower =
                        scanText.IndexOf("_usingMockInventorySlots", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BatteryChargerLogisticsBufferIds.MockInventorySlots", StringComparison.Ordinal) >= 0;
                    runtimeSharedInventoryAllocationHits =
                        CountToken(scanText, "GetBufferHandle<InventorySlotDTO>") > 0 ||
                        CountToken(scanText, "GetGenerationHandle<InventorySlotDTO>") > 0;
                    int mockGateIndex = scanText.IndexOf("private static bool AllowEmergencyMockNetwork()", StringComparison.Ordinal);
                    int mockGateEditorIndex = mockGateIndex >= 0 ? scanText.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", mockGateIndex, StringComparison.Ordinal) : -1;
                    int mockGateElseIndex = mockGateEditorIndex >= 0 ? scanText.IndexOf("#else", mockGateEditorIndex, StringComparison.Ordinal) : -1;
                    int mockGateFalseIndex = mockGateElseIndex >= 0 ? scanText.IndexOf("return false;", mockGateElseIndex, StringComparison.Ordinal) : -1;
                    emergencyMockEditorOrDevelopmentOnly = mockGateIndex >= 0 && mockGateFalseIndex > mockGateElseIndex;
                    liveRegistrationDropsMockFallback =
                        scanText.IndexOf("runtime.DropMockNetworkForLiveRegistration();", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("private void DropMockNetworkForLiveRegistration()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("_usingMockInventorySlots = false;", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("_activeCount = 0;", StringComparison.Ordinal) >= 0;
                    runtimeRegistersGlobalRegistryService =
                        scanText.IndexOf("GlobalRegistry.RegisterBatteryChargerLogisticsRuntime(this)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("GlobalRegistry.UnregisterBatteryChargerLogisticsRuntime(this)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BatteryChargerLogisticsBridge.Register(", StringComparison.Ordinal) < 0;
                    registryResetClearsBridgeForDomainReload =
                        scanText.IndexOf("GlobalRegistry.ResetBatteryChargerLogisticsRuntimeForDomainReload()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("GlobalRegistry.UnregisterBatteryChargerLogisticsRuntime(s_active)", StringComparison.Ordinal) < 0;
                    bridgeDirectClearEradicated =
                        scanText.IndexOf("BatteryChargerLogisticsBridge.Clear();", StringComparison.Ordinal) < 0;
                    runtimeRegistersBridge =
                        runtimeRegistersGlobalRegistryService &&
                        registryResetClearsBridgeForDomainReload &&
                        bridgeDirectClearEradicated;
                    lockedSimulationTickDeltaUsed =
                        scanText.IndexOf("private const float SimulationTickDeltaSeconds = 1f / 60f", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("float dt = ResolveSimulationTickDelta(in timing)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.clamp(fixedDelta, 1f / 240f, 1f / 5f)", StringComparison.Ordinal) >= 0;
                    frameDeltaBypassedForChargeAuthority =
                        scanText.IndexOf("timing.FrameDelta", StringComparison.Ordinal) < 0;
                    int integrationDtIndex = scanText.IndexOf("float integrationDt = math.min(_authorityAccumulator, 1f)", StringComparison.Ordinal);
                    int linkAdmissionIndex = integrationDtIndex >= 0
                        ? scanText.IndexOf("if (linkCount <= 0)", integrationDtIndex, StringComparison.Ordinal)
                        : -1;
                    int authoritySubtractIndex = integrationDtIndex >= 0
                        ? scanText.IndexOf("_authorityAccumulator = math.max(0f, _authorityAccumulator - integrationDt)", integrationDtIndex, StringComparison.Ordinal)
                        : -1;
                    cadenceCapPreservesAccumulatorRemainder =
                        integrationDtIndex >= 0 &&
                        authoritySubtractIndex > integrationDtIndex;
                    authorityAccumulatorSubtractedAfterAdmission =
                        integrationDtIndex >= 0 &&
                        linkAdmissionIndex > integrationDtIndex &&
                        authoritySubtractIndex > linkAdmissionIndex;
                    editorTuningWritesResolvedQualityCadence =
                        scanText.IndexOf("ApplyPendingTuningValues(ref dto)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("private static void ApplyPendingTuningValues(ref ChargerTuningDTO dto)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("dto.GlobalQualityWeight = ResolvePendingQualityWeight()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("dto.CadenceHz = ResolveCadenceHzStatic(dto.GlobalQualityWeight)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("dto.GlobalQualityWeight = ResolveQualityWeight()", StringComparison.Ordinal) < 0;
                    runtimeFiniteDtoWriteGuards =
                        scanText.IndexOf("if (!math.all(math.isfinite(chargerAup)))", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("link.ChargeRate = SanitizeNonNegative(chargeRate)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("link.EfficiencyScalar = SanitizeNonNegative(efficiencyScalar)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("s_pendingMaxChargeRate = SanitizeNonNegative(maxChargeRate)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("s_pendingQualityOverride = SanitizeQualityOverride(qualityOverride)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("private static float SanitizeNonNegative(float value)", StringComparison.Ordinal) >= 0;
                    telemetryUsesFenceElapsedMicroseconds =
                        scanText.IndexOf("_lastFenceElapsedMicroseconds", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("FaultDumpFenceElapsedThresholdMicroseconds", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("entry.FenceElapsedMicroseconds", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("_lastScheduleMicroseconds", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("BurstMicroseconds", StringComparison.Ordinal) < 0;
                    int preSimIndex = scanText.IndexOf("private void PreSimulationTick", StringComparison.Ordinal);
                    int visualSyncIndex = scanText.IndexOf("private void VisualSyncTick", StringComparison.Ordinal);
                    int prewarmIndex = scanText.IndexOf("_ = EnsureGraphicsBuffers();", StringComparison.Ordinal);
                    visualBuffersPrewarmedBeforeVisualSync =
                        preSimIndex >= 0 &&
                        prewarmIndex > preSimIndex &&
                        (visualSyncIndex < 0 || prewarmIndex < visualSyncIndex);
                    skippedCadenceTelemetryRecorded =
                        scanText.IndexOf("RecordSkippedCadenceFrame(dt)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("TelemetryFlagSkippedCadence", StringComparison.Ordinal) >= 0;
                    int recordSkipIndex = scanText.IndexOf("private void RecordSkippedCadenceFrame(float deltaSeconds)", StringComparison.Ordinal);
                    int writeTelemetryFrameIndex = scanText.IndexOf("private void WriteTelemetryFrame", StringComparison.Ordinal);
                    int immediateSkipWriteIndex = recordSkipIndex >= 0 && writeTelemetryFrameIndex > recordSkipIndex
                        ? scanText.IndexOf("WriteTelemetryEntry", recordSkipIndex, writeTelemetryFrameIndex - recordSkipIndex, StringComparison.Ordinal)
                        : -1;
                    int skippedCountIndex = scanText.IndexOf("_skippedCadenceFrames = math.min(_skippedCadenceFrames + 1, ushort.MaxValue)", StringComparison.Ordinal);
                    int skippedFlagIndex = scanText.IndexOf("entry.Flags |= BatteryChargerLogisticsConstants.TelemetryFlagSkippedCadence", StringComparison.Ordinal);
                    int skippedTailIndex = scanText.IndexOf("entry.SkippedCadenceFrames = (uint)math.max(0, skippedCadenceFrames)", StringComparison.Ordinal);
                    int skippedWriteIndex = skippedTailIndex >= 0
                        ? scanText.IndexOf("WriteTelemetryEntry(telemetry, cursor, in entry);", skippedTailIndex, StringComparison.Ordinal)
                        : -1;
                    int skippedResetIndex = skippedWriteIndex >= 0
                        ? scanText.IndexOf("_skippedCadenceFrames = 0", skippedWriteIndex, StringComparison.Ordinal)
                        : -1;
                    skippedCadenceTelemetryCoalesced =
                        skippedCountIndex > recordSkipIndex &&
                        skippedFlagIndex > writeTelemetryFrameIndex &&
                        skippedTailIndex > skippedFlagIndex &&
                        skippedWriteIndex > skippedTailIndex &&
                        skippedResetIndex > skippedWriteIndex &&
                        immediateSkipWriteIndex < 0 &&
                        scanText.IndexOf("entry.Flags = BatteryChargerLogisticsConstants.TelemetryFlagSkippedCadence", StringComparison.Ordinal) < 0;
                }

                if (fileName == "BatteryCharger.cs")
                {
                    int facadeSlotWriteIndex = scanText.IndexOf("WriteInventorySlotState(i, slot.batteryItem, slot.currentCharge)", StringComparison.Ordinal);
                    int facadeLinkRegisterIndex = scanText.IndexOf("TryRegisterChargerLink(", StringComparison.Ordinal);
                    facadeDirectRuntimeCallHits = CountToken(scanText, "BatteryChargerLogisticsRuntime.");
                    facadeUsesBridgeNoRuntimeCall =
                        facadeDirectRuntimeCallHits == 0 &&
                        CountToken(scanText, "BatteryChargerLogisticsBridge.") >= 4;
                    facadeRejectsUnassignedInventorySlotZero =
                        scanText.IndexOf("private const uint InvalidInventorySlotStartIndex = 0u", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("inventorySlotStartIndex != InvalidInventorySlotStartIndex", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("!HasAuthoredInventorySlotRange()", StringComparison.Ordinal) >= 0;
                    facadeWritesSlotBeforeLinkRegistration =
                        scanText.IndexOf("private bool WriteInventorySlotState", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("return BatteryChargerLogisticsBridge.TryWriteInventorySlotState", StringComparison.Ordinal) >= 0 &&
                        facadeSlotWriteIndex >= 0 &&
                        facadeLinkRegisterIndex > facadeSlotWriteIndex;
                    facadeWorldImportHits = CountToken(scanText, "using Hecton8.World");
                    facadeWorldRouteHits = CountToken(scanText, "Hecton8.World.");
                    facadeGlobalOriginAupHits = CountToken(scanText, "GlobalSignals.CurrentRuntimeOriginAup()");
                    facadeOffsetAbsoluteAupHits = CountToken(scanText, "OffsetAbsoluteMeters");
                    facadeDirectFloatingOriginBridgeHits = CountToken(scanText, "HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(");
                    facadeFromRuntimePositionHits = CountToken(scanText, "AbsoluteUniversePosition.FromRuntimePosition");
                    facadeConcreteInventoryToolImportHits =
                        CountToken(scanText, "using Hecton8.Inventory") +
                        CountToken(scanText, "using Hecton8.Tools");
                    facadeUsesCoreFloatingOriginAup =
                        facadeWorldImportHits == 0 &&
                        facadeWorldRouteHits == 0 &&
                        facadeGlobalOriginAupHits == 0 &&
                        facadeOffsetAbsoluteAupHits == 0 &&
                        scanText.IndexOf("HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("GlobalSignals.CurrentRuntimeOriginAup()", StringComparison.Ordinal) < 0;
                    facadeRejectsDirectFloatingOriginBridge =
                        facadeDirectFloatingOriginBridgeHits == 0 &&
                        facadeFromRuntimePositionHits == 0;
                    facadeAupFiniteGuarded =
                        scanText.IndexOf("math.isfinite(position.x)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.isfinite(position.y)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.isfinite(position.z)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("originAup.IsFinite()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.all(math.isfinite(chargerAup))", StringComparison.Ordinal) >= 0;
                    facadeUsesCurrentOriginAupProof =
                        facadeWorldImportHits == 0 &&
                        facadeWorldRouteHits == 2 &&
                        facadeGlobalOriginAupHits == 1 &&
                        facadeOffsetAbsoluteAupHits == 1 &&
                        facadeRejectsDirectFloatingOriginBridge &&
                        facadeAupFiniteGuarded;
                    interactTextUsesCachedToolOnly =
                        scanText.IndexOf("string IInteractable.GetInteractText()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("PlayerToolManager toolManager = _cachedToolManager", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("PlayerToolManager toolManager = BindToolManagerForInteraction();", StringComparison.Ordinal) < 0;
                    playerInventoryBridgeRemovesBeforeChargerCommit =
                        scanText.IndexOf("playerInventory.RemoveOneItem(x, y)", StringComparison.Ordinal) >= 0;
                    int reservationIndex = scanText.IndexOf("TryReserveQuantityForCraft(itemHashId, 1, _inventoryReservationScratch, ref reservationCount)", StringComparison.Ordinal);
                    int chargerInsertIndex = scanText.IndexOf("if (!InsertBattery(emptySlot, item, 0f))", StringComparison.Ordinal);
                    int reservationReleaseIndex = scanText.IndexOf("playerInventory.ReleaseCraftReservations(_inventoryReservationScratch, reservationCount)", StringComparison.Ordinal);
                    int reservationCommitIndex = scanText.IndexOf("playerInventory.CommitCraftReservations(_inventoryReservationScratch, reservationCount)", StringComparison.Ordinal);
                    playerInventoryBridgeReservesBeforeChargerCommit =
                        reservationIndex >= 0 &&
                        chargerInsertIndex > reservationIndex;
                    playerInventoryBridgeCommitsReservationAfterChargerCommit =
                        reservationCommitIndex > chargerInsertIndex;
                    playerInventoryBridgeReleasesReservationOnFailure =
                        reservationReleaseIndex > chargerInsertIndex &&
                        reservationReleaseIndex < reservationCommitIndex;
                    playerInventoryBridgePreflightsAuthoredSlotRange =
                        scanText.IndexOf("playerInventory == null || playerInventory.Grid == null || !HasAuthoredInventorySlotRange()", StringComparison.Ordinal) >= 0 &&
                        reservationIndex >
                        scanText.IndexOf("!HasAuthoredInventorySlotRange()", StringComparison.Ordinal);
                    playerInventoryRollbackResultChecked =
                        scanText.IndexOf("if (RemoveBattery(emptySlot) == null)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("ReportBridgeRollbackFailure()", StringComparison.Ordinal) >= 0;
                    playerInventoryBridgeHardReservationProof =
                        !playerInventoryBridgeRemovesBeforeChargerCommit &&
                        playerInventoryBridgeReservesBeforeChargerCommit &&
                        playerInventoryBridgeCommitsReservationAfterChargerCommit &&
                        playerInventoryBridgeReleasesReservationOnFailure &&
                        playerInventoryRollbackResultChecked;
                    toolSwapRollsBackOnInsertFailure =
                        scanText.IndexOf("batteryTool.InsertBattery(toolBattery, toolBatteryCharge)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("InsertBattery(chargedSlot, chargedBattery, previousCharge)", StringComparison.Ordinal) >= 0;
                    toolSwapRollbackResultsChecked =
                        scanText.IndexOf("if (!batteryTool.InsertBattery(toolBattery, toolBatteryCharge))", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("if (!InsertBattery(chargedSlot, chargedBattery, previousCharge))", StringComparison.Ordinal) >= 0;
                    int toolPreflightIndex = scanText.IndexOf("emptySlot < 0 || !HasAuthoredInventorySlotRange()", StringComparison.Ordinal);
                    int toolRemoveIndex = scanText.IndexOf("ItemData toolBattery = batteryTool.RemoveBattery()", StringComparison.Ordinal);
                    toolSwapPreflightsBeforeToolRemoval = toolPreflightIndex >= 0 && toolRemoveIndex > toolPreflightIndex;
                    int preflightIndex = scanText.IndexOf("CanAcceptItemQuantity(candidateHash, 1)", StringComparison.Ordinal);
                    int removeBatteryIndex = scanText.IndexOf("ItemData battery = RemoveBattery(slotIndex)", StringComparison.Ordinal);
                    removeToInventoryPreflightsCapacity = preflightIndex >= 0 && removeBatteryIndex > preflightIndex;
                    facadeColdInitializesSlotObjects =
                        scanText.IndexOf("private void EnsureSlotObjects()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("slots[i] = new BatterySlot()", StringComparison.Ordinal) >= 0;
                    concreteFacadeBridgeContractResidual = facadeConcreteInventoryToolImportHits > 0;
                }

                if (fileName == "BatteryChargerLogisticsBridge.cs")
                {
                    bridgeDelegateTableEradicated =
                        scanText.IndexOf("TryRegisterChargerLinkDelegate", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("TryUnregisterChargerLinksDelegate", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("TryWriteInventorySlotStateDelegate", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("TryReadCharge01Delegate", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("public delegate", StringComparison.Ordinal) < 0;
                    bridgeUsesCachedRegistryService =
                        scanText.IndexOf("IBatteryChargerLogisticsService s_service", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BindService(IBatteryChargerLogisticsService service)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("Volatile.Read(ref s_service)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("GlobalRegistry.BatteryChargerLogistics", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("public static void Clear(", StringComparison.Ordinal) < 0;
                }

                if (fileName == "BatteryLogisticsXRayWindow.cs")
                {
                    xrayDisplaysSkippedCadenceFrames =
                        scanText.IndexOf("entry.SkippedCadenceFrames", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("TelemetryFlagSkippedCadence", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("barColor = new Color(0.2f, 0.55f, 1f, 1f)", StringComparison.Ordinal) >= 0;
                }

                if (fileName == "GlobalRegistry.BatteryChargerLogistics.cs")
                {
                    globalRegistryBatteryServiceRoute =
                        scanText.IndexOf("IBatteryChargerLogisticsService _batteryChargerLogisticsRuntime", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("RegisterBatteryChargerLogisticsRuntime(IBatteryChargerLogisticsService instance)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("UnregisterBatteryChargerLogisticsRuntime(IBatteryChargerLogisticsService instance)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("ResetBatteryChargerLogisticsRuntimeForDomainReload()", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("Interlocked.Exchange(ref _batteryChargerLogisticsRuntime, null)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BatteryChargerLogisticsBridge.BindService(instance)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BatteryChargerLogisticsBridge.BindService(null)", StringComparison.Ordinal) >= 0;
                }

                if (fileName == "BatteryChargerModule.cs")
                {
                    constructionModuleConcreteGameplayToolImportHits =
                        CountToken(scanText, "using Hecton8.Gameplay") +
                        CountToken(scanText, "using Hecton8.Tools");
                    concreteFacadeBridgeContractResidual |= constructionModuleConcreteGameplayToolImportHits > 0;
                }

                if (fileName == "BatteryChargerLogisticsContracts.cs")
                {
                    telemetryContractsUseFenceElapsedMicroseconds =
                        scanText.IndexOf("FenceElapsedMicroseconds", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("FaultDumpFenceElapsedThresholdMicroseconds", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("BurstMicroseconds", StringComparison.Ordinal) < 0 &&
                        scanText.IndexOf("FaultDumpThresholdMicroseconds", StringComparison.Ordinal) < 0;
                    nanFaultProducerPresent =
                        scanText.IndexOf("AddFaultFlags(BatteryChargerLogisticsConstants.TelemetryFlagNaN", StringComparison.Ordinal) >= 0;
                    rawPointerSafetyJustificationPresent =
                        scanText.IndexOf("SAFETY: All pointer fields are generation-resolved", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("ALIASING:", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("LIFETIME:", StringComparison.Ordinal) >= 0;
                    csvParserRejectsMalformedRows =
                        scanText.IndexOf("TryParseFiniteFloat(NextField(ref line), out float maxChargeRate)", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("Trim(line).Length != 0", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("!sawDigit || index != value.Length", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("math.isfinite(parsed)", StringComparison.Ordinal) >= 0;
                }

                if (fileName == "InventoryRoutingNetwork.cs")
                {
                    inventoryRoutingSharesShinobuSlots =
                        scanText.IndexOf("BufferID.ShinobuInventorySlots", StringComparison.Ordinal) >= 0;
                    inventoryRoutingWholeSlotMaintenanceWriters =
                        scanText.IndexOf("PublishInventoryContainerSnapshotJob", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("ClearInventoryContainerRangeJob", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("TickInventoryDecayJob", StringComparison.Ordinal) >= 0 &&
                        scanText.IndexOf("CompactInventoryArrayJob", StringComparison.Ordinal) >= 0;
                }

                ScanCounts fileScan = AnalyzeSource(fileName, scanText);
                if (!fileScan.IsChargerFile)
                    continue;

                chargerFiles++;
                int fileUpdate = fileScan.UpdateLoops;
                int fileCoroutine = fileScan.CoroutineLoops;
                int fileLists = fileScan.ManagedBatteryLists;
                int fileArrays = fileScan.ManagedBatteryArrays;
                int fileSlow = fileScan.SlowTickRegistrations;
                int fileShadow = fileScan.ManagedChargingShadowState;
                int fileGridDirty = fileScan.LegacyGridDirty;
                int fileFacade = fileScan.LegacySlotFacades;

                updateLoops += fileUpdate;
                coroutineLoops += fileCoroutine;
                managedBatteryLists += fileLists;
                managedBatteryArrays += fileArrays;
                slowTickRegistrations += fileSlow;
                managedChargingShadowState += fileShadow;
                legacyGridDirty += fileGridDirty;
                legacySlotFacades += fileFacade;

                if (fileUpdate + fileCoroutine + fileLists + fileArrays + fileSlow + fileShadow + fileGridDirty > 0)
                {
                    findings.Append("    { \"path\": \"");
                    findings.Append(Escape(Path.GetRelativePath(projectRoot, path)));
                    findings.Append("\", \"updateLoops\": ");
                    findings.Append(fileUpdate);
                    findings.Append(", \"coroutines\": ");
                    findings.Append(fileCoroutine);
                    findings.Append(", \"managedBatteryLists\": ");
                    findings.Append(fileLists);
                    findings.Append(", \"managedBatteryArrays\": ");
                    findings.Append(fileArrays);
                    findings.Append(", \"slowTickRegistrations\": ");
                    findings.Append(fileSlow);
                    findings.Append(", \"managedChargingShadowState\": ");
                    findings.Append(fileShadow);
                    findings.Append(", \"legacyGridDirty\": ");
                    findings.Append(fileGridDirty);
                    findings.Append(" },\n");
                }
            }

            bool eradicated = updateLoops == 0 &&
                              coroutineLoops == 0 &&
                              managedBatteryLists == 0 &&
                              managedBatteryArrays == 0 &&
                              slowTickRegistrations == 0 &&
                              managedChargingShadowState == 0 &&
                              legacyGridDirty == 0;

            bool routeProofClean = facadeUsesCurrentOriginAupProof &&
                                   facadeWorldImportHits == 0 &&
                                   facadeRejectsDirectFloatingOriginBridge &&
                                   runtimeWorldImportHits == 0 &&
                                   runtimeWorldRouteHits == 0;
            bool chargerConservationExternalFenceRequired = inventoryRoutingSharesShinobuSlots &&
                                                            inventoryRoutingWholeSlotMaintenanceWriters;

            runtimeAsmdefPresent = File.Exists(runtimeAsmdefPath);
            editorAsmdefPresent = File.Exists(editorAsmdefPath);
            if (runtimeAsmdefPresent)
            {
                string asmdefText = File.ReadAllText(runtimeAsmdefPath);
                runtimeAsmdefNoSiblingRuntimeRefs =
                    asmdefText.IndexOf("\"Hecton8.Inventory.Routing.Runtime\"", StringComparison.Ordinal) < 0 &&
                    asmdefText.IndexOf("\"Hecton8.Gameplay.", StringComparison.Ordinal) < 0 &&
                    asmdefText.IndexOf("\"Hecton8.Construction.", StringComparison.Ordinal) < 0 &&
                    asmdefText.IndexOf("\"Hecton8.World.", StringComparison.Ordinal) < 0 &&
                    asmdefText.IndexOf("\"Hecton8.Power.Generators\"", StringComparison.Ordinal) < 0;
            }

            if (File.Exists(binaryLedgerPath))
            {
                string ledgerText = File.ReadAllText(binaryLedgerPath);
                binaryPayloadLedgerRangeRegistered =
                    ledgerText.IndexOf("`72300..72310`", StringComparison.Ordinal) >= 0 &&
                    ledgerText.IndexOf("battery charger logistics", StringComparison.OrdinalIgnoreCase) >= 0;
                binaryPayloadLedgerBoundaryRegistered =
                    ledgerText.IndexOf("SHINOBU_230 Battery Charger Logistics Payload Boundary", StringComparison.Ordinal) >= 0 &&
                    ledgerText.IndexOf("Dump_SHINOBU_230.bin", StringComparison.Ordinal) >= 0 &&
                    ledgerText.IndexOf("`72310` mock inventory slots", StringComparison.Ordinal) >= 0;
                faultDumpBlockingFaultOnlyDocumented =
                    ledgerText.IndexOf("blocking fault-only exception", StringComparison.Ordinal) >= 0 &&
                    ledgerText.IndexOf("fence-elapsed budget breach", StringComparison.Ordinal) >= 0;
            }

            if (!binaryPayloadLedgerRangeRegistered || !binaryPayloadLedgerBoundaryRegistered)
            {
                findings.Append("    { \"path\": \"Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md\", \"binaryPayloadLedgerRangeRegistered\": ");
                findings.Append(binaryPayloadLedgerRangeRegistered ? "true" : "false");
                findings.Append(", \"binaryPayloadLedgerBoundaryRegistered\": ");
                findings.Append(binaryPayloadLedgerBoundaryRegistered ? "true" : "false");
                findings.Append(" },\n");
            }

            if (!runtimeAsmdefPresent ||
                !runtimeAsmdefNoSiblingRuntimeRefs ||
                !editorAsmdefPresent ||
                !facadeUsesBridgeNoRuntimeCall ||
                !runtimeRegistersBridge ||
                !runtimeRegistersGlobalRegistryService ||
                !bridgeDelegateTableEradicated ||
                !bridgeUsesCachedRegistryService ||
                !globalRegistryBatteryServiceRoute)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics\", \"runtimeAsmdefPresent\": ");
                findings.Append(runtimeAsmdefPresent ? "true" : "false");
                findings.Append(", \"runtimeAsmdefNoSiblingRuntimeRefs\": ");
                findings.Append(runtimeAsmdefNoSiblingRuntimeRefs ? "true" : "false");
                findings.Append(", \"editorAsmdefPresent\": ");
                findings.Append(editorAsmdefPresent ? "true" : "false");
                findings.Append(", \"facadeUsesBridgeNoRuntimeCall\": ");
                findings.Append(facadeUsesBridgeNoRuntimeCall ? "true" : "false");
                findings.Append(", \"facadeDirectRuntimeCallHits\": ");
                findings.Append(facadeDirectRuntimeCallHits);
                findings.Append(", \"runtimeRegistersBridge\": ");
                findings.Append(runtimeRegistersBridge ? "true" : "false");
                findings.Append(", \"runtimeRegistersGlobalRegistryService\": ");
                findings.Append(runtimeRegistersGlobalRegistryService ? "true" : "false");
                findings.Append(", \"registryResetClearsBridgeForDomainReload\": ");
                findings.Append(registryResetClearsBridgeForDomainReload ? "true" : "false");
                findings.Append(", \"bridgeDirectClearEradicated\": ");
                findings.Append(bridgeDirectClearEradicated ? "true" : "false");
                findings.Append(", \"bridgeDelegateTableEradicated\": ");
                findings.Append(bridgeDelegateTableEradicated ? "true" : "false");
                findings.Append(", \"bridgeUsesCachedRegistryService\": ");
                findings.Append(bridgeUsesCachedRegistryService ? "true" : "false");
                findings.Append(", \"globalRegistryBatteryServiceRoute\": ");
                findings.Append(globalRegistryBatteryServiceRoute ? "true" : "false");
                findings.Append(" },\n");
            }

            if (!lockedSimulationTickDeltaUsed || !frameDeltaBypassedForChargeAuthority || !cadenceCapPreservesAccumulatorRemainder || !authorityAccumulatorSubtractedAfterAdmission)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs\", \"lockedSimulationTickDeltaUsed\": ");
                findings.Append(lockedSimulationTickDeltaUsed ? "true" : "false");
                findings.Append(", \"frameDeltaBypassedForChargeAuthority\": ");
                findings.Append(frameDeltaBypassedForChargeAuthority ? "true" : "false");
                findings.Append(", \"cadenceCapPreservesAccumulatorRemainder\": ");
                findings.Append(cadenceCapPreservesAccumulatorRemainder ? "true" : "false");
                findings.Append(", \"authorityAccumulatorSubtractedAfterAdmission\": ");
                findings.Append(authorityAccumulatorSubtractedAfterAdmission ? "true" : "false");
                findings.Append(" },\n");
            }

            if (!editorTuningWritesResolvedQualityCadence)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs\", \"editorTuningWritesResolvedQualityCadence\": false },\n");
            }

            if (!runtimeFiniteDtoWriteGuards)
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs\", \"runtimeFiniteDtoWriteGuards\": false },\n");

            if (!telemetryUsesFenceElapsedMicroseconds || !telemetryContractsUseFenceElapsedMicroseconds)
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics\", \"telemetryUsesFenceElapsedMicroseconds\": false },\n");

            if (!faultDumpBlockingFaultOnlyDocumented)
                findings.Append("    { \"path\": \"Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md\", \"faultDumpBlockingFaultOnlyDocumented\": false },\n");

            if (!coldSlotWriteUsesGenerationHandleFence)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs\", \"coldSlotWriteUsesGenerationHandleFence\": false },\n");
            }

            if (!skippedCadenceTelemetryCoalesced)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs\", \"skippedCadenceTelemetryCoalesced\": false, \"skippedCadenceTelemetryRecorded\": ");
                findings.Append(skippedCadenceTelemetryRecorded ? "true" : "false");
                findings.Append(" },\n");
            }

            if (!xrayDisplaysSkippedCadenceFrames)
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Power/Editor/BatteryLogisticsXRayWindow.cs\", \"xrayDisplaysSkippedCadenceFrames\": false },\n");

            if (!playerInventoryBridgeHardReservationProof)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Gameplay/BatteryCharger.cs\", \"playerInventoryBridgeHardReservationProof\": false, \"playerInventoryBridgeRemovesBeforeChargerCommit\": ");
                findings.Append(playerInventoryBridgeRemovesBeforeChargerCommit ? "true" : "false");
                findings.Append(", \"playerInventoryBridgeReservesBeforeChargerCommit\": ");
                findings.Append(playerInventoryBridgeReservesBeforeChargerCommit ? "true" : "false");
                findings.Append(", \"playerInventoryBridgeCommitsReservationAfterChargerCommit\": ");
                findings.Append(playerInventoryBridgeCommitsReservationAfterChargerCommit ? "true" : "false");
                findings.Append(", \"playerInventoryBridgeReleasesReservationOnFailure\": ");
                findings.Append(playerInventoryBridgeReleasesReservationOnFailure ? "true" : "false");
                findings.Append(" },\n");
            }

            if (!routeProofClean)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Gameplay/BatteryCharger.cs\", \"routeProofClean\": false, \"facadeWorldImportHits\": ");
                findings.Append(facadeWorldImportHits);
                findings.Append(", \"facadeWorldRouteHits\": ");
                findings.Append(facadeWorldRouteHits);
                findings.Append(", \"facadeGlobalOriginAupHits\": ");
                findings.Append(facadeGlobalOriginAupHits);
                findings.Append(", \"facadeOffsetAbsoluteAupHits\": ");
                findings.Append(facadeOffsetAbsoluteAupHits);
                findings.Append(", \"facadeDirectFloatingOriginBridgeHits\": ");
                findings.Append(facadeDirectFloatingOriginBridgeHits);
                findings.Append(", \"facadeFromRuntimePositionHits\": ");
                findings.Append(facadeFromRuntimePositionHits);
                findings.Append(", \"facadeUsesCurrentOriginAupProof\": ");
                findings.Append(facadeUsesCurrentOriginAupProof ? "true" : "false");
                findings.Append(", \"facadeAupFiniteGuarded\": ");
                findings.Append(facadeAupFiniteGuarded ? "true" : "false");
                findings.Append(", \"runtimeWorldImportHits\": ");
                findings.Append(runtimeWorldImportHits);
                findings.Append(", \"runtimeWorldRouteHits\": ");
                findings.Append(runtimeWorldRouteHits);
                findings.Append(" },\n");
            }

            if (concreteFacadeBridgeContractResidual)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Gameplay/BatteryCharger.cs\", \"ownership\": \"Gameplay/Construction facade contract residual\", \"concreteFacadeBridgeContractResidual\": true, \"facadeConcreteInventoryToolImportHits\": ");
                findings.Append(facadeConcreteInventoryToolImportHits);
                findings.Append(", \"constructionModuleConcreteGameplayToolImportHits\": ");
                findings.Append(constructionModuleConcreteGameplayToolImportHits);
                findings.Append(" },\n");
            }

            if (chargerConservationExternalFenceRequired)
            {
                findings.Append("    { \"path\": \"Assets/_Project/Scripts/Inventory/InventoryRoutingNetwork.cs\", \"ownership\": \"Inventory Routing owner\", \"chargerConservationExternalFenceRequired\": true, \"inventoryRoutingSharesShinobuSlots\": ");
                findings.Append(inventoryRoutingSharesShinobuSlots ? "true" : "false");
                findings.Append(", \"inventoryRoutingWholeSlotMaintenanceWriters\": ");
                findings.Append(inventoryRoutingWholeSlotMaintenanceWriters ? "true" : "false");
                findings.Append(" },\n");
            }

            if (findings.Length >= 2)
                findings.Length -= 2;

            StringBuilder json = new StringBuilder(2048);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_230\",\n");
            json.Append("  \"scanner\": \"Charger_OOP_Scanner\",\n");
            json.Append("  \"summary\": \"");
            json.Append(eradicated ? "Managed Charging Scripts Eradicated" : "Managed Charging Scripts Still Present");
            json.Append("\",\n");
            json.Append("  \"forbiddenPatternHits\": ");
            json.Append(updateLoops + coroutineLoops + managedBatteryLists + managedBatteryArrays + slowTickRegistrations + managedChargingShadowState + legacyGridDirty);
            json.Append(",\n");
            json.Append("  \"chargerFilesScanned\": ");
            json.Append(chargerFiles);
            json.Append(",\n");
            json.Append("  \"forbiddenPatterns\": {\n");
            json.Append("    \"updateLoops\": ");
            json.Append(updateLoops);
            json.Append(",\n");
            json.Append("    \"coroutineLoops\": ");
            json.Append(coroutineLoops);
            json.Append(",\n");
            json.Append("    \"managedBatteryLists\": ");
            json.Append(managedBatteryLists);
            json.Append(",\n");
            json.Append("    \"managedBatteryArrays\": ");
            json.Append(managedBatteryArrays);
            json.Append(",\n");
            json.Append("    \"slowTickRegistrations\": ");
            json.Append(slowTickRegistrations);
            json.Append(",\n");
            json.Append("    \"managedChargingShadowState\": ");
            json.Append(managedChargingShadowState);
            json.Append(",\n");
            json.Append("    \"legacyGridDirty\": ");
            json.Append(legacyGridDirty);
            json.Append("\n  },\n");
            json.Append("  \"legacyFacadePatterns\": {\n");
            json.Append("    \"batterySlotFacadeTokens\": ");
            json.Append(legacySlotFacades);
            json.Append("\n  },\n");
            json.Append("  \"scannerStripsCommentsAndStrings\": true,\n");
            json.Append("  \"scannerSelfClassificationFalsePositiveFixed\": true,\n");
            json.Append("  \"scannerUsesStructuralSyntaxPass\": true,\n");
            json.Append("  \"scannerUsesCustomSyntaxPass\": true,\n");
            json.Append("  \"scannerUsesAstParser\": false,\n");
            json.Append("  \"scannerParserRoute\": \"comment/string stripped custom declaration and invocation parser; no Roslyn dependency\",\n");
            json.Append("  \"scannerCountsMemberInvocations\": true,\n");
            json.Append("  \"routeProofClean\": ");
            json.Append(routeProofClean ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeAsmdefPresent\": ");
            json.Append(runtimeAsmdefPresent ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeAsmdefNoSiblingRuntimeRefs\": ");
            json.Append(runtimeAsmdefNoSiblingRuntimeRefs ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"editorAsmdefPresent\": ");
            json.Append(editorAsmdefPresent ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeUsesBridgeNoRuntimeCall\": ");
            json.Append(facadeUsesBridgeNoRuntimeCall ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeDirectRuntimeCallHits\": ");
            json.Append(facadeDirectRuntimeCallHits);
            json.Append(",\n");
            json.Append("  \"runtimeRegistersBridge\": ");
            json.Append(runtimeRegistersBridge ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeRegistersGlobalRegistryService\": ");
            json.Append(runtimeRegistersGlobalRegistryService ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"registryResetClearsBridgeForDomainReload\": ");
            json.Append(registryResetClearsBridgeForDomainReload ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"bridgeDirectClearEradicated\": ");
            json.Append(bridgeDirectClearEradicated ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"bridgeDelegateTableEradicated\": ");
            json.Append(bridgeDelegateTableEradicated ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"bridgeUsesCachedRegistryService\": ");
            json.Append(bridgeUsesCachedRegistryService ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"globalRegistryBatteryServiceRoute\": ");
            json.Append(globalRegistryBatteryServiceRoute ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"lockedSimulationTickDeltaUsed\": ");
            json.Append(lockedSimulationTickDeltaUsed ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"frameDeltaBypassedForChargeAuthority\": ");
            json.Append(frameDeltaBypassedForChargeAuthority ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"cadenceCapPreservesAccumulatorRemainder\": ");
            json.Append(cadenceCapPreservesAccumulatorRemainder ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"authorityAccumulatorSubtractedAfterAdmission\": ");
            json.Append(authorityAccumulatorSubtractedAfterAdmission ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"editorTuningWritesResolvedQualityCadence\": ");
            json.Append(editorTuningWritesResolvedQualityCadence ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeFiniteDtoWriteGuards\": ");
            json.Append(runtimeFiniteDtoWriteGuards ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"telemetryUsesFenceElapsedMicroseconds\": ");
            json.Append((telemetryUsesFenceElapsedMicroseconds && telemetryContractsUseFenceElapsedMicroseconds) ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"faultDumpBlockingFaultOnlyDocumented\": ");
            json.Append(faultDumpBlockingFaultOnlyDocumented ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"scheduleLocksBeforeBufferResolve\": ");
            json.Append(scheduleLocksBeforeBufferResolve ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"cadenceQualitySampleUsesTuningLock\": ");
            json.Append(cadenceQualitySampleUsesTuningLock ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"jobBufferLockIncludesTuning\": ");
            json.Append(jobBufferLockIncludesTuning ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"coldSlotWriteLocksBeforeResolve\": ");
            json.Append(coldSlotWriteLocksBeforeResolve ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"coldSlotWriteUsesGenerationHandleFence\": ");
            json.Append(coldSlotWriteUsesGenerationHandleFence ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"chargeReadRejectsReservedLock\": ");
            json.Append(chargeReadRejectsReservedLock ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeRejectsUnassignedInventorySlotZero\": ");
            json.Append(facadeRejectsUnassignedInventorySlotZero ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeWritesSlotBeforeLinkRegistration\": ");
            json.Append(facadeWritesSlotBeforeLinkRegistration ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeUsesCoreFloatingOriginAup\": ");
            json.Append(facadeUsesCoreFloatingOriginAup ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeUsesCurrentOriginAupProof\": ");
            json.Append(facadeUsesCurrentOriginAupProof ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeRejectsDirectFloatingOriginBridge\": ");
            json.Append(facadeRejectsDirectFloatingOriginBridge ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeAupFiniteGuarded\": ");
            json.Append(facadeAupFiniteGuarded ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeWorldImportHits\": ");
            json.Append(facadeWorldImportHits);
            json.Append(",\n");
            json.Append("  \"facadeWorldRouteHits\": ");
            json.Append(facadeWorldRouteHits);
            json.Append(",\n");
            json.Append("  \"facadeGlobalOriginAupHits\": ");
            json.Append(facadeGlobalOriginAupHits);
            json.Append(",\n");
            json.Append("  \"facadeOffsetAbsoluteAupHits\": ");
            json.Append(facadeOffsetAbsoluteAupHits);
            json.Append(",\n");
            json.Append("  \"facadeDirectFloatingOriginBridgeHits\": ");
            json.Append(facadeDirectFloatingOriginBridgeHits);
            json.Append(",\n");
            json.Append("  \"facadeFromRuntimePositionHits\": ");
            json.Append(facadeFromRuntimePositionHits);
            json.Append(",\n");
            json.Append("  \"humAupWritesContractFields\": ");
            json.Append(humAupWritesContractFields ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"humAupRejectsOutOfExtent\": ");
            json.Append(humAupRejectsOutOfExtent ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeWorldImportHits\": ");
            json.Append(runtimeWorldImportHits);
            json.Append(",\n");
            json.Append("  \"runtimeWorldRouteHits\": ");
            json.Append(runtimeWorldRouteHits);
            json.Append(",\n");
            json.Append("  \"interactTextUsesCachedToolOnly\": ");
            json.Append(interactTextUsesCachedToolOnly ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgeRemovesBeforeChargerCommit\": ");
            json.Append(playerInventoryBridgeRemovesBeforeChargerCommit ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgeReservesBeforeChargerCommit\": ");
            json.Append(playerInventoryBridgeReservesBeforeChargerCommit ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgeCommitsReservationAfterChargerCommit\": ");
            json.Append(playerInventoryBridgeCommitsReservationAfterChargerCommit ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgeReleasesReservationOnFailure\": ");
            json.Append(playerInventoryBridgeReleasesReservationOnFailure ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgePreflightsAuthoredSlotRange\": ");
            json.Append(playerInventoryBridgePreflightsAuthoredSlotRange ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryRollbackResultChecked\": ");
            json.Append(playerInventoryRollbackResultChecked ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"playerInventoryBridgeHardReservationProof\": ");
            json.Append(playerInventoryBridgeHardReservationProof ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"toolSwapRollsBackOnInsertFailure\": ");
            json.Append(toolSwapRollsBackOnInsertFailure ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"toolSwapRollbackResultsChecked\": ");
            json.Append(toolSwapRollbackResultsChecked ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"toolSwapPreflightsBeforeToolRemoval\": ");
            json.Append(toolSwapPreflightsBeforeToolRemoval ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"removeToInventoryPreflightsCapacity\": ");
            json.Append(removeToInventoryPreflightsCapacity ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeColdInitializesSlotObjects\": ");
            json.Append(facadeColdInitializesSlotObjects ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"facadeConcreteInventoryToolImportHits\": ");
            json.Append(facadeConcreteInventoryToolImportHits);
            json.Append(",\n");
            json.Append("  \"constructionModuleConcreteGameplayToolImportHits\": ");
            json.Append(constructionModuleConcreteGameplayToolImportHits);
            json.Append(",\n");
            json.Append("  \"concreteFacadeBridgeContractResidual\": ");
            json.Append(concreteFacadeBridgeContractResidual ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeUsesMockInventorySlotsOwnedByPower\": ");
            json.Append(runtimeUsesMockInventorySlotsOwnedByPower ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"runtimeSharedInventoryAllocationHits\": ");
            json.Append(runtimeSharedInventoryAllocationHits ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"emergencyMockEditorOrDevelopmentOnly\": ");
            json.Append(emergencyMockEditorOrDevelopmentOnly ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"liveRegistrationDropsMockFallback\": ");
            json.Append(liveRegistrationDropsMockFallback ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"visualBuffersPrewarmedBeforeVisualSync\": ");
            json.Append(visualBuffersPrewarmedBeforeVisualSync ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"skippedCadenceTelemetryRecorded\": ");
            json.Append(skippedCadenceTelemetryRecorded ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"skippedCadenceTelemetryCoalesced\": ");
            json.Append(skippedCadenceTelemetryCoalesced ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"xrayDisplaysSkippedCadenceFrames\": ");
            json.Append(xrayDisplaysSkippedCadenceFrames ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"nanFaultProducerPresent\": ");
            json.Append(nanFaultProducerPresent ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"rawPointerSafetyJustificationPresent\": ");
            json.Append(rawPointerSafetyJustificationPresent ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"csvParserRejectsMalformedRows\": ");
            json.Append(csvParserRejectsMalformedRows ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"binaryPayloadLedgerRangeRegistered\": ");
            json.Append(binaryPayloadLedgerRangeRegistered ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"binaryPayloadLedgerBoundaryRegistered\": ");
            json.Append(binaryPayloadLedgerBoundaryRegistered ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"inventoryRoutingSharesShinobuSlots\": ");
            json.Append(inventoryRoutingSharesShinobuSlots ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"inventoryRoutingWholeSlotMaintenanceWriters\": ");
            json.Append(inventoryRoutingWholeSlotMaintenanceWriters ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"chargerConservationExternalFenceRequired\": ");
            json.Append(chargerConservationExternalFenceRequired ? "true" : "false");
            json.Append(",\n");
            json.Append("  \"verdictScope\": \"scanner-only\",\n");
            json.Append("  \"verdict\": \"");
            bool ownedScannerProofClean = eradicated &&
                                          routeProofClean &&
                                          binaryPayloadLedgerRangeRegistered &&
                                          binaryPayloadLedgerBoundaryRegistered &&
                                          runtimeAsmdefPresent &&
                                          runtimeAsmdefNoSiblingRuntimeRefs &&
                                          editorAsmdefPresent &&
                                          facadeUsesBridgeNoRuntimeCall &&
                                          runtimeRegistersBridge &&
                                          runtimeRegistersGlobalRegistryService &&
                                          registryResetClearsBridgeForDomainReload &&
                                          bridgeDirectClearEradicated &&
                                          bridgeDelegateTableEradicated &&
                                          bridgeUsesCachedRegistryService &&
                                          globalRegistryBatteryServiceRoute &&
                                          lockedSimulationTickDeltaUsed &&
                                          frameDeltaBypassedForChargeAuthority &&
                                          cadenceCapPreservesAccumulatorRemainder &&
                                          authorityAccumulatorSubtractedAfterAdmission &&
                                          editorTuningWritesResolvedQualityCadence &&
                                          runtimeFiniteDtoWriteGuards &&
                                          telemetryUsesFenceElapsedMicroseconds &&
                                          telemetryContractsUseFenceElapsedMicroseconds &&
                                          faultDumpBlockingFaultOnlyDocumented &&
                                          coldSlotWriteUsesGenerationHandleFence &&
                                          skippedCadenceTelemetryCoalesced &&
                                          xrayDisplaysSkippedCadenceFrames &&
                                          playerInventoryBridgeHardReservationProof;
            json.Append(!ownedScannerProofClean
                ? "FAIL"
                : concreteFacadeBridgeContractResidual || chargerConservationExternalFenceRequired
                    ? "PARTIAL_BLOCKED_BY_CROSS_DOMAIN_OWNER"
                    : "PASS");
            json.Append("\",\n");
            json.Append("  \"findings\": [\n");
            json.Append(findings);
            json.Append("\n  ]\n");
            json.Append("}\n");
            WriteSharedReport(reportPath, json.ToString());
            return reportPath;
        }

        private static void WriteSharedReport(string reportPath, string entryJson)
        {
            string entry = entryJson.Trim();
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{\n  \"reports\": [\n" + Indent(entry, 4) + "\n  ]\n}\n", Encoding.UTF8);
                return;
            }

            string existing = RemoveAgentEntry(File.ReadAllText(reportPath), "SHINOBU_230").Trim();
            int reportsKey = existing.IndexOf("\"reports\"", StringComparison.Ordinal);
            int arrayEnd = reportsKey >= 0 ? existing.LastIndexOf(']') : -1;
            if (reportsKey >= 0 && arrayEnd >= 0)
            {
                string head = existing.Substring(0, arrayEnd).TrimEnd();
                bool emptyArray = head.EndsWith("[", StringComparison.Ordinal);
                string separator = emptyArray ? "\n" : ",\n";
                string merged = head + separator + Indent(entry, 4) + "\n  ]\n}\n";
                File.WriteAllText(reportPath, merged, Encoding.UTF8);
                return;
            }

            string wrapped = "{\n  \"reports\": [\n" +
                             Indent(existing, 4) +
                             ",\n" +
                             Indent(entry, 4) +
                             "\n  ]\n}\n";
            File.WriteAllText(reportPath, wrapped, Encoding.UTF8);
        }

        private static string Indent(string value, int spaces)
        {
            string prefix = new string(' ', spaces);
            return prefix + value.Replace("\r\n", "\n").Replace("\n", "\n" + prefix);
        }

        private static string RemoveAgentEntry(string json, string agent)
        {
            string quotedAgent = "\"" + agent + "\"";
            int searchStart = 0;
            while (searchStart < json.Length)
            {
                int agentIndex = json.IndexOf(quotedAgent, searchStart, StringComparison.Ordinal);
                if (agentIndex < 0)
                    break;

                int objectStart = json.LastIndexOf('{', agentIndex);
                int objectEnd = objectStart >= 0 ? FindJsonObjectEnd(json, objectStart) : -1;
                if (objectStart < 0 || objectEnd < objectStart)
                {
                    searchStart = agentIndex + quotedAgent.Length;
                    continue;
                }

                int removeStart = objectStart;
                int removeEnd = objectEnd + 1;
                int next = SkipJsonWhitespace(json, removeEnd);
                if (next < json.Length && json[next] == ',')
                {
                    removeEnd = next + 1;
                }
                else
                {
                    int previous = PreviousJsonNonWhitespace(json, removeStart - 1);
                    if (previous >= 0 && json[previous] == ',')
                        removeStart = previous;
                }

                json = json.Remove(removeStart, removeEnd - removeStart);
                searchStart = removeStart < 0 ? 0 : removeStart;
            }

            return json;
        }

        private static int FindJsonObjectEnd(string json, int objectStart)
        {
            bool stringLiteral = false;
            int depth = 0;
            for (int i = objectStart; i < json.Length; i++)
            {
                char c = json[i];
                if (stringLiteral)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        stringLiteral = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    stringLiteral = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int SkipJsonWhitespace(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            return index;
        }

        private static int PreviousJsonNonWhitespace(string json, int index)
        {
            while (index >= 0 && char.IsWhiteSpace(json[index]))
                index--;
            return index;
        }

        private static ScanCounts AnalyzeSource(string fileName, string scanText)
        {
            ScanCounts counts = default;
            bool chargerName = fileName.IndexOf("BatteryCharger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               fileName.IndexOf("PowerCellCharger", StringComparison.OrdinalIgnoreCase) >= 0;
            counts.IsChargerFile = chargerName || ContainsClassName(scanText, "BatteryCharger") || ContainsClassName(scanText, "PowerCellCharger");
            if (!counts.IsChargerFile)
                return counts;

            counts.UpdateLoops = CountMethodDeclaration(scanText, "Update");
            counts.CoroutineLoops = CountInvocation(scanText, "StartCoroutine") + CountIdentifier(scanText, "IEnumerator");
            counts.ManagedBatteryLists = CountToken(scanText, "List<Battery") + CountToken(scanText, "List<PowerCell");
            counts.ManagedBatteryArrays = CountToken(scanText, "Battery[]") + CountToken(scanText, "PowerCell[]");
            counts.SlowTickRegistrations = CountInvocation(scanText, "RegisterSlowTickable") + CountIdentifier(scanText, "ISlowTickable");
            counts.ManagedChargingShadowState = CountIdentifier(scanText, "_isCharging") +
                                                CountInvocation(scanText, "SetChargingState") +
                                                CountInvocation(scanText, "RefreshChargingDemand") +
                                                CountInvocation(scanText, "HasChargeWork");
            counts.LegacyGridDirty = CountInvocation(scanText, "MarkPowerGridDirty") +
                                     CountInvocation(scanText, "MarkGridDirty") +
                                     CountToken(scanText, "Grid.MarkDirty(");
            counts.LegacySlotFacades = CountToken(scanText, "BatterySlot[]") + CountClassDeclaration(scanText, "BatterySlot");
            return counts;
        }

        private static bool ContainsClassName(string text, string className)
        {
            return CountClassDeclaration(text, className) > 0;
        }

        private static int CountClassDeclaration(string text, string className)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = FindIdentifier(text, "class", index);
                if (found < 0)
                    break;

                int nameStart = SkipSpace(text, found + 5);
                if (IdentifierEquals(text, nameStart, className))
                    count++;

                index = found + 5;
            }

            return count;
        }

        private static int CountInvocation(string text, string methodName)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = FindIdentifier(text, methodName, index);
                if (found < 0)
                    break;

                int next = SkipSpace(text, found + methodName.Length);
                if (next < text.Length &&
                    text[next] == '(')
                {
                    count++;
                }

                index = found + methodName.Length;
            }

            return count;
        }

        private static int CountMethodDeclaration(string text, string methodName)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = FindIdentifier(text, methodName, index);
                if (found < 0)
                    break;

                int previous = PreviousNonSpace(text, found - 1);
                int next = SkipSpace(text, found + methodName.Length);
                if ((previous < 0 || text[previous] != '.') &&
                    next < text.Length &&
                    text[next] == '(' &&
                    TryFindMatchingParen(text, next, out int closeParen))
                {
                    int afterParams = SkipSpace(text, closeParen + 1);
                    if (afterParams < text.Length && (text[afterParams] == '{' || text[afterParams] == ';'))
                        count++;
                }

                index = found + methodName.Length;
            }

            return count;
        }

        private static int CountIdentifier(string text, string identifier)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = FindIdentifier(text, identifier, index);
                if (found < 0)
                    break;

                count++;
                index = found + identifier.Length;
            }

            return count;
        }

        private static int FindIdentifier(string text, string identifier, int startIndex)
        {
            int index = startIndex;
            while (index < text.Length)
            {
                int found = text.IndexOf(identifier, index, StringComparison.Ordinal);
                if (found < 0)
                    return -1;

                int before = found - 1;
                int after = found + identifier.Length;
                if ((before < 0 || !IsIdentifierChar(text[before])) &&
                    (after >= text.Length || !IsIdentifierChar(text[after])))
                {
                    return found;
                }

                index = found + identifier.Length;
            }

            return -1;
        }

        private static bool IdentifierEquals(string text, int index, string identifier)
        {
            if (index < 0 || index + identifier.Length > text.Length)
                return false;

            for (int i = 0; i < identifier.Length; i++)
            {
                if (text[index + i] != identifier[i])
                    return false;
            }

            int after = index + identifier.Length;
            return after >= text.Length || !IsIdentifierChar(text[after]);
        }

        private static int SkipSpace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return index;
        }

        private static int PreviousNonSpace(string text, int index)
        {
            while (index >= 0 && char.IsWhiteSpace(text[index]))
                index--;
            return index;
        }

        private static bool TryFindMatchingParen(string text, int openParen, out int closeParen)
        {
            int depth = 0;
            for (int i = openParen; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(')
                    depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeParen = i;
                        return true;
                    }
                }
            }

            closeParen = -1;
            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string StripNonCode(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\r' || c == '\n')
                    {
                        lineComment = false;
                        builder.Append(c);
                    }
                    else
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        i++;
                        blockComment = false;
                    }
                    else
                    {
                        builder.Append(c == '\r' || c == '\n' ? c : ' ');
                    }

                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString)
                    {
                        if (c == '"' && next == '"')
                        {
                            builder.Append(' ');
                            builder.Append(' ');
                            i++;
                            continue;
                        }

                        if (c == '"')
                        {
                            stringLiteral = false;
                            verbatimString = false;
                        }
                    }
                    else if (c == '\\')
                    {
                        builder.Append(' ');
                        if (i + 1 < text.Length)
                        {
                            builder.Append(' ');
                            i++;
                        }

                        continue;
                    }
                    else if (c == '"')
                    {
                        stringLiteral = false;
                    }

                    builder.Append(c == '\r' || c == '\n' ? c : ' ');
                    continue;
                }

                if (charLiteral)
                {
                    if (c == '\\')
                    {
                        builder.Append(' ');
                        if (i + 1 < text.Length)
                        {
                            builder.Append(' ');
                            i++;
                        }

                        continue;
                    }

                    if (c == '\'')
                        charLiteral = false;

                    builder.Append(c == '\r' || c == '\n' ? c : ' ');
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    lineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    blockComment = true;
                    continue;
                }

                if (c == '"' ||
                    (c == '@' && next == '"') ||
                    (c == '$' && next == '"') ||
                    (c == '$' && next == '@' && i + 2 < text.Length && text[i + 2] == '"') ||
                    (c == '@' && next == '$' && i + 2 < text.Length && text[i + 2] == '"'))
                {
                    int prefixLength = 0;
                    if (c == '@' || c == '$')
                        prefixLength = (next == '@' || next == '$') ? 2 : 1;

                    verbatimString = c == '@' || (c == '$' && next == '@') || (c == '@' && next == '$');
                    stringLiteral = true;

                    for (int p = 0; p <= prefixLength; p++)
                        builder.Append(' ');

                    i += prefixLength;
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
