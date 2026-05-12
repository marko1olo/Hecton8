# HECTON-8 Deterministic Replay Rationale

Status: PENDING VERIFICATION

## Assignment Binding

Problem: The repository batch file `Docs/Tasks/CURRENT_BATCH.txt` exists but is empty, so no `<AGENT_PROMPT>` block can be extracted.
Solution: Treat the chat master prompt as the operative assignment and record that fact in task state.
Rejected Alternatives: Guessing a neighboring batch prompt was rejected because AGENTS forbids cross-prompt contamination. Waiting for a hidden ID was rejected because the user ordered work now and the prompt supplied a clear 30-task domain.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state only.
Hardware Impact: No runtime impact.

## Replay Ownership Boundary

Problem: HECTON-8 already has `CrashTelemetryBuffer`, `GlobalTelemetryBus`, `MathGuard`, `NativeMemorySentinel`, and `SystemDispatcher`; a second concrete crash stack could become a god object or cross-domain dependency.
Solution: Add a narrow deterministic replay layer that reads native allocation metadata from `NativeMemorySentinel`, stages bytes into preallocated native scratch, and writes to `replay.bin` on a lowest-priority background thread.
Rejected Alternatives: Reflection over every subsystem was rejected because it allocates and breaks Burst/DOD ownership. Direct dependencies on fauna/physics/logistics concrete classes were rejected because 20+ agents may be editing them and AGENTS requires registry/event decoupling. Synchronous file writes in LateFrame were rejected because they can stall the main thread.
Scalability potential: Low uses interval snapshots and delta skip; Middle can increase staged bytes; High can retain more segment headers; Ultra can add replay overlay and comparer density once verified.
Hardware Impact: On i3/MX350 the unchanged segment path is expected to save disk bandwidth and avoid write stalls; exact gain is PENDING MEASUREMENT.

## Snapshot Fidelity Tradeoff

Problem: The prompt demands all active `NativeArrays` every 10 frames while debug mandate caps memory and frame overhead.
Solution: Capture pointer-backed persistent/session/scene NativeArrays discovered by the sentinel into a fixed scratch buffer, mark truncated bytes when capacity is exceeded, and keep a 500 MB circular MMF cap.
Rejected Alternatives: Unlimited scratch allocation was rejected because debug memory budget is finite. Capturing pointerless NativeHashMap internals was rejected because Unity does not expose stable backing pointers through the existing sentinel.
Scalability potential: Low keeps the scratch cap conservative; High/Ultra can increase scratch through quality/debug settings after profiler proof.
Hardware Impact: Avoids unbounded native memory pressure on MX350-class machines. Exact microseconds saved are PENDING MEASUREMENT.

## Editor Scrubber and Comparer

Problem: Replay data needs inspection without adding runtime Canvas/UI cost or string formatting to hot paths.
Solution: Add an editor-only UI Toolkit window that indexes `replay.bin`, scrubs snapshot headers, and compares adjacent payload bytes by OwnerHash/LabelHash.
Rejected Alternatives: Runtime uGUI slider was rejected because it adds gameplay UI rebuild risk. Full in-game wire overlay was rejected for this loop because it requires render-scene wiring and fresh visual verification.
Scalability potential: Low devices pay zero runtime cost; high-end devices can later use the same binary data for replay overlay and byte-diff visualizations.
Hardware Impact: Editor-only. No MX350 runtime cost.

## Generated Project Files

Problem: Unity-generated `.csproj` files did not refresh after new scripts were added, so local `dotnet build` could not see the new source files.
Solution: Add the new script compile includes to `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` for local verification.
Rejected Alternatives: Waiting for Unity regeneration was rejected because this turn needed compiler evidence. Moving all code into pre-existing files was rejected because it would create ownership noise and larger fragile files.
Scalability potential: No runtime effect; project file changes are build tooling only and may be overwritten by Unity regeneration.
Hardware Impact: No runtime impact.

## Compile Wall After Loop 2

Problem: After the replay code compiled once, later builds failed in dirty unrelated files: `GPUScatterDirector.cs` missing `TryAutoAssignAssets`/`ReleaseDepthPyramidTexture`, and `PlayerCriticalProceduralAudioRenderer.cs` missing a visible `BakeCaveConvolutionImpulseResponse` under current compile conditions.
Solution: Stop cross-domain patching and mark verification blocked by dependency for Loop 2.
Rejected Alternatives: Editing GPU scatter/audio systems was rejected because the deterministic replay prompt does not own those domains and those files have active unrelated modifications.
Scalability potential: No replay scalability impact.
Hardware Impact: No replay runtime impact; build verification remains PENDING until those unrelated compile errors are resolved.
