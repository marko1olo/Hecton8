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
