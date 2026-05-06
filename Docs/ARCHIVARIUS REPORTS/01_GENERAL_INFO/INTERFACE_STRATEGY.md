# INTERFACE STRATEGY

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: ownership strategy for `GlobalRegistryContracts.cs` after source recheck
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## 1. Current Position

The older "ghost interface" framing is no longer accurate.
Current source scan found a direct implementor for every interface in `GlobalRegistryContracts.cs`.

That changes the strategy.

The problem is no longer "which contracts are dead".
The problem is "which contracts now have real owners and must stop being documented as unresolved".

## 2. Confirmed Non-Ghost Contracts

### 2.1 `IAudioService`

Current direct owner:

- `SpatialAudioManager`

Implication:

- future docs must stop describing `IAudioService` as a ghost slot
- "audio extensions" should not be casually documented as alternate service owners unless code actually moves that authority

### 2.2 `IUIService`

Current direct owner in source scan:

- `SuitHUDV4CanvasOverlay`

Implication:

- the contract is not absent
- the contract is not currently fragmented at class-declaration level
- if a future bootstrap-level UI root replaces it, docs must name that new root explicitly instead of drifting back into vague "multiple owners" language

### 2.3 `IRenderable`

Current direct owners rechecked:

- `HectonUnderwaterVisuals`
- `HectonSubmarineOS`
- `MissionMarkerSystem`

Implication:

- `IRenderable` is a legitimate render-dispatch hook
- no deletion campaign is justified from the current source picture alone

### 2.4 `IDamageReceiver`

Current direct owner rechecked:

- `HabitatIntegrityManager`

Important correction:

- the older shadow-conflict story is stale
- the file now separates habitat-specific callback contracts into:
  - `IDamageSignalReceiver`
  - `IDamageSignalEmitter`
- `HabitatIntegrityManager` also implements the global `Hecton8.Core.IDamageReceiver`

Implication:

- docs must stop treating this as an unresolved ABI-level type conflict unless a new conflicting declaration appears again

## 3. Strategy Rules Going Forward

### Rule 1: No New "Ghost" Label Without Fresh Source Proof

Do not mark an interface as ghost unless current source scan proves:

- zero direct implementors
- or a real conflicting duplicate definition still exists

### Rule 2: Separate Source Ownership From Runtime Proof

An implementor existing in code does not prove:

- the scene contains it
- bootstrap registers it
- no other scene object competes for the same slot

So the correct wording is:

- source-backed owner confirmed
- runtime registration still pending verification

### Rule 3: Keep Single-Owner Service Contracts Explicit

These contracts should remain documented with one named owner unless code actually changes:

- `IAudioService` -> `SpatialAudioManager`
- `IUIService` -> `SuitHUDV4CanvasOverlay`
- `IQuestSystem` -> `QuestManager`
- `IEncounterDirectorService` -> `HectonDirectorAI`
- `ILogisticsService` -> `ConstructionManager`
- `IWorldGenService` -> `WorldProceduralScatterDirector`

### Rule 4: Do Not Inflate Narrow Usage Into Defect By Default

A contract with one or a few implementors is not automatically sick.
It is only a defect if:

- the contract owner is ambiguous
- the contract is unused
- the contract is contradicted by scene/bootstrap reality

That proof was not established for `IRenderable`, `IAudioService`, or `IUIService` in this pass.

## 4. Recommended Documentation Policy

| Priority | Action | Reason |
|---|---|---|
| P0 | purge ghost/fragmented claims from dependent docs | current source already disproves them |
| P1 | keep one authoritative owner named for each registry-facing service contract | prevents drift and folklore |
| P1 | when Unity evidence exists, add runtime occupancy notes separately from source ownership | avoids mixing static facts with live-state assumptions |
| P2 | rerun interface audit after any `GlobalRegistryContracts.cs` expansion | current May 6 source count is 34 direct public interfaces; older 19/27/31/33 snapshots are stale and coverage must be recounted before claims |

## 5. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: no runtime code changed
Correctness: improved by replacing stale ghost/conflict framing with current source-backed ownership strategy

## 6. Hot Path Impact

None. Markdown-only change.

STATUS: PENDING VERIFICATION
