# CONTRACT_AUTHORITY_SURGEON LOG

## 2026-05-16 - Contract Authority Codification
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED

What was wrong:
- Physical law values were split across physics, audio, atmosphere, construction, gameplay, survival, ecology, save, and editor validation code.
- AUP sector size literals and derived inverse math existed outside the contract authority.
- Save files had no contract-law hash, so changed gravity/O2/ecology rules could not be detected from persistence metadata.
- Human documentation had no generator and would drift from C# constants.

What was done:
- Added Core/Contracts authority files: HectonPhysicsContract, HectonSurvivalContract, HectonEcologyContract, ScalabilityContract, HectonMmfPagingContract, HectonVaultOffsetContract, HectonSignalLaneContract, HectonEditorBreadcrumbContract, HectonLoreContract, HectonContractValidator, and HectonContractVersion.
- Bound AUP sector size to one canonical 5000.0d source in HectonPhysicsContract; runtime aliases now point to that source.
- Rebound gravity, water density, hydrostatic pressure, surface pressure, water/air sound speed, O2/CO2/scrubber/fire rates, Homeostasis thresholds, MMF page size, signal lane IDs, breadcrumb defaults, LOD ratios, and ecology Lotka defaults.
- Added ref-readonly wrappers for hot physical/survival/ecology constants, backed by private static readonly fields.
- Added math.rcp-backed inverse constants and removed the remaining consumer-side `1.0d / HectonPhysicsContract...` AUP inverse.
- Added finite/positive/unit validation in static constructors for contract-owned numeric groups.
- Added save contract version hash fields and wrote HectonContractVersion.HashLo/HashHi into binary payload/master hash preimage.
- Added Tools/ContractAuthority/Generate-ArchitectHandbook.ps1 and regenerated Docs/ARCHITECT_HANDBOOK.md.
- Added ContractAuthorityEditTests for impossible values, LOD ratios, signal uniqueness, and version hash presence.

Cinematic cheats used:
- Preserved cheap scalar laws instead of introducing simulation: hydrostatic pressure is a single contract scalar per meter; LOD ratios are contract percentages; Homeostasis sacrifice thresholds are scalar gates.
- Kept visual-only constants out of physical-law contracts unless they directly controlled editor breadcrumb defaults or scalability policy.

Exact microseconds saved:
- Const/static readonly aliases: 0 us/frame versus local literals.
- Ref-readonly access: 0 us/frame copy pressure for scalar constants in Burst-compatible call sites.
- Contract static validation: cold load only, 0 us/frame.
- Save contract hash: +16 bytes per save payload, 0 us/frame.
- Handbook generator and edit tests: editor/build-time only, 0 us/frame.
- AUP inverse contract reuse: avoids one repeated division expression in path smoothing; estimate <1 us/frame, deterministic math source centralized.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 0 warnings, 0 errors.
- Field-level `public static float` scan in Core/Contracts: clean.
- Raw inverse scan against Hecton*Contract constants: clean.
- Core/Contracts AUP 5000.0d literal scan: only HectonPhysicsContract.cs.
- `dotnet build Assembly-CSharp-Editor.csproj`: blocked by existing RealtimeCSG.csproj missing source files, 216 CS2001 errors, unrelated to CONTRACT_AUTHORITY_SURGEON files.

## 2026-05-16 - Prompt Replay Re-Verification
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED

What was checked:
- Re-extracted `<AGENT_PROMPT id="CONTRACT_AUTHORITY_SURGEON">` from Docs/Tasks/CURRENT_BATCH.md using CLI.
- Re-read AGENTS.md and Docs/Actual Domains of Project.txt.
- Re-ran contract audit scans.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`.

Results:
- `public static float` field scan in Core/Contracts: clean.
- Raw inverse scan against Hecton*Contract constants: clean.
- Core/Contracts AUP sector literal scan: only HectonPhysicsContract.cs owns `5000.0d`.
- Semantic physics/survival constant scan: gravity, water density, sound speed, surface pressure, and O2 standard values exist in contract files; remaining non-contract hits are quaternion coefficients or visual authored values, not engine laws.
- Core build: PASS, 0 warnings, 0 errors.

Exact microseconds saved:
- Re-verification changed no runtime code. Runtime delta: 0 us/frame.

## 2026-05-16 - Multiplatform / H-Phi Inquisition Pass
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED; project compile currently blocked by external physics-domain errors.

What was wrong:
- Platform ceilings, Steam Deck IO pressure, typed data ownership, blackbox heartbeat sizing, and Ultra visual-overkill budgets were not named contract laws.
- The handbook generator used file names as contract names, which misreported nested public static contract classes.
- One exact `5000.0` sector-boundary probe remained in a world smoke tester outside HectonPhysicsContract.

What was done:
- Added HectonPlatformContract, HectonDataSovereigntyContract, and HectonVisualOverkillContract authority surfaces in the compiled contract unit.
- Added GlobalDataVault override offsets for platform, data-sovereignty, and visual-overkill constants.
- Mixed the new constants into HectonContractVersion so save payloads detect these law changes.
- Added edit-mode sanity tests for Quest-safe Pack=1 contract structs, platform bounds, data-sovereignty bounds, and Low-to-Ultra visual scaling.
- Updated the handbook generator to track the containing public static class and regenerated Docs/ARCHITECT_HANDBOOK.md.
- Replaced the last external exact `5000.0` AUP sector probe with the already-resolved AUP cell-size variable.

Audit results:
- Core/Contracts `public static float` field scan: clean.
- Core/Contracts non-readonly public static field scan: clean.
- Core/Contracts Update/LateUpdate/FixedUpdate/string.Format/Action/Func/delegate/EventBus scan: clean.
- Core/Contracts StructLayout Pack audit: clean; all explicit layouts use Pack = 1.
- Core/Contracts native-container allocation audit: clean; remaining NativeArray mentions are read-only views or caller-owned scratch parameters.
- Shader thread-group audit: max product 512, no product >1024, no Z >64.
- Exact `5000.0` literal scan: only HectonPhysicsContract owns it.

Cinematic cheats used:
- Low tier codifies LUT, triangle-noise, and dot-product fakes with no raymarch/POM/SSS.
- Ultra tier reserves 64 raymarch steps, 16 POM taps, 8 SSS samples, 8192 wake silt particles, 2048 visor salt crystals, and 512 hull dent decals.

Exact microseconds saved:
- Contract additions: 0 us/frame. These are const/static-readonly law surfaces.
- Steam Deck IO budgets: 0 us/frame until consumed by streaming systems.
- Blackbox capacity constants: 0 us/frame; fixed 300-frame sizing only.
- Smoke-test AUP literal cleanup: 0 us/frame.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore`: PASS before the external physics-domain change, 0 compiler errors, one transient locked-DLL retry warning.
- `dotnet build Hecton8.World.Contracts.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- Current `dotnet build Hecton8.Core.csproj --no-restore -p:UseSharedCompilation=false`: BLOCKED by `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`, 40 CS0103 missing `_exteriorThermal*` fields from another VaultNativeBuffer migration. No CONTRACT_AUTHORITY_SURGEON file appears in the compiler errors.

## 2026-05-17 - Contract File Authority Repair
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED

What was wrong:
- HectonPlatformContract, HectonDataSovereigntyContract, and HectonVisualOverkillContract existed as comment-only anchors while their real types were embedded in HectonContractValidator.cs.
- The generated local Core build carried a stale Hecton8.Core.Memory.Defrag reference and did not reliably compile the existing MemoryDefragContracts source.

What was done:
- Moved the three named contract classes back into their own files under Assets/_Project/Scripts/Core/Contracts.
- Kept HectonContractValidator focused on validation and HectonContractVersion hashing.
- Updated Directory.Build.targets to remove generated duplicate contract entries, explicitly include the named contract files for Hecton8.Core, remove the stale defrag DLL reference, and include MemoryDefragContracts.cs source for local builds.
- Regenerated Docs/ARCHITECT_HANDBOOK.md after the file split.

Cinematic cheats used:
- No new simulation. Low-tier Dear Lie and Ultra overkill budgets remain pure constants: LUT/triangle/dot-product on low, 64 raymarch steps, 16 POM taps, 8 SSS samples, 8192 wake silt particles, 2048 salt crystals, and 512 hull dent decals on ultra.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Build graph repair: build-time only.
- Contract file split: 0 us/frame.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Hecton8.World.Contracts.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- Core/Contracts scans: no public static float fields, no non-readonly public static fields, no Update/string.Format/delegate/EventBus, no local native container allocation, StructLayout Pack=1 clean.
- Shader numthreads audit: max product 512, no product >1024, no Z >64.
- Exact `5000.0` literal scan: only HectonPhysicsContract owns the sector size.

## 2026-05-17 - Automated Contract Inquisition Gate
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts. Current Hecton8.Core compile is blocked by external owner churn.

What was wrong:
- The inquisition relied on manual `rg` commands. Manual scans are not a durable quality gate under context compression.
- Current Core compile state is unstable because parallel agents are changing non-contract owners while this contract pass runs.

What was done:
- Added `Tools/ContractAuthority/Test-ContractAuthority.ps1`.
- The audit fails on: public static float fields, non-readonly public static fields, Update/LateUpdate/FixedUpdate, string.Format, Action/Func/delegate/EventBus, local native container allocation, non-Pack=1 StructLayout, exact external 5000.0 literals, comment-only contract anchors, missing named contract files, missing handbook sync, shader thread groups over 1024, shader Z groups over 64, DirectX-only renderer pragmas, and Metal-excluding shader pragmas.
- Re-ran the audit and the contract build after adding the tool.

Cinematic cheats used:
- The audit preserves Low-tier Dear Lie policies and Ultra-tier visual-overkill budgets as contract constants. No new simulation was added.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Audit script: developer/build tooling only.
- Contract build probe: build-time only.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- `dotnet build Hecton8.World.Contracts.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore -p:UseSharedCompilation=false`: currently BLOCKED outside this domain. Consecutive probes failed in changing non-contract owner files: Audio acoustic-zone listener migration, TetherManager slow-tick migration, and Gameplay/HectonPlayerMotor missing hot-swap/scalability interface methods. No Core/Contracts file appears in the current compiler error set.

## 2026-05-17 - Compile Stabilization Re-Probe
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts and selected contract compile targets.

What was wrong:
- Loop 7 ended with a real Core build wall caused by non-contract files changing under parallel agents.
- Plain root `dotnet build` is not a meaningful Unity gate in this folder because there is no selected solution and the root contains many generated project files.

What was done:
- Re-read the assignment from `Docs/Tasks/CURRENT_BATCH.md`, the domain map, AGENTS.md, and relevant mandates.
- Re-ran `Tools/ContractAuthority/Test-ContractAuthority.ps1`.
- Rebuilt `Hecton8.World.Contracts.csproj` and `Hecton8.Core.csproj`.
- Ran plain root `dotnet build --no-restore -p:UseSharedCompilation=false` to prove the command boundary.
- Ran `git diff --check` against touched contract/docs/tool files.

Cinematic cheats used:
- No new runtime simulation. Existing Low-tier Dear Lie and Ultra-tier visual-overkill constants remain the law surface.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Audit/build/diff probes: developer tooling only.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- `dotnet build Hecton8.World.Contracts.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore -p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build --no-restore -p:UseSharedCompilation=false`: BLOCKED by MSB1011 project-selection failure because the folder contains many generated `.csproj` files.
- `git diff --check` on touched files: PASS; line-ending warnings only.

## 2026-05-17 - Adjacent Survival DataVault Compile Repair
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts; Hecton8.Core selected compile target green after adjacent repair.

What was wrong:
- `HectonSurvivalSystem.Awake()` still called `EnsurePhysiologyScalarBuffer()` after the file had been migrated to `TryResolvePhysiologyScalarBuffer(...)`.
- The missing method broke `Hecton8.Core.csproj` even though the contract files remained clean.

What was done:
- Replaced the stale call with `_ = TryResolvePhysiologyScalarBuffer(out _)`.
- Preserved the existing GlobalDataVault-backed `VaultBufferHandle<SurvivalPhysiologyScalarResult>` path.
- Did not restore local persistent native allocation.

Cinematic cheats used:
- None. This is a compile/data-sovereignty repair. Runtime visual-tier laws remain in contracts.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- `Awake()` cold-path resolver call only.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false`: PASS, 0 warnings, 0 errors.

## 2026-05-17 - Version Authority File Repair
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts; selected Core compile target green.

What was wrong:
- `HectonContractVersion.cs` was still a comment-only anchor.
- The actual `HectonContractVersion` type was hidden in `HectonContractValidator.cs`, which violated named-file contract authority.

What was done:
- Moved `HectonContractVersion` into `Assets/_Project/Scripts/Core/Contracts/HectonContractVersion.cs`.
- Removed the version type from `HectonContractValidator.cs`.
- Updated `Directory.Build.targets` so generated `Hecton8.Core` removes then explicitly includes `HectonContractVersion.cs`.
- Hardened `Tools/ContractAuthority/Test-ContractAuthority.ps1` against generic comment-only anchors and off-file `HectonContractVersion` definitions.

Cinematic cheats used:
- None. This is authority hygiene and save-law hash visibility. Existing Dear Lie / Ultra visual-overkill constants are unchanged.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Static hash remains cold metadata.
- Audit additions are tool-only.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- `git diff --check` on the touched version/audit/shim files: PASS; line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false`: PASS, 0 warnings, 0 errors.
- Build discipline: one final selected build after static gates; no repeated rebuild loop.

## 2026-05-17 - Handbook Static-Readonly Sync Gate
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts documentation and static audit authority.

What was wrong:
- `Docs/ARCHITECT_HANDBOOK.md` was generated from `public const` declarations only.
- `HectonContractVersion.HashLo` and `HashHi` are public static readonly contract authority, but they were invisible in the handbook.
- The audit protected only the last repaired named-file classes instead of the full primary authority set.

What was done:
- Updated `Tools/ContractAuthority/Generate-ArchitectHandbook.ps1` to parse `public static readonly` declarations.
- Fixed the generator to preserve multi-line constants through the terminating semicolon, including bitmask unions.
- Regenerated `Docs/ARCHITECT_HANDBOOK.md`; it now records the computed contract-version hash fields.
- Expanded `Tools/ContractAuthority/Test-ContractAuthority.ps1` so primary authority classes must exist in their named file, must not be hidden elsewhere, and must appear in the handbook.

Cinematic cheats used:
- None. This is documentation/audit authority. The existing Low-tier Dear Lie and Ultra-tier visual-overkill constants remain unchanged.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Tooling improvement only; no runtime branch, allocation, native buffer, or Unity tick was added.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Generate-ArchitectHandbook.ps1`: PASS.
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- `git diff --check` on touched handbook/generator files: PASS; line-ending warnings only.
- Build discipline: no `dotnet build` run for docs/tool-only edits, per user instruction to avoid rebuild spam.

## 2026-05-17 - Signal ABI Registry Gate
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts signal ABI and selected Core compile target.

What was wrong:
- `HectonContractVersion` mixed a single signal lane instead of the whole signal-lane registry.
- `ContractAuthorityEditTests.SignalLaneIds_AreUnique` sampled a small list of lane IDs instead of reflecting every public lane.
- `PlayerMovementPresentationSignals.cs` already held the player signal payload structs with `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = ...)]`, but the generated Core build shim did not include that source file.

What was done:
- Added `HectonSignalLaneContract.SignalLaneRegistryHash = 0x13CABE35u`.
- Changed `HectonContractVersion` to mix `HectonSignalLaneContract.SignalLaneRegistryHash`.
- Replaced the sampled signal-lane NUnit test with reflection over every public static byte lane.
- Expanded `Tools/ContractAuthority/Test-ContractAuthority.ps1` to recompute the FNV-1a lane registry hash, reject duplicate/out-of-range lane IDs, require handbook sync for each lane, and require `PlayerMovementPresentationSignals.cs` in `Directory.Build.targets`.
- Updated `Directory.Build.targets` to remove then explicitly include `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs` for `Hecton8.Core`.

Cinematic cheats used:
- None. This is ABI authority and compile graph repair. Existing Low-tier Dear Lie and Ultra-tier visual-overkill constants remain unchanged.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Signal registry hash is const metadata.
- Reflection test and FNV audit are editor/tooling only.
- Build shim change compiles existing payload structs; it adds no runtime branch, allocation, or tick.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- `git diff --check` on touched signal/audit/shim files: PASS; line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false`: PASS, 0 warnings, 0 errors.
- Build discipline: one selected Core build after static gates; no repeated rebuild loop.

## 2026-05-17 - SignalBus Coverage Closure
Status: VERIFIED MASTER GRADE - PHYSICS CODIFIED for Core/Contracts static authority; selected Core compile currently blocked by external World/VFX churn.

What was wrong:
- 15 concrete `SignalBus<T>.Configure` lanes had no byte ID in `HectonSignalLaneContract`.
- `PlayerMovementPresentationSignals.cs` was an empty namespace while six player presentation payload structs lived in `GlobalSignals`.
- Scalability events used a local `0x53434C54u` stable lane hash outside the contract.

What was done:
- Added lane IDs 111-125 in `HectonSignalLaneContract` for scalability, acoustic-zone, data-vault, prefab, player presentation, HUD, seismic, and tool-acoustic lanes.
- Recomputed `SignalLaneRegistryHash` to `0x6F76078Au`.
- Moved `PlayerFootstepSignal`, `PlayerWaterSplashSignal`, `PlayerExhaleSignal`, `PlayerSprintStateSignal`, `PlayerFatalPressureSignal`, and `PlayerTransportBailoutSignal` into `Core/Signals/PlayerMovementPresentationSignals.cs` with explicit `Pack = 1`.
- Removed those six payload structs from the `GlobalSignals` monolith.
- Added `ScalabilityChangedEventStableHash` to `HectonSignalLaneContract` and routed `GlobalSignals` plus `IPlatformIntegration` through it.
- Hardened `Test-ContractAuthority.ps1` so configured typed lanes must have byte IDs, player presentation payloads must stay in the named file with Pack=1, and the scalability hash literal cannot reappear outside the contract.

Cinematic cheats used:
- None. This was signal ABI and hidden-constant repair. Existing Dear Lie and Ultra visual-overkill constants remain unchanged.

Exact microseconds saved:
- Runtime delta: 0 us/frame.
- Payload movement and constants are compile-time ABI hygiene.
- Audit checks are tooling only.

Verification:
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Generate-ArchitectHandbook.ps1`: PASS.
- `powershell -ExecutionPolicy Bypass -File Tools/ContractAuthority/Test-ContractAuthority.ps1`: PASS; shader numthreads max product 512.
- Configured `SignalBus<T>` versus lane contract comparison: PASS, no missing concrete lane IDs.
- `git diff --check` on touched signal/audit/handbook files: PASS; line-ending warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false`: BLOCKED outside Core/Contracts. Current failures are in `World/SargassumMicroFaunaBoids.cs` missing `_spawnData`/`_singleBoidUpload` and `VFX/HectonMarineSnowRenderer.cs` missing global wake fields/IDs.
