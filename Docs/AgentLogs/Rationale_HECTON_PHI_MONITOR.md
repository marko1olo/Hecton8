# Rationale_HECTON_PHI_MONITOR

Status: CLI COMPILE VERIFIED / PENDING RUNTIME VERIFICATION

## 2026-05-14 Current H-Phi Rescan

Problem: The old `HECTON_PHI_REPORT.md` was a 2026-05-13 snapshot. The workspace has changed to 1498 first-party C# files, so the previous H-Phi number is stale.

Solution: Run a fresh static scan over `Assets/_Project/Scripts/**/*.cs` and separate prompt-narrow H-Phi from risk-adjusted H-Phi. Runtime resonance `R` is not measured because Unity PlayMode/profiler evidence is absent.

Rejected Alternatives: Reusing archived Batch005 counts was rejected because concurrent agents changed the tree. Reporting runtime H-Phi was rejected because static regex cannot prove frame time, GC, Burst, scene wiring, or memory stability.

Scalability potential: Low-tier devices need the metric to expose DataVault and NativeArray ownership pressure before it becomes frame-time debt. High/Ultra devices benefit only if the metric pushes saved CPU/bandwidth into richer visual paths, not vanity refactors.

Hardware Impact: Static audit has no runtime cost. It identifies Data Sovereignty as the dominant blocker: `GlobalDataVaultRefs=15` versus `NativeArrayRefs=7445`.

## 2026-05-14 Audit Tool And ABI Sentinel

Problem: H-Phi was manually computed from ad hoc CLI commands. Several save DTOs already had `[BinaryBlittableSafe]` and `[StructLayout]`, but `BinaryLayoutManifest.VerifySaveLayouts()` did not assert their sizes/offsets, leaving layout drift to be detected later.

Solution: Add `Tools/Architecture/HectonPhiAudit.ps1` for reproducible static counters. Expand `BinaryLayoutManifest` with cold-boot size/offset assertions for `ExternalScavengerSiteDTO`, geology DTOs, `ModuleBlitDTO`, `PDAContextualAdvisoryDTO`, `EnvironmentalStrainDTO`, and `ModuleGraphEdgeDTO`.

Rejected Alternatives: Adding `[BinaryBlittableSafe]` to DTOs with strings, arrays, or bool flags was rejected because it would improve a vanity ratio while weakening truth. Migrating World/Gameplay NativeArrays into `GlobalDataVault` was rejected in this pass because it is a high-risk ownership change in a dirty parallel-agent tree.

Scalability potential: Low/MX350 benefits from earlier detection of ABI drift in binary save payloads before corrupted saves or ARM alignment bugs appear. High/Ultra keeps the same save path; the change is a guardrail, not a visual system.

Hardware Impact: Runtime hot path cost is zero. Cold boot performs additional constant-time layout assertions once during `BinaryLayoutManifest.VerifyColdBoot()`.

Verification: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` completed on 2026-05-14 with 0 warnings and 0 errors. Unity PlayMode, profiler, GCMonitor, and boot-time layout assertion execution remain pending because no Unity MCP/editor session is available in this turn.

## 2026-05-14 Save DTO Bool Blit Purge

Problem: `ProceduralFaunaStateDTO[]` and `HibernatedFaunaStateDTO[]` were serialized through raw `WriteStructArray<T>` / `ReadStructArray<T>` even though both DTOs contain `bool`. That ties save ABI to runtime bool layout and padding behavior.

Solution: Replace those two raw array blits with explicit field codecs. The procedural fauna record writes `long + float + bool + bool + 2 pad bytes` for a fixed 16-byte stride. The hibernated fauna record writes explicit scalar fields plus `AbsoluteUniversePositionBlit128`, a bool byte, and 3 pad bytes for a fixed 112-byte stride. Both DTOs now declare explicit `[StructLayout]` with those sizes.

Rejected Alternatives: `[BinaryBlittableSafe]` on bool DTOs was rejected because it would improve the H-Phi vanity ratio while hiding the bool ABI risk. Broad save format version mutation was rejected because the fixed-stride field codec preserves the current payload shape and avoids a migration branch.

Scalability potential: Low-tier devices benefit from save/load determinism and fewer platform-specific corruption cases. High/Ultra behavior is unchanged; this is persistence safety, not runtime visual work.

Hardware Impact: Hot path cost is zero. Save/load pays small explicit loops for two bounded arrays, replacing raw copy with deterministic field writes. This is cold-path CPU traded for ABI stability.

Verification: First build attempts hit missing `Temp\bin\Debug` dependency DLLs and then missing `project.assets.json`. After `dotnet restore`, rebuilding 15 dependency projects, and retrying `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`, the final core build succeeded with 0 warnings and 0 errors in 43.96 s.
