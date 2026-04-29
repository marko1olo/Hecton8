# HECTON-8 EVENT FLOW MAP

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: source-backed event topology visible in first-party code
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## 1. Audit Standard

This file records only event flows directly rechecked in current source.
It does not claim runtime replay, scene-wiring proof, or exhaustive subscriber coverage.

Evidence basis for this pass:

- direct reads of bus definitions
- direct scans for `NativeQueue<TPayload>` ownership
- direct scans for `Raise*` and `FlushPending()` paths
- direct scans for remaining static `Action` buses

## 2. Core Finding

The project is in a mixed event architecture state.

It currently contains:

- queue-backed deferred buses
- direct static `Action` buses
- feature-local direct callbacks
- a separate managed modding bus

There is no single event model yet.
There is a partial migration plus legacy/static residue.

## 3. Queue-Backed Deferred Buses Rechecked

### 3.1 `SaveEvents`

Definition rechecked in `Assets/_Project/Scripts/SaveEvents.cs`

Confirmed properties:

- `NativeQueue<SaveEventPayload>` backing store
- `RegistryBucket<ISaveEventListener>` listener registry
- `FlushPending()` fanout path
- `SaveEventPayload` uses:
  - `SaveEventType`
  - `FixedString64Bytes SlotName`
  - `FixedString128Bytes Message`

Correction:

- older doc claim that this bus was direct static `Action` dispatch was false

### 3.2 `QuestEvents`

Definition rechecked in `Assets/_Project/Scripts/Quest/QuestEvents.cs`

Confirmed properties:

- `NativeQueue<QuestEventPayload>` backing store
- `RegistryBucket<IQuestEventListener>` listener registry
- `FlushPending()` fanout path
- compact payload with quest hash + event type

### 3.3 `ScanEvents`

Definition rechecked in `Assets/_Project/Scripts/ScanEvents.cs`

Confirmed properties:

- `NativeQueue<ScanEventPayload>` backing store
- `RegistryBucket<IScanEventListener>` listener registry
- hash-based payload fields for entry/title/category/summary
- cold-path `Dictionary<uint, ScanEntryMetadata>` used for authored string recovery

Correction:

- older claim that `OnEntryDiscovered` was a public direct `Action<string, string, string, string>` is false in current source

### 3.4 `NarrativeEvents`

Definition rechecked in `Assets/_Project/Scripts/NarrativeEvents.cs`

Confirmed properties:

- `NativeQueue<NarrativeEventPayload>` backing store for discovery/depth events
- `RegistryBucket<INarrativeEventListener>` listener registry
- hashed discovery payload via `DiscoveryHash`
- cold-path `Dictionary<uint, string>` for authored discovery id recovery

Important nuance:

- `NarrativeEvents` also contains a separate direct point-of-interest callback lane:
  - `RegisterPointOfInterestListener`
  - `RaiseNarrativePOIRegistered`
  - `RaiseNarrativePOIDisposed`

So this file is not pure queue-only architecture.
It is a hybrid event owner.

### 3.5 `AudioLogEvents`

Definition rechecked indirectly by file readback and direct search hits in `Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs`

Confirmed properties:

- `NativeQueue<AudioLogEventPayload>` backing store
- `RegistryBucket<IAudioLogEventListener>` listener registry
- `FlushPending()` deferred dispatch path

## 4. Direct Static `Action` Buses Still Present

### 4.1 `InteractionEvents`

Definition rechecked in `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`

Confirmed properties:

- direct static `event Action<ItemData, int, Transform> OnItemCollected`
- direct static `event Action<IInteractable, Transform> OnInteractionStarted`
- direct static `event Action<IInteractable> OnHoverChanged`
- immediate delegate invocation in `Raise*` methods

### 4.2 `CraftingEvents`

Definition rechecked in `Assets/_Project/Scripts/CraftingEvents.cs`

Confirmed properties:

- direct static `Action` events
- immediate `Raise*` delegate invocation
- not queue-backed

Signals visible in current source:

- `OnFabricatorOpened`
- `OnFabricatorClosed`
- `OnCraftStarted`
- `OnCraftProgressUpdated`
- `OnCraftCompleted`
- `OnCraftCancelled`

### 4.3 `HectonSubmarineOsEvents`

Definition rechecked in `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`

Confirmed properties:

- direct static delegate events
- `OnSnapshotUpdated`
- `OnLogRequested`
- direct invocation, not queue-backed

### 4.4 Other Feature-Embedded Buses

Current codebase still contains multiple feature-local event surfaces such as:

- `PDAEvents`
- `FlashlightEvents`
- `RandomEventEvents`
- celestial/weather feature buses

Not all of those were fully re-mapped in this pass.
Their existence is confirmed.
Their full subscriber tables were not re-authored here because this pass prioritized factual correction over speculative completeness.

## 5. Separate Modding Bus

`Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` remains a separate typed managed bus.

Observed boundary:

- it is not the owner of first-party queue-backed gameplay buses
- it is not the canonical replacement for `SaveEvents`, `QuestEvents`, `ScanEvents`, or `NarrativeEvents`
- it remains an additional communication surface, not the central one

## 6. Conformity Findings Against `AGENTS.md`

### 6.1 What Already Moved Toward The Mandate

These buses now match the direction required by `AGENTS.md` much better than older docs admitted:

- `SaveEvents`
- `QuestEvents`
- `ScanEvents`
- `NarrativeEvents`
- `AudioLogEvents`

They are queue-backed and late-flush oriented.

### 6.2 What Still Drifts

Direct static delegate buses still remain in first-party gameplay/UI surfaces:

- `InteractionEvents`
- `CraftingEvents`
- `HectonSubmarineOsEvents`
- several feature-local embedded buses

So the event architecture is still only partially normalized.

### 6.3 String-Payload Risk Was Overstated In Older Docs

Older docs described several migrated buses as still string-heavy direct-action surfaces.
Current source shows the more accurate picture:

- `SaveEvents` payload is fixed-string based
- `QuestEvents` payload is hash/ushort based
- `ScanEvents` payload is hash-based with cold metadata cache
- `NarrativeEvents` payload is hash-based with cold id cache

This does not prove zero runtime alloc everywhere.
It does prove the older doc description was stale.

## 7. What Was Removed From The Old Version

Removed as unsupported or stale:

- claim that `SaveEvents` was static direct `Action`
- claim that `ScanEvents` published public string-based delegate payloads
- claim that `NarrativeEvents.OnDiscoveryMade` remained direct string dispatch
- inflated certainty about complete subscriber maps not revalidated in this pass

## 8. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: no runtime cadence changed
Correctness: documentation accuracy improved by separating migrated queue buses from legacy direct static buses

## 9. Hot Path Impact

None. Markdown-only change.

## 10. Failure Modes

- some subscriber hookups remain outside this static source pass
- scene/prefab wiring can still add listeners not visible in class-only scans
- live event cadence and leak behavior still require Unity-side validation

## 11. Why This Version Was Kept

Kept because it removes false negatives and false positives at the same time.
It does not pretend the event layer is clean.
It records the actual mixed state visible in source.

STATUS: PENDING VERIFICATION
