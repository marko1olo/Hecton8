# LOG_SAVE_METADATA_ARCHIVIST

## 2026-05-13 - Async Save Screenshot Metadata

What was wrong:
- Save completion/HUD notification could report success before thumbnail metadata was complete.
- Low-tier capture still risked stale screenshot state instead of explicit zero-byte metadata.
- Load-game UI path was synchronous at the slot widget boundary and could block scroll on disk read.
- Existing binary save v9 header has no thumbnail length/offset field; direct injection would corrupt the reader.
- Unity/dotnet verification is blocked by unrelated generated-project assembly failures.

What was done:
- Save flow now captures a thumbnail ticket and waits for completion before `SaveCompletedSignal`, `SaveEvents.RaiseSaveCompleted`, and synchronized HUD notification.
- `SaveThumbnailCaptureFeature.Dispose()` now cancels pending/inflight screenshot tickets so scene unload does not leave save completion waiting.
- Low/MX350 tier skips screenshot capture, purges stale sidecar thumbnail best-effort, and emits zero-byte metadata.
- `SaveSlotThumbnail` now loads the visible slot asynchronously, reads bytes off the main thread, and updates the existing `RawImage`.
- Corrupt decode fallback uses a cached static-noise texture uploaded through `Texture2D.LoadRawTextureData()` and `Apply()`.
- Reused-thumbnail metadata now uses byte length plus last-write ticks instead of reading the JPG body.
- `PersistenceUxSmokeTester` string probes were updated for the async thumbnail load method.
- Status and rationale files were updated from disk state, not chat memory.

Cinematic Cheats used:
- 256x144 capture target instead of full-resolution screenshot.
- MX350/Low tier zero-byte screenshot path.
- Deterministic static-noise fallback instead of expensive recovery/decode attempts.
- Length/timestamp metadata hash for reused thumbnails instead of full content hash.

Exact Microseconds saved:
- Historical target: remove stated ~150 ms synchronous readback/PNG stall.
- Reused-thumbnail byte-scan removal: expected O(file size) disk-scan avoidance; measured microseconds PENDING.
- Async UI scroll load: expected main-thread disk-read removal for visible slot loads; measured microseconds PENDING.
- Runtime/profiler proof is blocked: Unity MCP reports no session, and `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` fails on unrelated missing assemblies/contracts before local screenshot validation.

Blocked items:
- Task 3: no `Hecton8.Core.Persistence.Metadata` source/asmdef exists to move safely while generated project compilation is already red.
- Task 9: binary header injection is blocked by current save v9 layout lacking thumbnail length/offset fields.
- Task 10: primary JPG sidecar decode cannot use `LoadRawTextureData()` until Task 9 provides a raw/BC1 payload contract; only fallback texture uses raw load today.
- Task 19: scene unload guard is implemented, but compile/runtime leak proof is blocked by global dependencies and unavailable Unity session.

Final diff surface:
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
- `Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs`
- `Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs` is dirty with existing inventory/acoustic lane edits in addition to the verified metadata signal surface; no unrelated edits were reverted.
- `Docs/Tasks/Status_SAVE_METADATA_ARCHIVIST.md`
- `Docs/AgentLogs/Rationale_SAVE_METADATA_ARCHIVIST.md`

Status:
- PENDING VERIFICATION. Compile/runtime proof is blocked by dependency failures, not claimed.

## 2026-05-13 - Hardening Pass: Completion Ownership

What was wrong:
- `SaveThumbnailCapture` still listened to `SaveStarted`, creating a duplicate capture path after `SaveManager` had already issued the authoritative save-owned ticket.
- `SaveThumbnailSystem` retained only one last completion; a later operation-zero/manual completion could overwrite an immediate save completion before `WaitForCompletionAsync` joined it.
- Hidden/reused save-slot UI widgets could still accept a late async texture if disabled but not destroyed.

What was done:
- Replaced single thumbnail completion storage with a fixed 8-entry completion ring.
- Suppressed `SaveMetadataReadySignal` for operation-zero/manual captures while still retaining their completion state.
- Removed `ISaveEventListener` and `SaveEvents.Register(this)` from `SaveThumbnailCapture`; it is now a sanitized manual trigger only.
- Added `SaveSlotThumbnail.OnDisable` sequence advancement to reject stale async thumbnail results.
- Updated `PersistenceUxSmokeTester` probes to assert the completion ring, operation-id gate, listener removal, and async thumbnail load sequence.

Cinematic Cheats used:
- No extra capture to solve the race; ownership was tightened instead of adding a second synchronization layer.
- Low-tier still skips screenshot bytes; the hardening prevents the skip completion from being overwritten by UI noise.

Exact Microseconds saved:
- Worst-case prevented wait: 90 frames, approximately 1,500,000 us at 60 Hz.
- Duplicate readback/encode avoided: estimated 700-2,000 us on i3/MX350-class hardware, profiler proof PENDING.
- Static checks passed: no `SaveEvents.Register(this)`, `ISaveEventListener`, or `SaveEvents.TryResolveKnownSlotName` remains in `SaveThumbnailCapture`.
- Compile remains BLOCKED by the existing project dependency wall. Full build reports 113 unrelated missing contract/namespace errors; touched-file compile-output filter returned no matches.
- Unity MCP validation remains BLOCKED: `validate_script Assets/_Project/Scripts/SaveThumbnailSystem.cs` returned `no_unity_session`.

Final diff surface for this pass:
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
- `Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs`
- `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
- `Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs`
- `Docs/Tasks/Status_SAVE_METADATA_ARCHIVIST.md`
- `Docs/AgentLogs/Rationale_SAVE_METADATA_ARCHIVIST.md`
- `Docs/AgentLogs/LOG_SAVE_METADATA_ARCHIVIST.md`

Status:
- PENDING VERIFICATION. No runtime/profiler claim made.

## 2026-05-13 - Hardening Pass: Terminal Tickets and Cancellation

What was wrong:
- Terminal reused-thumbnail tickets could lose byte count/hash if the completion ring was churned before `WaitForCompletionAsync` joined.
- Cancellation during `AwaitableDebtMonitor.NextFrameAsync` could escape the thumbnail wait before a cancelled completion was recorded.
- Thumbnail write state was cleared after completion publication, so a reentrant signal consumer could see the writer as still active.

What was done:
- Added byte length/hash fields to `CaptureTicket` and used them in terminal fallback completion.
- Caught `OperationCanceledException` inside the thumbnail wait loop, cleared matching pending/inflight state, and returned a cancelled completion.
- Added `ReleaseWriteInProgress()` and call it before `CompleteRequest(completion)`.
- Updated smoke probes to assert terminal ticket metadata, cancellation handling, and write-state release ordering.

Cinematic Cheats used:
- Preserved the cheap reused-thumbnail metadata path instead of scanning the JPG body again.
- Kept cancellation deterministic and bounded rather than adding a blocking GPU wait.

Exact Microseconds saved:
- Prevented worst-case cancellation/stale-writer wait: 90 frames, approximately 1,500,000 us at 60 Hz.
- Reused-thumbnail metadata fallback avoids returning to full byte scans; measured microseconds remain PENDING.
- Touched-file compile-output filter returned no errors for `SaveThumbnailSystem`, `SaveThumbnailCapture`, `SaveSlotThumbnail`, or `PersistenceUxSmokeTester`.
- Unity MCP validation remains BLOCKED: `validate_script Assets/_Project/Scripts/SaveThumbnailSystem.cs` returned `no_unity_session`.

Final diff surface for this pass:
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
- `Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs`
- `Docs/Tasks/Status_SAVE_METADATA_ARCHIVIST.md`
- `Docs/AgentLogs/Rationale_SAVE_METADATA_ARCHIVIST.md`
- `Docs/AgentLogs/LOG_SAVE_METADATA_ARCHIVIST.md`

Status:
- PENDING VERIFICATION. Global compile/runtime proof remains blocked by existing project dependency failures and unavailable Unity session.
