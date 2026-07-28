# HECTON-8 Build / Playtest Issues

Date: 2026-06-09
Status: PENDING VERIFICATION
Owner: build/playtest issue anchor
Evidence: STATIC_DOC only unless a build/playtest artifact is cited

## Authority

This file tracks current player-facing blockers only. Historical full ledger copy:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`

Do not mark `[x]` without current player build, Play Mode, user confirmation, profiler, GCMonitor, or visual artifact as appropriate.

## Current Build Evidence

Last recorded full-solution CLI PASS — `ARTIFACT MISSING`, so the claim below is
`PENDING VERIFICATION`, not evidence:

- Cited artifact: `Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log`
- Command recorded: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Recorded result: exit `0`, proof lines `66 Build succeeded.`, `67 0 Warning(s)`, `68 0 Error(s)`
- Evidence class: CLI_COMPILE only
- **Status, verified 2026-07-28: the log file does not exist anywhere in the repository.** A
  repo-wide search for that basename returns nothing, and `Docs/Reports/` holds nine other
  `.log` files but not this one. Per `AGENTS.md` `Evidence Law`, a recorded exit code whose
  artifact is gone is not proof, so the full-solution CLI compile status reverts to
  `PENDING VERIFICATION` until a build is re-run and its log committed.

Nearest surviving artifact, and why it does **not** substitute:
`Docs/Reports/Compile_20260726.log` (2026-07-26, exit code 0, `Exiting batchmode successfully now!`)
is a Unity batchmode run, not a `dotnet build` of `Hecton8.slnx`. It carries no MSBuild
`N Warning(s)` / `N Error(s)` summary because it is a different proof class. Substituting it here
would be fabricated evidence. Whoever re-runs the CLI build should replace this whole block with
the new log path and its real summary lines.

The historical record above supersedes older root-doc statements for that dated source state only.
It does not authorize a new build attempt by itself, and it never proved Unity import, Play Mode,
player build, profiler, GC, scene wiring, or visual quality.

Before any new `dotnet`, Unity import, Play Mode, profiler, player build, asset reimport, or equivalent heavy proof action, apply the current process gate from `AGENTS.md` and `performance.md`: sample CPU plus active Unity/compiler/import/build processes. If CPU is above `50%`, `dotnet`/`csc`/Unity import/build is active, or the Unity slot is contested, report `BUILD_GATE_BLOCKED: <reason>` and continue with static/scoped work only.

Not proven by that log:

- Unity import
- Unity Console
- Play Mode
- player build
- profiler/GCMonitor
- save/load
- scene wiring
- visual quality
- platform readiness

## Open Product Blockers

| Blocker | Status | Proof Needed |
|---|---|---|
| Surface transition hitch | `[c]` | player/build swim while crossing surface and rotating camera |
| Surface oxygen refill | `[c]` | depleted-O2 surfacing test in build |
| Pause cursor and button focus | `[c]` | build check for cursor, lock state, Esc flow, button actions |
| Surface/interior/underwater audio | `[~]` | snapshot assets, runtime transition proof, player ambient source verification |
| Menu -> world start context | `[c]` | clean new/load/resume path in build |
| Save/load return route | `[~]` | current write/read/migration/corruption artifact |
| First 20 Minutes Copper Wire route | `[~]` | full route clip plus profiler/GC/memory capture |
| Data Monolith runtime boot | `[~]` | Unity import/player boot/checksum proof for `static_data.h8bin` |
| RT/VRAM retained owner set | `[!]` | Memory Profiler / Frame Debugger owner isolation |
| Tool durability does not persist | `[!]` | load a save after breaking a tool and read its durability back |
| Two MonoBehaviour services cannot reach a scene | `[!]` | a pre-Ready bootstrap lane, then Play Mode proof that both slots resolve non-null |

### Two sole-implementation services are unreachable at runtime — verified 2026-07-28

Not a lead: every step below was checked by hand, and the scan method was validated against a control
before any negative result was accepted.

**1. `WorldChunkResidencyManager`** — `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:590`,
6536 lines, `MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IBaseAirlockEventListener,
IStreamingBackpressureService, IGlobalRegistryHotSwapListener, IDisposable`.

- It is the ONLY type implementing `IStreamingBackpressureService`.
- Zero construction sites: no `new WorldChunkResidencyManager(` and no
  `AddComponent<WorldChunkResidencyManager>` anywhere under `Assets`.
- Absent from all 999 `.unity` and `.prefab` files, by class name AND by script GUID
  (`8de4f944c53c4f448bff65e8fd01a4db`).
- It is deliberate, and the reason is written down at `WorldRuntimeInstaller.cs:116-125`: the slot
  `GlobalRegistryServiceSlot.StreamingBackpressureRuntime` is HARD-DENIED by
  `GlobalRegistry.IsSceneRuntimeHotSwapSlot` (`GlobalRegistry.cs:7182`), the publication gate cannot issue
  a token for a denied slot, `OnEnable` registration at `:2491` would throw `CriticalBootException`, and
  installers run in sequence with no `try`/`catch` — so adding it there would abort every installer after
  it. That comment names the fix itself: "It needs a pre-Ready bootstrap lane, not this one."
- Live consumers therefore hold a field that can only ever be null:
  `PrologueSequenceRegistryBridge.cs:56`, `:324`, `:608` and `PDAMapTab.cs:187`.
- Player consequence: no chunk residency, no load/unload radius, no streaming backpressure, no far-field
  HLOD impostors, and the `BufferID.WorldChunkResidencyManager_ActiveImpostor*` DataVault entries are never
  populated. `AGENTS.md` `Memory Management & Chunk Dispose` has no owner on this route.

**2. `AssetLifecycleGovernor`** — `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:27`,
`MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IAssetLifecyclePressureSink,
IGlobalRegistryHotSwapListener`. Sole implementation of `IAssetLifecyclePressureSink`. Its only two
construction sites are its own tests — `Tests/PlayMode/Optimization/AssetLifecycleGovernorTickTests.cs:17`
and `Tests/Editor/Optimization/AssetLifecycleGovernorDumpTests.cs:18`. Absent by name and by GUID
(`0e7cdf0573f867d4983dde747e5c4c22`) from all 999 scene and prefab files. Its slot
`AssetLifecycleRuntime` sits in the same hard-denied switch block.

**The negative result matters as much, and is recorded so nobody repeats the near-miss.** The denied block
at `GlobalRegistry.cs:7178-7184` covers SEVEN slots, and all seven have exactly one implementation each
that is absent from every scene — which looks like seven blocked subsystems and is not. Five of them are
plain classes, not MonoBehaviours, with real construction sites in runtime code: `H8MacroDatabaseService`
(4 sites), `BurstTokenBucketJobAdmissionService` (4), `AssetLoadDispatcher` (1), `ModuloSimulationBucketer`
(1), `HardwareThermalService` (1). For a non-MonoBehaviour service, construction in code is the correct
pattern and scene absence means nothing. Only the two MonoBehaviours above need a scene or an
`AddComponent` they never get.

**Method, stated so the next audit can repeat it.** Scene absence was checked by byte-scanning all 999
`.unity` and `.prefab` files for the class name as ASCII and UTF-16LE, and separately for the script GUID
in four forms — ASCII lower, ASCII upper, raw 16 bytes, and the nibble-swapped byte order Unity's binary
serialiser uses. `02_HECTON_WORLD.unity` is binary, so a text grep alone proves nothing: the control
`WorldStreamingDirector` (guid `547a39a8034a57a47b65413eb12885d2`) was found in 6 files, and in the
production scene only in the nibble-swapped form. A negative result without that control is worthless.

Deliberately not fixed here. Building a pre-Ready bootstrap lane changes boot ordering and registry
publication policy, which needs `Docs/SYSTEMS_CONTRACTS.md`, the global-authority route card, and Unity
Play Mode proof that both slots resolve non-null. Filed as a `BLOCKER` with the exact missing condition,
per `AGENTS.md` Deliverable class lock.

### Tool durability does not persist — static evidence, 2026-07-28

Found by auditing first-party runtime against the bans `AGENTS.md` states, rather than by playing.
This one is a functional save defect, not a style violation:

- `Assets/_Project/Scripts/SaveData.cs:153` `public Dictionary<string, float> toolDurabilityMap`
- `Assets/_Project/Scripts/SaveData.cs:156` `public Dictionary<string, bool> toolBrokenMap`
- `Assets/_Project/Scripts/SaveData.cs:365` `public Dictionary<string, string> CustomModData`

These are public fields on the ROOT `SaveData` type, which is exactly what `AGENTS.md`
`Concrete Project Contracts` bans: "Managed-collections with dynamic allocations (e.g.
`Dictionary<string, T>` or `HashSet<string>`) in the root structures of `SaveData.cs` are banned;
serialization must rely on `ISerializationCallbackReceiver` and parallel flat lists."

Measured, not assumed: `SaveData.cs` does **not** implement `ISerializationCallbackReceiver`, has no
`OnBeforeSerialize` or `OnAfterDeserialize`, and carries no parallel flat lists for these three maps.
Unity serializes no `Dictionary` field, so the consequence is that tool durability and broken-tool state
are silently dropped on save and come back empty on load. `Assets/_Project/Scripts/SaveDataMigration.cs`
lines 232 and 254 hold `HashSet<string>` under the same ban.

Deliberately NOT fixed here. Changing root save structures touches save identity and needs
`persistence.md`, the save mandates and `SaveManager.cs` read as owner files first, plus a migration
decision and a real load-after-save artifact. `SaveManager.cs` also had concurrent edits in flight at the
time of this audit. Classified `BLOCKER` with the exact missing condition, per `AGENTS.md` Deliverable
class lock.

Clean in the same audit, across 2078 first-party runtime `.cs` files with comments and string literals
stripped: zero `Camera.main`, `FindObjectOfType`/`FindObjectsOfType`, `GameObject.Find`,
`Resources.Load`, `Resources.UnloadUnusedAssets`, `async void`, `BinaryFormatter`, `renderer.material`
and `renderer.materials`. The four `OnGUI` hits are all inside `#if UNITY_EDITOR`, so they are not
violations. `DontDestroyOnLoad` appears 7 times and `Time.deltaTime` 3 times outside Dev tooling — both
need an owner-route ruling rather than a blanket verdict, and neither is claimed as a defect here.

## Entry Template

```md
## Build Entry - YYYY-MM-DD - Build Name
- Artifact:
- Hardware:
- Scene:
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Evidence class:
- Main blocker:
- Change tested:
- Result:
- Failed:
- Next proof:
```

## Rules

- `[c]` means implementation/static-doc work closed, proof pending.
- `[x]` means current artifact proves the claim.
- Build feel beats editor feel.
- Player route proof beats subsystem count.
- Static source, H-Phi, route cards, and compile logs do not prove runtime quality.
- Visual tasks need screenshot/clip proof.
- Performance tasks need profiler/GC/memory evidence.
- Save tasks need write/read/corruption/migration evidence.
