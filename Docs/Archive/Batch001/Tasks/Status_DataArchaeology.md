# DataArchaeology Status

Prompt: Add lore discovery system / Discovery & Data Archaeology
Domain: Echelon 8 Presentation & UX, Frequency Tuning (Scanning)
Task count: 30
Status source: local code inspection and `dotnet build`; Unity Editor/GCMonitor verification unavailable in this tool session.

## Loop 1 - Tasks 1-5

- [x] Task 1 Frequency Tuning Mini-Game | DOD: Burst-compatible math kernel, no `math.sin/cos`, exact `math.abs(signal - noise) < threshold` gate. Rejected: honest sine in scanner hot path. Estimate: ~3-6 us saved per active scan tick versus trigonometric sine pair.
- [x] Task 2 Holographic Reconstruction | DOD: `Graphics.DrawMeshInstanced` using existing URP wire shader. Rejected: runtime mesh reconstruction physics. Estimate: ~80-300 us saved per completion event versus CPU mesh rebuild.
- [x] Task 3 Artifact Fragment Hashing | DOD: `NativeParallelHashMap<uint, float3>` plus MMF cold persistence. Rejected: string IDs and managed dictionaries in scan path. Estimate: ~2-8 us saved per lookup and 0 B GC.
- [x] Task 4 Lore Data Striping | DOD: `LoreMmfEncyclopedia.TryLoadEntryUtf16` only from read API. Rejected: preloading all PDA text. Estimate: RAM residency avoided; CPU saved only when PDA closed.
- [x] Task 5 Encyclopedia Unlock | DOD: `uint` FNV-1a discovery hash and bitset unlock. Rejected: string-key scan lookup. Estimate: ~1-4 us saved per unlock path and no string allocation.

## Loop 2 - Tasks 6-10

- [x] Task 6 Scanner Range Scaling | DOD: range now `baseRange * (1 + saturate(battery))`, then corrosion multiplier. Rejected: condition-only range. Estimate: no cost change.
- [x] Task 7 Discovery Notifications | DOD: fixed `NativeArray` ring queue, 32 entries, no allocation. Rejected: managed queue/list. Estimate: 0 B GC; ~1 us saved during burst notifications.
- [x] Task 8 Corrupted Log Recovery | DOD: Jacobi 4-lane relaxation helper in Burst-safe kernel. Rejected: managed mini-game state allocation. Estimate: source-present kernel only; runtime UI pending.
- [x] Task 9 Sensory Feedback | DOD: match closeness emits existing `PlayerSignalEvents` and `ToolHapticsRuntime` on 0.1 s gate. Rejected: per-frame audio source spawn. Estimate: 0 B GC; ~50+ us avoided versus AudioSource path.
- [ ] Task 10 Biome Data Mining | Pending.

## Loop 3 - Tasks 11-15

- [ ] Task 11 Lore-Based Upgrades | Pending.
- [x] Task 12 Scan Progress Persistence | DOD: partial hashes/progress persisted in v64 save arrays and MMF sidecar. Rejected: per-fragment string progress keys. Estimate: ~2-5 us saved per persist lookup.
- [ ] Task 13 Material Analysis | Pending.
- [ ] Task 14 3D Blueprint View | Pending.
- [ ] Task 15 Object Highlight | Pending.

## Loop 4 - Tasks 16-20

- [ ] Task 16 Audio Log Integration | Pending.
- [ ] Task 17 Ancient Technology | Pending.
- [ ] Task 18 Environmental Scanning | Pending.
- [ ] Task 19 Zero-GC Scanner Subtitles | Pending.
- [x] Task 20 Burst Frequency Math | DOD: `DataArchaeologyFrequencyTuningJob` wraps the tuning kernel. Rejected: managed callback math. Estimate: Burst path available; profiler proof pending.

## Loop 5 - Tasks 21-30

- [x] Task 21 Remove `string.Concat` | DOD: scanner science legacy concat replaced with `string.Create` cold helper. Rejected: `string.Concat`. Estimate: dev-only path; runtime hot path unaffected.
- [ ] Task 22 Clean Discovery Database Mojibake | Pending.
- [x] Task 23 LCG Randomness | DOD: deterministic LCG seeded from artifact hash and 50 m AUP sector. Rejected: `Random.Range`. Estimate: deterministic and allocation-free.
- [x] Task 24 `math.select` Progress Color | DOD: `ResolveProgressColorRgb` uses `math.select`. Rejected: branch-heavy UI color decision. Estimate: <1 us saved per UI sample.
- [x] Task 25 128-byte Discovery Bits | DOD: 1024 bits stored as 16 `long` words; codec writes raw 16 words, exactly 128 flag bytes. Rejected: bool arrays. Estimate: ~896 B saved versus `bool[1024]` payload.
- [ ] Task 26 Distance-to-Signal HUD Indicator | Pending.
- [ ] Task 27 Signal Interference Zones | Pending.
- [x] Task 28 Scan Interruption | DOD: `InterruptScan(hash)` snaps progress back to prior 25% milestone. Rejected: full scan reset. Estimate: no frame cost unless hit event calls it.
- [ ] Task 29 Data Log Transcript | Pending.
- [x] Task 30 Generate `.meta` Files | DOD: `DataArchaeologyRuntime.cs.meta` created with stable GUID. Rejected: relying on Unity auto-meta generation.

## Verification

- [x] `dotnet build Hecton8.Core.csproj -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` | Build succeeded, 0 warnings, 0 errors.
- [x] `git diff --check` on touched files | No whitespace errors; Git reported line-ending normalization warnings only.
- [ ] Unity Editor import, Console, PlayMode, GCMonitor, profiler, visual capture | PENDING VERIFICATION.
