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
