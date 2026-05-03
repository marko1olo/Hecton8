# 2026-05-03 Optimization Registry Ownership

Status: PENDING VERIFICATION

## Scope

- Removed private static `_instance` residue from the first-party Optimization runtime services: `AssetLifecycleGovernor`, `AssetLoadDispatcher`, `VRAMMonitor`, `VRAMPressureMonitor`, `RenderTextureLifecycleTracker`, `RenderTexturePool`, `VisorRTManager`, `CameraRTManager`, `PostFXRTManager`, and `UIRTManager`.
- Removed the remaining internal `Instance` accessors from the same slice.
- Moved duplicate-authority checks to the authoritative `GlobalRegistry` service slots.
- Kept `VRAMOptimizationBootstrap` as the creator/owner path; it already resolves existing services through `GlobalRegistry`.
- Updated `AssetLoadDispatcher` static helper reads to resolve `GlobalRegistry.AssetLoadDispatcher` instead of a private static owner.

## Evidence

- Static scan: `rg -n "_instance|Instance =>|public static .*Instance|internal static .*Instance|DontDestroyOnLoad\\(|SINGLETON" Assets/_Project/Scripts/Optimization -g "*.cs"` returned no matches.
- Lifecycle scan: every touched Optimization service now exposes `private bool TryRegisterService()` and uses a registry-slot equality guard before tick/listener registration.
- Diff check: scoped `git diff --check` returned no whitespace errors; Git only reported existing CRLF normalization warnings.
- Compile: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly` returned `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Full local Core compile: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly` returned `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Unity batchmode log: `Temp/CodexArtifacts/unity-batch-2026-05-03-optimization-registry-ownership.log`.
- Unity batchmode result: `Tundra build success`, `Mono: successfully reloaded assembly`, `Exiting batchmode successfully now!`.
- Unity strict failure scan: `error CS=0`, `warning CS=0`, `Scripts have compiler errors=0`, `Compilation failed=0`, `Tundra build failed=0`, `Compiler error=0`, `Unhandled Exception=0`.
- Unity residual note: the MCP package logs `No process found listening on port 8088` during editor shutdown. This is not a script compile error.
- Guard script: `Tools/ReloadAudit/Scan-FoundationGuards.ps1` now reports `Optimization singleton residue = 0` and treats regressions in this slice as blocking defects.

## Runtime Status

Not Play Mode verified. MCP resources are unavailable in this session. GCMonitor, VRAM residency, duplicate-scene-object behavior, and memory-retention slope remain PENDING VERIFICATION.

## Regression Model

CPU: unchanged in normal runtime; duplicate components now return before registering to tick/listener lanes.  
GC: no hot-path managed allocations added.  
Memory: removes ten static managed owner references; no runtime pools resized.  
Cadence: SlowTick/Tick registration remains under the existing `GlobalRegistry`/`SystemDispatcher` lanes.  
Correctness: registry slot is now the single source of truth for Optimization runtime authority.
