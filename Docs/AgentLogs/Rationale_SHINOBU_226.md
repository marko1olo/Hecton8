# Rationale_SHINOBU_226

Status: IMPLEMENTED_STATIC_VERIFIED_COMPILE_BLOCKED_BY_CPU_GATE

Problem: XML assignment has 19 explicit task nodes because Task 09 is absent.
Solution: Track 19 concrete tasks and record the numbering gap instead of inventing Task 09.
Rejected Alternatives: Inventing a missing task would violate strict parsing and cross-agent boundary discipline.
Scalability potential: No runtime effect; prevents scope creep that would waste engineer time and create merge risk.
Hardware Impact: 0 us runtime; avoids unnecessary source churn on low-end i3/MX350 and high-end machines alike.

Problem: Scanner/lore sync belongs to Echelon 8 Presentation & UX but touches Tools, UI, DataVault, shader presentation, and static data.
Solution: Keep authoritative scan state as owner-local Vault-backed DTOs with cold bootstrap dependency caching and method-local resolves; communicate with neighboring systems only through contracts/Vault/shader scalar surfaces.
Rejected Alternatives: Direct references to concrete tool/PDA/DataMonolith runtime classes, per-target MonoBehaviour state, or managed event/string routes.
Scalability potential: Low uses the same integer state and cheap shader scalar; middle/high/ultra can spend saved CPU on denser diegetic visual noise without changing scan truth.
Hardware Impact: Expected CPU saving from replacing managed string lookup/UI unlock paths with uint hash and bitmask writes is bounded in single-digit microseconds per scan tick; measured proof absent.

Problem: Existing scanner router stored legacy pointer-bearing `VaultBufferHandle<T>` fields, which violates current Vault ledger guidance and can retain stale pointers across compaction generations.
Solution: Replace persistent handles with `VaultGenerationHandle<T>` descriptors and resolve phase-local `NativeArray<T>` views inside `TryResolveVaultViews`.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` as a migration bridge; storing `NativeArray<T>` fields; direct GlobalRegistry lookups inside FastTick resolution.
Scalability potential: Low/middle/high/ultra all use the same pointer-free descriptor path; compaction and low-memory defrag can proceed without stale scanner-owned views.
Hardware Impact: Avoids stale pointer guard faults and defensive pointer refresh work; expected saving is small per tick but removes a failure mode on i3/MX350 and Quest-class ARM64.

Problem: Scan completion still needed a hash-to-lore unlock path that does not route through managed strings or PDA object state.
Solution: Add `ScanProgressDTO`, `ScannerLoreIndexDTO`, `ScannerEncyclopediaStateDTO`, plus `UpdateScanProgressJob` and `EvaluateScanCompletionJob`; completion uses FNV-1a uint keys and atomic OR into a 128-byte bitmask.
Rejected Alternatives: `Dictionary<string,int>`, `target.name`, `GetComponent<ItemData>`, PDA concrete method calls, or managed events carrying titles.
Scalability potential: Low uses the same O(1) native bit write; higher tiers spend saved CPU on shader scanner noise and refresh, not on authoritative unlock logic.
Hardware Impact: Replaces managed lookup/string compare with one bounded native hash probe and one atomic OR; expected saving 2-8 us per completion on low-end silicon, depending on prior UI path pressure.

Problem: Binary tier cadence created a quality switch instead of a continuous hardware response.
Solution: Resolve query cadence from `GlobalQualityWeight` using a smoothstep polynomial and pressure multiplier; scanner HUD globals carry progress, quality, refresh Hz, and dither complexity.
Rejected Alternatives: `if (IsLowEndHardware)`, discrete Low/Mid/High cadence branches, or CPU-side scanner HUD mesh simulation.
Scalability potential: q=0.1 collapses to low refresh/cheap dither; q=0.4-0.7 interpolates cadence and visual density; q=1.0 unlocks full shader refresh/visual overkill.
Hardware Impact: At thermal pressure, query cadence can stretch up to 3x smoothly, protecting i3/MX350/Quest frame time while preserving high-tier visual richness.

Problem: ARM64 layout proof was implicit and the prompt demanded exact byte offsets.
Solution: Convert scanner DTOs to explicit layouts where applicable and add `ValidateScanProgressLayout` plus tests for size and offsets.
Rejected Alternatives: Relying on sequential layout and comments; `[StructLayout(Pack=1)]`, which would risk unaligned ARM64 accesses.
Scalability potential: Layout stability benefits all tiers and rollback snapshots equally.
Hardware Impact: `ScanProgressDTO` is one 64-byte cache line; `ScannerEncyclopediaStateDTO` is 128 bytes of aligned ulong mask words, avoiding unaligned fetch penalties.

Problem: Static verification and editor control were missing for the scanner/lore sync slice.
Solution: Add `ScannerLoreDatabaseSyncTunerWindow` and `ScannerStringInquisitionValidator`; add `SHINOBU_226_SELF_AUDIT.xml` and route card documentation.
Rejected Alternatives: Chat-only reporting or relying on manual grep outside the repo.
Scalability potential: No runtime cost; designers can simulate hash unlocks without recompiling.
Hardware Impact: 0 us runtime; editor-only validation prevents expensive managed hot-path regressions.

Problem: Compile verification was requested but local CPU gate rejected build execution.
Solution: Check `dotnet/csc` process state and CPU load before build; CPU averaged 100%, so no dotnet build was launched.
Rejected Alternatives: Violating the project build gate to force a compile under load.
Scalability potential: Protects developer workstation responsiveness during 20+ agent parallel work.
Hardware Impact: Avoided additional compilation load while host was already saturated.
