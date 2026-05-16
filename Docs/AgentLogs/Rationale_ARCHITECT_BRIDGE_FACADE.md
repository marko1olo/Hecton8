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
