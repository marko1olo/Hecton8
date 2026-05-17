# ARCHITECT_BRIDGE_FACADE Rationale

## Decision 001 - Disk State Recovery
Problem: Required anti-amnesia files were missing, so progress could not survive context compression.
Solution: Created `Status_ARCHITECT_BRIDGE_FACADE.md` and this rationale file before implementation. Disk state becomes the primary memory.
Rejected Alternatives: Relying on chat memory or MCP summaries is invalid because the project protocol treats those as lossy.
Scalability potential: High. Deterministic state files let concurrent agents and the integrator recover exact Bridge status without runtime cost.
Hardware Impact: 0 us runtime cost on i3/MX350; prevents duplicate implementation passes that would waste build and review time.

## Decision 002 - Mandate Selection
Problem: The Bridge touches data ownership, SignalBus, editor UX, AUP, telemetry, Addressables, and persistence. Using only local preferences would create architecture drift.
Solution: Bound the implementation to GlobalRegistry/DataVault ownership, zero-GC cold sync, typed SignalBus lanes, binary persistence layout, fixed blackbox telemetry, AUP precision rules, and Addressables lifecycle rules.
Rejected Alternatives: Direct Unity event delegates, managed dictionaries in runtime sync, per-frame polling, and private NativeArrays are rejected because they violate the registry and zero-GC mandates.
Scalability potential: Ultra. Low tier pays only explicit sync cost; high tier gets richer visual metadata through the same packed buffer.
Hardware Impact: Estimated 30-150 us saved during designer live tuning on i3/MX350 versus managed event dispatch and string-keyed maps; 0 us in steady-state frames.

## Decision 003 - Prompt Source Handling
Problem: The protocol requires extracting this prompt from `CURRENT_BATCH.md`, but exact CLI extraction found no `ARCHITECT_BRIDGE_FACADE` block in that batch file.
Solution: Log the mismatch and treat the user-provided inline XML as the active source of truth for this recovery pass.
Rejected Alternatives: Guessing from neighboring batch prompts is rejected because strict parsing forbids influence from other agents.
Scalability potential: High. Prevents cross-agent task contamination.
Hardware Impact: 0 us runtime cost; avoids architectural churn.

## Decision 004 - Prefab SO Facade Instead Of Runtime Registry Replacement
Problem: The project already owns a runtime `PrefabRegistry` MonoBehaviour used by pooling and scatter systems, but designers need drag/drop hash registration.
Solution: Added `H8PrefabRegistry` as an authoring facade that feeds packed DataVault buffers and can warm the existing runtime registry at boot.
Rejected Alternatives: Replacing `PrefabRegistry` or adding a second runtime dictionary would create duplicate authority and hot-path string/object lookups.
Scalability potential: Ultra. Low tier gets one packed hash table; high tier can bind lore, acoustic, LUT, and high-tier visual hashes from the same row.
Hardware Impact: 0 us per frame; boot binder cost is linear and avoids per-spawn hash/name work on i3/MX350.

## Decision 005 - Bridge-Owned DataVault Buffers
Problem: Arbitrary designer offsets into unknown existing buffers can corrupt unrelated systems and violate concurrent-agent ownership.
Solution: Allocated Bridge buffer IDs for prefab mapping, design values, telemetry, input bindings, lore links, and MacroDB header.
Rejected Alternatives: Writing directly into submarine/physics/native buffers from ScriptableObject fields was rejected as unsafe cross-domain pointer mutation.
Scalability potential: High. Consumers can read raw bits without facade overhead, and the Bridge can add fields by appending offsets.
Hardware Impact: 0 us steady-state; explicit setter writes cost roughly 2 memory fences plus one 4-byte store.

## Decision 006 - Typed Signal Lanes Only
Problem: Prefab acoustic/lore updates and live design tuning need decoupling without managed delegates.
Solution: Added packed `DataVaultUpdateSignal`, `PrefabAcousticSignatureSignal`, and `PrefabLoreLinkSignal` lanes.
Rejected Alternatives: UnityEvent, C# delegates, string event names, or direct SONAR/PDA references were rejected as managed and tightly coupled.
Scalability potential: Ultra. Low tier can drop optional lanes under stress; high tier can consume richer visual metadata.
Hardware Impact: Saves estimated 30-150 us during live tuning bursts versus managed delegate/string dispatch; 0 us when no edits occur.

## Decision 007 - NaN Vaccination And Blackbox In DataVault
Problem: Designer-entered NaN or zero critical values can poison GPU/mobile pipelines.
Solution: Clamp invalid values to safe defaults before write, emit telemetry, keep a 300-entry DataVault replay ring, and dump it on invalid-number detection.
Rejected Alternatives: Inspector validation only was rejected because runtime/deserialization can still deliver invalid floats.
Scalability potential: High. Same binary ring works on Quest, Steam Deck, Mac, and PC without managed allocations.
Hardware Impact: One finite check per edit; zero frame cost after values are written.

## Decision 008 - MacroDB Header Persistence
Problem: Designer tweaks must survive sessions without adding a new persistence stack.
Solution: Wrote a packed `H8FacadeMacroHeader` into DataVault and marked the existing MacroDB dirty sector when the service is open.
Rejected Alternatives: JSON sidecar and PlayerPrefs were rejected because they bypass binary persistence and platform I/O policy.
Scalability potential: High. Steam Deck avoids extra microSD file churn; PC can persist richer hashes in the same packed header.
Hardware Impact: Cold edit path only; avoids repeated file opens outside MacroDB.

## Decision 009 - Compile Wall Repair
Problem: CLI verification exposed unrelated compile failures: a missing fully qualified `KccVelocitySignal` use and removed `LaserCutterEvents` queue fields.
Solution: Applied minimal compile repairs: fully qualified the KCC signal and restored the missing static NativeQueue/counter fields.
Rejected Alternatives: Ignoring compile failure or reverting other agents' larger edits was rejected. The repairs were the smallest surface that restored build.
Scalability potential: Middle. Repairs restore deterministic event lanes without changing gameplay behavior.
Hardware Impact: 0 us regression; existing LaserCutter cold allocation path remains unchanged.

## Decision 010 - CLI Project Verification
Problem: Unity-generated csproj files did not include the newly added Bridge scripts before Unity regeneration.
Solution: Added temporary compile includes to `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` so `dotnet build` verifies Bridge runtime and editor code immediately.
Rejected Alternatives: Claiming compile success from stale project files was rejected as fake verification.
Scalability potential: Low runtime impact, high integration value.
Hardware Impact: 0 us runtime; prevents shipping uncompiled Bridge code.

## Decision 011 - Final Compile Wall Stabilization
Problem: A later CLI build surfaced another unrelated generated-project gap: `Hecton8.AI.Ecosystem` source existed but was absent from `Hecton8.Core.csproj`, causing downstream editor build failure.
Solution: Added `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs` to the CLI project include list so `BinaryLayoutManifest` and runtime installer references compile.
Rejected Alternatives: Reporting stale success from a prior build was rejected because the final build must reflect the current worktree.
Scalability potential: Middle. The repair keeps ecosystem population data packed in DataVault and visible to binary layout verification.
Hardware Impact: 0 us runtime; compile-only project metadata repair.

## Decision 012 - Prompt Archived As Disk Truth
Problem: The batch extraction rule could not be satisfied from `Docs/Tasks/CURRENT_BATCH.md` because the `ARCHITECT_BRIDGE_FACADE` XML block is absent from that file.
Solution: Wrote the exact active XML assignment to `Docs/Tasks/Prompt_ARCHITECT_BRIDGE_FACADE.xml` and re-read it by CLI before the GO AGAIN pass.
Rejected Alternatives: Depending on chat memory, summary memory, or neighboring agent prompts is rejected by strict parsing.
Scalability potential: High. Future context compression can recover the precise Bridge scope without cross-agent contamination.
Hardware Impact: 0 us runtime; avoids duplicate or wrong-system implementation passes.

## Decision 013 - Cold Binary Layout Sentinel
Problem: Quest/ARM64 and Mac/Metal builds can desync or crash if Bridge DTOs gain implicit padding despite DataVault and MacroDB assuming byte-stable layouts.
Solution: Added `H8BridgeBinaryLayoutVerifier` and `[BinaryBlittableSafe]` markers. The verifier checks size and critical offsets for prefab mappings, lore links, design values, telemetry, input bindings, MacroDB header, and typed signals at cold boot.
Rejected Alternatives: Relying only on `[StructLayout(Pack = 1)]` was rejected because attributes can be removed later without an immediate failure.
Scalability potential: Ultra. Low/Middle/High/Ultra tiers share one binary contract; high-tier visual metadata stays packed rather than string-discovered.
Hardware Impact: 0 us steady-state on i3/MX350 and Quest; cold boot validation only.

## Decision 014 - Edit-Mode SignalBus Allocation Block
Problem: Drag/drop registry edits in edit mode could publish typed signals before runtime, waking persistent SignalBus lanes for an editor-only asset operation.
Solution: `H8PrefabRegistry.PublishPrefabSignals` now exits unless `Application.isPlaying`; runtime boot binding still publishes acoustic and lore signals when the game is live.
Rejected Alternatives: Letting editor asset validation allocate runtime lanes was rejected because the sync-layer must remain setter/boot only.
Scalability potential: High. Steam Deck and low RAM devices avoid stray queue residency; PC still receives boot-time high-tier metadata.
Hardware Impact: Static estimate saves 30-150 us and native queue residency during editor drag/drop bursts; 0 us steady-state.

## Decision 015 - Visual Overkill Hash Repair
Problem: The design facade high-tier visual hash used the acoustic seed, and prefab entries did not auto-generate high-tier visual metadata.
Solution: Switched design and prefab high-tier hashes to `VisualOverkillSeed`, while low-tier "Dear Lie" LUT hashes keep `LutSeed`.
Rejected Alternatives: Runtime string lookup for Ultra visual variants was rejected because DataVault consumers must read raw hashes.
Scalability potential: Low uses 1D LUT/triangle-noise/dot-product controls; Middle reads the same packed floats with modest VFX; High/Ultra can map deterministic hashes to raymarch, 16-tap POM, SSS, salt-crystal, silt, dent, and particle-overkill consumers.
Hardware Impact: 0 us steady-state; removes future per-spawn or per-material string lookup pressure.

## Decision 016 - Blackbox Heartbeat Without Per-Frame Polling
Problem: The Bridge blackbox recorded value deltas but not a high-level sync heartbeat, while a per-frame monitor would violate the no-`Update()` sync mandate.
Solution: `SyncDesignData` records a `BridgeHeartbeat` entry into the existing 300-entry DataVault telemetry ring each time the setter path runs.
Rejected Alternatives: A local NativeArray heartbeat store, MonoBehaviour `Update()`, or managed list log was rejected as private data and steady-frame overhead.
Scalability potential: High. Low tier pays only on edits; high tier gets enough forensic context to explain designer-value crashes.
Hardware Impact: One packed ring write per explicit sync; 0 us on frames with no designer changes.

## Decision 017 - Vault Handles Instead Of Bridge NativeArray Aliases
Problem: The Bridge did not own native memory, but its setter/binder methods still declared local `NativeArray<T>` aliases, which made the data-sovereignty audit ambiguous.
Solution: Switched design, prefab, input, MacroDB header, and blackbox paths to `VaultBufferHandle<T>` with resolved raw pointers. The Vault remains the owner and generation gate.
Rejected Alternatives: Keeping `NativeArray<T>` aliases was rejected because the audit rule is explicit: Bridge logic should not look like it owns native arrays.
Scalability potential: High. Low tier gets the same cold setter path with clearer ownership; High/Ultra consumers still read packed Vault buffers and visual hashes.
Hardware Impact: 0 us steady-state. Explicit sync adds a generation resolve only during designer edits or boot binding.

## Decision 018 - Boot Binder Start-Phase Registry Read
Problem: `H8PrefabRegistryBootBinder` read `GlobalRegistry.DataVault` in `Awake`, which violates the self-init-only init rule and can race bootstrap service registration.
Solution: Moved the optional bind trigger to `Start`, leaving `Awake` absent in Bridge runtime code.
Rejected Alternatives: Forcing DefaultExecutionOrder or polling for the DataVault was rejected as more brittle and more expensive.
Scalability potential: High. Low-end devices avoid init-order retries; high-end devices keep the same bind result.
Hardware Impact: 0 us steady-state; boot-only behavior.

## Decision 019 - Bounded VRAM Estimation
Problem: Designer-entered texture dimensions could be absurd and inflate VRAM math even though the path is cold.
Solution: Clamped estimator width/height/BPP before multiplication.
Rejected Alternatives: Letting inspector values pass through was rejected because bad authoring data must fail safe before hitting budgets.
Scalability potential: Low tier keeps budget math conservative; High/Ultra still expose visual-overkill controls without corrupting ledgers.
Hardware Impact: Cold path only; no frame cost.

## Decision 020 - Interrupted Build Recovery
Problem: The interrupted run left stale MSBuild/Roslyn processes locking `Temp/obj/Hecton8.Core/Hecton8.Core.dll`, causing a false compile failure.
Solution: Identified and terminated only stale project-build worker processes, restored project dependencies, and reran Core/Editor builds with node reuse and shared compilation disabled.
Rejected Alternatives: Reporting the lock as a Bridge compile failure or killing Unity's own Roslyn process were rejected.
Scalability potential: Middle. Deterministic verification prevents stale build workers from hiding real architecture regressions.
Hardware Impact: 0 us runtime; build hygiene only.

## Decision 021 - Post-Resume Verification Ledger
Problem: Context compaction can make prior build and static-audit claims unreliable unless the current shell session rechecks them.
Solution: Re-ran the Bridge grep inquisition, Unity `.meta` coverage check, `git diff --check`, and both Core/Editor builds with node reuse/shared compilation disabled, then recorded the result on disk.
Rejected Alternatives: Reporting only the pre-compaction result was rejected because the project protocol treats chat memory as lossy.
Scalability potential: High. The Bridge remains cold-setter-only for low tier, while High/Ultra visual metadata stays present as hashes and LUT IDs without adding frame work.
Hardware Impact: 0 us runtime; verification-only pass. Fresh compile outputs show 0 warnings and 0 errors for both checked projects.

## Decision 022 - Stale Vault Row Tombstones
Problem: DataVault buffers grow to satisfy prior registry/input sizes and do not shrink just because a designer removes rows. Without clearing, raw consumers could read stale prefab, lore, or input rows past the active count.
Solution: Clear the full resolved Vault span before writing active prefab, lore, and input rows; clear existing prefab/lore spans when a registry is empty. Active rows still use packed hashes and typed signals.
Rejected Alternatives: Adding managed count dictionaries or relying on future GPU/input consumers to know editor asset sizes was rejected because consumers read raw buffers and should see zero-hash tombstones for inactive rows.
Scalability potential: High. Low tier avoids stale draw/input work; High/Ultra still get dense packed rows for visual-overkill metadata without frame polling.
Hardware Impact: 0 us steady-state. Cold bind/sync adds one `MemClear` over existing Bridge-owned Vault spans, paid only when designers bind or live-sync data.

## Decision 023 - Editor Guard Rails For Human Control
Problem: Contract generation could produce duplicate or keyword constants, and the AUP visualizer could convert extreme 64-bit sectors into non-finite SceneView coordinates.
Solution: Contract identifiers now include asset hash, field hash, and binding index suffixes, with keyword prefixing. AUP editor pivots are clamped to finite SceneView coordinates.
Rejected Alternatives: Asking designers to avoid duplicate labels or small sector coordinates was rejected because the facade exists to absorb human error.
Scalability potential: Middle. The low-tier path is unchanged; High/Ultra authoring can add more facade assets and visual controls without generated-code collisions.
Hardware Impact: 0 us runtime; editor-only resilience.

## Decision 024 - Non-Bridge Compile Wall
Problem: After the Bridge stale-row patch passed Core once, concurrent non-Bridge edits introduced compile errors in Bootstrap, Lockstep, Fluid, Tether, PlayerTool, PlayerNoise, and GlobalSignals.
Solution: Logged the dependency wall and kept Bridge changes isolated. Static Bridge audit and diff hygiene remain clean.
Rejected Alternatives: Fixing seven outside domains from the Bridge pass was rejected as architectural overreach and likely conflict with active agents.
Scalability potential: High for integration discipline; Bridge remains independently clean while the integrator resolves the unrelated compile wall.
Hardware Impact: 0 us runtime.

## Decision 025 - Empty Input Tombstone
Problem: Input facade sync cleared stale rows only when the active binding count was positive. If a designer emptied the list, previous button masks could remain readable in `BridgeInputFacadeBindings`.
Solution: Added an empty-list tombstone path that resolves the existing Vault handle and clears the full span.
Rejected Alternatives: Treating count zero as a no-op was rejected because raw consumers cannot infer asset state from a managed list.
Scalability potential: High. Low tier avoids stale input actions; High/Ultra keep the same packed input lane.
Hardware Impact: 0 us steady-state. Explicit empty sync pays one existing-span `MemClear` only when designers push the input facade.

## Decision 026 - Header And Telemetry Fence Consistency
Problem: Design value writes had memory fences, but MacroDB header and telemetry ring writes were still unfenced raw pointer writes.
Solution: Added `Thread.MemoryBarrier()` around header and blackbox ring writes.
Rejected Alternatives: Relying on cold-path timing was rejected because the Bridge promise is deterministic setter behavior.
Scalability potential: High. Same packed header/telemetry data stays stable across Quest, Android, Steam Deck, Mac, and PC.
Hardware Impact: 0 us steady-state. Two fences are paid only during explicit facade sync or telemetry write.

## Decision 027 - Current Non-Bridge Compile Wall
Problem: A fresh Core build after the fence pass fails, but the errors are now in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs`, not Bridge.
Solution: Preserve the failure in the Bridge log and do not edit outside the assigned domain without a critical cross-domain Bridge reason.
Rejected Alternatives: Blindly repairing UI navigation and ecosystem systems from a Bridge-control prompt was rejected as cross-domain interference.
Scalability potential: High for concurrent-agent discipline. Bridge remains clean while UI/World owners fix their contracts.
Hardware Impact: 0 us runtime.

## Decision 028 - ARM-Safe Facade Float Offsets
Problem: The design facade accepted arbitrary byte offsets and the setter cast `byte* + offset` to `float*`. An odd offset is survivable on some PC paths but is invalid architecture for Quest/ARM64 and can also corrupt adjacent packed values.
Solution: Clamp the Bridge design-value buffer to 64 KiB and align every facade float offset to a 4-byte lane in both authoring validation and the runtime setter backstop. Hash/offset changes now count as live-sync changes, not just value changes.
Rejected Alternatives: Trusting designers to type multiples of four or silently writing unaligned floats was rejected. Writing through byte copies was also rejected because consumers expect raw float lanes and aligned offsets are the correct contract.
Scalability potential: Low/Middle/High/Ultra all read the same packed raw float lanes. Low keeps the setter cold; High/Ultra can add more visual-overkill controls without creating unbounded Vault buffers.
Hardware Impact: 0 us steady-state. Explicit edits pay one integer align/clamp operation; avoids ARM alignment faults and prevents accidental large DataVault growth on i3/MX350.

## Decision 029 - Editor IMGUI Window Purge
Problem: The Bridge had two editor windows using `OnGUI()`, and the current project checklist explicitly rejects `OnGUI` usage during self-review even when editor-only.
Solution: Replaced the Prefab Binder and AUP Visualizer window bodies with UI Toolkit `CreateGUI()` controls. Removed the redundant SceneView IMGUI Zero Camera overlay because the UI Toolkit window and menu item still expose the command.
Rejected Alternatives: Leaving IMGUI in place as "editor-only" was rejected because the Bridge domain is supposed to be the human-control surface and should meet the stricter authoring standard.
Scalability potential: Editor-only. Runtime profiles are unchanged; the authoring surface remains drag/drop and slider driven.
Hardware Impact: 0 us runtime on all tiers. Editor repaint allocations are outside gameplay, but the forbidden `OnGUI()` method is gone from Bridge.

## Decision 030 - Prefab VRAM Meter Texture Deduplication
Problem: The prefab registry VRAM meter only inspected `mainTexture` and could double-count the same texture when shared by multiple materials/renderers. That gives designers false budget pressure and can push unnecessary down-tiering.
Solution: Editor estimation now scans all material texture slots and counts each texture instance once per prefab.
Rejected Alternatives: Keeping a narrow `mainTexture` estimate was rejected because the facade's job is to make asset cost visible without forcing designers to inspect shader internals.
Scalability potential: Low tier gets more accurate MX350 budget pressure; High/Ultra can bind richer material sets without the registry exaggerating shared texture cost.
Hardware Impact: 0 us runtime. Editor-only estimate avoids false VRAM debt; no Unity profiler measurement was run in this CLI session.

## Decision 031 - Current Compile Wall Refresh
Problem: After the ARM/editor pass, Core and Editor builds still fail, but the failures are outside Bridge: compass presentation DTO drift and ArchitectEye diagnostics.
Solution: Preserve the dependency wall and keep Bridge changes isolated. Static Bridge inquisition and diff hygiene pass.
Rejected Alternatives: Editing UI Navigation or Core Diagnostics from the Bridge facade prompt was rejected as cross-domain interference.
Scalability potential: High for integration discipline. Bridge is not adding new frame work while other owners repair their contracts.
Hardware Impact: 0 us runtime.

## Decision 032 - Prefab Registry Tombstones For Cleared Assets
Problem: A prefab registry entry could keep stale hash, lore, acoustic, LUT, high-tier visual, VRAM, and flag data after a designer cleared the prefab reference. The runtime binder would then republish old identity data into `BridgePrefabMapping` and the typed acoustic/lore lanes.
Solution: Treat entries with neither a direct prefab nor an Addressables reference as tombstones. `RebuildHashes()` clears runtime hashes/flags/cost, runtime bind skips unbound rows, and signal publishing refuses unbound entries. Addressable-only rows remain valid under `UNITY_ADDRESSABLES_EXIST` by deriving their source hash from the asset GUID.
Rejected Alternatives: Preserving stale manually typed values was rejected because raw GPU/PDA/SONAR consumers cannot distinguish intentional metadata from a removed prefab. Adding a managed active-count side table was rejected because zero-hash tombstones are simpler and stay in the Vault lane.
Scalability potential: Low tier avoids drawing or acoustically registering deleted assets. Middle/High/Ultra still bind direct-prefab or addressable rows with the same packed visual-overkill hashes and 1D LUT IDs.
Hardware Impact: 0 us steady-state. Cold validation/bind pays simple branch/hash work only when designers edit or registries bind.

## Decision 033 - `in` SignalBus Pushes And Contract Offset Backstop
Problem: Bridge signals were already packed but were published by value, and generated facade contracts could emit an unaligned offset if the asset had not passed through `OnValidate()`.
Solution: Changed all Bridge `SignalBus<T>.Push` calls to `Push(in signal)` and made the contract generator emit offsets through `H8BridgeFacadeRuntime.AlignFloatOffsetBytes`.
Rejected Alternatives: Relying on the compiler to elide struct copies or on designers to force asset validation was rejected. The typed lane API already defines the copy-minimized call shape, and ARM/Quest alignment must be enforced at every Bridge boundary.
Scalability potential: Low/Middle/High/Ultra receive the same aligned raw float lanes. High/Ultra can add more visual-overkill controls without generated constants drifting from runtime setter alignment.
Hardware Impact: 0 us steady-state. Explicit signal pushes and contract generation are cold/editor paths; no Unity profiler microseconds were claimed.

## Decision 034 - Current Core Contracts Compile Wall
Problem: Fresh Core build now fails before Bridge verification completes because non-Bridge consumers cannot resolve `HectonPhysicsContract`, `HectonEcologyContract`, and `ScalabilityContract`.
Solution: Verified the contract source files exist under `Assets/_Project/Scripts/Core/Contracts` and that the CLI project lacks a generated `Hecton8.Core.Contracts.csproj`; recorded the wall instead of editing a separate contract assembly from the Bridge facade pass.
Rejected Alternatives: Creating or rewriting the Core Contracts project from the Bridge domain was rejected as cross-domain project-generation ownership. Editing dozens of non-Bridge consumers to inline constants was rejected as destructive and non-DOD.
Scalability potential: High for integration discipline. Bridge remains independently cold-setter-only while the contract assembly owner restores the missing generated project/reference.
Hardware Impact: 0 us runtime.

## Decision 035 - Boot Binder Serialized Field Honesty
Problem: `H8PrefabRegistryBootBinder` no longer uses `Awake()`, but the serialized inspector field was still named `bindOnAwake`, creating false authoring semantics and confusing static review.
Solution: Renamed the field to `bindOnStart` and added `[FormerlySerializedAs("bindOnAwake")]` so existing scene/prefab values migrate without YAML edits.
Rejected Alternatives: Leaving the stale field name was rejected because Bridge is the human-control layer and its authoring surface must state the actual lifecycle. Raw YAML migration was rejected because Unity serialization attributes are safer.
Scalability potential: Low/Middle/High/Ultra unchanged. The bind remains boot/cold only, with no added frame work.
Hardware Impact: 0 us runtime; serialization metadata only.

## Decision 036 - Current Build Contention And Non-Bridge Fluid Wall
Problem: Fresh isolated Core verification first failed outside Bridge in `SubmarineFluidDynamics.cs` due missing exterior thermal-anomaly fields. Later retries could not produce a stable diagnostic because many concurrent Core builds were running in the same workspace and a file-logged build terminated before compiler diagnostics.
Solution: Kept the Bridge change scoped, reran Bridge static audits, and recorded the active compile state as non-Bridge/blocking rather than claiming Platinum compile.
Rejected Alternatives: Taking ownership of submarine fluid state from the Bridge prompt was rejected as domain overreach. Killing other agents' build processes was rejected without proof they were stale.
Scalability potential: High for integration discipline. Bridge remains a cold DataVault setter/binder while the submarine-fluid owner restores its missing storage contract.
Hardware Impact: 0 us runtime. No profiler microseconds claimed.

## Decision 037 - Live Tuning Stress Gate Semantics
Problem: `H8BridgeFacadeRuntime.LiveTuningBlockedByStress()` mixed `SignalBusRegistry.SystemStress01` with `HomeostasisBrain.SystemHealthIndex01`, even though the Bridge mandate is explicitly `SystemStress01 > 0.9`. The name mismatch made the gate hard to audit and risked suppressing designer live tuning from the wrong semantic lane.
Solution: Gate live tuning on the typed stress lane plus normalized Homeostasis pressure level (`PressureLevel / 3`). The setter remains cold and still bypasses the block when Designer Override is active.
Rejected Alternatives: Polling Homeostasis every frame was rejected because the facade is a setter, not a runtime controller. Keeping the raw `SystemHealthIndex01` read was rejected because it hides stress semantics behind a misleading property name.
Scalability potential: Low tier suppresses live authoring churn only when the runtime is actually under emergency pressure. Middle/High/Ultra retain live tuning while healthy and can still drive raymarch/POM/SSS/particle visual-overkill knobs through packed hashes.
Hardware Impact: 0 us steady-state. Explicit live-edit sync pays two scalar reads and a max operation only when a designer changes a facade value; no profiler microseconds were claimed.

## Decision 038 - Editor Verification Path Honesty
Problem: Isolated `Hecton8.Editor.csproj` builds with custom `OutputPath`/`BaseIntermediateOutputPath` break the generated Unity package project graph: package DLL references are expected in the same generated output layout and several package projects report circular `ResolveProjectReferences`.
Solution: Treat isolated Editor-output failures as invalid verification for this graph. Core is verified with isolated output; Editor must be verified with default Unity project output when the shared workspace is quiet.
Rejected Alternatives: Rewriting generated package csproj references from the Bridge domain was rejected as project-generation sabotage. Claiming the isolated Editor failure as a Bridge compile error was rejected because it fails before Bridge editor code is compiled.
Scalability potential: Medium. Accurate verification discipline prevents false regressions from blocking the design-control layer while preserving generated Unity project ownership.
Hardware Impact: 0 us runtime; build-system verification only.

## Decision 039 - Current World Compile Wall
Problem: After the Bridge stress-gate repair compiled once in isolated Core output, the workspace moved again. Default-output Editor verification and a fresh isolated Core verification now fail outside Bridge in `World/SargassumMicroFaunaBoids.cs` because several storage fields are missing.
Solution: Record the active wall and stop at the Bridge boundary. The missing `_grazingAnchors`, `_formationBeacons`, `_formationObstacles`, and `_massiveThreats` fields belong to the World/Sargassum system, not the Bridge facade/control-panel domain.
Rejected Alternatives: Creating world boid storage from the Bridge prompt was rejected as cross-domain interference with another agent's ownership. Claiming the older green Core result as current was rejected because disk truth changed.
Scalability potential: High for integration discipline. Bridge remains a cold setter/binder with packed visual metadata while World owners restore their simulation storage contract.
Hardware Impact: 0 us runtime from Bridge; compile-wall documentation only.

## Decision 040 - Intentional Empty Facade Tombstones
Problem: `OnValidate()` reseeding and early zero-count returns made "delete every binding" ambiguous. A designer could empty the input or design facade in the inspector, but validation could resurrect defaults or the runtime setter could report success while stale raw Vault rows remained readable.
Solution: Split list initialization from default seeding. Defaults now come from `Reset()` or explicit context-menu seeding, while `OnValidate()` preserves an intentionally empty list. The design facade tracks binding-count changes, and `SyncDesignData()` clears `BridgeDesignFacadeValues`, publishes a heartbeat `DataVaultUpdateSignal`, and persists the MacroDB header when the count is zero.
Rejected Alternatives: Keeping default reseeding in `OnValidate()` was rejected because it makes the control panel fight the designer. Treating zero bindings as a no-op was rejected because raw consumers read Vault buffers, not managed ScriptableObject intent. Clearing the Vault without a typed-lane signal was rejected because runtime listeners must see the same notification path as normal value edits. Adding a managed "active count" side table was rejected because a zeroed Vault lane is cheaper and clearer for DOD consumers.
Scalability potential: Low tier avoids stale input/balance work after controls are removed. Middle/High/Ultra keep the same packed raw lanes and visual-overkill hashes; deletion no longer leaves invisible state that can poison raymarch/POM/SSS/particle controls.
Hardware Impact: 0 us steady-state. Explicit empty sync pays one existing-span `MemClear` and two pointer fences only when the designer intentionally clears a facade. No Unity profiler microseconds were claimed.

## Decision 041 - Typed Dirty Lanes For Non-Design Vault Buffers
Problem: Prefab and input Bridge buffers were written directly into DataVault with telemetry but without a typed dirty signal. That forces consumers to poll raw buffers or depend on side effects, which violates the SignalBus lane mandate and makes empty tombstones harder to observe.
Solution: Reused the existing packed `DataVaultUpdateSignal` lane for `BridgePrefabMapping`, `BridgePrefabLoreLinks`, and `BridgeInputFacadeBindings`. Cold sync paths now publish buffer-level dirty pulses after writes and after empty clears. Clear paths also compute `MemClear` byte counts through `long` multiplication with pointer fences.
Rejected Alternatives: Adding new private input/prefab dirty signals was rejected as interface chaos because `DataVaultUpdateSignal` already exists for buffer mutation. Leaving consumers to poll was rejected because the facade is the setter, not a hidden live state bus. Keeping int-sized clear byte math was rejected because scalable Vault buffers should not rely on pre-widened integer multiplication.
Scalability potential: Low tier avoids polling and stale raw rows. Middle/High/Ultra receive the same dense prefab/input lanes for visual-overkill consumers, PDA lore links, acoustic resonance, and input orchestration without adding per-frame work.
Hardware Impact: 0 us steady-state. Explicit sync/bind pays one or two typed signal pushes and fenced clears only when a designer syncs or a registry binds. No Unity profiler microseconds were claimed.

## Decision 042 - Visor Salt Crystal Control As Facade Data
Problem: The default design facade exposed silt wake, hull dents, raymarch, POM, SSS, and particles, but not the requested visor salt-crystal growth control. That leaves a high-tier visual-overkill feature outside the human-control layer.
Solution: Added `VisorSaltCrystalGrowth01` as a default visual binding at aligned offset 44 with 1D LUT and high-tier visual hashes. The renderer can consume the raw float/hash lane without a string lookup or facade polling.
Rejected Alternatives: Hard-coding salt coverage in a renderer or shader keyword path was rejected because the Bridge facade exists to make these knobs human-controllable. Auto-inserting the binding into every existing asset during `OnValidate()` was rejected because intentional asset lists must stay stable unless the designer resets or explicitly seeds defaults.
Scalability potential: Low tier can map salt growth to a cheap 1D LUT or dot-product mask. Middle can drive a static decal/normal blend. High/Ultra can spend saved cycles on crystalline visor buildup, raymarched wet edges, or particle sparkle while reading the same packed control lane.
Hardware Impact: 0 us steady-state. The new binding is authoring/default data only; setter cost occurs only during explicit facade sync. No Unity profiler microseconds were claimed.

## Decision 043 - Runtime-Only SignalBus Gate
Problem: Manual editor/window sync paths can call the same cold setters as play mode. Without an explicit play-mode gate, a valid edit-mode Vault could cause Bridge SignalBus pushes outside runtime, contradicting the assignment's "OnValidate in Editor and SignalBus in Runtime" split.
Solution: Added play-mode guards around Bridge DataVault dirty signals and cached `Application.isPlaying` inside the prefab binder loop before acoustic/lore signal publication. DataVault writes and telemetry remain cold setter behavior; typed SignalBus traffic is runtime-only.
Rejected Alternatives: Leaving editor SignalBus pushes in place was rejected because it creates hidden authoring-time queue traffic. Adding separate editor events was rejected because editor tooling already has direct inspector/window refresh paths and the runtime lane should stay typed and singular.
Scalability potential: Low tier avoids edit-mode queue churn and preserves zero steady-state cost. Middle/High/Ultra keep runtime dirty pulses for prefab, lore, acoustic, input, and design consumers without adding frame polling.
Hardware Impact: 0 us steady-state. The guard is evaluated only on explicit sync/bind paths, not per frame. No Unity profiler microseconds were claimed.
