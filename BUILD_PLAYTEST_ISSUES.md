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
| The content vacuum, measured: 4 items, 3 creatures, 0 quests | `[!]` | run the authoring generators, re-bake, and re-read this census |

### The Data Monolith content census — measured from the shipped blob, 2026-07-29

The vacuum is no longer an impression. `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
(7,457,664 bytes) was parsed byte-wise and every count below comes out of the section table, not out of a
document. Two self-checks passed before any number was believed: the first non-empty section starts at 576,
which is exactly `AlignUp(HeaderSizeBytes 64 + DirectorySizeBytes 64 + 28*16, 64)`, and the last section ends
at 7,457,664 — the file size to the byte. Field order taken from `H8DataSectionEntry`
(`H8DataMonolithTypes.cs:249-255`): `SectionId(0), RecordSize(4), Count(8), OffsetBytes(12)`. Names from
`H8DataSectionId` (`:87-117`). My first attempt had `Count` and `OffsetBytes` transposed and the self-check
caught it — without that check the numbers below would have been fiction.

**Empty — zero rows, offset zero:**
`QuestNodes` (6), `QuestEdges` (7), `NarrativeTriggers` (15), `RadiationIntensityMap` (18).
The quest graph has no nodes AND no edges. The narrative trigger table is empty.

**Authored gameplay tables, all stubs:**
`Items` 4, `Creatures` 3, `Recipes` 3, `Biomes` 2, `VoxelMaterials` 2, `LootCdf` 4, `AudioClipRegistry` 3,
`VfxScalars` 2, `ToolHeatCapacity` 2, `SubmarineHullConstants` 2, `PhysicsMaterials` 3, `GhostModules` 2,
`SpawnCreditCosts` 3, `SopErrors` 2, `HudLayouts` 2, `SectorPageDirectory` 2, `Economy` 3,
`PhysicsConstants` 3.

**Genuinely populated — and note what they have in common:**
`BiomeHeatmap` 65536, `DepthPressureCurve` 256, `LightAttenuationCurve` 256, `LocalizationUtf8` 5,444,599
bytes, `AppliedLorePackets` 6960, `AppliedLoreRoutes` 458.

The pattern is the finding. **Every machine-generated grid or curve is full, the lore corpus is full, and
every hand-authored gameplay table is a 2-to-4-row stub.** That is the shape of a pipeline where the
generators for procedural data and the lore lane both ran, and the authoring lane never did. A survival game
with four items and three creatures has no economy, no crafting tree and no bestiary to balance, regardless
of how good the systems consuming them are.

**Why boot never told anyone.** A zero-length section is structurally legal, through two independent
bypasses rather than one — patching either alone would not help:
- `H8StaticDataArena.cs:3388` — `if (section.Count == 0u) return section.OffsetBytes == 0u;` so range
  validation short-circuits.
- `:3255` — the contiguity walk in `IsDirectoryValid` is wrapped in `if (section.Count != 0u)`, so an empty
  section consumes no layout budget and breaks no adjacency.
Then `:1009` closes it: every public accessor is a `TryGet*`/`TryFind*` returning false on an empty span, so
an empty section never crashes and never logs — it fails every lookup, forever, in silence. Only
`IsAppliedLoreContractValid` (`:3280`, floor check at `:3291`) enforces a minimum count, and only for the two
AppliedLore sections, which is exactly why those two are the populated ones.

**The census must NOT abort boot, and this is load-bearing.** A minimum-count floor of 1 applied to all 28
sections would reject the blob that is on disk right now — ids 6, 7, 15 and 18 are empty — turning
`InvalidSectionTable` into a dead boot with a misattributed cause, since that is the same status code a
genuinely corrupt table produces. Fail-closed belongs in the editor bake and the test suite, where a bad
build is never produced in the first place; the runtime gets a diagnostic. That split is also why the
minimum-count table belongs in `H8DataLayoutAudit` (`H8DataMonolithTypes.cs`, beside `GetExpectedRecordSize`
at `:749-783`) rather than in the arena — that class is already the shared audit surface for tests, editor
bakes and boot guards, so the baker's idea of "required" cannot drift from the loader's.

**Emission trap, worth naming before someone trips it.** `H8StaticDataArena.cs` contains zero `Debug.Log`,
`LogError` or `LogWarning` calls across all 4,162 lines; its only outward channel is
`H8DataBlobLoadStatus`. Do not add a `LoadedWithEmptySections` member to that enum — it is consumed as
pass/fail and every `status == Loaded` comparison in the codebase would silently stop matching. The 28-bit
deficit mask fits a single `uint`, and `H8DataMonolithTelemetryEntry` (`:716`) has spare reserved uints at a
fixed 64-byte size, so it can carry the mask with no allocation and no string.

**Two smaller findings from the same parse.** The world-seed and app-version bindings at `:3157` and `:3163`
are guarded by `expected != 0u && _directory.X != 0u`, so a blob baked with `AppVersionHash` 0 silently
matches every app version. And `visual_tuning.h8bin`, the sibling artifact, is 64 bytes — header and
directory only, no payload.
| A failed save is shown to the player as a completed save | `[!]` | force a save write failure in a build and watch the HUD |

### A failed save is rendered as success — verified 2026-07-29

This is the worst-shaped defect found so far: it does not hide a failure, it reports it as a success, in a
survival game where the player's decision to keep playing depends on believing the save landed.

**The publisher is not at fault.** `SaveManager` raises the failure lane on every failure path — the
verified-pipeline failure at `:5701-5729` (failureCode 3) and the catch-all at `:5758-5762` (failureCode 1)
both funnel into `HandleSaveFailure`, which calls `SaveEvents.TryRaiseSaveFailed` at `SaveManager.cs:5476`
next to the `LogError`. Eleven more preflight and reject paths raise it too (`:2196`, `:2205`, `:2216`,
`:2292`, `:2303`, `:5046`, `:5486`, `:5495`, `:5507`, `:5520`, `:5536`). It does not merely log.

**The break is on the subscriber side, and there are exactly three candidate surfaces. None works.**

1. `HUDSaveNotificationLink` is the ONLY component in the repository that renders a save failure to the
   HUD — `notificationSystem.ShowCritical` for `SaveFailed`/`LoadFailed` at `:88-92`, building the literal
   `"SAVE FAILED"` at `:142`. Its script guid `473b7a7cc5029354e85995ce5c763e8f` appears in ZERO `.unity`
   and ZERO `.prefab` files, including zero nibble-swapped hits in the binary scenes, and it has no
   `AddComponent` site in any `.cs` — the only matches are two editor smoke testers that read its source as
   text. So `SaveEvents.Register(this)` at `:43` never runs and the component never exists.
2. `PauseMenuController.HandleSaveFailed` (`:579-581`) is real code, but the controller's only construction
   site is `PauseMenuHost.cs:38`, and `PauseMenuHost` (guid `99b935f9beb2c9d48a71477cfadfbaea`) is itself in
   zero scenes. Same one-link-too-short chain as `PDAMapTab` behind `PDASpectrumTab`.
3. `SuitHUDV4CanvasOverlay` IS reachable — and it is the one that lies. `OnSaveEvent` at
   `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:1747-1748` reads:
   `if (eventType == SaveEventType.SaveCompleted || eventType == SaveEventType.SaveFailed)`
   `    RequestSavingProgressHide();`
   One branch for both outcomes. `RequestSavingProgressHide` (`:1792`) sets
   `_savingProgressTargetAlpha = 0f`, so on a failed write the saving indicator fades out exactly as it does
   on a successful one. Visually indistinguishable from "saved".

**Why this is filed rather than fixed by me.** Checked directly: `SuitHUDV4CanvasOverlay` owns no
notification API at all — zero matches for `ShowCritical`, `ShowWarning` or `Notification` in the file. It
owns only the saving-progress indicator (`_savingProgressRoot`, `_savingProgressDataLamp`,
`_savingProgressTargetAlpha` and siblings). So every way of making a failure visible from inside that class
is a new player-visible visual state, which `AGENTS.md` puts behind the Visual Reference Parity Gate and
`TASTE.md`, and that gate needs the reference folder plus a capture I cannot produce without a Unity slot.
Inventing a failure visual and calling it done would be exactly the unverified visual claim the rules reject.

**The fix that needs no visual judgement, and is the recommended one.** Wire up
`HUDSaveNotificationLink`. Its presentation is already authored inside it, so activating it invents nothing.
Candidate live hosts identified: `SuitHUDPresentationController.cs:705` and `SuitHUDV4CanvasOverlay.cs:633`.
Open questions before wiring: whether the link resolves its notification system itself or needs it injected,
and whether its `Register`/`Unregister` pair survives a host created and destroyed per scene load.

**Second, independent fix, also low risk:** split the `SaveFailed` branch out of `OnSaveEvent:1747` so a
failure stops sharing a code path with a completion. Keep `RequestSavingProgressHide` for `SaveCompleted`
only. What the failure branch should then do is the visual decision above.

**Third:** `SaveStation` reports nothing about the save the player explicitly asked for. It already has
`ShowWarning` plumbing at `:316` and a lazy HUD notification resolve at `:194`, but implements no
`ISaveEventListener` and contains no failure branch.
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
- Consumers hold a field that can only ever be null — `PrologueSequenceRegistryBridge.cs:56`, `:324`, `:608`
  and `PDAMapTab.cs:187`. **Corrected 2026-07-29: neither consumer is reachable either, so both null reads
  are LATENT, not live.** I first wrote "live consumers" and that was an overstatement.
  `PrologueSequenceRegistryBridge` has no construction site anywhere and guid
  `45870ac22097485c8af3756f9b82f96f` returns 0 hits across all 999 scenes and prefabs, so `_service` is null,
  `OnEnable` bails at `:136-140` publishing `MissingServiceHash`, and `CacheRuntimeServices()` is never
  reached. `PDAMapTab` is created only at `PDASpectrumTab.cs:390`, and `PDASpectrumTab` itself has no
  construction site and is in no scene — so the chain stops one link earlier than I claimed. The defect is
  real and still worth fixing; it is queued behind whoever instantiates those two lanes.

**Third instance of the same shape, found 2026-07-29.** `IOrbitalDirector` is also permanently null in
`PrologueSequenceRegistryBridge` (`:55`, cached at `:607`). Its sole implementation
`Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:23` is a MonoBehaviour with no
construction site and guid `a157e4fc116ddcb47959dc414b43d02c` returns 0 hits across all 999 files. Unlike the
streaming case the registry is NOT the obstacle: the component self-registers at `:354` and
`OrbitalDirectorRuntime` is absent from the deny list at `GlobalRegistry.cs:7160-7186`, so registration would
succeed the moment the component exists. Cost inside the bridge: `TryGetOrbitalSnapshot` (`:194-212`) always
returns false so the prologue never receives universe velocity, planet distance, reentry heat or cloud
whiteout; `ZeroUniverseVelocity` (`:499-503`) is a silent no-op through `orbital?.`; and
`TryConsumeOrbitalWhiteoutFallback` (`:825-861`) bails at `:830`, removing one of three prologue-complete
fallback paths. Also absent from every scene in that lane:
`Prologue/Space/PrologueWorldHandoffSceneLoader.cs` and `Prologue/VFX/OrbitalDropReentryVfxController.cs`;
only `Prologue/Space/PrologueOrbitSceneBootstrap.cs` is wired.

**Two services in that same file are FINE, checked so nobody re-audits them.** `ITickDispatcher` (`:609`):
`SystemDispatcher` is `AddComponent`ed at `GameBootstrapper.cs:5874` and `GameBootstrapper` drives itself
from `[RuntimeInitializeOnLoadMethod]`, so it needs no scene presence; the bridge also degrades to
`SystemDispatcher.CurrentFrameDeltaTime` when it is null (`:722-724`, `:1080-1082`). `IInputService`
(`:657`) structurally cannot be null — the property returns a `NoOpInputService` null object at
`GlobalRegistry.cs:936`.

**The PDA is NOT a dead feature, and recording that is the point.** All 21 `PDA*` MonoBehaviours are absent
from every one of the 999 scenes and prefabs, which reads as a whole handheld device written and never
wired. It is not: the PDA is built programmatically. `PDARuntimeInstaller.EnsurePlayerSystems(playerObject)`
is invoked from `GameBootstrapper.cs:8031`, `ProgressionRuntimeInstaller.cs:49` adds `PDADeathMemoryDump`,
and tabs build their own sub-panels — `PDAInventoryTab.cs:883` and `:1531`, `PDAAtlasSignalTab.cs:496`.
Scene absence proves nothing for a code-constructed UI, exactly as it proves nothing for a
non-MonoBehaviour service. The one genuine gap in that lane is `PDASpectrumTab`: no construction site, no
scene, and it is the only creator of `PDAMapTab`, so both are unreachable.

**Method, improved on the earlier pass.** Header-test all 999 scene and prefab files for `%YAML` first:
exactly 4 are binary (`02_HECTON_WORLD.unity`, `010_TEST.unity`, `020_RENDER_SANDBOX.unity`,
`020_RENDER_SANDBOX_V2.unity`). For the 995 text files a plain guid grep is exact; only the 4 binary ones
need the nibble-swapped byte order. Control `547a39a8034a57a47b65413eb12885d2` (WorldStreamingDirector)
returns 6 hits, including two independent binary scenes in swapped form and 0 in raw form — that is what
makes every negative above meaningful. And always pair a scene negative with a construction-site search: an
absent MonoBehaviour that something `AddComponent`s is not a finding.
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
