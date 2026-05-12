# HECTON-8 Deterministic Replay Status

AgentID: HECTON-8
Domain: CORE & MEMORY INFRASTRUCTURE / Deterministic Replay & Fault Inquisitor
Assignment Source: Chat master prompt. `Docs/Tasks/CURRENT_BATCH.txt` was empty; no XML prompt block existed to extract.
Status: PENDING VERIFICATION

## Loop 1 - Tasks 1-5

- [x] 1. DOD Snapshot System | DOD practice: `NativeMemorySentinel.CopySnapshotSources` exposes pointer-backed native buffers; `DodReplayRecorder` stages segment headers + payload bytes with guarded `UnsafeUtility.MemCpy` path. Alternative rejected: managed serialization/reflection. Estimate: target <100 us when unchanged, PENDING MEASUREMENT.
- [x] 2. Input Journaling | DOD practice: `DodReplayInputEvent` is a 64-byte binary ring with `double PrecisionTimestamp`; caller API records hashed device/control/phase. Alternative rejected: string input logs. Estimate: target <10 us/event, PENDING MEASUREMENT.
- [x] 3. Seed Synchronization | DOD practice: `DeterministicReplaySeed.ComposeSeed(sessionSeed, currentFrameIndex, subjectHash, streamHash)` includes `currentFrameIndex` in LCG seed. Alternative rejected: `UnityEngine.Random` global state. Estimate: <1 us/seed, PENDING MEASUREMENT.
- [x] 4. Fault Interception | DOD practice: `MathGuard.DrainInvalidNumberErrors` publishes numeric telemetry and requests replay full-state dump. Alternative rejected: exceptions or Debug.Log-only fault reporting. Estimate: fault path only, PENDING MEASUREMENT.
- [x] 5. Time Scrubber | DOD practice: editor-only UI Toolkit scrubber indexes `replay.bin` headers and scrubs frame/segment/fault metadata. Alternative rejected: runtime Canvas UI. Estimate: editor-only, no runtime frame cost.

## Loop 2 - Tasks 6-10

- [x] 6. Snapshot Comparer | DOD practice: editor comparer loads adjacent snapshot payloads, matches OwnerHash/LabelHash, and reports first differing byte offset. Alternative rejected: runtime visual comparer before replay data validation. Estimate: editor-only, no runtime frame cost; full project verification blocked by unrelated Core errors.
- [ ] 7. Burst Panic Capture | PENDING.
- [ ] 8. Replay Overlay | PENDING.
- [x] 9. Telemetry SubjectHash | DOD practice: snapshot header stores `SubjectHash` and `ErrorCode`; MathGuard uses numeric subject hash. Alternative rejected: string system names in dump. Estimate: no measurable added hot-path cost, PENDING MEASUREMENT.
- [ ] 10. AUP Drift Detector | PENDING.

## Loop 3 - Tasks 11-15

- [ ] 11. Zero-GC Frame Profiler | PENDING.
- [ ] 12. Entity Ghosting | PENDING.
- [ ] 13. Logistic Flow Debugger | PENDING.
- [ ] 14. Atmosphere Pressure Map | PENDING.
- [ ] 15. VRAM Allocation Tracker | PENDING.

## Loop 4 - Tasks 16-20

- [x] 16. Circular MMF Buffer | DOD practice: `replay.bin` is sized to 499 MB and wraps `_writeOffset` to zero before overrun. Alternative rejected: unbounded dump files. Estimate: disk bound, PENDING MEASUREMENT.
- [ ] 17. Remote Debug Command | PENDING.
- [ ] 18. Physics Determinism Smoke Test | PENDING.
- [ ] 19. Voxel SDF Integrity Scan | PENDING.
- [ ] 20. Audio Event Heatmap | PENDING.

## Loop 5 - Tasks 21-25

- [x] 21. Background Thread Priority | DOD practice: new replay writer and existing blackbox/telemetry threads use `ThreadPriority.Lowest`. Alternative rejected: default/normal writer priority. Estimate: protects frame thread; exact microseconds PENDING MEASUREMENT.
- [x] 22. Delta Compression via math.select | DOD practice: segment changed/unchanged payload selection uses `math.select`; unchanged payload bytes are skipped. Alternative rejected: always-copy segment spam. Estimate: saves disk bandwidth proportional to unchanged arrays, PENDING MEASUREMENT.
- [x] 23. uint32 ErrorCodes | DOD practice: replay fault path stores `uint ErrorCode`; MathGuard casts deterministic int codes to uint. Alternative rejected: string error messages. Estimate: fault path only.
- [x] 24. Clean Replay Cyrillic Comments | DOD practice: scanned new replay runtime/editor files for Cyrillic code comments; none found. Alternative rejected: bulk modifying unrelated comments. Estimate: no runtime impact.
- [x] 25. SnapshotHeader 128 Bytes | DOD practice: `DodReplaySnapshotHeader` uses `[StructLayout(LayoutKind.Explicit, Size = 128)]` and runtime size check. Alternative rejected: variable managed headers. Estimate: parse-stable, PENDING MEASUREMENT.

## Loop 6 - Tasks 26-30

- [ ] 26. Save-Game Validation | PENDING.
- [ ] 27. Network Jitter Simulation | PENDING.
- [ ] 28. Entity Death Log | PENDING.
- [ ] 29. Global State Reset | PENDING.
- [x] 30. Generate .meta Files | DOD practice: `.meta` files generated for new replay scripts and editor scrubber. Alternative rejected: orphan scripts without GUIDs. Estimate: editor import stability only.

## Verification

- Compile loop 1: PASS - `dotnet build Hecton8.Core.csproj` and `dotnet build Hecton8.Editor.csproj`, 0 warnings, 0 errors.
- Compile loop 2 editor syntax: PASS - `dotnet build Hecton8.Editor.csproj --no-dependencies`, 0 warnings, 0 errors.
- Compile loop 2 full project: BLOCKED BY DEPENDENCY - later full builds fail in dirty, unrelated files `Assets/_Project/Scripts/World/GPUScatterDirector.cs` and `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`.
- Unity Console: PENDING.
- PlayMode / GCMonitor: PENDING.
