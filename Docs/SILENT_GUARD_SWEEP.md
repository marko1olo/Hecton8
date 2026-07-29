# SILENT GUARD SWEEP

Status: STATIC_REVIEW + ONE LIVE-LOG CONFIRMATION
Evidence class: source read (`Assets/_Project/Scripts`), plus `Logs/h8_worldsim_probe5.log` for the
currently-firing instances. No compile proof, no Unity run, no profiler. Nothing here is `VERIFIED`.
Owner: lane `silent-guard-sweep`
Scope of edits made: this file only. No `.cs` touched.

## What this hunts

One failure shape: **a check that records failure into a mask, a bool, or a `return`, and says nothing on
success.** When such a guard passes, and when it never runs at all, the log is byte-identical — empty. A
hypothesis about it cannot be disconfirmed by a run, only re-argued. The `PlayerInventory` DTO layout guard
cost two sessions and reached commit `2f8d44d2d` as established fact before a standalone-CLR lane proved it
could not fail at all; the very next run, after the guard was made to speak on success, printed the real
cause in one line.

Sub-shapes, lettered as in the lane brief:

| | shape |
|---|---|
| a | silent-success guard: sets a failure bit, returns bool, logs nothing on the happy path |
| b | combined pre-guard that returns BEFORE the call that would have logged |
| c | non-null NULL-OBJECT accepted by a null check |
| d | `catch { }` swallowing a failure whose only symptom appears later and elsewhere |
| e | min/max fold seeded with a sentinel that can never lose |
| f | a parameter accepted and then IGNORED |
| g | a DERIVED value printed under the name of the thing it was derived FROM |

## Counts

Candidate population found by scoped `rg` over `Assets/_Project/Scripts` (3204 `.cs`, 2 255 633 lines):

| pattern | hits | files |
|---|---|---|
| `if (!Application.isPlaying \|\| GlobalRegistry.{Dispatcher,TickDispatcher} == null)` | 108 | 90 |
| `GlobalRegistry.TryRegister*` **lines** falling within 4 lines of one of those guards | 84 | 68 |
| `if (!Application.isPlaying \|\| <expr> == null)` (broader form) | 139 | — |
| `GlobalRegistry.TryRegister*{Updatable,Tickable}` call sites (excl. `GlobalRegistry.cs`) | 817 | — |
| ...of those, result consumed by an `if` | 35 | — |
| empty `catch (…) { }` blocks | 1152 | 148 |
| `catch (Exception)` with the exception object discarded (no variable bound) | 303 | — |
| `failureMask`-style self-audit guards returning `mask == 0` | 12 | 12 |
| `float/int.{MinValue,MaxValue}` occurrences | 2581 | — |
| ...seeded folds named best/min/max/nearest | 283 | — |

**Kept instances: 14.** Everything else was discarded as harmless silence, or as a correct usage. In
particular I discarded the entire shape-(e) population: all 283 sampled sentinel folds are seeded the right
way round (min-folds with `MaxValue`, max-folds with `MinValue`). Naming made them look suspicious;
inspection did not. I also discarded `Tools/PerformanceBudgetController.cs` — its five `[Range]` fields
(`_throttleMultiplier`, `_globalQualityInfluence`, `_biolumBudget`, `_microfaunaBudget`, `_terrainBudget`)
all reach real arithmetic at `:359-385`, so they are not shape (f). A long list of trivia is worse than a
short list of real ones, because it will not be read.

---

# PART 1 — CURRENTLY HIDING SOMETHING

These are not "capable of hiding". They fired in the 2490-frame run in `Logs/h8_worldsim_probe5.log`.
They are next week's bugs and they are worth more than the rest of the list combined.

## 1. `NoOpAudioService.IsInitialized => true` — the null object that reports healthy

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:9040` — `public bool IsInitialized => true;`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1573` — `bool IsAudioRuntimeReady => IsInitialized;`
- Shape: **c + g.** Blast radius: the entire audio subsystem, plus every readiness gate that reads it.

`NoOpAudioService` is the fallback injected when the audio bootstrap owner cannot initialize. Every method
is a no-op or `return false` (`:9049-9096`). Its readiness bit is hardcoded `true`.

The consequence chain, all in current source:

1. `IsAudioRuntimeReady` has a **default interface implementation** that simply aliases `IsInitialized`
   (`GlobalRegistryContracts.cs:1573`). `NoOpAudioService` does not override it, so it is `true`.
2. `IsBootstrapAudioServiceUsable` (`GameBootstrapper.cs:6580-6589`) tests exactly
   `audioService == null || !audioService.IsAudioRuntimeReady`. The no-op passes.
3. `TryRegisterNoOpAudioFallback` therefore **returns true** (`:6577`), so
   `InitializeSpatialAudioBootstrapNode` returns true (`:6543`, `:6554`, `:6558`), so the bootstrap
   dependency graph records `SpatialAudioManager` as **satisfied**.
4. Every consumer gate is `audioService == null || !audioService.IsInitialized` — it only rejects null, so
   it accepts the no-op: `AcousticZoneController.cs:1312`, `BaseModule.cs:5253`,
   `ConstructionManager.cs:616`, `Fabricator.cs:4410`. Each then queues audio into a method that returns
   `false` and drops it.

This is shape (c) in its worst orientation. The known instance of (c) this session — the cached
`IInputDeterminismService` holding a `NoOpInputService` whose `IsInitialized` is hardcoded **false**
(`GlobalRegistry.cs:8428-8430`) — at least reported unready and could be caught by an audit. This one
reports *ready*. And it is shape (g) on top: `IsAudioRuntimeReady` is a second name for one bit, and
`SpatialAudioManager.cs:1739-1744` overrides it with a real five-term conjunction, so the *same property
name* means "five invariants hold" on the real owner and "yes, always" on the fallback.

**Live proof:** `Logs/h8_worldsim_probe5.log:1942` — `[GameBootstrapper] Injected NoOp audio service.`
Then `:1989` — `[GameBootstrapper] Waiting for heartbeat for node SpatialAudioManager`, i.e. bootstrap
proceeded to wait on a node it had already replaced with a stub. Then `:19186` —
`[GlobalRegistry] Unregister mismatch for IAudioService.` from `SpatialAudioManager:ShutdownServiceState`
(`SpatialAudioManager.cs:1666`): the real manager tried to unregister itself and found the stub in the
slot. That mismatch line is the *only* place the swap resurfaces, 17 000 lines later, under a name that
does not mention audio bootstrap.

Every audio verdict in that run is UNMEASURED, not negative — the same status the driver assigned to input.

## 2. `TryRegisterNoOpAudioFallback(string reason)` accepts `reason` and never uses it

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6562-6578`
- Shape: **f + g.** Blast radius: the diagnosability of finding 1.

Three distinct causes call it with three distinct strings:

- `:6543` `"SpatialAudioManager missing"` — no component found in the bootstrap hierarchy
- `:6554` `"SpatialAudioManager did not register IAudioService"` — component ran but never claimed the slot
- `:6558` `"SpatialAudioManager init exception"` — `InitializeService()` threw

All three collapse to the single line `LogOptionalBootstrapWarning("Injected NoOp audio service.")` at
`:6574`. The parameter is dead. One bit wearing three names, and the three demand opposite fixes: author a
component, fix a registration, or fix a throw.

In the probe run the cause is recoverable **only by accident** — Unity attached a stack trace whose frame
`GameBootstrapper:InitializeSpatialAudioBootstrapNode () (at .../GameBootstrapper.cs:6554)`
(`h8_worldsim_probe5.log:1951`) pins it to cause 2. Remove stack traces, or read the line rather than the
trace, and the cause is gone.

Worse: `LogOptionalBootstrapWarning` is
`[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` (`:6591`), and the call site at `:6574`
is *additionally* wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. In a release player the entire audio
subsystem can be replaced by a stub that reports ready, and **not one byte is logged**.

## 3. `catch (Exception)` at the audio bootstrap discards the exception

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6556-6559`
- Shape: **d.** Blast radius: the root cause of finding 1, whenever cause 3 is the live one.

```csharp
catch (Exception)
{
    return TryRegisterNoOpAudioFallback("SpatialAudioManager init exception");
}
```

Not an empty catch — which makes it worse, because it looks handled. The type, message, and stack of
whatever `SpatialAudioManager.InitializeService()` threw are destroyed, and the replacement message does
not even reach the log (finding 2). This is the `run.flag`-inside-`catch{}` class: the failure is swallowed
and its only symptom appears later and elsewhere, as a silent audio subsystem.

There are **303** `catch (Exception)` sites in this tree that bind no variable. This one is called out
because it sits directly upstream of a proven live degradation.

---

# PART 2 — SYSTEMIC, RANKED BY BLAST RADIUS

## 4. 108 combined pre-guards still suppress the only dispatcher diagnostic that exists

- 108 guard sites across 90 files. **84** `GlobalRegistry.TryRegister*` lines, in **68** distinct files,
  sit within 4 lines of one of these guards — i.e. the guard returns before the lane claim that would have
  logged. (84 is a line count, not a site count: `Gameplay/MountablePlayerTransport.cs:2341-2342` and
  `Tools/ToolKinematics/ToolKinematicsRuntime.cs:1604`/`:1613`/`:1622` each put several lane claims behind
  one guard. The site count is between 68 and 84 and I did not separate it further.)
- Suppressed diagnostic: `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7067`
- Shape: **b.** Blast radius: **the largest on this list** — one tick lane per site, and a lane is a
  whole system.

This is the exact shape that froze `HectonSurvivalSystem` for a full session with zero evidence. The
in-source post-mortem is at `HectonSurvivalSystem.cs:823-846` and it names the mechanism precisely: *"the
old combined pre-guard returned before `TryRegisterSlowTickable`, so GlobalRegistry's own 'SystemDispatcher
is not registered' error (GlobalRegistry.cs:7067) never fired — the guard suppressed the one diagnostic that
existed for this failure."*

**That fix was applied to one file. The shape survives at 107 other sites.** A representative sample, all
of the form `if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;` immediately above a
lane claim:

| site | lane lost when it trips |
|---|---|
| `Core/InputDispatcher.cs:3184` → `:3188` | `LateFrameTickable`, Core — input frame publication |
| `PlayerInventory.cs` (guard) → `:5876`, `:5917` | Slow + LateFrame, Player — inventory |
| `Quest/QuestManager.cs` → `:486` | LateFrame, Player — quest completion |
| `HectonPlayerMovement.cs:5436` | locomotion |
| `HectonFluidEngine.cs:2254` | fluid |
| `HectonFloatingOrigin.cs:2106` | AUP origin shift |
| `Gameplay/MountablePlayerTransport.cs:2341-2342` | Fixed + Update, Player — vehicles |
| `Tools/ToolDurabilitySystem.cs:1738`, `:1757`, `:1776` | three lanes at once |
| `Gameplay/HabitatIntegrityManager.cs:687` | Slow, Core — habitat integrity |
| `Gameplay/EndingSystem.cs:1022` | Slow — endgame |
| `World/DepthZoneDirector.cs:999` | Slow — depth zones |
| `Physics/Vehicles/SubmarineDynamicsRuntime.cs:524` | Fixed — submarine |
| `ObjectPoolManager.cs:1438` | Update, Core — pooling |

Note that `GlobalRegistry.TryRegisterSlowTickable` (`:6540-6560`) *already* checks
`!Application.isPlaying` itself at `:6545`. The caller's `isPlaying` half of the pre-guard therefore buys
nothing; its only effect is to suppress the diagnostic on the null-dispatcher half. The guard is pure loss.

Aggravating factor: the diagnostic it suppresses is latched globally
(`_dispatcherRegistrationErrorLogged`, `GlobalRegistry.cs:7064`). Even at a site that does reach it, the
error fires **once per session for the whole project**, and it names the dispatcher, not the owners that
lost lanes. If 30 owners lose lanes, you get one line that identifies none of them (see finding 9).

## 5. `RegistryBucket.TryRegister` fails silently on duplicate, and silently on everything in a player

- `Assets/_Project/Scripts/Core/RegistryBucket.cs:87-124`
- Shape: **a.** Blast radius: any owner whose `OnEnable` runs twice — pooled respawn, additive reload,
  cross-lane repair.

Three failure returns, unequal treatment:

```csharp
if (item == null)         { …LogError once…  return false; }   // :90-99   speaks
if (_count >= _capacity)  { …LogError once…  return false; }   // :101-110 speaks
if (Contains(item))                          return false;     // :112-113 SILENT
```

A duplicate registration is a *success* condition — the owner is already in the bucket — reported through
the same `false` channel as a hard capacity failure, with no log. The caller pattern is
`_registered = GlobalRegistry.TryRegisterSlowTickable(this, …)`, so the flag now says "not registered"
while the owner **is** in the bucket and ticking. The next teardown path guarded by `if (!_registered)
return;` then skips its unregister, and a destroyed object stays in the bucket.

And the entire diagnostic block is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. The release branch is:

```csharp
#else
    if (item == null || _count >= _capacity || Contains(item))
        return false;
#endif
```

In a shipped player, **all three** failure modes — null, capacity exhaustion, duplicate — are one silent
`false`. A player-only lane exhaustion is undiagnosable by construction.

## 6. 817 lane-registration call sites; 35 check the result

- Shape: **a**, at industrial scale. Blast radius: additive across the whole simulation.

`GlobalRegistry.TryRegisterSlowTickable` (`:6540`) has five distinct `return false` paths: null item,
`!isPlaying`, null dispatcher, bucket rejection, and `SystemDispatcher.Register` rejection — that last one
rolling the bucket entry back at `:6555`. Of those five, exactly one speaks, once, globally.
`SystemDispatcher.Register` itself returns bare `false` on lane rejection with no log at all
(`SystemDispatcher.cs:1332-1344`, `:1375-1387`, and the `IFastTickable`/`IFixedTickable` overloads).

Against that, **817** call sites and **35** that consume the result in an `if`. The dominant idiom is a
bare assignment to a `_registeredX` bool that nothing subsequently audits. An owner that fails to claim a
lane is constructed, enabled, census-visible, holding its `ResetToMax()` sentinel values, and emitting
nothing — indistinguishable in every log from an owner that is working and simply has nothing to report.

Only one file in the tree reports a registration gap at all: `HectonSurvivalSystem.cs`
(`ReportTickOwnerRegistrationGapIfNeeded`, called at `:855`). It is the template; see the log-line
prescriptions below.

## 7. `GlobalRegistry`'s own buckets are never checked for destroyed entries

- Validator: `Assets/_Project/Scripts/Core/RegistryBucket.cs:210-228`
- Call sites: **10, all on `SystemDispatcher` lanes** — `SystemDispatcher.cs:5289`, `:5557`, `:6258`,
  `:6289`, `:6389`, `:6444`, `:6503`, `:6801`, `:6871`, `:6925`
- Call sites on `GlobalRegistry._updatables` / `_slowTickables` / `_fixedTickables` / …: **zero**
- Shape: **a.** Blast radius: half of a double-bookkeeping system has no integrity check.

Registration is double-entry: `GlobalRegistry`'s bucket plus the `SystemDispatcher` lane
(`GlobalRegistry.cs:6550` then `:6553`). Only the lane half is ever validated. A destroyed owner stranded
in a `GlobalRegistry` bucket — which is exactly what finding 5's silent duplicate-return produces — is
invisible to the only detector that exists, and the bucket `Count` continues to include it. Any census that
reports "N slow-tick owners registered" from the bucket side is reporting a number that includes corpses.

## 8. `nameof(ISlowTickable)` is the label for two different lanes

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6503` and `:6801` — both pass
  `nameof(ISlowTickable)` as `bucketName`
- Shape: **g.** Blast radius: the diagnosability of finding 7.

The destroyed-entry error prints the *interface name*, not the lane and not the `PriorityLayer`. Two
distinct slow paths report under one identical string, and `_destroyedEntryLogged`
(`RegistryBucket.cs:20`) latches per bucket, so exactly one report per bucket per session ever escapes.
A reader gets one line naming a C# interface, and must guess which of eight priority layers and which of
two slow paths it came from. This is the `PlayerToolManager`-slot-count-under-a-`SignalBus`-label mistake:
a real number under a name that sends the reader at the wrong subsystem.

## 9. `_dispatcherRegistrationErrorLogged`: one line for N lost owners, naming none of them

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7058-7071`
- Shape: **a + g.** Blast radius: it is the *only* diagnostic behind findings 4 and 6, and it is
  single-shot.

```csharp
if (!_dispatcherRegistrationErrorLogged)
{
    _dispatcherRegistrationErrorLogged = true;
    Debug.LogError("[GlobalRegistry] SystemDispatcher is not registered. …");
}
```

The message names the *dispatcher*, which is the shared cause, and omits the *owner*, which is the thing
that went dark and the thing a reader needs. It is also `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so it does
not exist in a player. One suppressed, latched, editor-only error is the entire evidence budget for a
failure mode that can take out thirty systems.

## 10. `allocator=` prints a 6→3 lossy derivation while the truth sits in the same struct

- `Assets/_Project/Scripts/Core/NativeMemorySentinel.cs:1426` (print), `:2350-2361` (derive),
  `:996` (assign), `:75` (the field it should have printed)
- Shape: **g.** Blast radius: every scene-unload leak verdict.

`ResolveAllocator(NativeAllocationLifetime)` maps a **6-value** enum
(`Scene`, `Session`, `Permanent`, `TransientArena`, `Temp`, `TempJob` —
`Core/Contracts/NativeAllocationContracts.cs:9-17`) onto **3** Unity `Allocator` values, with
`default: return Allocator.Persistent` collapsing four lifetimes into one. The leak report then prints
that derived value under the name `allocator=`.

The line is a **scene-lifetime leak** report. The single distinction it must convey — was this buffer
declared `Scene`, `Session`, `Permanent`, or `TransientArena`? — is precisely the distinction the
derivation destroys, and those four verdicts require opposite work: fix the buffer, or fix the declaration.
`record.Lifetime` is on the same struct at `[FieldOffset(292)]` and is read four lines later at `:2373`.
The truth was in hand and the lossy alias was printed instead.

This instance is **half-fixed**, which is why it ranks. The docstring immediately above it
(`:1408-1414`) documents that this exact line previously could not say which scene unloaded or what the
reader should do, that "ten lines per run survived several sessions undiagnosed", and that
`unloadedScene=` and `ACTION=` were therefore added. Somebody audited this line, fixed two omissions on
it, and left the lossy field in place.

## 11. A 33-assertion ARM64 layout guard that cannot run on ARM64

- `Assets/_Project/Scripts/Atmosphere/AtmosphereMemorySovereigntyValidator1323.cs:1` (`#if UNITY_EDITOR`),
  `:11` (`[InitializeOnLoadMethod]`), `:51-52` (throw on failure)
- Same shape in `AtmosphereMemorySovereigntyValidator1324.cs`
- Shape: **a.** Blast radius: DTO layout across atmosphere signals, telemetry, and gas dynamics.

33 size/offset assertions folded into a `ulong failureMask`, then
`throw new FatalArchitectureException(… mask=…)`. Throwing on failure is the *correct* loud behaviour and
I am not flagging that. Two things are:

1. It is silent on success, so "33 layout invariants verified" and "this validator no longer runs" produce
   identical logs. The mitigating factor is real — the assertions use `nameof`, so a field rename is a
   compile error, not a silent pass — but a type rename, an `#if` change, or a
   `[StructLayout]`/`[FieldOffset]` edit that keeps the names is not covered.
2. It is `#if UNITY_EDITOR`. The invariants it enforces are *ARM64 determinism* invariants. They are
   checked only on the desktop editor, and are absent on every platform they exist to protect.

## 12. `AcousticEchoLocationRuntime.TryRunStaticSelfAudit` has zero callers

- `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:405-430`
- Shape: **a**, terminal form. Blast radius: 11 unchecked layout/behaviour invariants in creature sensory.

An 11-bit `failureMask` audit ending in `return failureMask == 0u;`. A search of `Assets/_Project` for
`TryRunStaticSelfAudit` returns three hits: this declaration, the unrelated
`TopographicalSonarSynthesizer.cs:609` declaration, and its EditMode test
(`Tests/Editor/TopographicalSonar/TopographicalSonarLayoutEditTests.cs:39`). **This one has no caller and
no test.**

This is the `PlayerInventory` situation exactly: a guard whose passing state and whose non-existence are
the same empty log. Two sessions were spent on that one. It costs one line to make it impossible again.

The other 10 `failureMask` guards, for completeness — kept as a group because each is the same shape at a
smaller radius, most reachable only from an editor window or a smoke tester:
`UtilityAICognitionVault_AnxietyDecay.cs:463`/`474` (called from `Editor/AnxietyProfileLayoutGuard.cs:12`),
`TopographicalSonarSynthesizer.cs:609`/`624`, `World/Biomes/BiomeTransitionManagerRuntime.cs:1748`
(called from `Editor/BiomeTransitionTunerWindow.cs:182`),
`Scavenging/ScavengingLootOracleRuntime.cs:1523` (self-called at `:2804`),
`HectonFluidEngine.cs:3648`, `Core/Diagnostics/AsynchronousTelemetryExporter.cs:172`,
`Lighting/InteriorGIProbeVolumeRuntime.cs:242`,
`Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:300`,
`World/ProceduralCoral/ProceduralCoralVault.cs:748`,
`World/Editor/ProceduralWreckGeneratorMemorySovereigntyValidator1328.cs:95`.

## 13. Empty `catch` over save-path temp files — the stranded-artifact class

- `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1023`
- `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs:3463`
- `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:550`
- `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:2265`
- Shape: **d.** Blast radius: bounded but nasty — a stranded file that changes a *later* run's behaviour.

`File.Delete(tempPath)` inside `catch (IOException) { }` / `catch (UnauthorizedAccessException) { }`. This
is the precedent shape: a `run.flag` delete inside `catch {}` left a flag that hijacked every subsequent
batchmode launch. A stranded `.sav.tmp` in the save directory is the same category of hazard, and the delete
failing is exactly the case where you most want to know.

I am **not** listing the other ~1140 empty catches. The large majority are editor tooling, importer
cleanup, and cancellation (`catch (OperationCanceledException) { }`), where the silence is harmless.

Worth naming as the **counter-example that shows the lesson landed once**:
`SaveSystem/SaveStateMerkleTree.cs:1476-1480`, where the same `File.Delete(restoreTempPath)` in a `catch`
carries the comment *"A stranded restore temp must never become the crash path; File.Copy below fails
loudly"*. That is the standard the other four should meet.

## 14. `IsServiceReady => IsInitialized` — seven aliases, one bit

- `ConstructionManager.cs:443`, `Gameplay/PlayerActionController.cs:166`, `SpatialAudioManager.cs:1750`,
  `SaveManager.cs:162`, `Visor/InternalFloodWaterlineRuntime.cs:163`,
  `Fauna/FaunaSimulationEngine.cs:58` (`=> IsReady`),
  `Physics/Vehicles/Automation/DockingAutopilotService.cs:327` (`=> IsReady`)
- Shape: **g.** Blast radius: low per site, but it is the mechanism that made finding 1 possible.

Two property names, one bit, per owner. On `SpatialAudioManager` the two names genuinely differ —
`IsAudioRuntimeReady` there is a five-term conjunction (`:1739-1744`) while `IsServiceReady` is the bare
flag — which is precisely how a reader learns to treat the names as interchangeable, and precisely how
`NoOpAudioService` inherits "ready" from the default alias in `GlobalRegistryContracts.cs:1573`. Listed
last because no individual site is a defect; the *convention* is the defect, and finding 1 is what it
costs.

---

# PART 3 — THE LOG LINE THAT SHOULD EXIST

One sentence each, for the ten highest-radius findings. The in-repo model to copy is
`WorldProceduralScatterDirector.ReportInertPlacementOwner` (`WorldProceduralScatterDirector.cs:934`,
message constant and rationale at `:920-949`), which fires in the current probe run at
`h8_worldsim_probe5.log:7686` and states three things in one line: **what capability went dark, why the
callback that would have registered it never ran, and the exact action that fixes it.** Every line below
follows that template. All of them must use `Hecton8.Core.H8Debug` if they can land in a tick path; a
serialized bool does not satisfy AGENTS.md:271 because the call still ships.

| # | finding | the line that should exist |
|---|---|---|
| 1 | `NoOpAudioService.IsInitialized => true` | `[GameBootstrapper] AUDIO IS A STUB: IAudioService slot holds NoOpAudioService, which reports IsInitialized=true and IsAudioRuntimeReady=true while discarding every queued event. No SFX, no ambience, no music, no vocal warnings, no acoustic zones for this entire session. Consumers that gate on IsInitialized WILL pass. Fix the SpatialAudioManager node named in the preceding cause line.` |
| 2 | dropped `reason` | `[GameBootstrapper] Injected NoOp audio service. cause=<reason> ACTION=<author the SpatialAudioManager component / fix its IAudioService registration / fix the throw reported below>` — and delete the `#if` wrapper at `:6573-6575` so the swap is not invisible in a player. |
| 3 | discarded exception | `[GameBootstrapper] SpatialAudioManager.InitializeService threw <exception.GetType().Name>: <exception.Message> — audio will be replaced by a silent stub. <exception.StackTrace>` |
| 4 | 108 pre-guards | At every one of the 68 affected files: `[<Owner>] Dispatcher was null at lane-claim time, so this owner holds no <lane> lane and <named capability> will not run this session; nothing retries. Bootstrap must register SystemDispatcher before <Owner>.OnEnable.` |
| 5 | `RegistryBucket` duplicate | `[RegistryBucket<T>] Duplicate registration rejected for <owner>; it is ALREADY in this bucket, so the caller's registered-flag is now false while the owner is live and ticking, and its teardown will skip unregistering.` — and move the block out of `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` into a fixed-size telemetry ring so a player build can report lane exhaustion. |
| 6 | 817 unchecked results | On every `false`: `[<Owner>] TryRegister<Lane>(<PriorityLayer>) returned false — reason=<null item \| not playing \| null dispatcher \| bucket full N/N \| lane full N/N>. <named capability> is dark for this session.` The reason must be an out-param; today all five collapse into one `false`. |
| 7 | unvalidated buckets | `[GlobalRegistry] Destroyed <T> remained registered in bucket _<name> at index i (owner=<name>) — bucket Count=<N> is overstated by at least one and any census reading it is wrong.` Call it from the same place the lane validators are called. |
| 8 | ambiguous bucket label | Replace `nameof(ISlowTickable)` with the lane identity: `[RegistryBucket] Destroyed entry in lane=Slow layer=<PriorityLayer> path=<bucketed\|direct> index=<i>.` |
| 9 | latched dispatcher error | Drop the global latch in favour of per-owner reporting: `[GlobalRegistry] SystemDispatcher is not registered; <Owner> is the <N>th owner denied a lane this boot. Denied so far: <ring of owner names>.` |
| 10 | `allocator=` | `… lifetime=<record.Lifetime> allocator=<derived, informational only> …` — print the 6-value field that decides the verdict, and mark the 3-value derivation as derived so no reader mistakes it for the declaration. |

Findings 11-14 need source changes rather than lines: 11 needs the validator compiled into player builds
(or an equivalent runtime layout assertion on the platform it protects); 12 needs one caller and one
success line; 13 needs the delete failure logged with the stranded path; 14 needs the default interface
implementation at `GlobalRegistryContracts.cs:1573` removed so a null object cannot inherit "ready".

---

# PART 4 — SEARCH BOUNDS, HONESTLY

## What I covered

- `Assets/_Project/Scripts`, 3204 `.cs` files, 2 255 633 lines, via scoped `rg`
  (`-g '!Library' -g '!Temp' -g '!obj' -g '!.git'`).
- Patterns actually run: `failures == 0`, `failureMask`, `errorMask`, `violationMask`, `mismatchMask`,
  `catch\s*(\([^)]*\))?\s*\{\s*\}` (multiline), `catch (Exception)`, `if (!Application.isPlaying`,
  `if (!Application.isPlaying || <expr> == null`, `GlobalRegistry.TryRegister*`, `float/int.MaxValue`,
  `float/int.MinValue`, seeded-fold names, `IsInitialized`, `bool Is\w+ => Is\w+;`,
  `class (NoOp|Null|Dummy|Stub|Fallback)\w*`, `[Range(`, `ValidateNoDestroyedEntriesDebug`,
  `TryRun\w*(SelfAudit|Audit)`, `allocator=`.
- Read in full: `Core/RegistryBucket.cs`, `Core/Contracts/NativeAllocationContracts.cs:9-17`, and the
  relevant regions of `Core/GlobalRegistry.cs`, `Core/SystemDispatcher.cs`,
  `Bootstrap/GameBootstrapper.cs`, `HectonSurvivalSystem.cs`, `SpatialAudioManager.cs`,
  `Core/NativeMemorySentinel.cs`, `WorldProceduralScatterDirector.cs`,
  `Atmosphere/AtmosphereMemorySovereigntyValidator1323.cs`.
- `Logs/h8_worldsim_probe5.log` (1.92 MB): targeted extraction, then the audio-bootstrap region
  `:1895-2005` read in full, plus keyword frequency folds over the whole file.

## What I did NOT cover — treat these as unswept, not clean

1. **`.compute`, `.hlsl`, `.shader`.** Not searched at all. A shader that early-`return`s on a failed
   sampler bind or writes a fallback colour is the same shape and this sweep says nothing about it.
2. **The 4 binary scenes and all `.prefab` assets.** A guard on a serialized bool that is authored
   `false` in a scene is invisible to a source search. I did not run
   `python Tools/SceneGuidReachability.py`, so every reachability claim here is from source structure and
   the probe log, not from scene binding.
3. **`Assets/_Project/Editor`, `Tools/`, `Assets/Crest`, `Assets/Plugins`, third-party.** Out of the
   searched root or out of lane. `Assets/_Project/Scripts/Editor` was included only because it lives under
   the searched path; I did not sweep it deliberately.
4. **The remaining ~1140 empty catches.** I filtered to file-mutation and destructive ops. A swallowed
   failure in the other 1140 that surfaces "later and elsewhere" as a wrong *number* rather than a wrong
   file would not have matched my filter.
5. **The remaining 268 sentinel folds.** I sampled 15 and reasoned from the fold's variable name. A fold
   named `bestScore` that is secretly a min-fold would read as correct in my sample and be wrong in the
   code. This is the single weakest claim in the document.
6. **Shape (f) beyond `[Range]` fields.** I checked serialized `[Range]` fields for reads and found no
   real instance. I did **not** systematically hunt method parameters that are accepted and ignored;
   finding 2 was discovered while tracing finding 1, not by a parameter sweep. `semgrep` or
   `ast-grep` would do this properly and I did not run either.
7. **Reachability of the pre-guard sites.** I proved the *shape* at all 108 by source read. I did **not**
   prove for any of them that `GlobalRegistry.Dispatcher` can actually be null at that moment in the real
   boot order. If bootstrap guarantees the dispatcher before every one of those `OnEnable` calls, the
   suppression is latent rather than live. `HectonSurvivalSystem.cs:832-840` asserts the null-at-OnEnable
   route is real for at least one owner; whether it generalises is unproven.
8. **Whether findings 4-14 are firing right now.** Only findings 1-3 have live-log proof. Everything in
   Part 2 is a static structural claim about what the code *cannot* report. Consistent with the lane
   premise: `h8_worldsim_probe5.log` contains **zero** matches for
   `SystemDispatcher is not registered`, `RegistryBucket`, or `capacity … exceeded` — which given findings
   4, 5, 6 and 9 is exactly as informative as an empty file. Absence of those lines is not evidence that
   no lane was lost.

## No compile or runtime proof

I cannot compile and cannot run Unity. Every line-number citation is from a source read at this commit;
no assembly was built and no scene was played to confirm any of it. Nothing here is `VERIFIED`.
