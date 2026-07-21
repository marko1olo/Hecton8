78:            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
79-            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot = default;
80-            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner = default;
81-            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot = default;
82-            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner = default;
83-            NativeArray<uint> packedQuestStateSnapshot = default;
84-            QuestSaveHeader packedQuestSaveHeader = default;
85-            NativeArray<byte> voxelDeltaSnapshot = default;
86-            bool ownsVoxelDeltaSnapshot = false;
87-            VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner = null;
88-
89:            try
90-            {
91-                SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));
92-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.05f, 0u);
93-                EnsureSaveWorkingBuffers();
94-                RequestSnapshotPause(operationId);
95-                snapshotPauseActive = true;
96-                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
97-                if (TryRejectSaveDuringRespawnReconciliation(
98-                        slotIndex,
99-                        operationId,
100-                        slotName,
101-                        activeSaveStarted: true,
102-                        elapsedMs: totalTimer.ElapsedMilliseconds))
103-                {
104-                    return;
105-                }
106-
107-                thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);
108-                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();
109-                snapshotTimer.Restart();
110:                SortRegistryIfDirty(SavePriorityComparer);
111-                for (int i = 0; i < _saveableCount; i++)
112-                {
113-                    ISaveable saveable = _saveables[i];
114-                    if (!IsAlive(saveable))
115-                        continue;
116-
117-                    if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
118-                    {
119-                        Exception cleanupException = null;
120-                        if (borrowedVoxelDeltaSnapshotOwner != null)
121-                        {
122-                            ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
123-                            borrowedVoxelDeltaSnapshotOwner = null;
124-                        }
125-
126-                        if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
127-                            DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
128-                        else
129-                            voxelDeltaSnapshot = default;
130-
131-                        ReportPersistenceCleanupFailure("save", cleanupException);
132-                        ownsVoxelDeltaSnapshot = false;
133-                        if (!voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
134-                                out voxelDeltaSnapshot,
135-                                out int voxelDeltaSnapshotByteCount) ||
136-                            voxelDeltaSnapshotByteCount <= 0)
137-                        {
138-                            if (voxelDeltaSnapshotByteCount > 0)
139-                            {
140-                                const string reason = "Voxel delta native snapshot copy failed.";
141-                                const string logReason = "[SaveManager] Save failed: voxel delta native snapshot copy failed.";
142-                                const uint failureCode = 3u;
143-                                voxelDeltaSnapshot = default;
144-                                if (snapshotPauseActive)
145-                                {
146-                                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
147-                                    snapshotPauseActive = false;
148-                                }
149-
150-                                ReportPersistenceCleanupFailure("save", cleanupException);
151:                                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
152-                                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
153-                                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
154-                                DumpSaveBlackBox();
155-                                RecordFailure(slotName, "save", reason);
156-                                LastOperationError = reason;
157-                                LogError(logReason);
158-                                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
159-                                return;
160-                            }
161-
162-                            voxelDeltaSnapshot = default;
163-                        }
164-                        else
165-                        {
166-                            borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;
167-                        }
168-
169-                    }
170-
171-                    saveable.PopulateSaveData(data);
172-                }
173-
174-                StampRuntimeWorldSeed(data);
175-                StampProceduralTerrainIdentity(data);
176-                ModSaveStateStore.PopulateSaveData(data);
177-                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();
178:                if (persistentWorldRegistry != null)
179-                {
180:                    if (!persistentWorldRegistry.CaptureSaveSnapshot())
181-                    {
182-                        const string reason = "Persistent world save snapshot capture failed.";
183-                        const string logReason = "[SaveManager] Save failed: persistent world save snapshot capture failed.";
184-                        const uint failureCode = 3u;
185-                        Exception cleanupException = null;
186-                        if (snapshotPauseActive)
187-                        {
188-                            ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
189-                            snapshotPauseActive = false;
190-                        }
191-
192-                        ReportPersistenceCleanupFailure("save", cleanupException);
193:                        RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
194-                        PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
195-                        PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
196-                        DumpSaveBlackBox();
197-                        RecordFailure(slotName, "save", reason);
198-                        LastOperationError = reason;
199-                        LogError(logReason);
200-                        SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
201-                        return;
202-                    }
203-
204:                    int persistentWorldSnapshotCapacity = persistentWorldRegistry.SaveSnapshotCapacity;
205-                    if (persistentWorldSnapshotCapacity > 0)
206-                    {
207-                        persistentWorldDeltaSnapshotOwner = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
208-                            persistentWorldSnapshotCapacity,
209-                            Allocator.Persistent,
210-                            NativeArrayOptions.UninitializedMemory,
211-                            "persistentWorldDeltaSnapshotOwner");
212-
213:                        if (!persistentWorldRegistry.TryCopySaveSnapshotDeltas(
214-                            persistentWorldDeltaSnapshotOwner,
215-                            persistentWorldSnapshotCapacity,
216-                            out int copiedPersistentWorldDeltas))
217-                        {
218-                            Exception cleanupException = null;
219-                            DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
220-                            const string reason = "Persistent world save snapshot copy failed.";
221-                            const string logReason = "[SaveManager] Save failed: persistent world save snapshot copy failed.";
222-                            const uint failureCode = 3u;
223-                            if (snapshotPauseActive)
224-                            {
225-                                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
226-                                snapshotPauseActive = false;
227-                            }
228-
229-                            ReportPersistenceCleanupFailure("save", cleanupException);
230:                            RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
231-                            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
232-                            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
233-                            DumpSaveBlackBox();
234-                            RecordFailure(slotName, "save", reason);
235-                            LastOperationError = reason;
236-                            LogError(logReason);
237-                            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
238-                            return;
239-                        }
240-
241-                        if (copiedPersistentWorldDeltas > 0)
242-                        {
243-                            NativeArray<PersistentWorldDeltaRecord> copiedView = copiedPersistentWorldDeltas < persistentWorldDeltaSnapshotOwner.Length
244-                                ? persistentWorldDeltaSnapshotOwner.GetSubArray(0, copiedPersistentWorldDeltas)
245-                                : persistentWorldDeltaSnapshotOwner;
246-                            persistentWorldDeltaSnapshot = copiedView.AsReadOnly();
247-                        }
248-                    }
249-                }
250-
251:                EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
252-                if (ecosystemDirector != null)
253-                {
254-                    ecosystemDirector.CaptureSaveSnapshot();
255-                    NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
256-                    if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
257-                    {
258-                        ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
259-                            ecosystemRecordCount,
260-                            Allocator.Persistent,
261-                            NativeArrayOptions.UninitializedMemory,
262-                            "ecosystemSectorSnapshotOwner");
263-
264-                        for (int i = 0; i < ecosystemRecordCount; i++)
265-                            ecosystemSectorSnapshotOwner[i] = ecosystemView[i];
266-
267-                        ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
268-                    }
269-                }
270-
271-                divergenceSnapshotTimer.Stop();
272-        private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)
273-        {
274-            CachePersistentDataPathRoot();
275-            LastOperationSucceeded = false;
276-            LastOperationError = string.Empty;
277-            LastOperationSlot = string.Empty;
278-            LastLoadUsedBackup = false;
279-            LastLoadBackupGeneration = 0;
280-            LastLoadSelfRepaired = false;
281-            LastLoadUsedLegacyCompression = false;
282-
283-            if (_runtimeOwnerAborted || !_serviceRegistered)
284-            {
285-                uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName);
286-                LastOperationError = SaveServiceUnavailableReason;
287-                LastOperationSlot = unavailableSlotName;
288-                SaveEvents.TryRaiseSaveFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
289-                PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, 1u);
290-                return;
291-            }
292-
293-            if (!TryResolveSafeSlotName(slotName, out slotName))
294-            {
295-                LastOperationError = InvalidSlotNameReason;
296-                LogWarning("[SaveManager] Ignored save request: invalid slot name.");
297-                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
298-                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
299-                return;
300-            }
301-
302-            LastOperationSlot = slotName;
303-
304-            if (_isBusy)
305-            {
306-                const string reason = "Save already in progress.";
307-                LastOperationError = reason;
308-                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
309-                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
310-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
311-                return;
312-            }
313-
314-            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))
315-                return;
316-
317-            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
318-            {
319-                const string reason = "Save blocked during floating-origin shift.";
320-                LastOperationError = reason;
321-                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
322-                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
323-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
324-                return;
325-            }
326-
327-            _isBusy = true;
328-            Exception startupException = null;
329-            NotifyMacroDatabasePersistenceGateBestEffort(true, ref startupException);
330-            if (startupException != null)
331-            {
332-                const string reason = "Save persistence gate request failed.";
333-                LastOperationError = reason;
334-                LogWarningBestEffort($"[SaveManager] Save failed for '{slotName}': {reason}");
335-                _isBusy = false;
336-                NotifyMacroDatabasePersistenceGateBestEffort(false, ref startupException);
337-                ReportPersistenceCleanupFailure("save", startupException);
338-                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
339-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
340-                return;
341-            }
342-
343-            SaveThumbnailSystem.CaptureTicket thumbnailTicket = default;
344-            var totalTimer = Stopwatch.StartNew();
345-            var snapshotTimer = Stopwatch.StartNew();
346-            bool snapshotPauseActive = false;
347-            double playTime = ResolveCurrentPlayTimeSeconds();
348-            SaveData data = SaveData.CreateNew(playTime);
349:            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
350-            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot = default;
351-            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner = default;
352-            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot = default;
353-            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner = default;
354-            NativeArray<uint> packedQuestStateSnapshot = default;
355-            QuestSaveHeader packedQuestSaveHeader = default;
356-            NativeArray<byte> voxelDeltaSnapshot = default;
357-            bool ownsVoxelDeltaSnapshot = false;
358-            VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner = null;
359-
360:            try
361-            {
362-                SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));
363-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.05f, 0u);
364-                EnsureSaveWorkingBuffers();
365-                RequestSnapshotPause(operationId);
366-                snapshotPauseActive = true;
367-                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
368-                if (TryRejectSaveDuringRespawnReconciliation(
369-                        slotIndex,
370-                        operationId,
371-                        slotName,
372-                        activeSaveStarted: true,
373-                        elapsedMs: totalTimer.ElapsedMilliseconds))
374-                {
375-                    return;
376-                }
377-
378-                thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);
379-                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();
380-                snapshotTimer.Restart();
381:                SortRegistryIfDirty(SavePriorityComparer);
382-                for (int i = 0; i < _saveableCount; i++)
383-                {
384-                    ISaveable saveable = _saveables[i];
385-                    if (!IsAlive(saveable))
386-                        continue;
387-
388-                    if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
389-                    {
390-                        Exception cleanupException = null;
391-                        if (borrowedVoxelDeltaSnapshotOwner != null)
392-                        {
393-                            ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
394-                            borrowedVoxelDeltaSnapshotOwner = null;
395-                        }
396-
397-                        if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
398-                            DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
399-                        else
400-                            voxelDeltaSnapshot = default;
401-
402-                        ReportPersistenceCleanupFailure("save", cleanupException);
403-                        ownsVoxelDeltaSnapshot = false;
404-                        if (!voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
405-                                out voxelDeltaSnapshot,
406-                                out int voxelDeltaSnapshotByteCount) ||
407-                            voxelDeltaSnapshotByteCount <= 0)
408-                        {
409-                            if (voxelDeltaSnapshotByteCount > 0)
410-                            {
411-                                const string reason = "Voxel delta native snapshot copy failed.";
412-                                const string logReason = "[SaveManager] Save failed: voxel delta native snapshot copy failed.";
413-                                const uint failureCode = 3u;
414-                                voxelDeltaSnapshot = default;
415-                                if (snapshotPauseActive)
416-                                {
417-                                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
418-                                    snapshotPauseActive = false;
419-                                }
420-
421-                                ReportPersistenceCleanupFailure("save", cleanupException);
422:                                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
423-                                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
424-                                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
425-                                DumpSaveBlackBox();
426-                                RecordFailure(slotName, "save", reason);
427-                                LastOperationError = reason;
428-                                LogError(logReason);
429-                                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
430-                                return;
431-                            }
432-
433-                            voxelDeltaSnapshot = default;
434-                        }
435-                        else
436-                        {
437-                            borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;
438-                        }
439-
440-                    }
441-
442-                    saveable.PopulateSaveData(data);
443-                }
444-
445-                StampRuntimeWorldSeed(data);
446-                StampProceduralTerrainIdentity(data);
447-                ModSaveStateStore.PopulateSaveData(data);
448-                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();
449:                if (persistentWorldRegistry != null)
450-                {
451:                    if (!persistentWorldRegistry.CaptureSaveSnapshot())
452-                    {
453-                        const string reason = "Persistent world save snapshot capture failed.";
454-                        const string logReason = "[SaveManager] Save failed: persistent world save snapshot capture failed.";
455-                        const uint failureCode = 3u;
456-                        Exception cleanupException = null;
457-                        if (snapshotPauseActive)
458-                        {
459-                            ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
460-                            snapshotPauseActive = false;
461-                        }
462-
463-                        ReportPersistenceCleanupFailure("save", cleanupException);
464:                        RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
465-                        PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
466-                        PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
467-                        DumpSaveBlackBox();
468-                        RecordFailure(slotName, "save", reason);
469-                        LastOperationError = reason;
470-                        LogError(logReason);
471-                        SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
472-                        return;
473-                    }
474-
475:                    int persistentWorldSnapshotCapacity = persistentWorldRegistry.SaveSnapshotCapacity;
476-                    if (persistentWorldSnapshotCapacity > 0)
477-                    {
478-                        persistentWorldDeltaSnapshotOwner = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
479-                            persistentWorldSnapshotCapacity,
480-                            Allocator.Persistent,
481-                            NativeArrayOptions.UninitializedMemory,
482-                            "persistentWorldDeltaSnapshotOwner");
483-
484:                        if (!persistentWorldRegistry.TryCopySaveSnapshotDeltas(
485-                            persistentWorldDeltaSnapshotOwner,
486-                            persistentWorldSnapshotCapacity,
487-                            out int copiedPersistentWorldDeltas))
488-                        {
489-                            Exception cleanupException = null;
490-                            DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
491-                            const string reason = "Persistent world save snapshot copy failed.";
492-                            const string logReason = "[SaveManager] Save failed: persistent world save snapshot copy failed.";
493-                            const uint failureCode = 3u;
494-                            if (snapshotPauseActive)
495-                            {
496-                                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
497-                                snapshotPauseActive = false;
498-                            }
499-
500-                            ReportPersistenceCleanupFailure("save", cleanupException);
501:                            RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
502-                            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
503-                            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
504-                            DumpSaveBlackBox();
505-                            RecordFailure(slotName, "save", reason);
506-                            LastOperationError = reason;
507-                            LogError(logReason);
508-                            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
509-                            return;
510-                        }
511-
512-                        if (copiedPersistentWorldDeltas > 0)
513-                        {
514-                            NativeArray<PersistentWorldDeltaRecord> copiedView = copiedPersistentWorldDeltas < persistentWorldDeltaSnapshotOwner.Length
515-                                ? persistentWorldDeltaSnapshotOwner.GetSubArray(0, copiedPersistentWorldDeltas)
516-                                : persistentWorldDeltaSnapshotOwner;
517-                            persistentWorldDeltaSnapshot = copiedView.AsReadOnly();
518-                        }
519-                    }
520-                }
521-
522:                EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
523-                if (ecosystemDirector != null)
524-                {
525-                    ecosystemDirector.CaptureSaveSnapshot();
526-                    NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
527-                    if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
528-                    {
529-                        ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
530-                            ecosystemRecordCount,
531-                            Allocator.Persistent,
532-                            NativeArrayOptions.UninitializedMemory,
533-                            "ecosystemSectorSnapshotOwner");
534-
535-                        for (int i = 0; i < ecosystemRecordCount; i++)
536-                            ecosystemSectorSnapshotOwner[i] = ecosystemView[i];
537-
538-                        ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
539-                    }
540-                }
541-
542-                divergenceSnapshotTimer.Stop();
543-                long saveTimestampTicks = DateTime.UtcNow.Ticks;
544:                QuestManager questManager = GlobalRegistry.Quest;
545-                if (questManager != null)
546-                {
547-                    int packedQuestWordCount = questManager.PackedStateWordCount;
548-                    if (packedQuestWordCount > 0)
549-                    {
550-                        packedQuestStateSnapshot = CreateTransientNativeArray<uint>(
551-                            packedQuestWordCount,
552-                            Allocator.Persistent,
553-                            NativeArrayOptions.ClearMemory,
554-                            "packedQuestStateSnapshot");
555-
556-                        bool copiedQuestState;
557-                        unsafe
558-                        {
559-                            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateSnapshot);
560-                            copiedQuestState = questManager.TryCopyPackedStateSnapshot(
561-                                destinationPtr,
562-                                packedQuestStateSnapshot.Length,
563-                                out packedQuestSaveHeader,
564-                                saveTimestampTicks);
565-                        }
566-
567-                        if (!copiedQuestState)
568-                            DisposeTransientNativeArrayBestEffortAndReport(ref packedQuestStateSnapshot, "save", "packedQuestStateSnapshot");
569-                    }
570-                }
571-
572-                RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.ExtractPlayerDialogueChoiceFlags(packedQuestStateSnapshot));
573-                ushort playerDialogueChoiceFlagsSnapshot = PlayerDialogueChoiceFlags;
574-
575-                SaveMetadata metadata = new SaveMetadata
576-                {
577-                    SlotName = slotName,
578-                    GameVersion = Application.version,
579-                    Timestamp = saveTimestampTicks,
580-                    PlayTimeSeconds = (float)playTime,
581-                    SceneName = SaveMetadata.NormalizeSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
582-                    PlayerPosition = data.playerStats.GetPosition(),
583-                    WorldSeed = data.ecosystemState.worldSeed,
584-                    WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
585-                };
586-
587-                snapshotTimer.Stop();
588-                StageSnapshotHeader(operationId, slotName, persistentWorldDeltaSnapshot, ecosystemSectorSnapshot, packedQuestStateSnapshot, voxelDeltaSnapshot);
589-                Exception snapshotPauseReleaseException = null;
590-                ReleaseSnapshotPauseBestEffort(operationId, ref snapshotPauseReleaseException);
591-                snapshotPauseActive = false;
592-                ReportPersistenceCleanupFailure("save", snapshotPauseReleaseException);
593-                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);
594-
595-                int backupRetention = GetBackupRetentionCount(slotName);
596-                string tempPath = GetTempSaveFilePath(slotName);
597-                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
598-                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
599-
600-                await Awaitable.MainThreadAsync();
601-                SaveContextFrameData frameData = SaveContextFrameData.CaptureMainThread();
602-                SaveEvents.TryRaiseMappedWriteStarted(SaveEvents.ComputeSlotHash(slotName));
603-                await Awaitable.BackgroundThreadAsync();
604-
605-                ulong payloadHash64;
606-                int rawPayloadLength;
607-                long compressionPipelineStartTicks = Stopwatch.GetTimestamp();
608-
609-                if (!TryExecuteVerifiedSavePipeline(
610-                    slotName,
611-                    tempPath,
612-                    GetPrimarySaveFilePath(slotName),
613-                    metadata,
614-                    data,
615-                    persistentWorldDeltaSnapshot,
616-                    ecosystemSectorSnapshot,
617-                    packedQuestSaveHeader,
618-                    packedQuestStateSnapshot,
619-                    playerDialogueChoiceFlagsSnapshot,
620-                    voxelDeltaSnapshot,
621-                    _savePayloadBuffer,
622-                    _compressedSaveBuffer,
623-                    backupRetention,
624-                    out payloadHash64,
625-                    out rawPayloadLength,
626-                    out long compressedSizeBytes,
627-                    out string savePipelineError))
628-                {
629-                    await Awaitable.MainThreadAsync();
630-                    const uint failureCode = 3u;
631-                    string failureMessage = string.IsNullOrEmpty(savePipelineError)
632-                        ? "Verified save pipeline failed."
633-                        : savePipelineError;
634:                    RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
635-                    PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
636-                    PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
637-                    DumpSaveBlackBox();
638-                    RecordFailure(slotName, "save", failureMessage);
639-                    LastOperationError = failureMessage;
640-                    LogError("[SaveManager] Save failed: " + failureMessage);
641-                    SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(failureMessage), failureMessage);
642-                    return;
643-                }
644-
645-                long compressionPipelineElapsedTicks = Stopwatch.GetTimestamp() - compressionPipelineStartTicks;
646-                await Awaitable.MainThreadAsync();
647-                RegisterCompressionPipelineElapsed(compressionPipelineElapsedTicks, in frameData);
648-                SaveThumbnailSystem.CaptureCompletion thumbnailCompletion =
649-                    await SaveThumbnailSystem.WaitForCompletionAsync(thumbnailTicket, destroyCancellationToken);
650:                RecordAsyncPersistenceTelemetry(
651-                    operationId,
652-                    slotName,
653-                    totalTimer.ElapsedMilliseconds,
654-                    compressedSizeBytes,
655-                    rawPayloadLength,
656-                    thumbnailCompletion.ByteLength,
657-                    thumbnailCompletion.Succeeded != 0 ? 0u : 2u);
658-                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: compressedSizeBytes, succeeded: true));
659-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Completed, 1f, 0u);
660-                StageIntegrityPayload(_savePayloadBuffer, rawPayloadLength, payloadHash64, slotName);
661-                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
662-                    ? SaveSlotIntegrityState.HealthyWithBackup
663-                    : SaveSlotIntegrityState.Healthy;
664-                RecordSuccessfulSave(slotName, data.version, savedIntegrity);
665-                NotifyMappedInventoryWritesCommitted();
666-
667-                LastOperationSucceeded = true;
668-                LogInfo($"[SaveManager] Saved '{slotName}' (XXH3-64: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
669-                RaiseSaveCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));
670-                PublishSaveSynchronizedNotification(slotName);
671-            }
672-            catch (Exception ex)
673-            {
674-                await Awaitable.MainThreadAsync();
675-                if (snapshotPauseActive)
676-                {
677-                    Exception cleanupException = null;
678-                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
679-                    snapshotPauseActive = false;
680-                    ReportPersistenceCleanupFailure("save", cleanupException);
681-                }
682-
683:                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, 1u);
684-                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
685-                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
686-                DumpSaveBlackBox();
687-                RecordFailure(slotName, "save", ex.Message);
688-                LastOperationError = ex.Message;
689-                LogError("[SaveManager] Save failed: " + ex);
690-                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(ex.Message), ex.Message);
691-            }
692-            finally
693-            {
694-                Exception cleanupException = null;
695-
696-                if (snapshotPauseActive)
697-                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
698-
699-                if (packedQuestStateSnapshot.IsCreated)
700-                    DisposeTransientNativeArrayBestEffort(ref packedQuestStateSnapshot, ref cleanupException, sentinelLabel: "packedQuestStateSnapshot");
701-
702-                if (persistentWorldDeltaSnapshotOwner.IsCreated)
703-                    DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
704-
705-                if (ecosystemSectorSnapshotOwner.IsCreated)
706-                    DisposeTransientNativeArrayBestEffort(ref ecosystemSectorSnapshotOwner, ref cleanupException, sentinelLabel: "ecosystemSectorSnapshotOwner");
707-
708-                if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
709-                    DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
710-
711-                if (borrowedVoxelDeltaSnapshotOwner != null)
712-                {
713-                    ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
714-                    borrowedVoxelDeltaSnapshotOwner = null;
715-                }
716-
717-                _isBusy = false;
718-                NotifyMacroDatabasePersistenceGateBestEffort(false, ref cleanupException);
719-                ReportPersistenceCleanupFailure("save", cleanupException);
720-            }
721-        }
