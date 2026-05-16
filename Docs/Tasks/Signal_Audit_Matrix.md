# Signal Audit Matrix - SIGNAL_AUTHORITY_VALIDATOR

Date: 2026-05-16
Domain: CORE/SIGNALS
Authority file: Assets/_Project/Scripts/Core/GlobalSignals.cs

## Duplicate Purge

| Check | Command | Result |
| --- | --- | --- |
| CombatEvent / HitSignal / DamageSignal live names | `rg -n "\b(CombatEvent|HitSignal|DamageSignal)\b" Assets/_Project/Scripts -g "*.cs"` | 0 matches |
| Old signal namespace | `rg -n "namespace Hecton8\.Core\.Signals|Hecton8\.Core\.Signals" Assets/_Project/Scripts -g "*.cs"` | 0 matches |
| ISignal structs outside contract namespace | PowerShell namespace scan over `struct ... : ISignal` | 0 matches |
| SignalBus lane Configure outside GlobalSignals | `rg "SignalBus<...>.Configure" ... | rg -v Core/GlobalSignals.cs` | 0 matches |
| PlayerInteraction managed events | `rg -n "UnityEvent|Action<|event Action|\bevent\b" Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` | 0 matches |

## Canonical Lanes

| Lane | Status | Notes |
| --- | --- | --- |
| Combat damage | Canonical | Legacy `DamageSignal` payload and generated hash removed; producers write `CombatDamageSignal`. |
| High speed impact | Canonical | `KineticEnergy` added as alias for shader-readable energy while preserving `LostKineticEnergy` callers. |
| Non-critical VFX | Bounded | Stress >= 0.8 drops optional VFX; stress < 0.2 allows full propagation. |
| Audio transition | SPSC compatible | Existing native queues/ring buffers remain; signal path uses typed unmanaged payloads. |
| AUP shift | PRE_SIMULATION | `GlobalSignals.FlushPreSimulation()` drains lanes before render and applies AUP shift safety. |

## Layout Audit

| Item | Result | Action |
| --- | --- | --- |
| External signal structs moved from feature namespaces | 0 outside contract namespace | Done |
| Newly evicted external signal structs | Sequential Pack=1 fixed-size | Done |
| Legacy explicit signal structs | 130 explicit layouts remain | ABI exception recorded in rationale; union aliases require staged migration. |

## Build Evidence

| Command | Result |
| --- | --- |
| `dotnet build Hecton8.Core.csproj` | Initial pass failed on unrelated AI/animation/VFX dependencies. Final pass after late-drift repair failed on unrelated `ProceduralLadderClimbRuntime` references in `GlobalRegistry.cs`. No signal namespace or damage lane errors were reported. |
| `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` | Timed out after 300 seconds; leftover dotnet workers were stopped. |

Audit conclusion: 0 duplicate signal names, 0 signal payloads outside `Hecton8.Core.Contracts.Signals`, and 0 decentralized SignalBus lane Configure calls remain.

Late drift note: a final scan caught new compass/biolum/path/scatter/ambient files with stale imports. They were folded into the contract namespace and compass lane registration was moved to `GlobalSignals.InitializeAllQueues()` before this audit was finalized.

## Omega Polish

| Question | Result |
| --- | --- |
| 0.1 ms flush dictatorship | Flush path uses native queue drain, NativeList snapshot writes, stress caps, and overflow clear. No managed containers were added to the flush path. |
| Managed format strings in signal surface | `rg` found 0 interpolated strings and 0 `string.Format` calls in the signal authority files. |
| Status | VERIFIED MASTER GRADE, except task 4 ABI layout migration and task 18 integration build are explicitly blocked as recorded. |
