# Status 1428 - Hybrid Compile And Unity Integrator

Domain: 82. The Integrator (Compile Medic)
Task count: 18
Status: PENDING VERIFICATION

Latest safe pass: Unity process observed at `C:\hades\Hecton8`; Unity-owned compiler server and AssetImport workers observed, so no `dotnet build` was launched. Third-party cleanup, XR/OpenXR settings audit, Unity quality-envelope routing, generic shared-memory policy, Standalone quality default, ProjectSettings whitespace correction, runtime profile contract cleanup, per-device URP shadow envelopes, and profile-aware visual budget smoke checks completed without launching an external build.

## Mandates Loaded Before Coding

- `ARCH_Execution_Phases.txt` - phase-safe proof for simulation vs VISUAL_SYNC.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` - cold DI and hot-path registry ban.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - static hot-path allocation audit.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - job fences, native ownership, lock safety.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` - blackbox and failure evidence expectations.
- `CORE_Global_State_Reset_NonReload_Transitions.txt` - domain reload and static reset validation.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` - CPU throttling and low-end VRAM budget.

## Loop 1 - Tasks 01-05

- [ ] Task 01 EXHAUSTIVE_CLI_BUILD_INQUISITION
  - DOD practice: parse existing report/log evidence before build execution.
  - Alternative rejected: blind `dotnet build`; violates compile throttling and wastes CPU.
  - Estimate: 180000 us.
- [ ] Task 02 ENVIRONMENT_AND_PROCESS_DISCOVERY
  - DOD practice: inspect Unity/dotnet/csc/VBCSCompiler process state before launching tools.
  - Alternative rejected: assume idle host; violates host stability rule.
  - Estimate: 90000 us.
- [ ] Task 03 EDITOR_LOG_PATH_RESOLUTION
  - DOD practice: resolve Editor.log path and read metadata with shared-read strategy.
  - Alternative rejected: lock or copy the log; risks blocking Unity writes.
  - Estimate: 70000 us.
- [ ] Task 04 PACKAGE_CACHE_BOUNDARY_AUDIT
  - DOD practice: inspect manifest and packages-lock before package override decisions.
  - Alternative rejected: copying package cache preemptively; creates duplicate asmdef risk.
  - Estimate: 120000 us.
- [ ] Task 05 REBIND_AND_SERIALIZATION_STATE_CHECK
  - DOD practice: parse current Editor.log for Missing Script and serialization warnings.
  - Alternative rejected: raw YAML edits; AGENTS.md rejects blind prefab mutation.
  - Estimate: 100000 us.

## Side Pass - Third-Party And XR Settings While Other Agents Compile

- [x] Third-party archive audit
  - DOD practice: archive only forbidden or unreferenced third-party assets outside `C:\hades\Hecton8`.
  - Alternative rejected: deleting assets or moving Crest/MapMagic/Odin/Feel/GPUInstancer/VLB/Technie/MeshBaker without first-party proof.
  - Estimate: 260000 us.
- [x] XR package chain audit
  - DOD practice: compare `manifest.json`, `packages-lock.json`, OpenXR settings asset, EditorBuildSettings config, and Editor.log.
  - Alternative rejected: removing transitive XR packages pulled by Meta OpenXR.
  - Estimate: 140000 us.
- [x] Quest OpenXR minimal feature correction
  - DOD practice: enable only Android controller/display/foveation features required by current first-party runtime policy.
  - Alternative rejected: enabling hand/eye/passthrough/AR/composition/mock/debug features without active usage.
  - Estimate: 90000 us.
- [x] Unity quality-envelope routing
  - DOD practice: use Unity quality rows as cold device-class envelopes and keep `HomeostasisBrain.GlobalQualityWeight` as the continuous runtime scalar.
  - Alternative rejected: binding the project identity to one development GPU or silently enabling XR for normal Standalone.
  - Estimate: 120000 us.
- [x] Generic shared-memory constraint routing
  - DOD practice: make `HomeostasisBrain` consume `HardwareTierDetector.SharedMemoryModeActive` and recommended VRAM budget instead of a single GPU-name string.
  - Alternative rejected: maintaining a one-device hard throttle inside the continuous quality dictator.
  - Estimate: 65000 us.
- [x] Standalone quality default lock
  - DOD practice: set `m_PerPlatformDefaultQuality.Standalone` to the medium cold envelope, then let bootstrap apply the measured hardware envelope.
  - Alternative rejected: relying on editor current-quality state for player builds.
  - Estimate: 12000 us.
- [x] Runtime profile contract cleanup
  - DOD practice: replace one-device enum/profile names in active runtime routes with compact/shared-memory/discrete device-class names while preserving numeric serialized values.
  - Alternative rejected: retaining one-device aliases in hot policy names; broad archive churn in historical docs.
  - Estimate: 52000 us.
- [x] Discrete VRAM budget scaling
  - DOD practice: route runtime VRAM hard ceilings through `HardwareTierDetector.RecommendedVramBudget*` and `VRAMBudgetThresholds.RuntimeDefault`.
  - Alternative rejected: forcing every discrete PC through the compact 1.8 GB ceiling.
  - Estimate: 48000 us.
- [x] URP high-envelope repair guard
  - DOD practice: make Crest render-pipeline validator apply per-asset render requirements so future repair passes do not collapse high/ultra assets or Quest VR settings back into compact settings.
  - Alternative rejected: one global shadow clamp inside the editor repair hook.
  - Estimate: 41000 us.
- [x] Profile-aware visual smoke checks
  - DOD practice: make dev visual budget smoke use the active profile thresholds instead of a fixed compact ceiling; keep compact fallback for unknown hardware.
  - Alternative rejected: making high/ultra debug smoke tests fail solely because the minimum proof lane budget was hardcoded.
  - Estimate: 34000 us.
- [x] Stable authority baseline wording
  - DOD practice: update current authority docs to define compact hardware as the minimum proof lane, not the product identity.
  - Alternative rejected: leaving future agents with one-device wording that conflicts with `GlobalQualityWeight`.
  - Estimate: 22000 us.
- [ ] Unity compile-blocker surgical pass
  - DOD practice: patch only diagnostics-backed Burst/SystemInfo/layout contract offenders, then let Unity import verify.
  - Alternative rejected: external `dotnet build` during active Unity compiler/import worker ownership.
  - Estimate: 145000 us.
