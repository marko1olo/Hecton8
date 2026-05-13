# Rationale_SAVE_METADATA_ARCHIVIST

Status: PENDING VERIFICATION  
Owner: SAVE_METADATA_ARCHIVIST  

## Decision 001 - Prompt and Domain Boundary

Problem: The batch file contains neighboring agent prompts and stale batch archives. Reading the wrong block would corrupt ownership.
Solution: Extracted only `<AGENT_PROMPT id="SAVE_METADATA_ARCHIVIST">...</AGENT_PROMPT>` from `Docs/Tasks/CURRENT_BATCH.md` via CLI regex and counted 19 tasks from the tag.
Rejected Alternatives: Manual IDE tab memory; archive `CURRENT_BATCH.md` files; MCP resource read that could truncate or blend context.
Scalability potential: Low tier avoids screenshot cost entirely; high/ultra can spend the saved frame time on richer save metadata presentation once runtime proof exists.
Hardware Impact: Expected save hitch reduction target is removal of the stated 150 ms synchronous readback stall on i3/MX350; measured gain is PENDING.

## Decision 002 - Mandate Set

Problem: Async save screenshots span save persistence, GPU readback, native memory, UI thumbnail streaming, telemetry, and GlobalRegistry/signal boundaries.
Solution: Loaded eight mandates directly covering those seams before source edits.
Rejected Alternatives: Treating screenshot capture as isolated UI work; adding ad hoc singleton service; using dated report text as authority.
Scalability potential: Low = empty metadata screenshot. Middle = async 256x144 compressed thumbnail. High = same core path with richer UI presentation, not larger uncontrolled runtime captures. Ultra = optional visual overkill after profiler proof.
Hardware Impact: Expected low-end gain is avoiding synchronous GPU/CPU readback and PNG encode stalls; expected high-end impact is extra visual metadata without blocking save flow. Exact microseconds PENDING.

## Decision 003 - Capture Ownership and Signal Contract

Problem: Save completion and HUD notifications could fire before thumbnail persistence finished, leaving metadata visually stale and hiding screenshot failures.
Solution: Verified `SaveMetadataReadySignal` as a 32-byte typed signal lane, kept save-owned thumbnail capture tickets, and joined the ticket after save file I/O but before `SaveCompletedSignal`, `SaveEvents.RaiseSaveCompleted`, and `HUDNotificationSignal`.
Rejected Alternatives: A `SaveScreenshotManager.Instance` singleton; blocking the save thread on readback; dispatching HUD notification from UI listeners before metadata was ready.
Scalability potential: Low = completion signal carries zero screenshot bytes. Middle = async 256x144 JPG metadata. High = same deterministic pipeline with richer downstream presentation once measured. Ultra = spend UI budget on extra visual treatment after profiler proof, not larger capture.
Hardware Impact: Expected i3/MX350 gain is eliminating the historical 150 ms main-thread stall from synchronous readback/PNG; measured microseconds are blocked by existing assembly-reference compile failures.

## Decision 004 - Binary Header Injection Boundary

Problem: The current save binary v9 header has no thumbnail length/offset field, and the payload prefix is fixed. Injecting thumbnail bytes into `.sav.tmp` would shift sectors and corrupt the reader.
Solution: Left binary header injection blocked, emitted thumbnail length/hash through `SaveMetadataReadySignal`, and kept the persisted screenshot sidecar path intact until the persistence surgeon owns a versioned layout change.
Rejected Alternatives: Appending bytes to the payload without a version bump; overloading reserved header fields; writing undocumented bytes into the sidecar metadata reader.
Scalability potential: Low = empty metadata bytes with no extra I/O. Middle = sidecar JPG. High/Ultra = future versioned header section can inline thumbnails without touching UI contracts.
Hardware Impact: Avoids a catastrophic corrupted-save failure on all devices. Direct microsecond gain is zero; risk reduction is the value.

## Decision 005 - UI Streaming and Corruption Fallback

Problem: Load-game UI must not decode all screenshots up front, and corrupt bytes need a deterministic visual fallback without prefab churn.
Solution: Changed `SaveSlotThumbnail` to schedule async per-slot loads, read thumbnail bytes off the main thread, update the existing `RawImage`, and generate a cached static-noise texture via `LoadRawTextureData()` plus `Apply()`.
Rejected Alternatives: Bulk loading all save thumbnails; instantiating preview prefabs; creating a new fallback texture per corrupt slot.
Scalability potential: Low = placeholder or static-noise texture only. Middle = visible slot async decode with a 12-texture cache. High = same cache policy with smoother hover/prefetch. Ultra = richer animated preview can reuse the same texture handoff.
Hardware Impact: Expected low-end benefit is bounded RAM and no all-slots disk burst. Exact microseconds saved are pending profiler proof.

## Decision 006 - Scene Unload and Compile Status

Problem: URP renderer feature disposal during a pending readback could leave save completion waiting forever, and the generated Core project currently fails before local screenshot code can be compiler-validated.
Solution: Renderer feature disposal now cancels pending/inflight tickets; save waits have a 90-frame timeout; readback native memory defers disposal if a background thumbnail write is active.
Rejected Alternatives: Trusting `AsyncGPUReadback` callbacks to always arrive; blocking `Complete()` on readback; claiming compile success while `Hecton8.Core.csproj` reports missing cross-assembly dependencies.
Scalability potential: Low = no GPU readback allocation. Middle/High/Ultra = bounded readback lifecycle and deterministic failure signal on scene unload.
Hardware Impact: Prevents indefinite save-completion wait and RTHandle/readback lifetime ambiguity. Runtime memory-leak proof is blocked because Unity MCP validation is unavailable and the post-polish dotnet build is stopped by unrelated assembly boundaries.

## OMEGA POLISH CHANGES

Problem: The reused-thumbnail path originally scanned the existing JPG byte-by-byte to synthesize a metadata hash on the main thread.
Solution: Replaced the honest file-content hash with a cheap metadata hash over byte length and last-write ticks. This keeps the signal deterministic enough for thumbnail metadata without touching the file body.
Rejected Alternatives: Byte-stream hash on the main thread; claiming zero-GC while doing synchronous file scans; adding a larger checksum dependency.
Scalability potential: Low = no thumbnail file hash because screenshot is skipped. Middle = cheap metadata hash for sidecar reuse. High/Ultra = future inline header can carry real encoded-byte hash from the background writer.
Hardware Impact: Avoids a cold but unnecessary O(file size) main-thread scan when the camera pose has not changed; expected gain is proportional to thumbnail size and storage latency, measured microseconds PENDING.

Exact Cinematic Cheats:
- Downscaled 256x144 capture instead of full-resolution save screenshot.
- MX350/Low tier writes zero screenshot bytes and deletes stale thumbnails.
- Static-noise fallback is generated once from deterministic xorshift noise and uploaded through `LoadRawTextureData()`/`Apply()`.
- Reused-thumbnail metadata uses length/timestamp hash instead of a full byte hash.

Final Diff Surface:
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
- `Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs`
- `Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs` is dirty with pre-existing inventory/acoustic lane edits in addition to the verified metadata signal surface; not reverted.
