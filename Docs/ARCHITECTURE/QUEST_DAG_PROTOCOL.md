# QUEST DAG Protocol
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this protocol as current runtime truth.
- This document is a quest-state architecture contract, not proof that all authored quests, event signals, save bits, or UI surfaces are integrated.
- Re-open `QuestStateManager`, narrative events, save owners, and current quest data before surgery.

## Scope
Runtime quest progression in HECTON-8 is a compiled directed acyclic graph backed by a bit-packed `NativeArray<uint>`. Quests are not `MonoBehaviour` state machines. They are precompiled descriptors evaluated only when an event signal arrives.

Mandates followed:
- `PROG_Quest_State_Graph_Logic`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`

## Packed State Layout
`QuestStateManager` owns one persistent `NativeArray<uint>` with `320` words. Each word is `32` bits. Total capacity: `10,240` flags.

Word bands:

| Word Range | Band | Purpose |
| --- | --- | --- |
| `0..63` | `Quest` | quest active and quest completed bits |
| `64..127` | `Item` | collected and lost item flags |
| `128..191` | `Location` | biome and location flags |
| `192..223` | `Narrative` | discovery, audio-log, decoded-signal flags |
| `224..255` | `Phase` | depth thresholds, eclipse, abyssal, thermal |
| `256..287` | `EntityDestroy` | destroyed critical-item flags |
| `288..319` | `Deadlock` | revert/deadlock recovery flags |

Each logical flag resolves to a `QuestBitAddress`:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct QuestBitAddress
{
    public int WordIndex;
    public uint BitMask;
    public uint FlagId;
}
```

`FlagId` is the stable FNV-1a hash. `WordIndex` and `BitMask` are the compiled O(1) packed address.

## Hash Contract
All narrative, quest, item, and marker target IDs must resolve through the stable FNV-1a kernel in `QuestFlagHashKernel`. The runtime does not use string comparisons in the signal lane.

```csharp
public static uint ComputeStableHash(ReadOnlySpan<char> value)
{
    if (value.Length <= 0)
        return 0u;

    unchecked
    {
        uint hash = Hecton.Localization.LocHash.FnvOffsetBasis;
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            hash ^= (byte)current;
            hash *= Hecton.Localization.LocHash.FnvPrime;
            hash ^= (byte)(current >> 8);
            hash *= Hecton.Localization.LocHash.FnvPrime;
        }

        return hash;
    }
}
```

This path allocates no temporary arrays. It hashes the UTF-16 bytes of the source span in place.

## Compiled Graph
Each authored quest becomes one or more `QuestNodeDescriptor` records. Activation and completion are separate nodes when both are event-driven.

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct QuestNodeDescriptor
{
    public uint QuestHash;
    public uint PayloadHash;
    public uint PrereqMask;
    public uint CompletionFlagID;
    public uint FailureFlagID;
    public uint RevertFlagID;
    public uint PhaseGate;
    public uint ActiveFlagID;
    public uint CriticalItemHash;
    public int PrereqStartIndex;
    public ushort PrereqWordIndex;
    public ushort ReservedWordIndex;
    public float RequiredValue;
    public uint ActiveMask;
    public uint CompletedMask;
    public uint SetMask;
    public uint ClearMask;
    public byte PrereqCount;
    public byte SignalKind;
    public byte TransitionType;
    public byte Reserved;
    public int QuestIndex;
    public int ActiveWordIndex;
    public int CompletedWordIndex;
    public int SetWordIndex;
    public int ClearWordIndex;
}
```

`QuestHash` maps back to the authored `questId` hash.

`PayloadHash` maps the incoming signal payload to the node.

`PrereqMask` and `PrereqWordIndex` allow the fast-path gate:
- O(1): `(flags[word] & mask) == mask`

When prerequisites span multiple words, the runtime falls back to the flattened `QuestPrerequisiteDescriptor` list.

## Procedural Directives
`QuestStateManager` reserves a fixed procedural pool at initialization:
- `8` procedural quest slots
- `8` procedural completion nodes
- no runtime `NativeArray` growth

`ResourceScarcityDirector` does not allocate or append arrays during play. It computes a deterministic scarcity quest hash per essential resource and calls the procedural registration seam on `QuestManager`, which routes into `QuestStateManager.TryUpsertProceduralDirective(...)`.

If no authored scarcity row covers Titanium Scrap, `ResourceScarcityDirector` seeds one runtime fallback definition for `Data_TitaniumScrap` so the structural-survival directive lane still exists in production scenes that missed authoring.

Runtime shape:
- quest slot addresses are reserved up front in the `Quest` band
- completion node payload is the harvested resource hash
- completion signal kind stays `ItemCollected`
- marker metadata is cached alongside the quest slot

Fast path:
- activation is a direct active-bit set when stock is below threshold
- completion is the existing DAG completion node evaluation
- recurring scarcity directives reuse the same quest hash and slot instead of creating duplicates

## Event Path
`QuestGraphEvaluator` is the runtime ingress owner.

Signal path:
1. `HectonEventBus`, `NarrativeEvents`, `AtlasSignalEvents`, and eclipse hooks publish runtime signals.
2. The evaluator converts them into blittable `QuestSignalPayload`.
3. Payloads are enqueued into a persistent `NativeQueue<QuestSignalPayload>`.
4. `QuestEvents.FlushPending()` triggers `QuestGraphEvaluator.FlushPendingSignals()` during the existing LateUpdate flush lane.
5. `QuestStateManager.EvaluateSignal(...)` runs the Burst job against the compiled node array.
6. The facade drains `QuestRuntimeResult` and emits UI/event fan-out.

There is no polling loop.

## Phase Gates
Two persistent phase bits are reserved up front in the `Phase` band:
- `phase.abyssal`
- `phase.thermal`

They are registered before any quest node compilation so manual quests and triggered quests share the same packed addresses.

Signal-driven activation already carries phase prerequisites through compiled node masks. Manual activation uses the direct phase word fast path:

```csharp
(flags[phaseSlot] & requiredPhaseMask) != 0
```

Current authored locks:
- `quest_atlas_signal_detected` â†’ `Abyssal`
- `quest_atlas_signal_decoded` â†’ `Thermal`
- `quest_atlas_core_reached` â†’ `Thermal`

Rationale:
- `quest_atlas_signal_detected` is gated behind deep-abandonment progression.
- `quest_atlas_signal_decoded` and `quest_atlas_core_reached` sit on `4500m` and `4800m` depth goals, which align with the authored thermal tier range already present in the world/audio stack.

## Anti-Deadlock Revert Kernel
Critical-item quests compile a `QuestRevertDescriptor` with:
- critical item hash
- entity-destroy flag
- deadlock flag
- active flag
- completed flag
- respawn event hash
- quest index

When the runtime receives an item-loss path for a critical quest item:
1. Set the `EntityDestroy` bit.
2. Set the `Deadlock` bit.
3. Clear the quest `Completed` bit.
4. Re-set the quest `Active` bit.
5. In `DEVELOPMENT_BUILD` only, append a short text audit line.
6. Emit `QuestRevertRequest` so the external spawn owner can re-spawn the item.

This prevents permanent story deadlocks caused by destruction or discard of a quest-critical item.

## Transition Audit
`QuestStateManager` stores no transition history and no state snapshots.

Deleted runtime allocations:
- `NativeArray<QuestTransitionHistoryEntry>`
- `NativeArray<uint>` history slab sized as `256 * 320`

Runtime persistence keeps only the current 320 packed state words, version, and checksum.

Development audit is a cold path:
- compiled behind `DEVELOPMENT_BUILD`
- appends plain text to `quest_transition_audit.log`
- format: `[{Time}] Quest {ID} -> {State}`

There is no runtime state restoration from transition history.

## Mission Markers
`MissionMarkerSystem` renders active quest markers through `Graphics.DrawMeshInstanced`.

Rules:
- no `GameObject` markers
- no string lookups in the hot path
- active quest hashes are copied into a fixed buffer
- marker targets resolve through hashed marker IDs or authored fallback positions

Current authored marker target:
- `atlas6_core`

Atlas quests resolve this hash to `AtlasSignalSystem.AtlasCorePosition` and render the instanced icon there.

Procedural scarcity directives resolve markers in this order:
1. explicit hashed target ID from the directive definition
2. nearest remembered harvest cluster for that resource
3. no marker if neither source exists

Remembered clusters are fixed-capacity records owned by `ResourceScarcityDirector`; they are updated from real `ItemCollectedEvent` world positions.

## Eco-Hostility Side Channel
`EcosystemDirector` owns a separate normalized `BiomeHostility01` scalar. It is not a quest flag and does not pollute the DAG bands.

Current path:
1. player-proximate apex predator death reports into `IEcosystemDirectorService.ReportApexPredatorKilled(...)`
2. hostility rises and is clamped in `EcosystemDirector`
3. hostility decays only on `SlowTick`
4. `EcosystemDirector` pushes external peak pressure into `HectonDirectorAI`
5. notification UI warns when hostility crosses tier bands

This keeps ecology pacing separate from quest-state persistence while still letting the abyss react to repeated apex kills.

## Crafting Inflation
`ResourceScarcityDirector` already owns sector-local extraction pressure. The same fixed-capacity `SectorExtractionRecord[64]` table now also drives fabrication surcharge.

Runtime formula:
- `valueScalar = inflationProfile.EvaluateValueScalar(itemHashId, extractedUnits)`
- `craftInflationScalar = 1f - valueScalar`
- `adjustedAmount = ceil(baseAmount * (1f + craftInflationScalar))`

Rules:
- `Fabricator` is the single runtime owner for adjusted ingredient counts.
- `CanCraft`, reclaim-cell checks, local/network reservations, and diegetic hologram counts all read the same adjusted amount path.
- `HectonFabricatorUI` renders the surcharge as a dedicated red multiplier label via `SetCharArray`; no hot-path string formatting is introduced for the inflation display.

## Hunter Squad Override
When `BiomeHostility01 >= 0.8`, `HectonDirectorAI` issues a forced Stalker squad request into `EncounterDirector`.

Rules:
- no second fauna spawn owner is introduced
- the encounter kernel still owns spawn placement, tracking, token accounting, and despawn
- forced spawns bypass normal pacing selection but still enter through the existing encounter output lane
- forced squad candidates prefer the far edge of the spawn ring so hunters emerge at the fog boundary and close in over successive cold ticks

## Decoder Progress
The final Atlas decode is a scalar progress gate. Strength phase `4` opens the decode window.

Decode contract:
- `Progress += UnpackSpeed * dt`
- clamp progress to `0..1`
- when `Progress >= 1.0`, emit the decoded Atlas signal

No waveform sample domain, sine comparison, dial solve, or decode buffer exists.

## Failure Modes
- If no external owner consumes `QuestRevertRequest.RespawnEventHash`, the quest will revert but the lost item will not physically respawn.
- Quests without a marker target hash or fallback marker position will not render a mission marker.
- Full-project compile health is external to this protocol. The quest DAG can validate cleanly while unrelated workspace errors still block a global clean console.
